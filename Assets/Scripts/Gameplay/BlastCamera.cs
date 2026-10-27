using Unity.Cinemachine;
using UnityEngine;

// A second camera for the blast: it follows a flying body and stays on it where it lies,
// until the next round puts the character back together. The player root never moved, so
// the normal camera would otherwise look at an empty spawn point. Cinemachine blends
// between the two by priority, so this only has to say which body to watch and when to stop.
public class BlastCamera : MonoBehaviour
{
    [Tooltip("The camera this switches on. Its own components do the following and framing.")]
    public CinemachineCamera cam;

    [Tooltip("Priority while a body is being followed; above the normal follow camera's 10.")]
    public int activePriority = 20;

    [Tooltip("Safety limit: longest the camera stays on a body, in game seconds, if no new round ever comes.")]
    public float maxSeconds = 20f;

    [Tooltip("How far behind the body the camera trails, in metres, and how high above it.")]
    public float distance = 6f;

    [Tooltip("Height of the camera above the body, in metres.")]
    public float height = 2.5f;

    public bool IsActive => _target != null;

    private RagdollBody _target;
    private float _time;
    private CinemachineFollow _follow;

    private void Awake()
    {
        if (cam == null)
        {
            cam = GetComponent<CinemachineCamera>();
        }

        _follow = cam.GetComponent<CinemachineFollow>();
        cam.Priority = 0;
    }

    // The body flies away from the blast, so the camera trails it from the blast's side:
    // that is where the players just stood, so it is open ground rather than a wall.
    public void Follow(RagdollBody body, Vector3 blastOrigin)
    {
        if (body == null || body.Hips == null)
        {
            return;
        }

        _target = body;
        _time = 0f;
        if (_follow != null)
        {
            Vector3 back = blastOrigin - body.Hips.position;
            back.y = 0f;
            if (back.sqrMagnitude < 0.01f)
            {
                back = -body.transform.forward;
                back.y = 0f;
            }

            _follow.FollowOffset = back.normalized * distance + Vector3.up * height;
        }

        cam.Follow = body.Hips;
        cam.LookAt = body.Hips;
        cam.Priority = activePriority;
    }

    public void Release()
    {
        _target = null;
        cam.Priority = 0;
    }

    private void Update()
    {
        if (_target == null)
        {
            return;
        }

        _time += Time.deltaTime;
        if (!_target.IsLimp || _time >= maxSeconds)
        {
            Release();
        }
    }
}
