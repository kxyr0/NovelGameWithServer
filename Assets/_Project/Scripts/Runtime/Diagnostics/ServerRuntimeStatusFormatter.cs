using System;
using System.Text;
using UnityEngine;

static class ServerRuntimeStatusFormatter
{
    const string EmptyValue = "нет";
    const float CacheProbeIntervalSeconds = 3f;
    static readonly StringBuilder Builder = new StringBuilder(1600);
    static string _cacheProbeEpisodeId = "";
    static float _nextCacheProbeTime;
    static bool _lastCacheProbeResult;
    static RemoteEpisodeGraphCacheEntry _lastCacheEntry;

    public static string Build(KeyCode toggleKey)
    {
        var config = SafeLoadConfig();
        var storyManager = StoryManager.Instance;
        string episodeId = FirstNonEmpty(
            storyManager != null ? storyManager.CurrentEpisodeId : "",
            storyManager != null ? storyManager.CurrentChapterId : "");

        Builder.Length = 0;
        AppendHeader("Коротко");
        AppendStatus("Сервер", NetworkManager.IsOnline, NetworkManager.LastNetworkError);
        AppendStatus("Auth", NetworkManager.IsAuthenticated);
        AppendLine("Каталог", CountCatalogSeasons() + " сезон(ов), " + CountCatalogEpisodes() + " эп.");
        AppendLine("Эпизод", episodeId);
        AppendLine("Источник графа", storyManager != null ? storyManager.LastResolvedGraphSource : "");
        AppendEpisodeQuickState(episodeId);

        AppendHeader("Сервер и загрузка");
        AppendLine("Клавиша панели", toggleKey == KeyCode.None ? "не задана" : toggleKey.ToString());
        AppendStatus("NetworkManager найден", NetworkManager.Instance != null);
        AppendStatus("Сервер отвечает", NetworkManager.IsOnline, NetworkManager.LastNetworkError);
        AppendStatus("Авторизация выполнена", NetworkManager.IsAuthenticated);
        AppendStatus("Auth-поток завершен", NetworkManager.AuthFlowCompleted);
        AppendLine("Среда", FirstNonEmpty(NetworkManager.ActiveEnvironmentId, ResolveEnvironmentId(config)));
        AppendLine("Адрес сервера", FirstNonEmpty(NetworkManager.ActiveBaseUrl, ResolveBaseUrl(config)));
        AppendLine("Addressables CDN", ResolveAddressablesPath(config));
        AppendStatus("Удаленный JSON включен", PrototypeFeatureFlags.RemoteEpisodeGraphsEnabled);
        AppendStatus("Есть очередь синхронизации", NetworkManager.HasPendingSync);

        AppendHeader("Каталог");
        AppendLine("Сезонов", CountCatalogSeasons().ToString());
        AppendLine("Эпизодов", CountCatalogEpisodes().ToString());

        AppendHeader("Текущая история");
        AppendLine("История", storyManager != null ? storyManager.CurrentStoryId : "");
        AppendLine("Сезон", storyManager != null ? storyManager.CurrentSeasonId : "");
        AppendLine("Глава", storyManager != null ? storyManager.CurrentChapterId : "");
        AppendLine("Эпизод", episodeId);
        AppendLine("Источник графа", storyManager != null ? storyManager.LastResolvedGraphSource : "");
        AppendLine("Источник эпизода", storyManager != null ? storyManager.LastResolvedGraphEpisodeId : "");
        AppendLine("Версия источника", storyManager != null ? storyManager.LastResolvedGraphContentVersion : "");

        AppendHeader("JSON эпизода");
        AppendEpisodeState(episodeId);
        return Builder.ToString();
    }

    static void AppendEpisodeQuickState(string episodeId)
    {
        if (string.IsNullOrWhiteSpace(episodeId))
        {
            AppendLine("JSON", "эпизод еще не выбран");
            return;
        }

        bool inCatalog = NetworkManager.TryGetCatalogEpisode(episodeId, out var catalogEpisode);
        bool hasCache = TryLoadEpisodeCache(episodeId, out var cacheEntry);
        bool hasGraph = hasCache && cacheEntry.HasGraphJson;
        AppendLine("JSON", "каталог " + YesNo(inCatalog && catalogEpisode.hasRemoteContent) +
            ", кэш " + YesNo(hasGraph));
    }

    static void AppendEpisodeState(string episodeId)
    {
        if (string.IsNullOrWhiteSpace(episodeId))
        {
            AppendLine("Текущий эпизод", "еще не выбран");
            return;
        }

        bool inCatalog = NetworkManager.TryGetCatalogEpisode(episodeId, out var catalogEpisode);
        AppendStatus("Эпизод есть в каталоге", inCatalog);
        AppendStatus("На сервере есть JSON", inCatalog && catalogEpisode.hasRemoteContent);
        AppendLine("Версия в каталоге", inCatalog ? catalogEpisode.contentVersion : "");

        bool hasCache = TryLoadEpisodeCache(episodeId, out var cacheEntry);
        AppendStatus("JSON сохранен в кэше", hasCache);
        AppendLine("Версия кэша", hasCache ? cacheEntry.contentVersion : "");
        AppendStatus("В кэше есть graph", hasCache && cacheEntry.HasGraphJson);
        AppendLine("Кэш обновлен", hasCache ? cacheEntry.fetchedAtIso : "");
    }

    static bool TryLoadEpisodeCache(string episodeId, out RemoteEpisodeGraphCacheEntry entry)
    {
        if (string.Equals(_cacheProbeEpisodeId, episodeId, StringComparison.OrdinalIgnoreCase) &&
            Time.unscaledTime < _nextCacheProbeTime)
        {
            entry = _lastCacheEntry;
            return _lastCacheProbeResult;
        }

        _cacheProbeEpisodeId = episodeId;
        _nextCacheProbeTime = Time.unscaledTime + CacheProbeIntervalSeconds;
        _lastCacheProbeResult = RemoteEpisodeGraphCache.TryLoad(episodeId, out _lastCacheEntry);
        entry = _lastCacheEntry;
        return _lastCacheProbeResult;
    }

    static NetworkRuntimeConfigData SafeLoadConfig()
    {
        try { return NetworkRuntimeConfigLoader.Load(); }
        catch (Exception) { return null; }
    }

    static int CountCatalogSeasons() =>
        NetworkManager.CatalogSeasons != null ? NetworkManager.CatalogSeasons.Count : 0;

    static int CountCatalogEpisodes()
    {
        int count = 0;
        var seasons = NetworkManager.CatalogSeasons;
        if (seasons == null)
            return 0;

        foreach (var season in seasons)
            if (season != null && season.episodes != null)
                count += season.episodes.Count;

        return count;
    }

    static string ResolveEnvironmentId(NetworkRuntimeConfigData config) =>
        config != null ? config.ResolveSelectedEnvironmentId() : "";

    static string ResolveBaseUrl(NetworkRuntimeConfigData config) =>
        config != null ? config.ResolveBaseUrl("") : "";

    static string ResolveAddressablesPath(NetworkRuntimeConfigData config) =>
        config != null ? config.ResolveAddressablesRemoteLoadPath("не задан") : "не задан";

    static string FirstNonEmpty(string first, string second) =>
        !string.IsNullOrWhiteSpace(first) ? first : second;

    static void AppendHeader(string value)
    {
        if (Builder.Length > 0)
            Builder.AppendLine();

        Builder.AppendLine(value);
    }

    static void AppendLine(string label, string value)
    {
        Builder.Append(label).Append(": ");
        Builder.AppendLine(WrapValue(value));
    }

    static void AppendStatus(string label, bool ok, string detail = "")
    {
        Builder.Append(label).Append(": ").Append(ok ? "да" : "нет");
        if (!string.IsNullOrWhiteSpace(detail))
            Builder.Append(" - ").Append(WrapValue(detail));
        Builder.AppendLine();
    }

    static string YesNo(bool value) => value ? "да" : "нет";

    static string WrapValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return EmptyValue;

        const int ChunkSize = 34;
        if (value.Length <= ChunkSize)
            return value;

        var wrapped = new StringBuilder(value.Length + 16);
        for (int i = 0; i < value.Length; i += ChunkSize)
        {
            if (i > 0)
                wrapped.AppendLine().Append("  ");

            wrapped.Append(value, i, Math.Min(ChunkSize, value.Length - i));
        }

        return wrapped.ToString();
    }
}
