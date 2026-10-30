using System.Collections.Generic;
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

    [Tooltip("The ears of this player. Switched on only on the machine that owns the player, so sounds are heard from the character and not from the camera behind it.")]
    public AudioListener listener;

    [Tooltip("The characters players can pick in the lobby. A pick is an index into this list.")]
    public CharacterCatalog characters;

    [Tooltip("Parent of the character model. At spawn its children are replaced by the picked character.")]
    public Transform modelParent;

    [Tooltip("Where the bomb sits, measured from the chest bone of the prefab's own model. A picked character uses its catalog value instead.")]
    public Vector3 bombLocalPosition;

    [Tooltip("How the bomb is turned, relative to the chest bone of the prefab's own model. A picked character uses its catalog value instead.")]
    public Vector3 bombLocalEulerAngles;

    // Written by the host as the player spawns, so it arrives with the player on every machine.
    public NetworkVariable<int> CharacterIndex = new NetworkVariable<int>(CharacterRules.None);

    public int CharacterCount => characters != null ? characters.Count : 0;

    // The bomb is parented here while this player holds it. Created when the player spawns.
    public Transform BombAnchor { get; private set; }

    private PlayerInput _playerInput;
    private StarterAssetsInputs _inputs;
    private ThirdPersonController _controller;
    private CharacterController _characterController;
    private NetworkTransform _networkTransform;
    private MatchManager _match;
    private AudioListener _cameraListener;

    // The host's pick for this player, handed over before the spawn.
    private int _pickForSpawn = CharacterRules.None;

    // Two things can freeze the player: the match (round over) and the in-game menu.
    // Input only comes back when neither of them is holding it.
    private bool _matchAllowsInput = true;
    private bool _menuOpen;

    // The countdown freezes the player but keeps the cursor hidden; the round-over card frees it.
    private bool _keepCursorLocked;
    private bool _cursorLocked;

    // Speed changes from several sources (Fuse Rush now; modifiers and pickups later) multiply.
    private readonly Dictionary<Object, float> _speedFactors = new Dictionary<Object, float>();
    private float _baseMoveSpeed;
    private float _baseSprintSpeed;

    // True on the owning machine while the player may act: not frozen by the match or the menu.
    public bool InputEnabled => IsOwner && _matchAllowsInput && !_menuOpen;

    // Host only, before the spawn. NGO warns about network variables written before the
    // object is spawned, so the pick waits here until OnNetworkSpawn writes it. That still
    // happens before the spawn message is built, so clients get it with the spawn.
    public void SetCharacterForSpawn(int index)
    {
        _pickForSpawn = index;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && _pickForSpawn != CharacterRules.None)
        {
            CharacterIndex.Value = _pickForSpawn;
        }

        _playerInput = GetComponent<PlayerInput>();
        _inputs = GetComponent<StarterAssetsInputs>();
        _controller = GetComponent<ThirdPersonController>();
        _characterController = GetComponent<CharacterController>();
        _networkTransform = GetComponent<NetworkTransform>();
        ApplyCharacter(characters != null ? characters.Get(CharacterIndex.Value) : null);
        BombAnchor = CreateBombAnchor();

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

            // Unity tells disabled scripts too when the window regains focus, and this one
            // would then lock the mouse, trapping it on the round-over screen and the menu.
            Destroy(_inputs);
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

    // Called by the match every frame: false while the round is over or about to start.
    public void SetInputEnabled(bool inputEnabled, bool keepCursorLocked = false)
    {
        _matchAllowsInput = inputEnabled;
        _keepCursorLocked = keepCursorLocked;
        ApplyInputState();
    }

    // Owner only. Sets one source's speed multiple (1 removes it); the controller gets the product of all of them.
    public void SetSpeedFactor(Object source, float factor)
    {
        if (!IsOwner || _controller == null)
        {
            return;
        }

        bool known = _speedFactors.TryGetValue(source, out float current);
        if (Mathf.Approximately(factor, 1f))
        {
            if (!known)
            {
                return;
            }

            _speedFactors.Remove(source);
        }
        else if (known && Mathf.Approximately(current, factor))
        {
            return;
        }
        else
        {
            _speedFactors[source] = factor;
        }

        float product = 1f;
        foreach (float value in _speedFactors.Values)
        {
            product *= value;
        }

        _controller.MoveSpeed = _baseMoveSpeed * product;
        _controller.SprintSpeed = _baseSprintSpeed * product;
    }

    // Called by the in-game menu when it opens or closes.
    public void SetMenuOpen(bool menuOpen)
    {
        _menuOpen = menuOpen;
        ApplyInputState();
    }

    // Freezes or releases this player on the machine that owns it. The controller keeps
    // running, so gravity and the idle animation still work while frozen.
    private void ApplyInputState()
    {
        if (!IsOwner)
        {
            return;
        }

        bool inputEnabled = _matchAllowsInput && !_menuOpen;
        bool lockCursor = inputEnabled || (_keepCursorLocked && !_menuOpen);
        if (lockCursor != _cursorLocked)
        {
            _cursorLocked = lockCursor;
            _inputs.cursorLocked = lockCursor;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        }

        if (_playerInput.enabled == inputEnabled)
        {
            return;
        }

        _playerInput.enabled = inputEnabled;

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
        _baseMoveSpeed = _controller.MoveSpeed;
        _baseSprintSpeed = _controller.SprintSpeed;

        // The arena also has the blast camera; the follow camera is the one without a BlastCamera.
        foreach (CinemachineCamera candidate in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude))
        {
            if (candidate.GetComponent<BlastCamera>() == null)
            {
                candidate.Follow = cameraRoot;
                break;
            }
        }

        if (_inputs.cursorLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            _cursorLocked = true;
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

    // Swaps the prefab's model for the picked one. Runs before the bomb anchor and the
    // ragdoll look for bones, so both find the new skeleton.
    private void ApplyCharacter(CharacterCatalog.Entry entry)
    {
        if (entry == null || entry.model == null || modelParent == null)
        {
            return;
        }

        // Destroyed at once rather than at the end of the frame: until then the Animator
        // would still find the old skeleton's bones by name when it binds the new one.
        for (int i = modelParent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(modelParent.GetChild(i).gameObject);
        }

        GameObject model = Instantiate(entry.model, modelParent, false);
        model.name = entry.model.name;

        // One Animator drives the character, the one on the root that ThirdPersonController
        // and NetworkAnimator use; it takes the model's avatar and the copy loses its own.
        Animator animator = GetComponent<Animator>();
        Animator modelAnimator = model.GetComponent<Animator>();
        if (modelAnimator != null)
        {
            animator.avatar = modelAnimator.avatar;
            DestroyImmediate(modelAnimator);
        }

        animator.Rebind();
        bombLocalPosition = entry.bombLocalPosition;
        bombLocalEulerAngles = entry.bombLocalEulerAngles;
    }

    // The chest is found through the humanoid mapping of the Animator instead of a bone name,
    // so the same code works for every humanoid character model.
    private Transform CreateBombAnchor()
    {
        Animator animator = GetComponent<Animator>();
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (chest == null)
        {
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        }

        if (chest == null)
        {
            chest = animator.GetBoneTransform(HumanBodyBones.Spine);
        }

        if (chest == null)
        {
            // Not a humanoid model: keep the bomb on the player root rather than fail.
            chest = transform;
        }

        Transform anchor = new GameObject("BombAnchor").transform;
        anchor.SetParent(chest, false);
        anchor.SetLocalPositionAndRotation(bombLocalPosition, Quaternion.Euler(bombLocalEulerAngles));

        // Cancel the scale of the model so the bomb keeps its own size on every character.
        Vector3 scale = chest.lossyScale;
        anchor.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
        return anchor;
    }

    private void OnLand(AnimationEvent animationEvent)
    {
    }

    private void OnFootstep(AnimationEvent animationEvent)
    {
    }
}
