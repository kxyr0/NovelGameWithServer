using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public sealed class StoryStartPreloadAssetSet
{
    private readonly HashSet<Sprite> _sprites = new HashSet<Sprite>();
    private readonly HashSet<Texture> _textures = new HashSet<Texture>();
    private readonly HashSet<AudioClip> _audioClips = new HashSet<AudioClip>();
    private readonly HashSet<VideoClip> _videoClips = new HashSet<VideoClip>();
    private readonly HashSet<TextAsset> _textAssets = new HashSet<TextAsset>();
    private readonly HashSet<TextAsset> _gifAssets = new HashSet<TextAsset>();

    public IEnumerable<Sprite> Sprites => _sprites;
    public IEnumerable<Texture> Textures => _textures;
    public IEnumerable<AudioClip> AudioClips => _audioClips;
    public IEnumerable<VideoClip> VideoClips => _videoClips;
    public IEnumerable<TextAsset> TextAssets => _textAssets;
    public IEnumerable<TextAsset> GifAssets => _gifAssets;

    public int TotalCount => _sprites.Count + _textures.Count + _audioClips.Count + _videoClips.Count + _textAssets.Count + _gifAssets.Count;

    public void Add(Sprite sprite)
    {
        if (sprite != null)
            _sprites.Add(sprite);
    }

    public void Add(Texture texture)
    {
        if (texture != null)
            _textures.Add(texture);
    }

    public void Add(AudioClip clip)
    {
        if (clip != null)
            _audioClips.Add(clip);
    }

    public void Add(VideoClip clip)
    {
        if (clip != null)
            _videoClips.Add(clip);
    }

    public void Add(TextAsset textAsset)
    {
        if (textAsset != null && !_gifAssets.Contains(textAsset))
            _textAssets.Add(textAsset);
    }

    public void AddGif(TextAsset gifAsset)
    {
        if (gifAsset == null)
            return;

        _textAssets.Remove(gifAsset);
        _gifAssets.Add(gifAsset);
    }
}

public static class StoryUiStylePreloadExtensions
{
    public static void CollectPreloadAssets(this StoryUiStyle style, StoryStartPreloadAssetSet assets)
    {
        if (style == null || assets == null)
            return;

        foreach (Sprite sprite in StoryUiStyleSpriteReflection.EnumerateSprites(style))
            assets.Add(sprite);
    }
}

internal static class StoryUiStyleSpriteReflection
{
    public static IEnumerable<Sprite> EnumerateSprites(StoryUiStyle style)
    {
        if (style == null)
            yield break;

        var fields = style.GetType().GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].FieldType != typeof(Sprite))
                continue;

            if (fields[i].GetValue(style) is Sprite sprite && sprite != null)
                yield return sprite;
        }
    }
}
