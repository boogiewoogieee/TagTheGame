using NUnit.Framework;

// Checks the character select rules by calling them directly, with no scene, network or Play Mode.
// Run them from Window > General > Test Runner > EditMode.
public class CharacterRulesTests
{
    private const int Count = 10;

    [Test]
    public void HostDefault_IsCharacter01()
    {
        Assert.AreEqual(0, CharacterRules.DefaultFor(0, Count));
    }

    [Test]
    public void SecondPlayerDefault_IsCharacter04()
    {
        Assert.AreEqual(3, CharacterRules.DefaultFor(1, Count));
    }

    [Test]
    public void EmptyCatalog_HasNoDefault()
    {
        Assert.AreEqual(CharacterRules.None, CharacterRules.DefaultFor(0, 0));
    }

    [Test]
    public void FreeRequest_IsKept()
    {
        Assert.AreEqual(7, CharacterRules.Resolve(7, 1, Count, new[] { 0 }));
    }

    [Test]
    public void TakenRequest_FallsBackToSlotDefault()
    {
        Assert.AreEqual(3, CharacterRules.Resolve(0, 1, Count, new[] { 0 }));
    }

    [Test]
    public void NoRequest_GetsSlotDefault()
    {
        Assert.AreEqual(0, CharacterRules.Resolve(CharacterRules.None, 0, Count, new int[0]));
    }

    [Test]
    public void OutOfRangeRequest_GetsSlotDefault()
    {
        Assert.AreEqual(3, CharacterRules.Resolve(12, 1, Count, new[] { 0 }));
    }

    [Test]
    public void RequestAndDefaultTaken_GetsFirstFree()
    {
        Assert.AreEqual(1, CharacterRules.Resolve(3, 1, Count, new[] { 3, 0 }));
    }

    [Test]
    public void EverythingTaken_GetsNone()
    {
        Assert.AreEqual(CharacterRules.None, CharacterRules.Resolve(0, 0, 2, new[] { 0, 1 }));
    }

    [Test]
    public void StepRight_SkipsTakenPick()
    {
        Assert.AreEqual(4, CharacterRules.Step(2, 1, Count, new[] { 3 }));
    }

    [Test]
    public void StepLeft_SkipsTakenPick()
    {
        Assert.AreEqual(2, CharacterRules.Step(4, -1, Count, new[] { 3 }));
    }

    [Test]
    public void StepRight_WrapsToStart()
    {
        Assert.AreEqual(0, CharacterRules.Step(9, 1, Count, new[] { 3 }));
    }

    [Test]
    public void StepLeft_WrapsToEnd()
    {
        Assert.AreEqual(9, CharacterRules.Step(0, -1, Count, new[] { 3 }));
    }

    [Test]
    public void StepWithNothingElseFree_StaysPut()
    {
        Assert.AreEqual(0, CharacterRules.Step(0, 1, 2, new[] { 1 }));
    }
}
