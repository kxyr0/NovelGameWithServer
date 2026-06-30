using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "StoryInterfaceStyleCatalog", menuName = "VN/UI/Story Interface Style Catalog")]
public sealed class StoryInterfaceStyleCatalog : ScriptableObject
{
    [SerializeField] private List<StoryInterfaceStyleEntry> _entries = new List<StoryInterfaceStyleEntry>();

    public IReadOnlyList<StoryInterfaceStyleEntry> Entries => _entries;

    public bool TryGetStoryUiStyle(
        StoryData story,
        string storyId,
        out StoryUiStyle style,
        out Sprite backgroundSprite)
    {
        if (TryGetEntry(story, storyId, out StoryInterfaceStyleEntry entry))
            return entry.TryGetStoryUiStyle(out style, out backgroundSprite);

        style = null;
        backgroundSprite = null;
        return false;
    }

    public bool TryGetCutsceneStoryUiStyle(
        StoryData story,
        string storyId,
        out StoryUiStyle style,
        out Sprite backgroundSprite)
    {
        if (TryGetEntry(story, storyId, out StoryInterfaceStyleEntry entry))
            return entry.TryGetCutsceneStoryUiStyle(out style, out backgroundSprite);

        style = null;
        backgroundSprite = null;
        return false;
    }

    public bool TryGetEntry(StoryData story, string storyId, out StoryInterfaceStyleEntry entry)
    {
        entry = null;

        if (_entries == null)
            return false;

        StoryInterfaceStyleEntry bestEntry = null;
        int bestScore = 0;

        for (int i = 0; i < _entries.Count; i++)
        {
            StoryInterfaceStyleEntry candidate = _entries[i];
            if (candidate == null)
                continue;

            int score = candidate.GetMatchScore(story, storyId);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestEntry = candidate;
        }

        entry = bestEntry;
        return entry != null;
    }

    private void OnValidate()
    {
        if (_entries == null)
            _entries = new List<StoryInterfaceStyleEntry>();

        for (int i = 0; i < _entries.Count; i++)
            _entries[i]?.Validate();
    }
}

[Serializable]
public sealed class StoryInterfaceStyleEntry
{
    [Tooltip("Метка только для редактора. Логика берёт Story Asset и Story IDs ниже.")]
    [SerializeField] private string _label = "";

    [Tooltip("Ассет StoryData для этого интерфейса. Это самая точная привязка к истории.")]
    [SerializeField] private StoryData _storyAsset;

    [Tooltip("ID историй, которые используют этот интерфейс. Сюда добавляются значения storyId из JSON, например privychka_pritvoryatsya.")]
    [SerializeField] private List<string> _storyIds = new List<string>();

    [Header("Regular Story UI")]
    [FormerlySerializedAs("_dialoguePanelStyle")]
    [SerializeField] private StoryUiStyle _storyUiStyle;
    [SerializeField] private Sprite _dialogueBackgroundSprite;

    [Header("Cutscene UI")]
    [FormerlySerializedAs("_useSeparateCutsceneDialoguePanelStyle")]
    [SerializeField] private bool _useSeparateCutsceneStoryUiStyle;
    [FormerlySerializedAs("_cutsceneDialoguePanelStyle")]
    [SerializeField] private StoryUiStyle _cutsceneStoryUiStyle;
    [SerializeField] private Sprite _cutsceneDialogueBackgroundSprite;

    public string Label => _label;
    public StoryData StoryAsset => _storyAsset;
    public IReadOnlyList<string> StoryIds => _storyIds;
    public StoryUiStyle StoryUiStyle => _storyUiStyle;
    public Sprite DialogueBackgroundSprite => _dialogueBackgroundSprite;
    public bool UseSeparateCutsceneStoryUiStyle => _useSeparateCutsceneStoryUiStyle;
    public StoryUiStyle CutsceneStoryUiStyle => _cutsceneStoryUiStyle;
    public Sprite CutsceneDialogueBackgroundSprite => _cutsceneDialogueBackgroundSprite;

    public bool TryGetStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        style = _storyUiStyle;
        backgroundSprite = _dialogueBackgroundSprite;
        return style != null || backgroundSprite != null;
    }

    public bool TryGetCutsceneStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        if (_useSeparateCutsceneStoryUiStyle)
        {
            style = _cutsceneStoryUiStyle;
            backgroundSprite = _cutsceneDialogueBackgroundSprite;
            return style != null || backgroundSprite != null;
        }

        return TryGetStoryUiStyle(out style, out backgroundSprite);
    }

    public int GetMatchScore(StoryData story, string storyId)
    {
        int score = 0;

        if (story != null && _storyAsset == story)
            score = 1000;

        string normalizedStoryId = Normalize(storyId);
        if (MatchesAny(_storyIds, normalizedStoryId))
            score = Mathf.Max(score, 500);

        if (story == null)
            return score;

        if (MatchesAny(_storyIds, Normalize(story.storyId)))
            score = Mathf.Max(score, 450);
        if (MatchesAny(_storyIds, Normalize(story.storyName)))
            score = Mathf.Max(score, 250);
        if (MatchesAny(_storyIds, Normalize(story.name)))
            score = Mathf.Max(score, 150);

        return score;
    }

    public void Validate()
    {
        if (_storyIds == null)
            _storyIds = new List<string>();

        for (int i = _storyIds.Count - 1; i >= 0; i--)
        {
            string value = Normalize(_storyIds[i]);
            if (string.IsNullOrEmpty(value))
            {
                _storyIds.RemoveAt(i);
                continue;
            }

            _storyIds[i] = value;
        }
    }

    static bool MatchesAny(List<string> values, string target)
    {
        if (values == null || string.IsNullOrEmpty(target))
            return false;

        for (int i = 0; i < values.Count; i++)
        {
            if (Normalize(values[i]) == target)
                return true;
        }

        return false;
    }

    static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }
}
