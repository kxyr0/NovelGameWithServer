using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Michsky.MUIP;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/History/Story History Screen")]
public sealed class StoryHistoryScreen : MonoBehaviour
{
	[Header("Story Info")]
	[SerializeField] private TMP_Text titleText;
	[SerializeField] private TMP_Text genreText;
	[SerializeField] private TMP_Text episodesText;
	[SerializeField] private TMP_Text descriptionText;
	[SerializeField] private Image coverImage;

	[Header("Story Stats")]
	[SerializeField] private StoryHistoryStatView[] statViews;

	[Header("Story Progress")]
	[SerializeField] private TMP_Text chapterProgressText;
	[SerializeField] private ProgressBar chapterProgressBar;

	[Header("Actions")]
	[SerializeField] private Button playButton;
	[SerializeField] private Button backButton;

	[SerializeField]
	private StoryHistoryUtilityNavigation utilityNavigation;

	private GameData _data;
	private MenuController _menuController;

	private void Awake()
	{
		if (playButton != null)
			playButton.onClick.AddListener(PlayStory);

		if (backButton != null)
			backButton.onClick.AddListener(ReturnToMainScreen);
	}

	private void OnDestroy()
	{
		if (playButton != null)
			playButton.onClick.RemoveListener(PlayStory);

		if (backButton != null)
			backButton.onClick.RemoveListener(ReturnToMainScreen);
	}

	public void Configure(
	GameData data,
	MenuController menuController)
	{
		_data = data;
		_menuController = menuController;

		utilityNavigation?.Configure(
			data,
			menuController);

		RefreshStoryInfo();
	}

	private void RefreshStoryInfo()
	{
		if (_data == null)
			return;

		if (titleText != null)
			titleText.text = ResolveTitle(_data);

		if (descriptionText != null)
			descriptionText.text = _data.Description ?? "";

		if (genreText != null)
			genreText.text = _data.GenreText ?? "";

		if (episodesText != null)
			episodesText.text = _data.EpisodeProgressText ?? "";

		if (coverImage != null)
		{
			if (_data.GameIcon != null)
			{
				coverImage.sprite = _data.GameIcon;
				coverImage.enabled = true;
			}
			else
			{
				coverImage.sprite = null;
				coverImage.enabled = false;
			}
		}
		RefreshStoryProgress();
		RefreshStoryStats();
	}

	private void PlayStory()
	{
		if (_data == null)
		{
			Debug.LogError(
				"Cannot play story: GameData is missing.",
				this);

			return;
		}

		if (_menuController == null)
		{
			Debug.LogError(
				"Cannot play story: MenuController is missing.",
				this);

			return;
		}

		_menuController.StartStory(_data);
	}

	private void ReturnToMainScreen()
	{
		if (_menuController == null)
		{
			Debug.LogError(
				"Cannot return to main screen: MenuController is missing.",
				this);

			return;
		}

		_menuController.ReturnToMenu();
	}

	private static string ResolveTitle(GameData data)
	{
		if (data == null)
			return "";

		if (!string.IsNullOrWhiteSpace(data.GameName))
			return data.GameName;

		if (data.Story != null &&
			!string.IsNullOrWhiteSpace(data.Story.StoryName))
		{
			return data.Story.StoryName;
		}

		return data.name;
	}
	private void RefreshStoryProgress()
	{
		if (_data == null)
			return;

		StoryCardProgressData progress =
			StoryCardProgressResolver.Resolve(_data);

		if (chapterProgressText != null)
			chapterProgressText.text = progress.ChapterLabel;

		if (chapterProgressBar != null)
		{
			bool hasProgress = progress.Percent > 0;

			chapterProgressBar.gameObject.SetActive(hasProgress);

			if (hasProgress)
			{
				chapterProgressBar.isOn = false;
				chapterProgressBar.restart = false;
				chapterProgressBar.ChangeValue(progress.Percent);
			}
		}
	}
	private void RefreshStoryStats()
	{
		if (_data == null || statViews == null)
			return;

		var stats = _data.StoryStats;

		for (int i = 0; i < statViews.Length; i++)
		{
			StoryHistoryStatView view = statViews[i];

			if (view == null)
				continue;

			if (i >= stats.Count || stats[i] == null)
			{
				view.Hide();
				continue;
			}

			GameStoryStatData stat = stats[i];

			int value =
				StoryHistoryStatsResolver.ResolveValue(
					_data,
					stat);

			string displayName =
				StoryStatDisplayNameResolver.Resolve(
				_data,
				stat);

			view.SetData(
				stat,
				value,
				displayName);
		}
	}
}