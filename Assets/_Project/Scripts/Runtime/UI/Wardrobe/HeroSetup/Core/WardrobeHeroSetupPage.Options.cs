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
    void BuildOptionsForStep(WardrobeHeroSetupStep step, List<RuntimeOption> result)
    {
        result.Clear();

        switch (step)
        {
            case WardrobeHeroSetupStep.Appearance:
                List<WardrobeHeroAppearanceOption> appearanceOptions = GetRuntimeAppearanceOptions();
                for (int i = 0; appearanceOptions != null && i < appearanceOptions.Count; i++)
                {
                    var option = appearanceOptions[i];
                    if (!IsAppearanceOptionUsable(option))
                        continue;

                    AppearanceVariant variant = GetAppearanceVariant(option.type);
                    result.Add(new RuntimeOption
                    {
                        Label = FirstNonEmpty(option.label, option.type.ToString()),
                        Preview = GetAppearancePreviewSprite(variant, option.previewSprite),
                        AppearanceType = option.type,
                        AppearanceVariant = variant,
                        SourceIndex = i,
                        Step = WardrobeHeroSetupStep.Appearance
                    });
                }

                if (result.Count == 0 && _useDefaultAppearanceOptionsWhenEmpty)
                    AddDefaultAppearanceOptions(result);
                break;

            case WardrobeHeroSetupStep.Outfit:
                AddClothingOptions(GetRuntimeOutfitItems(), ClothingType.Outfit, step, result);
                break;

            case WardrobeHeroSetupStep.Hair:
                AddClothingOptions(GetRuntimeHairItems(), ClothingType.Hair, step, result);
                break;

            case WardrobeHeroSetupStep.Accessories:
                AddClothingOptions(GetRuntimeAccessoryItems(), ClothingType.Accessory, step, result);
                break;
        }
    }

    void AddClothingOptions(List<ClothingItem> source, ClothingType type, WardrobeHeroSetupStep step, List<RuntimeOption> result)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            ClothingItem item = source[i];
            if (item == null || item.type != type || !IsClothingAllowedForTarget(item))
                continue;

            result.Add(CreateClothingOption(item, i, step));
        }
    }

    void BuildStoryWardrobeOptionsForStep(WardrobeChoiceNode node, WardrobeHeroSetupStep step, List<RuntimeOption> result)
    {
        result.Clear();

        if (node == null || node.availableClothes == null || !TryGetClothingTypeForStep(step, out ClothingType type))
            return;

        int hiddenByRule = 0;
        int nullItems = 0;
        int wrongType = 0;
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

            if (node.TryGetClearSlotType(i, out ClothingType clearType))
            {
                if (clearType != type)
                {
                    wrongType++;
                    continue;
                }

                int clearCost = node.GetPremiumCost(i);
                if (clearCost > 0)
                {
                    blockedByContext++;
                    continue;
                }

                result.Add(CreateClearClothingOption(
                    node.GetOptionLabel(i, "Ничего"),
                    i,
                    clearType));
                continue;
            }

            ClothingItem item = node.availableClothes[i];
            if (item == null)
            {
                nullItems++;
                continue;
            }

            if (item.type != type)
            {
                wrongType++;
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

            result.Add(CreateClothingOption(item, i, step, premiumCost, node.GetOptionLabel(i, "")));
        }

        LogStoryWardrobeOptionsBuilt(
            node,
            step,
            result.Count,
            hiddenByRule,
            nullItems,
            wrongType,
            blockedByContext,
            paidItems,
            ownedPaidItems);
    }

    void LogStoryWardrobeOptionsBuilt(
        WardrobeChoiceNode node,
        WardrobeHeroSetupStep step,
        int visibleCount,
        int hiddenByRule,
        int nullItems,
        int wrongType,
        int blockedByContext,
        int paidItems,
        int ownedPaidItems)
    {
        GetActiveStoryContext(out string storyId, out string chapterId);
        int total = node != null && node.availableClothes != null ? node.availableClothes.Count : 0;

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(WardrobeHeroSetupPage),
            nameof(BuildStoryWardrobeOptionsForStep),
            "[WARDROBE][OPTIONS] Built story wardrobe options for category.",
            LogMetadata.Of(
                "storyId", storyId,
                "chapterId", chapterId,
                "nodeGuid", node != null ? node.guid : "",
                "nodeName", node != null ? node.name : "",
                "step", step.ToString(),
                "totalNodeItems", total,
                "visibleOptions", visibleCount,
                "hiddenByRule", hiddenByRule,
                "nullItems", nullItems,
                "wrongType", wrongType,
                "blockedByContext", blockedByContext,
                "paidItems", paidItems,
                "ownedPaidItems", ownedPaidItems,
                "targetCharacterId", _targetCharacterId));
    }

    static bool TryGetClothingTypeForStep(WardrobeHeroSetupStep step, out ClothingType type)
    {
        switch (step)
        {
            case WardrobeHeroSetupStep.Outfit:
                type = ClothingType.Outfit;
                return true;
            case WardrobeHeroSetupStep.Hair:
                type = ClothingType.Hair;
                return true;
            case WardrobeHeroSetupStep.Accessories:
                type = ClothingType.Accessory;
                return true;
            default:
                type = default;
                return false;
        }
    }

    void AddDefaultAppearanceOptions(List<RuntimeOption> result)
    {
        AddDefaultAppearanceOption(result, "\u0415\u0432\u0440\u043e\u043f\u0435\u0439\u0441\u043a\u0430\u044f", AppearanceType.European);
        AddDefaultAppearanceOption(result, "\u0410\u0437\u0438\u0430\u0442\u0441\u043a\u0430\u044f", AppearanceType.Asian);
        AddDefaultAppearanceOption(result, "\u041b\u0430\u0442\u0438\u043d\u043e\u0430\u043c\u0435\u0440\u0438\u043a\u0430\u043d\u0441\u043a\u0430\u044f", AppearanceType.Latino);
    }

    void AddDefaultAppearanceOption(List<RuntimeOption> result, string label, AppearanceType type)
    {
        if (result == null)
            return;

        AppearanceVariant variant = GetAppearanceVariant(type);
        result.Add(new RuntimeOption
        {
            Label = label,
            Preview = GetAppearancePreviewSprite(variant, null),
            AppearanceType = type,
            AppearanceVariant = variant,
            SourceIndex = result.Count,
            Step = WardrobeHeroSetupStep.Appearance
        });
    }

    RuntimeOption CreateClothingOption(
        ClothingItem item,
        int sourceIndex,
        WardrobeHeroSetupStep step,
        int premiumCost = 0,
        string labelOverride = "")
    {
        premiumCost = SaveDataSanitizer.ClampCurrencyValue(premiumCost);
        return new RuntimeOption
        {
            Label = FormatClothingOptionLabel(item, sourceIndex, premiumCost, labelOverride),
            Preview = item != null ? item.sprite : null,
            Clothing = item,
            PremiumCost = premiumCost,
            SourceIndex = sourceIndex,
            Step = step
        };
    }

    RuntimeOption CreateClearClothingOption(string label, int sourceIndex, ClothingType type)
    {
        return new RuntimeOption
        {
            Label = FirstNonEmpty(label, "Ничего"),
            Preview = null,
            Clothing = null,
            ClearsClothingSlot = true,
            ClearClothingType = type,
            PremiumCost = 0,
            SourceIndex = sourceIndex,
            Step = GetSetupStepForClothingType(type)
        };
    }

    string FormatClothingOptionLabel(ClothingItem item, int sourceIndex, int premiumCost, string labelOverride = "")
    {
        string fallbackLabel = "Вариант " + (sourceIndex + 1);
        if (!string.IsNullOrWhiteSpace(labelOverride))
            return labelOverride.Trim();

        return item != null ? FirstNonEmpty(item.GetDisplayName(), item.id, item.name, fallbackLabel) : fallbackLabel;
    }

    bool IsOwnedClothing(ClothingItem item)
    {
        return item != null &&
            !string.IsNullOrEmpty(item.id) &&
            GameState.Instance != null &&
            GameState.Instance.HasClothing(item.id);
    }

    void ShowOptions(string title, string description, List<RuntimeOption> options)
    {
        KillSystemMessageRestoreTween();
        ClearOptions();
        RemoveGeneratedOptionsContainerIfNeeded();

        if (_titleText != null)
            _titleText.text = title ?? "";

        if (_descriptionText != null)
            _descriptionText.text = description ?? "";

        bool hasOptions = options != null && options.Count > 0;
        if (_emptyText != null)
        {
            _emptyText.gameObject.SetActive(!hasOptions);
            _emptyText.text = hasOptions ? "" : "Нет вариантов для показа. Проверьте список в инспекторе.";
        }

        if (!hasOptions)
        {
            SetWardrobePremiumChoiceBalancePanelVisible(false);

            if (UsesDedicatedPreviewLayers() || _previewImage == _bodyPreviewImage)
            {
                ClearLayeredPreview();
                RefreshBodyPreviewFromCharacter(true);
            }
            else
            {
                SetPreview(null);
            }

            SetSelectedOptionLabel("Недоступно");
            RefreshCurrentOptionActionButton();
            RefreshNavigationButtons();
            NotifyOptionSelectionChanged();
            return;
        }

        if (_selectedOptionIndex < 0)
            _selectedOptionIndex = FindCurrentOptionIndex(options);

        _selectedOptionIndex = Mathf.Clamp(_selectedOptionIndex, 0, options.Count - 1);

        if (_showOptionButtons && _optionsContainer != null)
        {
            for (int i = 0; i < options.Count; i++)
                CreateOptionButton(i, options[i]);
        }

        SelectOption(_selectedOptionIndex, false);
        RefreshNavigationButtons();
    }

    public void ShowTransientSystemMessage(string message, float duration = 2.5f)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        Log(message);

        if (_descriptionText == null)
            return;

        KillSystemMessageRestoreTween();

        _descriptionText.text = message;

        if (duration <= 0f)
            return;

        _systemMessageRestoreTween = DOVirtual.DelayedCall(duration, () =>
        {
            if (_descriptionText != null)
                _descriptionText.text = GetCurrentStepDescription();

            _systemMessageRestoreTween = null;
        }, true).SetTarget(this);
    }

    void KillSystemMessageRestoreTween()
    {
        _systemMessageRestoreTween?.Kill();
        _systemMessageRestoreTween = null;
    }

    string GetCurrentStepDescription()
    {
        if (_currentOptions != null && _currentOptions.Count > 0)
        {
            int optionIndex = Mathf.Clamp(_selectedOptionIndex, 0, _currentOptions.Count - 1);
            RuntimeOption option = _currentOptions[optionIndex];
            if (option != null)
                return GetStepDescription(option.Step);
        }

        if (_mode == OpenMode.FullSetup &&
            _fullSetupSteps != null &&
            _stepIndex >= 0 &&
            _stepIndex < _fullSetupSteps.Count)
        {
            return GetStepDescription(_fullSetupSteps[_stepIndex]);
        }

        return "";
    }

    void CreateOptionButton(int index, RuntimeOption option)
    {
        if (!_showOptionButtons || _optionsContainer == null)
            return;

        Button button = _optionButtonPrefab != null
            ? Instantiate(_optionButtonPrefab, _optionsContainer)
            : null;

        if (button == null)
            return;

        button.gameObject.SetActive(true);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            button.name = EditorPreviewOptionNamePrefix + (index + 1) + " - " + FirstNonEmpty(option != null ? option.Label : "", "Option");
            button.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }
#endif

        var optionView = button.GetComponent<WardrobeOptionButtonView>();
        if (optionView != null)
            optionView.Configure(index, option != null ? option.Label : "", OnOptionClicked);

        RefreshButtonView(button, option, index == _selectedOptionIndex);

        if (Application.isPlaying && optionView == null)
            button.onClick.AddListener(() => OnOptionClicked(index));
    }

    void EnsureRuntimeOptionUi()
    {
        if (_optionsContainer != null)
            return;

        Transform parent = _setupContentRoot != null ? _setupContentRoot.transform : (_pageRoot != null ? _pageRoot.transform : transform);
        var containerObject = new GameObject("RuntimeOptions", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        containerObject.transform.SetParent(parent, false);

        var rect = containerObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(72f, -40f);
        rect.sizeDelta = new Vector2(380f, 520f);

        var layout = containerObject.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(0, 0, 0, 0);

        var fitter = containerObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _optionsContainer = containerObject.transform;
    }

    Button CreateRuntimeOptionButton(Transform parent)
    {
        if (parent == null)
            return null;

        var buttonObject = new GameObject("RuntimeOptionButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 64f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.55f);

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 6f);
        labelRect.offsetMax = new Vector2(-18f, -6f);

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 24f;
        label.color = Color.white;
        label.raycastTarget = false;

        return button;
    }

    void RemoveGeneratedOptionsContainerIfNeeded()
    {
        if (_showOptionButtons)
            return;

        RemoveGeneratedOptionsContainer();
    }

    void QueueRemoveGeneratedOptionsContainer()
    {
        if (_showOptionButtons)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                    RemoveGeneratedOptionsContainer();
            };
            return;
        }
#endif

        RemoveGeneratedOptionsContainer();
    }

    void RemoveGeneratedOptionsContainer()
    {
        Transform generated = FindGeneratedOptionsContainer();
        if (generated == null)
            return;

        if (_optionsContainer == generated)
            _optionsContainer = null;

        if (Application.isPlaying)
            Destroy(generated.gameObject);
        else
            DestroyImmediate(generated.gameObject);
    }

    Transform FindGeneratedOptionsContainer()
    {
        if (_optionsContainer != null && string.Equals(_optionsContainer.name, GeneratedOptionsContainerName, StringComparison.Ordinal))
            return _optionsContainer;

        Transform parent = _setupContentRoot != null
            ? _setupContentRoot.transform
            : _pageRoot != null ? _pageRoot.transform : transform;

        return parent != null ? parent.Find(GeneratedOptionsContainerName) : null;
    }

    void RefreshAllOptionButtons()
    {
        if (_optionsContainer == null)
            return;

        int optionIndex = 0;
        for (int i = 0; i < _optionsContainer.childCount && optionIndex < _currentOptions.Count; i++)
        {
            Transform child = _optionsContainer.GetChild(i);

#if UNITY_EDITOR
            if (!Application.isPlaying && (child == null || !child.name.StartsWith(EditorPreviewOptionNamePrefix, StringComparison.Ordinal)))
                continue;
#endif

            var button = child.GetComponent<Button>();
            if (button != null)
                RefreshButtonView(button, _currentOptions[optionIndex], optionIndex == _selectedOptionIndex);

            optionIndex++;
        }
    }

    void RefreshButtonView(Button button, RuntimeOption option, bool selected)
    {
        if (button == null || option == null)
            return;

        int premiumCost = GetVisiblePremiumCost(option);
        var priceLayout = button.GetComponentInChildren<InlinePriceIconLayout>(true);
        var optionView = button.GetComponent<WardrobeOptionButtonView>();

        if (optionView != null)
        {
            optionView.SetOptionLabel(option.Label);
            optionView.SetPremiumCost(premiumCost, _premiumCostIcon);
            optionView.SetSelected(IsOptionApplied(option));
        }
        else
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            string prefix = selected && _markSelectedOptionInText ? _selectedOptionPrefix : "";
            if (priceLayout != null)
            {
                priceLayout.SetContent(prefix + option.Label, premiumCost, _premiumCostIcon);
            }
            else if (label != null)
            {
                label.text = prefix + FormatLabelWithTextPrice(option.Label, premiumCost);
            }
        }

        if (!_fillPreviewImageInsideOptionButton)
            return;

        var images = button.GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (image == null || image == button.targetGraphic)
                continue;

            if (priceLayout != null && image == priceLayout.Icon)
                continue;

            image.sprite = option.Preview;
            image.enabled = option.Preview != null;
            break;
        }
    }
}
