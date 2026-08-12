#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal sealed partial class AuthorInkSourceParser
{
    AuthorInkChoiceStatement ParseChoiceGroup(string sectionName, string prompt)
    {
        var group = new AuthorInkChoiceStatement
        {
            Line = _index + 1,
            SectionName = sectionName ?? "",
            Prompt = prompt ?? ""
        };

        while (_index < _lines.Length)
        {
            string line = AuthorInkSyntax.Trim(_lines[_index]);
            if (!AuthorInkSyntax.TryChoice(line, out bool onceOnly, out string label, out string text, out int cost))
                break;

            var branch = new AuthorInkChoiceBranch
            {
                Line = _index + 1,
                OnceOnly = onceOnly,
                Label = label,
                Text = text,
                PremiumCost = cost
            };
            _index++;

            List<AuthorInkStatement> body = ParseSequence(IsChoiceBodyStop);
            branch.Body.AddRange(body);
            group.Branches.Add(branch);

            if (_index >= _lines.Length || !AuthorInkSyntax.TryChoice(_lines[_index], out _, out _, out _, out _))
                break;
        }

        if (_index < _lines.Length && AuthorInkSyntax.TryGather(_lines[_index], out string gather))
        {
            _index++;
            if (!string.IsNullOrWhiteSpace(gather))
            {
                _section = gather;
                group.ContinuationAnchor = gather;
            }
        }

        _report.Choices += group.Branches.Count;
        return group;
    }

    bool IsChoiceBodyStop()
    {
        if (_index >= _lines.Length)
            return true;

        string line = AuthorInkSyntax.Trim(_lines[_index]);
        if (AuthorInkSyntax.TryChoice(line, out _, out _, out _, out _))
            return true;
        if (AuthorInkSyntax.TryGather(line, out _))
            return true;
        if (AuthorInkSyntax.TryKnot(line, out _))
            return true;
        return false;
    }

    AuthorInkSwitchStatement ParseSwitch(int lineNumber)
    {
        var statement = new AuthorInkSwitchStatement { Line = lineNumber };
        _index++; // {

        while (_index < _lines.Length)
        {
            SkipBlanksAndComments();
            if (_index >= _lines.Length)
                break;

            string line = AuthorInkSyntax.Trim(_lines[_index]);
            if (line == "}")
            {
                _index++;
                break;
            }

            if (!AuthorInkSyntax.TryCase(line, out string caseName))
            {
                _report.Warn(_index + 1, "Ожидалась ветка Ink switch '- label:', найдено: " + line);
                _index++;
                continue;
            }

            var item = new AuthorInkSwitchCase
            {
                Line = _index + 1,
                Label = caseName,
                IsElse = string.Equals(caseName, "else", StringComparison.OrdinalIgnoreCase)
            };
            _index++;
            item.Body.AddRange(ParseSequence(IsSwitchBodyStop));
            statement.Cases.Add(item);
        }

        return statement;
    }

    bool IsSwitchBodyStop()
    {
        if (_index >= _lines.Length)
            return true;
        string line = AuthorInkSyntax.Trim(_lines[_index]);
        return line == "}" || AuthorInkSyntax.TryCase(line, out _);
    }
}
#endif
