using System;
using System.IO;
using UnityEngine;

public static partial class GalleryImageSaver
{
    private const string AlbumName = "Nocturne";
    private const string LogPrefix = "[IMAGE_EXPORT]";

    public static bool TrySavePng(
        byte[] pngBytes,
        string suggestedName,
        out string savedPath,
        out string error)
    {
        savedPath = "";
        error = "";

        if (pngBytes == null || pngBytes.Length == 0)
        {
            error = "PNG-файл пустой.";
            Debug.LogWarning($"{LogPrefix}[FAILED] stage=input reason='{error}'");
            return false;
        }

        string fileName = BuildFileName(suggestedName);
        Debug.Log(
            $"{LogPrefix}[BEGIN] platform={Application.platform} file='{fileName}' bytes={pngBytes.Length}");

#if UNITY_ANDROID && !UNITY_EDITOR
        bool success = TrySaveAndroid(pngBytes, fileName, out savedPath, out error);
#else
        bool success = TrySaveDesktop(pngBytes, fileName, out savedPath, out error);
#endif

        if (success)
            Debug.Log($"{LogPrefix}[SUCCESS] platform={Application.platform} path='{savedPath}' bytes={pngBytes.Length}");
        else
            Debug.LogWarning($"{LogPrefix}[FAILED] platform={Application.platform} file='{fileName}' reason='{error}'");

        return success;
    }

    private static string BuildFileName(string suggestedName)
    {
        string raw = string.IsNullOrWhiteSpace(suggestedName)
            ? $"nocturne_cutscene_{DateTime.UtcNow:yyyyMMdd_HHmmss}"
            : suggestedName.Trim();

        foreach (char invalid in Path.GetInvalidFileNameChars())
            raw = raw.Replace(invalid, '_');

        return raw.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? raw
            : raw + ".png";
    }

    private static bool TrySaveDesktop(
        byte[] pngBytes,
        string fileName,
        out string savedPath,
        out string error)
    {
        savedPath = "";
        error = "";

        try
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrWhiteSpace(pictures))
                pictures = Application.persistentDataPath;

            string directory = Path.Combine(pictures, AlbumName);
            Directory.CreateDirectory(directory);
            savedPath = MakeUniquePath(directory, fileName);
            File.WriteAllBytes(savedPath, pngBytes);
            return true;
        }
        catch (Exception exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            return false;
        }
    }

    private static string MakeUniquePath(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            return path;

        string name = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);

        for (int i = 1; i < 1000; i++)
        {
            string candidate = Path.Combine(directory, $"{name}_{i}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(directory, $"{name}_{Guid.NewGuid():N}{extension}");
    }
}
