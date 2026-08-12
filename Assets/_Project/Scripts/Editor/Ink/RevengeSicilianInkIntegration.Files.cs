#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class RevengeSicilianInkIntegration
{
    static void ConfigureInkImporters()
    {
        SetCompileAsMaster(InkFolder + "/" + MasterFile, true);
        for (int i = 0; i < Episodes.Length; i++)
            SetCompileAsMaster(InkFolder + "/" + Episodes[i].SourceFile, false);
    }

    static void SetCompileAsMaster(string assetPath, bool value)
    {
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
            return;

        var serialized = new SerializedObject(importer);
        SerializedProperty property = serialized.FindProperty("compileAsMasterFileOverride");
        if (property == null || property.boolValue == value)
            return;

        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        importer.SaveAndReimport();
    }

    static void DeleteLegacyWrappers()
    {
        string[] legacy =
        {
            "MPS_Common.ink",
            "MPSs01e01_wrapper.ink",
            "MPSs01e02_wrapper.ink",
            "MPSs01e03_wrapper.ink"
        };
        for (int i = 0; i < legacy.Length; i++)
        {
            string assetPath = InkFolder + "/" + legacy[i];
            if (File.Exists(assetPath))
                AssetDatabase.DeleteAsset(assetPath);
        }
    }

    static List<string> CopySourceFiles(string sourceMaster, string sourceFolder)
    {
        var missing = new List<string>();
        string targetMaster = Path.Combine(InkFolder, MasterFile);
        CopyIfChanged(sourceMaster, targetMaster);
        if (!File.Exists(targetMaster))
            missing.Add("Месть по-сицилийски.ink");

        for (int i = 0; i < Episodes.Length; i++)
        {
            string source = !string.IsNullOrEmpty(sourceFolder) ? Path.Combine(sourceFolder, Episodes[i].SourceFile) : "";
            string target = Path.Combine(InkFolder, Episodes[i].SourceFile);
            CopyIfChanged(source, target);
            if (!File.Exists(target))
                missing.Add(Episodes[i].SourceFile);
        }
        return missing;
    }

    static void CopyIfChanged(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || !File.Exists(source))
            return;
        if (File.Exists(target) && FilesHaveSameContent(source, target))
            return;
        File.Copy(source, target, true);
    }

    static bool FilesHaveSameContent(string left, string right)
    {
        var leftInfo = new FileInfo(left);
        var rightInfo = new FileInfo(right);
        if (leftInfo.Length != rightInfo.Length)
            return false;

        byte[] leftBytes = File.ReadAllBytes(left);
        byte[] rightBytes = File.ReadAllBytes(right);
        for (int i = 0; i < leftBytes.Length; i++)
        {
            if (leftBytes[i] != rightBytes[i])
                return false;
        }
        return true;
    }

    static void ShowMissingFiles(List<string> missing)
    {
        string message = "Не найдены Ink-файлы:\n" + string.Join("\n", missing) +
                         "\n\nПоложи master и эпизоды рядом в Downloads или прямо в " + InkFolder + ".";
        EditorUtility.DisplayDialog("Ink интеграция", message, "OK");
        Debug.LogWarning("[AuthorInk] " + message);
    }

    static string FindSourceMaster()
    {
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(downloads))
            return "";

        foreach (string file in Directory.GetFiles(downloads, "*.ink"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (name.Contains("Месть") && name.Contains("сици"))
                return file;
        }
        return "";
    }

    static string EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return path;
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
        return path;
    }
}
#endif
