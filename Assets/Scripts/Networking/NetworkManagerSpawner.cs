using Unity.Netcode;
using UnityEngine;

// Every scene carries one of these so it can be played on its own: in the build the
// Bootstrap scene creates the NetworkManager and it survives scene loads, but when a
// scene is started directly in the Editor (or by Multiplayer Play Mode) nobody has made
// one yet. Runs before other scripts so they can rely on NetworkManager.Singleton in Awake.
[DefaultExecutionOrder(-100)]
public class NetworkManagerSpawner : MonoBehaviour
{
    [Tooltip("Prefab with the NetworkManager and its transport. Only instantiated when no NetworkManager exists yet.")]
    public GameObject networkManagerPrefab;

    private void Awake()
    {
        if (NetworkManager.Singleton != null)
        {
            return;
        }

        if (networkManagerPrefab == null)
        {
            Debug.LogError("NetworkManagerSpawner has no prefab assigned.", this);
            return;
        }

        Instantiate(networkManagerPrefab);
    }
}
