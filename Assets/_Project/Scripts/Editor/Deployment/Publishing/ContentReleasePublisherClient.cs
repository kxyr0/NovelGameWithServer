#if UNITY_EDITOR
using System;
using System.Collections;
using System.Text;
using UnityEngine.Networking;

public static class ContentReleasePublisherClient
{
    public const string AdminKeyEnvironmentVariable = "NOCTURNEDC_ADMIN_KEY";
    private const int MaxBodyChars = 256 * 1024;
    private const int MaxResponseChars = 512 * 1024;
    private const int MaxSecretChars = 4096;

    public static IEnumerator Upsert(
        ContentReleaseDescriptor release,
        Action<UnityPublisherRequestResult> callback,
        string baseUrlOverride = "",
        string adminKey = "",
        bool allowUnsigned = false)
    {
        DeploymentEnvironmentValidationResult validation = ContentReleasePolicy.Validate(release);
        if (!validation.IsValid)
        {
            Fail(callback, validation.Message);
            yield break;
        }

        string json = ContentReleasePayloadBuilder.ToJson(release, pretty: false);
        if (json.Length > MaxBodyChars)
        {
            Fail(callback, "Payload релиза слишком большой.");
            yield break;
        }

        yield return SendJsonRequest("POST", ContentReleaseAdminRoutes.BasePath, json, callback, baseUrlOverride, adminKey, allowUnsigned);
    }

    public static IEnumerator Fetch(
        string storyId,
        string episodeId,
        Action<UnityPublisherRequestResult> callback,
        string baseUrlOverride = "",
        string adminKey = "",
        bool allowUnsigned = false)
    {
        string path = ContentReleaseAdminRoutes.BuildFetchPath(storyId, episodeId);
        yield return SendJsonRequest("GET", path, null, callback, baseUrlOverride, adminKey, allowUnsigned);
    }

    public static IEnumerator Promote(
        string storyId,
        string episodeId,
        string contentVersion,
        Action<UnityPublisherRequestResult> callback,
        string baseUrlOverride = "",
        string adminKey = "",
        bool allowUnsigned = false)
    {
        yield return SendCommand(ContentReleaseAdminRoutes.PromotePath, storyId, episodeId, contentVersion, callback, baseUrlOverride, adminKey, allowUnsigned);
    }

    public static IEnumerator Rollback(
        string storyId,
        string episodeId,
        string contentVersion,
        Action<UnityPublisherRequestResult> callback,
        string baseUrlOverride = "",
        string adminKey = "",
        bool allowUnsigned = false)
    {
        yield return SendCommand(ContentReleaseAdminRoutes.RollbackPath, storyId, episodeId, contentVersion, callback, baseUrlOverride, adminKey, allowUnsigned);
    }

    private static IEnumerator SendCommand(
        string path,
        string storyId,
        string episodeId,
        string contentVersion,
        Action<UnityPublisherRequestResult> callback,
        string baseUrlOverride,
        string adminKey,
        bool allowUnsigned)
    {
        string json = ContentReleasePayloadBuilder.BuildCommandJson(
            storyId,
            episodeId,
            contentVersion,
            ContentReleaseChannel.Production,
            out string error);
        if (!string.IsNullOrEmpty(error))
        {
            Fail(callback, error);
            yield break;
        }

        yield return SendJsonRequest("POST", path, json, callback, baseUrlOverride, adminKey, allowUnsigned);
    }

    private static IEnumerator SendJsonRequest(
        string method,
        string path,
        string json,
        Action<UnityPublisherRequestResult> callback,
        string baseUrlOverride,
        string adminKey,
        bool allowUnsigned)
    {
        string url = BuildRequestUrl(path, baseUrlOverride, out string error);
        if (!string.IsNullOrEmpty(error))
        {
            Fail(callback, error);
            yield break;
        }

        string safeAdminKey = SanitizeSecret(FirstNonEmpty(adminKey, Environment.GetEnvironmentVariable(AdminKeyEnvironmentVariable)));
        if (string.IsNullOrEmpty(safeAdminKey) && !allowUnsigned && !IsLoopbackUrl(url))
        {
            Fail(callback, "Нужен X-Admin-Key.");
            yield break;
        }

        using (UnityWebRequest request = CreateRequest(url, method, json, safeAdminKey))
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone) yield return null;
            callback?.Invoke(UnityPublisherRequestResult.FromRequest(request, MaxResponseChars));
        }
    }

    private static UnityWebRequest CreateRequest(string url, string method, string json, string adminKey)
    {
        var request = new UnityWebRequest(url, method)
        {
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 30
        };
        if (!string.IsNullOrEmpty(json))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.SetRequestHeader("Content-Type", "application/json");
        }

        request.SetRequestHeader("Accept", "application/json");
        if (!string.IsNullOrEmpty(adminKey))
            request.SetRequestHeader("X-Admin-Key", adminKey);
        return request;
    }

    private static string BuildRequestUrl(string path, string baseUrlOverride, out string error)
    {
        error = "";
        if (!ContentReleaseAdminRoutes.IsKnownPath(path))
            return FailUrl("Неизвестный admin route релиза.", out error);

        string root = FirstNonEmpty(baseUrlOverride, NetworkRuntimeConfigLoader.Load()?.ResolveBaseUrl());
        root = string.IsNullOrWhiteSpace(root) ? "" : root.Trim().TrimEnd('/');
        if (!Uri.TryCreate(root, UriKind.Absolute, out Uri baseUri))
            return FailUrl("Адрес сервера некорректный.", out error);

        bool safeScheme = baseUri.Scheme == Uri.UriSchemeHttps ||
                          (baseUri.Scheme == Uri.UriSchemeHttp && baseUri.IsLoopback);
        if (!safeScheme)
            return FailUrl("Адрес сервера должен использовать HTTPS или локальный HTTP.", out error);

        if (!path.StartsWith("/", StringComparison.Ordinal))
            path = "/" + path;
        return root + path;
    }

    private static string FailUrl(string message, out string error)
    {
        error = message;
        return "";
    }

    private static void Fail(Action<UnityPublisherRequestResult> callback, string message)
    {
        callback?.Invoke(UnityPublisherRequestResult.Fail(message));
    }

    private static string FirstNonEmpty(string first, string second)
    {
        return !string.IsNullOrWhiteSpace(first) ? first : second;
    }

    private static string SanitizeSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        value = value.Trim();
        return value.Length <= MaxSecretChars ? value : value.Substring(0, MaxSecretChars);
    }

    private static bool IsLoopbackUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && uri.IsLoopback;
    }
}
#endif
