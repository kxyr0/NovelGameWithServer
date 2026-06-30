using System;
using System.Collections.Generic;

public partial class StoryManager
{
    StorySeasonRewardService _storySeasonRewardService;
    string currentSeasonCompletionRunId;
    StorySeasonRewardResult lastSeasonRewardResult;

    public StorySeasonRewardResult LastSeasonRewardResult => lastSeasonRewardResult;

    StorySeasonRewardService SeasonRewardService
    {
        get
        {
            if (_storySeasonRewardService == null)
                _storySeasonRewardService = new StorySeasonRewardService();
            return _storySeasonRewardService;
        }
    }

    void StartNewSeasonRewardRunForCurrentChapter(string source)
    {
        currentSeasonCompletionRunId = "";

        if (!TryResolveSeasonForChapterIndex(currentChapter, out int seasonNumber, out _, out _, out _))
            return;

        StorySeasonRunResult run = SeasonRewardService.StartNewCompletionRun(CurrentStoryId, seasonNumber, source);
        currentSeasonCompletionRunId = run != null ? run.CompletionRunId : "";
    }

    void RestoreOrStartSeasonRewardRunForCurrentChapter(string source)
    {
        currentSeasonCompletionRunId = "";

        if (!TryResolveSeasonForChapterIndex(currentChapter, out int seasonNumber, out _, out _, out _))
            return;

        StorySeasonRunResult run = SeasonRewardService.RestoreOrStartCompletionRun(CurrentStoryId, seasonNumber, source);
        currentSeasonCompletionRunId = run != null ? run.CompletionRunId : "";
    }

    StorySeasonRewardResult TryApplySeasonCompletionRewardForCompletedChapter(int completedChapterIndex)
    {
        lastSeasonRewardResult = null;

        if (!TryResolveSeasonForChapterIndex(
                completedChapterIndex,
                out int seasonNumber,
                out string seasonId,
                out int chapterIndexInSeason,
                out int seasonChapterCount))
        {
            return null;
        }

        if (seasonChapterCount <= 0 || chapterIndexInSeason != seasonChapterCount - 1)
            return null;

        if (string.IsNullOrEmpty(currentSeasonCompletionRunId))
            RestoreOrStartSeasonRewardRunForCurrentChapter(nameof(TryApplySeasonCompletionRewardForCompletedChapter));

        lastSeasonRewardResult = SeasonRewardService.CompleteSeason(new StorySeasonCompletionRequest
        {
            StoryId = CurrentStoryId,
            SeasonNumber = seasonNumber,
            SeasonId = seasonId,
            CompletionRunId = currentSeasonCompletionRunId
        });

        return lastSeasonRewardResult;
    }

    bool TryResolveSeasonForChapterIndex(
        int chapterIndex,
        out int seasonNumber,
        out string seasonId,
        out int chapterIndexInSeason,
        out int seasonChapterCount)
    {
        seasonNumber = 0;
        seasonId = "";
        chapterIndexInSeason = -1;
        seasonChapterCount = 0;

        IReadOnlyList<ChapterData> chapters = GetStoryChapters();
        if (storyData == null || chapters == null || chapterIndex < 0 || chapterIndex >= chapters.Count)
            return false;

        ChapterData chapter = chapters[chapterIndex];
        if (chapter == null)
            return false;

        if (storyData.seasons != null && storyData.seasons.Count > 0)
        {
            for (int seasonIndex = 0; seasonIndex < storyData.seasons.Count; seasonIndex++)
            {
                SeasonData season = storyData.seasons[seasonIndex];
                if (season == null || season.chapters == null || season.chapters.Count == 0)
                    continue;

                for (int localChapterIndex = 0; localChapterIndex < season.chapters.Count; localChapterIndex++)
                {
                    if (!ChapterMatches(season.chapters[localChapterIndex], chapter))
                        continue;

                    seasonNumber = seasonIndex + 1;
                    seasonId = ResolveSeasonId(season, seasonNumber);
                    chapterIndexInSeason = localChapterIndex;
                    seasonChapterCount = season.chapters.Count;
                    return true;
                }
            }
        }

        seasonNumber = 1;
        seasonId = ResolveSeasonId(null, seasonNumber);
        chapterIndexInSeason = chapterIndex;
        seasonChapterCount = chapters.Count;
        return true;
    }

    bool ChapterMatches(ChapterData left, ChapterData right)
    {
        if (left == null || right == null)
            return false;

        if (ReferenceEquals(left, right))
            return true;

        if (!string.IsNullOrEmpty(left.chapterId) &&
            string.Equals(left.chapterId, right.chapterId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrEmpty(left.name) &&
               string.Equals(left.name, right.name, StringComparison.OrdinalIgnoreCase);
    }

    string ResolveSeasonId(SeasonData season, int seasonNumber)
    {
        string explicitSeasonId = SaveDataSanitizer.SanitizeIdentifier(season != null ? season.seasonId : "");
        if (!string.IsNullOrEmpty(explicitSeasonId))
            return explicitSeasonId;

        string storyId = SaveDataSanitizer.SanitizeIdentifier(CurrentStoryId);
        if (string.IsNullOrEmpty(storyId))
            return "season_" + Math.Max(1, seasonNumber);

        return storyId + "_season_" + Math.Max(1, seasonNumber);
    }
}
