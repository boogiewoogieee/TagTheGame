using System.Collections.Generic;
using UnityEngine;

// Turns the character into a physics ragdoll for the blast and back again. The parts are
// built once from the humanoid bones the Animator knows, so every Hodaart character works
// without hand-made colliders and nothing in the imported models changes. While the
// character is alive the parts are kinematic with their colliders off, so the ragdoll
// costs nothing and never gets in the way of the CharacterController.
public class RagdollBody : MonoBehaviour
{
    [Tooltip("Physics layer for the ragdoll parts. It must not collide with the Player layer, or the parts fight the CharacterController.")]
    public string ragdollLayer = "Ragdoll";

    [Tooltip("Radius of the arm and leg capsules, in metres.")]
    public float limbRadius = 0.08f;

    [Tooltip("Radius of the hips and chest capsules, in metres.")]
    public float torsoRadius = 0.16f;

    [Tooltip("Radius of the head sphere, in metres.")]
    public float headRadius = 0.13f;

    [Tooltip("Slower than this, in metres per second, counts as lying still.")]
    public float restSpeed = 0.6f;

    [Tooltip("Seconds of lying still before the ragdoll counts as landed.")]
    public float restSeconds = 0.3f;

    [Tooltip("Seconds after the first touch of the ground before the body freezes where it lies. Short, so it stops like a cartoon instead of twitching.")]
    public float settleSeconds = 0.4f;

    [Tooltip("Longest a body stays loose, in game seconds, in case it never reaches the ground. A full flight from a jump or a roof takes over 3 s, so keep this well above that or the body freezes in mid-air.")]
    public float maxLimpSeconds = 6f;

    // The camera follows the hips: they sit at the centre of mass, so the framing stays calm.
    public Transform Hips => _hips != null ? _hips.transform : null;

    public bool IsLimp { get; private set; }

    // Landed means frozen: the body no longer moves at all.
    public bool HasLanded => IsLimp && _frozen;

    // Driven means another machine runs this body's flight and moves the hips; the limbs still flop here.
    public bool IsDriven { get; private set; }

    // Game seconds since the body went limp; it stops counting when the body freezes.
    public float LimpTime => _limpTime;

    public Vector3 HipsVelocity => _hips != null && !_hips.isKinematic ? _hips.linearVelocity : Vector3.zero;

    private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
    private readonly List<Collider> _colliders = new List<Collider>();
    private Rigidbody _hips;
    private Animator _animator;
    private SkinnedMeshRenderer[] _skins;
    private Transform[] _bones;
    private Vector3[] _bonePositions;
    private Quaternion[] _boneRotations;
    private float _restTime;
    private float _limpTime;
    private float _groundTime;
    private bool _frozen;
    private int _groundMask;
    private bool _built;
    private Quaternion[] _blendFrom;
    private Quaternion[] _blendTo;
    private float _blendTime;
    private float _blendSeconds;

    // Lets go of every limb and throws the whole body with one velocity, so the limbs
    // leave together instead of being torn apart by the joints.
    public void GoLimp(Vector3 velocity, Vector3 spin)
    {
        if (!Prepare())
        {
            return;
        }

        IsDriven = false;
        Loosen(velocity);
        _hips.angularVelocity = spin;
    }

    // For a copy whose flight runs on another machine: it starts from that machine's hips
    // pose, and from then on only DriveHips moves the hips.
    public void GoLimpDriven(Vector3 hipsPosition, Quaternion hipsRotation, Vector3 velocity)
    {
        if (!Prepare())
        {
            return;
        }

        IsDriven = true;
        _hips.transform.SetPositionAndRotation(hipsPosition, hipsRotation);
        Loosen(velocity);
        _hips.isKinematic = true;
    }

    public void DriveHips(Vector3 position, Quaternion rotation)
    {
        if (IsDriven && IsLimp && !_frozen)
        {
            _hips.MovePosition(position);
            _hips.MoveRotation(rotation);
        }
    }

    public void GetHipsPose(out Vector3 position, out Quaternion rotation)
    {
        position = _hips != null ? _hips.position : transform.position;
        rotation = _hips != null ? _hips.rotation : transform.rotation;
    }

    // The local rotation of every part below the hips, in build order: the lying pose to send.
    public Quaternion[] CapturePose()
    {
        var pose = new Quaternion[Mathf.Max(_bodies.Count - 1, 0)];
        for (int i = 0; i < pose.Length; i++)
        {
            pose[i] = _bodies[i + 1].transform.localRotation;
        }

        return pose;
    }

    // Freezes a driven copy where the owner's body came to rest, blending the limbs into
    // the owner's lying pose so both screens show the same heap.
    public void FreezeInto(Vector3 hipsPosition, Quaternion hipsRotation, Quaternion[] pose, float blendSeconds)
    {
        if (!IsLimp)
        {
            return;
        }

        Freeze();

        // Setting the body as well as the transform skips the hips' interpolation, which would drag it back.
        _hips.position = hipsPosition;
        _hips.rotation = hipsRotation;
        _hips.transform.SetPositionAndRotation(hipsPosition, hipsRotation);

        // Another rig has other parts; its own limbs are better than a wrong pose.
        if (pose == null || pose.Length != _bodies.Count - 1)
        {
            return;
        }

        _blendFrom = CapturePose();
        _blendTo = pose;
        _blendTime = 0f;
        _blendSeconds = Mathf.Max(blendSeconds, 0.0001f);
    }

    // Records the pose for Recover and resets the timers. False when there is nothing to throw.
    private bool Prepare()
    {
        if (!_built)
        {
            Build();
        }

        if (_hips == null || IsLimp)
        {
            return false;
        }

        IsLimp = true;
        _frozen = false;
        _restTime = 0f;
        _limpTime = 0f;
        _groundTime = -1f;
        _blendTo = null;

        for (int i = 0; i < _bones.Length; i++)
        {
            _bonePositions[i] = _bones[i].localPosition;
            _boneRotations[i] = _bones[i].localRotation;
        }

        return true;
    }

    private void Loosen(Vector3 velocity)
    {
        // A disabled Animator stops writing the bone poses; speed 0 would still overwrite them.
        _animator.enabled = false;

        // The skinned mesh is culled by the bounds of its root, which stays behind on the ground.
        foreach (SkinnedMeshRenderer skin in _skins)
        {
            skin.updateWhenOffscreen = true;
        }

        foreach (Collider part in _colliders)
        {
            part.enabled = true;
        }

        foreach (Rigidbody body in _bodies)
        {
            body.isKinematic = false;
            body.linearVelocity = velocity;
        }
    }

    // Hands the bones back to the Animator, which snaps them to the current animation pose.
    public void Recover()
    {
        if (!IsLimp)
        {
            return;
        }

        IsLimp = false;
        IsDriven = false;
        _frozen = false;
        _blendTo = null;
        foreach (Rigidbody body in _bodies)
        {
            body.isKinematic = true;
        }

        foreach (Collider part in _colliders)
        {
            part.enabled = false;
        }

        foreach (SkinnedMeshRenderer skin in _skins)
        {
            skin.updateWhenOffscreen = false;
        }

        // The Animator skips the bones while its renderer is off-screen, and the renderer's
        // bounds follow the hips: a body that came to rest out of view would never find its
        // way home. Putting the bones back by hand brings the bounds home with them.
        for (int i = 0; i < _bones.Length; i++)
        {
            _bones[i].localPosition = _bonePositions[i];
            _bones[i].localRotation = _boneRotations[i];
        }

        _animator.enabled = true;
        _animator.Update(0f);
    }

    private void FixedUpdate()
    {
        if (!IsLimp || _frozen)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;
        _limpTime += dt;

        // A driven copy lands when the owner says so. It plays a little behind the owner,
        // so its own safety limit waits a second longer for that message.
        if (IsDriven)
        {
            if (_limpTime >= maxLimpSeconds + 1f)
            {
                Freeze();
            }

            return;
        }

        _restTime = _hips.linearVelocity.magnitude < restSpeed ? _restTime + dt : 0f;

        // Ground under the hips while falling or lying, never a wall hit in mid-air.
        if (_groundTime < 0f && _hips.linearVelocity.y <= 0.5f
            && Physics.Raycast(_hips.position, Vector3.down, torsoRadius + 0.3f, _groundMask, QueryTriggerInteraction.Ignore))
        {
            _groundTime = _limpTime;
        }

        bool settled = _groundTime >= 0f && _limpTime - _groundTime >= settleSeconds;
        if (settled || _restTime >= restSeconds || _limpTime >= maxLimpSeconds)
        {
            Freeze();
        }
    }

    // A loose ragdoll on the ground twitches and slides forever; freezing it where it lies
    // reads as a cartoon crash and gives the camera a clear moment to let go.
    private void Freeze()
    {
        _frozen = true;
        foreach (Rigidbody body in _bodies)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }
    }

    private void LateUpdate()
    {
        if (_blendTo == null)
        {
            return;
        }

        _blendTime += Time.deltaTime;
        float t = Mathf.Clamp01(_blendTime / _blendSeconds);
        for (int i = 0; i < _blendTo.Length; i++)
        {
            _bodies[i + 1].transform.localRotation = Quaternion.Slerp(_blendFrom[i], _blendTo[i], t);
        }

        if (t >= 1f)
        {
            _blendTo = null;
        }
    }

    private void Build()
    {
        _built = true;
        _animator = GetComponent<Animator>();
        if (_animator == null || !_animator.isHuman)
        {
            Debug.LogWarning("RagdollBody needs a humanoid Animator on " + name + "; the blast will not go limp.", this);
            return;
        }

        int layer = LayerMask.NameToLayer(ragdollLayer);
        if (layer < 0)
        {
            Debug.LogWarning("Layer \"" + ragdollLayer + "\" does not exist; the ragdoll keeps the player's layer and may collide with players.", this);
            layer = gameObject.layer;
        }

        _groundMask = ~((1 << layer) | (1 << gameObject.layer));

        _skins = GetComponentsInChildren<SkinnedMeshRenderer>();

        Transform hips = Bone(HumanBodyBones.Hips);
        Transform spine = Bone(HumanBodyBones.Spine);
        Transform chest = Bone(HumanBodyBones.Chest);
        Transform head = Bone(HumanBodyBones.Head);
        if (hips == null || spine == null || head == null)
        {
            Debug.LogWarning("RagdollBody could not find the hips, spine or head of " + name + ".", this);
            return;
        }

        // Every bone of the rig, so the whole pose can be put back after the ragdoll.
        Transform rig = hips.parent != null ? hips.parent : hips;
        _bones = rig.GetComponentsInChildren<Transform>(true);
        _bonePositions = new Vector3[_bones.Length];
        _boneRotations = new Quaternion[_bones.Length];

        // Torso first, so the limbs have something to hang from. A rig without a chest bone
        // gets a two-part torso.
        _hips = AddCapsule(hips, null, spine, torsoRadius, 10f, layer, 20f, 20f);
        Rigidbody upperTorso = AddCapsule(spine, _hips, chest != null ? chest : head, torsoRadius, 8f, layer, 20f, 25f);
        if (chest != null)
        {
            upperTorso = AddCapsule(chest, upperTorso, head, torsoRadius, 8f, layer, 20f, 25f);
        }

        AddSphere(head, upperTorso, headRadius, 3f, layer, 40f, 30f);

        AddLimb(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, upperTorso, layer);
        AddLimb(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, upperTorso, layer);
        AddLimb(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, _hips, layer);
        AddLimb(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, _hips, layer);

        // Limbs caught inside each other shake the whole body; the parts never need to collide with their own kind.
        for (int i = 0; i < _colliders.Count; i++)
        {
            for (int j = i + 1; j < _colliders.Count; j++)
            {
                Physics.IgnoreCollision(_colliders[i], _colliders[j], true);
            }
        }
    }

    private void AddLimb(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end, Rigidbody root, int layer)
    {
        Transform upperBone = Bone(upper);
        Transform lowerBone = Bone(lower);
        Transform endBone = Bone(end);
        if (upperBone == null || lowerBone == null || endBone == null)
        {
            return;
        }

        Rigidbody upperBody = AddCapsule(upperBone, root, lowerBone, limbRadius, 2f, layer, 45f, 45f);
        AddCapsule(lowerBone, upperBody, endBone, limbRadius, 1.5f, layer, 10f, 70f);
    }

    // A capsule from this bone to the next one down the chain. Sizes are given in metres,
    // but a Mixamo rig is often scaled by 0.01, so they are converted into bone units.
    private Rigidbody AddCapsule(Transform bone, Rigidbody parent, Transform towards, float radius, float mass, int layer, float twist, float swing)
    {
        float unit = Mathf.Max(bone.lossyScale.x, 0.0001f);
        Vector3 local = bone.InverseTransformPoint(towards.position);
        int axis = Mathf.Abs(local.x) >= Mathf.Abs(local.y) && Mathf.Abs(local.x) >= Mathf.Abs(local.z) ? 0 : (Mathf.Abs(local.y) >= Mathf.Abs(local.z) ? 1 : 2);

        CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
        capsule.direction = axis;
        capsule.center = local * 0.5f;
        capsule.radius = radius / unit;
        capsule.height = local.magnitude + 2f * radius / unit;
        capsule.enabled = false;
        _colliders.Add(capsule);

        return AddBody(bone, parent, mass, layer, twist, swing);
    }

    private void AddSphere(Transform bone, Rigidbody parent, float radius, float mass, int layer, float twist, float swing)
    {
        float unit = Mathf.Max(bone.lossyScale.x, 0.0001f);
        SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
        sphere.radius = radius / unit;
        sphere.center = Vector3.up * (radius * 0.6f / unit);
        sphere.enabled = false;
        _colliders.Add(sphere);

        AddBody(bone, parent, mass, layer, twist, swing);
    }

    private Rigidbody AddBody(Transform bone, Rigidbody parent, float mass, int layer, float twist, float swing)
    {
        bone.gameObject.layer = layer;

        Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
        body.mass = mass;
        body.linearDamping = 0.2f;
        body.angularDamping = 0.6f;
        body.isKinematic = true;
        body.solverIterations = 10;
        body.solverVelocityIterations = 4;
        body.maxAngularVelocity = 15f;

        // The only mode a kinematic body accepts; it also keeps the fast launch from tunnelling through roofs.
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.interpolation = parent == null ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
        _bodies.Add(body);

        if (parent != null)
        {
            CharacterJoint joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.enableProjection = true;
            joint.lowTwistLimit = new SoftJointLimit { limit = -twist };
            joint.highTwistLimit = new SoftJointLimit { limit = twist };
            joint.swing1Limit = new SoftJointLimit { limit = swing };
            joint.swing2Limit = new SoftJointLimit { limit = swing };
        }

        return body;
    }

    private Transform Bone(HumanBodyBones bone)
    {
        return _animator.GetBoneTransform(bone);
    }
}
