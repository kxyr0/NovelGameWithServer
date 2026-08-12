#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.Networking;

public static class ContentReleaseAdminRoutes
{
    public const string BasePath = "/admin/content/releases";
    public const string PromotePath = BasePath + "/promote";
    public const string RollbackPath = BasePath + "/rollback";

    public static string BuildFetchPath(string storyId, string episodeId)
    {
        var query = new List<string>();
        AddQuery(query, "storyId", storyId);
        AddQuery(query, "episodeId", episodeId);
        return query.Count == 0 ? BasePath : BasePath + "?" + string.Join("&", query);
    }

    public static bool IsKnownPath(string path)
    {
        string normalized = NormalizePathOnly(path);
        return normalized == BasePath ||
               normalized == PromotePath ||
               normalized == RollbackPath ||
               normalized.StartsWith(BasePath + "/", StringComparison.Ordinal);
    }

    private static void AddQuery(List<string> query, string name, string value)
    {
        value = SaveDataSanitizer.SanitizeIdentifier(value);
        if (!string.IsNullOrWhiteSpace(value))
            query.Add(name + "=" + UnityWebRequest.EscapeURL(value));
    }

    private static string NormalizePathOnly(string path)
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
