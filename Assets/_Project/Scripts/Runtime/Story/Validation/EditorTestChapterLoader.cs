using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class EditorTestChapterLoader
{
    public const string EnabledPrefsKey = "VN.EditorTestChapter.LoadInEditor";
    public const string RootFolder = "Assets/_MyProject/Data/Stories/__EditorTest";
    public const string StoryAssetPath = RootFolder + "/Editor_Test_Story.asset";
    public const string ChapterAssetPath = RootFolder + "/Chapters/editor_test_all_nodes.asset";
    public const string GraphAssetPath = RootFolder + "/Graphs/Editor_Test_All_Nodes.asset";

#if UNITY_EDITOR
    public static bool IsEnabled
    {
        get => EditorPrefs.GetBool(EnabledPrefsKey, false);
        set => EditorPrefs.SetBool(EnabledPrefsKey, value);
    }

    public static StoryData ResolveStory(StoryData requestedStory)
    {
        if (!IsEnabled || Application.isBatchMode)
            return requestedStory;

        var testStory = AssetDatabase.LoadAssetAtPath<StoryData>(StoryAssetPath);
        if (testStory != null)
            return testStory;

        Debug.LogWarning(
            "[EditorTestChapter] Тестовая история включена, но ассеты не найдены. " +
            "Откройте VN/Тестовая глава и создайте её.");
        return requestedStory;
    }
#else
    public static bool IsEnabled => false;

    public static StoryData ResolveStory(StoryData requestedStory)
    {
        return requestedStory;
    }
#endif
}
