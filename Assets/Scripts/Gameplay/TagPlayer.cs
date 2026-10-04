using StarterAssets;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

// One networked player. Every machine has a copy of every player, but only the copy
// on the owning machine reads input and moves itself; the others follow the network.
public class TagPlayer : NetworkBehaviour
{
    [Tooltip("Followed by the Cinemachine camera on the machine that owns this player.")]
    public Transform cameraRoot;

    [Tooltip("The bomb sits here while this player holds it.")]
    public Transform bombAnchor;

    [Tooltip("The ears of this player. Switched on only on the machine that owns the player, so sounds are heard from the character and not from the camera behind it.")]
    public AudioListener listener;

    [Tooltip("Renderers that get tinted so the two players look different.")]
    public Renderer[] tintedRenderers;

    [Tooltip("Tint for every player except the host, who keeps the original look.")]
    public Color otherPlayerTint = new Color(1f, 0.55f, 0.45f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private PlayerInput _playerInput;
    private StarterAssetsInputs _inputs;
    private ThirdPersonController _controller;
    private CharacterController _characterController;
    private NetworkTransform _networkTransform;
    private MatchManager _match;
    private AudioListener _cameraListener;

    public override void OnNetworkSpawn()
    {
        _playerInput = GetComponent<PlayerInput>();
        _inputs = GetComponent<StarterAssetsInputs>();
        _controller = GetComponent<ThirdPersonController>();
        _characterController = GetComponent<CharacterController>();
        _networkTransform = GetComponent<NetworkTransform>();

        if (OwnerClientId != Unity.Netcode.NetworkManager.ServerClientId)
        {
            ApplyTint(otherPlayerTint);
        }

        if (IsOwner)
        {
            TakeControl();
        }
        else
        {
            // The landing and footstep clips call animation events on ThirdPersonController.
            // On a copy whose Start never ran those handlers would throw, so the script is
            // removed here; the empty receivers at the bottom handle the events instead.
            Destroy(_controller);
        }

        _match = FindAnyObjectByType<MatchManager>(FindObjectsInactive.Include);
        if (_match != null)
        {
            _match.RegisterPlayer(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_match != null)
        {
            _match.UnregisterPlayer(this);
        }

        if (IsOwner)
        {
            // StarterAssetsInputs locked the cursor; free it so the UI is clickable after leaving.
            Cursor.lockState = CursorLockMode.None;

            // Hand listening back to the camera, in the same order as below: never two at once.
            if (listener != null)
            {
                listener.enabled = false;
            }

            if (_cameraListener != null)
            {
                _cameraListener.enabled = true;
            }
        }
    }

    // Freezes or releases this player on the machine that owns it. The controller keeps
    // running, so gravity and the idle animation still work while frozen.
    public void SetInputEnabled(bool inputEnabled)
    {
        if (!IsOwner || _playerInput.enabled == inputEnabled)
        {
            return;
        }

        _playerInput.enabled = inputEnabled;
        _inputs.cursorLocked = inputEnabled;
        Cursor.lockState = inputEnabled ? CursorLockMode.Locked : CursorLockMode.None;

        if (!inputEnabled)
        {
            // PlayerInput stops sending values, so clear the last ones or the character keeps walking.
            _inputs.move = Vector2.zero;
            _inputs.look = Vector2.zero;
            _inputs.jump = false;
            _inputs.sprint = false;
        }
    }

    // Sent by the host when a round restarts. The transform is owner-authoritative, so the
    // host cannot move the player; the owner moves itself and tells everyone it teleported.
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void ReturnToSpawnRpc(Vector3 position, Quaternion rotation)
    {
        // The CharacterController would undo the move if it stayed on.
        _characterController.enabled = false;
        _networkTransform.Teleport(position, rotation, transform.localScale);
        _characterController.enabled = true;
    }

    // The prefab ships with input and movement switched off, so only the owner ever drives it.
    private void TakeControl()
    {
        _characterController.enabled = true;
        _inputs.enabled = true;
        _controller.enabled = true;
        _playerInput.enabled = true;

        CinemachineCamera followCamera = FindAnyObjectByType<CinemachineCamera>();
        if (followCamera != null)
        {
            followCamera.Follow = cameraRoot;
        }

        if (_inputs.cursorLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        // Unity allows one active listener. The scene has one on the main camera; switch it
        // off before this one goes on, so distance is measured from the character.
        if (listener != null)
        {
            Camera mainCamera = Camera.main;
            _cameraListener = mainCamera != null ? mainCamera.GetComponent<AudioListener>() : null;
            if (_cameraListener != null)
            {
                _cameraListener.enabled = false;
            }

            listener.enabled = true;
        }
    }

    private void ApplyTint(Color tint)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer tinted in tintedRenderers)
        {
            tinted.GetPropertyBlock(block);
            block.SetColor(BaseColorId, tint);
            tinted.SetPropertyBlock(block);
        }
    }

    private void OnLand(AnimationEvent animationEvent)
    {
    }

    private void OnFootstep(AnimationEvent animationEvent)
    {
    }
}
