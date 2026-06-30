using System;

public interface IRewardedAdService
{
    bool IsRewardedReady(string placementId);
    void LoadRewarded(string placementId);
    void ShowRewarded(string placementId, Action<AdRewardResult> callback);
}

public interface IInterstitialAdService
{
    bool IsInterstitialReady(string placementId);
    void LoadInterstitial(string placementId);
    bool TryShowInterstitial(string placementId, string reason);
}

public interface IBannerAdService
{
    void ShowBanner(string placementId);
    void HideBanner();
    void DestroyBanner();
}

public interface IAdsService : IRewardedAdService, IInterstitialAdService, IBannerAdService
{
    event Action<bool> InitializationChanged;
    event Action<AdRewardResult> RewardedAdCompleted;
    event Action<string, string> InterstitialSkipped;
    event Action<string> InterstitialShown;
    event Action<string> BannerShown;
    event Action<string> BannerHidden;

    bool IsInitialized { get; }
    void Initialize();
}
