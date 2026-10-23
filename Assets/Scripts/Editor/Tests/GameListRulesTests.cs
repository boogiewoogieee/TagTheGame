using System.Collections.Generic;
using NUnit.Framework;

public class GameListRulesTests
{
    private static GameListing Game(string id, string name, int players = 1, int capacity = 2, bool locked = false)
    {
        return new GameListing
        {
            Id = id,
            Name = name,
            PlayerCount = players,
            MaxPlayers = capacity,
            IsLocked = locked,
        };
    }

    [Test]
    public void IsJoinable_OpenGameWithNoOwnGame_IsTrue()
    {
        Assert.IsTrue(GameListRules.IsJoinable(Game("open", "Open"), null));
    }

    [Test]
    public void IsJoinable_LockedFullOwnAndZeroCapacityGames_AreFalse()
    {
        Assert.IsFalse(GameListRules.IsJoinable(Game("locked", "Locked", locked: true), null));
        Assert.IsFalse(GameListRules.IsJoinable(Game("full", "Full", players: 2), null));
        Assert.IsFalse(GameListRules.IsJoinable(Game("mine", "Mine"), "mine"));
        Assert.IsFalse(GameListRules.IsJoinable(Game("zero", "Zero", players: 0, capacity: 0), null));
    }

    [Test]
    public void Visible_NullAndEmptyInput_ReturnEmptyLists()
    {
        Assert.IsNotNull(GameListRules.Visible(null, null));
        Assert.IsEmpty(GameListRules.Visible(null, null));
        Assert.IsEmpty(GameListRules.Visible(new GameListing[0], null));
    }

    [Test]
    public void Visible_FiltersAndSortsByNameIgnoringCaseThenId()
    {
        List<GameListing> games = new List<GameListing>
        {
            Game("b", "beta"),
            Game("2", "alpha"),
            Game("1", "Alpha"),
            Game("locked", "aaa", locked: true),
            Game("full", "aaa", players: 2),
            Game("mine", "aaa"),
            Game("zero", "aaa", players: 0, capacity: 0),
        };

        List<GameListing> visible = GameListRules.Visible(games, "mine");

        Assert.AreEqual(3, visible.Count);
        Assert.AreEqual("1", visible[0].Id);
        Assert.AreEqual("2", visible[1].Id);
        Assert.AreEqual("b", visible[2].Id);
    }

    [Test]
    public void Describe_UsesNameAndPlayerCount()
    {
        Assert.AreEqual("Kasia's game (1/2)", GameListRules.Describe(Game("a", "Kasia's game")));
    }

    [Test]
    public void Describe_EmptyNameUsesFallback()
    {
        Assert.AreEqual("Unnamed game (1/2)", GameListRules.Describe(Game("a", "")));
        Assert.AreEqual("Unnamed game (1/2)", GameListRules.Describe(Game("a", "  ")));
    }

    [Test]
    public void CleanGameName_TrimsAndCollapsesWhitespace()
    {
        Assert.AreEqual("Friday bomb tag", GameListRules.CleanGameName(" \t Friday  bomb\n tag \r ", "Kasia"));
    }

    [Test]
    public void CleanGameName_CutsToThirtyTwoWithoutTrailingSpace()
    {
        Assert.AreEqual(new string('A', 32), GameListRules.CleanGameName(new string('A', 40), null));
        Assert.AreEqual(new string('A', 31), GameListRules.CleanGameName(new string('A', 31) + " long", null));
    }

    [Test]
    public void CleanGameName_BlankNameUsesPlayerFallback()
    {
        Assert.AreEqual("Kasia Nowak's game", GameListRules.CleanGameName(" \t ", " Kasia  Nowak "));
    }

    [Test]
    public void CleanGameName_BlankNameAndPlayerUseDefaultFallback()
    {
        Assert.AreEqual("Bomb tag game", GameListRules.CleanGameName(null, "  "));
    }
}
