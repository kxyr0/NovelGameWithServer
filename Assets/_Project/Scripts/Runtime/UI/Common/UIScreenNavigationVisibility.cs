using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
[AddComponentMenu("Nocturne/UI/UIScreenNavigationVisibility")]
public sealed class UIScreenNavigationVisibility : MonoBehaviour
{
    public enum VisibilityMode
    {
        HideOnListedScreens,
        ShowOnlyOnListedScreens
    }

    [Header("Rule")]
    [SerializeField]
    [InspectorName("Visibility mode")]
    [Tooltip("Hide On Listed Screens работает как blacklist. Show Only On Listed Screens работает как whitelist.")]
    private VisibilityMode _mode = VisibilityMode.HideOnListedScreens;

    [SerializeField]
    [InspectorName("Screen ids")]
    [Tooltip("Screen Id, для которых применяется правило видимости navigation.")]
    private string[] _screenIds =
    {
        "Settings",
        "Story",
        "History"
    };

    [Header("Fade")]
    [SerializeField]
    [InspectorName("Animate")]
    [Tooltip("Плавно менять alpha navigation при смене экрана.")]
    private bool _animate = true;

    [SerializeField, Min(0f)]
    [InspectorName("Fade duration")]
    [Tooltip("Длительность fade при показе или скрытии navigation.")]
    private float _fadeDuration = 0.2f;

    [SerializeField]
    [InspectorName("Ease")]
    [Tooltip("Кривая fade navigation.")]
    private Ease _fadeEase = Ease.OutQuad;

    [SerializeField]
    [InspectorName("Unscaled time")]
    [Tooltip("Использовать unscaled time, чтобы navigation реагировала даже во время паузы.")]
    private bool _useUnscaledTime = true;

    [SerializeField]
    [InspectorName("Hide immediately")]
    [Tooltip("Hide navigation instantly on excluded screens; keep fade only for showing.")]
    private bool _hideImmediately = true;

    [Header("Input")]
    [SerializeField]
    [InspectorName("Disable interaction when hidden")]
    [Tooltip("Когда navigation скрыта, CanvasGroup перестает принимать клики и raycast.")]
    private bool _disableInteractionWhenHidden = true;

    private CanvasGroup _canvasGroup;
    private Tween _fadeTween;
    private bool _isVisible = true;

    public VisibilityMode Mode => _mode;
    public IReadOnlyList<string> ScreenIds => _screenIds;
    public bool IsVisible => _isVisible;

    private void Awake()
    {
        EnsureCanvasGroup();
    }

    private void OnEnable()
    {
        EnsureCanvasGroup();
        if (Application.isPlaying)
            UIScreenState.CurrentScreenChanged += ApplyCurrentScreen;

        ApplyCurrentScreen(UIScreenState.CurrentScreenId, force: true);
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            UIScreenState.CurrentScreenChanged -= ApplyCurrentScreen;

        KillFade();
    }

    private void OnValidate()
    {
        NormalizeScreenIds();
        _fadeDuration = Mathf.Max(0f, _fadeDuration);
        EnsureCanvasGroup();
        ApplyCurrentScreen(UIScreenState.CurrentScreenId, force: true);
    }

    public void Refresh()
    {
        ApplyCurrentScreen(UIScreenState.CurrentScreenId, force: true);
    }

    private void ApplyCurrentScreen(string currentScreenId)
    {
        ApplyCurrentScreen(currentScreenId, force: false);
    }

    private void ApplyCurrentScreen(string currentScreenId, bool force)
    {
        bool visible = ShouldBeVisible(currentScreenId);
        if (!force && _isVisible == visible)
            return;

        _isVisible = visible;
        SetVisible(visible, force);
    }

    private bool ShouldBeVisible(string currentScreenId)
    {
        bool contains = ContainsScreen(currentScreenId);
        return _mode == VisibilityMode.HideOnListedScreens
            ? !contains
            : contains;
    }

    private bool ContainsScreen(string currentScreenId)
    {
        currentScreenId = UIScreenState.NormalizeScreenId(currentScreenId);
        if (currentScreenId.Length == 0 || _screenIds == null)
            return false;

        for (int i = 0; i < _screenIds.Length; i++)
        {
            if (UIScreenState.NormalizeScreenId(_screenIds[i]) == currentScreenId)
                return true;
        }

        return false;
    }

    private void SetVisible(bool visible, bool force)
    {
        CanvasGroup group = EnsureCanvasGroup();
        if (group == null)
            return;

        KillFade();

        if (_disableInteractionWhenHidden)
        {
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        float targetAlpha = visible ? 1f : 0f;
        bool canAnimate = !force &&
                          (visible || !_hideImmediately) &&
                          _animate &&
                          _fadeDuration > 0f &&
                          Application.isPlaying &&
                          gameObject.activeInHierarchy;
        if (canAnimate)
        {
            _fadeTween = group
                .DOFade(targetAlpha, _fadeDuration)
                .SetEase(_fadeEase)
                .SetUpdate(_useUnscaledTime);
        }
        else
        {
            group.alpha = targetAlpha;
        }

    }

    private CanvasGroup EnsureCanvasGroup()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        return _canvasGroup;
    }

    private void NormalizeScreenIds()
    {
        if (_screenIds == null)
        {
            _screenIds = Array.Empty<string>();
            return;
        }

        for (int i = 0; i < _screenIds.Length; i++)
            _screenIds[i] = UIScreenState.NormalizeScreenId(_screenIds[i]);
    }

    private void KillFade()
    {
        if (_fadeTween == null)
            return;

        _fadeTween.Kill();
        _fadeTween = null;
    }
}
