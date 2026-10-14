using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

// The networking half of the main menu. MenuController owns the screens and LobbyPanel
// the lobby; this owns hosting, joining and leaving, and reports back through UnityEvents
// so the scripts never reach into each other. The menu scene doubles as the lobby: the
// host starts the network here and the client joins here.
[RequireComponent(typeof(UIDocument))]
public class MenuNetwork : MonoBehaviour
{
    [Tooltip("Port used by host and client.")]
    public ushort port = 7777;

    [Tooltip("How many one-second connection attempts a client makes before giving up.")]
    [Min(1)]
    public int connectAttempts = 5;

    [Tooltip("The lobby state in this scene. It gets this player's name before hosting or joining.")]
    public LobbyManager lobby;

    [Tooltip("Fired on the host once it is listening; the menu shows the lobby.")]
    public UnityEvent onHosted;

    [Tooltip("Fired on a client once the host accepted it; the menu shows the lobby.")]
    public UnityEvent onJoined;

    [Tooltip("Fired when this machine leaves or loses the game; the menu goes back to the joining screen.")]
    public UnityEvent onDisconnected;

    private const string AddressPrefsKey = "TagTheGame.JoinAddress";
    private const string NamePrefsKey = "TagTheGame.PlayerName";

    // This machine's LAN address, which the lobby shows the host.
    public string LocalAddress { get; private set; }

    private NetworkManager _networkManager;
    private TextField _nameField;
    private TextField _addressField;
    private Label _joinStatus;
    private Button _hostButton;
    private Button _joinButton;
    private Button _cancelButton;
    private Button _createLobbyButton;
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

        _nameField = joining.Q<TextField>("PlayerName");
        _addressField = joining.Q<TextField>("Address");
        _joinStatus = joining.Q<Label>("JoinStatus");
        _hostButton = joining.Q<Button>("Host");
        _joinButton = joining.Q<Button>("Join");
        _cancelButton = joining.Q<Button>("Cancel");
        _createLobbyButton = multiplayer.Q<Button>("Create_Lobby");
        Button leaveButton = lobby.Q<Button>("Leave");
        Button lobbyClose = lobby.Q<Button>("X");
        Button joiningClose = joining.Q<Button>("X");

        _nameField.value = PlayerPrefs.GetString(NamePrefsKey, "");
        _addressField.value = PlayerPrefs.GetString(AddressPrefsKey, "127.0.0.1");
        _joinButton.clicked += Join;
        _createLobbyButton.clicked += Host;
        _cancelButton.clicked += Cancel;
        leaveButton.clicked += Leave;
        lobbyClose.clicked += Leave;
        joiningClose.clicked += Cancel;

        _networkManager.OnServerStarted += OnServerStarted;
        _networkManager.OnServerStopped += OnServerStopped;
        _networkManager.OnClientConnectedCallback += OnClientConnected;
        _networkManager.OnClientStopped += OnClientStopped;
        _networkManager.OnTransportFailure += OnTransportFailure;

        LocalAddress = FindLocalAddress();
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

        _networkManager.OnServerStarted -= OnServerStarted;
        _networkManager.OnServerStopped -= OnServerStopped;
        _networkManager.OnClientConnectedCallback -= OnClientConnected;
        _networkManager.OnClientStopped -= OnClientStopped;
        _networkManager.OnTransportFailure -= OnTransportFailure;
    }

    public void Host()
    {
        if (_networkManager.IsListening)
        {
            return;
        }

        HandNameToLobby();

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
        HandNameToLobby();
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
    }

    // The lobby registers this player as soon as the network starts, so the name has to be
    // in place before hosting or joining. An empty name stays empty; the host picks a fallback.
    private void HandNameToLobby()
    {
        string name = LobbyRules.CleanName(_nameField.value, "");
        _nameField.value = name;
        PlayerPrefs.SetString(NamePrefsKey, name);
        if (lobby != null)
        {
            lobby.localPlayerName = name;
        }
    }

    private UnityTransport GetTransport()
    {
        return (UnityTransport)_networkManager.NetworkConfig.NetworkTransport;
    }

    // Shown to the host so a LAN partner knows what to type. A PC often has several
    // addresses (Wi-Fi, Ethernet, VPNs like Tailscale, adapters with no network), so home
    // and school network ranges win, then anything else, and 169.254.x.x only as a last resort.
    private static string FindLocalAddress()
    {
        string best = "127.0.0.1";
        int bestRank = int.MaxValue;
        try
        {
            foreach (IPAddress address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                {
                    continue;
                }

                int rank = RankAddress(address.GetAddressBytes());
                if (rank < bestRank)
                {
                    best = address.ToString();
                    bestRank = rank;
                }
            }
        }
        catch (SocketException)
        {
        }

        return best;
    }

    private static int RankAddress(byte[] bytes)
    {
        bool privateRange = bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
        if (privateRange)
        {
            return 0;
        }

        bool linkLocal = bytes[0] == 169 && bytes[1] == 254;
        return linkLocal ? 2 : 1;
    }
}
