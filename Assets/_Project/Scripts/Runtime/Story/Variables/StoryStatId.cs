using System;

public static class StoryStatId
{
    public static string Canonical(string value)
    {
        string normalized = Normalize(value);
        if (string.IsNullOrEmpty(normalized))
            return "";

        switch (normalized)
        {
            case "town":
            case "city":
            case "gorod":
            case "город":
                return "city";

            case "story":
            case "tale":
            case "fairytale":
            case "skazka":
            case "сказка":
                return "fairytale";

            case "reputation":
            case "rep":
            case "respect":
            case "репутация":
                return "reputation";

            case "heart":
            case "hearts":
            case "spark":
            case "sparks":
            case "искра":
            case "искры":
                return "hearts";

            case "candle":
            case "candles":
            case "свеча":
            case "свечи":
                return "candles";

            default:
                return normalized;
        }
    }

    public static bool EqualsCanonical(string left, string right)
    {
        return string.Equals(Canonical(left), Canonical(right), StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return string.Join(" ", value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            .Trim()
            .ToLowerInvariant();
    }
}
