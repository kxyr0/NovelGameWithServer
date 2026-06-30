using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Screen Exit Button")]
public sealed class UIScreenExitButton : MonoBehaviour
{
    [Header("Button")]
    [SerializeField] private Button _button;
    [SerializeField] private bool _bindOnEnable = true;

    [Header("Navigation")]
    [SerializeField] private StoryScreenNavigator _screenNavigator;
    [SerializeField] private bool _autoFindNavigator = true;
    [SerializeField] private string _targetScreenId = "MainScreen";

    [Header("Fallback roots")]
    [SerializeField] private GameObject _sourceRoot;
    [SerializeField] private bool _hideSourceRootAfterNavigation = true;
    [SerializeField] private bool _deactivateHiddenSourceRoot;
    [SerializeField] private GameObject _targetRoot;
    [SerializeField] private bool _showTargetRootWhenNoNavigator = true;

    [Header("Events")]
    [SerializeField] private UnityEvent _beforeExit;
    [SerializeField] private UnityEvent _afterExit;

    public string TargetScreenId
    {
        get => UIScreenState.NormalizeScreenId(_targetScreenId);
        set => _targetScreenId = UIScreenState.NormalizeScreenId(value);
    }

    private void Awake()
    {
        EnsureButton();
    }

    private void OnEnable()
    {
        if (_bindOnEnable)
            BindButton();
    }

    private void OnDisable()
    {
        UnbindButton();
    }

    private void OnValidate()
    {
        EnsureButton();
        _targetScreenId = UIScreenState.NormalizeScreenId(_targetScreenId);
    }

    public void ExitToTargetScreen()
    {
        Debug.Log("Начало");
        string targetScreenId = TargetScreenId;
        GameObject sourceRoot = ResolveSourceRoot();
        GameObject targetRoot = ResolveTargetRoot(targetScreenId);
        StoryScreenNavigator navigator = ResolveNavigator();
        Debug.Log("Дошло");

        AppLogger.Info(
            AppLogCategory.ScreenNavigation,
            nameof(UIScreenExitButton),
            nameof(ExitToTargetScreen),
            "[SCREEN][EXIT_BUTTON] Exit button requested screen change.",
            LogMetadata.Of(
                "button", name,
                "targetScreenId", targetScreenId,
                "sourceRoot", sourceRoot != null ? sourceRoot.name : "",
                "targetRoot", targetRoot != null ? targetRoot.name : "",
                "hasNavigator", navigator != null));

        SafeInvoke(_beforeExit);

        if (navigator != null && targetScreenId.Length > 0)
        {
            bool opened = navigator.OpenScreen(targetScreenId, () => CompleteExit(sourceRoot, targetRoot, targetScreenId));
            if (opened)
                return;
        }

        if (_showTargetRootWhenNoNavigator && targetRoot == null)
        {
            AppLogger.Warn(
                AppLogCategory.ScreenNavigation,
                nameof(UIScreenExitButton),
                nameof(ExitToTargetScreen),
                "[SCREEN][EXIT_BUTTON] No navigator route and no fallback target root were found.",
                LogMetadata.Of(
                    "button", name,
                    "targetScreenId", targetScreenId,
                    "sourceRoot", sourceRoot != null ? sourceRoot.name : ""),
                recoverable: true);
        }

        if (_showTargetRootWhenNoNavigator && targetRoot != null)
            SetRootVisible(targetRoot, true, false);

        if (_hideSourceRootAfterNavigation)
            HideSourceRootIfSafe(sourceRoot, targetRoot, targetScreenId);

        if (targetScreenId.Length > 0)
        {
            UIScreenState.SetCurrentScreen(targetScreenId);
            UIScreenState.SetSelectedScreen(targetScreenId);
        }

        SafeInvoke(_afterExit);
    }

    public void ExitToScreen(string targetScreenId)
    {
        TargetScreenId = targetScreenId;
        ExitToTargetScreen();
    }

    public void SetTargetScreen(string targetScreenId)
    {
        TargetScreenId = targetScreenId;
    }

    private void CompleteExit(GameObject sourceRoot, GameObject targetRoot, string targetScreenId)
    {
        if (_hideSourceRootAfterNavigation)
            HideSourceRootIfSafe(sourceRoot, targetRoot, targetScreenId);

        SafeInvoke(_afterExit);
    }

    private StoryScreenNavigator ResolveNavigator()
    {
        if (_screenNavigator != null)
            return _screenNavigator;

        if (!_autoFindNavigator)
            return null;

        StoryScreenNavigator[] navigators = FindObjectsOfType<StoryScreenNavigator>(true);
        StoryScreenNavigator fallback = null;
        for (int i = 0; i < navigators.Length; i++)
        {
            StoryScreenNavigator navigator = navigators[i];
            if (navigator == null)
                continue;

            if (fallback == null)
                fallback = navigator;

            if (navigator.isActiveAndEnabled)
                return navigator;
        }

        return fallback;
    }

    private GameObject ResolveSourceRoot()
    {
        if (_sourceRoot != null)
            return _sourceRoot;

        UIScreenMarker marker = GetComponentInParent<UIScreenMarker>();
        return marker != null ? marker.gameObject : null;
    }

    private GameObject ResolveTargetRoot(string targetScreenId)
    {
        if (_targetRoot != null)
            return _targetRoot;

        targetScreenId = UIScreenState.NormalizeScreenId(targetScreenId);
        if (targetScreenId.Length == 0)
            return null;

        UIScreenMarker[] markers = FindObjectsOfType<UIScreenMarker>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            UIScreenMarker marker = markers[i];
            if (marker != null && marker.ScreenId == targetScreenId)
                return marker.gameObject;
        }

        return null;
    }

    private void HideSourceRootIfSafe(GameObject sourceRoot, GameObject targetRoot, string targetScreenId)
    {
        if (!CanHideSourceRoot(sourceRoot, targetRoot, targetScreenId))
            return;

        SetRootVisible(sourceRoot, false, _deactivateHiddenSourceRoot);
    }

    private static bool CanHideSourceRoot(GameObject sourceRoot, GameObject targetRoot, string targetScreenId)
    {
        if (sourceRoot == null)
            return false;

        if (targetRoot != null)
        {
            if (sourceRoot == targetRoot)
                return false;

            if (targetRoot.transform.IsChildOf(sourceRoot.transform))
                return false;
        }

        UIScreenMarker sourceMarker = sourceRoot.GetComponent<UIScreenMarker>();
        if (sourceMarker != null && sourceMarker.ScreenId == UIScreenState.NormalizeScreenId(targetScreenId))
            return false;

        return true;
    }

    private static void SetRootVisible(GameObject root, bool visible, bool deactivateWhenHidden)
    {
        if (root == null)
            return;

        if (visible && !root.activeSelf)
            root.SetActive(true);

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null)
            group = root.AddComponent<CanvasGroup>();

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;

        if (!visible && deactivateWhenHidden && root.activeSelf)
            root.SetActive(false);
    }

    private Button EnsureButton()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        return _button;
    }

    private void BindButton()
    {
        Button button = EnsureButton();
        if (button == null)
            return;

        button.onClick.RemoveListener(ExitToTargetScreen);
        button.onClick.AddListener(ExitToTargetScreen);
    }

    private void UnbindButton()
    {
        if (_button != null)
            _button.onClick.RemoveListener(ExitToTargetScreen);
    }

    private static void SafeInvoke(UnityEvent unityEvent)
    {
        try
        {
            unityEvent?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
