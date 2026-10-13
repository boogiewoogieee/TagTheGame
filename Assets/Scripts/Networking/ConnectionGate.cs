using Unity.Netcode;
using UnityEngine;

// Sits on the NetworkManager prefab, so it is in place before anyone connects, wherever
// the host happens to be. Netcode asks the host to approve every connection, including
// its own; this only caps the player count. Player objects are not created here: the
// PlayerSpawner in the arena spawns them once the arena is loaded.
public class ConnectionGate : MonoBehaviour
{
    [Tooltip("Players allowed in one match, the host included.")]
    [Min(1)]
    public int maxPlayers = 2;

    private NetworkManager _networkManager;

    private void Start()
    {
        _networkManager = GetComponent<NetworkManager>();
        if (_networkManager == null)
        {
            Debug.LogError("ConnectionGate must sit on the NetworkManager object.", this);
            return;
        }

        _networkManager.ConnectionApprovalCallback = ApproveConnection;
    }

    private void OnDestroy()
    {
        if (_networkManager != null && _networkManager.ConnectionApprovalCallback == ApproveConnection)
        {
            _networkManager.ConnectionApprovalCallback = null;
        }
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // Approved clients are added to the connected list straight away, so the count is current.
        if (_networkManager.ConnectedClientsIds.Count >= maxPlayers)
        {
            response.Approved = false;
            response.Reason = "The match is full.";
            return;
        }

        response.Approved = true;
        response.CreatePlayerObject = false;
    }
}
