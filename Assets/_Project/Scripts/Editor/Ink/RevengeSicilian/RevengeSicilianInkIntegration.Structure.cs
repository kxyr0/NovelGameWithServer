#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class RevengeSicilianInkIntegration
{
    const string CharactersFolder = StoryFolder + "/Characters";
    const string StatsFolder = StoryFolder + "/Stats";
    const string WardrobeFolder = StoryFolder + "/WardrobeItems";
    const string BackgroundsFolder = StoryFolder + "/Backgrounds";
    const string AudioFolder = StoryFolder + "/Audio";
    const string CutscenesFolder = StoryFolder + "/Cutscenes";
    const string UiFolder = StoryFolder + "/UI";
    const string BindingsFolder = StoryFolder + "/Bindings";
    const string RootStoryPath = StoryFolder + "/revenge_sicilian_style_Story.asset";
    const string AssetLibraryPath = StoryFolder + "/revenge_sicilian_style_JsonAssetLibrary.asset";
    const string BindingReportPath = BindingsFolder + "/MPS_ImportReport.txt";

    static void EnsureStoryFolders()
    {
        EnsureFolder(StoryFolder);
        EnsureFolder(InkFolder);
        EnsureFolder(CharactersFolder);
        EnsureFolder(StatsFolder);
        EnsureFolder(WardrobeFolder);
        EnsureFolder(BackgroundsFolder);
        EnsureFolder(AudioFolder);
        EnsureFolder(CutscenesFolder);
        EnsureFolder(UiFolder);
        EnsureFolder(BindingsFolder);
        EnsureFolder(StoryFolder + "/Menu");
    }

    static StoryData EnsureRootStoryData(List<string> report)
    {
        StoryData root = AssetDatabase.LoadAssetAtPath<StoryData>(RootStoryPath);
        if (root == null)
        {
            StoryData existing = FindStoryData();
            if (existing != null)
            {
                string existingPath = AssetDatabase.GetAssetPath(existing);
                if (!string.Equals(existingPath, RootStoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    string moveError = AssetDatabase.MoveAsset(existingPath, RootStoryPath);
                    if (string.IsNullOrEmpty(moveError))
                    {
                        root = AssetDatabase.LoadAssetAtPath<StoryData>(RootStoryPath);
                        report.Add("[STRUCTURE] StoryData перенесён из generated-папки в корень: " + RootStoryPath);
                    }
                    else
                    {
                        root = existing;
                        report.Add("[WARN] StoryData не удалось перенести в корень: " + moveError);
                    }
                }
                else
                {
                    root = existing;
                }
            }
        }

        if (root == null)
        {
            root = ScriptableObject.CreateInstance<StoryData>();
            root.Configure(StoryId, StoryName, Array.Empty<ChapterData>());
            AssetDatabase.CreateAsset(root, RootStoryPath);
            report.Add("[STRUCTURE] Создан StoryData: " + RootStoryPath);
        }

        if (root != null)
        {
            root.Configure(StoryId, StoryName, root.Chapters);
            EditorUtility.SetDirty(root);
        }

        return root;
    }
}
#endif
