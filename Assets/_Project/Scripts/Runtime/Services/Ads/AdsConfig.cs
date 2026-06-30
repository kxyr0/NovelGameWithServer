using UnityEngine;

[CreateAssetMenu(menuName = "Nocturne/Ads/Ads Config", fileName = "AdsConfig")]
public sealed class AdsConfig : ScriptableObject
{
    public const string DefaultResourcesPath = "Ads/AdsConfig";

    [Header("Runtime")]
    [SerializeField] private bool _adsEnabled;
    [SerializeField] private bool _autoCreateRuntimeService = true;
    [SerializeField] private bool _initializeOnStart = true;
    [SerializeField] private bool _debugLogging = true;

    [Header("LevelPlay App Keys")]
    [SerializeField] private string _androidAppKey = "YOUR_LEVELPLAY_ANDROID_APP_KEY";
    [SerializeField] private string _iosAppKey = "YOUR_LEVELPLAY_IOS_APP_KEY";

    [Header("Test And Diagnostics")]
    [SerializeField] private bool _testMode = true;
    [SerializeField] private bool _enableIntegrationTestSuite = true;
    [SerializeField] private bool _validateIntegrationOnInitialize;

    [Header("Rewarded")]
    [SerializeField] private AdsPlacementConfig[] _rewardedPlacements =
    {
        new AdsPlacementConfig(
            "rewarded_bonus",
            "ANDROID_REWARDED_AD_UNIT_ID",
            "IOS_REWARDED_AD_UNIT_ID",
            "rewarded_bonus")
    };

    [Header("Interstitial")]
    [SerializeField] private AdsPlacementConfig[] _interstitialPlacements =
    {
        new AdsPlacementConfig(
            "interstitial_transition",
            "ANDROID_INTERSTITIAL_AD_UNIT_ID",
            "IOS_INTERSTITIAL_AD_UNIT_ID",
            "interstitial_transition")
    };

    [SerializeField, Min(0f)] private float _interstitialCooldownSeconds = 90f;
    [SerializeField, Min(0f)] private float _minimumGameplaySecondsBeforeFirstInterstitial = 60f;
    [SerializeField, Min(0f)] private float _interstitialDelayAfterRewardedSeconds = 30f;

    [Header("Banner")]
    [SerializeField] private AdsBannerPlacementConfig[] _bannerPlacements =
    {
        new AdsBannerPlacementConfig(
            "banner_menu",
            "ANDROID_BANNER_AD_UNIT_ID",
            "IOS_BANNER_AD_UNIT_ID",
            "banner_menu")
    };

    [Header("Retries")]
    [SerializeField, Min(0f)] private float _reloadDelaySeconds = 10f;
    [SerializeField, Min(0f)] private float _rewardedCloseGraceSeconds = 1f;

    [Header("Editor Mock")]
    [SerializeField] private bool _editorMockAdsStartReady = true;
    [SerializeField] private bool _editorMockRewardedSucceeds = true;
    [SerializeField, Min(0f)] private float _editorMockDelaySeconds = 0.35f;
    [SerializeField] private string _editorMockRewardName = "soft_currency";
    [SerializeField, Min(0)] private int _editorMockRewardAmount = 10;

    public bool AdsEnabled => _adsEnabled;
    public bool AutoCreateRuntimeService => _autoCreateRuntimeService;
    public bool InitializeOnStart => _initializeOnStart;
    public bool DebugLogging => _debugLogging;
    public bool TestMode => _testMode;
    public bool EnableIntegrationTestSuite => _enableIntegrationTestSuite;
    public bool ValidateIntegrationOnInitialize => _validateIntegrationOnInitialize;
    public float ReloadDelaySeconds => Mathf.Max(0f, _reloadDelaySeconds);
    public float RewardedCloseGraceSeconds => Mathf.Max(0f, _rewardedCloseGraceSeconds);
    public bool EditorMockAdsStartReady => _editorMockAdsStartReady;
    public bool EditorMockRewardedSucceeds => _editorMockRewardedSucceeds;
    public float EditorMockDelaySeconds => Mathf.Max(0f, _editorMockDelaySeconds);
    public string EditorMockRewardName => Clean(_editorMockRewardName);
    public int EditorMockRewardAmount => Mathf.Max(0, _editorMockRewardAmount);
    public AdsPlacementConfig[] RewardedPlacements => _rewardedPlacements ?? System.Array.Empty<AdsPlacementConfig>();
    public AdsPlacementConfig[] InterstitialPlacements => _interstitialPlacements ?? System.Array.Empty<AdsPlacementConfig>();
    public AdsBannerPlacementConfig[] BannerPlacements => _bannerPlacements ?? System.Array.Empty<AdsBannerPlacementConfig>();
    public string AndroidAppKey => Clean(_androidAppKey);
    public string IosAppKey => Clean(_iosAppKey);

    public string GetAppKey()
    {
#if UNITY_IOS
        return IosAppKey;
#else
        return AndroidAppKey;
#endif
    }

    public InterstitialAdFrequencyPolicy BuildInterstitialPolicy()
    {
        return new InterstitialAdFrequencyPolicy(
            _interstitialCooldownSeconds,
            _minimumGameplaySecondsBeforeFirstInterstitial,
            _interstitialDelayAfterRewardedSeconds);
    }

    public AdsPlacementConfig FindRewardedPlacement(string placementId)
    {
        return FindPlacement(RewardedPlacements, placementId);
    }

    public AdsPlacementConfig FindInterstitialPlacement(string placementId)
    {
        return FindPlacement(InterstitialPlacements, placementId);
    }

    public AdsBannerPlacementConfig FindBannerPlacement(string placementId)
    {
        return FindBannerPlacement(BannerPlacements, placementId);
    }

    private static AdsPlacementConfig FindPlacement(AdsPlacementConfig[] placements, string placementId)
    {
        if (placements == null || placements.Length == 0)
            return null;

        if (string.IsNullOrWhiteSpace(placementId))
            return FirstConfigured(placements);

        foreach (AdsPlacementConfig placement in placements)
        {
            if (placement != null && placement.Matches(placementId))
                return placement;
        }

        return null;
    }

    private static AdsPlacementConfig FirstConfigured(AdsPlacementConfig[] placements)
    {
        foreach (AdsPlacementConfig placement in placements)
        {
            if (placement != null && placement.HasAdUnitForCurrentPlatform)
                return placement;
        }

        return placements.Length > 0 ? placements[0] : null;
    }

    private static AdsBannerPlacementConfig FindBannerPlacement(AdsBannerPlacementConfig[] placements, string placementId)
    {
        if (placements == null || placements.Length == 0)
            return null;

        if (string.IsNullOrWhiteSpace(placementId))
            return FirstConfigured(placements);

        foreach (AdsBannerPlacementConfig placement in placements)
        {
            if (placement != null && placement.Matches(placementId))
                return placement;
        }

        return null;
    }

    private static AdsBannerPlacementConfig FirstConfigured(AdsBannerPlacementConfig[] placements)
    {
        foreach (AdsBannerPlacementConfig placement in placements)
        {
            if (placement != null && placement.HasAdUnitForCurrentPlatform)
                return placement;
        }

        return placements.Length > 0 ? placements[0] : null;
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
    }
}
