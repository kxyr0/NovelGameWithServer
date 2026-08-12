#if UNITY_EDITOR
using System.Collections;

public sealed partial class NocturnalServerToolsWindow
{
    private void FetchCurrentCatalog()
    {
        StartBackendRequest(CurrentBackendCatalogClient.FetchCatalog(
            OnBackendRequestFinished,
            _baseUrl,
            _adminKey,
            _allowUnsigned));
    }

    private void CreateCurrentStory()
    {
        if (StopWithResponse(string.IsNullOrWhiteSpace(_storyId), "Перед созданием истории укажите ID истории."))
            return;
        if (!ConfirmBackendWrite("Создание истории"))
            return;

        StartBackendRequest(CurrentBackendCatalogClient.CreateStory(
            _storyId,
            _storyTitle,
            _allowHeroRename,
            OnBackendRequestFinished,
            _baseUrl,
            _adminKey,
            _allowUnsigned));
    }

    private void AddCurrentSeason()
    {
        if (StopWithResponse(string.IsNullOrWhiteSpace(_storyId), "Перед добавлением сезона укажите ID истории."))
            return;
        if (StopWithResponse(string.IsNullOrWhiteSpace(_seasonId), "Перед добавлением сезона укажите ID сезона."))
            return;
        if (!ConfirmBackendWrite("Добавление сезона"))
            return;

        StartBackendRequest(CurrentBackendCatalogClient.AddSeason(
            _storyId,
            _seasonId,
            _seasonTitle,
            _seasonOrder,
            OnBackendRequestFinished,
            _baseUrl,
            _adminKey,
            _allowUnsigned));
    }

    private void AddCurrentEpisode()
    {
        if (StopWithResponse(string.IsNullOrWhiteSpace(_seasonId), "Перед добавлением эпизода укажите ID сезона."))
            return;
        if (StopWithResponse(string.IsNullOrWhiteSpace(_episodeId), "Перед добавлением эпизода укажите ID эпизода."))
            return;
        if (!ConfirmBackendWrite("Добавление эпизода"))
            return;

        StartBackendRequest(CurrentBackendCatalogClient.AddEpisode(
            _seasonId,
            _episodeId,
            _episodeTitle,
            _isPremium,
            _candleCost,
            _episodeOrder,
            _geoRestricted,
            OnBackendRequestFinished,
            _baseUrl,
            _adminKey,
            _allowUnsigned));
    }

    private void UploadEpisodeJsonToCurrentBackend()
    {
        if (StopWithResponse(string.IsNullOrWhiteSpace(_episodeId), "Перед загрузкой JSON укажите ID эпизода."))
            return;
        if (StopWithResponse(_episodeJson == null, "Перед загрузкой выберите JSON эпизода."))
            return;

        string json = _episodeJson.text;
        if (StopWithResponse(string.IsNullOrWhiteSpace(json), "JSON эпизода пустой."))
            return;

        if (!ConfirmBackendWrite("Загрузка JSON эпизода"))
            return;

        StartBackendRequest(CurrentBackendCatalogClient.UploadEpisodeContent(
            _episodeId,
            json,
            OnBackendRequestFinished,
            _baseUrl,
            _adminKey,
            _allowUnsigned));
    }

    private void SetCurrentStoryPublished(bool published)
    {
        if (StopWithResponse(string.IsNullOrWhiteSpace(_storyId), "Перед публикацией или скрытием истории укажите ID истории."))
            return;

        if (!ConfirmBackendWrite(published ? "Публикация истории" : "Скрытие истории"))
            return;

        StartBackendRequest(CurrentBackendCatalogClient.SetStoryPublished(
            _storyId,
            published,
            OnBackendRequestFinished,
            _baseUrl,
            _adminKey,
            _allowUnsigned));
    }

    private void SetCurrentEpisodePublished(bool published)
    {
        if (StopWithResponse(string.IsNullOrWhiteSpace(_episodeId), "Перед публикацией или скрытием эпизода укажите ID эпизода."))
            return;

        if (!ConfirmBackendWrite(published ? "Публикация эпизода" : "Скрытие эпизода"))
            return;

        StartBackendRequest(CurrentBackendCatalogClient.SetEpisodePublished(
            _episodeId,
            published,
            OnBackendRequestFinished,
            _baseUrl,
            _adminKey,
            _allowUnsigned));
    }

    private void StartBackendRequest(IEnumerator request)
    {
        SavePrefs();
        _isBusy = true;
        _lastResponse = "Запрос отправлен.";
        Repaint();
        EditorCoroutineRunner.Start(RunBackendRequest(request));
    }

    private bool StopWithResponse(bool condition, string message)
    {
        if (!condition)
            return false;

        _lastResponse = message;
        Repaint();
        return true;
    }

    private IEnumerator RunBackendRequest(IEnumerator request)
    {
        while (request != null && request.MoveNext())
            yield return request.Current;

        _isBusy = false;
        Repaint();
    }

    private void OnBackendRequestFinished(UnityPublisherRequestResult result)
    {
        if (result == null)
        {
            _lastResponse = "Запрос завершился без результата.";
            return;
        }

        _lastResponse = result.Success
            ? "OK " + result.StatusCode + "\n" + result.Body
            : "ОШИБКА " + result.StatusCode + "\n" + FirstNonEmpty(result.Error, result.Body);
    }

    private void SavePrefs()
    {
        new CurrentBackendCatalogPublisherPrefs
        {
            StoryId = _storyId,
            StoryTitle = _storyTitle,
            SeasonId = _seasonId,
            SeasonTitle = _seasonTitle,
            EpisodeId = _episodeId,
            EpisodeTitle = _episodeTitle,
            BaseUrl = _baseUrl,
            SeasonOrder = _seasonOrder,
            EpisodeOrder = _episodeOrder,
            CandleCost = _candleCost,
            AllowHeroRename = _allowHeroRename,
            IsPremium = _isPremium,
            GeoRestricted = _geoRestricted,
            AllowUnsigned = _allowUnsigned
        }.Save();
        UnityEditor.EditorPrefs.SetString("NocturnalServerTools.ContentVersion", _contentVersion ?? "");
    }

    private static string FirstNonEmpty(string first, string second)
    {
        return !string.IsNullOrWhiteSpace(first) ? first : second;
    }
}
#endif
