using System.Globalization;

public static class PlayerPublicIdFormatter
{
    public static bool TryFormatServerId(string playerId, out string formatted)
    {
        string source = (playerId ?? "").Trim();
        if (source.Length == 0)
        {
            formatted = "";
            return false;
        }

        if (TryFormatDigits(source, out formatted))
        {
            if (formatted != "999-999")
                return true;

            formatted = "";
            return false;
        }

        formatted = FormatNumber(100000u + StableHash(source) % 900000u);
        return true;
    }

    public static string FormatServerIdOrEmpty(string playerId)
    {
        return TryFormatServerId(playerId, out string formatted) ? formatted : "";
    }

    public static string Format(string playerId, string secondaryIdentity = "", string fallback = "999-999")
    {
        if (TryFormatServerId(playerId, out string formatted))
            return formatted;

        _ = secondaryIdentity;
        return TryFormatDigits(fallback, out formatted) ? formatted : "999-999";
    }

    private static bool TryFormatDigits(string value, out string formatted)
    {
        string digits = (value ?? "").Trim().Replace("-", "");
        if (digits.Length == 6 && uint.TryParse(digits, NumberStyles.None,
                CultureInfo.InvariantCulture, out uint number))
        {
            formatted = FormatNumber(number);
            return true;
        }

        formatted = "";
        return false;
    }

    private static string FormatNumber(uint number)
    {
        string digits = number.ToString("D6", CultureInfo.InvariantCulture);
        return digits.Insert(3, "-");
    }

    private static uint StableHash(string value)
    {
        const uint offset = 2166136261u;
        const uint prime = 16777619u;
        uint hash = offset;
        for (int i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= prime;
        }
        return hash;
    }
}
