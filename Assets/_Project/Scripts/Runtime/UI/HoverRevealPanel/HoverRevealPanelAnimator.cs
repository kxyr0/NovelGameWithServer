using System;
using DG.Tweening;
using UnityEngine;

public sealed class HoverRevealPanelAnimator
{
    private RectTransform _panelRoot;
    private HoverRevealPanelAnimationSettings _settings;
    private Vector2 _startPosition;
    private Tween _activeTween;
    private bool _hasStartPosition;

    public bool HasPanel => _panelRoot != null;
    public bool IsAnimating => _activeTween != null && _activeTween.IsActive();

    public void Bind(RectTransform panelRoot, HoverRevealPanelAnimationSettings settings)
    {
        if (_panelRoot != panelRoot)
            _hasStartPosition = false;
        _panelRoot = panelRoot;
        _settings = settings;
    }

    public void CaptureStartPositionFromCurrent()
    {
        if (_panelRoot == null)
            return;
        _startPosition = _panelRoot.anchoredPosition;
        _hasStartPosition = true;
    }

    public void SetImmediate(bool open)
    {
        EnsureStartPosition();
        Kill(false);
        if (_panelRoot != null)
            _panelRoot.anchoredPosition = open ? Settings.OpenedPosition : _startPosition;
    }

    public void Play(bool open, Action onComplete)
    {
        EnsureStartPosition();
        Kill(false);
        if (_panelRoot == null)
        {
            onComplete?.Invoke();
            return;
        }
        float duration = open ? Settings.ShowDuration : Settings.HideDuration;
        if (duration <= 0f)
        {
            SetImmediate(open);
            onComplete?.Invoke();
            return;
        }
        Vector2 targetPosition = open ? Settings.OpenedPosition : _startPosition;
        Ease ease = open ? Settings.ShowEase : Settings.HideEase;
        _activeTween = _panelRoot
            .DOAnchorPos(targetPosition, duration)
            .SetEase(ease)
            .SetUpdate(Settings.UseUnscaledTime)
            .OnComplete(() => onComplete?.Invoke())
            .OnKill(() => _activeTween = null);
    }

    public void Kill(bool complete)
    {
        if (_activeTween == null)
            return;
        _activeTween.Kill(complete);
        _activeTween = null;
    }

    private void EnsureStartPosition()
    {
        if (!_hasStartPosition)
            CaptureStartPositionFromCurrent();
    }

    private HoverRevealPanelAnimationSettings Settings
    {
        get
        {
            if (_settings == null)
                _settings = new HoverRevealPanelAnimationSettings();
            return _settings;
        }
    }
}
