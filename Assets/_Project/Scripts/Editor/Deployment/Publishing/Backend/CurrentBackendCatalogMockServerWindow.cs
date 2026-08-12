#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class CurrentBackendCatalogMockServerWindow : EditorWindow
{
    private static CurrentBackendCatalogMockServer _server;

    [MenuItem("VN/Выкладка/Локальный backend-сервер", priority = 23)]
    public static void Open()
    {
        GetWindow<CurrentBackendCatalogMockServerWindow>("Mock backend");
    }

    [InitializeOnLoadMethod]
    private static void RegisterCleanup()
    {
        AssemblyReloadEvents.beforeAssemblyReload += StopServer;
        EditorApplication.quitting += StopServer;
    }

    private void OnGUI()
    {
        bool running = _server != null;
        EditorGUILayout.LabelField("Локальный backend-сервер", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Статус", running ? "Запущен" : "Остановлен");
        EditorGUILayout.LabelField("Адрес сервера", running ? _server.BaseUrl : "-");
        EditorGUILayout.LabelField("Ключ администратора", CurrentBackendCatalogMockServer.DefaultAdminKey);
        EditorGUILayout.LabelField("Запросов", running ? _server.RequestCount.ToString() : "0");

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(running))
        {
            if (GUILayout.Button(new GUIContent("Запустить", "Запускает локальный backend для безопасной проверки.")))
                StartServer();
        }

        using (new EditorGUI.DisabledScope(!running))
        {
            if (GUILayout.Button(new GUIContent("Остановить", "Останавливает локальный backend.")))
                StopServer();
            if (GUILayout.Button(new GUIContent("Подставить в публикатор", "Записывает адрес mock-сервера в окно backend-публикатора.")))
                UseInPublisher();
        }
        EditorGUILayout.EndHorizontal();

        if (running && !string.IsNullOrWhiteSpace(_server.LastRequestSummary))
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(_server.LastRequestSummary, MessageType.Info);
        }
    }

    private static void StartServer()
    {
        _server = new CurrentBackendCatalogMockServer();
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

        CurrentBackendCatalogPublisherPrefs prefs = CurrentBackendCatalogPublisherPrefs.Load();
        prefs.BaseUrl = _server.BaseUrl;
        prefs.AllowUnsigned = true;
        prefs.Save();
        Debug.Log("[CurrentBackendCatalogMockServer] Адрес публикатора backend установлен: " + _server.BaseUrl);
    }
}
#endif
