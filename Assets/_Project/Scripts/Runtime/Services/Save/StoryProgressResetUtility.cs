using System;
using UnityEngine;

public static class StoryProgressResetUtility
{
    private const string StoryBoundaryPrefix = "VN_STORY_BOUNDARY_";
    private const string ForceFreshStartPrefix = "VN_FORCE_FRESH_START_";
    private const string EpisodeSummaryStatsSuffix = "episode_summary_stats";
    private const string EpisodeSummaryCandlesSuffix = "episode_summary_candles";
    private const string EpisodeSummaryHeartsSuffix = "episode_summary_hearts";
    private const string PreStorySetupDoneKey = "VN_PRE_STORY_SETUP_DONE";
    private const string WardrobeHeroSetupDoneKey = "VN_WARDROBE_HERO_SETUP_DONE";

    public static void ResetLocalProgress(StoryData storyData)
    {
        ResetLocalProgress(storyData, "");
    }

    public static void ResetStoryProgress(StoryData storyData)
    {
        ResetStoryProgress(storyData, "");
    }

    public static void ResetStoryProgress(StoryData storyData, string currentStoryId)
    {
        DeleteStoryScopedKeys(storyData, currentStoryId);
        DeleteChapterUnlockKeys(storyData);

        string storyId = ResolveStoryId(storyData, currentStoryId);
        if (!string.IsNullOrEmpty(storyId))
        {
            DeleteStorySaveFiles(storyData, currentStoryId);
            MarkForceFreshStart(storyId);
            GameState.Instance?.InitForStory(storyId);
        }

        StoryHistory.Instance?.Clear();
        SafeSavePrefs();
    }

    public static void ResetStoryRuntimeStateForFreshSlot(StoryData storyData, string currentStoryId)
    {
        DeleteStoryScopedKeys(storyData, currentStoryId);

        string storyId = ResolveStoryId(storyData, currentStoryId);
        if (!string.IsNullOrEmpty(storyId))
        {
            MarkForceFreshStart(storyId);
            GameState.Instance?.InitForStory(storyId);
        }

        PlayerAppearance.ApplyState(new HeroCustomizationState(), save: false, notify: true);
        StoryHistory.Instance?.Clear();
        SafeSavePrefs();
    }

    public static void ResetLocalProgress(StoryData storyData, string currentStoryId)
    {
        DeleteLegacyProgressKeys();
        DeleteStoryScopedKeys(storyData, currentStoryId);
        DeleteChapterUnlockKeys(storyData);

        HeroCustomizationStore.DeleteStoredState();
        PlayerAppearance.ApplyState(new HeroCustomizationState(), save: false, notify: true);

        SaveManager.Instance?.DeleteAll();
        NetworkManager.ClearLocalProgressCache(clearPendingSync: true);

        string storyId = ResolveStoryId(storyData, currentStoryId);
        if (!string.IsNullOrEmpty(storyId))
        {
            MarkForceFreshStart(storyId);
            GameState.Instance?.InitForStory(storyId);
        }

        StoryHistory.Instance?.Clear();
        SafeSavePrefs();
    }

    public static bool ShouldForceFreshStart(string storyId)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (string.IsNullOrEmpty(storyId))
            return false;

        return LocalSecurePrefs.GetBool(GetForceFreshStartKey(storyId), GetForceFreshStartPurpose(storyId), false);
    }

    public static void ClearForceFreshStart(string storyId)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (string.IsNullOrEmpty(storyId))
            return;

        SafeDeleteSecurePrefsKey(GetForceFreshStartKey(storyId));
        SafeSavePrefs();
    }

    private static void DeleteLegacyProgressKeys()
    {
        SafeDeleteSecurePrefsKey("VN_STATS");
        SafeDeleteSecurePrefsKey("VN_OWNED_CLOTHES");
        SafeDeleteSecurePrefsKey("VN_EQUIPPED_CLOTHES");
        SafeDeleteSecurePrefsKey("VN_BOOKMARK");
        SafeDeleteSecurePrefsKey("VN_BOOKMARK_GUID");
        SafeDeleteSecurePrefsKey("VN_BOOKMARK_TIME");
        SafeDeleteSecurePrefsKey(PreStorySetupDoneKey);
        SafeDeleteSecurePrefsKey(WardrobeHeroSetupDoneKey);

        for (int seasonIndex = 0; seasonIndex < 10; seasonIndex++)
        {
            for (int chapterIndex = 0; chapterIndex < 20; chapterIndex++)
                SafeDeleteSecurePrefsKey($"chapter_{seasonIndex}_{chapterIndex}");
        }
    }

    private static void DeleteStoryScopedKeys(StoryData storyData, string currentStoryId)
    {
        DeleteStoryScopedKeysForId(currentStoryId);

        if (storyData != null)
        {
            DeleteStoryScopedKeysForId(storyData.storyId);
            DeleteStoryScopedKeysForId(storyData.name);
        }
    }

    private static void DeleteStorySaveFiles(StoryData storyData, string currentStoryId)
    {
        DeleteStorySaveFilesForId(currentStoryId);

        if (storyData != null)
        {
            DeleteStorySaveFilesForId(storyData.storyId);
            DeleteStorySaveFilesForId(storyData.name);
        }
    }

    private static void DeleteStorySaveFilesForId(string storyId)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (string.IsNullOrEmpty(storyId))
            return;

        SaveManager.Instance?.DeleteForStory(storyId);
    }

    private static void DeleteStoryScopedKeysForId(string storyId)
    {
        if (string.IsNullOrEmpty(storyId))
            return;

        storyId = storyId.Trim();
        SafeDeleteSecurePrefsKey("VN_STATS_" + storyId);
        SafeDeleteSecurePrefsKey("VN_OWNED_" + storyId);
        SafeDeleteSecurePrefsKey("VN_EQUIPPED_" + storyId);
        SafeDeleteSecurePrefsKey("VN_BOOKMARK_SNAPSHOT_" + storyId);
        HeroCustomizationStore.DeletePlayerNameForStory(storyId);
        HeroCustomizationStore.DeleteAppearanceForStory(storyId);

        string boundaryPrefix = StoryBoundaryPrefix + storyId + "_";
        SafeDeleteSecurePrefsKey(boundaryPrefix + "has");
        SafeDeleteSecurePrefsKey(boundaryPrefix + "completed");
        SafeDeleteSecurePrefsKey(boundaryPrefix + "next");
        SafeDeleteSecurePrefsKey(boundaryPrefix + "finished");
        SafeDeleteSecurePrefsKey(boundaryPrefix + EpisodeSummaryStatsSuffix);
        SafeDeleteSecurePrefsKey(boundaryPrefix + EpisodeSummaryCandlesSuffix);
        SafeDeleteSecurePrefsKey(boundaryPrefix + EpisodeSummaryHeartsSuffix);

        string safeId = SaveDataSanitizer.SafeKeyPart(storyId);
        if (!string.IsNullOrEmpty(safeId) && !string.Equals(safeId, storyId, StringComparison.Ordinal))
            DeleteStoryScopedKeysForId(safeId);
    }

    private static void MarkForceFreshStart(string storyId)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (string.IsNullOrEmpty(storyId))
            return;

        try
        {
            LocalSecurePrefs.SetBool(GetForceFreshStartKey(storyId), GetForceFreshStartPurpose(storyId), true);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Progress reset: failed to mark fresh start: " + exception.Message);
        }
    }

    private static string GetForceFreshStartKey(string storyId)
    {
        return ForceFreshStartPrefix + SaveDataSanitizer.SafeKeyPart(storyId);
    }

    private static string GetForceFreshStartPurpose(string storyId)
    {
        return LocalSaveSecurity.SetupFlagPurpose + ":force_fresh:" + SaveDataSanitizer.SanitizeIdentifier(storyId);
    }

    private static string ResolveStoryId(StoryData storyData, string currentStoryId)
    {
        string storyId = SaveDataSanitizer.SanitizeIdentifier(currentStoryId);
        if (!string.IsNullOrEmpty(storyId))
            return storyId;

        if (storyData == null)
            return "";

        storyId = SaveDataSanitizer.SanitizeIdentifier(storyData.storyId);
        if (!string.IsNullOrEmpty(storyId))
            return storyId;

        return SaveDataSanitizer.SanitizeIdentifier(storyData.name);
    }

    private static void DeleteChapterUnlockKeys(StoryData storyData)
    {
        if (storyData == null || storyData.chapters == null)
            return;

        foreach (ChapterData chapter in storyData.chapters)
        {
            if (chapter == null)
                continue;

            string unlockId = !string.IsNullOrEmpty(chapter.chapterId)
                ? chapter.chapterId
                : (chapter.graph != null ? chapter.graph.episodeId : "");

            if (string.IsNullOrEmpty(unlockId))
                continue;

            string safeUnlockId = SaveDataSanitizer.SafeKeyPart(unlockId);
            SafeDeleteSecurePrefsKey("chapter_unlock_" + safeUnlockId);
            if (!string.Equals(safeUnlockId, unlockId, StringComparison.Ordinal))
                SafeDeleteSecurePrefsKey("chapter_unlock_" + unlockId);
        }
    }

    private static void SafeDeleteSecurePrefsKey(string key)
    {
        try
        {
            LocalSecurePrefs.Delete(key);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Progress reset: failed to delete secure key '{key}': {exception.Message}");
        }
    }

    private static void SafeSavePrefs()
    {
        try
        {
            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Progress reset: failed to save PlayerPrefs: " + exception.Message);
        }
    }
}
