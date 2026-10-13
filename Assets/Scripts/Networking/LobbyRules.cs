using System.Text;

// The lobby's decisions, kept free of Unity and Netcode so they can be tested directly.
public static class LobbyRules
{
    // Short enough to fit a FixedString64Bytes even when every letter takes two bytes.
    public const int MaxNameLength = 16;

    // A match needs exactly the required number of players, and every one of them ready.
    public static bool CanStart(int playerCount, int readyCount, int requiredPlayers)
    {
        return playerCount == requiredPlayers && readyCount == requiredPlayers;
    }

    // What a typed name becomes in the lobby: trimmed, without control characters, cut to
    // MaxNameLength, or the fallback when nothing usable is left.
    public static string CleanName(string typed, string fallback)
    {
        if (string.IsNullOrEmpty(typed))
        {
            return fallback;
        }

        StringBuilder cleaned = new StringBuilder(typed.Length);
        foreach (char c in typed)
        {
            if (!char.IsControl(c))
            {
                cleaned.Append(c);
            }
        }

        string name = cleaned.ToString().Trim();
        if (name.Length == 0)
        {
            return fallback;
        }

        if (name.Length > MaxNameLength)
        {
            name = name.Substring(0, MaxNameLength).TrimEnd();
        }

        return name;
    }
}
