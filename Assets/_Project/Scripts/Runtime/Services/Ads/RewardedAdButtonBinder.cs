using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[AddComponentMenu("Nocturne/Ads/Rewarded Ad Button Binder")]
public sealed class RewardedAdButtonBinder : MonoBehaviour
{
    [SerializeField] private AdsServiceBehaviour _adsService;
    [SerializeField] private Button _button;
    [SerializeField] private string _placementId = "rewarded_bonus";
    [SerializeField] private bool _loadOnEnable = true;
    [SerializeField] private bool _updateInteractable = true;
    [SerializeField] private UnityEvent _rewardGranted = new UnityEvent();
    [SerializeField] private UnityEvent _rewardNotGranted = new UnityEvent();
    [SerializeField] private AdRewardResultEvent _completed = new AdRewardResultEvent();

    private bool _showPending;

    public event Action<AdRewardResult> Completed;

    private void Reset()
    {
        AutoBindButtonReference();
    }

    private void OnValidate()
    {
        AutoBindButtonReference();

        if (string.IsNullOrWhiteSpace(_placementId))
            _placementId = "rewarded_bonus";
    }

    private void Awake()
    {
        AutoBindButtonReference();
    }

    private void OnEnable()
    {
        BindButton();

        IAdsService service = ResolveService();
        if (_loadOnEnable)
            service?.LoadRewarded(_placementId);

        UpdateInteractable();
    }

    private void Update()
    {
        if (_updateInteractable)
            UpdateInteractable();
    }

    private void OnDisable()
    {
        UnbindButton();
        _showPending = false;
    }

    public void ShowRewarded()
    {
        if (_showPending)
            return;

        IAdsService service = ResolveService();
        if (service == null)
        {
            AppLogger.Warn(
                AppLogCategory.Ads,
                nameof(RewardedAdButtonBinder),
                nameof(ShowRewarded),
                "[Ads] Rewarded button did not find an ads service.",
                LogMetadata.Of("placementId", _placementId),
                recoverable: true);
            return;
        }

        if (!service.IsInitialized)
            service.Initialize();

        _showPending = true;
        UpdateInteractable();
        service.ShowRewarded(_placementId, HandleRewardedResult);
    }

    private void HandleRewardedResult(AdRewardResult result)
    {
        _showPending = false;
        UpdateInteractable();

        if (result != null && result.Success)
            _rewardGranted.Invoke();
        else
            _rewardNotGranted.Invoke();

        _completed.Invoke(result);
        Completed?.Invoke(result);
    }

    private void UpdateInteractable()
    {
        if (_button == null || !_updateInteractable)
            return;

        IAdsService service = ResolveService();
        _button.interactable = !_showPending && service != null && service.IsRewardedReady(_placementId);
    }

    private IAdsService ResolveService()
    {
        if (_adsService != null)
            return _adsService;

        return AdsServiceBehaviour.TryGetGlobal(out IAdsService service) ? service : null;
    }

    private void AutoBindButtonReference()
    {
        if (_button == null)
            _button = GetComponent<Button>();
    }

    private void BindButton()
    {
        if (_button == null)
            return;

        _button.onClick.RemoveListener(ShowRewarded);
        _button.onClick.AddListener(ShowRewarded);
    }

    private void UnbindButton()
    {
        if (_button != null)
            _button.onClick.RemoveListener(ShowRewarded);
    }
}
