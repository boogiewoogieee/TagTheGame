using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Runs the bomb-tag round. The host makes every decision (who holds the bomb, when it
// explodes, which phase the match is in) and writes it into three network variables.
// Every machine, the host included, then shows that state locally: where the bomb sits,
// the explosion, and whether its own player may move.
public class MatchManager : NetworkBehaviour
{
    public const ulong NoHolder = ulong.MaxValue;

    // How often the machine of the holder may ask the host for a pass while in range.
    private const double PassRequestInterval = 0.2;

    [Tooltip("Tunable rules: fuse length, pass range, cooldown.")]
    public GameRules rules;

    [Tooltip("The bomb in the scene. It is not networked; each machine moves its own copy.")]
    public BombFuse bomb;

    [Tooltip("Used to send players back to their spawn points when a round restarts.")]
    public PlayerSpawner spawner;

    public NetworkVariable<ulong> BombHolderId = new NetworkVariable<ulong>(NoHolder);
    public NetworkVariable<double> DetonateAtServerTime = new NetworkVariable<double>();
    public NetworkVariable<MatchPhase> Phase = new NetworkVariable<MatchPhase>(MatchPhase.WaitingForPlayers);

    private readonly List<TagPlayer> _players = new List<TagPlayer>();

    // The state this machine last showed, used to spot changes in the network variables.
    private MatchPhase _shownPhase = MatchPhase.WaitingForPlayers;
    private double _shownDetonateAt;
    private ulong _shownHolder = NoHolder;

    private double _holderSince;
    private double _lastPassRequest;

    // The only timer in the game: every machine works the remaining time out from the
    // synchronized server clock, so nothing is sent per frame.
    public float RemainingSeconds
    {
        get
        {
            if (!IsSpawned || Phase.Value != MatchPhase.Playing)
            {
                return 0f;
            }

            return Mathf.Max(0f, (float)(DetonateAtServerTime.Value - NetworkManager.ServerTime.Time));
        }
    }

    public bool LocalPlayerHasBomb => IsSpawned && BombHolderId.Value == NetworkManager.LocalClientId;

    // True on the host while two players are in, which is when a round can be restarted.
    public bool CanRestart => IsSpawned && IsServer && _players.Count >= 2;

    // Netcode only lets the host look up the objects of other players, so players sign in
    // here when they spawn. That gives every machine the same list.
    public void RegisterPlayer(TagPlayer player)
    {
        if (!_players.Contains(player))
        {
            _players.Add(player);
        }
    }

    public void UnregisterPlayer(TagPlayer player)
    {
        _players.Remove(player);

        // The bomb is parented to its holder, so let go before that player object is destroyed.
        if (bomb != null && bomb.transform.IsChildOf(player.transform))
        {
            bomb.Detach();
        }
    }

    // Host only. Sends both players back to their spawn points and starts a fresh round.
    public void RestartRound()
    {
        if (!CanRestart)
        {
            return;
        }

        foreach (TagPlayer player in _players)
        {
            if (spawner != null && spawner.TryGetSpawnPose(player.OwnerClientId, out Vector3 position, out Quaternion rotation))
            {
                player.ReturnToSpawnRpc(position, rotation);
            }
        }

        StartRound();
    }

    public override void OnNetworkDespawn()
    {
        if (bomb != null)
        {
            bomb.Detach();
        }

        _shownPhase = MatchPhase.WaitingForPlayers;
        _shownDetonateAt = 0;
        _shownHolder = NoHolder;
    }

    private void Update()
    {
        if (!IsSpawned)
        {
            return;
        }

        if (IsServer)
        {
            RunHostLogic();
        }

        ShowState();
        RequestPassIfInRange();
    }

    private void RunHostLogic()
    {
        if (_players.Count < 2)
        {
            if (Phase.Value != MatchPhase.WaitingForPlayers)
            {
                BombHolderId.Value = NoHolder;
                Phase.Value = MatchPhase.WaitingForPlayers;
            }

            return;
        }

        if (Phase.Value == MatchPhase.WaitingForPlayers)
        {
            StartRound();
        }
        else if (Phase.Value == MatchPhase.Playing && NetworkManager.ServerTime.Time >= DetonateAtServerTime.Value)
        {
            Phase.Value = MatchPhase.RoundOver;
        }
    }

    private void StartRound()
    {
        TagPlayer holder = _players[Random.Range(0, _players.Count)];
        BombHolderId.Value = holder.OwnerClientId;
        DetonateAtServerTime.Value = NetworkManager.ServerTime.Time + rules.fuseSeconds;
        Phase.Value = MatchPhase.Playing;
    }

    // Compares the network variables with what this machine last showed and catches up.
    // Polling instead of change callbacks keeps this independent of the order in which
    // the variables and the player objects arrive.
    private void ShowState()
    {
        MatchPhase phase = Phase.Value;

        if (phase == MatchPhase.Playing)
        {
            // A new detonation time means a new round, even when phase and holder look unchanged.
            if (DetonateAtServerTime.Value != _shownDetonateAt)
            {
                _shownDetonateAt = DetonateAtServerTime.Value;
                _shownHolder = NoHolder;
            }

            if (BombHolderId.Value != _shownHolder)
            {
                _shownHolder = BombHolderId.Value;
                _holderSince = NetworkManager.ServerTime.Time;
            }

            TagPlayer holder = FindPlayer(_shownHolder);
            if (holder != null && !bomb.IsAttachedTo(holder.BombAnchor))
            {
                bomb.AttachTo(holder.BombAnchor);
            }
        }
        else if (_shownPhase == MatchPhase.Playing)
        {
            if (phase == MatchPhase.RoundOver && bomb.IsAttached)
            {
                bomb.Explode();
            }
            else
            {
                bomb.Detach();
            }
        }

        _shownPhase = phase;

        // Round over freezes the players; in the other phases they move freely.
        TagPlayer localPlayer = FindPlayer(NetworkManager.LocalClientId);
        if (localPlayer != null)
        {
            localPlayer.SetInputEnabled(phase != MatchPhase.RoundOver);
        }
    }

    // Contact is checked by distance on the machine of the holder, where it feels instant.
    // Physics triggers would not work: copies of other players have no active collider.
    private void RequestPassIfInRange()
    {
        ulong localId = NetworkManager.LocalClientId;
        TagPlayer localPlayer = FindPlayer(localId);
        TagPlayer otherPlayer = FindOtherPlayer(localId);
        if (localPlayer == null || otherPlayer == null)
        {
            return;
        }

        double now = NetworkManager.ServerTime.Time;
        if (now - _lastPassRequest < PassRequestInterval)
        {
            return;
        }

        float distance = Vector3.Distance(localPlayer.transform.position, otherPlayer.transform.position);
        PassResult result = PassRules.Check(Phase.Value, BombHolderId.Value, localId, otherPlayer.OwnerClientId,
            now - _holderSince, rules.passCooldown, distance, rules.passRange);
        if (result != PassResult.Allowed)
        {
            return;
        }

        _lastPassRequest = now;
        RequestPassRpc(otherPlayer.OwnerClientId);
    }

    // Runs on the host. The request is only a suggestion: the host checks it against its
    // own view of the match, with a little extra distance allowed for network delay.
    [Rpc(SendTo.Server)]
    private void RequestPassRpc(ulong targetClientId, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        TagPlayer sender = FindPlayer(senderId);
        TagPlayer target = FindPlayer(targetClientId);
        if (sender == null || target == null)
        {
            return;
        }

        float distance = Vector3.Distance(sender.transform.position, target.transform.position);
        PassResult result = PassRules.Check(Phase.Value, BombHolderId.Value, senderId, targetClientId,
            NetworkManager.ServerTime.Time - _holderSince, rules.passCooldown, distance, rules.passRange + rules.latencyTolerance);
        if (result != PassResult.Allowed)
        {
            return;
        }

        BombHolderId.Value = targetClientId;
        _shownHolder = targetClientId;
        _holderSince = NetworkManager.ServerTime.Time;
    }

    private TagPlayer FindPlayer(ulong clientId)
    {
        foreach (TagPlayer player in _players)
        {
            if (player.OwnerClientId == clientId)
            {
                return player;
            }
        }

        return null;
    }

    private TagPlayer FindOtherPlayer(ulong clientId)
    {
        foreach (TagPlayer player in _players)
        {
            if (player.OwnerClientId != clientId)
            {
                return player;
            }
        }

        return null;
    }
}
