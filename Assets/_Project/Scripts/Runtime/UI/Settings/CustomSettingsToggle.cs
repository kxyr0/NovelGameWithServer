using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Settings/Custom Settings Toggle")]
public sealed class CustomSettingsToggle : MonoBehaviour, IPointerClickHandler
{
    [Header("Setting")]
    [SerializeField] AppSettingType _setting = AppSettingType.SoundEffects;
    [SerializeField] bool _interactable = true;

    [Header("Handle")]
    [SerializeField] RectTransform _handle;
    [FormerlySerializedAs("_calculatePositionsFromSize")]
    [SerializeField] bool _useInitialPositionAsOn = true;
    [FormerlySerializedAs("_offPosition")]
    [SerializeField] Vector2 _offOffset = new Vector2(-100f, 0f);
    [FormerlySerializedAs("_onPosition")]
    [SerializeField] Vector2 _manualOnPosition = new Vector2(24f, 0f);
    [SerializeField] Vector2 _manualOffPosition = new Vector2(-24f, 0f);
    [SerializeField, Min(0f)] float _animationDuration = 0.2f;
    [SerializeField] Ease _ease = Ease.OutCubic;

    [Header("Optional Off Color")]
    [SerializeField] bool _changeColorWhenOff;
    [SerializeField] Graphic[] _colorTargets = Array.Empty<Graphic>();
    [SerializeField] Color _offColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Header("Events")]
    [SerializeField] UnityEvent<bool> _valueChanged = new UnityEvent<bool>();

    Color[] _onColors = Array.Empty<Color>();
    Sequence _animation;
    bool _capturedColors;
    bool _capturedInitialPosition;
    Vector2 _initialHandlePosition;

    public bool IsOn => AppSettingsState.IsEnabled(_setting);

    void Awake()
    {
        FindHandleIfMissing();
        CaptureInitialPosition();
        CaptureOnColors();
    }

    void OnEnable()
    {
        AppSettingsState.Changed -= HandleSettingChanged;
        AppSettingsState.Changed += HandleSettingChanged;
        Refresh(false);
    }

    void OnDisable()
    {
        AppSettingsState.Changed -= HandleSettingChanged;
        KillAnimation();
    }

    void OnValidate()
    {
        _animationDuration = Mathf.Max(0f, _animationDuration);
        _colorTargets ??= Array.Empty<Graphic>();
        FindHandleIfMissing();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_interactable && (eventData == null || eventData.button == PointerEventData.InputButton.Left))
            Toggle();
    }

    public void Toggle()
    {
        if (_interactable)
            SetIsOn(!IsOn);
    }

    public void SetIsOn(bool enabled)
    {
        AppSettingsState.SetEnabled(_setting, enabled);
    }

    public void SetIsOnWithoutAnimation(bool enabled)
    {
        AppSettingsState.SetEnabled(_setting, enabled);
        ApplyVisual(enabled, false);
    }

    public void Refresh()
    {
        Refresh(false);
    }

    public void Refresh(bool animated)
    {
        ApplyVisual(IsOn, animated);
    }

    void HandleSettingChanged(AppSettingType type, bool enabled)
    {
        if (type != _setting)
            return;

        ApplyVisual(enabled, true);
        _valueChanged?.Invoke(enabled);
    }

    void ApplyVisual(bool enabled, bool animated)
    {
        FindHandleIfMissing();
        CaptureInitialPosition();
        CaptureOnColors();
        KillAnimation();

        Vector2 targetPosition = GetTargetPosition(enabled);
        if (!animated || _animationDuration <= 0f || !Application.isPlaying)
        {
            if (_handle != null)
                _handle.anchoredPosition = targetPosition;
            ApplyColors(enabled);
            return;
        }

        _animation = DOTween.Sequence().SetUpdate(true);
        if (_handle != null)
            _animation.Join(_handle.DOAnchorPos(targetPosition, _animationDuration).SetEase(_ease));

        if (_changeColorWhenOff)
        {
            for (int i = 0; i < _colorTargets.Length; i++)
            {
                Graphic graphic = _colorTargets[i];
                if (graphic != null)
                    _animation.Join(graphic.DOColor(GetTargetColor(i, enabled), _animationDuration));
            }
        }

        _animation.OnComplete(() => _animation = null);
    }

    Vector2 GetTargetPosition(bool enabled)
    {
        if (_useInitialPositionAsOn)
            return enabled ? _initialHandlePosition : _initialHandlePosition + _offOffset;
        return enabled ? _manualOnPosition : _manualOffPosition;
    }

    void CaptureInitialPosition()
    {
        if (_capturedInitialPosition || _handle == null)
            return;

        _initialHandlePosition = _handle.anchoredPosition;
        _capturedInitialPosition = true;
    }

    void CaptureOnColors()
    {
        if (_capturedColors)
            return;

        _onColors = new Color[_colorTargets.Length];
        for (int i = 0; i < _colorTargets.Length; i++)
            _onColors[i] = _colorTargets[i] != null ? _colorTargets[i].color : Color.white;
        _capturedColors = true;
    }

    void ApplyColors(bool enabled)
    {
        if (!_changeColorWhenOff)
            return;

        for (int i = 0; i < _colorTargets.Length; i++)
            if (_colorTargets[i] != null)
                _colorTargets[i].color = GetTargetColor(i, enabled);
    }

    Color GetTargetColor(int index, bool enabled)
    {
        return enabled && index < _onColors.Length ? _onColors[index] : _offColor;
    }

    void FindHandleIfMissing()
    {
        if (_handle == null)
            _handle = transform.Find("Handle") as RectTransform;
    }

    void KillAnimation()
    {
        _animation?.Kill(false);
        _animation = null;
    }
}
