using System;

public static class ContentReleaseStatus
{
    public const string Draft = "draft";
    public const string Staging = "staging";
    public const string Published = "published";
    public const string Archived = "archived";

    public static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }

    public static bool IsKnown(string value)
    {
        string normalized = Normalize(value);
        return normalized == Draft ||
               normalized == Staging ||
               normalized == Published ||
               normalized == Archived;
    }

    public static bool IsLive(string value)
    {
        string normalized = Normalize(value);
        return string.Equals(normalized, Staging, StringComparison.Ordinal) ||
               string.Equals(normalized, Published, StringComparison.Ordinal);
    }
}
