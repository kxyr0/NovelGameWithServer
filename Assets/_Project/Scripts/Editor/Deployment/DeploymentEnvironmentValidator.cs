#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

public static class DeploymentEnvironmentValidator
{
    private const string StoryLoadingMediaGroupName = "Story Loading Media";

    public static List<string> ValidateProject()
    {
        var issues = new List<string>();
        NetworkRuntimeConfigData config = NetworkRuntimeConfigFile.LoadOrDefault();
        DeploymentEnvironmentValidationResult runtime = DeploymentEnvironmentPolicy.Validate(config);
        if (!runtime.IsValid)
            issues.Add(runtime.Message);

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            issues.Add("Настройки Addressables не найдены.");
            return issues;
        }

        ValidateProfile(settings, DeploymentEnvironmentPresets.Stage, issues);
        ValidateProfile(settings, DeploymentEnvironmentPresets.Production, issues);
        ValidateStoryLoadingMediaGroup(settings, issues);
        if (!settings.BuildRemoteCatalog)
            issues.Add("Remote catalog в Addressables выключен.");

        return issues;
    }

    private static void ValidateProfile(
        AddressableAssetSettings settings,
        DeploymentEnvironmentPreset preset,
        List<string> issues)
    {
        string profileId = settings.profileSettings.GetProfileId(preset.AddressablesProfileName);
        if (string.IsNullOrEmpty(profileId))
        {
            issues.Add("Не найден профиль Addressables: " + preset.AddressablesProfileName);
            return;
        }

        DeploymentEnvironmentPreset effectivePreset = ContentReleaseUploadDestinationSettings.ApplyToPreset(preset);
        string loadPath = settings.profileSettings.GetValueByName(profileId, AddressableAssetSettings.kRemoteLoadPath);
        if (!string.Equals(loadPath, effectivePreset.AddressablesLoadPath, System.StringComparison.Ordinal))
            issues.Add("Неверный load path Addressables для " + preset.AddressablesProfileName + ".");
    }

    private static void ValidateStoryLoadingMediaGroup(AddressableAssetSettings settings, List<string> issues)
    {
        AddressableAssetGroup group = settings.FindGroup(StoryLoadingMediaGroupName);
        if (group == null)
        {
            issues.Add("Не найдена группа Addressables: " + StoryLoadingMediaGroupName);
            return;
        }

        BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema == null)
        {
            issues.Add("У группы Story Loading Media нет bundled asset schema.");
            return;
        }

        if (schema.BuildPath.Id != AddressableAssetSettings.kRemoteBuildPath)
            issues.Add("Build path группы Story Loading Media должен использовать Remote.BuildPath.");
        if (schema.LoadPath.Id != AddressableAssetSettings.kRemoteLoadPath)
            issues.Add("Load path группы Story Loading Media должен использовать Remote.LoadPath.");
        if (schema.RetryCount < 1)
            issues.Add("Retry count группы Story Loading Media должен быть не меньше 1.");
    }
}
#endif
