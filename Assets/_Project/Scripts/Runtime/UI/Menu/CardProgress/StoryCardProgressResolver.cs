using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct StoryCardProgressData
{
    public StoryCardProgressData(string chapterLabel, int percent)
    {
        ChapterLabel = chapterLabel ?? "";
        Percent = Mathf.Clamp(percent, 0, 100);
    }

    public string ChapterLabel { get; }
    public int Percent { get; }
}

public static class StoryCardProgressResolver
{
    private readonly struct JsonDocumentCacheEntry
    {
        public JsonDocumentCacheEntry(TextAsset asset, StoryJsonDocument document, bool success)
        {
            Asset = asset;
            Document = document;
            Success = success;
        }

        public TextAsset Asset { get; }
        public StoryJsonDocument Document { get; }
        public bool Success { get; }
    }

    private static readonly Dictionary<int, JsonDocumentCacheEntry> JsonDocumentCache =
        new Dictionary<int, JsonDocumentCacheEntry>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeCache()
    {
        JsonDocumentCache.Clear();
    }

    public static StoryCardProgressData Resolve(GameData game)
    {
        StoryData story = game != null ? game.Story : null;
        IReadOnlyList<ChapterData> chapters = story != null ? story.Chapters : null;
        if (chapters == null || chapters.Count == 0)
            return new StoryCardProgressData("", 0);

        string storyId = ResolveStoryId(story);
        SaveData save = ResolveLatestSave(story, storyId);
        int chapterIndex = ResolveChapterIndex(chapters, save);
        ChapterData chapter = chapters[Mathf.Clamp(chapterIndex, 0, chapters.Count - 1)];

        string label = BuildChapterLabel(chapter, chapterIndex);
        int percent = CalculateChapterPercent(chapter, save);
        return new StoryCardProgressData(label, percent);
    }

    public static int CalculateChapterPercent(ChapterData chapter, SaveData save)
    {
        if (chapter == null || save == null || string.IsNullOrEmpty(save.currentNodeGuid))
            return 0;

        StoryGraph runtimeGraph = ResolveActiveRuntimeGraph(save);
        if (runtimeGraph != null)
        {
            int runtimePercent = CalculateStoryGraphPercent(runtimeGraph, save);
            if (runtimePercent > 0)
                return runtimePercent;
        }

        if (TryCalculateJsonChapterPercent(chapter, save, out int jsonPercent))
            return jsonPercent;

        return CalculateStoryGraphPercent(chapter.Graph, save);
    }

    private static StoryGraph ResolveActiveRuntimeGraph(SaveData save)
    {
        StoryManager manager = StoryManager.Instance;
        StoryGraph graph = manager != null ? manager.storyGraph : null;
        if (graph == null || graph.nodes == null || save == null)
            return null;

        string savedStoryId = SaveDataSanitizer.SanitizeIdentifier(save.storyId);
        string managerStoryId = manager != null
            ? SaveDataSanitizer.SanitizeIdentifier(manager.CurrentStoryId)
            : "";
        if (!string.IsNullOrEmpty(savedStoryId) &&
            !string.IsNullOrEmpty(managerStoryId) &&
            !string.Equals(savedStoryId, managerStoryId, StringComparison.Ordinal))
        {
            return null;
        }

        string targetGuid = SaveDataSanitizer.SanitizeIdentifier(save.currentNodeGuid);
        foreach (XNode.Node node in graph.nodes)
        {
            if (node is BaseStoryNode storyNode && string.Equals(
                SaveDataSanitizer.SanitizeIdentifier(storyNode.guid),
                targetGuid,
                StringComparison.Ordinal))
            {
                return graph;
            }
        }

        return null;
    }

    private static bool TryCalculateJsonChapterPercent(ChapterData chapter, SaveData save, out int percent)
    {
        percent = 0;
        if (!TryGetJsonDocument(chapter, out StoryJsonDocument document) ||
            document.nodes == null || document.nodes.Count == 0)
        {
            return false;
        }

        string targetGuid = SaveDataSanitizer.SanitizeIdentifier(save.currentNodeGuid);
        int validNodeCount = 0;
        int currentIndex = -1;
        StoryJsonNode currentNode = null;

        for (int i = 0; i < document.nodes.Count; i++)
        {
            StoryJsonNode node = document.nodes[i];
            if (node == null)
                continue;

            string nodeId = SaveDataSanitizer.SanitizeIdentifier(node.id);
            if (string.IsNullOrEmpty(nodeId))
                continue;

            if (currentIndex < 0 && string.Equals(nodeId, targetGuid, StringComparison.Ordinal))
            {
                currentIndex = validNodeCount;
                currentNode = node;
            }

            validNodeCount++;
        }

        if (currentIndex < 0 || validNodeCount == 0)
            return false;

        if (validNodeCount == 1)
        {
            percent = 100;
            return true;
        }

        float position = currentIndex + ResolveJsonDialogueFraction(currentNode, save.currentDialogueLineIndex);
        percent = Mathf.Clamp(Mathf.RoundToInt(position * 100f / (validNodeCount - 1)), 0, 100);

        // Если сохранение уже существует и текущий node найден, карточка не должна
        // выглядеть как «история ни разу не запускалась» из-за округления 0.x% -> 0%.
        percent = Mathf.Max(1, percent);
        return true;
    }

    private static int CalculateStoryGraphPercent(StoryGraph graph, SaveData save)
    {
        if (graph == null || graph.nodes == null || save == null)
            return 0;

        string targetGuid = SaveDataSanitizer.SanitizeIdentifier(save.currentNodeGuid);
        int nodeCount = 0;
        int currentIndex = -1;
        BaseStoryNode currentNode = null;

        foreach (XNode.Node node in graph.nodes)
        {
            if (!(node is BaseStoryNode storyNode))
                continue;

            if (currentIndex < 0 && string.Equals(
                SaveDataSanitizer.SanitizeIdentifier(storyNode.guid),
                targetGuid,
                StringComparison.Ordinal))
            {
                currentIndex = nodeCount;
                currentNode = storyNode;
            }

            nodeCount++;
        }

        if (currentIndex < 0 || nodeCount == 0)
            return 0;

        if (nodeCount == 1)
            return 100;

        float position = currentIndex + ResolveDialogueFraction(currentNode, save.currentDialogueLineIndex);
        int percent = Mathf.Clamp(Mathf.RoundToInt(position * 100f / (nodeCount - 1)), 0, 100);
        return Mathf.Max(1, percent);
    }

    private static SaveData ResolveLatestSave(StoryData story, string storyId)
    {
        SaveData local = null;
        if (SaveManager.Instance != null)
        {
            int slot = StorySaveSlotSelection.GetSelectedSlot(storyId);
            if (!SaveManager.Instance.TryGetCachedSaveForStory(storyId, slot, out local))
                local = SaveManager.Instance.LoadForStorySlotIfExists(storyId, slot);
        }

        SaveData latest = local;
        latest = ChooseLatest(latest, NetworkManager.GetPendingProgressSnapshot(storyId), story, storyId);
        latest = ChooseLatest(latest, NetworkManager.BuildLoadedProgressSnapshot(), story, storyId);
        return latest;
    }

    private static SaveData ChooseLatest(SaveData current, SaveData candidate, StoryData story, string storyId)
    {
        if (!BelongsToStory(candidate, story, storyId))
            return current;
        if (current == null)
            return candidate;

        bool currentHasPosition = HasUsablePosition(current);
        bool candidateHasPosition = HasUsablePosition(candidate);

        // Для полосы прогресса snapshot без позиции не имеет права затереть
        // нормальное локальное сохранение только потому, что сервер поставил ему
        // более свежий timestamp.
        if (candidateHasPosition != currentHasPosition)
            return candidateHasPosition ? candidate : current;

        bool currentHasTime = TryGetTime(current, out DateTime currentTime);
        bool candidateHasTime = TryGetTime(candidate, out DateTime candidateTime);
        if (candidateHasTime && (!currentHasTime || candidateTime > currentTime))
            return candidate;

        return current;
    }

    private static bool HasUsablePosition(SaveData save)
    {
        return save != null && !string.IsNullOrEmpty(SaveDataSanitizer.SanitizeIdentifier(save.currentNodeGuid));
    }

    private static bool BelongsToStory(SaveData save, StoryData story, string storyId)
    {
        if (save == null)
            return false;

        string savedStoryId = SaveDataSanitizer.SanitizeIdentifier(save.storyId);
        if (!string.IsNullOrEmpty(savedStoryId))
            return string.Equals(savedStoryId, storyId, StringComparison.Ordinal);

        IReadOnlyList<ChapterData> chapters = story != null ? story.Chapters : null;
        if (chapters == null)
            return false;

        for (int i = 0; i < chapters.Count; i++)
        {
            if (MatchesChapter(chapters[i], save.chapterId) || MatchesChapter(chapters[i], save.episodeId))
                return true;
            if (ContainsNode(chapters[i], save.currentNodeGuid))
                return true;
        }

        return false;
    }

    private static int ResolveChapterIndex(IReadOnlyList<ChapterData> chapters, SaveData save)
    {
        if (save != null)
        {
            for (int i = 0; i < chapters.Count; i++)
            {
                if (MatchesChapter(chapters[i], save.chapterId) || MatchesChapter(chapters[i], save.episodeId) ||
                    ContainsNode(chapters[i], save.currentNodeGuid))
                    return i;
            }

            return Mathf.Clamp(save.currentChapterIndex, 0, chapters.Count - 1);
        }

        return 0;
    }

    private static bool MatchesChapter(ChapterData chapter, string id)
    {
        id = SaveDataSanitizer.SanitizeIdentifier(id);
        if (chapter == null || string.IsNullOrEmpty(id))
            return false;

        if (string.Equals(SaveDataSanitizer.SanitizeIdentifier(chapter.ChapterId), id, StringComparison.Ordinal))
            return true;

        if (chapter.Graph != null && string.Equals(
            SaveDataSanitizer.SanitizeIdentifier(chapter.Graph.episodeId), id, StringComparison.Ordinal))
        {
            return true;
        }

        if (TryGetJsonDocument(chapter, out StoryJsonDocument document))
        {
            string jsonChapterId = SaveDataSanitizer.SanitizeIdentifier(document.chapterId);
            string jsonEpisodeId = SaveDataSanitizer.SanitizeIdentifier(document.episodeId);
            return string.Equals(jsonChapterId, id, StringComparison.Ordinal) ||
                   string.Equals(jsonEpisodeId, id, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool ContainsNode(ChapterData chapter, string guid)
    {
        guid = SaveDataSanitizer.SanitizeIdentifier(guid);
        if (chapter == null || string.IsNullOrEmpty(guid))
            return false;

        if (TryGetJsonDocument(chapter, out StoryJsonDocument document) && document.nodes != null)
        {
            for (int i = 0; i < document.nodes.Count; i++)
            {
                StoryJsonNode node = document.nodes[i];
                if (node != null && string.Equals(
                    SaveDataSanitizer.SanitizeIdentifier(node.id), guid, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        if (chapter.Graph == null || chapter.Graph.nodes == null)
            return false;

        foreach (XNode.Node node in chapter.Graph.nodes)
        {
            if (node is BaseStoryNode storyNode && string.Equals(
                SaveDataSanitizer.SanitizeIdentifier(storyNode.guid), guid, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool TryGetJsonDocument(ChapterData chapter, out StoryJsonDocument document)
    {
        document = null;
        TextAsset jsonGraph = chapter != null ? chapter.JsonGraph : null;
        if (jsonGraph == null || string.IsNullOrWhiteSpace(jsonGraph.text))
            return false;

        int key = jsonGraph.GetInstanceID();
        if (JsonDocumentCache.TryGetValue(key, out JsonDocumentCacheEntry cached) &&
            cached.Asset == jsonGraph)
        {
            document = cached.Document;
            return cached.Success;
        }

        bool success = StoryJsonConverter.TryParseDocument(
            jsonGraph.text,
            out StoryJsonDocument parsed,
            out _) &&
            parsed != null;

        document = parsed;
        JsonDocumentCache[key] = new JsonDocumentCacheEntry(jsonGraph, parsed, success);
        return success;
    }

    private static float ResolveDialogueFraction(BaseStoryNode node, int lineIndex)
    {
        if (!(node is DialogueNode dialogue) || dialogue.lines == null || dialogue.lines.Count == 0)
            return 0f;

        return Mathf.Clamp01((lineIndex + 1f) / dialogue.lines.Count);
    }

    private static float ResolveJsonDialogueFraction(StoryJsonNode node, int lineIndex)
    {
        if (node == null || node.lines == null || node.lines.Count == 0)
            return 0f;

        return Mathf.Clamp01((lineIndex + 1f) / node.lines.Count);
    }

    private static string BuildChapterLabel(ChapterData chapter, int index)
    {
        string title = chapter != null ? (chapter.ChapterName ?? "").Trim() : "";
        if (title.StartsWith("Глава", StringComparison.OrdinalIgnoreCase))
            return title;
        return string.IsNullOrEmpty(title) ? $"Глава {index + 1}" : $"Глава {index + 1}. {title}";
    }

    private static string ResolveStoryId(StoryData story)
    {
        string id = story != null ? SaveDataSanitizer.SanitizeIdentifier(story.StoryId) : "";
        return !string.IsNullOrEmpty(id) ? id : SaveDataSanitizer.SanitizeIdentifier(story != null ? story.name : "");
    }

    private static bool TryGetTime(SaveData save, out DateTime time)
    {
        return DateTime.TryParse(save != null ? save.savedAtIso : "", null,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out time);
    }
}
