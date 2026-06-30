using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NetworkEnvironmentEntry
{
    public string id = "";
    public string displayName = "";
    public string baseUrl = "";
}

[Serializable]
public class NetworkRuntimeConfigData
{
    public string selectedEnvironmentId = "prod";
    public List<NetworkEnvironmentEntry> environments = new List<NetworkEnvironmentEntry>();
    public int requestTimeoutSeconds = 15;
    public int maxRetries = 2;
    public float retryDelaySeconds = 1f;
    public bool retryServerErrors = true;
    public bool showOfflineToasts = true;
    public string offlineMessage = "\u041d\u0435\u0442 \u0441\u043e\u0435\u0434\u0438\u043d\u0435\u043d\u0438\u044f. \u0420\u0430\u0431\u043e\u0442\u0430\u0435\u043c \u0441 \u043b\u043e\u043a\u0430\u043b\u044c\u043d\u044b\u043c\u0438 \u0434\u0430\u043d\u043d\u044b\u043c\u0438.";
    public string onlineMessage = "\u0421\u043e\u0435\u0434\u0438\u043d\u0435\u043d\u0438\u0435 \u0432\u043e\u0441\u0441\u0442\u0430\u043d\u043e\u0432\u043b\u0435\u043d\u043e.";

    public string ResolveSelectedEnvironmentId()
    {
        if (!string.IsNullOrWhiteSpace(selectedEnvironmentId))
            return selectedEnvironmentId.Trim();

        var environment = ResolveSelectedEnvironment();
        return environment != null && !string.IsNullOrWhiteSpace(environment.id)
            ? environment.id.Trim()
            : "";
    }

    public NetworkEnvironmentEntry ResolveSelectedEnvironment()
    {
        var requestedId = string.IsNullOrWhiteSpace(selectedEnvironmentId)
            ? ""
            : selectedEnvironmentId.Trim();

        if (environments != null)
        {
            foreach (var environment in environments)
            {
                if (environment == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(requestedId) &&
                    string.Equals(environment.id, requestedId, StringComparison.OrdinalIgnoreCase))
                    return environment;
            }

            foreach (var environment in environments)
            {
                if (environment != null && !string.IsNullOrWhiteSpace(environment.baseUrl))
                    return environment;
            }

            foreach (var environment in environments)
            {
                if (environment != null)
                    return environment;
            }
        }

        return null;
    }

    public string ResolveBaseUrl(string fallback = "")
    {
        var environment = ResolveSelectedEnvironment();
        var configuredUrl = environment != null ? environment.baseUrl : "";
        if (!string.IsNullOrWhiteSpace(configuredUrl))
            return configuredUrl.Trim();

        return string.IsNullOrWhiteSpace(fallback) ? "" : fallback.Trim();
    }

    public int GetRequestTimeoutSeconds() => Mathf.Clamp(requestTimeoutSeconds, 1, 60);

    public int GetRetryCount() => Mathf.Clamp(maxRetries, 0, 5);

    public float GetRetryDelaySeconds(int attemptIndex)
    {
        return Mathf.Clamp(retryDelaySeconds, 0f, 15f) * Mathf.Max(1, attemptIndex);
    }
}

public static class NetworkRuntimeConfigLoader
{
    public const string DefaultResourcePath = "NovelTemplate/network-runtime-config";

    static NetworkRuntimeConfigData _cached;

    public static NetworkRuntimeConfigData Load()
    {
        if (_cached != null)
            return _cached;

        var asset = Resources.Load<TextAsset>(DefaultResourcePath);
        if (asset == null)
        {
            AppLogger.Warn(
                AppLogCategory.Server,
                nameof(NetworkRuntimeConfigLoader),
                nameof(Load),
                "Runtime network config was not found; fallback configuration will be used.",
                LogMetadata.Of("resourcePath", DefaultResourcePath),
                recoverable: true);
            _cached = BuildFallback();
            return _cached;
        }

        try
        {
            _cached = JsonUtility.FromJson<NetworkRuntimeConfigData>(asset.text);
        }
        catch (Exception e)
        {
            AppLogger.Error(
                AppLogCategory.Server,
                nameof(NetworkRuntimeConfigLoader),
                nameof(Load),
                "Runtime network config JSON could not be parsed; fallback configuration will be used.",
                e,
                LogMetadata.Of("resourcePath", DefaultResourcePath),
                recoverable: true);
            _cached = BuildFallback();
        }

        if (_cached == null)
            _cached = BuildFallback();

        if (_cached.environments == null)
            _cached.environments = new List<NetworkEnvironmentEntry>();

        return _cached;
    }

    static NetworkRuntimeConfigData BuildFallback()
    {
        return new NetworkRuntimeConfigData
        {
            selectedEnvironmentId = "prod",
            environments = new List<NetworkEnvironmentEntry>
            {
                new NetworkEnvironmentEntry
                {
                    id = "prod",
                    displayName = "Production",
                    baseUrl = ApiRoutes.BaseUrl
                }
            }
        };
    }

#if UNITY_EDITOR
    public static void ResetCache()
    {
        _cached = null;
    }
#endif
}
