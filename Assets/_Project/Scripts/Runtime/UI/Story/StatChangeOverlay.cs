using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum StatChangeOverlayTextMode
{
    DeltaThenName,
    NameThenDelta,
    MessageOnly
}

public enum StatChangeOverlayAnimationMode
{
    FadeAndSlide,
    Pop,
    Instant
}

public enum StatChangeOverlaySlideDirection
{
    Up,
    Down,
    Left,
    Right
}

[Serializable]
public sealed class StatChangeOverlayDefinition
{
    public string statId;
    public string displayName;
    public Sprite icon;
}

[Serializable]
public sealed class StatIconOffsetOverride
{
    public string statId;
    public bool overrideIconOffset = true;
    public Vector2 iconOffset;

    public bool Matches(string value)
    {
        return overrideIconOffset && StoryStatId.EqualsCanonical(statId, value);
    }

    public void Validate()
    {
        statId = string.IsNullOrWhiteSpace(statId) ? "" : statId.Trim();
    }

    static string Normalize(string value) => StoryStatId.Normalize(value);
}

[Serializable]
public sealed class RelationshipMessageOverride
{
    public string statId;
    public bool overrideTargetText = true;
    public string targetText;
    public bool overrideImprovedText;
    [TextArea(1, 3)] public string improvedText = "Отношения {target} улучшились.";
    public bool overrideWorsenedText;
    [TextArea(1, 3)] public string worsenedText = "Отношения {target} ухудшились.";

    [Header("Text")]
    public bool overrideTextRect;
    public Vector2 textAnchoredPosition;
    public Vector2 textSizeDelta;
    public bool overrideTextColor;
    public Color textColor = Color.white;
    public bool overrideTextFont;
    public TMP_FontAsset textFont;
    public bool overrideTextFontSize;
    public float textFontSize = 54f;
    public bool overrideTextAutoSize;
    public bool textAutoSize = true;
    public bool overrideTextAutoFontSizeRange;
    public float minAutoFontSize = 42f;
    public float maxAutoFontSize = 54f;
    public bool overrideTextAlignment;
    public TextAlignmentOptions textAlignment = TextAlignmentOptions.Center;
    public bool overrideTextWordWrapping;
    public bool textWordWrapping = true;
    public bool overrideTextOverflowMode;
    public TextOverflowModes textOverflowMode = TextOverflowModes.Overflow;
    public bool overrideTextLineSpacing;
    public float textLineSpacing;
    public bool overrideTextMargins;
    public Vector4 textMargins;

    public bool Matches(string relationshipStatId)
    {
        return !string.IsNullOrWhiteSpace(statId) &&
               Normalize(statId) == Normalize(relationshipStatId);
    }

    public string Format(string fallbackTarget, bool improved)
    {
        string target = ResolveTargetText(fallbackTarget);
        string format = improved
            ? (overrideImprovedText && !string.IsNullOrWhiteSpace(improvedText) ? improvedText : "Отношения {target} улучшились.")
            : (overrideWorsenedText && !string.IsNullOrWhiteSpace(worsenedText) ? worsenedText : "Отношения {target} ухудшились.");

        return format
            .Replace("{target}", target)
            .Replace("{statId}", statId ?? "")
            .Trim();
    }

    public string ResolveTargetText(string fallbackTarget)
    {
        string value = overrideTargetText && !string.IsNullOrWhiteSpace(targetText)
            ? targetText
            : fallbackTarget;

        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().TrimEnd('.');
    }

    public void ApplyTo(TMP_Text text)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        if (overrideTextRect && rect != null)
        {
            rect.anchoredPosition = textAnchoredPosition;
            rect.sizeDelta = new Vector2(
                textSizeDelta.x > 0f ? textSizeDelta.x : rect.sizeDelta.x,
                textSizeDelta.y > 0f ? textSizeDelta.y : rect.sizeDelta.y);
        }

        if (overrideTextColor)
            text.color = textColor;
        if (overrideTextFont && textFont != null)
            text.font = textFont;
        if (overrideTextFontSize)
            text.fontSize = Mathf.Max(1f, textFontSize);
        if (overrideTextAutoSize)
            text.enableAutoSizing = textAutoSize;
        if (overrideTextAutoFontSizeRange)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(1f, minAutoFontSize);
            text.fontSizeMax = Mathf.Max(text.fontSizeMin, maxAutoFontSize);
        }
        if (overrideTextAlignment)
            text.alignment = textAlignment;
        if (overrideTextWordWrapping)
            text.enableWordWrapping = textWordWrapping;
        if (overrideTextOverflowMode)
            text.overflowMode = textOverflowMode;
        if (overrideTextLineSpacing)
            text.lineSpacing = textLineSpacing;
        if (overrideTextMargins)
            text.margin = textMargins;

        text.SetAllDirty();
        text.ForceMeshUpdate();
    }

    public RelationshipMessageOverride Clone()
    {
        return new RelationshipMessageOverride
        {
            statId = statId,
            overrideTargetText = overrideTargetText,
            targetText = targetText,
            overrideImprovedText = overrideImprovedText,
            improvedText = improvedText,
            overrideWorsenedText = overrideWorsenedText,
            worsenedText = worsenedText,
            overrideTextRect = overrideTextRect,
            textAnchoredPosition = textAnchoredPosition,
            textSizeDelta = textSizeDelta,
            overrideTextColor = overrideTextColor,
            textColor = textColor,
            overrideTextFont = overrideTextFont,
            textFont = textFont,
            overrideTextFontSize = overrideTextFontSize,
            textFontSize = textFontSize,
            overrideTextAutoSize = overrideTextAutoSize,
            textAutoSize = textAutoSize,
            overrideTextAutoFontSizeRange = overrideTextAutoFontSizeRange,
            minAutoFontSize = minAutoFontSize,
            maxAutoFontSize = maxAutoFontSize,
            overrideTextAlignment = overrideTextAlignment,
            textAlignment = textAlignment,
            overrideTextWordWrapping = overrideTextWordWrapping,
            textWordWrapping = textWordWrapping,
            overrideTextOverflowMode = overrideTextOverflowMode,
            textOverflowMode = textOverflowMode,
            overrideTextLineSpacing = overrideTextLineSpacing,
            textLineSpacing = textLineSpacing,
            overrideTextMargins = overrideTextMargins,
            textMargins = textMargins
        };
    }

    public void Validate()
    {
        statId = string.IsNullOrWhiteSpace(statId) ? "" : statId.Trim();
        targetText = string.IsNullOrWhiteSpace(targetText) ? "" : targetText.Trim();
        textFontSize = Mathf.Max(1f, textFontSize);
        minAutoFontSize = Mathf.Max(1f, minAutoFontSize);
        maxAutoFontSize = Mathf.Max(minAutoFontSize, maxAutoFontSize);
    }

    static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }
}

[Serializable]
public sealed class StatChangeOverlayStoryLayoutOverride
{
    [Tooltip("ID истории, например privychka_pritvoryatsya или only_the_heart_sees_clearly.")]
    [SerializeField] private string _storyId;

    [Header("Panel")]
    [SerializeField] private bool _overridePanelPadding;
    [SerializeField] private Vector2 _panelPadding = new Vector2(640f, 96f);

    [Header("Icon")]
    [SerializeField] private bool _overrideIconSize;
    [SerializeField] private Vector2 _iconSize;
    [SerializeField] private bool _overrideIconOffset;
    [SerializeField] private Vector2 _iconOffset;
    [SerializeField] private bool _overrideIconVisualScale;
    [SerializeField] private Vector2 _iconVisualScale = Vector2.one;
    [SerializeField] private bool _overrideIconMinSize;
    [SerializeField] private Vector2 _iconMinSize;
    [SerializeField] private bool _overrideReserveIconSpaceWhenHidden;
    [SerializeField] private bool _reserveIconSpaceWhenHidden;

    [Header("Spacing")]
    [Tooltip("Включи, если именно для этой истории нужен свой spacing между иконкой и текстом.")]
    [SerializeField] private bool _overrideIconParentSpacing;
    [SerializeField] private float _iconParentSpacing;
    [SerializeField] private bool _overrideIconParentPadding;
    [SerializeField] private RectOffset _iconParentPadding = new RectOffset();

    public bool Matches(string storyId)
    {
        return Normalize(_storyId) == Normalize(storyId);
    }

    public void Validate()
    {
        _storyId = Normalize(_storyId);
        _iconParentPadding ??= new RectOffset();
    }

    public void ApplyTo(StatChangeOverlay target)
    {
        if (target == null)
            return;

        Validate();
        target.ApplyLayoutOverrides(
            _overridePanelPadding,
            _panelPadding,
            _overrideIconSize,
            _iconSize,
            _overrideIconOffset,
            _iconOffset,
            _overrideIconVisualScale,
            _iconVisualScale,
            _overrideIconMinSize,
            _iconMinSize,
            _overrideReserveIconSpaceWhenHidden,
            _reserveIconSpaceWhenHidden,
            _overrideIconParentSpacing,
            _iconParentSpacing,
            _overrideIconParentPadding,
            CopyRectOffset(_iconParentPadding));
    }

    public void SetFromCurrent(
        string storyId,
        bool overridePanelPadding,
        Vector2 panelPadding,
        bool overrideIconSize,
        Vector2 iconSize,
        bool overrideIconOffset,
        Vector2 iconOffset,
        bool overrideIconVisualScale,
        Vector2 iconVisualScale,
        bool overrideIconMinSize,
        Vector2 iconMinSize,
        bool overrideReserveIconSpaceWhenHidden,
        bool reserveIconSpaceWhenHidden,
        bool overrideIconParentSpacing,
        float iconParentSpacing,
        bool overrideIconParentPadding,
        RectOffset iconParentPadding)
    {
        _storyId = Normalize(storyId);
        _overridePanelPadding = overridePanelPadding;
        _panelPadding = panelPadding;
        _overrideIconSize = overrideIconSize;
        _iconSize = iconSize;
        _overrideIconOffset = overrideIconOffset;
        _iconOffset = iconOffset;
        _overrideIconVisualScale = overrideIconVisualScale;
        _iconVisualScale = iconVisualScale;
        _overrideIconMinSize = overrideIconMinSize;
        _iconMinSize = iconMinSize;
        _overrideReserveIconSpaceWhenHidden = overrideReserveIconSpaceWhenHidden;
        _reserveIconSpaceWhenHidden = reserveIconSpaceWhenHidden;
        _overrideIconParentSpacing = overrideIconParentSpacing;
        _iconParentSpacing = iconParentSpacing;
        _overrideIconParentPadding = overrideIconParentPadding;
        _iconParentPadding = CopyRectOffset(iconParentPadding);
        Validate();
    }

    static RectOffset CopyRectOffset(RectOffset source)
    {
        if (source == null)
            return new RectOffset();

        return new RectOffset(source.left, source.right, source.top, source.bottom);
    }

    static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Novel Template/UI/Stat Change Overlay")]
public sealed class StatChangeOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform _panelRect;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Image _iconImage;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject _rootObject;

    [Header("Icon Image Settings")]
    [SerializeField] private bool _applyIconImageSettings = true;
    [SerializeField] private bool _useInitialIconAsFallback = true;
    [SerializeField] private bool _hideIconWhenMissing = true;
    [SerializeField] private bool _reserveIconSpaceWhenHidden;
    [SerializeField] private float _iconWidth;
    [SerializeField] private float _iconHeight;
    [SerializeField] private bool _applyIconVisualScale;
    [SerializeField] private Vector2 _iconVisualScale = Vector2.one;
    [SerializeField] private float _iconMinWidth;
    [SerializeField] private float _iconMinHeight;
    [SerializeField] private Vector2 _iconAnchoredOffset;
    [SerializeField] private bool _applyIconStretchPadding;
    [SerializeField] private RectOffset _iconStretchPadding = new RectOffset();
    [SerializeField] private bool _driveIconLayoutElement = true;
    [SerializeField] private bool _ignoreIconInLayout;
    [SerializeField] private bool _applyParentLayoutSpacing;
    [SerializeField] private float _parentLayoutSpacing;
    [SerializeField] private bool _applyParentLayoutPadding;
    [SerializeField] private RectOffset _parentLayoutPadding = new RectOffset();
    [SerializeField] private bool _preserveIconAspect = true;
    [SerializeField] private bool _iconRaycastTarget;
    [SerializeField] private bool _forceIconImageType;
    [SerializeField] private Image.Type _iconImageType = Image.Type.Simple;
    [SerializeField] private bool _overrideIconColor;
    [SerializeField] private Color _iconColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float _iconAlpha = 1f;
    [SerializeField] private List<StatIconOffsetOverride> _statIconOffsetOverrides = new List<StatIconOffsetOverride>();

    [Header("Layout по ID истории")]
    [Tooltip("Включает отдельные настройки layout/spacing для каждой истории по её ID. Если ID не найден, используются Icon Image Settings выше.")]
    [SerializeField] private bool _useStoryLayoutOverrides;
    [Tooltip("ID истории для предпросмотра в редакторе. Во время игры берётся текущий StoryManager/GameState.")]
    [SerializeField] private string _editorPreviewStoryId;
    [Tooltip("Список layout-настроек по ID истории: offset иконки, spacing, padding и размеры.")]
    [SerializeField] private List<StatChangeOverlayStoryLayoutOverride> _storyLayoutOverrides = new List<StatChangeOverlayStoryLayoutOverride>();

    [Header("Content")]
    [SerializeField] private StatChangeOverlayTextMode _textMode = StatChangeOverlayTextMode.DeltaThenName;
    [SerializeField] private bool _systemMessageOverridesStatText;
    [SerializeField] private List<StatChangeOverlayDefinition> _definitions = new List<StatChangeOverlayDefinition>();
    [SerializeField] private List<StatDefinition> _statDefinitions = new List<StatDefinition>();

    [Header("Relationship Content")]
    [SerializeField] private Vector2 _relationshipFrameSize = new Vector2(1320f, 175f);
    [SerializeField, Min(1f)] private float _relationshipFontSizeMax = 54f;
    [SerializeField, Min(1f)] private float _relationshipFontSizeMin = 42f;
    [SerializeField, Min(1)] private int _relationshipMaxVisibleLines = 3;
    [SerializeField] private List<RelationshipMessageOverride> _relationshipMessageOverrides = new List<RelationshipMessageOverride>();
    [SerializeField, HideInInspector] private List<StatPanelSizeOverride> _statPanelSizeOverrides = new List<StatPanelSizeOverride>();
    [SerializeField, HideInInspector] private List<StatTextRectOverride> _statTextRectOverrides = new List<StatTextRectOverride>();

    [Header("Animation")]
    [SerializeField] private StatChangeOverlayAnimationMode _animationMode = StatChangeOverlayAnimationMode.FadeAndSlide;
    [SerializeField] private StatChangeOverlaySlideDirection _slideDirection = StatChangeOverlaySlideDirection.Up;
    [SerializeField] private bool _captureShownPositionOnAwake = true;
    [SerializeField] private Vector2 _shownAnchoredPosition;
    [SerializeField] private float _slideOffset = 90f;
    [SerializeField, Min(0f)] private float _enterDuration = 0.28f;
    [SerializeField, Min(0f)] private float _visibleDuration = 1.05f;
    [SerializeField, Min(0f)] private float _exitDuration = 0.32f;
    [SerializeField] private Ease _enterEase = Ease.OutCubic;
    [SerializeField] private Ease _exitEase = Ease.InCubic;
    [SerializeField] private bool _useUnscaledTime = true;
    [SerializeField] private bool _queueChanges = true;
    [SerializeField] private bool _disableRootWhenHidden = true;

    private readonly Queue<Request> _queue = new Queue<Request>();
    private Sequence _sequence;
    private Vector3 _baseScale = Vector3.one;
    private bool _isShowing;
    private bool _messageTextPresentationCaptured;
    private bool _messageTextInitialWordWrapping;
    private TextOverflowModes _messageTextInitialOverflowMode;
    private bool _messageTextInitialAutoSizing;
    private float _messageTextInitialFontSize;
    private float _messageTextInitialFontSizeMin;
    private float _messageTextInitialFontSizeMax;
    private int _messageTextInitialMaxVisibleLines;
    private ButtonTextAutoSize[] _textAutoSizeDrivers = Array.Empty<ButtonTextAutoSize>();
    private bool[] _textAutoSizeDriverEnabledStates = Array.Empty<bool>();
    private bool _textAutoSizeDriversCaptured;
    private bool _textAutoSizeDriversSuppressed;
    private RectTransform _relationshipFrameRect;
    private Vector2 _relationshipFrameInitialSize;
    private bool _relationshipFrameSizeCaptured;
    private bool _relationshipFrameSizeApplied;
    private bool _relationshipFrameRectOverrideEnabled;
    private Vector2 _relationshipFrameAnchoredPosition;
    private RectTransformDefaults _relationshipFrameRectDefaults;
    private Sprite _initialIconSprite;
    private bool _initialIconSpriteCaptured;
    private RectTransform _iconRect;
    private Vector2 _iconCapturedAnchoredPosition;
    private bool _iconUseAbsoluteAnchoredOffset;
    private bool _storyUiStyleActive;
    private bool _storyStyleDefaultsCaptured;
    private ImageDefaults _storyStylePanelImageDefaults;
    private RectTransformDefaults _storyStylePanelBackgroundRectDefaults;
    private bool _storyStyleTextDefaultsCaptured;
    private Color _storyStyleMessageTextColor;
    private TMP_FontAsset _storyStyleMessageTextFont;
    private float _storyStyleMessageTextFontSize;
    private RectTransformDefaults _storyStylePanelRectDefaults;
    private RectTransformDefaults _storyStyleMessageTextRectDefaults;
    private TextLayoutDefaults _storyStyleMessageTextLayoutDefaults;
    private LayoutGroupDefaults _storyStylePanelVerticalLayoutDefaults;
    private ContentSizeFitterDefaults _storyStylePanelContentSizeFitterDefaults;
    private Vector2 _storyStyleShownAnchoredPosition;
    private IconLayoutDefaults _storyStyleIconLayoutDefaults;
    private List<StatChangeOverlayDefinition> _storyStyleDefaultDefinitions;
    private List<StatDefinition> _storyStyleDefaultStatDefinitions;
    private List<RelationshipMessageOverride> _storyStyleDefaultRelationshipMessageOverrides;
    private List<StatPanelSizeOverride> _storyStyleDefaultPanelSizeOverrides;
    private List<StatTextRectOverride> _storyStyleDefaultTextRectOverrides;
    private bool _storyStylePanelSizeBaseCaptured;
    private Vector2 _storyStylePanelSizeBaseAnchoredPosition;
    private Vector2 _storyStylePanelSizeBaseSizeDelta;
    private Vector2 _storyStylePanelSizeBaseShownPosition;
    private bool _storyStyleTextRectBaseCaptured;
    private Vector2 _storyStyleTextRectBaseAnchoredPosition;
    private Vector2 _storyStyleTextRectBaseSizeDelta;
    private PanelBackgroundRectOverrides _storyStyleStatPanelBackgroundOverrides;
    private PanelBackgroundRectOverrides _storyStyleRelationshipPanelBackgroundOverrides;
    private PanelLayoutGroupOverrides _storyStyleStatPanelLayoutOverrides;
    private PanelLayoutGroupOverrides _storyStyleRelationshipPanelLayoutOverrides;
    private VerticalLayoutGroup _panelVerticalLayoutGroup;
    private ContentSizeFitter _panelContentSizeFitter;
    private Request _lastContentRequest;
    private bool _hasLastContentRequest;

    public bool IsShowing => _isShowing;
    public Image PanelBackgroundImage
    {
        get
        {
            EnsureReferences();
            return FindPanelBackgroundImage();
        }
    }

    public TMP_Text MessageText
    {
        get
        {
            EnsureReferences();
            return _messageText;
        }
    }

    public VerticalLayoutGroup PanelVerticalLayoutGroup
    {
        get
        {
            EnsureReferences();
            return FindPanelVerticalLayoutGroup();
        }
    }

    public ContentSizeFitter PanelContentSizeFitter
    {
        get
        {
            EnsureReferences();
            return FindPanelContentSizeFitter();
        }
    }

    public RectTransform RelationshipFrameRect
    {
        get
        {
            EnsureReferences();
            return ResolveRelationshipFrameRect();
        }
    }

    private struct ImageDefaults
    {
        public Image Target;
        public Sprite Sprite;
        public Color Color;
        public Image.Type Type;
        public bool PreserveAspect;
        public float PixelsPerUnitMultiplier;
        public Material Material;
        public bool RaycastTarget;
        public bool Captured;
    }

    private struct IconLayoutDefaults
    {
        public ButtonTextAutoSize PanelAutoSize;
        public Vector2 PanelPadding;
        public RectTransform IconRect;
        public Vector2 IconAnchoredPosition;
        public Vector2 IconSize;
        public Vector3 IconScale;
        public bool IconUseAbsoluteAnchoredOffset;
        public bool ApplyIconImageSettings;
        public bool ReserveIconSpaceWhenHidden;
        public float IconWidth;
        public float IconHeight;
        public bool ApplyIconVisualScale;
        public Vector2 IconVisualScale;
        public float IconMinWidth;
        public float IconMinHeight;
        public Vector2 IconAnchoredOffset;
        public bool ApplyIconStretchPadding;
        public RectOffset IconStretchPadding;
        public bool DriveIconLayoutElement;
        public bool IgnoreIconInLayout;
        public bool ApplyParentLayoutSpacing;
        public float ParentLayoutSpacing;
        public bool ApplyParentLayoutPadding;
        public RectOffset ParentLayoutPadding;
        public bool PreserveIconAspect;
        public bool IconRaycastTarget;
        public bool ForceIconImageType;
        public Image.Type IconImageType;
        public bool OverrideIconColor;
        public Color IconColor;
        public float IconAlpha;
        public HorizontalOrVerticalLayoutGroup ParentLayoutGroup;
        public float ParentLayoutGroupSpacing;
        public RectOffset ParentLayoutGroupPadding;
        public bool Captured;
    }

    private struct RectTransformDefaults
    {
        public RectTransform Target;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public Vector2 Pivot;
        public Vector3 LocalScale;
        public LayoutElement LayoutElement;
        public bool LayoutElementIgnoreLayout;
        public bool Captured;
    }

    private struct TextLayoutDefaults
    {
        public TMP_Text Target;
        public bool EnableAutoSizing;
        public float FontSize;
        public float FontSizeMin;
        public float FontSizeMax;
        public TextAlignmentOptions Alignment;
        public bool EnableWordWrapping;
        public TextOverflowModes OverflowMode;
        public float LineSpacing;
        public Vector4 Margin;
        public bool Captured;
    }

    private struct LayoutGroupDefaults
    {
        public VerticalLayoutGroup Target;
        public RectOffset Padding;
        public TextAnchor ChildAlignment;
        public float Spacing;
        public bool ReverseArrangement;
        public bool ChildControlWidth;
        public bool ChildControlHeight;
        public bool ChildScaleWidth;
        public bool ChildScaleHeight;
        public bool ChildForceExpandWidth;
        public bool ChildForceExpandHeight;
        public bool Captured;
    }

    private struct ContentSizeFitterDefaults
    {
        public ContentSizeFitter Target;
        public ContentSizeFitter.FitMode HorizontalFit;
        public ContentSizeFitter.FitMode VerticalFit;
        public bool Captured;
    }

    private struct PanelBackgroundRectOverrides
    {
        public bool OverrideAnchors;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public bool OverridePivot;
        public Vector2 Pivot;
        public bool OverrideRect;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public bool OverrideStretchOffsets;
        public Vector4 StretchOffsets;

        public bool HasAny =>
            OverrideAnchors ||
            OverridePivot ||
            OverrideRect ||
            OverrideStretchOffsets;
    }

    private struct PanelLayoutGroupOverrides
    {
        public bool OverrideVerticalLayout;
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
        public bool OverrideContentSizeFitter;
        public ContentSizeFitter.FitMode ContentSizeFitterHorizontalFit;
        public ContentSizeFitter.FitMode ContentSizeFitterVerticalFit;

        public bool HasAny => OverrideVerticalLayout || OverrideContentSizeFitter;
    }

    private void Reset()
    {
        _panelRect = transform as RectTransform;
        _messageText = GetComponentInChildren<TMP_Text>(true);
        _iconImage = FindIconImage();
        _canvasGroup = GetComponent<CanvasGroup>();
        _rootObject = gameObject;
    }

    private void Awake()
    {
        EnsureReferences();
        CaptureInitialIconSprite();

        if (_panelRect != null)
        {
            _baseScale = _panelRect.localScale;
            if (_captureShownPositionOnAwake)
                _shownAnchoredPosition = _panelRect.anchoredPosition;
        }

        HideInstant();
    }

    private void OnValidate()
    {
        _enterDuration = Mathf.Max(0f, _enterDuration);
        _visibleDuration = Mathf.Max(0f, _visibleDuration);
        _exitDuration = Mathf.Max(0f, _exitDuration);
        _relationshipFontSizeMax = Mathf.Max(1f, _relationshipFontSizeMax);
        _relationshipFontSizeMin = Mathf.Clamp(_relationshipFontSizeMin, 1f, _relationshipFontSizeMax);
        _relationshipMaxVisibleLines = Mathf.Max(1, _relationshipMaxVisibleLines);
        ValidateRelationshipMessageOverrides();
        _iconStretchPadding ??= new RectOffset();
        _parentLayoutPadding ??= new RectOffset();
        ValidateStatIconOffsetOverrides();
        ValidateStatPanelSizeOverrides();
        ValidateStatTextRectOverrides();
        ValidateStoryLayoutOverrides();

        if (_rootObject == null)
            _rootObject = gameObject;
        if (_panelRect == null)
            _panelRect = transform as RectTransform;
        if (_messageText == null)
            _messageText = GetComponentInChildren<TMP_Text>(true);
        RemoveMessageTextLayoutElement();
        if (_iconImage == null)
            _iconImage = FindIconImage();
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        if (!Application.isPlaying)
        {
            if (!_storyUiStyleActive)
                ApplyStoryLayoutOverrideForCurrentStory();

            ApplyIconImageSettings(_iconImage != null && (_iconImage.sprite != null || _reserveIconSpaceWhenHidden), false, true);
            Canvas.ForceUpdateCanvases();
        }
    }

    private void OnDisable()
    {
        _queue.Clear();
        KillSequence();
        RestoreRelationshipFrameLayout();
        RestoreTextAutoSizeDrivers();
        RestoreMessageTextPresentation();
        _isShowing = false;
    }

    private void OnDestroy()
    {
        KillSequence();
    }

    public void ShowStatChange(string statId, string displayName, int delta, string message = null)
    {
        Request request = new Request(statId, displayName, delta, message);

        if (_queueChanges && _isShowing)
        {
            _queue.Enqueue(request);
            return;
        }

        _queue.Clear();
        Play(request);
    }

    public void ShowMessage(string message)
    {
        ShowStatChange(string.Empty, string.Empty, 0, message);
    }

    public void ApplyStoryUiStyle(StoryUiStyle style, string storyId = null)
    {
        EnsureReferences();
        RemoveMessageTextLayoutElement();
        CaptureStoryStyleDefaults();
        RestoreStoryStyleDefaults();

        _storyUiStyleActive = style != null;

        ApplyStoryLayoutOverrideForStoryId(storyId);

        if (style != null)
            style.ApplyToStatChangeOverlay(this);

        if (!Application.isPlaying)
            ApplyEditorPreviewContentStyle();
    }

    public void PreviewStatChange(string statId, string displayName, int delta, string message = null)
    {
        EnsureReferences();
        if (!_storyUiStyleActive)
            ApplyStoryLayoutOverrideForCurrentStory();

        CaptureInitialIconSprite();

        if (!Application.isPlaying && _panelRect != null)
            _baseScale = _panelRect.localScale;

        if (_rootObject != null && !_rootObject.activeSelf)
            _rootObject.SetActive(true);

        KillSequence();
        _queue.Clear();
        _isShowing = true;
        ApplyContent(new Request(statId, displayName, delta, message));

        if (_panelRect != null)
        {
            _panelRect.anchoredPosition = ResolveShownPosition();
            _panelRect.localScale = _baseScale;
        }

        SetAlpha(1f);
    }

    public void ReplaceDefinitions(
        IReadOnlyList<StatChangeOverlayDefinition> definitions,
        IReadOnlyList<StatDefinition> statDefinitions)
    {
        _definitions = definitions != null
            ? new List<StatChangeOverlayDefinition>(definitions)
            : new List<StatChangeOverlayDefinition>();

        _statDefinitions = statDefinitions != null
            ? new List<StatDefinition>(statDefinitions)
            : new List<StatDefinition>();
    }

    public void ReplaceStatIconOffsetOverrides(IReadOnlyList<StatIconOffsetOverride> overrides)
    {
        if (overrides == null)
        {
            _statIconOffsetOverrides = new List<StatIconOffsetOverride>();
            return;
        }

        _statIconOffsetOverrides = new List<StatIconOffsetOverride>(overrides.Count);
        for (int i = 0; i < overrides.Count; i++)
        {
            StatIconOffsetOverride entry = overrides[i];
            if (entry == null)
                continue;

            entry.Validate();
            _statIconOffsetOverrides.Add(new StatIconOffsetOverride
            {
                statId = entry.statId,
                overrideIconOffset = entry.overrideIconOffset,
                iconOffset = entry.iconOffset
            });
        }
    }

    public void ReplaceStatPanelSizeOverrides(IReadOnlyList<StatPanelSizeOverride> overrides)
    {
        _statPanelSizeOverrides = CloneStatPanelSizeOverrides(overrides);
    }

    public void ReplaceStatTextRectOverrides(IReadOnlyList<StatTextRectOverride> overrides)
    {
        _statTextRectOverrides = CloneStatTextRectOverrides(overrides);
    }

    public void ReplaceRelationshipMessageOverrides(IReadOnlyList<RelationshipMessageOverride> overrides)
    {
        _relationshipMessageOverrides = CloneRelationshipMessageOverrides(overrides);
    }

    public void ApplyLayoutOverrides(
        bool overridePanelPadding,
        Vector2 panelPadding,
        bool overrideIconSize,
        Vector2 iconSize,
        bool overrideIconOffset,
        Vector2 iconOffset,
        bool overrideIconVisualScale,
        Vector2 iconVisualScale,
        bool overrideIconMinSize,
        Vector2 iconMinSize,
        bool overrideReserveIconSpaceWhenHidden,
        bool reserveIconSpaceWhenHidden,
        bool overrideIconParentSpacing,
        float iconParentSpacing,
        bool overrideIconParentPadding,
        RectOffset iconParentPadding)
    {
        EnsureReferences();

        if (overridePanelPadding)
            ApplyPanelPadding(panelPadding);

        if (overrideIconSize)
        {
            _applyIconImageSettings = true;
            _iconWidth = iconSize.x;
            _iconHeight = iconSize.y;
        }

        if (overrideIconOffset)
        {
            _applyIconImageSettings = true;
            _iconAnchoredOffset = iconOffset;
            _iconUseAbsoluteAnchoredOffset = true;
            _ignoreIconInLayout = true;
        }

        if (overrideIconVisualScale)
        {
            _applyIconImageSettings = true;
            _applyIconVisualScale = true;
            _iconVisualScale = iconVisualScale;
        }

        if (overrideIconMinSize)
        {
            _applyIconImageSettings = true;
            _iconMinWidth = iconMinSize.x;
            _iconMinHeight = iconMinSize.y;
        }

        if (overrideReserveIconSpaceWhenHidden)
        {
            _applyIconImageSettings = true;
            _reserveIconSpaceWhenHidden = reserveIconSpaceWhenHidden;
        }

        if (overrideIconParentSpacing)
        {
            _applyIconImageSettings = true;
            _applyParentLayoutSpacing = true;
            _parentLayoutSpacing = iconParentSpacing;
        }

        if (overrideIconParentPadding)
        {
            _applyIconImageSettings = true;
            _applyParentLayoutPadding = true;
            _parentLayoutPadding = CopyRectOffset(iconParentPadding);
        }

        bool hasIcon = _iconImage != null && (_iconImage.sprite != null || _reserveIconSpaceWhenHidden);
        ApplyIconImageSettings(hasIcon, false, true);
    }

    public void ApplyPanelLayoutGroupOverrides(
        bool overrideVerticalLayout,
        RectOffset verticalLayoutPadding,
        float verticalLayoutSpacing,
        TextAnchor verticalLayoutChildAlignment,
        bool verticalLayoutReverseArrangement,
        bool verticalLayoutControlChildWidth,
        bool verticalLayoutControlChildHeight,
        bool verticalLayoutUseChildScaleWidth,
        bool verticalLayoutUseChildScaleHeight,
        bool verticalLayoutChildForceExpandWidth,
        bool verticalLayoutChildForceExpandHeight,
        bool overrideContentSizeFitter,
        ContentSizeFitter.FitMode contentSizeFitterHorizontalFit,
        ContentSizeFitter.FitMode contentSizeFitterVerticalFit)
    {
        _storyStyleStatPanelLayoutOverrides = CreatePanelLayoutGroupOverrides(
            overrideVerticalLayout,
            verticalLayoutPadding,
            verticalLayoutSpacing,
            verticalLayoutChildAlignment,
            verticalLayoutReverseArrangement,
            verticalLayoutControlChildWidth,
            verticalLayoutControlChildHeight,
            verticalLayoutUseChildScaleWidth,
            verticalLayoutUseChildScaleHeight,
            verticalLayoutChildForceExpandWidth,
            verticalLayoutChildForceExpandHeight,
            overrideContentSizeFitter,
            contentSizeFitterHorizontalFit,
            contentSizeFitterVerticalFit);

        if (!ShouldUseRelationshipStyleForCurrentContent())
            ApplyPanelLayoutStyleForContent(false);
    }

    public void ApplyRelationshipPanelLayoutGroupOverrides(
        bool overrideVerticalLayout,
        RectOffset verticalLayoutPadding,
        float verticalLayoutSpacing,
        TextAnchor verticalLayoutChildAlignment,
        bool verticalLayoutReverseArrangement,
        bool verticalLayoutControlChildWidth,
        bool verticalLayoutControlChildHeight,
        bool verticalLayoutUseChildScaleWidth,
        bool verticalLayoutUseChildScaleHeight,
        bool verticalLayoutChildForceExpandWidth,
        bool verticalLayoutChildForceExpandHeight,
        bool overrideContentSizeFitter,
        ContentSizeFitter.FitMode contentSizeFitterHorizontalFit,
        ContentSizeFitter.FitMode contentSizeFitterVerticalFit)
    {
        _storyStyleRelationshipPanelLayoutOverrides = CreatePanelLayoutGroupOverrides(
            overrideVerticalLayout,
            verticalLayoutPadding,
            verticalLayoutSpacing,
            verticalLayoutChildAlignment,
            verticalLayoutReverseArrangement,
            verticalLayoutControlChildWidth,
            verticalLayoutControlChildHeight,
            verticalLayoutUseChildScaleWidth,
            verticalLayoutUseChildScaleHeight,
            verticalLayoutChildForceExpandWidth,
            verticalLayoutChildForceExpandHeight,
            overrideContentSizeFitter,
            contentSizeFitterHorizontalFit,
            contentSizeFitterVerticalFit);

        if (ShouldUseRelationshipStyleForCurrentContent())
            ApplyPanelLayoutStyleForContent(true);
    }

    public void ApplyPanelBackgroundRectOverrides(
        bool overrideAnchors,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool overridePivot,
        Vector2 pivot,
        bool overrideStretchOffsets,
        Vector4 stretchOffsets)
    {
        _storyStyleStatPanelBackgroundOverrides = CreatePanelBackgroundRectOverrides(
            overrideAnchors,
            anchorMin,
            anchorMax,
            overridePivot,
            pivot,
            false,
            Vector2.zero,
            Vector2.zero,
            overrideStretchOffsets,
            stretchOffsets);

        if (!ShouldUseRelationshipStyleForCurrentContent())
            ApplyPanelBackgroundStyleForContent(false);
    }

    public void ApplyRelationshipPanelBackgroundRectOverrides(
        bool overrideAnchors,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool overridePivot,
        Vector2 pivot,
        bool overrideRect,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        bool overrideStretchOffsets,
        Vector4 stretchOffsets)
    {
        _storyStyleRelationshipPanelBackgroundOverrides = CreatePanelBackgroundRectOverrides(
            overrideAnchors,
            anchorMin,
            anchorMax,
            overridePivot,
            pivot,
            overrideRect,
            anchoredPosition,
            sizeDelta,
            overrideStretchOffsets,
            stretchOffsets);

        if (ShouldUseRelationshipStyleForCurrentContent())
            ApplyPanelBackgroundStyleForContent(true);
    }

    private static PanelBackgroundRectOverrides CreatePanelBackgroundRectOverrides(
        bool overrideAnchors,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool overridePivot,
        Vector2 pivot,
        bool overrideRect,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        bool overrideStretchOffsets,
        Vector4 stretchOffsets)
    {
        return new PanelBackgroundRectOverrides
        {
            OverrideAnchors = overrideAnchors,
            AnchorMin = anchorMin,
            AnchorMax = anchorMax,
            OverridePivot = overridePivot,
            Pivot = pivot,
            OverrideRect = overrideRect,
            AnchoredPosition = anchoredPosition,
            SizeDelta = sizeDelta,
            OverrideStretchOffsets = overrideStretchOffsets,
            StretchOffsets = stretchOffsets
        };
    }

    private static PanelLayoutGroupOverrides CreatePanelLayoutGroupOverrides(
        bool overrideVerticalLayout,
        RectOffset verticalLayoutPadding,
        float verticalLayoutSpacing,
        TextAnchor verticalLayoutChildAlignment,
        bool verticalLayoutReverseArrangement,
        bool verticalLayoutControlChildWidth,
        bool verticalLayoutControlChildHeight,
        bool verticalLayoutUseChildScaleWidth,
        bool verticalLayoutUseChildScaleHeight,
        bool verticalLayoutChildForceExpandWidth,
        bool verticalLayoutChildForceExpandHeight,
        bool overrideContentSizeFitter,
        ContentSizeFitter.FitMode contentSizeFitterHorizontalFit,
        ContentSizeFitter.FitMode contentSizeFitterVerticalFit)
    {
        return new PanelLayoutGroupOverrides
        {
            OverrideVerticalLayout = overrideVerticalLayout,
            VerticalLayoutPadding = CopyRectOffset(verticalLayoutPadding),
            VerticalLayoutSpacing = verticalLayoutSpacing,
            VerticalLayoutChildAlignment = verticalLayoutChildAlignment,
            VerticalLayoutReverseArrangement = verticalLayoutReverseArrangement,
            VerticalLayoutControlChildWidth = verticalLayoutControlChildWidth,
            VerticalLayoutControlChildHeight = verticalLayoutControlChildHeight,
            VerticalLayoutUseChildScaleWidth = verticalLayoutUseChildScaleWidth,
            VerticalLayoutUseChildScaleHeight = verticalLayoutUseChildScaleHeight,
            VerticalLayoutChildForceExpandWidth = verticalLayoutChildForceExpandWidth,
            VerticalLayoutChildForceExpandHeight = verticalLayoutChildForceExpandHeight,
            OverrideContentSizeFitter = overrideContentSizeFitter,
            ContentSizeFitterHorizontalFit = contentSizeFitterHorizontalFit,
            ContentSizeFitterVerticalFit = contentSizeFitterVerticalFit
        };
    }

    private void ApplyPanelBackgroundStyleForContent(bool isRelationshipMessage)
    {
        EnsureReferences();

        Image panelImage = FindPanelBackgroundImage();
        RectTransform backgroundRect = panelImage != null ? panelImage.rectTransform : null;
        if (backgroundRect == null)
            return;

        RestoreRectTransformDefaults(_storyStylePanelBackgroundRectDefaults);
        PanelBackgroundRectOverrides overrides = isRelationshipMessage
            ? _storyStyleRelationshipPanelBackgroundOverrides
            : _storyStyleStatPanelBackgroundOverrides;
        if (overrides.HasAny)
            SetRectIgnoreLayout(backgroundRect, true);
        ApplyPanelBackgroundRectOverrides(backgroundRect, overrides);

        MarkPanelBackgroundRectDirty(backgroundRect);
    }

    private static void ApplyPanelBackgroundRectOverrides(
        RectTransform backgroundRect,
        PanelBackgroundRectOverrides overrides)
    {
        if (backgroundRect == null || !overrides.HasAny)
            return;

        if (overrides.OverrideAnchors)
        {
            backgroundRect.anchorMin = overrides.AnchorMin;
            backgroundRect.anchorMax = overrides.AnchorMax;
        }

        if (overrides.OverridePivot)
            backgroundRect.pivot = overrides.Pivot;

        if (overrides.OverrideRect)
        {
            backgroundRect.anchoredPosition = overrides.AnchoredPosition;
            backgroundRect.sizeDelta = overrides.SizeDelta;
        }

        if (overrides.OverrideStretchOffsets)
        {
            backgroundRect.offsetMin = new Vector2(overrides.StretchOffsets.x, overrides.StretchOffsets.w);
            backgroundRect.offsetMax = new Vector2(-overrides.StretchOffsets.y, -overrides.StretchOffsets.z);
        }
    }

    private void ApplyPanelLayoutStyleForContent(bool isRelationshipMessage)
    {
        EnsureReferences();

        RestorePanelVerticalLayoutDefaults(_storyStylePanelVerticalLayoutDefaults);
        RestorePanelContentSizeFitterDefaults(_storyStylePanelContentSizeFitterDefaults);
        PanelLayoutGroupOverrides overrides = isRelationshipMessage
            ? _storyStyleRelationshipPanelLayoutOverrides
            : _storyStyleStatPanelLayoutOverrides;
        ApplyPanelLayoutGroupOverrides(overrides);

        MarkPanelLayoutGroupDirty();
    }

    private void ApplyEditorPreviewContentStyle()
    {
        if (_isShowing && _hasLastContentRequest)
        {
            ApplyContent(_lastContentRequest);
            return;
        }

        bool isRelationshipMessage = ShouldUseRelationshipStyleForCurrentContent();
        if (!isRelationshipMessage)
            return;

        RestoreSharedPanelLayoutForRelationship();
        ApplyPanelLayoutStyleForContent(true);
        ApplyPanelBackgroundStyleForContent(true);
        ApplyRelationshipTextPresentation();
    }

    private bool ShouldUseRelationshipStyleForCurrentContent()
    {
        if (_isShowing &&
            _hasLastContentRequest &&
            IsRelationshipRequest(_lastContentRequest.StatId, _lastContentRequest.Message))
        {
            return true;
        }

        return _messageText != null && IsRelationshipMessage(_messageText.text);
    }

    private void RestoreSharedPanelLayoutForRelationship()
    {
        if (!_storyStyleDefaultsCaptured)
            return;

        RestoreRectTransformDefaults(_storyStylePanelRectDefaults);
        _shownAnchoredPosition = _storyStyleShownAnchoredPosition;
        _storyStylePanelSizeBaseCaptured = false;

        if (_storyStyleTextDefaultsCaptured && _messageText != null)
        {
            RestoreRectTransformDefaults(_storyStyleMessageTextRectDefaults);
            RestoreTextLayoutDefaults(_storyStyleMessageTextLayoutDefaults);
            _storyStyleTextRectBaseCaptured = false;
            _messageText.SetAllDirty();
        }
    }

    private void ApplyPanelLayoutGroupOverrides(PanelLayoutGroupOverrides overrides)
    {
        if (!overrides.HasAny)
            return;

        VerticalLayoutGroup layoutGroup = FindPanelVerticalLayoutGroup();
        if (layoutGroup != null && overrides.OverrideVerticalLayout)
        {
            CopyRectOffset(overrides.VerticalLayoutPadding, layoutGroup.padding);
            layoutGroup.spacing = overrides.VerticalLayoutSpacing;
            layoutGroup.childAlignment = overrides.VerticalLayoutChildAlignment;
            layoutGroup.reverseArrangement = overrides.VerticalLayoutReverseArrangement;
            layoutGroup.childControlWidth = overrides.VerticalLayoutControlChildWidth;
            layoutGroup.childControlHeight = overrides.VerticalLayoutControlChildHeight;
            layoutGroup.childScaleWidth = overrides.VerticalLayoutUseChildScaleWidth;
            layoutGroup.childScaleHeight = overrides.VerticalLayoutUseChildScaleHeight;
            layoutGroup.childForceExpandWidth = overrides.VerticalLayoutChildForceExpandWidth;
            layoutGroup.childForceExpandHeight = overrides.VerticalLayoutChildForceExpandHeight;
        }

        ContentSizeFitter fitter = FindPanelContentSizeFitter();
        if (fitter != null && overrides.OverrideContentSizeFitter)
        {
            fitter.horizontalFit = overrides.ContentSizeFitterHorizontalFit;
            fitter.verticalFit = overrides.ContentSizeFitterVerticalFit;
        }
    }

    public void ApplyPanelAndTextLayoutOverrides(
        bool overridePanelRect,
        Vector2 panelAnchoredPosition,
        Vector2 panelSizeDelta,
        bool overrideTextRect,
        Vector2 textAnchoredPosition,
        Vector2 textSizeDelta,
        bool overrideTextAutoSize,
        bool textAutoSize,
        bool overrideTextAutoFontSizeRange,
        float textMinAutoFontSize,
        float textMaxAutoFontSize,
        bool overrideTextAlignment,
        TextAlignmentOptions textAlignment,
        bool overrideTextWordWrapping,
        bool textWordWrapping,
        bool overrideTextOverflowMode,
        TextOverflowModes textOverflowMode,
        bool overrideTextLineSpacing,
        float textLineSpacing,
        bool overrideTextMargins,
        Vector4 textMargins)
    {
        EnsureReferences();

        if (overridePanelRect && _panelRect != null)
        {
            ApplyRectOverride(_panelRect, panelAnchoredPosition, panelSizeDelta, true, true);
            ApplyPanelBackgroundSize(panelSizeDelta);
            _shownAnchoredPosition = panelAnchoredPosition;
        }

        RectTransform messageTextRect = null;
        bool shouldApplyTextRect = false;
        if (_messageText != null)
        {
            messageTextRect = _messageText.rectTransform;
            shouldApplyTextRect = overrideTextRect && messageTextRect != null;
            if (shouldApplyTextRect)
                ApplyTextRectOverrideWithoutLayoutElement(messageTextRect, textAnchoredPosition, textSizeDelta);

            if (overrideTextAutoSize)
                _messageText.enableAutoSizing = textAutoSize;

            if (overrideTextAutoFontSizeRange)
            {
                float min = Mathf.Max(1f, textMinAutoFontSize);
                float max = Mathf.Max(min, textMaxAutoFontSize);
                _messageText.fontSizeMin = min;
                _messageText.fontSizeMax = max;
                if (_messageText.enableAutoSizing)
                    _messageText.fontSize = max;
            }

            if (overrideTextAlignment)
                _messageText.alignment = textAlignment;
            if (overrideTextWordWrapping)
                _messageText.enableWordWrapping = textWordWrapping;
            if (overrideTextOverflowMode)
                _messageText.overflowMode = textOverflowMode;
            if (overrideTextLineSpacing)
                _messageText.lineSpacing = textLineSpacing;
            if (overrideTextMargins)
                _messageText.margin = textMargins;

            _messageText.SetAllDirty();
            _messageText.ForceMeshUpdate();
            CaptureCurrentMessageTextPresentationAsInitial();
        }

        _storyStylePanelSizeBaseCaptured = false;
        CaptureStoryStylePanelSizeBase();

        if (_applyIconImageSettings)
        {
            bool hasIcon = _iconImage != null && (_iconImage.sprite != null || _reserveIconSpaceWhenHidden);
            ApplyIconImageSettings(hasIcon, false, true);
        }
        else
        {
            RebuildLayoutForImmediatePreview();
        }

        if (shouldApplyTextRect)
            ApplyTextRectOverrideAfterLayout(messageTextRect, textAnchoredPosition, textSizeDelta);

        _storyStyleTextRectBaseCaptured = false;
        CaptureStoryStyleTextRectBase();
    }

    public void ApplyRelationshipLayoutOverrides(
        bool overrideFrameSize,
        Vector2 frameAnchoredPosition,
        Vector2 frameSize,
        bool overrideFontSizeRange,
        float minFontSize,
        float maxFontSize,
        bool overrideMaxVisibleLines,
        int maxVisibleLines)
    {
        if (overrideFrameSize)
        {
            _relationshipFrameRectOverrideEnabled = true;
            _relationshipFrameAnchoredPosition = frameAnchoredPosition;
            _relationshipFrameSize = frameSize;
        }
        else
        {
            _relationshipFrameRectOverrideEnabled = false;
        }

        if (overrideFontSizeRange)
        {
            float safeMin = Mathf.Max(1f, minFontSize);
            float safeMax = Mathf.Max(safeMin, maxFontSize);
            _relationshipFontSizeMin = safeMin;
            _relationshipFontSizeMax = safeMax;
        }

        if (overrideMaxVisibleLines)
            _relationshipMaxVisibleLines = Mathf.Max(1, maxVisibleLines);

        RebuildLayoutForImmediatePreview();
    }

    public bool ApplyStoryLayoutOverrideForCurrentStory()
    {
        return ApplyStoryLayoutOverrideForStoryId(null);
    }

    public bool ApplyStoryLayoutOverrideForStoryId(string storyId)
    {
        if (!_useStoryLayoutOverrides)
            return false;

        EnsureReferences();
        ValidateStoryLayoutOverrides();

        string resolvedStoryId = ResolveActiveStoryId(storyId);
        if (string.IsNullOrWhiteSpace(resolvedStoryId))
            return false;

        StatChangeOverlayStoryLayoutOverride layoutOverride = FindStoryLayoutOverride(resolvedStoryId);
        if (layoutOverride == null)
            return false;

        layoutOverride.ApplyTo(this);
        return true;
    }

    public bool CopyCurrentLayoutToPreviewStoryOverride()
    {
        EnsureReferences();
        ValidateStoryLayoutOverrides();

        string storyId = ResolveActiveStoryId();
        if (string.IsNullOrWhiteSpace(storyId))
            return false;

        StatChangeOverlayStoryLayoutOverride layoutOverride = FindStoryLayoutOverride(storyId);
        if (layoutOverride == null)
        {
            layoutOverride = new StatChangeOverlayStoryLayoutOverride();
            _storyLayoutOverrides.Add(layoutOverride);
        }

        ButtonTextAutoSize panelAutoSize = FindPanelAutoSizeDriver();
        Vector2 panelPadding = panelAutoSize != null ? panelAutoSize.Padding : Vector2.zero;
        Vector2 iconSize = ResolveCurrentIconSize();
        Vector2 iconMinSize = new Vector2(_iconMinWidth, _iconMinHeight);

        layoutOverride.SetFromCurrent(
            storyId,
            panelAutoSize != null,
            panelPadding,
            iconSize.x > 0f || iconSize.y > 0f,
            iconSize,
            true,
            _iconAnchoredOffset,
            _applyIconVisualScale,
            _iconVisualScale,
            iconMinSize.x > 0f || iconMinSize.y > 0f,
            iconMinSize,
            true,
            _reserveIconSpaceWhenHidden,
            _applyParentLayoutSpacing,
            _parentLayoutSpacing,
            _applyParentLayoutPadding,
            _parentLayoutPadding);

        _useStoryLayoutOverrides = true;
        _editorPreviewStoryId = storyId;
        return true;
    }

    public void HideInstant()
    {
        KillSequence();
        RestoreRelationshipFrameLayout();
        RestoreTextAutoSizeDrivers();
        RestoreMessageTextPresentation();
        _isShowing = false;
        _queue.Clear();
        SetAlpha(0f);

        if (_panelRect != null)
        {
            _panelRect.anchoredPosition = ResolveShownPosition();
            _panelRect.localScale = _baseScale;
        }

        if (_disableRootWhenHidden && _rootObject != null)
            _rootObject.SetActive(false);
    }

    private void Play(Request request)
    {
        EnsureReferences();
        if (!_storyUiStyleActive)
            ApplyStoryLayoutOverrideForCurrentStory();

        CaptureInitialIconSprite();

        if (_rootObject != null && !_rootObject.activeSelf)
            _rootObject.SetActive(true);

        KillSequence();
        _isShowing = true;
        ApplyContent(request);

        Vector2 shownPosition = ResolveShownPosition();
        SetAlpha(0f);

        if (_panelRect != null)
        {
            _panelRect.anchoredPosition = shownPosition;
            _panelRect.localScale = _baseScale;
        }

        if (_animationMode == StatChangeOverlayAnimationMode.Instant || _panelRect == null)
        {
            _sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime);
            _sequence.AppendCallback(() => SetAlpha(1f));
            _sequence.AppendInterval(_visibleDuration);
            _sequence.AppendCallback(() => SetAlpha(0f));
            _sequence.OnComplete(CompleteCurrent);
            return;
        }

        if (_animationMode == StatChangeOverlayAnimationMode.Pop)
        {
            _panelRect.localScale = _baseScale * 0.9f;
            _sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime);
            _sequence.Append(_canvasGroup.DOFade(1f, _enterDuration).SetEase(_enterEase));
            _sequence.Join(_panelRect.DOScale(_baseScale, _enterDuration).SetEase(_enterEase));
            _sequence.AppendInterval(_visibleDuration);
            _sequence.Append(_canvasGroup.DOFade(0f, _exitDuration).SetEase(_exitEase));
            _sequence.Join(_panelRect.DOScale(_baseScale * 0.96f, _exitDuration).SetEase(_exitEase));
            _sequence.OnComplete(CompleteCurrent);
            return;
        }

        Vector2 enterOffset = GetSlideOffset();
        Vector2 exitOffset = -enterOffset;
        _panelRect.anchoredPosition = shownPosition + enterOffset;

        _sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime);
        _sequence.Append(_canvasGroup.DOFade(1f, _enterDuration).SetEase(_enterEase));
        _sequence.Join(_panelRect.DOAnchorPos(shownPosition, _enterDuration).SetEase(_enterEase));
        _sequence.AppendInterval(_visibleDuration);
        _sequence.Append(_canvasGroup.DOFade(0f, _exitDuration).SetEase(_exitEase));
        _sequence.Join(_panelRect.DOAnchorPos(shownPosition + exitOffset, _exitDuration).SetEase(_exitEase));
        _sequence.OnComplete(CompleteCurrent);
    }

    private void CompleteCurrent()
    {
        KillSequence();
        _isShowing = false;
        SetAlpha(0f);
        RestoreRelationshipFrameLayout();
        RestoreTextAutoSizeDrivers();
        RestoreMessageTextPresentation();

        if (_panelRect != null)
        {
            _panelRect.anchoredPosition = ResolveShownPosition();
            _panelRect.localScale = _baseScale;
        }

        if (_queue.Count > 0)
        {
            Play(_queue.Dequeue());
            return;
        }

        if (_disableRootWhenHidden && _rootObject != null)
            _rootObject.SetActive(false);
    }

    private void ApplyContent(Request request)
    {
        _lastContentRequest = request;
        _hasLastContentRequest = true;

        string displayName = ResolveStatDisplayName(request.StatId, request.DisplayName);
        bool isRelationshipMessage = IsRelationshipRequest(request.StatId, request.Message);
        RelationshipMessageOverride relationshipOverride = isRelationshipMessage
            ? FindRelationshipMessageOverride(request.StatId)
            : null;

        // Remove the previous request's temporary presentation before applying the
        // next one. Otherwise a queued relationship message can leave normal stat
        // text with a transient, layout-driven width of almost zero.
        if (_messageText != null)
        {
            CaptureMessageTextPresentation();
            RestoreRelationshipFrameLayout();
            RestoreTextAutoSizeDrivers();
            RestoreMessageTextPresentation();
        }

        if (isRelationshipMessage)
            RestoreSharedPanelLayoutForRelationship();
        ApplyPanelLayoutStyleForContent(isRelationshipMessage);
        if (!isRelationshipMessage)
            ApplyPanelSizeForStat(request.StatId);
        ApplyPanelBackgroundStyleForContent(isRelationshipMessage);

        if (!isRelationshipMessage)
            ApplyTextRectForStat(request.StatId);

        if (_messageText != null)
        {
            if (isRelationshipMessage)
            {
                ApplyRelationshipTextPresentation();
                relationshipOverride?.ApplyTo(_messageText);
            }

            // Assign text only after its final RectTransform and TMP presentation are
            // in place, so the first rendered mesh matches all following frames.
            _messageText.text = BuildMessage(
                request.StatId,
                displayName,
                request.Delta,
                request.Message,
                relationshipOverride);
            _messageText.SetAllDirty();
        }

        if (_iconImage != null)
        {
            Sprite icon = ResolveStatIcon(request.StatId);
            if (icon == null &&
                _useInitialIconAsFallback &&
                !isRelationshipMessage &&
                !string.IsNullOrWhiteSpace(request.StatId))
            {
                icon = _initialIconSprite;
            }

            _iconImage.sprite = icon;
            ApplyIconImageSettingsForStat(request.StatId, icon != null, true, true);
        }

        RebuildContentLayoutDeterministically(request.StatId, isRelationshipMessage);
    }

    private void RebuildContentLayoutDeterministically(string statId, bool isRelationshipMessage)
    {
        Canvas.ForceUpdateCanvases();

        if (_panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);

        RectTransform textRect = _messageText != null ? _messageText.rectTransform : null;
        if (textRect != null && textRect.parent is RectTransform parentRect && parentRect != _panelRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        // Layout groups can write child rects while rebuilding the panel. Reapply the
        // authored rect once after that write has completed.
        if (isRelationshipMessage)
            ApplyRelationshipFrameLayout();
        else
            ApplyTextRectForStat(statId);

        if (_messageText == null)
            return;

        if (textRect != null)
        {
            textRect.ForceUpdateRectTransforms();
        }

        _messageText.SetAllDirty();
        _messageText.ForceMeshUpdate(true, true);
    }

    private string BuildMessage(
        string statId,
        string displayName,
        int delta,
        string message,
        RelationshipMessageOverride relationshipOverride)
    {
        if (IsRelationshipRequest(statId, message))
            return FormatRelationshipMessage(statId, displayName, delta, message, relationshipOverride);

        if (!string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(displayName))
            return message;

        if (!string.IsNullOrWhiteSpace(message) &&
            (_systemMessageOverridesStatText || _textMode == StatChangeOverlayTextMode.MessageOnly))
        {
            return message;
        }

        string deltaText = delta > 0 ? "+" + delta : delta.ToString();
        displayName = displayName ?? "";

        return _textMode == StatChangeOverlayTextMode.NameThenDelta
            ? (displayName + " " + deltaText).Trim()
            : (deltaText + " " + displayName).Trim();
    }

    private static bool IsRelationshipRequest(string statId, string message)
    {
        return IsRelationshipStatId(statId) || IsRelationshipMessage(message);
    }

    private static bool IsRelationshipStatId(string statId)
    {
        if (string.IsNullOrWhiteSpace(statId))
            return false;

        string value = statId.Trim().ToLowerInvariant();
        return value.StartsWith("relationship:", StringComparison.Ordinal) ||
               value.StartsWith("relationship_", StringComparison.Ordinal) ||
               value.StartsWith("rel:", StringComparison.Ordinal) ||
               value.StartsWith("rel_", StringComparison.Ordinal);
    }

    private static bool IsRelationshipMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        string trimmed = NormalizeMessageWhitespace(message);
        return trimmed.StartsWith("У вас улучшились отношения ", System.StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("У вас ухудшились отношения ", System.StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Ваши отношения с ", System.StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Отношения с ", System.StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatRelationshipMessage(
        string statId,
        string displayName,
        int delta,
        string message,
        RelationshipMessageOverride relationshipOverride)
    {
        string trimmed = NormalizeMessageWhitespace(message);
        bool improved = ResolveRelationshipImproved(trimmed, delta);
        string target = ResolveRelationshipTarget(trimmed, displayName, statId);

        if (relationshipOverride != null)
            return relationshipOverride.Format(target, improved);

        return improved
            ? "Отношения " + target + " улучшились."
            : "Отношения " + target + " ухудшились.";
    }

    private static bool ResolveRelationshipImproved(string message, int delta)
    {
        if (delta != 0)
            return delta > 0;

        string lower = (message ?? "").ToLowerInvariant();
        if (lower.Contains("ухудш") || lower.Contains("хуже"))
            return false;

        return true;
    }

    private static string ResolveRelationshipTarget(string message, string displayName, string statId)
    {
        if (TryExtractRelationshipTarget(message, out string target))
            return NormalizeRelationshipTarget(target);

        if (!string.IsNullOrWhiteSpace(displayName))
            return NormalizeRelationshipTarget("с " + displayName);

        string statTarget = ExtractRelationshipTargetFromStatId(statId);
        if (!string.IsNullOrWhiteSpace(statTarget))
            return NormalizeRelationshipTarget("с " + statTarget);

        return "с персонажем";
    }

    private static bool TryExtractRelationshipTarget(string message, out string target)
    {
        target = "";
        string trimmed = NormalizeMessageWhitespace(message).Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        if (TryExtractAfterPrefix(trimmed, "У вас улучшились отношения ", out target) ||
            TryExtractAfterPrefix(trimmed, "У вас ухудшились отношения ", out target))
        {
            return true;
        }

        if (TryExtractBetween(trimmed, "Отношения ", " улучшились", out target) ||
            TryExtractBetween(trimmed, "Отношения ", " ухудшились", out target) ||
            TryExtractBetween(trimmed, "Отношения ", " стали лучше", out target) ||
            TryExtractBetween(trimmed, "Отношения ", " стали хуже", out target))
        {
            return true;
        }

        if (TryExtractBetween(trimmed, "Ваши отношения ", " улучшились", out target) ||
            TryExtractBetween(trimmed, "Ваши отношения ", " ухудшились", out target) ||
            TryExtractBetween(trimmed, "Ваши отношения ", " стали лучше", out target) ||
            TryExtractBetween(trimmed, "Ваши отношения ", " стали хуже", out target))
        {
            return true;
        }

        return false;
    }

    private static bool TryExtractAfterPrefix(string value, string prefix, out string target)
    {
        target = "";
        if (string.IsNullOrEmpty(value) ||
            string.IsNullOrEmpty(prefix) ||
            !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        target = value.Substring(prefix.Length).Trim();
        return !string.IsNullOrWhiteSpace(target);
    }

    private static bool TryExtractBetween(string value, string prefix, string suffix, out string target)
    {
        target = "";
        if (string.IsNullOrEmpty(value) ||
            string.IsNullOrEmpty(prefix) ||
            string.IsNullOrEmpty(suffix) ||
            !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int start = prefix.Length;
        int length = value.Length - start - suffix.Length;
        if (length <= 0)
            return false;

        target = value.Substring(start, length).Trim();
        return !string.IsNullOrWhiteSpace(target);
    }

    private static string ExtractRelationshipTargetFromStatId(string statId)
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

    private static string NormalizeMessageWhitespace(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "";

        return string.Join(" ", message.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeRelationshipTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return "";

        target = target.Trim().TrimEnd('.');
        if (target.StartsWith("с ", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith("со ", StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        return "с " + target;
    }

    private void CaptureMessageTextPresentation()
    {
        if (_messageText == null || _messageTextPresentationCaptured)
            return;

        CaptureCurrentMessageTextPresentationAsInitial();
    }

    private void CaptureCurrentMessageTextPresentationAsInitial()
    {
        if (_messageText == null)
            return;

        _messageTextInitialWordWrapping = _messageText.enableWordWrapping;
        _messageTextInitialOverflowMode = _messageText.overflowMode;
        _messageTextInitialAutoSizing = _messageText.enableAutoSizing;
        _messageTextInitialFontSize = _messageText.fontSize;
        _messageTextInitialFontSizeMin = _messageText.fontSizeMin;
        _messageTextInitialFontSizeMax = _messageText.fontSizeMax;
        _messageTextInitialMaxVisibleLines = _messageText.maxVisibleLines;
        _messageTextPresentationCaptured = true;
    }

    private void ApplyRelationshipTextPresentation()
    {
        if (_messageText == null)
            return;

        SuppressTextAutoSizeDrivers();
        ApplyRelationshipFrameLayout();

        float maxFontSize = Mathf.Max(1f, _relationshipFontSizeMax);
        float minFontSize = Mathf.Clamp(_relationshipFontSizeMin, 1f, maxFontSize);

        _messageText.enableWordWrapping = true;
        _messageText.overflowMode = TextOverflowModes.Overflow;
        _messageText.maxVisibleLines = _relationshipMaxVisibleLines;
        _messageText.enableAutoSizing = true;
        _messageText.fontSizeMax = maxFontSize;
        _messageText.fontSizeMin = minFontSize;
        _messageText.fontSize = maxFontSize;
        Canvas.ForceUpdateCanvases();
        _messageText.ForceMeshUpdate();
    }

    private void RestoreMessageTextPresentation()
    {
        if (_messageText == null || !_messageTextPresentationCaptured)
            return;

        _messageText.enableWordWrapping = _messageTextInitialWordWrapping;
        _messageText.overflowMode = _messageTextInitialOverflowMode;
        _messageText.enableAutoSizing = _messageTextInitialAutoSizing;
        _messageText.fontSize = _messageTextInitialFontSize;
        _messageText.fontSizeMin = _messageTextInitialFontSizeMin;
        _messageText.fontSizeMax = _messageTextInitialFontSizeMax;
        _messageText.maxVisibleLines = _messageTextInitialMaxVisibleLines;
    }

    private void SuppressTextAutoSizeDrivers()
    {
        EnsureTextAutoSizeDrivers();
        if (_textAutoSizeDrivers == null || _textAutoSizeDrivers.Length == 0)
            return;

        if (!_textAutoSizeDriversCaptured || _textAutoSizeDriverEnabledStates.Length != _textAutoSizeDrivers.Length)
        {
            _textAutoSizeDriverEnabledStates = new bool[_textAutoSizeDrivers.Length];
            for (int i = 0; i < _textAutoSizeDrivers.Length; i++)
                _textAutoSizeDriverEnabledStates[i] = _textAutoSizeDrivers[i] != null && _textAutoSizeDrivers[i].enabled;

            _textAutoSizeDriversCaptured = true;
        }

        for (int i = 0; i < _textAutoSizeDrivers.Length; i++)
        {
            if (_textAutoSizeDrivers[i] != null)
                _textAutoSizeDrivers[i].enabled = false;
        }

        _textAutoSizeDriversSuppressed = true;
    }

    private void RestoreTextAutoSizeDrivers()
    {
        if (!_textAutoSizeDriversSuppressed || _textAutoSizeDrivers == null)
            return;

        for (int i = 0; i < _textAutoSizeDrivers.Length; i++)
        {
            if (_textAutoSizeDrivers[i] == null)
                continue;

            bool enabledState = i < _textAutoSizeDriverEnabledStates.Length && _textAutoSizeDriverEnabledStates[i];
            _textAutoSizeDrivers[i].enabled = enabledState;
        }

        _textAutoSizeDriversSuppressed = false;
    }

    private void ApplyRelationshipFrameLayout()
    {
        RectTransform frameRect = ResolveRelationshipFrameRect();
        if (frameRect == null)
            return;

        if (!_relationshipFrameSizeCaptured)
        {
            _relationshipFrameInitialSize = frameRect.rect.size;
            _relationshipFrameRectDefaults = CaptureRectTransformDefaults(frameRect);
            _relationshipFrameSizeCaptured = true;
        }

        if (_relationshipFrameRectOverrideEnabled)
        {
            frameRect.anchoredPosition = _relationshipFrameAnchoredPosition;
            frameRect.sizeDelta = _relationshipFrameSize;
            _relationshipFrameSizeApplied = true;
        }
    }

    private void RestoreRelationshipFrameLayout()
    {
        if (!_relationshipFrameSizeApplied || !_relationshipFrameSizeCaptured || _relationshipFrameRect == null)
            return;

        RestoreRectTransformDefaults(_relationshipFrameRectDefaults);
        _relationshipFrameSizeApplied = false;
    }

    private RectTransform ResolveRelationshipFrameRect()
    {
        if (_relationshipFrameRect != null)
            return _relationshipFrameRect;

        if (_messageText != null && _messageText.transform.parent is RectTransform parentRect)
        {
            _relationshipFrameRect = parentRect;
            return _relationshipFrameRect;
        }

        _relationshipFrameRect = _panelRect;
        return _relationshipFrameRect;
    }

    private void EnsureTextAutoSizeDrivers()
    {
        if (_textAutoSizeDrivers != null && _textAutoSizeDrivers.Length > 0)
            return;

        Transform searchRoot = _panelRect != null ? _panelRect : transform;
        _textAutoSizeDrivers = searchRoot != null
            ? searchRoot.GetComponentsInChildren<ButtonTextAutoSize>(true)
            : Array.Empty<ButtonTextAutoSize>();
    }

    private string ResolveStatDisplayName(string statId, string displayName)
    {
        StatChangeOverlayDefinition definition = FindDefinition(statId);
        StatDefinition statDefinition = FindStatDefinition(statId);
        return FirstNonEmpty(displayName, definition?.displayName, statDefinition != null ? statDefinition.displayName : "", statId);
    }

    private Sprite ResolveStatIcon(string statId)
    {
        StatChangeOverlayDefinition definition = FindDefinition(statId);
        if (definition != null && definition.icon != null)
            return definition.icon;

        StatDefinition statDefinition = FindStatDefinition(statId);
        return statDefinition != null ? statDefinition.icon : null;
    }

    private StatChangeOverlayDefinition FindDefinition(string statId)
    {
        if (string.IsNullOrWhiteSpace(statId) || _definitions == null)
            return null;

        for (int i = 0; i < _definitions.Count; i++)
        {
            StatChangeOverlayDefinition definition = _definitions[i];
            if (definition != null &&
                StoryStatId.EqualsCanonical(definition.statId, statId))
            {
                return definition;
            }
        }

        return null;
    }

    private StatDefinition FindStatDefinition(string statId)
    {
        if (string.IsNullOrWhiteSpace(statId) || _statDefinitions == null)
            return null;

        for (int i = 0; i < _statDefinitions.Count; i++)
        {
            StatDefinition definition = _statDefinitions[i];
            if (definition != null &&
                StoryStatId.EqualsCanonical(definition.statId, statId))
            {
                return definition;
            }
        }

        return null;
    }

    private void ApplyPanelSizeForStat(string statId)
    {
        if (_panelRect == null)
            return;

        CaptureStoryStylePanelSizeBase();

        Vector2 sizeDelta = _storyStylePanelSizeBaseSizeDelta;
        if (TryGetStatPanelSize(statId, out Vector2 overrideSize))
            sizeDelta = overrideSize;

        ApplyRectOverride(
            _panelRect,
            _storyStylePanelSizeBaseAnchoredPosition,
            sizeDelta,
            true,
            true);
        ApplyPanelBackgroundSize(sizeDelta);
        _shownAnchoredPosition = _storyStylePanelSizeBaseShownPosition;
    }

    private void ApplyPanelBackgroundSize(Vector2 sizeDelta)
    {
        Image panelImage = FindPanelBackgroundImage();
        RectTransform backgroundRect = panelImage != null ? panelImage.rectTransform : null;
        if (backgroundRect == null || backgroundRect == _panelRect)
            return;

        Vector2 visualSize = ResolvePositiveSize(sizeDelta);
        RectTransform sizeTarget = ResolvePanelBackgroundSizeTarget(backgroundRect);
        if (!IsStretchRect(sizeTarget))
            ApplyRectSize(sizeTarget, visualSize);

        if (sizeTarget != backgroundRect && !IsStretchRect(backgroundRect))
            ApplyRectSize(backgroundRect, visualSize);

        LayoutElement layoutElement = sizeTarget != null ? sizeTarget.GetComponent<LayoutElement>() : null;
        if (layoutElement != null)
        {
            layoutElement.minWidth = visualSize.x;
            layoutElement.preferredWidth = visualSize.x;
            layoutElement.minHeight = visualSize.y;
            layoutElement.preferredHeight = visualSize.y;
        }

        if (sizeTarget != null)
            LayoutRebuilder.MarkLayoutForRebuild(sizeTarget);
        LayoutRebuilder.MarkLayoutForRebuild(backgroundRect);
    }

    private RectTransform ResolvePanelBackgroundSizeTarget(RectTransform backgroundRect)
    {
        if (backgroundRect == null || _panelRect == null)
            return backgroundRect;

        RectTransform current = backgroundRect;
        RectTransform candidate = backgroundRect;
        while (current != null && current != _panelRect)
        {
            candidate = current;
            current = current.parent as RectTransform;
        }

        return candidate != null ? candidate : backgroundRect;
    }

    private static void ApplyRectSize(RectTransform rect, Vector2 size)
    {
        if (rect == null)
            return;

        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
    }

    private static bool IsStretchRect(RectTransform rect)
    {
        return rect != null &&
               (!Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x) ||
                !Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y));
    }

    private static Vector2 ResolvePositiveSize(Vector2 size)
    {
        return new Vector2(
            Mathf.Max(0f, Mathf.Abs(size.x)),
            Mathf.Max(0f, Mathf.Abs(size.y)));
    }

    private void CaptureStoryStylePanelSizeBase()
    {
        if (_storyStylePanelSizeBaseCaptured || _panelRect == null)
            return;

        _storyStylePanelSizeBaseAnchoredPosition = _panelRect.anchoredPosition;
        _storyStylePanelSizeBaseSizeDelta = _panelRect.sizeDelta;
        _storyStylePanelSizeBaseShownPosition = _shownAnchoredPosition;
        _storyStylePanelSizeBaseCaptured = true;
    }

    private void ApplyTextRectForStat(string statId)
    {
        if (_messageText == null)
            return;
        if (_statTextRectOverrides == null || _statTextRectOverrides.Count == 0)
            return;

        RectTransform textRect = _messageText.rectTransform;
        if (textRect == null)
            return;

        CaptureStoryStyleTextRectBase();

        Vector2 anchoredPosition = _storyStyleTextRectBaseAnchoredPosition;
        Vector2 sizeDelta = _storyStyleTextRectBaseSizeDelta;
        if (TryGetStatTextRect(statId, out Vector2 overrideAnchoredPosition, out Vector2 overrideSizeDelta))
        {
            anchoredPosition = overrideAnchoredPosition;
            sizeDelta = overrideSizeDelta;
        }

        ApplyTextRectOverrideAfterLayout(textRect, anchoredPosition, sizeDelta);
    }

    private void CaptureStoryStyleTextRectBase()
    {
        if (_storyStyleTextRectBaseCaptured || _messageText == null)
            return;

        RectTransform textRect = _messageText.rectTransform;
        if (textRect == null)
            return;

        _storyStyleTextRectBaseAnchoredPosition = textRect.anchoredPosition;
        _storyStyleTextRectBaseSizeDelta = textRect.sizeDelta;
        _storyStyleTextRectBaseCaptured = true;
    }

    private bool TryGetStatPanelSize(string statId, out Vector2 panelSizeDelta)
    {
        panelSizeDelta = default;

        if (string.IsNullOrWhiteSpace(statId) || _statPanelSizeOverrides == null)
            return false;

        for (int i = 0; i < _statPanelSizeOverrides.Count; i++)
        {
            StatPanelSizeOverride entry = _statPanelSizeOverrides[i];
            if (entry == null || !entry.Matches(statId))
                continue;

            panelSizeDelta = entry.panelSizeDelta;
            return true;
        }

        return false;
    }

    private bool TryGetStatTextRect(string statId, out Vector2 anchoredPosition, out Vector2 sizeDelta)
    {
        anchoredPosition = default;
        sizeDelta = default;

        if (string.IsNullOrWhiteSpace(statId) || _statTextRectOverrides == null)
            return false;

        for (int i = 0; i < _statTextRectOverrides.Count; i++)
        {
            StatTextRectOverride entry = _statTextRectOverrides[i];
            if (entry == null || !entry.Matches(statId))
                continue;

            anchoredPosition = entry.textAnchoredPosition;
            sizeDelta = entry.textSizeDelta;
            return true;
        }

        return false;
    }

    private void ApplyIconImageSettingsForStat(
        string statId,
        bool hasIcon,
        bool updateVisibility,
        bool applyPositionOffset)
    {
        if (!TryGetStatIconOffset(statId, out Vector2 iconOffset))
        {
            ApplyIconImageSettings(hasIcon, updateVisibility, applyPositionOffset);
            return;
        }

        Vector2 previousOffset = _iconAnchoredOffset;
        bool previousAbsoluteOffset = _iconUseAbsoluteAnchoredOffset;
        bool previousIgnoreLayout = _ignoreIconInLayout;
        bool previousApplyIconSettings = _applyIconImageSettings;

        _applyIconImageSettings = true;
        _iconAnchoredOffset = iconOffset;
        _iconUseAbsoluteAnchoredOffset = true;
        _ignoreIconInLayout = true;

        ApplyIconImageSettings(hasIcon, updateVisibility, applyPositionOffset);

        _iconAnchoredOffset = previousOffset;
        _iconUseAbsoluteAnchoredOffset = previousAbsoluteOffset;
        _ignoreIconInLayout = previousIgnoreLayout;
        _applyIconImageSettings = previousApplyIconSettings;
    }

    private bool TryGetStatIconOffset(string statId, out Vector2 iconOffset)
    {
        iconOffset = default;

        if (string.IsNullOrWhiteSpace(statId) || _statIconOffsetOverrides == null)
            return false;

        for (int i = 0; i < _statIconOffsetOverrides.Count; i++)
        {
            StatIconOffsetOverride entry = _statIconOffsetOverrides[i];
            if (entry == null || !entry.Matches(statId))
                continue;

            iconOffset = entry.iconOffset;
            return true;
        }

        return false;
    }

    private RelationshipMessageOverride FindRelationshipMessageOverride(string statId)
    {
        if (_relationshipMessageOverrides == null)
            return null;

        RelationshipMessageOverride fallback = null;
        for (int i = 0; i < _relationshipMessageOverrides.Count; i++)
        {
            RelationshipMessageOverride entry = _relationshipMessageOverrides[i];
            if (entry == null)
                continue;

            if (entry.Matches(statId))
                return entry;

            if (fallback == null && string.IsNullOrWhiteSpace(entry.statId))
                fallback = entry;
        }

        return fallback;
    }

    private Vector2 ResolveShownPosition()
    {
        if (_panelRect == null)
            return _shownAnchoredPosition;

        if (_shownAnchoredPosition == Vector2.zero && _captureShownPositionOnAwake)
            _shownAnchoredPosition = _panelRect.anchoredPosition;

        return _shownAnchoredPosition;
    }

    private Vector2 GetSlideOffset()
    {
        switch (_slideDirection)
        {
            case StatChangeOverlaySlideDirection.Down:
                return Vector2.up * _slideOffset;
            case StatChangeOverlaySlideDirection.Left:
                return Vector2.right * _slideOffset;
            case StatChangeOverlaySlideDirection.Right:
                return Vector2.left * _slideOffset;
            case StatChangeOverlaySlideDirection.Up:
            default:
                return Vector2.down * _slideOffset;
        }
    }

    private void CaptureStoryStyleDefaults()
    {
        if (_storyStyleDefaultsCaptured)
            return;

        Image panelImage = FindPanelBackgroundImage();
        _storyStylePanelImageDefaults = CaptureImageDefaults(panelImage);
        _storyStylePanelBackgroundRectDefaults = CaptureRectTransformDefaults(panelImage != null ? panelImage.rectTransform : null);
        _storyStylePanelRectDefaults = CaptureRectTransformDefaults(_panelRect);
        _storyStyleShownAnchoredPosition = _shownAnchoredPosition;
        _storyStylePanelVerticalLayoutDefaults = CapturePanelVerticalLayoutDefaults(FindPanelVerticalLayoutGroup());
        _storyStylePanelContentSizeFitterDefaults = CapturePanelContentSizeFitterDefaults(FindPanelContentSizeFitter());

        if (_messageText != null)
        {
            _storyStyleMessageTextColor = _messageText.color;
            _storyStyleMessageTextFont = _messageText.font;
            _storyStyleMessageTextFontSize = _messageText.fontSize;
            _storyStyleMessageTextRectDefaults = CaptureRectTransformDefaults(_messageText.rectTransform);
            _storyStyleMessageTextLayoutDefaults = CaptureTextLayoutDefaults(_messageText);
            _storyStyleTextDefaultsCaptured = true;
        }

        _storyStyleIconLayoutDefaults = CaptureIconLayoutDefaults();

        _storyStyleDefaultDefinitions = _definitions != null
            ? new List<StatChangeOverlayDefinition>(_definitions)
            : new List<StatChangeOverlayDefinition>();
        _storyStyleDefaultStatDefinitions = _statDefinitions != null
            ? new List<StatDefinition>(_statDefinitions)
            : new List<StatDefinition>();
        _storyStyleDefaultRelationshipMessageOverrides = CloneRelationshipMessageOverrides(_relationshipMessageOverrides);
        _storyStyleDefaultPanelSizeOverrides = CloneStatPanelSizeOverrides(_statPanelSizeOverrides);
        _storyStyleDefaultTextRectOverrides = CloneStatTextRectOverrides(_statTextRectOverrides);

        _storyStyleDefaultsCaptured = true;
    }

    private void RestoreStoryStyleDefaults()
    {
        if (!_storyStyleDefaultsCaptured)
            return;

        RestoreImageDefaults(_storyStylePanelImageDefaults);
        RestoreRectTransformDefaults(_storyStylePanelBackgroundRectDefaults);
        RestoreRectTransformDefaults(_storyStylePanelRectDefaults);
        RestorePanelVerticalLayoutDefaults(_storyStylePanelVerticalLayoutDefaults);
        RestorePanelContentSizeFitterDefaults(_storyStylePanelContentSizeFitterDefaults);
        _shownAnchoredPosition = _storyStyleShownAnchoredPosition;
        _storyStyleStatPanelBackgroundOverrides = default;
        _storyStyleRelationshipPanelBackgroundOverrides = default;
        _storyStyleStatPanelLayoutOverrides = default;
        _storyStyleRelationshipPanelLayoutOverrides = default;
        _storyStylePanelSizeBaseCaptured = false;
        _storyStyleTextRectBaseCaptured = false;

        if (_storyStyleTextDefaultsCaptured && _messageText != null)
        {
            RestoreRectTransformDefaults(_storyStyleMessageTextRectDefaults);
            RestoreTextLayoutDefaults(_storyStyleMessageTextLayoutDefaults);
            _messageText.color = _storyStyleMessageTextColor;
            _messageText.font = _storyStyleMessageTextFont;
            _messageText.fontSize = _storyStyleMessageTextFontSize;
            _messageText.SetAllDirty();
            CaptureCurrentMessageTextPresentationAsInitial();
        }

        _definitions = _storyStyleDefaultDefinitions != null
            ? new List<StatChangeOverlayDefinition>(_storyStyleDefaultDefinitions)
            : new List<StatChangeOverlayDefinition>();
        _statDefinitions = _storyStyleDefaultStatDefinitions != null
            ? new List<StatDefinition>(_storyStyleDefaultStatDefinitions)
            : new List<StatDefinition>();
        _relationshipMessageOverrides = CloneRelationshipMessageOverrides(_storyStyleDefaultRelationshipMessageOverrides);
        _statPanelSizeOverrides = CloneStatPanelSizeOverrides(_storyStyleDefaultPanelSizeOverrides);
        _statTextRectOverrides = CloneStatTextRectOverrides(_storyStyleDefaultTextRectOverrides);

        RestoreIconLayoutDefaults(_storyStyleIconLayoutDefaults);
    }

    private static ImageDefaults CaptureImageDefaults(Image image)
    {
        if (image == null)
            return default;

        return new ImageDefaults
        {
            Target = image,
            Sprite = image.sprite,
            Color = image.color,
            Type = image.type,
            PreserveAspect = image.preserveAspect,
            PixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier,
            Material = image.material,
            RaycastTarget = image.raycastTarget,
            Captured = true
        };
    }

    private static void RestoreImageDefaults(ImageDefaults defaults)
    {
        Image image = defaults.Target;
        if (!defaults.Captured || image == null)
            return;

        image.sprite = defaults.Sprite;
        image.color = defaults.Color;
        image.type = defaults.Type;
        image.preserveAspect = defaults.PreserveAspect;
        image.pixelsPerUnitMultiplier = defaults.PixelsPerUnitMultiplier;
        image.material = defaults.Material;
        image.raycastTarget = defaults.RaycastTarget;
        image.SetAllDirty();
    }

    private static RectTransformDefaults CaptureRectTransformDefaults(RectTransform rect)
    {
        if (rect == null)
            return default;

        LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
        return new RectTransformDefaults
        {
            Target = rect,
            AnchorMin = rect.anchorMin,
            AnchorMax = rect.anchorMax,
            AnchoredPosition = rect.anchoredPosition,
            SizeDelta = rect.sizeDelta,
            Pivot = rect.pivot,
            LocalScale = rect.localScale,
            LayoutElement = layoutElement,
            LayoutElementIgnoreLayout = layoutElement != null && layoutElement.ignoreLayout,
            Captured = true
        };
    }

    private static void RestoreRectTransformDefaults(RectTransformDefaults defaults)
    {
        RectTransform rect = defaults.Target;
        if (!defaults.Captured || rect == null)
            return;

        rect.anchorMin = defaults.AnchorMin;
        rect.anchorMax = defaults.AnchorMax;
        rect.anchoredPosition = defaults.AnchoredPosition;
        rect.sizeDelta = defaults.SizeDelta;
        rect.pivot = defaults.Pivot;
        rect.localScale = defaults.LocalScale;

        if (defaults.LayoutElement != null)
        {
            defaults.LayoutElement.ignoreLayout = defaults.LayoutElementIgnoreLayout;
        }
        else
        {
            LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
            if (layoutElement != null)
                layoutElement.ignoreLayout = false;
        }
    }

    private static TextLayoutDefaults CaptureTextLayoutDefaults(TMP_Text text)
    {
        if (text == null)
            return default;

        return new TextLayoutDefaults
        {
            Target = text,
            EnableAutoSizing = text.enableAutoSizing,
            FontSize = text.fontSize,
            FontSizeMin = text.fontSizeMin,
            FontSizeMax = text.fontSizeMax,
            Alignment = text.alignment,
            EnableWordWrapping = text.enableWordWrapping,
            OverflowMode = text.overflowMode,
            LineSpacing = text.lineSpacing,
            Margin = text.margin,
            Captured = true
        };
    }

    private static void RestoreTextLayoutDefaults(TextLayoutDefaults defaults)
    {
        TMP_Text text = defaults.Target;
        if (!defaults.Captured || text == null)
            return;

        text.enableAutoSizing = defaults.EnableAutoSizing;
        text.fontSize = defaults.FontSize;
        text.fontSizeMin = defaults.FontSizeMin;
        text.fontSizeMax = defaults.FontSizeMax;
        text.alignment = defaults.Alignment;
        text.enableWordWrapping = defaults.EnableWordWrapping;
        text.overflowMode = defaults.OverflowMode;
        text.lineSpacing = defaults.LineSpacing;
        text.margin = defaults.Margin;
    }

    private static LayoutGroupDefaults CapturePanelVerticalLayoutDefaults(VerticalLayoutGroup layoutGroup)
    {
        if (layoutGroup == null)
            return default;

        return new LayoutGroupDefaults
        {
            Target = layoutGroup,
            Padding = CopyRectOffset(layoutGroup.padding),
            ChildAlignment = layoutGroup.childAlignment,
            Spacing = layoutGroup.spacing,
            ReverseArrangement = layoutGroup.reverseArrangement,
            ChildControlWidth = layoutGroup.childControlWidth,
            ChildControlHeight = layoutGroup.childControlHeight,
            ChildScaleWidth = layoutGroup.childScaleWidth,
            ChildScaleHeight = layoutGroup.childScaleHeight,
            ChildForceExpandWidth = layoutGroup.childForceExpandWidth,
            ChildForceExpandHeight = layoutGroup.childForceExpandHeight,
            Captured = true
        };
    }

    private static void RestorePanelVerticalLayoutDefaults(LayoutGroupDefaults defaults)
    {
        VerticalLayoutGroup layoutGroup = defaults.Target;
        if (!defaults.Captured || layoutGroup == null)
            return;

        CopyRectOffset(defaults.Padding, layoutGroup.padding);
        layoutGroup.childAlignment = defaults.ChildAlignment;
        layoutGroup.spacing = defaults.Spacing;
        layoutGroup.reverseArrangement = defaults.ReverseArrangement;
        layoutGroup.childControlWidth = defaults.ChildControlWidth;
        layoutGroup.childControlHeight = defaults.ChildControlHeight;
        layoutGroup.childScaleWidth = defaults.ChildScaleWidth;
        layoutGroup.childScaleHeight = defaults.ChildScaleHeight;
        layoutGroup.childForceExpandWidth = defaults.ChildForceExpandWidth;
        layoutGroup.childForceExpandHeight = defaults.ChildForceExpandHeight;

        if (layoutGroup.transform is RectTransform rect)
            LayoutRebuilder.MarkLayoutForRebuild(rect);
    }

    private static ContentSizeFitterDefaults CapturePanelContentSizeFitterDefaults(ContentSizeFitter fitter)
    {
        if (fitter == null)
            return default;

        return new ContentSizeFitterDefaults
        {
            Target = fitter,
            HorizontalFit = fitter.horizontalFit,
            VerticalFit = fitter.verticalFit,
            Captured = true
        };
    }

    private static void RestorePanelContentSizeFitterDefaults(ContentSizeFitterDefaults defaults)
    {
        ContentSizeFitter fitter = defaults.Target;
        if (!defaults.Captured || fitter == null)
            return;

        fitter.horizontalFit = defaults.HorizontalFit;
        fitter.verticalFit = defaults.VerticalFit;

        if (fitter.transform is RectTransform rect)
            LayoutRebuilder.MarkLayoutForRebuild(rect);
    }

    private IconLayoutDefaults CaptureIconLayoutDefaults()
    {
        ButtonTextAutoSize panelAutoSize = FindPanelAutoSizeDriver();
        RectTransform iconRect = _iconImage != null ? _iconImage.rectTransform : null;
        HorizontalOrVerticalLayoutGroup parentLayoutGroup = null;

        if (_iconImage != null && _iconImage.transform.parent != null)
            parentLayoutGroup = _iconImage.transform.parent.GetComponent<HorizontalOrVerticalLayoutGroup>();

        return new IconLayoutDefaults
        {
            PanelAutoSize = panelAutoSize,
            PanelPadding = panelAutoSize != null ? panelAutoSize.Padding : Vector2.zero,
            IconRect = iconRect,
            IconAnchoredPosition = iconRect != null ? iconRect.anchoredPosition : Vector2.zero,
            IconSize = iconRect != null ? iconRect.rect.size : Vector2.zero,
            IconScale = iconRect != null ? iconRect.localScale : Vector3.one,
            IconUseAbsoluteAnchoredOffset = _iconUseAbsoluteAnchoredOffset,
            ApplyIconImageSettings = _applyIconImageSettings,
            ReserveIconSpaceWhenHidden = _reserveIconSpaceWhenHidden,
            IconWidth = _iconWidth,
            IconHeight = _iconHeight,
            ApplyIconVisualScale = _applyIconVisualScale,
            IconVisualScale = _iconVisualScale,
            IconMinWidth = _iconMinWidth,
            IconMinHeight = _iconMinHeight,
            IconAnchoredOffset = _iconAnchoredOffset,
            ApplyIconStretchPadding = _applyIconStretchPadding,
            IconStretchPadding = CopyRectOffset(_iconStretchPadding),
            DriveIconLayoutElement = _driveIconLayoutElement,
            IgnoreIconInLayout = _ignoreIconInLayout,
            ApplyParentLayoutSpacing = _applyParentLayoutSpacing,
            ParentLayoutSpacing = _parentLayoutSpacing,
            ApplyParentLayoutPadding = _applyParentLayoutPadding,
            ParentLayoutPadding = CopyRectOffset(_parentLayoutPadding),
            PreserveIconAspect = _preserveIconAspect,
            IconRaycastTarget = _iconRaycastTarget,
            ForceIconImageType = _forceIconImageType,
            IconImageType = _iconImageType,
            OverrideIconColor = _overrideIconColor,
            IconColor = _iconColor,
            IconAlpha = _iconAlpha,
            ParentLayoutGroup = parentLayoutGroup,
            ParentLayoutGroupSpacing = parentLayoutGroup != null ? parentLayoutGroup.spacing : 0f,
            ParentLayoutGroupPadding = parentLayoutGroup != null ? CopyRectOffset(parentLayoutGroup.padding) : null,
            Captured = true
        };
    }

    private void RestoreIconLayoutDefaults(IconLayoutDefaults defaults)
    {
        if (!defaults.Captured)
            return;

        if (defaults.PanelAutoSize != null)
        {
            defaults.PanelAutoSize.SetPadding(defaults.PanelPadding);
            defaults.PanelAutoSize.RefreshNow();
        }

        _applyIconImageSettings = defaults.ApplyIconImageSettings;
        _reserveIconSpaceWhenHidden = defaults.ReserveIconSpaceWhenHidden;
        _iconWidth = defaults.IconWidth;
        _iconHeight = defaults.IconHeight;
        _applyIconVisualScale = defaults.ApplyIconVisualScale;
        _iconVisualScale = defaults.IconVisualScale;
        _iconMinWidth = defaults.IconMinWidth;
        _iconMinHeight = defaults.IconMinHeight;
        _iconAnchoredOffset = defaults.IconAnchoredOffset;
        _iconUseAbsoluteAnchoredOffset = defaults.IconUseAbsoluteAnchoredOffset;
        _applyIconStretchPadding = defaults.ApplyIconStretchPadding;
        _iconStretchPadding = CopyRectOffset(defaults.IconStretchPadding);
        _driveIconLayoutElement = defaults.DriveIconLayoutElement;
        _ignoreIconInLayout = defaults.IgnoreIconInLayout;
        _applyParentLayoutSpacing = defaults.ApplyParentLayoutSpacing;
        _parentLayoutSpacing = defaults.ParentLayoutSpacing;
        _applyParentLayoutPadding = defaults.ApplyParentLayoutPadding;
        _parentLayoutPadding = CopyRectOffset(defaults.ParentLayoutPadding);
        _preserveIconAspect = defaults.PreserveIconAspect;
        _iconRaycastTarget = defaults.IconRaycastTarget;
        _forceIconImageType = defaults.ForceIconImageType;
        _iconImageType = defaults.IconImageType;
        _overrideIconColor = defaults.OverrideIconColor;
        _iconColor = defaults.IconColor;
        _iconAlpha = defaults.IconAlpha;

        if (defaults.IconRect != null)
        {
            _iconRect = defaults.IconRect;
            _iconCapturedAnchoredPosition = defaults.IconAnchoredPosition;
            defaults.IconRect.anchoredPosition = defaults.IconAnchoredPosition;
            defaults.IconRect.localScale = defaults.IconScale;

            if (defaults.IconSize.x > 0f)
                defaults.IconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, defaults.IconSize.x);
            if (defaults.IconSize.y > 0f)
                defaults.IconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, defaults.IconSize.y);
        }

        if (defaults.ParentLayoutGroup != null)
        {
            defaults.ParentLayoutGroup.spacing = defaults.ParentLayoutGroupSpacing;
            CopyRectOffset(defaults.ParentLayoutGroupPadding, defaults.ParentLayoutGroup.padding);
        }

        bool hasIcon = _iconImage != null && (_iconImage.sprite != null || _reserveIconSpaceWhenHidden);
        ApplyIconImageSettings(hasIcon, false, true);
    }

    private void ApplyPanelPadding(Vector2 panelPadding)
    {
        ButtonTextAutoSize panelAutoSize = FindPanelAutoSizeDriver();
        bool hasAutoSizeDriver = panelAutoSize != null;
        bool appliedToLayoutGroup = false;

        if (hasAutoSizeDriver)
        {
            panelAutoSize.SetPadding(panelPadding.x, panelPadding.y);
            panelAutoSize.RefreshNow();
        }
        else
        {
            appliedToLayoutGroup = ApplyPanelPaddingToLayoutGroups(panelPadding);
        }

        if (!hasAutoSizeDriver && !appliedToLayoutGroup)
            ApplyPanelPaddingToTextMargin(panelPadding);

        RebuildLayoutForImmediatePreview();
    }

    private bool ApplyPanelPaddingToLayoutGroups(Vector2 panelPadding)
    {
        Transform searchRoot = _panelRect != null ? _panelRect : transform;
        if (searchRoot == null)
            return false;

        HorizontalOrVerticalLayoutGroup[] layoutGroups = searchRoot.GetComponentsInChildren<HorizontalOrVerticalLayoutGroup>(true);
        if (layoutGroups == null || layoutGroups.Length == 0)
            return false;

        RectOffset padding = BuildSymmetricPadding(panelPadding);
        bool applied = false;
        for (int i = 0; i < layoutGroups.Length; i++)
        {
            HorizontalOrVerticalLayoutGroup layoutGroup = layoutGroups[i];
            if (layoutGroup == null || layoutGroup.GetComponentInParent<Button>() != null)
                continue;

            CopyRectOffset(padding, layoutGroup.padding);
            if (layoutGroup.transform is RectTransform rect)
                LayoutRebuilder.MarkLayoutForRebuild(rect);
            applied = true;
        }

        return applied;
    }

    private void ApplyPanelPaddingToTextMargin(Vector2 panelPadding)
    {
        if (_messageText == null)
            return;

        float horizontal = panelPadding.x * 0.5f;
        float vertical = panelPadding.y * 0.5f;
        _messageText.margin = new Vector4(horizontal, vertical, horizontal, vertical);
        _messageText.SetAllDirty();
        _messageText.ForceMeshUpdate();
    }

    private static RectOffset BuildSymmetricPadding(Vector2 panelPadding)
    {
        int horizontal = Mathf.RoundToInt(panelPadding.x * 0.5f);
        int vertical = Mathf.RoundToInt(panelPadding.y * 0.5f);
        return new RectOffset(horizontal, horizontal, vertical, vertical);
    }

    private static void ApplyRectOverride(
        RectTransform rect,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        bool ignoreLayout,
        bool allowNegativeSizeDelta = false)
    {
        if (rect == null)
            return;

        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
        if (layoutElement == null && ignoreLayout)
            layoutElement = AddLayoutElement(rect.gameObject);
        if (layoutElement != null && ignoreLayout)
        {
            Vector2 layoutSize = ResolveRectLayoutSize(rect, sizeDelta);
            layoutElement.ignoreLayout = true;
            layoutElement.minWidth = layoutSize.x;
            layoutElement.minHeight = layoutSize.y;
            layoutElement.preferredWidth = layoutSize.x;
            layoutElement.preferredHeight = layoutSize.y;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
        }

        LayoutRebuilder.MarkLayoutForRebuild(rect);
        if (rect.parent is RectTransform parentRect)
            LayoutRebuilder.MarkLayoutForRebuild(parentRect);
    }

    private static void SetRectIgnoreLayout(RectTransform rect, bool ignoreLayout)
    {
        if (rect == null)
            return;

        LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
        if (layoutElement == null && ignoreLayout)
            layoutElement = AddLayoutElement(rect.gameObject);

        if (layoutElement == null)
            return;

        layoutElement.ignoreLayout = ignoreLayout;
        if (ignoreLayout)
        {
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
        }
    }

    private static void ApplyTextRectOverrideWithoutLayoutElement(
        RectTransform textRect,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        if (textRect == null)
            return;

        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = sizeDelta;
        RemoveLayoutElementFromRect(textRect);

        LayoutRebuilder.MarkLayoutForRebuild(textRect);
        if (textRect.parent is RectTransform parentRect)
            LayoutRebuilder.MarkLayoutForRebuild(parentRect);
    }

    private static Vector2 ResolveRectLayoutSize(RectTransform rect, Vector2 requestedSizeDelta)
    {
        if (rect == null)
            return new Vector2(
                Mathf.Max(0f, requestedSizeDelta.x),
                Mathf.Max(0f, requestedSizeDelta.y));

        Vector2 rectSize = rect.rect.size;
        return new Vector2(
            ResolveRectLayoutAxis(rectSize.x, requestedSizeDelta.x),
            ResolveRectLayoutAxis(rectSize.y, requestedSizeDelta.y));
    }

    private static float ResolveRectLayoutAxis(float rectSize, float requestedSizeDelta)
    {
        float size = Mathf.Abs(rectSize);
        if (size > 0.5f)
            return size;

        return Mathf.Max(0f, Mathf.Abs(requestedSizeDelta));
    }

    private void ApplyTextRectOverrideAfterLayout(
        RectTransform textRect,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        if (textRect == null)
            return;

        ApplyTextRectOverrideWithoutLayoutElement(textRect, anchoredPosition, sizeDelta);

        if (!Application.isPlaying)
        {
            Canvas.ForceUpdateCanvases();
            ApplyTextRectOverrideWithoutLayoutElement(textRect, anchoredPosition, sizeDelta);
            textRect.ForceUpdateRectTransforms();
        }

        if (_messageText != null)
        {
            _messageText.SetAllDirty();
            _messageText.ForceMeshUpdate();
        }
    }

    private StatChangeOverlayStoryLayoutOverride FindStoryLayoutOverride(string storyId)
    {
        if (string.IsNullOrWhiteSpace(storyId) || _storyLayoutOverrides == null)
            return null;

        for (int i = 0; i < _storyLayoutOverrides.Count; i++)
        {
            StatChangeOverlayStoryLayoutOverride layoutOverride = _storyLayoutOverrides[i];
            if (layoutOverride != null && layoutOverride.Matches(storyId))
                return layoutOverride;
        }

        return null;
    }

    private void ValidateStoryLayoutOverrides()
    {
        if (_storyLayoutOverrides == null)
        {
            _storyLayoutOverrides = new List<StatChangeOverlayStoryLayoutOverride>();
            return;
        }

        _editorPreviewStoryId = NormalizeStoryId(_editorPreviewStoryId);

        for (int i = 0; i < _storyLayoutOverrides.Count; i++)
            _storyLayoutOverrides[i]?.Validate();
    }

    private void ValidateStatIconOffsetOverrides()
    {
        if (_statIconOffsetOverrides == null)
        {
            _statIconOffsetOverrides = new List<StatIconOffsetOverride>();
            return;
        }

        for (int i = 0; i < _statIconOffsetOverrides.Count; i++)
            _statIconOffsetOverrides[i]?.Validate();
    }

    private void ValidateStatPanelSizeOverrides()
    {
        if (_statPanelSizeOverrides == null)
        {
            _statPanelSizeOverrides = new List<StatPanelSizeOverride>();
            return;
        }

        for (int i = 0; i < _statPanelSizeOverrides.Count; i++)
            _statPanelSizeOverrides[i]?.Validate();
    }

    private void ValidateStatTextRectOverrides()
    {
        if (_statTextRectOverrides == null)
        {
            _statTextRectOverrides = new List<StatTextRectOverride>();
            return;
        }

        for (int i = 0; i < _statTextRectOverrides.Count; i++)
            _statTextRectOverrides[i]?.Validate();
    }

    private void ValidateRelationshipMessageOverrides()
    {
        if (_relationshipMessageOverrides == null)
        {
            _relationshipMessageOverrides = new List<RelationshipMessageOverride>();
            return;
        }

        for (int i = 0; i < _relationshipMessageOverrides.Count; i++)
            _relationshipMessageOverrides[i]?.Validate();
    }

    private static List<RelationshipMessageOverride> CloneRelationshipMessageOverrides(
        IReadOnlyList<RelationshipMessageOverride> source)
    {
        var result = new List<RelationshipMessageOverride>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            RelationshipMessageOverride entry = source[i];
            if (entry == null)
                continue;

            entry.Validate();
            result.Add(entry.Clone());
        }

        return result;
    }

    private static List<StatPanelSizeOverride> CloneStatPanelSizeOverrides(
        IReadOnlyList<StatPanelSizeOverride> source)
    {
        var result = new List<StatPanelSizeOverride>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            StatPanelSizeOverride entry = source[i];
            if (entry == null)
                continue;

            entry.Validate();
            result.Add(entry.Clone());
        }

        return result;
    }

    private static List<StatTextRectOverride> CloneStatTextRectOverrides(
        IReadOnlyList<StatTextRectOverride> source)
    {
        var result = new List<StatTextRectOverride>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            StatTextRectOverride entry = source[i];
            if (entry == null)
                continue;

            entry.Validate();
            result.Add(entry.Clone());
        }

        return result;
    }

    private string ResolveActiveStoryId(string storyIdOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(storyIdOverride))
            return NormalizeStoryId(storyIdOverride);

        if (!Application.isPlaying && !string.IsNullOrWhiteSpace(_editorPreviewStoryId))
            return NormalizeStoryId(_editorPreviewStoryId);

        StoryManager storyManager = StoryManager.Instance;
        if (storyManager == null && !Application.isPlaying)
            storyManager = FindObjectOfType<StoryManager>(true);

        if (storyManager != null && !string.IsNullOrWhiteSpace(storyManager.CurrentStoryId))
            return NormalizeStoryId(storyManager.CurrentStoryId);

        if (GameState.Instance != null && !string.IsNullOrWhiteSpace(GameState.Instance.CurrentStoryId))
            return NormalizeStoryId(GameState.Instance.CurrentStoryId);

        return "";
    }

    private Vector2 ResolveCurrentIconSize()
    {
        if (_iconWidth > 0f || _iconHeight > 0f)
            return new Vector2(_iconWidth, _iconHeight);

        RectTransform iconRect = _iconImage != null ? _iconImage.rectTransform : _iconRect;
        if (iconRect == null)
            return Vector2.zero;

        return iconRect.rect.size;
    }

    private static string NormalizeStoryId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }

    private ButtonTextAutoSize FindPanelAutoSizeDriver()
    {
        Transform searchRoot = _panelRect != null ? _panelRect : transform;
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

    private VerticalLayoutGroup FindPanelVerticalLayoutGroup()
    {
        Transform searchRoot = _panelRect != null ? _panelRect : transform;
        if (_panelVerticalLayoutGroup != null &&
            _panelVerticalLayoutGroup.transform != null &&
            searchRoot != null &&
            _panelVerticalLayoutGroup.transform.IsChildOf(searchRoot))
        {
            return _panelVerticalLayoutGroup;
        }

        _panelVerticalLayoutGroup = null;
        if (searchRoot == null)
            return null;

        VerticalLayoutGroup[] groups = searchRoot.GetComponentsInChildren<VerticalLayoutGroup>(true);
        if (groups == null || groups.Length == 0)
            return null;

        Transform textTransform = _messageText != null ? _messageText.transform : null;
        for (int i = 0; i < groups.Length; i++)
        {
            VerticalLayoutGroup group = groups[i];
            if (group != null &&
                textTransform != null &&
                textTransform.IsChildOf(group.transform) &&
                group.GetComponentInParent<Button>() == null)
            {
                _panelVerticalLayoutGroup = group;
                return _panelVerticalLayoutGroup;
            }
        }

        for (int i = 0; i < groups.Length; i++)
        {
            VerticalLayoutGroup group = groups[i];
            if (group != null &&
                group.GetComponentInParent<Button>() == null &&
                string.Equals(group.name, "Container", StringComparison.OrdinalIgnoreCase))
            {
                _panelVerticalLayoutGroup = group;
                return _panelVerticalLayoutGroup;
            }
        }

        for (int i = 0; i < groups.Length; i++)
        {
            VerticalLayoutGroup group = groups[i];
            if (group != null && group.GetComponentInParent<Button>() == null)
            {
                _panelVerticalLayoutGroup = group;
                return _panelVerticalLayoutGroup;
            }
        }

        return null;
    }

    private ContentSizeFitter FindPanelContentSizeFitter()
    {
        VerticalLayoutGroup layoutGroup = FindPanelVerticalLayoutGroup();
        if (layoutGroup == null)
        {
            _panelContentSizeFitter = null;
            return null;
        }

        if (_panelContentSizeFitter != null &&
            _panelContentSizeFitter.transform == layoutGroup.transform)
        {
            return _panelContentSizeFitter;
        }

        _panelContentSizeFitter = layoutGroup.GetComponent<ContentSizeFitter>();
        return _panelContentSizeFitter;
    }

    private void MarkPanelLayoutGroupDirty()
    {
        RectTransform layoutRect = _panelVerticalLayoutGroup != null
            ? _panelVerticalLayoutGroup.transform as RectTransform
            : null;

        if (layoutRect != null)
            LayoutRebuilder.MarkLayoutForRebuild(layoutRect);

        if (_panelRect != null)
            LayoutRebuilder.MarkLayoutForRebuild(_panelRect);

        if (!Application.isPlaying)
        {
            if (layoutRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRect);
            if (_panelRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
            Canvas.ForceUpdateCanvases();
        }
    }

    private void MarkPanelBackgroundRectDirty(RectTransform backgroundRect)
    {
        if (backgroundRect != null)
            LayoutRebuilder.MarkLayoutForRebuild(backgroundRect);

        if (_panelRect != null)
            LayoutRebuilder.MarkLayoutForRebuild(_panelRect);

        if (!Application.isPlaying)
        {
            if (backgroundRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRect);
            if (_panelRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
            Canvas.ForceUpdateCanvases();
        }
    }

    private static RectOffset CopyRectOffset(RectOffset source)
    {
        if (source == null)
            return new RectOffset();

        return new RectOffset(source.left, source.right, source.top, source.bottom);
    }

    private static void CopyRectOffset(RectOffset source, RectOffset target)
    {
        if (source == null || target == null)
            return;

        target.left = source.left;
        target.right = source.right;
        target.top = source.top;
        target.bottom = source.bottom;
    }

    private Image FindPanelBackgroundImage()
    {
        Transform searchRoot = _panelRect != null ? _panelRect : transform;
        if (searchRoot == null)
            return null;

        Image directImage = searchRoot.GetComponent<Image>();
        if (directImage != null && directImage != _iconImage)
            return directImage;

        ButtonTextAutoSize[] autoSizeDrivers = searchRoot.GetComponentsInChildren<ButtonTextAutoSize>(true);
        for (int i = 0; i < autoSizeDrivers.Length; i++)
        {
            ButtonTextAutoSize autoSizeDriver = autoSizeDrivers[i];
            if (autoSizeDriver == null)
                continue;

            Image image = autoSizeDriver.GetComponent<Image>();
            if (image != null && image != _iconImage && image.GetComponentInParent<Button>() == null)
                return image;
        }

        Image[] images = searchRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null ||
                image == _iconImage ||
                image.GetComponentInParent<Button>() != null)
            {
                continue;
            }

            return image;
        }

        return null;
    }

    private void EnsureReferences()
    {
        if (_rootObject == null)
            _rootObject = gameObject;
        if (_panelRect == null)
            _panelRect = transform as RectTransform;
        if (_messageText == null)
            _messageText = GetComponentInChildren<TMP_Text>(true);
        RemoveMessageTextLayoutElement();
        if (_iconImage == null)
            _iconImage = FindIconImage();
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    private void RemoveMessageTextLayoutElement()
    {
        if (_messageText == null)
            return;

        RemoveLayoutElementFromRect(_messageText.rectTransform);
    }

    private static void RemoveLayoutElementFromRect(RectTransform rect)
    {
        if (rect == null)
            return;

        LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
        if (layoutElement == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.DestroyObjectImmediate(layoutElement);
            return;
        }
#endif

        UnityEngine.Object.Destroy(layoutElement);
    }

    private void CaptureInitialIconSprite()
    {
        if (_initialIconSpriteCaptured || _iconImage == null)
            return;

        _initialIconSprite = _iconImage.sprite;
        _initialIconSpriteCaptured = true;
    }

    private void ApplyIconImageSettings(bool hasIcon, bool updateVisibility, bool applyPositionOffset)
    {
        if (_iconImage == null)
            return;

        bool keepSpace = _reserveIconSpaceWhenHidden && _applyIconImageSettings;
        bool keepActive = hasIcon || keepSpace || !_hideIconWhenMissing;

        if (updateVisibility)
        {
            _iconImage.enabled = hasIcon;
            _iconImage.gameObject.SetActive(keepActive);
        }

        if (!_applyIconImageSettings)
            return;

        RectTransform rect = _iconImage.rectTransform;
        if (rect != null)
        {
            if (_iconRect != rect)
            {
                _iconRect = rect;
                _iconCapturedAnchoredPosition = rect.anchoredPosition;
            }

            if (_iconWidth > 0f)
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _iconWidth);
            if (_iconHeight > 0f)
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _iconHeight);
            if (_applyIconVisualScale)
                rect.localScale = new Vector3(_iconVisualScale.x, _iconVisualScale.y, rect.localScale.z);

            if (applyPositionOffset)
                ApplyIconAnchoredOffset(rect);

            if (_applyIconStretchPadding)
                ApplyIconStretchPadding(rect);
        }

        _iconImage.preserveAspect = _preserveIconAspect;
        _iconImage.raycastTarget = _iconRaycastTarget;

        if (_forceIconImageType)
            _iconImage.type = _iconImageType;

        if (_overrideIconColor)
        {
            Color color = _iconColor;
            color.a *= _iconAlpha;
            _iconImage.color = color;
        }

        ApplyIconLayoutElement(keepActive);
        ApplyIconParentLayout();
        RebuildLayoutForImmediatePreview();

        if (applyPositionOffset && rect != null)
            ApplyIconAnchoredOffset(rect);
    }

    private void ApplyIconAnchoredOffset(RectTransform rect)
    {
        if (rect == null)
            return;

        Vector2 basePosition = _iconUseAbsoluteAnchoredOffset
            ? Vector2.zero
            : _iconCapturedAnchoredPosition;
        rect.anchoredPosition = basePosition + _iconAnchoredOffset;
    }

    private void RebuildLayoutForImmediatePreview()
    {
        if (Application.isPlaying)
            return;

        if (_iconImage != null)
        {
            RectTransform iconParent = _iconImage.transform.parent as RectTransform;
            if (iconParent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(iconParent);
        }

        if (_panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);

        Canvas.ForceUpdateCanvases();
    }

    private void ApplyIconStretchPadding(RectTransform rect)
    {
        if (rect == null)
            return;

        _iconStretchPadding ??= new RectOffset();
        rect.offsetMin = new Vector2(_iconStretchPadding.left, _iconStretchPadding.bottom);
        rect.offsetMax = new Vector2(-_iconStretchPadding.right, -_iconStretchPadding.top);
    }

    private void ApplyIconLayoutElement(bool reserveSpace)
    {
        if (!_driveIconLayoutElement || _iconImage == null)
            return;

        LayoutElement layoutElement = _iconImage.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = AddLayoutElement(_iconImage.gameObject);

        if (layoutElement == null)
            return;

        RectTransform rect = _iconImage.rectTransform;
        float rectWidth = rect != null ? Mathf.Max(0f, rect.rect.width) : 0f;
        float rectHeight = rect != null ? Mathf.Max(0f, rect.rect.height) : 0f;

        layoutElement.ignoreLayout = _ignoreIconInLayout || !reserveSpace;
        layoutElement.minWidth = ResolveLayoutDimension(_iconMinWidth, _iconWidth, rectWidth, reserveSpace);
        layoutElement.minHeight = ResolveLayoutDimension(_iconMinHeight, _iconHeight, rectHeight, reserveSpace);
        layoutElement.preferredWidth = ResolveLayoutDimension(_iconWidth, _iconMinWidth, rectWidth, reserveSpace);
        layoutElement.preferredHeight = ResolveLayoutDimension(_iconHeight, _iconMinHeight, rectHeight, reserveSpace);
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;
    }

    private static LayoutElement AddLayoutElement(GameObject target)
    {
        if (target == null)
            return null;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            return Undo.AddComponent<LayoutElement>(target);
#endif

        return target.AddComponent<LayoutElement>();
    }

    private float ResolveLayoutDimension(float preferredValue, float fallbackValue, float rectValue, bool reserveSpace)
    {
        if (!reserveSpace)
            return -1f;

        if (preferredValue > 0f)
            return preferredValue;

        if (fallbackValue > 0f)
            return fallbackValue;

        if (rectValue > 0f)
            return rectValue;

        return -1f;
    }

    private void ApplyIconParentLayout()
    {
        if (_iconImage == null || (!_applyParentLayoutSpacing && !_applyParentLayoutPadding))
            return;

        Transform parent = _iconImage.transform.parent;
        if (parent == null)
            return;

        HorizontalOrVerticalLayoutGroup layoutGroup = parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (layoutGroup == null)
            return;

        if (_applyParentLayoutSpacing)
            layoutGroup.spacing = _parentLayoutSpacing;

        if (_applyParentLayoutPadding)
        {
            _parentLayoutPadding ??= new RectOffset();
            layoutGroup.padding.left = _parentLayoutPadding.left;
            layoutGroup.padding.right = _parentLayoutPadding.right;
            layoutGroup.padding.top = _parentLayoutPadding.top;
            layoutGroup.padding.bottom = _parentLayoutPadding.bottom;
        }
    }

    private Image FindIconImage()
    {
        Transform searchRoot = _panelRect != null ? _panelRect : transform;
        if (searchRoot == null)
            return null;

        Image[] images = searchRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.GetComponent<ButtonTextAutoSize>() != null)
                continue;

            return image;
        }

        return null;
    }

    private void SetAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (_canvasGroup != null)
            _canvasGroup.alpha = alpha;
        else if (_messageText != null)
            _messageText.alpha = alpha;
    }

    private void KillSequence()
    {
        if (_sequence == null)
            return;

        _sequence.Kill();
        _sequence = null;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return "";
    }

    private readonly struct Request
    {
        public readonly string StatId;
        public readonly string DisplayName;
        public readonly int Delta;
        public readonly string Message;

        public Request(string statId, string displayName, int delta, string message)
        {
            StatId = statId ?? "";
            DisplayName = displayName ?? "";
            Delta = delta;
            Message = message ?? "";
        }
    }
}
