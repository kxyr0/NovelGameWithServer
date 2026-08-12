using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class StoryCardCarouselSwipeInput : MonoBehaviour,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Action _swipedLeft;
    private Action _swipedRight;
    private Vector2 _pressPosition;
    private int _pointerId = int.MinValue;
    private float _minimumDistance = 80f;
    private float _horizontalDominance = 1.15f;
    private bool _allowMouse = true;

    public void Configure(
        Action swipedLeft,
        Action swipedRight,
        float minimumDistance,
        float horizontalDominance,
        bool allowMouse)
    {
        _swipedLeft = swipedLeft;
        _swipedRight = swipedRight;
        _minimumDistance = Mathf.Max(1f, minimumDistance);
        _horizontalDominance = Mathf.Max(1f, horizontalDominance);
        _allowMouse = allowMouse;
        enabled = true;
    }

    public void ClearCallbacks()
    {
        _swipedLeft = null;
        _swipedRight = null;
        ResetGesture();
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanTrack(eventData) || _pointerId != int.MinValue)
            return;

        _pointerId = eventData.pointerId;
        _pressPosition = eventData.pressPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Presence of this handler makes EventSystem cancel Button clicks after a drag.
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData == null || eventData.pointerId != _pointerId)
            return;

        Vector2 delta = eventData.position - _pressPosition;
        ResetGesture();

        float horizontal = Mathf.Abs(delta.x);
        if (horizontal < _minimumDistance || horizontal < Mathf.Abs(delta.y) * _horizontalDominance)
            return;

        if (delta.x < 0f)
            _swipedLeft?.Invoke();
        else
            _swipedRight?.Invoke();
    }

    private bool CanTrack(PointerEventData eventData)
    {
        if (eventData == null)
            return false;

        bool isMouse = eventData.pointerId < 0;
        return _allowMouse || !isMouse;
    }

    private void OnDisable()
    {
        ResetGesture();
    }

    private void ResetGesture()
    {
        _pointerId = int.MinValue;
        _pressPosition = Vector2.zero;
    }
}
