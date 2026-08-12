#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

internal sealed partial class AuthorInkStoryJsonEmitter
{
    void EmitOnceInitializers(List<AuthorInkStatement> statements, AuthorInkFlowCursor cursor)
    {
        var lines = new List<int>();
        CollectOnceLines(statements, lines);
        for (int i = 0; i < lines.Count; i++)
            EmitInternalSet(OnceKey(lines[i]), 1, lines[i], cursor, "init");
    }

    void EmitInternalSet(string key, int value, int line, AuthorInkFlowCursor cursor, string suffix)
    {
        string id = NextId(line, "internal_" + suffix);
        var node = new StoryJsonNode
        {
            id = id,
            guid = id,
            type = StoryJsonTypes.VariableChange,
            title = "Ink internal " + suffix,
            variableKey = key,
            deltaValue = value,
            add = false
        };
        Place(node, cursor);
    }

    void CollectOnceLines(List<AuthorInkStatement> statements, List<int> output)
    {
        if (statements == null) return;
        for (int i = 0; i < statements.Count; i++)
        {
            if (statements[i] is AuthorInkChoiceStatement choice)
            {
                for (int b = 0; b < choice.Branches.Count; b++)
                {
                    if (choice.Branches[b].OnceOnly) output.Add(choice.Branches[b].Line);
                    CollectOnceLines(choice.Branches[b].Body, output);
                }
            }
            else if (statements[i] is AuthorInkWardrobeStatement wardrobe)
            {
                CollectOnceLines(new List<AuthorInkStatement> { wardrobe.Choice }, output);
            }
            else if (statements[i] is AuthorInkSwitchStatement inkSwitch)
            {
                for (int c = 0; c < inkSwitch.Cases.Count; c++) CollectOnceLines(inkSwitch.Cases[c].Body, output);
            }
        }
    }

    bool LooksLikeWardrobeSection(AuthorInkChoiceStatement choice)
    {
        string section = (choice.SectionName ?? "").ToLowerInvariant().Replace('ё', 'е');
        return section.Contains("одежд") || section.Contains("причес") || section.Contains("hair") || section.Contains("outfit");
    }

    static bool CanMapWardrobeBranches(AuthorInkChoiceStatement choice)
    {
        if (choice == null || choice.Branches.Count == 0) return false;
        for (int i = 0; i < choice.Branches.Count; i++)
            if (!TryFindWardrobeAssignment(choice.Branches[i], out _, out _))
                return false;
        return true;
    }

    string ExtractWardrobeItemId(AuthorInkChoiceBranch branch, out List<AuthorInkStatement> remainingBody)
    {
        remainingBody = new List<AuthorInkStatement>();
        string itemId = "";
        for (int i = 0; i < branch.Body.Count; i++)
        {
            AuthorInkStatement statement = branch.Body[i];
            if (statement is AuthorInkLogicStatement logic &&
                AuthorInkLogicMapper.TryReadStringAssignment(logic.Raw, out string name, out string value) &&
                (string.Equals(name, "outfit", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "hair", StringComparison.OrdinalIgnoreCase)))
            {
                itemId = value;
                continue;
            }
            remainingBody.Add(statement);
        }
        return itemId;
    }

    static bool TryFindWardrobeAssignment(AuthorInkChoiceBranch branch, out string name, out string value)
    {
        name = "";
        value = "";
        for (int i = 0; i < branch.Body.Count; i++)
        {
            if (!(branch.Body[i] is AuthorInkLogicStatement logic)) continue;
            if (!AuthorInkLogicMapper.TryReadStringAssignment(logic.Raw, out name, out value)) continue;
            if (string.Equals(name, "outfit", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "hair", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static string CleanWardrobeLabel(string value)
    {
        string result = Regex.Replace(value ?? "", @"\s*\(\s*\d+[^)]*\)\s*$", "").Trim();
        return result;
    }

    static string CleanChoiceDisplayText(string value, int premiumCost)
    {
        string result = value ?? "";
        if (premiumCost > 0)
            result = Regex.Replace(result, @"\s*\(\s*\d+\s*\uFE0F?\s*\)\s*(?=[.!?…»”]*\s*$)", "");
        return StoryJsonConverter.SanitizeDisplayText(result);
    }

    StoryJsonNode NewNode(int line, string type, string kind, string title)
    {
        string id = line == 0 && kind == "start" ? "start" : NextId(line, kind);
        return new StoryJsonNode { id = id, guid = id, type = type, title = title };
    }

    string NextId(int line, string kind)
    {
        int key = line < 0 ? 0 : line;
        _idOrdinals.TryGetValue(key, out int ordinal);
        ordinal++;
        _idOrdinals[key] = ordinal;
        string safeKind = Regex.Replace(kind ?? "node", "[^A-Za-z0-9_]+", "_");
        string lineToken = line == int.MaxValue ? "end" : Math.Max(0, line).ToString("0000");
        return _options.EpisodeId + "_l" + lineToken + "_" + safeKind + "_" + ordinal.ToString("00");
    }

    static string OnceKey(int line) => "__ink_once_l" + line.ToString("0000");

    static string ChoiceKey(string label)
    {
        string safe = Regex.Replace((label ?? "").Trim(), "[^A-Za-z0-9А-Яа-яЁё_]+", "_");
        return "__ink_choice_" + safe;
    }

}
#endif
