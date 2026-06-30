using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum PreStorySetupLaunchMode
{
    Disabled = 0,
    NameOnly = 1,
    NameAndWardrobe = 2
}

[CreateAssetMenu(menuName = "VN/Story")]
public class StoryData : ScriptableObject
{
    private static readonly IReadOnlyList<ChapterData> EmptyChapters = new List<ChapterData>();

    [SerializeField]
    [FormerlySerializedAs("storyId")]
    private string _storyId;

    [SerializeField]
    [FormerlySerializedAs("storyName")]
    private string _storyName;

    [Header("Chapters")]
    [SerializeField]
    private List<ChapterData> _chapters = new List<ChapterData>();

    [Header("Story UI")]
    [Tooltip("Reusable Story UI style for this story.")]
    [FormerlySerializedAs("_dialoguePanelStyle")]
    [SerializeField] private StoryUiStyle _storyUiStyle;

    [Tooltip("Быстрая замена Source Image у фона диалоговой плашки. Если задан и стиль, этот спрайт имеет приоритет над спрайтом из стиля.")]
    [SerializeField] private Sprite _dialogueBackgroundSprite;

    [Tooltip("Включи, если катсцены должны использовать отдельный стиль плашки. Если выключено, катсцены берут обычный стиль истории.")]
    [FormerlySerializedAs("_useSeparateCutsceneDialoguePanelStyle")]
    [SerializeField] private bool _useSeparateCutsceneStoryUiStyle;

    [Tooltip("Отдельный стиль фона диалоговой плашки для катсцен этой истории.")]
    [FormerlySerializedAs("_cutsceneDialoguePanelStyle")]
    [SerializeField] private StoryUiStyle _cutsceneStoryUiStyle;

    [Tooltip("Быстрая замена Source Image у фона плашки катсцен.")]
    [SerializeField] private Sprite _cutsceneDialogueBackgroundSprite;

    [Header("Pre Story Setup")]
    [Tooltip("Optional setup before the first story node. Leave Disabled when name/wardrobe nodes already exist in JSON.")]
    [SerializeField] private PreStorySetupLaunchMode _preStorySetupLaunchMode = PreStorySetupLaunchMode.Disabled;

    [Header("Legacy Seasons")]
    [SerializeField, HideInInspector]
    [FormerlySerializedAs("seasons")]
    private List<SeasonData> _seasons = new List<SeasonData>();

    public string StoryId => _storyId;
    public string StoryName => _storyName;
    public IReadOnlyList<ChapterData> Chapters => GetEffectiveChapters();
    public IReadOnlyList<SeasonData> Seasons => _seasons;
    public PreStorySetupLaunchMode PreStorySetupLaunchMode => _preStorySetupLaunchMode;
    public bool RunsPreStorySetupBeforeStart => _preStorySetupLaunchMode != PreStorySetupLaunchMode.Disabled;
    public bool PreStorySetupIncludesWardrobe => _preStorySetupLaunchMode == PreStorySetupLaunchMode.NameAndWardrobe;

    public string storyId => _storyId;
    public string storyName => _storyName;
    public IReadOnlyList<ChapterData> chapters => GetEffectiveChapters();
    public IReadOnlyList<SeasonData> seasons => _seasons;

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

    public bool UsesLegacySeasons => (_chapters == null || _chapters.Count == 0) && _seasons != null && _seasons.Count > 0;

    public int IndexOfSeason(SeasonData season)
    {
        return _seasons != null ? _seasons.IndexOf(season) : -1;
    }

    public int IndexOfChapter(ChapterData chapter)
    {
        var list = GetEffectiveChapters();
        return list != null ? new List<ChapterData>(list).IndexOf(chapter) : -1;
    }

    public bool TryGetChapterIndex(int legacySeasonIndex, int legacyChapterIndex, out int chapterIndex)
    {
        chapterIndex = -1;

        if (!UsesLegacySeasons)
        {
            if (legacySeasonIndex <= 0 && legacyChapterIndex >= 0 && legacyChapterIndex < chapters.Count)
            {
                chapterIndex = legacyChapterIndex;
                return true;
            }

            return false;
        }

        int flatIndex = 0;
        if (_seasons == null)
            return false;

        for (int seasonIndex = 0; seasonIndex < _seasons.Count; seasonIndex++)
        {
            var season = _seasons[seasonIndex];
            if (season == null || season.chapters == null)
                continue;

            for (int itemIndex = 0; itemIndex < season.chapters.Count; itemIndex++)
            {
                if (seasonIndex == legacySeasonIndex && itemIndex == legacyChapterIndex)
                {
                    chapterIndex = flatIndex;
                    return true;
                }

                flatIndex++;
            }
        }

        return false;
    }

    public void Configure(string storyId, string storyName, IEnumerable<ChapterData> chapters)
    {
        _storyId = storyId ?? "";
        _storyName = storyName ?? "";
        _chapters = chapters != null ? new List<ChapterData>(chapters) : new List<ChapterData>();
    }

    public void Configure(string storyId, string storyName, IEnumerable<SeasonData> seasons)
    {
        _storyId = storyId ?? "";
        _storyName = storyName ?? "";
        _seasons = seasons != null ? new List<SeasonData>(seasons) : new List<SeasonData>();

        if (_chapters == null || _chapters.Count == 0)
            _chapters = FlattenSeasonChapters(_seasons);
    }

    void OnValidate()
    {
        if (_chapters == null)
            _chapters = new List<ChapterData>();
        if (_seasons == null)
            _seasons = new List<SeasonData>();

        if (_chapters.Count == 0 && _seasons.Count > 0)
            _chapters = FlattenSeasonChapters(_seasons);
    }

    private IReadOnlyList<ChapterData> GetEffectiveChapters()
    {
        if (_chapters != null && _chapters.Count > 0)
            return _chapters;

        if (_seasons != null && _seasons.Count > 0)
            return FlattenSeasonChapters(_seasons);

        return EmptyChapters;
    }

    private static List<ChapterData> FlattenSeasonChapters(IEnumerable<SeasonData> seasons)
    {
        var result = new List<ChapterData>();
        if (seasons == null)
            return result;

        foreach (var season in seasons)
        {
            if (season == null || season.chapters == null)
                continue;

            foreach (var chapter in season.chapters)
            {
                if (chapter != null)
                    result.Add(chapter);
            }
        }

        return result;
    }
}
