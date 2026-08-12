#if UNITY_EDITOR
using System;
using UnityEngine.Networking;

public static class CurrentBackendAdminRoutes
{
    public const string AdminCatalog = "/admin/catalog";
    public const string Story = "/admin/catalog/story";

    public static string StoryPublish(string storyId)
    {
        return "/admin/catalog/story/" + EscapeId(storyId) + "/publish";
    }

    public static string StorySeason(string storyId)
    {
        return "/admin/catalog/story/" + EscapeId(storyId) + "/season";
    }

    public static string SeasonEpisode(string seasonId)
    {
        return "/admin/catalog/season/" + EscapeId(seasonId) + "/episode";
    }

    public static string EpisodeContent(string episodeId)
    {
        return "/admin/catalog/episode/" + EscapeId(episodeId) + "/content";
    }

    public static string EpisodePublish(string episodeId)
    {
        return "/admin/catalog/episode/" + EscapeId(episodeId) + "/publish";
    }

    public static bool IsKnownPath(string path)
    {
        string normalized = NormalizePath(path);
        return normalized == AdminCatalog ||
               normalized == Story ||
               (normalized.StartsWith("/admin/catalog/story/", StringComparison.Ordinal) &&
                (normalized.EndsWith("/publish", StringComparison.Ordinal) ||
                 normalized.EndsWith("/season", StringComparison.Ordinal))) ||
               (normalized.StartsWith("/admin/catalog/season/", StringComparison.Ordinal) &&
                normalized.EndsWith("/episode", StringComparison.Ordinal)) ||
               (normalized.StartsWith("/admin/catalog/episode/", StringComparison.Ordinal) &&
                (normalized.EndsWith("/content", StringComparison.Ordinal) ||
                 normalized.EndsWith("/publish", StringComparison.Ordinal)));
    }

    private static string EscapeId(string value)
    {
        return UnityWebRequest.EscapeURL(SaveDataSanitizer.SanitizeIdentifier(value));
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        int queryIndex = path.IndexOf('?');
        string onlyPath = queryIndex >= 0 ? path.Substring(0, queryIndex) : path;
        if (!onlyPath.StartsWith("/", StringComparison.Ordinal))
            onlyPath = "/" + onlyPath;
        return onlyPath.TrimEnd('/');
    }
}
#endif
