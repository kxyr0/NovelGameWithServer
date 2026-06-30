using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Michsky.MUIP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using VContainer;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Story Start Loading Screen")]
public sealed class StoryStartLoadingScreen : MonoBehaviour, IStoryStartLoadingScreen, IStoryStartLoadingFlowObserver
{
    [Header("Корень")]
    [SerializeField]
    [Tooltip("Корневой объект загрузочного экрана. Его можно держать выключенным в сцене: скрипт сам включит его перед показом и выключит после скрытия.")]
    private GameObject _root;

    [SerializeField]
    [Tooltip("CanvasGroup корня. Нужен для плавного появления/исчезновения и блокировки кликов по экрану History во время загрузки.")]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    [Tooltip("Если включено, загрузочный экран будет автоматически скрыт при Awake. Удобно, когда панель лежит в сцене сразу активной для настройки.")]
    private bool _hideOnAwake = true;

    [SerializeField]
    [Tooltip("Если включено, корневой объект выключается после завершения анимации скрытия. Оставь включенным, если экран должен полностью пропадать из иерархии UI.")]
    private bool _deactivateRootOnHide = true;

    [SerializeField]
    [Tooltip("Вызывать завершение загрузки до fade-out загрузочного экрана. Включи, чтобы под исчезающей загрузкой уже был Story screen, а не History screen.")]
    private bool _invokeCompleteBeforeHide = true;

    [Header("Обложка истории")]
    [SerializeField]
    [Tooltip("Image, куда обязательно подставляется обложка выбранной истории из GameData.GameIcon. Назначь сюда главный арт загрузочного экрана.")]
    private Image _coverImage;

    [SerializeField]
    [Tooltip("Запасная обложка, если у GameData не назначен GameIcon. Лучше заполнить, но нормальный путь - назначить обложку в каждой GameData.")]
    private Sprite _fallbackCoverSprite;

    [SerializeField]
    [Tooltip("RawImage для видео-обложки. Можно оставить пустым: скрипт сам создаст его внутри Cover Image.")]
    private RawImage _coverVideoRawImage;

    [SerializeField]
    [Tooltip("VideoPlayer для GameData.GameIconVideo. Можно оставить пустым: скрипт добавит его на RawImage видео-обложки.")]
    private VideoPlayer _coverVideoPlayer;

    [SerializeField]
    [InspectorName("Растянуть видео на Loading")]
    [Tooltip("Только для этого загрузочного экрана. Если включено, RawImage видео растягивается на весь RectTransform своего родителя и не использует Video Size/Position/Rotation из GameData.")]
    private bool _stretchVideoToCoverRectOnLoadingScreen;

    [SerializeField]
    [InspectorName("Rotation Z растянутого видео")]
    [Tooltip("Rotation Z для растянутого видео на загрузочном экране. Обычно 0. Если файл видео повернут боком, поставь -90 или 90.")]
    private float _stretchedVideoRotationZ;

    [SerializeField]
    [Tooltip("AnimatedGifPlayer для GameData.GameIconGif. Используется только если видео-обложка не задана.")]
    private AnimatedGifPlayer _coverGifPlayer;
    [SerializeField]
    [Tooltip("Сохранять пропорции обложки при подстановке в Image.")]
    private bool _preserveCoverAspect = true;

    [Header("Тексты")]
    [SerializeField]
    [Tooltip("TMP_Text для названия истории. Скрипт берёт GameData.GameName, затем StoryData.StoryName, затем имя asset.")]
    private TMP_Text _titleText;

    [SerializeField]
    [Tooltip("TMP_Text для статуса загрузки: подготовка обложки, текстур, аудио и финализация.")]
    private TMP_Text _statusText;

    [SerializeField]
    [Tooltip("TMP_Text для процентов. Можно оставить пустым, если проценты в UI не нужны.")]
    private TMP_Text _percentText;

    [SerializeField]
    [Tooltip("Текст, который показывается перед началом прогресса.")]
    private string _initialStatusText = "Готовим историю...";

    [SerializeField]
    [Tooltip("Текст, который показывается на 100% перед скрытием экрана.")]
    private string _completeStatusText = "Готово";

    [Header("Прогресс")]
    [SerializeField]
    [Tooltip("Необязательный Slider прогресса. Если не назначен, логика всё равно работает, а проценты можно вывести через TMP_Text.")]
    private Slider _progressSlider;

    [SerializeField]
    [Tooltip("Необязательный Image с Fill Amount. Используй, если вместо Slider у тебя кастомная полоска прогресса.")]
    private Image _progressFillImage;

    [SerializeField]
    [Tooltip("Progress Bar из Modern UI Pack. Назначай сюда именно компонент Progress Bar (Script), а не UI Manager Progress Bar: UI Manager только красит стиль.")]
    private ProgressBar _progressBar;

    [SerializeField]
    [Tooltip("Отключать автозаполнение MUIP через isOn, чтобы прогрессом управлял только StoryStartLoadingScreen.")]
    private bool _disableMuipAutoProgress = true;

    [SerializeField]
    [Tooltip("До какого значения фейковый прогресс может дойти, пока реальная подготовка ещё не завершилась. Обычно 0.85-0.95.")]
    [Range(0.1f, 0.99f)]
    private float _fakeProgressCeiling = 0.92f;

    [SerializeField]
    [Tooltip("Минимальное время показа загрузочного экрана. Даже если всё загрузилось мгновенно, экран не моргнёт.")]
    [Min(0f)]
    private float _minVisibleDuration = 1.25f;

    [SerializeField]
    [Tooltip("Сколько секунд фейковый прогресс плавно идёт к потолку, пока настоящая подготовка ещё работает.")]
    [Min(0.05f)]
    private float _fakeProgressDuration = 2.4f;

    [SerializeField]
    [Tooltip("Скорость, с которой визуальная полоска догоняет целевой прогресс. Чем выше значение, тем быстрее проценты реагируют.")]
    [Min(0.01f)]
    private float _progressCatchUpSpeed = 2.8f;

    [SerializeField]
    [Tooltip("Сколько секунд экран дотягивает прогресс от текущего значения до 100% после реальной подготовки.")]
    [Min(0f)]
    private float _finishToFullDuration = 0.45f;

    [SerializeField]
    [Tooltip("Пауза после 100% перед скрытием экрана.")]
    [Min(0f)]
    private float _completeHoldDuration = 0.18f;

    [SerializeField]
    [Tooltip("Кривая фейкового прогресса. По X время от 0 до 1, по Y прогресс от 0 до 1, который затем умножается на потолок.")]
    private AnimationCurve _fakeProgressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Асинхронная подготовка")]
    [SerializeField]
    [Tooltip("Какие ассеты прогревать перед открытием story screen: только обложку, сохранённую/первую главу или все главы истории.")]
    private StoryStartLoadingAssetScope _assetScope = StoryStartLoadingAssetScope.SavedOrFirstChapter;

    [SerializeField]
    [Tooltip("Если включено, скрипт прогревает текстуру обложки истории до показа 100%. Это обязательная часть, если обложка большая.")]
    private bool _preloadCoverTexture = true;

    [SerializeField]
    [Tooltip("Если включено, скрипт прогревает найденные Sprite/Texture из графа истории, JSON-библиотеки, персонажей и гардероба.")]
    private bool _preloadStoryTextures = true;

    [SerializeField]
    [Tooltip("Если включено, AudioClip пытаются загрузить AudioData заранее. Для больших треков это уменьшает паузу на первом запуске сцены.")]
    private bool _preloadAudioData = true;

    [SerializeField]
    [Tooltip("Если у Texture2D включён mipmap streaming, скрипт запросит лучший mip-level и подождёт его загрузки ограниченное время.")]
    private bool _waitForTextureStreaming = true;

    [SerializeField]
    [Tooltip("Максимальное ожидание одной streaming-текстуры. Нужен предел, чтобы загрузочный экран не завис из-за одной проблемной текстуры.")]
    [Min(0f)]
    private float _textureStreamingTimeout = 0.35f;

    [SerializeField]
    [Tooltip("Сколько ассетов прогревать за один кадр. 1-2 мягче для слабых устройств, 4-8 быстрее для мощных.")]
    [Min(1)]
    private int _assetsPerFrame = 2;

    [SerializeField]
    [Tooltip("Дополнительные пути внутри папок Resources, которые нужно загрузить через Resources.LoadAsync. Пиши путь без расширения, например Stories/MyStory/bg_01.")]
    private string[] _extraResourcesPaths = Array.Empty<string>();

    [Header("Анимации")]
    [SerializeField]
    [Tooltip("Использовать unscaled time для анимаций и прогресса. Включи, если меню может ставить Time.timeScale = 0.")]
    private bool _useUnscaledTime = true;

    [SerializeField]
    [Tooltip("Длительность плавного появления загрузочного экрана.")]
    [Min(0f)]
    private float _showDuration = 0.28f;

    [SerializeField]
    [Tooltip("Длительность плавного скрытия загрузочного экрана.")]
    [Min(0f)]
    private float _hideDuration = 0.24f;

    [SerializeField]
    [Tooltip("Ease появления экрана.")]
    private Ease _showEase = Ease.OutQuart;

    [SerializeField]
    [Tooltip("Ease скрытия экрана.")]
    private Ease _hideEase = Ease.InQuart;

    [SerializeField]
    [Tooltip("RectTransform обложки для вступительной анимации масштаба. Если пусто, будет взят RectTransform у Cover Image.")]
    private RectTransform _coverAnimatedRoot;

    [SerializeField]
    [Tooltip("Начальный масштаб обложки при появлении. Например 1.04 даёт мягкий zoom-out к нормальному размеру.")]
    [Min(0.01f)]
    private float _coverStartScale = 1.04f;

    [SerializeField]
    [Tooltip("Длительность вступительного движения обложки.")]
    [Min(0f)]
    private float _coverIntroDuration = 0.55f;

    [SerializeField]
    [Tooltip("Ease вступительного движения обложки.")]
    private Ease _coverIntroEase = Ease.OutCubic;

    [SerializeField]
    [Tooltip("RectTransform спиннера или декоративного элемента, который будет вращаться во время загрузки.")]
    private RectTransform _spinner;

    [SerializeField]
    [Tooltip("Скорость вращения спиннера в градусах за секунду.")]
    private float _spinnerDegreesPerSecond = -180f;

    [SerializeField]
    [Tooltip("Дополнительные элементы, которым нужен мягкий пульс масштаба во время загрузки.")]
    private RectTransform[] _pulseTargets = Array.Empty<RectTransform>();

    [SerializeField]
    [Tooltip("Множитель пульса. 1.03 означает увеличение на 3%.")]
    [Min(1f)]
    private float _pulseScale = 1.025f;

    [SerializeField]
    [Tooltip("Длительность половины пульса.")]
    [Min(0.05f)]
    private float _pulseHalfDuration = 0.8f;

    private readonly List<Tween> _loopTweens = new List<Tween>();
    private Coroutine _showRoutine;
    private StoryStartLoadingProgressModel _progressModel;
    private bool _skipHideOnAwakeOnce;
    private RenderTexture _coverRenderTexture;
    private bool _coverVideoPreparedHandlerRegistered;
    private VideoPlayer _registeredCoverVideoPlayer;
    private RectTransform _coverVideoSnapshotRect;
    private Vector2 _coverVideoBaseAnchorMin;
    private Vector2 _coverVideoBaseAnchorMax;
    private Vector2 _coverVideoBaseOffsetMin;
    private Vector2 _coverVideoBaseOffsetMax;
    private Vector2 _coverVideoBasePivot;
    private Vector2 _coverVideoBaseAnchoredPosition;
    private Vector2 _coverVideoBaseSize;
    private Vector3 _coverVideoBaseScale = Vector3.one;
    private float _coverVideoBaseRotationZ;
    private bool _coverVideoRawImageAutoCreated;
    private readonly Dictionary<RectTransform, Vector3> _pulseOriginalScales = new Dictionary<RectTransform, Vector3>();
    private CancellationTokenSource _loadingMediaCancellation;
    private IStoryLoadingMediaService _loadingMediaService;
    private IStoryLoadingMediaPolicy _loadingMediaPolicy;
    private IStoryStartAssetPreloadService _assetPreloadService;
    private IStoryStartPreloadAssetCollector _assetCollector;
    private IStoryStartVideoCoverLayoutPolicy _videoCoverLayoutPolicy;
    private IStoryStartLoadingFlow _loadingFlow;
    private StoryLoadingMediaLease _activeLoadingMedia;

    public bool IsVisible => ResolveRoot() != null && ResolveRoot().activeInHierarchy;

    [Inject]
    public void Construct(
        IStoryLoadingMediaService loadingMediaService,
        IStoryLoadingMediaPolicy loadingMediaPolicy,
        IStoryStartAssetPreloadService assetPreloadService,
        IStoryStartPreloadAssetCollector assetCollector,
        IStoryStartVideoCoverLayoutPolicy videoCoverLayoutPolicy,
        IStoryStartLoadingFlow loadingFlow)
    {
        if (loadingMediaService != null)
            _loadingMediaService = loadingMediaService;

        if (loadingMediaPolicy != null)
            _loadingMediaPolicy = loadingMediaPolicy;

        if (assetPreloadService != null)
            _assetPreloadService = assetPreloadService;

        if (assetCollector != null)
            _assetCollector = assetCollector;

        if (videoCoverLayoutPolicy != null)
            _videoCoverLayoutPolicy = videoCoverLayoutPolicy;

        if (loadingFlow != null)
            _loadingFlow = loadingFlow;
    }

    private void Awake()
    {
        ResolveRoot();
        ResolveCanvasGroup();
        ResolveCoverAnimatedRoot();

        if (_hideOnAwake && !_skipHideOnAwakeOnce)
            HideImmediate();

        _skipHideOnAwakeOnce = false;
    }

    private void OnValidate()
    {
        _fakeProgressCeiling = Mathf.Clamp(_fakeProgressCeiling, 0.1f, 0.99f);
        _minVisibleDuration = Mathf.Max(0f, _minVisibleDuration);
        _fakeProgressDuration = Mathf.Max(0.05f, _fakeProgressDuration);
        _progressCatchUpSpeed = Mathf.Max(0.01f, _progressCatchUpSpeed);
        _finishToFullDuration = Mathf.Max(0f, _finishToFullDuration);
        _completeHoldDuration = Mathf.Max(0f, _completeHoldDuration);
        _textureStreamingTimeout = Mathf.Max(0f, _textureStreamingTimeout);
        _assetsPerFrame = Mathf.Max(1, _assetsPerFrame);
        _showDuration = Mathf.Max(0f, _showDuration);
        _hideDuration = Mathf.Max(0f, _hideDuration);
        _coverStartScale = Mathf.Max(0.01f, _coverStartScale);
        _coverIntroDuration = Mathf.Max(0f, _coverIntroDuration);
        _pulseScale = Mathf.Max(1f, _pulseScale);
        _pulseHalfDuration = Mathf.Max(0.05f, _pulseHalfDuration);
        _extraResourcesPaths ??= Array.Empty<string>();
        _pulseTargets ??= Array.Empty<RectTransform>();
    }

    private void OnDisable()
    {
        StopRunningRoutines();
        StopLoopAnimations();
        StopAnimatedCover();
        ReleaseActiveLoadingMedia();
    }

    private void OnDestroy()
    {
        StopRunningRoutines();
        StopLoopAnimations();
        StopAnimatedCover();
        ReleaseActiveLoadingMedia();
        UnregisterCoverVideoPreparedHandler();

        ReleaseCoverRenderTexture();
    }

    public void Show(GameData data, Action onComplete)
    {
        _skipHideOnAwakeOnce = true;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        _skipHideOnAwakeOnce = false;

        if (!enabled)
            enabled = true;

        StopRunningRoutines();
        _showRoutine = StartCoroutine(ShowRoutine(data, onComplete));
    }

    public IEnumerator ShowAndWait(GameData data)
    {
        bool completed = false;
        Show(data, () => completed = true);

        while (!completed)
            yield return null;
    }

    public void HideImmediate()
    {
        StopRunningRoutines();
        StopLoopAnimations();
        StopAnimatedCover();
        ReleaseActiveLoadingMedia();

        GameObject root = ResolveRoot();
        CanvasGroup group = ResolveCanvasGroup();

        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        ApplyProgress(0f);
        if (root != null && _deactivateRootOnHide)
            root.SetActive(false);
    }

    private IEnumerator ShowRoutine(GameData data, Action onComplete)
    {
        CancellationToken loadingMediaToken = BeginLoadingMediaScope();
        GameObject root = ResolveRoot();
        CanvasGroup group = ResolveCanvasGroup();
        RectTransform coverRoot = ResolveCoverAnimatedRoot();
        Vector3 coverBaseScale = coverRoot != null ? coverRoot.localScale : Vector3.one;

        if (root == null)
        {
            SafeInvoke(onComplete);
            yield break;
        }

        if (group != null)
        {
            group.DOKill(false);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        root.SetActive(true);
        PrepareContent(data, null);

        if (group != null)
        {
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        if (coverRoot != null)
        {
            coverRoot.DOKill(false);
            coverRoot.localScale = coverBaseScale * _coverStartScale;
        }

        ApplyProgress(0f);
        ApplyStatus(_initialStatusText);

        StartLoopAnimations();
        PlayShowAnimation(group, coverRoot, coverBaseScale);

        float startTime = Now;
        bool preloadCompleted = false;
        StoryStartLoadingProgressModel progress = CreateProgressModel();

        RunPreloadFlowAsync(data, loadingMediaToken, () => preloadCompleted = true).Forget();

        while (!preloadCompleted || Now - startTime < _minVisibleDuration)
        {
            float elapsed = Now - startTime;
            ApplyProgressSnapshot(progress.TickLoading(elapsed, DeltaTime));
            yield return null;
        }

        yield return FinishProgressRoutine(progress.VisibleProgress);

        if (_completeHoldDuration > 0f)
            yield return Wait(_completeHoldDuration);

        if (_invokeCompleteBeforeHide)
        {
            SafeInvoke(onComplete);
            onComplete = null;
            yield return null;
        }

        yield return HideRoutine(group);
        StopAnimatedCover();
        ReleaseActiveLoadingMedia();
        _showRoutine = null;
        SafeInvoke(onComplete);

        if (_deactivateRootOnHide && root != null)
            root.SetActive(false);
    }

    private async UniTaskVoid RunPreloadFlowAsync(GameData data, CancellationToken cancellationToken, Action onComplete)
    {
        try
        {
            await ResolveLoadingFlow().RunAsync(
                CreateLoadingFlowRequest(data),
                new StoryStartPreloadProgressReporter(ApplyPreloadProgress),
                this,
                cancellationToken);

            if (!cancellationToken.IsCancellationRequested)
                ApplyProgressSnapshot(ResolveProgressModel().Complete(_completeStatusText));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (!cancellationToken.IsCancellationRequested)
                Debug.LogWarning($"[StoryStartLoadingScreen] Failed to run story preload flow: {exception.Message}", this);
        }
        finally
        {
            SafeInvoke(onComplete);
        }
    }

    bool IStoryStartLoadingFlowObserver.OnLoadingMediaLoaded(GameData data, StoryLoadingMediaLease loadingMedia)
    {
        if (!isActiveAndEnabled)
            return false;

        ApplyCover(data, loadingMedia);
        return true;
    }


    private void ApplyPreloadProgress(StoryStartPreloadProgress progress)
    {
        ResolveProgressModel().Report(progress.NormalizedProgress, progress.Status);
    }

    private IEnumerator FinishProgressRoutine(float fromProgress)
    {
        StoryStartLoadingProgressModel progress = ResolveProgressModel();
        float startProgress = Mathf.Clamp01(fromProgress);
        float duration = Mathf.Max(0f, _finishToFullDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += DeltaTime;
            ApplyProgressSnapshot(progress.TickCompleting(startProgress, elapsed, duration, _completeStatusText));
            yield return null;
        }

        ApplyProgressSnapshot(progress.Complete(_completeStatusText));
    }

    private IEnumerator HideRoutine(CanvasGroup group)
    {
        StopLoopAnimations();

        if (group == null || _hideDuration <= 0f)
        {
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            yield break;
        }

        bool completed = false;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.DOFade(0f, _hideDuration)
            .SetEase(_hideEase)
            .SetUpdate(_useUnscaledTime)
            .OnComplete(() => completed = true);

        while (!completed)
            yield return null;

    }

    private void PrepareContent(GameData data, StoryLoadingMediaLease loadingMedia)
    {
        ApplyCover(data, loadingMedia);

        if (_titleText != null)
            _titleText.text = ResolveTitle(data);
    }

    private void ApplyCover(GameData data, StoryLoadingMediaLease loadingMedia)
    {
        StopAnimatedCover();

        if (_activeLoadingMedia != loadingMedia)
            ReleaseActiveLoadingMedia();

        _activeLoadingMedia = loadingMedia;

        StoryLoadingMediaSelection media = ResolveLoadingMediaPolicy().SelectForPresentation(data, loadingMedia);
        Sprite cover = media.CoverSprite;
        if (_coverImage != null)
        {
            _coverImage.preserveAspect = _preserveCoverAspect;
            if (cover != null)
                RuntimeTextureFallback.EnsureImageVisible(_coverImage, cover);
            else
                RuntimeTextureFallback.ApplyImagePlaceholder(_coverImage);
        }

        if (data == null)
            return;

        VideoClip video = media.CoverVideo;
        TextAsset gif = media.CoverGif;

        if (video != null)
        {
            ShowVideoCover(data, video);
        }
        else if (gif != null)
        {
            ShowGifCover(gif);
        }
        else if (cover == null && _fallbackCoverSprite == null)
        {
            Debug.LogWarning($"[StoryStartLoadingScreen] У GameData '{data.name}' нет GameIcon, GameIconVideo и GameIconGif.", data);
        }
    }

    private void ShowVideoCover(GameData data, VideoClip clip)
    {
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
            name = $"{nameof(StoryStartLoadingScreen)} Cover RenderTexture"
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
            Debug.LogWarning($"[StoryStartLoadingScreen] Failed to prepare cover video '{clip.name}': {exception.Message}", this);
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
            Debug.LogWarning($"[StoryStartLoadingScreen] Failed to play cover video: {exception.Message}", this);
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
            _coverVideoRawImage = CreateCoverRawImage("Loading Cover Video");
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

        var request = new StoryStartVideoCoverLayoutRequest(
            new StoryStartVideoCoverBaseLayout(
                _coverVideoBaseSize,
                _coverVideoBaseAnchoredPosition,
                _coverVideoBaseScale,
                _coverVideoBaseRotationZ),
            overrides,
            _stretchVideoToCoverRectOnLoadingScreen,
            Vector2.one,
            _stretchedVideoRotationZ);

        StoryStartVideoCoverLayout layout = ResolveVideoCoverLayoutPolicy().Resolve(request);
        if (layout.Stretch)
        {
            StretchVideoRectForLoading(videoRect, layout.Scale, layout.RotationZ);
            ApplyAnimatedCoverLayering(overrides);
            return;
        }

        RestoreVideoRectSnapshot(videoRect);
        SetRectSize(videoRect, layout.Size);
        videoRect.anchoredPosition = layout.AnchoredPosition;
        videoRect.localScale = layout.Scale;
        SetLocalRotationZ(videoRect, layout.RotationZ);
        ApplyAnimatedCoverLayering(overrides);
    }

    private void TakeVideoRectSnapshot(RectTransform videoRect)
    {
        if (videoRect == null || _coverVideoSnapshotRect == videoRect)
            return;

        _coverVideoSnapshotRect = videoRect;
        _coverVideoBaseAnchorMin = videoRect.anchorMin;
        _coverVideoBaseAnchorMax = videoRect.anchorMax;
        _coverVideoBaseOffsetMin = videoRect.offsetMin;
        _coverVideoBaseOffsetMax = videoRect.offsetMax;
        _coverVideoBasePivot = videoRect.pivot;
        _coverVideoBaseAnchoredPosition = videoRect.anchoredPosition;
        _coverVideoBaseSize = videoRect.rect.size;
        _coverVideoBaseScale = videoRect.localScale;
        _coverVideoBaseRotationZ = StoryStartVideoCoverBaseLayout.NormalizeAngle(videoRect.localEulerAngles.z);
    }

    private void RestoreVideoRectSnapshot(RectTransform videoRect)
    {
        if (videoRect == null || _coverVideoSnapshotRect != videoRect)
            return;

        videoRect.anchorMin = _coverVideoBaseAnchorMin;
        videoRect.anchorMax = _coverVideoBaseAnchorMax;
        videoRect.offsetMin = _coverVideoBaseOffsetMin;
        videoRect.offsetMax = _coverVideoBaseOffsetMax;
        videoRect.pivot = _coverVideoBasePivot;
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

    private void StretchVideoRectForLoading(RectTransform rectTransform, Vector3 stretchScale, float rotationZ)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localScale = new Vector3(stretchScale.x, stretchScale.y, 1f);
        SetLocalRotationZ(rectTransform, rotationZ);
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

    private bool EnsureGifCover()
    {
        if (_coverGifPlayer == null)
        {
            RawImage rawImage = CreateCoverRawImage("Loading Cover GIF");
            if (rawImage != null)
                _coverGifPlayer = rawImage.GetComponent<AnimatedGifPlayer>() ?? rawImage.gameObject.AddComponent<AnimatedGifPlayer>();
        }

        return _coverGifPlayer != null;
    }

    private RawImage CreateCoverRawImage(string objectName)
    {
        if (_coverImage == null)
        {
            Debug.LogWarning("[StoryStartLoadingScreen] Cover Image не назначен, поэтому видео/GIF обложку создать нельзя.", this);
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

        if (_registeredCoverVideoPlayer != null && _registeredCoverVideoPlayer != _coverVideoPlayer)
            UnregisterCoverVideoPreparedHandler();

        if (_coverVideoPreparedHandlerRegistered && _registeredCoverVideoPlayer == _coverVideoPlayer)
            return;

        _coverVideoPlayer.prepareCompleted += OnCoverVideoPrepared;
        _registeredCoverVideoPlayer = _coverVideoPlayer;
        _coverVideoPreparedHandlerRegistered = true;
    }

    private void UnregisterCoverVideoPreparedHandler()
    {
        if (_coverVideoPreparedHandlerRegistered && _registeredCoverVideoPlayer != null)
            _registeredCoverVideoPlayer.prepareCompleted -= OnCoverVideoPrepared;

        _registeredCoverVideoPlayer = null;
        _coverVideoPreparedHandlerRegistered = false;
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

        if (_coverVideoPlayer != null && (_coverVideoPlayer.isPlaying || _coverVideoPlayer.isPrepared))
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
    private void ApplyProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        ApplyMuipProgress(progress);

        if (_progressSlider != null)
            _progressSlider.value = progress;

        if (_progressFillImage != null)
            _progressFillImage.fillAmount = progress;

        if (_percentText != null)
            _percentText.text = Mathf.RoundToInt(progress * 100f) + "%";
    }

    private void ApplyStatus(string status)
    {
        string resolvedStatus = string.IsNullOrWhiteSpace(status) ? _initialStatusText : status;

        if (_statusText != null)
            _statusText.text = resolvedStatus;
    }

    private void ApplyMuipProgress(float progress)
    {
        if (_progressBar == null)
            return;

        if (_disableMuipAutoProgress)
            _progressBar.isOn = false;

        if (Mathf.Approximately(_progressBar.maxValue, 0f))
            _progressBar.maxValue = 100f;

        float value = Mathf.Lerp(_progressBar.minValue, _progressBar.maxValue, progress);
        _progressBar.currentPercent = value;

        if (_progressBar.loadingBar != null && _progressBar.textPercent != null)
        {
            _progressBar.UpdateUI();
            return;
        }

        if (_progressBar.loadingBar != null)
            _progressBar.loadingBar.fillAmount = Mathf.Clamp01(value / _progressBar.maxValue);

        if (_progressBar.textPercent != null)
            _progressBar.textPercent.text = FormatMuipProgressText(_progressBar, value);

        if (_progressBar.eventSource != null)
            _progressBar.eventSource.value = value;
    }

    private static string FormatMuipProgressText(ProgressBar progressBar, float value)
    {
        string text = value.ToString("F" + Mathf.Clamp(progressBar.decimals, 0, 5));

        if (progressBar.addSuffix)
            text += progressBar.suffix;

        if (progressBar.addPrefix)
            text = progressBar.prefix + text;

        return text;
    }

    private void PlayShowAnimation(CanvasGroup group, RectTransform coverRoot, Vector3 coverBaseScale)
    {
        if (group != null)
        {
            if (_showDuration <= 0f)
                group.alpha = 1f;
            else
                group.DOFade(1f, _showDuration).SetEase(_showEase).SetUpdate(_useUnscaledTime);
        }

        if (coverRoot == null)
            return;

        if (_coverIntroDuration <= 0f)
        {
            coverRoot.localScale = coverBaseScale;
            return;
        }

        coverRoot.DOScale(coverBaseScale, _coverIntroDuration)
            .SetEase(_coverIntroEase)
            .SetUpdate(_useUnscaledTime);
    }

    private void StartLoopAnimations()
    {
        StopLoopAnimations();

        if (_spinner != null && !Mathf.Approximately(_spinnerDegreesPerSecond, 0f))
        {
            Tween spinnerTween = _spinner
                .DOLocalRotate(new Vector3(0f, 0f, _spinnerDegreesPerSecond), 1f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental)
                .SetUpdate(_useUnscaledTime);
            _loopTweens.Add(spinnerTween);
        }

        if (_pulseTargets == null)
            return;

        for (int i = 0; i < _pulseTargets.Length; i++)
        {
            RectTransform target = _pulseTargets[i];
            if (target == null)
                continue;

            if (!_pulseOriginalScales.ContainsKey(target))
                _pulseOriginalScales.Add(target, target.localScale);

            Vector3 targetScale = _pulseOriginalScales[target] * _pulseScale;
            Tween pulseTween = target
                .DOScale(targetScale, _pulseHalfDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(_useUnscaledTime);
            _loopTweens.Add(pulseTween);
        }
    }

    private void StopLoopAnimations()
    {
        for (int i = 0; i < _loopTweens.Count; i++)
            _loopTweens[i]?.Kill(false);

        _loopTweens.Clear();

        if (_spinner != null)
            _spinner.DOKill(false);

        if (_pulseTargets != null)
        {
            for (int i = 0; i < _pulseTargets.Length; i++)
            {
                if (_pulseTargets[i] != null)
                {
                    _pulseTargets[i].DOKill(false);
                    if (_pulseOriginalScales.TryGetValue(_pulseTargets[i], out Vector3 originalScale))
                        _pulseTargets[i].localScale = originalScale;
                }
            }
        }

        _pulseOriginalScales.Clear();
    }

    private void StopRunningRoutines()
    {
        CancelLoadingMediaScope();

        if (_showRoutine != null)
        {
            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }

    }

    private CancellationToken BeginLoadingMediaScope()
    {
        CancelLoadingMediaScope();
        _loadingMediaCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        return _loadingMediaCancellation.Token;
    }

    private void CancelLoadingMediaScope()
    {
        if (_loadingMediaCancellation == null)
            return;

        try
        {
            if (!_loadingMediaCancellation.IsCancellationRequested)
                _loadingMediaCancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _loadingMediaCancellation.Dispose();
        _loadingMediaCancellation = null;
    }

    private void ReleaseActiveLoadingMedia()
    {
        if (_activeLoadingMedia == null)
            return;

        _activeLoadingMedia.Dispose();
        _activeLoadingMedia = null;
    }

    private IStoryLoadingMediaService ResolveLoadingMediaService()
    {
        if (_loadingMediaService == null)
            _loadingMediaService = StoryLoadingMediaServices.Shared;

        return _loadingMediaService;
    }

    private IStoryLoadingMediaPolicy ResolveLoadingMediaPolicy()
    {
        if (_loadingMediaPolicy == null)
            _loadingMediaPolicy = StoryLoadingMediaPolicies.Shared;

        return _loadingMediaPolicy;
    }

    private IStoryStartAssetPreloadService ResolveAssetPreloadService()
    {
        if (_assetPreloadService == null)
            _assetPreloadService = StoryStartAssetPreloadServices.Shared;

        return _assetPreloadService;
    }

    private IStoryStartPreloadAssetCollector ResolveAssetCollector()
    {
        if (_assetCollector == null)
            _assetCollector = StoryStartPreloadAssetCollectors.Shared;

        return _assetCollector;
    }

    private IStoryStartVideoCoverLayoutPolicy ResolveVideoCoverLayoutPolicy()
    {
        if (_videoCoverLayoutPolicy == null)
            _videoCoverLayoutPolicy = StoryStartVideoCoverLayoutPolicies.Shared;

        return _videoCoverLayoutPolicy;
    }

    private IStoryStartLoadingFlow ResolveLoadingFlow()
    {
        if (_loadingFlow == null)
        {
            _loadingFlow = new StoryStartLoadingFlow(
                ResolveLoadingMediaService(),
                ResolveLoadingMediaPolicy(),
                ResolveAssetCollector(),
                ResolveAssetPreloadService());
        }

        return _loadingFlow;
    }

    private StoryStartLoadingFlowRequest CreateLoadingFlowRequest(GameData data)
    {
        return new StoryStartLoadingFlowRequest(
            data,
            _assetScope,
            CollectValidResourcesPaths(),
            _preloadCoverTexture,
            _preloadStoryTextures,
            _preloadAudioData,
            _waitForTextureStreaming,
            _useUnscaledTime,
            _textureStreamingTimeout,
            _assetsPerFrame,
            _fakeProgressCeiling,
            "Loading story media",
            "Р—Р°РіСЂСѓР¶Р°РµРј РѕР±Р»РѕР¶РєСѓ",
            "Р“РѕС‚РѕРІРёРј С‚РµРєСЃС‚СѓСЂС‹",
            "Р“РѕС‚РѕРІРёРј Р·РІСѓРє",
            "Р“РѕС‚РѕРІРёРј РІРёРґРµРѕ",
            "Р“РѕС‚РѕРІРёРј РґР°РЅРЅС‹Рµ",
            "РџРѕРґРіСЂСѓР¶Р°РµРј Resources");
    }

    private StoryStartLoadingProgressModel CreateProgressModel()
    {
        _progressModel = new StoryStartLoadingProgressModel(
            _fakeProgressCeiling,
            _fakeProgressDuration,
            _progressCatchUpSpeed,
            _fakeProgressCurve,
            _initialStatusText);
        return _progressModel;
    }

    private StoryStartLoadingProgressModel ResolveProgressModel()
    {
        return _progressModel ?? CreateProgressModel();
    }

    private void ApplyProgressSnapshot(StoryStartLoadingProgressSnapshot snapshot)
    {
        ApplyProgress(snapshot.VisibleProgress);
        ApplyStatus(snapshot.Status);
    }

    private float Now => _useUnscaledTime ? Time.unscaledTime : Time.time;
    private float DeltaTime => _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    private IEnumerator Wait(float seconds)
    {
        float startedAt = Now;
        while (Now - startedAt < seconds)
            yield return null;
    }

    private GameObject ResolveRoot()
    {
        if (_root == null)
            _root = gameObject;

        return _root;
    }

    private CanvasGroup ResolveCanvasGroup()
    {
        if (_canvasGroup == null)
        {
            GameObject root = ResolveRoot();
            if (root != null)
                _canvasGroup = root.GetComponent<CanvasGroup>();
        }

        if (_canvasGroup == null && ResolveRoot() != null)
            _canvasGroup = ResolveRoot().AddComponent<CanvasGroup>();

        return _canvasGroup;
    }

    private RectTransform ResolveCoverAnimatedRoot()
    {
        if (_coverAnimatedRoot == null && _coverImage != null)
            _coverAnimatedRoot = _coverImage.rectTransform;

        return _coverAnimatedRoot;
    }

    private List<string> CollectValidResourcesPaths()
    {
        var paths = new List<string>();
        if (_extraResourcesPaths == null)
            return paths;

        for (int i = 0; i < _extraResourcesPaths.Length; i++)
        {
            string path = _extraResourcesPaths[i];
            if (!string.IsNullOrWhiteSpace(path))
                paths.Add(path.Trim());
        }

        return paths;
    }

    private static string ResolveTitle(GameData data)
    {
        if (data == null)
            return "";

        if (!string.IsNullOrWhiteSpace(data.GameName))
            return data.GameName;

        if (data.Story != null && !string.IsNullOrWhiteSpace(data.Story.StoryName))
            return data.Story.StoryName;

        return data.name;
    }

    private void SafeInvoke(Action callback)
    {
        try
        {
            callback?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[StoryStartLoadingScreen] Completion callback failed: {exception.Message}", this);
        }
    }
}
