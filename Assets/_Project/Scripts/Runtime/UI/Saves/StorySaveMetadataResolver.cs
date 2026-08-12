using System;
using System.Globalization;
using UnityEngine;

public readonly struct StorySaveDisplayMetadata
{
    public StorySaveDisplayMetadata(
        string storyTitle,
        string episodeTitle,
        string savedAtText,
        int seasonNumber,
        int chapterNumber,
        int chapterPercent)
    {
        StoryTitle = storyTitle ?? "";
        EpisodeTitle = episodeTitle ?? "";
        SavedAtText = savedAtText ?? "";
        SeasonNumber = Mathf.Max(1, seasonNumber);
        ChapterNumber = Mathf.Max(1, chapterNumber);
        ChapterPercent = Mathf.Clamp(chapterPercent, 0, 100);
    }

    public string StoryTitle { get; }
    public string EpisodeTitle { get; }
    public string SavedAtText { get; }
    public int SeasonNumber { get; }
    public int ChapterNumber { get; }
    public int ChapterPercent { get; }
}

public static class StorySaveMetadataResolver
{
    public static StorySaveDisplayMetadata Resolve(
        GameData data,
        SaveData save,
        string dateFormat)
    {
        StoryData story = data != null ? data.Story : null;
        StorySaveChapterContext chapter =
            StorySaveChapterResolver.Resolve(story, save);

        return new StorySaveDisplayMetadata(
            ResolveStoryTitle(data),
            ResolveEpisodeTitle(chapter.Chapter, save),
            FormatSavedAt(save, dateFormat),
            chapter.SeasonNumber,
            chapter.ChapterNumber,
            StorySaveChapterResolver.CalculatePercent(
                chapter.Chapter, save));
    }

    public static DateTime ResolveSavedAtUtc(SaveData save)
    {
        if (save != null &&
            DateTime.TryParse(
                save.savedAtIso,
                null,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal,
                out DateTime parsed))
        {
            return parsed.ToUniversalTime();
        }

        return DateTime.MinValue;
    }

    private static string ResolveStoryTitle(GameData data)
    {
        if (data == null)
            return "";

        if (!string.IsNullOrWhiteSpace(data.GameName))
            return data.GameName.Trim();

        StoryData story = data.Story;
        if (story != null &&
            !string.IsNullOrWhiteSpace(story.StoryName))
        {
            return story.StoryName.Trim();
        }

        return data.name;
    }

    private static string ResolveEpisodeTitle(
        ChapterData chapter,
        SaveData save)
    {
        string fallback =
            chapter != null ? (chapter.ChapterName ?? "").Trim() : "";

        if (StorySaveChapterResolver.TryReadJson(
                chapter, out StoryJsonDocument document) &&
            !string.IsNullOrWhiteSpace(document.title))
        {
            fallback = document.title.Trim();
        }

        string episodeId = save != null
            ? SaveDataSanitizer.SanitizeIdentifier(save.episodeId)
            : "";

        string title =
            NetworkManager.GetCatalogEpisodeTitle(episodeId, fallback);

        if (!string.IsNullOrWhiteSpace(title))
            return title.Trim();

        return !string.IsNullOrWhiteSpace(fallback)
            ? fallback
            : episodeId;
    }

    private static string FormatSavedAt(
        SaveData save,
        string dateFormat)
    {
        DateTime utc = ResolveSavedAtUtc(save);
        if (utc == DateTime.MinValue)
            return "";

        string format = string.IsNullOrWhiteSpace(dateFormat)
            ? "dd.MM.yyyy HH:mm"
            : dateFormat;

        return utc.ToLocalTime().ToString(format);
    }
}
