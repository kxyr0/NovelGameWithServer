#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;

internal static class AuthorInkSyntax
{
    static readonly Regex ChoiceRegex = new Regex(
        @"^(?<kind>[*+])\s*(?:\((?<label>[^)]*)\))?\s*\[(?<text>[^\]]*)\](?<tail>.*)$",
        RegexOptions.Compiled);

    static readonly Regex PaidRegex = new Regex(
        @"#\s*(?:paid|premium|cost)\s*:\s*(?<cost>\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex KnotRegex = new Regex(
        @"^===\s*(?<name>.+?)\s*===\s*$",
        RegexOptions.Compiled);

    static readonly Regex GatherRegex = new Regex(
        @"^-\s*(?:\((?<name>[^)]*)\))?\s*$",
        RegexOptions.Compiled);

    static readonly Regex CaseRegex = new Regex(
        @"^-\s*(?<name>[^:]+?)\s*:\s*$",
        RegexOptions.Compiled);

    static readonly Regex InlineConditionRegex = new Regex(
        @"^\{\s*(?<var>[^\s]+)\s*(?<op>==|!=|>=|<=|>|<)\s*(?<value>-?\d+)\s*:\s*->\s*(?<true>[^|}]+?)\s*\|\s*->\s*(?<false>[^}]+?)\s*\}\s*$",
        RegexOptions.Compiled);

    static readonly Regex DirectiveRegex = new Regex(
        @"^(?<key>[^:()]{2,40}?)(?:\s*\((?<qualifier>[^)]*)\))?\s*:\s*(?<value>.*)$",
        RegexOptions.Compiled);

    static readonly Regex CompositeSpeakerRegex = new Regex(
        @"^(?<speaker>[^:(),]{1,48}?)\s*\((?<qualifier>[^)]{1,100})\)\s*,\s*\((?<emotion>[^)]{1,100})\)\s*:\s*(?<text>.*)$",
        RegexOptions.Compiled);

    static readonly Regex SpeakerRegex = new Regex(
        @"^(?<speaker>[^:]{1,48}?)\s*\((?<emotion>[^)]{1,100})\)\s*:\s*(?<text>.*)$",
        RegexOptions.Compiled);

    static readonly Regex MalformedSpeakerRegex = new Regex(
        @"^(?<speaker>[^:]{1,48}?)\s*:\s*\((?<emotion>[^)]{1,100})\)\s*:\s*(?<text>.*)$",
        RegexOptions.Compiled);

    public static bool IsBlank(string line) => string.IsNullOrWhiteSpace(line);

    public static bool IsComment(string line) => Trim(line).StartsWith("//", StringComparison.Ordinal);

    public static string Trim(string line) => (line ?? "").Trim();

    public static string StripInlineComment(string value)
    {
        string source = value ?? "";
        int index = source.IndexOf("//", StringComparison.Ordinal);
        return (index >= 0 ? source.Substring(0, index) : source).Trim();
    }

    public static bool TryChoice(string line, out bool onceOnly, out string label, out string text, out int premiumCost)
    {
        onceOnly = false;
        label = "";
        text = "";
        premiumCost = 0;
        Match match = ChoiceRegex.Match(Trim(line));
        if (!match.Success)
            return false;

        onceOnly = match.Groups["kind"].Value == "*";
        label = match.Groups["label"].Value.Trim();
        text = match.Groups["text"].Value.Trim();
        Match paid = PaidRegex.Match(match.Groups["tail"].Value);
        if (paid.Success)
            int.TryParse(paid.Groups["cost"].Value, out premiumCost);
        return true;
    }

    public static bool TryKnot(string line, out string name)
    {
        Match match = KnotRegex.Match(Trim(line));
        name = match.Success ? match.Groups["name"].Value.Trim() : "";
        return match.Success;
    }

    public static bool TryGather(string line, out string name)
    {
        Match match = GatherRegex.Match(Trim(line));
        name = match.Success ? match.Groups["name"].Value.Trim() : "";
        return match.Success;
    }

    public static bool TryCase(string line, out string name)
    {
        Match match = CaseRegex.Match(Trim(line));
        name = match.Success ? match.Groups["name"].Value.Trim() : "";
        return match.Success;
    }

    public static bool TryInlineCondition(
        string line,
        out string variable,
        out string comparison,
        out int requiredValue,
        out string trueTarget,
        out string falseTarget)
    {
        Match match = InlineConditionRegex.Match(Trim(line));
        variable = match.Success ? match.Groups["var"].Value.Trim() : "";
        comparison = match.Success ? NormalizeComparison(match.Groups["op"].Value) : "Equals";
        requiredValue = match.Success && int.TryParse(match.Groups["value"].Value, out int parsed) ? parsed : 0;
        trueTarget = match.Success ? match.Groups["true"].Value.Trim() : "";
        falseTarget = match.Success ? match.Groups["false"].Value.Trim() : "";
        return match.Success;
    }

    public static bool TryDirective(string line, out string key, out string qualifier, out string value)
    {
        Match match = DirectiveRegex.Match(Trim(line));
        key = match.Success ? match.Groups["key"].Value.Trim() : "";
        qualifier = match.Success ? match.Groups["qualifier"].Value.Trim() : "";
        value = match.Success ? match.Groups["value"].Value.Trim() : "";
        return match.Success && IsKnownDirective(key);
    }

    public static bool TrySpeaker(string line, out string speaker, out string emotion, out string text)
    {
        Match match = CompositeSpeakerRegex.Match(Trim(line));
        if (!match.Success)
            match = SpeakerRegex.Match(Trim(line));
        if (!match.Success)
            match = MalformedSpeakerRegex.Match(Trim(line));

        speaker = match.Success ? match.Groups["speaker"].Value.Trim() : "";
        emotion = match.Success ? match.Groups["emotion"].Value.Trim() : "";
        text = match.Success ? match.Groups["text"].Value.Trim() : Trim(line);
        return match.Success;
    }

    public static bool IsKnownDirective(string key)
    {
        string normalized = NormalizeKey(key);
        switch (normalized)
        {
            case "локация":
            case "музыка":
            case "камера":
            case "арт":
            case "уведомление":
            case "подсказка":
            case "звук":
            case "звуки окружения":
            case "кат-сцена":
            case "кат сцена":
            case "гардероб":
            case "название":
            case "жанры":
            case "аннотация":
            case "статы":
            case "описание":
                return true;
            default:
                return normalized.StartsWith("серия", StringComparison.Ordinal);
        }
    }

    public static string NormalizeKey(string value) => Trim(value).ToLowerInvariant().Replace('ё', 'е');

    public static string NormalizeComparison(string op)
    {
        switch (op)
        {
            case "!=": return "NotEquals";
            case ">": return "GreaterThan";
            case ">=": return "GreaterOrEqual";
            case "<": return "LessThan";
            case "<=": return "LessOrEqual";
            default: return "Equals";
        }
    }
}
#endif
