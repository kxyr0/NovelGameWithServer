using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

/// <summary>
/// Singleton — связь с NovelApp Backend.
///
/// Принцип: offline-first.
/// - Все игровые данные хранятся локально (PlayerPrefs / GameState).
/// - Сервер синхронизируется в фоне, ошибки сети не ломают игру.
/// - При первом запуске регистрирует гостевого игрока и получает JWT.
/// - При последующих запусках — восстанавливает сессию.
///
/// Подключение:
/// 1. Создай GameObject "NetworkManager" в стартовой сцене (DontDestroyOnLoad).
/// 2. Прикрепи этот скрипт.
/// 3. Заполни baseUrl (например https://nocturnedc.ru).
/// 4. (Опционально) Включи syncOnStart — при старте синхронизирует баланс и прогресс.
/// </summary>
public sealed partial class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    [Header("Server")]
    [Tooltip("Базовый адрес сервера. Используй домен с действующим TLS-сертификатом, а не прямой IP.")]
    [FormerlySerializedAs("baseUrl")]
    [SerializeField, HideInInspector] private string legacyBaseUrl = "";

    [Header("Sync settings")]
    [Tooltip("При запуске автоматически синхронизировать баланс и прогресс игрока.")]
    [SerializeField] private bool syncOnStart = true;
    [Tooltip("Интервал периодической синхронизации в секундах. 0 отключает автосинхронизацию.")]
    [SerializeField] private float syncIntervalSeconds = 120f;

    public bool SyncOnStart => syncOnStart;
    public float SyncIntervalSeconds => syncIntervalSeconds;

    private NetworkRuntimeConfigData _runtimeConfig;
    private NetworkHttpClient _httpClient;
    private string _resolvedBaseUrl = "";
    private bool _showOnlineToastOnRecovery = false;

    // ── Состояние ──────────────────────────────────────────────

    public static bool IsOnline { get; private set; } = false;
    public static bool IsAuthenticated { get; private set; } = false;
    public static bool AuthFlowCompleted { get; private set; } = false;
    public static string LastNetworkError { get; private set; } = "";
    public static NetworkErrorKind LastErrorKind { get; private set; } = NetworkErrorKind.Success;
    public static string ActiveEnvironmentId => Instance != null ? Instance.GetActiveEnvironmentId() : "";
    public static string ActiveBaseUrl => Instance != null ? Instance.GetActiveBaseUrl() : "";
    public static PlayerProfileState CurrentProfile => _currentProfile;
    public static PlayerBalanceState LastBalance => _lastBalance;
    public static bool HasPendingSync => _pendingProgress.Count > 0 || _pendingBookmarks.Count > 0;

    // Токен + ID
    private static string _authToken;
    private static string _refreshToken;
    private static string _playerId;
    private static readonly PlayerProfileState _currentProfile = new PlayerProfileState();
    private static readonly PlayerBalanceState _lastBalance = new PlayerBalanceState();
    private static readonly NetworkPendingSyncStore _pendingSyncStore = new NetworkPendingSyncStore();
    private static bool _serverBookmarkLocked;
    private bool _periodicSyncScheduled;
    private NetworkErrorKind _lastRestoreFailureKind = NetworkErrorKind.Success;

    // Флаги фичей (обновляются с сервера)
    public static bool FullAccessEnabled { get; private set; } = false;
    public static bool FastForwardEnabled { get; private set; } = false;
    public static int FastForwardSteps { get; private set; } = 5;
    public static bool BookmarksEnabled { get; private set; } = false;
    public static int BookmarkCapacity { get; private set; } = 30;

    // Последний загруженный прогресс
    public static string LastProgressNodeGuid { get; private set; }
    public static string LastProgressEpisodeId { get; private set; }
    public static string LastProgressSnapshotJson { get; private set; }
    public static string LastProgressRawJson { get; private set; }
    public static string LastProgressUpdatedAtIso { get; private set; }
    public static IReadOnlyList<string> LastUnlockedEpisodes => _lastUnlockedEpisodes;
    public static IReadOnlyDictionary<string, int> LastProgressStats => _lastProgressStats;
    public static IReadOnlyDictionary<string, bool> LastProgressFlags => _lastProgressFlags;
    public static IReadOnlyList<CatalogSeasonResponse> CatalogSeasons => _catalogSeasons;

    private static readonly List<string> _lastUnlockedEpisodes = new List<string>();
    private static readonly Dictionary<string, int> _lastProgressStats = new Dictionary<string, int>();
    private static readonly Dictionary<string, bool> _lastProgressFlags = new Dictionary<string, bool>();
    private static readonly List<CatalogSeasonResponse> _catalogSeasons = new List<CatalogSeasonResponse>();
    private static readonly Dictionary<string, CatalogEpisodeResponse> _catalogEpisodes = new Dictionary<string, CatalogEpisodeResponse>();
    private static readonly Dictionary<string, PendingProgressPayload> _pendingProgress = new Dictionary<string, PendingProgressPayload>();
    private static readonly Dictionary<string, PendingBookmarkPayload> _pendingBookmarks = new Dictionary<string, PendingBookmarkPayload>();

    // PlayerPrefs keys
    private const string KEY_TOKEN = "VN_AUTH_TOKEN";
    private const string KEY_PLAYER_ID = "VN_PLAYER_ID";
    private const string KEY_DEVICE_ID = "VN_DEVICE_ID";
    private const int MaxCredentialLength = 8192;
    private const int MaxCandlesSpendBatch = 1000;
    private const int MaxRemoteGraphJsonChars = 1024 * 1024;
    private const int MaxServerFastForwardSteps = 100;
    private const int MaxServerBookmarkCapacity = 200;
    private const int MaxCatalogSeasons = 100;
    private const int MaxCatalogEpisodes = 1000;
    private const int MaxCatalogOrder = 10000;
    private const int MaxNetworkErrorChars = 256;
    private const int MaxProgressResponseChars = 1024 * 1024;

    // ── События ────────────────────────────────────────────────

    /// <summary>Вызывается когда с сервера пришли флаги фичей.</summary>
    public static event Action OnFeaturesUpdated;

    /// <summary>Вызывается когда с сервера загрузился прогресс (передаёт сырой JSON).</summary>
    public static event Action<string> OnProgressLoaded;
    public static event Action<bool, string> OnConnectivityChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
        IsOnline = false;
        IsAuthenticated = false;
        AuthFlowCompleted = false;
        LastNetworkError = "";
        LastErrorKind = NetworkErrorKind.Success;
        _authToken = null;
        _refreshToken = null;
        _playerId = null;
        ResetProfileState();
        ResetBalanceState();
        _serverBookmarkLocked = false;
        FullAccessEnabled = false;
        FastForwardEnabled = false;
        FastForwardSteps = 5;
        BookmarksEnabled = false;
        BookmarkCapacity = 30;
        LastProgressNodeGuid = "";
        LastProgressEpisodeId = "";
        LastProgressSnapshotJson = "";
        LastProgressRawJson = "";
        LastProgressUpdatedAtIso = "";
        _lastUnlockedEpisodes.Clear();
        _lastProgressStats.Clear();
        _lastProgressFlags.Clear();
        _catalogSeasons.Clear();
        _catalogEpisodes.Clear();
        _pendingProgress.Clear();
        _pendingBookmarks.Clear();
        OnFeaturesUpdated = null;
        OnProgressLoaded = null;
        OnConnectivityChanged = null;
        ResetUiTextState();
    }

    // ── Unity lifecycle ────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureNetworkManagerExists()
    {
        if (Instance != null)
            return;

        NetworkManager existingManager = FindObjectOfType<NetworkManager>(true);
        if (existingManager != null)
        {
            existingManager.transform.SetParent(null);
            if (!existingManager.gameObject.activeSelf)
                existingManager.gameObject.SetActive(true);

            return;
        }

        GameObject networkManagerObject = new GameObject(nameof(NetworkManager));
        networkManagerObject.AddComponent<NetworkManager>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _runtimeConfig = NetworkRuntimeConfigLoader.Load();
        _resolvedBaseUrl = _runtimeConfig.ResolveBaseUrl(legacyBaseUrl);
        EnsureHttpClient();
        if (string.IsNullOrWhiteSpace(_resolvedBaseUrl))
        {
            AppLogger.Error(
                AppLogCategory.Server,
                nameof(NetworkManager),
                nameof(Awake),
                "No API base URL is configured.",
                null,
                LogMetadata.Of("resourcePath", NetworkRuntimeConfigLoader.DefaultResourcePath),
                recoverable: false);
        }

        string deviceId = GetOrCreateDeviceId();
        _authToken = null;
        _refreshToken = SanitizeCredential(NetworkCredentialStore.LoadRefreshToken(deviceId));
        _playerId = SanitizeCredential(PlayerPrefs.GetString(KEY_PLAYER_ID, null), SaveDataSanitizer.MaxIdChars);
        DeleteLegacyAccessToken();
        LoadPendingSyncFromPrefs();

        AppLogger.Info(
            AppLogCategory.Server,
            nameof(NetworkManager),
            nameof(Awake),
            "Network manager initialized.",
            LogMetadata.Of(
                "environmentId", GetActiveEnvironmentId(),
                "baseHost", GetSafeBaseHost(_resolvedBaseUrl),
                "hasBaseUrl", !string.IsNullOrWhiteSpace(_resolvedBaseUrl),
                "syncOnStart", syncOnStart,
                "syncIntervalSeconds", syncIntervalSeconds,
                "hasRefreshCredential", !string.IsNullOrEmpty(_refreshToken),
                "hasPlayerId", !string.IsNullOrEmpty(_playerId),
                "hasPendingSync", HasPendingSync));
    }

    private void OnDestroy()
    {
        if (_periodicSyncScheduled)
            CancelInvoke(nameof(PeriodicSync));

        if (Instance == this)
            Instance = null;

        AppLogger.Info(
            AppLogCategory.Server,
            nameof(NetworkManager),
            nameof(OnDestroy),
            "Network manager destroyed.",
            LogMetadata.Of("periodicSyncScheduled", _periodicSyncScheduled));
    }

    private void Start()
    {
        AppLogger.Info(
            AppLogCategory.Auth,
            nameof(NetworkManager),
            nameof(Start),
            "Starting authentication flow.",
            LogMetadata.Of("hasRefreshCredential", !string.IsNullOrEmpty(_refreshToken), "hasPlayerId", !string.IsNullOrEmpty(_playerId)));
        StartCoroutine(AuthFlow());
    }

    // ── Auth Flow ──────────────────────────────────────────────

    /// <summary>Инициализация: restore → если нет, guest.</summary>
    private IEnumerator AuthFlow()
    {
        long startedAt = AppDiagnostics.StartTimer();
        AuthFlowCompleted = false;
        var deviceId = GetOrCreateDeviceId();

        if (!string.IsNullOrEmpty(_refreshToken) && !string.IsNullOrEmpty(_playerId))
        {
            bool restored = false;
            yield return RestoreSession(deviceId, _refreshToken, success => restored = success);
            if (!restored && ShouldStartGuestAfterRestoreFailure(_lastRestoreFailureKind))
                yield return GuestAuth(deviceId);
        }
        else if (!string.IsNullOrEmpty(_authToken) && !string.IsNullOrEmpty(_playerId))
        {
            bool tokenStillWorks = false;
            yield return ProbeExistingToken(_authToken, success => tokenStillWorks = success);
            if (tokenStillWorks)
                StartPostAuthSync();
            else if (ShouldStartGuestAfterRestoreFailure(_lastRestoreFailureKind))
                yield return GuestAuth(deviceId);
        }
        else
        {
            yield return GuestAuth(deviceId);
        }

        AuthFlowCompleted = true;
        long durationMs = AppDiagnostics.ElapsedMilliseconds(startedAt);
        if (IsAuthenticated)
        {
            AppLogger.Info(
                AppLogCategory.Auth,
                nameof(NetworkManager),
                nameof(AuthFlow),
                "Authentication flow completed.",
                LogMetadata.Of(
                    "isOnline", IsOnline,
                    "lastErrorKind", LastErrorKind,
                    "usedRefreshCredential", !string.IsNullOrEmpty(_refreshToken),
                    "playerIdPresent", !string.IsNullOrEmpty(_playerId)),
                durationMs);
        }
        else
        {
            AppLogger.Warn(
                AppLogCategory.Auth,
                nameof(NetworkManager),
                nameof(AuthFlow),
                "Authentication flow completed without an authenticated session.",
                LogMetadata.Of("lastErrorKind", LastErrorKind, "lastNetworkError", LastNetworkError),
                durationMs,
                recoverable: true);
        }

        AppDiagnostics.LogIfSlow(AppLogCategory.Auth, nameof(NetworkManager), nameof(AuthFlow), durationMs);
    }

    private IEnumerator GuestAuth(string deviceId)
    {
        var body = new GuestAuthRequest
        {
            deviceId = deviceId,
            platform = GetPlatform(),
            appVersion = Application.version
        };

        yield return Post(ApiRoutes.AuthGuest, body, null, (json, err) =>
        {
            if (err != null)
            {
                AppLogger.Warn(
                    AppLogCategory.Auth,
                    nameof(NetworkManager),
                    nameof(GuestAuth),
                    "Guest authentication failed.",
                    LogMetadata.Of("endpoint", ApiRoutes.AuthGuest, "error", err),
                    recoverable: true);
                return;
            }

            if (ApplyAuthResponse(json))
            {
                ApplyGuestProfileDefaults(body);
                StartPostAuthSync();
            }
        });
    }

    private IEnumerator RestoreSession(string deviceId, string refreshToken, Action<bool> callback, bool startSync = true)
    {
        NetworkRequestResult restoreResult = null;

        yield return SendRequest(
            () => _httpClient.CreateJsonPostRequest(
                ApiRoutes.AuthRefresh,
                NetworkJson.ToJson(new RefreshAuthRequest { refreshToken = refreshToken ?? "" }),
                null),
            result => restoreResult = result,
            allowRetry: true);

        if (restoreResult == null)
        {
            callback?.Invoke(false);
            yield break;
        }

        if (!restoreResult.IsSuccess)
        {
            AppLogger.Warn(
                AppLogCategory.Auth,
                nameof(NetworkManager),
                nameof(RestoreSession),
                "Refresh-token authentication failed.",
                LogMetadata.Of(
                    "requestId", restoreResult.RequestId,
                    "endpoint", ApiRoutes.AuthRefresh,
                    "statusCode", restoreResult.ResponseCode,
                    "kind", restoreResult.Kind,
                    "error", restoreResult.Error),
                recoverable: true);
            _lastRestoreFailureKind = restoreResult.Kind;

            if (ShouldProbeTokenAfterRestoreFailure(restoreResult))
            {
                bool tokenStillWorks = false;
                yield return ProbeExistingToken(_authToken, success => tokenStillWorks = success);
                if (tokenStillWorks)
                {
                    _lastRestoreFailureKind = NetworkErrorKind.Success;
                    if (startSync)
                        StartPostAuthSync();

                    callback?.Invoke(true);
                    yield break;
                }
            }

            if (ShouldClearAuthAfterRestoreFailure(restoreResult.Kind))
                ClearAuthSession();

            callback?.Invoke(false);
            yield break;
        }

        bool applied = ApplyAuthResponse(restoreResult.Text);
        _lastRestoreFailureKind = applied ? NetworkErrorKind.Success : NetworkErrorKind.InvalidResponse;
        if (applied && startSync)
            StartPostAuthSync();

        callback?.Invoke(applied);
    }

    private static bool ShouldClearAuthAfterRestoreFailure(NetworkErrorKind kind)
    {
        return kind == NetworkErrorKind.Unauthorized ||
               kind == NetworkErrorKind.ClientError ||
               kind == NetworkErrorKind.InvalidResponse;
    }

    private static bool ShouldStartGuestAfterRestoreFailure(NetworkErrorKind kind)
    {
        return kind != NetworkErrorKind.Success;
    }

    private static bool ShouldProbeTokenAfterRestoreFailure(NetworkRequestResult result)
    {
        return result != null &&
               result.Kind != NetworkErrorKind.Success &&
               result.Kind != NetworkErrorKind.Offline &&
               result.Kind != NetworkErrorKind.Timeout &&
               !string.IsNullOrEmpty(_authToken) &&
               !string.IsNullOrEmpty(_playerId);
    }

    private IEnumerator ProbeExistingToken(string token, Action<bool> callback)
    {
        if (string.IsNullOrEmpty(token))
        {
            callback?.Invoke(false);
            yield break;
        }

        NetworkRequestResult probeResult = null;
        yield return SendRequest(
            () => _httpClient.CreateGetRequest(ApiRoutes.PlayerBalance, token),
            result => probeResult = result,
            allowRetry: true);

        if (probeResult == null || !probeResult.IsSuccess)
        {
            _lastRestoreFailureKind = probeResult != null ? probeResult.Kind : NetworkErrorKind.Offline;
            callback?.Invoke(false);
            yield break;
        }

        _lastRestoreFailureKind = NetworkErrorKind.Success;
        IsAuthenticated = true;
        UpdateConnectivityState(true, null);
        _currentProfile.playerId = _playerId ?? "";

        try
        {
            var balance = NetworkJson.FromJson<BalanceResponse>(probeResult.Text);
            if (balance != null)
                ApplyBalance(balance);
        }
        catch (Exception e)
        {
            AppLogger.Error(
                AppLogCategory.Auth,
                nameof(NetworkManager),
                nameof(ProbeExistingToken),
                "Existing token probe succeeded, but balance response could not be parsed.",
                e,
                LogMetadata.Of("endpoint", ApiRoutes.PlayerBalance),
                recoverable: true);
        }

        AppLogger.Info(
            AppLogCategory.Auth,
            nameof(NetworkManager),
            nameof(ProbeExistingToken),
            "Existing bearer token is valid after refresh failure.",
            LogMetadata.Of("endpoint", ApiRoutes.PlayerBalance));
        callback?.Invoke(true);
    }

    private bool ApplyAuthResponse(string json)
    {
        try
        {
            if (!NetworkJson.LooksLikeJsonObject(json))
                throw new Exception("Auth response is not a JSON object");

            var r = NetworkJson.FromJson<AuthResponse>(json);
            if (r == null)
                throw new Exception("Empty auth response");

            _authToken = SanitizeCredential(FirstNonEmptyRawString(
                r.authToken,
                r.token,
                NetworkJson.GetString(json, "authToken"),
                NetworkJson.GetString(json, "token")));
            _refreshToken = SanitizeCredential(FirstNonEmptyRawString(
                r.refreshToken,
                NetworkJson.GetString(json, "refreshToken"),
                _refreshToken));
            _playerId = SanitizeCredential(FirstNonEmptyRawString(
                r.playerId,
                NetworkJson.GetString(json, "playerId"),
                _playerId), SaveDataSanitizer.MaxIdChars);
            if (string.IsNullOrEmpty(_authToken) || string.IsNullOrEmpty(_playerId))
                throw new Exception("Auth response has no token or playerId");

            IsAuthenticated = true;
            UpdateConnectivityState(true, null);
            ApplyProfileFromAuth(r, json);

            PlayerPrefs.DeleteKey(KEY_TOKEN);
            if (!string.IsNullOrEmpty(_refreshToken))
                NetworkCredentialStore.SaveRefreshToken(_refreshToken, GetOrCreateDeviceId());
            else
                NetworkCredentialStore.ClearRefreshToken();
            PlayerPrefs.SetString(KEY_PLAYER_ID, _playerId);
            PlayerPrefs.Save();

            if (r.balances != null)
                ApplyBalance(r.balances);

            if (r.progress != null && !string.IsNullOrWhiteSpace(r.progress.data) && !ApplyLoadedProgressJson(r.progress.data))
            {
                AppLogger.Warn(
                    AppLogCategory.SaveSystem,
                    nameof(NetworkManager),
                    nameof(ApplyAuthResponse),
                    "Auth bootstrap progress payload was rejected.",
                    LogMetadata.Of("playerIdPresent", !string.IsNullOrEmpty(_playerId)),
                    recoverable: true);
            }

            AppLogger.Info(
                AppLogCategory.Auth,
                nameof(NetworkManager),
                nameof(ApplyAuthResponse),
                "Authentication response applied.",
                LogMetadata.Of(
                    "playerIdPresent", !string.IsNullOrEmpty(_playerId),
                    "refreshCredentialPresent", !string.IsNullOrEmpty(_refreshToken),
                    "isNewPlayer", r.isNew,
                    "hasBootstrapProgress", r.progress != null));
            return true;
        }
        catch (Exception e)
        {
            AppLogger.Error(
                AppLogCategory.Auth,
                nameof(NetworkManager),
                nameof(ApplyAuthResponse),
                "Failed to apply authentication response.",
                e,
                LogMetadata.Of("jsonLooksLikeObject", NetworkJson.LooksLikeJsonObject(json)),
                recoverable: true);
            ClearAuthSession();
            SetLastError(NetworkErrorKind.InvalidResponse, e.Message);
            return false;
        }
    }

    private void ApplyGuestProfileDefaults(GuestAuthRequest request)
    {
        if (request == null)
            return;

        if (string.IsNullOrEmpty(_currentProfile.locale))
            _currentProfile.locale = Application.systemLanguage.ToString();

        if (string.IsNullOrEmpty(_currentProfile.platform))
            _currentProfile.platform = request.platform ?? "";
    }

    private void StartPostAuthSync()
    {
        if (syncOnStart)
            StartCoroutine(SyncAll());

        if (syncIntervalSeconds > 0 && !_periodicSyncScheduled)
        {
            InvokeRepeating(nameof(PeriodicSync), syncIntervalSeconds, syncIntervalSeconds);
            _periodicSyncScheduled = true;
        }

        AppLogger.Info(
            AppLogCategory.Network,
            nameof(NetworkManager),
            nameof(StartPostAuthSync),
            "Post-authentication sync scheduled.",
            LogMetadata.Of(
                "syncOnStart", syncOnStart,
                "syncIntervalSeconds", syncIntervalSeconds,
                "periodicSyncScheduled", _periodicSyncScheduled));
    }

    // ── Синхронизация ──────────────────────────────────────────

    private void PeriodicSync()
    {
        AppLogger.DebugLog(
            AppLogCategory.Diagnostics,
            nameof(NetworkManager),
            nameof(PeriodicSync),
            "Periodic sync tick.",
            LogMetadata.Of("isAuthenticated", IsAuthenticated, "hasPendingSync", HasPendingSync));
        StartCoroutine(SyncAll());
    }

    private IEnumerator SyncAll()
    {
        long startedAt = AppDiagnostics.StartTimer();
        if (!IsAuthenticated)
        {
            AppLogger.DebugLog(
                AppLogCategory.Network,
                nameof(NetworkManager),
                nameof(SyncAll),
                "Skipped sync because the player is not authenticated.");
            yield break;
        }

        yield return SyncCatalog();
        yield return SyncBalance();
        yield return SyncFeatures();
        yield return SyncHeroName();
        yield return LoadProgress();
        yield return SyncWardrobeOwnership();
        yield return FlushPendingSync();

        AppDiagnostics.LogOperationCompleted(
            AppLogCategory.Network,
            nameof(NetworkManager),
            nameof(SyncAll),
            "Network sync completed.",
            startedAt,
            LogMetadata.Of("hasPendingSync", HasPendingSync, "lastErrorKind", LastErrorKind));
    }

    /// <summary>Получить баланс с сервера → обновить локальный PlayerData.</summary>
    public IEnumerator SyncBalance(Action<bool> callback = null)
    {
        if (!IsAuthenticated)
        {
            callback?.Invoke(false);
            yield break;
        }

        yield return GetInternal(ApiRoutes.PlayerBalance, (json, err) =>
        {
            if (err != null)
            {
                callback?.Invoke(false);
                return;
            }

            try
            {
                if (!NetworkJson.LooksLikeJsonObject(json))
                    throw new Exception("Balance response is not a JSON object");

                var r = NetworkJson.FromJson<BalanceResponse>(json);
                if (r == null)
                    throw new Exception("Empty balance response");

                ApplyBalance(r);
                callback?.Invoke(true);
            }
            catch (Exception e)
            {
                AppLogger.Error(
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(SyncBalance),
                    "Failed to parse player balance response.",
                    e,
                    LogMetadata.Of("endpoint", ApiRoutes.PlayerBalance),
                    recoverable: true);
                SetLastError(NetworkErrorKind.InvalidResponse, e.Message);
                callback?.Invoke(false);
            }
        });
    }

    /// <summary>Получить флаги подписочных функций с сервера.</summary>
    public IEnumerator SyncFeatures(Action<bool> callback = null)
    {
        if (!IsAuthenticated)
        {
            callback?.Invoke(false);
            yield break;
        }

        yield return GetInternal(ApiRoutes.PlayerFeatures, (json, err) =>
        {
            if (err != null)
            {
                callback?.Invoke(false);
                return;
            }

            try
            {
                if (!NetworkJson.LooksLikeJsonObject(json))
                    throw new Exception("Features response is not a JSON object");

                var r = NetworkJson.FromJson<FeaturesResponse>(json);
                if (r == null)
                    throw new Exception("Empty features response");

                if (NetworkJson.GetRawValue(json, "fullAccess") != null)
                    FullAccessEnabled = NetworkJson.GetBool(json, "fullAccess", FullAccessEnabled);

                if (r.fastForward != null)
                {
                    FastForwardEnabled = r.fastForward.enabled;
                    FastForwardSteps = r.fastForward.steps > 0
                        ? Mathf.Clamp(r.fastForward.steps, 1, MaxServerFastForwardSteps)
                        : 5;
                }

                if (r.bookmarks != null)
                {
                    BookmarksEnabled = r.bookmarks.enabled;
                    BookmarkCapacity = r.bookmarks.capacity > 0
                        ? Mathf.Clamp(r.bookmarks.capacity, 1, MaxServerBookmarkCapacity)
                        : 30;
                    if (BookmarksEnabled)
                        _serverBookmarkLocked = false;
                }

                OnFeaturesUpdated?.Invoke();
                callback?.Invoke(true);
            }
            catch (Exception e)
            {
                AppLogger.Error(
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(SyncFeatures),
                    "Failed to parse player features response.",
                    e,
                    LogMetadata.Of("endpoint", ApiRoutes.PlayerFeatures),
                    recoverable: true);
                SetLastError(NetworkErrorKind.InvalidResponse, e.Message);
                callback?.Invoke(false);
            }
        });
    }

    /// <summary>Получить имя героини с сервера → применить в PlayerAppearance.</summary>
    public IEnumerator SyncHeroName(Action<bool> callback = null)
    {
        if (!IsAuthenticated)
        {
            callback?.Invoke(false);
            yield break;
        }

        string storyId = ResolveActiveStoryIdForNetwork();
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        string path = ApiRoutes.PlayerHeroNameForStory(storyId);

        yield return GetInternal(path, (json, err) =>
        {
            if (err != null)
            {
                callback?.Invoke(false);
                return;
            }

            try
            {
                if (!NetworkJson.LooksLikeJsonObject(json))
                    throw new Exception("Hero-name response is not a JSON object");

                var r = NetworkJson.FromJson<HeroNameResponse>(json);
                if (r == null)
                    throw new Exception("Empty hero-name response");

                TryApplyIncomingHeroName(storyId, r.heroName, out _);

                callback?.Invoke(true);
            }
            catch (Exception e)
            {
                AppLogger.Error(
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(SyncHeroName),
                    "Failed to parse hero-name response.",
                    e,
                    LogMetadata.Of("endpoint", path),
                    recoverable: true);
                SetLastError(NetworkErrorKind.InvalidResponse, e.Message);
                callback?.Invoke(false);
            }
        });
    }

    // ── Прогресс ──────────────────────────────────────────────

    /// <summary>Сохранить текущий прогресс на сервер (fire & forget, не блокирует игру).</summary>
    public void SaveProgressAsync(
        string episodeId,
        string nodeGuid,
        SaveData snapshot = null,
        Dictionary<string, int> stats = null,
        Dictionary<string, bool> flags = null,
        List<string> unlockedEpisodes = null)
    {
        StartCoroutine(SaveProgressCoroutine(episodeId, nodeGuid, snapshot, stats, flags, unlockedEpisodes));
    }

    private IEnumerator SaveProgressCoroutine(
        string episodeId,
        string nodeGuid,
        SaveData snapshot,
        Dictionary<string, int> stats,
        Dictionary<string, bool> flags,
        List<string> unlockedEpisodes)
    {
        var pending = BuildPendingProgressPayload(episodeId, nodeGuid, snapshot, stats, flags, unlockedEpisodes);
        SavePendingProgress(pending);

        if (!IsAuthenticated)
            yield break;

        yield return SendPendingProgress(pending, ok =>
        {
            if (ok)
                ClearPendingProgress(GetProgressKey(pending.storyId));
        });
    }

    /// <summary>Загрузить прогресс с сервера и применить локально.</summary>
    public IEnumerator LoadProgress(Action<bool> callback = null)
    {
        if (!IsAuthenticated)
        {
            callback?.Invoke(false);
            yield break;
        }

        yield return GetInternal(ApiRoutes.PlayerProgress, (json, err) =>
        {
            if (err != null)
            {
                callback?.Invoke(false);
                return;
            }

            try
            {
                if (!NetworkJson.LooksLikeJsonObject(json))
                    throw new Exception("Progress response is not a JSON object");

                if (!ApplyLoadedProgressJson(json))
                    throw new Exception("Invalid progress response");
                OnProgressLoaded?.Invoke(json);
                callback?.Invoke(true);
            }
            catch (Exception e)
            {
                AppLogger.Error(
                    AppLogCategory.SaveSystem,
                    nameof(NetworkManager),
                    nameof(LoadProgress),
                    "Failed to parse player progress response.",
                    e,
                    LogMetadata.Of("endpoint", ApiRoutes.PlayerProgress),
                    recoverable: true);
                SetLastError(NetworkErrorKind.InvalidResponse, e.Message);
                callback?.Invoke(false);
            }
        });
    }

    // ── Имя героини ───────────────────────────────────────────

    /// <summary>Сменить имя героини на сервере + локально.</summary>
    private bool ApplyLoadedProgressJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxProgressResponseChars || !NetworkJson.LooksLikeJsonObject(json))
        {
            LastProgressRawJson = "";
            ClearLoadedProgressState();
            SetLastError(NetworkErrorKind.InvalidResponse, "Invalid progress response.");
            return false;
        }

        LastProgressRawJson = json ?? "";
        ClearLoadedProgressState();

        if (json.Trim() == "{}")
            return true;

        var response = NetworkJson.FromJson<ProgressResponse>(json) ?? new ProgressResponse();
        LastProgressUpdatedAtIso = SaveDataSanitizer.SanitizeSavedAtIso(FirstNonEmptyRawString(
            response.updatedAt,
            response.savedAt,
            NetworkJson.GetString(json, "updatedAt"),
            NetworkJson.GetString(json, "savedAt")));

        var heroName = !string.IsNullOrEmpty(response.heroName)
            ? response.heroName
            : NetworkJson.GetString(json, "heroName");
        if (string.IsNullOrEmpty(heroName))
            heroName = ResolveHeroNameFromProgress(json, response.storyId, null);
        string progressStoryId = SaveDataSanitizer.SanitizeIdentifier(FirstNonEmptyRawString(
            response.storyId,
            NetworkJson.GetString(json, "storyId")));
        heroName = TryApplyIncomingHeroName(progressStoryId, heroName, out string appliedHeroName)
            ? appliedHeroName
            : "";

        LastProgressNodeGuid = SaveDataSanitizer.SanitizeIdentifier(!string.IsNullOrEmpty(response.currentNodeGuid)
            ? response.currentNodeGuid
            : FirstNonEmptyRawString(
                NetworkJson.GetString(json, "currentNodeGuid"),
                NetworkJson.GetString(json, "nodeGuid"),
                NetworkJson.GetString(json, "nodeId")));
        LastProgressEpisodeId = SaveDataSanitizer.SanitizeIdentifier(!string.IsNullOrEmpty(response.currentEpisodeId)
            ? response.currentEpisodeId
            : FirstNonEmptyRawString(
                NetworkJson.GetString(json, "currentEpisodeId"),
                NetworkJson.GetString(json, "episodeId")));

        var snapshot = response.snapshot ?? NetworkJson.GetSaveData(json, "snapshot");
        if (snapshot == null && !string.IsNullOrEmpty(LastProgressNodeGuid))
        {
            snapshot = new SaveData
            {
                version = 1,
                storyId = FirstNonEmptyRawString(response.storyId, NetworkJson.GetString(json, "storyId")),
                episodeId = LastProgressEpisodeId,
                currentNodeGuid = LastProgressNodeGuid,
                savedAtIso = LastProgressUpdatedAtIso
            };
        }

        if (snapshot != null)
        {
            if (string.IsNullOrEmpty(snapshot.storyId))
                snapshot.storyId = FirstNonEmptyRawString(response.storyId, NetworkJson.GetString(json, "storyId"));
            if (string.IsNullOrEmpty(snapshot.episodeId))
                snapshot.episodeId = LastProgressEpisodeId;
            if (string.IsNullOrEmpty(snapshot.currentNodeGuid))
                snapshot.currentNodeGuid = LastProgressNodeGuid;
            if (string.IsNullOrEmpty(snapshot.savedAtIso))
                snapshot.savedAtIso = LastProgressUpdatedAtIso;
            string resolvedHeroName = ResolveNetworkPersistablePlayerName(heroName, progressStoryId);
            if (!string.IsNullOrEmpty(resolvedHeroName) &&
                (string.IsNullOrWhiteSpace(snapshot.playerName) ||
                 string.Equals(snapshot.playerName.Trim(), HeroCustomizationStore.DefaultPlayerName, StringComparison.OrdinalIgnoreCase)))
            {
                snapshot.playerName = resolvedHeroName;
            }

            SanitizeIncomingServerSnapshot(snapshot);
        }

        LastProgressSnapshotJson = snapshot != null ? NetworkJson.ToSaveDataJson(snapshot) : "";

        ApplyProgressDictionaries(NetworkJson.GetIntDictionary(json, "stats"), _lastProgressStats);
        ApplyProgressDictionaries(NetworkJson.GetBoolDictionary(json, "flags"), _lastProgressFlags);
        ApplyProgressDictionaries(NetworkJson.GetBoolDictionary(json, "variables"), _lastProgressFlags);
        ApplyUnlockedEpisodes(NetworkJson.GetStringList(json, "unlockedEpisodes"));

        if (response.features != null)
        {
            FullAccessEnabled = response.features.fullAccess;
            FastForwardEnabled = response.features.fastForwardEnabled;
            BookmarksEnabled = response.features.bookmarksEnabled;
            OnFeaturesUpdated?.Invoke();
        }

        AppLogger.Info(
            AppLogCategory.SaveSystem,
            nameof(NetworkManager),
            nameof(ApplyLoadedProgressJson),
            "Player progress response was applied.",
            LogMetadata.Of(
                "episodeId", LastProgressEpisodeId,
                "nodeId", LastProgressNodeGuid,
                "snapshotChars", LastProgressSnapshotJson != null ? LastProgressSnapshotJson.Length : 0));
        return true;
    }

    private void ClearLoadedProgressState()
    {
        LastProgressNodeGuid = "";
        LastProgressEpisodeId = "";
        LastProgressSnapshotJson = "";
        LastProgressUpdatedAtIso = "";
        _lastUnlockedEpisodes.Clear();
        _lastProgressStats.Clear();
        _lastProgressFlags.Clear();
    }

    private static void ApplyProgressDictionaries<TValue>(Dictionary<string, TValue> source, Dictionary<string, TValue> target)
    {
        target.Clear();
        if (source == null)
            return;

        foreach (var kv in source)
        {
            if (target.Count >= SaveDataSanitizer.MaxStatEntries)
                break;

            string key = SaveDataSanitizer.SanitizeStatKey(kv.Key);
            if (!string.IsNullOrEmpty(key))
                target[key] = kv.Value;
        }
    }

    private static void ApplyUnlockedEpisodes(List<string> episodes)
    {
        _lastUnlockedEpisodes.Clear();
        if (episodes == null)
            return;

        for (int i = 0; i < episodes.Count && _lastUnlockedEpisodes.Count < SaveDataSanitizer.MaxWardrobeEntries; i++)
        {
            string episodeId = SaveDataSanitizer.SanitizeIdentifier(episodes[i]);
            if (!string.IsNullOrEmpty(episodeId) && !_lastUnlockedEpisodes.Contains(episodeId))
                _lastUnlockedEpisodes.Add(episodeId);
        }
    }

    private static string ResolveHeroNameFromProgress(string json, string responseStoryId, SaveData snapshot)
    {
        var heroNames = NetworkJson.GetStringDictionary(json, "heroNames");
        if (heroNames == null || heroNames.Count == 0)
            return "";

        string storyId = FirstNonEmptyRawString(
            responseStoryId,
            snapshot != null ? snapshot.storyId : "",
            ResolveActiveStoryIdForNetwork());
        if (!string.IsNullOrEmpty(storyId) && heroNames.TryGetValue(storyId, out var namedHero))
            return namedHero;

        foreach (var kv in heroNames)
        {
            if (!string.IsNullOrEmpty(kv.Value))
                return kv.Value;
        }

        return "";
    }

    private bool TryApplyIncomingHeroName(string storyId, string rawHeroName, out string appliedHeroName)
    {
        appliedHeroName = "";
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        string heroName = ResolveNetworkPersistablePlayerName(rawHeroName, storyId);
        if (string.IsNullOrWhiteSpace(heroName))
            return false;

        if (TryResolveProtectedLocalHeroName(storyId, out string localHeroName) &&
            !string.Equals(localHeroName, heroName, StringComparison.OrdinalIgnoreCase))
        {
            appliedHeroName = CharacterProfileService.SaveSelectedPlayerName(
                localHeroName,
                storyId,
                nameof(TryApplyIncomingHeroName) + ".local");
            _currentProfile.heroName = appliedHeroName;
            return true;
        }

        appliedHeroName = HeroCustomizationState.NormalizePlayerName(heroName);
        appliedHeroName = CharacterProfileService.SaveSelectedPlayerName(
            appliedHeroName,
            storyId,
            nameof(TryApplyIncomingHeroName) + ".incoming");
        _currentProfile.heroName = appliedHeroName;
        return true;
    }

    private static bool TryResolveProtectedLocalHeroName(string storyId, out string heroName)
    {
        heroName = "";
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);

        if (!string.IsNullOrEmpty(storyId) &&
            HeroCustomizationStore.TryLoadPlayerNameForStory(storyId, out heroName) &&
            HeroCustomizationStore.IsCustomPlayerName(heroName))
        {
            return true;
        }

        string activeStoryId = ResolveActiveStoryIdForNetwork();
        bool canUseActiveName = !string.IsNullOrEmpty(activeStoryId) &&
                                (string.IsNullOrEmpty(storyId) ||
                                 string.Equals(storyId, activeStoryId, StringComparison.OrdinalIgnoreCase));
        if (canUseActiveName &&
            CharacterProfileService.TryResolveSavedOrActivePlayerName(storyId, "", out heroName, out _) &&
            HeroCustomizationStore.IsCustomPlayerName(heroName))
        {
            return true;
        }

        if (canUseActiveName)
        {
            heroName = SaveDataSanitizer.SanitizePlayerName(PlayerAppearance.PlayerName);
            if (HeroCustomizationStore.IsCustomPlayerName(heroName))
                return true;
        }

        HeroCustomizationState state = HeroCustomizationStore.Load();
        heroName = state != null ? SaveDataSanitizer.SanitizePlayerName(state.playerName) : "";
        return canUseActiveName && HeroCustomizationStore.IsCustomPlayerName(heroName);
    }

    private static string ResolveActiveStoryIdForNetwork()
    {
        if (StoryManager.Instance != null && !string.IsNullOrWhiteSpace(StoryManager.Instance.CurrentStoryId))
            return SaveDataSanitizer.SanitizeIdentifier(StoryManager.Instance.CurrentStoryId);

        if (GameState.Instance != null && !string.IsNullOrWhiteSpace(GameState.Instance.CurrentStoryId))
            return SaveDataSanitizer.SanitizeIdentifier(GameState.Instance.CurrentStoryId);

        return "";
    }

    public static List<CatalogSeasonResponse> ParseCatalogResponse(string json)
    {
        var result = new List<CatalogSeasonResponse>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        string trimmed = json.Trim();
        if (trimmed.StartsWith("["))
        {
            AddCatalogItems(result, trimmed);
            return result;
        }

        string rawCatalog = FirstNonEmptyRaw(
            NetworkJson.GetRawValue(trimmed, "seasons"),
            NetworkJson.GetRawValue(trimmed, "stories"),
            NetworkJson.GetRawValue(trimmed, "catalog"),
            NetworkJson.GetRawValue(trimmed, "data"));
        if (!string.IsNullOrWhiteSpace(rawCatalog))
        {
            string catalogTrimmed = rawCatalog.TrimStart();
            if (catalogTrimmed.StartsWith("["))
            {
                AddCatalogItems(result, rawCatalog);
                return result;
            }

            if (catalogTrimmed.StartsWith("{"))
            {
                var nested = ParseCatalogResponse(rawCatalog);
                if (nested.Count > 0)
                    return nested;
            }
        }

        string rawEpisodes = NetworkJson.GetRawValue(trimmed, "episodes");
        if (!string.IsNullOrWhiteSpace(rawEpisodes) && rawEpisodes.TrimStart().StartsWith("["))
        {
            AddDocumentedEpisodes(result, rawEpisodes);
            return result;
        }

        var singleSeason = NetworkJson.FromJson<CatalogSeasonResponse>(trimmed);
        if (singleSeason != null &&
            (!string.IsNullOrEmpty(singleSeason.seasonId) ||
             (singleSeason.episodes != null && singleSeason.episodes.Count > 0)))
        {
            NormalizeCatalogSeason(singleSeason);
            if (result.Count < MaxCatalogSeasons)
                result.Add(singleSeason);
        }

        return result;
    }

    private static void AddCatalogItems(List<CatalogSeasonResponse> result, string rawArray)
    {
        if (result == null || string.IsNullOrWhiteSpace(rawArray))
            return;

        foreach (var rawItem in NetworkJson.GetArrayItems(rawArray))
        {
            if (result.Count >= MaxCatalogSeasons)
                break;

            AddCatalogItem(result, rawItem);
        }
    }

    private static void AddCatalogItem(List<CatalogSeasonResponse> result, string rawItem)
    {
        if (string.IsNullOrWhiteSpace(rawItem))
            return;

        string parentStoryId = NetworkJson.GetString(rawItem, "storyId");
        string nestedSeasons = NetworkJson.GetRawValue(rawItem, "seasons");
        if (!string.IsNullOrWhiteSpace(nestedSeasons) && nestedSeasons.TrimStart().StartsWith("["))
        {
            foreach (var rawSeason in NetworkJson.GetArrayItems(nestedSeasons))
            {
                if (result.Count >= MaxCatalogSeasons)
                    break;

                var season = NetworkJson.FromJson<CatalogSeasonResponse>(rawSeason);
                if (season != null)
                {
                    if (string.IsNullOrEmpty(season.storyId))
                        season.storyId = parentStoryId ?? "";
                    NormalizeCatalogSeason(season);
                    result.Add(season);
                }
            }

            return;
        }

        var directSeason = NetworkJson.FromJson<CatalogSeasonResponse>(rawItem);
        if (directSeason != null &&
            (!string.IsNullOrEmpty(directSeason.seasonId) ||
             (directSeason.episodes != null && directSeason.episodes.Count > 0)))
        {
            NormalizeCatalogSeason(directSeason);
            if (result.Count < MaxCatalogSeasons)
                result.Add(directSeason);
        }
    }

    private static void AddDocumentedEpisodes(List<CatalogSeasonResponse> result, string rawArray)
    {
        var bySeason = new Dictionary<string, CatalogSeasonResponse>();

        foreach (var rawItem in NetworkJson.GetArrayItems(rawArray))
        {
            if (string.IsNullOrWhiteSpace(rawItem))
                continue;

            var dto = NetworkJson.FromJson<DocumentedCatalogEpisodeResponse>(rawItem) ?? new DocumentedCatalogEpisodeResponse();
            string episodeId = FirstNonEmptyRawString(dto.episodeId, dto.id, NetworkJson.GetString(rawItem, "episodeId"), NetworkJson.GetString(rawItem, "id"));
            if (string.IsNullOrEmpty(episodeId))
                continue;

            int seasonNumber = dto.season > 0 ? dto.season : NetworkJson.GetInt(rawItem, "season", 1);
            int episodeNumber = dto.episode > 0 ? dto.episode : NetworkJson.GetInt(rawItem, "episode", dto.order);
            string seasonId = FirstNonEmptyRawString(dto.seasonId, NetworkJson.GetString(rawItem, "seasonId"), "season_" + Mathf.Max(1, seasonNumber));

            if (!bySeason.TryGetValue(seasonId, out var season))
            {
                if (result.Count >= MaxCatalogSeasons)
                    break;

                season = new CatalogSeasonResponse
                {
                    seasonId = seasonId,
                    storyId = dto.storyId ?? "",
                    title = "Season " + Mathf.Max(1, seasonNumber),
                    order = Mathf.Max(1, seasonNumber),
                    episodes = new List<CatalogEpisodeResponse>()
                };
                bySeason[seasonId] = season;
                result.Add(season);
            }

            if (CountCatalogEpisodes(result) >= MaxCatalogEpisodes)
                break;

            var episode = new CatalogEpisodeResponse
            {
                episodeId = episodeId,
                seasonId = seasonId,
                storyId = FirstNonEmptyRawString(dto.storyId, season.storyId),
                order = episodeNumber > 0 ? episodeNumber : dto.order,
                title = dto.title ?? "",
                isPremium = dto.isPremium,
                candleCost = ClampCatalogCandleCost(dto.candleCost),
                isUnlocked = dto.isUnlocked || !dto.isPremium,
                isGeoRestricted = dto.isGeoRestricted,
                contentVersion = FirstNonEmptyRawString(dto.contentVersion, dto.version, NetworkJson.GetString(rawItem, "contentVersion"), NetworkJson.GetString(rawItem, "version")),
                hasRemoteContent = true
            };

            season.episodes.Add(episode);
        }

        foreach (var season in result)
            NormalizeCatalogSeason(season);
    }

    private static void NormalizeCatalogSeason(CatalogSeasonResponse season)
    {
        if (season == null)
            return;

        season.seasonId = SaveDataSanitizer.SanitizeIdentifier(season.seasonId);
        season.storyId = SaveDataSanitizer.SanitizeIdentifier(season.storyId);
        season.title = SaveDataSanitizer.SanitizeHistoryLine(season.title);
        season.order = Mathf.Clamp(season.order, 0, MaxCatalogOrder);

        if (season.episodes == null)
            season.episodes = new List<CatalogEpisodeResponse>();

        var safeEpisodes = new List<CatalogEpisodeResponse>();
        foreach (var episode in season.episodes)
        {
            if (episode == null)
                continue;

            NormalizeCatalogEpisode(episode, season);
            if (!string.IsNullOrEmpty(episode.episodeId))
            {
                safeEpisodes.Add(episode);
                if (safeEpisodes.Count >= MaxCatalogEpisodes)
                    break;
            }
        }

        season.episodes = safeEpisodes;
    }

    private static void NormalizeCatalogEpisode(CatalogEpisodeResponse episode, CatalogSeasonResponse season)
    {
        if (episode == null)
            return;

        episode.episodeId = SaveDataSanitizer.SanitizeIdentifier(episode.episodeId);
        episode.storyId = SaveDataSanitizer.SanitizeIdentifier(string.IsNullOrEmpty(episode.storyId) ? season.storyId : episode.storyId);
        episode.seasonId = SaveDataSanitizer.SanitizeIdentifier(string.IsNullOrEmpty(episode.seasonId) ? season.seasonId : episode.seasonId);
        episode.title = SaveDataSanitizer.SanitizeHistoryLine(episode.title);
        episode.order = Mathf.Clamp(episode.order, 0, MaxCatalogOrder);
        episode.contentVersion = SaveDataSanitizer.SanitizeIdentifier(episode.contentVersion);
        episode.candleCost = ClampCatalogCandleCost(episode.candleCost);

        if (!episode.isPremium)
        {
            episode.candleCost = 0;
            episode.isUnlocked = true;
        }
    }

    private static int ClampCatalogCandleCost(int value)
    {
        return SaveDataSanitizer.ClampCurrencyValue(value);
    }

    private static int CountCatalogEpisodes(List<CatalogSeasonResponse> seasons)
    {
        if (seasons == null)
            return 0;

        int count = 0;
        foreach (var season in seasons)
        {
            if (season != null && season.episodes != null)
                count += season.episodes.Count;
        }

        return count;
    }

    private void ApplyCatalogResponse(string json)
    {
        _catalogSeasons.Clear();
        _catalogEpisodes.Clear();

        foreach (var season in ParseCatalogResponse(json))
        {
            if (season == null)
                continue;

            if (_catalogSeasons.Count >= MaxCatalogSeasons)
                break;

            NormalizeCatalogSeason(season);
            int remainingEpisodes = MaxCatalogEpisodes - _catalogEpisodes.Count;
            if (remainingEpisodes <= 0)
                break;
            if (season.episodes.Count > remainingEpisodes)
                season.episodes.RemoveRange(remainingEpisodes, season.episodes.Count - remainingEpisodes);

            _catalogSeasons.Add(season);

            foreach (var episode in season.episodes)
            {
                if (_catalogEpisodes.Count >= MaxCatalogEpisodes)
                    break;

                if (episode == null || string.IsNullOrEmpty(episode.episodeId))
                    continue;

                _catalogEpisodes[episode.episodeId] = episode;
            }
        }
    }

    private static string FirstNonEmptyRaw(params string[] values)
    {
        if (values == null)
            return null;

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value) && value != "null")
                return value;
        }

        return null;
    }

    public void SetHeroNameAsync(string name, string nodeGuid = null, string episodeId = null, string storyId = null)
    {
        string safeName = HeroCustomizationState.NormalizePlayerName(name);
        string resolvedStoryId = !string.IsNullOrEmpty(storyId)
            ? storyId
            : ResolveActiveStoryIdForNetwork();
        resolvedStoryId = SaveDataSanitizer.SanitizeIdentifier(resolvedStoryId);
        safeName = CharacterProfileService.SaveSelectedPlayerName(
            safeName,
            resolvedStoryId,
            nameof(SetHeroNameAsync));
        _currentProfile.heroName = safeName;

        if (!IsAuthenticated) return;
        StartCoroutine(SetHeroNameCoroutine(safeName, nodeGuid, episodeId, resolvedStoryId));
    }

    private IEnumerator SetHeroNameCoroutine(string name, string nodeGuid, string episodeId, string storyId)
    {
        string resolvedStoryId = !string.IsNullOrEmpty(storyId)
            ? storyId
            : ResolveActiveStoryIdForNetwork();
        resolvedStoryId = SaveDataSanitizer.SanitizeIdentifier(resolvedStoryId);
        name = HeroCustomizationState.NormalizePlayerName(name);

        var body = new HeroNameRequest
        {
            name = name,
            storyId = resolvedStoryId ?? ""
        };

        yield return Post(ApiRoutes.PlayerHeroName, body, _authToken, (json, err) =>
        {
            if (err != null)
            {
                AppLogger.Warn(
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(SetHeroNameCoroutine),
                    "Failed to save hero name on server.",
                    LogMetadata.Of("endpoint", ApiRoutes.PlayerHeroName, "storyId", resolvedStoryId, "error", err),
                    recoverable: true);
                return;
            }

            TryApplyIncomingHeroName(resolvedStoryId, NetworkJson.GetString(json, "heroName"), out _);
        });
    }

    // ── Закладки ──────────────────────────────────────────────

    /// <summary>Сохранить закладку на сервере (если подписка активна).</summary>
    public void SaveBookmarkAsync(SaveData snapshot, string label = null)
    {
        if (snapshot == null) return;

        StartCoroutine(SaveBookmarkCoroutine(
            snapshot.currentNodeGuid,
            snapshot.episodeId,
            snapshot.storyId,
            snapshot,
            label));
    }

    public void SaveBookmarkAsync(string nodeGuid, string episodeId, string label = null)
    {
        StartCoroutine(SaveBookmarkCoroutine(nodeGuid, episodeId, "", null, label));
    }

    private IEnumerator SaveBookmarkCoroutine(string nodeGuid, string episodeId, string storyId, SaveData snapshot, string label)
    {
        var pending = BuildPendingBookmarkPayload(nodeGuid, episodeId, storyId, snapshot, label);
        SavePendingBookmark(pending);

        if (!IsAuthenticated || _serverBookmarkLocked)
            yield break;

        yield return SendPendingBookmark(pending, result =>
        {
            if (result != null && result.IsSuccess)
            {
                ClearPendingBookmark(GetProgressKey(pending.storyId));
                return;
            }

            if (result != null && result.Kind == NetworkErrorKind.PaymentRequired)
            {
                AppLogger.Info(
                    AppLogCategory.SaveSystem,
                    nameof(NetworkManager),
                    nameof(SaveBookmarkCoroutine),
                    "Bookmark was kept locally because the server reported subscription is required.",
                    LogMetadata.Of(
                        "endpoint", ApiRoutes.PlayerBookmarkSave,
                        "storyId", pending.storyId,
                        "episodeId", pending.episodeId,
                        "nodeId", pending.nodeGuid));
                _serverBookmarkLocked = true;
                ClearPendingBookmark(GetProgressKey(pending.storyId));
                return;
            }
            else
            {
                AppLogger.Warn(
                    AppLogCategory.SaveSystem,
                    nameof(NetworkManager),
                    nameof(SaveBookmarkCoroutine),
                    "Bookmark save failed.",
                    LogMetadata.Of(
                        "requestId", result != null ? result.RequestId : "",
                        "endpoint", ApiRoutes.PlayerBookmarkSave,
                        "storyId", pending.storyId,
                        "episodeId", pending.episodeId,
                        "nodeId", pending.nodeGuid,
                        "statusCode", result != null ? result.ResponseCode : 0,
                        "kind", result != null ? result.Kind : NetworkErrorKind.ClientError,
                        "error", result != null ? result.Error : ""),
                    recoverable: true);
            }
        });
    }

    /// <summary>Загрузить закладку с сервера.</summary>
    public IEnumerator LoadBookmark(Action<string, string> callback)
    {
        if (!IsAuthenticated)
        {
            callback?.Invoke(null, null);
            yield break;
        }

        yield return GetInternal(ApiRoutes.PlayerBookmark, (json, err) =>
        {
            if (err != null)
            {
                callback?.Invoke(null, null);
                return;
            }

            try
            {
                if (!NetworkJson.LooksLikeJsonObject(json))
                    throw new Exception("Bookmark response is not a JSON object");

                var bookmark = ParseBookmarkResponse(json);
                callback?.Invoke(bookmark?.nodeGuid, bookmark?.episodeId);
            }
            catch (Exception e)
            {
                SetLastError(NetworkErrorKind.InvalidResponse, e.Message);
                callback?.Invoke(null, null);
            }
        });
    }

    // ── Свечи ─────────────────────────────────────────────────

    /// <summary>Списать свечу за открытие главы.</summary>
    public IEnumerator LoadBookmarkSnapshot(Action<SaveData> callback)
    {
        if (!IsAuthenticated)
        {
            callback?.Invoke(null);
            yield break;
        }

        yield return GetInternal(ApiRoutes.PlayerBookmark, (json, err) =>
        {
            if (err != null)
            {
                callback?.Invoke(null);
                return;
            }

            try
            {
                if (!NetworkJson.LooksLikeJsonObject(json))
                    throw new Exception("Bookmark response is not a JSON object");

                var bookmark = ParseBookmarkResponse(json);
                var snapshot = bookmark != null
                    ? bookmark.snapshot ?? NetworkJson.GetSaveData(json, "snapshot")
                    : null;

                if (bookmark == null ||
                    (string.IsNullOrEmpty(bookmark.nodeGuid) &&
                     (snapshot == null || string.IsNullOrEmpty(snapshot.currentNodeGuid))))
                {
                    callback?.Invoke(null);
                    return;
                }

                if (snapshot != null)
                {
                    if (string.IsNullOrEmpty(snapshot.storyId))
                        snapshot.storyId = bookmark.storyId ?? "";
                    if (string.IsNullOrEmpty(snapshot.episodeId))
                        snapshot.episodeId = bookmark.episodeId ?? "";
                    if (string.IsNullOrEmpty(snapshot.currentNodeGuid))
                        snapshot.currentNodeGuid = bookmark.nodeGuid ?? "";
                    if (string.IsNullOrEmpty(snapshot.savedAtIso))
                        snapshot.savedAtIso = bookmark.savedAt ?? "";

                    SanitizeIncomingServerSnapshot(snapshot);
                    callback?.Invoke(snapshot);
                    return;
                }

                var fallbackSnapshot = new SaveData
                {
                    version = 1,
                    storyId = bookmark.storyId ?? "",
                    episodeId = bookmark.episodeId ?? "",
                    currentNodeGuid = bookmark.nodeGuid ?? "",
                    savedAtIso = bookmark.savedAt ?? ""
                };
                SanitizeIncomingServerSnapshot(fallbackSnapshot);
                callback?.Invoke(fallbackSnapshot);
            }
            catch (Exception e)
            {
                AppLogger.Error(
                    AppLogCategory.SaveSystem,
                    nameof(NetworkManager),
                    nameof(LoadBookmarkSnapshot),
                    "Failed to parse bookmark snapshot response.",
                    e,
                    LogMetadata.Of("endpoint", ApiRoutes.PlayerBookmark),
                    recoverable: true);
                SetLastError(NetworkErrorKind.InvalidResponse, e.Message);
                callback?.Invoke(null);
            }
        });
    }

    private BookmarkInfo ParseBookmarkResponse(string json)
    {
        var response = NetworkJson.FromJson<BookmarkEnvelope>(json) ?? new BookmarkEnvelope();
        if (HasBookmarkData(response.bookmark))
            return response.bookmark;

        var listedBookmark = response.GetFirstBookmark();
        if (HasBookmarkData(listedBookmark))
            return listedBookmark;

        var rawBookmark = NetworkJson.GetRawValue(json, "bookmark");
        if (!string.IsNullOrEmpty(rawBookmark) && rawBookmark != "null")
            return ParseBookmarkInfo(rawBookmark);

        var rawBookmarks = FirstNonEmptyRaw(
            NetworkJson.GetRawValue(json, "bookmarks"),
            NetworkJson.GetRawValue(json, "items"));
        var firstArrayBookmark = ParseFirstBookmark(rawBookmarks);
        if (firstArrayBookmark != null)
            return firstArrayBookmark;

        var direct = ParseBookmarkInfo(json);
        if (HasBookmarkData(direct))
            return direct;

        return null;
    }

    private BookmarkInfo ParseFirstBookmark(string rawArray)
    {
        if (string.IsNullOrWhiteSpace(rawArray) || !rawArray.TrimStart().StartsWith("["))
            return null;

        foreach (var rawItem in NetworkJson.GetArrayItems(rawArray))
        {
            var item = ParseBookmarkInfo(rawItem);
            if (HasBookmarkData(item))
                return item;
        }

        return null;
    }

    private static BookmarkInfo ParseBookmarkInfo(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || !NetworkJson.LooksLikeJsonObject(json))
            return null;

        var item = NetworkJson.FromJson<BookmarkInfo>(json) ?? new BookmarkInfo();
        if (string.IsNullOrEmpty(item.nodeGuid))
            item.nodeGuid = NetworkJson.GetString(json, "nodeGuid") ?? "";
        if (string.IsNullOrEmpty(item.episodeId))
            item.episodeId = NetworkJson.GetString(json, "episodeId") ?? "";
        if (string.IsNullOrEmpty(item.storyId))
            item.storyId = NetworkJson.GetString(json, "storyId") ?? "";
        if (string.IsNullOrEmpty(item.savedAt))
            item.savedAt = NetworkJson.GetString(json, "savedAt") ?? "";
        if (string.IsNullOrEmpty(item.label))
            item.label = NetworkJson.GetString(json, "label") ?? "";
        if (item.snapshot == null)
            item.snapshot = NetworkJson.GetSaveData(json, "snapshot");

        item.nodeGuid = SaveDataSanitizer.SanitizeIdentifier(item.nodeGuid);
        item.episodeId = SaveDataSanitizer.SanitizeIdentifier(item.episodeId);
        item.storyId = SaveDataSanitizer.SanitizeIdentifier(item.storyId);
        item.savedAt = SaveDataSanitizer.SanitizeSavedAtIso(item.savedAt);
        item.label = SaveDataSanitizer.SanitizeHistoryLine(item.label);
        item.snapshot = SaveDataSanitizer.Sanitize(item.snapshot);

        return item;
    }

    private static bool HasBookmarkData(BookmarkInfo bookmark)
    {
        return bookmark != null &&
               (!string.IsNullOrEmpty(bookmark.nodeGuid) ||
                !string.IsNullOrEmpty(bookmark.episodeId) ||
                HasSnapshotData(bookmark.snapshot));
    }

    private static bool HasSnapshotData(SaveData snapshot)
    {
        if (snapshot == null)
            return false;

        return snapshot.HasPosition ||
               !string.IsNullOrEmpty(snapshot.storyId) ||
               !string.IsNullOrEmpty(snapshot.seasonId) ||
               !string.IsNullOrEmpty(snapshot.chapterId) ||
               !string.IsNullOrEmpty(snapshot.episodeId) ||
               !string.IsNullOrEmpty(snapshot.graphName) ||
               !string.IsNullOrEmpty(snapshot.currentNodeGuid) ||
               !string.IsNullOrEmpty(snapshot.savedAtIso) ||
               !string.IsNullOrEmpty(snapshot.playerName) ||
               HasItems(snapshot.history) ||
               HasItems(snapshot.wardrobe) ||
               HasItems(snapshot.equippedClothes) ||
               HasItems(snapshot.statKeys) ||
               HasItems(snapshot.statValues);
    }

    private static bool HasItems<T>(List<T> items)
    {
        return items != null && items.Count > 0;
    }

    public IEnumerator SpendCandle(Action<bool> callback)
    {
        yield return SpendCandles(1, callback);
    }

    public IEnumerator SpendCandles(int amount, Action<bool> callback)
    {
        if (amount <= 0)
        {
            AppLogger.Warn(
                AppLogCategory.Network,
                nameof(NetworkManager),
                nameof(SpendCandles),
                "Refusing non-positive candle spend request.",
                LogMetadata.Of("amount", amount),
                recoverable: true);
            callback?.Invoke(false);
            yield break;
        }

        if (amount > MaxCandlesSpendBatch)
        {
            AppLogger.Warn(
                AppLogCategory.Network,
                nameof(NetworkManager),
                nameof(SpendCandles),
                "Refusing unusually large candle spend request.",
                LogMetadata.Of("amount", amount, "maxAmount", MaxCandlesSpendBatch),
                recoverable: true);
            callback?.Invoke(false);
            yield break;
        }

        if (!IsAuthenticated)
        {
            if (!PrototypeFeatureFlags.LocalPremiumSpendEnabled)
            {
                AppLogger.Warn(
                    AppLogCategory.Security,
                    nameof(NetworkManager),
                    nameof(SpendCandles),
                    "Local premium spend fallback is disabled.",
                    recoverable: true);
                callback?.Invoke(false);
                yield break;
            }

            bool ok = PlayerData.Candles >= amount;
            if (ok) PlayerData.AddCandlesValue(-amount);
            callback?.Invoke(ok);
            yield break;
        }

        var body = new CandleSpendRequest
        {
            amount = amount
        };

        yield return Post(ApiRoutes.PlayerCandlesSpend, body, _authToken, (json, err) =>
        {
            if (err != null)
            {
                AppLogger.Warn(
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(SpendCandles),
                    "Candle spend request failed.",
                    LogMetadata.Of("endpoint", ApiRoutes.PlayerCandlesSpend, "amount", amount, "error", err),
                    recoverable: true);
                callback?.Invoke(false);
                return;
            }

            if (ResponseHasApiError(json))
            {
                AppLogger.Warn(
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(SpendCandles),
                    "Candle spend request was rejected by API response.",
                    LogMetadata.Of(
                        "endpoint", ApiRoutes.PlayerCandlesSpend,
                        "amount", amount,
                        "apiError", NetworkJson.GetString(json, "error")),
                    recoverable: true);
                callback?.Invoke(false);
                return;
            }

            try
            {
                var r = NetworkJson.FromJson<CandlesResponse>(json);
                if (r == null)
                    throw new Exception("Empty candles response");

                int candles = SaveDataSanitizer.ClampCurrencyValue(r.candles);
                _lastBalance.candles = candles;
                _lastBalance.nextCandleAt = r.nextCandleAt ?? _lastBalance.nextCandleAt;
                _lastBalance.updatedAtIso = DateTime.UtcNow.ToString("o");
                PlayerData.SetCandlesValue(candles);
                ApplyBalancePatchFromJson(json);
                callback?.Invoke(true);
            }
            catch (Exception e)
            {
                AppLogger.Error(
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(SpendCandles),
                    "Failed to parse candle spend response.",
                    e,
                    LogMetadata.Of("endpoint", ApiRoutes.PlayerCandlesSpend, "amount", amount),
                    recoverable: true);
                callback?.Invoke(false);
            }
        });
    }

    public IEnumerator SyncCatalog(Action<bool> callback = null)
    {
        if (!IsAuthenticated)
        {
            callback?.Invoke(false);
            yield break;
        }

        yield return GetInternal(ApiRoutes.ContentCatalog, (json, err) =>
        {
            if (err != null)
            {
                callback?.Invoke(false);
                return;
            }

            try
            {
                ApplyCatalogResponse(json);
                callback?.Invoke(true);
            }
            catch (Exception e)
            {
                AppLogger.Error(
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(SyncCatalog),
                    "Failed to parse catalog response.",
                    e,
                    LogMetadata.Of("endpoint", ApiRoutes.ContentCatalog),
                    recoverable: true);
                callback?.Invoke(false);
            }
        });
    }

    public static bool TryGetCatalogEpisode(string episodeId, out CatalogEpisodeResponse episode)
    {
        if (string.IsNullOrEmpty(episodeId))
        {
            episode = null;
            return false;
        }

        return _catalogEpisodes.TryGetValue(episodeId, out episode);
    }

    public static string GetCatalogEpisodeTitle(string episodeId, string fallback = "")
    {
        return TryGetCatalogEpisode(episodeId, out var episode) && !string.IsNullOrEmpty(episode.title)
            ? episode.title
            : fallback;
    }

    public static bool IsCatalogEpisodePremium(string episodeId, bool fallback = false)
    {
        return TryGetCatalogEpisode(episodeId, out var episode) ? episode.isPremium : fallback;
    }

    public static int GetCatalogEpisodeCandleCost(string episodeId, int fallback = 0)
    {
        return ClampCatalogCandleCost(TryGetCatalogEpisode(episodeId, out var episode) ? episode.candleCost : fallback);
    }

    public static bool IsCatalogEpisodeUnlocked(string episodeId, bool fallback = false)
    {
        return TryGetCatalogEpisode(episodeId, out var episode) ? episode.isUnlocked : fallback;
    }

    public static bool HasCatalogRemoteContent(string episodeId)
    {
        return TryGetCatalogEpisode(episodeId, out var episode) && episode.hasRemoteContent;
    }

    public static string GetCatalogContentVersion(string episodeId, string fallback = "0")
    {
        return TryGetCatalogEpisode(episodeId, out var episode) && !string.IsNullOrEmpty(episode.contentVersion)
            ? episode.contentVersion
            : fallback;
    }

    // ── Каталог ───────────────────────────────────────────────

    /// <summary>Получить каталог глав с сервера.</summary>
    public IEnumerator FetchCatalog(Action<string> callback)
    {
        if (!IsAuthenticated)
        {
            callback?.Invoke(null);
            yield break;
        }

        yield return GetInternal(ApiRoutes.ContentCatalog, (json, err) =>
        {
            if (err != null)
            {
                callback?.Invoke(null);
                return;
            }

            try
            {
                ApplyCatalogResponse(json);
            }
            catch (Exception e)
            {
                AppLogger.Error(
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(FetchCatalog),
                    "Failed to parse catalog response.",
                    e,
                    LogMetadata.Of("endpoint", ApiRoutes.ContentCatalog),
                    recoverable: true);
            }

            callback?.Invoke(json);
        });
    }

    /// <summary>Скачать граф главы с сервера если версия новее локальной.</summary>
    public IEnumerator FetchEpisodeGraph(string episodeId, string localVersion, Action<string> callback)
    {
        yield return FetchEpisodeGraphResponse(episodeId, localVersion, response =>
        {
            callback?.Invoke(response != null ? response.graphJson : null);
        });
    }

    public IEnumerator FetchEpisodeGraphResponse(string episodeId, string localVersion, Action<EpisodeGraphResponse> callback)
    {
        if (!IsAuthenticated)
        {
            callback?.Invoke(null);
            yield break;
        }

        var safeEpisodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        if (string.IsNullOrEmpty(safeEpisodeId))
        {
            callback?.Invoke(null);
            yield break;
        }

        string path = ApiRoutes.ContentEpisodeGraph(safeEpisodeId);
        yield return SendAuthorizedRequest(
            () => _httpClient.CreateGetRequest(path, _authToken),
            result =>
            {
                if (result.ResponseCode == 304)
                {
                    callback?.Invoke(new EpisodeGraphResponse
                    {
                        episodeId = episodeId ?? "",
                        contentVersion = string.IsNullOrEmpty(localVersion) ? "0" : localVersion,
                        notModified = true
                    });
                    return;
                }

                if (!result.IsSuccess)
                {
                    AppLogger.Warn(
                        AppLogCategory.Network,
                        nameof(NetworkManager),
                        nameof(FetchEpisodeGraphResponse),
                        "Episode graph request failed.",
                        LogMetadata.Of(
                            "requestId", result.RequestId,
                            "endpoint", path,
                            "episodeId", safeEpisodeId,
                            "statusCode", result.ResponseCode,
                            "kind", result.Kind,
                            "error", result.Error),
                        recoverable: true);
                    callback?.Invoke(null);
                    return;
                }

                EpisodeGraphResponse response = ParseEpisodeGraphResponse(result.Text, episodeId, localVersion);
                if (!IsExpectedEpisodeGraphResponse(response, episodeId))
                {
                    AppLogger.Warn(
                        AppLogCategory.Network,
                        nameof(NetworkManager),
                        nameof(FetchEpisodeGraphResponse),
                        "Episode graph response was rejected because episodeId did not match.",
                        LogMetadata.Of("endpoint", path, "requestedEpisodeId", safeEpisodeId, "responseEpisodeId", response != null ? response.episodeId : ""),
                        recoverable: true);
                    callback?.Invoke(null);
                    return;
                }

                if (!string.IsNullOrEmpty(response.graphJson) && response.graphJson.Length > MaxRemoteGraphJsonChars)
                {
                    AppLogger.Warn(
                        AppLogCategory.Network,
                        nameof(NetworkManager),
                        nameof(FetchEpisodeGraphResponse),
                        "Episode graph response was rejected because graph payload is too large.",
                        LogMetadata.Of(
                            "endpoint", path,
                            "episodeId", safeEpisodeId,
                            "payloadChars", response.graphJson.Length,
                            "maxPayloadChars", MaxRemoteGraphJsonChars),
                        recoverable: true);
                    callback?.Invoke(null);
                    return;
                }

                callback?.Invoke(response);
            },
            allowRetry: true);
    }

    public static EpisodeGraphResponse ParseEpisodeGraphResponse(string payload, string fallbackEpisodeId, string fallbackVersion = "0")
    {
        var response = new EpisodeGraphResponse
        {
            rawPayloadJson = payload ?? "",
            episodeId = SaveDataSanitizer.SanitizeIdentifier(fallbackEpisodeId),
            contentVersion = string.IsNullOrWhiteSpace(fallbackVersion) ? "0" : SaveDataSanitizer.SanitizeIdentifier(fallbackVersion)
        };

        if (string.IsNullOrWhiteSpace(payload))
            return response;

        string rawData = NetworkJson.GetRawValue(payload, "data");
        var episodeId = FirstNonEmptyRawString(
            NetworkJson.GetString(payload, "episodeId"),
            NetworkJson.GetString(rawData, "episodeId"));
        if (!string.IsNullOrWhiteSpace(episodeId))
            response.episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);

        var version = FirstNonEmptyRawString(
            NetworkJson.GetString(payload, "contentVersion"),
            NetworkJson.GetString(rawData, "contentVersion"));
        if (string.IsNullOrWhiteSpace(version))
            version = FirstNonEmptyRawString(
                NetworkJson.GetString(payload, "version"),
                NetworkJson.GetString(rawData, "version"));
        if (!string.IsNullOrWhiteSpace(version))
            response.contentVersion = SaveDataSanitizer.SanitizeIdentifier(version);

        var graphJson = FirstNonEmptyRawString(
            NetworkJson.GetString(payload, "graphJson"),
            NetworkJson.GetString(rawData, "graphJson"));
        if (string.IsNullOrWhiteSpace(graphJson))
            graphJson = FirstNonEmptyRawString(
                NetworkJson.GetRawValue(payload, "graph"),
                NetworkJson.GetRawValue(rawData, "graph"));
        if (string.IsNullOrWhiteSpace(graphJson) && payload.TrimStart().StartsWith("{"))
            graphJson = payload;

        response.graphJson = graphJson ?? "";
        return response;
    }

    private static bool IsExpectedEpisodeGraphResponse(EpisodeGraphResponse response, string requestedEpisodeId)
    {
        if (response == null)
            return false;

        if (string.IsNullOrWhiteSpace(requestedEpisodeId) || string.IsNullOrWhiteSpace(response.episodeId))
            return true;

        return string.Equals(
            SaveDataSanitizer.SanitizeIdentifier(response.episodeId),
            SaveDataSanitizer.SanitizeIdentifier(requestedEpisodeId),
            StringComparison.OrdinalIgnoreCase);
    }

    // ── Internal HTTP — доступны другим менеджерам ────────────

    public IEnumerator Get(string path, Action<string, string> callback)
    {
        if (!IsAllowedPublicNetworkPath(path, "GET"))
        {
            callback?.Invoke(null, "Blocked public network path.");
            yield break;
        }

        yield return GetInternal(path, callback);
    }

    public IEnumerator PostRaw(string path, string jsonBody, Action<string, string> callback)
    {
        if (!IsAllowedPublicNetworkPath(path, "POST"))
        {
            callback?.Invoke(null, "Blocked public network path.");
            yield break;
        }

        yield return PostRawInternal(path, jsonBody, _authToken, callback);
    }

    public IEnumerator Delete(string path, Action<string, string> callback)
    {
        if (!IsAllowedPublicNetworkPath(path, "DELETE"))
        {
            callback?.Invoke(null, "Blocked public network path.");
            yield break;
        }

        yield return SendAuthorizedRequest(
            () => _httpClient.CreateDeleteRequest(path, _authToken),
            result => callback?.Invoke(result.IsSuccess ? result.Text : null, result.IsSuccess ? null : result.Error),
            allowRetry: false);
    }

    private static bool IsAllowedPublicNetworkPath(string path, string method)
    {
        string normalized = NormalizeApiPath(path);
        if (ContainsUnsafeApiPathSegments(normalized))
            return false;

        return IsAllowedRuntimeApiPath(normalized, method);
    }

    private static string NormalizeApiPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        string normalized = path.Trim().Replace('\\', '/');
        int queryIndex = normalized.IndexOf('?');
        if (queryIndex >= 0)
            normalized = normalized.Substring(0, queryIndex);

        return normalized.StartsWith("/", StringComparison.Ordinal)
            ? normalized
            : "/" + normalized;
    }

    // ── Private HTTP ───────────────────────────────────────────

    private static bool ContainsUnsafeApiPathSegments(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (path.IndexOf("%2e", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.IndexOf("%2f", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.IndexOf("%5c", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        string decodedPath;
        try
        {
            decodedPath = Uri.UnescapeDataString(path).Replace('\\', '/');
        }
        catch (Exception exception)
        {
            ThrottledAppLogger.Warn(
                nameof(NetworkManager) + "." + nameof(ContainsUnsafeApiPathSegments) + ".Decode",
                AppLogCategory.Network,
                nameof(NetworkManager),
                nameof(ContainsUnsafeApiPathSegments),
                "Request path could not be URL-decoded and was blocked.",
                LogMetadata.Of(
                    "pathChars", path != null ? path.Length : 0,
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

    private void EnsureHttpClient()
    {
        if (_httpClient != null)
            return;

        _httpClient = new NetworkHttpClient(
            () => _resolvedBaseUrl,
            () => _runtimeConfig,
            SetLastError,
            UpdateConnectivityState);
    }

    private string BuildUrl(string path)
    {
        EnsureHttpClient();
        return _httpClient.BuildUrl(path);
    }

    private IEnumerator PostRawInternal(string path, string jsonBody, string token, Action<string, string> callback, bool allowRetry = false)
    {
        bool authorized = !string.IsNullOrEmpty(token);
        if (authorized)
        {
            yield return SendAuthorizedRequest(
                () => _httpClient.CreateJsonPostRequest(path, jsonBody, _authToken),
                result => callback?.Invoke(result.IsSuccess ? result.Text : null, result.IsSuccess ? null : result.Error),
                allowRetry);
        }
        else
        {
            yield return SendRequest(
                () => _httpClient.CreateJsonPostRequest(path, jsonBody, token),
                result => callback?.Invoke(result.IsSuccess ? result.Text : null, result.IsSuccess ? null : result.Error),
                allowRetry);
        }
    }

    private IEnumerator PostRawInternalResult(string path, string jsonBody, string token, Action<NetworkRequestResult> callback, bool allowRetry = false)
    {
        bool authorized = !string.IsNullOrEmpty(token);
        if (authorized)
        {
            yield return SendAuthorizedRequest(
                () => _httpClient.CreateJsonPostRequest(path, jsonBody, _authToken),
                callback,
                allowRetry);
        }
        else
        {
            yield return SendRequest(
                () => _httpClient.CreateJsonPostRequest(path, jsonBody, token),
                callback,
                allowRetry);
        }
    }

    private IEnumerator GetInternal(string path, Action<string, string> callback, bool allowRetry = true)
    {
        yield return SendAuthorizedRequest(
            () => _httpClient.CreateGetRequest(path, _authToken),
            result => callback?.Invoke(result.IsSuccess ? result.Text : null, result.IsSuccess ? null : result.Error),
            allowRetry);
    }

    private IEnumerator Post(string path, object body, string token, Action<string, string> callback, bool allowRetry = false)
    {
        var json = NetworkJson.ToJson(body);
        bool authorized = !string.IsNullOrEmpty(token);
        if (authorized)
        {
            yield return SendAuthorizedRequest(
                () => _httpClient.CreateJsonPostRequest(path, json, _authToken),
                result => callback?.Invoke(result.IsSuccess ? result.Text : null, result.IsSuccess ? null : result.Error),
                allowRetry);
        }
        else
        {
            yield return SendRequest(
                () => _httpClient.CreateJsonPostRequest(path, json, token),
                result => callback?.Invoke(result.IsSuccess ? result.Text : null, result.IsSuccess ? null : result.Error),
                allowRetry);
        }
    }

    private IEnumerator SendAuthorizedRequest(Func<UnityWebRequest> requestFactory, Action<NetworkRequestResult> callback, bool allowRetry)
    {
        NetworkRequestResult firstResult = null;
        yield return SendRequest(requestFactory, result => firstResult = result, allowRetry);

        if (firstResult == null || firstResult.Kind != NetworkErrorKind.Unauthorized || string.IsNullOrEmpty(_authToken))
        {
            callback?.Invoke(firstResult);
            yield break;
        }

        AppLogger.Warn(
            AppLogCategory.Auth,
            nameof(NetworkManager),
            nameof(SendAuthorizedRequest),
            "Authorized request returned unauthorized; attempting session refresh.",
            LogMetadata.Of(
                "requestId", firstResult.RequestId,
                "method", firstResult.Method,
                "path", firstResult.Path,
                "statusCode", firstResult.ResponseCode,
                "lastError", firstResult.Error,
                "hasRefreshCredential", !string.IsNullOrEmpty(_refreshToken)),
            recoverable: true);

        if (string.IsNullOrEmpty(_refreshToken))
        {
            callback?.Invoke(firstResult);
            yield break;
        }

        bool restored = false;
        yield return RestoreSession(GetOrCreateDeviceId(), _refreshToken, ok => restored = ok, startSync: false);
        if (!restored)
        {
            AppLogger.Warn(
                AppLogCategory.Auth,
                nameof(NetworkManager),
                nameof(SendAuthorizedRequest),
                "Session refresh failed after unauthorized response.",
                LogMetadata.Of(
                    "requestId", firstResult.RequestId,
                    "method", firstResult.Method,
                    "path", firstResult.Path,
                    "lastRestoreFailureKind", _lastRestoreFailureKind),
                recoverable: false);
            if (ShouldClearAuthAfterRestoreFailure(_lastRestoreFailureKind))
                ClearAuthSession();
            callback?.Invoke(firstResult);
            yield break;
        }

        AppLogger.Info(
            AppLogCategory.Auth,
            nameof(NetworkManager),
            nameof(SendAuthorizedRequest),
            "Session refreshed; retrying authorized request.");
        yield return SendRequest(requestFactory, callback, allowRetry);
    }

    private IEnumerator SendRequest(Func<UnityWebRequest> requestFactory, Action<NetworkRequestResult> callback, bool allowRetry)
    {
        EnsureHttpClient();
        yield return _httpClient.SendRequest(requestFactory, callback, allowRetry);
    }

    private void UpdateConnectivityState(bool isOnline, string error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            LastNetworkError = SanitizeNetworkMessage(error);
            if (LastErrorKind == NetworkErrorKind.Success)
                LastErrorKind = NetworkErrorKind.ClientError;
        }

        if (IsOnline == isOnline)
        {
            if (isOnline && string.IsNullOrEmpty(error))
                SetLastError(NetworkErrorKind.Success, "");
            return;
        }

        IsOnline = isOnline;

        if (isOnline)
        {
            if (string.IsNullOrEmpty(error))
                SetLastError(NetworkErrorKind.Success, "");
            OnConnectivityChanged?.Invoke(true, null);

            AppLogger.Info(
                AppLogCategory.Server,
                nameof(NetworkManager),
                nameof(UpdateConnectivityState),
                "Server connection is online.",
                LogMetadata.Of("lastErrorKind", LastErrorKind));

            if (_showOnlineToastOnRecovery)
            {
                ShowNetworkToast(_runtimeConfig != null ? _runtimeConfig.onlineMessage : "");
                _showOnlineToastOnRecovery = false;
            }

            return;
        }

        _showOnlineToastOnRecovery = true;
        if (LastErrorKind == NetworkErrorKind.Success)
            LastErrorKind = NetworkErrorKind.Offline;
        OnConnectivityChanged?.Invoke(false, LastNetworkError);
        AppLogger.Warn(
            AppLogCategory.Server,
            nameof(NetworkManager),
            nameof(UpdateConnectivityState),
            "Server connection is offline.",
            LogMetadata.Of("lastErrorKind", LastErrorKind, "lastNetworkError", LastNetworkError),
            recoverable: true);
        ShowNetworkToast(_runtimeConfig != null ? _runtimeConfig.offlineMessage : "");
    }

    private void ShowNetworkToast(string message)
    {
        if (_runtimeConfig != null && !_runtimeConfig.showOfflineToasts)
            return;

        if (string.IsNullOrWhiteSpace(message))
            return;

        ToastManager.Instance?.ShowSystemMessage(message);
    }

    private static void SetLastError(NetworkErrorKind kind, string error)
    {
        LastErrorKind = kind;
        LastNetworkError = SanitizeNetworkMessage(error);
    }

    private static string SanitizeNetworkMessage(string message)
    {
        string safe = SaveDataSanitizer.SanitizeHistoryLine(message);
        if (string.IsNullOrEmpty(safe))
            return "";

        return safe.Length <= MaxNetworkErrorChars ? safe : safe.Substring(0, MaxNetworkErrorChars);
    }

    private static string SanitizeCredential(string value, int maxLength = MaxCredentialLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string trimmed = value.Trim();
        if (trimmed.Length > Mathf.Max(1, maxLength))
            return "";

        for (int i = 0; i < trimmed.Length; i++)
        {
            if (char.IsControl(trimmed[i]))
                return "";
        }

        return trimmed;
    }

    private static void DeleteLegacyAccessToken()
    {
        try
        {
            if (PlayerPrefs.HasKey(KEY_TOKEN))
            {
                PlayerPrefs.DeleteKey(KEY_TOKEN);
                PlayerPrefs.Save();
            }
        }
        catch (Exception e)
        {
            AppLogger.Error(
                AppLogCategory.Auth,
                nameof(NetworkManager),
                nameof(DeleteLegacyAccessToken),
                "Failed to delete legacy access token.",
                e,
                recoverable: true);
        }
    }

    private void ClearAuthSession()
    {
        _authToken = null;
        _refreshToken = null;
        _playerId = null;
        IsAuthenticated = false;
        ResetProfileState();

        try
        {
            PlayerPrefs.DeleteKey(KEY_TOKEN);
            NetworkCredentialStore.ClearRefreshToken();
            PlayerPrefs.DeleteKey(KEY_PLAYER_ID);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            AppLogger.Error(
                AppLogCategory.Auth,
                nameof(NetworkManager),
                nameof(ClearAuthSession),
                "Failed to clear local auth session.",
                e,
                recoverable: true);
        }
    }

    private static void ResetProfileState()
    {
        _currentProfile.playerId = "";
        _currentProfile.isNew = false;
        _currentProfile.locale = "";
        _currentProfile.platform = "";
        _currentProfile.createdAt = "";
        _currentProfile.heroName = "";
    }

    private static void ResetBalanceState()
    {
        _lastBalance.candles = 0;
        _lastBalance.hearts = 0;
        _lastBalance.candlesCap = 0;
        _lastBalance.dailyStreakDay = 0;
        _lastBalance.dailyRewardAvailabilityKnown = false;
        _lastBalance.dailyRewardCanClaim = false;
        _lastBalance.dailyRewardAmount = 0;
        _lastBalance.dailyLastClaimAt = "";
        _lastBalance.nextCandleAt = "";
        _lastBalance.updatedAtIso = "";
        _lastBalance.isSubscriber = false;
        _lastBalance.adMultiplier = 1;
        _lastBalance.catName = "";
    }

    private void ApplyProfileFromAuth(AuthResponse response, string rawJson)
    {
        _currentProfile.playerId = SaveDataSanitizer.SanitizeIdentifier(_playerId);
        _currentProfile.isNew = response != null && (response.isNew || response.isNewLink);

        if (response != null && response.profile != null)
        {
            _currentProfile.locale = SaveDataSanitizer.SanitizeIdentifier(response.profile.locale);
            _currentProfile.platform = SaveDataSanitizer.SanitizeIdentifier(response.profile.platform);
            _currentProfile.createdAt = SaveDataSanitizer.SanitizeSavedAtIso(response.profile.createdAt);
        }
        else
        {
            _currentProfile.locale = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(rawJson, "locale"));
            _currentProfile.platform = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(rawJson, "platform"));
        }
    }

    private void ApplyBalance(BalanceResponse balance)
    {
        if (balance == null)
            return;

        _lastBalance.candles = SaveDataSanitizer.ClampCurrencyValue(balance.candles);
        _lastBalance.hearts = SaveDataSanitizer.ClampCurrencyValue(balance.hearts);
        _lastBalance.candlesCap = SaveDataSanitizer.ClampCurrencyValue(balance.candlesCap);
        _lastBalance.dailyStreakDay = Mathf.Max(0, balance.dailyStreak != null ? balance.dailyStreak.day : balance.dailyStreakDay);
        _lastBalance.dailyRewardAvailabilityKnown = balance.dailyStreak != null;
        _lastBalance.dailyRewardCanClaim = balance.dailyStreak != null && balance.dailyStreak.canClaim;
        _lastBalance.dailyRewardAmount = SaveDataSanitizer.ClampCurrencyValue(balance.dailyStreak != null ? balance.dailyStreak.reward : 0);
        _lastBalance.dailyLastClaimAt = balance.dailyStreak != null
            ? SaveDataSanitizer.SanitizeSavedAtIso(balance.dailyStreak.lastClaimAt)
            : "";
        _lastBalance.nextCandleAt = balance.nextCandleAt ?? "";
        _lastBalance.isSubscriber = balance.isSubscriber;
        _lastBalance.adMultiplier = Mathf.Clamp(balance.adMultiplier <= 0 ? 1 : balance.adMultiplier, 1, 100);
        _lastBalance.catName = SaveDataSanitizer.SanitizePlayerName(balance.catName);
        _lastBalance.updatedAtIso = DateTime.UtcNow.ToString("o");

        PlayerData.SetBalanceValues(_lastBalance.hearts, _lastBalance.candles);
    }

    private PendingProgressPayload BuildPendingProgressPayload(
        string episodeId,
        string nodeGuid,
        SaveData snapshot,
        Dictionary<string, int> stats,
        Dictionary<string, bool> flags,
        List<string> unlockedEpisodes)
    {
        SaveData serverSnapshot = CreateServerSafeSnapshot(snapshot);
        if (serverSnapshot != null && string.IsNullOrEmpty(serverSnapshot.savedAtIso))
            serverSnapshot.savedAtIso = DateTime.UtcNow.ToString("o");

        var payload = new PendingProgressPayload
        {
            storyId = serverSnapshot != null ? SaveDataSanitizer.SanitizeIdentifier(serverSnapshot.storyId) : "",
            currentEpisodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId),
            currentNodeGuid = SaveDataSanitizer.SanitizeIdentifier(nodeGuid),
            snapshot = serverSnapshot,
            savedAtIso = serverSnapshot != null && !string.IsNullOrEmpty(serverSnapshot.savedAtIso)
                ? serverSnapshot.savedAtIso
                : DateTime.UtcNow.ToString("o"),
            unlockedEpisodes = new List<string>()
        };

        if (string.IsNullOrEmpty(payload.storyId) && serverSnapshot != null)
            payload.storyId = SaveDataSanitizer.SanitizeIdentifier(serverSnapshot.storyId);

        if (stats != null)
        {
            foreach (var kv in stats)
            {
                if (payload.stats.Count >= SaveDataSanitizer.MaxStatEntries)
                    break;

                string key = SaveDataSanitizer.SanitizeStatKey(kv.Key);
                if (!string.IsNullOrEmpty(key))
                    payload.stats.Add(new StringIntPair(key, SaveDataSanitizer.ClampStatValue(kv.Value)));
            }
        }

        if (flags != null)
        {
            foreach (var kv in flags)
            {
                if (payload.flags.Count >= SaveDataSanitizer.MaxStatEntries)
                    break;

                string key = SaveDataSanitizer.SanitizeStatKey(kv.Key);
                if (!string.IsNullOrEmpty(key))
                    payload.flags.Add(new StringBoolPair(key, kv.Value));
            }
        }

        return payload;
    }

    private PendingBookmarkPayload BuildPendingBookmarkPayload(string nodeGuid, string episodeId, string storyId, SaveData snapshot, string label)
    {
        SaveData serverSnapshot = CreateServerSafeSnapshot(snapshot);
        if (serverSnapshot != null && string.IsNullOrEmpty(serverSnapshot.savedAtIso))
            serverSnapshot.savedAtIso = DateTime.UtcNow.ToString("o");

        return new PendingBookmarkPayload
        {
            nodeGuid = SaveDataSanitizer.SanitizeIdentifier(nodeGuid),
            episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId),
            storyId = !string.IsNullOrEmpty(storyId)
                ? SaveDataSanitizer.SanitizeIdentifier(storyId)
                : serverSnapshot != null ? SaveDataSanitizer.SanitizeIdentifier(serverSnapshot.storyId) : "",
            snapshot = serverSnapshot,
            label = SaveDataSanitizer.SanitizeHistoryLine(label),
            savedAtIso = serverSnapshot != null && !string.IsNullOrEmpty(serverSnapshot.savedAtIso)
                ? serverSnapshot.savedAtIso
                : DateTime.UtcNow.ToString("o")
        };
    }

    private static SaveData CreateServerSafeSnapshot(SaveData snapshot)
    {
        if (snapshot == null)
            return null;

        SaveData copy = NetworkJson.FromSaveDataJson(NetworkJson.ToSaveDataJson(snapshot));
        if (copy == null)
            return null;

        copy.playerName = ResolveNetworkPersistablePlayerName(
            copy.playerName,
            copy.storyId,
            allowStoryDefaultFallback: true);

        copy.currency = 0;
        copy.hearts = 0;
        return copy;
    }

    private static void SanitizeIncomingServerSnapshot(SaveData snapshot)
    {
        if (snapshot == null)
            return;

        SaveDataSanitizer.Sanitize(snapshot);
        snapshot.playerName = ResolveNetworkPersistablePlayerName(snapshot.playerName, snapshot.storyId);
        snapshot.currency = SaveDataSanitizer.ClampCurrencyValue(_lastBalance.candles);
        snapshot.hearts = SaveDataSanitizer.ClampCurrencyValue(_lastBalance.hearts);
    }

    private static string ResolveNetworkPersistablePlayerName(
        string value,
        string storyId = "",
        bool allowStoryDefaultFallback = false)
    {
        string safeName = SaveDataSanitizer.SanitizePlayerName(value);
        if (DialogueVariableResolver.IsPlayerNameToken(safeName))
            return "";

        bool hasCandidate = HeroCustomizationStore.IsCustomPlayerName(safeName);
        StoryManager storyManager = StoryManager.Instance;
        if (storyManager != null &&
            (hasCandidate || allowStoryDefaultFallback) &&
            StoryIdsMatchActiveStory(storyId, storyManager))
        {
            string resolvedName = storyManager.ResolvePersistablePlayerNameForSave(safeName);
            if (!string.IsNullOrWhiteSpace(resolvedName))
                return resolvedName;
        }

        return hasCandidate
            ? HeroCustomizationState.NormalizePlayerName(safeName)
            : "";
    }

    private static bool StoryIdsMatchActiveStory(string storyId, StoryManager storyManager)
    {
        if (storyManager == null)
            return false;

        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        string activeStoryId = SaveDataSanitizer.SanitizeIdentifier(storyManager.CurrentStoryId);
        return string.IsNullOrEmpty(storyId) ||
               string.IsNullOrEmpty(activeStoryId) ||
               string.Equals(storyId, activeStoryId, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerator SendPendingProgress(PendingProgressPayload pending, Action<bool> callback)
    {
        if (pending == null)
        {
            callback?.Invoke(false);
            yield break;
        }

        var body = new SaveProgressRequest
        {
            storyId = SaveDataSanitizer.SanitizeIdentifier(pending.storyId),
            episodeId = SaveDataSanitizer.SanitizeIdentifier(pending.currentEpisodeId),
            nodeId = SaveDataSanitizer.SanitizeIdentifier(pending.currentNodeGuid),
            currentEpisodeId = SaveDataSanitizer.SanitizeIdentifier(pending.currentEpisodeId),
            currentNodeGuid = SaveDataSanitizer.SanitizeIdentifier(pending.currentNodeGuid),
            stats = pending.ToStatsDictionary(),
            flags = pending.ToFlagsDictionary(),
            variables = pending.ToFlagsDictionary(),
            snapshot = CreateServerSafeSnapshot(pending.snapshot),
            unlockedEpisodes = new List<string>()
        };

        string validationError = ValidateSaveProgressRequest(body);
        if (!string.IsNullOrEmpty(validationError))
        {
            AppLogger.Error(
                AppLogCategory.SaveSystem,
                nameof(NetworkManager),
                nameof(SendPendingProgress),
                "SaveProgress request was blocked by client-side validation.",
                null,
                LogMetadata.Of(
                    "endpoint", ApiRoutes.PlayerProgressSave,
                    "storyId", body.storyId,
                    "episodeId", body.episodeId,
                    "nodeId", body.nodeId,
                    "error", validationError),
                recoverable: true);
            callback?.Invoke(false);
            yield break;
        }

        string jsonBody = NetworkJson.ToJson(body);
        yield return PostRawInternalResult(ApiRoutes.PlayerProgressSave, jsonBody, _authToken, result =>
        {
            bool ok = result != null && result.IsSuccess;
            if (!ok && result != null)
            {
                ThrottledAppLogger.Warn(
                    "SaveProgressFailed:" + result.ResponseCode + ":" + body.episodeId + ":" + body.nodeId,
                    AppLogCategory.SaveSystem,
                    nameof(NetworkManager),
                    nameof(SendPendingProgress),
                    "SaveProgress request failed.",
                    LogMetadata.Of(
                        "requestId", result.RequestId,
                        "endpoint", ApiRoutes.PlayerProgressSave,
                        "method", result.Method,
                        "path", result.Path,
                        "storyId", body.storyId,
                        "episodeId", body.episodeId,
                        "nodeId", body.nodeId,
                        "statusCode", result.ResponseCode,
                        "kind", result.Kind,
                        "attempts", result.AttemptCount,
                        "payloadChars", result.PayloadChars,
                        "error", result.Error));
            }
            callback?.Invoke(ok);
        }, allowRetry: true);
    }

    private static string ValidateSaveProgressRequest(SaveProgressRequest body)
    {
        if (body == null)
            return "SaveProgress body is null.";

        if (string.IsNullOrEmpty(body.episodeId))
            return "SaveProgress episodeId is missing after sanitization.";

        if (string.IsNullOrEmpty(body.nodeId))
            return "SaveProgress nodeId is missing after sanitization.";

        if (body.stats == null)
            return "SaveProgress stats dictionary is null.";

        if (body.variables == null)
            return "SaveProgress variables dictionary is null.";

        if (body.stats.Count > SaveDataSanitizer.MaxStatEntries)
            return "SaveProgress stats dictionary exceeds client limit.";

        if (body.variables.Count > SaveDataSanitizer.MaxStatEntries)
            return "SaveProgress variables dictionary exceeds client limit.";

        if (body.snapshot != null)
        {
            string snapshotJson = NetworkJson.ToSaveDataJson(body.snapshot);
            if (!SaveDataSanitizer.IsSerializedSizeAllowed(snapshotJson))
                return "SaveProgress snapshot exceeds client size limit.";
        }

        return "";
    }

    private IEnumerator SendPendingBookmark(PendingBookmarkPayload pending, Action<NetworkRequestResult> callback)
    {
        if (pending == null)
        {
            callback?.Invoke(null);
            yield break;
        }

        var body = new BookmarkRequest
        {
            nodeGuid = SaveDataSanitizer.SanitizeIdentifier(pending.nodeGuid),
            episodeId = SaveDataSanitizer.SanitizeIdentifier(pending.episodeId),
            storyId = SaveDataSanitizer.SanitizeIdentifier(pending.storyId),
            snapshot = CreateServerSafeSnapshot(pending.snapshot),
            label = SaveDataSanitizer.SanitizeHistoryLine(pending.label)
        };

        yield return PostRawInternalResult(ApiRoutes.PlayerBookmarkSave, NetworkJson.ToJson(body), _authToken, callback, allowRetry: true);
    }

    public IEnumerator FlushPendingSync(string storyId = null)
    {
        if (!IsAuthenticated)
            yield break;

        yield return FlushPendingProgress(storyId);
        yield return FlushPendingBookmarks(storyId);
    }

    public IEnumerator FlushPendingProgress(string storyId = null)
    {
        if (!IsAuthenticated)
            yield break;

        var keys = new List<string>(_pendingProgress.Keys);
        foreach (var key in keys)
        {
            if (!ShouldUsePendingKey(key, storyId) || !_pendingProgress.TryGetValue(key, out var pending))
                continue;

            bool ok = false;
            yield return SendPendingProgress(pending, result => ok = result);
            if (ok)
                ClearPendingProgress(key);
        }
    }

    private IEnumerator FlushPendingBookmarks(string storyId = null)
    {
        if (!IsAuthenticated || _serverBookmarkLocked)
            yield break;

        var keys = new List<string>(_pendingBookmarks.Keys);
        foreach (var key in keys)
        {
            if (!ShouldUsePendingKey(key, storyId) || !_pendingBookmarks.TryGetValue(key, out var pending))
                continue;

            NetworkRequestResult result = null;
            yield return SendPendingBookmark(pending, value => result = value);

            if (result != null && result.IsSuccess)
            {
                ClearPendingBookmark(key);
            }
            else if (result != null && result.Kind == NetworkErrorKind.PaymentRequired)
            {
                _serverBookmarkLocked = true;
                ClearPendingBookmark(key);
                yield break;
            }
        }
    }

    private static bool ShouldUsePendingKey(string key, string storyId)
    {
        return string.IsNullOrEmpty(storyId) || key == GetProgressKey(storyId);
    }

    private static void SavePendingProgress(PendingProgressPayload pending)
    {
        if (pending == null)
            return;

        string key = GetProgressKey(pending.storyId);
        _pendingProgress[key] = pending;
        _pendingSyncStore.SaveProgress(key, pending);
    }

    private static void SavePendingBookmark(PendingBookmarkPayload pending)
    {
        if (pending == null)
            return;

        string key = GetProgressKey(pending.storyId);
        _pendingBookmarks[key] = pending;
        _pendingSyncStore.SaveBookmark(key, pending);
    }

    private static void ClearPendingProgress(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        _pendingProgress.Remove(key);
        _pendingSyncStore.ClearProgress(key, _pendingProgress.Keys);
    }

    private static void ClearPendingBookmark(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        _pendingBookmarks.Remove(key);
        _pendingSyncStore.ClearBookmark(key, _pendingBookmarks.Keys);
    }

    private void LoadPendingSyncFromPrefs()
    {
        _pendingSyncStore.Load(_pendingProgress, _pendingBookmarks);
    }

    public static SaveData ResolveLatestProgressSnapshot(string storyId, SaveData localSnapshot)
    {
        var candidates = new List<ProgressSnapshotCandidate>();
        if (localSnapshot != null)
            candidates.Add(new ProgressSnapshotCandidate(localSnapshot, ProgressSnapshotSource.Local));

        var pending = GetPendingProgressSnapshot(storyId);
        if (pending != null)
            candidates.Add(new ProgressSnapshotCandidate(pending, ProgressSnapshotSource.Pending));

        var server = BuildLoadedProgressSnapshot(storyId);
        if (server != null)
            candidates.Add(new ProgressSnapshotCandidate(server, ProgressSnapshotSource.Server));

        if (candidates.Count == 0)
            return null;

        candidates.Sort(CompareProgressCandidates);
        var winner = candidates[candidates.Count - 1];
        string key = GetProgressKey(storyId);

        if (winner.Source != ProgressSnapshotSource.Pending)
            ClearPendingProgress(key);

        return winner.Snapshot;
    }

    public static SaveData GetPendingProgressSnapshot(string storyId)
    {
        if (!_pendingProgress.TryGetValue(GetProgressKey(storyId), out var pending) || pending == null)
            return null;

        if (pending.snapshot != null)
            return pending.snapshot;

        if (string.IsNullOrEmpty(pending.currentNodeGuid))
            return null;

        return new SaveData
        {
            version = 1,
            storyId = pending.storyId ?? "",
            episodeId = pending.currentEpisodeId ?? "",
            currentNodeGuid = pending.currentNodeGuid ?? "",
            savedAtIso = pending.savedAtIso ?? ""
        };
    }

    public static SaveData BuildLoadedProgressSnapshot(string fallbackStoryId = "")
    {
        if (!string.IsNullOrEmpty(LastProgressSnapshotJson))
        {
            var snapshot = NetworkJson.FromSaveDataJson(LastProgressSnapshotJson);
            if (snapshot != null)
            {
                if (string.IsNullOrEmpty(snapshot.storyId))
                    snapshot.storyId = fallbackStoryId ?? "";
                if (string.IsNullOrEmpty(snapshot.savedAtIso))
                    snapshot.savedAtIso = LastProgressUpdatedAtIso;
                return snapshot;
            }
        }

        if (string.IsNullOrEmpty(LastProgressNodeGuid))
            return null;

        return new SaveData
        {
            version = 1,
            storyId = fallbackStoryId ?? "",
            episodeId = LastProgressEpisodeId ?? "",
            currentNodeGuid = LastProgressNodeGuid ?? "",
            savedAtIso = LastProgressUpdatedAtIso ?? ""
        };
    }

    private static int CompareProgressCandidates(ProgressSnapshotCandidate a, ProgressSnapshotCandidate b)
    {
        int timeCompare = CompareSnapshotTime(a.Snapshot, b.Snapshot);
        if (timeCompare != 0)
            return timeCompare;

        int completenessCompare = GetSnapshotCompleteness(a.Snapshot).CompareTo(GetSnapshotCompleteness(b.Snapshot));
        if (completenessCompare != 0)
            return completenessCompare;

        return GetSourceRank(a.Source).CompareTo(GetSourceRank(b.Source));
    }

    private static int CompareSnapshotTime(SaveData a, SaveData b)
    {
        bool aHasTime = TryParseSnapshotTime(a, out var aTime);
        bool bHasTime = TryParseSnapshotTime(b, out var bTime);
        if (aHasTime && bHasTime)
            return aTime.CompareTo(bTime);
        if (aHasTime)
            return 1;
        if (bHasTime)
            return -1;
        return 0;
    }

    private static bool TryParseSnapshotTime(SaveData snapshot, out DateTime time)
    {
        time = default;
        if (snapshot == null || string.IsNullOrEmpty(snapshot.savedAtIso))
            return false;

        return DateTime.TryParse(
            snapshot.savedAtIso,
            null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out time);
    }

    private static int GetSnapshotCompleteness(SaveData snapshot)
    {
        if (snapshot == null)
            return 0;

        int score = 0;
        if (snapshot.version >= SaveData.CurrentVersion) score += 4;
        if (!string.IsNullOrEmpty(snapshot.currentNodeGuid)) score++;
        if (!string.IsNullOrEmpty(snapshot.episodeId)) score++;
        if (!string.IsNullOrEmpty(snapshot.storyId)) score++;
        if (snapshot.history != null && snapshot.history.Count > 0) score++;
        if (snapshot.statKeys != null && snapshot.statKeys.Count > 0) score++;
        if (!string.IsNullOrEmpty(snapshot.savedAtIso)) score++;
        return score;
    }

    private static int GetSourceRank(ProgressSnapshotSource source)
    {
        switch (source)
        {
            case ProgressSnapshotSource.Server:
                return 3;
            case ProgressSnapshotSource.Pending:
                return 2;
            case ProgressSnapshotSource.Local:
                return 1;
            default:
                return 0;
        }
    }

    private static string GetProgressKey(string storyId)
    {
        if (string.IsNullOrWhiteSpace(storyId))
            return "default";

        string key = storyId.Trim();
        char[] invalid = { '\r', '\n', '\t', '\0' };
        for (int i = 0; i < invalid.Length; i++)
            key = key.Replace(invalid[i], '_');

        if (key.Length <= 80)
            return key;

        return key.Substring(0, 48) + "_" + StableHash(key);
    }

    private static string StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < value.Length; i++)
                hash = (hash ^ value[i]) * 16777619;

            return hash.ToString("x8");
        }
    }

    private static string FirstNonEmptyRawString(params string[] values)
    {
        if (values == null)
            return "";

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value) && value != "null")
                return value;
        }

        return "";
    }

    private string GetActiveEnvironmentId()
    {
        return _runtimeConfig != null ? _runtimeConfig.ResolveSelectedEnvironmentId() : "";
    }

    private string GetActiveBaseUrl()
    {
        return _resolvedBaseUrl ?? "";
    }

    // ── Device ID ─────────────────────────────────────────────

    private static string GetSafeBaseHost(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "";

        return Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri)
            ? uri.Host
            : "";
    }

    private static string GetOrCreateDeviceId()
    {
        var id = SaveDataSanitizer.SanitizeIdentifier(PlayerPrefs.GetString(KEY_DEVICE_ID, null));
        if (!string.IsNullOrEmpty(id)) return id;

        id = SaveDataSanitizer.SanitizeIdentifier(SystemInfo.deviceUniqueIdentifier);
        if (string.IsNullOrEmpty(id) || id == SystemInfo.unsupportedIdentifier)
            id = Guid.NewGuid().ToString();

        PlayerPrefs.SetString(KEY_DEVICE_ID, id);
        PlayerPrefs.Save();
        return id;
    }

    private static string GetPlatform()
    {
#if UNITY_IOS
        return "ios";
#elif UNITY_ANDROID
        return "android";
#else
        return "editor";
#endif
    }

    // ── JSON helpers ──────────────────────────────────────────

}

// ── Request / Response DTOs ───────────────────────────────────

internal static class NetworkCredentialStore
{
    private const string LegacyRefreshTokenKey = "VN_REFRESH_TOKEN";
    private const string ProtectedRefreshTokenKey = "VN_REFRESH_TOKEN_V2";
    private const string PayloadPrefix = "v1:";
    private const int SaltSize = 16;
    private const int IvSize = 16;
    private const int MacSize = 32;
    private const int KeySize = 32;
    private const int DerivationIterations = 10000;
    private const int MaxRefreshTokenChars = 8192;
    private const int MaxProtectedPayloadChars = 32768;

    public static string LoadRefreshToken(string deviceId)
    {
        string protectedPayload = PlayerPrefs.GetString(ProtectedRefreshTokenKey, "");
        if (protectedPayload.Length > MaxProtectedPayloadChars)
        {
            AppLogger.Warn(
                AppLogCategory.Security,
                nameof(NetworkCredentialStore),
                nameof(LoadRefreshToken),
                "Stored refresh token payload is too large and will be cleared.",
                LogMetadata.Of("payloadChars", protectedPayload.Length, "maxPayloadChars", MaxProtectedPayloadChars),
                recoverable: true);
            PlayerPrefs.DeleteKey(ProtectedRefreshTokenKey);
            PlayerPrefs.Save();
            protectedPayload = "";
        }

        if (!string.IsNullOrEmpty(protectedPayload))
        {
            string token = Unprotect(protectedPayload, deviceId);
            if (!string.IsNullOrEmpty(token))
            {
                DeleteLegacyRefreshToken(false);
                return token;
            }

            AppLogger.Warn(
                AppLogCategory.Security,
                nameof(NetworkCredentialStore),
                nameof(LoadRefreshToken),
                "Stored refresh token is invalid and will be cleared.",
                recoverable: true);
        }

        string legacyPayload = PlayerPrefs.GetString(LegacyRefreshTokenKey, "");
        string legacyToken = SanitizeStoredRefreshToken(legacyPayload);
        if (string.IsNullOrEmpty(legacyToken))
        {
            if (!string.IsNullOrEmpty(legacyPayload))
                PlayerPrefs.DeleteKey(LegacyRefreshTokenKey);

            if (!string.IsNullOrEmpty(protectedPayload))
                PlayerPrefs.DeleteKey(ProtectedRefreshTokenKey);

            if (!string.IsNullOrEmpty(legacyPayload) || !string.IsNullOrEmpty(protectedPayload))
            {
                PlayerPrefs.Save();
            }

            return null;
        }

        SaveRefreshToken(legacyToken, deviceId);
        return legacyToken;
    }

    public static void SaveRefreshToken(string refreshToken, string deviceId)
    {
        refreshToken = SanitizeStoredRefreshToken(refreshToken);
        if (string.IsNullOrEmpty(refreshToken))
        {
            ClearRefreshToken();
            return;
        }

        string protectedPayload = Protect(refreshToken, deviceId);
        if (string.IsNullOrEmpty(protectedPayload))
        {
            AppLogger.Error(
                AppLogCategory.Security,
                nameof(NetworkCredentialStore),
                nameof(SaveRefreshToken),
                "Failed to protect refresh token; clearing local session.",
                null,
                LogMetadata.Of("hasDeviceId", !string.IsNullOrEmpty(deviceId)),
                recoverable: true);
            ClearRefreshToken();
            return;
        }

        PlayerPrefs.SetString(ProtectedRefreshTokenKey, protectedPayload);
        DeleteLegacyRefreshToken(false);
        PlayerPrefs.Save();
    }

    public static void ClearRefreshToken()
    {
        PlayerPrefs.DeleteKey(ProtectedRefreshTokenKey);
        PlayerPrefs.DeleteKey(LegacyRefreshTokenKey);
        PlayerPrefs.Save();
    }

    private static string Protect(string value, string deviceId)
    {
        try
        {
            byte[] salt = CreateRandomBytes(SaltSize);
            byte[] iv = CreateRandomBytes(IvSize);
            byte[] plainBytes = Encoding.UTF8.GetBytes(value);

            DeriveKeyMaterial(deviceId, salt, out byte[] encryptionKey, out byte[] macKey);

            byte[] cipherBytes;
            using (Aes aes = Aes.Create())
            {
                if (aes == null)
                    return null;

                aes.Key = encryptionKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                    cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            }

            byte[] signedBytes = Combine(salt, iv, cipherBytes);
            byte[] macBytes;
            using (HMACSHA256 hmac = new HMACSHA256(macKey))
                macBytes = hmac.ComputeHash(signedBytes);

            return PayloadPrefix + Convert.ToBase64String(Combine(signedBytes, macBytes));
        }
        catch (Exception ex)
        {
            AppLogger.Error(
                AppLogCategory.Security,
                nameof(NetworkCredentialStore),
                nameof(Protect),
                "Refresh token protection failed.",
                ex,
                LogMetadata.Of("hasDeviceId", !string.IsNullOrEmpty(deviceId)),
                recoverable: true);
            return null;
        }
    }

    private static string Unprotect(string payload, string deviceId)
    {
        try
        {
            if (string.IsNullOrEmpty(payload) || !payload.StartsWith(PayloadPrefix, StringComparison.Ordinal))
                return null;
            if (payload.Length > MaxProtectedPayloadChars)
                return null;

            byte[] allBytes = Convert.FromBase64String(payload.Substring(PayloadPrefix.Length));
            int cipherSize = allBytes.Length - SaltSize - IvSize - MacSize;
            if (cipherSize <= 0)
                return null;

            byte[] salt = Slice(allBytes, 0, SaltSize);
            byte[] iv = Slice(allBytes, SaltSize, IvSize);
            byte[] cipherBytes = Slice(allBytes, SaltSize + IvSize, cipherSize);
            byte[] storedMac = Slice(allBytes, SaltSize + IvSize + cipherSize, MacSize);
            byte[] signedBytes = Slice(allBytes, 0, SaltSize + IvSize + cipherSize);

            DeriveKeyMaterial(deviceId, salt, out byte[] encryptionKey, out byte[] macKey);

            byte[] expectedMac;
            using (HMACSHA256 hmac = new HMACSHA256(macKey))
                expectedMac = hmac.ComputeHash(signedBytes);

            if (!FixedTimeEquals(storedMac, expectedMac))
                return null;

            byte[] plainBytes;
            using (Aes aes = Aes.Create())
            {
                if (aes == null)
                    return null;

                aes.Key = encryptionKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            }

            string token = Encoding.UTF8.GetString(plainBytes).Trim();
            return SanitizeStoredRefreshToken(token);
        }
        catch (Exception ex)
        {
            AppLogger.Error(
                AppLogCategory.Security,
                nameof(NetworkCredentialStore),
                nameof(Unprotect),
                "Refresh token restore failed.",
                ex,
                LogMetadata.Of("hasDeviceId", !string.IsNullOrEmpty(deviceId)),
                recoverable: true);
            return null;
        }
    }

    private static string SanitizeStoredRefreshToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        token = token.Trim();
        if (token.Length > MaxRefreshTokenChars)
            return null;

        for (int i = 0; i < token.Length; i++)
        {
            if (char.IsControl(token[i]))
                return null;
        }

        return token;
    }

    private static void DeriveKeyMaterial(string deviceId, byte[] salt, out byte[] encryptionKey, out byte[] macKey)
    {
        string appId = Application.identifier ?? "";
        string uniqueId = SystemInfo.deviceUniqueIdentifier ?? "";
        string secret = (deviceId ?? "") + "|" + appId + "|" + uniqueId;

        using (Rfc2898DeriveBytes derive = new Rfc2898DeriveBytes(secret, salt, DerivationIterations))
        {
            byte[] keyMaterial = derive.GetBytes(KeySize * 2);
            encryptionKey = Slice(keyMaterial, 0, KeySize);
            macKey = Slice(keyMaterial, KeySize, KeySize);
        }
    }

    private static byte[] CreateRandomBytes(int size)
    {
        byte[] bytes = new byte[size];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            rng.GetBytes(bytes);
        return bytes;
    }

    private static byte[] Slice(byte[] source, int offset, int count)
    {
        byte[] result = new byte[count];
        Buffer.BlockCopy(source, offset, result, 0, count);
        return result;
    }

    private static byte[] Combine(params byte[][] arrays)
    {
        int totalLength = 0;
        foreach (byte[] array in arrays)
        {
            if (array != null)
                totalLength += array.Length;
        }

        byte[] result = new byte[totalLength];
        int offset = 0;
        foreach (byte[] array in arrays)
        {
            if (array == null || array.Length == 0)
                continue;

            Buffer.BlockCopy(array, 0, result, offset, array.Length);
            offset += array.Length;
        }

        return result;
    }

    private static bool FixedTimeEquals(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
            return false;

        int diff = 0;
        for (int i = 0; i < left.Length; i++)
            diff |= left[i] ^ right[i];

        return diff == 0;
    }

    private static void DeleteLegacyRefreshToken(bool save)
    {
        PlayerPrefs.DeleteKey(LegacyRefreshTokenKey);
        if (save)
            PlayerPrefs.Save();
    }
}

#pragma warning disable 0649 // DTO fields are populated by JsonUtility/NetworkJson.

internal sealed class NetworkRequestResult
{
    public string Text;
    public string Error;
    public long ResponseCode;
    public UnityWebRequest.Result Result;
    public NetworkErrorKind Kind;
    public string RequestId;
    public string Method;
    public string Path;
    public int AttemptCount;
    public int PayloadChars;

    public bool IsSuccess => Kind == NetworkErrorKind.Success && Result == UnityWebRequest.Result.Success;
}

[Serializable]
internal sealed class GuestAuthRequest
{
    public string deviceId;
    public string platform;
    public string appVersion;
}

[Serializable]
internal sealed class AuthResponse
{
    public string playerId;
    public string token;
    public string authToken;
    public string refreshToken;
    public bool isNew;
    public bool isNewLink;
    public AuthProfile profile;
    public BalanceResponse balances;
    public BootstrapProgressResponse progress;
}

[Serializable]
internal sealed class BalanceResponse
{
    public int hearts;
    public int candles;
    public int candlesCap;
    public bool isSubscriber;
    public int adMultiplier;
    public int dailyStreakDay;
    public string catName;
    public string nextCandleAt;
    public DailyStreakResponse dailyStreak;
}

[Serializable]
internal sealed class DailyStreakResponse
{
    public int day;
    public string lastClaimAt;
    public bool canClaim;
    public int reward;
}

[Serializable]
internal sealed class AuthProfile
{
    public string locale;
    public string platform;
    public string createdAt;
}

[Serializable]
internal sealed class BootstrapProgressResponse
{
    public int schemaVersion;
    public string data;
}

[Serializable]
internal sealed class FeaturesResponse
{
    public bool fullAccess;
    public FastForwardFeature fastForward;
    public BookmarksFeature bookmarks;
}

[Serializable]
internal sealed class FastForwardFeature
{
    public bool enabled;
    public int steps;
}

[Serializable]
internal sealed class BookmarksFeature
{
    public bool enabled;
    public int capacity;
}

[Serializable]
internal sealed class HeroNameResponse
{
    public string heroName;
}

[Serializable]
internal sealed class HeroNameRequest
{
    public string name;
    public string storyId;
}

[Serializable]
internal sealed class SaveProgressRequest
{
    public string storyId;
    public string episodeId;
    public string nodeId;
    public string currentEpisodeId;
    public string currentNodeGuid;
    public Dictionary<string, int> stats;
    public Dictionary<string, bool> flags;
    public Dictionary<string, bool> variables;
    public SaveData snapshot;
    public List<string> unlockedEpisodes;
}

[Serializable]
internal sealed class ProgressResponse
{
    public int schemaVersion;
    public string storyId;
    public string heroName;
    public List<HeroNameHistoryEntry> heroNameHistory;
    public string currentEpisodeId;
    public string currentNodeGuid;
    public string updatedAt;
    public string savedAt;
    public SaveData snapshot;
    public ProgressFeaturesSnapshot features;
}

[Serializable]
internal sealed class BookmarkRequest
{
    public string nodeGuid;
    public string episodeId;
    public string storyId;
    public SaveData snapshot;
    public string label;
}

[Serializable]
internal sealed class HeroNameHistoryEntry
{
    public string Name;
    public string ChangedAt;
    public string NodeGuid;
    public string EpisodeId;
}

[Serializable]
internal sealed class ProgressFeaturesSnapshot
{
    public bool fullAccess;
    public bool fastForwardEnabled;
    public bool bookmarksEnabled;
}

[Serializable]
internal sealed class BookmarkEnvelope
{
    public BookmarkInfo bookmark;
    public List<BookmarkInfo> bookmarks = new List<BookmarkInfo>();
    public List<BookmarkInfo> items = new List<BookmarkInfo>();

    public BookmarkInfo GetFirstBookmark()
    {
        var bookmark = GetFirstBookmark(bookmarks);
        if (bookmark != null)
            return bookmark;

        return GetFirstBookmark(items);
    }

    private static BookmarkInfo GetFirstBookmark(List<BookmarkInfo> source)
    {
        if (source == null)
            return null;

        foreach (var item in source)
        {
            if (HasBookmarkData(item))
                return item;
        }

        return null;
    }

    private static bool HasBookmarkData(BookmarkInfo bookmark)
    {
        return bookmark != null &&
               (!string.IsNullOrEmpty(bookmark.nodeGuid) ||
                !string.IsNullOrEmpty(bookmark.episodeId) ||
                HasSnapshotData(bookmark.snapshot));
    }

    private static bool HasSnapshotData(SaveData snapshot)
    {
        if (snapshot == null)
            return false;

        return snapshot.HasPosition ||
               !string.IsNullOrEmpty(snapshot.storyId) ||
               !string.IsNullOrEmpty(snapshot.seasonId) ||
               !string.IsNullOrEmpty(snapshot.chapterId) ||
               !string.IsNullOrEmpty(snapshot.episodeId) ||
               !string.IsNullOrEmpty(snapshot.graphName) ||
               !string.IsNullOrEmpty(snapshot.currentNodeGuid) ||
               !string.IsNullOrEmpty(snapshot.savedAtIso) ||
               !string.IsNullOrEmpty(snapshot.playerName) ||
               HasItems(snapshot.history) ||
               HasItems(snapshot.wardrobe) ||
               HasItems(snapshot.equippedClothes) ||
               HasItems(snapshot.statKeys) ||
               HasItems(snapshot.statValues);
    }

    private static bool HasItems<T>(List<T> items)
    {
        return items != null && items.Count > 0;
    }
}

[Serializable]
internal sealed class BookmarkInfo
{
    public string nodeGuid;
    public string episodeId;
    public string storyId;
    public string savedAt;
    public string label;
    public SaveData snapshot;
}

[Serializable]
public class EpisodeGraphResponse
{
    public string episodeId;
    public string contentVersion;
    public string graphJson;
    public string rawPayloadJson;
    public bool notModified;
}

[Serializable]
public class CatalogSeasonResponse
{
    public string seasonId;
    public string storyId;
    public string title;
    public int order;
    public List<CatalogEpisodeResponse> episodes = new List<CatalogEpisodeResponse>();
}

[Serializable]
public class CatalogEpisodeResponse
{
    public string episodeId;
    public string seasonId;
    public string storyId;
    public int order;
    public string title;
    public bool isPremium;
    public int candleCost;
    public bool isUnlocked;
    public bool isGeoRestricted;
    public string contentVersion;
    public bool hasRemoteContent;
}

[Serializable]
internal sealed class CandlesResponse
{
    public int candles;
    public string nextCandleAt;
}

[Serializable]
internal sealed class EmptyRequest
{
}

public enum NetworkErrorKind
{
    Success,
    Unauthorized,
    PaymentRequired,
    ClientError,
    ServerError,
    Timeout,
    Offline,
    InvalidResponse
}

[Serializable]
public class PlayerProfileState
{
    public string playerId;
    public bool isNew;
    public string locale;
    public string platform;
    public string createdAt;
    public string heroName;
}

[Serializable]
public class PlayerBalanceState
{
    public int hearts;
    public int candles;
    public int candlesCap;
    public bool isSubscriber;
    public int adMultiplier = 1;
    public int dailyStreakDay;
    public bool dailyRewardAvailabilityKnown;
    public bool dailyRewardCanClaim;
    public int dailyRewardAmount;
    public string dailyLastClaimAt;
    public string catName;
    public string nextCandleAt;
    public string updatedAtIso;
}

[Serializable]
public class PendingProgressPayload
{
    public string storyId;
    public string currentEpisodeId;
    public string currentNodeGuid;
    public SaveData snapshot;
    public List<StringIntPair> stats = new List<StringIntPair>();
    public List<StringBoolPair> flags = new List<StringBoolPair>();
    public List<string> unlockedEpisodes = new List<string>();
    public string savedAtIso;

    public Dictionary<string, int> ToStatsDictionary()
    {
        var result = new Dictionary<string, int>();
        if (stats == null)
            return result;

        foreach (var pair in stats)
        {
            if (result.Count >= SaveDataSanitizer.MaxStatEntries)
                break;
            if (pair == null)
                continue;

            string key = SaveDataSanitizer.SanitizeStatKey(pair.key);
            if (!string.IsNullOrEmpty(key))
                result[key] = SaveDataSanitizer.ClampStatValue(pair.value);
        }

        return result;
    }

    public Dictionary<string, bool> ToFlagsDictionary()
    {
        var result = new Dictionary<string, bool>();
        if (flags == null)
            return result;

        foreach (var pair in flags)
        {
            if (result.Count >= SaveDataSanitizer.MaxStatEntries)
                break;
            if (pair == null)
                continue;

            string key = SaveDataSanitizer.SanitizeStatKey(pair.key);
            if (!string.IsNullOrEmpty(key))
                result[key] = pair.value;
        }

        return result;
    }
}

[Serializable]
public class PendingBookmarkPayload
{
    public string nodeGuid;
    public string episodeId;
    public string storyId;
    public SaveData snapshot;
    public string label;
    public string savedAtIso;
}

[Serializable]
public class StringIntPair
{
    public string key;
    public int value;

    public StringIntPair() { }

    public StringIntPair(string key, int value)
    {
        this.key = key;
        this.value = value;
    }
}

[Serializable]
public class StringBoolPair
{
    public string key;
    public bool value;

    public StringBoolPair() { }

    public StringBoolPair(string key, bool value)
    {
        this.key = key;
        this.value = value;
    }
}

[Serializable]
internal sealed class DocumentedCatalogEpisodeResponse
{
    public string id;
    public string episodeId;
    public string storyId;
    public string seasonId;
    public string title;
    public int season;
    public int episode;
    public int order;
    public string version;
    public string contentVersion;
    public bool isPremium;
    public int candleCost;
    public bool isUnlocked;
    public bool isGeoRestricted;
}

internal enum ProgressSnapshotSource
{
    Local,
    Pending,
    Server
}

internal struct ProgressSnapshotCandidate
{
    public SaveData Snapshot;
    public ProgressSnapshotSource Source;

    public ProgressSnapshotCandidate(SaveData snapshot, ProgressSnapshotSource source)
    {
        Snapshot = snapshot;
        Source = source;
    }
}

#pragma warning restore 0649
