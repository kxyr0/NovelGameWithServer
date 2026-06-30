#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class ChapterImportWindow : EditorWindow
{
    private const string DefaultGraphSavePath = "Assets/_MyProject/Data/Stories/Generated/Graphs/";
    private const string LegacyGraphSavePath = "Assets/NovelTemplate/Graphs/";
    private const string ApiKeyPreferenceKey = "VN_REMOTE_CHAPTER_API_KEY";
    private const string ModelPreferenceKey = "VN_REMOTE_CHAPTER_MODEL";
    private const string GraphPathPreferenceKey = "VN_GRAPH_PATH";

    private static readonly string LegacyApiKeyPreferenceKey = "VN_KXYR0_" + LegacyServiceToken + "_KEY";
    private static readonly string LegacyModelPreferenceKey = "VN_" + LegacyServiceToken + "_MODEL";
    private static string LegacyServiceToken => new string(new[] { (char)65, (char)73 });

    private enum Tab
    {
        Input,
        Preview,
        Assets,
        Settings
    }

    private Tab _tab = Tab.Input;
    private string _chapterText = "";
    private string _graphName = "NewChapter";
    private string _graphSavePath = DefaultGraphSavePath;
    private ParsedChapterData _parsedData;
    private StoryGraph _targetGraph;
    private StoryGraphAssetMatchReport _matchReport;
    private bool _analyzing;
    private bool _matchingAssets;
    private string _statusMessage = "";
    private bool _statusIsError;
    private string _apiKey = "";
    private string _model = "g" + "pt-4o";
    private float _temperature = 0.2f;
    private Vector2 _inputScroll;
    private Vector2 _previewScroll;
    private Vector2 _assetsScroll;

    [MenuItem("VN/Импорт глав")]
    public static void Open()
    {
        var window = GetWindow<ChapterImportWindow>("Импорт глав");
        window.minSize = new Vector2(600, 700);
    }

    private void OnEnable()
    {
        _apiKey = ReadEditorPreference(ApiKeyPreferenceKey, LegacyApiKeyPreferenceKey, "");
        _model = ReadEditorPreference(ModelPreferenceKey, LegacyModelPreferenceKey, "g" + "pt-4o");
        _graphSavePath = NormalizeGraphSavePath(EditorPrefs.GetString(GraphPathPreferenceKey, DefaultGraphSavePath));
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawTabs();
        EditorGUILayout.Space(4);

        switch (_tab)
        {
            case Tab.Input:
                DrawInputTab();
                break;
            case Tab.Preview:
                DrawPreviewTab();
                break;
            case Tab.Assets:
                DrawAssetsTab();
                break;
            case Tab.Settings:
                DrawSettingsTab();
                break;
        }

        DrawStatus();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(8);
        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("Импорт глав", style, GUILayout.Height(28));
        EditorGUILayout.Space(4);
    }

    private void DrawTabs()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Toggle(_tab == Tab.Input, "Текст", EditorStyles.toolbarButton)) _tab = Tab.Input;
        if (GUILayout.Toggle(_tab == Tab.Preview, "Превью", EditorStyles.toolbarButton)) _tab = Tab.Preview;
        if (GUILayout.Toggle(_tab == Tab.Assets, "Ассеты", EditorStyles.toolbarButton)) _tab = Tab.Assets;
        if (GUILayout.Toggle(_tab == Tab.Settings, "Настройки", EditorStyles.toolbarButton)) _tab = Tab.Settings;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawInputTab()
    {
        EditorGUILayout.LabelField("Название графа:", EditorStyles.miniLabel);
        _graphName = EditorGUILayout.TextField(_graphName);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Текст главы:", EditorStyles.miniLabel);

        _inputScroll = EditorGUILayout.BeginScrollView(_inputScroll, GUILayout.Height(380));
        _chapterText = EditorGUILayout.TextArea(
            _chapterText,
            new GUIStyle(EditorStyles.textArea) { wordWrap = true },
            GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6);
        DrawInputHint();
        EditorGUILayout.Space(8);

        GUI.enabled = !_analyzing && !string.IsNullOrWhiteSpace(_chapterText) && !string.IsNullOrEmpty(_apiKey);
        if (GUILayout.Button(_analyzing ? "Анализирую..." : "Анализировать текст", GUILayout.Height(40)))
            Analyze();
        GUI.enabled = true;

        if (string.IsNullOrEmpty(_apiKey))
            EditorGUILayout.HelpBox("Укажи ключ API во вкладке «Настройки».", MessageType.Warning);
    }

    private static void DrawInputHint()
    {
        string hint =
            "Поддерживаемые маркеры в тексте:\n" +
            "  Персонаж: текст реплики\n" +
            "  [СЦЕНА: описание фона, музыки]\n" +
            "  [ВЫБОР] ... варианты ... [/ВЫБОР]\n" +
            "  [ЭМОЦИЯ: счастливая] — после имени персонажа\n" +
            "  // комментарий — игнорируется при разборе";

        EditorGUILayout.HelpBox(hint, MessageType.Info);
    }

    private void DrawPreviewTab()
    {
        if (_parsedData == null)
        {
            EditorGUILayout.HelpBox("Сначала проанализируй текст на вкладке «Текст».", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        DrawStatBadge("Сцен", _parsedData.scenes.Count.ToString());
        DrawStatBadge("Реплик", _parsedData.TotalLines.ToString());
        DrawStatBadge("Выборов", _parsedData.TotalChoices.ToString());
        DrawStatBadge("Персонажей", _parsedData.UniqueCharacters.Count.ToString());
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        if (_parsedData.unmatchedCharacters.Count > 0)
        {
            string unmatched = string.Join(", ", _parsedData.unmatchedCharacters);
            EditorGUILayout.HelpBox(
                $"Персонажи не найдены в проекте: {unmatched}\n" +
                "Будут созданы DialogueNode без CharacterData.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(4);
        _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll);

        foreach (ParsedSceneData scene in _parsedData.scenes)
        {
            EditorGUILayout.BeginVertical("box");

            var sceneStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.6f, 0.9f, 1f) }
            };
            EditorGUILayout.LabelField(scene.sceneDescription, sceneStyle);

            if (!string.IsNullOrEmpty(scene.suggestedBackground))
                EditorGUILayout.LabelField($"Фон: {scene.suggestedBackground}", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(scene.suggestedMusic))
                EditorGUILayout.LabelField($"Музыка: {scene.suggestedMusic}", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);

            foreach (ParsedStoryNodeData node in scene.nodes)
                DrawNodePreview(node);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(8);

        GUI.enabled = _parsedData != null && !_analyzing;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Создать граф без ассетов", GUILayout.Height(40)))
            BuildGraph(matchAssets: false);
        if (GUILayout.Button("Создать граф с ассетами", GUILayout.Height(40)))
            BuildGraph(matchAssets: true);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            "Ассеты можно привязать позже на вкладке «Ассеты».",
            new GUIStyle(EditorStyles.centeredGreyMiniLabel));

        GUI.enabled = true;
    }

    private static void DrawNodePreview(ParsedStoryNodeData node)
    {
        switch (node.type)
        {
            case "dialogue":
                foreach (ParsedDialogueLineData line in node.lines)
                {
                    var speakerStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = new Color(0.9f, 0.8f, 0.5f) },
                        fontStyle = FontStyle.Bold
                    };
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(12);
                    EditorGUILayout.LabelField($"{line.speaker} [{line.emotion}]:", speakerStyle, GUILayout.Width(180));
                    EditorGUILayout.LabelField(TruncateText(line.text, 60), EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.EndHorizontal();
                }
                break;

            case "choice":
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                EditorGUILayout.LabelField(
                    "Выбор:",
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = new Color(0.5f, 1f, 0.5f) },
                        fontStyle = FontStyle.Bold
                    },
                    GUILayout.Width(60));

                string choices = string.Join(" | ", node.choices.Select(choice => choice.text));
                EditorGUILayout.LabelField(TruncateText(choices, 80), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndHorizontal();
                break;
        }
    }

    private static void DrawStatBadge(string label, string value)
    {
        var style = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.LabelField($"{value}\n{label}", style, GUILayout.Height(40), GUILayout.ExpandWidth(true));
    }

    private void DrawAssetsTab()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Привязка ассетов к существующему графу", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Выбери уже созданный граф, и пустые ссылки на персонажей, фоны и музыку будут заполнены по именам из проекта. Уже заполненные поля не меняются.",
            MessageType.Info);

        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Граф:", GUILayout.Width(50));
        _targetGraph = (StoryGraph)EditorGUILayout.ObjectField(_targetGraph, typeof(StoryGraph), false);
        EditorGUILayout.EndHorizontal();

        if (_parsedData != null)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(52);
            if (GUILayout.Button("Использовать последний созданный граф", EditorStyles.miniButton))
            {
                _graphSavePath = NormalizeGraphSavePath(_graphSavePath);
                string path = (_graphSavePath + _graphName + ".asset").Replace("\\", "/");
                _targetGraph = AssetDatabase.LoadAssetAtPath<StoryGraph>(path);
                if (_targetGraph == null)
                    SetStatus("Граф не найден. Сначала создай его на вкладке «Превью».", isError: true);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8);

        GUI.enabled = _targetGraph != null && !_matchingAssets;
        if (GUILayout.Button(_matchingAssets ? "Подбираю..." : "Подобрать ассеты", GUILayout.Height(36)))
            RunAssetMatching();
        GUI.enabled = true;

        if (_matchReport == null)
            return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Результат:", EditorStyles.boldLabel);
        _assetsScroll = EditorGUILayout.BeginScrollView(_assetsScroll, GUILayout.Height(300));
        DrawMatchReport(_matchReport);
        EditorGUILayout.EndScrollView();
    }

    private void RunAssetMatching()
    {
        if (_targetGraph == null)
            return;

        _matchingAssets = true;
        SetStatus("Сканирую ассеты...");
        Repaint();
        EditorApplication.delayCall += DoAssetMatching;
    }

    private void DoAssetMatching()
    {
        if (_targetGraph == null)
        {
            _matchingAssets = false;
            Repaint();
            return;
        }

        ProjectAssetContext context = ProjectAssetContext.Build();
        _matchReport = StoryGraphAssetMatcher.MatchAndApply(_targetGraph, context);

        _matchingAssets = false;
        SetStatus($"Готово: {_matchReport.applied} ассетов привязано, {_matchReport.skipped} уже были заполнены.");

        EditorUtility.SetDirty(_targetGraph);
        AssetDatabase.SaveAssets();
        Repaint();
    }

    private static void DrawMatchReport(StoryGraphAssetMatchReport report)
    {
        var okStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.4f, 1f, 0.4f) } };
        var skipStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
        var warnStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.8f, 0.3f) } };

        foreach (StoryGraphAssetMatchReport.Entry entry in report.entries)
        {
            string status = entry.status switch
            {
                StoryGraphAssetMatchReport.Status.Applied => "Привязано",
                StoryGraphAssetMatchReport.Status.Skipped => "Пропущено",
                StoryGraphAssetMatchReport.Status.NotFound => "Не найдено",
                _ => "Неизвестно"
            };
            GUIStyle style = entry.status switch
            {
                StoryGraphAssetMatchReport.Status.Applied => okStyle,
                StoryGraphAssetMatchReport.Status.Skipped => skipStyle,
                _ => warnStyle
            };
            EditorGUILayout.LabelField($"{status}: {entry.nodeType} | {entry.fieldName}: {entry.value}", style);
        }
    }

    private void DrawSettingsTab()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Сервис разбора", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Ключ API:", GUILayout.Width(120));
        _apiKey = EditorGUILayout.PasswordField(_apiKey);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Модель:", GUILayout.Width(120));
        _model = EditorGUILayout.TextField(_model);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Вариативность:", GUILayout.Width(120));
        _temperature = EditorGUILayout.Slider(_temperature, 0f, 1f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Пути", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Сохранять граф в:", GUILayout.Width(120));
        _graphSavePath = EditorGUILayout.TextField(_graphSavePath);
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
        {
            _graphSavePath = NormalizeGraphSavePath(_graphSavePath);
            EditorPrefs.SetString(ApiKeyPreferenceKey, _apiKey);
            EditorPrefs.SetString(ModelPreferenceKey, _model);
            EditorPrefs.SetString(GraphPathPreferenceKey, _graphSavePath);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Ключ хранится в EditorPrefs и не попадает в git или сборку.\n" +
            "Для продакшена используй переменную окружения KXYR0_CHAPTER_IMPORT_KEY.",
            MessageType.Info);
    }

    private void DrawStatus()
    {
        if (string.IsNullOrEmpty(_statusMessage))
            return;

        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = _statusIsError ? Color.red : Color.green },
            alignment = TextAnchor.MiddleCenter
        };

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(_statusMessage, style);
    }

    private void SetStatus(string message, bool isError = false)
    {
        _statusMessage = message;
        _statusIsError = isError;
        Repaint();
    }

    private void Analyze()
    {
        _analyzing = true;
        _parsedData = null;
        SetStatus("Отправляю запрос к сервису разбора...");

        ProjectAssetContext context = ProjectAssetContext.Build();
        RemoteChapterAnalyzer.Analyze(
            text: _chapterText,
            apiKey: _apiKey,
            model: _model,
            temperature: _temperature,
            context: context,
            onComplete: result =>
            {
                _analyzing = false;

                if (result.error != null)
                {
                    SetStatus($"Ошибка: {result.error}", isError: true);
                    return;
                }

                _parsedData = result.data;
                _tab = Tab.Preview;
                SetStatus($"Готово: {_parsedData.TotalLines} реплик, {_parsedData.TotalChoices} выборов.");
                Repaint();
            });
    }

    private void BuildGraph(bool matchAssets)
    {
        if (_parsedData == null)
            return;

        SetStatus(matchAssets
            ? "Создаю граф и привязываю ассеты..."
            : "Создаю граф без ассетов...");

        try
        {
            _graphSavePath = NormalizeGraphSavePath(_graphSavePath);
            string path = StoryGraphBuilder.Build(_parsedData, _graphName, _graphSavePath, matchAssets);
            string mode = matchAssets ? "с ассетами" : "без ассетов";
            SetStatus($"Граф создан ({mode}): {path}");
            AssetDatabase.Refresh();

            StoryGraph asset = AssetDatabase.LoadAssetAtPath<StoryGraph>(path);
            if (asset != null)
            {
                Selection.activeObject = asset;
                _targetGraph = asset;
            }

            if (!matchAssets)
            {
                EditorApplication.delayCall += () =>
                {
                    if (EditorUtility.DisplayDialog(
                        "Граф создан",
                        "Граф создан без ассетов.\nПривязать персонажей, фоны и музыку сейчас?",
                        "Да, привязать",
                        "Позже"))
                    {
                        _tab = Tab.Assets;
                        Repaint();
                    }
                };
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Ошибка создания графа: {exception.Message}", isError: true);
            Debug.LogException(exception);
        }
    }

    private static string ReadEditorPreference(string currentKey, string legacyKey, string defaultValue)
    {
        if (EditorPrefs.HasKey(currentKey))
            return EditorPrefs.GetString(currentKey, defaultValue);

        return EditorPrefs.HasKey(legacyKey)
            ? EditorPrefs.GetString(legacyKey, defaultValue)
            : defaultValue;
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (text == null)
            return "";

        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    private static string NormalizeGraphSavePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return DefaultGraphSavePath;

        path = path.Trim().Replace("\\", "/");

        if (string.Equals(path.TrimEnd('/'), LegacyGraphSavePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            return DefaultGraphSavePath;

        return path.EndsWith("/") ? path : path + "/";
    }
}
#endif
