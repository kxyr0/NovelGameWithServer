#if UNITY_EDITOR
using System;
using System.IO;

public static class CurrentBackendMediaPolicy
{
    public const long MaxBytes = 50L * 1024L * 1024L;
    private static readonly string[] AllowedExtensions =
    {
        ".png", ".jpg", ".jpeg", ".webp", ".mp3", ".ogg", ".wav", ".mp4"
    };

    public static bool TryValidateUploadFile(string path, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(path))
            return Fail("Выберите файл картинки, аудио или видео.", out error);
        if (!File.Exists(path))
            return Fail("Файл не найден: " + path, out error);

        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (Array.IndexOf(AllowedExtensions, extension) < 0)
            return Fail("Формат не поддерживается: " + extension + ". Разрешены png, jpg, jpeg, webp, mp3, ogg, wav, mp4.", out error);
        if (new FileInfo(path).Length > MaxBytes)
            return Fail("Файл больше 50MB: " + path, out error);
        return true;
    }

    public static string ContentTypeFor(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".png": return "image/png";
            case ".jpg":
            case ".jpeg": return "image/jpeg";
            case ".webp": return "image/webp";
            case ".mp3": return "audio/mpeg";
            case ".ogg": return "audio/ogg";
            case ".wav": return "audio/wav";
            case ".mp4": return "video/mp4";
            default: return "application/octet-stream";
        }
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
#endif
