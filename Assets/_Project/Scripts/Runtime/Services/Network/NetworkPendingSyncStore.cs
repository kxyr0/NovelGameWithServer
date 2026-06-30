using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class NetworkPendingSyncStore
{
    private const string PendingProgressIndexKey = "VN_PENDING_PROGRESS_INDEX";
    private const string PendingBookmarkIndexKey = "VN_PENDING_BOOKMARK_INDEX";
    private const string PendingProgressPrefix = "VN_PENDING_PROGRESS_";
    private const string PendingBookmarkPrefix = "VN_PENDING_BOOKMARK_";
    private const int MaxPendingItems = 32;
    private const int MaxPendingPayloadChars = LocalSaveSecurity.MaxProtectedPayloadChars;
    private const int MaxPendingIndexChars = 8192;

    public void Load(
        Dictionary<string, PendingProgressPayload> pendingProgress,
        Dictionary<string, PendingBookmarkPayload> pendingBookmarks)
    {
        if (pendingProgress == null || pendingBookmarks == null)
            return;

        pendingProgress.Clear();
        foreach (string key in LoadPendingIndex(PendingProgressIndexKey))
        {
            string prefsKey = PendingProgressPrefix + key;
            string json = LoadPendingPayload(prefsKey, PendingProgressPrefix, out bool wasProtected);
            if (string.IsNullOrEmpty(json))
                continue;

            PendingProgressPayload payload = NetworkJson.FromJson<PendingProgressPayload>(json);
            payload = SanitizeProgressPayload(payload);
            if (payload != null)
            {
                pendingProgress[key] = payload;
                if (!wasProtected)
                    SavePendingPayload(PendingProgressPrefix, PendingProgressIndexKey, key, NetworkJson.ToJson(payload));
            }
        }

        pendingBookmarks.Clear();
        foreach (string key in LoadPendingIndex(PendingBookmarkIndexKey))
        {
            string prefsKey = PendingBookmarkPrefix + key;
            string json = LoadPendingPayload(prefsKey, PendingBookmarkPrefix, out bool wasProtected);
            if (string.IsNullOrEmpty(json))
                continue;

            PendingBookmarkPayload payload = NetworkJson.FromJson<PendingBookmarkPayload>(json);
            payload = SanitizeBookmarkPayload(payload);
            if (payload != null)
            {
                pendingBookmarks[key] = payload;
                if (!wasProtected)
                    SavePendingPayload(PendingBookmarkPrefix, PendingBookmarkIndexKey, key, NetworkJson.ToJson(payload));
            }
        }
    }

    public void SaveProgress(string key, PendingProgressPayload pending)
    {
        pending = SanitizeProgressPayload(pending);
        if (pending == null)
            return;

        SavePendingPayload(PendingProgressPrefix, PendingProgressIndexKey, key, NetworkJson.ToJson(pending));
    }

    public void SaveBookmark(string key, PendingBookmarkPayload pending)
    {
        pending = SanitizeBookmarkPayload(pending);
        if (pending == null)
            return;

        SavePendingPayload(PendingBookmarkPrefix, PendingBookmarkIndexKey, key, NetworkJson.ToJson(pending));
    }

    public void ClearProgress(string key, ICollection<string> remainingKeys)
    {
        ClearPendingPayload(PendingProgressPrefix, PendingProgressIndexKey, key, remainingKeys);
    }

    public void ClearBookmark(string key, ICollection<string> remainingKeys)
    {
        ClearPendingPayload(PendingBookmarkPrefix, PendingBookmarkIndexKey, key, remainingKeys);
    }

    public void ClearAll()
    {
        ClearPendingGroup(PendingProgressPrefix, PendingProgressIndexKey);
        ClearPendingGroup(PendingBookmarkPrefix, PendingBookmarkIndexKey);
    }

    private static void SavePendingPayload(string prefix, string indexKey, string key, string json)
    {
        string safeKey = NormalizePendingKey(key);
        if (string.IsNullOrEmpty(safeKey))
            return;

        if (!string.IsNullOrEmpty(json) && json.Length > MaxPendingPayloadChars)
        {
            AppLogger.Warn(
                AppLogCategory.SaveSystem,
                nameof(NetworkPendingSyncStore),
                nameof(SavePendingPayload),
                "Pending sync payload is too large and was not stored.",
                LogMetadata.Of("prefix", prefix, "payloadChars", json.Length, "maxPayloadChars", MaxPendingPayloadChars),
                recoverable: true);
            return;
        }

        try
        {
            string protectedJson = LocalSaveSecurity.ProtectText(json ?? "", GetPendingPurpose(prefix));
            if (string.IsNullOrEmpty(protectedJson))
            {
                AppLogger.Warn(
                    AppLogCategory.Security,
                    nameof(NetworkPendingSyncStore),
                    nameof(SavePendingPayload),
                    "Pending sync payload could not be protected and was not stored.",
                    LogMetadata.Of("prefix", prefix),
                    recoverable: true);
                return;
            }

            string prefsKey = prefix + safeKey;
            PlayerPrefs.SetString(prefsKey, protectedJson);
            LocalSecurePrefs.MarkSecure(prefsKey);
            List<string> keys = LoadPendingIndex(indexKey);
            keys.Remove(safeKey);
            keys.Add(safeKey);

            while (keys.Count > MaxPendingItems)
            {
                string droppedKey = keys[0];
                keys.RemoveAt(0);
                LocalSecurePrefs.Delete(prefix + droppedKey);
            }

            SavePendingIndex(indexKey, keys);
            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.SaveSystem,
                nameof(NetworkPendingSyncStore),
                nameof(SavePendingPayload),
                "Failed to save pending sync payload.",
                exception,
                LogMetadata.Of("prefix", prefix),
                recoverable: true);
        }
    }

    private static void ClearPendingPayload(string prefix, string indexKey, string key, ICollection<string> remainingKeys)
    {
        string safeKey = NormalizePendingKey(key);
        if (string.IsNullOrEmpty(safeKey))
            return;

        try
        {
            LocalSecurePrefs.Delete(prefix + safeKey);
            SavePendingIndex(indexKey, SanitizePendingKeys(remainingKeys));
            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.SaveSystem,
                nameof(NetworkPendingSyncStore),
                nameof(ClearPendingPayload),
                "Failed to clear pending sync payload.",
                exception,
                LogMetadata.Of("prefix", prefix),
                recoverable: true);
        }
    }

    private static void ClearPendingGroup(string prefix, string indexKey)
    {
        try
        {
            foreach (string key in LoadPendingIndex(indexKey))
                LocalSecurePrefs.Delete(prefix + key);

            LocalSecurePrefs.Delete(indexKey);
            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.SaveSystem,
                nameof(NetworkPendingSyncStore),
                nameof(ClearPendingGroup),
                "Failed to clear pending sync group.",
                exception,
                LogMetadata.Of("prefix", prefix),
                recoverable: true);
        }
    }

    private static string LoadPendingPayload(string prefsKey, string prefix, out bool wasProtected)
    {
        wasProtected = false;

        string stored;
        try
        {
            stored = PlayerPrefs.GetString(prefsKey, "");
        }
        catch (Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.SaveSystem,
                nameof(NetworkPendingSyncStore),
                nameof(LoadPendingPayload),
                "Failed to load pending sync payload.",
                exception,
                LogMetadata.Of("prefix", prefix),
                recoverable: true);
            return "";
        }

        if (string.IsNullOrEmpty(stored))
            return "";

        if (stored.Length > MaxPendingPayloadChars)
        {
            LocalSecurePrefs.Delete(prefsKey);
            return "";
        }

        if (!LocalSaveSecurity.TryUnprotectText(stored, GetPendingPurpose(prefix), out string json, out wasProtected))
        {
            AppLogger.Warn(
                AppLogCategory.Security,
                nameof(NetworkPendingSyncStore),
                nameof(LoadPendingPayload),
                "Ignored pending sync payload with invalid integrity.",
                LogMetadata.Of("prefix", prefix),
                recoverable: true);
            LocalSecurePrefs.Delete(prefsKey);
            return "";
        }

        if (!wasProtected && LocalSecurePrefs.HasSecureMarker(prefsKey))
        {
            AppLogger.Warn(
                AppLogCategory.Security,
                nameof(NetworkPendingSyncStore),
                nameof(LoadPendingPayload),
                "Ignored downgraded pending sync payload.",
                LogMetadata.Of("prefix", prefix),
                recoverable: true);
            LocalSecurePrefs.Delete(prefsKey);
            return "";
        }

        if (wasProtected)
            LocalSecurePrefs.EnsureSecureMarker(prefsKey);

        return json;
    }

    private static string GetPendingPurpose(string prefix)
    {
        return "pending-sync:" + SaveDataSanitizer.SanitizeIdentifier(prefix);
    }

    private static List<string> LoadPendingIndex(string indexKey)
    {
        List<string> result = new List<string>();
        string raw;
        try
        {
            raw = LocalSecurePrefs.GetString(indexKey, GetPendingIndexPurpose(indexKey), "");
        }
        catch (Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.SaveSystem,
                nameof(NetworkPendingSyncStore),
                nameof(LoadPendingIndex),
                "Failed to load pending sync index.",
                exception,
                LogMetadata.Of("indexKey", indexKey),
                recoverable: true);
            return result;
        }

        if (string.IsNullOrEmpty(raw))
            return result;
        if (raw.Length > MaxPendingIndexChars)
        {
            LocalSecurePrefs.Delete(indexKey);
            PlayerPrefs.Save();
            return result;
        }

        foreach (string key in raw.Split('\n'))
        {
            string safeKey = NormalizePendingKey(key);
            if (!string.IsNullOrEmpty(safeKey) && !result.Contains(safeKey))
            {
                result.Add(safeKey);
                if (result.Count >= MaxPendingItems)
                    break;
            }
        }

        return result;
    }

    private static void SavePendingIndex(string indexKey, IEnumerable<string> keys)
    {
        string raw = string.Join("\n", SanitizePendingKeys(keys));
        if (raw.Length > MaxPendingIndexChars)
        {
            AppLogger.Warn(
                AppLogCategory.SaveSystem,
                nameof(NetworkPendingSyncStore),
                nameof(SavePendingIndex),
                "Pending sync index is too large and was not stored.",
                LogMetadata.Of("indexKey", indexKey, "indexChars", raw.Length, "maxIndexChars", MaxPendingIndexChars),
                recoverable: true);
            return;
        }

        LocalSecurePrefs.SetString(indexKey, GetPendingIndexPurpose(indexKey), raw);
    }

    private static string GetPendingIndexPurpose(string indexKey)
    {
        return "pending-sync-index:" + SaveDataSanitizer.SanitizeIdentifier(indexKey);
    }

    private static List<string> SanitizePendingKeys(IEnumerable<string> keys)
    {
        List<string> result = new List<string>();
        if (keys == null)
            return result;

        foreach (string key in keys)
        {
            string safeKey = NormalizePendingKey(key);
            if (string.IsNullOrEmpty(safeKey) || result.Contains(safeKey))
                continue;

            result.Add(safeKey);
            if (result.Count >= MaxPendingItems)
                break;
        }

        return result;
    }

    private static string NormalizePendingKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "";

        key = key.Trim();
        if (key.Length > 120)
            return "";

        for (int i = 0; i < key.Length; i++)
        {
            if (char.IsControl(key[i]))
                return "";
        }

        return key;
    }

    private static PendingProgressPayload SanitizeProgressPayload(PendingProgressPayload payload)
    {
        if (payload == null)
            return null;

        payload.storyId = SaveDataSanitizer.SanitizeIdentifier(payload.storyId);
        payload.currentEpisodeId = SaveDataSanitizer.SanitizeIdentifier(payload.currentEpisodeId);
        payload.currentNodeGuid = SaveDataSanitizer.SanitizeIdentifier(payload.currentNodeGuid);
        payload.savedAtIso = SaveDataSanitizer.SanitizeSavedAtIso(payload.savedAtIso);
        payload.snapshot = SaveDataSanitizer.Sanitize(payload.snapshot);
        payload.unlockedEpisodes = new List<string>();
        payload.stats = SanitizeIntPairs(payload.stats);
        payload.flags = SanitizeBoolPairs(payload.flags);

        if (payload.snapshot == null && string.IsNullOrEmpty(payload.currentNodeGuid))
            return null;

        return payload;
    }

    private static PendingBookmarkPayload SanitizeBookmarkPayload(PendingBookmarkPayload payload)
    {
        if (payload == null)
            return null;

        payload.nodeGuid = SaveDataSanitizer.SanitizeIdentifier(payload.nodeGuid);
        payload.episodeId = SaveDataSanitizer.SanitizeIdentifier(payload.episodeId);
        payload.storyId = SaveDataSanitizer.SanitizeIdentifier(payload.storyId);
        payload.label = SaveDataSanitizer.SanitizeHistoryLine(payload.label);
        payload.savedAtIso = SaveDataSanitizer.SanitizeSavedAtIso(payload.savedAtIso);
        payload.snapshot = SaveDataSanitizer.Sanitize(payload.snapshot);

        if (payload.snapshot == null && string.IsNullOrEmpty(payload.nodeGuid))
            return null;

        return payload;
    }

    private static List<StringIntPair> SanitizeIntPairs(List<StringIntPair> pairs)
    {
        List<StringIntPair> result = new List<StringIntPair>();
        if (pairs == null)
            return result;

        HashSet<string> seen = new HashSet<string>();
        foreach (StringIntPair pair in pairs)
        {
            if (result.Count >= SaveDataSanitizer.MaxStatEntries)
                break;
            if (pair == null)
                continue;

            string key = SaveDataSanitizer.SanitizeStatKey(pair.key);
            if (string.IsNullOrEmpty(key) || seen.Contains(key))
                continue;

            seen.Add(key);
            result.Add(new StringIntPair(key, SaveDataSanitizer.ClampStatValue(pair.value)));
        }

        return result;
    }

    private static List<StringBoolPair> SanitizeBoolPairs(List<StringBoolPair> pairs)
    {
        List<StringBoolPair> result = new List<StringBoolPair>();
        if (pairs == null)
            return result;

        HashSet<string> seen = new HashSet<string>();
        foreach (StringBoolPair pair in pairs)
        {
            if (result.Count >= SaveDataSanitizer.MaxStatEntries)
                break;
            if (pair == null)
                continue;

            string key = SaveDataSanitizer.SanitizeStatKey(pair.key);
            if (string.IsNullOrEmpty(key) || seen.Contains(key))
                continue;

            seen.Add(key);
            result.Add(new StringBoolPair(key, pair.value));
        }

        return result;
    }
}
