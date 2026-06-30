using System;
using System.IO;
using UnityEngine;

[Serializable]
public class RemoteEpisodeGraphCacheEntry
{
    public string episodeId;
    public string contentVersion;
    public string graphJson;
    public string rawPayloadJson;
    public string fetchedAtIso;

    public bool HasGraphJson =>
        !string.IsNullOrWhiteSpace(graphJson) &&
        graphJson.Trim() != "{}";
}

public static class RemoteEpisodeGraphCache
{
    const string CacheFolderName = "remote_episode_graphs";
    const int MaxGraphJsonChars = 1024 * 1024;
    const int MaxRawPayloadJsonChars = 2 * 1024 * 1024;
    const int MaxCachePlainChars = MaxGraphJsonChars + MaxRawPayloadJsonChars + 64 * 1024;
    const int MaxProtectedCacheChars = 8 * 1024 * 1024;
    const long MaxCacheFileBytes = MaxProtectedCacheChars;

    static string CacheDirectory =>
        Path.Combine(Application.persistentDataPath, CacheFolderName);

    public static string GetLocalVersion(string episodeId, string fallback = "0")
    {
        return TryLoad(episodeId, out var entry) &&
               !string.IsNullOrWhiteSpace(entry.contentVersion)
            ? entry.contentVersion
            : fallback;
    }

    public static bool TryLoad(string episodeId, out RemoteEpisodeGraphCacheEntry entry)
    {
        entry = null;

        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        if (string.IsNullOrWhiteSpace(episodeId))
            return false;

        string path = GetPath(episodeId);
        if (!File.Exists(path))
            return false;

        long startedAt = AppDiagnostics.StartTimer();
        var metadata = LogMetadata.Of("episodeId", episodeId, "file", Path.GetFileName(path));
        try
        {
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > MaxCacheFileBytes)
            {
                AppLogger.Warn(
                    AppLogCategory.Storage,
                    nameof(RemoteEpisodeGraphCache),
                    nameof(TryLoad),
                    "Remote graph cache file was ignored because it is oversized.",
                    LogMetadata.Of("episodeId", episodeId, "fileBytes", fileInfo.Length, "maxFileBytes", MaxCacheFileBytes),
                    AppDiagnostics.ElapsedMilliseconds(startedAt),
                    recoverable: true);
                return false;
            }

            string storedText = File.ReadAllText(path);
            if (!LocalSaveSecurity.TryUnprotectLargeText(
                    storedText,
                    GetCachePurpose(episodeId),
                    MaxCachePlainChars,
                    MaxProtectedCacheChars,
                    out string json,
                    out bool wasProtected))
            {
                AppLogger.Warn(
                    AppLogCategory.Storage,
                    nameof(RemoteEpisodeGraphCache),
                    nameof(TryLoad),
                    "Remote graph cache failed integrity validation.",
                    metadata,
                    AppDiagnostics.ElapsedMilliseconds(startedAt),
                    recoverable: true);
                entry = null;
                return false;
            }

            if (!wasProtected && HasSecureMarkerFile(path))
            {
                AppLogger.Warn(
                    AppLogCategory.Storage,
                    nameof(RemoteEpisodeGraphCache),
                    nameof(TryLoad),
                    "Remote graph cache downgrade was detected and ignored.",
                    metadata,
                    AppDiagnostics.ElapsedMilliseconds(startedAt),
                    recoverable: true);
                Delete(episodeId);
                entry = null;
                return false;
            }

            entry = JsonUtility.FromJson<RemoteEpisodeGraphCacheEntry>(json);
            if (entry == null)
                return false;

            entry.episodeId = SaveDataSanitizer.SanitizeIdentifier(entry.episodeId);
            entry.contentVersion = SaveDataSanitizer.SanitizeIdentifier(entry.contentVersion);
            entry.fetchedAtIso = SaveDataSanitizer.SanitizeSavedAtIso(entry.fetchedAtIso);
            if (!string.IsNullOrEmpty(entry.rawPayloadJson) && entry.rawPayloadJson.Length > MaxRawPayloadJsonChars)
                entry.rawPayloadJson = "";

            if (!string.Equals(entry.episodeId, episodeId, StringComparison.OrdinalIgnoreCase))
            {
                AppLogger.Warn(
                    AppLogCategory.Storage,
                    nameof(RemoteEpisodeGraphCache),
                    nameof(TryLoad),
                    "Remote graph cache episode id did not match the requested episode.",
                    metadata,
                    AppDiagnostics.ElapsedMilliseconds(startedAt),
                    recoverable: true);
                entry = null;
                return false;
            }

            if (!string.IsNullOrEmpty(entry.graphJson) && entry.graphJson.Length > MaxGraphJsonChars)
            {
                AppLogger.Warn(
                    AppLogCategory.Storage,
                    nameof(RemoteEpisodeGraphCache),
                    nameof(TryLoad),
                    "Remote graph cache payload was ignored because it is oversized.",
                    LogMetadata.Of("episodeId", episodeId, "graphJsonChars", entry.graphJson.Length, "maxGraphJsonChars", MaxGraphJsonChars),
                    AppDiagnostics.ElapsedMilliseconds(startedAt),
                    recoverable: true);
                entry = null;
                return false;
            }

            if (!wasProtected)
                Save(entry);
            else
                EnsureSecureMarkerFile(path);

            AppDiagnostics.LogOperationCompleted(
                AppLogCategory.Storage,
                nameof(RemoteEpisodeGraphCache),
                nameof(TryLoad),
                "Remote graph cache loaded.",
                startedAt,
                LogMetadata.Of("episodeId", episodeId, "contentVersion", entry.contentVersion, "wasProtected", wasProtected));
            return true;
        }
        catch (Exception e)
        {
            AppDiagnostics.LogOperationFailed(
                AppLogCategory.Storage,
                nameof(RemoteEpisodeGraphCache),
                nameof(TryLoad),
                "Remote graph cache load failed.",
                startedAt,
                e,
                metadata,
                recoverable: true);
            return false;
        }
    }

    public static void Save(string episodeId, string contentVersion, string graphJson, string rawPayloadJson)
    {
        Save(new RemoteEpisodeGraphCacheEntry
        {
            episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId),
            contentVersion = string.IsNullOrWhiteSpace(contentVersion) ? "0" : SaveDataSanitizer.SanitizeIdentifier(contentVersion),
            graphJson = graphJson ?? "",
            rawPayloadJson = rawPayloadJson != null && rawPayloadJson.Length <= MaxRawPayloadJsonChars
                ? rawPayloadJson
                : "",
            fetchedAtIso = DateTime.UtcNow.ToString("o")
        });
    }

    public static void Save(RemoteEpisodeGraphCacheEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.episodeId))
            return;

        long startedAt = AppDiagnostics.StartTimer();
        if (!string.IsNullOrEmpty(entry.graphJson) && entry.graphJson.Length > MaxGraphJsonChars)
        {
            AppLogger.Warn(
                AppLogCategory.Storage,
                nameof(RemoteEpisodeGraphCache),
                nameof(Save),
                "Remote graph cache save was rejected because the graph payload is oversized.",
                LogMetadata.Of("episodeId", entry.episodeId, "graphJsonChars", entry.graphJson.Length, "maxGraphJsonChars", MaxGraphJsonChars),
                AppDiagnostics.ElapsedMilliseconds(startedAt),
                recoverable: true);
            return;
        }

        entry.episodeId = SaveDataSanitizer.SanitizeIdentifier(entry.episodeId);
        entry.contentVersion = string.IsNullOrWhiteSpace(entry.contentVersion)
            ? "0"
            : SaveDataSanitizer.SanitizeIdentifier(entry.contentVersion);
        entry.fetchedAtIso = SaveDataSanitizer.SanitizeSavedAtIso(entry.fetchedAtIso);
        if (!string.IsNullOrEmpty(entry.rawPayloadJson) && entry.rawPayloadJson.Length > MaxRawPayloadJsonChars)
            entry.rawPayloadJson = "";

        try
        {
            Directory.CreateDirectory(CacheDirectory);
            string json = JsonUtility.ToJson(entry, false);
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaxCachePlainChars)
            {
                AppLogger.Warn(
                    AppLogCategory.Storage,
                    nameof(RemoteEpisodeGraphCache),
                    nameof(Save),
                    "Remote graph cache envelope was rejected because it is oversized.",
                    LogMetadata.Of("episodeId", entry.episodeId, "jsonChars", json != null ? json.Length : 0, "maxPlainChars", MaxCachePlainChars),
                    AppDiagnostics.ElapsedMilliseconds(startedAt),
                    recoverable: true);
                return;
            }

            string protectedJson = LocalSaveSecurity.ProtectLargeText(
                json,
                GetCachePurpose(entry.episodeId),
                MaxCachePlainChars,
                MaxProtectedCacheChars,
                true);

            if (string.IsNullOrEmpty(protectedJson))
            {
                AppLogger.Warn(
                    AppLogCategory.Storage,
                    nameof(RemoteEpisodeGraphCache),
                    nameof(Save),
                    "Remote graph cache could not be protected.",
                    LogMetadata.Of("episodeId", entry.episodeId),
                    AppDiagnostics.ElapsedMilliseconds(startedAt),
                    recoverable: true);
                return;
            }

            WriteCacheFile(GetPath(entry.episodeId), protectedJson);
            EnsureSecureMarkerFile(GetPath(entry.episodeId));
            AppDiagnostics.LogOperationCompleted(
                AppLogCategory.Storage,
                nameof(RemoteEpisodeGraphCache),
                nameof(Save),
                "Remote graph cache saved.",
                startedAt,
                LogMetadata.Of("episodeId", entry.episodeId, "contentVersion", entry.contentVersion));
        }
        catch (Exception e)
        {
            AppDiagnostics.LogOperationFailed(
                AppLogCategory.Storage,
                nameof(RemoteEpisodeGraphCache),
                nameof(Save),
                "Remote graph cache save failed.",
                startedAt,
                e,
                LogMetadata.Of("episodeId", entry.episodeId, "contentVersion", entry.contentVersion),
                recoverable: true);
        }
    }

    public static void Delete(string episodeId)
    {
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        if (string.IsNullOrWhiteSpace(episodeId))
            return;

        string path = GetPath(episodeId);
        if (!File.Exists(path) && !File.Exists(GetSecureMarkerPath(path)))
            return;

        long startedAt = AppDiagnostics.StartTimer();
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            string tempPath = path + ".tmp";
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            string markerPath = GetSecureMarkerPath(path);
            if (File.Exists(markerPath))
                File.Delete(markerPath);
            AppDiagnostics.LogOperationCompleted(
                AppLogCategory.Storage,
                nameof(RemoteEpisodeGraphCache),
                nameof(Delete),
                "Remote graph cache deleted.",
                startedAt,
                LogMetadata.Of("episodeId", episodeId));
        }
        catch (Exception e)
        {
            AppDiagnostics.LogOperationFailed(
                AppLogCategory.Storage,
                nameof(RemoteEpisodeGraphCache),
                nameof(Delete),
                "Remote graph cache delete failed.",
                startedAt,
                e,
                LogMetadata.Of("episodeId", episodeId),
                recoverable: true);
        }
    }

    static string GetPath(string episodeId)
    {
        return Path.Combine(CacheDirectory, SafeFilePart(episodeId) + ".json");
    }

    static string SafeFilePart(string value)
    {
        return SaveDataSanitizer.SafeKeyPart(value, "episode", 80);
    }

    static string GetCachePurpose(string episodeId)
    {
        return LocalSaveSecurity.RemoteGraphCachePurpose + ":" + SaveDataSanitizer.SanitizeIdentifier(episodeId);
    }

    static void WriteCacheFile(string path, string content)
    {
        long startedAt = AppDiagnostics.StartTimer();
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content);

        try
        {
            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
        catch (PlatformNotSupportedException)
        {
            ReplaceCacheFileFallback(tempPath, path);
        }
        catch (IOException)
        {
            ReplaceCacheFileFallback(tempPath, path);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }

        long durationMs = AppDiagnostics.ElapsedMilliseconds(startedAt);
        AppLogger.DebugLog(
            AppLogCategory.Storage,
            nameof(RemoteEpisodeGraphCache),
            nameof(WriteCacheFile),
            "Remote graph cache file written.",
            LogMetadata.Of("file", Path.GetFileName(path), "contentChars", content != null ? content.Length : 0),
            durationMs);
        AppDiagnostics.LogIfSlow(
            AppLogCategory.Storage,
            nameof(RemoteEpisodeGraphCache),
            nameof(WriteCacheFile),
            durationMs,
            LogMetadata.Of("file", Path.GetFileName(path)));
    }

    static void ReplaceCacheFileFallback(string tempPath, string path)
    {
        if (File.Exists(path))
            File.Delete(path);

        File.Move(tempPath, path);
    }

    static void TryDeleteTempFile(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e)
        {
            AppLogger.Error(
                AppLogCategory.Storage,
                nameof(RemoteEpisodeGraphCache),
                nameof(TryDeleteTempFile),
                "Failed to delete temporary remote graph cache file.",
                e,
                LogMetadata.Of("file", !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : ""),
                recoverable: true);
        }
    }

    static void EnsureSecureMarkerFile(string cachePath)
    {
        if (string.IsNullOrEmpty(cachePath))
            return;

        string markerPath = GetSecureMarkerPath(cachePath);
        if (File.Exists(markerPath))
            return;

        try
        {
            File.WriteAllText(markerPath, "nocturne-local-secure-v1");
        }
        catch (Exception e)
        {
            AppLogger.Error(
                AppLogCategory.Security,
                nameof(RemoteEpisodeGraphCache),
                nameof(EnsureSecureMarkerFile),
                "Failed to write remote graph cache secure marker.",
                e,
                LogMetadata.Of("file", Path.GetFileName(markerPath)),
                recoverable: true);
        }
    }

    static bool HasSecureMarkerFile(string cachePath)
    {
        return !string.IsNullOrEmpty(cachePath) && File.Exists(GetSecureMarkerPath(cachePath));
    }

    static string GetSecureMarkerPath(string cachePath)
    {
        return cachePath + ".secure";
    }
}
