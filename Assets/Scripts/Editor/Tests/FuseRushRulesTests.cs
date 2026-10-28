using NUnit.Framework;

// Checks the Fuse Rush timing by calling it directly, with no scene, network or Play Mode.
public class FuseRushRulesTests
{
    private const ulong Holder = 1;
    private const ulong Other = 2;

    private static bool CanStart(FuseRushState state, double now, double tolerance = 0.0)
    {
        return FuseRushRules.CanStart(MatchPhase.Playing, Holder, Holder, now, state, tolerance);
    }

    [Test]
    public void NewRound_IsReadyAtGo()
    {
        FuseRushState state = FuseRushRules.NewRound(Holder, 10.0);
        Assert.IsFalse(CanStart(state, 9.9));
        Assert.IsTrue(CanStart(state, 10.0));
        Assert.AreEqual(0f, FuseRushRules.CooldownLeft(state, 10.0));
    }

    [Test]
    public void AfterPass_WaitsTheAfterPassSeconds()
    {
        FuseRushState state = FuseRushRules.AfterPass(Holder, 20.0, 3f);
        Assert.IsFalse(CanStart(state, 22.9));
        Assert.IsTrue(CanStart(state, 23.0));
        Assert.IsFalse(FuseRushRules.IsActive(state, Holder, 20.0));
    }

    [Test]
    public void Started_RunsForTheRushSeconds()
    {
        FuseRushState state = FuseRushRules.Started(Holder, 5.0, 2f, 8f);
        Assert.IsTrue(FuseRushRules.IsActive(state, Holder, 5.0));
        Assert.IsTrue(FuseRushRules.IsActive(state, Holder, 6.99));
        Assert.IsFalse(FuseRushRules.IsActive(state, Holder, 7.0));
    }

    [Test]
    public void IsActive_OnlyForTheRushingPlayer()
    {
        FuseRushState state = FuseRushRules.Started(Holder, 5.0, 2f, 8f);
        Assert.IsFalse(FuseRushRules.IsActive(state, Other, 5.5));
    }

    [Test]
    public void Started_CooldownCountsFromThePress()
    {
        FuseRushState state = FuseRushRules.Started(Holder, 5.0, 2f, 8f);
        Assert.IsFalse(CanStart(state, 12.9));
        Assert.IsTrue(CanStart(state, 13.0));
    }

    [Test]
    public void CanStart_OnlyForTheHolder()
    {
        FuseRushState state = FuseRushRules.NewRound(Holder, 0.0);
        Assert.IsFalse(FuseRushRules.CanStart(MatchPhase.Playing, Holder, Other, 100.0, state, 0.0));
    }

    [Test]
    public void CanStart_NotWithAnEarlierHoldersState()
    {
        // The bomb was just passed to Holder, but the host has not written the new state yet.
        FuseRushState state = FuseRushRules.NewRound(Other, 0.0);
        Assert.IsFalse(CanStart(state, 100.0));
    }

    [Test]
    public void CanStart_OnlyWhilePlaying()
    {
        FuseRushState state = FuseRushRules.NewRound(Holder, 0.0);
        Assert.IsFalse(FuseRushRules.CanStart(MatchPhase.Countdown, Holder, Holder, 100.0, state, 0.0));
        Assert.IsFalse(FuseRushRules.CanStart(MatchPhase.RoundOver, Holder, Holder, 100.0, state, 0.0));
        Assert.IsFalse(FuseRushRules.CanStart(MatchPhase.WaitingForPlayers, Holder, Holder, 100.0, state, 0.0));
    }

    [Test]
    public void CanStart_ToleranceAcceptsASlightlyEarlyPress()
    {
        FuseRushState state = FuseRushRules.AfterPass(Holder, 10.0, 3f);
        Assert.IsTrue(CanStart(state, 12.95, 0.1));
        Assert.IsFalse(CanStart(state, 12.85, 0.1));
    }

    [Test]
    public void CooldownLeft_FallsFromOneToZero()
    {
        FuseRushState state = FuseRushRules.Started(Holder, 0.0, 2f, 8f);
        Assert.AreEqual(1f, FuseRushRules.CooldownLeft(state, 0.0), 0.0001f);
        Assert.AreEqual(0.5f, FuseRushRules.CooldownLeft(state, 4.0), 0.0001f);
        Assert.AreEqual(0f, FuseRushRules.CooldownLeft(state, 8.0));
        Assert.AreEqual(0f, FuseRushRules.CooldownLeft(state, 9.0));
    }

    [Test]
    public void ActiveLeft_FallsFromOneToZero()
    {
        FuseRushState state = FuseRushRules.Started(Holder, 0.0, 2f, 8f);
        Assert.AreEqual(1f, FuseRushRules.ActiveLeft(state, 0.0, 2f), 0.0001f);
        Assert.AreEqual(0.25f, FuseRushRules.ActiveLeft(state, 1.5, 2f), 0.0001f);
        Assert.AreEqual(0f, FuseRushRules.ActiveLeft(state, 2.0, 2f));
    }

    [Test]
    public void ActiveLeft_WithNoRushTime_IsZero()
    {
        FuseRushState state = FuseRushRules.Started(Holder, 0.0, 2f, 8f);
        Assert.AreEqual(0f, FuseRushRules.ActiveLeft(state, 0.5, 0f));
    }
}
