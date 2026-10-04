using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Decides who may join and where they appear. Netcode asks the host to approve every
// connection, including its own, so this logic only ever runs on the host.
public class PlayerSpawner : MonoBehaviour
{
    [Tooltip("One spawn point per player. When every point is taken, the match is full.")]
    public Transform[] spawnPoints;

    private readonly Dictionary<ulong, int> _slotByClient = new Dictionary<ulong, int>();
    private NetworkManager _networkManager;

    private void Start()
    {
        _networkManager = NetworkManager.Singleton;
        if (_networkManager == null)
        {
            Debug.LogError("PlayerSpawner needs a NetworkManager in the scene.", this);
            return;
        }

        _networkManager.ConnectionApprovalCallback = ApproveConnection;
        _networkManager.OnConnectionEvent += OnConnectionEvent;
        _networkManager.OnServerStopped += OnServerStopped;
    }

    private void OnDestroy()
    {
        if (_networkManager == null)
        {
            return;
        }

        if (_networkManager.ConnectionApprovalCallback == ApproveConnection)
        {
            _networkManager.ConnectionApprovalCallback = null;
        }

        _networkManager.OnConnectionEvent -= OnConnectionEvent;
        _networkManager.OnServerStopped -= OnServerStopped;
    }

    // Player transforms are owner-authoritative, so the host cannot move a player after it
    // spawns. The pose set here travels inside the spawn message instead, which puts the
    // player in the right place on every machine from the first frame.
    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        int slot = FindFreeSlot();
        if (slot < 0)
        {
            response.Approved = false;
            response.Reason = "The match is full.";
            return;
        }

        _slotByClient[request.ClientNetworkId] = slot;

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Position = spawnPoints[slot].position;
        response.Rotation = spawnPoints[slot].rotation;
    }

    // The pose a connected player first appeared at; used to send players back when a round restarts.
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

    private void OnConnectionEvent(NetworkManager networkManager, ConnectionEventData connectionEvent)
    {
        if (connectionEvent.EventType == ConnectionEvent.ClientDisconnected)
        {
            _slotByClient.Remove(connectionEvent.ClientId);
        }
    }

    private void OnServerStopped(bool wasHost)
    {
        _slotByClient.Clear();
    }
}
