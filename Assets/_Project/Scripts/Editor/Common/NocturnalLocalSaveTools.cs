using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class NocturnalLocalSaveTools
{
    const string MenuRoot = "Tools/Nocturnal/";

    [MenuItem(MenuRoot + "Reset Local Saves")]
    public static void ResetLocalSaves()
    {
        if (!EditorUtility.DisplayDialog(
                "Reset local saves",
                "Delete local save files and local progress PlayerPrefs?\n\nAuth tokens, audio settings, favorites, and balance are not touched.",
                "Reset",
                "Cancel"))
        {
            return;
        }

        var storyIds = new HashSet<string>(StringComparer.Ordinal);
        var unlockIds = new HashSet<string>(StringComparer.Ordinal);

        CollectIdsFromProject(storyIds, unlockIds);
        CollectIdsFromSaveFiles(storyIds, unlockIds);
        CollectIdsFromRuntime(storyIds);
        ResetRuntimeProgressState();

        int deletedFiles = DeleteSaveFiles();
        int deletedExtraFiles = DeleteExtraLocalProgressFiles();
        int deletedPrefs = DeleteProgressPrefs(storyIds, unlockIds);

        PlayerPrefs.Save();
        AssetDatabase.Refresh();

        Debug.Log($"[Nocturnal] Local saves reset. Deleted files: {deletedFiles + deletedExtraFiles}. Deleted/touched PlayerPrefs keys: {deletedPrefs}. Persistent data: {Application.persistentDataPath}");
    }

    [MenuItem(MenuRoot + "Open Persistent Data Folder")]
    public static void OpenPersistentDataFolder()
    {
        Directory.CreateDirectory(Application.persistentDataPath);
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }

    static void CollectIdsFromProject(HashSet<string> storyIds, HashSet<string> unlockIds)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:StoryData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var story = AssetDatabase.LoadAssetAtPath<StoryData>(path);
            if (story == null)
                continue;

            AddIfNotEmpty(storyIds, story.storyId);
            AddIfNotEmpty(storyIds, story.name);

            if (story.chapters == null)
                continue;

            foreach (var chapter in story.chapters)
                CollectChapterIds(chapter, unlockIds);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ChapterData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CollectChapterIds(AssetDatabase.LoadAssetAtPath<ChapterData>(path), unlockIds);
        }
    }

    static void CollectChapterIds(ChapterData chapter, HashSet<string> unlockIds)
    {
        if (chapter == null)
            return;

        AddIfNotEmpty(unlockIds, chapter.chapterId);
        AddIfNotEmpty(unlockIds, chapter.graph != null ? chapter.graph.episodeId : "");
    }

    static void CollectIdsFromSaveFiles(HashSet<string> storyIds, HashSet<string> unlockIds)
    {
        var persistence = new SavePersistenceService();
        SavePathResolver paths = persistence.Paths;

        foreach (string path in paths.EnumerateSaveFiles(includeTemp: false, includeBackups: false, includeMetadata: false))
        {
            try
            {
                SaveLoadResult result = persistence.LoadSaveFile(path, SavePathResolver.SafeFileLabel(path), "", 0);
                if (!result.Success || result.Data == null)
                    continue;

                SaveData save = result.Data;
                AddIfNotEmpty(storyIds, save.storyId);
                AddIfNotEmpty(unlockIds, save.episodeId);
                AddIfNotEmpty(unlockIds, save.chapterId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Nocturnal] Failed to inspect save '{path}': {exception.Message}");
            }
        }
    }

    static void CollectIdsFromRuntime(HashSet<string> storyIds)
    {
        StoryManager storyManager = StoryManager.Instance;
        if (storyManager == null)
            return;

        AddIfNotEmpty(storyIds, storyManager.CurrentStoryId);
        if (storyManager.storyData != null)
        {
            AddIfNotEmpty(storyIds, storyManager.storyData.storyId);
            AddIfNotEmpty(storyIds, storyManager.storyData.name);
        }
    }

    static void ResetRuntimeProgressState()
    {
        if (!EditorApplication.isPlaying)
            return;

        StoryManager storyManager = StoryManager.Instance;
        if (storyManager != null)
        {
            storyManager.StopAllCoroutines();
            storyManager.CloseEndPanel();
        }

        NetworkManager.ClearLocalProgressCache(clearPendingSync: true);
        StoryHistory.Instance?.Clear();
    }

    static int DeleteSaveFiles()
    {
        var persistence = new SavePersistenceService();
        SavePathResolver paths = persistence.Paths;
        int deleted = 0;
        foreach (string path in paths.EnumerateSaveFiles(includeTemp: true, includeBackups: true, includeMetadata: true))
        {
            try
            {
                persistence.DeleteFileSet(path);
                deleted++;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Nocturnal] Failed to delete save '{path}': {exception.Message}");
            }
        }

        deleted += DeleteDirectory(paths.GetSnapshotRootDirectory());
        return deleted;
    }

    static int DeleteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return 0;

        try
        {
            Directory.Delete(path, true);
            return 1;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Nocturnal] Failed to delete local progress directory '{path}': {exception.Message}");
            return 0;
        }
    }

    static int DeleteExtraLocalProgressFiles()
    {
        int deleted = 0;
        deleted += DeletePersistentFile("hero_customization.json");
        return deleted;
    }

    static int DeletePersistentFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return 0;

        string path = Path.Combine(Application.persistentDataPath, fileName);
        if (!File.Exists(path))
            return 0;

        try
        {
            File.Delete(path);
            return 1;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Nocturnal] Failed to delete local progress file '{path}': {exception.Message}");
            return 0;
        }
    }

    static int DeleteProgressPrefs(HashSet<string> storyIds, HashSet<string> unlockIds)
    {
        int deleted = 0;

        deleted += DeleteKey("VN_STATS");
        deleted += DeleteKey("VN_OWNED_CLOTHES");
        deleted += DeleteKey("VN_EQUIPPED_CLOTHES");
        deleted += DeleteKey("VN_PLAYER_NAME");
        deleted += DeleteKey("VN_APPEARANCE");
        deleted += DeleteKey("VN_HERO_OUTFIT");
        deleted += DeleteKey("VN_HERO_HAIR");
        deleted += DeleteKey("VN_PRE_STORY_SETUP_DONE");
        deleted += DeleteKey("VN_BOOKMARK");
        deleted += DeleteKey("VN_BOOKMARK_GUID");
        deleted += DeleteKey("VN_BOOKMARK_TIME");

        foreach (string storyId in storyIds)
            deleted += DeleteStoryScopedPrefs(storyId);

        foreach (string unlockId in unlockIds)
            deleted += DeleteUnlockPrefs(unlockId);

        for (int seasonIndex = 0; seasonIndex < 10; seasonIndex++)
        {
            for (int chapterIndex = 0; chapterIndex < 20; chapterIndex++)
                deleted += DeleteKey($"chapter_{seasonIndex}_{chapterIndex}");
        }

        deleted += DeleteIndexedPrefs("VN_PENDING_PROGRESS_INDEX", "VN_PENDING_PROGRESS_");
        deleted += DeleteIndexedPrefs("VN_PENDING_BOOKMARK_INDEX", "VN_PENDING_BOOKMARK_");

        return deleted;
    }

    static int DeleteStoryScopedPrefs(string storyId)
    {
        int deleted = 0;
        foreach (string keyPart in ExpandKeyParts(storyId))
        {
            deleted += DeleteKey("VN_STATS_" + keyPart);
            deleted += DeleteKey("VN_OWNED_" + keyPart);
            deleted += DeleteKey("VN_EQUIPPED_" + keyPart);
            deleted += DeleteKey("VN_BOOKMARK_SNAPSHOT_" + keyPart);
            deleted += DeleteKey("VN_FORCE_FRESH_START_" + keyPart);

            string boundaryPrefix = "VN_STORY_BOUNDARY_" + keyPart + "_";
            deleted += DeleteKey(boundaryPrefix + "has");
            deleted += DeleteKey(boundaryPrefix + "completed");
            deleted += DeleteKey(boundaryPrefix + "next");
            deleted += DeleteKey(boundaryPrefix + "finished");

            PlayerPrefs.SetInt("VN_FORCE_FRESH_START_" + keyPart, 1);
            deleted++;
        }

        return deleted;
    }

    static int DeleteUnlockPrefs(string unlockId)
    {
        int deleted = 0;
        foreach (string keyPart in ExpandKeyParts(unlockId))
            deleted += DeleteKey("chapter_unlock_" + keyPart);

        return deleted;
    }

    static int DeleteIndexedPrefs(string indexKey, string entryPrefix)
    {
        int deleted = 0;
        string rawIndex = PlayerPrefs.GetString(indexKey, "");

        foreach (string itemKey in rawIndex.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            deleted += DeleteKey(entryPrefix + itemKey);

        deleted += DeleteKey(indexKey);
        return deleted;
    }

    static int DeleteKey(string key)
    {
        if (string.IsNullOrEmpty(key) || !PlayerPrefs.HasKey(key))
            return 0;

        PlayerPrefs.DeleteKey(key);
        return 1;
    }

    static void AddIfNotEmpty(HashSet<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            values.Add(value);
    }

    static IEnumerable<string> ExpandKeyParts(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        string raw = value.Trim();
        yield return raw;

        string safe = SaveDataSanitizer.SafeKeyPart(raw);
        if (!string.IsNullOrEmpty(safe) && !string.Equals(safe, raw, StringComparison.Ordinal))
            yield return safe;

        string sanitized = SaveDataSanitizer.SanitizeIdentifier(raw);
        if (!string.IsNullOrEmpty(sanitized) &&
            !string.Equals(sanitized, raw, StringComparison.Ordinal) &&
            !string.Equals(sanitized, safe, StringComparison.Ordinal))
        {
            yield return sanitized;
        }
    }
}
