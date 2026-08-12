using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Settings/Cache Clear Button")]
public sealed class SettingsCacheClearButton : MonoBehaviour
{
    const string RemoteStoriesFolder = "remote_episode_graphs";

    [SerializeField] Button _button;
    [SerializeField] TMP_Text _statusText;
    [SerializeField] bool _clearDownloadedStoryCache = true;
    [SerializeField] bool _clearUnityDownloadCache = true;
    [SerializeField] bool _clearTemporaryFiles = true;
    [SerializeField] string _successMessage = "Кэш очищен";
    [SerializeField] string _partialFailureMessage = "Не весь кэш удалось очистить";

    void Awake()
    {
        if (_button == null)
            _button = GetComponent<Button>();
    }

    void OnEnable()
    {
        if (_button == null)
            return;
        _button.onClick.RemoveListener(ClearCache);
        _button.onClick.AddListener(ClearCache);
    }

    void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(ClearCache);
    }

    public void ClearCache()
    {
        bool success = true;

        if (_clearDownloadedStoryCache)
        {
            string storyCache = Path.Combine(Application.persistentDataPath, RemoteStoriesFolder);
            success &= DeleteChildDirectory(storyCache, Application.persistentDataPath);
        }

        if (_clearTemporaryFiles)
            success &= ClearDirectoryContents(Application.temporaryCachePath);

        if (_clearUnityDownloadCache)
            success &= ClearUnityCache();

        string message = success ? _successMessage : _partialFailureMessage;
        if (_statusText != null)
            _statusText.text = message;
        ToastManager.Instance?.ShowSystemMessage(message);
    }

    static bool DeleteChildDirectory(string path, string allowedRoot)
    {
        try
        {
            string fullPath = Normalize(path);
            string fullRoot = Normalize(allowedRoot);
            if (!IsInside(fullPath, fullRoot))
                return false;

            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsCache] Failed to delete '{path}': {exception.Message}");
            return false;
        }
    }

    static bool ClearDirectoryContents(string path)
    {
        try
        {
            string fullPath = Normalize(path);
            string driveRoot = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(fullPath) || string.Equals(fullPath, driveRoot, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!Directory.Exists(fullPath))
                return true;

            foreach (string file in Directory.GetFiles(fullPath))
                File.Delete(file);
            foreach (string directory in Directory.GetDirectories(fullPath))
                Directory.Delete(directory, true);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsCache] Failed to clear '{path}': {exception.Message}");
            return false;
        }
    }

    static bool ClearUnityCache()
    {
        try
        {
            return Caching.ClearCache();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsCache] Unity cache could not be cleared: {exception.Message}");
            return false;
        }
    }

    static string Normalize(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? ""
            : Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    static bool IsInside(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
            return false;
        string prefix = root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
