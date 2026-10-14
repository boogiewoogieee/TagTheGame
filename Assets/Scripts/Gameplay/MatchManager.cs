using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Runs the bomb-tag match: a series of rounds until one player has enough round wins.
// The host makes every decision (who holds the bomb, when it explodes, which phase the
// match is in, the score) and writes it into network variables. Every machine, the host
// included, then shows that state locally: where the bomb sits, the explosion, and
// whether its own player may move.
public class MatchManager : NetworkBehaviour
{
    public const ulong NoHolder = ulong.MaxValue;

    // How often the machine of the holder may ask the host for a pass while in range.
    private const double PassRequestInterval = 0.2;

    [Tooltip("Tunable rules: fuse length, pass range, cooldown, rounds to win.")]
    public GameRules rules;

    [Tooltip("The bomb in the scene. It is not networked; each machine moves its own copy.")]
    public BombFuse bomb;

    [Tooltip("Used to send players back to their spawn points between rounds.")]
    public PlayerSpawner spawner;

    public NetworkVariable<ulong> BombHolderId = new NetworkVariable<ulong>(NoHolder);
    public NetworkVariable<double> DetonateAtServerTime = new NetworkVariable<double>();
    public NetworkVariable<MatchPhase> Phase = new NetworkVariable<MatchPhase>(MatchPhase.WaitingForPlayers);

    // Between rounds: when the next round starts and who lost the last one.
    public NetworkVariable<double> NextRoundAtServerTime = new NetworkVariable<double>();
    public NetworkVariable<ulong> LastRoundLoserId = new NetworkVariable<ulong>(NoHolder);

    // Match over: the winner, and whether it was decided by the opponent leaving.
    public NetworkVariable<ulong> WinnerId = new NetworkVariable<ulong>(NoHolder);
    public NetworkVariable<bool> OpponentLeft = new NetworkVariable<bool>(false);

    // One entry per player for the whole match; written only by the host.
    public NetworkList<PlayerMatchStats> Stats = new NetworkList<PlayerMatchStats>();

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

    public float SecondsToNextRound
    {
        get
        {
            if (!IsSpawned || Phase.Value != MatchPhase.RoundOver)
            {
                return 0f;
            }

            return Mathf.Max(0f, (float)(NextRoundAtServerTime.Value - NetworkManager.ServerTime.Time));
        }
    }

    public bool LocalPlayerHasBomb => IsSpawned && BombHolderId.Value == NetworkManager.LocalClientId;

    public int LocalRoundsWon => RoundsWonOf(NetworkManager.LocalClientId);

    public int OpponentRoundsWon
    {
        get
        {
            int rounds = 0;
            foreach (PlayerMatchStats stats in Stats)
            {
                if (stats.ClientId != NetworkManager.LocalClientId)
                {
                    rounds += stats.RoundsWon;
                }
            }

            return rounds;
        }
    }

    // True on the host at the results screen while both players are still here.
    public bool CanPlayAgain => IsSpawned && IsServer && Phase.Value == MatchPhase.MatchOver && _players.Count >= 2 && !OpponentLeft.Value;

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

    // Host only. A fresh match in the same arena: scores cleared, players back at their
    // spawn points, new round.
    public void PlayAgain()
    {
        if (!CanPlayAgain)
        {
            return;
        }

        SendPlayersToSpawn();
        StartMatch();
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
        double now = NetworkManager.ServerTime.Time;

        if (_players.Count < 2)
        {
            if (Phase.Value == MatchPhase.Playing || Phase.Value == MatchPhase.RoundOver)
            {
                EndByForfeit();
            }
            else if (Phase.Value == MatchPhase.MatchOver && !OpponentLeft.Value)
            {
                // The match was decided, but a results screen for one is still "opponent left".
                OpponentLeft.Value = true;
            }

            if (Phase.Value == MatchPhase.WaitingForPlayers)
            {
                BombHolderId.Value = NoHolder;
            }

            return;
        }

        switch (Phase.Value)
        {
            case MatchPhase.WaitingForPlayers:
                StartMatch();
                break;

            case MatchPhase.Playing:
                if (now >= DetonateAtServerTime.Value)
                {
                    EndRound();
                }

                break;

            case MatchPhase.RoundOver:
                if (now >= NextRoundAtServerTime.Value)
                {
                    SendPlayersToSpawn();
                    StartRound();
                }

                break;

            case MatchPhase.MatchOver:
                // A new opponent in the Editor shortcut makes Play again possible again.
                if (OpponentLeft.Value)
                {
                    OpponentLeft.Value = false;
                }

                break;
        }
    }

    private void StartMatch()
    {
        Stats.Clear();
        foreach (TagPlayer player in _players)
        {
            // Names arrive with the lobby later; until then the slot number has to do.
            Stats.Add(new PlayerMatchStats
            {
                ClientId = player.OwnerClientId,
                Name = new FixedString64Bytes("Player " + (Stats.Count + 1)),
            });
        }

        WinnerId.Value = NoHolder;
        OpponentLeft.Value = false;
        LastRoundLoserId.Value = NoHolder;
        StartRound();
    }

    private void StartRound()
    {
        TagPlayer holder = _players[Random.Range(0, _players.Count)];
        BombHolderId.Value = holder.OwnerClientId;
        DetonateAtServerTime.Value = NetworkManager.ServerTime.Time + rules.fuseSeconds;
        Phase.Value = MatchPhase.Playing;
    }

    // The bomb went off: whoever holds it loses the round, the other player scores.
    private void EndRound()
    {
        double now = NetworkManager.ServerTime.Time;
        ulong loser = BombHolderId.Value;
        AddHoldTime(loser, (float)(now - _holderSince));

        for (int i = 0; i < Stats.Count; i++)
        {
            if (Stats[i].ClientId != loser)
            {
                PlayerMatchStats stats = Stats[i];
                stats.RoundsWon++;
                Stats[i] = stats;
            }
        }

        LastRoundLoserId.Value = loser;

        // The deciding round goes straight to the results; the countdown is for rounds still to come.
        ulong winner = MatchRules.WinnerOf(SnapshotStats(), rules.roundsToWin);
        if (winner != MatchRules.NoWinner)
        {
            WinnerId.Value = winner;
            Phase.Value = MatchPhase.MatchOver;
            return;
        }

        NextRoundAtServerTime.Value = now + rules.betweenRoundsSeconds;
        Phase.Value = MatchPhase.RoundOver;
    }

    // The opponent left in the middle of the match: the player still here wins.
    private void EndByForfeit()
    {
        if (Phase.Value == MatchPhase.Playing && BombHolderId.Value != NoHolder)
        {
            AddHoldTime(BombHolderId.Value, (float)(NetworkManager.ServerTime.Time - _holderSince));
        }

        WinnerId.Value = _players.Count > 0 ? _players[0].OwnerClientId : NoHolder;
        OpponentLeft.Value = true;
        BombHolderId.Value = NoHolder;
        Phase.Value = MatchPhase.MatchOver;
    }

    private void SendPlayersToSpawn()
    {
        foreach (TagPlayer player in _players)
        {
            if (spawner != null && spawner.TryGetSpawnPose(player.OwnerClientId, out Vector3 position, out Quaternion rotation))
            {
                player.ReturnToSpawnRpc(position, rotation);
            }
        }
    }

    private List<PlayerMatchStats> SnapshotStats()
    {
        List<PlayerMatchStats> snapshot = new List<PlayerMatchStats>(Stats.Count);
        foreach (PlayerMatchStats stats in Stats)
        {
            snapshot.Add(stats);
        }

        return snapshot;
    }

    private int RoundsWonOf(ulong clientId)
    {
        int index = IndexOfStats(clientId);
        return index >= 0 ? Stats[index].RoundsWon : 0;
    }

    private int IndexOfStats(ulong clientId)
    {
        for (int i = 0; i < Stats.Count; i++)
        {
            if (Stats[i].ClientId == clientId)
            {
                return i;
            }
        }

        return -1;
    }

    private void AddHoldTime(ulong clientId, float seconds)
    {
        int index = IndexOfStats(clientId);
        if (index >= 0 && seconds > 0f)
        {
            PlayerMatchStats stats = Stats[index];
            stats.SecondsHoldingBomb += seconds;
            Stats[index] = stats;
        }
    }

    private void AddPass(ulong clientId)
    {
        int index = IndexOfStats(clientId);
        if (index >= 0)
        {
            PlayerMatchStats stats = Stats[index];
            stats.Passes++;
            Stats[index] = stats;
        }
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
            // A round that ended by detonation keeps its holder; a forfeit clears it, so no explosion.
            if (BombHolderId.Value != NoHolder && bomb.IsAttached)
            {
                bomb.Explode();
            }
            else
            {
                bomb.Detach();
            }
        }

        _shownPhase = phase;

        // Between rounds and at the results the players stand still; otherwise they move freely.
        TagPlayer localPlayer = FindPlayer(NetworkManager.LocalClientId);
        if (localPlayer != null)
        {
            localPlayer.SetInputEnabled(phase == MatchPhase.Playing || phase == MatchPhase.WaitingForPlayers);
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

        double now = NetworkManager.ServerTime.Time;
        float distance = Vector3.Distance(sender.transform.position, target.transform.position);
        PassResult result = PassRules.Check(Phase.Value, BombHolderId.Value, senderId, targetClientId,
            now - _holderSince, rules.passCooldown, distance, rules.passRange + rules.latencyTolerance);
        if (result != PassResult.Allowed)
        {
            return;
        }

        AddHoldTime(senderId, (float)(now - _holderSince));
        AddPass(senderId);
        BombHolderId.Value = targetClientId;
        _shownHolder = targetClientId;
        _holderSince = now;
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
