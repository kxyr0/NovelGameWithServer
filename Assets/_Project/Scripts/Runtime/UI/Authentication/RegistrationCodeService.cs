using System;

public readonly struct RegistrationCodeResult
{
    public readonly bool Success;
    public readonly string Error;

    public RegistrationCodeResult(bool success, string error = "")
    {
        Success = success;
        Error = error ?? "";
    }
}

public interface IRegistrationCodeService
{
    void VerifyCode(
        string email,
        string code,
        Action<RegistrationCodeResult> completed);

    void ResendCode(
        string email,
        Action<bool, string> completed);
}
