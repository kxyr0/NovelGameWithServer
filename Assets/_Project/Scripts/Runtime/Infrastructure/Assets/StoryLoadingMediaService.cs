using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Video;

public interface IStoryLoadingMediaService
{
    UniTask<StoryLoadingMediaLease> LoadAsync(GameData data, CancellationToken cancellationToken);
}

public interface IStoryLoadingMediaAssetLoader
{
    UniTask<T> LoadAddressableOrDefaultAsync<T>(
        AssetReference reference,
        T fallback,
        StoryLoadingMediaLease lease,
        GameData data,
        string label,
        CancellationToken cancellationToken)
        where T : UnityEngine.Object;
}

public sealed class StoryLoadingMediaLease : IDisposable
{
    private readonly List<IDisposable> _trackedHandles = new List<IDisposable>();
    private bool _disposed;

    internal StoryLoadingMediaLease(GameData sourceData)
    {
        SourceData = sourceData;
    }

    public GameData SourceData { get; }
    public Sprite CoverSprite { get; private set; }
    public VideoClip CoverVideo { get; private set; }
    public TextAsset CoverGif { get; private set; }
    public bool UsesCustomMedia { get; private set; }
    public bool UsesMenuFallback { get; private set; }
    public bool HasAddressableHandles => _trackedHandles.Count > 0;
    public bool HasAnyMedia => CoverSprite != null || CoverVideo != null || CoverGif != null;

    internal void SetMedia(StoryLoadingMediaSelection selection)
    {
        CoverSprite = selection.CoverSprite;
        CoverVideo = selection.CoverVideo;
        CoverGif = selection.CoverGif;
        UsesCustomMedia = selection.UsesCustomMedia;
        UsesMenuFallback = selection.UsesMenuFallback;
    }

    public void RegisterOwnedResource(IDisposable handle)
    {
        if (handle == null)
            return;

        if (_disposed)
        {
            handle.Dispose();
            return;
        }

        _trackedHandles.Add(handle);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        for (int i = _trackedHandles.Count - 1; i >= 0; i--)
            _trackedHandles[i]?.Dispose();

        _trackedHandles.Clear();
        CoverSprite = null;
        CoverVideo = null;
        CoverGif = null;
        UsesCustomMedia = false;
        UsesMenuFallback = false;
    }
}

public sealed class StoryLoadingMediaService : IStoryLoadingMediaService
{
    private readonly IStoryLoadingMediaPolicy _policy;
    private readonly IStoryLoadingMediaAssetLoader _assetLoader;

    public StoryLoadingMediaService()
        : this(StoryLoadingMediaPolicies.Shared, StoryLoadingMediaAssetLoaders.Shared)
    {
    }

    public StoryLoadingMediaService(IStoryLoadingMediaPolicy policy)
        : this(policy, StoryLoadingMediaAssetLoaders.Shared)
    {
    }

    public StoryLoadingMediaService(
        IStoryLoadingMediaPolicy policy,
        IStoryLoadingMediaAssetLoader assetLoader)
    {
        _policy = policy ?? StoryLoadingMediaPolicies.Shared;
        _assetLoader = assetLoader ?? StoryLoadingMediaAssetLoaders.Shared;
    }

    public async UniTask<StoryLoadingMediaLease> LoadAsync(GameData data, CancellationToken cancellationToken)
    {
        var lease = new StoryLoadingMediaLease(data);
        if (cancellationToken.IsCancellationRequested)
            return lease;

        try
        {
            if (data == null)
                return lease;

            GameStoryLoadingMediaSettings settings = data.LoadingMedia;
            StoryLoadingMediaSelection selection = _policy.SelectInitial(data);
            lease.SetMedia(selection);

            if (selection.UsesCustomMedia && settings != null && settings.HasAddressableMedia)
            {
                Sprite sprite = selection.UsesMenuFallback ? settings.ImageFallback : selection.CoverSprite;
                VideoClip video = selection.UsesMenuFallback ? settings.VideoFallback : selection.CoverVideo;
                TextAsset gif = selection.UsesMenuFallback ? settings.GifFallback : selection.CoverGif;

                UniTask<Sprite> spriteTask = _assetLoader.LoadAddressableOrDefaultAsync(settings.ImageReference, sprite, lease, data, "loading sprite", cancellationToken);
                UniTask<VideoClip> videoTask = _assetLoader.LoadAddressableOrDefaultAsync(settings.VideoReference, video, lease, data, "loading video", cancellationToken);
                UniTask<TextAsset> gifTask = _assetLoader.LoadAddressableOrDefaultAsync(settings.GifReference, gif, lease, data, "loading gif", cancellationToken);

                (sprite, video, gif) = await UniTask.WhenAll(spriteTask, videoTask, gifTask);
                lease.SetMedia(_policy.SelectLoaded(data, sprite, video, gif));
            }

            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }
}

public sealed class AddressablesStoryLoadingMediaAssetLoader : IStoryLoadingMediaAssetLoader
{
    public async UniTask<T> LoadAddressableOrDefaultAsync<T>(
        AssetReference reference,
        T fallback,
        StoryLoadingMediaLease lease,
        GameData data,
        string label,
        CancellationToken cancellationToken)
        where T : UnityEngine.Object
    {
        if (!GameStoryLoadingMediaSettings.HasAddressableReference(reference))
            return fallback;

        AsyncOperationHandle<T> handle = default;
        bool hasHandle = false;

        try
        {
            handle = Addressables.LoadAssetAsync<T>(reference.RuntimeKey);
            hasHandle = true;

            T loadedAsset = await handle.ToUniTask(
                timing: PlayerLoopTiming.Update,
                cancellationToken: cancellationToken,
                autoReleaseWhenCanceled: false);

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                lease.RegisterOwnedResource(new AddressablesTrackedHandle(handle));
                hasHandle = false;
                return loadedAsset != null ? loadedAsset : fallback;
            }

            Debug.LogWarning($"[StoryLoadingMediaService] Failed to load {label} for '{ResolveName(data)}': {handle.OperationException?.Message}", data);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[StoryLoadingMediaService] Failed to load {label} for '{ResolveName(data)}': {exception.Message}", data);
        }
        finally
        {
            if (hasHandle && handle.IsValid())
                Addressables.Release(handle);
        }

        return fallback;
    }

    private static string ResolveName(GameData data)
    {
        if (data == null)
            return "";

        return !string.IsNullOrWhiteSpace(data.GameName) ? data.GameName : data.name;
    }

    private sealed class AddressablesTrackedHandle : IDisposable
    {
        private readonly AsyncOperationHandle _handle;
        private bool _disposed;

        public AddressablesTrackedHandle(AsyncOperationHandle handle)
        {
            _handle = handle;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_handle.IsValid())
                Addressables.Release(_handle);
        }
    }
}

public static class StoryLoadingMediaAssetLoaders
{
    private static readonly IStoryLoadingMediaAssetLoader SharedLoader = new AddressablesStoryLoadingMediaAssetLoader();

    public static IStoryLoadingMediaAssetLoader Shared => SharedLoader;
}

public static class StoryLoadingMediaServices
{
    private static readonly IStoryLoadingMediaService SharedService = new StoryLoadingMediaService(
        StoryLoadingMediaPolicies.Shared,
        StoryLoadingMediaAssetLoaders.Shared);

    public static IStoryLoadingMediaService Shared => SharedService;
}
