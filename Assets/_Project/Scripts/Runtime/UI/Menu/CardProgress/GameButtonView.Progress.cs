using System;
using Michsky.MUIP;
using TMPro;
using UnityEngine;

public partial class GameButtonView
{
	private const string ProgressVisibleScreenId = "MainScreen";
	private bool _storyProgressDirty;

	[Header("Прогресс истории")]
	[SerializeField, Tooltip("Текст вида «Глава 2. Название главы».")]
	private TMP_Text chapterProgressText;

	[SerializeField, Tooltip("Progress Bar из prefab карточки. Значение берётся из сохранения, его авто-анимация отключается.")]
	private ProgressBar chapterProgressBar;

	private void AutoWireProgressReferences()
	{
		if (chapterProgressBar == null)
			chapterProgressBar = GetComponentInChildren<ProgressBar>(true);

		TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
		for (int i = 0; i < texts.Length; i++)
		{
			TMP_Text text = texts[i];
			if (text == null)
				continue;

			string objectName = text.gameObject.name ?? "";
			if (gameNameText == null && IsNamed(objectName, "NameStory", "StoryName", "Title"))
				gameNameText = text;
			else if (genreText == null && IsNamed(objectName, "GenreText", "Genre", "Genres"))
				genreText = text;
		}

		if (chapterProgressText == null && chapterProgressBar != null)
		{
			TMP_Text percent = chapterProgressBar.textPercent;
			TMP_Text[] progressTexts = chapterProgressBar.GetComponentsInChildren<TMP_Text>(true);
			for (int i = 0; i < progressTexts.Length; i++)
			{
				if (progressTexts[i] != null && progressTexts[i] != percent)
				{
					chapterProgressText = progressTexts[i];
					break;
				}
			}
		}
	}

	public void RefreshStoryProgress()
	{
		if (_data == null)
			return;

		_storyProgressDirty = false;
		AutoWireProgressReferences();
		StoryCardProgressData progress = StoryCardProgressResolver.Resolve(_data);
		if (chapterProgressText != null && chapterProgressText.text != progress.ChapterLabel)
			chapterProgressText.SetText(progress.ChapterLabel);

		if (chapterProgressBar != null)
		{
			bool hasProgress = progress.Percent > 0;

			if (chapterProgressBar.gameObject.activeSelf != hasProgress)
				chapterProgressBar.gameObject.SetActive(hasProgress);

			if (hasProgress)
			{
				chapterProgressBar.isOn = false;
				chapterProgressBar.restart = false;

				if (!Mathf.Approximately(chapterProgressBar.currentPercent, progress.Percent))
					chapterProgressBar.ChangeValue(progress.Percent);
			}
		}
	}

	private void BindProgressRefreshEvents()
	{
		UnbindProgressRefreshEvents();
		SaveManager.OnStorySaveChanged += HandleStorySaveChanged;
		NetworkManager.OnFeaturesUpdated += HandleFeaturesUpdated;
		UIScreenState.CurrentScreenChanged += HandleProgressScreenChanged;
	}

	private void UnbindProgressRefreshEvents()
	{
		SaveManager.OnStorySaveChanged -= HandleStorySaveChanged;
		NetworkManager.OnFeaturesUpdated -= HandleFeaturesUpdated;
		UIScreenState.CurrentScreenChanged -= HandleProgressScreenChanged;
	}

	private void HandleStorySaveChanged(string storyId)
	{
		string ownStoryId = _data != null && _data.Story != null
			? SaveDataSanitizer.SanitizeIdentifier(_data.Story.StoryId)
			: "";

		if (!string.IsNullOrEmpty(storyId) &&
			!string.Equals(storyId, ownStoryId, StringComparison.Ordinal))
		{
			return;
		}

		if (!UIScreenState.IsCurrent(ProgressVisibleScreenId))
		{
			_storyProgressDirty = true;
			return;
		}

		RefreshStoryProgress();
	}

	private void HandleFeaturesUpdated()
	{
		if (!UIScreenState.IsCurrent(ProgressVisibleScreenId))
		{
			_storyProgressDirty = true;
			return;
		}

		RefreshStoryProgress();
	}

	private void HandleProgressScreenChanged(string screenId)
	{
		if (_storyProgressDirty && string.Equals(screenId, ProgressVisibleScreenId, StringComparison.Ordinal))
			RefreshStoryProgress();
	}

	private static bool IsNamed(string value, params string[] expected)
	{
		for (int i = 0; i < expected.Length; i++)
		{
			if (value.Equals(expected[i], StringComparison.OrdinalIgnoreCase) ||
				value.StartsWith(expected[i] + " -", StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}
}
