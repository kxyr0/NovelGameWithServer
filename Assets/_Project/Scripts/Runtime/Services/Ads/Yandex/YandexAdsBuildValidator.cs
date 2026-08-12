#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class YandexAdsBuildValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report == null)
            return;

        YandexAdsConfig config = Resources.Load<YandexAdsConfig>(YandexAdsConfig.DefaultResourcesPath);
        if (config == null || !config.AdsEnabled)
            return;
        if (report.summary.platform != BuildTarget.Android && report.summary.platform != BuildTarget.iOS)
            return;

        bool development = (report.summary.options & BuildOptions.Development) != 0;
        if (config.UseDemoAdUnitId)
        {
            if (config.AllowDemoInAnyBuildForTesting ||
                (development && config.AllowDemoInDevelopmentBuild))
            {
                Debug.LogWarning(
                    "[YandexAds] Сборка использует demo-rewarded-yandex. " +
                    "Реклама подходит для проверки на телефоне, но не приносит доход.");
                return;
            }
            throw new BuildFailedException(
                "Yandex Ads: demo-rewarded-yandex нельзя использовать в production build. " +
                "Укажите реальный R-M-... в Assets/_Project/Resources/Ads/YandexAdsConfig.");
        }

        string adUnitId = report.summary.platform == BuildTarget.iOS
            ? config.IosRewardedAdUnitId
            : config.AndroidRewardedAdUnitId;
        if (!IsProductionAdUnitId(adUnitId))
        {
            throw new BuildFailedException(
                "Yandex Ads: для текущей платформы нужен реальный rewarded Ad Unit ID формата R-M-XXXXXX-Y.");
        }

        if (config.UserConsent == YandexConsentState.Unknown)
            Debug.LogWarning("[YandexAds] User consent is Unknown. Connect your consent flow before release.");
        if (config.AgeRestriction == YandexAgeRestrictionState.Unknown)
            Debug.LogWarning("[YandexAds] Age restriction is Unknown. Configure the age flag before release.");
    }

    private static bool IsProductionAdUnitId(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.StartsWith("R-M-", StringComparison.Ordinal) &&
               value.Length >= 9;
    }
}
#endif
