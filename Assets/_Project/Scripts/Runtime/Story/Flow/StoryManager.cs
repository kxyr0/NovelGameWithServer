using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Video;
using XNode;

public enum NarrationHeroHideMode
{
    Instant = 0,
    Fade = 1,
    None = 2
}

public enum InactiveSpeakerHideMode
{
    Instant = 0,
    Fade = 1,
    None = 2
}

[Serializable]
public struct StoryDialogueUserInterfaceFields
{
    [Tooltip("Основной DialogueUIManager для обычных реплик, выборов и системных сообщений этой истории.")]
    [SerializeField] private DialogueUIManager _dialogueUI;

    [Tooltip("Отдельный DialogueUIManager для катсцен. Если поле пустое, используется Cutscene User Interface, назначенный в StoryManager.")]
    [SerializeField] private DialogueUIManager _cutsceneUserInterface;

    [Header("Story UI Style")]
    [Tooltip("Reusable Story UI style for this story. Create it through Assets/Create/VN/UI/Story UI Style.")]
    [FormerlySerializedAs("_dialoguePanelStyle")]
    [SerializeField] private StoryUiStyle _storyUiStyle;

    [Tooltip("Быстрая замена Source Image у фона диалоговой плашки. Если задан и стиль, этот спрайт имеет приоритет над спрайтом из стиля.")]
    [SerializeField] private Sprite _dialogueBackgroundSprite;

    [Tooltip("Включи, если катсцены должны использовать отдельный стиль плашки. Если выключено, катсцены берут обычный стиль истории.")]
    [FormerlySerializedAs("_useSeparateCutsceneDialoguePanelStyle")]
    [SerializeField] private bool _useSeparateCutsceneStoryUiStyle;

    [Tooltip("Отдельный стиль фона диалоговой плашки для катсцен этой истории.")]
    [FormerlySerializedAs("_cutsceneDialoguePanelStyle")]
    [SerializeField] private StoryUiStyle _cutsceneStoryUiStyle;

    [Tooltip("Быстрая замена Source Image у фона плашки катсцен.")]
    [SerializeField] private Sprite _cutsceneDialogueBackgroundSprite;

    [Tooltip("UI-объекты, которые нужно включать при выборе этой истории, например альтернативные диалоговые плашки.")]
    [SerializeField] private List<GameObject> _enableWhenSelected;

    [Tooltip("UI-объекты, которые нужно выключать при выборе этой истории, например старые плашки другой истории.")]
    [SerializeField] private List<GameObject> _disableWhenSelected;

    public DialogueUIManager DialogueUI => _dialogueUI;
    public DialogueUIManager CutsceneUserInterface => _cutsceneUserInterface;

    public void GetStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        style = _storyUiStyle;
        backgroundSprite = _dialogueBackgroundSprite;
    }

    public bool TryGetStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        GetStoryUiStyle(out style, out backgroundSprite);
        return style != null || backgroundSprite != null;
    }

    public void GetCutsceneStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        if (_useSeparateCutsceneStoryUiStyle)
        {
            style = _cutsceneStoryUiStyle;
            backgroundSprite = _cutsceneDialogueBackgroundSprite;
            return;
        }

        GetStoryUiStyle(out style, out backgroundSprite);
    }

    public bool TryGetCutsceneStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        GetCutsceneStoryUiStyle(out style, out backgroundSprite);
        return style != null || backgroundSprite != null;
    }

    public void ApplyObjectToggles(bool selected)
    {
        SetObjectsActive(_enableWhenSelected, selected);
        SetObjectsActive(_disableWhenSelected, !selected);
    }

    static void SetObjectsActive(List<GameObject> objects, bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Count; i++)
        {
            GameObject target = objects[i];
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}

[Serializable]
public struct StoryWardrobeUserInterfaceFields
{
    [Tooltip("Корневой объект гардероба этой истории. Если назначен, DialogueUIManager будет открывать именно его.")]
    [SerializeField] private GameObject _wardrobePanel;

    [Tooltip("Конкретная WardrobeHeroSetupPage для этой истории. Удобно назначать, когда отдельный корневой объект не нужен.")]
    [SerializeField] private WardrobeHeroSetupPage _setupPage;

    [Tooltip("Если Wardrobe Panel не назначен, использовать GameObject из Setup Page как корень гардероба.")]
    [SerializeField] private bool _useSetupPageRootWhenPanelEmpty;

    public GameObject WardrobePanel
    {
        get
        {
            if (_wardrobePanel != null)
                return _wardrobePanel;

            return _useSetupPageRootWhenPanelEmpty && _setupPage != null
                ? _setupPage.gameObject
                : null;
        }
    }

    public WardrobeHeroSetupPage SetupPage => _setupPage;
}

[Serializable]
public sealed class StoryUserInterfaceProfile
{
    [Tooltip("Понятная подпись профиля для автора. На логику не влияет.")]
    [SerializeField] private string _label = "";

    [Tooltip("StoryData, для которой применяется этот UI-профиль. Это самый надёжный способ привязки.")]
    [SerializeField] private StoryData _storyAsset;

    [Tooltip("Story ID, storyName или имя asset истории. Используется, если Story Asset не назначен.")]
    [SerializeField] private List<string> _storyIds = new List<string>();

    [Tooltip("Настройки диалогового UI для этой истории: обычные реплики, катсцены и включение или выключение UI-объектов.")]
    [SerializeField] private StoryDialogueUserInterfaceFields _dialogueUserInterface;

    [Tooltip("Настройки гардероба для этой истории: отдельный корневой объект или конкретная WardrobeHeroSetupPage.")]
    [SerializeField] private StoryWardrobeUserInterfaceFields _wardrobeUserInterface;

    [Tooltip("Сценовый UI-компонент этой истории. Хранит ссылки телефона и другие scene-specific UI настройки, которые не должны лежать в StoryUiStyle asset.")]
    [SerializeField] private StoryUserInterface _storyUserInterface;

    [SerializeField, HideInInspector] private DialogueUIManager _dialogueUI;
    [SerializeField, HideInInspector] private DialogueUIManager _cutsceneUserInterface;
    [SerializeField, HideInInspector] private GameObject _wardrobePanel;
    [SerializeField, HideInInspector] private List<GameObject> _enableWhenSelected = new List<GameObject>();
    [SerializeField, HideInInspector] private List<GameObject> _disableWhenSelected = new List<GameObject>();

    public string Label => _label;
    public DialogueUIManager DialogueUI => _dialogueUserInterface.DialogueUI != null
        ? _dialogueUserInterface.DialogueUI
        : _dialogueUI;
    public DialogueUIManager CutsceneUserInterface => _dialogueUserInterface.CutsceneUserInterface != null
        ? _dialogueUserInterface.CutsceneUserInterface
        : _cutsceneUserInterface;
    public GameObject WardrobePanel => _wardrobeUserInterface.WardrobePanel != null
        ? _wardrobeUserInterface.WardrobePanel
        : _wardrobePanel;
    public StoryUserInterface StoryUserInterface => _storyUserInterface;

    public void GetStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        _dialogueUserInterface.GetStoryUiStyle(out style, out backgroundSprite);
    }

    public bool TryGetStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        return _dialogueUserInterface.TryGetStoryUiStyle(out style, out backgroundSprite);
    }

    public void GetCutsceneStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        _dialogueUserInterface.GetCutsceneStoryUiStyle(out style, out backgroundSprite);
    }

    public bool TryGetCutsceneStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        return _dialogueUserInterface.TryGetCutsceneStoryUiStyle(out style, out backgroundSprite);
    }

    public int GetMatchScore(StoryData story)
    {
        if (story == null)
            return 0;

        int score = 0;
        if (_storyAsset == story)
            score = 1000;

        string storyId = Normalize(story.storyId);
        string storyName = Normalize(story.storyName);
        string assetName = Normalize(story.name);

        if (MatchesAny(_storyIds, storyId))
            score = Mathf.Max(score, 300);
        if (MatchesAny(_storyIds, storyName))
            score = Mathf.Max(score, 200);
        if (MatchesAny(_storyIds, assetName))
            score = Mathf.Max(score, 100);

        return score;
    }

    public void ApplyObjectToggles(bool selected)
    {
        _dialogueUserInterface.ApplyObjectToggles(selected);
        SetObjectsActive(_enableWhenSelected, selected);
        SetObjectsActive(_disableWhenSelected, !selected);
    }

    static bool MatchesAny(List<string> values, string target)
    {
        if (values == null || string.IsNullOrEmpty(target))
            return false;

        for (int i = 0; i < values.Count; i++)
        {
            if (Normalize(values[i]) == target)
                return true;
        }

        return false;
    }

    static void SetObjectsActive(List<GameObject> objects, bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Count; i++)
        {
            GameObject target = objects[i];
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }

    static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }
}

public partial class StoryManager : MonoBehaviour
{
    private static readonly IReadOnlyList<ChapterData> EmptyChapters = System.Array.Empty<ChapterData>();
    private static readonly Dictionary<string, StoryGraph> JsonGraphCache = new Dictionary<string, StoryGraph>();
    private static readonly KeyCode[] KeyboardAdvanceKeys = BuildKeyboardAdvanceKeys();
    private const string ChapterBoundaryResumePrefix = "VN_STORY_BOUNDARY_";

    public static StoryManager Instance;

    public AudioSource musicSource;
    public AudioSource sfxSource;

    AudioClip currentMusic;

    public CharacterViewManager characterView;
    public BackgroundViewManager backgroundView;
    public DialogueUIManager dialogueUI;

    [Header("UI профили историй")]
    [SerializeField] private List<StoryUserInterfaceProfile> storyUserInterfaceProfiles = new List<StoryUserInterfaceProfile>();
    [SerializeField] private StoryInterfaceStyleCatalog storyInterfaceStyleCatalog;

    [Header("Катсцены")]
    [Tooltip("DialogueUIManager для катсцен. Назначай явно: автоматический поиск отключён, чтобы отсутствие UI сразу показывало ошибку.")]
    [SerializeField] private DialogueUIManager cutsceneUserInterface;
    [SerializeField, Min(0f)] private float cutsceneImageTextDelay = 0.6f;
    [Tooltip("Во время диалоговых катсцен двигать к говорящему только слой фона, чтобы ощущалось движение камеры, а не сдвиг персонажа сбоку.")]
    [SerializeField] private bool moveCutsceneBackgroundWithCamera = true;
    [SerializeField, Range(0f, 1f)] private float cutsceneBackgroundCameraStrength = 0.35f;
    [SerializeField, Min(0f)] private float cutsceneBackgroundPanDuration = 0.55f;
    [SerializeField] private bool resetCutsceneBackgroundCameraOnExit = true;

    DialogueNode activeDialogueNode;
    int currentLineIndex;
    readonly List<string> currentLinePages = new List<string>();
    int currentLinePageIndex;
    Coroutine cutsceneTextRevealRoutine;
    bool cutsceneTextRevealed;
    ImageNode activeCutsceneImageNode;
    DialogueLine activeCutsceneImageLine;
    int activeCutsceneImageEnterFrame = -1;
    bool cutsceneBackgroundSceneActive;
    DialogueUIManager defaultDialogueUI;
    DialogueUIManager defaultCutsceneUserInterface;
    GameObject defaultWardrobePanel;
    StoryUserInterface defaultStoryUserInterface;
    StoryUserInterfaceProfile activeStoryUserInterfaceProfile;

    public StoryGraph storyGraph;

    public StoryData storyData;

    int currentSeason = 0;
    int currentChapter = 0;
    int lastCompletedChapter = -1;
    int endPanelNextChapter = -1;
    bool endPanelStoryFinished;
    bool storySelected;
    bool suppressProgressPersistence;
    bool heroSetupStoryUiHidden;
    readonly List<StoryUiActiveState> heroSetupStoryUiStates = new List<StoryUiActiveState>();

    public bool HasSelectedStory => storySelected && storyData != null;
    public int CurrentSeasonIndex => currentSeason;
    public int CurrentChapterIndex => currentChapter;
    public int CurrentDialogueLineIndex => activeDialogueNode != null ? currentLineIndex : 0;
    public int CurrentDialoguePageIndex => currentLinePages.Count > 0 ? Mathf.Clamp(currentLinePageIndex, 0, currentLinePages.Count - 1) : 0;
    public int CurrentDialoguePageCount => currentLinePages.Count;
    public int CurrentDialoguePageVisibleCharCount => GetCurrentDialoguePageVisibleCharCount();
    public int CurrentDialogueLineVisibleCharCount => GetCurrentDialogueLineVisibleCharCount();
    public string CurrentStoryId => storyData != null
        ? (string.IsNullOrEmpty(storyData.storyId) ? storyData.name : storyData.storyId)
        : "";
    public string CurrentSeasonId => GetCurrentSeasonOrNull()?.seasonId ?? "";
    public string CurrentChapterId => GetCurrentChapterOrNull()?.chapterId ?? "";
    public string CurrentStoryTitle => storyData != null
        ? (!string.IsNullOrWhiteSpace(storyData.storyName) ? storyData.storyName : storyData.name)
        : "";
    public string CurrentChapterTitle => GetChapterDisplayName(GetCurrentChapterOrNull());
    public int CurrentChapterNumber => GetCurrentChapterOrNull() != null ? currentChapter + 1 : 0;
    public int StoryChapterCount => GetStoryChapters().Count;
    public bool EndPanelStoryFinished => endPanelStoryFinished;
    public bool EndPanelHasNextChapter => ResolveEndPanelNextChapterIndex() >= 0;
    public bool CanContinueFromEndPanel => EndPanelHasNextChapter;
    public int EndPanelNextChapterIndex => ResolveEndPanelNextChapterIndex();
    public int EndPanelNextChapterNumber => EndPanelHasNextChapter ? ResolveEndPanelNextChapterIndex() + 1 : 0;
    public string EndPanelNextChapterId => ResolveChapterEpisodeId(GetChapterAtIndexOrNull(ResolveEndPanelNextChapterIndex()));
    public string EndPanelNextChapterTitle => GetChapterDisplayName(GetChapterAtIndexOrNull(ResolveEndPanelNextChapterIndex()));
    public bool CanRestartCompletedChapter => GetChapterAtIndexOrNull(lastCompletedChapter) != null;
    public int LastCompletedChapterIndex => lastCompletedChapter;
    public int LastCompletedChapterNumber => CanRestartCompletedChapter ? lastCompletedChapter + 1 : 0;
    public string LastCompletedEpisodeId => ResolveChapterEpisodeId(GetChapterAtIndexOrNull(lastCompletedChapter));
    public string LastCompletedChapterTitle => GetChapterDisplayName(GetChapterAtIndexOrNull(lastCompletedChapter));
    public string CurrentEpisodeId
    {
        get
        {
            var graph = storyGraph ?? GetCurrentGraphOrNull();
            if (graph != null && !string.IsNullOrEmpty(graph.episodeId))
                return graph.episodeId;

            var chapter = GetCurrentChapterOrNull();
            if (chapter != null && !string.IsNullOrEmpty(chapter.chapterId))
                return chapter.chapterId;

            return graph != null ? graph.name : "";
        }
    }
    public ChapterTitleOverlay ChapterTitleOverlay => _chapterTitleOverlay;

    sealed class StoryUiActiveState
    {
        public GameObject Target;
        public bool WasActiveSelf;
    }

    readonly HashSet<string> _pendingChoiceSelections = new HashSet<string>();
    readonly HashSet<string> _pendingWardrobeSelections = new HashSet<string>();
    readonly HashSet<string> _pendingPremiumNodeSpends = new HashSet<string>();
    readonly HashSet<string> _pendingChapterPurchases = new HashSet<string>();

    public GameObject endStoryPanel;

    [Header("Ошибки подключения")]
    [Tooltip("Панель 'Нет подключения к серверу'. Должна содержать кнопку Retry, привязанную к StoryManager.RetryConnection().")]
    public GameObject noConnectionPanel;
    public TMP_Text townText;
    public TMP_Text reputationText;
    public TMP_Text storyText;
    public TMP_Text heartsText;
    public Button purchase;

    [Header("Навигация")]
    [Tooltip("MenuController, который используется для возврата в главное меню.")]
    public MenuController menuController;

    [Header("Магазин")]
    [Tooltip("GameObject панели магазина. Открывается, когда игроку не хватает валюты.")]
    public GameObject shopPanel;

    [Header("UI — доп. панели")]
    [Tooltip("Панель для показа картинки с подписью.")]
    public ImageOverlayUI imageOverlay;
    [Tooltip("Панель для показа телефонного диалога.")]
    public PhoneDialogueUI phoneDialogueUI;

    [Header("UI - заголовок главы")]
    [Tooltip("Показывает номер и название главы при входе в историю или при запуске следующей главы.")]
    [SerializeField] private ChapterTitleOverlay _chapterTitleOverlay;
    [SerializeField] private StatChangeOverlay _statChangeOverlay;

    [Header("Камера")]
    [Tooltip("Контроллер движения камеры. Используется для автоматического панорамирования при смене говорящего персонажа.")]
    public CameraController cameraController;

    [Tooltip("Автоматически двигать камеру к позиции говорящего персонажа при смене реплики.")]
    public bool autoPanToSpeaker = true;

    [Header("Активные персонажи")]
    [Tooltip("Если у DialogueNode пустой activeCharacters, собрать список автоматически по говорящим персонажам: героиня слева, остальные справа.")]
    [SerializeField] private bool autoBuildActiveCharacters = true;

    [Tooltip("Id или имя asset главной героини. В JSON-историях по умолчанию используется hero.")]
    [SerializeField] private string heroCharacterId = "hero";

    [Tooltip("Позиция главной героини при автоматической сборке activeCharacters.")]
    [SerializeField] private CharacterPosition heroCharacterPosition = CharacterPosition.Left;

    [Tooltip("Позиция всех персонажей, кроме главной героини, при автоматической сборке activeCharacters.")]
    [SerializeField] private CharacterPosition otherCharacterPosition = CharacterPosition.Right;

    [Header("Реплики рассказчика")]
    [Tooltip("Как скрывать видимых персонажей на действиях и репликах рассказчика без говорящего персонажа.")]
    [SerializeField] private NarrationHeroHideMode narrationHeroHideMode = NarrationHeroHideMode.Instant;

    [Tooltip("Панорамировать камеру к слоту героини, когда показывается действие или реплика рассказчика.")]
    [SerializeField] private bool panToHeroOnNarrationLines = false;

    [Tooltip("Длительность плавного скрытия персонажей на репликах рассказчика. Используется только если Narration Hero Hide Mode = Fade.")]
    [Min(0f)]
    [SerializeField] private float narrationHeroFadeDuration = 0.25f;

    [Header("Фокус на спикере")]
    [Tooltip("Когда персонаж говорит, скрывать остальные активные слоты, чтобы при панорамировании камеры старые персонажи не оставались у края экрана.")]
    [SerializeField] private InactiveSpeakerHideMode inactiveSpeakerHideMode = InactiveSpeakerHideMode.Instant;

    [Tooltip("Длительность плавного скрытия не говорящих персонажей. Используется только если Inactive Speaker Hide Mode = Fade.")]
    [Min(0f)]
    [SerializeField] private float inactiveSpeakerFadeDuration = 0.25f;

    [Header("История / Перемотка")]
    [Tooltip("StoryHistory хранит пройденные ноды для перемотки и закладок.")]
    public StoryHistory storyHistory;

    [Header("Ввод")]
    [Tooltip("Прозрачная область тапа для смены реплик. Если не назначена, переключение будет проверяться через Update.")]
    public DialogueTapHandler tapHandler;

    [Tooltip("Разрешить клавиатуре переключать страницы и реплики диалога.")]
    [SerializeField] private bool advanceDialogueWithKeyboard = true;

    [Tooltip("Если включено, любая обычная клавиша переключает диалог. Если выключено, работают только Space и Enter.")]
    [SerializeField] private bool advanceDialogueWithAnyKeyboardKey = true;

    [Tooltip("Не переключать диалог с клавиатуры, пока выбран TMP_InputField или обычный InputField.")]
    [SerializeField] private bool ignoreDialogueKeyboardInputWhenTyping = true;

    [Tooltip("Включить горячую клавишу перемотки обычного текста до следующего интерактивного выбора или setup-ноды.")]
    [SerializeField] private bool skipToNextChoiceWithKeyboard = true;

    [SerializeField] private KeyCode skipToNextChoiceKey = KeyCode.Tab;

    [Tooltip("Включить горячую клавишу пропуска обычного текста до следующей катсцены. Сама катсцена не пропускается.")]
    [SerializeField] private bool skipToNextCutsceneWithKeyboard = true;

    [SerializeField] private KeyCode skipToNextCutsceneKey = KeyCode.C;

    [SerializeField, Min(1)] private int skipToNextChoiceMaxNodes = 500;

    [Header("Телефон: быстрый переход")]
    [Tooltip("Включить горячую клавишу, которая безопасно проматывает текущую историю до ближайшей PhoneDialogueNode.")]
    [SerializeField] private bool jumpToPhoneWithKeyboard = true;

    [SerializeField] private KeyCode jumpToPhoneKey = KeyCode.P;

    [Tooltip("Индекс обычного выбора, который будет автоматически выбран при тестовом переходе к телефону. Premium варианты пропускаются, если не включён флаг ниже.")]
    [SerializeField, Min(0)] private int jumpToPhoneDefaultChoiceIndex = 0;

    [SerializeField] private bool jumpToPhoneAllowPremiumDefaultChoice;

    [SerializeField, Min(1)] private int jumpToPhoneMaxNodes = 1500;

    [Tooltip("Если указан GUID конкретной PhoneDialogueNode, переход остановится именно на ней. Если пусто, будет найдена ближайшая phone-нода.")]
    [SerializeField] private string jumpToPhoneTargetNodeGuid = "";

    [Header("Постраничный текст диалога")]
    [Tooltip("Делить длинный текст реплики на несколько страниц по тапам, не меняя данные истории.")]
    [SerializeField] private bool splitLongDialogueLines = true;

    [Tooltip("Максимум видимых символов за один тап. Если реплика длиннее, продолжение появится следующим тапом.")]
    [Min(1)]
    [SerializeField] private int maxDialogueCharsPerTap = 500;

    Coroutine skipToNextChoiceRoutine;
    Coroutine skipToNextCutsceneRoutine;
    Coroutine jumpToPhoneRoutine;
    bool isSkippingToNextChoice;
    bool isSkippingToNextCutscene;
    bool isJumpingToPhone;

}
