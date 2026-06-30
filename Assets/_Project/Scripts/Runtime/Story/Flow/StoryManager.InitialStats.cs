using System;
using System.Collections.Generic;
using UnityEngine;

public partial class StoryManager
{
    private Dictionary<string, int> _pendingInitialStoryStats;
    private string _pendingInitialStoryStatsStoryId;

    public void QueueInitialStoryStats(string storyId, IReadOnlyDictionary<string, int> stats)
    {
        ClearPendingInitialStoryStats();

        if (stats == null || stats.Count == 0)
            return;

        var copy = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, int> pair in stats)
        {
            string statId = SaveDataSanitizer.SanitizeStatKey(pair.Key);
            if (string.IsNullOrEmpty(statId))
                continue;

            copy[statId] = SaveDataSanitizer.ClampStatValue(pair.Value);
        }

        if (copy.Count == 0)
            return;

        _pendingInitialStoryStats = copy;
        _pendingInitialStoryStatsStoryId = SaveDataSanitizer.SanitizeIdentifier(storyId);
    }

    public void ClearPendingInitialStoryStats()
    {
        _pendingInitialStoryStats = null;
        _pendingInitialStoryStatsStoryId = "";
    }

    private void ApplyPendingInitialStoryStats()
    {
        if (_pendingInitialStoryStats == null || _pendingInitialStoryStats.Count == 0)
            return;

        string currentStoryId = SaveDataSanitizer.SanitizeIdentifier(CurrentStoryId);
        if (!string.IsNullOrEmpty(_pendingInitialStoryStatsStoryId) &&
            !string.Equals(_pendingInitialStoryStatsStoryId, currentStoryId, StringComparison.OrdinalIgnoreCase))
        {
            AppLogger.Warn(
                AppLogCategory.SaveSystem,
                nameof(StoryManager),
                nameof(ApplyPendingInitialStoryStats),
                "Skipped queued initial stats because the selected story changed.",
                LogMetadata.Of(
                    "queuedStoryId", _pendingInitialStoryStatsStoryId,
                    "currentStoryId", currentStoryId,
                    "statsCount", _pendingInitialStoryStats.Count),
                recoverable: true);
            ClearPendingInitialStoryStats();
            return;
        }

        if (GameState.Instance == null)
        {
            Debug.LogWarning("[StoryManager] Cannot apply queued initial story stats: GameState is missing.", this);
            ClearPendingInitialStoryStats();
            return;
        }

        int applied = 0;
        foreach (KeyValuePair<string, int> pair in _pendingInitialStoryStats)
        {
            GameState.Instance.SetInt(pair.Key, pair.Value);
            applied++;
        }

        AppLogger.Info(
            AppLogCategory.SaveSystem,
            nameof(StoryManager),
            nameof(ApplyPendingInitialStoryStats),
            "Applied queued initial stats before story start.",
            LogMetadata.Of(
                "storyId", currentStoryId,
                "statsCount", applied));

        ClearPendingInitialStoryStats();
    }
}