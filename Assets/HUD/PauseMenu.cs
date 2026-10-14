using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// The in-game menu, opened and closed with Esc (or Start on a gamepad). The match is
// networked, so nothing actually pauses: the menu only freezes this player's own
// character and frees the mouse, and the round keeps running for both players.
[RequireComponent(typeof(UIDocument))]
public class PauseMenu : MonoBehaviour
{
    [Tooltip("Fired when the player chooses Leave to menu.")]
    public UnityEvent onLeave;

    public bool IsOpen { get; private set; }

    private VisualElement _menu;
    private Button _resume;
    private Button _leave;
    private Button _quit;

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        _menu = root.Q<VisualElement>("Menu");
        _resume = _menu.Q<Button>("Resume");
        _leave = _menu.Q<Button>("MenuLeave");
        _quit = _menu.Q<Button>("Quit");

        _resume.clicked += Close;
        _leave.clicked += Leave;
        _quit.clicked += Quit;
        SetOpen(false);
    }

    private void OnDisable()
    {
        if (_menu == null)
        {
            return;
        }

        _resume.clicked -= Close;
        _leave.clicked -= Leave;
        _quit.clicked -= Quit;
    }

    private void Update()
    {
        bool togglePressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
        if (togglePressed)
        {
            Toggle();
        }

        // The player may spawn while the menu is already open; keep it frozen until closed.
        if (IsOpen)
        {
            TagPlayer localPlayer = FindLocalPlayer();
            if (localPlayer != null)
            {
                localPlayer.SetMenuOpen(true);
            }
        }
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void Close()
    {
        SetOpen(false);
    }

    private void Leave()
    {
        onLeave.Invoke();
    }

    private void Quit()
    {
        // Does nothing in the Editor; closes the game in a build.
        Application.Quit();
    }

    private void SetOpen(bool open)
    {
        IsOpen = open;
        _menu.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

        TagPlayer localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            localPlayer.SetMenuOpen(open);
        }
        else if (open)
        {
            // No character to hand the cursor to yet, so free it here.
            UnityEngine.Cursor.lockState = CursorLockMode.None;
        }
    }

    private static TagPlayer FindLocalPlayer()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.LocalClient == null || networkManager.LocalClient.PlayerObject == null)
        {
            return null;
        }

        return networkManager.LocalClient.PlayerObject.GetComponent<TagPlayer>();
    }
}
