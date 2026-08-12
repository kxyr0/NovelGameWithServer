using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(TransparentImageRaycastTarget))]
[RequireComponent(typeof(HoverRevealPanelPointerInput))]
[AddComponentMenu("Novel Template/UI/Hover Reveal/Hover Reveal Panel Controller")]
public sealed class HoverRevealPanelController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField, Tooltip("Родительский RectTransform кнопок. Этот объект двигается целиком.")] private RectTransform _panelRoot;
    [SerializeField, Tooltip("Открыть панель сразу после включения компонента.")] private bool _startOpen;
    [Header("Input")]
    [SerializeField, Tooltip("Auto: на мобильных устройствах используется клик, на desktop используется hover.")] private HoverRevealPanelInputMode _inputMode = HoverRevealPanelInputMode.Auto;
    [SerializeField, Tooltip("На мобильном повторный тап по прозрачному Image закрывает открытую панель.")] private bool _mobileClickToggles = true;
    [SerializeField, Tooltip("На desktop закрывать панель, когда курсор вышел с прозрачного Image и панели.")] private bool _closeOnDesktopPointerExit = true;
    [SerializeField, Min(0f), Tooltip("Задержка перед закрытием на desktop, чтобы курсор успел перейти с Image на кнопки.")] private float _desktopCloseDelay = 0.18f;
    [Header("Animation")]
    [SerializeField, Tooltip("Конечная позиция и параметры DOTween-анимации.")] private HoverRevealPanelAnimationSettings _animation = new HoverRevealPanelAnimationSettings();
    [Header("Events")]
    [SerializeField, Tooltip("Вызывается после завершения анимации открытия.")] private UnityEvent _opened = new UnityEvent();
    [SerializeField, Tooltip("Вызывается после завершения анимации закрытия.")] private UnityEvent _closed = new UnityEvent();
    private readonly HoverRevealPanelAnimator _animator = new HoverRevealPanelAnimator();
    private readonly HoverRevealPanelInteractivity _interactivity = new HoverRevealPanelInteractivity();
    private bool _isOpen;
    private bool _triggerHovered;
    private bool _panelHovered;
    private bool _hasInitialized;
    private Coroutine _closeRoutine;
    public bool IsOpen => _isOpen;
    public RectTransform PanelRoot => _panelRoot;
    public UnityEvent Opened => _opened;
    public UnityEvent Closed => _closed;
    private void Awake() => ConfigureSupportComponents();

    private void OnEnable()
    {
        ConfigureSupportComponents();
        BindPanel();
        _animator.CaptureStartPositionFromCurrent();
        bool open = _hasInitialized ? _isOpen : _startOpen;
        _hasInitialized = true;
        SetImmediate(open);
    }

    private void OnDisable()
    {
        CancelCloseRoutine();
        _animator.Kill(false);
    }
    private void OnDestroy() => _animator.Kill(false);
    #if UNITY_EDITOR
    private void OnValidate()
    {
        _desktopCloseDelay = Mathf.Max(0f, _desktopCloseDelay);
        if (_animation == null)
            _animation = new HoverRevealPanelAnimationSettings();
        _animation.Validate();
    }
    #endif

    public void Open() => SetOpen(true, true);
    public void Close() => SetOpen(false, true);
    public void Toggle() => SetOpen(!_isOpen, true);
    public void SetImmediate(bool open)
    {
        _isOpen = open;
        _animator.SetImmediate(open);
        ApplyInteractivity(open);
    }

    public void SetPanelRoot(RectTransform panelRoot)
    {
        _panelRoot = panelRoot;
        BindPanel();
        _animator.CaptureStartPositionFromCurrent();
        SetImmediate(_isOpen);
    }

    public void HandlePointerEnter(HoverRevealPanelInputRole role)
    {
        SetHover(role, true);
        if (_inputMode.UsesDesktopHover())
            Open();
    }

    public void HandlePointerExit(HoverRevealPanelInputRole role)
    {
        SetHover(role, false);
        if (_inputMode.UsesDesktopHover())
            ScheduleDesktopClose();
    }

    public void HandlePrimaryClick(HoverRevealPanelInputRole role)
    {
        if (role != HoverRevealPanelInputRole.Trigger || !_inputMode.UsesMobileClick())
            return;
        if (_mobileClickToggles && _isOpen)
            Close();
        else
            Open();
    }

    private void SetOpen(bool open, bool animated)
    {
        CancelCloseRoutine();
        if (_panelRoot == null)
            return;
        _isOpen = open;
        if (open)
            _interactivity.ApplyOpening();
        else
            _interactivity.ApplyClosing();
        if (!animated)
        {
            SetImmediate(open);
            return;
        }
        _animator.Play(open, () => CompleteOpenChange(open));
    }

    private void CompleteOpenChange(bool open)
    {
        ApplyInteractivity(open);
        if (open)
            _opened.Invoke();
        else
            _closed.Invoke();
    }

    private void ApplyInteractivity(bool open)
    {
        if (open)
            _interactivity.ApplyOpened();
        else
            _interactivity.ApplyClosed();
    }

    private void ConfigureSupportComponents()
    {
        GetComponent<TransparentImageRaycastTarget>().Apply();
        GetComponent<HoverRevealPanelPointerInput>().Configure(this, HoverRevealPanelInputRole.Trigger);
    }

    private void BindPanel()
    {
        if (_panelRoot != null && !_panelRoot.gameObject.activeSelf)
            _panelRoot.gameObject.SetActive(true);
        _animator.Bind(_panelRoot, _animation);
        _interactivity.Bind(_panelRoot);
        ConfigurePanelHoverInput();
    }

    private void ConfigurePanelHoverInput()
    {
        if (_panelRoot == null || _panelRoot.gameObject == gameObject)
            return;
        HoverRevealPanelPointerInput input = _panelRoot.GetComponent<HoverRevealPanelPointerInput>();
        if (input == null)
            input = _panelRoot.gameObject.AddComponent<HoverRevealPanelPointerInput>();
        input.Configure(this, HoverRevealPanelInputRole.Panel);
    }

    private void SetHover(HoverRevealPanelInputRole role, bool hovered)
    {
        if (role == HoverRevealPanelInputRole.Trigger)
            _triggerHovered = hovered;
        else
            _panelHovered = hovered;
    }

    private void ScheduleDesktopClose()
    {
        if (!_closeOnDesktopPointerExit || _triggerHovered || _panelHovered)
            return;
        CancelCloseRoutine();
        _closeRoutine = StartCoroutine(CloseAfterDelay());
    }

    private IEnumerator CloseAfterDelay()
    {
        if (_desktopCloseDelay > 0f)
            yield return new WaitForSecondsRealtime(_desktopCloseDelay);
        _closeRoutine = null;
        if (!_triggerHovered && !_panelHovered)
            Close();
    }

    private void CancelCloseRoutine()
    {
        if (_closeRoutine == null)
            return;
        StopCoroutine(_closeRoutine);
        _closeRoutine = null;
    }
}
