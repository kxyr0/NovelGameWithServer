using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;

public enum VideoBackgroundSourceType
{
    VideoClip,
    Url
}

public enum VideoBackgroundFitMode
{
    FillParent,
    FitInsideParent,
    Stretch,
    Cover16By9
}

public enum VideoBackgroundLayoutMode
{
    Manual,
    StretchToParent,
    StretchToRootCanvas,
    ExactSize
}

public enum VideoBackgroundSiblingPlacement
{
    FirstSibling,
    AboveFirstBackground,
    CustomIndex,
    LastSibling
}

[Serializable]
public sealed class VideoBackgroundClipLayoutOverride
{
    [SerializeField, Tooltip("Видео, для которого применяются эти настройки. Остальные видео не затрагиваются.")]
    private VideoClip _clip;
    [SerializeField, Tooltip("Включить индивидуальные настройки для этого видео.")]
    private bool _enabled = true;
    [SerializeField, Tooltip("Переопределить режим вписывания только для этого видео.")]
    private bool _overrideFitMode = true;
    [SerializeField, Tooltip("Режим вписывания для этого видео. Для широкого видео на портретном экране обычно нужен Cover 16 By 9.")]
    private VideoBackgroundFitMode _fitMode = VideoBackgroundFitMode.Cover16By9;

    [Header("Размер")]
    [SerializeField, Tooltip("Задать минимальный ручной размер. Итоговый размер всё равно не станет меньше cover-размера экрана.")]
    private bool _useManualMinimumSize = true;
    [SerializeField, Tooltip("Минимальный размер видеофона в пикселях Canvas. Для cabinet_day удобно начать с 3600 x 2025.")]
    private Vector2 _manualMinimumSize = new Vector2(3600f, 2025f);
    [SerializeField, Min(0f), Tooltip("Дополнительный запас слева и справа для pan камеры. Значение 900 добавит 900px с каждой стороны.")]
    private float _extraPanPaddingX = 900f;
    [SerializeField, Min(0f), Tooltip("Дополнительный запас сверху и снизу, если нужно чуть выше/ниже кадр.")]
    private float _extraPaddingY = 0f;

    [Header("Масштаб и позиция")]
    [SerializeField, Tooltip("Переопределить общий overscan только для этого видео.")]
    private bool _overrideOverscanScale = false;
    [SerializeField, Min(1f), Tooltip("Дополнительный множитель размера только для этого видео.")]
    private float _overscanScale = 1f;
    [SerializeField, Tooltip("Задать базовый offset только для этого видео.")]
    private bool _overrideAnchoredPosition = false;
    [SerializeField, Tooltip("Базовый offset видеофона. Если включён pan камеры, позиция камеры сохраняется поверх этого значения.")]
    private Vector2 _anchoredPosition = Vector2.zero;
    [SerializeField, Tooltip("Сохранять текущий pan камеры, чтобы фон двигался вместе с персонажами.")]
    private bool _preserveCameraDrivenPosition = true;

    public bool Matches(VideoClip clip)
    {
        return _enabled && _clip != null && clip != null && _clip == clip;
    }

    public bool OverrideFitMode => _overrideFitMode;
    public VideoBackgroundFitMode FitMode => _fitMode;
    public bool UseManualMinimumSize => _useManualMinimumSize;
    public Vector2 ManualMinimumSize => _manualMinimumSize;
    public float ExtraPanPaddingX => Mathf.Max(0f, _extraPanPaddingX);
    public float ExtraPaddingY => Mathf.Max(0f, _extraPaddingY);
    public bool OverrideOverscanScale => _overrideOverscanScale;
    public float OverscanScale => Mathf.Max(1f, _overscanScale);
    public bool OverrideAnchoredPosition => _overrideAnchoredPosition;
    public Vector2 AnchoredPosition => _anchoredPosition;
    public bool PreserveCameraDrivenPosition => _preserveCameraDrivenPosition;
}

[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
[RequireComponent(typeof(VideoPlayer))]
public sealed class VideoBackgroundPlayer : MonoBehaviour, ICanvasRaycastFilter
{
    private const int MinTextureSize = 16;
    public const float WidescreenAspectRatio = 16f / 9f;

    [Header("Source")]
    [SerializeField] private VideoBackgroundSourceType _sourceType = VideoBackgroundSourceType.VideoClip;
    [SerializeField] private VideoClip _clip;
    [SerializeField] private string _url;

    [Header("View")]
    [SerializeField] private RawImage _rawImage;
    [SerializeField] private Color _emptyColor = Color.black;
    [SerializeField] private VideoBackgroundFitMode _fitMode = VideoBackgroundFitMode.Stretch;
    [SerializeField] private bool _raycastTarget;

    [Header("Layout")]
    [SerializeField] private bool _forceFullscreen = true;
    [SerializeField] private bool _reparentToRootCanvas = true;
    [SerializeField] private bool _preserveHierarchyAndSibling = true;
    [SerializeField] private VideoBackgroundLayoutMode _layoutMode = VideoBackgroundLayoutMode.StretchToRootCanvas;
    [SerializeField] private VideoBackgroundSiblingPlacement _rootCanvasSiblingPlacement = VideoBackgroundSiblingPlacement.AboveFirstBackground;
    [SerializeField, Min(0)] private int _customRootCanvasSiblingIndex = 1;
    [SerializeField, Min(1f)] private float _overscanScale = 1f;
    [SerializeField, Min(1f)] private float _overscanScaleX = 1f;
    [SerializeField] private Vector2 _exactSize = new Vector2(2200f, 1200f);
    [SerializeField] private Vector2 _anchoredPosition = Vector2.zero;
    [SerializeField] private Vector2 _pivot = new Vector2(0.5f, 0.5f);

    [Header("Playback")]
    [SerializeField] private bool _playOnEnable = true;
    [SerializeField] private bool _restartOnEnable = true;
    [SerializeField] private bool _loop = true;
    [SerializeField] private bool _mute = true;
    [SerializeField, Range(0f, 1f)] private float _volume = 1f;
    [SerializeField, Min(0.01f)] private float _playbackSpeed = 1f;
    [SerializeField] private bool _waitForFirstFrame = true;
    [SerializeField, Min(0.1f)] private float _prepareTimeout = 3f;

    [Header("Texture")]
    [SerializeField] private bool _useSourceSize;
    [SerializeField] private Vector2Int _fallbackTextureSize = new Vector2Int(2200, 1200);

    [Header("Per Clip Layout Overrides")]
    [SerializeField, Tooltip("Индивидуальные настройки размера/позиции для конкретных видео. Удобно для редких широких фонов вроде cabinet_day.")]
    private VideoBackgroundClipLayoutOverride[] _clipLayoutOverrides = Array.Empty<VideoBackgroundClipLayoutOverride>();

    [Header("Events")]
    [SerializeField] private UnityEvent _prepared = new UnityEvent();
    [SerializeField] private UnityEvent _started = new UnityEvent();
    [SerializeField] private UnityEvent _stopped = new UnityEvent();
    [SerializeField] private UnityEvent _failed = new UnityEvent();

    private VideoPlayer _videoPlayer;
    private RectTransform _rectTransform;
    private AspectRatioFitter _aspectRatioFitter;
    private CanvasGroup _canvasGroup;
    private RenderTexture _renderTexture;
    private bool _preparedHandlerRegistered;
    private bool _errorHandlerRegistered;
    private bool _isPreparing;
    private bool _shouldPlayAfterPrepare;
    private Coroutine _prepareWatchdogRoutine;

    public event Action Prepared;
    public event Action Started;
    public event Action Stopped;
    public event Action<string> Failed;

    public VideoClip Clip => _clip;
    public string Url => _url;
    public bool IsPrepared => _videoPlayer != null && _videoPlayer.isPrepared;
    public bool IsPlaying => _videoPlayer != null && _videoPlayer.isPlaying;
    public bool IsPreparing => _isPreparing;
    public VideoBackgroundSourceType SourceType => _sourceType;
    public RawImage RawImage => _rawImage;
    public RectTransform RectTransform => _rectTransform;
    public VideoPlayer VideoPlayer => _videoPlayer;
    internal float Volume => _volume;
    public VideoBackgroundLayoutMode LayoutMode => _layoutMode;
    public float OverscanScale => Mathf.Max(1f, _overscanScale);
    public float OverscanScaleX => Mathf.Max(1f, _overscanScaleX);
    public Vector2 ExactSize => _exactSize;
    public bool ForceFullscreen => _forceFullscreen;
    public bool PreserveHierarchyAndSibling => _preserveHierarchyAndSibling;

    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        return _raycastTarget;
    }

    private void Reset()
    {
        _rawImage = GetComponent<RawImage>();
        _rectTransform = GetComponent<RectTransform>();
        _videoPlayer = GetComponent<VideoPlayer>();
        if (_rawImage != null)
        {
            _rawImage.raycastTarget = _raycastTarget;
            _rawImage.color = _emptyColor;
        }
    }

    private void OnValidate()
    {
        _fallbackTextureSize = new Vector2Int(
            Mathf.Max(MinTextureSize, _fallbackTextureSize.x),
            Mathf.Max(MinTextureSize, _fallbackTextureSize.y));

        _playbackSpeed = Mathf.Max(0.01f, _playbackSpeed);
        _prepareTimeout = Mathf.Max(0.1f, _prepareTimeout);
        _volume = Mathf.Clamp01(_volume);
        _customRootCanvasSiblingIndex = Mathf.Max(0, _customRootCanvasSiblingIndex);
        _overscanScale = Mathf.Max(1f, _overscanScale);
        _overscanScaleX = Mathf.Max(1f, _overscanScaleX);
        _exactSize = new Vector2(
            Mathf.Max(1f, _exactSize.x),
            Mathf.Max(1f, _exactSize.y));
        _pivot = new Vector2(
            Mathf.Clamp01(_pivot.x),
            Mathf.Clamp01(_pivot.y));

        if (_rawImage != null)
        {
            _rawImage.raycastTarget = _raycastTarget;
        }

        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        if (_videoPlayer == null)
        {
            _videoPlayer = GetComponent<VideoPlayer>();
        }

        // Layout is applied from runtime entry points. Mutating RectTransform here
        // triggers Unity SendMessage warnings during validation/check consistency.
#if UNITY_EDITOR
        if (Application.isPlaying && _playOnEnable && isActiveAndEnabled && HasValidSource())
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null ||
                    !Application.isPlaying ||
                    !isActiveAndEnabled ||
                    !_playOnEnable ||
                    !HasValidSource() ||
                    IsCurrentSourceActive())
                {
                    return;
                }

                Play();
            };
        }
#endif
    }

    private void Awake()
    {
        EnsureComponents();
        ApplyLayoutSettings();
        ApplyFitMode(GetCurrentAspectRatio());
        ApplyViewSettings();
        ConfigureVideoPlayer();

        if (!HasValidSource())
            SetLayerVisible(false);
    }

    private void OnEnable()
    {
        EnsureComponents();
        ApplyLayoutSettings();
        ApplyFitMode(GetCurrentAspectRatio());

        if (!HasValidSource())
        {
            SetLayerVisible(false);
            return;
        }

        if (!_playOnEnable)
        {
            return;
        }

        if (_restartOnEnable)
        {
            Play();
            return;
        }

        Resume();
    }

    private void OnDisable()
    {
        StopPrepareWatchdog();
        Pause();
    }

    private void LateUpdate()
    {
        if (!isActiveAndEnabled || _rawImage == null)
        {
            return;
        }

        if (_fitMode == VideoBackgroundFitMode.Stretch)
        {
            RemoveAspectRatioFitter();
            return;
        }

        if (UsesManualCoverLayout())
        {
            VideoBackgroundClipLayoutOverride clipOverride = GetActiveClipLayoutOverride();
            VideoBackgroundFitMode fitMode = ResolveActiveFitMode(clipOverride);
            ApplyCoverLayout(GetManualCoverAspectRatio(fitMode, GetCurrentAspectRatio()), clipOverride);
        }
    }

    private void OnDestroy()
    {
        Stop();
        UnregisterVideoPlayerHandlers();
        ReleaseRenderTexture();
    }

    public void Play()
    {
        if (!EnsureComponents())
        {
            InvokeFailed("RawImage or VideoPlayer is missing.");
            return;
        }

        StopInternal(false);
        ConfigureVideoPlayer();
        ApplySource();
        PrepareAndPlay();
    }

    public void Play(VideoClip clip)
    {
        _sourceType = VideoBackgroundSourceType.VideoClip;
        _clip = clip;
        Play();
    }

    public void PlayUrl(string url)
    {
        _sourceType = VideoBackgroundSourceType.Url;
        _url = url;
        Play();
    }

    public void Pause()
    {
        if (_videoPlayer == null || !_videoPlayer.isPlaying)
        {
            return;
        }

        _videoPlayer.Pause();
    }

    public void Resume()
    {
        if (_videoPlayer == null)
        {
            Play();
            return;
        }

        if (_videoPlayer.isPlaying)
        {
            return;
        }

        if (_videoPlayer.isPrepared)
        {
            _videoPlayer.Play();
            InvokeStarted();
            return;
        }

        Play();
    }

    public void Stop()
    {
        StopInternal(true);
    }

    public void SetClip(VideoClip clip)
    {
        _sourceType = VideoBackgroundSourceType.VideoClip;
        _clip = clip;
    }

    public void SetUrl(string url)
    {
        _sourceType = VideoBackgroundSourceType.Url;
        _url = url;
    }

    public void SetLoop(bool loop)
    {
        _loop = loop;

        if (_videoPlayer != null)
        {
            _videoPlayer.isLooping = _loop;
        }
    }

    public void SetMute(bool mute)
    {
        _mute = mute;
        ApplyAudioSettings();
    }

    public void SetVolume(float volume)
    {
        _volume = Mathf.Clamp01(volume);
        ApplyAudioSettings();
    }

    public void SetPlaybackSpeed(float playbackSpeed)
    {
        _playbackSpeed = Mathf.Max(0.01f, playbackSpeed);

        if (_videoPlayer != null)
        {
            _videoPlayer.playbackSpeed = _playbackSpeed;
        }
    }

    public void SetFitMode(VideoBackgroundFitMode fitMode)
    {
        _fitMode = fitMode;
        ApplyFitMode(GetCurrentAspectRatio());
    }

    public void SetLayoutMode(VideoBackgroundLayoutMode layoutMode)
    {
        _layoutMode = layoutMode;
        if (layoutMode == VideoBackgroundLayoutMode.StretchToParent)
            _reparentToRootCanvas = false;
        else if (layoutMode == VideoBackgroundLayoutMode.StretchToRootCanvas && !_preserveHierarchyAndSibling)
            _reparentToRootCanvas = true;

        _forceFullscreen = layoutMode == VideoBackgroundLayoutMode.StretchToRootCanvas ||
                           layoutMode == VideoBackgroundLayoutMode.StretchToParent;
        ApplyLayoutSettings();
        ApplyFitMode(GetCurrentAspectRatio());
    }

    public void SetExactSize(Vector2 exactSize)
    {
        _exactSize = new Vector2(
            Mathf.Max(1f, exactSize.x),
            Mathf.Max(1f, exactSize.y));
        _forceFullscreen = false;
        _layoutMode = VideoBackgroundLayoutMode.ExactSize;
        ApplyLayoutSettings();
    }

    public void SetExactSize(float width, float height)
    {
        SetExactSize(new Vector2(width, height));
    }

    public void SetOverscanScale(float overscanScale)
    {
        SetOverscanScale(overscanScale, overscanScale);
    }

    public void SetOverscanScale(float overscanScaleX, float overscanScaleY)
    {
        _overscanScaleX = Mathf.Max(1f, overscanScaleX);
        _overscanScale = Mathf.Max(1f, overscanScaleY);
        ApplyLayoutSettings();
        ApplyFitMode(GetCurrentAspectRatio());
    }

    [ContextMenu("Apply Exact Fullscreen 2200x1200")]
    public void SetExactFullscreen2200x1200()
    {
        _forceFullscreen = false;
        _fitMode = VideoBackgroundFitMode.Stretch;
        _useSourceSize = false;
        _fallbackTextureSize = new Vector2Int(2200, 1200);
        SetExactSize(2200f, 1200f);
        ApplyFitMode(GetCurrentAspectRatio());

        if (_renderTexture != null && ShouldRecreateTexture(_fallbackTextureSize))
        {
            CreateRenderTexture(_fallbackTextureSize);
        }
    }

    public void StretchToParent()
    {
        _forceFullscreen = true;
        _reparentToRootCanvas = false;
        _layoutMode = VideoBackgroundLayoutMode.StretchToParent;
        ApplyLayoutSettings();
        ApplyFitMode(GetCurrentAspectRatio());
    }

    [ContextMenu("Force Fullscreen On Root Canvas")]
    public void ForceFullscreenOnRootCanvas()
    {
        _forceFullscreen = true;
        _reparentToRootCanvas = true;
        _fitMode = VideoBackgroundFitMode.Stretch;
        _useSourceSize = false;
        _fallbackTextureSize = new Vector2Int(2200, 1200);
        _layoutMode = VideoBackgroundLayoutMode.StretchToRootCanvas;
        ApplyLayoutSettings();
        ApplyFitMode(GetCurrentAspectRatio());
    }

    [ContextMenu("Apply 16:9 Cover")]
    public void ApplyWidescreenCover()
    {
        _forceFullscreen = true;
        _reparentToRootCanvas = false;
        _fitMode = VideoBackgroundFitMode.Cover16By9;
        _useSourceSize = true;
        _fallbackTextureSize = new Vector2Int(1920, 1080);
        _overscanScale = 1f;
        _overscanScaleX = 1f;
        _layoutMode = VideoBackgroundLayoutMode.StretchToParent;
        ApplyLayoutSettings();
        ApplyFitMode(WidescreenAspectRatio);
    }

    private void StopInternal(bool invokeStopped)
    {
        bool hadActiveState = _isPreparing ||
                              _shouldPlayAfterPrepare ||
                              _renderTexture != null ||
                              (_videoPlayer != null && (_videoPlayer.isPlaying || _videoPlayer.isPrepared));

        _isPreparing = false;
        _shouldPlayAfterPrepare = false;
        StopPrepareWatchdog();

        if (_videoPlayer != null)
        {
            if (_videoPlayer.isPlaying || _videoPlayer.isPrepared)
            {
                _videoPlayer.Stop();
            }

            _videoPlayer.targetTexture = null;
        }

        if (_rawImage != null)
        {
            _rawImage.texture = null;
            _rawImage.color = _emptyColor;
        }

        SetLayerVisible(false);
        ReleaseRenderTexture();

        if (invokeStopped && hadActiveState)
        {
            InvokeStopped();
        }
    }

    private bool EnsureComponents()
    {
        if (_rawImage == null)
        {
            _rawImage = GetComponent<RawImage>();
        }

        if (_rawImage == null)
        {
            _rawImage = gameObject.AddComponent<RawImage>();
        }

        if (_rectTransform == null && _rawImage != null)
        {
            _rectTransform = _rawImage.rectTransform;
        }

        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        if (_videoPlayer == null)
        {
            _videoPlayer = GetComponent<VideoPlayer>() ?? gameObject.AddComponent<VideoPlayer>();
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        return _rawImage != null && _videoPlayer != null;
    }

    private void SetLayerVisible(bool visible)
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    private void ConfigureVideoPlayer()
    {
        if (_videoPlayer == null)
        {
            return;
        }

        _videoPlayer.playOnAwake = false;
        _videoPlayer.waitForFirstFrame = _waitForFirstFrame;
        _videoPlayer.isLooping = _loop;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.aspectRatio = VideoAspectRatio.Stretch;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        _videoPlayer.playbackSpeed = _playbackSpeed;

        ApplyAudioSettings();
        RegisterVideoPlayerHandlers();
    }

    private void ApplySource()
    {
        if (_videoPlayer == null)
        {
            return;
        }

        if (_sourceType == VideoBackgroundSourceType.Url)
        {
            if (string.IsNullOrWhiteSpace(_url) && !string.IsNullOrWhiteSpace(_videoPlayer.url))
            {
                _url = _videoPlayer.url;
            }

            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.url = _url;
            _videoPlayer.clip = null;
            return;
        }

        if (_clip == null && _videoPlayer.clip != null)
        {
            _clip = _videoPlayer.clip;
        }

        _videoPlayer.source = VideoSource.VideoClip;
        _videoPlayer.clip = _clip;
        _videoPlayer.url = string.Empty;
    }

    private void PrepareAndPlay()
    {
        if (!HasValidSource())
        {
            Stop();
            InvokeFailed("Video source is empty.");
            return;
        }

        _isPreparing = true;
        _shouldPlayAfterPrepare = true;

        SetLayerVisible(false);
        CreateRenderTexture(GetPreferredTextureSize());
        ApplyLayoutSettings();
        ApplyViewSettings();
        ApplyFitMode(GetCurrentAspectRatio());

        try
        {
            _videoPlayer.Prepare();
            StartPrepareWatchdog();
        }
        catch (Exception exception)
        {
            Stop();
            InvokeFailed(exception.Message);
        }
    }

    private void OnVideoPrepared(VideoPlayer player)
    {
        if (player != _videoPlayer || !isActiveAndEnabled)
        {
            return;
        }

        _isPreparing = false;
        StopPrepareWatchdog();
        Vector2Int preparedSize = GetPreparedTextureSize();

        if (_useSourceSize && ShouldRecreateTexture(preparedSize))
        {
            CreateRenderTexture(preparedSize);
        }

        ApplyFitMode(GetAspectRatio(preparedSize));
        InvokePrepared();

        if (!_shouldPlayAfterPrepare)
        {
            return;
        }

        try
        {
            _videoPlayer.Play();
            _shouldPlayAfterPrepare = false;
            SetLayerVisible(true);
            InvokeStarted();
        }
        catch (Exception exception)
        {
            Stop();
            InvokeFailed(exception.Message);
        }
    }

    private void OnVideoError(VideoPlayer player, string message)
    {
        if (player != _videoPlayer)
        {
            return;
        }

        StopPrepareWatchdog();
        Stop();
        InvokeFailed(message);
    }

    private bool HasValidSource()
    {
        if (_sourceType == VideoBackgroundSourceType.Url)
        {
            return !string.IsNullOrWhiteSpace(_url);
        }

        return _clip != null;
    }

    private bool IsCurrentSourceActive()
    {
        if (_videoPlayer == null)
        {
            return false;
        }

        bool sourceMatches = _sourceType == VideoBackgroundSourceType.Url
            ? string.Equals(_videoPlayer.url, _url, StringComparison.Ordinal)
            : _videoPlayer.clip == _clip;

        return sourceMatches && (_videoPlayer.isPlaying || _isPreparing);
    }

    private Vector2Int GetPreferredTextureSize()
    {
        if (_useSourceSize && _sourceType == VideoBackgroundSourceType.VideoClip && _clip != null)
        {
            return new Vector2Int(
                Mathf.Max(MinTextureSize, (int)_clip.width),
                Mathf.Max(MinTextureSize, (int)_clip.height));
        }

        return _fallbackTextureSize;
    }

    private Vector2Int GetPreparedTextureSize()
    {
        if (_videoPlayer == null)
        {
            return GetPreferredTextureSize();
        }

        int width = Mathf.Max(MinTextureSize, (int)_videoPlayer.width);
        int height = Mathf.Max(MinTextureSize, (int)_videoPlayer.height);
        return new Vector2Int(width, height);
    }

    private bool ShouldRecreateTexture(Vector2Int size)
    {
        if (_renderTexture == null)
        {
            return true;
        }

        return _renderTexture.width != size.x || _renderTexture.height != size.y;
    }

    private void CreateRenderTexture(Vector2Int size)
    {
        if (_renderTexture != null && _renderTexture.width == size.x && _renderTexture.height == size.y)
        {
            AssignRenderTexture();
            return;
        }

        ReleaseRenderTexture();
        _renderTexture = new RenderTexture(size.x, size.y, 0)
        {
            name = $"{nameof(VideoBackgroundPlayer)} RenderTexture"
        };
        _renderTexture.Create();

        AssignRenderTexture();
    }

    private void AssignRenderTexture()
    {
        if (_videoPlayer != null)
        {
            _videoPlayer.targetTexture = _renderTexture;
        }

        if (_rawImage != null)
        {
            _rawImage.texture = _renderTexture;
            _rawImage.color = Color.white;
        }
    }

    private void ApplyViewSettings()
    {
        if (_rawImage == null)
        {
            return;
        }

        _rawImage.raycastTarget = _raycastTarget;

        if (_rawImage.texture == null)
        {
            _rawImage.color = _emptyColor;
        }
    }

    private void ApplyLayoutSettings()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        if (_rectTransform == null)
        {
            return;
        }

        VideoBackgroundLayoutMode resolvedLayoutMode = _forceFullscreen
            ? (_reparentToRootCanvas ? VideoBackgroundLayoutMode.StretchToRootCanvas : VideoBackgroundLayoutMode.StretchToParent)
            : _layoutMode;

        if (resolvedLayoutMode == VideoBackgroundLayoutMode.Manual)
        {
            return;
        }

        _rectTransform.pivot = _pivot;

        if (resolvedLayoutMode == VideoBackgroundLayoutMode.StretchToRootCanvas)
        {
            TryReparentToRootCanvas();
            StretchRectTransform();
            return;
        }

        if (resolvedLayoutMode == VideoBackgroundLayoutMode.StretchToParent)
        {
            StretchRectTransform();
            return;
        }

        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.sizeDelta = _exactSize;
        _rectTransform.anchoredPosition = _anchoredPosition;
        _rectTransform.localScale = GetOverscanScaleVector();
    }

    private void StretchRectTransform()
    {
        if (_rectTransform == null)
        {
            return;
        }

        _rectTransform.anchorMin = Vector2.zero;
        _rectTransform.anchorMax = Vector2.one;
        _rectTransform.offsetMin = Vector2.zero;
        _rectTransform.offsetMax = Vector2.zero;
        _rectTransform.anchoredPosition = Vector2.zero;
        _rectTransform.sizeDelta = Vector2.zero;
        _rectTransform.localScale = GetOverscanScaleVector();
        ApplyCameraPanOverflowIfNeeded();
    }

    private Vector3 GetOverscanScaleVector()
    {
        return new Vector3(
            Mathf.Max(1f, _overscanScaleX),
            Mathf.Max(1f, _overscanScale),
            1f);
    }

    private void ApplyCameraPanOverflowIfNeeded()
    {
        CameraController cameraController = CameraController.Instance ?? FindObjectOfType<CameraController>(true);
        if (cameraController == null || !cameraController.MovesRoot(_rectTransform))
        {
            return;
        }

        float horizontalOverflow = Mathf.Max(0f, cameraController.MaxOffsetX);
        if (horizontalOverflow <= 0f)
        {
            return;
        }

        _rectTransform.offsetMin = new Vector2(-horizontalOverflow, _rectTransform.offsetMin.y);
        _rectTransform.offsetMax = new Vector2(horizontalOverflow, _rectTransform.offsetMax.y);
    }

    private void TryReparentToRootCanvas()
    {
        if (_preserveHierarchyAndSibling || !_reparentToRootCanvas || _rectTransform == null)
        {
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            return;
        }

        Canvas rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
        Transform targetParent = rootCanvas.transform;

        if (_rectTransform.parent == targetParent)
        {
            ApplyRootCanvasSiblingPlacement(targetParent);
            return;
        }

        _rectTransform.SetParent(targetParent, false);
        ApplyRootCanvasSiblingPlacement(targetParent);
    }

    private void ApplyRootCanvasSiblingPlacement(Transform targetParent)
    {
        if (_preserveHierarchyAndSibling || _rectTransform == null || targetParent == null)
        {
            return;
        }

        switch (_rootCanvasSiblingPlacement)
        {
            case VideoBackgroundSiblingPlacement.LastSibling:
                _rectTransform.SetAsLastSibling();
                return;
            case VideoBackgroundSiblingPlacement.CustomIndex:
                _rectTransform.SetSiblingIndex(Mathf.Clamp(_customRootCanvasSiblingIndex, 0, targetParent.childCount - 1));
                return;
            case VideoBackgroundSiblingPlacement.AboveFirstBackground:
                _rectTransform.SetSiblingIndex(GetIndexAboveFirstBackground(targetParent));
                return;
            case VideoBackgroundSiblingPlacement.FirstSibling:
            default:
                _rectTransform.SetAsFirstSibling();
                return;
        }
    }

    private int GetIndexAboveFirstBackground(Transform targetParent)
    {
        if (targetParent == null)
        {
            return 0;
        }

        for (int i = 0; i < targetParent.childCount; i++)
        {
            Transform child = targetParent.GetChild(i);

            if (child == transform)
            {
                continue;
            }

            if (child.name.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Mathf.Clamp(i + 1, 0, targetParent.childCount - 1);
            }
        }

        return 0;
    }

    private void ApplyAudioSettings()
    {
        if (_videoPlayer == null)
        {
            return;
        }

        _videoPlayer.SetDirectAudioMute(0, _mute);
        _videoPlayer.SetDirectAudioVolume(0, _volume);
    }

    private void ApplyFitMode(float aspectRatio)
    {
        if (_rawImage == null)
        {
            return;
        }

        VideoBackgroundClipLayoutOverride clipOverride = GetActiveClipLayoutOverride();
        VideoBackgroundFitMode fitMode = ResolveActiveFitMode(clipOverride);

        if (fitMode == VideoBackgroundFitMode.Stretch)
        {
            RemoveAspectRatioFitter();
            _rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        if (UsesManualCoverLayout(fitMode))
        {
            ApplyCoverLayout(GetManualCoverAspectRatio(fitMode, aspectRatio), clipOverride);
            return;
        }

        EnsureAspectRatioFitter();

        if (_aspectRatioFitter == null)
        {
            return;
        }

        float fitterAspectRatio = Mathf.Max(0.01f, aspectRatio);

        _aspectRatioFitter.aspectRatio = fitterAspectRatio;
        _aspectRatioFitter.aspectMode = fitMode == VideoBackgroundFitMode.FitInsideParent
            ? AspectRatioFitter.AspectMode.FitInParent
            : AspectRatioFitter.AspectMode.EnvelopeParent;
    }

    private bool UsesManualCoverLayout()
    {
        return UsesManualCoverLayout(ResolveActiveFitMode(GetActiveClipLayoutOverride()));
    }

    private static bool UsesManualCoverLayout(VideoBackgroundFitMode fitMode)
    {
        return fitMode == VideoBackgroundFitMode.FillParent ||
               fitMode == VideoBackgroundFitMode.Cover16By9;
    }

    private static float GetManualCoverAspectRatio(VideoBackgroundFitMode fitMode, float aspectRatio)
    {
        return fitMode == VideoBackgroundFitMode.Cover16By9
            ? WidescreenAspectRatio
            : Mathf.Max(0.01f, aspectRatio);
    }

    private void ApplyCoverLayout(float aspectRatio, VideoBackgroundClipLayoutOverride clipOverride)
    {
        if (_rawImage == null)
        {
            return;
        }

        if (_rectTransform == null)
        {
            _rectTransform = _rawImage.rectTransform;
        }

        if (_rectTransform == null)
        {
            return;
        }

        RemoveAspectRatioFitter();

        Vector2 currentAnchoredPosition = _rectTransform.anchoredPosition;
        bool preservePosition = ShouldPreserveCameraDrivenPosition();
        Vector2 viewportSize = ResolveViewportSize();
        if (viewportSize.x <= 0.01f || viewportSize.y <= 0.01f)
        {
            return;
        }

        float safeAspectRatio = Mathf.Max(0.01f, aspectRatio);
        float width = viewportSize.x;
        float height = width / safeAspectRatio;

        if (height < viewportSize.y)
        {
            height = viewportSize.y;
            width = height * safeAspectRatio;
        }

        if (clipOverride != null && clipOverride.UseManualMinimumSize)
        {
            Vector2 manualSize = clipOverride.ManualMinimumSize;
            width = Mathf.Max(width, manualSize.x);
            height = Mathf.Max(height, manualSize.y);
        }

        float panOverflow = ResolveRequiredHorizontalPanOverflow(clipOverride);
        if (panOverflow > 0.01f)
        {
            width = Mathf.Max(width, viewportSize.x + panOverflow * 2f);
            height = width / safeAspectRatio;
        }

        if (clipOverride != null && clipOverride.ExtraPaddingY > 0.01f)
        {
            height += clipOverride.ExtraPaddingY * 2f;
        }

        float uniformOverscan = clipOverride != null && clipOverride.OverrideOverscanScale
            ? clipOverride.OverscanScale
            : Mathf.Max(1f, _overscanScale, _overscanScaleX);
        width *= uniformOverscan;
        height *= uniformOverscan;

        Vector2 baseAnchoredPosition = clipOverride != null && clipOverride.OverrideAnchoredPosition
            ? clipOverride.AnchoredPosition
            : _anchoredPosition;
        bool preserveCameraPosition = clipOverride == null || clipOverride.PreserveCameraDrivenPosition;

        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = _pivot;
        _rectTransform.sizeDelta = new Vector2(width, height);
        _rectTransform.anchoredPosition = preservePosition && preserveCameraPosition
            ? currentAnchoredPosition
            : baseAnchoredPosition;
        _rectTransform.localScale = Vector3.one;
        _rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    private Vector2 ResolveViewportSize()
    {
        RectTransform parent = _rectTransform != null ? _rectTransform.parent as RectTransform : null;
        Vector2 parentSize = parent != null ? parent.rect.size : Vector2.zero;
        if (parentSize.x > 0.01f && parentSize.y > 0.01f)
        {
            return parentSize;
        }

        Canvas canvas = GetComponentInParent<Canvas>(true);
        RectTransform canvasRect = canvas != null && canvas.rootCanvas != null
            ? canvas.rootCanvas.transform as RectTransform
            : null;
        Vector2 canvasSize = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
        if (canvasSize.x > 0.01f && canvasSize.y > 0.01f)
        {
            return canvasSize;
        }

        Vector2 ownSize = _rectTransform != null ? _rectTransform.rect.size : Vector2.zero;
        if (ownSize.x > 0.01f && ownSize.y > 0.01f)
        {
            return ownSize;
        }

        return new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
    }

    private float ResolveRequiredHorizontalPanOverflow(VideoBackgroundClipLayoutOverride clipOverride)
    {
        float currentRectOffset = _rectTransform != null
            ? Mathf.Abs(_rectTransform.anchoredPosition.x - _anchoredPosition.x)
            : 0f;

        CameraController cameraController = CameraController.Instance ?? FindObjectOfType<CameraController>(true);
        if (cameraController == null)
        {
            return currentRectOffset;
        }

        if (!_forceFullscreen)
        {
            return currentRectOffset;
        }

        float speakerOffset = Mathf.Max(
            Mathf.Abs(cameraController.leftOffset),
            Mathf.Abs(cameraController.centerOffset),
            Mathf.Abs(cameraController.rightOffset));
        float currentOffset = Mathf.Abs(cameraController.CurrentOffset);
        float overridePadding = clipOverride != null ? clipOverride.ExtraPanPaddingX : 0f;
        return Mathf.Max(speakerOffset, currentOffset, currentRectOffset) + overridePadding;
    }

    private VideoBackgroundFitMode ResolveActiveFitMode(VideoBackgroundClipLayoutOverride clipOverride)
    {
        return clipOverride != null && clipOverride.OverrideFitMode
            ? clipOverride.FitMode
            : _fitMode;
    }

    private VideoBackgroundClipLayoutOverride GetActiveClipLayoutOverride()
    {
        VideoClip activeClip = GetActiveClip();
        if (activeClip == null || _clipLayoutOverrides == null)
        {
            return null;
        }

        for (int i = 0; i < _clipLayoutOverrides.Length; i++)
        {
            VideoBackgroundClipLayoutOverride clipOverride = _clipLayoutOverrides[i];
            if (clipOverride != null && clipOverride.Matches(activeClip))
            {
                return clipOverride;
            }
        }

        return null;
    }

    private VideoClip GetActiveClip()
    {
        if (_sourceType == VideoBackgroundSourceType.VideoClip)
        {
            if (_clip != null)
            {
                return _clip;
            }

            if (_videoPlayer != null && _videoPlayer.clip != null)
            {
                return _videoPlayer.clip;
            }
        }

        return null;
    }

    private bool ShouldPreserveCameraDrivenPosition()
    {
        CameraController cameraController = CameraController.Instance ?? FindObjectOfType<CameraController>(true);
        return cameraController != null && _rectTransform != null && cameraController.MovesRoot(_rectTransform);
    }

    private float GetCurrentAspectRatio()
    {
        if (_renderTexture != null && _renderTexture.height > 0)
        {
            return (float)_renderTexture.width / _renderTexture.height;
        }

        return GetAspectRatio(GetPreferredTextureSize());
    }

    private float GetAspectRatio(Vector2Int size)
    {
        return size.y > 0 ? (float)size.x / size.y : 1f;
    }

    private void EnsureAspectRatioFitter()
    {
        if (_aspectRatioFitter != null)
        {
            return;
        }

        _aspectRatioFitter = GetComponent<AspectRatioFitter>() ?? gameObject.AddComponent<AspectRatioFitter>();
    }

    private void RemoveAspectRatioFitter()
    {
        if (_aspectRatioFitter == null)
        {
            _aspectRatioFitter = GetComponent<AspectRatioFitter>();
        }

        if (_aspectRatioFitter == null)
        {
            return;
        }

        _aspectRatioFitter.enabled = false;

        if (Application.isPlaying)
        {
            Destroy(_aspectRatioFitter);
        }
        else
        {
            DestroyImmediate(_aspectRatioFitter);
        }

        _aspectRatioFitter = null;
    }

    private void RegisterVideoPlayerHandlers()
    {
        if (_videoPlayer == null)
        {
            return;
        }

        if (!_preparedHandlerRegistered)
        {
            _videoPlayer.prepareCompleted += OnVideoPrepared;
            _preparedHandlerRegistered = true;
        }

        if (!_errorHandlerRegistered)
        {
            _videoPlayer.errorReceived += OnVideoError;
            _errorHandlerRegistered = true;
        }
    }

    private void UnregisterVideoPlayerHandlers()
    {
        if (_videoPlayer == null)
        {
            return;
        }

        if (_preparedHandlerRegistered)
        {
            _videoPlayer.prepareCompleted -= OnVideoPrepared;
            _preparedHandlerRegistered = false;
        }

        if (_errorHandlerRegistered)
        {
            _videoPlayer.errorReceived -= OnVideoError;
            _errorHandlerRegistered = false;
        }
    }

    private void ReleaseRenderTexture()
    {
        if (_renderTexture == null)
        {
            return;
        }

        _renderTexture.Release();

        if (Application.isPlaying)
        {
            Destroy(_renderTexture);
        }
        else
        {
            DestroyImmediate(_renderTexture);
        }

        _renderTexture = null;
    }

    private void InvokePrepared()
    {
        try
        {
            Prepared?.Invoke();
            _prepared.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"VideoBackgroundPlayer: prepared callback failed: {exception.Message}", this);
        }
    }

    private void InvokeStarted()
    {
        try
        {
            Started?.Invoke();
            _started.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"VideoBackgroundPlayer: started callback failed: {exception.Message}", this);
        }
    }

    private void InvokeStopped()
    {
        try
        {
            Stopped?.Invoke();
            _stopped.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"VideoBackgroundPlayer: stopped callback failed: {exception.Message}", this);
        }
    }

    private void InvokeFailed(string message)
    {
        Debug.LogWarning($"VideoBackgroundPlayer: {message}", this);
        SetLayerVisible(false);

        try
        {
            Failed?.Invoke(message);
            _failed.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"VideoBackgroundPlayer: failed callback failed: {exception.Message}", this);
        }
    }

    private void StartPrepareWatchdog()
    {
        StopPrepareWatchdog();

        if (!isActiveAndEnabled || _videoPlayer == null)
            return;

        _prepareWatchdogRoutine = StartCoroutine(PrepareWatchdog());
    }

    private void StopPrepareWatchdog()
    {
        if (_prepareWatchdogRoutine == null)
            return;

        StopCoroutine(_prepareWatchdogRoutine);
        _prepareWatchdogRoutine = null;
    }

    private IEnumerator PrepareWatchdog()
    {
        yield return new WaitForSecondsRealtime(_prepareTimeout);

        _prepareWatchdogRoutine = null;

        if (!_isPreparing || !_shouldPlayAfterPrepare || _videoPlayer == null || !HasValidSource())
            yield break;

        _isPreparing = false;
        _shouldPlayAfterPrepare = false;

        try
        {
            if (!_videoPlayer.isPlaying)
                _videoPlayer.Play();

            SetLayerVisible(true);
            InvokeStarted();
        }
        catch (Exception exception)
        {
            Stop();
            InvokeFailed(exception.Message);
        }
    }
}
