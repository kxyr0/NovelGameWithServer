#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Ink.Runtime;

public sealed class InkStoryJsonConvertContext
{
    public readonly Story Story;
    public readonly List<StoryJsonNode> Nodes = new List<StoryJsonNode>();
    public string Error = "";
    private int _nextIndex;

    public InkStoryJsonConvertContext(Story story)
    {
        Story = story;
    }

    public string NextId(string prefix)
    {
        _nextIndex++;
        return "ink_" + prefix + "_" + _nextIndex.ToString("0000");
    }
}

public static class InkStoryJsonUtility
{
    public static void SplitSpeaker(string line, out string speaker, out string text)
    {
        speaker = "";
        text = line;
        int separator = line.IndexOf(':');
        if (separator <= 0 || separator > 32)
            return;

        speaker = Clean(line.Substring(0, separator));
        text = Clean(line.Substring(separator + 1));
    }

    public static bool TryReadTag(List<string> tags, string key, out string value)
    {
        value = "";
        if (tags == null)
            return false;

        string prefix = key + ":";
        foreach (string tag in tags)
        {
            string trimmed = (tag ?? "").Trim();
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            value = trimmed.Substring(prefix.Length).Trim();
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    public static bool TryReadPremiumCost(List<string> tags, out int cost)
    {
        cost = 0;
        if (!TryReadTag(tags, "premium", out string value) &&
            !TryReadTag(tags, "cost", out value) &&
            !TryReadTag(tags, "paid", out value))
            return false;

        foreach (string part in value.Split(' '))
            if (int.TryParse(part, out cost) && cost > 0)
                return true;

        return false;
    }

    public static string ReadRootTag(Story story, string key)
    {
        return TryReadTag(story.globalTags, key, out string value) ? value : "";
    }

    public static string Clean(string value)
    {
        return StoryJsonConverter.SanitizeDisplayText(value ?? "");
    }

    public static string FirstNonEmpty(params string[] values)
    {
        foreach (string value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        return "";
    }

    public static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
#endif
