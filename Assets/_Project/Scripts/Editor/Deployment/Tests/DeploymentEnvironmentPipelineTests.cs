#if UNITY_EDITOR
using NUnit.Framework;

public class DeploymentEnvironmentPipelineTests
{
    [Test]
    public void DefaultRuntimeConfig_KeepsStageAndProdSeparated()
    {
        NetworkRuntimeConfigLoader.ResetCache();
        NetworkRuntimeConfigData config = NetworkRuntimeConfigLoader.Load();

        DeploymentEnvironmentValidationResult result = DeploymentEnvironmentPolicy.Validate(config);
        Assert.That(result.IsValid, Is.True, result.Message);
        Assert.That(config.ResolveSelectedEnvironmentId(), Is.EqualTo(DeploymentEnvironmentIds.Production));
        Assert.That(config.ResolveBaseUrl(), Is.EqualTo(ApiRoutes.BaseUrl));
        Assert.That(config.ResolveContentChannel(), Is.EqualTo(DeploymentEnvironmentPolicy.ProductionContentChannel));
    }

    [Test]
    public void DeploymentPresets_UseDifferentApiAndAddressablePaths()
    {
        DeploymentEnvironmentPreset stage = DeploymentEnvironmentPresets.Stage;
        DeploymentEnvironmentPreset prod = DeploymentEnvironmentPresets.Production;

        Assert.That(stage.BaseUrl, Is.Not.EqualTo(prod.BaseUrl));
        Assert.That(stage.AddressablesLoadPath, Is.Not.EqualTo(prod.AddressablesLoadPath));
        Assert.That(stage.ApplicationIdSuffix, Is.EqualTo(".stage"));
        Assert.That(prod.ApplicationIdSuffix, Is.Empty);
    }

    [Test]
    public void DeploymentFolders_RespectSevenScriptLimit()
    {
        AssertScriptLimit("Assets/_Project/Scripts/Runtime/Services/Deployment");
        AssertScriptLimit("Assets/_Project/Scripts/Editor/Deployment");
        AssertScriptLimit("Assets/_Project/Scripts/Editor/Deployment/Tests");
    }

    private static void AssertScriptLimit(string folder)
    {
        string[] scripts = System.IO.Directory.GetFiles(folder, "*.cs", System.IO.SearchOption.TopDirectoryOnly);
        Assert.That(scripts.Length, Is.LessThanOrEqualTo(7), folder);
    }
}
#endif
