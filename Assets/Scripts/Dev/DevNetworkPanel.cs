using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UIElements;

// Development-only panel for starting the arena on its own in the Editor: start a host
// or join one as a client without going through the menu. The NetworkManager comes from
// the NetworkManagerSpawner in the scene.
[RequireComponent(typeof(UIDocument))]
public class DevNetworkPanel : MonoBehaviour
{
    [Tooltip("Address the Client button connects to.")]
    public string address = "127.0.0.1";

    [Tooltip("Port used by both Host and Client.")]
    public ushort port = 7777;

    private VisualElement _panel;
    private Label _status;
    private Button _hostButton;
    private Button _clientButton;

    private void OnEnable()
    {
        // True in the Editor and in development builds, so release builds never show the panel.
        if (!Debug.isDebugBuild)
        {
            return;
        }

        _panel = new VisualElement();
        _panel.style.position = Position.Absolute;
        _panel.style.left = 20;
        _panel.style.top = 20;
        _panel.style.paddingLeft = 16;
        _panel.style.paddingRight = 16;
        _panel.style.paddingTop = 12;
        _panel.style.paddingBottom = 12;
        _panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);

        _status = new Label();
        _status.style.color = Color.white;
        _status.style.fontSize = 24;
        _status.style.maxWidth = 700;
        _status.style.whiteSpace = WhiteSpace.Normal;

        _hostButton = CreateButton("Host", StartHost);
        _clientButton = CreateButton("Client", StartClient);

        _panel.Add(_status);
        _panel.Add(_hostButton);
        _panel.Add(_clientButton);
        GetComponent<UIDocument>().rootVisualElement.Add(_panel);
    }

    private void OnDisable()
    {
        if (_panel != null)
        {
            _panel.RemoveFromHierarchy();
            _panel = null;
        }
    }

    private void Update()
    {
        if (_panel == null)
        {
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        bool offline = networkManager == null || !networkManager.IsListening;
        DisplayStyle connectDisplay = offline ? DisplayStyle.Flex : DisplayStyle.None;
        _hostButton.style.display = connectDisplay;
        _clientButton.style.display = connectDisplay;
        _status.text = DescribeState(networkManager);
    }

    private static Button CreateButton(string text, System.Action onClick)
    {
        Button button = new Button(onClick) { text = text };
        button.style.fontSize = 24;
        button.style.marginTop = 8;
        button.style.paddingLeft = 12;
        button.style.paddingRight = 12;
        button.style.paddingTop = 4;
        button.style.paddingBottom = 4;
        button.style.color = Color.white;
        button.style.backgroundColor = new Color(0.25f, 0.4f, 0.25f, 1f);

        // A focused button would also react to Space and Enter, which are gameplay keys.
        button.focusable = false;
        return button;
    }

    private string DescribeState(NetworkManager networkManager)
    {
        if (networkManager == null)
        {
            return "No NetworkManager in the scene";
        }

        if (!networkManager.IsListening)
        {
            string reason = networkManager.DisconnectReason;
            return string.IsNullOrEmpty(reason) ? "Not connected" : "Not connected: " + reason;
        }

        if (networkManager.IsHost)
        {
            return "Hosting on port " + port + ": " + networkManager.ConnectedClientsIds.Count + " of 2 players";
        }

        return networkManager.IsConnectedClient ? "Connected to " + address : "Connecting to " + address + "...";
    }

    private void StartHost()
    {
        NetworkManager networkManager = PrepareNetworkManager();
        if (networkManager != null)
        {
            networkManager.StartHost();
        }
    }

    private void StartClient()
    {
        NetworkManager networkManager = PrepareNetworkManager();
        if (networkManager != null)
        {
            networkManager.StartClient();
        }
    }

    private NetworkManager PrepareNetworkManager()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            return null;
        }

        UnityTransport transport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport != null)
        {
            transport.SetConnectionData(address, port);
        }

        return networkManager;
    }
}
