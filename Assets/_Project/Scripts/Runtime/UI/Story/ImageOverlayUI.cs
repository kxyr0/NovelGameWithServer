using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Video;

/// <summary>
/// UI-панель для показа картинки с подписью (нода ImageNode).
///
/// Подключение:
/// 1. Создай Canvas-панель "ImageOverlay" поверх всего (высокий Sort Order).
/// 2. Прикрепи этот скрипт.
/// 3. Назначь:
///    - panel          — корневой GameObject панели (с затемнением)
///    - imageDisplay   — Image для картинки (растянутая по центру)
///    - descriptionText — TMP_Text описания (можно скрыть если пусто)
///    - captionButton  — Button с TMP_Text ("Рассмотреть")
///    - captionButtonText — TMP_Text на кнопке
///    - closeButton    — Button "×" (опционально)
/// 4. Ссылку на этот компонент назначь в StoryManager.imageOverlay
/// </summary>
public class ImageOverlayUI : MonoBehaviour
{
    public static ImageOverlayUI Instance { get; private set; }

    [Header("Ссылки")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Image imageDisplay;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button captionButton;
    [SerializeField] private TMP_Text captionButtonText;
    [SerializeField] private Button closeButton;

    [Header("Анимированное медиа")]
    [SerializeField] private RawImage mediaRawImage;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AnimatedGifPlayer gifPlayer;
    [SerializeField, Min(0.5f)] private float mediaReadyTimeout = 5f;

    [Header("Масштабирование")]
    [Tooltip("Минимальный масштаб картинки при жесте приближения на телефоне.")]
    [SerializeField] private float minZoom = 1f;
    [Tooltip("Максимальный масштаб картинки при приближении.")]
    [SerializeField] private float maxZoom = 3f;

    [Header("Keyboard")]
    [SerializeField] private bool closeWithKeyboard = true;
    [SerializeField] private KeyCode dedicatedCloseKey = KeyCode.C;
    [SerializeField] private bool closeWithDialogueAdvanceKeys = true;
    [SerializeField] private bool closeWithEscape = true;

    public bool IsVisible => panel != null && panel.activeInHierarchy;

    private System.Action _onClose;
    private bool _zoomable;
    private bool _isClosing;
    private RectTransform _imageRect;
    private CanvasGroup _panelCanvasGroup;
    private RenderTexture _activeRenderTexture;
    private bool _videoPreparedHandlerRegistered;
    private bool _videoFrameReadyHandlerRegistered;
    private bool _videoErrorHandlerRegistered;
    private AnimatedGifPlayer _registeredGifPlayer;
    private bool _gifFirstFrameHandlerRegistered;
    private bool _awaitingMediaReady;
    private bool _panelRevealStarted;
    private Coroutine _mediaReadyTimeoutRoutine;
    private int _shownFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    public static ImageOverlayUI FindOrCreateRuntimeOverlay()
    {
        ImageOverlayUI overlay = Instance != null ? Instance : FindObjectOfType<ImageOverlayUI>(true);
        if (overlay != null)
            return overlay;

        return CreateRuntimeOverlay();
    }

    private static ImageOverlayUI CreateRuntimeOverlay()
    {
        var root = new GameObject(
            "RuntimeImageOverlayUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        root.SetActive(false);

        var rootRect = root.GetComponent<RectTransform>();
        StretchToParent(rootRect);

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        var panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.SetParent(rootRect, false);
        StretchToParent(panelRect);

        var panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.94f);
        panelImage.raycastTarget = true;

        var imageObject = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.SetParent(panelRect, false);
        imageRect.anchorMin = new Vector2(0.04f, 0.08f);
        imageRect.anchorMax = new Vector2(0.96f, 0.94f);
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        var image = imageObject.GetComponent<Image>();
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        var descriptionObject = new GameObject("Description", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var descriptionRect = descriptionObject.GetComponent<RectTransform>();
        descriptionRect.SetParent(panelRect, false);
        descriptionRect.anchorMin = new Vector2(0.08f, 0.08f);
        descriptionRect.anchorMax = new Vector2(0.92f, 0.2f);
        descriptionRect.offsetMin = Vector2.zero;
        descriptionRect.offsetMax = Vector2.zero;

        var description = descriptionObject.GetComponent<TextMeshProUGUI>();
        description.alignment = TextAlignmentOptions.Center;
        description.color = Color.white;
        description.fontSize = 28f;
        description.enableWordWrapping = true;
        description.raycastTarget = false;

        var captionButton = CreateOverlayButton(panelRect, "CaptionButton", new Vector2(0.36f, 0.015f), new Vector2(0.64f, 0.07f), "Close", 26f);
        var closeButton = CreateOverlayButton(panelRect, "CloseButton", new Vector2(0.91f, 0.94f), new Vector2(0.985f, 0.985f), "X", 24f);

        var overlay = root.AddComponent<ImageOverlayUI>();
        overlay.panel = panelObject;
        overlay.imageDisplay = image;
        overlay.descriptionText = description;
        overlay.captionButton = captionButton.Button;
        overlay.captionButtonText = captionButton.Text;
        overlay.closeButton = closeButton.Button;

        root.SetActive(true);
        return overlay;
    }

    private static (Button Button, TMP_Text Text) CreateOverlayButton(
        RectTransform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        string label,
        float fontSize)
    {
        var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.16f);

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        StretchToParent(textRect);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontSize = fontSize;
        text.enableWordWrapping = false;
        text.raycastTarget = false;

        return (button, text);
    }

    private static void StretchToParent(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        EnsurePresentationReferences();

        ResetPresentationState(clearCallback: true, deactivatePanel: true);
    }

    private void OnValidate()
    {
        minZoom = Mathf.Max(0.1f, minZoom);
        maxZoom = Mathf.Max(minZoom, maxZoom);
        mediaReadyTimeout = Mathf.Max(0.5f, mediaReadyTimeout);
    }

    private void OnDestroy()
    {
        if (captionButton != null)
            captionButton.onClick.RemoveListener(Close);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        ResetPresentationState(clearCallback: true, deactivatePanel: true);

        if (videoPlayer != null && _videoPreparedHandlerRegistered)
            videoPlayer.prepareCompleted -= OnVideoPrepared;
        if (videoPlayer != null && _videoFrameReadyHandlerRegistered)
            videoPlayer.frameReady -= OnVideoFrameReady;
        if (videoPlayer != null && _videoErrorHandlerRegistered)
            videoPlayer.errorReceived -= OnVideoError;
        UnregisterGifFirstFrameHandler();

        if (Instance == this)
            Instance = null;
    }

    private void OnDisable()
    {
        ResetPresentationState(clearCallback: true, deactivatePanel: true);
    }

    private void Start()
    {
        if (captionButton != null)
        {
            captionButton.onClick.RemoveListener(Close);
            captionButton.onClick.AddListener(Close);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    /// <summary>
    /// Показать картинку.
    /// </summary>
    public void Show(ImageNode node, System.Action onClose)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        EnsurePresentationReferences();
        ResetPresentationState(clearCallback: false, deactivatePanel: true);

        _onClose = onClose;
        _zoomable = node != null && node.zoomable;
        _isClosing = false;
        _shownFrame = Time.frameCount;

        if (panel == null || node == null)
        {
            Debug.LogWarning("[ImageOverlayUI] Cannot show image overlay: panel or node is missing.", this);
            CompleteClose();
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[ImageOverlayUI] Cannot show image overlay because its GameObject hierarchy is inactive.", this);
            CompleteClose();
            return;
        }

        panel.SetActive(true);
        if (!panel.activeInHierarchy)
        {
            Debug.LogWarning("[ImageOverlayUI] Cannot show image overlay because its panel hierarchy is inactive.", this);
            CompleteClose();
            return;
        }

        if (_panelCanvasGroup != null)
            _panelCanvasGroup.alpha = 0f;

        ShowMedia(node);

        if (_awaitingMediaReady)
            StartMediaReadyTimeout();

        if (descriptionText != null)
        {
            bool hasDesc = !string.IsNullOrEmpty(node.description);
            descriptionText.gameObject.SetActive(hasDesc);
            descriptionText.text = hasDesc ? PlayerAppearance.ReplacePlaceholders(node.description) : "";
        }

        if (captionButtonText != null)
            captionButtonText.text = string.IsNullOrEmpty(node.caption)
                ? "Закрыть"
                : PlayerAppearance.ReplacePlaceholders(node.caption);

        if (_imageRect != null)
            _imageRect.localScale = Vector3.one;

        // Анимация появления
        if (!_awaitingMediaReady)
            RevealPanel();
    }

    public void HideImmediate()
    {
        ResetPresentationState(clearCallback: true, deactivatePanel: true);
    }

    private void Close()
    {
        if (_isClosing)
            return;

        _isClosing = true;
        _zoomable = false;

        if (panel == null)
        {
            CompleteClose();
            return;
        }

        if (_panelCanvasGroup != null)
        {
            _panelCanvasGroup.DOKill();
            _panelCanvasGroup.DOFade(0f, 0.2f).OnComplete(() =>
            {
                if (panel != null)
                    panel.SetActive(false);
                CompleteClose();
            });
        }
        else
        {
            panel.SetActive(false);
            CompleteClose();
        }
    }

    private void CompleteClose()
    {
        var callback = _onClose;
        ResetPresentationState(clearCallback: true, deactivatePanel: true);
        callback?.Invoke();
    }

    private void ResetPresentationState(bool clearCallback, bool deactivatePanel)
    {
        EnsurePresentationReferences();
        _panelCanvasGroup ??= panel != null ? panel.GetComponent<CanvasGroup>() : null;
        _panelCanvasGroup?.DOKill();

        StopMediaReadyTimeout();
        StopMedia();

        _zoomable = false;
        _isClosing = false;
        _awaitingMediaReady = false;
        _panelRevealStarted = false;

        if (_imageRect != null)
            _imageRect.localScale = Vector3.one;

        if (_panelCanvasGroup != null)
            _panelCanvasGroup.alpha = deactivatePanel ? 0f : 1f;

        if (deactivatePanel && panel != null)
            panel.SetActive(false);

        if (clearCallback)
            _onClose = null;
    }

    private void EnsurePresentationReferences()
    {
        if (_imageRect == null && imageDisplay != null)
            _imageRect = imageDisplay.rectTransform;

        if (_panelCanvasGroup == null && panel != null)
            _panelCanvasGroup = panel.GetComponent<CanvasGroup>();

        ConfigureVideoPlayer();
    }

    private void ShowMedia(ImageNode node)
    {
        StopMedia();

        if (node == null)
            return;

        if (node.video != null)
        {
            ShowVideo(node.video);
            return;
        }

        if (node.gif != null)
        {
            ShowGif(node.gif);
            return;
        }

        ShowSprite(node.image);
    }

    private void ShowSprite(Sprite sprite)
    {
        if (imageDisplay == null)
            return;

        imageDisplay.sprite = sprite;
        imageDisplay.preserveAspect = true;
        imageDisplay.enabled = sprite != null;
    }

    private void ShowVideo(VideoClip clip)
    {
        if (clip == null)
            return;

        if (imageDisplay != null)
            imageDisplay.enabled = false;

        _awaitingMediaReady = true;

        if (!EnsureVideoPlayer())
        {
            Debug.LogWarning("[ImageOverlayUI] Cannot show video: VideoPlayer or RawImage is missing.", this);
            _awaitingMediaReady = false;
            return;
        }

        ReleaseActiveRenderTexture();

        int width = Mathf.Max(16, (int)clip.width);
        int height = Mathf.Max(16, (int)clip.height);
        _activeRenderTexture = new RenderTexture(width, height, 0);
        _activeRenderTexture.Create();

        try
        {
            ConfigureVideoPlayer();
            videoPlayer.targetTexture = _activeRenderTexture;
            mediaRawImage.texture = _activeRenderTexture;
            mediaRawImage.color = Color.clear;
            videoPlayer.clip = clip;
            mediaRawImage.gameObject.SetActive(true);
            videoPlayer.gameObject.SetActive(true);
            videoPlayer.Prepare();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[ImageOverlayUI] Failed to prepare video '{clip.name}': {exception.Message}", this);
            StopMedia();
            _awaitingMediaReady = false;
        }
    }

    private void ShowGif(TextAsset gifAsset)
    {
        if (gifAsset == null)
            return;

        if (imageDisplay != null)
            imageDisplay.enabled = false;

        _awaitingMediaReady = true;

        if (!EnsureGifPlayer())
        {
            Debug.LogWarning("[ImageOverlayUI] Cannot show GIF: AnimatedGifPlayer is missing.", this);
            _awaitingMediaReady = false;
            return;
        }

        StopVideo();
        RegisterGifFirstFrameHandler(gifPlayer);
        mediaRawImage.color = Color.clear;
        mediaRawImage.gameObject.SetActive(true);
        gifPlayer.gameObject.SetActive(true);
        gifPlayer.Play(gifAsset);

        if (gifPlayer.HasVisibleFrame)
            OnGifFirstFrameReady();
    }

    private void OnVideoPrepared(VideoPlayer player)
    {
        if (player == null || !isActiveAndEnabled)
            return;

        try
        {
            player.Play();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[ImageOverlayUI] Failed to play video: {exception.Message}", this);
        }
    }

    private void OnVideoFrameReady(VideoPlayer player, long frameIndex)
    {
        if (player == null || player != videoPlayer || !_awaitingMediaReady || !isActiveAndEnabled)
            return;

        if (mediaRawImage != null)
            mediaRawImage.color = Color.white;

        RevealPanel();
    }

    private void OnVideoError(VideoPlayer player, string message)
    {
        if (player == null || player != videoPlayer || !_awaitingMediaReady)
            return;

        Debug.LogWarning($"[ImageOverlayUI] Video playback failed: {message}", this);
        StopMedia();
        RevealPanel();
    }

    private void RegisterGifFirstFrameHandler(AnimatedGifPlayer player)
    {
        if (_registeredGifPlayer == player && _gifFirstFrameHandlerRegistered)
            return;

        UnregisterGifFirstFrameHandler();
        _registeredGifPlayer = player;
        if (_registeredGifPlayer == null)
            return;

        _registeredGifPlayer.FirstFrameReady += OnGifFirstFrameReady;
        _gifFirstFrameHandlerRegistered = true;
    }

    private void UnregisterGifFirstFrameHandler()
    {
        if (_gifFirstFrameHandlerRegistered && _registeredGifPlayer != null)
            _registeredGifPlayer.FirstFrameReady -= OnGifFirstFrameReady;

        _registeredGifPlayer = null;
        _gifFirstFrameHandlerRegistered = false;
    }

    private void OnGifFirstFrameReady()
    {
        if (!_awaitingMediaReady || gifPlayer == null || !gifPlayer.HasVisibleFrame)
            return;

        if (mediaRawImage != null)
            mediaRawImage.color = Color.white;

        RevealPanel();
    }

    private void RevealPanel()
    {
        _awaitingMediaReady = false;
        StopMediaReadyTimeout();
        if (_panelRevealStarted || panel == null || !panel.activeInHierarchy)
            return;

        _panelRevealStarted = true;
        if (_panelCanvasGroup == null)
            return;

        _panelCanvasGroup.DOKill();
        _panelCanvasGroup.alpha = 0f;
        _panelCanvasGroup.DOFade(1f, 0.3f).SetUpdate(true);
    }

    private void StartMediaReadyTimeout()
    {
        StopMediaReadyTimeout();
        if (isActiveAndEnabled)
            _mediaReadyTimeoutRoutine = StartCoroutine(MediaReadyTimeoutRoutine());
    }

    private void StopMediaReadyTimeout()
    {
        if (_mediaReadyTimeoutRoutine == null)
            return;

        StopCoroutine(_mediaReadyTimeoutRoutine);
        _mediaReadyTimeoutRoutine = null;
    }

    private IEnumerator MediaReadyTimeoutRoutine()
    {
        float elapsed = 0f;
        float timeout = Mathf.Max(0.5f, mediaReadyTimeout);
        while (_awaitingMediaReady && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _mediaReadyTimeoutRoutine = null;
        if (!_awaitingMediaReady)
            yield break;

        Debug.LogWarning($"[ImageOverlayUI] Media did not produce a frame within {timeout:0.##} seconds.", this);
        StopMedia();
        RevealPanel();
    }

    private bool EnsureVideoPlayer()
    {
        if (mediaRawImage == null)
            mediaRawImage = CreateMediaRawImage("Image Overlay Media");

        if (mediaRawImage == null)
            return false;

        if (videoPlayer == null)
            videoPlayer = mediaRawImage.GetComponent<VideoPlayer>() ?? mediaRawImage.gameObject.AddComponent<VideoPlayer>();

        ConfigureVideoPlayer();
        return videoPlayer != null;
    }

    private bool EnsureGifPlayer()
    {
        if (mediaRawImage == null)
            mediaRawImage = CreateMediaRawImage("Image Overlay Media");

        if (mediaRawImage == null)
            return false;

        if (gifPlayer == null)
            gifPlayer = mediaRawImage.GetComponent<AnimatedGifPlayer>() ?? mediaRawImage.gameObject.AddComponent<AnimatedGifPlayer>();

        return gifPlayer != null;
    }

    private RawImage CreateMediaRawImage(string objectName)
    {
        if (imageDisplay == null)
            return null;

        var mediaObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        var rectTransform = mediaObject.GetComponent<RectTransform>();
        rectTransform.SetParent(imageDisplay.transform, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        var rawImage = mediaObject.GetComponent<RawImage>();
        rawImage.raycastTarget = false;
        rawImage.gameObject.SetActive(false);
        return rawImage;
    }

    private void ConfigureVideoPlayer()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.sendFrameReadyEvents = true;

        if (!_videoPreparedHandlerRegistered)
        {
            videoPlayer.prepareCompleted += OnVideoPrepared;
            _videoPreparedHandlerRegistered = true;
        }

        if (!_videoFrameReadyHandlerRegistered)
        {
            videoPlayer.frameReady += OnVideoFrameReady;
            _videoFrameReadyHandlerRegistered = true;
        }

        if (!_videoErrorHandlerRegistered)
        {
            videoPlayer.errorReceived += OnVideoError;
            _videoErrorHandlerRegistered = true;
        }
    }

    private void StopMedia()
    {
        StopVideo();

        if (gifPlayer != null)
        {
            gifPlayer.Stop();
            gifPlayer.gameObject.SetActive(false);
        }

        if (mediaRawImage != null)
        {
            mediaRawImage.texture = null;
            mediaRawImage.color = Color.clear;
            mediaRawImage.gameObject.SetActive(false);
        }

        if (imageDisplay != null)
        {
            imageDisplay.sprite = null;
            imageDisplay.enabled = false;
        }
    }

    private void StopVideo()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Stop();

        if (videoPlayer != null)
        {
            videoPlayer.clip = null;
            videoPlayer.targetTexture = null;
        }

        ReleaseActiveRenderTexture();
    }

    private void ReleaseActiveRenderTexture()
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

    // ── Pinch-to-zoom (мобильный) ───────────────────────────

    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        if (ShouldCloseFromKeyboard())
        {
            Close();
            return;
        }

        if (!_zoomable || _imageRect == null) return;

        if (Input.touchCount == 2)
        {
            var t0 = Input.GetTouch(0);
            var t1 = Input.GetTouch(1);

            var prevT0 = t0.position - t0.deltaPosition;
            var prevT1 = t1.position - t1.deltaPosition;

            float prevDist = (prevT0 - prevT1).magnitude;
            float currDist = (t0.position - t1.position).magnitude;

            float delta = currDist - prevDist;
            float scaleFactor = 1f + delta * 0.005f;

            Vector3 newScale = _imageRect.localScale * scaleFactor;
            newScale.x = Mathf.Clamp(newScale.x, minZoom, maxZoom);
            newScale.y = Mathf.Clamp(newScale.y, minZoom, maxZoom);
            newScale.z = 1f;

            _imageRect.localScale = newScale;
        }
    }

    private bool ShouldCloseFromKeyboard()
    {
        if (!closeWithKeyboard || Time.frameCount == _shownFrame)
            return false;

        if (dedicatedCloseKey != KeyCode.None && Input.GetKeyDown(dedicatedCloseKey))
            return true;

        if (closeWithEscape && Input.GetKeyDown(KeyCode.Escape))
            return true;

        return closeWithDialogueAdvanceKeys &&
               (Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter));
    }
}
