using System;
using DG.Tweening;
using UnityEngine;

[Serializable]
public sealed class HoverRevealPanelAnimationSettings
{
    [SerializeField, InspectorName("Конечная позиция"), Tooltip("Конечная anchoredPosition панели в открытом состоянии. Стартовая позиция берется из текущей позиции панели в OnEnable.")] private Vector2 _openedPosition;
    [SerializeField, Min(0f), Tooltip("Длительность открытия панели сверху вниз.")] private float _showDuration = 0.22f;
    [SerializeField, Min(0f), Tooltip("Длительность закрытия панели обратно в стартовую позицию.")] private float _hideDuration = 0.16f;
    [SerializeField, Tooltip("Ease для открытия панели.")] private Ease _showEase = Ease.OutCubic;
    [SerializeField, Tooltip("Ease для закрытия панели.")] private Ease _hideEase = Ease.InCubic;
    [SerializeField, Tooltip("Использовать unscaled time, чтобы UI-анимация работала при Time.timeScale = 0.")] private bool _useUnscaledTime = true;

    public Vector2 OpenedPosition => _openedPosition;
    public float ShowDuration => Mathf.Max(0f, _showDuration);
    public float HideDuration => Mathf.Max(0f, _hideDuration);
    public Ease ShowEase => ResolveEase(_showEase, Ease.OutCubic);
    public Ease HideEase => ResolveEase(_hideEase, Ease.InCubic);
    public bool UseUnscaledTime => _useUnscaledTime;

    public void Validate()
    {
        _showDuration = Mathf.Max(0f, _showDuration);
        _hideDuration = Mathf.Max(0f, _hideDuration);
        _showEase = ResolveEase(_showEase, Ease.OutCubic);
        _hideEase = ResolveEase(_hideEase, Ease.InCubic);
    }

    private static Ease ResolveEase(Ease ease, Ease fallback)
    {
        return ease == Ease.Unset ? fallback : ease;
    }
}
