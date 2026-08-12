using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct StorySaveChapterContext
{
    public StorySaveChapterContext(
        ChapterData chapter, int flatIndex, int seasonNumber, int chapterNumber)
    {
        Chapter = chapter;
        FlatIndex = Mathf.Max(0, flatIndex);
        SeasonNumber = Mathf.Max(1, seasonNumber);
        ChapterNumber = Mathf.Max(1, chapterNumber);
    }

    public ChapterData Chapter { get; }
    public int FlatIndex { get; }
    public int SeasonNumber { get; }
    public int ChapterNumber { get; }
}

public static class StorySaveChapterResolver
{
    public static StorySaveChapterContext Resolve(StoryData story, SaveData save)
    {
        IReadOnlyList<ChapterData> chapters =
            story != null ? story.Chapters : null;

        if (chapters == null || chapters.Count == 0)
            return new StorySaveChapterContext(null, 0, 1, 1);

        int index = FindChapterIndex(chapters, save);
        ChapterData chapter =
            chapters[Mathf.Clamp(index, 0, chapters.Count - 1)];

        ResolveNumbers(
            story, chapter, index, save,
            out int seasonNumber, out int chapterNumber);

        return new StorySaveChapterContext(
            chapter, index, seasonNumber, chapterNumber);
    }

    public static int CalculatePercent(ChapterData chapter, SaveData save)
    {
        if (chapter == null || save == null)
            return 0;

        if (chapter.Graph != null)
            return StoryCardProgressResolver.CalculateChapterPercent(chapter, save);

        if (!TryReadJson(chapter, out StoryJsonDocument document) ||
            document.nodes == null || document.nodes.Count <= 1)
        {
            return 0;
        }

        string current =
            SaveDataSanitizer.SanitizeIdentifier(save.currentNodeGuid);
        int index = document.nodes.FindIndex(node =>
            node != null &&
            (MatchesId(node.id, current) || MatchesId(node.guid, current)));

        if (index < 0)
            return 0;

        StoryJsonNode node = document.nodes[index];
        float fraction = 0f;
        if (node.lines != null && node.lines.Count > 0)
        {
            fraction = Mathf.Clamp01(
                (save.currentDialogueLineIndex + 1f) / node.lines.Count);
        }

        return Mathf.Clamp(
            Mathf.RoundToInt(
                (index + fraction) * 100f / (document.nodes.Count - 1)),
            0, 100);
    }

    public static bool TryReadJson(
        ChapterData chapter, out StoryJsonDocument document)
    {
        document = null;
        TextAsset json = chapter != null ? chapter.JsonGraph : null;

        return json != null &&
               !string.IsNullOrWhiteSpace(json.text) &&
               StoryJsonConverter.TryParseDocument(
                   json.text, out document, out _);
    }

    private static int FindChapterIndex(
        IReadOnlyList<ChapterData> chapters, SaveData save)
    {
        if (save == null)
            return 0;

        for (int i = 0; i < chapters.Count; i++)
        {
            if (MatchesSave(chapters[i], save))
                return i;
        }

        return Mathf.Clamp(
            save.currentChapterIndex, 0, chapters.Count - 1);
    }

    private static bool MatchesSave(ChapterData chapter, SaveData save)
    {
        if (chapter == null || save == null)
            return false;

        if (MatchesId(chapter.ChapterId, save.chapterId) ||
            MatchesId(chapter.ChapterId, save.episodeId))
        {
            return true;
        }

        if (chapter.Graph != null &&
            MatchesId(chapter.Graph.episodeId, save.episodeId))
        {
            return true;
        }

        if (!TryReadJson(chapter, out StoryJsonDocument document))
            return false;

        return MatchesId(document.chapterId, save.chapterId) ||
               MatchesId(document.episodeId, save.episodeId);
    }

    private static void ResolveNumbers(
        StoryData story,
        ChapterData chapter,
        int flatIndex,
        SaveData save,
        out int seasonNumber,
        out int chapterNumber)
    {
        seasonNumber = save != null
            ? Mathf.Max(1, save.currentSeasonIndex + 1)
            : 1;
        chapterNumber = Mathf.Max(1, flatIndex + 1);

        IReadOnlyList<SeasonData> seasons =
            story != null ? story.Seasons : null;
        if (seasons == null || chapter == null)
            return;

        for (int s = 0; s < seasons.Count; s++)
        {
            IReadOnlyList<ChapterData> items =
                seasons[s] != null ? seasons[s].Chapters : null;
            if (items == null)
                continue;

            for (int c = 0; c < items.Count; c++)
            {
                ChapterData item = items[c];
                bool same = ReferenceEquals(item, chapter) ||
                    MatchesId(
                        item != null ? item.ChapterId : "",
                        chapter.ChapterId);

                if (!same)
                    continue;

                seasonNumber = s + 1;
                chapterNumber = c + 1;
                return;
            }
        }
    }

    private static bool MatchesId(string left, string right)
    {
        left = SaveDataSanitizer.SanitizeIdentifier(left);
        right = SaveDataSanitizer.SanitizeIdentifier(right);

        return !string.IsNullOrEmpty(left) &&
               string.Equals(left, right, StringComparison.Ordinal);
    }
}
