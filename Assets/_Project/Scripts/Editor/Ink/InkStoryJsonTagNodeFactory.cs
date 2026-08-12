#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using static InkStoryJsonUtility;

public static class InkStoryJsonTagNodeFactory
{
    public static List<StoryJsonNode> Build(InkStoryJsonConvertContext context, List<string> tags)
    {
        var nodes = new List<StoryJsonNode>();
        if (context == null || tags == null)
            return nodes;

        if (TryBuildStatNode(context, tags, out StoryJsonNode statNode))
            nodes.Add(statNode);
        if (TryBuildVariableNode(context, tags, out StoryJsonNode variableNode))
            nodes.Add(variableNode);
        return nodes;
    }

    private static bool TryBuildStatNode(InkStoryJsonConvertContext context, List<string> tags, out StoryJsonNode node)
    {
        node = null;
        if (!TryReadTag(tags, "stat", out string value) && !TryReadTag(tags, "стат", out value))
            return false;

        string[] parts = SplitPipe(value);
        if (!TryReadKeyAndNumber(parts[0], out string statId, out int delta, out _))
            return false;

        string id = context.NextId("stat");
        node = new StoryJsonNode
        {
            id = id,
            guid = id,
            type = StoryJsonTypes.StatChange,
            title = "Стат " + statId,
            statId = statId,
            statDelta = delta,
            statDisplayName = parts.Length > 1 ? Clean(parts[1]) : "",
            systemMessage = parts.Length > 2 ? Clean(parts[2]) : ""
        };
        return true;
    }

    private static bool TryBuildVariableNode(InkStoryJsonConvertContext context, List<string> tags, out StoryJsonNode node)
    {
        node = null;
        bool isSetTag = TryReadTag(tags, "set", out string value) || TryReadTag(tags, "установить", out value);
        if (!isSetTag &&
            !TryReadTag(tags, "var", out value) &&
            !TryReadTag(tags, "variable", out value) &&
            !TryReadTag(tags, "переменная", out value))
        {
            return false;
        }

        bool add = !isSetTag;
        if (!TryReadKeyAndNumber(value, out string key, out int amount, out bool explicitSet))
            return false;

        string id = context.NextId("var");
        node = new StoryJsonNode
        {
            id = id,
            guid = id,
            type = StoryJsonTypes.VariableChange,
            title = (explicitSet || !add ? "Set " : "Var ") + key,
            variableKey = key,
            deltaValue = amount,
            add = add && !explicitSet
        };
        return true;
    }

    private static bool TryReadKeyAndNumber(string value, out string key, out int number, out bool explicitSet)
    {
        key = "";
        number = 0;
        explicitSet = false;
        value = (value ?? "").Trim();
        int equals = value.IndexOf('=');
        if (equals > 0)
        {
            explicitSet = true;
            key = Clean(value.Substring(0, equals));
            return int.TryParse(Clean(value.Substring(equals + 1)), out number) && !string.IsNullOrWhiteSpace(key);
        }

        string[] tokens = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
            return false;

        key = Clean(tokens[0]);
        for (int i = 1; i < tokens.Length; i++)
        {
            string token = tokens[i].Trim();
            if (token == "set")
            {
                explicitSet = true;
                continue;
            }
            if (TryParseSignedInt(token, out number))
                return !string.IsNullOrWhiteSpace(key);
        }

        return false;
    }

    private static bool TryParseSignedInt(string value, out int number)
    {
        value = (value ?? "").Trim();
        if (value.StartsWith("+=", StringComparison.Ordinal))
            value = value.Substring(2);
        else if (value.StartsWith("-=", StringComparison.Ordinal))
            value = "-" + value.Substring(2);
        else
            value = value.TrimStart('+');
        return int.TryParse(value, out number);
    }

    private static string[] SplitPipe(string value)
    {
        return (value ?? "").Split(new[] { '|' }, StringSplitOptions.None);
    }
}
#endif
