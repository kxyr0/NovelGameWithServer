using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;

#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class MenuController : MonoBehaviour
{
    private static readonly IReadOnlyList<GameData> EmptyGames = System.Array.Empty<GameData>();

    [Header("Story Catalog")]
    [SerializeField] private GameCatalog _gameCatalog;

    [Header("Story List")]
    [SerializeField]
    [FormerlySerializedAs("GameButtonPrefab")]
    [InspectorName("Legacy GameButtonView")]
    [Tooltip("Старый вариант: prefab, на котором GameButtonView стоит на корне. Можно оставить пустым, если используется Prefab корня карточки.")]
    private GameButtonView _gameButtonPrefab;

    [SerializeField]
    [InspectorName("Prefab корня карточки")]
    [Tooltip("Сюда можно назначить целый prefab карточки, например ZokroLife. GameButtonView может лежать внутри child, код найдет его после создания.")]
    private GameObject _gameButtonPrefabRoot;

    [SerializeField]
    [FormerlySerializedAs("GamesParent")]
    private Transform _gamesParent;

    [Header("Story Carousel")]
    [SerializeField]
    [InspectorName("Layout карусели")]
    [Tooltip("Отдельный компонент, который управляет offset, rotation, scale и анимацией карточек историй.")]
    private StoryCardCarouselLayout _storyCarouselLayout;

    [SerializeField]
    [Tooltip("Стрелка влево для переключения выбранной истории. Назначается вручную в инспекторе.")]
    private Button _previousStoryButton;

    [SerializeField]
    [Tooltip("Стрелка вправо для переключения выбранной истории. Назначается вручную в инспекторе.")]
    private Button _nextStoryButton;

    [SerializeField]
    [Tooltip("Если включено, список историй строится как окно вокруг выбранной истории, а стрелки меняют выбранный индекс.")]
    private bool _storyCarouselEnabled = true;

    [SerializeField]
    [Tooltip("Зацикливать стрелки: после последней истории переходить к первой и наоборот.")]
    private bool _storyCarouselWrap = true;

    [SerializeField, Min(1)]
    [Tooltip("Сколько карточек одновременно строить вокруг выбранной истории. Для текущего макета удобно 3: левая, центральная и правая.")]
    private int _storyCarouselVisibleSlots = 3;

    [SerializeField]
    [Tooltip("Разрешить переключение выбранной истории клавишами LeftArrow/RightArrow на главном экране.")]
    private bool _storyCarouselKeyboardInput = true;

    [Header("History Screen")]
    [SerializeField]
    [Tooltip("Экран деталей выбранной истории. Сюда будут подставляться название, жанр, серии, описание и статы.")]
    private StoryHistoryScreen _storyHistoryScreen;

    [SerializeField]
    [Tooltip("Screen ID detail-экрана истории. На root экрана поставь UIScreenMarker с таким же ID.")]
    private string _historyScreenId = "History_Screen";

    [SerializeField]
    [Tooltip("Если включено, клик по карточке открывает History_Screen, а не запускает историю сразу.")]
    private bool _openHistoryScreenOnStoryClick = true;

    [Header("Panels")]
    [SerializeField]
    [FormerlySerializedAs("StoryManager")]
    private StoryManager _storyManager;

    [Header("Navigation")]
    [SerializeField]
    [FormerlySerializedAs("storyScreenNavigator")]
    private StoryScreenNavigator _storyScreenNavigator;

    [SerializeField]
    private GameObject _navigationRoot;

    [SerializeField]
    private GameObject[] _hideWhileStoryScreenOpen = System.Array.Empty<GameObject>();

    [Header("Settings")]
    [SerializeField]
    [FormerlySerializedAs("settingsPanel")]
    private GameObject _settingsPanel;

    [SerializeField]
    [FormerlySerializedAs("settingsButton")]
    private Button _settingsButton;

    [SerializeField]
    [FormerlySerializedAs("settingsCloseButton")]
    private Button _settingsCloseButton;

    [Header("Bug Report")]
    [SerializeField]
    [FormerlySerializedAs("bugReportButton")]
    private Button _bugReportButton;

    [SerializeField]
    [FormerlySerializedAs("bugReportPanel")]
    private BugReportPanel _bugReportPanel;

    [Header("Exit")]
    [SerializeField]
    [FormerlySerializedAs("exitButton")]
    private Button _exitButton;

    [Header("Audio")]
    [SerializeField]
    private MainMenuMusicPlayer _mainMenuMusicPlayer;

    [Header("Предстартовый экран")]
    [SerializeField]
    private PreStorySetupFlow _preStorySetupFlow;

    [SerializeField]
    private WardrobeHeroSetupPage _preStoryWardrobeSetupPage;

    [SerializeField]
    private WardrobeCategoryTabs _wardrobeCategoryTabs;

    [SerializeField]
    private WardrobeCategoryTabType _wardrobeOpenCategory = WardrobeCategoryTabType.Outfit;

    [SerializeField]
    private string _wardrobeScreenId = "Wardrobe";

    [SerializeField]
    private bool _runPreStorySetupBeforeStory;

    [Header("Стартовая загрузка истории")]
    [SerializeField]
    [Tooltip("Загрузочный экран, который показывается после кнопки Старт на History screen и перед открытием Story screen. Скрипт можно держать на выключенной UI-панели в этой же сцене.")]
    private StoryStartLoadingScreen _storyStartLoadingScreen;

    [SerializeField]
    [Tooltip("Если включено и Story Start Loading Screen назначен или найден в сцене, перед запуском истории будет показан загрузочный экран с обложкой, прогрессом и асинхронной подготовкой ассетов.")]
    private bool _showStoryStartLoadingScreen = true;

    [Header("Name Input")]
    [SerializeField]
    [FormerlySerializedAs("nameInputUI")]
    private PlayerNameInputUI _nameInputUI;

    [Header("Animation")]
    [SerializeField]
    [FormerlySerializedAs("menuCanvasGroup")]
    private CanvasGroup _menuCanvasGroup;

    [SerializeField]
    private GameObject _blackScreen;

    [SerializeField]
    private StoryBlackScreenTransition _storyBlackScreenTransition;

    [Header("Screen Transition Polish")]
    [SerializeField]
    private bool _applyModernScreenTransitionProfile = true;

    [SerializeField]
    private UIScreenTransitionType _menuScreenTransition = UIScreenTransitionType.SlideFade;

    [SerializeField]
    private float _menuScreenTransitionDuration = 0.42f;

    [SerializeField]
    private Ease _menuScreenTransitionEase = Ease.OutQuart;

    [SerializeField]
    private bool _menuScreenTransitionUsesUnscaledTime = true;

    [SerializeField]
    private bool _menuScreenTransitionUsesScreenOrder = true;

    [Header("Popup Motion")]
    [SerializeField]
    private float _popupTransitionDuration = 0.24f;

    [SerializeField]
    private Ease _popupTransitionEase = Ease.OutQuart;

    [SerializeField, Range(0.9f, 1f)]
    private float _popupStartScale = 0.97f;

    private Tween _menuFadeTween;
    private Tween _settingsFadeTween;
    private Coroutine _refreshCatalogRoutine;
    private Coroutine _storyLoadRoutine;
    private readonly Dictionary<GameObject, bool> _storyHiddenObjectStates = new Dictionary<GameObject, bool>();
    private readonly Dictionary<GameData, StoryCardInstance> _storyCardInstances = new Dictionary<GameData, StoryCardInstance>();
    private Vector3 _settingsPanelHomeScale = Vector3.one;
    private bool _settingsPanelHomeScaleCaptured;
    private bool _isStoryScreenOpen;
    private GameData _pendingStoryData;
    private Dictionary<string, int> _pendingStoryInitialStats;
    private GameData _wardrobeContextData;
    private bool _pendingPreStoryIncludesWardrobe;
    private readonly StoryLaunchStateMachine _storyLaunchState = new StoryLaunchStateMachine();
    private IStoryStartLoadingScreen _storyStartLoadingScreenContract;
    private int _selectedGameIndex;

    public GameCatalog GameCatalog => _gameCatalog;
    public IReadOnlyList<GameData> Games => _gameCatalog != null ? _gameCatalog.Games : EmptyGames;
    public GameButtonView GameButtonPrefab => _gameButtonPrefab;
    public StoryCardCarouselLayout StoryCarouselLayout => _storyCarouselLayout;
    public Transform GamesParent => _gamesParent;
    public GameData SelectedGame => ResolveSelectedGame();
    public GameData CurrentStoryContextData => ResolveCurrentStoryContextData();
    public StoryManager StoryManager => _storyManager;
    public StoryScreenNavigator ScreenNavigator => _storyScreenNavigator;
    public GameObject NavigationRoot => _navigationRoot;
    public MainMenuMusicPlayer MainMenuMusicPlayer => _mainMenuMusicPlayer;
    public PreStorySetupFlow PreStorySetupFlow => _preStorySetupFlow;

    [Inject]
    public void Construct(IStoryStartLoadingScreen storyStartLoadingScreen)
    {
        if (storyStartLoadingScreen != null)
            _storyStartLoadingScreenContract = storyStartLoadingScreen;
    }

    private sealed class StoryCardInstance
    {
        public GameData Data;
        public GameButtonView Button;
        public RectTransform Root;
        public GameObject RootObject;
    }

    private void Awake()
    {
        AutoWireReferences();
    }

    private void OnValidate()
    {
        if (_hideWhileStoryScreenOpen == null)
            _hideWhileStoryScreenOpen = System.Array.Empty<GameObject>();

        if (string.IsNullOrWhiteSpace(_wardrobeScreenId))
            _wardrobeScreenId = "Wardrobe";

        _storyCarouselVisibleSlots = Mathf.Max(1, _storyCarouselVisibleSlots);
        _menuScreenTransitionDuration = Mathf.Max(0f, _menuScreenTransitionDuration);
        _popupTransitionDuration = Mathf.Max(0f, _popupTransitionDuration);
        _popupStartScale = Mathf.Clamp(_popupStartScale, 0.9f, 1f);
        ClampSelectedGameIndex();
        AutoWireReferences();
    }

    private void Start()
    {
        ApplyMenuScreenTransitionProfile();
        _storyScreenNavigator?.PrepareInitialState();
        if (_menuCanvasGroup == null && _storyScreenNavigator != null)
            _menuCanvasGroup = _storyScreenNavigator.MenuCanvasGroup;

        if (_settingsButton != null)
            _settingsButton.onClick.AddListener(OpenSettings);

        if (_settingsCloseButton != null)
            _settingsCloseButton.onClick.AddListener(CloseSettings);

        if (_bugReportButton != null)
            _bugReportButton.onClick.AddListener(OpenBugReport);

        if (_exitButton != null)
            _exitButton.onClick.AddListener(QuitGame);

        BindStoryCarouselButtons();
        SetupStoryCarouselSwipe();

        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);

        FadeMenuIn(0.5f);
        _mainMenuMusicPlayer?.PlayMusic();
        BuildGameList();
        StoryCatalogRuntimeDiagnostics.LogCatalog(_gameCatalog, "menu_start", this);

        if (NetworkManager.Instance != null)
            _refreshCatalogRoutine = StartCoroutine(RefreshCatalogAndRebuild());

    }

    private void OnDestroy()
    {
        if (_settingsButton != null)
            _settingsButton.onClick.RemoveListener(OpenSettings);

        if (_settingsCloseButton != null)
            _settingsCloseButton.onClick.RemoveListener(CloseSettings);

        if (_bugReportButton != null)
            _bugReportButton.onClick.RemoveListener(OpenBugReport);

        if (_exitButton != null)
            _exitButton.onClick.RemoveListener(QuitGame);

        UnbindStoryCarouselButtons();
        ReleaseStoryCarouselSwipe();

        if (_refreshCatalogRoutine != null)
            StopCoroutine(_refreshCatalogRoutine);

        if (_storyLoadRoutine != null)
            StopCoroutine(_storyLoadRoutine);

        _menuFadeTween?.Kill();
        _settingsFadeTween?.Kill();
    }

    private void Update()
    {
        if (!_storyCarouselKeyboardInput || !IsStoryCarouselEnabled() || _isStoryScreenOpen)
            return;

        if (!_storyLaunchState.IsIdle)
            return;

        if (Input.GetKeyDown(KeyCode.LeftArrow))
            SelectPreviousStory();
        else if (Input.GetKeyDown(KeyCode.RightArrow))
            SelectNextStory();
    }

    private void BuildGameList()
    {
        if (_gamesParent == null)
        {
            Debug.LogError("[MenuController] GamesParent is not assigned.", this);
            return;
        }

        if (_gameButtonPrefabRoot == null && _gameButtonPrefab == null)
        {
            Debug.LogError("[MenuController] Story card prefab is not assigned. Set 'Prefab корня карточки' or legacy GameButtonView.", this);
            return;
        }

        GameData selectedBeforeRebuild = ResolveSelectedGame();
        bool useCarouselLayout = _storyCarouselLayout != null;
        if (!useCarouselLayout)
            DestroyStoryCardInstancesAndChildren();

        if (_gameCatalog == null)
        {
            Debug.LogWarning("[MenuController] GameCatalog is not assigned.", this);
            if (useCarouselLayout)
                HideUnusedStoryCardInstances(null);

            UpdateStoryCarouselButtons(0);
            return;
        }

        List<GameData> games = BuildAvailableGameList();
        RestoreSelectedGameIndex(games, selectedBeforeRebuild);
        _storyCarouselLayout?.PrepareParent(_gamesParent);

        int selectedIndex = games.Count > 0 ? Mathf.Clamp(_selectedGameIndex, 0, games.Count - 1) : 0;
        List<StoryCarouselVisibleCard> visibleCards = BuildVisibleStoryCards(games);
        var visibleData = useCarouselLayout ? new HashSet<GameData>() : null;
        var drawTargets = useCarouselLayout ? new List<StoryCarouselDrawTarget>() : null;
        foreach (StoryCarouselVisibleCard visibleCard in visibleCards)
        {
            GameData data = visibleCard.Data;
            if (data == null)
                continue;

            try
            {
                StoryCardInstance cardInstance = useCarouselLayout
                    ? GetOrCreateStoryCardInstance(data)
                    : CreateStoryCardInstance(data);

                if (cardInstance == null)
                    continue;

                if (visibleData != null)
                    visibleData.Add(data);

                GameButtonView button = cardInstance.Button;
                if (button == null)
                    continue;

                bool selected = visibleCard.SourceIndex == selectedIndex;
                if (cardInstance.RootObject != null)
                    cardInstance.RootObject.SetActive(true);

                button.Setup(data, () => HandleStoryCardClicked(data));
                button.SetSelected(selected);
                _storyCarouselLayout?.ApplyToCard(cardInstance.Root, visibleCard.SlotOffset, selected, false, data);

                if (drawTargets != null && cardInstance.Root != null)
                {
                    drawTargets.Add(new StoryCarouselDrawTarget(
                        cardInstance.Root,
                        visibleCard.SlotOffset,
                        selected,
                        visibleCard.SourceIndex,
                        drawTargets.Count));
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[MenuController] Failed to build game button: {exception.Message}", this);
            }
        }

        if (useCarouselLayout)
            _storyCarouselLayout?.ApplySiblingOrder(drawTargets);

        if (useCarouselLayout)
            HideUnusedStoryCardInstances(visibleData);

        UpdateStoryCarouselButtons(games.Count);
    }

    private StoryCardInstance GetOrCreateStoryCardInstance(GameData data)
    {
        if (data == null)
            return null;

        if (_storyCardInstances.TryGetValue(data, out StoryCardInstance instance) && IsStoryCardInstanceValid(instance))
            return instance;

        instance = CreateStoryCardInstance(data);
        if (instance != null)
            _storyCardInstances[data] = instance;

        return instance;
    }

    private StoryCardInstance CreateStoryCardInstance(GameData data)
    {
        GameButtonView button = InstantiateGameButtonView(out RectTransform cardRoot, out GameObject rootObject);
        if (button == null)
            return null;

        ApplyStoryCardObjectNames(data, rootObject, button);

        return new StoryCardInstance
        {
            Data = data,
            Button = button,
            Root = cardRoot,
            RootObject = rootObject != null ? rootObject : button.gameObject
        };
    }

    private static bool IsStoryCardInstanceValid(StoryCardInstance instance)
    {
        return instance != null && instance.Button != null && instance.RootObject != null;
    }

    private void HideUnusedStoryCardInstances(HashSet<GameData> visibleData)
    {
        var keysToRemove = new List<GameData>();
        foreach (KeyValuePair<GameData, StoryCardInstance> pair in _storyCardInstances)
        {
            StoryCardInstance instance = pair.Value;
            if (!IsStoryCardInstanceValid(instance))
            {
                keysToRemove.Add(pair.Key);
                continue;
            }

            bool visible = visibleData != null && visibleData.Contains(pair.Key);
            if (!visible)
            {
                instance.Button.SetSelected(false);
                if (instance.Root != null)
                    instance.Root.DOKill(false);

                instance.RootObject.SetActive(false);
            }
        }

        for (int i = 0; i < keysToRemove.Count; i++)
            _storyCardInstances.Remove(keysToRemove[i]);
    }

    private void DestroyStoryCardInstancesAndChildren()
    {
        foreach (Transform item in _gamesParent)
            Destroy(item.gameObject);

        _storyCardInstances.Clear();
    }

    private GameButtonView InstantiateGameButtonView(out RectTransform cardRoot, out GameObject rootObject)
    {
        cardRoot = null;
        rootObject = null;
        GameObject instance = null;

        if (_gameButtonPrefabRoot != null)
        {
            instance = Instantiate(_gameButtonPrefabRoot, _gamesParent);
        }
        else if (_gameButtonPrefab != null)
        {
            instance = Instantiate(_gameButtonPrefab.gameObject, _gamesParent);
        }

        if (instance == null)
            return null;

        rootObject = instance;
        cardRoot = instance.transform as RectTransform;
        GameButtonView button = instance.GetComponentInChildren<GameButtonView>(true);
        if (button != null)
        {
            if (cardRoot == null)
                cardRoot = button.transform as RectTransform;

            return button;
        }

        Debug.LogWarning($"[MenuController] Story card prefab '{instance.name}' has no GameButtonView in root or children.", instance);
        Destroy(instance);
        cardRoot = null;
        rootObject = null;
        return null;
    }

    private static void ApplyStoryCardObjectNames(GameData data, GameObject rootObject, GameButtonView button)
    {
        string storyName = ResolveStoryDisplayName(data);
        if (rootObject != null)
            rootObject.name = $"StoryCard - {storyName}";

        if (button != null)
            button.ApplyObjectNames(storyName);
    }

    private static string ResolveStoryDisplayName(GameData data)
    {
        if (data == null)
            return "Без названия";

        if (!string.IsNullOrWhiteSpace(data.GameName))
            return data.GameName.Trim();

        if (data.Story != null && !string.IsNullOrWhiteSpace(data.Story.StoryName))
            return data.Story.StoryName.Trim();

        return string.IsNullOrWhiteSpace(data.name) ? "Без названия" : data.name.Trim();
    }

    public void SelectPreviousStory()
    {
        SelectStoryOffset(-1);
    }

    public void SelectNextStory()
    {
        SelectStoryOffset(1);
    }

    public void SelectStoryAtIndex(int index)
    {
        List<GameData> games = BuildAvailableGameList();
        if (games.Count == 0)
        {
            _selectedGameIndex = 0;
            BuildGameList();
            return;
        }

        int nextIndex = Mathf.Clamp(index, 0, games.Count - 1);
        if (_selectedGameIndex == nextIndex)
        {
            UpdateStoryCarouselButtons(games.Count);
            return;
        }

        _selectedGameIndex = nextIndex;
        BuildGameList();
    }

    public void RefreshStoryCarousel()
    {
        BuildGameList();
    }

    public void OpenHistoryScreenFor(GameData data)
    {
        if (data == null)
            return;

        SelectStoryData(data);

        AppLogger.Info(
            AppLogCategory.Menu,
            nameof(MenuController),
            nameof(OpenHistoryScreenFor),
            "[MENU][HISTORY] Opening story history/details screen.",
            BuildGameDataMetadata(data, "open_history"));

        if (_storyHistoryScreen == null)
            _storyHistoryScreen = FindObjectOfType<StoryHistoryScreen>(true);

        if (_storyHistoryScreen != null)
            _storyHistoryScreen.Configure(data, this);

        if (_storyScreenNavigator == null)
        {
            Debug.LogWarning("MenuController: StoryScreenNavigator is not assigned.", this);
            return;
        }

        string screenId = UIScreenState.NormalizeScreenId(_historyScreenId);
        if (screenId.Length == 0)
        {
            Debug.LogWarning("MenuController: History screen id is empty.", this);
            return;
        }

        if (!_storyScreenNavigator.OpenScreen(screenId))
            Debug.LogWarning($"MenuController: failed to open history screen '{screenId}'. Add UIScreenMarker with this id or add the screen to StoryScreenNavigator.", this);
    }

    public bool PrepareSelectedStoryContext(GameData data)
    {
        if (data == null)
            return false;

        SelectStoryData(data);

        if (_storyManager == null)
            _storyManager = FindObjectOfType<StoryManager>(true);

        if (_storyManager == null)
        {
            Debug.LogWarning("[MenuController] Cannot prepare story context: StoryManager is not assigned.", this);
            return false;
        }

        if (data.Story == null)
        {
            Debug.LogWarning($"[MenuController] Cannot prepare story context: GameData '{data.name}' has no StoryData.", data);
            return false;
        }

        return _storyManager.SelectStory(data.Story);
    }

    public bool OpenWardrobeScreenFor(GameData data, Action onComplete = null)
    {
        _wardrobeContextData = data;
        AppLogger.Info(
            AppLogCategory.Menu,
            nameof(MenuController),
            nameof(OpenWardrobeScreenFor),
            "[MENU][WARDROBE] Opening wardrobe screen for GameData context.",
            BuildGameDataMetadata(data, "open_wardrobe_for"));
        PrepareSelectedStoryContext(data);
        return OpenWardrobeScreen(onComplete);
    }

    private void HandleStoryCardClicked(GameData data)
    {
        if (data == null)
            return;

        if (TrySelectCarouselCard(data))
            return;

        if (_openHistoryScreenOnStoryClick)
        {
            OpenHistoryScreenFor(data);
            return;
        }

        StartStory(data);
    }

    private void SelectStoryData(GameData data)
    {
        if (data == null)
            return;

        _wardrobeContextData = data;

        List<GameData> games = BuildAvailableGameList();
        for (int i = 0; i < games.Count; i++)
        {
            if (games[i] == data)
            {
                _selectedGameIndex = i;
                return;
            }
        }
    }

    private void SelectStoryOffset(int offset)
    {
        if (!IsStoryCarouselEnabled() || offset == 0)
            return;

        List<GameData> games = BuildAvailableGameList();
        if (games.Count <= 1)
        {
            _selectedGameIndex = 0;
            UpdateStoryCarouselButtons(games.Count);
            return;
        }

        int nextIndex = _selectedGameIndex + offset;
        if (ShouldWrapStoryCarousel())
            nextIndex = WrapIndex(nextIndex, games.Count);
        else
            nextIndex = Mathf.Clamp(nextIndex, 0, games.Count - 1);

        if (_selectedGameIndex == nextIndex)
        {
            UpdateStoryCarouselButtons(games.Count);
            return;
        }

        _selectedGameIndex = nextIndex;
        BuildGameList();
    }

    private List<GameData> BuildAvailableGameList()
    {
        var result = new List<GameData>();
        if (_gameCatalog == null)
            return result;

        IReadOnlyList<GameData> games = _gameCatalog.Games;
        for (int i = 0; i < games.Count; i++)
        {
            GameData data = games[i];
            if (data != null)
                result.Add(data);
        }

        return result;
    }

    private List<StoryCarouselVisibleCard> BuildVisibleStoryCards(IReadOnlyList<GameData> games)
    {
        if (_storyCarouselLayout != null)
            return _storyCarouselLayout.BuildVisibleCards(games, _selectedGameIndex);

        var result = new List<StoryCarouselVisibleCard>();
        if (games == null || games.Count == 0)
            return result;

        int count = games.Count;
        int selectedIndex = Mathf.Clamp(_selectedGameIndex, 0, count - 1);
        if (!IsStoryCarouselEnabled())
        {
            for (int i = 0; i < count; i++)
                result.Add(new StoryCarouselVisibleCard(games[i], i, i - selectedIndex));

            return result;
        }

        int visibleSlots = Mathf.Clamp(_storyCarouselVisibleSlots, 1, count);
        int startOffset = -(visibleSlots / 2);

        for (int slot = 0; slot < visibleSlots; slot++)
        {
            int offset = startOffset + slot;
            int index = ShouldWrapStoryCarousel()
                ? WrapIndex(selectedIndex + offset, count)
                : selectedIndex + offset;

            if (index < 0 || index >= count)
                continue;

            GameData data = games[index];
            if (data != null && !ContainsVisibleIndex(result, index))
                result.Add(new StoryCarouselVisibleCard(data, index, offset));
        }

        return result;
    }

    private static bool ContainsVisibleIndex(List<StoryCarouselVisibleCard> cards, int sourceIndex)
    {
        if (cards == null)
            return false;

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].SourceIndex == sourceIndex)
                return true;
        }

        return false;
    }

    private GameData ResolveSelectedGame()
    {
        List<GameData> games = BuildAvailableGameList();
        if (games.Count == 0)
            return null;

        _selectedGameIndex = Mathf.Clamp(_selectedGameIndex, 0, games.Count - 1);
        return games[_selectedGameIndex];
    }

    private GameData ResolveCurrentStoryContextData()
    {
        if (_pendingStoryData != null)
            return _pendingStoryData;

        string storyId = _storyManager != null ? _storyManager.CurrentStoryId : "";
        if (MatchesStoryId(_wardrobeContextData, storyId))
            return _wardrobeContextData;

        GameData selected = ResolveSelectedGame();
        if (MatchesStoryId(selected, storyId))
            return selected;

        if (_gameCatalog != null && _gameCatalog.Games != null)
        {
            IReadOnlyList<GameData> games = _gameCatalog.Games;
            for (int i = 0; i < games.Count; i++)
            {
                if (MatchesStoryId(games[i], storyId))
                    return games[i];
            }
        }

        return _wardrobeContextData ?? selected;
    }

    private static bool MatchesStoryId(GameData data, string storyId)
    {
        if (data == null)
            return false;

        if (string.IsNullOrWhiteSpace(storyId))
            return data.Story != null;

        StoryData story = data.Story;
        if (story == null)
            return false;

        string candidate = !string.IsNullOrWhiteSpace(story.StoryId) ? story.StoryId : story.name;
        return string.Equals(candidate, storyId, StringComparison.OrdinalIgnoreCase);
    }

    private void RestoreSelectedGameIndex(IReadOnlyList<GameData> games, GameData selectedBeforeRebuild)
    {
        if (games == null || games.Count == 0)
        {
            _selectedGameIndex = 0;
            return;
        }

        if (selectedBeforeRebuild != null)
        {
            for (int i = 0; i < games.Count; i++)
            {
                if (games[i] == selectedBeforeRebuild)
                {
                    _selectedGameIndex = i;
                    return;
                }
            }
        }

        _selectedGameIndex = Mathf.Clamp(_selectedGameIndex, 0, games.Count - 1);
    }

    private void ClampSelectedGameIndex()
    {
        int count = _gameCatalog != null ? BuildAvailableGameList().Count : 0;
        _selectedGameIndex = count > 0 ? Mathf.Clamp(_selectedGameIndex, 0, count - 1) : 0;
    }

    private void UpdateStoryCarouselButtons(int gameCount)
    {
        bool canNavigate = IsStoryCarouselEnabled() && gameCount > 1;
        bool wrap = ShouldWrapStoryCarousel();
        bool canGoPrevious = canNavigate && (wrap || _selectedGameIndex > 0);
        bool canGoNext = canNavigate && (wrap || _selectedGameIndex < gameCount - 1);

        if (_previousStoryButton != null)
            _previousStoryButton.interactable = canGoPrevious;

        if (_nextStoryButton != null)
            _nextStoryButton.interactable = canGoNext;
    }

    private bool IsStoryCarouselEnabled()
    {
        return _storyCarouselLayout != null ? _storyCarouselLayout.CarouselEnabled : _storyCarouselEnabled;
    }

    private bool ShouldWrapStoryCarousel()
    {
        return _storyCarouselLayout != null ? _storyCarouselLayout.Wrap : _storyCarouselWrap;
    }

    private static int WrapIndex(int index, int count)
    {
        if (count <= 0)
            return 0;

        index %= count;
        return index < 0 ? index + count : index;
    }

    private void BindStoryCarouselButtons()
    {
        if (_previousStoryButton != null)
        {
            _previousStoryButton.onClick.RemoveListener(SelectPreviousStory);
            _previousStoryButton.onClick.AddListener(SelectPreviousStory);
        }

        if (_nextStoryButton != null)
        {
            _nextStoryButton.onClick.RemoveListener(SelectNextStory);
            _nextStoryButton.onClick.AddListener(SelectNextStory);
        }
    }

    private void UnbindStoryCarouselButtons()
    {
        if (_previousStoryButton != null)
            _previousStoryButton.onClick.RemoveListener(SelectPreviousStory);

        if (_nextStoryButton != null)
            _nextStoryButton.onClick.RemoveListener(SelectNextStory);
    }

    public void StartStory(GameData data)
    {
        StartStory(data, null);
    }

    public void StartStory(GameData data, IReadOnlyDictionary<string, int> initialStoryStats)
    {
        if (_storyManager == null || data == null)
            return;

        AppLogger.Info(
            AppLogCategory.Menu,
            nameof(MenuController),
            nameof(StartStory),
            "[MENU][START_STORY] Start story requested.",
            BuildGameDataMetadata(data, "start_story_request"));

        if (!data.CanStartStory)
        {
            string unavailableReason = StoryCatalogRuntimeDiagnostics.DescribeAvailability(data);
            ToastManager.Instance?.ShowSystemMessage(data.ComingSoonButtonText);
            Debug.LogWarning(
                $"[MenuController][STORY_BLOCKED] GameData '{data.name}' cannot start. reason='{unavailableReason}' " +
                $"story='{(data.Story != null ? data.Story.name : "<null>")}' platform={Application.platform}.",
                data);
            return;
        }

        if (data.Story == null)
        {
            Debug.LogError($"[MenuController] GameData '{data.name}' has no StoryData assigned. Check Game Catalog and GameData._story.", data);
            return;
        }

        if (!_storyLaunchState.IsIdle)
        {
            AppLogger.Warn(
                AppLogCategory.Menu,
                nameof(MenuController),
                nameof(StartStory),
                "[MENU][START_STORY_BLOCKED] Story launch is already in progress.",
                AddLaunchStateMetadata(BuildGameDataMetadata(data, "launch_state_busy")),
                recoverable: true);
            return;
        }

        if (_preStorySetupFlow != null && _preStorySetupFlow.IsVisible)
        {
            AppLogger.Warn(
                AppLogCategory.Menu,
                nameof(MenuController),
                nameof(StartStory),
                "[MENU][START_STORY_BLOCKED] Pre-story setup flow is already visible.",
                BuildGameDataMetadata(data, "pre_story_visible"),
                recoverable: true);
            return;
        }

        _pendingStoryInitialStats = CopyStoryInitialStats(initialStoryStats);

        if (ShouldBypassPreStorySetupForEditorTest())
        {
            BeginStory(data);
            return;
        }

        if (ShouldRunPreStorySetupBeforeStory(data))
        {
            StartPreStorySetupStateMachine(data);
            return;
        }

        BeginStory(data);
    }

    private bool ShouldBypassPreStorySetupForEditorTest()
    {
#if UNITY_EDITOR
        return EditorTestChapterLoader.IsEnabled && !Application.isBatchMode;
#else
        return false;
#endif
    }

    private bool ShouldRunPreStorySetupBeforeStory(GameData data)
    {
        if (!_runPreStorySetupBeforeStory || _preStorySetupFlow == null || data == null || data.Story == null)
            return false;

        if (HasRestorableStoryProgress(data))
            return false;

        if (!data.Story.RunsPreStorySetupBeforeStart)
            return false;

        return _preStorySetupFlow.ShouldShowBeforeStoryFor(data.Story);
    }

    private bool HasRestorableStoryProgress(GameData data)
    {
        if (data == null || data.Story == null || SaveManager.Instance == null)
            return false;

        string storyId = ResolveStoryId(data.Story);
        int saveSlot = StorySaveSlotSelection.GetSelectedSlot(storyId);
        SaveData snapshot = SaveManager.Instance.LoadForStorySlotIfExists(storyId, saveSlot);
        bool hasProgress = snapshot != null && snapshot.HasPosition;
        AppLogger.Info(
            AppLogCategory.Menu,
            nameof(MenuController),
            nameof(HasRestorableStoryProgress),
            "[MENU][SAVE_CHECK] Checked story progress before start/setup.",
            LogMetadata.Of(
                "storyId", storyId,
                "saveSlot", saveSlot,
                "gameData", data.name,
                "hasProgress", hasProgress,
                "snapshotChapterId", snapshot != null ? snapshot.chapterId : "",
                "snapshotEpisodeId", snapshot != null ? snapshot.episodeId : "",
                "snapshotNodeGuid", snapshot != null ? snapshot.currentNodeGuid : ""));

        return hasProgress;
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

    private IDictionary<string, object> BuildGameDataMetadata(GameData data, string reason)
    {
        GameWardrobeSetupSettings wardrobe = data != null ? data.WardrobeSetup : null;
        StoryData story = data != null ? data.Story : null;
        return LogMetadata.Of(
            "reason", reason ?? "",
            "gameData", data != null ? data.name : "",
            "gameName", data != null ? data.GameName : "",
            "storyId", ResolveStoryId(story),
            "storyAsset", story != null ? story.name : "",
            "canStart", data != null && data.CanStartStory,
            "forceComingSoon", data != null && data.ForceComingSoon,
            "hasPlayableStory", data != null && data.HasPlayableStory,
            "availabilityReason", StoryCatalogRuntimeDiagnostics.DescribeAvailability(data),
            "episodeText", data != null ? data.EpisodeProgressText : "",
            "selectedIndex", _selectedGameIndex,
            "launchState", _storyLaunchState.ToString(),
            "pendingStory", _pendingStoryData != null ? _pendingStoryData.name : "",
            "wardrobeContext", _wardrobeContextData != null ? _wardrobeContextData.name : "",
            "wardrobeOverrides", wardrobe != null && wardrobe.OverrideWardrobeAssets,
            "wardrobeHasRuntimeContent", wardrobe != null && wardrobe.HasRuntimeContent,
            "appearanceOptions", wardrobe != null && wardrobe.AppearanceOptions != null ? wardrobe.AppearanceOptions.Count : 0,
            "outfitItems", wardrobe != null && wardrobe.OutfitItems != null ? wardrobe.OutfitItems.Count : 0,
            "hairItems", wardrobe != null && wardrobe.HairItems != null ? wardrobe.HairItems.Count : 0,
            "accessoryItems", wardrobe != null && wardrobe.AccessoryItems != null ? wardrobe.AccessoryItems.Count : 0,
            "defaultOutfit", wardrobe != null ? ClothingId(wardrobe.DefaultOutfitItem) : "",
            "defaultHair", wardrobe != null ? ClothingId(wardrobe.DefaultHairItem) : "",
            "defaultAccessory", wardrobe != null ? ClothingId(wardrobe.DefaultAccessoryItem) : "");
    }

    private IDictionary<string, object> AddLaunchStateMetadata(IDictionary<string, object> metadata)
    {
        if (metadata == null)
            metadata = LogMetadata.Of();

        metadata["launchState"] = _storyLaunchState.ToString();
        metadata["pendingStory"] = _pendingStoryData != null ? _pendingStoryData.name : "";
        metadata["pendingIncludesWardrobe"] = _pendingPreStoryIncludesWardrobe;
        metadata["wardrobeContext"] = _wardrobeContextData != null ? _wardrobeContextData.name : "";
        return metadata;
    }

    private static string ClothingId(ClothingItem item)
    {
        return item != null ? SaveDataSanitizer.SanitizeIdentifier(item.id) : "";
    }

    private static Dictionary<string, int> CopyStoryInitialStats(IReadOnlyDictionary<string, int> stats)
    {
        if (stats == null || stats.Count == 0)
            return null;

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, int> pair in stats)
        {
            string statId = SaveDataSanitizer.SanitizeStatKey(pair.Key);
            if (string.IsNullOrEmpty(statId))
                continue;

            result[statId] = SaveDataSanitizer.ClampStatValue(pair.Value);
        }

        return result.Count > 0 ? result : null;
    }

    private void QueuePendingStoryInitialStats(GameData data)
    {
        if (_storyManager == null || _pendingStoryInitialStats == null || _pendingStoryInitialStats.Count == 0)
            return;

        _storyManager.QueueInitialStoryStats(ResolveStoryId(data != null ? data.Story : null), _pendingStoryInitialStats);
        _pendingStoryInitialStats = null;
    }

    private void StartPreStorySetupStateMachine(GameData data)
    {
        if (!PrepareStoryForSetup(data))
            return;

        _pendingStoryData = data;
        _pendingPreStoryIncludesWardrobe = data.Story != null && data.Story.PreStorySetupIncludesWardrobe;
        _storyLaunchState.Enter(StoryLaunchState.AskingHeroName);

        void ShowNameStep()
        {
            if (_preStorySetupFlow != null)
            {
                _preStorySetupFlow.ShowNameOnly(OnPreStoryNameConfirmed, CancelPreStorySetup, false);
                return;
            }

            if (_nameInputUI != null)
            {
                _nameInputUI.Show(OnPreStoryNameConfirmed, forceShow: true);
                return;
            }

            OnPreStoryNameConfirmed();
        }

        _preStorySetupFlow?.HideNameStepObjectsImmediately();

        if (!OpenStoryScreen(ShowNameStep))
            ShowNameStep();
    }

    private bool PrepareStoryForSetup(GameData data)
    {
        if (_storyManager == null || data == null || data.Story == null)
            return false;

        try
        {
            return _storyManager.SelectStory(data.Story);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"MenuController: failed to prepare pre-story setup: {exception.Message}", this);
            return false;
        }
    }

    private void OnPreStoryNameConfirmed()
    {
        if (!_storyLaunchState.Is(StoryLaunchState.AskingHeroName) || _pendingStoryData == null)
            return;

        if (!_pendingPreStoryIncludesWardrobe)
        {
            CompletePreStorySetupAndBeginStory();
            return;
        }

        _preStorySetupFlow?.HideNameStepObjectsImmediately();
        _storyLaunchState.Enter(StoryLaunchState.OpeningWardrobe);

        if (!CloseStoryScreenAndOpenWardrobe(OpenPreStoryWardrobeSetup))
            OpenPreStoryWardrobeSetup();
    }

    private void OpenPreStoryWardrobeSetup()
    {
        if (_pendingStoryData == null)
        {
            ResetStoryLaunchState();
            return;
        }

        _storyLaunchState.Enter(StoryLaunchState.WaitingForWardrobe);

        WardrobeHeroSetupPage setupPage = ResolvePreStoryWardrobeSetupPage();
        bool completedSynchronously = false;

        void CompleteOnce()
        {
            if (completedSynchronously)
                return;

            completedSynchronously = true;
            OnPreStoryWardrobeCompleted();
        }

        if (setupPage != null)
            setupPage.PrepareForStory(_pendingStoryData);

        bool opened = setupPage != null && setupPage.OpenFullSetup(CompleteOnce, CancelPreStorySetup);
        if (!opened && !completedSynchronously)
            CompleteOnce();
    }

    private void OnPreStoryWardrobeCompleted()
    {
        CompletePreStorySetupAndBeginStory();
    }

    private void CompletePreStorySetupAndBeginStory()
    {
        if (_pendingStoryData == null)
        {
            ResetStoryLaunchState();
            return;
        }

        GameData data = _pendingStoryData;
        _storyLaunchState.Enter(StoryLaunchState.StartingStory);
        _preStorySetupFlow?.MarkCompleted();
        _pendingStoryData = null;
        _pendingPreStoryIncludesWardrobe = false;

        BeginStory(data);
    }

    private void CancelPreStorySetup()
    {
        ResetStoryLaunchState();
        ReturnToMenu();
    }

    private void BeginStory(GameData data)
    {
        _preStorySetupFlow?.RestoreNameStepObjectsImmediately();

        if (_storyManager == null || data == null)
            return;

        _storyLaunchState.Enter(StoryLaunchState.StartingStory);
        _wardrobeContextData = data;
        SelectStoryData(data);

        AppLogger.Info(
            AppLogCategory.Menu,
            nameof(MenuController),
            nameof(BeginStory),
            "[MENU][START_STORY] Beginning story load.",
            AddLaunchStateMetadata(BuildGameDataMetadata(data, "begin_story")));

        if (data.Story == null)
        {
            Debug.LogError($"[MenuController] Cannot start '{data.name}': StoryData is missing in GameData._story.", data);
            ResetStoryLaunchState();
            return;
        }

        void BeginLoadingAfterStoryScreenOpen()
        {
            if (_storyLoadRoutine != null)
                StopCoroutine(_storyLoadRoutine);

            _storyLoadRoutine = StartCoroutine(StartStoryRoutine());
        }

        try
        {
            QueuePendingStoryInitialStats(data);

            if (!_storyManager.SelectStory(data.Story))
            {
                _storyManager.ClearPendingInitialStoryStats();
                ResetStoryLaunchState();
                return;
            }

            _storyManager.CloseEndPanel();
            _storyManager.dialogueUI?.ResetStoryUi();
            _storyManager.dialogueUI?.ShowSystemMessage("Загрузка истории...");
            _mainMenuMusicPlayer?.StopMusic();

            void OpenStoryAfterStartLoading()
            {
                StoryBlackScreenTransition transition = ResolveStoryBlackScreenTransition();
                if (transition != null)
                {
                    transition.FadeToBlack(() =>
                    {
                        void ContinueFromBlack()
                        {
                            transition.FadeFromBlack();
                            BeginLoadingAfterStoryScreenOpen();
                        }

                        if (!OpenStoryScreen(ContinueFromBlack))
                            ContinueFromBlack();
                    });
                }
                else
                {
                    if (!OpenStoryScreen(BeginLoadingAfterStoryScreenOpen))
                        BeginLoadingAfterStoryScreenOpen();
                }
            }

            void OpenStoryImmediatelyAfterStartLoading()
            {
                if (!OpenStoryScreenImmediate(BeginLoadingAfterStoryScreenOpen))
                    BeginLoadingAfterStoryScreenOpen();
            }

            IStoryStartLoadingScreen startLoadingScreen = ResolveStoryStartLoadingScreen();
            if (_showStoryStartLoadingScreen && startLoadingScreen != null)
            {
                startLoadingScreen.Show(data, OpenStoryImmediatelyAfterStartLoading);
                return;
            }

            OpenStoryAfterStartLoading();
        }
        catch (System.Exception exception)
        {
            _storyManager?.ClearPendingInitialStoryStats();
            Debug.LogWarning($"MenuController: failed to select story: {exception.Message}", this);
            ResetStoryLaunchState();
            return;
        }
    }

    private IEnumerator StartStoryRoutine()
    {
        if (_storyManager != null)
            yield return RunRoutineSafely(_storyManager.LoadAndStart(), "story load");

        _storyLoadRoutine = null;
        _storyLaunchState.Reset();
    }

    private IEnumerator RefreshCatalogAndRebuild()
    {
        float timeout = 15f;
        float elapsed = 0f;

        while (!NetworkManager.IsAuthenticated && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        NetworkManager network = NetworkManager.Instance;
        if (!NetworkManager.IsAuthenticated || network == null)
        {
            _refreshCatalogRoutine = null;
            yield break;
        }

        bool synced = false;
        yield return RunRoutineSafely(network.SyncCatalog(ok => synced = ok), "catalog sync");

        if (synced)
        {
            BuildGameList();
            StoryCatalogRuntimeDiagnostics.LogCatalog(_gameCatalog, "after_catalog_sync", this);
        }

        _refreshCatalogRoutine = null;
    }

    private void AutoWireReferences()
    {
        if (_storyCarouselLayout == null)
            _storyCarouselLayout = GetComponent<StoryCardCarouselLayout>();

        if (_storyCarouselLayout == null && _gamesParent != null)
            _storyCarouselLayout = _gamesParent.GetComponent<StoryCardCarouselLayout>();

        if (_storyManager == null)
            _storyManager = FindObjectOfType<StoryManager>(true);

        if (_storyScreenNavigator == null)
            _storyScreenNavigator = FindObjectOfType<StoryScreenNavigator>(true);

        if (_mainMenuMusicPlayer == null)
            _mainMenuMusicPlayer = FindObjectOfType<MainMenuMusicPlayer>(true);

        if (_preStorySetupFlow == null)
            _preStorySetupFlow = FindObjectOfType<PreStorySetupFlow>(true);

        if (_preStoryWardrobeSetupPage == null)
            _preStoryWardrobeSetupPage = FindObjectOfType<WardrobeHeroSetupPage>(true);

        if (_wardrobeCategoryTabs == null)
            _wardrobeCategoryTabs = FindObjectOfType<WardrobeCategoryTabs>(true);

        if (_storyHistoryScreen == null)
            _storyHistoryScreen = FindObjectOfType<StoryHistoryScreen>(true);

        if (_storyStartLoadingScreen == null)
            _storyStartLoadingScreen = FindObjectOfType<StoryStartLoadingScreen>(true);

        if (_storyStartLoadingScreenContract == null && _storyStartLoadingScreen != null)
            _storyStartLoadingScreenContract = _storyStartLoadingScreen;

        if (_menuCanvasGroup == null && _storyScreenNavigator != null)
            _menuCanvasGroup = _storyScreenNavigator.MenuCanvasGroup;

        if (_blackScreen != null && _storyBlackScreenTransition == null)
            _storyBlackScreenTransition = _blackScreen.GetComponent<StoryBlackScreenTransition>();

        if (_storyBlackScreenTransition == null)
            _storyBlackScreenTransition = FindObjectOfType<StoryBlackScreenTransition>(true);

        if (_blackScreen != null && _storyBlackScreenTransition != null)
            _storyBlackScreenTransition.AssignBlackScreen(_blackScreen);

        if (_navigationRoot == null || IsWardrobeInternalNavigation(_navigationRoot))
            _navigationRoot = FindMenuNavigationRoot();
    }

    private void ApplyMenuScreenTransitionProfile()
    {
        if (!_applyModernScreenTransitionProfile || _storyScreenNavigator == null)
            return;

        _storyScreenNavigator.ConfigureScreenTransition(
            _menuScreenTransition,
            _menuScreenTransitionDuration,
            _menuScreenTransitionEase,
            _menuScreenTransitionUsesUnscaledTime,
            _menuScreenTransitionUsesScreenOrder);
    }

    private WardrobeHeroSetupPage ResolvePreStoryWardrobeSetupPage()
    {
        string storyId = _pendingStoryData != null && _pendingStoryData.Story != null
            ? _pendingStoryData.Story.StoryId
            : _storyManager != null ? _storyManager.CurrentStoryId : "";
        string chapterId = _storyManager != null
            ? FirstNonEmpty(_storyManager.CurrentEpisodeId, _storyManager.CurrentChapterId)
            : "";

        AutoWireReferences();
        WardrobeHeroSetupPage storyPage = WardrobeHeroSetupPage.FindBestForStory((Transform)null, storyId, chapterId);
        if (storyPage != null)
        {
            _preStoryWardrobeSetupPage = storyPage;
            return storyPage;
        }

        return _preStoryWardrobeSetupPage;
    }

    public void ReturnToMenu()
    {
        ReturnToMenu(null);
    }

    public void ReturnToMenu(Action onMenuShown)
    {
        ResetStoryLaunchState();
        ResolveStoryStartLoadingScreen()?.HideImmediate();
        _storyManager?.FadeOutStoryAudioForScreenBoundary();

        if (_storyLoadRoutine != null)
        {
            StopCoroutine(_storyLoadRoutine);
            _storyLoadRoutine = null;
        }

        StoryBlackScreenTransition transition = ResolveStoryBlackScreenTransition();
        if (transition != null && _isStoryScreenOpen)
        {
            transition.FadeOut(() => ReturnToMenuAfterFadeOut(transition, onMenuShown));
            return;
        }

        ReturnToMenuImmediate(onMenuShown);
    }

    private void ReturnToMenuAfterFadeOut(StoryBlackScreenTransition transition, Action onMenuShown)
    {
        ReturnToMenuImmediate(() =>
        {
            onMenuShown?.Invoke();
            transition?.FadeIn();
        });
    }

    private void ReturnToMenuImmediate(Action onMenuShown)
    {
        BuildGameList();

        if (_storyScreenNavigator != null)
        {
            _storyScreenNavigator.ShowMenuScreen(() =>
            {
                SetStoryScreenOpen(false);
                onMenuShown?.Invoke();
            });
        }
        else
        {
            SetStoryScreenOpen(false);
            FadeMenuIn(0.4f);
            onMenuShown?.Invoke();
        }

        _mainMenuMusicPlayer?.PlayMusic();
    }

    public bool OpenStoryScreen(Action onComplete = null)
    {
        if (_storyScreenNavigator == null)
        {
            Debug.LogWarning("MenuController: StoryScreenNavigator is not assigned.", this);
            return false;
        }

        _menuFadeTween?.Kill();
        SetStoryScreenOpen(true);
        AppLogger.Info(
            AppLogCategory.ScreenNavigation,
            nameof(MenuController),
            nameof(OpenStoryScreen),
            "[MENU][SCREEN] Opening story screen.",
            LogMetadata.Of(
                "hasCallback", onComplete != null,
                "selectedGame", SelectedGame != null ? SelectedGame.name : "",
                "contextGame", _wardrobeContextData != null ? _wardrobeContextData.name : "",
                "launchState", _storyLaunchState.ToString()));
        _storyScreenNavigator.ShowStoryScreen(onComplete);
        return true;
    }

    public bool OpenStoryScreenImmediate(Action onComplete = null)
    {
        if (_storyScreenNavigator == null)
        {
            Debug.LogWarning("MenuController: StoryScreenNavigator is not assigned.", this);
            return false;
        }

        _menuFadeTween?.Kill();
        SetStoryScreenOpen(true);
        AppLogger.Info(
            AppLogCategory.ScreenNavigation,
            nameof(MenuController),
            nameof(OpenStoryScreenImmediate),
            "[MENU][SCREEN] Opening story screen immediately.",
            LogMetadata.Of(
                "hasCallback", onComplete != null,
                "selectedGame", SelectedGame != null ? SelectedGame.name : "",
                "contextGame", _wardrobeContextData != null ? _wardrobeContextData.name : "",
                "launchState", _storyLaunchState.ToString()));

        if (_storyScreenNavigator.ShowStoryScreenImmediate(onComplete))
            return true;

        SetStoryScreenOpen(false);
        return false;
    }

    public bool OpenWardrobeScreen(Action onComplete = null, bool openEntryCategory = true)
    {
        AutoWireReferences();

        if (_storyScreenNavigator == null)
        {
            Debug.LogWarning("MenuController: StoryScreenNavigator is not assigned.", this);
            return false;
        }

        _menuFadeTween?.Kill();
        _storyManager?.FadeOutStoryAudioForScreenBoundary();
        SetStoryScreenOpen(false);
        AppLogger.Info(
            AppLogCategory.ScreenNavigation,
            nameof(MenuController),
            nameof(OpenWardrobeScreen),
            "[MENU][SCREEN] Opening wardrobe screen.",
            LogMetadata.Of(
                "screenId", _wardrobeScreenId,
                "openEntryCategory", openEntryCategory,
                "entryCategory", _wardrobeOpenCategory.ToString(),
                "selectedGame", SelectedGame != null ? SelectedGame.name : "",
                "contextGame", _wardrobeContextData != null ? _wardrobeContextData.name : "",
                "pendingStory", _pendingStoryData != null ? _pendingStoryData.name : "",
                "launchState", _storyLaunchState.ToString()));
        bool entryCategoryOpened = false;
        void OpenEntryCategoryOnce()
        {
            if (entryCategoryOpened)
                return;

            entryCategoryOpened = true;
            OpenWardrobeEntryCategory();
        }

        bool opened = _storyScreenNavigator.OpenScreen(_wardrobeScreenId, () =>
        {
            if (openEntryCategory)
                OpenEntryCategoryOnce();

            onComplete?.Invoke();
        });

        if (opened && openEntryCategory)
            OpenEntryCategoryOnce();

        return opened;
    }

    private void OpenWardrobeEntryCategory()
    {
        GameData contextData = _wardrobeContextData ?? _pendingStoryData ?? SelectedGame;
        WardrobeHeroSetupPage wardrobePage = ResolvePreStoryWardrobeSetupPage();
        if (wardrobePage != null)
            wardrobePage.PrepareForStory(contextData);

        WardrobeCategoryTabs tabs = ResolveWardrobeCategoryTabs();
        if (tabs != null)
        {
            if (wardrobePage != null)
                tabs.AssignWardrobePage(wardrobePage);

            if (_wardrobeOpenCategory == WardrobeCategoryTabType.None)
                tabs.OpenDefaultCategory();
            else
                tabs.OpenCategory(_wardrobeOpenCategory);

            return;
        }

        if (wardrobePage == null)
            return;

        switch (_wardrobeOpenCategory)
        {
            case WardrobeCategoryTabType.Appearance:
                wardrobePage.ShowAppearanceCategory();
                break;
            case WardrobeCategoryTabType.Hair:
                wardrobePage.ShowHairCategory();
                break;
            case WardrobeCategoryTabType.Outfit:
            case WardrobeCategoryTabType.None:
                wardrobePage.ShowOutfitCategory();
                break;
        }
    }

    private WardrobeCategoryTabs ResolveWardrobeCategoryTabs()
    {
        if (_wardrobeCategoryTabs == null)
            _wardrobeCategoryTabs = FindObjectOfType<WardrobeCategoryTabs>(true);

        return _wardrobeCategoryTabs;
    }

    public bool CloseStoryScreenAndOpenWardrobe(Action onComplete = null, bool openEntryCategory = true)
    {
        return OpenWardrobeScreen(onComplete, openEntryCategory);
    }

    public void QuitGame()
    {
        PlayerPrefs.Save();

#if UNITY_EDITOR
        if (Application.isPlaying)
            EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }

    private void ShowStoryScreen()
    {
        OpenStoryScreen();
    }

    private StoryBlackScreenTransition ResolveStoryBlackScreenTransition()
    {
        if (_blackScreen != null)
        {
            if (_storyBlackScreenTransition == null)
                _storyBlackScreenTransition = _blackScreen.GetComponent<StoryBlackScreenTransition>();

            if (_storyBlackScreenTransition == null)
                _storyBlackScreenTransition = _blackScreen.AddComponent<StoryBlackScreenTransition>();

            _storyBlackScreenTransition.AssignBlackScreen(_blackScreen);
            return _storyBlackScreenTransition;
        }

        if (_storyBlackScreenTransition != null)
            return _storyBlackScreenTransition;

        _storyBlackScreenTransition = StoryBlackScreenTransition.Instance;

        if (_storyBlackScreenTransition == null)
            _storyBlackScreenTransition = FindObjectOfType<StoryBlackScreenTransition>(true);

        if (_storyBlackScreenTransition != null && _blackScreen != null)
            _storyBlackScreenTransition.AssignBlackScreen(_blackScreen);

        return _storyBlackScreenTransition;
    }

    private IStoryStartLoadingScreen ResolveStoryStartLoadingScreen()
    {
        if (_storyStartLoadingScreenContract != null)
            return _storyStartLoadingScreenContract;

        if (_storyStartLoadingScreen == null)
            _storyStartLoadingScreen = FindObjectOfType<StoryStartLoadingScreen>(true);

        _storyStartLoadingScreenContract = _storyStartLoadingScreen;
        return _storyStartLoadingScreenContract;
    }

    private void ResetStoryLaunchState()
    {
        _preStorySetupFlow?.RestoreNameStepObjectsImmediately();
        _pendingStoryData = null;
        _pendingStoryInitialStats = null;
        _pendingPreStoryIncludesWardrobe = false;
        _storyLaunchState.Reset();
    }

    private void SetStoryScreenOpen(bool open)
    {
        if (_isStoryScreenOpen == open)
            return;

        _isStoryScreenOpen = open;

        if (open)
        {
            HideMenuUiForStory();
            return;
        }

        RestoreMenuUiAfterStory();
    }

    private void HideMenuUiForStory()
    {
        _settingsFadeTween?.Kill();
        HideNavigationForStory();
        HideObjectForStory(_settingsPanel);
        HideObjectForStory(_bugReportPanel != null ? _bugReportPanel.panel : null);

        if (_hideWhileStoryScreenOpen == null)
            return;

        foreach (GameObject item in _hideWhileStoryScreenOpen)
            HideObjectForStory(item);
    }

    private void RestoreMenuUiAfterStory()
    {
        foreach (KeyValuePair<GameObject, bool> state in _storyHiddenObjectStates)
        {
            if (state.Key != null)
                state.Key.SetActive(state.Value);
        }

        _storyHiddenObjectStates.Clear();
    }

    private void HideObjectForStory(GameObject target)
    {
        if (target == null)
            return;

        if (!_storyHiddenObjectStates.ContainsKey(target))
            _storyHiddenObjectStates.Add(target, target.activeSelf);

        if (target.activeSelf)
            target.SetActive(false);
    }

    private void HideNavigationForStory()
    {
        if (_navigationRoot == null || IsWardrobeInternalNavigation(_navigationRoot))
            return;

        // UIScreenNavigationVisibility already owns the global navigation visibility
        // from UIScreenState. Do not run a second SetActive-based visibility system
        // on the same object or it will fight screen navigation.
        if (_navigationRoot.GetComponent<UIScreenNavigationVisibility>() != null)
            return;

        HideObjectForStory(_navigationRoot);
    }

    private GameObject FindMenuNavigationRoot()
    {
        UIScreenNavigationVisibility[] visibilityRules = FindObjectsOfType<UIScreenNavigationVisibility>(true);
        for (int i = 0; i < visibilityRules.Length; i++)
        {
            UIScreenNavigationVisibility visibility = visibilityRules[i];
            GameObject candidate = visibility != null ? visibility.gameObject : null;
            if (candidate != null && !IsWardrobeInternalNavigation(candidate))
                return candidate;
        }

        Scene scene = gameObject.scene;
        if (!scene.IsValid())
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            if (root == null)
                continue;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform item in transforms)
            {
                if (item == null || item.name != "Navigation")
                    continue;

                GameObject candidate = item.gameObject;
                if (!IsWardrobeInternalNavigation(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static bool IsWardrobeInternalNavigation(GameObject target)
    {
        if (target == null)
            return false;

        if (target.GetComponent<WardrobeCategoryTabs>() != null)
            return true;

        return target.GetComponentInParent<WardrobeHeroSetupPage>(true) != null;
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Scene scene = gameObject.scene;
        if (!scene.IsValid())
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            if (root == null)
                continue;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform item in transforms)
            {
                if (item != null && item.name == objectName)
                    return item.gameObject;
            }
        }

        return null;
    }

    private void OpenSettings()
    {
        if (_settingsPanel == null)
            return;

        RectTransform panelRect = _settingsPanel.transform as RectTransform;
        CaptureSettingsPanelHomeScale(panelRect);

        _settingsPanel.SetActive(true);
        CanvasGroup canvasGroup = _settingsPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = _settingsPanel.AddComponent<CanvasGroup>();

        _settingsFadeTween?.Kill();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (panelRect != null)
            panelRect.localScale = ScaleBy(_settingsPanelHomeScale, _popupStartScale);

        float transitionDuration = Mathf.Max(0.01f, _popupTransitionDuration);
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(canvasGroup.DOFade(1f, transitionDuration).SetEase(Ease.OutQuad));
        if (panelRect != null)
            sequence.Join(panelRect.DOScale(_settingsPanelHomeScale, transitionDuration).SetEase(_popupTransitionEase));

        _settingsFadeTween = sequence.OnComplete(() =>
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            if (panelRect != null)
                panelRect.localScale = _settingsPanelHomeScale;
        });
    }

    private void CloseSettings()
    {
        if (_settingsPanel == null)
            return;

        RectTransform panelRect = _settingsPanel.transform as RectTransform;
        CaptureSettingsPanelHomeScale(panelRect);

        CanvasGroup canvasGroup = _settingsPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null || !_settingsPanel.activeSelf)
        {
            _settingsPanel.SetActive(false);
            return;
        }

        _settingsFadeTween?.Kill();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float transitionDuration = Mathf.Max(0.01f, _popupTransitionDuration * 0.82f);
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(canvasGroup.DOFade(0f, transitionDuration).SetEase(Ease.InQuad));
        if (panelRect != null)
            sequence.Join(panelRect.DOScale(ScaleBy(_settingsPanelHomeScale, _popupStartScale), transitionDuration).SetEase(Ease.InCubic));

        _settingsFadeTween = sequence.OnComplete(() =>
        {
            _settingsPanel.SetActive(false);
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            if (panelRect != null)
                panelRect.localScale = _settingsPanelHomeScale;
        });
    }

    private void OpenBugReport()
    {
        BugReportPanel panel = _bugReportPanel ?? BugReportPanel.Instance;
        if (panel == null)
        {
            Debug.LogWarning("MenuController: BugReportPanel is not assigned.", this);
            return;
        }

        try
        {
            panel.Show();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"MenuController: failed to open bug report panel: {exception.Message}", this);
        }
    }

    private void FadeMenuIn(float duration)
    {
        if (_menuCanvasGroup == null)
            return;

        _menuFadeTween?.Kill();
        _menuCanvasGroup.alpha = 0f;
        _menuFadeTween = _menuCanvasGroup
            .DOFade(1f, Mathf.Max(0f, duration))
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void CaptureSettingsPanelHomeScale(RectTransform panelRect)
    {
        if (_settingsPanelHomeScaleCaptured || panelRect == null)
            return;

        _settingsPanelHomeScale = panelRect.localScale;
        _settingsPanelHomeScaleCaptured = true;
    }

    private static Vector3 ScaleBy(Vector3 scale, float multiplier)
    {
        return new Vector3(scale.x * multiplier, scale.y * multiplier, scale.z * multiplier);
    }

    private IEnumerator RunRoutineSafely(IEnumerator routine, string label)
    {
        if (routine == null)
            yield break;

        while (true)
        {
            object current;
            try
            {
                if (!routine.MoveNext())
                    yield break;

                current = routine.Current;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"MenuController: {label} failed: {exception.Message}", this);
                yield break;
            }

            if (current is IEnumerator nestedRoutine)
                yield return RunRoutineSafely(nestedRoutine, label);
            else
                yield return current;
        }
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "";
    }
}
