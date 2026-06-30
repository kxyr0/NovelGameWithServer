using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class DialogueChoiceLayout : MonoBehaviour
{
    private const float SizeEpsilon = 0.5f;

    [Serializable]
    private sealed class ChoiceButtonReference
    {
        public ButtonTextAutoSize autoSize;
        public TMP_Text text;
        public RectTransform buttonRect;
        public LayoutElement layoutElement;
        public Image buttonImage;
    }

    private readonly struct ChoiceTarget
    {
        public readonly ButtonTextAutoSize AutoSize;
        public readonly TMP_Text Text;
        public readonly RectTransform ButtonRect;
        public readonly LayoutElement LayoutElement;
        public readonly Image ButtonImage;

        public ChoiceTarget(
            ButtonTextAutoSize autoSize,
            TMP_Text text,
            RectTransform buttonRect,
            LayoutElement layoutElement,
            Image buttonImage)
        {
            AutoSize = autoSize;
            Text = text;
            ButtonRect = buttonRect;
            LayoutElement = layoutElement;
            ButtonImage = buttonImage;
        }

        public bool IsValid => Text != null && ButtonRect != null;
    }

    private sealed class TextState
    {
        public bool HasState;
        public bool WordWrapping;
        public bool AutoSizing;
        public float FontSize;
        public float FontSizeMin;
        public float FontSizeMax;
        public TextOverflowModes OverflowMode;

        public void Capture(TMP_Text text)
        {
            if (HasState || text == null)
                return;

            WordWrapping = text.enableWordWrapping;
            AutoSizing = text.enableAutoSizing;
            FontSize = text.fontSize;
            FontSizeMin = text.fontSizeMin;
            FontSizeMax = text.fontSizeMax;
            OverflowMode = text.overflowMode;
            HasState = true;
        }

        public void Restore(TMP_Text text)
        {
            if (!HasState || text == null)
                return;

            text.enableWordWrapping = WordWrapping;
            text.enableAutoSizing = AutoSizing;
            if (!AutoSizing)
                text.fontSize = Mathf.Max(1f, FontSize);

            text.fontSizeMin = FontSizeMin;
            text.fontSizeMax = FontSizeMax;
            text.overflowMode = OverflowMode;
        }
    }

    private readonly struct ButtonMetrics
    {
        public readonly float TextWidth;
        public readonly float TextHeight;

        public ButtonMetrics(float textWidth, float textHeight)
        {
            TextWidth = textWidth;
            TextHeight = textHeight;
        }
    }

    [Header("Кнопки")]
    [Tooltip("ButtonTextAutoSize компоненты, которыми управляет этот layout. Дочерние кнопки можно собирать автоматически.")]
    [SerializeField] private List<ButtonTextAutoSize> _buttons = new List<ButtonTextAutoSize>();

    [Tooltip("Ручные ссылки для кнопок без ButtonTextAutoSize.")]
    [SerializeField] private List<ChoiceButtonReference> _manualButtons = new List<ChoiceButtonReference>();

    [Tooltip("Автоматически брать ButtonTextAutoSize из дочерних объектов.")]
    [SerializeField] private bool _collectChildButtonTextAutoSizes = true;

    [Tooltip("Автоматически брать дочерние TMP_Text и искать для них RectTransform кнопки выше по иерархии.")]
    [SerializeField] private bool _collectChildTextButtons;

    [Tooltip("Учитывать выключенные дочерние кнопки при сборе.")]
    [SerializeField] private bool _includeInactiveChildren = true;

    [Header("Ширина")]
    [SerializeField] private bool _sameWidthForAll = true;
    [SerializeField] private float _horizontalPadding = 96f;
    [SerializeField] private float _minButtonWidth;
    [SerializeField] private float _maxButtonWidth = 900f;

    [Header("Высота")]
    [SerializeField] private float _verticalPadding = 44f;
    [SerializeField] private float _minButtonHeight = 56f;
    [SerializeField] private float _maxButtonHeight;

    [Header("Внутренние отступы текста")]
    [Tooltip("Минимальный визуальный отступ текста от рамки кнопки. Если сумма меньше Horizontal/Vertical Padding, недостающий отступ распределяется поровну.")]
    [SerializeField] private RectOffset _textPadding;
    [Tooltip("Дополнительный сдвиг TMP_Text внутри кнопки. Для Y отрицательное значение опускает текст ниже.")]
    [SerializeField] private Vector2 _bodyTextOffset = new Vector2(0f, -4f);

    [Header("Шрифт")]
    [Tooltip("Управлять размером шрифта всех кнопок выбора из этого layout. Если выключено, используется размер из TMP_Text/стиля истории.")]
    [SerializeField] private bool _overrideFontSize = true;
    [Tooltip("Базовый размер шрифта вариантов. Размер кнопок пересчитывается уже с этим значением.")]
    [SerializeField] private float _fontSize = 48f;
    [Tooltip("Максимальный размер TMP Auto Size, когда текст пришлось ужимать. 0 означает использовать базовый размер шрифта.")]
    [SerializeField] private float _maxAutoFontSize = 0f;

    [Header("Подгонка текста")]
    [SerializeField] private bool _enableWrappingWhenConstrained = true;
    [SerializeField] private bool _enableAutoSizeWhenConstrained = true;
    [SerializeField] private float _minAutoFontSize = 30f;
    [SerializeField] private TextOverflowModes _overflowModeWhenConstrained = TextOverflowModes.Ellipsis;
    [SerializeField] private float _measurementPadding = 2f;

    [Header("Применение")]
    [SerializeField] private bool _callButtonTextAutoSize = true;
    [SerializeField] private bool _disableButtonAutoRefreshWhileManaged = true;
    [SerializeField] private bool _writeLayoutElement = true;
    [SerializeField] private bool _createMissingLayoutElement = true;
    [SerializeField] private bool _zeroFlexibleSize = true;
    [SerializeField] private bool _driveButtonRectSize = true;
    [SerializeField] private bool _driveTextRectToContentArea = true;
    [SerializeField] private bool _centerTextRect = true;
    [SerializeField] private bool _stretchImageToCalculatedSize = true;
    [SerializeField] private bool _useSlicedImageWhenPossible = true;

    [Header("Обновление")]
    [SerializeField] private bool _refreshOnEnable = true;
    [SerializeField] private bool _refreshInLateUpdate = true;
    [SerializeField] private bool _refreshBeforeCanvasRender = true;
    [SerializeField] private bool _forceCanvasUpdateBeforeMeasure = true;
    [SerializeField] private bool _forceParentLayoutRebuild = true;

    private List<ChoiceTarget> _targets = new List<ChoiceTarget>();
    private List<ButtonMetrics> _metrics = new List<ButtonMetrics>();
    private List<string> _runtimeChoices = new List<string>();
    private Dictionary<TMP_Text, TextState> _textStates = new Dictionary<TMP_Text, TextState>();
    private Dictionary<ButtonTextAutoSize, bool> _buttonAutoRefreshSuspendedStates = new Dictionary<ButtonTextAutoSize, bool>();
    private List<string> _lastTexts = new List<string>();
    private List<float> _lastFontSizes = new List<float>();
    private List<Vector2> _lastButtonSizes = new List<Vector2>();
    private List<Vector2> _lastTextRectSizes = new List<Vector2>();

    private bool _hasRuntimeChoices;
    private bool _dirty = true;
    private bool _isRefreshing;
    private bool _isHandlingWillRenderCanvases;

    public bool SameWidthForAll
    {
        get => _sameWidthForAll;
        set
        {
            if (_sameWidthForAll == value)
                return;

            _sameWidthForAll = value;
            MarkDirty();
        }
    }

    public void SetPadding(Vector2 padding)
    {
        SetPadding(padding.x, padding.y);
    }

    public void SetPadding(float horizontalPadding, float verticalPadding)
    {
        float nextHorizontal = Mathf.Max(0f, horizontalPadding);
        float nextVertical = Mathf.Max(0f, verticalPadding);

        if (Mathf.Approximately(_horizontalPadding, nextHorizontal) &&
            Mathf.Approximately(_verticalPadding, nextVertical))
        {
            return;
        }

        _horizontalPadding = nextHorizontal;
        _verticalPadding = nextVertical;
        MarkDirty();
        RefreshNow();
    }

    public void SetTextPadding(RectOffset padding)
    {
        SanitizeTextPadding();

        int nextLeft = padding != null ? Mathf.Max(0, padding.left) : 0;
        int nextRight = padding != null ? Mathf.Max(0, padding.right) : 0;
        int nextTop = padding != null ? Mathf.Max(0, padding.top) : 0;
        int nextBottom = padding != null ? Mathf.Max(0, padding.bottom) : 0;

        if (_textPadding.left == nextLeft &&
            _textPadding.right == nextRight &&
            _textPadding.top == nextTop &&
            _textPadding.bottom == nextBottom)
        {
            return;
        }

        _textPadding.left = nextLeft;
        _textPadding.right = nextRight;
        _textPadding.top = nextTop;
        _textPadding.bottom = nextBottom;
        MarkDirty();
        RefreshNow();
    }

    public void SetTextOffset(Vector2 offset)
    {
        if ((_bodyTextOffset - offset).sqrMagnitude <= SizeEpsilon * SizeEpsilon)
            return;

        _bodyTextOffset = offset;
        MarkDirty();
        RefreshNow();
    }

    public void SetFontSize(float fontSize)
    {
        float nextFontSize = Mathf.Max(1f, fontSize);
        bool wasOverridingFontSize = _overrideFontSize;
        _overrideFontSize = true;

        if (wasOverridingFontSize && Mathf.Approximately(_fontSize, nextFontSize))
            return;

        _fontSize = nextFontSize;
        if (_maxAutoFontSize > 0f && _maxAutoFontSize < _fontSize)
            _maxAutoFontSize = _fontSize;

        MarkDirty();
        RefreshNow();
    }

    public void RefreshNow()
    {
        EnsureCollections();

        if (_isRefreshing)
            return;

        _isRefreshing = true;
        try
        {
            BuildTargets();
            ApplyRuntimeChoices();

            if (_targets.Count == 0)
            {
                RememberCurrentState();
                _dirty = false;
                return;
            }

            ConfigureManagedButtonAutoRefresh();
            PrepareAutoSizeButtons();
            ForceTextAndCanvasUpdate();
            MeasureUnwrappedTexts();

            float maxTextWidth = FindMaxTextWidth();
            float sharedWidth = ResolveTargetWidth(maxTextWidth);

            for (int i = 0; i < _targets.Count; i++)
            {
                ChoiceTarget target = _targets[i];
                if (!target.IsValid)
                    continue;

                float targetWidth = _sameWidthForAll
                    ? sharedWidth
                    : ResolveTargetWidth(_metrics[i].TextWidth);

                ApplyTarget(target, targetWidth);
            }

            MarkLayoutsDirty();
            RememberCurrentState();
            _dirty = false;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public void SetChoices(IReadOnlyList<string> choices)
    {
        EnsureCollections();
        _runtimeChoices.Clear();

        if (choices == null)
        {
            _hasRuntimeChoices = false;
            MarkDirty();
            RefreshNow();
            return;
        }

        for (int i = 0; i < choices.Count; i++)
            _runtimeChoices.Add(choices[i] ?? string.Empty);

        _hasRuntimeChoices = true;
        MarkDirty();
        RefreshNow();
    }

    public void RegisterButton(ButtonTextAutoSize button)
    {
        EnsureCollections();

        if (button == null)
            return;

        if (!_buttons.Contains(button))
            _buttons.Add(button);

        MarkDirty();
        RefreshNow();
    }

    public void ClearButtons()
    {
        EnsureCollections();
        _buttons.Clear();
        _manualButtons.Clear();
        _targets.Clear();
        _metrics.Clear();
        _textStates.Clear();
        MarkDirty();
    }

    private void Reset()
    {
        _collectChildButtonTextAutoSizes = true;
        _includeInactiveChildren = true;
        MarkDirty();
    }

    private void OnEnable()
    {
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        Canvas.willRenderCanvases += HandleWillRenderCanvases;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(HandleTextChanged);

        MarkDirty();
        if (_refreshOnEnable)
            RefreshNow();
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(HandleTextChanged);
        RestoreManagedButtonAutoRefresh();
    }

    private void OnValidate()
    {
        _horizontalPadding = Mathf.Max(0f, _horizontalPadding);
        _verticalPadding = Mathf.Max(0f, _verticalPadding);
        _minButtonWidth = Mathf.Max(0f, _minButtonWidth);
        _maxButtonWidth = Mathf.Max(0f, _maxButtonWidth);
        _minButtonHeight = Mathf.Max(0f, _minButtonHeight);
        _maxButtonHeight = Mathf.Max(0f, _maxButtonHeight);
        _fontSize = Mathf.Max(1f, _fontSize);
        _maxAutoFontSize = Mathf.Max(0f, _maxAutoFontSize);
        _minAutoFontSize = Mathf.Max(1f, _minAutoFontSize);
        _measurementPadding = Mathf.Max(0f, _measurementPadding);
        SanitizeTextPadding();

        if (_maxButtonWidth > 0f && _maxButtonWidth < _minButtonWidth)
            _maxButtonWidth = _minButtonWidth;

        if (_maxButtonHeight > 0f && _maxButtonHeight < _minButtonHeight)
            _maxButtonHeight = _minButtonHeight;

        float autoMax = ResolveConfiguredAutoMaxFontSize();
        if (_minAutoFontSize > autoMax)
            _minAutoFontSize = autoMax;

        MarkDirty();
    }

    private void LateUpdate()
    {
        if (!_refreshInLateUpdate || _isRefreshing || !isActiveAndEnabled)
            return;

        if (_dirty || HasRelevantStateChanged())
            RefreshNow();
    }

    private void OnTransformChildrenChanged()
    {
        MarkDirty();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!_isRefreshing)
            MarkDirty();
    }

    private void HandleWillRenderCanvases()
    {
        if (!_refreshBeforeCanvasRender || _isRefreshing || !isActiveAndEnabled)
            return;

        if (!_dirty && !HasRelevantStateChanged())
            return;

        _isHandlingWillRenderCanvases = true;
        try
        {
            RefreshNow();
        }
        finally
        {
            _isHandlingWillRenderCanvases = false;
        }
    }

    private void HandleTextChanged(UnityEngine.Object changedObject)
    {
        TMP_Text changedText = changedObject as TMP_Text;
        if (changedText == null)
            return;

        BuildTargets();
        for (int i = 0; i < _targets.Count; i++)
        {
            if (_targets[i].Text == changedText)
            {
                MarkDirty();
                return;
            }
        }
    }

    private void MarkDirty()
    {
        _dirty = true;
    }

    private void BuildTargets()
    {
        EnsureCollections();
        _targets.Clear();
        var usedRects = new HashSet<RectTransform>();

        for (int i = 0; i < _buttons.Count; i++)
            AddAutoSizeTarget(_buttons[i], usedRects);

        for (int i = 0; i < _manualButtons.Count; i++)
            AddManualTarget(_manualButtons[i], usedRects);

        if (_collectChildButtonTextAutoSizes)
        {
            ButtonTextAutoSize[] childButtons = GetComponentsInChildren<ButtonTextAutoSize>(_includeInactiveChildren);
            for (int i = 0; i < childButtons.Length; i++)
                AddAutoSizeTarget(childButtons[i], usedRects);
        }

        if (_collectChildTextButtons)
        {
            TMP_Text[] childTexts = GetComponentsInChildren<TMP_Text>(_includeInactiveChildren);
            for (int i = 0; i < childTexts.Length; i++)
                AddTextTarget(childTexts[i], usedRects);
        }
    }

    private void EnsureCollections()
    {
        if (_buttons == null)
            _buttons = new List<ButtonTextAutoSize>();

        if (_manualButtons == null)
            _manualButtons = new List<ChoiceButtonReference>();

        if (_targets == null)
            _targets = new List<ChoiceTarget>();

        if (_metrics == null)
            _metrics = new List<ButtonMetrics>();

        if (_runtimeChoices == null)
            _runtimeChoices = new List<string>();

        if (_textStates == null)
            _textStates = new Dictionary<TMP_Text, TextState>();

        if (_buttonAutoRefreshSuspendedStates == null)
            _buttonAutoRefreshSuspendedStates = new Dictionary<ButtonTextAutoSize, bool>();

        if (_lastTexts == null)
            _lastTexts = new List<string>();

        if (_lastFontSizes == null)
            _lastFontSizes = new List<float>();

        if (_lastButtonSizes == null)
            _lastButtonSizes = new List<Vector2>();

        if (_lastTextRectSizes == null)
            _lastTextRectSizes = new List<Vector2>();
    }

    private void AddAutoSizeTarget(ButtonTextAutoSize autoSize, HashSet<RectTransform> usedRects)
    {
        if (autoSize == null)
            return;

        TMP_Text text = autoSize.Text != null
            ? autoSize.Text
            : autoSize.GetComponentInChildren<TMP_Text>(_includeInactiveChildren);

        RectTransform buttonRect = autoSize.ButtonRect != null
            ? autoSize.ButtonRect
            : autoSize.GetComponent<RectTransform>();

        if (text == null || buttonRect == null || !usedRects.Add(buttonRect))
            return;

        LayoutElement layoutElement = buttonRect.GetComponent<LayoutElement>();
        Image image = buttonRect.GetComponent<Image>();
        _targets.Add(new ChoiceTarget(autoSize, text, buttonRect, layoutElement, image));
    }

    private void AddManualTarget(ChoiceButtonReference reference, HashSet<RectTransform> usedRects)
    {
        if (reference == null)
            return;

        if (reference.autoSize != null)
        {
            AddAutoSizeTarget(reference.autoSize, usedRects);
            return;
        }

        TMP_Text text = reference.text;
        RectTransform buttonRect = reference.buttonRect;

        if (text == null && buttonRect != null)
            text = buttonRect.GetComponentInChildren<TMP_Text>(_includeInactiveChildren);

        if (buttonRect == null && text != null)
            buttonRect = FindButtonRectForText(text);

        if (text == null || buttonRect == null || !usedRects.Add(buttonRect))
            return;

        LayoutElement layoutElement = reference.layoutElement != null
            ? reference.layoutElement
            : buttonRect.GetComponent<LayoutElement>();

        Image image = reference.buttonImage != null
            ? reference.buttonImage
            : buttonRect.GetComponent<Image>();

        _targets.Add(new ChoiceTarget(null, text, buttonRect, layoutElement, image));
    }

    private void AddTextTarget(TMP_Text text, HashSet<RectTransform> usedRects)
    {
        if (text == null)
            return;

        RectTransform buttonRect = FindButtonRectForText(text);
        if (buttonRect == null || !usedRects.Add(buttonRect))
            return;

        LayoutElement layoutElement = buttonRect.GetComponent<LayoutElement>();
        Image image = buttonRect.GetComponent<Image>();
        _targets.Add(new ChoiceTarget(null, text, buttonRect, layoutElement, image));
    }

    private RectTransform FindButtonRectForText(TMP_Text text)
    {
        if (text == null)
            return null;

        Button button = text.GetComponentInParent<Button>(_includeInactiveChildren);
        if (button != null)
            return button.transform as RectTransform;

        ButtonTextAutoSize autoSize = text.GetComponentInParent<ButtonTextAutoSize>(_includeInactiveChildren);
        if (autoSize != null && autoSize.ButtonRect != null)
            return autoSize.ButtonRect;

        RectTransform textRect = text.rectTransform;
        Transform parent = textRect != null ? textRect.parent : null;
        return parent as RectTransform;
    }

    private void ApplyRuntimeChoices()
    {
        if (!_hasRuntimeChoices)
            return;

        int count = Mathf.Min(_runtimeChoices.Count, _targets.Count);
        for (int i = 0; i < count; i++)
        {
            TMP_Text text = _targets[i].Text;
            if (text != null && text.text != _runtimeChoices[i])
                text.text = _runtimeChoices[i];
        }
    }

    private void PrepareAutoSizeButtons()
    {
        if (!_callButtonTextAutoSize)
            return;

        for (int i = 0; i < _targets.Count; i++)
        {
            ButtonTextAutoSize autoSize = _targets[i].AutoSize;
            if (autoSize == null)
                continue;

            autoSize.SetPadding(GetHorizontalPadding(), GetVerticalPadding());
            autoSize.RefreshNow();
        }
    }

    private void ConfigureManagedButtonAutoRefresh()
    {
        if (!_disableButtonAutoRefreshWhileManaged)
        {
            RestoreManagedButtonAutoRefresh();
            return;
        }

        var activeButtons = new HashSet<ButtonTextAutoSize>();
        for (int i = 0; i < _targets.Count; i++)
        {
            ButtonTextAutoSize autoSize = _targets[i].AutoSize;
            if (autoSize == null)
                continue;

            activeButtons.Add(autoSize);
            if (!_buttonAutoRefreshSuspendedStates.ContainsKey(autoSize))
                _buttonAutoRefreshSuspendedStates.Add(autoSize, autoSize.AutoRefreshSuspended);

            autoSize.AutoRefreshSuspended = true;
        }

        RemoveStaleManagedButtonStates(activeButtons);
    }

    private void RemoveStaleManagedButtonStates(HashSet<ButtonTextAutoSize> activeButtons)
    {
        if (_buttonAutoRefreshSuspendedStates.Count == 0)
            return;

        var staleButtons = new List<ButtonTextAutoSize>();
        foreach (KeyValuePair<ButtonTextAutoSize, bool> pair in _buttonAutoRefreshSuspendedStates)
        {
            if (pair.Key == null || !activeButtons.Contains(pair.Key))
                staleButtons.Add(pair.Key);
        }

        for (int i = 0; i < staleButtons.Count; i++)
        {
            ButtonTextAutoSize autoSize = staleButtons[i];
            if (autoSize != null)
                autoSize.AutoRefreshSuspended = _buttonAutoRefreshSuspendedStates[autoSize];

            _buttonAutoRefreshSuspendedStates.Remove(autoSize);
        }
    }

    private void RestoreManagedButtonAutoRefresh()
    {
        EnsureCollections();

        if (_buttonAutoRefreshSuspendedStates.Count == 0)
            return;

        foreach (KeyValuePair<ButtonTextAutoSize, bool> pair in _buttonAutoRefreshSuspendedStates)
        {
            if (pair.Key != null)
                pair.Key.AutoRefreshSuspended = pair.Value;
        }

        _buttonAutoRefreshSuspendedStates.Clear();
    }

    private void ForceTextAndCanvasUpdate()
    {
        if (_forceCanvasUpdateBeforeMeasure && !_isHandlingWillRenderCanvases)
            Canvas.ForceUpdateCanvases();

        for (int i = 0; i < _targets.Count; i++)
        {
            TMP_Text text = _targets[i].Text;
            if (text == null)
                continue;

            GetTextState(text).Restore(text);
            ApplyBaseFontSettings(text);
            text.SetAllDirty();
            text.ForceMeshUpdate(true, true);
        }
    }

    private void MeasureUnwrappedTexts()
    {
        _metrics.Clear();

        for (int i = 0; i < _targets.Count; i++)
        {
            TMP_Text text = _targets[i].Text;
            if (text == null)
            {
                _metrics.Add(new ButtonMetrics(0f, 0f));
                continue;
            }

            string value = text.text ?? string.Empty;
            Vector2 preferred = text.GetPreferredValues(value, Mathf.Infinity, Mathf.Infinity);
            _metrics.Add(new ButtonMetrics(
                Mathf.Max(0f, preferred.x) + _measurementPadding,
                Mathf.Max(0f, preferred.y) + _measurementPadding));
        }
    }

    private float FindMaxTextWidth()
    {
        float max = 0f;
        for (int i = 0; i < _metrics.Count; i++)
            max = Mathf.Max(max, _metrics[i].TextWidth);
        return max;
    }

    private void ApplyTarget(ChoiceTarget target, float targetWidth)
    {
        TMP_Text text = target.Text;
        RectTransform buttonRect = target.ButtonRect;
        if (text == null || buttonRect == null)
            return;

        TextState state = GetTextState(text);
        state.Restore(text);
        ApplyBaseFontSettings(text);

        float contentWidth = GetContentWidth(targetWidth);
        bool needsWrapping = _enableWrappingWhenConstrained &&
                             IsTextTooWideForButton(text, contentWidth);

        text.enableWordWrapping = state.WordWrapping || needsWrapping;

        Vector2 preferred = MeasureTextForButton(text, contentWidth, text.enableWordWrapping);
        float targetHeight = ClampAxis(preferred.y + GetVerticalPadding(), _minButtonHeight, _maxButtonHeight);
        float contentHeight = GetContentHeight(targetHeight);

        ApplyVisualImage(target.ButtonImage);
        ApplyButtonSize(buttonRect, targetWidth, targetHeight);
        ApplyTextRect(text, contentWidth, contentHeight);

        if (TextExceedsFrame(text, contentWidth, contentHeight))
            ApplyAutoSizeIfNeeded(text, contentWidth, contentHeight, state);

        if (TextExceedsFrame(text, contentWidth, contentHeight))
            text.overflowMode = _overflowModeWhenConstrained;

        ApplyLayoutElement(target, targetWidth, targetHeight);
        LayoutRebuilder.MarkLayoutForRebuild(buttonRect);
    }

    private void ApplyBaseFontSettings(TMP_Text text)
    {
        if (!_overrideFontSize || text == null)
            return;

        float size = Mathf.Max(1f, _fontSize);
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.fontSizeMax = ResolveConfiguredAutoMaxFontSize();
        text.fontSizeMin = Mathf.Min(Mathf.Max(1f, _minAutoFontSize), text.fontSizeMax);
    }

    private Vector2 MeasureTextForButton(TMP_Text text, float contentWidth, bool wrapping)
    {
        if (text == null)
            return Vector2.zero;

        string value = text.text ?? string.Empty;
        float widthConstraint = wrapping ? Mathf.Max(1f, contentWidth) : Mathf.Infinity;
        Vector2 preferred = text.GetPreferredValues(value, widthConstraint, Mathf.Infinity);

        return new Vector2(
            Mathf.Max(0f, preferred.x) + _measurementPadding,
            Mathf.Max(0f, preferred.y) + _measurementPadding);
    }

    private bool IsTextTooWideForButton(TMP_Text text, float contentWidth)
    {
        if (text == null)
            return false;

        string value = text.text ?? string.Empty;
        Vector2 preferred = text.GetPreferredValues(value, Mathf.Infinity, Mathf.Infinity);
        return preferred.x + _measurementPadding > contentWidth + SizeEpsilon;
    }

    private void ApplyButtonSize(RectTransform buttonRect, float width, float height)
    {
        if (!_driveButtonRectSize || buttonRect == null)
            return;

        SetSizeIfChanged(buttonRect, RectTransform.Axis.Horizontal, width);
        SetSizeIfChanged(buttonRect, RectTransform.Axis.Vertical, height);
    }

    private void ApplyTextRect(TMP_Text text, float width, float height)
    {
        if (!_driveTextRectToContentArea || text == null)
            return;

        RectTransform textRect = text.rectTransform;
        if (textRect == null)
            return;

        SetSizeIfChanged(textRect, RectTransform.Axis.Horizontal, width);
        SetSizeIfChanged(textRect, RectTransform.Axis.Vertical, height);

        Vector2 targetPosition = GetTextAnchoredPosition();
        if (_centerTextRect && (textRect.anchoredPosition - targetPosition).sqrMagnitude > SizeEpsilon * SizeEpsilon)
            textRect.anchoredPosition = targetPosition;

        LayoutRebuilder.MarkLayoutForRebuild(textRect);
    }

    private void ApplyAutoSizeIfNeeded(TMP_Text text, float contentWidth, float contentHeight, TextState state)
    {
        if (!_enableAutoSizeWhenConstrained || text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(1f, _minAutoFontSize);
        text.fontSizeMax = ResolveAutoSizeMax(text, state);
        text.overflowMode = state.OverflowMode;
        text.ForceMeshUpdate(true, true);

        if (TextExceedsFrame(text, contentWidth, contentHeight))
            text.SetAllDirty();
    }

    private float ResolveAutoSizeMax(TMP_Text text, TextState state)
    {
        if (_overrideFontSize)
            return ResolveConfiguredAutoMaxFontSize();

        float max = text != null ? Mathf.Max(1f, text.fontSize) : 1f;
        if (state != null && state.HasState)
            max = Mathf.Max(max, state.FontSize, state.FontSizeMax);
        return Mathf.Max(max, _minAutoFontSize);
    }

    private float ResolveConfiguredAutoMaxFontSize()
    {
        float baseSize = Mathf.Max(1f, _fontSize);
        float configuredMax = _maxAutoFontSize > 0f ? _maxAutoFontSize : baseSize;
        return Mathf.Max(baseSize, configuredMax, _minAutoFontSize);
    }

    private bool TextExceedsFrame(TMP_Text text, float contentWidth, float contentHeight)
    {
        if (text == null)
            return false;

        text.ForceMeshUpdate(true, true);
        Vector2 rendered = text.GetRenderedValues(false);

        return rendered.x + _measurementPadding > contentWidth + SizeEpsilon ||
               rendered.y + _measurementPadding > contentHeight + SizeEpsilon ||
               text.isTextOverflowing;
    }

    private void ApplyLayoutElement(ChoiceTarget target, float width, float height)
    {
        if (!_writeLayoutElement || target.ButtonRect == null)
            return;

        LayoutElement layoutElement = target.LayoutElement;
        if (layoutElement == null && _createMissingLayoutElement)
            layoutElement = target.ButtonRect.GetComponent<LayoutElement>() ?? target.ButtonRect.gameObject.AddComponent<LayoutElement>();

        if (layoutElement == null)
            return;

        layoutElement.ignoreLayout = false;
        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;

        if (_zeroFlexibleSize)
        {
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
        }
    }

    private void ApplyVisualImage(Image image)
    {
        if (image == null)
            return;

        if (_stretchImageToCalculatedSize)
            image.preserveAspect = false;

        Sprite sprite = image.sprite;
        if (_useSlicedImageWhenPossible && sprite != null && sprite.border.sqrMagnitude > 0f)
            image.type = Image.Type.Sliced;
    }

    private TextState GetTextState(TMP_Text text)
    {
        EnsureCollections();

        if (text == null)
            return null;

        if (!_textStates.TryGetValue(text, out TextState state))
        {
            state = new TextState();
            _textStates.Add(text, state);
        }

        state.Capture(text);
        return state;
    }

    private float ResolveTargetWidth(float preferredTextWidth)
    {
        return ClampAxis(preferredTextWidth + GetHorizontalPadding(), _minButtonWidth, _maxButtonWidth);
    }

    private float GetContentWidth(float buttonWidth)
    {
        return Mathf.Max(1f, buttonWidth - GetHorizontalPadding());
    }

    private float GetContentHeight(float buttonHeight)
    {
        return Mathf.Max(1f, buttonHeight - GetVerticalPadding());
    }

    private float GetHorizontalPadding()
    {
        SanitizeTextPadding();
        return Mathf.Max(_horizontalPadding, _textPadding.left + _textPadding.right);
    }

    private float GetVerticalPadding()
    {
        SanitizeTextPadding();
        return Mathf.Max(_verticalPadding, _textPadding.top + _textPadding.bottom);
    }

    private Vector2 GetTextAnchoredPosition()
    {
        float horizontalExtra = Mathf.Max(0f, GetHorizontalPadding() - (_textPadding.left + _textPadding.right));
        float verticalExtra = Mathf.Max(0f, GetVerticalPadding() - (_textPadding.top + _textPadding.bottom));
        float left = _textPadding.left + horizontalExtra * 0.5f;
        float right = _textPadding.right + horizontalExtra * 0.5f;
        float top = _textPadding.top + verticalExtra * 0.5f;
        float bottom = _textPadding.bottom + verticalExtra * 0.5f;

        return new Vector2((left - right) * 0.5f, (bottom - top) * 0.5f) + _bodyTextOffset;
    }

    private void SanitizeTextPadding()
    {
        if (_textPadding == null)
            _textPadding = new RectOffset(48, 48, 18, 18);

        _textPadding.left = Mathf.Max(0, _textPadding.left);
        _textPadding.right = Mathf.Max(0, _textPadding.right);
        _textPadding.top = Mathf.Max(0, _textPadding.top);
        _textPadding.bottom = Mathf.Max(0, _textPadding.bottom);
    }

    private void MarkLayoutsDirty()
    {
        RectTransform ownRect = transform as RectTransform;
        if (ownRect != null)
            LayoutRebuilder.MarkLayoutForRebuild(ownRect);

        RectTransform parent = transform.parent as RectTransform;
        if (parent != null)
        {
            LayoutRebuilder.MarkLayoutForRebuild(parent);
            if (_forceParentLayoutRebuild)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }

        if (ownRect != null && _forceParentLayoutRebuild)
            LayoutRebuilder.ForceRebuildLayoutImmediate(ownRect);
    }

    private bool HasRelevantStateChanged()
    {
        BuildTargets();

        if (_targets.Count != _lastTexts.Count)
            return true;

        for (int i = 0; i < _targets.Count; i++)
        {
            TMP_Text text = _targets[i].Text;
            RectTransform buttonRect = _targets[i].ButtonRect;
            RectTransform textRect = text != null ? text.rectTransform : null;

            string currentText = text != null ? text.text : string.Empty;
            float fontSize = text != null ? text.fontSize : 0f;
            Vector2 buttonSize = buttonRect != null ? buttonRect.rect.size : Vector2.zero;
            Vector2 textSize = textRect != null ? textRect.rect.size : Vector2.zero;

            if (_lastTexts[i] != currentText ||
                !Mathf.Approximately(_lastFontSizes[i], fontSize) ||
                _lastButtonSizes[i] != buttonSize ||
                _lastTextRectSizes[i] != textSize)
            {
                return true;
            }
        }

        return false;
    }

    private void RememberCurrentState()
    {
        EnsureCollections();

        _lastTexts.Clear();
        _lastFontSizes.Clear();
        _lastButtonSizes.Clear();
        _lastTextRectSizes.Clear();

        for (int i = 0; i < _targets.Count; i++)
        {
            TMP_Text text = _targets[i].Text;
            RectTransform buttonRect = _targets[i].ButtonRect;
            RectTransform textRect = text != null ? text.rectTransform : null;

            _lastTexts.Add(text != null ? text.text : string.Empty);
            _lastFontSizes.Add(text != null ? text.fontSize : 0f);
            _lastButtonSizes.Add(buttonRect != null ? buttonRect.rect.size : Vector2.zero);
            _lastTextRectSizes.Add(textRect != null ? textRect.rect.size : Vector2.zero);
        }
    }

    private static float ClampAxis(float value, float min, float max)
    {
        if (min > 0f)
            value = Mathf.Max(value, min);

        if (max > 0f)
            value = Mathf.Min(value, max);

        return Mathf.Max(1f, value);
    }

    private static void SetSizeIfChanged(RectTransform rectTransform, RectTransform.Axis axis, float value)
    {
        if (rectTransform == null)
            return;

        float current = axis == RectTransform.Axis.Horizontal
            ? rectTransform.rect.width
            : rectTransform.rect.height;

        if (Mathf.Abs(current - value) <= SizeEpsilon)
            return;

        rectTransform.SetSizeWithCurrentAnchors(axis, Mathf.Max(1f, value));
    }
}
