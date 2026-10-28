using UnityEngine;
using UnityEngine.UIElements;

// Shows the fuse timer, who holds the bomb and the card between rounds (result, score,
// countdown). ResultsPanel takes over at the end of the match. It only reads the
// MatchManager: every machine works the numbers out locally from the synchronized clock,
// so the HUD needs no network traffic of its own.
[RequireComponent(typeof(UIDocument))]
public class MatchHud : MonoBehaviour
{
    [Tooltip("The match this HUD shows.")]
    public MatchManager match;

    [Tooltip("The in-game menu. The round-over screen hides while it is open, so their buttons never overlap.")]
    public PauseMenu pauseMenu;

    private Label _timer;
    private Label _status;
    private Label _result;
    private Label _score;
    private Label _countdown;
    private VisualElement _roundOver;
    private VisualElement _passFlash;
    private int _shownSeconds = -1;

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        // Documents that share a panel are stacked in a column and are only as tall as their
        // content. Stretching this one over the whole screen lets the HUD lay itself out freely
        // and keeps it from pushing other documents down.
        root.StretchToParentSize();
        root.pickingMode = PickingMode.Ignore;

        _timer = root.Q<Label>("Timer");
        _status = root.Q<Label>("Status");
        _result = root.Q<Label>("Result");
        _score = root.Q<Label>("Score");
        _countdown = root.Q<Label>("Countdown");
        _roundOver = root.Q<VisualElement>("RoundOver");
        _passFlash = root.Q<VisualElement>("PassFlash");
        _shownSeconds = -1;
    }

    private void Update()
    {
        if (_timer == null)
        {
            return;
        }

        bool inMatch = match != null && match.IsSpawned;
        MatchPhase phase = inMatch ? match.Phase.Value : MatchPhase.WaitingForPlayers;

        bool afterRound = phase == MatchPhase.RoundOver || phase == MatchPhase.MatchOver;

        // The countdown names the holder itself, so the timer and the status line wait until "GO!" has faded.
        bool goOnScreen = phase == MatchPhase.Playing && match.SecondsToRoundStart > -CountdownRules.GoSeconds;
        SetVisible(_timer, inMatch && phase == MatchPhase.Playing && !goOnScreen);
        SetVisible(_status, inMatch && !afterRound && phase != MatchPhase.Countdown && !goOnScreen);
        bool menuOpen = pauseMenu != null && pauseMenu.IsOpen;
        SetVisible(_roundOver, inMatch && phase == MatchPhase.RoundOver && !menuOpen && match.RoundEndVisible);

        if (!inMatch)
        {
            return;
        }

        if (phase == MatchPhase.Playing)
        {
            // Whole seconds, rounded up, so the display reaches 0 exactly when the bomb explodes.
            int seconds = Mathf.CeilToInt(match.RemainingSeconds);
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _timer.text = seconds.ToString();

                bool danger = match.rules != null && FuseRules.IsDangerSecond(seconds, match.rules.fuseDangerSeconds);
                _timer.EnableInClassList("hud-timer--danger", danger);
                if (danger)
                {
                    // On at once, off a moment later: the style's transition then shrinks it back smoothly.
                    _timer.AddToClassList("hud-timer--pop");
                    _timer.schedule.Execute(() => _timer.RemoveFromClassList("hud-timer--pop")).StartingIn(60);
                }
            }

            bool hasBomb = match.LocalPlayerHasBomb;
            _status.text = hasBomb ? "You have the bomb!" : "Opponent has the bomb";
            _status.EnableInClassList("hud-status-danger", hasBomb);
        }
        else if (phase == MatchPhase.WaitingForPlayers)
        {
            _status.text = "Waiting for the other player";
            _status.EnableInClassList("hud-status-danger", false);
        }
        else if (phase == MatchPhase.RoundOver)
        {
            // The loser of the round is recorded by the host, so this is right even after a pass.
            bool lost = match.LastRoundLoserId.Value == match.NetworkManager.LocalClientId;
            _result.text = lost ? "Round lost" : "Round won";
            _result.EnableInClassList("hud-result-lose", lost);
            _result.EnableInClassList("hud-result-win", !lost);
            _score.text = match.LocalRoundsWon + " : " + match.OpponentRoundsWon;
            _countdown.text = "Next round in " + Mathf.CeilToInt(match.SecondsToNextRound);
        }
    }

    // Called when the local player has just been tagged: a warm flash over the screen and a pop of the status line.
    public void ShowBombReceived()
    {
        Pop(_passFlash, "hud-pass-flash--on", 40);
        Pop(_status, "hud-status--pop", 80);
    }

    // On at once, off a moment later: the style's transition then eases it back.
    private static void Pop(VisualElement element, string className, long milliseconds)
    {
        if (element == null)
        {
            return;
        }

        element.AddToClassList(className);
        element.schedule.Execute(() => element.RemoveFromClassList(className)).StartingIn(milliseconds);
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
