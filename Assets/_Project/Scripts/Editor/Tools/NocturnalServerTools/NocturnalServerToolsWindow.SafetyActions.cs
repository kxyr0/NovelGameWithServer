#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed partial class NocturnalServerToolsWindow
{
    private const string RunbookPath = "Assets/_Project/Docs/NocturnalServerRunbook.md";

    private void DrawOpenWindows()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Окна и справка", EditorStyles.boldLabel);
        BeginActionRow();
        if (ActionButton("Публикатор релизов", "Открывает отдельное окно для публикации метаданных релиза.")) ContentReleasePublisherWindow.Open();
        if (ActionButton("Публикатор backend", "Открывает упрощённое окно загрузки JSON эпизода и публикации.")) CurrentBackendCatalogPublisherWindow.Open();
        if (ActionButton("Документация API", "Открывает документацию backend API в браузере.")) Application.OpenURL("https://nocturnedc.ru/api-docs");
        if (ActionButton("Инструкция", "Открывает пошаговую инструкцию по тестовой и реальной выкладке.")) OpenServerRunbook();
        EndActionRow();
        BeginActionRow();
        if (ActionButton("Проверка API", "Открывает безопасную диагностику backend API: auth, catalog, player/content read-only и admin read-only."))
            ApiDiagnosticsWindow.Open();
        EndActionRow();
    }

    private void DrawBackendSafetyNotice()
    {
        string mismatch = BuildEnvironmentMismatchWarning();
        if (!string.IsNullOrEmpty(mismatch))
            EditorGUILayout.HelpBox(mismatch, MessageType.Warning);

        if (!IsRemoteBackendTarget(_baseUrl))
            return;

        string title = IsProductionBackendTarget(_baseUrl)
            ? "Выбран реальный production backend."
            : "Выбран удалённый backend.";
        string unsigned = _allowUnsigned ? "\nРежим без ключа включён. Используйте его только для доверенного тестового сервера." : "";
        EditorGUILayout.HelpBox(title + "\nПеред отправкой записи появится подтверждение." + unsigned, MessageType.Warning);
    }

    private bool ConfirmBackendWrite(string action)
    {
        if (!IsRemoteBackendTarget(_baseUrl))
            return true;

        if (StopWithResponse(IsProductionBackendTarget(_baseUrl) && _allowUnsigned, "Перед записью в Прод выключите «Разрешить без ключа»."))
            return false;
        if (StopWithResponse(!_allowUnsigned && string.IsNullOrWhiteSpace(_adminKey) && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(CurrentBackendCatalogClient.AdminKeyEnvironmentVariable)), "Для записи на удалённый сервер нужен X-Admin-Key."))
            return false;
        string target = IsProductionBackendTarget(_baseUrl) ? "Прод" : "удалённый сервер";
        string mismatch = BuildEnvironmentMismatchWarning();
        if (StopWithResponse(!string.IsNullOrEmpty(mismatch), mismatch)) return false;
        return EditorUtility.DisplayDialog(
            "Подтвердите запись: " + target,
            action + " отправит запрос на запись:\n" + CleanBaseUrl(_baseUrl) +
            "\nСреда: " + SelectedEnvironmentId +
            "\n\nИстория: " + SaveDataSanitizer.SanitizeIdentifier(_storyId) +
            "\nСезон: " + SaveDataSanitizer.SanitizeIdentifier(_seasonId) +
            "\nЭпизод: " + SaveDataSanitizer.SanitizeIdentifier(_episodeId),
            "Отправить",
            "Отмена");
    }

    private void CopyCurrentBackendCurl()
    {
        string assetPath = _episodeJson != null ? AssetDatabase.GetAssetPath(_episodeJson) : "";
        if (!string.IsNullOrEmpty(assetPath))
            assetPath = System.IO.Path.GetFullPath(assetPath);

        EditorGUIUtility.systemCopyBuffer = NocturnalServerCommandBuilder.BuildCurrentBackendPowerShell(
            _baseUrl,
            _storyId,
            _storyTitle,
            _allowHeroRename,
            _seasonId,
            _seasonTitle,
            _seasonOrder,
            _episodeId,
            _episodeTitle,
            _isPremium,
            _candleCost,
            _episodeOrder,
            _geoRestricted,
            assetPath);
        _lastResponse = "API-команды скопированы. Перед ручным запуском укажите NOCTURNEDC_ADMIN_KEY.";
        Repaint();
    }

    private void PreviewBackendPayloads()
    {
        string story = CurrentBackendCatalogPayloadBuilder.BuildStory(_storyId, _storyTitle, _allowHeroRename, out string storyError);
        string season = CurrentBackendCatalogPayloadBuilder.BuildSeason(_seasonId, _seasonTitle, _seasonOrder, out string seasonError);
        string episode = CurrentBackendCatalogPayloadBuilder.BuildEpisode(
            _episodeId,
            _episodeTitle,
            _isPremium,
            _candleCost,
            _episodeOrder,
            _geoRestricted,
            out string episodeError);
        var builder = new StringBuilder();
        builder.AppendLine("Предпросмотр backend-запросов. На сервер ничего не отправлено.");
        builder.AppendLine("Цель: " + CleanBaseUrl(_baseUrl));
        AppendPreview(builder, "POST " + CurrentBackendAdminRoutes.Story, story, storyError);
        AppendPreview(builder, "POST " + CurrentBackendAdminRoutes.StorySeason(_storyId), season, seasonError);
        AppendPreview(builder, "POST " + CurrentBackendAdminRoutes.SeasonEpisode(_seasonId), episode, episodeError);
        builder.AppendLine("POST " + CurrentBackendAdminRoutes.EpisodeContent(_episodeId));
        builder.AppendLine("JSON эпизода: " + (_episodeJson != null ? AssetDatabase.GetAssetPath(_episodeJson) : "не выбран"));
        builder.AppendLine("PATCH " + CurrentBackendAdminRoutes.StoryPublish(_storyId) + " {\"published\":true}");
        builder.AppendLine("PATCH " + CurrentBackendAdminRoutes.EpisodePublish(_episodeId) + " {\"published\":true}");
        _lastResponse = builder.ToString();
        Repaint();
    }

    private void CopyBackendChecklist()
    {
        EditorGUIUtility.systemCopyBuffer = NocturnalServerCommandBuilder.BuildBackendChecklist(
            _baseUrl,
            _storyId,
            _seasonId,
            _episodeId,
            SelectedEnvironmentId);
        _lastResponse = "Чеклист проверки backend скопирован.";
        Repaint();
    }

    private void CopyBackendHandoffReport()
    {
        string assetPath = _episodeJson != null ? AssetDatabase.GetAssetPath(_episodeJson) : "";
        if (!string.IsNullOrEmpty(assetPath))
            assetPath = System.IO.Path.GetFullPath(assetPath);

        EditorGUIUtility.systemCopyBuffer = NocturnalServerCommandBuilder.BuildBackendHandoffReport(
            _baseUrl,
            _storyId,
            _storyTitle,
            _allowHeroRename,
            _seasonId,
            _seasonTitle,
            _seasonOrder,
            _episodeId,
            _episodeTitle,
            _isPremium,
            _candleCost,
            _episodeOrder,
            _geoRestricted,
            SelectedEnvironmentId,
            _contentVersion,
            assetPath);
        _lastResponse = "Текст передачи по backend скопирован.";
        Repaint();
    }

    private static void AppendPreview(StringBuilder builder, string route, string body, string error)
    {
        builder.AppendLine(route);
        builder.AppendLine(string.IsNullOrEmpty(error) ? body : "ОШИБКА " + error);
    }

    private void OpenServerRunbook()
    {
        string fullPath = System.IO.Path.GetFullPath(RunbookPath);
        if (!System.IO.File.Exists(fullPath))
        {
            _lastResponse = "Инструкция не найдена:\n" + fullPath;
            Repaint();
            return;
        }

        Application.OpenURL("file:///" + fullPath.Replace("\\", "/"));
    }

    private string BuildEnvironmentMismatchWarning()
    {
        if (DeploymentEnvironmentIds.IsProduction(SelectedEnvironmentId) && !IsProductionBackendTarget(_baseUrl))
            return "Выбран Прод, но адрес сервера не nocturnedc.ru.";
        if (!DeploymentEnvironmentIds.IsProduction(SelectedEnvironmentId) && IsProductionBackendTarget(_baseUrl))
            return "Выбрана тестовая среда, но адрес сервера указывает на nocturnedc.ru.";
        return "";
    }

    private static bool IsRemoteBackendTarget(string baseUrl)
    {
        return Uri.TryCreate(CleanBaseUrl(baseUrl), UriKind.Absolute, out Uri uri) && !uri.IsLoopback;
    }

    private static bool IsProductionBackendTarget(string baseUrl)
    {
        return Uri.TryCreate(CleanBaseUrl(baseUrl), UriKind.Absolute, out Uri uri) &&
               uri.Host.Equals("nocturnedc.ru", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanBaseUrl(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? ApiRoutes.BaseUrl : value.Trim().TrimEnd('/');
    }
}
#endif
