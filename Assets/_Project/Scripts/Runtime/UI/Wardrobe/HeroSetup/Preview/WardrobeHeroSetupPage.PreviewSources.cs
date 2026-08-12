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
    void SetStepPreview(RuntimeOption option)
    {
        if (option == null)
        {
            SetPreview(null);
            return;
        }

        Image target = GetPreviewImageForStep(option.Step);
        if (target == null && option.Step != WardrobeHeroSetupStep.Accessories)
            target = _previewImage;

        if (option.ClearsClothingSlot)
        {
            SetPreviewLayer(target, null);
            return;
        }

        if (option.Step == WardrobeHeroSetupStep.Appearance)
        {
            AppearanceVariant variant = GetAppearanceVariant(option.AppearanceType);
            Sprite sprite = GetAppearancePreviewSprite(variant, option.Preview);

            if (sprite != null)
                SetPreviewLayer(target, sprite);
            else
                EnsurePreviewLayerVisible(target);

            option.AppearanceVariant = variant;
            option.Preview = sprite;
            ApplyAppearancePreviewLayout(target, variant);
            return;
        }

        if (option.Preview == null)
        {
            EnsurePreviewLayerVisible(target);
            return;
        }

        if (option.Clothing != null)
        {
            ApplyClothingPreviewToLayer(target, option.Clothing);
            RememberCurrentClothingPreview(option.Clothing);
        }
        else
        {
            SetPreviewLayer(target, option.Preview);
        }
    }

    void RememberCurrentClothingPreview(ClothingItem item)
    {
        if (item == null)
            return;

        switch (item.type)
        {
            case ClothingType.Outfit:
                _currentOutfitPreviewItem = item;
                break;

            case ClothingType.Hair:
                _currentHairPreviewItem = item;
                break;

            case ClothingType.Accessory:
                _currentAccessoryPreviewItem = item;
                break;
        }
    }

    void ApplyClothingPreviewToLayer(Image target, ClothingItem item)
    {
        if (target == null || item == null)
            return;

        SetPreviewLayer(target, item.sprite);
        ApplyClothingPreviewLayout(target, item);
    }

    AppearanceVariant GetAppearanceVariant(AppearanceType type)
    {
        CharacterData character = GetActivePreviewCharacter();
        return character != null ? character.GetAppearanceVariant(type) : null;
    }

    Sprite GetAppearancePreviewSprite(AppearanceVariant variant, Sprite fallback)
    {
        return variant != null && variant.defaultSprite != null ? variant.defaultSprite : fallback;
    }

    void OnClothingItemChanged(ClothingItem item)
    {
        if (item == null)
            return;

        RefreshClothingItemPreviewAfterSourceChange(item);

#if UNITY_EDITOR
        if (!Application.isPlaying && _editorPreviewEnabled)
            QueueEditorPreviewRefresh();
#endif
    }

    bool RefreshClothingItemPreviewAfterSourceChange(ClothingItem item)
    {
        if (item == null)
            return false;

        bool refreshed = false;

        RuntimeOption selected = GetSelectedRuntimeOption();
        if (selected != null && selected.Clothing == item)
        {
            SetStepPreview(selected);
            SetSelectedOptionLabel(selected);
            RefreshAllOptionButtons();
            refreshed = true;
        }

        if (item == _currentOutfitPreviewItem)
        {
            ApplyClothingPreviewToLayer(GetPreviewImageForStep(WardrobeHeroSetupStep.Outfit), item);
            refreshed = true;
        }

        if (item == _currentHairPreviewItem)
        {
            ApplyClothingPreviewToLayer(GetPreviewImageForStep(WardrobeHeroSetupStep.Hair), item);
            refreshed = true;
        }

        if (item == _currentAccessoryPreviewItem)
        {
            ApplyClothingPreviewToLayer(GetPreviewImageForStep(WardrobeHeroSetupStep.Accessories), item);
            refreshed = true;
        }

#if UNITY_EDITOR
        if (refreshed && !Application.isPlaying)
            EditorUtility.SetDirty(this);
#endif

        return refreshed;
    }

    RuntimeOption GetSelectedRuntimeOption()
    {
        if (_currentOptions == null || _currentOptions.Count == 0)
            return null;

        int index = Mathf.Clamp(_selectedOptionIndex, 0, _currentOptions.Count - 1);
        return _currentOptions[index];
    }

    void OnCharacterDataChanged(CharacterData character)
    {
        if (!UsesCharacterForPreview(character))
            return;

        RefreshPreviewAfterSourceChange();
    }

    void RefreshPreviewAfterSourceChange()
    {
        if (_currentOptions != null && _currentOptions.Count > 0)
        {
            int index = Mathf.Clamp(_selectedOptionIndex, 0, _currentOptions.Count - 1);
            SetStepPreview(_currentOptions[index]);
        }
        else
        {
            RefreshBodyPreviewFromCharacter(true);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    void SetPreview(Sprite sprite)
    {
        SetPreviewLayer(_previewImage, sprite);
    }
}
