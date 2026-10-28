using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Fuse Rush: the bomb holder's short speed burst for catching the other player. The host
// keeps the rush state (who, until when, when it is ready again) in one network variable
// and checks every request. The holder's own machine starts the rush the moment the key
// is pressed, without waiting for the host, because the player moves itself anyway; if
// the host disagrees, the rush simply never shows on the other machine and runs out here.
public class FuseRush : NetworkBehaviour
{
    // A press the host's clock sees this much before the ready time still counts.
    private const double ClockTolerance = 0.1;

    // The host's answer to our own press arrives within this time; it must not replay the effects.
    private const double ConfirmationWindow = 1.0;

    [Tooltip("The match whose holder may rush. Its rules hold the speed, the length and the cooldowns.")]
    public MatchManager match;

    [Tooltip("Key or button for Fuse Rush. Q and the right mouse button are used when it has no bindings.")]
    public InputAction rushAction = new InputAction("Fuse Rush", InputActionType.Button);

    [Tooltip("Mixer group for the whoosh, usually the SFX group, so the volume settings apply.")]
    public AudioMixerGroup mixerGroup;

    [Tooltip("Played at the rushing player, heard by both. Leave empty to use a generated whoosh.")]
    public AudioClip whooshClip;

    [Range(0f, 1f)]
    public float volume = 0.8f;

    [Tooltip("Material of the streak behind a rushing player, seen by both players. Leave empty for no streak.")]
    public Material trailMaterial;

    [Tooltip("Colour of the streak, from the player to its tail.")]
    public Gradient trailColor = DefaultTrailColor();

    [Tooltip("Called on the rushing player's own machine when the rush starts, for example a camera FOV punch.")]
    public UnityEvent onLocalRushStarted;

    public NetworkVariable<FuseRushState> State = new NetworkVariable<FuseRushState>();

    private readonly Dictionary<TagPlayer, TrailRenderer> _trails = new Dictionary<TagPlayer, TrailRenderer>();
    private readonly List<TagPlayer> _staleTrails = new List<TagPlayer>();

    // Host: the round and holder the state was last written for.
    private double _seenDetonateAt;
    private ulong _seenHolder = MatchManager.NoHolder;

    // This machine's own press, shown before the host has confirmed it.
    private FuseRushState _predicted;
    private double _predictedAt = double.MinValue;

    private double _shownActiveUntil;
    private AudioSource _source;
    private Transform _whooshTarget;

    // The key as the player sees it, for the HUD.
    public string KeyLabel => rushAction.bindings.Count > 0 ? rushAction.GetBindingDisplayString(0) : "";

    // True while the local player holds the bomb in a running round.
    public bool LocalIsHolder => IsSpawned && match != null && match.Phase.Value == MatchPhase.Playing && match.LocalPlayerHasBomb;

    // 1 when the local player's rush starts, falling to 0 when it ends; 0 when not rushing.
    public float LocalRushLeft
    {
        get
        {
            if (!LocalIsHolder || match.rules == null)
            {
                return 0f;
            }

            return FuseRushRules.ActiveLeft(LocalState(), Now, match.rules.rushSeconds);
        }
    }

    // 1 right after a rush or a pass, falling to 0 when the local holder may rush again.
    public float LocalCooldownLeft => LocalIsHolder ? FuseRushRules.CooldownLeft(LocalState(), Now) : 0f;

    public float LocalSecondsToReady => LocalIsHolder ? Mathf.Max(0f, (float)(LocalState().ReadyAt - Now)) : 0f;

    private double Now => NetworkManager.ServerTime.Time;

    private void Awake()
    {
        if (rushAction.bindings.Count == 0)
        {
            rushAction.AddBinding("<Keyboard>/q");
            rushAction.AddBinding("<Mouse>/rightButton");
        }

        if (whooshClip == null)
        {
            whooshClip = GeneratedSounds.Whoosh("FuseRushWhoosh", 0.7f);
        }

        // A child, so it can follow the rushing player without moving the networked object.
        var sourceObject = new GameObject("FuseRushWhoosh");
        sourceObject.transform.SetParent(transform, false);
        _source = sourceObject.AddComponent<AudioSource>();
        _source.clip = whooshClip;
        _source.playOnAwake = false;
        _source.spatialBlend = 1f;
        _source.minDistance = 3f;
        _source.maxDistance = 60f;
        _source.rolloffMode = AudioRolloffMode.Linear;
        _source.volume = volume;
        _source.outputAudioMixerGroup = mixerGroup;
    }

    private void OnEnable()
    {
        rushAction.Enable();
    }

    private void OnDisable()
    {
        rushAction.Disable();
    }

    public override void OnNetworkDespawn()
    {
        _seenDetonateAt = 0;
        _seenHolder = MatchManager.NoHolder;
        _predictedAt = double.MinValue;
        _shownActiveUntil = 0;
    }

    private void Update()
    {
        if (!IsSpawned || match == null || !match.IsSpawned || match.rules == null)
        {
            return;
        }

        double now = Now;
        if (IsServer)
        {
            TrackHolder(now);
        }

        ulong localId = NetworkManager.LocalClientId;
        TagPlayer localPlayer = match.FindPlayer(localId);
        if (!LocalIsHolder)
        {
            // A prediction belongs to one holding; it ends with the pass or the round.
            _predictedAt = double.MinValue;
        }
        else if (localPlayer != null && localPlayer.InputEnabled && rushAction.WasPressedThisFrame()
            && FuseRushRules.CanStart(match.Phase.Value, match.BombHolderId.Value, localId, now, LocalState(), 0.0))
        {
            _predicted = FuseRushRules.Started(localId, now, match.rules.rushSeconds, match.rules.rushCooldownSeconds);
            _predictedAt = now;
            PlayEffects(localPlayer, true);
            RequestRushRpc();
        }

        ShowRushStart(now, localPlayer);

        if (localPlayer != null)
        {
            localPlayer.SetSpeedFactor(this, IsRushing(localId, now) ? match.rules.rushSpeedMultiplier : 1f);
        }

        UpdateTrails(now);
    }

    private void LateUpdate()
    {
        if (_whooshTarget != null && _source.isPlaying)
        {
            _source.transform.position = _whooshTarget.position;
        }
    }

    // Runs on the host. The request is only a suggestion: the host checks it against its own view.
    [Rpc(SendTo.Server)]
    private void RequestRushRpc(RpcParams rpcParams = default)
    {
        if (match == null || match.rules == null)
        {
            return;
        }

        double now = Now;

        // A pass handled earlier this frame must be in the state before the request is judged.
        TrackHolder(now);

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!FuseRushRules.CanStart(match.Phase.Value, match.BombHolderId.Value, senderId, now, State.Value, ClockTolerance))
        {
            return;
        }

        State.Value = FuseRushRules.Started(senderId, now, match.rules.rushSeconds, match.rules.rushCooldownSeconds);
    }

    // Host only: a new round makes the rush ready at GO, a pass gives the new holder a short wait.
    private void TrackHolder(double now)
    {
        ulong holder = match.BombHolderId.Value;
        double detonateAt = match.DetonateAtServerTime.Value;
        if (detonateAt != _seenDetonateAt)
        {
            _seenDetonateAt = detonateAt;
            _seenHolder = holder;
            State.Value = FuseRushRules.NewRound(holder, match.RoundStartsAtServerTime.Value);
        }
        else if (holder != _seenHolder)
        {
            _seenHolder = holder;
            State.Value = FuseRushRules.AfterPass(holder, now, match.rules.rushAfterPassSeconds);
        }
    }

    // The local prediction, until the host's state holds a rush started around the same press.
    private FuseRushState LocalState()
    {
        FuseRushState state = State.Value;
        if (_predictedAt == double.MinValue)
        {
            return state;
        }

        bool confirmed = state.HolderId == _predicted.HolderId && state.ActiveUntil > 0
            && state.CooldownFrom >= _predictedAt - ConfirmationWindow;
        return confirmed ? state : _predicted;
    }

    private bool IsRushing(ulong clientId, double now)
    {
        if (match.Phase.Value != MatchPhase.Playing || match.BombHolderId.Value != clientId)
        {
            return false;
        }

        if (FuseRushRules.IsActive(State.Value, clientId, now))
        {
            return true;
        }

        return clientId == NetworkManager.LocalClientId && _predictedAt != double.MinValue
            && FuseRushRules.IsActive(_predicted, clientId, now);
    }

    // A rush confirmed by the host: the other player's, or our own when the press was not seen here.
    private void ShowRushStart(double now, TagPlayer localPlayer)
    {
        FuseRushState state = State.Value;
        if (state.ActiveUntil == _shownActiveUntil)
        {
            return;
        }

        _shownActiveUntil = state.ActiveUntil;
        if (!FuseRushRules.IsActive(state, state.HolderId, now))
        {
            return;
        }

        TagPlayer rusher = match.FindPlayer(state.HolderId);
        bool alreadyShown = rusher != null && rusher == localPlayer && now - _predictedAt < ConfirmationWindow;
        if (rusher != null && !alreadyShown)
        {
            PlayEffects(rusher, rusher == localPlayer);
        }
    }

    private void PlayEffects(TagPlayer rusher, bool isLocal)
    {
        _whooshTarget = rusher.transform;
        _source.transform.position = rusher.transform.position;
        _source.volume = volume;
        _source.Play();

        if (isLocal)
        {
            onLocalRushStarted.Invoke();
        }
    }

    private void UpdateTrails(double now)
    {
        if (trailMaterial == null)
        {
            return;
        }

        foreach (TagPlayer player in match.Players)
        {
            bool rushing = IsRushing(player.OwnerClientId, now);
            if (!_trails.TryGetValue(player, out TrailRenderer trail))
            {
                if (!rushing)
                {
                    continue;
                }

                trail = CreateTrail(player);
                _trails[player] = trail;
            }

            if (trail != null)
            {
                trail.emitting = rushing;
            }
        }

        // The trails are children of the players, so they go when a player leaves.
        _staleTrails.Clear();
        foreach (KeyValuePair<TagPlayer, TrailRenderer> pair in _trails)
        {
            if (pair.Key == null || pair.Value == null)
            {
                _staleTrails.Add(pair.Key);
            }
        }

        foreach (TagPlayer stale in _staleTrails)
        {
            _trails.Remove(stale);
        }
    }

    private TrailRenderer CreateTrail(TagPlayer player)
    {
        var trailObject = new GameObject("FuseRushTrail");
        trailObject.transform.SetParent(player.transform, false);
        trailObject.transform.localPosition = new Vector3(0f, 0.9f, 0f);

        TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = trailMaterial;
        trail.colorGradient = trailColor;
        trail.time = 0.35f;
        trail.startWidth = 0.8f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.1f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = false;
        return trail;
    }

    private static Gradient DefaultTrailColor()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0f), new GradientColorKey(new Color(0.96f, 0.6f, 0.14f), 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }
}
