#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class NocturnalServerToolsWindow
{
    private static ContentReleaseMockServer _releaseMockServer;
    private static CurrentBackendCatalogMockServer _currentBackendMockServer;

    private static bool IsReleaseMockRunning => _releaseMockServer != null;
    private static bool IsCurrentBackendMockRunning => _currentBackendMockServer != null;

    private static void StopAllServers()
    {
        StopReleaseMockServer();
        StopCurrentBackendMockServer();
    }

    private static void StartReleaseMockServer()
    {
        if (_releaseMockServer != null)
            return;

        _releaseMockServer = new ContentReleaseMockServer();
        _releaseMockServer.Start();
        UseReleaseMockInPublisher();
    }

    private static void StopReleaseMockServer()
    {
        _releaseMockServer?.Dispose();
        _releaseMockServer = null;
    }

    private static void UseReleaseMockInPublisher()
    {
        if (_releaseMockServer == null)
            return;

        ContentReleasePublisherPrefs prefs = ContentReleasePublisherPrefs.Load();
        prefs.BaseUrl = _releaseMockServer.BaseUrl;
        prefs.AllowUnsigned = true;
        prefs.EnvironmentId = DeploymentEnvironmentIds.Stage;
        prefs.Status = ContentReleaseStatus.Staging;
        prefs.Save();
        Debug.Log("[NocturnalServerTools] Mock-адрес публикатора релизов: " + _releaseMockServer.BaseUrl);
    }

    private static void StartCurrentBackendMockServer()
    {
        if (_currentBackendMockServer != null)
            return;

        _currentBackendMockServer = new CurrentBackendCatalogMockServer();
        _currentBackendMockServer.Start();
        UseCurrentBackendMockInPublisher();
    }

    private static void StopCurrentBackendMockServer()
    {
        _currentBackendMockServer?.Dispose();
        _currentBackendMockServer = null;
    }

    private static void UseCurrentBackendMockInPublisher()
    {
        if (_currentBackendMockServer == null)
            return;

        CurrentBackendCatalogPublisherPrefs prefs = CurrentBackendCatalogPublisherPrefs.Load();
        prefs.BaseUrl = _currentBackendMockServer.BaseUrl;
        prefs.AllowUnsigned = true;
        prefs.Save();
        Debug.Log("[NocturnalServerTools] Mock-адрес текущего backend: " + _currentBackendMockServer.BaseUrl);
    }
}
#endif
