using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public sealed class SavePersistenceService
{
    const long MaxSaveFileBytes = SaveDataSanitizer.MaxSerializedChars * 4L;
    const int DefaultSnapshotRetention = 20;

    static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    readonly SavePathResolver _paths;
    readonly SaveValidator _validator;

    public SavePersistenceService(SavePathResolver paths = null, SaveValidator validator = null)
    {
        _paths = paths ?? SavePathResolver.ForPersistentData();
        _validator = validator ?? new SaveValidator();
    }

    public SavePathResolver Paths => _paths;

    public SaveOperationResult WriteSaveFile(string path, SaveData data, int slot, string saveType, string source)
    {
        string operationId = NewOperationId();
        long startedAt = AppDiagnostics.StartTimer();
        var metadata = BaseMetadata(operationId, slot, path, saveType, source, data);
        LogInfo(nameof(WriteSaveFile), "[SAVE][START] Writing save file.", metadata, null, operationId);

        try
        {
            if (!_paths.IsPathInRoot(path))
                return FailWrite(startedAt, path, "path_outside_root", "Save path is outside persistent data root.", metadata, operationId);

            SaveData safeData = _validator.SanitizeForWrite(data);
            SaveValidationResult validation = _validator.ValidateForWrite(safeData);
            if (!validation.IsValid)
                return FailWrite(startedAt, path, validation.ErrorType, validation.Message, metadata, operationId);

            string json = NetworkJson.ToSaveDataJson(safeData, false);
            validation = _validator.ValidateSerializedJson(json);
            if (!validation.IsValid)
                return FailWrite(startedAt, path, validation.ErrorType, validation.Message, metadata, operationId);

            string protectedJson = LocalSaveSecurity.ProtectJson(json, LocalSaveSecurity.SavePurpose, true);
            if (string.IsNullOrEmpty(protectedJson))
                return FailWrite(startedAt, path, "protect_failed", "Protected save payload is empty.", metadata, operationId);

            SaveMetadata saveMetadata = CreateMetadata(safeData, slot, saveType, source, operationId, json, protectedJson);
            bool wrote = WriteProtectedSaveText(path, protectedJson, saveMetadata, safeData.storyId, operationId, createBackup: true);
            if (!wrote)
                return FailWrite(startedAt, path, "write_failed", "Atomic save write failed.", metadata, operationId);

            long durationMs = AppDiagnostics.ElapsedMilliseconds(startedAt);
            LogInfo(
                nameof(WriteSaveFile),
                "[SAVE][SUCCESS] Save file written.",
                AddMetadata(metadata, "durationMs", durationMs),
                durationMs,
                operationId);

            return SaveOperationResult.Ok(path);
        }
        catch (Exception exception)
        {
            AppDiagnostics.LogOperationFailed(
                AppLogCategory.SaveSystem,
                nameof(SavePersistenceService),
                nameof(WriteSaveFile),
                "[SAVE][FAILURE] Save write failed.",
                startedAt,
                exception,
                AddMetadata(metadata, "errorType", exception.GetType().Name),
                recoverable: true);

            return SaveOperationResult.Fail(path, exception.GetType().Name, exception.Message);
        }
    }

    public SaveLoadResult LoadSaveFile(string path, string label, string expectedStoryId, int slot)
    {
        string operationId = NewOperationId();
        long startedAt = AppDiagnostics.StartTimer();
        var metadata = LogMetadata.Of(
            "operationId", operationId,
            "label", label,
            "slot", slot,
            "expectedStoryId", SaveDataSanitizer.SafeKeyPart(expectedStoryId, "any"),
            "file", SavePathResolver.SafeFileLabel(path));

        LogInfo(nameof(LoadSaveFile), "[LOAD][START] Loading save file.", metadata, null, operationId);

        CandidateResult primary = TryReadCandidate(path, expectedStoryId, operationId, "primary");
        if (primary.Success)
        {
            if (primary.WasLegacy)
                TryMigrateLegacy(path, primary.Data, slot, operationId);

            long durationMs = AppDiagnostics.ElapsedMilliseconds(startedAt);
            LogInfo(
                nameof(LoadSaveFile),
                "[LOAD][SUCCESS] Save loaded.",
                AddMetadata(metadata, "wasLegacy", primary.WasLegacy, "source", "primary", "durationMs", durationMs),
                durationMs,
                operationId);

            return new SaveLoadResult
            {
                Success = true,
                Data = primary.Data,
                Path = path,
                Source = "primary",
                WasLegacy = primary.WasLegacy
            };
        }

        LogWarn(
            nameof(LoadSaveFile),
            "[LOAD][PRIMARY_FAILED] Primary save is unavailable or invalid.",
            AddMetadata(metadata, "errorType", primary.ErrorType, "reason", primary.Message),
            AppDiagnostics.ElapsedMilliseconds(startedAt),
            operationId);

        string backupPath = _paths.GetBackupPath(path);
        CandidateResult backup = TryReadCandidate(backupPath, expectedStoryId, operationId, "backup");
        if (backup.Success)
        {
            bool recovered = TryRecoverPrimaryFromBackup(path, backupPath, operationId);
            long durationMs = AppDiagnostics.ElapsedMilliseconds(startedAt);
            LogInfo(
                nameof(LoadSaveFile),
                recovered
                    ? "[RECOVERY][SUCCESS] Backup save loaded and restored to primary path."
                    : "[RECOVERY][PARTIAL] Backup save loaded, but primary restore failed.",
                AddMetadata(metadata, "backupFile", SavePathResolver.SafeFileLabel(backupPath), "durationMs", durationMs),
                durationMs,
                operationId);

            return new SaveLoadResult
            {
                Success = true,
                Data = backup.Data,
                Path = backupPath,
                Source = "backup",
                RecoveredFromBackup = recovered,
                WasLegacy = backup.WasLegacy
            };
        }

        long failedDurationMs = AppDiagnostics.ElapsedMilliseconds(startedAt);
        LogWarn(
            nameof(LoadSaveFile),
            "[LOAD][FAILURE] No valid save candidate was found.",
            AddMetadata(
                metadata,
                "primaryErrorType", primary.ErrorType,
                "backupErrorType", backup.ErrorType,
                "durationMs", failedDurationMs),
            failedDurationMs,
            operationId);

        return new SaveLoadResult
        {
            Success = false,
            ErrorType = primary.ErrorType,
            Message = primary.Message,
            Path = path
        };
    }

    public SaveOperationResult CreateSnapshot(SaveData data, int slot, string source)
    {
        string operationId = NewOperationId();
        long startedAt = AppDiagnostics.StartTimer();
        SaveData safeData = _validator.SanitizeForWrite(data);
        var metadata = BaseMetadata(operationId, slot, "", "snapshot", source, safeData);

        try
        {
            SaveValidationResult validation = _validator.ValidateSnapshot(safeData, safeData != null ? safeData.storyId : "");
            if (!validation.IsValid)
                return FailWrite(startedAt, "", validation.ErrorType, validation.Message, metadata, operationId);

            string path = _paths.GetSnapshotPath(safeData.storyId, slot, source, DateTime.UtcNow, operationId);
            metadata["file"] = SavePathResolver.SafeFileLabel(path);
            metadata["snapshotDirectory"] = SavePathResolver.SafeFileLabel(Path.GetDirectoryName(path));
            LogInfo(nameof(CreateSnapshot), "[SNAPSHOT][START] Creating save snapshot.", metadata, null, operationId);

            string json = NetworkJson.ToSaveDataJson(safeData, false);
            validation = _validator.ValidateSerializedJson(json);
            if (!validation.IsValid)
                return FailWrite(startedAt, path, validation.ErrorType, validation.Message, metadata, operationId);

            string protectedJson = LocalSaveSecurity.ProtectJson(json, LocalSaveSecurity.SavePurpose, true);
            if (string.IsNullOrEmpty(protectedJson))
                return FailWrite(startedAt, path, "protect_failed", "Protected snapshot payload is empty.", metadata, operationId);

            SaveMetadata snapshotMetadata = CreateMetadata(safeData, slot, "snapshot", source, operationId, json, protectedJson);
            bool wrote = WriteProtectedSaveText(path, protectedJson, snapshotMetadata, safeData.storyId, operationId, createBackup: false);
            if (!wrote)
                return FailWrite(startedAt, path, "write_failed", "Snapshot write failed.", metadata, operationId);

            ApplySnapshotRetention(_paths.GetSnapshotDirectory(safeData.storyId), DefaultSnapshotRetention, operationId);

            long durationMs = AppDiagnostics.ElapsedMilliseconds(startedAt);
            LogInfo(
                nameof(CreateSnapshot),
                "[SNAPSHOT][SUCCESS] Save snapshot created.",
                AddMetadata(metadata, "durationMs", durationMs),
                durationMs,
                operationId);

            var result = SaveOperationResult.Ok(path);
            result.SnapshotPath = path;
            return result;
        }
        catch (Exception exception)
        {
            AppDiagnostics.LogOperationFailed(
                AppLogCategory.SaveSystem,
                nameof(SavePersistenceService),
                nameof(CreateSnapshot),
                "[SNAPSHOT][FAILURE] Snapshot creation failed.",
                startedAt,
                exception,
                AddMetadata(metadata, "errorType", exception.GetType().Name),
                recoverable: true);

            return SaveOperationResult.Fail("", exception.GetType().Name, exception.Message);
        }
    }

    public void DeleteFileSet(string path)
    {
        string operationId = NewOperationId();
        long startedAt = AppDiagnostics.StartTimer();
        var metadata = LogMetadata.Of("operationId", operationId, "file", SavePathResolver.SafeFileLabel(path));

        if (string.IsNullOrEmpty(path) || !_paths.IsPathInRoot(path))
            return;

        try
        {
            bool existed = File.Exists(path);
            TryDeleteFile(path, operationId);
            TryDeleteFile(_paths.GetBackupPath(path), operationId);
            TryDeleteFile(_paths.GetTempPath(path, operationId), operationId);
            TryDeleteFile(_paths.GetSecureMarkerPath(path), operationId);
            TryDeleteFile(_paths.GetSecureMarkerPath(_paths.GetBackupPath(path)), operationId);
            TryDeleteFile(_paths.GetMetadataPath(path), operationId);
            TryDeleteFile(_paths.GetMetadataPath(_paths.GetBackupPath(path)), operationId);

            AppDiagnostics.LogOperationCompleted(
                AppLogCategory.SaveSystem,
                nameof(SavePersistenceService),
                nameof(DeleteFileSet),
                "[SAVE][DELETE_OK] Save file set deleted.",
                startedAt,
                AddMetadata(metadata, "existed", existed));
        }
        catch (Exception exception)
        {
            AppDiagnostics.LogOperationFailed(
                AppLogCategory.SaveSystem,
                nameof(SavePersistenceService),
                nameof(DeleteFileSet),
                "[SAVE][DELETE_FAILED] Save delete failed.",
                startedAt,
                exception,
                AddMetadata(metadata, "errorType", exception.GetType().Name),
                recoverable: true);
        }
    }

    bool WriteProtectedSaveText(
        string path,
        string protectedJson,
        SaveMetadata metadata,
        string expectedStoryId,
        string operationId,
        bool createBackup)
    {
        if (!_paths.IsPathInRoot(path))
            return false;

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string tempPath = _paths.GetTempPath(path, operationId);
        File.WriteAllText(tempPath, protectedJson, Utf8NoBom);
        LogInfo(
            nameof(WriteProtectedSaveText),
            "[SAVE][TEMP_WRITE_OK] Temporary save file written.",
            LogMetadata.Of(
                "operationId", operationId,
                "tempFile", SavePathResolver.SafeFileLabel(tempPath),
                "targetFile", SavePathResolver.SafeFileLabel(path),
                "chars", protectedJson != null ? protectedJson.Length : 0),
            null,
            operationId);

        CandidateResult tempValidation = TryReadCandidate(tempPath, expectedStoryId, operationId, "temp");
        if (!tempValidation.Success)
        {
            TryDeleteFile(tempPath, operationId);
            LogWarn(
                nameof(WriteProtectedSaveText),
                "[VALIDATION][TEMP_FAILED] Temporary save failed validation.",
                LogMetadata.Of(
                    "operationId", operationId,
                    "targetFile", SavePathResolver.SafeFileLabel(path),
                    "errorType", tempValidation.ErrorType,
                    "reason", tempValidation.Message),
                null,
                operationId);
            return false;
        }

        if (createBackup)
            TryCreateKnownGoodBackup(path, expectedStoryId, operationId);

        ReplaceFile(tempPath, path);
        EnsureSecureMarkerFile(path);
        WriteMetadata(path, metadata, operationId);

        return true;
    }

    CandidateResult TryReadCandidate(string path, string expectedStoryId, string operationId, string candidateType)
    {
        if (string.IsNullOrEmpty(path) || !_paths.IsPathInRoot(path))
            return CandidateResult.Fail("path_invalid", "Save path is invalid or outside root.");

        if (!File.Exists(path))
            return CandidateResult.Fail("missing", "Save file does not exist.");

        try
        {
            FileInfo fileInfo = new FileInfo(path);
            if (fileInfo.Length <= 0)
                return CandidateResult.Fail("file_empty", "Save file is empty.");

            if (fileInfo.Length > MaxSaveFileBytes)
                return CandidateResult.Fail("file_too_large", "Save file exceeds the configured size limit.");

            string storedText = File.ReadAllText(path, Utf8NoBom);
            SaveMetadata metadata = TryReadMetadata(path, operationId);
            if (metadata != null && !string.IsNullOrEmpty(metadata.protectedPayloadChecksum))
            {
                string protectedChecksum = ComputeSha256(storedText);
                if (!FixedEquals(protectedChecksum, metadata.protectedPayloadChecksum))
                    return CandidateResult.Fail("checksum_mismatch", "Protected save payload checksum does not match metadata.");
            }

            if (!LocalSaveSecurity.TryUnprotectJson(
                    storedText,
                    LocalSaveSecurity.SavePurpose,
                    out string json,
                    out bool wasProtected))
            {
                return CandidateResult.Fail("integrity_failed", "Save payload failed secure integrity validation.");
            }

            if (!wasProtected && HasSecureMarkerFile(path))
                return CandidateResult.Fail("downgrade_detected", "Raw save payload was found for a path marked as secure.");

            SaveValidationResult serializedValidation = _validator.ValidateSerializedJson(json);
            if (!serializedValidation.IsValid)
                return CandidateResult.Fail(serializedValidation.ErrorType, serializedValidation.Message);

            if (metadata != null && !string.IsNullOrEmpty(metadata.payloadChecksum))
            {
                string payloadChecksum = ComputeSha256(json);
                if (!FixedEquals(payloadChecksum, metadata.payloadChecksum))
                    return CandidateResult.Fail("payload_checksum_mismatch", "Plain save payload checksum does not match metadata.");
            }

            SaveData data = NetworkJson.FromSaveDataJson(json);
            SaveValidationResult dataValidation = _validator.ValidateLoaded(data, expectedStoryId);
            if (!dataValidation.IsValid)
                return CandidateResult.Fail(dataValidation.ErrorType, dataValidation.Message);

            LogInfo(
                nameof(TryReadCandidate),
                "[VALIDATION][OK] Save candidate validated.",
                LogMetadata.Of(
                    "operationId", operationId,
                    "candidate", candidateType,
                    "file", SavePathResolver.SafeFileLabel(path),
                    "wasProtected", wasProtected,
                    "version", data.version,
                    "storyId", data.storyId,
                    "nodeGuid", data.currentNodeGuid),
                null,
                operationId);

            return CandidateResult.Ok(data, wasProtected: wasProtected, wasLegacy: !wasProtected);
        }
        catch (Exception exception)
        {
            return CandidateResult.Fail(exception.GetType().Name, exception.Message);
        }
    }

    void TryCreateKnownGoodBackup(string path, string expectedStoryId, string operationId)
    {
        if (!File.Exists(path))
            return;

        CandidateResult existing = TryReadCandidate(path, expectedStoryId, operationId, "existing-before-replace");
        if (!existing.Success)
        {
            LogWarn(
                nameof(TryCreateKnownGoodBackup),
                "[BACKUP][SKIPPED] Existing save was not valid, backup was left unchanged.",
                LogMetadata.Of(
                    "operationId", operationId,
                    "file", SavePathResolver.SafeFileLabel(path),
                    "errorType", existing.ErrorType,
                    "reason", existing.Message),
                null,
                operationId);
            return;
        }

        string backupPath = _paths.GetBackupPath(path);
        File.Copy(path, backupPath, true);
        CopyIfExists(_paths.GetSecureMarkerPath(path), _paths.GetSecureMarkerPath(backupPath));
        CopyIfExists(_paths.GetMetadataPath(path), _paths.GetMetadataPath(backupPath));

        LogInfo(
            nameof(TryCreateKnownGoodBackup),
            "[BACKUP][CREATE_OK] Previous known-good save copied to backup.",
            LogMetadata.Of(
                "operationId", operationId,
                "file", SavePathResolver.SafeFileLabel(path),
                "backupFile", SavePathResolver.SafeFileLabel(backupPath)),
            null,
            operationId);
    }

    bool TryRecoverPrimaryFromBackup(string path, string backupPath, string operationId)
    {
        try
        {
            if (!File.Exists(backupPath) || !_paths.IsPathInRoot(path) || !_paths.IsPathInRoot(backupPath))
                return false;

            string tempPath = _paths.GetTempPath(path, operationId + "_recovery");
            File.Copy(backupPath, tempPath, true);
            ReplaceFile(tempPath, path);
            CopyIfExists(_paths.GetSecureMarkerPath(backupPath), _paths.GetSecureMarkerPath(path));
            CopyIfExists(_paths.GetMetadataPath(backupPath), _paths.GetMetadataPath(path));
            EnsureSecureMarkerFile(path);
            return true;
        }
        catch (Exception exception)
        {
            LogWarn(
                nameof(TryRecoverPrimaryFromBackup),
                "[RECOVERY][RESTORE_FAILED] Backup recovery copy failed.",
                LogMetadata.Of(
                    "operationId", operationId,
                    "file", SavePathResolver.SafeFileLabel(path),
                    "backupFile", SavePathResolver.SafeFileLabel(backupPath),
                    "errorType", exception.GetType().Name,
                    "error", exception.Message),
                null,
                operationId);
            return false;
        }
    }

    void TryMigrateLegacy(string path, SaveData data, int slot, string parentOperationId)
    {
        LogInfo(
            nameof(TryMigrateLegacy),
            "[MIGRATION][START] Rewriting legacy raw save as protected save.",
            LogMetadata.Of(
                "operationId", parentOperationId,
                "file", SavePathResolver.SafeFileLabel(path),
                "slot", slot,
                "storyId", data != null ? data.storyId : ""),
            null,
            parentOperationId);

        SaveOperationResult result = WriteSaveFile(path, data, slot, "migration", "legacy-load");
        if (result.Success)
        {
            LogInfo(
                nameof(TryMigrateLegacy),
                "[MIGRATION][SUCCESS] Legacy save migrated.",
                LogMetadata.Of("operationId", parentOperationId, "file", SavePathResolver.SafeFileLabel(path)),
                null,
                parentOperationId);
        }
    }

    void ApplySnapshotRetention(string directory, int maxSnapshots, string operationId)
    {
        if (maxSnapshots <= 0 || string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;

        string[] files = Directory.GetFiles(directory, "*" + SavePathResolver.SaveFileExtension);
        List<string> snapshots = new List<string>();
        foreach (string file in files)
        {
            if (file.EndsWith(SavePathResolver.MetadataExtension, StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(SavePathResolver.BackupExtension, StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(SavePathResolver.TempExtension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            snapshots.Add(file);
        }

        snapshots.Sort(StringComparer.OrdinalIgnoreCase);
        int deleteCount = Math.Max(0, snapshots.Count - maxSnapshots);
        for (int i = 0; i < deleteCount; i++)
        {
            string path = snapshots[i];
            TryDeleteFile(path, operationId);
            TryDeleteFile(_paths.GetSecureMarkerPath(path), operationId);
            TryDeleteFile(_paths.GetMetadataPath(path), operationId);
        }

        if (deleteCount > 0)
        {
            LogInfo(
                nameof(ApplySnapshotRetention),
                "[SNAPSHOT][RETENTION_OK] Old snapshots removed.",
                LogMetadata.Of("operationId", operationId, "directory", SavePathResolver.SafeFileLabel(directory), "deleted", deleteCount),
                null,
                operationId);
        }
    }

    void WriteMetadata(string path, SaveMetadata metadata, string operationId)
    {
        if (metadata == null)
            return;

        try
        {
            string metadataPath = _paths.GetMetadataPath(path);
            string json = JsonUtility.ToJson(metadata, true);
            WritePlainTextAtomic(metadataPath, json, operationId);
        }
        catch (Exception exception)
        {
            LogWarn(
                nameof(WriteMetadata),
                "[SAVE][METADATA_FAILED] Save metadata write failed.",
                LogMetadata.Of(
                    "operationId", operationId,
                    "file", SavePathResolver.SafeFileLabel(path),
                    "errorType", exception.GetType().Name,
                    "error", exception.Message),
                null,
                operationId);
        }
    }

    SaveMetadata TryReadMetadata(string path, string operationId)
    {
        string metadataPath = _paths.GetMetadataPath(path);
        if (!File.Exists(metadataPath))
            return null;

        try
        {
            string json = File.ReadAllText(metadataPath, Utf8NoBom);
            return JsonUtility.FromJson<SaveMetadata>(json);
        }
        catch (Exception exception)
        {
            LogWarn(
                nameof(TryReadMetadata),
                "[SAVE][METADATA_READ_FAILED] Save metadata could not be read.",
                LogMetadata.Of(
                    "operationId", operationId,
                    "file", SavePathResolver.SafeFileLabel(path),
                    "metadataFile", SavePathResolver.SafeFileLabel(metadataPath),
                    "errorType", exception.GetType().Name),
                null,
                operationId);
            return null;
        }
    }

    static SaveMetadata CreateMetadata(
        SaveData data,
        int slot,
        string saveType,
        string source,
        string operationId,
        string json,
        string protectedJson)
    {
        return new SaveMetadata
        {
            schemaVersion = SaveMetadata.CurrentSchemaVersion,
            saveType = SaveDataSanitizer.SanitizeIdentifier(saveType),
            source = SaveDataSanitizer.SanitizeIdentifier(source),
            operationId = SaveDataSanitizer.SanitizeIdentifier(operationId),
            slot = SavePathResolver.ClampSlot(slot),
            storyId = data != null ? data.storyId : "",
            chapterId = data != null ? data.chapterId : "",
            episodeId = data != null ? data.episodeId : "",
            nodeGuid = data != null ? data.currentNodeGuid : "",
            saveVersion = data != null ? data.version : 0,
            savedAtIso = data != null ? data.savedAtIso : "",
            clientSavedAtIso = DateTime.UtcNow.ToString("o"),
            payloadChecksum = ComputeSha256(json),
            protectedPayloadChecksum = ComputeSha256(protectedJson),
            payloadChars = json != null ? json.Length : 0,
            protectedPayloadChars = protectedJson != null ? protectedJson.Length : 0
        };
    }

    static void ReplaceFile(string tempPath, string path)
    {
        try
        {
            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
        catch (PlatformNotSupportedException)
        {
            ReplaceFileFallback(tempPath, path);
        }
        catch (IOException)
        {
            ReplaceFileFallback(tempPath, path);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    static void ReplaceFileFallback(string tempPath, string path)
    {
        if (File.Exists(path))
            File.Delete(path);

        File.Move(tempPath, path);
    }

    void WritePlainTextAtomic(string path, string content, string operationId)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string tempPath = _paths.GetTempPath(path, operationId);
        File.WriteAllText(tempPath, content ?? "", Utf8NoBom);
        ReplaceFile(tempPath, path);
    }

    void EnsureSecureMarkerFile(string savePath)
    {
        string markerPath = _paths.GetSecureMarkerPath(savePath);
        if (File.Exists(markerPath))
            return;

        File.WriteAllText(markerPath, "nocturne-local-secure-v1", Utf8NoBom);
    }

    bool HasSecureMarkerFile(string savePath)
    {
        return File.Exists(_paths.GetSecureMarkerPath(savePath));
    }

    static void CopyIfExists(string source, string target)
    {
        if (File.Exists(source))
            File.Copy(source, target, true);
    }

    static void TryDeleteFile(string path, string operationId)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception)
        {
            AppLogger.Warn(
                AppLogCategory.SaveSystem,
                nameof(SavePersistenceService),
                nameof(TryDeleteFile),
                "[SAVE][DELETE_FILE_FAILED] File cleanup failed.",
                LogMetadata.Of(
                    "operationId", operationId,
                    "file", SavePathResolver.SafeFileLabel(path),
                    "errorType", exception.GetType().Name,
                    "error", exception.Message),
                recoverable: true);
        }
    }

    SaveOperationResult FailWrite(
        long startedAt,
        string path,
        string errorType,
        string message,
        IDictionary<string, object> metadata,
        string operationId)
    {
        long durationMs = AppDiagnostics.ElapsedMilliseconds(startedAt);
        LogWarn(
            nameof(WriteSaveFile),
            "[SAVE][FAILURE] Save operation rejected.",
            AddMetadata(metadata, "errorType", errorType, "reason", message, "durationMs", durationMs),
            durationMs,
            operationId);
        return SaveOperationResult.Fail(path, errorType, message);
    }

    static IDictionary<string, object> BaseMetadata(
        string operationId,
        int slot,
        string path,
        string saveType,
        string source,
        SaveData data)
    {
        return LogMetadata.Of(
            "operationId", operationId,
            "slot", slot,
            "saveType", saveType,
            "source", source,
            "file", SavePathResolver.SafeFileLabel(path),
            "storyId", data != null ? data.storyId : "",
            "chapterId", data != null ? data.chapterId : "",
            "episodeId", data != null ? data.episodeId : "",
            "nodeGuid", data != null ? data.currentNodeGuid : "",
            "schemaVersion", data != null ? data.version : 0);
    }

    static IDictionary<string, object> AddMetadata(IDictionary<string, object> metadata, params object[] keyValues)
    {
        var result = metadata != null
            ? new Dictionary<string, object>(metadata, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (keyValues == null)
            return result;

        for (int i = 0; i + 1 < keyValues.Length; i += 2)
        {
            string key = keyValues[i] != null ? keyValues[i].ToString() : "";
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = keyValues[i + 1];
        }

        return result;
    }

    static void LogInfo(string operation, string message, IDictionary<string, object> metadata, long? durationMs, string operationId)
    {
        AppLogger.Info(
            AppLogCategory.SaveSystem,
            nameof(SavePersistenceService),
            operation,
            message,
            metadata,
            durationMs,
            operationId);
    }

    static void LogWarn(string operation, string message, IDictionary<string, object> metadata, long? durationMs, string operationId)
    {
        AppLogger.Warn(
            AppLogCategory.SaveSystem,
            nameof(SavePersistenceService),
            operation,
            message,
            metadata,
            durationMs,
            operationId,
            recoverable: true);
    }

    static string NewOperationId()
    {
        return Guid.NewGuid().ToString("N");
    }

    static string ComputeSha256(string value)
    {
        if (value == null)
            value = "";

        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }
    }

    static bool FixedEquals(string left, string right)
    {
        if (left == null || right == null || left.Length != right.Length)
            return false;

        int diff = 0;
        for (int i = 0; i < left.Length; i++)
            diff |= left[i] ^ right[i];

        return diff == 0;
    }

    sealed class CandidateResult
    {
        public bool Success;
        public SaveData Data;
        public string ErrorType;
        public string Message;
        public bool WasProtected;
        public bool WasLegacy;

        public static CandidateResult Ok(SaveData data, bool wasProtected, bool wasLegacy)
        {
            return new CandidateResult
            {
                Success = true,
                Data = data,
                WasProtected = wasProtected,
                WasLegacy = wasLegacy
            };
        }

        public static CandidateResult Fail(string errorType, string message)
        {
            return new CandidateResult
            {
                Success = false,
                ErrorType = errorType ?? "",
                Message = message ?? ""
            };
        }
    }
}
