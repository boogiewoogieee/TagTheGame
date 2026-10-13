using UnityEngine;
using UnityEngine.SceneManagement;

// First scene of the build. Its only job is to let the NetworkManager get created once,
// outside any scene that gets reloaded, and then move on to the menu.
public class Bootstrap : MonoBehaviour
{
    [Tooltip("Scene to load once the bootstrap scene has started, normally the main menu.")]
    public string firstScene = "MainMenu";

    private void Start()
    {
        SceneManager.LoadScene(firstScene);
    }
}
