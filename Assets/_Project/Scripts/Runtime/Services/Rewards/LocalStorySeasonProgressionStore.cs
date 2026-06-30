using System;
using UnityEngine;

public sealed class LocalStorySeasonProgressionStore : IStorySeasonProgressionStore
{
    const string DefaultPrefsPrefix = "VN_STORY_SEASON_COMPLETION_";

    readonly string _prefsPrefix;

    public LocalStorySeasonProgressionStore(string prefsPrefix = DefaultPrefsPrefix)
    {
        _prefsPrefix = string.IsNullOrWhiteSpace(prefsPrefix) ? DefaultPrefsPrefix : prefsPrefix.Trim();
    }

    public StorySeasonCompletionState Load(StorySeasonKey key)
    {
        if (!key.IsValid)
        {
            AppLogger.Warn(
                AppLogCategory.StoryProgression,
                nameof(LocalStorySeasonProgressionStore),
                nameof(Load),
                "[StoryProgression] Invalid season completion key; returning empty state.",
                LogMetadata.Of("storyId", key.StoryId, "season", key.SeasonNumber),
                recoverable: true);
            return StorySeasonCompletionState.Empty(key);
        }

        string prefsKey = GetPrefsKey(key);
        try
        {
            if (!PlayerPrefs.HasKey(prefsKey))
            {
                AppLogger.Info(
                    AppLogCategory.StoryProgression,
                    nameof(LocalStorySeasonProgressionStore),
                    nameof(Load),
                    "[StoryProgression] No saved season completion state found.",
                    BuildSaveLogMetadata(key, prefsKey, protectedPayload: false, state: null));
                return StorySeasonCompletionState.Empty(key);
            }

            string stored = PlayerPrefs.GetString(prefsKey, "");
            if (string.IsNullOrEmpty(stored))
            {
                AppLogger.Warn(
                    AppLogCategory.StoryProgression,
                    nameof(LocalStorySeasonProgressionStore),
                    nameof(Load),
                    "[StoryProgression] Empty season completion state was ignored.",
                    BuildSaveLogMetadata(key, prefsKey, protectedPayload: false, state: null),
                    recoverable: true);
                return StorySeasonCompletionState.Empty(key);
            }

            AppLogger.Info(
                AppLogCategory.StoryProgression,
                nameof(LocalStorySeasonProgressionStore),
                nameof(Load),
                "[StoryProgression] Reading saved season completion state.",
                LogMetadata.Of(
                    "storyId", key.StoryId,
                    "season", key.SeasonNumber,
                    "prefsKey", SaveDataSanitizer.SafeKeyPart(prefsKey),
                    "storedLength", stored.Length,
                    "secureMarker", LocalSecurePrefs.HasSecureMarker(prefsKey)));

            if (!LocalSaveSecurity.TryUnprotectText(stored, GetPurpose(key), out string json, out bool wasProtected))
            {
                AppLogger.Warn(
                    AppLogCategory.StoryProgression,
                    nameof(LocalStorySeasonProgressionStore),
                    nameof(Load),
                    "[StoryProgression] Invalid protected season completion state was ignored.",
                    LogMetadata.Of("storyId", key.StoryId, "season", key.SeasonNumber, "prefsKey", SaveDataSanitizer.SafeKeyPart(prefsKey)),
                    recoverable: true);
                LocalSecurePrefs.Delete(prefsKey);
                return StorySeasonCompletionState.Empty(key);
            }

            AppLogger.Info(
                AppLogCategory.StoryProgression,
                nameof(LocalStorySeasonProgressionStore),
                nameof(Load),
                "[StoryProgression] Season completion payload protection validated.",
                LogMetadata.Of(
                    "storyId", key.StoryId,
                    "season", key.SeasonNumber,
                    "prefsKey", SaveDataSanitizer.SafeKeyPart(prefsKey),
                    "protectedPayload", wasProtected,
                    "secureMarker", LocalSecurePrefs.HasSecureMarker(prefsKey),
                    "plainLength", json != null ? json.Length : 0));

            if (!wasProtected && LocalSecurePrefs.HasSecureMarker(prefsKey))
            {
                AppLogger.Warn(
                    AppLogCategory.StoryProgression,
                    nameof(LocalStorySeasonProgressionStore),
                    nameof(Load),
                    "[StoryProgression] Downgraded raw season completion state was ignored.",
                    LogMetadata.Of("storyId", key.StoryId, "season", key.SeasonNumber, "prefsKey", SaveDataSanitizer.SafeKeyPart(prefsKey)),
                    recoverable: true);
                LocalSecurePrefs.Delete(prefsKey);
                return StorySeasonCompletionState.Empty(key);
            }

            StorySeasonCompletionState state = JsonUtility.FromJson<StorySeasonCompletionState>(json);
            if (state == null)
            {
                AppLogger.Warn(
                    AppLogCategory.StoryProgression,
                    nameof(LocalStorySeasonProgressionStore),
                    nameof(Load),
                    "[StoryProgression] Season completion state JSON was empty or invalid.",
                    BuildSaveLogMetadata(key, prefsKey, wasProtected, null),
                    recoverable: true);
                return StorySeasonCompletionState.Empty(key);
            }

            state.Normalize(key);

            if (!wasProtected)
            {
                AppLogger.Info(
                    AppLogCategory.RewardSave,
                    nameof(LocalStorySeasonProgressionStore),
                    nameof(Load),
                    "[RewardSave] Migrating raw season completion state to protected storage.",
                    BuildSaveLogMetadata(key, prefsKey, protectedPayload: false, state: state));
                Save(key, state);
            }

            AppLogger.Info(
                AppLogCategory.StoryProgression,
                nameof(LocalStorySeasonProgressionStore),
                nameof(Load),
                "[StoryProgression] Season completion state loaded.",
                LogMetadata.Of(
                    "storyId", key.StoryId,
                    "season", key.SeasonNumber,
                    "completedOnce", state.completedOnce,
                    "completionCount", state.completionCount,
                    "activeRunId", state.activeRunId,
                    "lastRewardedRunId", state.lastRewardedRunId));

            return state;
        }
        catch (Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.StoryProgression,
                nameof(LocalStorySeasonProgressionStore),
                nameof(Load),
                "[StoryProgression] Failed to load season completion state.",
                exception,
                LogMetadata.Of("storyId", key.StoryId, "season", key.SeasonNumber, "prefsKey", SaveDataSanitizer.SafeKeyPart(prefsKey)),
                recoverable: true);
            return StorySeasonCompletionState.Empty(key);
        }
    }

    public StorySeasonProgressionSaveResult Save(StorySeasonKey key, StorySeasonCompletionState state)
    {
        if (!key.IsValid)
        {
            AppLogger.Warn(
                AppLogCategory.RewardSave,
                nameof(LocalStorySeasonProgressionStore),
                nameof(Save),
                "[RewardSave] Invalid season completion key; state was not saved.",
                LogMetadata.Of("storyId", key.StoryId, "season", key.SeasonNumber),
                recoverable: true);
            return StorySeasonProgressionSaveResult.Fail("invalid_key", "Story id or season number is invalid.");
        }

        string prefsKey = GetPrefsKey(key);
        try
        {
            state = state != null ? state.Clone() : StorySeasonCompletionState.Empty(key);
            state.Normalize(key);

            AppLogger.Info(
                AppLogCategory.RewardSave,
                nameof(LocalStorySeasonProgressionStore),
                nameof(Save),
                "[RewardSave] Saving season completion state.",
                BuildSaveLogMetadata(key, prefsKey, protectedPayload: true, state: state));

            string json = JsonUtility.ToJson(state, false);
            AppLogger.Info(
                AppLogCategory.RewardSave,
                nameof(LocalStorySeasonProgressionStore),
                nameof(Save),
                "[RewardSave] Season completion state serialized.",
                LogMetadata.Of(
                    "storyId", key.StoryId,
                    "season", key.SeasonNumber,
                    "prefsKey", SaveDataSanitizer.SafeKeyPart(prefsKey),
                    "plainLength", json != null ? json.Length : 0));

            string protectedJson = LocalSaveSecurity.ProtectText(json, GetPurpose(key));
            if (string.IsNullOrEmpty(protectedJson))
            {
                AppLogger.Error(
                    AppLogCategory.RewardSave,
                    nameof(LocalStorySeasonProgressionStore),
                    nameof(Save),
                    "[RewardSave] Failed to protect season completion state payload.",
                    null,
                    LogMetadata.Of(
                        "storyId", key.StoryId,
                        "season", key.SeasonNumber,
                        "prefsKey", SaveDataSanitizer.SafeKeyPart(prefsKey),
                        "plainLength", json != null ? json.Length : 0),
                    recoverable: true);
                return StorySeasonProgressionSaveResult.Fail("protect_failed", "Progression payload could not be protected.");
            }

            AppLogger.Info(
                AppLogCategory.RewardSave,
                nameof(LocalStorySeasonProgressionStore),
                nameof(Save),
                "[RewardSave] Season completion state protected.",
                LogMetadata.Of(
                    "storyId", key.StoryId,
                    "season", key.SeasonNumber,
                    "prefsKey", SaveDataSanitizer.SafeKeyPart(prefsKey),
                    "protectedPayload", true,
                    "protectedLength", protectedJson.Length));

            PlayerPrefs.SetString(prefsKey, protectedJson);
            LocalSecurePrefs.MarkSecure(prefsKey);
            PlayerPrefs.Save();

            AppLogger.Info(
                AppLogCategory.RewardSave,
                nameof(LocalStorySeasonProgressionStore),
                nameof(Save),
                "[RewardSave] Progression state saved successfully.",
                LogMetadata.Of(
                    "storyId", key.StoryId,
                    "season", key.SeasonNumber,
                    "prefsKey", SaveDataSanitizer.SafeKeyPart(prefsKey),
                    "protectedPayload", true,
                    "secureMarker", LocalSecurePrefs.HasSecureMarker(prefsKey),
                    "completedOnce", state.completedOnce,
                    "completionCount", state.completionCount,
                    "activeRunId", state.activeRunId,
                    "lastRewardedRunId", state.lastRewardedRunId));

            return StorySeasonProgressionSaveResult.Ok();
        }
        catch (Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.RewardSave,
                nameof(LocalStorySeasonProgressionStore),
                nameof(Save),
                "[RewardSave] Failed to save progression state.",
                exception,
                LogMetadata.Of(
                    "storyId", key.StoryId,
                    "season", key.SeasonNumber,
                    "prefsKey", SaveDataSanitizer.SafeKeyPart(prefsKey),
                    "errorType", exception.GetType().Name),
                recoverable: true);

            return StorySeasonProgressionSaveResult.Fail(exception.GetType().Name, exception.Message);
        }
    }

    string GetPrefsKey(StorySeasonKey key)
    {
        return _prefsPrefix +
               SaveDataSanitizer.SafeKeyPart(key.StoryId, "story", 80) +
               "_S" +
               Mathf.Max(1, key.SeasonNumber);
    }

    static string GetPurpose(StorySeasonKey key)
    {
        return LocalSaveSecurity.StoryProgressionPurpose + ":" + key.StoryId + ":season:" + key.SeasonNumber;
    }

    static System.Collections.Generic.IDictionary<string, object> BuildSaveLogMetadata(
        StorySeasonKey key,
        string prefsKey,
        bool protectedPayload,
        StorySeasonCompletionState state)
    {
        return LogMetadata.Of(
            "storyId", key.StoryId,
            "season", key.SeasonNumber,
            "prefsKey", SaveDataSanitizer.SafeKeyPart(prefsKey),
            "purpose", GetPurpose(key),
            "protectedPayload", protectedPayload,
            "secureMarker", !string.IsNullOrEmpty(prefsKey) && LocalSecurePrefs.HasSecureMarker(prefsKey),
            "completedOnce", state != null && state.completedOnce,
            "completionCount", state != null ? state.completionCount : 0,
            "activeRunId", state != null ? state.activeRunId : "",
            "lastRewardedRunId", state != null ? state.lastRewardedRunId : "");
    }
}
