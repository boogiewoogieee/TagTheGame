using UnityEngine;
using UnityEngine.Events;

// The local view of the bomb: where it sits and the explosion effect. It has no timer
// of its own. MatchManager owns the countdown and tells the bomb what to show.
public class BombFuse : MonoBehaviour
{
    [Tooltip("Effect spawned at the position of the bomb when it explodes.")]
    public GameObject explosionPrefab;

    public UnityEvent onExploded;

    // The bomb is a scene root object whenever nobody holds it.
    public bool IsAttached => transform.parent != null;

    public bool IsAttachedTo(Transform anchor)
    {
        return transform.parent == anchor;
    }

    // Puts the bomb on a player and shows it.
    public void AttachTo(Transform anchor)
    {
        transform.SetParent(anchor, false);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        gameObject.SetActive(true);
    }

    // Takes the bomb off its holder and hides it. The bomb is never shown unheld.
    public void Detach()
    {
        transform.SetParent(null, true);
        gameObject.SetActive(false);
    }

    public void Explode()
    {
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            Destroy(explosion, GetEffectDuration(explosion));
        }

        Detach();
        onExploded.Invoke();
    }

    // The explosion prefab does not remove itself, so destroy it once its longest particle system has finished.
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
