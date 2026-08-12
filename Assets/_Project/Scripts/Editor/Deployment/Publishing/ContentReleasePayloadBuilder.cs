#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class ContentReleasePayloadBuilder
{
    public static ContentReleaseDescriptor Build(
        string environmentId,
        string status,
        string storyId,
        string episodeId,
        string contentVersion,
        string catalogUrl,
        string loadPath,
        string minAppVersion,
        string notes,
        string manifestUrl = "",
        string manifestHash = "",
        string buildTarget = "")
    {
        DeploymentEnvironmentPreset preset = ContentReleaseUploadDestinationSettings.ApplyToPreset(
            DeploymentEnvironmentPresets.Find(environmentId));
        var release = new ContentReleaseDescriptor
        {
            storyId = storyId,
            episodeId = episodeId,
            contentVersion = contentVersion,
            status = status,
            channel = ContentReleaseChannel.FromEnvironmentId(preset.EnvironmentId),
            addressablesCatalogUrl = catalogUrl,
            addressablesRemoteLoadPath = FirstNonEmpty(loadPath, preset.AddressablesLoadPath),
            addressablesManifestUrl = manifestUrl,
            addressablesManifestHash = manifestHash,
            buildTarget = buildTarget,
            minAppVersion = minAppVersion,
            notes = notes,
            updatedAtIso = DateTime.UtcNow.ToString("o")
        };

        release.Normalize();
        return release;
    }

    public static string ToJson(ContentReleaseDescriptor release, bool pretty)
    {
        if (release == null)
            return "{}";

        return JsonUtility.ToJson(release.CloneNormalized(), pretty);
    }

    public static string BuildCommandJson(
        string storyId,
        string episodeId,
        string contentVersion,
        string channel,
        out string error)
    {
        error = "";
        var command = new ContentReleaseCommand
        {
            storyId = SaveDataSanitizer.SanitizeIdentifier(storyId),
            episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId),
            contentVersion = SaveDataSanitizer.SanitizeIdentifier(contentVersion),
            channel = ContentReleaseChannel.Normalize(channel),
            requestedAtIso = DateTime.UtcNow.ToString("o")
        };
        if (string.IsNullOrWhiteSpace(command.storyId) || string.IsNullOrWhiteSpace(command.episodeId) ||
            string.IsNullOrWhiteSpace(command.contentVersion))
            error = "Укажите ID истории, ID эпизода и версию контента.";
        return JsonUtility.ToJson(command, prettyPrint: false);
    }

    public static bool TryReadSelectionIds(out string storyId, out string episodeId)
    {
        storyId = "";
        episodeId = "";
        UnityEngine.Object selected = Selection.activeObject;
        if (selected is StoryData story)
        {
            storyId = SaveDataSanitizer.SanitizeIdentifier(story.storyId);
            return !string.IsNullOrEmpty(storyId);
        }

        if (selected is ChapterData chapter)
        {
            storyId = FindStoryIdForChapter(chapter);
            episodeId = ResolveEpisodeId(chapter);
            return !string.IsNullOrEmpty(storyId) || !string.IsNullOrEmpty(episodeId);
        }

        if (selected is StoryGraph graph)
        {
            storyId = FindStoryIdForGraph(graph);
            episodeId = SaveDataSanitizer.SanitizeIdentifier(graph.episodeId);
            return !string.IsNullOrEmpty(storyId) || !string.IsNullOrEmpty(episodeId);
        }

        return false;
    }

    private static string FindStoryIdForChapter(ChapterData chapter)
    {
        if (chapter == null)
            return "";

        foreach (StoryData story in LoadStories())
        {
            if (story == null || story.chapters == null)
                continue;

            foreach (ChapterData item in story.chapters)
            {
                if (item == chapter)
                    return SaveDataSanitizer.SanitizeIdentifier(story.storyId);
            }
        }

        return "";
    }

    private static string FindStoryIdForGraph(StoryGraph graph)
    {
        if (graph == null)
            return "";

        foreach (StoryData story in LoadStories())
        {
            if (story == null || story.chapters == null)
                continue;

            foreach (ChapterData chapter in story.chapters)
            {
                if (chapter != null && chapter.graph == graph)
                    return SaveDataSanitizer.SanitizeIdentifier(story.storyId);
            }
        }

        return "";
    }

    private static string ResolveEpisodeId(ChapterData chapter)
    {
        if (chapter == null)
            return "";

        if (chapter.graph != null && !string.IsNullOrWhiteSpace(chapter.graph.episodeId))
            return SaveDataSanitizer.SanitizeIdentifier(chapter.graph.episodeId);

        return SaveDataSanitizer.SanitizeIdentifier(chapter.chapterId);
    }

    private static StoryData[] LoadStories()
    {
        string[] guids = AssetDatabase.FindAssets("t:StoryData");
        var stories = new StoryData[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            stories[i] = AssetDatabase.LoadAssetAtPath<StoryData>(path);
        }

        return stories;
    }

    private static string FirstNonEmpty(string first, string second)
    {
        return !string.IsNullOrWhiteSpace(first) ? first : second;
    }

    [Serializable]
    private sealed class ContentReleaseCommand
    {
        public string storyId = "";
        public string episodeId = "";
        public string contentVersion = "";
        public string channel = "";
        public string requestedAtIso = "";
    }
}
#endif
