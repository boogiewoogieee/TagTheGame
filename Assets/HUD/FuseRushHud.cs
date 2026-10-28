using UnityEngine;
using UnityEngine.UIElements;

// The holder's Fuse Rush on screen: a round skill icon with the key and a cooldown ring,
// and speed lines streaking in from the screen edges while the rush runs. Only the
// player holding the bomb sees it; it reads everything from FuseRush.
[RequireComponent(typeof(UIDocument))]
public class FuseRushHud : MonoBehaviour
{
    [Tooltip("The skill this HUD shows.")]
    public FuseRush rush;

    [Tooltip("How many speed lines are on screen at once during a rush.")]
    [Min(0)]
    public int lineCount = 28;

    [Tooltip("Seconds between new random speed lines; smaller looks faster.")]
    [Min(0.01f)]
    public float lineRefreshSeconds = 0.045f;

    private static readonly Color RingBack = new Color(0.1f, 0.1f, 0.12f, 0.75f);
    private static readonly Color RingCooldown = new Color(0.96f, 0.6f, 0.14f, 1f);
    private static readonly Color RingActive = new Color(1f, 0.85f, 0.3f, 1f);
    private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.85f);

    private VisualElement _skill;
    private VisualElement _ring;
    private Label _key;
    private VisualElement _lines;
    private float _cooldownLeft;
    private float _rushLeft;
    private float _nextLines;
    private int _lineSeed;
    private bool _wasReady;

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        _skill = root.Q<VisualElement>("RushSkill");
        _ring = root.Q<VisualElement>("RushRing");
        _key = root.Q<Label>("RushKey");
        _lines = root.Q<VisualElement>("RushLines");
        if (_ring != null)
        {
            _ring.generateVisualContent += DrawRing;
        }

        if (_lines != null)
        {
            _lines.generateVisualContent += DrawLines;
        }
    }

    private void OnDisable()
    {
        if (_ring != null)
        {
            _ring.generateVisualContent -= DrawRing;
        }

        if (_lines != null)
        {
            _lines.generateVisualContent -= DrawLines;
        }
    }

    private void Update()
    {
        if (_skill == null || _ring == null || _key == null || _lines == null)
        {
            return;
        }

        bool holder = rush != null && rush.LocalIsHolder;
        _skill.style.display = holder ? DisplayStyle.Flex : DisplayStyle.None;
        if (!holder)
        {
            _lines.style.opacity = 0f;
            _rushLeft = 0f;
            _wasReady = false;
            return;
        }

        _cooldownLeft = rush.LocalCooldownLeft;
        _rushLeft = rush.LocalRushLeft;
        bool ready = _cooldownLeft <= 0f;
        _key.text = ready || _rushLeft > 0f ? rush.KeyLabel : Mathf.CeilToInt(rush.LocalSecondsToReady).ToString();
        _skill.EnableInClassList("hud-rush--ready", ready);
        _skill.EnableInClassList("hud-rush--active", _rushLeft > 0f);

        // A short pop when the rush becomes ready again, so it is noticed without looking at it.
        if (ready && !_wasReady)
        {
            _skill.AddToClassList("hud-rush--pop");
            _skill.schedule.Execute(() => _skill.RemoveFromClassList("hud-rush--pop")).StartingIn(80);
        }

        _wasReady = ready;
        _ring.MarkDirtyRepaint();

        // Lines snap in at the start and thin out over the last part of the rush.
        _lines.style.opacity = Mathf.Clamp01(_rushLeft * 3f);
        if (_rushLeft > 0f && Time.unscaledTime >= _nextLines)
        {
            _nextLines = Time.unscaledTime + lineRefreshSeconds;
            _lineSeed++;
            _lines.MarkDirtyRepaint();
        }
    }

    private void DrawRing(MeshGenerationContext context)
    {
        Rect rect = _ring.contentRect;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f - 5f;
        if (radius <= 0f)
        {
            return;
        }

        Vector2 centre = rect.center;
        Painter2D painter = context.painter2D;
        painter.lineWidth = 8f;
        painter.lineCap = LineCap.Butt;

        painter.strokeColor = RingBack;
        painter.BeginPath();
        painter.Arc(centre, radius, Angle.Degrees(0f), Angle.Degrees(360f));
        painter.Stroke();

        // During the rush the ring shows the rush running out; otherwise it fills up as the cooldown ends.
        float filled = _rushLeft > 0f ? _rushLeft : 1f - _cooldownLeft;
        if (filled <= 0.001f)
        {
            return;
        }

        painter.strokeColor = _rushLeft > 0f || _cooldownLeft <= 0f ? RingActive : RingCooldown;
        painter.lineCap = LineCap.Round;
        painter.BeginPath();
        painter.Arc(centre, radius, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * filled));
        painter.Stroke();
    }

    // Thin streaks pointing at the middle of the screen, starting at random points near the edges.
    private void DrawLines(MeshGenerationContext context)
    {
        if (_rushLeft <= 0f)
        {
            return;
        }

        Rect rect = _lines.contentRect;
        Vector2 centre = rect.center;
        float reach = rect.size.magnitude * 0.5f;
        var random = new System.Random(_lineSeed);
        Painter2D painter = context.painter2D;
        painter.lineCap = LineCap.Round;
        painter.strokeColor = LineColor;
        for (int i = 0; i < lineCount; i++)
        {
            float angle = (float)(random.NextDouble() * Mathf.PI * 2.0);
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float inner = Mathf.Lerp(0.55f, 0.8f, (float)random.NextDouble()) * reach;
            painter.lineWidth = Mathf.Lerp(1.5f, 4.5f, (float)random.NextDouble());
            painter.BeginPath();
            painter.MoveTo(centre + direction * inner);
            painter.LineTo(centre + direction * reach * 1.05f);
            painter.Stroke();
        }
    }
}
