#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

public class ChapterSearchWindow : EditorWindow
{
    ChapterData chapter;
    string query;

    [MenuItem("VN/Search In Chapter")]
    static void Open()
    {
        GetWindow<ChapterSearchWindow>();
    }

    void OnGUI()
    {
        chapter = EditorGUILayout.ObjectField("Chapter", chapter, typeof(ChapterData), false) as ChapterData;
        query = EditorGUILayout.TextField("Search", query);

        if (chapter == null || chapter.graph == null || string.IsNullOrEmpty(query)) return;

        foreach (var node in chapter.graph.nodes)
        {
            if (node is DialogueNode dn)
            {
                if (dn.lines.Any(l => l.richText.Contains(query)))
                    GUILayout.Label("Found in: " + dn.name);
            }
        }
    }
}
#endif