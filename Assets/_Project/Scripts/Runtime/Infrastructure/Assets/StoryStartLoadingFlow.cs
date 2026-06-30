using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IStoryStartLoadingFlow
{
    UniTask<StoryStartLoadingFlowResult> RunAsync(
        StoryStartLoadingFlowRequest request,
        IProgress<StoryStartPreloadProgress> progress,
        IStoryStartLoadingFlowObserver observer,
        CancellationToken cancellationToken);
}

public interface IStoryStartLoadingFlowObserver
{
    bool OnLoadingMediaLoaded(GameData data, StoryLoadingMediaLease loadingMedia);
}

public readonly struct StoryStartLoadingFlowRequest
{
    public StoryStartLoadingFlowRequest(
        GameData data,
        StoryStartLoadingAssetScope assetScope,
        IReadOnlyList<string> resourcePaths,
        bool preloadCoverTexture,
        bool preloadStoryTextures,
        bool preloadAudioData,
        bool waitForTextureStreaming,
        bool useUnscaledTime,
        float textureStreamingTimeout,
        int assetsPerFrame,
        float asyncOperationProgressScale,
        string loadingMediaStatus,
        string coverStatus,
        string textureStatus,
        string audioStatus,
        string videoStatus,
        string dataStatus,
        string resourcesStatus)
    {
        Data = data;
        AssetScope = assetScope;
        ResourcePaths = resourcePaths ?? Array.Empty<string>();
        PreloadCoverTexture = preloadCoverTexture;
        PreloadStoryTextures = preloadStoryTextures;
        PreloadAudioData = preloadAudioData;
        WaitForTextureStreaming = waitForTextureStreaming;
        UseUnscaledTime = useUnscaledTime;
        TextureStreamingTimeout = Mathf.Max(0f, textureStreamingTimeout);
        AssetsPerFrame = Mathf.Max(1, assetsPerFrame);
        AsyncOperationProgressScale = Mathf.Clamp01(asyncOperationProgressScale);
        LoadingMediaStatus = loadingMediaStatus ?? "";
        CoverStatus = coverStatus ?? "";
        TextureStatus = textureStatus ?? "";
        AudioStatus = audioStatus ?? "";
        VideoStatus = videoStatus ?? "";
        DataStatus = dataStatus ?? "";
        ResourcesStatus = resourcesStatus ?? "";
    }

    public GameData Data { get; }
    public StoryStartLoadingAssetScope AssetScope { get; }
    public IReadOnlyList<string> ResourcePaths { get; }
    public bool PreloadCoverTexture { get; }
    public bool PreloadStoryTextures { get; }
    public bool PreloadAudioData { get; }
    public bool WaitForTextureStreaming { get; }
    public bool UseUnscaledTime { get; }
    public float TextureStreamingTimeout { get; }
    public int AssetsPerFrame { get; }
    public float AsyncOperationProgressScale { get; }
    public string LoadingMediaStatus { get; }
    public string CoverStatus { get; }
    public string TextureStatus { get; }
    public string AudioStatus { get; }
    public string VideoStatus { get; }
    public string DataStatus { get; }
    public string ResourcesStatus { get; }

    public StoryStartAssetPreloadRequest CreatePreloadRequest(
        StoryStartPreloadAssetSet assets,
        StoryLoadingMediaSelection loadingMedia)
    {
        return new StoryStartAssetPreloadRequest(
            assets,
            loadingMedia,
            ResourcePaths,
            PreloadCoverTexture,
            PreloadStoryTextures,
            PreloadAudioData,
            WaitForTextureStreaming,
            UseUnscaledTime,
            TextureStreamingTimeout,
            AssetsPerFrame,
            AsyncOperationProgressScale,
            CoverStatus,
            TextureStatus,
            AudioStatus,
            VideoStatus,
            DataStatus,
            ResourcesStatus);
    }
}

public readonly struct StoryStartLoadingFlowResult
{
    public StoryStartLoadingFlowResult(
        StoryLoadingMediaLease loadingMedia,
        StoryLoadingMediaSelection selectedMedia,
        StoryStartPreloadAssetSet assets)
    {
        LoadingMedia = loadingMedia;
        SelectedMedia = selectedMedia;
        Assets = assets ?? new StoryStartPreloadAssetSet();
    }

    public StoryLoadingMediaLease LoadingMedia { get; }
    public StoryLoadingMediaSelection SelectedMedia { get; }
    public StoryStartPreloadAssetSet Assets { get; }
}

public sealed class StoryStartLoadingFlow : IStoryStartLoadingFlow
{
    private readonly IStoryLoadingMediaService _loadingMediaService;
    private readonly IStoryLoadingMediaPolicy _loadingMediaPolicy;
    private readonly IStoryStartPreloadAssetCollector _assetCollector;
    private readonly IStoryStartAssetPreloadService _assetPreloadService;

    public StoryStartLoadingFlow()
        : this(
            StoryLoadingMediaServices.Shared,
            StoryLoadingMediaPolicies.Shared,
            StoryStartPreloadAssetCollectors.Shared,
            StoryStartAssetPreloadServices.Shared)
    {
    }

    public StoryStartLoadingFlow(
        IStoryLoadingMediaService loadingMediaService,
        IStoryLoadingMediaPolicy loadingMediaPolicy,
        IStoryStartPreloadAssetCollector assetCollector,
        IStoryStartAssetPreloadService assetPreloadService)
    {
        _loadingMediaService = loadingMediaService ?? StoryLoadingMediaServices.Shared;
        _loadingMediaPolicy = loadingMediaPolicy ?? StoryLoadingMediaPolicies.Shared;
        _assetCollector = assetCollector ?? StoryStartPreloadAssetCollectors.Shared;
        _assetPreloadService = assetPreloadService ?? StoryStartAssetPreloadServices.Shared;
    }

    public async UniTask<StoryStartLoadingFlowResult> RunAsync(
        StoryStartLoadingFlowRequest request,
        IProgress<StoryStartPreloadProgress> progress,
        IStoryStartLoadingFlowObserver observer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StoryLoadingMediaLease loadingMedia = null;
        bool mediaTransferredToObserver = false;

        try
        {
            if (!string.IsNullOrWhiteSpace(request.LoadingMediaStatus))
            {
                progress?.Report(new StoryStartPreloadProgress(
                    0,
                    1,
                    0f,
                    request.LoadingMediaStatus,
                    StoryStartPreloadStage.LoadingMedia));
            }

            loadingMedia = await _loadingMediaService.LoadAsync(request.Data, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (observer != null)
            {
                mediaTransferredToObserver = observer.OnLoadingMediaLoaded(request.Data, loadingMedia);
                if (!mediaTransferredToObserver)
                    throw new OperationCanceledException("Story loading media observer rejected the media lease.", cancellationToken);
            }

            StoryStartPreloadAssetSet assets = _assetCollector.Collect(
                request.Data,
                request.AssetScope,
                loadingMedia);

            StoryLoadingMediaSelection selectedMedia = _loadingMediaPolicy.SelectForPresentation(
                request.Data,
                loadingMedia);

            StoryStartAssetPreloadRequest preloadRequest = request.CreatePreloadRequest(assets, selectedMedia);
            await _assetPreloadService.PreloadAsync(preloadRequest, progress, cancellationToken);

            return new StoryStartLoadingFlowResult(loadingMedia, selectedMedia, assets);
        }
        catch
        {
            if (!mediaTransferredToObserver)
                loadingMedia?.Dispose();
            throw;
        }
    }
}

public static class StoryStartLoadingFlows
{
    private static readonly IStoryStartLoadingFlow SharedFlow = new StoryStartLoadingFlow(
        StoryLoadingMediaServices.Shared,
        StoryLoadingMediaPolicies.Shared,
        StoryStartPreloadAssetCollectors.Shared,
        StoryStartAssetPreloadServices.Shared);

    public static IStoryStartLoadingFlow Shared => SharedFlow;
}
