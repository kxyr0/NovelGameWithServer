#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine;

public sealed class CurrentBackendCatalogPublisherWindow : EditorWindow
{
    private string _episodeId = "";
    private string _baseUrl = ApiRoutes.BaseUrl;
    private string _adminKey = "";
    private bool _allowUnsigned;
    private bool _isBusy;
    private TextAsset _episodeJson;
    private string _lastResponse = "";
    private Vector2 _scroll;
    private Vector2 _windowScroll;

    [MenuItem("VN/Выкладка/Публикатор backend-каталога", priority = 22)]
    public static void Open()
    {
        GetWindow<CurrentBackendCatalogPublisherWindow>("Backend-каталог");
    }

    private void OnEnable()
    {
        CurrentBackendCatalogPublisherPrefs prefs = CurrentBackendCatalogPublisherPrefs.Load();
        _episodeId = prefs.EpisodeId;
        _baseUrl = prefs.BaseUrl;
        _allowUnsigned = prefs.AllowUnsigned;
    }

    private void OnDisable()
    {
        SavePrefs();
    }

    private void OnGUI()
    {
        _windowScroll = EditorGUILayout.BeginScrollView(_windowScroll);
        using (new EditorGUI.DisabledScope(_isBusy))
        {
            EditorGUILayout.LabelField("Администрирование backend-каталога", EditorStyles.boldLabel);
            _baseUrl = EditorGUILayout.TextField(new GUIContent("Адрес сервера", "Backend API для каталога."), _baseUrl);
            _adminKey = EditorGUILayout.PasswordField(new GUIContent("X-Admin-Key", "Секретный ключ администратора для записи на сервер."), _adminKey);
            _allowUnsigned = EditorGUILayout.Toggle(new GUIContent("Разрешить без ключа", "Только для локального mock или доверенного тестового сервера."), _allowUnsigned);
            _episodeId = EditorGUILayout.TextField(new GUIContent("ID эпизода", "ID серии/эпизода для загрузки JSON и публикации."), _episodeId);
            _episodeJson = EditorGUILayout.ObjectField(new GUIContent("JSON эпизода", "TextAsset с JSON серии."), _episodeJson, typeof(TextAsset), false) as TextAsset;

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Получить каталог", "Загружает каталог с сервера для проверки.")))
                FetchCatalog();
            if (GUILayout.Button(new GUIContent("Загрузить JSON", "Отправляет выбранный JSON эпизода в backend.")))
                UploadEpisodeJson();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Опубликовать эпизод", "Открывает эпизод игрокам.")))
                SetPublished(true);
            if (GUILayout.Button(new GUIContent("Скрыть эпизод", "Скрывает эпизод из каталога.")))
                SetPublished(false);
            EditorGUILayout.EndHorizontal();
        }

        DrawLastResponse();
        EditorGUILayout.EndScrollView();
    }

    private void FetchCatalog()
    {
        StartRequest(CurrentBackendCatalogClient.FetchCatalog(OnRequestFinished, _baseUrl, _adminKey, _allowUnsigned));
    }

    private void UploadEpisodeJson()
    {
        string json = _episodeJson != null ? _episodeJson.text : "";
        StartRequest(CurrentBackendCatalogClient.UploadEpisodeContent(_episodeId, json, OnRequestFinished, _baseUrl, _adminKey, _allowUnsigned));
    }

    private void SetPublished(bool published)
    {
        StartRequest(CurrentBackendCatalogClient.SetEpisodePublished(_episodeId, published, OnRequestFinished, _baseUrl, _adminKey, _allowUnsigned));
    }

    private void StartRequest(IEnumerator request)
    {
        SavePrefs();
        _isBusy = true;
        _lastResponse = "Запрос отправлен.";
        Repaint();
        EditorCoroutineRunner.Start(RunRequest(request));
    }

    private IEnumerator RunRequest(IEnumerator request)
    {
        while (request != null && request.MoveNext())
            yield return request.Current;

        _isBusy = false;
        Repaint();
    }

    private void OnRequestFinished(UnityPublisherRequestResult result)
    {
        _lastResponse = result != null && result.Success
            ? "OK " + result.StatusCode + "\n" + result.Body
            : "ОШИБКА " + (result != null ? result.StatusCode.ToString() : "0") + "\n" +
              (result != null ? FirstNonEmpty(result.Error, result.Body) : "Нет результата.");
    }

    private void DrawLastResponse()
    {
        if (string.IsNullOrWhiteSpace(_lastResponse))
            return;

        EditorGUILayout.Space(8f);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(120f));
        EditorGUILayout.TextArea(_lastResponse);
        EditorGUILayout.EndScrollView();
    }

    private void SavePrefs()
    {
        new CurrentBackendCatalogPublisherPrefs
        {
            EpisodeId = _episodeId,
            BaseUrl = _baseUrl,
            AllowUnsigned = _allowUnsigned
        }.Save();
    }

    private static string FirstNonEmpty(string first, string second)
    {
        return !string.IsNullOrWhiteSpace(first) ? first : second;
    }
}
#endif
