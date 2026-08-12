#if UNITY_EDITOR
using System;
using System.Collections;
using System.Text;
using UnityEngine.Networking;
public static class CurrentBackendCatalogClient
{
    public const string AdminKeyEnvironmentVariable = "NOCTURNEDC_ADMIN_KEY";
    private const int MaxBodyChars = 1024 * 1024;
    private const int MaxResponseChars = 1024 * 1024;
    public static IEnumerator FetchCatalog(Action<UnityPublisherRequestResult> callback, string baseUrl, string adminKey, bool allowUnsigned)
    {
        yield return Send("GET", CurrentBackendAdminRoutes.AdminCatalog, null, callback, baseUrl, adminKey, allowUnsigned);
    }
    public static IEnumerator CreateStory(
        string storyId,
        string title,
        bool allowHeroRename,
        Action<UnityPublisherRequestResult> callback,
        string baseUrl,
        string adminKey,
        bool allowUnsigned)
    {
        string json = CurrentBackendCatalogPayloadBuilder.BuildStory(storyId, title, allowHeroRename, out string error);
        yield return SendPayload("POST", CurrentBackendAdminRoutes.Story, json, error, callback, baseUrl, adminKey, allowUnsigned);
    }
    public static IEnumerator AddSeason(
        string storyId,
        string seasonId,
        string title,
        int order,
        Action<UnityPublisherRequestResult> callback,
        string baseUrl,
        string adminKey,
        bool allowUnsigned)
    {
        string story = RequiredId(storyId, "ID истории", out string error);
        string json = string.IsNullOrEmpty(error)
            ? CurrentBackendCatalogPayloadBuilder.BuildSeason(seasonId, title, order, out error)
            : "";
        yield return SendPayload("POST", CurrentBackendAdminRoutes.StorySeason(story), json, error, callback, baseUrl, adminKey, allowUnsigned);
    }
    public static IEnumerator AddEpisode(
        string seasonId,
        string episodeId,
        string title,
        bool isPremium,
        int candleCost,
        int order,
        bool geoRestricted,
        Action<UnityPublisherRequestResult> callback,
        string baseUrl,
        string adminKey,
        bool allowUnsigned)
    {
        string season = RequiredId(seasonId, "ID сезона", out string error);
        string json = string.IsNullOrEmpty(error)
            ? CurrentBackendCatalogPayloadBuilder.BuildEpisode(episodeId, title, isPremium, candleCost, order, geoRestricted, out error)
            : "";
        yield return SendPayload("POST", CurrentBackendAdminRoutes.SeasonEpisode(season), json, error, callback, baseUrl, adminKey, allowUnsigned);
    }
    public static IEnumerator UploadEpisodeContent(
        string episodeId,
        string jsonContent,
        Action<UnityPublisherRequestResult> callback,
        string baseUrl,
        string adminKey,
        bool allowUnsigned)
    {
        episodeId = RequiredId(episodeId, "ID эпизода", out string error);
        if (string.IsNullOrEmpty(error) && (string.IsNullOrWhiteSpace(jsonContent) || jsonContent.Length > MaxBodyChars))
            error = "JSON эпизода пустой или слишком большой.";
        yield return SendPayload("POST", CurrentBackendAdminRoutes.EpisodeContent(episodeId), jsonContent, error, callback, baseUrl, adminKey, allowUnsigned);
    }
    public static IEnumerator SetStoryPublished(
        string storyId,
        bool published,
        Action<UnityPublisherRequestResult> callback,
        string baseUrl,
        string adminKey,
        bool allowUnsigned)
    {
        yield return SendPublish(storyId, "ID истории", CurrentBackendAdminRoutes.StoryPublish, published, callback, baseUrl, adminKey, allowUnsigned);
    }
    public static IEnumerator SetEpisodePublished(
        string episodeId,
        bool published,
        Action<UnityPublisherRequestResult> callback,
        string baseUrl,
        string adminKey,
        bool allowUnsigned)
    {
        yield return SendPublish(episodeId, "ID эпизода", CurrentBackendAdminRoutes.EpisodePublish, published, callback, baseUrl, adminKey, allowUnsigned);
    }
    private static IEnumerator SendPublish(
        string id,
        string label,
        Func<string, string> route,
        bool published,
        Action<UnityPublisherRequestResult> callback,
        string baseUrl,
        string adminKey,
        bool allowUnsigned)
    {
        string safeId = RequiredId(id, label, out string error);
        string json = "{\"published\":" + (published ? "true" : "false") + "}";
        yield return SendPayload("PATCH", route(safeId), json, error, callback, baseUrl, adminKey, allowUnsigned);
    }
    private static IEnumerator SendPayload(
        string method,
        string path,
        string json,
        string error,
        Action<UnityPublisherRequestResult> callback,
        string baseUrl,
        string adminKey,
        bool allowUnsigned)
    {
        if (!string.IsNullOrEmpty(error))
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail(error));
            yield break;
        }
        yield return Send(method, path, json, callback, baseUrl, adminKey, allowUnsigned);
    }
    private static IEnumerator Send(
        string method,
        string path,
        string json,
        Action<UnityPublisherRequestResult> callback,
        string baseUrl,
        string adminKey,
        bool allowUnsigned)
    {
        string url = BuildUrl(path, baseUrl, out string error);
        if (!string.IsNullOrEmpty(error))
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail(error));
            yield break;
        }
        string key = FirstNonEmpty(adminKey, Environment.GetEnvironmentVariable(AdminKeyEnvironmentVariable));
        if (string.IsNullOrWhiteSpace(key) && !allowUnsigned && !IsLoopback(url))
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail("Для admin API текущего backend нужен X-Admin-Key."));
            yield break;
        }
        using (var request = new UnityWebRequest(url, method))
        {
            if (!string.IsNullOrEmpty(json))
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 30;
            request.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(json))
                request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrWhiteSpace(key))
                request.SetRequestHeader("X-Admin-Key", key.Trim());
            yield return request.SendWebRequest();
            callback?.Invoke(UnityPublisherRequestResult.FromRequest(request, MaxResponseChars));
        }
    }
    private static string BuildUrl(string path, string baseUrl, out string error)
    {
        error = "";
        if (!CurrentBackendAdminRoutes.IsKnownPath(path))
            return Fail("Неизвестный admin route текущего backend.", out error);
        string root = FirstNonEmpty(baseUrl, ApiRoutes.BaseUrl).Trim().TrimEnd('/');
        if (!Uri.TryCreate(root, UriKind.Absolute, out Uri uri))
            return Fail("Адрес сервера некорректный.", out error);
        if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            return Fail("Адрес сервера должен использовать HTTPS или локальный HTTP.", out error);
        return root + path;
    }
    private static string RequiredId(string value, string label, out string error)
    {
        string id = SaveDataSanitizer.SanitizeIdentifier(value);
        error = string.IsNullOrWhiteSpace(id) ? "Укажите " + label + "." : "";
        return id;
    }
    private static string Fail(string message, out string error)
    {
        error = message;
        return "";
    }
    private static string FirstNonEmpty(string first, string second)
    {
        return !string.IsNullOrWhiteSpace(first) ? first : second;
    }
    private static bool IsLoopback(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && uri.IsLoopback;
    }
}
#endif
