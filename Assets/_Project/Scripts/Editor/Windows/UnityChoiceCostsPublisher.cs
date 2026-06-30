#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using XNode;

public static class UnityChoiceCostsPublisher
{
    private const string ChoiceCostsPath = ApiRoutes.UnityChoiceCosts;
    private const string AdminKeyEnvironmentVariable = "NOCTURNEDC_ADMIN_KEY";
    private const int MaxEntries = 1000;
    private const int MaxPublisherBodyChars = 512 * 1024;
    private const int MaxPublisherResponseChars = 1024 * 1024;
    private const int MaxPublisherSecretChars = 4096;
    private const int PublisherSensitiveLimit = 5;
    private const double PublisherSensitiveWindowSeconds = 60d;

    private static readonly Queue<double> PublisherRequestTimestamps = new Queue<double>();

    public static List<StoryGraph> GetSelectedGraphs()
    {
        var result = new List<StoryGraph>();
        var seen = new HashSet<StoryGraph>();
        UnityEngine.Object[] selected = Selection.objects ?? Array.Empty<UnityEngine.Object>();

        foreach (UnityEngine.Object item in selected)
        {
            if (item is StoryGraph graph && graph != null && seen.Add(graph))
                result.Add(graph);
        }

        return result;
    }

    public static List<StoryGraph> GetAllProjectGraphs()
    {
        var result = new List<StoryGraph>();
        string[] guids = AssetDatabase.FindAssets("t:StoryGraph");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var graph = AssetDatabase.LoadAssetAtPath<StoryGraph>(path);
            if (graph != null)
                result.Add(graph);
        }

        return result;
    }

    public static UnityChoiceCostsPublishPayload BuildPayload(
        IEnumerable<StoryGraph> graphs,
        string storyIdOverride = "",
        string episodeIdOverride = "")
    {
        var payload = new UnityChoiceCostsPublishPayload();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        storyIdOverride = SaveDataSanitizer.SanitizeIdentifier(storyIdOverride);
        episodeIdOverride = SaveDataSanitizer.SanitizeIdentifier(episodeIdOverride);

        if (graphs == null)
            return payload;

        foreach (StoryGraph graph in graphs)
        {
            if (graph == null || payload.costs.Count >= MaxEntries)
                continue;

            GraphContext context = ResolveGraphContext(graph);
            if (!string.IsNullOrEmpty(storyIdOverride))
                context.storyId = storyIdOverride;
            if (!string.IsNullOrEmpty(episodeIdOverride))
                context.episodeId = episodeIdOverride;

            AddGraphChoiceCosts(payload, seen, graph, context);
        }

        payload.storyId = ResolveSingleValue(payload.costs, entry => entry.storyId);
        payload.episodeId = ResolveSingleValue(payload.costs, entry => entry.episodeId);
        payload.choices = new List<UnityChoiceCostEntry>(payload.costs);
        payload.items = new List<UnityChoiceCostEntry>(payload.costs);
        payload.choiceCosts = new List<UnityChoiceCostEntry>(payload.costs);
        payload.generatedAt = DateTime.UtcNow.ToString("o");
        payload.source = "unity-editor";
        return payload;
    }

    public static string ToJson(UnityChoiceCostsPublishPayload payload, bool pretty)
    {
        if (payload == null)
            return "{}";

        return JsonUtility.ToJson(payload, pretty);
    }

    public static IEnumerator Publish(
        UnityChoiceCostsPublishPayload payload,
        Action<UnityPublisherRequestResult> callback,
        string baseUrlOverride = "",
        string adminKey = "",
        bool allowUnsigned = false)
    {
        string json = ToJson(payload, pretty: false);
        if (payload == null || payload.costs == null || payload.costs.Count == 0)
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail("Нет цен выборов для публикации."));
            yield break;
        }

        if (json.Length > MaxPublisherBodyChars)
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail("Payload цен выборов слишком большой."));
            yield break;
        }

        yield return SendJsonRequest("POST", ChoiceCostsPath, json, callback, baseUrlOverride, adminKey, allowUnsigned);
    }

    public static IEnumerator Fetch(
        string storyId,
        string episodeId,
        Action<UnityPublisherRequestResult> callback,
        string baseUrlOverride = "",
        string adminKey = "",
        bool allowUnsigned = false)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);

        string path = ChoiceCostsPath;
        var query = new List<string>();
        if (!string.IsNullOrEmpty(storyId))
            query.Add("storyId=" + UnityWebRequest.EscapeURL(storyId));
        if (!string.IsNullOrEmpty(episodeId))
            query.Add("episodeId=" + UnityWebRequest.EscapeURL(episodeId));
        if (query.Count > 0)
            path += "?" + string.Join("&", query);

        yield return SendJsonRequest("GET", path, null, callback, baseUrlOverride, adminKey, allowUnsigned);
    }

    public static IEnumerator Delete(
        string nodeGuid,
        Action<UnityPublisherRequestResult> callback,
        string baseUrlOverride = "",
        string adminKey = "",
        bool allowUnsigned = false)
    {
        nodeGuid = SaveDataSanitizer.SanitizeIdentifier(nodeGuid);
        if (string.IsNullOrEmpty(nodeGuid))
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail("Некорректный nodeGuid."));
            yield break;
        }

        yield return SendJsonRequest(
            "DELETE",
            ChoiceCostsPath + "/" + UnityWebRequest.EscapeURL(nodeGuid),
            null,
            callback,
            baseUrlOverride,
            adminKey,
            allowUnsigned);
    }

    private static void AddGraphChoiceCosts(
        UnityChoiceCostsPublishPayload payload,
        HashSet<string> seen,
        StoryGraph graph,
        GraphContext context)
    {
        if (graph.nodes == null)
            return;

        foreach (Node rawNode in graph.nodes)
        {
            if (rawNode == null || payload.costs.Count >= MaxEntries)
                continue;

            if (rawNode is ChoiceNode choice)
                AddChoiceNodeCosts(payload, seen, choice, context);
            else if (rawNode is WardrobeChoiceNode wardrobe)
                AddWardrobeChoiceCosts(payload, seen, wardrobe, context);
        }
    }

    private static void AddChoiceNodeCosts(
        UnityChoiceCostsPublishPayload payload,
        HashSet<string> seen,
        ChoiceNode node,
        GraphContext context)
    {
        if (node.options == null)
            return;

        for (int i = 0; i < node.options.Count && payload.costs.Count < MaxEntries; i++)
        {
            ChoiceOption option = node.options[i];
            if (option == null || !option.isPremium || option.premiumCost <= 0)
                continue;

            int cost = SaveDataSanitizer.ClampCurrencyValue(option.premiumCost);
            if (cost <= 0)
                continue;

            var entry = CreateBaseEntry(node, context, i, cost, "choice");
            entry.choiceText = SaveDataSanitizer.SanitizeHistoryLine(option.text);
            entry.label = entry.choiceText;
            AddEntry(payload, seen, entry);
        }
    }

    private static void AddWardrobeChoiceCosts(
        UnityChoiceCostsPublishPayload payload,
        HashSet<string> seen,
        WardrobeChoiceNode node,
        GraphContext context)
    {
        int count = node.availableClothes != null ? node.availableClothes.Count : 0;
        for (int i = 0; i < count && payload.costs.Count < MaxEntries; i++)
        {
            int cost = node.GetPremiumCost(i);
            if (cost <= 0)
                continue;

            var entry = CreateBaseEntry(node, context, i, SaveDataSanitizer.ClampCurrencyValue(cost), "wardrobe");
            ClothingItem item = node.availableClothes[i];
            entry.itemId = SaveDataSanitizer.SanitizeIdentifier(item != null ? item.id : "");
            entry.choiceText = SaveDataSanitizer.SanitizeHistoryLine(item != null ? item.DisplayName : "");
            entry.label = string.IsNullOrEmpty(entry.choiceText) ? entry.itemId : entry.choiceText;
            AddEntry(payload, seen, entry);
        }
    }

    private static UnityChoiceCostEntry CreateBaseEntry(BaseStoryNode node, GraphContext context, int index, int cost, string source)
    {
        string nodeGuid = SaveDataSanitizer.SanitizeIdentifier(node != null ? node.guid : "");
        return new UnityChoiceCostEntry
        {
            storyId = SaveDataSanitizer.SanitizeIdentifier(context.storyId),
            episodeId = SaveDataSanitizer.SanitizeIdentifier(context.episodeId),
            chapterId = SaveDataSanitizer.SanitizeIdentifier(context.chapterId),
            nodeGuid = nodeGuid,
            nodeId = nodeGuid,
            nodeTitle = SaveDataSanitizer.SanitizeHistoryLine(node != null ? node.name : ""),
            choiceIndex = Mathf.Max(0, index),
            optionIndex = Mathf.Max(0, index),
            cost = SaveDataSanitizer.ClampCurrencyValue(cost),
            currency = "hearts",
            source = source
        };
    }

    private static void AddEntry(UnityChoiceCostsPublishPayload payload, HashSet<string> seen, UnityChoiceCostEntry entry)
    {
        if (payload == null || entry == null)
            return;

        if (string.IsNullOrEmpty(entry.episodeId) ||
            string.IsNullOrEmpty(entry.nodeGuid) ||
            entry.cost <= 0)
        {
            return;
        }

        string key = entry.storyId + "|" + entry.episodeId + "|" + entry.nodeGuid + "|" + entry.choiceIndex + "|" + entry.source;
        if (!seen.Add(key))
            return;

        payload.costs.Add(entry);
    }

    private static GraphContext ResolveGraphContext(StoryGraph graph)
    {
        var context = new GraphContext
        {
            episodeId = SaveDataSanitizer.SanitizeIdentifier(graph != null ? graph.episodeId : "")
        };

        if (graph == null)
            return context;

        string[] guids = AssetDatabase.FindAssets("t:StoryData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StoryData story = AssetDatabase.LoadAssetAtPath<StoryData>(path);
            if (story == null || story.chapters == null)
                continue;

            foreach (ChapterData chapter in story.chapters)
            {
                if (chapter == null || chapter.graph != graph)
                    continue;

                context.storyId = SaveDataSanitizer.SanitizeIdentifier(story.storyId);
                context.chapterId = SaveDataSanitizer.SanitizeIdentifier(chapter.chapterId);
                if (string.IsNullOrEmpty(context.episodeId))
                    context.episodeId = SaveDataSanitizer.SanitizeIdentifier(graph.episodeId);
                if (string.IsNullOrEmpty(context.episodeId))
                    context.episodeId = SaveDataSanitizer.SanitizeIdentifier(chapter.chapterId);
                return context;
            }
        }

        return context;
    }

    private static string ResolveSingleValue(List<UnityChoiceCostEntry> entries, Func<UnityChoiceCostEntry, string> selector)
    {
        if (entries == null || selector == null)
            return "";

        string value = "";
        foreach (UnityChoiceCostEntry entry in entries)
        {
            string next = SaveDataSanitizer.SanitizeIdentifier(selector(entry));
            if (string.IsNullOrEmpty(next))
                continue;

            if (string.IsNullOrEmpty(value))
            {
                value = next;
                continue;
            }

            if (!string.Equals(value, next, StringComparison.Ordinal))
                return "";
        }

        return value;
    }

    private static IEnumerator SendJsonRequest(
        string method,
        string path,
        string jsonBody,
        Action<UnityPublisherRequestResult> callback,
        string baseUrlOverride,
        string adminKey,
        bool allowUnsigned)
    {
        string url = BuildUrl(path, baseUrlOverride, out string urlError);
        if (string.IsNullOrEmpty(url))
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail(urlError));
            yield break;
        }

        string safeAdminKey = SanitizeSecret(FirstNonEmpty(adminKey, Environment.GetEnvironmentVariable(AdminKeyEnvironmentVariable)));
        if (string.IsNullOrEmpty(safeAdminKey) && !allowUnsigned && !IsLoopbackUrl(url))
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail("Не указан X-Admin-Key. Заполни поле в окне или NOCTURNEDC_ADMIN_KEY."));
            yield break;
        }

        using (UnityWebRequest request = CreateRequest(method, url, jsonBody))
        {
            if (request == null)
            {
                callback?.Invoke(UnityPublisherRequestResult.Fail("Не удалось создать запрос."));
                yield break;
            }

            yield return WaitForPublisherSlot();

            if (!string.IsNullOrEmpty(safeAdminKey))
                request.SetRequestHeader("X-Admin-Key", safeAdminKey);

            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Cache-Control", "no-store");
            request.timeout = 30;

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
                yield return null;

            string response = request.downloadHandler != null ? request.downloadHandler.text : "";
            if (response != null && response.Length > MaxPublisherResponseChars)
                response = response.Substring(0, MaxPublisherResponseChars);

            bool ok = request.result == UnityWebRequest.Result.Success &&
                      request.responseCode >= 200 &&
                      request.responseCode < 300;

            callback?.Invoke(new UnityPublisherRequestResult
            {
                Success = ok,
                StatusCode = request.responseCode,
                Body = response ?? "",
                Error = ok ? "" : FormatError(request)
            });
        }
    }

    private static IEnumerator WaitForPublisherSlot()
    {
        while (true)
        {
            double now = EditorApplication.timeSinceStartup;
            TrimPublisherWindow(now);
            if (PublisherRequestTimestamps.Count < PublisherSensitiveLimit)
            {
                PublisherRequestTimestamps.Enqueue(now);
                yield break;
            }

            double wait = Math.Max(0d, PublisherRequestTimestamps.Peek() + PublisherSensitiveWindowSeconds - now);
            double waitUntil = now + Math.Min(wait, 5d);
            while (EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
        }
    }

    private static void TrimPublisherWindow(double now)
    {
        while (PublisherRequestTimestamps.Count > 0 &&
               now - PublisherRequestTimestamps.Peek() >= PublisherSensitiveWindowSeconds)
        {
            PublisherRequestTimestamps.Dequeue();
        }
    }

    private static UnityWebRequest CreateRequest(string method, string url, string jsonBody)
    {
        method = (method ?? "").Trim().ToUpperInvariant();
        if (method == "GET")
            return UnityWebRequest.Get(url);

        if (method == "DELETE")
        {
            var request = UnityWebRequest.Delete(url);
            request.downloadHandler = new DownloadHandlerBuffer();
            return request;
        }

        if (method == "POST")
        {
            var request = new UnityWebRequest(url, "POST");
            byte[] body = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(jsonBody) ? "{}" : jsonBody);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        return null;
    }

    private static string BuildUrl(string path, string baseUrlOverride, out string error)
    {
        error = "";
        string root = FirstNonEmpty(baseUrlOverride, NetworkRuntimeConfigLoader.Load()?.ResolveBaseUrl());
        root = (root ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(root))
        {
            error = "Базовый URL пустой.";
            return "";
        }

        if (!Uri.TryCreate(root, UriKind.Absolute, out Uri baseUri))
        {
            error = "Базовый URL некорректный.";
            return "";
        }

        if (baseUri.Scheme != Uri.UriSchemeHttps && !(baseUri.Scheme == Uri.UriSchemeHttp && baseUri.IsLoopback))
        {
            error = "Публикация требует HTTPS, кроме локального loopback.";
            return "";
        }

        path = NormalizePath(path);
        if (string.IsNullOrEmpty(path) || !path.StartsWith(ChoiceCostsPath, StringComparison.Ordinal))
        {
            error = "Путь публикации заблокирован.";
            return "";
        }

        return new Uri(baseUri, path).ToString();
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        string normalized = path.Trim().Replace('\\', '/').Replace("\r", "").Replace("\n", "");
        if (normalized.StartsWith("//", StringComparison.Ordinal) ||
            Uri.TryCreate(normalized, UriKind.Absolute, out _))
        {
            return "";
        }

        return normalized.StartsWith("/", StringComparison.Ordinal) ? normalized : "/" + normalized;
    }

    private static string SanitizeSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        value = value.Trim();
        if (value.Length > MaxPublisherSecretChars)
            return "";

        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsControl(value[i]))
                return "";
        }

        return value;
    }

    private static bool IsLoopbackUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && uri.IsLoopback;
    }

    private static string FormatError(UnityWebRequest request)
    {
        if (request == null)
            return "Запрос не выполнен.";

        string error = string.IsNullOrEmpty(request.error) ? "Запрос не выполнен" : request.error;
        string message = request.responseCode > 0 ? request.responseCode + " " + error : error;
        return SaveDataSanitizer.SanitizeHistoryLine(message);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "";
    }

    private struct GraphContext
    {
        public string storyId;
        public string episodeId;
        public string chapterId;
    }
}

[Serializable]
public sealed class UnityWardrobeCostsPublishPayload
{
    public string source;
    public string generatedAt;
    public List<UnityWardrobeCostEntry> items = new List<UnityWardrobeCostEntry>();
}

[Serializable]
public sealed class UnityWardrobeCostEntry
{
    public string itemId;
    public int price;
}

public static class UnityWardrobeCostsPublisher
{
    private const string WardrobeCostsPath = ApiRoutes.UnityWardrobeCosts;
    private const string AdminKeyEnvironmentVariable = "NOCTURNEDC_ADMIN_KEY";
    private const int MaxEntries = 1000;
    private const int MaxPublisherBodyChars = 512 * 1024;
    private const int MaxPublisherResponseChars = 1024 * 1024;
    private const int MaxPublisherSecretChars = 4096;
    private const int PublisherSensitiveLimit = 5;
    private const double PublisherSensitiveWindowSeconds = 60d;

    private static readonly Queue<double> PublisherRequestTimestamps = new Queue<double>();

    public static UnityWardrobeCostsPublishPayload BuildPayload(IEnumerable<StoryGraph> graphs)
    {
        var payload = new UnityWardrobeCostsPublishPayload
        {
            generatedAt = DateTime.UtcNow.ToString("o"),
            source = "unity-editor"
        };
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (graphs == null)
            return payload;

        foreach (StoryGraph graph in graphs)
        {
            if (graph == null || graph.nodes == null || payload.items.Count >= MaxEntries)
                continue;

            foreach (Node rawNode in graph.nodes)
            {
                if (payload.items.Count >= MaxEntries)
                    break;

                if (rawNode is WardrobeChoiceNode wardrobe)
                    AddWardrobeCosts(payload, seen, wardrobe);
            }
        }

        return payload;
    }

    public static string ToJson(UnityWardrobeCostsPublishPayload payload, bool pretty)
    {
        if (payload == null)
            return "{}";

        return JsonUtility.ToJson(payload, pretty);
    }

    public static IEnumerator Publish(
        UnityWardrobeCostsPublishPayload payload,
        Action<UnityPublisherRequestResult> callback,
        string baseUrlOverride = "",
        string adminKey = "",
        bool allowUnsigned = false)
    {
        string json = ToJson(payload, pretty: false);
        if (payload == null || payload.items == null || payload.items.Count == 0)
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail("No wardrobe prices to publish."));
            yield break;
        }

        if (json.Length > MaxPublisherBodyChars)
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail("Wardrobe price payload is too large."));
            yield break;
        }

        yield return SendJsonRequest("POST", WardrobeCostsPath, json, callback, baseUrlOverride, adminKey, allowUnsigned);
    }

    public static IEnumerator Fetch(
        Action<UnityPublisherRequestResult> callback,
        string baseUrlOverride = "",
        string adminKey = "",
        bool allowUnsigned = false)
    {
        yield return SendJsonRequest("GET", WardrobeCostsPath, null, callback, baseUrlOverride, adminKey, allowUnsigned);
    }

    private static void AddWardrobeCosts(
        UnityWardrobeCostsPublishPayload payload,
        HashSet<string> seen,
        WardrobeChoiceNode node)
    {
        if (payload == null || seen == null || node == null || node.availableClothes == null)
            return;

        for (int i = 0; i < node.availableClothes.Count && payload.items.Count < MaxEntries; i++)
        {
            ClothingItem item = node.availableClothes[i];
            string itemId = SaveDataSanitizer.SanitizeIdentifier(item != null ? item.id : "");
            int price = SaveDataSanitizer.ClampCurrencyValue(node.GetPremiumCost(i));
            if (string.IsNullOrEmpty(itemId) || price <= 0 || !seen.Add(itemId))
                continue;

            payload.items.Add(new UnityWardrobeCostEntry
            {
                itemId = itemId,
                price = price
            });
        }
    }

    private static IEnumerator SendJsonRequest(
        string method,
        string path,
        string jsonBody,
        Action<UnityPublisherRequestResult> callback,
        string baseUrlOverride,
        string adminKey,
        bool allowUnsigned)
    {
        string url = BuildUrl(path, baseUrlOverride, out string urlError);
        if (string.IsNullOrEmpty(url))
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail(urlError));
            yield break;
        }

        string safeAdminKey = SanitizeSecret(FirstNonEmpty(adminKey, Environment.GetEnvironmentVariable(AdminKeyEnvironmentVariable)));
        if (string.IsNullOrEmpty(safeAdminKey) && !allowUnsigned && !IsLoopbackUrl(url))
        {
            callback?.Invoke(UnityPublisherRequestResult.Fail("X-Admin-Key is required."));
            yield break;
        }

        using (UnityWebRequest request = CreateRequest(method, url, jsonBody))
        {
            if (request == null)
            {
                callback?.Invoke(UnityPublisherRequestResult.Fail("Failed to create request."));
                yield break;
            }

            yield return WaitForPublisherSlot();

            if (!string.IsNullOrEmpty(safeAdminKey))
                request.SetRequestHeader("X-Admin-Key", safeAdminKey);

            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Cache-Control", "no-store");
            request.timeout = 30;

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
                yield return null;

            string response = request.downloadHandler != null ? request.downloadHandler.text : "";
            if (response != null && response.Length > MaxPublisherResponseChars)
                response = response.Substring(0, MaxPublisherResponseChars);

            bool ok = request.result == UnityWebRequest.Result.Success &&
                      request.responseCode >= 200 &&
                      request.responseCode < 300;

            callback?.Invoke(new UnityPublisherRequestResult
            {
                Success = ok,
                StatusCode = request.responseCode,
                Body = response ?? "",
                Error = ok ? "" : FormatError(request)
            });
        }
    }

    private static IEnumerator WaitForPublisherSlot()
    {
        while (true)
        {
            double now = EditorApplication.timeSinceStartup;
            TrimPublisherWindow(now);
            if (PublisherRequestTimestamps.Count < PublisherSensitiveLimit)
            {
                PublisherRequestTimestamps.Enqueue(now);
                yield break;
            }

            double wait = Math.Max(0d, PublisherRequestTimestamps.Peek() + PublisherSensitiveWindowSeconds - now);
            double waitUntil = now + Math.Min(wait, 5d);
            while (EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
        }
    }

    private static void TrimPublisherWindow(double now)
    {
        while (PublisherRequestTimestamps.Count > 0 &&
               now - PublisherRequestTimestamps.Peek() >= PublisherSensitiveWindowSeconds)
        {
            PublisherRequestTimestamps.Dequeue();
        }
    }

    private static UnityWebRequest CreateRequest(string method, string url, string jsonBody)
    {
        method = (method ?? "").Trim().ToUpperInvariant();
        if (method == "GET")
            return UnityWebRequest.Get(url);

        if (method == "POST")
        {
            var request = new UnityWebRequest(url, "POST");
            byte[] body = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(jsonBody) ? "{}" : jsonBody);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        return null;
    }

    private static string BuildUrl(string path, string baseUrlOverride, out string error)
    {
        error = "";
        string root = FirstNonEmpty(baseUrlOverride, NetworkRuntimeConfigLoader.Load()?.ResolveBaseUrl());
        root = (root ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(root))
        {
            error = "Base URL is empty.";
            return "";
        }

        if (!Uri.TryCreate(root, UriKind.Absolute, out Uri baseUri))
        {
            error = "Base URL is invalid.";
            return "";
        }

        if (baseUri.Scheme != Uri.UriSchemeHttps && !(baseUri.Scheme == Uri.UriSchemeHttp && baseUri.IsLoopback))
        {
            error = "Publisher requests require HTTPS except local loopback.";
            return "";
        }

        path = NormalizePath(path);
        if (string.IsNullOrEmpty(path) || !path.StartsWith(WardrobeCostsPath, StringComparison.Ordinal))
        {
            error = "Publisher path is blocked.";
            return "";
        }

        return new Uri(baseUri, path).ToString();
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        string normalized = path.Trim().Replace('\\', '/').Replace("\r", "").Replace("\n", "");
        if (normalized.StartsWith("//", StringComparison.Ordinal) ||
            Uri.TryCreate(normalized, UriKind.Absolute, out _))
        {
            return "";
        }

        return normalized.StartsWith("/", StringComparison.Ordinal) ? normalized : "/" + normalized;
    }

    private static string SanitizeSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        value = value.Trim();
        if (value.Length > MaxPublisherSecretChars)
            return "";

        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsControl(value[i]))
                return "";
        }

        return value;
    }

    private static bool IsLoopbackUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && uri.IsLoopback;
    }

    private static string FormatError(UnityWebRequest request)
    {
        if (request == null)
            return "Request was not executed.";

        string error = string.IsNullOrEmpty(request.error) ? "Request failed" : request.error;
        string message = request.responseCode > 0 ? request.responseCode + " " + error : error;
        return SaveDataSanitizer.SanitizeHistoryLine(message);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "";
    }
}
#endif
