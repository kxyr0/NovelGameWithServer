#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NocturnalBuildRunner
{
    public static void ApplyOptimalSettings(NocturnalBuildPanel panel)
    {
        if (panel == null)
            return;

        ApplyEnvironment(panel);
        ApplyCommonSettings(panel);
        ApplyAndroidSettings(panel);
        ApplyIosSettings(panel);
        ApplyStandaloneSettings(panel);
        AssetDatabase.SaveAssets();
        Debug.Log("[Nocturnal Build] Оптимальные настройки Android, iOS и desktop применены.");
    }

    public static bool BuildAndroid(NocturnalBuildPanel panel)
    {
        return BuildAndroidInternal(panel, false);
    }

    public static bool BuildAndRunAndroid(NocturnalBuildPanel panel)
    {
        return BuildAndroidInternal(panel, true);
    }

    public static bool BuildIos(NocturnalBuildPanel panel)
    {
        ApplyOptimalSettings(panel);
        string path = NocturnalBuildPaths.IosProjectPath(panel);
        if (Application.platform != RuntimePlatform.OSXEditor)
        {
            Debug.LogWarning(
                "[Nocturnal Build] Unity создаст Xcode-проект. Финальная сборка, подпись, архив и запуск iOS выполняются на macOS в Xcode.");
        }

        return Build(panel, BuildTargetGroup.iOS, BuildTarget.iOS, path, false);
    }

    public static bool BuildDesktop(NocturnalBuildPanel panel)
    {
        ApplyOptimalSettings(panel);
        BuildTarget target = ResolveDesktopTarget(panel.DesktopBuildTarget);
        string folder = Path.Combine(
            NocturnalBuildPaths.BuildRoot(panel),
            panel.DesktopBuildTarget.ToString());
        string path = Path.Combine(folder, NocturnalBuildPaths.DesktopExecutableName(target));
        return Build(panel, BuildTargetGroup.Standalone, target, path, false);
    }

    public static void BuildMobile(NocturnalBuildPanel panel)
    {
        if (BuildAndroid(panel))
            BuildIos(panel);
    }

    public static void BuildBoth(NocturnalBuildPanel panel)
    {
        if (BuildAndroid(panel))
            BuildDesktop(panel);
    }

    public static void OpenBuildFolder(NocturnalBuildPanel panel)
    {
        string root = NocturnalBuildPaths.BuildRoot(panel);
        Directory.CreateDirectory(root);
        EditorUtility.RevealInFinder(root);
    }

    private static bool BuildAndroidInternal(NocturnalBuildPanel panel, bool buildAndRun)
    {
        ApplyOptimalSettings(panel);
        EditorUserBuildSettings.buildAppBundle = !buildAndRun && panel.AndroidBuildsAppBundle;

        if (!buildAndRun && !ValidateAndroidPublishing(panel))
            return false;

        string extension = EditorUserBuildSettings.buildAppBundle ? "aab" : "apk";
        string category = buildAndRun ? "Android/Device" : "Android";
        string path = Path.Combine(
            NocturnalBuildPaths.BuildRoot(panel),
            category,
            NocturnalBuildPaths.AndroidFileName(panel, extension));
        return Build(panel, BuildTargetGroup.Android, BuildTarget.Android, path, buildAndRun);
    }

    private static bool Build(
        NocturnalBuildPanel panel,
        BuildTargetGroup group,
        BuildTarget target,
        string path,
        bool forceAutoRun)
    {
        if (!ValidateBuildReadiness(panel, group, target))
            return false;

        string[] scenes = EnabledScenes();
        if (scenes.Length == 0)
            return Fail("В Build Settings нет включённых сцен.");

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return Fail("Сборка отменена: изменения открытых сцен не сохранены.");

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (!SwitchTarget(group, target))
            return false;

        AddressableAssetSettings addressableSettings =
            AddressableAssetSettingsDefaultObject.Settings;
        AddressableAssetSettings.PlayerBuildOption previousAddressablesBuildOption =
            addressableSettings != null
                ? addressableSettings.BuildAddressablesWithPlayerBuild
                : AddressableAssetSettings.PlayerBuildOption.PreferencesValue;

        try
        {
            // The runner owns content preparation. Suppress Addressables' player-build
            // hook so the same catalog and bundles are not built for a second time.
            if (addressableSettings != null)
            {
                addressableSettings.BuildAddressablesWithPlayerBuild =
                    AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            }

            if (target == BuildTarget.Android &&
                !GooglePlayServices.PlayServicesResolver.ResolveSync(true))
            {
                return Fail("Не удалось подготовить Android-зависимости через External Dependency Manager.");
            }

            // Older generated JSON graphs could have persisted the converter's
            // transient DontSave flag. Repair them before Unity starts serializing
            // build assets; otherwise the first such graph aborts the player build.
            StoryJsonAutoImporter.RepairGeneratedGraphHideFlags();

            if (panel.BuildAddressablesBeforePlayer)
            {
                // Build-layout generation is diagnostic-only. In Addressables 1.21.21
                // its auto-open report can log a false error while the new JSON report
                // is still memory-mapped, and StrictMode then rejects a valid player build.
                ProjectConfigData.GenerateBuildLayout = false;
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
                if (result == null || !string.IsNullOrEmpty(result.Error))
                {
                    return Fail(
                        "Сборка Addressables не удалась: " +
                        (result != null ? result.Error : "результат сборки отсутствует"));
                }
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = ResolveBuildOptions(panel, target, forceAutoRun)
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool success = report != null && report.summary.result == BuildResult.Succeeded;
            string message = success
                ? "Сборка готова:\n" + Path.GetFullPath(path) +
                  "\nРазмер: " + FormatSize(report.summary.totalSize) +
                  "\nВремя: " + report.summary.totalTime
                : "Сборка не удалась: " +
                  (report != null ? report.summary.result.ToString() : "BuildReport отсутствует");

            Debug.Log("[Nocturnal Build] " + message);
            EditorUtility.DisplayDialog("Nocturnal Build", message, "OK");
            if (success && panel.ShowFolderAfterBuild)
                EditorUtility.RevealInFinder(Path.GetFullPath(path));
            return success;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return Fail("Сборка завершилась исключением: " + exception.Message);
        }
        finally
        {
            if (addressableSettings != null)
            {
                addressableSettings.BuildAddressablesWithPlayerBuild =
                    previousAddressablesBuildOption;
            }
        }
    }

    private static bool ValidateBuildReadiness(
        NocturnalBuildPanel panel,
        BuildTargetGroup group,
        BuildTarget target)
    {
        if (panel == null)
            return Fail("Компонент сборщика не найден.");
        if (EditorApplication.isCompiling)
            return Fail("Unity сейчас компилирует скрипты. Дождитесь завершения и повторите сборку.");
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return Fail("Остановите Play Mode перед созданием билда.");
        if (BuildPipeline.isBuildingPlayer)
            return Fail("Другой player-билд уже выполняется.");
        if (!BuildPipeline.IsBuildTargetSupported(group, target))
            return Fail("Модуль сборки не установлен: " + target + ". Добавьте его через Unity Hub.");
        if (panel.Environment == NocturnalBuildEnvironment.Production &&
            panel.DevelopmentBuild && panel.StrictMode)
        {
            return Fail("Production + Development Build запрещён строгим режимом. Отключите Development Build или строгий режим.");
        }

        string identifier = PlayerSettings.GetApplicationIdentifier(
            NamedBuildTarget.FromBuildTargetGroup(group));
        if (string.IsNullOrWhiteSpace(identifier))
            return Fail("Для " + target + " не задан Application Identifier / Bundle Identifier.");
        return true;
    }

    private static bool ValidateAndroidPublishing(NocturnalBuildPanel panel)
    {
        if (panel.Environment != NocturnalBuildEnvironment.Production ||
            !panel.AndroidBuildsAppBundle || !panel.StrictMode)
        {
            return true;
        }

        if (!PlayerSettings.Android.useCustomKeystore)
        {
            return Fail(
                "Production AAB нельзя выпускать с debug-подписью. Настройте Custom Keystore в Player Settings > Android > Publishing Settings.");
        }

        if (string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
            return Fail("Для Production AAB не выбран Key Alias в Android Publishing Settings.");
        return true;
    }

    private static void ApplyEnvironment(NocturnalBuildPanel panel)
    {
        if (!panel.ApplyEnvironmentBeforeBuild)
            return;

        string environmentId = panel.Environment == NocturnalBuildEnvironment.Production
            ? DeploymentEnvironmentIds.Production
            : DeploymentEnvironmentIds.Stage;
        DeploymentEnvironmentPreset preset = ContentReleaseUploadDestinationSettings.ApplyToPreset(
            DeploymentEnvironmentPresets.Find(environmentId));
        DeploymentConfigWriter.SaveSelected(preset.EnvironmentId);
        DeploymentConfigWriter.SaveTemplates();
        AddressablesDeploymentConfigurator.Apply(preset);
        DeploymentBuildSettingsConfigurator.Apply(preset);
    }

    private static void ApplyCommonSettings(NocturnalBuildPanel panel)
    {
        PlayerSettings.bundleVersion = panel.VersionName;
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.gcIncremental = true;
        EditorUserBuildSettings.development = panel.DevelopmentBuild;
        EditorUserBuildSettings.allowDebugging = panel.DevelopmentBuild;
        EditorUserBuildSettings.connectProfiler = panel.DevelopmentBuild;
    }

    private static void ApplyAndroidSettings(NocturnalBuildPanel panel)
    {
        PlayerSettings.Android.bundleVersionCode = panel.AndroidVersionCode;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.targetArchitectures = panel.AndroidIncludeArmv7
            ? AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7
            : AndroidArchitecture.ARM64;
        PlayerSettings.Android.minifyRelease = panel.AndroidMinifyRelease;
        PlayerSettings.Android.minifyDebug = false;
        PlayerSettings.Android.useAPKExpansionFiles = false;
        PlayerSettings.Android.optimizedFramePacing = panel.AndroidOptimizedFramePacing;
        EditorUserBuildSettings.androidCreateSymbols = panel.AndroidCreateSymbols
            ? AndroidCreateSymbols.Public
            : AndroidCreateSymbols.Disabled;
        ApplyIl2CppSettings(BuildTargetGroup.Android, panel);
    }

    private static void ApplyIosSettings(NocturnalBuildPanel panel)
    {
        PlayerSettings.iOS.buildNumber = panel.IosBuildNumber.ToString();
        PlayerSettings.iOS.targetOSVersionString = panel.IosMinimumVersion;
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetDevice = panel.IosSupportsIPad
            ? iOSTargetDevice.iPhoneAndiPad
            : iOSTargetDevice.iPhoneOnly;

        if (panel.IosAppleDeveloperTeamId.Length > 0)
        {
            PlayerSettings.iOS.appleDeveloperTeamID = panel.IosAppleDeveloperTeamId;
            PlayerSettings.iOS.appleEnableAutomaticSigning = panel.IosAutomaticSigning;
        }

        ApplyIl2CppSettings(BuildTargetGroup.iOS, panel);
    }

    private static void ApplyStandaloneSettings(NocturnalBuildPanel panel)
    {
        PlayerSettings.SetManagedStrippingLevel(
            NamedBuildTarget.Standalone,
            ResolveStrippingLevel(panel.ManagedStrippingLevel));
    }

    private static void ApplyIl2CppSettings(BuildTargetGroup group, NocturnalBuildPanel panel)
    {
        NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
        PlayerSettings.SetScriptingBackend(namedTarget, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(
            namedTarget,
            ResolveStrippingLevel(panel.ManagedStrippingLevel));
        PlayerSettings.SetIl2CppCodeGeneration(
            namedTarget,
            panel.Il2CppCodeGeneration == NocturnalIl2CppCodeGeneration.FasterSmallerBuilds
                ? Il2CppCodeGeneration.OptimizeSize
                : Il2CppCodeGeneration.OptimizeSpeed);
        PlayerSettings.SetIl2CppCompilerConfiguration(
            namedTarget,
            panel.Il2CppCompilerConfiguration == NocturnalIl2CppCompilerConfiguration.Master
                ? Il2CppCompilerConfiguration.Master
                : Il2CppCompilerConfiguration.Release);
    }

    private static ManagedStrippingLevel ResolveStrippingLevel(
        NocturnalManagedStrippingLevel level)
    {
        if (level == NocturnalManagedStrippingLevel.Minimal)
            return ManagedStrippingLevel.Minimal;
        if (level == NocturnalManagedStrippingLevel.Medium)
            return ManagedStrippingLevel.Medium;
        if (level == NocturnalManagedStrippingLevel.High)
            return ManagedStrippingLevel.High;
        return ManagedStrippingLevel.Low;
    }

    private static bool SwitchTarget(BuildTargetGroup group, BuildTarget target)
    {
        if (EditorUserBuildSettings.activeBuildTarget == target)
            return true;

        bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
        if (!ok)
        {
            EditorUtility.DisplayDialog(
                "Nocturnal Build",
                "Не удалось переключить платформу на " + target,
                "OK");
        }
        return ok;
    }

    private static BuildOptions ResolveBuildOptions(
        NocturnalBuildPanel panel,
        BuildTarget target,
        bool forceAutoRun)
    {
        BuildOptions options = panel.DevelopmentBuild
            ? BuildOptions.Development
            : BuildOptions.CompressWithLz4HC;
        if (panel.StrictMode)
            options |= BuildOptions.StrictMode;

        bool targetCanAutoRun = target != BuildTarget.iOS &&
            !(target == BuildTarget.Android && EditorUserBuildSettings.buildAppBundle);
        bool autoRun = forceAutoRun ||
            (panel.AutoRunAfterBuild && targetCanAutoRun);
        if (autoRun)
            options |= BuildOptions.AutoRunPlayer;
        if (panel.DevelopmentBuild)
            options |= BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler;
        return options;
    }

    private static string[] EnabledScenes()
    {
        return Array.ConvertAll(
            Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled),
            scene => scene.path);
    }

    private static BuildTarget ResolveDesktopTarget(NocturnalDesktopBuildTarget target)
    {
        if (target == NocturnalDesktopBuildTarget.Linux64)
            return BuildTarget.StandaloneLinux64;
        return target == NocturnalDesktopBuildTarget.MacOS
            ? BuildTarget.StandaloneOSX
            : BuildTarget.StandaloneWindows64;
    }

    private static string FormatSize(ulong bytes)
    {
        return bytes < 1024 * 1024
            ? bytes + " байт"
            : (bytes / 1024f / 1024f).ToString("0.0") + " MB";
    }

    private static bool Fail(string message)
    {
        Debug.LogError("[Nocturnal Build] " + message);
        EditorUtility.DisplayDialog("Nocturnal Build", message, "OK");
        return false;
    }
}
#endif
