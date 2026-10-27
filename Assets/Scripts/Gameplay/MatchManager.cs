using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    [Tooltip("Scene the host loads for everyone on Return to lobby.")]
    public string menuScene = "MainMenu";

    [Tooltip("Slows the game down at the explosion. Optional.")]
    public SlowMotion slowMotion;

    [Tooltip("Follows the flying body at the explosion. Optional.")]
    public BlastCamera blastCamera;

    public NetworkVariable<ulong> BombHolderId = new NetworkVariable<ulong>(NoHolder);
    public NetworkVariable<double> DetonateAtServerTime = new NetworkVariable<double>();
    public NetworkVariable<MatchPhase> Phase = new NetworkVariable<MatchPhase>(MatchPhase.WaitingForPlayers);

    // Between rounds: when the next round starts and who lost the last one.
    public NetworkVariable<double> NextRoundAtServerTime = new NetworkVariable<double>();

    // When the countdown ends and the players may move; the fuse starts counting from here.
    public NetworkVariable<double> RoundStartsAtServerTime = new NetworkVariable<double>();

    // When the round result or the results screen may appear: the flight after the explosion comes first.
    public NetworkVariable<double> ShowResultAtServerTime = new NetworkVariable<double>();
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

    // Positive during the countdown, negative once the round runs. The HUD draws the digits from it.
    public double SecondsToRoundStart => IsSpawned ? RoundStartsAtServerTime.Value - NetworkManager.ServerTime.Time : 0;

    public int RoundNumber => LocalRoundsWon + OpponentRoundsWon + 1;

    public bool LocalPlayerHasBomb => IsSpawned && BombHolderId.Value == NetworkManager.LocalClientId;

    // False while the explosion and the flight are still worth watching.
    public bool RoundEndVisible => IsSpawned && NetworkManager.ServerTime.Time >= ShowResultAtServerTime.Value;

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

    public bool CanReturnToLobby => IsSpawned && IsServer && Phase.Value == MatchPhase.MatchOver;

    // Only once a second arena exists and is named in the rules.
    public bool CanGoToNextLevel => CanPlayAgain && rules != null && !string.IsNullOrEmpty(rules.nextArenaScene);

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

    // Host only. Keeps the connection and takes both players back to the lobby scene, where
    // the lobby reopens for joining and everyone starts not ready.
    public void ReturnToLobby()
    {
        if (!CanReturnToLobby)
        {
            return;
        }

        NetworkManager.SceneManager.LoadScene(menuScene, LoadSceneMode.Single);
    }

    // Host only. Loads the next arena for everyone; a fresh match starts there.
    public void GoToNextLevel()
    {
        if (!CanGoToNextLevel)
        {
            return;
        }

        NetworkManager.SceneManager.LoadScene(rules.nextArenaScene, LoadSceneMode.Single);
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
            if (Phase.Value == MatchPhase.Playing || Phase.Value == MatchPhase.RoundOver || Phase.Value == MatchPhase.Countdown)
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

            case MatchPhase.Countdown:
                if (now >= RoundStartsAtServerTime.Value)
                {
                    Phase.Value = MatchPhase.Playing;
                }

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
        // The lobby left the names on the NetworkManager; the Editor shortcut has none, so slot numbers do.
        ConnectionNotes notes = NetworkManager.GetComponent<ConnectionNotes>();
        Stats.Clear();
        foreach (TagPlayer player in _players)
        {
            string name;
            if (notes == null || !notes.PlayerNames.TryGetValue(player.OwnerClientId, out name) || string.IsNullOrEmpty(name))
            {
                name = "Player " + (Stats.Count + 1);
            }

            Stats.Add(new PlayerMatchStats
            {
                ClientId = player.OwnerClientId,
                Name = new FixedString64Bytes(name),
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

        // The holder is shown during the countdown, but the fuse only burns from GO.
        RoundStartsAtServerTime.Value = NetworkManager.ServerTime.Time + rules.countdownSeconds;
        DetonateAtServerTime.Value = RoundStartsAtServerTime.Value + rules.fuseSeconds;
        Phase.Value = MatchPhase.Countdown;
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
        ShowResultAtServerTime.Value = now + rules.resultsDelaySeconds;

        // The deciding round goes straight to the results; the countdown is for rounds still to come.
        ulong winner = MatchRules.WinnerOf(SnapshotStats(), rules.roundsToWin);
        if (winner != MatchRules.NoWinner)
        {
            WinnerId.Value = winner;
            Phase.Value = MatchPhase.MatchOver;
            return;
        }

        // The pause between rounds starts once the result is on screen, not at the explosion.
        NextRoundAtServerTime.Value = now + rules.resultsDelaySeconds + rules.betweenRoundsSeconds;
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

        // Nothing explodes on a forfeit, so there is nothing to wait for.
        ShowResultAtServerTime.Value = NetworkManager.ServerTime.Time;
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

        if (phase == MatchPhase.Playing || phase == MatchPhase.Countdown)
        {
            // A new detonation time means a new round, even when phase and holder look unchanged.
            if (DetonateAtServerTime.Value != _shownDetonateAt)
            {
                _shownDetonateAt = DetonateAtServerTime.Value;
                _shownHolder = NoHolder;
                ResetBlastReactions();
            }

            if (BombHolderId.Value != _shownHolder)
            {
                _shownHolder = BombHolderId.Value;

                // During the countdown the holder is known early; the holding only counts from GO.
                _holderSince = System.Math.Max(NetworkManager.ServerTime.Time, RoundStartsAtServerTime.Value);
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
                Vector3 blastOrigin = bomb.transform.position;
                bomb.Explode();
                ThrowPlayers(blastOrigin, BombHolderId.Value);
            }
            else
            {
                bomb.Detach();
            }
        }
        else if (_shownPhase == MatchPhase.Countdown)
        {
            // The round never started (the opponent left during the countdown).
            bomb.Detach();
        }

        _shownPhase = phase;

        // Between rounds and at the results the players stand still; otherwise they move freely.
        // The countdown freezes them too, but keeps the cursor hidden: play is about to start.
        TagPlayer localPlayer = FindPlayer(NetworkManager.LocalClientId);
        if (localPlayer != null)
        {
            localPlayer.SetInputEnabled(phase == MatchPhase.Playing || phase == MatchPhase.WaitingForPlayers, phase == MatchPhase.Countdown);
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

    // Every machine throws its own player (the other copy flies when its owner says so),
    // slows down and watches a flight: its own body if it flew, otherwise the holder's,
    // who always flies.
    private void ThrowPlayers(Vector3 origin, ulong holderId)
    {
        BlastReaction watched = null;
        foreach (TagPlayer player in _players)
        {
            BlastReaction reaction = player.GetComponent<BlastReaction>();
            if (reaction == null)
            {
                continue;
            }

            bool isHolder = player.OwnerClientId == holderId;
            if (player.IsOwner)
            {
                if (reaction.Launch(origin, isHolder, rules.blastRadius, rules.blastUpSpeed, rules.blastOutSpeed))
                {
                    watched = reaction;
                }
            }
            else if (isHolder && watched == null)
            {
                watched = reaction;
            }
        }

        if (slowMotion != null)
        {
            slowMotion.Play();
        }

        if (blastCamera != null && watched != null)
        {
            blastCamera.Follow(watched.Body, origin);
        }
    }

    private void ResetBlastReactions()
    {
        if (blastCamera != null)
        {
            blastCamera.Release();
        }

        foreach (TagPlayer player in _players)
        {
            BlastReaction reaction = player.GetComponent<BlastReaction>();
            if (reaction != null)
            {
                reaction.ResetForRound(DetonateAtServerTime.Value);
            }
        }
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
