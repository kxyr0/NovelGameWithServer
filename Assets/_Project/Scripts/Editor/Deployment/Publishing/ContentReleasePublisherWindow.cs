#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class ContentReleasePublisherWindow : EditorWindow
{
    private static readonly string[] EnvironmentLabels = { "Тест", "Прод" };
    private static readonly string[] EnvironmentIds =
    {
        DeploymentEnvironmentIds.Stage,
        DeploymentEnvironmentIds.Production
    };

    private static readonly string[] StatusLabels = { ContentReleaseStatus.Draft, ContentReleaseStatus.Staging, ContentReleaseStatus.Published, ContentReleaseStatus.Archived };
    private static readonly string[] StatusDisplayLabels = { "Черновик", "Тест", "Опубликовано", "Архив" };

    private string _environmentId = DeploymentEnvironmentIds.Stage;
    private string _status = ContentReleaseStatus.Staging;
    private string _storyId = "";
    private string _episodeId = "";
    private string _contentVersion = "";
    private string _catalogUrl = "";
    private string _loadPath = "";
    private string _manifestUrl = "";
    private string _manifestHash = "";
    private string _buildTarget = "";
    private string _minAppVersion = "";
    private string _notes = "";
    private string _baseUrl = "";
    private string _adminKey = "";
    private string _lastResponse = "";
    private bool _allowUnsigned;
    private bool _isBusy;
    private Vector2 _scroll;
    private Vector2 _previewScroll;

    [MenuItem("VN/Выкладка/Публикатор релизов", priority = 20)]
    public static void Open()
    {
        GetWindow<ContentReleasePublisherWindow>("Релиз контента");
    }

    private void OnEnable()
    {
        ContentReleasePublisherPrefs prefs = ContentReleasePublisherPrefs.Load();
        _environmentId = prefs.EnvironmentId;
        _status = prefs.Status;
        _storyId = prefs.StoryId;
        _episodeId = prefs.EpisodeId;
        _contentVersion = prefs.ContentVersion;
        _catalogUrl = prefs.CatalogUrl;
        _loadPath = prefs.LoadPath;
        _manifestUrl = prefs.ManifestUrl;
        _manifestHash = prefs.ManifestHash;
        _buildTarget = prefs.BuildTarget;
        _minAppVersion = prefs.MinAppVersion;
        _notes = prefs.Notes;
        _baseUrl = prefs.BaseUrl;
        _allowUnsigned = prefs.AllowUnsigned;

        if (!DeploymentEnvironmentIds.IsStage(_environmentId) &&
            !DeploymentEnvironmentIds.IsProduction(_environmentId))
            _environmentId = DeploymentEnvironmentIds.Stage;

        ApplyEnvironmentPreset(onlyMissingValues: true);
    }

    private void OnDisable()
    {
        SavePrefs();
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        using (new EditorGUI.DisabledScope(_isBusy))
        {
            DrawEnvironment();
            DrawReleaseFields();
            DrawConnectionFields();
            DrawActionButtons();
        }

        DrawPayloadPreview();
        DrawLastResponse();
        EditorGUILayout.EndScrollView();
    }

    private void DrawEnvironment()
    {
        EditorGUILayout.LabelField("Среда публикации", EditorStyles.boldLabel);
        int selected = DeploymentEnvironmentIds.IsProduction(_environmentId) ? 1 : 0;
        int next = GUILayout.Toolbar(selected, EnvironmentLabels);
        if (next != selected)
        {
            _environmentId = EnvironmentIds[next];
            ApplyEnvironmentPreset(onlyMissingValues: false);
        }
    }

    private void DrawReleaseFields()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Данные релиза", EditorStyles.boldLabel);

        int statusIndex = IndexOf(StatusLabels, _status);
        int nextStatus = EditorGUILayout.Popup(new GUIContent("Статус", "Статус релиза в каталоге контента."), statusIndex, StatusDisplayLabels);
        _status = StatusLabels[Mathf.Clamp(nextStatus, 0, StatusLabels.Length - 1)];

        EditorGUILayout.BeginHorizontal();
        _storyId = EditorGUILayout.TextField(new GUIContent("ID истории", "ID истории, к которой относится релиз."), _storyId);
        if (GUILayout.Button(new GUIContent("Из выбора", "Берёт ID из выбранного StoryData, ChapterData или StoryGraph."), GUILayout.Width(110f)))
            CaptureSelectedIds();
        EditorGUILayout.EndHorizontal();

        _episodeId = EditorGUILayout.TextField(new GUIContent("ID эпизода", "ID серии/эпизода для этого релиза."), _episodeId);
        _contentVersion = EditorGUILayout.TextField(new GUIContent("Версия контента", "Версия контента, которая попадёт в manifest и backend."), _contentVersion);
        _minAppVersion = EditorGUILayout.TextField(new GUIContent("Мин. версия приложения", "Минимальная версия клиента, которая может загрузить этот контент."), _minAppVersion);
        _notes = EditorGUILayout.TextField(new GUIContent("Заметки", "Внутренний комментарий к релизу."), _notes);

        EditorGUILayout.Space(4f);
        _loadPath = EditorGUILayout.TextField(new GUIContent("Путь загрузки Addressables", "Удалённый путь, откуда клиент будет брать игровые файлы."), _loadPath);
        _catalogUrl = EditorGUILayout.TextField(new GUIContent("URL каталога", "URL удалённого каталога Addressables."), _catalogUrl);
        _manifestUrl = EditorGUILayout.TextField(new GUIContent("URL manifest", "URL manifest-файла для проверки состава релиза."), _manifestUrl);
        _manifestHash = EditorGUILayout.TextField(new GUIContent("Хеш manifest", "SHA-256 manifest, нужен для контроля изменений."), _manifestHash);
        _buildTarget = EditorGUILayout.TextField(new GUIContent("Платформа сборки", "Платформа Unity, для которой собран контент."), _buildTarget);
    }

    private void DrawConnectionFields()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Сервер", EditorStyles.boldLabel);
        _baseUrl = EditorGUILayout.TextField(new GUIContent("Адрес сервера", "Backend API для публикации релиза."), _baseUrl);
        _adminKey = EditorGUILayout.PasswordField(new GUIContent("X-Admin-Key", "Секретный ключ администратора для записи на сервер."), _adminKey);
        _allowUnsigned = EditorGUILayout.Toggle(new GUIContent("Разрешить без ключа", "Только для локального mock или доверенного тестового сервера."), _allowUnsigned);
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Проверить", "Проверяет заполненные поля релиза без отправки на сервер.")))
            ValidateRelease();
        if (GUILayout.Button(new GUIContent("Собрать manifest", "Собирает manifest для выбранной среды и версии контента.")))
            BuildManifest();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Отправить релиз", "Создаёт или обновляет релиз на выбранном сервере.")))
            PublishRelease();
        if (GUILayout.Button(new GUIContent("Получить", "Загружает релиз с сервера для проверки.")))
            FetchRelease();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Перенести Тест -> Прод", "Продвигает проверенный тестовый релиз в продакшен.")))
            PromoteRelease();
        if (GUILayout.Button(new GUIContent("Откатить Прод", "Откатывает продакшен-релиз на указанную версию.")))
            RollbackRelease();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPayloadPreview()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Предпросмотр JSON", EditorStyles.boldLabel);
        _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.MinHeight(120f));
        EditorGUILayout.TextArea(ContentReleasePayloadBuilder.ToJson(BuildRelease(), pretty: true));
        EditorGUILayout.EndScrollView();
    }

    private void DrawLastResponse()
    {
        if (string.IsNullOrWhiteSpace(_lastResponse))
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(_lastResponse, MessageType.Info);
    }

    private static int IndexOf(string[] values, string value)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == value)
                return i;
        }

        return 0;
    }
}
#endif
