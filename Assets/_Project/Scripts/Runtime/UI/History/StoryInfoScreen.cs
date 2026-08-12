using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/History/Story Info Screen")]
public sealed class StoryInfoScreen : MonoBehaviour
{
	[Header("Stats")]
	[SerializeField] private Transform contentRoot;
	[SerializeField] private StoryInfoStatView statViewPrefab;

	[Header("State")]
	[SerializeField] private GameObject emptyState;

	[SerializeField, TextArea(2, 4)]
	private string missingDescriptionText =
		"Описание характеристики не заполнено.";

	[Header("Navigation")]
	[SerializeField] private Button closeButton;
	[SerializeField] private string historyScreenId = "History";

	private readonly List<StoryInfoStatView> _views =
		new List<StoryInfoStatView>();

	private GameData _data;
	private MenuController _menuController;

	private void Awake()
	{
		if (closeButton != null)
			closeButton.onClick.AddListener(Close);
	}

	private void OnEnable()
	{
		if (_data != null)
			Refresh();
	}

	private void OnDestroy()
	{
		if (closeButton != null)
			closeButton.onClick.RemoveListener(Close);
	}

	public void Configure(
		GameData data,
		MenuController menuController)
	{
		_data = data;
		_menuController = menuController;

		Refresh();
	}

	private void Refresh()
	{
		if (_data == null)
		{
			HideAllViews();
			SetEmptyState(true);
			return;
		}

		var stats = _data.StoryStats;
		int count = stats != null ? stats.Count : 0;

		EnsureViewCount(count);

		bool hasVisibleStats = false;

		for (int i = 0; i < _views.Count; i++)
		{
			StoryInfoStatView view = _views[i];

			if (i >= count)
			{
				view.Hide();
				continue;
			}

			GameStoryStatData stat = stats[i];

			if (stat == null)
			{
				view.Hide();
				continue;
			}

			int value =
				StoryHistoryStatsResolver.ResolveValue(
					_data,
					stat);

			string displayName =
				StoryStatDisplayNameResolver.Resolve(
					_data,
					stat);

			string description =
				ResolveDescription(stat);

			view.SetData(
				stat,
				value,
				displayName,
				description);

			hasVisibleStats = true;
		}

		SetEmptyState(!hasVisibleStats);
	}

	private void EnsureViewCount(int requiredCount)
	{
		if (requiredCount <= _views.Count)
			return;

		if (contentRoot == null)
		{
			Debug.LogError(
				"Cannot create Info stat views: content root is missing.",
				this);

			return;
		}

		if (statViewPrefab == null)
		{
			Debug.LogError(
				"Cannot create Info stat views: stat prefab is missing.",
				this);

			return;
		}

		while (_views.Count < requiredCount)
		{
			StoryInfoStatView view =
				Instantiate(
					statViewPrefab,
					contentRoot);

			_views.Add(view);
		}
	}

	private string ResolveDescription(
		GameStoryStatData stat)
	{
		if (stat != null &&
			!string.IsNullOrWhiteSpace(stat.Description))
		{
			return stat.Description.Trim();
		}

		return missingDescriptionText ?? "";
	}

	private void HideAllViews()
	{
		for (int i = 0; i < _views.Count; i++)
		{
			if (_views[i] != null)
				_views[i].Hide();
		}
	}

	private void SetEmptyState(bool isVisible)
	{
		if (emptyState != null)
			emptyState.SetActive(isVisible);
	}

	private void Close()
	{
		if (_menuController == null)
		{
			Debug.LogError(
				"Cannot close Info: MenuController is missing.",
				this);

			return;
		}

		StoryScreenNavigator navigator =
			_menuController.ScreenNavigator;

		if (navigator == null)
		{
			Debug.LogError(
				"Cannot close Info: navigator is missing.",
				this);

			return;
		}

		string screenId =
			UIScreenState.NormalizeScreenId(
				historyScreenId);

		if (screenId.Length == 0)
			return;

		if (!navigator.OpenScreen(screenId))
		{
			Debug.LogWarning(
				$"Cannot return to screen '{screenId}'.",
				this);
		}
	}
}
