#if UNITY_EDITOR
using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public sealed class AdminDebugCommandsWindow : EditorWindow
{
    private const string BaseUrlPrefsKey = "VN_ADMIN_DEBUG_BASE_URL";
    private const string PlayerIdPrefsKey = "VN_ADMIN_DEBUG_PLAYER_ID";
    private const string AdminKeyEnvironmentVariable = "NOCTURNEDC_ADMIN_KEY";
    private const int MaxPreviewChars = 12000;

    private static readonly Regex SecretPattern = new Regex(
        "(?i)([\"']?\\b(password|token|accessToken|authToken|refreshToken|idToken|restoreCode|secret|apiKey|adminKey|x-admin-key|authorization|cookie|privateKey|session|jwt|purchaseToken|restoreToken|receipt|signature)\\b[\"']?\\s*[:=]\\s*)(\"[^\"]*\"|'[^']*'|[^\\s,;}\\]]+)",
        RegexOptions.Compiled);
    private static readonly Regex BearerPattern = new Regex("(?i)Bearer\\s+[A-Za-z0-9\\-._~+/]+=*", RegexOptions.Compiled);

    [SerializeField] private string _baseUrlOverride = "";
    [NonSerialized] private string _adminKey = "";
    [SerializeField] private string _playerId = "";
    [SerializeField] private int _heartsDelta = 10;
    [SerializeField] private int _candlesDelta = 10;
    [SerializeField] private bool _setHearts = true;
    [SerializeField] private int _heartsValue;
    [SerializeField] private bool _setCandles = true;
    [SerializeField] private int _candlesValue;
    [SerializeField] private bool _setSubscriber;
    [SerializeField] private bool _isSubscriber;
    [SerializeField] private bool _setSubscriptionDays;
    [SerializeField] private int _subscriptionDays = 30;
    [SerializeField] private bool _confirmServerMutation;
    [SerializeField] private bool _fetchAfterMutation = true;
    [SerializeField] private Vector2 _scroll;

    private bool _isBusy;
    private string _lastPlayerCardJson = "";
    private string _lastPayloadJson = "";
    private string _status = "Idle.";

    [MenuItem("VN/Network/Admin Debug Commands")]
    public static void Open()
    {
        var window = GetWindow<AdminDebugCommandsWindow>("Admin Debug");
        window.minSize = new Vector2(620f, 560f);
        window.LoadPrefs();
    }

    private void OnEnable()
    {
        LoadPrefs();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Admin Debug Commands", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Editor-only server debug tool. It is compiled under Assets/_Project/Scripts/Editor and uses only /admin/* endpoints with X-Admin-Key. Runtime client access to /admin/* stays blocked.",
            MessageType.Warning);

        using (new EditorGUI.DisabledScope(_isBusy))
        {
            DrawSettings();
            DrawBalanceCommands();
        }

        DrawStatus();
    }

    private void DrawSettings()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Server", EditorStyles.boldLabel);
        _baseUrlOverride = EditorGUILayout.TextField("Base URL override", _baseUrlOverride);
        _adminKey = EditorGUILayout.PasswordField("X-Admin-Key", ResolveInitialAdminKey());
        EditorGUILayout.HelpBox(
            "Admin key is not saved to the project or EditorPrefs. For local persistence use environment variable " + AdminKeyEnvironmentVariable + ".",
            MessageType.None);

        _playerId = EditorGUILayout.TextField("Player ID", _playerId);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save Local Editor Settings"))
                SavePrefs();

            if (GUILayout.Button("Open API Docs"))
                Application.OpenURL(ApiContract.DocumentationUrl);

            if (GUILayout.Button("Open Log Guide"))
                EditorUtility.OpenWithDefaultApp("Docs/MANDATORY_LOG_TRIAGE.md");
        }
    }

    private void DrawBalanceCommands()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Player Balance", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "POST /admin/players/{id}/balance is documented as: { hearts?, candles?, isSubscriber?, subscriptionDays? }. For test add-currency, use the delta button: it reads current balance first, then posts calculated absolute values.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Fetch Player Card"))
                StartCommand(FetchPlayerCard());
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Add delta safely", EditorStyles.boldLabel);
        _heartsDelta = EditorGUILayout.IntField("Hearts delta", _heartsDelta);
        _candlesDelta = EditorGUILayout.IntField("Candles delta", _candlesDelta);
        _fetchAfterMutation = EditorGUILayout.ToggleLeft("Fetch player card after mutation", _fetchAfterMutation);

        using (new EditorGUI.DisabledScope(!CanMutate()))
        {
            if (GUILayout.Button("Add Delta Via GET + POST"))
                StartCommand(AddBalanceDelta());
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Set/update exact fields", EditorStyles.boldLabel);
        _setHearts = EditorGUILayout.ToggleLeft("Include hearts", _setHearts);
        using (new EditorGUI.DisabledScope(!_setHearts))
            _heartsValue = EditorGUILayout.IntField("Hearts value", _heartsValue);

        _setCandles = EditorGUILayout.ToggleLeft("Include candles", _setCandles);
        using (new EditorGUI.DisabledScope(!_setCandles))
            _candlesValue = EditorGUILayout.IntField("Candles value", _candlesValue);

        _setSubscriber = EditorGUILayout.ToggleLeft("Include isSubscriber", _setSubscriber);
        using (new EditorGUI.DisabledScope(!_setSubscriber))
            _isSubscriber = EditorGUILayout.Toggle("Is subscriber", _isSubscriber);

        _setSubscriptionDays = EditorGUILayout.ToggleLeft("Include subscriptionDays", _setSubscriptionDays);
        using (new EditorGUI.DisabledScope(!_setSubscriptionDays))
            _subscriptionDays = EditorGUILayout.IntField("Subscription days", _subscriptionDays);

        _confirmServerMutation = EditorGUILayout.ToggleLeft("I understand this changes server player data", _confirmServerMutation);
        using (new EditorGUI.DisabledScope(!CanMutate() || !HasAnyExactBalanceField()))
        {
            if (GUILayout.Button("POST Exact Balance Payload"))
                StartCommand(PostBalance(BuildExactBalancePayload()));
        }
    }

    private void DrawStatus()
    {
        EditorGUILayout.Space(8f);
        if (!string.IsNullOrWhiteSpace(_status))
            EditorGUILayout.HelpBox(_status, MessageType.None);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.LabelField("Last Request Body", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(Redact(_lastPayloadJson), GUILayout.MinHeight(90f));
        EditorGUILayout.LabelField("Last Player / Response", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(Trim(Redact(_lastPlayerCardJson), MaxPreviewChars), GUILayout.MinHeight(180f));
        EditorGUILayout.EndScrollView();
    }

    private bool CanMutate()
    {
        return !_isBusy &&
               _confirmServerMutation &&
               !string.IsNullOrWhiteSpace(_playerId) &&
               !string.IsNullOrWhiteSpace(ResolveAdminKey());
    }

    private bool HasAnyExactBalanceField()
    {
        return _setHearts || _setCandles || _setSubscriber || _setSubscriptionDays;
    }

    private IEnumerator FetchPlayerCard()
    {
        string path = ApiRoutes.AdminPlayer(_playerId);
        yield return SendJsonRequest("GET", path, "", result =>
        {
            _lastPlayerCardJson = FormatResult("GET", path, result, "");
            _status = result.Success ? "Player card fetched." : "Fetch failed.";
        });
    }

    private IEnumerator AddBalanceDelta()
    {
        string playerPath = ApiRoutes.AdminPlayer(_playerId);
        AdminDebugRequestResult playerResult = null;
        yield return SendJsonRequest("GET", playerPath, "", result => playerResult = result);

        if (playerResult == null || !playerResult.Success)
        {
            _lastPlayerCardJson = FormatResult("GET", playerPath, playerResult, "");
            _status = "Cannot add delta: player card fetch failed.";
            yield break;
        }

        _lastPlayerCardJson = FormatResult("GET", playerPath, playerResult, "");
        if (!TryReadBalance(playerResult.Body, out int currentHearts, out int currentCandles))
        {
            _status = "Cannot add delta: response does not contain readable hearts/candles.";
            yield break;
        }

        int nextHearts = Mathf.Max(0, currentHearts + _heartsDelta);
        int nextCandles = Mathf.Max(0, currentCandles + _candlesDelta);
        string payload = "{\"hearts\":" + nextHearts + ",\"candles\":" + nextCandles + "}";
        yield return PostBalance(payload);
    }

    private IEnumerator PostBalance(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload == "{}")
        {
            _status = "No balance fields selected.";
            yield break;
        }

        string path = ApiRoutes.AdminPlayerBalance(_playerId);
        yield return SendJsonRequest("POST", path, payload, result =>
        {
            _lastPlayerCardJson = FormatResult("POST", path, result, payload);
            _status = result.Success ? "Balance mutation completed." : "Balance mutation failed.";
        });

        if (_fetchAfterMutation)
            yield return FetchPlayerCard();
    }

    private string BuildExactBalancePayload()
    {
        var builder = new StringBuilder();
        builder.Append('{');
        bool hasField = false;
        AppendIntField(builder, "hearts", Mathf.Max(0, _heartsValue), _setHearts, ref hasField);
        AppendIntField(builder, "candles", Mathf.Max(0, _candlesValue), _setCandles, ref hasField);
        AppendBoolField(builder, "isSubscriber", _isSubscriber, _setSubscriber, ref hasField);
        AppendIntField(builder, "subscriptionDays", Mathf.Max(0, _subscriptionDays), _setSubscriptionDays, ref hasField);
        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendIntField(StringBuilder builder, string key, int value, bool include, ref bool hasField)
    {
        if (!include)
            return;

        AppendCommaIfNeeded(builder, ref hasField);
        builder.Append('"').Append(key).Append("\":").Append(value);
    }

    private static void AppendBoolField(StringBuilder builder, string key, bool value, bool include, ref bool hasField)
    {
        if (!include)
            return;

        AppendCommaIfNeeded(builder, ref hasField);
        builder.Append('"').Append(key).Append("\":").Append(value ? "true" : "false");
    }

    private static void AppendCommaIfNeeded(StringBuilder builder, ref bool hasField)
    {
        if (hasField)
            builder.Append(',');

        hasField = true;
    }

    private static bool TryReadBalance(string json, out int hearts, out int candles)
    {
        hearts = 0;
        candles = 0;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        string nested = FirstNonEmpty(
            NetworkJson.GetRawValue(json, "balances"),
            NetworkJson.GetRawValue(json, "balance"));
        string source = string.IsNullOrWhiteSpace(nested) ? json : nested;

        bool hasHearts = NetworkJson.GetRawValue(source, "hearts") != null;
        bool hasCandles = NetworkJson.GetRawValue(source, "candles") != null;
        hearts = NetworkJson.GetInt(source, "hearts", 0);
        candles = NetworkJson.GetInt(source, "candles", 0);
        return hasHearts || hasCandles;
    }

    private void StartCommand(IEnumerator routine)
    {
        if (_isBusy)
            return;

        if (string.IsNullOrWhiteSpace(_playerId))
        {
            _status = "Player ID is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ResolveAdminKey()))
        {
            _status = "X-Admin-Key is required. Fill the field or set " + AdminKeyEnvironmentVariable + ".";
            return;
        }

        _isBusy = true;
        _status = "Running...";
        Repaint();
        EditorCoroutineRunner.Start(WrapCommand(routine));
    }

    private IEnumerator WrapCommand(IEnumerator routine)
    {
        while (true)
        {
            object current = null;
            bool moveNext = false;
            try
            {
                moveNext = routine != null && routine.MoveNext();
                if (moveNext)
                    current = routine.Current;
            }
            catch (Exception exception)
            {
                _status = "Admin debug command failed: " + exception.Message;
                AppLogger.Error(
                    AppLogCategory.Error,
                    nameof(AdminDebugCommandsWindow),
                    nameof(WrapCommand),
                    "[ADMIN_DEBUG][EXCEPTION] Admin debug command crashed.",
                    exception,
                    LogMetadata.Of("playerId", _playerId));
                break;
            }

            if (!moveNext)
                break;

            yield return current;
        }

        _isBusy = false;
        Repaint();
    }

    private IEnumerator SendJsonRequest(string method, string path, string jsonBody, Action<AdminDebugRequestResult> callback)
    {
        string safePath = NormalizeAllowedAdminPath(path);
        if (string.IsNullOrEmpty(safePath))
        {
            callback?.Invoke(AdminDebugRequestResult.Fail(0, "", "Blocked admin debug path."));
            yield break;
        }

        string url = BuildUrl(safePath, out string urlError);
        if (string.IsNullOrEmpty(url))
        {
            callback?.Invoke(AdminDebugRequestResult.Fail(0, "", urlError));
            yield break;
        }

        _lastPayloadJson = string.IsNullOrWhiteSpace(jsonBody) ? "" : jsonBody;
        using (UnityWebRequest request = CreateRequest(method, url, jsonBody))
        {
            if (request == null)
            {
                callback?.Invoke(AdminDebugRequestResult.Fail(0, "", "Unsupported method."));
                yield break;
            }

            request.timeout = 20;
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Cache-Control", "no-store");
            request.SetRequestHeader("X-Admin-Key", ResolveAdminKey());

            AppLogger.Info(
                AppLogCategory.Security,
                nameof(AdminDebugCommandsWindow),
                nameof(SendJsonRequest),
                "[ADMIN_DEBUG][REQUEST] Sending editor-only admin debug request.",
                LogMetadata.Of(
                    "method", method,
                    "path", ApiContract.RedactedEndpointForLog(safePath),
                    "payloadChars", string.IsNullOrEmpty(jsonBody) ? 0 : jsonBody.Length));

            yield return request.SendWebRequest();

            string body = request.downloadHandler != null ? request.downloadHandler.text : "";
            var result = new AdminDebugRequestResult
            {
                Success = request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300,
                StatusCode = request.responseCode,
                Body = body ?? "",
                Error = request.result == UnityWebRequest.Result.Success ? "" : request.error
            };

            AppLogger.Info(
                result.Success ? AppLogCategory.Security : AppLogCategory.Error,
                nameof(AdminDebugCommandsWindow),
                nameof(SendJsonRequest),
                result.Success
                    ? "[ADMIN_DEBUG][RESPONSE] Admin debug request completed."
                    : "[ADMIN_DEBUG][RESPONSE] Admin debug request failed.",
                LogMetadata.Of(
                    "method", method,
                    "path", ApiContract.RedactedEndpointForLog(safePath),
                    "statusCode", result.StatusCode,
                    "responseChars", result.Body.Length,
                    "error", result.Error));

            callback?.Invoke(result);
        }
    }

    private static UnityWebRequest CreateRequest(string method, string url, string jsonBody)
    {
        method = (method ?? "GET").Trim().ToUpperInvariant();
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

    private static string NormalizeAllowedAdminPath(string path)
    {
        string normalized = ApiContract.NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
            return "";

        if (ApiContract.Find("GET", normalized) != null &&
            normalized.StartsWith(ApiRoutes.AdminPlayers, StringComparison.Ordinal))
        {
            return normalized;
        }

        if (ApiContract.Find("POST", normalized) != null &&
            normalized.StartsWith(ApiRoutes.AdminPlayers, StringComparison.Ordinal) &&
            normalized.EndsWith("/balance", StringComparison.Ordinal))
        {
            return normalized;
        }

        return "";
    }

    private string BuildUrl(string path, out string error)
    {
        error = "";
        string root = FirstNonEmpty(_baseUrlOverride, NetworkRuntimeConfigLoader.Load()?.ResolveBaseUrl(), ApiRoutes.BaseUrl);
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
            error = "Admin debug requires HTTPS, except local loopback.";
            return "";
        }

        return new Uri(baseUri, path).ToString();
    }

    private string ResolveInitialAdminKey()
    {
        if (!string.IsNullOrWhiteSpace(_adminKey))
            return _adminKey;

        return Environment.GetEnvironmentVariable(AdminKeyEnvironmentVariable) ?? "";
    }

    private string ResolveAdminKey()
    {
        return FirstNonEmpty(_adminKey, Environment.GetEnvironmentVariable(AdminKeyEnvironmentVariable));
    }

    private void LoadPrefs()
    {
        _baseUrlOverride = EditorPrefs.GetString(BaseUrlPrefsKey, _baseUrlOverride);
        _playerId = EditorPrefs.GetString(PlayerIdPrefsKey, _playerId);
    }

    private void SavePrefs()
    {
        EditorPrefs.SetString(BaseUrlPrefsKey, _baseUrlOverride ?? "");
        EditorPrefs.SetString(PlayerIdPrefsKey, _playerId ?? "");
        _status = "Local editor settings saved. Admin key was not saved.";
    }

    private static string FormatResult(string method, string path, AdminDebugRequestResult result, string requestBody)
    {
        if (result == null)
            return "No result.";

        return
            "Method: " + method + "\n" +
            "Path: " + ApiContract.RedactedEndpointForLog(path) + "\n" +
            "Status: " + result.StatusCode + "\n" +
            "Success: " + result.Success + "\n" +
            "Error: " + Redact(result.Error) + "\n" +
            "Request Body: " + Redact(requestBody) + "\n" +
            "Body: " + Trim(Redact(result.Body), MaxPreviewChars);
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

        return value.Substring(0, maxChars) + "\n...[trimmed]";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return "";
    }

    private sealed class AdminDebugRequestResult
    {
        public bool Success;
        public long StatusCode;
        public string Body;
        public string Error;

        public static AdminDebugRequestResult Fail(long statusCode, string body, string error)
        {
            return new AdminDebugRequestResult
            {
                Success = false,
                StatusCode = statusCode,
                Body = body ?? "",
                Error = error ?? ""
            };
        }
    }
}
#endif
