#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class StoryGraphAssetMatcher
{
    public static StoryGraphAssetMatchReport MatchAndApply(StoryGraph graph, ProjectAssetContext context)
    {
        if (graph == null)
            throw new ArgumentNullException(nameof(graph));

        var report = new StoryGraphAssetMatchReport();
        ProjectAssetContext safeContext = context ?? new ProjectAssetContext();

        foreach (var node in graph.nodes)
        {
            if (node == null)
                continue;

            if (node is SceneSetupNode sceneNode)
                MatchScene(sceneNode, safeContext, report);

            if (node is DialogueNode dialogueNode &&
                MatchCharacters(dialogueNode.activeCharacters, safeContext, report, "DialogueNode"))
            {
                EditorUtility.SetDirty(dialogueNode);
            }

            if (node is ChoiceNode choiceNode &&
                MatchCharacters(choiceNode.activeCharacters, safeContext, report, "ChoiceNode"))
            {
                EditorUtility.SetDirty(choiceNode);
            }
        }

        return report;
    }

    private static void MatchScene(
        SceneSetupNode node,
        ProjectAssetContext context,
        StoryGraphAssetMatchReport report)
    {
        bool dirty = false;
        if (node.sceneData == null)
        {
            node.sceneData = ScriptableObject.CreateInstance<SceneSetupData>();
            node.sceneData.name = "SceneData_" + node.name;

            string graphPath = AssetDatabase.GetAssetPath(node.graph);
            if (!string.IsNullOrEmpty(graphPath))
            {
                AssetDatabase.AddObjectToAsset(node.sceneData, graphPath);
            }
            else
            {
                Debug.LogWarning($"StoryGraphAssetMatcher: не удалось получить путь к графу для SceneSetupData ({node.name}).");
            }

            dirty = true;
        }

        MatchBackground(node, context, report, ref dirty);
        MatchMusic(node, context, report, ref dirty);

        if (!dirty)
            return;

        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(node.sceneData);
    }

    private static void MatchBackground(
        SceneSetupNode node,
        ProjectAssetContext context,
        StoryGraphAssetMatchReport report,
        ref bool dirty)
    {
        if (node.sceneData.background == null && !string.IsNullOrEmpty(node.suggestedBackground))
        {
            Sprite sprite = FindSprite(node.suggestedBackground, context.backgroundNames);
            if (sprite != null)
            {
                node.sceneData.background = sprite;
                report.Add("SceneSetupNode", "background", node.suggestedBackground, StoryGraphAssetMatchReport.Status.Applied);
                dirty = true;
            }
            else
            {
                report.Add("SceneSetupNode", "background", node.suggestedBackground, StoryGraphAssetMatchReport.Status.NotFound);
            }
        }
        else if (node.sceneData.background != null)
        {
            report.Add("SceneSetupNode", "background", node.sceneData.background.name, StoryGraphAssetMatchReport.Status.Skipped);
        }
    }

    private static void MatchMusic(
        SceneSetupNode node,
        ProjectAssetContext context,
        StoryGraphAssetMatchReport report,
        ref bool dirty)
    {
        if (node.sceneData.music == null && !string.IsNullOrEmpty(node.suggestedMusic))
        {
            AudioClip clip = FindAudio(node.suggestedMusic, context.musicNames);
            if (clip != null)
            {
                node.sceneData.music = clip;
                report.Add("SceneSetupNode", "music", node.suggestedMusic, StoryGraphAssetMatchReport.Status.Applied);
                dirty = true;
            }
            else
            {
                report.Add("SceneSetupNode", "music", node.suggestedMusic, StoryGraphAssetMatchReport.Status.NotFound);
            }
        }
        else if (node.sceneData.music != null)
        {
            report.Add("SceneSetupNode", "music", node.sceneData.music.name, StoryGraphAssetMatchReport.Status.Skipped);
        }
    }

    private static bool MatchCharacters(
        List<DialogueCharacterEntry> entries,
        ProjectAssetContext context,
        StoryGraphAssetMatchReport report,
        string nodeType)
    {
        if (entries == null)
            return false;

        bool dirty = false;

        foreach (DialogueCharacterEntry entry in entries)
        {
            if (entry.character != null)
            {
                report.Add(nodeType, "character", entry.character.characterName, StoryGraphAssetMatchReport.Status.Skipped);
                continue;
            }

            if (string.IsNullOrEmpty(entry.speakerNameHint))
                continue;

            CharacterData character = FindCharacter(entry.speakerNameHint, context);
            if (character != null)
            {
                entry.character = character;
                report.Add(nodeType, "character", entry.speakerNameHint, StoryGraphAssetMatchReport.Status.Applied);
                dirty = true;
            }
            else
            {
                report.Add(nodeType, "character", entry.speakerNameHint, StoryGraphAssetMatchReport.Status.NotFound);
            }
        }

        return dirty;
    }

    private static Sprite FindSprite(string hint, List<string> knownNames)
    {
        string bestMatch = FindBestName(hint, knownNames);
        if (bestMatch == null)
            return null;

        foreach (string guid in AssetDatabase.FindAssets($"{bestMatch} t:Sprite"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private static AudioClip FindAudio(string hint, List<string> knownNames)
    {
        string bestMatch = FindBestName(hint, knownNames);
        if (bestMatch == null)
            return null;

        foreach (string guid in AssetDatabase.FindAssets($"{bestMatch} t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
                return clip;
        }

        return null;
    }

    private static CharacterData FindCharacter(string hint, ProjectAssetContext context)
    {
        if (string.IsNullOrWhiteSpace(hint))
            return null;

        foreach (ProjectAssetContext.CharacterEntry entry in context.characters)
        {
            if (string.Equals(entry.characterName, hint, StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<CharacterData>(entry.assetPath);
        }

        foreach (ProjectAssetContext.CharacterEntry entry in context.characters)
        {
            if (string.IsNullOrEmpty(entry.characterName))
                continue;

            if (entry.characterName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0 ||
                hint.IndexOf(entry.characterName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AssetDatabase.LoadAssetAtPath<CharacterData>(entry.assetPath);
            }
        }

        return null;
    }

    private static string FindBestName(string hint, List<string> names)
    {
        if (string.IsNullOrWhiteSpace(hint) || names == null || names.Count == 0)
            return null;

        string exact = names.FirstOrDefault(name => string.Equals(name, hint, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
            return exact;

        string contains = names.FirstOrDefault(name => name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0);
        if (contains != null)
            return contains;

        string[] words = hint.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 3)
            .ToArray();

        foreach (string word in words)
        {
            string match = names.FirstOrDefault(name => name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
            if (match != null)
                return match;
        }

        return null;
    }
}
#endif
