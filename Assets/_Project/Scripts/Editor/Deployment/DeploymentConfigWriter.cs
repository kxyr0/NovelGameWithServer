#if UNITY_EDITOR
using System.Collections.Generic;

public static class DeploymentConfigWriter
{
    public static NetworkRuntimeConfigData BuildConfig(string selectedEnvironmentId)
    {
        NetworkRuntimeConfigData current = NetworkRuntimeConfigFile.LoadOrDefault();
        var environments = new List<NetworkEnvironmentEntry>();
        foreach (DeploymentEnvironmentPreset preset in DeploymentEnvironmentPresets.All)
            environments.Add(ContentReleaseUploadDestinationSettings.ApplyToPreset(preset).ToNetworkEntry());

        current.selectedEnvironmentId = DeploymentEnvironmentIds.Normalize(selectedEnvironmentId);
        current.environments = environments;
        return current;
    }

    public static void SaveSelected(string selectedEnvironmentId)
    {
        NetworkRuntimeConfigFile.Save(BuildConfig(selectedEnvironmentId));
    }

    public static void SaveTemplates()
    {
        NetworkRuntimeConfigFile.Save(
            BuildConfig(DeploymentEnvironmentIds.Stage),
            NetworkRuntimeConfigFile.StageTemplatePath);
        NetworkRuntimeConfigFile.Save(
            BuildConfig(DeploymentEnvironmentIds.Production),
            NetworkRuntimeConfigFile.ProductionTemplatePath);
    }
}
#endif
