#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Ink.UnityIntegration;
using UnityEditor;

public static class DeploymentCliToolingChecks
{
    public static void Run(List<string> lines, ref int failures)
    {
        CheckRunbook(lines, ref failures);
        CheckNocturnalWindow(lines, ref failures);
        CheckManifestValidation(lines, ref failures);
        CheckUploadPlanMetadata(lines, ref failures);
        CheckUploadPlanValidation(lines, ref failures);
        CheckInkIntegration(lines, ref failures);
        CheckScriptBudget(lines, ref failures);
    }

    private static void CheckRunbook(List<string> lines, ref int failures)
    {
        const string path = "Assets/_Project/Docs/NocturnalServerRunbook.md";
        string text = File.Exists(path) ? File.ReadAllText(path) : "";
        Pass(text.Contains("Создать историю"), "инструкция содержит создание истории", lines, ref failures);
        Pass(text.Contains("Опубликовать историю"), "инструкция содержит публикацию истории", lines, ref failures);
        Pass(text.Contains("Локальная проверка"), "инструкция содержит локальную проверку", lines, ref failures);
    }

    private static void CheckInkIntegration(List<string> lines, ref int failures)
    {
        string manifest = ReadText("Packages/manifest.json");
        Pass(manifest.Contains("com.inkle.ink-unity-integration"), "Ink пакет подключён в Unity manifest", lines, ref failures);
        Pass(File.Exists("Assets/_Project/Scripts/Editor/Ink/InkStoryJsonConverter.cs"), "Ink конвертер добавлен", lines, ref failures);
        const string samplePath = "Assets/_MyProject/Ink/Examples/nocturnal_smoke.ink";
        InkStoryJsonMenu.EnsureCompiledAsMaster(samplePath);
        InkFile sample = InkStoryJsonMenu.LoadInkFileAtPath(samplePath);
        Pass(sample != null && sample.isCompiled, "Ink пример компилируется", lines, ref failures);
        string json = "";
        bool converted = sample != null &&
            InkStoryJsonConverter.TryConvert(sample, "", "ink_smoke_episode", out json, out _);
        Pass(converted && StoryJsonConverter.IsCanonicalJson(json), "Ink пример экспортируется в Story JSON", lines, ref failures);
    }

    private static void CheckNocturnalWindow(List<string> lines, ref int failures)
    {
        string text =
            ReadText("Assets/_Project/Scripts/Editor/Tools/NocturnalServerTools/NocturnalServerToolsWindow.cs") +
            ReadText("Assets/_Project/Scripts/Editor/Tools/NocturnalServerTools/NocturnalServerToolsWindow.BackendActions.cs") +
            ReadText("Assets/_Project/Scripts/Editor/Tools/NocturnalServerTools/NocturnalServerToolsWindow.DeploymentActions.cs") +
            ReadText("Assets/_Project/Scripts/Editor/Tools/NocturnalServerTools/NocturnalServerToolsWindow.SafetyActions.cs") +
            ReadText("Assets/_Project/Scripts/Editor/Deployment/Publishing/ContentReleasePublisherWindow.Actions.cs");
        Pass(text.Contains("Проверить проект"), "окно запускает проверку проекта", lines, ref failures);
        Pass(text.Contains("Предпросмотр запросов"), "окно показывает предпросмотр backend-запросов", lines, ref failures);
        Pass(text.Contains("PreviewBackendPayloads"), "окно строит предпросмотр backend-запросов", lines, ref failures);
        Pass(text.Contains("Скопировать ответ"), "окно умеет копировать последний ответ", lines, ref failures);
        Pass(text.Contains("Открыть отчёт"), "окно умеет открыть отчёт проверки", lines, ref failures);
        Pass(text.Contains("_baseUrl = preset.BaseUrl"), "применение среды синхронизирует адрес backend", lines, ref failures);
        Pass(text.Contains("Адрес backend"), "синхронизация адреса backend видна пользователю", lines, ref failures);
        Pass(text.Contains("Перед загрузкой JSON укажите ID эпизода"), "загрузка блокирует пустой ID эпизода", lines, ref failures);
        Pass(text.Contains("Перед загрузкой выберите JSON эпизода"), "загрузка блокирует отсутствие JSON", lines, ref failures);
        Pass(text.Contains("JSON эпизода пустой"), "загрузка блокирует пустой JSON", lines, ref failures);
        Pass(text.Contains("Перед созданием истории укажите ID истории"), "создание блокирует пустой ID истории", lines, ref failures);
        Pass(text.Contains("Перед добавлением сезона укажите ID истории"), "добавление сезона блокирует пустой ID истории", lines, ref failures);
        Pass(text.Contains("Перед добавлением сезона укажите ID сезона"), "добавление сезона блокирует пустой ID сезона", lines, ref failures);
        Pass(text.Contains("Перед добавлением эпизода укажите ID сезона"), "добавление эпизода блокирует пустой ID сезона", lines, ref failures);
        Pass(text.Contains("Перед добавлением эпизода укажите ID эпизода"), "добавление эпизода блокирует пустой ID эпизода", lines, ref failures);
        Pass(text.Contains("Перед публикацией или скрытием истории"), "публикация блокирует пустой ID истории", lines, ref failures);
        Pass(text.Contains("Перед публикацией или скрытием эпизода"), "публикация блокирует пустой ID эпизода", lines, ref failures);
        Pass(text.Contains("Для записи на удалённый сервер нужен X-Admin-Key"), "удалённая запись требует ключ администратора", lines, ref failures);
        Pass(text.Contains("Перед записью в Прод выключите"), "запись в Прод блокирует режим без ключа", lines, ref failures);
        Pass(text.Contains("Среда: \" + SelectedEnvironmentId"), "подтверждение записи показывает среду", lines, ref failures);
        Pass(text.Contains("StopWithResponse(!string.IsNullOrEmpty(mismatch), mismatch)"), "запись блокирует несовпадение среды и сервера", lines, ref failures);
        Pass(text.Contains("Перед запросами в Прод выключите"), "публикатор релизов блокирует Прод без ключа", lines, ref failures);
        Pass(text.Contains("Среда релиза не совпадает с адресом сервера"), "публикатор релизов блокирует несовпадение среды", lines, ref failures);
    }

    private static void CheckScriptBudget(List<string> lines, ref int failures)
    {
        Budget("Editor/Deployment", "Assets/_Project/Scripts/Editor/Deployment", lines, ref failures);
        Budget("Editor/Backend", "Assets/_Project/Scripts/Editor/Deployment/Publishing/Backend", lines, ref failures);
        Budget("Editor/NocturnalTools", "Assets/_Project/Scripts/Editor/Tools/NocturnalServerTools", lines, ref failures);
        Budget("Editor/Publishing", "Assets/_Project/Scripts/Editor/Deployment/Publishing", lines, ref failures);
        Budget("Editor/PublishingMockServer", "Assets/_Project/Scripts/Editor/Deployment/Publishing/MockServer", lines, ref failures);
        Budget("Editor/PublishingMedia", "Assets/_Project/Scripts/Editor/Deployment/Publishing/Media", lines, ref failures);
        Budget("Editor/PublishingUpload", "Assets/_Project/Scripts/Editor/Deployment/Publishing/Upload", lines, ref failures);
        Budget("Editor/Readiness", "Assets/_Project/Scripts/Editor/Deployment/Readiness", lines, ref failures);
        Budget("Editor/Ink", "Assets/_Project/Scripts/Editor/Ink", lines, ref failures);
        Budget("Tests/Backend", "Assets/_Project/Scripts/Editor/Deployment/Publishing/Backend/Tests", lines, ref failures);
        Budget("Tests/Deployment", "Assets/_Project/Scripts/Editor/Deployment/Tests", lines, ref failures);
        Budget("Tests/Publishing", "Assets/_Project/Scripts/Editor/Deployment/Publishing/Tests", lines, ref failures);
        Budget("Tests/PublishingMockServer", "Assets/_Project/Scripts/Editor/Deployment/Publishing/MockServer/Tests", lines, ref failures);
        Budget("Tests/PublishingUpload", "Assets/_Project/Scripts/Editor/Deployment/Publishing/Upload/Tests", lines, ref failures);
        Budget("Tests/Readiness", "Assets/_Project/Scripts/Editor/Deployment/Readiness/Tests", lines, ref failures);
    }

    private static void CheckUploadPlanValidation(List<string> lines, ref int failures)
    {
        var empty = new ContentReleaseBuildManifest { fileCount = 0, totalBytes = 0 };
        bool emptyOk = ContentReleaseUploadPlanBuilder.ValidateManifestFiles(empty, "Library/MissingUploadPlan", out string emptyError);
        Pass(!emptyOk && emptyError.Contains("нет файлов"), "план загрузки отклоняет пустой manifest", lines, ref failures);

        var missing = new ContentReleaseBuildManifest { fileCount = 1, totalBytes = 10 };
        missing.files.Add(new ContentReleaseBuildManifestFile { path = "missing.bundle", bytes = 10, sha256 = "hash" });
        bool missingOk = ContentReleaseUploadPlanBuilder.ValidateManifestFiles(missing, "Library/MissingUploadPlan", out string missingError);
        Pass(!missingOk && missingError.Contains("не найден"), "план загрузки отклоняет отсутствующие файлы", lines, ref failures);

        var escaping = new ContentReleaseBuildManifest { fileCount = 1, totalBytes = 10 };
        escaping.files.Add(new ContentReleaseBuildManifestFile { path = "../evil.bundle", bytes = 10, sha256 = "hash" });
        bool escapingOk = ContentReleaseUploadPlanBuilder.ValidateManifestFiles(escaping, "Library/MissingUploadPlan", out string escapingError);
        Pass(!escapingOk && escapingError.Contains("выходит за папку"), "план загрузки отклоняет выход за папку сборки", lines, ref failures);

        string hashDirectory = Path.Combine("Library", "DeploymentCliVerifier", Path.GetRandomFileName());
        Directory.CreateDirectory(hashDirectory);
        File.WriteAllBytes(Path.Combine(hashDirectory, "bundle.bin"), new byte[] { 1, 2, 3, 4 });
        var hashMismatch = new ContentReleaseBuildManifest { fileCount = 1, totalBytes = 4 };
        hashMismatch.files.Add(new ContentReleaseBuildManifestFile { path = "bundle.bin", bytes = 4, sha256 = "wrong" });
        bool hashOk = ContentReleaseUploadPlanBuilder.ValidateManifestFiles(hashMismatch, hashDirectory, out string hashError);
        Pass(!hashOk && hashError.Contains("Хеш"), "план загрузки отклоняет несовпадение хеша", lines, ref failures);
        Directory.Delete(hashDirectory, true);
    }

    private static void CheckUploadPlanMetadata(List<string> lines, ref int failures)
    {
        var manifest = new ContentReleaseBuildManifest
        {
            environmentId = DeploymentEnvironmentIds.Production,
            channel = ContentReleaseChannel.Stage,
            buildTarget = "StandaloneWindows64"
        };
        bool envOk = ContentReleaseUploadPlanBuilder.ValidateManifestMetadata(manifest, DeploymentEnvironmentPresets.Stage, "StandaloneWindows64", out string envError);
        Pass(!envOk && envError.Contains("Среда"), "план загрузки отклоняет несовпадение среды", lines, ref failures);

        manifest.environmentId = DeploymentEnvironmentIds.Stage;
        manifest.channel = ContentReleaseChannel.Production;
        bool channelOk = ContentReleaseUploadPlanBuilder.ValidateManifestMetadata(manifest, DeploymentEnvironmentPresets.Stage, "StandaloneWindows64", out string channelError);
        Pass(!channelOk && channelError.Contains("Канал"), "план загрузки отклоняет несовпадение канала", lines, ref failures);

        manifest.channel = ContentReleaseChannel.Stage;
        bool targetOk = ContentReleaseUploadPlanBuilder.ValidateManifestMetadata(manifest, DeploymentEnvironmentPresets.Stage, "Android", out string targetError);
        Pass(!targetOk && targetError.Contains("Платформа"), "план загрузки отклоняет несовпадение платформы", lines, ref failures);
    }

    private static void CheckManifestValidation(List<string> lines, ref int failures)
    {
        string emptyDirectory = Path.Combine("Library", "DeploymentCliVerifier", Path.GetRandomFileName());
        Directory.CreateDirectory(emptyDirectory);
        bool ok = ContentReleaseManifestBuilder.TryBuildFromDirectory(
            emptyDirectory,
            DeploymentEnvironmentPresets.Stage,
            "StandaloneWindows64",
            "2026.07.14.1",
            out _,
            out string error);
        Pass(!ok && error.Contains("нет файлов"), "manifest отклоняет пустую папку сборки", lines, ref failures);
        Directory.Delete(emptyDirectory, true);
    }

    private static void Budget(string label, string folder, List<string> lines, ref int failures)
    {
        string[] scripts = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.cs", SearchOption.TopDirectoryOnly) : new string[0];
        Pass(scripts.Length > 0 && scripts.Length <= 7, label + " количество скриптов", lines, ref failures);
        for (int i = 0; i < scripts.Length; i++)
            Pass(File.ReadAllLines(scripts[i]).Length <= 200, label + " " + Path.GetFileName(scripts[i]) + " строки", lines, ref failures);
    }

    private static string ReadText(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    private static void Pass(bool ok, string label, List<string> lines, ref int failures)
    {
        lines.Add((ok ? "PASS " : "FAIL ") + label);
        if (!ok)
            failures++;
    }
}
#endif
