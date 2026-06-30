using UnityEngine;
using UnityEngine.Video;

public interface IStoryLoadingMediaPolicy
{
    StoryLoadingMediaSelection SelectInitial(GameData data);
    StoryLoadingMediaSelection SelectLoaded(GameData data, Sprite sprite, VideoClip video, TextAsset gif);
    StoryLoadingMediaSelection SelectForPresentation(GameData data, StoryLoadingMediaLease loadingMedia);
    bool ShouldPreloadLegacyMenuMedia(GameData data, StoryLoadingMediaLease loadingMedia);
}

public struct StoryLoadingMediaSelection
{
    public StoryLoadingMediaSelection(
        GameData sourceData,
        Sprite coverSprite,
        VideoClip coverVideo,
        TextAsset coverGif,
        bool usesCustomMedia,
        bool usesMenuFallback)
    {
        SourceData = sourceData;
        CoverSprite = coverSprite;
        CoverVideo = coverVideo;
        CoverGif = coverGif;
        UsesCustomMedia = usesCustomMedia;
        UsesMenuFallback = usesMenuFallback;
    }

    public GameData SourceData { get; }
    public Sprite CoverSprite { get; }
    public VideoClip CoverVideo { get; }
    public TextAsset CoverGif { get; }
    public bool UsesCustomMedia { get; }
    public bool UsesMenuFallback { get; }
    public bool HasAnyMedia => CoverSprite != null || CoverVideo != null || CoverGif != null;

    public static StoryLoadingMediaSelection Empty(GameData sourceData)
    {
        return new StoryLoadingMediaSelection(sourceData, null, null, null, false, false);
    }
}

public sealed class StoryLoadingMediaPolicy : IStoryLoadingMediaPolicy
{
    public StoryLoadingMediaSelection SelectInitial(GameData data)
    {
        if (data == null)
            return StoryLoadingMediaSelection.Empty(null);

        GameStoryLoadingMediaSettings settings = data.LoadingMedia;
        if (settings == null || !settings.ShouldUseCustomMedia)
            return SelectLegacy(data);

        if (settings.HasDirectMedia)
        {
            return new StoryLoadingMediaSelection(
                data,
                settings.ImageFallback,
                settings.VideoFallback,
                settings.GifFallback,
                usesCustomMedia: true,
                usesMenuFallback: false);
        }

        return settings.FallbackToMenuMediaWhenEmpty
            ? SelectInterimMenuFallback(data, usesCustomMedia: true)
            : new StoryLoadingMediaSelection(data, null, null, null, usesCustomMedia: true, usesMenuFallback: false);
    }

    public StoryLoadingMediaSelection SelectLoaded(GameData data, Sprite sprite, VideoClip video, TextAsset gif)
    {
        if (data == null)
            return StoryLoadingMediaSelection.Empty(null);

        GameStoryLoadingMediaSettings settings = data.LoadingMedia;
        if (settings == null || !settings.ShouldUseCustomMedia)
            return SelectLegacy(data);

        if (sprite != null || video != null || gif != null)
        {
            return new StoryLoadingMediaSelection(
                data,
                sprite,
                video,
                gif,
                usesCustomMedia: true,
                usesMenuFallback: false);
        }

        return settings.FallbackToMenuMediaWhenEmpty
            ? SelectInterimMenuFallback(data, usesCustomMedia: true)
            : new StoryLoadingMediaSelection(data, null, null, null, usesCustomMedia: true, usesMenuFallback: false);
    }

    public StoryLoadingMediaSelection SelectForPresentation(GameData data, StoryLoadingMediaLease loadingMedia)
    {
        if (loadingMedia != null)
        {
            GameData sourceData = data != null ? data : loadingMedia.SourceData;
            return new StoryLoadingMediaSelection(
                sourceData,
                loadingMedia.CoverSprite,
                loadingMedia.CoverVideo,
                loadingMedia.CoverGif,
                loadingMedia.UsesCustomMedia,
                loadingMedia.UsesMenuFallback);
        }

        return SelectInitial(data);
    }

    public bool ShouldPreloadLegacyMenuMedia(GameData data, StoryLoadingMediaLease loadingMedia)
    {
        if (data == null)
            return false;

        return SelectForPresentation(data, loadingMedia).UsesMenuFallback;
    }

    private static StoryLoadingMediaSelection SelectLegacy(GameData data)
    {
        return SelectInterimMenuFallback(data, usesCustomMedia: false);
    }

    private static StoryLoadingMediaSelection SelectInterimMenuFallback(GameData data, bool usesCustomMedia)
    {
        if (data == null)
            return StoryLoadingMediaSelection.Empty(null);

        return new StoryLoadingMediaSelection(
            data,
            data.GameIcon,
            data.GameIconVideo,
            data.GameIconGif,
            usesCustomMedia,
            usesMenuFallback: true);
    }
}

public static class StoryLoadingMediaPolicies
{
    private static readonly IStoryLoadingMediaPolicy SharedPolicy = new StoryLoadingMediaPolicy();

    public static IStoryLoadingMediaPolicy Shared => SharedPolicy;
}
