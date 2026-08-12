using System;

public static class RegistrationFormValidator
{
    public const int MaxEmailLength = 254;
    private const int MaxLocalPartLength = 64;
    private const int MaxDomainLabelLength = 63;
    private const string LocalSpecials = ".!#$%&'*+-/=?^_`{|}~";

    public static string NormalizeUsername(string value)
    {
        return (value ?? "").Trim();
    }

    public static string NormalizeEmail(string value)
    {
        return (value ?? "").Trim().ToLowerInvariant();
    }

    public static bool IsUsernameReady(string value)
    {
        string username = NormalizeUsername(value);
        if (username.Length == 0)
            return false;

        for (int i = 0; i < username.Length; i++)
        {
            if (char.IsControl(username[i]))
                return false;
        }

        return true;
    }

    public static bool IsStrictEmail(string value)
    {
        string email = NormalizeEmail(value);
        if (email.Length == 0 || email.Length > MaxEmailLength)
            return false;

        int at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || at > MaxLocalPartLength)
            return false;

        string local = email.Substring(0, at);
        string domain = email.Substring(at + 1);
        return IsValidLocalPart(local) && IsValidDomain(domain);
    }

    private static bool IsValidLocalPart(string local)
    {
        if (local[0] == '.' || local[local.Length - 1] == '.' || local.Contains(".."))
            return false;

        for (int i = 0; i < local.Length; i++)
        {
            char c = local[i];
            if (!IsAsciiLetterOrDigit(c) && LocalSpecials.IndexOf(c) < 0)
                return false;
        }

        return true;
    }

    private static bool IsValidDomain(string domain)
    {
        if (domain.Length == 0 || domain.Length > 253 || domain.IndexOf('.') < 0)
            return false;

        string[] labels = domain.Split('.');
        for (int i = 0; i < labels.Length; i++)
        {
            string label = labels[i];
            if (label.Length == 0 || label.Length > MaxDomainLabelLength ||
                label[0] == '-' || label[label.Length - 1] == '-')
                return false;

            for (int j = 0; j < label.Length; j++)
            {
                char c = label[j];
                if (!IsAsciiLetterOrDigit(c) && c != '-')
                    return false;
            }
        }

        string topLevelDomain = labels[labels.Length - 1];
        if (topLevelDomain.Length < 2)
            return false;
        for (int i = 0; i < topLevelDomain.Length; i++)
        {
            if (!IsAsciiLetter(topLevelDomain[i]))
                return false;
        }

        return true;
    }

    private static bool IsAsciiLetterOrDigit(char value)
    {
        return IsAsciiLetter(value) || value >= '0' && value <= '9';
    }

    private static bool IsAsciiLetter(char value)
    {
        return value >= 'a' && value <= 'z' || value >= 'A' && value <= 'Z';
    }
}
