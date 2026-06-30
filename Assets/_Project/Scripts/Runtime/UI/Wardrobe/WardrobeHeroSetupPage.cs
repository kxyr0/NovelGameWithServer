using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum WardrobeHeroSetupStep
{
    Appearance,
    Outfit,
    Hair,
    Accessories
}

[Serializable]
public struct WardrobeOptionSelectionInfo
{
    public WardrobeHeroSetupStep step;
    public string label;
    public int index;
    public int count;
    public bool canSelectPrevious;
    public bool canSelectNext;
}

[Serializable]
public sealed class WardrobeHeroAppearanceOption
{
    [Tooltip("Название варианта, которое игрок увидит в списке, например Европейка, Азиатка или Латино.")]
    public string label;

    [Tooltip("Тип внешности, который будет сохранён в PlayerAppearance после выбора этого варианта.")]
    public AppearanceType type = AppearanceType.Default;

    [Tooltip("Запасной спрайт превью. Если у активного CharacterData есть AppearanceVariant.defaultSprite для этого типа, гардероб использует его вместо этого спрайта.")]
    public Sprite previewSprite;

    [Tooltip("Если выключено, вариант не показывается игроку, но остаётся в настройках автора.")]
    public bool enabled = true;
}

[DisallowMultipleComponent]
[AddComponentMenu("Novel Template/UI/Wardrobe Hero Setup Page")]
public sealed partial class WardrobeHeroSetupPage : MonoBehaviour
{
    const string DefaultCompletionPrefsKey = "VN_WARDROBE_HERO_SETUP_DONE";
    const string EditorPreviewOptionNamePrefix = "[Wardrobe Preview] ";
    const string GeneratedOptionsContainerName = "RuntimeOptions";
    const float LayeredPreviewOpenFadeDuration = 0.18f;

    [Header("Назначение страницы")]
    [Tooltip("Корневой объект всей страницы гардероба. Если поле пустое, будет использован объект с этим компонентом.")]
    [SerializeField] private GameObject _pageRoot;

    [Tooltip("CanvasGroup страницы гардероба. Необязателен, но нужен для плавного включения и выключения страницы.")]
    [SerializeField] private CanvasGroup _pageCanvasGroup;

    [Tooltip("Корень новых элементов настройки главной героини. Скрипт включает его при открытии процесса и выключает при закрытии.")]
    [SerializeField] private GameObject _setupContentRoot;

    [Tooltip("Старые или лишние элементы на странице гардероба, которые нужно скрывать, пока этот скрипт показывает свои шаги.")]
    [SerializeField] private List<GameObject> _hideWhileSetupOpen = new List<GameObject>();

    [Tooltip("Объекты истории, которые нужно скрыть, пока открыта страница гардероба, например диалоговую плашку, выборы или HUD истории.")]
    [SerializeField] private List<GameObject> _hideStoryObjectsWhileOpen = new List<GameObject>();

    [Space(8)]
    [Header("Тексты и превью")]
    [Tooltip("Заголовок текущего шага: выбор внешности, одежды или причёски.")]
    [SerializeField] private TMP_Text _titleText;

    [Tooltip("Текст под заголовком, который объясняет игроку текущий шаг.")]
    [SerializeField] private TMP_Text _descriptionText;

    [Tooltip("Текст, который показывается, если на текущем шаге нет доступных вариантов.")]
    [SerializeField] private TMP_Text _emptyText;

    [Tooltip("Большая картинка превью выбранного варианта. Для внешности сначала берётся CharacterData.AppearanceVariant.defaultSprite, для одежды и причёски - sprite из ClothingItem.")]
    [SerializeField] private Image _previewImage;

    [Header("Layered Character Preview")]
    [SerializeField] private Image _bodyPreviewImage;
    [SerializeField] private Image _outfitPreviewImage;
    [SerializeField] private Image _hairPreviewImage;
    [SerializeField] private Image _accessoryPreviewImage;

    [Space(8)]
    [Header("Список вариантов")]
    [Tooltip("Контейнер, куда будут создаваться кнопки вариантов. Обычно это Content внутри ScrollView или объект с VerticalLayoutGroup.")]
    [SerializeField] private Transform _optionsContainer;

    [Tooltip("Префаб кнопки варианта. Внутри желательно иметь TMP_Text; Image для превью внутри кнопки необязателен.")]
    [SerializeField] private Button _optionButtonPrefab;

    [Tooltip("Показывать отдельный список кнопок вариантов. Если варианты листаются стрелками ArrowLeft и ArrowRight, оставь выключенным.")]
    [SerializeField] private bool _showOptionButtons;

    [Tooltip("Если включено, скрипт попробует найти Image внутри кнопки варианта и поставить туда превью.")]
    [SerializeField] private bool _fillPreviewImageInsideOptionButton = true;

    [Tooltip("Если включено, выбранный вариант будет помечаться префиксом в тексте кнопки.")]
    [SerializeField] private bool _markSelectedOptionInText = true;

    [Tooltip("Префикс перед выбранным вариантом. Если шрифт не поддерживает галочку, поставь обычный текст вроде '[x] '.")]
    [SerializeField] private string _selectedOptionPrefix = "✓ ";

    [Tooltip("Кнопка-стрелка влево для листания вариантов текущего шага. Если поле пустое, скрипт ищет объект ArrowLeft.")]
    [SerializeField] private Button _previousOptionButton;

    [Tooltip("Кнопка-стрелка вправо для листания вариантов текущего шага. Если поле пустое, скрипт ищет объект ArrowRight.")]
    [SerializeField] private Button _nextOptionButton;

    [InspectorName("Зацикливать стрелки")]
    [Tooltip("Если включено, стрелки листают варианты по кругу. Если выключено, на первом варианте гаснет левая стрелка, на последнем - правая.")]
    [SerializeField] private bool _wrapOptionNavigation;

    [InspectorName("Левая стрелка disabled fade")]
    [Tooltip("Sprite Fade для выключенного состояния левой стрелки. Default sprite = обычная стрелка, Active sprite = выключенная стрелка.")]
    [SerializeField] private UISpriteStateFade[] _previousOptionDisabledFades = Array.Empty<UISpriteStateFade>();

    [InspectorName("Правая стрелка disabled fade")]
    [Tooltip("Sprite Fade для выключенного состояния правой стрелки. Default sprite = обычная стрелка, Active sprite = выключенная стрелка.")]
    [SerializeField] private UISpriteStateFade[] _nextOptionDisabledFades = Array.Empty<UISpriteStateFade>();

    [InspectorName("Отключать hover у стрелок")]
    [Tooltip("Отключить hover у fade-стрелок, чтобы их состояние задавалось только доступностью листания.")]
    [SerializeField] private bool _disableHoverOnOptionArrowFades = true;

    [InspectorName("Стрелки сразу выбирают")]
    [Tooltip("Если включено, стрелки не только показывают вариант, но и сразу применяют его. Для нового гардероба обычно выключено: применяет кнопка Выбрать.")]
    [SerializeField] private bool _applyOptionWhenBrowsingWithArrows;

    [Tooltip("Текст выбранного варианта. Если поле пустое, скрипт ищет TMP_Text с именем NameCloth.")]
    [SerializeField] private TMP_Text _selectedOptionLabel;

    [Tooltip("Помощник для цены платного варианта. Выводит число и отдельную иконку, чтобы не зависеть от символов в шрифте.")]
    [SerializeField] private InlinePriceIconLayout _selectedOptionPriceIcon;

    [Tooltip("Иконка, которая показывается рядом с ценой платного варианта гардероба.")]
    [SerializeField] private Sprite _premiumCostIcon;

    [Tooltip("Если отдельной кнопки Далее или Готово нет, клик по тексту выбранного варианта подтвердит текущий шаг.")]
    [SerializeField] private bool _useSelectedOptionLabelAsConfirmButton = true;

    Button _selectedOptionLabelConfirmButton;
    ClothingItem _currentOutfitPreviewItem;
    ClothingItem _currentHairPreviewItem;
    ClothingItem _currentAccessoryPreviewItem;

    [Header("Option Change Animation")]
    [SerializeField] private bool _animateOptionChanges = true;
    [SerializeField, Min(0f)] private float _optionSwipeDistance = 260f;
    [SerializeField, Min(0f)] private float _optionSwipeDuration = 0.28f;
    [SerializeField] private Ease _optionSwipeOutEase = Ease.InCubic;
    [SerializeField] private Ease _optionSwipeInEase = Ease.OutCubic;
    [SerializeField] private bool _useUnscaledOptionAnimation = true;
    [SerializeField] private bool _animateWholeLayeredCharacterForClothingChanges = true;

    Sequence _optionPreviewTween;
    RectTransform _optionAnimatedRect;
    CanvasGroup _optionAnimatedCanvasGroup;
    Vector2 _optionAnimatedBasePosition;
    Tween _systemMessageRestoreTween;
    Tween _layeredPreviewOpenTween;
    CanvasGroup _layeredPreviewCanvasGroup;

    [Space(8)]
    [Header("Кнопки управления")]
    [Tooltip("Кнопка назад. На первом шаге будет выключена, если назад идти некуда.")]
    [SerializeField] private Button _backButton;

    [Tooltip("Кнопка Далее или Готово. Подтверждает текущий выбор и переводит на следующий шаг.")]
    [SerializeField] private Button _continueButton;

    [Tooltip("Текст внутри кнопки продолжения. Скрипт будет менять его на 'Далее' или 'Готово'.")]
    [SerializeField] private TMP_Text _continueButtonLabel;

    [Tooltip("Кнопка закрытия страницы без продолжения истории. Можно оставить пустой, если закрытие не нужно.")]
    [SerializeField] private Button _closeButton;

    [Tooltip("Навигатор экранов меню. Если пусто, страница попробует найти StoryScreenNavigator в сцене.")]
    [SerializeField] private StoryScreenNavigator _closeScreenNavigator;

    [Tooltip("Screen ID, куда вернуться по X при открытом свободном гардеробе из меню истории.")]
    [SerializeField] private string _closeTargetScreenId = "History";

    [Tooltip("Если включено, X закрывает гардероб и возвращает на экран истории. Для сюжетных выборов возврат не применяется.")]
    [SerializeField] private bool _closeButtonReturnsToScreen = true;

    [Tooltip("Текст кнопки продолжения на промежуточных шагах.")]
    [SerializeField] private string _nextButtonText = "Далее";

    [Tooltip("Текст кнопки продолжения на последнем шаге.")]
    [SerializeField] private string _doneButtonText = "Готово";

    [Space(8)]
    [Header("Интеграция с историей")]
    [Tooltip("Если включено, нода appearanceChoice будет открываться на этой странице гардероба вместо обычных кнопок истории.")]
    [SerializeField] private bool _useForStoryAppearanceChoices = true;

    [Tooltip("Если включено, нода wardrobeChoice будет открываться на этой странице гардероба вместо обычных кнопок истории.")]
    [SerializeField] private bool _useForStoryWardrobeChoices = true;

    [Tooltip("Если включено, нода openWardrobe будет запускать полный процесс настройки: внешность, одежда, причёска.")]
    [SerializeField] private bool _useForOpenWardrobeNode = true;

    [Tooltip("Если включено, обычный полный гардероб показывает общий набор вещей без фильтра по Story ID. Сюжетные wardrobeChoice-ноды остаются привязанными к истории.")]
    [SerializeField] private bool _useGlobalInventoryInFullSetup;

    [Tooltip("Если включено, выбор внешности или гардероба из истории завершается сразу после клика по варианту, как обычная ChoiceNode.")]
    [SerializeField] private bool _completeStoryChoiceOnOptionClick = true;

    [Space(8)]
    [Header("Привязка к истории")]
    [Tooltip("Story ID историй, которые должны использовать эту страницу гардероба. Оставляй пустым только у запасной страницы по умолчанию.")]
    [SerializeField] private List<string> _storyIds = new List<string>();

    [Tooltip("Chapter ID или Episode ID, для которых подходит эта страница. Если список пустой, страница работает для всех глав совпавшей истории.")]
    [SerializeField] private List<string> _chapterIds = new List<string>();

    [Tooltip("Использовать эту страницу, если для текущей истории не найдено точное совпадение. Включай только на глобальной странице по умолчанию.")]
    [SerializeField] private bool _useAsFallbackForUnmatchedStories = true;

    [Space(8)]
    [Header("Полный flow настройки ГГ")]
    [Tooltip("Показывать шаг выбора внешности или национальности в полном процессе настройки.")]
    [SerializeField] private bool _showAppearanceStep = true;

    [Tooltip("Показывать шаг выбора одежды в полном процессе настройки.")]
    [SerializeField] private bool _showOutfitStep = true;

    [Tooltip("Показывать шаг выбора причёски в полном процессе настройки.")]
    [SerializeField] private bool _showHairStep = true;

    [Tooltip("Показывать шаг выбора аксессуаров в полном процессе настройки.")]
    [SerializeField] private bool _showAccessoriesStep = true;

    [Tooltip("Если включено, пустые шаги автоматически пропускаются, например когда причёски ещё не назначены.")]
    [SerializeField] private bool _skipEmptySteps = true;

    [Space(8)]
    [Header("Одежда по умолчанию при первом открытии")]
    [Tooltip("Одежда, которая применяется и сохраняется, если гардероб героини открыт без сохранённой одежды.")]
    [SerializeField] private ClothingItem _defaultOutfitItem;

    [Tooltip("Причёска, которая применяется и сохраняется, если гардероб героини открыт без сохранённой причёски.")]
    [SerializeField] private ClothingItem _defaultHairItem;

    [Tooltip("Аксессуар, который применяется при первом открытии, если у игрока еще нет сохраненного аксессуара.")]
    [SerializeField] private ClothingItem _defaultAccessoryItem;

    [Tooltip("Если одежда или причёска по умолчанию не назначены, взять подходящий предмет из списков, чтобы слоёвое превью не открывалось пустым.")]
    [SerializeField] private bool _useFirstAvailableClothingAsFallback = true;

    [Header("Трансформ превью одежды по умолчанию")]
    [Tooltip("Использовать эти точные значения позиции и размера только для предмета одежды по умолчанию.")]
    [SerializeField] private bool _useDefaultOutfitPreviewTransform;
    [SerializeField] private Vector3 _defaultOutfitPreviewPosition;
    [Min(0f)]
    [SerializeField] private float _defaultOutfitPreviewWidth;
    [Min(0f)]
    [SerializeField] private float _defaultOutfitPreviewHeight;
    [SerializeField] private Vector3 _defaultOutfitPreviewScale = Vector3.one;

    [Header("Трансформ превью прически по умолчанию")]
    [Tooltip("Использовать эти точные значения позиции и размера только для причёски по умолчанию.")]
    [SerializeField] private bool _useDefaultHairPreviewTransform;
    [SerializeField] private Vector3 _defaultHairPreviewPosition;
    [Min(0f)]
    [SerializeField] private float _defaultHairPreviewWidth;
    [Min(0f)]
    [SerializeField] private float _defaultHairPreviewHeight;
    [SerializeField] private Vector3 _defaultHairPreviewScale = Vector3.one;

    [Tooltip("Если включено, выбор варианта в полном процессе сразу переводит игрока на следующий шаг.")]
    [SerializeField] private bool _advanceFullSetupOnOptionClick;

    [Tooltip("Если включено, после успешного полного процесса сохраняется флаг завершения. Его можно использовать, чтобы пропускать настройку при следующих открытиях.")]
    [SerializeField] private bool _saveCompletionFlag = true;

    [Tooltip("Если включено, полный процесс пропускается после первого завершения. Авторский debug-режим ниже игнорирует этот пропуск.")]
    [SerializeField] private bool _skipFullSetupWhenCompleted;

    [Tooltip("Ключ PlayerPrefs для флага завершения полного процесса. Можно сменить для отдельной истории.")]
    [SerializeField] private string _completionPrefsKey = DefaultCompletionPrefsKey;

    [Space(8)]
    [Header("Debug режим автора")]
    [Tooltip("Авторский режим: полный процесс показывается каждый раз, даже если игрок уже выбирал внешность, одежду и причёску.")]
    [SerializeField] private bool _debugAlwaysRunFullSetupOnOpen;

    [Tooltip("Авторский режим: когда wardrobePanel включается через SetActive(true), этот скрипт сам запускает полный процесс.")]
    [SerializeField] private bool _debugAutoStartFullSetupOnEnable;

    [Tooltip("Писать подробные сообщения в Console при открытии, смене шага и выборе варианта.")]
    [SerializeField] private bool _debugLog;

    [Space(8)]
    [Header("Предпросмотр в редакторе")]
    [Tooltip("Включает живой предпросмотр страницы прямо в Edit Mode. Скрипт пересобирает кнопки из текущих списков, но не сохраняет выбор в PlayerPrefs или GameState.")]
    [SerializeField, HideInInspector] private bool _editorPreviewEnabled;

    [Tooltip("Какой шаг показывать в предпросмотре: внешность, одежду или причёску.")]
    [SerializeField, HideInInspector] private WardrobeHeroSetupStep _editorPreviewStep = WardrobeHeroSetupStep.Appearance;

    [Tooltip("Какой вариант подсветить в предпросмотре. Это только визуальный выбор для автора, в игру он не записывается.")]
    [Min(0)]
    [SerializeField, HideInInspector] private int _editorPreviewSelectedIndex;

    [Tooltip("Только для Edit Mode. Если назначено, предпросмотр гардероба использует этот CharacterData вместо Target Character.")]
    [SerializeField, HideInInspector] private CharacterData _editorPreviewCharacterOverride;

    [Tooltip("Автоматически обновлять предпросмотр после изменений в инспекторе: списки, тексты, ссылки на префабы и спрайты.")]
    [SerializeField, HideInInspector] private bool _editorPreviewAutoRefresh;

    [Tooltip("При предпросмотре включать корневой объект страницы, если он был выключен. При скрытии предпросмотра скрипт не выключает его обратно, чтобы не потерять выделение объекта в сцене.")]
    [SerializeField, HideInInspector] private bool _editorPreviewActivatePageRoot = true;

    [Tooltip("При предпросмотре скрывать старые элементы гардероба из списка Hide While Setup Open, чтобы видеть только новую страницу.")]
    [SerializeField, HideInInspector] private bool _editorPreviewHideOldWardrobeObjects = true;

    [Tooltip("При предпросмотре скрывать UI истории и диалога из списка Hide Story Objects While Open. Обычно выключено, чтобы случайно не спрятать рабочую сцену автора.")]
    [SerializeField, HideInInspector] private bool _editorPreviewHideStoryObjects;

    [Space(8)]
    [Header("Шаг: внешность / национальность")]
    [Tooltip("Заголовок, который показывается на шаге выбора внешности.")]
    [SerializeField] private string _appearanceTitle = "Выберите внешность героини";

    [Tooltip("Описание, которое показывается под заголовком шага выбора внешности.")]
    [TextArea]
    [SerializeField] private string _appearanceDescription = "Этот выбор меняет AppearanceType. Персонажи с inheritAppearanceFromPlayer будут брать подходящие спрайты.";

    [Tooltip("Варианты внешности, которые будут показаны в полном процессе настройки.")]
    [SerializeField] private List<WardrobeHeroAppearanceOption> _appearanceOptions = new List<WardrobeHeroAppearanceOption>();

    [Tooltip("Если список внешности пустой, автоматически показать базовые варианты: европейская, азиатская и латиноамериканская.")]
    [SerializeField] private bool _useDefaultAppearanceOptionsWhenEmpty = true;

    [Space(8)]
    [Header("Шаг: одежда")]
    [Tooltip("Заголовок, который показывается на шаге выбора одежды.")]
    [SerializeField] private string _outfitTitle = "Выберите одежду";

    [Tooltip("Описание, которое показывается под заголовком шага выбора одежды.")]
    [TextArea]
    [SerializeField] private string _outfitDescription = "Список берется из ClothingItem с типом Outfit. Выбранный id сохраняется в GameState.";

    [Tooltip("Все доступные предметы одежды для полного процесса. Можно добавлять и Hair, неподходящие предметы будут отфильтрованы по типу.")]
    [SerializeField] private List<ClothingItem> _outfitItems = new List<ClothingItem>();

    [Space(8)]
    [Header("Шаг: прическа")]
    [Tooltip("Заголовок, который показывается на шаге выбора причёски.")]
    [SerializeField] private string _hairTitle = "Выберите прическу";

    [Tooltip("Описание, которое показывается под заголовком шага выбора причёски.")]
    [TextArea]
    [SerializeField] private string _hairDescription = "Список берется из ClothingItem с типом Hair. Выбранный id сохраняется отдельно от одежды.";

    [Tooltip("Все доступные причёски для полного процесса. Можно добавлять и Outfit, неподходящие предметы будут отфильтрованы по типу.")]
    [SerializeField] private List<ClothingItem> _hairItems = new List<ClothingItem>();

    [Space(8)]
    [Header("Шаг: аксессуары")]
    [Tooltip("Заголовок шага выбора аксессуаров.")]
    [SerializeField] private string _accessoriesTitle = "Выберите аксессуар";

    [Tooltip("Описание шага выбора аксессуаров.")]
    [TextArea]
    [SerializeField] private string _accessoriesDescription = "Список берется из ClothingItem с типом Accessory. Выбранный id сохраняется отдельно от одежды и прически.";

    [Tooltip("Все доступные аксессуары для полного процесса.")]
    [SerializeField] private List<ClothingItem> _accessoryItems = new List<ClothingItem>();

    [Space(8)]
    [Header("Куда сохранять одежду и прическу")]
    [Tooltip("Персонаж главной героини, для которого сохраняются одежда и причёска. Если поле пустое, сохранение всё равно пройдёт по Character Id ниже.")]
    [SerializeField] private CharacterData _targetCharacter;

    [Tooltip("Id главной героини для GameState, например hero. Если поле пустое и задан CharacterData, будет использовано имя asset.")]
    [SerializeField] private string _targetCharacterId = "hero";

    [Tooltip("Суффикс слота одежды. Итоговый ключ будет вида hero:outfit, чтобы одежда не перетирала причёску.")]
    [SerializeField] private string _outfitSlotSuffix = "outfit";

    [Tooltip("Суффикс слота причёски. Итоговый ключ будет вида hero:hair, чтобы причёска не перетирала одежду.")]
    [SerializeField] private string _hairSlotSuffix = "hair";

    [Tooltip("Суффикс слота аксессуара. Итоговый ключ будет вида hero:accessory.")]
    [SerializeField] private string _accessorySlotSuffix = "accessory";

    [Tooltip("Если включено, выбранный спрайт будет записан в targetCharacter.defaultSprite. Используй только если предметы являются полным спрайтом персонажа.")]
    [SerializeField] private bool _applySelectedSpriteToCharacterDefault;

    readonly List<RuntimeOption> _currentOptions = new List<RuntimeOption>();
    readonly List<WardrobeHeroSetupStep> _fullSetupSteps = new List<WardrobeHeroSetupStep>();

    Action _onComplete;
    Action _onCancel;
    Action<int> _storyChoiceCallback;
    WardrobeChoiceNode _activeStoryWardrobeNode;
    OpenMode _mode;
    int _stepIndex;
    int _selectedOptionIndex = -1;
    bool _isOpen;
    bool _buttonsBound;
    bool _stayOpenAfterOptionApply;
    bool _saveProgressOnComplete = true;
    RuntimeOption _lastAppliedOption;
    LayerDefaults _bodyLayerDefaults;
    LayerDefaults _outfitLayerDefaults;
    LayerDefaults _hairLayerDefaults;
    LayerDefaults _accessoryLayerDefaults;
    string _runtimeStoryId;
    string _runtimeChapterId;
    bool _hasRuntimeWardrobeAssets;
    CharacterData _runtimeTargetCharacter;
    string _runtimeTargetCharacterId;
    List<WardrobeHeroAppearanceOption> _runtimeAppearanceOptions;
    List<ClothingItem> _runtimeOutfitItems;
    List<ClothingItem> _runtimeHairItems;
    List<ClothingItem> _runtimeAccessoryItems;
    ClothingItem _runtimeDefaultOutfitItem;
    ClothingItem _runtimeDefaultHairItem;
    ClothingItem _runtimeDefaultAccessoryItem;

#if UNITY_EDITOR
    bool _editorPreviewQueued;
#endif

    public bool UseForStoryAppearanceChoices => _useForStoryAppearanceChoices;
    public bool UseForStoryWardrobeChoices => _useForStoryWardrobeChoices;
    public bool UseForOpenWardrobeNode => _useForOpenWardrobeNode;
    public bool UseGlobalInventoryInFullSetup => _useGlobalInventoryInFullSetup;
    public bool IsOpen => _isOpen;

    public event Action<WardrobeOptionSelectionInfo> OptionSelectionChanged;

    public int GetStoryContextScore(string storyId, string chapterId)
    {
        bool hasStoryBindings = HasBindingEntries(_storyIds);
        bool hasChapterBindings = HasBindingEntries(_chapterIds);
        bool storyMatches = MatchesAnyBinding(_storyIds, storyId);
        bool chapterMatches = MatchesAnyBinding(_chapterIds, chapterId);

        if (hasStoryBindings && hasChapterBindings)
            return storyMatches && chapterMatches ? 300 : 0;

        if (hasStoryBindings)
            return storyMatches ? 220 : 0;

        if (hasChapterBindings)
            return chapterMatches ? 160 : 0;

        return _useAsFallbackForUnmatchedStories ? 10 : 0;
    }

    public bool MatchesStoryContext(string storyId, string chapterId)
    {
        return GetStoryContextScore(storyId, chapterId) > 0;
    }

    public static WardrobeHeroSetupPage FindBestForCurrentStory(Transform searchRoot = null)
    {
        StoryManager manager = StoryManager.Instance;
        string storyId = manager != null ? manager.CurrentStoryId : "";
        string chapterId = manager != null ? FirstNonEmpty(manager.CurrentChapterId, manager.CurrentEpisodeId) : "";
        return FindBestForStory(searchRoot, storyId, chapterId);
    }

    public static WardrobeHeroSetupPage FindBestForStory(Transform searchRoot, string storyId, string chapterId)
    {
        return FindBestForStory(searchRoot, storyId, chapterId, null);
    }

    public static WardrobeHeroSetupPage FindBestForStory(
        Transform searchRoot,
        string storyId,
        string chapterId,
        Predicate<WardrobeHeroSetupPage> predicate)
    {
        WardrobeHeroSetupPage[] pages = searchRoot != null
            ? searchRoot.GetComponentsInChildren<WardrobeHeroSetupPage>(true)
            : FindObjectsOfType<WardrobeHeroSetupPage>(true);

        return FindBestForStory(pages, storyId, chapterId, predicate);
    }

    public static WardrobeHeroSetupPage FindBestForStory(IEnumerable<WardrobeHeroSetupPage> pages, string storyId, string chapterId)
    {
        return FindBestForStory(pages, storyId, chapterId, null);
    }

    public static WardrobeHeroSetupPage FindBestForStory(
        IEnumerable<WardrobeHeroSetupPage> pages,
        string storyId,
        string chapterId,
        Predicate<WardrobeHeroSetupPage> predicate)
    {
        if (pages == null)
            return null;

        WardrobeHeroSetupPage bestPage = null;
        int bestScore = 0;

        foreach (WardrobeHeroSetupPage page in pages)
        {
            if (page == null || !page.gameObject.scene.IsValid())
                continue;

            if (predicate != null && !predicate(page))
                continue;

            int score = page.GetStoryContextScore(storyId, chapterId);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestPage = page;
        }

        return bestPage;
    }

    public bool TryFindClothing(string id, ClothingType type, out ClothingItem item)
    {
        item = FindAllowedClothingInList(GetRuntimeOutfitItems(), id, type);
        if (item != null)
            return true;

        item = FindAllowedClothingInList(GetRuntimeHairItems(), id, type);
        if (item != null)
            return true;

        item = FindAllowedClothingInList(GetRuntimeAccessoryItems(), id, type);
        if (item != null)
            return true;

        item = FindDefaultClothing(id, type);
        return item != null;
    }

    enum OpenMode
    {
        FullSetup,
        StoryAppearanceChoice,
        StoryWardrobeChoice
    }

    sealed class RuntimeOption
    {
        public string Label;
        public Sprite Preview;
        public AppearanceType AppearanceType;
        public AppearanceVariant AppearanceVariant;
        public ClothingItem Clothing;
        public int PremiumCost;
        public int SourceIndex;
        public WardrobeHeroSetupStep Step;
    }

    struct LayerDefaults
    {
        public bool Captured;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public Vector3 LocalScale;
        public bool PreserveAspect;

        public static LayerDefaults Capture(Image image)
        {
            RectTransform rect = image.rectTransform;
            return new LayerDefaults
            {
                Captured = true,
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta,
                LocalScale = rect.localScale,
                PreserveAspect = image.preserveAspect
            };
        }
    }
}
