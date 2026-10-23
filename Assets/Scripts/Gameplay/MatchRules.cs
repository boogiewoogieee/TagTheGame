using System.Collections.Generic;

// The match's decisions, kept free of Unity and Netcode so they can be tested directly.
public static class MatchRules
{
    public const ulong NoWinner = ulong.MaxValue;

    public static bool HasWon(int roundsWon, int roundsToWin)
    {
        return roundsToWin > 0 && roundsWon >= roundsToWin;
    }

    // The first player with enough round wins, or NoWinner while the match is still open.
    public static ulong WinnerOf(IReadOnlyList<PlayerMatchStats> stats, int roundsToWin)
    {
        foreach (PlayerMatchStats player in stats)
        {
            if (HasWon(player.RoundsWon, roundsToWin))
            {
                return player.ClientId;
            }
        }

        return NoWinner;
    }

    // When the opponent leaves, whoever is left wins, whatever the score was.
    public static ulong WinnerByForfeit(IReadOnlyList<PlayerMatchStats> stats, ulong leaverId)
    {
        foreach (PlayerMatchStats player in stats)
        {
            if (player.ClientId != leaverId)
            {
                return player.ClientId;
            }
        }

        return NoWinner;
    }

    // "12.3 s"; whole seconds are enough once the number gets big.
    public static string FormatHoldTime(float seconds)
    {
        if (seconds < 0f)
        {
            seconds = 0f;
        }

        return seconds < 100f ? seconds.ToString("0.0") + " s" : ((int)seconds).ToString() + " s";
    }
}
