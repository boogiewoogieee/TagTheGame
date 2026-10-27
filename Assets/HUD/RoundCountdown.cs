using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UIElements;

// The 3-2-1-GO at the start of every round: a big digit, the round number, who has the
// bomb, a beep per digit and a higher one for GO. It only reads MatchManager and works
// the step out from the synchronized clock, so both players see and hear the same digit
// at the same moment. Camera punches, particles and the like hook into the events.
[RequireComponent(typeof(UIDocument))]
public class RoundCountdown : MonoBehaviour
{
    [Tooltip("The match this countdown belongs to.")]
    public MatchManager match;

    [Tooltip("The in-game menu. The countdown hides while it is open.")]
    public PauseMenu pauseMenu;

    [Header("Sound")]
    [Tooltip("Mixer group the beeps play through. Leave empty for the master.")]
    public AudioMixerGroup mixerGroup;

    [Tooltip("Played on 3, 2 and 1. Leave empty for a generated beep.")]
    public AudioClip tickClip;

    [Tooltip("Played on GO. Leave empty for a generated higher, longer beep.")]
    public AudioClip goClip;

    [Range(0f, 1f)]
    public float volume = 0.7f;

    [Header("Hooks")]
    [Tooltip("Fired when the round number appears.")]
    public UnityEvent onIntro;

    [Tooltip("Fired on each digit, with the digit (3, 2, 1).")]
    public UnityEvent<int> onTick;

    [Tooltip("Fired on GO, when the players may move.")]
    public UnityEvent onGo;

    // Each digit pops in over the first part of its second and shrinks away at the end of it.
    private const float PopSeconds = 0.25f;
    private const float FadeSeconds = 0.2f;
    private const float PopFromScale = 2.2f;
    private const float FadeToScale = 0.7f;

    // 3 red, 2 orange, 1 yellow: the colour alone says how close GO is.
    private static readonly Color[] DigitColors =
    {
        new Color(1f, 0.35f, 0.25f),
        new Color(1f, 0.6f, 0.2f),
        new Color(1f, 0.9f, 0.3f),
    };

    private static readonly Color GoColor = new Color(0.5f, 0.9f, 0.4f);

    private VisualElement _panel;
    private Label _digit;
    private Label _round;
    private Label _bomb;

    // Ticks alternate between two sources, so a beep scheduled ahead never cuts the one playing.
    private AudioSource[] _tickSources;
    private AudioSource _goSource;
    private int _shownIndex = -1;
    private int _scheduledIndex = -1;
    private double _shownRoundStart;

    private void Awake()
    {
        if (tickClip == null)
        {
            tickClip = Tone("CountdownTick", 880f, 880f, 0.09f);
        }

        if (goClip == null)
        {
            goClip = Tone("CountdownGo", 1175f, 1760f, 0.35f);
        }

        _tickSources = new[] { MakeSource(tickClip), MakeSource(tickClip) };
        _goSource = MakeSource(goClip);
    }

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        _panel = root.Q<VisualElement>("RoundStart");
        _digit = _panel.Q<Label>("RoundStartDigit");
        _round = _panel.Q<Label>("RoundStartRound");
        _bomb = _panel.Q<Label>("RoundStartBomb");
        _panel.style.display = DisplayStyle.None;
        _shownIndex = -1;
        _scheduledIndex = -1;
    }

    private void Update()
    {
        if (_panel == null)
        {
            return;
        }

        bool inRound = match != null && match.IsSpawned
            && (match.Phase.Value == MatchPhase.Countdown || match.Phase.Value == MatchPhase.Playing);
        if (!inRound)
        {
            _panel.style.display = DisplayStyle.None;
            return;
        }

        // A new start time is a new countdown, even if the last one was cut short.
        if (match.RoundStartsAtServerTime.Value != _shownRoundStart)
        {
            _shownRoundStart = match.RoundStartsAtServerTime.Value;
            _shownIndex = -1;
            _scheduledIndex = -1;
        }

        float length = match.rules.countdownSeconds;
        double secondsToStart = match.SecondsToRoundStart;
        CountdownState state = CountdownRules.At(secondsToStart, length);
        if (state.Index != _shownIndex)
        {
            _shownIndex = state.Index;
            OnStep(state, secondsToStart, length);
        }

        bool menuOpen = pauseMenu != null && pauseMenu.IsOpen;
        if (state.Step == CountdownStep.None || menuOpen)
        {
            _panel.style.display = DisplayStyle.None;
            return;
        }

        _panel.style.display = DisplayStyle.Flex;
        Draw(state, length);
    }

    // A step has just begun on this machine's clock: fire its hook, play its sound if it
    // was not already scheduled, and schedule the next sound so it lands on the clock
    // instead of on whichever frame happens to notice it.
    private void OnStep(CountdownState state, double secondsToStart, float length)
    {
        switch (state.Step)
        {
            case CountdownStep.Intro:
                onIntro.Invoke();
                break;

            case CountdownStep.Digit:
                PlayNow(SourceFor(state.Index), state.Index);
                onTick.Invoke(state.Digit);
                break;

            case CountdownStep.Go:
                PlayNow(_goSource, state.Index);
                onGo.Invoke();
                break;
        }

        int next = state.Index + 1;
        if (state.Index >= CountdownRules.IntroIndex && next <= CountdownRules.GoIndex)
        {
            double wait = secondsToStart - CountdownRules.SecondsToStartOf(next, length);
            SourceFor(next).PlayScheduled(AudioSettings.dspTime + Math.Max(0.0, wait));
            _scheduledIndex = next;
        }
    }

    private AudioSource SourceFor(int index)
    {
        return index >= CountdownRules.GoIndex ? _goSource : _tickSources[index % 2];
    }

    private void PlayNow(AudioSource source, int index)
    {
        if (_scheduledIndex != index)
        {
            source.Play();
        }
    }

    private void Draw(CountdownState state, float length)
    {
        _round.text = "ROUND " + match.RoundNumber;
        bool hasBomb = match.LocalPlayerHasBomb;
        _bomb.text = hasBomb ? "YOU HAVE THE BOMB!" : "RUN!";
        _bomb.EnableInClassList("hud-start-bomb--danger", hasBomb);

        float stepSeconds;
        switch (state.Step)
        {
            case CountdownStep.Intro:
                stepSeconds = CountdownRules.IntroSeconds(length);
                _digit.style.display = DisplayStyle.None;
                SetPop(_round, state.Progress * stepSeconds, 0f, 1f);
                SetPop(_bomb, state.Progress * stepSeconds, 0f, 1f);
                return;

            case CountdownStep.Go:
                stepSeconds = CountdownRules.GoSeconds;
                _digit.text = "GO!";
                _digit.style.color = GoColor;
                break;

            default:
                stepSeconds = CountdownRules.DigitSeconds;
                _digit.text = state.Digit.ToString();
                _digit.style.color = DigitColors[Mathf.Clamp(CountdownRules.DigitCount - state.Digit, 0, DigitColors.Length - 1)];
                break;
        }

        _digit.style.display = DisplayStyle.Flex;
        float secondsIn = state.Progress * stepSeconds;
        float secondsLeft = stepSeconds - secondsIn;
        SetPop(_digit, secondsIn, secondsLeft, 1f);

        // The labels under the digit stay put during the digits and leave with GO.
        float labelOpacity = state.Step == CountdownStep.Go ? Mathf.Clamp01(secondsLeft / FadeSeconds) : 1f;
        SetScale(_round, 1f, labelOpacity);
        SetScale(_bomb, 1f, labelOpacity);
    }

    // Pops the element in over the first part of its step and shrinks it away at the end.
    private static void SetPop(VisualElement element, float secondsIn, float secondsLeft, float restScale)
    {
        float scale = secondsIn >= PopSeconds
            ? restScale
            : Mathf.LerpUnclamped(PopFromScale, restScale, EaseOutBack(secondsIn / PopSeconds));
        float opacity = 1f;
        if (secondsLeft > 0f && secondsLeft < FadeSeconds)
        {
            float k = secondsLeft / FadeSeconds;
            scale *= Mathf.Lerp(FadeToScale, 1f, k);
            opacity = k;
        }

        SetScale(element, scale, opacity);
    }

    private static void SetScale(VisualElement element, float scale, float opacity)
    {
        element.style.scale = new Scale(new Vector2(scale, scale));
        element.style.opacity = opacity;
    }

    // Overshoots a little past the target and settles back: the classic cartoon pop.
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float y = x - 1f;
        return 1f + c3 * y * y * y + c1 * y * y;
    }

    private AudioSource MakeSource(AudioClip clip)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.outputAudioMixerGroup = mixerGroup;
        return source;
    }

    // A short sine beep with a fast attack and a falling tail, so no audio asset is needed.
    private static AudioClip Tone(string name, float fromHz, float toHz, float seconds)
    {
        const int rate = 44100;
        int count = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[count];
        double phase = 0.0;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            phase += 2.0 * Math.PI * Mathf.Lerp(fromHz, toHz, t) / rate;
            float attack = Mathf.Min(1f, i / (0.004f * rate));
            float tail = Mathf.Pow(1f - t, 1.5f);
            data[i] = (float)Math.Sin(phase) * attack * tail;
        }

        AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
