using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class StoryTextLayoutOverride
{
    [Tooltip("ID истории, например privychka_pritvoryatsya или only_the_heart_sees_clearly.")]
    [SerializeField] private string _storyId;

    [Tooltip("Top Offset Y, который будет использоваться только для этой истории.")]
    [SerializeField] private float _topOffsetY;

    [Tooltip("Offset X, который будет использоваться только для этой истории.")]
    [SerializeField] private float _offsetX;

    [Header("Размер текста")]
    [SerializeField] private bool _overrideTextWidth;
    [SerializeField] private float _textWidth = 760f;

    [Header("Плашка по тексту")]
    [InspectorName("Переопределить padding плашки")]
    [SerializeField] private bool _overrideBackgroundPadding;
    [InspectorName("Padding плашки")]
    [SerializeField] private Vector2 _backgroundPadding = new Vector2(72f, 56f);
    [InspectorName("Переопределить мин. размер плашки")]
    [SerializeField] private bool _overrideBackgroundMinSize;
    [InspectorName("Мин. размер плашки")]
    [SerializeField] private Vector2 _backgroundMinSize;
    [InspectorName("Переопределить макс. размер плашки")]
    [SerializeField] private bool _overrideBackgroundMaxSize;
    [InspectorName("Макс. размер плашки")]
    [SerializeField] private Vector2 _backgroundMaxSize;
    [InspectorName("Переопределить подъём при росте")]
    [SerializeField] private bool _overrideBackgroundGrowthUpFactor;
    [InspectorName("Подъём при росте")]
    [SerializeField, Min(0f)] private float _backgroundGrowthUpFactor;

    [Header("Автовысота текста")]
    [SerializeField] private bool _overrideResizeHeightToPreferredText;
    [SerializeField] private bool _resizeHeightToPreferredText = true;
    [SerializeField] private bool _overrideExtraHeight;
    [SerializeField] private float _extraHeight;
    [SerializeField] private bool _overrideMinHeight;
    [SerializeField] private float _minHeight;
    [SerializeField] private bool _overrideMaxHeight;
    [SerializeField] private float _maxHeight;

    [Header("Ограничения размера текста")]
    [SerializeField] private bool _overrideMaxFontSize;
    [SerializeField] private float _maxFontSize;
    [SerializeField] private bool _overrideShrinkTextToFitRect;
    [SerializeField] private bool _shrinkTextToFitRect = true;
    [SerializeField] private bool _overrideMinAutoFontSize;
    [SerializeField] private float _minAutoFontSize = 18f;
    [SerializeField] private bool _overrideOverflowModeWhenStillTooLarge;
    [SerializeField] private TextOverflowModes _overflowModeWhenStillTooLarge = TextOverflowModes.Ellipsis;

    public float TopOffsetY => _topOffsetY;
    public float OffsetX => _offsetX;
    public bool OverrideTextWidth => _overrideTextWidth;
    public float TextWidth => _textWidth;
    public bool OverrideBackgroundPadding => _overrideBackgroundPadding;
    public Vector2 BackgroundPadding => _backgroundPadding;
    public bool OverrideBackgroundMinSize => _overrideBackgroundMinSize;
    public Vector2 BackgroundMinSize => _backgroundMinSize;
    public bool OverrideBackgroundMaxSize => _overrideBackgroundMaxSize;
    public Vector2 BackgroundMaxSize => _backgroundMaxSize;
    public bool OverrideBackgroundGrowthUpFactor => _overrideBackgroundGrowthUpFactor;
    public float BackgroundGrowthUpFactor => _backgroundGrowthUpFactor;
    public bool OverrideResizeHeightToPreferredText => _overrideResizeHeightToPreferredText;
    public bool ResizeHeightToPreferredText => _resizeHeightToPreferredText;
    public bool OverrideExtraHeight => _overrideExtraHeight;
    public float ExtraHeight => _extraHeight;
    public bool OverrideMinHeight => _overrideMinHeight;
    public float MinHeight => _minHeight;
    public bool OverrideMaxHeight => _overrideMaxHeight;
    public float MaxHeight => _maxHeight;
    public bool OverrideMaxFontSize => _overrideMaxFontSize;
    public float MaxFontSize => _maxFontSize;
    public bool OverrideShrinkTextToFitRect => _overrideShrinkTextToFitRect;
    public bool ShrinkTextToFitRect => _shrinkTextToFitRect;
    public bool OverrideMinAutoFontSize => _overrideMinAutoFontSize;
    public float MinAutoFontSize => _minAutoFontSize;
    public bool OverrideOverflowModeWhenStillTooLarge => _overrideOverflowModeWhenStillTooLarge;
    public TextOverflowModes OverflowModeWhenStillTooLarge => _overflowModeWhenStillTooLarge;

    public bool Matches(string storyId)
    {
        return Normalize(_storyId) == Normalize(storyId);
    }

    public void Validate()
    {
        _storyId = Normalize(_storyId);
        _textWidth = _textWidth > 0f ? _textWidth : 760f;
        _backgroundPadding = new Vector2(
            Mathf.Max(0f, _backgroundPadding.x),
            Mathf.Max(0f, _backgroundPadding.y));
        _backgroundMinSize = MaxVector2(_backgroundMinSize, 0f);
        _backgroundMaxSize = MaxVector2(_backgroundMaxSize, 0f);
        if (_overrideBackgroundMinSize && _overrideBackgroundMaxSize)
        {
            if (_backgroundMaxSize.x > 0f && _backgroundMaxSize.x < _backgroundMinSize.x)
                _backgroundMaxSize.x = _backgroundMinSize.x;
            if (_backgroundMaxSize.y > 0f && _backgroundMaxSize.y < _backgroundMinSize.y)
                _backgroundMaxSize.y = _backgroundMinSize.y;
        }
        _backgroundGrowthUpFactor = Mathf.Max(0f, _backgroundGrowthUpFactor);
        _extraHeight = Mathf.Max(0f, _extraHeight);
        _minHeight = Mathf.Max(0f, _minHeight);
        _maxHeight = Mathf.Max(0f, _maxHeight);
        _maxFontSize = Mathf.Max(0f, _maxFontSize);
        _minAutoFontSize = Mathf.Max(1f, _minAutoFontSize);

        if (_overrideMinHeight && _overrideMaxHeight && _maxHeight > 0f && _maxHeight < _minHeight)
            _maxHeight = _minHeight;

        if (_overrideMaxFontSize && _maxFontSize > 0f && _overrideMinAutoFontSize && _minAutoFontSize > _maxFontSize)
            _minAutoFontSize = _maxFontSize;
    }

    static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }

    static Vector2 MaxVector2(Vector2 value, float minValue)
    {
        return new Vector2(
            Mathf.Max(minValue, value.x),
            Mathf.Max(minValue, value.y));
    }
}

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
[AddComponentMenu("Novel Template/UI/Story Text Layout Lock")]
public sealed class StoryTextLayoutLock : MonoBehaviour
{
    [Header("Фиксация роста вниз")]
    [Tooltip("RectTransform, у которого нужно зафиксировать верхнюю кромку. Если поле пустое, используется RectTransform объекта с этим скриптом.")]
    [SerializeField] private RectTransform _target;

    [Tooltip("TMP_Text внутри этого объекта. Если назначен, скрипт выравнивает текст по верхнему краю, чтобы новые строки добавлялись вниз.")]
    [SerializeField] private TMP_Text _text;

    [Tooltip("При включении запоминает текущую верхнюю кромку как неподвижную точку. После этого ширина и высота могут меняться, но верх остаётся на месте.")]
    [SerializeField] private bool _captureTopOnEnable = true;

    [Tooltip("Каждый кадр возвращать верхнюю кромку в запомненную позицию. Включи для DialoguePanel или BodyText, если другие layout-скрипты меняют размер после обновления текста.")]
    [SerializeField] private bool _lockTopEveryFrame = true;

    [Tooltip("Ставить Pivot Y = 1. Это основной режим роста вниз: при изменении высоты верх остаётся на месте, а низ уходит вниз.")]
    [SerializeField] private bool _forceTopPivot = true;

    [Tooltip("Ставить вертикальное выравнивание TMP_Text в Top. Без этого текст может визуально центрироваться внутри своего прямоугольника.")]
    [SerializeField] private bool _forceTextTopAlignment = true;

    [Header("Смещение")]
    [Tooltip("Вертикальное смещение зафиксированной верхней кромки. Положительное значение поднимает блок вверх, отрицательное опускает вниз. Используй это поле вместо Pos Y, когда включена фиксация верхней кромки.")]
    [SerializeField] private float _topOffsetY;

    [Tooltip("Горизонтальное смещение RectTransform. Положительное значение двигает вправо, отрицательное влево.")]
    [SerializeField] private float _offsetX;

    [Header("Плашка по тексту")]
    [Tooltip("Фон/плашка, которую нужно подгонять под размер текста. Для текущего диалога сюда ставь родительский Background с Image Type = Sliced.")]
    [SerializeField] private RectTransform _backgroundTarget;

    [Tooltip("Менять размер плашки по preferred size TMP текста.")]
    [SerializeField] private bool _resizeBackgroundToText = true;

    [Tooltip("Padding вокруг текста с каждой стороны: X слева/справа, Y сверху/снизу.")]
    [SerializeField] private Vector2 _backgroundPadding = new Vector2(72f, 56f);

    [Tooltip("Минимальный размер плашки. 0 по оси означает не ограничивать.")]
    [SerializeField] private Vector2 _backgroundMinSize;

    [Tooltip("Максимальный размер плашки. 0 по оси означает не ограничивать.")]
    [SerializeField] private Vector2 _backgroundMaxSize;

    [Tooltip("Двигать плашку так, чтобы текст оставался в центре области с padding. Сам текст при этом остаётся на своём месте на экране.")]
    [SerializeField] private bool _centerBackgroundOnText;

    [Tooltip("После изменения плашки вернуть Rect текста туда, где он был настроен.")]
    [SerializeField] private bool _preserveTextRectWhenResizingBackground = true;

    [Tooltip("Насколько поднимать плашку при росте высоты: 0 = верх текста не меняется и плашка растёт вниз, 1 = нижняя кромка плашки старается остаться на месте, 0.5 = половина прироста.")]
    [InspectorName("Подъём при росте")]
    [SerializeField, Min(0f)] private float _backgroundGrowthUpFactor;

    [Header("Настройки по ID истории")]
    [Tooltip("Брать Offset X, Top Offset Y и включённые параметры текста из списка ниже для конкретной истории. Во время игры ID берётся из StoryManager или GameState.")]
    [FormerlySerializedAs("_useStoryTopOffsetOverrides")]
    [FormerlySerializedAs("_useStoryOffsetOverrides")]
    [SerializeField] private bool _useStoryOverrides;

    [Tooltip("ID истории для предпросмотра в редакторе. В игре это поле не используется.")]
    [SerializeField] private string _editorPreviewStoryId;

    [Tooltip("Список настроек по ID истории. Если ID не найден, используются обычные значения выше.")]
    [FormerlySerializedAs("_storyTopOffsetOverrides")]
    [FormerlySerializedAs("_storyOffsetOverrides")]
    [SerializeField] private List<StoryTextLayoutOverride> _storyOverrides = new List<StoryTextLayoutOverride>();

    [Header("Автовысота текста")]
    [Tooltip("Автоматически менять высоту RectTransform под preferred height текста. Для BodyText обычно полезно включить, для готовой фиксированной области можно оставить выключенным.")]
    [SerializeField] private bool _resizeHeightToPreferredText;

    [Tooltip("Дополнительная высота сверх preferred height текста.")]
    [SerializeField] private float _extraHeight;

    [Tooltip("Минимальная высота RectTransform. 0 означает не ограничивать.")]
    [SerializeField] private float _minHeight;

    [Tooltip("Максимальная высота RectTransform. 0 означает не ограничивать.")]
    [SerializeField] private float _maxHeight;

    [Header("Ограничения размера текста")]
    [Tooltip("Максимальный размер шрифта TMP, которым управляет этот компонент. 0 означает использовать максимум, заданный в самом TMP_Text.")]
    [SerializeField] private float _maxFontSize;

    [Tooltip("Включить TMP Auto Size, чтобы текст уменьшался внутри своего RectTransform и не вылезал за рамку.")]
    [SerializeField] private bool _shrinkTextToFitRect = true;

    [Tooltip("Минимальный размер шрифта, до которого TMP может уменьшать текст при включённом Shrink Text To Fit Rect.")]
    [SerializeField] private float _minAutoFontSize = 18f;

    [Tooltip("Режим переполнения, если текст всё ещё не помещается после уменьшения.")]
    [SerializeField] private TextOverflowModes _overflowModeWhenStillTooLarge = TextOverflowModes.Ellipsis;

    private bool _hasLockedTop;
    private float _lockedTopY;
    private float _lockedAnchoredX;
    private bool _applying;
    private bool _hasInitialTextSettings;
    private bool _initialAutoSizing;
    private float _initialFontSize;
    private float _initialFontSizeMin;
    private float _initialFontSizeMax;
    private TextOverflowModes _initialOverflowMode;
    private float _lockedRootHeight;
    private float _lockedTextAnchoredY;
    private RectTransform _capturedLockRoot;
    private bool _capturedContainerMode;
    private bool _layoutDirty = true;
    private bool _hasLastPreferredTextSize;
    private string _lastPreferredTextValue = "";
    private float _lastPreferredTextWidth = -1f;
    private Vector2 _lastPreferredTextSize;
    private string _lastAppliedText = "";
    private string _lastAppliedStoryId = "";
    private Vector2 _lastAppliedParentSize;
    private float _lastAppliedLockRootTopY;
    private float _lastAppliedLockRootX;

    private struct ResolvedTextLayoutSettings
    {
        public bool OverrideTextWidth;
        public float TextWidth;
        public Vector2 BackgroundPadding;
        public Vector2 BackgroundMinSize;
        public Vector2 BackgroundMaxSize;
        public float BackgroundGrowthUpFactor;
        public bool ResizeHeightToPreferredText;
        public float ExtraHeight;
        public float MinHeight;
        public float MaxHeight;
        public float MaxFontSize;
        public bool ShrinkTextToFitRect;
        public float MinAutoFontSize;
        public TextOverflowModes OverflowModeWhenStillTooLarge;
    }

    private struct RectWorldSnapshot
    {
        public bool Valid;
        public RectTransform Target;
        public Vector3 BottomLeft;
        public Vector3 TopRight;
    }

    private struct TextLayoutMetrics
    {
        public float MaxTextWidth;
        public float MaxTextHeight;
        public float TextWidth;
        public float TextHeight;
        public Vector2 TextSize;
        public Vector2 BackgroundSize;
    }

    public float TopOffsetY => _topOffsetY;
    public float EffectiveTopOffsetY => ResolveEffectiveTopOffsetY();
    public float OffsetX => _offsetX;
    public float EffectiveOffsetX => ResolveEffectiveOffsetX();
    public bool ResizeHeightToPreferredText => _resizeHeightToPreferredText;
    public float ExtraHeight => _extraHeight;
    public float MinHeight => _minHeight;
    public float MaxHeight => _maxHeight;
    public float MaxFontSize => _maxFontSize;
    public bool ShrinkTextToFitRect => _shrinkTextToFitRect;
    public float MinAutoFontSize => _minAutoFontSize;
    public TextOverflowModes OverflowModeWhenStillTooLarge => _overflowModeWhenStillTooLarge;

    public bool TryGetLastPreferredTextSize(string text, float width, out Vector2 preferredSize)
    {
        if (_hasLastPreferredTextSize &&
            Mathf.Abs(_lastPreferredTextWidth - width) < 0.01f &&
            string.Equals(_lastPreferredTextValue, text ?? "", System.StringComparison.Ordinal))
        {
            preferredSize = _lastPreferredTextSize;
            return true;
        }

        preferredSize = Vector2.zero;
        return false;
    }

    private void Reset()
    {
        AutoWire();
        CaptureCurrentTop();

        if (!Application.isPlaying)
            return;

        ApplyNow();
    }

    private void Awake()
    {
        AutoWire();
        if (!Application.isPlaying)
            return;

        if (_captureTopOnEnable)
            CaptureCurrentTop();
        ApplyNow();
    }

    private void OnEnable()
    {
        AutoWire();
        if (!Application.isPlaying)
        {
            CaptureCurrentTop();
            return;
        }

        if (_captureTopOnEnable)
            CaptureCurrentTop();
        ApplyNow();
    }

    private void OnValidate()
    {
        ValidateLayoutValues();
        ValidateStoryOverrides();
        AutoWire();

        if (Application.isPlaying)
        {
            ApplyNow();
            return;
        }

#if UNITY_EDITOR
        if (UnityEditor.Selection.activeGameObject == gameObject)
            ApplyNow();
#endif
    }

    private void ValidateLayoutValues()
    {
        _extraHeight = Mathf.Max(0f, _extraHeight);
        _minHeight = Mathf.Max(0f, _minHeight);
        _maxHeight = Mathf.Max(0f, _maxHeight);
        _maxFontSize = Mathf.Max(0f, _maxFontSize);
        _minAutoFontSize = Mathf.Max(1f, _minAutoFontSize);

        if (_maxHeight > 0f && _maxHeight < _minHeight)
            _maxHeight = _minHeight;

        if (_maxFontSize > 0f && _minAutoFontSize > _maxFontSize)
            _minAutoFontSize = _maxFontSize;

        _backgroundPadding = MaxVector2(_backgroundPadding, 0f);
        _backgroundMinSize = MaxVector2(_backgroundMinSize, 0f);
        _backgroundMaxSize = MaxVector2(_backgroundMaxSize, 0f);
        _backgroundGrowthUpFactor = Mathf.Max(0f, _backgroundGrowthUpFactor);

        if (_backgroundMaxSize.x > 0f && _backgroundMaxSize.x < _backgroundMinSize.x)
            _backgroundMaxSize.x = _backgroundMinSize.x;
        if (_backgroundMaxSize.y > 0f && _backgroundMaxSize.y < _backgroundMinSize.y)
            _backgroundMaxSize.y = _backgroundMinSize.y;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !_lockTopEveryFrame || !isActiveAndEnabled)
            return;

        if (HasObservedLayoutInputChanged() || HasLockedRectDrifted())
            ApplyNow();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (Application.isPlaying && !_applying)
        {
            _layoutDirty = true;
            _hasLastPreferredTextSize = false;
        }
    }

    public void MarkDirty()
    {
        _layoutDirty = true;
        _hasLastPreferredTextSize = false;
    }

    public void ApplyIfDirtyNow()
    {
        if (_layoutDirty || HasObservedLayoutInputChanged())
            ApplyNow();
    }

    public void CaptureCurrentTop()
    {
        AutoWire();
        RectTransform lockRoot = GetLockRoot();
        if (lockRoot == null)
            return;

        bool containerMode = IsBackgroundContainerMode();
        if (containerMode && _target != null)
        {
            _lockedTopY = GetTopY(lockRoot);
            _lockedAnchoredX = lockRoot.anchoredPosition.x;
            _lockedRootHeight = Mathf.Max(1f, _target.rect.height);
            _lockedTextAnchoredY = _target.anchoredPosition.y - ResolveEffectiveTopOffsetY();
        }
        else
        {
            _lockedTopY = GetTopY(lockRoot) - ResolveEffectiveTopOffsetY();
            _lockedAnchoredX = lockRoot.anchoredPosition.x - ResolveEffectiveOffsetX();
            _lockedRootHeight = Mathf.Max(1f, lockRoot.rect.height);
            _lockedTextAnchoredY = _target != null ? _target.anchoredPosition.y : 0f;
        }

        _capturedLockRoot = lockRoot;
        _capturedContainerMode = containerMode;
        _hasLockedTop = true;
    }

    public void CaptureBaseLayoutFromCurrentRect()
    {
        AutoWire();
        RectTransform lockRoot = GetLockRoot();
        if (lockRoot == null)
            return;

        bool containerMode = IsBackgroundContainerMode();
        _lockedTopY = GetTopY(lockRoot);
        _lockedAnchoredX = lockRoot.anchoredPosition.x;
        _lockedRootHeight = Mathf.Max(1f, containerMode && _target != null ? _target.rect.height : lockRoot.rect.height);
        _lockedTextAnchoredY = containerMode && _target != null
            ? _target.anchoredPosition.y - ResolveEffectiveTopOffsetY()
            : _target != null ? _target.anchoredPosition.y : 0f;
        _capturedLockRoot = lockRoot;
        _capturedContainerMode = containerMode;
        _hasLockedTop = true;
    }

    public void SetTopOffsetY(float topOffsetY, bool recaptureTop = false)
    {
        if (Mathf.Approximately(_topOffsetY, topOffsetY))
            return;

        _topOffsetY = topOffsetY;

        if (recaptureTop)
            CaptureCurrentTop();

        ApplyNow();
    }

    public void SetOffsetX(float offsetX, bool recaptureTop = false)
    {
        if (Mathf.Approximately(_offsetX, offsetX))
            return;

        _offsetX = offsetX;

        if (recaptureTop)
            CaptureCurrentTop();

        ApplyNow();
    }

    public void SetOffsets(float offsetX, float topOffsetY, bool recaptureTop = false)
    {
        bool changed = !Mathf.Approximately(_offsetX, offsetX) ||
                       !Mathf.Approximately(_topOffsetY, topOffsetY);
        if (!changed)
            return;

        _offsetX = offsetX;
        _topOffsetY = topOffsetY;

        if (recaptureTop)
            CaptureCurrentTop();

        ApplyNow();
    }

    public void ApplyLayoutOverrides(
        bool overrideResizeHeightToPreferredText,
        bool resizeHeightToPreferredText,
        bool overrideExtraHeight,
        float extraHeight,
        bool overrideMinHeight,
        float minHeight,
        bool overrideMaxHeight,
        float maxHeight,
        bool overrideMaxFontSize,
        float maxFontSize,
        bool overrideShrinkTextToFitRect,
        bool shrinkTextToFitRect,
        bool overrideMinAutoFontSize,
        float minAutoFontSize,
        bool overrideOverflowModeWhenStillTooLarge,
        TextOverflowModes overflowModeWhenStillTooLarge)
    {
        bool changed = false;

        if (overrideResizeHeightToPreferredText && _resizeHeightToPreferredText != resizeHeightToPreferredText)
        {
            _resizeHeightToPreferredText = resizeHeightToPreferredText;
            changed = true;
        }
        if (overrideExtraHeight && !Mathf.Approximately(_extraHeight, extraHeight))
        {
            _extraHeight = extraHeight;
            changed = true;
        }
        if (overrideMinHeight && !Mathf.Approximately(_minHeight, minHeight))
        {
            _minHeight = minHeight;
            changed = true;
        }
        if (overrideMaxHeight && !Mathf.Approximately(_maxHeight, maxHeight))
        {
            _maxHeight = maxHeight;
            changed = true;
        }
        if (overrideMaxFontSize && !Mathf.Approximately(_maxFontSize, maxFontSize))
        {
            _maxFontSize = maxFontSize;
            changed = true;
        }
        if (overrideShrinkTextToFitRect && _shrinkTextToFitRect != shrinkTextToFitRect)
        {
            _shrinkTextToFitRect = shrinkTextToFitRect;
            changed = true;
        }
        if (overrideMinAutoFontSize && !Mathf.Approximately(_minAutoFontSize, minAutoFontSize))
        {
            _minAutoFontSize = minAutoFontSize;
            changed = true;
        }
        if (overrideOverflowModeWhenStillTooLarge && _overflowModeWhenStillTooLarge != overflowModeWhenStillTooLarge)
        {
            _overflowModeWhenStillTooLarge = overflowModeWhenStillTooLarge;
            changed = true;
        }

        if (!changed)
            return;

        ValidateLayoutValues();
        _layoutDirty = true;
        _hasLastPreferredTextSize = false;
        ApplyNow();
    }

    public void ApplyNow()
    {
        if (_applying)
            return;

        AutoWire();
        if (_target == null)
            return;

        _applying = true;
        try
        {
            bool containerMode = IsBackgroundContainerMode();
            RectTransform lockRoot = GetLockRoot();
            if (!_hasLockedTop || _capturedLockRoot != lockRoot || _capturedContainerMode != containerMode)
                CaptureCurrentTop();

            ResolvedTextLayoutSettings layoutSettings = ResolveEffectiveTextLayoutSettings();
            if (containerMode)
                ApplyBackgroundContainerLayout(layoutSettings);
            else
                ApplyLegacyTargetLayout(layoutSettings);
        }
        finally
        {
            _applying = false;
            RememberAppliedLayoutState();
            _layoutDirty = false;
        }
    }

    public void SetTextAndApply(string value)
    {
        AutoWire();
        if (_text != null)
        {
            string safeValue = value ?? "";
            if (_text.text != safeValue)
                _text.SetText(safeValue);
        }

        _layoutDirty = true;
        _hasLastPreferredTextSize = false;
        ApplyNow();
    }

    private void ApplyLegacyTargetLayout(ResolvedTextLayoutSettings layoutSettings)
    {
        float topY = GetTargetTopY();
        float anchoredX = GetTargetAnchoredX();

        if (_forceTopPivot)
            SetPivotPreservingTop(_target, new Vector2(_target.pivot.x, 1f), topY);

        if (_text != null && _forceTextTopAlignment)
            _text.verticalAlignment = VerticalAlignmentOptions.Top;

        if (_text != null)
            ApplyTextSizeLimits(layoutSettings);

        TextLayoutMetrics metrics = CalculateTextLayoutMetrics(layoutSettings);
        ApplyTextRect(metrics, topY, anchoredX);

        if (_lockTopEveryFrame)
            SetTopY(_target, topY);

        ApplyBackgroundSizeToText(layoutSettings, metrics);

        if (_lockTopEveryFrame)
            SetTopY(_target, topY);
    }

    private void ApplyBackgroundContainerLayout(ResolvedTextLayoutSettings layoutSettings)
    {
        RectTransform background = _backgroundTarget;
        if (background == null || _target == null)
            return;

        float backgroundBaseTopY = _lockedTopY;

        if (_forceTopPivot)
        {
            SetPivotPreservingTop(background, new Vector2(background.pivot.x, 1f), backgroundBaseTopY);
            _target.pivot = new Vector2(_target.pivot.x, 1f);
        }

        if (_text != null && _forceTextTopAlignment)
            _text.verticalAlignment = VerticalAlignmentOptions.Top;

        if (_text != null)
            ApplyTextSizeLimits(layoutSettings);

        TextLayoutMetrics metrics = CalculateTextLayoutMetrics(layoutSettings);
        Vector2 backgroundSize = metrics.BackgroundSize;
        float growthTopShift = (metrics.TextSize.y - _lockedRootHeight) * layoutSettings.BackgroundGrowthUpFactor;
        float backgroundTopY = backgroundBaseTopY + growthTopShift;

        SetSize(background, backgroundSize);
        SetAnchoredX(background, _lockedAnchoredX);
        SetTopY(background, backgroundTopY);

        ApplyTextRectInsideBackground(metrics, layoutSettings);
    }

    private void ApplyTextRectInsideBackground(TextLayoutMetrics metrics, ResolvedTextLayoutSettings layoutSettings)
    {
        if (_target == null)
            return;

        // В контейнерном режиме BodyText живёт только локально внутри Background.
        _target.anchorMin = new Vector2(0.5f, 1f);
        _target.anchorMax = new Vector2(0.5f, 1f);
        _target.pivot = new Vector2(0.5f, 1f);
        SetSize(_target, metrics.TextSize);
        _target.anchoredPosition = new Vector2(
            ResolveEffectiveOffsetX(),
            _lockedTextAnchoredY + ResolveEffectiveTopOffsetY());
    }

    private TextLayoutMetrics CalculateTextLayoutMetrics(ResolvedTextLayoutSettings layoutSettings)
    {
        Vector2 maxBackgroundSize = ResolveMaxBackgroundSize(layoutSettings);
        float maxTextWidth = ResolveMaxTextWidth(layoutSettings);
        float maxTextHeight = ResolveMaxTextHeight(layoutSettings);
        string value = _text != null ? _text.text ?? "" : "";

        float textWidth;
        Vector2 preferred;

        if (layoutSettings.OverrideTextWidth && layoutSettings.TextWidth > 0f)
        {
            textWidth = Mathf.Max(1f, layoutSettings.TextWidth);
            if (maxTextWidth > 0f)
                textWidth = Mathf.Min(textWidth, maxTextWidth);

            preferred = GetPreferredTextSize(value, textWidth, Mathf.Infinity);
        }
        else
        {
            Vector2 naturalPreferred = GetPreferredTextSize(value, Mathf.Infinity, Mathf.Infinity);
            textWidth = ResolveTextWidth(layoutSettings, naturalPreferred.x, maxTextWidth);
            preferred = GetPreferredTextSize(value, textWidth, Mathf.Infinity);

            if (preferred.x > 0f && preferred.x < textWidth)
            {
                textWidth = Mathf.Clamp(preferred.x, 1f, maxTextWidth);
                preferred = GetPreferredTextSize(value, textWidth, Mathf.Infinity);
            }
        }

        _lastPreferredTextValue = value;
        _lastPreferredTextWidth = textWidth;
        _lastPreferredTextSize = preferred;
        _hasLastPreferredTextSize = true;

        float textHeight = layoutSettings.ResizeHeightToPreferredText
            ? Mathf.Max(1f, preferred.y) + layoutSettings.ExtraHeight
            : Mathf.Max(1f, _target.rect.height);

        textHeight = ClampHeight(textHeight, layoutSettings);
        if (maxTextHeight > 0f)
            textHeight = Mathf.Min(textHeight, maxTextHeight);

        Vector2 textSize = new Vector2(textWidth, Mathf.Max(1f, textHeight));
        Vector2 backgroundSize = ResolveBackgroundSize(textSize, layoutSettings);
        backgroundSize = ClampBackgroundSizeToContainer(backgroundSize, maxBackgroundSize);

        return new TextLayoutMetrics
        {
            MaxTextWidth = maxTextWidth,
            MaxTextHeight = maxTextHeight,
            TextWidth = textSize.x,
            TextHeight = textSize.y,
            TextSize = textSize,
            BackgroundSize = backgroundSize
        };
    }

    private void ApplyTextRect(TextLayoutMetrics metrics, float topY, float anchoredX)
    {
        if (_target == null)
            return;

        SetSize(_target, metrics.TextSize);
        SetAnchoredX(_target, anchoredX);
        SetTopY(_target, topY);
    }

    private float ResolveTextWidth(ResolvedTextLayoutSettings layoutSettings, float preferredWidth, float maxTextWidth)
    {
        float width = layoutSettings.OverrideTextWidth && layoutSettings.TextWidth > 0f
            ? layoutSettings.TextWidth
            : preferredWidth;

        if (!IsFinite(width) || width <= 0f)
            width = maxTextWidth;

        maxTextWidth = Mathf.Max(1f, maxTextWidth);
        return Mathf.Clamp(width, 1f, maxTextWidth);
    }

    private float ResolveMaxTextWidth(ResolvedTextLayoutSettings layoutSettings)
    {
        float maxWidth = layoutSettings.OverrideTextWidth && layoutSettings.TextWidth > 0f
            ? layoutSettings.TextWidth
            : ResolveDefaultAvailableTextWidth();

        return Mathf.Max(1f, maxWidth);
    }

    private float ResolveDefaultAvailableTextWidth()
    {
        float width = 0f;

        RectTransform parent = _target != null ? _target.parent as RectTransform : null;
        if (parent != null)
            width = Mathf.Max(width, Mathf.Abs(parent.rect.width));

        if (_backgroundTarget != null)
        {
            RectTransform backgroundParent = _backgroundTarget.parent as RectTransform;
            if (backgroundParent != null)
                width = Mathf.Max(width, Mathf.Abs(backgroundParent.rect.width));
        }

        if (width <= 1f)
            width = 760f;

        return width;
    }

    private float ResolveMaxTextHeight(ResolvedTextLayoutSettings layoutSettings)
    {
        return Mathf.Max(0f, layoutSettings.MaxHeight);
    }

    private Vector2 GetPreferredTextSize(string value, float width, float height)
    {
        if (_text == null)
            return _target != null ? _target.rect.size : Vector2.one;

        Vector2 preferred = _text.GetPreferredValues(value ?? "", width, height);
        if (!IsFinite(preferred.x) || preferred.x <= 0f)
            preferred.x = 1f;
        if (!IsFinite(preferred.y) || preferred.y <= 0f)
            preferred.y = Mathf.Max(1f, _text.fontSize);

        return preferred;
    }

    private bool HasObservedLayoutInputChanged()
    {
        if (_layoutDirty)
            return true;

        string text = _text != null ? _text.text ?? "" : "";
        if (!string.Equals(text, _lastAppliedText, StringComparison.Ordinal))
            return true;

        string storyId = _useStoryOverrides ? ResolveActiveStoryId() : "";
        if (!string.Equals(storyId, _lastAppliedStoryId, StringComparison.Ordinal))
            return true;

        RectTransform parent = _target != null ? _target.parent as RectTransform : null;
        Vector2 parentSize = parent != null ? parent.rect.size : Vector2.zero;
        return !Approximately(parentSize, _lastAppliedParentSize);
    }

    private bool HasLockedRectDrifted()
    {
        if (!_hasLockedTop)
            return false;

        RectTransform lockRoot = GetLockRoot();
        if (lockRoot == null)
            return false;

        return Mathf.Abs(GetTopY(lockRoot) - _lastAppliedLockRootTopY) > 0.05f ||
               Mathf.Abs(lockRoot.anchoredPosition.x - _lastAppliedLockRootX) > 0.05f;
    }

    private void RememberAppliedLayoutState()
    {
        _lastAppliedText = _text != null ? _text.text ?? "" : "";
        _lastAppliedStoryId = _useStoryOverrides ? ResolveActiveStoryId() : "";

        RectTransform parent = _target != null ? _target.parent as RectTransform : null;
        _lastAppliedParentSize = parent != null ? parent.rect.size : Vector2.zero;

        RectTransform lockRoot = GetLockRoot();
        if (lockRoot != null)
        {
            _lastAppliedLockRootTopY = GetTopY(lockRoot);
            _lastAppliedLockRootX = lockRoot.anchoredPosition.x;
        }
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) <= 0.05f && Mathf.Abs(a.y - b.y) <= 0.05f;
    }

    private float GetTargetTopY()
    {
        return _lockedTopY + ResolveEffectiveTopOffsetY();
    }

    private float GetTargetAnchoredX()
    {
        return _lockedAnchoredX + ResolveEffectiveOffsetX();
    }

    private RectTransform GetLockRoot()
    {
        return IsBackgroundContainerMode() ? _backgroundTarget : _target;
    }

    private bool IsBackgroundContainerMode()
    {
        return _resizeBackgroundToText &&
               _backgroundTarget != null &&
               _target != null &&
               _backgroundTarget != _target &&
               _target.parent == _backgroundTarget;
    }

    private float ResolveEffectiveTopOffsetY()
    {
        if (_useStoryOverrides &&
            TryGetStoryTopOffsetY(ResolveActiveStoryId(), out float storyTopOffsetY))
        {
            return storyTopOffsetY;
        }

        return _topOffsetY;
    }

    private float ResolveEffectiveOffsetX()
    {
        if (_useStoryOverrides &&
            TryGetStoryOffsetX(ResolveActiveStoryId(), out float storyOffsetX))
        {
            return storyOffsetX;
        }

        return _offsetX;
    }

    private ResolvedTextLayoutSettings ResolveEffectiveTextLayoutSettings()
    {
        ResolvedTextLayoutSettings settings = new ResolvedTextLayoutSettings
        {
            ResizeHeightToPreferredText = _resizeHeightToPreferredText,
            ExtraHeight = _extraHeight,
            MinHeight = _minHeight,
            MaxHeight = _maxHeight,
            TextWidth = 0f,
            BackgroundPadding = _backgroundPadding,
            BackgroundMinSize = _backgroundMinSize,
            BackgroundMaxSize = _backgroundMaxSize,
            BackgroundGrowthUpFactor = _backgroundGrowthUpFactor,
            MaxFontSize = _maxFontSize,
            ShrinkTextToFitRect = _shrinkTextToFitRect,
            MinAutoFontSize = _minAutoFontSize,
            OverflowModeWhenStillTooLarge = _overflowModeWhenStillTooLarge
        };

        if (_useStoryOverrides && TryGetStoryOverride(ResolveActiveStoryId(), out StoryTextLayoutOverride storyOverride))
        {
            if (storyOverride.OverrideTextWidth)
            {
                settings.OverrideTextWidth = true;
                settings.TextWidth = storyOverride.TextWidth;
            }
            if (storyOverride.OverrideBackgroundPadding)
                settings.BackgroundPadding = storyOverride.BackgroundPadding;
            if (storyOverride.OverrideBackgroundMinSize)
                settings.BackgroundMinSize = storyOverride.BackgroundMinSize;
            if (storyOverride.OverrideBackgroundMaxSize)
                settings.BackgroundMaxSize = storyOverride.BackgroundMaxSize;
            if (storyOverride.OverrideBackgroundGrowthUpFactor)
                settings.BackgroundGrowthUpFactor = storyOverride.BackgroundGrowthUpFactor;
            if (storyOverride.OverrideResizeHeightToPreferredText)
                settings.ResizeHeightToPreferredText = storyOverride.ResizeHeightToPreferredText;
            if (storyOverride.OverrideExtraHeight)
                settings.ExtraHeight = storyOverride.ExtraHeight;
            if (storyOverride.OverrideMinHeight)
                settings.MinHeight = storyOverride.MinHeight;
            if (storyOverride.OverrideMaxHeight)
                settings.MaxHeight = storyOverride.MaxHeight;
            if (storyOverride.OverrideMaxFontSize)
                settings.MaxFontSize = storyOverride.MaxFontSize;
            if (storyOverride.OverrideShrinkTextToFitRect)
                settings.ShrinkTextToFitRect = storyOverride.ShrinkTextToFitRect;
            if (storyOverride.OverrideMinAutoFontSize)
                settings.MinAutoFontSize = storyOverride.MinAutoFontSize;
            if (storyOverride.OverrideOverflowModeWhenStillTooLarge)
                settings.OverflowModeWhenStillTooLarge = storyOverride.OverflowModeWhenStillTooLarge;
        }

        ValidateTextLayoutSettings(ref settings);
        return settings;
    }

    private static void ValidateTextLayoutSettings(ref ResolvedTextLayoutSettings settings)
    {
        settings.ExtraHeight = Mathf.Max(0f, settings.ExtraHeight);
        settings.MinHeight = Mathf.Max(0f, settings.MinHeight);
        settings.MaxHeight = Mathf.Max(0f, settings.MaxHeight);
        settings.TextWidth = Mathf.Max(0f, settings.TextWidth);
        settings.BackgroundPadding = MaxVector2(settings.BackgroundPadding, 0f);
        settings.BackgroundMinSize = MaxVector2(settings.BackgroundMinSize, 0f);
        settings.BackgroundMaxSize = MaxVector2(settings.BackgroundMaxSize, 0f);
        settings.BackgroundGrowthUpFactor = Mathf.Max(0f, settings.BackgroundGrowthUpFactor);
        settings.MaxFontSize = Mathf.Max(0f, settings.MaxFontSize);
        settings.MinAutoFontSize = Mathf.Max(1f, settings.MinAutoFontSize);

        if (settings.MaxHeight > 0f && settings.MaxHeight < settings.MinHeight)
            settings.MaxHeight = settings.MinHeight;

        if (settings.MaxFontSize > 0f && settings.MinAutoFontSize > settings.MaxFontSize)
            settings.MinAutoFontSize = settings.MaxFontSize;

        if (settings.BackgroundMaxSize.x > 0f && settings.BackgroundMaxSize.x < settings.BackgroundMinSize.x)
            settings.BackgroundMaxSize.x = settings.BackgroundMinSize.x;
        if (settings.BackgroundMaxSize.y > 0f && settings.BackgroundMaxSize.y < settings.BackgroundMinSize.y)
            settings.BackgroundMaxSize.y = settings.BackgroundMinSize.y;
    }

    private bool TryGetStoryOverride(string storyId, out StoryTextLayoutOverride storyOverride)
    {
        storyOverride = null;

        if (string.IsNullOrWhiteSpace(storyId) || _storyOverrides == null)
            return false;

        for (int i = 0; i < _storyOverrides.Count; i++)
        {
            StoryTextLayoutOverride entry = _storyOverrides[i];
            if (entry == null || !entry.Matches(storyId))
                continue;

            storyOverride = entry;
            return true;
        }

        return false;
    }

    private bool TryGetStoryTopOffsetY(string storyId, out float topOffsetY)
    {
        topOffsetY = 0f;

        if (TryGetStoryOverride(storyId, out StoryTextLayoutOverride storyOverride))
        {
            topOffsetY = storyOverride.TopOffsetY;
            return true;
        }

        return false;
    }

    private bool TryGetStoryOffsetX(string storyId, out float offsetX)
    {
        offsetX = 0f;

        if (TryGetStoryOverride(storyId, out StoryTextLayoutOverride storyOverride))
        {
            offsetX = storyOverride.OffsetX;
            return true;
        }

        return false;
    }

    private string ResolveActiveStoryId()
    {
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

    private void ValidateStoryOverrides()
    {
        if (_storyOverrides == null)
        {
            _storyOverrides = new List<StoryTextLayoutOverride>();
            return;
        }

        for (int i = 0; i < _storyOverrides.Count; i++)
            _storyOverrides[i]?.Validate();

        _editorPreviewStoryId = NormalizeStoryId(_editorPreviewStoryId);
    }

    private static string NormalizeStoryId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }

    private void ApplyBackgroundSizeToText(ResolvedTextLayoutSettings layoutSettings, TextLayoutMetrics metrics)
    {
        if (!_resizeBackgroundToText || _target == null)
            return;

        AutoWireBackgroundTarget();
        if (_backgroundTarget == null || _backgroundTarget == _target)
            return;

        RectTransform backgroundParent = _backgroundTarget.parent as RectTransform;
        if (backgroundParent == null)
            return;

        if (!TryGetRectBoundsInParent(_target, backgroundParent, out Vector2 textCenter, out Vector2 textSize))
            return;

        // Размер плашки берём от уже пересчитанного TMP preferred size, а не от старого rect.
        Vector2 targetSize = ResolveBackgroundSize(metrics.TextSize, layoutSettings);
        targetSize = ClampBackgroundSizeToContainer(targetSize, ResolveMaxBackgroundSize(layoutSettings));
        if (targetSize.x <= 1f || targetSize.y <= 1f)
            targetSize = ResolveBackgroundSize(textSize, layoutSettings);

        RectWorldSnapshot textSnapshot = _preserveTextRectWhenResizingBackground
            ? CaptureWorldSnapshot(_target)
            : default;

        float previousBackgroundHeight = _backgroundTarget.rect.height;
        float growthShiftY = (targetSize.y - previousBackgroundHeight) * layoutSettings.BackgroundGrowthUpFactor;
        Vector2 desiredCenter = textCenter + new Vector2(0f, growthShiftY);
        desiredCenter = ClampCenterToVisibleBounds(backgroundParent, targetSize, desiredCenter);

        bool changed = SetSize(_backgroundTarget, targetSize);

        bool targetMovesWithBackground = IsChildOf(_target, _backgroundTarget);
        if (_centerBackgroundOnText && !targetMovesWithBackground)
            changed |= SetRectCenterInParent(_backgroundTarget, backgroundParent, desiredCenter);

        if (_centerBackgroundOnText && targetMovesWithBackground)
        {
            changed |= SetRectCenterInParent(_backgroundTarget, backgroundParent, desiredCenter);
        }
        else if (!_centerBackgroundOnText && Mathf.Abs(growthShiftY) >= 0.01f)
        {
            _backgroundTarget.anchoredPosition += new Vector2(0f, growthShiftY);
            changed = true;
        }

        if (changed && textSnapshot.Valid)
            RestoreWorldSnapshot(textSnapshot);
    }

    private Vector2 ResolveBackgroundSize(Vector2 textSize, ResolvedTextLayoutSettings layoutSettings)
    {
        Vector2 padding = layoutSettings.BackgroundPadding;
        Vector2 contentSize = new Vector2(
            Mathf.Max(1f, textSize.x),
            Mathf.Max(1f, textSize.y));

        if (layoutSettings.BackgroundMinSize.x > 0f)
            contentSize.x = Mathf.Max(contentSize.x, layoutSettings.BackgroundMinSize.x);
        if (layoutSettings.BackgroundMinSize.y > 0f)
            contentSize.y = Mathf.Max(contentSize.y, layoutSettings.BackgroundMinSize.y);

        Vector2 size = new Vector2(
            contentSize.x + padding.x * 2f,
            contentSize.y + padding.y * 2f);

        if (layoutSettings.BackgroundMaxSize.x > 0f)
            size.x = Mathf.Min(size.x, layoutSettings.BackgroundMaxSize.x);
        if (layoutSettings.BackgroundMaxSize.y > 0f)
            size.y = Mathf.Min(size.y, layoutSettings.BackgroundMaxSize.y);

        return size;
    }

    private Vector2 ResolveMaxBackgroundSize(ResolvedTextLayoutSettings layoutSettings)
    {
        Vector2 maxSize = layoutSettings.BackgroundMaxSize;

        if (maxSize.x > 0f)
            maxSize.x = Mathf.Max(1f, maxSize.x);
        if (maxSize.y > 0f)
            maxSize.y = Mathf.Max(1f, maxSize.y);

        return maxSize;
    }

    private Vector2 ClampBackgroundSizeToContainer(Vector2 size, Vector2 maxSize)
    {
        if (maxSize.x > 0f)
            size.x = Mathf.Min(size.x, maxSize.x);
        if (maxSize.y > 0f)
            size.y = Mathf.Min(size.y, maxSize.y);

        return new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
    }

    private Vector2 ClampCenterToVisibleBounds(RectTransform parent, Vector2 size, Vector2 center)
    {
        if (!TryGetVisibleBoundsInParent(parent, out Vector2 min, out Vector2 max))
            return center;

        Vector2 halfSize = size * 0.5f;
        center.x = ClampCenterAxis(center.x, min.x, max.x, halfSize.x);
        center.y = ClampCenterAxis(center.y, min.y, max.y, halfSize.y);
        return center;
    }

    private void ClampRectToVisibleBounds(RectTransform rect)
    {
        RectTransform parent = rect != null ? rect.parent as RectTransform : null;
        if (parent == null ||
            !TryGetRectBoundsInParent(rect, parent, out Vector2 currentCenter, out Vector2 size))
        {
            return;
        }

        Vector2 clampedCenter = ClampCenterToVisibleBounds(parent, size, currentCenter);
        SetRectCenterInParent(rect, parent, clampedCenter);
    }

    private static float ClampCenterAxis(float value, float min, float max, float halfSize)
    {
        if (max <= min)
            return value;

        if (halfSize * 2f >= max - min)
            return (min + max) * 0.5f;

        return Mathf.Clamp(value, min + halfSize, max - halfSize);
    }

    private bool TryGetVisibleBoundsInParent(RectTransform parent, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;
        if (parent == null)
            return false;

        Rect rect = parent.rect;
        min = rect.min;
        max = rect.max;

        Canvas canvas = _target != null ? _target.GetComponentInParent<Canvas>() : null;
        RectTransform canvasRect = canvas != null && canvas.rootCanvas != null
            ? canvas.rootCanvas.transform as RectTransform
            : null;

        if (canvasRect != null && canvasRect != parent &&
            TryGetRectBoundsMinMaxInParent(canvasRect, parent, out Vector2 canvasMin, out Vector2 canvasMax))
        {
            min = Vector2.Max(min, canvasMin);
            max = Vector2.Min(max, canvasMax);
        }

        return max.x > min.x && max.y > min.y;
    }

    private void AutoWireBackgroundTarget()
    {
        if (_target == null)
            return;

        if (_backgroundTarget != null && _backgroundTarget != _target)
            return;

        RectTransform parent = _target.parent as RectTransform;
        if (IsStrongBackgroundCandidate(parent))
        {
            _backgroundTarget = parent;
            return;
        }

        RectTransform siblingBackground = FindSiblingBackgroundTarget(parent);
        if (siblingBackground != null)
        {
            _backgroundTarget = siblingBackground;
            return;
        }

        if (parent != null && parent != _target)
            _backgroundTarget = parent;
    }

    private RectTransform FindSiblingBackgroundTarget(RectTransform parent)
    {
        if (parent == null)
            return null;

        RectTransform fallback = null;
        for (int i = 0; i < parent.childCount; i++)
        {
            RectTransform child = parent.GetChild(i) as RectTransform;
            if (child == null || child == _target)
                continue;

            string childName = child.name;
            if (string.Equals(childName, "Background", StringComparison.OrdinalIgnoreCase))
                return child;

            string normalizedName = childName.ToLowerInvariant();
            if (normalizedName.Contains("background"))
                return child;

            if (fallback == null &&
                (string.Equals(normalizedName, "image", StringComparison.OrdinalIgnoreCase) ||
                 normalizedName.Contains("panel") ||
                 child.GetComponent<UnityEngine.UI.Image>() != null))
            {
                fallback = child;
            }
        }

        return fallback;
    }

    private bool IsStrongBackgroundCandidate(RectTransform value)
    {
        if (value == null || value == _target)
            return false;

        string normalizedName = value.name.ToLowerInvariant();
        return normalizedName.Contains("background") ||
               value.GetComponent<UnityEngine.UI.Image>() != null;
    }

    private float ClampHeight(float value, ResolvedTextLayoutSettings layoutSettings)
    {
        if (layoutSettings.MinHeight > 0f)
            value = Mathf.Max(value, layoutSettings.MinHeight);

        if (layoutSettings.MaxHeight > 0f)
            value = Mathf.Min(value, layoutSettings.MaxHeight);

        return value;
    }

    private void AutoWire()
    {
        if (_text == null)
            _text = GetComponent<TMP_Text>();

        RectTransform ownRect = transform as RectTransform;
        if (_target == null)
            _target = ownRect;

        RepairTargetIfItPointsAtBackground(ownRect);
        AutoWireBackgroundTarget();
        CaptureInitialTextSettings();
    }

    private void RepairTargetIfItPointsAtBackground(RectTransform ownRect)
    {
        if (ownRect == null ||
            _target == null ||
            _target == ownRect ||
            _text == null ||
            _text.rectTransform != ownRect ||
            !ownRect.IsChildOf(_target))
        {
            return;
        }

        if (_backgroundTarget == null || _backgroundTarget == _target)
        {
            _backgroundTarget = _target;
            _target = ownRect;
        }
    }

    private void CaptureInitialTextSettings()
    {
        if (_hasInitialTextSettings || _text == null)
            return;

        _initialAutoSizing = _text.enableAutoSizing;
        _initialFontSize = _text.fontSize;
        _initialFontSizeMin = _text.fontSizeMin;
        _initialFontSizeMax = _text.fontSizeMax;
        _initialOverflowMode = _text.overflowMode;
        _hasInitialTextSettings = true;
    }

    private void ApplyTextSizeLimits(ResolvedTextLayoutSettings layoutSettings)
    {
        if (_text == null)
            return;

        CaptureInitialTextSettings();

        if (!_text.enableWordWrapping)
            _text.enableWordWrapping = true;

        float maxFontSize = ResolveMaxFontSize(layoutSettings);
        if (maxFontSize > 0f)
        {
            if (Mathf.Abs(_text.fontSizeMax - maxFontSize) >= 0.01f)
                _text.fontSizeMax = maxFontSize;
            if (Mathf.Abs(_text.fontSize - maxFontSize) >= 0.01f)
                _text.fontSize = maxFontSize;
        }
        else if (_hasInitialTextSettings)
        {
            float initialFontSize = Mathf.Max(1f, _initialFontSize);
            float initialFontSizeMax = Mathf.Max(1f, _initialFontSizeMax);
            if (Mathf.Abs(_text.fontSize - initialFontSize) >= 0.01f)
                _text.fontSize = initialFontSize;
            if (Mathf.Abs(_text.fontSizeMax - initialFontSizeMax) >= 0.01f)
                _text.fontSizeMax = initialFontSizeMax;
        }

        if (!layoutSettings.ShrinkTextToFitRect)
        {
            if (_text.enableAutoSizing)
                _text.enableAutoSizing = false;

            float minFontSize = maxFontSize > 0f
                ? Mathf.Min(Mathf.Max(1f, _text.fontSizeMin), maxFontSize)
                : _hasInitialTextSettings
                    ? Mathf.Max(1f, _initialFontSizeMin)
                    : _text.fontSizeMin;

            if (Mathf.Abs(_text.fontSizeMin - minFontSize) >= 0.01f)
                _text.fontSizeMin = minFontSize;

            if (_text.overflowMode != _initialOverflowMode)
                _text.overflowMode = _initialOverflowMode;

            return;
        }

        float autoMax = maxFontSize > 0f ? maxFontSize : ResolveInitialFontSizeMax();
        autoMax = Mathf.Max(1f, autoMax);
        float autoMin = Mathf.Min(Mathf.Max(1f, layoutSettings.MinAutoFontSize), autoMax);
        TextOverflowModes overflowMode = layoutSettings.OverflowModeWhenStillTooLarge == TextOverflowModes.Overflow
            ? TextOverflowModes.Ellipsis
            : layoutSettings.OverflowModeWhenStillTooLarge;

        if (!_text.enableAutoSizing)
            _text.enableAutoSizing = true;

        if (Mathf.Abs(_text.fontSizeMax - autoMax) >= 0.01f)
            _text.fontSizeMax = autoMax;
        if (Mathf.Abs(_text.fontSizeMin - autoMin) >= 0.01f)
            _text.fontSizeMin = autoMin;

        // Overflow не даём: иначе TMP рисует за пределами BodyText и визуально выходит из плашки.
        if (_text.overflowMode != overflowMode)
            _text.overflowMode = overflowMode;
    }

    private float ResolveMaxFontSize(ResolvedTextLayoutSettings layoutSettings)
    {
        if (layoutSettings.MaxFontSize > 0f)
            return Mathf.Max(1f, layoutSettings.MaxFontSize);

        return 0f;
    }

    private float ResolveInitialFontSizeMax()
    {
        if (!_hasInitialTextSettings)
            return Mathf.Max(1f, _text != null ? _text.fontSize : 1f);

        if (_initialAutoSizing && _initialFontSizeMax > 0f)
            return Mathf.Max(1f, _initialFontSizeMax);

        return Mathf.Max(1f, _initialFontSize, _initialFontSizeMax, _initialFontSizeMin);
    }

    private static void SetPivotPreservingTop(RectTransform target, Vector2 pivot, float topY)
    {
        if (target == null || target.pivot == pivot)
            return;

        target.pivot = pivot;
        SetTopY(target, topY);
    }

    private static float GetTopY(RectTransform target)
    {
        if (target == null)
            return 0f;

        Vector3[] corners = RectTransformCornerCache.Corners;
        target.GetWorldCorners(corners);

        RectTransform parent = target.parent as RectTransform;
        if (parent == null)
            return corners[1].y;

        return parent.InverseTransformPoint(corners[1]).y;
    }

    private static void SetTopY(RectTransform target, float topY)
    {
        if (target == null)
            return;

        float currentTopY = GetTopY(target);
        float deltaY = topY - currentTopY;

        if (Mathf.Abs(deltaY) < 0.01f)
            return;

        target.anchoredPosition += new Vector2(0f, deltaY);
    }

    private static void SetAnchoredX(RectTransform target, float anchoredX)
    {
        if (target == null)
            return;

        Vector2 position = target.anchoredPosition;
        if (Mathf.Abs(position.x - anchoredX) < 0.01f)
            return;

        position.x = anchoredX;
        target.anchoredPosition = position;
    }

    private static bool SetSize(RectTransform target, Vector2 size)
    {
        if (target == null)
            return false;

        bool changed = false;

        if (size.x > 0f && Mathf.Abs(target.rect.width - size.x) >= 0.01f)
        {
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            changed = true;
        }

        if (size.y > 0f && Mathf.Abs(target.rect.height - size.y) >= 0.01f)
        {
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            changed = true;
        }

        return changed;
    }

    private static bool TryGetRectBoundsInParent(
        RectTransform rect,
        RectTransform parent,
        out Vector2 center,
        out Vector2 size)
    {
        center = Vector2.zero;
        size = Vector2.zero;

        if (rect == null || parent == null)
            return false;

        Vector3[] corners = RectTransformCornerCache.Corners;
        rect.GetWorldCorners(corners);

        Vector2 min = parent.InverseTransformPoint(corners[0]);
        Vector2 max = min;

        for (int i = 1; i < corners.Length; i++)
        {
            Vector2 local = parent.InverseTransformPoint(corners[i]);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        size = max - min;
        center = (min + max) * 0.5f;
        return size.x > 0f && size.y > 0f;
    }

    private static bool TryGetRectBoundsMinMaxInParent(
        RectTransform rect,
        RectTransform parent,
        out Vector2 min,
        out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;

        if (rect == null || parent == null)
            return false;

        Vector3[] corners = RectTransformCornerCache.Corners;
        rect.GetWorldCorners(corners);

        min = parent.InverseTransformPoint(corners[0]);
        max = min;

        for (int i = 1; i < corners.Length; i++)
        {
            Vector2 local = parent.InverseTransformPoint(corners[i]);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return max.x > min.x && max.y > min.y;
    }

    private static RectWorldSnapshot CaptureWorldSnapshot(RectTransform target)
    {
        if (target == null)
            return default;

        Vector3[] corners = RectTransformCornerCache.Corners;
        target.GetWorldCorners(corners);

        return new RectWorldSnapshot
        {
            Valid = true,
            Target = target,
            BottomLeft = corners[0],
            TopRight = corners[2]
        };
    }

    private static void RestoreWorldSnapshot(RectWorldSnapshot snapshot)
    {
        RestoreWorldSnapshot(snapshot, Vector3.zero);
    }

    private static void RestoreWorldSnapshot(RectWorldSnapshot snapshot, Vector3 worldOffset)
    {
        RectTransform target = snapshot.Target;
        RectTransform parent = target != null ? target.parent as RectTransform : null;
        if (!snapshot.Valid || target == null || parent == null)
            return;

        Vector2 bottomLeft = parent.InverseTransformPoint(snapshot.BottomLeft + worldOffset);
        Vector2 topRight = parent.InverseTransformPoint(snapshot.TopRight + worldOffset);
        Vector2 size = new Vector2(
            Mathf.Abs(topRight.x - bottomLeft.x),
            Mathf.Abs(topRight.y - bottomLeft.y));
        Vector2 center = (bottomLeft + topRight) * 0.5f;

        SetSize(target, size);
        SetRectCenterInParent(target, parent, center);
    }

    private static Vector3 LocalDeltaToWorldDelta(RectTransform parent, Vector2 localDelta)
    {
        if (parent == null || localDelta.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        Vector3 worldOrigin = parent.TransformPoint(Vector3.zero);
        Vector3 worldTarget = parent.TransformPoint(new Vector3(localDelta.x, localDelta.y, 0f));
        return worldTarget - worldOrigin;
    }

    private static Vector2 WorldDeltaToLocalDelta(RectTransform parent, Vector3 worldDelta)
    {
        if (parent == null || worldDelta.sqrMagnitude < 0.0001f)
            return Vector2.zero;

        Vector3 worldOrigin = parent.TransformPoint(Vector3.zero);
        Vector3 localOrigin = parent.InverseTransformPoint(worldOrigin);
        Vector3 localTarget = parent.InverseTransformPoint(worldOrigin + worldDelta);
        return localTarget - localOrigin;
    }

    private static bool IsChildOf(RectTransform child, RectTransform parent)
    {
        return child != null && parent != null && child.transform.IsChildOf(parent.transform);
    }

    private static bool SetRectCenterInParent(RectTransform target, RectTransform parent, Vector2 desiredCenter)
    {
        if (target == null || parent == null)
            return false;

        if (!TryGetRectBoundsInParent(target, parent, out Vector2 currentCenter, out _))
            return false;

        Vector2 delta = desiredCenter - currentCenter;
        if (delta.sqrMagnitude < 0.0001f)
            return false;

        target.anchoredPosition += delta;
        return true;
    }

    private static Vector2 MaxVector2(Vector2 value, float min)
    {
        return new Vector2(Mathf.Max(min, value.x), Mathf.Max(min, value.y));
    }

    private static float MinPositive(float current, float candidate)
    {
        if (candidate <= 0f)
            return current;

        return current > 0f ? Mathf.Min(current, candidate) : candidate;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static class RectTransformCornerCache
    {
        public static readonly Vector3[] Corners = new Vector3[4];
    }
}
