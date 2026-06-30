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
    void ApplyClothingPreviewLayout(Image image, ClothingItem item)
    {
        if (image == null || item == null)
            return;

        RectTransform rect = image.rectTransform;

        if (TryApplyDefaultClothingPreviewTransform(image, item))
        {
            return;
        }

        ClothingWardrobePreviewLayout layout = item.GetWardrobePreviewLayout(PlayerAppearance.CurrentAppearance);
        rect.anchoredPosition3D = new Vector3(layout.Offset.x, layout.Offset.y, 0f);

        Vector2 previewSize = layout.Size;
        if (previewSize.x > 0f && previewSize.y > 0f)
            rect.sizeDelta = previewSize;
        else
            rect.sizeDelta = GetFallbackPreviewSize(image, item.sprite);

        rect.localScale = NormalizeScale(layout.Scale);

        image.preserveAspect = layout.PreserveAspect;

        CharacterData character = GetActivePreviewCharacter();
        ApplyWardrobeCharacterLayout(image, character != null ? character.GetWardrobeEquipmentLayout(item, item.type) : null);
    }

    bool TryApplyDefaultClothingPreviewTransform(Image image, ClothingItem item)
    {
        if (image == null || item == null)
            return false;

        if (IsRuntimeDefaultClothing(item, ClothingType.Outfit))
            return TryApplyConfiguredDefaultClothingPreviewTransform(
                image,
                item,
                _useDefaultOutfitPreviewTransform,
                _defaultOutfitPreviewPosition,
                _defaultOutfitPreviewWidth,
                _defaultOutfitPreviewHeight,
                _defaultOutfitPreviewScale);

        if (IsRuntimeDefaultClothing(item, ClothingType.Hair))
            return TryApplyConfiguredDefaultClothingPreviewTransform(
                image,
                item,
                _useDefaultHairPreviewTransform,
                _defaultHairPreviewPosition,
                _defaultHairPreviewWidth,
                _defaultHairPreviewHeight,
                _defaultHairPreviewScale);

        return false;
    }

    bool TryApplyConfiguredDefaultClothingPreviewTransform(
        Image image,
        ClothingItem item,
        bool enabled,
        Vector3 position,
        float width,
        float height,
        Vector3 scale)
    {
        if (!enabled)
            return false;

        RectTransform rect = image.rectTransform;
        rect.anchoredPosition3D = position;
        rect.sizeDelta = width > 0f && height > 0f ? new Vector2(width, height) : GetFallbackPreviewSize(image, item.sprite);
        rect.localScale = NormalizeScale(scale);
        image.preserveAspect = item.wardrobePreserveAspect;
        return true;
    }

    static Vector3 NormalizeScale(Vector3 scale)
    {
        scale.x = Mathf.Approximately(scale.x, 0f) ? 1f : scale.x;
        scale.y = Mathf.Approximately(scale.y, 0f) ? 1f : scale.y;
        scale.z = Mathf.Approximately(scale.z, 0f) ? 1f : scale.z;
        return scale;
    }

    void ApplyNeutralClothingPreviewLayout(Image image)
    {
        if (image == null)
            return;

        RectTransform rect = image.rectTransform;
        rect.anchoredPosition3D = Vector3.zero;
        rect.localScale = Vector3.one;

        LayerDefaults defaults = GetLayerDefaults(image);
        rect.sizeDelta = defaults.SizeDelta;
        image.preserveAspect = defaults.PreserveAspect;
    }

    Vector2 GetFallbackPreviewSize(Image image, Sprite sprite)
    {
        if (sprite != null && sprite.rect.width > 0f && sprite.rect.height > 0f)
            return new Vector2(sprite.rect.width, sprite.rect.height);

        LayerDefaults defaults = GetLayerDefaults(image);
        return defaults.SizeDelta;
    }

    void ApplyAppearancePreviewLayout(Image image, AppearanceVariant variant)
    {
        if (image == null)
            return;

        LayerDefaults defaults = GetLayerDefaults(image);
        RectTransform rect = image.rectTransform;

        if (variant == null)
        {
            ApplyDefaultPreviewLayout(image);
            CharacterData character = GetActivePreviewCharacter();
            ApplyWardrobeCharacterLayout(image, character != null ? character.GetWardrobeBodyLayout() : null);
            return;
        }

        rect.anchoredPosition = defaults.AnchoredPosition + variant.previewOffset;

        Vector2 previewSize = variant.GetPreviewSize();
        if (previewSize.x > 0f && previewSize.y > 0f)
            rect.sizeDelta = previewSize;
        else
            rect.sizeDelta = defaults.SizeDelta;

        rect.localScale = defaults.LocalScale;
        image.preserveAspect = variant.previewPreserveAspect;

        CharacterData activeCharacter = GetActivePreviewCharacter();
        ApplyWardrobeCharacterLayout(image, activeCharacter != null ? activeCharacter.GetWardrobeBodyLayout() : null);
    }

    void ApplyWardrobeCharacterLayout(Image image, StoryLayerLayout layout)
    {
        if (image == null || layout == null || !layout.HasCustomLayout())
            return;

        RectTransform rect = image.rectTransform;
        rect.anchoredPosition += layout.offset;

        Vector2 size = rect.sizeDelta;
        if (layout.width > 0f)
            size.x = layout.width;
        if (layout.height > 0f)
            size.y = layout.height;
        rect.sizeDelta = size;

        Vector3 scale = NormalizeScale(layout.scale);
        rect.localScale = new Vector3(
            rect.localScale.x * scale.x,
            rect.localScale.y * scale.y,
            rect.localScale.z * scale.z);
        image.preserveAspect = layout.preserveAspect;
    }

    void ApplyDefaultPreviewLayout(Image image)
    {
        if (image == null)
            return;

        LayerDefaults defaults = GetLayerDefaults(image);
        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = defaults.AnchoredPosition;
        rect.sizeDelta = defaults.SizeDelta;
        rect.localScale = defaults.LocalScale;
        image.preserveAspect = defaults.PreserveAspect;
    }

    LayerDefaults GetLayerDefaults(Image image)
    {
        if (image == _bodyPreviewImage && _bodyLayerDefaults.Captured)
            return _bodyLayerDefaults;
        if (image == _outfitPreviewImage && _outfitLayerDefaults.Captured)
            return _outfitLayerDefaults;
        if (image == _hairPreviewImage && _hairLayerDefaults.Captured)
            return _hairLayerDefaults;
        if (image == _accessoryPreviewImage && _accessoryLayerDefaults.Captured)
            return _accessoryLayerDefaults;

        return LayerDefaults.Capture(image);
    }

    void EnsureLayerOrder()
    {
        if (_bodyPreviewImage != null)
            _bodyPreviewImage.transform.SetAsFirstSibling();

        if (_outfitPreviewImage != null)
            _outfitPreviewImage.transform.SetSiblingIndex(_bodyPreviewImage != null ? _bodyPreviewImage.transform.GetSiblingIndex() + 1 : 0);

        if (_accessoryPreviewImage != null)
            _accessoryPreviewImage.transform.SetSiblingIndex(_outfitPreviewImage != null ? _outfitPreviewImage.transform.GetSiblingIndex() + 1 : 0);

        if (_hairPreviewImage != null)
            _hairPreviewImage.transform.SetAsLastSibling();
    }

    bool IsLayeredPreviewObject(GameObject target)
    {
        if (target == null)
            return false;

        return ContainsLayer(target, _bodyPreviewImage) ||
               ContainsLayer(target, _outfitPreviewImage) ||
               ContainsLayer(target, _hairPreviewImage) ||
               ContainsLayer(target, _accessoryPreviewImage);
    }

    static bool ContainsLayer(GameObject target, Image layer)
    {
        if (target == null || layer == null)
            return false;

        Transform targetTransform = target.transform;
        Transform layerTransform = layer.transform;
        return targetTransform == layerTransform || layerTransform.IsChildOf(targetTransform);
    }
}
