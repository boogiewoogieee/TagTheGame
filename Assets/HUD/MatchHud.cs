using UnityEngine;
using UnityEngine.UIElements;

// Shows the fuse timer, who holds the bomb and the round result. It only reads the
// MatchManager: every machine works the numbers out locally from the synchronized clock,
// so the HUD needs no network traffic of its own.
[RequireComponent(typeof(UIDocument))]
public class MatchHud : MonoBehaviour
{
    [Tooltip("The match this HUD shows.")]
    public MatchManager match;

    private Label _timer;
    private Label _status;
    private Label _result;
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

        SetVisible(_timer, inMatch && phase == MatchPhase.Playing);
        SetVisible(_status, inMatch && phase != MatchPhase.RoundOver);
        SetVisible(_result, inMatch && phase == MatchPhase.RoundOver);

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
        else
        {
            // The holder stays set after the explosion, so it tells who lost.
            bool lost = match.LocalPlayerHasBomb;
            _result.text = lost ? "You lose" : "You win";
            _result.EnableInClassList("hud-result-lose", lost);
            _result.EnableInClassList("hud-result-win", !lost);
        }
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
