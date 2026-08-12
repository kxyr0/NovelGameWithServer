public readonly struct DeploymentEnvironmentValidationResult
{
    public DeploymentEnvironmentValidationResult(bool isValid, string message)
    {
        IsValid = isValid;
        Message = message ?? "";
    }

    public bool IsValid { get; }
    public string Message { get; }

    public static DeploymentEnvironmentValidationResult Ok(string message = "OK")
    {
        return new DeploymentEnvironmentValidationResult(true, message);
    }

    public static DeploymentEnvironmentValidationResult Fail(string message)
    {
        return new DeploymentEnvironmentValidationResult(false, message);
    }
}
