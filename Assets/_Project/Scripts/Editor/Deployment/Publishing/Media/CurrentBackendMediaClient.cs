#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

public static class CurrentBackendMediaClient
{
    private const int MaxResponseChars = 1024 * 1024;

    public static IEnumerator List(
        Action<UnityPublisherRequestResult> callback,
        string baseUrl,
        string adminKey,
        bool allowUnsigned)
    {
        yield return SendSimple("GET", CurrentBackendMediaRoutes.MediaList, callback, baseUrl, adminKey, allowUnsigned);
    }

    public static IEnumerator Delete(
        string filename,
        Action<UnityPublisherRequestResult> callback,
        string baseUrl,
        string adminKey,
        bool allowUnsigned)
    {
        filename = CurrentBackendMediaRoutes.SanitizeFilename(filename);
        if (string.IsNullOrWhiteSpace(filename))
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail("Укажите filename для удаления."));
            yield break;
        }

        yield return SendSimple("DELETE", CurrentBackendMediaRoutes.MediaDelete(filename), callback, baseUrl, adminKey, allowUnsigned);
    }

    public static IEnumerator Upload(
        string filePath,
        Action<UnityPublisherRequestResult> callback,
        string baseUrl,
        string adminKey,
        bool allowUnsigned)
    {
        if (!CurrentBackendMediaPolicy.TryValidateUploadFile(filePath, out string validationError))
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail(validationError));
            yield break;
        }

        string url = BuildUrl(CurrentBackendMediaRoutes.MediaUpload, baseUrl, out string error);
        if (!PrepareRequest(url, adminKey, allowUnsigned, out string key, out error))
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail(error));
            yield break;
        }

        byte[] bytes = File.ReadAllBytes(filePath);
        var form = new List<IMultipartFormSection>
        {
            new MultipartFormFileSection("file", bytes, Path.GetFileName(filePath), CurrentBackendMediaPolicy.ContentTypeFor(filePath))
        };

        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
            request.timeout = 120;
            request.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrWhiteSpace(key))
                request.SetRequestHeader("X-Admin-Key", key.Trim());
            yield return request.SendWebRequest();
            callback?.Invoke(UnityPublisherRequestResult.FromRequest(request, MaxResponseChars));
        }
    }

    private static IEnumerator SendSimple(
        string method,
        string path,
        Action<UnityPublisherRequestResult> callback,
        string baseUrl,
        string adminKey,
        bool allowUnsigned)
    {
        string url = BuildUrl(path, baseUrl, out string error);
        if (!PrepareRequest(url, adminKey, allowUnsigned, out string key, out error))
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail(error));
            yield break;
        }

        using (var request = new UnityWebRequest(url, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 30;
            request.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrWhiteSpace(key))
                request.SetRequestHeader("X-Admin-Key", key.Trim());
            yield return request.SendWebRequest();
            callback?.Invoke(UnityPublisherRequestResult.FromRequest(request, MaxResponseChars));
        }
    }

    private static bool PrepareRequest(
        string url,
        string adminKey,
        bool allowUnsigned,
        out string key,
        out string error)
    {
        key = FirstNonEmpty(adminKey, Environment.GetEnvironmentVariable(CurrentBackendCatalogClient.AdminKeyEnvironmentVariable));
        error = "";
        if (string.IsNullOrWhiteSpace(url))
            return Fail("Адрес media API некорректный.", out error);
        if (string.IsNullOrWhiteSpace(key) && !allowUnsigned && !IsLoopback(url))
            return Fail("Для media API нужен X-Admin-Key.", out error);
        return true;
    }

    private static string BuildUrl(string path, string baseUrl, out string error)
    {
        error = "";
        if (!CurrentBackendMediaRoutes.IsKnownPath(path))
            return FailString("Неизвестный media route.", out error);
        string root = FirstNonEmpty(baseUrl, ApiRoutes.BaseUrl).Trim().TrimEnd('/');
        if (!Uri.TryCreate(root, UriKind.Absolute, out Uri uri))
            return FailString("Адрес сервера некорректный.", out error);
        if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            return FailString("Адрес сервера должен использовать HTTPS или локальный HTTP.", out error);
        return root + path;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }

    private static string FailString(string message, out string error)
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
