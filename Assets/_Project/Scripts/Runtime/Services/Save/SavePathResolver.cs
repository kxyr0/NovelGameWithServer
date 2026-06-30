using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class SavePathResolver
{
    public const int MaxSaveSlot = 99;
    public const string SaveFilePrefix = "save_";
    public const string SaveFileExtension = ".json";
    public const string TempExtension = ".tmp";
    public const string BackupExtension = ".bak";
    public const string SecureMarkerExtension = ".secure";
    public const string MetadataExtension = ".meta.json";
    public const string SnapshotFolderName = "snapshots";

    readonly string _rootDirectory;

    public SavePathResolver(string rootDirectory)
    {
        _rootDirectory = NormalizeRoot(rootDirectory);
    }

    public static SavePathResolver ForPersistentData()
    {
        return new SavePathResolver(Application.persistentDataPath);
    }

    public string RootDirectory => _rootDirectory;

    public void EnsureRootDirectory()
    {
        Directory.CreateDirectory(_rootDirectory);
    }

    public string GetSavePath(int slot)
    {
        return Path.Combine(_rootDirectory, SaveFilePrefix + ClampSlot(slot) + SaveFileExtension);
    }

    public string GetStorySavePath(int slot, string storyId)
    {
        return Path.Combine(
            _rootDirectory,
            SaveFilePrefix + SafeFilePart(storyId) + "_" + ClampSlot(slot) + SaveFileExtension);
    }

    public string GetTempPath(string targetPath, string operationId)
    {
        string safeOperationId = SafeFilePart(operationId, "op", 32);
        return targetPath + "." + safeOperationId + TempExtension;
    }

    public string GetBackupPath(string targetPath)
    {
        return targetPath + BackupExtension;
    }

    public string GetSecureMarkerPath(string targetPath)
    {
        return targetPath + SecureMarkerExtension;
    }

    public string GetMetadataPath(string targetPath)
    {
        return targetPath + MetadataExtension;
    }

    public string GetSnapshotDirectory(string storyId)
    {
        return Path.Combine(_rootDirectory, SnapshotFolderName, SafeFilePart(storyId));
    }

    public string GetSnapshotRootDirectory()
    {
        return Path.Combine(_rootDirectory, SnapshotFolderName);
    }

    public string GetSnapshotPath(string storyId, int slot, string source, DateTime utcNow, string operationId)
    {
        string fileName =
            utcNow.ToUniversalTime().ToString("yyyyMMddTHHmmssfffZ") +
            "_slot-" + ClampSlot(slot) +
            "_" + SafeFilePart(source, "save", 32) +
            "_" + SafeFilePart(operationId, "op", 24) +
            SaveFileExtension;

        return Path.Combine(GetSnapshotDirectory(storyId), fileName);
    }

    public IEnumerable<string> EnumerateSaveFiles(bool includeTemp, bool includeBackups, bool includeMetadata)
    {
        if (!Directory.Exists(_rootDirectory))
            yield break;

        foreach (string path in Directory.GetFiles(_rootDirectory, SaveFilePrefix + "*" + SaveFileExtension))
        {
            if (IsPrimarySaveFile(path))
                yield return path;
        }

        if (includeTemp)
        {
            foreach (string path in Directory.GetFiles(_rootDirectory, SaveFilePrefix + "*" + SaveFileExtension + "*" + TempExtension))
                yield return path;
        }

        if (includeBackups)
        {
            foreach (string path in Directory.GetFiles(_rootDirectory, SaveFilePrefix + "*" + SaveFileExtension + BackupExtension))
                yield return path;
        }

        if (includeMetadata)
        {
            foreach (string path in Directory.GetFiles(_rootDirectory, SaveFilePrefix + "*" + SaveFileExtension + MetadataExtension))
                yield return path;

            foreach (string path in Directory.GetFiles(_rootDirectory, SaveFilePrefix + "*" + SaveFileExtension + BackupExtension + MetadataExtension))
                yield return path;

            foreach (string path in Directory.GetFiles(_rootDirectory, SaveFilePrefix + "*" + SaveFileExtension + SecureMarkerExtension))
                yield return path;

            foreach (string path in Directory.GetFiles(_rootDirectory, SaveFilePrefix + "*" + SaveFileExtension + BackupExtension + SecureMarkerExtension))
                yield return path;
        }
    }

    public bool IsPathInRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            string root = Path.GetFullPath(_rootDirectory);
            string fullPath = Path.GetFullPath(path);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) &&
                !root.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                root += Path.DirectorySeparatorChar;
            }

            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            ThrottledAppLogger.Warn(
                nameof(SavePathResolver) + ".IsPathInRoot",
                AppLogCategory.SaveSystem,
                nameof(SavePathResolver),
                nameof(IsPathInRoot),
                "[PATH][INVALID] Save path could not be validated against root.",
                LogMetadata.Of(
                    "file", SafeFileLabel(path),
                    "root", SafeFileLabel(_rootDirectory),
                    "errorType", exception.GetType().Name));
            return false;
        }
    }

    public static bool IsValidSlot(int slot)
    {
        return slot >= 0 && slot <= MaxSaveSlot;
    }

    public static int ClampSlot(int slot)
    {
        if (slot < 0)
            return 0;
        if (slot > MaxSaveSlot)
            return MaxSaveSlot;
        return slot;
    }

    public static string SafeFilePart(string value, string fallback = "default", int maxLength = 80)
    {
        return SaveDataSanitizer.SafeKeyPart(value, fallback, maxLength);
    }

    public static string SafeFileLabel(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "";

        try
        {
            return Path.GetFileName(path);
        }
        catch (Exception exception)
        {
            ThrottledAppLogger.Warn(
                nameof(SavePathResolver) + ".SafeFileLabel",
                AppLogCategory.SaveSystem,
                nameof(SavePathResolver),
                nameof(SafeFileLabel),
                "[PATH][LABEL_FAILED] Save path file name could not be resolved.",
                LogMetadata.Of("pathChars", path.Length, "errorType", exception.GetType().Name));
            return "";
        }
    }

    static string NormalizeRoot(string rootDirectory)
    {
        string root = string.IsNullOrWhiteSpace(rootDirectory)
            ? Application.persistentDataPath
            : rootDirectory.Trim();

        if (string.IsNullOrWhiteSpace(root))
            root = Directory.GetCurrentDirectory();

        return Path.GetFullPath(root);
    }

    static bool IsPrimarySaveFile(string path)
    {
        return !path.EndsWith(TempExtension, StringComparison.OrdinalIgnoreCase) &&
               !path.EndsWith(BackupExtension, StringComparison.OrdinalIgnoreCase) &&
               !path.EndsWith(SecureMarkerExtension, StringComparison.OrdinalIgnoreCase) &&
               !path.EndsWith(MetadataExtension, StringComparison.OrdinalIgnoreCase);
    }
}
