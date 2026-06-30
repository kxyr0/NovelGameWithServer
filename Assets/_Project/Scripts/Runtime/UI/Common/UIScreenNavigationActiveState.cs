using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/UIScreenNavigationActiveState")]
public sealed class UIScreenNavigationActiveState : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Настройка экрана")]
    [SerializeField]
    [InspectorName("Активные ID экранов")]
    [Tooltip("ID экранов, при которых эта nav-кнопка должна быть в выбранном состоянии. Например: Shop, Divination, Profile.")]
    private string[] _activeScreenIds = Array.Empty<string>();

    [SerializeField]
    [InspectorName("Кнопка навигации")]
    [Tooltip("UIScreenNavigationButton на этом же объекте. Если список активных ID экранов пустой, можно взять целевой ID экрана из неё.")]
    private UIScreenNavigationButton _navigationButton;

    [SerializeField]
    [InspectorName("Брать цель из кнопки")]
    [Tooltip("Если список активных ID экранов пустой, компонент будет использовать целевой ID экрана из UIScreenNavigationButton.")]
    private bool _useNavigationButtonTargetWhenEmpty = true;

    [SerializeField]
    [InspectorName("Брать ближайший UIScreenMarker")]
    [Tooltip("Если список активных ID экранов пустой, компонент попробует взять ID экрана из ближайшего UIScreenMarker в родителях.")]
    private bool _useNearestScreenMarkerWhenEmpty;

    [SerializeField]
    [InspectorName("Использовать выбранный экран")]
    [Tooltip("Включает выбранное состояние от UIScreenState.SelectedScreenId. При клике по nav-кнопке именно этот ID держит подсветку.")]
    private bool _useSelectedScreen = true;

    [Header("Визуальные цели")]
    [SerializeField]
    [InspectorName("FadeSprite цели")]
    [Tooltip("UISpriteStateFade, которые должны быть активны, когда эта nav-кнопка выбрана, наведена или нажата.")]
    private UISpriteStateFade[] _spriteFades = Array.Empty<UISpriteStateFade>();

    [SerializeField]
    [InspectorName("Автоискать FadeSprite в детях")]
    [Tooltip("Если список FadeSprite пустой, скрипт сам возьмёт все UISpriteStateFade/UISpriteFade в дочерних объектах.")]
    private bool _autoCollectSpriteFades = true;

    [SerializeField]
    [InspectorName("Отключить наведение у FadeSprite")]
    [Tooltip("Если включено, сами FadeSprite не будут переключаться от наведения и будут слушаться только этого скрипта.")]
    private bool _disableSpriteFadePointerHover = true;

    [SerializeField]
    [InspectorName("Связанные FadeSprite")]
    [Tooltip("Если включено, выбранное состояние кнопки передается в связанные Sprite Fades каждого FadeSprite.")]
    private bool _propagateLinkedFades = true;

    [SerializeField]
    [InspectorName("Удерживать активность каждый кадр")]
    [Tooltip("Если другой скрипт случайно вернёт спрайт в обычное состояние, этот компонент на следующем кадре снова поставит правильное состояние.")]
    private bool _enforceActiveStateEveryFrame = true;

    [SerializeField]
    [InspectorName("Показать когда активно")]
    [Tooltip("Объекты, которые включаются, когда страница активна или кнопка подсвечена.")]
    private GameObject[] _showWhenActive = Array.Empty<GameObject>();

    [SerializeField]
    [InspectorName("Скрыть когда активно")]
    [Tooltip("Объекты, которые выключаются, когда страница активна или кнопка подсвечена.")]
    private GameObject[] _hideWhenActive = Array.Empty<GameObject>();

    [SerializeField]
    [InspectorName("CanvasGroup подсветки")]
    [Tooltip("CanvasGroup декоративной подсветки самой кнопки. Скрипт ставит alpha/interactable/blocksRaycasts по активному состоянию.")]
    private CanvasGroup[] _activeCanvasGroups = Array.Empty<CanvasGroup>();

    [Header("Наведение и нажатие")]
    [SerializeField]
    [InspectorName("Активно при наведении")]
    [Tooltip("Подсвечивать кнопку, пока курсор или палец находится над nav-кнопкой.")]
    private bool _activeOnHover = true;

    [SerializeField]
    [InspectorName("Активно при нажатии")]
    [Tooltip("Подсвечивать кнопку во время нажатия.")]
    private bool _activeOnPress = true;

    [SerializeField]
    [InspectorName("Брать наведение из кнопки")]
    [Tooltip("Использовать состояние наведения и нажатия из UIScreenNavigationButton. Это надежнее, когда raycast попадает в Image/Button, а не прямо в этот компонент.")]
    private bool _useNavigationButtonPointerState = true;

    [Header("Кнопка")]
    [SerializeField]
    [InspectorName("Unity Button")]
    [Tooltip("Unity Button этой nav-кнопки. Нужен только если нужно менять interactable по active-состоянию.")]
    private Button _button;

    [SerializeField]
    [InspectorName("Менять interactable")]
    [Tooltip("Если включено, компонент будет менять Button.interactable в зависимости от выбранного или текущего экрана.")]
    private bool _setButtonInteractable;

    [SerializeField]
    [InspectorName("Interactable когда активно")]
    [Tooltip("Какой Button.interactable поставить, когда эта nav-кнопка активна.")]
    private bool _interactableWhenScreenActive = true;

    [SerializeField]
    [InspectorName("Interactable когда неактивно")]
    [Tooltip("Какой Button.interactable поставить, когда эта nav-кнопка неактивна.")]
    private bool _interactableWhenScreenInactive = true;

    [Header("События")]
    [SerializeField]
    [InspectorName("Состояние изменилось")]
    [Tooltip("Вызывается, когда итоговое визуальное состояние меняется. True означает активное состояние.")]
    private UnityEvent<bool> _stateChanged = new UnityEvent<bool>();

    private bool _isHovered;
    private bool _isPressed;
    private bool _isScreenActive;
    private bool _isVisualActive;
    private UIScreenNavigationButton _subscribedNavigationButton;

    public bool IsScreenActive => _isScreenActive;
    public bool IsVisualActive => _isVisualActive;
    public UnityEvent<bool> StateChanged => _stateChanged;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();
        if (Application.isPlaying)
        {
            UIScreenState.CurrentScreenChanged += HandleCurrentScreenChanged;
            UIScreenState.SelectedScreenChanged += HandleSelectedScreenChanged;
            SubscribeNavigationButton();
        }

        ApplyCurrentScreen(UIScreenState.CurrentScreenId, force: true);
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            UIScreenState.CurrentScreenChanged -= HandleCurrentScreenChanged;
            UIScreenState.SelectedScreenChanged -= HandleSelectedScreenChanged;
            UnsubscribeNavigationButton();
        }

        _isHovered = false;
        _isPressed = false;
        _isScreenActive = false;
        ApplyVisualState(force: true);
    }

    private void Update()
    {
        if (!Application.isPlaying)
            ApplyCurrentScreen(UIScreenState.CurrentScreenId, force: false);
    }

    private void LateUpdate()
    {
        if (Application.isPlaying && _enforceActiveStateEveryFrame)
            ApplyCurrentScreen(UIScreenState.CurrentScreenId, force: false);

        if (_enforceActiveStateEveryFrame && (_isScreenActive || IsPointerVisualActive()))
            ApplyVisualState(force: true);
    }

    private void OnValidate()
    {
        NormalizeScreenIds();
        EnsureReferences();
        ApplyCurrentScreen(UIScreenState.CurrentScreenId, force: true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        ApplyVisualState(force: false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        _isPressed = false;
        ApplyVisualState(force: false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        ApplyVisualState(force: false);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        ApplyVisualState(force: false);
    }

    public void SetActiveScreenIds(params string[] screenIds)
    {
        _activeScreenIds = screenIds ?? Array.Empty<string>();
        NormalizeScreenIds();
        ApplyCurrentScreen(UIScreenState.CurrentScreenId, force: true);
    }

    public void Refresh()
    {
        ApplyCurrentScreen(UIScreenState.CurrentScreenId, force: true);
    }

    private void HandleCurrentScreenChanged(string currentScreenId)
    {
        if (_useSelectedScreen && MatchesCurrentScreen(currentScreenId) && !UIScreenState.IsSelected(currentScreenId))
            UIScreenState.SetSelectedScreen(currentScreenId);

        ApplyCurrentScreen(currentScreenId, force: false);
    }

    private void HandleSelectedScreenChanged(string selectedScreenId)
    {
        ApplyCurrentScreen(UIScreenState.CurrentScreenId, force: false);
    }

    private void HandleNavigationButtonVisualInputChanged()
    {
        ApplyVisualState(force: false);
    }

    private void ApplyCurrentScreen(string currentScreenId, bool force)
    {
        bool active = MatchesSelectedScreen() || MatchesCurrentScreen(currentScreenId);
        if (!force && _isScreenActive == active)
        {
            ApplyVisualState(force: false);
            return;
        }

        _isScreenActive = active;
        ApplyButtonInteractable();

        AppLogger.DebugLog(
            AppLogCategory.ScreenNavigation,
            nameof(UIScreenNavigationActiveState),
            nameof(ApplyCurrentScreen),
            "[SCREEN][NAV_ACTIVE] Navigation active state refreshed.",
            LogMetadata.Of(
                "object", name,
                "currentScreenId", UIScreenState.NormalizeScreenId(currentScreenId),
                "selectedScreenId", UIScreenState.SelectedScreenId,
                "isScreenActive", _isScreenActive,
                "matchedScreenIds", string.Join(",", ResolveScreenIds()),
                "hasSelectedMatch", MatchesSelectedScreen(),
                "hasCurrentMatch", MatchesCurrentScreen(currentScreenId)));

        ApplyVisualState(force: true);
    }

    private bool MatchesCurrentScreen(string currentScreenId)
    {
        return MatchesScreenId(currentScreenId);
    }

    private bool MatchesSelectedScreen()
    {
        return _useSelectedScreen && MatchesScreenId(UIScreenState.SelectedScreenId);
    }

    private bool MatchesScreenId(string screenId)
    {
        screenId = UIScreenState.NormalizeScreenId(screenId);
        if (screenId.Length == 0)
            return false;

        string[] screenIds = ResolveScreenIds();
        for (int i = 0; i < screenIds.Length; i++)
        {
            if (UIScreenState.NormalizeScreenId(screenIds[i]) == screenId)
                return true;
        }

        return false;
    }

    private string[] ResolveScreenIds()
    {
        if (_activeScreenIds != null && _activeScreenIds.Length > 0)
            return _activeScreenIds;

        if (_useNavigationButtonTargetWhenEmpty)
        {
            UIScreenNavigationButton navigationButton = ResolveNavigationButton();
            if (navigationButton != null && navigationButton.TargetScreenId.Length > 0)
                return new[] { navigationButton.TargetScreenId };
        }

        if (_useNearestScreenMarkerWhenEmpty)
        {
            UIScreenMarker marker = GetComponentInParent<UIScreenMarker>();
            if (marker != null && marker.ScreenId.Length > 0)
                return new[] { marker.ScreenId };
        }

        return Array.Empty<string>();
    }

    private bool IsPointerVisualActive()
    {
        return (_activeOnPress && IsPointerPressed()) ||
               (_activeOnHover && IsPointerHovered());
    }

    private bool IsPointerHovered()
    {
        if (_isHovered)
            return true;

        UIScreenNavigationButton navigationButton = _useNavigationButtonPointerState
            ? ResolveNavigationButton()
            : null;

        return navigationButton != null && navigationButton.IsHovered;
    }

    private bool IsPointerPressed()
    {
        if (_isPressed)
            return true;

        UIScreenNavigationButton navigationButton = _useNavigationButtonPointerState
            ? ResolveNavigationButton()
            : null;

        return navigationButton != null && navigationButton.IsPressed;
    }

    private void ApplyVisualState(bool force)
    {
        bool active = _isScreenActive || IsPointerVisualActive();

        bool changed = _isVisualActive != active;
        if (!force && !changed)
            return;

        _isVisualActive = active;
        ApplySpriteFades(active);
        SetObjectsActive(_showWhenActive, active);
        SetObjectsActive(_hideWhenActive, !active);
        ApplyCanvasGroups(active);
        if (changed)
            _stateChanged?.Invoke(active);
    }

    private void ApplySpriteFades(bool active)
    {
        if (_spriteFades == null)
            return;

        for (int i = 0; i < _spriteFades.Length; i++)
        {
            UISpriteStateFade spriteFade = _spriteFades[i];
            if (spriteFade == null)
                continue;

            if (_disableSpriteFadePointerHover && Application.isPlaying)
                spriteFade.SetPointerHoverEnabled(false, false);

            spriteFade.SetActiveState(active, _propagateLinkedFades);
        }
    }

    private static void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && objects[i].activeSelf != active)
                objects[i].SetActive(active);
        }
    }

    private void ApplyCanvasGroups(bool active)
    {
        if (_activeCanvasGroups == null)
            return;

        for (int i = 0; i < _activeCanvasGroups.Length; i++)
        {
            CanvasGroup group = _activeCanvasGroups[i];
            if (group == null)
                continue;

            group.alpha = active ? 1f : 0f;
            group.interactable = active;
            group.blocksRaycasts = active;
        }
    }

    private void ApplyButtonInteractable()
    {
        if (!_setButtonInteractable)
            return;

        Button button = ResolveButton();
        if (button != null)
            button.interactable = _isScreenActive ? _interactableWhenScreenActive : _interactableWhenScreenInactive;
    }

    private void EnsureReferences()
    {
        ResolveButton();
        ResolveNavigationButton();

        if (_autoCollectSpriteFades && (_spriteFades == null || _spriteFades.Length == 0))
            _spriteFades = GetComponentsInChildren<UISpriteStateFade>(true);

        if (_spriteFades == null)
            _spriteFades = Array.Empty<UISpriteStateFade>();

        _showWhenActive ??= Array.Empty<GameObject>();
        _hideWhenActive ??= Array.Empty<GameObject>();
        _activeCanvasGroups ??= Array.Empty<CanvasGroup>();
    }

    private void SubscribeNavigationButton()
    {
        UIScreenNavigationButton navigationButton = ResolveNavigationButton();
        if (_subscribedNavigationButton == navigationButton)
            return;

        UnsubscribeNavigationButton();

        if (navigationButton == null)
            return;

        _subscribedNavigationButton = navigationButton;
        _subscribedNavigationButton.VisualInputChanged += HandleNavigationButtonVisualInputChanged;
    }

    private void UnsubscribeNavigationButton()
    {
        if (_subscribedNavigationButton == null)
            return;

        _subscribedNavigationButton.VisualInputChanged -= HandleNavigationButtonVisualInputChanged;
        _subscribedNavigationButton = null;
    }

    private Button ResolveButton()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        return _button;
    }

    private UIScreenNavigationButton ResolveNavigationButton()
    {
        if (_navigationButton == null)
            _navigationButton = GetComponent<UIScreenNavigationButton>();

        return _navigationButton;
    }

    private void NormalizeScreenIds()
    {
        if (_activeScreenIds == null)
        {
            _activeScreenIds = Array.Empty<string>();
            return;
        }

        for (int i = 0; i < _activeScreenIds.Length; i++)
            _activeScreenIds[i] = UIScreenState.NormalizeScreenId(_activeScreenIds[i]);
    }
}
