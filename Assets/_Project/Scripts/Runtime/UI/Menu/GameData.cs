using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;

[Serializable]
public sealed class GameStoryStatData
{
    [SerializeField, HideInInspector]
    [InspectorName("Название")]
    [Tooltip("Название стата для detail-экрана истории.")]
    private string _label;

    [SerializeField]
    [InspectorName("Stat ID")]
    [Tooltip("ID накопительного стата истории. По нему History_Screen берет value из текущей игры или выбранного слота сохранения. Частые ID: city, fairytale, reputation. Alias respect тоже работает, но хранится как reputation.")]
    private string _statId;

    [SerializeField]
    [InspectorName("Значение")]
    [Tooltip("Число, которое будет показано рядом со статом на detail-экране.")]
    private int _value;

    [SerializeField]
    [InspectorName("Иконка")]
    [Tooltip("Sprite стата для detail-экрана истории. У каждой GameData можно поставить свою иконку.")]
    private Sprite _icon;

    [SerializeField, TextArea(2, 5)]
    [InspectorName("Описание")]
    [Tooltip("Ручное описание того, на что влияет стат. Показывается на экране Info.")]
    private string _description;

    public string Label => _label;
    public string StatId => _statId;
    public int Value => _value;
    public Sprite Icon => _icon;
    public string Description => _description ?? "";
}

[Serializable]
public sealed class GameMenuCardOverrideSettings
{
    [Header("Root карточки")]
    [SerializeField]
    [InspectorName("Добавить offset root")]
    [Tooltip("Добавляет story-specific поправку к позиции слота карусели.")]
    private bool _overrideRootPositionOffset;

    [SerializeField]
    [InspectorName("Root offset")]
    [Tooltip("Дополнительный offset root-карточки поверх позиции слота.")]
    private Vector2 _rootPositionOffset;

    [SerializeField]
    [InspectorName("Переопределить размер root")]
    [Tooltip("Временно задает размер root-карточки именно для этой истории.")]
    private bool _overrideRootSize;

    [SerializeField]
    [InspectorName("Root размер")]
    [Tooltip("Размер root-карточки для этой истории.")]
    private Vector2 _rootSize = new Vector2(1076.663f, 1716.369f);

    [SerializeField]
    [InspectorName("Добавить rotation root")]
    [Tooltip("Добавляет поправку к Rotation Z слота. У выбранной карточки rotation все равно может принудительно стать 0.")]
    private bool _overrideRootRotationOffset;

    [SerializeField]
    [InspectorName("Root rotation offset Z")]
    [Tooltip("Дополнительный поворот root-карточки по Z.")]
    private float _rootRotationOffsetZ;

    [SerializeField]
    [InspectorName("Умножить scale root")]
    [Tooltip("Умножает scale слота на story-specific множитель.")]
    private bool _overrideRootScaleMultiplier;

    [SerializeField]
    [InspectorName("Root scale multiplier")]
    [Tooltip("Множитель scale root-карточки. Удобно для черновых обложек разного размера.")]
    private Vector3 _rootScaleMultiplier = Vector3.one;

    [Header("Image обложки")]
    [SerializeField]
    [InspectorName("Переопределить размер обложки")]
    [Tooltip("Временный размер Image обложки. Используй для черновых картинок, потом можно выключить.")]
    private bool _overrideCoverSize;

    [SerializeField]
    [InspectorName("Размер обложки")]
    [Tooltip("Размер Image обложки для этой истории.")]
    private Vector2 _coverSize = new Vector2(1097f, 1726f);

    [SerializeField]
    [InspectorName("Добавить offset обложки")]
    [Tooltip("Добавляет story-specific offset к Image обложки.")]
    private bool _overrideCoverPositionOffset;

    [SerializeField]
    [InspectorName("Offset обложки")]
    [Tooltip("Дополнительная позиция Image обложки относительно prefab.")]
    private Vector2 _coverPositionOffset;

    [SerializeField]
    [InspectorName("Переопределить scale обложки")]
    [Tooltip("Задает scale Image обложки именно для этой истории.")]
    private bool _overrideCoverScale;

    [SerializeField]
    [InspectorName("Scale обложки")]
    [Tooltip("Scale Image обложки.")]
    private Vector3 _coverScale = Vector3.one;

    [Header("VideoPlayer обложки")]
    [SerializeField]
    [InspectorName("Переопределить размер видео")]
    [Tooltip("Если включено, RawImage/VideoPlayer обложки получит точный размер из GameData. Нужно для историй, где видео обложки другого формата, чем Card Frame.")]
    private bool _overrideVideoSize;

    [SerializeField]
    [InspectorName("Размер видео")]
    [Tooltip("Точный Width/Height для RectTransform видео-обложки.")]
    private Vector2 _videoSize = new Vector2(1522.2f, 991f);

    [SerializeField]
    [InspectorName("Переопределить позицию видео")]
    [Tooltip("Если включено, RawImage/VideoPlayer обложки получит точную Anchored Position из GameData.")]
    private bool _overrideVideoPosition;

    [SerializeField]
    [InspectorName("Позиция видео")]
    [Tooltip("Точная Pos X/Pos Y для RectTransform видео-обложки.")]
    private Vector2 _videoPosition;

    [SerializeField]
    [InspectorName("Переопределить rotation видео")]
    [Tooltip("Если включено, RawImage/VideoPlayer обложки получит точный Rotation Z из GameData.")]
    private bool _overrideVideoRotation;

    [SerializeField]
    [InspectorName("Rotation видео Z")]
    [Tooltip("Точный поворот видео-обложки по Z. Например -90 для горизонтального ролика внутри вертикальной карточки.")]
    private float _videoRotationZ = -90f;

    [SerializeField]
    [InspectorName("Видео под Card_Frame")]
    [Tooltip("Если включено, скрипт держит объект VideoPlayer перед Card_Frame в Hierarchy, чтобы визуально рамка была поверх видео.")]
    private bool _keepVideoBelowCardFrame = true;

    [SerializeField]
    [InspectorName("Loading: растянуть видео")]
    [Tooltip("Только для StoryStartLoadingScreen. Если включено, видео на загрузочном экране растягивается по родительскому RectTransform и не использует обычные Video Size/Position/Rotation.")]
    private bool _stretchVideoOnLoadingScreen;

    [SerializeField]
    [InspectorName("Loading stretch XY")]
    [Tooltip("Множитель растяжения видео на загрузочном экране по X/Y. 1/1 = ровно размер родителя, 1.1/1 = шире, 1/1.1 = выше.")]
    private Vector2 _loadingVideoStretchScale = Vector2.one;

    [SerializeField]
    [InspectorName("Loading rotation Z")]
    [Tooltip("Rotation Z для растянутого видео на загрузочном экране. Обычно 0. Если файл видео повернут боком, поставь -90 или 90.")]
    private float _loadingVideoRotationZ;

    public bool OverrideRootPositionOffset => _overrideRootPositionOffset;
    public Vector2 RootPositionOffset => _rootPositionOffset;
    public bool OverrideRootSize => _overrideRootSize;
    public Vector2 RootSize => _rootSize;
    public bool OverrideRootRotationOffset => _overrideRootRotationOffset;
    public float RootRotationOffsetZ => _rootRotationOffsetZ;
    public bool OverrideRootScaleMultiplier => _overrideRootScaleMultiplier;
    public Vector3 RootScaleMultiplier => NormalizeScale(_rootScaleMultiplier);
    public bool OverrideCoverSize => _overrideCoverSize;
    public Vector2 CoverSize => _coverSize;
    public bool OverrideCoverPositionOffset => _overrideCoverPositionOffset;
    public Vector2 CoverPositionOffset => _coverPositionOffset;
    public bool OverrideCoverScale => _overrideCoverScale;
    public Vector3 CoverScale => NormalizeScale(_coverScale);
    public bool OverrideVideoSize => _overrideVideoSize;
    public Vector2 VideoSize => _videoSize;
    public bool OverrideVideoPosition => _overrideVideoPosition;
    public Vector2 VideoPosition => _videoPosition;
    public bool OverrideVideoRotation => _overrideVideoRotation;
    public float VideoRotationZ => _videoRotationZ;
    public bool KeepVideoBelowCardFrame => _keepVideoBelowCardFrame;
    public bool StretchVideoOnLoadingScreen => _stretchVideoOnLoadingScreen;
    public Vector2 LoadingVideoStretchScale => NormalizeScale(_loadingVideoStretchScale);
    public float LoadingVideoRotationZ => _loadingVideoRotationZ;

    public void Validate()
    {
        _rootSize = ClampSize(_rootSize);
        _coverSize = ClampSize(_coverSize);
        _videoSize = ClampSize(_videoSize);
        _loadingVideoStretchScale = NormalizeScale(_loadingVideoStretchScale);
        _rootScaleMultiplier = NormalizeScale(_rootScaleMultiplier);
        _coverScale = NormalizeScale(_coverScale);
    }

    private static Vector2 ClampSize(Vector2 size)
    {
        return new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
    }

    private static Vector2 NormalizeScale(Vector2 scale)
    {
        if (Mathf.Approximately(scale.x, 0f))
            scale.x = 1f;

        if (Mathf.Approximately(scale.y, 0f))
            scale.y = 1f;

        return scale;
    }

    private static Vector3 NormalizeScale(Vector3 scale)
    {
        if (Mathf.Approximately(scale.x, 0f))
            scale.x = 1f;

        if (Mathf.Approximately(scale.y, 0f))
            scale.y = 1f;

        if (Mathf.Approximately(scale.z, 0f))
            scale.z = 1f;

        return scale;
    }
}

[Serializable]
public sealed class GameWardrobeSetupSettings
{
    private static readonly IReadOnlyList<WardrobeHeroAppearanceOption> EmptyAppearanceOptions =
        Array.Empty<WardrobeHeroAppearanceOption>();
    private static readonly IReadOnlyList<ClothingItem> EmptyClothingItems = Array.Empty<ClothingItem>();

    [Header("Гардероб этой истории")]
    [SerializeField]
    [InspectorName("Использовать override гардероба")]
    [Tooltip("Если включено, экран гардероба возьмет персонажа, типажи, наряды и прически отсюда, а не из fallback-сцены.")]
    private bool _overrideWardrobeAssets;

    [SerializeField]
    [InspectorName("Персонаж")]
    [Tooltip("CharacterData героини именно для этой истории. Отсюда берется body и layout тела.")]
    private CharacterData _targetCharacter;

    [SerializeField]
    [InspectorName("Character ID")]
    [Tooltip("ID слота сохранения одежды, обычно hero. Если пусто, будет использовано имя CharacterData или hero.")]
    private string _targetCharacterId = "hero";

    [SerializeField]
    [InspectorName("Типажи")]
    [Tooltip("Варианты типажа/национальности для этой истории.")]
    private List<WardrobeHeroAppearanceOption> _appearanceOptions = new List<WardrobeHeroAppearanceOption>();

    [SerializeField]
    [InspectorName("Наряды")]
    [Tooltip("ClothingItem с типом Outfit, доступные в гардеробе этой истории.")]
    private List<ClothingItem> _outfitItems = new List<ClothingItem>();

    [SerializeField]
    [InspectorName("Прически")]
    [Tooltip("ClothingItem с типом Hair, доступные в гардеробе этой истории.")]
    private List<ClothingItem> _hairItems = new List<ClothingItem>();

    [SerializeField]
    [InspectorName("Аксессуары")]
    [Tooltip("ClothingItem с типом Accessory, доступные в гардеробе этой истории.")]
    private List<ClothingItem> _accessoryItems = new List<ClothingItem>();

    [SerializeField]
    [InspectorName("Дефолтный наряд")]
    [Tooltip("Наряд, который будет поставлен первым, если у игрока еще нет сохраненного выбора для этой истории.")]
    private ClothingItem _defaultOutfitItem;

    [SerializeField]
    [InspectorName("Дефолтная прическа")]
    [Tooltip("Прическа, которая будет поставлена первой, если у игрока еще нет сохраненного выбора для этой истории.")]
    private ClothingItem _defaultHairItem;

    [SerializeField]
    [InspectorName("Дефолтный аксессуар")]
    [Tooltip("Аксессуар, который будет поставлен первым, если у игрока еще нет сохраненного выбора для этой истории.")]
    private ClothingItem _defaultAccessoryItem;

    public bool OverrideWardrobeAssets => _overrideWardrobeAssets;
    public CharacterData TargetCharacter => _targetCharacter;
    public string TargetCharacterId => _targetCharacterId;
    public IReadOnlyList<WardrobeHeroAppearanceOption> AppearanceOptions => _appearanceOptions ?? EmptyAppearanceOptions;
    public IReadOnlyList<ClothingItem> OutfitItems => _outfitItems ?? EmptyClothingItems;
    public IReadOnlyList<ClothingItem> HairItems => _hairItems ?? EmptyClothingItems;
    public IReadOnlyList<ClothingItem> AccessoryItems => _accessoryItems ?? EmptyClothingItems;
    public ClothingItem DefaultOutfitItem => _defaultOutfitItem;
    public ClothingItem DefaultHairItem => _defaultHairItem;
    public ClothingItem DefaultAccessoryItem => _defaultAccessoryItem;

    public bool HasRuntimeContent =>
        _targetCharacter != null ||
        HasItems(_appearanceOptions) ||
        HasItems(_outfitItems) ||
        HasItems(_hairItems) ||
        HasItems(_accessoryItems) ||
        _defaultOutfitItem != null ||
        _defaultHairItem != null ||
        _defaultAccessoryItem != null ||
        !string.IsNullOrWhiteSpace(_targetCharacterId);

    public void Validate()
    {
        _appearanceOptions ??= new List<WardrobeHeroAppearanceOption>();
        _outfitItems ??= new List<ClothingItem>();
        _hairItems ??= new List<ClothingItem>();
        _accessoryItems ??= new List<ClothingItem>();
        _targetCharacterId = string.IsNullOrWhiteSpace(_targetCharacterId) ? "hero" : _targetCharacterId.Trim();
    }

    private static bool HasItems<T>(IReadOnlyList<T> items) where T : class
    {
        if (items == null)
            return false;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                return true;
        }

        return false;
    }
}

[CreateAssetMenu(fileName = "Game Data", menuName = "Nocturne/Story/Game Data")]
public class GameData : ScriptableObject
{
    private static readonly IReadOnlyList<GameStoryStatData> EmptyStats = Array.Empty<GameStoryStatData>();

    [Header("Карточка истории")]
    [SerializeField]
    [FormerlySerializedAs("GameIcon")]
    [InspectorName("Обложка")]
    [Tooltip("Sprite обложки, который карточка истории подставит в Image обложки.")]
    private Sprite _gameIcon;

    [SerializeField]
    [FormerlySerializedAs("GameIconVideo")]
    [InspectorName("Видео обложки")]
    [Tooltip("Опциональное видео для animated-cover. Если задано, карточка может показать его вместо статичной обложки.")]
    private VideoClip _gameIconVideo;

    [SerializeField]
    [FormerlySerializedAs("GameIconGif")]
    [InspectorName("GIF обложки")]
    [Tooltip("GIF-файл как TextAsset для превью. Если Unity импортирует .gif как Texture2D, переименуй файл в .gif.bytes и назначь сюда.")]
    private TextAsset _gameIconGif;

    [SerializeField]
    [InspectorName("Overrides карточки")]
    [Tooltip("Story-specific правки root/cover для черновых обложек и точной подгонки в карусели.")]
    private GameMenuCardOverrideSettings _menuCardOverrides = new GameMenuCardOverrideSettings();

    [SerializeField]
    [InspectorName("Story start loading media")]
    [Tooltip("Lazy image/video/GIF replacements for StoryStartLoadingScreen. Addressable references are loaded only while the selected story is starting.")]
    private GameStoryLoadingMediaSettings _loadingMedia = new GameStoryLoadingMediaSettings();

    [SerializeField]
    [InspectorName("Гардероб истории")]
    [Tooltip("Story-specific ассеты гардероба. Сюда назначай ПП/ЗЛС персонажа, волосы, наряды и дефолтный выбор.")]
    private GameWardrobeSetupSettings _wardrobeSetup = new GameWardrobeSetupSettings();

    [Header("Тексты")]
    [SerializeField]
    [FormerlySerializedAs("GameName")]
    [InspectorName("Название истории")]
    [Tooltip("Название, которое карточка и detail-экран выведут в поле текста названия.")]
    private string _gameName;

    [SerializeField]
    [InspectorName("Жанр")]
    [Tooltip("Короткий жанровый текст на карточке, например Драма.")]
    private string _genreText;

    [SerializeField, TextArea(3, 8)]
    [InspectorName("Описание")]
    [Tooltip("Описание истории для экрана History_Screen.")]
    private string _description;

    [Header("Серии")]
    [SerializeField, Min(1)]
    [InspectorName("Текущая серия")]
    [Tooltip("Номер серии, который будет подставлен в строку серии.")]
    private int _currentEpisodeNumber = 1;

    [SerializeField, Min(0)]
    [InspectorName("Всего серий")]
    [Tooltip("Общее количество серий истории. Если 0, строка серии будет пустой.")]
    private int _episodeCount;

    [SerializeField]
    [InspectorName("Формат строки серии")]
    [Tooltip("Формат TMP-текста серии. {0} = текущая серия, {1} = всего серий.")]
    private string _episodeLabelFormat = "Серия {0}/{1}";

    [SerializeField]
    [InspectorName("Принудительно Скоро")]
    [Tooltip("Включи, если карточка уже должна быть в меню, но запускать историю пока нельзя. Кнопка на History_Screen покажет текст Скоро.")]
    private bool _forceComingSoon;

    [SerializeField]
    [InspectorName("Текст кнопки старта")]
    [Tooltip("Текст кнопки, когда история доступна для запуска.")]
    private string _startButtonText = "Начать";

    [SerializeField]
    [InspectorName("Текст Скоро")]
    [Tooltip("Текст кнопки, когда у истории нет доступных серий или включен флаг Принудительно Скоро.")]
    private string _comingSoonButtonText = "Скоро";

    [Header("Статы истории")]
    [SerializeField]
    [InspectorName("Статы")]
    [Tooltip("Значения статов для экрана History_Screen. UI ищет строки по Stat ID или по порядку.")]
    private GameStoryStatData[] _storyStats = Array.Empty<GameStoryStatData>();

    [Header("Связь")]
    [SerializeField]
    [FormerlySerializedAs("Story")]
    [InspectorName("Story Data")]
    [Tooltip("StoryData, которую запускает эта карточка.")]
    private StoryData _story;

    public Sprite GameIcon => _gameIcon;
    public VideoClip GameIconVideo => _gameIconVideo;
    public TextAsset GameIconGif => _gameIconGif;
    public GameMenuCardOverrideSettings MenuCardOverrides => _menuCardOverrides;
    public GameStoryLoadingMediaSettings LoadingMedia => EnsureLoadingMedia();
    public GameWardrobeSetupSettings WardrobeSetup => _wardrobeSetup;
    public string GameName => _gameName;
    public string GenreText => _genreText;
    public string Description => _description;
    public int CurrentEpisodeNumber => Mathf.Max(1, _currentEpisodeNumber);
    public int EpisodeCount => Mathf.Max(0, _episodeCount);
    public string EpisodeLabelFormat => string.IsNullOrWhiteSpace(_episodeLabelFormat) ? "Серия {0}/{1}" : _episodeLabelFormat;
    public bool ForceComingSoon => _forceComingSoon;
    public bool HasPlayableStory => HasPlayableChapter(_story);
    public bool IsComingSoon => _forceComingSoon || !HasPlayableStory;
    public bool CanStartStory => !IsComingSoon;
    public string AvailableStartButtonText => string.IsNullOrWhiteSpace(_startButtonText) ? "Начать" : _startButtonText;
    public string ComingSoonButtonText => string.IsNullOrWhiteSpace(_comingSoonButtonText) ? "Скоро" : _comingSoonButtonText;
    public string StartButtonText => IsComingSoon ? ComingSoonButtonText : AvailableStartButtonText;
    public IReadOnlyList<GameStoryStatData> StoryStats => _storyStats ?? EmptyStats;
    public StoryData Story => _story;

    public string EpisodeProgressText
    {
        get
        {
            int total = EpisodeCount;
            if (total <= 0)
                return "";

            return string.Format(EpisodeLabelFormat, Mathf.Clamp(CurrentEpisodeNumber, 1, total), total);
        }
    }

    private void OnValidate()
    {
        _currentEpisodeNumber = Mathf.Max(1, _currentEpisodeNumber);
        _episodeCount = Mathf.Max(0, _episodeCount);
        _startButtonText = string.IsNullOrWhiteSpace(_startButtonText) ? "Начать" : _startButtonText.Trim();
        _comingSoonButtonText = string.IsNullOrWhiteSpace(_comingSoonButtonText) ? "Скоро" : _comingSoonButtonText.Trim();

        if (_storyStats == null)
            _storyStats = Array.Empty<GameStoryStatData>();

        if (_menuCardOverrides == null)
            _menuCardOverrides = new GameMenuCardOverrideSettings();

        _menuCardOverrides.Validate();

        if (_loadingMedia == null)
            _loadingMedia = new GameStoryLoadingMediaSettings();
        _loadingMedia.EnsureInitialized();

        if (_wardrobeSetup == null)
            _wardrobeSetup = new GameWardrobeSetupSettings();

        _wardrobeSetup.Validate();
    }

    public GameStoryLoadingMediaSettings EnsureLoadingMedia()
    {
        if (_loadingMedia == null)
            _loadingMedia = new GameStoryLoadingMediaSettings();

        _loadingMedia.EnsureInitialized();
        return _loadingMedia;
    }

    public void Configure(
        string gameName,
        StoryData story,
        Sprite icon = null,
        VideoClip iconVideo = null,
        TextAsset iconGif = null,
        string genreText = null)
    {
        _gameName = gameName ?? "";
        _genreText = genreText ?? "";
        _story = story;
        _gameIcon = icon;
        _gameIconVideo = iconVideo;
        _gameIconGif = iconGif;
    }

    private static bool HasPlayableChapter(StoryData story)
    {
        if (story == null || story.Chapters == null || story.Chapters.Count == 0)
            return false;

        for (int i = 0; i < story.Chapters.Count; i++)
        {
            ChapterData chapter = story.Chapters[i];
            if (chapter != null && (chapter.Graph != null || chapter.JsonGraph != null))
                return true;
        }

        return false;
    }
}
