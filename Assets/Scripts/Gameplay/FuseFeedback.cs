using UnityEngine;
using UnityEngine.Audio;

// The bomb's own warning: a ticking sound in 3D on the bomb that speeds up as the fuse
// runs out, with a crackle of sparks from the wires on every tick that grows with it. It sits on the bomb, which is only active while
// someone holds it, and works everything out from the match's fuse time, so both machines
// hear and see the same thing without anything extra being sent.
public class FuseFeedback : MonoBehaviour
{
    [Tooltip("The match whose fuse this follows.")]
    public MatchManager match;

    [Tooltip("Mixer group for the ticking, usually the SFX group, so the volume settings apply.")]
    public AudioMixerGroup mixerGroup;

    [Tooltip("Tick sound. Leave empty to use a generated click.")]
    public AudioClip tickClip;

    [Range(0f, 1f)]
    public float volume = 0.5f;

    [Tooltip("Ticks per second while the fuse is calm.")]
    public float calmTickRate = 1f;

    [Tooltip("Ticks per second right before the explosion.")]
    public float dangerTickRate = 6f;

    [Tooltip("Pitch of the last ticks; the ticking rises towards it during the danger seconds.")]
    public float dangerPitch = 1.3f;

    [Tooltip("Closer than this, in metres, the ticking is at full volume.")]
    public float minDistance = 2f;

    [Tooltip("Beyond this, in metres, the ticking can no longer be heard.")]
    public float maxDistance = 30f;

    [Tooltip("Sparks fired on every tick. Optional; its own emission should be off, as this emits the bursts.")]
    public ParticleSystem sparks;

    [Tooltip("Sparks per tick while the fuse is calm.")]
    public int calmSparks = 4;

    [Tooltip("Sparks per tick right before the explosion.")]
    public int dangerSparks = 14;

    private AudioSource _source;
    private float _nextTick;

    private void Awake()
    {
        if (tickClip == null)
        {
            tickClip = GeneratedSounds.Tick("FuseTick", 2600f);
        }

        _source = gameObject.AddComponent<AudioSource>();
        _source.clip = tickClip;
        _source.playOnAwake = false;
        _source.spatialBlend = 1f;
        _source.rolloffMode = AudioRolloffMode.Linear;
        _source.minDistance = minDistance;
        _source.maxDistance = maxDistance;
        _source.dopplerLevel = 0f;
        _source.volume = volume;
        _source.outputAudioMixerGroup = mixerGroup;
    }

    private void OnEnable()
    {
        // The bomb is switched on when someone picks it up; tick soon, not a whole calm beat later.
        _nextTick = Time.time + 0.2f;
    }

    private void Update()
    {
        if (match == null || !match.IsSpawned || match.Phase.Value != MatchPhase.Playing || match.rules == null)
        {
            return;
        }

        float remaining = match.RemainingSeconds;
        if (remaining <= 0f)
        {
            return;
        }

        float dangerSeconds = match.rules.fuseDangerSeconds;
        if (Time.time >= _nextTick)
        {
            float danger = FuseRules.Danger(remaining, dangerSeconds);
            _source.pitch = Mathf.Lerp(1f, dangerPitch, danger);
            _source.PlayOneShot(tickClip);
            if (sparks != null)
            {
                sparks.Emit(Mathf.RoundToInt(Mathf.Lerp(calmSparks, dangerSparks, danger)));
            }

            _nextTick = Time.time + 1f / Mathf.Max(FuseRules.Rate(remaining, dangerSeconds, calmTickRate, dangerTickRate), 0.1f);
        }
    }
}
