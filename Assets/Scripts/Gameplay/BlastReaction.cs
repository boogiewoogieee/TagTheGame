using StarterAssets;
using UnityEngine;

// The comic part of losing: when the bomb goes off, everyone near it goes limp and is
// thrown into the air. The ragdoll is only for show, so every machine runs its own copy
// from the same explosion and nothing about the flight goes over the network. The player
// root stays where it was; the next round puts the character back together there.
[RequireComponent(typeof(RagdollBody))]
public class BlastReaction : MonoBehaviour
{
    [Tooltip("Random spin given to the body at launch, in radians per second. More tumbles, more comedy.")]
    public float spinSpeed = 8f;

    [Tooltip("The holder stands at the blast, so it flies outward at this fraction of the others' sideways speed.")]
    [Range(0f, 1f)]
    public float holderOutwardFraction = 0.6f;

    public RagdollBody Body { get; private set; }

    public bool IsLaunched => Body != null && Body.IsLimp;

    private ThirdPersonController _controller;
    private StarterAssetsInputs _inputs;

    private void Awake()
    {
        Body = GetComponent<RagdollBody>();
        _controller = GetComponent<ThirdPersonController>();
        _inputs = GetComponent<StarterAssetsInputs>();
    }

    // Called on every machine when the explosion goes off. Returns whether this player flew.
    public bool Launch(Vector3 origin, bool isHolder, float radius, float upSpeed, float outSpeed)
    {
        if (!isHolder && Vector3.Distance(transform.position, origin) > radius)
        {
            return false;
        }

        Vector3 away = transform.position - origin;
        away.y = 0f;
        float outward = outSpeed;
        if (isHolder || away.sqrMagnitude < 0.01f)
        {
            away = Random.insideUnitSphere;
            away.y = 0f;
            outward = outSpeed * holderOutwardFraction;
        }

        Vector3 velocity = Vector3.up * upSpeed + away.normalized * outward;
        Vector3 spin = Random.onUnitSphere * spinSpeed;

        // The controller would keep steering the root; the owner gets it back at the next round.
        if (_controller != null)
        {
            _controller.enabled = false;
        }

        Body.GoLimp(velocity, spin);
        return true;
    }

    // Puts everything back the way it was, for a new round.
    public void Reset()
    {
        Body.Recover();
        if (_inputs != null)
        {
            // A blast mid-jump leaves the jump press behind, and the controller would jump again at the next round.
            _inputs.jump = false;
        }

        if (_controller != null)
        {
            _controller.enabled = true;
        }
    }
}
