#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[Serializable]
public sealed class ContentReleaseUploadDestinationConfig
{
    public List<ContentReleaseUploadDestinationEntry> entries = new List<ContentReleaseUploadDestinationEntry>();
}

[Serializable]
public sealed class ContentReleaseUploadDestinationEntry
{
    public string environmentId = "";
    public string publicLoadRootUrl = "";
    public string uploadMode = ContentReleaseUploadDestinationSettings.ManualMode;
    public string uploadRootPath = "";
    public string notes = "";

    public ContentReleaseUploadDestinationEntry Clone()
    {
        return (ContentReleaseUploadDestinationEntry)MemberwiseClone();
    }
}

public sealed class ContentReleaseUploadTarget
{
    public string publicLoadRootUrl = "";
    public string uploadMode = "";
    public string uploadRootPath = "";

    public string PublicUrlFor(string relativePath)
    {
        return publicLoadRootUrl.TrimEnd('/') + "/" + Normalize(relativePath);
    }

    public string UploadTargetFor(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(uploadRootPath))
            return "НЕ УКАЗАНО: CDN/R2 ещё не подключён, заполните абсолютный путь загрузки в Nocturnal -> Выкладка.";

        string root = uploadRootPath.Trim();
        string file = Normalize(relativePath);
        if (root.Contains("://") || root.StartsWith("/"))
            return root.TrimEnd('/') + "/" + file;

        return Path.GetFullPath(Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string Normalize(string path)
    {
        return (path ?? "").Replace('\\', '/').TrimStart('/');
    }
}

public static class ContentReleaseUploadDestinationSettings
{
    public const string ManualMode = "manual";
    public const string FtpMode = "ftp";
    public const string SftpMode = "sftp";
    public const string LocalMode = "local";
    public const string ConfigPath = "Assets/_Project/Configs/Deployment/addressables-upload-destinations.json";

    public static ContentReleaseUploadDestinationEntry Get(string environmentId)
    {
        ContentReleaseUploadDestinationConfig config = Load();
        string id = DeploymentEnvironmentIds.Normalize(environmentId);
        for (int i = 0; i < config.entries.Count; i++)
            if (DeploymentEnvironmentIds.Normalize(config.entries[i].environmentId) == id)
                return config.entries[i].Clone();

        return CreateDefaultEntry(id);
    }

    public static void Save(ContentReleaseUploadDestinationEntry entry)
    {
        ContentReleaseUploadDestinationConfig config = Load();
        entry = NormalizeEntry(entry);
        string id = DeploymentEnvironmentIds.Normalize(entry.environmentId);
        for (int i = 0; i < config.entries.Count; i++)
        {
            if (DeploymentEnvironmentIds.Normalize(config.entries[i].environmentId) != id)
                continue;

            config.entries[i] = entry;
            Write(config);
            return;
        }

        config.entries.Add(entry);
        Write(config);
    }

    public static void Reset(string environmentId)
    {
        Save(CreateDefaultEntry(environmentId));
    }

    public static void RevealConfig()
    {
        if (!File.Exists(ConfigPath))
            Write(Load());
        EditorUtility.RevealInFinder(Path.GetFullPath(ConfigPath));
    }

    public static DeploymentEnvironmentPreset ApplyToPreset(DeploymentEnvironmentPreset preset)
    {
        ContentReleaseUploadDestinationEntry entry = Get(preset.EnvironmentId);
        return new DeploymentEnvironmentPreset
        {
            EnvironmentId = preset.EnvironmentId,
            DisplayName = preset.DisplayName,
            BaseUrl = preset.BaseUrl,
            ContentChannel = preset.ContentChannel,
            AddressablesProfileName = preset.AddressablesProfileName,
            AddressablesBuildPath = preset.AddressablesBuildPath,
            AddressablesLoadPath = FirstNonEmpty(entry.publicLoadRootUrl, preset.AddressablesLoadPath).TrimEnd('/'),
            ProductName = preset.ProductName,
            ApplicationIdSuffix = preset.ApplicationIdSuffix
        };
    }

    public static ContentReleaseUploadTarget Resolve(DeploymentEnvironmentPreset preset, string buildTarget)
    {
        ContentReleaseUploadDestinationEntry entry = Get(preset.EnvironmentId);
        return new ContentReleaseUploadTarget
        {
            publicLoadRootUrl = ResolveTokens(FirstNonEmpty(entry.publicLoadRootUrl, preset.AddressablesLoadPath), buildTarget),
            uploadMode = FirstNonEmpty(entry.uploadMode, ManualMode),
            uploadRootPath = ResolveTokens(entry.uploadRootPath, buildTarget)
        };
    }

    private static ContentReleaseUploadDestinationConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return CreateDefaultConfig();

        ContentReleaseUploadDestinationConfig config =
            JsonUtility.FromJson<ContentReleaseUploadDestinationConfig>(File.ReadAllText(ConfigPath));
        if (config == null || config.entries == null)
            return CreateDefaultConfig();
        return config;
    }

    private static void Write(ContentReleaseUploadDestinationConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
        File.WriteAllText(ConfigPath, JsonUtility.ToJson(config, prettyPrint: true));
        AssetDatabase.ImportAsset(ConfigPath);
    }

    private static ContentReleaseUploadDestinationConfig CreateDefaultConfig()
    {
        var config = new ContentReleaseUploadDestinationConfig();
        config.entries.Add(CreateDefaultEntry(DeploymentEnvironmentIds.Stage));
        config.entries.Add(CreateDefaultEntry(DeploymentEnvironmentIds.Production));
        return config;
    }

    private static ContentReleaseUploadDestinationEntry CreateDefaultEntry(string environmentId)
    {
        DeploymentEnvironmentPreset preset = DeploymentEnvironmentPresets.Find(environmentId);
        return new ContentReleaseUploadDestinationEntry
        {
            environmentId = preset.EnvironmentId,
            publicLoadRootUrl = preset.AddressablesLoadPath,
            uploadMode = ManualMode,
            uploadRootPath = "",
            notes = "CDN/R2 ещё не готов. Когда дадут бакет, публичный URL и путь загрузки, заполните uploadRootPath."
        };
    }

    private static ContentReleaseUploadDestinationEntry NormalizeEntry(ContentReleaseUploadDestinationEntry entry)
    {
        entry ??= new ContentReleaseUploadDestinationEntry();
        entry.environmentId = DeploymentEnvironmentIds.Normalize(entry.environmentId);
        entry.publicLoadRootUrl = (entry.publicLoadRootUrl ?? "").Trim().TrimEnd('/');
        entry.uploadMode = FirstNonEmpty(entry.uploadMode, ManualMode);
        entry.uploadRootPath = (entry.uploadRootPath ?? "").Trim();
        entry.notes ??= "";
        return entry;
    }

    private static string ResolveTokens(string value, string buildTarget)
    {
        return (value ?? "").Replace("[BuildTarget]", buildTarget ?? "");
    }

    private static string FirstNonEmpty(string first, string second)
    {
        return !string.IsNullOrWhiteSpace(first) ? first : second;
    }
}
#endif
