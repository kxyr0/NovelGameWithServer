#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class ContentReleaseMockServerWindow : EditorWindow
{
    private static ContentReleaseMockServer _server;
    private Vector2 _scroll;

    [MenuItem("VN/Выкладка/Локальный сервер релизов", priority = 21)]
    public static void Open()
    {
        GetWindow<ContentReleaseMockServerWindow>("Mock релизов");
    }

    [InitializeOnLoadMethod]
    private static void RegisterCleanup()
    {
        AssemblyReloadEvents.beforeAssemblyReload += StopServer;
        EditorApplication.quitting += StopServer;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Локальный сервер релизов", EditorStyles.boldLabel);
        bool running = _server != null;
        EditorGUILayout.LabelField("Статус", running ? "Запущен" : "Остановлен");
        EditorGUILayout.LabelField("Адрес сервера", running ? _server.BaseUrl : "-");
        EditorGUILayout.LabelField("Ключ администратора", ContentReleaseMockServer.DefaultAdminKey);
        EditorGUILayout.LabelField("Запросов", running ? _server.RequestCount.ToString() : "0");
        EditorGUILayout.LabelField("Сохранённых релизов", running ? _server.ReleaseCount.ToString() : "0");

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(running))
        {
            if (GUILayout.Button(new GUIContent("Запустить", "Запускает локальный сервер релизов для безопасной проверки.")))
                StartServer();
        }

        using (new EditorGUI.DisabledScope(!running))
        {
            if (GUILayout.Button(new GUIContent("Остановить", "Останавливает локальный сервер релизов.")))
                StopServer();
            if (GUILayout.Button(new GUIContent("Подставить в публикатор", "Записывает адрес mock-сервера в окно публикации релизов.")))
                UseInPublisher();
        }
        EditorGUILayout.EndHorizontal();

        DrawLastRequest();
    }

    private void OnDestroy()
    {
        Repaint();
    }

    private static void StartServer()
    {
        _server = new ContentReleaseMockServer();
        _server.Start();
        UseInPublisher();
    }

    private static void StopServer()
    {
        _server?.Dispose();
        _server = null;
    }

    private static void UseInPublisher()
    {
        if (_server == null)
            return;

        ContentReleasePublisherPrefs prefs = ContentReleasePublisherPrefs.Load();
        prefs.BaseUrl = _server.BaseUrl;
        prefs.AllowUnsigned = true;
        prefs.EnvironmentId = DeploymentEnvironmentIds.Stage;
        prefs.Status = ContentReleaseStatus.Staging;
        prefs.Save();
        Debug.Log("[ContentReleaseMockServer] Адрес публикатора релизов установлен: " + _server.BaseUrl);
    }

    private void DrawLastRequest()
    {
        if (_server == null || string.IsNullOrWhiteSpace(_server.LastRequestSummary))
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Последний запрос", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(60f));
        EditorGUILayout.TextArea(_server.LastRequestSummary);
        EditorGUILayout.EndScrollView();
    }
}
#endif
