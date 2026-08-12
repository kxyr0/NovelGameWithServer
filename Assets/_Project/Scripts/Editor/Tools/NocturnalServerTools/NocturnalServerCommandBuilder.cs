#if UNITY_EDITOR
using System;
using System.Text;

public static class NocturnalServerCommandBuilder
{
    public static string BuildCurrentBackendPowerShell(
        string baseUrl,
        string storyId,
        string storyTitle,
        bool allowHeroRename,
        string seasonId,
        string seasonTitle,
        int seasonOrder,
        string episodeId,
        string episodeTitle,
        bool isPremium,
        int candleCost,
        int episodeOrder,
        bool geoRestricted,
        string episodeJsonPath)
    {
        string root = CleanBaseUrl(baseUrl);
        string story = SaveDataSanitizer.SanitizeIdentifier(storyId);
        string storyName = SaveDataSanitizer.SanitizeHistoryLine(storyTitle);
        string season = SaveDataSanitizer.SanitizeIdentifier(seasonId);
        string seasonName = SaveDataSanitizer.SanitizeHistoryLine(seasonTitle);
        string id = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        string episodeName = SaveDataSanitizer.SanitizeHistoryLine(episodeTitle);
        string jsonPath = string.IsNullOrWhiteSpace(episodeJsonPath) ? "episode.json" : episodeJsonPath;
        string curlGuard = "; if ($LASTEXITCODE -ne 0) { throw \"API-запрос завершился с кодом $LASTEXITCODE\" }";

        var builder = new StringBuilder();
        builder.AppendLine("$ErrorActionPreference = \"Stop\"");
        builder.AppendLine("$baseUrl = \"" + EscapePowerShell(root) + "\"");
        builder.AppendLine("$storyId = \"" + EscapePowerShell(story) + "\"");
        builder.AppendLine("$seasonId = \"" + EscapePowerShell(season) + "\"");
        builder.AppendLine("$episodeId = \"" + EscapePowerShell(id) + "\"");
        builder.AppendLine("$episodeJson = \"" + EscapePowerShell(jsonPath) + "\"");
        builder.AppendLine("$adminKey = $env:NOCTURNEDC_ADMIN_KEY");
        builder.AppendLine("if ([string]::IsNullOrWhiteSpace($adminKey)) { throw \"Перед запуском укажите NOCTURNEDC_ADMIN_KEY.\" }");
        builder.AppendLine("if ([string]::IsNullOrWhiteSpace($storyId)) { throw \"Перед записью в backend нужен ID истории.\" }");
        builder.AppendLine("if ([string]::IsNullOrWhiteSpace($seasonId)) { throw \"Перед записью в backend нужен ID сезона.\" }");
        builder.AppendLine("if ([string]::IsNullOrWhiteSpace($episodeId)) { throw \"Перед записью в backend нужен ID эпизода.\" }");
        builder.AppendLine();
        builder.AppendLine("curl.exe --fail-with-body -sS -H \"X-Admin-Key: $adminKey\" \"$baseUrl/admin/catalog\"" + curlGuard);
        if (!string.IsNullOrEmpty(story))
            builder.AppendLine("curl.exe --fail-with-body -sS -X POST -H \"X-Admin-Key: $adminKey\" -H \"Content-Type: application/json\" --data '{\"storyId\":\"" + EscapeJson(story) + "\",\"title\":\"" + EscapeJson(storyName) + "\",\"allowHeroRename\":" + Bool(allowHeroRename) + "}' \"$baseUrl/admin/catalog/story\"" + curlGuard);
        if (!string.IsNullOrEmpty(story) && !string.IsNullOrEmpty(season))
            builder.AppendLine("curl.exe --fail-with-body -sS -X POST -H \"X-Admin-Key: $adminKey\" -H \"Content-Type: application/json\" --data '{\"seasonId\":\"" + EscapeJson(season) + "\",\"title\":\"" + EscapeJson(seasonName) + "\",\"order\":" + Math.Max(0, seasonOrder) + "}' \"$baseUrl/admin/catalog/story/$storyId/season\"" + curlGuard);
        if (!string.IsNullOrEmpty(season) && !string.IsNullOrEmpty(id))
            builder.AppendLine("curl.exe --fail-with-body -sS -X POST -H \"X-Admin-Key: $adminKey\" -H \"Content-Type: application/json\" --data '{\"episodeId\":\"" + EscapeJson(id) + "\",\"title\":\"" + EscapeJson(episodeName) + "\",\"isPremium\":" + Bool(isPremium) + ",\"candleCost\":" + Math.Max(0, candleCost) + ",\"order\":" + Math.Max(0, episodeOrder) + ",\"geoRestricted\":" + Bool(geoRestricted) + "}' \"$baseUrl/admin/catalog/season/$seasonId/episode\"" + curlGuard);
        builder.AppendLine("if (!(Test-Path -LiteralPath $episodeJson)) { throw \"JSON эпизода не найден: $episodeJson\" }");
        builder.AppendLine("curl.exe --fail-with-body -sS -X POST -H \"X-Admin-Key: $adminKey\" -H \"Content-Type: application/json\" --data-binary \"@$episodeJson\" \"$baseUrl/admin/catalog/episode/$episodeId/content\"" + curlGuard);
        if (!string.IsNullOrEmpty(story))
            builder.AppendLine("curl.exe --fail-with-body -sS -X PATCH -H \"X-Admin-Key: $adminKey\" -H \"Content-Type: application/json\" --data '{\"published\":true}' \"$baseUrl/admin/catalog/story/$storyId/publish\"" + curlGuard);
        builder.AppendLine("curl.exe --fail-with-body -sS -X PATCH -H \"X-Admin-Key: $adminKey\" -H \"Content-Type: application/json\" --data '{\"published\":true}' \"$baseUrl/admin/catalog/episode/$episodeId/publish\"" + curlGuard);
        builder.AppendLine("curl.exe --fail-with-body -sS -X PATCH -H \"X-Admin-Key: $adminKey\" -H \"Content-Type: application/json\" --data '{\"published\":false}' \"$baseUrl/admin/catalog/episode/$episodeId/publish\"" + curlGuard);
        if (!string.IsNullOrEmpty(story))
            builder.AppendLine("curl.exe --fail-with-body -sS -X PATCH -H \"X-Admin-Key: $adminKey\" -H \"Content-Type: application/json\" --data '{\"published\":false}' \"$baseUrl/admin/catalog/story/$storyId/publish\"" + curlGuard);
        return builder.ToString();
    }

    public static string BuildBackendChecklist(
        string baseUrl,
        string storyId,
        string seasonId,
        string episodeId,
        string environmentId)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Чеклист проверки сервера Nocturnal");
        builder.AppendLine();
        builder.AppendLine("Сервер: " + CleanBaseUrl(baseUrl));
        builder.AppendLine("История: " + SaveDataSanitizer.SanitizeIdentifier(storyId));
        builder.AppendLine("Сезон: " + SaveDataSanitizer.SanitizeIdentifier(seasonId));
        builder.AppendLine("Эпизод: " + SaveDataSanitizer.SanitizeIdentifier(episodeId));
        builder.AppendLine("Среда: " + environmentId);
        builder.AppendLine();
        builder.AppendLine("[ ] Нажать «Локальная проверка» в окне «Инструменты/Nocturnal/Сервер».");
        builder.AppendLine("[ ] Перед новой серией применить тестовую среду.");
        builder.AppendLine("[ ] Если JSON/сценарий использует картинки, аудио или видео, загрузить их во вкладке «Медиа» и вставить публичные ссылки.");
        builder.AppendLine("[ ] Собрать Addressables и manifest.");
        builder.AppendLine("[ ] Если R2/CDN уже выдан, создать план загрузки и загрузить Addressables; если нет - не считать Addressables опубликованными.");
        builder.AppendLine("[ ] Сначала создать историю, сезон и эпизод на тестовой среде или локальном mock.");
        builder.AppendLine("[ ] Сначала загрузить JSON эпизода на тестовую среду или локальный mock.");
        builder.AppendLine("[ ] Получить каталог и проверить, что эпизод есть и не открыт случайно.");
        builder.AppendLine("[ ] Указать X-Admin-Key в Unity или NOCTURNEDC_ADMIN_KEY для ручных API-команд.");
        builder.AppendLine("[ ] Нажать «Проверить проект» и открыть отчёт проверки.");
        builder.AppendLine("[ ] Перед удалённой записью проверить среду и сервер в окне подтверждения.");
        builder.AppendLine("[ ] Публиковать только после отчёта готовности без блокирующих ошибок.");
        builder.AppendLine("[ ] Повторять в Прод только после проверки на тестовой среде.");
        return builder.ToString();
    }

    public static string BuildBackendHandoffReport(
        string baseUrl,
        string storyId,
        string storyTitle,
        bool allowHeroRename,
        string seasonId,
        string seasonTitle,
        int seasonOrder,
        string episodeId,
        string episodeTitle,
        bool isPremium,
        int candleCost,
        int episodeOrder,
        bool geoRestricted,
        string environmentId,
        string contentVersion,
        string episodeJsonPath)
    {
        string root = CleanBaseUrl(baseUrl);
        string story = SaveDataSanitizer.SanitizeIdentifier(storyId);
        string season = SaveDataSanitizer.SanitizeIdentifier(seasonId);
        string id = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        var builder = new StringBuilder();
        builder.AppendLine("# Передача по серверу Nocturnal");
        builder.AppendLine();
        builder.AppendLine("Сервер: " + root);
        builder.AppendLine("Среда: " + environmentId);
        builder.AppendLine("История: " + story);
        builder.AppendLine("Сезон: " + season);
        builder.AppendLine("Эпизод: " + id);
        builder.AppendLine("Версия контента: " + SaveDataSanitizer.SanitizeIdentifier(contentVersion));
        builder.AppendLine();
        builder.AppendLine("Маршруты:");
        builder.AppendLine("- GET " + CurrentBackendAdminRoutes.AdminCatalog);
        builder.AppendLine("- POST " + CurrentBackendAdminRoutes.Story);
        builder.AppendLine("- POST " + CurrentBackendAdminRoutes.StorySeason(story));
        builder.AppendLine("- POST " + CurrentBackendAdminRoutes.SeasonEpisode(season));
        builder.AppendLine("- PATCH " + CurrentBackendAdminRoutes.StoryPublish(story));
        builder.AppendLine("- POST " + CurrentBackendAdminRoutes.EpisodeContent(id));
        builder.AppendLine("- PATCH " + CurrentBackendAdminRoutes.EpisodePublish(id));
        builder.AppendLine();
        builder.Append(BuildBackendChecklist(root, story, season, id, environmentId));
        builder.AppendLine();
        builder.AppendLine("API-команды:");
        builder.AppendLine("```powershell");
        builder.Append(BuildCurrentBackendPowerShell(
            root,
            story,
            storyTitle,
            allowHeroRename,
            season,
            seasonTitle,
            seasonOrder,
            id,
            episodeTitle,
            isPremium,
            candleCost,
            episodeOrder,
            geoRestricted,
            episodeJsonPath));
        builder.AppendLine("```");
        return builder.ToString();
    }

    public static string BuildCliVerificationCommand(string unityExePath, string projectPath)
    {
        string unity = string.IsNullOrWhiteSpace(unityExePath) ? "Unity.exe" : unityExePath;
        string project = string.IsNullOrWhiteSpace(projectPath) ? "." : projectPath;
        string log = System.IO.Path.Combine(project, "Library", "NocturnalProjectVerification.log");
        var builder = new StringBuilder();
        builder.Append("& \"").Append(EscapePowerShell(unity)).Append("\" ");
        builder.Append("-batchmode -nographics ");
        builder.Append("-projectPath \"").Append(EscapePowerShell(project)).Append("\" ");
        builder.Append("-executeMethod DeploymentCliVerifier.RunForBatchMode ");
        builder.Append("-logFile \"").Append(EscapePowerShell(log)).Append("\" ");
        builder.Append("-quit");
        return builder.ToString();
    }

    private static string CleanBaseUrl(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? ApiRoutes.BaseUrl : value.Trim().TrimEnd('/');
    }

    private static string EscapePowerShell(string value)
    {
        return (value ?? "").Replace("`", "``").Replace("$", "`$").Replace("\"", "`\"");
    }

    private static string EscapeJson(string value)
    {
        return NetworkJson.Escape(value ?? "");
    }

    private static string Bool(bool value)
    {
        return value ? "true" : "false";
    }
}
#endif
