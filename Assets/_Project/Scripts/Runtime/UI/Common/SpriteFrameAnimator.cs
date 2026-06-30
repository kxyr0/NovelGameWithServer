using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum SpriteFrameAnimationPlaybackMode
{
    Once,
    Loop,
    PingPong
}

public enum SpriteFrameAnimationTarget
{
    Auto,
    UIImage,
    SpriteRenderer
}

public enum SpriteFrameAnimationSizeMode
{
    Manual,
    ExactSize,
    StretchToParent
}

public enum SpriteFrameAnimationAspectMode
{
    Stretch,
    FitInside,
    Fill
}

public enum SpriteFrameAnimationReparentSiblingMode
{
    KeepCurrent,
    FirstSibling,
    LastSibling,
    CustomIndex
}

public enum SpriteFrameAnimationParentSwitchMode
{
    FirstObjectParentsSecond,
    SecondObjectParentsFirst,
    Toggle
}

[DisallowMultipleComponent]
public sealed class SpriteFrameAnimator : MonoBehaviour
{
    private const float MinFramesPerSecond = 0.01f;

    [Header("Target")]
    [SerializeField] private SpriteFrameAnimationTarget _target = SpriteFrameAnimationTarget.Auto;
    [SerializeField] private Image _imageTarget;
    [SerializeField] private SpriteRenderer _spriteRendererTarget;
    [SerializeField] private bool _hideTargetWhenFrameIsEmpty;

    [Header("Frames")]
    [SerializeField] private List<Sprite> _frames = new List<Sprite>();
    [SerializeField] private int _startFrameIndex;

    [Header("Size")]
    [SerializeField] private SpriteFrameAnimationSizeMode _sizeMode = SpriteFrameAnimationSizeMode.Manual;
    [SerializeField] private SpriteFrameAnimationAspectMode _aspectMode = SpriteFrameAnimationAspectMode.Stretch;
    [SerializeField] private Vector2 _targetSize = new Vector2(2200f, 1200f);
    [SerializeField] private Vector2 _anchoredPosition = Vector2.zero;
    [SerializeField] private Vector2 _pivot = new Vector2(0.5f, 0.5f);

    [Header("Playback")]
    [SerializeField] private float _framesPerSecond = 12f;
    [SerializeField] private SpriteFrameAnimationPlaybackMode _playbackMode = SpriteFrameAnimationPlaybackMode.Loop;
    [SerializeField] private bool _playOnEnable = true;
    [SerializeField] private bool _restartOnEnable = true;
    [SerializeField] private bool _useUnscaledTime;

    [Header("Trigger Gate")]
    [SerializeField] private bool _requiresTrigger;
    [SerializeField] private bool _permanentAfterTrigger;

    [Header("Frame Reparent")]
    [SerializeField] private bool _reparentOnFrame;
    [SerializeField] private Transform _reparentTargetParent;
    [SerializeField] private int _reparentFrameIndex;
    [SerializeField] private bool _keepWorldPosition = true;
    [SerializeField] private bool _reparentOnlyOncePerPlayback = true;
    [SerializeField] private SpriteFrameAnimationReparentSiblingMode _reparentSiblingMode = SpriteFrameAnimationReparentSiblingMode.LastSibling;
    [SerializeField] private int _reparentCustomSiblingIndex;

    [Header("Frame Parent Switch")]
    [SerializeField] private bool _switchParentOnFrame;
    [SerializeField] private Transform _firstParentSwitchObject;
    [SerializeField] private Transform _secondParentSwitchObject;
    [SerializeField] private int _parentSwitchFrameIndex;
    [SerializeField] private SpriteFrameAnimationParentSwitchMode _parentSwitchMode = SpriteFrameAnimationParentSwitchMode.Toggle;
    [SerializeField] private bool _preserveParentSwitchWorldPosition = true;
    [SerializeField] private bool _preserveParentSwitchWorldScale = true;
    [SerializeField] private bool _parentSwitchOnlyOncePerPlayback = true;

    [Header("Parent Restore")]
    [SerializeField] private bool _restoreOriginalParentOnComplete = true;
    [SerializeField] private bool _restoreOriginalSiblingIndexOnComplete = true;
    [SerializeField] private bool _preserveRestoredParentWorldPosition = true;
    [SerializeField] private bool _preserveRestoredParentWorldScale = true;

    [Header("Frame GameObject Activation")]
    [SerializeField] private bool _setGameObjectActiveOnFrame;
    [SerializeField] private GameObject _activeStateTarget;
    [SerializeField] private int _activeStateFrameIndex;
    [SerializeField] private bool _targetActiveState = true;
    [SerializeField] private bool _setGameObjectActiveOnlyOncePerPlayback = true;

    [Header("Card Animation")]
    [SerializeField] private Image _cardWhiteFlashImage;
    [SerializeField] private Color _cardWhiteFlashColor = Color.white;
    [SerializeField] private float _cardWhiteFlashHoldDuration = 0.04f;
    [SerializeField] private float _cardWhiteFlashFadeOutDuration = 0.16f;
    [SerializeField] private bool _hideCardWhiteFlashWhenIdle = true;
    [SerializeField] private bool _cardWhiteFlashBlocksRaycasts;
    [SerializeField] private bool _replaceLastCardSpriteWithRandom = true;
    [SerializeField] private Sprite[] _finalCardSprites = new Sprite[0];
    [SerializeField] private bool _resizeFinalCard;
    [SerializeField] private Vector2 _finalCardSize = new Vector2(699f, 907f);
    [SerializeField] private SpriteFrameAnimationAspectMode _finalCardAspectMode = SpriteFrameAnimationAspectMode.Stretch;
    [SerializeField] private bool _stopOnFinalCardUntilSceneRestart = true;

    [Header("Events")]
    [SerializeField] private UnityEvent _frameChanged = new UnityEvent();
    [SerializeField] private UnityEvent _animationCompleted = new UnityEvent();
    [SerializeField] private UnityEvent _triggered = new UnityEvent();
    [SerializeField] private UnityEvent _reparented = new UnityEvent();
    [SerializeField] private UnityEvent _gameObjectActiveChanged = new UnityEvent();

    private Coroutine _playCoroutine;
    private RectTransform _imageRectTransform;
    private int _currentFrameIndex;
    private int _direction = 1;
    private bool _isPaused;
    private bool _hasCompleted;
    private bool _hasTrigger;
    private bool _hasReparentedThisPlayback;
    private bool _hasSwitchedParentThisPlayback;
    private bool _hasSetGameObjectActiveThisPlayback;
    private bool _hasStoppedOnFinalCard;
    private readonly List<ParentRestoreState> _originalParentStates = new List<ParentRestoreState>();

    public event Action<int, Sprite> FrameChanged;
    public event Action AnimationCompleted;
    public event Action Triggered;
    public event Action<Transform> Reparented;
    public event Action<GameObject, bool> GameObjectActiveChanged;

    public IReadOnlyList<Sprite> Frames => _frames;
    public int FrameCount => _frames != null ? _frames.Count : 0;
    public int CurrentFrameIndex => _currentFrameIndex;
    public float FramesPerSecond => _framesPerSecond;
    public SpriteFrameAnimationPlaybackMode PlaybackMode => _playbackMode;
    public bool IsPlaying => _playCoroutine != null;
    public bool IsPaused => _isPaused;
    public bool UseUnscaledTime => _useUnscaledTime;
    public bool RequiresTrigger => _requiresTrigger;
    public bool HasTrigger => _hasTrigger;
    public bool PermanentAfterTrigger => _permanentAfterTrigger;
    public SpriteFrameAnimationSizeMode SizeMode => _sizeMode;
    public SpriteFrameAnimationAspectMode AspectMode => _aspectMode;
    public Vector2 TargetSize => _targetSize;
    public bool ReparentOnFrame => _reparentOnFrame;
    public int ReparentFrameIndex => _reparentFrameIndex;
    public Transform ReparentTargetParent => _reparentTargetParent;
    public bool SwitchParentOnFrame => _switchParentOnFrame;
    public int ParentSwitchFrameIndex => _parentSwitchFrameIndex;
    public Transform FirstParentSwitchObject => _firstParentSwitchObject;
    public Transform SecondParentSwitchObject => _secondParentSwitchObject;
    public SpriteFrameAnimationParentSwitchMode ParentSwitchMode => _parentSwitchMode;
    public bool RestoreOriginalParentOnComplete => _restoreOriginalParentOnComplete;
    public bool SetGameObjectActiveOnFrame => _setGameObjectActiveOnFrame;
    public int ActiveStateFrameIndex => _activeStateFrameIndex;
    public GameObject ActiveStateTarget => _activeStateTarget;
    public bool TargetActiveState => _targetActiveState;
    public bool HasStoppedOnFinalCard => _hasStoppedOnFinalCard;

    private void Reset()
    {
        ResolveTargets(false);
        ApplySizeSettings(GetCurrentFrame());
    }

    private void OnValidate()
    {
        if (_frames == null)
        {
            _frames = new List<Sprite>();
        }

        _framesPerSecond = Mathf.Max(MinFramesPerSecond, _framesPerSecond);
        _startFrameIndex = ClampFrameIndex(_startFrameIndex);
        _currentFrameIndex = ClampFrameIndex(_currentFrameIndex);
        _reparentFrameIndex = ClampFrameIndex(_reparentFrameIndex);
        _parentSwitchFrameIndex = ClampFrameIndex(_parentSwitchFrameIndex);
        _activeStateFrameIndex = ClampFrameIndex(_activeStateFrameIndex);
        _reparentCustomSiblingIndex = Mathf.Max(0, _reparentCustomSiblingIndex);
        _cardWhiteFlashHoldDuration = Mathf.Max(0f, _cardWhiteFlashHoldDuration);
        _cardWhiteFlashFadeOutDuration = Mathf.Max(0f, _cardWhiteFlashFadeOutDuration);
        _cardWhiteFlashColor.a = Mathf.Clamp01(_cardWhiteFlashColor.a);
        _finalCardSize = new Vector2(
            Mathf.Max(1f, _finalCardSize.x),
            Mathf.Max(1f, _finalCardSize.y));
        ApplyCardWhiteFlashRaycastTarget();

        if (_finalCardSprites == null)
        {
            _finalCardSprites = new Sprite[0];
        }

        _targetSize = new Vector2(
            Mathf.Max(1f, _targetSize.x),
            Mathf.Max(1f, _targetSize.y));
        _pivot = new Vector2(
            Mathf.Clamp01(_pivot.x),
            Mathf.Clamp01(_pivot.y));

        ResolveTargets(false);
        ApplySizeSettings(GetCurrentFrame());
    }

    private void Awake()
    {
        ResolveTargets(false);
        HideCardWhiteFlashImmediate();
        _currentFrameIndex = ClampFrameIndex(_startFrameIndex);

        if (!_playOnEnable)
        {
            ApplyCurrentFrame(false);
        }
    }

    private void OnEnable()
    {
        if (!_playOnEnable)
        {
            return;
        }

        if (_requiresTrigger && !_hasTrigger)
        {
            ApplyCurrentFrame(false);
            return;
        }

        if (_restartOnEnable)
        {
            PlayFromStart();
            return;
        }

        Play();
    }

    private void OnDisable()
    {
        StopPlaybackCoroutine();
        _isPaused = false;
    }

    private void OnDestroy()
    {
        StopPlaybackCoroutine();
        _isPaused = false;
    }

    public void Play()
    {
        if (_hasStoppedOnFinalCard)
        {
            return;
        }

        if (!CanStartByTriggerGate(true))
        {
            return;
        }

        StartPlayback();
    }

    public void PlayFromStart()
    {
        if (_hasStoppedOnFinalCard)
        {
            return;
        }

        if (!CanStartByTriggerGate(true))
        {
            return;
        }

        StopPlaybackCoroutine();
        ResetPlaybackState();
        StartPlayback();
    }

    public void TriggerAnimation()
    {
        _hasTrigger = true;
        InvokeTriggered();
        PlayFromStart();
    }

    public void TriggerPermanentAnimation()
    {
        _permanentAfterTrigger = true;
        TriggerAnimation();
    }

    public void ResetTrigger()
    {
        _hasTrigger = false;
    }

    public void SetRequiresTrigger(bool requiresTrigger)
    {
        _requiresTrigger = requiresTrigger;
    }

    public void SetPermanentAfterTrigger(bool permanentAfterTrigger)
    {
        _permanentAfterTrigger = permanentAfterTrigger;
    }

    private void StartPlayback()
    {
        if (_hasStoppedOnFinalCard)
        {
            return;
        }

        if (!CanPlay())
        {
            return;
        }

        if (_hasCompleted)
        {
            ResetPlaybackState();
        }

        if (_playCoroutine != null)
        {
            _isPaused = false;
            return;
        }

        _isPaused = false;
        _hasCompleted = false;
        _playCoroutine = StartCoroutine(PlayRoutine());
    }

    public void Restart()
    {
        PlayFromStart();
    }

    public void Pause()
    {
        if (_playCoroutine == null)
        {
            return;
        }

        _isPaused = true;
    }

    public void Resume()
    {
        if (_playCoroutine == null)
        {
            Play();
            return;
        }

        _isPaused = false;
    }

    public void Stop()
    {
        StopPlaybackCoroutine();
        _isPaused = false;
        _hasCompleted = false;
    }

    public void StopAndReset()
    {
        Stop();
        ResetPlaybackState();
        ApplyCurrentFrame(true);
    }

    public void SetFrame(int frameIndex)
    {
        _currentFrameIndex = ClampFrameIndex(frameIndex);
        ApplyCurrentFrame(true);
    }

    public void SetFrames(IEnumerable<Sprite> frames)
    {
        SetFrames(frames, true);
    }

    public void SetFrames(IEnumerable<Sprite> frames, bool restartIfPlaying)
    {
        bool wasPlaying = IsPlaying;
        List<Sprite> copiedFrames = CopyFrames(frames);

        StopPlaybackCoroutine();
        _frames.Clear();
        _frames.AddRange(copiedFrames);
        _startFrameIndex = ClampFrameIndex(_startFrameIndex);
        ResetPlaybackState();
        ApplyCurrentFrame(true);

        if (wasPlaying && restartIfPlaying)
        {
            Play();
        }
    }

    public void ClearFrames()
    {
        Stop();
        _frames.Clear();
        _currentFrameIndex = 0;
        ApplySprite(null);
    }

    public void SetFramesPerSecond(float framesPerSecond)
    {
        _framesPerSecond = Mathf.Max(MinFramesPerSecond, framesPerSecond);
    }

    public void SetPlaybackMode(SpriteFrameAnimationPlaybackMode playbackMode)
    {
        _playbackMode = playbackMode;
        _direction = 1;
    }

    public void SetLooping(bool loop)
    {
        SetPlaybackMode(loop ? SpriteFrameAnimationPlaybackMode.Loop : SpriteFrameAnimationPlaybackMode.Once);
    }

    public void SetUseUnscaledTime(bool useUnscaledTime)
    {
        _useUnscaledTime = useUnscaledTime;
    }

    public void SetImageTarget(Image target)
    {
        _imageTarget = target;
        _target = SpriteFrameAnimationTarget.UIImage;
        ApplyCurrentFrame(false);
    }

    public void SetSpriteRendererTarget(SpriteRenderer target)
    {
        _spriteRendererTarget = target;
        _target = SpriteFrameAnimationTarget.SpriteRenderer;
        ApplyCurrentFrame(false);
    }

    public void UseAutoTarget()
    {
        _target = SpriteFrameAnimationTarget.Auto;
        ResolveTargets(false);
        ApplyCurrentFrame(false);
    }

    public void SetSizeMode(SpriteFrameAnimationSizeMode sizeMode)
    {
        _sizeMode = sizeMode;
        ApplySizeSettings(GetCurrentFrame());
    }

    public void SetAspectMode(SpriteFrameAnimationAspectMode aspectMode)
    {
        _aspectMode = aspectMode;
        ApplySizeSettings(GetCurrentFrame());
    }

    public void SetTargetSize(Vector2 targetSize)
    {
        _targetSize = new Vector2(
            Mathf.Max(1f, targetSize.x),
            Mathf.Max(1f, targetSize.y));
        _sizeMode = SpriteFrameAnimationSizeMode.ExactSize;
        ApplySizeSettings(GetCurrentFrame());
    }

    public void SetTargetSize(float width, float height)
    {
        SetTargetSize(new Vector2(width, height));
    }

    [ContextMenu("Apply Exact Size 2200x1200")]
    public void ApplyExactSize2200x1200()
    {
        _aspectMode = SpriteFrameAnimationAspectMode.Stretch;
        SetTargetSize(2200f, 1200f);
    }

    public void StretchToParent()
    {
        _sizeMode = SpriteFrameAnimationSizeMode.StretchToParent;
        _aspectMode = SpriteFrameAnimationAspectMode.Stretch;
        ApplySizeSettings(GetCurrentFrame());
    }

    public void DisableSizeControl()
    {
        _sizeMode = SpriteFrameAnimationSizeMode.Manual;
    }

    public void ConfigureFrameReparent(Transform targetParent, int frameIndex)
    {
        _reparentTargetParent = targetParent;
        _reparentFrameIndex = ClampFrameIndex(frameIndex);
        _reparentOnFrame = targetParent != null;
    }

    public void SetFrameReparentEnabled(bool enabled)
    {
        _reparentOnFrame = enabled;
    }

    public void SetFrameReparentTarget(Transform targetParent)
    {
        _reparentTargetParent = targetParent;
    }

    public void SetFrameReparentIndex(int frameIndex)
    {
        _reparentFrameIndex = ClampFrameIndex(frameIndex);
    }

    public void ReparentNow()
    {
        TryReparentForCurrentFrame(true);
    }

    public void ConfigureFrameParentSwitch(Transform firstObject, Transform secondObject, int frameIndex)
    {
        _firstParentSwitchObject = firstObject;
        _secondParentSwitchObject = secondObject;
        _parentSwitchFrameIndex = ClampFrameIndex(frameIndex);
        _switchParentOnFrame = firstObject != null && secondObject != null;
    }

    public void SetFrameParentSwitchEnabled(bool enabled)
    {
        _switchParentOnFrame = enabled;
    }

    public void SetFrameParentSwitchObjects(Transform firstObject, Transform secondObject)
    {
        _firstParentSwitchObject = firstObject;
        _secondParentSwitchObject = secondObject;
    }

    public void SetFrameParentSwitchIndex(int frameIndex)
    {
        _parentSwitchFrameIndex = ClampFrameIndex(frameIndex);
    }

    public void SetFrameParentSwitchMode(SpriteFrameAnimationParentSwitchMode mode)
    {
        _parentSwitchMode = mode;
    }

    public void SwitchParentsNow()
    {
        TrySwitchParentsForCurrentFrame(true);
    }

    public void SetRestoreOriginalParentOnComplete(bool restore)
    {
        _restoreOriginalParentOnComplete = restore;
    }

    public void RestoreOriginalParentsNow()
    {
        RestoreOriginalParentsOnComplete();
    }

    public void ConfigureFrameGameObjectActivation(GameObject target, int frameIndex)
    {
        _activeStateTarget = target;
        _activeStateFrameIndex = ClampFrameIndex(frameIndex);
        _targetActiveState = true;
        _setGameObjectActiveOnFrame = target != null;
    }

    public void ConfigureFrameGameObjectActiveState(GameObject target, int frameIndex, bool active)
    {
        _activeStateTarget = target;
        _activeStateFrameIndex = ClampFrameIndex(frameIndex);
        _targetActiveState = active;
        _setGameObjectActiveOnFrame = target != null;
    }

    public void SetFrameGameObjectActivationEnabled(bool enabled)
    {
        _setGameObjectActiveOnFrame = enabled;
    }

    public void SetFrameGameObjectActivationTarget(GameObject target)
    {
        _activeStateTarget = target;
    }

    public void SetFrameGameObjectActivationIndex(int frameIndex)
    {
        _activeStateFrameIndex = ClampFrameIndex(frameIndex);
    }

    public void SetFrameGameObjectActiveState(bool active)
    {
        _targetActiveState = active;
    }

    public void SetGameObjectActiveNow()
    {
        TrySetGameObjectActiveForCurrentFrame(true);
    }

    public void SetCardWhiteFlashImage(Image whiteFlashImage)
    {
        _cardWhiteFlashImage = whiteFlashImage;
        ApplyCardWhiteFlashRaycastTarget();
        HideCardWhiteFlashImmediate();
    }

    public void SetFinalCardSprites(Sprite[] finalCardSprites)
    {
        _finalCardSprites = finalCardSprites ?? new Sprite[0];
    }

    public void SetReplaceLastCardSpriteWithRandom(bool replace)
    {
        _replaceLastCardSpriteWithRandom = replace;
    }

    public void ClearStoppedOnFinalCard()
    {
        _hasStoppedOnFinalCard = false;
    }

    public void SetResizeFinalCard(bool resizeFinalCard)
    {
        _resizeFinalCard = resizeFinalCard;
    }

    public void SetFinalCardSize(Vector2 finalCardSize)
    {
        _finalCardSize = new Vector2(
            Mathf.Max(1f, finalCardSize.x),
            Mathf.Max(1f, finalCardSize.y));
    }

    public void SetFinalCardSize(float width, float height)
    {
        SetFinalCardSize(new Vector2(width, height));
    }

    private IEnumerator PlayRoutine()
    {
        while (FrameCount > 0)
        {
            if (_isPaused)
            {
                yield return null;
                continue;
            }

            ApplyCurrentFrame(true);
            yield return WaitForCurrentFrameDuration();

            if (!TryAdvanceFrame())
            {
                break;
            }
        }

        _playCoroutine = null;
        _isPaused = false;

        if (_playbackMode == SpriteFrameAnimationPlaybackMode.Once)
        {
            yield return PlayCardFinishEffect();
            RestoreOriginalParentsOnComplete();
            _hasCompleted = true;
            InvokeAnimationCompleted();
        }
    }

    private IEnumerator WaitForCurrentFrameDuration()
    {
        float elapsed = 0f;
        float duration = GetFrameDuration();

        while (elapsed < duration)
        {
            if (!_isPaused)
            {
                elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            }

            yield return null;
        }
    }

    private bool CanPlay()
    {
        if (!HasPlayableFrames())
        {
            Debug.LogWarning("SpriteFrameAnimator: animation has no playable frames.", this);
            ApplySprite(null);
            return false;
        }

        if (!ResolveTargets(true))
        {
            return false;
        }

        return true;
    }

    private bool CanStartByTriggerGate(bool logWarning)
    {
        if (!_requiresTrigger || _hasTrigger)
        {
            return true;
        }

        if (logWarning)
        {
            Debug.LogWarning("SpriteFrameAnimator: playback is locked until TriggerAnimation() is called.", this);
        }

        return false;
    }

    private void ResetPlaybackState()
    {
        _currentFrameIndex = ClampFrameIndex(_startFrameIndex);
        _direction = 1;
        _isPaused = false;
        _hasCompleted = false;
        _hasReparentedThisPlayback = false;
        _hasSwitchedParentThisPlayback = false;
        _hasSetGameObjectActiveThisPlayback = false;
        _originalParentStates.Clear();
    }

    private bool TryAdvanceFrame()
    {
        if (FrameCount <= 0)
        {
            return false;
        }

        if (_playbackMode == SpriteFrameAnimationPlaybackMode.Once)
        {
            if (_currentFrameIndex >= FrameCount - 1)
            {
                if (ShouldPlayPermanently())
                {
                    _currentFrameIndex = 0;
                    return true;
                }

                return false;
            }

            _currentFrameIndex++;
            return true;
        }

        if (_playbackMode == SpriteFrameAnimationPlaybackMode.Loop)
        {
            _currentFrameIndex = (_currentFrameIndex + 1) % FrameCount;
            return true;
        }

        if (FrameCount == 1)
        {
            return true;
        }

        if (_currentFrameIndex >= FrameCount - 1)
        {
            _direction = -1;
        }
        else if (_currentFrameIndex <= 0)
        {
            _direction = 1;
        }

        _currentFrameIndex += _direction;
        return true;
    }

    private bool ShouldPlayPermanently()
    {
        return _permanentAfterTrigger && _hasTrigger;
    }

    private void ApplyCurrentFrame(bool notify)
    {
        if (FrameCount <= 0)
        {
            ApplySprite(null);
            return;
        }

        _currentFrameIndex = ClampFrameIndex(_currentFrameIndex);
        Sprite frame = _frames[_currentFrameIndex];
        ApplySprite(frame);

        if (notify)
        {
            TryReparentForCurrentFrame(false);
            TrySwitchParentsForCurrentFrame(false);
            TrySetGameObjectActiveForCurrentFrame(false);
            InvokeFrameChanged(_currentFrameIndex, frame);
        }
    }

    private void ApplySprite(Sprite sprite)
    {
        if (!ResolveTargets(false))
        {
            return;
        }

        if (ShouldUseImageTarget())
        {
            _imageTarget.sprite = sprite;
            _imageTarget.enabled = !_hideTargetWhenFrameIsEmpty || sprite != null;
            ApplySizeSettings(sprite);
            return;
        }

        if (ShouldUseSpriteRendererTarget())
        {
            _spriteRendererTarget.sprite = sprite;
            _spriteRendererTarget.enabled = !_hideTargetWhenFrameIsEmpty || sprite != null;
            ApplySizeSettings(sprite);
        }
    }

    private bool ResolveTargets(bool logWarning)
    {
        if (_imageTarget == null && (_target == SpriteFrameAnimationTarget.Auto || _target == SpriteFrameAnimationTarget.UIImage))
        {
            TryGetComponent(out _imageTarget);
        }

        if (_spriteRendererTarget == null && (_target == SpriteFrameAnimationTarget.Auto || _target == SpriteFrameAnimationTarget.SpriteRenderer))
        {
            TryGetComponent(out _spriteRendererTarget);
        }

        if (_imageTarget != null && _imageRectTransform == null)
        {
            _imageRectTransform = _imageTarget.rectTransform;
        }

        bool hasTarget = ShouldUseImageTarget() || ShouldUseSpriteRendererTarget();

        if (!hasTarget && logWarning)
        {
            Debug.LogWarning("SpriteFrameAnimator: Image or SpriteRenderer target is missing.", this);
        }

        return hasTarget;
    }

    private bool ShouldUseImageTarget()
    {
        if (_target == SpriteFrameAnimationTarget.SpriteRenderer)
        {
            return false;
        }

        return _imageTarget != null;
    }

    private bool ShouldUseSpriteRendererTarget()
    {
        if (_target == SpriteFrameAnimationTarget.UIImage)
        {
            return false;
        }

        if (_target == SpriteFrameAnimationTarget.Auto && _imageTarget != null)
        {
            return false;
        }

        return _spriteRendererTarget != null;
    }

    private void ApplySizeSettings(Sprite sprite)
    {
        if (_sizeMode == SpriteFrameAnimationSizeMode.Manual)
        {
            return;
        }

        if (ShouldUseImageTarget())
        {
            ApplyImageSize(sprite);
            return;
        }

        if (ShouldUseSpriteRendererTarget())
        {
            ApplySpriteRendererSize(sprite);
        }
    }

    private void ApplyImageSize(Sprite sprite)
    {
        if (_imageTarget == null)
        {
            return;
        }

        if (_imageRectTransform == null)
        {
            _imageRectTransform = _imageTarget.rectTransform;
        }

        if (_imageRectTransform == null)
        {
            return;
        }

        _imageTarget.preserveAspect = false;
        _imageRectTransform.pivot = _pivot;

        if (_sizeMode == SpriteFrameAnimationSizeMode.StretchToParent)
        {
            _imageRectTransform.anchorMin = Vector2.zero;
            _imageRectTransform.anchorMax = Vector2.one;
            _imageRectTransform.offsetMin = Vector2.zero;
            _imageRectTransform.offsetMax = Vector2.zero;
            _imageRectTransform.anchoredPosition = Vector2.zero;
            _imageRectTransform.localScale = Vector3.one;
            return;
        }

        Vector2 resolvedSize = ResolveTargetSize(sprite, _targetSize);
        _imageRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _imageRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _imageRectTransform.sizeDelta = resolvedSize;
        _imageRectTransform.anchoredPosition = _anchoredPosition;
        _imageRectTransform.localScale = Vector3.one;
    }

    private void ApplySpriteRendererSize(Sprite sprite)
    {
        if (_spriteRendererTarget == null || sprite == null || _sizeMode == SpriteFrameAnimationSizeMode.StretchToParent)
        {
            return;
        }

        Vector2 spriteSize = sprite.bounds.size;

        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            return;
        }

        Vector2 resolvedSize = ResolveTargetSize(sprite, _targetSize);
        Transform targetTransform = _spriteRendererTarget.transform;
        targetTransform.localScale = new Vector3(
            resolvedSize.x / spriteSize.x,
            resolvedSize.y / spriteSize.y,
            targetTransform.localScale.z);
    }

    private Vector2 ResolveTargetSize(Sprite sprite, Vector2 availableSize)
    {
        return ResolveTargetSize(sprite, availableSize, _aspectMode);
    }

    private Vector2 ResolveTargetSize(Sprite sprite, Vector2 availableSize, SpriteFrameAnimationAspectMode aspectMode)
    {
        if (aspectMode == SpriteFrameAnimationAspectMode.Stretch || sprite == null || sprite.rect.height <= 0f)
        {
            return availableSize;
        }

        float spriteAspect = sprite.rect.width / sprite.rect.height;
        float availableAspect = availableSize.x / availableSize.y;
        bool fitByWidth = aspectMode == SpriteFrameAnimationAspectMode.FitInside
            ? spriteAspect > availableAspect
            : spriteAspect < availableAspect;

        if (fitByWidth)
        {
            return new Vector2(availableSize.x, availableSize.x / spriteAspect);
        }

        return new Vector2(availableSize.y * spriteAspect, availableSize.y);
    }

    private void TryReparentForCurrentFrame(bool force)
    {
        if (!_reparentOnFrame || _reparentTargetParent == null)
        {
            return;
        }

        if (!force && _currentFrameIndex != ClampFrameIndex(_reparentFrameIndex))
        {
            return;
        }

        if (!force && _reparentOnlyOncePerPlayback && _hasReparentedThisPlayback)
        {
            return;
        }

        Transform currentTransform = transform;
        CaptureOriginalParentIfNeeded(currentTransform);

        if (currentTransform.parent == _reparentTargetParent)
        {
            ApplyReparentSiblingMode();
            _hasReparentedThisPlayback = true;
            InvokeReparented(_reparentTargetParent);
            return;
        }

        currentTransform.SetParent(_reparentTargetParent, _keepWorldPosition);
        ApplyReparentSiblingMode();

        if (!_keepWorldPosition)
        {
            ApplySizeSettings(GetCurrentFrame());
        }

        _hasReparentedThisPlayback = true;
        InvokeReparented(_reparentTargetParent);
    }

    private void TrySwitchParentsForCurrentFrame(bool force)
    {
        if (!_switchParentOnFrame || _firstParentSwitchObject == null || _secondParentSwitchObject == null)
        {
            return;
        }

        if (_firstParentSwitchObject == _secondParentSwitchObject)
        {
            Debug.LogWarning("SpriteFrameAnimator: parent switch needs two different objects.", this);
            return;
        }

        if (!force && _currentFrameIndex != ClampFrameIndex(_parentSwitchFrameIndex))
        {
            return;
        }

        if (!force && _parentSwitchOnlyOncePerPlayback && _hasSwitchedParentThisPlayback)
        {
            return;
        }

        ResolveParentSwitchPair(out Transform newParent, out Transform newChild);

        if (newParent == null || newChild == null)
        {
            return;
        }

        CaptureOriginalParentIfNeeded(_firstParentSwitchObject);
        CaptureOriginalParentIfNeeded(_secondParentSwitchObject);
        ParentSwitchPose firstPose = CaptureParentSwitchPose(_firstParentSwitchObject);
        ParentSwitchPose secondPose = CaptureParentSwitchPose(_secondParentSwitchObject);

        if (IsAncestorOf(newChild, newParent))
        {
            newParent.SetParent(newChild.parent, true);
        }

        if (newChild.parent != newParent)
        {
            newChild.SetParent(newParent, true);
        }

        if (newParent == _firstParentSwitchObject)
        {
            RestoreParentSwitchPose(_firstParentSwitchObject, firstPose);
            RestoreParentSwitchPose(_secondParentSwitchObject, secondPose);
        }
        else
        {
            RestoreParentSwitchPose(_secondParentSwitchObject, secondPose);
            RestoreParentSwitchPose(_firstParentSwitchObject, firstPose);
        }

        _hasSwitchedParentThisPlayback = true;
        InvokeReparented(newParent);
    }

    private void TrySetGameObjectActiveForCurrentFrame(bool force)
    {
        if (!_setGameObjectActiveOnFrame || _activeStateTarget == null)
        {
            return;
        }

        if (!force && _currentFrameIndex != ClampFrameIndex(_activeStateFrameIndex))
        {
            return;
        }

        if (!force && _setGameObjectActiveOnlyOncePerPlayback && _hasSetGameObjectActiveThisPlayback)
        {
            return;
        }

        if (_activeStateTarget.activeSelf != _targetActiveState)
        {
            _activeStateTarget.SetActive(_targetActiveState);
        }

        _hasSetGameObjectActiveThisPlayback = true;
        InvokeGameObjectActiveChanged(_activeStateTarget, _targetActiveState);
    }

    private IEnumerator PlayCardFinishEffect()
    {
        Sprite finalSprite = PickRandomFinalCardSprite();
        bool hasFlash = _cardWhiteFlashImage != null;

        if (finalSprite == null && !hasFlash)
        {
            yield break;
        }

        if (hasFlash)
        {
            ShowCardWhiteFlashImmediate();
        }

        if (finalSprite != null)
        {
            ApplySprite(finalSprite);
            ApplyFinalCardSize(finalSprite);

            if (_stopOnFinalCardUntilSceneRestart)
            {
                _hasStoppedOnFinalCard = true;
            }
        }

        if (!hasFlash)
        {
            yield break;
        }

        if (_cardWhiteFlashHoldDuration > 0f)
        {
            yield return WaitCardFinishEffectDuration(_cardWhiteFlashHoldDuration);
        }

        if (_cardWhiteFlashFadeOutDuration > 0f)
        {
            yield return FadeOutCardWhiteFlash();
        }

        HideCardWhiteFlashImmediate();
    }

    private Sprite PickRandomFinalCardSprite()
    {
        if (!_replaceLastCardSpriteWithRandom || _finalCardSprites == null || _finalCardSprites.Length == 0)
        {
            return null;
        }

        int validCount = 0;

        for (int i = 0; i < _finalCardSprites.Length; i++)
        {
            if (_finalCardSprites[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        int selectedIndex = UnityEngine.Random.Range(0, validCount);

        for (int i = 0; i < _finalCardSprites.Length; i++)
        {
            Sprite sprite = _finalCardSprites[i];

            if (sprite == null)
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                return sprite;
            }

            selectedIndex--;
        }

        return null;
    }

    private void ApplyFinalCardSize(Sprite sprite)
    {
        if (!_resizeFinalCard)
        {
            return;
        }

        if (!ResolveTargets(false))
        {
            return;
        }

        if (ShouldUseImageTarget())
        {
            ApplyFinalImageSize(sprite);
            return;
        }

        if (ShouldUseSpriteRendererTarget())
        {
            ApplyFinalSpriteRendererSize(sprite);
        }
    }

    private void ApplyFinalImageSize(Sprite sprite)
    {
        if (_imageTarget == null)
        {
            return;
        }

        if (_imageRectTransform == null)
        {
            _imageRectTransform = _imageTarget.rectTransform;
        }

        if (_imageRectTransform == null)
        {
            return;
        }

        _imageTarget.preserveAspect = false;
        _imageRectTransform.sizeDelta = ResolveTargetSize(sprite, _finalCardSize, _finalCardAspectMode);
        _imageRectTransform.localScale = Vector3.one;
    }

    private void ApplyFinalSpriteRendererSize(Sprite sprite)
    {
        if (_spriteRendererTarget == null || sprite == null)
        {
            return;
        }

        Vector2 spriteSize = sprite.bounds.size;

        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            return;
        }

        Vector2 resolvedSize = ResolveTargetSize(sprite, _finalCardSize, _finalCardAspectMode);
        Transform targetTransform = _spriteRendererTarget.transform;
        targetTransform.localScale = new Vector3(
            resolvedSize.x / spriteSize.x,
            resolvedSize.y / spriteSize.y,
            targetTransform.localScale.z);
    }

    private IEnumerator FadeOutCardWhiteFlash()
    {
        float startAlpha = _cardWhiteFlashColor.a;
        float elapsed = 0f;

        while (elapsed < _cardWhiteFlashFadeOutDuration)
        {
            elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / _cardWhiteFlashFadeOutDuration);
            SetCardWhiteFlashAlpha(Mathf.Lerp(startAlpha, 0f, progress));
            yield return null;
        }
    }

    private IEnumerator WaitCardFinishEffectDuration(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }
    }

    private void ShowCardWhiteFlashImmediate()
    {
        if (_cardWhiteFlashImage == null)
        {
            return;
        }

        ApplyCardWhiteFlashRaycastTarget();

        if (!_cardWhiteFlashImage.gameObject.activeSelf)
        {
            _cardWhiteFlashImage.gameObject.SetActive(true);
        }

        SetCardWhiteFlashAlpha(_cardWhiteFlashColor.a);
    }

    private void SetCardWhiteFlashAlpha(float alpha)
    {
        if (_cardWhiteFlashImage == null)
        {
            return;
        }

        Color color = _cardWhiteFlashColor;
        color.a = Mathf.Clamp01(alpha);
        _cardWhiteFlashImage.color = color;
    }

    private void ApplyCardWhiteFlashRaycastTarget()
    {
        if (_cardWhiteFlashImage == null)
        {
            return;
        }

        _cardWhiteFlashImage.raycastTarget = _cardWhiteFlashBlocksRaycasts;
    }

    private void HideCardWhiteFlashImmediate()
    {
        if (_cardWhiteFlashImage == null)
        {
            return;
        }

        ApplyCardWhiteFlashRaycastTarget();
        SetCardWhiteFlashAlpha(0f);

        if (_hideCardWhiteFlashWhenIdle && _cardWhiteFlashImage.gameObject.activeSelf)
        {
            _cardWhiteFlashImage.gameObject.SetActive(false);
        }
    }

    private void CaptureOriginalParentIfNeeded(Transform target)
    {
        if (!_restoreOriginalParentOnComplete || target == null)
        {
            return;
        }

        for (int i = 0; i < _originalParentStates.Count; i++)
        {
            if (_originalParentStates[i].Target == target)
            {
                return;
            }
        }

        _originalParentStates.Add(new ParentRestoreState(
            target,
            target.parent,
            target.GetSiblingIndex()));
    }

    private void RestoreOriginalParentsOnComplete()
    {
        if (!_restoreOriginalParentOnComplete || _originalParentStates.Count == 0)
        {
            return;
        }

        List<ParentRestoreState> pendingStates = new List<ParentRestoreState>(_originalParentStates);

        while (pendingStates.Count > 0)
        {
            int restoreIndex = FindNextParentRestoreIndex(pendingStates);
            ParentRestoreState state = pendingStates[restoreIndex];
            pendingStates.RemoveAt(restoreIndex);
            RestoreOriginalParent(state);
        }

        _originalParentStates.Clear();
    }

    private int FindNextParentRestoreIndex(List<ParentRestoreState> pendingStates)
    {
        for (int i = 0; i < pendingStates.Count; i++)
        {
            if (!ContainsRestoreTarget(pendingStates, pendingStates[i].Parent))
            {
                return i;
            }
        }

        return 0;
    }

    private bool ContainsRestoreTarget(List<ParentRestoreState> states, Transform target)
    {
        if (target == null)
        {
            return false;
        }

        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Target == target)
            {
                return true;
            }
        }

        return false;
    }

    private void RestoreOriginalParent(ParentRestoreState state)
    {
        Transform target = state.Target;

        if (target == null)
        {
            return;
        }

        ParentSwitchPose pose = CaptureParentSwitchPose(target);
        Transform originalParent = state.Parent;

        if (originalParent != null && IsAncestorOf(target, originalParent))
        {
            originalParent.SetParent(target.parent, true);
        }

        if (target.parent != originalParent)
        {
            target.SetParent(originalParent, true);
        }

        RestoreParentPose(target, pose, _preserveRestoredParentWorldPosition, _preserveRestoredParentWorldScale);
        ApplyOriginalSiblingIndex(target, state);
    }

    private void ApplyOriginalSiblingIndex(Transform target, ParentRestoreState state)
    {
        if (!_restoreOriginalSiblingIndexOnComplete || target.parent == null)
        {
            return;
        }

        target.SetSiblingIndex(Mathf.Clamp(state.SiblingIndex, 0, target.parent.childCount - 1));
    }

    private void ResolveParentSwitchPair(out Transform newParent, out Transform newChild)
    {
        switch (_parentSwitchMode)
        {
            case SpriteFrameAnimationParentSwitchMode.FirstObjectParentsSecond:
                newParent = _firstParentSwitchObject;
                newChild = _secondParentSwitchObject;
                return;
            case SpriteFrameAnimationParentSwitchMode.SecondObjectParentsFirst:
                newParent = _secondParentSwitchObject;
                newChild = _firstParentSwitchObject;
                return;
            case SpriteFrameAnimationParentSwitchMode.Toggle:
            default:
                if (_secondParentSwitchObject.parent == _firstParentSwitchObject)
                {
                    newParent = _secondParentSwitchObject;
                    newChild = _firstParentSwitchObject;
                    return;
                }

                newParent = _firstParentSwitchObject;
                newChild = _secondParentSwitchObject;
                return;
        }
    }

    private ParentSwitchPose CaptureParentSwitchPose(Transform target)
    {
        return new ParentSwitchPose(
            target.position,
            target.rotation,
            target.lossyScale);
    }

    private void RestoreParentSwitchPose(Transform target, ParentSwitchPose pose)
    {
        RestoreParentPose(
            target,
            pose,
            _preserveParentSwitchWorldPosition,
            _preserveParentSwitchWorldScale);
    }

    private void RestoreParentPose(Transform target, ParentSwitchPose pose, bool preserveWorldPosition, bool preserveWorldScale)
    {
        if (preserveWorldPosition)
        {
            target.SetPositionAndRotation(pose.Position, pose.Rotation);
        }

        if (preserveWorldScale)
        {
            SetWorldScale(target, pose.WorldScale);
        }
    }

    private bool IsAncestorOf(Transform possibleAncestor, Transform possibleChild)
    {
        Transform current = possibleChild.parent;

        while (current != null)
        {
            if (current == possibleAncestor)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void SetWorldScale(Transform target, Vector3 worldScale)
    {
        Transform parent = target.parent;

        if (parent == null)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale = new Vector3(
            DivideScaleComponent(worldScale.x, parentScale.x),
            DivideScaleComponent(worldScale.y, parentScale.y),
            DivideScaleComponent(worldScale.z, parentScale.z));
    }

    private float DivideScaleComponent(float value, float divisor)
    {
        if (Mathf.Approximately(divisor, 0f))
        {
            return value;
        }

        return value / divisor;
    }

    private void ApplyReparentSiblingMode()
    {
        Transform currentTransform = transform;
        Transform parent = currentTransform.parent;

        if (parent == null)
        {
            return;
        }

        switch (_reparentSiblingMode)
        {
            case SpriteFrameAnimationReparentSiblingMode.FirstSibling:
                currentTransform.SetAsFirstSibling();
                return;
            case SpriteFrameAnimationReparentSiblingMode.LastSibling:
                currentTransform.SetAsLastSibling();
                return;
            case SpriteFrameAnimationReparentSiblingMode.CustomIndex:
                currentTransform.SetSiblingIndex(Mathf.Clamp(_reparentCustomSiblingIndex, 0, parent.childCount - 1));
                return;
            case SpriteFrameAnimationReparentSiblingMode.KeepCurrent:
            default:
                return;
        }
    }

    private bool HasPlayableFrames()
    {
        if (_frames == null || _frames.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < _frames.Count; i++)
        {
            if (_frames[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private int ClampFrameIndex(int frameIndex)
    {
        if (_frames == null || _frames.Count == 0)
        {
            return 0;
        }

        return Mathf.Clamp(frameIndex, 0, _frames.Count - 1);
    }

    private Sprite GetCurrentFrame()
    {
        if (_frames == null || _frames.Count == 0)
        {
            return null;
        }

        return _frames[ClampFrameIndex(_currentFrameIndex)];
    }

    private float GetFrameDuration()
    {
        return 1f / Mathf.Max(MinFramesPerSecond, _framesPerSecond);
    }

    private void StopPlaybackCoroutine()
    {
        if (_playCoroutine == null)
        {
            return;
        }

        StopCoroutine(_playCoroutine);
        _playCoroutine = null;
        HideCardWhiteFlashImmediate();
    }

    private void InvokeFrameChanged(int frameIndex, Sprite frame)
    {
        try
        {
            FrameChanged?.Invoke(frameIndex, frame);
            _frameChanged.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"SpriteFrameAnimator: frame changed callback failed: {exception.Message}", this);
        }
    }

    private void InvokeAnimationCompleted()
    {
        try
        {
            AnimationCompleted?.Invoke();
            _animationCompleted.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"SpriteFrameAnimator: completion callback failed: {exception.Message}", this);
        }
    }

    private void InvokeTriggered()
    {
        try
        {
            Triggered?.Invoke();
            _triggered.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"SpriteFrameAnimator: trigger callback failed: {exception.Message}", this);
        }
    }

    private void InvokeReparented(Transform targetParent)
    {
        try
        {
            Reparented?.Invoke(targetParent);
            _reparented.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"SpriteFrameAnimator: reparent callback failed: {exception.Message}", this);
        }
    }

    private void InvokeGameObjectActiveChanged(GameObject target, bool active)
    {
        try
        {
            GameObjectActiveChanged?.Invoke(target, active);
            _gameObjectActiveChanged.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"SpriteFrameAnimator: game object active callback failed: {exception.Message}", this);
        }
    }

    private List<Sprite> CopyFrames(IEnumerable<Sprite> frames)
    {
        List<Sprite> copiedFrames = new List<Sprite>();

        if (frames == null)
        {
            return copiedFrames;
        }

        foreach (Sprite frame in frames)
        {
            copiedFrames.Add(frame);
        }

        return copiedFrames;
    }

    private readonly struct ParentSwitchPose
    {
        public ParentSwitchPose(Vector3 position, Quaternion rotation, Vector3 worldScale)
        {
            Position = position;
            Rotation = rotation;
            WorldScale = worldScale;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 WorldScale { get; }
    }

    private readonly struct ParentRestoreState
    {
        public ParentRestoreState(Transform target, Transform parent, int siblingIndex)
        {
            Target = target;
            Parent = parent;
            SiblingIndex = siblingIndex;
        }

        public Transform Target { get; }
        public Transform Parent { get; }
        public int SiblingIndex { get; }
    }
}
