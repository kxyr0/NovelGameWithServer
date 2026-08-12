using System;
using UnityEngine;

public enum AppSettingType
{
    SoundEffects = 0,
    Music = 1,
    Vibration = 2
}

public static class AppSettingsState
{
    const string SoundsDisabledKey = "VN_SETTINGS_SOUNDS_DISABLED";
    const string SoundsEnabledLegacyKey = "VN_SETTINGS_SOUNDS_ENABLED";
    const string SfxMuteKey = "VN_SFX_MUTE";
    const string MusicMuteKey = "VN_MUSIC_MUTE";
    const string VibrationDisabledKey = "VN_SETTINGS_VIBRATION_DISABLED";
    const string VibrationEnabledLegacyKey = "VN_SETTINGS_VIBRATION_ENABLED";

    public static event Action<AppSettingType, bool> Changed;

    public static bool IsEnabled(AppSettingType type)
    {
        switch (type)
        {
            case AppSettingType.SoundEffects:
                if (HasKey(SfxMuteKey))
                    return GetInt(SfxMuteKey, 0) == 0;
                return !GetDisabled(SoundsDisabledKey, SoundsEnabledLegacyKey);
            case AppSettingType.Music:
                return GetInt(MusicMuteKey, 0) == 0;
            case AppSettingType.Vibration:
                return !GetDisabled(VibrationDisabledKey, VibrationEnabledLegacyKey);
            default:
                return true;
        }
    }

    public static void SetEnabled(AppSettingType type, bool enabled)
    {
        switch (type)
        {
            case AppSettingType.SoundEffects:
                SetInt(SfxMuteKey, enabled ? 0 : 1);
                SetInt(SoundsDisabledKey, enabled ? 0 : 1);
                SetInt(SoundsEnabledLegacyKey, enabled ? 1 : 0);
                break;
            case AppSettingType.Music:
                SetInt(MusicMuteKey, enabled ? 0 : 1);
                break;
            case AppSettingType.Vibration:
                SetInt(VibrationDisabledKey, enabled ? 0 : 1);
                SetInt(VibrationEnabledLegacyKey, enabled ? 1 : 0);
                break;
        }

        Save();
        ApplyLive(type, enabled);
        Changed?.Invoke(type, enabled);
    }

    public static void RefreshLiveAudio()
    {
        ApplyLive(AppSettingType.SoundEffects, IsEnabled(AppSettingType.SoundEffects));
        ApplyLive(AppSettingType.Music, IsEnabled(AppSettingType.Music));
    }

    static void ApplyLive(AppSettingType type, bool enabled)
    {
        StoryManager story = StoryManager.Instance;
        if (type == AppSettingType.SoundEffects && story != null && story.sfxSource != null)
            story.sfxSource.mute = !enabled;

        if (type == AppSettingType.Music)
        {
            if (story != null && story.musicSource != null)
                story.musicSource.mute = !enabled;

            MainMenuMusicPlayer player = MainMenuMusicPlayer.Instance;
            if (player == null && Application.isPlaying)
                player = UnityEngine.Object.FindObjectOfType<MainMenuMusicPlayer>(true);
            player?.ApplySavedVolume();
        }
    }

    static bool GetDisabled(string disabledKey, string legacyEnabledKey)
    {
        if (HasKey(disabledKey))
            return GetInt(disabledKey, 0) != 0;
        return HasKey(legacyEnabledKey) && GetInt(legacyEnabledKey, 1) == 0;
    }

    static bool HasKey(string key)
    {
        try { return PlayerPrefs.HasKey(key); }
        catch (Exception exception)
        {
            Debug.LogWarning($"[AppSettings] Failed to read '{key}': {exception.Message}");
            return false;
        }
    }

    static int GetInt(string key, int fallback)
    {
        try { return PlayerPrefs.GetInt(key, fallback); }
        catch (Exception exception)
        {
            Debug.LogWarning($"[AppSettings] Failed to read '{key}': {exception.Message}");
            return fallback;
        }
    }

    static void SetInt(string key, int value)
    {
        try { PlayerPrefs.SetInt(key, value); }
        catch (Exception exception) { Debug.LogWarning($"[AppSettings] Failed to save '{key}': {exception.Message}"); }
    }

    static void Save()
    {
        try { PlayerPrefs.Save(); }
        catch (Exception exception) { Debug.LogWarning($"[AppSettings] Failed to save preferences: {exception.Message}"); }
    }
}
