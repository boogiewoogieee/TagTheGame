using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;

// Checks the match rules by calling them directly, with no scene, network or Play Mode.
// Run them from Window > General > Test Runner > EditMode.
public class MatchRulesTests
{
    private const int RoundsToWin = 2;

    private static List<PlayerMatchStats> Stats(int hostRounds, int clientRounds)
    {
        return new List<PlayerMatchStats>
        {
            new PlayerMatchStats { ClientId = 0, Name = new FixedString64Bytes("Host"), RoundsWon = hostRounds },
            new PlayerMatchStats { ClientId = 1, Name = new FixedString64Bytes("Client"), RoundsWon = clientRounds },
        };
    }

    [Test]
    public void HasWon_AtTheTarget_IsTrue()
    {
        Assert.IsTrue(MatchRules.HasWon(2, RoundsToWin));
    }

    [Test]
    public void HasWon_BelowTheTarget_IsFalse()
    {
        Assert.IsFalse(MatchRules.HasWon(1, RoundsToWin));
    }

    [Test]
    public void HasWon_ZeroTarget_NeverWins()
    {
        Assert.IsFalse(MatchRules.HasWon(5, 0));
    }

    [Test]
    public void WinnerOf_NobodyThereYet_IsNoWinner()
    {
        Assert.AreEqual(MatchRules.NoWinner, MatchRules.WinnerOf(Stats(1, 1), RoundsToWin));
    }

    [Test]
    public void WinnerOf_HostReachesTarget_IsHost()
    {
        Assert.AreEqual(0UL, MatchRules.WinnerOf(Stats(2, 1), RoundsToWin));
    }

    [Test]
    public void WinnerOf_ClientReachesTarget_IsClient()
    {
        Assert.AreEqual(1UL, MatchRules.WinnerOf(Stats(0, 2), RoundsToWin));
    }

    [Test]
    public void WinnerOf_EmptyList_IsNoWinner()
    {
        Assert.AreEqual(MatchRules.NoWinner, MatchRules.WinnerOf(new List<PlayerMatchStats>(), RoundsToWin));
    }

    [Test]
    public void WinnerByForfeit_IsTheOtherPlayer()
    {
        Assert.AreEqual(0UL, MatchRules.WinnerByForfeit(Stats(0, 1), 1));
        Assert.AreEqual(1UL, MatchRules.WinnerByForfeit(Stats(1, 0), 0));
    }

    [Test]
    public void WinnerByForfeit_NobodyLeft_IsNoWinner()
    {
        List<PlayerMatchStats> alone = new List<PlayerMatchStats> { new PlayerMatchStats { ClientId = 1 } };
        Assert.AreEqual(MatchRules.NoWinner, MatchRules.WinnerByForfeit(alone, 1));
    }

    [Test]
    public void FormatHoldTime_ShowsTenths()
    {
        Assert.AreEqual("12.3 s", MatchRules.FormatHoldTime(12.34f));
    }

    [Test]
    public void FormatHoldTime_BigNumbersDropTheTenths()
    {
        Assert.AreEqual("125 s", MatchRules.FormatHoldTime(125.7f));
    }

    [Test]
    public void FormatHoldTime_NeverNegative()
    {
        Assert.AreEqual("0.0 s", MatchRules.FormatHoldTime(-3f));
    }
}
