using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed partial class LoginScreenController
{
    [Header("Register Button Fade")]
    [SerializeField, Min(0f)] private float _registerFadeDuration = 0.25f;
    [SerializeField] private Ease _registerFadeEase = Ease.OutCubic;
    private Tween _registerFadeTween;
    private bool _registerFadeTargetVisible;
    public void RefreshForm()
    {
        ResolveVisibilityTargets();
        bool hasEmail = HasText(_emailInput);
        bool hasPassword = HasText(_passwordInput);
        FadeRegisterButton(!hasEmail && !hasPassword);
        SetGroupVisible(_passwordVisibilityGroup, hasPassword);
        SetPasswordIconReady(hasPassword);
        if (!hasPassword && _passwordVisible)
            SetPasswordVisibility(false);
        bool ready = !_busy && hasEmail && hasPassword;
        UIButtonStateColor.Apply(
            _loginButton, ready, _readyColor, _disabledColor, _loginColorMode);
        SetInputsInteractable(!_busy);
        if (_passwordVisibilityButton != null)
            _passwordVisibilityButton.interactable = !_busy && hasPassword;
    }

    public void TogglePasswordVisibility()
    {
        if (_busy || !HasText(_passwordInput))
            return;
        SetPasswordVisibility(!_passwordVisible);
    }

    private void ConfigureUi()
    {
        ResolveVisibilityTargets();
        ConfigureField(_emailInput, TMP_InputField.ContentType.EmailAddress);
        ConfigureField(_passwordInput, TMP_InputField.ContentType.Password);
        SetPasswordVisibility(false);
    }

    private void SetPasswordVisibility(bool visible)
    {
        _passwordVisible = visible;
        if (_passwordInput != null)
        {
            _passwordInput.contentType = visible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;
            _passwordInput.ForceLabelUpdate();
        }

        if (_passwordVisibilityIcon == null)
            return;
        Sprite sprite = visible ? _visiblePasswordSprite : _hiddenPasswordSprite;
        if (sprite != null)
            _passwordVisibilityIcon.sprite = sprite;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        RefreshForm();
    }

    private void FadeRegisterButton(bool visible)
    {
        if (_registerButtonGroup == null)
            return;
        if (_registerFadeTween != null && _registerFadeTween.IsActive() &&
            _registerFadeTargetVisible == visible)
            return;
        _registerFadeTargetVisible = visible;
        if (!_registerButtonGroup.gameObject.activeSelf)
            _registerButtonGroup.gameObject.SetActive(true);
        _registerButtonGroup.interactable = visible;
        _registerButtonGroup.blocksRaycasts = visible;
        float targetAlpha = visible ? 1f : 0f;
        if (Mathf.Approximately(_registerButtonGroup.alpha, targetAlpha) &&
            (_registerFadeTween == null || !_registerFadeTween.IsActive()))
            return;
        StopRegisterFade();
        if (!Application.isPlaying || _registerFadeDuration <= 0f)
        {
            _registerButtonGroup.alpha = targetAlpha;
            return;
        }
        _registerFadeTween = _registerButtonGroup
            .DOFade(targetAlpha, _registerFadeDuration)
            .SetEase(_registerFadeEase)
            .SetUpdate(true)
            .OnComplete(() => _registerFadeTween = null);
    }

    private void StopRegisterFade()
    {
        if (_registerFadeTween != null && _registerFadeTween.IsActive())
            _registerFadeTween.Kill(false);
        _registerFadeTween = null;
    }
    private bool HasEnteredCredentials()
    {
        return HasText(_emailInput) && HasText(_passwordInput);
    }
    private void HandleInputChanged(string value)
    {
        _feedback?.Clear();
        RefreshForm();
    }

    private void BindUi()
    {
        BindInput(_emailInput);
        BindInput(_passwordInput);
        BindButton(_loginButton, SubmitLogin);
        BindButton(_registerButton, OpenRegister);
        BindButton(_passwordRecoveryButton, OpenPasswordRecovery);
        BindButton(_passwordVisibilityButton, TogglePasswordVisibility);
    }

    private void UnbindUi()
    {
        UnbindInput(_emailInput);
        UnbindInput(_passwordInput);
        UnbindButton(_loginButton, SubmitLogin);
        UnbindButton(_registerButton, OpenRegister);
        UnbindButton(_passwordRecoveryButton, OpenPasswordRecovery);
        UnbindButton(_passwordVisibilityButton, TogglePasswordVisibility);
    }

    private void SetInputsInteractable(bool interactable)
    {
        if (_emailInput != null)
            _emailInput.interactable = interactable;
        if (_passwordInput != null)
            _passwordInput.interactable = interactable;
    }

    private static void ConfigureField(
        TMP_InputField field,
        TMP_InputField.ContentType contentType)
    {
        if (field == null)
            return;
        field.transition = Selectable.Transition.None;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.contentType = contentType;
        field.ForceLabelUpdate();
    }

    private static bool HasText(TMP_InputField input)
    {
        return input != null && !string.IsNullOrEmpty(input.text);
    }

    private static void SetGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null)
            return;
        if (!group.gameObject.activeSelf)
            group.gameObject.SetActive(true);
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    private void BindInput(TMP_InputField input)
    {
        if (input == null)
            return;
        input.onValueChanged.RemoveListener(HandleInputChanged);
        input.onValueChanged.AddListener(HandleInputChanged);
    }

    private void UnbindInput(TMP_InputField input)
    {
        if (input != null)
            input.onValueChanged.RemoveListener(HandleInputChanged);
    }

    private static void BindButton(Button button, UnityAction action)
    {
        if (button == null)
            return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnbindButton(Button button, UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }
}
