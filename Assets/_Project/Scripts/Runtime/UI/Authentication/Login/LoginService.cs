using System;

public enum LoginFailureKind
{
    None,
    InvalidCredentials,
    Unavailable
}

public readonly struct LoginResult
{
    public readonly bool Success;
    public readonly LoginFailureKind FailureKind;
    public readonly string Error;

    public LoginResult(
        bool success,
        LoginFailureKind failureKind = LoginFailureKind.None,
        string error = "")
    {
        Success = success;
        FailureKind = success ? LoginFailureKind.None : failureKind;
        Error = error ?? "";
    }
}

public interface ILoginService
{
    void Login(string email, string password, Action<LoginResult> completed);
}
