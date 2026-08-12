#if UNITY_EDITOR
using UnityEditor;

public sealed class ContentReleasePublisherPrefs
{
    private const string Prefix = "VN.ContentReleasePublisher.";

    public string EnvironmentId = DeploymentEnvironmentIds.Stage;
    public string Status = ContentReleaseStatus.Staging;
    public string StoryId = "";
    public string EpisodeId = "";
    public string ContentVersion = "";
    public string CatalogUrl = "";
    public string LoadPath = "";
    public string ManifestUrl = "";
    public string ManifestHash = "";
    public string BuildTarget = "";
    public string MinAppVersion = "";
    public string Notes = "";
    public string BaseUrl = "";
    public bool AllowUnsigned;

    public static ContentReleasePublisherPrefs Load()
    {
        return new ContentReleasePublisherPrefs
        {
            EnvironmentId = EditorPrefs.GetString(Key(nameof(EnvironmentId)), DeploymentEnvironmentIds.Stage),
            Status = EditorPrefs.GetString(Key(nameof(Status)), ContentReleaseStatus.Staging),
            StoryId = EditorPrefs.GetString(Key(nameof(StoryId)), ""),
            EpisodeId = EditorPrefs.GetString(Key(nameof(EpisodeId)), ""),
            ContentVersion = EditorPrefs.GetString(Key(nameof(ContentVersion)), ""),
            CatalogUrl = EditorPrefs.GetString(Key(nameof(CatalogUrl)), ""),
            LoadPath = EditorPrefs.GetString(Key(nameof(LoadPath)), ""),
            ManifestUrl = EditorPrefs.GetString(Key(nameof(ManifestUrl)), ""),
            ManifestHash = EditorPrefs.GetString(Key(nameof(ManifestHash)), ""),
            BuildTarget = EditorPrefs.GetString(Key(nameof(BuildTarget)), ""),
            MinAppVersion = EditorPrefs.GetString(Key(nameof(MinAppVersion)), ""),
            Notes = EditorPrefs.GetString(Key(nameof(Notes)), ""),
            BaseUrl = EditorPrefs.GetString(Key(nameof(BaseUrl)), ""),
            AllowUnsigned = EditorPrefs.GetBool(Key(nameof(AllowUnsigned)), false)
        };
    }

    public void Save()
    {
        EditorPrefs.SetString(Key(nameof(EnvironmentId)), EnvironmentId ?? "");
        EditorPrefs.SetString(Key(nameof(Status)), Status ?? "");
        EditorPrefs.SetString(Key(nameof(StoryId)), StoryId ?? "");
        EditorPrefs.SetString(Key(nameof(EpisodeId)), EpisodeId ?? "");
        EditorPrefs.SetString(Key(nameof(ContentVersion)), ContentVersion ?? "");
        EditorPrefs.SetString(Key(nameof(CatalogUrl)), CatalogUrl ?? "");
        EditorPrefs.SetString(Key(nameof(LoadPath)), LoadPath ?? "");
        EditorPrefs.SetString(Key(nameof(ManifestUrl)), ManifestUrl ?? "");
        EditorPrefs.SetString(Key(nameof(ManifestHash)), ManifestHash ?? "");
        EditorPrefs.SetString(Key(nameof(BuildTarget)), BuildTarget ?? "");
        EditorPrefs.SetString(Key(nameof(MinAppVersion)), MinAppVersion ?? "");
        EditorPrefs.SetString(Key(nameof(Notes)), Notes ?? "");
        EditorPrefs.SetString(Key(nameof(BaseUrl)), BaseUrl ?? "");
        EditorPrefs.SetBool(Key(nameof(AllowUnsigned)), AllowUnsigned);
    }

    private static string Key(string name)
    {
        return Prefix + name;
    }
}
#endif
