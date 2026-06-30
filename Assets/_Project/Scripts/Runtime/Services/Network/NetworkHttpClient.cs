using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

internal sealed class NetworkHttpClient
{
    private const string JsonContentType = "application/json";
    private const int MaxJsonRequestChars = 1024 * 1024;
    private const int MaxJsonResponseChars = 2 * 1024 * 1024;
    private const int MaxBearerTokenChars = 8192;
    private const int MaxErrorMessageChars = 256;

    private readonly Func<string> _baseUrlProvider;
    private readonly Func<NetworkRuntimeConfigData> _configProvider;
    private readonly Action<NetworkErrorKind, string> _setLastError;
    private readonly Action<bool, string> _updateConnectivityState;
    private readonly NetworkApiRateLimiter _rateLimiter = new NetworkApiRateLimiter();
    private readonly Dictionary<UnityWebRequest, int> _requestPayloadChars =
        new Dictionary<UnityWebRequest, int>();

    public NetworkHttpClient(
        Func<string> baseUrlProvider,
        Func<NetworkRuntimeConfigData> configProvider,
        Action<NetworkErrorKind, string> setLastError,
        Action<bool, string> updateConnectivityState)
    {
        _baseUrlProvider = baseUrlProvider;
        _configProvider = configProvider;
        _setLastError = setLastError;
        _updateConnectivityState = updateConnectivityState;
    }

    public string BuildUrl(string path)
    {
        string root = (_baseUrlProvider?.Invoke() ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(root))
            return "";

        if (!Uri.TryCreate(root, UriKind.Absolute, out Uri baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
        {
            AppLogger.Error(
                AppLogCategory.Server,
                nameof(NetworkHttpClient),
                nameof(BuildUrl),
                "Invalid API base URL. Only http/https URLs are allowed.",
                null,
                LogMetadata.Of("baseUrlChars", root.Length),
                recoverable: false);
            return "";
        }

        if (baseUri.Scheme == Uri.UriSchemeHttp && !CanUseInsecureHttp(baseUri))
        {
            AppLogger.Error(
                AppLogCategory.Security,
                nameof(NetworkHttpClient),
                nameof(BuildUrl),
                "Insecure HTTP API base URL is blocked outside editor/development loopback.",
                null,
                LogMetadata.Of("host", baseUri.Host),
                recoverable: false);
            return "";
        }

        string safePath = NormalizeRelativePath(path);
        if (safePath == null)
            return "";

        return new Uri(baseUri, safePath).ToString();
    }

    public UnityWebRequest CreateGetRequest(string path, string token)
    {
        if (!ValidateAuthRequirement("GET", path, token))
            return null;

        string url = BuildUrl(path);
        if (string.IsNullOrEmpty(url))
            return null;

        UnityWebRequest request = UnityWebRequest.Get(url);
        ApplyAuthorizationHeader(request, token);
        ApplyJsonHeaders(request);
        return request;
    }

    public UnityWebRequest CreateDeleteRequest(string path, string token)
    {
        if (!ValidateAuthRequirement("DELETE", path, token))
            return null;

        string url = BuildUrl(path);
        if (string.IsNullOrEmpty(url))
            return null;

        UnityWebRequest request = UnityWebRequest.Delete(url);
        request.downloadHandler = new DownloadHandlerBuffer();
        ApplyAuthorizationHeader(request, token);
        ApplyJsonHeaders(request);
        return request;
    }

    public UnityWebRequest CreateJsonPostRequest(string path, string jsonBody, string token)
    {
        if (!ValidateAuthRequirement("POST", path, token))
            return null;

        string url = BuildUrl(path);
        if (string.IsNullOrEmpty(url))
            return null;

        string body = string.IsNullOrWhiteSpace(jsonBody) ? "{}" : jsonBody;
        if (body.Length > MaxJsonRequestChars)
        {
            AppLogger.Warn(
                AppLogCategory.Network,
                nameof(NetworkHttpClient),
                nameof(CreateJsonPostRequest),
                "JSON request body is too large and was blocked.",
                LogMetadata.Of(
                    "path", ApiContract.RedactedEndpointForLog(path),
                    "payloadChars", body.Length,
                    "maxPayloadChars", MaxJsonRequestChars),
                recoverable: true);
            return null;
        }

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", JsonContentType);
        ApplyAuthorizationHeader(request, token);
        ApplyJsonHeaders(request);
        _requestPayloadChars[request] = body.Length;
        return request;
    }

    public void ApplyAuthorizationHeader(UnityWebRequest request, string token)
    {
        if (request == null || string.IsNullOrWhiteSpace(token))
            return;

        string safeToken = token.Trim();
        if (safeToken.Length > MaxBearerTokenChars || ContainsUnsafeHeaderValueChars(safeToken))
        {
            AppLogger.Warn(
                AppLogCategory.Security,
                nameof(NetworkHttpClient),
                nameof(ApplyAuthorizationHeader),
                "Authorization token was rejected because it contains unsafe characters.",
                LogMetadata.Of("tokenChars", safeToken.Length),
                recoverable: true);
            return;
        }

        request.SetRequestHeader("Authorization", "Bearer " + safeToken);
    }

    private bool ValidateAuthRequirement(string method, string path, string token)
    {
        string normalizedPath = ApiContract.NormalizePath(path);
        if (ApiContract.IsAdminOrPublisher(normalizedPath))
        {
            AppLogger.Error(
                AppLogCategory.Security,
                nameof(NetworkHttpClient),
                nameof(ValidateAuthRequirement),
                "Runtime HTTP client blocked an admin or Unity Publisher endpoint.",
                null,
                LogMetadata.Of(
                    "method", ApiContract.NormalizeMethod(method),
                    "path", normalizedPath),
                recoverable: false);
            return false;
        }

        if (ApiContract.RequiresBearerToken(method, normalizedPath) && string.IsNullOrWhiteSpace(token))
        {
            ThrottledAppLogger.Warn(
                nameof(NetworkHttpClient) + ".MissingBearer:" + ApiContract.NormalizeMethod(method) + ":" + normalizedPath,
                AppLogCategory.Security,
                nameof(NetworkHttpClient),
                nameof(ValidateAuthRequirement),
                "Protected API endpoint was blocked before send because bearer token is missing.",
                LogMetadata.Of(
                    "method", ApiContract.NormalizeMethod(method),
                    "path", normalizedPath,
                    "authRequirement", ApiAuthRequirement.BearerJwt));
            return false;
        }

        return true;
    }

    private int ResolvePayloadChars(UnityWebRequest request)
    {
        if (request != null && _requestPayloadChars.TryGetValue(request, out int chars))
            return chars;

        return 0;
    }

    private void DisposeRequest(UnityWebRequest request)
    {
        if (request == null)
            return;

        _requestPayloadChars.Remove(request);
        request.Dispose();
    }

    private static string CreateRequestId()
    {
        string guid = Guid.NewGuid().ToString("N");
        return "req_" + guid.Substring(0, 12);
    }

    public IEnumerator SendRequest(
        Func<UnityWebRequest> requestFactory,
        Action<NetworkRequestResult> callback,
        bool allowRetry)
    {
        long operationStartedAt = AppDiagnostics.StartTimer();
        string requestId = CreateRequestId();
        NetworkRequestResult lastResult = new NetworkRequestResult
        {
            Error = "Request was not created.",
            Kind = NetworkErrorKind.ClientError,
            RequestId = requestId
        };

        NetworkRuntimeConfigData config = _configProvider?.Invoke();
        int retryCount = config != null ? config.GetRetryCount() : 0;
        string lastMethod = "";
        string lastPath = "";
        string lastCategory = AppLogCategory.Network;

        for (int attempt = 0; attempt <= retryCount; attempt++)
        {
            UnityWebRequest request = requestFactory?.Invoke();
            if (request == null || string.IsNullOrEmpty(request.url))
            {
                lastResult.AttemptCount = attempt + 1;
                long durationMs = AppDiagnostics.ElapsedMilliseconds(operationStartedAt);
                AppLogger.Warn(
                    AppLogCategory.Network,
                    nameof(NetworkHttpClient),
                    nameof(SendRequest),
                    "HTTP request was not created.",
                    LogMetadata.Of("requestId", requestId, "attempt", attempt + 1, "allowRetry", allowRetry),
                    durationMs,
                    recoverable: true);
                callback?.Invoke(lastResult);
                yield break;
            }

            long attemptStartedAt = AppDiagnostics.StartTimer();
            string method = request.method ?? "";
            string path = ExtractSafeRequestPath(request.url);
            string category = ResolveRequestCategory(method, path);
            ApiEndpoint endpoint = ApiContract.Find(method, path);
            int payloadChars = ResolvePayloadChars(request);
            string rateGroups = string.Join(",", ApiContract.GetRateLimitGroups(method, path).ToArray());
            lastMethod = method;
            lastPath = path;
            lastCategory = category;
            var requestMetadata = LogMetadata.Of(
                "requestId", requestId,
                "method", method,
                "path", path,
                "endpoint", endpoint != null ? endpoint.Name : "",
                "endpointKind", endpoint != null ? endpoint.Kind.ToString() : "unknown",
                "rateGroups", rateGroups,
                "payloadChars", payloadChars,
                "attempt", attempt + 1,
                "maxAttempts", retryCount + 1,
                "allowRetry", allowRetry);

            yield return _rateLimiter.WaitForSlot(request.method, request.url);

            ApplyRequestDefaults(request);
            yield return request.SendWebRequest();

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : "";
            if (responseText.Length > MaxJsonResponseChars)
            {
                lastResult = new NetworkRequestResult
                {
                    Error = "Response body is too large.",
                    ResponseCode = request.responseCode,
                    Result = request.result,
                    Kind = NetworkErrorKind.InvalidResponse,
                    RequestId = requestId,
                    Method = method,
                    Path = path,
                    AttemptCount = attempt + 1,
                    PayloadChars = payloadChars
                };

                long durationMs = AppDiagnostics.ElapsedMilliseconds(attemptStartedAt);
                AppLogger.Warn(
                    category,
                    nameof(NetworkHttpClient),
                    nameof(SendRequest),
                    "HTTP response body exceeded the safe size limit.",
                    LogMetadata.Of(
                        "method", method,
                        "path", path,
                        "requestId", requestId,
                        "statusCode", request.responseCode,
                        "kind", lastResult.Kind,
                        "responseChars", responseText.Length,
                        "maxResponseChars", MaxJsonResponseChars),
                    durationMs,
                    recoverable: true);
                AppDiagnostics.LogIfSlow(category, nameof(NetworkHttpClient), nameof(SendRequest), durationMs, requestMetadata);
                _setLastError?.Invoke(lastResult.Kind, lastResult.Error);
                _updateConnectivityState?.Invoke(true, lastResult.Error);
                DisposeRequest(request);
                break;
            }

            lastResult = new NetworkRequestResult
            {
                Text = responseText,
                Error = FormatRequestError(request),
                ResponseCode = request.responseCode,
                Result = request.result,
                Kind = ClassifyRequestResult(request),
                RequestId = requestId,
                Method = method,
                Path = path,
                AttemptCount = attempt + 1,
                PayloadChars = payloadChars
            };

            _setLastError?.Invoke(
                lastResult.Kind,
                lastResult.Kind == NetworkErrorKind.Success ? "" : lastResult.Error);

            bool isSuccess = lastResult.IsSuccess || lastResult.ResponseCode == 304;
            if (isSuccess)
            {
                lastResult.Kind = NetworkErrorKind.Success;
                _updateConnectivityState?.Invoke(true, null);
                long durationMs = AppDiagnostics.ElapsedMilliseconds(attemptStartedAt);
                AppLogger.DebugLog(
                    category,
                    nameof(NetworkHttpClient),
                    nameof(SendRequest),
                    "HTTP request completed.",
                    LogMetadata.Of(
                        "requestId", requestId,
                        "method", method,
                        "path", path,
                        "statusCode", lastResult.ResponseCode,
                        "attempt", attempt + 1,
                        "rateGroups", rateGroups,
                        "payloadChars", payloadChars),
                    durationMs);
                AppDiagnostics.LogIfSlow(category, nameof(NetworkHttpClient), nameof(SendRequest), durationMs, requestMetadata);
                callback?.Invoke(lastResult);
                DisposeRequest(request);
                yield break;
            }

            bool offlineLike = IsOfflineLike(lastResult);
            _updateConnectivityState?.Invoke(!offlineLike, lastResult.Error);

            bool canRetry = allowRetry &&
                            attempt < retryCount &&
                            ShouldRetry(lastResult, config);

            DisposeRequest(request);

            long failedDurationMs = AppDiagnostics.ElapsedMilliseconds(attemptStartedAt);
            AppLogger.Warn(
                category,
                nameof(NetworkHttpClient),
                nameof(SendRequest),
                canRetry ? "HTTP request failed; retry scheduled." : "HTTP request failed.",
                LogMetadata.Of(
                    "method", method,
                    "path", path,
                    "requestId", requestId,
                    "statusCode", lastResult.ResponseCode,
                    "kind", lastResult.Kind,
                    "result", lastResult.Result,
                    "attempt", attempt + 1,
                    "rateGroups", rateGroups,
                    "payloadChars", payloadChars,
                    "willRetry", canRetry,
                    "error", lastResult.Error),
                failedDurationMs,
                recoverable: canRetry || offlineLike);
            AppDiagnostics.LogIfSlow(category, nameof(NetworkHttpClient), nameof(SendRequest), failedDurationMs, requestMetadata);

            if (!canRetry)
                break;

            float retryDelay = config != null ? config.GetRetryDelaySeconds(attempt + 1) : 0f;
            var retryMetadata = LogMetadata.Of(
                "method", method,
                "path", path,
                "requestId", requestId,
                "statusCode", lastResult.ResponseCode,
                "kind", lastResult.Kind,
                "attempt", attempt + 1,
                "retryCount", retryCount,
                "retryDelay", retryDelay,
                "rateGroups", rateGroups,
                "payloadChars", payloadChars);
            ThrottledAppLogger.Warn(
                "NetworkRetry:" + method + ":" + path + ":" + lastResult.ResponseCode,
                category,
                nameof(NetworkHttpClient),
                nameof(SendRequest),
                "HTTP request failed; retry scheduled.",
                retryMetadata);
            if (IsSaveProgressPath(path))
            {
                ThrottledAppLogger.Warn(
                    "SaveProgressRetry:" + lastResult.ResponseCode,
                    AppLogCategory.SaveSystem,
                    nameof(NetworkHttpClient),
                    nameof(SendRequest),
                    "Save progress request failed; retry scheduled.",
                    retryMetadata);
            }

            if (retryDelay > 0f)
                yield return new WaitForSeconds(retryDelay);
        }

        var finalMetadata = LogMetadata.Of(
            "requestId", requestId,
            "method", lastMethod,
            "path", lastPath,
            "statusCode", lastResult.ResponseCode,
            "kind", lastResult.Kind,
            "result", lastResult.Result,
            "error", lastResult.Error,
            "attempts", lastResult.AttemptCount,
            "payloadChars", lastResult.PayloadChars,
            "allowRetry", allowRetry);
        AppLogger.Error(
            lastCategory,
            nameof(NetworkHttpClient),
            nameof(SendRequest),
            "HTTP request failed after all attempts.",
            null,
            finalMetadata,
            recoverable: IsOfflineLike(lastResult));
        if (IsSaveProgressPath(lastPath))
        {
            AppLogger.Error(
                AppLogCategory.SaveSystem,
                nameof(NetworkHttpClient),
                nameof(SendRequest),
                "Save progress request failed after all attempts.",
                null,
                finalMetadata,
                recoverable: IsOfflineLike(lastResult));
        }

        callback?.Invoke(lastResult);
    }

    private static bool IsSaveProgressPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return ApiContract.IsSaveProgress("POST", path);
    }

    private static string ExtractSafeRequestPath(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            return string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "/" : uri.AbsolutePath;

        int queryIndex = url.IndexOfAny(new[] { '?', '#' });
        string path = queryIndex >= 0 ? url.Substring(0, queryIndex) : url;
        return string.IsNullOrWhiteSpace(path) ? "/" : path;
    }

    private static string ResolveRequestCategory(string method, string path)
    {
        return ApiContract.ResolveLogCategory(method, path);
    }

    private static string NormalizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        string trimmedPath = path.Trim().Replace('\\', '/').Replace("\r", "").Replace("\n", "");
        if (trimmedPath.StartsWith("//", StringComparison.Ordinal))
        {
            AppLogger.Warn(
                AppLogCategory.Security,
                nameof(NetworkHttpClient),
                nameof(NormalizeRelativePath),
                "Scheme-relative request path was blocked.",
                LogMetadata.Of("pathChars", trimmedPath.Length),
                recoverable: true);
            return null;
        }

        if (Uri.TryCreate(trimmedPath, UriKind.Absolute, out _))
        {
            AppLogger.Warn(
                AppLogCategory.Security,
                nameof(NetworkHttpClient),
                nameof(NormalizeRelativePath),
                "Absolute request path was blocked.",
                LogMetadata.Of("pathChars", trimmedPath.Length),
                recoverable: true);
            return null;
        }

        if (ContainsUnsafePathSegments(trimmedPath))
        {
            AppLogger.Warn(
                AppLogCategory.Security,
                nameof(NetworkHttpClient),
                nameof(NormalizeRelativePath),
                "Unsafe request path segments were blocked.",
                LogMetadata.Of("pathChars", trimmedPath.Length),
                recoverable: true);
            return null;
        }

        return trimmedPath.StartsWith("/", StringComparison.Ordinal)
            ? trimmedPath
            : "/" + trimmedPath;
    }

    private static bool ContainsUnsafePathSegments(string path)
    {
        string pathOnly = path ?? "";
        int queryIndex = pathOnly.IndexOfAny(new[] { '?', '#' });
        if (queryIndex >= 0)
            pathOnly = pathOnly.Substring(0, queryIndex);

        if (pathOnly.IndexOf("%2e", StringComparison.OrdinalIgnoreCase) >= 0 ||
            pathOnly.IndexOf("%2f", StringComparison.OrdinalIgnoreCase) >= 0 ||
            pathOnly.IndexOf("%5c", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        string decodedPath;
        try
        {
            decodedPath = Uri.UnescapeDataString(pathOnly).Replace('\\', '/');
        }
        catch (Exception exception)
        {
            ThrottledAppLogger.Warn(
                nameof(NetworkHttpClient) + ".ContainsUnsafePathSegments.Decode",
                AppLogCategory.Network,
                nameof(NetworkHttpClient),
                nameof(ContainsUnsafePathSegments),
                "Request path could not be URL-decoded and was blocked.",
                LogMetadata.Of(
                    "pathChars", pathOnly != null ? pathOnly.Length : 0,
                    "errorType", exception.GetType().Name));
            return true;
        }

        string[] segments = decodedPath.Split('/');
        foreach (string segment in segments)
        {
            if (segment == "." || segment == "..")
                return true;
        }

        return false;
    }

    private static bool CanUseInsecureHttp(Uri uri)
    {
        if (uri == null || uri.Scheme != Uri.UriSchemeHttp)
            return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (uri.IsLoopback)
            return true;
#endif

        return false;
    }

    private void ApplyRequestDefaults(UnityWebRequest request)
    {
        if (request == null)
            return;

        NetworkRuntimeConfigData config = _configProvider?.Invoke();
        request.timeout = config != null ? config.GetRequestTimeoutSeconds() : 15;
    }

    private static void ApplyJsonHeaders(UnityWebRequest request)
    {
        if (request == null)
            return;

        request.SetRequestHeader("Accept", JsonContentType);
        request.SetRequestHeader("Cache-Control", "no-store");
    }

    private static bool ContainsUnsafeHeaderValueChars(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsControl(c))
                return true;
        }

        return false;
    }

    private static bool ShouldRetry(NetworkRequestResult result, NetworkRuntimeConfigData config)
    {
        if (result == null)
            return false;

        if (result.Kind == NetworkErrorKind.Offline ||
            result.Kind == NetworkErrorKind.Timeout)
            return true;

        if (config != null && config.retryServerErrors)
        {
            return result.ResponseCode == 408 ||
                   result.ResponseCode == 429 ||
                   result.ResponseCode >= 500;
        }

        return false;
    }

    private static bool IsOfflineLike(NetworkRequestResult result)
    {
        if (result == null)
            return false;

        return result.Kind == NetworkErrorKind.Offline ||
               result.Kind == NetworkErrorKind.Timeout;
    }

    private static NetworkErrorKind ClassifyRequestResult(UnityWebRequest request)
    {
        if (request == null)
            return NetworkErrorKind.ClientError;

        if (request.result == UnityWebRequest.Result.Success || request.responseCode == 304)
            return NetworkErrorKind.Success;

        if (request.responseCode == 401)
            return NetworkErrorKind.Unauthorized;
        if (request.responseCode == 402)
            return NetworkErrorKind.PaymentRequired;
        if (request.responseCode == 408)
            return NetworkErrorKind.Timeout;
        if (request.responseCode == 429)
            return NetworkErrorKind.ServerError;
        if (request.responseCode >= 500)
            return NetworkErrorKind.ServerError;
        if (request.responseCode >= 400)
            return NetworkErrorKind.ClientError;
        if (request.result == UnityWebRequest.Result.DataProcessingError)
            return NetworkErrorKind.InvalidResponse;

        string error = request.error ?? "";
        if (error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0 ||
            error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
            return NetworkErrorKind.Timeout;

        if (request.result == UnityWebRequest.Result.ConnectionError)
            return NetworkErrorKind.Offline;

        return NetworkErrorKind.ClientError;
    }

    private static string FormatRequestError(UnityWebRequest request)
    {
        if (request == null)
            return "Unknown request error";

        string rawError = string.IsNullOrEmpty(request.error) ? "Request failed" : request.error;
        string message = request.responseCode > 0
            ? $"{request.responseCode} {rawError}"
            : rawError;
        string safe = SaveDataSanitizer.SanitizeHistoryLine(message);
        return safe.Length <= MaxErrorMessageChars ? safe : safe.Substring(0, MaxErrorMessageChars);
    }
}

internal sealed class NetworkApiRateLimiter
{
    private const int AuthLimit = 10;
    private const float AuthWindowSeconds = 60f;
    private const int AdminAuthLimit = 5;
    private const float AdminAuthWindowSeconds = 15f * 60f;
    private const int PlayerLimit = 60;
    private const float PlayerWindowSeconds = 60f;
    private const int SensitiveLimit = 5;
    private const float SensitiveWindowSeconds = 60f;

    private readonly Dictionary<string, SlidingWindowBucket> _buckets =
        new Dictionary<string, SlidingWindowBucket>(StringComparer.Ordinal);

    public IEnumerator WaitForSlot(string method, string url)
    {
        List<string> groups = ResolveGroups(method, url);
        if (groups.Count == 0)
            yield break;

        float warnedAt = -999f;
        while (true)
        {
            float now = Time.realtimeSinceStartup;
            float wait = GetMaxWaitSeconds(groups, now);
            if (wait <= 0f)
            {
                Reserve(groups, now);
                yield break;
            }

            if (now - warnedAt > 5f)
            {
                warnedAt = now;
                ThrottledAppLogger.Warn(
                    nameof(NetworkApiRateLimiter) + ".Delay:" + string.Join(",", groups.ToArray()),
                    AppLogCategory.Network,
                    nameof(NetworkApiRateLimiter),
                    nameof(WaitForSlot),
                    "Client rate limit delayed an API request.",
                    LogMetadata.Of(
                        "rateGroups", string.Join(",", groups.ToArray()),
                        "waitSeconds", wait.ToString("0.0")));
            }

            yield return new WaitForSecondsRealtime(Mathf.Min(wait, 5f));
        }
    }

    private float GetMaxWaitSeconds(List<string> groups, float now)
    {
        float wait = 0f;
        for (int i = 0; i < groups.Count; i++)
            wait = Mathf.Max(wait, GetBucket(groups[i]).GetWaitSeconds(now));
        return wait;
    }

    private void Reserve(List<string> groups, float now)
    {
        for (int i = 0; i < groups.Count; i++)
            GetBucket(groups[i]).Reserve(now);
    }

    private SlidingWindowBucket GetBucket(string group)
    {
        if (_buckets.TryGetValue(group, out SlidingWindowBucket bucket))
            return bucket;

        switch (group)
        {
            case "auth":
                bucket = new SlidingWindowBucket(AuthLimit, AuthWindowSeconds);
                break;
            case "admin-auth":
                bucket = new SlidingWindowBucket(AdminAuthLimit, AdminAuthWindowSeconds);
                break;
            case "sensitive":
                bucket = new SlidingWindowBucket(SensitiveLimit, SensitiveWindowSeconds);
                break;
            default:
                bucket = new SlidingWindowBucket(PlayerLimit, PlayerWindowSeconds);
                break;
        }

        _buckets[group] = bucket;
        return bucket;
    }

    private static List<string> ResolveGroups(string method, string url)
    {
        string path = ExtractPath(url);
        return ApiContract.GetRateLimitGroups(method, path);
    }

    private static string ExtractPath(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            return uri.AbsolutePath;

        int queryIndex = url.IndexOfAny(new[] { '?', '#' });
        string path = queryIndex >= 0 ? url.Substring(0, queryIndex) : url;
        return path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
    }

    private sealed class SlidingWindowBucket
    {
        private readonly int _limit;
        private readonly float _windowSeconds;
        private readonly Queue<float> _timestamps = new Queue<float>();

        public SlidingWindowBucket(int limit, float windowSeconds)
        {
            _limit = Mathf.Max(1, limit);
            _windowSeconds = Mathf.Max(1f, windowSeconds);
        }

        public float GetWaitSeconds(float now)
        {
            Trim(now);
            if (_timestamps.Count < _limit)
                return 0f;

            return Mathf.Max(0f, _timestamps.Peek() + _windowSeconds - now);
        }

        public void Reserve(float now)
        {
            Trim(now);
            _timestamps.Enqueue(now);
        }

        private void Trim(float now)
        {
            while (_timestamps.Count > 0 && now - _timestamps.Peek() >= _windowSeconds)
                _timestamps.Dequeue();
        }
    }
}
