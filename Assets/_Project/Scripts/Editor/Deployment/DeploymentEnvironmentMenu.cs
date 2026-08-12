#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public static class DeploymentEnvironmentMenu
{
    [MenuItem("VN/Выкладка/Применить Тест", priority = 10)]
    public static void ApplyStage()
    {
        Apply(DeploymentEnvironmentIds.Stage);
    }

    [MenuItem("VN/Выкладка/Применить Прод", priority = 11)]
    public static void ApplyProduction()
    {
        Apply(DeploymentEnvironmentIds.Production);
    }

    [MenuItem("VN/Выкладка/Проверить настройки среды", priority = 30)]
    public static void Validate()
    {
        List<string> issues = DeploymentEnvironmentValidator.ValidateProject();
        string message = issues.Count == 0
            ? "Настройки Тест/Прод корректны."
            : string.Join("\n", issues);
        EditorUtility.DisplayDialog("Проверка выкладки", message, "OK");
    }

    [MenuItem("VN/Выкладка/Собрать активные Addressables", priority = 50)]
    public static void BuildActiveAddressables()
    {
        AddressableAssetSettings.BuildPlayerContent();
    }

    private static void Apply(string environmentId)
    {
        DeploymentEnvironmentPreset preset = ContentReleaseUploadDestinationSettings.ApplyToPreset(
            DeploymentEnvironmentPresets.Find(environmentId));
        DeploymentConfigWriter.SaveSelected(preset.EnvironmentId);
        DeploymentConfigWriter.SaveTemplates();
        AddressablesDeploymentConfigurator.Apply(preset);
        DeploymentBuildSettingsConfigurator.Apply(preset);

        List<string> issues = DeploymentEnvironmentValidator.ValidateProject();
        string message = issues.Count == 0
            ? "Среда применена: " + preset.DisplayName + "."
            : "Среда применена, но есть проблемы:\n" + string.Join("\n", issues);
        EditorUtility.DisplayDialog("Выкладка", message, "OK");
    }
}
#endif
