using UnityEngine;

internal static class AdsRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (AdsServiceBehaviour.GlobalService != null)
            return;

        AdsConfig config = Resources.Load<AdsConfig>(AdsConfig.DefaultResourcesPath);
        if (config == null || !config.AutoCreateRuntimeService)
            return;

        var gameObject = new GameObject(nameof(LevelPlayAdsService));
        Object.DontDestroyOnLoad(gameObject);
        LevelPlayAdsService service = gameObject.AddComponent<LevelPlayAdsService>();
        service.Configure(config);
    }
}
