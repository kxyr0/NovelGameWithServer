#if UNITY_EDITOR
using System.IO;
using UnityEditor;

public static class DeploymentReadinessScanner
{
    public static DeploymentReadinessReport Scan()
    {
        var report = new DeploymentReadinessReport
        {
            BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString()
        };

        AddEnvironmentChecks(report);
        AddManifestChecks(report, DeploymentEnvironmentPresets.Stage);
        AddManifestChecks(report, DeploymentEnvironmentPresets.Production);
        AddScriptBudgetChecks(report);
        AddToolingChecks(report);
        AddNocturnalToolingChecks(report);
        return report;
    }

    private static void AddEnvironmentChecks(DeploymentReadinessReport report)
    {
        var issues = DeploymentEnvironmentValidator.ValidateProject();
        if (issues.Count == 0)
        {
            report.Items.Add(DeploymentReadinessItem.Pass(
                "Тест/Прод",
                "Проверка окружений выкладки",
                "Runtime config и профили Addressables разделены."));
            return;
        }

        for (int i = 0; i < issues.Count; i++)
            report.Items.Add(DeploymentReadinessItem.Fail("Тест/Прод", "Проверка окружений выкладки", issues[i]));
    }

    private static void AddManifestChecks(DeploymentReadinessReport report, DeploymentEnvironmentPreset preset)
    {
        string target = report.BuildTarget;
        string buildPath = ContentReleaseManifestBuilder.ResolveTokens(preset.AddressablesBuildPath, target);
        string manifestPath = Path.Combine(buildPath, ContentReleaseManifestBuilder.ManifestFileName);

        if (!Directory.Exists(buildPath))
        {
            report.Items.Add(DeploymentReadinessItem.Warn(
                preset.DisplayName,
                "Сборка Addressables",
                "Не найдена папка " + buildPath + ". Перед загрузкой соберите Addressables."));
            return;
        }

        if (!File.Exists(manifestPath))
        {
            report.Items.Add(DeploymentReadinessItem.Warn(
                preset.DisplayName,
                "Manifest релиза",
                "Не найден " + manifestPath + ". Перед публикацией соберите manifest."));
            return;
        }

        report.Items.Add(DeploymentReadinessItem.Pass(
            preset.DisplayName,
            "Manifest релиза",
            manifestPath));
    }

    private static void AddScriptBudgetChecks(DeploymentReadinessReport report)
    {
        CheckBudget(report, "Runtime/Deployment", "Assets/_Project/Scripts/Runtime/Services/Deployment");
        CheckBudget(report, "Runtime/Addressables", "Assets/_Project/Scripts/Runtime/Infrastructure/Addressables");
        CheckBudget(report, "Editor/Deployment", "Assets/_Project/Scripts/Editor/Deployment");
        CheckBudget(report, "Editor/Publishing", "Assets/_Project/Scripts/Editor/Deployment/Publishing");
        CheckBudget(report, "Editor/MockServer", "Assets/_Project/Scripts/Editor/Deployment/Publishing/MockServer");
        CheckBudget(report, "Editor/Backend", "Assets/_Project/Scripts/Editor/Deployment/Publishing/Backend");
        CheckBudget(report, "Editor/Media", "Assets/_Project/Scripts/Editor/Deployment/Publishing/Media");
        CheckBudget(report, "Editor/Upload", "Assets/_Project/Scripts/Editor/Deployment/Publishing/Upload");
        CheckBudget(report, "Editor/Readiness", "Assets/_Project/Scripts/Editor/Deployment/Readiness");
        CheckBudget(report, "Editor/NocturnalTools", "Assets/_Project/Scripts/Editor/Tools/NocturnalServerTools");
        CheckBudget(report, "Tests/Publishing", "Assets/_Project/Scripts/Editor/Deployment/Publishing/Tests");
        CheckBudget(report, "Tests/Backend", "Assets/_Project/Scripts/Editor/Deployment/Publishing/Backend/Tests");
        CheckBudget(report, "Tests/MockServer", "Assets/_Project/Scripts/Editor/Deployment/Publishing/MockServer/Tests");
        CheckBudget(report, "Tests/Upload", "Assets/_Project/Scripts/Editor/Deployment/Publishing/Upload/Tests");
        CheckBudget(report, "Tests/Readiness", "Assets/_Project/Scripts/Editor/Deployment/Readiness/Tests");
    }

    private static void CheckBudget(DeploymentReadinessReport report, string area, string folder)
    {
        if (!Directory.Exists(folder))
        {
            report.Items.Add(DeploymentReadinessItem.Fail(area, "Лимит скриптов", "Не найдена папка: " + folder));
            return;
        }

        string[] scripts = Directory.GetFiles(folder, "*.cs", SearchOption.TopDirectoryOnly);
        if (scripts.Length > 7)
            report.Items.Add(DeploymentReadinessItem.Fail(area, "Количество скриптов", scripts.Length + " скриптов."));
        else
            report.Items.Add(DeploymentReadinessItem.Pass(area, "Количество скриптов", scripts.Length + "/7 скриптов."));

        for (int i = 0; i < scripts.Length; i++)
        {
            int lines = File.ReadAllLines(scripts[i]).Length;
            if (lines > 200)
                report.Items.Add(DeploymentReadinessItem.Fail(area, Path.GetFileName(scripts[i]), lines + " строк."));
        }
    }

    private static void AddToolingChecks(DeploymentReadinessReport report)
    {
        report.Items.Add(DeploymentReadinessItem.Pass(
            "Инструменты",
            "Публикатор релизов",
            "Меню: VN/Выкладка/Публикатор релизов"));
        report.Items.Add(DeploymentReadinessItem.Pass(
            "Инструменты",
            "Локальный сервер релизов",
            "Меню: VN/Выкладка/Локальный сервер релизов"));
        report.Items.Add(DeploymentReadinessItem.Pass(
            "Текущий backend",
            "Публикатор admin-каталога",
            "Использует маршруты создания истории/сезона/эпизода, загрузки контента и публикации."));
        report.Items.Add(DeploymentReadinessItem.Warn(
            "Текущий backend",
            "Маршрут метаданных релиза",
            "/admin/content/releases не описан в текущей документации API; mock/future publisher не является реальным backend."));
        report.Items.Add(DeploymentReadinessItem.Warn(
            "Текущий backend",
            "CDN/R2 для Addressables",
            "CDN/R2 ещё не готов; uploadRootPath остаётся пустым, пока не выдадут бакет, публичный URL и путь загрузки."));
    }

    private static void AddNocturnalToolingChecks(DeploymentReadinessReport report)
    {
        AddFileCheck(
            report,
            "Инструменты Nocturnal",
            "Окно сервера",
            "Assets/_Project/Scripts/Editor/Tools/NocturnalServerTools/NocturnalServerToolsWindow.cs");
        AddFileCheck(
            report,
            "Инструменты Nocturnal",
            "Инструкция",
            "Assets/_Project/Docs/NocturnalServerRunbook.md");
        AddSourceContainsCheck(
            report,
            "Инструменты Nocturnal",
            "Покрытие локальной проверки backend",
            "Assets/_Project/Scripts/Editor/Deployment/Publishing/Backend/Tests/CurrentBackendCatalogTests.cs",
            "MockServer_CompletesEpisodeFlowOverHttp");
        AddSourceContainsCheck(
            report,
            "Инструменты Nocturnal",
            "Покрытие ожидания HTTP в editor coroutine",
            "Assets/_Project/Scripts/Editor/Deployment/Publishing/Backend/Tests/CurrentBackendCatalogTests.cs",
            "EditorCoroutineRunner_WaitsForCurrentBackendRequest");
    }

    private static void AddFileCheck(
        DeploymentReadinessReport report,
        string area,
        string title,
        string path)
    {
        if (File.Exists(path))
            report.Items.Add(DeploymentReadinessItem.Pass(area, title, path));
        else
            report.Items.Add(DeploymentReadinessItem.Fail(area, title, "Не найдено: " + path));
    }

    private static void AddSourceContainsCheck(
        DeploymentReadinessReport report,
        string area,
        string title,
        string path,
        string marker)
    {
        if (!File.Exists(path))
        {
            report.Items.Add(DeploymentReadinessItem.Fail(area, title, "Не найдено: " + path));
            return;
        }

        string text = File.ReadAllText(path);
        if (text.Contains(marker))
            report.Items.Add(DeploymentReadinessItem.Pass(area, title, marker));
        else
            report.Items.Add(DeploymentReadinessItem.Fail(area, title, "Не найден маркер: " + marker));
    }
}
#endif
