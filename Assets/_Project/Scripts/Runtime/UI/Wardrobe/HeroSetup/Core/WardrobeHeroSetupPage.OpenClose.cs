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
    public bool OpenFullSetup(
        Action onComplete = null,
        Action onCancel = null,
        bool skipWhenAlreadyCompleted = false,
        bool saveProgressOnComplete = true)
    {
        if (!_useForOpenWardrobeNode)
            return false;

        EnsureInitialDefaultClothingForPlay();

        if (ShouldSkipFullSetup(skipWhenAlreadyCompleted))
        {
            Log("Полный flow пропущен: флаг завершения уже сохранен.");
            onComplete?.Invoke();
            return true;
        }

        _mode = OpenMode.FullSetup;
        _onComplete = onComplete;
        _onCancel = onCancel;
        _storyChoiceCallback = null;
        _activeStoryWardrobeNode = null;
        _stayOpenAfterOptionApply = false;
        _saveProgressOnComplete = saveProgressOnComplete;
        _lastAppliedOption = null;
        _stepIndex = 0;
        _selectedOptionIndex = -1;
        AutoWire();
        CaptureLayerDefaults();

        BuildFullSetupSteps();
        if (_fullSetupSteps.Count == 0)
        {
            Debug.LogWarning("[WardrobeHeroSetupPage] Нет ни одного шага для показа. Проверьте списки внешности, одежды и причесок.", this);
            onComplete?.Invoke();
            return false;
        }

        OpenPage();
        PrepareLayeredPreviewForOpen(true);
        ShowCurrentFullSetupStep();
        RevealLayeredPreviewAfterOpen();
        return true;
    }

    public bool ShouldSkipFullSetup(bool skipWhenAlreadyCompleted = false)
    {
        bool shouldSkipCompletedFlow = _skipFullSetupWhenCompleted || skipWhenAlreadyCompleted;
        bool allowDebugRepeat = _debugAlwaysRunFullSetupOnOpen && !skipWhenAlreadyCompleted;
        return shouldSkipCompletedFlow &&
               !allowDebugRepeat &&
               IsFullSetupCompleted();
    }

    public bool OpenStoryAppearanceChoice(AppearanceChoiceNode node, Action<int> onSelected)
    {
        if (!_useForStoryAppearanceChoices || node == null || node.options == null || node.options.Count == 0)
            return false;

        _mode = OpenMode.StoryAppearanceChoice;
        _onComplete = null;
        _onCancel = null;
        _storyChoiceCallback = onSelected;
        _activeStoryWardrobeNode = null;
        _stayOpenAfterOptionApply = false;
        _saveProgressOnComplete = true;
        _lastAppliedOption = null;
        _selectedOptionIndex = -1;

        _currentOptions.Clear();
        for (int i = 0; i < node.options.Count; i++)
        {
            var option = node.options[i];
            if (option == null)
                continue;

            AppearanceVariant variant = GetAppearanceVariant(option.type);
            _currentOptions.Add(new RuntimeOption
            {
                Label = FirstNonEmpty(option.label, option.type.ToString()),
                Preview = GetAppearancePreviewSprite(variant, option.previewSprite),
                AppearanceType = option.type,
                AppearanceVariant = variant,
                SourceIndex = i,
                Step = WardrobeHeroSetupStep.Appearance
            });
        }

        if (_currentOptions.Count == 0)
        {
            _storyChoiceCallback = null;
            _activeStoryWardrobeNode = null;
            return false;
        }

        OpenPage();
        ShowOptions(node.promptText, _appearanceDescription, _currentOptions);
        return true;
    }

    public bool OpenStoryWardrobeChoice(WardrobeChoiceNode node, Action<int> onSelected)
    {
        if (!_useForStoryWardrobeChoices || node == null || node.availableClothes == null || node.availableClothes.Count == 0)
            return false;

        _mode = OpenMode.StoryWardrobeChoice;
        _onComplete = null;
        _onCancel = null;
        _storyChoiceCallback = onSelected;
        _activeStoryWardrobeNode = node;
        _stayOpenAfterOptionApply = false;
        _saveProgressOnComplete = true;
        _lastAppliedOption = null;
        _targetCharacter = node.character != null ? node.character : _targetCharacter;
        _targetCharacterId = FirstNonEmpty(node.characterId, _targetCharacterId);
        _selectedOptionIndex = -1;

        _currentOptions.Clear();
        int hiddenByRule = 0;
        int nullItems = 0;
        int blockedByContext = 0;
        int paidItems = 0;
        int ownedPaidItems = 0;

        for (int i = 0; i < node.availableClothes.Count; i++)
        {
            if (!node.IsOptionVisible(i))
            {
                hiddenByRule++;
                continue;
            }

            ClothingItem item = node.availableClothes[i];
            if (item == null)
            {
                nullItems++;
                continue;
            }

            if (!IsClothingAllowedForTarget(item))
            {
                blockedByContext++;
                continue;
            }

            int premiumCost = node.GetPremiumCost(i);
            if (premiumCost > 0)
            {
                paidItems++;
                if (IsOwnedClothing(item))
                    ownedPaidItems++;
            }

            _currentOptions.Add(CreateClothingOption(item, i, GetSetupStepForClothingType(item.type), premiumCost));
        }

        LogStoryWardrobeChoiceOpen(
            node,
            _currentOptions.Count,
            hiddenByRule,
            nullItems,
            blockedByContext,
            paidItems,
            ownedPaidItems);

        if (_currentOptions.Count == 0)
        {
            _storyChoiceCallback = null;
            _activeStoryWardrobeNode = null;
            return false;
        }

        OpenPage();
        PrepareLayeredPreviewForOpen(true);
        WardrobeHeroSetupStep step = _currentOptions[0].Step;
        ShowOptions(GetStepTitle(step), GetStepDescription(step), _currentOptions);
        RevealLayeredPreviewAfterOpen();
        return true;
    }

    void LogStoryWardrobeChoiceOpen(
        WardrobeChoiceNode node,
        int visibleCount,
        int hiddenByRule,
        int nullItems,
        int blockedByContext,
        int paidItems,
        int ownedPaidItems)
    {
        GetActiveStoryContext(out string storyId, out string chapterId);
        int total = node != null && node.availableClothes != null ? node.availableClothes.Count : 0;
        var metadata = LogMetadata.Of(
            "storyId", storyId,
            "chapterId", chapterId,
            "nodeGuid", node != null ? node.guid : "",
            "nodeName", node != null ? node.name : "",
            "totalNodeItems", total,
            "visibleOptions", visibleCount,
            "hiddenByRule", hiddenByRule,
            "nullItems", nullItems,
            "blockedByContext", blockedByContext,
            "paidItems", paidItems,
            "ownedPaidItems", ownedPaidItems,
            "targetCharacterId", _targetCharacterId,
            "hasCallback", _storyChoiceCallback != null);

        if (visibleCount <= 0)
        {
            AppLogger.Warn(
                AppLogCategory.Wardrobe,
                nameof(WardrobeHeroSetupPage),
                nameof(OpenStoryWardrobeChoice),
                "[WARDROBE][OPEN] Story wardrobe choice has no visible options.",
                metadata,
                recoverable: true);
            return;
        }

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(WardrobeHeroSetupPage),
            nameof(OpenStoryWardrobeChoice),
            "[WARDROBE][OPEN] Story wardrobe choice opened on hero setup page.",
            metadata);
    }

    public bool ShowAppearanceCategory()
    {
        return ShowFullSetupCategory(WardrobeHeroSetupStep.Appearance);
    }

    public bool ShowHairCategory()
    {
        return ShowFullSetupCategory(WardrobeHeroSetupStep.Hair);
    }

    public bool ShowOutfitCategory()
    {
        return ShowFullSetupCategory(WardrobeHeroSetupStep.Outfit);
    }

    public bool ShowAccessoriesCategory()
    {
        return ShowFullSetupCategory(WardrobeHeroSetupStep.Accessories);
    }

    public bool ShowFullSetupCategory(WardrobeHeroSetupStep step)
    {
        if (_mode == OpenMode.StoryWardrobeChoice && _activeStoryWardrobeNode != null)
            return ShowStoryWardrobeCategory(step);

        if (!_useForOpenWardrobeNode)
            return false;

        EnsureInitialDefaultClothingForPlay();

        Action preservedComplete = _onComplete;
        Action preservedCancel = _onCancel;
        bool preservedSaveProgressOnComplete = _saveProgressOnComplete;
        bool preserveReturnCallbacks =
            _mode == OpenMode.FullSetup &&
            (preservedComplete != null || preservedCancel != null);

        _mode = OpenMode.FullSetup;
        _onComplete = preserveReturnCallbacks ? preservedComplete : null;
        _onCancel = preserveReturnCallbacks ? preservedCancel : null;
        _storyChoiceCallback = null;
        _stayOpenAfterOptionApply = true;
        _saveProgressOnComplete = preserveReturnCallbacks ? preservedSaveProgressOnComplete : true;
        _selectedOptionIndex = -1;

        AutoWire();
        CaptureLayerDefaults();

        _fullSetupSteps.Clear();
        _fullSetupSteps.Add(step);
        _stepIndex = 0;

        BuildOptionsForStep(step, _currentOptions);

        OpenPage();
        PrepareLayeredPreviewForOpen(true);
        ShowOptions(GetStepTitle(step), GetStepDescription(step), _currentOptions);
        RevealLayeredPreviewAfterOpen();

        Log("Показана вкладка wardrobe: " + step);
        return _currentOptions.Count > 0;
    }

    bool ShowStoryWardrobeCategory(WardrobeHeroSetupStep step)
    {
        if (!_useForStoryWardrobeChoices || _activeStoryWardrobeNode == null)
            return false;

        _selectedOptionIndex = -1;
        BuildStoryWardrobeOptionsForStep(_activeStoryWardrobeNode, step, _currentOptions);

        OpenPage();
        PrepareLayeredPreviewForOpen(true);
        ShowOptions(GetStepTitle(step), GetStepDescription(step), _currentOptions);
        RevealLayeredPreviewAfterOpen();

        Log("Story wardrobe category shown: " + step);
        return _currentOptions.Count > 0;
    }

    public void Close()
    {
        KillOptionPreviewTween(true);
        KillLayeredPreviewOpenTween(true);
        KillSystemMessageRestoreTween();
        HideWardrobePremiumChoiceBalancePanel();
        ClearOptions();
        _isOpen = false;
        _storyChoiceCallback = null;
        _activeStoryWardrobeNode = null;
        _onComplete = null;
        _onCancel = null;
        _stayOpenAfterOptionApply = false;
        _saveProgressOnComplete = true;
        _lastAppliedOption = null;

        SetSetupObjectsVisible(false);
        RestoreStoryObjects();

        if (_pageCanvasGroup != null)
        {
            _pageCanvasGroup.alpha = 0f;
            _pageCanvasGroup.interactable = false;
            _pageCanvasGroup.blocksRaycasts = false;
        }
    }

    public void EnsureSetupPanelVisible()
    {
        if (!_isOpen)
            return;

        if (_pageRoot != null && !_pageRoot.activeSelf)
            _pageRoot.SetActive(true);

        if (_pageCanvasGroup != null)
        {
            _pageCanvasGroup.alpha = 1f;
            _pageCanvasGroup.interactable = true;
            _pageCanvasGroup.blocksRaycasts = true;
        }

        SetSetupObjectsVisible(true);
    }

    void OpenPage()
    {
        AutoWire();
        BindButtons();

        if (_pageRoot != null)
            _pageRoot.SetActive(true);

        if (_pageCanvasGroup != null)
        {
            _pageCanvasGroup.alpha = 1f;
            _pageCanvasGroup.interactable = true;
            _pageCanvasGroup.blocksRaycasts = true;
        }

        _isOpen = true;
        SetSetupObjectsVisible(true);
        HideStoryObjects();
        Log("Страница wardrobe открыта в режиме " + _mode);
    }

    void BuildFullSetupSteps()
    {
        _fullSetupSteps.Clear();

        AddStepIfNeeded(WardrobeHeroSetupStep.Appearance, _showAppearanceStep, HasAppearanceOptions());
        AddStepIfNeeded(WardrobeHeroSetupStep.Outfit, _showOutfitStep, HasClothingOptions(GetRuntimeOutfitItems(), ClothingType.Outfit));
        AddStepIfNeeded(WardrobeHeroSetupStep.Hair, _showHairStep, HasClothingOptions(GetRuntimeHairItems(), ClothingType.Hair));
        AddStepIfNeeded(WardrobeHeroSetupStep.Accessories, _showAccessoriesStep, HasClothingOptions(GetRuntimeAccessoryItems(), ClothingType.Accessory));
    }

    void AddStepIfNeeded(WardrobeHeroSetupStep step, bool enabled, bool hasOptions)
    {
        if (!enabled)
            return;

        if (_skipEmptySteps && !hasOptions)
            return;

        _fullSetupSteps.Add(step);
    }

    void ShowCurrentFullSetupStep()
    {
        if (_stepIndex < 0 || _stepIndex >= _fullSetupSteps.Count)
        {
            CompleteFullSetup();
            return;
        }

        WardrobeHeroSetupStep step = _fullSetupSteps[_stepIndex];
        BuildOptionsForStep(step, _currentOptions);

        string title = GetStepTitle(step);
        string description = GetStepDescription(step);

        ShowOptions(title, description, _currentOptions);
        Log("Показан шаг: " + step);
    }
}
