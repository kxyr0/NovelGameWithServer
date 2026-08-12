#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;

public sealed class ContentReleasePublisherTests
{
    [Test]
    public void ContentReleasePolicy_SeparatesStageAndProduction()
    {
        var stage = BuildRelease(
            ContentReleaseStatus.Staging,
            ContentReleaseChannel.Stage,
            DeploymentEnvironmentPresets.Stage.AddressablesLoadPath);
        Assert.IsTrue(ContentReleasePolicy.Validate(stage).IsValid);

        stage.status = ContentReleaseStatus.Published;
        Assert.IsFalse(ContentReleasePolicy.Validate(stage).IsValid);

        var prod = BuildRelease(
            ContentReleaseStatus.Published,
            ContentReleaseChannel.Production,
            DeploymentEnvironmentPresets.Production.AddressablesLoadPath);
        Assert.IsTrue(ContentReleasePolicy.Validate(prod).IsValid);
    }

    [Test]
    public void PayloadBuilder_UsesPresetLoadPathAndChannel()
    {
        ContentReleaseDescriptor release = ContentReleasePayloadBuilder.Build(
            DeploymentEnvironmentIds.Stage,
            ContentReleaseStatus.Staging,
            "story_demo",
            "ep_01",
            "2026.07.14.1",
            "",
            "",
            "1.0.0",
            "");
        string expectedLoadPath = ContentReleaseUploadDestinationSettings.ApplyToPreset(
            DeploymentEnvironmentPresets.Stage).AddressablesLoadPath;

        Assert.AreEqual(ContentReleaseChannel.Stage, release.channel);
        Assert.AreEqual(expectedLoadPath, release.addressablesRemoteLoadPath);
    }

    [Test]
    public void AdminRoutes_AcceptOnlyContentReleasePaths()
    {
        Assert.IsTrue(ContentReleaseAdminRoutes.IsKnownPath(ContentReleaseAdminRoutes.BasePath));
        Assert.IsTrue(ContentReleaseAdminRoutes.IsKnownPath(ContentReleaseAdminRoutes.BuildFetchPath("story", "ep")));
        Assert.IsTrue(ContentReleaseAdminRoutes.IsKnownPath(ContentReleaseAdminRoutes.PromotePath));
        Assert.IsFalse(ContentReleaseAdminRoutes.IsKnownPath("/admin/users"));
        Assert.IsFalse(ContentReleaseAdminRoutes.IsKnownPath("https://example.com/admin/content/releases"));
    }

    [Test]
    public void ManifestBuilder_DescribesBuiltAddressablesFiles()
    {
        string root = Path.Combine("Temp", "ContentReleaseManifestTests");
        if (Directory.Exists(root))
            Directory.Delete(root, true);

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "catalog"));
            File.WriteAllText(Path.Combine(root, "catalog.json"), "{\"ok\":true}");
            File.WriteAllText(Path.Combine(root, "catalog", "bundle.bundle"), "bundle bytes");

            bool ok = ContentReleaseManifestBuilder.TryBuildFromDirectory(
                root,
                DeploymentEnvironmentPresets.Stage,
                "StandaloneWindows64",
                "2026.07.14.1",
                out ContentReleaseBuildManifest manifest,
                out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(2, manifest.fileCount);
            Assert.AreEqual(ContentReleaseChannel.Stage, manifest.channel);
            Assert.AreEqual("catalog.json", manifest.files[0].path);
            Assert.IsFalse(string.IsNullOrWhiteSpace(manifest.files[0].sha256));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public void NocturnalCommandBuilder_UsesCurrentRoutesAndEnvAdminKey()
    {
        string script = NocturnalServerCommandBuilder.BuildCurrentBackendPowerShell(
            "https://nocturnedc.ru/",
            "story_01",
            "Story",
            true,
            "season_01",
            "Season",
            1,
            "ep_01",
            "Episode",
            true,
            3,
            1,
            false,
            "Assets/episode.json");

        Assert.That(script, Does.Contain("$env:NOCTURNEDC_ADMIN_KEY"));
        Assert.That(script, Does.Contain("/admin/catalog"));
        Assert.That(script, Does.Contain("/admin/catalog/story"));
        Assert.That(script, Does.Contain("/admin/catalog/story/$storyId/season"));
        Assert.That(script, Does.Contain("/admin/catalog/season/$seasonId/episode"));
        Assert.That(script, Does.Contain("/admin/catalog/episode/$episodeId/content"));
        Assert.That(script, Does.Contain("/admin/catalog/episode/$episodeId/publish"));
        Assert.That(script, Does.Not.Contain("X-Admin-Key: secret"));

        string report = NocturnalServerCommandBuilder.BuildBackendHandoffReport(
            "https://nocturnedc.ru/",
            "story_01",
            "Story",
            true,
            "season_01",
            "Season",
            1,
            "ep_01",
            "Episode",
            true,
            3,
            1,
            false,
            DeploymentEnvironmentIds.Production,
            "2026.07.14.1",
            "Assets/episode.json");
        Assert.That(report, Does.Contain("# Передача по серверу Nocturnal"));
        Assert.That(report, Does.Contain("Версия контента: 2026.07.14.1"));
        Assert.That(report, Does.Contain("API-команды:"));
    }

    [Test]
    public void NocturnalRunbook_DocumentsSafeServerFlow()
    {
        const string path = "Assets/_Project/Docs/NocturnalServerRunbook.md";
        Assert.IsTrue(File.Exists(path), path);

        string text = File.ReadAllText(path);
        Assert.That(text, Does.Contain("Stage"));
        Assert.That(text, Does.Contain("Prod"));
        Assert.That(text, Does.Contain("Локальная проверка"));
        Assert.That(text, Does.Contain("План загрузки"));
        Assert.That(text, Does.Contain("/admin/catalog/episode/{episodeId}/content"));
    }

    [Test]
    public void DeploymentFolders_StayInsideScriptBudget()
    {
        AssertScriptBudget("Assets/_Project/Scripts/Runtime/Services/Deployment", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Runtime/Infrastructure/Addressables", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Editor/Deployment/Publishing", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Editor/Deployment/Publishing/MockServer", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Editor/Deployment/Publishing/Backend", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Editor/Deployment/Publishing/Media", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Editor/Deployment/Publishing/Upload", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Editor/Deployment/Readiness", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Editor/Tools/NocturnalServerTools", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Editor/Deployment/Tests", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Editor/Deployment/Publishing/Tests", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Editor/Deployment/Publishing/Backend/Tests", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Editor/Deployment/Publishing/MockServer/Tests", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Editor/Deployment/Publishing/Upload/Tests", 7, 200);
        AssertScriptBudget("Assets/_Project/Scripts/Editor/Deployment/Readiness/Tests", 7, 200);
    }

    private static ContentReleaseDescriptor BuildRelease(string status, string channel, string loadPath)
    {
        return new ContentReleaseDescriptor
        {
            storyId = "story_demo",
            episodeId = "ep_01",
            contentVersion = "2026.07.14.1",
            status = status,
            channel = channel,
            addressablesRemoteLoadPath = loadPath
        };
    }

    private static void AssertScriptBudget(string folder, int maxScripts, int maxLines)
    {
        Assert.IsTrue(Directory.Exists(folder), "Missing folder: " + folder);
        string[] scripts = Directory.GetFiles(folder, "*.cs", SearchOption.TopDirectoryOnly);
        Assert.LessOrEqual(scripts.Length, maxScripts, folder);

        foreach (string script in scripts)
        {
            int lines = File.ReadAllLines(script).Length;
            Assert.LessOrEqual(lines, maxLines, Path.GetFileName(script));
        }
    }
}
#endif
