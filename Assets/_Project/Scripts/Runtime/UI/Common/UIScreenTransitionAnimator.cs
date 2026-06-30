using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public enum UIScreenTransitionType
{
    None,
    Fade,
    SlideLeft,
    SlideRight,
    SlideUp,
    SlideDown,
    SlideFadeLeft,
    SlideFadeRight,
    SlideFadeUp,
    SlideFadeDown,
    ScaleFade,
    Depth,
    PushLeft,
    PushRight,
    PushUp,
    PushDown,
    CoverLeft,
    CoverRight,
    CoverUp,
    CoverDown,
    RevealLeft,
    RevealRight,
    RevealUp,
    RevealDown,
    ZoomIn,
    ZoomOut,
    Pop,
    FlipHorizontal,
    FlipVertical,
    SlideFade
}

public class UIScreenTransitionAnimator : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private UIScreenTransitionType transitionType = UIScreenTransitionType.SlideLeft;
    [SerializeField] private float duration = 0.35f;
    [SerializeField] private Ease ease = Ease.OutCubic;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Modern Motion")]
    [SerializeField, Range(0.05f, 0.75f)] private float modernTravel = 0.18f;
    [SerializeField, Range(0.9f, 1.1f)] private float incomingScale = 1.015f;
    [SerializeField, Range(0.9f, 1.05f)] private float outgoingScale = 0.985f;
    [SerializeField, Range(0f, 0.15f)] private float incomingDelay = 0.035f;
    [SerializeField, Range(0.65f, 1f)] private float revealBackgroundScale = 0.96f;
    [SerializeField, Range(0f, 16f)] private float popOvershootPercent = 4f;
    [SerializeField, Range(8f, 90f)] private float flipAngle = 38f;

    private readonly Dictionary<RectTransform, Vector2> _homePositions = new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<RectTransform, Vector3> _homeScales = new Dictionary<RectTransform, Vector3>();
    private readonly Dictionary<RectTransform, Quaternion> _homeRotations = new Dictionary<RectTransform, Quaternion>();
    private Sequence _activeSequence;

    public UIScreenTransitionType TransitionType => transitionType;
    public float Duration => duration;
    public Ease Ease => ease;
    public bool IsTransitioning => _activeSequence != null && _activeSequence.IsActive();

    private void OnValidate()
    {
        duration = Mathf.Max(0f, duration);
        modernTravel = Mathf.Clamp(modernTravel, 0.05f, 0.75f);
        incomingScale = Mathf.Clamp(incomingScale, 0.9f, 1.1f);
        outgoingScale = Mathf.Clamp(outgoingScale, 0.9f, 1.05f);
        incomingDelay = Mathf.Clamp(incomingDelay, 0f, 0.15f);
        revealBackgroundScale = Mathf.Clamp(revealBackgroundScale, 0.65f, 1f);
        popOvershootPercent = Mathf.Clamp(popOvershootPercent, 0f, 16f);
        flipAngle = Mathf.Clamp(flipAngle, 8f, 90f);
    }

    private void OnDestroy()
    {
        KillActiveTransition();
    }

    public void Configure(UIScreenTransitionType type, float transitionDuration, Ease transitionEase, bool unscaledTime)
    {
        transitionType = type;
        duration = Mathf.Max(0f, transitionDuration);
        ease = transitionEase;
        useUnscaledTime = unscaledTime;
    }

    public void CancelActiveTransition()
    {
        KillActiveTransition();
    }

    public void Play(
        GameObject fromPage,
        GameObject toPage,
        bool reverse,
        Action onComplete = null)
    {
        KillActiveTransition();

        if (fromPage == null && toPage == null)
        {
            SafeInvoke(onComplete);
            return;
        }

        if (fromPage != null && !fromPage.activeSelf)
            fromPage.SetActive(true);

        if (toPage != null && !toPage.activeSelf)
            toPage.SetActive(true);

        if (transitionType == UIScreenTransitionType.None || duration <= 0f || !isActiveAndEnabled)
        {
            ApplyImmediate(fromPage, toPage);
            SafeInvoke(onComplete);
            return;
        }

        RectTransform fromRect = GetRectTransform(fromPage);
        RectTransform toRect = GetRectTransform(toPage);
        CanvasGroup fromGroup = GetOrAddCanvasGroup(fromPage);
        CanvasGroup toGroup = GetOrAddCanvasGroup(toPage);

        CaptureHomePosition(fromRect);
        CaptureHomePosition(toRect);
        CaptureHomeScale(fromRect);
        CaptureHomeScale(toRect);
        CaptureHomeRotation(fromRect);
        CaptureHomeRotation(toRect);

        Canvas.ForceUpdateCanvases();

        SetInteraction(fromGroup, false);
        SetInteraction(toGroup, false);

        _activeSequence = DOTween.Sequence().SetUpdate(useUnscaledTime);

        if (transitionType == UIScreenTransitionType.Fade)
        {
            BuildFadeSequence(fromGroup, toGroup);
        }
        else if (IsModernTransition(transitionType))
        {
            BuildModernSequence(fromRect, toRect, fromGroup, toGroup, reverse);
        }
        else
        {
            if (fromGroup != null)
                fromGroup.alpha = 1f;
            if (toGroup != null)
                toGroup.alpha = 1f;

            BuildSlideSequence(fromRect, toRect, reverse);
        }

        _activeSequence
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                CompleteTransition(fromRect, toRect, fromGroup, toGroup);
                SafeInvoke(onComplete);
            })
            .OnKill(() => _activeSequence = null);
    }

    public void ResetPage(GameObject page, bool visible)
    {
        if (visible && page != null && !page.activeSelf)
            page.SetActive(true);

        RectTransform rect = GetRectTransform(page);
        CanvasGroup group = GetOrAddCanvasGroup(page);
        CaptureHomePosition(rect);
        CaptureHomeScale(rect);
        CaptureHomeRotation(rect);
        MoveToHome(rect);
        MoveToHomeScale(rect);
        MoveToHomeRotation(rect);

        if (group != null)
        {
            group.alpha = visible ? 1f : 0f;
            SetInteraction(group, visible);
        }
    }

    private void BuildFadeSequence(CanvasGroup fromGroup, CanvasGroup toGroup)
    {
        if (toGroup != null)
            toGroup.alpha = 0f;

        if (fromGroup != null)
        {
            fromGroup.alpha = 1f;
            _activeSequence.Join(fromGroup.DOFade(0f, duration).SetEase(ease));
        }

        if (toGroup != null)
            _activeSequence.Join(toGroup.DOFade(1f, duration).SetEase(ease));
    }

    private void BuildSlideSequence(RectTransform fromRect, RectTransform toRect, bool reverse)
    {
        Vector2 offset = GetSlideOffset(fromRect != null ? fromRect : toRect, transitionType);
        if (reverse)
            offset = -offset;

        if (fromRect != null)
        {
            Vector2 fromHome = GetHomePosition(fromRect);
            fromRect.anchoredPosition = fromHome;
            MoveToHomeScale(fromRect);
            _activeSequence.Join(fromRect.DOAnchorPos(fromHome - offset, duration).SetEase(ease));
        }

        if (toRect != null)
        {
            Vector2 toHome = GetHomePosition(toRect);
            toRect.anchoredPosition = toHome + offset;
            MoveToHomeScale(toRect);
            _activeSequence.Join(toRect.DOAnchorPos(toHome, duration).SetEase(ease));
        }
    }

    private void BuildModernSequence(
        RectTransform fromRect,
        RectTransform toRect,
        CanvasGroup fromGroup,
        CanvasGroup toGroup,
        bool reverse)
    {
        if (IsPushTransition(transitionType))
        {
            BuildPushSequence(fromRect, toRect, fromGroup, toGroup, reverse);
            return;
        }

        if (IsCoverTransition(transitionType))
        {
            BuildCoverSequence(fromRect, toRect, fromGroup, toGroup, reverse);
            return;
        }

        if (IsRevealTransition(transitionType))
        {
            BuildRevealSequence(fromRect, toRect, fromGroup, toGroup, reverse);
            return;
        }

        if (transitionType == UIScreenTransitionType.ZoomIn ||
            transitionType == UIScreenTransitionType.ZoomOut ||
            transitionType == UIScreenTransitionType.Pop)
        {
            BuildZoomSequence(fromRect, toRect, fromGroup, toGroup);
            return;
        }

        if (transitionType == UIScreenTransitionType.FlipHorizontal ||
            transitionType == UIScreenTransitionType.FlipVertical)
        {
            BuildFlipSequence(fromRect, toRect, fromGroup, toGroup, reverse);
            return;
        }

        RectTransform reference = toRect != null ? toRect : fromRect;
        Vector2 offset = GetModernOffset(reference, reverse);
        float safeDuration = Mathf.Max(0.01f, duration);
        float safeDelay = Mathf.Min(incomingDelay, safeDuration * 0.35f);
        float incomingDuration = Mathf.Max(0.01f, safeDuration - safeDelay);
        float outgoingDuration = Mathf.Max(0.01f, safeDuration * 0.82f);

        if (fromGroup != null)
            fromGroup.alpha = 1f;
        if (toGroup != null)
            toGroup.alpha = 0f;

        if (fromRect != null)
        {
            Vector2 fromHome = GetHomePosition(fromRect);
            Vector3 fromScale = GetHomeScale(fromRect);
            fromRect.anchoredPosition = fromHome;
            fromRect.localScale = fromScale;

            Vector2 fromTarget = transitionType == UIScreenTransitionType.ScaleFade
                ? fromHome
                : fromHome - offset * 0.65f;

            _activeSequence.Insert(0f, fromRect.DOAnchorPos(fromTarget, outgoingDuration).SetEase(Ease.InCubic));
            _activeSequence.Insert(0f, fromRect.DOScale(ScaleBy(fromScale, outgoingScale), outgoingDuration).SetEase(Ease.InOutSine));
        }

        if (fromGroup != null)
            _activeSequence.Insert(0f, fromGroup.DOFade(0f, outgoingDuration * 0.82f).SetEase(Ease.InQuad));

        if (toRect != null)
        {
            Vector2 toHome = GetHomePosition(toRect);
            Vector3 toScale = GetHomeScale(toRect);
            toRect.anchoredPosition = transitionType == UIScreenTransitionType.ScaleFade
                ? toHome
                : toHome + offset;
            toRect.localScale = ScaleBy(toScale, incomingScale);

            _activeSequence.Insert(safeDelay, toRect.DOAnchorPos(toHome, incomingDuration).SetEase(ease));
            _activeSequence.Insert(safeDelay, toRect.DOScale(toScale, incomingDuration).SetEase(Ease.OutCubic));
        }

        if (toGroup != null)
            _activeSequence.Insert(safeDelay, toGroup.DOFade(1f, incomingDuration * 0.9f).SetEase(Ease.OutQuad));
    }

    private void BuildPushSequence(
        RectTransform fromRect,
        RectTransform toRect,
        CanvasGroup fromGroup,
        CanvasGroup toGroup,
        bool reverse)
    {
        RectTransform reference = toRect != null ? toRect : fromRect;
        Vector2 offset = GetDirectionalOffset(reference, transitionType, reverse);
        float safeDuration = Mathf.Max(0.01f, duration);

        if (fromGroup != null)
            fromGroup.alpha = 1f;
        if (toGroup != null)
            toGroup.alpha = 0.92f;

        if (fromRect != null)
        {
            Vector2 home = GetHomePosition(fromRect);
            Vector3 scale = GetHomeScale(fromRect);
            fromRect.anchoredPosition = home;
            fromRect.localScale = scale;
            MoveToHomeRotation(fromRect);
            _activeSequence.Insert(0f, fromRect.DOAnchorPos(home - offset, safeDuration).SetEase(ease));
            _activeSequence.Insert(0f, fromRect.DOScale(ScaleBy(scale, outgoingScale), safeDuration * 0.9f).SetEase(Ease.InOutSine));
        }

        if (fromGroup != null)
            _activeSequence.Insert(0f, fromGroup.DOFade(0f, safeDuration * 0.86f).SetEase(Ease.InQuad));

        if (toRect != null)
        {
            Vector2 home = GetHomePosition(toRect);
            Vector3 scale = GetHomeScale(toRect);
            toRect.anchoredPosition = home + offset;
            toRect.localScale = ScaleBy(scale, incomingScale);
            MoveToHomeRotation(toRect);
            _activeSequence.Insert(0f, toRect.DOAnchorPos(home, safeDuration).SetEase(ease));
            _activeSequence.Insert(0f, toRect.DOScale(scale, safeDuration).SetEase(Ease.OutCubic));
        }

        if (toGroup != null)
            _activeSequence.Insert(0f, toGroup.DOFade(1f, safeDuration * 0.86f).SetEase(Ease.OutQuad));
    }

    private void BuildCoverSequence(
        RectTransform fromRect,
        RectTransform toRect,
        CanvasGroup fromGroup,
        CanvasGroup toGroup,
        bool reverse)
    {
        RectTransform reference = toRect != null ? toRect : fromRect;
        Vector2 offset = GetDirectionalOffset(reference, transitionType, reverse);
        float safeDuration = Mathf.Max(0.01f, duration);

        if (fromRect != null)
        {
            Vector2 home = GetHomePosition(fromRect);
            Vector3 scale = GetHomeScale(fromRect);
            fromRect.anchoredPosition = home;
            fromRect.localScale = scale;
            MoveToHomeRotation(fromRect);
            _activeSequence.Insert(0f, fromRect.DOScale(ScaleBy(scale, revealBackgroundScale), safeDuration).SetEase(Ease.OutCubic));
        }

        if (fromGroup != null)
        {
            fromGroup.alpha = 1f;
            _activeSequence.Insert(0f, fromGroup.DOFade(0.62f, safeDuration * 0.75f).SetEase(Ease.OutQuad));
        }

        if (toRect != null)
        {
            Vector2 home = GetHomePosition(toRect);
            toRect.anchoredPosition = home + offset;
            MoveToHomeScale(toRect);
            MoveToHomeRotation(toRect);
            _activeSequence.Insert(0f, toRect.DOAnchorPos(home, safeDuration).SetEase(ease));
        }

        if (toGroup != null)
        {
            toGroup.alpha = 0f;
            _activeSequence.Insert(0f, toGroup.DOFade(1f, safeDuration * 0.82f).SetEase(Ease.OutQuad));
        }
    }

    private void BuildRevealSequence(
        RectTransform fromRect,
        RectTransform toRect,
        CanvasGroup fromGroup,
        CanvasGroup toGroup,
        bool reverse)
    {
        RectTransform reference = fromRect != null ? fromRect : toRect;
        Vector2 offset = GetDirectionalOffset(reference, transitionType, reverse);
        float safeDuration = Mathf.Max(0.01f, duration);

        if (toRect != null)
        {
            Vector2 home = GetHomePosition(toRect);
            Vector3 scale = GetHomeScale(toRect);
            toRect.anchoredPosition = home;
            toRect.localScale = ScaleBy(scale, revealBackgroundScale);
            MoveToHomeRotation(toRect);
            _activeSequence.Insert(0f, toRect.DOScale(scale, safeDuration).SetEase(Ease.OutCubic));
        }

        if (toGroup != null)
        {
            toGroup.alpha = 0.72f;
            _activeSequence.Insert(0f, toGroup.DOFade(1f, safeDuration * 0.9f).SetEase(Ease.OutQuad));
        }

        if (fromRect != null)
        {
            Vector2 home = GetHomePosition(fromRect);
            fromRect.anchoredPosition = home;
            MoveToHomeScale(fromRect);
            MoveToHomeRotation(fromRect);
            _activeSequence.Insert(0f, fromRect.DOAnchorPos(home - offset, safeDuration).SetEase(Ease.InOutCubic));
        }

        if (fromGroup != null)
        {
            fromGroup.alpha = 1f;
            _activeSequence.Insert(0f, fromGroup.DOFade(0f, safeDuration * 0.72f).SetEase(Ease.InQuad));
        }
    }

    private void BuildZoomSequence(
        RectTransform fromRect,
        RectTransform toRect,
        CanvasGroup fromGroup,
        CanvasGroup toGroup)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float enterStartScale = transitionType == UIScreenTransitionType.ZoomOut ? 1.08f : 0.92f;
        float exitScale = transitionType == UIScreenTransitionType.ZoomOut ? 0.94f : 1.04f;
        bool pop = transitionType == UIScreenTransitionType.Pop;

        if (fromRect != null)
        {
            Vector2 home = GetHomePosition(fromRect);
            Vector3 scale = GetHomeScale(fromRect);
            fromRect.anchoredPosition = home;
            fromRect.localScale = scale;
            MoveToHomeRotation(fromRect);
            _activeSequence.Insert(0f, fromRect.DOScale(ScaleBy(scale, exitScale), safeDuration * 0.75f).SetEase(Ease.InCubic));
        }

        if (fromGroup != null)
        {
            fromGroup.alpha = 1f;
            _activeSequence.Insert(0f, fromGroup.DOFade(0f, safeDuration * 0.72f).SetEase(Ease.InQuad));
        }

        if (toRect != null)
        {
            Vector2 home = GetHomePosition(toRect);
            Vector3 scale = GetHomeScale(toRect);
            toRect.anchoredPosition = home;
            toRect.localScale = ScaleBy(scale, pop ? Mathf.Max(0.8f, _popupStartSafeScale()) : enterStartScale);
            MoveToHomeRotation(toRect);

            if (pop && popOvershootPercent > 0f)
            {
                float overshootScale = 1f + popOvershootPercent * 0.01f;
                _activeSequence.Insert(0f, toRect.DOScale(ScaleBy(scale, overshootScale), safeDuration * 0.68f).SetEase(Ease.OutCubic));
                _activeSequence.Insert(safeDuration * 0.68f, toRect.DOScale(scale, safeDuration * 0.32f).SetEase(Ease.OutSine));
            }
            else
            {
                _activeSequence.Insert(0f, toRect.DOScale(scale, safeDuration).SetEase(ease));
            }
        }

        if (toGroup != null)
        {
            toGroup.alpha = 0f;
            _activeSequence.Insert(0f, toGroup.DOFade(1f, safeDuration * 0.82f).SetEase(Ease.OutQuad));
        }
    }

    private void BuildFlipSequence(
        RectTransform fromRect,
        RectTransform toRect,
        CanvasGroup fromGroup,
        CanvasGroup toGroup,
        bool reverse)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float halfDuration = safeDuration * 0.5f;
        float direction = reverse ? -1f : 1f;
        Vector3 axis = transitionType == UIScreenTransitionType.FlipVertical
            ? new Vector3(flipAngle * direction, 0f, 0f)
            : new Vector3(0f, -flipAngle * direction, 0f);

        if (fromRect != null)
        {
            Vector3 homeEuler = GetHomeRotation(fromRect).eulerAngles;
            MoveToHome(fromRect);
            MoveToHomeScale(fromRect);
            MoveToHomeRotation(fromRect);
            _activeSequence.Insert(0f, fromRect.DOLocalRotate(homeEuler + axis, halfDuration, RotateMode.Fast).SetEase(Ease.InCubic));
            _activeSequence.Insert(halfDuration, fromRect.DOLocalRotate(homeEuler, halfDuration, RotateMode.Fast).SetEase(Ease.OutCubic));
        }

        if (fromGroup != null)
        {
            fromGroup.alpha = 1f;
            _activeSequence.Insert(0f, fromGroup.DOFade(0f, halfDuration).SetEase(Ease.InQuad));
        }

        if (toRect != null)
        {
            Vector3 homeEuler = GetHomeRotation(toRect).eulerAngles;
            Vector3 toAxis = -axis;
            MoveToHome(toRect);
            MoveToHomeScale(toRect);
            toRect.localRotation = GetHomeRotation(toRect) * Quaternion.Euler(toAxis);
            _activeSequence.Insert(halfDuration * 0.72f, toRect.DOLocalRotate(homeEuler, safeDuration * 0.58f, RotateMode.Fast).SetEase(Ease.OutQuart));
        }

        if (toGroup != null)
        {
            toGroup.alpha = 0f;
            _activeSequence.Insert(halfDuration * 0.72f, toGroup.DOFade(1f, safeDuration * 0.45f).SetEase(Ease.OutQuad));
        }
    }

    private void CompleteTransition(
        RectTransform fromRect,
        RectTransform toRect,
        CanvasGroup fromGroup,
        CanvasGroup toGroup)
    {
        MoveToHome(toRect);
        MoveToHome(fromRect);
        MoveToHomeScale(toRect);
        MoveToHomeScale(fromRect);
        MoveToHomeRotation(toRect);
        MoveToHomeRotation(fromRect);

        if (toGroup != null)
        {
            toGroup.alpha = 1f;
            SetInteraction(toGroup, true);
        }

        if (fromGroup != null)
        {
            fromGroup.alpha = 0f;
            SetInteraction(fromGroup, false);
        }
    }

    private void ApplyImmediate(GameObject fromPage, GameObject toPage)
    {
        if (fromPage != null)
        {
            RectTransform fromRect = GetRectTransform(fromPage);
            CanvasGroup fromGroup = GetOrAddCanvasGroup(fromPage);
            CaptureHomePosition(fromRect);
            CaptureHomeScale(fromRect);
            CaptureHomeRotation(fromRect);
            MoveToHome(fromRect);
            MoveToHomeScale(fromRect);
            MoveToHomeRotation(fromRect);

            if (fromGroup != null)
            {
                fromGroup.alpha = 0f;
                SetInteraction(fromGroup, false);
            }
        }

        if (toPage != null)
        {
            if (!toPage.activeSelf)
                toPage.SetActive(true);

            RectTransform toRect = GetRectTransform(toPage);
            CanvasGroup toGroup = GetOrAddCanvasGroup(toPage);
            CaptureHomePosition(toRect);
            CaptureHomeScale(toRect);
            CaptureHomeRotation(toRect);
            MoveToHome(toRect);
            MoveToHomeScale(toRect);
            MoveToHomeRotation(toRect);

            if (toGroup != null)
            {
                toGroup.alpha = 1f;
                SetInteraction(toGroup, true);
            }
        }
    }

    private Vector2 GetModernOffset(RectTransform reference, bool reverse)
    {
        if (transitionType == UIScreenTransitionType.ScaleFade)
            return Vector2.zero;

        if (transitionType == UIScreenTransitionType.Depth)
        {
            Vector2 depthOffset = GetSlideOffset(reference, UIScreenTransitionType.SlideLeft) * Mathf.Min(modernTravel, 0.12f);
            return reverse ? -depthOffset : depthOffset;
        }

        Vector2 offset = GetSlideOffset(reference, ResolveModernSlideType(transitionType)) * modernTravel;
        return reverse ? -offset : offset;
    }

    private UIScreenTransitionType ResolveModernSlideType(UIScreenTransitionType type)
    {
        return type == UIScreenTransitionType.SlideFade
            ? UIScreenTransitionType.SlideLeft
            : type;
    }

    private Vector2 GetDirectionalOffset(RectTransform reference, UIScreenTransitionType type, bool reverse)
    {
        Vector2 offset = GetSlideOffset(reference, ResolveDirectionalSlideType(type));
        return reverse ? -offset : offset;
    }

    private UIScreenTransitionType ResolveDirectionalSlideType(UIScreenTransitionType type)
    {
        switch (type)
        {
            case UIScreenTransitionType.PushRight:
            case UIScreenTransitionType.CoverRight:
            case UIScreenTransitionType.RevealRight:
                return UIScreenTransitionType.SlideRight;
            case UIScreenTransitionType.PushUp:
            case UIScreenTransitionType.CoverUp:
            case UIScreenTransitionType.RevealUp:
                return UIScreenTransitionType.SlideUp;
            case UIScreenTransitionType.PushDown:
            case UIScreenTransitionType.CoverDown:
            case UIScreenTransitionType.RevealDown:
                return UIScreenTransitionType.SlideDown;
            case UIScreenTransitionType.PushLeft:
            case UIScreenTransitionType.CoverLeft:
            case UIScreenTransitionType.RevealLeft:
            default:
                return UIScreenTransitionType.SlideLeft;
        }
    }

    private Vector2 GetSlideOffset(RectTransform reference, UIScreenTransitionType type)
    {
        float width = Screen.width;
        float height = Screen.height;

        RectTransform parent = reference != null ? reference.parent as RectTransform : null;
        if (parent != null)
        {
            if (parent.rect.width > 0f)
                width = parent.rect.width;
            if (parent.rect.height > 0f)
                height = parent.rect.height;
        }
        else if (reference != null)
        {
            if (reference.rect.width > 0f)
                width = reference.rect.width;
            if (reference.rect.height > 0f)
                height = reference.rect.height;
        }

        switch (type)
        {
            case UIScreenTransitionType.SlideRight:
            case UIScreenTransitionType.SlideFadeRight:
                return new Vector2(-Mathf.Max(1f, width), 0f);
            case UIScreenTransitionType.SlideUp:
            case UIScreenTransitionType.SlideFadeUp:
                return new Vector2(0f, -Mathf.Max(1f, height));
            case UIScreenTransitionType.SlideDown:
            case UIScreenTransitionType.SlideFadeDown:
                return new Vector2(0f, Mathf.Max(1f, height));
            case UIScreenTransitionType.SlideLeft:
            case UIScreenTransitionType.SlideFadeLeft:
            default:
                return new Vector2(Mathf.Max(1f, width), 0f);
        }
    }

    private bool IsModernTransition(UIScreenTransitionType type)
    {
        return type == UIScreenTransitionType.SlideFadeLeft ||
               type == UIScreenTransitionType.SlideFadeRight ||
               type == UIScreenTransitionType.SlideFadeUp ||
               type == UIScreenTransitionType.SlideFadeDown ||
               type == UIScreenTransitionType.SlideFade ||
               type == UIScreenTransitionType.ScaleFade ||
               type == UIScreenTransitionType.Depth ||
               IsPushTransition(type) ||
               IsCoverTransition(type) ||
               IsRevealTransition(type) ||
               type == UIScreenTransitionType.ZoomIn ||
               type == UIScreenTransitionType.ZoomOut ||
               type == UIScreenTransitionType.Pop ||
               type == UIScreenTransitionType.FlipHorizontal ||
               type == UIScreenTransitionType.FlipVertical;
    }

    private bool IsPushTransition(UIScreenTransitionType type)
    {
        return type == UIScreenTransitionType.PushLeft ||
               type == UIScreenTransitionType.PushRight ||
               type == UIScreenTransitionType.PushUp ||
               type == UIScreenTransitionType.PushDown;
    }

    private bool IsCoverTransition(UIScreenTransitionType type)
    {
        return type == UIScreenTransitionType.CoverLeft ||
               type == UIScreenTransitionType.CoverRight ||
               type == UIScreenTransitionType.CoverUp ||
               type == UIScreenTransitionType.CoverDown;
    }

    private bool IsRevealTransition(UIScreenTransitionType type)
    {
        return type == UIScreenTransitionType.RevealLeft ||
               type == UIScreenTransitionType.RevealRight ||
               type == UIScreenTransitionType.RevealUp ||
               type == UIScreenTransitionType.RevealDown;
    }

    private RectTransform GetRectTransform(GameObject page)
    {
        return page != null ? page.GetComponent<RectTransform>() : null;
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject page)
    {
        if (page == null)
            return null;

        CanvasGroup group = page.GetComponent<CanvasGroup>();
        if (group == null)
            group = page.AddComponent<CanvasGroup>();

        return group;
    }

    private void CaptureHomePosition(RectTransform rect)
    {
        if (rect == null || _homePositions.ContainsKey(rect))
            return;

        _homePositions[rect] = rect.anchoredPosition;
    }

    private void CaptureHomeScale(RectTransform rect)
    {
        if (rect == null || _homeScales.ContainsKey(rect))
            return;

        _homeScales[rect] = rect.localScale;
    }

    private void CaptureHomeRotation(RectTransform rect)
    {
        if (rect == null || _homeRotations.ContainsKey(rect))
            return;

        _homeRotations[rect] = rect.localRotation;
    }

    private Vector2 GetHomePosition(RectTransform rect)
    {
        if (rect == null)
            return Vector2.zero;

        CaptureHomePosition(rect);
        return _homePositions.TryGetValue(rect, out Vector2 position) ? position : rect.anchoredPosition;
    }

    private void MoveToHome(RectTransform rect)
    {
        if (rect != null)
            rect.anchoredPosition = GetHomePosition(rect);
    }

    private Vector3 GetHomeScale(RectTransform rect)
    {
        if (rect == null)
            return Vector3.one;

        CaptureHomeScale(rect);
        return _homeScales.TryGetValue(rect, out Vector3 scale) ? scale : rect.localScale;
    }

    private void MoveToHomeScale(RectTransform rect)
    {
        if (rect != null)
            rect.localScale = GetHomeScale(rect);
    }

    private Quaternion GetHomeRotation(RectTransform rect)
    {
        if (rect == null)
            return Quaternion.identity;

        CaptureHomeRotation(rect);
        return _homeRotations.TryGetValue(rect, out Quaternion rotation) ? rotation : rect.localRotation;
    }

    private void MoveToHomeRotation(RectTransform rect)
    {
        if (rect != null)
            rect.localRotation = GetHomeRotation(rect);
    }

    private static Vector3 ScaleBy(Vector3 scale, float multiplier)
    {
        return new Vector3(scale.x * multiplier, scale.y * multiplier, scale.z * multiplier);
    }

    private float _popupStartSafeScale()
    {
        return Mathf.Clamp(1f - popOvershootPercent * 0.01f, 0.84f, 0.98f);
    }

    private void SetInteraction(CanvasGroup group, bool enabled)
    {
        if (group == null)
            return;

        group.interactable = enabled;
        group.blocksRaycasts = enabled;
    }

    private void KillActiveTransition()
    {
        if (_activeSequence == null)
            return;

        _activeSequence.Kill(false);
        _activeSequence = null;
    }

    private void SafeInvoke(Action callback)
    {
        try
        {
            callback?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"UIScreenTransitionAnimator: completion callback failed: {exception.Message}", this);
        }
    }
}
