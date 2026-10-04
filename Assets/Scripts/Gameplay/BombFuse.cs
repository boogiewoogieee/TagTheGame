using UnityEngine;
using UnityEngine.Events;

// Bomb timer: counts the fuse down and, when it runs out, spawns the explosion
// effect where the bomb is and hides the bomb.
public class BombFuse : MonoBehaviour
{
    [Tooltip("Seconds from lighting the fuse until the bomb explodes.")]
    [Min(0.1f)]
    public float fuseSeconds = 30f;

    [Tooltip("Light the fuse automatically when the scene starts.")]
    public bool igniteOnStart = true;

    [Tooltip("Effect spawned at the bomb's position when it explodes.")]
    public GameObject explosionPrefab;

    [Tooltip("Hide the bomb model after it explodes.")]
    public bool hideOnExplode = true;

    public UnityEvent onExploded;

    public bool IsLit { get; private set; }
    public bool HasExploded { get; private set; }
    public float RemainingSeconds { get; private set; }

    private void Start()
    {
        RemainingSeconds = fuseSeconds;
        if (igniteOnStart)
        {
            Ignite();
        }
    }

    public void Ignite()
    {
        if (HasExploded)
        {
            return;
        }

        RemainingSeconds = fuseSeconds;
        IsLit = true;
    }

    private void Update()
    {
        if (!IsLit)
        {
            return;
        }

        RemainingSeconds -= Time.deltaTime;
        if (RemainingSeconds <= 0f)
        {
            Explode();
        }
    }

    public void Explode()
    {
        if (HasExploded)
        {
            return;
        }

        HasExploded = true;
        IsLit = false;
        RemainingSeconds = 0f;

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            Destroy(explosion, GetEffectDuration(explosion));
        }

        if (hideOnExplode)
        {
            foreach (Renderer bombRenderer in GetComponentsInChildren<Renderer>())
            {
                bombRenderer.enabled = false;
            }
        }

        onExploded.Invoke();
    }

    // The explosion prefab doesn't remove itself, so destroy it once its longest particle system has finished.
    private static float GetEffectDuration(GameObject effect)
    {
        float longest = 0f;
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule main = particles.main;
            longest = Mathf.Max(longest, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
        }
        return longest > 0f ? longest : 5f;
    }
}
