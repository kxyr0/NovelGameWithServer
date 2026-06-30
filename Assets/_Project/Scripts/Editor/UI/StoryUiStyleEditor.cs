using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using Unity.VectorGraphics;
using UnityEngine.U2D;
using UnityEngine.UI;

[CustomEditor(typeof(StoryUiStyle))]
[CanEditMultipleObjects]
public sealed class StoryUiStyleEditor : Editor
{
    const double SceneAutoApplyDelaySeconds = 0.32d;
    const double PhoneInlineApplyDelaySeconds = 0.28d;

    enum InspectorTab
    {
        Dialogue,
        NameInput,
        Choices,
        Stats,
        Chapter,
        EndScreen,
        Phone,
        Advanced
    }

    enum NameInputTab
    {
        Background,
        Field,
        Text,
        Button,
        ExtraTexts
    }

    enum StatsTab
    {
        Panel,
        Definitions,
        Layout,
        Relationships
    }

    enum ChapterTab
    {
        Panel,
        Position,
        Text,
        Padding,
        Motion
    }

    static readonly GUIContent[] TabLabels =
    {
        new GUIContent("Диалог"),
        new GUIContent("Имя"),
        new GUIContent("Выборы"),
        new GUIContent("Статы"),
        new GUIContent("Глава"),
        new GUIContent("Финал"),
        new GUIContent("Телефон"),
        new GUIContent("Доп.")
    };

    static readonly GUIContent[] NameInputTabLabels =
    {
        new GUIContent("Фон"),
        new GUIContent("Поле"),
        new GUIContent("Текст"),
        new GUIContent("Кнопка"),
        new GUIContent("Доп. тексты")
    };

    static readonly GUIContent[] StatsTabLabels =
    {
        new GUIContent("Плашка"),
        new GUIContent("Список"),
        new GUIContent("Layout"),
        new GUIContent("Отношения")
    };

    static readonly GUIContent[] ChapterTabLabels =
    {
        new GUIContent("Плашка"),
        new GUIContent("Позиция"),
        new GUIContent("Текст"),
        new GUIContent("Padding"),
        new GUIContent("Движение")
    };

    InspectorTab _activeTab;
    NameInputTab _nameInputTab;
    StatsTab _statsTab;
    ChapterTab _chapterTab;
    bool _showDisabledOverrideValues;
    bool _showHints;
    bool _showSceneBindings;
    bool _sceneBindingsChanged;
    bool _applyToSceneAutomatically = true;
    double _queuedApplyAt;
    List<RelationshipCharacterOption> _relationshipCharacterOptionsCache;
    PhoneDialogueNode _phoneInspectorPreviewNode;
    string _phoneInspectorPreviewContactName = "\u0420\u043E\u0431";
    string _phoneInspectorPreviewScript =
        "\u041C\u044D\u0433: \u0423 \u043C\u0435\u043D\u044F \u0431\u0443\u0434\u0435\u0442 \u043F\u043E\u0434\u043A\u0430\u0441\u0442 \u0441 \u0413\u0430\u0431\u0440\u0438\u044D\u043B\u0435\u043C \u041C\u043E\u0440\u0442\u0435\u043B\u043B\u043E\u043C!!!\n{PlayerName}: \u0421 \u043A\u0435\u043C?\n\u041C\u044D\u0433: \u0421\u0442\u044B\u0434\u043D\u043E \u043D\u0435 \u0437\u043D\u0430\u0442\u044C, \u0441 \u0442\u0432\u043E\u0435\u0439-\u0442\u043E \u043F\u0440\u043E\u0444\u0435\u0441\u0441\u0438\u0435\u0439))\n\u041C\u044D\u0433: \u0424\u043E\u0442\u043E [\u0444\u043E\u0442\u043E]";

    SerializedProperty _backgroundSprite;
    SerializedProperty _backgroundSpriteSource;
    SerializedProperty _dialogueApplyOnlySprites;
    SerializedProperty _overrideDialogueBackgroundAnchors;
    SerializedProperty _dialogueBackgroundAnchorMin;
    SerializedProperty _dialogueBackgroundAnchorMax;
    SerializedProperty _overrideDialogueBackgroundPivot;
    SerializedProperty _dialogueBackgroundPivot;
    SerializedProperty _overrideDialogueBackgroundRect;
    SerializedProperty _dialogueBackgroundAnchoredPosition;
    SerializedProperty _dialogueBackgroundSizeDelta;
    SerializedProperty _overrideDialogueBackgroundStretchOffsets;
    SerializedProperty _dialogueBackgroundStretchOffsets;
    SerializedProperty _overrideDialoguePanelRect;
    SerializedProperty _dialoguePanelAnchoredPosition;
    SerializedProperty _dialoguePanelSizeDelta;
    SerializedProperty _overrideDialoguePanelAutoHeight;
    SerializedProperty _dialoguePanelAutoHeight;
    SerializedProperty _dialoguePanelAutoHeightPadding;
    SerializedProperty _dialoguePanelAutoMinHeight;
    SerializedProperty _dialoguePanelAutoMaxHeight;
    SerializedProperty _dialoguePanelAutoHeightKeepTop;
    SerializedProperty _dialoguePanelAutoHeightGrowthUpFactor;
    SerializedProperty _overrideDialoguePanelVerticalLayout;
    SerializedProperty _dialoguePanelVerticalLayoutPadding;
    SerializedProperty _dialoguePanelVerticalLayoutSpacing;
    SerializedProperty _dialoguePanelVerticalLayoutChildAlignment;
    SerializedProperty _dialoguePanelVerticalLayoutReverseArrangement;
    SerializedProperty _dialoguePanelVerticalLayoutControlChildWidth;
    SerializedProperty _dialoguePanelVerticalLayoutControlChildHeight;
    SerializedProperty _dialoguePanelVerticalLayoutUseChildScaleWidth;
    SerializedProperty _dialoguePanelVerticalLayoutUseChildScaleHeight;
    SerializedProperty _dialoguePanelVerticalLayoutChildForceExpandWidth;
    SerializedProperty _dialoguePanelVerticalLayoutChildForceExpandHeight;
    SerializedProperty _overrideDialoguePanelContentSizeFitter;
    SerializedProperty _dialoguePanelContentSizeFitterHorizontalFit;
    SerializedProperty _dialoguePanelContentSizeFitterVerticalFit;
    SerializedProperty _overrideBodyTextOffsetY;
    SerializedProperty _bodyTextOffsetY;
    SerializedProperty _overrideBodyTextTopOffsetY;
    SerializedProperty _bodyTextTopOffsetY;
    SerializedProperty _overrideBodyTextGrowDownOffsetX;
    SerializedProperty _bodyTextGrowDownOffsetX;
    SerializedProperty _overrideBodyTextResizeHeightToPreferredText;
    SerializedProperty _bodyTextResizeHeightToPreferredText;
    SerializedProperty _overrideBodyTextExtraHeight;
    SerializedProperty _bodyTextExtraHeight;
    SerializedProperty _overrideBodyTextMinHeight;
    SerializedProperty _bodyTextMinHeight;
    SerializedProperty _overrideBodyTextMaxHeight;
    SerializedProperty _bodyTextMaxHeight;
    SerializedProperty _overrideBodyTextMaxFontSize;
    SerializedProperty _bodyTextMaxFontSize;
    SerializedProperty _overrideBodyTextFont;
    SerializedProperty _bodyTextFont;
    SerializedProperty _overrideBodyTextShrinkTextToFitRect;
    SerializedProperty _bodyTextShrinkTextToFitRect;
    SerializedProperty _overrideBodyTextMinAutoFontSize;
    SerializedProperty _bodyTextMinAutoFontSize;
    SerializedProperty _overrideBodyTextOverflowModeWhenStillTooLarge;
    SerializedProperty _bodyTextOverflowModeWhenStillTooLarge;
    SerializedProperty _overrideBodyTextHorizontalClamp;
    SerializedProperty _bodyTextHorizontalClamp;
    SerializedProperty _bodyTextHorizontalInset;
    SerializedProperty _bodyTextMaxWidth;
    SerializedProperty _dialogueExtraLayers;
    SerializedProperty _overrideCharacterNameOffset;
    SerializedProperty _characterNameOffset;
    SerializedProperty _overrideCharacterNameFont;
    SerializedProperty _characterNameFont;
    SerializedProperty _overrideCharacterNameFontSize;
    SerializedProperty _characterNameFontSize;
    SerializedProperty _namePlateSprite;
    SerializedProperty _namePlateSpriteSource;
    SerializedProperty _overrideNamePlateColor;
    SerializedProperty _namePlateColor;
    SerializedProperty _overrideNamePlateImageType;
    SerializedProperty _namePlateImageType;
    SerializedProperty _overrideNamePlatePreserveAspect;
    SerializedProperty _namePlatePreserveAspect;
    SerializedProperty _overrideNamePlatePixelsPerUnitMultiplier;
    SerializedProperty _namePlatePixelsPerUnitMultiplier;
    SerializedProperty _overrideNamePlateMaterial;
    SerializedProperty _namePlateMaterial;
    SerializedProperty _overrideNamePlateRaycastTarget;
    SerializedProperty _namePlateRaycastTarget;
    SerializedProperty _overrideNamePlateAnchors;
    SerializedProperty _namePlateAnchorMin;
    SerializedProperty _namePlateAnchorMax;
    SerializedProperty _overrideNamePlatePivot;
    SerializedProperty _namePlatePivot;
    SerializedProperty _overrideNamePlateRect;
    SerializedProperty _namePlateAnchoredPosition;
    SerializedProperty _namePlateSizeDelta;

    SerializedProperty _nameScreenBackgroundSprite;
    SerializedProperty _nameScreenBackgroundSpriteSource;
    SerializedProperty _nameInputApplyOnlySprites;
    SerializedProperty _overrideNameScreenBackgroundColor;
    SerializedProperty _nameScreenBackgroundColor;
    SerializedProperty _overrideNameScreenBackgroundImageType;
    SerializedProperty _nameScreenBackgroundImageType;
    SerializedProperty _namePanelBackgroundSprite;
    SerializedProperty _namePanelBackgroundSpriteSource;
    SerializedProperty _overrideNamePanelBackgroundColor;
    SerializedProperty _namePanelBackgroundColor;
    SerializedProperty _overrideNamePanelBackgroundImageType;
    SerializedProperty _namePanelBackgroundImageType;
    SerializedProperty _overrideNamePanelBackgroundRect;
    SerializedProperty _namePanelBackgroundAnchoredPosition;
    SerializedProperty _namePanelBackgroundSizeDelta;
    SerializedProperty _nameInputFieldSprite;
    SerializedProperty _nameInputFieldSpriteSource;
    SerializedProperty _overrideNameInputFieldColor;
    SerializedProperty _nameInputFieldColor;
    SerializedProperty _overrideNameInputFieldImageType;
    SerializedProperty _nameInputFieldImageType;
    SerializedProperty _overrideNameInputFieldRect;
    SerializedProperty _nameInputFieldAnchoredPosition;
    SerializedProperty _nameInputFieldSizeDelta;
    SerializedProperty _overrideNameInputTextRect;
    SerializedProperty _nameInputTextAnchoredPosition;
    SerializedProperty _nameInputTextSizeDelta;
    SerializedProperty _overrideNameInputTextColor;
    SerializedProperty _nameInputTextColor;
    SerializedProperty _overrideNameInputTextFont;
    SerializedProperty _nameInputTextFont;
    SerializedProperty _overrideNameInputTextFontSize;
    SerializedProperty _nameInputTextFontSize;
    SerializedProperty _overrideNamePlaceholderTextRect;
    SerializedProperty _namePlaceholderTextAnchoredPosition;
    SerializedProperty _namePlaceholderTextSizeDelta;
    SerializedProperty _overrideNamePlaceholderTextColor;
    SerializedProperty _namePlaceholderTextColor;
    SerializedProperty _overrideNamePlaceholderTextFont;
    SerializedProperty _namePlaceholderTextFont;
    SerializedProperty _overrideNamePlaceholderTextFontSize;
    SerializedProperty _namePlaceholderTextFontSize;
    SerializedProperty _nameConfirmButtonPrefabOverride;
    SerializedProperty _nameConfirmButtonSprite;
    SerializedProperty _nameConfirmButtonSpriteSource;
    SerializedProperty _overrideNameConfirmButtonColor;
    SerializedProperty _nameConfirmButtonColor;
    SerializedProperty _overrideNameConfirmButtonImageType;
    SerializedProperty _nameConfirmButtonImageType;
    SerializedProperty _overrideNameConfirmButtonRect;
    SerializedProperty _nameConfirmButtonAnchoredPosition;
    SerializedProperty _nameConfirmButtonSizeDelta;
    SerializedProperty _overrideNameConfirmButtonTextRect;
    SerializedProperty _nameConfirmButtonTextAnchoredPosition;
    SerializedProperty _nameConfirmButtonTextSizeDelta;
    SerializedProperty _overrideNameConfirmButtonTextColor;
    SerializedProperty _nameConfirmButtonTextColor;
    SerializedProperty _overrideNameConfirmButtonTextFont;
    SerializedProperty _nameConfirmButtonTextFont;
    SerializedProperty _overrideNameConfirmButtonTextFontSize;
    SerializedProperty _nameConfirmButtonTextFontSize;
    SerializedProperty _useNameExtraTextOne;
    SerializedProperty _nameExtraTextOneText;
    SerializedProperty _overrideNameExtraTextOneRect;
    SerializedProperty _nameExtraTextOneAnchoredPosition;
    SerializedProperty _nameExtraTextOneSizeDelta;
    SerializedProperty _overrideNameExtraTextOneColor;
    SerializedProperty _nameExtraTextOneColor;
    SerializedProperty _overrideNameExtraTextOneFont;
    SerializedProperty _nameExtraTextOneFont;
    SerializedProperty _overrideNameExtraTextOneFontSize;
    SerializedProperty _nameExtraTextOneFontSize;
    SerializedProperty _useNameExtraTextTwo;
    SerializedProperty _nameExtraTextTwoText;
    SerializedProperty _overrideNameExtraTextTwoRect;
    SerializedProperty _nameExtraTextTwoAnchoredPosition;
    SerializedProperty _nameExtraTextTwoSizeDelta;
    SerializedProperty _overrideNameExtraTextTwoColor;
    SerializedProperty _nameExtraTextTwoColor;
    SerializedProperty _overrideNameExtraTextTwoFont;
    SerializedProperty _nameExtraTextTwoFont;
    SerializedProperty _overrideNameExtraTextTwoFontSize;
    SerializedProperty _nameExtraTextTwoFontSize;
    SerializedProperty _nameExtraTexts;

    SerializedProperty _choiceButtonPrefabOverride;
    SerializedProperty _premiumChoiceButtonPrefabOverride;
    SerializedProperty _premiumChoiceBalancePanelPrefabOverride;
    SerializedProperty _premiumChoiceBalancePanelOffset;
    SerializedProperty _choicesApplyOnlySprites;
    SerializedProperty _choiceButtonSprite;
    SerializedProperty _choiceButtonSpriteSource;
    SerializedProperty _overrideChoiceButtonColor;
    SerializedProperty _choiceButtonColor;
    SerializedProperty _overrideChoiceButtonImageType;
    SerializedProperty _choiceButtonImageType;
    SerializedProperty _overrideChoiceButtonTextColor;
    SerializedProperty _choiceButtonTextColor;
    SerializedProperty _overrideChoiceButtonFont;
    SerializedProperty _choiceButtonFont;
    SerializedProperty _overrideChoiceButtonFontSize;
    SerializedProperty _choiceButtonFontSize;
    SerializedProperty _overrideChoiceButtonPadding;
    SerializedProperty _choiceButtonPadding;
    SerializedProperty _overrideChoiceButtonTextPadding;
    SerializedProperty _choiceButtonTextPadding;
    SerializedProperty _overrideChoiceButtonTextOffset;
    SerializedProperty _choiceButtonTextOffset;
    SerializedProperty _choicePanelSprite;
    SerializedProperty _choicePanelSpriteSource;
    SerializedProperty _overrideChoicePanelColor;
    SerializedProperty _choicePanelColor;
    SerializedProperty _overrideChoicePanelImageType;
    SerializedProperty _choicePanelImageType;

    SerializedProperty _statPanelSprite;
    SerializedProperty _statPanelSpriteSource;
    SerializedProperty _statsApplyOnlySprites;
    SerializedProperty _overrideStatPanelColor;
    SerializedProperty _statPanelColor;
    SerializedProperty _overrideStatPanelImageType;
    SerializedProperty _statPanelImageType;
    SerializedProperty _overrideStatPanelBackgroundAnchors;
    SerializedProperty _statPanelBackgroundAnchorMin;
    SerializedProperty _statPanelBackgroundAnchorMax;
    SerializedProperty _overrideStatPanelBackgroundPivot;
    SerializedProperty _statPanelBackgroundPivot;
    SerializedProperty _overrideStatPanelBackgroundStretchOffsets;
    SerializedProperty _statPanelBackgroundStretchOffsets;
    SerializedProperty _overrideStatTextColor;
    SerializedProperty _statTextColor;
    SerializedProperty _overrideStatTextFont;
    SerializedProperty _statTextFont;
    SerializedProperty _overrideStatTextFontSize;
    SerializedProperty _statTextFontSize;
    SerializedProperty _overrideStatPanelRect;
    SerializedProperty _statPanelAnchoredPosition;
    SerializedProperty _statPanelSizeDelta;
    SerializedProperty _statPanelSizeOverrides;
    SerializedProperty _overrideStatTextRect;
    SerializedProperty _statTextAnchoredPosition;
    SerializedProperty _statTextSizeDelta;
    SerializedProperty _statTextRectOverrides;
    SerializedProperty _overrideStatTextAutoSize;
    SerializedProperty _statTextAutoSize;
    SerializedProperty _overrideStatTextAutoFontSizeRange;
    SerializedProperty _statTextMinAutoFontSize;
    SerializedProperty _statTextMaxAutoFontSize;
    SerializedProperty _overrideStatTextAlignment;
    SerializedProperty _statTextAlignment;
    SerializedProperty _overrideStatTextWordWrapping;
    SerializedProperty _statTextWordWrapping;
    SerializedProperty _overrideStatTextOverflowMode;
    SerializedProperty _statTextOverflowMode;
    SerializedProperty _overrideStatTextLineSpacing;
    SerializedProperty _statTextLineSpacing;
    SerializedProperty _overrideStatTextMargins;
    SerializedProperty _statTextMargins;
    SerializedProperty _replaceStatDefinitions;
    SerializedProperty _statOverlayDefinitions;
    SerializedProperty _statDefinitionAssets;
    SerializedProperty _overrideStatPanelPadding;
    SerializedProperty _statPanelPadding;
    SerializedProperty _overrideStatIconSize;
    SerializedProperty _statIconSize;
    SerializedProperty _overrideStatIconOffset;
    SerializedProperty _statIconOffset;
    SerializedProperty _overrideStatIconVisualScale;
    SerializedProperty _statIconVisualScale;
    SerializedProperty _overrideStatIconMinSize;
    SerializedProperty _statIconMinSize;
    SerializedProperty _overrideStatIconReserveSpaceWhenHidden;
    SerializedProperty _statIconReserveSpaceWhenHidden;
    SerializedProperty _overrideStatIconParentSpacing;
    SerializedProperty _statIconParentSpacing;
    SerializedProperty _overrideStatIconParentPadding;
    SerializedProperty _statIconParentPadding;
    SerializedProperty _statIconOffsetOverrides;
    SerializedProperty _overrideStatPanelVerticalLayout;
    SerializedProperty _statPanelVerticalLayoutPadding;
    SerializedProperty _statPanelVerticalLayoutSpacing;
    SerializedProperty _statPanelVerticalLayoutChildAlignment;
    SerializedProperty _statPanelVerticalLayoutReverseArrangement;
    SerializedProperty _statPanelVerticalLayoutControlChildWidth;
    SerializedProperty _statPanelVerticalLayoutControlChildHeight;
    SerializedProperty _statPanelVerticalLayoutUseChildScaleWidth;
    SerializedProperty _statPanelVerticalLayoutUseChildScaleHeight;
    SerializedProperty _statPanelVerticalLayoutChildForceExpandWidth;
    SerializedProperty _statPanelVerticalLayoutChildForceExpandHeight;
    SerializedProperty _overrideStatPanelContentSizeFitter;
    SerializedProperty _statPanelContentSizeFitterHorizontalFit;
    SerializedProperty _statPanelContentSizeFitterVerticalFit;
    SerializedProperty _overrideRelationshipFrameSize;
    SerializedProperty _relationshipFrameSize;
    SerializedProperty _relationshipFrameAnchoredPosition;
    SerializedProperty _overrideRelationshipPanelBackgroundAnchors;
    SerializedProperty _relationshipPanelBackgroundAnchorMin;
    SerializedProperty _relationshipPanelBackgroundAnchorMax;
    SerializedProperty _overrideRelationshipPanelBackgroundPivot;
    SerializedProperty _relationshipPanelBackgroundPivot;
    SerializedProperty _overrideRelationshipPanelBackgroundRect;
    SerializedProperty _relationshipPanelBackgroundAnchoredPosition;
    SerializedProperty _relationshipPanelBackgroundSizeDelta;
    SerializedProperty _overrideRelationshipPanelBackgroundStretchOffsets;
    SerializedProperty _relationshipPanelBackgroundStretchOffsets;
    SerializedProperty _overrideRelationshipPanelVerticalLayout;
    SerializedProperty _relationshipPanelVerticalLayoutPadding;
    SerializedProperty _relationshipPanelVerticalLayoutSpacing;
    SerializedProperty _relationshipPanelVerticalLayoutChildAlignment;
    SerializedProperty _relationshipPanelVerticalLayoutReverseArrangement;
    SerializedProperty _relationshipPanelVerticalLayoutControlChildWidth;
    SerializedProperty _relationshipPanelVerticalLayoutControlChildHeight;
    SerializedProperty _relationshipPanelVerticalLayoutUseChildScaleWidth;
    SerializedProperty _relationshipPanelVerticalLayoutUseChildScaleHeight;
    SerializedProperty _relationshipPanelVerticalLayoutChildForceExpandWidth;
    SerializedProperty _relationshipPanelVerticalLayoutChildForceExpandHeight;
    SerializedProperty _overrideRelationshipPanelContentSizeFitter;
    SerializedProperty _relationshipPanelContentSizeFitterHorizontalFit;
    SerializedProperty _relationshipPanelContentSizeFitterVerticalFit;
    SerializedProperty _overrideRelationshipFontSizeRange;
    SerializedProperty _relationshipFontSizeMin;
    SerializedProperty _relationshipFontSizeMax;
    SerializedProperty _overrideRelationshipMaxVisibleLines;
    SerializedProperty _relationshipMaxVisibleLines;
    SerializedProperty _relationshipMessageOverrides;

    SerializedProperty _chapterTitlePanelSprite;
    SerializedProperty _chapterTitlePanelSpriteSource;
    SerializedProperty _chapterApplyOnlySprites;
    SerializedProperty _overrideChapterTitlePanelColor;
    SerializedProperty _chapterTitlePanelColor;
    SerializedProperty _overrideChapterTitlePanelImageType;
    SerializedProperty _chapterTitlePanelImageType;
    SerializedProperty _overrideChapterTitleTextColor;
    SerializedProperty _chapterTitleTextColor;
    SerializedProperty _overrideChapterTitleTextFont;
    SerializedProperty _chapterTitleTextFont;
    SerializedProperty _overrideChapterTitleTextFontSize;
    SerializedProperty _chapterTitleTextFontSize;
    SerializedProperty _overrideChapterTitleTextRect;
    SerializedProperty _chapterTitleTextAnchoredPosition;
    SerializedProperty _chapterTitleTextSizeDelta;
    SerializedProperty _overrideChapterTitleTextHeightLimits;
    SerializedProperty _chapterTitleTextMinHeight;
    SerializedProperty _chapterTitleTextMaxHeight;
    SerializedProperty _overrideChapterTitleTextAutoSize;
    SerializedProperty _chapterTitleTextAutoSize;
    SerializedProperty _overrideChapterTitleTextAutoFontSizeRange;
    SerializedProperty _chapterTitleTextMinAutoFontSize;
    SerializedProperty _chapterTitleTextMaxAutoFontSize;
    SerializedProperty _overrideChapterTitleTextAlignment;
    SerializedProperty _chapterTitleTextAlignment;
    SerializedProperty _overrideChapterTitleTextWordWrapping;
    SerializedProperty _chapterTitleTextWordWrapping;
    SerializedProperty _overrideChapterTitleTextOverflowMode;
    SerializedProperty _chapterTitleTextOverflowMode;
    SerializedProperty _overrideChapterTitleTextLineSpacing;
    SerializedProperty _chapterTitleTextLineSpacing;
    SerializedProperty _overrideChapterTitleTextMargins;
    SerializedProperty _chapterTitleTextMargins;
    SerializedProperty _overrideChapterTitleCenterOnShow;
    SerializedProperty _chapterTitleCenterOnShow;
    SerializedProperty _overrideChapterTitleBringToFrontOnShow;
    SerializedProperty _chapterTitleBringToFrontOnShow;
    SerializedProperty _overrideChapterTitleBackgroundDimSizeMode;
    SerializedProperty _chapterTitleBackgroundDimSizeMode;
    SerializedProperty _overrideChapterTitleBackgroundDimFixedSize;
    SerializedProperty _chapterTitleBackgroundDimFixedSize;
    SerializedProperty _overrideChapterTitleBackgroundDimColor;
    SerializedProperty _chapterTitleBackgroundDimColor;
    SerializedProperty _overrideChapterTitleBackgroundDimAlpha;
    SerializedProperty _chapterTitleBackgroundDimAlpha;
    SerializedProperty _overrideChapterTitleTextMode;
    SerializedProperty _chapterTitleTextMode;
    SerializedProperty _overrideChapterTitleTextFormat;
    SerializedProperty _chapterTitleTextFormat;
    SerializedProperty _overrideChapterTitleNumberAndTitleFormat;
    SerializedProperty _chapterTitleNumberAndTitleFormat;
    SerializedProperty _overrideChapterTitleNumberOffset;
    SerializedProperty _chapterTitleNumberOffset;
    SerializedProperty _overrideChapterTitleEmptyTitleFallback;
    SerializedProperty _chapterTitleEmptyTitleFallback;
    SerializedProperty _overrideChapterTitleTrimTitle;
    SerializedProperty _chapterTitleTrimTitle;
    SerializedProperty _overrideChapterTitleUppercaseTitle;
    SerializedProperty _chapterTitleUppercaseTitle;
    SerializedProperty _overrideChapterTitleSpecificPaddingSettings;
    SerializedProperty _chapterTitleUseSpecificPadding;
    SerializedProperty _chapterTitleSpecificPaddingMarkers;
    SerializedProperty _chapterTitleSpecificPadding;
    SerializedProperty _overrideChapterTitleAnimationMode;
    SerializedProperty _chapterTitleAnimationMode;
    SerializedProperty _overrideChapterTitleShownPosition;
    SerializedProperty _chapterTitleShownPosition;
    SerializedProperty _overrideChapterTitleCaptureShownPositionOnAwake;
    SerializedProperty _chapterTitleCaptureShownPositionOnAwake;
    SerializedProperty _overrideChapterTitleHiddenOffsetY;
    SerializedProperty _chapterTitleHiddenOffsetY;
    SerializedProperty _overrideChapterTitleEnterDuration;
    SerializedProperty _chapterTitleEnterDuration;
    SerializedProperty _overrideChapterTitleVisibleDuration;
    SerializedProperty _chapterTitleVisibleDuration;
    SerializedProperty _overrideChapterTitleExitDuration;
    SerializedProperty _chapterTitleExitDuration;
    SerializedProperty _overrideChapterTitleFadeWithMovement;
    SerializedProperty _chapterTitleFadeWithMovement;
    SerializedProperty _overrideChapterTitleAnimatePosition;
    SerializedProperty _chapterTitleAnimatePosition;
    SerializedProperty _overrideChapterTitleUseUnscaledTime;
    SerializedProperty _chapterTitleUseUnscaledTime;
    SerializedProperty _overrideChapterTitleDisableRootAfterExit;
    SerializedProperty _chapterTitleDisableRootAfterExit;

    SerializedProperty _overrideColor;
    SerializedProperty _color;
    SerializedProperty _overrideImageType;
    SerializedProperty _imageType;
    SerializedProperty _overridePreserveAspect;
    SerializedProperty _preserveAspect;
    SerializedProperty _overridePixelsPerUnitMultiplier;
    SerializedProperty _pixelsPerUnitMultiplier;
    SerializedProperty _overrideMaterial;
    SerializedProperty _material;
    SerializedProperty _overrideRaycastTarget;
    SerializedProperty _raycastTarget;

    bool _showDialogue = true;
    bool _showNameInput = true;
    bool _showChoices = true;
    bool _showStats = true;
    bool _showChapter = true;
    bool _showAdvanced;
    bool _showEndScreenReferences = true;
    bool _showEndScreenRoot = true;
    bool _showEndScreenBackground = true;
    bool _showEndScreenTexts = true;
    bool _showEndScreenStats = true;
    bool _showEndScreenButton = true;
    bool _showEndScreenLayout;
    bool _showEndScreenPreview;
    bool _showEndScreenStatBindings = true;
    bool _showEndScreenSplitTextReferences;
    bool _applyTargetsQueued;
    bool _phoneApplyQueued;
    double _queuedPhoneApplyAt;
    StoryUserInterface _queuedPhoneOwner;
    PhoneDialogueUI _queuedPhoneUi;
    bool _queuedPhoneRecalculateLayout;
    StoryUserInterface _cachedPhoneValidationOwner;
    PhonePreviewValidationResult _cachedPhoneValidation;
    bool _phoneValidationDirty = true;

    void OnEnable()
    {
        _backgroundSprite = serializedObject.FindProperty("_backgroundSprite");
        _backgroundSpriteSource = serializedObject.FindProperty("_backgroundSpriteSource");
        _dialogueApplyOnlySprites = serializedObject.FindProperty("_dialogueApplyOnlySprites");
        _overrideDialogueBackgroundAnchors = serializedObject.FindProperty("_overrideDialogueBackgroundAnchors");
        _dialogueBackgroundAnchorMin = serializedObject.FindProperty("_dialogueBackgroundAnchorMin");
        _dialogueBackgroundAnchorMax = serializedObject.FindProperty("_dialogueBackgroundAnchorMax");
        _overrideDialogueBackgroundPivot = serializedObject.FindProperty("_overrideDialogueBackgroundPivot");
        _dialogueBackgroundPivot = serializedObject.FindProperty("_dialogueBackgroundPivot");
        _overrideDialogueBackgroundRect = serializedObject.FindProperty("_overrideDialogueBackgroundRect");
        _dialogueBackgroundAnchoredPosition = serializedObject.FindProperty("_dialogueBackgroundAnchoredPosition");
        _dialogueBackgroundSizeDelta = serializedObject.FindProperty("_dialogueBackgroundSizeDelta");
        _overrideDialogueBackgroundStretchOffsets = serializedObject.FindProperty("_overrideDialogueBackgroundStretchOffsets");
        _dialogueBackgroundStretchOffsets = serializedObject.FindProperty("_dialogueBackgroundStretchOffsets");
        _overrideDialoguePanelRect = serializedObject.FindProperty("_overrideDialoguePanelRect");
        _dialoguePanelAnchoredPosition = serializedObject.FindProperty("_dialoguePanelAnchoredPosition");
        _dialoguePanelSizeDelta = serializedObject.FindProperty("_dialoguePanelSizeDelta");
        _overrideDialoguePanelAutoHeight = serializedObject.FindProperty("_overrideDialoguePanelAutoHeight");
        _dialoguePanelAutoHeight = serializedObject.FindProperty("_dialoguePanelAutoHeight");
        _dialoguePanelAutoHeightPadding = serializedObject.FindProperty("_dialoguePanelAutoHeightPadding");
        _dialoguePanelAutoMinHeight = serializedObject.FindProperty("_dialoguePanelAutoMinHeight");
        _dialoguePanelAutoMaxHeight = serializedObject.FindProperty("_dialoguePanelAutoMaxHeight");
        _dialoguePanelAutoHeightKeepTop = serializedObject.FindProperty("_dialoguePanelAutoHeightKeepTop");
        _dialoguePanelAutoHeightGrowthUpFactor = serializedObject.FindProperty("_dialoguePanelAutoHeightGrowthUpFactor");
        _overrideDialoguePanelVerticalLayout = serializedObject.FindProperty("_overrideDialoguePanelVerticalLayout");
        _dialoguePanelVerticalLayoutPadding = serializedObject.FindProperty("_dialoguePanelVerticalLayoutPadding");
        _dialoguePanelVerticalLayoutSpacing = serializedObject.FindProperty("_dialoguePanelVerticalLayoutSpacing");
        _dialoguePanelVerticalLayoutChildAlignment = serializedObject.FindProperty("_dialoguePanelVerticalLayoutChildAlignment");
        _dialoguePanelVerticalLayoutReverseArrangement = serializedObject.FindProperty("_dialoguePanelVerticalLayoutReverseArrangement");
        _dialoguePanelVerticalLayoutControlChildWidth = serializedObject.FindProperty("_dialoguePanelVerticalLayoutControlChildWidth");
        _dialoguePanelVerticalLayoutControlChildHeight = serializedObject.FindProperty("_dialoguePanelVerticalLayoutControlChildHeight");
        _dialoguePanelVerticalLayoutUseChildScaleWidth = serializedObject.FindProperty("_dialoguePanelVerticalLayoutUseChildScaleWidth");
        _dialoguePanelVerticalLayoutUseChildScaleHeight = serializedObject.FindProperty("_dialoguePanelVerticalLayoutUseChildScaleHeight");
        _dialoguePanelVerticalLayoutChildForceExpandWidth = serializedObject.FindProperty("_dialoguePanelVerticalLayoutChildForceExpandWidth");
        _dialoguePanelVerticalLayoutChildForceExpandHeight = serializedObject.FindProperty("_dialoguePanelVerticalLayoutChildForceExpandHeight");
        _overrideDialoguePanelContentSizeFitter = serializedObject.FindProperty("_overrideDialoguePanelContentSizeFitter");
        _dialoguePanelContentSizeFitterHorizontalFit = serializedObject.FindProperty("_dialoguePanelContentSizeFitterHorizontalFit");
        _dialoguePanelContentSizeFitterVerticalFit = serializedObject.FindProperty("_dialoguePanelContentSizeFitterVerticalFit");
        _overrideBodyTextOffsetY = serializedObject.FindProperty("_overrideBodyTextOffsetY");
        _bodyTextOffsetY = serializedObject.FindProperty("_bodyTextOffsetY");
        _overrideBodyTextTopOffsetY = serializedObject.FindProperty("_overrideBodyTextTopOffsetY");
        _bodyTextTopOffsetY = serializedObject.FindProperty("_bodyTextTopOffsetY");
        _overrideBodyTextGrowDownOffsetX = serializedObject.FindProperty("_overrideBodyTextGrowDownOffsetX");
        _bodyTextGrowDownOffsetX = serializedObject.FindProperty("_bodyTextGrowDownOffsetX");
        _overrideBodyTextResizeHeightToPreferredText = serializedObject.FindProperty("_overrideBodyTextResizeHeightToPreferredText");
        _bodyTextResizeHeightToPreferredText = serializedObject.FindProperty("_bodyTextResizeHeightToPreferredText");
        _overrideBodyTextExtraHeight = serializedObject.FindProperty("_overrideBodyTextExtraHeight");
        _bodyTextExtraHeight = serializedObject.FindProperty("_bodyTextExtraHeight");
        _overrideBodyTextMinHeight = serializedObject.FindProperty("_overrideBodyTextMinHeight");
        _bodyTextMinHeight = serializedObject.FindProperty("_bodyTextMinHeight");
        _overrideBodyTextMaxHeight = serializedObject.FindProperty("_overrideBodyTextMaxHeight");
        _bodyTextMaxHeight = serializedObject.FindProperty("_bodyTextMaxHeight");
        _overrideBodyTextMaxFontSize = serializedObject.FindProperty("_overrideBodyTextMaxFontSize");
        _bodyTextMaxFontSize = serializedObject.FindProperty("_bodyTextMaxFontSize");
        _overrideBodyTextFont = serializedObject.FindProperty("_overrideBodyTextFont");
        _bodyTextFont = serializedObject.FindProperty("_bodyTextFont");
        _overrideBodyTextShrinkTextToFitRect = serializedObject.FindProperty("_overrideBodyTextShrinkTextToFitRect");
        _bodyTextShrinkTextToFitRect = serializedObject.FindProperty("_bodyTextShrinkTextToFitRect");
        _overrideBodyTextMinAutoFontSize = serializedObject.FindProperty("_overrideBodyTextMinAutoFontSize");
        _bodyTextMinAutoFontSize = serializedObject.FindProperty("_bodyTextMinAutoFontSize");
        _overrideBodyTextOverflowModeWhenStillTooLarge = serializedObject.FindProperty("_overrideBodyTextOverflowModeWhenStillTooLarge");
        _bodyTextOverflowModeWhenStillTooLarge = serializedObject.FindProperty("_bodyTextOverflowModeWhenStillTooLarge");
        _overrideBodyTextHorizontalClamp = serializedObject.FindProperty("_overrideBodyTextHorizontalClamp");
        _bodyTextHorizontalClamp = serializedObject.FindProperty("_bodyTextHorizontalClamp");
        _bodyTextHorizontalInset = serializedObject.FindProperty("_bodyTextHorizontalInset");
        _bodyTextMaxWidth = serializedObject.FindProperty("_bodyTextMaxWidth");
        _dialogueExtraLayers = serializedObject.FindProperty("_dialogueExtraLayers");
        _overrideCharacterNameOffset = serializedObject.FindProperty("_overrideCharacterNameOffset");
        _characterNameOffset = serializedObject.FindProperty("_characterNameOffset");
        _overrideCharacterNameFont = serializedObject.FindProperty("_overrideCharacterNameFont");
        _characterNameFont = serializedObject.FindProperty("_characterNameFont");
        _overrideCharacterNameFontSize = serializedObject.FindProperty("_overrideCharacterNameFontSize");
        _characterNameFontSize = serializedObject.FindProperty("_characterNameFontSize");
        _namePlateSprite = serializedObject.FindProperty("_namePlateSprite");
        _namePlateSpriteSource = serializedObject.FindProperty("_namePlateSpriteSource");
        _overrideNamePlateColor = serializedObject.FindProperty("_overrideNamePlateColor");
        _namePlateColor = serializedObject.FindProperty("_namePlateColor");
        _overrideNamePlateImageType = serializedObject.FindProperty("_overrideNamePlateImageType");
        _namePlateImageType = serializedObject.FindProperty("_namePlateImageType");
        _overrideNamePlatePreserveAspect = serializedObject.FindProperty("_overrideNamePlatePreserveAspect");
        _namePlatePreserveAspect = serializedObject.FindProperty("_namePlatePreserveAspect");
        _overrideNamePlatePixelsPerUnitMultiplier = serializedObject.FindProperty("_overrideNamePlatePixelsPerUnitMultiplier");
        _namePlatePixelsPerUnitMultiplier = serializedObject.FindProperty("_namePlatePixelsPerUnitMultiplier");
        _overrideNamePlateMaterial = serializedObject.FindProperty("_overrideNamePlateMaterial");
        _namePlateMaterial = serializedObject.FindProperty("_namePlateMaterial");
        _overrideNamePlateRaycastTarget = serializedObject.FindProperty("_overrideNamePlateRaycastTarget");
        _namePlateRaycastTarget = serializedObject.FindProperty("_namePlateRaycastTarget");
        _overrideNamePlateAnchors = serializedObject.FindProperty("_overrideNamePlateAnchors");
        _namePlateAnchorMin = serializedObject.FindProperty("_namePlateAnchorMin");
        _namePlateAnchorMax = serializedObject.FindProperty("_namePlateAnchorMax");
        _overrideNamePlatePivot = serializedObject.FindProperty("_overrideNamePlatePivot");
        _namePlatePivot = serializedObject.FindProperty("_namePlatePivot");
        _overrideNamePlateRect = serializedObject.FindProperty("_overrideNamePlateRect");
        _namePlateAnchoredPosition = serializedObject.FindProperty("_namePlateAnchoredPosition");
        _namePlateSizeDelta = serializedObject.FindProperty("_namePlateSizeDelta");

        _nameScreenBackgroundSprite = serializedObject.FindProperty("_nameScreenBackgroundSprite");
        _nameScreenBackgroundSpriteSource = serializedObject.FindProperty("_nameScreenBackgroundSpriteSource");
        _nameInputApplyOnlySprites = serializedObject.FindProperty("_nameInputApplyOnlySprites");
        _overrideNameScreenBackgroundColor = serializedObject.FindProperty("_overrideNameScreenBackgroundColor");
        _nameScreenBackgroundColor = serializedObject.FindProperty("_nameScreenBackgroundColor");
        _overrideNameScreenBackgroundImageType = serializedObject.FindProperty("_overrideNameScreenBackgroundImageType");
        _nameScreenBackgroundImageType = serializedObject.FindProperty("_nameScreenBackgroundImageType");
        _namePanelBackgroundSprite = serializedObject.FindProperty("_namePanelBackgroundSprite");
        _namePanelBackgroundSpriteSource = serializedObject.FindProperty("_namePanelBackgroundSpriteSource");
        _overrideNamePanelBackgroundColor = serializedObject.FindProperty("_overrideNamePanelBackgroundColor");
        _namePanelBackgroundColor = serializedObject.FindProperty("_namePanelBackgroundColor");
        _overrideNamePanelBackgroundImageType = serializedObject.FindProperty("_overrideNamePanelBackgroundImageType");
        _namePanelBackgroundImageType = serializedObject.FindProperty("_namePanelBackgroundImageType");
        _overrideNamePanelBackgroundRect = serializedObject.FindProperty("_overrideNamePanelBackgroundRect");
        _namePanelBackgroundAnchoredPosition = serializedObject.FindProperty("_namePanelBackgroundAnchoredPosition");
        _namePanelBackgroundSizeDelta = serializedObject.FindProperty("_namePanelBackgroundSizeDelta");
        _nameInputFieldSprite = serializedObject.FindProperty("_nameInputFieldSprite");
        _nameInputFieldSpriteSource = serializedObject.FindProperty("_nameInputFieldSpriteSource");
        _overrideNameInputFieldColor = serializedObject.FindProperty("_overrideNameInputFieldColor");
        _nameInputFieldColor = serializedObject.FindProperty("_nameInputFieldColor");
        _overrideNameInputFieldImageType = serializedObject.FindProperty("_overrideNameInputFieldImageType");
        _nameInputFieldImageType = serializedObject.FindProperty("_nameInputFieldImageType");
        _overrideNameInputFieldRect = serializedObject.FindProperty("_overrideNameInputFieldRect");
        _nameInputFieldAnchoredPosition = serializedObject.FindProperty("_nameInputFieldAnchoredPosition");
        _nameInputFieldSizeDelta = serializedObject.FindProperty("_nameInputFieldSizeDelta");
        _overrideNameInputTextRect = serializedObject.FindProperty("_overrideNameInputTextRect");
        _nameInputTextAnchoredPosition = serializedObject.FindProperty("_nameInputTextAnchoredPosition");
        _nameInputTextSizeDelta = serializedObject.FindProperty("_nameInputTextSizeDelta");
        _overrideNameInputTextColor = serializedObject.FindProperty("_overrideNameInputTextColor");
        _nameInputTextColor = serializedObject.FindProperty("_nameInputTextColor");
        _overrideNameInputTextFont = serializedObject.FindProperty("_overrideNameInputTextFont");
        _nameInputTextFont = serializedObject.FindProperty("_nameInputTextFont");
        _overrideNameInputTextFontSize = serializedObject.FindProperty("_overrideNameInputTextFontSize");
        _nameInputTextFontSize = serializedObject.FindProperty("_nameInputTextFontSize");
        _overrideNamePlaceholderTextRect = serializedObject.FindProperty("_overrideNamePlaceholderTextRect");
        _namePlaceholderTextAnchoredPosition = serializedObject.FindProperty("_namePlaceholderTextAnchoredPosition");
        _namePlaceholderTextSizeDelta = serializedObject.FindProperty("_namePlaceholderTextSizeDelta");
        _overrideNamePlaceholderTextColor = serializedObject.FindProperty("_overrideNamePlaceholderTextColor");
        _namePlaceholderTextColor = serializedObject.FindProperty("_namePlaceholderTextColor");
        _overrideNamePlaceholderTextFont = serializedObject.FindProperty("_overrideNamePlaceholderTextFont");
        _namePlaceholderTextFont = serializedObject.FindProperty("_namePlaceholderTextFont");
        _overrideNamePlaceholderTextFontSize = serializedObject.FindProperty("_overrideNamePlaceholderTextFontSize");
        _namePlaceholderTextFontSize = serializedObject.FindProperty("_namePlaceholderTextFontSize");
        _nameConfirmButtonPrefabOverride = serializedObject.FindProperty("_nameConfirmButtonPrefabOverride");
        _nameConfirmButtonSprite = serializedObject.FindProperty("_nameConfirmButtonSprite");
        _nameConfirmButtonSpriteSource = serializedObject.FindProperty("_nameConfirmButtonSpriteSource");
        _overrideNameConfirmButtonColor = serializedObject.FindProperty("_overrideNameConfirmButtonColor");
        _nameConfirmButtonColor = serializedObject.FindProperty("_nameConfirmButtonColor");
        _overrideNameConfirmButtonImageType = serializedObject.FindProperty("_overrideNameConfirmButtonImageType");
        _nameConfirmButtonImageType = serializedObject.FindProperty("_nameConfirmButtonImageType");
        _overrideNameConfirmButtonRect = serializedObject.FindProperty("_overrideNameConfirmButtonRect");
        _nameConfirmButtonAnchoredPosition = serializedObject.FindProperty("_nameConfirmButtonAnchoredPosition");
        _nameConfirmButtonSizeDelta = serializedObject.FindProperty("_nameConfirmButtonSizeDelta");
        _overrideNameConfirmButtonTextRect = serializedObject.FindProperty("_overrideNameConfirmButtonTextRect");
        _nameConfirmButtonTextAnchoredPosition = serializedObject.FindProperty("_nameConfirmButtonTextAnchoredPosition");
        _nameConfirmButtonTextSizeDelta = serializedObject.FindProperty("_nameConfirmButtonTextSizeDelta");
        _overrideNameConfirmButtonTextColor = serializedObject.FindProperty("_overrideNameConfirmButtonTextColor");
        _nameConfirmButtonTextColor = serializedObject.FindProperty("_nameConfirmButtonTextColor");
        _overrideNameConfirmButtonTextFont = serializedObject.FindProperty("_overrideNameConfirmButtonTextFont");
        _nameConfirmButtonTextFont = serializedObject.FindProperty("_nameConfirmButtonTextFont");
        _overrideNameConfirmButtonTextFontSize = serializedObject.FindProperty("_overrideNameConfirmButtonTextFontSize");
        _nameConfirmButtonTextFontSize = serializedObject.FindProperty("_nameConfirmButtonTextFontSize");
        _useNameExtraTextOne = serializedObject.FindProperty("_useNameExtraTextOne");
        _nameExtraTextOneText = serializedObject.FindProperty("_nameExtraTextOneText");
        _overrideNameExtraTextOneRect = serializedObject.FindProperty("_overrideNameExtraTextOneRect");
        _nameExtraTextOneAnchoredPosition = serializedObject.FindProperty("_nameExtraTextOneAnchoredPosition");
        _nameExtraTextOneSizeDelta = serializedObject.FindProperty("_nameExtraTextOneSizeDelta");
        _overrideNameExtraTextOneColor = serializedObject.FindProperty("_overrideNameExtraTextOneColor");
        _nameExtraTextOneColor = serializedObject.FindProperty("_nameExtraTextOneColor");
        _overrideNameExtraTextOneFont = serializedObject.FindProperty("_overrideNameExtraTextOneFont");
        _nameExtraTextOneFont = serializedObject.FindProperty("_nameExtraTextOneFont");
        _overrideNameExtraTextOneFontSize = serializedObject.FindProperty("_overrideNameExtraTextOneFontSize");
        _nameExtraTextOneFontSize = serializedObject.FindProperty("_nameExtraTextOneFontSize");
        _useNameExtraTextTwo = serializedObject.FindProperty("_useNameExtraTextTwo");
        _nameExtraTextTwoText = serializedObject.FindProperty("_nameExtraTextTwoText");
        _overrideNameExtraTextTwoRect = serializedObject.FindProperty("_overrideNameExtraTextTwoRect");
        _nameExtraTextTwoAnchoredPosition = serializedObject.FindProperty("_nameExtraTextTwoAnchoredPosition");
        _nameExtraTextTwoSizeDelta = serializedObject.FindProperty("_nameExtraTextTwoSizeDelta");
        _overrideNameExtraTextTwoColor = serializedObject.FindProperty("_overrideNameExtraTextTwoColor");
        _nameExtraTextTwoColor = serializedObject.FindProperty("_nameExtraTextTwoColor");
        _overrideNameExtraTextTwoFont = serializedObject.FindProperty("_overrideNameExtraTextTwoFont");
        _nameExtraTextTwoFont = serializedObject.FindProperty("_nameExtraTextTwoFont");
        _overrideNameExtraTextTwoFontSize = serializedObject.FindProperty("_overrideNameExtraTextTwoFontSize");
        _nameExtraTextTwoFontSize = serializedObject.FindProperty("_nameExtraTextTwoFontSize");
        _nameExtraTexts = serializedObject.FindProperty("_nameExtraTexts");

        _choiceButtonPrefabOverride = serializedObject.FindProperty("_choiceButtonPrefabOverride");
        _premiumChoiceButtonPrefabOverride = serializedObject.FindProperty("_premiumChoiceButtonPrefabOverride");
        _premiumChoiceBalancePanelPrefabOverride = serializedObject.FindProperty("_premiumChoiceBalancePanelPrefabOverride");
        _premiumChoiceBalancePanelOffset = serializedObject.FindProperty("_premiumChoiceBalancePanelOffset");
        _choicesApplyOnlySprites = serializedObject.FindProperty("_choicesApplyOnlySprites");
        _choiceButtonSprite = serializedObject.FindProperty("_choiceButtonSprite");
        _choiceButtonSpriteSource = serializedObject.FindProperty("_choiceButtonSpriteSource");
        _overrideChoiceButtonColor = serializedObject.FindProperty("_overrideChoiceButtonColor");
        _choiceButtonColor = serializedObject.FindProperty("_choiceButtonColor");
        _overrideChoiceButtonImageType = serializedObject.FindProperty("_overrideChoiceButtonImageType");
        _choiceButtonImageType = serializedObject.FindProperty("_choiceButtonImageType");
        _overrideChoiceButtonTextColor = serializedObject.FindProperty("_overrideChoiceButtonTextColor");
        _choiceButtonTextColor = serializedObject.FindProperty("_choiceButtonTextColor");
        _overrideChoiceButtonFont = serializedObject.FindProperty("_overrideChoiceButtonFont");
        _choiceButtonFont = serializedObject.FindProperty("_choiceButtonFont");
        _overrideChoiceButtonFontSize = serializedObject.FindProperty("_overrideChoiceButtonFontSize");
        _choiceButtonFontSize = serializedObject.FindProperty("_choiceButtonFontSize");
        _overrideChoiceButtonPadding = serializedObject.FindProperty("_overrideChoiceButtonPadding");
        _choiceButtonPadding = serializedObject.FindProperty("_choiceButtonPadding");
        _overrideChoiceButtonTextPadding = serializedObject.FindProperty("_overrideChoiceButtonTextPadding");
        _choiceButtonTextPadding = serializedObject.FindProperty("_choiceButtonTextPadding");
        _overrideChoiceButtonTextOffset = serializedObject.FindProperty("_overrideChoiceButtonTextOffset");
        _choiceButtonTextOffset = serializedObject.FindProperty("_choiceButtonTextOffset");
        _choicePanelSprite = serializedObject.FindProperty("_choicePanelSprite");
        _choicePanelSpriteSource = serializedObject.FindProperty("_choicePanelSpriteSource");
        _overrideChoicePanelColor = serializedObject.FindProperty("_overrideChoicePanelColor");
        _choicePanelColor = serializedObject.FindProperty("_choicePanelColor");
        _overrideChoicePanelImageType = serializedObject.FindProperty("_overrideChoicePanelImageType");
        _choicePanelImageType = serializedObject.FindProperty("_choicePanelImageType");

        _statPanelSprite = serializedObject.FindProperty("_statPanelSprite");
        _statPanelSpriteSource = serializedObject.FindProperty("_statPanelSpriteSource");
        _statsApplyOnlySprites = serializedObject.FindProperty("_statsApplyOnlySprites");
        _overrideStatPanelColor = serializedObject.FindProperty("_overrideStatPanelColor");
        _statPanelColor = serializedObject.FindProperty("_statPanelColor");
        _overrideStatPanelImageType = serializedObject.FindProperty("_overrideStatPanelImageType");
        _statPanelImageType = serializedObject.FindProperty("_statPanelImageType");
        _overrideStatPanelBackgroundAnchors = serializedObject.FindProperty("_overrideStatPanelBackgroundAnchors");
        _statPanelBackgroundAnchorMin = serializedObject.FindProperty("_statPanelBackgroundAnchorMin");
        _statPanelBackgroundAnchorMax = serializedObject.FindProperty("_statPanelBackgroundAnchorMax");
        _overrideStatPanelBackgroundPivot = serializedObject.FindProperty("_overrideStatPanelBackgroundPivot");
        _statPanelBackgroundPivot = serializedObject.FindProperty("_statPanelBackgroundPivot");
        _overrideStatPanelBackgroundStretchOffsets = serializedObject.FindProperty("_overrideStatPanelBackgroundStretchOffsets");
        _statPanelBackgroundStretchOffsets = serializedObject.FindProperty("_statPanelBackgroundStretchOffsets");
        _overrideStatTextColor = serializedObject.FindProperty("_overrideStatTextColor");
        _statTextColor = serializedObject.FindProperty("_statTextColor");
        _overrideStatTextFont = serializedObject.FindProperty("_overrideStatTextFont");
        _statTextFont = serializedObject.FindProperty("_statTextFont");
        _overrideStatTextFontSize = serializedObject.FindProperty("_overrideStatTextFontSize");
        _statTextFontSize = serializedObject.FindProperty("_statTextFontSize");
        _overrideStatPanelRect = serializedObject.FindProperty("_overrideStatPanelRect");
        _statPanelAnchoredPosition = serializedObject.FindProperty("_statPanelAnchoredPosition");
        _statPanelSizeDelta = serializedObject.FindProperty("_statPanelSizeDelta");
        _statPanelSizeOverrides = serializedObject.FindProperty("_statPanelSizeOverrides");
        _overrideStatTextRect = serializedObject.FindProperty("_overrideStatTextRect");
        _statTextAnchoredPosition = serializedObject.FindProperty("_statTextAnchoredPosition");
        _statTextSizeDelta = serializedObject.FindProperty("_statTextSizeDelta");
        _statTextRectOverrides = serializedObject.FindProperty("_statTextRectOverrides");
        _overrideStatTextAutoSize = serializedObject.FindProperty("_overrideStatTextAutoSize");
        _statTextAutoSize = serializedObject.FindProperty("_statTextAutoSize");
        _overrideStatTextAutoFontSizeRange = serializedObject.FindProperty("_overrideStatTextAutoFontSizeRange");
        _statTextMinAutoFontSize = serializedObject.FindProperty("_statTextMinAutoFontSize");
        _statTextMaxAutoFontSize = serializedObject.FindProperty("_statTextMaxAutoFontSize");
        _overrideStatTextAlignment = serializedObject.FindProperty("_overrideStatTextAlignment");
        _statTextAlignment = serializedObject.FindProperty("_statTextAlignment");
        _overrideStatTextWordWrapping = serializedObject.FindProperty("_overrideStatTextWordWrapping");
        _statTextWordWrapping = serializedObject.FindProperty("_statTextWordWrapping");
        _overrideStatTextOverflowMode = serializedObject.FindProperty("_overrideStatTextOverflowMode");
        _statTextOverflowMode = serializedObject.FindProperty("_statTextOverflowMode");
        _overrideStatTextLineSpacing = serializedObject.FindProperty("_overrideStatTextLineSpacing");
        _statTextLineSpacing = serializedObject.FindProperty("_statTextLineSpacing");
        _overrideStatTextMargins = serializedObject.FindProperty("_overrideStatTextMargins");
        _statTextMargins = serializedObject.FindProperty("_statTextMargins");
        _replaceStatDefinitions = serializedObject.FindProperty("_replaceStatDefinitions");
        _statOverlayDefinitions = serializedObject.FindProperty("_statOverlayDefinitions");
        _statDefinitionAssets = serializedObject.FindProperty("_statDefinitionAssets");
        _overrideStatPanelPadding = serializedObject.FindProperty("_overrideStatPanelPadding");
        _statPanelPadding = serializedObject.FindProperty("_statPanelPadding");
        _overrideStatIconSize = serializedObject.FindProperty("_overrideStatIconSize");
        _statIconSize = serializedObject.FindProperty("_statIconSize");
        _overrideStatIconOffset = serializedObject.FindProperty("_overrideStatIconOffset");
        _statIconOffset = serializedObject.FindProperty("_statIconOffset");
        _overrideStatIconVisualScale = serializedObject.FindProperty("_overrideStatIconVisualScale");
        _statIconVisualScale = serializedObject.FindProperty("_statIconVisualScale");
        _overrideStatIconMinSize = serializedObject.FindProperty("_overrideStatIconMinSize");
        _statIconMinSize = serializedObject.FindProperty("_statIconMinSize");
        _overrideStatIconReserveSpaceWhenHidden = serializedObject.FindProperty("_overrideStatIconReserveSpaceWhenHidden");
        _statIconReserveSpaceWhenHidden = serializedObject.FindProperty("_statIconReserveSpaceWhenHidden");
        _overrideStatIconParentSpacing = serializedObject.FindProperty("_overrideStatIconParentSpacing");
        _statIconParentSpacing = serializedObject.FindProperty("_statIconParentSpacing");
        _overrideStatIconParentPadding = serializedObject.FindProperty("_overrideStatIconParentPadding");
        _statIconParentPadding = serializedObject.FindProperty("_statIconParentPadding");
        _statIconOffsetOverrides = serializedObject.FindProperty("_statIconOffsetOverrides");
        _overrideStatPanelVerticalLayout = serializedObject.FindProperty("_overrideStatPanelVerticalLayout");
        _statPanelVerticalLayoutPadding = serializedObject.FindProperty("_statPanelVerticalLayoutPadding");
        _statPanelVerticalLayoutSpacing = serializedObject.FindProperty("_statPanelVerticalLayoutSpacing");
        _statPanelVerticalLayoutChildAlignment = serializedObject.FindProperty("_statPanelVerticalLayoutChildAlignment");
        _statPanelVerticalLayoutReverseArrangement = serializedObject.FindProperty("_statPanelVerticalLayoutReverseArrangement");
        _statPanelVerticalLayoutControlChildWidth = serializedObject.FindProperty("_statPanelVerticalLayoutControlChildWidth");
        _statPanelVerticalLayoutControlChildHeight = serializedObject.FindProperty("_statPanelVerticalLayoutControlChildHeight");
        _statPanelVerticalLayoutUseChildScaleWidth = serializedObject.FindProperty("_statPanelVerticalLayoutUseChildScaleWidth");
        _statPanelVerticalLayoutUseChildScaleHeight = serializedObject.FindProperty("_statPanelVerticalLayoutUseChildScaleHeight");
        _statPanelVerticalLayoutChildForceExpandWidth = serializedObject.FindProperty("_statPanelVerticalLayoutChildForceExpandWidth");
        _statPanelVerticalLayoutChildForceExpandHeight = serializedObject.FindProperty("_statPanelVerticalLayoutChildForceExpandHeight");
        _overrideStatPanelContentSizeFitter = serializedObject.FindProperty("_overrideStatPanelContentSizeFitter");
        _statPanelContentSizeFitterHorizontalFit = serializedObject.FindProperty("_statPanelContentSizeFitterHorizontalFit");
        _statPanelContentSizeFitterVerticalFit = serializedObject.FindProperty("_statPanelContentSizeFitterVerticalFit");
        _overrideRelationshipFrameSize = serializedObject.FindProperty("_overrideRelationshipFrameSize");
        _relationshipFrameAnchoredPosition = serializedObject.FindProperty("_relationshipFrameAnchoredPosition");
        _relationshipFrameSize = serializedObject.FindProperty("_relationshipFrameSize");
        _overrideRelationshipPanelBackgroundAnchors = serializedObject.FindProperty("_overrideRelationshipPanelBackgroundAnchors");
        _relationshipPanelBackgroundAnchorMin = serializedObject.FindProperty("_relationshipPanelBackgroundAnchorMin");
        _relationshipPanelBackgroundAnchorMax = serializedObject.FindProperty("_relationshipPanelBackgroundAnchorMax");
        _overrideRelationshipPanelBackgroundPivot = serializedObject.FindProperty("_overrideRelationshipPanelBackgroundPivot");
        _relationshipPanelBackgroundPivot = serializedObject.FindProperty("_relationshipPanelBackgroundPivot");
        _overrideRelationshipPanelBackgroundRect = serializedObject.FindProperty("_overrideRelationshipPanelBackgroundRect");
        _relationshipPanelBackgroundAnchoredPosition = serializedObject.FindProperty("_relationshipPanelBackgroundAnchoredPosition");
        _relationshipPanelBackgroundSizeDelta = serializedObject.FindProperty("_relationshipPanelBackgroundSizeDelta");
        _overrideRelationshipPanelBackgroundStretchOffsets = serializedObject.FindProperty("_overrideRelationshipPanelBackgroundStretchOffsets");
        _relationshipPanelBackgroundStretchOffsets = serializedObject.FindProperty("_relationshipPanelBackgroundStretchOffsets");
        _overrideRelationshipPanelVerticalLayout = serializedObject.FindProperty("_overrideRelationshipPanelVerticalLayout");
        _relationshipPanelVerticalLayoutPadding = serializedObject.FindProperty("_relationshipPanelVerticalLayoutPadding");
        _relationshipPanelVerticalLayoutSpacing = serializedObject.FindProperty("_relationshipPanelVerticalLayoutSpacing");
        _relationshipPanelVerticalLayoutChildAlignment = serializedObject.FindProperty("_relationshipPanelVerticalLayoutChildAlignment");
        _relationshipPanelVerticalLayoutReverseArrangement = serializedObject.FindProperty("_relationshipPanelVerticalLayoutReverseArrangement");
        _relationshipPanelVerticalLayoutControlChildWidth = serializedObject.FindProperty("_relationshipPanelVerticalLayoutControlChildWidth");
        _relationshipPanelVerticalLayoutControlChildHeight = serializedObject.FindProperty("_relationshipPanelVerticalLayoutControlChildHeight");
        _relationshipPanelVerticalLayoutUseChildScaleWidth = serializedObject.FindProperty("_relationshipPanelVerticalLayoutUseChildScaleWidth");
        _relationshipPanelVerticalLayoutUseChildScaleHeight = serializedObject.FindProperty("_relationshipPanelVerticalLayoutUseChildScaleHeight");
        _relationshipPanelVerticalLayoutChildForceExpandWidth = serializedObject.FindProperty("_relationshipPanelVerticalLayoutChildForceExpandWidth");
        _relationshipPanelVerticalLayoutChildForceExpandHeight = serializedObject.FindProperty("_relationshipPanelVerticalLayoutChildForceExpandHeight");
        _overrideRelationshipPanelContentSizeFitter = serializedObject.FindProperty("_overrideRelationshipPanelContentSizeFitter");
        _relationshipPanelContentSizeFitterHorizontalFit = serializedObject.FindProperty("_relationshipPanelContentSizeFitterHorizontalFit");
        _relationshipPanelContentSizeFitterVerticalFit = serializedObject.FindProperty("_relationshipPanelContentSizeFitterVerticalFit");
        _overrideRelationshipFontSizeRange = serializedObject.FindProperty("_overrideRelationshipFontSizeRange");
        _relationshipFontSizeMin = serializedObject.FindProperty("_relationshipFontSizeMin");
        _relationshipFontSizeMax = serializedObject.FindProperty("_relationshipFontSizeMax");
        _overrideRelationshipMaxVisibleLines = serializedObject.FindProperty("_overrideRelationshipMaxVisibleLines");
        _relationshipMaxVisibleLines = serializedObject.FindProperty("_relationshipMaxVisibleLines");
        _relationshipMessageOverrides = serializedObject.FindProperty("_relationshipMessageOverrides");

        _chapterTitlePanelSprite = serializedObject.FindProperty("_chapterTitlePanelSprite");
        _chapterTitlePanelSpriteSource = serializedObject.FindProperty("_chapterTitlePanelSpriteSource");
        _chapterApplyOnlySprites = serializedObject.FindProperty("_chapterApplyOnlySprites");
        _overrideChapterTitlePanelColor = serializedObject.FindProperty("_overrideChapterTitlePanelColor");
        _chapterTitlePanelColor = serializedObject.FindProperty("_chapterTitlePanelColor");
        _overrideChapterTitlePanelImageType = serializedObject.FindProperty("_overrideChapterTitlePanelImageType");
        _chapterTitlePanelImageType = serializedObject.FindProperty("_chapterTitlePanelImageType");
        _overrideChapterTitleTextColor = serializedObject.FindProperty("_overrideChapterTitleTextColor");
        _chapterTitleTextColor = serializedObject.FindProperty("_chapterTitleTextColor");
        _overrideChapterTitleTextFont = serializedObject.FindProperty("_overrideChapterTitleTextFont");
        _chapterTitleTextFont = serializedObject.FindProperty("_chapterTitleTextFont");
        _overrideChapterTitleTextFontSize = serializedObject.FindProperty("_overrideChapterTitleTextFontSize");
        _chapterTitleTextFontSize = serializedObject.FindProperty("_chapterTitleTextFontSize");
        _overrideChapterTitleTextRect = serializedObject.FindProperty("_overrideChapterTitleTextRect");
        _chapterTitleTextAnchoredPosition = serializedObject.FindProperty("_chapterTitleTextAnchoredPosition");
        _chapterTitleTextSizeDelta = serializedObject.FindProperty("_chapterTitleTextSizeDelta");
        _overrideChapterTitleTextHeightLimits = serializedObject.FindProperty("_overrideChapterTitleTextHeightLimits");
        _chapterTitleTextMinHeight = serializedObject.FindProperty("_chapterTitleTextMinHeight");
        _chapterTitleTextMaxHeight = serializedObject.FindProperty("_chapterTitleTextMaxHeight");
        _overrideChapterTitleTextAutoSize = serializedObject.FindProperty("_overrideChapterTitleTextAutoSize");
        _chapterTitleTextAutoSize = serializedObject.FindProperty("_chapterTitleTextAutoSize");
        _overrideChapterTitleTextAutoFontSizeRange = serializedObject.FindProperty("_overrideChapterTitleTextAutoFontSizeRange");
        _chapterTitleTextMinAutoFontSize = serializedObject.FindProperty("_chapterTitleTextMinAutoFontSize");
        _chapterTitleTextMaxAutoFontSize = serializedObject.FindProperty("_chapterTitleTextMaxAutoFontSize");
        _overrideChapterTitleTextAlignment = serializedObject.FindProperty("_overrideChapterTitleTextAlignment");
        _chapterTitleTextAlignment = serializedObject.FindProperty("_chapterTitleTextAlignment");
        _overrideChapterTitleTextWordWrapping = serializedObject.FindProperty("_overrideChapterTitleTextWordWrapping");
        _chapterTitleTextWordWrapping = serializedObject.FindProperty("_chapterTitleTextWordWrapping");
        _overrideChapterTitleTextOverflowMode = serializedObject.FindProperty("_overrideChapterTitleTextOverflowMode");
        _chapterTitleTextOverflowMode = serializedObject.FindProperty("_chapterTitleTextOverflowMode");
        _overrideChapterTitleTextLineSpacing = serializedObject.FindProperty("_overrideChapterTitleTextLineSpacing");
        _chapterTitleTextLineSpacing = serializedObject.FindProperty("_chapterTitleTextLineSpacing");
        _overrideChapterTitleTextMargins = serializedObject.FindProperty("_overrideChapterTitleTextMargins");
        _chapterTitleTextMargins = serializedObject.FindProperty("_chapterTitleTextMargins");
        _overrideChapterTitleCenterOnShow = serializedObject.FindProperty("_overrideChapterTitleCenterOnShow");
        _chapterTitleCenterOnShow = serializedObject.FindProperty("_chapterTitleCenterOnShow");
        _overrideChapterTitleBringToFrontOnShow = serializedObject.FindProperty("_overrideChapterTitleBringToFrontOnShow");
        _chapterTitleBringToFrontOnShow = serializedObject.FindProperty("_chapterTitleBringToFrontOnShow");
        _overrideChapterTitleBackgroundDimSizeMode = serializedObject.FindProperty("_overrideChapterTitleBackgroundDimSizeMode");
        _chapterTitleBackgroundDimSizeMode = serializedObject.FindProperty("_chapterTitleBackgroundDimSizeMode");
        _overrideChapterTitleBackgroundDimFixedSize = serializedObject.FindProperty("_overrideChapterTitleBackgroundDimFixedSize");
        _chapterTitleBackgroundDimFixedSize = serializedObject.FindProperty("_chapterTitleBackgroundDimFixedSize");
        _overrideChapterTitleBackgroundDimColor = serializedObject.FindProperty("_overrideChapterTitleBackgroundDimColor");
        _chapterTitleBackgroundDimColor = serializedObject.FindProperty("_chapterTitleBackgroundDimColor");
        _overrideChapterTitleBackgroundDimAlpha = serializedObject.FindProperty("_overrideChapterTitleBackgroundDimAlpha");
        _chapterTitleBackgroundDimAlpha = serializedObject.FindProperty("_chapterTitleBackgroundDimAlpha");
        _overrideChapterTitleTextMode = serializedObject.FindProperty("_overrideChapterTitleTextMode");
        _chapterTitleTextMode = serializedObject.FindProperty("_chapterTitleTextMode");
        _overrideChapterTitleTextFormat = serializedObject.FindProperty("_overrideChapterTitleTextFormat");
        _chapterTitleTextFormat = serializedObject.FindProperty("_chapterTitleTextFormat");
        _overrideChapterTitleNumberAndTitleFormat = serializedObject.FindProperty("_overrideChapterTitleNumberAndTitleFormat");
        _chapterTitleNumberAndTitleFormat = serializedObject.FindProperty("_chapterTitleNumberAndTitleFormat");
        _overrideChapterTitleNumberOffset = serializedObject.FindProperty("_overrideChapterTitleNumberOffset");
        _chapterTitleNumberOffset = serializedObject.FindProperty("_chapterTitleNumberOffset");
        _overrideChapterTitleEmptyTitleFallback = serializedObject.FindProperty("_overrideChapterTitleEmptyTitleFallback");
        _chapterTitleEmptyTitleFallback = serializedObject.FindProperty("_chapterTitleEmptyTitleFallback");
        _overrideChapterTitleTrimTitle = serializedObject.FindProperty("_overrideChapterTitleTrimTitle");
        _chapterTitleTrimTitle = serializedObject.FindProperty("_chapterTitleTrimTitle");
        _overrideChapterTitleUppercaseTitle = serializedObject.FindProperty("_overrideChapterTitleUppercaseTitle");
        _chapterTitleUppercaseTitle = serializedObject.FindProperty("_chapterTitleUppercaseTitle");
        _overrideChapterTitleSpecificPaddingSettings = serializedObject.FindProperty("_overrideChapterTitleSpecificPaddingSettings");
        _chapterTitleUseSpecificPadding = serializedObject.FindProperty("_chapterTitleUseSpecificPadding");
        _chapterTitleSpecificPaddingMarkers = serializedObject.FindProperty("_chapterTitleSpecificPaddingMarkers");
        _chapterTitleSpecificPadding = serializedObject.FindProperty("_chapterTitleSpecificPadding");
        _overrideChapterTitleAnimationMode = serializedObject.FindProperty("_overrideChapterTitleAnimationMode");
        _chapterTitleAnimationMode = serializedObject.FindProperty("_chapterTitleAnimationMode");
        _overrideChapterTitleShownPosition = serializedObject.FindProperty("_overrideChapterTitleShownPosition");
        _chapterTitleShownPosition = serializedObject.FindProperty("_chapterTitleShownPosition");
        _overrideChapterTitleCaptureShownPositionOnAwake = serializedObject.FindProperty("_overrideChapterTitleCaptureShownPositionOnAwake");
        _chapterTitleCaptureShownPositionOnAwake = serializedObject.FindProperty("_chapterTitleCaptureShownPositionOnAwake");
        _overrideChapterTitleHiddenOffsetY = serializedObject.FindProperty("_overrideChapterTitleHiddenOffsetY");
        _chapterTitleHiddenOffsetY = serializedObject.FindProperty("_chapterTitleHiddenOffsetY");
        _overrideChapterTitleEnterDuration = serializedObject.FindProperty("_overrideChapterTitleEnterDuration");
        _chapterTitleEnterDuration = serializedObject.FindProperty("_chapterTitleEnterDuration");
        _overrideChapterTitleVisibleDuration = serializedObject.FindProperty("_overrideChapterTitleVisibleDuration");
        _chapterTitleVisibleDuration = serializedObject.FindProperty("_chapterTitleVisibleDuration");
        _overrideChapterTitleExitDuration = serializedObject.FindProperty("_overrideChapterTitleExitDuration");
        _chapterTitleExitDuration = serializedObject.FindProperty("_chapterTitleExitDuration");
        _overrideChapterTitleFadeWithMovement = serializedObject.FindProperty("_overrideChapterTitleFadeWithMovement");
        _chapterTitleFadeWithMovement = serializedObject.FindProperty("_chapterTitleFadeWithMovement");
        _overrideChapterTitleAnimatePosition = serializedObject.FindProperty("_overrideChapterTitleAnimatePosition");
        _chapterTitleAnimatePosition = serializedObject.FindProperty("_chapterTitleAnimatePosition");
        _overrideChapterTitleUseUnscaledTime = serializedObject.FindProperty("_overrideChapterTitleUseUnscaledTime");
        _chapterTitleUseUnscaledTime = serializedObject.FindProperty("_chapterTitleUseUnscaledTime");
        _overrideChapterTitleDisableRootAfterExit = serializedObject.FindProperty("_overrideChapterTitleDisableRootAfterExit");
        _chapterTitleDisableRootAfterExit = serializedObject.FindProperty("_chapterTitleDisableRootAfterExit");

        _overrideColor = serializedObject.FindProperty("_overrideColor");
        _color = serializedObject.FindProperty("_color");
        _overrideImageType = serializedObject.FindProperty("_overrideImageType");
        _imageType = serializedObject.FindProperty("_imageType");
        _overridePreserveAspect = serializedObject.FindProperty("_overridePreserveAspect");
        _preserveAspect = serializedObject.FindProperty("_preserveAspect");
        _overridePixelsPerUnitMultiplier = serializedObject.FindProperty("_overridePixelsPerUnitMultiplier");
        _pixelsPerUnitMultiplier = serializedObject.FindProperty("_pixelsPerUnitMultiplier");
        _overrideMaterial = serializedObject.FindProperty("_overrideMaterial");
        _material = serializedObject.FindProperty("_material");
        _overrideRaycastTarget = serializedObject.FindProperty("_overrideRaycastTarget");
        _raycastTarget = serializedObject.FindProperty("_raycastTarget");
    }

    public override void OnInspectorGUI()
    {
        bool changed = false;
        bool sceneBindingsChanged = false;

        try
        {
            serializedObject.Update();

            DrawHeaderTools();
            DrawInspectorOptions();
            DrawTabBar();
            DrawActiveSection();
            sceneBindingsChanged = _sceneBindingsChanged;
            _sceneBindingsChanged = false;

            changed = serializedObject.ApplyModifiedProperties();
        }
        catch (Exception ex)
        {
            AppLogger.Error(
                AppLogCategory.Editor,
                nameof(StoryUiStyleEditor),
                nameof(OnInspectorGUI),
                "Story UI Style inspector GUI failed but recovered safely.",
                ex,
                LogMetadata.Of("target", target != null ? target.name : ""));

            EditorGUILayout.HelpBox(
                "Инспектор Story UI временно не смог отрисовать один из разделов. Ошибка записана в editor/errors log; остальные данные не сброшены.",
                MessageType.Error);

            try
            {
                changed = serializedObject.ApplyModifiedProperties();
            }
            catch (Exception applyException)
            {
                AppLogger.Error(
                    AppLogCategory.Editor,
                    nameof(StoryUiStyleEditor),
                    nameof(OnInspectorGUI),
                    "Story UI Style inspector failed while applying modified properties after GUI recovery.",
                    applyException,
                    LogMetadata.Of("target", target != null ? target.name : ""));
            }
        }

        if (changed || sceneBindingsChanged)
        {
            if (_applyToSceneAutomatically)
                QueueApplyTargetsToOpenScene();
            Repaint();
        }
    }

    void OnDisable()
    {
        if (_phoneApplyQueued)
        {
            EditorApplication.update -= ApplyQueuedPhoneConfiguration;
            _phoneApplyQueued = false;
        }

        if (_applyTargetsQueued)
        {
            EditorApplication.update -= ApplyQueuedTargetsToOpenScene;
            _applyTargetsQueued = false;
        }
    }

    void QueueApplyTargetsToOpenScene()
    {
        if (!_applyToSceneAutomatically)
            return;

        _queuedApplyAt = EditorApplication.timeSinceStartup + SceneAutoApplyDelaySeconds;
        if (_applyTargetsQueued)
            return;

        _applyTargetsQueued = true;
        EditorApplication.update += ApplyQueuedTargetsToOpenScene;
    }

    void ApplyQueuedTargetsToOpenScene()
    {
        if (EditorApplication.timeSinceStartup < _queuedApplyAt)
            return;

        EditorApplication.update -= ApplyQueuedTargetsToOpenScene;
        _applyTargetsQueued = false;

        if (this == null || target == null)
            return;

        serializedObject.UpdateIfRequiredOrScript();
        ApplyTargetsToOpenScene();
        Repaint();
    }

    void QueuePhoneConfigurationApply(
        StoryUserInterface storyUserInterface,
        PhoneDialogueUI phoneUi,
        bool recalculateLayout)
    {
        if (storyUserInterface == null)
            return;

        _queuedPhoneOwner = storyUserInterface;
        _queuedPhoneUi = phoneUi;
        _queuedPhoneRecalculateLayout |= recalculateLayout;
        _queuedPhoneApplyAt = EditorApplication.timeSinceStartup + PhoneInlineApplyDelaySeconds;
        InvalidatePhoneValidationCache();

        if (_phoneApplyQueued)
            return;

        _phoneApplyQueued = true;
        EditorApplication.update += ApplyQueuedPhoneConfiguration;
    }

    void ApplyQueuedPhoneConfiguration()
    {
        if (EditorApplication.timeSinceStartup < _queuedPhoneApplyAt)
            return;

        EditorApplication.update -= ApplyQueuedPhoneConfiguration;
        _phoneApplyQueued = false;

        StoryUserInterface storyUserInterface = _queuedPhoneOwner;
        PhoneDialogueUI phoneUi = _queuedPhoneUi;
        bool recalculateLayout = _queuedPhoneRecalculateLayout;
        _queuedPhoneOwner = null;
        _queuedPhoneUi = null;
        _queuedPhoneRecalculateLayout = false;

        if (storyUserInterface == null)
            return;

        storyUserInterface.ApplyPhoneConfiguration("StoryUiStyleEditorQueuedChanged");
        if (recalculateLayout)
        {
            if (phoneUi == null)
                phoneUi = storyUserInterface.ResolvePhoneDialogueUI();
            if (phoneUi != null)
                phoneUi.RecalculateLayout("StoryUiStyleEditorQueuedChanged");
        }

        InvalidatePhoneValidationCache();
        Repaint();
    }

    void InvalidatePhoneValidationCache()
    {
        _phoneValidationDirty = true;
    }

    PhonePreviewValidationResult GetCachedPhoneValidation(StoryUserInterface storyUserInterface)
    {
        if (storyUserInterface == null)
            return null;

        if (!_phoneValidationDirty &&
            _cachedPhoneValidationOwner == storyUserInterface &&
            _cachedPhoneValidation != null)
        {
            return _cachedPhoneValidation;
        }

        _cachedPhoneValidationOwner = storyUserInterface;
        _cachedPhoneValidation = storyUserInterface.ValidatePhoneReferences(null, false);
        _phoneValidationDirty = false;
        return _cachedPhoneValidation;
    }

    void DrawInspectorOptions()
    {
        EditorGUILayout.Space(3f);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Вид инспектора", GUILayout.Width(96f));
            _showDisabledOverrideValues = GUILayout.Toggle(
                _showDisabledOverrideValues,
                "выключенные поля",
                EditorStyles.toolbarButton);
            _showHints = GUILayout.Toggle(
                _showHints,
                "подсказки",
                EditorStyles.toolbarButton);
            _applyToSceneAutomatically = GUILayout.Toggle(
                _applyToSceneAutomatically,
                "автоприменение",
                EditorStyles.toolbarButton,
                GUILayout.Width(118f));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Применить", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                ApplyTargetsToOpenScene();
        }
    }

    void DrawTabBar()
    {
        EditorGUILayout.Space(6f);
        _activeTab = (InspectorTab)GUILayout.Toolbar((int)_activeTab, TabLabels, GUILayout.MinHeight(26f));
    }

    void DrawActiveSection()
    {
        switch (_activeTab)
        {
            case InspectorTab.Dialogue:
                _showDialogue = true;
                DrawDialogueSection();
                break;
            case InspectorTab.NameInput:
                _showNameInput = true;
                DrawNameInputSection();
                break;
            case InspectorTab.Choices:
                _showChoices = true;
                DrawChoicesSection();
                break;
            case InspectorTab.Stats:
                _showStats = true;
                DrawStatsSection();
                break;
            case InspectorTab.Chapter:
                _showChapter = true;
                DrawChapterSection();
                break;
            case InspectorTab.EndScreen:
                DrawEndScreenSection();
                break;
            case InspectorTab.Phone:
                DrawPhoneSection();
                break;
            case InspectorTab.Advanced:
                _showAdvanced = true;
                DrawAdvancedSection();
                break;
        }
    }

    void DrawHeaderTools()
    {
        EditorGUILayout.LabelField("Story UI Style", EditorStyles.boldLabel);
        if (_showHints)
        {
            EditorGUILayout.HelpBox(
                "Один style на историю: диалог, имя, выборы, статы и глава. Настройки разнесены по вкладкам ниже; изменения автоматически применяются к открытой сцене, если включено автоприменение.",
                MessageType.Info);
        }

        if (!serializedObject.isEditingMultipleObjects)
            DrawStyleBindingStatus();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Открыть Story UI Catalog"))
                StoryInterfaceStyleCatalogEditor.SelectDefaultCatalog();

            if (GUILayout.Button("Preview", GUILayout.Width(105f)))
                OpenPreviewForCurrentStyle();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Автоназначить ссылки UI в сцене"))
            {
                AutoBindSceneUiReferences(true);
                ApplyTargetsToOpenScene();
            }

            _showSceneBindings = GUILayout.Toggle(_showSceneBindings, "Ссылки сцены", EditorStyles.miniButton, GUILayout.Width(135f));
        }

        if (_showSceneBindings)
            DrawSceneBindingsPanel();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sprite диалога из сцены"))
                CopyDialogueSpriteFromScene();

            if (GUILayout.Button("Rect плашки из сцены"))
                CopyDialoguePanelRectFromScene();

            if (GUILayout.Button("NamePlate из сцены"))
                CopyNamePlateFromScene();

            if (GUILayout.Button("BodyText Top из сцены"))
                CopyBodyTextTopOffsetFromScene();

            if (GUILayout.Button("Layout статов из сцены"))
                CopyStatLayoutFromScene();
        }
    }

    void DrawStyleBindingStatus()
    {
        StoryUiStyle style = target as StoryUiStyle;
        if (style == null)
            return;

        if (TryFindStyleContext(style, out StyleContext context))
        {
            string storyName = context.Story != null ? context.Story.name : "без StoryData";
            EditorGUILayout.HelpBox($"Story ID: {context.StoryId} ({storyName})", MessageType.None);
            return;
        }

        EditorGUILayout.HelpBox(
            "Не подключено к Story UI Catalog.",
            MessageType.Warning);
    }

    void DrawSceneBindingsPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Ссылки сцены", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Это реальные UI-объекты открытой сцены, к которым применяется style. Если ссылка пустая или не та, настройки визуально не сработают.", MessageType.Info);

            DialogueUIManager dialogue = FindSceneDialogueManager();
            DrawSceneComponentHeader("Диалог / выборы", dialogue);
            if (dialogue != null)
            {
                DrawSceneReference(dialogue, "dialoguePanel", "Плашка диалога");
                DrawSceneReference(dialogue, "dialogueBackgroundImage", "Image диалога");
                DrawSceneReference(dialogue, "dialogueExtraBackgroundImages", "Доп. Image-слои диалога");
                DrawSceneReference(dialogue, "nameText", "Имя персонажа TMP");
                DrawSceneReference(dialogue, "namePlateObject", "NamePlate / плашка имени");
                DrawSceneReference(dialogue, "namePlateImage", "NamePlate Image");
                DrawSceneReference(dialogue, "hideNamePlateWhenSpeakerMissing", "Скрывать плашку без имени");
                DrawSceneReference(dialogue, "dialogueText", "BodyText TMP");
                DrawSceneReference(dialogue, "choiceContainer", "Контейнер выборов");
                DrawSceneReference(dialogue, "choiceLayout", "DialogueChoiceLayout");
                DrawSceneReference(dialogue, "choiceButtonPrefab", "Префаб кнопки выбора");
                DrawSceneReference(dialogue, "premiumChoiceButtonPrefab", "Префаб платной кнопки");
            }

            PreStorySetupFlow setup = FindSceneSetupFlow();
            DrawSceneComponentHeader("Экран имени", setup);
            if (setup != null)
            {
                DrawSceneReference(setup, "_nameScreenBackgroundImage", "Фон экрана имени Image");
                DrawSceneReference(setup, "_namePanel", "Панель имени");
                DrawSceneReference(setup, "_namePanelBackgroundImage", "Фон имени Image");
                DrawSceneReference(setup, "_nameInputField", "TMP InputField");
                DrawSceneReference(setup, "_namePlaceholderText", "Placeholder TMP");
                DrawSceneReference(setup, "_nameConfirmButton", "Кнопка продолжения");
                DrawSceneReference(setup, "_nameConfirmButtonLabel", "Текст кнопки продолжения");
                DrawSceneReference(setup, "_nameConfirmButtonPrefabParent", "Родитель prefab кнопки продолжения");
                DrawSceneReference(setup, "_nameExtraTextOne", "Доп. текст 1");
                DrawSceneReference(setup, "_nameExtraTextTwo", "Доп. текст 2");
            }

            StatChangeOverlay stat = FindSceneStatOverlay();
            DrawSceneComponentHeader("Статы", stat);
            if (stat != null)
            {
                DrawSceneReference(stat, "_panelRect", "Плашка статов Rect");
                DrawSceneReference(stat, "_messageText", "Текст статов TMP");
                DrawSceneReference(stat, "_iconImage", "Иконка статов Image");
                DrawSceneReference(stat, "_canvasGroup", "CanvasGroup");
                DrawSceneReference(stat, "_rootObject", "Root object");
            }

            ChapterTitleOverlay chapter = FindSceneChapterOverlay();
            DrawSceneComponentHeader("Глава", chapter);
            if (chapter != null)
            {
                DrawSceneReference(chapter, "_panelRect", "Плашка главы Rect");
                DrawSceneReference(chapter, "_titleText", "Текст главы TMP");
                DrawSceneReference(chapter, "_canvasGroup", "CanvasGroup");
                DrawSceneReference(chapter, "_rootObject", "Root object");
                DrawSceneReference(chapter, "_backgroundDimCanvasGroup", "Затемнение CanvasGroup");
                DrawSceneReference(chapter, "_backgroundDimImage", "Затемнение Image");
            }
        }
    }

    void DrawSceneReference(UnityEngine.Object owner, string propertyName, string label)
    {
        if (owner == null)
            return;

        var sceneObject = new SerializedObject(owner);
        SerializedProperty property = sceneObject.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox($"{label}: поле {propertyName} не найдено.", MessageType.Warning);
            return;
        }

        sceneObject.Update();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(owner, "Assign Story UI Scene Reference");
            sceneObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(owner);
            _sceneBindingsChanged = true;
        }
    }

    static void DrawSceneComponentHeader(string label, UnityEngine.Object component)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.ObjectField(label, component, component != null ? component.GetType() : typeof(UnityEngine.Object), true);
            if (component == null)
                GUILayout.Label("не найдено", EditorStyles.miniLabel, GUILayout.Width(75f));
        }
    }

    void DrawDialogueSection()
    {
        DrawApplyOnlySpritesToggle(_dialogueApplyOnlySprites, "диалога");
        if (!DrawFoldout(ref _showDialogue, "Диалоговая плашка"))
            return;

        DrawSpriteColumnHeader();
        DrawSpriteRow("Sprite плашки диалога", _backgroundSprite, _backgroundSpriteSource, CopyDialogueSpriteFromScene, _overrideColor, _color, _overrideImageType, _imageType, Image.Type.Sliced);
        DrawDialogueBackgroundRectControls();
        DrawDialogueExtraLayersControls();
        DrawVector2Pair(
            _overrideDialoguePanelRect,
            _dialoguePanelAnchoredPosition,
            _dialoguePanelSizeDelta,
            "DialoguePanel Rect",
            "Позиция",
            "Размер");
        DrawDialoguePanelLayoutControls();
        DrawDialoguePanelAutoHeightControls();
        DrawBodyTextHorizontalClampControls();
        DrawOverridePair(_overrideBodyTextOffsetY, _bodyTextOffsetY, "BodyText Y", "Y");
        DrawOverridePair(_overrideBodyTextTopOffsetY, _bodyTextTopOffsetY, "BodyText Top Y", "Top Y");
        DrawBodyTextGrowDownControls();
        DrawOverridePair(_overrideCharacterNameOffset, _characterNameOffset, "CharacterName X/Y", "Offset");
        DrawFontOverridePair(_overrideCharacterNameFont, _characterNameFont, "CharacterName font", "Font");
        DrawOverridePair(_overrideCharacterNameFontSize, _characterNameFontSize, "CharacterName font size", "Size");
        DrawNamePlateControls();
        if (_showHints)
        {
            EditorGUILayout.HelpBox(
                "DialoguePanel Rect двигает и масштабирует саму плашку. BodyText Y двигает сам текст. BodyText Top Y двигает верхнюю границу StoryTextLayoutLock. CharacterName X/Y двигает имя персонажа только для этой истории. NamePlate Rect отдельно двигает и масштабирует фон имени.",
                MessageType.None);
        }

        if (GUILayout.Button("Скопировать Rect плашки из сцены"))
            CopyDialoguePanelRectFromScene();
    }

    void DrawDialogueBackgroundRectControls()
    {
        EditorGUILayout.Space(4f);
        DrawGroupTitle("Dialogue Background Rect", "Anchors are separate so stretch can be copied from scene or left untouched.");
        EditorGUI.BeginChangeCheck();
        DrawVector2Pair(
            _overrideDialogueBackgroundAnchors,
            _dialogueBackgroundAnchorMin,
            _dialogueBackgroundAnchorMax,
            "Background anchors / stretch",
            "Anchor Min",
            "Anchor Max");
        DrawOverridePair(_overrideDialogueBackgroundPivot, _dialogueBackgroundPivot, "Background pivot", "Pivot");
        DrawVector2Pair(
            _overrideDialogueBackgroundRect,
            _dialogueBackgroundAnchoredPosition,
            _dialogueBackgroundSizeDelta,
            "Background rect / offsets",
            "Position / Left-Bottom",
            "Size / Right-Top");
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            MarkTargetsDirty();
            ApplyTargetsToOpenScene();
            serializedObject.Update();
        }

        if (GUILayout.Button("Copy Background Rect from scene"))
            CopyDialogueBackgroundRectFromScene();
    }

    void DrawDialoguePanelLayoutControls()
    {
        EditorGUILayout.Space(4f);
        DrawGroupTitle("DialoguePanel: VerticalLayoutGroup");

        if (_overrideDialoguePanelVerticalLayout != null)
        {
            EditorGUILayout.PropertyField(_overrideDialoguePanelVerticalLayout, new GUIContent("Override VerticalLayoutGroup"));
            if (_overrideDialoguePanelVerticalLayout.boolValue || _showDisabledOverrideValues)
            {
                using (new EditorGUI.DisabledScope(!_overrideDialoguePanelVerticalLayout.boolValue))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_dialoguePanelVerticalLayoutPadding, new GUIContent("Padding"), true);
                    EditorGUILayout.PropertyField(_dialoguePanelVerticalLayoutSpacing, new GUIContent("Spacing"));
                    EditorGUILayout.PropertyField(_dialoguePanelVerticalLayoutChildAlignment, new GUIContent("Child Alignment"));
                    EditorGUILayout.PropertyField(_dialoguePanelVerticalLayoutReverseArrangement, new GUIContent("Reverse Arrangement"));
                    DrawBoolPropertyPair(
                        _dialoguePanelVerticalLayoutControlChildWidth,
                        "Control Width",
                        _dialoguePanelVerticalLayoutControlChildHeight,
                        "Control Height");
                    DrawBoolPropertyPair(
                        _dialoguePanelVerticalLayoutUseChildScaleWidth,
                        "Use Scale Width",
                        _dialoguePanelVerticalLayoutUseChildScaleHeight,
                        "Use Scale Height");
                    DrawBoolPropertyPair(
                        _dialoguePanelVerticalLayoutChildForceExpandWidth,
                        "Force Width",
                        _dialoguePanelVerticalLayoutChildForceExpandHeight,
                        "Force Height");
                    EditorGUI.indentLevel--;
                }
            }
        }

        EditorGUILayout.Space(3f);
        DrawGroupTitle("DialoguePanel: ContentSizeFitter");

        if (_overrideDialoguePanelContentSizeFitter != null)
        {
            EditorGUILayout.PropertyField(_overrideDialoguePanelContentSizeFitter, new GUIContent("Override ContentSizeFitter"));
            if (_overrideDialoguePanelContentSizeFitter.boolValue || _showDisabledOverrideValues)
            {
                using (new EditorGUI.DisabledScope(!_overrideDialoguePanelContentSizeFitter.boolValue))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_dialoguePanelContentSizeFitterHorizontalFit, new GUIContent("Horizontal Fit"));
                    EditorGUILayout.PropertyField(_dialoguePanelContentSizeFitterVerticalFit, new GUIContent("Vertical Fit"));
                    EditorGUI.indentLevel--;
                }
            }
        }

        if (GUILayout.Button("Copy VerticalLayoutGroup from scene"))
            CopyDialoguePanelLayoutFromScene();
    }

    void DrawNamePlateControls()
    {
        EditorGUILayout.Space(6f);
        DrawGroupTitle("NamePlate", "Отдельный Image фона имени говорящего. Rect использует Anchored Position и Size Delta: X/Y позиция, Width/Height размер.");
        DrawSpriteColumnHeader();
        DrawSpriteRow(
            "Sprite / Image nameplate",
            _namePlateSprite,
            _namePlateSpriteSource,
            CopyNamePlateFromScene,
            _overrideNamePlateColor,
            _namePlateColor,
            _overrideNamePlateImageType,
            _namePlateImageType,
            Image.Type.Sliced);
        DrawOverridePair(_overrideNamePlateColor, _namePlateColor, "NamePlate Color", "Color");
        DrawOverridePair(_overrideNamePlateImageType, _namePlateImageType, "NamePlate Image Type", "Type");
        DrawOverridePair(_overrideNamePlatePreserveAspect, _namePlatePreserveAspect, "Preserve Aspect", "Preserve");
        DrawOverridePair(_overrideNamePlatePixelsPerUnitMultiplier, _namePlatePixelsPerUnitMultiplier, "Pixels Per Unit", "Multiplier");
        DrawOverridePair(_overrideNamePlateMaterial, _namePlateMaterial, "Material", "Material");
        DrawOverridePair(_overrideNamePlateRaycastTarget, _namePlateRaycastTarget, "Raycast Target", "Raycast");
        DrawVector2Pair(
            _overrideNamePlateAnchors,
            _namePlateAnchorMin,
            _namePlateAnchorMax,
            "NamePlate Anchors",
            "Min",
            "Max");
        DrawOverridePair(_overrideNamePlatePivot, _namePlatePivot, "NamePlate Pivot", "Pivot");
        DrawVector2Pair(
            _overrideNamePlateRect,
            _namePlateAnchoredPosition,
            _namePlateSizeDelta,
            "NamePlate Rect",
            "Position",
            "Width / Height");

        if (GUILayout.Button("Скопировать NamePlate из сцены"))
            CopyNamePlateFromScene();
    }

    void DrawDialogueExtraLayersControls()
    {
        if (_dialogueExtraLayers == null)
            return;

        EditorGUILayout.Space(4f);
        DrawGroupTitle("Доп. плашки диалога", "Например Background (1) для прозрачности. Если у другой истории список пустой, эти доп. слои в сцене будут выключены.");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Собрать из сцены"))
                CopyDialogueExtraLayersFromScene();

            if (GUILayout.Button("+", GUILayout.Width(32f)))
                _dialogueExtraLayers.InsertArrayElementAtIndex(_dialogueExtraLayers.arraySize);

            using (new EditorGUI.DisabledScope(_dialogueExtraLayers.arraySize <= 0))
            {
                if (GUILayout.Button("-", GUILayout.Width(32f)))
                    _dialogueExtraLayers.DeleteArrayElementAtIndex(_dialogueExtraLayers.arraySize - 1);
            }
        }

        EditorGUILayout.PropertyField(_dialogueExtraLayers, new GUIContent("Слои"), true);
    }

    void DrawDialoguePanelAutoHeightControls()
    {
        EditorGUILayout.Space(4f);
        DrawGroupTitle("DialoguePanel: auto height");
        DrawOverridePair(_overrideDialoguePanelAutoHeight, _dialoguePanelAutoHeight, "Auto height by text", "Enabled");

        if (_overrideDialoguePanelAutoHeight == null ||
            (!_overrideDialoguePanelAutoHeight.boolValue && !_showDisabledOverrideValues))
        {
            return;
        }

        using (new EditorGUI.DisabledScope(!_overrideDialoguePanelAutoHeight.boolValue))
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_dialoguePanelAutoHeightPadding, new GUIContent("Bottom padding"));
            EditorGUILayout.PropertyField(_dialoguePanelAutoMinHeight, new GUIContent("Min height"));
            EditorGUILayout.PropertyField(_dialoguePanelAutoMaxHeight, new GUIContent("Max height (0 = Rect height)"));
            EditorGUILayout.PropertyField(_dialoguePanelAutoHeightKeepTop, new GUIContent("Keep top fixed"));
            EditorGUILayout.PropertyField(_dialoguePanelAutoHeightGrowthUpFactor, new GUIContent("Рост вверх (0-1)"));
            EditorGUI.indentLevel--;
        }
    }

    void DrawBodyTextHorizontalClampControls()
    {
        EditorGUILayout.Space(4f);
        DrawGroupTitle("BodyText: width clamp");
        DrawOverridePair(_overrideBodyTextHorizontalClamp, _bodyTextHorizontalClamp, "Clamp text inside panel", "Enabled");

        if (_overrideBodyTextHorizontalClamp == null ||
            (!_overrideBodyTextHorizontalClamp.boolValue && !_showDisabledOverrideValues))
        {
            return;
        }

        using (new EditorGUI.DisabledScope(!_overrideBodyTextHorizontalClamp.boolValue))
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_bodyTextHorizontalInset, new GUIContent("Horizontal inset"));
            EditorGUILayout.PropertyField(_bodyTextMaxWidth, new GUIContent("Max text width (0 = panel)"));
            EditorGUI.indentLevel--;
        }
    }

    void DrawBodyTextGrowDownControls()
    {
        EditorGUILayout.Space(4f);
        DrawGroupTitle("BodyText: GrowDown / высота / шрифт");
        DrawOverridePair(_overrideBodyTextGrowDownOffsetX, _bodyTextGrowDownOffsetX, "Offset X", "X");
        DrawOverridePair(_overrideBodyTextResizeHeightToPreferredText, _bodyTextResizeHeightToPreferredText, "Resize Height To Preferred", "Resize");
        DrawOverridePair(_overrideBodyTextExtraHeight, _bodyTextExtraHeight, "Extra Height", "Height");
        DrawOverridePair(_overrideBodyTextMinHeight, _bodyTextMinHeight, "Min Height", "Min");
        DrawOverridePair(_overrideBodyTextMaxHeight, _bodyTextMaxHeight, "Max Height", "Max");
        DrawOverridePair(_overrideBodyTextMaxFontSize, _bodyTextMaxFontSize, "Max Font Size", "Max");
        DrawFontOverridePair(_overrideBodyTextFont, _bodyTextFont, "BodyText font", "Font");
        DrawOverridePair(_overrideBodyTextShrinkTextToFitRect, _bodyTextShrinkTextToFitRect, "Shrink Text To Fit", "Shrink");
        DrawOverridePair(_overrideBodyTextMinAutoFontSize, _bodyTextMinAutoFontSize, "Min Auto Font Size", "Min");
        DrawOverridePair(_overrideBodyTextOverflowModeWhenStillTooLarge, _bodyTextOverflowModeWhenStillTooLarge, "Overflow", "Mode");
    }

    void DrawNameInputSection()
    {
        DrawApplyOnlySpritesToggle(_nameInputApplyOnlySprites, "экрана имени");
        if (!DrawFoldout(ref _showNameInput, "Экран ввода имени"))
            return;

        DrawSubTabBar(ref _nameInputTab, NameInputTabLabels);

        switch (_nameInputTab)
        {
            case NameInputTab.Background:
                DrawGroupTitle("Фон всего экрана", "Большой Image за экраном ввода имени. Это тот самый Background рядом с PreStoryFlow.");
                DrawSpriteColumnHeader();
                DrawSpriteRow("Sprite фона экрана", _nameScreenBackgroundSprite, _nameScreenBackgroundSpriteSource, CopyNameScreenBackgroundFromScene, _overrideNameScreenBackgroundColor, _nameScreenBackgroundColor, _overrideNameScreenBackgroundImageType, _nameScreenBackgroundImageType, Image.Type.Simple);
                DrawOverridePair(_overrideNameScreenBackgroundColor, _nameScreenBackgroundColor, "Цвет фона экрана", "Цвет");
                DrawOverridePair(_overrideNameScreenBackgroundImageType, _nameScreenBackgroundImageType, "Image Type фона экрана", "Type");

                EditorGUILayout.Space(6f);
                DrawGroupTitle("Плашка ввода имени", "Sprite, цвет и Rect панели, внутри которой находится поле имени.");
                DrawSpriteColumnHeader();
                DrawSpriteRow("Sprite плашки имени", _namePanelBackgroundSprite, _namePanelBackgroundSpriteSource, null, _overrideNamePanelBackgroundColor, _namePanelBackgroundColor, _overrideNamePanelBackgroundImageType, _namePanelBackgroundImageType, Image.Type.Simple);
                DrawOverridePair(_overrideNamePanelBackgroundColor, _namePanelBackgroundColor, "Цвет фона", "Цвет");
                DrawOverridePair(_overrideNamePanelBackgroundImageType, _namePanelBackgroundImageType, "Image Type фона", "Type");
                DrawVector2Pair(
                    _overrideNamePanelBackgroundRect,
                    _namePanelBackgroundAnchoredPosition,
                    _namePanelBackgroundSizeDelta,
                    "Rect фона",
                    "Позиция",
                    "Размер");
                break;

            case NameInputTab.Field:
                DrawGroupTitle("Поле ввода", "Sprite, цвет и Rect самого input-поля.");
                DrawSpriteColumnHeader();
                DrawSpriteRow("Sprite поля", _nameInputFieldSprite, _nameInputFieldSpriteSource, null, _overrideNameInputFieldColor, _nameInputFieldColor, _overrideNameInputFieldImageType, _nameInputFieldImageType, Image.Type.Sliced);
                DrawOverridePair(_overrideNameInputFieldColor, _nameInputFieldColor, "Цвет поля", "Цвет");
                DrawOverridePair(_overrideNameInputFieldImageType, _nameInputFieldImageType, "Image Type поля", "Type");
                DrawVector2Pair(
                    _overrideNameInputFieldRect,
                    _nameInputFieldAnchoredPosition,
                    _nameInputFieldSizeDelta,
                    "Rect поля",
                    "Позиция",
                    "Размер");
                break;

            case NameInputTab.Text:
                DrawGroupTitle("TMP_Text внутри поля", "Отдельно двигает и масштабирует текст имени внутри input-поля.");
                EditorGUILayout.LabelField("Текст ввода", EditorStyles.boldLabel);
                DrawVector2Pair(
                    _overrideNameInputTextRect,
                    _nameInputTextAnchoredPosition,
                    _nameInputTextSizeDelta,
                    "Rect TMP_Text",
                    "Позиция",
                    "Размер");
                DrawOverridePair(_overrideNameInputTextColor, _nameInputTextColor, "Цвет TMP_Text", "Цвет");
                DrawFontOverridePair(_overrideNameInputTextFont, _nameInputTextFont, "Override TMP_Text font", "Font");
                DrawOverridePair(_overrideNameInputTextFontSize, _nameInputTextFontSize, "Размер TMP_Text", "Размер");

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Placeholder", EditorStyles.boldLabel);
                DrawVector2Pair(
                    _overrideNamePlaceholderTextRect,
                    _namePlaceholderTextAnchoredPosition,
                    _namePlaceholderTextSizeDelta,
                    "Rect Placeholder",
                    "Позиция",
                    "Размер");
                DrawOverridePair(_overrideNamePlaceholderTextColor, _namePlaceholderTextColor, "Цвет Placeholder", "Цвет");
                DrawFontOverridePair(_overrideNamePlaceholderTextFont, _namePlaceholderTextFont, "Override Placeholder font", "Font");
                DrawOverridePair(_overrideNamePlaceholderTextFontSize, _namePlaceholderTextFontSize, "Размер Placeholder", "Размер");
                break;

            case NameInputTab.Button:
                DrawGroupTitle("Кнопка продолжения", "Sprite, Rect и текст кнопки на экране имени.");
                EditorGUILayout.PropertyField(_nameConfirmButtonPrefabOverride, new GUIContent("Prefab кнопки продолжения"));
                DrawSpriteColumnHeader();
                DrawSpriteRow("Sprite кнопки", _nameConfirmButtonSprite, _nameConfirmButtonSpriteSource, null, _overrideNameConfirmButtonColor, _nameConfirmButtonColor, _overrideNameConfirmButtonImageType, _nameConfirmButtonImageType, Image.Type.Simple);
                DrawOverridePair(_overrideNameConfirmButtonColor, _nameConfirmButtonColor, "Цвет кнопки", "Цвет");
                DrawOverridePair(_overrideNameConfirmButtonImageType, _nameConfirmButtonImageType, "Image Type кнопки", "Type");
                DrawVector2Pair(
                    _overrideNameConfirmButtonRect,
                    _nameConfirmButtonAnchoredPosition,
                    _nameConfirmButtonSizeDelta,
                    "Rect кнопки",
                    "Позиция",
                    "Размер");
                DrawVector2Pair(
                    _overrideNameConfirmButtonTextRect,
                    _nameConfirmButtonTextAnchoredPosition,
                    _nameConfirmButtonTextSizeDelta,
                    "Rect текста кнопки",
                    "Позиция",
                    "Размер");
                DrawOverridePair(_overrideNameConfirmButtonTextColor, _nameConfirmButtonTextColor, "Цвет текста", "Цвет");
                DrawFontOverridePair(_overrideNameConfirmButtonTextFont, _nameConfirmButtonTextFont, "Override button text font", "Font");
                DrawOverridePair(_overrideNameConfirmButtonTextFontSize, _nameConfirmButtonTextFontSize, "Размер текста", "Размер");
                break;

            case NameInputTab.ExtraTexts:
                DrawGroupTitle("Дополнительные тексты", "Список текстов для экрана имени. Количество хранится в этом Story UI Style, поэтому у каждой истории может быть свой набор.");
                DrawNameExtraTextList();
                break;
        }
    }

    void DrawNameExtraTextList()
    {
        if (_nameExtraTexts == null)
            return;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Тексты", EditorStyles.boldLabel);
            if (GUILayout.Button("+", GUILayout.Width(32f)))
                _nameExtraTexts.InsertArrayElementAtIndex(_nameExtraTexts.arraySize);
            using (new EditorGUI.DisabledScope(_nameExtraTexts.arraySize <= 0))
            {
                if (GUILayout.Button("-", GUILayout.Width(32f)))
                    _nameExtraTexts.DeleteArrayElementAtIndex(_nameExtraTexts.arraySize - 1);
            }
        }

        for (int i = 0; i < _nameExtraTexts.arraySize; i++)
        {
            SerializedProperty item = _nameExtraTexts.GetArrayElementAtIndex(i);
            SerializedProperty enabled = item.FindPropertyRelative("_enabled");
            SerializedProperty label = item.FindPropertyRelative("_label");
            string title = string.IsNullOrWhiteSpace(label.stringValue)
                ? $"Текст {i + 1}"
                : $"Текст {i + 1}: {label.stringValue}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                item.isExpanded = EditorGUILayout.Foldout(item.isExpanded, title, true);
                if (GUILayout.Button("↑", GUILayout.Width(28f)) && i > 0)
                    _nameExtraTexts.MoveArrayElement(i, i - 1);
                if (GUILayout.Button("↓", GUILayout.Width(28f)) && i < _nameExtraTexts.arraySize - 1)
                    _nameExtraTexts.MoveArrayElement(i, i + 1);
                if (GUILayout.Button("x", GUILayout.Width(28f)))
                {
                    _nameExtraTexts.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndVertical();
                    break;
                }
            }

            if (item.isExpanded)
                DrawNameExtraTextItem(item);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }
    }

    void DrawNameExtraTextItem(SerializedProperty item)
    {
        SerializedProperty enabled = item.FindPropertyRelative("_enabled");
        EditorGUILayout.PropertyField(enabled, new GUIContent("Показать"));
        DrawNameExtraTextTargetFields(item);
        EditorGUILayout.PropertyField(item.FindPropertyRelative("_label"), new GUIContent("Название в редакторе"));

        using (new EditorGUI.DisabledScope(!enabled.boolValue))
        {
            EditorGUILayout.PropertyField(item.FindPropertyRelative("_text"), new GUIContent("Текст"), true);
            DrawNameExtraTextRectControls(item);
            DrawMinMaxPair(
                item.FindPropertyRelative("_overrideHeightLimits"),
                item.FindPropertyRelative("_minHeight"),
                item.FindPropertyRelative("_maxHeight"),
                "Min/Max height",
                "Min",
                "Max");
            DrawOverridePair(item.FindPropertyRelative("_overrideColor"), item.FindPropertyRelative("_color"), "Цвет текста", "Цвет");
            DrawFontOverridePair(item.FindPropertyRelative("_overrideFont"), item.FindPropertyRelative("_font"), "Override text font", "Font");
            DrawOverridePair(item.FindPropertyRelative("_overrideFontSize"), item.FindPropertyRelative("_fontSize"), "Размер текста", "Размер");
            DrawOverridePair(item.FindPropertyRelative("_overrideAutoSize"), item.FindPropertyRelative("_autoSize"), "TMP Auto Size", "Auto");
            DrawMinMaxPair(
                item.FindPropertyRelative("_overrideAutoFontSizeRange"),
                item.FindPropertyRelative("_minAutoFontSize"),
                item.FindPropertyRelative("_maxAutoFontSize"),
                "Auto Size min/max",
                "Min",
                "Max");
            DrawOverridePair(item.FindPropertyRelative("_overrideAlignment"), item.FindPropertyRelative("_alignment"), "Alignment", "Alignment");
            DrawOverridePair(item.FindPropertyRelative("_overrideWordWrapping"), item.FindPropertyRelative("_wordWrapping"), "Word Wrapping", "Wrap");
            DrawOverridePair(item.FindPropertyRelative("_overrideOverflowMode"), item.FindPropertyRelative("_overflowMode"), "Overflow", "Mode");
            DrawOverridePair(item.FindPropertyRelative("_overrideLineSpacing"), item.FindPropertyRelative("_lineSpacing"), "Line Spacing", "Spacing");
            DrawOverridePair(item.FindPropertyRelative("_overrideMargins"), item.FindPropertyRelative("_margins"), "Margins", "Margins");
        }
    }

    void DrawNameExtraTextRectControls(SerializedProperty item)
    {
        if (item == null)
            return;

        DrawNameTextRectControls(
            item.FindPropertyRelative("_overrideRect"),
            item.FindPropertyRelative("_anchoredPosition"),
            item.FindPropertyRelative("_sizeDelta"),
            ResolveNameExtraTextForItem(item),
            "Rect / offset");
    }

    void DrawNameExtraTextTargetFields(SerializedProperty item)
    {
        SerializedProperty targetText = item.FindPropertyRelative("_targetText");
        SerializedProperty targetPath = item.FindPropertyRelative("_targetPath");
        if (targetText == null || targetPath == null)
            return;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("TMP_Text ссылка");
            TMP_Text currentText = targetText.objectReferenceValue as TMP_Text;
            if (currentText == null)
                currentText = ResolveNameExtraTextSceneReference(targetPath.stringValue);

            EditorGUI.BeginChangeCheck();
            UnityEngine.Object next = EditorGUILayout.ObjectField(currentText, typeof(TMP_Text), true);
            if (EditorGUI.EndChangeCheck())
            {
                TMP_Text pickedText = next as TMP_Text;
                targetText.objectReferenceValue = pickedText;
                targetPath.stringValue = pickedText != null ? BuildSceneTextPath(pickedText) : "";
                currentText = pickedText;
            }
            using (new EditorGUI.DisabledScope(currentText == null))
            {
                if (GUILayout.Button("Path", GUILayout.Width(52f)))
                    targetPath.stringValue = BuildSceneTextPath(currentText);
                if (GUILayout.Button("Ping", GUILayout.Width(52f)))
                    EditorGUIUtility.PingObject(currentText);
            }
        }

        EditorGUILayout.PropertyField(targetPath, new GUIContent("Scene path/name"));
        if (_showHints)
        {
            EditorGUILayout.HelpBox(
                "Прямая ссылка удобна для открытой сцены. Для сохранения между перезапусками держи ещё Scene path/name: можно указать имя TMP_Text или путь внутри inputName, например Header/Subtitle.",
                MessageType.None);
        }
    }

    TMP_Text ResolveNameExtraTextForItem(SerializedProperty item)
    {
        if (item == null)
            return null;

        SerializedProperty targetText = item.FindPropertyRelative("_targetText");
        SerializedProperty targetPath = item.FindPropertyRelative("_targetPath");
        TMP_Text text = targetText != null ? targetText.objectReferenceValue as TMP_Text : null;
        if (text != null)
            return text;

        return targetPath != null ? ResolveNameExtraTextSceneReference(targetPath.stringValue) : null;
    }

    void DrawNameTextRectControls(
        SerializedProperty overrideRect,
        SerializedProperty anchoredPosition,
        SerializedProperty sizeDelta,
        TMP_Text sceneText,
        string label)
    {
        if (overrideRect == null || anchoredPosition == null || sizeDelta == null)
            return;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(overrideRect, new GUIContent(label));
            using (new EditorGUI.DisabledScope(sceneText == null))
            {
                if (GUILayout.Button("from scene", GUILayout.Width(88f)))
                {
                    overrideRect.boolValue = true;
                    CopyTextRectFromScene(sceneText, anchoredPosition, sizeDelta);
                }
            }
        }

        using (new EditorGUI.DisabledScope(!overrideRect.boolValue))
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(anchoredPosition, new GUIContent("Position"));
            DrawSizeDeltaWidthHeight(sizeDelta);
            EditorGUI.indentLevel--;
        }
    }

    static void DrawSizeDeltaWidthHeight(SerializedProperty sizeDelta)
    {
        if (sizeDelta == null)
            return;

        Vector2 size = sizeDelta.vector2Value;
        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("Size");
            EditorGUILayout.LabelField("Width", GUILayout.Width(42f));
            float width = EditorGUILayout.FloatField(size.x);
            EditorGUILayout.LabelField("Height", GUILayout.Width(46f));
            float height = EditorGUILayout.FloatField(size.y);
            if (EditorGUI.EndChangeCheck())
                sizeDelta.vector2Value = new Vector2(width, height);
        }
    }

    static void CopyTextRectFromScene(
        TMP_Text text,
        SerializedProperty anchoredPosition,
        SerializedProperty sizeDelta)
    {
        RectTransform rect = text != null ? text.rectTransform : null;
        if (rect == null || anchoredPosition == null || sizeDelta == null)
            return;

        Vector2 size = rect.sizeDelta;
        Vector2 rectSize = rect.rect.size;
        if (Mathf.Abs(size.x) < 0.01f && rectSize.x > 0.01f)
            size.x = rectSize.x;
        if (Mathf.Abs(size.y) < 0.01f && rectSize.y > 0.01f)
            size.y = rectSize.y;

        anchoredPosition.vector2Value = rect.anchoredPosition;
        sizeDelta.vector2Value = size;
    }

    static string BuildSceneTextPath(TMP_Text text)
    {
        if (text == null)
            return "";

        Transform root = FindNamePanelRoot(text.transform);
        return root != null ? BuildRelativePath(root, text.transform) : text.name;
    }

    static TMP_Text ResolveNameExtraTextSceneReference(string pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
            return null;

        string normalized = pathOrName.Trim().Replace('\\', '/');
        PreStorySetupFlow setup = FindSceneSetupFlow();
        Transform setupRoot = setup != null ? setup.transform : null;

        TMP_Text[] texts = UnityEngine.Object.FindObjectsOfType<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            if (string.Equals(text.name, normalized, StringComparison.OrdinalIgnoreCase))
                return text;

            Transform nameRoot = FindNamePanelRoot(text.transform);
            if (nameRoot != null && string.Equals(BuildRelativePath(nameRoot, text.transform), normalized, StringComparison.OrdinalIgnoreCase))
                return text;

            if (setupRoot != null && text.transform.IsChildOf(setupRoot) &&
                string.Equals(BuildRelativePath(setupRoot, text.transform), normalized, StringComparison.OrdinalIgnoreCase))
                return text;
        }

        return null;
    }

    static Transform FindNamePanelRoot(Transform current)
    {
        Transform fallback = null;
        while (current != null)
        {
            if (string.Equals(current.name, "inputName", StringComparison.OrdinalIgnoreCase))
                return current;

            if (fallback == null && current.GetComponent<PreStorySetupFlow>() != null)
                fallback = current;

            current = current.parent;
        }

        return fallback;
    }

    static string BuildRelativePath(Transform root, Transform target)
    {
        if (root == null || target == null)
            return "";

        if (root == target)
            return target.name;

        List<string> parts = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        if (current != root)
            return target.name;

        parts.Reverse();
        return string.Join("/", parts);
    }

    void DrawNameExtraTextBlock(
        string title,
        SerializedProperty useText,
        SerializedProperty text,
        SerializedProperty overrideRect,
        SerializedProperty anchoredPosition,
        SerializedProperty sizeDelta,
        SerializedProperty overrideColor,
        SerializedProperty color,
        SerializedProperty overrideFont,
        SerializedProperty font,
        SerializedProperty overrideFontSize,
        SerializedProperty fontSize)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useText, new GUIContent("Показать"));
        using (new EditorGUI.DisabledScope(useText != null && !useText.boolValue))
        {
            EditorGUILayout.PropertyField(text, new GUIContent("Текст"), true);
            DrawVector2Pair(
                overrideRect,
                anchoredPosition,
                sizeDelta,
                "Rect",
                "Позиция",
                "Размер");
            DrawOverridePair(overrideColor, color, "Цвет текста", "Цвет");
            DrawFontOverridePair(overrideFont, font, "Override text font", "Font");
            DrawOverridePair(overrideFontSize, fontSize, "Размер текста", "Размер");
        }
    }

    void DrawChoicesSection()
    {
        DrawApplyOnlySpritesToggle(_choicesApplyOnlySprites, "выборов");
        if (!DrawFoldout(ref _showChoices, "Выборы"))
            return;

        EditorGUILayout.LabelField("Кнопка выбора", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_choiceButtonPrefabOverride, new GUIContent("Prefab кнопки выбора"));
        EditorGUILayout.PropertyField(_premiumChoiceButtonPrefabOverride, new GUIContent("Prefab платной кнопки выбора"));
        EditorGUILayout.PropertyField(_premiumChoiceBalancePanelPrefabOverride, new GUIContent("Prefab панели баланса платного выбора"));
        EditorGUILayout.PropertyField(_premiumChoiceBalancePanelOffset, new GUIContent("Offset панели баланса платного выбора"));
        DrawSpriteColumnHeader();
        DrawSpriteRow("Sprite кнопки", _choiceButtonSprite, _choiceButtonSpriteSource, null, _overrideChoiceButtonColor, _choiceButtonColor, _overrideChoiceButtonImageType, _choiceButtonImageType, Image.Type.Sliced);
        DrawOverridePair(_overrideChoiceButtonColor, _choiceButtonColor, "Цвет кнопки", "Цвет");
        DrawOverridePair(_overrideChoiceButtonImageType, _choiceButtonImageType, "Image Type кнопки", "Type");
        DrawOverridePair(_overrideChoiceButtonTextColor, _choiceButtonTextColor, "Цвет текста", "Цвет");
        DrawFontOverridePair(_overrideChoiceButtonFont, _choiceButtonFont, "Override choice text font", "Font");
        DrawOverridePair(_overrideChoiceButtonFontSize, _choiceButtonFontSize, "Размер текста", "Размер");
        DrawOverridePair(_overrideChoiceButtonPadding, _choiceButtonPadding, "Padding кнопки", "X/Y");
        DrawOverridePair(_overrideChoiceButtonTextPadding, _choiceButtonTextPadding, "Padding текста внутри", "Padding");
        DrawOverridePair(_overrideChoiceButtonTextOffset, _choiceButtonTextOffset, "Offset текста внутри", "Offset");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Фон контейнера выбора", EditorStyles.boldLabel);
        DrawSpriteColumnHeader();
        DrawSpriteRow("Sprite фона выбора", _choicePanelSprite, _choicePanelSpriteSource, null, _overrideChoicePanelColor, _choicePanelColor, _overrideChoicePanelImageType, _choicePanelImageType, Image.Type.Sliced);
        DrawOverridePair(_overrideChoicePanelColor, _choicePanelColor, "Цвет фона", "Цвет");
        DrawOverridePair(_overrideChoicePanelImageType, _choicePanelImageType, "Image Type фона", "Type");
    }

    void DrawStatPanelBackgroundRectControls(string title = "Stats Background Rect")
    {
        EditorGUILayout.Space(4f);
        DrawGroupTitle(title, "RectTransform of the shared stats background Image. Stretch offsets match Unity Left/Right/Top/Bottom.");
        DrawVector2Pair(
            _overrideStatPanelBackgroundAnchors,
            _statPanelBackgroundAnchorMin,
            _statPanelBackgroundAnchorMax,
            "Background anchors / stretch",
            "Anchor Min",
            "Anchor Max");
        DrawOverridePair(_overrideStatPanelBackgroundPivot, _statPanelBackgroundPivot, "Background pivot", "Pivot");
        DrawStretchOffsetsPair(
            _overrideStatPanelBackgroundStretchOffsets,
            _statPanelBackgroundStretchOffsets,
            "Background stretch offsets");

        if (GUILayout.Button("Copy Stats Background Rect from scene"))
            CopyStatBackgroundRectFromScene();
    }

    void DrawStatPanelLayoutGroupControls()
    {
        DrawGroupTitle("Stats Panel: VerticalLayoutGroup");

        if (_overrideStatPanelVerticalLayout != null)
        {
            EditorGUILayout.PropertyField(_overrideStatPanelVerticalLayout, new GUIContent("Override VerticalLayoutGroup"));
            if (_overrideStatPanelVerticalLayout.boolValue || _showDisabledOverrideValues)
            {
                using (new EditorGUI.DisabledScope(!_overrideStatPanelVerticalLayout.boolValue))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_statPanelVerticalLayoutPadding, new GUIContent("Padding"), true);
                    EditorGUILayout.PropertyField(_statPanelVerticalLayoutSpacing, new GUIContent("Spacing"));
                    EditorGUILayout.PropertyField(_statPanelVerticalLayoutChildAlignment, new GUIContent("Child Alignment"));
                    EditorGUILayout.PropertyField(_statPanelVerticalLayoutReverseArrangement, new GUIContent("Reverse Arrangement"));
                    DrawBoolPropertyPair(
                        _statPanelVerticalLayoutControlChildWidth,
                        "Control Width",
                        _statPanelVerticalLayoutControlChildHeight,
                        "Control Height");
                    DrawBoolPropertyPair(
                        _statPanelVerticalLayoutUseChildScaleWidth,
                        "Use Scale Width",
                        _statPanelVerticalLayoutUseChildScaleHeight,
                        "Use Scale Height");
                    DrawBoolPropertyPair(
                        _statPanelVerticalLayoutChildForceExpandWidth,
                        "Force Width",
                        _statPanelVerticalLayoutChildForceExpandHeight,
                        "Force Height");
                    EditorGUI.indentLevel--;
                }
            }
        }

        EditorGUILayout.Space(3f);
        DrawGroupTitle("Stats Panel: ContentSizeFitter");

        if (_overrideStatPanelContentSizeFitter != null)
        {
            EditorGUILayout.PropertyField(_overrideStatPanelContentSizeFitter, new GUIContent("Override ContentSizeFitter"));
            if (_overrideStatPanelContentSizeFitter.boolValue || _showDisabledOverrideValues)
            {
                using (new EditorGUI.DisabledScope(!_overrideStatPanelContentSizeFitter.boolValue))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_statPanelContentSizeFitterHorizontalFit, new GUIContent("Horizontal Fit"));
                    EditorGUILayout.PropertyField(_statPanelContentSizeFitterVerticalFit, new GUIContent("Vertical Fit"));
                    EditorGUI.indentLevel--;
                }
            }
        }

        if (GUILayout.Button("Copy Stats VerticalLayoutGroup from scene"))
            CopyStatLayoutFromScene();

        EditorGUILayout.Space(6f);
    }

    void DrawRelationshipPanelBackgroundRectControls()
    {
        EditorGUILayout.Space(4f);
        DrawGroupTitle("Relationship Background Rect", "Separate RectTransform overrides for relationship messages.");
        DrawVector2Pair(
            _overrideRelationshipPanelBackgroundAnchors,
            _relationshipPanelBackgroundAnchorMin,
            _relationshipPanelBackgroundAnchorMax,
            "Background anchors / stretch",
            "Anchor Min",
            "Anchor Max");
        DrawOverridePair(_overrideRelationshipPanelBackgroundPivot, _relationshipPanelBackgroundPivot, "Background pivot", "Pivot");
        DrawVector2Pair(
            _overrideRelationshipPanelBackgroundRect,
            _relationshipPanelBackgroundAnchoredPosition,
            _relationshipPanelBackgroundSizeDelta,
            "Background rect / offsets",
            "Position / offsets",
            "SizeDelta",
            true);
        DrawStretchOffsetsPair(
            _overrideRelationshipPanelBackgroundStretchOffsets,
            _relationshipPanelBackgroundStretchOffsets,
            "Background stretch offsets");

        if (GUILayout.Button("Copy Relationship Background Rect from scene"))
            CopyRelationshipBackgroundRectFromScene();
    }

    void DrawRelationshipPanelLayoutGroupControls()
    {
        EditorGUILayout.Space(4f);
        DrawGroupTitle("Relationship Panel: VerticalLayoutGroup");

        if (_overrideRelationshipPanelVerticalLayout != null)
        {
            EditorGUILayout.PropertyField(_overrideRelationshipPanelVerticalLayout, new GUIContent("Override VerticalLayoutGroup"));
            if (_overrideRelationshipPanelVerticalLayout.boolValue || _showDisabledOverrideValues)
            {
                using (new EditorGUI.DisabledScope(!_overrideRelationshipPanelVerticalLayout.boolValue))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_relationshipPanelVerticalLayoutPadding, new GUIContent("Padding"), true);
                    EditorGUILayout.PropertyField(_relationshipPanelVerticalLayoutSpacing, new GUIContent("Spacing"));
                    EditorGUILayout.PropertyField(_relationshipPanelVerticalLayoutChildAlignment, new GUIContent("Child Alignment"));
                    EditorGUILayout.PropertyField(_relationshipPanelVerticalLayoutReverseArrangement, new GUIContent("Reverse Arrangement"));
                    DrawBoolPropertyPair(
                        _relationshipPanelVerticalLayoutControlChildWidth,
                        "Control Width",
                        _relationshipPanelVerticalLayoutControlChildHeight,
                        "Control Height");
                    DrawBoolPropertyPair(
                        _relationshipPanelVerticalLayoutUseChildScaleWidth,
                        "Use Scale Width",
                        _relationshipPanelVerticalLayoutUseChildScaleHeight,
                        "Use Scale Height");
                    DrawBoolPropertyPair(
                        _relationshipPanelVerticalLayoutChildForceExpandWidth,
                        "Force Width",
                        _relationshipPanelVerticalLayoutChildForceExpandHeight,
                        "Force Height");
                    EditorGUI.indentLevel--;
                }
            }
        }

        EditorGUILayout.Space(3f);
        DrawGroupTitle("Relationship Panel: ContentSizeFitter");

        if (_overrideRelationshipPanelContentSizeFitter != null)
        {
            EditorGUILayout.PropertyField(_overrideRelationshipPanelContentSizeFitter, new GUIContent("Override ContentSizeFitter"));
            if (_overrideRelationshipPanelContentSizeFitter.boolValue || _showDisabledOverrideValues)
            {
                using (new EditorGUI.DisabledScope(!_overrideRelationshipPanelContentSizeFitter.boolValue))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_relationshipPanelContentSizeFitterHorizontalFit, new GUIContent("Horizontal Fit"));
                    EditorGUILayout.PropertyField(_relationshipPanelContentSizeFitterVerticalFit, new GUIContent("Vertical Fit"));
                    EditorGUI.indentLevel--;
                }
            }
        }

        if (GUILayout.Button("Copy Relationship VerticalLayoutGroup from scene"))
            CopyRelationshipLayoutFromScene();

        EditorGUILayout.Space(6f);
    }

    void DrawStatsSection()
    {
        DrawApplyOnlySpritesToggle(_statsApplyOnlySprites, "статов");
        if (!DrawFoldout(ref _showStats, "Статы"))
            return;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Скопировать layout статов из сцены"))
                CopyStatLayoutFromScene();

            if (GUILayout.Button("Применить style к статам в сцене"))
                ApplyTargetsToOpenScene();
        }

        DrawSubTabBar(ref _statsTab, StatsTabLabels);

        switch (_statsTab)
        {
            case StatsTab.Panel:
                DrawGroupTitle("Плашка статов", "Sprite, цвет, размер, позиция и padding самой плашки. Это главный блок настройки статового уведомления.");
                DrawSpriteColumnHeader();
                DrawSpriteRow("Sprite плашки", _statPanelSprite, _statPanelSpriteSource, null, _overrideStatPanelColor, _statPanelColor, _overrideStatPanelImageType, _statPanelImageType, Image.Type.Sliced);
                DrawOverridePair(_overrideStatPanelColor, _statPanelColor, "Цвет плашки", "Цвет");
                DrawOverridePair(_overrideStatPanelImageType, _statPanelImageType, "Image Type плашки", "Type");
                DrawStatPanelBackgroundRectControls();
                DrawVector2Pair(_overrideStatPanelRect, _statPanelAnchoredPosition, _statPanelSizeDelta, "Rect плашки", "Позиция", "Размер", true);
                DrawStatPanelSizeOverrides();
                DrawOverridePair(_overrideStatPanelPadding, _statPanelPadding, "Padding плашки", "Padding");

                EditorGUILayout.Space(6f);
                DrawGroupTitle("Текст статов", "Позиция, размер, шрифт и поведение TMP_Text внутри плашки.");
                DrawOverridePair(_overrideStatTextColor, _statTextColor, "Цвет текста", "Цвет");
                DrawFontOverridePair(_overrideStatTextFont, _statTextFont, "Override stat text font", "Font");
                DrawOverridePair(_overrideStatTextFontSize, _statTextFontSize, "Размер текста", "Размер");
                DrawVector2Pair(_overrideStatTextRect, _statTextAnchoredPosition, _statTextSizeDelta, "Rect текста", "Позиция", "Размер", true);
                DrawStatTextRectOverrides();
                DrawOverridePair(_overrideStatTextAutoSize, _statTextAutoSize, "TMP Auto Size", "Auto Size");
                DrawMinMaxPair(_overrideStatTextAutoFontSizeRange, _statTextMinAutoFontSize, _statTextMaxAutoFontSize, "Auto Size min/max", "Min", "Max");
                DrawOverridePair(_overrideStatTextAlignment, _statTextAlignment, "Alignment текста", "Alignment");
                DrawOverridePair(_overrideStatTextWordWrapping, _statTextWordWrapping, "Word wrapping", "Wrapping");
                DrawOverridePair(_overrideStatTextOverflowMode, _statTextOverflowMode, "Overflow", "Overflow");
                DrawOverridePair(_overrideStatTextLineSpacing, _statTextLineSpacing, "Line spacing", "Spacing");
                DrawOverridePair(_overrideStatTextMargins, _statTextMargins, "Margins текста", "Margins");
                break;

            case StatsTab.Definitions:
                DrawGroupTitle("Список статов", "Включи замену, чтобы история не брала дефолтные статы ZLS.");
                EditorGUILayout.PropertyField(_replaceStatDefinitions, new GUIContent("Заменить список статов"));
                using (new EditorGUI.DisabledScope(!_replaceStatDefinitions.boolValue))
                {
                    EditorGUILayout.PropertyField(_statOverlayDefinitions, new GUIContent("Определения для плашки"), true);
                    EditorGUILayout.PropertyField(_statDefinitionAssets, new GUIContent("Stat assets истории"), true);
                }
                break;

            case StatsTab.Layout:
                DrawStatPanelLayoutGroupControls();
                DrawGroupTitle("Иконка и layout", "Размер, offset и spacing иконки. Offset переводит иконку в ручной режим, чтобы LayoutGroup не сбрасывал позицию.");
                DrawOverridePair(_overrideStatIconSize, _statIconSize, "Размер иконки", "Размер");
                DrawOverridePair(_overrideStatIconOffset, _statIconOffset, "Offset иконки", "Offset");
                DrawStatIconOffsetOverrides();
                DrawOverridePair(_overrideStatIconVisualScale, _statIconVisualScale, "Visual scale иконки", "Scale");
                DrawOverridePair(_overrideStatIconMinSize, _statIconMinSize, "Min size иконки", "Min size");
                DrawOverridePair(_overrideStatIconReserveSpaceWhenHidden, _statIconReserveSpaceWhenHidden, "Резерв места без иконки", "Резерв");
                DrawOverridePair(_overrideStatIconParentSpacing, _statIconParentSpacing, "Spacing иконка-текст", "Spacing");
                DrawOverridePair(_overrideStatIconParentPadding, _statIconParentPadding, "Padding родителя", "Padding");
                break;

            case StatsTab.Relationships:
                DrawGroupTitle("Отношения", "Отдельные размеры и текст для сообщений отношений. Иконки отношений настраиваются ниже через statId relationship:имя.");
                DrawVector2Pair(
                    _overrideRelationshipFrameSize,
                    _relationshipFrameAnchoredPosition,
                    _relationshipFrameSize,
                    "Rect плашки отношений",
                    "Позиция",
                    "Размер",
                    true);
                DrawRelationshipPanelBackgroundRectControls();
                DrawRelationshipPanelLayoutGroupControls();
                DrawMinMaxPair(_overrideRelationshipFontSizeRange, _relationshipFontSizeMin, _relationshipFontSizeMax, "Auto Size min/max", "Min", "Max");
                DrawOverridePair(_overrideRelationshipMaxVisibleLines, _relationshipMaxVisibleLines, "Макс. строк", "Строки");

                EditorGUILayout.Space(6f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Добавить offsets отношений"))
                        AddDefaultRelationshipIconOffsetOverrides();

                    if (GUILayout.Button("Добавить фразы из статов"))
                        AddMissingRelationshipMessageOverridesFromDefinitions();

                    if (GUILayout.Button("Preview отношений"))
                        StoryInterfacePreviewWindow.Open();
                }

                DrawRelationshipMessageOverrides();

                EditorGUILayout.Space(6f);
                DrawStatIconOffsetOverrides();
                break;
        }
    }

    void DrawRelationshipMessageOverrides()
    {
        if (_relationshipMessageOverrides == null)
            return;

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Фразы, склонения и текст по relationship:*", EditorStyles.boldLabel);
                if (GUILayout.Button("+", GUILayout.Width(30f)))
                    AddRelationshipMessageOverride("", "");
            }

            if (_showHints)
            {
                EditorGUILayout.HelpBox(
                    "Для склонения пиши готовую форму в Target, например: \"с Джеймсоном\". В шаблонах доступны {target} и {statId}.",
                    MessageType.None);
            }

            if (_relationshipMessageOverrides.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Пока нет отдельных настроек отношений. Нажми \"Добавить фразы из статов\" или плюс.", MessageType.Info);
                return;
            }

            for (int i = 0; i < _relationshipMessageOverrides.arraySize; i++)
            {
                SerializedProperty item = _relationshipMessageOverrides.GetArrayElementAtIndex(i);
                DrawRelationshipMessageOverrideElement(item, i);
            }
        }
    }

    void DrawRelationshipMessageOverrideElement(SerializedProperty item, int index)
    {
        if (item == null)
            return;

        SerializedProperty statId = item.FindPropertyRelative("statId");
        SerializedProperty overrideTargetText = item.FindPropertyRelative("overrideTargetText");
        SerializedProperty targetText = item.FindPropertyRelative("targetText");
        SerializedProperty overrideImprovedText = item.FindPropertyRelative("overrideImprovedText");
        SerializedProperty improvedText = item.FindPropertyRelative("improvedText");
        SerializedProperty overrideWorsenedText = item.FindPropertyRelative("overrideWorsenedText");
        SerializedProperty worsenedText = item.FindPropertyRelative("worsenedText");

        string title = string.IsNullOrWhiteSpace(statId?.stringValue)
            ? "Общий шаблон отношений"
            : statId.stringValue;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                item.isExpanded = EditorGUILayout.Foldout(item.isExpanded, title, true);

                if (GUILayout.Button("из списка", GUILayout.Width(74f)))
                    AddMissingRelationshipMessageOverridesFromDefinitions();
                if (GUILayout.Button("↑", GUILayout.Width(24f)) && index > 0)
                    _relationshipMessageOverrides.MoveArrayElement(index, index - 1);
                if (GUILayout.Button("↓", GUILayout.Width(24f)) && index < _relationshipMessageOverrides.arraySize - 1)
                    _relationshipMessageOverrides.MoveArrayElement(index, index + 1);
                if (GUILayout.Button("x", GUILayout.Width(24f)))
                {
                    _relationshipMessageOverrides.DeleteArrayElementAtIndex(index);
                    return;
                }
            }

            if (!item.isExpanded)
                return;

            DrawRelationshipCharacterSelector(item, index, statId, overrideTargetText, targetText);
            DrawRelationshipTargetOverride(overrideTargetText, targetText);
            DrawRelationshipTemplate(overrideImprovedText, improvedText, "Своя фраза при плюсе", "Отношения {target} улучшились.");
            DrawRelationshipTemplate(overrideWorsenedText, worsenedText, "Своя фраза при минусе", "Отношения {target} ухудшились.");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Текст плашки только для этого отношения", EditorStyles.miniBoldLabel);
            DrawVector2Pair(
                item.FindPropertyRelative("overrideTextRect"),
                item.FindPropertyRelative("textAnchoredPosition"),
                item.FindPropertyRelative("textSizeDelta"),
                "Rect / offset текста",
                "Позиция",
                "Размер");
            DrawOverridePair(item.FindPropertyRelative("overrideTextColor"), item.FindPropertyRelative("textColor"), "Цвет текста", "Цвет");
            DrawFontOverridePair(item.FindPropertyRelative("overrideTextFont"), item.FindPropertyRelative("textFont"), "Override text font", "Font");
            DrawOverridePair(item.FindPropertyRelative("overrideTextFontSize"), item.FindPropertyRelative("textFontSize"), "Размер текста", "Размер");
            DrawOverridePair(item.FindPropertyRelative("overrideTextAutoSize"), item.FindPropertyRelative("textAutoSize"), "TMP Auto Size", "Auto");
            DrawMinMaxPair(
                item.FindPropertyRelative("overrideTextAutoFontSizeRange"),
                item.FindPropertyRelative("minAutoFontSize"),
                item.FindPropertyRelative("maxAutoFontSize"),
                "Auto Size min/max",
                "Min",
                "Max");
            DrawOverridePair(item.FindPropertyRelative("overrideTextAlignment"), item.FindPropertyRelative("textAlignment"), "Alignment текста", "Alignment");
            DrawOverridePair(item.FindPropertyRelative("overrideTextWordWrapping"), item.FindPropertyRelative("textWordWrapping"), "Word wrapping", "Wrapping");
            DrawOverridePair(item.FindPropertyRelative("overrideTextOverflowMode"), item.FindPropertyRelative("textOverflowMode"), "Overflow", "Mode");
            DrawOverridePair(item.FindPropertyRelative("overrideTextLineSpacing"), item.FindPropertyRelative("textLineSpacing"), "Line spacing", "Spacing");
            DrawOverridePair(item.FindPropertyRelative("overrideTextMargins"), item.FindPropertyRelative("textMargins"), "Margins текста", "Margins");
        }
    }

    void DrawRelationshipCharacterSelector(
        SerializedProperty item,
        int index,
        SerializedProperty statId,
        SerializedProperty overrideTargetText,
        SerializedProperty targetText)
    {
        if (statId == null)
            return;

        List<RelationshipCharacterOption> characters = GetRelationshipCharacterOptions();
        if (characters.Count > 0)
        {
            string[] labels = new string[characters.Count + 1];
            labels[0] = "Общий шаблон / без персонажа";
            int selectedIndex = 0;
            for (int i = 0; i < characters.Count; i++)
            {
                RelationshipCharacterOption option = characters[i];
                labels[i + 1] = option.Label;
                if (string.Equals(option.StatId, statId.stringValue, StringComparison.OrdinalIgnoreCase))
                    selectedIndex = i + 1;
            }

            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup("ID персонажа", selectedIndex, labels);
            if (EditorGUI.EndChangeCheck())
            {
                string previousStatId = statId.stringValue;
                if (nextIndex <= 0)
                {
                    statId.stringValue = "";
                    if (overrideTargetText != null)
                        overrideTargetText.boolValue = false;
                    if (targetText != null)
                        targetText.stringValue = "";
                }
                else
                {
                    RelationshipCharacterOption option = characters[nextIndex - 1];
                    int existingIndex = FindRelationshipMessageOverrideIndexExcept(option.StatId, index);
                    if (!string.Equals(previousStatId, option.StatId, StringComparison.OrdinalIgnoreCase) &&
                        existingIndex >= 0)
                    {
                        MoveRelationshipMessageOverrideToIndex(existingIndex, index);
                        serializedObject.ApplyModifiedProperties();
                        MarkTargetsDirty();
                        QueueApplyTargetsToOpenScene();
                        Repaint();
                        GUIUtility.ExitGUI();
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(previousStatId) &&
                        !string.Equals(previousStatId, option.StatId, StringComparison.OrdinalIgnoreCase) &&
                        !HasRelationshipMessageOverrideExcept(previousStatId, index))
                    {
                        PreserveCurrentRelationshipOverrideAsSeparateElement(index);
                        item = _relationshipMessageOverrides.GetArrayElementAtIndex(index);
                        statId = item.FindPropertyRelative("statId");
                        overrideTargetText = item.FindPropertyRelative("overrideTargetText");
                        targetText = item.FindPropertyRelative("targetText");
                    }

                    statId.stringValue = option.StatId;
                    if (overrideTargetText != null)
                        overrideTargetText.boolValue = true;
                    if (targetText != null)
                        targetText.stringValue = BuildDefaultRelationshipTargetText(option.DisplayName, option.StatId);
                }
            }
        }

        EditorGUILayout.PropertyField(statId, new GUIContent("ID отношения"));
    }

    void MoveRelationshipMessageOverrideToIndex(int fromIndex, int toIndex)
    {
        if (_relationshipMessageOverrides == null ||
            fromIndex < 0 ||
            toIndex < 0 ||
            fromIndex >= _relationshipMessageOverrides.arraySize ||
            toIndex >= _relationshipMessageOverrides.arraySize ||
            fromIndex == toIndex)
        {
            return;
        }

        _relationshipMessageOverrides.MoveArrayElement(fromIndex, toIndex);
        SerializedProperty current = _relationshipMessageOverrides.GetArrayElementAtIndex(toIndex);
        if (current != null)
            current.isExpanded = true;
    }

    void PreserveCurrentRelationshipOverrideAsSeparateElement(int index)
    {
        if (_relationshipMessageOverrides == null ||
            index < 0 ||
            index >= _relationshipMessageOverrides.arraySize)
        {
            return;
        }

        _relationshipMessageOverrides.InsertArrayElementAtIndex(index + 1);
        SerializedProperty preserved = _relationshipMessageOverrides.GetArrayElementAtIndex(index + 1);
        if (preserved != null)
            preserved.isExpanded = false;
    }

    bool HasRelationshipMessageOverrideExcept(string statId, int exceptIndex)
    {
        return FindRelationshipMessageOverrideIndexExcept(statId, exceptIndex) >= 0;
    }

    int FindRelationshipMessageOverrideIndexExcept(string statId, int exceptIndex)
    {
        if (_relationshipMessageOverrides == null || string.IsNullOrWhiteSpace(statId))
            return -1;

        for (int i = 0; i < _relationshipMessageOverrides.arraySize; i++)
        {
            if (i == exceptIndex)
                continue;

            SerializedProperty item = _relationshipMessageOverrides.GetArrayElementAtIndex(i);
            SerializedProperty existingId = item.FindPropertyRelative("statId");
            if (existingId != null &&
                string.Equals(existingId.stringValue?.Trim(), statId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    void DrawRelationshipTargetOverride(SerializedProperty toggle, SerializedProperty targetText)
    {
        if (toggle == null || targetText == null)
            return;

        if (!toggle.boolValue && !_showDisabledOverrideValues)
        {
            DrawOverrideToggleOnly(toggle, "Ручное склонение");
            return;
        }

        EditorGUILayout.PropertyField(toggle, new GUIContent("Ручное склонение"));
        using (new EditorGUI.DisabledScope(!toggle.boolValue))
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Текст после «Отношения»");
            targetText.stringValue = EditorGUILayout.TextField(targetText.stringValue);
            EditorGUI.indentLevel--;
        }
    }

    void DrawRelationshipTemplate(SerializedProperty toggle, SerializedProperty text, string label, string placeholder)
    {
        if (toggle == null || text == null)
            return;

        if (!toggle.boolValue && !_showDisabledOverrideValues)
        {
            DrawOverrideToggleOnly(toggle, label);
            return;
        }

        EditorGUILayout.PropertyField(toggle, new GUIContent(label));
        using (new EditorGUI.DisabledScope(!toggle.boolValue))
        {
            string current = text.stringValue;
            if (string.IsNullOrEmpty(current) && toggle.boolValue)
                current = placeholder;

            text.stringValue = EditorGUILayout.TextArea(current, GUILayout.MinHeight(38f));
        }
    }

    void AddMissingRelationshipMessageOverridesFromDefinitions()
    {
        if (_relationshipMessageOverrides == null)
            return;

        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_statOverlayDefinitions != null)
        {
            for (int i = 0; i < _statOverlayDefinitions.arraySize; i++)
            {
                SerializedProperty item = _statOverlayDefinitions.GetArrayElementAtIndex(i);
                SerializedProperty statId = item.FindPropertyRelative("statId");
                SerializedProperty displayName = item.FindPropertyRelative("displayName");
                if (statId == null || !IsRelationshipStatId(statId.stringValue) || ids.Contains(statId.stringValue))
                    continue;

                ids.Add(statId.stringValue);
                AddRelationshipMessageOverride(statId.stringValue, displayName != null ? displayName.stringValue : "");
            }
        }

        if (_statDefinitionAssets != null)
        {
            for (int i = 0; i < _statDefinitionAssets.arraySize; i++)
            {
                StatDefinition definition = _statDefinitionAssets.GetArrayElementAtIndex(i).objectReferenceValue as StatDefinition;
                if (definition == null || !IsRelationshipStatId(definition.statId) || ids.Contains(definition.statId))
                    continue;

                ids.Add(definition.statId);
                AddRelationshipMessageOverride(definition.statId, definition.displayName);
            }
        }

        StoryUiStyle style = target as StoryUiStyle;
        if (style != null && TryFindStyleContext(style, out StyleContext context) && context.Library != null && context.Library.Assets != null)
        {
            IReadOnlyList<StoryJsonAssetReference> assets = context.Library.Assets;
            for (int i = 0; i < assets.Count; i++)
            {
                StoryJsonAssetReference asset = assets[i];
                CharacterData character = asset != null ? asset.Character : null;
                if (character == null)
                    continue;

                string statId = "relationship:" + ToRelationshipStatIdPart(asset.Id);
                string displayName = !string.IsNullOrWhiteSpace(character.characterName)
                    ? character.characterName
                    : asset.Id;
                AddRelationshipMessageOverrideIfMissing(statId, displayName);
            }
        }

        AddRelationshipMessageOverrideIfMissing("relationship:vlad", "Влад");
        AddRelationshipMessageOverrideIfMissing("relationship:alice", "Алиса");
        AddRelationshipMessageOverrideIfMissing("relationship:elison", "Элисон");
    }

    void AddRelationshipMessageOverrideIfMissing(string statId, string displayName)
    {
        if (HasRelationshipMessageOverride(statId))
            return;

        AddRelationshipMessageOverride(statId, displayName);
    }

    bool HasRelationshipMessageOverride(string statId)
    {
        if (_relationshipMessageOverrides == null || string.IsNullOrWhiteSpace(statId))
            return false;

        for (int i = 0; i < _relationshipMessageOverrides.arraySize; i++)
        {
            SerializedProperty item = _relationshipMessageOverrides.GetArrayElementAtIndex(i);
            SerializedProperty existingId = item.FindPropertyRelative("statId");
            if (existingId != null &&
                string.Equals(existingId.stringValue?.Trim(), statId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    void AddRelationshipMessageOverride(string statId, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(statId) && HasRelationshipMessageOverride(statId))
            return;

        if (string.IsNullOrWhiteSpace(statId) && HasBlankRelationshipMessageOverride())
            return;

        int index = _relationshipMessageOverrides.arraySize;
        _relationshipMessageOverrides.InsertArrayElementAtIndex(index);
        SerializedProperty item = _relationshipMessageOverrides.GetArrayElementAtIndex(index);

        item.FindPropertyRelative("statId").stringValue = statId;
        item.FindPropertyRelative("overrideTargetText").boolValue = true;
        item.FindPropertyRelative("targetText").stringValue = BuildDefaultRelationshipTargetText(displayName, statId);
        item.FindPropertyRelative("overrideImprovedText").boolValue = false;
        item.FindPropertyRelative("improvedText").stringValue = "Отношения {target} улучшились.";
        item.FindPropertyRelative("overrideWorsenedText").boolValue = false;
        item.FindPropertyRelative("worsenedText").stringValue = "Отношения {target} ухудшились.";
        item.FindPropertyRelative("overrideTextRect").boolValue = false;
        item.FindPropertyRelative("textAnchoredPosition").vector2Value = Vector2.zero;
        item.FindPropertyRelative("textSizeDelta").vector2Value = Vector2.zero;
        item.FindPropertyRelative("overrideTextColor").boolValue = false;
        item.FindPropertyRelative("textColor").colorValue = Color.white;
        item.FindPropertyRelative("overrideTextFont").boolValue = false;
        item.FindPropertyRelative("textFont").objectReferenceValue = null;
        item.FindPropertyRelative("overrideTextFontSize").boolValue = false;
        item.FindPropertyRelative("textFontSize").floatValue = 54f;
        item.FindPropertyRelative("overrideTextAutoSize").boolValue = false;
        item.FindPropertyRelative("textAutoSize").boolValue = true;
        item.FindPropertyRelative("overrideTextAutoFontSizeRange").boolValue = false;
        item.FindPropertyRelative("minAutoFontSize").floatValue = 42f;
        item.FindPropertyRelative("maxAutoFontSize").floatValue = 54f;
        item.FindPropertyRelative("overrideTextAlignment").boolValue = false;
        item.FindPropertyRelative("textAlignment").intValue = (int)TextAlignmentOptions.Center;
        item.FindPropertyRelative("overrideTextWordWrapping").boolValue = false;
        item.FindPropertyRelative("textWordWrapping").boolValue = true;
        item.FindPropertyRelative("overrideTextOverflowMode").boolValue = false;
        item.FindPropertyRelative("textOverflowMode").intValue = (int)TextOverflowModes.Overflow;
        item.FindPropertyRelative("overrideTextLineSpacing").boolValue = false;
        item.FindPropertyRelative("textLineSpacing").floatValue = 0f;
        item.FindPropertyRelative("overrideTextMargins").boolValue = false;
        item.FindPropertyRelative("textMargins").vector4Value = Vector4.zero;
        item.isExpanded = true;
    }

    bool HasBlankRelationshipMessageOverride()
    {
        if (_relationshipMessageOverrides == null)
            return false;

        for (int i = 0; i < _relationshipMessageOverrides.arraySize; i++)
        {
            SerializedProperty item = _relationshipMessageOverrides.GetArrayElementAtIndex(i);
            SerializedProperty existingId = item.FindPropertyRelative("statId");
            if (existingId != null && string.IsNullOrWhiteSpace(existingId.stringValue))
                return true;
        }

        return false;
    }

    static bool IsRelationshipStatId(string statId)
    {
        if (string.IsNullOrWhiteSpace(statId))
            return false;

        string value = statId.Trim().ToLowerInvariant();
        return value.StartsWith("relationship:", StringComparison.Ordinal) ||
               value.StartsWith("relationship_", StringComparison.Ordinal) ||
               value.StartsWith("rel:", StringComparison.Ordinal) ||
               value.StartsWith("rel_", StringComparison.Ordinal);
    }

    List<RelationshipCharacterOption> GetRelationshipCharacterOptions()
    {
        if (_relationshipCharacterOptionsCache != null)
            return _relationshipCharacterOptionsCache;

        _relationshipCharacterOptionsCache = BuildRelationshipCharacterOptionsUncached();
        return _relationshipCharacterOptionsCache;
    }

    List<RelationshipCharacterOption> BuildRelationshipCharacterOptionsUncached()
    {
        var result = new List<RelationshipCharacterOption>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        StoryUiStyle style = target as StoryUiStyle;
        if (style != null && TryFindStyleContext(style, out StyleContext context) && context.Library != null && context.Library.Assets != null)
        {
            IReadOnlyList<StoryJsonAssetReference> assets = context.Library.Assets;
            for (int i = 0; i < assets.Count; i++)
            {
                StoryJsonAssetReference asset = assets[i];
                CharacterData character = asset != null ? asset.Character : null;
                if (character == null || string.IsNullOrWhiteSpace(asset.Id))
                    continue;

                AddRelationshipCharacterOption(
                    result,
                    seenIds,
                    asset.Id,
                    !string.IsNullOrWhiteSpace(character.characterName) ? character.characterName : asset.Id);
            }
        }

        if (result.Count == 0)
        {
            AddRelationshipCharacterOption(result, seenIds, "vlad", "Влад");
            AddRelationshipCharacterOption(result, seenIds, "alice", "Алиса");
            AddRelationshipCharacterOption(result, seenIds, "elison", "Элисон");
        }

        result.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    static void AddRelationshipCharacterOption(
        List<RelationshipCharacterOption> options,
        HashSet<string> seenIds,
        string characterId,
        string displayName)
    {
        string id = string.IsNullOrWhiteSpace(characterId) ? "" : characterId.Trim();
        if (string.IsNullOrWhiteSpace(id))
            return;

        string statId = "relationship:" + ToRelationshipStatIdPart(id);
        if (!seenIds.Add(statId))
            return;

        string name = string.IsNullOrWhiteSpace(displayName) ? id : displayName.Trim();
        options.Add(new RelationshipCharacterOption
        {
            CharacterId = id,
            DisplayName = name,
            StatId = statId,
            Label = $"{name}  ({id})"
        });
    }

    static string BuildDefaultRelationshipTargetText(string displayName, string statId)
    {
        string value = !string.IsNullOrWhiteSpace(displayName)
            ? displayName.Trim()
            : ExtractRelationshipNameFromStatId(statId);

        if (string.IsNullOrWhiteSpace(value))
            value = "персонажем";

        if (value.StartsWith("с ", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("со ", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return "с " + value;
    }

    static string ExtractRelationshipNameFromStatId(string statId)
    {
        if (string.IsNullOrWhiteSpace(statId))
            return "";

        string value = statId.Trim();
        string lower = value.ToLowerInvariant();
        string[] prefixes =
        {
            "relationship:",
            "relationship_",
            "rel:",
            "rel_"
        };

        for (int i = 0; i < prefixes.Length; i++)
        {
            string prefix = prefixes[i];
            if (!lower.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            return value.Substring(prefix.Length).Replace('_', ' ').Replace('-', ' ').Trim();
        }

        return "";
    }

    static string ToRelationshipStatIdPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "character";

        string trimmed = value.Trim().ToLowerInvariant();
        var builder = new System.Text.StringBuilder(trimmed.Length);
        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                builder.Append(c);
            else if (c == '_' || c == '-' || c == ' ' || c == '.')
                builder.Append('_');
        }

        string result = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "character" : result;
    }

    void AddDefaultRelationshipIconOffsetOverrides()
    {
        Vector2 defaultOffset = _statIconOffset != null ? _statIconOffset.vector2Value : Vector2.zero;
        AddRelationshipIconOffsetOverrideIfMissing("relationship:vlad", defaultOffset);
        AddRelationshipIconOffsetOverrideIfMissing("relationship:alice", defaultOffset);
        AddRelationshipIconOffsetOverrideIfMissing("relationship:elison", defaultOffset);
    }

    void AddRelationshipIconOffsetOverrideIfMissing(string statId, Vector2 offset)
    {
        if (HasStatIconOffsetOverride(statId))
            return;

        AddStatIconOffsetOverride(statId, offset);
    }

    void DrawCopyFromSiblingButtons(
        SerializedProperty array,
        int targetIndex,
        Action<SerializedProperty, SerializedProperty> copyValues,
        string undoName)
    {
        if (array == null || copyValues == null)
            return;

        using (new EditorGUI.DisabledScope(targetIndex <= 0))
        {
            if (GUILayout.Button(new GUIContent("пред.", "Взять значения из предыдущей строки"), GUILayout.Width(50f)))
                CopyFromSiblingArrayElement(array.propertyPath, targetIndex, targetIndex - 1, copyValues, undoName);
        }

        using (new EditorGUI.DisabledScope(array.arraySize <= 1))
        {
            if (GUILayout.Button(new GUIContent("из...", "Взять значения из другой строки"), GUILayout.Width(42f)))
                ShowCopyFromSiblingMenu(array, targetIndex, copyValues, undoName);
        }
    }

    void ShowCopyFromSiblingMenu(
        SerializedProperty array,
        int targetIndex,
        Action<SerializedProperty, SerializedProperty> copyValues,
        string undoName)
    {
        GenericMenu menu = new GenericMenu();
        string arrayPath = array.propertyPath;
        for (int i = 0; i < array.arraySize; i++)
        {
            if (i == targetIndex)
                continue;

            int sourceIndex = i;
            SerializedProperty source = array.GetArrayElementAtIndex(i);
            menu.AddItem(
                new GUIContent(GetSiblingCopyLabel(source, i)),
                false,
                () => CopyFromSiblingArrayElement(arrayPath, targetIndex, sourceIndex, copyValues, undoName));
        }

        menu.ShowAsContext();
    }

    void CopyFromSiblingArrayElement(
        string arrayPath,
        int targetIndex,
        int sourceIndex,
        Action<SerializedProperty, SerializedProperty> copyValues,
        string undoName)
    {
        serializedObject.Update();
        SerializedProperty array = serializedObject.FindProperty(arrayPath);
        if (array == null ||
            sourceIndex < 0 ||
            targetIndex < 0 ||
            sourceIndex >= array.arraySize ||
            targetIndex >= array.arraySize ||
            sourceIndex == targetIndex)
        {
            return;
        }

        Undo.RecordObjects(targets, undoName);
        SerializedProperty source = array.GetArrayElementAtIndex(sourceIndex);
        SerializedProperty target = array.GetArrayElementAtIndex(targetIndex);
        copyValues(source, target);
        serializedObject.ApplyModifiedProperties();
        MarkTargetsDirty();
        if (_applyToSceneAutomatically)
            QueueApplyTargetsToOpenScene();
        Repaint();
    }

    static string GetSiblingCopyLabel(SerializedProperty item, int index)
    {
        SerializedProperty id = item.FindPropertyRelative("statId");
        if (id != null && !string.IsNullOrWhiteSpace(id.stringValue))
            return $"{index + 1}. {id.stringValue.Trim()}";

        return $"{index + 1}. Element";
    }

    static void CopyStatPanelSizeOverrideValues(SerializedProperty source, SerializedProperty target)
    {
        CopyBool(source, target, "overridePanelSize");
        CopyVector2(source, target, "panelSizeDelta");
    }

    static void CopyStatTextRectOverrideValues(SerializedProperty source, SerializedProperty target)
    {
        CopyBool(source, target, "overrideTextRect");
        CopyVector2(source, target, "textAnchoredPosition");
        CopyVector2(source, target, "textSizeDelta");
    }

    static void CopyStatIconOffsetOverrideValues(SerializedProperty source, SerializedProperty target)
    {
        CopyBool(source, target, "overrideIconOffset");
        CopyVector2(source, target, "iconOffset");
    }

    static void CopyBool(SerializedProperty source, SerializedProperty target, string propertyName)
    {
        SerializedProperty sourceProperty = source.FindPropertyRelative(propertyName);
        SerializedProperty targetProperty = target.FindPropertyRelative(propertyName);
        if (sourceProperty != null && targetProperty != null)
            targetProperty.boolValue = sourceProperty.boolValue;
    }

    static void CopyVector2(SerializedProperty source, SerializedProperty target, string propertyName)
    {
        SerializedProperty sourceProperty = source.FindPropertyRelative(propertyName);
        SerializedProperty targetProperty = target.FindPropertyRelative(propertyName);
        if (sourceProperty != null && targetProperty != null)
            targetProperty.vector2Value = sourceProperty.vector2Value;
    }

    static void CopyStatOverrideToClipboard(SerializedProperty item, string title, params string[] propertyNames)
    {
        if (item == null)
            return;

        var builder = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(title))
            builder.AppendLine(title);

        for (int i = 0; i < propertyNames.Length; i++)
            AppendClipboardProperty(builder, propertyNames[i], item.FindPropertyRelative(propertyNames[i]));

        EditorGUIUtility.systemCopyBuffer = builder.ToString().TrimEnd();
    }

    static void AppendClipboardProperty(System.Text.StringBuilder builder, string label, SerializedProperty property)
    {
        if (builder == null || property == null)
            return;

        builder.Append(label);
        builder.Append(": ");
        builder.Append(FormatClipboardPropertyValue(property));
        builder.AppendLine();
    }

    static string FormatClipboardPropertyValue(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Float:
                return property.floatValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            case SerializedPropertyType.String:
                return property.stringValue ?? "";
            case SerializedPropertyType.Vector2:
                Vector2 value = property.vector2Value;
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0:0.###}, {1:0.###}",
                    value.x,
                    value.y);
            case SerializedPropertyType.Enum:
                return property.enumDisplayNames != null &&
                    property.enumValueIndex >= 0 &&
                    property.enumValueIndex < property.enumDisplayNames.Length
                        ? property.enumDisplayNames[property.enumValueIndex]
                        : property.enumValueIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            default:
                return property.propertyType.ToString();
        }
    }

    static string GetStatIdOrFallback(SerializedProperty statId, string fallback)
    {
        return statId != null && !string.IsNullOrWhiteSpace(statId.stringValue)
            ? statId.stringValue.Trim()
            : fallback;
    }

    static string FormatVector2Summary(Vector2 value)
    {
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0:0.###} x {1:0.###}",
            value.x,
            value.y);
    }

    static string BuildStatPanelSizeTitle(
        SerializedProperty statId,
        SerializedProperty overridePanelSize,
        SerializedProperty panelSizeDelta,
        int index)
    {
        string title = GetStatIdOrFallback(statId, $"Stat {index + 1}");
        if (overridePanelSize != null && overridePanelSize.boolValue && panelSizeDelta != null)
            return $"{title} | size {FormatVector2Summary(panelSizeDelta.vector2Value)}";

        return $"{title} | общий size";
    }

    static string BuildStatTextRectTitle(
        SerializedProperty statId,
        SerializedProperty overrideTextRect,
        SerializedProperty textAnchoredPosition,
        SerializedProperty textSizeDelta,
        int index)
    {
        string title = GetStatIdOrFallback(statId, $"Stat {index + 1}");
        if (overrideTextRect != null && overrideTextRect.boolValue)
        {
            string position = textAnchoredPosition != null ? FormatVector2Summary(textAnchoredPosition.vector2Value) : "-";
            string size = textSizeDelta != null ? FormatVector2Summary(textSizeDelta.vector2Value) : "-";
            return $"{title} | pos {position} | size {size}";
        }

        return $"{title} | общий rect";
    }

    static string BuildStatIconOffsetTitle(
        SerializedProperty statId,
        SerializedProperty overrideIconOffset,
        SerializedProperty iconOffset,
        int index)
    {
        string title = GetStatIdOrFallback(statId, $"Стат {index + 1}");
        if (overrideIconOffset != null && overrideIconOffset.boolValue && iconOffset != null)
            return $"{title} | offset {FormatVector2Summary(iconOffset.vector2Value)}";

        return $"{title} | общий offset";
    }

    void DrawStatPanelSizeOverrides()
    {
        if (_statPanelSizeOverrides == null)
            return;

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Panel size by statId", EditorStyles.boldLabel);
                if (GUILayout.Button("from stats", GUILayout.Width(100f)))
                    AddMissingStatPanelSizeOverridesFromDefinitions();
                if (GUILayout.Button("+", GUILayout.Width(30f)))
                    AddStatPanelSizeOverride("", _statPanelSizeDelta != null ? _statPanelSizeDelta.vector2Value : new Vector2(1000f, 140f));
            }

            for (int i = 0; i < _statPanelSizeOverrides.arraySize; i++)
            {
                SerializedProperty item = _statPanelSizeOverrides.GetArrayElementAtIndex(i);
                SerializedProperty statId = item.FindPropertyRelative("statId");
                SerializedProperty overridePanelSize = item.FindPropertyRelative("overridePanelSize");
                SerializedProperty panelSizeDelta = item.FindPropertyRelative("panelSizeDelta");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        item.isExpanded = EditorGUILayout.Foldout(
                            item.isExpanded,
                            BuildStatPanelSizeTitle(statId, overridePanelSize, panelSizeDelta, i),
                            true);
                        if (GUILayout.Button(new GUIContent("коп.", "Скопировать значения этой строки в буфер"), GUILayout.Width(40f)))
                            CopyStatOverrideToClipboard(
                                item,
                                "Stat panel size",
                                "statId",
                                "overridePanelSize",
                                "panelSizeDelta");
                        if (GUILayout.Button(new GUIContent("общ.", "Взять размер из общего Rect плашки статов"), GUILayout.Width(50f)) && _statPanelSizeDelta != null)
                        {
                            overridePanelSize.boolValue = true;
                            panelSizeDelta.vector2Value = _statPanelSizeDelta.vector2Value;
                        }
                        DrawCopyFromSiblingButtons(
                            _statPanelSizeOverrides,
                            i,
                            CopyStatPanelSizeOverrideValues,
                            "Copy Stat Panel Size Override");
                        if (GUILayout.Button("x", GUILayout.Width(24f)))
                        {
                            _statPanelSizeOverrides.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    if (item.isExpanded)
                    {
                        EditorGUILayout.PropertyField(statId, new GUIContent("Stat ID"));
                        DrawOverridePair(overridePanelSize, panelSizeDelta, "Override panel size", "Size");
                    }
                }
            }
        }
    }

    void AddMissingStatPanelSizeOverridesFromDefinitions()
    {
        if (_statPanelSizeOverrides == null)
            return;

        Vector2 defaultSize = _statPanelSizeDelta != null ? _statPanelSizeDelta.vector2Value : new Vector2(1000f, 140f);
        foreach (string statId in CollectStatIdsFromDefinitions())
        {
            if (!HasStatPanelSizeOverride(statId))
                AddStatPanelSizeOverride(statId, defaultSize);
        }
    }

    HashSet<string> CollectStatIdsFromDefinitions()
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_statOverlayDefinitions != null)
        {
            for (int i = 0; i < _statOverlayDefinitions.arraySize; i++)
            {
                SerializedProperty item = _statOverlayDefinitions.GetArrayElementAtIndex(i);
                SerializedProperty statId = item.FindPropertyRelative("statId");
                if (statId != null && !string.IsNullOrWhiteSpace(statId.stringValue))
                    ids.Add(statId.stringValue.Trim());
            }
        }

        if (_statDefinitionAssets != null)
        {
            for (int i = 0; i < _statDefinitionAssets.arraySize; i++)
            {
                StatDefinition definition = _statDefinitionAssets.GetArrayElementAtIndex(i).objectReferenceValue as StatDefinition;
                if (definition != null && !string.IsNullOrWhiteSpace(definition.statId))
                    ids.Add(definition.statId.Trim());
            }
        }

        return ids;
    }

    bool HasStatPanelSizeOverride(string statId)
    {
        if (_statPanelSizeOverrides == null || string.IsNullOrWhiteSpace(statId))
            return false;

        for (int i = 0; i < _statPanelSizeOverrides.arraySize; i++)
        {
            SerializedProperty item = _statPanelSizeOverrides.GetArrayElementAtIndex(i);
            SerializedProperty existingId = item.FindPropertyRelative("statId");
            if (existingId != null &&
                StoryStatId.EqualsCanonical(existingId.stringValue, statId))
            {
                return true;
            }
        }

        return false;
    }

    void AddStatPanelSizeOverride(string statId, Vector2 size)
    {
        int index = _statPanelSizeOverrides.arraySize;
        _statPanelSizeOverrides.InsertArrayElementAtIndex(index);

        SerializedProperty item = _statPanelSizeOverrides.GetArrayElementAtIndex(index);
        item.FindPropertyRelative("statId").stringValue = statId ?? "";
        item.FindPropertyRelative("overridePanelSize").boolValue = true;
        item.FindPropertyRelative("panelSizeDelta").vector2Value = size;
        item.isExpanded = true;
    }

    void DrawStatTextRectOverrides()
    {
        if (_statTextRectOverrides == null)
            return;

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Rect текста по statId", EditorStyles.boldLabel);
                if (GUILayout.Button("из статов", GUILayout.Width(100f)))
                    AddMissingStatTextRectOverridesFromDefinitions();
                if (GUILayout.Button("+", GUILayout.Width(30f)))
                    AddStatTextRectOverride(
                        "",
                        _statTextAnchoredPosition != null ? _statTextAnchoredPosition.vector2Value : Vector2.zero,
                        _statTextSizeDelta != null ? _statTextSizeDelta.vector2Value : new Vector2(760f, 96f));
            }

            for (int i = 0; i < _statTextRectOverrides.arraySize; i++)
            {
                SerializedProperty item = _statTextRectOverrides.GetArrayElementAtIndex(i);
                SerializedProperty statId = item.FindPropertyRelative("statId");
                SerializedProperty overrideTextRect = item.FindPropertyRelative("overrideTextRect");
                SerializedProperty textAnchoredPosition = item.FindPropertyRelative("textAnchoredPosition");
                SerializedProperty textSizeDelta = item.FindPropertyRelative("textSizeDelta");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        item.isExpanded = EditorGUILayout.Foldout(
                            item.isExpanded,
                            BuildStatTextRectTitle(statId, overrideTextRect, textAnchoredPosition, textSizeDelta, i),
                            true);
                        if (GUILayout.Button(new GUIContent("коп.", "Скопировать значения этой строки в буфер"), GUILayout.Width(40f)))
                            CopyStatOverrideToClipboard(
                                item,
                                "Stat text rect",
                                "statId",
                                "overrideTextRect",
                                "textAnchoredPosition",
                                "textSizeDelta");
                        if (GUILayout.Button(new GUIContent("общ.", "Взять позицию и размер из общего Rect текста"), GUILayout.Width(50f)))
                        {
                            overrideTextRect.boolValue = true;
                            if (_statTextAnchoredPosition != null)
                                textAnchoredPosition.vector2Value = _statTextAnchoredPosition.vector2Value;
                            if (_statTextSizeDelta != null)
                                textSizeDelta.vector2Value = _statTextSizeDelta.vector2Value;
                        }
                        DrawCopyFromSiblingButtons(
                            _statTextRectOverrides,
                            i,
                            CopyStatTextRectOverrideValues,
                            "Copy Stat Text Rect Override");
                        if (GUILayout.Button("x", GUILayout.Width(24f)))
                        {
                            _statTextRectOverrides.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    if (item.isExpanded)
                    {
                        EditorGUILayout.PropertyField(statId, new GUIContent("Stat ID"));
                        DrawVector2Pair(
                            overrideTextRect,
                            textAnchoredPosition,
                            textSizeDelta,
                            "Rect текста",
                            "Позиция",
                            "Размер",
                            true);
                    }
                }
            }
        }
    }

    void AddMissingStatTextRectOverridesFromDefinitions()
    {
        if (_statTextRectOverrides == null)
            return;

        Vector2 defaultPosition = _statTextAnchoredPosition != null ? _statTextAnchoredPosition.vector2Value : Vector2.zero;
        Vector2 defaultSize = _statTextSizeDelta != null ? _statTextSizeDelta.vector2Value : new Vector2(760f, 96f);
        foreach (string statId in CollectStatIdsFromDefinitions())
        {
            if (!HasStatTextRectOverride(statId))
                AddStatTextRectOverride(statId, defaultPosition, defaultSize);
        }
    }

    bool HasStatTextRectOverride(string statId)
    {
        if (_statTextRectOverrides == null || string.IsNullOrWhiteSpace(statId))
            return false;

        for (int i = 0; i < _statTextRectOverrides.arraySize; i++)
        {
            SerializedProperty item = _statTextRectOverrides.GetArrayElementAtIndex(i);
            SerializedProperty existingId = item.FindPropertyRelative("statId");
            if (existingId != null &&
                StoryStatId.EqualsCanonical(existingId.stringValue, statId))
            {
                return true;
            }
        }

        return false;
    }

    void AddStatTextRectOverride(string statId, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        int index = _statTextRectOverrides.arraySize;
        _statTextRectOverrides.InsertArrayElementAtIndex(index);

        SerializedProperty item = _statTextRectOverrides.GetArrayElementAtIndex(index);
        item.FindPropertyRelative("statId").stringValue = statId ?? "";
        item.FindPropertyRelative("overrideTextRect").boolValue = true;
        item.FindPropertyRelative("textAnchoredPosition").vector2Value = anchoredPosition;
        item.FindPropertyRelative("textSizeDelta").vector2Value = sizeDelta;
        item.isExpanded = true;
    }

    void DrawStatIconOffsetOverrides()
    {
        if (_statIconOffsetOverrides == null)
            return;

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Offset по отдельным statId", EditorStyles.boldLabel);
                if (GUILayout.Button("из списка статов", GUILayout.Width(120f)))
                    AddMissingStatIconOffsetOverridesFromDefinitions();
                if (GUILayout.Button("+", GUILayout.Width(30f)))
                    AddStatIconOffsetOverride("", _statIconOffset != null ? _statIconOffset.vector2Value : Vector2.zero);
            }

            if (_showHints)
            {
                EditorGUILayout.HelpBox(
                    "Эти значения переопределяют общий Offset иконки только для указанного statId. Так можно отдельно подогнать, например, respect и city.",
                    MessageType.None);
            }

            for (int i = 0; i < _statIconOffsetOverrides.arraySize; i++)
            {
                SerializedProperty item = _statIconOffsetOverrides.GetArrayElementAtIndex(i);
                SerializedProperty statId = item.FindPropertyRelative("statId");
                SerializedProperty overrideIconOffset = item.FindPropertyRelative("overrideIconOffset");
                SerializedProperty iconOffset = item.FindPropertyRelative("iconOffset");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        item.isExpanded = EditorGUILayout.Foldout(
                            item.isExpanded,
                            BuildStatIconOffsetTitle(statId, overrideIconOffset, iconOffset, i),
                            true);
                        if (GUILayout.Button(new GUIContent("коп.", "Скопировать значения этой строки в буфер"), GUILayout.Width(40f)))
                            CopyStatOverrideToClipboard(
                                item,
                                "Stat icon offset",
                                "statId",
                                "overrideIconOffset",
                                "iconOffset");
                        if (GUILayout.Button(new GUIContent("общ.", "Взять offset из общего значения иконки"), GUILayout.Width(50f)) && _statIconOffset != null)
                        {
                            overrideIconOffset.boolValue = true;
                            iconOffset.vector2Value = _statIconOffset.vector2Value;
                        }
                        DrawCopyFromSiblingButtons(
                            _statIconOffsetOverrides,
                            i,
                            CopyStatIconOffsetOverrideValues,
                            "Copy Stat Icon Offset Override");
                        if (GUILayout.Button("↑", GUILayout.Width(24f)) && i > 0)
                            _statIconOffsetOverrides.MoveArrayElement(i, i - 1);
                        if (GUILayout.Button("↓", GUILayout.Width(24f)) && i < _statIconOffsetOverrides.arraySize - 1)
                            _statIconOffsetOverrides.MoveArrayElement(i, i + 1);
                        if (GUILayout.Button("x", GUILayout.Width(24f)))
                        {
                            _statIconOffsetOverrides.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    if (item.isExpanded)
                    {
                        EditorGUILayout.PropertyField(statId, new GUIContent("ID стата / отношения"));
                        EditorGUILayout.PropertyField(overrideIconOffset, new GUIContent("Использовать offset"));
                        using (new EditorGUI.DisabledScope(!overrideIconOffset.boolValue))
                            EditorGUILayout.PropertyField(iconOffset, new GUIContent("Offset"));
                    }
                }
            }
        }
    }

    void AddMissingStatIconOffsetOverridesFromDefinitions()
    {
        if (_statIconOffsetOverrides == null)
            return;

        Vector2 defaultOffset = _statIconOffset != null ? _statIconOffset.vector2Value : Vector2.zero;
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_statOverlayDefinitions != null)
        {
            for (int i = 0; i < _statOverlayDefinitions.arraySize; i++)
            {
                SerializedProperty item = _statOverlayDefinitions.GetArrayElementAtIndex(i);
                SerializedProperty statId = item.FindPropertyRelative("statId");
                if (statId != null && !string.IsNullOrWhiteSpace(statId.stringValue))
                    ids.Add(statId.stringValue.Trim());
            }
        }

        if (_statDefinitionAssets != null)
        {
            for (int i = 0; i < _statDefinitionAssets.arraySize; i++)
            {
                StatDefinition definition = _statDefinitionAssets.GetArrayElementAtIndex(i).objectReferenceValue as StatDefinition;
                if (definition != null && !string.IsNullOrWhiteSpace(definition.statId))
                    ids.Add(definition.statId.Trim());
            }
        }

        foreach (string statId in ids)
        {
            if (!HasStatIconOffsetOverride(statId))
                AddStatIconOffsetOverride(statId, defaultOffset);
        }
    }

    bool HasStatIconOffsetOverride(string statId)
    {
        if (_statIconOffsetOverrides == null || string.IsNullOrWhiteSpace(statId))
            return false;

        for (int i = 0; i < _statIconOffsetOverrides.arraySize; i++)
        {
            SerializedProperty item = _statIconOffsetOverrides.GetArrayElementAtIndex(i);
            SerializedProperty existingId = item.FindPropertyRelative("statId");
            if (existingId != null &&
                StoryStatId.EqualsCanonical(existingId.stringValue, statId))
            {
                return true;
            }
        }

        return false;
    }

    void AddStatIconOffsetOverride(string statId, Vector2 offset)
    {
        int index = _statIconOffsetOverrides.arraySize;
        _statIconOffsetOverrides.InsertArrayElementAtIndex(index);

        SerializedProperty item = _statIconOffsetOverrides.GetArrayElementAtIndex(index);
        item.FindPropertyRelative("statId").stringValue = statId ?? "";
        item.FindPropertyRelative("overrideIconOffset").boolValue = true;
        item.FindPropertyRelative("iconOffset").vector2Value = offset;
        item.isExpanded = true;
    }

    void DrawChapterSection()
    {
        DrawApplyOnlySpritesToggle(_chapterApplyOnlySprites, "главы");
        if (!DrawFoldout(ref _showChapter, "Заголовок главы"))
            return;

        DrawSubTabBar(ref _chapterTab, ChapterTabLabels);

        switch (_chapterTab)
        {
            case ChapterTab.Panel:
                DrawGroupTitle("Плашка главы", "Sprite, цвет, Image Type и текст на самой плашке главы.");
                DrawSpriteColumnHeader();
                DrawSpriteRow("Sprite плашки", _chapterTitlePanelSprite, _chapterTitlePanelSpriteSource, null, _overrideChapterTitlePanelColor, _chapterTitlePanelColor, _overrideChapterTitlePanelImageType, _chapterTitlePanelImageType, Image.Type.Sliced);
                DrawOverridePair(_overrideChapterTitlePanelColor, _chapterTitlePanelColor, "Цвет плашки", "Цвет");
                DrawOverridePair(_overrideChapterTitlePanelImageType, _chapterTitlePanelImageType, "Image Type плашки", "Type");
                DrawOverridePair(_overrideChapterTitleTextColor, _chapterTitleTextColor, "Цвет текста", "Цвет");
                DrawFontOverridePair(_overrideChapterTitleTextFont, _chapterTitleTextFont, "Override title font", "Font");
                DrawOverridePair(_overrideChapterTitleTextFontSize, _chapterTitleTextFontSize, "Размер текста", "Размер");
                break;

            case ChapterTab.Position:
                DrawGroupTitle("Положение и затемнение", "Центровка, порядок поверх UI и фон затемнения.");
                DrawOverridePair(_overrideChapterTitleCenterOnShow, _chapterTitleCenterOnShow, "Center On Show", "Center");
                DrawOverridePair(_overrideChapterTitleBringToFrontOnShow, _chapterTitleBringToFrontOnShow, "Bring To Front", "Bring To Front");
                DrawOverridePair(_overrideChapterTitleBackgroundDimSizeMode, _chapterTitleBackgroundDimSizeMode, "Режим затемнения", "Mode");
                DrawOverridePair(_overrideChapterTitleBackgroundDimFixedSize, _chapterTitleBackgroundDimFixedSize, "Размер затемнения", "Размер");
                DrawOverridePair(_overrideChapterTitleBackgroundDimColor, _chapterTitleBackgroundDimColor, "Цвет затемнения", "Цвет");
                DrawOverridePair(_overrideChapterTitleBackgroundDimAlpha, _chapterTitleBackgroundDimAlpha, "Сила затемнения", "Alpha");
                break;

            case ChapterTab.Text:
                DrawGroupTitle("Текст главы", "Формат строки, номер главы и обработка пустого названия.");
                DrawVector2Pair(
                    _overrideChapterTitleTextRect,
                    _chapterTitleTextAnchoredPosition,
                    _chapterTitleTextSizeDelta,
                    "Rect / offset текста",
                    "Позиция",
                    "Размер");
                DrawMinMaxPair(
                    _overrideChapterTitleTextHeightLimits,
                    _chapterTitleTextMinHeight,
                    _chapterTitleTextMaxHeight,
                    "Min/Max height текста",
                    "Min",
                    "Max");
                DrawOverridePair(_overrideChapterTitleTextColor, _chapterTitleTextColor, "Цвет текста", "Цвет");
                DrawFontOverridePair(_overrideChapterTitleTextFont, _chapterTitleTextFont, "Override title font", "Font");
                DrawOverridePair(_overrideChapterTitleTextFontSize, _chapterTitleTextFontSize, "Размер текста", "Размер");
                DrawOverridePair(_overrideChapterTitleTextAutoSize, _chapterTitleTextAutoSize, "TMP Auto Size", "Auto");
                DrawMinMaxPair(
                    _overrideChapterTitleTextAutoFontSizeRange,
                    _chapterTitleTextMinAutoFontSize,
                    _chapterTitleTextMaxAutoFontSize,
                    "Auto Size min/max",
                    "Min",
                    "Max");
                DrawOverridePair(_overrideChapterTitleTextAlignment, _chapterTitleTextAlignment, "Alignment", "Alignment");
                DrawOverridePair(_overrideChapterTitleTextWordWrapping, _chapterTitleTextWordWrapping, "Word Wrapping", "Wrap");
                DrawOverridePair(_overrideChapterTitleTextOverflowMode, _chapterTitleTextOverflowMode, "Overflow", "Mode");
                DrawOverridePair(_overrideChapterTitleTextLineSpacing, _chapterTitleTextLineSpacing, "Line Spacing", "Spacing");
                DrawOverridePair(_overrideChapterTitleTextMargins, _chapterTitleTextMargins, "Margins", "Margins");
                EditorGUILayout.Space(8f);
                DrawOverridePair(_overrideChapterTitleTextMode, _chapterTitleTextMode, "Text Mode", "Mode");
                DrawOverridePair(_overrideChapterTitleTextFormat, _chapterTitleTextFormat, "Text Format", "Format");
                DrawOverridePair(_overrideChapterTitleNumberAndTitleFormat, _chapterTitleNumberAndTitleFormat, "Number And Title Format", "Format");
                DrawOverridePair(_overrideChapterTitleNumberOffset, _chapterTitleNumberOffset, "Chapter Number Offset", "Offset");
                DrawOverridePair(_overrideChapterTitleEmptyTitleFallback, _chapterTitleEmptyTitleFallback, "Empty Title Fallback", "Fallback");
                DrawOverridePair(_overrideChapterTitleTrimTitle, _chapterTitleTrimTitle, "Trim Title", "Trim");
                DrawOverridePair(_overrideChapterTitleUppercaseTitle, _chapterTitleUppercaseTitle, "Uppercase Title", "Uppercase");
                break;

            case ChapterTab.Padding:
                DrawGroupTitle("Особый padding", "Отдельный padding для названий, которые не помещаются в обычную плашку.");
                EditorGUILayout.PropertyField(_overrideChapterTitleSpecificPaddingSettings, new GUIContent("Включить настройки padding"));
                using (new EditorGUI.DisabledScope(!_overrideChapterTitleSpecificPaddingSettings.boolValue))
                {
                    EditorGUILayout.PropertyField(_chapterTitleUseSpecificPadding, new GUIContent("Use Specific Title Padding"));
                    EditorGUILayout.PropertyField(_chapterTitleSpecificPaddingMarkers, new GUIContent("Specific Title Padding Markers"), true);
                    EditorGUILayout.PropertyField(_chapterTitleSpecificPadding, new GUIContent("Specific Title Padding"));
                }
                break;

            case ChapterTab.Motion:
                DrawGroupTitle("Движение", "Анимация появления, длительности и скрытие root после выхода.");
                DrawOverridePair(_overrideChapterTitleAnimationMode, _chapterTitleAnimationMode, "Animation Mode", "Mode");
                DrawOverridePair(_overrideChapterTitleShownPosition, _chapterTitleShownPosition, "Shown Position", "Position");
                DrawOverridePair(_overrideChapterTitleCaptureShownPositionOnAwake, _chapterTitleCaptureShownPositionOnAwake, "Capture Position", "Capture");
                DrawOverridePair(_overrideChapterTitleHiddenOffsetY, _chapterTitleHiddenOffsetY, "Hidden Offset Y", "Offset Y");
                DrawOverridePair(_overrideChapterTitleEnterDuration, _chapterTitleEnterDuration, "Enter Duration", "Seconds");
                DrawOverridePair(_overrideChapterTitleVisibleDuration, _chapterTitleVisibleDuration, "Visible Duration", "Seconds");
                DrawOverridePair(_overrideChapterTitleExitDuration, _chapterTitleExitDuration, "Exit Duration", "Seconds");
                DrawOverridePair(_overrideChapterTitleFadeWithMovement, _chapterTitleFadeWithMovement, "Fade With Movement", "Fade");
                DrawOverridePair(_overrideChapterTitleAnimatePosition, _chapterTitleAnimatePosition, "Animate Position", "Animate");
                DrawOverridePair(_overrideChapterTitleUseUnscaledTime, _chapterTitleUseUnscaledTime, "Use Unscaled Time", "Unscaled");
                DrawOverridePair(_overrideChapterTitleDisableRootAfterExit, _chapterTitleDisableRootAfterExit, "Disable Root After Exit", "Disable");
                break;
        }
    }

    void DrawEndScreenSection()
    {
        if (DrawEndScreenStyleSection())
            return;

        DrawGroupTitle(
            "Финал",
            "Отдельный экран завершения истории: ссылки сцены, безопасный Edit Mode preview, реальные runtime-статы, фон, layout и кнопка возврата в меню.");

        StoryEndScreenController endScreen = FindSceneEndScreen();
        StoryManager storyManager = FindSceneStoryManager();

        if (endScreen == null)
        {
            EditorGUILayout.HelpBox(
                "В открытой сцене не найден StoryEndScreenController. Добавь его на endStoryPanel, чтобы предпросмотр финального экрана работал без Play Mode.",
                MessageType.Warning);

            using (new EditorGUI.DisabledScope(storyManager == null || storyManager.endStoryPanel == null))
            {
                if (GUILayout.Button("Добавить StoryEndScreenController на endStoryPanel", GUILayout.Height(28f)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(storyManager.endStoryPanel, "Add Story End Screen Controller");
                    endScreen = storyManager.endStoryPanel.GetComponent<StoryEndScreenController>();
                    if (endScreen == null)
                        endScreen = storyManager.endStoryPanel.AddComponent<StoryEndScreenController>();
                    endScreen.AutoFillEndScreenReferencesFromHierarchy();
                    EditorUtility.SetDirty(endScreen);
                    Repaint();
                }
            }

            if (storyManager == null || storyManager.endStoryPanel == null)
                EditorGUILayout.HelpBox("StoryManager/endStoryPanel пока не назначены в сцене.", MessageType.Info);

            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.ObjectField("StoryEndScreenController", endScreen, typeof(StoryEndScreenController), true);

            SerializedObject endObject = new SerializedObject(endScreen);
            endObject.Update();
            DrawEndScreenSerializedProperty(endObject, "_references", "Ссылки финального экрана");
            DrawEndScreenSerializedProperty(endObject, "_layoutSettings", "Layout и safe area");
            DrawEndScreenSerializedProperty(endObject, "_previewSettings", "Настройки preview");
            StoryUiStyle currentStyle = target as StoryUiStyle;
            string currentStoryId = TryFindStyleContext(currentStyle, out StyleContext styleContext)
                ? styleContext.StoryId
                : "";
            DrawEndScreenStatBindings(endObject, "_statBindings", "Биндинги статов", endScreen, currentStoryId);
            bool autoRefsChanged = AutoFillEmptyEndScreenStatBindings(endObject, endScreen);
            bool changed = endObject.ApplyModifiedProperties();
            if (changed || autoRefsChanged)
            {
                EditorUtility.SetDirty(endScreen);
                RefreshEndScreenInspectorLivePreview(endScreen, "StoryUiStyleEditorChanged");
            }

            StoryEndScreenValidationResult validation = endScreen.ValidateEndScreen(requireRuntime: false);
            DrawEndScreenValidationResult(validation);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Автозаполнить ссылки", GUILayout.Height(26f)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(endScreen.gameObject, "Auto Fill End Screen References");
                    endScreen.AutoFillEndScreenReferencesFromHierarchy();
                    RefreshEndScreenInspectorLivePreview(endScreen, "StoryUiStyleEditorAutoFill");
                    EditorUtility.SetDirty(endScreen);
                    Repaint();
                }

                if (GUILayout.Button("Проверить", GUILayout.Height(26f)))
                    ShowEndScreenValidationDialog(endScreen);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Считать визуалы из сцены", GUILayout.Height(26f)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(endScreen.gameObject, "Capture End Screen Stat Visuals");
                    endScreen.CaptureCurrentStatVisualSprites(overwriteExisting: true);
                    RefreshEndScreenInspectorLivePreview(endScreen, "StoryUiStyleEditorCaptureVisuals");
                    EditorUtility.SetDirty(endScreen);
                    Repaint();
                }

                if (GUILayout.Button("Применить визуалы", GUILayout.Height(26f)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(endScreen.gameObject, "Apply End Screen Stat Visuals");
                    RefreshEndScreenInspectorLivePreview(endScreen, "StoryUiStyleEditorApplyVisuals");
                    EditorUtility.SetDirty(endScreen);
                    Repaint();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Показать preview", GUILayout.Height(26f)))
                    RenderEndScreenInspectorPreview(endScreen);

                if (GUILayout.Button("Скрыть preview", GUILayout.Height(26f)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(endScreen.gameObject, "Hide End Screen Preview");
                    endScreen.Hide();
                    EditorUtility.SetDirty(endScreen);
                    SceneView.RepaintAll();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Пересчитать layout", GUILayout.Height(26f)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(endScreen.gameObject, "Recalculate End Screen Layout");
                    endScreen.RecalculateLayout("StoryUiStyleEditorManual");
                    EditorUtility.SetDirty(endScreen);
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Открыть объект", GUILayout.Height(26f)))
                {
                    Selection.activeGameObject = endScreen.gameObject;
                    EditorGUIUtility.PingObject(endScreen.gameObject);
                }
            }
        }
    }

    bool DrawEndScreenStyleSection()
    {
        SerializedProperty endStyle = serializedObject.FindProperty("_endScreenStyle");
        if (endStyle == null)
            return false;

        DrawGroupTitle(
            "Финал",
            "Override финального экрана хранится в этом StoryUiStyle. Сцена хранит только ссылки, куда применять картинки, шрифты и размеры.");

        StoryEndScreenController endScreen = FindSceneEndScreen();
        StoryManager storyManager = FindSceneStoryManager();
        StoryUiStyle currentStyle = target as StoryUiStyle;
        string currentStoryId = TryFindStyleContext(currentStyle, out StyleContext styleContext)
            ? styleContext.StoryId
            : "";

        if (endScreen == null)
        {
            EditorGUILayout.HelpBox(
                "В сцене не найден StoryEndScreenController. Override можно заполнить, но live preview появится только после назначения контроллера финального экрана.",
                MessageType.Warning);

            using (new EditorGUI.DisabledScope(storyManager == null || storyManager.endStoryPanel == null))
            {
                if (GUILayout.Button("Добавить StoryEndScreenController на endStoryPanel", GUILayout.Height(28f)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(storyManager.endStoryPanel, "Add Story End Screen Controller");
                    endScreen = storyManager.endStoryPanel.GetComponent<StoryEndScreenController>();
                    if (endScreen == null)
                        endScreen = storyManager.endStoryPanel.AddComponent<StoryEndScreenController>();
                    endScreen.AutoFillEndScreenReferencesFromHierarchy();
                    EditorUtility.SetDirty(endScreen);
                    Repaint();
                }
            }
        }

        EditorGUI.BeginChangeCheck();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.ObjectField("StoryEndScreenController", endScreen, typeof(StoryEndScreenController), true);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(endScreen == null))
                {
                    if (GUILayout.Button("Показать preview", GUILayout.Height(26f)))
                        RefreshEndScreenStyleLivePreview(endScreen, currentStyle, currentStoryId, "StoryUiStyleEditorPreview");

                    if (GUILayout.Button("Скрыть preview", GUILayout.Height(26f)))
                    {
                        endScreen.Hide();
                        SceneView.RepaintAll();
                    }

                    if (GUILayout.Button("Автозаполнить ссылки сцены", GUILayout.Height(26f)))
                    {
                        Undo.RegisterFullObjectHierarchyUndo(endScreen.gameObject, "Auto Fill End Screen References");
                        endScreen.AutoFillEndScreenReferencesFromHierarchy();
                        EditorUtility.SetDirty(endScreen);
                        RefreshEndScreenStyleLivePreview(endScreen, currentStyle, currentStoryId, "StoryUiStyleEditorAutoFill");
                    }

                    if (GUILayout.Button("Проверить", GUILayout.Height(26f)))
                        ShowEndScreenValidationDialog(endScreen);
                }
            }
        }

        StoryEndScreenReferences refs = endScreen != null ? endScreen.References : null;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Фон", EditorStyles.boldLabel);
            DrawEndScreenStyleSpritePair(
                "Фон экрана",
                refs != null ? refs.backgroundImage : null,
                "Background Image",
                endStyle.FindPropertyRelative("_backgroundSprite"),
                endStyle.FindPropertyRelative("_backgroundSpriteSource"));
            DrawEndScreenStyleSpritePair(
                "Фон блока статов",
                refs != null ? refs.statsBackgroundImage : null,
                "Stats Background Image",
                endStyle.FindPropertyRelative("_statsBackgroundSprite"),
                endStyle.FindPropertyRelative("_statsBackgroundSpriteSource"));
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Тексты", EditorStyles.boldLabel);
            DrawEndScreenTextStyleBlock("Header / Title", refs != null ? refs.titleText : null, endStyle.FindPropertyRelative("_titleTextStyle"));
            DrawEndScreenTextStyleBlock("Story Title", refs != null ? refs.storyTitleText : null, endStyle.FindPropertyRelative("_storyTitleTextStyle"));
            DrawEndScreenTextStyleBlock("Completed Episode", refs != null ? refs.completedEpisodeText : null, endStyle.FindPropertyRelative("_completedEpisodeTextStyle"));
            DrawEndScreenTextStyleBlock("Next Episode", refs != null ? refs.nextEpisodeText : null, endStyle.FindPropertyRelative("_nextEpisodeTextStyle"));
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Кнопка", EditorStyles.boldLabel);
            DrawReadonlyObjectField("Continue Button", refs != null ? refs.continueButton : null, typeof(Button));
            DrawReadonlyObjectField("Текст кнопки", refs != null ? refs.continueButtonText : null, typeof(TMP_Text));
            DrawEndScreenStyleSpritePair(
                "Плашка кнопки",
                refs != null ? refs.continueButtonPlateImage : null,
                "Image в сцене",
                endStyle.FindPropertyRelative("_continueButtonPlateSprite"),
                endStyle.FindPropertyRelative("_continueButtonPlateSpriteSource"));
            DrawEndScreenTextStyleBlock("Текст кнопки", refs != null ? refs.continueButtonText : null, endStyle.FindPropertyRelative("_continueButtonTextStyle"));
        }

        DrawEndScreenStyleStatBindings(
            endStyle.FindPropertyRelative("_statBindings"),
            endScreen,
            currentStoryId,
            currentStyle);

        bool changed = EditorGUI.EndChangeCheck();
        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            RefreshEndScreenStyleLivePreview(endScreen, currentStyle, currentStoryId, "StoryUiStyleEditorChanged");
        }

        return true;
    }

    static void DrawEndScreenStyleSpritePair(
        string title,
        UnityEngine.Object sceneReference,
        string sceneLabel,
        SerializedProperty spriteProperty,
        SerializedProperty sourceProperty)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            DrawReadonlyObjectField("Ссылка в сцене: " + sceneLabel, sceneReference, sceneReference != null ? sceneReference.GetType() : typeof(UnityEngine.Object));
            DrawSpriteRow("Override этой истории", spriteProperty, sourceProperty, null);
            if (spriteProperty != null && spriteProperty.objectReferenceValue != null && sceneReference == null)
            {
                EditorGUILayout.HelpBox(
                    "Override задан, но ссылка в сцене пустая. Назначь scene Image в StoryEndScreenController или нажми автозаполнение ссылок сцены.",
                    MessageType.Warning);
            }
        }
    }

    static void DrawReadonlyObjectField(string label, UnityEngine.Object value, Type type)
    {
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField(label, value, type ?? typeof(UnityEngine.Object), true);
    }

    void DrawEndScreenTextStyleBlock(string title, TMP_Text sceneText, SerializedProperty style)
    {
        if (style == null)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            DrawReadonlyObjectField("Ссылка в сцене", sceneText, typeof(TMP_Text));
            DrawFontOverridePair(style.FindPropertyRelative("_overrideFont"), style.FindPropertyRelative("_font"), "Переопределить шрифт", "Шрифт");
            DrawOverridePair(style.FindPropertyRelative("_overrideFontSize"), style.FindPropertyRelative("_fontSize"), "Переопределить размер", "Размер");
            DrawVector2Pair(
                style.FindPropertyRelative("_overrideTextRect"),
                style.FindPropertyRelative("_anchoredPosition"),
                style.FindPropertyRelative("_sizeDelta"),
                "Переопределить rect текста",
                "Position",
                "Size");
        }
    }

    void DrawEndScreenStyleStatBindings(SerializedProperty array, StoryEndScreenController endScreen, string storyId, StoryUiStyle currentStyle)
    {
        if (array == null || !array.isArray)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Статы", EditorStyles.boldLabel);
                if (GUILayout.Button("+", GUILayout.Width(28f)))
                {
                    AddEndScreenStyleStatBinding(array);
                    return;
                }
            }

            EditorGUILayout.HelpBox(
                "Это список статов именно для текущей истории. Сцена даёт только Row/Image/Text; override ниже меняет картинки, шрифты и размеры.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Пресет текущей истории", GUILayout.Height(24f)))
                    ApplyRecommendedEndScreenStylePreset(array, storyId);
                if (GUILayout.Button("ПП", GUILayout.Height(24f)))
                    ApplyPpEndScreenStylePreset(array);
                if (GUILayout.Button("ЗЛС", GUILayout.Height(24f)))
                    ApplyZlsEndScreenStylePreset(array);
            }

            for (int i = 0; i < array.arraySize; i++)
                DrawEndScreenStyleStatBindingElement(array, i, endScreen, currentStyle, storyId);
        }
    }

    void DrawEndScreenStyleStatBindingElement(
        SerializedProperty array,
        int index,
        StoryEndScreenController endScreen,
        StoryUiStyle currentStyle,
        string storyId)
    {
        SerializedProperty item = array.GetArrayElementAtIndex(index);
        if (item == null)
            return;

        SerializedProperty enabled = item.FindPropertyRelative("_enabled");
        SerializedProperty label = item.FindPropertyRelative("_label");
        SerializedProperty statId = item.FindPropertyRelative("_statId");
        string title = !string.IsNullOrWhiteSpace(label?.stringValue)
            ? label.stringValue
            : !string.IsNullOrWhiteSpace(statId?.stringValue) ? statId.stringValue : "Стат " + (index + 1);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (enabled != null)
                    enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(18f));
                item.isExpanded = EditorGUILayout.Foldout(item.isExpanded, title + "    " + (statId != null ? statId.stringValue : ""), true);

                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (GUILayout.Button("↑", GUILayout.Width(24f)))
                    {
                        array.MoveArrayElement(index, index - 1);
                        return;
                    }
                }
                using (new EditorGUI.DisabledScope(index >= array.arraySize - 1))
                {
                    if (GUILayout.Button("↓", GUILayout.Width(24f)))
                    {
                        array.MoveArrayElement(index, index + 1);
                        return;
                    }
                }
                if (GUILayout.Button("x", GUILayout.Width(24f)))
                {
                    array.DeleteArrayElementAtIndex(index);
                    return;
                }
            }

            if (!item.isExpanded)
                return;

            EditorGUILayout.LabelField("Значение", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(label, new GUIContent("Название"));
            EditorGUILayout.PropertyField(statId, new GUIContent("Stat ID"));
            EditorGUILayout.PropertyField(item.FindPropertyRelative("_statAliases"), new GUIContent("Алиасы Stat ID"), true);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(item.FindPropertyRelative("_valueMode"), new GUIContent("Источник значения"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("_previewValue"), new GUIContent("Preview"));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(item.FindPropertyRelative("_format"), new GUIContent("Формат"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("_hideWhenZero"), new GUIContent("Скрыть при 0"));
            }

            if (endScreen != null)
                endScreen.RestoreSerializedConfigurationSnapshotForEditor("StoryUiStyleSceneLinkDraw");

            SerializedObject endObject = endScreen != null ? new SerializedObject(endScreen) : null;
            SerializedProperty sceneBinding = null;
            if (endObject != null)
            {
                endObject.Update();
                sceneBinding = FindOrCreateEndScreenSceneBinding(endObject, item);
                if (sceneBinding != null && AutoFillEndScreenStatBindingFromScene(sceneBinding, endScreen, overwriteExisting: false))
                {
                    endObject.ApplyModifiedProperties();
                    endScreen.RefreshSerializedConfigurationSnapshotForEditor("StoryUiStyleAutoSceneLinks");
                    EditorUtility.SetDirty(endScreen);
                    endObject.Update();
                    sceneBinding = FindOrCreateEndScreenSceneBinding(endObject, item);
                }
            }

            RectTransform row = GetEndScreenObject<RectTransform>(sceneBinding, "row");
            if (row == null && endScreen != null)
                row = FindEndScreenStatRowForBinding(endScreen, item);

            Image background = GetEndScreenObject<Image>(sceneBinding, "backgroundImage");
            Image plate = GetEndScreenObject<Image>(sceneBinding, "plateImage");
            Image icon = GetEndScreenObject<Image>(sceneBinding, "iconImage");
            if (row != null)
            {
                background ??= FindImageInEndScreenRow(row, EndScreenImageRole.Background);
                plate ??= FindImageInEndScreenRow(row, EndScreenImageRole.Plate);
                icon ??= FindImageInEndScreenRow(row, EndScreenImageRole.Icon);
            }

            bool sharedPlateIcon = plate != null && icon == plate;
            if (sharedPlateIcon)
                icon = null;

            TMP_Text lineText = GetEndScreenObject<TMP_Text>(sceneBinding, "lineText");
            TMP_Text labelText = GetEndScreenObject<TMP_Text>(sceneBinding, "labelText");
            TMP_Text valueText = GetEndScreenObject<TMP_Text>(sceneBinding, "valueText");
            if (row != null)
            {
                lineText ??= FindTextInEndScreenRow(row, "line", "text", "stat");
                labelText ??= FindTextInEndScreenRow(row, "label", "name", "title", "назв");
                valueText ??= FindTextInEndScreenRow(row, "value", "count", "amount", "number", "знач", "число");
            }

            EditorGUILayout.Space(4f);
            DrawEndScreenStyleSceneBindingFields(
                sceneBinding,
                endObject,
                endScreen,
                currentStyle,
                storyId);

            if (row == null)
            {
                EditorGUILayout.HelpBox(
                    "Row для этого Stat ID не найден явно. Автопривязка не будет гадать по порядку строк. Если нужен override на существующую строку, переименуй Row/путь или заполни scene refs в StoryEndScreenController.",
                    MessageType.Warning);
            }
            if (sharedPlateIcon)
                EditorGUILayout.HelpBox("Плашка и иконка указывают на один Image. Иконка будет очищена при применении.", MessageType.Warning);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Картинки", EditorStyles.boldLabel);
            DrawEndScreenStyleSpritePair("Фон строки", background, "Image в сцене", item.FindPropertyRelative("_backgroundSprite"), item.FindPropertyRelative("_backgroundSpriteSource"));
            DrawEndScreenStyleSpritePair("Плашка строки", plate, "Image в сцене", item.FindPropertyRelative("_plateSprite"), item.FindPropertyRelative("_plateSpriteSource"));
            DrawEndScreenStyleSpritePair("Иконка", icon, "Image в сцене", item.FindPropertyRelative("_iconSprite"), item.FindPropertyRelative("_iconSpriteSource"));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(item.FindPropertyRelative("_hideBackground"), new GUIContent("Скрыть фон"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("_hidePlate"), new GUIContent("Скрыть плашку"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("_hideIcon"), new GUIContent("Скрыть иконку"));
            }
            DrawOverridePair(item.FindPropertyRelative("_overrideIconSize"), item.FindPropertyRelative("_iconSize"), "Размер иконки без позиции", "Size");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Тексты", EditorStyles.boldLabel);
            TMP_Text singleText = lineText;
            if (singleText == null && labelText != null && (valueText == null || valueText == labelText))
                singleText = labelText;
            if (singleText == null && valueText != null && labelText == null)
                singleText = valueText;
            DrawEndScreenTextStyleBlock("Текст строки (Принципы: 0)", singleText, item.FindPropertyRelative("_lineTextStyle"));
            if (_showEndScreenSplitTextReferences)
            {
                DrawEndScreenTextStyleBlock("Label Text", labelText, item.FindPropertyRelative("_labelTextStyle"));
                DrawEndScreenTextStyleBlock("Value Text", valueText, item.FindPropertyRelative("_valueTextStyle"));
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Позиция", EditorStyles.boldLabel);
            DrawOverridePair(item.FindPropertyRelative("_overrideRowPosition"), item.FindPropertyRelative("_rowAnchoredPosition"), "Позиция Row", "Position");
            EditorGUILayout.PropertyField(item.FindPropertyRelative("_rowOffset"), new GUIContent("Row Offset"));
            DrawOverridePair(item.FindPropertyRelative("_overrideRowSize"), item.FindPropertyRelative("_rowSize"), "Размер Row", "Size");
            DrawVector2Pair(
                item.FindPropertyRelative("_overrideBackgroundRect"),
                item.FindPropertyRelative("_backgroundAnchoredPosition"),
                item.FindPropertyRelative("_backgroundSize"),
                "Rect фона строки",
                "Position",
                "Size");
            DrawVector2Pair(
                item.FindPropertyRelative("_overridePlateRect"),
                item.FindPropertyRelative("_plateAnchoredPosition"),
                item.FindPropertyRelative("_plateSize"),
                "Rect плашки строки",
                "Position",
                "Size");
            DrawVector2Pair(
                item.FindPropertyRelative("_overrideIconRect"),
                item.FindPropertyRelative("_iconAnchoredPosition"),
                item.FindPropertyRelative("_iconSize"),
                "Rect иконки",
                "Position",
                "Size");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(item.FindPropertyRelative("_backgroundOffset"), new GUIContent("Фон"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("_plateOffset"), new GUIContent("Плашка"));
            }
            EditorGUILayout.PropertyField(item.FindPropertyRelative("_iconOffset"), new GUIContent("Иконка"));
            EditorGUILayout.PropertyField(item.FindPropertyRelative("_ignoreParentLayoutWhenPositioned"), new GUIContent("Игнорировать parent layout"));
        }
    }

    void DrawEndScreenStyleSceneBindingFields(
        SerializedProperty sceneBinding,
        SerializedObject endObject,
        StoryEndScreenController endScreen,
        StoryUiStyle currentStyle,
        string storyId)
    {
        EditorGUILayout.LabelField("Ссылки сцены (можно менять)", EditorStyles.boldLabel);
        if (sceneBinding == null)
        {
            EditorGUILayout.HelpBox(
                "Не найден StoryEndScreenController или массив _statBindings. Override можно заполнить, но preview не знает, к каким объектам сцены его применять.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            "Эти ссылки сохраняются в StoryEndScreenController. Override ниже сохраняется в текущем StoryUiStyle и не смешивается между историями.",
            MessageType.None);

        bool sceneLinksChanged = endObject != null && endObject.hasModifiedProperties;

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(sceneBinding.FindPropertyRelative("row"), new GUIContent("Row"));
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(sceneBinding.FindPropertyRelative("backgroundImage"), new GUIContent("Фон"));
            EditorGUILayout.PropertyField(sceneBinding.FindPropertyRelative("plateImage"), new GUIContent("Плашка"));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(sceneBinding.FindPropertyRelative("iconImage"), new GUIContent("Иконка"));
            EditorGUILayout.PropertyField(sceneBinding.FindPropertyRelative("lineText"), new GUIContent("Текст строки"));
        }

        _showEndScreenSplitTextReferences = EditorGUILayout.Foldout(
            _showEndScreenSplitTextReferences,
            "Раздельный текст (если label и value - разные TMP)",
            true);
        if (_showEndScreenSplitTextReferences)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(sceneBinding.FindPropertyRelative("labelText"), new GUIContent("Label Text"));
                EditorGUILayout.PropertyField(sceneBinding.FindPropertyRelative("valueText"), new GUIContent("Value Text"));
            }
        }
        sceneLinksChanged |= EditorGUI.EndChangeCheck();

        TMP_Text lineText = GetEndScreenObject<TMP_Text>(sceneBinding, "lineText");
        TMP_Text labelText = GetEndScreenObject<TMP_Text>(sceneBinding, "labelText");
        TMP_Text valueText = GetEndScreenObject<TMP_Text>(sceneBinding, "valueText");
        if (lineText != null && (labelText == lineText || valueText == lineText))
        {
            EditorGUILayout.HelpBox(
                "Этот TMP используется как единый текст строки. Label/Value можно очистить, чтобы не путаться; runtime всё равно не будет перетирать его до одного нуля.",
                MessageType.Info);
            if (GUILayout.Button("Оставить только Текст строки", GUILayout.Height(22f)))
            {
                Undo.RecordObject(endScreen, "Use Single End Screen Stat Text");
                if (labelText == lineText)
                    sceneLinksChanged |= ClearEndScreenObject(sceneBinding, "labelText");
                if (valueText == lineText)
                    sceneLinksChanged |= ClearEndScreenObject(sceneBinding, "valueText");
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(GetEndScreenObject<RectTransform>(sceneBinding, "row") == null))
            {
                if (GUILayout.Button("Авто из Row", GUILayout.Height(22f)))
                {
                    Undo.RecordObject(endScreen, "Auto Fill End Screen Stat Row Links");
                    sceneLinksChanged |= AutoFillEndScreenStatBindingFromRow(sceneBinding, overwriteExisting: true);
                }
            }

            using (new EditorGUI.DisabledScope(endScreen == null))
            {
                if (GUILayout.Button("Найти в EndScreen", GUILayout.Height(22f)))
                {
                    Undo.RecordObject(endScreen, "Find End Screen Stat Row Links");
                    sceneLinksChanged |= AutoFillEndScreenStatBindingFromScene(sceneBinding, endScreen, overwriteExisting: false);
                }
            }

            if (GUILayout.Button("Очистить", GUILayout.Height(22f), GUILayout.Width(78f)))
            {
                Undo.RecordObject(endScreen, "Clear End Screen Stat Row Links");
                sceneLinksChanged |= ClearEndScreenSceneBindingReferences(sceneBinding);
            }
        }

        Image plate = GetEndScreenObject<Image>(sceneBinding, "plateImage");
        Image icon = GetEndScreenObject<Image>(sceneBinding, "iconImage");
        if (plate != null && icon != null && plate == icon)
        {
            EditorGUILayout.HelpBox(
                "Плашка и иконка указывают на один Image. Очисти иконку или назначь отдельный Icon Image, иначе спрайт плашки попадёт и в иконку.",
                MessageType.Warning);
        }

        if (!sceneLinksChanged || endObject == null)
            return;

        endObject.ApplyModifiedProperties();
        if (endScreen != null)
        {
            endScreen.RefreshSerializedConfigurationSnapshotForEditor("StoryUiStyleSceneLinks");
            EditorUtility.SetDirty(endScreen);
            RefreshEndScreenStyleLivePreview(endScreen, currentStyle, storyId, "StoryUiStyleSceneLinksChanged");
        }
        Repaint();
    }

    static SerializedProperty FindOrCreateEndScreenSceneBinding(
        SerializedObject endObject,
        SerializedProperty styleItem)
    {
        SerializedProperty array = endObject != null ? endObject.FindProperty("_statBindings") : null;
        if (array == null || !array.isArray)
            return null;

        SerializedProperty match = FindMatchingEndScreenSceneBinding(array, styleItem);
        if (match != null)
            return match;

        AddEndScreenStatBinding(array);
        SerializedProperty created = array.GetArrayElementAtIndex(array.arraySize - 1);
        SetEndScreenBindingBool(created, "enabled", false);
        CopyEndScreenStyleIdentityToSceneBinding(created, styleItem);
        return created;
    }

    static SerializedProperty FindMatchingEndScreenSceneBinding(
        SerializedProperty sceneBindings,
        SerializedProperty styleItem)
    {
        if (sceneBindings == null || !sceneBindings.isArray)
            return null;

        for (int i = 0; i < sceneBindings.arraySize; i++)
        {
            SerializedProperty sceneItem = sceneBindings.GetArrayElementAtIndex(i);
            if (EndScreenSceneBindingMatchesStyle(sceneItem, styleItem))
                return sceneItem;
        }

        return null;
    }

    static bool EndScreenSceneBindingMatchesStyle(
        SerializedProperty sceneItem,
        SerializedProperty styleItem)
    {
        List<string> sceneTokens = BuildEndScreenBindingTokens(sceneItem);
        List<string> styleTokens = BuildEndScreenBindingTokens(styleItem);
        if (sceneTokens.Count == 0 || styleTokens.Count == 0)
            return false;

        for (int i = 0; i < styleTokens.Count; i++)
        {
            string styleToken = styleTokens[i];
            if (string.IsNullOrWhiteSpace(styleToken))
                continue;

            for (int j = 0; j < sceneTokens.Count; j++)
            {
                string sceneToken = sceneTokens[j];
                if (string.IsNullOrWhiteSpace(sceneToken))
                    continue;

                if (StoryStatId.EqualsCanonical(styleToken, sceneToken) ||
                    string.Equals(Normalize(styleToken), Normalize(sceneToken), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    static void CopyEndScreenStyleIdentityToSceneBinding(
        SerializedProperty sceneItem,
        SerializedProperty styleItem)
    {
        SetEndScreenBindingString(sceneItem, "label", FindRelativeString(styleItem, "_label", "label"));
        SetEndScreenBindingString(sceneItem, "statId", FindRelativeString(styleItem, "_statId", "statId"));
        CopyEndScreenStyleAliasesToSceneBinding(sceneItem, styleItem);

        SerializedProperty valueMode = FindRelativeProperty(styleItem, "_valueMode", "valueMode");
        if (valueMode != null)
            SetEndScreenBindingEnum(sceneItem, "valueMode", valueMode.enumValueIndex);

        SerializedProperty previewValue = FindRelativeProperty(styleItem, "_previewValue", "previewValue");
        if (previewValue != null)
            SetEndScreenBindingInt(sceneItem, "previewValue", previewValue.intValue);

        SetEndScreenBindingString(sceneItem, "format", FindRelativeString(styleItem, "_format", "format"));

        SerializedProperty hideWhenZero = FindRelativeProperty(styleItem, "_hideWhenZero", "hideWhenZero");
        if (hideWhenZero != null)
            SetEndScreenBindingBool(sceneItem, "hideWhenZero", hideWhenZero.boolValue);
    }

    static void CopyEndScreenStyleAliasesToSceneBinding(
        SerializedProperty sceneItem,
        SerializedProperty styleItem)
    {
        SerializedProperty aliases = FindRelativeProperty(styleItem, "_statAliases", "statAliases");
        if (aliases == null || !aliases.isArray)
        {
            SetEndScreenBindingArraySize(sceneItem, "statAliases", 0);
            return;
        }

        var values = new string[aliases.arraySize];
        for (int i = 0; i < aliases.arraySize; i++)
            values[i] = aliases.GetArrayElementAtIndex(i).stringValue ?? "";

        SetEndScreenBindingStringArray(sceneItem, "statAliases", values);
    }

    static bool ClearEndScreenSceneBindingReferences(SerializedProperty sceneBinding)
    {
        bool changed = false;
        changed |= ClearEndScreenObject(sceneBinding, "row");
        changed |= ClearEndScreenObject(sceneBinding, "backgroundImage");
        changed |= ClearEndScreenObject(sceneBinding, "plateImage");
        changed |= ClearEndScreenObject(sceneBinding, "iconImage");
        changed |= ClearEndScreenObject(sceneBinding, "lineText");
        changed |= ClearEndScreenObject(sceneBinding, "labelText");
        changed |= ClearEndScreenObject(sceneBinding, "valueText");
        return changed;
    }

    static bool ClearEndScreenObject(SerializedProperty item, string name)
    {
        SerializedProperty property = item != null ? item.FindPropertyRelative(name) : null;
        if (property == null || property.objectReferenceValue == null)
            return false;

        property.objectReferenceValue = null;
        return true;
    }

    static T GetEndScreenObject<T>(SerializedProperty item, string name) where T : UnityEngine.Object
    {
        SerializedProperty property = item != null ? item.FindPropertyRelative(name) : null;
        return property != null ? property.objectReferenceValue as T : null;
    }

    static void ApplyRecommendedEndScreenStylePreset(SerializedProperty array, string storyId)
    {
        string normalized = string.IsNullOrWhiteSpace(storyId) ? "" : storyId.ToLowerInvariant();
        if (normalized.Contains("privychka") || normalized.Contains("pp"))
        {
            ApplyPpEndScreenStylePreset(array);
            return;
        }

        if (normalized.Contains("zls") || normalized.Contains("heart") || normalized.Contains("only_the_heart"))
        {
            ApplyZlsEndScreenStylePreset(array);
            return;
        }

        EditorUtility.DisplayDialog("Story UI Style", "Не понял story id. Используй кнопку ПП или ЗЛС.", "OK");
    }

    static void ApplyPpEndScreenStylePreset(SerializedProperty array)
    {
        ApplyEndScreenStylePreset(
            array,
            new[]
            {
                new EndScreenStatPreset("Самооценка", "self_esteem", new[] { "selfesteem", "self", "esteem", "samoocenka" }),
                new EndScreenStatPreset("Принципы", "principles", new[] { "principle", "princip" }),
                new EndScreenStatPreset("Чувства", "feelings", new[] { "feel", "feels", "feeling" })
            });
    }

    static void ApplyZlsEndScreenStylePreset(SerializedProperty array)
    {
        ApplyEndScreenStylePreset(
            array,
            new[]
            {
                new EndScreenStatPreset("Город", "city", new[] { "town", "gorod" }),
                new EndScreenStatPreset("Сказка", "fairytale", new[] { "story", "tale", "skazka" }),
                new EndScreenStatPreset("Репутация", "reputation", new[] { "respect", "rep" }),
                new EndScreenStatPreset("Искры", "hearts", new[] { "sparks", "spark", "heart" })
            });
    }

    static void ApplyEndScreenStylePreset(SerializedProperty array, EndScreenStatPreset[] presets)
    {
        if (array == null || presets == null)
            return;

        array.arraySize = 0;
        for (int i = 0; i < presets.Length; i++)
        {
            AddEndScreenStyleStatBinding(array);
            SerializedProperty item = array.GetArrayElementAtIndex(array.arraySize - 1);
            EndScreenStatPreset preset = presets[i];
            SetEndScreenStyleBindingString(item, "_label", preset.Label);
            SetEndScreenStyleBindingString(item, "_statId", preset.StatId);
            SetEndScreenStyleBindingStringArray(item, "_statAliases", preset.Aliases);
            SetEndScreenStyleBindingEnum(item, "_valueMode", (int)StoryEndScreenStatValueMode.CurrentTotal);
            item.isExpanded = i == 0;
        }
    }

    static void AddEndScreenStyleStatBinding(SerializedProperty array)
    {
        if (array == null)
            return;

        int index = array.arraySize;
        array.InsertArrayElementAtIndex(index);
        SerializedProperty item = array.GetArrayElementAtIndex(index);
        SetEndScreenStyleBindingBool(item, "_enabled", true);
        SetEndScreenStyleBindingString(item, "_label", "Стат");
        SetEndScreenStyleBindingString(item, "_statId", "custom_stat");
        SetEndScreenStyleBindingArraySize(item, "_statAliases", 0);
        SetEndScreenStyleBindingEnum(item, "_valueMode", (int)StoryEndScreenStatValueMode.CurrentTotal);
        SetEndScreenStyleBindingInt(item, "_previewValue", 0);
        SetEndScreenStyleBindingString(item, "_format", "{0}");
        SetEndScreenStyleBindingBool(item, "_hideWhenZero", false);
        SetEndScreenStyleBindingObject(item, "_backgroundSprite", null);
        SetEndScreenStyleBindingObject(item, "_backgroundSpriteSource", null);
        SetEndScreenStyleBindingObject(item, "_plateSprite", null);
        SetEndScreenStyleBindingObject(item, "_plateSpriteSource", null);
        SetEndScreenStyleBindingObject(item, "_iconSprite", null);
        SetEndScreenStyleBindingObject(item, "_iconSpriteSource", null);
        SetEndScreenStyleBindingBool(item, "_hideBackground", false);
        SetEndScreenStyleBindingBool(item, "_hidePlate", false);
        SetEndScreenStyleBindingBool(item, "_hideIcon", false);
        SetEndScreenStyleBindingBool(item, "_overrideIconSize", false);
        SetEndScreenStyleBindingVector2(item, "_iconSize", new Vector2(96f, 96f));
        SetEndScreenStyleBindingBool(item, "_overrideBackgroundRect", false);
        SetEndScreenStyleBindingVector2(item, "_backgroundAnchoredPosition", Vector2.zero);
        SetEndScreenStyleBindingVector2(item, "_backgroundSize", Vector2.zero);
        SetEndScreenStyleBindingBool(item, "_overridePlateRect", false);
        SetEndScreenStyleBindingVector2(item, "_plateAnchoredPosition", Vector2.zero);
        SetEndScreenStyleBindingVector2(item, "_plateSize", Vector2.zero);
        SetEndScreenStyleBindingBool(item, "_overrideIconRect", false);
        SetEndScreenStyleBindingVector2(item, "_iconAnchoredPosition", Vector2.zero);
        item.isExpanded = true;
    }

    static void SetEndScreenStyleBindingString(SerializedProperty item, string name, string value)
    {
        SerializedProperty property = item != null ? item.FindPropertyRelative(name) : null;
        if (property != null)
            property.stringValue = value ?? "";
    }

    static void SetEndScreenStyleBindingStringArray(SerializedProperty item, string name, string[] values)
    {
        SerializedProperty property = item != null ? item.FindPropertyRelative(name) : null;
        if (property == null || !property.isArray)
            return;
        property.arraySize = values != null ? values.Length : 0;
        for (int i = 0; i < property.arraySize; i++)
            property.GetArrayElementAtIndex(i).stringValue = values[i] ?? "";
    }

    static void SetEndScreenStyleBindingArraySize(SerializedProperty item, string name, int size)
    {
        SerializedProperty property = item != null ? item.FindPropertyRelative(name) : null;
        if (property != null && property.isArray)
            property.arraySize = Mathf.Max(0, size);
    }

    static void SetEndScreenStyleBindingBool(SerializedProperty item, string name, bool value)
    {
        SerializedProperty property = item != null ? item.FindPropertyRelative(name) : null;
        if (property != null)
            property.boolValue = value;
    }

    static void SetEndScreenStyleBindingEnum(SerializedProperty item, string name, int value)
    {
        SerializedProperty property = item != null ? item.FindPropertyRelative(name) : null;
        if (property != null)
            property.enumValueIndex = value;
    }

    static void SetEndScreenStyleBindingInt(SerializedProperty item, string name, int value)
    {
        SerializedProperty property = item != null ? item.FindPropertyRelative(name) : null;
        if (property != null)
            property.intValue = value;
    }

    static void SetEndScreenStyleBindingObject(SerializedProperty item, string name, UnityEngine.Object value)
    {
        SerializedProperty property = item != null ? item.FindPropertyRelative(name) : null;
        if (property != null)
            property.objectReferenceValue = value;
    }

    static void SetEndScreenStyleBindingVector2(SerializedProperty item, string name, Vector2 value)
    {
        SerializedProperty property = item != null ? item.FindPropertyRelative(name) : null;
        if (property != null)
            property.vector2Value = value;
    }

    static bool RefreshEndScreenStyleLivePreview(StoryEndScreenController endScreen, StoryUiStyle style, string storyId, string reason)
    {
        if (endScreen == null)
            return false;

        endScreen.ApplyStoryUiStyle(style, storyId, preview: true);
        SceneView.RepaintAll();
        return true;
    }

    void DrawEndScreenSerializedProperty(SerializedObject serialized, string propertyName, string label)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox("Поле " + propertyName + " не найдено на StoryEndScreenController.", MessageType.Warning);
            return;
        }

        if (propertyName == "_references")
        {
            DrawEndScreenReferenceFoldouts(property, label);
            return;
        }

        bool expanded = propertyName == "_layoutSettings" ? _showEndScreenLayout : _showEndScreenPreview;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            expanded = EditorGUILayout.Foldout(expanded, label, true, EditorStyles.foldoutHeader);
            if (propertyName == "_layoutSettings")
                _showEndScreenLayout = expanded;
            else
                _showEndScreenPreview = expanded;

            if (!expanded)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(property, GUIContent.none, true);
            EditorGUI.indentLevel--;
        }
    }

    void DrawEndScreenReferenceFoldouts(SerializedProperty references, string label)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _showEndScreenReferences = EditorGUILayout.Foldout(_showEndScreenReferences, label, true, EditorStyles.foldoutHeader);
            if (!_showEndScreenReferences)
                return;

            DrawEndScreenReferenceGroup(ref _showEndScreenRoot, "Корень", references,
                ("root", "EndScreen Root"),
                ("canvasGroup", "Canvas Group"),
                ("safeArea", "Safe Area"),
                ("panelRoot", "Panel Root"));

            DrawEndScreenReferenceGroup(ref _showEndScreenBackground, "Фон", references);
            if (_showEndScreenBackground)
            {
                DrawEndScreenReferenceOverridePair(
                    references,
                    "Фон экрана",
                    "backgroundImage",
                    "Image в сцене",
                    "backgroundOverride",
                    "Override Sprite");
                DrawEndScreenReferenceOverridePair(
                    references,
                    "Fallback",
                    null,
                    null,
                    "defaultBackground",
                    "Fallback Sprite");
            }

            DrawEndScreenReferenceGroup(ref _showEndScreenTexts, "Тексты", references,
                ("titleText", "Заголовок"),
                ("storyTitleText", "Название истории"),
                ("completedEpisodeText", "Завершённая серия"),
                ("nextEpisodeText", "Следующая серия"));

            DrawEndScreenReferenceGroup(ref _showEndScreenStats, "Статы", references,
                ("statsContainer", "Родитель авто-строк"),
                ("statRowTemplate", "Шаблон авто-строки"));
            if (_showEndScreenStats)
            {
                DrawEndScreenReferenceOverridePair(
                    references,
                    "Фон блока статов",
                    "statsBackgroundImage",
                    "Image в сцене",
                    "statsBackgroundOverride",
                    "Override Sprite");
                DrawEndScreenRelative(references, "hideStatsBackground", "Скрыть фон блока");
            }

            DrawEndScreenReferenceGroup(ref _showEndScreenButton, "Кнопка", references,
                ("continueButton", "Continue Button"),
                ("continueButtonText", "Текст кнопки"));
            if (_showEndScreenButton)
            {
                DrawEndScreenReferenceOverridePair(
                    references,
                    "Плашка кнопки",
                    "continueButtonPlateImage",
                    "Image плашки",
                    "continueButtonPlateSprite",
                    "Override Sprite");
                DrawEndScreenRelative(references, "continueButtonPlateSpriteSource", "Источник override");
            }
        }
    }

    static void DrawEndScreenReferenceOverridePair(
        SerializedProperty parent,
        string title,
        string scenePropertyName,
        string sceneLabel,
        string overridePropertyName,
        string overrideLabel)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrWhiteSpace(scenePropertyName))
                DrawEndScreenRelative(parent, scenePropertyName, sceneLabel);
            DrawEndScreenRelative(parent, overridePropertyName, overrideLabel);
        }
    }

    static void DrawEndScreenRelative(SerializedProperty parent, string propertyName, string label)
    {
        SerializedProperty property = parent != null ? parent.FindPropertyRelative(propertyName) : null;
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
    }

    static void DrawEndScreenReferenceGroup(ref bool expanded, string title, SerializedProperty parent, params (string propertyName, string label)[] fields)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            expanded = EditorGUILayout.Foldout(expanded, title, true, EditorStyles.foldoutHeader);
            if (!expanded)
                return;

            EditorGUI.indentLevel++;
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty property = parent.FindPropertyRelative(fields[i].propertyName);
                if (property != null)
                    EditorGUILayout.PropertyField(property, new GUIContent(fields[i].label), true);
            }
            EditorGUI.indentLevel--;
        }
    }

    void DrawEndScreenStatBindings(
        SerializedObject serialized,
        string propertyName,
        string label,
        StoryEndScreenController endScreen,
        string currentStoryId)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox("Поле " + propertyName + " не найдено на StoryEndScreenController.", MessageType.Warning);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _showEndScreenStatBindings = EditorGUILayout.Foldout(_showEndScreenStatBindings, label, true, EditorStyles.foldoutHeader);
            if (!_showEndScreenStatBindings)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Строки статов", EditorStyles.boldLabel);
                if (GUILayout.Button("Статы текущей истории", GUILayout.Width(150f)))
                    ApplyRecommendedEndScreenStatPreset(property, endScreen, currentStoryId);
                if (GUILayout.Button("ПП", GUILayout.Width(42f)))
                    ApplyPpEndScreenStatPreset(property, endScreen);
                if (GUILayout.Button("ЗЛС", GUILayout.Width(42f)))
                    ApplyZlsEndScreenStatPreset(property, endScreen);
                if (GUILayout.Button("+", GUILayout.Width(30f)))
                    AddEndScreenStatBinding(property);
            }

            EditorGUILayout.HelpBox(
                "Для замены картинки у строки должны быть ссылки Row/Plate/Icon. Если Row пустой, sprite сохранится, но preview не сможет понять, куда его применить. Нажми «Авто из Row» или назначь Row вручную.",
                MessageType.None);

            if (property.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Список статов пуст. Нажми + или автозаполни ссылки, чтобы вернуть базовые статы.", MessageType.Info);
                return;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty item = property.GetArrayElementAtIndex(i);
                DrawEndScreenStatBindingElement(property, item, i, endScreen);
            }
        }
    }

    static void DrawEndScreenStatBindingElement(
        SerializedProperty array,
        SerializedProperty item,
        int index,
        StoryEndScreenController endScreen)
    {
        if (array == null || item == null)
            return;

        SerializedProperty enabled = item.FindPropertyRelative("enabled");
        SerializedProperty label = item.FindPropertyRelative("label");
        SerializedProperty statId = item.FindPropertyRelative("statId");
        SerializedProperty statAliases = item.FindPropertyRelative("statAliases");
        SerializedProperty valueMode = item.FindPropertyRelative("valueMode");
        SerializedProperty previewValue = item.FindPropertyRelative("previewValue");
        SerializedProperty row = item.FindPropertyRelative("row");
        SerializedProperty backgroundImage = item.FindPropertyRelative("backgroundImage");
        SerializedProperty plateImage = item.FindPropertyRelative("plateImage");
        SerializedProperty iconImage = item.FindPropertyRelative("iconImage");
        SerializedProperty lineText = item.FindPropertyRelative("lineText");
        SerializedProperty labelText = item.FindPropertyRelative("labelText");
        SerializedProperty valueText = item.FindPropertyRelative("valueText");
        SerializedProperty backgroundSprite = item.FindPropertyRelative("backgroundSprite");
        SerializedProperty backgroundSpriteSource = item.FindPropertyRelative("backgroundSpriteSource");
        SerializedProperty plateSprite = item.FindPropertyRelative("plateSprite");
        SerializedProperty plateSpriteSource = item.FindPropertyRelative("plateSpriteSource");
        SerializedProperty icon = item.FindPropertyRelative("icon");
        SerializedProperty iconSpriteSource = item.FindPropertyRelative("iconSpriteSource");
        SerializedProperty hideBackground = item.FindPropertyRelative("hideBackground");
        SerializedProperty hidePlate = item.FindPropertyRelative("hidePlate");
        SerializedProperty hideIcon = item.FindPropertyRelative("hideIcon");
        SerializedProperty overrideRowPosition = item.FindPropertyRelative("overrideRowPosition");
        SerializedProperty rowAnchoredPosition = item.FindPropertyRelative("rowAnchoredPosition");
        SerializedProperty rowOffset = item.FindPropertyRelative("rowOffset");
        SerializedProperty backgroundOffset = item.FindPropertyRelative("backgroundOffset");
        SerializedProperty plateOffset = item.FindPropertyRelative("plateOffset");
        SerializedProperty iconOffset = item.FindPropertyRelative("iconOffset");
        SerializedProperty lineTextOffset = item.FindPropertyRelative("lineTextOffset");
        SerializedProperty labelTextOffset = item.FindPropertyRelative("labelTextOffset");
        SerializedProperty valueTextOffset = item.FindPropertyRelative("valueTextOffset");
        SerializedProperty overrideRowSize = item.FindPropertyRelative("overrideRowSize");
        SerializedProperty rowSize = item.FindPropertyRelative("rowSize");
        SerializedProperty ignoreParentLayoutWhenPositioned = item.FindPropertyRelative("ignoreParentLayoutWhenPositioned");
        SerializedProperty hideWhenZero = item.FindPropertyRelative("hideWhenZero");
        SerializedProperty format = item.FindPropertyRelative("format");

        string title = !string.IsNullOrWhiteSpace(label?.stringValue)
            ? label.stringValue
            : !string.IsNullOrWhiteSpace(statId?.stringValue) ? statId.stringValue : "Стат " + (index + 1);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (enabled != null)
                    enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(18f));

                item.isExpanded = EditorGUILayout.Foldout(item.isExpanded, title, true);

                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (GUILayout.Button("↑", GUILayout.Width(24f)))
                    {
                        array.MoveArrayElement(index, index - 1);
                        return;
                    }
                }

                using (new EditorGUI.DisabledScope(index >= array.arraySize - 1))
                {
                    if (GUILayout.Button("↓", GUILayout.Width(24f)))
                    {
                        array.MoveArrayElement(index, index + 1);
                        return;
                    }
                }

                if (GUILayout.Button("x", GUILayout.Width(24f)))
                {
                    array.DeleteArrayElementAtIndex(index);
                    return;
                }
            }

            if (!item.isExpanded)
                return;

            if (label != null)
                EditorGUILayout.PropertyField(label, new GUIContent("Название"));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (statId != null)
                    EditorGUILayout.PropertyField(statId, new GUIContent("Stat ID"));
                if (valueMode != null)
                    EditorGUILayout.PropertyField(valueMode, new GUIContent("Значение"));
            }

            if (statAliases != null)
                EditorGUILayout.PropertyField(statAliases, new GUIContent("Алиасы Stat ID"), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (previewValue != null)
                    EditorGUILayout.PropertyField(previewValue, new GUIContent("Preview"));
                if (hideWhenZero != null)
                    EditorGUILayout.PropertyField(hideWhenZero, new GUIContent("Скрыть при 0"));
            }

            if (format != null)
                EditorGUILayout.PropertyField(format, new GUIContent("Формат"));

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Объекты сцены и override", EditorStyles.boldLabel);
            if (row != null)
                EditorGUILayout.PropertyField(row, new GUIContent("Row"));
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(row == null || row.objectReferenceValue == null))
                {
                    if (GUILayout.Button("Авто из Row", GUILayout.Height(22f)))
                        AutoFillEndScreenStatBindingFromRow(item, overwriteExisting: true);
                }

                using (new EditorGUI.DisabledScope(endScreen == null))
                {
                    if (GUILayout.Button("Найти в EndScreen", GUILayout.Height(22f)))
                        AutoFillEndScreenStatBindingFromScene(item, endScreen, overwriteExisting: false);
                }
            }

            DrawEndScreenStatOverridePair("Фон строки", backgroundImage, "Image в сцене", backgroundSprite, "Override Sprite", backgroundSpriteSource);
            DrawEndScreenStatOverridePair("Плашка строки", plateImage, "Image в сцене", plateSprite, "Override Sprite", plateSpriteSource);
            DrawEndScreenStatOverridePair("Иконка", iconImage, "Image в сцене", icon, "Override Sprite", iconSpriteSource);

            EditorGUILayout.LabelField("Тексты", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (lineText != null)
                    EditorGUILayout.PropertyField(lineText, new GUIContent("Line Text"));
                if (labelText != null)
                    EditorGUILayout.PropertyField(labelText, new GUIContent("Label Text"));
            }
            if (valueText != null)
                EditorGUILayout.PropertyField(valueText, new GUIContent("Value Text"));

            DrawEndScreenStatTargetWarning(backgroundSprite, backgroundImage, row, "Фон строки");
            DrawEndScreenStatTargetWarning(plateSprite, plateImage, row, "Плашка строки");
            DrawEndScreenStatTargetWarning(icon, iconImage, row, "Иконка");

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Видимость", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (hideBackground != null)
                    EditorGUILayout.PropertyField(hideBackground, new GUIContent("Hide Background"));
                if (hidePlate != null)
                    EditorGUILayout.PropertyField(hidePlate, new GUIContent("Hide Plate"));
                if (hideIcon != null)
                    EditorGUILayout.PropertyField(hideIcon, new GUIContent("Hide Icon"));
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Layout override", EditorStyles.boldLabel);
            if (overrideRowPosition != null)
                EditorGUILayout.PropertyField(overrideRowPosition, new GUIContent("Переопределить позицию"));
            if (overrideRowPosition != null && overrideRowPosition.boolValue && rowAnchoredPosition != null)
                EditorGUILayout.PropertyField(rowAnchoredPosition, new GUIContent("Anchored Position"));
            if (overrideRowSize != null)
                EditorGUILayout.PropertyField(overrideRowSize, new GUIContent("Переопределить размер"));
            if (overrideRowSize != null && overrideRowSize.boolValue && rowSize != null)
                EditorGUILayout.PropertyField(rowSize, new GUIContent("Size"));
            if (rowOffset != null)
                EditorGUILayout.PropertyField(rowOffset, new GUIContent("Row Offset"));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (backgroundOffset != null)
                    EditorGUILayout.PropertyField(backgroundOffset, new GUIContent("Background"));
                if (plateOffset != null)
                    EditorGUILayout.PropertyField(plateOffset, new GUIContent("Plate"));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (iconOffset != null)
                    EditorGUILayout.PropertyField(iconOffset, new GUIContent("Icon"));
                if (lineTextOffset != null)
                    EditorGUILayout.PropertyField(lineTextOffset, new GUIContent("Line Text"));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (labelTextOffset != null)
                    EditorGUILayout.PropertyField(labelTextOffset, new GUIContent("Label"));
                if (valueTextOffset != null)
                    EditorGUILayout.PropertyField(valueTextOffset, new GUIContent("Value"));
            }
            if (((overrideRowPosition != null && overrideRowPosition.boolValue) ||
                (overrideRowSize != null && overrideRowSize.boolValue)) &&
                ignoreParentLayoutWhenPositioned != null)
            {
                EditorGUILayout.PropertyField(ignoreParentLayoutWhenPositioned, new GUIContent("Игнорировать parent layout"));
            }
        }
    }

    static void DrawEndScreenStatOverridePair(
        string title,
        SerializedProperty sceneReference,
        string sceneLabel,
        SerializedProperty overrideSprite,
        string overrideLabel,
        SerializedProperty source)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (sceneReference != null)
                EditorGUILayout.PropertyField(sceneReference, new GUIContent(sceneLabel), true);
            if (source != null)
                EditorGUILayout.PropertyField(source, new GUIContent("Источник override"), true);
            if (overrideSprite != null)
                EditorGUILayout.PropertyField(overrideSprite, new GUIContent(overrideLabel), true);
        }
    }

    static void DrawEndScreenStatTargetWarning(
        SerializedProperty overrideSprite,
        SerializedProperty sceneReference,
        SerializedProperty row,
        string title)
    {
        if (overrideSprite == null || overrideSprite.objectReferenceValue == null)
            return;

        bool hasTarget =
            sceneReference != null && sceneReference.objectReferenceValue != null ||
            row != null && row.objectReferenceValue != null;
        if (hasTarget)
            return;

        EditorGUILayout.HelpBox(
            title + ": override sprite задан, но Row/Image пустые. Назначь Row и нажми «Авто из Row», иначе preview и runtime не знают, куда применить картинку.",
            MessageType.Warning);
    }

    static bool AutoFillEmptyEndScreenStatBindings(SerializedObject serialized, StoryEndScreenController endScreen)
    {
        SerializedProperty array = serialized != null ? serialized.FindProperty("_statBindings") : null;
        if (array == null || !array.isArray)
            return false;

        bool changed = false;
        for (int i = 0; i < array.arraySize; i++)
            changed |= AutoFillEndScreenStatBindingFromScene(array.GetArrayElementAtIndex(i), endScreen, overwriteExisting: false);
        return changed;
    }

    static bool AutoFillEndScreenStatBindingFromScene(
        SerializedProperty item,
        StoryEndScreenController endScreen,
        bool overwriteExisting)
    {
        if (item == null)
            return false;

        bool changed = false;
        SerializedProperty rowProperty = item.FindPropertyRelative("row");
        RectTransform row = rowProperty != null ? rowProperty.objectReferenceValue as RectTransform : null;

        if (row == null && endScreen != null)
        {
            row = FindEndScreenStatRowForBinding(endScreen, item);
            if (row != null && rowProperty != null)
            {
                rowProperty.objectReferenceValue = row;
                changed = true;
            }
        }

        if (row != null)
            changed |= AutoFillEndScreenStatBindingFromRow(item, overwriteExisting);

        return changed;
    }

    static bool AutoFillEndScreenStatBindingFromRow(SerializedProperty item, bool overwriteExisting)
    {
        if (item == null)
            return false;

        SerializedProperty rowProperty = item.FindPropertyRelative("row");
        RectTransform row = rowProperty != null ? rowProperty.objectReferenceValue as RectTransform : null;
        if (row == null)
            return false;

        bool changed = false;
        Image background = FindImageInEndScreenRow(row, EndScreenImageRole.Background);
        Image plate = FindImageInEndScreenRow(row, EndScreenImageRole.Plate);
        Image icon = FindImageInEndScreenRow(row, EndScreenImageRole.Icon);

        changed |= SetEndScreenObject(item, "backgroundImage", background, overwriteExisting);
        changed |= SetEndScreenObject(item, "plateImage", plate, overwriteExisting);
        changed |= SetEndScreenObject(item, "iconImage", icon, overwriteExisting);

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text singleVisibleText = ResolveSingleVisibleEndScreenText(texts);
        if (singleVisibleText != null)
        {
            changed |= SetEndScreenObject(item, "lineText", singleVisibleText, overwriteExisting);

            if (overwriteExisting)
            {
                changed |= ClearEndScreenObject(item, "labelText");
                changed |= ClearEndScreenObject(item, "valueText");
            }
        }
        else if (texts.Length == 1)
        {
            changed |= SetEndScreenObject(item, "lineText", texts[0], overwriteExisting);

            if (overwriteExisting)
            {
                changed |= ClearEndScreenObject(item, "labelText");
                changed |= ClearEndScreenObject(item, "valueText");
            }
        }
        else if (texts.Length > 1)
        {
            TMP_Text labelText = FindTextInEndScreenRow(row, "label", "name", "title", "назв");
            TMP_Text valueText = FindTextInEndScreenRow(row, "value", "count", "amount", "number", "знач", "число");
            if (labelText == null && valueText != null)
            {
                changed |= SetEndScreenObject(item, "lineText", valueText, overwriteExisting);
                if (overwriteExisting)
                {
                    changed |= ClearEndScreenObject(item, "labelText");
                    changed |= ClearEndScreenObject(item, "valueText");
                }

                changed |= SetSpriteFromImageIfMissing(item, "backgroundSprite", "backgroundSpriteSource", background);
                changed |= SetSpriteFromImageIfMissing(item, "plateSprite", "plateSpriteSource", plate);
                changed |= SetSpriteFromImageIfMissing(item, "icon", "iconSpriteSource", icon);
                return changed;
            }

            if (labelText == null)
                labelText = FirstNonNumericText(texts);
            if (valueText == null)
                valueText = FirstNumericText(texts);
            if (valueText == null)
                valueText = texts[texts.Length - 1];

            changed |= SetEndScreenObject(item, "labelText", labelText, overwriteExisting);
            changed |= SetEndScreenObject(item, "valueText", valueText, overwriteExisting);
        }

        changed |= SetSpriteFromImageIfMissing(item, "backgroundSprite", "backgroundSpriteSource", background);
        changed |= SetSpriteFromImageIfMissing(item, "plateSprite", "plateSpriteSource", plate);
        changed |= SetSpriteFromImageIfMissing(item, "icon", "iconSpriteSource", icon);
        return changed;
    }

    enum EndScreenImageRole
    {
        Background,
        Plate,
        Icon
    }

    static Image FindImageInEndScreenRow(RectTransform row, EndScreenImageRole role)
    {
        if (row == null)
            return null;

        Image[] images = row.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
            return null;

        string[] tokens = role == EndScreenImageRole.Background
            ? new[] { "background", "bg", "fon", "фон" }
            : role == EndScreenImageRole.Icon
                ? new[] { "icon", "икон", "cityicon", "fairytaleicon", "respecticon", "sparkicon" }
                : new[] { "plate", "back", "frame", "panel", "input", "field", "plashka", "podlozka", "подлож", "плаш" };

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            string name = image.name.ToLowerInvariant();
            if (ContainsAnyToken(name, tokens))
                return image;
        }

        return null;
    }

    static Image LargestImage(Image[] images, Image exclude = null)
    {
        Image best = null;
        float bestArea = -1f;
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image == exclude)
                continue;

            Rect rect = image.rectTransform.rect;
            float area = Mathf.Abs(rect.width * rect.height);
            if (area > bestArea)
            {
                best = image;
                bestArea = area;
            }
        }
        return best;
    }

    static Image SmallestImage(Image[] images)
    {
        Image best = null;
        float bestArea = float.MaxValue;
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            Rect rect = image.rectTransform.rect;
            float area = Mathf.Abs(rect.width * rect.height);
            if (area > 0.01f && area < bestArea)
            {
                best = image;
                bestArea = area;
            }
        }
        return best;
    }

    static TMP_Text FindTextInEndScreenRow(RectTransform row, params string[] tokens)
    {
        if (row == null)
            return null;

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            string haystack = (text.name + "\n" + text.text).ToLowerInvariant();
            if (ContainsAnyToken(haystack, tokens))
                return text;
        }

        return null;
    }

    static TMP_Text ResolveSingleVisibleEndScreenText(TMP_Text[] texts)
    {
        if (texts == null || texts.Length == 0)
            return null;

        TMP_Text result = null;
        int usableCount = 0;
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || !text.enabled || !text.gameObject.activeInHierarchy || text.color.a <= 0.001f)
                continue;

            usableCount++;
            result = text;
            if (usableCount > 1)
                return null;
        }

        return usableCount == 1 ? result : null;
    }

    static TMP_Text FirstNumericText(TMP_Text[] texts)
    {
        if (texts == null)
            return null;

        for (int i = 0; i < texts.Length; i++)
        {
            if (IsMostlyNumericText(texts[i]))
                return texts[i];
        }

        return null;
    }

    static TMP_Text FirstNonNumericText(TMP_Text[] texts)
    {
        if (texts == null)
            return null;

        for (int i = 0; i < texts.Length; i++)
        {
            if (!IsMostlyNumericText(texts[i]))
                return texts[i];
        }

        return null;
    }

    static bool IsMostlyNumericText(TMP_Text text)
    {
        if (text == null)
            return false;

        string value = text.text;
        if (string.IsNullOrWhiteSpace(value))
            value = text.name;

        int digits = 0;
        int letters = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsDigit(c))
                digits++;
            else if (char.IsLetter(c))
                letters++;
        }

        return digits > 0 && letters == 0;
    }

    static bool SetEndScreenObject(SerializedProperty item, string name, UnityEngine.Object value, bool overwriteExisting)
    {
        if (value == null)
            return false;

        SerializedProperty property = item.FindPropertyRelative(name);
        if (property == null)
            return false;

        if (!overwriteExisting && property.objectReferenceValue != null)
            return false;

        if (property.objectReferenceValue == value)
            return false;

        property.objectReferenceValue = value;
        return true;
    }

    static bool SetSpriteFromImageIfMissing(
        SerializedProperty item,
        string spriteName,
        string sourceName,
        Image image)
    {
        if (item == null || image == null || image.sprite == null)
            return false;

        bool changed = false;
        SerializedProperty sprite = item.FindPropertyRelative(spriteName);
        UnityEngine.Object resolvedSprite = sprite != null ? sprite.objectReferenceValue : null;
        if (sprite != null && sprite.objectReferenceValue == null)
        {
            sprite.objectReferenceValue = image.sprite;
            resolvedSprite = image.sprite;
            changed = true;
        }

        SerializedProperty source = item.FindPropertyRelative(sourceName);
        if (source != null && source.objectReferenceValue == null)
        {
            source.objectReferenceValue = resolvedSprite != null ? resolvedSprite : image.sprite;
            changed = true;
        }

        return changed;
    }

    static RectTransform FindEndScreenStatRowForBinding(
        StoryEndScreenController endScreen,
        SerializedProperty item)
    {
        if (endScreen == null || item == null || endScreen.References == null)
            return null;

        GameObject rootObject = endScreen.References.ResolveRoot(endScreen);
        Transform root = rootObject != null ? rootObject.transform : endScreen.transform;
        if (root == null)
            return null;

        List<string> tokens = BuildEndScreenBindingTokens(item);
        if (tokens.Count == 0)
            return null;

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        RectTransform fallback = null;
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null || rect.transform == root || rect.GetComponent<Button>() != null)
                continue;
            if (rect.GetComponentInChildren<TMP_Text>(true) == null || rect.GetComponentsInChildren<Image>(true).Length == 0)
                continue;

            string haystack = BuildEndScreenRowHaystack(rect);
            if (!ContainsAnyToken(haystack, tokens))
                continue;

            if (haystack.Contains("finalstat") || haystack.Contains("statrow"))
                return rect;

            fallback ??= rect;
        }

        return fallback;
    }

    static List<string> BuildEndScreenBindingTokens(SerializedProperty item)
    {
        var tokens = new List<string>();
        AddToken(tokens, FindRelativeString(item, "statId", "_statId"));
        AddToken(tokens, FindRelativeString(item, "label", "_label"));

        SerializedProperty aliases = FindRelativeProperty(item, "statAliases", "_statAliases");
        if (aliases != null && aliases.isArray)
        {
            for (int i = 0; i < aliases.arraySize; i++)
                AddToken(tokens, aliases.GetArrayElementAtIndex(i).stringValue);
        }

        bool hasCity = ContainsToken(tokens, "city") || ContainsToken(tokens, "town") || ContainsToken(tokens, "город");
        bool hasFairytale = ContainsToken(tokens, "fairytale") || ContainsToken(tokens, "story") || ContainsToken(tokens, "сказка");
        bool hasReputation = ContainsToken(tokens, "reputation") || ContainsToken(tokens, "respect") || ContainsToken(tokens, "репутация");
        bool hasHearts = ContainsToken(tokens, "hearts") || ContainsToken(tokens, "sparks") || ContainsToken(tokens, "искры");
        bool hasSelfEsteem = ContainsToken(tokens, "self_esteem") || ContainsToken(tokens, "самооценка");
        bool hasPrinciples = ContainsToken(tokens, "principles") || ContainsToken(tokens, "принципы");
        bool hasFeelings = ContainsToken(tokens, "feelings") || ContainsToken(tokens, "feels") || ContainsToken(tokens, "чувства");

        hasSelfEsteem = hasSelfEsteem || ContainsToken(tokens, "selfesteem") || ContainsToken(tokens, "self") || ContainsToken(tokens, "esteem");
        hasPrinciples = hasPrinciples || ContainsToken(tokens, "principle") || ContainsToken(tokens, "princip");
        hasFeelings = hasFeelings || ContainsToken(tokens, "feel") || ContainsToken(tokens, "feeling");

        if (hasCity)
            AddTokens(tokens, "city", "town", "gorod", "город", "cityfinalstat");
        if (hasFairytale)
            AddTokens(tokens, "fairytale", "story", "tale", "skazka", "сказка", "fairytalefinalstat");
        if (hasReputation)
            AddTokens(tokens, "reputation", "respect", "rep", "репутация", "respectfinalstat");
        if (hasHearts)
            AddTokens(tokens, "hearts", "sparks", "spark", "heart", "искры", "sparkfinalstat", "heartfinalstat");
        if (hasSelfEsteem)
            AddTokens(tokens, "self_esteem", "selfesteem", "self", "esteem", "самооценка", "самооцен");
        if (hasPrinciples)
            AddTokens(tokens, "principles", "principle", "princip", "принципы", "принцип");
        if (hasFeelings)
            AddTokens(tokens, "feelings", "feels", "feel", "feeling", "чувства", "чувств");

        if (hasSelfEsteem)
            AddTokens(tokens, "cityfinalstat", "city", "town");
        if (hasPrinciples)
            AddTokens(tokens, "fairytalefinalstat", "fairytale", "story", "tale");
        if (hasFeelings)
            AddTokens(tokens, "respectfinalstat", "reputation", "respect", "rep");

        return tokens;
    }

    static SerializedProperty FindRelativeProperty(SerializedProperty item, params string[] names)
    {
        if (item == null || names == null)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            SerializedProperty property = item.FindPropertyRelative(names[i]);
            if (property != null)
                return property;
        }

        return null;
    }

    static string FindRelativeString(SerializedProperty item, params string[] names)
    {
        SerializedProperty property = FindRelativeProperty(item, names);
        return property != null ? property.stringValue : "";
    }

    static string BuildEndScreenRowHaystack(RectTransform row)
    {
        var builder = new System.Text.StringBuilder(256);
        Transform current = row != null ? row.transform : null;
        while (current != null)
        {
            builder.Append(current.name).Append('\n');
            current = current.parent;
        }

        TMP_Text[] texts = row != null ? row.GetComponentsInChildren<TMP_Text>(true) : Array.Empty<TMP_Text>();
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
                builder.Append(texts[i].name).Append('\n').Append(texts[i].text).Append('\n');
        }

        return builder.ToString().ToLowerInvariant();
    }

    static void AddTokens(List<string> tokens, params string[] values)
    {
        if (values == null)
            return;

        for (int i = 0; i < values.Length; i++)
            AddToken(tokens, values[i]);
    }

    static void AddToken(List<string> tokens, string value)
    {
        if (tokens == null || string.IsNullOrWhiteSpace(value))
            return;

        string normalized = value.Trim().ToLowerInvariant();
        if (!tokens.Contains(normalized))
            tokens.Add(normalized);
    }

    static bool ContainsToken(List<string> tokens, string value)
    {
        if (tokens == null || string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim().ToLowerInvariant();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] == normalized)
                return true;
        }

        return false;
    }

    static bool ContainsAnyToken(string haystack, IEnumerable<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(haystack) || tokens == null)
            return false;

        string normalizedHaystack = haystack.ToLowerInvariant();
        foreach (string token in tokens)
        {
            if (!string.IsNullOrWhiteSpace(token) && normalizedHaystack.Contains(token.Trim().ToLowerInvariant()))
                return true;
        }

        return false;
    }

    static void ApplyRecommendedEndScreenStatPreset(
        SerializedProperty array,
        StoryEndScreenController endScreen,
        string storyId)
    {
        string normalized = string.IsNullOrWhiteSpace(storyId) ? "" : storyId.ToLowerInvariant();
        if (normalized.Contains("privychka") || normalized.Contains("pp_") || normalized.Contains("pp"))
        {
            ApplyPpEndScreenStatPreset(array, endScreen);
            return;
        }

        if (normalized.Contains("zls") || normalized.Contains("heart") || normalized.Contains("only_the_heart"))
        {
            ApplyZlsEndScreenStatPreset(array, endScreen);
            return;
        }

        EditorUtility.DisplayDialog(
            "Story End Screen",
            "Не понял story id для пресета. Используй кнопку «ПП» или «ЗЛС».",
            "OK");
    }

    static void ApplyPpEndScreenStatPreset(SerializedProperty array, StoryEndScreenController endScreen)
    {
        ApplyEndScreenStatPreset(
            array,
            endScreen,
            new[]
            {
                new EndScreenStatPreset("Самооценка", "self_esteem", new[] { "selfesteem", "self", "esteem", "samoocenka", "самооценка" }),
                new EndScreenStatPreset("Принципы", "principles", new[] { "principle", "princip", "принцип", "принципы" }),
                new EndScreenStatPreset("Чувства", "feelings", new[] { "feels", "feel", "feeling", "чувства" })
            });
    }

    static void ApplyZlsEndScreenStatPreset(SerializedProperty array, StoryEndScreenController endScreen)
    {
        ApplyEndScreenStatPreset(
            array,
            endScreen,
            new[]
            {
                new EndScreenStatPreset("Город", "city", new[] { "town", "gorod", "город" }),
                new EndScreenStatPreset("Сказка", "fairytale", new[] { "story", "tale", "skazka", "сказка" }),
                new EndScreenStatPreset("Репутация", "reputation", new[] { "respect", "rep", "репутация" }),
                new EndScreenStatPreset("Искры", "hearts", new[] { "sparks", "spark", "heart", "искры" })
            });
    }

    static void ApplyEndScreenStatPreset(
        SerializedProperty array,
        StoryEndScreenController endScreen,
        EndScreenStatPreset[] presets)
    {
        if (array == null || presets == null)
            return;

        array.arraySize = 0;
        for (int i = 0; i < presets.Length; i++)
        {
            AddEndScreenStatBinding(array);
            SerializedProperty item = array.GetArrayElementAtIndex(array.arraySize - 1);
            if (item == null)
                continue;

            EndScreenStatPreset preset = presets[i];
            SetEndScreenBindingString(item, "label", preset.Label);
            SetEndScreenBindingString(item, "statId", preset.StatId);
            SetEndScreenBindingStringArray(item, "statAliases", preset.Aliases);
            SetEndScreenBindingEnum(item, "valueMode", (int)StoryEndScreenStatValueMode.CurrentTotal);
            SetEndScreenBindingInt(item, "previewValue", 0);
            AutoFillEndScreenStatBindingFromScene(item, endScreen, overwriteExisting: false);
            item.isExpanded = i == 0;
        }
    }

    struct EndScreenStatPreset
    {
        public readonly string Label;
        public readonly string StatId;
        public readonly string[] Aliases;

        public EndScreenStatPreset(string label, string statId, string[] aliases)
        {
            Label = label;
            StatId = statId;
            Aliases = aliases ?? Array.Empty<string>();
        }
    }

    static void AddEndScreenStatBinding(SerializedProperty array)
    {
        if (array == null)
            return;

        int index = array.arraySize;
        array.InsertArrayElementAtIndex(index);
        SerializedProperty item = array.GetArrayElementAtIndex(index);
        if (item == null)
            return;

        SetEndScreenBindingBool(item, "enabled", true);
        SetEndScreenBindingString(item, "label", "Стат");
        SetEndScreenBindingString(item, "statId", "custom_stat");
        SetEndScreenBindingArraySize(item, "statAliases", 0);
        SetEndScreenBindingEnum(item, "valueMode", (int)StoryEndScreenStatValueMode.CurrentTotal);
        SetEndScreenBindingInt(item, "previewValue", 0);
        SetEndScreenBindingObject(item, "row", null);
        SetEndScreenBindingObject(item, "backgroundImage", null);
        SetEndScreenBindingObject(item, "plateImage", null);
        SetEndScreenBindingObject(item, "iconImage", null);
        SetEndScreenBindingObject(item, "lineText", null);
        SetEndScreenBindingObject(item, "labelText", null);
        SetEndScreenBindingObject(item, "valueText", null);
        SetEndScreenBindingObject(item, "backgroundSprite", null);
        SetEndScreenBindingObject(item, "backgroundSpriteSource", null);
        SetEndScreenBindingObject(item, "plateSprite", null);
        SetEndScreenBindingObject(item, "plateSpriteSource", null);
        SetEndScreenBindingObject(item, "icon", null);
        SetEndScreenBindingObject(item, "iconSpriteSource", null);
        SetEndScreenBindingBool(item, "hideBackground", false);
        SetEndScreenBindingBool(item, "hidePlate", false);
        SetEndScreenBindingBool(item, "hideIcon", false);
        SetEndScreenBindingBool(item, "overrideRowPosition", false);
        SetEndScreenBindingVector2(item, "rowAnchoredPosition", Vector2.zero);
        SetEndScreenBindingVector2(item, "rowOffset", Vector2.zero);
        SetEndScreenBindingVector2(item, "backgroundOffset", Vector2.zero);
        SetEndScreenBindingVector2(item, "plateOffset", Vector2.zero);
        SetEndScreenBindingVector2(item, "iconOffset", Vector2.zero);
        SetEndScreenBindingBool(item, "overrideBackgroundRect", false);
        SetEndScreenBindingVector2(item, "backgroundAnchoredPosition", Vector2.zero);
        SetEndScreenBindingVector2(item, "backgroundSize", Vector2.zero);
        SetEndScreenBindingBool(item, "overridePlateRect", false);
        SetEndScreenBindingVector2(item, "plateAnchoredPosition", Vector2.zero);
        SetEndScreenBindingVector2(item, "plateSize", Vector2.zero);
        SetEndScreenBindingBool(item, "overrideIconRect", false);
        SetEndScreenBindingVector2(item, "iconAnchoredPosition", Vector2.zero);
        SetEndScreenBindingVector2(item, "lineTextOffset", Vector2.zero);
        SetEndScreenBindingVector2(item, "labelTextOffset", Vector2.zero);
        SetEndScreenBindingVector2(item, "valueTextOffset", Vector2.zero);
        SetEndScreenBindingBool(item, "overrideRowSize", false);
        SetEndScreenBindingVector2(item, "rowSize", Vector2.zero);
        SetEndScreenBindingBool(item, "ignoreParentLayoutWhenPositioned", true);
        SetEndScreenBindingBool(item, "hideWhenZero", false);
        SetEndScreenBindingString(item, "format", "{0}");
        item.isExpanded = true;
    }

    static void SetEndScreenBindingBool(SerializedProperty item, string name, bool value)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null)
            property.boolValue = value;
    }

    static void SetEndScreenBindingString(SerializedProperty item, string name, string value)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null)
            property.stringValue = value ?? "";
    }

    static void SetEndScreenBindingInt(SerializedProperty item, string name, int value)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null)
            property.intValue = value;
    }

    static void SetEndScreenBindingEnum(SerializedProperty item, string name, int value)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null)
            property.enumValueIndex = value;
    }

    static void SetEndScreenBindingObject(SerializedProperty item, string name, UnityEngine.Object value)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null)
            property.objectReferenceValue = value;
    }

    static void SetEndScreenBindingVector2(SerializedProperty item, string name, Vector2 value)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null)
            property.vector2Value = value;
    }

    static void SetEndScreenBindingArraySize(SerializedProperty item, string name, int size)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null && property.isArray)
            property.arraySize = Mathf.Max(0, size);
    }

    static void SetEndScreenBindingStringArray(SerializedProperty item, string name, string[] values)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property == null || !property.isArray)
            return;

        values ??= Array.Empty<string>();
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).stringValue = values[i] ?? "";
    }

    static void DrawEndScreenValidationResult(StoryEndScreenValidationResult validation)
    {
        if (validation == null)
            return;

        if (!validation.HasWarnings && !validation.HasErrors)
        {
            EditorGUILayout.HelpBox("Финальный экран готов для Edit Mode preview и runtime-показа.", MessageType.Info);
            return;
        }

        for (int i = 0; i < validation.Errors.Count; i++)
            EditorGUILayout.HelpBox(validation.Errors[i], MessageType.Error);
        for (int i = 0; i < validation.Warnings.Count; i++)
            EditorGUILayout.HelpBox(validation.Warnings[i], MessageType.Warning);
    }

    static void ShowEndScreenValidationDialog(StoryEndScreenController endScreen)
    {
        StoryEndScreenValidationResult validation = endScreen != null ? endScreen.ValidateEndScreen(requireRuntime: true) : null;
        string message = FormatEndScreenValidation(validation);
        EditorUtility.DisplayDialog("Story End Screen", message, "OK");
        AppLogger.Info(
            AppLogCategory.Editor,
            nameof(StoryUiStyleEditor),
            nameof(ShowEndScreenValidationDialog),
            "StoryEndScreenController validation requested from Story UI Style inspector.",
            LogMetadata.Of(
                "errors", validation != null ? validation.Errors.Count : 0,
                "warnings", validation != null ? validation.Warnings.Count : 0,
                "object", endScreen != null ? endScreen.name : ""));
    }

    static string FormatEndScreenValidation(StoryEndScreenValidationResult validation)
    {
        if (validation == null)
            return "Проверка не выполнена.";

        if (!validation.HasWarnings && !validation.HasErrors)
            return "Финальный экран готов: критичных ошибок и предупреждений нет.";

        var builder = new System.Text.StringBuilder(512);
        for (int i = 0; i < validation.Errors.Count; i++)
            builder.Append("Ошибка: ").AppendLine(validation.Errors[i]);
        for (int i = 0; i < validation.Warnings.Count; i++)
            builder.Append("Предупреждение: ").AppendLine(validation.Warnings[i]);
        return builder.ToString();
    }

    static void RenderEndScreenInspectorPreview(StoryEndScreenController endScreen)
    {
        if (endScreen == null)
            return;

        Undo.RegisterFullObjectHierarchyUndo(endScreen.gameObject, "Preview End Screen");
        bool shown = RefreshEndScreenInspectorLivePreview(endScreen, nameof(StoryUiStyleEditor));
        if (!shown)
            EditorUtility.DisplayDialog("Story End Screen", "Предпросмотр финального экрана не смог отрисоваться. Проверь ссылки root/text/stats/template.", "OK");
    }

    static bool RefreshEndScreenInspectorLivePreview(StoryEndScreenController endScreen, string reason)
    {
        if (endScreen == null)
            return false;

        endScreen.AutoFillEndScreenReferencesFromHierarchy();
        bool shown = endScreen.ShowStaticPreview(reason);
        if (!shown)
            endScreen.ApplyConfiguredStatVisualsToScene();
        endScreen.RecalculateLayout(reason);
        EditorUtility.SetDirty(endScreen);
        Canvas.ForceUpdateCanvases();
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        return shown;
    }

    void DrawPhoneSection()
    {
        DrawGroupTitle(
            "Телефон",
            "Scene-specific ссылки телефона теперь живут в StoryUserInterface. StoryUiStyle хранит только визуальные пресеты.");

        PhoneDialogueUI phoneUi = FindScenePhoneUi();
        StoryUserInterface storyUserInterface = PhoneDialoguePreviewSetup.FindSceneStoryUserInterface();
        if (phoneUi == null)
        {
            EditorGUILayout.HelpBox(
                "В открытой сцене не найден PhoneDialogueUI. Создай/настрой экран телефона, чтобы видеть SMS preview без Play Mode.",
                MessageType.Warning);

            if (GUILayout.Button("Создать/настроить PhoneDialogueUI", GUILayout.Height(28f)))
            {
                phoneUi = PhoneDialoguePreviewSetup.CreateOrConfigureInOpenScene();
                if (phoneUi != null)
                    Repaint();
            }

            DrawPhoneHotkeySettings();
            return;
        }

        if (storyUserInterface == null)
        {
            EditorGUILayout.HelpBox(
                "PhoneDialogueUI найден, но StoryUserInterface ещё не содержит конфигурацию телефона. Нажми кнопку миграции, чтобы перенести scene-ссылки из старого места.",
                MessageType.Warning);

            if (GUILayout.Button("Мигрировать ссылки в StoryUserInterface", GUILayout.Height(28f)))
            {
                storyUserInterface = PhoneDialoguePreviewSetup.FindOrCreateStoryUserInterface(phoneUi);
                if (storyUserInterface != null)
                {
                    Undo.RecordObject(storyUserInterface, "Migrate Phone UI To StoryUserInterface");
                    storyUserInterface.MigratePhoneReferencesFromLegacyPhoneDialogueUI(overwrite: false);
                    storyUserInterface.AutoFillPhoneReferences(overwrite: false);
                    storyUserInterface.ApplyPhoneConfiguration(nameof(StoryUiStyleEditor));
                    InvalidatePhoneValidationCache();
                    EditorUtility.SetDirty(storyUserInterface);
                    Repaint();
                }
            }

            DrawPhoneHotkeySettings();
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.ObjectField("StoryUserInterface", storyUserInterface, typeof(StoryUserInterface), true);
            EditorGUILayout.ObjectField("PhoneDialogueUI в сцене", phoneUi, typeof(PhoneDialogueUI), true);
            EditorGUILayout.HelpBox("Ссылки и layout редактируются здесь через StoryUserInterface. StoryUiStyle остаётся только визуальным preset asset.", MessageType.Info);

            SerializedObject storyUserInterfaceObject = new SerializedObject(storyUserInterface);
            storyUserInterfaceObject.Update();
            DrawPhoneSerializedProperty(storyUserInterfaceObject, "_phoneReferences", "Ссылки UI телефона и шаблоны сообщений");
            DrawPhoneSerializedProperty(storyUserInterfaceObject, "_phoneLayoutSettings", "Layout сообщений");
            DrawPhoneSerializedProperty(storyUserInterfaceObject, "_phonePreviewSettings", "Предпросмотр телефона");
            if (storyUserInterfaceObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(storyUserInterface);
                QueuePhoneConfigurationApply(storyUserInterface, phoneUi, recalculateLayout: true);
            }

            PhonePreviewValidationResult validation = GetCachedPhoneValidation(storyUserInterface);
            DrawPhoneValidationResult(validation);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Открыть StoryUserInterface", GUILayout.Height(26f)))
                {
                    Selection.activeGameObject = storyUserInterface.gameObject;
                    EditorGUIUtility.PingObject(storyUserInterface.gameObject);
                }

                if (GUILayout.Button("Мигрировать ссылки из StoryUiStyle", GUILayout.Height(26f)))
                {
                    Undo.RecordObject(storyUserInterface, "Migrate Phone References");
                    storyUserInterface.MigratePhoneReferencesFromLegacyPhoneDialogueUI(overwrite: false);
                    InvalidatePhoneValidationCache();
                    EditorUtility.SetDirty(storyUserInterface);
                    Repaint();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Показать preview", GUILayout.Height(26f)))
                    RenderPhoneInspectorPreview(storyUserInterface, phoneUi);

                if (GUILayout.Button("Очистить preview", GUILayout.Height(26f)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(phoneUi.gameObject, "Hide Phone Preview");
                    storyUserInterface.ClearPhonePreview();
                    EditorUtility.SetDirty(phoneUi);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Пересчитать layout", GUILayout.Height(26f)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(phoneUi.gameObject, "Recalculate Phone Layout");
                    storyUserInterface.RecalculatePhoneLayout("StoryUiStyleEditorManual");
                    EditorUtility.SetDirty(phoneUi);
                }

                if (GUILayout.Button("Открыть PhoneDialogueUI", GUILayout.Height(26f)))
                {
                    Selection.activeGameObject = phoneUi.gameObject;
                    EditorGUIUtility.PingObject(phoneUi.gameObject);
                }
            }

            EditorGUILayout.LabelField("Сценарий быстрого preview", EditorStyles.boldLabel);
            PhonePreviewSettings quickPreviewSettings = storyUserInterface.PhonePreviewSettings;
            string previewContactName = quickPreviewSettings != null
                ? quickPreviewSettings.quickPreviewContactName
                : _phoneInspectorPreviewContactName;
            EditorGUI.BeginChangeCheck();
            previewContactName = EditorGUILayout.TextField(
                "\u041A\u043E\u043D\u0442\u0430\u043A\u0442 \u0432 \u0448\u0430\u043F\u043A\u0435",
                previewContactName);
            if (EditorGUI.EndChangeCheck())
            {
                _phoneInspectorPreviewContactName = previewContactName;
                if (quickPreviewSettings != null)
                {
                    Undo.RecordObject(storyUserInterface, "Change Phone Preview Contact");
                    quickPreviewSettings.quickPreviewContactName = previewContactName;
                    quickPreviewSettings.Normalize();
                    EditorUtility.SetDirty(storyUserInterface);
                    QueuePhoneConfigurationApply(storyUserInterface, phoneUi, recalculateLayout: false);
                }
            }
            else
            {
                _phoneInspectorPreviewContactName = previewContactName;
            }
            _phoneInspectorPreviewScript = EditorGUILayout.TextArea(_phoneInspectorPreviewScript ?? "", GUILayout.MinHeight(88f));
            EditorGUILayout.HelpBox("Токены имени поддерживаются прямо здесь: {PlayerName}, {CharacterName}, [player_name], NAME, ИМЯ.", MessageType.None);
        }

        DrawPhoneHotkeySettings();
    }

    static void DrawPhoneSerializedProperty(SerializedObject serialized, string propertyName, string label)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox("Поле " + propertyName + " не найдено на PhoneDialogueUI.", MessageType.Warning);
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label), true);
    }

    static void DrawPhoneValidationResult(PhonePreviewValidationResult validation)
    {
        if (validation == null)
            return;

        if (!validation.HasWarnings && !validation.HasErrors)
        {
            EditorGUILayout.HelpBox("Ссылки телефона выглядят готовыми для Edit Mode preview и runtime.", MessageType.Info);
            return;
        }

        for (int i = 0; i < validation.Errors.Count; i++)
            EditorGUILayout.HelpBox(validation.Errors[i], MessageType.Error);
        for (int i = 0; i < validation.Warnings.Count; i++)
            EditorGUILayout.HelpBox(validation.Warnings[i], MessageType.Warning);
    }

    void ShowPhoneValidationDialog(PhoneDialogueUI phoneUi)
    {
        PhonePreviewValidationResult validation = PhonePreviewValidator.Validate(phoneUi, BuildPhoneInspectorPreviewNode(phoneUi), true);
        string message = FormatPhoneValidation(validation);
        EditorUtility.DisplayDialog("PhoneDialogueUI", message, "OK");
        AppLogger.Info(
            AppLogCategory.Editor,
            nameof(StoryUiStyleEditor),
            nameof(ShowPhoneValidationDialog),
            "PhoneDialogueUI validation requested from Story UI Style inspector.",
            LogMetadata.Of(
                "errors", validation != null ? validation.Errors.Count : 0,
                "warnings", validation != null ? validation.Warnings.Count : 0,
                "object", phoneUi != null ? phoneUi.name : ""));
    }

    void RenderPhoneInspectorPreview(PhoneDialogueUI phoneUi)
    {
        if (phoneUi == null)
            return;

        Undo.RegisterFullObjectHierarchyUndo(phoneUi.gameObject, "Preview Phone UI");
        PhoneDialogueNode node = BuildPhoneInspectorPreviewNode(phoneUi);
        phoneUi.AutoFillPhoneReferencesFromHierarchy();
        bool shown = Application.isPlaying
            ? new PhoneDialogueRuntimePlayer().Play(phoneUi, node, null)
            : new PhoneDialogueEditorPreviewRenderer().Render(phoneUi, node, nameof(StoryUiStyleEditor));

        if (!shown)
            EditorUtility.DisplayDialog("PhoneDialogueUI", "Предпросмотр телефона не смог отрисоваться. Проверь ссылки и шаблоны SMS-бабблов.", "OK");

        EditorUtility.SetDirty(phoneUi);
        Canvas.ForceUpdateCanvases();
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    void RenderPhoneInspectorPreview(StoryUserInterface storyUserInterface, PhoneDialogueUI phoneUi)
    {
        if (storyUserInterface == null || phoneUi == null)
            return;

        Undo.RegisterFullObjectHierarchyUndo(phoneUi.gameObject, "Preview Phone UI");
        PhoneDialogueNode node = BuildPhoneInspectorPreviewNode(phoneUi);
        storyUserInterface.AutoFillPhoneReferences(overwrite: false);
        bool shown = storyUserInterface.ShowPhonePreview(node, nameof(StoryUiStyleEditor));
        InvalidatePhoneValidationCache();

        if (!shown)
            EditorUtility.DisplayDialog("PhoneDialogueUI", "Предпросмотр телефона не смог отрисоваться. Проверь ссылки и шаблоны SMS-бабблов.", "OK");

        EditorUtility.SetDirty(storyUserInterface);
        EditorUtility.SetDirty(phoneUi);
        Canvas.ForceUpdateCanvases();
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    PhoneDialogueNode BuildPhoneInspectorPreviewNode(PhoneDialogueUI phoneUi)
    {
        if (_phoneInspectorPreviewNode == null)
        {
            _phoneInspectorPreviewNode = CreateInstance<PhoneDialogueNode>();
            _phoneInspectorPreviewNode.hideFlags = HideFlags.HideAndDontSave;
            _phoneInspectorPreviewNode.name = "Phone Inspector Preview Node";
        }

        string configuredContactName = phoneUi != null &&
            phoneUi.PreviewSettings != null
            ? phoneUi.PreviewSettings.quickPreviewContactName
            : _phoneInspectorPreviewContactName;
        if (string.IsNullOrWhiteSpace(configuredContactName))
            configuredContactName = _phoneInspectorPreviewContactName;
        _phoneInspectorPreviewNode.contactName = string.IsNullOrWhiteSpace(configuredContactName)
            ? "\u0420\u043E\u0431"
            : configuredContactName.Trim();
        _phoneInspectorPreviewNode.typingDelay = phoneUi != null && phoneUi.PreviewSettings != null
            ? Mathf.Max(0f, phoneUi.PreviewSettings.runtimeTypingDelay)
            : 0.15f;
        bool useDefaultPhoto = phoneUi != null &&
                               phoneUi.PreviewSettings != null &&
                               phoneUi.PreviewSettings.useDefaultPhotoSpriteInQuickPreview;
        _phoneInspectorPreviewNode.messages = BuildPhoneInspectorPreviewMessages(
            _phoneInspectorPreviewScript,
            _phoneInspectorPreviewNode.contactName,
            useDefaultPhoto && phoneUi != null && phoneUi.PhoneReferences != null ? phoneUi.PhoneReferences.defaultPhotoSprite : null);
        return _phoneInspectorPreviewNode;
    }

    static List<PhoneMessage> BuildPhoneInspectorPreviewMessages(string script, string contactName, Sprite defaultAttachment)
    {
        var messages = new List<PhoneMessage>();
        if (string.IsNullOrWhiteSpace(script))
            return messages;

        string[] lines = script.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        PhoneMessageSide lastSide = PhoneMessageSide.Incoming;
        string lastSenderName = ResolvePhonePreviewSenderName(lastSide, contactName);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = (lines[i] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string speaker = "";
            string text = line;
            int colon = line.IndexOf(':');
            if (colon > 0)
            {
                speaker = line.Substring(0, colon).Trim();
                text = line.Substring(colon + 1).Trim();
            }

            PhoneMessageSide side = string.IsNullOrWhiteSpace(speaker)
                ? lastSide
                : IsOutgoingPhonePreviewSpeaker(speaker, contactName) ? PhoneMessageSide.Outgoing : PhoneMessageSide.Incoming;
            string senderName = string.IsNullOrWhiteSpace(speaker)
                ? lastSenderName
                : NormalizePhonePreviewSenderName(speaker, side, contactName);
            bool usePhotoLayout;
            Sprite attachment = ExtractPhoneInspectorAttachment(ref text, defaultAttachment, out usePhotoLayout);
            messages.Add(new PhoneMessage
            {
                senderName = senderName,
                side = side,
                text = text,
                timeText = messages.Count == 0 ? "15:25" : "",
                attachment = attachment,
                usePhotoLayout = usePhotoLayout || attachment != null
            });
            lastSide = side;
            lastSenderName = senderName;
        }

        return messages;
    }

    static bool IsOutgoingPhonePreviewSpeaker(string speaker, string contactName)
    {
        if (DialogueVariableResolver.IsPlayerSpeakerName(
                speaker,
                DialogueVariableContext.PhoneDialogue(nameof(StoryUiStyleEditor))))
            return true;

        string value = (speaker ?? "").Trim().Trim('[', ']', '<', '>').ToLowerInvariant();
        string contact = (contactName ?? "").Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(contact) && value == contact)
            return false;

        if (value == "name" ||
            value == "hero" ||
            value == "me" ||
            value == "player" ||
            value == "\u0438\u043C\u044F" ||
            value == "\u0433\u0433" ||
            value == "\u044F")
            return true;

        if (value == "contact" ||
            value == "meg" ||
            value == "\u043C\u044D\u0433")
            return false;

        return value == "out" || value == "outgoing";
    }

    static string NormalizePhonePreviewSenderName(string speaker, PhoneMessageSide side, string contactName)
    {
        if (DialogueVariableResolver.IsPlayerNameToken(speaker))
            return "{PlayerName}";

        string value = (speaker ?? "").Trim();
        string normalized = value.Trim('[', ']', '<', '>').ToLowerInvariant();
        if (normalized == "name" ||
            normalized == "hero" ||
            normalized == "me" ||
            normalized == "player" ||
            normalized == "\u0438\u043C\u044F" ||
            normalized == "\u0433\u0433" ||
            normalized == "\u044F")
            return "{PlayerName}";

        if ((normalized == "contact" || normalized == "in" || normalized == "incoming") &&
            !string.IsNullOrWhiteSpace(contactName))
            return contactName.Trim();

        return string.IsNullOrWhiteSpace(value)
            ? ResolvePhonePreviewSenderName(side, contactName)
            : value;
    }

    static string ResolvePhonePreviewSenderName(PhoneMessageSide side, string contactName)
    {
        return side == PhoneMessageSide.Outgoing
            ? "{PlayerName}"
            : string.IsNullOrWhiteSpace(contactName) ? "Contact" : contactName.Trim();
    }

    static Sprite ExtractPhoneInspectorAttachment(ref string text, Sprite defaultAttachment, out bool usePhotoLayout)
    {
        usePhotoLayout = false;
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (text.IndexOf("[photo]", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            usePhotoLayout = true;
            text = text.Replace("[photo]", "").Trim();
            return defaultAttachment;
        }

        if (text.IndexOf("[\u0444\u043E\u0442\u043E]", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            usePhotoLayout = true;
            text = text.Replace("[\u0444\u043E\u0442\u043E]", "").Trim();
            return defaultAttachment;
        }

        if (text.IndexOf("photo", StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf("\u0444\u043E\u0442\u043E", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            usePhotoLayout = true;
            text = RemovePhoneTokenIgnoreCase(RemovePhoneTokenIgnoreCase(text, "photo"), "\u0444\u043E\u0442\u043E").Trim();
            return defaultAttachment;
        }

        return null;
    }

    static string RemovePhoneTokenIgnoreCase(string value, string token)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(token))
            return value ?? "";

        int index = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            value = value.Remove(index, token.Length);
            index = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }

    static string FormatPhoneValidation(PhonePreviewValidationResult validation)
    {
        if (validation == null)
            return "Проверка не выполнена.";

        if (!validation.HasWarnings && !validation.HasErrors)
            return "PhoneDialogueUI готов: критичных ошибок и предупреждений нет.";

        var builder = new System.Text.StringBuilder(512);
        for (int i = 0; i < validation.Errors.Count; i++)
            builder.Append("Ошибка: ").AppendLine(validation.Errors[i]);
        for (int i = 0; i < validation.Warnings.Count; i++)
            builder.Append("Предупреждение: ").AppendLine(validation.Warnings[i]);
        return builder.ToString();
    }

    void DrawPhoneHotkeySettings()
    {
        DrawGroupTitle("Переход к phone node", "Горячая клавиша StoryManager для проверки телефонной катсцены из любого текущего состояния истории.");

        StoryManager storyManager = FindSceneStoryManager();
        if (storyManager == null)
        {
            EditorGUILayout.HelpBox("В сцене не найден StoryManager. Настройки горячей клавиши появятся, когда StoryManager будет открыт в сцене.", MessageType.Info);
            return;
        }

        SerializedObject managerObject = new SerializedObject(storyManager);
        managerObject.Update();
        DrawOptionalSerializedProperty(managerObject, "jumpToPhoneWithKeyboard", "Включить горячую клавишу");
        DrawOptionalSerializedProperty(managerObject, "jumpToPhoneKey", "Клавиша перехода");
        DrawOptionalSerializedProperty(managerObject, "jumpToPhoneDefaultChoiceIndex", "Выбор по умолчанию");
        DrawOptionalSerializedProperty(managerObject, "jumpToPhoneAllowPremiumDefaultChoice", "Разрешить premium fallback");
        DrawOptionalSerializedProperty(managerObject, "jumpToPhoneMaxNodes", "Лимит нод для поиска");
        DrawOptionalSerializedProperty(managerObject, "jumpToPhoneTargetNodeGuid", "Target node GUID");
        if (managerObject.ApplyModifiedProperties())
            EditorUtility.SetDirty(storyManager);
    }

    static void DrawOptionalSerializedProperty(SerializedObject serialized, string propertyName, string label)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox("Поле StoryManager." + propertyName + " пока не найдено.", MessageType.None);
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label), true);
    }

    void DrawAdvancedSection()
    {
        DrawApplyOnlySpritesToggle(_dialogueApplyOnlySprites, "доп. настроек диалога");
        if (!DrawFoldout(ref _showAdvanced, "Дополнительно для Image диалога"))
            return;

        DrawOverridePair(_overrideColor, _color, "Цвет Image", "Цвет");
        DrawOverridePair(_overrideImageType, _imageType, "Image Type", "Type");
        DrawOverridePair(_overridePreserveAspect, _preserveAspect, "Preserve Aspect", "Preserve");
        DrawOverridePair(_overridePixelsPerUnitMultiplier, _pixelsPerUnitMultiplier, "Pixels Per Unit", "Multiplier");
        DrawOverridePair(_overrideMaterial, _material, "Material", "Material");
        DrawOverridePair(_overrideRaycastTarget, _raycastTarget, "Raycast Target", "Raycast");
    }

    static bool DrawFoldout(ref bool expanded, string title)
    {
        EditorGUILayout.Space(8f);
        expanded = true;
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        return true;
    }

    void DrawSubTabBar(ref NameInputTab tab, GUIContent[] labels)
    {
        EditorGUILayout.Space(4f);
        tab = (NameInputTab)GUILayout.Toolbar((int)tab, labels, GUILayout.MinHeight(23f));
        EditorGUILayout.Space(3f);
    }

    void DrawSubTabBar(ref StatsTab tab, GUIContent[] labels)
    {
        EditorGUILayout.Space(4f);
        tab = (StatsTab)GUILayout.Toolbar((int)tab, labels, GUILayout.MinHeight(23f));
        EditorGUILayout.Space(3f);
    }

    void DrawSubTabBar(ref ChapterTab tab, GUIContent[] labels)
    {
        EditorGUILayout.Space(4f);
        tab = (ChapterTab)GUILayout.Toolbar((int)tab, labels, GUILayout.MinHeight(23f));
        EditorGUILayout.Space(3f);
    }

    void DrawGroupTitle(string title, string hint = null)
    {
        EditorGUILayout.Space(5f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (_showHints && !string.IsNullOrWhiteSpace(hint))
            EditorGUILayout.HelpBox(hint, MessageType.None);
    }

    void DrawApplyOnlySpritesToggle(SerializedProperty property, string categoryName)
    {
        if (property == null)
            return;

        EditorGUILayout.PropertyField(
            property,
            new GUIContent(
                "Только спрайты",
                "Если включено, эта категория Story UI Style меняет только Sprite и не трогает Rect, текст, padding, offsets, цвета, шрифты и авто-настройки."));

        if (_showHints && property.boolValue)
        {
            EditorGUILayout.HelpBox(
                $"Для {categoryName} активен режим только спрайтов: остальные настройки в этой категории сохраняются в asset, но не применяются к сцене и preview.",
                MessageType.Info);
        }
    }

    static void DrawSpriteColumnHeader()
    {
        EditorGUILayout.LabelField("Источник и итоговый Sprite задаются отдельно", EditorStyles.miniBoldLabel);
    }

    static void DrawSpriteRow(
        string label,
        SerializedProperty spriteProperty,
        SerializedProperty sourceProperty,
        Action copyFromScene,
        SerializedProperty overrideColor = null,
        SerializedProperty color = null,
        SerializedProperty overrideImageType = null,
        SerializedProperty imageType = null,
        Image.Type defaultImageType = Image.Type.Sliced)
    {
        if (spriteProperty == null)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            UnityEngine.Object sourceObject = sourceProperty != null
                ? sourceProperty.objectReferenceValue
                : null;
            if (spriteProperty.objectReferenceValue is Sprite currentSprite)
            {
                UnityEngine.Object inferredSource = ResolveSourceForFinalSprite(currentSprite);
                if (inferredSource != null && ShouldReplaceSpriteSourceWithInferredSvg(sourceObject, currentSprite, inferredSource))
                {
                    sourceObject = inferredSource;
                    SetSpriteSource(sourceProperty, inferredSource);
                }
                else if (sourceObject == null)
                {
                    sourceObject = currentSprite;
                    SetSpriteSource(sourceProperty, currentSprite);
                }
            }

            if (sourceObject != null &&
                ShouldRefreshRasterSiblingForSvgSource(sourceObject, spriteProperty.objectReferenceValue))
            {
                if (AssignSpriteFromObject(spriteProperty, sourceProperty, sourceObject, false))
                {
                    sourceObject = sourceProperty != null
                        ? sourceProperty.objectReferenceValue
                        : sourceObject;
                    ApplySpriteDefaults(overrideColor, color, overrideImageType, imageType, defaultImageType);
                }
            }

            if (sourceObject != null && spriteProperty.objectReferenceValue == null)
            {
                if (AssignSpriteFromObject(spriteProperty, sourceProperty, sourceObject, false))
                {
                    sourceObject = sourceProperty != null
                        ? sourceProperty.objectReferenceValue
                        : sourceObject;
                    ApplySpriteDefaults(overrideColor, color, overrideImageType, imageType, defaultImageType);
                }
            }

            EditorGUI.BeginChangeCheck();
            UnityEngine.Object pickedObject = EditorGUILayout.ObjectField(
                "Источник",
                sourceObject,
                typeof(UnityEngine.Object),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                if (pickedObject == null)
                {
                    if (sourceProperty != null)
                        sourceProperty.objectReferenceValue = null;
                }
                else if (AssignSpriteFromObject(spriteProperty, sourceProperty, pickedObject, true))
                    ApplySpriteDefaults(overrideColor, color, overrideImageType, imageType, defaultImageType);
            }

            EditorGUI.BeginChangeCheck();
            Sprite pickedSprite = (Sprite)EditorGUILayout.ObjectField(
                "Итоговый Sprite",
                spriteProperty.objectReferenceValue,
                typeof(Sprite),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                string pickedSpritePath = pickedSprite != null ? AssetDatabase.GetAssetPath(pickedSprite) : "";
                if (TryAssignRasterSiblingForSvg(spriteProperty, pickedSpritePath))
                    SetSpriteSource(sourceProperty, ResolveSourceObjectForAssetPath(pickedSpritePath, pickedSprite));
                else
                {
                    spriteProperty.objectReferenceValue = pickedSprite;
                    UnityEngine.Object inferredSource = ResolveSourceForFinalSprite(pickedSprite);
                    if (inferredSource != null)
                        SetSpriteSource(sourceProperty, inferredSource);
                    else if (pickedSprite != null)
                        SetSpriteSource(sourceProperty, pickedSprite);
                    else
                        SetSpriteSource(sourceProperty, null);
                }

                if (pickedSprite != null)
                    ApplySpriteDefaults(overrideColor, color, overrideImageType, imageType, defaultImageType);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Файл..."))
                {
                    if (AssignSpriteFromExternalFile(spriteProperty, sourceProperty))
                        ApplySpriteDefaults(overrideColor, color, overrideImageType, imageType, defaultImageType);
                }

                using (new EditorGUI.DisabledScope(Selection.activeObject == null))
                {
                    if (GUILayout.Button("Выбранное"))
                    {
                        if (AssignSpriteFromObject(spriteProperty, sourceProperty, Selection.activeObject, true))
                            ApplySpriteDefaults(overrideColor, color, overrideImageType, imageType, defaultImageType);
                    }
                }

                if (copyFromScene != null && GUILayout.Button("из сцены"))
                    copyFromScene();

                using (new EditorGUI.DisabledScope(sourceProperty == null || sourceProperty.objectReferenceValue == null))
                {
                    if (GUILayout.Button("Очистить источник"))
                    {
                        if (sourceProperty != null)
                            sourceProperty.objectReferenceValue = null;
                    }
                }

                UnityEngine.Object asset = spriteProperty.objectReferenceValue;
                using (new EditorGUI.DisabledScope(asset == null))
                {
                    if (GUILayout.Button("Ping Sprite"))
                        EditorGUIUtility.PingObject(asset);
                }
            }
        }
    }

    static bool AssignSpriteFromExternalFile(SerializedProperty spriteProperty, SerializedProperty sourceProperty)
    {
        if (spriteProperty == null)
            return false;

        string sourcePath = EditorUtility.OpenFilePanelWithFilters(
            "Выбрать спрайт для Story UI",
            "",
            new[] { "Images", "png,jpg,jpeg,svg", "All files", "*" });

        if (string.IsNullOrWhiteSpace(sourcePath))
            return false;

        string assetPath = ImportExternalSpriteFile(sourcePath, spriteProperty);
        Sprite sprite = LoadSpriteAtPath(assetPath);
        if (sprite != null)
        {
            spriteProperty.objectReferenceValue = sprite;
            SetSpriteSource(sourceProperty, AssetDatabase.LoadMainAssetAtPath(assetPath) ?? sprite);
            return true;
        }

        EditorUtility.DisplayDialog(
            "Story UI Style",
            "Файл добавлен в проект, но Unity не смогла получить из него Sprite. Для PNG/JPG проверь Texture Type = Sprite. Для SVG нужен Vector Graphics и импорт, который создаёт Sprite.",
            "OK");
        return false;
    }

    static bool AssignSpriteFromObject(
        SerializedProperty spriteProperty,
        SerializedProperty sourceProperty,
        UnityEngine.Object source,
        bool showError)
    {
        if (spriteProperty == null || source == null)
            return false;

        if (source is Sprite sprite)
        {
            string spriteAssetPath = AssetDatabase.GetAssetPath(sprite);
            if (TryAssignRasterSiblingForSvg(spriteProperty, spriteAssetPath))
            {
                SetSpriteSource(sourceProperty, ResolveSourceObjectForAssetPath(spriteAssetPath, source));
                return true;
            }

            spriteProperty.objectReferenceValue = sprite;
            SetSpriteSource(sourceProperty, ResolveSourceForFinalSprite(sprite) ?? source);
            return true;
        }

        if (source is SpriteAtlas atlas && TryAssignSpriteFromAtlas(spriteProperty, atlas))
        {
            SetSpriteSource(sourceProperty, source);
            return true;
        }

        if (TryAssignSpriteFromPrefab(spriteProperty, source))
        {
            SetSpriteSource(sourceProperty, source);
            return true;
        }

        string assetPath = AssetDatabase.GetAssetPath(source);
        if (!string.IsNullOrWhiteSpace(assetPath))
        {
            UnityEngine.Object sourceToStore = ResolveSourceObjectForAssetPath(assetPath, source);

            if (TryAssignRasterSiblingForSvg(spriteProperty, assetPath))
            {
                SetSpriteSource(sourceProperty, sourceToStore);
                return true;
            }

            ConfigureTextureAsSprite(assetPath);
            Sprite loadedSprite = LoadSpriteAtPath(assetPath);
            if (loadedSprite != null)
            {
                spriteProperty.objectReferenceValue = loadedSprite;
                SetSpriteSource(sourceProperty, sourceToStore);
                return true;
            }

            SpriteAtlas loadedAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(assetPath);
            if (loadedAtlas != null && TryAssignSpriteFromAtlas(spriteProperty, loadedAtlas))
            {
                SetSpriteSource(sourceProperty, sourceToStore);
                return true;
            }

            if (TryAssignRelatedSprite(spriteProperty, assetPath, source.name))
            {
                SetSpriteSource(sourceProperty, sourceToStore);
                return true;
            }

            loadedSprite = LoadSpriteAtPath(assetPath);
            if (loadedSprite != null)
            {
                spriteProperty.objectReferenceValue = loadedSprite;
                SetSpriteSource(sourceProperty, ResolveSourceObjectForAssetPath(assetPath, source));
                return true;
            }
        }

        if (showError)
        {
            EditorUtility.DisplayDialog(
                "Story UI Style",
                "Выбранный asset не удалось назначить как Sprite. Если это PNG/JPG, поставь ему Texture Type = Sprite или нажми кнопку 'Файл...' и выбери картинку с диска.",
                "OK");
        }

        return false;
    }

    static bool TryAssignRelatedSprite(SerializedProperty spriteProperty, string assetPath, string sourceName)
    {
        if (spriteProperty == null || string.IsNullOrWhiteSpace(assetPath))
            return false;

        string folder = AssetDatabase.IsValidFolder(assetPath)
            ? assetPath
            : Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder))
            return false;

        string token = RemoveAtlasSuffix(Path.GetFileNameWithoutExtension(sourceName));
        string spriteToken = string.IsNullOrWhiteSpace(token) ? "" : token + "Sprite";
        Sprite bestSprite = null;

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int j = 0; j < assets.Length; j++)
            {
                Sprite sprite = assets[j] as Sprite;
                if (sprite == null)
                    continue;

                bestSprite ??= sprite;
                string spriteName = RemoveCloneSuffix(sprite.name);
                if (MatchesRelatedSpriteName(spriteName, token, spriteToken))
                {
                    spriteProperty.objectReferenceValue = sprite;
                    return true;
                }
            }
        }

        if (bestSprite == null)
            return false;

        spriteProperty.objectReferenceValue = bestSprite;
        return true;
    }

    static bool MatchesRelatedSpriteName(string spriteName, string token, string spriteToken)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
            return false;

        if (!string.IsNullOrWhiteSpace(spriteToken) &&
            spriteName.IndexOf(spriteToken, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return !string.IsNullOrWhiteSpace(token) &&
               spriteName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void SetSpriteSource(SerializedProperty sourceProperty, UnityEngine.Object source)
    {
        if (sourceProperty != null)
            sourceProperty.objectReferenceValue = source;
    }

    static bool IsRedundantSpriteSource(UnityEngine.Object source, UnityEngine.Object sprite)
    {
        return source != null &&
               sprite != null &&
               source == sprite &&
               source is Sprite;
    }

    static bool ShouldRefreshRasterSiblingForSvgSource(UnityEngine.Object source, UnityEngine.Object spriteObject)
    {
        if (source == null)
            return false;

        string sourcePath = AssetDatabase.GetAssetPath(source);
        string rasterPath = ResolveRasterSiblingPath(sourcePath);
        if (string.IsNullOrWhiteSpace(rasterPath))
            return false;

        Sprite sprite = spriteObject as Sprite;
        if (sprite == null)
            return true;

        string spritePath = AssetDatabase.GetAssetPath(sprite);
        return !string.Equals(spritePath, rasterPath, StringComparison.OrdinalIgnoreCase);
    }

    static UnityEngine.Object ResolveSourceForFinalSprite(Sprite sprite)
    {
        if (sprite == null)
            return null;

        string assetPath = AssetDatabase.GetAssetPath(sprite);
        if (!IsSvgAssetPath(assetPath))
            return null;

        return AssetDatabase.LoadMainAssetAtPath(assetPath);
    }

    static UnityEngine.Object ResolveSourceObjectForAssetPath(string assetPath, UnityEngine.Object fallback)
    {
        if (!IsSvgAssetPath(assetPath))
            return fallback;

        return AssetDatabase.LoadMainAssetAtPath(assetPath) ?? fallback;
    }

    static bool TryAssignRasterSiblingForSvg(SerializedProperty spriteProperty, string assetPath)
    {
        if (spriteProperty == null || !IsSvgAssetPath(assetPath))
            return false;

        string rasterPath = ResolveRasterSiblingPath(assetPath);
        if (string.IsNullOrWhiteSpace(rasterPath))
            return false;

        if (AssetImporter.GetAtPath(rasterPath) == null)
            AssetDatabase.ImportAsset(rasterPath, ImportAssetOptions.ForceSynchronousImport);

        ConfigureTextureAsSprite(rasterPath);
        Sprite rasterSprite = LoadSpriteAtPath(rasterPath);
        if (rasterSprite == null)
            return false;

        spriteProperty.objectReferenceValue = rasterSprite;
        return true;
    }

    static string ResolveRasterSiblingPath(string svgAssetPath)
    {
        if (!IsSvgAssetPath(svgAssetPath))
            return "";

        string folder = Path.GetDirectoryName(svgAssetPath)?.Replace('\\', '/');
        string name = Path.GetFileNameWithoutExtension(svgAssetPath);
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(name))
            return "";

        string pngPath = folder + "/" + name + ".png";
        return File.Exists(ToAbsoluteProjectPath(pngPath)) ? pngPath : "";
    }

    static bool ShouldReplaceSpriteSourceWithInferredSvg(
        UnityEngine.Object source,
        Sprite sprite,
        UnityEngine.Object inferredSource)
    {
        if (sprite == null || inferredSource == null || source == inferredSource)
            return false;

        if (source == null || source == sprite)
            return true;

        string spritePath = AssetDatabase.GetAssetPath(sprite);
        string sourcePath = AssetDatabase.GetAssetPath(source);
        return IsSvgAssetPath(spritePath) &&
               string.Equals(spritePath, sourcePath, StringComparison.OrdinalIgnoreCase);
    }

    static bool IsSvgAssetPath(string assetPath)
    {
        return !string.IsNullOrWhiteSpace(assetPath) &&
               assetPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    }

    static bool TryAssignSpriteFromAtlas(SerializedProperty spriteProperty, SpriteAtlas atlas)
    {
        if (spriteProperty == null || atlas == null || atlas.spriteCount <= 0)
            return false;

        Sprite[] sprites = new Sprite[atlas.spriteCount];
        int count = atlas.GetSprites(sprites);
        Sprite bestSprite = null;
        string atlasName = RemoveAtlasSuffix(atlas.name);

        for (int i = 0; i < count; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            bestSprite ??= sprite;

            string spriteName = RemoveCloneSuffix(sprite.name);
            if (!string.IsNullOrWhiteSpace(atlasName) &&
                spriteName.IndexOf(atlasName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bestSprite = sprite;
                break;
            }
        }

        if (bestSprite == null)
            return false;

        spriteProperty.objectReferenceValue = bestSprite;
        return true;
    }

    static bool TryAssignSpriteFromPrefab(SerializedProperty spriteProperty, UnityEngine.Object source)
    {
        if (spriteProperty == null || source == null)
            return false;

        GameObject root = source as GameObject;
        if (root == null && source is Component component)
            root = component.gameObject;

        if (root == null)
        {
            string assetPath = AssetDatabase.GetAssetPath(source);
            root = !string.IsNullOrWhiteSpace(assetPath)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(assetPath)
                : null;
        }

        if (root == null)
            return false;

        SVGImage svgImage = root.GetComponent<SVGImage>();
        if (svgImage == null)
            svgImage = root.GetComponentInChildren<SVGImage>(true);

        if (svgImage != null && svgImage.sprite != null)
        {
            spriteProperty.objectReferenceValue = svgImage.sprite;
            return true;
        }

        Image image = root.GetComponent<Image>();
        if (image == null)
            image = root.GetComponentInChildren<Image>(true);

        if (image != null && image.sprite != null)
        {
            spriteProperty.objectReferenceValue = image.sprite;
            return true;
        }

        SpriteRenderer spriteRenderer = root.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = root.GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return false;

        spriteProperty.objectReferenceValue = spriteRenderer.sprite;
        return true;
    }

    static string RemoveAtlasSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string result = value.Trim();
        return result.EndsWith("Atlas", StringComparison.OrdinalIgnoreCase)
            ? result.Substring(0, result.Length - "Atlas".Length).Trim()
            : result;
    }

    static string RemoveCloneSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string result = value.Trim();
        return result.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase)
            ? result.Substring(0, result.Length - "(Clone)".Length).Trim()
            : result;
    }

    static void ApplySpriteDefaults(
        SerializedProperty overrideColor,
        SerializedProperty color,
        SerializedProperty overrideImageType,
        SerializedProperty imageType,
        Image.Type defaultImageType)
    {
        if (overrideColor != null && color != null)
        {
            overrideColor.boolValue = true;
            color.colorValue = Color.white;
        }

        if (overrideImageType != null && imageType != null)
        {
            overrideImageType.boolValue = true;
            imageType.enumValueIndex = (int)defaultImageType;
        }
    }

    static string ImportExternalSpriteFile(string sourcePath, SerializedProperty spriteProperty)
    {
        string existingAssetPath = ToProjectAssetPath(sourcePath);
        if (!string.IsNullOrWhiteSpace(existingAssetPath))
        {
            ConfigureImportedVisualAsset(existingAssetPath);
            AssetDatabase.ImportAsset(existingAssetPath, ImportAssetOptions.ForceUpdate);
            return existingAssetPath;
        }

        string importFolder = ResolveSpriteImportFolder(spriteProperty);
        string absoluteFolder = ToAbsoluteProjectPath(importFolder);
        Directory.CreateDirectory(absoluteFolder);

        string targetPath = AssetDatabase.GenerateUniqueAssetPath(
            importFolder.TrimEnd('/') + "/" + Path.GetFileName(sourcePath));

        File.Copy(sourcePath, ToAbsoluteProjectPath(targetPath));
        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
        ConfigureImportedVisualAsset(targetPath);
        AssetDatabase.Refresh();
        return targetPath;
    }

    static void ConfigureImportedVisualAsset(string assetPath)
    {
        if (IsSvgAssetPath(assetPath))
        {
            return;
        }

        ConfigureTextureAsSprite(assetPath);
    }

    static void ConfigureTextureAsSprite(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (!changed)
            return;

        importer.SaveAndReimport();
    }

    static Sprite LoadSpriteAtPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
            return sprite;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite nestedSprite)
                return nestedSprite;
        }

        return null;
    }

    static string ResolveSpriteImportFolder(SerializedProperty spriteProperty)
    {
        UnityEngine.Object owner = spriteProperty != null ? spriteProperty.serializedObject.targetObject : null;
        string ownerPath = owner != null ? AssetDatabase.GetAssetPath(owner) : "";
        string ownerFolder = !string.IsNullOrWhiteSpace(ownerPath)
            ? Path.GetDirectoryName(ownerPath)?.Replace('\\', '/')
            : "";

        return string.IsNullOrWhiteSpace(ownerFolder)
            ? "Assets/_MyProject/Art/ImportedUI"
            : ownerFolder + "/ImportedSprites";
    }

    static string ToProjectAssetPath(string absoluteOrAssetPath)
    {
        if (string.IsNullOrWhiteSpace(absoluteOrAssetPath))
            return "";

        string normalizedPath = absoluteOrAssetPath.Replace('\\', '/');
        if (normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            return normalizedPath;

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(projectRoot))
            return "";

        string prefix = projectRoot.TrimEnd('/') + "/";
        return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? normalizedPath.Substring(prefix.Length)
            : "";
    }

    static string ToAbsoluteProjectPath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        return Path.Combine(projectRoot ?? "", assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    void DrawOverridePair(SerializedProperty toggle, SerializedProperty value, string toggleLabel, string valueLabel)
    {
        if (toggle == null || value == null)
            return;

        bool drawExpanded = value.propertyType == SerializedPropertyType.Generic && value.hasVisibleChildren;
        bool drawSeparateValueLine = ShouldDrawOverrideValueOnSeparateLine(value);
        if (!toggle.boolValue && !_showDisabledOverrideValues)
        {
            DrawOverrideToggleOnly(toggle, toggleLabel);
            return;
        }

        if (drawExpanded || drawSeparateValueLine)
        {
            EditorGUILayout.PropertyField(toggle, new GUIContent(toggleLabel));
            using (new EditorGUI.DisabledScope(!toggle.boolValue))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(value, new GUIContent(valueLabel), true);
                EditorGUI.indentLevel--;
            }
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.showMixedValue = toggle.hasMultipleDifferentValues;
            bool nextValue = EditorGUILayout.ToggleLeft(toggleLabel, toggle.boolValue, GUILayout.Width(245f));
            EditorGUI.showMixedValue = false;
            if (nextValue != toggle.boolValue)
                toggle.boolValue = nextValue;

            using (new EditorGUI.DisabledScope(!toggle.boolValue))
                EditorGUILayout.PropertyField(value, new GUIContent(valueLabel), true);
        }
    }

    void DrawFontOverridePair(SerializedProperty toggle, SerializedProperty font, string toggleLabel = "Override font", string valueLabel = "Font")
    {
        DrawOverridePair(toggle, font, toggleLabel, valueLabel);
    }

    void DrawVector2Pair(
        SerializedProperty toggle,
        SerializedProperty anchoredPosition,
        SerializedProperty sizeDelta,
        string toggleLabel,
        string positionLabel,
        string sizeLabel,
        bool alwaysShowValues = false)
    {
        if (toggle == null || anchoredPosition == null || sizeDelta == null)
            return;

        if (!toggle.boolValue && !_showDisabledOverrideValues && !alwaysShowValues)
        {
            DrawOverrideToggleOnly(toggle, toggleLabel);
            return;
        }

        EditorGUILayout.PropertyField(toggle, new GUIContent(toggleLabel));
        using (new EditorGUI.DisabledScope(!toggle.boolValue))
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(anchoredPosition, new GUIContent(positionLabel));
            EditorGUILayout.PropertyField(sizeDelta, new GUIContent(sizeLabel));
            EditorGUI.indentLevel--;
        }
    }

    void DrawStretchOffsetsPair(SerializedProperty toggle, SerializedProperty offsets, string toggleLabel)
    {
        if (toggle == null || offsets == null)
            return;

        if (!toggle.boolValue && !_showDisabledOverrideValues)
        {
            DrawOverrideToggleOnly(toggle, toggleLabel);
            return;
        }

        EditorGUILayout.PropertyField(toggle, new GUIContent(toggleLabel));
        using (new EditorGUI.DisabledScope(!toggle.boolValue))
        {
            EditorGUI.indentLevel++;
            Vector4 value = offsets.vector4Value;
            using (new EditorGUILayout.HorizontalScope())
            {
                value.x = EditorGUILayout.FloatField("Left", value.x);
                value.y = EditorGUILayout.FloatField("Right", value.y);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                value.z = EditorGUILayout.FloatField("Top", value.z);
                value.w = EditorGUILayout.FloatField("Bottom", value.w);
            }

            offsets.vector4Value = value;
            EditorGUI.indentLevel--;
        }
    }

    void DrawMinMaxPair(
        SerializedProperty toggle,
        SerializedProperty minValue,
        SerializedProperty maxValue,
        string toggleLabel,
        string minLabel,
        string maxLabel)
    {
        if (toggle == null || minValue == null || maxValue == null)
            return;

        if (!toggle.boolValue && !_showDisabledOverrideValues)
        {
            DrawOverrideToggleOnly(toggle, toggleLabel);
            return;
        }

        EditorGUILayout.PropertyField(toggle, new GUIContent(toggleLabel));
        using (new EditorGUI.DisabledScope(!toggle.boolValue))
        {
            EditorGUI.indentLevel++;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(minValue, new GUIContent(minLabel));
                EditorGUILayout.PropertyField(maxValue, new GUIContent(maxLabel));
            }
            EditorGUI.indentLevel--;
        }
    }

    static void DrawBoolPropertyPair(
        SerializedProperty left,
        string leftLabel,
        SerializedProperty right,
        string rightLabel)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (left != null)
                EditorGUILayout.PropertyField(left, new GUIContent(leftLabel));
            if (right != null)
                EditorGUILayout.PropertyField(right, new GUIContent(rightLabel));
        }
    }

    static bool ShouldDrawOverrideValueOnSeparateLine(SerializedProperty value)
    {
        if (value == null)
            return false;

        switch (value.propertyType)
        {
            case SerializedPropertyType.Vector2:
            case SerializedPropertyType.Vector2Int:
            case SerializedPropertyType.Vector3:
            case SerializedPropertyType.Vector3Int:
            case SerializedPropertyType.Vector4:
            case SerializedPropertyType.Rect:
            case SerializedPropertyType.RectInt:
            case SerializedPropertyType.Bounds:
            case SerializedPropertyType.BoundsInt:
                return true;
            default:
                return false;
        }
    }

    static void DrawOverrideToggleOnly(SerializedProperty toggle, string label)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.showMixedValue = toggle.hasMultipleDifferentValues;
            bool nextValue = EditorGUILayout.ToggleLeft(label, toggle.boolValue);
            EditorGUI.showMixedValue = false;
            if (nextValue != toggle.boolValue)
                toggle.boolValue = nextValue;
        }
    }

    void CopyBodyTextTopOffsetFromScene()
    {
        StoryTextLayoutLock bodyTextLock = FindSceneBodyTextLock();
        if (bodyTextLock == null)
        {
            EditorUtility.DisplayDialog("Story UI Style", "В открытой сцене не найден StoryTextLayoutLock для BodyText.", "OK");
            return;
        }

        Undo.RecordObjects(targets, "Copy BodyText Top Offset To Style");
        _overrideBodyTextTopOffsetY.boolValue = true;
        _bodyTextTopOffsetY.floatValue = bodyTextLock.EffectiveTopOffsetY;
        serializedObject.ApplyModifiedProperties();
        MarkTargetsDirty();
        ApplyTargetsToOpenScene();
    }

    void CopyDialogueSpriteFromScene()
    {
        DialogueUIManager manager = FindSceneDialogueManager();
        Image background = manager != null ? manager.DialogueBackgroundImage : null;
        if (background == null || background.sprite == null)
        {
            EditorUtility.DisplayDialog("Story UI Style", "В открытой сцене не найден Sprite диалоговой плашки.", "OK");
            return;
        }

        Undo.RecordObjects(targets, "Copy Dialogue Sprite To Style");
        _backgroundSprite.objectReferenceValue = background.sprite;
        if (_backgroundSpriteSource != null)
            _backgroundSpriteSource.objectReferenceValue = background.sprite;
        serializedObject.ApplyModifiedProperties();
        MarkTargetsDirty();
        ApplyTargetsToOpenScene();
    }

    void CopyNameScreenBackgroundFromScene()
    {
        PreStorySetupFlow setup = FindSceneSetupFlow();
        Image background = setup != null ? setup.NameScreenBackgroundImage : null;
        if (background == null || background.sprite == null)
        {
            EditorUtility.DisplayDialog("Story UI Style", "В открытой сцене не найден большой фон экрана ввода имени со Sprite.", "OK");
            return;
        }

        Undo.RecordObjects(targets, "Copy Name Screen Background To Style");
        _nameScreenBackgroundSprite.objectReferenceValue = background.sprite;
        if (_nameScreenBackgroundSpriteSource != null)
            _nameScreenBackgroundSpriteSource.objectReferenceValue = background.sprite;
        _overrideNameScreenBackgroundColor.boolValue = true;
        _nameScreenBackgroundColor.colorValue = background.color;
        _overrideNameScreenBackgroundImageType.boolValue = true;
        _nameScreenBackgroundImageType.enumValueIndex = (int)background.type;
        serializedObject.ApplyModifiedProperties();
        MarkTargetsDirty();
        ApplyTargetsToOpenScene();
    }

    void CopyDialoguePanelRectFromScene()
    {
        DialogueUIManager manager = FindSceneDialogueManager();
        RectTransform panelRect = manager != null ? manager.DialoguePanelRect : null;
        if (panelRect == null)
        {
            EditorUtility.DisplayDialog("Story UI Style", "В открытой сцене не найден RectTransform диалоговой плашки.", "OK");
            return;
        }

        Undo.RecordObjects(targets, "Copy Dialogue Panel Rect To Style");
        _overrideDialoguePanelRect.boolValue = true;
        _dialoguePanelAnchoredPosition.vector2Value = panelRect.anchoredPosition;
        _dialoguePanelSizeDelta.vector2Value = panelRect.sizeDelta;
        serializedObject.ApplyModifiedProperties();
        MarkTargetsDirty();
        ApplyTargetsToOpenScene();
    }

    void CopyDialogueBackgroundRectFromScene()
    {
        DialogueUIManager manager = FindSceneDialogueManager();
        RectTransform backgroundRect = manager != null ? manager.DialogueBackgroundRect : null;
        if (backgroundRect == null)
        {
            EditorUtility.DisplayDialog("Story UI Style", "Dialogue background RectTransform was not found in the open scene.", "OK");
            return;
        }

        Undo.RecordObjects(targets, "Copy Dialogue Background Rect To Style");
        _overrideDialogueBackgroundAnchors.boolValue = true;
        _dialogueBackgroundAnchorMin.vector2Value = backgroundRect.anchorMin;
        _dialogueBackgroundAnchorMax.vector2Value = backgroundRect.anchorMax;
        _overrideDialogueBackgroundPivot.boolValue = true;
        _dialogueBackgroundPivot.vector2Value = backgroundRect.pivot;
        _overrideDialogueBackgroundRect.boolValue = true;
        if (IsFullStretchRect(backgroundRect))
        {
            Vector4 offsets = ReadStretchOffsets(backgroundRect);
            _dialogueBackgroundAnchoredPosition.vector2Value = new Vector2(offsets.x, offsets.w);
            _dialogueBackgroundSizeDelta.vector2Value = new Vector2(offsets.y, offsets.z);
        }
        else
        {
            _dialogueBackgroundAnchoredPosition.vector2Value = backgroundRect.anchoredPosition;
            _dialogueBackgroundSizeDelta.vector2Value = backgroundRect.sizeDelta;
        }
        _overrideDialogueBackgroundStretchOffsets.boolValue = false;
        _dialogueBackgroundStretchOffsets.vector4Value = Vector4.zero;
        serializedObject.ApplyModifiedProperties();
        MarkTargetsDirty();
        ApplyTargetsToOpenScene();
    }

    void CopyDialoguePanelLayoutFromScene()
    {
        DialogueUIManager manager = FindSceneDialogueManager();
        VerticalLayoutGroup layoutGroup = manager != null ? manager.DialoguePanelVerticalLayoutGroup : null;
        if (layoutGroup == null)
        {
            EditorUtility.DisplayDialog("Story UI Style", "DialoguePanel VerticalLayoutGroup was not found in the open scene.", "OK");
            return;
        }

        Undo.RecordObjects(targets, "Copy Dialogue Panel Layout To Style");
        _overrideDialoguePanelVerticalLayout.boolValue = true;
        SetRectOffset(_dialoguePanelVerticalLayoutPadding, layoutGroup.padding);
        _dialoguePanelVerticalLayoutSpacing.floatValue = layoutGroup.spacing;
        _dialoguePanelVerticalLayoutChildAlignment.enumValueIndex = (int)layoutGroup.childAlignment;
        _dialoguePanelVerticalLayoutReverseArrangement.boolValue = layoutGroup.reverseArrangement;
        _dialoguePanelVerticalLayoutControlChildWidth.boolValue = layoutGroup.childControlWidth;
        _dialoguePanelVerticalLayoutControlChildHeight.boolValue = layoutGroup.childControlHeight;
        _dialoguePanelVerticalLayoutUseChildScaleWidth.boolValue = layoutGroup.childScaleWidth;
        _dialoguePanelVerticalLayoutUseChildScaleHeight.boolValue = layoutGroup.childScaleHeight;
        _dialoguePanelVerticalLayoutChildForceExpandWidth.boolValue = layoutGroup.childForceExpandWidth;
        _dialoguePanelVerticalLayoutChildForceExpandHeight.boolValue = layoutGroup.childForceExpandHeight;

        ContentSizeFitter fitter = manager.DialoguePanelContentSizeFitter;
        if (fitter != null)
        {
            _overrideDialoguePanelContentSizeFitter.boolValue = true;
            _dialoguePanelContentSizeFitterHorizontalFit.enumValueIndex = (int)fitter.horizontalFit;
            _dialoguePanelContentSizeFitterVerticalFit.enumValueIndex = (int)fitter.verticalFit;
        }

        serializedObject.ApplyModifiedProperties();
        MarkTargetsDirty();
        ApplyTargetsToOpenScene();
    }

    void CopyNamePlateFromScene()
    {
        DialogueUIManager manager = FindSceneDialogueManager();
        Image image = manager != null ? manager.NamePlateImage : null;
        RectTransform rect = manager != null ? manager.NamePlateRect : null;

        if (image == null && rect == null)
        {
            EditorUtility.DisplayDialog("Story UI Style", "В открытой сцене не найден NamePlate Image или RectTransform.", "OK");
            return;
        }

        Undo.RecordObjects(targets, "Copy NamePlate To Style");

        if (image != null)
        {
            SVGImage svgImage = image.GetComponent<SVGImage>();
            bool useSvg = svgImage != null && svgImage.enabled;
            Sprite sprite = useSvg && svgImage.sprite != null ? svgImage.sprite : image.sprite;

            _namePlateSprite.objectReferenceValue = sprite;
            if (_namePlateSpriteSource != null)
                _namePlateSpriteSource.objectReferenceValue = ResolveSourceForFinalSprite(sprite) ?? sprite;

            _overrideNamePlateColor.boolValue = true;
            _namePlateColor.colorValue = useSvg ? svgImage.color : image.color;
            _overrideNamePlateImageType.boolValue = true;
            _namePlateImageType.enumValueIndex = (int)image.type;
            _overrideNamePlatePreserveAspect.boolValue = true;
            _namePlatePreserveAspect.boolValue = useSvg ? svgImage.preserveAspect : image.preserveAspect;
            _overrideNamePlatePixelsPerUnitMultiplier.boolValue = true;
            _namePlatePixelsPerUnitMultiplier.floatValue = Mathf.Max(0.01f, image.pixelsPerUnitMultiplier);
            _overrideNamePlateMaterial.boolValue = image.material != null || (useSvg && svgImage.material != null);
            _namePlateMaterial.objectReferenceValue = useSvg && svgImage.material != null ? svgImage.material : image.material;
            _overrideNamePlateRaycastTarget.boolValue = true;
            _namePlateRaycastTarget.boolValue = useSvg ? svgImage.raycastTarget : image.raycastTarget;
        }

        if (rect != null)
        {
            _overrideNamePlateAnchors.boolValue = true;
            _namePlateAnchorMin.vector2Value = rect.anchorMin;
            _namePlateAnchorMax.vector2Value = rect.anchorMax;
            _overrideNamePlatePivot.boolValue = true;
            _namePlatePivot.vector2Value = rect.pivot;
            _overrideNamePlateRect.boolValue = true;
            _namePlateAnchoredPosition.vector2Value = rect.anchoredPosition;
            _namePlateSizeDelta.vector2Value = rect.sizeDelta;
        }

        serializedObject.ApplyModifiedProperties();
        MarkTargetsDirty();
        ApplyTargetsToOpenScene();
    }

    void CopyDialogueExtraLayersFromScene()
    {
        DialogueUIManager manager = FindSceneDialogueManager();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Story UI Style", "В открытой сцене не найден DialogueUIManager.", "OK");
            return;
        }

        Undo.RecordObject(manager, "Collect Dialogue Extra Layers");
        manager.RefreshDialogueExtraBackgroundImagesFromScene();
        EditorUtility.SetDirty(manager);

        IReadOnlyList<Image> layers = manager.DialogueExtraBackgroundImages;
        if (layers == null || layers.Count == 0)
        {
            EditorUtility.DisplayDialog("Story UI Style", "Доп. Image-слои не найдены. Назови объект вроде Background (1), ExtraLayer, Overlay или добавь его в список ссылок сцены вручную.", "OK");
            return;
        }

        Undo.RecordObjects(targets, "Copy Dialogue Extra Layers To Style");
        _dialogueExtraLayers.ClearArray();

        for (int i = 0; i < layers.Count; i++)
        {
            Image image = layers[i];
            if (image == null)
                continue;

            _dialogueExtraLayers.InsertArrayElementAtIndex(_dialogueExtraLayers.arraySize);
            SerializedProperty item = _dialogueExtraLayers.GetArrayElementAtIndex(_dialogueExtraLayers.arraySize - 1);
            RectTransform rect = image.rectTransform;

            item.FindPropertyRelative("_enabled").boolValue = image.gameObject.activeSelf;
            item.FindPropertyRelative("_targetPath").stringValue = GetRelativePath(manager.DialoguePanelRect != null ? manager.DialoguePanelRect.transform : null, image.transform);
            item.FindPropertyRelative("_targetName").stringValue = image.name;
            item.FindPropertyRelative("_sprite").objectReferenceValue = image.sprite;
            item.FindPropertyRelative("_spriteSource").objectReferenceValue = image.sprite;
            item.FindPropertyRelative("_overrideColor").boolValue = true;
            item.FindPropertyRelative("_color").colorValue = image.color;
            item.FindPropertyRelative("_overrideImageType").boolValue = true;
            item.FindPropertyRelative("_imageType").enumValueIndex = (int)image.type;
            item.FindPropertyRelative("_overrideRect").boolValue = true;
            item.FindPropertyRelative("_anchoredPosition").vector2Value = rect != null ? rect.anchoredPosition : Vector2.zero;
            item.FindPropertyRelative("_sizeDelta").vector2Value = rect != null ? rect.sizeDelta : Vector2.zero;
            SerializedProperty matchAutoHeight = item.FindPropertyRelative("_matchDialoguePanelAutoHeight");
            if (matchAutoHeight != null)
                matchAutoHeight.boolValue = true;
            item.FindPropertyRelative("_overrideRaycastTarget").boolValue = true;
            item.FindPropertyRelative("_raycastTarget").boolValue = image.raycastTarget;
        }

        serializedObject.ApplyModifiedProperties();
        MarkTargetsDirty();
        ApplyTargetsToOpenScene();
    }

    void CopyStatBackgroundRectFromScene()
    {
        StatChangeOverlay overlay = FindSceneStatOverlay();
        Image panelBackground = overlay != null ? overlay.PanelBackgroundImage : null;
        RectTransform backgroundRect = panelBackground != null ? panelBackground.rectTransform : null;
        if (backgroundRect == null)
        {
            EditorUtility.DisplayDialog("Story UI Style", "Stats background RectTransform was not found in the open scene.", "OK");
            return;
        }

        Undo.RecordObjects(targets, "Copy Stat Background Rect To Style");
        _overrideStatPanelBackgroundAnchors.boolValue = true;
        _statPanelBackgroundAnchorMin.vector2Value = backgroundRect.anchorMin;
        _statPanelBackgroundAnchorMax.vector2Value = backgroundRect.anchorMax;
        _overrideStatPanelBackgroundPivot.boolValue = true;
        _statPanelBackgroundPivot.vector2Value = backgroundRect.pivot;
        _overrideStatPanelBackgroundStretchOffsets.boolValue = true;
        _statPanelBackgroundStretchOffsets.vector4Value = ReadStretchOffsets(backgroundRect);
        serializedObject.ApplyModifiedProperties();
        MarkTargetsDirty();
        ApplyTargetsToOpenScene();
    }

    void CopyRelationshipBackgroundRectFromScene()
    {
        StatChangeOverlay overlay = FindSceneStatOverlay();
        Image panelBackground = overlay != null ? overlay.PanelBackgroundImage : null;
        RectTransform backgroundRect = panelBackground != null ? panelBackground.rectTransform : null;
        if (backgroundRect == null)
        {
            EditorUtility.DisplayDialog("Story UI Style", "Relationship background RectTransform was not found in the open scene.", "OK");
            return;
        }

        Undo.RecordObjects(targets, "Copy Relationship Background Rect To Style");
        _overrideRelationshipPanelBackgroundAnchors.boolValue = true;
        _relationshipPanelBackgroundAnchorMin.vector2Value = backgroundRect.anchorMin;
        _relationshipPanelBackgroundAnchorMax.vector2Value = backgroundRect.anchorMax;
        _overrideRelationshipPanelBackgroundPivot.boolValue = true;
        _relationshipPanelBackgroundPivot.vector2Value = backgroundRect.pivot;
        _overrideRelationshipPanelBackgroundRect.boolValue = true;
        _relationshipPanelBackgroundAnchoredPosition.vector2Value = backgroundRect.anchoredPosition;
        _relationshipPanelBackgroundSizeDelta.vector2Value = backgroundRect.sizeDelta;
        _overrideRelationshipPanelBackgroundStretchOffsets.boolValue = true;
        _relationshipPanelBackgroundStretchOffsets.vector4Value = ReadStretchOffsets(backgroundRect);
        serializedObject.ApplyModifiedProperties();
        MarkTargetsDirty();
        ApplyTargetsToOpenScene();
    }

    void CopyRelationshipLayoutFromScene()
    {
        StatChangeOverlay overlay = FindSceneStatOverlay();
        if (overlay == null)
        {
            EditorUtility.DisplayDialog("Story UI Style", "StatChangeOverlay was not found in the open scene.", "OK");
            return;
        }

        RectTransform frameRect = overlay.RelationshipFrameRect;
        VerticalLayoutGroup layoutGroup = overlay.PanelVerticalLayoutGroup;
        ContentSizeFitter fitter = overlay.PanelContentSizeFitter;

        Undo.RecordObjects(targets, "Copy Relationship Layout To Style");
        _overrideRelationshipFrameSize.boolValue = frameRect != null;
        if (frameRect != null)
        {
            _relationshipFrameAnchoredPosition.vector2Value = frameRect.anchoredPosition;
            _relationshipFrameSize.vector2Value = frameRect.sizeDelta;
        }

        _overrideRelationshipPanelVerticalLayout.boolValue = layoutGroup != null;
        if (layoutGroup != null)
        {
            SetRectOffset(_relationshipPanelVerticalLayoutPadding, layoutGroup.padding);
            _relationshipPanelVerticalLayoutSpacing.floatValue = layoutGroup.spacing;
            _relationshipPanelVerticalLayoutChildAlignment.enumValueIndex = (int)layoutGroup.childAlignment;
            _relationshipPanelVerticalLayoutReverseArrangement.boolValue = layoutGroup.reverseArrangement;
            _relationshipPanelVerticalLayoutControlChildWidth.boolValue = layoutGroup.childControlWidth;
            _relationshipPanelVerticalLayoutControlChildHeight.boolValue = layoutGroup.childControlHeight;
            _relationshipPanelVerticalLayoutUseChildScaleWidth.boolValue = layoutGroup.childScaleWidth;
            _relationshipPanelVerticalLayoutUseChildScaleHeight.boolValue = layoutGroup.childScaleHeight;
            _relationshipPanelVerticalLayoutChildForceExpandWidth.boolValue = layoutGroup.childForceExpandWidth;
            _relationshipPanelVerticalLayoutChildForceExpandHeight.boolValue = layoutGroup.childForceExpandHeight;
        }

        _overrideRelationshipPanelContentSizeFitter.boolValue = fitter != null;
        if (fitter != null)
        {
            _relationshipPanelContentSizeFitterHorizontalFit.enumValueIndex = (int)fitter.horizontalFit;
            _relationshipPanelContentSizeFitterVerticalFit.enumValueIndex = (int)fitter.verticalFit;
        }

        serializedObject.ApplyModifiedProperties();
        MarkTargetsDirty();
        ApplyTargetsToOpenScene();
    }

    void CopyStatLayoutFromScene()
    {
        StatChangeOverlay overlay = FindSceneStatOverlay();
        if (overlay == null)
        {
            EditorUtility.DisplayDialog("Story UI Style", "В открытой сцене не найден StatChangeOverlay.", "OK");
            return;
        }

        StatLayoutSnapshot layout = ReadStatLayout(overlay);

        Undo.RecordObjects(targets, "Copy Stat Layout To Style");
        _overrideStatPanelRect.boolValue = layout.HasPanelRect;
        _statPanelAnchoredPosition.vector2Value = layout.PanelAnchoredPosition;
        _statPanelSizeDelta.vector2Value = layout.PanelSizeDelta;
        _overrideStatPanelBackgroundAnchors.boolValue = layout.HasPanelBackgroundRect;
        _statPanelBackgroundAnchorMin.vector2Value = layout.PanelBackgroundAnchorMin;
        _statPanelBackgroundAnchorMax.vector2Value = layout.PanelBackgroundAnchorMax;
        _overrideStatPanelBackgroundPivot.boolValue = layout.HasPanelBackgroundRect;
        _statPanelBackgroundPivot.vector2Value = layout.PanelBackgroundPivot;
        _overrideStatPanelBackgroundStretchOffsets.boolValue = layout.HasPanelBackgroundRect;
        _statPanelBackgroundStretchOffsets.vector4Value = layout.PanelBackgroundStretchOffsets;
        _overrideStatTextRect.boolValue = layout.HasTextRect;
        _statTextAnchoredPosition.vector2Value = layout.TextAnchoredPosition;
        _statTextSizeDelta.vector2Value = layout.TextSizeDelta;
        _overrideStatPanelPadding.boolValue = layout.HasPanelPadding;
        _statPanelPadding.vector2Value = layout.PanelPadding;
        _overrideStatIconSize.boolValue = layout.IconSize.x > 0f || layout.IconSize.y > 0f;
        _statIconSize.vector2Value = layout.IconSize;
        _overrideStatIconOffset.boolValue = true;
        _statIconOffset.vector2Value = layout.IconOffset;
        _overrideStatIconVisualScale.boolValue = layout.OverrideIconVisualScale;
        _statIconVisualScale.vector2Value = layout.IconVisualScale;
        _overrideStatIconMinSize.boolValue = layout.IconMinSize.x > 0f || layout.IconMinSize.y > 0f;
        _statIconMinSize.vector2Value = layout.IconMinSize;
        _overrideStatIconReserveSpaceWhenHidden.boolValue = true;
        _statIconReserveSpaceWhenHidden.boolValue = layout.ReserveIconSpaceWhenHidden;
        _overrideStatIconParentSpacing.boolValue = layout.OverrideParentSpacing;
        _statIconParentSpacing.floatValue = layout.ParentSpacing;
        _overrideStatIconParentPadding.boolValue = layout.OverrideParentPadding;
        SetRectOffset(_statIconParentPadding, layout.ParentPadding);
        _overrideStatPanelVerticalLayout.boolValue = layout.HasVerticalLayout;
        SetRectOffset(_statPanelVerticalLayoutPadding, layout.VerticalLayoutPadding);
        _statPanelVerticalLayoutSpacing.floatValue = layout.VerticalLayoutSpacing;
        _statPanelVerticalLayoutChildAlignment.enumValueIndex = (int)layout.VerticalLayoutChildAlignment;
        _statPanelVerticalLayoutReverseArrangement.boolValue = layout.VerticalLayoutReverseArrangement;
        _statPanelVerticalLayoutControlChildWidth.boolValue = layout.VerticalLayoutControlChildWidth;
        _statPanelVerticalLayoutControlChildHeight.boolValue = layout.VerticalLayoutControlChildHeight;
        _statPanelVerticalLayoutUseChildScaleWidth.boolValue = layout.VerticalLayoutUseChildScaleWidth;
        _statPanelVerticalLayoutUseChildScaleHeight.boolValue = layout.VerticalLayoutUseChildScaleHeight;
        _statPanelVerticalLayoutChildForceExpandWidth.boolValue = layout.VerticalLayoutChildForceExpandWidth;
        _statPanelVerticalLayoutChildForceExpandHeight.boolValue = layout.VerticalLayoutChildForceExpandHeight;
        _overrideStatPanelContentSizeFitter.boolValue = layout.HasContentSizeFitter;
        _statPanelContentSizeFitterHorizontalFit.enumValueIndex = (int)layout.ContentSizeFitterHorizontalFit;
        _statPanelContentSizeFitterVerticalFit.enumValueIndex = (int)layout.ContentSizeFitterVerticalFit;

        serializedObject.ApplyModifiedProperties();
        MarkTargetsDirty();
        ApplyTargetsToOpenScene();
    }

    void OpenPreviewForCurrentStyle()
    {
        serializedObject.ApplyModifiedProperties();

        StoryUiStyle style = target as StoryUiStyle;
        if (style == null)
            return;

        if (!TryFindStyleContext(style, out StyleContext context))
        {
            EditorUtility.DisplayDialog(
                "Story UI Style",
                "Этот style еще не подключен к Story UI Catalog. Открой каталог, назначь style нужной истории и нажми Preview там или здесь.",
                "OK");
            StoryInterfaceStyleCatalogEditor.SelectDefaultCatalog();
            return;
        }

        StoryInterfacePreviewWindow.OpenForStory(
            context.Catalog,
            context.Story,
            context.StoryId,
            context.Library);
    }

    static void AutoBindSceneUiReferences(bool forceRefresh)
    {
        int changed = 0;

        DialogueUIManager[] dialogueManagers = UnityEngine.Object.FindObjectsOfType<DialogueUIManager>(true);
        for (int i = 0; i < dialogueManagers.Length; i++)
        {
            DialogueUIManager manager = dialogueManagers[i];
            if (manager == null)
                continue;

            _ = manager.DialoguePanelRect;
            _ = manager.DialogueBackgroundImage;
            _ = manager.NamePlateImage;
            manager.RefreshDialogueExtraBackgroundImagesFromScene();
            EditorUtility.SetDirty(manager);
        }

        PreStorySetupFlow[] setupFlows = UnityEngine.Object.FindObjectsOfType<PreStorySetupFlow>(true);
        for (int i = 0; i < setupFlows.Length; i++)
        {
            PreStorySetupFlow setupFlow = setupFlows[i];
            if (setupFlow == null)
                continue;

            _ = setupFlow.NameScreenBackgroundImage;
            _ = setupFlow.NamePanelBackgroundImage;
            _ = setupFlow.NameInputField;
            _ = setupFlow.NamePlaceholderText;
            _ = setupFlow.NameConfirmButton;
            EditorUtility.SetDirty(setupFlow);
        }

        StatChangeOverlay[] statOverlays = UnityEngine.Object.FindObjectsOfType<StatChangeOverlay>(true);
        for (int i = 0; i < statOverlays.Length; i++)
        {
            if (AutoBindStatOverlay(statOverlays[i], forceRefresh))
                changed++;
        }

        ChapterTitleOverlay[] chapterOverlays = UnityEngine.Object.FindObjectsOfType<ChapterTitleOverlay>(true);
        for (int i = 0; i < chapterOverlays.Length; i++)
        {
            if (AutoBindChapterOverlay(chapterOverlays[i], forceRefresh))
                changed++;
        }

        if (forceRefresh)
        {
            Canvas.ForceUpdateCanvases();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            EditorUtility.DisplayDialog("Story UI Style", $"Ссылки UI проверены. Обновлено компонентов: {changed}.", "OK");
        }
    }

    static bool AutoBindStatOverlay(StatChangeOverlay overlay, bool force)
    {
        if (overlay == null)
            return false;

        var serialized = new SerializedObject(overlay);
        serialized.Update();

        Transform root = overlay.transform;
        bool changed = false;
        changed |= SetObjectReference(serialized, "_panelRect", root as RectTransform, force);
        changed |= SetObjectReference(serialized, "_messageText", FindBestComponent<TMP_Text>(root, IsUsableText, "stat", "message", "body", "text"), force);
        changed |= SetObjectReference(serialized, "_iconImage", FindBestComponent<Image>(root, IsUsableStatIcon, "icon", "stat", "image"), force);
        changed |= SetObjectReference(serialized, "_canvasGroup", overlay.GetComponent<CanvasGroup>() ?? overlay.GetComponentInChildren<CanvasGroup>(true), force);
        changed |= SetObjectReference(serialized, "_rootObject", overlay.gameObject, force);

        if (changed)
        {
            Undo.RecordObject(overlay, "Auto Bind Stat UI References");
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(overlay);
        }

        return changed;
    }

    static bool AutoBindChapterOverlay(ChapterTitleOverlay overlay, bool force)
    {
        if (overlay == null)
            return false;

        var serialized = new SerializedObject(overlay);
        serialized.Update();

        Transform root = overlay.transform;
        bool changed = false;
        changed |= SetObjectReference(serialized, "_panelRect", root as RectTransform, force);
        changed |= SetObjectReference(serialized, "_titleText", FindBestComponent<TMP_Text>(root, IsUsableText, "chapter", "title", "body", "text"), force);
        changed |= SetObjectReference(serialized, "_canvasGroup", overlay.GetComponent<CanvasGroup>() ?? overlay.GetComponentInChildren<CanvasGroup>(true), force);
        changed |= SetObjectReference(serialized, "_rootObject", overlay.gameObject, force);
        changed |= SetObjectReference(serialized, "_backgroundDimImage", FindBestComponent<Image>(root, IsLikelyDimImage, "dim", "fade", "dark", "background"), false);

        Image dimImage = ReadObjectReference<Image>(serialized, "_backgroundDimImage");
        CanvasGroup dimGroup = dimImage != null ? dimImage.GetComponent<CanvasGroup>() : null;
        changed |= SetObjectReference(serialized, "_backgroundDimCanvasGroup", dimGroup, false);

        if (changed)
        {
            Undo.RecordObject(overlay, "Auto Bind Chapter UI References");
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(overlay);
        }

        return changed;
    }

    void ApplyTargetsToOpenScene()
    {
        foreach (UnityEngine.Object selectedTarget in targets)
        {
            StoryUiStyle style = selectedTarget as StoryUiStyle;
            if (style == null)
                continue;

            EditorUtility.SetDirty(style);
            ApplyStyleToOpenScene(style);
            StoryInterfacePreviewWindow.RefreshOpenLivePreviewsForStyle(style);
        }
    }

    static void ApplyStyleToOpenScene(StoryUiStyle style)
    {
        if (style == null)
            return;

        TryFindStyleContext(style, out StyleContext context);
        Sprite backgroundSprite = context.BackgroundSprite;
        string storyId = context.StoryId;

        DialogueUIManager[] dialogueManagers = UnityEngine.Object.FindObjectsOfType<DialogueUIManager>(true);
        for (int i = 0; i < dialogueManagers.Length; i++)
        {
            DialogueUIManager dialogueManager = dialogueManagers[i];
            if (dialogueManager == null)
                continue;

            Undo.RecordObject(dialogueManager, "Apply Story UI Style");
            dialogueManager.ApplyStoryUiStyle(style, backgroundSprite);
            EditorUtility.SetDirty(dialogueManager);
        }

        StatChangeOverlay[] statOverlays = UnityEngine.Object.FindObjectsOfType<StatChangeOverlay>(true);
        for (int i = 0; i < statOverlays.Length; i++)
        {
            StatChangeOverlay overlay = statOverlays[i];
            if (overlay == null)
                continue;

            Undo.RecordObject(overlay, "Apply Story UI Style");
            overlay.ApplyStoryUiStyle(style, storyId);
            EditorUtility.SetDirty(overlay);
        }

        ChapterTitleOverlay[] chapterOverlays = UnityEngine.Object.FindObjectsOfType<ChapterTitleOverlay>(true);
        for (int i = 0; i < chapterOverlays.Length; i++)
        {
            ChapterTitleOverlay overlay = chapterOverlays[i];
            if (overlay == null)
                continue;

            Undo.RecordObject(overlay, "Apply Story UI Style");
            overlay.ApplyStoryUiStyle(style);
            EditorUtility.SetDirty(overlay);
        }

        PreStorySetupFlow[] setupFlows = UnityEngine.Object.FindObjectsOfType<PreStorySetupFlow>(true);
        for (int i = 0; i < setupFlows.Length; i++)
        {
            PreStorySetupFlow setupFlow = setupFlows[i];
            if (setupFlow == null)
                continue;

            Undo.RecordObject(setupFlow, "Apply Story UI Style");
            setupFlow.ApplyStoryUiStyle(style, storyId);
            EditorUtility.SetDirty(setupFlow);
        }

        Canvas.ForceUpdateCanvases();
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    static StatChangeOverlay FindSceneStatOverlay()
    {
        StatChangeOverlay[] overlays = UnityEngine.Object.FindObjectsOfType<StatChangeOverlay>(true);
        if (overlays == null || overlays.Length == 0)
            return null;

        for (int i = 0; i < overlays.Length; i++)
        {
            if (overlays[i] != null && overlays[i].name.IndexOf("StatChangeOverlay", StringComparison.OrdinalIgnoreCase) >= 0)
                return overlays[i];
        }

        return overlays[0];
    }

    static ChapterTitleOverlay FindSceneChapterOverlay()
    {
        ChapterTitleOverlay[] overlays = UnityEngine.Object.FindObjectsOfType<ChapterTitleOverlay>(true);
        if (overlays == null || overlays.Length == 0)
            return null;

        for (int i = 0; i < overlays.Length; i++)
        {
            if (overlays[i] != null && overlays[i].name.IndexOf("Chapter", StringComparison.OrdinalIgnoreCase) >= 0)
                return overlays[i];
        }

        return overlays[0];
    }

    static PreStorySetupFlow FindSceneSetupFlow()
    {
        PreStorySetupFlow[] setupFlows = UnityEngine.Object.FindObjectsOfType<PreStorySetupFlow>(true);
        if (setupFlows == null || setupFlows.Length == 0)
            return null;

        for (int i = 0; i < setupFlows.Length; i++)
        {
            if (setupFlows[i] != null && setupFlows[i].name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0)
                return setupFlows[i];
        }

        return setupFlows[0];
    }

    static DialogueUIManager FindSceneDialogueManager()
    {
        DialogueUIManager[] managers = UnityEngine.Object.FindObjectsOfType<DialogueUIManager>(true);
        if (managers == null || managers.Length == 0)
            return null;

        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] != null && managers[i].DialogueBackgroundImage != null)
                return managers[i];
        }

        return managers[0];
    }

    static PhoneDialogueUI FindScenePhoneUi()
    {
        PhoneDialogueUI[] phoneUis = UnityEngine.Object.FindObjectsOfType<PhoneDialogueUI>(true);
        if (phoneUis == null || phoneUis.Length == 0)
            return null;

        for (int i = 0; i < phoneUis.Length; i++)
        {
            if (phoneUis[i] != null && phoneUis[i].name.IndexOf("Phone", StringComparison.OrdinalIgnoreCase) >= 0)
                return phoneUis[i];
        }

        return phoneUis[0];
    }

    static StoryEndScreenController FindSceneEndScreen()
    {
        StoryEndScreenController[] endScreens = UnityEngine.Object.FindObjectsOfType<StoryEndScreenController>(true);
        if (endScreens == null || endScreens.Length == 0)
        {
            StoryManager manager = FindSceneStoryManager();
            return manager != null && manager.endStoryPanel != null
                ? manager.endStoryPanel.GetComponentInChildren<StoryEndScreenController>(true)
                : null;
        }

        for (int i = 0; i < endScreens.Length; i++)
        {
            StoryEndScreenController endScreen = endScreens[i];
            if (endScreen == null)
                continue;

            string name = endScreen.name;
            if (name.IndexOf("End", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Final", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Finish", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return endScreen;
            }
        }

        return endScreens[0];
    }

    static StoryManager FindSceneStoryManager()
    {
        StoryManager[] managers = UnityEngine.Object.FindObjectsOfType<StoryManager>(true);
        if (managers == null || managers.Length == 0)
            return null;

        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] != null && managers[i].gameObject.scene.IsValid())
                return managers[i];
        }

        return managers[0];
    }

    static StoryTextLayoutLock FindSceneBodyTextLock()
    {
        StoryTextLayoutLock[] locks = UnityEngine.Object.FindObjectsOfType<StoryTextLayoutLock>(true);
        if (locks == null || locks.Length == 0)
            return null;

        for (int i = 0; i < locks.Length; i++)
        {
            if (locks[i] != null && locks[i].name.IndexOf("BodyText", StringComparison.OrdinalIgnoreCase) >= 0)
                return locks[i];
        }

        return locks[0];
    }

    static StatLayoutSnapshot ReadStatLayout(StatChangeOverlay overlay)
    {
        var snapshot = new StatLayoutSnapshot();
        var source = new SerializedObject(overlay);

        Image iconImage = ReadObject<Image>(source, "_iconImage");
        float iconWidth = ReadFloat(source, "_iconWidth");
        float iconHeight = ReadFloat(source, "_iconHeight");
        Vector2 iconSize = new Vector2(iconWidth, iconHeight);
        if ((Mathf.Abs(iconSize.x) < 0.01f || Mathf.Abs(iconSize.y) < 0.01f) && iconImage != null)
        {
            Vector2 rectSize = iconImage.rectTransform.rect.size;
            if (Mathf.Abs(iconSize.x) < 0.01f)
                iconSize.x = rectSize.x;
            if (Mathf.Abs(iconSize.y) < 0.01f)
                iconSize.y = rectSize.y;
        }

        snapshot.IconSize = iconSize;
        snapshot.IconOffset = ReadVector2(source, "_iconAnchoredOffset");
        snapshot.OverrideIconVisualScale = ReadBool(source, "_applyIconVisualScale");
        snapshot.IconVisualScale = ReadVector2(source, "_iconVisualScale", Vector2.one);
        snapshot.IconMinSize = new Vector2(ReadFloat(source, "_iconMinWidth"), ReadFloat(source, "_iconMinHeight"));
        snapshot.ReserveIconSpaceWhenHidden = ReadBool(source, "_reserveIconSpaceWhenHidden");
        snapshot.OverrideParentSpacing = ReadBool(source, "_applyParentLayoutSpacing");
        snapshot.ParentSpacing = ReadFloat(source, "_parentLayoutSpacing");
        snapshot.OverrideParentPadding = ReadBool(source, "_applyParentLayoutPadding");
        snapshot.ParentPadding = ReadRectOffset(source.FindProperty("_parentLayoutPadding"));

        RectTransform panelRect = ReadObject<RectTransform>(source, "_panelRect");
        snapshot.HasPanelRect = panelRect != null;
        if (panelRect != null)
        {
            snapshot.PanelAnchoredPosition = panelRect.anchoredPosition;
            snapshot.PanelSizeDelta = panelRect.sizeDelta;
        }

        Image panelBackground = overlay.PanelBackgroundImage;
        RectTransform panelBackgroundRect = panelBackground != null ? panelBackground.rectTransform : null;
        snapshot.HasPanelBackgroundRect = panelBackgroundRect != null;
        if (panelBackgroundRect != null)
        {
            snapshot.PanelBackgroundAnchorMin = panelBackgroundRect.anchorMin;
            snapshot.PanelBackgroundAnchorMax = panelBackgroundRect.anchorMax;
            snapshot.PanelBackgroundPivot = panelBackgroundRect.pivot;
            snapshot.PanelBackgroundStretchOffsets = ReadStretchOffsets(panelBackgroundRect);
        }

        TMP_Text messageText = ReadObject<TMP_Text>(source, "_messageText");
        RectTransform textRect = messageText != null ? messageText.rectTransform : null;
        snapshot.HasTextRect = textRect != null;
        if (textRect != null)
        {
            snapshot.TextAnchoredPosition = textRect.anchoredPosition;
            snapshot.TextSizeDelta = textRect.sizeDelta;
        }

        ButtonTextAutoSize panelAutoSize = FindPanelAutoSizeDriver(panelRect != null ? panelRect : overlay.transform);
        snapshot.HasPanelPadding = panelAutoSize != null;
        snapshot.PanelPadding = panelAutoSize != null ? panelAutoSize.Padding : Vector2.zero;

        VerticalLayoutGroup layoutGroup = overlay.PanelVerticalLayoutGroup;
        snapshot.HasVerticalLayout = layoutGroup != null;
        if (layoutGroup != null)
        {
            snapshot.VerticalLayoutPadding = ReadRectOffset(layoutGroup.padding);
            snapshot.VerticalLayoutSpacing = layoutGroup.spacing;
            snapshot.VerticalLayoutChildAlignment = layoutGroup.childAlignment;
            snapshot.VerticalLayoutReverseArrangement = layoutGroup.reverseArrangement;
            snapshot.VerticalLayoutControlChildWidth = layoutGroup.childControlWidth;
            snapshot.VerticalLayoutControlChildHeight = layoutGroup.childControlHeight;
            snapshot.VerticalLayoutUseChildScaleWidth = layoutGroup.childScaleWidth;
            snapshot.VerticalLayoutUseChildScaleHeight = layoutGroup.childScaleHeight;
            snapshot.VerticalLayoutChildForceExpandWidth = layoutGroup.childForceExpandWidth;
            snapshot.VerticalLayoutChildForceExpandHeight = layoutGroup.childForceExpandHeight;
        }

        ContentSizeFitter fitter = overlay.PanelContentSizeFitter;
        snapshot.HasContentSizeFitter = fitter != null;
        if (fitter != null)
        {
            snapshot.ContentSizeFitterHorizontalFit = fitter.horizontalFit;
            snapshot.ContentSizeFitterVerticalFit = fitter.verticalFit;
        }

        return snapshot;
    }

    static ButtonTextAutoSize FindPanelAutoSizeDriver(Transform searchRoot)
    {
        if (searchRoot == null)
            return null;

        ButtonTextAutoSize[] autoSizeDrivers = searchRoot.GetComponentsInChildren<ButtonTextAutoSize>(true);
        for (int i = 0; i < autoSizeDrivers.Length; i++)
        {
            ButtonTextAutoSize autoSizeDriver = autoSizeDrivers[i];
            if (autoSizeDriver != null && autoSizeDriver.GetComponentInParent<Button>() == null)
                return autoSizeDriver;
        }

        return null;
    }

    static bool TryFindStyleContext(StoryUiStyle style, out StyleContext context)
    {
        context = default;
        if (style == null)
            return false;

        string[] guids = AssetDatabase.FindAssets("t:StoryInterfaceStyleCatalog");
        if (guids == null)
            return false;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            StoryInterfaceStyleCatalog catalog = AssetDatabase.LoadAssetAtPath<StoryInterfaceStyleCatalog>(path);
            if (catalog == null || catalog.Entries == null)
                continue;

            foreach (StoryInterfaceStyleEntry entry in catalog.Entries)
            {
                if (entry == null)
                    continue;

                bool isRegularStyle = entry.StoryUiStyle == style;
                bool isCutsceneStyle = entry.UseSeparateCutsceneStoryUiStyle && entry.CutsceneStoryUiStyle == style;
                if (!isRegularStyle && !isCutsceneStyle)
                    continue;

                string storyId = ResolveEntryStoryId(entry);
                StoryData story = entry.StoryAsset;
                context = new StyleContext
                {
                    Catalog = catalog,
                    Story = story,
                    StoryId = storyId,
                    BackgroundSprite = isCutsceneStyle ? entry.CutsceneDialogueBackgroundSprite : entry.DialogueBackgroundSprite,
                    Library = FindLibraryForStory(story, storyId)
                };
                return true;
            }
        }

        return false;
    }

    static StoryJsonAssetLibrary FindLibraryForStory(StoryData story, string storyId)
    {
        if (story != null && story.Chapters != null)
        {
            foreach (ChapterData chapter in story.Chapters)
            {
                if (chapter != null && chapter.JsonAssetLibrary != null)
                    return chapter.JsonAssetLibrary;
            }
        }

        string root = ResolveStoryRootFolder(story != null ? AssetDatabase.GetAssetPath(story) : "", storyId);
        string[] guids = AssetDatabase.FindAssets("t:StoryJsonAssetLibrary", new[] { root });
        if (guids != null && guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<StoryJsonAssetLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));

        return null;
    }

    static string ResolveEntryStoryId(StoryInterfaceStyleEntry entry)
    {
        if (entry == null)
            return "";

        if (entry.StoryIds != null)
        {
            foreach (string id in entry.StoryIds)
            {
                string normalized = Normalize(id);
                if (!string.IsNullOrWhiteSpace(normalized))
                    return normalized;
            }
        }

        if (entry.StoryAsset != null && !string.IsNullOrWhiteSpace(entry.StoryAsset.storyId))
            return Normalize(entry.StoryAsset.storyId);

        return Normalize(entry.Label);
    }

    static string ResolveStoryRootFolder(string assetPath, string storyId)
    {
        const string storiesRoot = "Assets/_MyProject/Data/Stories/";
        string normalized = (assetPath ?? "").Replace('\\', '/');
        int start = normalized.IndexOf(storiesRoot, StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            string rest = normalized.Substring(start + storiesRoot.Length);
            int slash = rest.IndexOf('/');
            if (slash > 0)
                return storiesRoot + rest.Substring(0, slash);
        }

        return storiesRoot + SafeFileName(storyId);
    }

    static string SafeFileName(string value)
    {
        value = Normalize(value);
        if (string.IsNullOrWhiteSpace(value))
            return "story";

        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value.Replace(' ', '_');
    }

    static string GetRelativePath(Transform root, Transform target)
    {
        if (target == null)
            return "";

        if (root == null || target == root)
            return target.name;

        var names = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    static bool SetObjectReference(SerializedObject source, string propertyName, UnityEngine.Object value, bool force)
    {
        if (source == null || value == null)
            return false;

        SerializedProperty property = source.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            return false;

        UnityEngine.Object current = property.objectReferenceValue;
        if (current == value)
            return false;

        if (!force && current != null)
            return false;

        property.objectReferenceValue = value;
        return true;
    }

    static T ReadObjectReference<T>(SerializedObject source, string propertyName) where T : UnityEngine.Object
    {
        if (source == null)
            return null;

        SerializedProperty property = source.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    static T FindBestComponent<T>(Transform root, Func<T, bool> filter, params string[] preferredNames) where T : Component
    {
        if (root == null)
            return null;

        T[] components = root.GetComponentsInChildren<T>(true);
        T fallback = null;
        T best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || (filter != null && !filter(component)))
                continue;

            fallback ??= component;
            int score = ScoreSceneReference(component.transform, preferredNames);
            if (score > bestScore)
            {
                bestScore = score;
                best = component;
            }
        }

        return best != null ? best : fallback;
    }

    static int ScoreSceneReference(Transform transform, params string[] preferredNames)
    {
        if (transform == null)
            return 0;

        string name = transform.name ?? "";
        string path = BuildTransformPath(transform);
        int score = 0;

        for (int i = 0; i < preferredNames.Length; i++)
        {
            string token = preferredNames[i];
            if (string.IsNullOrWhiteSpace(token))
                continue;

            int weight = (preferredNames.Length - i) * 10;
            if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                score += weight * 3;
            if (path.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                score += weight;
        }

        return score;
    }

    static string BuildTransformPath(Transform transform)
    {
        if (transform == null)
            return "";

        var parts = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    static bool IsUsableText(TMP_Text text)
    {
        return text != null && text.rectTransform != null;
    }

    static bool IsUsableStatIcon(Image image)
    {
        if (image == null)
            return false;

        string name = image.name ?? "";
        if (name.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("panel", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return true;
    }

    static bool IsLikelyDimImage(Image image)
    {
        if (image == null)
            return false;

        string path = BuildTransformPath(image.transform);
        return path.IndexOf("dim", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("fade", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("dark", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static T ReadObject<T>(SerializedObject source, string propertyName) where T : UnityEngine.Object
    {
        SerializedProperty property = source.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    static bool ReadBool(SerializedObject source, string propertyName)
    {
        SerializedProperty property = source.FindProperty(propertyName);
        return property != null && property.boolValue;
    }

    static float ReadFloat(SerializedObject source, string propertyName)
    {
        SerializedProperty property = source.FindProperty(propertyName);
        return property != null ? property.floatValue : 0f;
    }

    static Vector2 ReadVector2(SerializedObject source, string propertyName, Vector2 fallback = default)
    {
        SerializedProperty property = source.FindProperty(propertyName);
        return property != null ? property.vector2Value : fallback;
    }

    static Vector4 ReadStretchOffsets(RectTransform rect)
    {
        if (rect == null)
            return Vector4.zero;

        return new Vector4(
            rect.offsetMin.x,
            -rect.offsetMax.x,
            -rect.offsetMax.y,
            rect.offsetMin.y);
    }

    static bool IsFullStretchRect(RectTransform rect)
    {
        if (rect == null)
            return false;

        return !Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x) &&
               !Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y);
    }

    static RectOffset ReadRectOffset(SerializedProperty property)
    {
        if (property == null)
            return new RectOffset();

        return new RectOffset(
            ReadInt(property, "m_Left"),
            ReadInt(property, "m_Right"),
            ReadInt(property, "m_Top"),
            ReadInt(property, "m_Bottom"));
    }

    static RectOffset ReadRectOffset(RectOffset value)
    {
        if (value == null)
            return new RectOffset();

        return new RectOffset(value.left, value.right, value.top, value.bottom);
    }

    static int ReadInt(SerializedProperty parent, string propertyName)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        return property != null ? property.intValue : 0;
    }

    static void SetRectOffset(SerializedProperty property, RectOffset value)
    {
        if (property == null)
            return;

        SetInt(property, "m_Left", value != null ? value.left : 0);
        SetInt(property, "m_Right", value != null ? value.right : 0);
        SetInt(property, "m_Top", value != null ? value.top : 0);
        SetInt(property, "m_Bottom", value != null ? value.bottom : 0);
    }

    static void SetInt(SerializedProperty parent, string propertyName, int value)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
            property.intValue = value;
    }

    static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }

    void MarkTargetsDirty()
    {
        foreach (UnityEngine.Object selectedTarget in targets)
            EditorUtility.SetDirty(selectedTarget);
    }

    struct StyleContext
    {
        public StoryInterfaceStyleCatalog Catalog;
        public StoryData Story;
        public string StoryId;
        public Sprite BackgroundSprite;
        public StoryJsonAssetLibrary Library;
    }

    struct RelationshipCharacterOption
    {
        public string CharacterId;
        public string DisplayName;
        public string StatId;
        public string Label;
    }

    struct StatLayoutSnapshot
    {
        public bool HasPanelRect;
        public Vector2 PanelAnchoredPosition;
        public Vector2 PanelSizeDelta;
        public bool HasPanelBackgroundRect;
        public Vector2 PanelBackgroundAnchorMin;
        public Vector2 PanelBackgroundAnchorMax;
        public Vector2 PanelBackgroundPivot;
        public Vector4 PanelBackgroundStretchOffsets;
        public bool HasTextRect;
        public Vector2 TextAnchoredPosition;
        public Vector2 TextSizeDelta;
        public bool HasPanelPadding;
        public Vector2 PanelPadding;
        public Vector2 IconSize;
        public Vector2 IconOffset;
        public bool OverrideIconVisualScale;
        public Vector2 IconVisualScale;
        public Vector2 IconMinSize;
        public bool ReserveIconSpaceWhenHidden;
        public bool OverrideParentSpacing;
        public float ParentSpacing;
        public bool OverrideParentPadding;
        public RectOffset ParentPadding;
        public bool HasVerticalLayout;
        public RectOffset VerticalLayoutPadding;
        public float VerticalLayoutSpacing;
        public TextAnchor VerticalLayoutChildAlignment;
        public bool VerticalLayoutReverseArrangement;
        public bool VerticalLayoutControlChildWidth;
        public bool VerticalLayoutControlChildHeight;
        public bool VerticalLayoutUseChildScaleWidth;
        public bool VerticalLayoutUseChildScaleHeight;
        public bool VerticalLayoutChildForceExpandWidth;
        public bool VerticalLayoutChildForceExpandHeight;
        public bool HasContentSizeFitter;
        public ContentSizeFitter.FitMode ContentSizeFitterHorizontalFit;
        public ContentSizeFitter.FitMode ContentSizeFitterVerticalFit;
    }
}

