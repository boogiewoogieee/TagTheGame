using System;
using System.Text;

public static class AuthProfileRules
{
    public const int MaxLength = 30;

    public static string ProfileFor(string[] commandLineArgs, string playModeTag)
    {
        if (commandLineArgs != null)
        {
            for (int index = 0; index < commandLineArgs.Length - 1; index++)
            {
                if (!string.Equals(commandLineArgs[index], "-profile", StringComparison.Ordinal))
                {
                    continue;
                }

                string profile = Sanitize(commandLineArgs[index + 1]);
                if (profile.Length > 0)
                {
                    return profile;
                }
            }
        }

        string tagProfile = Sanitize(playModeTag);
        return tagProfile.Length > 0 ? tagProfile : null;
    }

    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        StringBuilder result = new StringBuilder(Math.Min(raw.Length, MaxLength));
        foreach (char character in raw)
        {
            if (char.IsLetterOrDigit(character) || character == '-' || character == '_')
            {
                result.Append(character);
                if (result.Length == MaxLength)
                {
                    break;
                }
            }
        }

        return result.ToString();
    }
}
