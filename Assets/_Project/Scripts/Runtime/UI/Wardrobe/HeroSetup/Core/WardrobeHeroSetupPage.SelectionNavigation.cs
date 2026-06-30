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
    void OnOptionClicked(int index)
    {
        SelectOption(index, _mode != OpenMode.StoryWardrobeChoice);

        if (_mode != OpenMode.FullSetup && _completeStoryChoiceOnOptionClick)
        {
            ConfirmCurrentStep();
            return;
        }

        if (_mode == OpenMode.FullSetup && _advanceFullSetupOnOptionClick)
            ConfirmCurrentStep();
    }

    void SelectOption(int index, bool apply, int direction = 0)
    {
        if (_currentOptions.Count == 0)
            return;

        _selectedOptionIndex = Mathf.Clamp(index, 0, _currentOptions.Count - 1);
        RuntimeOption option = _currentOptions[_selectedOptionIndex];

        SetStepPreviewAnimated(option, direction);
        SetSelectedOptionLabel(option);
        RefreshWardrobePremiumChoiceBalancePanel();

        if (apply)
            ApplyOption(option);

        RefreshAllOptionButtons();
        RefreshCurrentOptionActionButton();

        if (_optionPreviewTween == null)
            RefreshNavigationButtons();

        NotifyOptionSelectionChanged();

        Log("Выбран вариант: " + option.Label);
    }

    public void SelectPreviousOption()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorMovePreviewOption(-1);
            return;
        }
#endif

        SelectRelativeOption(-1);
    }

    public void SelectNextOption()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorMovePreviewOption(1);
            return;
        }
#endif

        SelectRelativeOption(1);
    }

    void SelectRelativeOption(int direction)
    {
        if (_currentOptions.Count == 0)
            return;

        int currentIndex = Mathf.Clamp(_selectedOptionIndex, 0, _currentOptions.Count - 1);
        int nextIndex = currentIndex + direction;

        if (_wrapOptionNavigation)
        {
            nextIndex %= _currentOptions.Count;
            if (nextIndex < 0)
                nextIndex += _currentOptions.Count;
        }
        else
        {
            if (nextIndex < 0 || nextIndex >= _currentOptions.Count)
            {
                RefreshNavigationButtons();
                return;
            }
        }

        bool applyWhileBrowsing = _applyOptionWhenBrowsingWithArrows && _mode != OpenMode.StoryWardrobeChoice;
        SelectOption(nextIndex, applyWhileBrowsing, direction);
    }
}
