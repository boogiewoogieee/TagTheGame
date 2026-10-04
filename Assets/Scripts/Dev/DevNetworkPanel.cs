using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UIElements;

// Development-only panel for testing on one PC: start a host, or join it as a client.
// For now the NetworkManager and its transport sit in Arena_01. They move to a Bootstrap
// scene once the menu and lobby exist, and this panel is deleted then.
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

        _hostButton = new Button(StartHost) { text = "Host" };
        _hostButton.style.fontSize = 24;
        _hostButton.style.marginTop = 8;

        _clientButton = new Button(StartClient) { text = "Client" };
        _clientButton.style.fontSize = 24;
        _clientButton.style.marginTop = 8;

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
        DisplayStyle buttonDisplay = offline ? DisplayStyle.Flex : DisplayStyle.None;
        _hostButton.style.display = buttonDisplay;
        _clientButton.style.display = buttonDisplay;
        _status.text = DescribeState(networkManager);
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
