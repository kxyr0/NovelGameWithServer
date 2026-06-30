using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public sealed class ApiDiagnosticsWindow : EditorWindow
{
    private static readonly Regex SecretPattern = new Regex(
        "(?i)([\"']?\\b(password|token|accessToken|authToken|refreshToken|idToken|restoreCode|secret|apiKey|adminKey|x-admin-key|authorization|cookie|privateKey|session|jwt|purchaseToken|restoreToken|receipt|signature)\\b[\"']?\\s*[:=]\\s*)(\"[^\"]*\"|'[^']*'|[^\\s,;}\\]]+)",
        RegexOptions.Compiled);
    private static readonly Regex BearerPattern = new Regex("(?i)Bearer\\s+[A-Za-z0-9\\-._~+/]+=*", RegexOptions.Compiled);

    private string _baseUrl = ApiRoutes.BaseUrl;
    private string _bearerToken = "";
    private bool _allowWriteProbes;
    private string _testEpisodeId = "api_canary_episode";
    private string _testNodeId = "api_canary_node";
    private string _lastResult = "Idle.";

    [MenuItem("VN/Network/API Diagnostics")]
    public static void Open()
    {
        GetWindow<ApiDiagnosticsWindow>("API Diagnostics");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("NovelApp API Diagnostics", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Manual-only probes. Production defaults are read-only; write probes require the explicit toggle below. Admin and Unity Publisher write endpoints are intentionally not exposed here.",
            MessageType.Info);

        _baseUrl = EditorGUILayout.TextField("Base URL", string.IsNullOrWhiteSpace(_baseUrl) ? ApiRoutes.BaseUrl : _baseUrl);
        _bearerToken = EditorGUILayout.PasswordField("Bearer JWT", _bearerToken);
        _allowWriteProbes = EditorGUILayout.ToggleLeft("Allow write probes with disposable test data", _allowWriteProbes);

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate Local Contract"))
                ValidateLocalContract();

            if (GUILayout.Button("Probe API Docs"))
                StartProbe(Send("GET", "/api-docs", null, null));

            if (GUILayout.Button("Probe Profile"))
                StartProbe(Send("GET", ApiRoutes.PlayerProfile, null, _bearerToken));
        }

        using (new EditorGUI.DisabledScope(!_allowWriteProbes))
        {
            _testEpisodeId = EditorGUILayout.TextField("Test episodeId", _testEpisodeId);
            _testNodeId = EditorGUILayout.TextField("Test nodeId", _testNodeId);
            if (GUILayout.Button("Write Canary SaveProgress"))
                StartProbe(Send("POST", ApiRoutes.PlayerProgressSave, BuildCanarySaveProgressJson(), _bearerToken));
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Last Result", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(_lastResult, GUILayout.MinHeight(140f));
    }

    private void ValidateLocalContract()
    {
        int documented = 0;
        int runtimeAllowed = 0;
        int legacy = 0;
        foreach (ApiEndpoint endpoint in ApiContract.AllEndpoints)
        {
            if (endpoint.Documented)
                documented++;
            if (endpoint.RuntimeAllowed)
                runtimeAllowed++;
            if (endpoint.Kind == ApiEndpointKind.Legacy)
                legacy++;
        }

        _lastResult =
            "Contract OK\n" +
            "Docs: " + ApiContract.DocumentationUrl + "\n" +
            "Documented endpoints tracked: " + documented + "\n" +
            "Runtime-allowed endpoints: " + runtimeAllowed + "\n" +
            "Legacy/undocumented tracked risks: " + legacy + "\n" +
            "Runtime admin blocked: " + !ApiContract.IsRuntimeAllowed("POST", "/admin/catalog/story") + "\n" +
            "Runtime publisher blocked: " + !ApiContract.IsRuntimeAllowed("POST", ApiRoutes.UnityChoiceCosts);
    }

    private void StartProbe(IEnumerator routine)
    {
        _lastResult = "Running...";
        Repaint();
        EditorCoroutineRunner.Start(WrapProbe(routine));
    }

    private IEnumerator WrapProbe(IEnumerator routine)
    {
        while (routine.MoveNext())
            yield return routine.Current;

        Repaint();
    }

    private IEnumerator Send(string method, string path, string jsonBody, string token)
    {
        string url = BuildUrl(path);
        if (string.IsNullOrEmpty(url))
        {
            _lastResult = "Invalid URL.";
            yield break;
        }

        using (UnityWebRequest request = CreateRequest(method, url, jsonBody))
        {
            request.timeout = 15;
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Cache-Control", "no-store");
            if (!string.IsNullOrWhiteSpace(token))
                request.SetRequestHeader("Authorization", "Bearer " + token.Trim());

            yield return request.SendWebRequest();

            string body = request.downloadHandler != null ? request.downloadHandler.text : "";
            _lastResult =
                "Method: " + method + "\n" +
                "Path: " + ApiContract.RedactedEndpointForLog(path) + "\n" +
                "Status: " + request.responseCode + "\n" +
                "Result: " + request.result + "\n" +
                "Error: " + Redact(request.error) + "\n" +
                "Body: " + Trim(Redact(body), 1000);
        }
    }

    private string BuildUrl(string path)
    {
        string root = (string.IsNullOrWhiteSpace(_baseUrl) ? ApiRoutes.BaseUrl : _baseUrl.Trim()).TrimEnd('/');
        if (!Uri.TryCreate(root, UriKind.Absolute, out Uri baseUri))
            return "";

        string relative = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        if (relative.StartsWith("//", StringComparison.Ordinal) || Uri.TryCreate(relative, UriKind.Absolute, out _))
            return "";

        return new Uri(baseUri, relative.StartsWith("/", StringComparison.Ordinal) ? relative : "/" + relative).ToString();
    }

    private static UnityWebRequest CreateRequest(string method, string url, string jsonBody)
    {
        method = (method ?? "GET").Trim().ToUpperInvariant();
        if (method == "GET")
            return UnityWebRequest.Get(url);

        var request = new UnityWebRequest(url, method);
        string body = string.IsNullOrWhiteSpace(jsonBody) ? "{}" : jsonBody;
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    private string BuildCanarySaveProgressJson()
    {
        string episodeId = SaveDataSanitizer.SanitizeIdentifier(_testEpisodeId);
        string nodeId = SaveDataSanitizer.SanitizeIdentifier(_testNodeId);
        return "{\"episodeId\":\"" + NetworkJson.Escape(episodeId) + "\",\"nodeId\":\"" + NetworkJson.Escape(nodeId) + "\",\"stats\":{},\"variables\":{},\"snapshot\":null}";
    }

    private static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        string redacted = BearerPattern.Replace(value, "Bearer [REDACTED]");
        return SecretPattern.Replace(redacted, "$1[REDACTED]");
    }

    private static string Trim(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
            return value ?? "";

        return value.Substring(0, maxChars) + "...";
    }
}
