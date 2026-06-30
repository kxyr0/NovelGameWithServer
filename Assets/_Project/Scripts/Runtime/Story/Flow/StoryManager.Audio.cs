using System;
using DG.Tweening;
using UnityEngine;

public partial class StoryManager
{
    const float StoryAudioFadeDuration = 0.5f;

    Tween _storyMusicFadeTween;
    Tween _storySfxFadeTween;
    float _storyMusicRestoreVolume = -1f;
    float _storySfxRestoreVolume = -1f;

    void OnEnable()
    {
        RegisterStoryAudioScreenEvents();
    }

    void OnDisable()
    {
        UnregisterStoryAudioScreenEvents();
        CancelStoryAudioFades(true);
    }

    void OnDestroy()
    {
        UnregisterStoryAudioScreenEvents();
        CancelStoryAudioFades(true);
    }

    void RegisterStoryAudioScreenEvents()
    {
        UIScreenState.CurrentScreenChanged -= HandleStoryAudioScreenChanged;
        UIScreenState.CurrentScreenChanged += HandleStoryAudioScreenChanged;
    }

    void UnregisterStoryAudioScreenEvents()
    {
        UIScreenState.CurrentScreenChanged -= HandleStoryAudioScreenChanged;
    }

    void HandleStoryAudioScreenChanged(string screenId)
    {
        screenId = UIScreenState.NormalizeScreenId(screenId);
        if (string.Equals(screenId, "MainScreen", StringComparison.Ordinal) ||
            string.Equals(screenId, "Wardrobe", StringComparison.Ordinal))
        {
            FadeOutStoryAudioForScreenBoundary();
        }
    }

#if UNITY_EDITOR
    internal void HandleEditorScreenStateChanged(string screenId)
    {
        HandleStoryAudioScreenChanged(screenId);
    }
#endif

    void ApplySceneAudio(SceneSetupData data, bool soundsDisabled)
    {
        if (data == null)
            return;

        if (musicSource != null)
            musicSource.mute = soundsDisabled;

        if (sfxSource != null)
            sfxSource.mute = soundsDisabled;

        if (data.music != null)
            PlayOrSwitchStoryMusic(data.music);
        else if (data.stopMusic)
            FadeOutStoryMusic();

        if (data.stopSfx)
            FadeOutStorySfx(nextSfx: data.startSfx != null && !soundsDisabled ? data.startSfx : null);
        else if (data.startSfx != null && !soundsDisabled)
            PlayStorySfx(data.startSfx);
    }

    void FadeOutStorySessionAudio(float duration = StoryAudioFadeDuration)
    {
        FadeOutStoryMusic(duration);
        FadeOutStorySfx(duration);
        FadeOutStoryVideoMedia(duration);
    }

    internal void FadeOutStoryAudioForScreenBoundary()
    {
        FadeOutStorySessionAudio(0f);
    }

    internal void FadeOutStoryAudioForWardrobe()
    {
        FadeOutStoryAudioForScreenBoundary();
    }

    void FadeOutStoryAudioForStorySelection()
    {
        if (storySelected || HasActiveStoryAudio())
            FadeOutStorySessionAudio();
    }

    void PlayOrSwitchStoryMusic(AudioClip clip, float duration = StoryAudioFadeDuration)
    {
        if (musicSource == null || clip == null)
            return;

        CancelMusicFade(true);
        float targetVolume = Mathf.Clamp01(musicSource.volume);
        bool sameClip = musicSource.clip == clip && currentMusic == clip;

        if (sameClip)
        {
            if (!musicSource.isPlaying)
                musicSource.Play();
            return;
        }

        if (musicSource.isPlaying && musicSource.clip != null && duration > 0f && targetVolume > 0f)
        {
            _storyMusicRestoreVolume = targetVolume;
            _storyMusicFadeTween = musicSource
                .DOFade(0f, duration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    _storyMusicFadeTween = null;
                    SetAndPlayStoryMusic(clip, 0f);
                    FadeInStoryMusic(targetVolume, duration);
                });
            return;
        }

        SetAndPlayStoryMusic(clip, targetVolume);
    }

    void SetAndPlayStoryMusic(AudioClip clip, float volume)
    {
        if (musicSource == null || clip == null)
            return;

        musicSource.Stop();
        musicSource.clip = clip;
        currentMusic = clip;
        musicSource.volume = Mathf.Clamp01(volume);
        musicSource.Play();
    }

    void FadeInStoryMusic(float targetVolume, float duration)
    {
        if (musicSource == null)
            return;

        targetVolume = Mathf.Clamp01(targetVolume);
        if (duration <= 0f || targetVolume <= 0f)
        {
            musicSource.volume = targetVolume;
            _storyMusicRestoreVolume = -1f;
            return;
        }

        _storyMusicRestoreVolume = targetVolume;
        _storyMusicFadeTween = musicSource
            .DOFade(targetVolume, duration)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _storyMusicFadeTween = null;
                _storyMusicRestoreVolume = -1f;
                if (musicSource != null)
                    musicSource.volume = targetVolume;
            });
    }

    void FadeOutStoryMusic(float duration = StoryAudioFadeDuration)
    {
        if (musicSource == null)
        {
            currentMusic = null;
            return;
        }

        float restoreVolume = _storyMusicRestoreVolume >= 0f
            ? _storyMusicRestoreVolume
            : Mathf.Clamp01(musicSource.volume);
        CancelMusicFade(false);
        _storyMusicRestoreVolume = restoreVolume;

        if (duration <= 0f || !musicSource.isPlaying || restoreVolume <= 0f)
        {
            StopStoryMusicImmediate(restoreVolume);
            return;
        }

        _storyMusicFadeTween = musicSource
            .DOFade(0f, duration)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _storyMusicFadeTween = null;
                StopStoryMusicImmediate(restoreVolume);
            });
    }

    void StopStoryMusicImmediate(float restoreVolume)
    {
        if (musicSource != null)
        {
            musicSource.Stop();
            musicSource.clip = null;
            musicSource.volume = Mathf.Clamp01(restoreVolume);
        }

        currentMusic = null;
        _storyMusicRestoreVolume = -1f;
    }

    void PlayStorySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
            return;

        CancelSfxFade(true);
        sfxSource.PlayOneShot(clip);
    }

    void FadeOutStorySfx(float duration = StoryAudioFadeDuration, AudioClip nextSfx = null)
    {
        if (sfxSource == null)
            return;

        float restoreVolume = _storySfxRestoreVolume >= 0f
            ? _storySfxRestoreVolume
            : Mathf.Clamp01(sfxSource.volume);
        CancelSfxFade(false);
        _storySfxRestoreVolume = restoreVolume;

        if (duration <= 0f || !sfxSource.isPlaying || restoreVolume <= 0f)
        {
            StopStorySfxImmediate(restoreVolume);
            if (nextSfx != null)
                PlayStorySfx(nextSfx);
            return;
        }

        _storySfxFadeTween = sfxSource
            .DOFade(0f, duration)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _storySfxFadeTween = null;
                StopStorySfxImmediate(restoreVolume);
                if (nextSfx != null)
                    PlayStorySfx(nextSfx);
            });
    }

    void StopStorySfxImmediate(float restoreVolume)
    {
        if (sfxSource != null)
        {
            sfxSource.Stop();
            sfxSource.clip = null;
            sfxSource.volume = Mathf.Clamp01(restoreVolume);
        }

        _storySfxRestoreVolume = -1f;
    }

    void FadeOutStoryVideoMedia(float duration)
    {
        if (backgroundView == null)
            backgroundView = FindObjectOfType<BackgroundViewManager>(true);

        if (backgroundView != null)
            backgroundView.FadeOutVideoAudioAndStop(duration);
    }

    bool HasActiveStoryAudio()
    {
        return IsAudioSourceActive(musicSource) ||
               IsAudioSourceActive(sfxSource);
    }

    static bool IsAudioSourceActive(AudioSource source)
    {
        return source != null && (source.isPlaying || source.clip != null);
    }

    void CancelStoryAudioFades(bool restoreVolume)
    {
        CancelMusicFade(restoreVolume);
        CancelSfxFade(restoreVolume);
    }

    void CancelMusicFade(bool restoreVolume)
    {
        if (_storyMusicFadeTween != null)
        {
            _storyMusicFadeTween.Kill(false);
            _storyMusicFadeTween = null;
        }

        if (restoreVolume && musicSource != null && _storyMusicRestoreVolume >= 0f)
            musicSource.volume = Mathf.Clamp01(_storyMusicRestoreVolume);

        _storyMusicRestoreVolume = -1f;
    }

    void CancelSfxFade(bool restoreVolume)
    {
        if (_storySfxFadeTween != null)
        {
            _storySfxFadeTween.Kill(false);
            _storySfxFadeTween = null;
        }

        if (restoreVolume && sfxSource != null && _storySfxRestoreVolume >= 0f)
            sfxSource.volume = Mathf.Clamp01(_storySfxRestoreVolume);

        _storySfxRestoreVolume = -1f;
    }
}
