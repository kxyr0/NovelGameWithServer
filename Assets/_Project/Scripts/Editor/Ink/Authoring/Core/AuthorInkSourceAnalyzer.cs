#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

internal static class AuthorInkSourceAnalyzer
{
    static readonly Regex VarRegex = new Regex(
        @"^VAR\s+(?<name>[^\s=]+)\s*=\s*(?<value>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static AuthorInkSharedContext Analyze(IEnumerable<string> sources)
    {
        var context = new AuthorInkSharedContext();
        foreach (string source in sources)
            AnalyzeSource(source, context);
        return context;
    }

    static void AnalyzeSource(string source, AuthorInkSharedContext context)
    {
        string section = "";
        string[] lines = SplitLines(source);
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = AuthorInkSyntax.Trim(lines[i]);
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                string comment = trimmed.Substring(2).Trim().ToLowerInvariant().Replace('ё', 'е');
                if (comment.Contains("стат")) section = "stat";
                else if (comment.Contains("фавор")) section = "relationship";
                else if (comment.Contains("внешност")) section = "appearance";
                continue;
            }

            Match variable = VarRegex.Match(AuthorInkSyntax.StripInlineComment(trimmed));
            if (variable.Success)
            {
                string name = variable.Groups["name"].Value.Trim();
                string value = variable.Groups["value"].Value.Trim();
                context.RegisterVariable(name, DetectKind(name, value, section));
                RegisterStringAssignment(context, trimmed);
                continue;
            }

            RegisterStringAssignment(context, trimmed);

            // "Гардероб (prompt):" внешне похож на speaker с emotion в скобках.
            // Сначала отсекаем все известные директивы, иначе получаем фальшивого персонажа "Гардероб".
            if (AuthorInkSyntax.TryDirective(trimmed, out _, out _, out _))
                continue;

            if (AuthorInkSyntax.TrySpeaker(trimmed, out string speaker, out _, out _))
                context.Speakers.Add(speaker);
        }
    }

    static void RegisterStringAssignment(AuthorInkSharedContext context, string line)
    {
        if (AuthorInkLogicMapper.TryReadStringAssignment(line, out string name, out string value))
            context.RegisterStringValue(name, value);
    }

    static AuthorInkVariableKind DetectKind(string name, string value, string section)
    {
        if (string.Equals(name, "appearance", StringComparison.OrdinalIgnoreCase))
            return AuthorInkVariableKind.Appearance;
        if (string.Equals(name, "outfit", StringComparison.OrdinalIgnoreCase))
            return AuthorInkVariableKind.Outfit;
        if (string.Equals(name, "hair", StringComparison.OrdinalIgnoreCase))
            return AuthorInkVariableKind.Hair;
        if (section == "stat")
            return AuthorInkVariableKind.Stat;
        if (section == "relationship")
            return AuthorInkVariableKind.Relationship;
        if (int.TryParse(value.Trim(' ', '"'), out _))
            return AuthorInkVariableKind.Integer;
        return AuthorInkVariableKind.Unknown;
    }

    public static string[] SplitLines(string source)
    {
        return (source ?? "")
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');
    }
}
#endif
