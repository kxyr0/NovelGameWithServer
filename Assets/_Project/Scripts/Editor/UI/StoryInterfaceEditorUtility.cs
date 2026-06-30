#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class StoryInterfaceEditorUtility
{
    public const string StoriesRoot = "Assets/_MyProject/Data/Stories";

    public static StoryInterfaceStyleCatalog FindDefaultCatalog()
    {
        string[] guids = AssetDatabase.FindAssets("t:StoryInterfaceStyleCatalog");
        if (guids == null || guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<StoryInterfaceStyleCatalog>(path);
    }

    public static bool TryGetCatalogEntry(
        StoryData story,
        string storyId,
        out StoryInterfaceStyleCatalog catalog,
        out StoryInterfaceStyleEntry entry)
    {
        catalog = FindDefaultCatalog();
        entry = null;

        if (catalog == null)
            return false;

        string id = ResolveStoryId(story, storyId);
        return catalog.TryGetEntry(story, id, out entry);
    }

    public static List<StoryData> FindAllStories(bool includeEditorTest = false)
    {
        var result = new List<StoryData>();
        string[] guids = AssetDatabase.FindAssets("t:StoryData", new[] { StoriesRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!includeEditorTest && path.IndexOf("/__EditorTest/", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            StoryData story = AssetDatabase.LoadAssetAtPath<StoryData>(path);
            if (story != null)
                result.Add(story);
        }

        result.Sort((left, right) => string.Compare(left.storyId, right.storyId, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    public static StoryData FindStoryForLibrary(StoryJsonAssetLibrary library)
    {
        if (library == null)
            return null;

        List<StoryData> stories = FindAllStories();
        for (int i = 0; i < stories.Count; i++)
        {
            StoryData story = stories[i];
            if (story == null || story.Chapters == null)
                continue;

            foreach (ChapterData chapter in story.Chapters)
            {
                if (chapter != null && chapter.JsonAssetLibrary == library)
                    return story;
            }
        }

        string libraryPath = AssetDatabase.GetAssetPath(library);
        if (string.IsNullOrWhiteSpace(libraryPath))
            return null;

        string libraryRoot = ResolveStoryRootFolder(libraryPath, "");
        for (int i = 0; i < stories.Count; i++)
        {
            StoryData story = stories[i];
            string storyRoot = ResolveStoryRootFolder(AssetDatabase.GetAssetPath(story), ResolveStoryId(story, ""));
            if (string.Equals(libraryRoot, storyRoot, StringComparison.OrdinalIgnoreCase))
                return story;
        }

        return null;
    }

    public static StoryJsonAssetLibrary FindLibraryForStory(StoryData story, string storyId)
    {
        if (story != null && story.Chapters != null)
        {
            foreach (ChapterData chapter in story.Chapters)
            {
                if (chapter != null && chapter.JsonAssetLibrary != null)
                    return chapter.JsonAssetLibrary;
            }
        }

        string root = ResolveStoryRootFolder(story != null ? AssetDatabase.GetAssetPath(story) : "", storyId);
        string[] guids = AssetDatabase.FindAssets("t:StoryJsonAssetLibrary", new[] { root });
        if (guids != null && guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<StoryJsonAssetLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));

        return null;
    }

    public static string ResolveStoryId(StoryData story, string fallback)
    {
        if (story != null)
        {
            string storyId = Normalize(story.storyId);
            if (!string.IsNullOrWhiteSpace(storyId))
                return storyId;

            string assetName = Normalize(story.name);
            if (!string.IsNullOrWhiteSpace(assetName))
                return assetName;
        }

        return Normalize(fallback);
    }

    public static string ResolveEntryStoryId(StoryInterfaceStyleEntry entry)
    {
        if (entry == null)
            return "";

        if (entry.StoryIds != null)
        {
            foreach (string id in entry.StoryIds)
            {
                string normalized = Normalize(id);
                if (!string.IsNullOrWhiteSpace(normalized))
                    return normalized;
            }
        }

        return ResolveStoryId(entry.StoryAsset, entry.Label);
    }

    public static string ResolveStoryRootFolder(string assetPath, string storyId)
    {
        string normalized = (assetPath ?? "").Replace('\\', '/');
        string root = StoriesRoot + "/";
        int start = normalized.IndexOf(root, StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            string rest = normalized.Substring(start + root.Length);
            int slash = rest.IndexOf('/');
            if (slash > 0)
                return root + rest.Substring(0, slash);
        }

        return root + SafeFileName(storyId);
    }

    public static string SafeFileName(string value)
    {
        value = Normalize(value);
        if (string.IsNullOrWhiteSpace(value))
            return "story";

        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value.Replace(' ', '_');
    }

    public static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }

    public static void SelectAndPing(UnityEngine.Object asset)
    {
        if (asset == null)
            return;

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
#endif
