using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DialogueIdentityValidationMenu
{
    const string MenuPath = "Tools/Novel Template/Validate Dialogue Identity";

    [MenuItem(MenuPath)]
    public static void ValidateDialogueIdentity()
    {
        DialogueIdentityValidationReport report = new DialogueIdentityValidationReport();
        var scannedJson = new HashSet<int>();

        foreach (StoryData story in LoadAssets<StoryData>())
        {
            if (story == null)
                continue;

            IReadOnlyList<ChapterData> chapters = story.Chapters;
            if (chapters == null)
                continue;

            for (int i = 0; i < chapters.Count; i++)
            {
                ChapterData chapter = chapters[i];
                if (chapter == null)
                    continue;

                if (chapter.Graph != null)
                    report.AddRange(DialogueIdentityValidator.ValidateGraph(chapter.Graph, story.StoryId, chapter.ChapterId));

                if (chapter.JsonGraph != null)
                {
                    scannedJson.Add(chapter.JsonGraph.GetInstanceID());
                    report.AddRange(ValidateJsonAsset(chapter.JsonGraph, story.StoryId, chapter.ChapterId));
                }
            }
        }

        foreach (StoryGraph graph in LoadAssets<StoryGraph>())
        {
            if (graph != null)
                report.AddRange(DialogueIdentityValidator.ValidateGraph(graph, AssetDatabase.GetAssetPath(graph), graph.episodeId));
        }

        foreach (TextAsset textAsset in LoadAssets<TextAsset>())
        {
            if (textAsset == null || scannedJson.Contains(textAsset.GetInstanceID()) || !LooksLikeStoryJson(textAsset.text))
                continue;

            report.AddRange(ValidateJsonAsset(textAsset, AssetDatabase.GetAssetPath(textAsset), ""));
        }

        foreach (DialogueIdentityValidationIssue issue in report.Issues)
            Debug.LogWarning("[DialogueIdentity] " + issue);

        string summary = "Dialogue identity validation finished: " +
                         report.ErrorCount + " errors, " +
                         report.WarningCount + " warnings.";

        if (report.ErrorCount > 0)
            Debug.LogError("[DialogueIdentity] " + summary);
        else if (report.WarningCount > 0)
            Debug.LogWarning("[DialogueIdentity] " + summary);
        else
            Debug.Log("[DialogueIdentity] " + summary);

        EditorUtility.DisplayDialog("Dialogue Identity", summary, "OK");
    }

    static DialogueIdentityValidationReport ValidateJsonAsset(TextAsset asset, string storyId, string chapterId)
    {
        if (asset == null)
            return new DialogueIdentityValidationReport();

        if (!StoryJsonConverter.TryParseDocument(asset.text, out StoryJsonDocument document, out string reason))
        {
            var report = new DialogueIdentityValidationReport();
            report.Add(new DialogueIdentityValidationIssue
            {
                Severity = DialogueIdentityIssueSeverity.Warning,
                StoryId = storyId,
                ChapterId = chapterId,
                Message = "Story JSON could not be parsed for identity validation: " + reason
            });
            return report;
        }

        if (string.IsNullOrWhiteSpace(document.storyId))
            document.storyId = storyId;
        if (string.IsNullOrWhiteSpace(document.chapterId))
            document.chapterId = chapterId;

        return DialogueIdentityValidator.ValidateJsonDocument(document, AssetDatabase.GetAssetPath(asset));
    }

    static bool LooksLikeStoryJson(string text)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               text.IndexOf("\"nodes\"", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
               text.IndexOf("\"version\"", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static IEnumerable<T> LoadAssets<T>() where T : Object
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
}
