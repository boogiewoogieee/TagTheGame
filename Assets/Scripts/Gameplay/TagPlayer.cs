using StarterAssets;
using Unity.Cinemachine;
using Unity.Netcode;
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

    [Tooltip("Renderers that get tinted so the two players look different.")]
    public Renderer[] tintedRenderers;

    [Tooltip("Tint for every player except the host, who keeps the original look.")]
    public Color otherPlayerTint = new Color(1f, 0.55f, 0.45f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private PlayerInput _playerInput;
    private StarterAssetsInputs _inputs;
    private ThirdPersonController _controller;
    private CharacterController _characterController;

    public override void OnNetworkSpawn()
    {
        _playerInput = GetComponent<PlayerInput>();
        _inputs = GetComponent<StarterAssetsInputs>();
        _controller = GetComponent<ThirdPersonController>();
        _characterController = GetComponent<CharacterController>();

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
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            // StarterAssetsInputs locked the cursor; free it so the UI is clickable after leaving.
            Cursor.lockState = CursorLockMode.None;
        }
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
