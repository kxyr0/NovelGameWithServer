using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Автоматически подгоняет фон диалогового окна под текущий TMP_Text.
/// Используется для диалогов, где высота/ширина плашки должна меняться вместе с текстом и размером шрифта.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class DialogueBackgroundAutoSize : MonoBehaviour
{
    [Header("Что делает скрипт")]
    [Tooltip("TMP_Text с репликой диалога. По нему рассчитывается размер фона.")]
    [SerializeField] private TMP_Text _dialogueText;

    [Tooltip("RectTransform фона или плашки диалога, который нужно растягивать под текст.")]
    [SerializeField] private RectTransform _background;

    [Header("Отступы")]
    [Tooltip("Дополнительное место вокруг текста: X для левого и правого края, Y для верхнего и нижнего края.")]
    [SerializeField] private Vector2 _padding = new Vector2(48f, 32f);

    [Header("Размер")]
    [Tooltip("Если включено, фон меняет ширину вместе с текстом. Если выключено, меняется только высота.")]
    [SerializeField] private bool _resizeWidth;

    [Tooltip("Минимальная ширина фона. 0 означает не ограничивать.")]
    [SerializeField] private float _minWidth;

    [Tooltip("Максимальная ширина фона. 0 означает не ограничивать.")]
    [SerializeField] private float _maxWidth;

    [Tooltip("Минимальная высота фона. 0 означает не ограничивать.")]
    [SerializeField] private float _minHeight = 96f;

    [Tooltip("Максимальная высота фона. 0 означает не ограничивать.")]
    [SerializeField] private float _maxHeight;

    [Header("Поведение")]
    [Tooltip("Обновлять размер в LateUpdate. Полезно, если текст меняется из разных скриптов.")]
    [SerializeField] private bool _updateInLateUpdate = true;

    [Tooltip("LayoutElement этой плашки, если он используется. Скрипт будет обновлять preferred и minimum размеры.")]
    [SerializeField] private LayoutElement _layoutElement;

    [Tooltip("Если включено, высота RectTransform самого текста тоже подгоняется под рассчитанную высоту без внутреннего отступа.")]
    [SerializeField] private bool _resizeTextHeight;

    [Header("Направление роста")]
    [Tooltip("Если включено, верхняя кромка фона остаётся на месте, а новая высота растёт вниз. Это убирает сдвиг DialoguePanel или BodyText при изменении размера.")]
    [SerializeField] private bool _growDownFromTop = true;

    [Tooltip("Ставить вертикальное выравнивание TMP_Text в Top, чтобы текст начинался сверху и добавлял строки вниз.")]
    [SerializeField] private bool _forceTextTopAlignment = true;

    [Tooltip("Ставить Pivot Y = 1 у фона перед изменением высоты. При таком pivot фон растёт вниз, а не вокруг центра.")]
    [SerializeField] private bool _forceBackgroundTopPivot = true;

    [Tooltip("Если включено вместе с Resize Text Height, ставит Pivot Y = 1 у BodyText, чтобы текстовый RectTransform тоже рос вниз.")]
    [SerializeField] private bool _forceTextTopPivot = true;

    private string _lastText;
    private float _lastFontSize;
    private Vector2 _lastTextRectSize;
    private bool _dirty = true;
    private bool _missingReferencesLogged;
    private bool _suppressedByStoryUiStyle;

    public TMP_Text DialogueText => _dialogueText;
    public RectTransform Background => _background;
    public Vector2 Padding => _padding;

    public void SetSuppressedByStoryUiStyle(bool suppressed)
    {
        if (_suppressedByStoryUiStyle == suppressed)
            return;

        _suppressedByStoryUiStyle = suppressed;
        if (!suppressed)
            MarkDirty();
    }

    public void SetTargets(TMP_Text dialogueText, RectTransform background)
    {
        _dialogueText = dialogueText;
        _background = background;
        MarkDirty();
    }

    public void SetPadding(Vector2 padding)
    {
        _padding = new Vector2(Mathf.Max(0f, padding.x), Mathf.Max(0f, padding.y));
        MarkDirty();
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    public void RefreshNow()
    {
        if (_suppressedByStoryUiStyle)
        {
            _dirty = false;
            return;
        }

        if (!CanResize())
            return;

        RectTransform textRect = _dialogueText.rectTransform;
        float backgroundTopY = _growDownFromTop ? GetTopY(_background) : 0f;
        float textTopY = _growDownFromTop ? GetTopY(textRect) : 0f;

        if (_forceTextTopAlignment && _dialogueText.verticalAlignment != VerticalAlignmentOptions.Top)
            _dialogueText.verticalAlignment = VerticalAlignmentOptions.Top;

        if (_growDownFromTop && _forceBackgroundTopPivot)
            SetPivotPreservingTop(_background, new Vector2(_background.pivot.x, 1f), backgroundTopY);

        if (_growDownFromTop && _resizeTextHeight && _forceTextTopPivot)
            SetPivotPreservingTop(textRect, new Vector2(textRect.pivot.x, 1f), textTopY);

        float textWidth = ResolveTextWidth(textRect);
        Vector2 preferredTextSize = _dialogueText.GetPreferredValues(_dialogueText.text, textWidth, Mathf.Infinity);

        float targetWidth = _background.rect.width;
        if (_resizeWidth)
            targetWidth = ClampSize(preferredTextSize.x + _padding.x, _minWidth, _maxWidth);

        float targetHeight = ClampSize(preferredTextSize.y + _padding.y, _minHeight, _maxHeight);

        if (_resizeWidth && !Mathf.Approximately(_background.rect.width, targetWidth))
            _background.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);

        if (!Mathf.Approximately(_background.rect.height, targetHeight))
            _background.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        if (_growDownFromTop)
            SetTopY(_background, backgroundTopY);

        if (_resizeTextHeight)
        {
            if (!Mathf.Approximately(textRect.rect.height, preferredTextSize.y))
                textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredTextSize.y);
            if (_growDownFromTop)
                SetTopY(textRect, textTopY);
        }

        ApplyLayoutElement(targetWidth, targetHeight);
        RememberCurrentState(textRect);
        _dirty = false;
    }

    private void Reset()
    {
        TryAutoWire();
    }

    private void Awake()
    {
        TryAutoWire();
    }

    private void OnEnable()
    {
        MarkDirty();
    }

    private void OnValidate()
    {
        _padding = new Vector2(Mathf.Max(0f, _padding.x), Mathf.Max(0f, _padding.y));
        _minWidth = Mathf.Max(0f, _minWidth);
        _maxWidth = Mathf.Max(0f, _maxWidth);
        _minHeight = Mathf.Max(0f, _minHeight);
        _maxHeight = Mathf.Max(0f, _maxHeight);

        if (_maxWidth > 0f && _maxWidth < _minWidth)
            _maxWidth = _minWidth;

        if (_maxHeight > 0f && _maxHeight < _minHeight)
            _maxHeight = _minHeight;

        TryAutoWire();
        MarkDirty();
    }

    private void LateUpdate()
    {
        if (_suppressedByStoryUiStyle)
            return;

        if (!_updateInLateUpdate && !_dirty)
            return;

        if (_dirty || HasObservedTextChanged())
            RefreshNow();
    }

    private void TryAutoWire()
    {
        if (_background == null)
            _background = GetComponent<RectTransform>();

        if (_dialogueText == null)
            _dialogueText = GetComponentInChildren<TMP_Text>(true);

        if (_layoutElement == null && _background != null)
            _layoutElement = _background.GetComponent<LayoutElement>();
    }

    private bool CanResize()
    {
        if (_dialogueText != null && _background != null)
        {
            _missingReferencesLogged = false;
            return true;
        }

        if (!_missingReferencesLogged)
        {
            Debug.LogWarning("DialogueBackgroundAutoSize: назначь TMP_Text и RectTransform фона.", this);
            _missingReferencesLogged = true;
        }

        return false;
    }

    private bool HasObservedTextChanged()
    {
        if (_dialogueText == null)
            return false;

        RectTransform textRect = _dialogueText.rectTransform;
        return _lastText != _dialogueText.text ||
               !Mathf.Approximately(_lastFontSize, _dialogueText.fontSize) ||
               _lastTextRectSize != textRect.rect.size;
    }

    private void RememberCurrentState(RectTransform textRect)
    {
        _lastText = _dialogueText.text;
        _lastFontSize = _dialogueText.fontSize;
        _lastTextRectSize = textRect.rect.size;
    }

    private float ResolveTextWidth(RectTransform textRect)
    {
        float width = textRect.rect.width;
        if (width > 1f)
            return Mathf.Max(1f, width);

        if (_background != null && _background.rect.width > 1f)
            return Mathf.Max(1f, _background.rect.width - _padding.x);

        return 1000f;
    }

    private static float ClampSize(float value, float min, float max)
    {
        if (min > 0f)
            value = Mathf.Max(value, min);

        if (max > 0f)
            value = Mathf.Min(value, max);

        return value;
    }

    private void ApplyLayoutElement(float targetWidth, float targetHeight)
    {
        if (_layoutElement == null)
            return;

        if (_resizeWidth)
        {
            if (!Mathf.Approximately(_layoutElement.preferredWidth, targetWidth))
                _layoutElement.preferredWidth = targetWidth;
            if (_minWidth > 0f && !Mathf.Approximately(_layoutElement.minWidth, _minWidth))
                _layoutElement.minWidth = _minWidth;
        }

        if (!Mathf.Approximately(_layoutElement.preferredHeight, targetHeight))
            _layoutElement.preferredHeight = targetHeight;
        if (_minHeight > 0f && !Mathf.Approximately(_layoutElement.minHeight, _minHeight))
            _layoutElement.minHeight = _minHeight;
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

    private static class RectTransformCornerCache
    {
        public static readonly Vector3[] Corners = new Vector3[4];
    }
}
