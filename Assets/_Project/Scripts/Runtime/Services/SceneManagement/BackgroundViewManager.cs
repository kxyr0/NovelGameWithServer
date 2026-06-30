using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Video;

public class BackgroundViewManager : MonoBehaviour
{
    const float CutsceneHorizontalInset = -200f;

    [Header("Static background")]
    [SerializeField]
    [FormerlySerializedAs("backgroundImage")]
    private Image backgroundImage;

    [Header("Video background")]
    [SerializeField]
    [FormerlySerializedAs("videoPlayer")]
    private VideoPlayer videoPlayer;

    [SerializeField]
    [FormerlySerializedAs("videoRawImage")]
    private RawImage videoRawImage;

    [Header("GIF background")]
    [SerializeField]
    [FormerlySerializedAs("gifPlayer")]
    private AnimatedGifPlayer gifPlayer;

    [Header("Responsive layout")]
    [SerializeField] private bool stretchBackgroundToParent = true;
    [SerializeField] private bool stretchBackgroundTexture = true;
    [SerializeField, Min(1f)] private float backgroundOverscanScale = 1.08f;
    [SerializeField, Min(1f)] private float backgroundOverscanScaleX = 1.14f;
    [SerializeField, Min(1f)] private float videoBackgroundOverscanScale = 1.16f;
    [SerializeField, Min(1f)] private float videoBackgroundOverscanScaleX = 1.24f;
    [SerializeField]
    [Tooltip("Если включено, видеофон сохраняет 16:9 и покрывает весь экран, обрезая лишнее по краям.")]
    private bool coverVideoBackground16By9 = true;
    [SerializeField]
    [Tooltip("Если включено, отдельный VideoBackgroundPlayer использует настройки увеличения видео из этого компонента.")]
    private bool overrideStandaloneVideoOverscan;

    [Header("Cutscene framing")]
    [SerializeField] private float cutsceneInspectorLeft = CutsceneHorizontalInset;
    [SerializeField] private float cutsceneInspectorRight = CutsceneHorizontalInset;

    [Header("Background transition")]
    [SerializeField, Min(0f)] private float backgroundTransitionDuration = 0.35f;

    Sprite currentBackground;
    VideoClip currentVideo;
    RenderTexture _activeRenderTexture;
    bool _videoPreparedHandlerRegistered;
    VideoPlayer _registeredVideoPlayer;
    VideoBackgroundPlayer _videoBackgroundPlayer;
    CanvasGroup _videoCanvasGroup;
    bool _standaloneVideoHandlersRegistered;
    bool _cutsceneHorizontalFramingActive;
    bool _cutsceneMediaLayoutActive;
    Coroutine _backgroundTransitionRoutine;
    Coroutine _videoAudioFadeRoutine;
    Image _backgroundTransitionImage;
    float _standaloneVideoAudioRestoreVolume = -1f;
    float _directVideoAudioRestoreVolume = -1f;
    readonly Dictionary<RectTransform, Vector2> _cutsceneSavedHorizontalOffsets =
        new Dictionary<RectTransform, Vector2>();

    void Awake()
    {
        EnforceFixedCutsceneFraming();
        ConfigureVideoPlayer();
        EnsureAssignedVideoRawImage();
        ApplyBackgroundLayout();

        if (videoRawImage != null)
        {
            AlignRawImageWithBackground(videoRawImage);
            SetVideoLayerVisible(false);
        }

        if (gifPlayer != null)
            gifPlayer.gameObject.SetActive(false);

    }

    void OnValidate()
    {
        backgroundOverscanScale = Mathf.Max(1f, backgroundOverscanScale);
        backgroundOverscanScaleX = Mathf.Max(1f, backgroundOverscanScaleX);
        videoBackgroundOverscanScale = Mathf.Max(1f, videoBackgroundOverscanScale);
        videoBackgroundOverscanScaleX = Mathf.Max(1f, videoBackgroundOverscanScaleX);
        backgroundTransitionDuration = Mathf.Max(0f, backgroundTransitionDuration);
        EnforceFixedCutsceneFraming();
    }

    void OnDisable()
    {
        CancelBackgroundTransition();
        StopVideo();
        StopGif();
    }

    public void ClearBackground()
    {
        StopVideo();
        StopGif();

        currentBackground = null;

        if (backgroundImage == null)
            return;

        RuntimeTextureFallback.ApplyImagePlaceholder(backgroundImage);
        backgroundImage.color = Color.black;
        backgroundImage.enabled = true;
        ApplyBackgroundLayout();
        backgroundImage.gameObject.SetActive(true);
    }

    public void HideCurrentMediaBeforeLayoutSwitch()
    {
        CancelBackgroundTransition();
        StopVideo();
        StopGif();

        currentBackground = null;
        currentVideo = null;

        if (backgroundImage == null)
            return;

        RuntimeTextureFallback.ApplyImagePlaceholder(backgroundImage);
        backgroundImage.color = Color.black;
        ApplyBackgroundLayout();
    }

    public void BeginCutsceneHorizontalFraming()
    {
        _cutsceneMediaLayoutActive = true;

        if (!_cutsceneHorizontalFramingActive)
        {
            _cutsceneHorizontalFramingActive = true;
            _cutsceneSavedHorizontalOffsets.Clear();
        }

        ApplyBackgroundLayout();
        ApplyCutsceneHorizontalFramingToCurrentLayers();
    }

    public void EndCutsceneHorizontalFraming()
    {
        bool hadCutsceneLayout = _cutsceneMediaLayoutActive;
        _cutsceneMediaLayoutActive = false;

        if (!_cutsceneHorizontalFramingActive)
        {
            if (hadCutsceneLayout)
                ApplyBackgroundLayout();

            return;
        }

        foreach (KeyValuePair<RectTransform, Vector2> entry in _cutsceneSavedHorizontalOffsets)
        {
            if (IsActiveBackgroundTransitionRect(entry.Key))
                continue;

            RestoreHorizontalOffsets(entry.Key, entry.Value);
        }

        _cutsceneSavedHorizontalOffsets.Clear();
        _cutsceneHorizontalFramingActive = false;

        ApplyBackgroundLayout();
    }

    public void SetBackground(Sprite sprite)
    {
        if (sprite == null)
            return;

        if (backgroundImage == null)
        {
            Debug.LogWarning("BackgroundViewManager: backgroundImage is not assigned.", this);
            return;
        }

        if (currentBackground == sprite)
        {
            EnsureStaticBackgroundVisible(sprite);
            return;
        }

        Sprite previousSprite = backgroundImage.sprite;
        Color previousColor = backgroundImage.color;
        bool animateTransition = ShouldAnimateStaticBackgroundTransition(previousSprite, sprite);

        StopVideo();
        StopGif();

        EnsureStaticBackgroundVisible(sprite);
        if (animateTransition)
            StartStaticBackgroundTransition(previousSprite, previousColor);

        currentBackground = sprite;
        currentVideo = null;
    }

    public void PreviewStaticBackground(Sprite sprite)
    {
        CancelBackgroundTransition();
        StopVideo();
        StopGif();

        if (sprite == null)
        {
            ClearBackground();
            return;
        }

        if (backgroundImage == null)
        {
            Debug.LogWarning("BackgroundViewManager: backgroundImage is not assigned.", this);
            return;
        }

        currentBackground = sprite;
        currentVideo = null;
        EndCutsceneHorizontalFraming();
        EnsureStaticBackgroundVisible(sprite);
    }

    void EnsureStaticBackgroundVisible(Sprite sprite)
    {
        if (backgroundImage == null)
            return;

        backgroundImage.sprite = sprite;
        backgroundImage.color = Color.white;
        backgroundImage.enabled = true;
        ApplyBackgroundLayout();
        backgroundImage.gameObject.SetActive(true);
    }

    bool ShouldAnimateStaticBackgroundTransition(Sprite previousSprite, Sprite nextSprite)
    {
        return Application.isPlaying &&
               backgroundTransitionDuration > 0f &&
               previousSprite != null &&
               nextSprite != null &&
               previousSprite != nextSprite &&
               backgroundImage != null &&
               backgroundImage.isActiveAndEnabled &&
               backgroundImage.gameObject.activeInHierarchy &&
               backgroundImage.color.a > 0.01f;
    }

    void StartStaticBackgroundTransition(Sprite previousSprite, Color previousColor)
    {
        Image transitionImage = EnsureBackgroundTransitionImage();
        if (transitionImage == null)
            return;

        CancelBackgroundTransition();

        CopyRectTransformLayout(backgroundImage.rectTransform, transitionImage.rectTransform);
        transitionImage.sprite = previousSprite;
        transitionImage.color = previousColor;
        transitionImage.preserveAspect = backgroundImage.preserveAspect;
        transitionImage.type = backgroundImage.type;
        transitionImage.raycastTarget = false;
        transitionImage.enabled = true;
        transitionImage.gameObject.SetActive(true);
        transitionImage.transform.SetSiblingIndex(backgroundImage.transform.GetSiblingIndex() + 1);

        _backgroundTransitionRoutine = StartCoroutine(FadeOutBackgroundTransition(transitionImage));
    }

    Image EnsureBackgroundTransitionImage()
    {
        if (_backgroundTransitionImage != null)
            return _backgroundTransitionImage;

        if (backgroundImage == null || backgroundImage.transform.parent == null)
            return null;

        var transitionObject = new GameObject("Background Transition", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var transitionRect = transitionObject.GetComponent<RectTransform>();
        transitionRect.SetParent(backgroundImage.transform.parent, false);

        _backgroundTransitionImage = transitionObject.GetComponent<Image>();
        _backgroundTransitionImage.raycastTarget = false;
        transitionObject.SetActive(false);
        return _backgroundTransitionImage;
    }

    IEnumerator FadeOutBackgroundTransition(Image transitionImage)
    {
        float duration = Mathf.Max(0.01f, backgroundTransitionDuration);
        float startAlpha = transitionImage != null ? transitionImage.color.a : 0f;
        float elapsed = 0f;

        while (transitionImage != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            Color color = transitionImage.color;
            color.a = Mathf.Lerp(startAlpha, 0f, eased);
            transitionImage.color = color;
            yield return null;
        }

        if (transitionImage != null)
        {
            transitionImage.sprite = null;
            transitionImage.gameObject.SetActive(false);
        }

        _backgroundTransitionRoutine = null;
    }

    void CancelBackgroundTransition()
    {
        if (_backgroundTransitionRoutine != null)
        {
            StopCoroutine(_backgroundTransitionRoutine);
            _backgroundTransitionRoutine = null;
        }

        if (_backgroundTransitionImage == null)
            return;

        _backgroundTransitionImage.sprite = null;
        _backgroundTransitionImage.gameObject.SetActive(false);
    }

    static void CopyRectTransformLayout(RectTransform source, RectTransform target)
    {
        if (source == null || target == null)
            return;

        if (target.parent != source.parent)
            target.SetParent(source.parent, false);

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.offsetMin = source.offsetMin;
        target.offsetMax = source.offsetMax;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.pivot = source.pivot;
        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
    }

    public void SetBackgroundVideo(VideoClip clip)
    {
        CancelVideoAudioFade(true);

        if (clip == null)
        {
            StopVideo();
            return;
        }

        if (currentVideo == clip && TryShowCurrentVideo(clip))
            return;

        StopVideo();
        StopGif();

        if (backgroundImage != null)
        {
            RuntimeTextureFallback.EnsureImageVisible(backgroundImage, currentBackground);
            backgroundImage.color = Color.white;
            backgroundImage.enabled = true;
            ApplyBackgroundLayout();
        }

        currentVideo = clip;
        currentBackground = null;

        if (!EnsureVideoView())
        {
            Debug.LogWarning("BackgroundViewManager: videoPlayer or videoRawImage is not assigned.", this);
            return;
        }

        SetVideoLayerVisible(false);

        if (_videoBackgroundPlayer != null)
        {
            EnsureStandaloneVideoPlayerComponents();
            ReleaseActiveRenderTexture();
            ApplyStandaloneVideoPlayerLayout(_videoBackgroundPlayer);
            _videoBackgroundPlayer.SetClip(clip);
            SetVideoLayerVisible(true);
            _videoBackgroundPlayer.enabled = true;
            _videoBackgroundPlayer.Play(clip);
            return;
        }

        ReleaseActiveRenderTexture();

        int width = Mathf.Max(16, (int)clip.width);
        int height = Mathf.Max(16, (int)clip.height);
        var rt = new RenderTexture(width, height, 0);
        _activeRenderTexture = rt;
        rt.Create();

        try
        {
            videoPlayer.targetTexture = rt;
            videoRawImage.texture = rt;
            videoRawImage.color = Color.white;
            SetVideoLayerVisible(false);
            videoPlayer.clip = clip;
            videoPlayer.gameObject.SetActive(true);
            videoPlayer.Prepare();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"BackgroundViewManager: failed to prepare video '{clip.name}': {exception.Message}", this);
            StopVideo();
        }
    }

    bool TryShowCurrentVideo(VideoClip clip)
    {
        if (clip == null || !EnsureVideoView())
            return false;

        if (_videoBackgroundPlayer != null)
        {
            if (_videoBackgroundPlayer.Clip != clip)
                return false;

            AlignRawImageWithBackground(videoRawImage);
            SetVideoLayerVisible(true);

            if (backgroundImage != null)
                backgroundImage.gameObject.SetActive(false);

            if (!_videoBackgroundPlayer.IsPlaying && !_videoBackgroundPlayer.IsPreparing)
                _videoBackgroundPlayer.Play(clip);

            return true;
        }

        if (videoPlayer.clip != clip ||
            videoPlayer.targetTexture == null ||
            videoRawImage.texture == null ||
            !videoPlayer.isPrepared)
        {
            return false;
        }

        videoRawImage.color = Color.white;
        AlignRawImageWithBackground(videoRawImage);
        SetVideoLayerVisible(true);

        if (backgroundImage != null)
            backgroundImage.gameObject.SetActive(false);

        if (!videoPlayer.isPlaying)
            videoPlayer.Play();

        return true;
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        if (vp == null || !isActiveAndEnabled)
            return;

        try
        {
            if (videoRawImage != null)
            {
                videoRawImage.color = Color.white;
                AlignRawImageWithBackground(videoRawImage);
                SetVideoLayerVisible(true);
            }

            if (backgroundImage != null)
                backgroundImage.gameObject.SetActive(false);

            vp.Play();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"BackgroundViewManager: failed to play video: {exception.Message}", this);
        }
    }

    public void StopVideo()
    {
        StopVideo(true);
    }

    void StopVideo(bool cancelVideoAudioFade)
    {
        if (cancelVideoAudioFade)
            CancelVideoAudioFade(true);

        if (_videoBackgroundPlayer != null)
        {
            _videoBackgroundPlayer.Stop();
            _videoBackgroundPlayer.SetClip(null);
            _videoBackgroundPlayer.enabled = false;
        }

        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Stop();

        if (videoRawImage != null)
        {
            videoRawImage.texture = null;
            SetVideoLayerVisible(false);
        }

        if (videoPlayer != null)
        {
            videoPlayer.clip = null;
            videoPlayer.targetTexture = null;
        }

        ReleaseActiveRenderTexture();
        currentVideo = null;
    }

    internal void FadeOutVideoAudioAndStop(float duration)
    {
        duration = Mathf.Max(0f, duration);

        if (!isActiveAndEnabled || duration <= 0f)
        {
            StopVideo();
            return;
        }

        bool hasStandaloneVideo = _videoBackgroundPlayer != null &&
                                  (_videoBackgroundPlayer.IsPlaying || _videoBackgroundPlayer.IsPreparing);
        bool hasDirectVideo = videoPlayer != null && (videoPlayer.isPlaying || videoPlayer.isPrepared);

        if (!hasStandaloneVideo && !hasDirectVideo)
        {
            StopVideo();
            return;
        }

        CancelVideoAudioFade(true);
        _standaloneVideoAudioRestoreVolume = _videoBackgroundPlayer != null ? _videoBackgroundPlayer.Volume : -1f;
        _directVideoAudioRestoreVolume = GetDirectAudioVolumeOrDefault(videoPlayer, 1f);
        _videoAudioFadeRoutine = StartCoroutine(FadeOutVideoAudioAndStopRoutine(duration));
    }

    IEnumerator FadeOutVideoAudioAndStopRoutine(float duration)
    {
        float elapsed = 0f;
        float standaloneStartVolume = _standaloneVideoAudioRestoreVolume >= 0f ? _standaloneVideoAudioRestoreVolume : 1f;
        float directStartVolume = _directVideoAudioRestoreVolume >= 0f ? _directVideoAudioRestoreVolume : 1f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float standaloneVolume = Mathf.Lerp(standaloneStartVolume, 0f, t);
            float directVolume = Mathf.Lerp(directStartVolume, 0f, t);

            if (_videoBackgroundPlayer != null)
                _videoBackgroundPlayer.SetVolume(standaloneVolume);

            SetDirectAudioVolumeIfPossible(videoPlayer, directVolume);
            yield return null;
        }

        if (_videoBackgroundPlayer != null)
            _videoBackgroundPlayer.SetVolume(0f);
        SetDirectAudioVolumeIfPossible(videoPlayer, 0f);

        StopVideo(false);
        RestoreVideoAudioVolumes();
        _videoAudioFadeRoutine = null;
        _standaloneVideoAudioRestoreVolume = -1f;
        _directVideoAudioRestoreVolume = -1f;
    }

    void CancelVideoAudioFade(bool restoreVolume)
    {
        if (_videoAudioFadeRoutine != null)
        {
            StopCoroutine(_videoAudioFadeRoutine);
            _videoAudioFadeRoutine = null;
        }

        if (restoreVolume)
            RestoreVideoAudioVolumes();

        _standaloneVideoAudioRestoreVolume = -1f;
        _directVideoAudioRestoreVolume = -1f;
    }

    void RestoreVideoAudioVolumes()
    {
        if (_videoBackgroundPlayer != null && _standaloneVideoAudioRestoreVolume >= 0f)
            _videoBackgroundPlayer.SetVolume(_standaloneVideoAudioRestoreVolume);

        if (videoPlayer != null && _directVideoAudioRestoreVolume >= 0f)
            SetDirectAudioVolumeIfPossible(videoPlayer, _directVideoAudioRestoreVolume);
    }

    static float GetDirectAudioVolumeOrDefault(VideoPlayer player, float fallback)
    {
        if (player == null)
            return fallback;

        try
        {
            return Mathf.Clamp01(player.GetDirectAudioVolume(0));
        }
        catch
        {
            return fallback;
        }
    }

    static void SetDirectAudioVolumeIfPossible(VideoPlayer player, float volume)
    {
        if (player == null)
            return;

        try
        {
            player.SetDirectAudioVolume(0, Mathf.Clamp01(volume));
        }
        catch
        {
            // Some imported clips have no direct audio track. Stopping the player still handles them.
        }
    }

    public void SetBackgroundGif(TextAsset gifAsset)
    {
        if (gifAsset == null)
        {
            StopGif();
            return;
        }

        StopVideo();

        if (backgroundImage != null)
        {
            ApplyBackgroundLayout();
            backgroundImage.gameObject.SetActive(false);
        }

        currentBackground = null;
        currentVideo = null;

        if (!EnsureGifPlayer())
        {
            Debug.LogWarning("BackgroundViewManager: gifPlayer is not assigned. GIF will not be played.", this);
            return;
        }

        gifPlayer.gameObject.SetActive(true);
        gifPlayer.Play(gifAsset);
    }

    public void StopGif()
    {
        if (gifPlayer == null)
            return;

        gifPlayer.Stop();
        gifPlayer.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        StopVideo();
        StopGif();
        UnregisterStandaloneVideoHandlers();
        UnregisterVideoPreparedHandler();

        ReleaseActiveRenderTexture();
    }

    bool EnsureVideoView()
    {
        EnsureAssignedVideoRawImage();

        if (videoRawImage == null)
            videoRawImage = CreateRawImage("Video Background", videoBackgroundOverscanScaleX, videoBackgroundOverscanScale);
        else
            AlignRawImageWithBackground(videoRawImage);

        SetStandaloneVideoPlayer(videoRawImage.GetComponent<VideoBackgroundPlayer>());
        if (_videoBackgroundPlayer != null)
        {
            EnsureStandaloneVideoPlayerComponents();
            return true;
        }

        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>() ??
                          (videoRawImage != null ? videoRawImage.GetComponent<VideoPlayer>() : null) ??
                          gameObject.AddComponent<VideoPlayer>();

        ConfigureVideoPlayer();
        return videoPlayer != null && videoRawImage != null;
    }

    void EnsureAssignedVideoRawImage()
    {
        if (videoRawImage != null)
            return;

        Canvas canvas = backgroundImage != null ? backgroundImage.GetComponentInParent<Canvas>(true) : null;
        Transform searchRoot = canvas != null && canvas.rootCanvas != null
            ? canvas.rootCanvas.transform
            : backgroundImage != null && backgroundImage.transform.parent != null
                ? backgroundImage.transform.parent
                : transform;

        foreach (RawImage rawImage in searchRoot.GetComponentsInChildren<RawImage>(true))
        {
            if (rawImage == null)
                continue;

            bool isVideoLayer = rawImage.GetComponent<VideoBackgroundPlayer>() != null ||
                                rawImage.name.IndexOf("VideoBackground", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                rawImage.name.IndexOf("Video Background", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isVideoLayer)
                continue;

            videoRawImage = rawImage;
            break;
        }
    }

    void AlignRawImageWithBackground(RawImage rawImage)
    {
        if (rawImage == null || backgroundImage == null)
            return;

        ApplyBackgroundLayout();

        VideoBackgroundPlayer standalonePlayer = rawImage.GetComponent<VideoBackgroundPlayer>();
        SetStandaloneVideoPlayer(standalonePlayer);
        if (standalonePlayer != null)
        {
            EnsureStandaloneVideoPlayerComponents();
            ApplyStandaloneVideoPlayerLayout(standalonePlayer);
            ApplyCutsceneHorizontalFramingIfActive(standalonePlayer.RectTransform);
            return;
        }

        RectTransform rectTransform = rawImage.rectTransform;
        RectTransform sourceRect = backgroundImage.rectTransform;
        if (rectTransform == null || sourceRect == null)
            return;

        Transform targetParent = backgroundImage.transform.parent;
        if (targetParent != null && rectTransform.parent != targetParent)
            rectTransform.SetParent(targetParent, false);

        rectTransform.anchorMin = sourceRect.anchorMin;
        rectTransform.anchorMax = sourceRect.anchorMax;
        rectTransform.anchoredPosition = sourceRect.anchoredPosition;
        rectTransform.sizeDelta = sourceRect.sizeDelta;
        rectTransform.pivot = sourceRect.pivot;
        rectTransform.localScale = _cutsceneMediaLayoutActive
            ? Vector3.one
            : GetOverscanScaleVector(videoBackgroundOverscanScaleX, videoBackgroundOverscanScale);
        ApplyRawVideoBackgroundFit(rawImage);
        ApplyCutsceneHorizontalFramingIfActive(rectTransform);
        ReapplyCameraPanAfterBackgroundLayout(rectTransform, linkedRootAlreadyIncludesCameraOffset: true);
        rectTransform.SetSiblingIndex(backgroundImage.transform.GetSiblingIndex() + 1);
    }

    void SetVideoLayerVisible(bool visible)
    {
        if (videoRawImage == null)
            return;

        if (!videoRawImage.gameObject.activeSelf)
            videoRawImage.gameObject.SetActive(true);

        videoRawImage.enabled = true;
        videoRawImage.raycastTarget = false;

        CanvasGroup canvasGroup = GetVideoCanvasGroup();
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    CanvasGroup GetVideoCanvasGroup()
    {
        if (videoRawImage == null)
            return null;

        if (_videoCanvasGroup == null)
            _videoCanvasGroup = videoRawImage.GetComponent<CanvasGroup>();

        if (_videoCanvasGroup == null)
            _videoCanvasGroup = videoRawImage.gameObject.AddComponent<CanvasGroup>();

        return _videoCanvasGroup;
    }

    void SetStandaloneVideoPlayer(VideoBackgroundPlayer player)
    {
        if (_videoBackgroundPlayer == player)
            return;

        UnregisterStandaloneVideoHandlers();
        _videoBackgroundPlayer = player;
        EnsureStandaloneVideoPlayerComponents();
        RegisterStandaloneVideoHandlers();
    }

    void ApplyStandaloneVideoPlayerLayout(VideoBackgroundPlayer standalonePlayer)
    {
        if (standalonePlayer == null)
            return;

        if (_cutsceneMediaLayoutActive)
            standalonePlayer.SetOverscanScale(1f, 1f);
        else if (coverVideoBackground16By9)
            standalonePlayer.SetOverscanScale(1f, 1f);
        else if (overrideStandaloneVideoOverscan)
            standalonePlayer.SetOverscanScale(videoBackgroundOverscanScaleX, videoBackgroundOverscanScale);

        if (!_cutsceneMediaLayoutActive && coverVideoBackground16By9)
        {
            standalonePlayer.ApplyWidescreenCover();
        }
        else
        {
            standalonePlayer.SetLayoutMode(VideoBackgroundLayoutMode.StretchToParent);
            standalonePlayer.StretchToParent();
            standalonePlayer.SetFitMode(ResolveVideoBackgroundFitMode());
        }

        ApplyCutsceneHorizontalFramingIfActive(standalonePlayer.RectTransform);
        ReapplyCameraPanAfterBackgroundLayout(standalonePlayer.RectTransform);
    }

    void EnsureStandaloneVideoPlayerComponents()
    {
        if (_videoBackgroundPlayer == null)
            return;

        if (videoRawImage == null)
            videoRawImage = _videoBackgroundPlayer.GetComponent<RawImage>();

        if (videoRawImage != null)
            videoRawImage.raycastTarget = false;

        VideoPlayer standaloneVideoPlayer = _videoBackgroundPlayer.GetComponent<VideoPlayer>();
        if (standaloneVideoPlayer == null)
            standaloneVideoPlayer = _videoBackgroundPlayer.gameObject.AddComponent<VideoPlayer>();

        if (videoPlayer != standaloneVideoPlayer)
        {
            UnregisterVideoPreparedHandler();
            videoPlayer = standaloneVideoPlayer;
        }
    }

    void RegisterStandaloneVideoHandlers()
    {
        if (_videoBackgroundPlayer == null || _standaloneVideoHandlersRegistered)
            return;

        _videoBackgroundPlayer.Prepared += OnStandaloneVideoReady;
        _videoBackgroundPlayer.Started += OnStandaloneVideoReady;
        _videoBackgroundPlayer.Failed += OnStandaloneVideoFailed;
        _standaloneVideoHandlersRegistered = true;
    }

    void UnregisterStandaloneVideoHandlers()
    {
        if (_videoBackgroundPlayer == null || !_standaloneVideoHandlersRegistered)
            return;

        _videoBackgroundPlayer.Prepared -= OnStandaloneVideoReady;
        _videoBackgroundPlayer.Started -= OnStandaloneVideoReady;
        _videoBackgroundPlayer.Failed -= OnStandaloneVideoFailed;
        _standaloneVideoHandlersRegistered = false;
    }

    void OnStandaloneVideoReady()
    {
        if (currentVideo == null)
            return;

        if (videoRawImage != null)
            AlignRawImageWithBackground(videoRawImage);

        SetVideoLayerVisible(true);

        if (backgroundImage != null)
            backgroundImage.gameObject.SetActive(false);
    }

    void OnStandaloneVideoFailed(string message)
    {
        Debug.LogWarning($"BackgroundViewManager: failed to play video: {message}", this);
        SetVideoLayerVisible(false);

        if (backgroundImage != null && backgroundImage.sprite != null)
        {
            backgroundImage.enabled = true;
            backgroundImage.gameObject.SetActive(true);
        }
    }

    void ApplyBackgroundLayout()
    {
        if (backgroundImage == null)
            return;

        if (_cutsceneMediaLayoutActive)
        {
            backgroundImage.preserveAspect = true;
            ApplyFillParentLayout(backgroundImage.rectTransform, 1f, 1f);
            ApplyCameraPanOverflowIfNeeded(backgroundImage.rectTransform);
            ApplyCutsceneHorizontalFramingIfActive(backgroundImage.rectTransform);
            ApplyCutsceneCoverScale(backgroundImage);
            ReapplyCameraPanAfterBackgroundLayout();
            return;
        }

        if (stretchBackgroundTexture)
            backgroundImage.preserveAspect = false;

        ApplyFillParentLayout(backgroundImage.rectTransform, backgroundOverscanScaleX, backgroundOverscanScale);
        ApplyCameraPanOverflowIfNeeded(backgroundImage.rectTransform);
        ApplyCutsceneHorizontalFramingIfActive(backgroundImage.rectTransform);
        ReapplyCameraPanAfterBackgroundLayout();
    }

    void ApplyFillParentLayout(RectTransform rectTransform, float overscanScaleX, float overscanScaleY)
    {
        if (!stretchBackgroundToParent || rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = GetOverscanScaleVector(overscanScaleX, overscanScaleY);
    }

    void ApplyCutsceneCoverScale(Image image)
    {
        if (!stretchBackgroundToParent || image == null || image.sprite == null)
            return;

        RectTransform rectTransform = image.rectTransform;
        if (rectTransform == null)
            return;

        Vector2 rectSize = ResolveRectSize(rectTransform);
        if (rectSize.x <= 0.01f || rectSize.y <= 0.01f)
            return;

        Rect spriteRect = image.sprite.rect;
        if (spriteRect.width <= 0.01f || spriteRect.height <= 0.01f)
            return;

        float spriteAspect = spriteRect.width / spriteRect.height;
        float rectAspect = rectSize.x / rectSize.y;
        float displayedWidth = rectSize.x;
        float displayedHeight = rectSize.y;

        if (spriteAspect > rectAspect)
            displayedHeight = displayedWidth / spriteAspect;
        else
            displayedWidth = displayedHeight * spriteAspect;

        float coverScaleX = rectSize.x / Mathf.Max(0.01f, displayedWidth);
        float coverScaleY = rectSize.y / Mathf.Max(0.01f, displayedHeight);
        float coverScale = Mathf.Max(1f, coverScaleX, coverScaleY);
        rectTransform.localScale = new Vector3(coverScale, coverScale, 1f);
    }

    static Vector2 ResolveRectSize(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return Vector2.zero;

        RectTransform parent = rectTransform.parent as RectTransform;
        if (parent == null)
            return rectTransform.rect.size;

        Vector2 parentSize = parent.rect.size;
        float width = parentSize.x * (rectTransform.anchorMax.x - rectTransform.anchorMin.x) -
                      rectTransform.offsetMin.x + rectTransform.offsetMax.x;
        float height = parentSize.y * (rectTransform.anchorMax.y - rectTransform.anchorMin.y) -
                       rectTransform.offsetMin.y + rectTransform.offsetMax.y;

        if (width <= 0.01f || height <= 0.01f)
            return rectTransform.rect.size;

        return new Vector2(width, height);
    }

    void ApplyCameraPanOverflowIfNeeded(RectTransform rectTransform)
    {
        if (!stretchBackgroundToParent || rectTransform == null)
            return;

        if (_cutsceneHorizontalFramingActive)
        {
            CaptureAndApplyCutsceneHorizontalOffsets(rectTransform);
            return;
        }

        CameraController cameraController = CameraController.Instance ?? FindObjectOfType<CameraController>(true);
        if (cameraController == null || !cameraController.MovesRoot(rectTransform))
            return;

        float horizontalOverflow = Mathf.Max(0f, cameraController.MaxOffsetX);
        if (horizontalOverflow <= 0f)
            return;

        rectTransform.offsetMin = new Vector2(-horizontalOverflow, rectTransform.offsetMin.y);
        rectTransform.offsetMax = new Vector2(horizontalOverflow, rectTransform.offsetMax.y);
    }

    bool EnsureGifPlayer()
    {
        if (gifPlayer == null)
        {
            var rawImage = CreateRawImage("GIF Background", backgroundOverscanScaleX, backgroundOverscanScale);
            if (rawImage != null)
                gifPlayer = rawImage.GetComponent<AnimatedGifPlayer>() ?? rawImage.gameObject.AddComponent<AnimatedGifPlayer>();
        }

        return gifPlayer != null;
    }

    RawImage CreateRawImage(string objectName, float overscanScaleX, float overscanScaleY)
    {
        Transform parent = backgroundImage != null && backgroundImage.transform.parent != null
            ? backgroundImage.transform.parent
            : transform;

        var mediaObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        var rectTransform = mediaObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);

        if (backgroundImage != null)
        {
            ApplyBackgroundLayout();

            var sourceRect = backgroundImage.rectTransform;
            rectTransform.anchorMin = sourceRect.anchorMin;
            rectTransform.anchorMax = sourceRect.anchorMax;
            rectTransform.anchoredPosition = sourceRect.anchoredPosition;
            rectTransform.sizeDelta = sourceRect.sizeDelta;
            rectTransform.pivot = sourceRect.pivot;
            rectTransform.localScale = GetOverscanScaleVector(overscanScaleX, overscanScaleY);
            rectTransform.SetSiblingIndex(backgroundImage.transform.GetSiblingIndex() + 1);
        }
        else
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        var rawImage = mediaObject.GetComponent<RawImage>();
        rawImage.raycastTarget = false;
        ApplyFillParentLayout(rawImage.rectTransform, overscanScaleX, overscanScaleY);
        if (objectName.IndexOf("Video", System.StringComparison.OrdinalIgnoreCase) >= 0)
            ApplyRawVideoBackgroundFit(rawImage);
        ApplyCameraPanOverflowIfNeeded(rawImage.rectTransform);
        ApplyCutsceneHorizontalFramingIfActive(rawImage.rectTransform);
        ReapplyCameraPanAfterBackgroundLayout(rawImage.rectTransform);
        rawImage.gameObject.SetActive(true);
        var canvasGroup = rawImage.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        return rawImage;
    }

    VideoBackgroundFitMode ResolveVideoBackgroundFitMode()
    {
        if (_cutsceneMediaLayoutActive)
            return VideoBackgroundFitMode.FitInsideParent;

        return coverVideoBackground16By9
            ? VideoBackgroundFitMode.Cover16By9
            : VideoBackgroundFitMode.Stretch;
    }

    void ApplyRawVideoBackgroundFit(RawImage rawImage)
    {
        if (rawImage == null)
            return;

        AspectRatioFitter aspectRatioFitter = rawImage.GetComponent<AspectRatioFitter>();
        VideoBackgroundFitMode fitMode = ResolveVideoBackgroundFitMode();

        if (fitMode == VideoBackgroundFitMode.Stretch)
        {
            RemoveRawVideoAspectRatioFitter(aspectRatioFitter);
            return;
        }

        if (aspectRatioFitter == null)
            aspectRatioFitter = rawImage.gameObject.AddComponent<AspectRatioFitter>();

        rawImage.rectTransform.localScale = Vector3.one;
        aspectRatioFitter.aspectRatio = fitMode == VideoBackgroundFitMode.Cover16By9
            ? VideoBackgroundPlayer.WidescreenAspectRatio
            : ResolveVideoAspectRatio(rawImage);
        aspectRatioFitter.aspectMode = fitMode == VideoBackgroundFitMode.FitInsideParent
            ? AspectRatioFitter.AspectMode.FitInParent
            : AspectRatioFitter.AspectMode.EnvelopeParent;
    }

    static float ResolveVideoAspectRatio(RawImage rawImage)
    {
        if (rawImage != null && rawImage.texture != null && rawImage.texture.height > 0)
            return Mathf.Max(0.01f, (float)rawImage.texture.width / rawImage.texture.height);

        return VideoBackgroundPlayer.WidescreenAspectRatio;
    }

    static void RemoveRawVideoAspectRatioFitter(AspectRatioFitter aspectRatioFitter)
    {
        if (aspectRatioFitter == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(aspectRatioFitter);
        else
            UnityEngine.Object.DestroyImmediate(aspectRatioFitter);
    }

    void ApplyCutsceneHorizontalFramingToCurrentLayers()
    {
        CaptureAndApplyCutsceneHorizontalOffsets(backgroundImage != null ? backgroundImage.rectTransform : null);
        CaptureAndApplyCutsceneHorizontalOffsets(videoRawImage != null ? videoRawImage.rectTransform : null);
        CaptureAndApplyCutsceneHorizontalOffsets(gifPlayer != null ? gifPlayer.transform as RectTransform : null);
        CaptureAndApplyCutsceneHorizontalOffsets(_backgroundTransitionImage != null ? _backgroundTransitionImage.rectTransform : null);
    }

    void ApplyCutsceneHorizontalFramingIfActive(RectTransform rectTransform)
    {
        if (_cutsceneHorizontalFramingActive)
            CaptureAndApplyCutsceneHorizontalOffsets(rectTransform);
    }

    bool IsActiveBackgroundTransitionRect(RectTransform rectTransform)
    {
        return _backgroundTransitionImage != null &&
               rectTransform == _backgroundTransitionImage.rectTransform &&
               _backgroundTransitionImage.gameObject.activeSelf;
    }

    void CaptureAndApplyCutsceneHorizontalOffsets(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        if (!_cutsceneSavedHorizontalOffsets.ContainsKey(rectTransform))
            _cutsceneSavedHorizontalOffsets.Add(rectTransform, new Vector2(rectTransform.offsetMin.x, rectTransform.offsetMax.x));

        ResolveCutsceneInspectorOffsets(out float left, out float right);
        SetInspectorHorizontalOffsets(rectTransform, left, right);
    }

    void ResolveCutsceneInspectorOffsets(out float left, out float right)
    {
        EnforceFixedCutsceneFraming();
        left = cutsceneInspectorLeft;
        right = cutsceneInspectorRight;
    }

    void EnforceFixedCutsceneFraming()
    {
        cutsceneInspectorLeft = CutsceneHorizontalInset;
        cutsceneInspectorRight = CutsceneHorizontalInset;
    }

    void ReapplyCameraPanAfterBackgroundLayout(
        RectTransform linkedRoot = null,
        bool linkedRootAlreadyIncludesCameraOffset = false)
    {
        CameraController cameraController = CameraController.Instance ?? FindObjectOfType<CameraController>(true);
        if (cameraController == null)
            return;

        if (linkedRoot != null)
        {
            cameraController.RegisterOrUpdateLinkedCameraRoot(
                linkedRoot,
                recaptureBasePosition: true,
                rootAlreadyIncludesCurrentOffset: linkedRootAlreadyIncludesCameraOffset);
        }

        cameraController.ReapplyCurrentOffset();
    }

    static void RestoreHorizontalOffsets(RectTransform rectTransform, Vector2 savedOffsets)
    {
        if (rectTransform == null)
            return;

        SetHorizontalOffsets(rectTransform, savedOffsets.x, savedOffsets.y);
    }

    static void SetHorizontalOffsets(RectTransform rectTransform, float offsetMinX, float offsetMaxX)
    {
        Vector2 offsetMin = rectTransform.offsetMin;
        Vector2 offsetMax = rectTransform.offsetMax;
        offsetMin.x = offsetMinX;
        offsetMax.x = offsetMaxX;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    static void SetInspectorHorizontalOffsets(RectTransform rectTransform, float left, float right)
    {
        SetHorizontalOffsets(rectTransform, left, -right);
    }

    Vector3 GetOverscanScaleVector(float overscanScaleX, float overscanScaleY)
    {
        return new Vector3(
            Mathf.Max(1f, overscanScaleX),
            Mathf.Max(1f, overscanScaleY),
            1f);
    }

    void ConfigureVideoPlayer()
    {
        if (videoPlayer == null)
            return;

        if (_registeredVideoPlayer != null && _registeredVideoPlayer != videoPlayer)
            UnregisterVideoPreparedHandler();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.aspectRatio = VideoAspectRatio.Stretch;

        if (_videoPreparedHandlerRegistered && _registeredVideoPlayer == videoPlayer)
            return;

        videoPlayer.prepareCompleted += OnVideoPrepared;
        _registeredVideoPlayer = videoPlayer;
        _videoPreparedHandlerRegistered = true;
    }

    void UnregisterVideoPreparedHandler()
    {
        if (_videoPreparedHandlerRegistered && _registeredVideoPlayer != null)
            _registeredVideoPlayer.prepareCompleted -= OnVideoPrepared;

        _registeredVideoPlayer = null;
        _videoPreparedHandlerRegistered = false;
    }

    void ReleaseActiveRenderTexture()
    {
        if (_activeRenderTexture == null)
            return;

        _activeRenderTexture.Release();
        if (Application.isPlaying)
            Destroy(_activeRenderTexture);
        else
            DestroyImmediate(_activeRenderTexture);

        _activeRenderTexture = null;
    }
}
