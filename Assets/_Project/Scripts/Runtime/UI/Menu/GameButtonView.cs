using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Video;

public class GameButtonView : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField]
    [FormerlySerializedAs("gameIcon")]
    [InspectorName("Image обложки")]
    [Tooltip("Image, куда карточка подставляет обложку из GameData.")]
    private Image gameIcon;

    [SerializeField]
    [FormerlySerializedAs("gameNameText")]
    [InspectorName("Текст названия")]
    [Tooltip("TMP-текст для названия истории из GameData.")]
    private TMP_Text gameNameText;

    [SerializeField]
    [InspectorName("Текст жанра")]
    [Tooltip("TMP-текст для жанра истории из GameData.")]
    private TMP_Text genreText;

    [SerializeField]
    [FormerlySerializedAs("lockOverlay")]
    [InspectorName("Lock overlay")]
    [Tooltip("Объект замка/блокировки. Включается, если первая глава истории закрыта.")]
    private GameObject lockOverlay;

    [SerializeField]
    [FormerlySerializedAs("button")]
    [InspectorName("Кнопка запуска")]
    [Tooltip("Основная Button карточки, которая запускает историю.")]
    private Button button;

    [Header("Анимированная обложка")]
    [SerializeField]
    [FormerlySerializedAs("coverVideoPlayer")]
    [InspectorName("Video Player обложки")]
    [Tooltip("Опциональный VideoPlayer для видео-обложки.")]
    private VideoPlayer coverVideoPlayer;

    [SerializeField]
    [FormerlySerializedAs("coverVideoRawImage")]
    [InspectorName("Raw Image видео")]
    [Tooltip("RawImage, в который будет выводиться видео-обложка.")]
    private RawImage coverVideoRawImage;

    [SerializeField]
    [FormerlySerializedAs("coverGifPlayer")]
    [InspectorName("GIF Player обложки")]
    [Tooltip("Опциональный AnimatedGifPlayer для GIF-обложки.")]
    private AnimatedGifPlayer coverGifPlayer;

    [Header("Selected-визуал")]
    [SerializeField]
    [InspectorName("Selected sprite fades")]
    [Tooltip("Компоненты Sprite Fade, которым карточка сообщает selected/unselected. Например Vitrage_Default -> Vitrage_Active.")]
    private UISpriteStateFade[] selectedStateFades = Array.Empty<UISpriteStateFade>();

    [Header("Избранное")]
    [SerializeField]
    [InspectorName("Кнопки избранного")]
    [Tooltip("Кнопки избранного внутри карточки. Если список пуст, карточка возьмет StoryFavoriteToggleButton из детей.")]
    private StoryFavoriteToggleButton[] favoriteToggleButtons = Array.Empty<StoryFavoriteToggleButton>();

    [SerializeField]
    [InspectorName("Искать кнопки в детях")]
    [Tooltip("Искать StoryFavoriteToggleButton в детях карточки, если список выше пуст.")]
    private bool findFavoriteToggleButtonsInChildren = true;

    GameData _data;
    Action _onClick;
    RenderTexture _coverRenderTexture;
    bool _videoPreparedHandlerRegistered;
    bool _coverVideoRawImageAutoCreated;
    bool _isSelected;
    VideoClip _activeVideoClip;
    TextAsset _activeGifAsset;
    RectTransform _coverSnapshotRect;
    Vector2 _coverBaseAnchoredPosition;
    Vector2 _coverBaseSize;
    Vector3 _coverBaseScale;
    RectTransform _videoSnapshotRect;
    Vector2 _videoBaseAnchoredPosition;
    Vector2 _videoBaseSize;
    Vector3 _videoBaseScale;
    float _videoBaseRotationZ;

    public GameData Data => _data;

    void Awake()
    {
        AutoWireReferences();
        BindClickHandler();
    }

    void OnValidate()
    {
        AutoWireReferences();
    }

    void OnEnable()
    {
        BindClickHandler();
    }

    void Start()
    {
        BindClickHandler();
    }

    void OnDisable()
    {
        StopAnimatedCover();
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);

        StopAnimatedCover();

        if (coverVideoPlayer != null && _videoPreparedHandlerRegistered)
            coverVideoPlayer.prepareCompleted -= OnCoverVideoPrepared;
    }

    public void Setup(GameData data, Action onClick)
    {
        AutoWireReferences();
        BindClickHandler();

        _data = data;
        _onClick = onClick;

        ConfigureFavoriteToggleButtons(data);
        ApplyCover(data);
        ApplyMenuCardOverrides(data);
        ApplyTexts(data);

        bool locked = IsFirstChapterLocked(data);
        if (lockOverlay != null)
            lockOverlay.SetActive(locked);
        if (button != null)
            button.interactable = !locked;
    }

    public void SetSelected(bool selected)
    {
        if (_isSelected == selected)
            return;

        _isSelected = selected;

        if (selectedStateFades == null)
            return;

        for (int i = 0; i < selectedStateFades.Length; i++)
        {
            UISpriteStateFade stateFade = selectedStateFades[i];
            if (stateFade != null)
                stateFade.SetActiveState(selected);
        }
    }

    void AutoWireReferences()
    {
        if (gameIcon == null)
            gameIcon = GetComponent<Image>();

        if (button == null)
            button = GetComponent<Button>();

        if (coverVideoPlayer == null)
            coverVideoPlayer = FindChildVideoPlayer();

        if (coverVideoRawImage == null && coverVideoPlayer != null)
            coverVideoRawImage = coverVideoPlayer.GetComponent<RawImage>();

        if (coverVideoRawImage == null)
            coverVideoRawImage = FindChildVideoRawImage();

        if (coverVideoPlayer == null && coverVideoRawImage != null)
            coverVideoPlayer = coverVideoRawImage.GetComponent<VideoPlayer>();
    }

    VideoPlayer FindChildVideoPlayer()
    {
        VideoPlayer[] players = GetComponentsInChildren<VideoPlayer>(true);
        for (int i = 0; i < players.Length; i++)
        {
            VideoPlayer player = players[i];
            if (player != null && player.gameObject != gameObject)
                return player;
        }

        return GetComponent<VideoPlayer>();
    }

    RawImage FindChildVideoRawImage()
    {
        RawImage[] rawImages = GetComponentsInChildren<RawImage>(true);
        RawImage fallback = null;

        for (int i = 0; i < rawImages.Length; i++)
        {
            RawImage rawImage = rawImages[i];
            if (rawImage == null || rawImage.gameObject == gameObject)
                continue;

            bool hasVideoPlayer = rawImage.GetComponent<VideoPlayer>() != null;
            bool hasGifPlayer = rawImage.GetComponent<AnimatedGifPlayer>() != null;
            string objectName = rawImage.gameObject.name ?? "";
            bool namedAsVideo = objectName.IndexOf("video", StringComparison.OrdinalIgnoreCase) >= 0;

            if (hasVideoPlayer || namedAsVideo)
            {
                _coverVideoRawImageAutoCreated = false;
                return rawImage;
            }

            if (!hasGifPlayer && fallback == null)
                fallback = rawImage;
        }

        if (fallback != null)
            _coverVideoRawImageAutoCreated = false;

        return fallback;
    }
    void ApplyMenuCardOverrides(GameData data)
    {
        RectTransform coverRect = gameIcon != null ? gameIcon.rectTransform : null;
        if (coverRect == null)
            return;

        TakeCoverRectSnapshot(coverRect);

        GameMenuCardOverrideSettings overrides = data != null ? data.MenuCardOverrides : null;
        Vector2 coverSize = _coverBaseSize;
        Vector2 coverPosition = _coverBaseAnchoredPosition;
        Vector3 coverScale = _coverBaseScale;

        if (overrides != null)
        {
            if (overrides.OverrideCoverSize)
                coverSize = overrides.CoverSize;

            if (overrides.OverrideCoverPositionOffset)
                coverPosition += overrides.CoverPositionOffset;

            if (overrides.OverrideCoverScale)
                coverScale = overrides.CoverScale;
        }

        SetRectSize(coverRect, coverSize);
        coverRect.anchoredPosition = coverPosition;
        coverRect.localScale = coverScale;

        ApplyVideoCoverOverrides(overrides);
    }

    void ApplyVideoCoverOverrides(GameMenuCardOverrideSettings overrides)
    {
        if (coverVideoRawImage == null)
            return;

        RectTransform videoRect = coverVideoRawImage.rectTransform;
        if (videoRect == null)
            return;

        TakeVideoRectSnapshot(videoRect);

        Vector2 videoSize = _videoBaseSize;
        Vector2 videoPosition = _videoBaseAnchoredPosition;
        float videoRotationZ = _videoBaseRotationZ;

        if (overrides != null)
        {
            if (overrides.OverrideVideoSize)
                videoSize = overrides.VideoSize;

            if (overrides.OverrideVideoPosition)
                videoPosition = overrides.VideoPosition;

            if (overrides.OverrideVideoRotation)
                videoRotationZ = overrides.VideoRotationZ;
        }

        SetRectSize(videoRect, videoSize);
        videoRect.anchoredPosition = videoPosition;
        videoRect.localScale = _videoBaseScale;
        SetLocalRotationZ(videoRect, videoRotationZ);
        ApplyAnimatedCoverLayering(overrides);
    }

    void TakeCoverRectSnapshot(RectTransform coverRect)
    {
        if (coverRect == null)
            return;

        if (_coverSnapshotRect == coverRect)
            return;

        _coverSnapshotRect = coverRect;
        _coverBaseAnchoredPosition = coverRect.anchoredPosition;
        _coverBaseSize = coverRect.rect.size;
        _coverBaseScale = coverRect.localScale;
    }

    void TakeVideoRectSnapshot(RectTransform videoRect)
    {
        if (videoRect == null)
            return;

        if (_videoSnapshotRect == videoRect)
            return;

        _videoSnapshotRect = videoRect;
        _videoBaseAnchoredPosition = videoRect.anchoredPosition;
        _videoBaseSize = videoRect.rect.size;
        _videoBaseScale = videoRect.localScale;
        _videoBaseRotationZ = NormalizeAngle(videoRect.localEulerAngles.z);
    }

    static void SetRectSize(RectTransform rectTransform, Vector2 size)
    {
        if (rectTransform == null)
            return;

        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, size.x));
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, size.y));
    }

    static void SetLocalRotationZ(RectTransform rectTransform, float rotationZ)
    {
        if (rectTransform == null)
            return;

        Vector3 euler = rectTransform.localEulerAngles;
        euler.z = rotationZ;
        rectTransform.localEulerAngles = euler;
    }

    static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
            angle -= 360f;
        if (angle < -180f)
            angle += 360f;
        return angle;
    }

    public void ApplyObjectNames(string storyName)
    {
        AutoWireReferences();

        string safeName = string.IsNullOrWhiteSpace(storyName)
            ? "Без названия"
            : storyName.Trim();

        gameObject.name = $"StoryBackground - {safeName}";

        if (gameIcon != null && gameIcon.gameObject != gameObject)
            gameIcon.gameObject.name = $"Cover - {safeName}";

        if (gameNameText != null)
            gameNameText.gameObject.name = $"Title - {safeName}";

        if (genreText != null)
            genreText.gameObject.name = $"Genre - {safeName}";
    }

    void ConfigureFavoriteToggleButtons(GameData data)
    {
        if ((favoriteToggleButtons == null || favoriteToggleButtons.Length == 0) && findFavoriteToggleButtonsInChildren)
            favoriteToggleButtons = GetComponentsInChildren<StoryFavoriteToggleButton>(true);

        if (favoriteToggleButtons == null)
            return;

        for (int i = 0; i < favoriteToggleButtons.Length; i++)
        {
            StoryFavoriteToggleButton favoriteToggleButton = favoriteToggleButtons[i];
            if (favoriteToggleButton != null)
                favoriteToggleButton.Configure(data);
        }
    }

    void BindClickHandler()
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    void HandleClick()
    {
        _onClick?.Invoke();
    }

    void ApplyTexts(GameData data)
    {
        if (gameNameText != null)
            gameNameText.text = ResolveGameName(data);

        if (genreText != null)
            genreText.text = data != null ? data.GenreText : "";
    }

    void ApplyCover(GameData data)
    {
        if (gameIcon != null)
        {
            Sprite cover = data != null ? data.GameIcon : null;
            bool hasAnimatedCover = data != null && (data.GameIconVideo != null || data.GameIconGif != null);
            if (cover != null)
                RuntimeTextureFallback.EnsureImageVisible(gameIcon, cover);
            else
                RuntimeTextureFallback.ApplyImagePlaceholder(gameIcon);

            if (data != null && cover == null && !hasAnimatedCover)
                Debug.LogWarning($"[GameButtonView] GameData '{data.name}' has no cover assigned. Showing button without image cover.", data);
        }

        if (data == null)
        {
            StopAnimatedCover();
            return;
        }

        if (data.GameIconVideo != null)
        {
            if (IsShowingVideoCover(data.GameIconVideo))
                RefreshVideoCover(data.GameIconVideo);
            else
            {
                StopAnimatedCover();
                ShowVideoCover(data.GameIconVideo);
            }
        }
        else if (data.GameIconGif != null)
        {
            if (IsShowingGifCover(data.GameIconGif))
                RefreshGifCover();
            else
            {
                StopAnimatedCover();
                ShowGifCover(data.GameIconGif);
            }
        }
        else
        {
            StopAnimatedCover();
        }
    }

    void ShowVideoCover(VideoClip clip)
    {
        if (clip == null || !EnsureVideoCover())
            return;

        if (coverGifPlayer != null)
            coverGifPlayer.gameObject.SetActive(false);

        ReleaseCoverRenderTexture();
        _activeVideoClip = clip;
        _activeGifAsset = null;

        int width = Mathf.Max(16, (int)clip.width);
        int height = Mathf.Max(16, (int)clip.height);
        _coverRenderTexture = new RenderTexture(width, height, 0)
        {
            name = $"{nameof(GameButtonView)} Cover RenderTexture"
        };
        _coverRenderTexture.Create();

        try
        {
            ConfigureVideoPlayer();
            coverVideoPlayer.source = VideoSource.VideoClip;
            coverVideoPlayer.clip = clip;
            coverVideoPlayer.targetTexture = _coverRenderTexture;
            coverVideoRawImage.texture = _coverRenderTexture;
            coverVideoRawImage.enabled = true;
            coverVideoRawImage.color = Color.clear;

            ApplyAnimatedCoverLayering(_data != null ? _data.MenuCardOverrides : null);

            coverVideoRawImage.gameObject.SetActive(true);
            coverVideoPlayer.gameObject.SetActive(true);
            coverVideoPlayer.Prepare();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[GameButtonView] Failed to prepare cover video '{clip.name}': {exception.Message}", this);
            StopAnimatedCover();
        }
    }

    void ShowGifCover(TextAsset gifAsset)
    {
        if (gifAsset == null || !EnsureGifCover())
            return;

        StopVideoCover();
        _activeGifAsset = gifAsset;
        coverGifPlayer.transform.SetAsLastSibling();
        coverGifPlayer.gameObject.SetActive(true);
        coverGifPlayer.Play(gifAsset);
    }

    bool IsShowingVideoCover(VideoClip clip)
    {
        return clip != null &&
               _activeVideoClip == clip &&
               coverVideoPlayer != null &&
               coverVideoPlayer.clip == clip &&
               coverVideoRawImage != null &&
               coverVideoRawImage.texture != null;
    }

    bool IsShowingGifCover(TextAsset gifAsset)
    {
        return gifAsset != null &&
               _activeGifAsset == gifAsset &&
               coverGifPlayer != null &&
               coverGifPlayer.gameObject.activeSelf;
    }

    void RefreshVideoCover(VideoClip clip)
    {
        if (clip == null || coverVideoPlayer == null || coverVideoRawImage == null)
            return;

        if (coverGifPlayer != null)
            coverGifPlayer.gameObject.SetActive(false);

        ConfigureVideoPlayer();
        coverVideoRawImage.enabled = true;
        coverVideoRawImage.gameObject.SetActive(true);
        coverVideoPlayer.gameObject.SetActive(true);

        if (coverVideoPlayer.isPrepared || coverVideoPlayer.isPlaying)
            coverVideoRawImage.color = Color.white;

        ApplyAnimatedCoverLayering(_data != null ? _data.MenuCardOverrides : null);

        if (coverVideoPlayer.isPrepared)
        {
            if (!coverVideoPlayer.isPlaying)
                coverVideoPlayer.Play();
        }
        else if (!coverVideoPlayer.isPlaying)
        {
            coverVideoPlayer.Prepare();
        }
    }

    void RefreshGifCover()
    {
        if (coverGifPlayer == null)
            return;

        StopVideoCover();
        coverGifPlayer.transform.SetAsLastSibling();
        coverGifPlayer.gameObject.SetActive(true);
    }

    void ApplyAnimatedCoverLayering(GameMenuCardOverrideSettings overrides)
    {
        if (coverVideoRawImage == null)
            return;

        Transform mediaTransform = coverVideoRawImage.transform;
        if (mediaTransform == null)
            return;

        bool keepBelowFrame = overrides == null || overrides.KeepVideoBelowCardFrame;
        if (keepBelowFrame && TryPlaceBeforeCardFrame(mediaTransform))
            return;

        if (_coverVideoRawImageAutoCreated || mediaTransform.parent == (gameIcon != null ? gameIcon.transform : null))
            mediaTransform.SetAsLastSibling();
    }

    static bool TryPlaceBeforeCardFrame(Transform mediaTransform)
    {
        Transform parent = mediaTransform != null ? mediaTransform.parent : null;
        if (parent == null)
            return false;

        Transform frame = FindCardFrameSibling(parent);
        if (frame == null || frame == mediaTransform)
            return false;

        int mediaIndex = mediaTransform.GetSiblingIndex();
        int frameIndex = frame.GetSiblingIndex();
        if (mediaIndex < frameIndex)
            return true;

        mediaTransform.SetSiblingIndex(frameIndex);
        return true;
    }

    static Transform FindCardFrameSibling(Transform parent)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && IsCardFrameName(child.name))
                return child;
        }

        return null;
    }

    static bool IsCardFrameName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        string normalized = objectName.Replace(" ", "").Replace("-", "_");
        return normalized.Equals("Card_Frame", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("CardFrame", StringComparison.OrdinalIgnoreCase) ||
               normalized.IndexOf("card_frame", StringComparison.OrdinalIgnoreCase) >= 0;
    }
    void OnCoverVideoPrepared(VideoPlayer player)
    {
        if (player == null || !isActiveAndEnabled)
            return;

        if (_activeVideoClip != null && player.clip != _activeVideoClip)
            return;

        try
        {
            if (coverVideoRawImage != null && player == coverVideoPlayer)
                coverVideoRawImage.color = Color.white;

            player.Play();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[GameButtonView] Failed to play cover video: {exception.Message}", this);
        }
    }

    bool EnsureVideoCover()
    {
        if (coverVideoRawImage == null)
            coverVideoRawImage = FindChildVideoRawImage();

        if (coverVideoRawImage == null)
        {
            coverVideoRawImage = CreateCoverRawImage("Cover Video");
            _coverVideoRawImageAutoCreated = coverVideoRawImage != null;
        }

        if (coverVideoRawImage == null)
            return false;

        if (coverVideoPlayer == null)
            coverVideoPlayer = coverVideoRawImage.GetComponent<VideoPlayer>() ?? coverVideoRawImage.gameObject.AddComponent<VideoPlayer>();

        ConfigureVideoPlayer();
        return coverVideoPlayer != null;
    }
    bool EnsureGifCover()
    {
        if (coverGifPlayer == null)
        {
            var rawImage = CreateCoverRawImage("Cover GIF");
            if (rawImage != null)
                coverGifPlayer = rawImage.GetComponent<AnimatedGifPlayer>() ?? rawImage.gameObject.AddComponent<AnimatedGifPlayer>();
        }

        return coverGifPlayer != null;
    }

    RawImage CreateCoverRawImage(string objectName)
    {
        if (gameIcon == null)
        {
            Debug.LogWarning("[GameButtonView] GameIcon is not assigned; animated cover cannot be created.", this);
            return null;
        }

        var mediaObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        var rectTransform = mediaObject.GetComponent<RectTransform>();
        rectTransform.SetParent(gameIcon.transform, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        var rawImage = mediaObject.GetComponent<RawImage>();
        rawImage.raycastTarget = false;
        rawImage.gameObject.SetActive(false);
        _coverVideoRawImageAutoCreated = true;
        return rawImage;
    }

    void ConfigureVideoPlayer()
    {
        if (coverVideoPlayer == null)
            return;

        coverVideoPlayer.playOnAwake = false;
        coverVideoPlayer.isLooping = true;
        coverVideoPlayer.source = VideoSource.VideoClip;
        coverVideoPlayer.renderMode = VideoRenderMode.RenderTexture;

        if (_videoPreparedHandlerRegistered)
            return;

        coverVideoPlayer.prepareCompleted += OnCoverVideoPrepared;
        _videoPreparedHandlerRegistered = true;
    }

    void StopAnimatedCover()
    {
        StopVideoCover();

        if (coverGifPlayer != null)
        {
            coverGifPlayer.Stop();
            coverGifPlayer.gameObject.SetActive(false);
        }

        _activeGifAsset = null;
    }

    void ResolveExistingVideoCoverReferences()
    {
        if (coverVideoPlayer == null)
            coverVideoPlayer = FindChildVideoPlayer();

        if (coverVideoRawImage == null && coverVideoPlayer != null)
            coverVideoRawImage = coverVideoPlayer.GetComponent<RawImage>();

        if (coverVideoRawImage == null)
            coverVideoRawImage = FindChildVideoRawImage();

        if (coverVideoPlayer == null && coverVideoRawImage != null)
            coverVideoPlayer = coverVideoRawImage.GetComponent<VideoPlayer>();
    }
    void StopVideoCover()
    {
        ResolveExistingVideoCoverReferences();
        _activeVideoClip = null;

        if (coverVideoPlayer != null && coverVideoPlayer.isPlaying)
            coverVideoPlayer.Stop();

        if (coverVideoPlayer != null)
        {
            coverVideoPlayer.clip = null;
            coverVideoPlayer.targetTexture = null;
        }

        if (coverVideoRawImage != null)
        {
            coverVideoRawImage.texture = null;
            coverVideoRawImage.color = Color.clear;
            coverVideoRawImage.gameObject.SetActive(false);
        }

        ReleaseCoverRenderTexture();
    }

    void ReleaseCoverRenderTexture()
    {
        if (_coverRenderTexture == null)
            return;

        _coverRenderTexture.Release();
        if (Application.isPlaying)
            Destroy(_coverRenderTexture);
        else
            DestroyImmediate(_coverRenderTexture);

        _coverRenderTexture = null;
    }

    static string ResolveGameName(GameData data)
    {
        if (data == null)
            return "";

        if (!string.IsNullOrWhiteSpace(data.GameName))
            return data.GameName;

        return data.Story != null ? data.Story.StoryName : "";
    }

    static bool IsFirstChapterLocked(GameData data)
    {
        if (data == null || data.Story == null || data.Story.chapters == null || data.Story.chapters.Count == 0)
            return false;

        var firstChapter = data.Story.chapters[0];
        if (firstChapter == null)
            return false;

        string episodeId = ResolveEpisodeId(firstChapter);
        bool isPremium = firstChapter.isPremium || NetworkManager.IsCatalogEpisodePremium(episodeId, false);
        if (!isPremium)
            return false;

        if (NetworkManager.IsCatalogEpisodeUnlocked(episodeId, false))
            return false;

        if (!PrototypeFeatureFlags.LocalPremiumSpendEnabled)
            return true;

        string stableKey = GetChapterUnlockKey(firstChapter);
        if (!string.IsNullOrEmpty(stableKey) && LocalChapterUnlockStore.IsUnlocked(stableKey))
            return false;

        return !LocalChapterUnlockStore.IsUnlocked("chapter_0_0");
    }

    static string ResolveEpisodeId(ChapterData chapter)
    {
        if (chapter == null)
            return "";
        if (!string.IsNullOrEmpty(chapter.chapterId))
            return SaveDataSanitizer.SanitizeIdentifier(chapter.chapterId);
        if (chapter.graph != null && !string.IsNullOrEmpty(chapter.graph.episodeId))
            return SaveDataSanitizer.SanitizeIdentifier(chapter.graph.episodeId);
        return SaveDataSanitizer.SanitizeIdentifier(chapter.chapterName);
    }

    static string GetChapterUnlockKey(ChapterData chapter)
    {
        if (chapter == null)
            return "";
        if (!string.IsNullOrEmpty(chapter.chapterId))
            return "chapter_unlock_" + SaveDataSanitizer.SafeKeyPart(chapter.chapterId);
        if (chapter.graph != null && !string.IsNullOrEmpty(chapter.graph.episodeId))
            return "chapter_unlock_" + SaveDataSanitizer.SafeKeyPart(chapter.graph.episodeId);
        return "";
    }
}
