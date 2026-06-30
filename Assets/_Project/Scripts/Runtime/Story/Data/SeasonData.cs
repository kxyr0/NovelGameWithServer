using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "VN/Season")]
public class SeasonData : ScriptableObject
{
    [SerializeField]
    [FormerlySerializedAs("seasonId")]
    private string _seasonId;

    [SerializeField]
    [FormerlySerializedAs("seasonName")]
    private string _seasonName;

    [SerializeField]
    [FormerlySerializedAs("chapters")]
    private List<ChapterData> _chapters = new List<ChapterData>();

    public string SeasonId => _seasonId;
    public string SeasonName => _seasonName;
    public IReadOnlyList<ChapterData> Chapters => _chapters;

    public string seasonId => _seasonId;
    public string seasonName => _seasonName;
    public IReadOnlyList<ChapterData> chapters => _chapters;

    public int IndexOfChapter(ChapterData chapter)
    {
        return _chapters != null ? _chapters.IndexOf(chapter) : -1;
    }

    public void Configure(string seasonId, string seasonName, IEnumerable<ChapterData> chapters)
    {
        _seasonId = seasonId ?? "";
        _seasonName = seasonName ?? "";
        _chapters = chapters != null ? new List<ChapterData>(chapters) : new List<ChapterData>();
    }

    void OnValidate()
    {
        if (_chapters == null)
            _chapters = new List<ChapterData>();
    }
}
