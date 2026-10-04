using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class MenuController : MonoBehaviour
{
    // --- KONTENERY G£ÓWNE (EKRANY) ---
    private VisualElement screenMain;
    private VisualElement screenJoining;
    private VisualElement screenMultiplayer;
    private VisualElement screenSettings;
    private VisualElement screenLobby;
    private VisualElement screenPopUp;

    // --- PRZYCISKI Z MENU G£ÓWNEGO ---
    private Button btnMultiplayer;
    private Button btnSettings;
    private Button btnExit;

    // --- PRZYCISKI Z OKNA JOINING ---
    private Button btnHostLobby;
    private Button btnJoinLobby;

    // --- PRZYCISK Z OKNA MULTIPLAYER SETTINGS ---
    private Button btnCreateLobby;

    // --- PRZYCISKI Z POP-UPU ---
    private Button btnPopUpRestart;
    private Button btnPopUpExit;
    private Label lblPopUpResult;

    // --- PRZYCISKI "X" ---
    private Button btnCloseJoining;
    private Button btnCloseMultiplayer;
    private Button btnCloseSettings;
    private Button btnCloseLobby;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // 1. POBIERANIE EKRANÓW G£ÓWNYCH Z ROOT'a
        screenMain = root.Q<VisualElement>("Screen_Main");
        screenJoining = root.Q<VisualElement>("Joining");
        screenMultiplayer = root.Q<VisualElement>("Multiplayer");
        screenSettings = root.Q<VisualElement>("Settings");
        screenLobby = root.Q<VisualElement>("Lobby");
        screenPopUp = root.Q<VisualElement>("PopUp");

        // 2. POBIERANIE PRZYCISKÓW WNÊTRZ EKRANÓW
        if (screenMain != null)
        {
            btnMultiplayer = screenMain.Q<Button>("Screen_Multiplayer");
            btnSettings = screenMain.Q<Button>("Screen_Settings");
            btnExit = screenMain.Q<Button>("Screen_Exit");
        }

        if (screenJoining != null)
        {
            btnHostLobby = screenJoining.Q<Button>("Host");
            btnJoinLobby = screenJoining.Q<Button>("Join");
            btnCloseJoining = screenJoining.Q<Button>("X");
        }

        if (screenMultiplayer != null)
        {
            btnCreateLobby = screenMultiplayer.Q<Button>("CreatLobby");
            btnCloseMultiplayer = screenMultiplayer.Q<Button>("X");
        }

        if (screenSettings != null)
        {
            btnCloseSettings = screenSettings.Q<Button>("X");
        }

        if (screenLobby != null)
        {
            btnCloseLobby = screenLobby.Q<Button>("X");
        }

        if (screenPopUp != null)
        {
            btnPopUpRestart = screenPopUp.Q<Button>("Restart");
            btnPopUpExit = screenPopUp.Q<Button>("Exit");
            lblPopUpResult = screenPopUp.Q<Label>();
        }

        // 3. REJESTRACJA KLIKNIÊÆ
        if (btnMultiplayer != null) btnMultiplayer.clicked += OpenJoiningScreen;
        if (btnSettings != null) btnSettings.clicked += OpenSettingsScreen;
        if (btnExit != null) btnExit.clicked += QuitGame;

        if (btnHostLobby != null) btnHostLobby.clicked += OpenMultiplayerSettingsScreen;
        if (btnJoinLobby != null) btnJoinLobby.clicked += OpenLobbyScreen;
        if (btnCreateLobby != null) btnCreateLobby.clicked += OpenLobbyScreen; // <--- Przenosi do Lobby!

        if (btnCloseJoining != null) btnCloseJoining.clicked += OpenMainScreen;
        if (btnCloseSettings != null) btnCloseSettings.clicked += OpenMainScreen;
        if (btnCloseMultiplayer != null) btnCloseMultiplayer.clicked += OpenJoiningScreen;
        if (btnCloseLobby != null) btnCloseLobby.clicked += OpenJoiningScreen;

        if (btnPopUpRestart != null) btnPopUpRestart.clicked += OpenMainScreen;
        if (btnPopUpExit != null) btnPopUpExit.clicked += QuitGame;

        // 4. START GRY
        OpenMainScreen();
    }

    void OnDisable()
    {
        if (btnMultiplayer != null) btnMultiplayer.clicked -= OpenJoiningScreen;
        if (btnSettings != null) btnSettings.clicked -= OpenSettingsScreen;
        if (btnExit != null) btnExit.clicked -= QuitGame;

        if (btnHostLobby != null) btnHostLobby.clicked -= OpenMultiplayerSettingsScreen;
        if (btnJoinLobby != null) btnJoinLobby.clicked -= OpenLobbyScreen;
        if (btnCreateLobby != null) btnCreateLobby.clicked -= OpenLobbyScreen;

        if (btnCloseJoining != null) btnCloseJoining.clicked -= OpenMainScreen;
        if (btnCloseSettings != null) btnCloseSettings.clicked -= OpenMainScreen;
        if (btnCloseMultiplayer != null) btnCloseMultiplayer.clicked -= OpenJoiningScreen;
        if (btnCloseLobby != null) btnCloseLobby.clicked -= OpenJoiningScreen;

        if (btnPopUpRestart != null) btnPopUpRestart.clicked -= OpenMainScreen;
        if (btnPopUpExit != null) btnPopUpExit.clicked -= QuitGame;
    }

    private void HideAllScreens()
    {
        if (screenMain != null) screenMain.style.display = DisplayStyle.None;
        if (screenJoining != null) screenJoining.style.display = DisplayStyle.None;
        if (screenMultiplayer != null) screenMultiplayer.style.display = DisplayStyle.None;
        if (screenSettings != null) screenSettings.style.display = DisplayStyle.None;
        if (screenLobby != null) screenLobby.style.display = DisplayStyle.None;
        if (screenPopUp != null) screenPopUp.style.display = DisplayStyle.None;
    }

    private void ShowScreen(VisualElement screen)
    {
        HideAllScreens();
        if (screen != null)
        {
            screen.style.display = DisplayStyle.Flex;
            StartCoroutine(RefreshLayoutNextFrame(screen));
        }
    }

    private IEnumerator RefreshLayoutNextFrame(VisualElement screen)
    {
        yield return null;
        if (screen != null)
        {
            screen.MarkDirtyRepaint();
        }
    }

    private void OpenMainScreen() => ShowScreen(screenMain);
    private void OpenJoiningScreen() => ShowScreen(screenJoining);
    private void OpenMultiplayerSettingsScreen() => ShowScreen(screenMultiplayer);
    private void OpenSettingsScreen() => ShowScreen(screenSettings);
    private void OpenLobbyScreen() => ShowScreen(screenLobby);

    public void ShowGameOverPopUp(bool isWinner)
    {
        if (screenPopUp != null)
        {
            if (lblPopUpResult != null)
            {
                if (isWinner) lblPopUpResult.text = "You Win!";
                else lblPopUpResult.text = "You Lose!";
            }
            screenPopUp.style.display = DisplayStyle.Flex;
            StartCoroutine(RefreshLayoutNextFrame(screenPopUp));
        }
    }

    private void QuitGame()
    {
        Debug.Log("Zamykanie aplikacji...");
        Application.Quit();
    }
}