#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class NocturnalStoryBuildAudit : IPreprocessBuildWithReport
{
    private const string Prefix = "[MOBILE_STORY_BUILD_AUDIT]";

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report == null ||
            (report.summary.platform != BuildTarget.Android &&
             report.summary.platform != BuildTarget.iOS))
            return;

        Log(report.summary.platform, EnabledScenes());
    }

    [MenuItem("Nocturne/Diagnostics/Log Android Story Build Audit")]
    public static void LogAndroidBuildAuditMenu()
    {
        Log(BuildTarget.Android, EnabledScenes());
    }

    [MenuItem("Nocturne/Diagnostics/Log iOS Story Build Audit")]
    public static void LogIosBuildAuditMenu()
    {
        Log(BuildTarget.iOS, EnabledScenes());
    }

    public static void Log(BuildTarget target, string[] enabledScenes)
    {
        enabledScenes ??= Array.Empty<string>();
        var dependencies = new HashSet<string>(
            AssetDatabase.GetDependencies(enabledScenes, true),
            StringComparer.OrdinalIgnoreCase);

        // Resources registries are included independently from scene dependencies.
        // Fold their referenced assets into the audit so "inPlayerBuild" reflects
        // what the player will actually receive after StoryRuntimeAssetRegistryBuilder runs.
        string[] registryGuids = AssetDatabase.FindAssets(
            "t:StoryRuntimeAssetRegistry",
            new[] { "Assets/Resources/StoryRuntimeRegistry" });
        for (int i = 0; i < registryGuids.Length; i++)
        {
            string registryPath = AssetDatabase.GUIDToAssetPath(registryGuids[i]);
            string[] registryDependencies = AssetDatabase.GetDependencies(registryPath, true);
            for (int dependencyIndex = 0; dependencyIndex < registryDependencies.Length; dependencyIndex++)
                dependencies.Add(registryDependencies[dependencyIndex]);
        }

        string[] catalogGuids = AssetDatabase.FindAssets("t:GameCatalog");
        var report = new StringBuilder(8192);
        report.AppendLine(
            $"{Prefix}[BEGIN] target={target} scenes={enabledScenes.Length} catalogsFound={catalogGuids.Length} " +
            $"runtimeRegistries={registryGuids.Length} dependencies={dependencies.Count}");

        for (int i = 0; i < enabledScenes.Length; i++)
            report.AppendLine($"  [SCENE] {enabledScenes[i]}");

        int runtimeCatalogs = 0;
        int gamesTotal = 0;
        int gamesIncluded = 0;
        int playable = 0;
        int blocked = 0;

        for (int i = 0; i < catalogGuids.Length; i++)
        {
            string catalogPath = AssetDatabase.GUIDToAssetPath(catalogGuids[i]);
            GameCatalog catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(catalogPath);
            if (catalog == null)
                continue;

            bool catalogInBuild = IsIncluded(catalogPath, dependencies);
            report.AppendLine(
                $"  [CATALOG] inPlayerBuild={catalogInBuild} path='{catalogPath}' entries={catalog.Count}");

            if (!catalogInBuild)
                continue;

            runtimeCatalogs++;
            AppendCatalog(
                report,
                catalog,
                dependencies,
                ref gamesTotal,
                ref gamesIncluded,
                ref playable,
                ref blocked);
        }

        if (runtimeCatalogs == 0)
        {
            report.AppendLine(
                "  [ERROR] No GameCatalog is reachable from enabled scenes. " +
                "Android player cannot receive story cards through scene dependencies.");
        }

        report.AppendLine(
            $"{Prefix}[SUMMARY] runtimeCatalogs={runtimeCatalogs} gamesTotal={gamesTotal} " +
            $"gameDataIncluded={gamesIncluded} playable={playable} blocked={blocked}");
        report.AppendLine($"{Prefix}[END]");

        Debug.Log(report.ToString());
    }

    private static void AppendCatalog(
        StringBuilder report,
        GameCatalog catalog,
        HashSet<string> dependencies,
        ref int gamesTotal,
        ref int gamesIncluded,
        ref int playable,
        ref int blocked)
    {
        IReadOnlyList<GameData> games = catalog.Games;
        if (games == null)
            return;

        for (int i = 0; i < games.Count; i++)
        {
            gamesTotal++;
            GameData data = games[i];
            if (data == null)
            {
                blocked++;
                report.AppendLine($"    [GAME {i}] status=BROKEN reason='null catalog entry'");
                continue;
            }

            string gamePath = AssetDatabase.GetAssetPath(data);
            StoryData story = data.Story;
            string storyPath = story != null ? AssetDatabase.GetAssetPath(story) : "";
            bool gameInBuild = IsIncluded(gamePath, dependencies);
            bool storyInBuild = story != null && IsIncluded(storyPath, dependencies);
            string availability = StoryCatalogRuntimeDiagnostics.DescribeAvailability(data);

            if (gameInBuild)
                gamesIncluded++;

            if (data.CanStartStory && gameInBuild && storyInBuild)
                playable++;
            else
                blocked++;

            string status = data.CanStartStory && gameInBuild && storyInBuild
                ? "PLAYABLE"
                : "BLOCKED";

            report.AppendLine(
                $"    [GAME {i}] status={status} gameData='{data.name}' gameName='{data.GameName}' " +
                $"gameDataInPlayerBuild={gameInBuild} story='{(story != null ? story.name : "<null>")}' " +
                $"storyInPlayerBuild={storyInBuild} canStart={data.CanStartStory} " +
                $"forceComingSoon={data.ForceComingSoon} reason='{availability}' gamePath='{gamePath}' storyPath='{storyPath}'");

            if (story == null || story.Chapters == null)
                continue;

            for (int chapterIndex = 0; chapterIndex < story.Chapters.Count; chapterIndex++)
                AppendChapter(report, story.Chapters[chapterIndex], chapterIndex, dependencies);
        }
    }

    private static void AppendChapter(
        StringBuilder report,
        ChapterData chapter,
        int index,
        HashSet<string> dependencies)
    {
        if (chapter == null)
        {
            report.AppendLine($"      [CHAPTER {index}] status=BROKEN reason='null chapter'");
            return;
        }

        string chapterPath = AssetDatabase.GetAssetPath(chapter);
        string jsonPath = chapter.JsonGraph != null ? AssetDatabase.GetAssetPath(chapter.JsonGraph) : "";
        string graphPath = chapter.Graph != null ? AssetDatabase.GetAssetPath(chapter.Graph) : "";
        string libraryPath = chapter.JsonAssetLibrary != null ? AssetDatabase.GetAssetPath(chapter.JsonAssetLibrary) : "";
        bool jsonHasText = chapter.JsonGraph != null && !string.IsNullOrWhiteSpace(chapter.JsonGraph.text);
        bool chapterInBuild = IsIncluded(chapterPath, dependencies);
        bool jsonInBuild = IsIncluded(jsonPath, dependencies);
        bool graphInBuild = IsIncluded(graphPath, dependencies);
        bool hasUsableLocalGraph =
            (jsonHasText && jsonInBuild) ||
            (chapter.Graph != null && graphInBuild);

        report.AppendLine(
            $"      [CHAPTER {index}] status={(hasUsableLocalGraph ? "LOCAL_OK" : "NO_LOCAL_GRAPH_IN_BUILD")} " +
            $"id='{chapter.ChapterId}' chapterInPlayerBuild={chapterInBuild} " +
            $"json={(chapter.JsonGraph != null ? "yes" : "no")} jsonHasText={jsonHasText} jsonInPlayerBuild={jsonInBuild} " +
            $"graph={(chapter.Graph != null ? "yes" : "no")} graphInPlayerBuild={graphInBuild} " +
            $"assetLibrary={(chapter.JsonAssetLibrary != null ? "yes" : "no")} " +
            $"libraryInPlayerBuild={IsIncluded(libraryPath, dependencies)} " +
            $"chapterPath='{chapterPath}' jsonPath='{jsonPath}' graphPath='{graphPath}'");
    }

    private static string[] EnabledScenes()
    {
        return Array.ConvertAll(
            Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled),
            scene => scene.path);
    }

    private static bool IsIncluded(string path, HashSet<string> dependencies)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (dependencies.Contains(path))
            return true;

        return path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
#endif
