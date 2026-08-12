using System;
using System.Collections.Generic;
using System.IO;

public partial class SaveManager
{
	public bool EnsureRetryChapterCheckpoint(
		SaveData data,
		int slot,
		int chapterIndex)
	{
		if (!CanUseRetryCheckpoint(data, slot))
			return false;

		string path =
			Persistence.Paths.GetRetryChapterCheckpointPath(
				data.storyId,
				slot,
				chapterIndex);

		return EnsureRetryCheckpoint(
			data,
			slot,
			path,
			"retry_chapter");
	}

	public bool EnsureRetrySeasonCheckpoint(
		SaveData data,
		int slot,
		int seasonIndex)
	{
		if (!CanUseRetryCheckpoint(data, slot))
			return false;

		string path =
			Persistence.Paths.GetRetrySeasonCheckpointPath(
				data.storyId,
				slot,
				seasonIndex);

		return EnsureRetryCheckpoint(
			data,
			slot,
			path,
			"retry_season");
	}

	public SaveData LoadRetryChapterCheckpoint(
		string storyId,
		int slot,
		int chapterIndex)
	{
		storyId =
			SaveDataSanitizer.SanitizeIdentifier(storyId);

		if (string.IsNullOrEmpty(storyId) ||
			!SavePathResolver.IsValidSlot(slot))
		{
			return null;
		}

		string path =
			Persistence.Paths.GetRetryChapterCheckpointPath(
				storyId,
				slot,
				chapterIndex);

		return LoadRetryCheckpoint(
			path,
			storyId,
			slot,
			"retry chapter");
	}

	public SaveData LoadRetrySeasonCheckpoint(
		string storyId,
		int slot,
		int seasonIndex)
	{
		storyId =
			SaveDataSanitizer.SanitizeIdentifier(storyId);

		if (string.IsNullOrEmpty(storyId) ||
			!SavePathResolver.IsValidSlot(slot))
		{
			return null;
		}

		string path =
			Persistence.Paths.GetRetrySeasonCheckpointPath(
				storyId,
				slot,
				seasonIndex);

		return LoadRetryCheckpoint(
			path,
			storyId,
			slot,
			"retry season");
	}

	private bool EnsureRetryCheckpoint(
		SaveData data,
		int slot,
		string path,
		string saveType)
	{
		string backupPath =
			Persistence.Paths.GetBackupPath(path);

		if (File.Exists(path) || File.Exists(backupPath))
		{
			SaveLoadResult existing =
				Persistence.LoadSaveFile(
					path,
					saveType,
					data.storyId,
					slot);

			if (existing.Success)
				return true;
		}

		SaveOperationResult result =
			Persistence.WriteSaveFile(
				path,
				data,
				slot,
				saveType,
				nameof(SaveManager));

		return result.Success;
	}

	private SaveData LoadRetryCheckpoint(
		string path,
		string storyId,
		int slot,
		string label)
	{
		SaveLoadResult result =
			Persistence.LoadSaveFile(
				path,
				label,
				storyId,
				slot);

		return result.Success
			? result.Data
			: null;
	}

	private static bool CanUseRetryCheckpoint(
		SaveData data,
		int slot)
	{
		return data != null &&
			   data.HasPosition &&
			   !string.IsNullOrEmpty(
				   SaveDataSanitizer.SanitizeIdentifier(
					   data.storyId)) &&
			   SavePathResolver.IsValidSlot(slot);
	}
}