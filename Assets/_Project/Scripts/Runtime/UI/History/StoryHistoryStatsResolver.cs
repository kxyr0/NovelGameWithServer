using System;
using UnityEngine;

public static class StoryHistoryStatsResolver
{
	public static int ResolveValue(
		GameData game,
		GameStoryStatData stat)
	{
		if (stat == null)
			return 0;

		string statId =
			SaveDataSanitizer.SanitizeStatKey(stat.StatId);

		if (string.IsNullOrEmpty(statId))
			return stat.Value;

		string storyId = ResolveStoryId(
			game != null ? game.Story : null);

		if (TryGetRuntimeValue(
			storyId,
			statId,
			out int runtimeValue))
		{
			return runtimeValue;
		}

		if (TryGetSavedValue(
			storyId,
			statId,
			out int savedValue))
		{
			return savedValue;
		}

		return stat.Value;
	}

	private static bool TryGetRuntimeValue(
		string storyId,
		string statId,
		out int value)
	{
		value = 0;

		GameState state = GameState.Instance;
		if (state == null)
			return false;

		string currentStoryId =
			SaveDataSanitizer.SanitizeIdentifier(
				state.CurrentStoryId);

		if (!string.Equals(
			currentStoryId,
			storyId,
			StringComparison.Ordinal))
		{
			return false;
		}

		if (state.stats == null)
			return false;

		return state.stats.TryGetValue(statId, out value);
	}

	private static bool TryGetSavedValue(
		string storyId,
		string statId,
		out int value)
	{
		value = 0;

		if (string.IsNullOrEmpty(storyId) ||
			SaveManager.Instance == null)
		{
			return false;
		}

		int slot =
			StorySaveSlotSelection.GetSelectedSlot(storyId);

		SaveData save =
			SaveManager.Instance.LoadForStorySlotIfExists(
				storyId,
				slot);

		if (save == null ||
			save.statKeys == null ||
			save.statValues == null)
		{
			return false;
		}

		int count = Mathf.Min(
			save.statKeys.Count,
			save.statValues.Count);

		for (int i = 0; i < count; i++)
		{
			string key =
				SaveDataSanitizer.SanitizeStatKey(
					save.statKeys[i]);

			if (!string.Equals(
				key,
				statId,
				StringComparison.Ordinal))
			{
				continue;
			}

			value = save.statValues[i];
			return true;
		}

		return false;
	}

	private static string ResolveStoryId(
		StoryData story)
	{
		if (story == null)
			return "";

		string id =
			SaveDataSanitizer.SanitizeIdentifier(
				story.StoryId);

		if (!string.IsNullOrEmpty(id))
			return id;

		id = SaveDataSanitizer.SanitizeIdentifier(
			story.storyId);

		if (!string.IsNullOrEmpty(id))
			return id;

		return SaveDataSanitizer.SanitizeIdentifier(
			story.name);
	}
}