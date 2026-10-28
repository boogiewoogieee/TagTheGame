using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

// What a pass looks and sounds like: a tag sound at the new holder, a light flash there,
// the bomb popping in size and a hit-stop of a few hundredths of a second. The passer's
// machine shows it the moment it asks the host for the pass, without waiting for the
// answer; the other machine shows it when the host's new holder arrives. Each pass is
// shown once per machine.
public class PassFeedback : MonoBehaviour
{
    // The host's answer to our own pass arrives within this time; it must not replay the feedback.
    private const double ConfirmationWindow = 1.0;

    [Tooltip("The match whose passes this shows.")]
    public MatchManager match;

    [Tooltip("Does the hit-stop. Optional.")]
    public SlowMotion slowMotion;

    [Tooltip("Mixer group for the tag sound, usually the SFX group, so the volume settings apply.")]
    public AudioMixerGroup mixerGroup;

    [Tooltip("Played at the new holder. Leave empty to use a generated tag sound.")]
    public AudioClip tagClip;

    [Range(0f, 1f)]
    public float volume = 0.9f;

    [Tooltip("Real seconds the game nearly stops at a pass. 0 turns the hit-stop off.")]
    [Min(0f)]
    public float hitStopSeconds = 0.05f;

    [Tooltip("Real seconds the bomb takes to pop up and settle back.")]
    [Min(0f)]
    public float popSeconds = 0.25f;

    [Tooltip("Bomb size at the top of the pop, as a multiple of its normal size.")]
    [Min(1f)]
    public float popPeak = 1.6f;

    public Color flashColor = new Color(1f, 0.7f, 0.3f);

    [Tooltip("Light intensity at the start of the flash.")]
    [Min(0f)]
    public float flashIntensity = 6f;

    [Min(0f)]
    public float flashRange = 5f;

    [Tooltip("Real seconds the flash takes to fade out.")]
    [Min(0f)]
    public float flashSeconds = 0.3f;

    [Tooltip("Called on this machine whenever a pass is shown, for example a camera FOV punch.")]
    public UnityEvent onPassShown;

    [Tooltip("Called on this machine when the local player has just received the bomb, for example a HUD flash.")]
    public UnityEvent onBombReceived;

    private AudioSource _source;
    private Light _light;
    private Transform _flashTarget;
    private Vector3 _bombScale = Vector3.one;
    private float _popElapsed = float.MaxValue;
    private float _flashElapsed = float.MaxValue;

    private double _seenDetonateAt;
    private ulong _seenHolder = MatchManager.NoHolder;
    private bool _watching;
    private double _seenRequestAt;
    private double _pressedAt = double.MinValue;
    private bool _shownPressThisHolding;

    private void Awake()
    {
        if (tagClip == null)
        {
            tagClip = GeneratedSounds.Tag("PassTag");
        }

        if (match != null && match.bomb != null)
        {
            _bombScale = match.bomb.transform.localScale;
        }

        // Children, so they can sit at the new holder without moving the networked object.
        var sourceObject = new GameObject("PassTagSound");
        sourceObject.transform.SetParent(transform, false);
        _source = sourceObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 1f;
        _source.minDistance = 3f;
        _source.maxDistance = 60f;
        _source.rolloffMode = AudioRolloffMode.Linear;
        _source.outputAudioMixerGroup = mixerGroup;

        var lightObject = new GameObject("PassFlash");
        lightObject.transform.SetParent(transform, false);
        _light = lightObject.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.shadows = LightShadows.None;
        _light.enabled = false;
    }

    private void OnDisable()
    {
        EndEffects();
    }

    private void Update()
    {
        if (match != null && match.IsSpawned)
        {
            WatchPasses();
        }

        AnimateEffects();
    }

    private void LateUpdate()
    {
        if (_light.enabled && _flashTarget != null)
        {
            _light.transform.position = FlashPoint(_flashTarget);
        }
    }

    private void WatchPasses()
    {
        double now = match.NetworkManager.ServerTime.Time;
        ulong localId = match.NetworkManager.LocalClientId;

        // A request made before this machine started watching is not a press to show.
        if (!_watching)
        {
            _watching = true;
            _seenRequestAt = match.LastPassRequestAt;
        }

        // Our own pass request comes first, so a pass the host settled in the same frame counts as already shown.
        if (match.LastPassRequestAt != _seenRequestAt)
        {
            _seenRequestAt = match.LastPassRequestAt;
            TagPlayer target = match.FindPlayer(match.LastPassRequestTarget);
            if (!_shownPressThisHolding && target != null)
            {
                _shownPressThisHolding = true;
                _pressedAt = now;
                Show(target, false);
            }
        }

        ulong holder = match.BombHolderId.Value;
        double detonateAt = match.DetonateAtServerTime.Value;
        if (detonateAt != _seenDetonateAt)
        {
            // A new round hands out the bomb without a pass.
            _seenDetonateAt = detonateAt;
            _seenHolder = holder;
            _shownPressThisHolding = false;
        }
        else if (holder != _seenHolder)
        {
            ulong passer = _seenHolder;
            _seenHolder = holder;
            _shownPressThisHolding = false;

            // A forfeit clears the holder; that is not a pass.
            bool isPass = match.Phase.Value == MatchPhase.Playing && passer != MatchManager.NoHolder && holder != MatchManager.NoHolder;
            if (isPass && !PassFeedbackRules.AlreadyShownAtPress(passer, localId, _pressedAt, now, ConfirmationWindow))
            {
                Show(match.FindPlayer(holder), holder == localId);
            }
        }
    }

    private void Show(TagPlayer newHolder, bool received)
    {
        if (newHolder != null)
        {
            Vector3 point = FlashPoint(newHolder.transform);
            _source.transform.position = point;
            _source.PlayOneShot(tagClip, volume);

            _flashTarget = newHolder.transform;
            _light.transform.position = point;
            _light.color = flashColor;
            _light.range = flashRange;
            _flashElapsed = 0f;
        }

        _popElapsed = 0f;

        if (slowMotion != null)
        {
            slowMotion.HitStop(hitStopSeconds);
        }

        onPassShown.Invoke();
        if (received)
        {
            onBombReceived.Invoke();
        }
    }

    // Real time, so the hit-stop does not hold the flash and the pop still.
    private void AnimateEffects()
    {
        float step = Time.unscaledDeltaTime;

        if (_flashElapsed < flashSeconds)
        {
            _flashElapsed += step;
            _light.intensity = PassFeedbackRules.FlashIntensity(_flashElapsed, flashSeconds, flashIntensity);
            _light.enabled = _light.intensity > 0f;
        }
        else if (_light.enabled)
        {
            _light.enabled = false;
        }

        if (match != null && match.bomb != null && _popElapsed < popSeconds)
        {
            _popElapsed += step;
            match.bomb.transform.localScale = _bombScale * PassFeedbackRules.PopScale(_popElapsed, popSeconds, popPeak);
        }
    }

    // Leaving in the middle of a pop must not leave the bomb big for the next match.
    private void EndEffects()
    {
        if (match != null && match.bomb != null)
        {
            match.bomb.transform.localScale = _bombScale;
        }

        _popElapsed = float.MaxValue;
        _flashElapsed = float.MaxValue;
        if (_light != null)
        {
            _light.enabled = false;
        }
    }

    // About chest height, where the bomb sits.
    private static Vector3 FlashPoint(Transform player)
    {
        return player.position + Vector3.up * 1.2f;
    }
}
