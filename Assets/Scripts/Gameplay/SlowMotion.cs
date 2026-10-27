using UnityEngine;

// Slows the whole game down for the blast and brings it back up. Only the local clock
// changes: Netcode keeps its own, unscaled time, so the fuse and the pause between rounds
// stay in step with the other machine. One of these lives in the arena.
public class SlowMotion : MonoBehaviour
{
    [Tooltip("Speed of the game at the slowest point, as a fraction of normal.")]
    [Range(0.05f, 1f)]
    public float slowScale = 0.25f;

    [Tooltip("Real seconds to slow down. Short, so the hit lands at once.")]
    public float rampInSeconds = 0.08f;

    [Tooltip("Real seconds at the slowest speed.")]
    public float holdSeconds = 1f;

    [Tooltip("Real seconds to get back to normal speed.")]
    public float rampOutSeconds = 0.5f;

    public bool IsPlaying { get; private set; }

    private float _elapsed;
    private float _normalFixedStep;

    private void Awake()
    {
        _normalFixedStep = Time.fixedDeltaTime;
    }

    public void Play()
    {
        IsPlaying = true;
        _elapsed = 0f;
    }

    public void Stop()
    {
        IsPlaying = false;
        Apply(1f);
    }

    private void Update()
    {
        if (!IsPlaying)
        {
            return;
        }

        _elapsed += Time.unscaledDeltaTime;
        float scale;
        if (_elapsed < rampInSeconds)
        {
            scale = Mathf.Lerp(1f, slowScale, _elapsed / rampInSeconds);
        }
        else if (_elapsed < rampInSeconds + holdSeconds)
        {
            scale = slowScale;
        }
        else if (_elapsed < rampInSeconds + holdSeconds + rampOutSeconds)
        {
            scale = Mathf.Lerp(slowScale, 1f, (_elapsed - rampInSeconds - holdSeconds) / rampOutSeconds);
        }
        else
        {
            Stop();
            return;
        }

        Apply(scale);
    }

    // Leaving the arena in the middle of the effect must not leave the menu in slow motion.
    private void OnDisable()
    {
        if (IsPlaying)
        {
            Stop();
        }
    }

    // Physics steps shrink with the clock, so the ragdoll stays smooth instead of jerking.
    private void Apply(float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = _normalFixedStep * scale;
    }
}
