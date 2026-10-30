using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Draws the Lobby screen from LobbyManager: who is in, who plays which character, who is
// ready, and the character picker, Ready and Start match buttons. It only reads the
// lobby state and passes button presses back, the same split as MatchHud and MatchManager.
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
    private readonly Label[] _slotCharacters = new Label[SlotCount];
    private readonly VisualElement[] _slotPortraits = new VisualElement[SlotCount];
    private readonly Button[] _slotPrev = new Button[SlotCount];
    private readonly Button[] _slotNext = new Button[SlotCount];
    private readonly int[] _shownCharacter = new int[SlotCount];
    private readonly List<Button> _rosterCells = new List<Button>();
    private VisualElement _roster;
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
            _slotCharacters[i] = screen.Q<Label>("Slot" + (i + 1) + "Character");
            _slotPortraits[i] = screen.Q<VisualElement>("Slot" + (i + 1) + "Portrait");
            _slotPrev[i] = screen.Q<Button>("Slot" + (i + 1) + "Prev");
            _slotNext[i] = screen.Q<Button>("Slot" + (i + 1) + "Next");
            _slotPrev[i].clicked += OnPrevClicked;
            _slotNext[i].clicked += OnNextClicked;
            _shownCharacter[i] = int.MinValue;
        }

        _roster = screen.Q<VisualElement>("Roster");
        BuildRoster();

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
        for (int i = 0; i < SlotCount; i++)
        {
            _slotPrev[i].clicked -= OnPrevClicked;
            _slotNext[i].clicked -= OnNextClicked;
        }

        // The cells' click handlers go with them; OnEnable builds new ones.
        _roster.Clear();
        _rosterCells.Clear();
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

        ShowRoster(localId);

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

    // One cell per catalog entry, in roster order; the cell's index is the pick it asks for.
    private void BuildRoster()
    {
        CharacterCatalog catalog = lobby != null ? lobby.characters : null;
        if (_roster == null || catalog == null)
        {
            return;
        }

        for (int i = 0; i < catalog.Count; i++)
        {
            int index = i;
            Button cell = new Button(() => OnRosterClicked(index));
            cell.AddToClassList("lobby-roster-cell");
            CharacterCatalog.Entry entry = catalog.Get(i);
            cell.tooltip = entry.displayName;
            if (entry.portrait != null)
            {
                cell.style.backgroundImage = new StyleBackground(entry.portrait);
            }
            else
            {
                cell.text = (i + 1).ToString("00");
            }

            _roster.Add(cell);
            _rosterCells.Add(cell);
        }
    }

    private void ShowRoster(ulong localId)
    {
        int mine = lobby.LocalCharacterIndex;
        List<int> taken = lobby.CharactersTakenBy(localId);
        bool locked = lobby.LocalIsReady;
        for (int i = 0; i < _rosterCells.Count; i++)
        {
            bool isTaken = taken.Contains(i);
            _rosterCells[i].EnableInClassList("lobby-roster-cell--mine", i == mine);
            _rosterCells[i].EnableInClassList("lobby-roster-cell--taken", isTaken);
            _rosterCells[i].SetEnabled(!isTaken && !locked);
        }
    }

    private void ShowCharacter(int slot, int index)
    {
        if (_shownCharacter[slot] == index)
        {
            return;
        }

        _shownCharacter[slot] = index;
        CharacterCatalog.Entry entry = lobby.characters != null ? lobby.characters.Get(index) : null;
        _slotCharacters[slot].text = entry != null ? entry.displayName : "";
        _slotPortraits[slot].style.backgroundImage = entry != null && entry.portrait != null
            ? new StyleBackground(entry.portrait)
            : new StyleBackground(StyleKeyword.None);
    }

    private void ShowPlayer(int slot, LobbyPlayer player, bool isLocal)
    {
        _slots[slot].EnableInClassList("lobby-slot--empty", false);
        ShowCharacter(slot, player.CharacterIndex);

        // Only your own card has arrows, and Ready locks them. Hidden rather than removed,
        // so both cards keep the same layout.
        Visibility arrows = isLocal ? Visibility.Visible : Visibility.Hidden;
        _slotPrev[slot].style.visibility = arrows;
        _slotNext[slot].style.visibility = arrows;
        _slotPrev[slot].SetEnabled(!player.IsReady);
        _slotNext[slot].SetEnabled(!player.IsReady);

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
        ShowCharacter(slot, CharacterRules.None);

        _slotPrev[slot].style.visibility = Visibility.Hidden;
        _slotNext[slot].style.visibility = Visibility.Hidden;
    }

    private void OnPrevClicked()
    {
        StepCharacter(-1);
    }

    private void OnNextClicked()
    {
        StepCharacter(1);
    }

    private void StepCharacter(int direction)
    {
        if (lobby == null || !lobby.IsSpawned)
        {
            return;
        }

        List<int> taken = lobby.CharactersTakenBy(lobby.NetworkManager.LocalClientId);
        lobby.ChangeCharacter(CharacterRules.Step(lobby.LocalCharacterIndex, direction, lobby.CharacterCount, taken));
    }

    private void OnRosterClicked(int index)
    {
        if (lobby != null)
        {
            lobby.ChangeCharacter(index);
        }
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
