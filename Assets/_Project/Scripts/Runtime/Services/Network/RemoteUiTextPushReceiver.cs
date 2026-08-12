#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RemoteUiTextPushReceiver : MonoBehaviour
{
    private const int DefaultPort = 52208;
    private const int MaxPayloadChars = 64 * 1024;
    private const string PortEnvironmentVariable = "NOCTURNAL_UNITY_PUSH_PORT";
    private const string PushPath = "/ui-texts/push";

    private static RemoteUiTextPushReceiver _instance;

    private readonly Queue<PushJob> _jobs = new Queue<PushJob>();
    private readonly object _jobsLock = new object();

    private HttpListener _listener;
    private Thread _listenerThread;
    private volatile bool _running;
    private int _port = DefaultPort;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (!Application.isEditor && !Debug.isDebugBuild)
            return;
        if (_instance != null)
            return;

        var go = new GameObject("Remote UI Text Push Receiver");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<RemoteUiTextPushReceiver>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _port = ResolvePort();
    }

    private void OnEnable()
    {
        StartListener();
    }

    private void OnDisable()
    {
        StopListener();
    }

    private void OnDestroy()
    {
        StopListener();
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        while (true)
        {
            PushJob job = null;
            lock (_jobsLock)
            {
                if (_jobs.Count > 0)
                    job = _jobs.Dequeue();
            }

            if (job == null)
                return;

            ApplyJob(job);
        }
    }

    private void StartListener()
    {
        if (_running)
            return;

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:" + _port + "/");
            _listener.Start();
            _running = true;
            _listenerThread = new Thread(ListenerLoop)
            {
                IsBackground = true,
                Name = "RemoteUiTextPushReceiver"
            };
            _listenerThread.Start();
            Debug.Log("[Nocturnal] UI text push receiver: http://127.0.0.1:" + _port + PushPath);
        }
        catch (Exception exception)
        {
            _running = false;
            if (_listener != null)
            {
                _listener.Close();
                _listener = null;
            }

            Debug.LogWarning("[Nocturnal] UI text push receiver failed: " + exception.Message);
        }
    }

    private void StopListener()
    {
        _running = false;
        if (_listener != null)
        {
            _listener.Close();
            _listener = null;
        }

        _listenerThread = null;
    }

    private void ListenerLoop()
    {
        while (_running && _listener != null)
        {
            try
            {
                HttpListenerContext context = _listener.GetContext();
                ProcessRequest(context);
            }
            catch
            {
                if (!_running)
                    return;
            }
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        string path = context.Request.Url != null ? context.Request.Url.AbsolutePath : "";
        if (context.Request.HttpMethod == "GET" && path == ApiRoutes.Health)
        {
            WriteJson(context.Response, 200, "{\"ok\":true,\"receiver\":\"unity-ui-text-push\"}");
            return;
        }

        if (context.Request.HttpMethod != "POST" || path != PushPath)
        {
            WriteJson(context.Response, 404, "{\"ok\":false,\"error\":\"not_found\"}");
            return;
        }

        string body;
        using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8))
            body = reader.ReadToEnd();

        if (string.IsNullOrWhiteSpace(body) || body.Length > MaxPayloadChars)
        {
            WriteJson(context.Response, 422, "{\"ok\":false,\"error\":\"invalid_payload\"}");
            return;
        }

        var job = new PushJob(body);
        lock (_jobsLock)
        {
            _jobs.Enqueue(job);
        }

        bool completed = job.Done.Wait(2000);
        if (!completed)
        {
            WriteJson(context.Response, 202, "{\"ok\":false,\"queued\":true,\"error\":\"unity_main_thread_timeout\"}");
            return;
        }

        WriteJson(
            context.Response,
            job.Ok ? 200 : 422,
            "{\"ok\":" + (job.Ok ? "true" : "false") + ",\"message\":\"" + EscapeResponse(job.Message) + "\"}");
    }

    private void ApplyJob(PushJob job)
    {
        try
        {
            string screenId;
            string storyId;
            string locale;
            string responseJson = BuildUiTextResponseJson(job.Payload, out screenId, out storyId, out locale);
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                job.Complete(false, "payload_without_items_or_id");
                return;
            }

            string error;
            bool ok = NetworkManager.ApplyPushedUiTexts(screenId, storyId, locale, responseJson, out error);
            job.Complete(ok, ok ? "applied" : error);
        }
        catch (Exception exception)
        {
            job.Complete(false, exception.Message);
        }
    }

    private static string BuildUiTextResponseJson(string payload, out string screenId, out string storyId, out string locale)
    {
        screenId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(payload, "screenId"));
        storyId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(payload, "storyId"));
        locale = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(payload, "locale"));
        if (string.IsNullOrWhiteSpace(locale))
            locale = "ru";

        string rawItems = NetworkJson.GetRawValue(payload, "items");
        if (!string.IsNullOrWhiteSpace(rawItems))
        {
            if (string.IsNullOrWhiteSpace(screenId))
                screenId = InferFirstItemString(rawItems, "screenId");
            if (string.IsNullOrWhiteSpace(storyId))
                storyId = InferFirstItemString(rawItems, "storyId");
            if (string.IsNullOrWhiteSpace(locale))
                locale = FirstNonEmpty(InferFirstItemString(rawItems, "locale"), "ru");

            return payload;
        }

        string id = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(id))
            return "";

        string text = NetworkJson.GetString(payload, "text") ?? "";
        bool enabled = NetworkJson.GetRawValue(payload, "enabled") == null ||
                       NetworkJson.GetBool(payload, "enabled", true);

        return "{\"version\":\"push\",\"updatedAt\":\"" + NetworkJson.Escape(DateTime.UtcNow.ToString("O")) +
            "\",\"items\":[{\"id\":\"" + NetworkJson.Escape(id) +
            "\",\"text\":\"" + NetworkJson.Escape(text) +
            "\",\"enabled\":" + (enabled ? "true" : "false") +
            ",\"locale\":\"" + NetworkJson.Escape(locale) +
            "\",\"screenId\":\"" + NetworkJson.Escape(screenId) +
            "\",\"storyId\":\"" + NetworkJson.Escape(storyId) + "\"}]}";
    }

    private static string InferFirstItemString(string rawItems, string key)
    {
        foreach (string item in NetworkJson.GetArrayItems(rawItems))
            return SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(item, key));

        return "";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();

        return "";
    }

    private static int ResolvePort()
    {
        string raw = Environment.GetEnvironmentVariable(PortEnvironmentVariable);
        int port;
        if (int.TryParse(raw, out port) && port >= 1024 && port <= 65535)
            return port;

        return DefaultPort;
    }

    private static void WriteJson(HttpListenerResponse response, int statusCode, string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json ?? "{}");
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.OutputStream.Close();
    }

    private static string EscapeResponse(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private sealed class PushJob
    {
        public readonly string Payload;
        public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        public bool Ok;
        public string Message = "";

        public PushJob(string payload)
        {
            Payload = payload;
        }

        public void Complete(bool ok, string message)
        {
            Ok = ok;
            Message = message ?? "";
            Done.Set();
        }
    }
}
#endif
