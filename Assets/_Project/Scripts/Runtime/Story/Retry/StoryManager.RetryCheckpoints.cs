using UnityEngine;

public partial class StoryManager
{
	private void CaptureRetryCheckpoints(
		StartNode startNode)
	{
		if (startNode == null ||
			SaveManager.Instance == null ||
			GameState.Instance == null)
		{
			return;
		}

		int slot = ResolveProgressSaveSlot();

		BaseStoryNode previousNode =
			GameState.Instance.currentNode;

		GameState.Instance.currentNode = startNode;

		SaveData checkpoint =
			SaveManager.Instance.BuildCurrentSaveData(this);

		GameState.Instance.currentNode = previousNode;

		if (checkpoint == null ||
			!checkpoint.HasPosition)
		{
			Debug.LogWarning(
				"[StoryManager] Retry checkpoint could not be created.",
				this);

			return;
		}

		SaveManager.Instance.EnsureRetryChapterCheckpoint(
			checkpoint,
			slot,
			currentChapter);

		SaveManager.Instance.EnsureRetrySeasonCheckpoint(
			checkpoint,
			slot,
			currentSeason);
	}
}