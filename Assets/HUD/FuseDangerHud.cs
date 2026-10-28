using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UIElements;

// The holder's panic in the last seconds of the fuse: red screen edges that pulse with a
// heartbeat, both getting faster as the explosion nears. Only the player holding the bomb
// gets it; the other one hears the bomb's ticking instead.
[RequireComponent(typeof(UIDocument))]
public class FuseDangerHud : MonoBehaviour
{
    [Tooltip("The match whose fuse this follows.")]
    public MatchManager match;

    [Tooltip("Mixer group for the heartbeat, usually the SFX group, so the volume settings apply.")]
    public AudioMixerGroup mixerGroup;

    [Tooltip("Heartbeat sound. Leave empty to use a generated \"lub-dub\".")]
    public AudioClip heartbeatClip;

    [Range(0f, 1f)]
    public float volume = 0.9f;

    [Tooltip("Heartbeats per second when the danger seconds begin.")]
    public float calmHeartRate = 1.2f;

    [Tooltip("Heartbeats per second right before the explosion.")]
    public float dangerHeartRate = 2.6f;

    [Tooltip("Colour of the screen edges; its alpha is the strongest the edges get.")]
    public Color vignetteColor = new Color(0.85f, 0.05f, 0.02f, 0.65f);

    private VisualElement _vignette;
    private Texture2D _texture;
    private AudioSource _source;
    private float _nextBeat;
    private float _lastBeat = -10f;

    private void Awake()
    {
        if (heartbeatClip == null)
        {
            heartbeatClip = GeneratedSounds.Heartbeat("Heartbeat");
        }

        _source = gameObject.AddComponent<AudioSource>();
        _source.clip = heartbeatClip;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _source.volume = volume;
        _source.outputAudioMixerGroup = mixerGroup;
    }

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        _vignette = root.Q<VisualElement>("DangerVignette");
        if (_vignette == null)
        {
            return;
        }

        if (_texture == null)
        {
            _texture = MakeVignette(128);
        }

        _vignette.style.backgroundImage = Background.FromTexture2D(_texture);
        _vignette.style.unityBackgroundImageTintColor = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, 1f);
        _vignette.style.opacity = 0f;
    }

    private void OnDestroy()
    {
        if (_texture != null)
        {
            Destroy(_texture);
        }
    }

    private void Update()
    {
        if (_vignette == null)
        {
            return;
        }

        float danger = 0f;
        float remaining = 0f;
        if (match != null && match.IsSpawned && match.rules != null && match.Phase.Value == MatchPhase.Playing && match.LocalPlayerHasBomb)
        {
            remaining = match.RemainingSeconds;
            danger = remaining > 0f ? FuseRules.Danger(remaining, match.rules.fuseDangerSeconds) : 0f;
        }

        if (danger <= 0f)
        {
            _vignette.style.opacity = 0f;
            _nextBeat = 0f;
            return;
        }

        if (Time.time >= _nextBeat)
        {
            _source.PlayOneShot(heartbeatClip);
            _lastBeat = Time.time;
            float rate = FuseRules.Rate(remaining, match.rules.fuseDangerSeconds, calmHeartRate, dangerHeartRate);
            _nextBeat = Time.time + 1f / Mathf.Max(rate, 0.1f);
        }

        // The edges flare on each beat and sink back, so the screen seems to throb with it.
        float pulse = Mathf.Exp(-(Time.time - _lastBeat) * 6f);
        float strength = Mathf.Lerp(0.35f, 1f, danger) * Mathf.Lerp(0.6f, 1f, pulse);
        _vignette.style.opacity = vignetteColor.a * strength;
    }

    // Clear in the middle, solid at the edges. Stretched over the screen it becomes an oval.
    private static Texture2D MakeVignette(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, Mathf.Sqrt(dx * dx + dy * dy)));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(edge * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}
