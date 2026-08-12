#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class NocturnalServerToolsWindow : EditorWindow
{
    private static readonly string[] EnvironmentLabels = { "Тест", "Прод" };
    private static readonly string[] EnvironmentIds = { DeploymentEnvironmentIds.Stage, DeploymentEnvironmentIds.Production };
    private static readonly string[] PageLabels = { "Серверы", "Подключение", "Контент", "Публикация", "Контроль", "Выкладка", "Медиа", "Справка" };

    private string _storyId = "";
    private string _storyTitle = "";
    private string _seasonId = "";
    private string _seasonTitle = "";
    private string _baseUrl = ApiRoutes.BaseUrl;
    private string _adminKey = "";
    private string _episodeId = "";
    private string _episodeTitle = "";
    private string _contentVersion = "";
    private int _seasonOrder = 1;
    private int _episodeOrder = 1;
    private int _candleCost;
    private bool _allowHeroRename = true;
    private bool _isPremium;
    private bool _geoRestricted;
    private bool _allowUnsigned;
    private bool _isBusy;
    private int _environmentIndex;
    private TextAsset _episodeJson;
    private string _lastResponse = "";
    private Vector2 _scroll;
    private int _pageIndex = 1;

    [MenuItem("Инструменты/Nocturnal/Сервер", priority = 20)]
    [MenuItem("VN/Выкладка/Сервер Nocturnal", priority = 1)]
    public static void Open()
    {
        GetWindow<NocturnalServerToolsWindow>("Сервер Nocturnal");
    }

    [InitializeOnLoadMethod]
    private static void RegisterCleanup()
    {
        AssemblyReloadEvents.beforeAssemblyReload += StopAllServers;
        EditorApplication.quitting += StopAllServers;
    }

    private void OnEnable()
    {
        CurrentBackendCatalogPublisherPrefs backend = CurrentBackendCatalogPublisherPrefs.Load();
        _storyId = backend.StoryId;
        _storyTitle = backend.StoryTitle;
        _seasonId = backend.SeasonId;
        _seasonTitle = backend.SeasonTitle;
        _baseUrl = backend.BaseUrl;
        _episodeId = backend.EpisodeId;
        _episodeTitle = backend.EpisodeTitle;
        _seasonOrder = backend.SeasonOrder;
        _episodeOrder = backend.EpisodeOrder;
        _candleCost = backend.CandleCost;
        _allowHeroRename = backend.AllowHeroRename;
        _isPremium = backend.IsPremium;
        _geoRestricted = backend.GeoRestricted;
        _allowUnsigned = backend.AllowUnsigned;
        _contentVersion = EditorPrefs.GetString("NocturnalServerTools.ContentVersion", "");
    }

    private void OnDisable()
    {
        SavePrefs();
    }

    private void OnGUI()
    {
        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;
            DrawHeader();
            DrawPageTabs();
            DrawSelectedPage();
            DrawLastResponse();
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Инструменты сервера Nocturnal", EditorStyles.boldLabel);
    }

    private void DrawMockServers()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Локальные тестовые серверы", EditorStyles.boldLabel);
        DrawServerRow(
            "Mock релизов",
            IsReleaseMockRunning,
            StartReleaseMockServer,
            StopReleaseMockServer,
            UseReleaseMockInPublisher);
        DrawServerRow(
            "Mock backend-каталога",
            IsCurrentBackendMockRunning,
            StartCurrentBackendMockServer,
            StopCurrentBackendMockServer,
            UseCurrentBackendMockInPublisher);
    }

    private void DrawServerRow(string label, bool running, System.Action start, System.Action stop, System.Action use)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(180f));
        EditorGUILayout.LabelField(running ? "Запущен" : "Остановлен", GUILayout.Width(90f));
        using (new EditorGUI.DisabledScope(running))
        {
            if (GUILayout.Button(new GUIContent("Старт", "Запускает локальный сервер для безопасной проверки без реального backend."), GUILayout.Width(70f)))
                start();
        }
        using (new EditorGUI.DisabledScope(!running))
        {
            if (GUILayout.Button(new GUIContent("Подставить", "Записывает адрес этого mock-сервера в соответствующее окно публикации."), GUILayout.Width(90f)))
                use();
            if (GUILayout.Button(new GUIContent("Стоп", "Останавливает локальный сервер."), GUILayout.Width(70f)))
                stop();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCurrentBackend()
    {
        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField(PageLabels[_pageIndex], EditorStyles.boldLabel);
        EditorGUIUtility.labelWidth = 190f;
        using (new EditorGUI.DisabledScope(_isBusy))
        {
            if (_pageIndex == 1) {
                EditorGUILayout.HelpBox("Для реального сервера укажите https://nocturnedc.ru, вставьте X-Admin-Key и выключите «Разрешить без ключа». Затем нажмите «Получить каталог» для проверки доступа.", MessageType.Info);
                _baseUrl = EditorGUILayout.TextField(new GUIContent("Адрес сервера", "Для теста используйте mock или Stage. Для реального сервера - https://nocturnedc.ru."), _baseUrl, GUILayout.Height(28f));
                _adminKey = EditorGUILayout.PasswordField(new GUIContent("X-Admin-Key", "Секретный ключ администратора. Нужен для записи на удалённый сервер."), _adminKey, GUILayout.Height(28f));
                _allowUnsigned = EditorGUILayout.Toggle(new GUIContent("Разрешить без ключа", "Только для локального mock. Для Прод должно быть выключено."), _allowUnsigned, GUILayout.Height(24f));
                DrawBackendSafetyNotice(); ActionGroup("Проверка доступа"); BeginActionRow();
                if (ActionButton("Получить каталог", "Проверяет доступ к выбранному backend без записи.")) FetchCurrentCatalog();
                if (ActionButton("Локальная проверка", "Запускает полный безопасный тест на локальном mock-сервере.")) RunCurrentBackendSmoke();
                EndActionRow(); return;
            }
            if (_pageIndex == 2) {
                _storyId = EditorGUILayout.TextField(new GUIContent("ID истории", "Уникальный технический ID истории, например story_01."), _storyId, GUILayout.Height(28f));
                _storyTitle = EditorGUILayout.TextField(new GUIContent("Название истории", "Название, которое попадёт в каталог."), _storyTitle, GUILayout.Height(28f));
                _allowHeroRename = EditorGUILayout.Toggle(new GUIContent("Можно менять имя героя", "Включите, если игрок может переименовать главного героя."), _allowHeroRename, GUILayout.Height(24f));
                _seasonId = EditorGUILayout.TextField(new GUIContent("ID сезона", "Уникальный ID сезона внутри истории."), _seasonId, GUILayout.Height(28f));
                _seasonTitle = EditorGUILayout.TextField(new GUIContent("Название сезона", "Отображаемое название сезона."), _seasonTitle, GUILayout.Height(28f));
                _seasonOrder = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Порядок сезона", "Число для сортировки сезонов в каталоге."), _seasonOrder, GUILayout.Height(28f)));
                _episodeId = EditorGUILayout.TextField(new GUIContent("ID эпизода", "Уникальный ID серии/эпизода."), _episodeId, GUILayout.Height(28f));
                _episodeTitle = EditorGUILayout.TextField(new GUIContent("Название эпизода", "Отображаемое название серии/эпизода."), _episodeTitle, GUILayout.Height(28f));
                _episodeOrder = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Порядок эпизода", "Число для сортировки эпизодов в сезоне."), _episodeOrder, GUILayout.Height(28f)));
                _isPremium = EditorGUILayout.Toggle(new GUIContent("Платный эпизод", "Включите, если эпизод открывается за свечи."), _isPremium, GUILayout.Height(24f));
                _candleCost = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Цена в свечах", "Стоимость эпизода в свечах. Для бесплатного эпизода оставьте 0."), _candleCost, GUILayout.Height(28f)));
                _geoRestricted = EditorGUILayout.Toggle(new GUIContent("Гео-ограничение", "Включите, если эпизод должен быть скрыт в отдельных регионах."), _geoRestricted, GUILayout.Height(24f));
                ActionGroup("Создание черновика"); BeginActionRow();
                if (ActionButton("Создать историю", "Создаёт черновик истории на выбранном backend.")) CreateCurrentStory();
                if (ActionButton("Добавить сезон", "Добавляет сезон в указанную историю.")) AddCurrentSeason();
                if (ActionButton("Добавить эпизод", "Добавляет эпизод в указанный сезон.")) AddCurrentEpisode();
                EndActionRow(); return;
            }
            _storyId = EditorGUILayout.TextField(new GUIContent("ID истории", "ID истории для текущего действия."), _storyId, GUILayout.Height(28f));
            _episodeId = EditorGUILayout.TextField(new GUIContent("ID эпизода", "ID эпизода для текущего действия."), _episodeId, GUILayout.Height(28f));
            if (_pageIndex == 3) { DrawBackendSafetyNotice(); ActionGroup("Публикация истории и эпизода"); BeginActionRow();
                if (ActionButton("Опубликовать историю", "Открывает историю игрокам после проверки.")) SetCurrentStoryPublished(true);
                if (ActionButton("Скрыть историю", "Скрывает историю из публичного каталога.")) SetCurrentStoryPublished(false);
                if (ActionButton("Опубликовать эпизод", "Открывает эпизод игрокам после проверки.")) SetCurrentEpisodePublished(true);
                if (ActionButton("Скрыть эпизод", "Скрывает эпизод из публичного каталога.")) SetCurrentEpisodePublished(false);
                EndActionRow(); return; }
            _seasonId = EditorGUILayout.TextField(new GUIContent("ID сезона", "Нужен для предпросмотра и чеклиста."), _seasonId, GUILayout.Height(28f));
            _episodeJson = EditorGUILayout.ObjectField(new GUIContent("JSON эпизода", "TextAsset с подготовленным JSON серии для загрузки на backend."), _episodeJson, typeof(TextAsset), false, GUILayout.Height(28f)) as TextAsset;
            ActionGroup("Проверки и загрузка"); BeginActionRow();
            if (ActionButton("Получить каталог", "Загружает текущий каталог с сервера для проверки.")) FetchCurrentCatalog();
            if (ActionButton("Загрузить JSON эпизода", "Отправляет выбранный JSON серии в backend.")) UploadEpisodeJsonToCurrentBackend();
            if (ActionButton("Предпросмотр запросов", "Показывает маршруты и JSON без отправки на сервер.")) PreviewBackendPayloads();
            EndActionRow(); ActionGroup("Документы для передачи"); BeginActionRow();
            if (ActionButton("Скопировать чеклист", "Копирует короткий список проверок перед публикацией.")) CopyBackendChecklist();
            if (ActionButton("Скопировать API-команды", "Копирует команды для ручной проверки backend API.")) CopyCurrentBackendCurl();
            if (ActionButton("Текст передачи", "Копирует полный отчёт для передачи другому человеку.")) CopyBackendHandoffReport();
            EndActionRow();
        }
    }

    private void DrawLastResponse()
    {
        if (string.IsNullOrWhiteSpace(_lastResponse))
            return;

        EditorGUILayout.Space(8f);
        if (ActionButton("Скопировать ответ", "Копирует последний результат, предпросмотр или ответ сервера."))
            EditorGUIUtility.systemCopyBuffer = _lastResponse;
        EditorGUILayout.TextArea(_lastResponse, GUILayout.MinHeight(90f));
    }
}
#endif
