using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// The lobby's state: who is in and who is ready. The host owns the list, like the match
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

    public override void OnNetworkSpawn()
    {
        // The name was typed in the menu and kept on the NetworkManager, so it is still here
        // when the lobby comes back after a match.
        ConnectionNotes notes = NetworkManager.GetComponent<ConnectionNotes>();
        FixedString64Bytes name = new FixedString64Bytes(notes != null ? notes.LocalPlayerName : "");

        if (IsServer)
        {
            NetworkManager.OnConnectionEvent += OnConnectionEvent;

            // A lobby that is open again takes players again (the match locked it).
            SetJoinable(true);

            AddPlayer(NetworkManager.ServerClientId, name);
        }
        else
        {
            JoinLobbyRpc(name);
        }
    }

    public override void OnNetworkDespawn()
    {
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
            foreach (LobbyPlayer player in Players)
            {
                notes.PlayerNames[player.ClientId] = player.Name.ToString();
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
    private void JoinLobbyRpc(FixedString64Bytes name, RpcParams rpcParams = default)
    {
        AddPlayer(rpcParams.Receive.SenderClientId, name);
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

    private void AddPlayer(ulong clientId, FixedString64Bytes requestedName)
    {
        if (IndexOf(clientId) >= 0)
        {
            return;
        }

        // The sender cleaned its name already, but the host is the one that decides.
        string name = LobbyRules.CleanName(requestedName.ToString(), "Player " + (Players.Count + 1));
        Players.Add(new LobbyPlayer { ClientId = clientId, Name = new FixedString64Bytes(name), IsReady = false });

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
