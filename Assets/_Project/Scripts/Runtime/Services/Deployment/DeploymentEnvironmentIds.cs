public static class DeploymentEnvironmentIds
{
    public const string Development = "dev";
    public const string Stage = "stage";
    public const string Production = "prod";

    public static string Normalize(string environmentId)
    {
        return string.IsNullOrWhiteSpace(environmentId)
            ? ""
            : environmentId.Trim().ToLowerInvariant();
    }

    public static bool IsStage(string environmentId)
    {
        return Normalize(environmentId) == Stage;
    }

    public static bool IsProduction(string environmentId)
    {
        return Normalize(environmentId) == Production;
    }
}
