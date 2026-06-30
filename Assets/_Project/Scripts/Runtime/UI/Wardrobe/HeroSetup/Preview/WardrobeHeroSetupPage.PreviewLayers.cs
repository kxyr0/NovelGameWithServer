using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed partial class WardrobeHeroSetupPage
{
    void SetPreviewLayer(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        if (!image.gameObject.activeSelf)
            image.gameObject.SetActive(true);

        SetPreviewLayerCanvasGroupVisible(image);
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    void ClearLayeredPreview()
    {
        ApplyDefaultPreviewLayout(_bodyPreviewImage);
        ApplyNeutralClothingPreviewLayout(_outfitPreviewImage);
        ApplyNeutralClothingPreviewLayout(_hairPreviewImage);
        ApplyNeutralClothingPreviewLayout(_accessoryPreviewImage);

        EnsurePreviewLayerVisible(_bodyPreviewImage);
        ApplyEquippedClothingPreview(_outfitPreviewImage != _previewImage ? _outfitPreviewImage : null, ClothingType.Outfit);
        ApplyEquippedClothingPreview(_hairPreviewImage != _previewImage ? _hairPreviewImage : null, ClothingType.Hair);
        ApplyEquippedClothingPreview(_accessoryPreviewImage != _previewImage ? _accessoryPreviewImage : null, ClothingType.Accessory);

        if (_previewImage != null &&
            _previewImage != _bodyPreviewImage &&
            _previewImage != _outfitPreviewImage &&
            _previewImage != _hairPreviewImage &&
            _previewImage != _accessoryPreviewImage)
        {
            SetPreview(null);
        }
    }

    bool ShouldPreserveDefaultLayerTransform(ClothingType type)
    {
        ClothingItem defaultItem = GetDefaultClothing(type);
        if (defaultItem == null)
            return false;

        if (type == ClothingType.Outfit && _useDefaultOutfitPreviewTransform)
            return false;

        if (type == ClothingType.Hair && _useDefaultHairPreviewTransform)
            return false;

        string slotSuffix = GetSlotSuffix(type);
        string savedId = GetSavedClothingId(type, slotSuffix);
        return string.IsNullOrWhiteSpace(savedId) || MatchesClothing(defaultItem, savedId, type);
    }

    void ApplyEquippedClothingPreview(Image target, ClothingType type)
    {
        if (target == null)
            return;

        string slotSuffix = GetSlotSuffix(type);
        string savedId = GetSavedClothingId(type, slotSuffix);
        ClothingItem item = null;

        if (!string.IsNullOrWhiteSpace(savedId))
            TryFindClothing(savedId, type, out item);

        if (item == null)
        {
            ClothingItem defaultItem = GetDefaultClothing(type);
            if (IsClothingUsable(defaultItem, type, requireId: false) && IsClothingAllowedForTarget(defaultItem))
                item = defaultItem;
        }

        if (item != null)
        {
            ApplyClothingPreviewToLayer(target, item);
            RememberCurrentClothingPreview(item);
            return;
        }

        SetPreviewLayer(target, GetPlayerAppearanceClothingSprite(type));
    }

    void PrepareLayeredPreviewForOpen(bool hideUntilReady = false)
    {
        KillLayeredPreviewOpenTween(false);
        SetLayeredPreviewOpenVisible(!hideUntilReady);
        EnsureLayerOrder();
        ClearLayeredPreview();
        RefreshBodyPreviewFromCharacter(true);
    }

    void RevealLayeredPreviewAfterOpen()
    {
        CanvasGroup group = GetOrAddLayeredPreviewCanvasGroup();
        if (group == null)
            return;

        KillLayeredPreviewOpenTween(false);

        if (!Application.isPlaying || LayeredPreviewOpenFadeDuration <= 0f)
        {
            group.alpha = 1f;
            return;
        }

        group.alpha = 0f;
        _layeredPreviewOpenTween = group
            .DOFade(1f, LayeredPreviewOpenFadeDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(_useUnscaledOptionAnimation)
            .SetTarget(this)
            .OnComplete(() =>
            {
                group.alpha = 1f;
                _layeredPreviewOpenTween = null;
            });
    }

    void SetLayeredPreviewOpenVisible(bool visible)
    {
        CanvasGroup group = GetOrAddLayeredPreviewCanvasGroup();
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    void KillLayeredPreviewOpenTween(bool restore)
    {
        if (_layeredPreviewOpenTween != null)
        {
            _layeredPreviewOpenTween.Kill();
            _layeredPreviewOpenTween = null;
        }

        if (restore && _layeredPreviewCanvasGroup != null)
            _layeredPreviewCanvasGroup.alpha = 1f;
    }

    CanvasGroup GetOrAddLayeredPreviewCanvasGroup()
    {
        RectTransform root = GetLayeredPreviewOpenFadeRoot();
        if (root == null)
            return null;

        if (_layeredPreviewCanvasGroup != null && _layeredPreviewCanvasGroup.transform == root)
            return _layeredPreviewCanvasGroup;

        _layeredPreviewCanvasGroup = root.GetComponent<CanvasGroup>();
        if (_layeredPreviewCanvasGroup == null)
            _layeredPreviewCanvasGroup = root.gameObject.AddComponent<CanvasGroup>();

        _layeredPreviewCanvasGroup.interactable = false;
        _layeredPreviewCanvasGroup.blocksRaycasts = false;
        return _layeredPreviewCanvasGroup;
    }

    RectTransform GetLayeredPreviewOpenFadeRoot()
    {
        Transform layerParent = _bodyPreviewImage != null ? _bodyPreviewImage.transform.parent : null;
        if (layerParent != null &&
            ContainsLayer(layerParent.gameObject, _bodyPreviewImage) &&
            ContainsLayer(layerParent.gameObject, _outfitPreviewImage) &&
            ContainsLayer(layerParent.gameObject, _hairPreviewImage) &&
            ContainsOptionalLayer(layerParent.gameObject, _accessoryPreviewImage))
        {
            return layerParent as RectTransform;
        }

        if (_setupContentRoot != null &&
            ContainsLayer(_setupContentRoot, _bodyPreviewImage) &&
            ContainsLayer(_setupContentRoot, _outfitPreviewImage) &&
            ContainsLayer(_setupContentRoot, _hairPreviewImage) &&
            ContainsOptionalLayer(_setupContentRoot, _accessoryPreviewImage))
        {
            return _setupContentRoot.transform as RectTransform;
        }

        if (_bodyPreviewImage != null)
            return _bodyPreviewImage.rectTransform;

        return _previewImage != null ? _previewImage.rectTransform : null;
    }

    void RefreshBodyPreviewFromCharacter(bool force = false)
    {
        CharacterData character = GetActivePreviewCharacter();
        if (_bodyPreviewImage == null || (!force && _bodyPreviewImage.sprite != null) || character == null)
            return;

        AppearanceVariant variant = character.GetVariantForCurrentAppearance();
        Sprite bodySprite = GetAppearancePreviewSprite(variant, character.GetBodySprite());
        if (bodySprite != null)
        {
            SetPreviewLayer(_bodyPreviewImage, bodySprite);
            ApplyAppearancePreviewLayout(_bodyPreviewImage, variant);
        }
    }

    CharacterData GetActivePreviewCharacter()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && _editorPreviewCharacterOverride != null)
            return _editorPreviewCharacterOverride;
#endif

        return GetRuntimeTargetCharacter();
    }

    bool UsesCharacterForPreview(CharacterData character)
    {
        if (character == null)
            return false;

#if UNITY_EDITOR
        if (!Application.isPlaying && _editorPreviewCharacterOverride == character)
            return true;
#endif

        return GetRuntimeTargetCharacter() == character;
    }

    bool UsesDedicatedPreviewLayers()
    {
        return (_bodyPreviewImage != null && _bodyPreviewImage != _previewImage) ||
               (_outfitPreviewImage != null && _outfitPreviewImage != _previewImage) ||
               (_hairPreviewImage != null && _hairPreviewImage != _previewImage) ||
               (_accessoryPreviewImage != null && _accessoryPreviewImage != _previewImage);
    }

    void EnsurePreviewLayerVisible(Image image)
    {
        if (image == null)
            return;

        if (image == _bodyPreviewImage && image.sprite == null)
            RefreshBodyPreviewFromCharacter();

        if (!image.gameObject.activeSelf)
            image.gameObject.SetActive(true);

        SetPreviewLayerCanvasGroupVisible(image);
        image.enabled = image.sprite != null;
    }

    void SetPreviewLayerCanvasGroupVisible(Image image)
    {
        if (image == null)
            return;

        CanvasGroup canvasGroup = image.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    void CaptureLayerDefaults()
    {
        CaptureLayerDefaults(_bodyPreviewImage, ref _bodyLayerDefaults);
        CaptureLayerDefaults(_outfitPreviewImage, ref _outfitLayerDefaults);
        CaptureLayerDefaults(_hairPreviewImage, ref _hairLayerDefaults);
        CaptureLayerDefaults(_accessoryPreviewImage, ref _accessoryLayerDefaults);
    }

    void CaptureLayerDefaults(Image image, ref LayerDefaults defaults)
    {
        if (image == null || defaults.Captured)
            return;

        defaults = LayerDefaults.Capture(image);
    }

    static bool ContainsOptionalLayer(GameObject target, Image layer)
    {
        return layer == null || ContainsLayer(target, layer);
    }
}
