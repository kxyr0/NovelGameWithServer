using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum UIScreenNavigationButtonAction
{
    None = 0,
    OpenScreen = 1,
    CloseScreen = 2
}

public enum UIScreenNavigationButtonVisualMode
{
    None = 0,
    Hover = 1,
    Press = 2,
    CurrentScreen = 3,
    HoverOrCurrentScreen = 4
}

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/UIScreenNavigationButton")]
public sealed class UIScreenNavigationButton : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Навигация")]
    [SerializeField]
    [InspectorName("Button")]
    [Tooltip("Button, по клику которого будет открыт или закрыт экран.")]
    private Button _button;

    [SerializeField]
    [InspectorName("Screen navigator")]
    [Tooltip("StoryScreenNavigator, который управляет экранами меню.")]
    private StoryScreenNavigator _screenNavigator;

    [SerializeField]
    [InspectorName("Target screen id")]
    [Tooltip("ID экрана, например Shop, Divination или RelationshipsWithCharacters.")]
    private string _targetScreenId;

    [SerializeField]
    [InspectorName("Действие клика")]
    [Tooltip("Что сделать при клике: открыть экран, закрыть экран или ничего.")]
    private UIScreenNavigationButtonAction _clickAction = UIScreenNavigationButtonAction.OpenScreen;

    [Header("Sprite state")]
    [SerializeField]
    [InspectorName("Режим визуала")]
    [Tooltip("Когда включать active-спрайт: при наведении, нажатии, текущем экране или вместе.")]
    private UIScreenNavigationButtonVisualMode _visualMode = UIScreenNavigationButtonVisualMode.HoverOrCurrentScreen;

    [SerializeField]
    [InspectorName("Sprite fades")]
    [Tooltip("Sprite Fade компоненты, которые будут получать active/default состояние кнопки.")]
    private UISpriteStateFade[] _spriteFades = Array.Empty<UISpriteStateFade>();

    [SerializeField]
    [InspectorName("Применять при OnEnable")]
    [Tooltip("При включении объекта сразу обновить визуальное состояние.")]
    private bool _applyVisualOnEnable = true;

    [SerializeField]
    [InspectorName("Отдать fades Active State")]
    [Tooltip("Если рядом есть UIScreenNavigationActiveState, этот компонент не будет напрямую менять Sprite Fades, чтобы hover и selected не перебивали друг друга.")]
    private bool _deferSpriteFadesToActiveState = true;

    private bool _isHovered;
    private bool _isPressed;

    public event Action VisualInputChanged;

    public string TargetScreenId => UIScreenState.NormalizeScreenId(_targetScreenId);
    public bool IsHovered => _isHovered;
    public bool IsPressed => _isPressed;

    private void Awake()
    {
        EnsureButton();
    }

    private void OnEnable()
    {
        BindButton();
        UIScreenState.CurrentScreenChanged += HandleCurrentScreenChanged;
        UIScreenState.SelectedScreenChanged += HandleSelectedScreenChanged;

        if (_applyVisualOnEnable)
            RefreshVisual();
    }

    private void OnDisable()
    {
        UnbindButton();
        UIScreenState.CurrentScreenChanged -= HandleCurrentScreenChanged;
        UIScreenState.SelectedScreenChanged -= HandleSelectedScreenChanged;
        _isHovered = false;
        _isPressed = false;
        RefreshVisual();
    }

    private void OnValidate()
    {
        EnsureButton();
        _targetScreenId = UIScreenState.NormalizeScreenId(_targetScreenId);
        _spriteFades ??= Array.Empty<UISpriteStateFade>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        VisualInputChanged?.Invoke();
        RefreshVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        _isPressed = false;
        VisualInputChanged?.Invoke();
        RefreshVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        VisualInputChanged?.Invoke();
        RefreshVisual();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        VisualInputChanged?.Invoke();
        RefreshVisual();
    }

    public void OpenTargetScreen()
    {
        if (TryOpenTargetScreen(out string targetScreenId))
            UIScreenState.SetSelectedScreen(targetScreenId);
    }

    public void CloseTargetScreen()
    {
        if (TryCloseTargetScreen(out string targetScreenId) && UIScreenState.IsSelected(targetScreenId))
            UIScreenState.ClearSelectedScreen();
    }

    public void RefreshVisual()
    {
        if (ShouldDeferSpriteFadesToActiveState())
            return;

        bool active = ResolveVisualActive();
        ApplySpriteState(active);
    }

    private void HandleClick()
    {
        switch (_clickAction)
        {
            case UIScreenNavigationButtonAction.OpenScreen:
                OpenTargetScreen();
                break;
            case UIScreenNavigationButtonAction.CloseScreen:
                CloseTargetScreen();
                break;
        }

        RefreshVisual();
    }

    private void HandleCurrentScreenChanged(string screenId)
    {
        RefreshVisual();
    }

    private void HandleSelectedScreenChanged(string screenId)
    {
        RefreshVisual();
    }

    private bool ResolveVisualActive()
    {
        switch (_visualMode)
        {
            case UIScreenNavigationButtonVisualMode.Hover:
                return _isHovered;
            case UIScreenNavigationButtonVisualMode.Press:
                return _isPressed;
            case UIScreenNavigationButtonVisualMode.CurrentScreen:
                return IsTargetScreenSelectedOrCurrent();
            case UIScreenNavigationButtonVisualMode.HoverOrCurrentScreen:
                return _isHovered || IsTargetScreenSelectedOrCurrent();
            default:
                return false;
        }
    }

    private bool IsTargetScreenSelectedOrCurrent()
    {
        string targetScreenId = TargetScreenId;
        if (targetScreenId.Length == 0)
            return false;

        string selectedScreenId = UIScreenState.SelectedScreenId;
        if (selectedScreenId.Length > 0)
            return selectedScreenId == targetScreenId;

        return UIScreenState.IsCurrent(targetScreenId);
    }

    private bool TryOpenTargetScreen(out string targetScreenId)
    {
        targetScreenId = TargetScreenId;
        if (!TryValidateNavigationTarget(targetScreenId))
            return false;

        return _screenNavigator.OpenScreen(targetScreenId);
    }

    private bool TryCloseTargetScreen(out string targetScreenId)
    {
        targetScreenId = TargetScreenId;
        if (!TryValidateNavigationTarget(targetScreenId))
            return false;

        return _screenNavigator.CloseScreen(targetScreenId);
    }

    private bool TryValidateNavigationTarget(string targetScreenId)
    {
        if (_screenNavigator == null)
        {
            Debug.LogWarning("[UIScreenNavigationButton] Screen navigator is not assigned.", this);
            return false;
        }

        if (targetScreenId.Length == 0)
        {
            Debug.LogWarning("[UIScreenNavigationButton] Target screen id is empty.", this);
            return false;
        }

        return true;
    }

    private bool ShouldDeferSpriteFadesToActiveState()
    {
        return _deferSpriteFadesToActiveState &&
               TryGetComponent(out UIScreenNavigationActiveState activeState) &&
               activeState != null &&
               activeState.isActiveAndEnabled;
    }

    private void ApplySpriteState(bool active)
    {
        if (_spriteFades == null)
            return;

        for (int i = 0; i < _spriteFades.Length; i++)
        {
            UISpriteStateFade spriteFade = _spriteFades[i];
            if (spriteFade != null)
                spriteFade.SetActiveState(active);
        }
    }

    private void BindButton()
    {
        Button button = EnsureButton();
        if (button == null)
            return;

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    private void UnbindButton()
    {
        if (_button != null)
            _button.onClick.RemoveListener(HandleClick);
    }

    private Button EnsureButton()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        return _button;
    }
}
