#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
public sealed class CurrentBackendCatalogMockServer : IDisposable
{
    public const string DefaultAdminKey = "local-current-backend-key";
    private readonly Dictionary<string, string> _stories = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _seasons = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<string> _episodes = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _content = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<string> _publishedStories = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _publishedEpisodes = new HashSet<string>(StringComparer.Ordinal);
    private readonly object _lock = new object();
    private TcpListener _listener;
    private bool _disposed;
    private int _requestCount;
    public string BaseUrl { get; private set; } = "";
    public string LastRequestSummary { get; private set; } = "";
    public int RequestCount => _requestCount;
    public void Start()
    {
        if (_listener != null)
            return;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        BaseUrl = "http://127.0.0.1:" + port;
        new Thread(ListenLoop) { IsBackground = true }.Start();
    }
    public void Dispose()
    {
        _disposed = true;
        try { _listener?.Stop(); }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
        _listener = null;
    }
    public bool IsPublished(string episodeId)
    {
        lock (_lock)
            return _publishedEpisodes.Contains(SaveDataSanitizer.SanitizeIdentifier(episodeId));
    }
    private void ListenLoop()
    {
        while (!_disposed)
        {
            try
            {
                using (TcpClient client = _listener.AcceptTcpClient())
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;
                    HandleClient(client);
                }
            }
            catch (SocketException)
            {
                if (!_disposed)
                    Thread.Sleep(10);
            }
            catch (ObjectDisposedException) { return; }
            catch (InvalidOperationException) { return; }
        }
    }
    private void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        ContentReleaseMockHttpRequest request = ContentReleaseMockHttpTransport.Read(stream);
        if (request == null)
            return;
        Interlocked.Increment(ref _requestCount);
        LastRequestSummary = request.Method + " " + request.Target;
        ContentReleaseMockHttpTransport.Write(stream, Handle(request));
    }
    private ContentReleaseMockHttpResponse Handle(ContentReleaseMockHttpRequest request)
    {
        if (!HasValidAdminKey(request))
            return Json(401, "{\"ok\":false,\"error\":\"admin_key_required\"}");
        if (request.Method == "GET" && request.Path == CurrentBackendAdminRoutes.AdminCatalog)
            return Json(200, BuildCatalogJson());
        if (request.Method == "POST" && request.Path == CurrentBackendAdminRoutes.Story)
            return CreateStory(request.Body);
        if (request.Method == "POST" && TryPath(request.Path, "/admin/catalog/story/", "/season", out string storyId))
            return AddSeason(storyId, request.Body);
        if (request.Method == "POST" && TryPath(request.Path, "/admin/catalog/season/", "/episode", out string seasonId))
            return AddEpisode(seasonId, request.Body);
        if (request.Method == "PATCH" && TryPath(request.Path, "/admin/catalog/story/", "/publish", out string publishStoryId))
            return SetPublished(_publishedStories, publishStoryId, request.Body, "storyId");
        if (request.Method == "POST" && TryPath(request.Path, "/admin/catalog/episode/", "/content", out string contentEpisodeId))
            return SaveContent(contentEpisodeId, request.Body);
        if (request.Method == "PATCH" && TryPath(request.Path, "/admin/catalog/episode/", "/publish", out string publishEpisodeId))
            return SetPublished(_publishedEpisodes, publishEpisodeId, request.Body, "episodeId");
        return Json(404, "{\"ok\":false,\"error\":\"not_found\"}");
    }
    private ContentReleaseMockHttpResponse CreateStory(string body)
    {
        string storyId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(body, "storyId"));
        if (string.IsNullOrWhiteSpace(storyId))
            return Json(422, "{\"ok\":false,\"error\":\"story_required\"}");
        lock (_lock)
            _stories[storyId] = body ?? "{}";
        return Ok("storyId", storyId);
    }
    private ContentReleaseMockHttpResponse AddSeason(string storyId, string body)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        string seasonId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(body, "seasonId"));
        if (string.IsNullOrWhiteSpace(storyId) || string.IsNullOrWhiteSpace(seasonId))
            return Json(422, "{\"ok\":false,\"error\":\"season_required\"}");
        lock (_lock)
            _seasons[seasonId] = storyId;
        return Ok("seasonId", seasonId);
    }
    private ContentReleaseMockHttpResponse AddEpisode(string seasonId, string body)
    {
        seasonId = SaveDataSanitizer.SanitizeIdentifier(seasonId);
        string episodeId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(body, "episodeId"));
        if (string.IsNullOrWhiteSpace(seasonId) || string.IsNullOrWhiteSpace(episodeId))
            return Json(422, "{\"ok\":false,\"error\":\"episode_required\"}");
        lock (_lock)
            _episodes.Add(episodeId);
        return Ok("episodeId", episodeId);
    }
    private ContentReleaseMockHttpResponse SaveContent(string episodeId, string body)
    {
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        if (string.IsNullOrWhiteSpace(episodeId) || string.IsNullOrWhiteSpace(body))
            return Json(422, "{\"ok\":false,\"error\":\"episode_content_required\"}");
        lock (_lock)
        {
            _episodes.Add(episodeId);
            _content[episodeId] = body;
        }
        return Ok("episodeId", episodeId);
    }
    private ContentReleaseMockHttpResponse SetPublished(HashSet<string> target, string id, string body, string key)
    {
        id = SaveDataSanitizer.SanitizeIdentifier(id);
        bool publish = body != null && body.IndexOf("\"published\":true", StringComparison.OrdinalIgnoreCase) >= 0;
        lock (_lock)
        {
            if (publish) target.Add(id);
            else target.Remove(id);
        }
        return Json(200, "{\"ok\":true,\"" + key + "\":\"" + NetworkJson.Escape(id) + "\",\"published\":" + (publish ? "true" : "false") + "}");
    }
    private string BuildCatalogJson()
    {
        lock (_lock)
            return "{\"stories\":" + IdArray(_stories.Keys, _publishedStories, "storyId") +
                ",\"seasons\":" + SeasonArray() +
                ",\"episodes\":" + IdArray(_episodes, _publishedEpisodes, "episodeId") + "}";
    }
    private string SeasonArray()
    {
        var items = new List<string>();
        foreach (var pair in _seasons)
            items.Add("{\"seasonId\":\"" + NetworkJson.Escape(pair.Key) + "\",\"storyId\":\"" + NetworkJson.Escape(pair.Value) + "\"}");
        return "[" + string.Join(",", items) + "]";
    }
    private static string IdArray(IEnumerable<string> ids, HashSet<string> published, string key)
    {
        var items = new List<string>();
        foreach (string id in ids)
            items.Add("{\"" + key + "\":\"" + NetworkJson.Escape(id) + "\",\"isPublished\":" + (published.Contains(id) ? "true" : "false") + "}");
        return "[" + string.Join(",", items) + "]";
    }
    private static bool TryPath(string path, string prefix, string suffix, out string id)
    {
        id = "";
        if (!path.StartsWith(prefix, StringComparison.Ordinal) || !path.EndsWith(suffix, StringComparison.Ordinal))
            return false;
        id = Uri.UnescapeDataString(path.Substring(prefix.Length, path.Length - prefix.Length - suffix.Length));
        return true;
    }
    private static bool HasValidAdminKey(ContentReleaseMockHttpRequest request)
    {
        return !request.Headers.TryGetValue("X-Admin-Key", out string key) ||
               string.Equals(key, DefaultAdminKey, StringComparison.Ordinal);
    }
    private static ContentReleaseMockHttpResponse Ok(string key, string id)
    {
        return Json(200, "{\"ok\":true,\"" + key + "\":\"" + NetworkJson.Escape(id) + "\"}");
    }
    private static ContentReleaseMockHttpResponse Json(int code, string body)
    {
        return ContentReleaseMockHttpResponse.Json(code, body);
    }
}
#endif
