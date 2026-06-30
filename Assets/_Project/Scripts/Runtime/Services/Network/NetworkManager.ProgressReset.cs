public sealed partial class NetworkManager
{
    public static void ClearLocalProgressCache(bool clearPendingSync = true)
    {
        LastProgressNodeGuid = "";
        LastProgressEpisodeId = "";
        LastProgressSnapshotJson = "";
        LastProgressRawJson = "";
        LastProgressUpdatedAtIso = "";

        _lastUnlockedEpisodes.Clear();
        _lastProgressStats.Clear();
        _lastProgressFlags.Clear();

        if (!clearPendingSync)
            return;

        _pendingProgress.Clear();
        _pendingBookmarks.Clear();
        _pendingSyncStore.ClearAll();
    }
}
