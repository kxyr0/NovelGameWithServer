using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;
using System.Collections.Generic;

/// <summary>
/// Нода вставки картинки с подписью.
/// Показывает изображение на экране с кнопкой "Рассмотреть" (или другой подписью).
/// Игрок нажимает — картинка закрывается, история продолжается.
///
/// Поля:
///   image       — Sprite для показа
///   video       — VideoClip для показа вместо Sprite
///   gif         — TextAsset с GIF-байтами для показа вместо Sprite
///   caption     — подпись под кнопкой ("Рассмотреть", "Читать", "Закрыть" и т.д.)
///   description — необязательный текст под картинкой
///   zoomable    — можно ли приближать (pinch-to-zoom)
/// </summary>
public class ImageNode : BaseStoryNode
{
    [Header("Медиа")]
    [SerializeField]
    [FormerlySerializedAs("image")]
    private Sprite _image;

    [SerializeField]
    [FormerlySerializedAs("video")]
    private VideoClip _video;

    [SerializeField]
    [FormerlySerializedAs("gif")]
    [Tooltip("GIF-файл как TextAsset. Если Unity импортирует .gif как Texture2D, переименуй файл в .gif.bytes и назначь сюда.")]
    private TextAsset _gif;

    [Header("Катсцены по сборке героини")]
    [SerializeField, Tooltip("Правила подмены медиа в runtime. Первое подходящее правило заменяет обычную картинку/видео/GIF этой ноды.")]
    private List<HeroBuildCutsceneOverride> _heroBuildCutsceneOverrides = new List<HeroBuildCutsceneOverride>();

    [SerializeField]
    [FormerlySerializedAs("caption")]
    [Tooltip("Текст на кнопке под картинкой, например 'Рассмотреть', 'Закрыть' или 'Прочитать'.")]
    private string _caption = "Рассмотреть";

    [SerializeField]
    [FormerlySerializedAs("description")]
    [TextArea(2, 5)]
    [Tooltip("Необязательное описание, которое будет показано под картинкой.")]
    private string _description;

    [SerializeField]
    [FormerlySerializedAs("zoomable")]
    [Tooltip("Разрешить игроку приближать картинку жестом масштабирования на телефоне.")]
    private bool _zoomable = false;

    public Sprite image => ResolveRuntimeMedia().Image;
    public VideoClip video => ResolveRuntimeMedia().Video;
    public TextAsset gif => ResolveRuntimeMedia().Gif;
    public Sprite defaultImage => _image;
    public VideoClip defaultVideo => _video;
    public TextAsset defaultGif => _gif;
    public IReadOnlyList<HeroBuildCutsceneOverride> heroBuildCutsceneOverrides => _heroBuildCutsceneOverrides;
    public string caption => _caption;
    public string description => _description;
    public bool zoomable => _zoomable;

    public void Configure(Sprite image, VideoClip video, TextAsset gif, string caption, string description, bool zoomable)
    {
        _image = image;
        _video = video;
        _gif = gif;
        _caption = string.IsNullOrWhiteSpace(caption) ? "Рассмотреть" : caption;
        _description = description ?? "";
        _zoomable = zoomable;
    }

    public void ConfigureHeroBuildCutsceneOverrides(List<HeroBuildCutsceneOverride> overrides)
    {
        _heroBuildCutsceneOverrides = overrides != null
            ? new List<HeroBuildCutsceneOverride>(overrides)
            : new List<HeroBuildCutsceneOverride>();
    }

    public HeroBuildCutsceneMedia ResolveRuntimeMedia()
    {
        return HeroBuildCutsceneResolver.Resolve(_image, _video, _gif, _heroBuildCutsceneOverrides);
    }
}
