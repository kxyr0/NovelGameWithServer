#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal enum AuthorInkVariableKind
{
    Unknown,
    Stat,
    Relationship,
    Integer,
    Appearance,
    Outfit,
    Hair
}

internal sealed class AuthorInkSharedContext
{
    public readonly Dictionary<string, AuthorInkVariableKind> Variables =
        new Dictionary<string, AuthorInkVariableKind>(StringComparer.OrdinalIgnoreCase);

    public readonly List<string> VariableOrder = new List<string>();

    public readonly HashSet<string> Speakers =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public readonly Dictionary<string, HashSet<string>> StringValues =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    public void RegisterVariable(string name, AuthorInkVariableKind kind)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        if (!Variables.ContainsKey(name))
            VariableOrder.Add(name);

        Variables[name] = kind;
    }

    public void RegisterStringValue(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
            return;

        if (!StringValues.TryGetValue(name, out HashSet<string> values))
        {
            values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            StringValues[name] = values;
        }

        values.Add(value.Trim());
    }
}

internal sealed class AuthorInkCompileOptions
{
    public string StoryId;
    public string EpisodeId;
    public string Title;
    public string DefaultName = "Элементина";
    public string EpisodeKnot;
    public string NextEpisodeKnot;
    public string HeroCharacterId = "hero";
}

internal sealed class AuthorInkImportReport
{
    public readonly List<string> Warnings = new List<string>();
    public readonly List<string> Infos = new List<string>();
    public int SourceLines;
    public int OutputNodes;
    public int DialogueLines;
    public int Choices;
    public int Directives;
    public int LogicStatements;

    public void Warn(int line, string message)
    {
        Warnings.Add("L" + line + ": " + message);
    }

    public void Info(int line, string message)
    {
        Infos.Add("L" + line + ": " + message);
    }

    public override string ToString()
    {
        return "Строк: " + SourceLines +
               ", узлов: " + OutputNodes +
               ", реплик: " + DialogueLines +
               ", выборов: " + Choices +
               ", директив: " + Directives +
               ", логики: " + LogicStatements +
               ", предупреждений: " + Warnings.Count +
               (Warnings.Count == 0 ? "" : "\n" + string.Join("\n", Warnings));
    }
}

internal abstract class AuthorInkStatement
{
    public int Line;
}

internal sealed class AuthorInkAnchorStatement : AuthorInkStatement
{
    public string Name;
}

internal sealed class AuthorInkTextStatement : AuthorInkStatement
{
    public readonly List<AuthorInkTextLine> Lines = new List<AuthorInkTextLine>();
}

internal sealed class AuthorInkTextLine
{
    public int Line;
    public string Raw;
}

internal sealed class AuthorInkDirectiveStatement : AuthorInkStatement
{
    public string Key;
    public string Qualifier;
    public string Value;
}

internal sealed class AuthorInkLogicStatement : AuthorInkStatement
{
    public string Raw;
}

internal sealed class AuthorInkDivertStatement : AuthorInkStatement
{
    public string Target;
}

internal sealed class AuthorInkInlineConditionStatement : AuthorInkStatement
{
    public string Variable;
    public string Comparison;
    public int RequiredValue;
    public string TrueTarget;
    public string FalseTarget;
}

internal sealed class AuthorInkChoiceStatement : AuthorInkStatement
{
    public string SectionName;
    public string Prompt;
    public string ContinuationAnchor;
    public readonly List<AuthorInkChoiceBranch> Branches = new List<AuthorInkChoiceBranch>();
}

internal sealed class AuthorInkWardrobeStatement : AuthorInkStatement
{
    public string Prompt;
    public string ButtonText;
    public AuthorInkChoiceStatement Choice;
}

internal sealed class AuthorInkChoiceBranch
{
    public int Line;
    public bool OnceOnly;
    public string Label;
    public string Text;
    public int PremiumCost;
    public readonly List<AuthorInkStatement> Body = new List<AuthorInkStatement>();
}

internal sealed class AuthorInkSwitchStatement : AuthorInkStatement
{
    public readonly List<AuthorInkSwitchCase> Cases = new List<AuthorInkSwitchCase>();
}

internal sealed class AuthorInkSwitchCase
{
    public int Line;
    public string Label;
    public bool IsElse;
    public readonly List<AuthorInkStatement> Body = new List<AuthorInkStatement>();
}
#endif
