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
    void Reset()
    {
        _pageRoot = gameObject;
        _pageCanvasGroup = GetComponent<CanvasGroup>();
    }

    void Awake()
    {
        AutoWire();
        PrepareOptionArrowFades();
        RemoveGeneratedOptionsContainerIfNeeded();
        CaptureLayerDefaults();
        BindButtons();
        SetSetupObjectsVisible(false);
    }

    void OnEnable()
    {
        PrepareOptionArrowFades();
        ClothingItem.Changed -= OnClothingItemChanged;
        ClothingItem.Changed += OnClothingItemChanged;
        CharacterData.Changed -= OnCharacterDataChanged;
        CharacterData.Changed += OnCharacterDataChanged;
        PlayerData.HeartsChanged -= HandleWardrobePremiumChoiceHeartsChanged;
        PlayerData.HeartsChanged += HandleWardrobePremiumChoiceHeartsChanged;
        RefreshWardrobePremiumChoiceBalanceText();

        if (!Application.isPlaying)
            return;

        if (_debugAutoStartFullSetupOnEnable && !_isOpen)
            OpenFullSetup();
    }

    void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_completionPrefsKey))
            _completionPrefsKey = DefaultCompletionPrefsKey;

        if (string.IsNullOrWhiteSpace(_targetCharacterId))
            _targetCharacterId = "hero";

        if (string.IsNullOrWhiteSpace(_outfitSlotSuffix))
            _outfitSlotSuffix = "outfit";

        if (string.IsNullOrWhiteSpace(_hairSlotSuffix))
            _hairSlotSuffix = "hair";

        if (string.IsNullOrWhiteSpace(_accessorySlotSuffix))
            _accessorySlotSuffix = "accessory";

        if (_appearanceOptions == null)
            _appearanceOptions = new List<WardrobeHeroAppearanceOption>();

        _previousOptionDisabledFades ??= Array.Empty<UISpriteStateFade>();
        _nextOptionDisabledFades ??= Array.Empty<UISpriteStateFade>();
        PrepareOptionArrowFades();

        AutoWire();
        QueueRemoveGeneratedOptionsContainer();

#if UNITY_EDITOR
        if (_editorPreviewEnabled)
            QueueEditorPreviewRefresh();
        else if (gameObject.activeInHierarchy)
            RefreshPreviewAfterSourceChange();
#endif
    }

    void OnDisable()
    {
        KillOptionPreviewTween(true);
        KillLayeredPreviewOpenTween(true);
        KillSystemMessageRestoreTween();
        HideWardrobePremiumChoiceBalancePanel();
        PlayerData.HeartsChanged -= HandleWardrobePremiumChoiceHeartsChanged;
        ClothingItem.Changed -= OnClothingItemChanged;
        CharacterData.Changed -= OnCharacterDataChanged;
    }

    void OnDestroy()
    {
        KillOptionPreviewTween(false);
        KillLayeredPreviewOpenTween(false);
        KillSystemMessageRestoreTween();
        HideWardrobePremiumChoiceBalancePanel();
        PlayerData.HeartsChanged -= HandleWardrobePremiumChoiceHeartsChanged;
        ClothingItem.Changed -= OnClothingItemChanged;
        CharacterData.Changed -= OnCharacterDataChanged;
        UnbindButtons();
    }
}
