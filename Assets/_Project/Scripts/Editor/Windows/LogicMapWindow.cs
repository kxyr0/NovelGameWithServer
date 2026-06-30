#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using XNode;

public class LogicMapWindow : EditorWindow
{
    StoryGraph graph;

    [MenuItem("VN/Logic Map")]
    static void Open()
    {
        GetWindow<LogicMapWindow>();
    }

    void OnGUI()
    {
        graph = EditorGUILayout.ObjectField("Graph", graph, typeof(StoryGraph), false) as StoryGraph;

        if (graph == null) return;

        foreach (var node in graph.nodes)
        {
            GUILayout.Label(node.name);
        }
    }
}
#endif