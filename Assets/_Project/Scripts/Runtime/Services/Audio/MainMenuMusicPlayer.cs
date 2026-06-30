using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
[AddComponentMenu("Novel Template/Menu/Main Menu Music Player")]
public sealed class MainMenuMusicPlayer : MonoBehaviour
{
    private const string MusicVolumeKey = "VN_MUSIC_VOL";
    private const string MusicMuteKey = "VN_MUSIC_MUTE";

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _menuMusic;
    [SerializeField] private AudioMixerGroup _outputAudioMixerGroup;

    [Header("Playback")]
    [SerializeField] private bool _playOnStart = true;
    [SerializeField] private bool _loop = true;
    [SerializeField] private bool _restartIfAlreadyPlaying = false;
    [SerializeField] private bool _stopWhenDisabled = true;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float _baseVolume = 1f;

    public static MainMenuMusicPlayer Instance { get; private set; }

    public AudioClip MenuMusic => _menuMusic;
    public bool IsPlaying => _audioSource != null && _audioSource.isPlaying && _audioSource.clip == _menuMusic;

    private void Awake()
    {
        RegisterInstance();
        EnsureAudioSource();
        ConfigureAudioSource();
        ApplySavedVolume();
    }

    private void Start()
    {
        if (_playOnStart)
            PlayMusic();
    }

    private void OnDisable()
    {
        if (_stopWhenDisabled)
            StopMusic();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        _baseVolume = Mathf.Clamp01(_baseVolume);

        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        ConfigureAudioSource();
    }

    public void PlayMusic()
    {
        EnsureAudioSource();

        if (_audioSource == null)
        {
            Debug.LogWarning("MainMenuMusicPlayer: AudioSource is not assigned.", this);
            return;
        }

        if (_menuMusic == null)
        {
            Debug.LogWarning("MainMenuMusicPlayer: menu music clip is not assigned.", this);
            return;
        }

        ConfigureAudioSource();

        if (!_restartIfAlreadyPlaying && IsPlaying)
            return;

        _audioSource.clip = _menuMusic;
        ApplySavedVolume();
        _audioSource.Play();
    }

    public void StopMusic()
    {
        if (_audioSource == null || _audioSource.clip != _menuMusic)
            return;

        _audioSource.Stop();
    }

    public void ApplySavedVolume()
    {
        float savedVolume = SafeGetFloat(MusicVolumeKey, 1f);
        bool muted = SafeGetInt(MusicMuteKey, 0) == 1;

        ApplyVolume(savedVolume, muted);
    }

    public void ApplyVolume(float musicVolume, bool muted)
    {
        EnsureAudioSource();

        if (_audioSource == null)
            return;

        _audioSource.volume = muted ? 0f : Mathf.Clamp01(musicVolume) * _baseVolume;
    }

    public void SetMenuMusic(AudioClip menuMusic)
    {
        bool wasPlaying = IsPlaying;
        _menuMusic = menuMusic;

        if (wasPlaying)
            PlayMusic();
    }

    private void RegisterInstance()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("MainMenuMusicPlayer: another instance already exists in the scene.", this);
            return;
        }

        Instance = this;
    }

    private void EnsureAudioSource()
    {
        if (_audioSource != null)
            return;

        _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void ConfigureAudioSource()
    {
        if (_audioSource == null)
            return;

        _audioSource.playOnAwake = false;
        _audioSource.loop = _loop;
        _audioSource.spatialBlend = 0f;

        if (_outputAudioMixerGroup != null)
            _audioSource.outputAudioMixerGroup = _outputAudioMixerGroup;
    }

    private static float SafeGetFloat(string key, float defaultValue)
    {
        try
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(key, defaultValue));
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"MainMenuMusicPlayer: failed to load '{key}': {exception.Message}");
            return defaultValue;
        }
    }

    private static int SafeGetInt(string key, int defaultValue)
    {
        try
        {
            return PlayerPrefs.GetInt(key, defaultValue);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"MainMenuMusicPlayer: failed to load '{key}': {exception.Message}");
            return defaultValue;
        }
    }
}
