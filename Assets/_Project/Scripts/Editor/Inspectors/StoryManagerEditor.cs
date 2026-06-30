using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StoryManager))]
public sealed class StoryManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        DrawDialoguePagingRuntimeInfo();
    }

    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    private void DrawDialoguePagingRuntimeInfo()
    {
        var manager = target as StoryManager;
        if (manager == null)
            return;

        SerializedProperty limitProperty = serializedObject.FindProperty("maxDialogueCharsPerTap");
        int limit = limitProperty != null ? limitProperty.intValue : 0;
        int pageCount = manager.CurrentDialoguePageCount;
        int pageNumber = pageCount > 0 ? manager.CurrentDialoguePageIndex + 1 : 0;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dialogue Text Paging Runtime", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("Current Line Chars Total", manager.CurrentDialogueLineVisibleCharCount);
            EditorGUILayout.IntField("Current Page Chars", manager.CurrentDialoguePageVisibleCharCount);
            EditorGUILayout.TextField("Current Page", pageCount > 0 ? $"{pageNumber} / {pageCount}" : "None");
            EditorGUILayout.TextField("Page Chars / Limit", limit > 0 ? $"{manager.CurrentDialoguePageVisibleCharCount} / {limit}" : "No limit");
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Runtime counters update while the game is playing.", MessageType.Info);
    }
}
