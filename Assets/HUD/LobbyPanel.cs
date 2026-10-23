using UnityEngine;
using UnityEngine.UIElements;

// Draws the Lobby screen from LobbyManager: who is in, who is ready, and the Ready and
// Start match buttons. It only reads the lobby state and passes button presses back,
// the same split as MatchHud and MatchManager.
[RequireComponent(typeof(UIDocument))]
public class LobbyPanel : MonoBehaviour
{
    [Tooltip("The lobby state this screen shows.")]
    public LobbyManager lobby;

    [Tooltip("Gives the host's own address and port, shown while it waits so a partner on the same network knows what to type.")]
    public MenuNetwork network;

    private const int SlotCount = 2;

    private readonly VisualElement[] _slots = new VisualElement[SlotCount];
    private readonly Label[] _slotNames = new Label[SlotCount];
    private readonly Label[] _slotStates = new Label[SlotCount];
    private Label _status;
    private Label _title;
    private SessionService _sessions;
    private Button _ready;
    private Button _start;

    private void OnEnable()
    {
        VisualElement screen = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("Lobby");
        for (int i = 0; i < SlotCount; i++)
        {
            _slots[i] = screen.Q<VisualElement>("Slot" + (i + 1));
            _slotNames[i] = screen.Q<Label>("Slot" + (i + 1) + "Name");
            _slotStates[i] = screen.Q<Label>("Slot" + (i + 1) + "State");
        }

        _status = screen.Q<Label>("LobbyStatus");
        _title = screen.Q<Label>("Lobby");
        _ready = screen.Q<Button>("Ready");
        _start = screen.Q<Button>("StartMatch");
        _ready.clicked += OnReadyClicked;
        _start.clicked += OnStartClicked;
    }

    private void OnDisable()
    {
        if (_ready == null)
        {
            return;
        }

        _ready.clicked -= OnReadyClicked;
        _start.clicked -= OnStartClicked;
    }

    // Rebuilt every frame while the lobby runs; two rows of text cost nothing, and it
    // needs no change callbacks from the network list.
    private void Update()
    {
        if (lobby == null || !lobby.IsSpawned)
        {
            return;
        }

        // An online game shows its listed name; a direct LAN game keeps the plain title.
        if (_sessions == null)
        {
            _sessions = lobby.NetworkManager.GetComponent<SessionService>();
        }

        bool online = _sessions != null && _sessions.InOnlineGame;
        _title.text = online ? _sessions.CurrentGameName : "LOBBY";

        ulong localId = lobby.NetworkManager.LocalClientId;
        for (int i = 0; i < SlotCount; i++)
        {
            if (i < lobby.Players.Count)
            {
                LobbyPlayer player = lobby.Players[i];
                ShowPlayer(i, player, player.ClientId == localId);
            }
            else
            {
                ShowEmpty(i);
            }
        }

        bool isHost = lobby.IsServer;
        _ready.text = lobby.LocalIsReady ? "Not ready" : "Ready";
        _start.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
        _start.SetEnabled(lobby.CanStart);
        _status.text = StatusText(isHost, online);
    }

    private string StatusText(bool isHost, bool online)
    {
        if (lobby.Players.Count < lobby.requiredPlayers)
        {
            if (isHost && online)
            {
                return "Waiting for a player to join...\nYour game is in the list of open games.";
            }

            if (isHost && network != null)
            {
                return "Waiting for a player to join...\nYour address: " + network.LocalAddress + ", port " + network.port;
            }

            return "Waiting for the other player...";
        }

        if (lobby.ReadyCount < lobby.Players.Count)
        {
            return lobby.LocalIsReady ? "Waiting for the other player to be ready." : "Press Ready when you are ready to play.";
        }

        return isHost ? "Everyone is ready. Start the match!" : "Everyone is ready. Waiting for the host to start.";
    }

    private void ShowPlayer(int slot, LobbyPlayer player, bool isLocal)
    {
        _slots[slot].EnableInClassList("lobby-slot--empty", false);
        _slotNames[slot].text = isLocal ? player.Name.ToString() + " (you)" : player.Name.ToString();
        _slotStates[slot].text = player.IsReady ? "Ready" : "Not ready";
        _slotStates[slot].EnableInClassList("lobby-state--ready", player.IsReady);
        _slotStates[slot].EnableInClassList("lobby-state--not-ready", !player.IsReady);
    }

    private void ShowEmpty(int slot)
    {
        _slots[slot].EnableInClassList("lobby-slot--empty", true);
        _slotNames[slot].text = "Waiting for a player...";
        _slotStates[slot].text = "";
    }

    private void OnReadyClicked()
    {
        if (lobby != null)
        {
            lobby.ToggleReady();
        }
    }

    private void OnStartClicked()
    {
        if (lobby != null)
        {
            lobby.StartMatch();
        }
    }
}
