using UnityEngine;

internal static class YandexAdsRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (YandexRewardedAdsService.Instance != null)
            return;

        YandexAdsConfig config = Resources.Load<YandexAdsConfig>(YandexAdsConfig.DefaultResourcesPath);
        if (config == null || !config.AutoCreateRuntimeService)
            return;

        var root = new GameObject(nameof(YandexRewardedAdsService));
        YandexRewardedAdsService service = root.AddComponent<YandexRewardedAdsService>();
        service.Configure(config);
    }
}
