using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CanvasGroup))]
public class UIScreenVisibilityRule : MonoBehaviour
{
    public enum VisibilityMode
    {
        HideOnListedScreens,
        ShowOnlyOnListedScreens
    }

    [Header("Rule")]
    [SerializeField]
    [FormerlySerializedAs("mode")]
    private VisibilityMode _mode = VisibilityMode.HideOnListedScreens;

    [SerializeField]
    [FormerlySerializedAs("screenIds")]
    private string[] _screenIds = Array.Empty<string>();

    [Header("Fade")]
    [SerializeField]
    [FormerlySerializedAs("animate")]
    private bool _animate = true;

    [SerializeField]
    [FormerlySerializedAs("fadeDuration")]
    private float _fadeDuration = 0.2f;

    [Header("Input")]
    [SerializeField]
    [FormerlySerializedAs("disableInteractionWhenHidden")]
    private bool _disableInteractionWhenHidden = true;

    private CanvasGroup _canvasGroup;
    private Tween _fadeTween;

    public VisibilityMode Mode => _mode;
    public IReadOnlyList<string> ScreenIds => _screenIds;
    public bool Animate => _animate;
    public float FadeDuration => _fadeDuration;
    public bool DisableInteractionWhenHidden => _disableInteractionWhenHidden;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnValidate()
    {
        if (_screenIds == null)
            _screenIds = Array.Empty<string>();

        for (int i = 0; i < _screenIds.Length; i++)
            _screenIds[i] = UIScreenState.NormalizeScreenId(_screenIds[i]);

        _fadeDuration = Mathf.Max(0f, _fadeDuration);
    }

    private void OnEnable()
    {
        UIScreenState.CurrentScreenChanged += ApplyCurrentScreen;
        ApplyCurrentScreen(UIScreenState.CurrentScreenId);
    }

    private void OnDisable()
    {
        UIScreenState.CurrentScreenChanged -= ApplyCurrentScreen;
        _fadeTween?.Kill();
        _fadeTween = null;
    }

    public void ApplyCurrentScreen(string currentScreenId)
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        if (_canvasGroup == null)
            return;

        bool visible = ShouldBeVisible(currentScreenId);
        SetVisible(visible);
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

        foreach (string screenId in _screenIds)
        {
            if (UIScreenState.NormalizeScreenId(screenId) == currentScreenId)
                return true;
        }

        return false;
    }

    private void SetVisible(bool visible)
    {
        if (_canvasGroup == null)
            return;

        _fadeTween?.Kill();
        _fadeTween = null;

        float targetAlpha = visible ? 1f : 0f;
        if (_animate && _fadeDuration > 0f && gameObject.activeInHierarchy)
            _fadeTween = _canvasGroup.DOFade(targetAlpha, _fadeDuration);
        else
            _canvasGroup.alpha = targetAlpha;

        if (!_disableInteractionWhenHidden)
            return;

        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
    }
}
