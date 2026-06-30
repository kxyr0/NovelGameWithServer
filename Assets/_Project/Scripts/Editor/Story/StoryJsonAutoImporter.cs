#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class StoryJsonAutoImporter : AssetPostprocessor
{
    private const string LogPrefix = "[StoryJsonAutoImporter]";
    private const string GeneratedFolderName = "StoryJsonGenerated";
    private static readonly HashSet<string> PendingJsonPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static bool _delayScheduled;
    private static bool _isImporting;

    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (_isImporting || importedAssets == null)
            return;

        foreach (string path in importedAssets)
        {
            if (IsJsonAssetPath(path))
                PendingJsonPaths.Add(path.Replace("\\", "/"));
        }

        if (PendingJsonPaths.Count == 0 || _delayScheduled)
            return;

        _delayScheduled = true;
        EditorApplication.delayCall += ProcessPendingJson;
    }

    [MenuItem("VN/Reimport Selected Story JSON")]
    public static void ReimportSelectedJson()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!IsJsonAssetPath(path))
        {
            Debug.LogWarning("[StoryJsonAutoImporter] Select a JSON TextAsset in Project.");
            return;
        }

        bool imported = TryAutoImport(path, out string message);
        LogImportResult(imported, message);
    }

    private static void ProcessPendingJson()
    {
        _delayScheduled = false;

        string[] paths = PendingJsonPaths.ToArray();
        PendingJsonPaths.Clear();

        if (paths.Length == 0)
            return;

        _isImporting = true;
        try
        {
            foreach (string path in paths)
            {
                bool imported = TryAutoImport(path, out string message);
                LogImportResult(imported, message);
            }
        }
        finally
        {
            _isImporting = false;
        }
    }

    public static bool TryAutoImport(string jsonPath, out string message)
    {
        message = "";
        jsonPath = (jsonPath ?? "").Replace("\\", "/");

        try
        {
            return TryAutoImportInternal(jsonPath, out message);
        }
        catch (Exception exception)
        {
            message = "Exception while auto-importing story JSON '" + jsonPath + "': " + exception.Message;
            Debug.LogException(exception);
            return false;
        }
    }

    private static bool TryAutoImportInternal(string jsonPath, out string message)
    {
        message = "";

        if (!IsJsonAssetPath(jsonPath))
            return false;

        var jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
        if (jsonAsset == null || string.IsNullOrWhiteSpace(jsonAsset.text))
            return false;

        if (!StoryJsonConverter.IsCanonicalJson(jsonAsset.text))
        {
            if (LooksLikeStoryJsonCandidate(jsonAsset.text))
                message = "JSON looks like a story file but is not valid story JSON. Required top-level fields: version and nodes. Path: " + jsonPath;

            return false;
        }

        if (!StoryJsonConverter.TryParseDocument(jsonAsset.text, out var document, out string parseReason))
        {
            message = "Story JSON was found, but cannot be parsed. Path: " + jsonPath + "\n" + parseReason;
            return false;
        }

        string storyId = FirstNonEmpty(document.storyId, "json_story");
        string chapterId = FirstNonEmpty(document.chapterId, document.episodeId, Path.GetFileNameWithoutExtension(jsonPath));
        string episodeId = FirstNonEmpty(document.episodeId, chapterId);
        string chapterTitle = FirstNonEmpty(StoryJsonConverter.SanitizeDisplayText(document.title), chapterId);

        Debug.Log(
            LogPrefix + " Import started. " +
            "json='" + jsonPath + "', storyId='" + storyId + "', chapterId='" + chapterId + "', episodeId='" + episodeId + "'.");

        var assetLibrary = FindNearestAssetLibrary(jsonPath);
        StoryJsonAssetResolver resolver = assetLibrary != null
            ? new StoryJsonAssetLibraryResolver(assetLibrary, new StoryJsonEditorAssetResolver())
            : new StoryJsonEditorAssetResolver();

        if (assetLibrary != null)
            Debug.Log(LogPrefix + " Using asset library: " + AssetDatabase.GetAssetPath(assetLibrary));
        else
            Debug.Log(LogPrefix + " No nearby StoryJsonAssetLibrary found for '" + jsonPath + "'. Editor fallback resolver will be used.");

        if (!StoryJsonConverter.TryBuildGraphWithReport(
                jsonAsset.text,
                episodeId,
                out var graph,
                out var report,
                resolver))
        {
            DestroyTransientGraph(graph);
            message = "Cannot build StoryGraph from JSON '" + jsonPath + "'.\n" + report.ToDisplayString();
            return false;
        }

        if (report.HasWarnings)
            Debug.LogWarning(LogPrefix + " Import warnings for '" + jsonPath + "':\n" + report.ToDisplayString());

        string storyFolder = EnsureFolder(GetGeneratedStoryFolder(jsonPath, storyId));
        string graphsFolder = EnsureFolder(storyFolder + "/Graphs");
        string chaptersFolder = EnsureFolder(storyFolder + "/Chapters");

        Debug.Log(
            LogPrefix + " Output folders ready. " +
            "storyFolder='" + storyFolder + "', graphsFolder='" + graphsFolder + "', chaptersFolder='" + chaptersFolder + "'.");

        graph.name = SafeFileName(chapterId) + "_JsonGraph";
        graph.episodeId = episodeId;

        string graphPath = graphsFolder + "/" + SafeFileName(chapterId) + "_JsonGraph.asset";
        var existingGraph = AssetDatabase.LoadAssetAtPath<StoryGraph>(graphPath);
        if (existingGraph != null)
        {
            Debug.Log(LogPrefix + " Replacing generated graph: " + graphPath);
            if (!AssetDatabase.DeleteAsset(graphPath))
            {
                DestroyTransientGraph(graph);
                message = "Cannot replace existing generated graph asset: " + graphPath;
                return false;
            }
        }

        AssetDatabase.CreateAsset(graph, graphPath);
        if (AssetDatabase.LoadAssetAtPath<StoryGraph>(graphPath) == null)
        {
            DestroyTransientGraph(graph);
            message = "Unity did not create StoryGraph asset at: " + graphPath;
            return false;
        }

        AddGraphSubAssets(graph, graphPath);
        Debug.Log(LogPrefix + " StoryGraph created: " + graphPath + " (" + graph.nodes.Count + " nodes).");

        var story = FindStoryData(storyId);
        string storyPath = storyFolder + "/" + SafeFileName(storyId) + "_Story.asset";
        if (story == null)
        {
            story = AssetDatabase.LoadAssetAtPath<StoryData>(storyPath);
            if (story == null)
            {
                story = ScriptableObject.CreateInstance<StoryData>();
                AssetDatabase.CreateAsset(story, storyPath);
                story.Configure(storyId, storyId, Array.Empty<ChapterData>());
                Debug.Log(LogPrefix + " StoryData created: " + storyPath);
            }
        }
        else
        {
            Debug.Log(LogPrefix + " Existing StoryData found: " + AssetDatabase.GetAssetPath(story));
        }

        if (story == null)
        {
            message = "Cannot create or find StoryData for storyId '" + storyId + "'. Expected path: " + storyPath;
            return false;
        }

        var chapter = FindExistingChapter(story, chapterId);
        if (chapter == null)
            chapter = AssetDatabase.LoadAssetAtPath<ChapterData>(chaptersFolder + "/" + SafeFileName(chapterId) + ".asset");
        if (chapter == null)
        {
            chapter = ScriptableObject.CreateInstance<ChapterData>();
            AssetDatabase.CreateAsset(chapter, chaptersFolder + "/" + SafeFileName(chapterId) + ".asset");
            Debug.Log(LogPrefix + " ChapterData created: " + AssetDatabase.GetAssetPath(chapter));
        }

        if (chapter == null)
        {
            message = "Cannot create or find ChapterData for chapterId '" + chapterId + "'.";
            return false;
        }

        chapter.Configure(
            chapterId,
            chapterTitle,
            graph,
            jsonAsset,
            assetLibrary,
            chapter.isPremium,
            chapter.unlockCost);
        EditorUtility.SetDirty(chapter);

        AddChapterToStoryData(story, chapter);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        message =
            "Auto-imported story JSON.\n" +
            "JSON: " + jsonPath + "\n" +
            "StoryData: " + AssetDatabase.GetAssetPath(story) + "\n" +
            "ChapterData: " + AssetDatabase.GetAssetPath(chapter) + "\n" +
            "StoryGraph: " + graphPath;
        if (report.HasWarnings)
            message += "\n" + report.ToDisplayString();

        return true;
    }

    private static void LogImportResult(bool imported, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (imported)
            Debug.Log(LogPrefix + " " + message);
        else
            Debug.LogError(LogPrefix + " " + message);
    }

    private static bool LooksLikeStoryJsonCandidate(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        return json.IndexOf("\"nodes\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
               json.IndexOf("\"storyId\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
               json.IndexOf("\"chapterId\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
               json.IndexOf("\"episodeId\"", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsJsonAssetPath(string path)
    {
        path = (path ?? "").Replace("\\", "/");
        return path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
               !path.Contains("/" + GeneratedFolderName + "/", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);
    }

    private static StoryData FindStoryData(string storyId)
    {
        if (string.IsNullOrWhiteSpace(storyId))
            return null;

        foreach (string guid in AssetDatabase.FindAssets("t:StoryData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var story = AssetDatabase.LoadAssetAtPath<StoryData>(path);
            if (story != null && string.Equals(story.storyId, storyId, StringComparison.OrdinalIgnoreCase))
                return story;
        }

        return null;
    }

    private static ChapterData FindExistingChapter(StoryData story, string chapterId)
    {
        if (story == null || story.chapters == null || string.IsNullOrWhiteSpace(chapterId))
            return null;

        return story.chapters.FirstOrDefault(chapter =>
            chapter != null &&
            string.Equals(chapter.chapterId, chapterId, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddChapterToStoryData(StoryData story, ChapterData chapter)
    {
        if (story == null || chapter == null)
            return;

        var chapters = story.chapters != null
            ? new List<ChapterData>(story.chapters)
            : new List<ChapterData>();

        if (!chapters.Contains(chapter))
            chapters.Add(chapter);

        story.Configure(story.storyId, story.storyName, chapters);
        EditorUtility.SetDirty(story);
    }

    private static StoryJsonAssetLibrary FindNearestAssetLibrary(string jsonPath)
    {
        string jsonFolder = GetAssetFolder(jsonPath);
        StoryJsonAssetLibrary best = null;
        int bestScore = -1;

        foreach (string guid in AssetDatabase.FindAssets("t:StoryJsonAssetLibrary"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var library = AssetDatabase.LoadAssetAtPath<StoryJsonAssetLibrary>(path);
            if (library == null)
                continue;

            int score = GetCommonFolderPrefixScore(jsonFolder, GetAssetFolder(path));
            if (score > bestScore)
            {
                best = library;
                bestScore = score;
            }
        }

        return bestScore > 1 ? best : null;
    }

    private static int GetCommonFolderPrefixScore(string left, string right)
    {
        string[] leftParts = (left ?? "").Split('/');
        string[] rightParts = (right ?? "").Split('/');
        int count = Mathf.Min(leftParts.Length, rightParts.Length);
        int score = 0;

        for (int i = 0; i < count; i++)
        {
            if (!string.Equals(leftParts[i], rightParts[i], StringComparison.OrdinalIgnoreCase))
                break;

            score++;
        }

        return score;
    }

    private static string GetGeneratedStoryFolder(string jsonPath, string storyId)
    {
        string jsonFolder = GetAssetFolder(jsonPath);
        if (string.IsNullOrWhiteSpace(jsonFolder))
            jsonFolder = "Assets";

        return jsonFolder + "/" + GeneratedFolderName + "/" + SafeFileName(storyId);
    }

    private static string GetAssetFolder(string assetPath)
    {
        string folder = Path.GetDirectoryName(assetPath ?? "");
        return string.IsNullOrWhiteSpace(folder) ? "" : folder.Replace("\\", "/");
    }

    private static void AddGraphSubAssets(StoryGraph graph, string graphPath)
    {
        if (graph?.nodes == null)
            return;

        foreach (var node in graph.nodes.OfType<BaseStoryNode>())
        {
            node.hideFlags = HideFlags.None;
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(node)))
                AssetDatabase.AddObjectToAsset(node, graphPath);

            if (node is SceneSetupNode scene && scene.sceneData != null)
                AddTransientSubAsset(scene.sceneData, graphPath, "SceneData_" + node.guid);

            if (node is DialogueNode dialogue)
                AddDialogueSubAssets(dialogue.activeCharacters, dialogue.lines, graphPath);

            if (node is ChoiceNode choice)
                AddDialogueSubAssets(choice.activeCharacters, choice.lines, graphPath);

            EditorUtility.SetDirty(node);
        }

        EditorUtility.SetDirty(graph);
    }

    private static void AddDialogueSubAssets(
        IEnumerable<DialogueCharacterEntry> activeCharacters,
        IEnumerable<DialogueLine> lines,
        string graphPath)
    {
        if (activeCharacters != null)
        {
            foreach (var entry in activeCharacters)
                AddTransientSubAsset(entry?.character, graphPath, entry?.character != null ? entry.character.name : "");
        }

        if (lines == null)
            return;

        foreach (var line in lines)
            AddTransientSubAsset(line?.speaker, graphPath, line?.speaker != null ? line.speaker.name : "");
    }

    private static void AddTransientSubAsset(UnityEngine.Object asset, string graphPath, string fallbackName)
    {
        if (asset == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
            return;

        asset.hideFlags = HideFlags.None;
        if (string.IsNullOrWhiteSpace(asset.name))
            asset.name = fallbackName;

        AssetDatabase.AddObjectToAsset(asset, graphPath);
        EditorUtility.SetDirty(asset);
    }

    private static void DestroyTransientGraph(StoryGraph graph)
    {
        if (graph == null)
            return;

        var nodes = graph.nodes != null
            ? graph.nodes.OfType<BaseStoryNode>().ToArray()
            : Array.Empty<BaseStoryNode>();

        foreach (var node in nodes)
        {
            if (node is SceneSetupNode scene && scene.sceneData != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(scene.sceneData)))
                UnityEngine.Object.DestroyImmediate(scene.sceneData);

            if (node is DialogueNode dialogue)
                DestroyTransientDialogueAssets(dialogue.activeCharacters, dialogue.lines);

            if (node is ChoiceNode choice)
                DestroyTransientDialogueAssets(choice.activeCharacters, choice.lines);

            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(node)))
                UnityEngine.Object.DestroyImmediate(node);
        }

        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(graph)))
            UnityEngine.Object.DestroyImmediate(graph);
    }

    private static void DestroyTransientDialogueAssets(
        IEnumerable<DialogueCharacterEntry> activeCharacters,
        IEnumerable<DialogueLine> lines)
    {
        if (activeCharacters != null)
        {
            foreach (var entry in activeCharacters)
                DestroyTransientAsset(entry?.character);
        }

        if (lines == null)
            return;

        foreach (var line in lines)
            DestroyTransientAsset(line?.speaker);
    }

    private static void DestroyTransientAsset(UnityEngine.Object asset)
    {
        if (asset != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
            UnityEngine.Object.DestroyImmediate(asset);
    }

    private static string EnsureFolder(string path)
    {
        path = (path ?? "").Replace("\\", "/").TrimEnd('/');
        if (AssetDatabase.IsValidFolder(path))
            return path;

        string[] parts = path.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
            throw new InvalidOperationException("Folder must be inside Assets: " + path);

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }

        return path;
    }

    private static string SafeFileName(string value)
    {
        value = string.IsNullOrWhiteSpace(value) ? "story_json" : value.Trim();

        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');

        return value.Replace(" ", "_");
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "";
    }
}
#endif
