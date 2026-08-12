using System;
using UnityEngine;
using UnityEngine.UI;

public static class AccountLoginState
{
    private const string SignedInKey = "Nocturne.Account.SignedIn";
    private const string EmailKey = "Nocturne.Account.Email";
    private const string PublicPlayerIdKey = "Nocturne.Account.PublicPlayerId";
    private const string LoginScreenId = "LoginScreen";

    public static bool IsSignedIn => PlayerPrefs.GetInt(SignedInKey, 0) == 1;
    public static string Email => RegistrationFormValidator.NormalizeEmail(
        PlayerPrefs.GetString(EmailKey, ""));
    public static string PublicPlayerId => PlayerPrefs.GetString(PublicPlayerIdKey, "").Trim();
    public static bool HasAccountIdentity => IsSignedIn && Email.Length > 0;

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents()
    {
        Changed = null;
    }

    public static string ResolveInitialScreen(string configuredScreenId)
    {
        return HasAccountIdentity
            ? UIScreenState.NormalizeScreenId(configuredScreenId)
            : LoginScreenId;
    }

    public static void MarkSignedIn(string email = "", string playerId = "")
    {
        string normalizedEmail = RegistrationFormValidator.NormalizeEmail(email);
        if (normalizedEmail.Length == 0)
            normalizedEmail = Email;
        if (normalizedEmail.Length == 0)
            return;

        string identity = string.IsNullOrWhiteSpace(playerId)
            ? NetworkManager.CurrentProfile?.playerId
            : playerId;
        string publicId = PlayerPublicIdFormatter.FormatServerIdOrEmpty(identity);

        PlayerPrefs.SetInt(SignedInKey, 1);
        if (normalizedEmail.Length > 0)
            PlayerPrefs.SetString(EmailKey, normalizedEmail);
        if (publicId.Length > 0)
            PlayerPrefs.SetString(PublicPlayerIdKey, publicId);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SignOut()
    {
        PlayerPrefs.DeleteKey(SignedInKey);
        PlayerPrefs.DeleteKey(EmailKey);
        PlayerPrefs.DeleteKey(PublicPlayerIdKey);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}

public sealed partial class LoginScreenController
{
    private const float PasswordButtonRightPadding = 12f;

    private void ResolveVisibilityTargets()
    {
        CanvasGroup oldRegisterGroup = _registerButtonGroup;
        _registerButtonGroup = ResolveButtonGroup(
            _registerButton, _registerButtonGroup, "RegisterButton");
        if (oldRegisterGroup != null && oldRegisterGroup != _registerButtonGroup &&
            IsInside(_passwordRecoveryButton, oldRegisterGroup))
            SetGroupVisible(oldRegisterGroup, true);
        _passwordVisibilityGroup = ResolveButtonGroup(
            _passwordVisibilityButton, _passwordVisibilityGroup, null);
        KeepPasswordButtonInsideField();
    }

    private static CanvasGroup ResolveButtonGroup(
        Button button,
        CanvasGroup assignedGroup,
        string wrapperName)
    {
        if (button == null)
            return assignedGroup;
        if (IsInside(button, assignedGroup))
            return assignedGroup;

        Transform target = button.transform;
        for (Transform current = target; current != null; current = current.parent)
        {
            if (!string.IsNullOrEmpty(wrapperName) && current.name == wrapperName)
            {
                target = current;
                break;
            }
        }

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
    }

    private static bool IsInside(Button button, CanvasGroup group)
    {
        return button != null && group != null &&
            (button.transform == group.transform ||
             button.transform.IsChildOf(group.transform));
    }

    private void KeepPasswordButtonInsideField()
    {
        if (_passwordVisibilityButton == null)
            return;
        RectTransform buttonRect = _passwordVisibilityButton.transform as RectTransform;
        RectTransform parentRect = buttonRect != null ? buttonRect.parent as RectTransform : null;
        if (parentRect == null || parentRect.rect.width <= 0f ||
            !Mathf.Approximately(buttonRect.anchorMin.x, buttonRect.anchorMax.x))
            return;

        buttonRect.SetAsLastSibling();

        float anchorX = Mathf.Lerp(
            parentRect.rect.xMin, parentRect.rect.xMax, buttonRect.anchorMin.x);
        float minX = parentRect.rect.xMin - anchorX +
            buttonRect.rect.width * buttonRect.pivot.x + PasswordButtonRightPadding;
        float maxX = parentRect.rect.xMax - anchorX -
            buttonRect.rect.width * (1f - buttonRect.pivot.x) - PasswordButtonRightPadding;
        Vector2 position = buttonRect.anchoredPosition;
        position.x = Mathf.Clamp(position.x, minX, Mathf.Max(minX, maxX));
        buttonRect.anchoredPosition = position;
    }

    private void SetPasswordIconReady(bool hasPassword)
    {
        if (_passwordVisibilityIcon == null)
            return;
        _passwordVisibilityIcon.enabled = true;
        Color color = _passwordVisibilityIcon.color;
        color.a = hasPassword ? 1f : 0f;
        _passwordVisibilityIcon.color = color;
    }
}
