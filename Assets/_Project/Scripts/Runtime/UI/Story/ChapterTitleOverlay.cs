using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ChapterTitleTextMode
{
    Auto,
    TitleOnly,
    NumberOnly,
    NumberAndTitle,
    CustomFormat
}

public enum ChapterTitleAnimationMode
{
    Fade,
    SlideFromTop,
    Instant
}

public enum ChapterTitleBackdropSizeMode
{
    FixedSize,
    StretchToParent
}

[DisallowMultipleComponent]
[AddComponentMenu("Novel Template/UI/Chapter Title Overlay")]
public sealed class ChapterTitleOverlay : MonoBehaviour
{
    private const string DefaultTextFormat = "{1}";

    [Header("Плашка главы")]
    [Tooltip("RectTransform самой плашки главы. Именно этот объект будет выезжать сверху из-за экрана и затем уезжать обратно вверх.")]
    [SerializeField] private RectTransform _panelRect;

    [Tooltip("TMP_Text внутри плашки. В него записывается название текущей главы, например \"ГЛАВА 1: ПОДЛЕСЬЕ\".")]
    [SerializeField] private TMP_Text _titleText;

    [Tooltip("CanvasGroup плашки для плавного появления и исчезновения. Если поле пустое, скрипт попробует взять CanvasGroup с этого же объекта.")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Tooltip("Корневой объект плашки. Если поле пустое, будет использован объект с этим скриптом.")]
    [SerializeField] private GameObject _rootObject;

    [SerializeField, HideInInspector] private string _editorPreviewStoryId;

    [Header("Положение и затемнение")]
    [Tooltip("Держать плашку в центре родительского UI во время показа.")]
    [SerializeField] private bool _centerOnShow = true;

    [Tooltip("Поднимать плашку и затемнение поверх остальных элементов этого UI-слоя.")]
    [SerializeField] private bool _bringToFrontOnShow = true;

    [Tooltip("CanvasGroup затемняющего слоя за плашкой. Если поле пустое, слой будет создан автоматически.")]
    [SerializeField] private CanvasGroup _backgroundDimCanvasGroup;

    [Tooltip("Image затемняющего слоя. Если поле пустое, слой будет создан автоматически.")]
    [SerializeField] private Image _backgroundDimImage;
    [SerializeField] private ChapterTitleBackdropSizeMode _backgroundDimSizeMode = ChapterTitleBackdropSizeMode.FixedSize;
    [SerializeField] private Vector2 _backgroundDimFixedSize = new Vector2(5000f, 5000f);

    [Tooltip("Цвет затемняющего слоя за плашкой.")]
    [SerializeField] private Color _backgroundDimColor = Color.black;

    [Tooltip("Итоговая сила затемнения фона во время показа плашки.")]
    [Range(0f, 1f)]
    [SerializeField] private float _backgroundDimAlpha = 0.6f;

    [Header("Текст главы")]
    [Tooltip("Формат текста. {0} - номер главы, {1} - название главы. По умолчанию используется готовое название главы из ChapterData.")]
    [SerializeField] private ChapterTitleTextMode _textMode = ChapterTitleTextMode.Auto;
    [SerializeField] private string _textFormat = DefaultTextFormat;
    [SerializeField] private string _numberAndTitleFormat = "\u0413\u041b\u0410\u0412\u0410 {0}: {1}";

    [Tooltip("Число, которое прибавляется к индексу главы. Для обычной нумерации с 1 оставь значение 1.")]
    [SerializeField] private int _chapterNumberOffset = 1;

    [Tooltip("Текст, который будет показан, если у главы пустое название. {0} - номер главы.")]
    [SerializeField] private string _emptyTitleFallback = "ГЛАВА {0}";

    [Tooltip("Обрезать лишние пробелы в начале и конце названия главы.")]
    [SerializeField] private bool _trimTitle = true;

    [Tooltip("Принудительно переводить название главы в верхний регистр. По умолчанию выключено, чтобы плашка показывала текст так, как он задан в ChapterData.")]
    [SerializeField] private bool _uppercaseTitle;

    [Header("Особый padding для отдельных названий")]
    [SerializeField] private bool _useSpecificTitlePadding = true;
    [SerializeField] private string[] _specificTitlePaddingMarkers =
    {
        "\u0422\u0410\u041c, \u0423 \u0412\u041e\u0414\u042b"
    };
    [SerializeField] private Vector2 _specificTitlePadding = new Vector2(390f, 72f);

    [Header("Движение сверху")]
    [Tooltip("Позиция плашки, когда она полностью видна. Если включён захват позиции, значение берётся из текущей позиции RectTransform при запуске.")]
    [SerializeField] private ChapterTitleAnimationMode _animationMode = ChapterTitleAnimationMode.Fade;
    [SerializeField] private Vector2 _shownAnchoredPosition;

    [Tooltip("При запуске сцены автоматически считать текущую позицию RectTransform видимой позицией плашки.")]
    [SerializeField] private bool _captureShownPositionOnAwake = true;

    [Tooltip("Насколько выше видимой позиции плашка прячется перед показом и после показа. Увеличь значение, если плашка не полностью уходит за верх экрана.")]
    [SerializeField] private float _hiddenOffsetY = 360f;

    [Tooltip("Сколько секунд плашка выезжает сверху до видимой позиции.")]
    [SerializeField] private float _enterDuration = 0.45f;

    [Tooltip("Сколько секунд плашка остаётся на экране после появления.")]
    [SerializeField] private float _visibleDuration = 1.35f;

    [Tooltip("Сколько секунд плашка уезжает вверх после показа.")]
    [SerializeField] private float _exitDuration = 1.35f;

    [Tooltip("Плавно менять прозрачность вместе с движением плашки.")]
    [SerializeField] private bool _fadeWithMovement = true;

    [Tooltip("Оставлено для совместимости со старым выездом сверху. Для центральной плашки должно быть выключено.")]
    [SerializeField] private bool _animatePosition;

    [Tooltip("Использовать время, независимое от Time.timeScale, чтобы анимация работала даже на паузе.")]
    [SerializeField] private bool _useUnscaledTime = true;

    [Tooltip("Выключать корневой объект после ухода плашки вверх. Обычно лучше оставить выключенным, чтобы ссылка в StoryManager оставалась стабильной.")]
    [SerializeField] private bool _disableRootAfterExit;

    private Coroutine _showRoutine;
    private ButtonTextAutoSize _titleAutoSize;
    private bool _hasDefaultTitlePadding;
    private Vector2 _defaultTitlePadding;
    private GameObject _runtimeBackgroundDimObject;
    private bool _storyStyleDefaultsCaptured;
    private ImageDefaults _storyStylePanelImageDefaults;
    private bool _storyStyleTextDefaultsCaptured;
    private Color _storyStyleTitleTextColor;
    private TMP_FontAsset _storyStyleTitleTextFont;
    private float _storyStyleTitleTextFontSize;
    private bool _storyStyleTitleTextAutoSize;
    private float _storyStyleTitleTextFontSizeMin;
    private float _storyStyleTitleTextFontSizeMax;
    private TextAlignmentOptions _storyStyleTitleTextAlignment;
    private bool _storyStyleTitleTextWordWrapping;
    private TextOverflowModes _storyStyleTitleTextOverflowMode;
    private float _storyStyleTitleTextLineSpacing;
    private Vector4 _storyStyleTitleTextMargin;
    private Vector2 _storyStyleTitleTextAnchorMin;
    private Vector2 _storyStyleTitleTextAnchorMax;
    private Vector2 _storyStyleTitleTextAnchoredPosition;
    private Vector2 _storyStyleTitleTextSizeDelta;
    private Vector2 _storyStyleTitleTextPivot;
    private bool _storyStyleManualTitleTextRect;
    private bool _storyStyleControlsTitleTextOverflow;
    private bool _storyStyleControlsTitleTextAutoSize;
    private bool _storyStyleControlsTitleTextWordWrapping;

    public TMP_Text TitleText => _titleText;
    public Image PanelBackgroundImage
    {
        get
        {
            EnsureReferences();
            return FindPanelBackgroundImage();
        }
    }
    public bool IsShowing => _showRoutine != null;

    public void SetStoryStyleTitleTextControl(
        bool manualTextRect,
        bool controlsOverflow,
        bool controlsAutoSize,
        bool controlsWordWrapping)
    {
        _storyStyleManualTitleTextRect = manualTextRect;
        _storyStyleControlsTitleTextOverflow = controlsOverflow;
        _storyStyleControlsTitleTextAutoSize = controlsAutoSize;
        _storyStyleControlsTitleTextWordWrapping = controlsWordWrapping;
        ApplyTitleAutoSizeSuspension();
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

    private void Reset()
    {
        _panelRect = transform as RectTransform;
        _titleText = GetComponentInChildren<TMP_Text>(true);
        _canvasGroup = GetComponent<CanvasGroup>();
        _rootObject = gameObject;

        if (_panelRect != null)
            _shownAnchoredPosition = _panelRect.anchoredPosition;

        _centerOnShow = true;
        _bringToFrontOnShow = true;
        _backgroundDimColor = Color.black;
        _backgroundDimAlpha = 0.6f;
        _animatePosition = false;
    }

    private void Awake()
    {
        EnsureReferences();

        if (_captureShownPositionOnAwake && _panelRect != null)
            _shownAnchoredPosition = _panelRect.anchoredPosition;

        PreparePanelForCenteredDisplay();
        HideInstant();
    }

    private void OnValidate()
    {
        _hiddenOffsetY = Mathf.Max(0f, _hiddenOffsetY);
        _enterDuration = Mathf.Max(0f, _enterDuration);
        _visibleDuration = Mathf.Max(0f, _visibleDuration);
        _exitDuration = Mathf.Max(0f, _exitDuration);
        _backgroundDimAlpha = Mathf.Clamp01(_backgroundDimAlpha);
        _backgroundDimFixedSize = new Vector2(
            Mathf.Max(1f, _backgroundDimFixedSize.x),
            Mathf.Max(1f, _backgroundDimFixedSize.y));

        if (string.IsNullOrEmpty(_textFormat))
            _textFormat = DefaultTextFormat;

        if (string.IsNullOrEmpty(_numberAndTitleFormat))
            _numberAndTitleFormat = "\u0413\u041b\u0410\u0412\u0410 {0}: {1}";

        if (_rootObject == null)
            _rootObject = gameObject;

        if (_panelRect == null)
            _panelRect = transform as RectTransform;

        if (_titleText == null)
            _titleText = GetComponentInChildren<TMP_Text>(true);

        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        if (_backgroundDimCanvasGroup == null && _backgroundDimImage != null)
            _backgroundDimCanvasGroup = _backgroundDimImage.GetComponent<CanvasGroup>();

        if (_backgroundDimImage == null && _backgroundDimCanvasGroup != null)
            _backgroundDimImage = _backgroundDimCanvasGroup.GetComponent<Image>();

        _specificTitlePadding = new Vector2(
            Mathf.Max(0f, _specificTitlePadding.x),
            Mathf.Max(0f, _specificTitlePadding.y));

        if (_centerOnShow && _panelRect != null)
            _shownAnchoredPosition = Vector2.zero;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall -= RefreshTitleLayoutInEditor;
            UnityEditor.EditorApplication.delayCall += RefreshTitleLayoutInEditor;
        }
#endif
    }

#if UNITY_EDITOR
    private void RefreshTitleLayoutInEditor()
    {
        if (this == null)
            return;

        RefreshTitleLayout();
    }
#endif

    private void OnDisable()
    {
        StopShowRoutine();
        SetVisibility(0f);
    }

    private void OnDestroy()
    {
        if (_runtimeBackgroundDimObject != null)
            Destroy(_runtimeBackgroundDimObject);
    }

    public Coroutine ShowChapter(ChapterData chapter, int zeroBasedChapterIndex)
    {
        string chapterTitle = chapter != null ? chapter.ChapterName : "";
        return ShowChapter(zeroBasedChapterIndex, chapterTitle);
    }

    public Coroutine ShowChapter(int zeroBasedChapterIndex, string chapterTitle)
    {
        return ShowText(BuildChapterText(zeroBasedChapterIndex, chapterTitle));
    }

    public Coroutine ShowText(string text)
    {
        EnsureReferences();

        if (_titleText == null)
        {
            Debug.LogWarning("ChapterTitleOverlay: TMP_Text не назначен.", this);
            return null;
        }

        if (_rootObject != null && !_rootObject.activeSelf)
            _rootObject.SetActive(true);

        StopShowRoutine();
        PreparePanelForDisplay();
        EnsureBackgroundDim();
        PlaceBackgroundDimBehindOverlay();

        if (!isActiveAndEnabled)
        {
            _titleText.text = text ?? "";
            RefreshTitleLayout();
            SetPanelPosition(ResolveShownPosition());
            SetVisibility(1f);
            return null;
        }

        _showRoutine = StartCoroutine(ShowRoutine(text ?? ""));
        return _showRoutine;
    }

    public void ApplyStoryUiStyle(StoryUiStyle style)
    {
        EnsureReferences();
        CaptureStoryStyleDefaults();
        RestoreStoryStyleDefaults();

        if (style != null)
            style.ApplyToChapterTitleOverlay(this);
    }

    public void ApplyStorySettingsOverrides(
        bool overrideCenterOnShow,
        bool centerOnShow,
        bool overrideBringToFrontOnShow,
        bool bringToFrontOnShow,
        bool overrideBackgroundDimSizeMode,
        ChapterTitleBackdropSizeMode backgroundDimSizeMode,
        bool overrideBackgroundDimFixedSize,
        Vector2 backgroundDimFixedSize,
        bool overrideBackgroundDimColor,
        Color backgroundDimColor,
        bool overrideBackgroundDimAlpha,
        float backgroundDimAlpha,
        bool overrideTextMode,
        ChapterTitleTextMode textMode,
        bool overrideTextFormat,
        string textFormat,
        bool overrideNumberAndTitleFormat,
        string numberAndTitleFormat,
        bool overrideChapterNumberOffset,
        int chapterNumberOffset,
        bool overrideEmptyTitleFallback,
        string emptyTitleFallback,
        bool overrideTrimTitle,
        bool trimTitle,
        bool overrideUppercaseTitle,
        bool uppercaseTitle,
        bool overrideSpecificTitlePaddingSettings,
        bool useSpecificTitlePadding,
        string[] specificTitlePaddingMarkers,
        Vector2 specificTitlePadding,
        bool overrideAnimationMode,
        ChapterTitleAnimationMode animationMode,
        bool overrideShownAnchoredPosition,
        Vector2 shownAnchoredPosition,
        bool overrideCaptureShownPositionOnAwake,
        bool captureShownPositionOnAwake,
        bool overrideHiddenOffsetY,
        float hiddenOffsetY,
        bool overrideEnterDuration,
        float enterDuration,
        bool overrideVisibleDuration,
        float visibleDuration,
        bool overrideExitDuration,
        float exitDuration,
        bool overrideFadeWithMovement,
        bool fadeWithMovement,
        bool overrideAnimatePosition,
        bool animatePosition,
        bool overrideUseUnscaledTime,
        bool useUnscaledTime,
        bool overrideDisableRootAfterExit,
        bool disableRootAfterExit)
    {
        if (overrideCenterOnShow)
            _centerOnShow = centerOnShow;
        if (overrideBringToFrontOnShow)
            _bringToFrontOnShow = bringToFrontOnShow;
        if (overrideBackgroundDimSizeMode)
            _backgroundDimSizeMode = backgroundDimSizeMode;
        if (overrideBackgroundDimFixedSize)
            _backgroundDimFixedSize = backgroundDimFixedSize;
        if (overrideBackgroundDimColor)
            _backgroundDimColor = backgroundDimColor;
        if (overrideBackgroundDimAlpha)
            _backgroundDimAlpha = backgroundDimAlpha;
        if (overrideTextMode)
            _textMode = textMode;
        if (overrideTextFormat)
            _textFormat = textFormat;
        if (overrideNumberAndTitleFormat)
            _numberAndTitleFormat = numberAndTitleFormat;
        if (overrideChapterNumberOffset)
            _chapterNumberOffset = chapterNumberOffset;
        if (overrideEmptyTitleFallback)
            _emptyTitleFallback = emptyTitleFallback;
        if (overrideTrimTitle)
            _trimTitle = trimTitle;
        if (overrideUppercaseTitle)
            _uppercaseTitle = uppercaseTitle;
        if (overrideSpecificTitlePaddingSettings)
        {
            _useSpecificTitlePadding = useSpecificTitlePadding;
            _specificTitlePaddingMarkers = specificTitlePaddingMarkers != null
                ? (string[])specificTitlePaddingMarkers.Clone()
                : Array.Empty<string>();
            _specificTitlePadding = specificTitlePadding;
        }
        if (overrideAnimationMode)
            _animationMode = animationMode;
        if (overrideShownAnchoredPosition)
            _shownAnchoredPosition = shownAnchoredPosition;
        if (overrideCaptureShownPositionOnAwake)
            _captureShownPositionOnAwake = captureShownPositionOnAwake;
        if (overrideHiddenOffsetY)
            _hiddenOffsetY = hiddenOffsetY;
        if (overrideEnterDuration)
            _enterDuration = enterDuration;
        if (overrideVisibleDuration)
            _visibleDuration = visibleDuration;
        if (overrideExitDuration)
            _exitDuration = exitDuration;
        if (overrideFadeWithMovement)
            _fadeWithMovement = fadeWithMovement;
        if (overrideAnimatePosition)
            _animatePosition = animatePosition;
        if (overrideUseUnscaledTime)
            _useUnscaledTime = useUnscaledTime;
        if (overrideDisableRootAfterExit)
            _disableRootAfterExit = disableRootAfterExit;

        _hiddenOffsetY = Mathf.Max(0f, _hiddenOffsetY);
        _enterDuration = Mathf.Max(0f, _enterDuration);
        _visibleDuration = Mathf.Max(0f, _visibleDuration);
        _exitDuration = Mathf.Max(0f, _exitDuration);
        _backgroundDimAlpha = Mathf.Clamp01(_backgroundDimAlpha);
        _backgroundDimFixedSize = new Vector2(
            Mathf.Max(1f, _backgroundDimFixedSize.x),
            Mathf.Max(1f, _backgroundDimFixedSize.y));
        _specificTitlePadding = new Vector2(
            Mathf.Max(0f, _specificTitlePadding.x),
            Mathf.Max(0f, _specificTitlePadding.y));

        if (string.IsNullOrEmpty(_textFormat))
            _textFormat = DefaultTextFormat;
        if (string.IsNullOrEmpty(_numberAndTitleFormat))
            _numberAndTitleFormat = "\u0413\u041b\u0410\u0412\u0410 {0}: {1}";

        EnsureReferences();
        PreparePanelForCenteredDisplay();
        PrepareBackgroundDimLayout();
        RefreshTitleLayout();
    }

    public void PreviewTitleText(string text)
    {
        EnsureReferences();

        if (_titleText == null)
            return;

        if (_rootObject != null && !_rootObject.activeSelf)
            _rootObject.SetActive(true);

        StopShowRoutine();
        PreparePanelForDisplay();
        EnsureBackgroundDim();
        PlaceBackgroundDimBehindOverlay();

        _titleText.text = text ?? "";
        RefreshTitleLayout();
        SetPanelPosition(ResolveShownPosition());
        SetVisibility(1f);
    }

    public void PreviewChapterTitle(int zeroBasedChapterIndex, string chapterTitle)
    {
        PreviewTitleText(BuildChapterText(zeroBasedChapterIndex, chapterTitle));
    }

    public void HideInstant()
    {
        StopShowRoutine();
        SetVisibility(0f);
        SetPanelPosition(ResolveShownPosition());

        if (_disableRootAfterExit && _rootObject != null)
            _rootObject.SetActive(false);
    }

    public void RefreshNow()
    {
        EnsureReferences();
        RefreshTitleLayout();
    }

    public string BuildChapterText(int zeroBasedChapterIndex, string chapterTitle)
    {
        int displayNumber = Mathf.Max(0, zeroBasedChapterIndex) + _chapterNumberOffset;
        string preparedTitle = PrepareTitle(chapterTitle, displayNumber);

        if (_textMode == ChapterTitleTextMode.TitleOnly)
            return preparedTitle;

        if (_textMode == ChapterTitleTextMode.NumberOnly)
            return FormatFallbackTitle(displayNumber);

        if (_textMode == ChapterTitleTextMode.NumberAndTitle)
            return FormatChapterTitle(_numberAndTitleFormat, displayNumber, preparedTitle);

        if (_textMode == ChapterTitleTextMode.CustomFormat)
            return FormatChapterTitle(_textFormat, displayNumber, preparedTitle);

        if (LooksLikeCompleteChapterTitle(preparedTitle))
            return preparedTitle;

        try
        {
            return string.Format(_textFormat, displayNumber, preparedTitle);
        }
        catch (FormatException exception)
        {
            Debug.LogWarning($"ChapterTitleOverlay: неверный формат текста главы: {exception.Message}", this);
            return preparedTitle;
        }
    }

    private string FormatChapterTitle(string format, int displayNumber, string preparedTitle)
    {
        try
        {
            return string.Format(string.IsNullOrEmpty(format) ? DefaultTextFormat : format, displayNumber, preparedTitle);
        }
        catch (FormatException exception)
        {
            Debug.LogWarning($"ChapterTitleOverlay: invalid chapter text format: {exception.Message}", this);
            return preparedTitle;
        }
    }

    private IEnumerator ShowRoutine(string text)
    {
        _titleText.text = text;
        RefreshTitleLayout();

        Vector2 shownPosition = ResolveShownPosition();
        ChapterTitleAnimationMode animationMode = ResolveAnimationMode();

        if (animationMode == ChapterTitleAnimationMode.Instant)
        {
            SetPanelPosition(shownPosition);
            SetVisibility(1f);
            yield return Wait(_visibleDuration);
            SetVisibility(0f);
        }
        else if (animationMode == ChapterTitleAnimationMode.SlideFromTop)
        {
            Vector2 hiddenPosition = GetHiddenPosition(shownPosition);
            SetPanelPosition(hiddenPosition);
            SetVisibility(_fadeWithMovement ? 0f : 1f);

            yield return MoveTo(hiddenPosition, shownPosition, _enterDuration, _fadeWithMovement ? 0f : 1f, 1f);
            yield return Wait(_visibleDuration);
            yield return MoveTo(shownPosition, hiddenPosition, _exitDuration, 1f, _fadeWithMovement ? 0f : 1f);

            SetPanelPosition(shownPosition);
        }
        else
        {
            SetPanelPosition(shownPosition);
            SetVisibility(0f);

            yield return FadeVisibility(0f, 1f, _enterDuration);
            yield return Wait(_visibleDuration);
            yield return FadeVisibility(1f, 0f, _exitDuration);
        }

        SetVisibility(0f);

        if (_disableRootAfterExit && _rootObject != null)
            _rootObject.SetActive(false);

        _showRoutine = null;
    }

    private ChapterTitleAnimationMode ResolveAnimationMode()
    {
        if (_animationMode == ChapterTitleAnimationMode.Fade && _animatePosition)
            return ChapterTitleAnimationMode.SlideFromTop;

        return _animationMode;
    }

    private IEnumerator MoveTo(Vector2 from, Vector2 to, float duration, float fromAlpha, float toAlpha)
    {
        if (duration <= 0f)
        {
            SetPanelPosition(to);
            SetVisibility(toAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            SetPanelPosition(Vector2.LerpUnclamped(from, to, eased));
            SetVisibility(Mathf.LerpUnclamped(fromAlpha, toAlpha, eased));

            yield return null;
        }

        SetPanelPosition(to);
        SetVisibility(toAlpha);
    }

    private IEnumerator FadeVisibility(float fromAlpha, float toAlpha, float duration)
    {
        if (duration <= 0f)
        {
            SetVisibility(toAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            SetVisibility(Mathf.LerpUnclamped(fromAlpha, toAlpha, eased));
            yield return null;
        }

        SetVisibility(toAlpha);
    }

    private IEnumerator Wait(float duration)
    {
        if (duration <= 0f)
            yield break;

        if (_useUnscaledTime)
        {
            yield return new WaitForSecondsRealtime(duration);
            yield break;
        }

        yield return new WaitForSeconds(duration);
    }

    private static bool LooksLikeCompleteChapterTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string prepared = value.Trim().ToUpperInvariant();
        return prepared.StartsWith("ГЛАВА", StringComparison.Ordinal) ||
               prepared.StartsWith("CHAPTER", StringComparison.Ordinal);
    }

    private string PrepareTitle(string chapterTitle, int displayNumber)
    {
        string preparedTitle = StoryJsonConverter.SanitizeDisplayText(chapterTitle ?? "");

        if (_trimTitle)
            preparedTitle = preparedTitle.Trim();

        if (string.IsNullOrEmpty(preparedTitle))
            preparedTitle = FormatFallbackTitle(displayNumber);

        return _uppercaseTitle ? preparedTitle.ToUpperInvariant() : preparedTitle;
    }

    private string FormatFallbackTitle(int displayNumber)
    {
        try
        {
            return string.Format(_emptyTitleFallback ?? "", displayNumber);
        }
        catch (FormatException)
        {
            return "ГЛАВА " + displayNumber;
        }
    }

    private Vector2 GetHiddenPosition(Vector2 shownPosition)
    {
        return shownPosition + Vector2.up * _hiddenOffsetY;
    }

    private void EnsureReferences()
    {
        if (_rootObject == null)
            _rootObject = gameObject;

        if (_panelRect == null)
            _panelRect = transform as RectTransform;

        if (_titleText == null)
            _titleText = GetComponentInChildren<TMP_Text>(true);

        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        if (_titleAutoSize == null)
            _titleAutoSize = GetComponentInChildren<ButtonTextAutoSize>(true);

        ApplyTitleAutoSizeSuspension();

        CaptureDefaultTitlePadding();

        if (_backgroundDimCanvasGroup == null && _backgroundDimImage != null)
            _backgroundDimCanvasGroup = _backgroundDimImage.GetComponent<CanvasGroup>();

        if (_backgroundDimImage == null && _backgroundDimCanvasGroup != null)
            _backgroundDimImage = _backgroundDimCanvasGroup.GetComponent<Image>();

        PrepareBackgroundDimLayout();
    }

    private void CaptureDefaultTitlePadding()
    {
        if (_hasDefaultTitlePadding || _titleAutoSize == null)
            return;

        _defaultTitlePadding = _titleAutoSize.Padding;
        _hasDefaultTitlePadding = true;
    }

    private void ApplyTitlePadding(string text)
    {
        if (_titleAutoSize == null)
            return;

        CaptureDefaultTitlePadding();

        Vector2 targetPadding = ShouldUseSpecificTitlePadding(text)
            ? _specificTitlePadding
            : _defaultTitlePadding;

        _titleAutoSize.SetPadding(targetPadding);
    }

    private bool ShouldUseSpecificTitlePadding(string text)
    {
        if (!_useSpecificTitlePadding ||
            string.IsNullOrWhiteSpace(text) ||
            _specificTitlePaddingMarkers == null ||
            _specificTitlePaddingMarkers.Length == 0)
        {
            return false;
        }

        string preparedText = NormalizeTitlePaddingMarker(text);
        for (int i = 0; i < _specificTitlePaddingMarkers.Length; i++)
        {
            string marker = NormalizeTitlePaddingMarker(_specificTitlePaddingMarkers[i]);
            if (!string.IsNullOrEmpty(marker) && preparedText.Contains(marker))
                return true;
        }

        return false;
    }

    private static string NormalizeTitlePaddingMarker(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim()
            .Replace('\u0451', '\u0435')
            .Replace('\u0401', '\u0415')
            .ToUpperInvariant();
    }

    private void RefreshTitleLayout()
    {
        if (_titleText != null)
        {
            PrepareTitleTextForReadableLayout();
            _titleText.SetAllDirty();
            _titleText.ForceMeshUpdate(true, true);
        }

        if (_titleAutoSize == null)
            _titleAutoSize = GetComponentInChildren<ButtonTextAutoSize>(true);

        bool useTitleAutoSize = _titleAutoSize != null && !_storyStyleManualTitleTextRect;

        if (useTitleAutoSize)
            ApplyTitlePadding(_titleText != null ? _titleText.text : "");

        if (useTitleAutoSize)
        {
            _titleAutoSize.MarkDirty();
            _titleAutoSize.RefreshNow();
        }

        if (_panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);

        Canvas.ForceUpdateCanvases();
    }

    private void PrepareTitleTextForReadableLayout()
    {
        if (_titleText == null)
            return;

        if (!_storyStyleControlsTitleTextOverflow &&
            (_titleText.overflowMode == TextOverflowModes.Ellipsis ||
             _titleText.overflowMode == TextOverflowModes.Truncate))
        {
            _titleText.overflowMode = TextOverflowModes.Overflow;
        }

        RectTransform textRect = _titleText.rectTransform;
        if (textRect == null)
            return;

        float width = textRect.rect.width;
        float height = textRect.rect.height;
        if (width <= 1f || height <= 1f)
            return;

        string value = _titleText.text ?? string.Empty;
        Vector2 preferred = _titleText.GetPreferredValues(value, width, Mathf.Infinity);
        if (preferred.x <= width + 1f && preferred.y <= height + 1f)
            return;

        if (!_storyStyleControlsTitleTextWordWrapping)
            _titleText.enableWordWrapping = true;

        if (!_storyStyleControlsTitleTextAutoSize)
        {
            _titleText.enableAutoSizing = true;
            _titleText.fontSizeMax = Mathf.Max(_titleText.fontSizeMax, _titleText.fontSize);
            _titleText.fontSizeMin = Mathf.Clamp(_titleText.fontSizeMin, 1f, _titleText.fontSizeMax);
        }
    }

    private void ApplyTitleAutoSizeSuspension()
    {
        if (_titleAutoSize == null)
            return;

        _titleAutoSize.AutoRefreshSuspended = _storyStyleManualTitleTextRect;
    }

    private void SetPanelPosition(Vector2 anchoredPosition)
    {
        if (_panelRect != null)
            _panelRect.anchoredPosition = anchoredPosition;
    }

    private void SetVisibility(float alpha)
    {
        SetAlpha(alpha);
        SetBackgroundDimAlpha(alpha);
    }

    private void SetAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = alpha;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            return;
        }

        if (_titleText != null)
            _titleText.alpha = alpha;
    }

    private void SetBackgroundDimAlpha(float alpha)
    {
        float dimAlpha = Mathf.Clamp01(alpha) * Mathf.Clamp01(_backgroundDimAlpha);

        if (_backgroundDimCanvasGroup != null)
        {
            _backgroundDimCanvasGroup.alpha = dimAlpha;
            _backgroundDimCanvasGroup.interactable = false;
            _backgroundDimCanvasGroup.blocksRaycasts = false;
        }

        if (_backgroundDimImage != null)
        {
            Color color = _backgroundDimColor;
            color.a = _backgroundDimCanvasGroup != null
                ? Mathf.Clamp01(_backgroundDimColor.a)
                : Mathf.Clamp01(_backgroundDimColor.a) * dimAlpha;
            _backgroundDimImage.color = color;
            _backgroundDimImage.raycastTarget = false;
        }
    }

    private void PreparePanelForDisplay()
    {
        PreparePanelForCenteredDisplay();
        PrepareBackgroundDimLayout();

        if (_bringToFrontOnShow)
            GetOverlayRootTransform()?.SetAsLastSibling();
    }

    private void PreparePanelForCenteredDisplay()
    {
        if (!_centerOnShow || _panelRect == null)
            return;

        _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRect.pivot = new Vector2(0.5f, 0.5f);
        _shownAnchoredPosition = Vector2.zero;
        _panelRect.anchoredPosition = Vector2.zero;
        _panelRect.localScale = Vector3.one;
    }

    private Vector2 ResolveShownPosition()
    {
        if (!_centerOnShow)
            return _shownAnchoredPosition;

        PreparePanelForCenteredDisplay();
        return Vector2.zero;
    }

    private void EnsureBackgroundDim()
    {
        if (_backgroundDimCanvasGroup != null || _backgroundDimImage != null)
            return;

        Transform rootTransform = GetOverlayRootTransform();
        Transform parent = rootTransform != null && rootTransform.parent != null ? rootTransform.parent : transform.parent;
        if (parent == null)
            parent = transform;

        _runtimeBackgroundDimObject = new GameObject(
            "ChapterTitleBackgroundDim",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        _runtimeBackgroundDimObject.layer = gameObject.layer;

        RectTransform dimRect = _runtimeBackgroundDimObject.GetComponent<RectTransform>();
        dimRect.SetParent(parent, false);
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.anchoredPosition = Vector2.zero;
        dimRect.sizeDelta = Vector2.zero;
        dimRect.pivot = new Vector2(0.5f, 0.5f);

        _backgroundDimImage = _runtimeBackgroundDimObject.GetComponent<Image>();
        _backgroundDimCanvasGroup = _runtimeBackgroundDimObject.GetComponent<CanvasGroup>();
        PrepareBackgroundDimLayout();
        SetBackgroundDimAlpha(0f);
    }

    private void PrepareBackgroundDimLayout()
    {
        RectTransform dimRect = null;

        if (_backgroundDimCanvasGroup != null)
            dimRect = _backgroundDimCanvasGroup.transform as RectTransform;

        if (dimRect == null && _backgroundDimImage != null)
            dimRect = _backgroundDimImage.rectTransform;

        if (dimRect == null)
            return;

        if (_backgroundDimSizeMode == ChapterTitleBackdropSizeMode.StretchToParent)
        {
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.sizeDelta = Vector2.zero;
        }
        else
        {
            dimRect.anchorMin = new Vector2(0.5f, 0.5f);
            dimRect.anchorMax = new Vector2(0.5f, 0.5f);
            dimRect.sizeDelta = _backgroundDimFixedSize;
        }

        dimRect.anchoredPosition = Vector2.zero;
        dimRect.pivot = new Vector2(0.5f, 0.5f);
        dimRect.localScale = Vector3.one;

        if (_backgroundDimCanvasGroup != null)
        {
            _backgroundDimCanvasGroup.interactable = false;
            _backgroundDimCanvasGroup.blocksRaycasts = false;
        }

        if (_backgroundDimImage != null)
            _backgroundDimImage.raycastTarget = false;
    }

    private void PlaceBackgroundDimBehindOverlay()
    {
        if (!_bringToFrontOnShow)
            return;

        Transform rootTransform = GetOverlayRootTransform();
        if (rootTransform == null)
            return;

        Transform dimTransform = _backgroundDimCanvasGroup != null
            ? _backgroundDimCanvasGroup.transform
            : _backgroundDimImage != null
                ? _backgroundDimImage.transform
                : null;

        if (dimTransform != null && dimTransform.parent == rootTransform.parent)
            dimTransform.SetAsLastSibling();

        rootTransform.SetAsLastSibling();
    }

    private Transform GetOverlayRootTransform()
    {
        return _rootObject != null ? _rootObject.transform : transform;
    }

    private void CaptureStoryStyleDefaults()
    {
        if (_storyStyleDefaultsCaptured)
            return;

        Image panelImage = FindPanelBackgroundImage();
        _storyStylePanelImageDefaults = CaptureImageDefaults(panelImage);

        if (_titleText != null)
        {
            _storyStyleTitleTextColor = _titleText.color;
            _storyStyleTitleTextFont = _titleText.font;
            _storyStyleTitleTextFontSize = _titleText.fontSize;
            _storyStyleTitleTextAutoSize = _titleText.enableAutoSizing;
            _storyStyleTitleTextFontSizeMin = _titleText.fontSizeMin;
            _storyStyleTitleTextFontSizeMax = _titleText.fontSizeMax;
            _storyStyleTitleTextAlignment = _titleText.alignment;
            _storyStyleTitleTextWordWrapping = _titleText.enableWordWrapping;
            _storyStyleTitleTextOverflowMode = _titleText.overflowMode;
            _storyStyleTitleTextLineSpacing = _titleText.lineSpacing;
            _storyStyleTitleTextMargin = _titleText.margin;
            RectTransform rect = _titleText.rectTransform;
            if (rect != null)
            {
                _storyStyleTitleTextAnchorMin = rect.anchorMin;
                _storyStyleTitleTextAnchorMax = rect.anchorMax;
                _storyStyleTitleTextAnchoredPosition = rect.anchoredPosition;
                _storyStyleTitleTextSizeDelta = rect.sizeDelta;
                _storyStyleTitleTextPivot = rect.pivot;
            }
            _storyStyleTextDefaultsCaptured = true;
        }

        _storyStyleDefaultsCaptured = true;
    }

    private void RestoreStoryStyleDefaults()
    {
        if (!_storyStyleDefaultsCaptured)
            return;

        _storyStyleManualTitleTextRect = false;
        _storyStyleControlsTitleTextOverflow = false;
        _storyStyleControlsTitleTextAutoSize = false;
        _storyStyleControlsTitleTextWordWrapping = false;
        ApplyTitleAutoSizeSuspension();

        RestoreImageDefaults(_storyStylePanelImageDefaults);

        if (_storyStyleTextDefaultsCaptured && _titleText != null)
        {
            _titleText.color = _storyStyleTitleTextColor;
            _titleText.font = _storyStyleTitleTextFont;
            _titleText.fontSize = _storyStyleTitleTextFontSize;
            _titleText.enableAutoSizing = _storyStyleTitleTextAutoSize;
            _titleText.fontSizeMin = _storyStyleTitleTextFontSizeMin;
            _titleText.fontSizeMax = _storyStyleTitleTextFontSizeMax;
            _titleText.alignment = _storyStyleTitleTextAlignment;
            _titleText.enableWordWrapping = _storyStyleTitleTextWordWrapping;
            _titleText.overflowMode = _storyStyleTitleTextOverflowMode;
            _titleText.lineSpacing = _storyStyleTitleTextLineSpacing;
            _titleText.margin = _storyStyleTitleTextMargin;
            RectTransform rect = _titleText.rectTransform;
            if (rect != null)
            {
                rect.anchorMin = _storyStyleTitleTextAnchorMin;
                rect.anchorMax = _storyStyleTitleTextAnchorMax;
                rect.anchoredPosition = _storyStyleTitleTextAnchoredPosition;
                rect.sizeDelta = _storyStyleTitleTextSizeDelta;
                rect.pivot = _storyStyleTitleTextPivot;
            }
            _titleText.SetAllDirty();
        }
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

    private Image FindPanelBackgroundImage()
    {
        Transform searchRoot = _panelRect != null ? _panelRect : transform;
        if (searchRoot == null)
            return null;

        ButtonTextAutoSize[] autoSizeDrivers = searchRoot.GetComponentsInChildren<ButtonTextAutoSize>(true);
        for (int i = 0; i < autoSizeDrivers.Length; i++)
        {
            ButtonTextAutoSize autoSizeDriver = autoSizeDrivers[i];
            if (autoSizeDriver == null)
                continue;

            Image image = autoSizeDriver.GetComponent<Image>();
            if (image != null && image != _backgroundDimImage && image.GetComponentInParent<Button>() == null)
                return image;
        }

        Image directImage = searchRoot.GetComponent<Image>();
        if (directImage != null && directImage != _backgroundDimImage)
            return directImage;

        Image[] images = searchRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null ||
                image == _backgroundDimImage ||
                image.GetComponentInParent<Button>() != null)
            {
                continue;
            }

            return image;
        }

        return null;
    }

    private void StopShowRoutine()
    {
        if (_showRoutine == null)
            return;

        StopCoroutine(_showRoutine);
        _showRoutine = null;
    }
}
