#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public sealed class DeploymentEnvironmentPreset
{
    public string EnvironmentId;
    public string DisplayName;
    public string BaseUrl;
    public string ContentChannel;
    public string AddressablesProfileName;
    public string AddressablesBuildPath;
    public string AddressablesLoadPath;
    public string ProductName;
    public string ApplicationIdSuffix;

    public NetworkEnvironmentEntry ToNetworkEntry()
    {
        return new NetworkEnvironmentEntry
        {
            id = EnvironmentId,
            displayName = DisplayName,
            baseUrl = BaseUrl,
            contentChannel = ContentChannel,
            addressablesRemoteLoadPath = AddressablesLoadPath
        };
    }
}

public static class DeploymentEnvironmentPresets
{
    public const string DefaultProductName = "NovelTemplate";
    public const string DefaultApplicationId = "com.nocturnal.novella";

    public static DeploymentEnvironmentPreset Stage => new DeploymentEnvironmentPreset
    {
        EnvironmentId = DeploymentEnvironmentIds.Stage,
        DisplayName = "Staging",
        BaseUrl = "https://stage.nocturnedc.ru",
        ContentChannel = DeploymentEnvironmentPolicy.StageContentChannel,
        AddressablesProfileName = "Stage",
        AddressablesBuildPath = "ServerData/stage/[BuildTarget]",
        AddressablesLoadPath = "https://cdn.nocturnedc.ru/stage/[BuildTarget]",
        ProductName = DefaultProductName + " Stage",
        ApplicationIdSuffix = ".stage"
    };

    public static DeploymentEnvironmentPreset Production => new DeploymentEnvironmentPreset
    {
        EnvironmentId = DeploymentEnvironmentIds.Production,
        DisplayName = "Production",
        BaseUrl = ApiRoutes.BaseUrl,
        ContentChannel = DeploymentEnvironmentPolicy.ProductionContentChannel,
        AddressablesProfileName = "Prod",
        AddressablesBuildPath = "ServerData/prod/[BuildTarget]",
        AddressablesLoadPath = "https://cdn.nocturnedc.ru/prod/[BuildTarget]",
        ProductName = DefaultProductName,
        ApplicationIdSuffix = ""
    };

    public static IReadOnlyList<DeploymentEnvironmentPreset> All => new[]
    {
        Stage,
        Production
    };

    public static DeploymentEnvironmentPreset Find(string environmentId)
    {
        string id = DeploymentEnvironmentIds.Normalize(environmentId);
        foreach (DeploymentEnvironmentPreset preset in All)
        {
            if (DeploymentEnvironmentIds.Normalize(preset.EnvironmentId) == id)
                return preset;
        }

        throw new ArgumentException("Unknown deployment environment: " + environmentId);
    }
}
#endif
