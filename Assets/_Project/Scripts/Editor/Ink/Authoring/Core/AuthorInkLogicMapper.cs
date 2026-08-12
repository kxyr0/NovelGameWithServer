#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;

internal sealed class AuthorInkLogicMapper
{
    static readonly Regex IncrementRegex = new Regex(
        @"^~\s*(?<name>[^\s]+)\s*(?<op>\+\+|--)$",
        RegexOptions.Compiled);

    static readonly Regex SelfMathRegex = new Regex(
        @"^~\s*(?<name>[^\s=]+)\s*=\s*\k<name>\s*(?<op>[+-])\s*(?<value>\d+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex NumericSetRegex = new Regex(
        @"^(?:VAR\s+|~\s*)?(?<name>[^\s=]+)\s*=\s*(?<value>-?\d+)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex StringSetRegex = new Regex(
        @"^(?:VAR\s+|~\s*)?(?<name>[^\s=]+)\s*=\s*""(?<value>[^""]*)""\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    readonly AuthorInkSharedContext _context;

    public AuthorInkLogicMapper(AuthorInkSharedContext context)
    {
        _context = context ?? new AuthorInkSharedContext();
    }

    public bool TryMap(string raw, int line, Func<string, string> idFactory, AuthorInkImportReport report, out StoryJsonNode node)
    {
        node = null;
        string source = AuthorInkSyntax.StripInlineComment(raw);
        Match increment = IncrementRegex.Match(source);
        if (increment.Success)
        {
            string name = increment.Groups["name"].Value.Trim();
            int delta = increment.Groups["op"].Value == "++" ? 1 : -1;
            node = BuildDelta(name, delta, line, idFactory);
            report.LogicStatements++;
            return true;
        }

        Match selfMath = SelfMathRegex.Match(source);
        if (selfMath.Success)
        {
            string name = selfMath.Groups["name"].Value.Trim();
            int value = int.Parse(selfMath.Groups["value"].Value);
            int delta = selfMath.Groups["op"].Value == "+" ? value : -value;
            node = BuildDelta(name, delta, line, idFactory);
            report.LogicStatements++;
            return true;
        }

        Match numeric = NumericSetRegex.Match(source);
        if (numeric.Success)
        {
            string name = numeric.Groups["name"].Value.Trim();
            string id = idFactory("var");
            node = new StoryJsonNode
            {
                id = id,
                guid = id,
                type = StoryJsonTypes.VariableChange,
                title = "Ink var " + name,
                variableKey = name,
                deltaValue = int.Parse(numeric.Groups["value"].Value),
                add = false
            };
            report.LogicStatements++;
            return true;
        }

        if (TryReadStringAssignment(source, out string stringName, out string stringValue))
        {
            AuthorInkVariableKind kind = GetKind(stringName);
            if (kind == AuthorInkVariableKind.Stat || kind == AuthorInkVariableKind.Relationship)
            {
                // Empty-string declarations are authoring declarations; GameState starts numeric stats at zero.
                report.Info(line, "Пропущена строковая декларация числового показателя '" + stringName + "'.");
                return true;
            }

            if (source.StartsWith("VAR ", StringComparison.OrdinalIgnoreCase))
            {
                report.Info(line, "Пропущено строковое значение по умолчанию '" + stringName + " = \"" + stringValue + "\"'.");
                return true;
            }

            report.Warn(line, "Строковое присваивание '" + stringName + " = \"" + stringValue + "\"' не имеет прямого VariableChange-аналога и должно быть поглощено appearance/wardrobe importer-ом.");
            return true;
        }

        report.Warn(line, "Неизвестная Ink-логика: " + raw);
        return false;
    }

    public static bool TryReadStringAssignment(string raw, out string name, out string value)
    {
        Match match = StringSetRegex.Match(AuthorInkSyntax.StripInlineComment(raw));
        name = match.Success ? match.Groups["name"].Value.Trim() : "";
        value = match.Success ? match.Groups["value"].Value.Trim() : "";
        return match.Success;
    }

    StoryJsonNode BuildDelta(string name, int delta, int line, Func<string, string> idFactory)
    {
        AuthorInkVariableKind kind = GetKind(name);
        if (kind == AuthorInkVariableKind.Stat || kind == AuthorInkVariableKind.Relationship)
        {
            string statId = kind == AuthorInkVariableKind.Relationship ? "relationship:" + name : name;
            string id = idFactory("stat");
            return new StoryJsonNode
            {
                id = id,
                guid = id,
                type = StoryJsonTypes.StatChange,
                title = "Ink stat " + name,
                statId = statId,
                statDelta = delta,
                statDisplayName = name
            };
        }

        string variableId = idFactory("var");
        return new StoryJsonNode
        {
            id = variableId,
            guid = variableId,
            type = StoryJsonTypes.VariableChange,
            title = "Ink var " + name,
            variableKey = name,
            deltaValue = delta,
            add = true
        };
    }

    AuthorInkVariableKind GetKind(string name)
    {
        return _context.Variables.TryGetValue(name ?? "", out AuthorInkVariableKind kind)
            ? kind
            : AuthorInkVariableKind.Integer;
    }
}
#endif
