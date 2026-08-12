using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Registration Email Error Layout")]
public sealed class RegistrationEmailErrorLayout : MonoBehaviour
{
    [Header("Validation")]
    [SerializeField] private TMP_InputField _emailInput;
    [SerializeField] private TMP_Text _errorText;
    [SerializeField] private CanvasGroup _errorTextGroup;
    [SerializeField] private string _errorMessage = "Некорректный e-mail";

    [Header("Vertical Grid Spacing")]
    [SerializeField] private VerticalLayoutGroup _verticalGrid;
    [SerializeField] private float _invalidSpacingIncrease = 16f;
    [SerializeField, Min(0f)] private float _spacingDuration = 0.2f;
    [SerializeField] private Ease _spacingEase = Ease.OutCubic;

    [Header("Root Movement")]
    [SerializeField] private RectTransform _rootToMove;
    [SerializeField] private Vector2 _invalidRootOffset = new Vector2(0f, -24f);
    [SerializeField, Min(0f)] private float _moveDuration = 0.2f;
    [SerializeField] private Ease _moveEase = Ease.OutCubic;

    [Header("Input Outline")]
    [SerializeField] private Graphic _inputOutline;
    [SerializeField] private Color _invalidOutlineColor = new Color32(190, 80, 70, 255);
    [SerializeField, Min(0f)] private float _feedbackDuration = 0.15f;
    [SerializeField] private Ease _feedbackEase = Ease.OutQuad;

    [Header("Time")]
    [SerializeField] private bool _useUnscaledTime = true;

    private float _normalSpacing;
    private Vector2 _normalRootPosition;
    private Color _normalOutlineColor;
    private Tween _spacingTween;
    private Tween _rootTween;
    private Tween _outlineTween;
    private Tween _errorTween;
    private bool _normalStateCaptured;
    private bool _emailWasEdited;
    private bool? _lastInvalidState;

    private void OnEnable()
    {
        CaptureNormalState();
        _emailWasEdited = false;
        _lastInvalidState = null;
        if (_errorText != null)
            _errorText.text = _errorMessage;
        if (_emailInput != null)
        {
            _emailInput.transition = Selectable.Transition.None;
            _emailInput.onValueChanged.RemoveListener(HandleEmailChanged);
            _emailInput.onValueChanged.AddListener(HandleEmailChanged);
        }
        ApplyState(false, false);
    }

    private void OnDisable()
    {
        if (_emailInput != null)
            _emailInput.onValueChanged.RemoveListener(HandleEmailChanged);
        KillTweens();
    }

    private void OnValidate()
    {
        _spacingDuration = Mathf.Max(0f, _spacingDuration);
        _moveDuration = Mathf.Max(0f, _moveDuration);
        _feedbackDuration = Mathf.Max(0f, _feedbackDuration);
    }

    public void Refresh()
    {
        ApplyState(_emailWasEdited && HasInvalidEmail(), true);
    }

    public void ResetInteraction()
    {
        _emailWasEdited = false;
        _lastInvalidState = null;
        ApplyState(false, true);
    }

    private void HandleEmailChanged(string value)
    {
        _emailWasEdited = true;
        ApplyState(HasInvalidEmail(), true);
    }
    private bool HasInvalidEmail()
    {
        string email = _emailInput != null ? _emailInput.text : "";
        return email.Length > 0 && !RegistrationFormValidator.IsStrictEmail(email);
    }

    private void ApplyState(bool invalid, bool animate)
    {
        if (_lastInvalidState.HasValue && _lastInvalidState.Value == invalid)
            return;

        _lastInvalidState = invalid;
        KillTweens();
        AnimateSpacing(invalid, animate);
        AnimateRoot(invalid, animate);
        AnimateOutline(invalid, animate);
        AnimateError(invalid, animate);
    }

    private void AnimateSpacing(bool invalid, bool animate)
    {
        if (_verticalGrid == null)
            return;
        float target = _normalSpacing + (invalid ? _invalidSpacingIncrease : 0f);
        if (!CanAnimate(animate, _spacingDuration))
        {
            _verticalGrid.spacing = target;
            return;
        }
        _spacingTween = DOTween.To(() => _verticalGrid.spacing,
                value => _verticalGrid.spacing = value, target, _spacingDuration)
            .SetEase(_spacingEase).SetUpdate(_useUnscaledTime);
    }

    private void AnimateRoot(bool invalid, bool animate)
    {
        if (_rootToMove == null)
            return;
        Vector2 target = _normalRootPosition + (invalid ? _invalidRootOffset : Vector2.zero);
        if (!CanAnimate(animate, _moveDuration))
        {
            _rootToMove.anchoredPosition = target;
            return;
        }
        _rootTween = _rootToMove.DOAnchorPos(target, _moveDuration)
            .SetEase(_moveEase).SetUpdate(_useUnscaledTime);
    }

    private void AnimateOutline(bool invalid, bool animate)
    {
        if (_inputOutline == null)
            return;
        Color target = invalid ? _invalidOutlineColor : _normalOutlineColor;
        if (!CanAnimate(animate, _feedbackDuration))
        {
            _inputOutline.color = target;
            return;
        }
        _outlineTween = _inputOutline.DOColor(target, _feedbackDuration)
            .SetEase(_feedbackEase).SetUpdate(_useUnscaledTime);
    }

    private void AnimateError(bool visible, bool animate)
    {
        if (_errorText != null)
            _errorText.enabled = _errorTextGroup != null || visible;
        if (_errorTextGroup == null)
            return;
        _errorTextGroup.interactable = false;
        _errorTextGroup.blocksRaycasts = false;
        float target = visible ? 1f : 0f;
        if (!CanAnimate(animate, _feedbackDuration))
        {
            _errorTextGroup.alpha = target;
            return;
        }
        _errorTween = _errorTextGroup.DOFade(target, _feedbackDuration)
            .SetEase(_feedbackEase).SetUpdate(_useUnscaledTime);
    }

    private void CaptureNormalState()
    {
        if (_normalStateCaptured)
            return;
        if (_verticalGrid != null)
            _normalSpacing = _verticalGrid.spacing;
        if (_rootToMove != null)
            _normalRootPosition = _rootToMove.anchoredPosition;
        if (_inputOutline != null)
            _normalOutlineColor = _inputOutline.color;
        _normalStateCaptured = true;
    }

    private static bool CanAnimate(bool animate, float duration)
    {
        return animate && duration > 0f && Application.isPlaying;
    }
    private void KillTweens()
    {
        _spacingTween?.Kill();
        _rootTween?.Kill();
        _outlineTween?.Kill();
        _errorTween?.Kill();
        _spacingTween = _rootTween = _outlineTween = _errorTween = null;
    }
}
