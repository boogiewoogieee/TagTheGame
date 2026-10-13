using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// The networking half of the main menu. MenuController owns the screens; this owns
// hosting, joining, leaving and starting the match, and reports back through UnityEvents
// so the two scripts never reach into each other. The menu scene doubles as the lobby:
// the host starts the network here, the client joins here, and the host loads the arena
// for both when the match starts.
[RequireComponent(typeof(UIDocument))]
public class MenuNetwork : MonoBehaviour
{
    [Tooltip("Scene the host loads for everyone when the match starts.")]
    public string arenaScene = "Arena_01";

    [Tooltip("Port used by host and client.")]
    public ushort port = 7777;

    [Tooltip("How many one-second connection attempts a client makes before giving up.")]
    [Min(1)]
    public int connectAttempts = 5;

    [Tooltip("The lobby state in this scene. Start match goes through it so joining gets locked.")]
    public LobbyManager lobby;

    [Tooltip("Fired on the host once it is listening; the menu shows the lobby.")]
    public UnityEvent onHosted;

    [Tooltip("Fired on a client once the host accepted it; the menu shows the lobby.")]
    public UnityEvent onJoined;

    [Tooltip("Fired when this machine leaves or loses the game; the menu goes back to the joining screen.")]
    public UnityEvent onDisconnected;

    private const string AddressPrefsKey = "TagTheGame.JoinAddress";

    private NetworkManager _networkManager;
    private TextField _addressField;
    private Label _joinStatus;
    private Label _lobbyStatus;
    private Button _hostButton;
    private Button _joinButton;
    private Button _cancelButton;
    private Button _startButton;
    private Button _createLobbyButton;
    private string _localAddress;
    private bool _connecting;

    private void OnEnable()
    {
        _networkManager = NetworkManager.Singleton;
        if (_networkManager == null)
        {
            Debug.LogError("MenuNetwork needs a NetworkManager. Add a NetworkManagerSpawner to the scene.", this);
            return;
        }

        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        VisualElement joining = root.Q<VisualElement>("Joining");
        VisualElement multiplayer = root.Q<VisualElement>("Multiplayer");
        VisualElement lobby = root.Q<VisualElement>("Lobby");

        _addressField = joining.Q<TextField>("Address");
        _joinStatus = joining.Q<Label>("JoinStatus");
        _hostButton = joining.Q<Button>("Host");
        _joinButton = joining.Q<Button>("Join");
        _cancelButton = joining.Q<Button>("Cancel");
        _createLobbyButton = multiplayer.Q<Button>("Create_Lobby");
        _lobbyStatus = lobby.Q<Label>("LobbyStatus");
        _startButton = lobby.Q<Button>("StartMatch");
        Button leaveButton = lobby.Q<Button>("Leave");
        Button lobbyClose = lobby.Q<Button>("X");
        Button joiningClose = joining.Q<Button>("X");

        _addressField.value = PlayerPrefs.GetString(AddressPrefsKey, "127.0.0.1");
        _joinButton.clicked += Join;
        _createLobbyButton.clicked += Host;
        _cancelButton.clicked += Cancel;
        _startButton.clicked += StartMatch;
        leaveButton.clicked += Leave;
        lobbyClose.clicked += Leave;
        joiningClose.clicked += Cancel;

        _networkManager.OnServerStarted += OnServerStarted;
        _networkManager.OnServerStopped += OnServerStopped;
        _networkManager.OnClientConnectedCallback += OnClientConnected;
        _networkManager.OnClientStopped += OnClientStopped;
        _networkManager.OnTransportFailure += OnTransportFailure;

        _localAddress = FindLocalAddress();
        ShowIdle("");
    }

    private void OnDisable()
    {
        if (_networkManager == null)
        {
            return;
        }

        _joinButton.clicked -= Join;
        _createLobbyButton.clicked -= Host;
        _cancelButton.clicked -= Cancel;
        _startButton.clicked -= StartMatch;

        _networkManager.OnServerStarted -= OnServerStarted;
        _networkManager.OnServerStopped -= OnServerStopped;
        _networkManager.OnClientConnectedCallback -= OnClientConnected;
        _networkManager.OnClientStopped -= OnClientStopped;
        _networkManager.OnTransportFailure -= OnTransportFailure;
    }

    private void Update()
    {
        if (_networkManager == null || !_networkManager.IsListening)
        {
            return;
        }

        // The lobby text is cheap to rebuild, and polling avoids a callback per event.
        if (_networkManager.IsHost)
        {
            int players = _networkManager.ConnectedClientsIds.Count;
            bool full = players >= 2;
            _lobbyStatus.text = full
                ? players + " of 2 players. Ready to start."
                : "Waiting for a player to join...\nYour address: " + _localAddress + ", port " + port;
            _startButton.style.display = DisplayStyle.Flex;
            _startButton.SetEnabled(full);
        }
        else
        {
            _lobbyStatus.text = "Connected. Waiting for the host to start the match...";
            _startButton.style.display = DisplayStyle.None;
        }
    }

    public void Host()
    {
        if (_networkManager.IsListening)
        {
            return;
        }

        // Listen on every interface so a LAN partner can connect; the host itself connects locally.
        GetTransport().SetConnectionData("127.0.0.1", port, "0.0.0.0");
        if (!_networkManager.StartHost())
        {
            ShowIdle("Could not start hosting. Is another game already running on port " + port + "?");
        }
    }

    public void Join()
    {
        if (_networkManager.IsListening)
        {
            return;
        }

        string address = _addressField.value.Trim();
        if (address.Length == 0)
        {
            ShowIdle("Enter the host's address first.");
            return;
        }

        PlayerPrefs.SetString(AddressPrefsKey, address);
        UnityTransport transport = GetTransport();
        transport.SetConnectionData(address, port);
        transport.ConnectTimeoutMS = 1000;
        transport.MaxConnectAttempts = connectAttempts;

        _connecting = true;
        _joinStatus.text = "Connecting to " + address + "...";
        _hostButton.SetEnabled(false);
        _joinButton.SetEnabled(false);
        _cancelButton.style.display = DisplayStyle.Flex;
        _networkManager.StartClient();
    }

    public void Cancel()
    {
        if (_connecting)
        {
            _connecting = false;
            _networkManager.Shutdown();
            ShowIdle("");
        }
    }

    public void Leave()
    {
        if (_networkManager.IsListening)
        {
            _networkManager.Shutdown();
        }
    }

    // Host only. Loads the arena for everyone; PlayerSpawner puts the players in once both have it.
    public void StartMatch()
    {
        if (!_networkManager.IsHost || _networkManager.ConnectedClientsIds.Count < 2)
        {
            return;
        }

        _startButton.SetEnabled(false);
        if (lobby != null)
        {
            lobby.LockJoining();
        }

        _networkManager.SceneManager.LoadScene(arenaScene, LoadSceneMode.Single);
    }

    private void OnServerStarted()
    {
        onHosted.Invoke();
    }

    private void OnServerStopped(bool wasHost)
    {
        ShowIdle("");
        onDisconnected.Invoke();
    }

    private void OnClientConnected(ulong clientId)
    {
        // The host hears about every client; only a joining client needs the lobby shown.
        if (!_networkManager.IsHost && clientId == _networkManager.LocalClientId)
        {
            _connecting = false;
            ShowIdle("");
            onJoined.Invoke();
        }
    }

    private void OnClientStopped(bool wasHost)
    {
        if (wasHost)
        {
            return;
        }

        // The host's refusal ("The match is full.") arrives as the reason. Netcode's own
        // bracketed transport messages are not meant for players, so they are replaced.
        string reason = _networkManager.DisconnectReason;
        if (string.IsNullOrEmpty(reason) || reason.StartsWith("["))
        {
            reason = "";
        }

        if (_connecting)
        {
            _connecting = false;
            ShowIdle(string.IsNullOrEmpty(reason) ? "Could not connect to " + _addressField.value.Trim() + "." : reason);
        }
        else
        {
            ShowIdle(string.IsNullOrEmpty(reason) ? "Host left." : reason);
        }

        onDisconnected.Invoke();
    }

    private void OnTransportFailure()
    {
        _connecting = false;
        ShowIdle("Network error. Check the address and port.");
    }

    private void ShowIdle(string message)
    {
        _joinStatus.text = message;
        _hostButton.SetEnabled(true);
        _joinButton.SetEnabled(true);
        _cancelButton.style.display = DisplayStyle.None;
        _startButton.SetEnabled(false);
    }

    private UnityTransport GetTransport()
    {
        return (UnityTransport)_networkManager.NetworkConfig.NetworkTransport;
    }

    // Shown to the host so a LAN partner knows what to type. Adapters without a network
    // give themselves 169.254.x.x addresses, so those only count when nothing else exists.
    private static string FindLocalAddress()
    {
        string fallback = "127.0.0.1";
        try
        {
            foreach (IPAddress address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                {
                    continue;
                }

                if (!address.ToString().StartsWith("169.254."))
                {
                    return address.ToString();
                }

                fallback = address.ToString();
            }
        }
        catch (SocketException)
        {
        }

        return fallback;
    }
}
