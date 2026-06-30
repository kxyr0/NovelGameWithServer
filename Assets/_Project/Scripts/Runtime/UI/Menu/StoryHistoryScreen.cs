using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[Serializable]
public sealed class StoryHistoryStatBinding
{
    [SerializeField, HideInInspector]
    [InspectorName("Stat ID")]
    [Tooltip("Старое скрытое поле для совместимости. Если было заполнено раньше, стат найдется по этому ID; для новой настройки обычно достаточно порядка элементов в GameData.StoryStats.")]
    private string _statId;

    [SerializeField]
    [InspectorName("Icon Image")]
    [Tooltip("Image иконки стата. Скрипт каждый Refresh подставляет сюда Sprite из GameData -> Story Stats у выбранной истории.")]
    private Image _iconImage;

    [SerializeField]
    [InspectorName("Value Text")]
    [Tooltip("TMP_Text значения стата. Скрипт каждый Refresh подставляет сюда Value из GameData -> Story Stats у выбранной истории.")]
    private TMP_Text _valueText;

    [SerializeField, HideInInspector]
    private bool _hideIconWhenMissing = true;

    [SerializeField, HideInInspector] private TMP_Text _labelText;
    [SerializeField, HideInInspector] private TMP_Text _lineText;
    [SerializeField, HideInInspector] private string _lineFormat = "{1}";

    public string StatId => _statId;

    public void Apply(GameStoryStatData stat, int? valueOverride = null)
    {
        Sprite icon = stat != null ? stat.Icon : null;
        string label = stat != null ? stat.Label : "";
        int? value = stat != null
            ? valueOverride.HasValue ? valueOverride.Value : stat.Value
            : (int?)null;
        string valueText = value.HasValue ? value.Value.ToString() : "";

        if (_iconImage != null)
        {
            _iconImage.sprite = icon;
            if (_hideIconWhenMissing)
                _iconImage.enabled = icon != null;
        }

        if (_labelText != null)
            _labelText.text = label ?? "";

        if (_valueText != null)
            _valueText.text = valueText;

        if (_lineText != null)
            _lineText.text = FormatLegacyLine(label, valueText);
    }

    private string FormatLegacyLine(string label, string valueText)
    {
        if (string.IsNullOrWhiteSpace(_lineFormat))
            return valueText ?? "";

        try
        {
            return string.Format(_lineFormat, label ?? "", valueText ?? "");
        }
        catch (FormatException)
        {
            return valueText ?? "";
        }
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Story History Screen")]
public sealed class StoryHistoryScreen : MonoBehaviour
{
    [Header("История")]
    [SerializeField]
    [InspectorName("Menu Controller")]
    [Tooltip("MenuController, который открыл этот экран и запускает историю по кнопке Начать.")]
    private MenuController _menuController;

    [SerializeField]
    [InspectorName("Текущая история")]
    [Tooltip("Текущая GameData. Обычно заполняется автоматически при клике по карточке.")]
    private GameData _currentGameData;

    [Header("Картинки")]
    [SerializeField]
    [InspectorName("Image обложки")]
    [Tooltip("Image большой карточки/обложки на экране History_Screen.")]
    private Image _coverImage;

    [SerializeField]
    [InspectorName("RawImage видео обложки")]
    [Tooltip("RawImage, куда будет выводиться Game Icon Video. Можно оставить пустым: скрипт сам создаст дочерний RawImage внутри Image обложки.")]
    private RawImage _coverVideoRawImage;

    [SerializeField]
    [InspectorName("VideoPlayer обложки")]
    [Tooltip("VideoPlayer для Game Icon Video. Можно оставить пустым: скрипт добавит VideoPlayer на RawImage видео обложки.")]
    private VideoPlayer _coverVideoPlayer;

    [SerializeField]
    [InspectorName("GIF плеер обложки")]
    [Tooltip("AnimatedGifPlayer для Game Icon Gif. Используется только если Game Icon Video не задан. Можно оставить пустым: скрипт создаст его сам.")]
    private AnimatedGifPlayer _coverGifPlayer;
    [Header("Тексты")]
    [SerializeField]
    [InspectorName("Название")]
    [Tooltip("TMP_Text названия истории.")]
    private TMP_Text _titleText;

    [SerializeField]
    [InspectorName("Жанр")]
    [Tooltip("TMP_Text жанра истории.")]
    private TMP_Text _genreText;

    [SerializeField]
    [InspectorName("Количество серий")]
    [Tooltip("TMP_Text строки серии, например Серия 1/11.")]
    private TMP_Text _episodeText;

    [SerializeField]
    [InspectorName("Описание")]
    [Tooltip("TMP_Text описания истории.")]
    private TMP_Text _descriptionText;

    [Header("Статы")]
    [SerializeField]
    [InspectorName("Статы")]
    [Tooltip("TMP_Text для статов истории. Можно привязать по Stat ID или по порядку.")]
    private StoryHistoryStatBinding[] _statBindings = Array.Empty<StoryHistoryStatBinding>();

    [Header("Кнопки")]
    [SerializeField]
    [InspectorName("Кнопка Начать")]
    [Tooltip("Button, который запускает выбранную историю.")]
    private Button _startButton;

    [SerializeField]
    [InspectorName("Текст кнопки Начать")]
    [Tooltip("TMP_Text внутри кнопки старта. Если поле пустое, скрипт попробует найти текст внутри самой кнопки.")]
    private TMP_Text _startButtonText;

    [SerializeField]
    [InspectorName("Кнопка Заново")]
    [Tooltip("Сбрасывает прогресс выбранной истории. Сейчас не запускает историю автоматически.")]
    private Button _restartButton;

    [SerializeField]
    [InspectorName("Кнопка Сохранение")]
    [Tooltip("Открывает экран сохранений через StoryScreenNavigator. Screen ID задается ниже.")]
    private Button _saveButton;

    [SerializeField]
    [InspectorName("Кнопка Гардероб")]
    [Tooltip("Открывает экран гардероба для выбранной истории.")]
    private Button _wardrobeButton;

    [SerializeField]
    [InspectorName("Кнопка закрыть")]
    [Tooltip("Опциональная кнопка закрытия detail-экрана.")]
    private Button _closeButton;

    [SerializeField]
    [InspectorName("Заново сразу начинает")]
    [Tooltip("Задел на будущее: если включить, кнопка Заново после сброса сразу запустит выбранную историю.")]
    private bool _restartStartsStoryAfterReset;

    [SerializeField]
    [InspectorName("Экран сохранений")]
    [Tooltip("Screen ID экрана сохранений. На root экрана сохранений поставь UIScreenMarker с таким же ID.")]
    private string _saveTargetScreenId = "Save";

    [SerializeField]
    [InspectorName("Контроллер сохранений")]
    [Tooltip("StorySaveSlotsScreenController на экране сохранений. Если оставить пустым, History попробует найти его по Screen ID экрана сохранений.")]
    private StorySaveSlotsScreenController _saveSlotsScreen;

    [SerializeField]
    [InspectorName("Авто найти сохранения")]
    [Tooltip("Если Контроллер сохранений не назначен, искать StorySaveSlotsScreenController внутри экрана с указанным Screen ID. Нужно для перехода History -> Saves с правильной GameData.")]
    private bool _autoFindSaveSlotsScreen = true;

    [SerializeField]
    [InspectorName("Экран гардероба")]
    [Tooltip("Screen ID гардероба. Обычно Wardrobe.")]
    private string _wardrobeTargetScreenId = "Wardrobe";

    [SerializeField]
    [InspectorName("Экран возврата")]
    [Tooltip("Screen ID, куда вернуться по кнопке закрытия.")]
    private string _closeTargetScreenId = "MainScreen";

    private RenderTexture _coverRenderTexture;
    private bool _coverVideoPreparedHandlerRegistered;
    private RectTransform _coverVideoSnapshotRect;
    private Vector2 _coverVideoBaseAnchoredPosition;
    private Vector2 _coverVideoBaseSize;
    private Vector3 _coverVideoBaseScale = Vector3.one;
    private float _coverVideoBaseRotationZ;
    private bool _coverVideoRawImageAutoCreated;
    public GameData CurrentGameData => _currentGameData;

    private void OnEnable()
    {
        EnsureCurrentGameData();
        BindButtons();
        Refresh();
    }

    private void OnDisable()
    {
        UnbindButtons();
        StopAnimatedCover();
    }

    private void OnDestroy()
    {
        if (_coverVideoPlayer != null && _coverVideoPreparedHandlerRegistered)
        {
            _coverVideoPlayer.prepareCompleted -= OnCoverVideoPrepared;
            _coverVideoPreparedHandlerRegistered = false;
        }

        ReleaseCoverRenderTexture();
    }

    private void OnValidate()
    {
        if (_statBindings == null)
            _statBindings = Array.Empty<StoryHistoryStatBinding>();

        ResolveStartButtonText();
        _closeTargetScreenId = UIScreenState.NormalizeScreenId(_closeTargetScreenId);
        _saveTargetScreenId = UIScreenState.NormalizeScreenId(_saveTargetScreenId);
        _wardrobeTargetScreenId = UIScreenState.NormalizeScreenId(_wardrobeTargetScreenId);
    }

    public void Configure(GameData gameData, MenuController menuController)
    {
        _currentGameData = gameData;
        if (menuController != null)
            _menuController = menuController;

        AppLogger.Info(
            AppLogCategory.Menu,
            nameof(StoryHistoryScreen),
            nameof(Configure),
            "[HISTORY][CONFIGURE] Story history screen configured.",
            BuildHistoryMetadata("configure"));

        Refresh();
    }

    public void Refresh()
    {
        EnsureCurrentGameData();

        GameData data = _currentGameData;

        ApplyCover(data);

        if (_titleText != null)
            _titleText.text = ResolveTitle(data);

        if (_genreText != null)
            _genreText.text = data != null ? data.GenreText : "";

        if (_episodeText != null)
            _episodeText.text = data != null ? data.EpisodeProgressText : "";

        if (_descriptionText != null)
            _descriptionText.text = data != null ? data.Description : "";

        RefreshStats(data);
        RefreshStartButton(data);

        AppLogger.Info(
            AppLogCategory.Menu,
            nameof(StoryHistoryScreen),
            nameof(Refresh),
            "[HISTORY][REFRESH] Story history screen refreshed.",
            BuildHistoryMetadata("refresh"));

        bool canUseStoryActions = data != null && data.CanStartStory;
        SetButtonInputEnabled(_restartButton, canUseStoryActions);
        SetButtonInputEnabled(
            _saveButton,
            canUseStoryActions &&
            _menuController != null &&
            _menuController.ScreenNavigator != null &&
            !string.IsNullOrWhiteSpace(_saveTargetScreenId));
        SetButtonInputEnabled(
            _wardrobeButton,
            canUseStoryActions &&
            _menuController != null &&
            _menuController.ScreenNavigator != null &&
            !string.IsNullOrWhiteSpace(_wardrobeTargetScreenId));
    }

    public void StartCurrentStory()
    {
        AppLogger.Info(
            AppLogCategory.Menu,
            nameof(StoryHistoryScreen),
            nameof(StartCurrentStory),
            "[HISTORY][START] Start button clicked.",
            BuildHistoryMetadata("start"));

        if (_menuController == null)
        {
            Debug.LogWarning("[StoryHistoryScreen] MenuController is not assigned.", this);
            return;
        }

        if (_currentGameData == null)
        {
            Debug.LogWarning("[StoryHistoryScreen] Current GameData is empty.", this);
            return;
        }

        if (!_currentGameData.CanStartStory)
        {
            ToastManager.Instance?.ShowSystemMessage(_currentGameData.ComingSoonButtonText);
            Debug.LogWarning($"[StoryHistoryScreen] Story '{_currentGameData.name}' is marked as coming soon or has no playable chapters.", _currentGameData);
            return;
        }

        _menuController.StartStory(_currentGameData, BuildStoryStartStatValues(_currentGameData));
    }

    public void RestartCurrentStory()
    {
        if (!CanUseCurrentStoryActions("restart"))
            return;

        if (!ResetCurrentStoryProgress())
            return;

        if (_restartStartsStoryAfterReset)
        {
            StartCurrentStory();
            return;
        }

        Refresh();
    }

    public bool ResetCurrentStoryProgress()
    {
        AppLogger.Info(
            AppLogCategory.Menu,
            nameof(StoryHistoryScreen),
            nameof(ResetCurrentStoryProgress),
            "[HISTORY][RESET] Reset story progress requested.",
            BuildHistoryMetadata("reset"));

        if (!CanUseCurrentStoryActions("reset"))
            return false;

        if (_currentGameData == null || _currentGameData.Story == null)
        {
            Debug.LogWarning("[StoryHistoryScreen] Cannot reset progress: selected story is empty.", this);
            return false;
        }

        StoryData story = _currentGameData.Story;
        string storyId = ResolveStoryId(story);
        StoryProgressResetUtility.ResetStoryProgress(story, storyId);
        ToastManager.Instance?.ShowSystemMessage("История сброшена.");
        return true;
    }

    public void OpenSaves()
    {
        if (!CanUseCurrentStoryActions("save"))
            return;

        ApplySaveSlotsContext("before_open");
        if (OpenTargetScreen(_saveTargetScreenId, "save"))
            ApplySaveSlotsContext("after_open");
    }

    public void OpenWardrobe()
    {
        AppLogger.Info(
            AppLogCategory.Menu,
            nameof(StoryHistoryScreen),
            nameof(OpenWardrobe),
            "[HISTORY][WARDROBE] Wardrobe button clicked from story history screen.",
            BuildHistoryMetadata("open_wardrobe"));

        if (!CanUseCurrentStoryActions("wardrobe"))
            return;

        if (_menuController != null && _menuController.OpenWardrobeScreenFor(_currentGameData))
            return;

        OpenTargetScreen(_wardrobeTargetScreenId, "wardrobe");
    }

    public void Close()
    {
        if (_menuController != null && _menuController.ScreenNavigator != null && !string.IsNullOrWhiteSpace(_closeTargetScreenId))
            _menuController.ScreenNavigator.OpenScreen(_closeTargetScreenId);
    }

    private void ApplyCover(GameData data)
    {
        StopAnimatedCover();

        bool hasAnimatedCover = data != null && (data.GameIconVideo != null || data.GameIconGif != null);
        Sprite cover = data != null ? data.GameIcon : null;
        if (_coverImage != null)
        {
            if (cover != null)
                RuntimeTextureFallback.EnsureImageVisible(_coverImage, cover);
            else
                RuntimeTextureFallback.ApplyImagePlaceholder(_coverImage);
        }

        if (data == null)
            return;

        if (data.GameIconVideo != null)
            ShowVideoCover(data);
        else if (data.GameIconGif != null)
            ShowGifCover(data.GameIconGif);
    }

    private void ShowVideoCover(GameData data)
    {
        VideoClip clip = data != null ? data.GameIconVideo : null;
        if (clip == null || !EnsureVideoCover())
            return;

        if (_coverGifPlayer != null)
        {
            _coverGifPlayer.Stop();
            _coverGifPlayer.gameObject.SetActive(false);
        }

        ReleaseCoverRenderTexture();

        int width = Mathf.Max(16, (int)clip.width);
        int height = Mathf.Max(16, (int)clip.height);
        _coverRenderTexture = new RenderTexture(width, height, 0)
        {
            name = $"{nameof(StoryHistoryScreen)} Cover RenderTexture"
        };
        _coverRenderTexture.Create();

        try
        {
            ConfigureVideoPlayer();
            _coverVideoPlayer.source = VideoSource.VideoClip;
            _coverVideoPlayer.clip = clip;
            _coverVideoPlayer.targetTexture = _coverRenderTexture;
            _coverVideoRawImage.texture = _coverRenderTexture;
            _coverVideoRawImage.enabled = true;
            _coverVideoRawImage.color = Color.clear;

            ApplyVideoCoverOverrides(data != null ? data.MenuCardOverrides : null);

            _coverVideoRawImage.gameObject.SetActive(true);
            _coverVideoPlayer.gameObject.SetActive(true);
            _coverVideoPlayer.Prepare();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[StoryHistoryScreen] Failed to prepare cover video '{clip.name}': {exception.Message}", this);
            StopAnimatedCover();
        }
    }

    private void ShowGifCover(TextAsset gifAsset)
    {
        if (gifAsset == null || !EnsureGifCover())
            return;

        StopVideoCover();
        _coverGifPlayer.transform.SetAsLastSibling();
        _coverGifPlayer.gameObject.SetActive(true);
        _coverGifPlayer.Play(gifAsset);
    }

    private void OnCoverVideoPrepared(VideoPlayer player)
    {
        if (player == null || !isActiveAndEnabled)
            return;

        try
        {
            if (_coverVideoRawImage != null && player == _coverVideoPlayer)
                _coverVideoRawImage.color = Color.white;

            player.Play();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[StoryHistoryScreen] Failed to play cover video: {exception.Message}", this);
        }
    }

    private bool EnsureVideoCover()
    {
        if (_coverVideoPlayer == null)
            _coverVideoPlayer = FindChildVideoPlayer();

        if (_coverVideoRawImage == null && _coverVideoPlayer != null)
            _coverVideoRawImage = _coverVideoPlayer.GetComponent<RawImage>();

        if (_coverVideoRawImage == null)
            _coverVideoRawImage = FindChildVideoRawImage();

        if (_coverVideoRawImage == null)
        {
            _coverVideoRawImage = CreateCoverRawImage("History Cover Video");
            _coverVideoRawImageAutoCreated = _coverVideoRawImage != null;
        }

        if (_coverVideoRawImage == null)
            return false;

        if (_coverVideoPlayer == null)
            _coverVideoPlayer = _coverVideoRawImage.GetComponent<VideoPlayer>() ?? _coverVideoRawImage.gameObject.AddComponent<VideoPlayer>();

        ConfigureVideoPlayer();
        return _coverVideoPlayer != null;
    }

    private VideoPlayer FindChildVideoPlayer()
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

    private RawImage FindChildVideoRawImage()
    {
        RawImage[] rawImages = _coverImage != null
            ? _coverImage.GetComponentsInChildren<RawImage>(true)
            : GetComponentsInChildren<RawImage>(true);
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

    private void ApplyVideoCoverOverrides(GameMenuCardOverrideSettings overrides)
    {
        if (_coverVideoRawImage == null)
            return;

        RectTransform videoRect = _coverVideoRawImage.rectTransform;
        if (videoRect == null)
            return;

        TakeVideoRectSnapshot(videoRect);

        Vector2 videoSize = _coverVideoBaseSize;
        Vector2 videoPosition = _coverVideoBaseAnchoredPosition;
        float videoRotationZ = _coverVideoBaseRotationZ;

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
        videoRect.localScale = _coverVideoBaseScale;
        SetLocalRotationZ(videoRect, videoRotationZ);
        ApplyAnimatedCoverLayering(overrides);
    }

    private void TakeVideoRectSnapshot(RectTransform videoRect)
    {
        if (videoRect == null || _coverVideoSnapshotRect == videoRect)
            return;

        _coverVideoSnapshotRect = videoRect;
        _coverVideoBaseAnchoredPosition = videoRect.anchoredPosition;
        _coverVideoBaseSize = videoRect.rect.size;
        _coverVideoBaseScale = videoRect.localScale;
        _coverVideoBaseRotationZ = NormalizeAngle(videoRect.localEulerAngles.z);
    }

    private void ApplyAnimatedCoverLayering(GameMenuCardOverrideSettings overrides)
    {
        if (_coverVideoRawImage == null)
            return;

        Transform mediaTransform = _coverVideoRawImage.transform;
        if (mediaTransform == null)
            return;

        bool keepBelowFrame = overrides == null || overrides.KeepVideoBelowCardFrame;
        if (keepBelowFrame && TryPlaceBeforeCardFrame(mediaTransform))
            return;

        if (_coverVideoRawImageAutoCreated || mediaTransform.parent == (_coverImage != null ? _coverImage.transform : null))
            mediaTransform.SetAsLastSibling();
    }

    private static bool TryPlaceBeforeCardFrame(Transform mediaTransform)
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

    private static Transform FindCardFrameSibling(Transform parent)
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

    private static bool IsCardFrameName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        string normalized = objectName.Replace(" ", "").Replace("-", "_");
        return normalized.Equals("Card_Frame", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("CardFrame", StringComparison.OrdinalIgnoreCase) ||
               normalized.IndexOf("card_frame", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SetRectSize(RectTransform rectTransform, Vector2 size)
    {
        if (rectTransform == null)
            return;

        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, size.x));
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, size.y));
    }

    private static void SetLocalRotationZ(RectTransform rectTransform, float rotationZ)
    {
        if (rectTransform == null)
            return;

        Vector3 euler = rectTransform.localEulerAngles;
        euler.z = rotationZ;
        rectTransform.localEulerAngles = euler;
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
            angle -= 360f;
        if (angle < -180f)
            angle += 360f;
        return angle;
    }
    private bool EnsureGifCover()
    {
        if (_coverGifPlayer == null)
        {
            RawImage rawImage = CreateCoverRawImage("History Cover GIF");
            if (rawImage != null)
                _coverGifPlayer = rawImage.GetComponent<AnimatedGifPlayer>() ?? rawImage.gameObject.AddComponent<AnimatedGifPlayer>();
        }

        return _coverGifPlayer != null;
    }

    private RawImage CreateCoverRawImage(string objectName)
    {
        if (_coverImage == null)
        {
            Debug.LogWarning("[StoryHistoryScreen] Image обложки не назначен, поэтому видео/GIF обложку создать нельзя.", this);
            return null;
        }

        GameObject mediaObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        RectTransform rectTransform = mediaObject.GetComponent<RectTransform>();
        rectTransform.SetParent(_coverImage.transform, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        RawImage rawImage = mediaObject.GetComponent<RawImage>();
        rawImage.raycastTarget = false;
        rawImage.gameObject.SetActive(false);
        return rawImage;
    }

    private void ConfigureVideoPlayer()
    {
        if (_coverVideoPlayer == null)
            return;

        _coverVideoPlayer.playOnAwake = false;
        _coverVideoPlayer.waitForFirstFrame = true;
        _coverVideoPlayer.isLooping = true;
        _coverVideoPlayer.source = VideoSource.VideoClip;
        _coverVideoPlayer.renderMode = VideoRenderMode.RenderTexture;

        if (_coverVideoPreparedHandlerRegistered)
            return;

        _coverVideoPlayer.prepareCompleted += OnCoverVideoPrepared;
        _coverVideoPreparedHandlerRegistered = true;
    }

    private void StopAnimatedCover()
    {
        StopVideoCover();

        if (_coverGifPlayer != null)
        {
            _coverGifPlayer.Stop();
            _coverGifPlayer.gameObject.SetActive(false);
        }
    }

    private void ResolveExistingVideoCoverReferences()
    {
        if (_coverVideoPlayer == null)
            _coverVideoPlayer = FindChildVideoPlayer();

        if (_coverVideoRawImage == null && _coverVideoPlayer != null)
            _coverVideoRawImage = _coverVideoPlayer.GetComponent<RawImage>();

        if (_coverVideoRawImage == null)
            _coverVideoRawImage = FindChildVideoRawImage();

        if (_coverVideoPlayer == null && _coverVideoRawImage != null)
            _coverVideoPlayer = _coverVideoRawImage.GetComponent<VideoPlayer>();
    }

    private void StopVideoCover()
    {
        ResolveExistingVideoCoverReferences();

        if (_coverVideoPlayer != null && _coverVideoPlayer.isPlaying)
            _coverVideoPlayer.Stop();

        if (_coverVideoPlayer != null)
        {
            _coverVideoPlayer.clip = null;
            _coverVideoPlayer.targetTexture = null;
        }

        if (_coverVideoRawImage != null)
        {
            _coverVideoRawImage.texture = null;
            _coverVideoRawImage.color = Color.clear;
            _coverVideoRawImage.gameObject.SetActive(false);
        }

        ReleaseCoverRenderTexture();
    }

    private void ReleaseCoverRenderTexture()
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

    private void RefreshStats(GameData data)
    {
        if (_statBindings == null)
            return;

        for (int i = 0; i < _statBindings.Length; i++)
        {
            StoryHistoryStatBinding binding = _statBindings[i];
            if (binding == null)
                continue;

            GameStoryStatData stat = ResolveStat(data, binding.StatId, i);
            binding.Apply(stat, ResolveStoryStatValue(data, stat));
        }
    }

    private static Dictionary<string, int> BuildStoryStartStatValues(GameData data)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (data == null || data.StoryStats == null)
            return result;

        for (int i = 0; i < data.StoryStats.Count; i++)
        {
            GameStoryStatData stat = data.StoryStats[i];
            if (stat == null)
                continue;

            string statId = SaveDataSanitizer.SanitizeStatKey(stat.StatId);
            if (string.IsNullOrEmpty(statId))
                continue;

            int value = ResolveStoryStatValue(data, stat) ?? stat.Value;
            result[statId] = SaveDataSanitizer.ClampStatValue(value);
        }

        return result;
    }

    private static GameStoryStatData ResolveStat(GameData data, string statId, int index)
    {
        if (data == null || data.StoryStats == null)
            return null;

        if (!string.IsNullOrWhiteSpace(statId))
        {
            for (int i = 0; i < data.StoryStats.Count; i++)
            {
                GameStoryStatData stat = data.StoryStats[i];
                if (stat != null && StoryStatId.EqualsCanonical(stat.StatId, statId))
                    return stat;
            }
        }

        return index >= 0 && index < data.StoryStats.Count ? data.StoryStats[index] : null;
    }

    private static int? ResolveStoryStatValue(GameData data, GameStoryStatData stat)
    {
        if (data == null || stat == null || string.IsNullOrWhiteSpace(stat.StatId))
            return null;

        string statId = SaveDataSanitizer.SanitizeStatKey(stat.StatId);
        if (string.IsNullOrEmpty(statId))
            return null;

        string storyId = ResolveStoryId(data.Story);
        if (TryGetRuntimeStoryStatValue(storyId, statId, out int runtimeValue))
            return runtimeValue;

        if (SaveManager.Instance == null || string.IsNullOrEmpty(storyId))
            return null;

        int slot = StorySaveSlotSelection.GetSelectedSlot(storyId);
        SaveData saveData = SaveManager.Instance.LoadForStorySlotIfExists(storyId, slot);
        if (!IsSaveForStory(saveData, storyId))
            return null;

        return TryGetSavedStatValue(saveData, statId, out int savedValue) ? savedValue : null;
    }

    private static bool TryGetRuntimeStoryStatValue(string storyId, string statId, out int value)
    {
        value = 0;

        GameState state = GameState.Instance;
        if (state == null || state.stats == null)
            return false;

        string runtimeStoryId = SaveDataSanitizer.SanitizeIdentifier(state.CurrentStoryId);
        if (!string.IsNullOrEmpty(storyId) && !string.Equals(runtimeStoryId, storyId, StringComparison.OrdinalIgnoreCase))
            return false;

        return TryFindStatValue(state.stats, statId, out value);
    }

    private static bool TryGetSavedStatValue(SaveData saveData, string statId, out int value)
    {
        value = 0;
        if (saveData == null || saveData.statKeys == null || saveData.statValues == null)
            return false;

        int count = Mathf.Min(saveData.statKeys.Count, saveData.statValues.Count);
        for (int i = 0; i < count; i++)
        {
            if (StoryStatId.EqualsCanonical(saveData.statKeys[i], statId))
            {
                value = saveData.statValues[i];
                return true;
            }
        }

        return false;
    }

    private static bool TryFindStatValue(IDictionary<string, int> stats, string statId, out int value)
    {
        value = 0;
        if (stats == null || string.IsNullOrWhiteSpace(statId))
            return false;

        foreach (KeyValuePair<string, int> pair in stats)
        {
            if (StoryStatId.EqualsCanonical(pair.Key, statId))
            {
                value = pair.Value;
                return true;
            }
        }

        return false;
    }

    private static bool IsSaveForStory(SaveData saveData, string storyId)
    {
        if (saveData == null)
            return false;

        string saveStoryId = SaveDataSanitizer.SanitizeIdentifier(saveData.storyId);
        return string.IsNullOrEmpty(storyId) ||
               string.IsNullOrEmpty(saveStoryId) ||
               string.Equals(saveStoryId, storyId, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshStartButton(GameData data)
    {
        bool canStart = data != null && data.CanStartStory;

        if (_startButton != null)
            _startButton.interactable = canStart;

        TMP_Text label = ResolveStartButtonText();
        if (label != null)
            label.text = data != null ? data.StartButtonText : "Скоро";
    }

    private static void SetButtonInputEnabled(Button button, bool enabled)
    {
        if (button == null)
            return;

        button.interactable = enabled;

        CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = button.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private bool CanUseCurrentStoryActions(string action)
    {
        EnsureCurrentGameData();

        if (_currentGameData != null && _currentGameData.CanStartStory)
            return true;

        AppLogger.Info(
            AppLogCategory.Menu,
            nameof(StoryHistoryScreen),
            nameof(CanUseCurrentStoryActions),
            "[HISTORY][ACTION_BLOCKED] Story history action is disabled for an empty or coming soon story.",
            BuildHistoryMetadata(action));
        return false;
    }

    private TMP_Text ResolveStartButtonText()
    {
        if (_startButtonText == null && _startButton != null)
            _startButtonText = _startButton.GetComponentInChildren<TMP_Text>(true);

        return _startButtonText;
    }

    private static string ResolveTitle(GameData data)
    {
        if (data == null)
            return "";

        if (!string.IsNullOrWhiteSpace(data.GameName))
            return data.GameName;

        return data.Story != null ? data.Story.StoryName : "";
    }

    private void EnsureCurrentGameData()
    {
        if (_menuController == null)
            _menuController = FindObjectOfType<MenuController>(true);

        if (_currentGameData == null && _menuController != null)
            _currentGameData = _menuController.SelectedGame;
    }

    private static string ResolveStoryId(StoryData story)
    {
        if (story == null)
            return "";

        string storyId = SaveDataSanitizer.SanitizeIdentifier(story.StoryId);
        if (!string.IsNullOrEmpty(storyId))
            return storyId;

        storyId = SaveDataSanitizer.SanitizeIdentifier(story.storyId);
        if (!string.IsNullOrEmpty(storyId))
            return storyId;

        return SaveDataSanitizer.SanitizeIdentifier(story.name);
    }

    private IDictionary<string, object> BuildHistoryMetadata(string reason)
    {
        GameData data = _currentGameData;
        StoryData story = data != null ? data.Story : null;
        GameWardrobeSetupSettings wardrobe = data != null ? data.WardrobeSetup : null;
        return LogMetadata.Of(
            "reason", reason ?? "",
            "gameData", data != null ? data.name : "",
            "gameName", data != null ? data.GameName : "",
            "storyId", ResolveStoryId(story),
            "storyAsset", story != null ? story.name : "",
            "canStart", data != null && data.CanStartStory,
            "episodeText", data != null ? data.EpisodeProgressText : "",
            "startText", data != null ? data.StartButtonText : "",
            "hasMenuController", _menuController != null,
            "appearanceOptions", wardrobe != null && wardrobe.AppearanceOptions != null ? wardrobe.AppearanceOptions.Count : 0,
            "outfitItems", wardrobe != null && wardrobe.OutfitItems != null ? wardrobe.OutfitItems.Count : 0,
            "hairItems", wardrobe != null && wardrobe.HairItems != null ? wardrobe.HairItems.Count : 0,
            "accessoryItems", wardrobe != null && wardrobe.AccessoryItems != null ? wardrobe.AccessoryItems.Count : 0);
    }

    private void ApplySaveSlotsContext(string reason)
    {
        StorySaveSlotsScreenController saveSlotsScreen = ResolveSaveSlotsScreen();
        if (saveSlotsScreen == null)
        {
            Debug.LogWarning($"[StoryHistoryScreen] Cannot pass GameData to saves screen: StorySaveSlotsScreenController was not found for Screen ID '{_saveTargetScreenId}'.", this);
            return;
        }

        saveSlotsScreen.SetStoryContext(_currentGameData);
        AppLogger.Info(
            AppLogCategory.Menu,
            nameof(StoryHistoryScreen),
            nameof(ApplySaveSlotsContext),
            "[HISTORY][SAVES_CONTEXT] Passed selected GameData to saves screen.",
            LogMetadata.Of(
                "reason", reason ?? "",
                "saveScreenId", _saveTargetScreenId ?? "",
                "gameData", _currentGameData != null ? _currentGameData.name : "",
                "storyId", ResolveStoryId(_currentGameData != null ? _currentGameData.Story : null)));
    }

    private StorySaveSlotsScreenController ResolveSaveSlotsScreen()
    {
        if (_saveSlotsScreen != null)
            return _saveSlotsScreen;

        if (!_autoFindSaveSlotsScreen)
            return null;

        string saveScreenId = UIScreenState.NormalizeScreenId(_saveTargetScreenId);
        if (!string.IsNullOrEmpty(saveScreenId))
        {
            UIScreenMarker[] markers = FindObjectsOfType<UIScreenMarker>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                UIScreenMarker marker = markers[i];
                if (marker == null || marker.ScreenId != saveScreenId)
                    continue;

                StorySaveSlotsScreenController fromMarker = marker.GetComponentInChildren<StorySaveSlotsScreenController>(true);
                if (fromMarker != null)
                {
                    _saveSlotsScreen = fromMarker;
                    return _saveSlotsScreen;
                }
            }
        }

        StorySaveSlotsScreenController[] controllers = FindObjectsOfType<StorySaveSlotsScreenController>(true);
        if (controllers == null || controllers.Length == 0)
            return null;

        if (controllers.Length == 1)
        {
            _saveSlotsScreen = controllers[0];
            return _saveSlotsScreen;
        }

        for (int i = 0; i < controllers.Length; i++)
        {
            StorySaveSlotsScreenController controller = controllers[i];
            if (controller == null)
                continue;

            UIScreenMarker marker = controller.GetComponentInParent<UIScreenMarker>(true);
            if (marker != null && marker.ScreenId == saveScreenId)
            {
                _saveSlotsScreen = controller;
                return _saveSlotsScreen;
            }
        }

        return null;
    }
    private bool OpenTargetScreen(string screenId, string label)
    {
        if (_menuController == null || _menuController.ScreenNavigator == null)
        {
            Debug.LogWarning($"[StoryHistoryScreen] Cannot open {label}: StoryScreenNavigator is not assigned.", this);
            return false;
        }

        screenId = UIScreenState.NormalizeScreenId(screenId);
        if (string.IsNullOrWhiteSpace(screenId))
        {
            Debug.LogWarning($"[StoryHistoryScreen] Cannot open {label}: target Screen ID is empty.", this);
            return false;
        }

        bool opened = _menuController.ScreenNavigator.OpenScreen(screenId);
        if (!opened)
            Debug.LogWarning($"[StoryHistoryScreen] Cannot open {label}: screen '{screenId}' is not assigned. Add UIScreenMarker or a navigator binding.", this);

        return opened;
    }

    private void BindButtons()
    {
        if (_startButton != null)
        {
            _startButton.onClick.RemoveListener(StartCurrentStory);
            _startButton.onClick.AddListener(StartCurrentStory);
        }

        if (_restartButton != null)
        {
            _restartButton.onClick.RemoveListener(RestartCurrentStory);
            _restartButton.onClick.AddListener(RestartCurrentStory);
        }

        if (_saveButton != null)
        {
            _saveButton.onClick.RemoveListener(OpenSaves);
            _saveButton.onClick.AddListener(OpenSaves);
        }

        if (_wardrobeButton != null)
        {
            _wardrobeButton.onClick.RemoveListener(OpenWardrobe);
            _wardrobeButton.onClick.AddListener(OpenWardrobe);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
            _closeButton.onClick.AddListener(Close);
        }
    }

    private void UnbindButtons()
    {
        if (_startButton != null)
            _startButton.onClick.RemoveListener(StartCurrentStory);

        if (_restartButton != null)
            _restartButton.onClick.RemoveListener(RestartCurrentStory);

        if (_saveButton != null)
            _saveButton.onClick.RemoveListener(OpenSaves);

        if (_wardrobeButton != null)
            _wardrobeButton.onClick.RemoveListener(OpenWardrobe);

        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Close);
    }
}
