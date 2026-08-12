#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public sealed class ContentReleaseMockStore
{
    private readonly object _lock = new object();
    private readonly Dictionary<string, ContentReleaseDescriptor> _releases =
        new Dictionary<string, ContentReleaseDescriptor>(StringComparer.Ordinal);

    public int Count
    {
        get
        {
            lock (_lock)
                return _releases.Count;
        }
    }

    public ContentReleaseMockHttpResponse Upsert(ContentReleaseDescriptor release)
    {
        DeploymentEnvironmentValidationResult validation = ContentReleasePolicy.Validate(release);
        if (!validation.IsValid)
            return Error(422, validation.Message);

        release = release.CloneNormalized();
        lock (_lock)
            _releases[Key(release)] = release;

        return Json(200, "{\"ok\":true,\"release\":" + ReleaseJson(release) + "}");
    }

    public ContentReleaseMockHttpResponse List(string storyId, string episodeId)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        var items = new List<string>();

        lock (_lock)
        {
            foreach (ContentReleaseDescriptor release in _releases.Values)
            {
                if (!string.IsNullOrEmpty(storyId) && release.storyId != storyId)
                    continue;
                if (!string.IsNullOrEmpty(episodeId) && release.episodeId != episodeId)
                    continue;
                items.Add(ReleaseJson(release));
            }
        }

        return Json(200, "{\"ok\":true,\"count\":" + items.Count + ",\"releases\":[" + string.Join(",", items) + "]}");
    }

    public ContentReleaseMockHttpResponse Promote(string body)
    {
        ContentReleaseDescriptor source = FindCommandSource(body);
        if (source == null)
            return Error(404, "stage_release_not_found");

        var promoted = source.CloneNormalized();
        promoted.status = ContentReleaseStatus.Published;
        promoted.channel = ContentReleaseChannel.Production;
        DeploymentEnvironmentPreset prod = ContentReleaseUploadDestinationSettings.ApplyToPreset(
            DeploymentEnvironmentPresets.Production);
        promoted.addressablesRemoteLoadPath = prod.AddressablesLoadPath;
        if (!string.IsNullOrWhiteSpace(promoted.buildTarget))
        {
            promoted.addressablesManifestUrl = ContentReleaseManifestBuilder.ResolveTokens(
                prod.AddressablesLoadPath,
                promoted.buildTarget).TrimEnd('/') + "/" + ContentReleaseManifestBuilder.ManifestFileName;
        }

        promoted.updatedAtIso = DateTime.UtcNow.ToString("o");
        return Upsert(promoted);
    }

    public ContentReleaseMockHttpResponse Rollback(string body)
    {
        string storyId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(body, "storyId"));
        string episodeId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(body, "episodeId"));
        string version = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(body, "contentVersion"));
        ContentReleaseDescriptor release = Find(storyId, episodeId, version, ContentReleaseChannel.Production);
        if (release == null)
            return Error(404, "prod_release_not_found");

        release = release.CloneNormalized();
        release.status = ContentReleaseStatus.Published;
        release.updatedAtIso = DateTime.UtcNow.ToString("o");
        return Upsert(release);
    }

    private ContentReleaseDescriptor FindCommandSource(string body)
    {
        string storyId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(body, "storyId"));
        string episodeId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(body, "episodeId"));
        string version = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(body, "contentVersion"));
        return Find(storyId, episodeId, version, ContentReleaseChannel.Stage);
    }

    private ContentReleaseDescriptor Find(string storyId, string episodeId, string version, string channel)
    {
        lock (_lock)
        {
            foreach (ContentReleaseDescriptor release in _releases.Values)
            {
                if (release.storyId == storyId &&
                    release.episodeId == episodeId &&
                    release.contentVersion == version &&
                    release.channel == channel)
                {
                    return release;
                }
            }
        }

        return null;
    }

    private static string Key(ContentReleaseDescriptor release)
    {
        return release.storyId + "\n" + release.episodeId + "\n" +
               release.contentVersion + "\n" + release.channel;
    }

    private static ContentReleaseMockHttpResponse Error(int code, string error)
    {
        return Json(code, "{\"ok\":false,\"error\":\"" + NetworkJson.Escape(error) + "\"}");
    }

    private static ContentReleaseMockHttpResponse Json(int code, string body)
    {
        return ContentReleaseMockHttpResponse.Json(code, body);
    }

    public static string ReleaseJson(ContentReleaseDescriptor release)
    {
        return "{" +
               "\"storyId\":\"" + NetworkJson.Escape(release.storyId) + "\"," +
               "\"episodeId\":\"" + NetworkJson.Escape(release.episodeId) + "\"," +
               "\"contentVersion\":\"" + NetworkJson.Escape(release.contentVersion) + "\"," +
               "\"status\":\"" + NetworkJson.Escape(release.status) + "\"," +
               "\"channel\":\"" + NetworkJson.Escape(release.channel) + "\"," +
               "\"addressablesCatalogUrl\":\"" + NetworkJson.Escape(release.addressablesCatalogUrl) + "\"," +
               "\"addressablesRemoteLoadPath\":\"" + NetworkJson.Escape(release.addressablesRemoteLoadPath) + "\"," +
               "\"addressablesManifestUrl\":\"" + NetworkJson.Escape(release.addressablesManifestUrl) + "\"," +
               "\"addressablesManifestHash\":\"" + NetworkJson.Escape(release.addressablesManifestHash) + "\"," +
               "\"buildTarget\":\"" + NetworkJson.Escape(release.buildTarget) + "\"," +
               "\"minAppVersion\":\"" + NetworkJson.Escape(release.minAppVersion) + "\"," +
               "\"notes\":\"" + NetworkJson.Escape(release.notes) + "\"," +
               "\"updatedAtIso\":\"" + NetworkJson.Escape(release.updatedAtIso) + "\"}";
    }
}
#endif
