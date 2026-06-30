using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.VectorGraphics;
using UnityEngine.UI;

[System.Serializable]
public sealed class StoryNameExtraTextStyle
{
    [SerializeField] private bool _enabled = true;
    [SerializeField] private TMP_Text _targetText;
    [SerializeField] private string _targetPath;
    [SerializeField] private string _label;
    [TextArea]
    [SerializeField] private string _text;
    [SerializeField] private bool _overrideRect = true;
    [SerializeField] private Vector2 _anchoredPosition;
    [SerializeField] private Vector2 _sizeDelta = new Vector2(900f, 90f);
    [SerializeField] private bool _overrideHeightLimits;
    [SerializeField, Min(0f)] private float _minHeight;
    [SerializeField, Min(0f)] private float _maxHeight;
    [SerializeField] private bool _overrideColor;
    [SerializeField] private Color _color = Color.white;
    [SerializeField] private bool _overrideFont;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private bool _overrideFontSize;
    [SerializeField, Min(1f)] private float _fontSize = 48f;
    [SerializeField] private bool _overrideAutoSize;
    [SerializeField] private bool _autoSize;
    [SerializeField] private bool _overrideAutoFontSizeRange;
    [SerializeField, Min(1f)] private float _minAutoFontSize = 24f;
    [SerializeField, Min(1f)] private float _maxAutoFontSize = 72f;
    [SerializeField] private bool _overrideAlignment;
    [SerializeField] private TextAlignmentOptions _alignment = TextAlignmentOptions.Center;
    [SerializeField] private bool _overrideWordWrapping;
    [SerializeField] private bool _wordWrapping = true;
    [SerializeField] private bool _overrideOverflowMode;
    [SerializeField] private TextOverflowModes _overflowMode = TextOverflowModes.Overflow;
    [SerializeField] private bool _overrideLineSpacing;
    [SerializeField] private float _lineSpacing;
    [SerializeField] private bool _overrideMargins;
    [SerializeField] private Vector4 _margins;

    public bool Enabled => _enabled;
    public TMP_Text TargetText => _targetText;
    public string TargetPath => _targetPath;
    public string Label => _label;
    public string Text => _text;
    public bool OverrideRect => _overrideRect;
    public Vector2 AnchoredPosition => _anchoredPosition;
    public Vector2 SizeDelta => _sizeDelta;
    public bool OverrideHeightLimits => _overrideHeightLimits;
    public float MinHeight => _minHeight;
    public float MaxHeight => _maxHeight;
    public bool OverrideColor => _overrideColor;
    public Color Color => _color;
    public bool OverrideFont => _overrideFont;
    public TMP_FontAsset Font => _font;
    public bool OverrideFontSize => _overrideFontSize;
    public float FontSize => _fontSize;
    public bool OverrideAutoSize => _overrideAutoSize;
    public bool AutoSize => _autoSize;
    public bool OverrideAutoFontSizeRange => _overrideAutoFontSizeRange;
    public float MinAutoFontSize => _minAutoFontSize;
    public float MaxAutoFontSize => _maxAutoFontSize;
    public bool OverrideAlignment => _overrideAlignment;
    public TextAlignmentOptions Alignment => _alignment;
    public bool OverrideWordWrapping => _overrideWordWrapping;
    public bool WordWrapping => _wordWrapping;
    public bool OverrideOverflowMode => _overrideOverflowMode;
    public TextOverflowModes OverflowMode => _overflowMode;
    public bool OverrideLineSpacing => _overrideLineSpacing;
    public float LineSpacing => _lineSpacing;
    public bool OverrideMargins => _overrideMargins;
    public Vector4 Margins => _margins;

    public void Validate()
    {
        _minHeight = Mathf.Max(0f, _minHeight);
        _maxHeight = Mathf.Max(0f, _maxHeight);
        if (_overrideHeightLimits && _maxHeight > 0f && _maxHeight < _minHeight)
            _maxHeight = _minHeight;

        _fontSize = Mathf.Max(1f, _fontSize);
        _minAutoFontSize = Mathf.Max(1f, _minAutoFontSize);
        _maxAutoFontSize = Mathf.Max(_minAutoFontSize, _maxAutoFontSize);
    }
}

[System.Serializable]
public sealed class DialoguePanelExtraLayerStyle
{
    [SerializeField] private bool _enabled = true;
    [SerializeField] private string _targetPath;
    [SerializeField] private string _targetName;
    [SerializeField] private Sprite _sprite;
    [SerializeField] private UnityEngine.Object _spriteSource;
    [SerializeField] private bool _overrideColor = true;
    [SerializeField] private Color _color = Color.white;
    [SerializeField] private bool _overrideImageType = true;
    [SerializeField] private Image.Type _imageType = Image.Type.Sliced;
    [SerializeField] private bool _overrideRect = true;
    [SerializeField] private Vector2 _anchoredPosition;
    [SerializeField] private Vector2 _sizeDelta = new Vector2(1000f, 560f);
    [SerializeField] private bool _matchDialoguePanelAutoHeight = true;
    [SerializeField] private bool _overrideRaycastTarget = true;
    [SerializeField] private bool _raycastTarget;

    public bool Enabled => _enabled;
    public string TargetPath => _targetPath;
    public string TargetName => _targetName;
    public Sprite Sprite => _sprite;
    public UnityEngine.Object SpriteSource => _spriteSource;
    public bool OverrideColor => _overrideColor;
    public Color Color => _color;
    public bool OverrideImageType => _overrideImageType;
    public Image.Type ImageType => _imageType;
    public bool OverrideRect => _overrideRect;
    public Vector2 AnchoredPosition => _anchoredPosition;
    public Vector2 SizeDelta => _sizeDelta;
    public bool MatchDialoguePanelAutoHeight => _matchDialoguePanelAutoHeight;
    public bool OverrideRaycastTarget => _overrideRaycastTarget;
    public bool RaycastTarget => _raycastTarget;

    public void Validate()
    {
        _targetPath = (_targetPath ?? "").Trim();
        _targetName = (_targetName ?? "").Trim();
    }
}

[System.Serializable]
public sealed class StatPanelSizeOverride
{
    public string statId;
    public bool overridePanelSize = true;
    public Vector2 panelSizeDelta = new Vector2(1000f, 140f);

    public bool Matches(string value)
    {
        return overridePanelSize && StoryStatId.EqualsCanonical(statId, value);
    }

    public StatPanelSizeOverride Clone()
    {
        return new StatPanelSizeOverride
        {
            statId = statId,
            overridePanelSize = overridePanelSize,
            panelSizeDelta = panelSizeDelta
        };
    }

    public void Validate()
    {
        statId = string.IsNullOrWhiteSpace(statId) ? "" : statId.Trim();
    }

    static string Normalize(string value) => StoryStatId.Normalize(value);
}

[System.Serializable]
public sealed class StatTextRectOverride
{
    public string statId;
    public bool overrideTextRect = true;
    public Vector2 textAnchoredPosition;
    public Vector2 textSizeDelta = new Vector2(760f, 96f);

    public bool Matches(string value)
    {
        return overrideTextRect && StoryStatId.EqualsCanonical(statId, value);
    }

    public StatTextRectOverride Clone()
    {
        return new StatTextRectOverride
        {
            statId = statId,
            overrideTextRect = overrideTextRect,
            textAnchoredPosition = textAnchoredPosition,
            textSizeDelta = textSizeDelta
        };
    }

    public void Validate()
    {
        statId = string.IsNullOrWhiteSpace(statId) ? "" : statId.Trim();
    }

    static string Normalize(string value) => StoryStatId.Normalize(value);
}

[System.Serializable]
public sealed class StoryEndScreenTextStyle
{
    [SerializeField] private bool _overrideFont;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private bool _overrideFontSize;
    [SerializeField, Min(1f)] private float _fontSize = 48f;
    [SerializeField] private bool _overrideTextRect;
    [SerializeField] private Vector2 _anchoredPosition;
    [SerializeField] private Vector2 _sizeDelta;

    public bool OverrideFont => _overrideFont && _font != null;
    public TMP_FontAsset Font => _font;
    public bool OverrideFontSize => _overrideFontSize;
    public float FontSize => Mathf.Max(1f, _fontSize);
    public bool OverrideTextRect => _overrideTextRect;
    public Vector2 AnchoredPosition => _anchoredPosition;
    public Vector2 SizeDelta => _sizeDelta;
    public bool HasOverrides => OverrideFont || _overrideFontSize || _overrideTextRect;

    public void ApplyTo(TMP_Text target)
    {
        if (target == null)
            return;

        if (OverrideFont)
            target.font = _font;

        if (_overrideFontSize)
        {
            float size = FontSize;
            target.fontSize = size;
            if (target.enableAutoSizing && target.fontSizeMax > 0f)
                target.fontSizeMax = Mathf.Min(target.fontSizeMax, size);
        }

        if (_overrideTextRect && target.rectTransform != null)
        {
            target.rectTransform.anchoredPosition = _anchoredPosition;
            if (_sizeDelta.x > 0f || _sizeDelta.y > 0f)
                target.rectTransform.sizeDelta = _sizeDelta;
        }

        target.SetAllDirty();
        if (target.rectTransform != null)
            LayoutRebuilder.MarkLayoutForRebuild(target.rectTransform);
    }

    public void Validate()
    {
        _fontSize = Mathf.Max(1f, _fontSize);
    }
}

[System.Serializable]
public sealed class StoryEndScreenStatStyleBinding
{
    [SerializeField] private bool _enabled = true;
    [SerializeField] private string _label = "Стат";
    [SerializeField] private string _statId = "custom_stat";
    [SerializeField] private string[] _statAliases = System.Array.Empty<string>();
    [SerializeField] private StoryEndScreenStatValueMode _valueMode = StoryEndScreenStatValueMode.CurrentTotal;
    [SerializeField] private int _previewValue;
    [SerializeField] private bool _hideWhenZero;
    [SerializeField] private string _format = "{0}";

    [SerializeField] private Sprite _backgroundSprite;
    [SerializeField] private UnityEngine.Object _backgroundSpriteSource;
    [SerializeField] private Sprite _plateSprite;
    [SerializeField] private UnityEngine.Object _plateSpriteSource;
    [SerializeField] private Sprite _iconSprite;
    [SerializeField] private UnityEngine.Object _iconSpriteSource;
    [SerializeField] private bool _hideBackground;
    [SerializeField] private bool _hidePlate;
    [SerializeField] private bool _hideIcon;

    [SerializeField] private bool _overrideIconSize;
    [SerializeField] private Vector2 _iconSize = new Vector2(96f, 96f);
    [SerializeField] private bool _overrideRowPosition;
    [SerializeField] private Vector2 _rowAnchoredPosition;
    [SerializeField] private Vector2 _rowOffset;
    [SerializeField] private Vector2 _backgroundOffset;
    [SerializeField] private Vector2 _plateOffset;
    [SerializeField] private Vector2 _iconOffset;
    [SerializeField] private bool _overrideBackgroundRect;
    [SerializeField] private Vector2 _backgroundAnchoredPosition;
    [SerializeField] private Vector2 _backgroundSize;
    [SerializeField] private bool _overridePlateRect;
    [SerializeField] private Vector2 _plateAnchoredPosition;
    [SerializeField] private Vector2 _plateSize;
    [SerializeField] private bool _overrideIconRect;
    [SerializeField] private Vector2 _iconAnchoredPosition;
    [SerializeField] private bool _overrideRowSize;
    [SerializeField] private Vector2 _rowSize;
    [SerializeField] private bool _ignoreParentLayoutWhenPositioned = true;

    [SerializeField] private StoryEndScreenTextStyle _lineTextStyle = new StoryEndScreenTextStyle();
    [SerializeField] private StoryEndScreenTextStyle _labelTextStyle = new StoryEndScreenTextStyle();
    [SerializeField] private StoryEndScreenTextStyle _valueTextStyle = new StoryEndScreenTextStyle();

    public bool Enabled => _enabled;
    public string Label => _label;
    public string StatId => _statId;
    public string[] StatAliases => _statAliases ?? System.Array.Empty<string>();
    public StoryEndScreenStatValueMode ValueMode => _valueMode;
    public int PreviewValue => _previewValue;
    public bool HideWhenZero => _hideWhenZero;
    public string Format => string.IsNullOrWhiteSpace(_format) ? "{0}" : _format;
    public Sprite BackgroundSprite => _backgroundSprite;
    public UnityEngine.Object BackgroundSpriteSource => _backgroundSpriteSource;
    public Sprite PlateSprite => _plateSprite;
    public UnityEngine.Object PlateSpriteSource => _plateSpriteSource;
    public Sprite IconSprite => _iconSprite;
    public UnityEngine.Object IconSpriteSource => _iconSpriteSource;
    public bool HideBackground => _hideBackground;
    public bool HidePlate => _hidePlate;
    public bool HideIcon => _hideIcon;
    public bool OverrideIconSize => _overrideIconSize;
    public Vector2 IconSize => _iconSize;
    public bool OverrideRowPosition => _overrideRowPosition;
    public Vector2 RowAnchoredPosition => _rowAnchoredPosition;
    public Vector2 RowOffset => _rowOffset;
    public Vector2 BackgroundOffset => _backgroundOffset;
    public Vector2 PlateOffset => _plateOffset;
    public Vector2 IconOffset => _iconOffset;
    public bool OverrideBackgroundRect => _overrideBackgroundRect;
    public Vector2 BackgroundAnchoredPosition => _backgroundAnchoredPosition;
    public Vector2 BackgroundSize => _backgroundSize;
    public bool OverridePlateRect => _overridePlateRect;
    public Vector2 PlateAnchoredPosition => _plateAnchoredPosition;
    public Vector2 PlateSize => _plateSize;
    public bool OverrideIconRect => _overrideIconRect;
    public Vector2 IconAnchoredPosition => _iconAnchoredPosition;
    public bool OverrideRowSize => _overrideRowSize;
    public Vector2 RowSize => _rowSize;
    public bool IgnoreParentLayoutWhenPositioned => _ignoreParentLayoutWhenPositioned;
    public StoryEndScreenTextStyle LineTextStyle => _lineTextStyle;
    public StoryEndScreenTextStyle LabelTextStyle => _labelTextStyle;
    public StoryEndScreenTextStyle ValueTextStyle => _valueTextStyle;

    public IEnumerable<string> AllStatIds()
    {
        if (!string.IsNullOrWhiteSpace(_statId))
            yield return _statId.Trim();

        if (_statAliases == null)
            yield break;

        for (int i = 0; i < _statAliases.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(_statAliases[i]))
                yield return _statAliases[i].Trim();
        }
    }

    public bool Matches(StoryEndScreenStatBinding binding)
    {
        if (binding == null)
            return false;

        foreach (string id in AllStatIds())
        {
            foreach (string candidate in binding.AllStatIds())
            {
                if (StoryStatId.EqualsCanonical(id, candidate))
                    return true;
            }
        }

        return !string.IsNullOrWhiteSpace(_label) && binding.MatchesLabel(_label);
    }

    public bool Matches(StoryEndScreenStatValue stat)
    {
        if (stat == null)
            return false;

        foreach (string id in AllStatIds())
        {
            if (StoryStatId.EqualsCanonical(id, stat.StatId) ||
                StoryStatId.EqualsCanonical(id, stat.Label))
            {
                return true;
            }
        }

        return !string.IsNullOrWhiteSpace(_label) &&
            string.Equals(_label.Trim(), (stat.Label ?? "").Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    public void Validate()
    {
        _label = (_label ?? "").Trim();
        _statId = (_statId ?? "").Trim();
        _format = string.IsNullOrWhiteSpace(_format) ? "{0}" : _format.Trim();
        if (_statAliases == null)
            _statAliases = System.Array.Empty<string>();
        _iconSize = new Vector2(Mathf.Max(0f, _iconSize.x), Mathf.Max(0f, _iconSize.y));
        _backgroundSize = new Vector2(Mathf.Max(0f, _backgroundSize.x), Mathf.Max(0f, _backgroundSize.y));
        _plateSize = new Vector2(Mathf.Max(0f, _plateSize.x), Mathf.Max(0f, _plateSize.y));
        _lineTextStyle ??= new StoryEndScreenTextStyle();
        _labelTextStyle ??= new StoryEndScreenTextStyle();
        _valueTextStyle ??= new StoryEndScreenTextStyle();
        _lineTextStyle.Validate();
        _labelTextStyle.Validate();
        _valueTextStyle.Validate();
    }
}

[System.Serializable]
public sealed class StoryEndScreenStyleSettings
{
    [SerializeField] private Sprite _backgroundSprite;
    [SerializeField] private UnityEngine.Object _backgroundSpriteSource;
    [SerializeField] private Sprite _statsBackgroundSprite;
    [SerializeField] private UnityEngine.Object _statsBackgroundSpriteSource;
    [SerializeField] private Sprite _continueButtonPlateSprite;
    [SerializeField] private UnityEngine.Object _continueButtonPlateSpriteSource;
    [SerializeField] private StoryEndScreenTextStyle _titleTextStyle = new StoryEndScreenTextStyle();
    [SerializeField] private StoryEndScreenTextStyle _storyTitleTextStyle = new StoryEndScreenTextStyle();
    [SerializeField] private StoryEndScreenTextStyle _completedEpisodeTextStyle = new StoryEndScreenTextStyle();
    [SerializeField] private StoryEndScreenTextStyle _nextEpisodeTextStyle = new StoryEndScreenTextStyle();
    [SerializeField] private StoryEndScreenTextStyle _continueButtonTextStyle = new StoryEndScreenTextStyle();
    [SerializeField] private List<StoryEndScreenStatStyleBinding> _statBindings = new List<StoryEndScreenStatStyleBinding>();

    public Sprite BackgroundSprite => _backgroundSprite;
    public UnityEngine.Object BackgroundSpriteSource => _backgroundSpriteSource;
    public Sprite StatsBackgroundSprite => _statsBackgroundSprite;
    public UnityEngine.Object StatsBackgroundSpriteSource => _statsBackgroundSpriteSource;
    public Sprite ContinueButtonPlateSprite => _continueButtonPlateSprite;
    public UnityEngine.Object ContinueButtonPlateSpriteSource => _continueButtonPlateSpriteSource;
    public StoryEndScreenTextStyle TitleTextStyle => _titleTextStyle;
    public StoryEndScreenTextStyle StoryTitleTextStyle => _storyTitleTextStyle;
    public StoryEndScreenTextStyle CompletedEpisodeTextStyle => _completedEpisodeTextStyle;
    public StoryEndScreenTextStyle NextEpisodeTextStyle => _nextEpisodeTextStyle;
    public StoryEndScreenTextStyle ContinueButtonTextStyle => _continueButtonTextStyle;
    public IReadOnlyList<StoryEndScreenStatStyleBinding> StatBindings => _statBindings;
    public bool HasStatBindings => _statBindings != null && _statBindings.Count > 0;
    public bool HasOverrides =>
        _backgroundSprite != null ||
        _backgroundSpriteSource != null ||
        _statsBackgroundSprite != null ||
        _statsBackgroundSpriteSource != null ||
        _continueButtonPlateSprite != null ||
        _continueButtonPlateSpriteSource != null ||
        (_titleTextStyle != null && _titleTextStyle.HasOverrides) ||
        (_storyTitleTextStyle != null && _storyTitleTextStyle.HasOverrides) ||
        (_completedEpisodeTextStyle != null && _completedEpisodeTextStyle.HasOverrides) ||
        (_nextEpisodeTextStyle != null && _nextEpisodeTextStyle.HasOverrides) ||
        (_continueButtonTextStyle != null && _continueButtonTextStyle.HasOverrides) ||
        HasStatBindings;

    public StoryEndScreenStatStyleBinding FindStatStyle(StoryEndScreenStatValue stat)
    {
        if (_statBindings == null || stat == null)
            return null;

        for (int i = 0; i < _statBindings.Count; i++)
        {
            StoryEndScreenStatStyleBinding binding = _statBindings[i];
            if (binding != null && binding.Enabled && binding.Matches(stat))
                return binding;
        }

        return null;
    }

    public void Validate()
    {
        _titleTextStyle ??= new StoryEndScreenTextStyle();
        _storyTitleTextStyle ??= new StoryEndScreenTextStyle();
        _completedEpisodeTextStyle ??= new StoryEndScreenTextStyle();
        _nextEpisodeTextStyle ??= new StoryEndScreenTextStyle();
        _continueButtonTextStyle ??= new StoryEndScreenTextStyle();
        _titleTextStyle.Validate();
        _storyTitleTextStyle.Validate();
        _completedEpisodeTextStyle.Validate();
        _nextEpisodeTextStyle.Validate();
        _continueButtonTextStyle.Validate();

        if (_statBindings == null)
            _statBindings = new List<StoryEndScreenStatStyleBinding>();
        for (int i = 0; i < _statBindings.Count; i++)
            _statBindings[i]?.Validate();
    }
}

[CreateAssetMenu(fileName = "StoryUiStyle", menuName = "VN/UI/Story UI Style")]
public sealed class StoryUiStyle : ScriptableObject
{
    static Material _fallbackVectorUiMaterial;
    static Material _fallbackVectorGradientUiMaterial;

    [Header("Фон")]
    [Tooltip("Спрайт для Source Image у фона диалоговой плашки. Оставь пустым, если стиль должен сохранить дефолтный спрайт.")]
    [SerializeField] private bool _dialogueApplyOnlySprites;
    [SerializeField] private Sprite _backgroundSprite;
    [SerializeField] private UnityEngine.Object _backgroundSpriteSource;

    [Header("Dialogue background rect")]
    [SerializeField] private bool _overrideDialogueBackgroundAnchors;
    [SerializeField] private Vector2 _dialogueBackgroundAnchorMin = Vector2.zero;
    [SerializeField] private Vector2 _dialogueBackgroundAnchorMax = Vector2.one;
    [SerializeField] private bool _overrideDialogueBackgroundPivot;
    [SerializeField] private Vector2 _dialogueBackgroundPivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private bool _overrideDialogueBackgroundRect;
    [SerializeField] private Vector2 _dialogueBackgroundAnchoredPosition;
    [SerializeField] private Vector2 _dialogueBackgroundSizeDelta;
    [SerializeField] private bool _overrideDialogueBackgroundStretchOffsets;
    [SerializeField] private Vector4 _dialogueBackgroundStretchOffsets;

    [Header("Положение диалоговой плашки")]
    [Tooltip("Включи, если этой истории нужна своя позиция или размер самой диалоговой плашки.")]
    [SerializeField] private bool _overrideDialoguePanelRect;
    [Tooltip("Anchored Position диалоговой плашки только для этой истории.")]
    [SerializeField] private Vector2 _dialoguePanelAnchoredPosition;
    [Tooltip("Size Delta диалоговой плашки только для этой истории.")]
    [SerializeField] private Vector2 _dialoguePanelSizeDelta;

    [Header("Dialogue Panel: auto height")]
    [SerializeField] private bool _overrideDialoguePanelAutoHeight;
    [SerializeField] private bool _dialoguePanelAutoHeight;
    [SerializeField] private float _dialoguePanelAutoHeightPadding = 72f;
    [SerializeField, Min(0f)] private float _dialoguePanelAutoMinHeight;
    [SerializeField, Min(0f)] private float _dialoguePanelAutoMaxHeight;
    [SerializeField] private bool _dialoguePanelAutoHeightKeepTop = true;
    [Tooltip("Насколько поднимать DialoguePanel при росте высоты: 0 = не поднимать, 1 = нижняя кромка остаётся на месте, 0.5 = половина прироста.")]
    [SerializeField] private float _dialoguePanelAutoHeightGrowthUpFactor;

    [Header("Dialogue Panel: vertical layout")]
    [SerializeField] private bool _overrideDialoguePanelVerticalLayout;
    [SerializeField] private RectOffset _dialoguePanelVerticalLayoutPadding = new RectOffset();
    [SerializeField] private float _dialoguePanelVerticalLayoutSpacing;
    [SerializeField] private TextAnchor _dialoguePanelVerticalLayoutChildAlignment = TextAnchor.UpperLeft;
    [SerializeField] private bool _dialoguePanelVerticalLayoutReverseArrangement;
    [SerializeField] private bool _dialoguePanelVerticalLayoutControlChildWidth = true;
    [SerializeField] private bool _dialoguePanelVerticalLayoutControlChildHeight = true;
    [SerializeField] private bool _dialoguePanelVerticalLayoutUseChildScaleWidth;
    [SerializeField] private bool _dialoguePanelVerticalLayoutUseChildScaleHeight;
    [SerializeField] private bool _dialoguePanelVerticalLayoutChildForceExpandWidth;
    [SerializeField] private bool _dialoguePanelVerticalLayoutChildForceExpandHeight = true;
    [SerializeField] private bool _overrideDialoguePanelContentSizeFitter;
    [SerializeField] private ContentSizeFitter.FitMode _dialoguePanelContentSizeFitterHorizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    [SerializeField] private ContentSizeFitter.FitMode _dialoguePanelContentSizeFitterVerticalFit = ContentSizeFitter.FitMode.PreferredSize;

    [Header("Отступ текста диалога")]
    [Tooltip("Включи, если в этой истории нужно поднять или опустить основной текст внутри диалоговой плашки.")]
    [SerializeField] private bool _overrideBodyTextOffsetY;
    [Tooltip("Дополнительный Y-offset для основного текста диалога. Плюс двигает текст вверх, минус двигает вниз.")]
    [SerializeField] private float _bodyTextOffsetY;
    [Tooltip("Включи, если этой истории нужен свой Top Offset Y у Grow Down RectTransform Lock на BodyText.")]
    [SerializeField] private bool _overrideBodyTextTopOffsetY;
    [Tooltip("Значение Top Offset Y для Grow Down RectTransform Lock на BodyText. Используй его, когда нужно подогнать верхнюю границу текста под плашку конкретной истории.")]
    [SerializeField] private float _bodyTextTopOffsetY;
    [SerializeField] private bool _overrideBodyTextGrowDownOffsetX;
    [SerializeField] private float _bodyTextGrowDownOffsetX;
    [SerializeField] private bool _overrideBodyTextResizeHeightToPreferredText;
    [SerializeField] private bool _bodyTextResizeHeightToPreferredText = true;
    [SerializeField] private bool _overrideBodyTextExtraHeight;
    [SerializeField] private float _bodyTextExtraHeight;
    [SerializeField] private bool _overrideBodyTextMinHeight;
    [SerializeField, Min(0f)] private float _bodyTextMinHeight;
    [SerializeField] private bool _overrideBodyTextMaxHeight;
    [SerializeField, Min(0f)] private float _bodyTextMaxHeight;
    [SerializeField] private bool _overrideBodyTextMaxFontSize;
    [SerializeField, Min(0f)] private float _bodyTextMaxFontSize;
    [SerializeField] private bool _overrideBodyTextFont;
    [SerializeField] private TMP_FontAsset _bodyTextFont;
    [SerializeField] private bool _overrideBodyTextShrinkTextToFitRect;
    [SerializeField] private bool _bodyTextShrinkTextToFitRect = true;
    [SerializeField] private bool _overrideBodyTextMinAutoFontSize;
    [SerializeField, Min(1f)] private float _bodyTextMinAutoFontSize = 18f;
    [SerializeField] private bool _overrideBodyTextOverflowModeWhenStillTooLarge;
    [SerializeField] private TextOverflowModes _bodyTextOverflowModeWhenStillTooLarge = TextOverflowModes.Ellipsis;
    [SerializeField] private bool _overrideBodyTextHorizontalClamp;
    [SerializeField] private bool _bodyTextHorizontalClamp = true;
    [SerializeField] private float _bodyTextHorizontalInset = 48f;
    [SerializeField] private float _bodyTextMaxWidth;

    [Header("Dialogue Panel: extra layers")]
    [SerializeField] private List<DialoguePanelExtraLayerStyle> _dialogueExtraLayers = new List<DialoguePanelExtraLayerStyle>();

    [Header("Отступ имени персонажа")]
    [Tooltip("Включи, если имя персонажа в диалоговой плашке этой истории нужно сдвинуть отдельно от общей сцены.")]
    [SerializeField] private bool _overrideCharacterNameOffset;
    [Tooltip("X/Y offset для CharacterName. X двигает вправо/влево, Y двигает вверх/вниз относительно базовой позиции в сцене.")]
    [SerializeField] private Vector2 _characterNameOffset;
    [SerializeField] private bool _overrideCharacterNameFont;
    [SerializeField] private TMP_FontAsset _characterNameFont;
    [SerializeField] private bool _overrideCharacterNameFontSize;
    [SerializeField, Min(1f)] private float _characterNameFontSize = 42f;

    [Header("Dialogue NamePlate")]
    [SerializeField] private Sprite _namePlateSprite;
    [SerializeField] private UnityEngine.Object _namePlateSpriteSource;
    [SerializeField] private bool _overrideNamePlateColor;
    [SerializeField] private Color _namePlateColor = Color.white;
    [SerializeField] private bool _overrideNamePlateImageType;
    [SerializeField] private Image.Type _namePlateImageType = Image.Type.Sliced;
    [SerializeField] private bool _overrideNamePlatePreserveAspect;
    [SerializeField] private bool _namePlatePreserveAspect;
    [SerializeField] private bool _overrideNamePlatePixelsPerUnitMultiplier;
    [SerializeField, Min(0.01f)] private float _namePlatePixelsPerUnitMultiplier = 1f;
    [SerializeField] private bool _overrideNamePlateMaterial;
    [SerializeField] private Material _namePlateMaterial;
    [SerializeField] private bool _overrideNamePlateRaycastTarget;
    [SerializeField] private bool _namePlateRaycastTarget;
    [SerializeField] private bool _overrideNamePlateAnchors;
    [SerializeField] private Vector2 _namePlateAnchorMin;
    [SerializeField] private Vector2 _namePlateAnchorMax = Vector2.one;
    [SerializeField] private bool _overrideNamePlatePivot;
    [SerializeField] private Vector2 _namePlatePivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private bool _overrideNamePlateRect;
    [SerializeField] private Vector2 _namePlateAnchoredPosition;
    [SerializeField] private Vector2 _namePlateSizeDelta = new Vector2(420f, 96f);

    [Header("Экран ввода имени")]
    [SerializeField] private bool _nameInputApplyOnlySprites;
    [SerializeField] private Sprite _nameScreenBackgroundSprite;
    [SerializeField] private UnityEngine.Object _nameScreenBackgroundSpriteSource;
    [SerializeField] private bool _overrideNameScreenBackgroundColor;
    [SerializeField] private Color _nameScreenBackgroundColor = Color.white;
    [SerializeField] private bool _overrideNameScreenBackgroundImageType;
    [SerializeField] private Image.Type _nameScreenBackgroundImageType = Image.Type.Simple;

    [SerializeField] private Sprite _namePanelBackgroundSprite;
    [SerializeField] private UnityEngine.Object _namePanelBackgroundSpriteSource;
    [SerializeField] private bool _overrideNamePanelBackgroundColor;
    [SerializeField] private Color _namePanelBackgroundColor = Color.white;
    [SerializeField] private bool _overrideNamePanelBackgroundImageType;
    [SerializeField] private Image.Type _namePanelBackgroundImageType = Image.Type.Simple;
    [SerializeField] private bool _overrideNamePanelBackgroundRect;
    [SerializeField] private Vector2 _namePanelBackgroundAnchoredPosition;
    [SerializeField] private Vector2 _namePanelBackgroundSizeDelta = new Vector2(1251.2f, 1259.2f);

    [SerializeField] private Sprite _nameInputFieldSprite;
    [SerializeField] private UnityEngine.Object _nameInputFieldSpriteSource;
    [SerializeField] private bool _overrideNameInputFieldColor;
    [SerializeField] private Color _nameInputFieldColor = Color.white;
    [SerializeField] private bool _overrideNameInputFieldImageType;
    [SerializeField] private Image.Type _nameInputFieldImageType = Image.Type.Sliced;
    [SerializeField] private bool _overrideNameInputFieldRect;
    [SerializeField] private Vector2 _nameInputFieldAnchoredPosition;
    [SerializeField] private Vector2 _nameInputFieldSizeDelta = new Vector2(760f, 132f);

    [SerializeField] private bool _overrideNameInputTextRect;
    [SerializeField] private Vector2 _nameInputTextAnchoredPosition;
    [SerializeField] private Vector2 _nameInputTextSizeDelta = new Vector2(0f, 72f);
    [SerializeField] private bool _overrideNameInputTextColor;
    [SerializeField] private Color _nameInputTextColor = Color.white;
    [SerializeField] private bool _overrideNameInputTextFont;
    [SerializeField] private TMP_FontAsset _nameInputTextFont;
    [SerializeField] private bool _overrideNameInputTextFontSize;
    [SerializeField, Min(1f)] private float _nameInputTextFontSize = 72f;

    [SerializeField] private bool _overrideNamePlaceholderTextRect;
    [SerializeField] private Vector2 _namePlaceholderTextAnchoredPosition;
    [SerializeField] private Vector2 _namePlaceholderTextSizeDelta = new Vector2(900f, 110f);
    [SerializeField] private bool _overrideNamePlaceholderTextColor;
    [SerializeField] private Color _namePlaceholderTextColor = Color.white;
    [SerializeField] private bool _overrideNamePlaceholderTextFont;
    [SerializeField] private TMP_FontAsset _namePlaceholderTextFont;
    [SerializeField] private bool _overrideNamePlaceholderTextFontSize;
    [SerializeField, Min(1f)] private float _namePlaceholderTextFontSize = 57f;

    [SerializeField] private GameObject _nameConfirmButtonPrefabOverride;
    [SerializeField] private Sprite _nameConfirmButtonSprite;
    [SerializeField] private UnityEngine.Object _nameConfirmButtonSpriteSource;
    [SerializeField] private bool _overrideNameConfirmButtonColor;
    [SerializeField] private Color _nameConfirmButtonColor = Color.white;
    [SerializeField] private bool _overrideNameConfirmButtonImageType;
    [SerializeField] private Image.Type _nameConfirmButtonImageType = Image.Type.Simple;
    [SerializeField] private bool _overrideNameConfirmButtonRect;
    [SerializeField] private Vector2 _nameConfirmButtonAnchoredPosition;
    [SerializeField] private Vector2 _nameConfirmButtonSizeDelta = new Vector2(578f, 177f);
    [SerializeField] private bool _overrideNameConfirmButtonTextRect;
    [SerializeField] private Vector2 _nameConfirmButtonTextAnchoredPosition;
    [SerializeField] private Vector2 _nameConfirmButtonTextSizeDelta = new Vector2(578f, 177f);
    [SerializeField] private bool _overrideNameConfirmButtonTextColor;
    [SerializeField] private Color _nameConfirmButtonTextColor = Color.white;
    [SerializeField] private bool _overrideNameConfirmButtonTextFont;
    [SerializeField] private TMP_FontAsset _nameConfirmButtonTextFont;
    [SerializeField] private bool _overrideNameConfirmButtonTextFontSize;
    [SerializeField, Min(1f)] private float _nameConfirmButtonTextFontSize = 57f;

    [Header("Экран ввода имени: дополнительные тексты")]
    [SerializeField] private bool _useNameExtraTextOne;
    [TextArea]
    [SerializeField] private string _nameExtraTextOneText;
    [SerializeField] private bool _overrideNameExtraTextOneRect;
    [SerializeField] private Vector2 _nameExtraTextOneAnchoredPosition = new Vector2(0f, 210f);
    [SerializeField] private Vector2 _nameExtraTextOneSizeDelta = new Vector2(900f, 90f);
    [SerializeField] private bool _overrideNameExtraTextOneColor;
    [SerializeField] private Color _nameExtraTextOneColor = Color.white;
    [SerializeField] private bool _overrideNameExtraTextOneFont;
    [SerializeField] private TMP_FontAsset _nameExtraTextOneFont;
    [SerializeField] private bool _overrideNameExtraTextOneFontSize;
    [SerializeField, Min(1f)] private float _nameExtraTextOneFontSize = 48f;

    [SerializeField] private bool _useNameExtraTextTwo;
    [TextArea]
    [SerializeField] private string _nameExtraTextTwoText;
    [SerializeField] private bool _overrideNameExtraTextTwoRect;
    [SerializeField] private Vector2 _nameExtraTextTwoAnchoredPosition = new Vector2(0f, -210f);
    [SerializeField] private Vector2 _nameExtraTextTwoSizeDelta = new Vector2(900f, 90f);
    [SerializeField] private bool _overrideNameExtraTextTwoColor;
    [SerializeField] private Color _nameExtraTextTwoColor = Color.white;
    [SerializeField] private bool _overrideNameExtraTextTwoFont;
    [SerializeField] private TMP_FontAsset _nameExtraTextTwoFont;
    [SerializeField] private bool _overrideNameExtraTextTwoFontSize;
    [SerializeField, Min(1f)] private float _nameExtraTextTwoFontSize = 48f;
    [SerializeField] private List<StoryNameExtraTextStyle> _nameExtraTexts = new List<StoryNameExtraTextStyle>();

    [Header("Кнопки выбора")]
    [Tooltip("Необязательный prefab кнопки выбора. Используй его, если истории нужен полностью отдельный вид кнопок.")]
    [SerializeField] private bool _choicesApplyOnlySprites;
    [SerializeField] private GameObject _choiceButtonPrefabOverride;
    [Tooltip("Префаб для платных вариантов выбора в этой истории. Если пусто, используется сценовый premium prefab или обычный prefab выбора.")]
    [SerializeField] private GameObject _premiumChoiceButtonPrefabOverride;
    [Tooltip("Prefab панели баланса над платными выборами в этой истории. На prefab добавьте PremiumChoiceBalancePanelView и назначьте TMP-текст баланса.")]
    [SerializeField] private GameObject _premiumChoiceBalancePanelPrefabOverride;
    [Tooltip("Сдвиг prefab-панели баланса платного выбора относительно позиции, сохранённой в prefab.")]
    [SerializeField] private Vector2 _premiumChoiceBalancePanelOffset;
    [SerializeField] private Sprite _choiceButtonSprite;
    [SerializeField] private UnityEngine.Object _choiceButtonSpriteSource;
    [SerializeField] private bool _overrideChoiceButtonColor;
    [SerializeField] private Color _choiceButtonColor = Color.white;
    [SerializeField] private bool _overrideChoiceButtonImageType;
    [SerializeField] private Image.Type _choiceButtonImageType = Image.Type.Sliced;
    [SerializeField] private bool _overrideChoiceButtonTextColor;
    [SerializeField] private Color _choiceButtonTextColor = Color.white;
    [SerializeField] private bool _overrideChoiceButtonFont;
    [SerializeField] private TMP_FontAsset _choiceButtonFont;
    [SerializeField] private bool _overrideChoiceButtonFontSize;
    [SerializeField, Min(1f)] private float _choiceButtonFontSize = 36f;
    [SerializeField] private bool _overrideChoiceButtonPadding;
    [SerializeField] private Vector2 _choiceButtonPadding = new Vector2(96f, 44f);
    [SerializeField] private bool _overrideChoiceButtonTextPadding;
    [SerializeField] private RectOffset _choiceButtonTextPadding;
    [SerializeField] private bool _overrideChoiceButtonTextOffset;
    [SerializeField] private Vector2 _choiceButtonTextOffset = new Vector2(0f, -4f);

    [Header("Фон выборов")]
    [SerializeField] private Sprite _choicePanelSprite;
    [SerializeField] private UnityEngine.Object _choicePanelSpriteSource;
    [SerializeField] private bool _overrideChoicePanelColor;
    [SerializeField] private Color _choicePanelColor = Color.white;
    [SerializeField] private bool _overrideChoicePanelImageType;
    [SerializeField] private Image.Type _choicePanelImageType = Image.Type.Sliced;

    [Header("Плашка статов")]
    [SerializeField] private bool _statsApplyOnlySprites;
    [SerializeField] private Sprite _statPanelSprite;
    [SerializeField] private UnityEngine.Object _statPanelSpriteSource;
    [SerializeField] private bool _overrideStatPanelColor;
    [SerializeField] private Color _statPanelColor = Color.white;
    [SerializeField] private bool _overrideStatPanelImageType;
    [SerializeField] private Image.Type _statPanelImageType = Image.Type.Sliced;
    [SerializeField] private bool _overrideStatPanelBackgroundAnchors;
    [SerializeField] private Vector2 _statPanelBackgroundAnchorMin = Vector2.zero;
    [SerializeField] private Vector2 _statPanelBackgroundAnchorMax = Vector2.one;
    [SerializeField] private bool _overrideStatPanelBackgroundPivot;
    [SerializeField] private Vector2 _statPanelBackgroundPivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private bool _overrideStatPanelBackgroundStretchOffsets;
    [SerializeField] private Vector4 _statPanelBackgroundStretchOffsets;
    [SerializeField] private bool _overrideStatTextColor;
    [SerializeField] private Color _statTextColor = Color.white;
    [SerializeField] private bool _overrideStatTextFont;
    [SerializeField] private TMP_FontAsset _statTextFont;
    [SerializeField] private bool _overrideStatTextFontSize;
    [SerializeField, Min(1f)] private float _statTextFontSize = 42f;
    [SerializeField] private bool _overrideStatPanelRect;
    [SerializeField] private Vector2 _statPanelAnchoredPosition;
    [SerializeField] private Vector2 _statPanelSizeDelta = new Vector2(1000f, 140f);
    [SerializeField] private List<StatPanelSizeOverride> _statPanelSizeOverrides = new List<StatPanelSizeOverride>();
    [SerializeField] private bool _overrideStatTextRect;
    [SerializeField] private Vector2 _statTextAnchoredPosition;
    [SerializeField] private Vector2 _statTextSizeDelta = new Vector2(760f, 96f);
    [SerializeField] private List<StatTextRectOverride> _statTextRectOverrides = new List<StatTextRectOverride>();
    [SerializeField] private bool _overrideStatTextAutoSize;
    [SerializeField] private bool _statTextAutoSize;
    [SerializeField] private bool _overrideStatTextAutoFontSizeRange;
    [SerializeField, Min(1f)] private float _statTextMinAutoFontSize = 28f;
    [SerializeField, Min(1f)] private float _statTextMaxAutoFontSize = 54f;
    [SerializeField] private bool _overrideStatTextAlignment;
    [SerializeField] private TextAlignmentOptions _statTextAlignment = TextAlignmentOptions.Center;
    [SerializeField] private bool _overrideStatTextWordWrapping;
    [SerializeField] private bool _statTextWordWrapping;
    [SerializeField] private bool _overrideStatTextOverflowMode;
    [SerializeField] private TextOverflowModes _statTextOverflowMode = TextOverflowModes.Overflow;
    [SerializeField] private bool _overrideStatTextLineSpacing;
    [SerializeField] private float _statTextLineSpacing;
    [SerializeField] private bool _overrideStatTextMargins;
    [SerializeField] private Vector4 _statTextMargins;
    [Tooltip("Если включено, плашка статов использует только списки ниже и не берёт дефолтные статы ZLS.")]
    [SerializeField] private bool _replaceStatDefinitions;
    [SerializeField] private List<StatChangeOverlayDefinition> _statOverlayDefinitions = new List<StatChangeOverlayDefinition>();
    [SerializeField] private List<StatDefinition> _statDefinitionAssets = new List<StatDefinition>();

    [Header("Layout статов")]
    [SerializeField] private bool _overrideStatPanelPadding;
    [SerializeField] private Vector2 _statPanelPadding = new Vector2(640f, 96f);
    [SerializeField] private bool _overrideStatIconSize;
    [SerializeField] private Vector2 _statIconSize;
    [SerializeField] private bool _overrideStatIconOffset;
    [SerializeField] private Vector2 _statIconOffset;
    [SerializeField] private bool _overrideStatIconVisualScale;
    [SerializeField] private Vector2 _statIconVisualScale = Vector2.one;
    [SerializeField] private bool _overrideStatIconMinSize;
    [SerializeField] private Vector2 _statIconMinSize;
    [SerializeField] private bool _overrideStatIconReserveSpaceWhenHidden;
    [SerializeField] private bool _statIconReserveSpaceWhenHidden;
    [SerializeField] private bool _overrideStatIconParentSpacing;
    [SerializeField] private float _statIconParentSpacing;
    [SerializeField] private bool _overrideStatIconParentPadding;
    [SerializeField] private RectOffset _statIconParentPadding;
    [SerializeField] private List<StatIconOffsetOverride> _statIconOffsetOverrides = new List<StatIconOffsetOverride>();

    [Header("Stats Panel: vertical layout")]
    [SerializeField] private bool _overrideStatPanelVerticalLayout;
    [SerializeField] private RectOffset _statPanelVerticalLayoutPadding = new RectOffset();
    [SerializeField] private float _statPanelVerticalLayoutSpacing;
    [SerializeField] private TextAnchor _statPanelVerticalLayoutChildAlignment = TextAnchor.UpperLeft;
    [SerializeField] private bool _statPanelVerticalLayoutReverseArrangement;
    [SerializeField] private bool _statPanelVerticalLayoutControlChildWidth = true;
    [SerializeField] private bool _statPanelVerticalLayoutControlChildHeight = true;
    [SerializeField] private bool _statPanelVerticalLayoutUseChildScaleWidth;
    [SerializeField] private bool _statPanelVerticalLayoutUseChildScaleHeight;
    [SerializeField] private bool _statPanelVerticalLayoutChildForceExpandWidth;
    [SerializeField] private bool _statPanelVerticalLayoutChildForceExpandHeight = true;
    [SerializeField] private bool _overrideStatPanelContentSizeFitter;
    [SerializeField] private ContentSizeFitter.FitMode _statPanelContentSizeFitterHorizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    [SerializeField] private ContentSizeFitter.FitMode _statPanelContentSizeFitterVerticalFit = ContentSizeFitter.FitMode.PreferredSize;

    [Header("Отношения")]
    [SerializeField] private bool _overrideRelationshipFrameSize;
    [SerializeField] private Vector2 _relationshipFrameAnchoredPosition;
    [SerializeField] private Vector2 _relationshipFrameSize = new Vector2(1320f, 175f);
    [SerializeField] private bool _overrideRelationshipPanelBackgroundAnchors;
    [SerializeField] private Vector2 _relationshipPanelBackgroundAnchorMin = Vector2.zero;
    [SerializeField] private Vector2 _relationshipPanelBackgroundAnchorMax = Vector2.one;
    [SerializeField] private bool _overrideRelationshipPanelBackgroundPivot;
    [SerializeField] private Vector2 _relationshipPanelBackgroundPivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private bool _overrideRelationshipPanelBackgroundRect;
    [SerializeField] private Vector2 _relationshipPanelBackgroundAnchoredPosition;
    [SerializeField] private Vector2 _relationshipPanelBackgroundSizeDelta;
    [SerializeField] private bool _overrideRelationshipPanelBackgroundStretchOffsets;
    [SerializeField] private Vector4 _relationshipPanelBackgroundStretchOffsets;
    [SerializeField] private bool _overrideRelationshipPanelVerticalLayout;
    [SerializeField] private RectOffset _relationshipPanelVerticalLayoutPadding = new RectOffset();
    [SerializeField] private float _relationshipPanelVerticalLayoutSpacing;
    [SerializeField] private TextAnchor _relationshipPanelVerticalLayoutChildAlignment = TextAnchor.UpperLeft;
    [SerializeField] private bool _relationshipPanelVerticalLayoutReverseArrangement;
    [SerializeField] private bool _relationshipPanelVerticalLayoutControlChildWidth = true;
    [SerializeField] private bool _relationshipPanelVerticalLayoutControlChildHeight = true;
    [SerializeField] private bool _relationshipPanelVerticalLayoutUseChildScaleWidth;
    [SerializeField] private bool _relationshipPanelVerticalLayoutUseChildScaleHeight;
    [SerializeField] private bool _relationshipPanelVerticalLayoutChildForceExpandWidth;
    [SerializeField] private bool _relationshipPanelVerticalLayoutChildForceExpandHeight = true;
    [SerializeField] private bool _overrideRelationshipPanelContentSizeFitter;
    [SerializeField] private ContentSizeFitter.FitMode _relationshipPanelContentSizeFitterHorizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    [SerializeField] private ContentSizeFitter.FitMode _relationshipPanelContentSizeFitterVerticalFit = ContentSizeFitter.FitMode.PreferredSize;
    [SerializeField] private bool _overrideRelationshipFontSizeRange;
    [SerializeField, Min(1f)] private float _relationshipFontSizeMin = 42f;
    [SerializeField, Min(1f)] private float _relationshipFontSizeMax = 54f;
    [SerializeField] private bool _overrideRelationshipMaxVisibleLines;
    [SerializeField, Min(1)] private int _relationshipMaxVisibleLines = 3;
    [SerializeField] private List<RelationshipMessageOverride> _relationshipMessageOverrides = new List<RelationshipMessageOverride>();

    [Header("Заголовок главы")]
    [SerializeField] private bool _chapterApplyOnlySprites;
    [SerializeField] private Sprite _chapterTitlePanelSprite;
    [SerializeField] private UnityEngine.Object _chapterTitlePanelSpriteSource;
    [SerializeField] private bool _overrideChapterTitlePanelColor;
    [SerializeField] private Color _chapterTitlePanelColor = Color.white;
    [SerializeField] private bool _overrideChapterTitlePanelImageType;
    [SerializeField] private Image.Type _chapterTitlePanelImageType = Image.Type.Sliced;
    [SerializeField] private bool _overrideChapterTitleTextColor;
    [SerializeField] private Color _chapterTitleTextColor = Color.white;
    [SerializeField] private bool _overrideChapterTitleTextFont;
    [SerializeField] private TMP_FontAsset _chapterTitleTextFont;
    [SerializeField] private bool _overrideChapterTitleTextFontSize;
    [SerializeField, Min(1f)] private float _chapterTitleTextFontSize = 72f;
    [SerializeField] private bool _overrideChapterTitleTextRect;
    [SerializeField] private Vector2 _chapterTitleTextAnchoredPosition;
    [SerializeField] private Vector2 _chapterTitleTextSizeDelta = new Vector2(900f, 120f);
    [SerializeField] private bool _overrideChapterTitleTextHeightLimits;
    [SerializeField, Min(0f)] private float _chapterTitleTextMinHeight;
    [SerializeField, Min(0f)] private float _chapterTitleTextMaxHeight;
    [SerializeField] private bool _overrideChapterTitleTextAutoSize;
    [SerializeField] private bool _chapterTitleTextAutoSize;
    [SerializeField] private bool _overrideChapterTitleTextAutoFontSizeRange;
    [SerializeField, Min(1f)] private float _chapterTitleTextMinAutoFontSize = 36f;
    [SerializeField, Min(1f)] private float _chapterTitleTextMaxAutoFontSize = 72f;
    [SerializeField] private bool _overrideChapterTitleTextAlignment;
    [SerializeField] private TextAlignmentOptions _chapterTitleTextAlignment = TextAlignmentOptions.Center;
    [SerializeField] private bool _overrideChapterTitleTextWordWrapping;
    [SerializeField] private bool _chapterTitleTextWordWrapping = true;
    [SerializeField] private bool _overrideChapterTitleTextOverflowMode;
    [SerializeField] private TextOverflowModes _chapterTitleTextOverflowMode = TextOverflowModes.Overflow;
    [SerializeField] private bool _overrideChapterTitleTextLineSpacing;
    [SerializeField] private float _chapterTitleTextLineSpacing;
    [SerializeField] private bool _overrideChapterTitleTextMargins;
    [SerializeField] private Vector4 _chapterTitleTextMargins;

    [Header("Заголовок главы: положение и затемнение")]
    [SerializeField] private bool _overrideChapterTitleCenterOnShow;
    [SerializeField] private bool _chapterTitleCenterOnShow = true;
    [SerializeField] private bool _overrideChapterTitleBringToFrontOnShow;
    [SerializeField] private bool _chapterTitleBringToFrontOnShow = true;
    [SerializeField] private bool _overrideChapterTitleBackgroundDimSizeMode;
    [SerializeField] private ChapterTitleBackdropSizeMode _chapterTitleBackgroundDimSizeMode = ChapterTitleBackdropSizeMode.FixedSize;
    [SerializeField] private bool _overrideChapterTitleBackgroundDimFixedSize;
    [SerializeField] private Vector2 _chapterTitleBackgroundDimFixedSize = new Vector2(5000f, 5000f);
    [SerializeField] private bool _overrideChapterTitleBackgroundDimColor;
    [SerializeField] private Color _chapterTitleBackgroundDimColor = Color.black;
    [SerializeField] private bool _overrideChapterTitleBackgroundDimAlpha;
    [SerializeField, Range(0f, 1f)] private float _chapterTitleBackgroundDimAlpha = 0.6f;

    [Header("Заголовок главы: текст")]
    [SerializeField] private bool _overrideChapterTitleTextMode;
    [SerializeField] private ChapterTitleTextMode _chapterTitleTextMode = ChapterTitleTextMode.Auto;
    [SerializeField] private bool _overrideChapterTitleTextFormat;
    [SerializeField] private string _chapterTitleTextFormat = "{1}";
    [SerializeField] private bool _overrideChapterTitleNumberAndTitleFormat;
    [SerializeField] private string _chapterTitleNumberAndTitleFormat = "ГЛАВА {0}: {1}";
    [SerializeField] private bool _overrideChapterTitleNumberOffset;
    [SerializeField] private int _chapterTitleNumberOffset = 1;
    [SerializeField] private bool _overrideChapterTitleEmptyTitleFallback;
    [SerializeField] private string _chapterTitleEmptyTitleFallback = "ГЛАВА {0}";
    [SerializeField] private bool _overrideChapterTitleTrimTitle;
    [SerializeField] private bool _chapterTitleTrimTitle = true;
    [SerializeField] private bool _overrideChapterTitleUppercaseTitle;
    [SerializeField] private bool _chapterTitleUppercaseTitle;

    [Header("Заголовок главы: особый padding")]
    [SerializeField] private bool _overrideChapterTitleSpecificPaddingSettings;
    [SerializeField] private bool _chapterTitleUseSpecificPadding = true;
    [SerializeField] private string[] _chapterTitleSpecificPaddingMarkers = { "ТАМ, У ВОДЫ" };
    [SerializeField] private Vector2 _chapterTitleSpecificPadding = new Vector2(390f, 72f);

    [Header("Заголовок главы: движение")]
    [SerializeField] private bool _overrideChapterTitleAnimationMode;
    [SerializeField] private ChapterTitleAnimationMode _chapterTitleAnimationMode = ChapterTitleAnimationMode.Fade;
    [SerializeField] private bool _overrideChapterTitleShownPosition;
    [SerializeField] private Vector2 _chapterTitleShownPosition;
    [SerializeField] private bool _overrideChapterTitleCaptureShownPositionOnAwake;
    [SerializeField] private bool _chapterTitleCaptureShownPositionOnAwake = true;
    [SerializeField] private bool _overrideChapterTitleHiddenOffsetY;
    [SerializeField] private float _chapterTitleHiddenOffsetY = 360f;
    [SerializeField] private bool _overrideChapterTitleEnterDuration;
    [SerializeField, Min(0f)] private float _chapterTitleEnterDuration = 0.45f;
    [SerializeField] private bool _overrideChapterTitleVisibleDuration;
    [SerializeField, Min(0f)] private float _chapterTitleVisibleDuration = 1.35f;
    [SerializeField] private bool _overrideChapterTitleExitDuration;
    [SerializeField, Min(0f)] private float _chapterTitleExitDuration = 1.35f;
    [SerializeField] private bool _overrideChapterTitleFadeWithMovement;
    [SerializeField] private bool _chapterTitleFadeWithMovement = true;
    [SerializeField] private bool _overrideChapterTitleAnimatePosition;
    [SerializeField] private bool _chapterTitleAnimatePosition;
    [SerializeField] private bool _overrideChapterTitleUseUnscaledTime;
    [SerializeField] private bool _chapterTitleUseUnscaledTime = true;
    [SerializeField] private bool _overrideChapterTitleDisableRootAfterExit;
    [SerializeField] private bool _chapterTitleDisableRootAfterExit;

    [Header("Дополнительные настройки")]
    [Tooltip("Если включено, стиль меняет Color у фонового Image.")]
    [SerializeField] private bool _overrideColor;
    [SerializeField] private Color _color = Color.white;

    [Tooltip("Если включено, стиль меняет Image Type, например Simple, Sliced или Tiled.")]
    [SerializeField] private bool _overrideImageType;
    [SerializeField] private Image.Type _imageType = Image.Type.Sliced;

    [Tooltip("Если включено, стиль меняет Preserve Aspect у фонового Image.")]
    [SerializeField] private bool _overridePreserveAspect;
    [SerializeField] private bool _preserveAspect;

    [Tooltip("Если включено, стиль меняет Pixels Per Unit Multiplier. Полезно для Sliced и Tiled спрайтов.")]
    [SerializeField] private bool _overridePixelsPerUnitMultiplier;
    [SerializeField, Min(0.01f)] private float _pixelsPerUnitMultiplier = 1f;

    [Tooltip("Если включено, стиль меняет Material у фонового Image.")]
    [SerializeField] private bool _overrideMaterial;
    [SerializeField] private Material _material;

    [Tooltip("Если включено, стиль меняет Raycast Target: будет ли фон плашки перехватывать клики.")]
    [SerializeField] private bool _overrideRaycastTarget;
    [SerializeField] private bool _raycastTarget;

    [Header("End Screen Style")]
    [SerializeField] private StoryEndScreenStyleSettings _endScreenStyle = new StoryEndScreenStyleSettings();

    public Sprite BackgroundSprite => _backgroundSprite;
    public UnityEngine.Object BackgroundSpriteSource => _backgroundSpriteSource;
    public UnityEngine.Object ChoiceButtonSpriteSource => _choiceButtonSpriteSource;
    public UnityEngine.Object ChoicePanelSpriteSource => _choicePanelSpriteSource;
    public UnityEngine.Object StatPanelSpriteSource => _statPanelSpriteSource;
    public UnityEngine.Object ChapterTitlePanelSpriteSource => _chapterTitlePanelSpriteSource;
    public UnityEngine.Object NamePanelBackgroundSpriteSource => _namePanelBackgroundSpriteSource;
    public UnityEngine.Object NameInputFieldSpriteSource => _nameInputFieldSpriteSource;
    public UnityEngine.Object NameConfirmButtonSpriteSource => _nameConfirmButtonSpriteSource;
    public StoryEndScreenStyleSettings EndScreenStyle => _endScreenStyle;
    public bool HasEndScreenStyleOverrides => _endScreenStyle != null && _endScreenStyle.HasOverrides;
    public GameObject ChoiceButtonPrefabOverride => _choicesApplyOnlySprites ? null : _choiceButtonPrefabOverride;
    public GameObject PremiumChoiceButtonPrefabOverride => _choicesApplyOnlySprites ? null : _premiumChoiceButtonPrefabOverride;
    public GameObject PremiumChoiceBalancePanelPrefabOverride => _premiumChoiceBalancePanelPrefabOverride;
    public Vector2 PremiumChoiceBalancePanelOffset => _premiumChoiceBalancePanelOffset;
    public GameObject NameConfirmButtonPrefabOverride => _nameInputApplyOnlySprites ? null : _nameConfirmButtonPrefabOverride;
    public bool DialogueApplyOnlySprites => _dialogueApplyOnlySprites;
    public bool NameInputApplyOnlySprites => _nameInputApplyOnlySprites;
    public bool ChoicesApplyOnlySprites => _choicesApplyOnlySprites;
    public bool StatsApplyOnlySprites => _statsApplyOnlySprites;
    public bool ChapterApplyOnlySprites => _chapterApplyOnlySprites;
    public bool HasDialogueBackgroundSprite => _backgroundSprite != null || ResolveSpriteFromSource(_backgroundSpriteSource) != null;
    public bool HasDialogueBackgroundRectOverrides =>
        !_dialogueApplyOnlySprites &&
        (_overrideDialogueBackgroundAnchors ||
         _overrideDialogueBackgroundPivot ||
         _overrideDialogueBackgroundRect ||
         _overrideDialogueBackgroundStretchOffsets);
    public bool OverrideDialogueBackgroundAnchors => !_dialogueApplyOnlySprites && _overrideDialogueBackgroundAnchors;
    public Vector2 DialogueBackgroundAnchorMin => _dialogueBackgroundAnchorMin;
    public Vector2 DialogueBackgroundAnchorMax => _dialogueBackgroundAnchorMax;
    public bool OverrideDialogueBackgroundPivot => !_dialogueApplyOnlySprites && _overrideDialogueBackgroundPivot;
    public Vector2 DialogueBackgroundPivot => _dialogueBackgroundPivot;
    public bool OverrideDialogueBackgroundRect => !_dialogueApplyOnlySprites && _overrideDialogueBackgroundRect;
    public Vector2 DialogueBackgroundAnchoredPosition => _dialogueBackgroundAnchoredPosition;
    public Vector2 DialogueBackgroundSizeDelta => _dialogueBackgroundSizeDelta;
    public bool OverrideDialogueBackgroundStretchOffsets => !_dialogueApplyOnlySprites && _overrideDialogueBackgroundStretchOffsets;
    public Vector4 DialogueBackgroundStretchOffsets => _dialogueBackgroundStretchOffsets;
    public bool OverrideDialoguePanelRect => !_dialogueApplyOnlySprites && _overrideDialoguePanelRect;
    public Vector2 DialoguePanelAnchoredPosition => _dialoguePanelAnchoredPosition;
    public Vector2 DialoguePanelSizeDelta => _dialoguePanelSizeDelta;
    public bool OverrideDialoguePanelAutoHeight => !_dialogueApplyOnlySprites && _overrideDialoguePanelAutoHeight;
    public bool DialoguePanelAutoHeight => _dialoguePanelAutoHeight;
    public float DialoguePanelAutoHeightPadding => _dialoguePanelAutoHeightPadding;
    public float DialoguePanelAutoMinHeight => _dialoguePanelAutoMinHeight;
    public float DialoguePanelAutoMaxHeight => _dialoguePanelAutoMaxHeight;
    public bool DialoguePanelAutoHeightKeepTop => _dialoguePanelAutoHeightKeepTop;
    public float DialoguePanelAutoHeightGrowthUpFactor => _dialoguePanelAutoHeightGrowthUpFactor;
    public bool OverrideDialoguePanelVerticalLayout => !_dialogueApplyOnlySprites && _overrideDialoguePanelVerticalLayout;
    public RectOffset DialoguePanelVerticalLayoutPadding => _dialoguePanelVerticalLayoutPadding;
    public float DialoguePanelVerticalLayoutSpacing => _dialoguePanelVerticalLayoutSpacing;
    public TextAnchor DialoguePanelVerticalLayoutChildAlignment => _dialoguePanelVerticalLayoutChildAlignment;
    public bool DialoguePanelVerticalLayoutReverseArrangement => _dialoguePanelVerticalLayoutReverseArrangement;
    public bool DialoguePanelVerticalLayoutControlChildWidth => _dialoguePanelVerticalLayoutControlChildWidth;
    public bool DialoguePanelVerticalLayoutControlChildHeight => _dialoguePanelVerticalLayoutControlChildHeight;
    public bool DialoguePanelVerticalLayoutUseChildScaleWidth => _dialoguePanelVerticalLayoutUseChildScaleWidth;
    public bool DialoguePanelVerticalLayoutUseChildScaleHeight => _dialoguePanelVerticalLayoutUseChildScaleHeight;
    public bool DialoguePanelVerticalLayoutChildForceExpandWidth => _dialoguePanelVerticalLayoutChildForceExpandWidth;
    public bool DialoguePanelVerticalLayoutChildForceExpandHeight => _dialoguePanelVerticalLayoutChildForceExpandHeight;
    public bool OverrideDialoguePanelContentSizeFitter => !_dialogueApplyOnlySprites && _overrideDialoguePanelContentSizeFitter;
    public ContentSizeFitter.FitMode DialoguePanelContentSizeFitterHorizontalFit => _dialoguePanelContentSizeFitterHorizontalFit;
    public ContentSizeFitter.FitMode DialoguePanelContentSizeFitterVerticalFit => _dialoguePanelContentSizeFitterVerticalFit;
    public bool OverrideBodyTextOffsetY => !_dialogueApplyOnlySprites && _overrideBodyTextOffsetY;
    public float BodyTextOffsetY => _bodyTextOffsetY;
    public bool OverrideBodyTextTopOffsetY => !_dialogueApplyOnlySprites && _overrideBodyTextTopOffsetY;
    public float BodyTextTopOffsetY => _bodyTextTopOffsetY;
    public bool OverrideBodyTextGrowDownOffsetX => !_dialogueApplyOnlySprites && _overrideBodyTextGrowDownOffsetX;
    public float BodyTextGrowDownOffsetX => _bodyTextGrowDownOffsetX;
    public bool OverrideBodyTextResizeHeightToPreferredText => !_dialogueApplyOnlySprites && _overrideBodyTextResizeHeightToPreferredText;
    public bool BodyTextResizeHeightToPreferredText => _bodyTextResizeHeightToPreferredText;
    public bool OverrideBodyTextExtraHeight => !_dialogueApplyOnlySprites && _overrideBodyTextExtraHeight;
    public float BodyTextExtraHeight => _bodyTextExtraHeight;
    public bool OverrideBodyTextMinHeight => !_dialogueApplyOnlySprites && _overrideBodyTextMinHeight;
    public float BodyTextMinHeight => _bodyTextMinHeight;
    public bool OverrideBodyTextMaxHeight => !_dialogueApplyOnlySprites && _overrideBodyTextMaxHeight;
    public float BodyTextMaxHeight => _bodyTextMaxHeight;
    public bool OverrideBodyTextMaxFontSize => !_dialogueApplyOnlySprites && _overrideBodyTextMaxFontSize;
    public float BodyTextMaxFontSize => _bodyTextMaxFontSize;
    public bool OverrideBodyTextFont => !_dialogueApplyOnlySprites && _overrideBodyTextFont && _bodyTextFont != null;
    public TMP_FontAsset BodyTextFont => _bodyTextFont;
    public bool OverrideBodyTextShrinkTextToFitRect => !_dialogueApplyOnlySprites && _overrideBodyTextShrinkTextToFitRect;
    public bool BodyTextShrinkTextToFitRect => _bodyTextShrinkTextToFitRect;
    public bool OverrideBodyTextMinAutoFontSize => !_dialogueApplyOnlySprites && _overrideBodyTextMinAutoFontSize;
    public float BodyTextMinAutoFontSize => _bodyTextMinAutoFontSize;
    public bool OverrideBodyTextOverflowModeWhenStillTooLarge => !_dialogueApplyOnlySprites && _overrideBodyTextOverflowModeWhenStillTooLarge;
    public TextOverflowModes BodyTextOverflowModeWhenStillTooLarge => _bodyTextOverflowModeWhenStillTooLarge;
    public bool OverrideBodyTextHorizontalClamp => !_dialogueApplyOnlySprites && _overrideBodyTextHorizontalClamp;
    public bool BodyTextHorizontalClamp => _bodyTextHorizontalClamp;
    public float BodyTextHorizontalInset => _bodyTextHorizontalInset;
    public float BodyTextMaxWidth => _bodyTextMaxWidth;
    public IReadOnlyList<DialoguePanelExtraLayerStyle> DialogueExtraLayers => _dialogueApplyOnlySprites ? System.Array.Empty<DialoguePanelExtraLayerStyle>() : _dialogueExtraLayers;
    public bool OverrideCharacterNameOffset => !_dialogueApplyOnlySprites && _overrideCharacterNameOffset;
    public Vector2 CharacterNameOffset => _characterNameOffset;
    public bool OverrideCharacterNameFont => !_dialogueApplyOnlySprites && _overrideCharacterNameFont && _characterNameFont != null;
    public TMP_FontAsset CharacterNameFont => _characterNameFont;
    public bool OverrideCharacterNameFontSize => !_dialogueApplyOnlySprites && _overrideCharacterNameFontSize;
    public float CharacterNameFontSize => Mathf.Max(1f, _characterNameFontSize);
    public UnityEngine.Object NamePlateSpriteSource => _namePlateSpriteSource;
    public bool HasNamePlateImageOverrides =>
        _namePlateSprite != null ||
        ResolveSpriteFromSource(_namePlateSpriteSource) != null ||
        (!_dialogueApplyOnlySprites &&
            (_overrideNamePlateColor ||
             _overrideNamePlateImageType ||
             _overrideNamePlatePreserveAspect ||
             _overrideNamePlatePixelsPerUnitMultiplier ||
             _overrideNamePlateMaterial ||
             _overrideNamePlateRaycastTarget));
    public bool OverrideNamePlateRect => !_dialogueApplyOnlySprites && _overrideNamePlateRect;
    public Vector2 NamePlateAnchoredPosition => _namePlateAnchoredPosition;
    public Vector2 NamePlateSizeDelta => _namePlateSizeDelta;
    public IReadOnlyList<StatChangeOverlayDefinition> StatOverlayDefinitions => _statOverlayDefinitions;
    public IReadOnlyList<StatDefinition> StatDefinitionAssets => _statDefinitionAssets;
    public bool OverrideStatPanelVerticalLayout => !_statsApplyOnlySprites && _overrideStatPanelVerticalLayout;
    public RectOffset StatPanelVerticalLayoutPadding => _statPanelVerticalLayoutPadding;
    public float StatPanelVerticalLayoutSpacing => _statPanelVerticalLayoutSpacing;
    public TextAnchor StatPanelVerticalLayoutChildAlignment => _statPanelVerticalLayoutChildAlignment;
    public bool StatPanelVerticalLayoutReverseArrangement => _statPanelVerticalLayoutReverseArrangement;
    public bool StatPanelVerticalLayoutControlChildWidth => _statPanelVerticalLayoutControlChildWidth;
    public bool StatPanelVerticalLayoutControlChildHeight => _statPanelVerticalLayoutControlChildHeight;
    public bool StatPanelVerticalLayoutUseChildScaleWidth => _statPanelVerticalLayoutUseChildScaleWidth;
    public bool StatPanelVerticalLayoutUseChildScaleHeight => _statPanelVerticalLayoutUseChildScaleHeight;
    public bool StatPanelVerticalLayoutChildForceExpandWidth => _statPanelVerticalLayoutChildForceExpandWidth;
    public bool StatPanelVerticalLayoutChildForceExpandHeight => _statPanelVerticalLayoutChildForceExpandHeight;
    public bool OverrideStatPanelContentSizeFitter => !_statsApplyOnlySprites && _overrideStatPanelContentSizeFitter;
    public ContentSizeFitter.FitMode StatPanelContentSizeFitterHorizontalFit => _statPanelContentSizeFitterHorizontalFit;
    public ContentSizeFitter.FitMode StatPanelContentSizeFitterVerticalFit => _statPanelContentSizeFitterVerticalFit;

    public void ApplyTo(Image target)
    {
        if (target == null)
            return;

        if (_dialogueApplyOnlySprites)
        {
            ApplySpriteOnly(target, _backgroundSprite, _backgroundSpriteSource);
            return;
        }

        ApplyImageOverrides(
            target,
            _backgroundSprite,
            _backgroundSpriteSource,
            _overrideColor,
            _color,
            _overrideImageType,
            _imageType);

        SVGImage svgImage = target.GetComponent<SVGImage>();
        bool svgActive = svgImage != null && svgImage.enabled;

        if (_overridePreserveAspect)
        {
            target.preserveAspect = _preserveAspect;
            if (svgActive)
                svgImage.preserveAspect = _preserveAspect;
        }

        if (_overridePixelsPerUnitMultiplier)
            target.pixelsPerUnitMultiplier = Mathf.Max(0.01f, _pixelsPerUnitMultiplier);

        if (_overrideMaterial)
        {
            target.material = _material;
            if (svgActive)
                svgImage.material = _material;
        }

        if (_overrideRaycastTarget)
        {
            target.raycastTarget = _raycastTarget;
            if (svgActive)
                svgImage.raycastTarget = _raycastTarget;
        }

        target.SetAllDirty();
        if (svgActive)
            svgImage.SetAllDirty();
    }

    public void ApplyToDialogueBackgroundRect(RectTransform rect)
    {
        if (rect == null || _dialogueApplyOnlySprites)
            return;

        if (_overrideDialogueBackgroundAnchors)
        {
            rect.anchorMin = _dialogueBackgroundAnchorMin;
            rect.anchorMax = _dialogueBackgroundAnchorMax;
        }

        if (_overrideDialogueBackgroundPivot)
            rect.pivot = _dialogueBackgroundPivot;

        ApplyRectOverrides(
            rect,
            _overrideDialogueBackgroundRect,
            _dialogueBackgroundAnchoredPosition,
            _dialogueBackgroundSizeDelta);
        ApplyStretchOffsetOverrides(
            rect,
            _overrideDialogueBackgroundStretchOffsets,
            _dialogueBackgroundStretchOffsets);

        if (HasDialogueBackgroundRectOverrides)
            LayoutRebuilder.MarkLayoutForRebuild(rect);
    }

    public void ApplyToDialogueExtraLayer(Image target, DialoguePanelExtraLayerStyle layer)
    {
        if (target == null || layer == null)
            return;

        if (_dialogueApplyOnlySprites)
        {
            if (layer.Enabled)
                ApplySpriteOnly(target, layer.Sprite, layer.SpriteSource);
            return;
        }

        target.gameObject.SetActive(layer.Enabled);
        if (!layer.Enabled)
            return;

        RectTransform rect = target.rectTransform;
        if (layer.OverrideRect && rect != null)
        {
            rect.anchoredPosition = layer.AnchoredPosition;
            rect.sizeDelta = layer.SizeDelta;
        }

        ApplyImageOverrides(
            target,
            layer.Sprite,
            layer.SpriteSource,
            layer.OverrideColor,
            layer.Color,
            layer.OverrideImageType,
            layer.ImageType);

        if (layer.OverrideRaycastTarget)
            target.raycastTarget = layer.RaycastTarget;
    }

    public void ApplyToNamePlate(Image target, RectTransform rect)
    {
        if (rect == null && target != null)
            rect = target.rectTransform;

        if (_dialogueApplyOnlySprites)
        {
            ApplySpriteOnly(target, _namePlateSprite, _namePlateSpriteSource);
            return;
        }

        if (rect != null)
        {
            if (_overrideNamePlateAnchors)
            {
                rect.anchorMin = _namePlateAnchorMin;
                rect.anchorMax = _namePlateAnchorMax;
            }

            if (_overrideNamePlatePivot)
                rect.pivot = _namePlatePivot;
        }

        ApplyRectOverrides(
            rect,
            _overrideNamePlateRect,
            _namePlateAnchoredPosition,
            _namePlateSizeDelta);

        if ((_overrideNamePlateRect || _overrideNamePlateAnchors || _overrideNamePlatePivot) && rect != null)
            LayoutRebuilder.MarkLayoutForRebuild(rect);

        ApplyImageOverrides(
            target,
            _namePlateSprite,
            _namePlateSpriteSource,
            _overrideNamePlateColor,
            _namePlateColor,
            _overrideNamePlateImageType,
            _namePlateImageType);

        if (target == null)
            return;

        SVGImage svgImage = target.GetComponent<SVGImage>();
        bool svgActive = svgImage != null && svgImage.enabled;

        if (_overrideNamePlatePreserveAspect)
        {
            target.preserveAspect = _namePlatePreserveAspect;
            if (svgActive)
                svgImage.preserveAspect = _namePlatePreserveAspect;
        }

        if (_overrideNamePlatePixelsPerUnitMultiplier)
            target.pixelsPerUnitMultiplier = Mathf.Max(0.01f, _namePlatePixelsPerUnitMultiplier);

        if (_overrideNamePlateMaterial)
        {
            target.material = _namePlateMaterial;
            if (svgActive)
                svgImage.material = _namePlateMaterial;
        }

        if (_overrideNamePlateRaycastTarget)
        {
            target.raycastTarget = _namePlateRaycastTarget;
            if (svgActive)
                svgImage.raycastTarget = _namePlateRaycastTarget;
        }

        target.SetAllDirty();
        if (svgActive)
            svgImage.SetAllDirty();
    }

    public void ApplyToChoiceButton(Button target)
    {
        if (target == null)
            return;

        Image image = target.targetGraphic as Image;
        if (image == null)
            image = target.GetComponent<Image>();

        if (_choicesApplyOnlySprites)
        {
            ApplySpriteOnly(image, _choiceButtonSprite, _choiceButtonSpriteSource);
            SVGImage spriteOnlySvg = image != null ? image.GetComponent<SVGImage>() : null;
            if (spriteOnlySvg != null && spriteOnlySvg.enabled)
                target.targetGraphic = spriteOnlySvg;
            else if (image != null)
                target.targetGraphic = image;
            return;
        }

        ApplyImageOverrides(
            image,
            _choiceButtonSprite,
            _choiceButtonSpriteSource,
            _overrideChoiceButtonColor,
            _choiceButtonColor,
            _overrideChoiceButtonImageType,
            _choiceButtonImageType);

        SVGImage svgImage = image != null ? image.GetComponent<SVGImage>() : null;
        target.targetGraphic = svgImage != null && svgImage.enabled ? svgImage : image;

        ButtonTextAutoSize autoSize = target.GetComponent<ButtonTextAutoSize>();
        if (autoSize != null && _overrideChoiceButtonPadding)
            autoSize.SetPadding(_choiceButtonPadding);

        TMP_Text label = target.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            return;

        if (_overrideChoiceButtonTextColor)
            label.color = _choiceButtonTextColor;

        if (_overrideChoiceButtonFont && _choiceButtonFont != null)
            label.font = _choiceButtonFont;

        if (_overrideChoiceButtonFontSize)
        {
            float fontSize = Mathf.Max(1f, _choiceButtonFontSize);
            label.fontSize = fontSize;
            if (label.enableAutoSizing && label.fontSizeMax > 0f)
                label.fontSizeMax = Mathf.Min(label.fontSizeMax, fontSize);
        }

        label.SetAllDirty();
    }

    public void ApplyToChoiceLayout(DialogueChoiceLayout target)
    {
        if (target == null)
            return;

        if (_choicesApplyOnlySprites)
            return;

        if (_overrideChoiceButtonPadding)
            target.SetPadding(_choiceButtonPadding);

        if (_overrideChoiceButtonTextPadding)
        {
            _choiceButtonTextPadding ??= new RectOffset(48, 48, 18, 18);
            target.SetTextPadding(_choiceButtonTextPadding);
        }

        if (_overrideChoiceButtonTextOffset)
            target.SetTextOffset(_choiceButtonTextOffset);

        if (_overrideChoiceButtonFontSize)
            target.SetFontSize(_choiceButtonFontSize);

        target.RefreshNow();
    }

    public void ApplyToChoicePanel(Image target)
    {
        if (_choicesApplyOnlySprites)
        {
            ApplySpriteOnly(target, _choicePanelSprite, _choicePanelSpriteSource);
            return;
        }

        ApplyImageOverrides(
            target,
            _choicePanelSprite,
            _choicePanelSpriteSource,
            _overrideChoicePanelColor,
            _choicePanelColor,
            _overrideChoicePanelImageType,
            _choicePanelImageType);
    }

    public void ApplyToPreStorySetupFlow(PreStorySetupFlow target)
    {
        if (target == null)
            return;

        if (_nameInputApplyOnlySprites)
        {
            target.ApplyNameConfirmButtonPrefabOverride(null);
            ApplyOptionalSpriteOnly(target.NameScreenBackgroundImage, _nameScreenBackgroundSprite, _nameScreenBackgroundSpriteSource);
            ApplySpriteOnly(target.NamePanelBackgroundImage, _namePanelBackgroundSprite, _namePanelBackgroundSpriteSource);
            ApplySpriteOnly(target.NameInputFieldImage, _nameInputFieldSprite, _nameInputFieldSpriteSource);
            ApplySpriteOnly(target.NameConfirmButtonImage, _nameConfirmButtonSprite, _nameConfirmButtonSpriteSource);
            return;
        }

        target.ApplyNameConfirmButtonPrefabOverride(_nameConfirmButtonPrefabOverride);

        ApplyOptionalImageOverrides(
            target.NameScreenBackgroundImage,
            _nameScreenBackgroundSprite,
            _nameScreenBackgroundSpriteSource,
            _overrideNameScreenBackgroundColor,
            _nameScreenBackgroundColor,
            _overrideNameScreenBackgroundImageType,
            _nameScreenBackgroundImageType);

        ApplyImageOverrides(
            target.NamePanelBackgroundImage,
            _namePanelBackgroundSprite,
            _namePanelBackgroundSpriteSource,
            _overrideNamePanelBackgroundColor,
            _namePanelBackgroundColor,
            _overrideNamePanelBackgroundImageType,
            _namePanelBackgroundImageType);
        ApplyRectOverrides(
            target.NamePanelBackgroundRect,
            _overrideNamePanelBackgroundRect,
            _namePanelBackgroundAnchoredPosition,
            _namePanelBackgroundSizeDelta);

        ApplyImageOverrides(
            target.NameInputFieldImage,
            _nameInputFieldSprite,
            _nameInputFieldSpriteSource,
            _overrideNameInputFieldColor,
            _nameInputFieldColor,
            _overrideNameInputFieldImageType,
            _nameInputFieldImageType);
        ApplyRectOverrides(
            target.NameInputFieldRect,
            _overrideNameInputFieldRect,
            _nameInputFieldAnchoredPosition,
            _nameInputFieldSizeDelta);
        ApplyRectOverrides(
            target.NameInputTextRect,
            _overrideNameInputTextRect,
            _nameInputTextAnchoredPosition,
            _nameInputTextSizeDelta);
        ApplyTextOverrides(
            target.NameInputText,
            _overrideNameInputTextColor,
            _nameInputTextColor,
            _overrideNameInputTextFont,
            _nameInputTextFont,
            _overrideNameInputTextFontSize,
            _nameInputTextFontSize);
        ApplyRectOverrides(
            target.NamePlaceholderTextRect,
            _overrideNamePlaceholderTextRect,
            _namePlaceholderTextAnchoredPosition,
            _namePlaceholderTextSizeDelta);
        ApplyTextOverrides(
            target.NamePlaceholderText,
            _overrideNamePlaceholderTextColor || _overrideNameInputTextColor,
            _overrideNamePlaceholderTextColor ? _namePlaceholderTextColor : _nameInputTextColor,
            _overrideNamePlaceholderTextFont || (_overrideNameInputTextFont && _namePlaceholderTextFont == null),
            _namePlaceholderTextFont != null ? _namePlaceholderTextFont : _nameInputTextFont,
            _overrideNamePlaceholderTextFontSize || _overrideNameInputTextFontSize,
            _overrideNamePlaceholderTextFontSize ? _namePlaceholderTextFontSize : _nameInputTextFontSize);

        ApplyImageOverrides(
            target.NameConfirmButtonImage,
            _nameConfirmButtonSprite,
            _nameConfirmButtonSpriteSource,
            _overrideNameConfirmButtonColor,
            _nameConfirmButtonColor,
            _overrideNameConfirmButtonImageType,
            _nameConfirmButtonImageType);
        ApplyRectOverrides(
            target.NameConfirmButtonRect,
            _overrideNameConfirmButtonRect,
            _nameConfirmButtonAnchoredPosition,
            _nameConfirmButtonSizeDelta);
        ApplyRectOverrides(
            target.NameConfirmButtonTextRect,
            _overrideNameConfirmButtonTextRect,
            _nameConfirmButtonTextAnchoredPosition,
            _nameConfirmButtonTextSizeDelta);
        ApplyTextOverrides(
            target.NameConfirmButtonText,
            _overrideNameConfirmButtonTextColor,
            _nameConfirmButtonTextColor,
            _overrideNameConfirmButtonTextFont,
            _nameConfirmButtonTextFont,
            _overrideNameConfirmButtonTextFontSize,
            _nameConfirmButtonTextFontSize);

        ApplyNameExtraTexts(target);
    }

    public void ApplyToStatChangeOverlay(StatChangeOverlay target)
    {
        if (target == null)
            return;

        if (_statsApplyOnlySprites)
        {
            ApplySpriteOnly(target.PanelBackgroundImage, _statPanelSprite, _statPanelSpriteSource);
            return;
        }

        ApplyImageOverrides(
            target.PanelBackgroundImage,
            _statPanelSprite,
            _statPanelSpriteSource,
            _overrideStatPanelColor,
            _statPanelColor,
            _overrideStatPanelImageType,
            _statPanelImageType);

        TMP_Text messageText = target.MessageText;
        if (messageText != null)
        {
            if (_overrideStatTextColor)
                messageText.color = _statTextColor;

            if (_overrideStatTextFont && _statTextFont != null)
                messageText.font = _statTextFont;

            if (_overrideStatTextFontSize)
                messageText.fontSize = Mathf.Max(1f, _statTextFontSize);

            messageText.SetAllDirty();
        }

        if (_replaceStatDefinitions)
            target.ReplaceDefinitions(_statOverlayDefinitions, _statDefinitionAssets);
        target.ReplaceStatIconOffsetOverrides(_statIconOffsetOverrides);
        target.ReplaceStatPanelSizeOverrides(_statPanelSizeOverrides);
        target.ReplaceStatTextRectOverrides(_statTextRectOverrides);
        target.ReplaceRelationshipMessageOverrides(_relationshipMessageOverrides);

        target.ApplyLayoutOverrides(
            _overrideStatPanelPadding,
            _statPanelPadding,
            _overrideStatIconSize,
            _statIconSize,
            _overrideStatIconOffset,
            _statIconOffset,
            _overrideStatIconVisualScale,
            _statIconVisualScale,
            _overrideStatIconMinSize,
            _statIconMinSize,
            _overrideStatIconReserveSpaceWhenHidden,
            _statIconReserveSpaceWhenHidden,
            _overrideStatIconParentSpacing,
            _statIconParentSpacing,
            _overrideStatIconParentPadding,
            _statIconParentPadding);

        target.ApplyPanelLayoutGroupOverrides(
            _overrideStatPanelVerticalLayout,
            _statPanelVerticalLayoutPadding,
            _statPanelVerticalLayoutSpacing,
            _statPanelVerticalLayoutChildAlignment,
            _statPanelVerticalLayoutReverseArrangement,
            _statPanelVerticalLayoutControlChildWidth,
            _statPanelVerticalLayoutControlChildHeight,
            _statPanelVerticalLayoutUseChildScaleWidth,
            _statPanelVerticalLayoutUseChildScaleHeight,
            _statPanelVerticalLayoutChildForceExpandWidth,
            _statPanelVerticalLayoutChildForceExpandHeight,
            _overrideStatPanelContentSizeFitter,
            _statPanelContentSizeFitterHorizontalFit,
            _statPanelContentSizeFitterVerticalFit);
        target.ApplyRelationshipPanelLayoutGroupOverrides(
            _overrideRelationshipPanelVerticalLayout,
            _relationshipPanelVerticalLayoutPadding,
            _relationshipPanelVerticalLayoutSpacing,
            _relationshipPanelVerticalLayoutChildAlignment,
            _relationshipPanelVerticalLayoutReverseArrangement,
            _relationshipPanelVerticalLayoutControlChildWidth,
            _relationshipPanelVerticalLayoutControlChildHeight,
            _relationshipPanelVerticalLayoutUseChildScaleWidth,
            _relationshipPanelVerticalLayoutUseChildScaleHeight,
            _relationshipPanelVerticalLayoutChildForceExpandWidth,
            _relationshipPanelVerticalLayoutChildForceExpandHeight,
            _overrideRelationshipPanelContentSizeFitter,
            _relationshipPanelContentSizeFitterHorizontalFit,
            _relationshipPanelContentSizeFitterVerticalFit);

        target.ApplyPanelAndTextLayoutOverrides(
            _overrideStatPanelRect,
            _statPanelAnchoredPosition,
            _statPanelSizeDelta,
            _overrideStatTextRect,
            _statTextAnchoredPosition,
            _statTextSizeDelta,
            _overrideStatTextAutoSize,
            _statTextAutoSize,
            _overrideStatTextAutoFontSizeRange,
            _statTextMinAutoFontSize,
            _statTextMaxAutoFontSize,
            _overrideStatTextAlignment,
            _statTextAlignment,
            _overrideStatTextWordWrapping,
            _statTextWordWrapping,
            _overrideStatTextOverflowMode,
            _statTextOverflowMode,
            _overrideStatTextLineSpacing,
            _statTextLineSpacing,
            _overrideStatTextMargins,
            _statTextMargins);

        target.ApplyPanelBackgroundRectOverrides(
            _overrideStatPanelBackgroundAnchors,
            _statPanelBackgroundAnchorMin,
            _statPanelBackgroundAnchorMax,
            _overrideStatPanelBackgroundPivot,
            _statPanelBackgroundPivot,
            _overrideStatPanelBackgroundStretchOffsets,
            _statPanelBackgroundStretchOffsets);
        target.ApplyRelationshipPanelBackgroundRectOverrides(
            _overrideRelationshipPanelBackgroundAnchors,
            _relationshipPanelBackgroundAnchorMin,
            _relationshipPanelBackgroundAnchorMax,
            _overrideRelationshipPanelBackgroundPivot,
            _relationshipPanelBackgroundPivot,
            _overrideRelationshipPanelBackgroundRect,
            _relationshipPanelBackgroundAnchoredPosition,
            _relationshipPanelBackgroundSizeDelta,
            _overrideRelationshipPanelBackgroundStretchOffsets,
            _relationshipPanelBackgroundStretchOffsets);

        target.ApplyRelationshipLayoutOverrides(
            _overrideRelationshipFrameSize,
            _relationshipFrameAnchoredPosition,
            _relationshipFrameSize,
            _overrideRelationshipFontSizeRange,
            _relationshipFontSizeMin,
            _relationshipFontSizeMax,
            _overrideRelationshipMaxVisibleLines,
            _relationshipMaxVisibleLines);
    }

    public void ApplyToChapterTitleOverlay(ChapterTitleOverlay target)
    {
        if (target == null)
            return;

        if (_chapterApplyOnlySprites)
        {
            ApplySpriteOnly(target.PanelBackgroundImage, _chapterTitlePanelSprite, _chapterTitlePanelSpriteSource);
            return;
        }

        target.SetStoryStyleTitleTextControl(
            _overrideChapterTitleTextRect || _overrideChapterTitleTextHeightLimits,
            _overrideChapterTitleTextOverflowMode,
            _overrideChapterTitleTextAutoSize || _overrideChapterTitleTextAutoFontSizeRange,
            _overrideChapterTitleTextWordWrapping);

        target.ApplyStorySettingsOverrides(
            _overrideChapterTitleCenterOnShow,
            _chapterTitleCenterOnShow,
            _overrideChapterTitleBringToFrontOnShow,
            _chapterTitleBringToFrontOnShow,
            _overrideChapterTitleBackgroundDimSizeMode,
            _chapterTitleBackgroundDimSizeMode,
            _overrideChapterTitleBackgroundDimFixedSize,
            _chapterTitleBackgroundDimFixedSize,
            _overrideChapterTitleBackgroundDimColor,
            _chapterTitleBackgroundDimColor,
            _overrideChapterTitleBackgroundDimAlpha,
            _chapterTitleBackgroundDimAlpha,
            _overrideChapterTitleTextMode,
            _chapterTitleTextMode,
            _overrideChapterTitleTextFormat,
            _chapterTitleTextFormat,
            _overrideChapterTitleNumberAndTitleFormat,
            _chapterTitleNumberAndTitleFormat,
            _overrideChapterTitleNumberOffset,
            _chapterTitleNumberOffset,
            _overrideChapterTitleEmptyTitleFallback,
            _chapterTitleEmptyTitleFallback,
            _overrideChapterTitleTrimTitle,
            _chapterTitleTrimTitle,
            _overrideChapterTitleUppercaseTitle,
            _chapterTitleUppercaseTitle,
            _overrideChapterTitleSpecificPaddingSettings,
            _chapterTitleUseSpecificPadding,
            _chapterTitleSpecificPaddingMarkers,
            _chapterTitleSpecificPadding,
            _overrideChapterTitleAnimationMode,
            _chapterTitleAnimationMode,
            _overrideChapterTitleShownPosition,
            _chapterTitleShownPosition,
            _overrideChapterTitleCaptureShownPositionOnAwake,
            _chapterTitleCaptureShownPositionOnAwake,
            _overrideChapterTitleHiddenOffsetY,
            _chapterTitleHiddenOffsetY,
            _overrideChapterTitleEnterDuration,
            _chapterTitleEnterDuration,
            _overrideChapterTitleVisibleDuration,
            _chapterTitleVisibleDuration,
            _overrideChapterTitleExitDuration,
            _chapterTitleExitDuration,
            _overrideChapterTitleFadeWithMovement,
            _chapterTitleFadeWithMovement,
            _overrideChapterTitleAnimatePosition,
            _chapterTitleAnimatePosition,
            _overrideChapterTitleUseUnscaledTime,
            _chapterTitleUseUnscaledTime,
            _overrideChapterTitleDisableRootAfterExit,
            _chapterTitleDisableRootAfterExit);

        ApplyImageOverrides(
            target.PanelBackgroundImage,
            _chapterTitlePanelSprite,
            _chapterTitlePanelSpriteSource,
            _overrideChapterTitlePanelColor,
            _chapterTitlePanelColor,
            _overrideChapterTitlePanelImageType,
            _chapterTitlePanelImageType);

        TMP_Text titleText = target.TitleText;
        if (titleText == null)
            return;

        ApplyRectOverrides(
            titleText.rectTransform,
            _overrideChapterTitleTextRect,
            _chapterTitleTextAnchoredPosition,
            _chapterTitleTextSizeDelta);
        ApplyHeightLimits(
            titleText.rectTransform,
            _overrideChapterTitleTextHeightLimits,
            _chapterTitleTextMinHeight,
            _chapterTitleTextMaxHeight);
        ApplyTextLayoutOverrides(
            titleText,
            _overrideChapterTitleTextColor,
            _chapterTitleTextColor,
            _overrideChapterTitleTextFont,
            _chapterTitleTextFont,
            _overrideChapterTitleTextFontSize,
            _chapterTitleTextFontSize,
            _overrideChapterTitleTextAutoSize,
            _chapterTitleTextAutoSize,
            _overrideChapterTitleTextAutoFontSizeRange,
            _chapterTitleTextMinAutoFontSize,
            _chapterTitleTextMaxAutoFontSize,
            _overrideChapterTitleTextAlignment,
            _chapterTitleTextAlignment,
            _overrideChapterTitleTextWordWrapping,
            _chapterTitleTextWordWrapping,
            _overrideChapterTitleTextOverflowMode,
            _chapterTitleTextOverflowMode,
            _overrideChapterTitleTextLineSpacing,
            _chapterTitleTextLineSpacing,
            _overrideChapterTitleTextMargins,
            _chapterTitleTextMargins);

        target.RefreshNow();
    }

    public void ApplyToEndScreen(StoryEndScreenController target, string storyId = "", bool preview = false)
    {
        if (target == null)
            return;

        target.ApplyStoryUiStyle(this, storyId, preview);
    }

    static void ApplySpriteOnly(Image target, Sprite sprite, UnityEngine.Object source)
    {
        if (target == null)
            return;

        sprite = sprite != null ? sprite : ResolveSpriteFromSource(source);
        if (sprite == null)
            return;

        if (ShouldUseSvgImage(source, sprite))
        {
            ApplySvgImageOverrides(target, sprite, source, false, target.color);
            LayoutRebuilder.MarkLayoutForRebuild(target.rectTransform);
            return;
        }

        DisableSvgImage(target);
        target.enabled = true;
        target.sprite = sprite;
        if (target.color.a <= 0.001f)
            target.color = new Color(target.color.r, target.color.g, target.color.b, 1f);

        target.SetAllDirty();
        LayoutRebuilder.MarkLayoutForRebuild(target.rectTransform);
    }

    static void ApplyOptionalSpriteOnly(Image target, Sprite sprite, UnityEngine.Object source)
    {
        if (target == null)
            return;

        sprite = sprite != null ? sprite : ResolveSpriteFromSource(source);
        if (sprite == null)
        {
            ClearOptionalImage(target);
            return;
        }

        ApplySpriteOnly(target, sprite, source);
    }

    static void ApplyOptionalImageOverrides(
        Image target,
        Sprite sprite,
        UnityEngine.Object source,
        bool overrideColor,
        Color color,
        bool overrideImageType,
        Image.Type imageType)
    {
        if (target == null)
            return;

        sprite = sprite != null ? sprite : ResolveSpriteFromSource(source);
        if (sprite == null && !overrideColor && !overrideImageType)
        {
            ClearOptionalImage(target);
            return;
        }

        ApplyImageOverrides(target, sprite, source, overrideColor, color, overrideImageType, imageType);
    }

    static void ClearOptionalImage(Image target)
    {
        if (target == null)
            return;

        DisableSvgImage(target);
        target.sprite = null;
        target.enabled = false;
        target.SetAllDirty();
        LayoutRebuilder.MarkLayoutForRebuild(target.rectTransform);
    }

    static void ApplyImageOverrides(
        Image target,
        Sprite sprite,
        UnityEngine.Object source,
        bool overrideColor,
        Color color,
        bool overrideImageType,
        Image.Type imageType)
    {
        if (target == null)
            return;

        sprite = sprite != null ? sprite : ResolveSpriteFromSource(source);

        if (sprite != null && ShouldUseSvgImage(source, sprite))
        {
            ApplySvgImageOverrides(target, sprite, source, overrideColor, color);
            LayoutRebuilder.MarkLayoutForRebuild(target.rectTransform);
            return;
        }

        DisableSvgImage(target);
        target.enabled = true;

        if (sprite != null)
        {
            target.sprite = sprite;
            if (!overrideColor && target.color.a <= 0.001f)
                target.color = new Color(target.color.r, target.color.g, target.color.b, 1f);
        }

        if (overrideColor)
            target.color = color;

        if (overrideImageType)
            target.type = imageType;

        target.SetAllDirty();
        LayoutRebuilder.MarkLayoutForRebuild(target.rectTransform);
    }

    static void ApplySvgImageOverrides(
        Image target,
        Sprite sprite,
        UnityEngine.Object source,
        bool overrideColor,
        Color color)
    {
        if (target == null || sprite == null)
            return;

        if (!TryGetOrAddSvgImage(target, out SVGImage svgImage))
        {
            ApplyImageFallback(target, sprite, overrideColor, color);
            return;
        }

        bool preserveAspect = target.preserveAspect;
        bool raycastTarget = target.raycastTarget;
        bool maskable = target.maskable;
        Material fallbackMaterial = target.material;
        Color fallbackColor = target.color;
        Material svgMaterial = ResolveSvgMaterial(source, sprite);

        svgImage.enabled = true;
        svgImage.sprite = sprite;
        svgImage.preserveAspect = preserveAspect;
        svgImage.raycastTarget = raycastTarget;
        svgImage.maskable = maskable;
        svgImage.material = svgMaterial != null ? svgMaterial : fallbackMaterial;
        svgImage.color = overrideColor ? color : fallbackColor;
        if (!overrideColor && svgImage.color.a <= 0.001f)
            svgImage.color = new Color(svgImage.color.r, svgImage.color.g, svgImage.color.b, 1f);

        target.enabled = false;
        svgImage.SetAllDirty();
    }

    static bool TryGetOrAddSvgImage(Image target, out SVGImage svgImage)
    {
        svgImage = null;
        if (target == null)
            return false;

        GameObject owner;
        try
        {
            owner = target.gameObject;
        }
        catch (System.Exception)
        {
            return false;
        }

        if (owner == null)
            return false;

        svgImage = owner.GetComponent<SVGImage>();
        if (svgImage != null)
            return true;

        return false;
    }

    static void ApplyImageFallback(Image target, Sprite sprite, bool overrideColor, Color color)
    {
        if (target == null || sprite == null)
            return;

        target.enabled = true;
        target.sprite = sprite;
        if (overrideColor)
            target.color = color;
        else if (target.color.a <= 0.001f)
            target.color = new Color(target.color.r, target.color.g, target.color.b, 1f);

        target.SetAllDirty();
        LayoutRebuilder.MarkLayoutForRebuild(target.rectTransform);
    }

    static void DisableSvgImage(Image target)
    {
        SVGImage svgImage = target != null ? target.GetComponent<SVGImage>() : null;
        if (svgImage != null)
            svgImage.enabled = false;
    }

    static bool ShouldUseSvgImage(UnityEngine.Object source, Sprite sprite)
    {
        if (!IsLikelyVectorSprite(sprite))
            return false;

        if (FindSourceSvgImage(source) != null)
            return true;

        SpriteRenderer spriteRenderer = FindSourceSpriteRenderer(source);
        if (spriteRenderer != null && IsVectorMaterial(spriteRenderer.sharedMaterial))
            return true;

        return IsLikelyVectorSprite(sprite);
    }

    static SVGImage FindSourceSvgImage(UnityEngine.Object source)
    {
        if (source is SVGImage svgImage)
            return svgImage;

        if (source is GameObject gameObject)
            return gameObject.GetComponent<SVGImage>() ?? gameObject.GetComponentInChildren<SVGImage>(true);

        if (source is Component component)
            return component.GetComponent<SVGImage>() ?? component.GetComponentInChildren<SVGImage>(true);

        return null;
    }

    static SpriteRenderer FindSourceSpriteRenderer(UnityEngine.Object source)
    {
        if (source is SpriteRenderer spriteRenderer)
            return spriteRenderer;

        if (source is GameObject gameObject)
            return gameObject.GetComponent<SpriteRenderer>() ?? gameObject.GetComponentInChildren<SpriteRenderer>(true);

        if (source is Component component)
            return component.GetComponent<SpriteRenderer>() ?? component.GetComponentInChildren<SpriteRenderer>(true);

        return null;
    }

    static Material ResolveSvgMaterial(UnityEngine.Object source, Sprite sprite)
    {
        SVGImage svgImage = FindSourceSvgImage(source);
        if (svgImage != null && svgImage.material != null)
            return svgImage.material;

        SpriteRenderer spriteRenderer = FindSourceSpriteRenderer(source);
        if (spriteRenderer != null && spriteRenderer.sharedMaterial != null)
            return ResolveFallbackSvgMaterial(sprite) ?? spriteRenderer.sharedMaterial;

        return ResolveFallbackSvgMaterial(sprite);
    }

    static Sprite ResolveSpriteFromSource(UnityEngine.Object source)
    {
        if (source is Sprite sprite)
            return sprite;

        SVGImage svgImage = FindSourceSvgImage(source);
        if (svgImage != null && svgImage.sprite != null)
            return svgImage.sprite;

        SpriteRenderer spriteRenderer = FindSourceSpriteRenderer(source);
        if (spriteRenderer != null && spriteRenderer.sprite != null)
            return spriteRenderer.sprite;

        Image image = FindSourceImage(source);
        return image != null ? image.sprite : null;
    }

    static Image FindSourceImage(UnityEngine.Object source)
    {
        if (source is Image image)
            return image;

        if (source is GameObject gameObject)
            return gameObject.GetComponent<Image>() ?? gameObject.GetComponentInChildren<Image>(true);

        if (source is Component component)
            return component.GetComponent<Image>() ?? component.GetComponentInChildren<Image>(true);

        return null;
    }

    static Material ResolveFallbackSvgMaterial(Sprite sprite)
    {
        bool hasTexture = sprite != null && sprite.texture != null;
        if (hasTexture)
        {
            if (_fallbackVectorGradientUiMaterial == null)
                _fallbackVectorGradientUiMaterial = CreateRuntimeMaterial("Unlit/VectorGradientUI");

            return _fallbackVectorGradientUiMaterial;
        }

        if (_fallbackVectorUiMaterial == null)
            _fallbackVectorUiMaterial = CreateRuntimeMaterial("Unlit/VectorUI");

        return _fallbackVectorUiMaterial;
    }

    static Material CreateRuntimeMaterial(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
            return null;

        Material material = new Material(shader);
        material.hideFlags = HideFlags.HideAndDontSave;
        return material;
    }

    static bool IsVectorMaterial(Material material)
    {
        return material != null &&
               material.shader != null &&
               material.shader.name.IndexOf("Vector", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsLikelyVectorSprite(Sprite sprite)
    {
        return sprite != null &&
               sprite.texture != null &&
               sprite.name.EndsWith("Sprite", System.StringComparison.OrdinalIgnoreCase) &&
               sprite.texture.name.EndsWith("Atlas", System.StringComparison.OrdinalIgnoreCase);
    }

    void ApplyNameExtraTexts(PreStorySetupFlow target)
    {
        if (target == null)
            return;

        int visibleCount = _nameExtraTexts != null ? _nameExtraTexts.Count : 0;
        target.HideNameExtraTextsFrom(visibleCount);

        for (int i = 0; i < visibleCount; i++)
        {
            StoryNameExtraTextStyle style = _nameExtraTexts[i];
            ApplyNameExtraText(target, i, style);
        }
    }

    static void ApplyNameExtraText(PreStorySetupFlow target, int index, StoryNameExtraTextStyle style)
    {
        if (target == null)
            return;

        if (style == null)
        {
            TMP_Text unusedText = target.GetNameExtraText(index);
            if (unusedText != null)
            {
                unusedText.text = "";
                unusedText.gameObject.SetActive(false);
            }
            return;
        }

        TMP_Text text = target.ResolveNameExtraText(index, style.TargetText, style.TargetPath, style.Enabled);
        if (text == null)
            return;

        text.gameObject.SetActive(style.Enabled);
        if (!style.Enabled)
        {
            text.text = "";
            return;
        }

        text.text = style.Text ?? "";
        ApplyRectOverrides(text.rectTransform, style.OverrideRect, style.AnchoredPosition, style.SizeDelta);
        ApplyHeightLimits(text.rectTransform, style.OverrideHeightLimits, style.MinHeight, style.MaxHeight);
        ApplyTextLayoutOverrides(
            text,
            style.OverrideColor,
            style.Color,
            style.OverrideFont,
            style.Font,
            style.OverrideFontSize,
            style.FontSize,
            style.OverrideAutoSize,
            style.AutoSize,
            style.OverrideAutoFontSizeRange,
            style.MinAutoFontSize,
            style.MaxAutoFontSize,
            style.OverrideAlignment,
            style.Alignment,
            style.OverrideWordWrapping,
            style.WordWrapping,
            style.OverrideOverflowMode,
            style.OverflowMode,
            style.OverrideLineSpacing,
            style.LineSpacing,
            style.OverrideMargins,
            style.Margins);
    }

    static void ApplyHeightLimits(RectTransform target, bool overrideHeightLimits, float minHeight, float maxHeight)
    {
        if (target == null || !overrideHeightLimits)
            return;

        Vector2 size = target.sizeDelta;
        float height = Mathf.Max(0f, size.y);
        minHeight = Mathf.Max(0f, minHeight);
        maxHeight = Mathf.Max(0f, maxHeight);
        if (maxHeight > 0f)
            height = Mathf.Clamp(height, minHeight, Mathf.Max(minHeight, maxHeight));
        else
            height = Mathf.Max(minHeight, height);

        target.sizeDelta = new Vector2(size.x, height);
    }

    static void ApplyTextOverrides(
        TMP_Text target,
        bool overrideColor,
        Color color,
        bool overrideFont,
        TMP_FontAsset font,
        bool overrideFontSize,
        float fontSize)
    {
        if (target == null)
            return;

        if (overrideColor)
            target.color = color;

        if (overrideFont && font != null)
            target.font = font;

        if (overrideFontSize)
        {
            float size = Mathf.Max(1f, fontSize);
            target.fontSize = size;
            if (target.enableAutoSizing && target.fontSizeMax > 0f)
                target.fontSizeMax = Mathf.Min(target.fontSizeMax, size);
        }

        target.SetAllDirty();
    }

    static void ApplyTextLayoutOverrides(
        TMP_Text target,
        bool overrideColor,
        Color color,
        bool overrideFont,
        TMP_FontAsset font,
        bool overrideFontSize,
        float fontSize,
        bool overrideAutoSize,
        bool autoSize,
        bool overrideAutoFontSizeRange,
        float minAutoFontSize,
        float maxAutoFontSize,
        bool overrideAlignment,
        TextAlignmentOptions alignment,
        bool overrideWordWrapping,
        bool wordWrapping,
        bool overrideOverflowMode,
        TextOverflowModes overflowMode,
        bool overrideLineSpacing,
        float lineSpacing,
        bool overrideMargins,
        Vector4 margins)
    {
        if (target == null)
            return;

        ApplyTextOverrides(target, overrideColor, color, overrideFont, font, overrideFontSize, fontSize);

        if (overrideAutoSize)
            target.enableAutoSizing = autoSize;

        if (overrideAutoFontSizeRange)
        {
            target.enableAutoSizing = true;
            target.fontSizeMin = Mathf.Max(1f, minAutoFontSize);
            target.fontSizeMax = Mathf.Max(target.fontSizeMin, maxAutoFontSize);
        }

        if (overrideAlignment)
            target.alignment = alignment;

        if (overrideWordWrapping)
            target.enableWordWrapping = wordWrapping;

        if (overrideOverflowMode)
            target.overflowMode = overflowMode;

        if (overrideLineSpacing)
            target.lineSpacing = lineSpacing;

        if (overrideMargins)
            target.margin = margins;

        target.SetAllDirty();
        if (target.rectTransform != null)
            LayoutRebuilder.MarkLayoutForRebuild(target.rectTransform);
    }

    static void ApplyRectOverrides(
        RectTransform target,
        bool overrideRect,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        if (target == null || !overrideRect)
            return;

        if (IsFullStretchRect(target))
        {
            target.offsetMin = new Vector2(anchoredPosition.x, anchoredPosition.y);
            target.offsetMax = new Vector2(-sizeDelta.x, -sizeDelta.y);
            return;
        }

        target.anchoredPosition = anchoredPosition;
        target.sizeDelta = sizeDelta;
    }

    static bool IsFullStretchRect(RectTransform target)
    {
        if (target == null)
            return false;

        return !Mathf.Approximately(target.anchorMin.x, target.anchorMax.x) &&
               !Mathf.Approximately(target.anchorMin.y, target.anchorMax.y);
    }

    static void ApplyStretchOffsetOverrides(
        RectTransform target,
        bool overrideOffsets,
        Vector4 offsets)
    {
        if (target == null || !overrideOffsets)
            return;

        target.offsetMin = new Vector2(offsets.x, offsets.w);
        target.offsetMax = new Vector2(-offsets.y, -offsets.z);
    }

    private void OnValidate()
    {
        _pixelsPerUnitMultiplier = Mathf.Max(0.01f, _pixelsPerUnitMultiplier);
        _bodyTextMinHeight = Mathf.Max(0f, _bodyTextMinHeight);
        _bodyTextMaxHeight = Mathf.Max(0f, _bodyTextMaxHeight);
        if (_overrideBodyTextMaxHeight && _bodyTextMaxHeight > 0f && _bodyTextMaxHeight < _bodyTextMinHeight)
            _bodyTextMaxHeight = _bodyTextMinHeight;
        _dialoguePanelAutoMinHeight = Mathf.Max(0f, _dialoguePanelAutoMinHeight);
        _dialoguePanelAutoMaxHeight = Mathf.Max(0f, _dialoguePanelAutoMaxHeight);
        if (_dialoguePanelAutoMaxHeight > 0f && _dialoguePanelAutoMaxHeight < _dialoguePanelAutoMinHeight)
            _dialoguePanelAutoMaxHeight = _dialoguePanelAutoMinHeight;
        _dialogueBackgroundAnchorMin = ClampVector2Range(_dialogueBackgroundAnchorMin, 0f, 1f);
        _dialogueBackgroundAnchorMax = ClampVector2Range(_dialogueBackgroundAnchorMax, 0f, 1f);
        _dialogueBackgroundAnchorMax = Vector2.Max(_dialogueBackgroundAnchorMin, _dialogueBackgroundAnchorMax);
        _dialogueBackgroundPivot = ClampVector2Range(_dialogueBackgroundPivot, 0f, 1f);
        _dialoguePanelVerticalLayoutPadding ??= new RectOffset();
        if (_dialogueExtraLayers == null)
            _dialogueExtraLayers = new List<DialoguePanelExtraLayerStyle>();
        for (int i = 0; i < _dialogueExtraLayers.Count; i++)
            _dialogueExtraLayers[i]?.Validate();
        _bodyTextMaxFontSize = Mathf.Max(0f, _bodyTextMaxFontSize);
        _bodyTextMinAutoFontSize = Mathf.Max(1f, _bodyTextMinAutoFontSize);
        if (_bodyTextMaxFontSize > 0f && _bodyTextMinAutoFontSize > _bodyTextMaxFontSize)
            _bodyTextMinAutoFontSize = _bodyTextMaxFontSize;
        _namePlatePixelsPerUnitMultiplier = Mathf.Max(0.01f, _namePlatePixelsPerUnitMultiplier);
        _characterNameFontSize = Mathf.Max(1f, _characterNameFontSize);
        _namePlateAnchorMin = ClampVector2Range(_namePlateAnchorMin, 0f, 1f);
        _namePlateAnchorMax = ClampVector2Range(_namePlateAnchorMax, 0f, 1f);
        _namePlateAnchorMax = Vector2.Max(_namePlateAnchorMin, _namePlateAnchorMax);
        _namePlatePivot = ClampVector2Range(_namePlatePivot, 0f, 1f);
        _choiceButtonFontSize = Mathf.Max(1f, _choiceButtonFontSize);
        _choiceButtonTextPadding ??= new RectOffset(48, 48, 18, 18);
        _nameInputTextFontSize = Mathf.Max(1f, _nameInputTextFontSize);
        _namePlaceholderTextFontSize = Mathf.Max(1f, _namePlaceholderTextFontSize);
        _nameConfirmButtonTextFontSize = Mathf.Max(1f, _nameConfirmButtonTextFontSize);
        _nameExtraTextOneFontSize = Mathf.Max(1f, _nameExtraTextOneFontSize);
        _nameExtraTextTwoFontSize = Mathf.Max(1f, _nameExtraTextTwoFontSize);
        if (_nameExtraTexts == null)
            _nameExtraTexts = new List<StoryNameExtraTextStyle>();
        for (int i = 0; i < _nameExtraTexts.Count; i++)
            _nameExtraTexts[i]?.Validate();
        _statTextFontSize = Mathf.Max(1f, _statTextFontSize);
        if (_statPanelSizeOverrides == null)
            _statPanelSizeOverrides = new List<StatPanelSizeOverride>();
        for (int i = 0; i < _statPanelSizeOverrides.Count; i++)
            _statPanelSizeOverrides[i]?.Validate();
        if (_statTextRectOverrides == null)
            _statTextRectOverrides = new List<StatTextRectOverride>();
        for (int i = 0; i < _statTextRectOverrides.Count; i++)
            _statTextRectOverrides[i]?.Validate();
        _statPanelBackgroundAnchorMin = ClampVector2Range(_statPanelBackgroundAnchorMin, 0f, 1f);
        _statPanelBackgroundAnchorMax = ClampVector2Range(_statPanelBackgroundAnchorMax, 0f, 1f);
        _statPanelBackgroundAnchorMax = Vector2.Max(_statPanelBackgroundAnchorMin, _statPanelBackgroundAnchorMax);
        _statPanelBackgroundPivot = ClampVector2Range(_statPanelBackgroundPivot, 0f, 1f);
        _statPanelVerticalLayoutPadding ??= new RectOffset();
        _relationshipPanelBackgroundAnchorMin = ClampVector2Range(_relationshipPanelBackgroundAnchorMin, 0f, 1f);
        _relationshipPanelBackgroundAnchorMax = ClampVector2Range(_relationshipPanelBackgroundAnchorMax, 0f, 1f);
        _relationshipPanelBackgroundAnchorMax = Vector2.Max(_relationshipPanelBackgroundAnchorMin, _relationshipPanelBackgroundAnchorMax);
        _relationshipPanelBackgroundPivot = ClampVector2Range(_relationshipPanelBackgroundPivot, 0f, 1f);
        _relationshipPanelVerticalLayoutPadding ??= new RectOffset();
        _statTextMinAutoFontSize = Mathf.Max(1f, _statTextMinAutoFontSize);
        _statTextMaxAutoFontSize = Mathf.Max(_statTextMinAutoFontSize, _statTextMaxAutoFontSize);
        _chapterTitleTextFontSize = Mathf.Max(1f, _chapterTitleTextFontSize);
        _chapterTitleTextMinHeight = Mathf.Max(0f, _chapterTitleTextMinHeight);
        _chapterTitleTextMaxHeight = Mathf.Max(0f, _chapterTitleTextMaxHeight);
        if (_overrideChapterTitleTextHeightLimits && _chapterTitleTextMaxHeight > 0f && _chapterTitleTextMaxHeight < _chapterTitleTextMinHeight)
            _chapterTitleTextMaxHeight = _chapterTitleTextMinHeight;
        _chapterTitleTextMinAutoFontSize = Mathf.Max(1f, _chapterTitleTextMinAutoFontSize);
        _chapterTitleTextMaxAutoFontSize = Mathf.Max(_chapterTitleTextMinAutoFontSize, _chapterTitleTextMaxAutoFontSize);
        _relationshipFontSizeMin = Mathf.Max(1f, _relationshipFontSizeMin);
        _relationshipFontSizeMax = Mathf.Max(_relationshipFontSizeMin, _relationshipFontSizeMax);
        _relationshipMaxVisibleLines = Mathf.Max(1, _relationshipMaxVisibleLines);
        if (_relationshipMessageOverrides == null)
            _relationshipMessageOverrides = new List<RelationshipMessageOverride>();
        for (int i = 0; i < _relationshipMessageOverrides.Count; i++)
            _relationshipMessageOverrides[i]?.Validate();
        _statIconParentPadding ??= new RectOffset();
        if (_statIconOffsetOverrides == null)
            _statIconOffsetOverrides = new List<StatIconOffsetOverride>();
        for (int i = 0; i < _statIconOffsetOverrides.Count; i++)
            _statIconOffsetOverrides[i]?.Validate();
        _chapterTitleBackgroundDimFixedSize = new Vector2(
            Mathf.Max(1f, _chapterTitleBackgroundDimFixedSize.x),
            Mathf.Max(1f, _chapterTitleBackgroundDimFixedSize.y));
        _chapterTitleBackgroundDimAlpha = Mathf.Clamp01(_chapterTitleBackgroundDimAlpha);
        _chapterTitleEnterDuration = Mathf.Max(0f, _chapterTitleEnterDuration);
        _chapterTitleVisibleDuration = Mathf.Max(0f, _chapterTitleVisibleDuration);
        _chapterTitleExitDuration = Mathf.Max(0f, _chapterTitleExitDuration);
        _endScreenStyle ??= new StoryEndScreenStyleSettings();
        _endScreenStyle.Validate();

        if (string.IsNullOrEmpty(_chapterTitleTextFormat))
            _chapterTitleTextFormat = "{1}";
        if (string.IsNullOrEmpty(_chapterTitleNumberAndTitleFormat))
            _chapterTitleNumberAndTitleFormat = "ГЛАВА {0}: {1}";
        if (string.IsNullOrEmpty(_chapterTitleEmptyTitleFallback))
            _chapterTitleEmptyTitleFallback = "ГЛАВА {0}";
    }

    static Vector2 ClampVector2Range(Vector2 value, float min, float max)
    {
        return new Vector2(Mathf.Clamp(value.x, min, max), Mathf.Clamp(value.y, min, max));
    }
}
