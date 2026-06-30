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
        lastCompletedEpisodeStatDeltas.Clear();
        currentEpisodeStartCandles = PlayerData.Candles;
        currentEpisodeStartHearts = PlayerData.Hearts;
        lastCompletedEpisodeCandleDelta = 0;
        lastCompletedEpisodeHeartDelta = 0;
    }

    void ResetCurrentEpisodeSummary()
    {
        currentEpisodeStatDeltas.Clear();
        currentEpisodeStartCandles = PlayerData.Candles;
        currentEpisodeStartHearts = PlayerData.Hearts;
    }

    void CaptureCompletedEpisodeSummary()
    {
        lastCompletedEpisodeStatDeltas.Clear();

        foreach (var pair in currentEpisodeStatDeltas)
        {
            string statId = SaveDataSanitizer.SanitizeStatKey(pair.Key);
            if (!string.IsNullOrEmpty(statId) && pair.Value != 0)
                lastCompletedEpisodeStatDeltas[statId] = SaveDataSanitizer.ClampStatValue(pair.Value);
        }

        lastCompletedEpisodeCandleDelta = PlayerData.Candles - currentEpisodeStartCandles;
        lastCompletedEpisodeHeartDelta = PlayerData.Hearts - currentEpisodeStartHearts;
    }

    void RecordEpisodeStatDelta(string statId, int appliedDelta)
    {
        statId = SaveDataSanitizer.SanitizeStatKey(statId);
        if (string.IsNullOrEmpty(statId) || appliedDelta == 0)
            return;

        currentEpisodeStatDeltas.TryGetValue(statId, out int currentDelta);
        currentEpisodeStatDeltas[statId] = SaveDataSanitizer.ClampStatDelta(currentDelta, appliedDelta);
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
