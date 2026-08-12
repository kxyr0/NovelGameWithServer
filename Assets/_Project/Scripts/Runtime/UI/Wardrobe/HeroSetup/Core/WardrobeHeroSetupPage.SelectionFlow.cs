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
    void ConfirmCurrentStep()
    {
        if (_currentOptions.Count == 0)
        {
            if (_mode == OpenMode.StoryWardrobeChoice)
            {
                LogStoryWardrobeSelection("confirm_no_options", null, true);
                TrySubmitFirstAvailableStoryWardrobeOption("confirm_fallback_first_available");
            }
            return;
        }

        if (_currentOptions.Count > 0)
        {
            if (_selectedOptionIndex < 0)
                _selectedOptionIndex = 0;

            RuntimeOption option = _currentOptions[Mathf.Clamp(_selectedOptionIndex, 0, _currentOptions.Count - 1)];
            bool usesOptionActionButton = GetContinueOptionButtonView() != null;
            if (_mode == OpenMode.StoryWardrobeChoice)
            {
                Action<int> callback = _storyChoiceCallback;
                if (callback == null)
                {
                    LogStoryWardrobeSelection("confirm_missing_callback", option, true);
                    Cancel();
                    return;
                }

                LogStoryWardrobeSelection("confirm_selected_option", option, false);
                callback?.Invoke(option.SourceIndex);
                return;
            }

            if (_mode == OpenMode.FullSetup && usesOptionActionButton && IsOptionApplied(option))
                return;

            ApplyOption(option);
            RefreshCurrentOptionStateAfterApply();

            if (_mode == OpenMode.FullSetup && (usesOptionActionButton || _stayOpenAfterOptionApply))
                return;

            if (_mode != OpenMode.FullSetup)
            {
                int sourceIndex = option.SourceIndex;
                Action<int> callback = _storyChoiceCallback;
                Close();
                callback?.Invoke(sourceIndex);
                return;
            }
        }

        if (_mode == OpenMode.FullSetup)
            MoveToNextFullSetupStep();
    }

    void MoveToNextFullSetupStep()
    {
        _stepIndex++;
        _selectedOptionIndex = -1;

        if (_stepIndex >= _fullSetupSteps.Count)
        {
            CompleteFullSetup();
            return;
        }

        ShowCurrentFullSetupStep();
    }

    void MoveToPreviousFullSetupStep()
    {
        if (_mode != OpenMode.FullSetup || _stepIndex <= 0)
            return;

        _stepIndex--;
        _selectedOptionIndex = -1;
        ShowCurrentFullSetupStep();
    }

    void CompleteFullSetup()
    {
        CommitHeroCustomization();

        if (_saveCompletionFlag)
            SaveCompletionFlag();

        Action callback = _onComplete;
        Close();
        callback?.Invoke();
    }

    void Cancel()
    {
        Action callback = _onCancel;
        Close();
        callback?.Invoke();
    }

    void HandleCloseButtonClicked()
    {
        if (_mode == OpenMode.StoryWardrobeChoice)
        {
            RuntimeOption option = GetSelectedRuntimeOption();
            if (option != null)
            {
                LogStoryWardrobeSelection("close_button_confirms_current_option", option, false);
                ConfirmCurrentStep();
            }
            else
            {
                LogStoryWardrobeSelection("close_button_no_story_options", null, true);
                if (!TrySubmitFirstAvailableStoryWardrobeOption("close_button_fallback_first_available"))
                    ShowTransientSystemMessage("\u041d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u043d\u043e");
            }

            return;
        }

        bool closeCompletesFullSetup =
            _mode == OpenMode.FullSetup &&
            _onComplete != null &&
            (_onCancel == null || _onCancel == _onComplete);

        if (closeCompletesFullSetup)
        {
            CompleteFullSetup();
            return;
        }

        if (_onCancel != null)
        {
            Cancel();
            return;
        }

        bool shouldReturnToScreen = _closeButtonReturnsToScreen &&
                                    _mode == OpenMode.FullSetup &&
                                    _stayOpenAfterOptionApply;

        Cancel();

        if (shouldReturnToScreen)
            OpenCloseTargetScreen();
    }

    void LogStoryWardrobeSelection(string eventName, RuntimeOption option, bool warning)
    {
        if (_mode != OpenMode.StoryWardrobeChoice && _activeStoryWardrobeNode == null)
            return;

        GetActiveStoryContext(out string storyId, out string chapterId);
        ClothingItem item = option != null ? option.Clothing : null;
        bool clearsSlot = option != null && option.ClearsClothingSlot;
        string itemType = item != null
            ? item.type.ToString()
            : clearsSlot ? option.ClearClothingType.ToString() : "";
        int premiumCost = option != null ? SaveDataSanitizer.ClampCurrencyValue(option.PremiumCost) : 0;
        int visibleCost = option != null ? GetVisiblePremiumCost(option) : 0;

        var metadata = LogMetadata.Of(
            "event", eventName,
            "storyId", storyId,
            "chapterId", chapterId,
            "nodeGuid", _activeStoryWardrobeNode != null ? _activeStoryWardrobeNode.guid : "",
            "nodeName", _activeStoryWardrobeNode != null ? _activeStoryWardrobeNode.name : "",
            "selectedIndex", _selectedOptionIndex,
            "optionCount", _currentOptions != null ? _currentOptions.Count : 0,
            "sourceIndex", option != null ? option.SourceIndex : -1,
            "label", option != null ? option.Label : "",
            "itemId", item != null ? item.id : "",
            "itemType", itemType,
            "clearsSlot", clearsSlot,
            "premiumCost", premiumCost,
            "visibleCost", visibleCost,
            "owned", item != null && IsOwnedClothing(item),
            "hasCallback", _storyChoiceCallback != null);

        if (warning)
        {
            AppLogger.Warn(
                AppLogCategory.Wardrobe,
                nameof(WardrobeHeroSetupPage),
                nameof(ConfirmCurrentStep),
                "[WARDROBE][STORY_CHOICE] Story wardrobe action could not continue normally.",
                metadata,
                recoverable: true);
            return;
        }

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(WardrobeHeroSetupPage),
            nameof(ConfirmCurrentStep),
            "[WARDROBE][STORY_CHOICE] Story wardrobe option submitted to StoryManager.",
            metadata);
    }

    bool TrySubmitFirstAvailableStoryWardrobeOption(string eventName)
    {
        if (_mode != OpenMode.StoryWardrobeChoice ||
            _activeStoryWardrobeNode == null ||
            _activeStoryWardrobeNode.availableClothes == null ||
            _storyChoiceCallback == null)
        {
            return false;
        }

        for (int i = 0; i < _activeStoryWardrobeNode.availableClothes.Count; i++)
        {
            if (!_activeStoryWardrobeNode.IsOptionVisible(i))
                continue;

            if (_activeStoryWardrobeNode.TryGetClearSlotType(i, out ClothingType clearType))
            {
                RuntimeOption clearOption = CreateClearClothingOption(
                    _activeStoryWardrobeNode.GetOptionLabel(i, "Ничего"),
                    i,
                    clearType);

                LogStoryWardrobeSelection(eventName, clearOption, false);
                _storyChoiceCallback.Invoke(i);
                return true;
            }

            ClothingItem item = _activeStoryWardrobeNode.availableClothes[i];
            if (item == null || !IsClothingAllowedForTarget(item))
                continue;

            int premiumCost = _activeStoryWardrobeNode.GetPremiumCost(i);
            if (premiumCost > 0 && !IsOwnedClothing(item))
                continue;

            RuntimeOption option = CreateClothingOption(
                item,
                i,
                GetSetupStepForClothingType(item.type),
                premiumCost);

            LogStoryWardrobeSelection(eventName, option, false);
            _storyChoiceCallback.Invoke(i);
            return true;
        }

        return false;
    }

    void OpenCloseTargetScreen()
    {
        if (string.IsNullOrWhiteSpace(_closeTargetScreenId))
            return;

        if (_closeScreenNavigator == null)
            _closeScreenNavigator = GetComponentInParent<StoryScreenNavigator>(true);

        if (_closeScreenNavigator == null)
            _closeScreenNavigator = FindObjectOfType<StoryScreenNavigator>(true);

        if (_closeScreenNavigator == null)
        {
            Debug.LogWarning("WardrobeHeroSetupPage: StoryScreenNavigator is not assigned, cannot return from wardrobe.", this);
            return;
        }

        _closeScreenNavigator.OpenScreen(_closeTargetScreenId);
    }
}
