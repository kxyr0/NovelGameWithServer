using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;

[CreateAssetMenu(menuName = "VN/Story JSON Asset Library")]
public sealed class StoryJsonAssetLibrary : ScriptableObject
{
    [Header("Story UI")]
    [Tooltip("Reusable Story UI style for this JSON story.")]
    [FormerlySerializedAs("_dialoguePanelStyle")]
    [SerializeField] private StoryUiStyle _storyUiStyle;

    [Tooltip("Быстрая замена Source Image у фона диалоговой плашки. Если задан и стиль, этот спрайт имеет приоритет над спрайтом из стиля.")]
    [SerializeField] private Sprite _dialogueBackgroundSprite;

    [Tooltip("Включи, если катсцены должны использовать отдельный стиль плашки. Если выключено, катсцены берут обычный стиль истории.")]
    [FormerlySerializedAs("_useSeparateCutsceneDialoguePanelStyle")]
    [SerializeField] private bool _useSeparateCutsceneStoryUiStyle;

    [Tooltip("Отдельный стиль фона диалоговой плашки для катсцен этой JSON-истории.")]
    [FormerlySerializedAs("_cutsceneDialoguePanelStyle")]
    [SerializeField] private StoryUiStyle _cutsceneStoryUiStyle;

    [Tooltip("Быстрая замена Source Image у фона плашки катсцен.")]
    [SerializeField] private Sprite _cutsceneDialogueBackgroundSprite;

    [SerializeField]
    private List<StoryJsonAssetReference> _assets = new List<StoryJsonAssetReference>();

    public IReadOnlyList<StoryJsonAssetReference> Assets => _assets;

    public bool TryGetStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        style = _storyUiStyle;
        backgroundSprite = _dialogueBackgroundSprite;
        return style != null || backgroundSprite != null;
    }

    public bool TryGetCutsceneStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        if (_useSeparateCutsceneStoryUiStyle)
        {
            style = _cutsceneStoryUiStyle;
            backgroundSprite = _cutsceneDialogueBackgroundSprite;
            return style != null || backgroundSprite != null;
        }

        return TryGetStoryUiStyle(out style, out backgroundSprite);
    }

    public CharacterData FindCharacter(string id)
    {
        return Find(id)?.Character;
    }

    public ClothingItem FindClothing(string id)
    {
        return Find(id)?.Clothing;
    }

    public Sprite FindSprite(string id)
    {
        return Find(id)?.Sprite;
    }

    public VideoClip FindVideoClip(string id)
    {
        return Find(id)?.Video;
    }

    public TextAsset FindTextAsset(string id)
    {
        return Find(id)?.TextAsset;
    }

    public AudioClip FindAudioClip(string id)
    {
        return Find(id)?.Audio;
    }

    public DialogueStyle FindDialogueStyle(string id)
    {
        return Find(id)?.DialogueStyle;
    }

    public string FindIdForAsset(UnityEngine.Object asset)
    {
        if (asset == null || _assets == null)
            return "";

        foreach (var entry in _assets)
        {
            if (entry != null && entry.Contains(asset))
                return entry.Id;
        }

        return "";
    }

    public void Configure(IEnumerable<StoryJsonAssetReference> assets)
    {
        _assets = assets != null
            ? new List<StoryJsonAssetReference>(assets)
            : new List<StoryJsonAssetReference>();
    }

    private StoryJsonAssetReference Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || _assets == null)
            return null;

        foreach (var entry in _assets)
        {
            if (entry != null && entry.Matches(id))
                return entry;
        }

        return null;
    }
}

[Serializable]
public sealed class StoryJsonAssetReference
{
    [SerializeField]
    private string _id;

    [SerializeField]
    private CharacterData _character;

    [SerializeField]
    private ClothingItem _clothing;

    [SerializeField]
    private Sprite _sprite;

    [SerializeField]
    private VideoClip _video;

    [SerializeField]
    private TextAsset _textAsset;

    [SerializeField]
    private AudioClip _audio;

    [SerializeField]
    private DialogueStyle _dialogueStyle;

    public string Id => _id;
    public CharacterData Character => _character;
    public ClothingItem Clothing => _clothing;
    public Sprite Sprite => _sprite;
    public VideoClip Video => _video;
    public TextAsset TextAsset => _textAsset;
    public AudioClip Audio => _audio;
    public DialogueStyle DialogueStyle => _dialogueStyle;

    public bool Matches(string id)
    {
        return !string.IsNullOrWhiteSpace(id) &&
               string.Equals(_id, id, StringComparison.OrdinalIgnoreCase);
    }

    public bool Contains(UnityEngine.Object asset)
    {
        return asset != null &&
               (asset == _character ||
                asset == _clothing ||
                asset == _sprite ||
                asset == _video ||
                asset == _textAsset ||
                asset == _audio ||
                asset == _dialogueStyle);
    }

    public static StoryJsonAssetReference CreateCharacter(string id, CharacterData asset)
    {
        return new StoryJsonAssetReference { _id = id ?? "", _character = asset };
    }

    public static StoryJsonAssetReference CreateClothing(string id, ClothingItem asset)
    {
        return new StoryJsonAssetReference { _id = id ?? "", _clothing = asset };
    }

    public static StoryJsonAssetReference CreateSprite(string id, Sprite asset)
    {
        return new StoryJsonAssetReference { _id = id ?? "", _sprite = asset };
    }

    public static StoryJsonAssetReference CreateVideo(string id, VideoClip asset)
    {
        return new StoryJsonAssetReference { _id = id ?? "", _video = asset };
    }

    public static StoryJsonAssetReference CreateText(string id, TextAsset asset)
    {
        return new StoryJsonAssetReference { _id = id ?? "", _textAsset = asset };
    }

    public static StoryJsonAssetReference CreateAudio(string id, AudioClip asset)
    {
        return new StoryJsonAssetReference { _id = id ?? "", _audio = asset };
    }

    public static StoryJsonAssetReference CreateStyle(string id, DialogueStyle asset)
    {
        return new StoryJsonAssetReference { _id = id ?? "", _dialogueStyle = asset };
    }
}
