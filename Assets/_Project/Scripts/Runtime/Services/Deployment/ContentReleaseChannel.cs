using System;

public static class ContentReleaseChannel
{
    public const string Stage = "stage";
    public const string Production = "prod";

    public static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }

    public static bool IsKnown(string value)
    {
        string normalized = Normalize(value);
        return normalized == Stage || normalized == Production;
    }

    public static bool IsProduction(string value)
    {
        return string.Equals(Normalize(value), Production, StringComparison.Ordinal);
    }

    public static string FromEnvironmentId(string environmentId)
    {
        return DeploymentEnvironmentIds.IsProduction(environmentId) ? Production : Stage;
    }
}
