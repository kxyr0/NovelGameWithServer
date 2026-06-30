using UnityEngine;

public abstract class AdsEntitlementProvider : MonoBehaviour
{
    public virtual bool CanShowRewardedAds => true;
    public virtual bool CanShowInterstitialAds => true;
    public virtual bool CanShowBannerAds => true;

    public bool CanShow(AdsAdType adType)
    {
        switch (adType)
        {
            case AdsAdType.Rewarded:
                return CanShowRewardedAds;
            case AdsAdType.Interstitial:
                return CanShowInterstitialAds;
            case AdsAdType.Banner:
                return CanShowBannerAds;
            default:
                return true;
        }
    }
}
