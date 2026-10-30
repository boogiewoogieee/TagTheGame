using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// The lobby's state: who is in, who is ready and who plays which character. The host owns the list, like the match
// state in MatchManager; players only ever ask to change their own row, and the host
// takes the sender from the network rather than from the message. Lives in the menu
// scene as an in-scene NetworkObject, so it exists exactly while the lobby does.
public class LobbyManager : NetworkBehaviour
{
    [Tooltip("Scene the host loads for everyone when the match starts.")]
    public string arenaScene = "Arena_01";

    [Tooltip("Players needed before the match can start, the host included.")]
    [Min(1)]
    public int requiredPlayers = 2;

    [Tooltip("The characters players can pick. Must be the same asset the Player prefab uses.")]
    public CharacterCatalog characters;

    public NetworkList<LobbyPlayer> Players = new NetworkList<LobbyPlayer>();

    public int ReadyCount
    {
        get
        {
            int count = 0;
            foreach (LobbyPlayer player in Players)
            {
                if (player.IsReady)
                {
                    count++;
                }
            }

            return count;
        }
    }

    // True on the host while the rules allow the match to start.
    public bool CanStart => IsSpawned && IsServer && LobbyRules.CanStart(Players.Count, ReadyCount, requiredPlayers);

    public bool LocalIsReady => TryGetPlayer(NetworkManager.LocalClientId, out LobbyPlayer player) && player.IsReady;

    public int LocalCharacterIndex => TryGetPlayer(NetworkManager.LocalClientId, out LobbyPlayer player) ? player.CharacterIndex : CharacterRules.None;

    public int CharacterCount => characters != null ? characters.Count : 0;

    public override void OnNetworkSpawn()
    {
        // The name was typed in the menu and kept on the NetworkManager, so it is still here
        // when the lobby comes back after a match.
        ConnectionNotes notes = NetworkManager.GetComponent<ConnectionNotes>();
        FixedString64Bytes name = new FixedString64Bytes(notes != null ? notes.LocalPlayerName : "");
        int lastPick = notes != null ? notes.LocalCharacterIndex : CharacterRules.None;
        Players.OnListChanged += OnPlayersChanged;

        if (IsServer)
        {
            NetworkManager.OnConnectionEvent += OnConnectionEvent;

            // A lobby that is open again takes players again (the match locked it).
            SetJoinable(true);

            AddPlayer(NetworkManager.ServerClientId, name, lastPick);
        }
        else
        {
            JoinLobbyRpc(name, lastPick);
        }
    }

    public override void OnNetworkDespawn()
    {
        Players.OnListChanged -= OnPlayersChanged;

        if (IsServer)
        {
            NetworkManager.OnConnectionEvent -= OnConnectionEvent;
        }
    }

    public bool TryGetPlayer(ulong clientId, out LobbyPlayer player)
    {
        int index = IndexOf(clientId);
        player = index >= 0 ? Players[index] : default;
        return index >= 0;
    }

    // Flips this machine's own ready mark.
    public void ToggleReady()
    {
        if (IsSpawned)
        {
            SetReadyRpc(!LocalIsReady);
        }
    }

    // Asks for a character for this machine's player. The host refuses it while the
    // player is ready or when the other player has it.
    public void ChangeCharacter(int index)
    {
        if (IsSpawned && !LocalIsReady && index != LocalCharacterIndex)
        {
            SetCharacterRpc(index);
        }
    }

    // The picks of everyone except this client: the ones it may not take.
    public List<int> CharactersTakenBy(ulong exceptClientId)
    {
        List<int> taken = new List<int>();
        foreach (LobbyPlayer player in Players)
        {
            if (player.ClientId != exceptClientId && player.CharacterIndex != CharacterRules.None)
            {
                taken.Add(player.CharacterIndex);
            }
        }

        return taken;
    }

    // Host only. Locks the door, clears the marks and takes everyone to the arena.
    public void StartMatch()
    {
        if (!CanStart)
        {
            return;
        }

        LockJoining();

        // The lobby is unloaded with this scene, so the arena gets the names through the NetworkManager.
        ConnectionNotes notes = NetworkManager.GetComponent<ConnectionNotes>();
        if (notes != null)
        {
            notes.PlayerNames.Clear();
            notes.PlayerCharacters.Clear();
            foreach (LobbyPlayer player in Players)
            {
                notes.PlayerNames[player.ClientId] = player.Name.ToString();
                notes.PlayerCharacters[player.ClientId] = player.CharacterIndex;
            }
        }

        NetworkManager.SceneManager.LoadScene(arenaScene, LoadSceneMode.Single);
    }

    // Host only. Nobody may join a match in progress, and ready marks mean nothing once it runs.
    public void LockJoining()
    {
        if (!IsServer)
        {
            return;
        }

        SetJoinable(false);
        ResetReady();
    }

    // The gate refuses connections; an online game also leaves or rejoins the list of
    // open games, so nobody tries to join a match they would be turned away from.
    private void SetJoinable(bool joinable)
    {
        ConnectionGate gate = NetworkManager.GetComponent<ConnectionGate>();
        if (gate != null)
        {
            gate.AcceptingPlayers = joinable;
        }

        SessionService sessions = NetworkManager.GetComponent<SessionService>();
        if (sessions != null)
        {
            _ = sessions.SetJoinableAsync(joinable);
        }
    }

    [Rpc(SendTo.Server)]
    private void JoinLobbyRpc(FixedString64Bytes name, int lastPick, RpcParams rpcParams = default)
    {
        AddPlayer(rpcParams.Receive.SenderClientId, name, lastPick);
    }

    [Rpc(SendTo.Server)]
    private void SetReadyRpc(bool ready, RpcParams rpcParams = default)
    {
        int index = IndexOf(rpcParams.Receive.SenderClientId);
        if (index < 0)
        {
            return;
        }

        LobbyPlayer player = Players[index];
        player.IsReady = ready;
        Players[index] = player;
    }

    [Rpc(SendTo.Server)]
    private void SetCharacterRpc(int character, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        int index = IndexOf(sender);
        if (index < 0 || Players[index].IsReady || !CharacterRules.IsValid(character, CharacterCount))
        {
            return;
        }

        // Both players may click the same free character at once; the first request wins.
        if (CharactersTakenBy(sender).Contains(character))
        {
            return;
        }

        LobbyPlayer player = Players[index];
        player.CharacterIndex = character;
        Players[index] = player;
    }

    private void AddPlayer(ulong clientId, FixedString64Bytes requestedName, int requestedCharacter)
    {
        if (IndexOf(clientId) >= 0)
        {
            return;
        }

        // The sender cleaned its name already, but the host is the one that decides.
        string name = LobbyRules.CleanName(requestedName.ToString(), "Player " + (Players.Count + 1));
        int character = CharacterRules.Resolve(requestedCharacter, Players.Count, CharacterCount, CharactersTakenBy(clientId));
        Players.Add(new LobbyPlayer { ClientId = clientId, Name = new FixedString64Bytes(name), IsReady = false, CharacterIndex = character });

        // A newcomer changes who you would be playing against, so everyone confirms again.
        ResetReady();
    }

    private void OnConnectionEvent(NetworkManager networkManager, ConnectionEventData connectionEvent)
    {
        if (connectionEvent.EventType != ConnectionEvent.ClientDisconnected)
        {
            return;
        }

        int index = IndexOf(connectionEvent.ClientId);
        if (index >= 0)
        {
            Players.RemoveAt(index);
            ResetReady();
        }

        // Frees the seat in the online list if the player vanished without leaving it.
        SetJoinable(true);
    }

    // Every machine remembers its own pick, so it asks for it again after a match.
    private void OnPlayersChanged(NetworkListEvent<LobbyPlayer> changeEvent)
    {
        ConnectionNotes notes = NetworkManager.GetComponent<ConnectionNotes>();
        int pick = LocalCharacterIndex;
        if (notes != null && pick != CharacterRules.None)
        {
            notes.LocalCharacterIndex = pick;
        }
    }

    private void ResetReady()
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].IsReady)
            {
                LobbyPlayer player = Players[i];
                player.IsReady = false;
                Players[i] = player;
            }
        }
    }

    private int IndexOf(ulong clientId)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId == clientId)
            {
                return i;
            }
        }

        return -1;
    }
}
