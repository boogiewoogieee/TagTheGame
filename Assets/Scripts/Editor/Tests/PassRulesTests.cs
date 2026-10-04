using NUnit.Framework;

// Checks the rules for passing the bomb by calling them directly, with no scene, network
// or Play Mode. Run them from Window > General > Test Runner > EditMode.
// The file sits in an Editor folder because that is the one place where tests can see
// the game code without assembly definitions.
public class PassRulesTests
{
    private const ulong Holder = 0;
    private const ulong Other = 1;
    private const float Cooldown = 1f;
    private const float Range = 1.5f;
    private const float Tolerance = 1f;

    // A pass that is allowed; each test changes one thing about it.
    private static PassResult Check(
        MatchPhase phase = MatchPhase.Playing,
        ulong holderId = Holder,
        ulong senderId = Holder,
        ulong targetId = Other,
        double secondsHeld = 2.0,
        float distance = 1f,
        float allowedDistance = Range)
    {
        return PassRules.Check(phase, holderId, senderId, targetId, secondsHeld, Cooldown, distance, allowedDistance);
    }

    [Test]
    public void HolderInRangeAfterCooldown_IsAllowed()
    {
        Assert.AreEqual(PassResult.Allowed, Check());
    }

    [Test]
    public void ExactlyAtRange_IsAllowed()
    {
        Assert.AreEqual(PassResult.Allowed, Check(distance: Range));
    }

    [Test]
    public void JustBeyondRange_IsTooFar()
    {
        Assert.AreEqual(PassResult.TooFar, Check(distance: Range + 0.01f));
    }

    [Test]
    public void HostTolerance_AcceptsALittleBeyondRange()
    {
        Assert.AreEqual(PassResult.Allowed, Check(distance: Range + 0.9f, allowedDistance: Range + Tolerance));
    }

    [Test]
    public void BeyondHostTolerance_IsTooFar()
    {
        Assert.AreEqual(PassResult.TooFar, Check(distance: Range + 1.1f, allowedDistance: Range + Tolerance));
    }

    [Test]
    public void BeforeCooldownEnds_IsOnCooldown()
    {
        Assert.AreEqual(PassResult.OnCooldown, Check(secondsHeld: 0.5));
    }

    [Test]
    public void ExactlyAtCooldown_IsAllowed()
    {
        Assert.AreEqual(PassResult.Allowed, Check(secondsHeld: Cooldown));
    }

    [Test]
    public void SenderWithoutTheBomb_IsRejected()
    {
        Assert.AreEqual(PassResult.SenderIsNotHolder, Check(holderId: Other));
    }

    [Test]
    public void NobodyHoldsTheBomb_IsRejected()
    {
        Assert.AreEqual(PassResult.SenderIsNotHolder, Check(holderId: MatchManager.NoHolder));
    }

    [Test]
    public void PassingToYourself_IsRejected()
    {
        Assert.AreEqual(PassResult.InvalidTarget, Check(targetId: Holder));
    }

    [Test]
    public void WaitingForPlayers_IsNotPlaying()
    {
        Assert.AreEqual(PassResult.NotPlaying, Check(phase: MatchPhase.WaitingForPlayers));
    }

    [Test]
    public void RoundOver_IsNotPlaying()
    {
        Assert.AreEqual(PassResult.NotPlaying, Check(phase: MatchPhase.RoundOver));
    }

    [Test]
    public void CooldownIsReportedBeforeDistance()
    {
        Assert.AreEqual(PassResult.OnCooldown, Check(secondsHeld: 0.5, distance: 10f));
    }
}
