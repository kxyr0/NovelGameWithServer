using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

[Serializable]
public enum AdRewardCurrency
{
    Candles = 0,
    Rubies = 1
}

[Serializable]
public sealed class CandleAdRewardEvent : UnityEvent<int> { }

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Ads/Ad Screen Controller")]
public sealed partial class AdScreenController : MonoBehaviour
{
    [Header("Yandex Rewarded")]
    [SerializeField] private YandexRewardedAdsService _adsService;
    [SerializeField] private string _placementId = "yandex_rewarded_candles";
    [SerializeField, Min(1)] private int _dailyLimit = 5;
    [SerializeField] private AdRewardCurrency _rewardCurrency = AdRewardCurrency.Candles;
    [FormerlySerializedAs("_rewardCandles")]
    [SerializeField, Min(1)] private int _rewardAmount = 25;
    [Tooltip("Включать только когда серверный endpoint выдаёт выбранную валюту в нужном количестве.")]
    [SerializeField] private bool _useServerAuthoritativeReward;

    [Header("UI")]
    [SerializeField] private Button _watchButton;
    [SerializeField] private TMP_Text _rewardAmountText;
    [SerializeField] private Image _rewardCurrencyIcon;
    [SerializeField] private Sprite _candlesIcon;
    [SerializeField] private Sprite _rubiesIcon;
    [SerializeField] private TMP_Text _adCountText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private bool _keepButtonImageColor = true;
    [SerializeField] private string _countFormat = "Доступно {0} из {1} роликов сегодня";
    [SerializeField] private string _rewardAmountFormat = "Награда: {0}";
    [SerializeField] private string _loadingText = "Реклама загружается...";
    [SerializeField] private string _limitText = "Лимит на сегодня исчерпан";
    [FormerlySerializedAs("_rewardText")]
    [SerializeField] private string _candlesRewardText = "+{0} свечей";
    [SerializeField] private string _rubiesRewardText = "+{0} рубина";

    [Header("Back")]
    [SerializeField] private Button _backButton;
    [SerializeField] private StoryScreenNavigator _screenNavigator;
    [SerializeField] private string _backScreenId = "MainScreen";

    [Header("Events")]
    [SerializeField] private CandleAdRewardEvent _rewardGranted = new CandleAdRewardEvent();
    [SerializeField] private UnityEvent _rewardFailed = new UnityEvent();

    private bool _busy;
    private float _nextRefreshAt;

    private void Awake()
    {
        if (_keepButtonImageColor && _watchButton != null)
            _watchButton.transition = Selectable.Transition.None;
        ResolveAdsService();
    }

    private void OnEnable()
    {
        BindButtons();
        YandexRewardedAdsService service = ResolveAdsService();
        service?.Initialize();
        service?.LoadRewarded(_placementId);
        RefreshState();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshAt)
            return;
        _nextRefreshAt = Time.unscaledTime + 0.25f;
        RefreshState();
    }

    private void OnValidate()
    {
        _dailyLimit = Mathf.Max(1, _dailyLimit);
        _rewardAmount = Mathf.Max(1, _rewardAmount);
        if (string.IsNullOrWhiteSpace(_placementId))
            _placementId = "yandex_rewarded_candles";
        if (string.IsNullOrWhiteSpace(_backScreenId))
            _backScreenId = "MainScreen";
    }

    public void RefreshState()
    {
        int remaining = AdDailyLimitStore.GetRemainingToday(_dailyLimit);
        if (_rewardAmountText != null)
            _rewardAmountText.text = Format(_rewardAmountFormat, _rewardAmount);
        RefreshRewardIcon();
        if (_adCountText != null)
            _adCountText.text = Format(_countFormat, remaining, _dailyLimit);

        YandexRewardedAdsService service = ResolveAdsService();
        bool ready = service != null && service.IsRewardedReady(_placementId);
        if (_watchButton != null)
            _watchButton.interactable = !_busy && remaining > 0 && ready;

        if (!_busy && remaining <= 0)
            SetStatus(_limitText);
    }

    private void RefreshRewardIcon()
    {
        if (_rewardCurrencyIcon == null)
            return;
        Sprite sprite = _rewardCurrency == AdRewardCurrency.Rubies ? _rubiesIcon : _candlesIcon;
        if (sprite != null)
            _rewardCurrencyIcon.sprite = sprite;
    }

    private string GetRewardText()
    {
        return _rewardCurrency == AdRewardCurrency.Rubies ? _rubiesRewardText : _candlesRewardText;
    }

    public void OpenMainScreen()
    {
        string target = string.IsNullOrWhiteSpace(_backScreenId) ? "MainScreen" : _backScreenId.Trim();
        if (_screenNavigator == null || !_screenNavigator.OpenScreen(target))
            Debug.LogWarning("[AdScreen] Screen is not registered: " + target);
    }

    private YandexRewardedAdsService ResolveAdsService()
    {
        if (_adsService == null)
            _adsService = YandexRewardedAdsService.Instance;
        return _adsService;
    }

    private void BindButtons()
    {
        if (_watchButton != null)
        {
            _watchButton.onClick.RemoveListener(WatchRewardedAd);
            _watchButton.onClick.AddListener(WatchRewardedAd);
        }
        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OpenMainScreen);
            _backButton.onClick.AddListener(OpenMainScreen);
        }
    }

    private void UnbindButtons()
    {
        if (_watchButton != null)
            _watchButton.onClick.RemoveListener(WatchRewardedAd);
        if (_backButton != null)
            _backButton.onClick.RemoveListener(OpenMainScreen);
    }

    private void SetStatus(string value)
    {
        if (_statusText != null)
            _statusText.text = value ?? "";
    }

    private static string Format(string format, params object[] values)
    {
        try { return string.Format(format ?? "", values); }
        catch (FormatException) { return ""; }
    }
}
