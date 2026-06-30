#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

public sealed class UnityChoiceCostsPublisherWindow : EditorWindow
{
    private const string LegacyAdminKeyPrefsKey = "VN_UNITY_PUBLISHER_ADMIN_KEY";
    private const string BaseUrlPrefsKey = "VN_UNITY_PUBLISHER_BASE_URL";
    private const string AllowUnsignedPrefsKey = "VN_UNITY_PUBLISHER_ALLOW_UNSIGNED";
    private const int MaxPreviewChars = 12000;

    [SerializeField] private string _storyIdOverride = "";
    [SerializeField] private string _episodeIdOverride = "";
    [SerializeField] private string _baseUrlOverride = "";
    [SerializeField] private string _adminKey = "";
    [SerializeField] private bool _allowUnsignedPublisherRequests;
    [SerializeField] private bool _includeAllProjectGraphs;
    [SerializeField] private string _deleteNodeGuid = "";
    [SerializeField] private Vector2 _scroll;

    private UnityChoiceCostsPublishPayload _lastPayload;
    private string _lastJson = "";
    private string _status = "";
    private bool _isBusy;

    [MenuItem("VN/Публикация/Цены выборов")]
    public static void Open()
    {
        var window = GetWindow<UnityChoiceCostsPublisherWindow>("Цены выборов");
        window.minSize = new Vector2(560f, 520f);
        window.LoadPrefs();
        window.ScanSelection();
    }

    [MenuItem("VN/Публикация/Опубликовать цены выбранных выборов")]
    public static void PublishSelectedMenu()
    {
        var payload = UnityChoiceCostsPublisher.BuildPayload(
            UnityChoiceCostsPublisher.GetSelectedGraphs(),
            "",
            "");

        if (payload == null || payload.costs.Count == 0)
        {
            EditorUtility.DisplayDialog("Цены выборов", "В выбранных StoryGraph не найдены платные выборы.", "ОК");
            return;
        }

        EditorCoroutineRunner.Start(UnityChoiceCostsPublisher.Publish(payload, result =>
        {
            if (!result.Success)
            {
                EditorUtility.DisplayDialog("Цены выборов", result.Error, "ОК");
                return;
            }

            EditorUtility.DisplayDialog("Цены выборов", "Опубликовано цен выборов: " + payload.costs.Count + ".", "ОК");
        }));
    }

    private void OnEnable()
    {
        LoadPrefs();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Публикация цен выборов", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Редакторская публикация для /unity/choice-costs. Доступ runtime-клиента к /unity/* остаётся заблокированным.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(_isBusy))
        {
            DrawSettings();
            DrawActions();
        }

        if (!string.IsNullOrEmpty(_status))
            EditorGUILayout.HelpBox(_status, MessageType.None);

        DrawPreview();
    }

    private void DrawSettings()
    {
        EditorGUILayout.Space(4);
        _includeAllProjectGraphs = EditorGUILayout.ToggleLeft("Сканировать все StoryGraph в проекте", _includeAllProjectGraphs);
        _storyIdOverride = EditorGUILayout.TextField("Переопределить ID истории", _storyIdOverride);
        _episodeIdOverride = EditorGUILayout.TextField("Переопределить ID эпизода", _episodeIdOverride);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Сервер", EditorStyles.boldLabel);
        _baseUrlOverride = EditorGUILayout.TextField("Базовый URL", _baseUrlOverride);
        _adminKey = EditorGUILayout.PasswordField("X-Admin-Key", _adminKey);
        EditorGUILayout.HelpBox("Админ-ключ не сохраняется в проекте или EditorPrefs. Для постоянной локальной настройки используй NOCTURNEDC_ADMIN_KEY.", MessageType.None);
        _allowUnsignedPublisherRequests = EditorGUILayout.ToggleLeft(
            "Разрешить запрос без подписи",
            _allowUnsignedPublisherRequests);

        if (GUILayout.Button("Сохранить локальные настройки редактора"))
            SavePrefs();
    }

    private void DrawActions()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Сканировать"))
            Scan();

        using (new EditorGUI.DisabledScope(_lastPayload == null || _lastPayload.costs.Count == 0))
        {
            if (GUILayout.Button("Опубликовать"))
                Publish();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Загрузить цены с сервера"))
            Fetch();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _deleteNodeGuid = EditorGUILayout.TextField("Удалить узел по GUID", _deleteNodeGuid);
        if (GUILayout.Button("Удалить", GUILayout.Width(90)))
            Delete();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPreview()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Предпросмотр данных запроса", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.TextArea(_lastJson, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Scan()
    {
        var graphs = _includeAllProjectGraphs
            ? UnityChoiceCostsPublisher.GetAllProjectGraphs()
            : UnityChoiceCostsPublisher.GetSelectedGraphs();

        _lastPayload = UnityChoiceCostsPublisher.BuildPayload(graphs, _storyIdOverride, _episodeIdOverride);
        _lastJson = UnityChoiceCostsPublisher.ToJson(_lastPayload, pretty: true);
        if (_lastJson.Length > MaxPreviewChars)
            _lastJson = _lastJson.Substring(0, MaxPreviewChars) + "\n...";

        int count = _lastPayload != null ? _lastPayload.costs.Count : 0;
        _status = "Найдено записей цен платных выборов: " + count + ".";
        Repaint();
    }

    private void ScanSelection()
    {
        if (Selection.objects == null || Selection.objects.Length == 0)
            return;

        Scan();
    }

    private void Publish()
    {
        if (_lastPayload == null || _lastPayload.costs.Count == 0)
        {
            _status = "Нечего публиковать.";
            return;
        }

        SavePrefs();
        Run(UnityChoiceCostsPublisher.Publish(_lastPayload, OnRequestComplete, _baseUrlOverride, _adminKey, _allowUnsignedPublisherRequests));
    }

    private void Fetch()
    {
        SavePrefs();
        Run(UnityChoiceCostsPublisher.Fetch(
            _storyIdOverride,
            _episodeIdOverride,
            result =>
            {
                OnRequestComplete(result);
                if (result.Success)
                    _lastJson = result.Body ?? "";
            },
            _baseUrlOverride,
            _adminKey,
            _allowUnsignedPublisherRequests));
    }

    private void Delete()
    {
        SavePrefs();
        Run(UnityChoiceCostsPublisher.Delete(_deleteNodeGuid, OnRequestComplete, _baseUrlOverride, _adminKey, _allowUnsignedPublisherRequests));
    }

    private void Run(IEnumerator routine)
    {
        _isBusy = true;
        _status = "Запрос выполняется...";
        Repaint();
        EditorCoroutineRunner.Start(WrapRequest(routine));
    }

    private IEnumerator WrapRequest(IEnumerator routine)
    {
        yield return routine;
        _isBusy = false;
        Repaint();
    }

    private void OnRequestComplete(UnityPublisherRequestResult result)
    {
        _isBusy = false;
        _status = result.Success
            ? "Готово: " + result.StatusCode + "."
            : "Ошибка: " + result.Error;
        Repaint();
    }

    private void LoadPrefs()
    {
        _baseUrlOverride = EditorPrefs.GetString(BaseUrlPrefsKey, "");
        _adminKey = Environment.GetEnvironmentVariable("NOCTURNEDC_ADMIN_KEY") ?? "";
        _allowUnsignedPublisherRequests = EditorPrefs.GetBool(AllowUnsignedPrefsKey, false);
    }

    private void SavePrefs()
    {
        EditorPrefs.SetString(BaseUrlPrefsKey, _baseUrlOverride ?? "");
        if (EditorPrefs.HasKey(LegacyAdminKeyPrefsKey))
            EditorPrefs.DeleteKey(LegacyAdminKeyPrefsKey);
        EditorPrefs.SetBool(AllowUnsignedPrefsKey, _allowUnsignedPublisherRequests);
    }
}
#endif
