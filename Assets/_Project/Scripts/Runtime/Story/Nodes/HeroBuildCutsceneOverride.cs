using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[Serializable]
public readonly struct HeroBuildCutsceneMedia
{
    public readonly Sprite Image;
    public readonly VideoClip Video;
    public readonly TextAsset Gif;

    public HeroBuildCutsceneMedia(Sprite image, VideoClip video, TextAsset gif)
    {
        Image = image;
        Video = video;
        Gif = gif;
    }

    public bool HasMedia => Image != null || Video != null || Gif != null;
}

[Serializable]
public sealed class HeroBuildCutsceneOverride
{
    [Header("Условие")]
    [SerializeField, Tooltip("Если выключено, правило пропускается и не влияет на выбор катсцены.")]
    private bool _enabled = true;

    [SerializeField, Tooltip("Короткая подпись только для удобства в инспекторе. На игру не влияет.")]
    private string _ruleName;

    [SerializeField, Tooltip("Если включено, правило сработает только для выбранного типажа героини.")]
    private bool _matchAppearance;

    [SerializeField, Tooltip("Типаж героини: European, Asian, Latino, African или Default.")]
    private AppearanceType _appearance = AppearanceType.Default;

    [SerializeField, Tooltip("Одежда героини. Можно назначить ClothingItem, тогда ID возьмется из него. Если поле пустое, одежда не проверяется.")]
    private ClothingItem _outfitItem;

    [SerializeField, Tooltip("ID одежды вручную. Используется, если ClothingItem выше не назначен. Оставь пустым, если одежда не важна.")]
    private string _outfitId;

    [SerializeField, Tooltip("Прическа героини. Можно назначить ClothingItem, тогда ID возьмется из него. Если поле пустое, прическа не проверяется.")]
    private ClothingItem _hairItem;

    [SerializeField, Tooltip("ID прически вручную. Используется, если ClothingItem выше не назначен. Например сюда можно вписать ID белых волос.")]
    private string _hairId;

    [SerializeField, Tooltip("Аксессуар героини. Можно назначить ClothingItem, тогда ID возьмется из него. Если поле пустое, аксессуар не проверяется.")]
    private ClothingItem _accessoryItem;

    [SerializeField, Tooltip("ID аксессуара вручную. Используется, если ClothingItem выше не назначен. Оставь пустым, если аксессуар не важен.")]
    private string _accessoryId;

    [Header("Медиа для этой сборки")]
    [SerializeField, Tooltip("Картинка катсцены для этой сборки. Используется, если видео и GIF не назначены.")]
    private Sprite _image;

    [SerializeField, Tooltip("Видео катсцены для этой сборки. Приоритет выше картинки и GIF.")]
    private VideoClip _video;

    [SerializeField, Tooltip("GIF катсцены для этой сборки как TextAsset (.gif.bytes). Используется, если видео не назначено.")]
    private TextAsset _gif;

    [SerializeField, HideInInspector]
    private string _imageAssetId;

    [SerializeField, HideInInspector]
    private string _videoAssetId;

    [SerializeField, HideInInspector]
    private string _gifAssetId;

    public bool Enabled => _enabled;
    public string RuleName => _ruleName ?? "";
    public bool MatchAppearance => _matchAppearance;
    public AppearanceType Appearance => _appearance;
    public string OutfitId => GetConfiguredItemId(_outfitItem, ClothingType.Outfit, _outfitId);
    public string HairId => GetConfiguredItemId(_hairItem, ClothingType.Hair, _hairId);
    public string AccessoryId => GetConfiguredItemId(_accessoryItem, ClothingType.Accessory, _accessoryId);
    public Sprite DefaultImage => _image;
    public VideoClip DefaultVideo => _video;
    public TextAsset DefaultGif => _gif;
    public string ImageAssetId => _imageAssetId ?? "";
    public string VideoAssetId => _videoAssetId ?? "";
    public string GifAssetId => _gifAssetId ?? "";

    public void ConfigureFromJson(
        bool enabled,
        string ruleName,
        bool matchAppearance,
        AppearanceType appearance,
        string outfitId,
        string hairId,
        string accessoryId,
        Sprite image,
        VideoClip video,
        TextAsset gif,
        string imageAssetId,
        string videoAssetId,
        string gifAssetId)
    {
        _enabled = enabled;
        _ruleName = ruleName ?? "";
        _matchAppearance = matchAppearance;
        _appearance = HeroCustomizationState.NormalizeAppearance(appearance);
        _outfitItem = null;
        _hairItem = null;
        _accessoryItem = null;
        _outfitId = SaveDataSanitizer.SanitizeIdentifier(outfitId);
        _hairId = SaveDataSanitizer.SanitizeIdentifier(hairId);
        _accessoryId = SaveDataSanitizer.SanitizeIdentifier(accessoryId);
        _image = image;
        _video = video;
        _gif = gif;
        _imageAssetId = imageAssetId ?? "";
        _videoAssetId = videoAssetId ?? "";
        _gifAssetId = gifAssetId ?? "";
    }

    public bool TryResolve(HeroCustomizationState state, out HeroBuildCutsceneMedia media)
    {
        media = default;

        if (!_enabled)
            return false;

        if (state == null)
            state = HeroCustomizationState.CaptureCurrent();

        state.Normalized();

        if (_matchAppearance &&
            HeroCustomizationState.NormalizeAppearance(_appearance) != state.appearance)
        {
            return false;
        }

        if (!MatchesOptionalId(GetConfiguredItemId(_outfitItem, ClothingType.Outfit, _outfitId), state.outfitId))
            return false;

        if (!MatchesOptionalId(GetConfiguredItemId(_hairItem, ClothingType.Hair, _hairId), state.hairId))
            return false;

        if (!MatchesOptionalId(GetConfiguredItemId(_accessoryItem, ClothingType.Accessory, _accessoryId), state.accessoryId))
            return false;

        media = new HeroBuildCutsceneMedia(_image, _video, _gif);
        return media.HasMedia;
    }

    private static string GetConfiguredItemId(ClothingItem item, ClothingType expectedType, string manualId)
    {
        if (item != null && item.type == expectedType && !string.IsNullOrWhiteSpace(item.id))
            return item.id;

        if (item != null && !string.IsNullOrWhiteSpace(item.id))
            return item.id;

        return manualId;
    }

    private static bool MatchesOptionalId(string requiredId, string currentId)
    {
        string required = SaveDataSanitizer.SanitizeIdentifier(requiredId);
        if (string.IsNullOrEmpty(required))
            return true;

        string current = SaveDataSanitizer.SanitizeIdentifier(currentId);
        return string.Equals(required, current, StringComparison.OrdinalIgnoreCase);
    }
}

public static class HeroBuildCutsceneResolver
{
    public static HeroBuildCutsceneMedia Resolve(
        Sprite fallbackImage,
        VideoClip fallbackVideo,
        TextAsset fallbackGif,
        IList<HeroBuildCutsceneOverride> overrides)
    {
        var fallback = new HeroBuildCutsceneMedia(fallbackImage, fallbackVideo, fallbackGif);

        if (!Application.isPlaying || overrides == null || overrides.Count == 0)
            return fallback;

        HeroCustomizationState state = PlayerAppearance.CaptureState();
        for (int i = 0; i < overrides.Count; i++)
        {
            HeroBuildCutsceneOverride rule = overrides[i];
            if (rule != null && rule.TryResolve(state, out HeroBuildCutsceneMedia media))
                return media;
        }

        return fallback;
    }
}