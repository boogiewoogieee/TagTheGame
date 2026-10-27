using Unity.Cinemachine;
using UnityEngine;

// A short field-of-view kick on the follow camera, for GO and other hits. It is called
// from UnityEvents (for example RoundCountdown.onGo) and leaves the lens as it found it.
[RequireComponent(typeof(CinemachineCamera))]
public class CameraFovPunch : MonoBehaviour
{
    [Tooltip("How far the field of view widens at the peak, in degrees.")]
    public float degrees = 4f;

    [Tooltip("How long the punch lasts, out and back, in seconds.")]
    [Min(0.05f)]
    public float seconds = 0.3f;

    private CinemachineCamera _camera;
    private float _baseFov;
    private float _time = float.MaxValue;

    private void Awake()
    {
        _camera = GetComponent<CinemachineCamera>();
    }

    public void Punch()
    {
        // A punch during a punch keeps the original lens, so the base value never drifts.
        if (_time >= seconds)
        {
            _baseFov = _camera.Lens.FieldOfView;
        }

        _time = 0f;
    }

    private void LateUpdate()
    {
        if (_time >= seconds)
        {
            return;
        }

        _time += Time.deltaTime;
        float bump = Mathf.Sin(Mathf.Clamp01(_time / seconds) * Mathf.PI);
        LensSettings lens = _camera.Lens;
        lens.FieldOfView = _time >= seconds ? _baseFov : _baseFov + degrees * bump;
        _camera.Lens = lens;
    }
}
