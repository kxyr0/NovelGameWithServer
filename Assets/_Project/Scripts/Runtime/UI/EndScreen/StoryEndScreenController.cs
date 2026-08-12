using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum StoryEndButtonAction
{
    None = 0,
    ReturnToMenu = 1,
    ContinueStory = 2,
    RestartCompletedEpisode = 3,
    OpenScreen = 4,
    CloseEndPanel = 5,
    Refresh = 6,
    InvokeEventOnly = 7,
    ContinueOrReturnToMenu = 8
}

public enum StoryEndTextSource
{
    StaticText = 0,
    CompletionTitle = 1,
    StoryTitle = 2,
    StoryId = 3,
    CompletedEpisodeTitle = 4,
    CompletedEpisodeId = 5,
    CompletedEpisodeNumber = 6,
    CompletedEpisodeProgress = 7,
    NextEpisodeTitle = 8,
    NextEpisodeId = 9,
    NextEpisodeNumber = 10,
    NextEpisodeProgress = 11,
    CurrentEpisodeTitle = 12,
    CurrentEpisodeId = 13,
    CurrentEpisodeNumber = 14,
    CurrentEpisodeProgress = 15,
    CandleBalance = 16,
    TownStat = 17,
    ReputationStat = 18,
    StoryStat = 19,
    CustomStat = 20,
    PlayerName = 21,
    HeartBalance = 22,
    EpisodeCandleDelta = 23,
    EpisodeHeartDelta = 24
}

[DisallowMultipleComponent]
public sealed class StoryEndScreenController : MonoBehaviour
{
    const string DefaultCompletionTitle = "Серия завершена";
    const string DefaultChapterCompletionTitle = "Глава завершена";

    [Serializable]
    public sealed class ButtonBinding
    {
        [SerializeField] private Button _button;
        [SerializeField] private StoryEndButtonAction _action = StoryEndButtonAction.OpenScreen;
        [SerializeField] private string _targetScreenId;
        [SerializeField] private bool _hideWhenUnavailable = true;
        [SerializeField] private bool _closeEndPanelBeforeScreenOpen = true;
        [SerializeField] private UnityEvent _onClick = new UnityEvent();

        public Button Button => _button;
        public StoryEndButtonAction Action => _action;
        public string TargetScreenId => _targetScreenId;
        public bool HideWhenUnavailable => _hideWhenUnavailable;
        public bool CloseEndPanelBeforeScreenOpen => _closeEndPanelBeforeScreenOpen;
        public UnityEvent OnClick => _onClick;
    }

    [Serializable]
    public sealed class TextBinding
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private StoryEndTextSource _source = StoryEndTextSource.StaticText;
        [SerializeField] private string _staticText;
        [SerializeField] private string _statId;
        [SerializeField] private string _format = "{0}";
        [SerializeField] private bool _hideWhenEmpty;

        public TMP_Text Text => _text;
        public StoryEndTextSource Source => _source;
        public string StaticText => _staticText;
        public string StatId => _statId;
        public string Format => _format;
        public bool HideWhenEmpty => _hideWhenEmpty;
    }

    sealed class ButtonRegistration
    {
        readonly Button _button;
        readonly UnityAction _action;

        public ButtonRegistration(Button button, UnityAction action)
        {
            _button = button;
            _action = action;
        }

        public void Add()
        {
            if (_button != null && _action != null)
                _button.onClick.AddListener(_action);
        }

        public void Remove()
        {
            if (_button != null && _action != null)
                _button.onClick.RemoveListener(_action);
        }
    }

    struct RectSnapshot
    {
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
    }

    struct ImageSnapshot
    {
        public Sprite Sprite;
        public bool Enabled;
        public Color Color;
        public Image.Type Type;
        public bool PreserveAspect;
        public float PixelsPerUnitMultiplier;
    }

    struct TextSnapshot
    {
        public TMP_FontAsset Font;
        public float FontSize;
        public bool EnableAutoSizing;
        public float FontSizeMin;
        public float FontSizeMax;
        public TextAlignmentOptions Alignment;
        public bool EnableWordWrapping;
        public TextOverflowModes OverflowMode;
        public float LineSpacing;
        public Vector4 Margin;
        public Color Color;
    }

    [Header("References")]
    [SerializeField] private StoryManager _storyManager;
    [SerializeField] private MenuController _menuController;
    [SerializeField] private StoryScreenNavigator _screenNavigator;

    [Header("Main Buttons")]
    [SerializeField] private Button _continueButton;
    [HideInInspector]
    [SerializeField] private Button _menuButton;
    [HideInInspector]
    [SerializeField] private Button _nextEpisodeButton;
    [HideInInspector]
    [SerializeField] private Button _restartEpisodeButton;
    [SerializeField] private bool _hideUnavailableMainButtons = true;

    [Header("Main Texts")]
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _storyTitleText;
    [SerializeField] private TMP_Text _completedEpisodeText;
    [SerializeField] private TMP_Text _nextEpisodeText;
    [SerializeField] private string _completionTitle = DefaultCompletionTitle;
    [SerializeField] private string _emptyNextEpisodeText = "";

    [Header("End Screen Architecture")]
    [SerializeField] private StoryEndScreenReferences _references = new StoryEndScreenReferences();
    [SerializeField] private StoryEndScreenLayoutSettings _layoutSettings = new StoryEndScreenLayoutSettings();
    [SerializeField] private StoryEndScreenPreviewSettings _previewSettings = new StoryEndScreenPreviewSettings();
    [SerializeField] private StoryEndScreenStatBinding[] _statBindings = StoryEndScreenStatBinding.CreateDefaults();

    [Header("Extra Inspector Bindings")]
    [SerializeField] private ButtonBinding[] _extraButtons = Array.Empty<ButtonBinding>();
    [SerializeField] private TextBinding[] _extraTexts = Array.Empty<TextBinding>();

    readonly List<ButtonRegistration> _buttonRegistrations = new List<ButtonRegistration>();
    readonly Dictionary<RectTransform, RectSnapshot> _rectSnapshots = new Dictionary<RectTransform, RectSnapshot>();
    readonly Dictionary<Image, ImageSnapshot> _imageSnapshots = new Dictionary<Image, ImageSnapshot>();
    readonly Dictionary<TMP_Text, TextSnapshot> _textSnapshots = new Dictionary<TMP_Text, TextSnapshot>();
    readonly Dictionary<RectTransform, RectSnapshot> _sceneRectSnapshots = new Dictionary<RectTransform, RectSnapshot>();
    readonly StoryEndScreenDataProvider _dataProvider = new StoryEndScreenDataProvider();
    readonly StoryEndScreenPreviewRenderer _previewRenderer = new StoryEndScreenPreviewRenderer();
    readonly StoryEndScreenRuntimePresenter _runtimePresenter = new StoryEndScreenRuntimePresenter();
    readonly StoryEndScreenNavigationController _navigation = new StoryEndScreenNavigationController();
    readonly StoryEndScreenValidator _validator = new StoryEndScreenValidator();

    bool _registeredButtons;
    bool _useUnifiedContinueButton;
    bool _runtimeRenderInProgress;
    StoryEndScreenActivationRelay _activationRelay;
    StoryEndScreenReferences _serializedReferencesSnapshot;
    StoryEndScreenLayoutSettings _serializedLayoutSettingsSnapshot;
    StoryEndScreenPreviewSettings _serializedPreviewSettingsSnapshot;
    StoryEndScreenStatBinding[] _serializedStatBindingsSnapshot;
    StoryEndScreenStyleSettings _activeStoryStyle;
    string _activeStoryStyleId;

    public StoryManager StoryManager => _storyManager;
    public MenuController MenuController => _menuController;
    public StoryScreenNavigator ScreenNavigator => _screenNavigator;
    public StoryEndScreenReferences References => _references;
    public StoryEndScreenLayoutSettings LayoutSettings => _layoutSettings;
    public StoryEndScreenPreviewSettings PreviewSettings => _previewSettings;
    public IReadOnlyList<StoryEndScreenStatBinding> StatBindings => _statBindings;
    public Button ContinueButton => _continueButton;
    public Button MenuButton => _menuButton;
    public TMP_Text TitleText => _titleText;
    public TMP_Text TownText => _storyManager != null ? _storyManager.townText : null;

    void Awake()
    {
        EnsureState();
        EnsureReferences();
    }

    void OnEnable()
    {
        EnsureState();
        EnsureReferences();
        RegisterButtons();
        AutoFillEndScreenReferencesFromHierarchy();

        if (Application.isPlaying)
        {
            EnsureActivationRelay();

            // This controller is usually attached to UIRoot while the actual EndScreen
            // is a child GameObject. UIRoot.OnEnable fires only once at scene startup,
            // so it must NOT be treated as the lifecycle event for every chapter end.
            GameObject root = _references != null ? _references.ResolveRoot(this) : null;
            if (root != null && root.activeInHierarchy)
                ShowRuntime(nameof(OnEnable));
        }
        else
        {
            Refresh();
        }
    }

    void OnDisable()
    {
        ClearButtonListeners();
        StoryEndScreenTweenController.KillHierarchy(gameObject);
    }

    void Reset()
    {
        EnsureState();
        EnsureReferences();
        AutoFillEndScreenReferencesFromHierarchy();
    }

    void OnValidate()
    {
        EnsureState();
        if (string.IsNullOrWhiteSpace(_completionTitle))
            _completionTitle = DefaultCompletionTitle;
        RemoveStatRowExtraTextBindings();
        RestoreStatRowPreviewTextsInEditMode();
    }

    public void Refresh()
    {
        EnsureState();
        EnsureReferences();

        bool preview = !Application.isPlaying;
        StoryEndScreenData data = _dataProvider.Build(_storyManager, _statBindings, _previewSettings, preview);
        RenderData(data, animate: false, reason: nameof(Refresh));
    }

    public void RefreshTexts()
    {
        Refresh();
    }

    public bool ShowStaticPreview(string reason = "EditModePreview")
    {
        EnsureState();
        EnsureReferences();
        AutoFillEndScreenReferencesFromHierarchy();

        StoryEndScreenValidationResult validation = ValidateEndScreen(requireRuntime: false);
        LogValidation(validation, reason);
        if (validation.HasErrors)
            return false;

        StoryEndScreenData data = _dataProvider.Build(_storyManager, _statBindings, _previewSettings, preview: true);
        return _previewRenderer.Render(this, data, reason);
    }

    public bool ShowRuntime(string reason = "Runtime")
    {
        if (_runtimeRenderInProgress)
            return false;

        _runtimeRenderInProgress = true;
        try
        {
            EnsureState();
            EnsureReferences();
            AutoFillEndScreenReferencesFromHierarchy();
            EnsureActivationRelay();

            StoryEndScreenValidationResult validation = ValidateEndScreen(requireRuntime: true);
            LogValidation(validation, reason);
            if (validation.HasErrors)
                return false;

            // EndScreen must prepare its own data instead of relying on a hidden
            // chapter-completion call ordering somewhere else in StoryManager.
            _storyManager?.PrepareEpisodeSummaryForEndScreen();

            StoryEndScreenData data = _dataProvider.Build(_storyManager, _statBindings, _previewSettings, preview: false);
            bool rendered = _runtimePresenter.Show(this, data, reason);

            if (Debug.isDebugBuild || Application.isEditor)
            {
                Debug.Log(
                    $"[END_STATS][CONTROLLER_RENDER] reason='{reason ?? ""}' rendered={rendered} " +
                    $"controllerObject='{name}' root='{(_references != null && _references.ResolveRoot(this) != null ? _references.ResolveRoot(this).name : "<null>")}' " +
                    $"statCount={(data != null ? data.Stats.Count : 0)}.",
                    this);
            }

            return rendered;
        }
        finally
        {
            _runtimeRenderInProgress = false;
        }
    }

    internal void NotifyEndScreenRootEnabled(GameObject activatedRoot)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || _runtimeRenderInProgress)
            return;

        GameObject expectedRoot = _references != null ? _references.ResolveRoot(this) : null;
        if (expectedRoot == null || activatedRoot != expectedRoot)
            return;

        if (Debug.isDebugBuild || Application.isEditor)
        {
            Debug.Log(
                $"[END_STATS][ACTIVATION] EndScreen root enabled. controllerObject='{name}' root='{activatedRoot.name}'.",
                this);
        }

        ShowRuntime("EndScreenRoot.OnEnable");
    }

    void EnsureActivationRelay()
    {
        if (!Application.isPlaying || _references == null)
            return;

        GameObject root = _references.ResolveRoot(this);
        if (root == null || root == gameObject)
            return;

        if (_activationRelay == null || _activationRelay.gameObject != root)
        {
            _activationRelay = root.GetComponent<StoryEndScreenActivationRelay>();
            if (_activationRelay == null)
                _activationRelay = root.AddComponent<StoryEndScreenActivationRelay>();
        }

        _activationRelay.Bind(this);
    }

    public void CaptureCurrentStatBackplateSprites(bool overwriteExisting = true)
    {
        EnsureState();
        AutoFillEndScreenReferencesFromHierarchy();

        AssignPlateSpriteFromImage("city", _references.legacyCityImage, overwriteExisting);
        AssignPlateSpriteFromImage("fairytale", _references.legacyFairytaleImage, overwriteExisting);
        AssignPlateSpriteFromImage("reputation", _references.legacyReputationImage, overwriteExisting);
        AssignPlateSpriteFromImage("hearts", _references.legacySparksImage, overwriteExisting);
        AssignPlateSpriteFromImage("candles", _references.legacyCandlesImage, overwriteExisting);
    }

    public void CaptureCurrentStatVisualSprites(bool overwriteExisting = true)
    {
        EnsureState();
        AutoFillEndScreenReferencesFromHierarchy();
        CaptureExplicitStatBindingVisuals(overwriteExisting);
        CaptureCurrentStatBackplateSprites(overwriteExisting);

        AssignIconSpriteFromImage("city", _references.legacyCityIconImage, overwriteExisting);
        AssignIconSpriteFromImage("fairytale", _references.legacyFairytaleIconImage, overwriteExisting);
        AssignIconSpriteFromImage("reputation", _references.legacyReputationIconImage, overwriteExisting);
        AssignIconSpriteFromImage("hearts", _references.legacySparksIconImage, overwriteExisting);
        AssignIconSpriteFromImage("candles", _references.legacyCandlesIconImage, overwriteExisting);
    }

    void CaptureExplicitStatBindingVisuals(bool overwriteExisting)
    {
        if (_statBindings == null)
            return;

        for (int i = 0; i < _statBindings.Length; i++)
        {
            StoryEndScreenStatBinding binding = _statBindings[i];
            if (binding == null)
                continue;

            if ((overwriteExisting || binding.backgroundSprite == null) && binding.backgroundImage != null && binding.backgroundImage.sprite != null)
            {
                binding.backgroundSprite = binding.backgroundImage.sprite;
                binding.backgroundSpriteSource = binding.backgroundImage.sprite;
            }
            if ((overwriteExisting || binding.plateSprite == null) && binding.plateImage != null && binding.plateImage.sprite != null)
            {
                binding.plateSprite = binding.plateImage.sprite;
                binding.plateSpriteSource = binding.plateImage.sprite;
            }
            if ((overwriteExisting || binding.icon == null) && binding.iconImage != null && binding.iconImage.sprite != null)
            {
                binding.icon = binding.iconImage.sprite;
                binding.iconSpriteSource = binding.iconImage.sprite;
            }
        }
    }

    public void ApplyConfiguredStatBackplatesToScene()
    {
        ApplyConfiguredStatVisualsToScene();
    }

    public void ApplyConfiguredStatVisualsToScene()
    {
        EnsureState();
        EnsureReferences();

        GameObject root = _references.root != null
            ? _references.root
            : _storyManager != null && _storyManager.endStoryPanel != null ? _storyManager.endStoryPanel : gameObject;
        AutoFillLegacyStatReferences(root != null ? root.transform : transform);

        StoryEndScreenData data = _dataProvider.Build(_storyManager, _statBindings, _previewSettings, preview: !Application.isPlaying);
        ApplyStatsBackground();
        ApplyContinueButtonVisual();
        ApplyLegacyStatSprites(data);
        UpdateExplicitStatBindings(data);
        StoryEndScreenBackgroundController.Apply(this, data);
    }

    public void RenderData(StoryEndScreenData data, bool animate, string reason)
    {
        if (data == null)
            return;

        EnsureState();
        EnsureReferences();
        HideConflictingStoryUi(data.IsPreview);

        GameObject root = _references.ResolveRoot(this);
        if (root != null && !root.activeSelf)
            root.SetActive(true);

        StoryEndScreenTweenController.KillHierarchy(root != null ? root : gameObject);
        ClearGeneratedRows();
        SetTemplatesInactive();

        ApplyMainTexts(data);
        ApplyMainTextStyles();
        ApplyStatsBackground();
        ApplyContinueButtonVisual();
        RenderStats(data);
        RefreshExtraTexts(data);

        // Extra text bindings are intentionally rendered after the stat rows, but a
        // legacy scene can still have one of those bindings pointing at the same TMP
        // object as a completion stat. In that case the binding used to overwrite the
        // freshly rendered chapter delta with its stale/current-total value (usually 0).
        // Re-apply completion stats as the final text pass so EndScreen owns those rows.
        ReapplyCompletionStatTexts(data, reason);

        RefreshButtonAvailability();
        StoryEndScreenBackgroundController.Apply(this, data);
        StoryEndScreenLayoutController.Recalculate(this, reason);
        StoryEndScreenTweenController.FadeIn(_references.canvasGroup, animate && Application.isPlaying);

        AppLogger.Info(
            AppLogCategory.EndScreen,
            nameof(StoryEndScreenController),
            nameof(RenderData),
            "Story end screen rendered.",
            LogMetadata.Of(
                "reason", reason ?? "",
                "preview", data.IsPreview,
                "storyId", data.StoryId ?? "",
                "completedEpisodeId", data.CompletedEpisodeId ?? "",
                "statCount", data.Stats.Count));
    }

    public void Hide()
    {
        GameObject root = _references != null ? _references.ResolveRoot(this) : gameObject;
        StoryEndScreenTweenController.KillHierarchy(root != null ? root : gameObject);
        ClearGeneratedRows();
        if (root != null)
            root.SetActive(false);
    }

    public void ReturnToMenu()
    {
        if (_navigation.ReturnToMenu(this))
            return;

        AppLogger.Warn(
            AppLogCategory.EndScreen,
            nameof(StoryEndScreenController),
            nameof(ReturnToMenu),
            "Menu target is not assigned for story end screen.",
            LogMetadata.Of("object", name),
            recoverable: true);
    }

    public void ContinueOrReturnToMenu()
    {
        if (!_navigation.ContinueOrReturnToMenu(this))
        {
            AppLogger.Warn(
                AppLogCategory.EndScreen,
                nameof(StoryEndScreenController),
                nameof(ContinueOrReturnToMenu),
                "Cannot continue or return to menu from story end screen.",
                LogMetadata.Of("object", name),
                recoverable: true);
        }

    }

    public void ContinueStory()
    {
        if (!_navigation.ContinueStory(this))
        {
            AppLogger.Warn(
                AppLogCategory.EndScreen,
                nameof(StoryEndScreenController),
                nameof(ContinueStory),
                "Cannot continue from story end screen.",
                LogMetadata.Of("object", name),
                recoverable: true);
        }

        Refresh();
    }

    public void RestartCompletedEpisode()
    {
        if (!_navigation.RestartCompletedEpisode(this))
        {
            AppLogger.Warn(
                AppLogCategory.EndScreen,
                nameof(StoryEndScreenController),
                nameof(RestartCompletedEpisode),
                "Cannot restart completed episode from story end screen.",
                LogMetadata.Of("object", name),
                recoverable: true);
        }

        Refresh();
    }

    public void CloseEndPanel()
    {
        if (_storyManager != null && _storyManager.endStoryPanel != null)
        {
            _storyManager.CloseEndPanel();
            return;
        }

        Hide();
    }

    public bool OpenScreen(string screenId)
    {
        EnsureReferences();

        if (string.IsNullOrWhiteSpace(screenId))
        {
            AppLogger.Warn(
                AppLogCategory.EndScreen,
                nameof(StoryEndScreenController),
                nameof(OpenScreen),
                "Target screen id is empty.",
                LogMetadata.Of("object", name),
                recoverable: true);
            return false;
        }

        bool opened = _navigation.OpenScreen(this, screenId);
        if (!opened)
        {
            AppLogger.Warn(
                AppLogCategory.EndScreen,
                nameof(StoryEndScreenController),
                nameof(OpenScreen),
                "Story screen navigator is missing or rejected target screen.",
                LogMetadata.Of("screenId", screenId, "object", name),
                recoverable: true);
        }

        return opened;
    }

    public void ConfigureFromStoryUserInterface(StoryUserInterface owner, string reason = "StoryUserInterface")
    {
        if (owner == null)
            return;

        EnsureState();
        CaptureSerializedConfigurationSnapshot();
        _useUnifiedContinueButton = true;

        StoryEndScreenReferences ownerReferences = owner.EndScreenReferences;
        StoryEndScreenLayoutSettings ownerLayoutSettings = owner.EndScreenLayoutSettings;
        StoryEndScreenPreviewSettings ownerPreviewSettings = owner.EndScreenPreviewSettings;
        StoryEndScreenStatBinding[] ownerStatBindings = owner.EndScreenStatBindings;

        if (ownerReferences != null)
            _references = ownerReferences;
        if (ownerLayoutSettings != null)
            _layoutSettings = ownerLayoutSettings;
        if (ownerPreviewSettings != null)
            _previewSettings = ownerPreviewSettings;
        if (ownerStatBindings != null && ownerStatBindings.Length > 0)
            _statBindings = ownerStatBindings;

        RefreshSerializedConfigurationSnapshotFromCurrent();
        EnsureState();
        EnsureReferences();
        AutoFillEndScreenReferencesFromHierarchy();

        if (isActiveAndEnabled)
            RegisterButtons();

        AppLogger.Info(
            AppLogCategory.EndScreen,
            nameof(StoryEndScreenController),
            nameof(ConfigureFromStoryUserInterface),
            "Story end screen configuration applied from StoryUserInterface.",
            LogMetadata.Of("owner", owner.name, "controller", name, "reason", reason ?? ""));
    }

    public void ApplyStoryUiStyle(StoryUiStyle style, string storyId = "", bool preview = false)
    {
        EnsureState();
        EnsureReferences();
        CaptureSerializedConfigurationSnapshot();
        CaptureSceneVisualSnapshots();
        RestoreSerializedConfigurationSnapshot();
        RestoreSceneVisualSnapshots();
        _rectSnapshots.Clear();

        _activeStoryStyle = style != null ? style.EndScreenStyle : null;
        _activeStoryStyleId = storyId ?? "";

        if (_activeStoryStyle != null && _activeStoryStyle.HasOverrides)
            ApplyActiveStoryStyleToConfiguration();

        EnsureState();
        EnsureReferences();
        AutoFillEndScreenReferencesFromHierarchy();

        if (preview && !Application.isPlaying)
            ShowStaticPreview("StoryUiStylePreview:" + _activeStoryStyleId);
        else if (isActiveAndEnabled)
            Refresh();
    }

    public void RefreshSerializedConfigurationSnapshotForEditor(string reason = "Editor")
    {
        EnsureState();
        EnsureReferences();
        RefreshSerializedConfigurationSnapshotFromCurrent();
        _rectSnapshots.Clear();

        AppLogger.Info(
            AppLogCategory.EndScreen,
            nameof(StoryEndScreenController),
            nameof(RefreshSerializedConfigurationSnapshotForEditor),
            "Story end screen serialized snapshot refreshed.",
            LogMetadata.Of("controller", name, "reason", reason ?? ""));
    }

    public void RestoreSerializedConfigurationSnapshotForEditor(string reason = "Editor")
    {
        EnsureState();
        EnsureReferences();
        CaptureSerializedConfigurationSnapshot();
        RestoreSerializedConfigurationSnapshot();
        _rectSnapshots.Clear();
    }

    public StoryEndScreenStatBinding[] CopySerializedConfigurationTo(
        StoryEndScreenReferences targetReferences,
        StoryEndScreenLayoutSettings targetLayoutSettings,
        StoryEndScreenPreviewSettings targetPreviewSettings,
        StoryEndScreenStatBinding[] targetStatBindings,
        bool overwrite = false)
    {
        EnsureState();
        StoryEndScreenReferences sourceReferences = _serializedReferencesSnapshot ?? _references;
        StoryEndScreenLayoutSettings sourceLayoutSettings = _serializedLayoutSettingsSnapshot ?? _layoutSettings;
        StoryEndScreenPreviewSettings sourcePreviewSettings = _serializedPreviewSettingsSnapshot ?? _previewSettings;
        StoryEndScreenStatBinding[] sourceStatBindings = _serializedStatBindingsSnapshot ?? _statBindings;

        CopyReferences(sourceReferences, targetReferences, overwrite);
        CopyLayoutSettings(sourceLayoutSettings, targetLayoutSettings);
        CopyPreviewSettings(sourcePreviewSettings, targetPreviewSettings);

        if (overwrite || targetStatBindings == null || targetStatBindings.Length == 0)
            return CloneStatBindings(sourceStatBindings);

        return targetStatBindings;
    }

    public void AutoFillEndScreenReferencesFromHierarchy()
    {
        EnsureState();
        EnsureReferences();

        GameObject root = _references.root != null
            ? _references.root
            : _storyManager != null && _storyManager.endStoryPanel != null ? _storyManager.endStoryPanel : gameObject;
        Transform searchRoot = root != null ? root.transform : transform;

        _references.root = root;
        if (_references.canvasGroup == null && root != null)
            _references.canvasGroup = root.GetComponent<CanvasGroup>();
        if (_references.panelRoot == null && root != null)
            _references.panelRoot = root.GetComponent<RectTransform>();
        if (_references.safeArea == null)
            _references.safeArea = FindRect(searchRoot, "safe", "area", "panel");
        if (_references.backgroundImage == null)
            _references.backgroundImage = FindImage(searchRoot, "background", "fon", "bg", "final");

        if (_references.titleText == null)
            _references.titleText = FirstNonNull(_titleText, FindTextByTokens(searchRoot, "title", "completed", "заверш"));
        if (_references.storyTitleText == null)
            _references.storyTitleText = FirstNonNull(_storyTitleText, FindTextByTokens(searchRoot, "story", "история"));
        if (_references.completedEpisodeText == null)
            _references.completedEpisodeText = FirstNonNull(_completedEpisodeText, FindTextByTokens(searchRoot, "episode", "chapter", "глава"));
        if (_references.nextEpisodeText == null)
            _references.nextEpisodeText = FirstNonNull(_nextEpisodeText, FindTextByTokens(searchRoot, "next", "след"));

        if (_references.legacyCityText == null)
            _references.legacyCityText = FirstNonNull(_storyManager != null ? _storyManager.townText : null, FindStatText(searchRoot, "город", "city", "town"));
        if (_references.legacyFairytaleText == null)
            _references.legacyFairytaleText = FirstNonNull(_storyManager != null ? _storyManager.storyText : null, FindStatText(searchRoot, "сказка", "fairytale", "story"));
        if (_references.legacyReputationText == null)
            _references.legacyReputationText = FirstNonNull(_storyManager != null ? _storyManager.reputationText : null, FindStatText(searchRoot, "репутация", "reputation", "respect"));
        if (_references.legacySparksText == null)
            _references.legacySparksText = FirstNonNull(_storyManager != null ? _storyManager.heartsText : null, FindStatText(searchRoot, "искры", "hearts", "sparks"));

        AutoFillLegacyStatReferences(searchRoot);

        if (_references.statsContainer == null)
            _references.statsContainer = ResolveStatsContainer(searchRoot);
        if (_references.statRowTemplate == null)
            _references.statRowTemplate = FindTemplate(searchRoot);
        if (_references.statsBackgroundImage == null)
            _references.statsBackgroundImage = FindImage(searchRoot, "statsbackground", "statbackground", "stats_bg", "stat_bg", "stats");

        AutoFillStatBindingReferences(searchRoot);
        SeedStatBindingSpritesFromLegacyImages();

        if (_references.continueButton == null)
            _references.continueButton = FirstNonNull(_continueButton, FindButton(searchRoot, "continue", "next", "прод"));
        if (_references.menuButton == null)
            _references.menuButton = FirstNonNull(_menuButton, FindButton(searchRoot, "menu", "меню"));
        if (_references.nextEpisodeButton == null)
            _references.nextEpisodeButton = FirstNonNull(_nextEpisodeButton, FindButton(searchRoot, "next", "continue", "прод"));
        if (_references.restartEpisodeButton == null)
            _references.restartEpisodeButton = FirstNonNull(_restartEpisodeButton, FindButton(searchRoot, "restart", "replay", "заново"));

        NormalizeUnifiedButtonReferences(searchRoot);
        AutoFillContinueButtonVisualReferences();
        RemoveStatRowExtraTextBindings();
        MirrorReferencesToLegacyFields();
        SetTemplatesInactive();
    }

    public StoryEndScreenValidationResult ValidateEndScreen(bool requireRuntime = false)
    {
        EnsureState();
        EnsureReferences();
        return _validator.Validate(this, requireRuntime);
    }

    public void RecalculateLayout(string reason = "Manual")
    {
        EnsureState();
        EnsureReferences();
        SetTemplatesInactive();
        StoryEndScreenLayoutController.Recalculate(this, reason);
    }

    void EnsureState()
    {
        if (_references == null)
            _references = new StoryEndScreenReferences();
        if (_layoutSettings == null)
            _layoutSettings = new StoryEndScreenLayoutSettings();
        if (_previewSettings == null)
            _previewSettings = new StoryEndScreenPreviewSettings();
        if (_statBindings == null || _statBindings.Length == 0)
            _statBindings = StoryEndScreenStatBinding.CreateDefaults();
        MigrateLegacyCompletionStatModes();
        if (_extraButtons == null)
            _extraButtons = Array.Empty<ButtonBinding>();
        if (_extraTexts == null)
            _extraTexts = Array.Empty<TextBinding>();
        if (string.IsNullOrWhiteSpace(_completionTitle))
            _completionTitle = DefaultCompletionTitle;
    }


    void MigrateLegacyCompletionStatModes()
    {
        if (_statBindings == null)
            return;

        bool changed = false;
        for (int i = 0; i < _statBindings.Length; i++)
        {
            StoryEndScreenStatBinding binding = _statBindings[i];
            if (binding == null)
                continue;

            if (BindingContainsAnyStatId(binding, "city", "town", "gorod") || binding.MatchesLabel("Город"))
            {
                if (string.IsNullOrWhiteSpace(binding.statId))
                    binding.statId = "city";
                if (binding.valueMode != StoryEndScreenStatValueMode.EpisodeDelta)
                {
                    binding.valueMode = StoryEndScreenStatValueMode.EpisodeDelta;
                    changed = true;
                }
                continue;
            }

            if (BindingContainsAnyStatId(binding, "fairytale", "story", "tale", "skazka") || binding.MatchesLabel("Сказка"))
            {
                if (string.IsNullOrWhiteSpace(binding.statId))
                    binding.statId = "fairytale";
                if (binding.valueMode != StoryEndScreenStatValueMode.EpisodeDelta)
                {
                    binding.valueMode = StoryEndScreenStatValueMode.EpisodeDelta;
                    changed = true;
                }
                continue;
            }

            if (BindingContainsAnyStatId(binding, "reputation", "respect", "rep") || binding.MatchesLabel("Репутация"))
            {
                if (string.IsNullOrWhiteSpace(binding.statId))
                    binding.statId = "reputation";
                if (binding.valueMode != StoryEndScreenStatValueMode.EpisodeDelta)
                {
                    binding.valueMode = StoryEndScreenStatValueMode.EpisodeDelta;
                    changed = true;
                }
                continue;
            }

            if (BindingContainsAnyStatId(binding, "hearts", "sparks") || binding.MatchesLabel("Искры"))
            {
                if (string.IsNullOrWhiteSpace(binding.statId))
                    binding.statId = "hearts";
                if (binding.valueMode != StoryEndScreenStatValueMode.HeartDelta)
                {
                    binding.valueMode = StoryEndScreenStatValueMode.HeartDelta;
                    changed = true;
                }
            }
        }

        if (changed && Application.isPlaying)
        {
            Debug.Log(
                $"[END_STATS][BINDINGS_MIGRATED] object='{name}' standard completion rows forced to chapter delta modes.",
                this);

            AppLogger.Info(
                AppLogCategory.EndScreen,
                nameof(StoryEndScreenController),
                nameof(MigrateLegacyCompletionStatModes),
                "[END_STATS][BINDINGS_MIGRATED] Standard end-screen stat bindings were forced to chapter delta modes.",
                LogMetadata.Of("object", name));
        }
    }

    static bool BindingContainsAnyStatId(StoryEndScreenStatBinding binding, params string[] ids)
    {
        if (binding == null || ids == null)
            return false;

        foreach (string candidate in binding.AllStatIds())
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(candidate, ids[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    void EnsureReferences()
    {
        if (_storyManager == null)
            _storyManager = StoryManager.Instance != null ? StoryManager.Instance : FindObjectOfType<StoryManager>(true);
        if (_menuController == null && _storyManager != null)
            _menuController = _storyManager.menuController;
        if (_menuController == null)
            _menuController = FindObjectOfType<MenuController>(true);
        if (_screenNavigator == null)
            _screenNavigator = FindObjectOfType<StoryScreenNavigator>(true);

        if (_references.root == null && _storyManager != null && _storyManager.endStoryPanel != null)
            _references.root = _storyManager.endStoryPanel;
        if (_references.root == null)
            _references.root = gameObject;

        if (_references.canvasGroup == null && _references.root != null)
            _references.canvasGroup = _references.root.GetComponent<CanvasGroup>();

        MirrorReferencesToLegacyFields();
    }

    void MirrorReferencesToLegacyFields()
    {
        if (_references == null)
            return;

        if (_continueButton == null)
            _continueButton = _references.continueButton;
        if (_menuButton == null)
            _menuButton = _references.menuButton;
        if (_nextEpisodeButton == null)
            _nextEpisodeButton = _references.nextEpisodeButton;
        if (_restartEpisodeButton == null)
            _restartEpisodeButton = _references.restartEpisodeButton;
        if (_titleText == null)
            _titleText = _references.titleText;
        if (_storyTitleText == null)
            _storyTitleText = _references.storyTitleText;
        if (_completedEpisodeText == null)
            _completedEpisodeText = _references.completedEpisodeText;
        if (_nextEpisodeText == null)
            _nextEpisodeText = _references.nextEpisodeText;
    }

    void SeedStatBindingSpritesFromLegacyImages()
    {
        AssignPlateSpriteFromImage("city", _references.legacyCityImage, overwriteExisting: false);
        AssignPlateSpriteFromImage("fairytale", _references.legacyFairytaleImage, overwriteExisting: false);
        AssignPlateSpriteFromImage("reputation", _references.legacyReputationImage, overwriteExisting: false);
        AssignPlateSpriteFromImage("hearts", _references.legacySparksImage, overwriteExisting: false);
        AssignPlateSpriteFromImage("candles", _references.legacyCandlesImage, overwriteExisting: false);
        AssignIconSpriteFromImage("city", _references.legacyCityIconImage, overwriteExisting: false);
        AssignIconSpriteFromImage("fairytale", _references.legacyFairytaleIconImage, overwriteExisting: false);
        AssignIconSpriteFromImage("reputation", _references.legacyReputationIconImage, overwriteExisting: false);
        AssignIconSpriteFromImage("hearts", _references.legacySparksIconImage, overwriteExisting: false);
        AssignIconSpriteFromImage("candles", _references.legacyCandlesIconImage, overwriteExisting: false);
    }

    void AutoFillLegacyStatReferences(Transform searchRoot)
    {
        AutoFillLegacyStatRowReferences(searchRoot);
        AutoFillLegacyStatImageReferences(searchRoot);
        AutoFillLegacyStatIconReferences(searchRoot);
    }

    void AutoFillLegacyStatRowReferences(Transform searchRoot)
    {
        if (_references.legacyCityRow == null)
            _references.legacyCityRow = FindLegacyStatRow(searchRoot, _references.legacyCityText, _references.legacyCityImage, "cityfinalstat", "city", "town");
        if (_references.legacyFairytaleRow == null)
            _references.legacyFairytaleRow = FindLegacyStatRow(searchRoot, _references.legacyFairytaleText, _references.legacyFairytaleImage, "fairytalefinalstat", "fairytale", "story", "tale");
        if (_references.legacyReputationRow == null)
            _references.legacyReputationRow = FindLegacyStatRow(searchRoot, _references.legacyReputationText, _references.legacyReputationImage, "respectfinalstat", "reputation", "respect", "rep");
        if (_references.legacySparksRow == null)
            _references.legacySparksRow = FindLegacyStatRow(searchRoot, _references.legacySparksText, _references.legacySparksImage, "sparksfinalstat", "heartsfinalstat", "hearts", "sparks");
        if (_references.legacyCandlesRow == null)
            _references.legacyCandlesRow = FindLegacyStatRow(searchRoot, _references.legacyCandlesText, _references.legacyCandlesImage, "candlesfinalstat", "candles", "candle");
    }

    void AutoFillLegacyStatImageReferences(Transform searchRoot)
    {
        if (_references.legacyCityImage == null)
            _references.legacyCityImage = FindLegacyStatBackplateImage(searchRoot, _references.legacyCityRow, "city", "town");
        if (_references.legacyFairytaleImage == null)
            _references.legacyFairytaleImage = FindLegacyStatBackplateImage(searchRoot, _references.legacyFairytaleRow, "story", "fairytale", "tale");
        if (_references.legacyReputationImage == null)
            _references.legacyReputationImage = FindLegacyStatBackplateImage(searchRoot, _references.legacyReputationRow, "respect", "reputation", "rep");
        if (_references.legacySparksImage == null)
            _references.legacySparksImage = FindLegacyStatBackplateImage(searchRoot, _references.legacySparksRow, "hearts", "sparks");
        if (_references.legacyCandlesImage == null)
            _references.legacyCandlesImage = FindLegacyStatBackplateImage(searchRoot, _references.legacyCandlesRow, "candles", "candle");
    }

    void AutoFillLegacyStatIconReferences(Transform searchRoot)
    {
        if (_references.legacyCityIconImage == null)
            _references.legacyCityIconImage = FindLegacyStatIconImage(searchRoot, _references.legacyCityRow, "city", "town");
        if (_references.legacyFairytaleIconImage == null)
            _references.legacyFairytaleIconImage = FindLegacyStatIconImage(searchRoot, _references.legacyFairytaleRow, "story", "fairytale", "tale");
        if (_references.legacyReputationIconImage == null)
            _references.legacyReputationIconImage = FindLegacyStatIconImage(searchRoot, _references.legacyReputationRow, "respect", "reputation", "rep");
        if (_references.legacySparksIconImage == null)
            _references.legacySparksIconImage = FindLegacyStatIconImage(searchRoot, _references.legacySparksRow, "hearts", "sparks");
        if (_references.legacyCandlesIconImage == null)
            _references.legacyCandlesIconImage = FindLegacyStatIconImage(searchRoot, _references.legacyCandlesRow, "candles", "candle");
    }

    void AutoFillStatBindingReferences(Transform searchRoot)
    {
        if (_statBindings == null || _statBindings.Length == 0)
            return;

        for (int i = 0; i < _statBindings.Length; i++)
        {
            StoryEndScreenStatBinding binding = _statBindings[i];
            if (binding == null)
                continue;

            if (binding.row == null)
                binding.row = FindStatRowForBinding(searchRoot, binding);

            if (binding.row != null)
                AutoFillStatBindingFromRow(binding, binding.row);

            SanitizePlateIconTargets(binding);
        }
    }

    RectTransform FindStatRowForBinding(Transform searchRoot, StoryEndScreenStatBinding binding)
    {
        if (binding == null)
            return null;

        RectTransform mappedRow = FindMappedStatRow(binding);
        if (mappedRow != null)
            return mappedRow;

        string[] tokens = BuildStatBindingSearchTokens(binding);
        return tokens.Length > 0
            ? FindLegacyStatRow(searchRoot, binding.lineText, FirstNonNull(binding.plateImage, binding.backgroundImage, binding.iconImage), tokens)
            : null;
    }

    RectTransform FindMappedStatRow(StoryEndScreenStatBinding binding)
    {
        if (binding == null || _references == null)
            return null;

        foreach (string id in binding.AllStatIds())
        {
            if (MatchesAnyStatId(id, "city", "town", "gorod", "self_esteem", "selfesteem", "self", "esteem", "samoocenka"))
                return _references.legacyCityRow;
            if (MatchesAnyStatId(id, "fairytale", "story", "tale", "skazka", "principles", "principle", "princip"))
                return _references.legacyFairytaleRow;
            if (MatchesAnyStatId(id, "reputation", "respect", "rep", "feelings", "feeling", "feel", "feels"))
                return _references.legacyReputationRow;
            if (MatchesAnyStatId(id, "hearts", "heart", "sparks", "spark"))
                return _references.legacySparksRow;
            if (MatchesAnyStatId(id, "candles", "candle"))
                return _references.legacyCandlesRow;
        }

        return null;
    }

    static string[] BuildStatBindingSearchTokens(StoryEndScreenStatBinding binding)
    {
        if (binding == null)
            return Array.Empty<string>();

        var tokens = new List<string>();
        foreach (string id in binding.AllStatIds())
            AddStatSearchTokens(tokens, id);
        AddStatSearchTokens(tokens, binding.label);
        return tokens.ToArray();
    }

    static void AddStatSearchTokens(List<string> tokens, string value)
    {
        if (tokens == null || string.IsNullOrWhiteSpace(value))
            return;

        string token = value.Trim();
        if (!ContainsToken(tokens, token))
            tokens.Add(token);

        if (MatchesAnyStatId(token, "city", "town", "gorod", "self_esteem", "selfesteem", "self", "esteem", "samoocenka"))
            AddTokens(tokens, "cityfinalstat", "city", "town");
        else if (MatchesAnyStatId(token, "fairytale", "story", "tale", "skazka", "principles", "principle", "princip"))
            AddTokens(tokens, "fairytalefinalstat", "fairytale", "story", "tale");
        else if (MatchesAnyStatId(token, "reputation", "respect", "rep", "feelings", "feeling", "feel", "feels"))
            AddTokens(tokens, "respectfinalstat", "reputation", "respect", "rep");
        else if (MatchesAnyStatId(token, "hearts", "heart", "sparks", "spark"))
            AddTokens(tokens, "sparkfinalstat", "sparksfinalstat", "heartsfinalstat", "hearts", "sparks", "spark");
        else if (MatchesAnyStatId(token, "candles", "candle"))
            AddTokens(tokens, "candlesfinalstat", "candles", "candle");
    }

    static void AddTokens(List<string> tokens, params string[] values)
    {
        if (tokens == null || values == null)
            return;

        for (int i = 0; i < values.Length; i++)
        {
            string value = values[i];
            if (!string.IsNullOrWhiteSpace(value) && !ContainsToken(tokens, value))
                tokens.Add(value);
        }
    }

    static bool ContainsToken(List<string> tokens, string value)
    {
        if (tokens == null || string.IsNullOrWhiteSpace(value))
            return false;

        for (int i = 0; i < tokens.Count; i++)
        {
            if (string.Equals(tokens[i], value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static bool MatchesAnyStatId(string value, params string[] candidates)
    {
        if (string.IsNullOrWhiteSpace(value) || candidates == null)
            return false;

        for (int i = 0; i < candidates.Length; i++)
        {
            if (StoryStatId.EqualsCanonical(value, candidates[i]))
                return true;
        }

        return false;
    }

    static void AutoFillStatBindingFromRow(StoryEndScreenStatBinding binding, RectTransform row)
    {
        if (binding == null || row == null)
            return;

        if (binding.backgroundImage == null)
            binding.backgroundImage = FindStatRowImageByRole(row, EndScreenImageRole.Background);
        if (binding.plateImage == null)
            binding.plateImage = FindStatRowImageByRole(row, EndScreenImageRole.Plate);
        if (binding.iconImage == null)
            binding.iconImage = FindStatRowImageByRole(row, EndScreenImageRole.Icon);
        if (binding.plateImage != null && binding.iconImage == binding.plateImage)
            binding.iconImage = null;

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text singleText = ResolveSingleStatText(texts);
        if (singleText != null)
        {
            if (binding.lineText == null)
                binding.lineText = singleText;
            return;
        }

        if (texts == null || texts.Length == 0)
            return;

        if (binding.lineText == null && texts.Length == 1)
        {
            binding.lineText = texts[0];
            return;
        }

        if (binding.labelText == null)
            binding.labelText = FindTextByTokens(row, "label", "name", "title", "назв");
        if (binding.valueText == null)
            binding.valueText = FindTextByTokens(row, "value", "count", "amount", "number", "знач", "число");
        if (binding.labelText == null)
            binding.labelText = FirstNonNumericText(texts);
        if (binding.valueText == null)
            binding.valueText = FirstNumericText(texts);
        if (binding.valueText == null && texts.Length > 0)
            binding.valueText = texts[texts.Length - 1];
    }

    static TMP_Text ResolveSingleStatText(TMP_Text[] texts)
    {
        if (texts == null || texts.Length == 0)
            return null;
        if (texts.Length == 1)
            return texts[0];

        TMP_Text result = null;
        int usableCount = 0;
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || !text.enabled || text.color.a <= 0.001f)
                continue;

            usableCount++;
            result = text;
            if (usableCount > 1)
                return null;
        }

        return usableCount == 1 ? result : null;
    }

    static TMP_Text FirstNumericText(TMP_Text[] texts)
    {
        if (texts == null)
            return null;

        for (int i = 0; i < texts.Length; i++)
        {
            if (IsMostlyNumericText(texts[i]))
                return texts[i];
        }

        return null;
    }

    static TMP_Text FirstNonNumericText(TMP_Text[] texts)
    {
        if (texts == null)
            return null;

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && !IsMostlyNumericText(texts[i]))
                return texts[i];
        }

        return null;
    }

    static bool IsMostlyNumericText(TMP_Text text)
    {
        if (text == null)
            return false;

        string value = StripRichTextTags(text.text ?? "").Trim();
        if (string.IsNullOrEmpty(value))
            value = text.name ?? "";

        int numeric = 0;
        int letters = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsDigit(c) || c == '+' || c == '-' || c == ':' || char.IsWhiteSpace(c))
                numeric++;
            else if (char.IsLetter(c))
                letters++;
        }

        return numeric > 0 && letters == 0;
    }

    void NormalizeUnifiedButtonReferences(Transform searchRoot)
    {
        if (_references == null)
            return;

        Button continueButton = FirstNonNull(
            _references.continueButton,
            _continueButton,
            IsContinueButton(_references.menuButton) ? _references.menuButton : null,
            IsContinueButton(_references.nextEpisodeButton) ? _references.nextEpisodeButton : null,
            FindButton(searchRoot, "continue", "next", "прод"));

        if (continueButton == null)
            return;

        _references.continueButton = continueButton;
        if (_continueButton == null)
            _continueButton = continueButton;

        if (_references.menuButton == continueButton || IsContinueButton(_references.menuButton))
            _references.menuButton = null;
        if (_references.nextEpisodeButton == continueButton || IsContinueButton(_references.nextEpisodeButton))
            _references.nextEpisodeButton = null;
        if (_references.restartEpisodeButton == continueButton)
            _references.restartEpisodeButton = null;
        if (_menuButton == continueButton || IsContinueButton(_menuButton))
            _menuButton = null;
        if (_nextEpisodeButton == continueButton || IsContinueButton(_nextEpisodeButton))
            _nextEpisodeButton = null;
        if (_restartEpisodeButton == continueButton)
            _restartEpisodeButton = null;
    }

    void AutoFillContinueButtonVisualReferences()
    {
        if (_references == null)
            return;

        Button button = FirstNonNull(_references.continueButton, _continueButton);
        if (button == null)
            return;

        if (_references.continueButtonPlateImage == null)
            _references.continueButtonPlateImage = ResolveContinueButtonPlateImage();

        if (_references.continueButtonText == null)
            _references.continueButtonText = button.GetComponentInChildren<TMP_Text>(true);

        if (_references.continueButtonPlateSprite == null &&
            _references.continueButtonPlateImage != null &&
            _references.continueButtonPlateImage.sprite != null)
        {
            _references.continueButtonPlateSprite = _references.continueButtonPlateImage.sprite;
        }

        if (_references.continueButtonPlateSpriteSource == null && _references.continueButtonPlateSprite != null)
            _references.continueButtonPlateSpriteSource = _references.continueButtonPlateSprite;
    }

    void AssignPlateSpriteFromImage(string statId, Image sourceImage, bool overwriteExisting)
    {
        if (_statBindings == null || sourceImage == null || sourceImage.sprite == null || string.IsNullOrWhiteSpace(statId))
            return;

        for (int i = 0; i < _statBindings.Length; i++)
        {
            StoryEndScreenStatBinding binding = _statBindings[i];
            if (binding == null || (!overwriteExisting && binding.plateSprite != null))
                continue;

            foreach (string candidate in binding.AllStatIds())
            {
                if (!string.Equals(candidate, statId, StringComparison.OrdinalIgnoreCase))
                    continue;

                binding.plateSprite = sourceImage.sprite;
                binding.plateSpriteSource = sourceImage.sprite;
                break;
            }
        }
    }

    void AssignIconSpriteFromImage(string statId, Image sourceImage, bool overwriteExisting)
    {
        if (_statBindings == null || sourceImage == null || sourceImage.sprite == null || string.IsNullOrWhiteSpace(statId))
            return;

        for (int i = 0; i < _statBindings.Length; i++)
        {
            StoryEndScreenStatBinding binding = _statBindings[i];
            if (binding == null || (!overwriteExisting && binding.icon != null))
                continue;

            foreach (string candidate in binding.AllStatIds())
            {
                if (!string.Equals(candidate, statId, StringComparison.OrdinalIgnoreCase))
                    continue;

                binding.icon = sourceImage.sprite;
                binding.iconSpriteSource = sourceImage.sprite;
                break;
            }
        }
    }

    void RegisterButtons()
    {
        if (_registeredButtons)
            ClearButtonListeners();

        Button unifiedButton = FirstNonNull(_continueButton, _references.continueButton);
        if (ShouldUseUnifiedContinueButton(unifiedButton))
        {
            RegisterButton(unifiedButton, ContinueOrReturnToMenu);
        }
        else
        {
            RegisterButton(FirstNonNull(_menuButton, _references.menuButton), ReturnToMenu);
            RegisterButton(FirstNonNull(_nextEpisodeButton, _references.nextEpisodeButton), ContinueStory);
            RegisterButton(FirstNonNull(_restartEpisodeButton, _references.restartEpisodeButton), RestartCompletedEpisode);
        }
        RegisterButton(_references.closeButton, CloseEndPanel);

        for (int i = 0; i < _extraButtons.Length; i++)
        {
            ButtonBinding binding = _extraButtons[i];
            if (binding == null || binding.Button == null)
                continue;

            ButtonBinding captured = binding;
            RegisterButton(captured.Button, () => ExecuteButtonBinding(captured));
        }

        _registeredButtons = true;
    }

    void RegisterButton(Button button, UnityAction action)
    {
        if (button == null || action == null)
            return;

        var registration = new ButtonRegistration(button, action);
        registration.Add();
        _buttonRegistrations.Add(registration);
    }

    void ClearButtonListeners()
    {
        for (int i = 0; i < _buttonRegistrations.Count; i++)
            _buttonRegistrations[i].Remove();

        _buttonRegistrations.Clear();
        _registeredButtons = false;
    }

    void ExecuteButtonBinding(ButtonBinding binding)
    {
        if (binding == null)
            return;

        if (!IsActionAvailable(binding.Action, binding.TargetScreenId))
        {
            RefreshButtonAvailability();
            return;
        }

        switch (binding.Action)
        {
            case StoryEndButtonAction.ReturnToMenu:
                ReturnToMenu();
                break;
            case StoryEndButtonAction.ContinueStory:
                ContinueStory();
                break;
            case StoryEndButtonAction.ContinueOrReturnToMenu:
                ContinueOrReturnToMenu();
                break;
            case StoryEndButtonAction.RestartCompletedEpisode:
                RestartCompletedEpisode();
                break;
            case StoryEndButtonAction.OpenScreen:
                if (binding.CloseEndPanelBeforeScreenOpen)
                    CloseEndPanel();
                OpenScreen(binding.TargetScreenId);
                break;
            case StoryEndButtonAction.CloseEndPanel:
                CloseEndPanel();
                break;
            case StoryEndButtonAction.Refresh:
                Refresh();
                break;
            case StoryEndButtonAction.InvokeEventOnly:
            case StoryEndButtonAction.None:
                break;
        }

        binding.OnClick?.Invoke();
        RefreshButtonAvailability();
    }

    void ApplyMainTexts(StoryEndScreenData data)
    {
        string title = FirstNonEmpty(
            data.Title,
            _completionTitle,
            data.StoryFinished ? DefaultCompletionTitle : DefaultChapterCompletionTitle);

        SetText(FirstNonNull(_references.titleText, _titleText), title, false);
        SetText(FirstNonNull(_references.storyTitleText, _storyTitleText), data.StoryTitle, true);
        SetText(FirstNonNull(_references.completedEpisodeText, _completedEpisodeText), data.CompletedEpisodeTitle, true);

        string next = FirstNonEmpty(data.NextEpisodeTitle, _emptyNextEpisodeText);
        SetText(FirstNonNull(_references.nextEpisodeText, _nextEpisodeText), next, true);
    }

    void ApplyMainTextStyles()
    {
        if (_activeStoryStyle == null)
            return;

        _activeStoryStyle.TitleTextStyle?.ApplyTo(FirstNonNull(_references.titleText, _titleText));
        _activeStoryStyle.StoryTitleTextStyle?.ApplyTo(FirstNonNull(_references.storyTitleText, _storyTitleText));
        _activeStoryStyle.CompletedEpisodeTextStyle?.ApplyTo(FirstNonNull(_references.completedEpisodeText, _completedEpisodeText));
        _activeStoryStyle.NextEpisodeTextStyle?.ApplyTo(FirstNonNull(_references.nextEpisodeText, _nextEpisodeText));
        _activeStoryStyle.ContinueButtonTextStyle?.ApplyTo(FirstNonNull(_references.continueButtonText, ResolveContinueButtonText()));
    }

    void RenderStats(StoryEndScreenData data)
    {
        UpdateExplicitStatBindings(data);
        UpdateLegacyStatTexts(data);

        bool canSpawnRows = _references.statsContainer != null && _references.statRowTemplate != null;
        if (!canSpawnRows)
        {
            RefreshExistingSummaryTexts(data);
            return;
        }

        for (int i = 0; i < data.Stats.Count; i++)
        {
            StoryEndScreenStatValue stat = data.Stats[i];
            if (stat == null)
                continue;
            if (HasExplicitStatTargets(stat) || HasLegacyStatTargets(stat))
                continue;

            GameObject row = Instantiate(_references.statRowTemplate, _references.statsContainer);
            row.name = "EndScreenStat_" + SanitizeName(stat.Label);
            row.SetActive(true);
            if (row.GetComponent<StoryEndScreenGeneratedRowMarker>() == null)
                row.AddComponent<StoryEndScreenGeneratedRowMarker>();

            LayoutElement layout = row.GetComponent<LayoutElement>();
            if (layout == null)
                layout = row.AddComponent<LayoutElement>();
            layout.minHeight = Mathf.Max(1f, _layoutSettings.statRowMinHeight);
            layout.preferredHeight = Mathf.Max(layout.minHeight, _layoutSettings.statRowPreferredHeight);
            layout.preferredWidth = Mathf.Max(1f, _layoutSettings.statRowMaxWidth);

            ApplyStatRow(row, stat);
            ApplyStatRowLayout(row.GetComponent<RectTransform>(), stat);
        }
    }

    void ApplyStatRow(GameObject row, StoryEndScreenStatValue stat)
    {
        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        string line = BuildStatLine(stat);
        if (texts.Length == 1)
        {
            texts[0].text = line;
        }
        else if (texts.Length > 1)
        {
            TMP_Text labelText = FindTextByTokens(row.transform, "label", "name", "title");
            TMP_Text valueText = FindTextByTokens(row.transform, "value", "count", "amount", "number");
            if (labelText == null)
                labelText = texts[0];
            if (valueText == null)
                valueText = texts[texts.Length - 1];

            labelText.text = stat.Label;
            valueText.text = stat.FormattedValue;
        }

        TMP_Text lineTarget = texts.Length == 1 ? texts[0] : null;
        TMP_Text labelTarget = texts.Length > 1 ? FindTextByTokens(row.transform, "label", "name", "title") : null;
        TMP_Text valueTarget = texts.Length > 1 ? FindTextByTokens(row.transform, "value", "count", "amount", "number") : null;
        if (labelTarget == null && texts.Length > 1)
            labelTarget = texts[0];
        if (valueTarget == null && texts.Length > 1)
            valueTarget = texts[texts.Length - 1];

        stat.LineTextStyle?.ApplyTo(lineTarget);
        stat.LabelTextStyle?.ApplyTo(labelTarget);
        stat.ValueTextStyle?.ApplyTo(valueTarget);

        Image backgroundImage = FindStatRowImageByRole(row.transform, EndScreenImageRole.Background);
        Image plateImage = FindStatRowImageByRole(row.transform, EndScreenImageRole.Plate);
        Image iconImage = FindStatRowImageByRole(row.transform, EndScreenImageRole.Icon);
        if (plateImage != null && iconImage == plateImage)
            iconImage = null;

        ApplyStatImage(backgroundImage, stat.BackgroundSprite, stat.HideBackground);
        ApplyStatImage(plateImage, stat.PlateSprite, stat.HidePlate);
        ApplyStatImage(iconImage, stat.Icon, stat.HideIcon);
        ApplyIconSize(iconImage, stat);
        ApplyRectOverrideOrOffset(backgroundImage != null ? backgroundImage.rectTransform : null, stat.OverrideBackgroundRect, stat.BackgroundAnchoredPosition, stat.BackgroundSize, stat.BackgroundOffset);
        ApplyRectOverrideOrOffset(plateImage != null ? plateImage.rectTransform : null, stat.OverridePlateRect, stat.PlateAnchoredPosition, stat.PlateSize, stat.PlateOffset);
        ApplyRectOverrideOrOffset(iconImage != null ? iconImage.rectTransform : null, stat.OverrideIconRect, stat.IconAnchoredPosition, stat.IconSize, stat.IconOffset);
    }

    void ApplyStatsBackground()
    {
        if (_references == null || _references.statsBackgroundImage == null)
            return;

        _references.statsBackgroundImage.enabled = !_references.hideStatsBackground;
        if (_references.hideStatsBackground)
            return;

        if (_references.statsBackgroundOverride != null)
            _references.statsBackgroundImage.sprite = _references.statsBackgroundOverride;
    }

    void ApplyContinueButtonVisual()
    {
        if (_references == null)
            return;

        Image plateImage = ResolveContinueButtonPlateImage();
        if (plateImage == null || _references.continueButtonPlateSprite == null)
            return;

        plateImage.sprite = _references.continueButtonPlateSprite;
        plateImage.enabled = true;
    }

    Image ResolveContinueButtonPlateImage()
    {
        if (_references == null)
            return null;

        if (_references.continueButtonPlateImage != null)
            return _references.continueButtonPlateImage;

        Button button = FirstNonNull(_references.continueButton, _continueButton);
        if (button == null)
            return null;

        Image targetImage = button.targetGraphic as Image;
        if (targetImage != null)
            return targetImage;

        Image ownImage = button.GetComponent<Image>();
        if (ownImage != null)
            return ownImage;

        return button.GetComponentInChildren<Image>(true);
    }

    TMP_Text ResolveContinueButtonText()
    {
        Button button = FirstNonNull(_references.continueButton, _continueButton);
        return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
    }

    void UpdateExplicitStatBindings(StoryEndScreenData data)
    {
        if (_statBindings == null)
            return;

        for (int i = 0; i < _statBindings.Length; i++)
        {
            StoryEndScreenStatBinding binding = _statBindings[i];
            if (binding == null || !HasExplicitBindingTargets(binding))
                continue;

            StoryEndScreenStatValue stat = FindStatForBinding(data, binding);
            if (stat == null)
            {
                if (binding.hideWhenZero)
                    SetExplicitBindingVisible(binding, false);
                continue;
            }

            ApplyExplicitStatBinding(stat);
        }
    }

    void ApplyExplicitStatBinding(StoryEndScreenStatValue stat)
    {
        if (stat == null)
            return;

        if (stat.Row != null && !stat.Row.gameObject.activeSelf)
            stat.Row.gameObject.SetActive(true);
        SetExplicitStatTextEnabled(stat, true);

        TMP_Text singleLineText = ResolveSingleLineStatText(stat);
        if (singleLineText != null)
        {
            SetText(singleLineText, BuildStatLine(stat), false);
            ApplySingleLineStatTextStyle(stat, singleLineText);
            PrepareSingleLineStatText(stat, singleLineText);
        }
        if (stat.LabelText != null && stat.LabelText != singleLineText)
        {
            SetText(stat.LabelText, stat.Label, false);
            stat.LabelTextStyle?.ApplyTo(stat.LabelText);
        }
        if (stat.ValueText != null && stat.ValueText != singleLineText && stat.ValueText != stat.LabelText)
        {
            SetText(stat.ValueText, stat.FormattedValue, false);
            stat.ValueTextStyle?.ApplyTo(stat.ValueText);
        }

        ApplyStatImage(stat.BackgroundImage, stat.BackgroundSprite, stat.HideBackground);
        ApplyStatImage(stat.PlateImage, stat.PlateSprite, stat.HidePlate);
        ApplyStatImage(stat.IconImage, stat.Icon, stat.HideIcon);
        ApplyIconSize(stat.IconImage, stat);

        ApplyStatRowLayout(stat.Row, stat);
        ApplyRectOverrideOrOffset(stat.BackgroundImage != null ? stat.BackgroundImage.rectTransform : null, stat.OverrideBackgroundRect, stat.BackgroundAnchoredPosition, stat.BackgroundSize, stat.BackgroundOffset);
        ApplyRectOverrideOrOffset(stat.PlateImage != null ? stat.PlateImage.rectTransform : null, stat.OverridePlateRect, stat.PlateAnchoredPosition, stat.PlateSize, stat.PlateOffset);
        ApplyRectOverrideOrOffset(stat.IconImage != null ? stat.IconImage.rectTransform : null, stat.OverrideIconRect, stat.IconAnchoredPosition, stat.IconSize, stat.IconOffset);
        if (singleLineText != null)
            ApplyRectOffset(singleLineText.rectTransform, stat.LineTextOffset);
        if (stat.LabelText != null && stat.LabelText != singleLineText)
            ApplyRectOffset(stat.LabelText.rectTransform, stat.LabelTextOffset);
        if (stat.ValueText != null && stat.ValueText != singleLineText && stat.ValueText != stat.LabelText)
            ApplyRectOffset(stat.ValueText.rectTransform, stat.ValueTextOffset);
    }

    static TMP_Text ResolveSingleLineStatText(StoryEndScreenStatValue stat)
    {
        if (stat == null)
            return null;

        TMP_Text onlyTextInRow = ResolveOnlyTextInRow(stat.Row);
        if (onlyTextInRow != null)
            return onlyTextInRow;

        if (stat.LineText != null && (IsUsableStatText(stat.LineText) || stat.LabelText == null && stat.ValueText == null))
            return stat.LineText;

        TMP_Text rowSingleText = ResolveSingleVisibleTextInRow(stat.Row);
        if (rowSingleText != null)
            return rowSingleText;

        if (stat.LabelText != null && (stat.ValueText == null || stat.ValueText == stat.LabelText))
            return stat.LabelText;
        if (stat.ValueText != null && stat.Row != null && !LooksLikeSplitLabelText(stat.LabelText))
            return stat.ValueText;
        if (stat.ValueText != null && !IsUsableStatText(stat.LabelText))
            return stat.ValueText;
        if (stat.ValueText != null && stat.LabelText == null)
            return stat.ValueText;
        return null;
    }

    static bool LooksLikeSplitLabelText(TMP_Text text)
    {
        if (text == null)
            return false;

        string haystack = (text.name + "\n" + BuildTransformPath(text.transform)).ToLowerInvariant();
        return ContainsAnyToken(haystack, "label", "name", "title", "назв");
    }

    static TMP_Text ResolveSingleVisibleTextInRow(RectTransform row)
    {
        if (row == null)
            return null;

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text result = null;
        int usableCount = 0;
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (!IsUsableStatText(text))
                continue;

            usableCount++;
            result = text;
            if (usableCount > 1)
                return null;
        }

        return usableCount == 1 ? result : null;
    }

    static TMP_Text ResolveOnlyTextInRow(RectTransform row)
    {
        if (row == null)
            return null;

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        return texts != null && texts.Length == 1 ? texts[0] : null;
    }

    static void PrepareSingleLineStatText(StoryEndScreenStatValue stat, TMP_Text target)
    {
        if (stat == null || target == null)
            return;

        target.enableWordWrapping = false;
        target.overflowMode = TextOverflowModes.Overflow;

        bool customRect = stat.LineTextStyle != null && stat.LineTextStyle.OverrideTextRect;
        if (!customRect && stat.Row != null && ResolveOnlyTextInRow(stat.Row) == target && target.rectTransform != null)
        {
            RectTransform textRect = target.rectTransform;
            float rowWidth = stat.Row.rect.width;
            if (rowWidth > 1f && textRect.rect.width < rowWidth * 0.7f)
                textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rowWidth * 0.72f);

            LayoutRebuilder.MarkLayoutForRebuild(textRect);
        }

        target.SetAllDirty();
        target.ForceMeshUpdate();
    }

    static bool IsUsableStatText(TMP_Text text)
    {
        return text != null &&
            text.enabled &&
            text.gameObject.activeInHierarchy &&
            text.color.a > 0.001f;
    }

    static void ApplySingleLineStatTextStyle(StoryEndScreenStatValue stat, TMP_Text target)
    {
        if (stat == null || target == null)
            return;

        if (stat.LineTextStyle != null && stat.LineTextStyle.HasOverrides)
        {
            stat.LineTextStyle.ApplyTo(target);
            return;
        }

        if (target == stat.LabelText && stat.LabelTextStyle != null && stat.LabelTextStyle.HasOverrides)
        {
            stat.LabelTextStyle.ApplyTo(target);
            return;
        }

        if (target == stat.ValueText && stat.ValueTextStyle != null && stat.ValueTextStyle.HasOverrides)
            stat.ValueTextStyle.ApplyTo(target);
    }

    static void SetExplicitBindingVisible(StoryEndScreenStatBinding binding, bool visible)
    {
        if (binding == null)
            return;

        if (binding.row != null)
        {
            binding.row.gameObject.SetActive(visible);
            return;
        }

        if (binding.backgroundImage != null)
            binding.backgroundImage.enabled = visible && !binding.hideBackground;
        if (binding.plateImage != null)
            binding.plateImage.enabled = visible && !binding.hidePlate;
        if (binding.iconImage != null)
            binding.iconImage.enabled = visible && !binding.hideIcon;
        if (binding.lineText != null)
            binding.lineText.enabled = visible;
        if (binding.labelText != null)
            binding.labelText.enabled = visible;
        if (binding.valueText != null)
            binding.valueText.enabled = visible;
    }

    static void SetExplicitStatTextEnabled(StoryEndScreenStatValue stat, bool enabled)
    {
        if (stat == null)
            return;

        if (stat.LineText != null)
            stat.LineText.enabled = enabled;
        if (stat.LabelText != null)
            stat.LabelText.enabled = enabled;
        if (stat.ValueText != null)
            stat.ValueText.enabled = enabled;
    }

    static void ApplyStatImage(Image image, Sprite sprite, bool hidden)
    {
        if (image == null)
            return;

        image.enabled = !hidden;
        if (hidden)
            return;

        if (sprite != null)
            image.sprite = sprite;
    }

    static void ApplyIconSize(Image image, StoryEndScreenStatValue stat)
    {
        if (image == null || stat == null || !stat.OverrideIconSize)
            return;

        Vector2 size = stat.IconSize;
        if (size.x <= 0f && size.y <= 0f)
            return;

        RectTransform rect = image.rectTransform;
        if (rect == null)
            return;

        if (size.x > 0f)
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        if (size.y > 0f)
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        LayoutRebuilder.MarkLayoutForRebuild(rect);
    }

    void UpdateLegacyStatTexts(StoryEndScreenData data)
    {
        SetLegacyStatText(FirstNonNull(_references.legacyCityText, _storyManager != null ? _storyManager.townText : null), data, "city");
        SetLegacyStatText(FirstNonNull(_references.legacyFairytaleText, _storyManager != null ? _storyManager.storyText : null), data, "fairytale");
        SetLegacyStatText(FirstNonNull(_references.legacyReputationText, _storyManager != null ? _storyManager.reputationText : null), data, "reputation");
        SetLegacyStatText(FirstNonNull(_references.legacySparksText, _storyManager != null ? _storyManager.heartsText : null), data, "hearts");
        SetLegacyStatText(_references.legacyCandlesText, data, "candles");
        ApplyLegacyStatSprites(data);
    }

    void ReapplyCompletionStatTexts(StoryEndScreenData data, string reason)
    {
        if (data == null)
            return;

        // Explicit bindings and legacy references can coexist in old scenes. Apply both
        // after all generic text bindings so neither path can be the accidental last writer.
        UpdateExplicitStatBindings(data);
        UpdateLegacyStatTexts(data);

        if (!Debug.isDebugBuild && !Application.isEditor)
            return;

        TMP_Text cityText = FirstNonNull(_references.legacyCityText, _storyManager != null ? _storyManager.townText : null);
        TMP_Text fairytaleText = FirstNonNull(_references.legacyFairytaleText, _storyManager != null ? _storyManager.storyText : null);
        TMP_Text reputationText = FirstNonNull(_references.legacyReputationText, _storyManager != null ? _storyManager.reputationText : null);
        TMP_Text heartsText = FirstNonNull(_references.legacySparksText, _storyManager != null ? _storyManager.heartsText : null);

        StoryEndScreenStatValue city = FindStat(data, "city");
        StoryEndScreenStatValue fairytale = FindStat(data, "fairytale");
        StoryEndScreenStatValue reputation = FindStat(data, "reputation");
        StoryEndScreenStatValue hearts = FindStat(data, "hearts");

        Debug.Log(
            $"[END_STATS][FINAL_UI] reason='{reason ?? ""}' " +
            $"cityData={ValueOrZero(city)} cityText='{TextOrNull(cityText)}' " +
            $"fairytaleData={ValueOrZero(fairytale)} fairytaleText='{TextOrNull(fairytaleText)}' " +
            $"reputationData={ValueOrZero(reputation)} reputationText='{TextOrNull(reputationText)}' " +
            $"heartsData={ValueOrZero(hearts)} heartsText='{TextOrNull(heartsText)}'.",
            this);
    }

    static int ValueOrZero(StoryEndScreenStatValue stat)
    {
        return stat != null ? stat.Value : 0;
    }

    static string TextOrNull(TMP_Text text)
    {
        return text != null ? text.text : "<null>";
    }

    void ApplyLegacyStatSprites(StoryEndScreenData data)
    {
        ApplyLegacyStatVisual(_references.legacyCityRow, _references.legacyCityImage, _references.legacyCityIconImage, data, "city");
        ApplyLegacyStatVisual(_references.legacyFairytaleRow, _references.legacyFairytaleImage, _references.legacyFairytaleIconImage, data, "fairytale");
        ApplyLegacyStatVisual(_references.legacyReputationRow, _references.legacyReputationImage, _references.legacyReputationIconImage, data, "reputation");
        ApplyLegacyStatVisual(_references.legacySparksRow, _references.legacySparksImage, _references.legacySparksIconImage, data, "hearts");
        ApplyLegacyStatVisual(_references.legacyCandlesRow, _references.legacyCandlesImage, _references.legacyCandlesIconImage, data, "candles");
    }

    void ApplyLegacyStatVisual(RectTransform row, Image plateTarget, Image iconTarget, StoryEndScreenData data, string label)
    {
        StoryEndScreenStatValue stat = FindStat(data, label);
        if (stat == null)
            return;

        bool rowReserved = IsExplicitStatRowTarget(row);
        bool plateReserved = IsExplicitStatImageTarget(plateTarget);
        bool iconReserved = IsExplicitStatImageTarget(iconTarget);

        if (plateTarget != null && !plateReserved && stat.PlateSprite != null)
        {
            plateTarget.sprite = stat.PlateSprite;
            plateTarget.enabled = true;
        }

        if (iconTarget != null && iconTarget != plateTarget && !iconReserved && stat.Icon != null)
        {
            iconTarget.sprite = stat.Icon;
            iconTarget.enabled = true;
        }

        if (!iconReserved)
            ApplyIconSize(iconTarget, stat);
        if (!rowReserved)
            ApplyStatRowLayout(row, stat);
    }

    void ApplyStatRowLayout(RectTransform row, StoryEndScreenStatValue stat)
    {
        if (row == null || stat == null)
            return;

        bool hasManualLayout = stat.OverrideRowPosition || stat.OverrideRowSize;
        if (hasManualLayout && stat.IgnoreParentLayoutWhenPositioned)
        {
            LayoutElement layout = row.GetComponent<LayoutElement>();
            if (layout == null)
                layout = row.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
        }

        if (stat.OverrideRowSize)
        {
            if (stat.RowSize.x > 0f)
                row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, stat.RowSize.x);
            if (stat.RowSize.y > 0f)
                row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, stat.RowSize.y);
        }

        if (stat.OverrideRowPosition)
            row.anchoredPosition = stat.RowAnchoredPosition + stat.RowOffset;
        else
            ApplyRectOffset(row, stat.RowOffset);
    }

    void ApplyRectOverrideOrOffset(RectTransform rect, bool overrideRect, Vector2 anchoredPosition, Vector2 size, Vector2 offset)
    {
        if (rect == null)
            return;

        if (!overrideRect)
        {
            ApplyRectOffset(rect, offset);
            return;
        }

        ApplyRectOverride(rect, anchoredPosition + offset, size);
    }

    static void ApplyRectOverride(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchoredPosition = anchoredPosition;
        if (size.x > 0f)
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        if (size.y > 0f)
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        LayoutRebuilder.MarkLayoutForRebuild(rect);
    }

    void ApplyRectOffset(RectTransform rect, Vector2 offset)
    {
        if (rect == null)
            return;

        RectSnapshot snapshot = GetRectSnapshot(rect);
        rect.anchoredPosition = snapshot.AnchoredPosition + offset;
    }

    RectSnapshot GetRectSnapshot(RectTransform rect)
    {
        if (rect == null)
            return new RectSnapshot();

        if (!_rectSnapshots.TryGetValue(rect, out RectSnapshot snapshot))
        {
            snapshot = new RectSnapshot
            {
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta
            };
            _rectSnapshots[rect] = snapshot;
        }

        return snapshot;
    }

    StoryEndScreenStatValue FindStatForBinding(StoryEndScreenData data, StoryEndScreenStatBinding binding)
    {
        if (data == null || binding == null)
            return null;

        foreach (string statId in binding.AllStatIds())
        {
            StoryEndScreenStatValue stat = FindStat(data, statId);
            if (stat != null)
                return stat;
        }

        return FindStat(data, binding.label);
    }

    static bool HasExplicitBindingTargets(StoryEndScreenStatBinding binding)
    {
        return binding != null && (
            binding.row != null ||
            binding.backgroundImage != null ||
            binding.plateImage != null ||
            binding.iconImage != null ||
            binding.lineText != null ||
            binding.labelText != null ||
            binding.valueText != null);
    }

    static bool HasExplicitStatTargets(StoryEndScreenStatValue stat)
    {
        return stat != null && (
            stat.Row != null ||
            stat.BackgroundImage != null ||
            stat.PlateImage != null ||
            stat.IconImage != null ||
            stat.LineText != null ||
            stat.LabelText != null ||
            stat.ValueText != null);
    }

    bool HasLegacyStatTargets(StoryEndScreenStatValue stat)
    {
        if (stat == null || _references == null)
            return false;

        if (StatMatches(stat, "city", "town") && HasLegacyTargets(_references.legacyCityRow, _references.legacyCityImage, _references.legacyCityIconImage, _references.legacyCityText))
            return true;
        if (StatMatches(stat, "fairytale", "story", "tale") && HasLegacyTargets(_references.legacyFairytaleRow, _references.legacyFairytaleImage, _references.legacyFairytaleIconImage, _references.legacyFairytaleText))
            return true;
        if (StatMatches(stat, "reputation", "respect", "rep") && HasLegacyTargets(_references.legacyReputationRow, _references.legacyReputationImage, _references.legacyReputationIconImage, _references.legacyReputationText))
            return true;
        if (StatMatches(stat, "hearts", "sparks") && HasLegacyTargets(_references.legacySparksRow, _references.legacySparksImage, _references.legacySparksIconImage, _references.legacySparksText))
            return true;
        if (StatMatches(stat, "candles", "candle") && HasLegacyTargets(_references.legacyCandlesRow, _references.legacyCandlesImage, _references.legacyCandlesIconImage, _references.legacyCandlesText))
            return true;

        return false;
    }

    static bool HasLegacyTargets(RectTransform row, Image plate, Image icon, TMP_Text text)
    {
        return row != null || plate != null || icon != null || text != null;
    }

    static bool StatMatches(StoryEndScreenStatValue stat, params string[] ids)
    {
        if (stat == null || ids == null)
            return false;

        string statId = Normalize(stat.StatId).ToLowerInvariant();
        string label = Normalize(stat.Label).ToLowerInvariant();
        for (int i = 0; i < ids.Length; i++)
        {
            string id = Normalize(ids[i]).ToLowerInvariant();
            if (!string.IsNullOrEmpty(id) && (statId == id || label == id))
                return true;
        }

        return false;
    }

    void SetLegacyStatText(TMP_Text target, StoryEndScreenData data, string label)
    {
        if (target == null)
            return;
        if (IsExplicitStatTextTarget(target))
            return;

        StoryEndScreenStatValue stat = FindStat(data, label);
        if (stat == null)
            return;

        SetText(target, BuildStatLine(stat), false);
        stat.LineTextStyle?.ApplyTo(target);
    }

    void RefreshExistingSummaryTexts(StoryEndScreenData data)
    {
        GameObject root = _references.ResolveRoot(this);
        TMP_Text[] texts = root != null ? root.GetComponentsInChildren<TMP_Text>(true) : GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || IsExplicitTextTarget(text))
                continue;

            string label = ExtractSummaryLabel(text.text);
            if (string.IsNullOrWhiteSpace(label))
                label = ExtractSummaryLabel(text.name);

            StoryEndScreenStatValue stat = FindStat(data, label);
            if (stat != null)
                text.text = BuildStatLine(stat);
        }
    }

    void RefreshExtraTexts(StoryEndScreenData data)
    {
        if (_extraTexts == null)
            return;

        for (int i = 0; i < _extraTexts.Length; i++)
        {
            TextBinding binding = _extraTexts[i];
            if (binding == null || binding.Text == null)
                continue;
            if (IsStatRowTextTarget(binding.Text))
                continue;

            string rawValue = binding.Source == StoryEndTextSource.StaticText
                ? binding.StaticText
                : ResolveText(binding.Source, binding.StatId, binding.StaticText, data);

            SetText(binding.Text, FormatBindingText(binding, rawValue), binding.HideWhenEmpty);
        }
    }

    void RemoveStatRowExtraTextBindings()
    {
        if (_extraTexts == null || _extraTexts.Length == 0)
            return;

        List<TextBinding> kept = null;
        for (int i = 0; i < _extraTexts.Length; i++)
        {
            TextBinding binding = _extraTexts[i];
            if (binding == null || binding.Text == null || IsStatRowTextTarget(binding.Text))
                continue;

            kept ??= new List<TextBinding>(_extraTexts.Length);
            kept.Add(binding);
        }

        if (kept == null)
        {
            _extraTexts = Array.Empty<TextBinding>();
            return;
        }

        if (kept.Count != _extraTexts.Length)
            _extraTexts = kept.ToArray();
    }

    void RestoreStatRowPreviewTextsInEditMode()
    {
        if (Application.isPlaying || _statBindings == null)
            return;

        for (int i = 0; i < _statBindings.Length; i++)
        {
            StoryEndScreenStatBinding binding = _statBindings[i];
            if (binding == null || !binding.enabled)
                continue;

            TMP_Text target = ResolveSingleLineBindingText(binding);
            if (target == null)
                continue;

            target.text = BuildPreviewStatLine(binding);
            target.SetAllDirty();
        }
    }

    static TMP_Text ResolveSingleLineBindingText(StoryEndScreenStatBinding binding)
    {
        if (binding == null)
            return null;

        if (binding.lineText != null &&
            (binding.labelText == null || binding.labelText == binding.lineText) &&
            (binding.valueText == null || binding.valueText == binding.lineText))
        {
            return binding.lineText;
        }

        if (binding.row == null)
            return null;

        TMP_Text[] texts = binding.row.GetComponentsInChildren<TMP_Text>(true);
        return texts != null && texts.Length == 1 ? texts[0] : null;
    }

    static string BuildPreviewStatLine(StoryEndScreenStatBinding binding)
    {
        if (binding == null)
            return "";

        string label = FirstNonEmpty(binding.label, binding.statId, "Стат");
        return label + ": " + FormatPreviewStatValue(binding.previewValue, binding.format);
    }

    static string FormatPreviewStatValue(int value, string format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return value.ToString(CultureInfo.InvariantCulture);

        try
        {
            return string.Format(CultureInfo.InvariantCulture, format, value);
        }
        catch (FormatException)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }

    void RefreshButtonAvailability()
    {
        Button unifiedButton = FirstNonNull(_continueButton, _references.continueButton);
        if (ShouldUseUnifiedContinueButton(unifiedButton))
        {
            SetButtonAvailability(
                unifiedButton,
                IsActionAvailable(StoryEndButtonAction.ContinueOrReturnToMenu, ""),
                hideWhenUnavailable: false);

            SetLegacyButtonInactiveIfSeparate(FirstNonNull(_menuButton, _references.menuButton), unifiedButton);
            SetLegacyButtonInactiveIfSeparate(FirstNonNull(_nextEpisodeButton, _references.nextEpisodeButton), unifiedButton);
            SetLegacyButtonInactiveIfSeparate(FirstNonNull(_restartEpisodeButton, _references.restartEpisodeButton), unifiedButton);
        }
        else
        {
            SetButtonAvailability(
                FirstNonNull(_nextEpisodeButton, _references.nextEpisodeButton),
                IsActionAvailable(StoryEndButtonAction.ContinueStory, ""),
                _hideUnavailableMainButtons);

            SetButtonAvailability(
                FirstNonNull(_restartEpisodeButton, _references.restartEpisodeButton),
                IsActionAvailable(StoryEndButtonAction.RestartCompletedEpisode, ""),
                _hideUnavailableMainButtons);
        }

        if (_extraButtons == null)
            return;

        for (int i = 0; i < _extraButtons.Length; i++)
        {
            ButtonBinding binding = _extraButtons[i];
            if (binding == null)
                continue;

            SetButtonAvailability(
                binding.Button,
                IsActionAvailable(binding.Action, binding.TargetScreenId),
                binding.HideWhenUnavailable);
        }
    }

    bool ShouldUseUnifiedContinueButton(Button unifiedButton)
    {
        return unifiedButton != null && (_useUnifiedContinueButton || !HasSeparateMainButtons(unifiedButton));
    }

    bool HasSeparateMainButtons(Button unifiedButton)
    {
        return IsSeparateButton(FirstNonNull(_menuButton, _references.menuButton), unifiedButton) ||
               IsSeparateButton(FirstNonNull(_nextEpisodeButton, _references.nextEpisodeButton), unifiedButton) ||
               IsSeparateButton(FirstNonNull(_restartEpisodeButton, _references.restartEpisodeButton), unifiedButton);
    }

    static bool IsSeparateButton(Button button, Button unifiedButton)
    {
        return button != null && button != unifiedButton;
    }

    bool IsActionAvailable(StoryEndButtonAction action, string targetScreenId)
    {
        switch (action)
        {
            case StoryEndButtonAction.ContinueOrReturnToMenu:
                return _storyManager != null || _menuController != null;
            case StoryEndButtonAction.ContinueStory:
                return _storyManager != null && _storyManager.CanContinueFromEndPanel;
            case StoryEndButtonAction.RestartCompletedEpisode:
                return _storyManager != null && _storyManager.CanRestartCompletedChapter;
            case StoryEndButtonAction.OpenScreen:
                return _screenNavigator != null && !string.IsNullOrWhiteSpace(targetScreenId);
            default:
                return true;
        }
    }

    void SetLegacyButtonInactiveIfSeparate(Button legacyButton, Button unifiedButton)
    {
        if (legacyButton == null || unifiedButton == null || legacyButton == unifiedButton)
            return;

        legacyButton.gameObject.SetActive(false);
    }

    void SetButtonAvailability(Button button, bool available, bool hideWhenUnavailable)
    {
        if (button == null)
            return;

        if (hideWhenUnavailable)
        {
            button.gameObject.SetActive(available);
            return;
        }

        if (!button.gameObject.activeSelf)
            button.gameObject.SetActive(true);

        button.interactable = available;
    }

    string ResolveText(StoryEndTextSource source, string statId, string fallback, StoryEndScreenData data)
    {
        EnsureReferences();

        switch (source)
        {
            case StoryEndTextSource.CompletionTitle:
                return FirstNonEmpty(data != null ? data.Title : "", _completionTitle);
            case StoryEndTextSource.StoryTitle:
                return FirstNonEmpty(data != null ? data.StoryTitle : "", _storyManager != null ? _storyManager.CurrentStoryTitle : "", fallback);
            case StoryEndTextSource.StoryId:
                return FirstNonEmpty(data != null ? data.StoryId : "", _storyManager != null ? _storyManager.CurrentStoryId : "", fallback);
            case StoryEndTextSource.CompletedEpisodeTitle:
                return FirstNonEmpty(data != null ? data.CompletedEpisodeTitle : "", _storyManager != null ? _storyManager.LastCompletedChapterTitle : "", fallback);
            case StoryEndTextSource.CompletedEpisodeId:
                return FirstNonEmpty(data != null ? data.CompletedEpisodeId : "", _storyManager != null ? _storyManager.LastCompletedEpisodeId : "", fallback);
            case StoryEndTextSource.CompletedEpisodeNumber:
                return FormatNumber(data != null ? data.CompletedEpisodeNumber : _storyManager != null ? _storyManager.LastCompletedChapterNumber : 0);
            case StoryEndTextSource.CompletedEpisodeProgress:
                return FormatProgress(data != null ? data.CompletedEpisodeNumber : _storyManager != null ? _storyManager.LastCompletedChapterNumber : 0);
            case StoryEndTextSource.NextEpisodeTitle:
                return FirstNonEmpty(data != null ? data.NextEpisodeTitle : "", _storyManager != null ? _storyManager.EndPanelNextChapterTitle : "", fallback);
            case StoryEndTextSource.NextEpisodeId:
                return FirstNonEmpty(data != null ? data.NextEpisodeId : "", _storyManager != null ? _storyManager.EndPanelNextChapterId : "", fallback);
            case StoryEndTextSource.NextEpisodeNumber:
                return FormatNumber(data != null ? data.NextEpisodeNumber : _storyManager != null ? _storyManager.EndPanelNextChapterNumber : 0);
            case StoryEndTextSource.NextEpisodeProgress:
                return FormatProgress(data != null ? data.NextEpisodeNumber : _storyManager != null ? _storyManager.EndPanelNextChapterNumber : 0);
            case StoryEndTextSource.CurrentEpisodeTitle:
                return FirstNonEmpty(_storyManager != null ? _storyManager.CurrentChapterTitle : "", fallback);
            case StoryEndTextSource.CurrentEpisodeId:
                return FirstNonEmpty(_storyManager != null ? _storyManager.CurrentEpisodeId : "", fallback);
            case StoryEndTextSource.CurrentEpisodeNumber:
                return FormatNumber(_storyManager != null ? _storyManager.CurrentChapterNumber : 0);
            case StoryEndTextSource.CurrentEpisodeProgress:
                return FormatProgress(_storyManager != null ? _storyManager.CurrentChapterNumber : 0);
            case StoryEndTextSource.CandleBalance:
                return PlayerData.Candles.ToString(CultureInfo.InvariantCulture);
            case StoryEndTextSource.HeartBalance:
                return PlayerData.Hearts.ToString(CultureInfo.InvariantCulture);
            case StoryEndTextSource.EpisodeCandleDelta:
                return StoryManager.FormatSignedEpisodeValue(_storyManager != null ? _storyManager.LastCompletedEpisodeCandleDelta : 0);
            case StoryEndTextSource.EpisodeHeartDelta:
                return StoryManager.FormatSignedEpisodeValue(_storyManager != null ? _storyManager.LastCompletedEpisodeHeartDelta : 0);
            case StoryEndTextSource.TownStat:
                return ResolveDataStatValue(data, "Город", "city");
            case StoryEndTextSource.ReputationStat:
                return ResolveDataStatValue(data, "Репутация", "reputation");
            case StoryEndTextSource.StoryStat:
                return ResolveDataStatValue(data, "Сказка", "fairytale");
            case StoryEndTextSource.CustomStat:
                return ResolveCustomStat(statId);
            case StoryEndTextSource.PlayerName:
                return DialogueVariableResolver.ResolvePlayerName(DialogueVariableContext.StoryUi(nameof(StoryEndScreenController), gameObject, _storyManager != null ? _storyManager.CurrentStoryId : ""));
            case StoryEndTextSource.StaticText:
            default:
                return fallback ?? "";
        }
    }

    string ResolveDataStatValue(StoryEndScreenData data, string label, string statId)
    {
        StoryEndScreenStatValue stat = FindStat(data, label);
        if (stat != null)
            return stat.FormattedValue;

        return GameState.Instance != null ? GameState.Instance.GetInt(statId).ToString(CultureInfo.InvariantCulture) : "0";
    }

    string ResolveCustomStat(string statId)
    {
        if (GameState.Instance == null || string.IsNullOrWhiteSpace(statId))
            return "0";

        return GameState.Instance.GetInt(statId).ToString(CultureInfo.InvariantCulture);
    }

    string FormatBindingText(TextBinding binding, string value)
    {
        if (binding == null)
            return value ?? "";

        return FormatText(value, binding.Format);
    }

    string FormatText(string value, string format)
    {
        value ??= "";
        if (string.IsNullOrWhiteSpace(format))
            return value;

        try
        {
            return string.Format(CultureInfo.InvariantCulture, format, value);
        }
        catch (FormatException exception)
        {
            AppLogger.Warn(
                AppLogCategory.EndScreen,
                nameof(StoryEndScreenController),
                nameof(FormatText),
                "Invalid end-screen text format. Raw value was used.",
                LogMetadata.Of("format", format, "error", exception.Message),
                recoverable: true);
            return value;
        }
    }

    void HideConflictingStoryUi(bool preview)
    {
        if (preview && (_previewSettings == null || !_previewSettings.hideOtherStoryUiDuringPreview))
            return;

        if (_storyManager == null)
            return;

        _storyManager.dialogueUI?.ResetStoryUi();
        HideSecondaryDialogueManagers(_storyManager.dialogueUI);
        _storyManager.ChapterTitleOverlay?.HideInstant();
        _storyManager.phoneDialogueUI?.Hide();
    }

    static void HideSecondaryDialogueManagers(DialogueUIManager primary)
    {
        DialogueUIManager[] managers = FindObjectsOfType<DialogueUIManager>(true);
        if (managers == null)
            return;

        for (int i = 0; i < managers.Length; i++)
        {
            DialogueUIManager manager = managers[i];
            if (manager == null || manager == primary)
                continue;

            manager.HideDialoguePanelForCutsceneIntro();
        }
    }

    void ClearGeneratedRows()
    {
        if (_layoutSettings != null && !_layoutSettings.clearGeneratedRowsBeforeRender)
            return;

        GameObject root = _references != null ? _references.ResolveRoot(this) : gameObject;
        StoryEndScreenGeneratedRowMarker[] rows = root != null
            ? root.GetComponentsInChildren<StoryEndScreenGeneratedRowMarker>(true)
            : GetComponentsInChildren<StoryEndScreenGeneratedRowMarker>(true);

        for (int i = rows.Length - 1; i >= 0; i--)
        {
            if (rows[i] == null)
                continue;

            GameObject row = rows[i].gameObject;
            if (Application.isPlaying)
                Destroy(row);
            else
                DestroyImmediate(row);
        }
    }

    void SetTemplatesInactive()
    {
        if (_references != null && _references.statRowTemplate != null)
            _references.statRowTemplate.SetActive(false);
    }

    void SetText(TMP_Text target, string value, bool hideWhenEmpty)
    {
        if (target == null)
            return;

        string resolved = DialogueVariableResolver.ResolveText(
            value ?? "",
            DialogueVariableContext.StoryUi(nameof(StoryEndScreenController), gameObject, _storyManager != null ? _storyManager.CurrentStoryId : ""));
        target.text = resolved;

        if (hideWhenEmpty)
            target.gameObject.SetActive(!string.IsNullOrWhiteSpace(resolved));
    }

    void LogValidation(StoryEndScreenValidationResult validation, string operation)
    {
        if (validation == null)
            return;

        for (int i = 0; i < validation.Errors.Count; i++)
        {
            ThrottledAppLogger.Error(
                "EndScreenValidationError:" + validation.Errors[i],
                AppLogCategory.EndScreen,
                nameof(StoryEndScreenController),
                operation ?? nameof(LogValidation),
                validation.Errors[i],
                null,
                LogMetadata.Of("object", name));
        }

        for (int i = 0; i < validation.Warnings.Count; i++)
        {
            ThrottledAppLogger.Warn(
                "EndScreenValidationWarning:" + validation.Warnings[i],
                AppLogCategory.EndScreen,
                nameof(StoryEndScreenController),
                operation ?? nameof(LogValidation),
                validation.Warnings[i],
                LogMetadata.Of("object", name),
                8d);
        }
    }

    bool IsExplicitTextTarget(TMP_Text text)
    {
        if (text == null)
            return true;

        if (text == _titleText ||
            text == _storyTitleText ||
            text == _completedEpisodeText ||
            text == _nextEpisodeText ||
            text == _references.titleText ||
            text == _references.storyTitleText ||
            text == _references.completedEpisodeText ||
            text == _references.nextEpisodeText)
        {
            return true;
        }

        if (_extraTexts == null)
            return IsExplicitStatTextTarget(text);

        for (int i = 0; i < _extraTexts.Length; i++)
        {
            TextBinding binding = _extraTexts[i];
            if (binding != null && binding.Text == text)
                return true;
        }

        return IsExplicitStatTextTarget(text);
    }

    bool IsExplicitStatTextTarget(TMP_Text text)
    {
        if (text == null || _statBindings == null)
            return false;

        for (int i = 0; i < _statBindings.Length; i++)
        {
            StoryEndScreenStatBinding binding = _statBindings[i];
            if (binding == null)
                continue;

            if (binding.lineText == text || binding.labelText == text || binding.valueText == text)
                return true;
            if (binding.row != null && IsDescendantOf(text.transform, binding.row.transform))
                return true;
        }

        return false;
    }

    bool IsStatRowTextTarget(TMP_Text text)
    {
        if (text == null)
            return false;

        if (IsExplicitStatTextTarget(text))
            return true;
        if (_references == null)
            return false;

        // Old EndScreen scenes often assign only the TMP_Text reference and leave the
        // legacy row RectTransform empty. The previous check looked only at row ancestry,
        // so an _extraTexts binding targeting the exact same TMP slipped through and
        // overwrote "Город: 4" back to "Город: 0" after RenderStats().
        if (text == _references.legacyCityText ||
            text == _references.legacyFairytaleText ||
            text == _references.legacyReputationText ||
            text == _references.legacySparksText ||
            text == _references.legacyCandlesText ||
            (_storyManager != null && (
                text == _storyManager.townText ||
                text == _storyManager.storyText ||
                text == _storyManager.reputationText ||
                text == _storyManager.heartsText)))
        {
            return true;
        }

        return IsDescendantOf(text.transform, _references.legacyCityRow) ||
            IsDescendantOf(text.transform, _references.legacyFairytaleRow) ||
            IsDescendantOf(text.transform, _references.legacyReputationRow) ||
            IsDescendantOf(text.transform, _references.legacySparksRow) ||
            IsDescendantOf(text.transform, _references.legacyCandlesRow);
    }

    bool IsExplicitStatImageTarget(Image image)
    {
        if (image == null || _statBindings == null)
            return false;

        for (int i = 0; i < _statBindings.Length; i++)
        {
            StoryEndScreenStatBinding binding = _statBindings[i];
            if (binding == null)
                continue;

            if (binding.backgroundImage == image || binding.plateImage == image || binding.iconImage == image)
                return true;
            if (binding.row != null && IsDescendantOf(image.transform, binding.row.transform))
                return true;
        }

        return false;
    }

    bool IsExplicitStatRowTarget(RectTransform row)
    {
        if (row == null || _statBindings == null)
            return false;

        for (int i = 0; i < _statBindings.Length; i++)
        {
            StoryEndScreenStatBinding binding = _statBindings[i];
            if (binding == null || binding.row == null)
                continue;

            if (binding.row == row ||
                IsDescendantOf(binding.row.transform, row.transform) ||
                IsDescendantOf(row.transform, binding.row.transform))
            {
                return true;
            }
        }

        return false;
    }

    string FormatNumber(int number)
    {
        return number > 0 ? number.ToString(CultureInfo.InvariantCulture) : "";
    }

    string FormatProgress(int number)
    {
        int total = _storyManager != null ? _storyManager.StoryChapterCount : 0;
        if (number <= 0)
            return "";

        return total > 0
            ? number.ToString(CultureInfo.InvariantCulture) + "/" + total.ToString(CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture);
    }

    void CaptureSerializedConfigurationSnapshot()
    {
        if (_serializedReferencesSnapshot != null)
            return;

        RefreshSerializedConfigurationSnapshotFromCurrent();
    }

    void RefreshSerializedConfigurationSnapshotFromCurrent()
    {
        _serializedReferencesSnapshot = new StoryEndScreenReferences();
        _serializedLayoutSettingsSnapshot = new StoryEndScreenLayoutSettings();
        _serializedPreviewSettingsSnapshot = new StoryEndScreenPreviewSettings();

        CopyReferences(_references, _serializedReferencesSnapshot, overwrite: true);
        CopyLayoutSettings(_layoutSettings, _serializedLayoutSettingsSnapshot);
        CopyPreviewSettings(_previewSettings, _serializedPreviewSettingsSnapshot);
        _serializedStatBindingsSnapshot = CloneStatBindings(_statBindings);
    }

    void RestoreSerializedConfigurationSnapshot()
    {
        if (_serializedReferencesSnapshot != null)
        {
            StoryEndScreenReferences restoredReferences = new StoryEndScreenReferences();
            CopyReferences(_serializedReferencesSnapshot, restoredReferences, overwrite: true);
            _references = restoredReferences;
        }

        if (_serializedLayoutSettingsSnapshot != null)
        {
            StoryEndScreenLayoutSettings restoredLayout = new StoryEndScreenLayoutSettings();
            CopyLayoutSettings(_serializedLayoutSettingsSnapshot, restoredLayout);
            _layoutSettings = restoredLayout;
        }

        if (_serializedPreviewSettingsSnapshot != null)
        {
            StoryEndScreenPreviewSettings restoredPreview = new StoryEndScreenPreviewSettings();
            CopyPreviewSettings(_serializedPreviewSettingsSnapshot, restoredPreview);
            _previewSettings = restoredPreview;
        }

        if (_serializedStatBindingsSnapshot != null)
            _statBindings = CloneStatBindings(_serializedStatBindingsSnapshot);
    }

    void CaptureSceneVisualSnapshots()
    {
        GameObject root = _references != null ? _references.ResolveRoot(this) : gameObject;
        Transform searchRoot = root != null ? root.transform : transform;
        if (searchRoot == null)
            return;

        Image[] images = searchRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || _imageSnapshots.ContainsKey(image))
                continue;

            _imageSnapshots[image] = new ImageSnapshot
            {
                Sprite = image.sprite,
                Enabled = image.enabled,
                Color = image.color,
                Type = image.type,
                PreserveAspect = image.preserveAspect,
                PixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier
            };
        }

        TMP_Text[] texts = searchRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || _textSnapshots.ContainsKey(text))
                continue;

            _textSnapshots[text] = new TextSnapshot
            {
                Font = text.font,
                FontSize = text.fontSize,
                EnableAutoSizing = text.enableAutoSizing,
                FontSizeMin = text.fontSizeMin,
                FontSizeMax = text.fontSizeMax,
                Alignment = text.alignment,
                EnableWordWrapping = text.enableWordWrapping,
                OverflowMode = text.overflowMode,
                LineSpacing = text.lineSpacing,
                Margin = text.margin,
                Color = text.color
            };
        }

        RectTransform[] rects = searchRoot.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null || _sceneRectSnapshots.ContainsKey(rect))
                continue;

            _sceneRectSnapshots[rect] = new RectSnapshot
            {
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta
            };
        }
    }

    void RestoreSceneVisualSnapshots()
    {
        foreach (KeyValuePair<Image, ImageSnapshot> pair in _imageSnapshots)
        {
            Image image = pair.Key;
            if (image == null)
                continue;

            ImageSnapshot snapshot = pair.Value;
            image.sprite = snapshot.Sprite;
            image.enabled = snapshot.Enabled;
            image.color = snapshot.Color;
            image.type = snapshot.Type;
            image.preserveAspect = snapshot.PreserveAspect;
            image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, snapshot.PixelsPerUnitMultiplier);
            image.SetAllDirty();
        }

        foreach (KeyValuePair<TMP_Text, TextSnapshot> pair in _textSnapshots)
        {
            TMP_Text text = pair.Key;
            if (text == null)
                continue;

            TextSnapshot snapshot = pair.Value;
            text.font = snapshot.Font;
            text.fontSize = snapshot.FontSize;
            text.enableAutoSizing = snapshot.EnableAutoSizing;
            text.fontSizeMin = snapshot.FontSizeMin;
            text.fontSizeMax = snapshot.FontSizeMax;
            text.alignment = snapshot.Alignment;
            text.enableWordWrapping = snapshot.EnableWordWrapping;
            text.overflowMode = snapshot.OverflowMode;
            text.lineSpacing = snapshot.LineSpacing;
            text.margin = snapshot.Margin;
            text.color = snapshot.Color;
            text.SetAllDirty();
        }

        foreach (KeyValuePair<RectTransform, RectSnapshot> pair in _sceneRectSnapshots)
        {
            RectTransform rect = pair.Key;
            if (rect == null)
                continue;

            rect.anchoredPosition = pair.Value.AnchoredPosition;
            rect.sizeDelta = pair.Value.SizeDelta;
        }
    }

    void ApplyActiveStoryStyleToConfiguration()
    {
        if (_activeStoryStyle == null)
            return;

        Sprite background = ResolveStyleSprite(_activeStoryStyle.BackgroundSprite, _activeStoryStyle.BackgroundSpriteSource);
        if (background != null)
            _references.backgroundOverride = background;

        Sprite statsBackground = ResolveStyleSprite(_activeStoryStyle.StatsBackgroundSprite, _activeStoryStyle.StatsBackgroundSpriteSource);
        if (statsBackground != null)
            _references.statsBackgroundOverride = statsBackground;

        Sprite continuePlate = ResolveStyleSprite(_activeStoryStyle.ContinueButtonPlateSprite, _activeStoryStyle.ContinueButtonPlateSpriteSource);
        if (continuePlate != null)
            _references.continueButtonPlateSprite = continuePlate;

        if (!_activeStoryStyle.HasStatBindings)
            return;

        var runtimeBindings = new List<StoryEndScreenStatBinding>();
        IReadOnlyList<StoryEndScreenStatStyleBinding> styleBindings = _activeStoryStyle.StatBindings;
        for (int i = 0; i < styleBindings.Count; i++)
        {
            StoryEndScreenStatStyleBinding styleBinding = styleBindings[i];
            if (styleBinding == null || !styleBinding.Enabled)
                continue;

            StoryEndScreenStatBinding runtimeBinding = CloneStatBinding(FindBaseStatBinding(styleBinding)) ?? new StoryEndScreenStatBinding();
            ApplyStyleBindingToRuntimeBinding(styleBinding, runtimeBinding);
            SanitizePlateIconTargets(runtimeBinding);
            runtimeBindings.Add(runtimeBinding);
        }

        if (runtimeBindings.Count > 0)
            _statBindings = runtimeBindings.ToArray();
    }

    StoryEndScreenStatBinding FindBaseStatBinding(StoryEndScreenStatStyleBinding styleBinding)
    {
        if (styleBinding == null || _serializedStatBindingsSnapshot == null)
            return null;

        for (int i = 0; i < _serializedStatBindingsSnapshot.Length; i++)
        {
            StoryEndScreenStatBinding binding = _serializedStatBindingsSnapshot[i];
            if (styleBinding.Matches(binding))
                return binding;
        }

        return null;
    }

    static void ApplyStyleBindingToRuntimeBinding(
        StoryEndScreenStatStyleBinding styleBinding,
        StoryEndScreenStatBinding runtimeBinding)
    {
        if (styleBinding == null || runtimeBinding == null)
            return;

        runtimeBinding.enabled = styleBinding.Enabled;
        runtimeBinding.label = styleBinding.Label;
        runtimeBinding.statId = styleBinding.StatId;
        runtimeBinding.statAliases = styleBinding.StatAliases != null ? (string[])styleBinding.StatAliases.Clone() : Array.Empty<string>();
        runtimeBinding.valueMode = styleBinding.ValueMode;
        runtimeBinding.previewValue = styleBinding.PreviewValue;
        runtimeBinding.hideWhenZero = styleBinding.HideWhenZero;
        runtimeBinding.format = styleBinding.Format;

        Sprite background = ResolveStyleSprite(styleBinding.BackgroundSprite, styleBinding.BackgroundSpriteSource);
        if (background != null)
            runtimeBinding.backgroundSprite = background;
        if (styleBinding.BackgroundSpriteSource != null)
            runtimeBinding.backgroundSpriteSource = styleBinding.BackgroundSpriteSource;

        Sprite plate = ResolveStyleSprite(styleBinding.PlateSprite, styleBinding.PlateSpriteSource);
        if (plate != null)
            runtimeBinding.plateSprite = plate;
        if (styleBinding.PlateSpriteSource != null)
            runtimeBinding.plateSpriteSource = styleBinding.PlateSpriteSource;

        Sprite icon = ResolveStyleSprite(styleBinding.IconSprite, styleBinding.IconSpriteSource);
        if (icon != null)
            runtimeBinding.icon = icon;
        if (styleBinding.IconSpriteSource != null)
            runtimeBinding.iconSpriteSource = styleBinding.IconSpriteSource;

        runtimeBinding.hideBackground = styleBinding.HideBackground;
        runtimeBinding.hidePlate = styleBinding.HidePlate;
        runtimeBinding.hideIcon = styleBinding.HideIcon;
        runtimeBinding.overrideIconSize = styleBinding.OverrideIconSize;
        runtimeBinding.iconSize = styleBinding.IconSize;
        runtimeBinding.overrideRowPosition = styleBinding.OverrideRowPosition;
        runtimeBinding.rowAnchoredPosition = styleBinding.RowAnchoredPosition;
        runtimeBinding.rowOffset = styleBinding.RowOffset;
        runtimeBinding.backgroundOffset = styleBinding.BackgroundOffset;
        runtimeBinding.plateOffset = styleBinding.PlateOffset;
        runtimeBinding.iconOffset = styleBinding.IconOffset;
        runtimeBinding.overrideBackgroundRect = styleBinding.OverrideBackgroundRect;
        runtimeBinding.backgroundAnchoredPosition = styleBinding.BackgroundAnchoredPosition;
        runtimeBinding.backgroundSize = styleBinding.BackgroundSize;
        runtimeBinding.overridePlateRect = styleBinding.OverridePlateRect;
        runtimeBinding.plateAnchoredPosition = styleBinding.PlateAnchoredPosition;
        runtimeBinding.plateSize = styleBinding.PlateSize;
        runtimeBinding.overrideIconRect = styleBinding.OverrideIconRect;
        runtimeBinding.iconAnchoredPosition = styleBinding.IconAnchoredPosition;
        runtimeBinding.overrideRowSize = styleBinding.OverrideRowSize;
        runtimeBinding.rowSize = styleBinding.RowSize;
        runtimeBinding.ignoreParentLayoutWhenPositioned = styleBinding.IgnoreParentLayoutWhenPositioned;
        runtimeBinding.lineTextStyle = styleBinding.LineTextStyle;
        runtimeBinding.labelTextStyle = styleBinding.LabelTextStyle;
        runtimeBinding.valueTextStyle = styleBinding.ValueTextStyle;
    }

    void SanitizePlateIconTargets(StoryEndScreenStatBinding binding)
    {
        if (binding == null || binding.plateImage == null || binding.iconImage == null || binding.plateImage != binding.iconImage)
            return;

        binding.iconImage = null;
        AppLogger.Warn(
            AppLogCategory.EndScreen,
            nameof(StoryEndScreenController),
            nameof(SanitizePlateIconTargets),
            "Plate and icon pointed to the same Image. Icon target was cleared.",
            LogMetadata.Of("statId", binding.statId ?? "", "image", binding.plateImage != null ? binding.plateImage.name : ""),
            recoverable: true);
    }

    static Sprite ResolveStyleSprite(Sprite sprite, UnityEngine.Object source)
    {
        if (sprite != null)
            return sprite;
        if (source is Sprite sourceSprite)
            return sourceSprite;
        if (source is Image image)
            return image.sprite;
        if (source is SpriteRenderer spriteRenderer)
            return spriteRenderer.sprite;
        if (source is GameObject gameObject)
        {
            Image childImage = gameObject.GetComponent<Image>() ?? gameObject.GetComponentInChildren<Image>(true);
            if (childImage != null && childImage.sprite != null)
                return childImage.sprite;
            SpriteRenderer childRenderer = gameObject.GetComponent<SpriteRenderer>() ?? gameObject.GetComponentInChildren<SpriteRenderer>(true);
            return childRenderer != null ? childRenderer.sprite : null;
        }
        if (source is Component component)
        {
            Image childImage = component.GetComponent<Image>() ?? component.GetComponentInChildren<Image>(true);
            if (childImage != null && childImage.sprite != null)
                return childImage.sprite;
            SpriteRenderer childRenderer = component.GetComponent<SpriteRenderer>() ?? component.GetComponentInChildren<SpriteRenderer>(true);
            return childRenderer != null ? childRenderer.sprite : null;
        }

        return null;
    }

    static void CopyReferences(StoryEndScreenReferences source, StoryEndScreenReferences target, bool overwrite)
    {
        if (source == null || target == null)
            return;

        CopyObject(ref target.root, source.root, overwrite);
        CopyObject(ref target.canvasGroup, source.canvasGroup, overwrite);
        CopyObject(ref target.safeArea, source.safeArea, overwrite);
        CopyObject(ref target.panelRoot, source.panelRoot, overwrite);
        CopyObject(ref target.backgroundImage, source.backgroundImage, overwrite);
        CopyObject(ref target.backgroundOverride, source.backgroundOverride, overwrite);
        CopyObject(ref target.defaultBackground, source.defaultBackground, overwrite);
        CopyObject(ref target.titleText, source.titleText, overwrite);
        CopyObject(ref target.storyTitleText, source.storyTitleText, overwrite);
        CopyObject(ref target.completedEpisodeText, source.completedEpisodeText, overwrite);
        CopyObject(ref target.nextEpisodeText, source.nextEpisodeText, overwrite);
        CopyObject(ref target.statsContainer, source.statsContainer, overwrite);
        CopyObject(ref target.statRowTemplate, source.statRowTemplate, overwrite);
        CopyObject(ref target.statsBackgroundImage, source.statsBackgroundImage, overwrite);
        CopyObject(ref target.statsBackgroundOverride, source.statsBackgroundOverride, overwrite);
        target.hideStatsBackground = source.hideStatsBackground;
        CopyObject(ref target.legacyCityRow, source.legacyCityRow, overwrite);
        CopyObject(ref target.legacyFairytaleRow, source.legacyFairytaleRow, overwrite);
        CopyObject(ref target.legacyReputationRow, source.legacyReputationRow, overwrite);
        CopyObject(ref target.legacySparksRow, source.legacySparksRow, overwrite);
        CopyObject(ref target.legacyCandlesRow, source.legacyCandlesRow, overwrite);
        CopyObject(ref target.legacyCityImage, source.legacyCityImage, overwrite);
        CopyObject(ref target.legacyFairytaleImage, source.legacyFairytaleImage, overwrite);
        CopyObject(ref target.legacyReputationImage, source.legacyReputationImage, overwrite);
        CopyObject(ref target.legacySparksImage, source.legacySparksImage, overwrite);
        CopyObject(ref target.legacyCandlesImage, source.legacyCandlesImage, overwrite);
        CopyObject(ref target.legacyCityIconImage, source.legacyCityIconImage, overwrite);
        CopyObject(ref target.legacyFairytaleIconImage, source.legacyFairytaleIconImage, overwrite);
        CopyObject(ref target.legacyReputationIconImage, source.legacyReputationIconImage, overwrite);
        CopyObject(ref target.legacySparksIconImage, source.legacySparksIconImage, overwrite);
        CopyObject(ref target.legacyCandlesIconImage, source.legacyCandlesIconImage, overwrite);
        CopyObject(ref target.legacyCityText, source.legacyCityText, overwrite);
        CopyObject(ref target.legacyFairytaleText, source.legacyFairytaleText, overwrite);
        CopyObject(ref target.legacyReputationText, source.legacyReputationText, overwrite);
        CopyObject(ref target.legacySparksText, source.legacySparksText, overwrite);
        CopyObject(ref target.legacyCandlesText, source.legacyCandlesText, overwrite);
        CopyObject(ref target.continueButton, source.continueButton, overwrite);
        CopyObject(ref target.continueButtonPlateImage, source.continueButtonPlateImage, overwrite);
        CopyObject(ref target.continueButtonPlateSprite, source.continueButtonPlateSprite, overwrite);
        CopyObject(ref target.continueButtonPlateSpriteSource, source.continueButtonPlateSpriteSource, overwrite);
        CopyObject(ref target.continueButtonText, source.continueButtonText, overwrite);
        CopyObject(ref target.menuButton, source.menuButton, overwrite);
        CopyObject(ref target.nextEpisodeButton, source.nextEpisodeButton, overwrite);
        CopyObject(ref target.restartEpisodeButton, source.restartEpisodeButton, overwrite);
        CopyObject(ref target.closeButton, source.closeButton, overwrite);

        if (target.continueButton == null)
        {
            target.continueButton = FirstNonNull(
                source.continueButton,
                IsContinueButton(source.nextEpisodeButton) ? source.nextEpisodeButton : null,
                IsContinueButton(source.menuButton) ? source.menuButton : null);
        }

        if (target.continueButton != null)
        {
            if (target.menuButton == target.continueButton || IsContinueButton(target.menuButton))
                target.menuButton = null;
            if (target.nextEpisodeButton == target.continueButton || IsContinueButton(target.nextEpisodeButton))
                target.nextEpisodeButton = null;
            if (target.restartEpisodeButton == target.continueButton)
                target.restartEpisodeButton = null;
        }
    }

    static void CopyLayoutSettings(StoryEndScreenLayoutSettings source, StoryEndScreenLayoutSettings target)
    {
        if (source == null || target == null)
            return;

        target.applyLayoutInEditMode = source.applyLayoutInEditMode;
        target.keepTemplatesInactive = source.keepTemplatesInactive;
        target.clearGeneratedRowsBeforeRender = source.clearGeneratedRowsBeforeRender;
        target.forceRebuildLayout = source.forceRebuildLayout;
        target.stretchRootToScreen = source.stretchRootToScreen;
        target.useSafeAreaPadding = source.useSafeAreaPadding;
        target.safeAreaPadding = source.safeAreaPadding;
        target.statsSpacing = source.statsSpacing;
        target.statRowMinHeight = source.statRowMinHeight;
        target.statRowPreferredHeight = source.statRowPreferredHeight;
        target.statRowMaxWidth = source.statRowMaxWidth;
        target.centerStatsContainer = source.centerStatsContainer;
    }

    static void CopyPreviewSettings(StoryEndScreenPreviewSettings source, StoryEndScreenPreviewSettings target)
    {
        if (source == null || target == null)
            return;

        target.useSavedValuesInEditor = source.useSavedValuesInEditor;
        target.usePreviewFallbackValues = source.usePreviewFallbackValues;
        target.hideOtherStoryUiDuringPreview = source.hideOtherStoryUiDuringPreview;
        target.showNextEpisodeInPreview = source.showNextEpisodeInPreview;
        target.previewBackground = source.previewBackground;
        target.previewTitle = source.previewTitle;
        target.previewStoryTitle = source.previewStoryTitle;
        target.previewCompletedEpisodeTitle = source.previewCompletedEpisodeTitle;
        target.previewNextEpisodeTitle = source.previewNextEpisodeTitle;
        target.previewCity = source.previewCity;
        target.previewFairytale = source.previewFairytale;
        target.previewReputation = source.previewReputation;
        target.previewSparks = source.previewSparks;
        target.previewCandles = source.previewCandles;
    }

    static void CopyObject<T>(ref T target, T source, bool overwrite) where T : UnityEngine.Object
    {
        if (overwrite || target == null)
            target = source;
    }

    static StoryEndScreenStatBinding[] CloneStatBindings(StoryEndScreenStatBinding[] source)
    {
        if (source == null || source.Length == 0)
            return StoryEndScreenStatBinding.CreateDefaults();

        StoryEndScreenStatBinding[] result = new StoryEndScreenStatBinding[source.Length];
        for (int i = 0; i < source.Length; i++)
            result[i] = CloneStatBinding(source[i]);
        return result;
    }

    static StoryEndScreenStatBinding CloneStatBinding(StoryEndScreenStatBinding source)
    {
        if (source == null)
            return null;

        return new StoryEndScreenStatBinding
        {
            enabled = source.enabled,
            label = source.label,
            statId = source.statId,
            statAliases = source.statAliases != null ? (string[])source.statAliases.Clone() : Array.Empty<string>(),
            valueMode = source.valueMode,
            previewValue = source.previewValue,
            row = source.row,
            backgroundImage = source.backgroundImage,
            plateImage = source.plateImage,
            iconImage = source.iconImage,
            lineText = source.lineText,
            labelText = source.labelText,
            valueText = source.valueText,
            backgroundSprite = source.backgroundSprite,
            backgroundSpriteSource = source.backgroundSpriteSource,
            plateSprite = source.plateSprite,
            plateSpriteSource = source.plateSpriteSource,
            icon = source.icon,
            iconSpriteSource = source.iconSpriteSource,
            hideBackground = source.hideBackground,
            hidePlate = source.hidePlate,
            hideIcon = source.hideIcon,
            overrideIconSize = source.overrideIconSize,
            iconSize = source.iconSize,
            overrideRowPosition = source.overrideRowPosition,
            rowAnchoredPosition = source.rowAnchoredPosition,
            rowOffset = source.rowOffset,
            backgroundOffset = source.backgroundOffset,
            plateOffset = source.plateOffset,
            iconOffset = source.iconOffset,
            overrideBackgroundRect = source.overrideBackgroundRect,
            backgroundAnchoredPosition = source.backgroundAnchoredPosition,
            backgroundSize = source.backgroundSize,
            overridePlateRect = source.overridePlateRect,
            plateAnchoredPosition = source.plateAnchoredPosition,
            plateSize = source.plateSize,
            overrideIconRect = source.overrideIconRect,
            iconAnchoredPosition = source.iconAnchoredPosition,
            lineTextOffset = source.lineTextOffset,
            labelTextOffset = source.labelTextOffset,
            valueTextOffset = source.valueTextOffset,
            overrideRowSize = source.overrideRowSize,
            rowSize = source.rowSize,
            ignoreParentLayoutWhenPositioned = source.ignoreParentLayoutWhenPositioned,
            hideWhenZero = source.hideWhenZero,
            format = source.format,
            lineTextStyle = source.lineTextStyle,
            labelTextStyle = source.labelTextStyle,
            valueTextStyle = source.valueTextStyle
        };
    }

    static StoryEndScreenStatValue FindStat(StoryEndScreenData data, string labelOrId)
    {
        if (data == null || data.Stats == null || string.IsNullOrWhiteSpace(labelOrId))
            return null;

        string wanted = Normalize(labelOrId);
        for (int i = 0; i < data.Stats.Count; i++)
        {
            StoryEndScreenStatValue stat = data.Stats[i];
            if (stat == null)
                continue;

            if (string.Equals(Normalize(stat.Label), wanted, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Normalize(stat.StatId), wanted, StringComparison.OrdinalIgnoreCase))
            {
                return stat;
            }
        }

        return null;
    }

    static string BuildStatLine(StoryEndScreenStatValue stat)
    {
        if (stat == null)
            return "";

        return FirstNonEmpty(stat.Label, stat.StatId, "Стат") + ": " + FirstNonEmpty(stat.FormattedValue, stat.Value.ToString(CultureInfo.InvariantCulture));
    }

    static string ExtractSummaryLabel(string text)
    {
        text = StripRichTextTags(text ?? "").Trim();
        if (string.IsNullOrEmpty(text))
            return "";

        int colonIndex = text.IndexOf(':');
        if (colonIndex >= 0)
            text = text.Substring(0, colonIndex);

        return Normalize(text);
    }

    static string StripRichTextTags(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        int tagDepth = 0;
        var buffer = new char[value.Length];
        int length = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '<')
            {
                tagDepth++;
                continue;
            }

            if (c == '>' && tagDepth > 0)
            {
                tagDepth--;
                continue;
            }

            if (tagDepth == 0)
                buffer[length++] = c;
        }

        return new string(buffer, 0, length);
    }

    static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : string.Join(" ", value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    static string SanitizeName(string value)
    {
        value = Normalize(value);
        if (string.IsNullOrEmpty(value))
            return "Stat";

        var chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }

        return new string(chars);
    }

    static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return "";
    }

    static T FirstNonNull<T>(params T[] values) where T : UnityEngine.Object
    {
        if (values == null)
            return null;

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] != null)
                return values[i];
        }

        return null;
    }

    static RectTransform ResolveStatsContainer(Transform root)
    {
        RectTransform found = FindRect(root, "stats", "stat", "summary", "result");
        if (found != null)
            return found;

        TMP_Text statText = FindStatText(root, "город", "city", "town");
        return statText != null && statText.transform.parent != null
            ? statText.transform.parent as RectTransform
            : null;
    }

    static GameObject FindTemplate(Transform root)
    {
        if (root == null)
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null)
                continue;

            string name = transform.name.ToLowerInvariant();
            if ((name.Contains("template") || name.Contains("prefab")) &&
                (name.Contains("stat") || name.Contains("row") || name.Contains("result")))
            {
                return transform.gameObject;
            }
        }

        return null;
    }

    static RectTransform FindRect(Transform root, params string[] tokens)
    {
        Transform found = FindTransform(root, tokens);
        return found != null ? found.GetComponent<RectTransform>() : null;
    }

    static Image FindImage(Transform root, params string[] tokens)
    {
        Transform found = FindTransform(root, tokens);
        return found != null ? found.GetComponent<Image>() : null;
    }

    static RectTransform FindLegacyStatRow(Transform root, TMP_Text text, Image image, params string[] tokens)
    {
        if (root == null)
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null)
                continue;

            string haystack = (transform.name + "\n" + BuildTransformPath(transform)).ToLowerInvariant();
            if (ContainsAnyToken(haystack, tokens) && LooksLikeStatRow(transform))
                return transform as RectTransform;
        }

        return FindNearestStatRow(root, text, image);
    }

    static RectTransform FindNearestStatRow(Transform root, params Component[] components)
    {
        if (root == null || components == null)
            return null;

        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || !IsDescendantOf(component.transform, root))
                continue;

            Transform current = component.transform;
            while (current != null && current != root)
            {
                if (LooksLikeStatRow(current))
                    return current as RectTransform;
                current = current.parent;
            }
        }

        return null;
    }

    static bool LooksLikeStatRow(Transform transform)
    {
        if (transform == null || transform.GetComponent<RectTransform>() == null)
            return false;

        string name = transform.name.ToLowerInvariant();
        bool rowName = name.Contains("stat") || name.Contains("row") || name.Contains("result");
        bool hasText = transform.GetComponentsInChildren<TMP_Text>(true).Length > 0;
        bool hasImage = transform.GetComponentsInChildren<Image>(true).Length > 0;
        return rowName && (hasText || hasImage);
    }

    static Image FindLegacyStatBackplateImage(Transform root, RectTransform row, params string[] tokens)
    {
        Image image = FindImageInStatRow(row, false);
        return image != null ? image : FindLegacyStatImage(root, false, tokens);
    }

    static Image FindLegacyStatIconImage(Transform root, RectTransform row, params string[] tokens)
    {
        Image image = FindImageInStatRow(row, true);
        return image != null ? image : FindLegacyStatImage(root, true, tokens);
    }

    enum EndScreenImageRole
    {
        Background,
        Plate,
        Icon
    }

    static Image FindStatRowImageByRole(Transform row, EndScreenImageRole role)
    {
        if (row == null)
            return null;

        Image[] images = row.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
            return null;

        string[] tokens = role == EndScreenImageRole.Background
            ? new[] { "background", "bg", "fon", "фон" }
            : role == EndScreenImageRole.Icon
                ? new[] { "icon", "medallion", "currency", "stat-icon", "иконка" }
                : new[] { "plate", "back", "frame", "panel", "plaque", "input-field", "input", "field", "подложка", "плашка" };

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            string haystack = (image.name + "\n" + BuildTransformPath(image.transform)).ToLowerInvariant();
            if (ContainsAnyToken(haystack, tokens))
                return image;
        }

        return null;
    }

    static Image FindImageInStatRow(RectTransform row, bool preferIcon)
    {
        if (row == null)
            return null;

        return FindStatRowImageByRole(row, preferIcon ? EndScreenImageRole.Icon : EndScreenImageRole.Plate);
    }

    static bool LooksLikeBackplateImageName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return name.Contains("plate") ||
            name.Contains("back") ||
            name.Contains("background") ||
            name.Contains("bg") ||
            name.Contains("panel") ||
            name.Contains("frame") ||
            name.Contains("row");
    }

    static Image FindLegacyStatImage(Transform root, bool preferIcon, params string[] tokens)
    {
        if (root == null)
            return null;

        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            string haystack = (image.name + "\n" + BuildTransformPath(image.transform)).ToLowerInvariant();
            if (!ContainsAnyToken(haystack, tokens))
                continue;

            bool nameLooksIcon = image.name.ToLowerInvariant().Contains("icon");
            if (preferIcon == nameLooksIcon)
                return image;
        }

        return null;
    }

    static bool IsDescendantOf(Transform transform, Transform root)
    {
        while (transform != null)
        {
            if (transform == root)
                return true;
            transform = transform.parent;
        }

        return false;
    }

    static Button FindButton(Transform root, params string[] tokens)
    {
        Transform found = FindTransform(root, tokens);
        return found != null ? found.GetComponent<Button>() : null;
    }

    static bool IsContinueButton(Button button)
    {
        if (button == null)
            return false;

        string haystack = (button.name + "\n" + BuildTransformPath(button.transform)).ToLowerInvariant();
        if (ContainsAnyToken(haystack, "continue", "next", "прод"))
            return true;

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            string value = (text.name + "\n" + text.text).ToLowerInvariant();
            if (ContainsAnyToken(value, "continue", "next", "прод"))
                return true;
        }

        return false;
    }

    static TMP_Text FindTextByTokens(Transform root, params string[] tokens)
    {
        if (root == null)
            return null;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            string haystack = (text.name + "\n" + text.text).ToLowerInvariant();
            if (ContainsAnyToken(haystack, tokens))
                return text;
        }

        return null;
    }

    static TMP_Text FindStatText(Transform root, params string[] tokens)
    {
        if (root == null)
            return null;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            string haystack = (text.name + "\n" + StripRichTextTags(text.text)).ToLowerInvariant();
            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                string token = tokens[tokenIndex];
                if (!string.IsNullOrWhiteSpace(token) && haystack.Contains(token.ToLowerInvariant()))
                    return text;
            }
        }

        return null;
    }

    static Transform FindTransform(Transform root, params string[] tokens)
    {
        if (root == null)
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null)
                continue;

            if (ContainsAnyToken(transform.name.ToLowerInvariant(), tokens))
                return transform;
        }

        return null;
    }

    static string BuildTransformPath(Transform transform)
    {
        if (transform == null)
            return "";

        var parts = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    static bool ContainsAnyToken(string haystack, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(haystack) || tokens == null || tokens.Length == 0)
            return false;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (string.IsNullOrWhiteSpace(token))
                continue;

            if (haystack.Contains(token.ToLowerInvariant()))
                return true;
        }

        return false;
    }
}
