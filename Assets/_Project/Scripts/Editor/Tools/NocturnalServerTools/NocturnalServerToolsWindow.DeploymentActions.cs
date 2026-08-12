#if UNITY_EDITOR
public sealed partial class NocturnalServerToolsWindow
{
    private const float ActionButtonMinWidth = 180f;
    private const float ActionButtonMaxWidth = 260f;
    private const float ActionButtonHeight = 32f;

    private static bool ActionButton(string text, string tooltip) => UnityEngine.GUILayout.Button(new UnityEngine.GUIContent(text, tooltip), UnityEngine.GUILayout.MinWidth(ActionButtonMinWidth), UnityEngine.GUILayout.MaxWidth(ActionButtonMaxWidth), UnityEngine.GUILayout.Height(ActionButtonHeight));
    private static void BeginActionRow() => UnityEditor.EditorGUILayout.BeginHorizontal();
    private static void EndActionRow() { UnityEngine.GUILayout.FlexibleSpace(); UnityEditor.EditorGUILayout.EndHorizontal(); }
    private static void ActionGroup(string title) { UnityEditor.EditorGUILayout.Space(10f); UnityEditor.EditorGUILayout.LabelField(title, UnityEditor.EditorStyles.miniBoldLabel); }

    private void DrawPageTabs()
    {
        BeginActionRow();
        for (int i = 0; i < PageLabels.Length; i++)
            if (UnityEngine.GUILayout.Toggle(_pageIndex == i, PageLabels[i], UnityEngine.GUI.skin.button, UnityEngine.GUILayout.MinWidth(96f), UnityEngine.GUILayout.MaxWidth(132f), UnityEngine.GUILayout.Height(28f)))
                _pageIndex = i;
        EndActionRow();
        UnityEditor.EditorGUILayout.Space(8f);
    }

    private void DrawSelectedPage()
    {
        if (_pageIndex == 0) DrawMockServers();
        else if (_pageIndex >= 1 && _pageIndex <= 4) DrawCurrentBackend();
        else if (_pageIndex == 5) DrawDeploymentArtifacts();
        else if (_pageIndex == 6) DrawMediaPage();
        else DrawOpenWindows();
    }

    private string SelectedEnvironmentId => EnvironmentIds[
        UnityEngine.Mathf.Clamp(_environmentIndex, 0, EnvironmentIds.Length - 1)];

    private void DrawDeploymentArtifacts()
    {
        UnityEditor.EditorGUILayout.Space(8f);
        UnityEditor.EditorGUILayout.LabelField("Выкладка и проверки", UnityEditor.EditorStyles.boldLabel);
        _environmentIndex = UnityEngine.GUILayout.Toolbar(_environmentIndex, EnvironmentLabels, UnityEngine.GUILayout.MaxWidth(440f), UnityEngine.GUILayout.Height(ActionButtonHeight));
        _contentVersion = UnityEditor.EditorGUILayout.TextField(new UnityEngine.GUIContent("Версия контента", "Версия сборки контента, например 2026.07.14.1."), _contentVersion);
        ContentReleaseUploadDestinationEditor.Draw(SelectedEnvironmentId);

        ActionGroup("1. Среда проекта");
        BeginActionRow();
        if (ActionButton("Применить среду", "Переключает настройки проекта под Тест или Прод."))
            ApplySelectedEnvironment();
        if (ActionButton("Проверить настройки", "Проверяет, что окружение и пути готовы к выкладке."))
            DeploymentEnvironmentMenu.Validate();
        EndActionRow();

        ActionGroup("2. Сборка файлов");
        UnityEditor.EditorGUILayout.HelpBox(
            "План загрузки не отправляет файлы сам. Он создаёт Markdown/JSON с абсолютными локальными путями и адресами назначения. Пока CDN/R2 не готов, пустой путь загрузки означает ожидание доступа.",
            UnityEditor.MessageType.Info);
        BeginActionRow();
        if (ActionButton("Собрать Addressables", "Собирает игровые файлы для загрузки на сервер или хранилище."))
            DeploymentEnvironmentMenu.BuildActiveAddressables();
        if (ActionButton("Собрать manifest", "Создаёт manifest с файлами, хешами и адресами загрузки."))
            BuildSelectedManifest();
        if (ActionButton("Создать план загрузки", "Создаёт Markdown/JSON со списком абсолютных локальных путей и адресов назначения."))
            GenerateSelectedUploadPlan();
        EndActionRow();

        ActionGroup("3. Отчёты и папки");
        BeginActionRow();
        if (ActionButton("Отчёт готовности", "Собирает отчёт с блокирующими проблемами перед публикацией."))
            DeploymentReadinessMenu.Generate();
        if (ActionButton("Проверить проект", "Запускает внутреннюю проверку окон, маршрутов, manifest и лимитов скриптов."))
            RunCliVerification();
        if (ActionButton("Открыть отчёт", "Открывает последний отчёт проверки проекта."))
            OpenCliVerificationReport();
        EndActionRow();

        BeginActionRow();
        if (ActionButton("Папка сборки", "Открывает папку, куда Unity собрала Addressables."))
            OpenSelectedBuildFolder();
        if (ActionButton("Папка плана", "Открывает папку с планами загрузки."))
            OpenUploadPlanFolder();
        EndActionRow();
    }

    private void ApplySelectedEnvironment()
    {
        DeploymentEnvironmentPreset preset = DeploymentEnvironmentPresets.Find(SelectedEnvironmentId);
        if (DeploymentEnvironmentIds.IsProduction(SelectedEnvironmentId))
            DeploymentEnvironmentMenu.ApplyProduction();
        else
            DeploymentEnvironmentMenu.ApplyStage();

        _baseUrl = preset.BaseUrl;
        SavePrefs();
        _lastResponse = "Среда применена:\n" + preset.DisplayName + "\nАдрес backend: " + _baseUrl;
        Repaint();
    }

    private void BuildSelectedManifest()
    {
        SavePrefs();
        if (!ContentReleaseManifestBuilder.TryWrite(
                SelectedEnvironmentId,
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

        _lastResponse = "Manifest собран:\n" + path + "\n" + url + "\n" + hash;
        Repaint();
    }

    private void GenerateSelectedUploadPlan()
    {
        SavePrefs();
        if (!ContentReleaseUploadPlanBuilder.TryWrite(
                SelectedEnvironmentId,
                out string jsonPath,
                out string markdownPath,
                out string error))
        {
            _lastResponse = "План загрузки не создан: " + error;
            Repaint();
            return;
        }

        string markdownFullPath = System.IO.Path.GetFullPath(markdownPath);
        string jsonFullPath = System.IO.Path.GetFullPath(jsonPath);
        UnityEditor.EditorGUIUtility.systemCopyBuffer = markdownFullPath;
        _lastResponse = "План загрузки создан. Абсолютный путь Markdown скопирован:\n" +
            markdownFullPath + "\n\nJSON:\n" + jsonFullPath +
            "\n\nОткройте Markdown и загрузите каждый локальный файл по указанному адресу назначения.";
        Repaint();
    }

    private void OpenSelectedBuildFolder()
    {
        DeploymentEnvironmentPreset preset = DeploymentEnvironmentPresets.Find(SelectedEnvironmentId);
        string buildTarget = UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString();
        string path = System.IO.Path.GetFullPath(ContentReleaseManifestBuilder.ResolveTokens(
            preset.AddressablesBuildPath,
            buildTarget));
        if (!System.IO.Directory.Exists(path))
        {
            _lastResponse = "Папка сборки не найдена. Сначала нажмите «Собрать Addressables»:\n" + path;
            Repaint();
            return;
        }

        UnityEditor.EditorUtility.RevealInFinder(path);
    }

    private static void OpenUploadPlanFolder()
    {
        System.IO.Directory.CreateDirectory(ContentReleaseUploadPlanBuilder.OutputDirectory);
        UnityEditor.EditorUtility.RevealInFinder(
            System.IO.Path.GetFullPath(ContentReleaseUploadPlanBuilder.OutputDirectory));
    }

    private void OpenCliVerificationReport()
    {
        string path = System.IO.Path.GetFullPath(DeploymentCliVerifier.ReportPath);
        if (!System.IO.File.Exists(path))
        {
            _lastResponse = "Отчёт проверки не найден. Сначала нажмите «Проверить проект»:\n" + path;
            Repaint();
            return;
        }

        UnityEditor.EditorUtility.RevealInFinder(path);
    }

    private void RunCliVerification()
    {
        int failures = DeploymentCliVerifier.RunNow();
        string path = System.IO.Path.GetFullPath(DeploymentCliVerifier.ReportPath);
        _lastResponse = failures == 0
            ? "Проверка проекта пройдена:\n" + path
            : "Проверка проекта нашла проблемы: " + failures + "\n" + path;
        Repaint();
    }

    private void CopyCliVerificationCommand()
    {
        string command = NocturnalServerCommandBuilder.BuildCliVerificationCommand(
            UnityEditor.EditorApplication.applicationPath,
            System.IO.Directory.GetCurrentDirectory());
        UnityEditor.EditorGUIUtility.systemCopyBuffer = command;
        _lastResponse = "Команда проверки скопирована:\n" + command;
        Repaint();
    }
}
#endif
