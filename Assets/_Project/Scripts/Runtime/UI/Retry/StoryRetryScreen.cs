using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Retry/Story Retry Screen")]
public sealed class StoryRetryScreen : MonoBehaviour
{
	[Header("Componets")]
	[SerializeField] private StoryHistoryScreen storyHistoryScreen;
	[Header("Buttons")]
	[SerializeField] private Button chapterButton;
	[SerializeField] private Button seasonButton;
	[SerializeField] private Button backButton;

	[SerializeField] private string historyScreenId = "History";

	private GameData _data;
	private MenuController _menuController;

	private void Awake()
	{
		if (chapterButton != null)
			chapterButton.onClick.AddListener(RetryChapter);

		if (seasonButton != null)
			seasonButton.onClick.AddListener(RetrySeason);

		if (backButton != null)
			backButton.onClick.AddListener(ReturnToHistory);
	}

	private void OnDestroy()
	{
		if (chapterButton != null)
			chapterButton.onClick.RemoveListener(RetryChapter);

		if (seasonButton != null)
			seasonButton.onClick.RemoveListener(RetrySeason);

		if (backButton != null)
			backButton.onClick.RemoveListener(ReturnToHistory);
	}

	public void Configure(
		GameData data,
		MenuController menuController)
	{
		_data = data;
		_menuController = menuController;
	}
	private void RetrySeason()
	{
		if (_data == null ||
			_data.Story == null ||
			_menuController == null ||
			SaveManager.Instance == null)
		{
			Debug.LogError(
				"Cannot retry season: retry context is incomplete.",
				this);

			return;
		}

		string storyId =
			ResolveStoryId(_data.Story);

		if (string.IsNullOrEmpty(storyId))
			return;

		int slot =
			StorySaveSlotSelection.GetSelectedSlot(
				storyId);

		SaveData current =
			SaveManager.Instance.LoadForStorySlotIfExists(
				storyId,
				slot);

		if (current == null || !current.HasPosition)
		{
			Debug.LogWarning(
				$"Cannot retry season: story '{storyId}' has no save.",
				this);

			return;
		}

		SaveData restored =
			SaveManager.Instance.RestoreRetrySeasonCheckpoint(
				storyId,
				slot,
				current.currentSeasonIndex);

		if (restored == null)
		{
			Debug.LogWarning(
				$"Cannot retry season {current.currentSeasonIndex}: checkpoint is missing.",
				this);

			return;
		}

		_menuController.StartStory(_data);
	}
	private void RetryChapter()
	{
		if (_data == null ||
			_data.Story == null ||
			_menuController == null ||
			SaveManager.Instance == null)
		{
			Debug.LogError(
				"Cannot retry chapter: retry context is incomplete.",
				this);

			return;
		}

		string storyId = ResolveStoryId(_data.Story);

		if (string.IsNullOrEmpty(storyId))
			return;

		int slot =
			StorySaveSlotSelection.GetSelectedSlot(
				storyId);

		SaveData current =
			SaveManager.Instance.LoadForStorySlotIfExists(
				storyId,
				slot);

		if (current == null || !current.HasPosition)
		{
			Debug.LogWarning(
				$"Cannot retry chapter: story '{storyId}' has no save.",
				this);

			return;
		}

		SaveData restored =
			SaveManager.Instance.RestoreRetryChapterCheckpoint(
				storyId,
				slot,
				current.currentChapterIndex);

		if (restored == null)
		{
			Debug.LogWarning(
				$"Cannot retry chapter {current.currentChapterIndex}: checkpoint is missing.",
				this);

			return;
		}

		_menuController.StartStory(_data);
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

		id =
			SaveDataSanitizer.SanitizeIdentifier(
				story.storyId);

		if (!string.IsNullOrEmpty(id))
			return id;

		return SaveDataSanitizer.SanitizeIdentifier(
			story.name);
	}
	private void ReturnToHistory()
	{
		if (_menuController == null)
		{
			Debug.LogError(
				"Cannot return from Retry: MenuController is missing.",
				this);

			return;
		}

		gameObject
			.GetComponent<CanvasGroup>()
				.DOFade(0, 0.5f).Complete();

		gameObject.GetComponent<CanvasGroup>().blocksRaycasts = false;
		gameObject.GetComponent<CanvasGroup>().interactable = false;
	}
}