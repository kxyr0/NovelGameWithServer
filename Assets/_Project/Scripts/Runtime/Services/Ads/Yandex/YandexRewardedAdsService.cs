#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
#define YANDEX_ADS_RUNTIME
using YandexMobileAds;
using YandexMobileAds.Base;
#endif

using System;
using UnityEngine;

[DefaultExecutionOrder(-8900)]
[DisallowMultipleComponent]
public sealed partial class YandexRewardedAdsService : MonoBehaviour, IRewardedAdService
{
    [SerializeField] private YandexAdsConfig _config;

    private bool _initialized;
    private bool _loading;
    private bool _showing;
    private bool _rewardReceived;
    private bool _callbackInvoked;
    private bool _editorMockReady;
    private string _activePlacementId = "";
    private Action<AdRewardResult> _activeCallback;
    private Coroutine _reloadRoutine;

#if YANDEX_ADS_RUNTIME
    private RewardedAdLoader _loader;
    private RewardedAd _rewardedAd;
#endif

    public static YandexRewardedAdsService Instance { get; private set; }
    public bool IsInitialized => _initialized;

    public void Configure(YandexAdsConfig config)
    {
        if (_initialized)
            return;
        _config = config;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveConfig();
        if (_config != null && _config.InitializeOnStart)
            Initialize();
    }

    private void OnDestroy()
    {
        ShutdownYandex();
        if (Instance == this)
            Instance = null;
    }

    public void Initialize()
    {
        YandexAdsConfig config = ResolveConfig();
        if (_initialized || config == null || !config.AdsEnabled)
            return;

#if YANDEX_ADS_RUNTIME
        ApplyPrivacySettings(config);
        _loader = new RewardedAdLoader();
        _loader.OnAdLoaded += HandleAdLoaded;
        _loader.OnAdFailedToLoad += HandleAdLoadFailed;
        _initialized = true;
        LoadRewarded("");
#elif UNITY_EDITOR
        _initialized = config.EnableEditorMock;
        _editorMockReady = _initialized;
        if (!_initialized)
            Debug.Log("[YandexAds] Editor mock is disabled. Test rewarded ads on a device.");
#else
        Debug.LogWarning("[YandexAds] Rewarded ads are supported only on Android and iOS.");
#endif
    }

    public bool IsRewardedReady(string placementId)
    {
        if (!_initialized || _loading || _showing)
            return false;
#if YANDEX_ADS_RUNTIME
        return _rewardedAd != null;
#else
        return _editorMockReady;
#endif
    }

    public void LoadRewarded(string placementId)
    {
        if (!_initialized)
            Initialize();
        if (!_initialized || _loading || _showing || IsRewardedReady(placementId))
            return;

        YandexAdsConfig config = ResolveConfig();
        if (config == null || !config.HasRewardedAdUnitId)
        {
            Debug.LogWarning("[YandexAds] Rewarded Ad Unit ID is empty.");
            return;
        }

#if YANDEX_ADS_RUNTIME
        _loading = true;
        var request = new AdRequestConfiguration.Builder(config.RewardedAdUnitId).Build();
        _loader.LoadAd(request);
#elif UNITY_EDITOR
        _editorMockReady = config.EnableEditorMock;
#endif
    }

    public void ShowRewarded(string placementId, Action<AdRewardResult> callback)
    {
        string safePlacement = string.IsNullOrWhiteSpace(placementId) ? "yandex_rewarded" : placementId.Trim();
        if (_showing)
        {
            callback?.Invoke(AdRewardResult.Create(AdRewardStatus.AlreadyShowing, safePlacement));
            return;
        }
        if (!IsRewardedReady(safePlacement))
        {
            callback?.Invoke(AdRewardResult.Create(AdRewardStatus.NotReady, safePlacement, "Реклама ещё загружается."));
            LoadRewarded(safePlacement);
            return;
        }

        BeginShow(safePlacement, callback);
#if YANDEX_ADS_RUNTIME
        _rewardedAd.Show();
#elif UNITY_EDITOR
        StartCoroutine(ShowEditorMock());
#endif
    }

    private void BeginShow(string placementId, Action<AdRewardResult> callback)
    {
        _showing = true;
        _rewardReceived = false;
        _callbackInvoked = false;
        _activePlacementId = placementId;
        _activeCallback = callback;
#if UNITY_EDITOR
        _editorMockReady = false;
#endif
    }

    private YandexAdsConfig ResolveConfig()
    {
        if (_config == null)
            _config = Resources.Load<YandexAdsConfig>(YandexAdsConfig.DefaultResourcesPath);
        return _config;
    }
}
