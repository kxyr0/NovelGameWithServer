using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class StoryScreenNavigator : MonoBehaviour
{
    private const string DefaultMenuScreenId = "MainScreen";
    private const string DefaultStoryScreenId = "Story";

    [Serializable]
    public struct ScreenBinding
    {
        [SerializeField]
        [FormerlySerializedAs("idScreen")]
        private string _screenId;

        [SerializeField]
        [FormerlySerializedAs("mainScreen")]
        private CanvasGroup _mainScreen;

        [SerializeField]
        [FormerlySerializedAs("openButton")]
        private Button _openButton;

        [SerializeField]
        [FormerlySerializedAs("closeButton")]
        private Button _closeButton;

        public string ScreenId => UIScreenState.NormalizeScreenId(_screenId);
        public CanvasGroup MainScreen => _mainScreen;
        public Button OpenButton => _openButton;
        public Button CloseButton => _closeButton;
        public bool HasScreen => _mainScreen != null && ScreenId.Length > 0;

        public ScreenBinding(string screenId, CanvasGroup mainScreen, Button openButton, Button closeButton)
        {
            _screenId = UIScreenState.NormalizeScreenId(screenId);
            _mainScreen = mainScreen;
            _openButton = openButton;
            _closeButton = closeButton;
        }
    }

    private sealed class ButtonRegistration
    {
        private readonly Button _button;
        private readonly UnityAction _action;

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

    [Header("Screens")]
    [SerializeField]
    [FormerlySerializedAs("screens")]
    private ScreenBinding[] _screens = Array.Empty<ScreenBinding>();

    [SerializeField]
    [FormerlySerializedAs("initialScreenId")]
    private string _initialScreenId = DefaultMenuScreenId;

    [SerializeField]
    [FormerlySerializedAs("menuScreenId")]
    private string _menuScreenId = DefaultMenuScreenId;

    [SerializeField]
    [FormerlySerializedAs("storyScreenId")]
    private string _storyScreenId = DefaultStoryScreenId;

    [Header("Screen Markers")]
    [SerializeField]
    [Tooltip("Добавлять экраны из UIScreenMarker в кеш навигации. Это не ищет по именам, а использует явно назначенный Screen Id на маркере.")]
    private bool _includeScreenMarkers = true;

    [SerializeField]
    [Tooltip("Если задан, маркеры берутся только внутри этого root. Если пусто, используются все UIScreenMarker в сцене.")]
    private Transform _screenMarkerSearchRoot;

    [SerializeField]
    [HideInInspector]
    [FormerlySerializedAs("menuScreen")]
    private GameObject _legacyMenuScreen;

    [SerializeField]
    [HideInInspector]
    [FormerlySerializedAs("storyScreen")]
    private GameObject _legacyStoryScreen;

    [Header("Transition")]
    [SerializeField]
    [FormerlySerializedAs("screenTransitionAnimator")]
    private UIScreenTransitionAnimator _screenTransitionAnimator;

    [SerializeField]
    [FormerlySerializedAs("screenTransition")]
    private UIScreenTransitionType _screenTransition = UIScreenTransitionType.SlideLeft;

    [SerializeField]
    [FormerlySerializedAs("screenTransitionDuration")]
    private float _screenTransitionDuration = 0.35f;

    [SerializeField]
    [FormerlySerializedAs("screenTransitionEase")]
    private Ease _screenTransitionEase = Ease.OutCubic;

    [SerializeField]
    [FormerlySerializedAs("screenTransitionUsesUnscaledTime")]
    private bool _screenTransitionUsesUnscaledTime = true;

    [SerializeField]
    [Tooltip("When enabled, tab-like screen changes use the screen order to choose forward/backward transition direction.")]
    private bool _useScreenOrderForTransitionDirection = true;

    private readonly Dictionary<string, CanvasGroup> _screenGroups = new Dictionary<string, CanvasGroup>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _screenOrder = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<ButtonRegistration> _buttonRegistrations = new List<ButtonRegistration>();
    private readonly List<ScreenCanvasState> _overlayScreenStates = new List<ScreenCanvasState>();
    private string _currentScreenId = "";
    private string _overlayPreviousScreenId = "";
    private bool _overlayScreensHidden;

    private struct ScreenCanvasState
    {
        public CanvasGroup Group;
        public bool ActiveSelf;
        public float Alpha;
        public bool Interactable;
        public bool BlocksRaycasts;
        public bool IgnoreParentGroups;

        public ScreenCanvasState(CanvasGroup group)
        {
            Group = group;
            ActiveSelf = group != null && group.gameObject.activeSelf;
            Alpha = group != null ? group.alpha : 0f;
            Interactable = group != null && group.interactable;
            BlocksRaycasts = group != null && group.blocksRaycasts;
            IgnoreParentGroups = group != null && group.ignoreParentGroups;
        }
    }

    public string CurrentScreenId => _currentScreenId;
    public CanvasGroup MenuCanvasGroup => GetScreenCanvasGroup(_menuScreenId);
    public CanvasGroup StoryCanvasGroup => GetScreenCanvasGroup(_storyScreenId);
    public GameObject MenuScreen => MenuCanvasGroup != null ? MenuCanvasGroup.gameObject : null;
    public GameObject StoryScreen => StoryCanvasGroup != null ? StoryCanvasGroup.gameObject : null;

    public UIScreenTransitionType ScreenTransition
    {
        get => _screenTransition;
        set
        {
            _screenTransition = value;
            ApplyTransitionSettings();
        }
    }

    public void ConfigureScreenTransition(
        UIScreenTransitionType transition,
        float transitionDuration,
        Ease transitionEase,
        bool usesUnscaledTime,
        bool useScreenOrderForDirection)
    {
        _screenTransition = transition;
        _screenTransitionDuration = Mathf.Max(0f, transitionDuration);
        _screenTransitionEase = transitionEase;
        _screenTransitionUsesUnscaledTime = usesUnscaledTime;
        _useScreenOrderForTransitionDirection = useScreenOrderForDirection;
        ApplyTransitionSettings();
    }

    private void Awake()
    {
        EnsureReferences();
        PrepareInitialState();
    }

    private void OnEnable()
    {
        RegisterButtonListeners();
    }

    private void OnDisable()
    {
        ClearButtonListeners();
    }

    private void OnValidate()
    {
        _screenTransitionDuration = Mathf.Max(0f, _screenTransitionDuration);
        _initialScreenId = UIScreenState.NormalizeScreenId(_initialScreenId);
        _menuScreenId = UIScreenState.NormalizeScreenId(_menuScreenId);
        _storyScreenId = UIScreenState.NormalizeScreenId(_storyScreenId);
    }

    public void SetScreens(CanvasGroup menuScreen, CanvasGroup storyScreen)
    {
        _screens = new[]
        {
            new ScreenBinding(_menuScreenId, menuScreen, null, null),
            new ScreenBinding(_storyScreenId, storyScreen, null, null)
        };

        EnsureReferences();
        PrepareInitialState();
    }

    public void PrepareInitialState()
    {
        EnsureReferences();
        string initialScreenId = AccountLoginState.ResolveInitialScreen(_initialScreenId);
        if (!_screenGroups.ContainsKey(initialScreenId))
        {
            Debug.LogWarning(
                $"StoryScreenNavigator: startup screen '{initialScreenId}' is not assigned; " +
                $"using '{_initialScreenId}'.",
                this);
            initialScreenId = _initialScreenId;
        }
        OpenScreenImmediate(initialScreenId);
    }

    public bool OpenScreen(string screenId)
    {
        return OpenScreen(screenId, null);
    }

    public bool OpenScreen(string screenId, Action onComplete)
    {
        EnsureReferences();
        if (_overlayScreensHidden)
            RestoreScreensAfterOverlay();

        string targetScreenId = UIScreenState.NormalizeScreenId(screenId);
        if (targetScreenId.Length == 0)
            return false;

        CanvasGroup toGroup = GetScreenCanvasGroup(targetScreenId);
        if (toGroup == null)
        {
            AppLogger.Warn(
                AppLogCategory.ScreenNavigation,
                nameof(StoryScreenNavigator),
                nameof(OpenScreen),
                "[SCREEN][OPEN_FAILED] Target screen is not assigned.",
                LogMetadata.Of(
                    "requestedScreenId", screenId ?? "",
                    "targetScreenId", targetScreenId,
                    "currentScreenId", _currentScreenId,
                    "knownScreens", string.Join(",", _screenGroups.Keys)),
                recoverable: true);
            Debug.LogWarning($"StoryScreenNavigator: screen '{targetScreenId}' is not assigned.", this);
            return false;
        }

        if (_currentScreenId == targetScreenId)
        {
            AppLogger.Info(
                AppLogCategory.ScreenNavigation,
                nameof(StoryScreenNavigator),
                nameof(OpenScreen),
                "[SCREEN][OPEN] Target screen is already current; forcing visible state.",
                LogMetadata.Of(
                    "targetScreenId", targetScreenId,
                    "targetObject", toGroup != null ? toGroup.name : ""));
            SetScreenVisible(toGroup, true);
            UIScreenState.SetCurrentScreen(targetScreenId);
            UIScreenState.SetSelectedScreen(targetScreenId);
            SafeInvoke(onComplete);
            return true;
        }

        string fromScreenId = _currentScreenId;
        CanvasGroup fromGroup = GetScreenCanvasGroup(fromScreenId);

        AppLogger.Info(
            AppLogCategory.ScreenNavigation,
            nameof(StoryScreenNavigator),
            nameof(OpenScreen),
            "[SCREEN][OPEN] Opening screen.",
            LogMetadata.Of(
                "fromScreenId", fromScreenId,
                "targetScreenId", targetScreenId,
                "fromObject", fromGroup != null ? fromGroup.name : "",
                "targetObject", toGroup.name,
                "hasTransitionAnimator", _screenTransitionAnimator != null,
                "transition", _screenTransition.ToString(),
                "overlayScreensHidden", _overlayScreensHidden));

        UIScreenState.SetCurrentScreen(targetScreenId);
        UIScreenState.SetSelectedScreen(targetScreenId);
        HideScreensExcept(fromScreenId, targetScreenId);

        if (_screenTransitionAnimator == null || fromGroup == null)
        {
            SetScreenVisible(fromGroup, false);
            SetScreenVisible(toGroup, true);
            CompleteScreenChange(targetScreenId, onComplete);
            return true;
        }

        ApplyTransitionSettings();
        bool reverse = ShouldReverseTransition(fromScreenId, targetScreenId);
        _screenTransitionAnimator.Play(
            fromGroup.gameObject,
            toGroup.gameObject,
            reverse,
            () => CompleteScreenChange(targetScreenId, onComplete));

        return true;
    }

    public bool CloseScreen(string screenId)
    {
        string normalizedScreenId = UIScreenState.NormalizeScreenId(screenId);
        if (normalizedScreenId.Length == 0)
            return false;

        if (normalizedScreenId == _initialScreenId)
            return OpenScreen(_initialScreenId);

        return OpenScreen(_initialScreenId);
    }

    public void ShowStoryScreen(Action onComplete = null)
    {
        OpenScreen(_storyScreenId, onComplete);
    }

    public bool ShowStoryScreenImmediate(Action onComplete = null)
    {
        return OpenScreenImmediatePublic(_storyScreenId, onComplete);
    }

    public void ShowMenuScreen(Action onComplete = null)
    {
        OpenScreen(_menuScreenId, onComplete);
    }

    public bool ShowMenuScreenImmediate(Action onComplete = null)
    {
        return OpenScreenImmediatePublic(_menuScreenId, onComplete);
    }

    public void HideScreensForOverlay(params GameObject[] overlayRoots)
    {
        EnsureReferences();
        if (_screenTransitionAnimator != null)
            _screenTransitionAnimator.CancelActiveTransition();

        AppLogger.Info(
            AppLogCategory.ScreenNavigation,
            nameof(StoryScreenNavigator),
            nameof(HideScreensForOverlay),
            "[SCREEN][OVERLAY_HIDE] Hiding screens for overlay.",
            LogMetadata.Of(
                "currentScreenId", _currentScreenId,
                "alreadyHidden", _overlayScreensHidden,
                "screenCount", _screenGroups.Count,
                "overlayRoots", JoinObjectNames(overlayRoots)));

        if (!_overlayScreensHidden)
        {
            _overlayScreenStates.Clear();
            _overlayPreviousScreenId = _currentScreenId;
            _overlayScreensHidden = true;
        }

        foreach (KeyValuePair<string, CanvasGroup> pair in _screenGroups)
        {
            CanvasGroup group = pair.Value;
            if (group == null || IsRelatedToOverlayRoot(group.transform, overlayRoots))
                continue;

            CaptureOverlayScreenState(group);
            SetScreenVisible(group, false);
        }

        _currentScreenId = "";
        UIScreenState.SetCurrentScreen("");
    }

    public void RestoreScreensAfterOverlay()
    {
        if (!_overlayScreensHidden)
        {
            AppLogger.Info(
                AppLogCategory.ScreenNavigation,
                nameof(StoryScreenNavigator),
                nameof(RestoreScreensAfterOverlay),
                "[SCREEN][OVERLAY_RESTORE] Restore requested, but no overlay-hidden screens are cached.",
                LogMetadata.Of("currentScreenId", _currentScreenId));
            return;
        }

        if (_screenTransitionAnimator != null)
            _screenTransitionAnimator.CancelActiveTransition();

        AppLogger.Info(
            AppLogCategory.ScreenNavigation,
            nameof(StoryScreenNavigator),
            nameof(RestoreScreensAfterOverlay),
            "[SCREEN][OVERLAY_RESTORE] Restoring screens after overlay.",
            LogMetadata.Of(
                "previousScreenId", _overlayPreviousScreenId,
                "stateCount", _overlayScreenStates.Count));

        for (int i = 0; i < _overlayScreenStates.Count; i++)
            RestoreScreenCanvasState(_overlayScreenStates[i]);

        _overlayScreenStates.Clear();
        if (!string.IsNullOrEmpty(_overlayPreviousScreenId))
        {
            _currentScreenId = _overlayPreviousScreenId;
            UIScreenState.SetCurrentScreen(_overlayPreviousScreenId);
        }

        _overlayPreviousScreenId = "";
        _overlayScreensHidden = false;
    }

    private void CompleteScreenChange(string targetScreenId, Action onComplete)
    {
        _currentScreenId = targetScreenId;
        HideScreensExcept("", targetScreenId);
        SetScreenVisible(GetScreenCanvasGroup(targetScreenId), true);
        UIScreenState.SetCurrentScreen(targetScreenId);
        UIScreenState.SetSelectedScreen(targetScreenId);
        SafeInvoke(onComplete);
    }

    private bool OpenScreenImmediatePublic(string screenId, Action onComplete)
    {
        EnsureReferences();
        if (_overlayScreensHidden)
            RestoreScreensAfterOverlay();

        string targetScreenId = UIScreenState.NormalizeScreenId(screenId);
        if (targetScreenId.Length == 0 || GetScreenCanvasGroup(targetScreenId) == null)
            return false;

        if (_screenTransitionAnimator != null)
            _screenTransitionAnimator.CancelActiveTransition();

        OpenScreenImmediate(targetScreenId);
        SafeInvoke(onComplete);
        return true;
    }

    private void OpenScreenImmediate(string screenId)
    {
        string targetScreenId = UIScreenState.NormalizeScreenId(screenId);
        if (targetScreenId.Length == 0)
            targetScreenId = _menuScreenId;

        foreach (KeyValuePair<string, CanvasGroup> pair in _screenGroups)
        {
            bool visible = pair.Key == targetScreenId;
            if (_screenTransitionAnimator != null)
                _screenTransitionAnimator.ResetPage(pair.Value != null ? pair.Value.gameObject : null, visible);
            else
                SetScreenVisible(pair.Value, visible);
        }

        _currentScreenId = targetScreenId;
        UIScreenState.SetCurrentScreen(targetScreenId);
        UIScreenState.SetSelectedScreen(targetScreenId);
    }

    private CanvasGroup GetScreenCanvasGroup(string screenId)
    {
        EnsureReferences();

        screenId = UIScreenState.NormalizeScreenId(screenId);
        if (screenId.Length == 0)
            return null;

        return _screenGroups.TryGetValue(screenId, out CanvasGroup group) ? group : null;
    }

    private void EnsureReferences()
    {
        if (_screenTransitionAnimator == null)
            _screenTransitionAnimator = GetComponent<UIScreenTransitionAnimator>() ?? gameObject.AddComponent<UIScreenTransitionAnimator>();

        RebuildScreenCache();
        ApplyTransitionSettings();
    }

    private void RebuildScreenCache()
    {
        _screenGroups.Clear();
        _screenOrder.Clear();

        if (_screens != null)
        {
            foreach (ScreenBinding screen in _screens)
            {
                if (!screen.HasScreen)
                    continue;

                TryAddScreen(screen.ScreenId, screen.MainScreen);
            }
        }

        AddScreensFromMarkers();

        TryAddScreen(_menuScreenId, GetOrAddCanvasGroup(_legacyMenuScreen));
        TryAddScreen(_storyScreenId, GetOrAddCanvasGroup(_legacyStoryScreen));
    }

    private void AddScreensFromMarkers()
    {
        if (!_includeScreenMarkers)
            return;

        if (_screenMarkerSearchRoot != null)
        {
            UIScreenMarker[] markers = _screenMarkerSearchRoot.GetComponentsInChildren<UIScreenMarker>(true);
            AddScreensFromMarkers(markers);
            return;
        }

        AddScreensFromMarkers(FindObjectsOfType<UIScreenMarker>(true));
    }

    private void AddScreensFromMarkers(UIScreenMarker[] markers)
    {
        if (markers == null)
            return;

        foreach (UIScreenMarker marker in markers)
        {
            if (marker == null)
                continue;

            string screenId = marker.ScreenId;
            if (screenId.Length == 0)
                continue;

            CanvasGroup group = GetOrAddCanvasGroup(marker.gameObject);
            TryAddScreen(screenId, group);
        }
    }

    private void TryAddScreen(string screenId, CanvasGroup group)
    {
        screenId = UIScreenState.NormalizeScreenId(screenId);
        if (screenId.Length == 0 || group == null || _screenGroups.ContainsKey(screenId))
            return;

        _screenGroups.Add(screenId, group);
        _screenOrder.Add(screenId, _screenOrder.Count);
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject screenRoot)
    {
        if (screenRoot == null)
            return null;

        CanvasGroup group = screenRoot.GetComponent<CanvasGroup>();
        if (group == null)
            group = screenRoot.AddComponent<CanvasGroup>();

        return group;
    }

    private void RegisterButtonListeners()
    {
        ClearButtonListeners();

        if (_screens == null)
            return;

        foreach (ScreenBinding screen in _screens)
        {
            string screenId = screen.ScreenId;
            if (screenId.Length == 0)
                continue;

            RegisterButton(screen.OpenButton, () => OpenScreen(screenId));
            RegisterButton(screen.CloseButton, () => CloseScreen(screenId));
        }
    }

    private void RegisterButton(Button button, UnityAction action)
    {
        if (button == null || action == null)
            return;

        ButtonRegistration registration = new ButtonRegistration(button, action);
        registration.Add();
        _buttonRegistrations.Add(registration);
    }

    private void ClearButtonListeners()
    {
        foreach (ButtonRegistration registration in _buttonRegistrations)
            registration.Remove();

        _buttonRegistrations.Clear();
    }

    private void HideScreensExcept(string firstScreenId, string secondScreenId)
    {
        firstScreenId = UIScreenState.NormalizeScreenId(firstScreenId);
        secondScreenId = UIScreenState.NormalizeScreenId(secondScreenId);

        foreach (KeyValuePair<string, CanvasGroup> pair in _screenGroups)
        {
            if (pair.Key == firstScreenId || pair.Key == secondScreenId)
                continue;

            SetScreenVisible(pair.Value, false);
        }
    }

    private void ApplyTransitionSettings()
    {
        if (_screenTransitionAnimator == null)
            return;

        _screenTransitionAnimator.Configure(
            _screenTransition,
            _screenTransitionDuration,
            _screenTransitionEase,
            _screenTransitionUsesUnscaledTime);
    }

    private bool ShouldReverseTransition(string fromScreenId, string targetScreenId)
    {
        fromScreenId = UIScreenState.NormalizeScreenId(fromScreenId);
        targetScreenId = UIScreenState.NormalizeScreenId(targetScreenId);

        if (_useScreenOrderForTransitionDirection &&
            _screenOrder.TryGetValue(fromScreenId, out int fromOrder) &&
            _screenOrder.TryGetValue(targetScreenId, out int targetOrder) &&
            fromOrder != targetOrder)
        {
            return targetOrder < fromOrder;
        }

        return targetScreenId == _initialScreenId;
    }

    private static bool IsRelatedToOverlayRoot(Transform screenRoot, GameObject[] overlayRoots)
    {
        if (screenRoot == null || overlayRoots == null)
            return false;

        for (int i = 0; i < overlayRoots.Length; i++)
        {
            GameObject overlayRoot = overlayRoots[i];
            if (overlayRoot == null)
                continue;

            Transform overlayTransform = overlayRoot.transform;
            if (overlayTransform == null)
                continue;

            if (overlayTransform == screenRoot ||
                overlayTransform.IsChildOf(screenRoot) ||
                screenRoot.IsChildOf(overlayTransform))
            {
                return true;
            }
        }

        return false;
    }

    private void CaptureOverlayScreenState(CanvasGroup group)
    {
        if (group == null)
            return;

        for (int i = 0; i < _overlayScreenStates.Count; i++)
        {
            if (_overlayScreenStates[i].Group == group)
                return;
        }

        _overlayScreenStates.Add(new ScreenCanvasState(group));
    }

    private static void RestoreScreenCanvasState(ScreenCanvasState state)
    {
        CanvasGroup group = state.Group;
        if (group == null)
            return;

        if (state.ActiveSelf && !group.gameObject.activeSelf)
            group.gameObject.SetActive(true);

        group.alpha = state.Alpha;
        group.interactable = state.Interactable;
        group.blocksRaycasts = state.BlocksRaycasts;
        group.ignoreParentGroups = state.IgnoreParentGroups;

        if (!state.ActiveSelf && group.gameObject.activeSelf)
            group.gameObject.SetActive(false);
    }

    private void SetScreenVisible(CanvasGroup group, bool visible)
    {
        if (group == null)
            return;

        AppLogger.DebugLog(
            AppLogCategory.ScreenNavigation,
            nameof(StoryScreenNavigator),
            nameof(SetScreenVisible),
            "[SCREEN][CANVAS] Applying CanvasGroup visibility.",
            LogMetadata.Of(
                "object", group.name,
                "visible", visible,
                "activeBefore", group.gameObject.activeSelf,
                "alphaBefore", group.alpha,
                "interactableBefore", group.interactable,
                "blocksRaycastsBefore", group.blocksRaycasts));

        if (visible && !group.gameObject.activeSelf)
            group.gameObject.SetActive(true);

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    private static string JoinObjectNames(GameObject[] objects)
    {
        if (objects == null || objects.Length == 0)
            return "";

        List<string> names = new List<string>();
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                names.Add(objects[i].name);
        }

        return string.Join(",", names);
    }

    private void SafeInvoke(Action callback)
    {
        try
        {
            callback?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(new Exception("StoryScreenNavigator: completion callback failed.", exception), this);
        }
    }

}
