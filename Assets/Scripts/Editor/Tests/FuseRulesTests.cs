using NUnit.Framework;

// Checks how the fuse urgency rises, by calling it directly, with no scene, network or Play Mode.
public class FuseRulesTests
{
    private const float DangerSeconds = 5f;

    [Test]
    public void Danger_WhileCalm_IsZero()
    {
        Assert.AreEqual(0f, FuseRules.Danger(12f, DangerSeconds));
        Assert.AreEqual(0f, FuseRules.Danger(5f, DangerSeconds));
    }

    [Test]
    public void Danger_HalfwayThroughTheLastSeconds_IsHalf()
    {
        Assert.AreEqual(0.5f, FuseRules.Danger(2.5f, DangerSeconds), 0.0001f);
    }

    [Test]
    public void Danger_AtOrPastTheExplosion_IsOne()
    {
        Assert.AreEqual(1f, FuseRules.Danger(0f, DangerSeconds));
        Assert.AreEqual(1f, FuseRules.Danger(-1f, DangerSeconds));
    }

    [Test]
    public void Danger_WithNoDangerTime_StaysCalm()
    {
        Assert.AreEqual(0f, FuseRules.Danger(0f, 0f));
    }

    [Test]
    public void Rate_WhileCalm_IsTheCalmRate()
    {
        Assert.AreEqual(1f, FuseRules.Rate(20f, DangerSeconds, 1f, 6f));
    }

    [Test]
    public void Rate_RisesToTheDangerRate()
    {
        Assert.AreEqual(3.5f, FuseRules.Rate(2.5f, DangerSeconds, 1f, 6f), 0.0001f);
        Assert.AreEqual(6f, FuseRules.Rate(0f, DangerSeconds, 1f, 6f));
    }

    [Test]
    public void IsDangerSecond_StartsAtTheDangerTime()
    {
        Assert.IsFalse(FuseRules.IsDangerSecond(6, DangerSeconds));
        Assert.IsTrue(FuseRules.IsDangerSecond(5, DangerSeconds));
        Assert.IsTrue(FuseRules.IsDangerSecond(1, DangerSeconds));
    }

    [Test]
    public void IsDangerSecond_AtZero_IsOver()
    {
        Assert.IsFalse(FuseRules.IsDangerSecond(0, DangerSeconds));
    }
}
