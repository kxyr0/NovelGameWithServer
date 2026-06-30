using System.Collections.Generic;
using UnityEngine;

public class StoryHistory : MonoBehaviour
{
    public static StoryHistory Instance;

    [Header("History")]
    public int maxHistorySize = 50;

    [Header("Fast forward")]
    public int fastForwardSteps = 5;

    [Header("Bookmarks")]
    public int maxBookmarkSlides = 30;

    readonly LinkedList<BaseStoryNode> _past = new LinkedList<BaseStoryNode>();
    readonly Queue<BaseStoryNode> _future = new Queue<BaseStoryNode>();

    BookmarkSnapshot _bookmark;
    bool _hasBookmark;

    const string BOOKMARK_KEY_PREFIX = "VN_BOOKMARK_SNAPSHOT_";
    const string LEGACY_BOOKMARK_GUID_KEY = "VN_BOOKMARK_GUID";
    const string LEGACY_BOOKMARK_TIME_KEY = "VN_BOOKMARK_TIME";
    const int MaxBookmarkPayloadChars = LocalSaveSecurity.MaxProtectedPayloadChars;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void OnValidate()
    {
        maxHistorySize = Mathf.Max(1, maxHistorySize);
        fastForwardSteps = Mathf.Max(1, fastForwardSteps);
        maxBookmarkSlides = Mathf.Max(1, maxBookmarkSlides);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Push(BaseStoryNode node)
    {
        if (node == null) return;

        _past.AddLast(node);

        int limit = Mathf.Max(1, maxHistorySize);
        while (_past.Count > limit)
            _past.RemoveFirst();
    }

    public void Clear()
    {
        _past.Clear();
        _future.Clear();
        _hasBookmark = false;
        _bookmark = default;
    }

    public bool CanFastForward => _past.Count > 0;
    public int FastForwardSteps => fastForwardSteps;

    public void SaveBookmark(BaseStoryNode currentNode)
    {
        SaveBookmark(new SaveData
        {
            version = 1,
            currentNodeGuid = currentNode?.guid ?? "",
            savedAtIso = System.DateTime.UtcNow.ToString("o")
        });
    }

    public void SaveBookmark(SaveData snapshot)
    {
        snapshot = SaveDataSanitizer.SanitizeCopy(snapshot);
        if (snapshot == null || string.IsNullOrEmpty(snapshot.currentNodeGuid))
            return;

        if (string.IsNullOrEmpty(snapshot.savedAtIso))
            snapshot.savedAtIso = System.DateTime.UtcNow.ToString("o");

        _hasBookmark = true;
        _bookmark = new BookmarkSnapshot
        {
            nodeGuid = snapshot.currentNodeGuid,
            episodeId = snapshot.episodeId,
            storyId = snapshot.storyId,
            savedAt = ParseDate(snapshot.savedAtIso),
            saveData = snapshot
        };

        try
        {
            string json = NetworkJson.ToSaveDataJson(snapshot);
            if (!SaveDataSanitizer.IsSerializedSizeAllowed(json))
            {
                Debug.LogWarning("[Bookmark] Refused to save oversized local bookmark.");
                return;
            }

            string protectedJson = LocalSaveSecurity.ProtectJson(json, LocalSaveSecurity.BookmarkPurpose);
            if (string.IsNullOrEmpty(protectedJson))
            {
                Debug.LogWarning("[Bookmark] Refused to save invalid protected bookmark.");
                return;
            }

            string bookmarkKey = GetBookmarkKey(snapshot.storyId);
            PlayerPrefs.SetString(bookmarkKey, protectedJson);
            LocalSecurePrefs.MarkSecure(bookmarkKey);
            LocalSecurePrefs.SetString(LEGACY_BOOKMARK_GUID_KEY, GetLegacyBookmarkPurpose("guid"), _bookmark.nodeGuid);
            LocalSecurePrefs.SetString(LEGACY_BOOKMARK_TIME_KEY, GetLegacyBookmarkPurpose("time"), _bookmark.savedAt.ToString("o"));
            PlayerPrefs.Save();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[Bookmark] Failed to save local bookmark: " + exception.Message);
        }

        Debug.Log($"[Bookmark] Saved node {_bookmark.nodeGuid}");
    }

    public void LoadBookmarkFromPrefs()
    {
        LoadBookmarkFromPrefs(null);
    }

    public bool LoadBookmarkFromPrefs(string storyId)
    {
        string json = SafeGetString(GetBookmarkKey(storyId), "");

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                if (json.Length > MaxBookmarkPayloadChars)
                {
                    LocalSecurePrefs.Delete(GetBookmarkKey(storyId));
                    json = "";
                    throw new System.Exception("Local bookmark snapshot is too large.");
                }

                if (!LocalSaveSecurity.TryUnprotectJson(
                        json,
                        LocalSaveSecurity.BookmarkPurpose,
                        out string snapshotJson,
                        out bool wasProtected))
                {
                    LocalSecurePrefs.Delete(GetBookmarkKey(storyId));
                    throw new System.Exception("Local bookmark integrity check failed.");
                }

                if (!wasProtected && LocalSecurePrefs.HasSecureMarker(GetBookmarkKey(storyId)))
                {
                    LocalSecurePrefs.Delete(GetBookmarkKey(storyId));
                    throw new System.Exception("Local bookmark snapshot was downgraded.");
                }

                if (wasProtected)
                    LocalSecurePrefs.EnsureSecureMarker(GetBookmarkKey(storyId));

                var snapshot = NetworkJson.FromSaveDataJson(snapshotJson);
                if (snapshot != null && !string.IsNullOrEmpty(snapshot.currentNodeGuid))
                {
                    _hasBookmark = true;
                    _bookmark = new BookmarkSnapshot
                    {
                        nodeGuid = snapshot.currentNodeGuid,
                        episodeId = snapshot.episodeId,
                        storyId = snapshot.storyId,
                        savedAt = ParseDate(snapshot.savedAtIso),
                        saveData = snapshot
                    };

                    if (!wasProtected)
                        ResaveBookmarkSnapshot(snapshot);

                    return true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Bookmark] Failed to parse local snapshot: " + e.Message);
            }
        }

        string guid = SaveDataSanitizer.SanitizeIdentifier(LocalSecurePrefs.GetString(LEGACY_BOOKMARK_GUID_KEY, GetLegacyBookmarkPurpose("guid"), ""));
        string timeStr = LocalSecurePrefs.GetString(LEGACY_BOOKMARK_TIME_KEY, GetLegacyBookmarkPurpose("time"), "");

        if (string.IsNullOrEmpty(guid)) return false;

        _hasBookmark = true;
        _bookmark = new BookmarkSnapshot
        {
            nodeGuid = guid,
            storyId = storyId,
            savedAt = ParseDate(timeStr),
            saveData = new SaveData
            {
                version = 1,
                storyId = storyId,
                currentNodeGuid = guid,
                savedAtIso = timeStr
            }
        };

        return true;
    }

    public bool HasBookmark => _hasBookmark;

    public BookmarkSnapshot GetBookmark() => _bookmark;

    public void ClearBookmark()
    {
        string storyId = _bookmark.storyId;

        _hasBookmark = false;
        _bookmark = default;
        try
        {
            LocalSecurePrefs.Delete(GetBookmarkKey(storyId));
            LocalSecurePrefs.Delete(LEGACY_BOOKMARK_GUID_KEY);
            LocalSecurePrefs.Delete(LEGACY_BOOKMARK_TIME_KEY);
            PlayerPrefs.Save();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[Bookmark] Failed to clear local bookmark: " + exception.Message);
        }
    }

    public void ApplyServerBookmark(SaveData snapshot)
    {
        SaveBookmark(SaveDataSanitizer.SanitizeCopy(snapshot));
    }

    void ResaveBookmarkSnapshot(SaveData snapshot)
    {
        try
        {
            string json = NetworkJson.ToSaveDataJson(snapshot);
            string protectedJson = LocalSaveSecurity.ProtectJson(json, LocalSaveSecurity.BookmarkPurpose);
            if (string.IsNullOrEmpty(protectedJson))
                return;

            string bookmarkKey = GetBookmarkKey(snapshot.storyId);
            PlayerPrefs.SetString(bookmarkKey, protectedJson);
            LocalSecurePrefs.MarkSecure(bookmarkKey);
            PlayerPrefs.Save();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[Bookmark] Failed to migrate local bookmark: " + exception.Message);
        }
    }

    string GetBookmarkKey(string storyId)
    {
        return BOOKMARK_KEY_PREFIX + SaveDataSanitizer.SafeKeyPart(storyId);
    }

    static string GetLegacyBookmarkPurpose(string suffix)
    {
        return LocalSaveSecurity.BookmarkPurpose + ":legacy:" + SaveDataSanitizer.SanitizeIdentifier(suffix);
    }

    string SafeGetString(string key, string defaultValue)
    {
        try
        {
            return PlayerPrefs.GetString(key, defaultValue);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[Bookmark] Failed to read local bookmark key: " + exception.Message);
            return defaultValue;
        }
    }

    System.DateTime ParseDate(string value)
    {
        if (System.DateTime.TryParse(value, out var dt))
            return dt;

        return System.DateTime.Now;
    }

    public struct BookmarkSnapshot
    {
        public string nodeGuid;
        public string episodeId;
        public string storyId;
        public System.DateTime savedAt;
        public SaveData saveData;
    }
}
