using System;
using System.Collections.Generic;
using UnityEngine;

public static class StoryCatalogRuntimeDiagnostics
{
    private const string Prefix = "[STORY_RUNTIME_AUDIT]";

    public static void LogCatalog(GameCatalog catalog, string phase, UnityEngine.Object context = null)
    {
        string platform = Application.platform.ToString();
        string build = Debug.isDebugBuild ? "development" : "release";
        phase = string.IsNullOrWhiteSpace(phase) ? "unknown" : phase.Trim();

        if (catalog == null)
        {
            LogWarning($"{Prefix} platform={platform} build={build} phase={phase} catalog=<null> reason=GameCatalog_not_assigned", context);
            return;
        }

        IReadOnlyList<GameData> games = catalog.Games;
        int count = games != null ? games.Count : 0;
        Log($"{Prefix} platform={platform} build={build} phase={phase} catalog='{catalog.name}' entries={count}", context);

        for (int i = 0; i < count; i++)
            LogGame(games[i], i, phase, context);
    }

    public static string DescribeAvailability(GameData data)
    {
        if (data == null)
            return "GameData is null";

        if (data.Story == null)
            return "GameData.Story is null";

        if (data.ForceComingSoon)
            return "ForceComingSoon is enabled";

        IReadOnlyList<ChapterData> chapters = data.Story.Chapters;
        if (chapters == null || chapters.Count == 0)
            return "StoryData has no chapters";

        bool hasNonNullChapter = false;
        for (int i = 0; i < chapters.Count; i++)
        {
            ChapterData chapter = chapters[i];
            if (chapter == null)
                continue;

            hasNonNullChapter = true;
            if (HasLocalGraph(chapter))
                return "OK";
        }

        return hasNonNullChapter
            ? "No chapter contains StoryGraph or local JSON"
            : "All StoryData chapters are null";
    }

    private static void LogGame(GameData data, int index, string phase, UnityEngine.Object context)
    {
        if (data == null)
        {
            LogWarning($"{Prefix}[ENTRY] phase={phase} index={index} status=BROKEN gameData=<null> reason=Null_catalog_entry", context);
            return;
        }

        StoryData story = data.Story;
        string reason = DescribeAvailability(data);
        string status = data.CanStartStory ? "PLAYABLE" : "BLOCKED";
        int chapterCount = story != null && story.Chapters != null ? story.Chapters.Count : 0;
        string storyId = story != null
            ? FirstNonEmpty(story.StoryId, story.storyId, story.name)
            : "";

        string message =
            $"{Prefix}[ENTRY] phase={phase} index={index} status={status} " +
            $"gameData='{data.name}' gameName='{data.GameName}' storyAsset='{(story != null ? story.name : "<null>")}' " +
            $"storyId='{storyId}' chapters={chapterCount} forceComingSoon={data.ForceComingSoon} " +
            $"hasPlayableStory={data.HasPlayableStory} reason='{reason}'";

        if (data.CanStartStory)
            Log(message, context);
        else
            LogWarning(message, context);

        if (story == null || story.Chapters == null)
            return;

        for (int i = 0; i < story.Chapters.Count; i++)
        {
            ChapterData chapter = story.Chapters[i];
            if (chapter == null)
            {
                LogWarning($"{Prefix}[CHAPTER] storyId='{storyId}' index={i} status=BROKEN chapter=<null>", context);
                continue;
            }

            bool hasJson = chapter.JsonGraph != null && !string.IsNullOrWhiteSpace(chapter.JsonGraph.text);
            bool hasGraph = chapter.Graph != null;
            string chapterStatus = hasJson || hasGraph ? "LOCAL_OK" : "NO_LOCAL_GRAPH";
            string chapterMessage =
                $"{Prefix}[CHAPTER] storyId='{storyId}' index={i} status={chapterStatus} " +
                $"chapterAsset='{chapter.name}' chapterId='{chapter.ChapterId}' hasJson={hasJson} " +
                $"jsonBytes={(hasJson ? chapter.JsonGraph.text.Length : 0)} hasGraph={hasGraph} " +
                $"hasJsonAssetLibrary={chapter.JsonAssetLibrary != null}";

            if (hasJson || hasGraph)
                Log(chapterMessage, context);
            else
                LogWarning(chapterMessage, context);
        }
    }

    private static bool HasLocalGraph(ChapterData chapter)
    {
        return chapter != null &&
               (chapter.Graph != null ||
                (chapter.JsonGraph != null && !string.IsNullOrWhiteSpace(chapter.JsonGraph.text)));
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return "";
    }

    private static void Log(string message, UnityEngine.Object context)
    {
        if (context != null)
            Debug.Log(message, context);
        else
            Debug.Log(message);
    }

    private static void LogWarning(string message, UnityEngine.Object context)
    {
        if (context != null)
            Debug.LogWarning(message, context);
        else
            Debug.LogWarning(message);
    }
}
