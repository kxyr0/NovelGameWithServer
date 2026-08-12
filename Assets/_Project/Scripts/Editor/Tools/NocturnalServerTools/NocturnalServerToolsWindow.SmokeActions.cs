#if UNITY_EDITOR
using System;
using System.Collections;
using System.Text;

public sealed partial class NocturnalServerToolsWindow
{
    private void RunCurrentBackendSmoke()
    {
        if (_isBusy)
            return;
        StartCurrentBackendMockServer();
        _baseUrl = _currentBackendMockServer.BaseUrl;
        _adminKey = CurrentBackendCatalogMockServer.DefaultAdminKey;
        _allowUnsigned = true;
        _storyId = "smoke_story";
        _storyTitle = "Тестовая история";
        _seasonId = "smoke_season";
        _seasonTitle = "Тестовый сезон";
        _episodeId = "smoke_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        _episodeTitle = "Тестовый эпизод";
        _seasonOrder = 1;
        _episodeOrder = 1;
        _isPremium = false;
        _candleCost = 0;
        _geoRestricted = false;
        SavePrefs();
        _isBusy = true;
        _lastResponse = "Локальная проверка backend запущена:\n" + _baseUrl;
        Repaint();
        EditorCoroutineRunner.Start(RunCurrentBackendSmokeRoutine(_episodeId, BuildSmokeEpisodeJson(_episodeId)));
    }

    private IEnumerator RunCurrentBackendSmokeRoutine(string episodeId, string json)
    {
        var log = new StringBuilder();
        log.AppendLine("Локальная проверка текущего backend");
        log.AppendLine("Адрес сервера: " + _baseUrl);
        log.AppendLine("ID истории: " + _storyId);
        log.AppendLine("ID сезона: " + _seasonId);
        log.AppendLine("ID эпизода: " + episodeId);

        UnityPublisherRequestResult story = null;
        yield return RunSmokeRequest(CurrentBackendCatalogClient.CreateStory(_storyId, _storyTitle, true, result => story = result, _baseUrl, _adminKey, _allowUnsigned));
        if (StopSmokeOnFailure(log, "создать историю", story)) yield break;

        UnityPublisherRequestResult season = null;
        yield return RunSmokeRequest(CurrentBackendCatalogClient.AddSeason(_storyId, _seasonId, _seasonTitle, _seasonOrder, result => season = result, _baseUrl, _adminKey, _allowUnsigned));
        if (StopSmokeOnFailure(log, "добавить сезон", season)) yield break;

        UnityPublisherRequestResult episode = null;
        yield return RunSmokeRequest(CurrentBackendCatalogClient.AddEpisode(_seasonId, episodeId, _episodeTitle, false, 0, _episodeOrder, false, result => episode = result, _baseUrl, _adminKey, _allowUnsigned));
        if (StopSmokeOnFailure(log, "добавить эпизод", episode)) yield break;

        UnityPublisherRequestResult upload = null;
        yield return RunSmokeRequest(CurrentBackendCatalogClient.UploadEpisodeContent(episodeId, json, result => upload = result, _baseUrl, _adminKey, _allowUnsigned));
        if (StopSmokeOnFailure(log, "загрузить JSON эпизода", upload)) yield break;

        UnityPublisherRequestResult publishStory = null;
        yield return RunSmokeRequest(CurrentBackendCatalogClient.SetStoryPublished(_storyId, true, result => publishStory = result, _baseUrl, _adminKey, _allowUnsigned));
        if (StopSmokeOnFailure(log, "опубликовать историю", publishStory)) yield break;

        UnityPublisherRequestResult publish = null;
        yield return RunSmokeRequest(CurrentBackendCatalogClient.SetEpisodePublished(episodeId, true, result => publish = result, _baseUrl, _adminKey, _allowUnsigned));
        if (StopSmokeOnFailure(log, "опубликовать эпизод", publish)) yield break;

        UnityPublisherRequestResult catalog = null;
        yield return RunSmokeRequest(CurrentBackendCatalogClient.FetchCatalog(result => catalog = result, _baseUrl, _adminKey, _allowUnsigned));
        AppendSmokeResult(log, "получить каталог", catalog);
        AppendCatalogAssertion(log, catalog, _storyId, _seasonId, episodeId);
        FinishSmoke(log);
    }

    private bool StopSmokeOnFailure(StringBuilder log, string step, UnityPublisherRequestResult result)
    {
        AppendSmokeResult(log, step, result);
        if (IsSmokeSuccess(result))
            return false;
        FinishSmoke(log);
        return true;
    }

    private static IEnumerator RunSmokeRequest(IEnumerator request)
    {
        while (request != null && request.MoveNext())
            yield return request.Current;
    }

    private void FinishSmoke(StringBuilder log)
    {
        _isBusy = false;
        _lastResponse = log.ToString();
        Repaint();
    }

    private static void AppendSmokeResult(StringBuilder log, string step, UnityPublisherRequestResult result)
    {
        log.AppendLine();
        log.AppendLine((IsSmokeSuccess(result) ? "OK " : "ОШИБКА ") + step);
        log.AppendLine("Статус: " + (result != null ? result.StatusCode.ToString() : "0"));
        log.AppendLine(TrimSmokeText(result != null ? FirstNonEmpty(result.Error, result.Body) : "Нет результата."));
    }

    private static void AppendCatalogAssertion(StringBuilder log, UnityPublisherRequestResult catalog, string storyId, string seasonId, string episodeId)
    {
        string body = catalog != null ? catalog.Body : "";
        bool containsStory = body.Contains("\"storyId\":\"" + storyId + "\"");
        bool containsSeason = body.Contains("\"seasonId\":\"" + seasonId + "\"");
        bool containsEpisode = body.Contains("\"episodeId\":\"" + episodeId + "\"") && body.Contains("\"isPublished\":true");
        log.AppendLine();
        log.AppendLine(containsStory && containsSeason && containsEpisode
            ? "OK каталог содержит тестовую историю, сезон и опубликованный эпизод."
            : "ОШИБКА каталог не содержит полную цепочку истории, сезона и эпизода.");
    }

    private static bool IsSmokeSuccess(UnityPublisherRequestResult result)
    {
        return result != null && result.Success;
    }

    private static string BuildSmokeEpisodeJson(string episodeId)
    {
        return "{\"episodeId\":\"" + episodeId + "\",\"nodes\":[],\"edges\":[]}";
    }

    private static string TrimSmokeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        value = value.Trim();
        return value.Length <= 600 ? value : value.Substring(0, 600);
    }
}
#endif
