#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ContentReleaseUploadPlanBuilder
{
    public const string OutputDirectory = "Library/DeploymentUpload";
    public const string JsonFileSuffix = "-upload-plan.json";
    public const string MarkdownFileSuffix = "-upload-plan.md";

    public static bool TryWrite(
        string environmentId,
        out string jsonPath,
        out string markdownPath,
        out string error)
    {
        jsonPath = "";
        markdownPath = "";
        error = "";

        if (!TryBuild(environmentId, out ContentReleaseUploadPlan plan, out error))
            return false;

        Directory.CreateDirectory(OutputDirectory);
        string name = plan.environmentId + "-" + plan.buildTarget;
        jsonPath = Path.Combine(OutputDirectory, name + JsonFileSuffix);
        markdownPath = Path.Combine(OutputDirectory, name + MarkdownFileSuffix);
        File.WriteAllText(jsonPath, JsonUtility.ToJson(plan, prettyPrint: true), Encoding.UTF8);
        File.WriteAllText(markdownPath, plan.ToMarkdown(), Encoding.UTF8);
        return true;
    }

    public static bool TryBuild(
        string environmentId,
        out ContentReleaseUploadPlan plan,
        out string error)
    {
        plan = null;
        error = "";

        DeploymentEnvironmentPreset preset = ContentReleaseUploadDestinationSettings.ApplyToPreset(
            DeploymentEnvironmentPresets.Find(environmentId));
        string buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
        string sourceDirectory = Path.GetFullPath(ContentReleaseManifestBuilder.ResolveTokens(
            preset.AddressablesBuildPath,
            buildTarget));
        string manifestPath = Path.Combine(sourceDirectory, ContentReleaseManifestBuilder.ManifestFileName);
        if (!File.Exists(manifestPath))
            return Fail("Manifest релиза не найден: " + manifestPath, out error);

        ContentReleaseBuildManifest manifest = JsonUtility.FromJson<ContentReleaseBuildManifest>(
            File.ReadAllText(manifestPath, Encoding.UTF8));
        if (manifest == null)
            return Fail("Manifest релиза не удалось прочитать.", out error);
        if (!ValidateManifestMetadata(manifest, preset, buildTarget, out error))
            return false;
        if (!ValidateManifestFiles(manifest, sourceDirectory, out error))
            return false;

        plan = BuildFromManifest(manifest, preset, sourceDirectory);
        return true;
    }

    public static bool ValidateManifestMetadata(
        ContentReleaseBuildManifest manifest,
        DeploymentEnvironmentPreset preset,
        string buildTarget,
        out string error)
    {
        error = "";
        if (manifest == null)
            return Fail("Manifest релиза отсутствует.", out error);
        if (preset == null)
            return Fail("Preset выкладки отсутствует.", out error);
        if (DeploymentEnvironmentIds.Normalize(manifest.environmentId) != DeploymentEnvironmentIds.Normalize(preset.EnvironmentId))
            return Fail("Среда в manifest не совпадает с выбранной средой.", out error);
        if (ContentReleaseChannel.Normalize(manifest.channel) != ContentReleaseChannel.Normalize(preset.ContentChannel))
            return Fail("Канал в manifest не совпадает с выбранной средой.", out error);
        if (!string.Equals(manifest.buildTarget, buildTarget, StringComparison.Ordinal))
            return Fail("Платформа сборки в manifest не совпадает с текущей платформой.", out error);
        return true;
    }

    public static bool ValidateManifestFiles(
        ContentReleaseBuildManifest manifest,
        string sourceDirectory,
        out string error)
    {
        error = "";
        if (manifest.files == null)
            return Fail("В manifest отсутствует список файлов.", out error);
        if (manifest.fileCount != manifest.files.Count)
            return Fail("Количество файлов в manifest не совпадает со списком.", out error);
        if (manifest.files.Count == 0)
            return Fail("В manifest нет файлов.", out error);

        long totalBytes = 0;
        string root = Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (int i = 0; i < manifest.files.Count; i++)
        {
            ContentReleaseBuildManifestFile file = manifest.files[i];
            if (string.IsNullOrWhiteSpace(file.path))
                return Fail("В manifest есть пустой путь файла.", out error);

            string path = Path.GetFullPath(Path.Combine(root, file.path.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return Fail("Путь в manifest выходит за папку сборки: " + file.path, out error);
            if (!File.Exists(path))
                return Fail("Файл релиза не найден: " + path, out error);

            long length = new FileInfo(path).Length;
            totalBytes += length;
            if (length != file.bytes)
                return Fail("Размер файла релиза изменился: " + file.path, out error);
            if (string.IsNullOrWhiteSpace(file.sha256) ||
                !HashFile(path).Equals(file.sha256, StringComparison.OrdinalIgnoreCase))
                return Fail("Хеш файла релиза изменился: " + file.path, out error);
        }

        return manifest.totalBytes == totalBytes ||
            Fail("Общий размер файлов в manifest не совпадает с файлами.", out error);
    }

    public static ContentReleaseUploadPlan BuildFromManifest(
        ContentReleaseBuildManifest manifest,
        DeploymentEnvironmentPreset preset,
        string sourceDirectory)
    {
        ContentReleaseUploadTarget target = ContentReleaseUploadDestinationSettings.Resolve(
            preset,
            manifest.buildTarget);
        string destinationRoot = target.publicLoadRootUrl.TrimEnd('/');
        string sourceRoot = Path.GetFullPath(sourceDirectory ?? "");
        var plan = new ContentReleaseUploadPlan
        {
            generatedAtIso = DateTime.UtcNow.ToString("o"),
            environmentId = preset.EnvironmentId,
            channel = preset.ContentChannel,
            buildTarget = manifest.buildTarget,
            contentVersion = manifest.contentVersion,
            sourceDirectory = sourceRoot,
            destinationRootUrl = destinationRoot,
            uploadMode = target.uploadMode,
            uploadRootPath = target.uploadRootPath,
            fileCount = manifest.fileCount,
            totalBytes = manifest.totalBytes
        };

        for (int i = 0; manifest.files != null && i < manifest.files.Count; i++)
        {
            ContentReleaseBuildManifestFile file = manifest.files[i];
            string absolutePath = Path.GetFullPath(Path.Combine(
                sourceRoot,
                file.path.Replace('/', Path.DirectorySeparatorChar)));
            plan.files.Add(new ContentReleaseUploadPlanFile
            {
                path = file.path,
                sourceAbsolutePath = absolutePath,
                bytes = file.bytes,
                sha256 = file.sha256,
                uploadTargetPath = target.UploadTargetFor(file.path),
                destinationUrl = target.PublicUrlFor(file.path)
            });
        }

        return plan;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }

    private static string HashFile(string path)
    {
        using (var sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}
#endif
