#if UNITY_EDITOR
using System.Collections.Generic;

public static class DeploymentCliBackendChecks
{
    public static void Run(List<string> lines, ref int failures)
    {
        CheckRoutes(lines, ref failures);
        CheckPayloads(lines, ref failures);
        CheckCommandBuilder(lines, ref failures);
    }

    private static void CheckRoutes(List<string> lines, ref int failures)
    {
        Pass(CurrentBackendAdminRoutes.AdminCatalog == "/admin/catalog", "маршрут GET admin catalog", lines, ref failures);
        Pass(CurrentBackendAdminRoutes.Story == "/admin/catalog/story", "маршрут POST story", lines, ref failures);
        Pass(
            CurrentBackendAdminRoutes.StorySeason("story_01") == "/admin/catalog/story/story_01/season",
            "маршрут POST season",
            lines,
            ref failures);
        Pass(
            CurrentBackendAdminRoutes.SeasonEpisode("season_01") == "/admin/catalog/season/season_01/episode",
            "маршрут POST episode",
            lines,
            ref failures);
        Pass(
            !CurrentBackendAdminRoutes.IsKnownPath("/admin/content/releases"),
            "будущий release route не смешан с текущим backend-каталогом",
            lines,
            ref failures);
    }

    private static void CheckPayloads(List<string> lines, ref int failures)
    {
        string story = CurrentBackendCatalogPayloadBuilder.BuildStory("story_01", "Story", true, out string storyError);
        string season = CurrentBackendCatalogPayloadBuilder.BuildSeason("season_01", "Season", 2, out string seasonError);
        string episode = CurrentBackendCatalogPayloadBuilder.BuildEpisode("ep_01", "Episode", true, 3, 4, false, out string episodeError);
        Pass(string.IsNullOrEmpty(storyError) && story.Contains("\"allowHeroRename\":true"), "payload истории", lines, ref failures);
        Pass(string.IsNullOrEmpty(seasonError) && season.Contains("\"order\":2"), "payload сезона", lines, ref failures);
        Pass(string.IsNullOrEmpty(episodeError) && episode.Contains("\"candleCost\":3"), "payload эпизода", lines, ref failures);
    }

    private static void CheckCommandBuilder(List<string> lines, ref int failures)
    {
        string script = NocturnalServerCommandBuilder.BuildCurrentBackendPowerShell(
            "https://nocturnedc.ru/",
            "story_01", "Story", true,
            "season_01", "Season", 2,
            "ep_01", "Episode", true, 3, 4, false,
            "Assets/episode.json");
        Pass(script.Contains("$env:NOCTURNEDC_ADMIN_KEY"), "ключ администратора берётся из окружения", lines, ref failures);
        Pass(script.Contains("$ErrorActionPreference = \"Stop\""), "ручная проверка останавливается на ошибках", lines, ref failures);
        Pass(script.Contains("IsNullOrWhiteSpace($adminKey)"), "ручная проверка требует ключ администратора", lines, ref failures);
        Pass(script.Contains("IsNullOrWhiteSpace($storyId)"), "ручная проверка требует ID истории", lines, ref failures);
        Pass(script.Contains("IsNullOrWhiteSpace($seasonId)"), "ручная проверка требует ID сезона", lines, ref failures);
        Pass(script.Contains("IsNullOrWhiteSpace($episodeId)"), "ручная проверка требует ID эпизода", lines, ref failures);
        Pass(script.Contains("--fail-with-body"), "ручная проверка ловит HTTP-ошибки", lines, ref failures);
        Pass(script.Contains("$LASTEXITCODE -ne 0"), "ручная проверка останавливается после ошибки запроса", lines, ref failures);
        Pass(script.Contains("Test-Path -LiteralPath $episodeJson"), "ручная проверка проверяет путь JSON эпизода", lines, ref failures);
        Pass(script.Contains("/admin/catalog/story/$storyId/season"), "команда сезона", lines, ref failures);
        Pass(script.Contains("/admin/catalog/season/$seasonId/episode"), "команда эпизода", lines, ref failures);
        Pass(script.Contains("/admin/catalog/episode/$episodeId/content"), "команда загрузки JSON", lines, ref failures);
        Pass(!script.Contains("X-Admin-Key: secret"), "ручная проверка не содержит секреты", lines, ref failures);
        string cli = NocturnalServerCommandBuilder.BuildCliVerificationCommand("Unity.exe", "D:/Project");
        Pass(cli.Contains("-executeMethod DeploymentCliVerifier.RunForBatchMode"), "пакетная проверка указывает метод запуска", lines, ref failures);
        Pass(cli.Contains("NocturnalProjectVerification.log"), "пакетная проверка указывает путь лога", lines, ref failures);
        string checklist = NocturnalServerCommandBuilder.BuildBackendChecklist("https://nocturnedc.ru", "story_01", "season_01", "ep_01", DeploymentEnvironmentIds.Stage);
        Pass(checklist.Contains("NOCTURNEDC_ADMIN_KEY"), "чеклист содержит проверку ключа администратора", lines, ref failures);
        Pass(checklist.Contains("Проверить проект"), "чеклист содержит проверку проекта", lines, ref failures);
        Pass(checklist.Contains("проверить среду и сервер"), "чеклист содержит подтверждение цели записи", lines, ref failures);
    }

    private static void Pass(bool ok, string label, List<string> lines, ref int failures)
    {
        lines.Add((ok ? "PASS " : "FAIL ") + label);
        if (!ok)
            failures++;
    }
}
#endif
