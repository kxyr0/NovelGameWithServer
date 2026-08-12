using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Authentication/Test Login Service")]
public sealed class TestLoginService : MonoBehaviour, ILoginService
{
    [Header("Test Credentials")]
    [SerializeField] private string _testEmail = "test@example.com";
    [SerializeField] private string _testPassword = "123456";
    [SerializeField, Min(0f)] private float _delaySeconds = 0.3f;

    private void OnValidate()
    {
        _testEmail = RegistrationFormValidator.NormalizeEmail(_testEmail);
        _testPassword ??= "";
        _delaySeconds = Mathf.Max(0f, _delaySeconds);
    }

    public void Login(string email, string password, Action<LoginResult> completed)
    {
        if (completed == null)
            return;

        if (!isActiveAndEnabled)
        {
            completed(new LoginResult(false, LoginFailureKind.Unavailable));
            return;
        }

        bool valid = string.Equals(
                         RegistrationFormValidator.NormalizeEmail(email),
                         RegistrationFormValidator.NormalizeEmail(_testEmail),
                         StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(password ?? "", _testPassword ?? "", StringComparison.Ordinal);
        StartCoroutine(CompleteAfterDelay(valid, completed));
    }

    private IEnumerator CompleteAfterDelay(bool valid, Action<LoginResult> completed)
    {
        if (_delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(_delaySeconds);

        completed(new LoginResult(
            valid,
            valid ? LoginFailureKind.None : LoginFailureKind.InvalidCredentials));
    }
}
