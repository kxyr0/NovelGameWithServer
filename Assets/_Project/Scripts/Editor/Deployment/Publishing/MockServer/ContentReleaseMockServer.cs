#if UNITY_EDITOR
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public sealed class ContentReleaseMockServer : IDisposable
{
    public const string DefaultAdminKey = "local-content-release-key";

    private readonly ContentReleaseMockStore _store = new ContentReleaseMockStore();
    private TcpListener _listener;
    private Thread _thread;
    private bool _disposed;
    private int _requestCount;

    public string BaseUrl { get; private set; } = "";
    public string LastRequestSummary { get; private set; } = "";
    public int RequestCount => _requestCount;
    public int ReleaseCount => _store.Count;

    public void Start()
    {
        if (_listener != null)
            return;

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        BaseUrl = "http://127.0.0.1:" + port;
        _thread = new Thread(ListenLoop) { IsBackground = true };
        _thread.Start();
    }

    public void Dispose()
    {
        _disposed = true;
        try
        {
            _listener?.Stop();
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        _listener = null;
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
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }
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
        if (request.Path.StartsWith("/admin/", StringComparison.Ordinal) && !HasValidAdminKey(request))
            return Json(401, "{\"ok\":false,\"error\":\"admin_key_required\"}");

        if (request.Path == ContentReleaseAdminRoutes.BasePath && request.Method == "POST")
            return _store.Upsert(ReadRelease(request.Body));

        if (request.Path == ContentReleaseAdminRoutes.BasePath && request.Method == "GET")
        {
            return _store.List(
                QueryValue(request.Query, "storyId"),
                QueryValue(request.Query, "episodeId"));
        }

        if (request.Path == ContentReleaseAdminRoutes.PromotePath && request.Method == "POST")
            return _store.Promote(request.Body);

        if (request.Path == ContentReleaseAdminRoutes.RollbackPath && request.Method == "POST")
            return _store.Rollback(request.Body);

        if (request.Path == "/health" && request.Method == "GET")
            return Json(200, "{\"ok\":true,\"service\":\"content-release-mock\"}");

        return Json(404, "{\"ok\":false,\"error\":\"not_found\"}");
    }

    private static bool HasValidAdminKey(ContentReleaseMockHttpRequest request)
    {
        if (!request.Headers.TryGetValue("X-Admin-Key", out string key))
            return true;

        return string.Equals(key, DefaultAdminKey, StringComparison.Ordinal);
    }

    private static ContentReleaseDescriptor ReadRelease(string body)
    {
        return new ContentReleaseDescriptor
        {
            storyId = NetworkJson.GetString(body, "storyId"),
            episodeId = NetworkJson.GetString(body, "episodeId"),
            contentVersion = NetworkJson.GetString(body, "contentVersion"),
            status = NetworkJson.GetString(body, "status"),
            channel = NetworkJson.GetString(body, "channel"),
            addressablesCatalogUrl = NetworkJson.GetString(body, "addressablesCatalogUrl"),
            addressablesRemoteLoadPath = NetworkJson.GetString(body, "addressablesRemoteLoadPath"),
            addressablesManifestUrl = NetworkJson.GetString(body, "addressablesManifestUrl"),
            addressablesManifestHash = NetworkJson.GetString(body, "addressablesManifestHash"),
            buildTarget = NetworkJson.GetString(body, "buildTarget"),
            minAppVersion = NetworkJson.GetString(body, "minAppVersion"),
            notes = NetworkJson.GetString(body, "notes"),
            updatedAtIso = NetworkJson.GetString(body, "updatedAtIso")
        };
    }

    private static string QueryValue(string query, string name)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "";

        string prefix = name + "=";
        string[] parts = query.Split('&');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith(prefix, StringComparison.Ordinal))
                return Uri.UnescapeDataString(parts[i].Substring(prefix.Length));
        }

        return "";
    }

    private static ContentReleaseMockHttpResponse Json(int code, string body)
    {
        return ContentReleaseMockHttpResponse.Json(code, body);
    }
}
#endif
