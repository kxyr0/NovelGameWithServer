using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class NovelTemplateContentValidator
{
    private const string MenuPath = "Tools/Novel Template/Validate Content";

    [MenuItem(MenuPath)]
    public static void ValidateContent()
    {
        int errors = 0;
        int warnings = 0;

        ValidateClothingItems(ref errors, ref warnings);
        ValidateStoryGraphs(ref errors, ref warnings);
        ValidateStoryData(ref errors, ref warnings);

        string summary = $"Novel Template validation finished: {errors} errors, {warnings} warnings.";
        if (errors > 0)
            Debug.LogError(summary);
        else if (warnings > 0)
            Debug.LogWarning(summary);
        else
            Debug.Log(summary);
    }

    private static void ValidateClothingItems(ref int errors, ref int warnings)
    {
        Dictionary<string, ClothingItem> seenIds = new Dictionary<string, ClothingItem>();
        foreach (ClothingItem item in LoadAssets<ClothingItem>())
        {
            if (item == null)
                continue;

            string label = GetAssetLabel(item);
            if (string.IsNullOrWhiteSpace(item.id))
            {
                LogError($"ClothingItem has empty id: {label}", item, ref errors);
            }
            else if (seenIds.TryGetValue(item.id, out ClothingItem duplicate))
            {
                LogWarning($"Duplicate ClothingItem id '{item.id}': {label} and {GetAssetLabel(duplicate)}", item, ref warnings);
            }
            else
            {
                seenIds[item.id] = item;
            }

            if (item.sprite == null)
                LogError($"ClothingItem '{item.id}' has no sprite: {label}", item, ref errors);
        }
    }

    private static void ValidateStoryGraphs(ref int errors, ref int warnings)
    {
        foreach (StoryGraph graph in LoadAssets<StoryGraph>())
        {
            if (graph == null)
                continue;

            ValidateGraph(graph, ref errors, ref warnings);
        }
    }

    private static void ValidateStoryData(ref int errors, ref int warnings)
    {
        foreach (StoryData story in LoadAssets<StoryData>())
        {
            if (story == null)
                continue;

            if (string.IsNullOrWhiteSpace(story.StoryId))
                LogWarning($"StoryData has empty story id: {GetAssetLabel(story)}", story, ref warnings);

            IReadOnlyList<ChapterData> chapters = story.Chapters;
            if (chapters == null || chapters.Count == 0)
            {
                LogError($"StoryData has no chapters: {GetAssetLabel(story)}", story, ref errors);
                continue;
            }

            for (int i = 0; i < chapters.Count; i++)
            {
                ChapterData chapter = chapters[i];
                if (chapter == null)
                {
                    LogError($"StoryData '{story.name}' has null chapter at index {i}.", story, ref errors);
                    continue;
                }

                if (chapter.Graph == null && chapter.JsonGraph == null)
                    LogError($"Chapter '{chapter.name}' has neither graph nor json graph.", chapter, ref errors);
            }
        }
    }

    private static void ValidateGraph(StoryGraph graph, ref int errors, ref int warnings)
    {
        if (graph.nodes == null || graph.nodes.Count == 0)
        {
            LogError($"StoryGraph has no nodes: {GetAssetLabel(graph)}", graph, ref errors);
            return;
        }

        HashSet<string> guids = new HashSet<string>();
        bool hasStart = false;

        foreach (XNode.Node rawNode in graph.nodes)
        {
            BaseStoryNode node = rawNode as BaseStoryNode;
            if (node == null)
                continue;

            if (node is StartNode)
                hasStart = true;

            if (string.IsNullOrWhiteSpace(node.guid))
                LogError($"Node has empty guid in graph '{graph.name}'.", graph, ref errors);
            else if (!guids.Add(node.guid))
                LogError($"Duplicate node guid '{node.guid}' in graph '{graph.name}'.", graph, ref errors);

            ValidateWardrobeChoice(node as WardrobeChoiceNode, ref errors, ref warnings);
            ValidateDialogueNode(node as DialogueNode, ref warnings);
            ValidateChoiceNode(node as ChoiceNode, ref warnings);
        }

        if (!hasStart)
            LogError($"StoryGraph has no StartNode: {GetAssetLabel(graph)}", graph, ref errors);
    }

    private static void ValidateWardrobeChoice(WardrobeChoiceNode node, ref int errors, ref int warnings)
    {
        if (node == null)
            return;

        if (node.availableClothes == null || node.availableClothes.Count == 0)
        {
            LogError($"WardrobeChoiceNode '{node.guid}' has no clothes.", node, ref errors);
            return;
        }

        for (int i = 0; i < node.availableClothes.Count; i++)
        {
            ClothingItem item = node.availableClothes[i];
            if (item == null)
            {
                LogError($"WardrobeChoiceNode '{node.guid}' has null clothing at index {i}.", node, ref errors);
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.id))
                LogError($"WardrobeChoiceNode '{node.guid}' uses clothing with empty id at index {i}.", item, ref errors);

            if (item.sprite == null)
                LogError($"WardrobeChoiceNode '{node.guid}' uses clothing '{item.id}' with no sprite.", item, ref errors);

            if (node.premiumCosts != null && i < node.premiumCosts.Count)
            {
                int cost = node.premiumCosts[i];
                if (cost < 0 || cost > SaveDataSanitizer.MaxCurrencyValue)
                    LogError($"WardrobeChoiceNode '{node.guid}' has invalid premium cost {cost} at index {i}.", node, ref errors);
            }

            WardrobeChoiceOptionRule rule = node.GetOptionRule(i);
            if (rule != null)
            {
                int ruleCost = rule.GetPremiumCost();
                if (rule.premiumCost < 0 || rule.premiumCost > SaveDataSanitizer.MaxCurrencyValue)
                    LogError($"WardrobeChoiceNode '{node.guid}' has invalid option rule cost {rule.premiumCost} at index {i}.", node, ref errors);

                if (ruleCost > 0 && string.IsNullOrWhiteSpace(rule.GetServerPurchaseKey(node.guid, i, item.id)))
                    LogWarning($"WardrobeChoiceNode '{node.guid}' has paid option '{item.id}' without a purchase key.", node, ref warnings);
            }
        }

        if (node.premiumCosts != null && node.premiumCosts.Count > 0 && node.premiumCosts.Count < node.availableClothes.Count)
        {
            LogWarning(
                $"WardrobeChoiceNode '{node.guid}' has fewer premium costs than clothes. Clothes={node.availableClothes.Count}, premiumCosts={node.premiumCosts.Count}.",
                node,
                ref warnings);
        }

        if (node.optionRules != null && node.optionRules.Count > 0 && node.optionRules.Count < node.availableClothes.Count)
        {
            LogWarning(
                $"WardrobeChoiceNode '{node.guid}' has fewer option rules than clothes. Clothes={node.availableClothes.Count}, optionRules={node.optionRules.Count}.",
                node,
                ref warnings);
        }

        if (node.exits == null || node.exits.Count < node.availableClothes.Count)
        {
            LogWarning(
                $"WardrobeChoiceNode '{node.guid}' has fewer exits than clothes. Clothes={node.availableClothes.Count}, exits={(node.exits != null ? node.exits.Count : 0)}.",
                node,
                ref warnings);
        }
    }

    private static void ValidateDialogueNode(DialogueNode node, ref int warnings)
    {
        if (node == null || node.lines == null)
            return;

        for (int i = 0; i < node.lines.Count; i++)
        {
            DialogueLine line = node.lines[i];
            if (line == null)
            {
                LogWarning($"DialogueNode '{node.guid}' has null line at index {i}.", node, ref warnings);
                continue;
            }

            if (line.speaker != null && !HasAnyCharacterSprite(line.speaker))
                LogWarning($"DialogueNode '{node.guid}' uses character '{line.speaker.name}' without base sprite/body.", line.speaker, ref warnings);
        }
    }

    private static void ValidateChoiceNode(ChoiceNode node, ref int warnings)
    {
        if (node == null || node.options == null)
            return;

        for (int i = 0; i < node.options.Count; i++)
        {
            ChoiceOption option = node.options[i];
            if (option == null)
            {
                LogWarning($"ChoiceNode '{node.guid}' has null option at index {i}.", node, ref warnings);
                continue;
            }

            if (option.isPremium && option.premiumCost <= 0)
                LogWarning($"ChoiceNode '{node.guid}' has premium option with non-positive cost at index {i}.", node, ref warnings);
        }
    }

    private static bool HasAnyCharacterSprite(CharacterData character)
    {
        return character != null &&
               (character.defaultSprite != null ||
                character.bodySprite != null ||
                character.GetBaseSprite() != null);
    }

    private static IEnumerable<T> LoadAssets<T>() where T : Object
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                yield return asset;
        }
    }

    private static string GetAssetLabel(Object asset)
    {
        if (asset == null)
            return "<null>";

        string path = AssetDatabase.GetAssetPath(asset);
        return string.IsNullOrEmpty(path) ? asset.name : path;
    }

    private static void LogError(string message, Object context, ref int errors)
    {
        errors++;
        Debug.LogError("[ContentValidator] " + message, context);
    }

    private static void LogWarning(string message, Object context, ref int warnings)
    {
        warnings++;
        Debug.LogWarning("[ContentValidator] " + message, context);
    }
}
