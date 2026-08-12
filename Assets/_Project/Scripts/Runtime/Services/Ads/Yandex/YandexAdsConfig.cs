using UnityEngine;

public enum YandexConsentState
{
    Unknown,
    Granted,
    Denied
}

public enum YandexAgeRestrictionState
{
    Unknown,
    NotRestricted,
    Restricted
}

[CreateAssetMenu(menuName = "Nocturne/Ads/Yandex Ads Config", fileName = "YandexAdsConfig")]
public sealed class YandexAdsConfig : ScriptableObject
{
    public const string DefaultResourcesPath = "Ads/YandexAdsConfig";
    public const string DemoRewardedAdUnitId = "demo-rewarded-yandex";

    [Header("Runtime")]
    [SerializeField] private bool _adsEnabled = true;
    [SerializeField] private bool _autoCreateRuntimeService = true;
    [SerializeField] private bool _initializeOnStart = true;

    [Header("Rewarded Ad Unit")]
    [SerializeField] private bool _useDemoAdUnitId = true;
    [SerializeField] private string _androidRewardedAdUnitId = "";
    [SerializeField] private string _iosRewardedAdUnitId = "";
    [SerializeField, Min(1f)] private float _reloadDelaySeconds = 10f;

    [Header("Privacy - apply on every launch")]
    [SerializeField] private YandexConsentState _userConsent = YandexConsentState.Unknown;
    [SerializeField] private YandexAgeRestrictionState _ageRestriction = YandexAgeRestrictionState.Unknown;

    [Header("Editor Only")]
    [SerializeField] private bool _enableEditorMock;
    [SerializeField] private bool _allowDemoInDevelopmentBuild = true;
    [SerializeField] private bool _allowDemoInAnyBuildForTesting = true;
    [SerializeField, Min(0f)] private float _editorMockDelaySeconds = 0.35f;

    public bool AdsEnabled => _adsEnabled;
    public bool AutoCreateRuntimeService => _autoCreateRuntimeService;
    public bool InitializeOnStart => _initializeOnStart;
    public bool UseDemoAdUnitId => _useDemoAdUnitId;
    public float ReloadDelaySeconds => Mathf.Max(1f, _reloadDelaySeconds);
    public YandexConsentState UserConsent => _userConsent;
    public YandexAgeRestrictionState AgeRestriction => _ageRestriction;
    public bool EnableEditorMock => _enableEditorMock;
    public bool AllowDemoInDevelopmentBuild => _allowDemoInDevelopmentBuild;
    public bool AllowDemoInAnyBuildForTesting => _allowDemoInAnyBuildForTesting;
    public float EditorMockDelaySeconds => Mathf.Max(0f, _editorMockDelaySeconds);
    public string AndroidRewardedAdUnitId => Clean(_androidRewardedAdUnitId);
    public string IosRewardedAdUnitId => Clean(_iosRewardedAdUnitId);

    public string RewardedAdUnitId
    {
        get
        {
            if (_useDemoAdUnitId)
                return DemoRewardedAdUnitId;
#if UNITY_IOS
            return IosRewardedAdUnitId;
#else
            return AndroidRewardedAdUnitId;
#endif
        }
    }

    public bool HasRewardedAdUnitId => !string.IsNullOrEmpty(RewardedAdUnitId);

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
    }
}
