using System;

[Serializable]
public sealed class ContentReleaseDescriptor
{
    public string storyId = "";
    public string episodeId = "";
    public string contentVersion = "";
    public string status = ContentReleaseStatus.Draft;
    public string channel = ContentReleaseChannel.Stage;
    public string addressablesCatalogUrl = "";
    public string addressablesRemoteLoadPath = "";
    public string addressablesManifestUrl = "";
    public string addressablesManifestHash = "";
    public string buildTarget = "";
    public string minAppVersion = "";
    public string notes = "";
    public string updatedAtIso = "";

    public void Normalize()
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        contentVersion = SaveDataSanitizer.SanitizeIdentifier(contentVersion);
        status = ContentReleaseStatus.Normalize(status);
        channel = ContentReleaseChannel.Normalize(channel);
        addressablesCatalogUrl = NormalizePath(addressablesCatalogUrl);
        addressablesRemoteLoadPath = NormalizePath(addressablesRemoteLoadPath);
        addressablesManifestUrl = NormalizePath(addressablesManifestUrl);
        addressablesManifestHash = SaveDataSanitizer.SanitizeIdentifier(addressablesManifestHash);
        buildTarget = SaveDataSanitizer.SanitizeIdentifier(buildTarget);
        minAppVersion = SaveDataSanitizer.SanitizeIdentifier(minAppVersion);
        notes = SaveDataSanitizer.SanitizeHistoryLine(notes);
        updatedAtIso = SaveDataSanitizer.SanitizeSavedAtIso(updatedAtIso);
    }

    public ContentReleaseDescriptor CloneNormalized()
    {
        var copy = new ContentReleaseDescriptor
        {
            storyId = storyId,
            episodeId = episodeId,
            contentVersion = contentVersion,
            status = status,
            channel = channel,
            addressablesCatalogUrl = addressablesCatalogUrl,
            addressablesRemoteLoadPath = addressablesRemoteLoadPath,
            addressablesManifestUrl = addressablesManifestUrl,
            addressablesManifestHash = addressablesManifestHash,
            buildTarget = buildTarget,
            minAppVersion = minAppVersion,
            notes = notes,
            updatedAtIso = updatedAtIso
        };

        copy.Normalize();
        return copy;
    }

    private static string NormalizePath(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : SaveDataSanitizer.SanitizeHistoryLine(value).TrimEnd('/');
    }
}
