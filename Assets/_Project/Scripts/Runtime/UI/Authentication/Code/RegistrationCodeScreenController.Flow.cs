using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed partial class RegistrationCodeScreenController
{
    private void CompleteVerification(RegistrationCodeResult result)
    {
        if (!_sessionActive)
            return;
        _verificationInProgress = false;
        _codeInputs?.SetResultState(result.Success);
        if (!result.Success)
        {
            _codeInputs?.SetInteractable(true);
            return;
        }
        string email = _registrationScreen != null ? _registrationScreen.Email : "";
        AccountLoginState.MarkSignedIn(email, NetworkManager.CurrentProfile?.playerId);
        StopSuccessRoutine();
        _successRoutine = StartCoroutine(OpenMainAfterDelay());
    }

    private void CompleteResend(bool success, string error)
    {
        if (!_sessionActive)
            return;
        _resendInProgress = false;
        if (success)
        {
            _codeInputs?.ClearAndFocus();
            StartResendTimer();
            return;
        }
        RefreshResendUi();
        Debug.LogWarning($"Registration code resend failed: {error}", this);
    }

    private void HandleScreenChanged(string screenId)
    {
        bool active = UIScreenState.NormalizeScreenId(screenId) == _codeScreenId;
        if (active && !_sessionActive)
            BeginSession();
        else if (!active && _sessionActive)
            EndSession();
    }

    private void BeginSession()
    {
        _sessionActive = true;
        _verificationInProgress = false;
        _resendInProgress = false;
        _codeInputs?.ClearAndFocus();
        StartResendTimer();
    }

    private void EndSession()
    {
        _sessionActive = false;
        StopSuccessRoutine();
    }

    private void StartResendTimer()
    {
        _remainingSeconds = _resendDelay;
        RefreshResendUi();
    }

    private void RefreshResendUi()
    {
        int total = Mathf.CeilToInt(_remainingSeconds);
        if (_countdownText != null)
            _countdownText.text = total > 0
                ? $"{_countdownPrefix}{total / 60:00}:{total % 60:00}"
                : _resendReadyText;
        ApplyResendButtonState(_sessionActive && total <= 0 && !_resendInProgress);
    }

    private void ApplyResendButtonState(bool ready)
    {
        UIButtonStateColor.Apply(_resendButton, ready, _resendReadyColor,
            _resendDisabledColor, _resendColorMode);
    }

    private IEnumerator OpenMainAfterDelay()
    {
        yield return new WaitForSecondsRealtime(_successExitDelay);
        _successRoutine = null;
        if (_screenNavigator == null)
            _screenNavigator = FindObjectOfType<StoryScreenNavigator>(true);
        if (_screenNavigator == null || !_screenNavigator.OpenScreen(_mainScreenId))
            Debug.LogWarning($"Main screen '{_mainScreenId}' is unavailable.", this);
    }

    private void FailUnavailableService()
    {
        _verificationInProgress = false;
        _codeInputs?.SetInteractable(true);
        Debug.LogWarning("Registration code service is not assigned.", this);
    }

    private void StopSuccessRoutine()
    {
        if (_successRoutine != null)
            StopCoroutine(_successRoutine);
        _successRoutine = null;
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
