using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Authentication/Login Error Layout Animator")]
public sealed class LoginErrorLayoutAnimator : MonoBehaviour
{
    [Header("Vertical Layout Spacing")]
    [SerializeField] private VerticalLayoutGroup _verticalLayout;
    [SerializeField] private float _errorSpacingIncrease = 30f;
    [SerializeField, Min(0f)] private float _spacingDuration = 0.2f;
    [SerializeField] private Ease _spacingEase = Ease.OutCubic;

    [Header("Root Movement")]
    [SerializeField] private RectTransform _rootToMove;
    [SerializeField] private Vector2 _errorRootOffset = new Vector2(0f, -80f);
    [SerializeField, Min(0f)] private float _moveDuration = 0.2f;
    [SerializeField] private Ease _moveEase = Ease.OutCubic;

    [Header("Time")]
    [SerializeField] private bool _useUnscaledTime = true;

    private float _normalSpacing;
    private Vector2 _normalRootPosition;
    private Tween _spacingTween;
    private Tween _rootTween;
    private bool _normalStateCaptured;
    private bool? _lastErrorVisible;

    private void OnEnable()
    {
        CaptureNormalState();
        _lastErrorVisible = null;
        ApplyState(false, false);
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void OnValidate()
    {
        _spacingDuration = Mathf.Max(0f, _spacingDuration);
        _moveDuration = Mathf.Max(0f, _moveDuration);
    }

    public void SetErrorVisible(bool visible)
    {
        ApplyState(visible, true);
    }

    public void ResolveReferences(Transform context)
    {
        Transform screenRoot = FindScreenRoot(context);
        bool changed = false;
        if (_verticalLayout == null)
        {
            Transform grid = FindDescendant(screenRoot, "VerticalGrid");
            _verticalLayout = grid != null ? grid.GetComponent<VerticalLayoutGroup>() : null;
            changed |= _verticalLayout != null;
        }
        if (_rootToMove == null)
        {
            Transform root = FindDescendant(screenRoot, "Root");
            _rootToMove = root as RectTransform;
            changed |= _rootToMove != null;
        }
        if (!changed)
            return;

        KillTweens();
        _normalStateCaptured = false;
        _lastErrorVisible = null;
        ApplyState(false, false);
    }

    public void ResetState(bool animate = true)
    {
        _lastErrorVisible = null;
        ApplyState(false, animate);
    }

    private void ApplyState(bool visible, bool animate)
    {
        CaptureNormalState();
        if (_lastErrorVisible.HasValue && _lastErrorVisible.Value == visible)
            return;

        _lastErrorVisible = visible;
        KillTweens();
        AnimateSpacing(visible, animate);
        AnimateRoot(visible, animate);
    }

    private void AnimateSpacing(bool visible, bool animate)
    {
        if (_verticalLayout == null)
            return;
        float target = _normalSpacing + (visible ? _errorSpacingIncrease : 0f);
        if (!CanAnimate(animate, _spacingDuration))
        {
            _verticalLayout.spacing = target;
            return;
        }
        _spacingTween = DOTween.To(() => _verticalLayout.spacing,
                value => _verticalLayout.spacing = value, target, _spacingDuration)
            .SetEase(_spacingEase).SetUpdate(_useUnscaledTime);
    }

    private void AnimateRoot(bool visible, bool animate)
    {
        if (_rootToMove == null)
            return;
        Vector2 target = _normalRootPosition + (visible ? _errorRootOffset : Vector2.zero);
        if (!CanAnimate(animate, _moveDuration))
        {
            _rootToMove.anchoredPosition = target;
            return;
        }
        _rootTween = _rootToMove.DOAnchorPos(target, _moveDuration)
            .SetEase(_moveEase).SetUpdate(_useUnscaledTime);
    }

    private void CaptureNormalState()
    {
        if (_normalStateCaptured)
            return;
        if (_verticalLayout != null)
            _normalSpacing = _verticalLayout.spacing;
        if (_rootToMove != null)
            _normalRootPosition = _rootToMove.anchoredPosition;
        _normalStateCaptured = true;
    }

    private static bool CanAnimate(bool animate, float duration)
    {
        return animate && duration > 0f && Application.isPlaying;
    }

    private static Transform FindScreenRoot(Transform context)
    {
        Transform current = context;
        while (current != null && current.name != "LoginScreen")
            current = current.parent;
        return current != null ? current : context;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (root.name == objectName)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }
        return null;
    }

    private void KillTweens()
    {
        _spacingTween?.Kill();
        _rootTween?.Kill();
        _spacingTween = _rootTween = null;
    }
}
