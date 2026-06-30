using System;
using System.Collections.Generic;

public sealed class SaveValidationResult
{
    public readonly bool IsValid;
    public readonly string ErrorType;
    public readonly string Message;

    SaveValidationResult(bool isValid, string errorType, string message)
    {
        IsValid = isValid;
        ErrorType = errorType ?? "";
        Message = message ?? "";
    }

    public static SaveValidationResult Ok()
    {
        return new SaveValidationResult(true, "", "");
    }

    public static SaveValidationResult Fail(string errorType, string message)
    {
        return new SaveValidationResult(false, errorType, message);
    }
}

public sealed class SaveValidator
{
    public SaveData SanitizeForWrite(SaveData data)
    {
        return SaveDataSanitizer.SanitizeCopy(data);
    }

    public SaveValidationResult ValidateForWrite(SaveData data)
    {
        SaveValidationResult structural = ValidateStructure(data, requirePosition: true, expectedStoryId: "");
        if (!structural.IsValid)
            return structural;

        if (string.IsNullOrEmpty(data.savedAtIso))
            data.savedAtIso = DateTime.UtcNow.ToString("o");

        return SaveValidationResult.Ok();
    }

    public SaveValidationResult ValidateLoaded(SaveData data, string expectedStoryId)
    {
        return ValidateStructure(data, requirePosition: true, expectedStoryId: expectedStoryId);
    }

    public SaveValidationResult ValidateSnapshot(SaveData data, string expectedStoryId)
    {
        return ValidateStructure(data, requirePosition: true, expectedStoryId: expectedStoryId);
    }

    public SaveValidationResult ValidateSerializedJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return SaveValidationResult.Fail("serialization_empty", "Serialized save payload is empty.");

        if (!SaveDataSanitizer.IsSerializedSizeAllowed(json))
            return SaveValidationResult.Fail("serialization_too_large", "Serialized save payload exceeds the configured size limit.");

        int version = NetworkJson.GetInt(json, "version", SaveData.CurrentVersion);
        if (version > SaveData.CurrentVersion)
            return SaveValidationResult.Fail("version_too_new", "Save schema version is newer than this client supports.");

        return SaveValidationResult.Ok();
    }

    SaveValidationResult ValidateStructure(SaveData data, bool requirePosition, string expectedStoryId)
    {
        if (data == null)
            return SaveValidationResult.Fail("null_data", "Save data is null.");

        if (data.version < 1)
            return SaveValidationResult.Fail("version_missing", "Save schema version is missing or invalid.");

        if (data.version > SaveData.CurrentVersion)
            return SaveValidationResult.Fail("version_too_new", "Save schema version is newer than this client supports.");

        if (requirePosition && !data.HasPosition)
            return SaveValidationResult.Fail("missing_position", "Save data has no restorable story/node position.");

        if (!string.IsNullOrEmpty(expectedStoryId) &&
            !string.IsNullOrEmpty(data.storyId) &&
            !string.Equals(data.storyId, expectedStoryId, StringComparison.Ordinal))
        {
            return SaveValidationResult.Fail("story_mismatch", "Save belongs to a different story.");
        }

        if (data.statKeys == null || data.statValues == null)
            return SaveValidationResult.Fail("stats_null", "Save stats collections are missing.");

        if (data.statKeys.Count != data.statValues.Count)
            return SaveValidationResult.Fail("stats_mismatch", "Save stat key/value collections have different sizes.");

        if (data.statKeys.Count > SaveDataSanitizer.MaxStatEntries)
            return SaveValidationResult.Fail("stats_too_many", "Save contains too many stat entries.");

        if (HasTooMany(data.history, SaveDataSanitizer.MaxHistoryEntries))
            return SaveValidationResult.Fail("history_too_large", "Save contains too many history entries.");

        if (HasTooMany(data.wardrobe, SaveDataSanitizer.MaxWardrobeEntries))
            return SaveValidationResult.Fail("wardrobe_too_large", "Save contains too many wardrobe entries.");

        if (HasTooMany(data.equippedClothes, SaveDataSanitizer.MaxEquippedEntries))
            return SaveValidationResult.Fail("equipped_too_large", "Save contains too many equipped clothing entries.");

        return SaveValidationResult.Ok();
    }

    static bool HasTooMany<T>(List<T> values, int max)
    {
        return values != null && values.Count > max;
    }
}

[Serializable]
public sealed class SaveMetadata
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public string saveType;
    public string source;
    public string operationId;
    public int slot;
    public string storyId;
    public string chapterId;
    public string episodeId;
    public string nodeGuid;
    public int saveVersion;
    public string savedAtIso;
    public string clientSavedAtIso;
    public string payloadChecksum;
    public string protectedPayloadChecksum;
    public long payloadChars;
    public long protectedPayloadChars;
}

public sealed class SaveOperationResult
{
    public bool Success;
    public string ErrorType;
    public string Message;
    public string Path;
    public string BackupPath;
    public string SnapshotPath;
    public bool Recovered;

    public static SaveOperationResult Ok(string path)
    {
        return new SaveOperationResult { Success = true, Path = path };
    }

    public static SaveOperationResult Fail(string path, string errorType, string message)
    {
        return new SaveOperationResult
        {
            Success = false,
            Path = path,
            ErrorType = errorType ?? "",
            Message = message ?? ""
        };
    }
}

public sealed class SaveLoadResult
{
    public bool Success;
    public SaveData Data;
    public string ErrorType;
    public string Message;
    public string Path;
    public string Source;
    public bool RecoveredFromBackup;
    public bool WasLegacy;
}
