using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Deterministic resolver for imported stories.
/// It never searches Resources/AssetDatabase when an ID is absent from the library.
/// This prevents a similarly named asset elsewhere in the project from being selected by accident.
/// </summary>
public sealed class StoryJsonStrictAssetLibraryResolver : StoryJsonAssetResolver
{
    readonly StoryJsonAssetLibrary _library;

    public StoryJsonStrictAssetLibraryResolver(StoryJsonAssetLibrary library)
    {
        _library = library;
    }

    public override string CacheKey =>
        "strict-library:" + (_library != null ? _library.GetInstanceID().ToString() : "none");

    public override CharacterData ResolveCharacter(string id, string displayName = null)
    {
        return _library != null ? _library.FindCharacter(id) : null;
    }

    public override ClothingItem ResolveClothing(string id)
    {
        return _library != null ? _library.FindClothing(id) : null;
    }

    public override Sprite ResolveSprite(string id)
    {
        return _library != null ? _library.FindSprite(id) : null;
    }

    public override VideoClip ResolveVideoClip(string id)
    {
        return _library != null ? _library.FindVideoClip(id) : null;
    }

    public override TextAsset ResolveTextAsset(string id)
    {
        return _library != null ? _library.FindTextAsset(id) : null;
    }

    public override AudioClip ResolveAudioClip(string id)
    {
        return _library != null ? _library.FindAudioClip(id) : null;
    }

    public override DialogueStyle ResolveDialogueStyle(string id)
    {
        return _library != null ? _library.FindDialogueStyle(id) : null;
    }

    public override string GetAssetId(Object asset)
    {
        return _library != null ? _library.FindIdForAsset(asset) : "";
    }
}
