#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static partial class RevengeSicilianInkIntegration
{
    const string StoryId = "revenge_sicilian_style";
    const string StoryName = "Месть по-сицилийски";
    const string StoryFolder = "Assets/_MyProject/Data/Stories/revenge_sicilian_style";
    const string InkFolder = StoryFolder + "/Ink";
    const string MenuGameDataPath = StoryFolder + "/Menu/revenge_sicilian_style.asset";
    const string MasterFile = "MPS_Master.ink";

    static readonly EpisodeInfo[] Episodes =
    {
        new EpisodeInfo("MPSs01e01.ink", "mps_s01e01", "Месть по-сицилийски. Серия 1", "MPSs01e01", "MPSs01e02"),
        new EpisodeInfo("MPSs01e02.ink", "mps_s01e02", "Месть по-сицилийски. Серия 2", "MPSs01e02", "MPSs01e03"),
        new EpisodeInfo("MPSs01e03.ink", "mps_s01e03", "Месть по-сицилийски. Серия 3", "MPSs01e03", "END")
    };

    [MenuItem("VN/Ink/Интегрировать Месть по-сицилийски", priority = 30)]
    public static void Integrate()
    {
        EnsureStoryFolders();

        string sourceMaster = FindSourceMaster();
        string sourceFolder = !string.IsNullOrEmpty(sourceMaster) ? Path.GetDirectoryName(sourceMaster) : "";
        List<string> missing = CopySourceFiles(sourceMaster, sourceFolder);
        DeleteLegacyWrappers();
        AssetDatabase.Refresh();

        if (missing.Count > 0)
        {
            ShowMissingFiles(missing);
            return;
        }

        ConfigureInkImporters();
        if (!TryCompileEpisodes(
                out List<CompiledEpisode> compiled,
                out AuthorInkSharedContext shared,
                out string compileError))
        {
            Fail(compileError);
            return;
        }

        var integrationReport = new List<string>();
        StoryData story = EnsureRootStoryData(integrationReport);
        if (!TryPrepareStoryAssets(shared, compiled, integrationReport, out string assetError))
        {
            Fail(assetError);
            return;
        }

        if (!TryWriteAndImportEpisodes(compiled, integrationReport, out string importError))
        {
            Fail(importError);
            return;
        }

        story = FindStoryData();
        LinkMenuGameData(story, shared, integrationReport);
        WriteIntegrationReport(shared, integrationReport, story);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AuthorInk] Месть по-сицилийски интегрирована.\n\n" + string.Join("\n", integrationReport));
    }

    static bool TryCompileEpisodes(
        out List<CompiledEpisode> compiled,
        out AuthorInkSharedContext shared,
        out string error)
    {
        compiled = new List<CompiledEpisode>();
        error = "";

        var sources = new List<string>();
        for (int i = 0; i < Episodes.Length; i++)
            sources.Add(File.ReadAllText(Path.Combine(InkFolder, Episodes[i].SourceFile), Encoding.UTF8));

        shared = AuthorInkStoryJsonCompiler.AnalyzeSources(sources);
        for (int i = 0; i < Episodes.Length; i++)
        {
            EpisodeInfo episode = Episodes[i];
            var options = new AuthorInkCompileOptions
            {
                StoryId = StoryId,
                EpisodeId = episode.EpisodeId,
                Title = episode.Title,
                DefaultName = "Элементина",
                EpisodeKnot = episode.Knot,
                NextEpisodeKnot = episode.NextKnot,
                HeroCharacterId = "hero"
            };

            if (!AuthorInkStoryJsonCompiler.TryCompile(
                    sources[i], options, shared, out string json, out AuthorInkImportReport report, out string compileError))
            {
                error = episode.SourceFile + ": " + compileError;
                return false;
            }

            if (!StoryJsonConverter.TryParseDocument(json, out StoryJsonDocument document, out string parseReason))
            {
                error = episode.SourceFile + ": созданный JSON не прошёл собственный parser.\n" + parseReason;
                return false;
            }

            compiled.Add(new CompiledEpisode(episode, json, document, report));
        }

        return true;
    }

    static bool TryWriteAndImportEpisodes(
        List<CompiledEpisode> compiled,
        List<string> integrationReport,
        out string error)
    {
        error = "";
        for (int i = 0; i < compiled.Count; i++)
        {
            CompiledEpisode item = compiled[i];
            string jsonPath = StoryFolder + "/" + item.Episode.EpisodeId + ".json";
            File.WriteAllText(jsonPath, item.Json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceSynchronousImport);

            if (!StoryJsonAutoImporter.TryAutoImport(jsonPath, out string importMessage))
            {
                error = item.Episode.SourceFile + ": JSON создан, но StoryJsonAutoImporter отказался импортировать его.\n" + importMessage;
                return false;
            }

            integrationReport.Add(
                "[EPISODE] " + item.Episode.EpisodeId +
                " — " + item.Report.OutputNodes + " nodes, " +
                item.Report.DialogueLines + " lines, " +
                item.Report.Warnings.Count + " warnings.");
        }

        return true;
    }

    static void Fail(string error)
    {
        EditorUtility.DisplayDialog("Ink интеграция", error, "OK");
        Debug.LogError("[AuthorInk] " + error);
    }

    static StoryData FindStoryData()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:StoryData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StoryData story = AssetDatabase.LoadAssetAtPath<StoryData>(path);
            if (story != null && string.Equals(story.storyId, StoryId, StringComparison.OrdinalIgnoreCase))
                return story;
        }
        return null;
    }

    sealed class CompiledEpisode
    {
        public readonly EpisodeInfo Episode;
        public readonly string Json;
        public readonly StoryJsonDocument Document;
        public readonly AuthorInkImportReport Report;

        public CompiledEpisode(EpisodeInfo episode, string json, StoryJsonDocument document, AuthorInkImportReport report)
        {
            Episode = episode;
            Json = json;
            Document = document;
            Report = report;
        }
    }

    readonly struct EpisodeInfo
    {
        public readonly string SourceFile;
        public readonly string EpisodeId;
        public readonly string Title;
        public readonly string Knot;
        public readonly string NextKnot;

        public EpisodeInfo(string sourceFile, string episodeId, string title, string knot, string nextKnot)
        {
            SourceFile = sourceFile;
            EpisodeId = episodeId;
            Title = title;
            Knot = knot;
            NextKnot = nextKnot;
        }
    }
}
#endif
