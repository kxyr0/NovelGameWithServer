#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
#define LEVELPLAY_RUNTIME
using Unity.Services.LevelPlay;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-9000)]
public sealed class LevelPlayAdsService : AdsServiceBehaviour
{
    private const string Component = nameof(LevelPlayAdsService);

    [SerializeField] private AdsConfig _config;
    [SerializeField] private AdsEntitlementProvider _entitlementProvider;
    [SerializeField] private bool _initializeOnAwake = true;

    private readonly Dictionary<string, bool> _mockRewardedReady = new Dictionary<string, bool>(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _mockInterstitialReady = new Dictionary<string, bool>(StringComparer.Ordinal);
    private InterstitialAdFrequencyLimiter _frequencyLimiter;
    private bool _initializationStarted;
    private bool _initialized;
    private bool _unsupportedPlatform;
    private bool _quitting;
    private string _currentRewardedPlacementId = "";
    private Action<AdRewardResult> _currentRewardedCallback;
    private bool _rewardedCallbackInvoked;
    private bool _rewardedRewardGranted;
    private Coroutine _rewardedCloseGraceRoutine;
    private string _currentInterstitialPlacementId = "";
    private string _currentBannerPlacementId = "";

#if LEVELPLAY_RUNTIME
    private readonly Dictionary<string, ILevelPlayRewardedAd> _rewardedAds = new Dictionary<string, ILevelPlayRewardedAd>(StringComparer.Ordinal);
    private readonly Dictionary<string, ILevelPlayInterstitialAd> _interstitialAds = new Dictionary<string, ILevelPlayInterstitialAd>(StringComparer.Ordinal);
    private LevelPlayBannerAd _currentBannerAd;
    private bool _showCurrentBannerAfterLoad;
#endif

    public override event Action<bool> InitializationChanged;
    public override event Action<AdRewardResult> RewardedAdCompleted;
    public override event Action<string, string> InterstitialSkipped;
    public override event Action<string> InterstitialShown;
    public override event Action<string> BannerShown;
    public override event Action<string> BannerHidden;

    public override bool IsInitialized => _initialized;

    public void Configure(AdsConfig config, AdsEntitlementProvider entitlementProvider = null)
    {
        _config = config;
        if (entitlementProvider != null)
            _entitlementProvider = entitlementProvider;
    }

    protected override void Awake()
    {
        base.Awake();
        _frequencyLimiter = new InterstitialAdFrequencyLimiter(() => Time.realtimeSinceStartup);
        ResolveConfig();

        if (_initializeOnAwake && (_config == null || _config.InitializeOnStart))
            Initialize();
    }

    private void OnApplicationQuit()
    {
        _quitting = true;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        UnsubscribeLevelPlayInitEvents();
        DestroyLevelPlayAds();
    }

    public override void Initialize()
    {
        AdsConfig config = ResolveConfig();
        if (config == null)
        {
            LogWarning(nameof(Initialize), "[Ads] AdsConfig не найден. Инициализация рекламы пропущена.", null);
            return;
        }

        if (!config.AdsEnabled)
        {
            LogInfo(nameof(Initialize), "[Ads] Реклама отключена в AdsConfig.", null);
            return;
        }

        if (_initialized || _initializationStarted)
            return;

#if UNITY_EDITOR
        InitializeEditorMock(config);
#elif LEVELPLAY_RUNTIME
        InitializeLevelPlay(config);
#else
        InitializeUnsupportedStub();
#endif
    }

    public override bool IsRewardedReady(string placementId)
    {
        AdsPlacementConfig placement = ResolveRewardedPlacement(placementId);
        if (!CanCheckAd(placement, AdsAdType.Rewarded))
            return false;

#if LEVELPLAY_RUNTIME
        if (!_rewardedAds.TryGetValue(placement.ConfigKey, out ILevelPlayRewardedAd ad) || ad == null)
            return false;

        return ad.IsAdReady() && !LevelPlayRewardedAd.IsPlacementCapped(placement.PlacementName);
#else
        return _mockRewardedReady.TryGetValue(placement.ConfigKey, out bool ready) && ready;
#endif
    }

    public override bool IsInterstitialReady(string placementId)
    {
        AdsPlacementConfig placement = ResolveInterstitialPlacement(placementId);
        if (!CanCheckAd(placement, AdsAdType.Interstitial))
            return false;

#if LEVELPLAY_RUNTIME
        if (!_interstitialAds.TryGetValue(placement.ConfigKey, out ILevelPlayInterstitialAd ad) || ad == null)
            return false;

        return ad.IsAdReady() && !LevelPlayInterstitialAd.IsPlacementCapped(placement.PlacementName);
#else
        return _mockInterstitialReady.TryGetValue(placement.ConfigKey, out bool ready) && ready;
#endif
    }

    public override void LoadRewarded(string placementId)
    {
        AdsPlacementConfig placement = ResolveRewardedPlacement(placementId);
        if (!CanLoadAd(placement, AdsAdType.Rewarded, nameof(LoadRewarded)))
            return;

#if LEVELPLAY_RUNTIME
        if (_rewardedAds.TryGetValue(placement.ConfigKey, out ILevelPlayRewardedAd ad) && ad != null)
        {
            ad.LoadAd();
            LogDebug(nameof(LoadRewarded), "[Ads] Запрошена загрузка rewarded.", PlacementMetadata(placement));
        }
#else
        _mockRewardedReady[placement.ConfigKey] = true;
        LogDebug(nameof(LoadRewarded), "[Ads] Editor mock rewarded готов.", PlacementMetadata(placement));
#endif
    }

    public override void ShowRewarded(string placementId, Action<AdRewardResult> callback)
    {
        AdsPlacementConfig placement = ResolveRewardedPlacement(placementId);
        AdRewardResult blockedResult = GetRewardedBlockResult(placement, placementId);
        if (blockedResult != null)
        {
            CompleteBlockedRewarded(callback, blockedResult);
            return;
        }

        StartRewardedShow(placement, callback);

#if LEVELPLAY_RUNTIME
        try
        {
            _rewardedAds[placement.ConfigKey].ShowAd(placement.PlacementName);
            LogInfo(nameof(ShowRewarded), "[Ads] Rewarded показан.", PlacementMetadata(placement));
        }
        catch (Exception exception)
        {
            HandleRewardedDisplayFailed(placement.ConfigKey, null, exception);
        }
#else
        StartCoroutine(ShowMockRewarded(placement));
#endif
    }

    public override void LoadInterstitial(string placementId)
    {
        AdsPlacementConfig placement = ResolveInterstitialPlacement(placementId);
        if (!CanLoadAd(placement, AdsAdType.Interstitial, nameof(LoadInterstitial)))
            return;

#if LEVELPLAY_RUNTIME
        if (_interstitialAds.TryGetValue(placement.ConfigKey, out ILevelPlayInterstitialAd ad) && ad != null)
        {
            ad.LoadAd();
            LogDebug(nameof(LoadInterstitial), "[Ads] Запрошена загрузка interstitial.", PlacementMetadata(placement));
        }
#else
        _mockInterstitialReady[placement.ConfigKey] = true;
        LogDebug(nameof(LoadInterstitial), "[Ads] Editor mock interstitial готов.", PlacementMetadata(placement));
#endif
    }

    public override bool TryShowInterstitial(string placementId, string reason)
    {
        AdsPlacementConfig placement = ResolveInterstitialPlacement(placementId);
        bool ready = placement != null && IsInterstitialReady(placement.ConfigKey);
        InterstitialAdShowDecision decision = BuildInterstitialDecision(placement, ready);
        if (!decision.Allowed)
        {
            string key = placement != null ? placement.ConfigKey : placementId;
            LogInterstitialSkipped(key, reason, decision.Reason);
            InterstitialSkipped?.Invoke(key ?? "", decision.Reason);
            return false;
        }

        _frequencyLimiter.MarkAdShowing();
        _currentInterstitialPlacementId = placement.ConfigKey;

#if LEVELPLAY_RUNTIME
        try
        {
            _interstitialAds[placement.ConfigKey].ShowAd(placement.PlacementName);
            LogInfo(nameof(TryShowInterstitial), "[Ads] Interstitial запрошен к показу.", PlacementMetadata(placement, "reason", reason));
            return true;
        }
        catch (Exception exception)
        {
            HandleInterstitialDisplayFailed(placement.ConfigKey, null, exception);
            return false;
        }
#else
        StartCoroutine(ShowMockInterstitial(placement, reason));
        return true;
#endif
    }

    public override void ShowBanner(string placementId)
    {
        AdsBannerPlacementConfig placement = ResolveBannerPlacement(placementId);
        if (!CanShowBannerPlacement(placement))
            return;

#if LEVELPLAY_RUNTIME
        CreateAndLoadBanner(placement);
#else
        _currentBannerPlacementId = placement.ConfigKey;
        LogInfo(nameof(ShowBanner), "[Ads] Editor mock banner показан.", BannerMetadata(placement));
        BannerShown?.Invoke(placement.ConfigKey);
#endif
    }

    public override void HideBanner()
    {
#if LEVELPLAY_RUNTIME
        _currentBannerAd?.HideAd();
#endif
        if (!string.IsNullOrEmpty(_currentBannerPlacementId))
        {
            LogInfo(nameof(HideBanner), "[Ads] Banner скрыт.", LogMetadata.Of("placementId", _currentBannerPlacementId));
            BannerHidden?.Invoke(_currentBannerPlacementId);
        }
    }

    public override void DestroyBanner()
    {
#if LEVELPLAY_RUNTIME
        if (_currentBannerAd != null)
        {
            _currentBannerAd.DestroyAd();
            _currentBannerAd = null;
        }
#endif
        if (!string.IsNullOrEmpty(_currentBannerPlacementId))
            LogInfo(nameof(DestroyBanner), "[Ads] Banner уничтожен.", LogMetadata.Of("placementId", _currentBannerPlacementId));

        _currentBannerPlacementId = "";
#if LEVELPLAY_RUNTIME
        _showCurrentBannerAfterLoad = false;
#endif
    }

    public void LaunchLevelPlayTestSuite()
    {
#if LEVELPLAY_RUNTIME
        if (!_initialized)
        {
            LogWarning(nameof(LaunchLevelPlayTestSuite), "[Ads] Test Suite нельзя открыть до инициализации LevelPlay.", null);
            return;
        }

        LevelPlay.LaunchTestSuite();
#else
        LogInfo(nameof(LaunchLevelPlayTestSuite), "[Ads] Test Suite доступен только в player-сборке с LevelPlay.", null);
#endif
    }

    private AdsConfig ResolveConfig()
    {
        if (_config != null)
            return _config;

        _config = Resources.Load<AdsConfig>(AdsConfig.DefaultResourcesPath);
        return _config;
    }

#if UNITY_EDITOR
    private void InitializeEditorMock(AdsConfig config)
    {
        _initializationStarted = true;
        _initialized = true;
        FillMockAvailability(config);

        LogInfo(nameof(InitializeEditorMock), "[Ads] Editor mock рекламы инициализирован.", null);
        InitializationChanged?.Invoke(true);
    }
#endif

#if LEVELPLAY_RUNTIME
    private void InitializeLevelPlay(AdsConfig config)
    {
        string appKey = config.GetAppKey();
        if (string.IsNullOrWhiteSpace(appKey) || appKey.StartsWith("YOUR_", StringComparison.Ordinal))
        {
            LogWarning(nameof(InitializeLevelPlay), "[Ads] App Key LevelPlay не настроен.", null);
            return;
        }

        _initializationStarted = true;
        LevelPlay.OnInitSuccess += HandleLevelPlayInitSuccess;
        LevelPlay.OnInitFailed += HandleLevelPlayInitFailed;

        if (config.TestMode && config.EnableIntegrationTestSuite)
            LevelPlay.SetMetaData("is_test_suite", "enable");

        LevelPlay.SetAdaptersDebug(config.DebugLogging);
        LevelPlay.Init(appKey);

        LogInfo(nameof(InitializeLevelPlay), "[Ads] Инициализация LevelPlay запущена.", LogMetadata.Of("testMode", config.TestMode));
    }

    private void HandleLevelPlayInitSuccess(LevelPlayConfiguration configuration)
    {
        _initialized = true;
        LogInfo(nameof(HandleLevelPlayInitSuccess), "[Ads] LevelPlay инициализирован.", LogMetadata.Of("configuration", configuration));
        CreateLevelPlayAds();
        LoadInitialAds();

        if (_config != null && _config.ValidateIntegrationOnInitialize)
            LevelPlay.ValidateIntegration();

        InitializationChanged?.Invoke(true);
    }

    private void HandleLevelPlayInitFailed(LevelPlayInitError error)
    {
        _initialized = false;
        _initializationStarted = false;
        LogWarning(nameof(HandleLevelPlayInitFailed), "[Ads] Ошибка инициализации LevelPlay.", LogMetadata.Of("error", error));
        InitializationChanged?.Invoke(false);
    }
#endif

    private void InitializeUnsupportedStub()
    {
        _unsupportedPlatform = true;
        _initializationStarted = true;
        LogInfo(nameof(InitializeUnsupportedStub), "[Ads] Платформа не поддерживает runtime LevelPlay. Реклама будет отключена.", null);
        InitializationChanged?.Invoke(false);
    }

    private void FillMockAvailability(AdsConfig config)
    {
        _mockRewardedReady.Clear();
        _mockInterstitialReady.Clear();

        foreach (AdsPlacementConfig placement in config.RewardedPlacements)
        {
            if (placement != null)
                _mockRewardedReady[placement.ConfigKey] = config.EditorMockAdsStartReady;
        }

        foreach (AdsPlacementConfig placement in config.InterstitialPlacements)
        {
            if (placement != null)
                _mockInterstitialReady[placement.ConfigKey] = config.EditorMockAdsStartReady;
        }
    }

    private void LoadInitialAds()
    {
        AdsConfig config = ResolveConfig();
        if (config == null)
            return;

        foreach (AdsPlacementConfig placement in config.RewardedPlacements)
        {
            if (placement != null)
                LoadRewarded(placement.ConfigKey);
        }

        foreach (AdsPlacementConfig placement in config.InterstitialPlacements)
        {
            if (placement != null)
                LoadInterstitial(placement.ConfigKey);
        }
    }

#if LEVELPLAY_RUNTIME
    private void CreateLevelPlayAds()
    {
        AdsConfig config = ResolveConfig();
        if (config == null)
            return;

        foreach (AdsPlacementConfig placement in config.RewardedPlacements)
            CreateRewardedAd(placement);

        foreach (AdsPlacementConfig placement in config.InterstitialPlacements)
            CreateInterstitialAd(placement);
    }

    private void CreateRewardedAd(AdsPlacementConfig placement)
    {
        if (placement == null || !placement.HasAdUnitForCurrentPlatform || _rewardedAds.ContainsKey(placement.ConfigKey))
            return;

        var ad = new LevelPlayRewardedAd(placement.GetAdUnitId());
        string key = placement.ConfigKey;
        ad.OnAdLoaded += info => HandleRewardedLoaded(key, info);
        ad.OnAdLoadFailed += error => HandleRewardedLoadFailed(key, error);
        ad.OnAdDisplayed += info => HandleRewardedDisplayed(key, info);
        ad.OnAdDisplayFailed += (info, error) => HandleRewardedDisplayFailed(key, error, null);
        ad.OnAdRewarded += (info, reward) => HandleRewardedRewarded(key, info, reward);
        ad.OnAdClosed += info => HandleRewardedClosed(key, info);
        ad.OnAdClicked += info => LogDebug("RewardedClicked", "[Ads] Rewarded clicked.", PlacementMetadata(placement));
        ad.OnAdInfoChanged += info => LogDebug("RewardedInfoChanged", "[Ads] Rewarded info changed.", PlacementMetadata(placement, "info", info));
        _rewardedAds[key] = ad;
    }

    private void CreateInterstitialAd(AdsPlacementConfig placement)
    {
        if (placement == null || !placement.HasAdUnitForCurrentPlatform || _interstitialAds.ContainsKey(placement.ConfigKey))
            return;

        var ad = new LevelPlayInterstitialAd(placement.GetAdUnitId());
        string key = placement.ConfigKey;
        ad.OnAdLoaded += info => HandleInterstitialLoaded(key, info);
        ad.OnAdLoadFailed += error => HandleInterstitialLoadFailed(key, error);
        ad.OnAdDisplayed += info => HandleInterstitialDisplayed(key, info);
        ad.OnAdDisplayFailed += (info, error) => HandleInterstitialDisplayFailed(key, error, null);
        ad.OnAdClosed += info => HandleInterstitialClosed(key, info);
        ad.OnAdClicked += info => LogDebug("InterstitialClicked", "[Ads] Interstitial clicked.", LogMetadata.Of("placementId", key));
        ad.OnAdInfoChanged += info => LogDebug("InterstitialInfoChanged", "[Ads] Interstitial info changed.", LogMetadata.Of("placementId", key, "info", info));
        _interstitialAds[key] = ad;
    }
#endif

    private void StartRewardedShow(AdsPlacementConfig placement, Action<AdRewardResult> callback)
    {
        StopRewardedCloseGraceRoutine();
        _frequencyLimiter.MarkAdShowing();
        _currentRewardedPlacementId = placement.ConfigKey;
        _currentRewardedCallback = callback;
        _rewardedCallbackInvoked = false;
        _rewardedRewardGranted = false;

#if !LEVELPLAY_RUNTIME
        _mockRewardedReady[placement.ConfigKey] = false;
#endif
    }

    private AdRewardResult GetRewardedBlockResult(AdsPlacementConfig placement, string requestedPlacementId)
    {
        string resultPlacementId = placement != null ? placement.ConfigKey : requestedPlacementId;
        AdsConfig config = ResolveConfig();
        if (config == null || !config.AdsEnabled)
            return AdRewardResult.Create(AdRewardStatus.AdsDisabled, resultPlacementId, "Реклама отключена.");

        if (_unsupportedPlatform)
            return AdRewardResult.Create(AdRewardStatus.UnsupportedPlatform, resultPlacementId, "Платформа не поддерживает рекламу.");

        if (!EntitlementAllows(AdsAdType.Rewarded))
            return AdRewardResult.Create(AdRewardStatus.Skipped, resultPlacementId, "Реклама отключена для пользователя.");

        if (placement == null)
            return AdRewardResult.Create(AdRewardStatus.FailedToLoad, resultPlacementId, "Плейсмент rewarded не найден.");

        if (!_initialized)
            return AdRewardResult.Create(AdRewardStatus.NotInitialized, resultPlacementId, "Сервис рекламы не инициализирован.");

        if (_frequencyLimiter.IsAdShowing)
            return AdRewardResult.Create(AdRewardStatus.AlreadyShowing, resultPlacementId, "Уже показывается другая реклама.");

        if (!IsRewardedReady(placement.ConfigKey))
            return AdRewardResult.Create(AdRewardStatus.NotReady, resultPlacementId, "Rewarded еще не готов.");

        return null;
    }

    private InterstitialAdShowDecision BuildInterstitialDecision(AdsPlacementConfig placement, bool ready)
    {
        AdsConfig config = ResolveConfig();
        bool adsEnabled = config != null && config.AdsEnabled;
        bool entitlementAllowsAds = EntitlementAllows(AdsAdType.Interstitial);
        bool initialized = _initialized && !_unsupportedPlatform && placement != null;
        InterstitialAdFrequencyPolicy policy = config != null
            ? config.BuildInterstitialPolicy()
            : new InterstitialAdFrequencyPolicy(0f, 0f, 0f);

        return _frequencyLimiter.CanShow(policy, adsEnabled, entitlementAllowsAds, initialized, ready);
    }

    private bool CanCheckAd(AdsPlacementConfig placement, AdsAdType adType)
    {
        AdsConfig config = ResolveConfig();
        return config != null &&
               config.AdsEnabled &&
               _initialized &&
               placement != null &&
               placement.HasAdUnitForCurrentPlatform &&
               EntitlementAllows(adType);
    }

    private bool CanLoadAd(AdsPlacementConfig placement, AdsAdType adType, string operation)
    {
        AdsConfig config = ResolveConfig();
        if (config == null || !config.AdsEnabled || !_initialized || _unsupportedPlatform)
            return false;

        if (!EntitlementAllows(adType))
        {
            LogDebug(operation, "[Ads] Загрузка рекламы пропущена: реклама отключена для пользователя.", null);
            return false;
        }

        if (placement != null && placement.HasAdUnitForCurrentPlatform)
            return true;

        LogWarning(operation, "[Ads] Плейсмент рекламы не настроен.", null);
        return false;
    }

    private bool CanShowBannerPlacement(AdsBannerPlacementConfig placement)
    {
        AdsConfig config = ResolveConfig();
        if (config == null || !config.AdsEnabled)
            return false;

        if (!_initialized || _unsupportedPlatform)
        {
            LogDebug(nameof(ShowBanner), "[Ads] Banner пропущен: сервис рекламы не инициализирован.", null);
            return false;
        }

        if (!EntitlementAllows(AdsAdType.Banner))
        {
            LogDebug(nameof(ShowBanner), "[Ads] Banner пропущен: реклама отключена для пользователя.", null);
            return false;
        }

        if (placement != null && placement.HasAdUnitForCurrentPlatform)
            return true;

        LogWarning(nameof(ShowBanner), "[Ads] Banner placement не настроен.", null);
        return false;
    }

    private bool EntitlementAllows(AdsAdType adType)
    {
        return _entitlementProvider == null || _entitlementProvider.CanShow(adType);
    }

    private AdsPlacementConfig ResolveRewardedPlacement(string placementId)
    {
        AdsConfig config = ResolveConfig();
        return config != null ? config.FindRewardedPlacement(placementId) : null;
    }

    private AdsPlacementConfig ResolveInterstitialPlacement(string placementId)
    {
        AdsConfig config = ResolveConfig();
        return config != null ? config.FindInterstitialPlacement(placementId) : null;
    }

    private AdsBannerPlacementConfig ResolveBannerPlacement(string placementId)
    {
        AdsConfig config = ResolveConfig();
        return config != null ? config.FindBannerPlacement(placementId) : null;
    }

#if LEVELPLAY_RUNTIME
    private void HandleRewardedLoaded(string key, LevelPlayAdInfo info)
    {
        LogInfo("RewardedLoaded", "[Ads] Rewarded загружен.", LogMetadata.Of("placementId", key, "info", info));
    }

    private void HandleRewardedLoadFailed(string key, LevelPlayAdError error)
    {
        LogWarning("RewardedLoadFailed", "[Ads] Rewarded не загрузился.", LogMetadata.Of("placementId", key, "error", error));
        ReloadRewardedLater(key);
    }

    private void HandleRewardedDisplayed(string key, LevelPlayAdInfo info)
    {
        LogInfo("RewardedDisplayed", "[Ads] Rewarded открыт.", LogMetadata.Of("placementId", key, "info", info));
    }

    private void HandleRewardedRewarded(string key, LevelPlayAdInfo info, LevelPlayReward reward)
    {
        if (!string.Equals(_currentRewardedPlacementId, key, StringComparison.Ordinal))
            LogWarning("RewardedUnexpectedReward", "[Ads] Получена награда rewarded для неактивного плейсмента.", LogMetadata.Of("placementId", key));

        if (_rewardedRewardGranted)
        {
            LogWarning("RewardedDuplicateReward", "[Ads] Повторная награда rewarded проигнорирована.", LogMetadata.Of("placementId", key), true);
            return;
        }

        _rewardedRewardGranted = true;
        _frequencyLimiter.MarkRewardedFinished();
        CompleteRewarded(AdRewardResult.Create(
            AdRewardStatus.Success,
            key,
            "",
            reward != null ? reward.Name : "",
            reward != null ? reward.Amount : 0));

        LogInfo("RewardedGranted", "[Ads] Награда rewarded подтверждена SDK.", LogMetadata.Of("placementId", key, "info", info, "reward", reward));
    }

    private void HandleRewardedClosed(string key, LevelPlayAdInfo info)
    {
        _frequencyLimiter.MarkRewardedFinished();
        _frequencyLimiter.MarkAdClosed();
        LogInfo("RewardedClosed", "[Ads] Rewarded закрыт.", LogMetadata.Of("placementId", key, "info", info, "rewardGranted", _rewardedRewardGranted));
        ReloadRewardedLater(key);

        if (!_rewardedCallbackInvoked)
            _rewardedCloseGraceRoutine = StartCoroutine(CompleteRewardedWithoutRewardAfterGrace(key));
        else
            ClearRewardedShowState();
    }

    private void HandleRewardedDisplayFailed(string key, LevelPlayAdError error, Exception exception)
    {
        _frequencyLimiter.MarkAdClosed();
        LogWarning("RewardedDisplayFailed", "[Ads] Rewarded не показался.", LogMetadata.Of("placementId", key, "error", error, "exception", exception != null ? exception.Message : ""));
        CompleteRewarded(AdRewardResult.Create(AdRewardStatus.DisplayFailed, key, "Rewarded не показался."));
        ReloadRewardedLater(key);
        ClearRewardedShowState(false);
    }

    private void HandleInterstitialLoaded(string key, LevelPlayAdInfo info)
    {
        LogInfo("InterstitialLoaded", "[Ads] Interstitial загружен.", LogMetadata.Of("placementId", key, "info", info));
    }

    private void HandleInterstitialLoadFailed(string key, LevelPlayAdError error)
    {
        LogWarning("InterstitialLoadFailed", "[Ads] Interstitial не загрузился.", LogMetadata.Of("placementId", key, "error", error));
        ReloadInterstitialLater(key);
    }

    private void HandleInterstitialDisplayed(string key, LevelPlayAdInfo info)
    {
        _frequencyLimiter.MarkInterstitialShown();
        LogInfo("InterstitialDisplayed", "[Ads] Interstitial открыт.", LogMetadata.Of("placementId", key, "info", info));
        InterstitialShown?.Invoke(key);
    }

    private void HandleInterstitialClosed(string key, LevelPlayAdInfo info)
    {
        _frequencyLimiter.MarkAdClosed();
        _currentInterstitialPlacementId = "";
        LogInfo("InterstitialClosed", "[Ads] Interstitial закрыт.", LogMetadata.Of("placementId", key, "info", info));
        ReloadInterstitialLater(key);
    }

    private void HandleInterstitialDisplayFailed(string key, LevelPlayAdError error, Exception exception)
    {
        _frequencyLimiter.MarkAdClosed();
        _currentInterstitialPlacementId = "";
        LogWarning("InterstitialDisplayFailed", "[Ads] Interstitial не показался.", LogMetadata.Of("placementId", key, "error", error, "exception", exception != null ? exception.Message : ""));
        ReloadInterstitialLater(key);
    }
#endif

    private IEnumerator CompleteRewardedWithoutRewardAfterGrace(string key)
    {
        AdsConfig config = ResolveConfig();
        float delay = config != null ? config.RewardedCloseGraceSeconds : 1f;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);
        else
            yield return null;

        if (!_rewardedCallbackInvoked)
            CompleteRewarded(AdRewardResult.Create(AdRewardStatus.ClosedWithoutReward, key, "Реклама закрыта без награды."));

        ClearRewardedShowState();
    }

    private IEnumerator ShowMockRewarded(AdsPlacementConfig placement)
    {
        AdsConfig config = ResolveConfig();
        float delay = config != null ? config.EditorMockDelaySeconds : 0.25f;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        bool success = config == null || config.EditorMockRewardedSucceeds;
        if (success)
        {
            _rewardedRewardGranted = true;
            _frequencyLimiter.MarkRewardedFinished();
            CompleteRewarded(AdRewardResult.Create(
                AdRewardStatus.Success,
                placement.ConfigKey,
                "",
                config != null ? config.EditorMockRewardName : "soft_currency",
                config != null ? config.EditorMockRewardAmount : 1));
        }
        else
        {
            CompleteRewarded(AdRewardResult.Create(
                AdRewardStatus.ClosedWithoutReward,
                placement.ConfigKey,
                "Editor mock: награда не выдана."));
        }

        bool rewardGranted = _rewardedRewardGranted;
        _frequencyLimiter.MarkAdClosed();
        _mockRewardedReady[placement.ConfigKey] = true;
        ClearRewardedShowState();
        LogInfo(nameof(ShowMockRewarded), "[Ads] Editor mock rewarded завершен.", PlacementMetadata(placement, "success", success, "rewardGranted", rewardGranted));
    }

    private IEnumerator ShowMockInterstitial(AdsPlacementConfig placement, string reason)
    {
        _mockInterstitialReady[placement.ConfigKey] = false;
        _frequencyLimiter.MarkInterstitialShown();
        InterstitialShown?.Invoke(placement.ConfigKey);
        LogInfo(nameof(ShowMockInterstitial), "[Ads] Editor mock interstitial показан.", PlacementMetadata(placement, "reason", reason));

        AdsConfig config = ResolveConfig();
        float delay = config != null ? config.EditorMockDelaySeconds : 0.25f;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        _frequencyLimiter.MarkAdClosed();
        _mockInterstitialReady[placement.ConfigKey] = true;
        _currentInterstitialPlacementId = "";
        LogInfo(nameof(ShowMockInterstitial), "[Ads] Editor mock interstitial закрыт.", PlacementMetadata(placement));
    }

    private void CompleteRewarded(AdRewardResult result)
    {
        if (_rewardedCallbackInvoked)
            return;

        _rewardedCallbackInvoked = true;
        Action<AdRewardResult> callback = _currentRewardedCallback;
        InvokeRewardedCallbacks(result, callback);

        LogInfo(
            nameof(CompleteRewarded),
            result.Success ? "[Ads] Rewarded завершен с наградой." : "[Ads] Rewarded завершен без награды.",
            LogMetadata.Of(
                "placementId", result.PlacementId,
                "status", result.Status.ToString(),
                "rewardName", result.RewardName,
                "rewardAmount", result.RewardAmount,
                "error", result.ErrorMessage));
    }

    private void CompleteBlockedRewarded(Action<AdRewardResult> callback, AdRewardResult result)
    {
        InvokeRewardedCallbacks(result, callback);
        LogInfo(
            nameof(CompleteBlockedRewarded),
            "[Ads] Rewarded не был показан.",
            LogMetadata.Of(
                "placementId", result.PlacementId,
                "status", result.Status.ToString(),
                "error", result.ErrorMessage));
    }

    private void InvokeRewardedCallbacks(AdRewardResult result, Action<AdRewardResult> callback)
    {
        try
        {
            RewardedAdCompleted?.Invoke(result);
        }
        catch (Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.Ads,
                Component,
                nameof(InvokeRewardedCallbacks),
                "[Ads] Ошибка обработчика RewardedAdCompleted.",
                exception,
                LogMetadata.Of("placementId", result != null ? result.PlacementId : ""),
                recoverable: true);
        }

        try
        {
            callback?.Invoke(result);
        }
        catch (Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.Ads,
                Component,
                nameof(InvokeRewardedCallbacks),
                "[Ads] Ошибка callback rewarded.",
                exception,
                LogMetadata.Of("placementId", result != null ? result.PlacementId : ""),
                recoverable: true);
        }
    }

    private void ClearRewardedShowState(bool stopGraceRoutine = true)
    {
        if (stopGraceRoutine)
            StopRewardedCloseGraceRoutine();
        else
            _rewardedCloseGraceRoutine = null;

        _currentRewardedPlacementId = "";
        _currentRewardedCallback = null;
        _rewardedCallbackInvoked = false;
        _rewardedRewardGranted = false;
    }

    private void StopRewardedCloseGraceRoutine()
    {
        if (_rewardedCloseGraceRoutine == null)
            return;

        StopCoroutine(_rewardedCloseGraceRoutine);
        _rewardedCloseGraceRoutine = null;
    }

    private void ReloadRewardedLater(string key)
    {
        if (!_quitting && isActiveAndEnabled)
            StartCoroutine(ReloadRewardedAfterDelay(key));
    }

    private void ReloadInterstitialLater(string key)
    {
        if (!_quitting && isActiveAndEnabled)
            StartCoroutine(ReloadInterstitialAfterDelay(key));
    }

    private IEnumerator ReloadRewardedAfterDelay(string key)
    {
        AdsConfig config = ResolveConfig();
        float delay = config != null ? config.ReloadDelaySeconds : 0f;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        LoadRewarded(key);
    }

    private IEnumerator ReloadInterstitialAfterDelay(string key)
    {
        AdsConfig config = ResolveConfig();
        float delay = config != null ? config.ReloadDelaySeconds : 0f;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        LoadInterstitial(key);
    }

#if LEVELPLAY_RUNTIME
    private void CreateAndLoadBanner(AdsBannerPlacementConfig placement)
    {
        DestroyBanner();
        _currentBannerPlacementId = placement.ConfigKey;
        _showCurrentBannerAfterLoad = !placement.DisplayOnLoad;

        var config = new LevelPlayBannerAd.Config.Builder()
            .SetSize(ToLevelPlaySize(placement.Size))
            .SetPosition(ToLevelPlayPosition(placement.Position))
            .SetDisplayOnLoad(placement.DisplayOnLoad)
            .SetRespectSafeArea(placement.RespectSafeArea)
            .SetPlacementName(placement.PlacementName)
            .Build();

        _currentBannerAd = new LevelPlayBannerAd(placement.GetAdUnitId(), config);
        _currentBannerAd.OnAdLoaded += HandleBannerLoaded;
        _currentBannerAd.OnAdLoadFailed += HandleBannerLoadFailed;
        _currentBannerAd.OnAdDisplayed += HandleBannerDisplayed;
        _currentBannerAd.OnAdDisplayFailed += HandleBannerDisplayFailed;
        _currentBannerAd.OnAdClicked += info => LogDebug("BannerClicked", "[Ads] Banner clicked.", BannerMetadata(placement, "info", info));
        _currentBannerAd.OnAdCollapsed += info => LogDebug("BannerCollapsed", "[Ads] Banner collapsed.", BannerMetadata(placement, "info", info));
        _currentBannerAd.OnAdLeftApplication += info => LogDebug("BannerLeftApplication", "[Ads] Banner left application.", BannerMetadata(placement, "info", info));
        _currentBannerAd.OnAdExpanded += info => LogDebug("BannerExpanded", "[Ads] Banner expanded.", BannerMetadata(placement, "info", info));
        _currentBannerAd.LoadAd();

        LogInfo(nameof(CreateAndLoadBanner), "[Ads] Banner создан и загружается.", BannerMetadata(placement));
    }

    private void HandleBannerLoaded(LevelPlayAdInfo info)
    {
        LogInfo("BannerLoaded", "[Ads] Banner загружен.", LogMetadata.Of("placementId", _currentBannerPlacementId, "info", info));
        if (_showCurrentBannerAfterLoad)
        {
            _showCurrentBannerAfterLoad = false;
            _currentBannerAd?.ShowAd();
        }
    }

    private void HandleBannerLoadFailed(LevelPlayAdError error)
    {
        LogWarning("BannerLoadFailed", "[Ads] Banner не загрузился.", LogMetadata.Of("placementId", _currentBannerPlacementId, "error", error));
    }

    private void HandleBannerDisplayed(LevelPlayAdInfo info)
    {
        LogInfo("BannerDisplayed", "[Ads] Banner показан.", LogMetadata.Of("placementId", _currentBannerPlacementId, "info", info));
        BannerShown?.Invoke(_currentBannerPlacementId);
    }

    private void HandleBannerDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        LogWarning("BannerDisplayFailed", "[Ads] Banner не показался.", LogMetadata.Of("placementId", _currentBannerPlacementId, "info", info, "error", error));
    }

    private static LevelPlayAdSize ToLevelPlaySize(AdsBannerSize size)
    {
        switch (size)
        {
            case AdsBannerSize.Large:
                return LevelPlayAdSize.LARGE;
            case AdsBannerSize.MediumRectangle:
                return LevelPlayAdSize.MEDIUM_RECTANGLE;
            case AdsBannerSize.Adaptive:
                return LevelPlayAdSize.CreateAdaptiveAdSize();
            default:
                return LevelPlayAdSize.BANNER;
        }
    }

    private static LevelPlayBannerPosition ToLevelPlayPosition(AdsBannerPosition position)
    {
        switch (position)
        {
            case AdsBannerPosition.TopLeft:
                return LevelPlayBannerPosition.TopLeft;
            case AdsBannerPosition.TopCenter:
                return LevelPlayBannerPosition.TopCenter;
            case AdsBannerPosition.TopRight:
                return LevelPlayBannerPosition.TopRight;
            case AdsBannerPosition.CenterLeft:
                return LevelPlayBannerPosition.CenterLeft;
            case AdsBannerPosition.Center:
                return LevelPlayBannerPosition.Center;
            case AdsBannerPosition.CenterRight:
                return LevelPlayBannerPosition.CenterRight;
            case AdsBannerPosition.BottomLeft:
                return LevelPlayBannerPosition.BottomLeft;
            case AdsBannerPosition.BottomRight:
                return LevelPlayBannerPosition.BottomRight;
            default:
                return LevelPlayBannerPosition.BottomCenter;
        }
    }

    private void UnsubscribeLevelPlayInitEvents()
    {
        LevelPlay.OnInitSuccess -= HandleLevelPlayInitSuccess;
        LevelPlay.OnInitFailed -= HandleLevelPlayInitFailed;
    }

    private void DestroyLevelPlayAds()
    {
        foreach (ILevelPlayRewardedAd ad in _rewardedAds.Values)
            ad?.DestroyAd();

        foreach (ILevelPlayInterstitialAd ad in _interstitialAds.Values)
            ad?.DestroyAd();

        _rewardedAds.Clear();
        _interstitialAds.Clear();
        DestroyBanner();
    }
#else
    private static void UnsubscribeLevelPlayInitEvents()
    {
    }

    private static void DestroyLevelPlayAds()
    {
    }
#endif

    private void LogInterstitialSkipped(string placementId, string reason, string skipReason)
    {
        ThrottledAppLogger.Debug(
            Component + ".InterstitialSkipped." + placementId + "." + skipReason,
            AppLogCategory.Ads,
            Component,
            nameof(TryShowInterstitial),
            "[Ads] Interstitial пропущен.",
            LogMetadata.Of("placementId", placementId, "reason", reason, "skipReason", skipReason),
            10d);
    }

    private void LogInfo(string operation, string message, IDictionary<string, object> metadata)
    {
        AppLogger.Info(AppLogCategory.Ads, Component, operation, message, metadata);
    }

    private void LogDebug(string operation, string message, IDictionary<string, object> metadata)
    {
        AdsConfig config = ResolveConfig();
        if (config != null && !config.DebugLogging)
            return;

        AppLogger.DebugLog(AppLogCategory.Ads, Component, operation, message, metadata);
    }

    private void LogWarning(string operation, string message, IDictionary<string, object> metadata, bool recoverable = true)
    {
        AppLogger.Warn(AppLogCategory.Ads, Component, operation, message, metadata, recoverable: recoverable);
    }

    private static IDictionary<string, object> PlacementMetadata(AdsPlacementConfig placement, params object[] extra)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            { "placementId", placement != null ? placement.ConfigKey : "" },
            { "placementName", placement != null ? placement.PlacementName : "" },
            { "adUnitId", placement != null ? placement.GetAdUnitId() : "" }
        };

        AddExtra(metadata, extra);
        return metadata;
    }

    private static IDictionary<string, object> BannerMetadata(AdsBannerPlacementConfig placement, params object[] extra)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            { "placementId", placement != null ? placement.ConfigKey : "" },
            { "placementName", placement != null ? placement.PlacementName : "" },
            { "adUnitId", placement != null ? placement.GetAdUnitId() : "" },
            { "size", placement != null ? placement.Size.ToString() : "" },
            { "position", placement != null ? placement.Position.ToString() : "" }
        };

        AddExtra(metadata, extra);
        return metadata;
    }

    private static void AddExtra(IDictionary<string, object> metadata, object[] extra)
    {
        if (metadata == null || extra == null)
            return;

        for (int i = 0; i + 1 < extra.Length; i += 2)
        {
            string key = extra[i] != null ? extra[i].ToString() : "";
            if (!string.IsNullOrWhiteSpace(key))
                metadata[key] = extra[i + 1];
        }
    }
}
