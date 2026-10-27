using StarterAssets;
using Unity.Netcode;
using UnityEngine;

// The comic part of losing: when the bomb goes off, everyone near it goes limp and is
// thrown into the air. Each body's flight belongs to its owner: the owner's machine runs
// the real ragdoll and streams the hips to the other machine, which drives its copy's hips
// along the same path and lets the limbs flop on their own. Running the physics on both
// machines gave two different flights, because a tumbling ragdoll never repeats itself and
// the other machine's copy starts from an interpolated pose that lags behind. The player
// root stays where it was; the next round puts the character back together there.
[RequireComponent(typeof(RagdollBody))]
public class BlastReaction : NetworkBehaviour
{
    [Tooltip("Random spin given to the body at launch, in radians per second. More tumbles, more comedy.")]
    public float spinSpeed = 8f;

    [Tooltip("The holder stands at the blast, so it flies outward at this fraction of the others' sideways speed.")]
    [Range(0f, 1f)]
    public float holderOutwardFraction = 0.6f;

    [Tooltip("How many times per real second the owner sends the hips during the flight.")]
    public float sendRate = 20f;

    [Tooltip("How far behind the owner the other machine plays the flight, in real seconds. A little delay lets it blend between two samples instead of guessing.")]
    public float playbackDelay = 0.08f;

    [Tooltip("Seconds the other machine takes to blend the limbs into the owner's final pose.")]
    public float landBlendSeconds = 0.25f;

    public RagdollBody Body { get; private set; }

    public bool IsLaunched => Body != null && Body.IsLimp;

    // The owner's flight as this machine has received it; empty on the owner's own machine.
    public BlastFlight Flight { get; } = new BlastFlight();

    private ThirdPersonController _controller;
    private StarterAssetsInputs _inputs;

    // The round's detonation time: messages from an earlier explosion must not throw this round's body.
    private double _roundKey;

    // Owner side.
    private float _nextSendTime;
    private bool _sentLanding;


    private void Awake()
    {
        Body = GetComponent<RagdollBody>();
        _controller = GetComponent<ThirdPersonController>();
        _inputs = GetComponent<StarterAssetsInputs>();
    }

    // Called on the owner's machine when the explosion goes off; the owner alone decides
    // whether its body flies, so both screens agree. Returns whether it flew.
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

        StopController();
        Body.GoLimp(velocity, spin);
        if (!Body.IsLimp)
        {
            return false;
        }

        _sentLanding = false;
        _nextSendTime = 0f;
        if (IsSpawned)
        {
            Body.GetHipsPose(out Vector3 position, out Quaternion rotation);
            LaunchedRpc(_roundKey, position, rotation, velocity);
        }

        return true;
    }

    // Puts everything back the way it was, for the round with this detonation time.
    public void ResetForRound(double roundKey)
    {
        _roundKey = roundKey;
        Flight.Clear();
        _sentLanding = false;

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

    private void StopController()
    {
        // The controller would keep steering the root; the owner gets it back at the next round.
        if (_controller != null)
        {
            _controller.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (!Body.IsLimp)
        {
            return;
        }

        if (Body.IsDriven)
        {
            PlayFlight();
        }
        else if (IsOwner && IsSpawned)
        {
            SendFlight();
        }
    }

    private void SendFlight()
    {
        if (Body.HasLanded)
        {
            if (!_sentLanding)
            {
                _sentLanding = true;
                Body.GetHipsPose(out Vector3 position, out Quaternion rotation);
                LandedRpc(_roundKey, Body.LimpTime, position, rotation, Body.CapturePose());
            }

            return;
        }

        // Real time, so the slow motion gets more samples per game second, not fewer per real one.
        if (Time.unscaledTime < _nextSendTime)
        {
            return;
        }

        _nextSendTime = Time.unscaledTime + 1f / Mathf.Max(sendRate, 1f);
        Body.GetHipsPose(out Vector3 hipsPosition, out Quaternion hipsRotation);
        FlightRpc(_roundKey, Body.LimpTime, hipsPosition, hipsRotation, Body.HipsVelocity);
    }

    // Moves this copy's hips to where the owner's were a moment ago on the owner's clock.
    // Both clocks start at the explosion and both machines play the same slow motion, so
    // game seconds since the launch line up without any clock syncing.
    private void PlayFlight()
    {
        if (Body.HasLanded)
        {
            return;
        }

        float target = Mathf.Max(0f, Body.LimpTime - playbackDelay * Time.timeScale);
        if (Flight.IsLandedAt(target))
        {
            Body.FreezeInto(Flight.LandPosition, Flight.LandRotation, Flight.LandPose, landBlendSeconds);
            return;
        }

        Flight.DropBefore(target);
        if (Flight.Evaluate(target, Physics.gravity, out Vector3 position, out Quaternion rotation))
        {
            Body.DriveHips(position, rotation);
        }
    }

    [Rpc(SendTo.NotOwner, InvokePermission = RpcInvokePermission.Owner)]
    private void LaunchedRpc(double roundKey, Vector3 hipsPosition, Quaternion hipsRotation, Vector3 velocity)
    {
        OnLaunched(roundKey, hipsPosition, hipsRotation, velocity);
    }

    [Rpc(SendTo.NotOwner, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
    private void FlightRpc(double roundKey, float time, Vector3 hipsPosition, Quaternion hipsRotation, Vector3 velocity)
    {
        OnFlight(roundKey, time, hipsPosition, hipsRotation, velocity);
    }

    [Rpc(SendTo.NotOwner, InvokePermission = RpcInvokePermission.Owner)]
    private void LandedRpc(double roundKey, float time, Vector3 hipsPosition, Quaternion hipsRotation, Quaternion[] pose)
    {
        OnLanded(roundKey, time, hipsPosition, hipsRotation, pose);
    }

    // The receiving side of the three messages. The round key keeps a late message from an
    // earlier explosion from throwing this round's body.
    private void OnLaunched(double roundKey, Vector3 hipsPosition, Quaternion hipsRotation, Vector3 velocity)
    {
        if (roundKey != _roundKey || Body.IsLimp)
        {
            return;
        }

        StopController();
        Flight.Add(0f, hipsPosition, hipsRotation, velocity);
        Body.GoLimpDriven(hipsPosition, hipsRotation, velocity);
    }

    private void OnFlight(double roundKey, float time, Vector3 hipsPosition, Quaternion hipsRotation, Vector3 velocity)
    {
        if (roundKey == _roundKey)
        {
            Flight.Add(time, hipsPosition, hipsRotation, velocity);
        }
    }

    private void OnLanded(double roundKey, float time, Vector3 hipsPosition, Quaternion hipsRotation, Quaternion[] pose)
    {
        if (roundKey != _roundKey)
        {
            return;
        }

        Flight.Land(time, hipsPosition, hipsRotation, pose);

        // The safety limit may have frozen this copy already; it still ends up in the owner's pose.
        if (Body.IsDriven && Body.HasLanded)
        {
            Body.FreezeInto(hipsPosition, hipsRotation, pose, landBlendSeconds);
        }
    }
}
