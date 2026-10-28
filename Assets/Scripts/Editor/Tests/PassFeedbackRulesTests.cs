using NUnit.Framework;

public class PassFeedbackRulesTests
{
    private const ulong Local = 0;
    private const ulong Other = 1;

    [Test]
    public void OwnRecentPress_WasAlreadyShown()
    {
        Assert.IsTrue(PassFeedbackRules.AlreadyShownAtPress(Local, Local, 10.0, 10.08, 1.0));
    }

    [Test]
    public void OwnPress_LongAgo_IsShownAgain()
    {
        Assert.IsFalse(PassFeedbackRules.AlreadyShownAtPress(Local, Local, 10.0, 11.5, 1.0));
    }

    [Test]
    public void OtherPlayersPass_IsAlwaysShown()
    {
        Assert.IsFalse(PassFeedbackRules.AlreadyShownAtPress(Other, Local, 10.0, 10.05, 1.0));
    }

    [Test]
    public void NoPressYet_IsShown()
    {
        Assert.IsFalse(PassFeedbackRules.AlreadyShownAtPress(Local, Local, double.MinValue, 10.0, 1.0));
    }

    [Test]
    public void PopScale_PeaksEarlyAndEndsAtOne()
    {
        Assert.AreEqual(1f, PassFeedbackRules.PopScale(0f, 0.25f, 1.5f), 1e-4f);
        Assert.AreEqual(1.5f, PassFeedbackRules.PopScale(0.05f, 0.25f, 1.5f), 1e-4f);
        Assert.Less(PassFeedbackRules.PopScale(0.2f, 0.25f, 1.5f), 1.5f);
        Assert.AreEqual(1f, PassFeedbackRules.PopScale(0.25f, 0.25f, 1.5f), 1e-4f);
    }

    [Test]
    public void PopScale_OutsideThePop_IsOne()
    {
        Assert.AreEqual(1f, PassFeedbackRules.PopScale(-0.1f, 0.25f, 1.5f), 1e-4f);
        Assert.AreEqual(1f, PassFeedbackRules.PopScale(0.1f, 0f, 1.5f), 1e-4f);
    }

    [Test]
    public void FlashIntensity_StartsFullAndFadesOut()
    {
        Assert.AreEqual(8f, PassFeedbackRules.FlashIntensity(0f, 0.3f, 8f), 1e-4f);
        Assert.AreEqual(2f, PassFeedbackRules.FlashIntensity(0.15f, 0.3f, 8f), 1e-4f);
        Assert.AreEqual(0f, PassFeedbackRules.FlashIntensity(0.3f, 0.3f, 8f), 1e-4f);
    }
}
