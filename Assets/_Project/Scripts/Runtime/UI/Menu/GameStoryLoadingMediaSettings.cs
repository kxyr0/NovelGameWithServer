using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Video;

[Serializable]
public sealed class GameStoryLoadingMediaSettings
{
    [SerializeField]
    [Tooltip("When enabled, StoryStartLoadingScreen uses these lazy references instead of the menu card cover.")]
    private bool _overrideLoadingMedia;

    [SerializeField]
    [Tooltip("If custom loading media is empty or fails to load, keep the old GameIcon/GameIconVideo/GameIconGif fallback.")]
    private bool _fallbackToMenuMediaWhenEmpty = true;

    [Header("Addressables")]
    [SerializeField]
    [Tooltip("Addressable sprite loaded only while this story start loading screen is shown.")]
    private AssetReferenceSprite _imageReference = new AssetReferenceSprite("");

    [SerializeField]
    [Tooltip("Addressable VideoClip loaded only while this story start loading screen is shown.")]
    private AssetReferenceVideoClip _videoReference = new AssetReferenceVideoClip("");

    [SerializeField]
    [Tooltip("Addressable TextAsset with GIF bytes loaded only while this story start loading screen is shown.")]
    private AssetReferenceTextAsset _gifReference = new AssetReferenceTextAsset("");

    [Header("Direct fallback")]
    [SerializeField]
    [Tooltip("Optional direct sprite fallback. Prefer Addressables above for heavy files.")]
    private Sprite _imageFallback;

    [SerializeField]
    [Tooltip("Optional direct video fallback. Prefer Addressables above for heavy files.")]
    private VideoClip _videoFallback;

    [SerializeField]
    [Tooltip("Optional direct GIF bytes fallback. Prefer Addressables above for heavy files.")]
    private TextAsset _gifFallback;

    public bool OverrideLoadingMedia => _overrideLoadingMedia;
    public bool FallbackToMenuMediaWhenEmpty => _fallbackToMenuMediaWhenEmpty;
    public AssetReferenceSprite ImageReference
    {
        get
        {
            EnsureInitialized();
            return _imageReference;
        }
    }

    public AssetReferenceVideoClip VideoReference
    {
        get
        {
            EnsureInitialized();
            return _videoReference;
        }
    }

    public AssetReferenceTextAsset GifReference
    {
        get
        {
            EnsureInitialized();
            return _gifReference;
        }
    }
    public Sprite ImageFallback => _imageFallback;
    public VideoClip VideoFallback => _videoFallback;
    public TextAsset GifFallback => _gifFallback;

    public bool HasAddressableMedia =>
        HasAddressableReference(ImageReference) ||
        HasAddressableReference(VideoReference) ||
        HasAddressableReference(GifReference);

    public bool HasDirectMedia => _imageFallback != null || _videoFallback != null || _gifFallback != null;
    public bool HasAnyCustomMedia => HasAddressableMedia || HasDirectMedia;
    public bool ShouldUseCustomMedia => _overrideLoadingMedia && HasAnyCustomMedia;
    public bool HasAddressableReferenceForEveryDirectFallback =>
        (_imageFallback == null || HasAddressableReference(ImageReference)) &&
        (_videoFallback == null || HasAddressableReference(VideoReference)) &&
        (_gifFallback == null || HasAddressableReference(GifReference));

    public static bool HasAddressableReference(AssetReference reference)
    {
        return reference != null && reference.RuntimeKeyIsValid();
    }

    public void EnsureInitialized()
    {
        _imageReference ??= new AssetReferenceSprite("");
        _videoReference ??= new AssetReferenceVideoClip("");
        _gifReference ??= new AssetReferenceTextAsset("");
    }

#if UNITY_EDITOR
    public bool ConfigureEditorAddressableMedia(
        Sprite image,
        VideoClip video,
        TextAsset gif,
        bool overwriteExistingReferences)
    {
        EnsureInitialized();

        bool changed = false;
        changed |= AssignEditorAsset(_imageReference, image, overwriteExistingReferences);
        changed |= AssignEditorAsset(_videoReference, video, overwriteExistingReferences);
        changed |= AssignEditorAsset(_gifReference, gif, overwriteExistingReferences);

        if (HasAnyCustomMedia && !_overrideLoadingMedia)
        {
            _overrideLoadingMedia = true;
            changed = true;
        }

        if (!_fallbackToMenuMediaWhenEmpty)
        {
            _fallbackToMenuMediaWhenEmpty = true;
            changed = true;
        }

        return changed;
    }

    public Sprite ResolveEditorImageCandidate(Sprite menuFallback)
    {
        return _imageFallback != null ? _imageFallback : menuFallback;
    }

    public VideoClip ResolveEditorVideoCandidate(VideoClip menuFallback)
    {
        return _videoFallback != null ? _videoFallback : menuFallback;
    }

    public TextAsset ResolveEditorGifCandidate(TextAsset menuFallback)
    {
        return _gifFallback != null ? _gifFallback : menuFallback;
    }

    public bool ClearEditorDirectFallbackMediaWithAddressableReferences()
    {
        EnsureInitialized();

        bool changed = false;
        if (_imageFallback != null && HasAddressableReference(_imageReference))
        {
            _imageFallback = null;
            changed = true;
        }

        if (_videoFallback != null && HasAddressableReference(_videoReference))
        {
            _videoFallback = null;
            changed = true;
        }

        if (_gifFallback != null && HasAddressableReference(_gifReference))
        {
            _gifFallback = null;
            changed = true;
        }

        return changed;
    }

    private static bool AssignEditorAsset(AssetReference reference, UnityEngine.Object asset, bool overwriteExistingReference)
    {
        if (reference == null || asset == null)
            return false;

        if (!overwriteExistingReference && HasAddressableReference(reference))
            return false;

        string previousGuid = reference.AssetGUID;
        string previousSubObject = reference.SubObjectName;
        UnityEngine.Object previousAsset = reference.editorAsset;

        bool assigned = reference.SetEditorAsset(asset);
        if (!assigned)
            return false;

        if (asset is Sprite)
            reference.SetEditorSubObject(asset);

        return previousGuid != reference.AssetGUID ||
            previousSubObject != reference.SubObjectName ||
            previousAsset != reference.editorAsset;
    }
#endif
}
