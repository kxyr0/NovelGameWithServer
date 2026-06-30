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
    public void PrepareForStory(GameData data)
    {
        string previousStoryId = _runtimeStoryId;
        _runtimeStoryId = ResolveRuntimeStoryId(data);
        _runtimeChapterId = ResolveRuntimeChapterId();
        CacheRuntimePremiumChoiceStoryUiStyle(data);
        EnsureGameStateStoryContext();
        ApplyRuntimeWardrobeAssets(data != null ? data.WardrobeSetup : null);

        bool storyChanged = !string.Equals(previousStoryId, _runtimeStoryId, StringComparison.OrdinalIgnoreCase);
        if (storyChanged)
            ResetRuntimeSelectionCache();

        LoadAppearanceForRuntimeStory();
        EnsureInitialDefaultClothingForPlay();
    }

    CharacterData GetRuntimeTargetCharacter()
    {
        return _hasRuntimeWardrobeAssets
            ? _runtimeTargetCharacter
            : _targetCharacter;
    }

    string GetRuntimeTargetCharacterId()
    {
        CharacterData character = GetRuntimeTargetCharacter();
        if (_hasRuntimeWardrobeAssets)
            return FirstNonEmpty(_runtimeTargetCharacterId, character != null ? character.name : "", "hero");

        return FirstNonEmpty(
            _targetCharacterId,
            character != null ? character.name : "",
            "hero");
    }

    List<WardrobeHeroAppearanceOption> GetRuntimeAppearanceOptions()
    {
        return _hasRuntimeWardrobeAssets
            ? _runtimeAppearanceOptions
            : _appearanceOptions;
    }

    List<ClothingItem> GetRuntimeOutfitItems()
    {
        return _hasRuntimeWardrobeAssets
            ? _runtimeOutfitItems
            : _outfitItems;
    }

    List<ClothingItem> GetRuntimeHairItems()
    {
        return _hasRuntimeWardrobeAssets
            ? _runtimeHairItems
            : _hairItems;
    }

    List<ClothingItem> GetRuntimeAccessoryItems()
    {
        return _hasRuntimeWardrobeAssets
            ? _runtimeAccessoryItems
            : _accessoryItems;
    }

    ClothingItem GetRuntimeDefaultClothing(ClothingType type)
    {
        if (_hasRuntimeWardrobeAssets)
        {
            switch (type)
            {
                case ClothingType.Outfit:
                    return _runtimeDefaultOutfitItem;
                case ClothingType.Hair:
                    return _runtimeDefaultHairItem;
                case ClothingType.Accessory:
                    return _runtimeDefaultAccessoryItem;
            }
        }

        switch (type)
        {
            case ClothingType.Outfit:
                return _defaultOutfitItem;
            case ClothingType.Hair:
                return _defaultHairItem;
            case ClothingType.Accessory:
                return _defaultAccessoryItem;
            default:
                return null;
        }
    }

    bool IsRuntimeDefaultClothing(ClothingItem item, ClothingType type)
    {
        return item != null && item == GetRuntimeDefaultClothing(type);
    }

    void ResetRuntimeSelectionCache()
    {
        _selectedOptionIndex = -1;
        _lastAppliedOption = null;
        _currentOptions.Clear();
        _currentOutfitPreviewItem = null;
        _currentHairPreviewItem = null;
        _currentAccessoryPreviewItem = null;

        if (Application.isPlaying)
        {
            PlayerAppearance.SetEquippedClothing(ClothingType.Outfit, "", null, null);
            PlayerAppearance.SetEquippedClothing(ClothingType.Hair, "", null, null);
            PlayerAppearance.SetEquippedClothing(ClothingType.Accessory, "", null, null);
        }
    }

    private void ApplyRuntimeWardrobeAssets(GameWardrobeSetupSettings setup)
    {
        ClearRuntimeWardrobeAssets();

        if (setup == null || !setup.OverrideWardrobeAssets)
            return;

        _hasRuntimeWardrobeAssets = true;
        _runtimeTargetCharacter = setup.TargetCharacter;
        _runtimeTargetCharacterId = setup.TargetCharacterId;
        _runtimeAppearanceOptions = CopyRuntimeList(setup.AppearanceOptions);
        _runtimeOutfitItems = CopyRuntimeList(setup.OutfitItems);
        _runtimeHairItems = CopyRuntimeList(setup.HairItems);
        _runtimeAccessoryItems = CopyRuntimeList(setup.AccessoryItems);
        _runtimeDefaultOutfitItem = setup.DefaultOutfitItem;
        _runtimeDefaultHairItem = setup.DefaultHairItem;
        _runtimeDefaultAccessoryItem = setup.DefaultAccessoryItem;
    }

    private void ClearRuntimeWardrobeAssets()
    {
        _hasRuntimeWardrobeAssets = false;
        _runtimeTargetCharacter = null;
        _runtimeTargetCharacterId = "";
        _runtimeAppearanceOptions = null;
        _runtimeOutfitItems = null;
        _runtimeHairItems = null;
        _runtimeAccessoryItems = null;
        _runtimeDefaultOutfitItem = null;
        _runtimeDefaultHairItem = null;
        _runtimeDefaultAccessoryItem = null;
    }

    private void LoadAppearanceForRuntimeStory()
    {
        if (string.IsNullOrWhiteSpace(_runtimeStoryId))
            return;

        if (HeroCustomizationStore.TryLoadAppearanceForStory(_runtimeStoryId, out AppearanceType appearance))
            PlayerAppearance.SetAppearance(appearance);
    }

    private void EnsureGameStateStoryContext()
    {
        if (!Application.isPlaying || GameState.Instance == null || string.IsNullOrWhiteSpace(_runtimeStoryId))
            return;

        if (!string.Equals(GameState.Instance.CurrentStoryId, _runtimeStoryId, StringComparison.OrdinalIgnoreCase))
            GameState.Instance.InitForStory(_runtimeStoryId);
    }

    private string ResolveRuntimeStoryId(GameData data)
    {
        if (data != null && data.Story != null)
            return data.Story.StoryId;

        StoryManager manager = StoryManager.Instance;
        if (manager != null && !string.IsNullOrWhiteSpace(manager.CurrentStoryId))
            return manager.CurrentStoryId;

        return "";
    }

    private static string ResolveRuntimeChapterId()
    {
        StoryManager manager = StoryManager.Instance;
        return manager != null
            ? FirstNonEmpty(manager.CurrentChapterId, manager.CurrentEpisodeId)
            : "";
    }

    private static List<T> CopyRuntimeList<T>(IReadOnlyList<T> source) where T : class
    {
        if (source == null)
            return new List<T>();

        var result = new List<T>(source.Count);
        for (int i = 0; i < source.Count; i++)
            result.Add(source[i]);

        return result;
    }
}
