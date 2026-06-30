using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class AnimatedGifPlayer : MonoBehaviour
{
    static readonly Dictionary<int, DecodedAnimatedGif> GifCache = new Dictionary<int, DecodedAnimatedGif>();

    [Header("GIF ассет")]
    [SerializeField]
    [FormerlySerializedAs("gifAsset")]
    [Tooltip("GIF-файл как TextAsset. Если Unity не импортирует .gif как TextAsset, переименуй файл в .bytes и назначь сюда.")]
    private TextAsset gifAsset;

    [Header("Кадры анимации")]
    [SerializeField]
    [FormerlySerializedAs("frames")]
    [Tooltip("Запасные кадры анимации. Они используются, если GIF TextAsset не назначен или файл не удалось декодировать.")]
    private List<Texture2D> frames = new List<Texture2D>();

    [Header("Playback")]
    [SerializeField]
    [FormerlySerializedAs("fps")]
    private float fps = 24f;

    [SerializeField]
    [FormerlySerializedAs("loop")]
    private bool loop = true;

    [SerializeField]
    [Tooltip("Use unscaled time so UI GIFs keep animating while menus pause Time.timeScale.")]
    private bool useUnscaledTime = true;

    RawImage _rawImage;
    Coroutine _playCoroutine;
    IList<Texture2D> _activeFrames;
    IList<float> _activeDelays;

    void OnValidate()
    {
        fps = Mathf.Max(1f, fps);
        frames ??= new List<Texture2D>();
    }

    void Awake()
    {
        TryResolveRawImage();
    }

    void OnDisable()
    {
        Stop();
    }

    void OnDestroy()
    {
        Stop();
    }

    public void Play(TextAsset gifAsset)
    {
        Stop();

        this.gifAsset = gifAsset;

        if (gifAsset == null)
        {
            Debug.LogWarning("AnimatedGifPlayer: cannot play a missing GIF asset.", this);
            return;
        }

        if (!TryResolveRawImage())
            return;

        RuntimeTextureFallback.EnsureRawImageVisible(_rawImage);

        if (TryGetDecodedGif(gifAsset, out var decodedGif))
        {
            StartPlayback(decodedGif.Frames, decodedGif.Delays);
            return;
        }

        if (HasPlayableFrames(frames))
        {
            StartPlayback(frames, null);
            return;
        }

        Debug.LogWarning($"AnimatedGifPlayer: no playable frames available for GIF '{gifAsset.name}'.", this);
    }

    public void Play()
    {
        Stop();

        if (!TryResolveRawImage())
            return;

        RuntimeTextureFallback.EnsureRawImageVisible(_rawImage);

        if (gifAsset != null && TryGetDecodedGif(gifAsset, out var decodedGif))
        {
            StartPlayback(decodedGif.Frames, decodedGif.Delays);
            return;
        }

        if (HasPlayableFrames(frames))
            StartPlayback(frames, null);
    }

    public void Stop()
    {
        if (_playCoroutine == null) return;

        StopCoroutine(_playCoroutine);
        _playCoroutine = null;
        _activeFrames = null;
        _activeDelays = null;
    }

    void StartPlayback(IList<Texture2D> playbackFrames, IList<float> playbackDelays)
    {
        if (playbackFrames == null || playbackFrames.Count == 0)
            return;

        _activeFrames = playbackFrames;
        _activeDelays = playbackDelays;
        _playCoroutine = StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        int index = 0;

        while (_activeFrames != null && (loop || index < _activeFrames.Count))
        {
            if (index >= _activeFrames.Count)
            {
                if (!loop) break;
                index = 0;
            }

            if (_rawImage == null || !_rawImage)
            {
                ClearPlaybackState();
                yield break;
            }

            Texture2D frame = _activeFrames[index];
            if (frame != null)
            {
                _rawImage.texture = frame;
            }

            float delay = GetFrameDelay(index);
            index++;
            yield return useUnscaledTime
                ? new WaitForSecondsRealtime(delay)
                : new WaitForSeconds(delay);
        }

        ClearPlaybackState();
    }

    void ClearPlaybackState()
    {
        _playCoroutine = null;
        _activeFrames = null;
        _activeDelays = null;
    }

    float GetFrameDelay(int index)
    {
        if (_activeDelays != null && index >= 0 && index < _activeDelays.Count)
            return Mathf.Max(0.01f, _activeDelays[index]);

        return 1f / Mathf.Max(fps, 1f);
    }

    bool TryResolveRawImage()
    {
        if (_rawImage != null && _rawImage) return true;

        _rawImage = GetComponent<RawImage>();
        if (_rawImage != null && _rawImage) return true;

        Debug.LogWarning("AnimatedGifPlayer: RawImage component is missing.", this);
        return false;
    }

    bool TryGetDecodedGif(TextAsset asset, out DecodedAnimatedGif decodedGif)
    {
        decodedGif = null;
        if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
            return false;

        int cacheKey = asset.GetInstanceID();
        if (GifCache.TryGetValue(cacheKey, out decodedGif))
            return decodedGif != null && decodedGif.Frames.Count > 0;

        RuntimeTextureLoadScope loadScope = null;
        try
        {
            loadScope = RuntimePerformanceDiagnostics.BeginTextureLoad("Gif:" + asset.name);
            decodedGif = AnimatedGifDecoder.Decode(asset.bytes, asset.name);
            if (decodedGif != null && decodedGif.Frames.Count > 0)
            {
                GifCache[cacheKey] = decodedGif;
                loadScope.Complete(true, "frames=" + decodedGif.Frames.Count);
                return true;
            }

            loadScope.Complete(false, "empty");
        }
        catch (System.Exception exception)
        {
            loadScope?.Complete(false, exception.GetType().Name);
            Debug.LogWarning($"AnimatedGifPlayer: failed to decode GIF '{asset.name}': {exception.Message}", this);
        }

        return false;
    }

    bool HasPlayableFrames(IList<Texture2D> playbackFrames)
    {
        if (playbackFrames == null || playbackFrames.Count == 0) return false;

        for (int i = 0; i < playbackFrames.Count; i++)
        {
            if (playbackFrames[i] != null) return true;
        }

        return false;
    }
}
