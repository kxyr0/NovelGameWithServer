using UnityEngine;

public static class MobileFrameRateBootstrap
{
    private const int FallbackTargetFps = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        if (!Application.isMobilePlatform)
            return;

        // On mobile, targetFrameRate == -1 may fall back to a low platform default.
        // Explicitly target the current display refresh rate instead of silently living at ~30 FPS.
        QualitySettings.vSyncCount = 0;

        int refreshRate = ResolveDisplayRefreshRate();
        int target = refreshRate >= 45 ? refreshRate : FallbackTargetFps;
        target = Mathf.Clamp(target, 45, 240);
        Application.targetFrameRate = target;

        // Give async texture/asset uploads enough time to make progress on slower phones
        // without turning loading into a frame-count-dependent operation.
        Application.backgroundLoadingPriority = ThreadPriority.Normal;
        QualitySettings.asyncUploadTimeSlice = Mathf.Max(QualitySettings.asyncUploadTimeSlice, 4);
        QualitySettings.asyncUploadBufferSize = Mathf.Max(QualitySettings.asyncUploadBufferSize, 32);
#if UNITY_2020_2_OR_NEWER
        QualitySettings.asyncUploadPersistentBuffer = true;
#endif

        Debug.Log(
            $"[PERF][FRAME_RATE] platform={Application.platform} displayRefresh={refreshRate} " +
            $"targetFrameRate={Application.targetFrameRate} vSyncCount={QualitySettings.vSyncCount} " +
            $"asyncUploadTimeSlice={QualitySettings.asyncUploadTimeSlice} " +
            $"asyncUploadBufferSize={QualitySettings.asyncUploadBufferSize}.");
    }

    private static int ResolveDisplayRefreshRate()
    {
#if UNITY_2022_2_OR_NEWER
        double value = Screen.currentResolution.refreshRateRatio.value;
        if (value > 1.0)
            return Mathf.RoundToInt((float)value);
#else
#pragma warning disable 0618
        int value = Screen.currentResolution.refreshRate;
#pragma warning restore 0618
        if (value > 1)
            return value;
#endif
        return FallbackTargetFps;
    }
}
