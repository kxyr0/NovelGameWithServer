using System;
using System.Collections.Generic;
using System.Globalization;

[Serializable]
public class SaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public string storyId;
    public string seasonId;
    public string chapterId;
    public string episodeId;
    public string graphName;
    public int currentSeasonIndex;
    public int currentChapterIndex;
    public string currentNodeGuid;
    public int currentDialogueLineIndex;
    public int currency;
    public int hearts;
    public string playerName;
    public int appearance;
    public string heroOutfitId;
    public string heroHairId;
    public string heroAccessoryId;
    public string savedAtIso;
    public List<string> history = new List<string>();
    public List<string> wardrobe = new List<string>();
    public List<StringPair> equippedClothes = new List<StringPair>();
    public List<string> statKeys = new List<string>();
    public List<int> statValues = new List<int>();

    // Baseline of the current chapter. It is persisted with the save so the
    // completion screen can show values earned in this chapter even after
    // loading a save in the middle of the chapter.
    public bool hasEpisodeStartSnapshot;
    public int episodeStartCandles;
    public int episodeStartHearts;
    public List<string> episodeStartStatKeys = new List<string>();
    public List<int> episodeStartStatValues = new List<int>();

    public bool HasPosition =>
        !string.IsNullOrEmpty(currentNodeGuid) &&
        (!string.IsNullOrEmpty(storyId) || !string.IsNullOrEmpty(episodeId));
}

[Serializable]
public class StringPair
{
    public string key;
    public string value;

    public StringPair() { }

    public StringPair(string key, string value)
    {
        this.key = key;
        this.value = value;
    }
}

public static class SaveDataSanitizer
{
    public const int MaxSerializedChars = 256 * 1024;
    public const int MaxHistoryEntries = 300;
    public const int MaxHistoryLineChars = 512;
    public const int MaxContentTextChars = 2048;
    public const int MaxWardrobeEntries = 200;
    public const int MaxEquippedEntries = 100;
    public const int MaxStatEntries = 200;
    public const int MaxIdChars = 128;
    public const int MaxNameChars = 64;
    public const int MaxStatValue = 1000000;
    public const int MaxCurrencyValue = 1000000;

    public static SaveData Sanitize(SaveData data)
    {
        if (data == null)
            return null;

        data.version = Clamp(data.version, 1, SaveData.CurrentVersion);
        data.storyId = SanitizeIdentifier(data.storyId);
        data.seasonId = SanitizeIdentifier(data.seasonId);
        data.chapterId = SanitizeIdentifier(data.chapterId);
        data.episodeId = SanitizeIdentifier(data.episodeId);
        data.graphName = SanitizeIdentifier(data.graphName);
        data.currentNodeGuid = SanitizeIdentifier(data.currentNodeGuid);
        data.currentSeasonIndex = Clamp(data.currentSeasonIndex, 0, 10000);
        data.currentChapterIndex = Clamp(data.currentChapterIndex, 0, 10000);
        data.currentDialogueLineIndex = Clamp(data.currentDialogueLineIndex, 0, 10000);
        data.currency = Clamp(data.currency, 0, MaxCurrencyValue);
        data.hearts = Clamp(data.hearts, 0, MaxCurrencyValue);
        data.playerName = SanitizeText(data.playerName, MaxNameChars, false);
        data.appearance = Clamp(data.appearance, 0, 32);
        data.heroOutfitId = SanitizeIdentifier(data.heroOutfitId);
        data.heroHairId = SanitizeIdentifier(data.heroHairId);
        data.heroAccessoryId = SanitizeIdentifier(data.heroAccessoryId);
        data.savedAtIso = SanitizeTimestamp(data.savedAtIso);
        data.history = SanitizeStringList(data.history, MaxHistoryEntries, MaxHistoryLineChars, true, keepLast: true);
        data.wardrobe = SanitizeStringList(data.wardrobe, MaxWardrobeEntries, MaxIdChars, false, keepLast: false);
        data.equippedClothes = SanitizePairs(data.equippedClothes, MaxEquippedEntries);
        SanitizeStats(data);
        data.episodeStartCandles = ClampCurrencyValue(data.episodeStartCandles);
        data.episodeStartHearts = ClampCurrencyValue(data.episodeStartHearts);
        SanitizeEpisodeStartStats(data);

        return data;
    }

    public static SaveData SanitizeCopy(SaveData data)
    {
        if (data == null)
            return null;

        return Sanitize(new SaveData
        {
            version = data.version,
            storyId = data.storyId,
            seasonId = data.seasonId,
            chapterId = data.chapterId,
            episodeId = data.episodeId,
            graphName = data.graphName,
            currentSeasonIndex = data.currentSeasonIndex,
            currentChapterIndex = data.currentChapterIndex,
            currentNodeGuid = data.currentNodeGuid,
            currentDialogueLineIndex = data.currentDialogueLineIndex,
            currency = data.currency,
            hearts = data.hearts,
            playerName = data.playerName,
            appearance = data.appearance,
            heroOutfitId = data.heroOutfitId,
            heroHairId = data.heroHairId,
            heroAccessoryId = data.heroAccessoryId,
            savedAtIso = data.savedAtIso,
            history = data.history != null ? new List<string>(data.history) : new List<string>(),
            wardrobe = data.wardrobe != null ? new List<string>(data.wardrobe) : new List<string>(),
            equippedClothes = ClonePairs(data.equippedClothes),
            statKeys = data.statKeys != null ? new List<string>(data.statKeys) : new List<string>(),
            statValues = data.statValues != null ? new List<int>(data.statValues) : new List<int>(),
            hasEpisodeStartSnapshot = data.hasEpisodeStartSnapshot,
            episodeStartCandles = data.episodeStartCandles,
            episodeStartHearts = data.episodeStartHearts,
            episodeStartStatKeys = data.episodeStartStatKeys != null ? new List<string>(data.episodeStartStatKeys) : new List<string>(),
            episodeStartStatValues = data.episodeStartStatValues != null ? new List<int>(data.episodeStartStatValues) : new List<int>()
        });
    }

    public static bool IsSerializedSizeAllowed(string json)
    {
        return !string.IsNullOrWhiteSpace(json) && json.Length <= MaxSerializedChars;
    }

    public static string SanitizeIdentifier(string value)
    {
        return SanitizeText(value, MaxIdChars, false);
    }

    public static string SanitizeStatKey(string value)
    {
        return SanitizeText(value, MaxIdChars, false);
    }

    public static string SanitizeHistoryLine(string value)
    {
        return SanitizeText(value, MaxHistoryLineChars, true);
    }

    public static string SanitizeContentText(string value)
    {
        return SanitizeText(value, MaxContentTextChars, true);
    }

    public static string SanitizePlayerName(string value)
    {
        return SanitizeText(value, MaxNameChars, false);
    }

    public static string SanitizeSavedAtIso(string value)
    {
        return SanitizeTimestamp(value);
    }

    public static int ClampStatValue(int value)
    {
        return Clamp(value, -MaxStatValue, MaxStatValue);
    }

    public static int ClampStatDelta(int currentValue, int delta)
    {
        return ClampDelta(currentValue, delta, -MaxStatValue, MaxStatValue);
    }

    public static int ClampCurrencyValue(int value)
    {
        return Clamp(value, 0, MaxCurrencyValue);
    }

    public static int ClampCurrencyDelta(int currentValue, int delta)
    {
        return ClampDelta(currentValue, delta, 0, MaxCurrencyValue);
    }

    public static string SafeKeyPart(string value, string fallback = "default", int maxLength = 80)
    {
        string key = SanitizeText(value, MaxIdChars, false);
        if (string.IsNullOrEmpty(key))
            key = fallback;

        char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidChars.Length; i++)
            key = key.Replace(invalidChars[i], '_');

        if (key.Length <= maxLength)
            return key;

        return key.Substring(0, Math.Max(1, maxLength - 9)) + "_" + StableHash(key);
    }

    static void SanitizeStats(SaveData data)
    {
        SanitizeStatLists(data.statKeys, data.statValues, out List<string> keys, out List<int> values);
        data.statKeys = keys;
        data.statValues = values;
    }

    static void SanitizeEpisodeStartStats(SaveData data)
    {
        SanitizeStatLists(
            data.episodeStartStatKeys,
            data.episodeStartStatValues,
            out List<string> keys,
            out List<int> values);

        data.episodeStartStatKeys = keys;
        data.episodeStartStatValues = values;

        if (!data.hasEpisodeStartSnapshot)
        {
            data.episodeStartStatKeys.Clear();
            data.episodeStartStatValues.Clear();
            data.episodeStartCandles = 0;
            data.episodeStartHearts = 0;
        }
    }

    static void SanitizeStatLists(
        List<string> sourceKeys,
        List<int> sourceValues,
        out List<string> keys,
        out List<int> values)
    {
        keys = new List<string>();
        values = new List<int>();

        if (sourceKeys == null || sourceValues == null)
            return;

        int count = Math.Min(Math.Min(sourceKeys.Count, sourceValues.Count), MaxStatEntries);
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < count; i++)
        {
            string key = SanitizeStatKey(sourceKeys[i]);
            if (string.IsNullOrEmpty(key) || !seen.Add(key))
                continue;

            keys.Add(key);
            values.Add(ClampStatValue(sourceValues[i]));
        }
    }

    static List<string> SanitizeStringList(List<string> values, int maxEntries, int maxChars, bool allowNewLines, bool keepLast)
    {
        List<string> result = new List<string>();
        if (values == null || values.Count == 0)
            return result;

        HashSet<string> seen = allowNewLines ? null : new HashSet<string>();
        int start = keepLast ? Math.Max(0, values.Count - maxEntries) : 0;
        for (int i = start; i < values.Count && result.Count < maxEntries; i++)
        {
            string value = SanitizeText(values[i], maxChars, allowNewLines);
            if (string.IsNullOrEmpty(value))
                continue;

            if (seen != null)
            {
                if (seen.Contains(value))
                    continue;
                seen.Add(value);
            }

            result.Add(value);
        }

        return result;
    }

    static List<StringPair> SanitizePairs(List<StringPair> pairs, int maxEntries)
    {
        List<StringPair> result = new List<StringPair>();
        if (pairs == null || pairs.Count == 0)
            return result;

        HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < pairs.Count && result.Count < maxEntries; i++)
        {
            StringPair pair = pairs[i];
            if (pair == null)
                continue;

            string key = SanitizeIdentifier(pair.key);
            string value = SanitizeIdentifier(pair.value);
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value) || seen.Contains(key))
                continue;

            seen.Add(key);
            result.Add(new StringPair(key, value));
        }

        return result;
    }

    static List<StringPair> ClonePairs(List<StringPair> pairs)
    {
        List<StringPair> result = new List<StringPair>();
        if (pairs == null)
            return result;

        foreach (StringPair pair in pairs)
        {
            if (pair != null)
                result.Add(new StringPair(pair.key, pair.value));
        }

        return result;
    }

    static string SanitizeTimestamp(string value)
    {
        string text = SanitizeText(value, 64, false);
        if (string.IsNullOrEmpty(text))
            return "";

        if (!DateTime.TryParse(
                text,
                null,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime parsed))
        {
            return "";
        }

        DateTime utc = parsed.ToUniversalTime();
        DateTime now = DateTime.UtcNow;
        if (utc > now.AddMinutes(5))
            utc = now;
        if (utc < new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            return "";

        return utc.ToString("o");
    }

    static string SanitizeText(string value, int maxChars, bool allowNewLines)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        value = value.Trim();
        if (value.Length > maxChars)
            value = value.Substring(0, maxChars);

        char[] buffer = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsControl(c))
            {
                if (allowNewLines && (c == '\n' || c == '\r' || c == '\t'))
                    buffer[count++] = ' ';
                continue;
            }

            buffer[count++] = c;
        }

        return new string(buffer, 0, count).Trim();
    }

    static int Clamp(int value, int min, int max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    static int ClampDelta(int currentValue, int delta, int min, int max)
    {
        long value = (long)currentValue + delta;
        if (value < min)
            return min;
        if (value > max)
            return max;
        return (int)value;
    }

    static string StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < value.Length; i++)
                hash = (hash ^ value[i]) * 16777619;

            return hash.ToString("x8");
        }
    }
}
