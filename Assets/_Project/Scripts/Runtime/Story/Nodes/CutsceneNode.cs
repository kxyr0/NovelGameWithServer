using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;

public class CutsceneNode : DialogueNode
{
    private const float DefaultTextDelay = 0.6f;

    [Header("Cutscene Media")]
    [SerializeField]
    [FormerlySerializedAs("image")]
    private Sprite _image;

    [SerializeField]
    [FormerlySerializedAs("video")]
    private VideoClip _video;

    [SerializeField]
    [FormerlySerializedAs("gif")]
    private TextAsset _gif;

    [Header("Катсцены по сборке героини")]
    [SerializeField, Tooltip("Правила подмены медиа в runtime. Первое подходящее правило заменяет обычную картинку/видео/GIF этой катсцены.")]
    private List<HeroBuildCutsceneOverride> _heroBuildCutsceneOverrides = new List<HeroBuildCutsceneOverride>();

    [Header("Cutscene Flow")]
    [SerializeField, Min(0f)]
    private float _textDelay = DefaultTextDelay;

    [SerializeField]
    private bool _hideCharacters = true;

    public Sprite image => ResolveRuntimeMedia().Image;
    public VideoClip video => ResolveRuntimeMedia().Video;
    public TextAsset gif => ResolveRuntimeMedia().Gif;
    public Sprite defaultImage => _image;
    public VideoClip defaultVideo => _video;
    public TextAsset defaultGif => _gif;
    public IReadOnlyList<HeroBuildCutsceneOverride> heroBuildCutsceneOverrides => _heroBuildCutsceneOverrides;
    public float TextDelay => Mathf.Max(0f, _textDelay);
    public bool HideCharacters => _hideCharacters;

    public void Configure(
        Sprite image,
        VideoClip video,
        TextAsset gif,
        float textDelay,
        bool hideCharacters,
        string title,
        List<DialogueLine> dialogueLines)
    {
        _image = image;
        _video = video;
        _gif = gif;
        _textDelay = Mathf.Max(0f, textDelay);
        _hideCharacters = hideCharacters;
        nodeTitle = title ?? "";
        lines = dialogueLines ?? new List<DialogueLine>();
        activeCharacters = new List<DialogueCharacterEntry>();
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
