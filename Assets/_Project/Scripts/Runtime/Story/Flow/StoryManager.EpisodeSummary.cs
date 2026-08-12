using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public partial class StoryManager
{
    private const string EpisodeSummaryStatsPrefsSuffix = "episode_summary_stats";
    private const string EpisodeSummaryCandlesPrefsSuffix = "episode_summary_candles";
    private const string EpisodeSummaryHeartsPrefsSuffix = "episode_summary_hearts";
    private const int MaxEpisodeSummaryPayloadChars = LocalSaveSecurity.MaxProtectedPayloadChars;

    private readonly Dictionary<string, int> currentEpisodeStatDeltas =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> lastCompletedEpisodeStatDeltas =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> currentEpisodeStartStats =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private bool hasCurrentEpisodeStartSnapshot;
    private int currentEpisodeStartCandles;
    private int currentEpisodeStartHearts;
    private int lastCompletedEpisodeCandleDelta;
    private int lastCompletedEpisodeHeartDelta;

    public int LastCompletedEpisodeCandleDelta => lastCompletedEpisodeCandleDelta;
    public int LastCompletedEpisodeHeartDelta => lastCompletedEpisodeHeartDelta;

    public int GetLastCompletedEpisodeStatDelta(params string[] statIds)
    {
        if (statIds == null || statIds.Length == 0)
            return 0;

        for (int i = 0; i < statIds.Length; i++)
        {
            string statId = SaveDataSanitizer.SanitizeStatKey(statIds[i]);
            if (string.IsNullOrEmpty(statId))
                continue;

            if (lastCompletedEpisodeStatDeltas.TryGetValue(statId, out int delta) && delta != 0)
                return delta;
        }

        // End panel is opened before the next chapter is started, so the currently
        // tracked episode deltas are still a valid emergency source. This also
        // protects rendering from completion-order mistakes.
        for (int i = 0; i < statIds.Length; i++)
        {
            string statId = SaveDataSanitizer.SanitizeStatKey(statIds[i]);
            if (string.IsNullOrEmpty(statId))
                continue;

            if (currentEpisodeStatDeltas.TryGetValue(statId, out int trackedDelta) && trackedDelta != 0)
                return trackedDelta;
        }

        return 0;
    }

    string FormatLastCompletedEpisodeStatLine(string label, params string[] statIds)
    {
        return FormatEpisodeSummaryLine(label, GetLastCompletedEpisodeStatDelta(statIds));
    }

    string FormatLastCompletedEpisodeHeartsLine(string label)
    {
        return FormatEpisodeSummaryLine(label, lastCompletedEpisodeHeartDelta);
    }

    string FormatLastCompletedEpisodeCandlesLine(string label)
    {
        return FormatEpisodeSummaryLine(label, lastCompletedEpisodeCandleDelta);
    }

    void ResetEpisodeSummaryState()
    {
        currentEpisodeStatDeltas.Clear();
        currentEpisodeStartStats.Clear();
        lastCompletedEpisodeStatDeltas.Clear();
        hasCurrentEpisodeStartSnapshot = false;
        currentEpisodeStartCandles = PlayerData.Candles;
        currentEpisodeStartHearts = PlayerData.Hearts;
        lastCompletedEpisodeCandleDelta = 0;
        lastCompletedEpisodeHeartDelta = 0;
    }

    void ResetCurrentEpisodeSummary()
    {
        currentEpisodeStatDeltas.Clear();
        CaptureCurrentEpisodeStartSnapshot();
    }

    void CaptureCurrentEpisodeStartSnapshot()
    {
        currentEpisodeStartStats.Clear();

        if (GameState.Instance != null && GameState.Instance.stats != null)
        {
            foreach (KeyValuePair<string, int> pair in GameState.Instance.stats)
            {
                string statId = SaveDataSanitizer.SanitizeStatKey(pair.Key);
                if (!string.IsNullOrEmpty(statId))
                    currentEpisodeStartStats[statId] = SaveDataSanitizer.ClampStatValue(pair.Value);
            }
        }

        currentEpisodeStartCandles = PlayerData.Candles;
        currentEpisodeStartHearts = PlayerData.Hearts;
        hasCurrentEpisodeStartSnapshot = true;

        if (Debug.isDebugBuild || Application.isEditor)
        {
            Debug.Log(
                $"[END_STATS][BASELINE] storyId='{CurrentStoryId}' chapterId='{CurrentChapterId}' " +
                $"stats={currentEpisodeStartStats.Count} hearts={currentEpisodeStartHearts} candles={currentEpisodeStartCandles}.",
                this);
        }

        AppLogger.DebugLog(
            AppLogCategory.EndScreen,
            nameof(StoryManager),
            nameof(CaptureCurrentEpisodeStartSnapshot),
            "[END_STATS][BASELINE_CAPTURED] Chapter stat baseline captured.",
            LogMetadata.Of(
                "storyId", CurrentStoryId,
                "chapterId", CurrentChapterId,
                "statCount", currentEpisodeStartStats.Count,
                "candles", currentEpisodeStartCandles,
                "hearts", currentEpisodeStartHearts));
    }

    public void WriteCurrentEpisodeSummaryToSaveData(SaveData data)
    {
        if (data == null)
            return;

        if (!hasCurrentEpisodeStartSnapshot)
            CaptureCurrentEpisodeStartSnapshot();

        data.hasEpisodeStartSnapshot = hasCurrentEpisodeStartSnapshot;
        data.episodeStartCandles = currentEpisodeStartCandles;
        data.episodeStartHearts = currentEpisodeStartHearts;
        if (data.episodeStartStatKeys == null)
            data.episodeStartStatKeys = new List<string>();
        if (data.episodeStartStatValues == null)
            data.episodeStartStatValues = new List<int>();
        data.episodeStartStatKeys.Clear();
        data.episodeStartStatValues.Clear();

        foreach (KeyValuePair<string, int> pair in currentEpisodeStartStats)
        {
            if (data.episodeStartStatKeys.Count >= SaveDataSanitizer.MaxStatEntries)
                break;

            data.episodeStartStatKeys.Add(pair.Key);
            data.episodeStartStatValues.Add(pair.Value);
        }
    }

    void RestoreCurrentEpisodeSummaryFromSaveData(SaveData data)
    {
        currentEpisodeStatDeltas.Clear();
        currentEpisodeStartStats.Clear();
        hasCurrentEpisodeStartSnapshot = false;

        if (data != null && data.hasEpisodeStartSnapshot)
        {
            int count = Math.Min(
                data.episodeStartStatKeys != null ? data.episodeStartStatKeys.Count : 0,
                data.episodeStartStatValues != null ? data.episodeStartStatValues.Count : 0);

            count = Math.Min(count, SaveDataSanitizer.MaxStatEntries);
            for (int i = 0; i < count; i++)
            {
                string statId = SaveDataSanitizer.SanitizeStatKey(data.episodeStartStatKeys[i]);
                if (!string.IsNullOrEmpty(statId))
                    currentEpisodeStartStats[statId] = SaveDataSanitizer.ClampStatValue(data.episodeStartStatValues[i]);
            }

            currentEpisodeStartCandles = SaveDataSanitizer.ClampCurrencyValue(data.episodeStartCandles);
            currentEpisodeStartHearts = SaveDataSanitizer.ClampCurrencyValue(data.episodeStartHearts);
            hasCurrentEpisodeStartSnapshot = true;

            AppLogger.DebugLog(
                AppLogCategory.EndScreen,
                nameof(StoryManager),
                nameof(RestoreCurrentEpisodeSummaryFromSaveData),
                "[END_STATS][BASELINE_RESTORED] Chapter stat baseline restored from save.",
                LogMetadata.Of(
                    "storyId", CurrentStoryId,
                    "chapterId", CurrentChapterId,
                    "statCount", currentEpisodeStartStats.Count,
                    "candles", currentEpisodeStartCandles,
                    "hearts", currentEpisodeStartHearts));
            return;
        }

        // Old saves did not persist the chapter baseline. Starting from the
        // restored state is the only safe fallback: showing cumulative story
        // totals as a chapter reward would be actively wrong.
        CaptureCurrentEpisodeStartSnapshot();
        ThrottledAppLogger.Warn(
            "EndStatsLegacySaveNoBaseline:" + CurrentStoryId + ":" + CurrentChapterId,
            AppLogCategory.EndScreen,
            nameof(StoryManager),
            nameof(RestoreCurrentEpisodeSummaryFromSaveData),
            "[END_STATS][LEGACY_SAVE_NO_BASELINE] Save has no chapter baseline; only changes made after this restore can be counted for this chapter.",
            LogMetadata.Of(
                "storyId", CurrentStoryId,
                "chapterId", CurrentChapterId));
    }

    void CaptureCompletedEpisodeSummary()
    {
        PrepareEpisodeSummaryForEndScreen();
    }

    public void PrepareEpisodeSummaryForEndScreen()
    {
        var computed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // First source: the persisted chapter-start baseline. This survives save/load
        // and also catches stat changes performed outside the normal story node handlers.
        if (hasCurrentEpisodeStartSnapshot && GameState.Instance != null)
        {
            var statIds = new HashSet<string>(currentEpisodeStartStats.Keys, StringComparer.OrdinalIgnoreCase);
            if (GameState.Instance.stats != null)
            {
                foreach (KeyValuePair<string, int> pair in GameState.Instance.stats)
                {
                    string statId = SaveDataSanitizer.SanitizeStatKey(pair.Key);
                    if (!string.IsNullOrEmpty(statId))
                        statIds.Add(statId);
                }
            }

            foreach (string statId in statIds)
            {
                currentEpisodeStartStats.TryGetValue(statId, out int startValue);
                int currentValue = GameState.Instance.GetStat(statId);
                int delta = SaveDataSanitizer.ClampStatValue(currentValue - startValue);
                if (delta != 0)
                    computed[statId] = delta;
            }
        }

        // Second source: deltas recorded exactly when story nodes apply a stat change.
        // It is deliberately merged even when a baseline exists. This protects the
        // end screen from lifecycle/order bugs where the baseline was captured too late.
        foreach (KeyValuePair<string, int> pair in currentEpisodeStatDeltas)
        {
            string statId = SaveDataSanitizer.SanitizeStatKey(pair.Key);
            int trackedDelta = SaveDataSanitizer.ClampStatValue(pair.Value);
            if (!string.IsNullOrEmpty(statId) && trackedDelta != 0)
                computed[statId] = trackedDelta;
        }

        lastCompletedEpisodeStatDeltas.Clear();
        foreach (KeyValuePair<string, int> pair in computed)
        {
            if (pair.Value != 0)
                lastCompletedEpisodeStatDeltas[pair.Key] = pair.Value;
        }

        if (hasCurrentEpisodeStartSnapshot)
        {
            lastCompletedEpisodeCandleDelta = SaveDataSanitizer.ClampStatValue(PlayerData.Candles - currentEpisodeStartCandles);
            lastCompletedEpisodeHeartDelta = SaveDataSanitizer.ClampStatValue(PlayerData.Hearts - currentEpisodeStartHearts);
        }

        int city = GetLastCompletedEpisodeStatDelta("city", "town", "gorod");
        int fairytale = GetLastCompletedEpisodeStatDelta("fairytale", "story", "tale", "skazka");
        int reputation = GetLastCompletedEpisodeStatDelta("reputation", "respect", "rep");

        string plainLog =
            $"[END_STATS][PREPARE] platform={Application.platform} storyId='{CurrentStoryId}' chapterId='{CurrentChapterId}' " +
            $"baseline={hasCurrentEpisodeStartSnapshot} baselineStats={currentEpisodeStartStats.Count} " +
            $"trackedStats={currentEpisodeStatDeltas.Count} resultStats={lastCompletedEpisodeStatDeltas.Count} " +
            $"city={city} fairytale={fairytale} reputation={reputation} " +
            $"hearts={lastCompletedEpisodeHeartDelta} candles={lastCompletedEpisodeCandleDelta}.";
        Debug.Log(plainLog, this);

        AppLogger.Info(
            AppLogCategory.EndScreen,
            nameof(StoryManager),
            nameof(PrepareEpisodeSummaryForEndScreen),
            plainLog,
            LogMetadata.Of(
                "storyId", CurrentStoryId,
                "chapterId", CurrentChapterId,
                "baseline", hasCurrentEpisodeStartSnapshot,
                "baselineStats", currentEpisodeStartStats.Count,
                "trackedStats", currentEpisodeStatDeltas.Count,
                "resultStats", lastCompletedEpisodeStatDeltas.Count,
                "city", city,
                "fairytale", fairytale,
                "reputation", reputation,
                "hearts", lastCompletedEpisodeHeartDelta,
                "candles", lastCompletedEpisodeCandleDelta));
    }

    void RecordEpisodeStatDelta(string statId, int appliedDelta)
    {
        statId = SaveDataSanitizer.SanitizeStatKey(statId);
        if (string.IsNullOrEmpty(statId) || appliedDelta == 0)
            return;

        currentEpisodeStatDeltas.TryGetValue(statId, out int currentDelta);
        currentEpisodeStatDeltas[statId] = SaveDataSanitizer.ClampStatDelta(currentDelta, appliedDelta);

        if (Debug.isDebugBuild || Application.isEditor)
        {
            Debug.Log(
                $"[END_STATS][TRACK] storyId='{CurrentStoryId}' chapterId='{CurrentChapterId}' " +
                $"stat='{statId}' applied={appliedDelta} chapterTracked={currentEpisodeStatDeltas[statId]}.",
                this);
        }
    }

    void SaveLastCompletedEpisodeSummary(string prefsPrefix)
    {
        if (string.IsNullOrEmpty(prefsPrefix))
            return;

        SaveEpisodeSummaryText(prefsPrefix, EpisodeSummaryStatsPrefsSuffix, "stats", SerializeEpisodeStats(lastCompletedEpisodeStatDeltas));
        SaveEpisodeSummaryText(prefsPrefix, EpisodeSummaryCandlesPrefsSuffix, "candles", SaveDataSanitizer.ClampStatValue(lastCompletedEpisodeCandleDelta).ToString(CultureInfo.InvariantCulture));
        SaveEpisodeSummaryText(prefsPrefix, EpisodeSummaryHeartsPrefsSuffix, "hearts", SaveDataSanitizer.ClampStatValue(lastCompletedEpisodeHeartDelta).ToString(CultureInfo.InvariantCulture));
    }

    void LoadLastCompletedEpisodeSummary(string prefsPrefix)
    {
        lastCompletedEpisodeStatDeltas.Clear();
        lastCompletedEpisodeCandleDelta = 0;
        lastCompletedEpisodeHeartDelta = 0;

        if (string.IsNullOrEmpty(prefsPrefix))
            return;

        string statsText = LoadEpisodeSummaryText(prefsPrefix, EpisodeSummaryStatsPrefsSuffix, "stats", out bool statsWasProtected, out bool statsHadValue);
        DeserializeEpisodeStats(statsText, lastCompletedEpisodeStatDeltas);
        lastCompletedEpisodeCandleDelta = LoadEpisodeSummaryInt(prefsPrefix, EpisodeSummaryCandlesPrefsSuffix, "candles", out bool candlesWasProtected, out bool candlesHadValue);
        lastCompletedEpisodeHeartDelta = LoadEpisodeSummaryInt(prefsPrefix, EpisodeSummaryHeartsPrefsSuffix, "hearts", out bool heartsWasProtected, out bool heartsHadValue);

        if ((statsHadValue && !statsWasProtected) ||
            (candlesHadValue && !candlesWasProtected) ||
            (heartsHadValue && !heartsWasProtected))
        {
            SaveLastCompletedEpisodeSummary(prefsPrefix);
        }
    }

    void ClearLastCompletedEpisodeSummary(string prefsPrefix)
    {
        lastCompletedEpisodeStatDeltas.Clear();
        lastCompletedEpisodeCandleDelta = 0;
        lastCompletedEpisodeHeartDelta = 0;

        if (string.IsNullOrEmpty(prefsPrefix))
            return;

        DeleteEpisodeSummaryKey(prefsPrefix + EpisodeSummaryStatsPrefsSuffix);
        DeleteEpisodeSummaryKey(prefsPrefix + EpisodeSummaryCandlesPrefsSuffix);
        DeleteEpisodeSummaryKey(prefsPrefix + EpisodeSummaryHeartsPrefsSuffix);
    }

    static void SaveEpisodeSummaryText(string prefsPrefix, string suffix, string purpose, string payload)
    {
        string key = prefsPrefix + suffix;
        string protectedPayload = LocalSaveSecurity.ProtectText(payload ?? "", GetEpisodeSummaryPurpose(prefsPrefix, purpose));
        if (string.IsNullOrEmpty(protectedPayload))
        {
            Debug.LogWarning("[StoryManager] Episode summary payload could not be protected.");
            return;
        }

        PlayerPrefs.SetString(key, protectedPayload);
        LocalSecurePrefs.MarkSecure(key);
    }

    static string LoadEpisodeSummaryText(string prefsPrefix, string suffix, string purpose, out bool wasProtected, out bool hadValue)
    {
        wasProtected = false;
        hadValue = false;

        string key = prefsPrefix + suffix;
        if (!PlayerPrefs.HasKey(key))
            return "";

        string stored = "";
        try
        {
            stored = PlayerPrefs.GetString(key, "");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[StoryManager] Failed to load episode summary text: " + exception.Message);
            return "";
        }

        if (string.IsNullOrEmpty(stored))
            return "";

        hadValue = true;
        if (stored.Length > MaxEpisodeSummaryPayloadChars)
        {
            DeleteEpisodeSummaryKey(key);
            return "";
        }

        if (!LocalSaveSecurity.TryUnprotectText(stored, GetEpisodeSummaryPurpose(prefsPrefix, purpose), out string payload, out wasProtected))
        {
            Debug.LogWarning("[StoryManager] Ignored episode summary payload with invalid integrity.");
            DeleteEpisodeSummaryKey(key);
            hadValue = false;
            return "";
        }

        if (!wasProtected && LocalSecurePrefs.HasSecureMarker(key))
        {
            Debug.LogWarning("[StoryManager] Ignored downgraded episode summary payload.");
            DeleteEpisodeSummaryKey(key);
            hadValue = false;
            return "";
        }

        if (wasProtected)
            LocalSecurePrefs.EnsureSecureMarker(key);

        return payload;
    }

    static int LoadEpisodeSummaryInt(string prefsPrefix, string suffix, string purpose, out bool wasProtected, out bool hadValue)
    {
        string key = prefsPrefix + suffix;
        string raw = LoadEpisodeSummaryText(prefsPrefix, suffix, purpose, out wasProtected, out bool hadTextValue);
        if (hadTextValue)
        {
            hadValue = true;
            return TryParseEpisodeSummaryValue(raw);
        }

        wasProtected = false;
        hadValue = PlayerPrefs.HasKey(key);
        if (!hadValue)
            return 0;

        if (LocalSecurePrefs.HasSecureMarker(key))
        {
            Debug.LogWarning("[StoryManager] Ignored downgraded episode summary int.");
            DeleteEpisodeSummaryKey(key);
            hadValue = false;
            return 0;
        }

        try
        {
            return SaveDataSanitizer.ClampStatValue(PlayerPrefs.GetInt(key, 0));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[StoryManager] Failed to load legacy episode summary int: " + exception.Message);
            DeleteEpisodeSummaryKey(key);
            hadValue = false;
            return 0;
        }
    }

    static int TryParseEpisodeSummaryValue(string raw)
    {
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? SaveDataSanitizer.ClampStatValue(value)
            : 0;
    }

    static void DeleteEpisodeSummaryKey(string key)
    {
        try
        {
            LocalSecurePrefs.Delete(key);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[StoryManager] Failed to delete invalid episode summary key: " + exception.Message);
        }
    }

    static string GetEpisodeSummaryPurpose(string prefsPrefix, string purpose)
    {
        return LocalSaveSecurity.EpisodeSummaryPurpose + ":" +
               SaveDataSanitizer.SanitizeIdentifier(prefsPrefix) + ":" +
               SaveDataSanitizer.SanitizeIdentifier(purpose);
    }

    static string FormatEpisodeSummaryLine(string label, int delta)
    {
        return (label ?? "").Trim() + ": " + FormatSignedEpisodeValue(delta);
    }

    public static string FormatSignedEpisodeValue(int delta)
    {
        return delta > 0
            ? "+" + delta.ToString(CultureInfo.InvariantCulture)
            : delta.ToString(CultureInfo.InvariantCulture);
    }

    static string SerializeEpisodeStats(Dictionary<string, int> stats)
    {
        if (stats == null || stats.Count == 0)
            return "";

        string result = "";
        int saved = 0;
        foreach (var pair in stats)
        {
            string statId = SaveDataSanitizer.SanitizeStatKey(pair.Key);
            if (string.IsNullOrEmpty(statId) || pair.Value == 0)
                continue;

            result += statId + ":" + SaveDataSanitizer.ClampStatValue(pair.Value).ToString(CultureInfo.InvariantCulture) + ";";
            saved++;
            if (saved >= SaveDataSanitizer.MaxStatEntries)
                break;
        }

        return result;
    }

    static void DeserializeEpisodeStats(string raw, Dictionary<string, int> target)
    {
        if (target == null)
            return;

        target.Clear();
        if (string.IsNullOrEmpty(raw))
            return;

        string[] pairs = raw.Split(';');
        for (int i = 0; i < pairs.Length; i++)
        {
            string pair = pairs[i];
            if (string.IsNullOrEmpty(pair))
                continue;

            string[] parts = pair.Split(new[] { ':' }, 2);
            if (parts.Length != 2)
                continue;

            string statId = SaveDataSanitizer.SanitizeStatKey(parts[0]);
            if (string.IsNullOrEmpty(statId))
                continue;

            if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int delta) && delta != 0)
                target[statId] = SaveDataSanitizer.ClampStatValue(delta);
        }
    }
}
