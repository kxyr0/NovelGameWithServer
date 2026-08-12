using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Saves/Story Save Card View")]
public sealed class StorySaveCardView : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text _storyTitleText;
    [SerializeField] private TMP_Text _episodeTitleText;
    [SerializeField] private TMP_Text _savedAtText;
    [SerializeField] private TMP_Text _progressText;
    [SerializeField] private string _dateFormat = "dd.MM.yyyy HH:mm";
    [SerializeField] private string _progressFormat = "Сезон {0} Глава {1} {2}%";

    [Header("Stats")]
    [SerializeField] private StorySaveStatView[] _statViews =
        Array.Empty<StorySaveStatView>();
    [SerializeField] private bool _autoFindStatViews = true;

    [Header("Action")]
    [SerializeField] private Button _loadButton;

    private StorySavesScreen _screen;
    private int _slot;

    private void Awake()
    {
        if (_loadButton == null)
            _loadButton = GetComponent<Button>();

        _loadButton?.onClick.AddListener(Open);
    }

    private void OnDestroy()
    {
        _loadButton?.onClick.RemoveListener(Open);
    }

    public void Bind(
        StorySavesScreen screen,
        GameData data,
        SaveData save,
        int slot)
    {
        _screen = screen;
        _slot = slot;

        StorySaveDisplayMetadata metadata =
            StorySaveMetadataResolver.Resolve(data, save, _dateFormat);

        SetText(_storyTitleText, metadata.StoryTitle);
        SetText(_episodeTitleText, metadata.EpisodeTitle);
        SetText(_savedAtText, metadata.SavedAtText);
        SetText(_progressText, FormatProgress(metadata));
        RefreshStats(data, save);
    }

    private void Open()
    {
        _screen?.OpenSaveSlot(_slot);
    }

    private void RefreshStats(GameData data, SaveData save)
    {
        StorySaveStatView[] views = ResolveStatViews();
        IReadOnlyList<GameStoryStatData> stats =
            data != null ? data.StoryStats : null;

        for (int i = 0; i < views.Length; i++)
        {
            StorySaveStatView view = views[i];
            if (view == null)
                continue;

            if (stats == null || i >= stats.Count || stats[i] == null)
            {
                view.Hide();
                continue;
            }

            GameStoryStatData stat = stats[i];
            view.SetData(
                stat,
                StorySaveStatValueResolver.Resolve(save, stat),
                StoryStatDisplayNameResolver.Resolve(data, stat));
        }
    }

    private StorySaveStatView[] ResolveStatViews()
    {
        if ((_statViews == null || _statViews.Length == 0) &&
            _autoFindStatViews)
        {
            _statViews =
                GetComponentsInChildren<StorySaveStatView>(true);
        }

        return _statViews ?? Array.Empty<StorySaveStatView>();
    }

    private string FormatProgress(StorySaveDisplayMetadata metadata)
    {
        string format = string.IsNullOrWhiteSpace(_progressFormat)
            ? "Сезон {0} Глава {1} {2}%"
            : _progressFormat;

        try
        {
            return string.Format(
                format,
                metadata.SeasonNumber,
                metadata.ChapterNumber,
                metadata.ChapterPercent);
        }
        catch (FormatException)
        {
            return $"Сезон {metadata.SeasonNumber} " +
                   $"Глава {metadata.ChapterNumber} " +
                   $"{metadata.ChapterPercent}%";
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target == null)
            return;

        target.text = value ?? "";
        target.gameObject.SetActive(
            !string.IsNullOrWhiteSpace(target.text));
    }
}
