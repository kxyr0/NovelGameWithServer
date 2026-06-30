using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

public enum RegionAccessMode
{
    Disabled,
    ReportOnly,
    Enforce
}

public enum RegionAccessSource
{
    SystemRegion,
    OverrideForTesting,
    IpGeolocation
}

public enum RegionLookupState
{
    NotStarted,
    Resolving,
    Resolved,
    Failed
}

[DisallowMultipleComponent]
[AddComponentMenu("Novel Template/Compliance/Region Access Gate")]
public sealed class RegionAccessGate : MonoBehaviour
{
    private static readonly string[] DefaultRestrictedRegionCodes = { "RU", "BY" };
    private const string IpRegionCodePrefsKey = "VN_IP_REGION_CODE";
    private const string IpRegionTimePrefsKey = "VN_IP_REGION_TIME";
    private static RegionAccessGate _instance;

    [Header("Mode")]
    [SerializeField] private RegionAccessMode _mode = RegionAccessMode.Disabled;
    [SerializeField] private bool _allowDeveloperBypass = true;
    [SerializeField] private RegionAccessSource _source = RegionAccessSource.IpGeolocation;
    [SerializeField] private string _testingRegionOverride;

    [Header("IP Geolocation")]
    [SerializeField] private string _ipGeolocationUrl = "https://ipapi.co/json/";
    [SerializeField, Min(1)] private int _ipRequestTimeoutSeconds = 4;
    [SerializeField] private bool _useCachedIpRegion = false;
    [SerializeField, Min(0f)] private float _ipRegionCacheHours = 24f;
    [SerializeField] private bool _blockWhenIpLookupUnavailable = true;
    [SerializeField] private bool _useSystemRegionFallbackWhenIpFails = false;
    [SerializeField] private bool _hideRestrictedChoicesUntilIpResolved = true;
    [SerializeField] private List<string> _ipCountryCodeJsonKeys = new List<string>
    {
        "country",
        "country_code",
        "countryCode"
    };

    [Header("Blocked Regions")]
    [SerializeField] private List<string> _blockedRegionCodes = new List<string> { "RU", "BY" };

    [Header("UI")]
    [SerializeField] private GameObject _blockedRegionPanel;
    [SerializeField] private bool _pauseGameWhenBlocked = true;
    [SerializeField] private UnityEvent _blocked = new UnityEvent();
    [SerializeField] private UnityEvent _allowed = new UnityEvent();
    [SerializeField] private UnityEvent _regionResolved = new UnityEvent();
    [SerializeField] private UnityEvent _regionLookupFailed = new UnityEvent();

    private Coroutine _ipLookupRoutine;
    private bool _pausedByRegionBlock;
    private float _timeScaleBeforeRegionBlock = 1f;
    private const int MaxIpGeolocationResponseChars = 8192;

    public RegionAccessMode Mode => _mode;
    public RegionLookupState LookupState { get; private set; } = RegionLookupState.NotStarted;
    public string LastResolvedRegionCode { get; private set; } = "";
    public bool LastEvaluationBlocked { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRegionAccessGateExists()
    {
        if (_instance != null)
            return;

        GameObject gateObject = new GameObject(nameof(RegionAccessGate));
        DontDestroyOnLoad(gateObject);
        gateObject.AddComponent<RegionAccessGate>();
    }

    private void Awake()
    {
        _instance = this;
        if (_source == RegionAccessSource.IpGeolocation)
            StartIpRegionLookup();

        Evaluate();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    [ContextMenu("Evaluate Region Access")]
    public bool Evaluate()
    {
        LastResolvedRegionCode = ResolveCurrentRegionCode();
        LastEvaluationBlocked = ShouldBlockRegion(LastResolvedRegionCode);
        bool lookupUnavailableBlock = ShouldBlockForUnavailableIpLookup();

        bool enforceBlock = _mode == RegionAccessMode.Enforce &&
                            (LastEvaluationBlocked || lookupUnavailableBlock) &&
                            !HasDeveloperBypass();

        if (_blockedRegionPanel != null)
            _blockedRegionPanel.SetActive(enforceBlock);

        if (enforceBlock)
        {
            if (_pauseGameWhenBlocked)
            {
                if (!_pausedByRegionBlock)
                    _timeScaleBeforeRegionBlock = Time.timeScale;

                Time.timeScale = 0f;
                _pausedByRegionBlock = true;
            }

            _blocked.Invoke();
            return false;
        }

        if (_pausedByRegionBlock)
        {
            Time.timeScale = _timeScaleBeforeRegionBlock;
            _pausedByRegionBlock = false;
        }

        _allowed.Invoke();
        return true;
    }

    public void StartIpRegionLookup()
    {
        if (_source != RegionAccessSource.IpGeolocation || !isActiveAndEnabled)
            return;

        if (_ipLookupRoutine != null)
            StopCoroutine(_ipLookupRoutine);

        _ipLookupRoutine = StartCoroutine(ResolveIpRegionRoutine());
    }

    public bool IsBlockedForCurrentUser()
    {
        string regionCode = ResolveCurrentRegionCode();
        return _mode != RegionAccessMode.Disabled &&
               (ShouldBlockRegion(regionCode) || ShouldBlockForUnavailableIpLookup()) &&
               !HasDeveloperBypass();
    }

    private bool ShouldBlockRegion(string regionCode)
    {
        if (_mode == RegionAccessMode.Disabled || string.IsNullOrWhiteSpace(regionCode))
            return false;

        if (_blockedRegionCodes == null)
            return false;

        string normalized = NormalizeRegionCode(regionCode);
        for (int i = 0; i < _blockedRegionCodes.Count; i++)
        {
            if (string.Equals(NormalizeRegionCode(_blockedRegionCodes[i]), normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool HasDeveloperBypass()
    {
        if (!_allowDeveloperBypass)
            return false;

        return IsDeveloperBuildContext();
    }

    private static bool IsDevelopmentBuild()
    {
#if DEVELOPMENT_BUILD
        return true;
#else
        return false;
#endif
    }

    private string ResolveCurrentRegionCode()
    {
        if (_source == RegionAccessSource.OverrideForTesting)
            return NormalizeRegionCode(_testingRegionOverride);

        if (_source == RegionAccessSource.IpGeolocation)
        {
            if (!string.IsNullOrWhiteSpace(LastResolvedRegionCode))
                return NormalizeRegionCode(LastResolvedRegionCode);

            if (TryReadCachedIpRegion(out string cachedRegion))
                return cachedRegion;

            return _useSystemRegionFallbackWhenIpFails ? ResolveSystemRegionCode() : "";
        }

        return ResolveSystemRegionCode();
    }

    public static string GetCurrentRegionCode()
    {
        RegionAccessGate gate = FindCurrentGate();
        return gate != null ? gate.ResolveCurrentRegionCode() : ResolveSystemRegionCode();
    }

    public static bool IsIpRegionLookupPending()
    {
        RegionAccessGate gate = FindCurrentGate();
        return gate != null &&
               gate._source == RegionAccessSource.IpGeolocation &&
               (gate.LookupState == RegionLookupState.NotStarted ||
                gate.LookupState == RegionLookupState.Resolving);
    }

    public static bool ShouldHoldRegionSensitiveChoices()
    {
        RegionAccessGate gate = FindCurrentGate();
        return gate != null &&
               gate._source == RegionAccessSource.IpGeolocation &&
               gate._hideRestrictedChoicesUntilIpResolved &&
               string.IsNullOrWhiteSpace(gate.LastResolvedRegionCode) &&
               IsIpRegionLookupPending() &&
               !HasActiveDeveloperBypass();
    }

    public static bool ShouldHideRegionSensitiveChoicesWithoutResolvedIp()
    {
        RegionAccessGate gate = FindCurrentGate();
        return gate != null &&
               gate._source == RegionAccessSource.IpGeolocation &&
               gate._hideRestrictedChoicesUntilIpResolved &&
               string.IsNullOrWhiteSpace(gate.LastResolvedRegionCode) &&
               !HasActiveDeveloperBypass();
    }

    public static void EnsureIpLookupStarted()
    {
        RegionAccessGate gate = FindCurrentGate();
        if (gate != null &&
            gate._source == RegionAccessSource.IpGeolocation &&
            gate.LookupState == RegionLookupState.NotStarted)
        {
            gate.StartIpRegionLookup();
        }
    }

    public static bool HasActiveDeveloperBypass()
    {
        RegionAccessGate gate = FindCurrentGate();
        return gate != null ? gate.HasDeveloperBypass() : IsDeveloperBuildContext();
    }

    public static bool IsRestrictedRegionCode(string regionCode)
    {
        regionCode = NormalizeRegionCode(regionCode);
        if (string.IsNullOrEmpty(regionCode))
            return false;

        RegionAccessGate gate = FindCurrentGate();
        if (gate != null && gate._blockedRegionCodes != null && gate._blockedRegionCodes.Count > 0)
        {
            for (int i = 0; i < gate._blockedRegionCodes.Count; i++)
            {
                if (string.Equals(NormalizeRegionCode(gate._blockedRegionCodes[i]), regionCode, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        for (int i = 0; i < DefaultRestrictedRegionCodes.Length; i++)
        {
            if (string.Equals(DefaultRestrictedRegionCodes[i], regionCode, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static string ResolveSystemRegionCode()
    {
        try
        {
            return NormalizeRegionCode(RegionInfo.CurrentRegion.TwoLetterISORegionName);
        }
        catch (Exception exception)
        {
            ThrottledAppLogger.Warn(
                nameof(RegionAccessGate) + ".ResolveSystemRegionCode",
                AppLogCategory.Security,
                nameof(RegionAccessGate),
                nameof(ResolveSystemRegionCode),
                "System region code could not be resolved.",
                LogMetadata.Of("errorType", exception.GetType().Name));
            return "";
        }
    }

    private IEnumerator ResolveIpRegionRoutine()
    {
        LookupState = RegionLookupState.Resolving;

        if (TryReadCachedIpRegion(out string cachedRegion))
            LastResolvedRegionCode = cachedRegion;

        string url = ResolveIpGeolocationUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            HandleIpLookupFailed("IP geolocation URL is empty.");
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = Mathf.Max(1, _ipRequestTimeoutSeconds);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                HandleIpLookupFailed(request.error);
                yield break;
            }

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : "";
            if (responseText.Length > MaxIpGeolocationResponseChars)
            {
                HandleIpLookupFailed("IP geolocation response is too large.");
                yield break;
            }

            string countryCode = ParseCountryCodeFromIpResponse(responseText);
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                HandleIpLookupFailed("IP geolocation response did not contain a country code.");
                yield break;
            }

            LastResolvedRegionCode = countryCode;
            LookupState = RegionLookupState.Resolved;
            CacheIpRegion(countryCode);
            Evaluate();
            _regionResolved.Invoke();
        }

        _ipLookupRoutine = null;
    }

    private string ResolveIpGeolocationUrl()
    {
        string url = _ipGeolocationUrl ?? "";
        if (string.IsNullOrWhiteSpace(url))
            return "";

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return IsAllowedIpGeolocationUrl(url) ? url : "";
        }

        string baseUrl = NetworkManager.ActiveBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "";

        string resolvedUrl = baseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
        return IsAllowedIpGeolocationUrl(resolvedUrl) ? resolvedUrl : "";
    }

    private static bool IsAllowedIpGeolocationUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            return false;

        if (uri.Scheme == Uri.UriSchemeHttps)
            return true;

        if (uri.Scheme == Uri.UriSchemeHttp)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return uri.IsLoopback;
#else
            return false;
#endif
        }

        return false;
    }

    private string ParseCountryCodeFromIpResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "";

        if (_ipCountryCodeJsonKeys != null)
        {
            for (int i = 0; i < _ipCountryCodeJsonKeys.Count; i++)
            {
                string key = _ipCountryCodeJsonKeys[i];
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                string value = NetworkJson.GetString(json, key);
                if (!string.IsNullOrWhiteSpace(value))
                    return NormalizeRegionCode(value);
            }
        }

        return "";
    }

    private void HandleIpLookupFailed(string reason)
    {
        LookupState = RegionLookupState.Failed;
        _ipLookupRoutine = null;

        if (string.IsNullOrWhiteSpace(LastResolvedRegionCode) && _useSystemRegionFallbackWhenIpFails)
            LastResolvedRegionCode = ResolveSystemRegionCode();

        ThrottledAppLogger.Warn(
            nameof(RegionAccessGate) + ".IpLookupFailed",
            AppLogCategory.Network,
            nameof(RegionAccessGate),
            nameof(HandleIpLookupFailed),
            "IP geolocation failed.",
            LogMetadata.Of(
                "reason", reason,
                "fallbackRegion", LastResolvedRegionCode,
                "mode", _mode));
        Evaluate();
        _regionLookupFailed.Invoke();
    }

    private bool TryReadCachedIpRegion(out string regionCode)
    {
        regionCode = "";

        if (!_useCachedIpRegion)
            return false;

        if (_mode == RegionAccessMode.Enforce && !HasDeveloperBypass())
            return false;

        string cached = NormalizeRegionCode(LocalSecurePrefs.GetString(IpRegionCodePrefsKey, GetRegionCachePurpose("code"), ""));
        if (string.IsNullOrEmpty(cached))
            return false;

        if (_ipRegionCacheHours > 0f)
        {
            string rawTicks = LocalSecurePrefs.GetString(IpRegionTimePrefsKey, GetRegionCachePurpose("time"), "");
            if (!long.TryParse(rawTicks, out long ticks))
                return false;

            TimeSpan age = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
            if (age.TotalHours > _ipRegionCacheHours)
                return false;
        }

        regionCode = cached;
        return true;
    }

    private void CacheIpRegion(string regionCode)
    {
        regionCode = NormalizeRegionCode(regionCode);
        if (!_useCachedIpRegion || string.IsNullOrEmpty(regionCode))
            return;

        if (_mode == RegionAccessMode.Enforce && !HasDeveloperBypass())
            return;

        LocalSecurePrefs.SetString(IpRegionCodePrefsKey, GetRegionCachePurpose("code"), regionCode);
        LocalSecurePrefs.SetString(IpRegionTimePrefsKey, GetRegionCachePurpose("time"), DateTime.UtcNow.Ticks.ToString());
    }

    public static string NormalizeRegionCode(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToUpperInvariant();
    }

    private static string GetRegionCachePurpose(string suffix)
    {
        return LocalSaveSecurity.RegionCachePurpose + ":" + SaveDataSanitizer.SanitizeIdentifier(suffix);
    }

    private static RegionAccessGate FindCurrentGate()
    {
        if (_instance != null)
            return _instance;

        _instance = FindObjectOfType<RegionAccessGate>(true);
        return _instance;
    }

    private static bool IsDeveloperBuildContext()
    {
        return Application.isEditor || Debug.isDebugBuild || IsDevelopmentBuild();
    }

    private bool ShouldBlockForUnavailableIpLookup()
    {
        if (!_blockWhenIpLookupUnavailable ||
            _source != RegionAccessSource.IpGeolocation ||
            _mode != RegionAccessMode.Enforce)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(LastResolvedRegionCode) ||
            TryReadCachedIpRegion(out _))
        {
            return false;
        }

        return LookupState == RegionLookupState.NotStarted ||
               LookupState == RegionLookupState.Resolving ||
               LookupState == RegionLookupState.Failed;
    }
}
