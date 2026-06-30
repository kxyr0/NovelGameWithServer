#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using XNodeEditor;

public sealed class EditorTestChapterWindow : EditorWindow
{
    const string WindowTitle = "Тестовая глава";

    StoryData _story;
    ChapterData _chapter;
    StoryGraph _graph;
    Vector2 _scroll;
    string _status;
    bool _statusIsError;

    [MenuItem("VN/Тестовая глава")]
    public static void Open()
    {
        var window = GetWindow<EditorTestChapterWindow>(WindowTitle);
        window.minSize = new Vector2(520f, 360f);
        window.RefreshAssetReferences();
        window.Show();
    }

    void OnEnable()
    {
        RefreshAssetReferences();
    }

    void OnGUI()
    {
        DrawHeader();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "Настройка работает только в редакторе. Если она включена, запуск любой истории из меню загрузит созданную тестовую главу со всеми типами узлов. В сборках игрока настройка игнорируется.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        bool enabled = EditorGUILayout.Toggle("Загружать тестовую главу в редакторе", EditorTestChapterLoader.IsEnabled);
        if (EditorGUI.EndChangeCheck())
        {
            EditorTestChapterLoader.IsEnabled = enabled;
            if (enabled)
                CreateOrRefresh();
            else
                SetStatus("Загрузка тестовой главы отключена.");
        }

        EditorGUILayout.Space(8f);
        DrawAssetPreview();
        EditorGUILayout.Space(10f);
        DrawActions();

        EditorGUILayout.EndScrollView();
        DrawStatus();
    }

    void DrawHeader()
    {
        EditorGUILayout.Space(8f);
        var titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField(WindowTitle, titleStyle, GUILayout.Height(28f));
        EditorGUILayout.Space(4f);
    }

    void DrawAssetPreview()
    {
        EditorGUILayout.LabelField("Созданные ассеты", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("StoryData", _story, typeof(StoryData), false);
        EditorGUILayout.ObjectField("ChapterData", _chapter, typeof(ChapterData), false);
        EditorGUILayout.ObjectField("StoryGraph", _graph, typeof(StoryGraph), false);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.LabelField("Папка", EditorTestChapterLoader.RootFolder, EditorStyles.miniLabel);
    }

    void DrawActions()
    {
        if (GUILayout.Button("Создать / обновить тестовую главу", GUILayout.Height(34f)))
            CreateOrRefresh();

        EditorGUILayout.BeginHorizontal();

        GUI.enabled = _graph != null;
        if (GUILayout.Button("Открыть граф", GUILayout.Height(28f)))
            NodeEditorWindow.Open(_graph);

        GUI.enabled = _story != null;
        if (GUILayout.Button("Выбрать историю", GUILayout.Height(28f)))
        {
            Selection.activeObject = _story;
            EditorGUIUtility.PingObject(_story);
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    void DrawStatus()
    {
        if (string.IsNullOrEmpty(_status))
            return;

        var style = new GUIStyle(EditorStyles.helpBox);
        style.normal.textColor = _statusIsError ? new Color(1f, 0.35f, 0.35f) : new Color(0.45f, 0.95f, 0.55f);
        EditorGUILayout.LabelField(_status, style);
    }

    void CreateOrRefresh()
    {
        try
        {
            _story = EditorTestChapterBuilder.EnsureTestStory();
            RefreshAssetReferences();
            SetStatus("Тестовая глава готова. Типов узлов: " + EditorTestChapterBuilder.AllNodeTypeCount + ".");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SetStatus("Ошибка: " + exception.Message, true);
        }
    }

    void RefreshAssetReferences()
    {
        _story = AssetDatabase.LoadAssetAtPath<StoryData>(EditorTestChapterLoader.StoryAssetPath);
        _chapter = AssetDatabase.LoadAssetAtPath<ChapterData>(EditorTestChapterLoader.ChapterAssetPath);
        _graph = AssetDatabase.LoadAssetAtPath<StoryGraph>(EditorTestChapterLoader.GraphAssetPath);
        Repaint();
    }

    void SetStatus(string message, bool isError = false)
    {
        _status = message;
        _statusIsError = isError;
        Repaint();
    }
}
#endif
