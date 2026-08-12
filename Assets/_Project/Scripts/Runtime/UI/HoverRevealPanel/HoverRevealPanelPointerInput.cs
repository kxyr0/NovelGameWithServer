using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[AddComponentMenu("Novel Template/UI/Hover Reveal/Panel Pointer Input")]
public sealed class HoverRevealPanelPointerInput :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    [SerializeField, Tooltip("Trigger — прозрачный Image; Panel — область кнопок, которая удерживает панель открытой при hover.")]
    private HoverRevealPanelInputRole _role = HoverRevealPanelInputRole.Trigger;

    private HoverRevealPanelController _controller;

    private void Awake()
    {
        if (_controller == null)
            _controller = GetComponent<HoverRevealPanelController>();
    }

    public void Configure(HoverRevealPanelController controller, HoverRevealPanelInputRole role)
    {
        _controller = controller;
        _role = role;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_controller != null)
            _controller.HandlePointerEnter(_role);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_controller != null)
            _controller.HandlePointerExit(_role);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        if (_controller != null)
            _controller.HandlePrimaryClick(_role);
    }
}
