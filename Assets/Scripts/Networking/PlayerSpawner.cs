using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Puts a player object into the arena for every connected client and remembers where
// each one appeared. Lives in the arena scene, so it only exists while the arena is
// loaded, and spawns in two situations: the menu flow, where both players are already
// connected and the host loads the arena (spawn when everyone has finished loading), and
// the Editor shortcut, where the arena is started directly and players connect into it.
// Only the host spawns; the approval cap lives in ConnectionGate on the NetworkManager.
public class PlayerSpawner : MonoBehaviour
{
    [Tooltip("One spawn point per player. When every point is taken, no more players spawn.")]
    public Transform[] spawnPoints;

    [Tooltip("Player prefab for everyone except the host, who gets the default player prefab of the NetworkManager. Leave empty to give every player the default.")]
    public GameObject clientPlayerPrefab;

    private readonly Dictionary<ulong, int> _slotByClient = new Dictionary<ulong, int>();
    private NetworkManager _networkManager;
    private bool _sceneEventsHooked;

    private void OnEnable()
    {
        _networkManager = NetworkManager.Singleton;
        if (_networkManager == null)
        {
            Debug.LogError("PlayerSpawner needs a NetworkManager. Add a NetworkManagerSpawner to the scene.", this);
            return;
        }

        _networkManager.OnServerStarted += OnServerStarted;
        _networkManager.OnServerStopped += OnServerStopped;
        _networkManager.OnConnectionEvent += OnConnectionEvent;

        // The scene manager only exists while the network runs. In the menu flow it already
        // does when the arena loads; in the Editor shortcut it appears with OnServerStarted.
        if (_networkManager.IsListening)
        {
            HookSceneEvents();
        }
    }

    private void OnDisable()
    {
        if (_networkManager == null)
        {
            return;
        }

        _networkManager.OnServerStarted -= OnServerStarted;
        _networkManager.OnServerStopped -= OnServerStopped;
        _networkManager.OnConnectionEvent -= OnConnectionEvent;
        UnhookSceneEvents();
        _slotByClient.Clear();
    }

    // The pose a player first appeared at; used to send players back when a round restarts.
    public bool TryGetSpawnPose(ulong clientId, out Vector3 position, out Quaternion rotation)
    {
        if (_slotByClient.TryGetValue(clientId, out int slot))
        {
            position = spawnPoints[slot].position;
            rotation = spawnPoints[slot].rotation;
            return true;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }

    private void HookSceneEvents()
    {
        if (_sceneEventsHooked || _networkManager.SceneManager == null)
        {
            return;
        }

        _networkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        _networkManager.SceneManager.OnSynchronizeComplete += OnSynchronizeComplete;
        _sceneEventsHooked = true;
    }

    private void UnhookSceneEvents()
    {
        if (!_sceneEventsHooked)
        {
            return;
        }

        if (_networkManager.SceneManager != null)
        {
            _networkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            _networkManager.SceneManager.OnSynchronizeComplete -= OnSynchronizeComplete;
        }

        _sceneEventsHooked = false;
    }

    // Editor shortcut: the host started inside the arena, so its own player can appear now.
    private void OnServerStarted()
    {
        HookSceneEvents();
        SpawnPlayerFor(NetworkManager.ServerClientId);
    }

    private void OnServerStopped(bool wasHost)
    {
        UnhookSceneEvents();
        _slotByClient.Clear();
    }

    // Menu flow: the host loaded the arena for everyone, and all of them have finished.
    private void OnLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!_networkManager.IsServer || sceneName != gameObject.scene.name)
        {
            return;
        }

        foreach (ulong clientId in clientsCompleted)
        {
            SpawnPlayerFor(clientId);
        }
    }

    // Editor shortcut: a client joined the running arena and has finished loading it.
    private void OnSynchronizeComplete(ulong clientId)
    {
        if (_networkManager.IsServer)
        {
            SpawnPlayerFor(clientId);
        }
    }

    private void OnConnectionEvent(NetworkManager networkManager, ConnectionEventData connectionEvent)
    {
        if (connectionEvent.EventType == ConnectionEvent.ClientDisconnected)
        {
            _slotByClient.Remove(connectionEvent.ClientId);
        }
    }

    private void SpawnPlayerFor(ulong clientId)
    {
        if (!_networkManager.IsServer || !_networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client.PlayerObject != null)
        {
            return;
        }

        int slot = FindFreeSlot();
        if (slot < 0)
        {
            Debug.LogWarning("No free spawn point for client " + clientId + ".", this);
            return;
        }

        // The host keeps the default player prefab; everyone else gets the other character.
        GameObject prefab = _networkManager.NetworkConfig.PlayerPrefab;
        if (clientPlayerPrefab != null && clientId != NetworkManager.ServerClientId)
        {
            prefab = clientPlayerPrefab;
        }

        _slotByClient[clientId] = slot;
        GameObject player = Instantiate(prefab, spawnPoints[slot].position, spawnPoints[slot].rotation);

        // Spawned with the arena, so leaving the arena destroys the players with it.
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, destroyWithScene: true);
    }

    private int FindFreeSlot()
    {
        for (int slot = 0; slot < spawnPoints.Length; slot++)
        {
            if (!_slotByClient.ContainsValue(slot))
            {
                return slot;
            }
        }

        return -1;
    }
}
