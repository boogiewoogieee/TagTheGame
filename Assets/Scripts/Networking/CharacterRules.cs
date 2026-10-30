using System.Collections.Generic;

// Who may play which character, kept free of Unity and Netcode so it can be tested
// directly. Picks are indexes into the CharacterCatalog, and no two players share one.
public static class CharacterRules
{
    public const int None = -1;

    // Before character select the host was Character 01 and the second player Character 04,
    // so those stay the starting picks.
    private static readonly int[] SlotDefaults = { 0, 3 };

    public static bool IsValid(int index, int count)
    {
        return index >= 0 && index < count;
    }

    public static int DefaultFor(int slot, int count)
    {
        if (count <= 0)
        {
            return None;
        }

        int index = slot >= 0 && slot < SlotDefaults.Length ? SlotDefaults[slot] : slot;
        return index < count ? index : index % count;
    }

    // The pick a player gets: the one it asked for if free, else its slot's default if
    // free, else the first free one. None when every character is taken.
    public static int Resolve(int requested, int slot, int count, IList<int> taken)
    {
        if (IsValid(requested, count) && !taken.Contains(requested))
        {
            return requested;
        }

        int fallback = DefaultFor(slot, count);
        if (fallback != None && !taken.Contains(fallback))
        {
            return fallback;
        }

        for (int index = 0; index < count; index++)
        {
            if (!taken.Contains(index))
            {
                return index;
            }
        }

        return None;
    }

    // The next free pick to the left (direction -1) or right (+1), wrapping round the
    // roster. Stays on the current pick when nothing else is free.
    public static int Step(int current, int direction, int count, IList<int> taken)
    {
        if (count <= 0)
        {
            return None;
        }

        int step = direction < 0 ? -1 : 1;
        int start = IsValid(current, count) ? current : 0;
        for (int i = 1; i <= count; i++)
        {
            int index = ((start + step * i) % count + count) % count;
            if (!taken.Contains(index))
            {
                return index;
            }
        }

        return current;
    }
}
