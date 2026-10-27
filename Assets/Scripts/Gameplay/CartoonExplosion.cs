using Unity.Cinemachine;
using UnityEngine;

// The parts of our explosion effect that particles cannot do: a flash of light, a ground
// ring that grows and disappears, and a camera shake. The particle systems on the same
// prefab run on their own; BombFuse removes the whole effect when the longest one is done.
public class CartoonExplosion : MonoBehaviour
{
    [Tooltip("Light that flashes at the blast and fades out.")]
    public Light flash;

    [Tooltip("Seconds the flash takes to fade.")]
    public float flashSeconds = 0.25f;

    [Tooltip("Flat ring on the ground that grows outward.")]
    public Transform ring;

    [Tooltip("Radius the ring reaches, in metres. Match it to the blast radius so players see how far it throws.")]
    public float ringRadius = 6f;

    [Tooltip("Seconds the ring takes to grow; it disappears afterwards.")]
    public float ringSeconds = 0.45f;

    [Tooltip("Shakes every camera that listens for impulses. Optional.")]
    public CinemachineImpulseSource impulse;

    private float _flashIntensity;
    private float _time;

    private void Start()
    {
        if (flash != null)
        {
            _flashIntensity = flash.intensity;
        }

        if (ring != null)
        {
            ring.localScale = Vector3.zero;
        }

        if (impulse != null)
        {
            impulse.GenerateImpulse();
        }
    }

    private void Update()
    {
        _time += Time.deltaTime;

        if (flash != null && flash.enabled)
        {
            float left = 1f - _time / flashSeconds;
            flash.intensity = _flashIntensity * Mathf.Max(0f, left);
            flash.enabled = left > 0f;
        }

        if (ring != null && ring.gameObject.activeSelf)
        {
            // Fast at first, then easing out: the ring reads as a shockwave, not a growing disc.
            float t = Mathf.Clamp01(_time / ringSeconds);
            float eased = 1f - (1f - t) * (1f - t);
            ring.localScale = new Vector3(ringRadius * eased * 2f, 1f, ringRadius * eased * 2f);
            if (t >= 1f)
            {
                ring.gameObject.SetActive(false);
            }
        }
    }
}
