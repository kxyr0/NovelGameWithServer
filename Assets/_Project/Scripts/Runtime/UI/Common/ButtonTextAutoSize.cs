using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class ButtonTextAutoSize : MonoBehaviour
{
    private const float SizeEpsilon = 0.5f;

    [Header("Ссылки")]
    [Tooltip("TMP_Text с надписью кнопки. По нему рассчитывается размер кнопки; если поле пустое, скрипт возьмёт первый TMP_Text в дочерних объектах.")]
    [SerializeField] private TMP_Text _text;

    [Tooltip("RectTransform самой кнопки. Если поле пустое, используется RectTransform объекта с этим скриптом.")]
    [SerializeField] private RectTransform _buttonRect;

    [Tooltip("LayoutElement для кнопки внутри Layout Group. Необязательно, но помогает корректно передавать рассчитанный preferred size.")]
    [SerializeField] private LayoutElement _layoutElement;

    [Tooltip("Image фона кнопки. Если поле пустое, используется Image на объекте с этим скриптом.")]
    [SerializeField] private Image _buttonImage;

    [Header("Ширина")]
    [SerializeField] private bool _resizeWidth = true;
    [SerializeField] private float _horizontalPadding = 96f;
    [SerializeField] private float _minWidth;
    [SerializeField] private float _maxWidth;
    [SerializeField] private bool _useCurrentWidthAsMinimum;
    [SerializeField] private bool _keepTextOnOneLine = true;

    [Tooltip("Считать ширину по реальному preferred width текста. Если включено, широкая стартовая плашка или широкий TMP_Text не заставляют короткие надписи оставаться огромными.")]
    [SerializeField] private bool _fitWidthToTextContent = true;

    [Tooltip("Для длинного текста с переносом искать самую компактную ширину, которая не добавляет лишнюю строку.")]
    [SerializeField] private bool _compactWrappedTextWidth = true;

    [Header("Высота")]
    [SerializeField] private bool _resizeHeight = true;
    [SerializeField] private float _verticalPadding = 44f;
    [SerializeField] private float _minHeight = 56f;
    [SerializeField] private float _maxHeight;
    [SerializeField] private bool _useCurrentHeightAsMinimum;

    [Header("Layout")]
    [SerializeField] private bool _writeLayoutElement = true;
    [SerializeField] private bool _zeroFlexibleSize = true;
    [SerializeField] private bool _autoRefresh = true;
    [SerializeField] private bool _updateInLateUpdate = true;

    [Tooltip("Если LayoutElement не назначен, создать его во время игры, чтобы Layout Group могла читать рассчитанный preferred size.")]
    [SerializeField] private bool _createLayoutElementAtRuntime = true;

    [Tooltip("Обновлять размер ещё раз перед рендером Canvas. Помогает подхватить изменения текста, сделанные после LateUpdate.")]
    [SerializeField] private bool _refreshBeforeCanvasRender = true;

    [Tooltip("Перед измерением текста принудительно обновлять Canvas и TMP layout, чтобы preferred size был актуальным.")]
    [SerializeField] private bool _forceCanvasUpdateBeforeMeasure = true;

    [Tooltip("Сразу перестраивать родительский layout после изменения размера, чтобы соседние кнопки сдвигались в тот же кадр.")]
    [SerializeField] private bool _forceParentLayoutRebuild = true;

    [Header("Фон кнопки")]
    [Tooltip("Отключать Preserve Aspect у фонового Image, чтобы фон заполнял рассчитанный размер RectTransform.")]
    [SerializeField] private bool _stretchImageToCalculatedSize = true;

    [Tooltip("Если у спрайта есть borders, переключать Image в режим Sliced перед изменением размера, чтобы края рамки не растягивались.")]
    [SerializeField] private bool _useSlicedImageWhenPossible = true;

    [Header("Перенос и удержание текста")]
    [Tooltip("Если включено, скрипт сохраняет настройку переноса строк из TMP_Text. Если выключено, переносом управляет Keep Text On One Line.")]
    [SerializeField] private bool _respectTextWrappingSetting = true;

    [Tooltip("Перед измерением подгонять RectTransform текста под фактическую внутреннюю область кнопки.")]
    [SerializeField] private bool _driveTextRectSize = true;

    [Tooltip("Если включено, скрипт управляет шириной RectTransform текста. Выключи, если ширина текста должна настраиваться вручную в инспекторе.")]
    [SerializeField] private bool _driveTextRectWidth;

    [Tooltip("Автоматически исправлять ручную ширину TMP_Text, если она устарела, слишком маленькая или шире рассчитанной области.")]
    [SerializeField] private bool _repairTextRectWidthWhenOutside = true;

    [Tooltip("Центрировать RectTransform текста внутри кнопки, когда скрипт управляет его шириной или исправляет её.")]
    [SerializeField] private bool _centerDrivenTextRect = true;

    [Tooltip("Если включено, скрипт управляет высотой RectTransform текста, чтобы строки с переносом оставались внутри рамки.")]
    [SerializeField] private bool _driveTextRectHeight = true;

    [Tooltip("Не давать высоте TMP_Text становиться больше стартовой. Полезно для декоративных плашек, где текст должен уменьшаться, а не растягивать рамку.")]
    [SerializeField] private bool _limitTextRectHeightToInitialSize;

    [Tooltip("Центрировать RectTransform текста по вертикали, когда скрипт управляет его высотой.")]
    [SerializeField] private bool _centerDrivenTextRectVertically;

    [Tooltip("Дополнительный сдвиг TMP_Text внутри кнопки. Для Y отрицательное значение опускает текст ниже.")]
    [SerializeField] private Vector2 _bodyTextOffset = new Vector2(0f, -4f);

    [Tooltip("Дополнительные пиксели к измеренному размеру, чтобы избежать небольших переполнений из-за округления TMP bounds.")]
    [SerializeField] private float _measurementPadding = 2f;

    [Tooltip("Если итоговый текст всё ещё больше своего RectTransform, дополнительно ужимать его внутрь рамки.")]
    [SerializeField] private bool _forceTextInsideFrame = true;

    [Tooltip("Режим переполнения, если текст нельзя вместить изменением размера, обычно из-за ограничений ширины или высоты.")]
    [SerializeField] private TextOverflowModes _containedOverflowMode = TextOverflowModes.Ellipsis;

    [Tooltip("Включать TMP Auto Size только если отрисованный текст не помещается в свою рамку.")]
    [SerializeField] private bool _autoSizeWhenConstrained = true;

    [Tooltip("Жёсткий верхний предел размера шрифта TMP для этого компонента. 0 означает оставить максимум, заданный в самом TMP_Text.")]
    [SerializeField] private float _maxFontSize;

    [SerializeField] private float _minAutoFontSize = 14f;
    [SerializeField] private float _minReadableTextRectWidth = 16f;
    [SerializeField] private int _maxFitPasses = 3;

    private string _lastText;
    private float _lastFontSize;
    private Vector2 _lastButtonSize;
    private Vector2 _lastTextRectSize;
    private float _initialWidth;
    private float _initialHeight;
    private Vector2 _initialTextRectSize;
    private bool _hasInitialSize;
    private bool _hasInitialTextRectSize;
    private bool _dirty = true;
    private bool _missingReferencesLogged;
    private bool _hasInitialTextSettings;
    private TextOverflowModes _initialOverflowMode;
    private bool _initialAutoSizing;
    private float _initialFontSize;
    private float _initialFontSizeMin;
    private float _initialFontSizeMax;
    private bool _isRefreshing;
    private bool _isHandlingWillRenderCanvases;
    private bool _isSubscribedToTextChanges;
    private bool _containmentApplied;
    private bool _hasFontSizeBeforeContainment;
    private float _fontSizeBeforeContainment;
    private bool _autoRefreshSuspended;

    public TMP_Text Text => _text;
    public RectTransform ButtonRect => _buttonRect;
    public Vector2 Padding => new Vector2(_horizontalPadding, _verticalPadding);
    public bool AutoRefresh
    {
        get => _autoRefresh;
        set
        {
            if (_autoRefresh == value)
                return;

            _autoRefresh = value;
            if (_autoRefresh)
                MarkDirty();
        }
    }

    public bool AutoRefreshSuspended
    {
        get => _autoRefreshSuspended;
        set
        {
            if (_autoRefreshSuspended == value)
                return;

            _autoRefreshSuspended = value;
            if (!_autoRefreshSuspended && _autoRefresh)
                MarkDirty();
        }
    }

    private bool CanAutoRefresh => _autoRefresh && !_autoRefreshSuspended;

    public void SetTargets(TMP_Text text, RectTransform buttonRect)
    {
        _text = text;
        _buttonRect = buttonRect;
        CaptureInitialSize(true);
        MarkDirty();
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
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    [ContextMenu("Refresh Now")]
    public void RefreshNow()
    {
        if (_isRefreshing)
            return;

        TryAutoWire();
        CaptureInitialSize(false);

        if (!CanResize())
            return;

        _isRefreshing = true;
        try
        {
            CaptureInitialTextSettings();
            RestoreTextContainmentSettings();
            ApplyFontSizeLimit();
            ApplyWrappingMode();
            ApplyImageSizingMode();
            ForceTextAndLayoutUpdate();

            Vector2 size = CalculateTargetSize(_text.text ?? string.Empty);
            float width = size.x;
            float height = size.y;

            ApplyButtonSize(width, height);
            ApplyTextRectSize(ResolveFinalTextWidth(width), ResolveFinalTextHeight(height));
            FitRenderedTextInsideFrame(ref width, ref height);

            ApplyLayoutElement(width, height);
            MarkLayoutDirty();
            RememberCurrentState();
            _dirty = false;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    [ContextMenu("Auto Wire References")]
    private void AutoWireReferencesFromContext()
    {
        _buttonRect = GetComponent<RectTransform>();
        _text = GetComponentInChildren<TMP_Text>(true);
        _layoutElement = _buttonRect != null ? _buttonRect.GetComponent<LayoutElement>() : null;
        CaptureInitialSize(true);
        MarkDirty();
        RefreshNow();
    }

    [ContextMenu("Add Missing LayoutElement")]
    private void AddMissingLayoutElementFromContext()
    {
        TryAutoWire();

        if (_buttonRect != null && _layoutElement == null)
            _layoutElement = _buttonRect.gameObject.AddComponent<LayoutElement>();

        MarkDirty();
        RefreshNow();
    }

    private void Reset()
    {
        TryAutoWire();
        CaptureInitialSize(true);
    }

    private void Awake()
    {
        TryAutoWire();
        CaptureInitialSize(false);
    }

    private void OnEnable()
    {
        TryAutoWire();
        SubscribeToTextChanges();
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        Canvas.willRenderCanvases += HandleWillRenderCanvases;
        CaptureInitialSize(false);
        MarkDirty();
        if (CanAutoRefresh)
            RefreshNow();
    }

    private void OnDisable()
    {
        UnsubscribeFromTextChanges();
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        RestoreTextContainmentSettings();
    }

    private void OnValidate()
    {
        _horizontalPadding = Mathf.Max(0f, _horizontalPadding);
        _verticalPadding = Mathf.Max(0f, _verticalPadding);
        _minWidth = Mathf.Max(0f, _minWidth);
        _maxWidth = Mathf.Max(0f, _maxWidth);
        _minHeight = Mathf.Max(0f, _minHeight);
        _maxHeight = Mathf.Max(0f, _maxHeight);
        _measurementPadding = Mathf.Max(0f, _measurementPadding);
        _maxFontSize = Mathf.Max(0f, _maxFontSize);
        _minAutoFontSize = Mathf.Max(1f, _minAutoFontSize);
        _minReadableTextRectWidth = Mathf.Max(1f, _minReadableTextRectWidth);
        _maxFitPasses = Mathf.Clamp(_maxFitPasses, 1, 8);

        if (_maxFontSize > 0f && _minAutoFontSize > _maxFontSize)
            _minAutoFontSize = _maxFontSize;

        if (_maxWidth > 0f && _maxWidth < _minWidth)
            _maxWidth = _minWidth;

        if (_maxHeight > 0f && _maxHeight < _minHeight)
            _maxHeight = _minHeight;

        TryAutoWire();
        MarkDirty();
    }

    private void LateUpdate()
    {
        if (!CanAutoRefresh)
            return;

        if (!_updateInLateUpdate && !_dirty)
            return;

        if (_dirty || HasTextChanged())
            RefreshNow();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_isRefreshing || !CanAutoRefresh || !isActiveAndEnabled)
            return;

        MarkDirty();
    }

    private void OnTransformParentChanged()
    {
        MarkDirty();
    }

    private void OnDidApplyAnimationProperties()
    {
        MarkDirty();
    }

    private void HandleWillRenderCanvases()
    {
        if (!CanAutoRefresh || !_refreshBeforeCanvasRender || _isRefreshing || !isActiveAndEnabled)
            return;

        if (!_dirty && !HasTextChanged())
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

    private void SubscribeToTextChanges()
    {
        if (_isSubscribedToTextChanges)
            return;

        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(HandleTextChanged);
        _isSubscribedToTextChanges = true;
    }

    private void UnsubscribeFromTextChanges()
    {
        if (!_isSubscribedToTextChanges)
            return;

        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(HandleTextChanged);
        _isSubscribedToTextChanges = false;
    }

    private void HandleTextChanged(Object changedObject)
    {
        if (changedObject == _text)
            MarkDirty();
    }

    private void TryAutoWire()
    {
        if (_buttonRect == null)
            _buttonRect = GetComponent<RectTransform>();

        if (_text == null)
            _text = GetComponentInChildren<TMP_Text>(true);

        if (_buttonImage == null)
            _buttonImage = GetComponent<Image>();

        if (_layoutElement == null && _buttonRect != null)
        {
            _layoutElement = _buttonRect.GetComponent<LayoutElement>();

            if (_layoutElement == null && _writeLayoutElement && _createLayoutElementAtRuntime && Application.isPlaying)
                _layoutElement = _buttonRect.gameObject.AddComponent<LayoutElement>();
        }
    }

    private bool CanResize()
    {
        if (_text != null && _buttonRect != null)
        {
            _missingReferencesLogged = false;
            return true;
        }

        if (!_missingReferencesLogged)
        {
            Debug.LogWarning("ButtonTextAutoSize needs TMP_Text and RectTransform references.", this);
            _missingReferencesLogged = true;
        }

        return false;
    }

    private void CaptureInitialSize(bool force)
    {
        if (!force && _hasInitialSize)
        {
            CaptureInitialTextRectSize(false);
            return;
        }

        if (_buttonRect == null)
        {
            CaptureInitialTextRectSize(force);
            return;
        }

        Vector2 size = _buttonRect.rect.size;
        _initialWidth = Mathf.Max(0f, size.x);
        _initialHeight = Mathf.Max(0f, size.y);
        _hasInitialSize = true;

        CaptureInitialTextRectSize(force);
    }

    private void CaptureInitialTextRectSize(bool force)
    {
        if (!force && _hasInitialTextRectSize)
            return;

        RectTransform textRect = _text != null ? _text.rectTransform : null;
        if (textRect == null)
            return;

        Vector2 size = textRect.rect.size;
        if (size.x <= 1f && size.y <= 1f)
            return;

        _initialTextRectSize = new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
        _hasInitialTextRectSize = true;
    }

    private void CaptureInitialTextSettings()
    {
        if (_hasInitialTextSettings || _text == null)
            return;

        _initialOverflowMode = _text.overflowMode;
        _initialAutoSizing = _text.enableAutoSizing;
        _initialFontSize = _text.fontSize;
        _initialFontSizeMin = _text.fontSizeMin;
        _initialFontSizeMax = _text.fontSizeMax;
        _hasInitialTextSettings = true;
    }

    private void ApplyWrappingMode()
    {
        if (_text == null || _respectTextWrappingSetting)
            return;

        _text.enableWordWrapping = !_keepTextOnOneLine;
    }

    private void RestoreTextContainmentSettings()
    {
        if (_text == null || !_hasInitialTextSettings)
            return;

        _text.overflowMode = _initialOverflowMode;
        _text.enableAutoSizing = _initialAutoSizing;
        if (_containmentApplied && _hasFontSizeBeforeContainment && !_initialAutoSizing)
            _text.fontSize = Mathf.Max(1f, _fontSizeBeforeContainment);

        _text.fontSizeMin = _initialFontSizeMin;
        _text.fontSizeMax = _initialFontSizeMax;
        _containmentApplied = false;
        _hasFontSizeBeforeContainment = false;
    }

    private void ApplyFontSizeLimit()
    {
        if (_text == null || _maxFontSize <= 0f)
            return;

        float maxFontSize = Mathf.Max(1f, _maxFontSize);

        if (_text.fontSize > maxFontSize)
            _text.fontSize = maxFontSize;

        _text.fontSizeMax = maxFontSize;
        _text.fontSizeMin = Mathf.Min(Mathf.Max(1f, _text.fontSizeMin), maxFontSize);
    }

    private void ForceTextAndLayoutUpdate()
    {
        if (_forceCanvasUpdateBeforeMeasure && !_isHandlingWillRenderCanvases)
            Canvas.ForceUpdateCanvases();

        if (_text == null)
            return;

        _text.SetAllDirty();
        _text.ForceMeshUpdate(true, true);
    }

    private void ApplyImageSizingMode()
    {
        if (_buttonImage == null)
            return;

        if (_stretchImageToCalculatedSize)
            _buttonImage.preserveAspect = false;

        Sprite sprite = _buttonImage.sprite;
        if (_useSlicedImageWhenPossible && sprite != null && sprite.border.sqrMagnitude > 0f)
            _buttonImage.type = Image.Type.Sliced;
    }

    private Vector2 CalculateTargetSize(string value)
    {
        float width = Mathf.Max(1f, _buttonRect.rect.width);
        float textWidth = ResolveMeasuringTextWidth(width);
        Vector2 preferred = Vector2.zero;
        int passes = Mathf.Max(1, _maxFitPasses);

        for (int i = 0; i < passes; i++)
        {
            preferred = MeasurePreferred(value, textWidth);

            float nextWidth = ResolveWidth(preferred.x);
            float nextTextWidth = ResolveFinalTextWidth(nextWidth);
            bool stable = !IsWrappingEnabled() || Mathf.Abs(nextTextWidth - textWidth) <= SizeEpsilon;

            width = nextWidth;
            textWidth = nextTextWidth;

            if (stable)
                break;
        }

        preferred = MeasurePreferred(value, ResolveFinalTextWidth(width));

        float refinedWidth = ResolveWidth(preferred.x);
        if (Mathf.Abs(refinedWidth - width) > SizeEpsilon)
        {
            width = refinedWidth;
            preferred = MeasurePreferred(value, ResolveFinalTextWidth(width));
        }

        width = RefineWidthForWrappedText(value, width);
        preferred = MeasurePreferred(value, ResolveFinalTextWidth(width));

        float height = ResolveHeight(preferred.y);
        return new Vector2(width, height);
    }

    private void ApplyButtonSize(float width, float height)
    {
        if (_resizeWidth)
            SetSizeIfChanged(_buttonRect, RectTransform.Axis.Horizontal, width);

        if (_resizeHeight)
            SetSizeIfChanged(_buttonRect, RectTransform.Axis.Vertical, height);
    }

    private Vector2 MeasurePreferred(string value, float textWidth)
    {
        float widthConstraint = IsWrappingEnabled()
            ? Mathf.Max(1f, textWidth)
            : Mathf.Infinity;

        Vector2 preferred = _text.GetPreferredValues(value, widthConstraint, Mathf.Infinity);
        if (_fitWidthToTextContent && IsWrappingEnabled())
        {
            Vector2 unwrapped = _text.GetPreferredValues(value, Mathf.Infinity, Mathf.Infinity);
            preferred.x = Mathf.Min(preferred.x, unwrapped.x, widthConstraint);
        }

        preferred.x = Mathf.Max(0f, preferred.x) + _measurementPadding;
        preferred.y = Mathf.Max(0f, preferred.y) + _measurementPadding;
        return preferred;
    }

    private float RefineWidthForWrappedText(string value, float width)
    {
        if (!_fitWidthToTextContent || !_compactWrappedTextWidth || !IsWrappingEnabled() || string.IsNullOrEmpty(value))
            return width;

        float currentWidth = Mathf.Max(1f, width);
        Vector2 currentPreferred = MeasurePreferred(value, ResolveFinalTextWidth(currentWidth));
        float allowedHeight = currentPreferred.y + ResolveLineHeightTolerance();
        float lowerWidth = Mathf.Min(currentWidth, Mathf.Max(ResolveMinimumWidth(), ResolveNarrowestReadableWidth(value)));

        if (currentWidth - lowerWidth <= SizeEpsilon)
            return currentWidth;

        float bestWidth = currentWidth;
        float low = lowerWidth;
        float high = currentWidth;

        for (int i = 0; i < 8; i++)
        {
            float mid = Mathf.Lerp(low, high, 0.5f);
            Vector2 preferred = MeasurePreferred(value, ResolveFinalTextWidth(mid));

            if (preferred.y <= allowedHeight)
            {
                bestWidth = mid;
                high = mid;
            }
            else
            {
                low = mid;
            }
        }

        return Mathf.Ceil(ClampAxis(bestWidth, ResolveMinimumWidth(), _maxWidth));
    }

    private float ResolveLineHeightTolerance()
    {
        float fontSize = _text != null ? Mathf.Max(1f, _text.fontSize) : 1f;
        return Mathf.Max(_measurementPadding + SizeEpsilon, fontSize * 0.2f);
    }

    private float ResolveNarrowestReadableWidth(string value)
    {
        float widestToken = _minReadableTextRectWidth;
        string[] tokens = value.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < tokens.Length; i++)
        {
            Vector2 tokenSize = _text.GetPreferredValues(tokens[i], Mathf.Infinity, Mathf.Infinity);
            widestToken = Mathf.Max(widestToken, tokenSize.x + _measurementPadding);
        }

        return ResolveWidth(widestToken);
    }

    private bool IsWrappingEnabled()
    {
        return _text != null && _text.enableWordWrapping;
    }

    private float ResolveMeasuringTextWidth(float currentButtonWidth)
    {
        if (!IsWrappingEnabled())
            return Mathf.Infinity;

        float calculatedContentWidth = _maxWidth > 0f
            ? GetContentWidth(_maxWidth)
            : GetContentWidth(currentButtonWidth);

        if (!_driveTextRectWidth &&
            TryGetTextRectSize(out Vector2 textSize) &&
            ShouldUseManualTextWidth(textSize.x, calculatedContentWidth))
        {
            return Mathf.Max(1f, textSize.x);
        }

        if (_maxWidth > 0f)
            return GetContentWidth(_maxWidth);

        if (_resizeWidth && currentButtonWidth > 1f)
            return GetContentWidth(currentButtonWidth);

        RectTransform textRect = _text != null ? _text.rectTransform : null;
        if (textRect != null && textRect.rect.width > 1f)
            return Mathf.Max(1f, textRect.rect.width);

        return GetContentWidth(currentButtonWidth);
    }

    private float ResolveWidth(float preferredTextWidth)
    {
        float current = Mathf.Max(1f, _buttonRect.rect.width);

        if (!_resizeWidth)
            return current;

        float min = _minWidth;
        if (_useCurrentWidthAsMinimum && !_fitWidthToTextContent)
            min = Mathf.Max(min, _initialWidth);

        return ClampAxis(preferredTextWidth + _horizontalPadding, min, _maxWidth);
    }

    private float ResolveHeight(float preferredTextHeight)
    {
        float current = Mathf.Max(1f, _buttonRect.rect.height);

        if (!_resizeHeight)
            return current;

        float min = _minHeight;
        if (_useCurrentHeightAsMinimum)
            min = Mathf.Max(min, _initialHeight);

        return ClampAxis(preferredTextHeight + _verticalPadding, min, _maxHeight);
    }

    private float GetContentWidth(float buttonWidth)
    {
        return Mathf.Max(Mathf.Max(1f, _minReadableTextRectWidth), buttonWidth - _horizontalPadding);
    }

    private float GetContentHeight(float buttonHeight)
    {
        return Mathf.Max(1f, buttonHeight - _verticalPadding);
    }

    private float ResolveFinalTextWidth(float buttonWidth)
    {
        float contentWidth = GetContentWidth(buttonWidth);
        if (!_driveTextRectWidth &&
            TryGetTextRectSize(out Vector2 textSize) &&
            ShouldUseManualTextWidth(textSize.x, contentWidth))
        {
            return Mathf.Max(1f, textSize.x);
        }

        return contentWidth;
    }

    private float ResolveFinalTextHeight(float buttonHeight)
    {
        float height;
        if (!_driveTextRectHeight && TryGetTextRectSize(out Vector2 textSize) && textSize.y > 1f)
            height = Mathf.Max(1f, textSize.y);
        else
            height = GetContentHeight(buttonHeight);

        if (_limitTextRectHeightToInitialSize &&
            _hasInitialTextRectSize &&
            _initialTextRectSize.y > 1f)
        {
            height = Mathf.Min(height, _initialTextRectSize.y);
        }

        return Mathf.Max(1f, height);
    }

    private bool ShouldUseManualTextWidth(float manualWidth, float calculatedContentWidth)
    {
        if (_driveTextRectWidth)
            return false;

        if (manualWidth <= Mathf.Max(SizeEpsilon, _minReadableTextRectWidth))
            return false;

        if (!_repairTextRectWidthWhenOutside)
            return true;

        return manualWidth <= calculatedContentWidth + SizeEpsilon;
    }

    private bool ShouldRepairTextRectWidth(float calculatedWidth)
    {
        if (!_repairTextRectWidthWhenOutside || _text == null)
            return false;

        if (!TryGetTextRectSize(out Vector2 textSize))
            return false;

        return !ShouldUseManualTextWidth(textSize.x, calculatedWidth);
    }

    private bool TryGetTextRectSize(out Vector2 size)
    {
        RectTransform textRect = _text != null ? _text.rectTransform : null;
        size = textRect != null ? textRect.rect.size : Vector2.zero;
        return textRect != null;
    }

    private void ApplyTextRectSize(float width, float height)
    {
        if (!_driveTextRectSize || _text == null)
            return;

        RectTransform textRect = _text.rectTransform;
        if (textRect == null)
            return;

        bool driveWidth = _driveTextRectWidth || ShouldRepairTextRectWidth(width);
        if (driveWidth)
        {
            SetSizeIfChanged(textRect, RectTransform.Axis.Horizontal, Mathf.Max(1f, width));
            CenterTextRectHorizontally(textRect);
        }

        if (_driveTextRectHeight)
        {
            SetSizeIfChanged(textRect, RectTransform.Axis.Vertical, Mathf.Max(1f, height));
            CenterTextRectVertically(textRect);
        }

        LayoutRebuilder.MarkLayoutForRebuild(textRect);
    }

    private void CenterTextRectHorizontally(RectTransform textRect)
    {
        if (!_centerDrivenTextRect || textRect == null)
            return;

        Vector2 anchoredPosition = textRect.anchoredPosition;
        if (Mathf.Abs(anchoredPosition.x - _bodyTextOffset.x) <= SizeEpsilon)
            return;

        textRect.anchoredPosition = new Vector2(_bodyTextOffset.x, anchoredPosition.y);
    }

    private void CenterTextRectVertically(RectTransform textRect)
    {
        if ((!_centerDrivenTextRectVertically && Mathf.Abs(_bodyTextOffset.y) <= SizeEpsilon) || textRect == null)
            return;

        Vector2 anchoredPosition = textRect.anchoredPosition;
        if (Mathf.Abs(anchoredPosition.y - _bodyTextOffset.y) <= SizeEpsilon)
            return;

        textRect.anchoredPosition = new Vector2(anchoredPosition.x, _bodyTextOffset.y);
    }

    private void FitRenderedTextInsideFrame(ref float width, ref float height)
    {
        if (_text == null)
            return;

        for (int i = 0; i < _maxFitPasses; i++)
        {
            float contentWidth = GetContentWidth(width);
            float contentHeight = GetContentHeight(height);
            Vector2 rendered = GetRenderedTextSize();

            bool expanded = false;
            if (_resizeWidth && rendered.x > contentWidth + SizeEpsilon)
            {
                float nextWidth = ClampAxis(rendered.x + _horizontalPadding + _measurementPadding, ResolveMinimumWidth(), _maxWidth);
                if (nextWidth > width + SizeEpsilon)
                {
                    width = nextWidth;
                    SetSizeIfChanged(_buttonRect, RectTransform.Axis.Horizontal, width);
                    expanded = true;
                }
            }

            if (_resizeHeight && rendered.y > contentHeight + SizeEpsilon)
            {
                float nextHeight = ClampAxis(rendered.y + _verticalPadding + _measurementPadding, ResolveMinimumHeight(), _maxHeight);
                if (nextHeight > height + SizeEpsilon)
                {
                    height = nextHeight;
                    SetSizeIfChanged(_buttonRect, RectTransform.Axis.Vertical, height);
                    expanded = true;
                }
            }

            ApplyTextRectSize(ResolveFinalTextWidth(width), ResolveFinalTextHeight(height));

            if (!expanded)
                break;
        }

        if (_forceTextInsideFrame && RenderedTextExceedsFrame())
            ApplyTextContainment();
    }

    private Vector2 GetRenderedTextSize()
    {
        _text.ForceMeshUpdate(true, true);
        Vector2 rendered = _text.GetRenderedValues(false);
        return new Vector2(
            Mathf.Max(0f, rendered.x) + _measurementPadding,
            Mathf.Max(0f, rendered.y) + _measurementPadding);
    }

    private bool RenderedTextExceedsFrame()
    {
        RectTransform textRect = _text.rectTransform;
        if (textRect == null)
            return false;

        Vector2 rendered = GetRenderedTextSize();
        Vector2 rectSize = textRect.rect.size;
        return rendered.x > rectSize.x + SizeEpsilon || rendered.y > rectSize.y + SizeEpsilon;
    }

    private void ApplyTextContainment()
    {
        if (!_containmentApplied)
        {
            _fontSizeBeforeContainment = Mathf.Max(1f, _text.fontSize);
            _hasFontSizeBeforeContainment = true;
            _containmentApplied = true;
        }

        if (_autoSizeWhenConstrained)
        {
            float currentFontSize = Mathf.Max(1f, _text.fontSize);
            _text.enableAutoSizing = true;
            _text.fontSizeMax = ResolveAutoFontSizeMax(currentFontSize);
            _text.fontSizeMin = Mathf.Min(Mathf.Max(1f, _minAutoFontSize), _text.fontSizeMax);
            _text.ForceMeshUpdate(true, true);
        }

        if (RenderedTextExceedsFrame())
        {
            _text.overflowMode = _containedOverflowMode;
            _text.ForceMeshUpdate(true, true);
        }
    }

    private float ResolveAutoFontSizeMax(float currentFontSize)
    {
        if (_maxFontSize > 0f)
            return Mathf.Max(1f, _maxFontSize);

        return Mathf.Max(_text.fontSizeMax, currentFontSize, _initialFontSize);
    }

    private float ResolveMinimumWidth()
    {
        float min = _minWidth;
        if (_useCurrentWidthAsMinimum && !_fitWidthToTextContent)
            min = Mathf.Max(min, _initialWidth);
        return min;
    }

    private float ResolveMinimumHeight()
    {
        float min = _minHeight;
        if (_useCurrentHeightAsMinimum)
            min = Mathf.Max(min, _initialHeight);
        return min;
    }

    private void ApplyLayoutElement(float width, float height)
    {
        if (!_writeLayoutElement || _layoutElement == null)
            return;

        if (_resizeWidth)
        {
            _layoutElement.preferredWidth = width;
            float minWidth = ResolveMinimumWidth();
            _layoutElement.minWidth = minWidth > 0f ? minWidth : -1f;
        }

        if (_resizeHeight)
        {
            _layoutElement.preferredHeight = height;
            float minHeight = ResolveMinimumHeight();
            _layoutElement.minHeight = minHeight > 0f ? minHeight : -1f;
        }

        if (_zeroFlexibleSize)
        {
            if (_resizeWidth)
                _layoutElement.flexibleWidth = 0f;

            if (_resizeHeight)
                _layoutElement.flexibleHeight = 0f;
        }
    }

    private bool HasTextChanged()
    {
        if (_text == null)
            return false;

        RectTransform textRect = _text.rectTransform;
        Vector2 buttonSize = _buttonRect != null ? _buttonRect.rect.size : Vector2.zero;
        Vector2 textRectSize = textRect != null ? textRect.rect.size : Vector2.zero;

        return _lastText != _text.text ||
               _text.havePropertiesChanged ||
               !Mathf.Approximately(_lastFontSize, _text.fontSize) ||
               _lastButtonSize != buttonSize ||
               _lastTextRectSize != textRectSize;
    }

    private void RememberCurrentState()
    {
        _lastText = _text.text;
        _lastFontSize = _text.fontSize;
        _lastButtonSize = _buttonRect != null ? _buttonRect.rect.size : Vector2.zero;
        RectTransform textRect = _text != null ? _text.rectTransform : null;
        _lastTextRectSize = textRect != null ? textRect.rect.size : Vector2.zero;
    }

    private void MarkLayoutDirty()
    {
        LayoutRebuilder.MarkLayoutForRebuild(_buttonRect);

        if (_buttonRect.parent is RectTransform parent)
        {
            LayoutRebuilder.MarkLayoutForRebuild(parent);

            if (_forceParentLayoutRebuild)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
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
