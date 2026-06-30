using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Данные сцены: фон (спрайт / видео / GIF), музыка, эффекты.
///
/// Заполни ОДНО из трёх полей фона:
///   background      — статичный Sprite
///   backgroundVideo — VideoClip (mp4, webm)
///   backgroundGif   — TextAsset с GIF-байтами (.gif.bytes) — требует AnimatedGifPlayer
///
/// backgroundOverlay — опциональный спрайт-оверлей поверх фона (затемнение, виньетка и т.д.)
/// </summary>
[CreateAssetMenu(menuName = "VN/Scene Setup")]
public class SceneSetupData : ScriptableObject
{
    [Header("JSON asset ids")]
    public string backgroundId;
    public string backgroundVideoId;
    public string backgroundGifId;
    public string backgroundOverlayId;
    public string musicId;
    public string startSfxId;

    [Header("Фон (выбери одно)")]
    public Sprite background;
    public VideoClip backgroundVideo;
    [Tooltip("GIF-файл как TextAsset для фона сцены. Если Unity импортирует .gif как Texture2D, переименуй файл в .gif.bytes и назначь сюда.")]
    public TextAsset backgroundGif;

    [Header("Оверлей (опционально)")]
    [Tooltip("Спрайт, который накладывается поверх фона, например затемнение или виньетка.")]
    public Sprite backgroundOverlay;

    [Header("Аудио")]
    public AudioClip music;
    [Tooltip("Плавно остановить текущую музыку истории. Если выключено и Music пустой, предыдущая музыка продолжит играть.")]
    public bool stopMusic;
    [Tooltip("Остановить текущие SFX перед запуском этой сцены. Полезно, когда длинный звук мог остаться после быстрого прохождения.")]
    public bool stopSfx;
    public AudioClip startSfx;
}
