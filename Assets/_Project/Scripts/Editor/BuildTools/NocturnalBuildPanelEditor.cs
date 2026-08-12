#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NocturnalBuildPanel))]
public sealed class NocturnalBuildPanelEditor : Editor
{
    private SerializedProperty _environment;
    private SerializedProperty _outputRoot;
    private SerializedProperty _versionName;
    private SerializedProperty _androidVersionCode;
    private SerializedProperty _iosBuildNumber;

    private SerializedProperty _applyEnvironmentBeforeBuild;
    private SerializedProperty _buildAddressablesBeforePlayer;
    private SerializedProperty _developmentBuild;
    private SerializedProperty _strictMode;
    private SerializedProperty _autoRunAfterBuild;
    private SerializedProperty _showFolderAfterBuild;

    private SerializedProperty _managedStrippingLevel;
    private SerializedProperty _il2CppCodeGeneration;
    private SerializedProperty _il2CppCompilerConfiguration;

    private SerializedProperty _androidPackageFormat;
    private SerializedProperty _androidIncludeArmv7;
    private SerializedProperty _androidMinifyRelease;
    private SerializedProperty _androidCreateSymbols;
    private SerializedProperty _androidOptimizedFramePacing;

    private SerializedProperty _iosMinimumVersion;
    private SerializedProperty _iosSupportsIPad;
    private SerializedProperty _iosAutomaticSigning;
    private SerializedProperty _iosAppleDeveloperTeamId;

    private SerializedProperty _desktopBuildTarget;

    private void OnEnable()
    {
        _environment = Find("_environment");
        _outputRoot = Find("_outputRoot");
        _versionName = Find("_versionName");
        _androidVersionCode = Find("_androidVersionCode");
        _iosBuildNumber = Find("_iosBuildNumber");

        _applyEnvironmentBeforeBuild = Find("_applyEnvironmentBeforeBuild");
        _buildAddressablesBeforePlayer = Find("_buildAddressablesBeforePlayer");
        _developmentBuild = Find("_developmentBuild");
        _strictMode = Find("_strictMode");
        _autoRunAfterBuild = Find("_autoRunAfterBuild");
        _showFolderAfterBuild = Find("_showFolderAfterBuild");

        _managedStrippingLevel = Find("_managedStrippingLevel");
        _il2CppCodeGeneration = Find("_il2CppCodeGeneration");
        _il2CppCompilerConfiguration = Find("_il2CppCompilerConfiguration");

        _androidPackageFormat = Find("_androidPackageFormat");
        _androidIncludeArmv7 = Find("_androidIncludeArmv7");
        _androidMinifyRelease = Find("_androidMinifyRelease");
        _androidCreateSymbols = Find("_androidCreateSymbols");
        _androidOptimizedFramePacing = Find("_androidOptimizedFramePacing");

        _iosMinimumVersion = Find("_iosMinimumVersion");
        _iosSupportsIPad = Find("_iosSupportsIPad");
        _iosAutomaticSigning = Find("_iosAutomaticSigning");
        _iosAppleDeveloperTeamId = Find("_iosAppleDeveloperTeamId");

        _desktopBuildTarget = Find("_desktopBuildTarget");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox(
            "Единый сборщик production/stage билдов. Перед player-билдом он может применить окружение, собрать Addressables и выставить проверенные мобильные настройки.",
            MessageType.Info);

        BeginCategory("1. Релиз и версии");
        Draw(_environment, "Среда", "Stage или Production. Меняет backend, Addressables и идентификаторы через deployment preset.");
        Draw(_outputRoot, "Папка билдов", "Относительный путь считается от корня проекта.");
        Draw(_versionName, "Версия приложения", "Bundle Version / Version Name, например 1.2.0.");
        Draw(_androidVersionCode, "Android Version Code", "Для каждой публикации в магазине увеличивайте число.");
        Draw(_iosBuildNumber, "iOS Build Number", "CFBundleVersion. Для каждой загрузки в App Store Connect увеличивайте число.");
        if (_environment.enumValueIndex == (int)NocturnalBuildEnvironment.Stage)
        {
            EditorGUILayout.HelpBox(
                "Сейчас выбрана Stage-среда. Для магазинного релиза переключите на Production.",
                MessageType.Warning);
        }
        EndCategory();

        BeginCategory("2. Подготовка контента");
        Draw(_applyEnvironmentBeforeBuild, "Применять среду", "Записывает выбранный Stage/Production preset перед билдом.");
        Draw(_buildAddressablesBeforePlayer, "Собирать Addressables", "Пересобирает Addressables после переключения целевой платформы.");
        Draw(_strictMode, "Строгая сборка", "Останавливает билд при ошибках и запрещает Production Development Build.");
        Draw(_showFolderAfterBuild, "Открыть результат", "Открывает готовый файл или Xcode-проект в проводнике.");
        EndCategory();

        BeginCategory("3. Тип и оптимизация");
        bool production = _environment.enumValueIndex == (int)NocturnalBuildEnvironment.Production;
        using (new EditorGUI.DisabledScope(production))
        {
            Draw(_developmentBuild, "Development Build", "Только для Stage-диагностики. В Production принудительно выключается.");
        }
        if (production)
        {
            EditorGUILayout.HelpBox(
                "Production всегда собирается без Development, Script Debugging и подключения Profiler.",
                MessageType.Info);
        }
        Draw(_autoRunAfterBuild, "Автозапуск обычного билда", "Работает для Android APK и desktop. Для iOS/Xcode и AAB не применяется.");
        Draw(_managedStrippingLevel, "Managed Stripping", "Low — безопасный релизный баланс. Medium/High требуют полного тестирования reflection и link.xml.");
        Draw(_il2CppCodeGeneration, "IL2CPP Code Generation", "Faster Runtime оптимизирует скорость игры; Faster Smaller Builds уменьшает код и время сборки.");
        Draw(_il2CppCompilerConfiguration, "C++ Configuration", "Release — рекомендуемый баланс. Master сильнее оптимизирует, но заметно дольше собирается.");
        EndCategory();

        BeginCategory("4. Android");
        Draw(_androidPackageFormat, "Формат публикации", "APK — установка на устройство. AAB — публикация в Google Play.");
        Draw(_androidIncludeArmv7, "Добавить ARMv7", "По умолчанию используется только ARM64. Включите для старых 32-битных устройств.");
        Draw(_androidMinifyRelease, "R8/Minify Release", "Уменьшает Java/Kotlin часть, но требует проверки правил keep для SDK рекламы и аналитики.");
        Draw(_androidCreateSymbols, "Создавать symbols.zip", "Public symbols нужны для расшифровки native crash в Google Play.");
        Draw(_androidOptimizedFramePacing, "Optimized Frame Pacing", "Равномернее распределяет кадры и уменьшает variance frame time.");
        if (_androidPackageFormat.enumValueIndex == (int)NocturnalAndroidPackageFormat.AppBundle)
        {
            EditorGUILayout.HelpBox(
                "AAB предназначен для магазина и напрямую не запускается. Кнопка «Собрать и запустить APK» всегда создаёт отдельный APK.",
                MessageType.Info);
            if (_environment.enumValueIndex == (int)NocturnalBuildEnvironment.Production &&
                !PlayerSettings.Android.useCustomKeystore)
            {
                EditorGUILayout.HelpBox(
                    "Для Production AAB настройте Custom Keystore и Key Alias в Player Settings > Android > Publishing Settings.",
                    MessageType.Error);
            }
        }
        EndCategory();

        BeginCategory("5. iOS");
        Draw(_iosMinimumVersion, "Минимальная iOS", "Формат Major.Minor, например 13.0.");
        Draw(_iosSupportsIPad, "iPhone + iPad", "Если выключено, Xcode-проект предназначен только для iPhone.");
        Draw(_iosAutomaticSigning, "Automatic Signing", "Применяется только когда заполнен Apple Team ID.");
        Draw(_iosAppleDeveloperTeamId, "Apple Team ID", "Не пароль. Если поле пустое, текущие настройки подписи Unity не перезаписываются.");
        EditorGUILayout.HelpBox(
            Application.platform == RuntimePlatform.OSXEditor
                ? "Будет создан Xcode-проект. Подпись, Archive и отправка выполняются в Xcode."
                : "На Windows Unity создаст Xcode-проект. Финальный .ipa, подпись, Archive и запуск требуют macOS с Xcode.",
            MessageType.Info);
        EndCategory();

        BeginCategory("6. Desktop");
        Draw(_desktopBuildTarget, "Целевая система", "Windows x64, Linux x64 или macOS.");
        EndCategory();

        serializedObject.ApplyModifiedProperties();
        DrawBuildActions();
    }

    private void DrawBuildActions()
    {
        BeginCategory("7. Android — действия");
        EditorGUILayout.BeginHorizontal();
        if (Button("Собрать Android", "Создаёт выбранный APK или AAB."))
            Run(NocturnalBuildRunner.BuildAndroid);
        if (Button("Собрать и запустить APK", "Всегда создаёт APK, устанавливает и запускает его на выбранном Android-устройстве."))
            Run(NocturnalBuildRunner.BuildAndRunAndroid);
        EditorGUILayout.EndHorizontal();
        EndCategory();

        BeginCategory("8. iOS — действия");
        if (Button("Создать iOS Xcode-проект", "Создаёт оптимизированный Xcode-проект для устройства iOS."))
            Run(NocturnalBuildRunner.BuildIos);
        EndCategory();

        BeginCategory("9. Desktop и пакетные действия");
        EditorGUILayout.BeginHorizontal();
        if (Button("Собрать Desktop", "Создаёт desktop-билд выбранной платформы."))
            Run(NocturnalBuildRunner.BuildDesktop);
        if (Button("Собрать Android + iOS", "Последовательно создаёт Android и iOS билды."))
            Run(NocturnalBuildRunner.BuildMobile);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (Button("Применить настройки", "Применяет оптимальные настройки без запуска сборки."))
            Run(NocturnalBuildRunner.ApplyOptimalSettings);
        if (Button("Открыть папку билдов", "Открывает корневую папку выбранной среды."))
            Run(NocturnalBuildRunner.OpenBuildFolder);
        EditorGUILayout.EndHorizontal();
        EndCategory();
    }

    private SerializedProperty Find(string name)
    {
        return serializedObject.FindProperty(name);
    }

    private static void BeginCategory(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.Space(2f);
    }

    private static void EndCategory()
    {
        EditorGUILayout.EndVertical();
    }

    private static void Draw(SerializedProperty property, string label, string tooltip)
    {
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
    }

    private static bool Button(string label, string tooltip)
    {
        return GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(36f));
    }

    private void Run(System.Action<NocturnalBuildPanel> action)
    {
        serializedObject.ApplyModifiedProperties();
        NocturnalBuildPanel panel = (NocturnalBuildPanel)target;
        EditorApplication.delayCall += () =>
        {
            if (panel != null)
                action(panel);
        };
    }

    private void Run(System.Func<NocturnalBuildPanel, bool> action)
    {
        serializedObject.ApplyModifiedProperties();
        NocturnalBuildPanel panel = (NocturnalBuildPanel)target;
        EditorApplication.delayCall += () =>
        {
            if (panel != null)
                action(panel);
        };
    }
}
#endif
