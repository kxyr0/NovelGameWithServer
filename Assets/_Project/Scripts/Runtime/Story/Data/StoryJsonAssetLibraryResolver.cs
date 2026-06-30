using UnityEngine;
using UnityEngine.Video;

public sealed class StoryJsonAssetLibraryResolver : StoryJsonAssetResolver
{
    private readonly StoryJsonAssetLibrary _library;
    private readonly StoryJsonAssetResolver _fallback;

    public StoryJsonAssetLibraryResolver(StoryJsonAssetLibrary library, StoryJsonAssetResolver fallback = null)
    {
        _library = library;
        _fallback = fallback;
    }

    public override string CacheKey
    {
        get
        {
            string libraryKey = _library != null ? _library.GetInstanceID().ToString() : "none";
            string fallbackKey = _fallback != null ? _fallback.CacheKey : "default";
            return "library:" + libraryKey + ":" + fallbackKey;
        }
    }

    public override CharacterData ResolveCharacter(string id, string displayName = null)
    {
        var character = _library != null ? _library.FindCharacter(id) : null;
        if (character != null)
            return character;

        return _fallback != null ? _fallback.ResolveCharacter(id, displayName) : base.ResolveCharacter(id, displayName);
    }

    public override ClothingItem ResolveClothing(string id)
    {
        var clothing = _library != null ? _library.FindClothing(id) : null;
        if (clothing != null)
            return clothing;

        return _fallback != null ? _fallback.ResolveClothing(id) : base.ResolveClothing(id);
    }

    public override Sprite ResolveSprite(string id)
    {
        var sprite = _library != null ? _library.FindSprite(id) : null;
        if (sprite != null)
            return sprite;

        return _fallback != null ? _fallback.ResolveSprite(id) : base.ResolveSprite(id);
    }

    public override VideoClip ResolveVideoClip(string id)
    {
        var video = _library != null ? _library.FindVideoClip(id) : null;
        if (video != null)
            return video;

        return _fallback != null ? _fallback.ResolveVideoClip(id) : base.ResolveVideoClip(id);
    }

    public override TextAsset ResolveTextAsset(string id)
    {
        var textAsset = _library != null ? _library.FindTextAsset(id) : null;
        if (textAsset != null)
            return textAsset;

        return _fallback != null ? _fallback.ResolveTextAsset(id) : base.ResolveTextAsset(id);
    }

    public override AudioClip ResolveAudioClip(string id)
    {
        var audio = _library != null ? _library.FindAudioClip(id) : null;
        if (audio != null)
            return audio;

        return _fallback != null ? _fallback.ResolveAudioClip(id) : base.ResolveAudioClip(id);
    }

    public override DialogueStyle ResolveDialogueStyle(string id)
    {
        var style = _library != null ? _library.FindDialogueStyle(id) : null;
        if (style != null)
            return style;

        return _fallback != null ? _fallback.ResolveDialogueStyle(id) : base.ResolveDialogueStyle(id);
    }

    public override string GetAssetId(Object asset)
    {
        string id = _library != null ? _library.FindIdForAsset(asset) : "";
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        return _fallback != null ? _fallback.GetAssetId(asset) : base.GetAssetId(asset);
    }
}
