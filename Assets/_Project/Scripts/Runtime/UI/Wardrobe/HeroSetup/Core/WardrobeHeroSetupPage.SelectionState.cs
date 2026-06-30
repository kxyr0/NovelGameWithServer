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
    void ApplyOption(RuntimeOption option)
    {
        if (option == null)
            return;

        switch (option.Step)
        {
            case WardrobeHeroSetupStep.Appearance:
                PlayerAppearance.SetAppearance(option.AppearanceType);
                SaveAppearanceForActiveStory(option.AppearanceType);
                RefreshBodyPreviewFromCharacter(true);
                break;

            case WardrobeHeroSetupStep.Outfit:
                EquipClothing(option.Clothing, _outfitSlotSuffix);
                break;

            case WardrobeHeroSetupStep.Hair:
                EquipClothing(option.Clothing, _hairSlotSuffix);
                break;

            case WardrobeHeroSetupStep.Accessories:
                EquipClothing(option.Clothing, _accessorySlotSuffix);
                break;
        }

        _lastAppliedOption = option;
    }

    void EquipClothing(ClothingItem item, string slotSuffix)
    {
        if (item == null || string.IsNullOrEmpty(item.id))
            return;

        if (GameState.Instance != null)
        {
            GameState.Instance.AddClothing(item.id);
            GameState.Instance.EquipClothing(GetEquipKey(slotSuffix), item.id);
        }

        PlayerAppearance.SetEquippedClothing(item.type, item.id, item.sprite, item);

        CharacterData character = GetRuntimeTargetCharacter();
        if (_applySelectedSpriteToCharacterDefault && !UsesDedicatedPreviewLayers() && character != null)
            character.defaultSprite = item.sprite;
    }

    void SaveAppearanceForActiveStory(AppearanceType appearance)
    {
        GetActiveStoryContext(out string storyId, out _);
        HeroCustomizationStore.SaveAppearanceForStory(storyId, appearance);
    }

    void CommitHeroCustomization()
    {
        SaveAppearanceForActiveStory(PlayerAppearance.CurrentAppearance);

        if (GameState.Instance != null)
        {
            if (!string.IsNullOrWhiteSpace(PlayerAppearance.OutfitId))
            {
                GameState.Instance.AddClothing(PlayerAppearance.OutfitId);
                GameState.Instance.EquipClothing(GetEquipKey(_outfitSlotSuffix), PlayerAppearance.OutfitId);
            }

            if (!string.IsNullOrWhiteSpace(PlayerAppearance.HairId))
            {
                GameState.Instance.AddClothing(PlayerAppearance.HairId);
                GameState.Instance.EquipClothing(GetEquipKey(_hairSlotSuffix), PlayerAppearance.HairId);
            }

            if (!string.IsNullOrWhiteSpace(PlayerAppearance.AccessoryId))
            {
                GameState.Instance.AddClothing(PlayerAppearance.AccessoryId);
                GameState.Instance.EquipClothing(GetEquipKey(_accessorySlotSuffix), PlayerAppearance.AccessoryId);
            }
        }

        if (_saveProgressOnComplete &&
            SaveManager.Instance != null &&
            StoryManager.Instance != null &&
            StoryManager.Instance.HasSelectedStory)
        {
            SaveManager.Instance.SaveCurrentData(StoryManager.Instance.ResolveProgressSaveSlot(), StoryManager.Instance);
        }
    }

    string GetEquipKey(string slotSuffix)
    {
        string characterId = GetRuntimeTargetCharacterId();
        return string.IsNullOrWhiteSpace(slotSuffix)
            ? characterId
            : characterId + ":" + slotSuffix;
    }

    void EnsureInitialDefaultClothingForPlay()
    {
        if (!Application.isPlaying)
            return;

        EnsureDefaultClothingIfMissing(ClothingType.Outfit, _outfitSlotSuffix, GetDefaultClothing(ClothingType.Outfit));
        EnsureDefaultClothingIfMissing(ClothingType.Hair, _hairSlotSuffix, GetDefaultClothing(ClothingType.Hair));
        EnsureDefaultClothingIfMissing(ClothingType.Accessory, _accessorySlotSuffix, GetDefaultClothing(ClothingType.Accessory));
    }

    void EnsureDefaultClothingIfMissing(ClothingType type, string slotSuffix, ClothingItem defaultItem)
    {
        string savedId = GetSavedClothingId(type, slotSuffix);
        if (!string.IsNullOrWhiteSpace(savedId))
        {
            if (TryFindClothing(savedId, type, out ClothingItem savedItem))
            {
                SyncClothingState(savedItem, slotSuffix);
                return;
            }
        }

        if (IsClothingUsable(defaultItem, type, requireId: true) && IsClothingAllowedForTarget(defaultItem))
            SyncClothingState(defaultItem, slotSuffix);
    }

    void SyncClothingState(ClothingItem item, string slotSuffix)
    {
        if (!IsClothingUsable(item, item != null ? item.type : ClothingType.Outfit, requireId: true) ||
            !IsClothingAllowedForTarget(item))
            return;

        if (GameState.Instance != null)
        {
            GameState.Instance.AddClothing(item.id);
            GameState.Instance.EquipClothing(GetEquipKey(slotSuffix), item.id);
        }

        if (!string.Equals(GetPlayerAppearanceClothingId(item.type), item.id, StringComparison.OrdinalIgnoreCase) ||
            GetPlayerAppearanceClothingSprite(item.type) == null ||
            GetPlayerAppearanceClothingItem(item.type) != item)
        {
            PlayerAppearance.SetEquippedClothing(item.type, item.id, item.sprite, item);
        }
    }

    string GetSavedClothingId(ClothingType type, string slotSuffix)
    {
        string equippedId = GameState.Instance != null ? GameState.Instance.GetEquipped(GetEquipKey(slotSuffix)) : "";
        if (!string.IsNullOrWhiteSpace(equippedId))
            return equippedId;

        if (CanUseGlobalAppearanceFallbackForSavedClothing())
        {
            string appearanceId = GetPlayerAppearanceClothingId(type);
            if (!string.IsNullOrWhiteSpace(appearanceId))
                return appearanceId;
        }

        return "";
    }

    bool CanUseGlobalAppearanceFallbackForSavedClothing()
    {
        GetActiveStoryContext(out string storyId, out _);
        return string.IsNullOrWhiteSpace(storyId) &&
               (GameState.Instance == null || string.IsNullOrWhiteSpace(GameState.Instance.CurrentStoryId));
    }

    int FindCurrentOptionIndex(List<RuntimeOption> options)
    {
        if (options == null)
            return -1;

        for (int i = 0; i < options.Count; i++)
        {
            RuntimeOption option = options[i];
            if (IsCurrentOption(option))
                return i;
        }

        return -1;
    }

    bool IsCurrentOption(RuntimeOption option)
    {
        if (option == null)
            return false;

        if (option.Step == WardrobeHeroSetupStep.Appearance)
            return option.AppearanceType == PlayerAppearance.CurrentAppearance;

        if (option.Clothing == null)
            return false;

        string slotSuffix = GetSlotSuffix(option.Clothing.type);
        string savedId = GetSavedClothingId(option.Clothing.type, slotSuffix);
        return MatchesClothing(option.Clothing, savedId, option.Clothing.type);
    }

    bool IsOptionApplied(RuntimeOption option)
    {
        return IsCurrentOption(option) || RuntimeOptionsMatch(option, _lastAppliedOption);
    }

    static bool RuntimeOptionsMatch(RuntimeOption first, RuntimeOption second)
    {
        if (first == null || second == null || first.Step != second.Step)
            return false;

        if (first.Step == WardrobeHeroSetupStep.Appearance)
            return first.AppearanceType == second.AppearanceType;

        if (first.Clothing == null || second.Clothing == null)
            return false;

        if (!string.IsNullOrWhiteSpace(first.Clothing.id) || !string.IsNullOrWhiteSpace(second.Clothing.id))
            return string.Equals(first.Clothing.id, second.Clothing.id, StringComparison.OrdinalIgnoreCase);

        return ReferenceEquals(first.Clothing, second.Clothing);
    }

    void RefreshNavigationButtons()
    {
        bool fullSetup = _mode == OpenMode.FullSetup;
        bool lastStep = fullSetup && _stepIndex >= _fullSetupSteps.Count - 1;

        if (CanUseAsBackButton(_backButton))
        {
            _backButton.gameObject.SetActive(fullSetup);
            _backButton.interactable = fullSetup && _stepIndex > 0;
        }

        if (CanUseAsContinueButton(_continueButton))
            _continueButton.gameObject.SetActive(true);

        if (_continueButtonLabel != null && CanUseAsContinueButton(_continueButton))
        {
            WardrobeOptionButtonView continueOptionView = GetContinueOptionButtonView();
            if (continueOptionView == null)
                _continueButtonLabel.text = fullSetup && !lastStep ? _nextButtonText : _doneButtonText;
        }

        RefreshCurrentOptionActionButton();

        ApplyOptionArrowState(_previousOptionButton, _previousOptionDisabledFades, CanSelectPreviousOption());
        ApplyOptionArrowState(_nextOptionButton, _nextOptionDisabledFades, CanSelectNextOption());
    }

    void SetSelectedOptionLabel(string label)
    {
        SetSelectedOptionLabel(label, 0);
    }

    void SetSelectedOptionLabel(RuntimeOption option)
    {
        if (option == null)
        {
            SetSelectedOptionLabel("");
            return;
        }

        SetSelectedOptionLabel(option.Label, GetVisiblePremiumCost(option));
    }

    void SetSelectedOptionLabel(string label, int premiumCost)
    {
        label = label ?? "";
        premiumCost = Mathf.Max(0, premiumCost);

        if (_selectedOptionPriceIcon != null)
        {
            _selectedOptionPriceIcon.SetContent(label, premiumCost, _premiumCostIcon);
            return;
        }

        if (_selectedOptionLabel != null)
            _selectedOptionLabel.text = FormatLabelWithTextPrice(label, premiumCost);
    }

    void RefreshCurrentOptionStateAfterApply()
    {
        RefreshAllOptionButtons();
        RefreshCurrentOptionActionButton();
        RefreshNavigationButtons();
        RefreshWardrobePremiumChoiceBalancePanel();
        NotifyOptionSelectionChanged();
    }

    void RefreshCurrentOptionActionButton()
    {
        WardrobeOptionButtonView optionView = GetContinueOptionButtonView();
        RuntimeOption option = GetSelectedRuntimeOption();

        if (optionView == null)
        {
            bool available = option != null;
            if (_continueButton != null)
                _continueButton.interactable = available;

            if (_continueButtonLabel != null)
                _continueButtonLabel.text = available ? _continueButtonLabel.text : "Недоступно";

            return;
        }

        if (option == null)
        {
            optionView.SetOptionLabel("Недоступно");
            optionView.SetPremiumCost(0, _premiumCostIcon);
            optionView.SetSelected(false);
            optionView.SetUnavailable(true);
            return;
        }

        int premiumCost = GetVisiblePremiumCost(option);
        optionView.SetUnavailable(false);
        optionView.SetOptionLabel(option.Label);
        optionView.SetPremiumCost(premiumCost, _premiumCostIcon);
        optionView.SetSelected(IsOptionApplied(option));
    }

    WardrobeOptionButtonView GetContinueOptionButtonView()
    {
        return _continueButton != null ? _continueButton.GetComponent<WardrobeOptionButtonView>() : null;
    }

    int GetVisiblePremiumCost(RuntimeOption option)
    {
        if (option == null || option.PremiumCost <= 0 || IsOwnedClothing(option.Clothing))
            return 0;

        return SaveDataSanitizer.ClampCurrencyValue(option.PremiumCost);
    }

    string FormatLabelWithTextPrice(string label, int premiumCost)
    {
        return premiumCost > 0 ? (label ?? "") + " (" + premiumCost + ")" : (label ?? "");
    }

    bool CanSelectPreviousOption()
    {
        return _currentOptions.Count > 1 && (_wrapOptionNavigation || _selectedOptionIndex > 0);
    }

    bool CanSelectNextOption()
    {
        return _currentOptions.Count > 1 && (_wrapOptionNavigation || _selectedOptionIndex < _currentOptions.Count - 1);
    }

    void ApplyOptionArrowState(Button button, UISpriteStateFade[] disabledFades, bool canUse)
    {
        if (button != null)
        {
            button.gameObject.SetActive(true);
            button.interactable = canUse;
        }

        ApplyOptionArrowDisabledFades(disabledFades, !canUse);
    }

    void ApplyOptionArrowDisabledFades(UISpriteStateFade[] disabledFades, bool disabled)
    {
        if (disabledFades == null)
            return;

        for (int i = 0; i < disabledFades.Length; i++)
        {
            UISpriteStateFade fade = disabledFades[i];
            if (fade != null)
                fade.SetActiveState(disabled);
        }
    }

    void PrepareOptionArrowFades()
    {
        if (!_disableHoverOnOptionArrowFades)
            return;

        DisablePointerHover(_previousOptionDisabledFades);
        DisablePointerHover(_nextOptionDisabledFades);
    }

    void DisablePointerHover(UISpriteStateFade[] fades)
    {
        if (fades == null)
            return;

        for (int i = 0; i < fades.Length; i++)
        {
            UISpriteStateFade fade = fades[i];
            if (fade != null)
                fade.SetPointerHoverEnabled(false, false);
        }
    }

    WardrobeOptionSelectionInfo CreateOptionSelectionInfo()
    {
        RuntimeOption option = null;
        int count = _currentOptions.Count;
        int index = count > 0 ? Mathf.Clamp(_selectedOptionIndex, 0, count - 1) : -1;
        if (index >= 0)
            option = _currentOptions[index];

        return new WardrobeOptionSelectionInfo
        {
            step = option != null ? option.Step : GetCurrentSetupStep(),
            label = option != null ? FormatLabelWithTextPrice(option.Label, GetVisiblePremiumCost(option)) : "Недоступно",
            index = index,
            count = count,
            canSelectPrevious = CanSelectPreviousOption(),
            canSelectNext = CanSelectNextOption()
        };
    }

    WardrobeHeroSetupStep GetCurrentSetupStep()
    {
        if (_mode == OpenMode.StoryAppearanceChoice)
            return WardrobeHeroSetupStep.Appearance;

        if (_mode == OpenMode.FullSetup && _fullSetupSteps.Count > 0)
            return _fullSetupSteps[Mathf.Clamp(_stepIndex, 0, _fullSetupSteps.Count - 1)];

        return WardrobeHeroSetupStep.Outfit;
    }

    public WardrobeOptionSelectionInfo GetCurrentOptionSelectionInfo()
    {
        return CreateOptionSelectionInfo();
    }

    void NotifyOptionSelectionChanged()
    {
        OptionSelectionChanged?.Invoke(CreateOptionSelectionInfo());
    }
}
