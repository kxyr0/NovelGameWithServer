using System;
using System.Text;

public static class SafeTextSanitizer
{
    private const int MaxRichTextTagChars = 96;

    private static readonly string[] AllowedTmpTags =
    {
        "b", "i", "u", "s", "br", "nobr", "sub", "sup",
        "color", "size", "align", "indent", "line-height", "margin", "mark"
    };

    public static string SanitizeStoryText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return NormalizeSpaces(SanitizeRichText(value, allowSafeTags: true));
    }

    public static string SanitizeUserText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return NormalizeSpaces(SanitizeRichText(value, allowSafeTags: false));
    }

    private static string SanitizeRichText(string value, bool allowSafeTags)
    {
        var builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '<')
            {
                int end = value.IndexOf('>', i + 1);
                if (end < 0 || end - i > MaxRichTextTagChars)
                {
                    builder.Append("&lt;");
                    continue;
                }

                string tag = value.Substring(i + 1, end - i - 1);
                if (allowSafeTags && IsAllowedTmpTag(tag))
                    builder.Append('<').Append(tag).Append('>');
                else
                    builder.Append("&lt;").Append(EscapeText(tag)).Append("&gt;");

                i = end;
                continue;
            }

            AppendSafeChar(builder, c);
        }

        return builder.ToString();
    }

    private static bool IsAllowedTmpTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        string trimmed = tag.Trim();
        if (trimmed.Length == 0)
            return false;

        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if (char.IsControl(c) || c == '<' || c == '>' || c == '`')
                return false;
        }

        if (IsHexColorTag(trimmed))
            return true;

        if (trimmed[0] == '/')
            trimmed = trimmed.Substring(1).TrimStart();

        string name = ExtractTagName(trimmed);
        if (string.IsNullOrEmpty(name))
            return false;

        for (int i = 0; i < AllowedTmpTags.Length; i++)
        {
            if (string.Equals(name, AllowedTmpTags[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ExtractTagName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        int end = 0;
        while (end < value.Length)
        {
            char c = value[end];
            if (char.IsWhiteSpace(c) || c == '=')
                break;
            end++;
        }

        return end <= 0 ? "" : value.Substring(0, end).Trim().ToLowerInvariant();
    }

    private static bool IsHexColorTag(string value)
    {
        if (string.IsNullOrEmpty(value) || value[0] != '#')
            return false;

        int hexChars = value.Length - 1;
        if (hexChars != 3 && hexChars != 4 && hexChars != 6 && hexChars != 8)
            return false;

        for (int i = 1; i < value.Length; i++)
        {
            char c = value[i];
            bool hex = (c >= '0' && c <= '9') ||
                       (c >= 'a' && c <= 'f') ||
                       (c >= 'A' && c <= 'F');
            if (!hex)
                return false;
        }

        return true;
    }

    private static void AppendSafeChar(StringBuilder builder, char c)
    {
        if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
            return;

        if (c == '>')
        {
            builder.Append("&gt;");
            return;
        }

        builder.Append(c);
    }

    private static string EscapeText(string value)
    {
        return (value ?? "")
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static string NormalizeSpaces(string value)
    {
        string result = value ?? "";
        while (result.Contains("  "))
            result = result.Replace("  ", " ");

        return result.Trim();
    }
}
