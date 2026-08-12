#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;

public static class NocturnalBuildPaths
{
    public static string BuildRoot(NocturnalBuildPanel panel)
    {
        string root = Path.IsPathRooted(panel.OutputRoot)
            ? panel.OutputRoot
            : Path.Combine(Directory.GetCurrentDirectory(), panel.OutputRoot);
        return Path.Combine(root, panel.Environment.ToString());
    }

    public static string AndroidFileName(NocturnalBuildPanel panel, string extension)
    {
        return Clean(PlayerSettings.productName) + "_" +
               Clean(panel.VersionName) + "_" +
               panel.AndroidVersionCode + "." + extension;
    }

    public static string IosProjectPath(NocturnalBuildPanel panel)
    {
        string folderName = Clean(PlayerSettings.productName) + "_" +
                            Clean(panel.VersionName) + "_" +
                            panel.IosBuildNumber + "_Xcode";
        string parent = Path.Combine(BuildRoot(panel), "iOS");
        string path = Path.Combine(parent, folderName);
        if (!Directory.Exists(path))
            return path;

        return path + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }

    public static string DesktopExecutableName(BuildTarget target)
    {
        string name = Clean(PlayerSettings.productName);
        if (target == BuildTarget.StandaloneWindows64)
            return name + ".exe";
        return target == BuildTarget.StandaloneOSX ? name + ".app" : name + ".x86_64";
    }

    private static string Clean(string value)
    {
        string cleaned = string.IsNullOrWhiteSpace(value) ? "Nocturnal" : value.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
            cleaned = cleaned.Replace(c, '_');
        return cleaned.Replace(' ', '_');
    }
}
#endif
