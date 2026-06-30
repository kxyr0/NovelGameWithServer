using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

public static class StoryLoadingMediaAddressablesMigration
{
    private const string StoryRoot = "Assets/_MyProject/Data/Stories";
    private const string GroupName = "Story Loading Media";
    private const string SharedLabel = "story-loading-media";
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };
    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".m4v", ".webm" };
    private static readonly string[] GifExtensions = { ".gif", ".bytes" };

    [MenuItem("VN/Addressables/Migrate Story Loading Media", priority = 200)]
    public static void MigrateAllMenuGameData()
    {
        MigrationSummary summary = MigrateAll(overwriteExistingReferences: false);
        EditorUtility.DisplayDialog("Story Loading Media", summary.ToDialogText(), "OK");
    }

    [MenuItem("VN/Addressables/Force Remigrate Story Loading Media", priority = 201)]
    public static void ForceRemigrateAllMenuGameData()
    {
        MigrationSummary summary = MigrateAll(overwriteExistingReferences: true);
        EditorUtility.DisplayDialog("Story Loading Media", summary.ToDialogText(), "OK");
    }

    [MenuItem("VN/Addressables/Migrate Story Loading Media Strict Lazy", priority = 202)]
    public static void MigrateAllMenuGameDataStrictLazy()
    {
        MigrationSummary summary = MigrateAll(
            overwriteExistingReferences: false,
            clearDirectFallbacksAfterAddressableMigration: true);
        EditorUtility.DisplayDialog("Story Loading Media", summary.ToDialogText(), "OK");
    }

    public static void MigrateAllMenuGameDataFromCommandLine()
    {
        MigrationSummary summary = MigrateAll(overwriteExistingReferences: false);
        if (summary.ErrorCount > 0)
            EditorApplication.Exit(1);
    }

    public static void MigrateAllMenuGameDataStrictLazyFromCommandLine()
    {
        MigrationSummary summary = MigrateAll(
            overwriteExistingReferences: false,
            clearDirectFallbacksAfterAddressableMigration: true);
        if (summary.ErrorCount > 0)
            EditorApplication.Exit(1);
    }

    public static void ValidateStoryLoadingMediaSetupFromCommandLine()
    {
        bool valid = true;
        EditorSceneManager.OpenScene("Assets/_MyProject/Scenes/Game.unity");

        if (!IsSerializableAssetReferenceType(typeof(AssetReferenceVideoClip)))
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] AssetReferenceVideoClip must be [Serializable] for Unity inspector serialization.");
            valid = false;
        }

        if (!IsSerializableAssetReferenceType(typeof(AssetReferenceTextAsset)))
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] AssetReferenceTextAsset must be [Serializable] for Unity inspector serialization.");
            valid = false;
        }

        if (!typeof(IStoryLoadingMediaPolicy).IsAssignableFrom(typeof(StoryLoadingMediaPolicy)))
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] StoryLoadingMediaPolicy must implement IStoryLoadingMediaPolicy.");
            valid = false;
        }

        if (!typeof(IStoryLoadingMediaReadinessPolicy).IsAssignableFrom(typeof(StoryLoadingMediaReadinessPolicy)))
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] StoryLoadingMediaReadinessPolicy must implement IStoryLoadingMediaReadinessPolicy.");
            valid = false;
        }

        if (!typeof(IStoryLoadingMediaAssetLoader).IsAssignableFrom(typeof(AddressablesStoryLoadingMediaAssetLoader)))
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] AddressablesStoryLoadingMediaAssetLoader must implement IStoryLoadingMediaAssetLoader.");
            valid = false;
        }

        if (!typeof(IStoryStartAssetPreloadService).IsAssignableFrom(typeof(StoryStartAssetPreloadService)))
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] StoryStartAssetPreloadService must implement IStoryStartAssetPreloadService.");
            valid = false;
        }

        if (!typeof(IStoryStartPreloadAssetCollector).IsAssignableFrom(typeof(StoryStartPreloadAssetCollector)))
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] StoryStartPreloadAssetCollector must implement IStoryStartPreloadAssetCollector.");
            valid = false;
        }

        if (!typeof(IStoryStartChapterSelector).IsAssignableFrom(typeof(SavedOrFirstStoryStartChapterSelector)))
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] SavedOrFirstStoryStartChapterSelector must implement IStoryStartChapterSelector.");
            valid = false;
        }

        if (!typeof(IStoryStartVideoCoverLayoutPolicy).IsAssignableFrom(typeof(StoryStartVideoCoverLayoutPolicy)))
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] StoryStartVideoCoverLayoutPolicy must implement IStoryStartVideoCoverLayoutPolicy.");
            valid = false;
        }

        if (!typeof(IStoryStartLoadingFlow).IsAssignableFrom(typeof(StoryStartLoadingFlow)))
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] StoryStartLoadingFlow must implement IStoryStartLoadingFlow.");
            valid = false;
        }

        if (!typeof(IStoryStartLoadingScreen).IsAssignableFrom(typeof(StoryStartLoadingScreen)))
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] StoryStartLoadingScreen must implement IStoryStartLoadingScreen.");
            valid = false;
        }

        var scope = UnityEngine.Object.FindObjectOfType<NovelTemplateLifetimeScope>(true);
        var loadingScreen = UnityEngine.Object.FindObjectOfType<StoryStartLoadingScreen>(true);
        var menuController = UnityEngine.Object.FindObjectOfType<MenuController>(true);

        if (scope == null)
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] NovelTemplateLifetimeScope is missing in Game.unity.");
            valid = false;
        }

        if (loadingScreen == null)
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] StoryStartLoadingScreen is missing in Game.unity.");
            valid = false;
        }

        if (menuController == null)
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] MenuController is missing in Game.unity.");
            valid = false;
        }

        if (scope != null && loadingScreen != null && !HasAutoInjectTarget(scope, loadingScreen.gameObject))
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] NovelTemplateLifetimeScope does not auto-inject StoryStartLoadingScreen.");
            valid = false;
        }

        if (scope != null && menuController != null && !HasAutoInjectTarget(scope, menuController.gameObject))
        {
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] NovelTemplateLifetimeScope does not auto-inject MenuController.");
            valid = false;
        }

        int checkedGameData = 0;
        IStoryLoadingMediaReadinessPolicy readinessPolicy = StoryLoadingMediaReadinessPolicies.Shared;
        string[] roots = AssetDatabase.IsValidFolder(StoryRoot) ? new[] { StoryRoot } : new[] { "Assets" };
        string[] guids = AssetDatabase.FindAssets("t:GameData", roots);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameData data = AssetDatabase.LoadAssetAtPath<GameData>(path);
            if (data == null)
                continue;

            checkedGameData++;
            GameStoryLoadingMediaSettings loadingMedia = data.LoadingMedia;
            if (loadingMedia == null || !loadingMedia.ShouldUseCustomMedia)
            {
                Debug.LogError($"[StoryLoadingMediaAddressablesMigration] GameData '{path}' has no custom loading media.");
                valid = false;
            }

            StoryLoadingMediaReadinessReport readiness = readinessPolicy.Evaluate(data);
            if (readiness.Severity == StoryLoadingMediaReadinessSeverity.Error)
            {
                Debug.LogError($"[StoryLoadingMediaAddressablesMigration] GameData '{path}' loading media is invalid: {readiness.Message}");
                valid = false;
            }
            else if (readiness.BlocksStrictLazyLoading)
            {
                Debug.LogError($"[StoryLoadingMediaAddressablesMigration] GameData '{path}' loading media is not strict-lazy safe: {readiness.Message}");
                valid = false;
            }
            else if (readiness.Severity == StoryLoadingMediaReadinessSeverity.Warning)
            {
                Debug.LogWarning($"[StoryLoadingMediaAddressablesMigration] GameData '{path}' loading media warning: {readiness.Message}");
            }
        }

        Debug.Log($"[StoryLoadingMediaAddressablesMigration] Validation checked GameData: {checkedGameData}, valid: {valid}.");
        if (!valid)
            EditorApplication.Exit(1);
    }

    private static bool IsSerializableAssetReferenceType(Type type)
    {
        return type != null &&
            type.IsDefined(typeof(SerializableAttribute), false) &&
            typeof(UnityEngine.AddressableAssets.AssetReference).IsAssignableFrom(type);
    }

    public static MigrationSummary MigrateAll(bool overwriteExistingReferences)
    {
        return MigrateAll(
            overwriteExistingReferences,
            clearDirectFallbacksAfterAddressableMigration: false);
    }

    public static MigrationSummary MigrateAll(
        bool overwriteExistingReferences,
        bool clearDirectFallbacksAfterAddressableMigration)
    {
        var summary = new MigrationSummary();
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            summary.AddError("Cannot create or load Addressables settings.");
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] Cannot create or load Addressables settings.");
            return summary;
        }

        AddressableAssetGroup group = EnsureGroup(settings);
        string[] roots = AssetDatabase.IsValidFolder(StoryRoot) ? new[] { StoryRoot } : new[] { "Assets" };
        string[] guids = AssetDatabase.FindAssets("t:GameData", roots);

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameData data = AssetDatabase.LoadAssetAtPath<GameData>(path);
                if (data == null)
                    continue;

                summary.GameDataScanned++;
                MigrateGameData(
                    settings,
                    group,
                    data,
                    path,
                    overwriteExistingReferences,
                    clearDirectFallbacksAfterAddressableMigration,
                    summary);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log("[StoryLoadingMediaAddressablesMigration] " + summary.ToLogText());
        return summary;
    }

    public static MigrationSummary MigrateGameDataAsset(
        GameData data,
        bool overwriteExistingReferences,
        bool clearDirectFallbacksAfterAddressableMigration)
    {
        var summary = new MigrationSummary();
        if (data == null)
        {
            summary.AddError("Cannot migrate null GameData.");
            return summary;
        }

        string path = AssetDatabase.GetAssetPath(data);
        if (string.IsNullOrWhiteSpace(path))
        {
            summary.AddError($"GameData '{data.name}' is not a project asset.");
            return summary;
        }

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            summary.AddError("Cannot create or load Addressables settings.");
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] Cannot create or load Addressables settings.");
            return summary;
        }

        AddressableAssetGroup group = EnsureGroup(settings);
        summary.GameDataScanned = 1;

        AssetDatabase.StartAssetEditing();
        try
        {
            MigrateGameData(
                settings,
                group,
                data,
                path,
                overwriteExistingReferences,
                clearDirectFallbacksAfterAddressableMigration,
                summary);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log("[StoryLoadingMediaAddressablesMigration] " + summary.ToLogText());
        return summary;
    }

    private static void MigrateGameData(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        GameData data,
        string gameDataPath,
        bool overwriteExistingReferences,
        bool clearDirectFallbacksAfterAddressableMigration,
        MigrationSummary summary)
    {
        string storyId = ResolveStoryId(gameDataPath, data);
        bool entryChanged = false;
        GameStoryLoadingMediaSettings loadingMedia = data.EnsureLoadingMedia();
        Sprite menuImage = ResolveSpriteCandidate(gameDataPath, data.GameIcon);
        VideoClip menuVideo = ResolveVideoCandidate(gameDataPath, data.GameIconVideo);
        TextAsset menuGif = ResolveGifCandidate(gameDataPath, data.GameIconGif);
        Sprite loadingImage = loadingMedia.ResolveEditorImageCandidate(menuImage);
        VideoClip loadingVideo = loadingMedia.ResolveEditorVideoCandidate(menuVideo);
        TextAsset loadingGif = loadingMedia.ResolveEditorGifCandidate(menuGif);

        entryChanged |= RegisterAsset(settings, group, loadingImage, storyId, "image", summary);
        entryChanged |= RegisterAsset(settings, group, loadingVideo, storyId, "video", summary);
        entryChanged |= RegisterAsset(settings, group, loadingGif, storyId, "gif", summary);

        bool dataChanged = RepairLegacyMenuMediaReferences(data, menuImage, menuVideo, menuGif);
        dataChanged |= loadingMedia.ConfigureEditorAddressableMedia(
            loadingImage,
            loadingVideo,
            loadingGif,
            overwriteExistingReferences);
        if (clearDirectFallbacksAfterAddressableMigration)
        {
            bool cleared = loadingMedia.ClearEditorDirectFallbackMediaWithAddressableReferences();
            if (cleared)
            {
                dataChanged = true;
                summary.DirectFallbackReferencesCleared++;
            }
        }

        if (dataChanged)
        {
            EditorUtility.SetDirty(data);
            summary.GameDataUpdated++;
        }

        if (entryChanged)
            summary.AddressableEntriesChanged++;
    }

    private static bool RepairLegacyMenuMediaReferences(GameData data, Sprite image, VideoClip video, TextAsset gif)
    {
        if (data == null)
            return false;

        var serialized = new SerializedObject(data);
        bool changed = false;
        changed |= SetObjectIfMissing(serialized, "_gameIcon", image);
        changed |= SetObjectIfMissing(serialized, "_gameIconVideo", video);
        changed |= SetObjectIfMissing(serialized, "_gameIconGif", gif);

        if (changed)
            serialized.ApplyModifiedPropertiesWithoutUndo();

        return changed;
    }

    private static bool SetObjectIfMissing(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        if (value == null)
            return false;

        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue != null)
            return false;

        property.objectReferenceValue = value;
        return true;
    }

    private static bool HasAutoInjectTarget(NovelTemplateLifetimeScope scope, GameObject target)
    {
        if (scope == null || target == null)
            return false;

        var serialized = new SerializedObject(scope);
        SerializedProperty targets = serialized.FindProperty("autoInjectGameObjects");
        if (targets == null || !targets.isArray)
            return false;

        for (int i = 0; i < targets.arraySize; i++)
        {
            SerializedProperty element = targets.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == target)
                return true;
        }

        return false;
    }

    private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings)
    {
        AddressableAssetGroup group = settings.FindGroup(GroupName);
        if (group == null)
        {
            group = settings.CreateGroup(
                GroupName,
                false,
                false,
                true,
                null,
                typeof(ContentUpdateGroupSchema),
                typeof(BundledAssetGroupSchema));
        }

        BundledAssetGroupSchema bundledSchema = group.GetSchema<BundledAssetGroupSchema>();
        if (bundledSchema != null)
        {
            bundledSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundledSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            bundledSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;
            EditorUtility.SetDirty(bundledSchema);
        }

        settings.AddLabel(SharedLabel, false);
        EditorUtility.SetDirty(group);
        return group;
    }

    private static bool RegisterAsset(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        UnityEngine.Object asset,
        string storyId,
        string mediaKind,
        MigrationSummary summary)
    {
        if (asset == null)
            return false;

        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrWhiteSpace(path))
        {
            summary.AddWarning($"Asset '{asset.name}' has no AssetDatabase path.");
            return false;
        }

        string guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrWhiteSpace(guid))
        {
            summary.AddWarning($"Asset '{path}' has no GUID.");
            return false;
        }

        bool changed = false;
        AddressableAssetEntry entry = settings.FindAssetEntry(guid);
        if (entry == null)
        {
            entry = settings.CreateOrMoveEntry(guid, group, false, false);
            changed = true;
            summary.AddressableEntriesCreated++;
        }

        string storyLabel = "story-" + storyId;
        settings.AddLabel(storyLabel, false);

        string targetAddress = BuildAddress(storyId, mediaKind, path);
        if (CanOwnAddress(entry, path) && entry.address != targetAddress)
        {
            entry.address = targetAddress;
            changed = true;
        }

        changed |= entry.SetLabel(SharedLabel, true, true, false);
        changed |= entry.SetLabel(storyLabel, true, true, false);

        if (changed)
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, false, true);

        return changed;
    }

    private static Sprite ResolveSpriteCandidate(string gameDataPath, Sprite existing)
    {
        if (existing != null)
            return existing;

        return FindFirstMediaAsset(
            gameDataPath,
            ImageExtensions,
            LoadSpriteAtPath,
            "Posters/Small",
            "Posters/Big",
            "Posters");
    }

    private static VideoClip ResolveVideoCandidate(string gameDataPath, VideoClip existing)
    {
        if (existing != null)
            return existing;

        return FindFirstMediaAsset(
            gameDataPath,
            VideoExtensions,
            AssetDatabase.LoadAssetAtPath<VideoClip>,
            "Posters/Video/Small",
            "Posters/Video",
            "Posters/Video/Big");
    }

    private static TextAsset ResolveGifCandidate(string gameDataPath, TextAsset existing)
    {
        if (existing != null)
            return existing;

        return FindFirstMediaAsset(
            gameDataPath,
            GifExtensions,
            AssetDatabase.LoadAssetAtPath<TextAsset>,
            "Posters/Gif",
            "Posters/GIF",
            "Posters");
    }

    private static T FindFirstMediaAsset<T>(
        string gameDataPath,
        string[] extensions,
        Func<string, T> loadAsset,
        params string[] relativeFolders)
        where T : UnityEngine.Object
    {
        string menuFolder = Path.GetDirectoryName(gameDataPath);
        if (string.IsNullOrWhiteSpace(menuFolder))
            return null;

        menuFolder = menuFolder.Replace('\\', '/');
        for (int i = 0; i < relativeFolders.Length; i++)
        {
            string folder = menuFolder + "/" + relativeFolders[i];
            T asset = FindFirstAssetInFolder(folder, extensions, loadAsset);
            if (asset != null)
                return asset;
        }

        return null;
    }

    private static T FindFirstAssetInFolder<T>(string folder, string[] extensions, Func<string, T> loadAsset)
        where T : UnityEngine.Object
    {
        if (!AssetDatabase.IsValidFolder(folder) || !Directory.Exists(folder))
            return null;

        string[] files = Directory.GetFiles(folder);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i].Replace('\\', '/');
            if (!HasExtension(path, extensions))
                continue;

            T asset = loadAsset(path);
            if (asset != null)
                return asset;
        }

        return null;
    }

    private static Sprite LoadSpriteAtPath(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
            return sprite;

        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
        for (int i = 0; i < subAssets.Length; i++)
        {
            if (subAssets[i] is Sprite subSprite)
                return subSprite;
        }

        return null;
    }

    private static bool HasExtension(string path, string[] extensions)
    {
        string extension = Path.GetExtension(path);
        for (int i = 0; i < extensions.Length; i++)
        {
            if (string.Equals(extension, extensions[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool CanOwnAddress(AddressableAssetEntry entry, string assetPath)
    {
        return entry != null &&
            (string.IsNullOrWhiteSpace(entry.address) ||
             string.Equals(entry.address, assetPath, StringComparison.OrdinalIgnoreCase) ||
             entry.address.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildAddress(string storyId, string mediaKind, string assetPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        return "stories/" + storyId + "/loading/" + mediaKind + "/" + SanitizeAddressPart(fileName);
    }

    private static string ResolveStoryId(string gameDataPath, GameData data)
    {
        string normalized = gameDataPath.Replace('\\', '/');
        string[] parts = normalized.Split('/');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (string.Equals(parts[i], "Stories", StringComparison.OrdinalIgnoreCase))
                return SanitizeAddressPart(parts[i + 1]);
        }

        return SanitizeAddressPart(data != null ? data.name : "story");
    }

    private static string SanitizeAddressPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "story";

        value = value.Trim().ToLowerInvariant();
        var chars = new char[value.Length];
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            chars[i] = char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.'
                ? ch
                : '_';
        }

        return new string(chars);
    }

    public sealed class MigrationSummary
    {
        private readonly List<string> _warnings = new List<string>();
        private readonly List<string> _errors = new List<string>();

        public int GameDataScanned { get; set; }
        public int GameDataUpdated { get; set; }
        public int AddressableEntriesCreated { get; set; }
        public int AddressableEntriesChanged { get; set; }
        public int DirectFallbackReferencesCleared { get; set; }
        public int WarningCount => _warnings.Count;
        public int ErrorCount => _errors.Count;

        public void AddWarning(string warning)
        {
            _warnings.Add(warning);
            Debug.LogWarning("[StoryLoadingMediaAddressablesMigration] " + warning);
        }

        public void AddError(string error)
        {
            _errors.Add(error);
            Debug.LogError("[StoryLoadingMediaAddressablesMigration] " + error);
        }

        public string ToDialogText()
        {
            return ToLogText();
        }

        public string ToLogText()
        {
            return $"Scanned GameData: {GameDataScanned}, updated GameData: {GameDataUpdated}, " +
                $"created entries: {AddressableEntriesCreated}, changed entries: {AddressableEntriesChanged}, " +
                $"cleared direct fallback refs: {DirectFallbackReferencesCleared}, " +
                $"warnings: {WarningCount}, errors: {ErrorCount}.";
        }
    }
}
