using UnityEngine;

public abstract class AdsServiceBehaviour : MonoBehaviour, IAdsService
{
    [SerializeField] private bool _registerAsGlobalService = true;

    public static IAdsService GlobalService { get; private set; }

    public abstract event System.Action<bool> InitializationChanged;
    public abstract event System.Action<AdRewardResult> RewardedAdCompleted;
    public abstract event System.Action<string, string> InterstitialSkipped;
    public abstract event System.Action<string> InterstitialShown;
    public abstract event System.Action<string> BannerShown;
    public abstract event System.Action<string> BannerHidden;

    public abstract bool IsInitialized { get; }
    public abstract bool IsRewardedReady(string placementId);
    public abstract bool IsInterstitialReady(string placementId);
    public abstract void Initialize();
    public abstract void LoadRewarded(string placementId);
    public abstract void ShowRewarded(string placementId, System.Action<AdRewardResult> callback);
    public abstract void LoadInterstitial(string placementId);
    public abstract bool TryShowInterstitial(string placementId, string reason);
    public abstract void ShowBanner(string placementId);
    public abstract void HideBanner();
    public abstract void DestroyBanner();

    public static bool TryGetGlobal(out IAdsService service)
    {
        service = GlobalService;
        return service != null;
    }

    protected virtual void Awake()
    {
        if (!_registerAsGlobalService)
            return;

        if (GlobalService != null && !ReferenceEquals(GlobalService, this))
        {
            AppLogger.Warn(
                AppLogCategory.Ads,
                nameof(AdsServiceBehaviour),
                nameof(Awake),
                "[Ads] В сцене уже зарегистрирован сервис рекламы. Новый экземпляр не будет глобальным.",
                LogMetadata.Of("existing", GlobalService.GetType().Name, "candidate", GetType().Name),
                recoverable: true);
            return;
        }

        GlobalService = this;
    }

    protected virtual void OnDestroy()
    {
        if (ReferenceEquals(GlobalService, this))
            GlobalService = null;
    }
}
