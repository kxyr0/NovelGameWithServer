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
    bool HasAppearanceOptions()
    {
        if (_useDefaultAppearanceOptionsWhenEmpty)
            return true;

        List<WardrobeHeroAppearanceOption> options = GetRuntimeAppearanceOptions();
        if (options == null)
            return false;

        foreach (var option in options)
        {
            if (IsAppearanceOptionUsable(option))
                return true;
        }

        return false;
    }

    bool IsAppearanceOptionUsable(WardrobeHeroAppearanceOption option)
    {
        if (option == null || !option.enabled)
            return false;

        if (option.type != AppearanceType.Default)
            return true;

        return option.previewSprite != null || GetAppearanceVariant(option.type) != null || !string.IsNullOrWhiteSpace(option.label);
    }

    bool HasClothingOptions(List<ClothingItem> source, ClothingType type)
    {
        if (source == null)
            return false;

        foreach (var item in source)
        {
            if (item != null && item.type == type && IsClothingAllowedForTarget(item))
                return true;
        }

        return false;
    }

    static ClothingItem FindClothingInList(List<ClothingItem> source, string id, ClothingType type)
    {
        if (source == null || string.IsNullOrWhiteSpace(id))
            return null;

        foreach (ClothingItem item in source)
        {
            if (item == null || item.type != type)
                continue;

            if (string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.name, id, StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
    }

    ClothingItem FindAllowedClothingInList(List<ClothingItem> source, string id, ClothingType type)
    {
        ClothingItem item = FindClothingInList(source, id, type);
        return IsClothingAllowedForTarget(item) ? item : null;
    }

    ClothingItem FindDefaultClothing(string id, ClothingType type)
    {
        ClothingItem defaultItem = GetDefaultClothing(type);
        return MatchesClothing(defaultItem, id, type) && IsClothingAllowedForTarget(defaultItem) ? defaultItem : null;
    }

    ClothingItem GetDefaultClothing(ClothingType type)
    {
        ClothingItem configured = GetRuntimeDefaultClothing(type);

        if (IsClothingUsable(configured, type, requireId: false) && IsClothingAllowedForTarget(configured))
            return configured;

        return _useFirstAvailableClothingAsFallback
            ? FindFallbackClothing(type)
            : null;
    }

    ClothingItem FindFallbackClothing(ClothingType type)
    {
        List<ClothingItem> source;
        switch (type)
        {
            case ClothingType.Hair:
                source = GetRuntimeHairItems();
                break;
            case ClothingType.Accessory:
                source = GetRuntimeAccessoryItems();
                break;
            default:
                source = GetRuntimeOutfitItems();
                break;
        }

        if (source == null)
            return null;

        ClothingItem firstUsable = null;
        ClothingItem preferred = null;

        foreach (ClothingItem item in source)
        {
            if (!IsClothingUsable(item, type, requireId: false) ||
                !IsClothingAllowedForTarget(item) ||
                item.sprite == null)
                continue;

            if (firstUsable == null)
                firstUsable = item;
            if (preferred == null && IsPreferredFallbackClothing(item, type))
                preferred = item;
        }

        return preferred != null ? preferred : firstUsable;
    }

    static bool IsPreferredFallbackClothing(ClothingItem item, ClothingType type)
    {
        if (item == null || type != ClothingType.Outfit)
            return false;

        string value = ((item.id ?? "") + " " + (item.name ?? "") + " " + item.GetDisplayName()).ToLowerInvariant();
        return value.Contains("night") ||
               value.Contains("robe") ||
               value.Contains("halat") ||
               value.Contains("under") ||
               value.Contains("sleep") ||
               value.Contains("pajama");
    }

    static bool MatchesClothing(ClothingItem item, string id, ClothingType type)
    {
        if (item == null || item.type != type || string.IsNullOrWhiteSpace(id))
            return false;

        return string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(item.name, id, StringComparison.OrdinalIgnoreCase);
    }

    static bool IsClothingUsable(ClothingItem item, ClothingType type, bool requireId)
    {
        if (item == null || item.type != type)
            return false;

        return !requireId || !string.IsNullOrWhiteSpace(item.id);
    }

    bool IsClothingAllowedForTarget(ClothingItem item)
    {
        if (item == null)
            return false;

        string targetId = GetRuntimeTargetCharacterId();
        if (_mode == OpenMode.FullSetup && _useGlobalInventoryInFullSetup)
            return item.IsAvailableForCharacter(targetId);

        GetActiveStoryContext(out string storyId, out string chapterId);
        return item.IsAvailableForWardrobe(targetId, storyId, chapterId);
    }
}
