using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// The way out of the arena. Leaving shuts the network down and returns to the menu, and
// losing the connection (the host left, or the link dropped) does the same on its own,
// so nobody is left staring at an empty arena. The results screen will build on this.
public class MatchExit : MonoBehaviour
{
    [Tooltip("Scene to load after leaving the match.")]
    public string menuScene = "MainMenu";

    private NetworkManager _networkManager;
    private bool _leaving;

    private void OnEnable()
    {
        _networkManager = NetworkManager.Singleton;
        if (_networkManager == null)
        {
            return;
        }

        _networkManager.OnClientStopped += OnNetworkStopped;
        _networkManager.OnServerStopped += OnNetworkStopped;
    }

    private void OnDisable()
    {
        if (_networkManager == null)
        {
            return;
        }

        _networkManager.OnClientStopped -= OnNetworkStopped;
        _networkManager.OnServerStopped -= OnNetworkStopped;
    }

    // Called by the HUD's Leave button. The host leaving ends the match for the client too.
    public void LeaveToMenu()
    {
        if (_leaving)
        {
            return;
        }

        _leaving = true;
        if (_networkManager != null && _networkManager.IsListening)
        {
            // The menu loads from the stopped callback, once Netcode has cleaned up the players.
            _networkManager.Shutdown();
            return;
        }

        SceneManager.LoadScene(menuScene);
    }

    private void OnNetworkStopped(bool wasHost)
    {
        // Only the arena started from the menu goes back to it; the Editor shortcut has no menu to return to.
        if (!Application.isPlaying || !gameObject.activeInHierarchy)
        {
            return;
        }

        _leaving = true;
        SceneManager.LoadScene(menuScene);
    }
}
