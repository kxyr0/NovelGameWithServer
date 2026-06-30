using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class NetworkManager
{
    private const float UiTextRefreshCooldownSeconds = 10f;
    private const int MaxUiTextItems = 1000;
    private const string DefaultUiTextLocale = "ru";

    private static readonly Dictionary<string, UiTextContextCache> _uiTextCaches =
        new Dictionary<string, UiTextContextCache>(StringComparer.Ordinal);
    private static readonly Dictionary<string, UiTextPendingRequest> _uiTextPendingRequests =
        new Dictionary<string, UiTextPendingRequest>(StringComparer.Ordinal);

    public static event Action OnUiTextsUpdated;

    public IEnumerator RefreshUiTexts(
        string screenId,
        string storyId,
        string locale,
        Action<bool, string> callback = null,
        bool force = false)
    {
        string safeLocale = ResolveUiTextLocale(locale);
        string safeScreenId = NormalizeUiTextContextValue(screenId);
        string safeStoryId = NormalizeUiTextContextValue(storyId);
        string contextKey = BuildUiTextContextKey(safeLocale, safeScreenId, safeStoryId);

        if (!IsAuthenticated)
        {
            RemoveUiTextCache(contextKey);
            callback?.Invoke(false, "Not authenticated.");
            yield break;
        }

        if (!force &&
            _uiTextCaches.TryGetValue(contextKey, out UiTextContextCache cache) &&
            cache != null &&
            cache.HasSuccessfulPayload &&
            Time.realtimeSinceStartup - cache.LoadedAtRealtime <= UiTextRefreshCooldownSeconds)
        {
            callback?.Invoke(true, "");
            yield break;
        }

        if (_uiTextPendingRequests.TryGetValue(contextKey, out UiTextPendingRequest pending) && pending != null)
        {
            pending.Add(callback);
            while (_uiTextPendingRequests.ContainsKey(contextKey))
                yield return null;

            yield break;
        }

        pending = new UiTextPendingRequest();
        pending.Add(callback);
        _uiTextPendingRequests[contextKey] = pending;

        string path = ApiRoutes.ContentUiTextsQuery(safeScreenId, safeStoryId, safeLocale);
        bool ok = false;
        string message = "";

        yield return GetRuntime(path, (json, err) =>
        {
            if (!string.IsNullOrEmpty(err))
            {
                RemoveUiTextCache(contextKey);
                ok = false;
                message = err;
                return;
            }

            if (!TryApplyUiTextResponse(safeScreenId, safeStoryId, safeLocale, json, out message))
            {
                RemoveUiTextCache(contextKey);
                ok = false;
                return;
            }

            ok = true;
            message = "";
        });

        _uiTextPendingRequests.Remove(contextKey);
        OnUiTextsUpdated?.Invoke();
        pending.Complete(ok, message);
    }

    public static bool TryGetUiText(string textId, string screenId, string storyId, string locale, out string text)
    {
        text = "";
        string targetId = NormalizeUiTextId(textId);
        if (string.IsNullOrEmpty(targetId))
            return false;

        string safeLocale = ResolveUiTextLocale(locale);
        string safeScreenId = NormalizeUiTextContextValue(screenId);
        string safeStoryId = NormalizeUiTextContextValue(storyId);

        var contextKeys = BuildUiTextLookupContextKeys(safeLocale, safeScreenId, safeStoryId);
        for (int i = 0; i < contextKeys.Count; i++)
        {
            if (!TryGetUiTextFromCache(
                    contextKeys[i],
                    targetId,
                    safeLocale,
                    safeScreenId,
                    safeStoryId,
                    out text,
                    out bool matchedHiddenText))
            {
                if (matchedHiddenText)
                    return false;

                continue;
            }

            return true;
        }

        return false;
    }

    private static bool TryGetUiTextFromCache(
        string contextKey,
        string targetId,
        string safeLocale,
        string safeScreenId,
        string safeStoryId,
        out string text,
        out bool matchedHiddenText)
    {
        text = "";
        matchedHiddenText = false;

        if (!_uiTextCaches.TryGetValue(contextKey, out UiTextContextCache cache) ||
            cache == null ||
            cache.Items == null)
        {
            return false;
        }

        RemoteUiTextItem best = FindBestUiTextCandidate(cache, contextKey, targetId, safeLocale, safeScreenId, safeStoryId);
        if (best == null || !best.enabled || string.IsNullOrWhiteSpace(best.text))
        {
            matchedHiddenText = best != null;
            return false;
        }

        text = best.text;
        return true;
    }

    private static RemoteUiTextItem FindBestUiTextCandidate(
        UiTextContextCache cache,
        string contextKey,
        string targetId,
        string safeLocale,
        string safeScreenId,
        string safeStoryId)
    {
        RemoteUiTextItem best = null;
        int bestScore = -1;
        for (int i = 0; i < cache.Items.Count; i++)
        {
            RemoteUiTextItem item = cache.Items[i];
            if (item == null || !string.Equals(item.NormalizedId, targetId, StringComparison.Ordinal))
                continue;

            if (!UiTextContextMatches(item.NormalizedLocale, safeLocale) ||
                !UiTextContextMatches(item.NormalizedScreenId, safeScreenId) ||
                !UiTextContextMatches(item.NormalizedStoryId, safeStoryId))
            {
                continue;
            }

            int score = UiTextSpecificityScore(item);
            if (score > bestScore)
            {
                best = item;
                bestScore = score;
                continue;
            }

            if (score == bestScore && best != null)
            {
                ThrottledAppLogger.Warn(
                    nameof(NetworkManager) + ".UiTextDuplicate:" + targetId + ":" + contextKey,
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(TryGetUiText),
                    "Duplicate remote UI text candidate with equal specificity; first item is used.",
                    LogMetadata.Of(
                        "textId", targetId,
                        "locale", safeLocale,
                        "screenId", safeScreenId,
                        "storyId", safeStoryId,
                        "existingOrder", best.Order,
                        "duplicateOrder", item.Order));
            }
        }

        return best;
    }

    public static string ResolveUiTextLocale(string localeOverride = "")
    {
        string locale = NormalizeUiTextContextValue(localeOverride);
        if (!string.IsNullOrEmpty(locale))
            return locale;

        locale = NormalizeUiTextContextValue(CurrentProfile != null ? CurrentProfile.locale : "");
        return string.IsNullOrEmpty(locale) ? DefaultUiTextLocale : locale;
    }

    public static string ResolveActiveStoryIdForUiTexts()
    {
        return ResolveActiveStoryIdForNetwork();
    }

    public static List<RemoteUiTextItem> ParseUiTextResponse(string json)
    {
        var result = new List<RemoteUiTextItem>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        string trimmed = json.Trim();
        string rawItems = trimmed.StartsWith("[", StringComparison.Ordinal)
            ? trimmed
            : NetworkJson.GetRawValue(trimmed, "items");

        if (string.IsNullOrWhiteSpace(rawItems))
            return result;

        int order = 0;
        foreach (string rawItem in NetworkJson.GetArrayItems(rawItems))
        {
            if (result.Count >= MaxUiTextItems)
                break;

            RemoteUiTextItem item = ParseUiTextItem(rawItem, order++);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    private static bool TryApplyUiTextResponse(
        string screenId,
        string storyId,
        string locale,
        string json,
        out string error)
    {
        error = "";
        if (!NetworkJson.LooksLikeJsonObject(json) && (json == null || !json.TrimStart().StartsWith("[", StringComparison.Ordinal)))
        {
            error = "UI text response is not a JSON object or array.";
            return false;
        }

        string safeLocale = ResolveUiTextLocale(locale);
        string safeScreenId = NormalizeUiTextContextValue(screenId);
        string safeStoryId = NormalizeUiTextContextValue(storyId);
        string contextKey = BuildUiTextContextKey(safeLocale, safeScreenId, safeStoryId);
        string version = NetworkJson.LooksLikeJsonObject(json) ? SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(json, "version")) : "";
        string updatedAt = NetworkJson.LooksLikeJsonObject(json) ? SaveDataSanitizer.SanitizeSavedAtIso(NetworkJson.GetString(json, "updatedAt")) : "";

        var cache = new UiTextContextCache
        {
            Items = ParseUiTextResponse(json),
            LoadedAtRealtime = Time.realtimeSinceStartup,
            HasSuccessfulPayload = true,
            Version = version,
            UpdatedAt = updatedAt
        };

        _uiTextCaches[contextKey] = cache;
        return true;
    }

    private static RemoteUiTextItem ParseUiTextItem(string rawItem, int order)
    {
        if (string.IsNullOrWhiteSpace(rawItem) || !NetworkJson.LooksLikeJsonObject(rawItem))
            return null;

        string id = NormalizeUiTextId(NetworkJson.GetString(rawItem, "id"));
        if (string.IsNullOrEmpty(id))
            return null;

        bool enabled = NetworkJson.GetRawValue(rawItem, "enabled") == null ||
                       NetworkJson.GetBool(rawItem, "enabled", true);
        string rawText = NetworkJson.GetString(rawItem, "text");
        string text = SanitizeUiTextValue(rawText);

        var item = new RemoteUiTextItem
        {
            id = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(rawItem, "id")),
            text = text,
            enabled = enabled,
            locale = NormalizeUiTextContextValue(NetworkJson.GetString(rawItem, "locale")),
            screenId = NormalizeUiTextContextValue(NetworkJson.GetString(rawItem, "screenId")),
            storyId = NormalizeUiTextContextValue(NetworkJson.GetString(rawItem, "storyId")),
            updatedAt = SaveDataSanitizer.SanitizeSavedAtIso(NetworkJson.GetString(rawItem, "updatedAt")),
            Order = order
        };

        item.NormalizedId = id;
        item.NormalizedLocale = NormalizeUiTextContextKey(item.locale);
        item.NormalizedScreenId = NormalizeUiTextContextKey(item.screenId);
        item.NormalizedStoryId = NormalizeUiTextContextKey(item.storyId);
        return item;
    }

    private static string SanitizeUiTextValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string limited = value.Length <= SaveDataSanitizer.MaxContentTextChars
            ? value
            : value.Substring(0, SaveDataSanitizer.MaxContentTextChars);
        string sanitized = SafeTextSanitizer.SanitizeStoryText(limited);
        return sanitized.Length <= SaveDataSanitizer.MaxContentTextChars
            ? sanitized
            : sanitized.Substring(0, SaveDataSanitizer.MaxContentTextChars).Trim();
    }

    private static void RemoveUiTextCache(string contextKey)
    {
        if (!string.IsNullOrEmpty(contextKey))
            _uiTextCaches.Remove(contextKey);
    }

    private static void ResetUiTextState()
    {
        _uiTextCaches.Clear();
        _uiTextPendingRequests.Clear();
        OnUiTextsUpdated = null;
    }

    private static string NormalizeUiTextId(string value)
    {
        return NormalizeUiTextContextKey(SaveDataSanitizer.SanitizeIdentifier(value));
    }

    private static string NormalizeUiTextContextValue(string value)
    {
        return SaveDataSanitizer.SanitizeIdentifier(value);
    }

    private static string NormalizeUiTextContextKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : SaveDataSanitizer.SanitizeIdentifier(value).ToLowerInvariant();
    }

    private static string BuildUiTextContextKey(string locale, string screenId, string storyId)
    {
        return NormalizeUiTextContextKey(locale) + "|" +
               NormalizeUiTextContextKey(screenId) + "|" +
               NormalizeUiTextContextKey(storyId);
    }

    private static List<string> BuildUiTextLookupContextKeys(string locale, string screenId, string storyId)
    {
        string safeLocale = NormalizeUiTextContextKey(locale);
        string safeScreenId = NormalizeUiTextContextKey(screenId);
        string safeStoryId = NormalizeUiTextContextKey(storyId);
        var keys = new List<string>(8);

        AddUiTextLookupContextKey(keys, safeLocale, safeScreenId, safeStoryId);
        AddUiTextLookupContextKey(keys, safeLocale, safeScreenId, "");
        AddUiTextLookupContextKey(keys, safeLocale, "", safeStoryId);
        AddUiTextLookupContextKey(keys, safeLocale, "", "");
        AddUiTextLookupContextKey(keys, "", safeScreenId, safeStoryId);
        AddUiTextLookupContextKey(keys, "", safeScreenId, "");
        AddUiTextLookupContextKey(keys, "", "", safeStoryId);
        AddUiTextLookupContextKey(keys, "", "", "");

        return keys;
    }

    private static void AddUiTextLookupContextKey(List<string> keys, string locale, string screenId, string storyId)
    {
        string key = BuildUiTextContextKey(locale, screenId, storyId);
        if (!keys.Contains(key))
            keys.Add(key);
    }

    private static bool UiTextContextMatches(string itemValue, string requestedValue)
    {
        return string.IsNullOrEmpty(itemValue) ||
               string.Equals(itemValue, NormalizeUiTextContextKey(requestedValue), StringComparison.Ordinal);
    }

    private static int UiTextSpecificityScore(RemoteUiTextItem item)
    {
        int score = 0;
        if (!string.IsNullOrEmpty(item.NormalizedLocale))
            score++;
        if (!string.IsNullOrEmpty(item.NormalizedScreenId))
            score++;
        if (!string.IsNullOrEmpty(item.NormalizedStoryId))
            score++;
        return score;
    }

    private sealed class UiTextContextCache
    {
        public List<RemoteUiTextItem> Items = new List<RemoteUiTextItem>();
        public float LoadedAtRealtime;
        public bool HasSuccessfulPayload;
        public string Version = "";
        public string UpdatedAt = "";
    }

    private sealed class UiTextPendingRequest
    {
        private readonly List<Action<bool, string>> _callbacks = new List<Action<bool, string>>();

        public void Add(Action<bool, string> callback)
        {
            if (callback != null)
                _callbacks.Add(callback);
        }

        public void Complete(bool ok, string message)
        {
            for (int i = 0; i < _callbacks.Count; i++)
                _callbacks[i]?.Invoke(ok, message);
        }
    }
}

// DTO for backend JSON and parsed remote UI text state.
[Serializable]
public sealed class RemoteUiTextItem
{
    public string id = "";
    public string text = "";
    public bool enabled = true;
    public string locale = "";
    public string screenId = "";
    public string storyId = "";
    public string updatedAt = "";

    [NonSerialized] public int Order;
    [NonSerialized] public string NormalizedId = "";
    [NonSerialized] public string NormalizedLocale = "";
    [NonSerialized] public string NormalizedScreenId = "";
    [NonSerialized] public string NormalizedStoryId = "";
}
