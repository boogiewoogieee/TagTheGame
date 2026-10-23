using NUnit.Framework;

public class AuthProfileRulesTests
{
    [Test]
    public void ProfileFor_CommandLineNameWins()
    {
        Assert.AreEqual("Host_1", AuthProfileRules.ProfileFor(new[] { "game.exe", "-profile", " Host_1! " }, "VirtualPlayer"));
    }

    [Test]
    public void ProfileFor_LastArgumentFlagFallsBackToTag()
    {
        Assert.AreEqual("VirtualPlayer", AuthProfileRules.ProfileFor(new[] { "game.exe", "-profile" }, "Virtual Player"));
    }

    [Test]
    public void ProfileFor_TagAloneIsUsed()
    {
        Assert.AreEqual("Player2", AuthProfileRules.ProfileFor(new[] { "game.exe" }, "Player 2"));
    }

    [Test]
    public void ProfileFor_NoValidNameUsesDefaultProfile()
    {
        Assert.IsNull(AuthProfileRules.ProfileFor(null, null));
        Assert.IsNull(AuthProfileRules.ProfileFor(new[] { "-profile", "!!!" }, "   "));
    }

    [Test]
    public void ProfileFor_InvalidCommandLineNameFallsBackToTag()
    {
        Assert.AreEqual("Virtual", AuthProfileRules.ProfileFor(new[] { "-profile", "!!!" }, "Virtual"));
    }

    [Test]
    public void Sanitize_RemovesSpacesAndSymbolsButKeepsAllowedPunctuation()
    {
        Assert.AreEqual("A_B-42", AuthProfileRules.Sanitize(" A _B-42!@ "));
    }

    [Test]
    public void Sanitize_DropsLettersTheSignInServiceRejects()
    {
        Assert.AreEqual("Za_gl", AuthProfileRules.Sanitize("Zażółć_gęślą"));
    }

    [Test]
    public void Sanitize_CutsAfterThirtyAllowedCharacters()
    {
        Assert.AreEqual(new string('x', 30), AuthProfileRules.Sanitize(new string('x', 40)));
    }

    [Test]
    public void Sanitize_AllInvalidInputIsEmpty()
    {
        Assert.AreEqual(string.Empty, AuthProfileRules.Sanitize(" !@#$%^&*() "));
        Assert.AreEqual(string.Empty, AuthProfileRules.Sanitize(null));
    }
}
