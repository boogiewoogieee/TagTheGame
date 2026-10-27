using System;

public enum CountdownStep
{
    None,
    Intro,
    Digit,
    Go
}

// What the round-start countdown shows at a given moment, as a value: which step, which
// digit and how far into the step. Every machine works it out from the synchronized
// clock, so the digits land together and nothing is sent per frame.
public struct CountdownState
{
    public CountdownStep Step;

    // 3, 2 or 1 while Step is Digit; 0 otherwise.
    public int Digit;

    // 0 at the start of the step, 1 at its end.
    public float Progress;

    // Counts up through the steps: 0 intro, 1 for "3", 2 for "2", 3 for "1", 4 GO, 5 done.
    // Lets the HUD notice a step change in one comparison.
    public int Index;
}

// The timing of the countdown, kept free of Unity so it can be tested directly.
public static class CountdownRules
{
    public const int DigitCount = 3;
    public const float DigitSeconds = 1f;

    // How long "GO!" stays on screen once the round is already running.
    public const float GoSeconds = 0.6f;

    public const int IntroIndex = 0;
    public const int GoIndex = DigitCount + 1;
    public const int DoneIndex = DigitCount + 2;

    // The part of the countdown before the digits, where the round number is shown.
    public static float IntroSeconds(float countdownSeconds)
    {
        return Math.Max(0f, countdownSeconds - DigitCount * DigitSeconds);
    }

    // secondsToStart is positive before GO and negative after it.
    public static CountdownState At(double secondsToStart, float countdownSeconds)
    {
        CountdownState state = new CountdownState();
        float intro = IntroSeconds(countdownSeconds);
        float digitsStart = DigitCount * DigitSeconds;

        if (secondsToStart > digitsStart + intro)
        {
            state.Step = CountdownStep.None;
            state.Index = -1;
        }
        else if (secondsToStart > digitsStart)
        {
            state.Step = CountdownStep.Intro;
            state.Index = IntroIndex;
            state.Progress = intro > 0f ? (float)((digitsStart + intro - secondsToStart) / intro) : 1f;
        }
        else if (secondsToStart > 0)
        {
            state.Step = CountdownStep.Digit;
            state.Digit = (int)Math.Ceiling(secondsToStart / DigitSeconds);
            state.Index = DigitCount - state.Digit + 1;
            state.Progress = (float)(1 - (secondsToStart - (state.Digit - 1) * DigitSeconds) / DigitSeconds);
        }
        else if (secondsToStart > -GoSeconds)
        {
            state.Step = CountdownStep.Go;
            state.Index = GoIndex;
            state.Progress = (float)(-secondsToStart / GoSeconds);
        }
        else
        {
            state.Step = CountdownStep.None;
            state.Index = DoneIndex;
        }

        state.Progress = Math.Min(1f, Math.Max(0f, state.Progress));
        return state;
    }

    // Seconds before GO at which the step with this index begins; used to schedule its sound ahead of time.
    public static float SecondsToStartOf(int index, float countdownSeconds)
    {
        if (index <= IntroIndex)
        {
            return DigitCount * DigitSeconds + IntroSeconds(countdownSeconds);
        }

        if (index >= GoIndex)
        {
            return 0f;
        }

        return (DigitCount - index + 1) * DigitSeconds;
    }

    // The digit a step index stands for, or 0 for the intro and GO.
    public static int DigitOf(int index)
    {
        return index >= 1 && index <= DigitCount ? DigitCount - index + 1 : 0;
    }
}
