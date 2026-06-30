using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    [Header("Audio")]
    public Slider musicSlider;
    public Slider sfxSlider;
    public Toggle musicToggle;
    public Toggle sfxToggle;
    public TMP_Text musicValueText;
    public TMP_Text sfxValueText;

    [Header("Progress")]
    public Button resetButton;
    public GameObject resetConfirmPanel;
    public Button confirmResetButton;
    public Button cancelResetButton;

    [Header("Player Name")]
    public Button changeNameButton;

    const string MUSIC_VOL_KEY = "VN_MUSIC_VOL";
    const string SFX_VOL_KEY = "VN_SFX_VOL";
    const string MUSIC_MUTE_KEY = "VN_MUSIC_MUTE";
    const string SFX_MUTE_KEY = "VN_SFX_MUTE";
    const string ProgressResetMessage = "\u041f\u0440\u043e\u0433\u0440\u0435\u0441\u0441 \u0441\u0431\u0440\u043e\u0448\u0435\u043d";
    const string NameLockedMessage = "\u0412 \u044d\u0442\u043e\u0439 \u0438\u0441\u0442\u043e\u0440\u0438\u0438 \u0438\u043c\u044f \u043d\u0435\u043b\u044c\u0437\u044f \u0438\u0437\u043c\u0435\u043d\u0438\u0442\u044c";

    void Start()
    {
        float musicVol = SafeGetFloat(MUSIC_VOL_KEY, 1f);
        float sfxVol = SafeGetFloat(SFX_VOL_KEY, 1f);
        bool musicMute = SafeGetInt(MUSIC_MUTE_KEY, 0) == 1;
        bool sfxMute = SafeGetInt(SFX_MUTE_KEY, 0) == 1;

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(musicVol);
            musicSlider.onValueChanged.AddListener(OnMusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(sfxVol);
            sfxSlider.onValueChanged.AddListener(OnSfxVolume);
        }

        if (musicToggle != null)
        {
            musicToggle.SetIsOnWithoutNotify(musicMute);
            musicToggle.onValueChanged.AddListener(OnMusicMute);
        }

        if (sfxToggle != null)
        {
            sfxToggle.SetIsOnWithoutNotify(sfxMute);
            sfxToggle.onValueChanged.AddListener(OnSfxMute);
        }

        UpdateLabels(musicVol, sfxVol);

        if (resetButton != null) resetButton.onClick.AddListener(ShowResetConfirm);
        if (confirmResetButton != null) confirmResetButton.onClick.AddListener(ResetProgress);
        if (cancelResetButton != null) cancelResetButton.onClick.AddListener(HideResetConfirm);
        if (changeNameButton != null) changeNameButton.onClick.AddListener(ChangeName);

        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);

        ApplyAudio(musicVol, sfxVol, musicMute, sfxMute);
    }

    void OnDestroy()
    {
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxVolume);
        if (musicToggle != null) musicToggle.onValueChanged.RemoveListener(OnMusicMute);
        if (sfxToggle != null) sfxToggle.onValueChanged.RemoveListener(OnSfxMute);
        if (resetButton != null) resetButton.onClick.RemoveListener(ShowResetConfirm);
        if (confirmResetButton != null) confirmResetButton.onClick.RemoveListener(ResetProgress);
        if (cancelResetButton != null) cancelResetButton.onClick.RemoveListener(HideResetConfirm);
        if (changeNameButton != null) changeNameButton.onClick.RemoveListener(ChangeName);
    }

    void OnMusicVolume(float val)
    {
        val = Mathf.Clamp01(val);
        SafeSetFloat(MUSIC_VOL_KEY, val);
        ApplyAudio();
        UpdateLabels(val, SafeGetFloat(SFX_VOL_KEY, 1f));
    }

    void OnSfxVolume(float val)
    {
        val = Mathf.Clamp01(val);
        SafeSetFloat(SFX_VOL_KEY, val);
        ApplyAudio();
        UpdateLabels(SafeGetFloat(MUSIC_VOL_KEY, 1f), val);
    }

    void OnMusicMute(bool muted)
    {
        SafeSetInt(MUSIC_MUTE_KEY, muted ? 1 : 0);
        ApplyAudio();
    }

    void OnSfxMute(bool muted)
    {
        SafeSetInt(SFX_MUTE_KEY, muted ? 1 : 0);
        ApplyAudio();
    }

    void ApplyAudio(float musicVol = -1f, float sfxVol = -1f, bool? musicMute = null, bool? sfxMute = null)
    {
        if (musicVol < 0f) musicVol = SafeGetFloat(MUSIC_VOL_KEY, 1f);
        if (sfxVol < 0f) sfxVol = SafeGetFloat(SFX_VOL_KEY, 1f);
        if (musicMute == null) musicMute = SafeGetInt(MUSIC_MUTE_KEY, 0) == 1;
        if (sfxMute == null) sfxMute = SafeGetInt(SFX_MUTE_KEY, 0) == 1;

        musicVol = Mathf.Clamp01(musicVol);
        sfxVol = Mathf.Clamp01(sfxVol);

        var sm = StoryManager.Instance;
        if (sm != null)
        {
            if (sm.musicSource != null)
                sm.musicSource.volume = musicMute.Value ? 0f : musicVol;

            if (sm.sfxSource != null)
                sm.sfxSource.volume = sfxMute.Value ? 0f : sfxVol;
        }

        var menuMusicPlayer = MainMenuMusicPlayer.Instance;
        if (menuMusicPlayer == null)
            menuMusicPlayer = FindObjectOfType<MainMenuMusicPlayer>(true);

        if (menuMusicPlayer != null)
            menuMusicPlayer.ApplyVolume(musicVol, musicMute.Value);
    }

    void UpdateLabels(float musicVol, float sfxVol)
    {
        if (musicValueText != null)
            musicValueText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(musicVol) * 100f)}%";

        if (sfxValueText != null)
            sfxValueText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(sfxVol) * 100f)}%";
    }

    void ShowResetConfirm()
    {
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(true);
    }

    void HideResetConfirm()
    {
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }

    void ResetProgress()
    {
        StoryManager storyManager = StoryManager.Instance;
        if (storyManager != null)
        {
            storyManager.StopAllCoroutines();
            storyManager.CloseEndPanel();
        }

        StoryProgressResetUtility.ResetLocalProgress(
            storyManager != null ? storyManager.storyData : null,
            storyManager != null ? storyManager.CurrentStoryId : "");
        HideResetConfirm();

        ToastManager.Instance?.ShowSystemMessage(ProgressResetMessage);
        SafeLoadScene(0);
    }

    void ChangeName()
    {
        var graph = StoryManager.Instance?.storyGraph;
        if (graph != null && !graph.allowNameChange)
        {
            ToastManager.Instance?.ShowSystemMessage(NameLockedMessage);
            return;
        }

        PlayerNameInputUI.Instance?.Show(forceShow: true);
    }

    static float SafeGetFloat(string key, float defaultValue)
    {
        try
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(key, defaultValue));
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"SettingsPanel: failed to load '{key}': {exception.Message}");
            return defaultValue;
        }
    }

    static int SafeGetInt(string key, int defaultValue)
    {
        try
        {
            return PlayerPrefs.GetInt(key, defaultValue);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"SettingsPanel: failed to load '{key}': {exception.Message}");
            return defaultValue;
        }
    }

    static void SafeSetFloat(string key, float value)
    {
        try
        {
            PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"SettingsPanel: failed to save '{key}': {exception.Message}");
        }
    }

    static void SafeSetInt(string key, int value)
    {
        try
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"SettingsPanel: failed to save '{key}': {exception.Message}");
        }
    }

    static void SafeDeleteKey(string key)
    {
        try
        {
            PlayerPrefs.DeleteKey(key);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"SettingsPanel: failed to delete '{key}': {exception.Message}");
        }
    }

    static void SafeSavePrefs()
    {
        try
        {
            PlayerPrefs.Save();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("SettingsPanel: failed to save PlayerPrefs after reset: " + exception.Message);
        }
    }

    static void SafeLoadScene(int sceneIndex)
    {
        try
        {
            SceneManager.LoadScene(sceneIndex);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"SettingsPanel: failed to reload scene {sceneIndex}: {exception.Message}");
        }
    }
}
