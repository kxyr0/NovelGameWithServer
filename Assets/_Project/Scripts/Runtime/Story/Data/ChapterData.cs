using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "VN/Chapter")]
public class ChapterData : ScriptableObject
{
    [SerializeField]
    [FormerlySerializedAs("chapterId")]
    private string _chapterId;

    [SerializeField]
    [FormerlySerializedAs("chapterName")]
    private string _chapterName;

    [SerializeField]
    [FormerlySerializedAs("graph")]
    private StoryGraph _graph;

    [SerializeField]
    private TextAsset _jsonGraph;

    [SerializeField]
    private StoryJsonAssetLibrary _jsonAssetLibrary;

    [SerializeField]
    [FormerlySerializedAs("isPremium")]
    private bool _isPremium;

    [SerializeField]
    [FormerlySerializedAs("unlockCost")]
    private int _unlockCost;

    public string ChapterId => _chapterId;
    public string ChapterName => _chapterName;
    public StoryGraph Graph => _graph;
    public TextAsset JsonGraph => _jsonGraph;
    public StoryJsonAssetLibrary JsonAssetLibrary => _jsonAssetLibrary;
    public bool IsPremium => _isPremium;
    public int UnlockCost => _unlockCost;

    public string chapterId => _chapterId;
    public string chapterName => _chapterName;
    public StoryGraph graph => _graph;
    public TextAsset jsonGraph => _jsonGraph;
    public StoryJsonAssetLibrary jsonAssetLibrary => _jsonAssetLibrary;
    public bool isPremium => _isPremium;
    public int unlockCost => _unlockCost;

    public void Configure(string chapterId, string chapterName, StoryGraph graph, bool isPremium, int unlockCost)
    {
        Configure(chapterId, chapterName, graph, null, null, isPremium, unlockCost);
    }

    public void Configure(string chapterId, string chapterName, StoryGraph graph, TextAsset jsonGraph, bool isPremium, int unlockCost)
    {
        Configure(chapterId, chapterName, graph, jsonGraph, null, isPremium, unlockCost);
    }

    public void Configure(
        string chapterId,
        string chapterName,
        StoryGraph graph,
        TextAsset jsonGraph,
        StoryJsonAssetLibrary jsonAssetLibrary,
        bool isPremium,
        int unlockCost)
    {
        _chapterId = SaveDataSanitizer.SanitizeIdentifier(chapterId);
        _chapterName = SaveDataSanitizer.SanitizeHistoryLine(chapterName);
        _graph = graph;
        _jsonGraph = jsonGraph;
        _jsonAssetLibrary = jsonAssetLibrary;
        _isPremium = isPremium;
        _unlockCost = SaveDataSanitizer.ClampCurrencyValue(unlockCost);
    }
}
