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
    void GetActiveStoryContext(out string storyId, out string chapterId)
    {
        StoryManager manager = StoryManager.Instance;
        storyId = FirstNonEmpty(_runtimeStoryId, manager != null ? manager.CurrentStoryId : "");
        chapterId = FirstNonEmpty(_runtimeChapterId, manager != null ? FirstNonEmpty(manager.CurrentChapterId, manager.CurrentEpisodeId) : "");

        if (string.IsNullOrWhiteSpace(storyId))
            storyId = FirstBinding(_storyIds);
        if (string.IsNullOrWhiteSpace(chapterId))
            chapterId = FirstBinding(_chapterIds);
    }

    static string FirstBinding(List<string> values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return "";
    }

    static string GetPlayerAppearanceClothingId(ClothingType type)
    {
        switch (type)
        {
            case ClothingType.Outfit:
                return PlayerAppearance.OutfitId;
            case ClothingType.Hair:
                return PlayerAppearance.HairId;
            case ClothingType.Accessory:
                return PlayerAppearance.AccessoryId;
            default:
                return "";
        }
    }

    static Sprite GetPlayerAppearanceClothingSprite(ClothingType type)
    {
        switch (type)
        {
            case ClothingType.Outfit:
                return PlayerAppearance.OutfitSprite;
            case ClothingType.Hair:
                return PlayerAppearance.HairSprite;
            case ClothingType.Accessory:
                return PlayerAppearance.AccessorySprite;
            default:
                return null;
        }
    }

    static ClothingItem GetPlayerAppearanceClothingItem(ClothingType type)
    {
        switch (type)
        {
            case ClothingType.Outfit:
                return PlayerAppearance.OutfitItem;
            case ClothingType.Hair:
                return PlayerAppearance.HairItem;
            case ClothingType.Accessory:
                return PlayerAppearance.AccessoryItem;
            default:
                return null;
        }
    }

    WardrobeHeroSetupStep GetSetupStepForClothingType(ClothingType type)
    {
        switch (type)
        {
            case ClothingType.Hair:
                return WardrobeHeroSetupStep.Hair;
            case ClothingType.Accessory:
                return WardrobeHeroSetupStep.Accessories;
            default:
                return WardrobeHeroSetupStep.Outfit;
        }
    }

    string GetSlotSuffix(ClothingType type)
    {
        switch (type)
        {
            case ClothingType.Hair:
                return _hairSlotSuffix;
            case ClothingType.Accessory:
                return _accessorySlotSuffix;
            default:
                return _outfitSlotSuffix;
        }
    }

    string GetStepTitle(WardrobeHeroSetupStep step)
    {
        switch (step)
        {
            case WardrobeHeroSetupStep.Appearance: return _appearanceTitle;
            case WardrobeHeroSetupStep.Outfit: return _outfitTitle;
            case WardrobeHeroSetupStep.Hair: return _hairTitle;
            case WardrobeHeroSetupStep.Accessories: return _accessoriesTitle;
            default: return "";
        }
    }

    string GetStepDescription(WardrobeHeroSetupStep step)
    {
        switch (step)
        {
            case WardrobeHeroSetupStep.Appearance: return _appearanceDescription;
            case WardrobeHeroSetupStep.Outfit: return _outfitDescription;
            case WardrobeHeroSetupStep.Hair: return _hairDescription;
            case WardrobeHeroSetupStep.Accessories: return _accessoriesDescription;
            default: return "";
        }
    }

    bool IsFullSetupCompleted()
    {
        try
        {
            return LocalSecurePrefs.GetBool(_completionPrefsKey, GetCompletionPurpose(), false);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[WardrobeHeroSetupPage] Не удалось прочитать флаг завершения: " + exception.Message, this);
            return false;
        }
    }

    void SaveCompletionFlag()
    {
        try
        {
            LocalSecurePrefs.SetBool(_completionPrefsKey, GetCompletionPurpose(), true);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[WardrobeHeroSetupPage] Не удалось сохранить флаг завершения: " + exception.Message, this);
        }
    }

    string GetCompletionPurpose()
    {
        return LocalSaveSecurity.SetupFlagPurpose + ":wardrobe:" + SaveDataSanitizer.SanitizeIdentifier(_completionPrefsKey);
    }

    void Log(string message)
    {
        if (_debugLog)
            Debug.Log("[WardrobeHeroSetupPage] " + message, this);
    }

}
