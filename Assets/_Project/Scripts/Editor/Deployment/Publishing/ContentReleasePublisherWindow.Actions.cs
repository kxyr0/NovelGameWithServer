#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine;

public sealed partial class ContentReleasePublisherWindow
{
    private void ApplyEnvironmentPreset(bool onlyMissingValues)
    {
        DeploymentEnvironmentPreset preset = ContentReleaseUploadDestinationSettings.ApplyToPreset(DeploymentEnvironmentPresets.Find(_environmentId));
        if (!onlyMissingValues || string.IsNullOrWhiteSpace(_baseUrl))
            _baseUrl = preset.BaseUrl;
        if (!onlyMissingValues || string.IsNullOrWhiteSpace(_loadPath))
            _loadPath = preset.AddressablesLoadPath;

        if (!onlyMissingValues || !ContentReleaseStatus.IsKnown(_status))
        {
            _status = DeploymentEnvironmentIds.IsProduction(_environmentId)
                ? ContentReleaseStatus.Published
                : ContentReleaseStatus.Staging;
        }
    }

    private void CaptureSelectedIds()
    {
        if (!ContentReleasePayloadBuilder.TryReadSelectionIds(out string storyId, out string episodeId))
        {
            _lastResponse = "Сначала выберите StoryData, ChapterData или StoryGraph в Project.";
            Repaint();
            return;
        }

        if (!string.IsNullOrEmpty(storyId))
            _storyId = storyId;
        if (!string.IsNullOrEmpty(episodeId))
            _episodeId = episodeId;
        _lastResponse = "ID из выбранного ассета подставлены.";
        Repaint();
    }

    private ContentReleaseDescriptor BuildRelease()
    {
        return ContentReleasePayloadBuilder.Build(
            _environmentId,
            _status,
            _storyId,
            _episodeId,
            _contentVersion,
            _catalogUrl,
            _loadPath,
            _minAppVersion,
            _notes,
            _manifestUrl,
            _manifestHash,
            _buildTarget);
    }

    private void ValidateRelease()
    {
        DeploymentEnvironmentValidationResult result = ContentReleasePolicy.Validate(BuildRelease());
        _lastResponse = result.IsValid ? "Проверка пройдена: " + result.Message : "Проверка не пройдена: " + result.Message;
        SavePrefs();
        Repaint();
    }

    private void BuildManifest()
    {
        if (!ContentReleaseManifestBuilder.TryWrite(
                _environmentId,
                _contentVersion,
                out string path,
                out string url,
                out string hash,
                out string error))
        {
            _lastResponse = "Manifest не собран: " + error;
            Repaint();
            return;
        }

        _manifestUrl = url;
        _manifestHash = hash;
        _buildTarget = UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString();
        _lastResponse = "Manifest собран:\n" + path;
        SavePrefs();
        Repaint();
    }

    private void PublishRelease()
    {
        StartRequest(ContentReleasePublisherClient.Upsert(
            BuildRelease(),
            OnRequestFinished,
            _baseUrl,
            _adminKey,
            _allowUnsigned));
    }

    private void FetchRelease()
    {
        StartRequest(ContentReleasePublisherClient.Fetch(
            _storyId,
            _episodeId,
            OnRequestFinished,
            _baseUrl,
            _adminKey,
            _allowUnsigned));
    }

    private void PromoteRelease()
    {
        StartRequest(ContentReleasePublisherClient.Promote(
            _storyId,
            _episodeId,
            _contentVersion,
            OnRequestFinished,
            _baseUrl,
            _adminKey,
            _allowUnsigned));
    }

    private void RollbackRelease()
    {
        StartRequest(ContentReleasePublisherClient.Rollback(
            _storyId,
            _episodeId,
            _contentVersion,
            OnRequestFinished,
            _baseUrl,
            _adminKey,
            _allowUnsigned));
    }

    private void StartRequest(IEnumerator request)
    {
        if (StopUnsafeReleaseRequest()) return;
        SavePrefs();
        _isBusy = true;
        _lastResponse = "Запрос отправлен.";
        Repaint();
        EditorCoroutineRunner.Start(RunRequest(request));
    }

    private bool StopUnsafeReleaseRequest() {
        bool hasUri = System.Uri.TryCreate((_baseUrl ?? "").Trim().TrimEnd('/'), System.UriKind.Absolute, out System.Uri uri);
        bool prodTarget = hasUri && uri.Host.Equals("nocturnedc.ru", System.StringComparison.OrdinalIgnoreCase);
        bool prodEnv = DeploymentEnvironmentIds.IsProduction(_environmentId);
        string message = prodTarget && _allowUnsigned ? "Перед запросами в Прод выключите «Разрешить без ключа»." : hasUri && !uri.IsLoopback && prodTarget != prodEnv ? "Среда релиза не совпадает с адресом сервера." : "";
        if (string.IsNullOrEmpty(message)) return false; _lastResponse = message; Repaint(); return true;
    }

    private IEnumerator RunRequest(IEnumerator request)
    {
        while (request != null && request.MoveNext())
            yield return request.Current;

        _isBusy = false;
        Repaint();
    }

    private void OnRequestFinished(UnityPublisherRequestResult result)
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
        new ContentReleasePublisherPrefs
        {
            EnvironmentId = _environmentId,
            Status = _status,
            StoryId = _storyId,
            EpisodeId = _episodeId,
            ContentVersion = _contentVersion,
            CatalogUrl = _catalogUrl,
            LoadPath = _loadPath,
            ManifestUrl = _manifestUrl,
            ManifestHash = _manifestHash,
            BuildTarget = _buildTarget,
            MinAppVersion = _minAppVersion,
            Notes = _notes,
            BaseUrl = _baseUrl,
            AllowUnsigned = _allowUnsigned
        }.Save();
    }

    private static string FirstNonEmpty(string first, string second)
    {
        return !string.IsNullOrWhiteSpace(first) ? first : second;
    }
}
#endif
