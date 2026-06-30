using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class HeroCustomizationState
{
    public string playerName;
    public AppearanceType appearance = AppearanceType.Default;
    public string outfitId;
    public string hairId;
    public string accessoryId;

    public bool HasOutfit => !string.IsNullOrWhiteSpace(outfitId);
    public bool HasHair => !string.IsNullOrWhiteSpace(hairId);
    public bool HasAccessory => !string.IsNullOrWhiteSpace(accessoryId);

    public static HeroCustomizationState CaptureCurrent()
    {
        return new HeroCustomizationState
        {
            playerName = PlayerAppearance.PlayerName,
            appearance = PlayerAppearance.CurrentAppearance,
            outfitId = PlayerAppearance.OutfitId,
            hairId = PlayerAppearance.HairId,
            accessoryId = PlayerAppearance.AccessoryId
        }.Normalized();
    }

    public static HeroCustomizationState FromSaveData(SaveData data)
    {
        if (data == null)
            return new HeroCustomizationState().Normalized();

        return new HeroCustomizationState
        {
            playerName = data.playerName,
            appearance = NormalizeAppearance((AppearanceType)data.appearance),
            outfitId = FirstNonEmpty(FindEquippedId(data.equippedClothes, "hero:outfit", "outfit"), data.heroOutfitId),
            hairId = FirstNonEmpty(FindEquippedId(data.equippedClothes, "hero:hair", "hair"), data.heroHairId),
            accessoryId = FirstNonEmpty(FindEquippedId(data.equippedClothes, "hero:accessory", "accessory"), data.heroAccessoryId)
        }.Normalized();
    }

    public HeroCustomizationState Normalized()
    {
        playerName = NormalizePlayerName(playerName);
        appearance = NormalizeAppearance(appearance);
        outfitId = NormalizeId(outfitId);
        hairId = NormalizeId(hairId);
        accessoryId = NormalizeId(accessoryId);
        return this;
    }

    public void WriteToSaveData(SaveData data)
    {
        if (data == null)
            return;

        Normalized();
        data.playerName = playerName;
        data.appearance = (int)appearance;
        data.heroOutfitId = outfitId;
        data.heroHairId = hairId;
        data.heroAccessoryId = accessoryId;
    }

    public static string NormalizePlayerName(string name)
    {
        string safeName = SaveDataSanitizer.SanitizePlayerName(name);
        if (string.IsNullOrWhiteSpace(safeName))
            return HeroCustomizationStore.DefaultPlayerName;

        return safeName.Length <= HeroCustomizationStore.MaxPlayerNameLength
            ? safeName
            : safeName.Substring(0, HeroCustomizationStore.MaxPlayerNameLength);
    }

    public static AppearanceType NormalizeAppearance(AppearanceType type)
    {
        return Enum.IsDefined(typeof(AppearanceType), type) ? type : AppearanceType.Default;
    }

    private static string NormalizeId(string value)
    {
        return SaveDataSanitizer.SanitizeIdentifier(value);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "";
    }

    private static string FindEquippedId(List<StringPair> equippedClothes, string preferredKey, string slotSuffix)
    {
        if (equippedClothes == null || string.IsNullOrWhiteSpace(slotSuffix))
            return "";

        if (!string.IsNullOrWhiteSpace(preferredKey))
        {
            foreach (StringPair pair in equippedClothes)
            {
                if (pair == null || string.IsNullOrWhiteSpace(pair.key) || string.IsNullOrWhiteSpace(pair.value))
                    continue;

                if (string.Equals(pair.key, preferredKey, StringComparison.OrdinalIgnoreCase))
                    return pair.value.Trim();
            }
        }

        foreach (StringPair pair in equippedClothes)
        {
            if (pair == null || string.IsNullOrWhiteSpace(pair.key) || string.IsNullOrWhiteSpace(pair.value))
                continue;

            if (string.Equals(pair.key, slotSuffix, StringComparison.OrdinalIgnoreCase))
                return pair.value.Trim();
        }

        return "";
    }
}

public static class HeroCustomizationStore
{
    public const string DefaultPlayerName = "\u0413\u0435\u0440\u043e\u0438\u043d\u044f";
    public const int MaxPlayerNameLength = 64;

    private const string AppearanceKey = "VN_APPEARANCE";
    private const string PlayerNameKey = "VN_PLAYER_NAME";
    private const string StoryPlayerNameKeyPrefix = "VN_PLAYER_NAME_STORY_";
    private const string StoryAppearanceKeyPrefix = "VN_APPEARANCE_STORY_";
    private const string OutfitKey = "VN_HERO_OUTFIT";
    private const string HairKey = "VN_HERO_HAIR";
    private const string AccessoryKey = "VN_HERO_ACCESSORY";
    private const long MaxStoreFileBytes = 64 * 1024L;
    private const string StoreFileName = "hero_customization.json";

    public static HeroCustomizationState Load()
    {
        try
        {
            HeroCustomizationState prefsState = LoadFromPlayerPrefs();
            HeroCustomizationState fileState = LoadFromFile();
            return MergeStoredStates(prefsState, fileState).Normalized();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to load state: {exception.Message}");
            return new HeroCustomizationState().Normalized();
        }
    }

    public static void Save(HeroCustomizationState state)
    {
        if (state == null)
            return;

        state.Normalized();
        try
        {
            LocalSecurePrefs.SetString(PlayerNameKey, GetPrefsPurpose("name"), state.playerName);
            LocalSecurePrefs.SetInt(AppearanceKey, GetPrefsPurpose("appearance"), (int)state.appearance);
            LocalSecurePrefs.SetString(OutfitKey, GetPrefsPurpose("outfit"), state.outfitId ?? "");
            LocalSecurePrefs.SetString(HairKey, GetPrefsPurpose("hair"), state.hairId ?? "");
            LocalSecurePrefs.SetString(AccessoryKey, GetPrefsPurpose("accessory"), state.accessoryId ?? "");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to save PlayerPrefs state: {exception.Message}");
        }

        try
        {
            SaveToFile(state);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to save file state: {exception.Message}");
        }
    }

    public static void DeleteStoredState()
    {
        try
        {
            LocalSecurePrefs.Delete(PlayerNameKey);
            LocalSecurePrefs.Delete(AppearanceKey);
            LocalSecurePrefs.Delete(OutfitKey);
            LocalSecurePrefs.Delete(HairKey);
            LocalSecurePrefs.Delete(AccessoryKey);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to clear PlayerPrefs state: {exception.Message}");
        }

        try
        {
            string path = GetStorePath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);

            if (!string.IsNullOrEmpty(path))
            {
                string tempPath = path + ".tmp";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                string markerPath = GetSecureMarkerPath(path);
                if (File.Exists(markerPath))
                    File.Delete(markerPath);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to delete file state: {exception.Message}");
        }
    }

    public static bool HasStoredPlayerName()
    {
        try
        {
            if (IsCustomPlayerName(LoadPrefsString(PlayerNameKey, "name", "")))
                return true;

            HeroCustomizationState fileState = LoadFromFile();
            return fileState != null && IsCustomPlayerName(fileState.playerName);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to check stored player name: {exception.Message}");
            return false;
        }
    }

    public static bool HasStoredPlayerNameForStory(string storyId)
    {
        string playerName;
        return TryLoadPlayerNameForStory(storyId, out playerName);
    }

    public static bool TryLoadPlayerNameForStory(string storyId, out string playerName)
    {
        playerName = "";
        string safeStoryId = NormalizeStoryId(storyId);
        if (string.IsNullOrEmpty(safeStoryId))
            return false;

        try
        {
            string loadedName = LocalSecurePrefs.GetString(
                GetStoryPlayerNameKey(safeStoryId),
                GetStoryPlayerNamePurpose(safeStoryId),
                "");

            loadedName = SaveDataSanitizer.SanitizePlayerName(loadedName);
            if (!IsCustomPlayerName(loadedName))
                return false;

            playerName = HeroCustomizationState.NormalizePlayerName(loadedName);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to load story player name '{safeStoryId}': {exception.Message}");
            return false;
        }
    }

    public static string LoadPlayerNameForStory(string storyId, string fallback = "")
    {
        return TryLoadPlayerNameForStory(storyId, out string playerName)
            ? playerName
            : fallback;
    }

    public static void SavePlayerNameForStory(string storyId, string name)
    {
        string safeStoryId = NormalizeStoryId(storyId);
        if (string.IsNullOrEmpty(safeStoryId))
            return;

        try
        {
            string safeName = HeroCustomizationState.NormalizePlayerName(name);
            if (!IsCustomPlayerName(safeName))
            {
                LocalSecurePrefs.Delete(GetStoryPlayerNameKey(safeStoryId));
                return;
            }

            LocalSecurePrefs.SetString(
                GetStoryPlayerNameKey(safeStoryId),
                GetStoryPlayerNamePurpose(safeStoryId),
                safeName);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to save story player name '{safeStoryId}': {exception.Message}");
        }
    }

    public static void DeletePlayerNameForStory(string storyId)
    {
        string safeStoryId = NormalizeStoryId(storyId);
        if (string.IsNullOrEmpty(safeStoryId))
            return;

        try
        {
            LocalSecurePrefs.Delete(GetStoryPlayerNameKey(safeStoryId));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to delete story player name '{safeStoryId}': {exception.Message}");
        }
    }

    public static bool TryLoadAppearanceForStory(string storyId, out AppearanceType appearance)
    {
        appearance = AppearanceType.Default;
        string safeStoryId = NormalizeStoryId(storyId);
        if (string.IsNullOrEmpty(safeStoryId))
            return false;

        try
        {
            appearance = HeroCustomizationState.NormalizeAppearance(
                (AppearanceType)LocalSecurePrefs.GetInt(
                    GetStoryAppearanceKey(safeStoryId),
                    GetStoryAppearancePurpose(safeStoryId),
                    (int)AppearanceType.Default));

            return appearance != AppearanceType.Default;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to load story appearance '{safeStoryId}': {exception.Message}");
            appearance = AppearanceType.Default;
            return false;
        }
    }

    public static void SaveAppearanceForStory(string storyId, AppearanceType appearance)
    {
        string safeStoryId = NormalizeStoryId(storyId);
        if (string.IsNullOrEmpty(safeStoryId))
            return;

        appearance = HeroCustomizationState.NormalizeAppearance(appearance);
        if (appearance == AppearanceType.Default)
            return;

        try
        {
            LocalSecurePrefs.SetInt(
                GetStoryAppearanceKey(safeStoryId),
                GetStoryAppearancePurpose(safeStoryId),
                (int)appearance);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to save story appearance '{safeStoryId}': {exception.Message}");
        }
    }

    public static void DeleteAppearanceForStory(string storyId)
    {
        string safeStoryId = NormalizeStoryId(storyId);
        if (string.IsNullOrEmpty(safeStoryId))
            return;

        try
        {
            LocalSecurePrefs.Delete(GetStoryAppearanceKey(safeStoryId));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to delete story appearance '{safeStoryId}': {exception.Message}");
        }
    }

    static HeroCustomizationState LoadFromPlayerPrefs()
    {
        return new HeroCustomizationState
        {
            playerName = LoadPrefsString(PlayerNameKey, "name", DefaultPlayerName),
            appearance = (AppearanceType)LocalSecurePrefs.GetInt(AppearanceKey, GetPrefsPurpose("appearance"), 0),
            outfitId = LoadPrefsString(OutfitKey, "outfit", ""),
            hairId = LoadPrefsString(HairKey, "hair", ""),
            accessoryId = LoadPrefsString(AccessoryKey, "accessory", "")
        }.Normalized();
    }

    static HeroCustomizationState MergeStoredStates(HeroCustomizationState prefsState, HeroCustomizationState fileState)
    {
        prefsState = prefsState != null ? prefsState.Normalized() : new HeroCustomizationState().Normalized();
        fileState = fileState != null ? fileState.Normalized() : null;

        if (fileState == null)
            return prefsState;

        if (!IsCustomPlayerName(fileState.playerName) && IsCustomPlayerName(prefsState.playerName))
            fileState.playerName = prefsState.playerName;

        if (fileState.appearance == AppearanceType.Default && prefsState.appearance != AppearanceType.Default)
            fileState.appearance = prefsState.appearance;

        if (string.IsNullOrEmpty(fileState.outfitId) && !string.IsNullOrEmpty(prefsState.outfitId))
            fileState.outfitId = prefsState.outfitId;

        if (string.IsNullOrEmpty(fileState.hairId) && !string.IsNullOrEmpty(prefsState.hairId))
            fileState.hairId = prefsState.hairId;

        if (string.IsNullOrEmpty(fileState.accessoryId) && !string.IsNullOrEmpty(prefsState.accessoryId))
            fileState.accessoryId = prefsState.accessoryId;

        return fileState;
    }

    public static bool IsCustomPlayerName(string value)
    {
        string name = SaveDataSanitizer.SanitizePlayerName(value);
        return !string.IsNullOrWhiteSpace(name) &&
               !string.Equals(name.Trim(), DefaultPlayerName, StringComparison.OrdinalIgnoreCase);
    }

    static HeroCustomizationState LoadFromFile()
    {
        string path = GetStorePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        var info = new FileInfo(path);
        if (info.Length > MaxStoreFileBytes)
        {
            Debug.LogWarning("HeroCustomizationStore: ignored oversized customization file.");
            return null;
        }

        string storedText = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(storedText) || storedText.Length > MaxStoreFileBytes)
            return null;

        try
        {
            if (!LocalSaveSecurity.TryUnprotectJson(
                    storedText,
                    LocalSaveSecurity.HeroCustomizationPurpose,
                    out string json,
                    out bool wasProtected))
            {
                Debug.LogWarning("HeroCustomizationStore: ignored customization file with invalid integrity.");
                return null;
            }

            if (!wasProtected && HasSecureMarkerFile(path))
            {
                Debug.LogWarning("HeroCustomizationStore: ignored downgraded customization file.");
                TryDeleteStoreFile(path);
                return null;
            }

            HeroCustomizationState state = JsonUtility.FromJson<HeroCustomizationState>(json)?.Normalized();
            if (state != null && !wasProtected)
                SaveToFile(state);
            else if (state != null)
                EnsureSecureMarkerFile(path);

            return state;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to parse customization file: {exception.Message}");
            return null;
        }
    }

    static void SaveToFile(HeroCustomizationState state)
    {
        string path = GetStorePath();
        if (string.IsNullOrEmpty(path))
            return;

        Directory.CreateDirectory(Application.persistentDataPath);
        string json = JsonUtility.ToJson(state.Normalized(), false);
        if (string.IsNullOrWhiteSpace(json) || json.Length > SaveDataSanitizer.MaxSerializedChars)
        {
            Debug.LogWarning("HeroCustomizationStore: refused to save oversized customization file.");
            return;
        }

        string protectedJson = LocalSaveSecurity.ProtectJson(json, LocalSaveSecurity.HeroCustomizationPurpose, true);
        if (string.IsNullOrEmpty(protectedJson) || protectedJson.Length > MaxStoreFileBytes)
        {
            Debug.LogWarning("HeroCustomizationStore: refused to save invalid protected customization file.");
            return;
        }

        WriteStoreFile(path, protectedJson);
        EnsureSecureMarkerFile(path);
    }

    static string GetStorePath()
    {
        string root = Application.persistentDataPath;
        if (string.IsNullOrEmpty(root))
            return "";

        return Path.Combine(root, StoreFileName);
    }

    static void WriteStoreFile(string path, string content)
    {
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content);

        try
        {
            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
        catch (PlatformNotSupportedException)
        {
            ReplaceStoreFileFallback(tempPath, path);
        }
        catch (IOException)
        {
            ReplaceStoreFileFallback(tempPath, path);
        }
        finally
        {
            TryDeleteStoreTempFile(tempPath);
        }
    }

    static void ReplaceStoreFileFallback(string tempPath, string path)
    {
        if (File.Exists(path))
            File.Delete(path);

        File.Move(tempPath, path);
    }

    static void TryDeleteStoreTempFile(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to delete temp file: {exception.Message}");
        }
    }

    static void EnsureSecureMarkerFile(string storePath)
    {
        if (string.IsNullOrEmpty(storePath))
            return;

        string markerPath = GetSecureMarkerPath(storePath);
        if (File.Exists(markerPath))
            return;

        try
        {
            File.WriteAllText(markerPath, "nocturne-local-secure-v1");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to write secure marker: {exception.Message}");
        }
    }

    static bool HasSecureMarkerFile(string storePath)
    {
        return !string.IsNullOrEmpty(storePath) && File.Exists(GetSecureMarkerPath(storePath));
    }

    static void TryDeleteStoreFile(string storePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(storePath) && File.Exists(storePath))
                File.Delete(storePath);

            string markerPath = GetSecureMarkerPath(storePath);
            if (!string.IsNullOrEmpty(markerPath) && File.Exists(markerPath))
                File.Delete(markerPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HeroCustomizationStore: failed to delete invalid customization file: {exception.Message}");
        }
    }

    static string GetSecureMarkerPath(string storePath)
    {
        return storePath + ".secure";
    }

    static string NormalizeStoryId(string storyId)
    {
        return SaveDataSanitizer.SanitizeIdentifier(storyId).ToLowerInvariant();
    }

    static string GetStoryPlayerNameKey(string safeStoryId)
    {
        return StoryPlayerNameKeyPrefix + safeStoryId;
    }

    static string GetStoryPlayerNamePurpose(string safeStoryId)
    {
        return "story_name_" + safeStoryId;
    }

    static string GetStoryAppearanceKey(string safeStoryId)
    {
        return StoryAppearanceKeyPrefix + safeStoryId;
    }

    static string GetStoryAppearancePurpose(string safeStoryId)
    {
        return "story_appearance_" + safeStoryId;
    }

    static string LoadPrefsString(string key, string purpose, string defaultValue)
    {
        return LocalSecurePrefs.GetString(key, GetPrefsPurpose(purpose), defaultValue);
    }

    static string GetPrefsPurpose(string purpose)
    {
        return LocalSaveSecurity.HeroCustomizationPurpose + ":prefs:" + SaveDataSanitizer.SanitizeIdentifier(purpose);
    }
}
