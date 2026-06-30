using System;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum SettingsSocialNetwork
{
    Telegram = 0,
    VK = 1,
    Instagram = 2,
    TikTok = 3
}

[CreateAssetMenu(fileName = "Settings Social Links", menuName = "Nocturne/UI/Settings Social Links")]
public sealed class SettingsSocialLinksConfig : ScriptableObject
{
    [SerializeField] private string _telegramUrl = "";
    [SerializeField] private string _vkUrl = "";
    [SerializeField] private string _instagramUrl = "";
    [SerializeField] private string _tikTokUrl = "";

    public string TelegramUrl => _telegramUrl;
    public string VkUrl => _vkUrl;
    public string InstagramUrl => _instagramUrl;
    public string TikTokUrl => _tikTokUrl;

    public string GetUrl(SettingsSocialNetwork network)
    {
        switch (network)
        {
            case SettingsSocialNetwork.Telegram:
                return _telegramUrl;
            case SettingsSocialNetwork.VK:
                return _vkUrl;
            case SettingsSocialNetwork.Instagram:
                return _instagramUrl;
            case SettingsSocialNetwork.TikTok:
                return _tikTokUrl;
            default:
                return "";
        }
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Settings Screen Controller")]
public sealed class SettingsScreenController : MonoBehaviour
{
    private const string NotificationsDisabledKey = "VN_SETTINGS_NOTIFICATIONS_DISABLED";
    private const string SoundsDisabledKey = "VN_SETTINGS_SOUNDS_DISABLED";
    private const string VibrationDisabledKey = "VN_SETTINGS_VIBRATION_DISABLED";
    private const string LegacyNotificationsEnabledKey = "VN_SETTINGS_NOTIFICATIONS_ENABLED";
    private const string LegacySoundsEnabledKey = "VN_SETTINGS_SOUNDS_ENABLED";
    private const string LegacyVibrationEnabledKey = "VN_SETTINGS_VIBRATION_ENABLED";
    private const string MusicVolumeKey = "VN_MUSIC_VOL";
    private const string MusicMuteKey = "VN_MUSIC_MUTE";
    private const string SfxMuteKey = "VN_SFX_MUTE";
    private const string RemoteGraphCacheFolderName = "remote_episode_graphs";

    [Header("Navigation")]
    [SerializeField] private StoryScreenNavigator _screenNavigator;
    [SerializeField] private string _profileScreenId = "Profile";
    [SerializeField] private string _closeScreenId = "MainScreen";

    [Header("Social Links")]
    [SerializeField] private SettingsSocialLinksConfig _socialLinks;

    [Header("Buttons")]
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _loginButton;
    [SerializeField] private Button _clearCacheButton;
    [SerializeField] private Button _profileButton;
    [SerializeField] private Button _telegramButton;
    [SerializeField] private Button _vkButton;
    [SerializeField] private Button _instagramButton;
    [SerializeField] private Button _tikTokButton;
    [SerializeField] private Button _supportButton;
    [SerializeField] private Button _privacyPolicyButton;
    [SerializeField] private Button _termsOfUseButton;
    [SerializeField] private Button _quitButton;

    [Header("State Controls")]
    [SerializeField] private GameObjectToggle _notificationsToggle;
    [SerializeField] private GameObjectToggle _soundsToggle;
    [SerializeField] private GameObjectToggle _vibrationToggle;
    [SerializeField] private bool _syncToggleVisualsOnEnable = true;
    [SerializeField] private bool _applySoundStateOnEnable = true;

    [Header("Audio")]
    [SerializeField] private MainMenuMusicPlayer _mainMenuMusicPlayer;
    [SerializeField] private StoryManager _storyManager;
    [SerializeField] private AudioSource[] _audioSourcesToMute = Array.Empty<AudioSource>();
    [SerializeField] private bool _controlAudioListenerVolume;
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private string _audioMixerVolumeParameter = "";
    [SerializeField] private float _audioMixerEnabledDecibels = 0f;
    [SerializeField] private float _audioMixerDisabledDecibels = -80f;

    [Header("Cache")]
    [SerializeField] private bool _clearRemoteEpisodeGraphCache = true;
    [SerializeField] private bool _clearUnityCache = true;
    [SerializeField] private bool _clearTemporaryCachePath;
    [SerializeField] private bool _clearNetworkProgressMemoryCache;
    [SerializeField] private string[] _extraPlayerPrefsCacheKeys = Array.Empty<string>();
    [SerializeField] private string[] _extraCacheFoldersInPersistentDataPath = Array.Empty<string>();

    [Header("Messages")]
    [SerializeField] private bool _showToastMessages = true;
    [SerializeField] private string _cacheClearedMessage = "\u041a\u044d\u0448 \u043e\u0447\u0438\u0449\u0435\u043d.";
    [SerializeField] private string _loginUnavailableMessage = "\u0412\u0445\u043e\u0434 \u0432 \u0430\u043a\u043a\u0430\u0443\u043d\u0442 \u043f\u043e\u043a\u0430 \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u0435\u043d.";
    [SerializeField] private string _emptySocialLinkMessage = "\u0421\u0441\u044b\u043b\u043a\u0430 \u043f\u043e\u043a\u0430 \u043d\u0435 \u0437\u0430\u0434\u0430\u043d\u0430.";

    public static bool NotificationsDisabled => SafeGetDisabledPreference(
        NotificationsDisabledKey,
        LegacyNotificationsEnabledKey,
        false);
    public static bool SoundsDisabled => SafeGetBool(
        SoundsDisabledKey,
        SafeGetInt(MusicMuteKey, 0) == 1 ||
        SafeGetInt(SfxMuteKey, 0) == 1 ||
        SafeGetDisabledPreference(SoundsDisabledKey, LegacySoundsEnabledKey, false));
    public static bool VibrationDisabled => SafeGetDisabledPreference(
        VibrationDisabledKey,
        LegacyVibrationEnabledKey,
        false);
    public static bool NotificationsEnabled => !NotificationsDisabled;
    public static bool SoundsEnabled => !SoundsDisabled;
    public static bool VibrationEnabled => !VibrationDisabled;

    private void OnEnable()
    {
        BindControls();

        if (_syncToggleVisualsOnEnable)
            SyncToggleVisuals();

        if (_applySoundStateOnEnable)
            ApplySoundDisabled(SoundsDisabled);
    }

    private void OnDisable()
    {
        UnbindControls();
    }

    private void OnValidate()
    {
        _profileScreenId = UIScreenState.NormalizeScreenId(_profileScreenId);
        _closeScreenId = UIScreenState.NormalizeScreenId(_closeScreenId);
        _audioSourcesToMute ??= Array.Empty<AudioSource>();
        _extraPlayerPrefsCacheKeys ??= Array.Empty<string>();
        _extraCacheFoldersInPersistentDataPath ??= Array.Empty<string>();
        _audioMixerVolumeParameter = _audioMixerVolumeParameter != null ? _audioMixerVolumeParameter.Trim() : "";
    }

    public void SyncToggleVisuals()
    {
        SetToggleWithoutNotify(_notificationsToggle, NotificationsDisabled);
        SetToggleWithoutNotify(_soundsToggle, SoundsDisabled);
        SetToggleWithoutNotify(_vibrationToggle, VibrationDisabled);
    }

    public void SetNotificationsOff(bool off)
    {
        SafeSetBool(NotificationsDisabledKey, off);
        SafeSetBool(LegacyNotificationsEnabledKey, !off);
        SetToggleWithoutNotify(_notificationsToggle, off);
    }

    public void SetNotificationsEnabled(bool enabled)
    {
        SetNotificationsOff(!enabled);
    }

    public void ToggleNotifications()
    {
        SetNotificationsOff(!NotificationsDisabled);
    }

    public void SetSoundsOff(bool off)
    {
        SafeSetBool(SoundsDisabledKey, off);
        SafeSetBool(LegacySoundsEnabledKey, !off);
        SafeSetInt(MusicMuteKey, off ? 1 : 0);
        SafeSetInt(SfxMuteKey, off ? 1 : 0);
        ApplySoundDisabled(off);
        SetToggleWithoutNotify(_soundsToggle, off);
    }

    public void SetSoundsEnabled(bool enabled)
    {
        SetSoundsOff(!enabled);
    }

    public void ToggleSounds()
    {
        SetSoundsOff(!SoundsDisabled);
    }

    public void SetVibrationOff(bool off)
    {
        SafeSetBool(VibrationDisabledKey, off);
        SafeSetBool(LegacyVibrationEnabledKey, !off);
        SetToggleWithoutNotify(_vibrationToggle, off);
    }

    public void SetVibrationEnabled(bool enabled)
    {
        SetVibrationOff(!enabled);
    }

    public void ToggleVibration()
    {
        SetVibrationOff(!VibrationDisabled);
    }

    public void OpenProfile()
    {
        OpenNavigatorScreen(_profileScreenId, "Profile");
    }

    public void CloseSettings()
    {
        OpenNavigatorScreen(_closeScreenId, "Close");
    }

    private void OpenNavigatorScreen(string screenId, string label)
    {
        screenId = UIScreenState.NormalizeScreenId(screenId);
        if (_screenNavigator == null || string.IsNullOrWhiteSpace(screenId))
        {
            Debug.LogWarning($"[SettingsScreenController] {label} screen cannot be opened: navigator or screen id is not assigned.", this);
            return;
        }

        if (!_screenNavigator.OpenScreen(screenId))
            Debug.LogWarning($"[SettingsScreenController] {label} screen '{screenId}' is not registered in StoryScreenNavigator.", this);
    }

    public void ClearCache()
    {
        if (_clearRemoteEpisodeGraphCache)
            DeleteDirectorySafe(Path.Combine(Application.persistentDataPath, RemoteGraphCacheFolderName));

        if (_clearTemporaryCachePath)
            ClearDirectoryContentsSafe(Application.temporaryCachePath);

        ClearExtraCacheFolders();
        ClearExtraPlayerPrefsKeys();

        if (_clearUnityCache)
            ClearUnityCacheSafe();

        if (_clearNetworkProgressMemoryCache)
            NetworkManager.ClearLocalProgressCache(clearPendingSync: false);

        SafeSavePrefs();
        ShowToast(_cacheClearedMessage);
    }

    public void LoginNotAvailable()
    {
        ShowToast(_loginUnavailableMessage);
    }

    public void Login()
    {
        LoginNotAvailable();
    }

    public void OpenTelegram()
    {
        OpenSocial(SettingsSocialNetwork.Telegram);
    }

    public void OpenVk()
    {
        OpenSocial(SettingsSocialNetwork.VK);
    }

    public void OpenVK()
    {
        OpenVk();
    }

    public void OpenInstagram()
    {
        OpenSocial(SettingsSocialNetwork.Instagram);
    }

    public void OpenTikTok()
    {
        OpenSocial(SettingsSocialNetwork.TikTok);
    }

    public void OpenSocial(SettingsSocialNetwork network)
    {
        string url = _socialLinks != null ? _socialLinks.GetUrl(network) : "";
        OpenUrl(url);
    }

    public void OpenSupport()
    {
    }

    public void OpenPrivacyPolicy()
    {
    }

    public void OpenTermsOfUse()
    {
    }

    public void QuitGame()
    {
        SafeSavePrefs();

#if UNITY_EDITOR
        if (Application.isPlaying)
            EditorApplication.ExitPlaymode();
        else
            EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ExitGame()
    {
        QuitGame();
    }

    private void ApplySoundDisabled(bool disabled)
    {
        if (_mainMenuMusicPlayer != null)
            _mainMenuMusicPlayer.ApplyVolume(SafeGetFloat(MusicVolumeKey, 1f), disabled);

        if (_storyManager != null)
        {
            if (_storyManager.musicSource != null)
                _storyManager.musicSource.mute = disabled;

            if (_storyManager.sfxSource != null)
                _storyManager.sfxSource.mute = disabled;
        }

        if (_controlAudioListenerVolume)
            AudioListener.volume = disabled ? 0f : 1f;

        if (_audioSourcesToMute != null)
        {
            for (int i = 0; i < _audioSourcesToMute.Length; i++)
            {
                AudioSource source = _audioSourcesToMute[i];
                if (source != null)
                    source.mute = disabled;
            }
        }

        if (_audioMixer != null && !string.IsNullOrWhiteSpace(_audioMixerVolumeParameter))
            _audioMixer.SetFloat(_audioMixerVolumeParameter, disabled ? _audioMixerDisabledDecibels : _audioMixerEnabledDecibels);
    }

    private void OpenUrl(string url)
    {
        url = NormalizeUrl(url);
        if (string.IsNullOrWhiteSpace(url))
        {
            ShowToast(_emptySocialLinkMessage);
            return;
        }

        Application.OpenURL(url);
    }

    private void ClearExtraCacheFolders()
    {
        if (_extraCacheFoldersInPersistentDataPath == null)
            return;

        string persistentRoot = GetFullPathSafe(Application.persistentDataPath);
        for (int i = 0; i < _extraCacheFoldersInPersistentDataPath.Length; i++)
        {
            string relativePath = _extraCacheFoldersInPersistentDataPath[i];
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                continue;

            string path = GetFullPathSafe(Path.Combine(Application.persistentDataPath, relativePath));
            if (!IsPathInsideRoot(path, persistentRoot))
                continue;

            DeleteDirectorySafe(path);
        }
    }

    private void ClearExtraPlayerPrefsKeys()
    {
        if (_extraPlayerPrefsCacheKeys == null)
            return;

        for (int i = 0; i < _extraPlayerPrefsCacheKeys.Length; i++)
        {
            string key = _extraPlayerPrefsCacheKeys[i];
            if (!string.IsNullOrWhiteSpace(key))
                SafeDeleteKey(key.Trim());
        }
    }

    private void BindControls()
    {
        UnbindControls();

        BindToggle(_notificationsToggle, SetNotificationsOff);
        BindToggle(_soundsToggle, SetSoundsOff);
        BindToggle(_vibrationToggle, SetVibrationOff);

        BindButton(_closeButton, CloseSettings);
        BindButton(_loginButton, Login);
        BindButton(_clearCacheButton, ClearCache);
        BindButton(_profileButton, OpenProfile);
        BindButton(_telegramButton, OpenTelegram);
        BindButton(_vkButton, OpenVK);
        BindButton(_instagramButton, OpenInstagram);
        BindButton(_tikTokButton, OpenTikTok);
        BindButton(_supportButton, OpenSupport);
        BindButton(_privacyPolicyButton, OpenPrivacyPolicy);
        BindButton(_termsOfUseButton, OpenTermsOfUse);
        BindButton(_quitButton, QuitGame);
    }

    private void UnbindControls()
    {
        UnbindToggle(_notificationsToggle, SetNotificationsOff);
        UnbindToggle(_soundsToggle, SetSoundsOff);
        UnbindToggle(_vibrationToggle, SetVibrationOff);

        UnbindButton(_closeButton, CloseSettings);
        UnbindButton(_loginButton, Login);
        UnbindButton(_clearCacheButton, ClearCache);
        UnbindButton(_profileButton, OpenProfile);
        UnbindButton(_telegramButton, OpenTelegram);
        UnbindButton(_vkButton, OpenVK);
        UnbindButton(_instagramButton, OpenInstagram);
        UnbindButton(_tikTokButton, OpenTikTok);
        UnbindButton(_supportButton, OpenSupport);
        UnbindButton(_privacyPolicyButton, OpenPrivacyPolicy);
        UnbindButton(_termsOfUseButton, OpenTermsOfUse);
        UnbindButton(_quitButton, QuitGame);
    }

    private static void BindToggle(GameObjectToggle toggle, UnityEngine.Events.UnityAction<bool> action)
    {
        if (toggle == null || action == null)
            return;

        toggle.RemoveValueChangedListener(action);
        toggle.AddValueChangedListener(action);
    }

    private static void UnbindToggle(GameObjectToggle toggle, UnityEngine.Events.UnityAction<bool> action)
    {
        if (toggle != null && action != null)
            toggle.RemoveValueChangedListener(action);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null && action != null)
            button.onClick.RemoveListener(action);
    }

    private static void SetToggleWithoutNotify(GameObjectToggle toggle, bool enabled)
    {
        if (toggle != null)
            toggle.SetIsOnWithoutNotify(enabled);
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";

        url = url.Trim();
        return url.Contains("://") ? url : "https://" + url;
    }

    private static string GetFullPathSafe(string path)
    {
        try
        {
            return string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsScreenController] Failed to resolve path '{path}': {exception.Message}");
            return "";
        }
    }

    private static bool IsPathInsideRoot(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
            return false;

        root = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteDirectorySafe(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsScreenController] Failed to delete cache directory '{path}': {exception.Message}");
        }
    }

    private static void ClearDirectoryContentsSafe(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            string[] files = Directory.GetFiles(path);
            for (int i = 0; i < files.Length; i++)
                File.Delete(files[i]);

            string[] directories = Directory.GetDirectories(path);
            for (int i = 0; i < directories.Length; i++)
                Directory.Delete(directories[i], true);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsScreenController] Failed to clear cache directory '{path}': {exception.Message}");
        }
    }

    private static void ClearUnityCacheSafe()
    {
        try
        {
            Caching.ClearCache();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsScreenController] Failed to clear Unity cache: {exception.Message}");
        }
    }

    private static bool SafeGetBool(string key, bool defaultValue)
    {
        try
        {
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsScreenController] Failed to read '{key}': {exception.Message}");
            return defaultValue;
        }
    }

    private static bool SafeGetDisabledPreference(string disabledKey, string legacyEnabledKey, bool defaultDisabled)
    {
        try
        {
            if (PlayerPrefs.HasKey(disabledKey))
                return PlayerPrefs.GetInt(disabledKey, defaultDisabled ? 1 : 0) != 0;

            if (PlayerPrefs.HasKey(legacyEnabledKey))
                return PlayerPrefs.GetInt(legacyEnabledKey, defaultDisabled ? 0 : 1) == 0;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsScreenController] Failed to read setting '{disabledKey}': {exception.Message}");
        }

        return defaultDisabled;
    }

    private static float SafeGetFloat(string key, float defaultValue)
    {
        try
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(key, defaultValue));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsScreenController] Failed to read '{key}': {exception.Message}");
            return defaultValue;
        }
    }

    private static int SafeGetInt(string key, int defaultValue)
    {
        try
        {
            return PlayerPrefs.GetInt(key, defaultValue);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsScreenController] Failed to read '{key}': {exception.Message}");
            return defaultValue;
        }
    }

    private static void SafeSetBool(string key, bool value)
    {
        SafeSetInt(key, value ? 1 : 0);
    }

    private static void SafeSetInt(string key, int value)
    {
        try
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsScreenController] Failed to save '{key}': {exception.Message}");
        }
    }

    private static void SafeDeleteKey(string key)
    {
        try
        {
            PlayerPrefs.DeleteKey(key);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsScreenController] Failed to delete '{key}': {exception.Message}");
        }
    }

    private static void SafeSavePrefs()
    {
        try
        {
            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SettingsScreenController] Failed to save PlayerPrefs: {exception.Message}");
        }
    }

    private void ShowToast(string message)
    {
        if (!_showToastMessages || string.IsNullOrWhiteSpace(message))
            return;

        ToastManager.Instance?.ShowSystemMessage(message);
    }
}
