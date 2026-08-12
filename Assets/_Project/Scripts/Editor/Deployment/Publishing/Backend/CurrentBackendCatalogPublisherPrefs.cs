#if UNITY_EDITOR
using UnityEditor;

public sealed class CurrentBackendCatalogPublisherPrefs
{
    private const string Prefix = "VN.CurrentBackendCatalogPublisher.";

    public string StoryId = "";
    public string StoryTitle = "";
    public string SeasonId = "";
    public string SeasonTitle = "";
    public string EpisodeId = "";
    public string EpisodeTitle = "";
    public string BaseUrl = ApiRoutes.BaseUrl;
    public int SeasonOrder = 1;
    public int EpisodeOrder = 1;
    public int CandleCost;
    public bool AllowHeroRename = true;
    public bool IsPremium;
    public bool GeoRestricted;
    public bool AllowUnsigned;

    public static CurrentBackendCatalogPublisherPrefs Load()
    {
        return new CurrentBackendCatalogPublisherPrefs
        {
            StoryId = EditorPrefs.GetString(Key(nameof(StoryId)), ""),
            StoryTitle = EditorPrefs.GetString(Key(nameof(StoryTitle)), ""),
            SeasonId = EditorPrefs.GetString(Key(nameof(SeasonId)), ""),
            SeasonTitle = EditorPrefs.GetString(Key(nameof(SeasonTitle)), ""),
            EpisodeId = EditorPrefs.GetString(Key(nameof(EpisodeId)), ""),
            EpisodeTitle = EditorPrefs.GetString(Key(nameof(EpisodeTitle)), ""),
            BaseUrl = EditorPrefs.GetString(Key(nameof(BaseUrl)), ApiRoutes.BaseUrl),
            SeasonOrder = EditorPrefs.GetInt(Key(nameof(SeasonOrder)), 1),
            EpisodeOrder = EditorPrefs.GetInt(Key(nameof(EpisodeOrder)), 1),
            CandleCost = EditorPrefs.GetInt(Key(nameof(CandleCost)), 0),
            AllowHeroRename = EditorPrefs.GetBool(Key(nameof(AllowHeroRename)), true),
            IsPremium = EditorPrefs.GetBool(Key(nameof(IsPremium)), false),
            GeoRestricted = EditorPrefs.GetBool(Key(nameof(GeoRestricted)), false),
            AllowUnsigned = EditorPrefs.GetBool(Key(nameof(AllowUnsigned)), false)
        };
    }

    public void Save()
    {
        EditorPrefs.SetString(Key(nameof(StoryId)), StoryId ?? "");
        EditorPrefs.SetString(Key(nameof(StoryTitle)), StoryTitle ?? "");
        EditorPrefs.SetString(Key(nameof(SeasonId)), SeasonId ?? "");
        EditorPrefs.SetString(Key(nameof(SeasonTitle)), SeasonTitle ?? "");
        EditorPrefs.SetString(Key(nameof(EpisodeId)), EpisodeId ?? "");
        EditorPrefs.SetString(Key(nameof(EpisodeTitle)), EpisodeTitle ?? "");
        EditorPrefs.SetString(Key(nameof(BaseUrl)), BaseUrl ?? "");
        EditorPrefs.SetInt(Key(nameof(SeasonOrder)), SeasonOrder);
        EditorPrefs.SetInt(Key(nameof(EpisodeOrder)), EpisodeOrder);
        EditorPrefs.SetInt(Key(nameof(CandleCost)), CandleCost);
        EditorPrefs.SetBool(Key(nameof(AllowHeroRename)), AllowHeroRename);
        EditorPrefs.SetBool(Key(nameof(IsPremium)), IsPremium);
        EditorPrefs.SetBool(Key(nameof(GeoRestricted)), GeoRestricted);
        EditorPrefs.SetBool(Key(nameof(AllowUnsigned)), AllowUnsigned);
    }

    private static string Key(string name)
    {
        return Prefix + name;
    }
}
#endif
