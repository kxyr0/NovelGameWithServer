#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

public static class AddressablesDeploymentConfigurator
{
    private const string StoryLoadingMediaGroupName = "Story Loading Media";

    public static void Apply(DeploymentEnvironmentPreset preset)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
            throw new System.InvalidOperationException("Addressables settings could not be loaded.");

        string profileId = EnsureProfile(settings, preset);
        settings.activeProfileId = profileId;
        settings.BuildRemoteCatalog = true;
        settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
        settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);

        AddressableAssetGroup group = settings.FindGroup(StoryLoadingMediaGroupName);
        if (group != null)
            ConfigureRemoteGroup(settings, group);

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
    }

    private static string EnsureProfile(AddressableAssetSettings settings, DeploymentEnvironmentPreset preset)
    {
        string profileId = settings.profileSettings.GetProfileId(preset.AddressablesProfileName);
        if (string.IsNullOrEmpty(profileId))
            profileId = settings.profileSettings.AddProfile(preset.AddressablesProfileName, settings.activeProfileId);

        settings.profileSettings.SetValue(profileId, AddressableAssetSettings.kRemoteBuildPath, preset.AddressablesBuildPath);
        settings.profileSettings.SetValue(profileId, AddressableAssetSettings.kRemoteLoadPath, preset.AddressablesLoadPath);
        return profileId;
    }

    private static void ConfigureRemoteGroup(AddressableAssetSettings settings, AddressableAssetGroup group)
    {
        BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema == null)
            return;

        schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
        schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
        schema.UseAssetBundleCache = true;
        schema.RetryCount = 2;
    }
}
#endif
