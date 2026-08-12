#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

[Serializable]
public sealed class ContentReleaseBuildManifest
{
    public string generatedAtIso = "";
    public string environmentId = "";
    public string channel = "";
    public string buildTarget = "";
    public string contentVersion = "";
    public int fileCount;
    public long totalBytes;
    public List<ContentReleaseBuildManifestFile> files = new List<ContentReleaseBuildManifestFile>();
}

[Serializable]
public sealed class ContentReleaseBuildManifestFile
{
    public string path = "";
    public long bytes;
    public string sha256 = "";
}

public static class ContentReleaseManifestBuilder
{
    public const string ManifestFileName = "content-release-manifest.json";

    public static bool TryWrite(
        string environmentId,
        string contentVersion,
        out string manifestPath,
        out string manifestUrl,
        out string manifestHash,
        out string error)
    {
        manifestPath = "";
        manifestUrl = "";
        manifestHash = "";
        error = "";

        DeploymentEnvironmentPreset preset = ContentReleaseUploadDestinationSettings.ApplyToPreset(
            DeploymentEnvironmentPresets.Find(environmentId));
        string buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
        string buildDirectory = Path.GetFullPath(ResolveTokens(preset.AddressablesBuildPath, buildTarget));
        if (!Directory.Exists(buildDirectory))
            return Fail("Папка сборки Addressables не найдена. Сначала соберите Addressables.", out error);

        if (!TryBuildFromDirectory(buildDirectory, preset, buildTarget, contentVersion, out var manifest, out error))
            return false;

        manifestPath = Path.Combine(buildDirectory, ManifestFileName);
        File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, prettyPrint: true), Encoding.UTF8);
        manifestHash = HashFile(manifestPath);
        manifestUrl = ResolveTokens(preset.AddressablesLoadPath, buildTarget).TrimEnd('/') + "/" + ManifestFileName;
        return true;
    }

    public static bool TryBuildFromDirectory(
        string directory,
        DeploymentEnvironmentPreset preset,
        string buildTarget,
        string contentVersion,
        out ContentReleaseBuildManifest manifest,
        out string error)
    {
        manifest = null;
        error = "";
        if (preset == null)
            return Fail("Preset выкладки отсутствует.", out error);
        if (string.IsNullOrWhiteSpace(contentVersion))
            return Fail("Укажите версию контента.", out error);
        if (!Directory.Exists(directory))
            return Fail("Папка-источник для manifest не найдена.", out error);

        var files = new List<ContentReleaseBuildManifestFile>();
        long totalBytes = 0;
        foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == ManifestFileName)
                continue;

            var info = new FileInfo(file);
            totalBytes += info.Length;
            files.Add(new ContentReleaseBuildManifestFile
            {
                path = ToRelativePath(directory, file),
                bytes = info.Length,
                sha256 = HashFile(file)
            });
        }

        if (files.Count == 0)
            return Fail("В папке сборки релиза нет файлов.", out error);

        files.Sort((left, right) => string.CompareOrdinal(left.path, right.path));
        manifest = new ContentReleaseBuildManifest
        {
            generatedAtIso = DateTime.UtcNow.ToString("o"),
            environmentId = preset.EnvironmentId,
            channel = preset.ContentChannel,
            buildTarget = SaveDataSanitizer.SanitizeIdentifier(buildTarget),
            contentVersion = SaveDataSanitizer.SanitizeIdentifier(contentVersion),
            fileCount = files.Count,
            totalBytes = totalBytes,
            files = files
        };
        return true;
    }

    public static string ResolveTokens(string value, string buildTarget)
    {
        return (value ?? "").Replace("[BuildTarget]", buildTarget ?? "");
    }

    private static string ToRelativePath(string root, string file)
    {
        string relative = Path.GetFullPath(file).Substring(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).Length + 1);
        return relative.Replace('\\', '/');
    }

    private static string HashFile(string path)
    {
        using (var sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
#endif
