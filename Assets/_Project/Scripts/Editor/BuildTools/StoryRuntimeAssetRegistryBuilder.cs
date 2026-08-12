#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class StoryRuntimeAssetRegistryBuilder : IPreprocessBuildWithReport
{
    private const string StoryRoot = "Assets/Data/Stories";
    private const string ClothingRoot = "Assets/Data/Cloth";
    private const string RegistryRoot = "Assets/Resources/StoryRuntimeRegistry";
    private const string Prefix = "[STORY_BUILD_REGISTRY]";

    private static readonly Regex AssetPathRegex = new Regex(
        @"Assets/[^""\r\n]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public int callbackOrder => -2000;

    public void OnPreprocessBuild(BuildReport report)
    {
        RebuildAll();
    }

    [MenuItem("Nocturne/Story/Rebuild Runtime Asset Registries")]
    public static void RebuildAllMenu()
    {
        RebuildAll();
    }

    public static void RebuildAll()
    {
        if (!AssetDatabase.IsValidFolder(StoryRoot))
        {
            Debug.LogWarning($"{Prefix} Story root does not exist: '{StoryRoot}'.");
            return;
        }

        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "StoryRuntimeRegistry");

        string[] storyFolders = AssetDatabase.GetSubFolders(StoryRoot);
        int built = 0;
        for (int i = 0; i < storyFolders.Length; i++)
        {
            string storyFolder = storyFolders[i];
            string storyId = Path.GetFileName(storyFolder);
            if (string.IsNullOrWhiteSpace(storyId))
                continue;

            BuildStoryRegistry(storyId, storyFolder);
            built++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"{Prefix}[SUMMARY] registriesBuilt={built} target='{EditorUserBuildSettings.activeBuildTarget}'.");
    }

    private static void BuildStoryRegistry(string storyId, string storyFolder)
    {
        var entries = new List<StoryRuntimeAssetRegistry.Entry>();
        var pairKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueAssets = new HashSet<int>();
        var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int jsonCount = 0;
        int externalJsonReferences = 0;

        string[] storyGuids = AssetDatabase.FindAssets("", new[] { storyFolder });
        for (int i = 0; i < storyGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(storyGuids[i]);
            if (!ShouldIncludeStoryPath(storyFolder, path))
                continue;

            AddPathAssets(path, entries, pairKeys, uniqueAssets, scannedPaths);

            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                jsonCount++;
                TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (json != null)
                    externalJsonReferences += AddExplicitJsonAssetReferences(json.text, entries, pairKeys, uniqueAssets, scannedPaths);
            }
        }

        string clothingFolder = ClothingRoot + "/" + storyId;
        if (AssetDatabase.IsValidFolder(clothingFolder))
        {
            string[] clothingGuids = AssetDatabase.FindAssets("", new[] { clothingFolder });
            for (int i = 0; i < clothingGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(clothingGuids[i]);
                AddPathAssets(path, entries, pairKeys, uniqueAssets, scannedPaths);
            }
        }

        string assetPath = RegistryRoot + "/" + MakeAssetName(storyId) + ".asset";
        StoryRuntimeAssetRegistry registry = AssetDatabase.LoadAssetAtPath<StoryRuntimeAssetRegistry>(assetPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<StoryRuntimeAssetRegistry>();
            AssetDatabase.CreateAsset(registry, assetPath);
        }

        registry.EditorSetData(storyId, entries);
        EditorUtility.SetDirty(registry);

        Debug.Log(
            $"{Prefix}[STORY] storyId='{storyId}' jsonFiles={jsonCount} uniqueAssets={uniqueAssets.Count} " +
            $"aliases={entries.Count} externalJsonReferences={externalJsonReferences} registry='{assetPath}'.");
    }

    private static bool ShouldIncludeStoryPath(string storyFolder, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
            return false;

        if (path.IndexOf("/Menu/Posters/", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        if (path.IndexOf("/Ink/", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            return false;

        // JSON, generated chapter/graph assets, characters, backgrounds, audio,
        // cutscenes, stats, bindings, UI and wardrobe data are legitimate runtime story dependencies.
        return path.StartsWith(storyFolder + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static int AddExplicitJsonAssetReferences(
        string json,
        List<StoryRuntimeAssetRegistry.Entry> entries,
        HashSet<string> pairKeys,
        HashSet<int> uniqueAssets,
        HashSet<string> scannedPaths)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;

        int addedPaths = 0;
        MatchCollection matches = AssetPathRegex.Matches(json);
        for (int i = 0; i < matches.Count; i++)
        {
            string path = matches[i].Value
                .Replace("\\/", "/")
                .Replace("\\\\", "\\")
                .Trim();

            if (string.IsNullOrWhiteSpace(path))
                continue;

            if (AddPathAssets(path, entries, pairKeys, uniqueAssets, scannedPaths))
                addedPaths++;
            else
                Debug.LogWarning($"{Prefix}[MISSING_REFERENCE] jsonAssetPath='{path}'.");
        }

        return addedPaths;
    }

    private static bool AddPathAssets(
        string path,
        List<StoryRuntimeAssetRegistry.Entry> entries,
        HashSet<string> pairKeys,
        HashSet<int> uniqueAssets,
        HashSet<string> scannedPaths)
    {
        if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
            return false;

        if (!scannedPaths.Add(path))
            return true;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (assets == null || assets.Length == 0)
            return false;

        bool any = false;
        for (int i = 0; i < assets.Length; i++)
        {
            UnityEngine.Object asset = assets[i];
            if (asset == null || asset is MonoScript || asset is DefaultAsset)
                continue;

            any = true;
            uniqueAssets.Add(asset.GetInstanceID());
            AddAliasesForAsset(path, asset, entries, pairKeys);

            if (asset is StoryJsonAssetLibrary library)
            {
                AddJsonAssetLibraryAliases(library, entries, pairKeys, uniqueAssets);
                AddDependencies(path, entries, pairKeys, uniqueAssets, scannedPaths);
            }
        }

        return any;
    }

    private static void AddDependencies(
        string ownerPath,
        List<StoryRuntimeAssetRegistry.Entry> entries,
        HashSet<string> pairKeys,
        HashSet<int> uniqueAssets,
        HashSet<string> scannedPaths)
    {
        string[] dependencies = AssetDatabase.GetDependencies(ownerPath, true);
        if (dependencies == null)
            return;

        for (int i = 0; i < dependencies.Length; i++)
        {
            string dependencyPath = dependencies[i];
            if (string.Equals(dependencyPath, ownerPath, StringComparison.OrdinalIgnoreCase))
                continue;

            AddPathAssets(dependencyPath, entries, pairKeys, uniqueAssets, scannedPaths);
        }
    }

    private static void AddJsonAssetLibraryAliases(
        StoryJsonAssetLibrary library,
        List<StoryRuntimeAssetRegistry.Entry> entries,
        HashSet<string> pairKeys,
        HashSet<int> uniqueAssets)
    {
        if (library == null || library.Assets == null)
            return;

        IReadOnlyList<StoryJsonAssetReference> references = library.Assets;
        for (int i = 0; i < references.Count; i++)
        {
            StoryJsonAssetReference reference = references[i];
            if (reference == null || string.IsNullOrWhiteSpace(reference.Id))
                continue;

            AddLibraryAlias(reference.Id, reference.Character, entries, pairKeys, uniqueAssets);
            AddLibraryAlias(reference.Id, reference.Clothing, entries, pairKeys, uniqueAssets);
            AddLibraryAlias(reference.Id, reference.Sprite, entries, pairKeys, uniqueAssets);
            AddLibraryAlias(reference.Id, reference.Video, entries, pairKeys, uniqueAssets);
            AddLibraryAlias(reference.Id, reference.TextAsset, entries, pairKeys, uniqueAssets);
            AddLibraryAlias(reference.Id, reference.Audio, entries, pairKeys, uniqueAssets);
            AddLibraryAlias(reference.Id, reference.DialogueStyle, entries, pairKeys, uniqueAssets);
        }
    }

    private static void AddLibraryAlias(
        string id,
        UnityEngine.Object asset,
        List<StoryRuntimeAssetRegistry.Entry> entries,
        HashSet<string> pairKeys,
        HashSet<int> uniqueAssets)
    {
        if (asset == null)
            return;

        uniqueAssets.Add(asset.GetInstanceID());
        AddEntry(id, asset, entries, pairKeys);
    }

    private static void AddAliasesForAsset(
        string path,
        UnityEngine.Object asset,
        List<StoryRuntimeAssetRegistry.Entry> entries,
        HashSet<string> pairKeys)
    {
        AddEntry(path, asset, entries, pairKeys);
        AddEntry(path.Replace("Assets/", ""), asset, entries, pairKeys);
        AddEntry(path.Replace("Assets/Data/", ""), asset, entries, pairKeys);
        AddEntry(Path.GetFileName(path), asset, entries, pairKeys);
        AddEntry(Path.GetFileNameWithoutExtension(path), asset, entries, pairKeys);
        AddEntry(asset.name, asset, entries, pairKeys);

        string identifier = TryReadIdentifier(asset);
        if (!string.IsNullOrWhiteSpace(identifier))
            AddEntry(identifier, asset, entries, pairKeys);
    }

    private static string TryReadIdentifier(UnityEngine.Object asset)
    {
        if (asset == null)
            return "";

        try
        {
            Type type = asset.GetType();
            string[] names = { "id", "Id", "ID", "_id", "characterId", "statId" };
            for (int i = 0; i < names.Length; i++)
            {
                FieldInfo field = type.GetField(
                    names[i],
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(string))
                {
                    string value = field.GetValue(asset) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }

                PropertyInfo property = type.GetProperty(
                    names[i],
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
                {
                    string value = property.GetValue(asset, null) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{Prefix}[IDENTIFIER] asset='{asset.name}' error='{exception.Message}'.");
        }

        return "";
    }

    private static void AddEntry(
        string key,
        UnityEngine.Object asset,
        List<StoryRuntimeAssetRegistry.Entry> entries,
        HashSet<string> pairKeys)
    {
        if (asset == null || string.IsNullOrWhiteSpace(key))
            return;

        key = NormalizeKey(key);
        if (string.IsNullOrEmpty(key))
            return;

        string pair = key + "|" + asset.GetInstanceID();
        if (!pairKeys.Add(pair))
            return;

        entries.Add(new StoryRuntimeAssetRegistry.Entry
        {
            key = key,
            asset = asset
        });
    }

    private static string NormalizeKey(string value)
    {
        return (value ?? "")
            .Trim()
            .Trim('"')
            .Replace('\\', '/')
            .ToLowerInvariant();
    }

    private static string MakeAssetName(string storyId)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string result = storyId ?? "story";
        for (int i = 0; i < invalid.Length; i++)
            result = result.Replace(invalid[i], '_');
        return result.Replace('/', '_').Replace('\\', '_');
    }

    private static void EnsureFolder(string parent, string child)
    {
        string full = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(full))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
