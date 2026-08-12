#if UNITY_EDITOR
using UnityEditor;

public static class DeploymentBuildSettingsConfigurator
{
    public static void Apply(DeploymentEnvironmentPreset preset)
    {
        PlayerSettings.productName = preset.ProductName;

        string baseIdentifier = ResolveBaseApplicationIdentifier();
        string identifier = baseIdentifier + preset.ApplicationIdSuffix;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, identifier);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, identifier);
        AssetDatabase.SaveAssets();
    }

    private static string ResolveBaseApplicationIdentifier()
    {
        string current = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
        if (string.IsNullOrWhiteSpace(current))
            return DeploymentEnvironmentPresets.DefaultApplicationId;

        string stageSuffix = DeploymentEnvironmentPresets.Stage.ApplicationIdSuffix;
        return current.EndsWith(stageSuffix, System.StringComparison.OrdinalIgnoreCase)
            ? current.Substring(0, current.Length - stageSuffix.Length)
            : current;
    }
}
#endif
