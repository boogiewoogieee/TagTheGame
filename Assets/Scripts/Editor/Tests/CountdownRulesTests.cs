using NUnit.Framework;

// Checks the countdown timing by calling it directly, with no scene, network or Play Mode.
public class CountdownRulesTests
{
    private const float Countdown = 3.6f;

    [Test]
    public void At_BeforeTheCountdown_IsNone()
    {
        Assert.AreEqual(CountdownStep.None, CountdownRules.At(4.0, Countdown).Step);
    }

    [Test]
    public void At_RightAtTheStart_IsTheIntro()
    {
        // Just inside the start: the exact boundary depends on float rounding and never happens on a real clock.
        CountdownState state = CountdownRules.At(3.59, Countdown);
        Assert.AreEqual(CountdownStep.Intro, state.Step);
        Assert.AreEqual(CountdownRules.IntroIndex, state.Index);
        Assert.AreEqual(0f, state.Progress, 0.05f);
    }

    [Test]
    public void At_EndOfTheIntro_HasFullProgress()
    {
        Assert.AreEqual(1f, CountdownRules.At(3.0001, Countdown).Progress, 0.01f);
    }

    [Test]
    public void At_ThreeSecondsLeft_ShowsThree()
    {
        CountdownState state = CountdownRules.At(3.0, Countdown);
        Assert.AreEqual(CountdownStep.Digit, state.Step);
        Assert.AreEqual(3, state.Digit);
        Assert.AreEqual(1, state.Index);
        Assert.AreEqual(0f, state.Progress, 0.001f);
    }

    [Test]
    public void At_HalfWayThroughTwo_HasHalfProgress()
    {
        CountdownState state = CountdownRules.At(1.5, Countdown);
        Assert.AreEqual(2, state.Digit);
        Assert.AreEqual(2, state.Index);
        Assert.AreEqual(0.5f, state.Progress, 0.001f);
    }

    [Test]
    public void At_JustBeforeGo_ShowsOne()
    {
        CountdownState state = CountdownRules.At(0.01, Countdown);
        Assert.AreEqual(1, state.Digit);
        Assert.AreEqual(3, state.Index);
    }

    [Test]
    public void At_Zero_IsGo()
    {
        CountdownState state = CountdownRules.At(0.0, Countdown);
        Assert.AreEqual(CountdownStep.Go, state.Step);
        Assert.AreEqual(CountdownRules.GoIndex, state.Index);
        Assert.AreEqual(0, state.Digit);
    }

    [Test]
    public void At_AfterGoHasFaded_IsDone()
    {
        CountdownState state = CountdownRules.At(-0.7, Countdown);
        Assert.AreEqual(CountdownStep.None, state.Step);
        Assert.AreEqual(CountdownRules.DoneIndex, state.Index);
    }

    [Test]
    public void At_ProgressNeverLeavesZeroToOne()
    {
        for (double seconds = 4.0; seconds > -1.0; seconds -= 0.05)
        {
            CountdownState state = CountdownRules.At(seconds, Countdown);
            Assert.GreaterOrEqual(state.Progress, 0f);
            Assert.LessOrEqual(state.Progress, 1f);
        }
    }

    [Test]
    public void At_IndexOnlyEverCountsUp()
    {
        int last = -1;
        for (double seconds = 4.0; seconds > -1.0; seconds -= 0.01)
        {
            int index = CountdownRules.At(seconds, Countdown).Index;
            Assert.GreaterOrEqual(index, last);
            last = index;
        }
    }

    [Test]
    public void At_CountdownWithoutIntro_StartsWithThree()
    {
        CountdownState state = CountdownRules.At(3.0, 3f);
        Assert.AreEqual(CountdownStep.Digit, state.Step);
        Assert.AreEqual(3, state.Digit);
        Assert.AreEqual(0f, CountdownRules.IntroSeconds(3f));
    }

    [Test]
    public void SecondsToStartOf_MatchesTheSteps()
    {
        Assert.AreEqual(3.6f, CountdownRules.SecondsToStartOf(CountdownRules.IntroIndex, Countdown), 0.001f);
        Assert.AreEqual(3f, CountdownRules.SecondsToStartOf(1, Countdown), 0.001f);
        Assert.AreEqual(2f, CountdownRules.SecondsToStartOf(2, Countdown), 0.001f);
        Assert.AreEqual(1f, CountdownRules.SecondsToStartOf(3, Countdown), 0.001f);
        Assert.AreEqual(0f, CountdownRules.SecondsToStartOf(CountdownRules.GoIndex, Countdown), 0.001f);
    }

    [Test]
    public void SecondsToStartOf_AgreesWithAt()
    {
        for (int index = CountdownRules.IntroIndex; index <= CountdownRules.GoIndex; index++)
        {
            double start = CountdownRules.SecondsToStartOf(index, Countdown);
            Assert.AreEqual(index, CountdownRules.At(start, Countdown).Index, "index " + index);
        }
    }

    [Test]
    public void DigitOf_MapsIndexesToDigits()
    {
        Assert.AreEqual(0, CountdownRules.DigitOf(CountdownRules.IntroIndex));
        Assert.AreEqual(3, CountdownRules.DigitOf(1));
        Assert.AreEqual(2, CountdownRules.DigitOf(2));
        Assert.AreEqual(1, CountdownRules.DigitOf(3));
        Assert.AreEqual(0, CountdownRules.DigitOf(CountdownRules.GoIndex));
    }
}
