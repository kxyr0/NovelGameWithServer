#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;

public class StoryCreationWizardWindow : EditorWindow
{
    enum Mode
    {
        NewStory,
        AddChapter,
        JsonTools
    }

    const string DefaultRootFolder = "Assets/_MyProject/Data/Stories";
    const string DefaultGameCatalogPath = "Assets/_MyProject/Data/Games/Game Catalog.asset";
    static readonly string[] StandardStoryFolders =
    {
        "Chapters",
        "Graphs",
        "Characters",
        "Backgrounds",
        "Cutscenes",
        "Audio",
        "UI",
        "Json",
        "Menu"
    };

    Mode _mode = Mode.NewStory;
    Vector2 _scroll;

    string _rootFolder = DefaultRootFolder;
    string _storyName = "New Story";
    string _storyId = "new_story";
    string _chapterName = "Chapter 1";
    string _chapterId = "chapter_1";
    string _episodeId = "ep_s1e1";
    string _graphName = "New Story S1E1";

    StoryData _targetStory;
    bool _createGameData = true;
    bool _createAssetLibrary = true;
    bool _registerGameDataInCatalog = true;
    GameCatalog _gameCatalog;
    bool _createStarterChoice;
    bool _openGraph = true;
    bool _openTextWorkspace = true;

    Sprite _coverSprite;
    UnityEngine.Video.VideoClip _coverVideo;
    TextAsset _coverGif;

    TextAsset _chapterJsonAsset;
    StoryJsonAssetLibrary _jsonAssetLibrary;
    StoryGraph _graphForExport;
    bool _assignJsonAssetToChapter = true;
    string _jsonTemplateStoryId = "new_story";
    string _jsonTemplateChapterId = "chapter_1";
    string _jsonTemplateEpisodeId = "chapter_1";
    string _jsonTemplateTitle = "ГЛАВА 1: НАЧАЛО";

    string _status;
    bool _statusIsError;

    [MenuItem("VN/Story Creation Wizard")]
    public static void Open()
    {
        var window = GetWindow<StoryCreationWizardWindow>("Story Wizard");
        window.minSize = new Vector2(560f, 680f);
        window.Show();
    }

    void OnEnable()
    {
        _rootFolder = EditorPrefs.GetString("VN_STORY_WIZARD_ROOT", DefaultRootFolder);
        _gameCatalog = LoadDefaultGameCatalog();
        if (string.IsNullOrWhiteSpace(_jsonTemplateTitle))
            _jsonTemplateTitle = "ГЛАВА 1: НАЧАЛО";
    }

    void OnGUI()
    {
        DrawHeader();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawMode();
        EditorGUILayout.Space(8f);

        DrawCommonFields();
        EditorGUILayout.Space(8f);

        if (_mode == Mode.JsonTools)
        {
            DrawJsonTools();
            EditorGUILayout.EndScrollView();
            DrawStatus();
            return;
        }

        if (_mode == Mode.NewStory)
            DrawNewStoryFields();
        else
            DrawAddChapterFields();

        EditorGUILayout.Space(8f);
        DrawStarterOptions();
        EditorGUILayout.Space(8f);
        DrawPreview();
        EditorGUILayout.Space(12f);
        DrawCreateButton();

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
        EditorGUILayout.LabelField("Story Creation Wizard", titleStyle, GUILayout.Height(28f));
        EditorGUILayout.Space(4f);
    }

    void DrawMode()
    {
        EditorGUI.BeginChangeCheck();
        _mode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "New story pack", "Add chapter", "JSON tools" });
        if (EditorGUI.EndChangeCheck())
            SetStatus("");
    }

    void DrawCommonFields()
    {
        EditorGUILayout.LabelField("Location", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _rootFolder = EditorGUILayout.TextField("Root folder", _rootFolder);
        if (GUILayout.Button("Pick", GUILayout.Width(58f)))
            PickRootFolder();
        EditorGUILayout.EndHorizontal();

        EditorPrefs.SetString("VN_STORY_WIZARD_ROOT", _rootFolder);
    }

    void DrawNewStoryFields()
    {
        EditorGUILayout.LabelField("Story", EditorStyles.boldLabel);
        DrawAutoIdTextField("Story name", ref _storyName, "Story ID", ref _storyId, "story");

        EditorGUILayout.Space(4f);
        DrawChapterFields();

        EditorGUILayout.Space(8f);
        _createAssetLibrary = EditorGUILayout.Toggle("Create asset library", _createAssetLibrary);
        _createGameData = EditorGUILayout.Toggle("Create GameData", _createGameData);

        if (_createGameData)
        {
            _coverSprite = (Sprite)EditorGUILayout.ObjectField("Cover sprite", _coverSprite, typeof(Sprite), false);
            _coverVideo = (UnityEngine.Video.VideoClip)EditorGUILayout.ObjectField("Cover video", _coverVideo, typeof(UnityEngine.Video.VideoClip), false);
            _coverGif = (TextAsset)EditorGUILayout.ObjectField("Cover GIF", _coverGif, typeof(TextAsset), false);
            _registerGameDataInCatalog = EditorGUILayout.Toggle("Add to Game Catalog", _registerGameDataInCatalog);
            if (_registerGameDataInCatalog)
                _gameCatalog = (GameCatalog)EditorGUILayout.ObjectField("Game Catalog", _gameCatalog, typeof(GameCatalog), false);
        }
    }

    void DrawAddChapterFields()
    {
        EditorGUILayout.LabelField("Existing story", EditorStyles.boldLabel);
        _targetStory = (StoryData)EditorGUILayout.ObjectField("StoryData", _targetStory, typeof(StoryData), false);

        EditorGUILayout.Space(4f);
        DrawChapterFields();
    }

    void DrawChapterFields()
    {
        DrawAutoIdTextField("Chapter name", ref _chapterName, "Chapter ID", ref _chapterId, "chapter");

        EditorGUI.BeginChangeCheck();
        _episodeId = EditorGUILayout.TextField("Episode ID", _episodeId);
        _graphName = EditorGUILayout.TextField("Graph name", _graphName);
        if (EditorGUI.EndChangeCheck())
            SetStatus("");
    }

    void DrawAutoIdTextField(string nameLabel, ref string name, string idLabel, ref string id, string prefix)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        name = EditorGUILayout.TextField(nameLabel, name);
        if (GUILayout.Button("ID", GUILayout.Width(34f)))
            id = MakeId(prefix, name);
        if (EditorGUI.EndChangeCheck() && string.IsNullOrWhiteSpace(id))
            id = MakeId(prefix, name);
        EditorGUILayout.EndHorizontal();

        id = EditorGUILayout.TextField(idLabel, id);
    }

    void DrawStarterOptions()
    {
        EditorGUILayout.LabelField("Starter graph", EditorStyles.boldLabel);
        _createStarterChoice = EditorGUILayout.Toggle("Add sample ChoiceNode", _createStarterChoice);
        _openGraph = EditorGUILayout.Toggle("Open xNode after create", _openGraph);
        _openTextWorkspace = EditorGUILayout.Toggle("Open text workspace", _openTextWorkspace);
    }

    void DrawPreview()
    {
        EditorGUILayout.LabelField("Will create", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (_mode == Mode.NewStory)
        {
            string storyFolder = GetStoryFolder();
            EditorGUILayout.LabelField("Folder: " + storyFolder);
            EditorGUILayout.LabelField("StoryData: " + SafeFileName(_storyId) + "_Story.asset");
            EditorGUILayout.LabelField("ChapterData: " + SafeFileName(_chapterId) + ".asset");
            EditorGUILayout.LabelField("StoryGraph: " + SafeFileName(_graphName) + ".asset");
            EditorGUILayout.LabelField("Folders: " + string.Join(", ", StandardStoryFolders));
            if (_createAssetLibrary)
                EditorGUILayout.LabelField("Asset Library: " + SafeFileName(_storyId) + "_JsonAssetLibrary.asset");
            if (_createGameData)
                EditorGUILayout.LabelField("GameData: " + SafeFileName(_storyId) + "_GameData.asset");
            if (_createGameData && _registerGameDataInCatalog)
                EditorGUILayout.LabelField("Catalog: " + (_gameCatalog != null ? _gameCatalog.name : "<not selected>"));
        }
        else
        {
            EditorGUILayout.LabelField("StoryData: " + (_targetStory != null ? _targetStory.name : "<not selected>"));
            EditorGUILayout.LabelField("ChapterData: " + SafeFileName(_chapterId) + ".asset");
            EditorGUILayout.LabelField("StoryGraph: " + SafeFileName(_graphName) + ".asset");
        }

        EditorGUILayout.LabelField("Graph nodes: Start -> Scene -> Dialogue" + (_createStarterChoice ? " -> Choice" : ""));
        EditorGUILayout.EndVertical();
    }

    void DrawCreateButton()
    {
        GUI.enabled = CanCreate(out string reason);

        if (GUILayout.Button(_mode == Mode.NewStory ? "Create story pack" : "Add chapter", GUILayout.Height(42f)))
            Create();

        GUI.enabled = true;

        if (!string.IsNullOrEmpty(reason))
            EditorGUILayout.HelpBox(reason, MessageType.Warning);
    }

    void DrawJsonTools()
    {
        EditorGUILayout.LabelField("JSON import", EditorStyles.boldLabel);
        _targetStory = (StoryData)EditorGUILayout.ObjectField("Target StoryData", _targetStory, typeof(StoryData), false);
        _chapterJsonAsset = (TextAsset)EditorGUILayout.ObjectField("Chapter JSON", _chapterJsonAsset, typeof(TextAsset), false);
        _jsonAssetLibrary = (StoryJsonAssetLibrary)EditorGUILayout.ObjectField("Asset Library", _jsonAssetLibrary, typeof(StoryJsonAssetLibrary), false);
        _assignJsonAssetToChapter = EditorGUILayout.Toggle("Assign JSON to ChapterData", _assignJsonAssetToChapter);

        EditorGUILayout.HelpBox(
            "Import Chapter JSON creates a StoryGraph and ChapterData. If Target StoryData is selected, the chapter is added to StoryData.chapters.",
            MessageType.Info);

        GUI.enabled = _chapterJsonAsset != null;
        if (GUILayout.Button("Validate Chapter JSON", GUILayout.Height(30f)))
            ValidateChapterJson();
        if (GUILayout.Button("Import Chapter JSON", GUILayout.Height(36f)))
            ImportChapterJson();
        GUI.enabled = true;

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("JSON template", EditorStyles.boldLabel);
        _jsonTemplateStoryId = EditorGUILayout.TextField("Story ID", _jsonTemplateStoryId);
        _jsonTemplateChapterId = EditorGUILayout.TextField("Chapter ID", _jsonTemplateChapterId);
        _jsonTemplateEpisodeId = EditorGUILayout.TextField("Episode ID", _jsonTemplateEpisodeId);
        _jsonTemplateTitle = EditorGUILayout.TextField("Title", _jsonTemplateTitle);
        if (GUILayout.Button("Create Chapter JSON Template", GUILayout.Height(34f)))
            CreateChapterJsonTemplate();
        if (GUILayout.Button("Create Empty JSON Asset Library", GUILayout.Height(30f)))
            CreateJsonAssetLibrary();

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("JSON export", EditorStyles.boldLabel);
        _graphForExport = (StoryGraph)EditorGUILayout.ObjectField("Graph", _graphForExport, typeof(StoryGraph), false);
        EditorGUILayout.HelpBox("Export uses the selected StoryGraph if this field is empty.", MessageType.Info);

        GUI.enabled = _graphForExport != null || Selection.activeObject is StoryGraph;
        if (GUILayout.Button("Export Selected Graph JSON", GUILayout.Height(36f)))
            ExportSelectedGraphJson();
        GUI.enabled = true;
    }

    void DrawStatus()
    {
        if (string.IsNullOrEmpty(_status))
            return;

        var style = new GUIStyle(EditorStyles.helpBox);
        style.normal.textColor = _statusIsError ? new Color(1f, 0.35f, 0.35f) : new Color(0.45f, 0.95f, 0.55f);
        EditorGUILayout.LabelField(_status, style);
    }

    bool CanCreate(out string reason)
    {
        reason = "";

        if (string.IsNullOrWhiteSpace(_rootFolder))
        {
            reason = "Root folder is empty.";
            return false;
        }

        if (_mode == Mode.NewStory && string.IsNullOrWhiteSpace(_storyName))
        {
            reason = "Story name is empty.";
            return false;
        }

        if (_mode == Mode.NewStory)
        {
            string candidateStoryId = string.IsNullOrWhiteSpace(_storyId) ? MakeId("story", _storyName) : _storyId.Trim();
            if (FindStoryDataById(candidateStoryId) != null)
            {
                reason = "Story ID already exists. Use a unique Story ID for a separate story.";
                return false;
            }
        }

        if (_mode == Mode.NewStory && _createGameData && _registerGameDataInCatalog && _gameCatalog == null)
        {
            reason = "Select Game Catalog or disable Add to Game Catalog.";
            return false;
        }

        if (_mode == Mode.AddChapter)
        {
            if (_targetStory == null)
            {
                reason = "Select StoryData.";
                return false;
            }

            string candidateChapterId = string.IsNullOrWhiteSpace(_chapterId) ? MakeId("chapter", _chapterName) : _chapterId.Trim();
            if (FindExistingChapter(_targetStory, candidateChapterId) != null)
            {
                reason = "Chapter ID already exists in selected StoryData.";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(_chapterName))
        {
            reason = "Chapter name is empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_graphName))
        {
            reason = "Graph name is empty.";
            return false;
        }

        return true;
    }

    void Create()
    {
        try
        {
            if (_mode == Mode.NewStory)
                CreateNewStoryPack();
            else
                AddChapterToStory();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SetStatus("Failed: " + exception.Message, true);
        }
    }

    void ValidateChapterJson()
    {
        if (_chapterJsonAsset == null)
        {
            SetStatus("Select a JSON TextAsset.", true);
            return;
        }

        var resolver = CreateEditorJsonResolver();
        if (!StoryJsonConverter.TryBuildGraphWithReport(
                _chapterJsonAsset.text,
                _chapterJsonAsset.name,
                out var graph,
                out var report,
                resolver))
        {
            DestroyTransientGraph(graph);
            SetStatus(report.ToDisplayString(), true);
            return;
        }

        DestroyTransientGraph(graph);

        string message = report.HasWarnings
            ? "JSON is valid with warnings:\n" + report.ToDisplayString()
            : "JSON is valid.";
        SetStatus(message, false);
    }

    void CreateChapterJsonTemplate()
    {
        string storyId = string.IsNullOrWhiteSpace(_jsonTemplateStoryId) ? "story_id" : MakeId("story", _jsonTemplateStoryId);
        string chapterId = string.IsNullOrWhiteSpace(_jsonTemplateChapterId) ? "chapter_1" : MakeId("chapter", _jsonTemplateChapterId);
        string episodeId = string.IsNullOrWhiteSpace(_jsonTemplateEpisodeId) ? chapterId : MakeId("episode", _jsonTemplateEpisodeId);
        string title = string.IsNullOrWhiteSpace(_jsonTemplateTitle) ? "ГЛАВА 1: НАЧАЛО" : _jsonTemplateTitle.Trim();

        string path = EditorUtility.SaveFilePanelInProject(
            "Create chapter JSON template",
            SafeFileName(chapterId) + ".json",
            "json",
            "Choose where to save the chapter JSON template",
            _rootFolder);

        if (string.IsNullOrWhiteSpace(path))
            return;

        File.WriteAllText(path, BuildChapterJsonTemplate(storyId, chapterId, episodeId, title), new UTF8Encoding(false));
        AssetDatabase.Refresh();

        _chapterJsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        SetStatus("Created JSON template: " + path);
    }

    void CreateJsonAssetLibrary()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create JSON asset library",
            SafeFileName(FirstNonEmpty(_jsonTemplateStoryId, "story")) + "_JsonAssetLibrary.asset",
            "asset",
            "Choose where to save the JSON asset library",
            _rootFolder);

        if (string.IsNullOrWhiteSpace(path))
            return;

        var library = ScriptableObject.CreateInstance<StoryJsonAssetLibrary>();
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);
        AssetDatabase.CreateAsset(library, uniquePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _jsonAssetLibrary = library;
        Selection.activeObject = library;
        EditorGUIUtility.PingObject(library);
        SetStatus("Created JSON asset library: " + uniquePath);
    }

    void ImportChapterJson()
    {
        if (_chapterJsonAsset == null)
        {
            SetStatus("Select a JSON TextAsset.", true);
            return;
        }

        try
        {
            if (!StoryJsonConverter.TryParseDocument(_chapterJsonAsset.text, out var document, out string parseReason))
            {
                SetStatus(parseReason, true);
                return;
            }

            if (_jsonAssetLibrary == null)
                _jsonAssetLibrary = FindNearestAssetLibrary(GetAssetFolder(AssetDatabase.GetAssetPath(_chapterJsonAsset)));

            var resolver = CreateEditorJsonResolver();
            if (!StoryJsonConverter.TryBuildGraphWithReport(
                    _chapterJsonAsset.text,
                    document.episodeId,
                    out var graph,
                    out var report,
                    resolver))
            {
                SetStatus(report.ToDisplayString(), true);
                return;
            }

            string storyId = FirstNonEmpty(document.storyId, _targetStory != null ? _targetStory.storyId : "", "json_story");
            string chapterId = FirstNonEmpty(document.chapterId, document.episodeId, MakeId("chapter", document.title));
            string chapterTitle = FirstNonEmpty(document.title, chapterId);
            string episodeId = FirstNonEmpty(document.episodeId, chapterId);

            graph.name = FirstNonEmpty(document.title, episodeId);
            graph.episodeId = episodeId;

            string storyFolder = _targetStory != null
                ? GetAssetFolder(_targetStory)
                : EnsureFolder(_rootFolder.TrimEnd('/', '\\') + "/" + SafeFileName(storyId));
            if (string.IsNullOrEmpty(storyFolder))
                storyFolder = EnsureFolder(_rootFolder.TrimEnd('/', '\\') + "/" + SafeFileName(storyId));

            EnsureStandardStoryFolders(storyFolder);
            string graphsFolder = storyFolder + "/Graphs";
            string chaptersFolder = storyFolder + "/Chapters";
            string graphPath = AssetDatabase.GenerateUniqueAssetPath(graphsFolder + "/" + SafeFileName(graph.name) + ".asset");

            AssetDatabase.CreateAsset(graph, graphPath);
            AddGraphSubAssets(graph, graphPath);

            var chapter = FindExistingChapter(_targetStory, chapterId);
            if (chapter == null)
                chapter = AssetDatabase.LoadAssetAtPath<ChapterData>(chaptersFolder + "/" + SafeFileName(chapterId) + ".asset");
            if (chapter == null)
                chapter = CreateAsset<ChapterData>(chaptersFolder + "/" + SafeFileName(chapterId) + ".asset");

            chapter.Configure(
                chapterId,
                chapterTitle,
                graph,
                _assignJsonAssetToChapter ? _chapterJsonAsset : null,
                _jsonAssetLibrary,
                chapter.isPremium,
                chapter.unlockCost);
            EditorUtility.SetDirty(chapter);

            if (_targetStory != null)
                AddChapterToStoryData(_targetStory, chapter);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = graph;
            EditorGUIUtility.PingObject(graph);
            if (_openGraph)
                NodeEditorWindow.Open(graph);

            string suffix = report.HasWarnings ? "\n" + report.ToDisplayString() : "";
            SetStatus("Imported JSON chapter: " + chapterTitle + suffix, report.HasErrors);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SetStatus("Import failed: " + exception.Message, true);
        }
    }

    void ExportSelectedGraphJson()
    {
        var graph = _graphForExport != null ? _graphForExport : Selection.activeObject as StoryGraph;
        if (graph == null)
        {
            SetStatus("Select a StoryGraph to export.", true);
            return;
        }

        var resolver = CreateEditorJsonResolver();
        if (!StoryJsonConverter.TryExportGraph(graph, out string json, out string reason, resolver, true))
        {
            SetStatus(reason, true);
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Export chapter JSON",
            SafeFileName(graph.name) + ".json",
            "json",
            "Choose where to save the exported story JSON",
            _rootFolder);

        if (string.IsNullOrWhiteSpace(path))
            return;

        File.WriteAllText(path, json, new UTF8Encoding(false));
        AssetDatabase.Refresh();
        SetStatus("Exported JSON: " + path);
    }

    StoryJsonAssetResolver CreateEditorJsonResolver()
    {
        var editorResolver = new StoryJsonEditorAssetResolver();
        return _jsonAssetLibrary != null
            ? new StoryJsonAssetLibraryResolver(_jsonAssetLibrary, editorResolver)
            : editorResolver;
    }

    void CreateNewStoryPack()
    {
        NormalizeIds();

        string storyFolder = EnsureFolder(GetStoryFolder());
        EnsureStandardStoryFolders(storyFolder);
        string graphsFolder = storyFolder + "/Graphs";
        string chaptersFolder = storyFolder + "/Chapters";
        string menuFolder = storyFolder + "/Menu";
        StoryJsonAssetLibrary assetLibrary = _createAssetLibrary
            ? CreateOrLoadAssetLibrary(storyFolder, _storyId)
            : null;

        var graph = CreateStarterGraph(graphsFolder, _graphName, _episodeId);

        var chapter = CreateAsset<ChapterData>(chaptersFolder + "/" + SafeFileName(_chapterId) + ".asset");
        chapter.Configure(_chapterId, _chapterName, graph, null, assetLibrary, false, 0);
        EditorUtility.SetDirty(chapter);

        var story = CreateAsset<StoryData>(storyFolder + "/" + SafeFileName(_storyId) + "_Story.asset");
        story.Configure(_storyId, _storyName, new[] { chapter });
        EditorUtility.SetDirty(story);

        if (_createGameData)
        {
            var gameData = CreateAsset<GameData>(menuFolder + "/" + SafeFileName(_storyId) + "_GameData.asset");
            gameData.Configure(_storyName, story, _coverSprite, _coverVideo, _coverGif);
            EditorUtility.SetDirty(gameData);

            if (_registerGameDataInCatalog)
                RegisterGameDataInCatalog(gameData);
        }

        SaveAndOpen(graph, story);
        SetStatus("Created story pack: " + _storyName);
    }

    void AddChapterToStory()
    {
        NormalizeIds();

        string storyFolder = GetAssetFolder(_targetStory);
        if (string.IsNullOrEmpty(storyFolder))
            storyFolder = EnsureFolder(_rootFolder + "/" + SafeFileName(_targetStory.name));

        EnsureStandardStoryFolders(storyFolder);
        string graphsFolder = storyFolder + "/Graphs";
        string chaptersFolder = storyFolder + "/Chapters";
        StoryJsonAssetLibrary assetLibrary = FindNearestAssetLibrary(storyFolder);

        var graph = CreateStarterGraph(graphsFolder, _graphName, _episodeId);

        var chapter = CreateAsset<ChapterData>(chaptersFolder + "/" + SafeFileName(_chapterId) + ".asset");
        chapter.Configure(_chapterId, _chapterName, graph, null, assetLibrary, false, 0);
        EditorUtility.SetDirty(chapter);

        var chapters = _targetStory.chapters != null
            ? new List<ChapterData>(_targetStory.chapters)
            : new List<ChapterData>();
        chapters.Add(chapter);
        _targetStory.Configure(_targetStory.storyId, _targetStory.storyName, chapters);

        EditorUtility.SetDirty(_targetStory);
        SaveAndOpen(graph, _targetStory);
        SetStatus("Added chapter: " + _chapterName);
    }

    StoryGraph CreateStarterGraph(string folder, string graphName, string episodeId)
    {
        string graphPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeFileName(graphName) + ".asset");
        var graph = ScriptableObject.CreateInstance<StoryGraph>();
        graph.name = graphName;
        graph.episodeId = episodeId ?? "";
        AssetDatabase.CreateAsset(graph, graphPath);

        var start = AddNode<StartNode>(graph, graphPath, "Start", new Vector2(0f, 0f));
        var scene = AddNode<SceneSetupNode>(graph, graphPath, "Scene - Opening", new Vector2(320f, 0f));
        var dialogue = AddNode<DialogueNode>(graph, graphPath, "Dialogue - Opening", new Vector2(700f, 0f));

        scene.sceneLabel = "Открывающая сцена";
        dialogue.nodeTitle = "Первая реплика";
        dialogue.lines = new List<DialogueLine>
        {
            new DialogueLine { richText = "Первая реплика новой главы." }
        };

        Connect(start, "exit", scene, "enter");
        Connect(scene, "exit", dialogue, "enter");

        if (_createStarterChoice)
        {
            var choice = AddNode<ChoiceNode>(graph, graphPath, "Choice - First decision", new Vector2(1120f, 0f));
            choice.nodeTitle = "Первый выбор";
            choice.lines = new List<DialogueLine> { new DialogueLine { richText = "Что сделать дальше?" } };
            choice.options = new List<ChoiceOption>
            {
                new ChoiceOption { text = "Продолжить" },
                new ChoiceOption { text = "Осмотреться" }
            };
            choice.choices = new List<BaseStoryNode> { null, null };
            choice.AddDynamicOutput(typeof(BaseStoryNode), Node.ConnectionType.Multiple, Node.TypeConstraint.None, "choices 0");
            choice.AddDynamicOutput(typeof(BaseStoryNode), Node.ConnectionType.Multiple, Node.TypeConstraint.None, "choices 1");
            Connect(dialogue, "exit", choice, "enter");
            EditorUtility.SetDirty(choice);
        }

        EditorUtility.SetDirty(start);
        EditorUtility.SetDirty(scene);
        EditorUtility.SetDirty(dialogue);
        EditorUtility.SetDirty(graph);
        return graph;
    }

    static T AddNode<T>(StoryGraph graph, string assetPath, string name, Vector2 position) where T : BaseStoryNode
    {
        var node = graph.AddNode<T>();
        node.name = name;
        node.position = position;
        node.graph = graph;
        node.guid = Guid.NewGuid().ToString();
        AssetDatabase.AddObjectToAsset(node, assetPath);
        return node;
    }

    static void Connect(BaseStoryNode from, string outputPortName, BaseStoryNode to, string inputPortName)
    {
        var output = from.GetOutputPort(outputPortName);
        var input = to.GetInputPort(inputPortName);
        if (output != null && input != null && !output.IsConnectedTo(input))
            output.Connect(input);
    }

    static void AddGraphSubAssets(StoryGraph graph, string graphPath)
    {
        foreach (var node in graph.nodes.OfType<BaseStoryNode>())
        {
            node.hideFlags = HideFlags.None;
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(node)))
                AssetDatabase.AddObjectToAsset(node, graphPath);

            if (node is SceneSetupNode scene && scene.sceneData != null)
                AddTransientSubAsset(scene.sceneData, graphPath, "SceneData_" + node.guid);

            if (node is DialogueNode dialogue)
                AddDialogueSubAssets(dialogue.activeCharacters, dialogue.lines, graphPath);

            if (node is ChoiceNode choice)
                AddDialogueSubAssets(choice.activeCharacters, choice.lines, graphPath);
        }

        EditorUtility.SetDirty(graph);
    }

    static void AddDialogueSubAssets(
        IEnumerable<DialogueCharacterEntry> activeCharacters,
        IEnumerable<DialogueLine> lines,
        string graphPath)
    {
        if (activeCharacters != null)
        {
            foreach (var entry in activeCharacters)
                AddTransientSubAsset(entry?.character, graphPath, entry?.character != null ? entry.character.name : "");
        }

        if (lines == null)
            return;

        foreach (var line in lines)
            AddTransientSubAsset(line?.speaker, graphPath, line?.speaker != null ? line.speaker.name : "");
    }

    static void AddTransientSubAsset(UnityEngine.Object asset, string graphPath, string fallbackName)
    {
        if (asset == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
            return;

        asset.hideFlags = HideFlags.None;
        if (string.IsNullOrWhiteSpace(asset.name))
            asset.name = fallbackName;

        AssetDatabase.AddObjectToAsset(asset, graphPath);
        EditorUtility.SetDirty(asset);
    }

    static void DestroyTransientGraph(StoryGraph graph)
    {
        if (graph == null)
            return;

        var nodes = graph.nodes != null
            ? graph.nodes.OfType<BaseStoryNode>().ToArray()
            : Array.Empty<BaseStoryNode>();

        foreach (var node in nodes)
        {
            if (node is SceneSetupNode scene && scene.sceneData != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(scene.sceneData)))
                DestroyImmediate(scene.sceneData);

            if (node is DialogueNode dialogue)
                DestroyTransientDialogueAssets(dialogue.activeCharacters, dialogue.lines);

            if (node is ChoiceNode choice)
                DestroyTransientDialogueAssets(choice.activeCharacters, choice.lines);

            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(node)))
                DestroyImmediate(node);
        }

        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(graph)))
            DestroyImmediate(graph);
    }

    static void DestroyTransientDialogueAssets(
        IEnumerable<DialogueCharacterEntry> activeCharacters,
        IEnumerable<DialogueLine> lines)
    {
        if (activeCharacters != null)
        {
            foreach (var entry in activeCharacters)
                DestroyTransientAsset(entry?.character);
        }

        if (lines == null)
            return;

        foreach (var line in lines)
            DestroyTransientAsset(line?.speaker);
    }

    static void DestroyTransientAsset(UnityEngine.Object asset)
    {
        if (asset != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
            DestroyImmediate(asset);
    }

    void SaveAndOpen(StoryGraph graph, StoryData story)
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = graph;
        EditorGUIUtility.PingObject(graph);

        if (_openGraph)
            NodeEditorWindow.Open(graph);
        if (_openTextWorkspace)
            StoryTextWorkspaceWindow.Open(graph);
    }

    static ChapterData FindExistingChapter(StoryData story, string chapterId)
    {
        if (story == null || story.chapters == null || string.IsNullOrWhiteSpace(chapterId))
            return null;

        return story.chapters.FirstOrDefault(chapter =>
            chapter != null &&
            string.Equals(chapter.chapterId, chapterId, StringComparison.OrdinalIgnoreCase));
    }

    static StoryData FindStoryDataById(string storyId)
    {
        if (string.IsNullOrWhiteSpace(storyId))
            return null;

        foreach (string guid in AssetDatabase.FindAssets("t:StoryData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var story = AssetDatabase.LoadAssetAtPath<StoryData>(path);
            if (story != null && string.Equals(story.storyId, storyId, StringComparison.OrdinalIgnoreCase))
                return story;
        }

        return null;
    }

    static void AddChapterToStoryData(StoryData story, ChapterData chapter)
    {
        if (story == null || chapter == null)
            return;

        var chapters = story.chapters != null
            ? new List<ChapterData>(story.chapters)
            : new List<ChapterData>();

        if (!chapters.Contains(chapter))
            chapters.Add(chapter);

        story.Configure(story.storyId, story.storyName, chapters);
        EditorUtility.SetDirty(story);
    }

    void NormalizeIds()
    {
        if (string.IsNullOrWhiteSpace(_storyId))
            _storyId = MakeId("story", _storyName);
        if (string.IsNullOrWhiteSpace(_chapterId))
            _chapterId = MakeId("chapter", _chapterName);
        if (string.IsNullOrWhiteSpace(_episodeId))
            _episodeId = MakeEpisodeId(_chapterId);
        if (string.IsNullOrWhiteSpace(_graphName))
            _graphName = _storyName + " " + _chapterName;
    }

    string GetStoryFolder()
    {
        return _rootFolder.TrimEnd('/', '\\') + "/" + SafeFileName(string.IsNullOrWhiteSpace(_storyId) ? _storyName : _storyId);
    }

    static string GetAssetFolder(UnityEngine.Object asset)
    {
        if (asset == null) return "";
        string path = AssetDatabase.GetAssetPath(asset);
        return string.IsNullOrEmpty(path) ? "" : Path.GetDirectoryName(path)?.Replace("\\", "/");
    }

    static void EnsureStandardStoryFolders(string storyFolder)
    {
        foreach (string folder in StandardStoryFolders)
            EnsureFolder(storyFolder + "/" + folder);
    }

    static StoryJsonAssetLibrary CreateOrLoadAssetLibrary(string storyFolder, string storyId)
    {
        string path = storyFolder + "/" + SafeFileName(storyId) + "_JsonAssetLibrary.asset";
        var library = AssetDatabase.LoadAssetAtPath<StoryJsonAssetLibrary>(path);
        if (library != null)
            return library;

        library = ScriptableObject.CreateInstance<StoryJsonAssetLibrary>();
        AssetDatabase.CreateAsset(library, AssetDatabase.GenerateUniqueAssetPath(path));
        EditorUtility.SetDirty(library);
        return library;
    }

    static StoryJsonAssetLibrary FindNearestAssetLibrary(string storyFolder)
    {
        storyFolder = (storyFolder ?? "").Replace("\\", "/").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(storyFolder))
            return null;

        StoryJsonAssetLibrary best = null;
        int bestScore = -1;

        foreach (string guid in AssetDatabase.FindAssets("t:StoryJsonAssetLibrary"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var library = AssetDatabase.LoadAssetAtPath<StoryJsonAssetLibrary>(path);
            if (library == null)
                continue;

            int score = GetCommonFolderPrefixScore(storyFolder, GetAssetFolder(path));
            if (score > bestScore)
            {
                best = library;
                bestScore = score;
            }
        }

        return bestScore > 1 ? best : null;
    }

    static int GetCommonFolderPrefixScore(string left, string right)
    {
        string[] leftParts = (left ?? "").Split('/');
        string[] rightParts = (right ?? "").Split('/');
        int count = Mathf.Min(leftParts.Length, rightParts.Length);
        int score = 0;

        for (int i = 0; i < count; i++)
        {
            if (!string.Equals(leftParts[i], rightParts[i], StringComparison.OrdinalIgnoreCase))
                break;

            score++;
        }

        return score;
    }

    static string GetAssetFolder(string assetPath)
    {
        string folder = Path.GetDirectoryName(assetPath ?? "");
        return string.IsNullOrWhiteSpace(folder) ? "" : folder.Replace("\\", "/");
    }

    void RegisterGameDataInCatalog(GameData gameData)
    {
        if (_gameCatalog == null || gameData == null)
            return;

        Undo.RecordObject(_gameCatalog, "Register Story GameData");
        if (_gameCatalog.AddGame(gameData))
        {
            EditorUtility.SetDirty(_gameCatalog);
            SetStatus("GameData added to catalog: " + _gameCatalog.name);
        }
    }

    static GameCatalog LoadDefaultGameCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(DefaultGameCatalogPath);
        if (catalog != null)
            return catalog;

        foreach (string guid in AssetDatabase.FindAssets("t:GameCatalog"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(path);
            if (catalog != null)
                return catalog;
        }

        return null;
    }

    static T CreateAsset<T>(string path) where T : ScriptableObject
    {
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(path.Replace("\\", "/"));
        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, uniquePath);
        return asset;
    }

    static string EnsureFolder(string path)
    {
        path = path.Replace("\\", "/").TrimEnd('/');
        if (AssetDatabase.IsValidFolder(path))
            return path;

        string[] parts = path.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
            throw new InvalidOperationException("Folder must be inside Assets: " + path);

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }

        return path;
    }

    void PickRootFolder()
    {
        string absolute = EditorUtility.OpenFolderPanel("Story root folder", Application.dataPath, "");
        if (string.IsNullOrEmpty(absolute))
            return;

        absolute = absolute.Replace("\\", "/");
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName.Replace("\\", "/");
        if (!string.IsNullOrEmpty(projectRoot) && absolute.StartsWith(projectRoot))
        {
            _rootFolder = absolute.Substring(projectRoot.Length).TrimStart('/');
            return;
        }

        SetStatus("Pick a folder inside this Unity project.", true);
    }

    static string MakeEpisodeId(string chapterId)
    {
        string chapter = ExtractLastNumber(chapterId, "1");
        return "ep_" + chapter;
    }

    static string MakeId(string prefix, string value)
    {
        string slug = Slugify(value);
        return string.IsNullOrWhiteSpace(slug) ? prefix + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") : slug;
    }

    static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        value = value.Trim().ToLowerInvariant();
        var chars = new List<char>(value.Length);
        bool lastWasSeparator = false;

        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                chars.Add(c);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                chars.Add('_');
                lastWasSeparator = true;
            }
        }

        return new string(chars.ToArray()).Trim('_');
    }

    static string SafeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            value = "New Asset";

        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value.Trim();
    }

    static string ExtractLastNumber(string value, string fallback)
    {
        if (string.IsNullOrEmpty(value))
            return fallback;

        string digits = "";
        for (int i = value.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(value[i]))
            {
                if (digits.Length > 0)
                    break;
                continue;
            }

            digits = value[i] + digits;
        }

        return string.IsNullOrEmpty(digits) ? fallback : digits;
    }

    static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "";
    }

    static string BuildChapterJsonTemplate(string storyId, string chapterId, string episodeId, string title)
    {
        return
            "{\n" +
            "  \"version\": 1,\n" +
            "  \"storyId\": \"" + EscapeJson(storyId) + "\",\n" +
            "  \"chapterId\": \"" + EscapeJson(chapterId) + "\",\n" +
            "  \"episodeId\": \"" + EscapeJson(episodeId) + "\",\n" +
            "  \"title\": \"" + EscapeJson(title) + "\",\n" +
            "  \"characters\": [\n" +
            "    { \"id\": \"hero\", \"name\": \"Героиня\" },\n" +
            "    { \"id\": \"companion\", \"name\": \"Незнакомец\" }\n" +
            "  ],\n" +
            "  \"nodes\": [\n" +
            "    { \"id\": \"start\", \"type\": \"start\", \"next\": \"scene_opening\" },\n" +
            "    {\n" +
            "      \"id\": \"scene_opening\",\n" +
            "      \"type\": \"scene\",\n" +
            "      \"label\": \"Открывающая сцена\",\n" +
            "      \"background\": \"bg_opening\",\n" +
            "      \"music\": \"music_opening\",\n" +
            "      \"next\": \"dialogue_opening\"\n" +
            "    },\n" +
            "    {\n" +
            "      \"id\": \"dialogue_opening\",\n" +
            "      \"type\": \"dialogue\",\n" +
            "      \"activeCharacters\": [\n" +
            "        { \"character\": \"hero\", \"emotion\": \"Neutral\", \"position\": \"Center\" }\n" +
            "      ],\n" +
            "      \"lines\": [\n" +
            "        { \"speaker\": \"hero\", \"emotion\": \"Neutral\", \"text\": \"Я не помню, как оказалась здесь...\" }\n" +
            "      ],\n" +
            "      \"next\": \"choice_opening\"\n" +
            "    },\n" +
            "    {\n" +
            "      \"id\": \"choice_opening\",\n" +
            "      \"type\": \"choice\",\n" +
            "      \"choicePrompt\": \"Куда пойти?\",\n" +
            "      \"choices\": [\n" +
            "        { \"text\": \"К старой тропе\", \"next\": \"stat_bravery\" },\n" +
            "        { \"text\": \"Остаться на месте\", \"next\": \"dialogue_wait\" }\n" +
            "      ]\n" +
            "    },\n" +
            "    {\n" +
            "      \"id\": \"stat_bravery\",\n" +
            "      \"type\": \"statChange\",\n" +
            "      \"statId\": \"bravery\",\n" +
            "      \"statDelta\": 1,\n" +
            "      \"statDisplayName\": \"Смелость\",\n" +
            "      \"systemMessage\": \"+1 Смелость\",\n" +
            "      \"next\": \"dialogue_end\"\n" +
            "    },\n" +
            "    {\n" +
            "      \"id\": \"dialogue_wait\",\n" +
            "      \"type\": \"dialogue\",\n" +
            "      \"lines\": [\n" +
            "        { \"speaker\": \"hero\", \"text\": \"Лучше сначала осмотреться.\" }\n" +
            "      ],\n" +
            "      \"next\": \"dialogue_end\"\n" +
            "    },\n" +
            "    {\n" +
            "      \"id\": \"dialogue_end\",\n" +
            "      \"type\": \"dialogue\",\n" +
            "      \"lines\": [\n" +
            "        { \"speaker\": \"companion\", \"emotion\": \"Serious\", \"text\": \"Ты опоздала.\" }\n" +
            "      ]\n" +
            "    }\n" +
            "  ]\n" +
            "}\n";
    }

    static string EscapeJson(string value)
    {
        return NetworkJson.Escape(value ?? "");
    }

    void SetStatus(string message, bool isError = false)
    {
        _status = message;
        _statusIsError = isError;
        Repaint();
    }
}
#endif
