using NUnit.Framework;

// Checks the lobby rules by calling them directly, with no scene, network or Play Mode.
// Run them from Window > General > Test Runner > EditMode.
public class LobbyRulesTests
{
    private const int Required = 2;
    private const string Fallback = "Player 1";

    [Test]
    public void TwoPlayersBothReady_CanStart()
    {
        Assert.IsTrue(LobbyRules.CanStart(2, 2, Required));
    }

    [Test]
    public void OneOfTwoReady_CannotStart()
    {
        Assert.IsFalse(LobbyRules.CanStart(2, 1, Required));
    }

    [Test]
    public void OnlyOnePlayerEvenIfReady_CannotStart()
    {
        Assert.IsFalse(LobbyRules.CanStart(1, 1, Required));
    }

    [Test]
    public void NobodyReady_CannotStart()
    {
        Assert.IsFalse(LobbyRules.CanStart(2, 0, Required));
    }

    [Test]
    public void TooManyPlayers_CannotStart()
    {
        Assert.IsFalse(LobbyRules.CanStart(3, 3, Required));
    }

    [Test]
    public void CleanName_KeepsAnOrdinaryName()
    {
        Assert.AreEqual("Tymek", LobbyRules.CleanName("Tymek", Fallback));
    }

    [Test]
    public void CleanName_TrimsSurroundingSpaces()
    {
        Assert.AreEqual("Tymek", LobbyRules.CleanName("  Tymek  ", Fallback));
    }

    [Test]
    public void CleanName_EmptyUsesFallback()
    {
        Assert.AreEqual(Fallback, LobbyRules.CleanName("", Fallback));
    }

    [Test]
    public void CleanName_NullUsesFallback()
    {
        Assert.AreEqual(Fallback, LobbyRules.CleanName(null, Fallback));
    }

    [Test]
    public void CleanName_OnlySpacesUsesFallback()
    {
        Assert.AreEqual(Fallback, LobbyRules.CleanName("   ", Fallback));
    }

    [Test]
    public void CleanName_RemovesControlCharacters()
    {
        Assert.AreEqual("Tymek", LobbyRules.CleanName("Ty\tm\nek\r", Fallback));
    }

    [Test]
    public void CleanName_CutsLongNames()
    {
        string cleaned = LobbyRules.CleanName("abcdefghijklmnopqrstuvwxyz", Fallback);
        Assert.AreEqual(LobbyRules.MaxNameLength, cleaned.Length);
        Assert.AreEqual("abcdefghijklmnop", cleaned);
    }

    [Test]
    public void CleanName_CutNameDoesNotEndWithASpace()
    {
        Assert.AreEqual("abcdefghijklmno", LobbyRules.CleanName("abcdefghijklmno pqrstuvwxyz", Fallback));
    }

    [Test]
    public void CleanName_KeepsPolishLetters()
    {
        Assert.AreEqual("Zażółć gęślą", LobbyRules.CleanName("Zażółć gęślą", Fallback));
    }
}
