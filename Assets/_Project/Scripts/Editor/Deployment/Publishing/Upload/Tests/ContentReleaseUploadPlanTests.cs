#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;

public sealed class ContentReleaseUploadPlanTests
{
    [Test]
    public void BuildFromManifest_MapsFilesToDestinationUrls()
    {
        var manifest = new ContentReleaseBuildManifest
        {
            buildTarget = "StandaloneWindows64",
            contentVersion = "2026.07.14.1",
            fileCount = 2,
            totalBytes = 30
        };
        manifest.files.Add(new ContentReleaseBuildManifestFile
        {
            path = "catalog.json",
            bytes = 10,
            sha256 = "hash1"
        });
        manifest.files.Add(new ContentReleaseBuildManifestFile
        {
            path = "bundles/a.bundle",
            bytes = 20,
            sha256 = "hash2"
        });

        ContentReleaseUploadPlan plan = ContentReleaseUploadPlanBuilder.BuildFromManifest(
            manifest,
            DeploymentEnvironmentPresets.Stage,
            "ServerData/stage/StandaloneWindows64");
        string expectedRoot = ContentReleaseUploadDestinationSettings.Resolve(
            DeploymentEnvironmentPresets.Stage,
            "StandaloneWindows64").publicLoadRootUrl.TrimEnd('/');

        Assert.AreEqual(2, plan.fileCount);
        Assert.AreEqual(ContentReleaseChannel.Stage, plan.channel);
        Assert.AreEqual(expectedRoot + "/catalog.json", plan.files[0].destinationUrl);
        Assert.IsFalse(string.IsNullOrWhiteSpace(plan.files[0].uploadTargetPath));
        Assert.That(
            plan.files[0].sourceAbsolutePath,
            Does.EndWith(Path.Combine("ServerData", "stage", "StandaloneWindows64", "catalog.json")));
        Assert.That(plan.ToMarkdown(), Does.Contain("Абсолютный локальный путь"));
        Assert.That(plan.ToMarkdown(), Does.Contain("bundles/a.bundle"));
    }

    [Test]
    public void ValidateManifestFiles_RejectsMissingFiles()
    {
        string sourceDirectory = Path.Combine("Library", "DeploymentUploadPlanTests", "missing");
        var manifest = new ContentReleaseBuildManifest
        {
            fileCount = 1,
            totalBytes = 10
        };
        manifest.files.Add(new ContentReleaseBuildManifestFile
        {
            path = "missing.bundle",
            bytes = 10,
            sha256 = "hash"
        });

        bool ok = ContentReleaseUploadPlanBuilder.ValidateManifestFiles(
            manifest,
            sourceDirectory,
            out string error);

        Assert.IsFalse(ok);
        Assert.That(error, Does.Contain("не найден"));
    }

    [Test]
    public void ValidateManifestFiles_RejectsEmptyManifest()
    {
        var manifest = new ContentReleaseBuildManifest { fileCount = 0, totalBytes = 0 };

        bool ok = ContentReleaseUploadPlanBuilder.ValidateManifestFiles(
            manifest,
            "Library/DeploymentUploadPlanTests/empty",
            out string error);

        Assert.IsFalse(ok);
        Assert.That(error, Does.Contain("нет файлов"));
    }

    [Test]
    public void ValidateManifestMetadata_RejectsWrongEnvironment()
    {
        var manifest = new ContentReleaseBuildManifest
        {
            environmentId = DeploymentEnvironmentIds.Production,
            channel = ContentReleaseChannel.Stage,
            buildTarget = "StandaloneWindows64"
        };

        bool ok = ContentReleaseUploadPlanBuilder.ValidateManifestMetadata(
            manifest,
            DeploymentEnvironmentPresets.Stage,
            "StandaloneWindows64",
            out string error);

        Assert.IsFalse(ok);
        Assert.That(error, Does.Contain("Среда"));
    }

    [Test]
    public void ValidateManifestFiles_RejectsHashMismatch()
    {
        string sourceDirectory = Path.Combine("Library", "DeploymentUploadPlanTests", "hash");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllBytes(Path.Combine(sourceDirectory, "bundle.bin"), new byte[] { 1, 2, 3, 4 });
        var manifest = new ContentReleaseBuildManifest { fileCount = 1, totalBytes = 4 };
        manifest.files.Add(new ContentReleaseBuildManifestFile
        {
            path = "bundle.bin",
            bytes = 4,
            sha256 = "wrong"
        });

        bool ok = ContentReleaseUploadPlanBuilder.ValidateManifestFiles(
            manifest,
            sourceDirectory,
            out string error);

        Assert.IsFalse(ok);
        Assert.That(error, Does.Contain("Хеш"));
    }
}
#endif
