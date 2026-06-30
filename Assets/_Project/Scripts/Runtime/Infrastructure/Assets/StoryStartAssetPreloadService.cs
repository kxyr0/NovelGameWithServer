using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

public interface IStoryStartAssetPreloadService
{
    UniTask PreloadAsync(
        StoryStartAssetPreloadRequest request,
        IProgress<StoryStartPreloadProgress> progress,
        CancellationToken cancellationToken);
}

public enum StoryStartPreloadStage
{
    None,
    LoadingMedia,
    Cover,
    Texture,
    Audio,
    Video,
    Data,
    Resources,
    Complete
}

public readonly struct StoryStartAssetPreloadRequest
{
    public StoryStartAssetPreloadRequest(
        StoryStartPreloadAssetSet assets,
        StoryLoadingMediaSelection loadingMedia,
        IReadOnlyList<string> resourcePaths,
        bool preloadCoverTexture,
        bool preloadStoryTextures,
        bool preloadAudioData,
        bool waitForTextureStreaming,
        bool useUnscaledTime,
        float textureStreamingTimeout,
        int assetsPerFrame,
        float asyncOperationProgressScale,
        string coverStatus,
        string textureStatus,
        string audioStatus,
        string videoStatus,
        string dataStatus,
        string resourcesStatus)
    {
        Assets = assets ?? new StoryStartPreloadAssetSet();
        LoadingMedia = loadingMedia;
        ResourcePaths = resourcePaths ?? Array.Empty<string>();
        PreloadCoverTexture = preloadCoverTexture;
        PreloadStoryTextures = preloadStoryTextures;
        PreloadAudioData = preloadAudioData;
        WaitForTextureStreaming = waitForTextureStreaming;
        UseUnscaledTime = useUnscaledTime;
        TextureStreamingTimeout = Mathf.Max(0f, textureStreamingTimeout);
        AssetsPerFrame = Mathf.Max(1, assetsPerFrame);
        AsyncOperationProgressScale = Mathf.Clamp01(asyncOperationProgressScale);
        CoverStatus = coverStatus ?? "";
        TextureStatus = textureStatus ?? "";
        AudioStatus = audioStatus ?? "";
        VideoStatus = videoStatus ?? "";
        DataStatus = dataStatus ?? "";
        ResourcesStatus = resourcesStatus ?? "";
    }

    public StoryStartPreloadAssetSet Assets { get; }
    public StoryLoadingMediaSelection LoadingMedia { get; }
    public IReadOnlyList<string> ResourcePaths { get; }
    public bool PreloadCoverTexture { get; }
    public bool PreloadStoryTextures { get; }
    public bool PreloadAudioData { get; }
    public bool WaitForTextureStreaming { get; }
    public bool UseUnscaledTime { get; }
    public float TextureStreamingTimeout { get; }
    public int AssetsPerFrame { get; }
    public float AsyncOperationProgressScale { get; }
    public string CoverStatus { get; }
    public string TextureStatus { get; }
    public string AudioStatus { get; }
    public string VideoStatus { get; }
    public string DataStatus { get; }
    public string ResourcesStatus { get; }

    public int TotalSteps => Mathf.Max(1, Assets.TotalCount + ResourcePaths.Count);
}

public readonly struct StoryStartPreloadProgress
{
    public StoryStartPreloadProgress(int completedSteps, int totalSteps, float normalizedProgress, string status)
        : this(completedSteps, totalSteps, normalizedProgress, status, StoryStartPreloadStage.None)
    {
    }

    public StoryStartPreloadProgress(
        int completedSteps,
        int totalSteps,
        float normalizedProgress,
        string status,
        StoryStartPreloadStage stage)
    {
        CompletedSteps = Mathf.Max(0, completedSteps);
        TotalSteps = Mathf.Max(1, totalSteps);
        NormalizedProgress = Mathf.Clamp01(normalizedProgress);
        Status = status ?? "";
        Stage = stage;
    }

    public int CompletedSteps { get; }
    public int TotalSteps { get; }
    public float NormalizedProgress { get; }
    public string Status { get; }
    public StoryStartPreloadStage Stage { get; }

    public static StoryStartPreloadProgress Complete(string status)
    {
        return new StoryStartPreloadProgress(1, 1, 1f, status, StoryStartPreloadStage.Complete);
    }
}

public sealed class StoryStartAssetPreloadService : IStoryStartAssetPreloadService
{
    private const float AudioLoadTimeout = 2f;

    public async UniTask PreloadAsync(
        StoryStartAssetPreloadRequest request,
        IProgress<StoryStartPreloadProgress> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int totalSteps = request.TotalSteps;
        int completedSteps = 0;
        int processedThisFrame = 0;

        void ReportStep(StoryStartPreloadStage stage, string status)
        {
            completedSteps++;
            processedThisFrame++;
            progress?.Report(new StoryStartPreloadProgress(
                completedSteps,
                totalSteps,
                (float)completedSteps / totalSteps,
                status,
                stage));
        }

        async UniTask YieldFrameIfNeededAsync()
        {
            if (processedThisFrame < request.AssetsPerFrame)
                return;

            processedThisFrame = 0;
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        if (request.PreloadCoverTexture && request.LoadingMedia.CoverSprite != null)
        {
            await WarmUpSpriteAsync(request.LoadingMedia.CoverSprite, request, cancellationToken);
            ReportStep(StoryStartPreloadStage.Cover, request.CoverStatus);
        }

        if (request.PreloadStoryTextures)
        {
            foreach (Sprite sprite in request.Assets.Sprites)
            {
                await WarmUpSpriteAsync(sprite, request, cancellationToken);
                ReportStep(StoryStartPreloadStage.Texture, request.TextureStatus);
                await YieldFrameIfNeededAsync();
            }

            foreach (Texture texture in request.Assets.Textures)
            {
                await WarmUpTextureAsync(texture, request, cancellationToken);
                ReportStep(StoryStartPreloadStage.Texture, request.TextureStatus);
                await YieldFrameIfNeededAsync();
            }
        }

        if (request.PreloadAudioData)
        {
            foreach (AudioClip clip in request.Assets.AudioClips)
            {
                await WarmUpAudioAsync(clip, request, cancellationToken);
                ReportStep(StoryStartPreloadStage.Audio, request.AudioStatus);
                await YieldFrameIfNeededAsync();
            }
        }

        foreach (VideoClip clip in request.Assets.VideoClips)
        {
            WarmUpVideo(clip);
            ReportStep(StoryStartPreloadStage.Video, request.VideoStatus);
            await YieldFrameIfNeededAsync();
        }

        foreach (TextAsset textAsset in request.Assets.TextAssets)
        {
            WarmUpTextAsset(textAsset);
            ReportStep(StoryStartPreloadStage.Data, request.DataStatus);
            await YieldFrameIfNeededAsync();
        }

        for (int i = 0; i < request.ResourcePaths.Count; i++)
        {
            await LoadResourceAsync(request.ResourcePaths[i], request, progress, completedSteps, totalSteps, cancellationToken);
            ReportStep(StoryStartPreloadStage.Resources, request.ResourcesStatus);
            await YieldFrameIfNeededAsync();
        }

        progress?.Report(StoryStartPreloadProgress.Complete(request.DataStatus));
    }

    private static async UniTask WarmUpSpriteAsync(
        Sprite sprite,
        StoryStartAssetPreloadRequest request,
        CancellationToken cancellationToken)
    {
        if (sprite == null)
            return;

        await WarmUpTextureAsync(sprite.texture, request, cancellationToken);
    }

    private static async UniTask WarmUpTextureAsync(
        Texture texture,
        StoryStartAssetPreloadRequest request,
        CancellationToken cancellationToken)
    {
        if (texture == null)
            return;

        if (texture is Texture2D texture2D && request.WaitForTextureStreaming)
        {
            try
            {
                texture2D.requestedMipmapLevel = 0;
            }
            catch (Exception)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                return;
            }

            float startedAt = Now(request.UseUnscaledTime);
            while (!texture2D.IsRequestedMipmapLevelLoaded() &&
                   Now(request.UseUnscaledTime) - startedAt < request.TextureStreamingTimeout)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
    }

    private static async UniTask WarmUpAudioAsync(
        AudioClip clip,
        StoryStartAssetPreloadRequest request,
        CancellationToken cancellationToken)
    {
        if (clip == null)
            return;

        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();

        float startedAt = Now(request.UseUnscaledTime);
        while (clip.loadState == AudioDataLoadState.Loading &&
               Now(request.UseUnscaledTime) - startedAt < AudioLoadTimeout)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
    }

    private static void WarmUpVideo(VideoClip clip)
    {
        if (clip == null)
            return;

        _ = clip.width;
        _ = clip.height;
        _ = clip.length;
    }

    private static void WarmUpTextAsset(TextAsset textAsset)
    {
        if (textAsset == null)
            return;

        _ = textAsset.bytes;
    }

    private static async UniTask LoadResourceAsync(
        string path,
        StoryStartAssetPreloadRequest request,
        IProgress<StoryStartPreloadProgress> progress,
        int completedSteps,
        int totalSteps,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        string resourcePath = path.Trim();
        RuntimeTextureLoadScope loadScope = RuntimePerformanceDiagnostics.BeginTextureLoad("Resources:" + resourcePath);
        ResourceRequest resourceRequest = null;
        try
        {
            resourceRequest = Resources.LoadAsync<UnityEngine.Object>(resourcePath);
            RuntimePerformanceDiagnostics.TrackAsyncOperation("Resources:" + resourcePath, resourceRequest);
            while (resourceRequest != null && !resourceRequest.isDone)
            {
                float operationProgress = Mathf.Clamp01(resourceRequest.progress) * request.AsyncOperationProgressScale;
                float completedProgress = Mathf.Clamp01((float)completedSteps / totalSteps);
                progress?.Report(new StoryStartPreloadProgress(
                    completedSteps,
                    totalSteps,
                    Mathf.Max(completedProgress, operationProgress),
                    request.ResourcesStatus,
                    StoryStartPreloadStage.Resources));

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            bool success = resourceRequest != null && resourceRequest.asset != null;
            loadScope.Complete(success, success ? resourceRequest.asset.GetType().Name : "missing");
        }
        catch
        {
            loadScope.Complete(false, "exception");
            throw;
        }
    }

    private static float Now(bool useUnscaledTime)
    {
        return useUnscaledTime ? Time.unscaledTime : Time.time;
    }
}

public static class StoryStartAssetPreloadServices
{
    private static readonly IStoryStartAssetPreloadService SharedService = new StoryStartAssetPreloadService();

    public static IStoryStartAssetPreloadService Shared => SharedService;
}
