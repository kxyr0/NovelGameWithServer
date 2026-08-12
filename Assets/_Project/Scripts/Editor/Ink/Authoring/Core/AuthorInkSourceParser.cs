#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal sealed partial class AuthorInkSourceParser
{
    readonly string[] _lines;
    readonly AuthorInkImportReport _report;
    int _index;
    string _section = "";

    public AuthorInkSourceParser(string source, AuthorInkImportReport report)
    {
        _lines = AuthorInkSourceAnalyzer.SplitLines(source);
        _report = report;
        _report.SourceLines = _lines.Length;
    }

    public List<AuthorInkStatement> Parse()
    {
        _index = 0;
        return ParseSequence(() => false);
    }

    List<AuthorInkStatement> ParseSequence(Func<bool> shouldStop)
    {
        var result = new List<AuthorInkStatement>();
        AuthorInkTextStatement text = null;

        while (_index < _lines.Length && !shouldStop())
        {
            string raw = _lines[_index];
            string line = AuthorInkSyntax.Trim(raw);
            int number = _index + 1;

            if (AuthorInkSyntax.IsBlank(line) || AuthorInkSyntax.IsComment(line))
            {
                _index++;
                continue;
            }

            if (TryControlStatement(line, number, out AuthorInkStatement statement))
            {
                FlushText(result, ref text);
                if (statement != null)
                    result.Add(statement);
                continue;
            }

            if (text == null)
                text = new AuthorInkTextStatement { Line = number };
            text.Lines.Add(new AuthorInkTextLine { Line = number, Raw = raw.Trim() });
            _index++;
        }

        FlushText(result, ref text);
        return result;
    }

    bool TryControlStatement(string line, int number, out AuthorInkStatement statement)
    {
        statement = null;

        if (AuthorInkSyntax.TryKnot(line, out string knot))
        {
            _section = knot;
            statement = new AuthorInkAnchorStatement { Line = number, Name = knot };
            _index++;
            return true;
        }

        if (AuthorInkSyntax.TryGather(line, out string gather))
        {
            if (!string.IsNullOrWhiteSpace(gather))
            {
                _section = gather;
                statement = new AuthorInkAnchorStatement { Line = number, Name = gather };
            }
            _index++;
            return true;
        }

        if (line.StartsWith("VAR ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("~", StringComparison.Ordinal))
        {
            statement = new AuthorInkLogicStatement { Line = number, Raw = line };
            _index++;
            return true;
        }

        if (line.StartsWith("->", StringComparison.Ordinal))
        {
            statement = new AuthorInkDivertStatement { Line = number, Target = line.Substring(2).Trim() };
            _index++;
            return true;
        }

        if (AuthorInkSyntax.TryInlineCondition(line, out string variable, out string comparison, out int value, out string yes, out string no))
        {
            statement = new AuthorInkInlineConditionStatement
            {
                Line = number,
                Variable = variable,
                Comparison = comparison,
                RequiredValue = value,
                TrueTarget = yes,
                FalseTarget = no
            };
            _index++;
            return true;
        }

        if (line == "{")
        {
            statement = ParseSwitch(number);
            return true;
        }

        if (AuthorInkSyntax.TryChoice(line, out _, out _, out _, out _))
        {
            statement = ParseChoiceGroup(_section, "");
            return true;
        }

        if (AuthorInkSyntax.TryDirective(line, out string key, out string qualifier, out string directiveValue))
        {
            _index++;
            if (string.Equals(AuthorInkSyntax.NormalizeKey(key), "гардероб", StringComparison.Ordinal) &&
                _index < _lines.Length && NextNonBlankIsChoice())
            {
                SkipBlanksAndComments();
                AuthorInkChoiceStatement choice = ParseChoiceGroup(_section, qualifier);
                statement = new AuthorInkWardrobeStatement
                {
                    Line = number,
                    Prompt = FirstQualifierPart(qualifier, 0),
                    ButtonText = FirstQualifierPart(qualifier, 1),
                    Choice = choice
                };
                return true;
            }

            statement = new AuthorInkDirectiveStatement
            {
                Line = number,
                Key = key,
                Qualifier = qualifier,
                Value = directiveValue
            };
            return true;
        }

        if (line.StartsWith("Серия ", StringComparison.OrdinalIgnoreCase))
        {
            _report.Info(number, "Служебная строка серии не выводится как диалог: " + line);
            _index++;
            return true;
        }

        if (line == "}")
            return false;

        return false;
    }

    bool NextNonBlankIsChoice()
    {
        int cursor = _index;
        while (cursor < _lines.Length && (AuthorInkSyntax.IsBlank(_lines[cursor]) || AuthorInkSyntax.IsComment(_lines[cursor])))
            cursor++;
        return cursor < _lines.Length && AuthorInkSyntax.TryChoice(_lines[cursor], out _, out _, out _, out _);
    }

    void SkipBlanksAndComments()
    {
        while (_index < _lines.Length && (AuthorInkSyntax.IsBlank(_lines[_index]) || AuthorInkSyntax.IsComment(_lines[_index])))
            _index++;
    }

    static string FirstQualifierPart(string qualifier, int index)
    {
        string[] parts = (qualifier ?? "").Split(',');
        return index >= 0 && index < parts.Length ? parts[index].Trim() : "";
    }

    static void FlushText(List<AuthorInkStatement> output, ref AuthorInkTextStatement text)
    {
        if (text == null || text.Lines.Count == 0)
            return;
        output.Add(text);
        text = null;
    }
}
#endif
