using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

// The results screen at the end of a match: winner, score, a line of stats per player
// and what to do next. It only reads MatchManager and passes the button presses back,
// like MatchHud and LobbyPanel. The host decides for both; the client sees why it waits.
[RequireComponent(typeof(UIDocument))]
public class ResultsPanel : MonoBehaviour
{
    [Tooltip("The match whose results this shows.")]
    public MatchManager match;

    [Tooltip("The in-game menu. The results hide while it is open, so their buttons never overlap.")]
    public PauseMenu pauseMenu;

    [Tooltip("Fired when the player presses Leave.")]
    public UnityEvent onLeave;

    private const int RowCount = 2;

    private VisualElement _screen;
    private Label _title;
    private Label _score;
    private Label _hint;
    private Button _playAgain;
    private Button _nextLevel;
    private Button _returnToLobby;
    private Button _leave;
    private readonly Label[] _names = new Label[RowCount];
    private readonly Label[] _rounds = new Label[RowCount];
    private readonly Label[] _holdTimes = new Label[RowCount];
    private readonly Label[] _passes = new Label[RowCount];

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        _screen = root.Q<VisualElement>("Results");
        _title = _screen.Q<Label>("ResultsTitle");
        _score = _screen.Q<Label>("ResultsScore");
        _hint = _screen.Q<Label>("ResultsHint");
        _playAgain = _screen.Q<Button>("PlayAgain");
        _nextLevel = _screen.Q<Button>("NextLevel");
        _returnToLobby = _screen.Q<Button>("ReturnToLobby");
        _leave = _screen.Q<Button>("ResultsLeave");
        for (int i = 0; i < RowCount; i++)
        {
            _names[i] = _screen.Q<Label>("Row" + (i + 1) + "Name");
            _rounds[i] = _screen.Q<Label>("Row" + (i + 1) + "Rounds");
            _holdTimes[i] = _screen.Q<Label>("Row" + (i + 1) + "Hold");
            _passes[i] = _screen.Q<Label>("Row" + (i + 1) + "Passes");
        }

        _playAgain.clicked += OnPlayAgain;
        _nextLevel.clicked += OnNextLevel;
        _returnToLobby.clicked += OnReturnToLobby;
        _leave.clicked += OnLeave;
        _screen.style.display = DisplayStyle.None;
    }

    private void OnDisable()
    {
        if (_screen == null)
        {
            return;
        }

        _playAgain.clicked -= OnPlayAgain;
        _nextLevel.clicked -= OnNextLevel;
        _returnToLobby.clicked -= OnReturnToLobby;
        _leave.clicked -= OnLeave;
    }

    private void Update()
    {
        bool show = match != null && match.IsSpawned && match.Phase.Value == MatchPhase.MatchOver
            && !(pauseMenu != null && pauseMenu.IsOpen);
        _screen.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (!show)
        {
            return;
        }

        ulong localId = match.NetworkManager.LocalClientId;
        bool won = match.WinnerId.Value == localId;
        _title.text = match.OpponentLeft.Value ? "Opponent left" : (won ? "You win" : "You lose");
        _title.EnableInClassList("hud-result-win", won);
        _title.EnableInClassList("hud-result-lose", !won && !match.OpponentLeft.Value);
        _score.text = match.LocalRoundsWon + " : " + match.OpponentRoundsWon;

        for (int i = 0; i < RowCount; i++)
        {
            bool hasRow = i < match.Stats.Count;
            if (hasRow)
            {
                PlayerMatchStats stats = match.Stats[i];
                _names[i].text = stats.ClientId == localId ? stats.Name.ToString() + " (you)" : stats.Name.ToString();
                _rounds[i].text = stats.RoundsWon.ToString();
                _holdTimes[i].text = MatchRules.FormatHoldTime(stats.SecondsHoldingBomb);
                _passes[i].text = stats.Passes.ToString();
            }

            _names[i].parent.style.display = hasRow ? DisplayStyle.Flex : DisplayStyle.None;
        }

        bool isHost = match.IsServer;
        _playAgain.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
        _playAgain.SetEnabled(match.CanPlayAgain);
        _nextLevel.style.display = isHost && match.CanGoToNextLevel ? DisplayStyle.Flex : DisplayStyle.None;
        _returnToLobby.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
        _hint.text = isHost
            ? (match.OpponentLeft.Value ? "Your opponent left. Return to the lobby to wait for a new one." : "")
            : "Waiting for the host to choose...";
    }

    private void OnPlayAgain()
    {
        if (match != null)
        {
            match.PlayAgain();
        }
    }

    private void OnNextLevel()
    {
        if (match != null)
        {
            match.GoToNextLevel();
        }
    }

    private void OnReturnToLobby()
    {
        if (match != null)
        {
            match.ReturnToLobby();
        }
    }

    private void OnLeave()
    {
        onLeave.Invoke();
    }
}
