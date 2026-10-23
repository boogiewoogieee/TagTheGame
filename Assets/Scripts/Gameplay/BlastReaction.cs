using StarterAssets;
using UnityEngine;

// The comic part of losing: when the bomb goes off, the character is thrown into the air
// and tumbles head over heels before landing. The owning machine flies the root on a
// short arc (the position travels over the usual network sync); every machine spins the
// model child locally and freezes the animation, so the tumble needs no network traffic.
// The flight goes through the CharacterController, so walls and tunnel roofs stop it.
public class BlastReaction : MonoBehaviour
{
    [Tooltip("The model under the player root. It spins; the root only moves.")]
    public Transform modelRoot;

    [Tooltip("Upward speed given to the holder, in metres per second.")]
    public float launchSpeed = 9f;

    [Tooltip("Sideways speed away from the bomb, in metres per second.")]
    public float pushSpeed = 3f;

    [Tooltip("Gravity during the flight. Stronger than real gravity so the hop stays short.")]
    public float gravity = -22f;

    [Tooltip("How long the model tumbles, in seconds. Covers the flight and the landing.")]
    public float tumbleSeconds = 1.1f;

    [Tooltip("How fast the model spins while tumbling, in degrees per second.")]
    public float spinSpeed = 540f;

    [Tooltip("How long the model takes to get back upright after the tumble, in seconds.")]
    public float recoverSeconds = 0.4f;

    [Tooltip("A player this close to the bomb is knocked about too, at the strength below.")]
    public float nearbyRadius = 4f;

    [Tooltip("Strength of the knock for a nearby player, as a fraction of the holder's.")]
    [Range(0f, 1f)]
    public float nearbyStrength = 0.5f;

    [Tooltip("Longest possible flight, in seconds. Ends it even if no ground is found, e.g. off the map edge.")]
    public float maxFlightSeconds = 3f;

    private CharacterController _characterController;
    private ThirdPersonController _controller;
    private Animator _animator;

    private bool _flying;
    private Vector3 _velocity;
    private float _flightLeft;
    private float _tumbleLeft;
    private float _recoverLeft;
    private Vector3 _spinAxis;
    private Quaternion _restRotation;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _controller = GetComponent<ThirdPersonController>();
        _animator = GetComponent<Animator>();
        if (modelRoot != null)
        {
            _restRotation = modelRoot.localRotation;
        }
    }

    // Called on every machine when the explosion goes off at `origin`. Only the machine
    // that owns this player moves it; the others just show the tumble.
    public void Launch(Vector3 origin, bool isHolder, bool ownedHere)
    {
        float strength = isHolder ? 1f : nearbyStrength;
        if (!isHolder && Vector3.Distance(transform.position, origin) > nearbyRadius)
        {
            return;
        }

        if (ownedHere && !_flying && _characterController != null)
        {
            Vector3 away = transform.position - origin;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitSphere;
            away.y = 0f;
            _velocity = Vector3.up * launchSpeed * strength + away.normalized * pushSpeed * strength;
            _flightLeft = maxFlightSeconds;
            _flying = true;

            // The third-person controller would fight the flight with its own gravity and
            // snap the character back to the ground. The CharacterController stays on for collisions.
            if (_controller != null)
            {
                _controller.enabled = false;
            }
        }

        _tumbleLeft = tumbleSeconds * Mathf.Lerp(0.6f, 1f, strength);
        _recoverLeft = 0f;
        _spinAxis = (Vector3.right + Random.insideUnitSphere * 0.4f).normalized;
        if (_animator != null)
        {
            // A pose frozen mid-air reads as "no control over any limb".
            _animator.speed = 0f;
        }
    }

    // Puts everything back the way it was, for a new round or a teleport.
    public void Reset()
    {
        if (_flying)
        {
            Land();
        }

        _tumbleLeft = 0f;
        _recoverLeft = 0f;
        if (modelRoot != null)
        {
            modelRoot.localRotation = _restRotation;
        }

        if (_animator != null)
        {
            _animator.speed = 1f;
        }
    }

    private void Update()
    {
        if (_flying)
        {
            Fly();
        }

        if (modelRoot == null)
        {
            return;
        }

        if (_tumbleLeft > 0f)
        {
            _tumbleLeft -= Time.deltaTime;
            modelRoot.localRotation = Quaternion.AngleAxis(spinSpeed * Time.deltaTime, _spinAxis) * modelRoot.localRotation;
            if (_tumbleLeft <= 0f)
            {
                _recoverLeft = recoverSeconds;
            }
        }
        else if (_recoverLeft > 0f)
        {
            _recoverLeft -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(_recoverLeft / recoverSeconds);
            modelRoot.localRotation = Quaternion.Slerp(modelRoot.localRotation, _restRotation, t);
            if (_recoverLeft <= 0f)
            {
                modelRoot.localRotation = _restRotation;
                if (_animator != null)
                {
                    _animator.speed = 1f;
                }
            }
        }
    }

    private void Fly()
    {
        // A teleport to the spawn point switches the CharacterController off for a moment.
        if (!_characterController.enabled)
        {
            return;
        }

        _flightLeft -= Time.deltaTime;
        _velocity.y += gravity * Time.deltaTime;
        CollisionFlags hit = _characterController.Move(_velocity * Time.deltaTime);

        // Bumping a roof ends the climb at once, instead of sticking to it until gravity wins.
        if ((hit & CollisionFlags.Above) != 0 && _velocity.y > 0f)
        {
            _velocity.y = 0f;
        }

        bool landed = (hit & CollisionFlags.Below) != 0 && _velocity.y < 0f;
        if (landed || _flightLeft <= 0f)
        {
            Land();
        }
    }

    // Hands the character back to its controller, which takes over gravity from here.
    private void Land()
    {
        _flying = false;
        _velocity = Vector3.zero;

        if (_controller != null)
        {
            _controller.enabled = true;
        }
    }
}
