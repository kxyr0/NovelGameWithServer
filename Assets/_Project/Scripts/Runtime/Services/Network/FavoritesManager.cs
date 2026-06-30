using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Управление «Важным» (Избранным) — истории/эпизоды, которые игрок пометил.
///
/// Работает offline-first:
/// - Кеш хранится в PlayerPrefs (JSON-список)
/// - При наличии сети синхронизируется с сервером
///
/// Использование:
///   FavoritesManager.Instance.IsFavorite("ep_s1e2")  → bool
///   FavoritesManager.Instance.Add("ep_s1e2", "Встреча с Алексом")
///   FavoritesManager.Instance.Remove("ep_s1e2")
///   FavoritesManager.OnChanged — подписывайся на обновления UI
///
/// Подключение:
///   Добавь этот скрипт на тот же GameObject что и NetworkManager.
/// </summary>
public class FavoritesManager : MonoBehaviour
{
    public static FavoritesManager Instance;

    const string PREFS_KEY = "VN_FAVORITES";
    const int MaxFavorites = 200;
    const int MaxFavoritesPayloadChars = 200000;
    static bool ServerFavoritesApiEnabled => true;

    // Локальный кеш
    List<FavoriteItem> _favorites = new List<FavoriteItem>();

    // Событие — вызывается при любом изменении списка
    public static event Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Instance = null;
        OnChanged = null;
    }

    // ── Unity lifecycle ────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        LoadLocal();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        // Синхронизируем с сервером в фоне
        if (ServerFavoritesApiEnabled && NetworkManager.Instance != null)
            StartCoroutine(SyncFromServer());
    }

    // ── Public API ─────────────────────────────────────────────

    /// Проверить — в избранном ли эпизод (локально, быстро)
    public bool IsFavorite(string storyId)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        return !string.IsNullOrEmpty(storyId) && _favorites != null && _favorites.Exists(f => f != null && f.Matches(storyId));
    }

    /// Получить список избранных (копия)
    public List<FavoriteItem> GetAll()
        => _favorites != null ? new List<FavoriteItem>(_favorites) : new List<FavoriteItem>();

    /// Добавить в избранное
    public void Add(string storyId, string label = null)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        label = SaveDataSanitizer.SanitizeHistoryLine(label);
        if (string.IsNullOrEmpty(storyId))
            return;

        if (_favorites == null)
            _favorites = new List<FavoriteItem>();

        if (IsFavorite(storyId)) return;

        _favorites.Add(new FavoriteItem
        {
            storyId = storyId,
            episodeId = storyId,
            label     = label ?? "",
            addedAt   = DateTime.UtcNow.ToString("o")
        });

        SaveLocal();
        RaiseChanged();

        // Синхронизируем с сервером в фоне
        if (ServerFavoritesApiEnabled && NetworkManager.Instance != null)
            StartCoroutine(AddToServer(storyId));
    }

    /// Убрать из избранного
    public void Remove(string storyId)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (string.IsNullOrEmpty(storyId) || _favorites == null)
            return;

        int removed = _favorites.RemoveAll(f => f != null && f.Matches(storyId));
        if (removed == 0) return;

        SaveLocal();
        RaiseChanged();

        // Синхронизируем с сервером в фоне
        if (ServerFavoritesApiEnabled && NetworkManager.Instance != null)
            StartCoroutine(RemoveFromServer(storyId));
    }

    /// Переключить избранное (toggle)
    public void Toggle(string storyId, string label = null)
    {
        if (IsFavorite(storyId)) Remove(storyId);
        else Add(storyId, label);
    }

    // ── Синхронизация с сервером ───────────────────────────────

    IEnumerator SyncFromServer()
    {
        if (!ServerFavoritesApiEnabled) yield break;
        if (!NetworkManager.IsAuthenticated) yield break;
        if (NetworkManager.Instance == null) yield break;

        yield return NetworkManager.Instance.Get(ApiRoutes.PlayerFavorites, (json, err) =>
        {
            if (err != null) return; // offline — используем локальный кеш

            try
            {
                var response = NetworkJson.FromJson<FavoritesResponse>(json);
                var items = response?.GetItems();
                if (items != null && items.Count > 0)
                {
                    _favorites = NormalizeItems(items);
                    SaveLocal();
                    RaiseChanged();
                }
            }
            catch (Exception e)
            {
                AppLogger.Error(
                    AppLogCategory.Network,
                    nameof(FavoritesManager),
                    nameof(SyncFromServer),
                    "Failed to parse favorites response.",
                    e,
                    LogMetadata.Of("endpoint", ApiRoutes.PlayerFavorites),
                    recoverable: true);
            }
        });
    }

    IEnumerator AddToServer(string storyId)
    {
        if (!ServerFavoritesApiEnabled) yield break;
        if (!NetworkManager.IsAuthenticated) yield break;
        if (NetworkManager.Instance == null) yield break;

        var body = new FavoriteAddRequest
        {
            storyId = storyId
        };

        yield return NetworkManager.Instance.PostRaw(ApiRoutes.PlayerFavorites, NetworkJson.ToJson(body), (json, err) =>
        {
            if (err != null)
            {
                AppLogger.Warn(
                    AppLogCategory.Network,
                    nameof(FavoritesManager),
                    nameof(AddToServer),
                    "Failed to add favorite on server.",
                    LogMetadata.Of("endpoint", ApiRoutes.PlayerFavorites, "storyId", storyId, "error", err),
                    recoverable: true);
            }
        });
    }

    IEnumerator RemoveFromServer(string storyId)
    {
        if (!ServerFavoritesApiEnabled) yield break;
        if (!NetworkManager.IsAuthenticated) yield break;
        if (NetworkManager.Instance == null) yield break;

        yield return NetworkManager.Instance.Delete(ApiRoutes.PlayerFavoriteForStory(storyId), (json, err) =>
        {
            if (err != null)
            {
                AppLogger.Warn(
                    AppLogCategory.Network,
                    nameof(FavoritesManager),
                    nameof(RemoveFromServer),
                    "Failed to remove favorite on server.",
                    LogMetadata.Of("endpoint", ApiRoutes.PlayerFavorites, "storyId", storyId, "error", err),
                    recoverable: true);
            }
        });
    }

    // ── Локальное хранение ─────────────────────────────────────

    void SaveLocal()
    {
        if (_favorites == null)
            _favorites = new List<FavoriteItem>();

        _favorites = NormalizeItems(_favorites);
        var wrapper = new FavoritesWrapper { items = _favorites };
        try
        {
            string json = NetworkJson.ToJson(wrapper);
            if (json.Length > MaxFavoritesPayloadChars)
            {
                AppLogger.Warn(
                    AppLogCategory.SaveSystem,
                    nameof(FavoritesManager),
                    nameof(SaveLocal),
                    "Refused to save oversized favorites cache.",
                    LogMetadata.Of("payloadChars", json.Length, "maxPayloadChars", MaxFavoritesPayloadChars),
                    recoverable: true);
                return;
            }

            LocalSecurePrefs.SetString(PREFS_KEY, LocalSaveSecurity.FavoritesPurpose, json);
        }
        catch (Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.SaveSystem,
                nameof(FavoritesManager),
                nameof(SaveLocal),
                "Failed to save local favorites cache.",
                exception,
                LogMetadata.Of("prefsKey", PREFS_KEY),
                recoverable: true);
        }
    }

    void LoadLocal()
    {
        string json;
        try
        {
            json = LocalSecurePrefs.GetString(PREFS_KEY, LocalSaveSecurity.FavoritesPurpose, "");
        }
        catch (Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.SaveSystem,
                nameof(FavoritesManager),
                nameof(LoadLocal),
                "Failed to load local favorites cache.",
                exception,
                LogMetadata.Of("prefsKey", PREFS_KEY),
                recoverable: true);
            _favorites = new List<FavoriteItem>();
            return;
        }

        if (string.IsNullOrEmpty(json)) return;
        if (json.Length > MaxFavoritesPayloadChars)
        {
            LocalSecurePrefs.Delete(PREFS_KEY);
            _favorites = new List<FavoriteItem>();
            return;
        }
        try
        {
            var wrapper = NetworkJson.FromJson<FavoritesWrapper>(json);
            _favorites = NormalizeItems(wrapper?.items);
        }
        catch (Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.SaveSystem,
                nameof(FavoritesManager),
                nameof(LoadLocal),
                "Failed to parse local favorites cache.",
                exception,
                LogMetadata.Of("prefsKey", PREFS_KEY),
                recoverable: true);
            _favorites = new List<FavoriteItem>();
        }
    }

    void RaiseChanged()
    {
        var handlers = OnChanged;
        if (handlers == null)
            return;

        foreach (Action handler in handlers.GetInvocationList())
        {
            try
            {
                handler?.Invoke();
            }
            catch (Exception exception)
            {
                AppLogger.Error(
                    AppLogCategory.StoryUi,
                    nameof(FavoritesManager),
                    nameof(RaiseChanged),
                    "Favorites change listener failed.",
                    exception,
                    recoverable: true);
            }
        }
    }

    static List<FavoriteItem> NormalizeItems(List<FavoriteItem> source)
    {
        var result = new List<FavoriteItem>();
        if (source == null)
            return result;

        var seen = new HashSet<string>();
        foreach (var item in source)
        {
            if (item == null)
                continue;

            item.NormalizeIds();
            if (string.IsNullOrEmpty(item.storyId) || !seen.Add(item.storyId))
                continue;

            result.Add(item);
            if (result.Count >= MaxFavorites)
                break;
        }

        return result;
    }

}

// ── Модели ────────────────────────────────────────────────────

[Serializable]
public class FavoriteItem
{
    public string storyId;
    public string episodeId;
    public string label;
    public string addedAt;

    public bool Matches(string id)
    {
        return !string.IsNullOrEmpty(id) && (storyId == id || episodeId == id);
    }

    public void NormalizeIds()
    {
        if (string.IsNullOrEmpty(storyId))
            storyId = episodeId ?? "";
        if (string.IsNullOrEmpty(episodeId))
            episodeId = storyId ?? "";

        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        label = SaveDataSanitizer.SanitizeHistoryLine(label);
        addedAt = SaveDataSanitizer.SanitizeSavedAtIso(addedAt);
    }
}

[Serializable]
class FavoritesWrapper
{
    public List<FavoriteItem> items = new List<FavoriteItem>();
}

[Serializable]
class FavoritesResponse
{
    public List<FavoriteItem> favorites = new List<FavoriteItem>();
    public List<FavoriteItem> items = new List<FavoriteItem>();

    public List<FavoriteItem> GetItems()
    {
        if (favorites != null && favorites.Count > 0)
            return favorites;

        return items ?? new List<FavoriteItem>();
    }
}

[Serializable]
class FavoriteAddRequest
{
    public string storyId;
}
