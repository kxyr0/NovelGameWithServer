using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum CardDrawStartMode
{
    FromDirection,
    FromAnchoredOffset,
    FromAnchoredPosition,
    FromRectTransform
}

public enum CardDrawDirection
{
    Left,
    Right,
    Top,
    Bottom
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class CardDrawImageAnimator : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Image _image;
    [SerializeField] private RectTransform _sourceRectTransform;

    [Header("Start Position")]
    [SerializeField] private CardDrawStartMode _startMode = CardDrawStartMode.FromDirection;
    [SerializeField] private CardDrawDirection _startDirection = CardDrawDirection.Bottom;
    [SerializeField] private float _startDistance = 420f;
    [SerializeField] private Vector2 _startAnchoredOffset = new Vector2(0f, -420f);
    [SerializeField] private Vector2 _customStartAnchoredPosition;

    [Header("Motion")]
    [SerializeField] private float _duration = 0.55f;
    [SerializeField] private Ease _motionEase = Ease.OutCubic;
    [SerializeField] private float _startScale = 0.65f;
    [SerializeField] private float _overshootScale = 1.06f;
    [SerializeField] private float _settleDuration = 0.12f;
    [SerializeField] private float _startRotationZ = 12f;

    [Header("Reveal")]
    [SerializeField] private bool _useFlipReveal = true;
    [SerializeField] private Sprite _cardBackSprite;
    [SerializeField] private float _flipDelay = 0.12f;
    [SerializeField] private float _flipDuration = 0.26f;
    [SerializeField] private bool _fadeIn = true;
    [SerializeField] private float _startAlpha;

    [Header("Runtime")]
    [SerializeField] private bool _playOnEnable;
    [SerializeField] private bool _captureHomeOnPlay = true;
    [SerializeField] private bool _restoreHomeOnDisable = true;
    [SerializeField] private bool _blockRaycastsDuringAnimation = true;
    [SerializeField] private bool _useUnscaledTime = true;

    [Header("Events")]
    [SerializeField] private UnityEvent _drawStarted = new UnityEvent();
    [SerializeField] private UnityEvent _drawCompleted = new UnityEvent();

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Sequence _activeSequence;
    private Action _activeCompletion;
    private Vector2 _homeAnchoredPosition;
    private Vector3 _homeScale;
    private Vector3 _homeEulerAngles;
    private float _homeAlpha = 1f;
    private bool _homeBlocksRaycasts = true;
    private bool _hasHomePose;
    private bool _frontSpriteApplied;
    private Vector2 _runtimeStartPosition;
    private Vector3 _runtimeStartEulerAngles;

    public event Action DrawStarted;
    public event Action DrawCompleted;

    public bool IsAnimating => _activeSequence != null && _activeSequence.IsActive();
    public float Duration => _duration;
    public Image Image => _image;
    public RectTransform RectTransform => _rectTransform;

    private void Reset()
    {
        _image = GetComponent<Image>();
    }

    private void OnValidate()
    {
        _duration = Mathf.Max(0f, _duration);
        _startDistance = Mathf.Max(0f, _startDistance);
        _startScale = Mathf.Max(0f, _startScale);
        _overshootScale = Mathf.Max(0f, _overshootScale);
        _settleDuration = Mathf.Clamp(_settleDuration, 0f, Mathf.Max(0f, _duration));
        _flipDelay = Mathf.Max(0f, _flipDelay);
        _flipDuration = Mathf.Max(0f, _flipDuration);
        _startAlpha = Mathf.Clamp01(_startAlpha);
    }

    private void Awake()
    {
        TryResolveComponents(false);
        CaptureHomePose();
    }

    private void OnEnable()
    {
        if (_playOnEnable)
        {
            PlayDraw();
        }
    }

    private void OnDisable()
    {
        Stop();

        if (_restoreHomeOnDisable)
        {
            ResetToHome();
        }
    }

    private void OnDestroy()
    {
        Stop();
    }

    public void PlayDraw()
    {
        PlayDraw(null, null);
    }

    public void PlayDraw(Sprite frontSprite)
    {
        PlayDraw(frontSprite, null);
    }

    public void PlayDraw(Sprite frontSprite, Action onComplete)
    {
        if (!TryResolveComponents(true))
        {
            SafeInvoke(onComplete);
            return;
        }

        KillActiveAnimation(false);

        if (_captureHomeOnPlay || !_hasHomePose)
        {
            CaptureHomePose();
        }

        Sprite resolvedFrontSprite = frontSprite != null ? frontSprite : _image.sprite;
        _activeCompletion = onComplete;

        if (_duration <= 0f)
        {
            ApplyImmediateDraw(resolvedFrontSprite);
            return;
        }

        PrepareStartPose(resolvedFrontSprite);
        InvokeDrawStarted();

        _activeSequence = DOTween.Sequence()
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject);

        Tween poseTween = DOVirtual
            .Float(0f, 1f, _duration, progress => ApplyDrawPose(progress, resolvedFrontSprite))
            .SetEase(_motionEase);

        _activeSequence
            .Append(poseTween)
            .OnComplete(() => CompleteDraw(resolvedFrontSprite))
            .OnKill(() => _activeSequence = null);
    }

    public void SetSourceRectTransform(RectTransform sourceRectTransform)
    {
        _sourceRectTransform = sourceRectTransform;
        _startMode = CardDrawStartMode.FromRectTransform;
    }

    public void SetStartMode(CardDrawStartMode startMode)
    {
        _startMode = startMode;
    }

    public void SetUseUnscaledTime(bool useUnscaledTime)
    {
        _useUnscaledTime = useUnscaledTime;
    }

    public void CaptureHomePose()
    {
        if (!TryResolveComponents(false))
        {
            return;
        }

        _homeAnchoredPosition = _rectTransform.anchoredPosition;
        _homeScale = _rectTransform.localScale;
        _homeEulerAngles = _rectTransform.localEulerAngles;

        if (_canvasGroup != null)
        {
            _homeAlpha = _canvasGroup.alpha;
            _homeBlocksRaycasts = _canvasGroup.blocksRaycasts;
        }

        _hasHomePose = true;
    }

    public void ResetToHome()
    {
        if (!TryResolveComponents(false) || !_hasHomePose)
        {
            return;
        }

        _rectTransform.anchoredPosition = _homeAnchoredPosition;
        _rectTransform.localScale = _homeScale;
        _rectTransform.localEulerAngles = _homeEulerAngles;
        RestoreCanvasGroup();
    }

    public void CompleteImmediately()
    {
        if (_activeSequence == null)
        {
            ResetToHome();
            return;
        }

        KillActiveAnimation(true);
    }

    public void Stop()
    {
        KillActiveAnimation(false);
    }

    private void ApplyImmediateDraw(Sprite frontSprite)
    {
        if (frontSprite != null)
        {
            _image.sprite = frontSprite;
        }

        ResetToHome();
        InvokeDrawStarted();
        CompleteDraw(frontSprite);
    }

    private void PrepareStartPose(Sprite frontSprite)
    {
        _frontSpriteApplied = false;
        _runtimeStartPosition = GetStartAnchoredPosition();
        _runtimeStartEulerAngles = new Vector3(
            _homeEulerAngles.x,
            _homeEulerAngles.y,
            _homeEulerAngles.z + _startRotationZ);

        if (ShouldUseFlipReveal(frontSprite))
        {
            _image.sprite = _cardBackSprite;
        }
        else if (frontSprite != null)
        {
            _image.sprite = frontSprite;
            _frontSpriteApplied = true;
        }

        _rectTransform.anchoredPosition = _runtimeStartPosition;
        _rectTransform.localScale = _homeScale * _startScale;
        _rectTransform.localEulerAngles = _runtimeStartEulerAngles;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = _fadeIn ? _startAlpha : _homeAlpha;

            if (_blockRaycastsDuringAnimation)
            {
                _canvasGroup.blocksRaycasts = false;
            }
        }
    }

    private void ApplyDrawPose(float progress, Sprite frontSprite)
    {
        float clampedProgress = Mathf.Clamp01(progress);

        _rectTransform.anchoredPosition = Vector2.LerpUnclamped(_runtimeStartPosition, _homeAnchoredPosition, clampedProgress);
        _rectTransform.localScale = _homeScale * EvaluateScale(clampedProgress);
        _rectTransform.localEulerAngles = EvaluateRotation(clampedProgress, frontSprite);

        if (_canvasGroup != null && _fadeIn)
        {
            _canvasGroup.alpha = Mathf.Lerp(_startAlpha, _homeAlpha, clampedProgress);
        }
    }

    private Vector3 EvaluateRotation(float progress, Sprite frontSprite)
    {
        float z = Mathf.LerpAngle(_runtimeStartEulerAngles.z, _homeEulerAngles.z, progress);
        float y = _homeEulerAngles.y + EvaluateFlipRotation(progress, frontSprite);
        return new Vector3(_homeEulerAngles.x, y, z);
    }

    private float EvaluateFlipRotation(float progress, Sprite frontSprite)
    {
        if (!ShouldUseFlipReveal(frontSprite))
        {
            return 0f;
        }

        float flipStart = _duration > 0f ? Mathf.Clamp01(_flipDelay / _duration) : 0f;
        float flipEnd = _duration > 0f ? Mathf.Clamp01((_flipDelay + _flipDuration) / _duration) : 1f;

        if (flipEnd <= flipStart)
        {
            ApplyFrontSprite(frontSprite);
            return 0f;
        }

        float flipProgress = Mathf.InverseLerp(flipStart, flipEnd, progress);

        if (flipProgress >= 0.5f)
        {
            ApplyFrontSprite(frontSprite);
        }

        if (progress <= flipStart || progress >= flipEnd)
        {
            return 0f;
        }

        if (flipProgress < 0.5f)
        {
            return Mathf.Lerp(0f, 90f, flipProgress * 2f);
        }

        return Mathf.Lerp(90f, 0f, (flipProgress - 0.5f) * 2f);
    }

    private float EvaluateScale(float progress)
    {
        if (_settleDuration <= 0f || _duration <= 0f || _overshootScale <= 0f)
        {
            return Mathf.Lerp(_startScale, 1f, progress);
        }

        float settleStart = Mathf.Clamp01((_duration - _settleDuration) / _duration);

        if (settleStart >= 1f)
        {
            return Mathf.Lerp(_startScale, 1f, progress);
        }

        if (progress <= settleStart)
        {
            float growProgress = settleStart > 0f ? progress / settleStart : 1f;
            return Mathf.Lerp(_startScale, _overshootScale, growProgress);
        }

        float settleProgress = Mathf.InverseLerp(settleStart, 1f, progress);
        return Mathf.Lerp(_overshootScale, 1f, settleProgress);
    }

    private void CompleteDraw(Sprite frontSprite)
    {
        ApplyFrontSprite(frontSprite);
        _rectTransform.anchoredPosition = _homeAnchoredPosition;
        _rectTransform.localScale = _homeScale;
        _rectTransform.localEulerAngles = _homeEulerAngles;
        RestoreCanvasGroup();

        Action completion = _activeCompletion;
        _activeCompletion = null;
        InvokeDrawCompleted();
        SafeInvoke(completion);
    }

    private void ApplyFrontSprite(Sprite frontSprite)
    {
        if (_frontSpriteApplied)
        {
            return;
        }

        if (frontSprite != null)
        {
            _image.sprite = frontSprite;
        }

        _frontSpriteApplied = true;
    }

    private Vector2 GetStartAnchoredPosition()
    {
        switch (_startMode)
        {
            case CardDrawStartMode.FromRectTransform:
                return GetAnchoredPositionFromSourceRect();
            case CardDrawStartMode.FromAnchoredOffset:
                return _homeAnchoredPosition + _startAnchoredOffset;
            case CardDrawStartMode.FromAnchoredPosition:
                return _customStartAnchoredPosition;
            case CardDrawStartMode.FromDirection:
            default:
                return _homeAnchoredPosition + GetDirectionOffset();
        }
    }

    private Vector2 GetDirectionOffset()
    {
        float distance = GetResolvedStartDistance();

        switch (_startDirection)
        {
            case CardDrawDirection.Left:
                return new Vector2(-distance, 0f);
            case CardDrawDirection.Right:
                return new Vector2(distance, 0f);
            case CardDrawDirection.Top:
                return new Vector2(0f, distance);
            case CardDrawDirection.Bottom:
            default:
                return new Vector2(0f, -distance);
        }
    }

    private float GetResolvedStartDistance()
    {
        if (_startDistance > 0f)
        {
            return _startDistance;
        }

        RectTransform parent = _rectTransform != null ? _rectTransform.parent as RectTransform : null;

        if (parent == null)
        {
            return 420f;
        }

        if (_startDirection == CardDrawDirection.Left || _startDirection == CardDrawDirection.Right)
        {
            return Mathf.Max(1f, parent.rect.width);
        }

        return Mathf.Max(1f, parent.rect.height);
    }

    private Vector2 GetAnchoredPositionFromSourceRect()
    {
        if (_sourceRectTransform == null || _rectTransform == null)
        {
            return _homeAnchoredPosition + GetDirectionOffset();
        }

        Vector3 originalPosition = _rectTransform.position;
        Vector2 originalAnchoredPosition = _rectTransform.anchoredPosition;
        Vector3 sourceWorldCenter = _sourceRectTransform.TransformPoint(_sourceRectTransform.rect.center);

        _rectTransform.position = sourceWorldCenter;
        Vector2 anchoredPosition = _rectTransform.anchoredPosition;
        _rectTransform.position = originalPosition;
        _rectTransform.anchoredPosition = originalAnchoredPosition;

        return anchoredPosition;
    }

    private bool ShouldUseFlipReveal(Sprite frontSprite)
    {
        return _useFlipReveal && _cardBackSprite != null && frontSprite != null && _cardBackSprite != frontSprite;
    }

    private bool TryResolveComponents(bool logWarning)
    {
        if (_image == null)
        {
            _image = GetComponent<Image>();
        }

        if (_rectTransform == null)
        {
            _rectTransform = _image != null ? _image.rectTransform : GetComponent<RectTransform>();
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_canvasGroup == null && Application.isPlaying)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        bool resolved = _image != null && _rectTransform != null;

        if (!resolved && logWarning)
        {
            Debug.LogWarning("CardDrawImageAnimator: Image or RectTransform component is missing.", this);
        }

        return resolved;
    }

    private void RestoreCanvasGroup()
    {
        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha = _homeAlpha;
        _canvasGroup.blocksRaycasts = _homeBlocksRaycasts;
    }

    private void KillActiveAnimation(bool complete)
    {
        if (_activeSequence == null)
        {
            return;
        }

        Sequence sequence = _activeSequence;
        _activeSequence = null;

        if (sequence.IsActive())
        {
            sequence.Kill(complete);
        }

        if (!complete)
        {
            _activeCompletion = null;
        }
    }

    private void InvokeDrawStarted()
    {
        try
        {
            DrawStarted?.Invoke();
            _drawStarted.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"CardDrawImageAnimator: start callback failed: {exception.Message}", this);
        }
    }

    private void InvokeDrawCompleted()
    {
        try
        {
            DrawCompleted?.Invoke();
            _drawCompleted.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"CardDrawImageAnimator: completion callback failed: {exception.Message}", this);
        }
    }

    private void SafeInvoke(Action callback)
    {
        try
        {
            callback?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"CardDrawImageAnimator: callback failed: {exception.Message}", this);
        }
    }
}
