using System;
using System.Collections.Generic;
using System.Text;

public static class GameListRules
{
    public const int MaxNameLength = 32;

    public static bool IsJoinable(GameListing game, string ownGameId)
    {
        return !game.IsLocked
            && game.MaxPlayers > 0
            && game.PlayerCount < game.MaxPlayers
            && (ownGameId == null || !string.Equals(game.Id, ownGameId, StringComparison.Ordinal));
    }

    public static List<GameListing> Visible(IEnumerable<GameListing> games, string ownGameId)
    {
        List<GameListing> visible = new List<GameListing>();
        if (games == null)
        {
            return visible;
        }

        foreach (GameListing game in games)
        {
            if (IsJoinable(game, ownGameId))
            {
                visible.Add(game);
            }
        }

        visible.Sort((left, right) =>
        {
            int byName = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            return byName != 0 ? byName : StringComparer.Ordinal.Compare(left.Id, right.Id);
        });
        return visible;
    }

    public static string Describe(GameListing game)
    {
        string name = string.IsNullOrWhiteSpace(game.Name) ? "Unnamed game" : game.Name;
        return name + " (" + game.PlayerCount + "/" + game.MaxPlayers + ")";
    }

    public static string CleanGameName(string typedName, string playerName)
    {
        string name = CollapseWhitespace(typedName);
        if (name.Length == 0)
        {
            string player = CollapseWhitespace(playerName);
            name = player.Length == 0 ? "Bomb tag game" : player + "'s game";
        }

        return name.Length <= MaxNameLength ? name : name.Substring(0, MaxNameLength).TrimEnd();
    }

    private static string CollapseWhitespace(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder result = new StringBuilder(value.Length);
        bool pendingSpace = false;
        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(character);
        }

        return result.ToString();
    }
}
