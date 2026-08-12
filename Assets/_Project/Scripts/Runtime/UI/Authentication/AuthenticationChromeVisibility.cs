using System;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Authentication Chrome Visibility")]
public sealed class AuthenticationChromeVisibility : MonoBehaviour
{
    [Header("Shared UI")]
    [SerializeField] private CanvasGroup _itemsGroup;
    [SerializeField] private CanvasGroup _navigationGroup;

    [Header("Authentication Screens")]
    [SerializeField] private string[] _hiddenScreenIds =
    {
        "LoginScreen",
        "RegisterScreen"
    };
    [SerializeField] private string _passwordRecoveryScreenId = "PasswordRecoveryScreen";
    [SerializeField] private string _registrationCodeScreenId = "RegistrationCodeScreen";

    private bool _mustStayHidden;

    private void OnEnable()
    {
        UIScreenState.CurrentScreenChanged += HandleScreenChanged;
        HandleScreenChanged(UIScreenState.CurrentScreenId);
    }

    private void OnDisable()
    {
        UIScreenState.CurrentScreenChanged -= HandleScreenChanged;
    }

    private void OnValidate()
    {
        _hiddenScreenIds ??= Array.Empty<string>();
        for (int i = 0; i < _hiddenScreenIds.Length; i++)
            _hiddenScreenIds[i] = UIScreenState.NormalizeScreenId(_hiddenScreenIds[i]);
        _passwordRecoveryScreenId = UIScreenState.NormalizeScreenId(_passwordRecoveryScreenId);
        _registrationCodeScreenId = UIScreenState.NormalizeScreenId(_registrationCodeScreenId);
    }

    private void LateUpdate()
    {
        if (_mustStayHidden)
            SetSharedUiVisible(false);
    }

    public void Refresh()
    {
        HandleScreenChanged(UIScreenState.CurrentScreenId);
    }

    private void HandleScreenChanged(string screenId)
    {
        _mustStayHidden = ContainsScreen(screenId);
        if (_mustStayHidden)
        {
            SetSharedUiVisible(false);
            return;
        }

        RestoreGroup(_itemsGroup, screenId);
        RestoreGroup(_navigationGroup, screenId);
    }

    private bool ContainsScreen(string screenId)
    {
        string currentId = UIScreenState.NormalizeScreenId(screenId);
        if (_passwordRecoveryScreenId.Length > 0 && currentId == _passwordRecoveryScreenId)
            return true;
        if (_registrationCodeScreenId.Length > 0 && currentId == _registrationCodeScreenId)
            return true;
        for (int i = 0; i < _hiddenScreenIds.Length; i++)
        {
            if (UIScreenState.NormalizeScreenId(_hiddenScreenIds[i]) == currentId)
                return true;
        }

        return false;
    }

    private void SetSharedUiVisible(bool visible)
    {
        SetGroup(_itemsGroup, visible);
        SetGroup(_navigationGroup, visible);
    }

    private static void RestoreGroup(CanvasGroup group, string screenId)
    {
        if (group == null)
            return;

        UIScreenNavigationVisibility navigationRule =
            group.GetComponent<UIScreenNavigationVisibility>();
        if (navigationRule != null)
        {
            navigationRule.Refresh();
            return;
        }

        UIScreenVisibilityRule visibilityRule = group.GetComponent<UIScreenVisibilityRule>();
        if (visibilityRule != null)
        {
            visibilityRule.ApplyCurrentScreen(screenId);
            return;
        }

        SetGroup(group, true);
    }

    private static void SetGroup(CanvasGroup group, bool visible)
    {
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }
}
