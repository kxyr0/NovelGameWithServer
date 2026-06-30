using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Sprite State Fade")]
public class UISpriteStateFade : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float TransparentAlphaThreshold = 0.001f;

    [Header("Ссылки")]
    [SerializeField]
    [InspectorName("Image цели")]
    [Tooltip("Image, у которого меняется sprite. Компонент можно повесить на другой объект, например на большую панель наведения.")]
    private Image _targetImage;

    [SerializeField]
    [InspectorName("Fade image")]
    [Tooltip("Вспомогательный overlay для плавного перехода. Обычно можно оставить пустым.")]
    private Image _fadeImage;

    [SerializeField]
    [InspectorName("Создавать fade image")]
    [Tooltip("Если Fade image пустой, создать временный overlay автоматически.")]
    private bool _createFadeImageIfMissing = true;

    [Header("Спрайты")]
    [SerializeField]
    [InspectorName("Default sprite")]
    [Tooltip("Обычный sprite.")]
    private Sprite _defaultSprite;

    [SerializeField]
    [InspectorName("Active sprite")]
    [Tooltip("Sprite активного состояния.")]
    private Sprite _activeSprite;

    [SerializeField]
    [InspectorName("Взять текущий как default")]
    [Tooltip("Если Default sprite пустой, использовать текущий sprite из Image цели.")]
    private bool _captureCurrentSpriteAsDefault = true;

    [SerializeField]
    [InspectorName("Начать active")]
    [Tooltip("Включить active-состояние при Awake.")]
    private bool _startActive;

    [SerializeField]
    [InspectorName("Применять при OnEnable")]
    [Tooltip("Повторно применить текущее состояние при включении объекта.")]
    private bool _applyOnEnable = true;

    [Header("Наведение")]
    [SerializeField]
    [InspectorName("Включать при наведении")]
    [Tooltip("При наведении мыши/пальца включать active-состояние.")]
    private bool _activateOnPointerHover = true;

    [SerializeField]
    [InspectorName("Выключать при уходе")]
    [Tooltip("При уходе с объекта возвращать default-состояние.")]
    private bool _deactivateOnPointerExit = true;

    [Header("Связанные состояния")]
    [SerializeField]
    [InspectorName("Linked sprite fades")]
    [Tooltip("Дополнительные Sprite State Fade, которые будут включаться и выключаться вместе с этим компонентом.")]
    private UISpriteStateFade[] _linkedSpriteFades = System.Array.Empty<UISpriteStateFade>();

    [Header("Fade")]
    [SerializeField, Min(0f)]
    [InspectorName("Длительность")]
    [Tooltip("Сколько длится плавная смена sprite.")]
    private float _fadeDuration = 0.18f;

    [SerializeField]
    [InspectorName("Ease")]
    [Tooltip("Кривая анимации fade.")]
    private Ease _fadeEase = Ease.OutQuad;

    [SerializeField]
    [InspectorName("Unscaled time")]
    [Tooltip("Игнорировать Time.timeScale, чтобы UI-анимация работала во время паузы.")]
    private bool _useUnscaledTime = true;

    [Header("События")]
    [SerializeField]
    [InspectorName("State changed")]
    [Tooltip("Событие вызывается после смены состояния. Значение true означает active.")]
    private UnityEvent<bool> _stateChanged = new UnityEvent<bool>();

    private Sprite _capturedDefaultSprite;
    private Sequence _fadeSequence;
    private bool _isActive;
    private float _targetAlphaBeforeFade = -1f;

    public bool IsActive => _isActive;
    public Image TargetImage => _targetImage;
    public UnityEvent<bool> StateChanged => _stateChanged;

    private void Awake()
    {
        EnsureTargetImage();
        CaptureDefaultSpriteIfNeeded();
        ApplyImmediate(_startActive, notify: false);
    }

    private void OnEnable()
    {
        EnsureTargetImage();
        CaptureDefaultSpriteIfNeeded();

        if (_applyOnEnable)
            ApplyImmediate(_isActive, notify: false);
    }

    private void OnDisable()
    {
        KillFade();
        RestoreTargetAlphaIfNeeded();
        ResetFadeImage();
    }

    private void OnDestroy()
    {
        KillFade();
    }

    private void OnValidate()
    {
        _fadeDuration = Mathf.Max(0f, _fadeDuration);

        if (_fadeImage != null && _fadeImage == _targetImage)
        {
            _fadeImage = null;

            if (_targetImage != null && _targetImage.color.a <= TransparentAlphaThreshold)
                SetImageAlpha(_targetImage, 1f);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_activateOnPointerHover)
            ShowActive();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_activateOnPointerHover && _deactivateOnPointerExit)
            ShowDefault();
    }

    public void ShowDefault()
    {
        SetActiveState(false);
    }

    public void ShowActive()
    {
        SetActiveState(true);
    }

    public void Toggle()
    {
        SetActiveState(!_isActive);
    }

    public void SetActiveState(bool active)
    {
        SetActiveState(active, propagateToLinked: true);
    }

    public void SetActiveState(bool active, bool propagateToLinked)
    {
        Image target = EnsureTargetImage();

        CaptureDefaultSpriteIfNeeded();
        Sprite nextSprite = ResolveSprite(active);
        bool canApplyOwnState = target != null && nextSprite != null;

        if (_isActive == active)
        {
            EnsureTargetVisibleAlpha(target);

            if (canApplyOwnState && target.sprite != nextSprite && _fadeSequence == null)
            {
                ApplyImmediate(active, notify: false);
            }
            else if (_fadeSequence == null)
            {
                ResetFadeImage();
            }

            if (propagateToLinked)
                ApplyLinkedSpriteStates(active);

            return;
        }

        _isActive = active;

        if (!canApplyOwnState)
        {
            if (propagateToLinked)
                ApplyLinkedSpriteStates(active);

            _stateChanged?.Invoke(_isActive);
            return;
        }

        if (!isActiveAndEnabled || _fadeDuration <= 0f || !Application.isPlaying)
        {
            ApplyImmediate(active, notify: true);
            if (propagateToLinked)
                ApplyLinkedSpriteStates(active);

            return;
        }

        if (propagateToLinked)
            ApplyLinkedSpriteStates(active);

        CrossFadeTo(nextSprite, () => _stateChanged?.Invoke(_isActive));
    }

    public void SetSprites(Sprite defaultSprite, Sprite activeSprite, bool applyCurrentState = true)
    {
        _defaultSprite = defaultSprite;
        _activeSprite = activeSprite;
        _capturedDefaultSprite = null;

        if (applyCurrentState)
            ApplyImmediate(_isActive, notify: false);
    }

    public void ApplyImmediate()
    {
        ApplyImmediate(_isActive, notify: false);
    }

    public void SetPointerHoverEnabled(bool activateOnHover, bool deactivateOnExit)
    {
        _activateOnPointerHover = activateOnHover;
        _deactivateOnPointerExit = deactivateOnExit;
    }

    private void ApplyImmediate(bool active, bool notify)
    {
        Image target = EnsureTargetImage();
        CaptureDefaultSpriteIfNeeded();
        Sprite sprite = ResolveSprite(active);

        if (target == null || sprite == null)
        {
            _isActive = active;
            if (notify)
                _stateChanged?.Invoke(_isActive);

            return;
        }

        _isActive = active;
        KillFade();
        target.sprite = sprite;
        EnsureTargetVisibleAlpha(target);
        ResetFadeImage();

        if (notify)
            _stateChanged?.Invoke(_isActive);
    }

    private void CrossFadeTo(Sprite nextSprite, TweenCallback onComplete)
    {
        KillFade();

        Image target = EnsureTargetImage();
        Image fade = EnsureFadeImage();
        if (target == null)
            return;

        EnsureTargetVisibleAlpha(target);

        if (fade == null)
        {
            FadeTargetThroughTransparent(target, nextSprite, onComplete);
            return;
        }

        _targetAlphaBeforeFade = -1f;
        CopyImageSettings(target, fade);
        fade.sprite = nextSprite;
        fade.gameObject.SetActive(true);
        SetImageAlpha(fade, 0f);

        float targetAlpha = ResolveVisibleAlpha(target);
        _fadeSequence = DOTween.Sequence().SetUpdate(_useUnscaledTime);
        _fadeSequence.Join(FadeImageAlpha(fade, targetAlpha, _fadeDuration).SetEase(_fadeEase));
        _fadeSequence.OnComplete(() =>
        {
            target.sprite = nextSprite;
            EnsureTargetVisibleAlpha(target);
            ResetFadeImage();
            _fadeSequence = null;
            onComplete?.Invoke();
        });
    }

    private void FadeTargetThroughTransparent(Image target, Sprite nextSprite, TweenCallback onComplete)
    {
        float originalAlpha = target.color.a;
        float halfDuration = _fadeDuration * 0.5f;
        _targetAlphaBeforeFade = originalAlpha;

        _fadeSequence = DOTween.Sequence().SetUpdate(_useUnscaledTime);
        _fadeSequence.Append(FadeImageAlpha(target, 0f, halfDuration).SetEase(_fadeEase));
        _fadeSequence.AppendCallback(() => target.sprite = nextSprite);
        _fadeSequence.Append(FadeImageAlpha(target, originalAlpha, halfDuration).SetEase(_fadeEase));
        _fadeSequence.OnComplete(() =>
        {
            _targetAlphaBeforeFade = -1f;
            _fadeSequence = null;
            onComplete?.Invoke();
        });
    }

    private Image EnsureTargetImage()
    {
        if (_targetImage == null)
            _targetImage = GetComponent<Image>();

        return _targetImage;
    }

    private Image EnsureFadeImage()
    {
        if (_fadeImage != null && _fadeImage == _targetImage)
            _fadeImage = null;

        if (_fadeImage != null)
            return _fadeImage;

        if (!_createFadeImageIfMissing || _targetImage == null)
            return null;

        var fadeObject = new GameObject("Sprite Fade Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var fadeRect = fadeObject.GetComponent<RectTransform>();
        fadeRect.SetParent(_targetImage.rectTransform, false);
        fadeRect.SetAsFirstSibling();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        fadeRect.pivot = new Vector2(0.5f, 0.5f);

        _fadeImage = fadeObject.GetComponent<Image>();
        _fadeImage.raycastTarget = false;
        _fadeImage.gameObject.SetActive(false);
        return _fadeImage;
    }

    private void CaptureDefaultSpriteIfNeeded()
    {
        if (!_captureCurrentSpriteAsDefault || _defaultSprite != null || _capturedDefaultSprite != null)
            return;

        Image target = EnsureTargetImage();
        if (target != null)
            _capturedDefaultSprite = target.sprite;
    }

    private Sprite ResolveSprite(bool active)
    {
        if (active)
            return _activeSprite != null ? _activeSprite : ResolveDefaultSprite();

        return ResolveDefaultSprite();
    }

    private Sprite ResolveDefaultSprite()
    {
        if (_defaultSprite != null)
            return _defaultSprite;

        return _capturedDefaultSprite;
    }

    private static Tween FadeImageAlpha(Image image, float alpha, float duration)
    {
        return DOTween.To(
            () => image != null ? image.color.a : 0f,
            value => SetImageAlpha(image, value),
            alpha,
            Mathf.Max(0f, duration));
    }

    private static void CopyImageSettings(Image source, Image target)
    {
        if (source == null || target == null)
            return;

        target.color = source.color;
        target.material = source.material;
        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillAmount = source.fillAmount;
        target.fillClockwise = source.fillClockwise;
        target.fillOrigin = source.fillOrigin;
        target.useSpriteMesh = source.useSpriteMesh;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.maskable = source.maskable;
        target.raycastTarget = false;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private void ResetFadeImage()
    {
        if (_fadeImage == null || _fadeImage == _targetImage)
            return;

        SetImageAlpha(_fadeImage, 0f);
        _fadeImage.gameObject.SetActive(false);
    }

    private void RestoreTargetAlphaIfNeeded()
    {
        if (_targetAlphaBeforeFade < 0f)
            return;

        SetImageAlpha(_targetImage, _targetAlphaBeforeFade);
        _targetAlphaBeforeFade = -1f;
    }

    private static float ResolveVisibleAlpha(Image image)
    {
        if (image == null)
            return 1f;

        return image.color.a > TransparentAlphaThreshold ? image.color.a : 1f;
    }

    private static void EnsureTargetVisibleAlpha(Image image)
    {
        if (image == null || image.color.a > TransparentAlphaThreshold)
            return;

        SetImageAlpha(image, 1f);
    }

    private void KillFade()
    {
        if (_fadeSequence == null)
            return;

        _fadeSequence.Kill();
        _fadeSequence = null;
        RestoreTargetAlphaIfNeeded();
    }

    private void ApplyLinkedSpriteStates(bool active)
    {
        if (_linkedSpriteFades == null)
            return;

        for (int i = 0; i < _linkedSpriteFades.Length; i++)
        {
            UISpriteStateFade linked = _linkedSpriteFades[i];
            if (linked == null || linked == this)
                continue;

            linked.SetActiveState(active, propagateToLinked: false);
        }
    }
}
