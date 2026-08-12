#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
#define YANDEX_ADS_RUNTIME
using YandexMobileAds;
using YandexMobileAds.Base;
#endif

using System;
using System.Collections;
using UnityEngine;

public sealed partial class YandexRewardedAdsService
{
#if YANDEX_ADS_RUNTIME
    private void HandleAdLoaded(object sender, RewardedAdLoadedEventArgs args)
    {
        _loading = false;
        DestroyLoadedAd();
        _rewardedAd = args != null ? args.RewardedAd : null;
        if (_rewardedAd == null)
        {
            Debug.LogWarning("[YandexAds] Rewarded load returned no ad.");
            ScheduleReload();
            return;
        }
        _rewardedAd.OnRewarded += HandleRewarded;
        _rewardedAd.OnAdDismissed += HandleDismissed;
        _rewardedAd.OnAdFailedToShow += HandleFailedToShow;
    }

    private void HandleAdLoadFailed(object sender, AdFailedToLoadEventArgs args)
    {
        _loading = false;
        Debug.LogWarning("[YandexAds] Rewarded load failed: " + (args != null ? args.Message : "unknown"));
        ScheduleReload();
    }

    private void HandleRewarded(object sender, Reward reward)
    {
        if (_rewardReceived)
            return;
        _rewardReceived = true;
        Complete(AdRewardResult.Create(
            AdRewardStatus.Success,
            _activePlacementId,
            rewardName: reward != null ? reward.type : "",
            rewardAmount: reward != null ? reward.amount : 0));
    }

    private void HandleDismissed(object sender, EventArgs args)
    {
        if (!_rewardReceived)
            Complete(AdRewardResult.Create(AdRewardStatus.ClosedWithoutReward, _activePlacementId));
        FinishShowAndReload();
    }

    private void HandleFailedToShow(object sender, AdFailureEventArgs args)
    {
        Complete(AdRewardResult.Create(
            AdRewardStatus.DisplayFailed,
            _activePlacementId,
            args != null ? args.Message : "Реклама не показалась."));
        FinishShowAndReload();
    }

    private void ApplyPrivacySettings(YandexAdsConfig config)
    {
        if (config.UserConsent != YandexConsentState.Unknown)
            MobileAds.SetUserConsent(config.UserConsent == YandexConsentState.Granted);
        if (config.AgeRestriction != YandexAgeRestrictionState.Unknown)
            MobileAds.SetAgeRestrictedUser(config.AgeRestriction == YandexAgeRestrictionState.Restricted);
    }

    private void DestroyLoadedAd()
    {
        _rewardedAd?.Destroy();
        _rewardedAd = null;
    }
#else
    private static void ApplyPrivacySettings(YandexAdsConfig config) { }
    private void DestroyLoadedAd() { }
#endif

#if UNITY_EDITOR
    private IEnumerator ShowEditorMock()
    {
        float delay = _config != null ? _config.EditorMockDelaySeconds : 0.35f;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        _rewardReceived = true;
        Complete(AdRewardResult.Create(
            AdRewardStatus.Success,
            _activePlacementId,
            rewardName: "editor_mock",
            rewardAmount: 25));
        FinishShowAndReload();
    }
#else
    private IEnumerator ShowEditorMock() { yield break; }
#endif

    private void Complete(AdRewardResult result)
    {
        if (_callbackInvoked)
            return;
        _callbackInvoked = true;

        Action<AdRewardResult> callback = _activeCallback;
        _activeCallback = null;
        try
        {
            callback?.Invoke(result);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void FinishShowAndReload()
    {
        _showing = false;
        _activePlacementId = "";
        DestroyLoadedAd();
        ScheduleReload();
    }

    private void ScheduleReload()
    {
        if (!isActiveAndEnabled || _reloadRoutine != null)
            return;
        _reloadRoutine = StartCoroutine(ReloadAfterDelay());
    }

    private IEnumerator ReloadAfterDelay()
    {
        float delay = _config != null ? _config.ReloadDelaySeconds : 10f;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);
        _reloadRoutine = null;
#if UNITY_EDITOR
        _editorMockReady = _config != null && _config.EnableEditorMock;
#else
        LoadRewarded("");
#endif
    }

    private void ShutdownYandex()
    {
        if (_reloadRoutine != null)
            StopCoroutine(_reloadRoutine);
        _reloadRoutine = null;
#if YANDEX_ADS_RUNTIME
        if (_loader != null)
        {
            _loader.OnAdLoaded -= HandleAdLoaded;
            _loader.OnAdFailedToLoad -= HandleAdLoadFailed;
            _loader.CancelLoading();
            _loader = null;
        }
#endif
        DestroyLoadedAd();
        _activeCallback = null;
        _initialized = false;
    }
}
