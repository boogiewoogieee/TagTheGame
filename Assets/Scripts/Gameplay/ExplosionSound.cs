using UnityEngine;

// A one-shot 3D explosion sound. It is spawned where the bomb explodes, plays once and
// removes itself. How loud it is depends on the distance to the AudioListener, which sits
// on the local player, so the holder hears it loudest and far-away players hear a faint boom.
[RequireComponent(typeof(AudioSource))]
public class ExplosionSound : MonoBehaviour
{
    // Unity stores distance curves over 0..MaxDistance; this many samples rebuild ours in that form.
    private const int CurveSamples = 32;

    [Tooltip("Within this distance the explosion plays at full volume, in metres.")]
    [Min(0.1f)]
    public float minDistance = 3f;

    [Tooltip("Distance at which the explosion has faded to its quietest level, in metres. Further away it stays at that level.")]
    [Min(1f)]
    public float maxDistance = 100f;

    [Tooltip("Volume from MinDistance (left edge) to MaxDistance (right edge). 1 is full volume. Keep the right end above 0 so a far-off boom stays audible.")]
    public AnimationCurve volumeFalloff = SmoothCurve(
        new Vector2(0f, 1f), new Vector2(0.124f, 0.42f), new Vector2(0.381f, 0.2f), new Vector2(1f, 0.06f));

    [Tooltip("Muffle the sound with distance, like a real far-off explosion.")]
    public bool muffleWithDistance = true;

    [Tooltip("Brightness from MinDistance (left edge) to MaxDistance (right edge). 1 is unfiltered; lower values cut more of the high frequencies (0.11 is about 2.5 kHz).")]
    public AnimationCurve brightnessFalloff = SmoothCurve(
        new Vector2(0f, 1f), new Vector2(0.072f, 1f), new Vector2(0.4f, 0.35f), new Vector2(1f, 0.114f));

    private void Awake()
    {
        Apply();
    }

    private void Start()
    {
        AudioSource source = GetComponent<AudioSource>();
        source.Play();
        Destroy(gameObject, source.clip != null ? source.clip.length + 0.5f : 1f);
    }

    private void OnValidate()
    {
        maxDistance = Mathf.Max(maxDistance, minDistance + 1f);
        Apply();
    }

    // Copies the settings above into the AudioSource (and the low-pass filter, when there is one).
    private void Apply()
    {
        AudioSource source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, ToDistanceCurve(volumeFalloff));

        AudioLowPassFilter lowPass = GetComponent<AudioLowPassFilter>();
        if (lowPass != null)
        {
            lowPass.enabled = muffleWithDistance;
            lowPass.customCutoffCurve = ToDistanceCurve(brightnessFalloff);
        }
    }

    // Our curves run from MinDistance to MaxDistance. Unity wants them from 0 to MaxDistance,
    // so this holds the value at 1 up to MinDistance and resamples the rest.
    private AnimationCurve ToDistanceCurve(AnimationCurve curve)
    {
        Keyframe[] keys = new Keyframe[CurveSamples + 1];
        for (int i = 0; i <= CurveSamples; i++)
        {
            float normalizedDistance = (float)i / CurveSamples;
            float distance = normalizedDistance * maxDistance;
            float value = distance <= minDistance
                ? 1f
                : Mathf.Clamp01(curve.Evaluate(Mathf.InverseLerp(minDistance, maxDistance, distance)));
            keys[i] = new Keyframe(normalizedDistance, value);
        }

        // Straight lines between the samples, so the result never overshoots.
        for (int i = 0; i <= CurveSamples; i++)
        {
            if (i > 0)
            {
                keys[i].inTangent = (keys[i].value - keys[i - 1].value) / (keys[i].time - keys[i - 1].time);
            }

            if (i < CurveSamples)
            {
                keys[i].outTangent = (keys[i + 1].value - keys[i].value) / (keys[i + 1].time - keys[i].time);
            }
        }

        return new AnimationCurve(keys);
    }

    private static AnimationCurve SmoothCurve(params Vector2[] points)
    {
        AnimationCurve curve = new AnimationCurve();
        foreach (Vector2 point in points)
        {
            curve.AddKey(point.x, point.y);
        }

        for (int i = 0; i < curve.length; i++)
        {
            curve.SmoothTangents(i, 0f);
        }

        return curve;
    }
}
