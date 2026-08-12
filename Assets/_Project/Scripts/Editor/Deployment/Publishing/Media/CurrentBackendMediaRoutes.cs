#if UNITY_EDITOR
using System;

public static class CurrentBackendMediaRoutes
{
    public const string MediaList = "/admin/media";
    public const string MediaUpload = "/admin/media/upload";

    public static string MediaDelete(string filename)
    {
        return "/admin/media/" + Uri.EscapeDataString(SanitizeFilename(filename));
    }

    public static string PublicMedia(string filename)
    {
        return "/media/" + Uri.EscapeDataString(SanitizeFilename(filename));
    }

    public static bool IsKnownPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        return path == MediaList || path == MediaUpload || path.StartsWith("/admin/media/", StringComparison.Ordinal);
    }

    public static string SanitizeFilename(string value)
    {
        value = (value ?? "").Replace('\\', '/').Trim();
        int slash = value.LastIndexOf('/');
        return slash >= 0 ? value.Substring(slash + 1) : value;
    }
}
#endif
