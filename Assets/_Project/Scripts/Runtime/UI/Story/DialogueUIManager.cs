using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;

public class DialogueUIManager : MonoBehaviour
{
    const string DefaultChoiceDialoguePlaceholder = "...";
    const string LegacyChoiceDialoguePlaceholder = "Выберите вариант.";

    public TMP_Text nameText;
    public TMP_Text dialogueText;

    public GameObject choiceButtonPrefab;
    [Tooltip("Префаб для платных вариантов выбора. Если пусто, используется обычный prefab кнопки выбора.")]
    public GameObject premiumChoiceButtonPrefab;
    public Transform choiceContainer;
    readonly Dictionary<Button, bool> dedicatedPremiumChoiceButtons = new Dictionary<Button, bool>();

    public GameObject wardrobePanel;

    [Header("Границы диалога")]
    [SerializeField] private RectTransform dialoguePanel;
    [SerializeField] private bool keepDialoguePanelOnScreen = true;
    [SerializeField] private bool allowDialoguePanelBelowScreen = true;
    [SerializeField] private bool keepStoryUiOutsideCameraRoot = true;
    [SerializeField] private Vector2 dialoguePanelPadding = new Vector2(12f, 12f);
    [SerializeField] private bool shrinkDialoguePanelToScreen = true;

    [Header("Стиль диалоговой плашки")]
    [Tooltip("Image фона диалоговой плашки. Если поле пустое, скрипт попробует взять Image с Dialogue Panel или дочернего объекта Background.")]
    [SerializeField] private Image dialogueBackgroundImage;
    [Tooltip("Автоматически искать Image фона, если Dialogue Background Image не назначен вручную.")]
    [SerializeField] private bool autoFindDialogueBackgroundImage = true;
    [Tooltip("Дополнительные Image-слои внутри DialoguePanel, например прозрачная подложка Background (1). Если стиль истории не использует доп. слои, они будут выключены.")]
    [SerializeField] private List<Image> dialogueExtraBackgroundImages = new List<Image>();

    [Header("Text transition")]
    [SerializeField] private bool animateTextChanges = true;
    [SerializeField, Min(0f)] private float textFadeOutDuration = 0.08f;
    [SerializeField, Min(0f)] private float textFadeInDuration = 0.14f;
    [SerializeField] private Ease textFadeEase = Ease.OutQuad;
    [SerializeField] private bool useUnscaledTextFade = true;

    [Header("Choices")]
    [SerializeField] private string choiceDialoguePlaceholder = "";
    [Tooltip("Групповой layout для кнопок выбора. Вешается на choiceContainer, то есть на родителя кнопок с VerticalLayoutGroup.")]
    [SerializeField] private DialogueChoiceLayout choiceLayout;
    [Tooltip("Если на choiceContainer есть DialogueChoiceLayout, он управляет общей шириной и высотой кнопок вместо старого выравнивания по длине текста.")]
    [SerializeField] private bool useDialogueChoiceLayout = true;
    [Tooltip("Ручная раскладка вариантов выбора по фактической высоте кнопок. Помогает, когда VerticalLayoutGroup не видит настоящую высоту prefab или варианты налезают друг на друга.")]
    [SerializeField] private ChoiceHeightSpacingLayout choiceHeightSpacingLayout;
    [Tooltip("Если включено, после создания вариантов выбора ChoiceHeightSpacingLayout выставляет позиции кнопок вручную по их реальной высоте.")]
    [SerializeField] private bool useChoiceHeightSpacingLayout = true;
    [Tooltip("Если на choiceContainer нет ChoiceHeightSpacingLayout, создать его во время игры автоматически.")]
    [SerializeField] private bool createChoiceHeightSpacingLayoutIfMissing = true;
    [SerializeField] private bool equalizeChoiceButtonsByVisibleLength = true;
    [SerializeField] private bool equalizeChoiceFontSizeByVisibleLength = true;
    [SerializeField] private bool adjustChoiceSpacingByVisibleCount = false;
    [SerializeField] private int compactChoiceSpacingThreshold = 3;
    [SerializeField] private float regularChoiceSpacing = 12.2f;
    [SerializeField] private float compactChoiceSpacing = -20f;

    [Header("Панель баланса платного выбора")]
    [SerializeField, InspectorName("Показывать панель")]
    [Tooltip("Если включено, при появлении хотя бы одного видимого платного выбора будет показана вручную назначенная панель баланса.")]
    private bool showPremiumChoiceBalancePanel = true;
    [SerializeField, InspectorName("Prefab панели по умолчанию")]
    [Tooltip("Fallback-prefab панели баланса, если в Story UI Style текущей истории не задан свой prefab.")]
    private GameObject premiumChoiceBalancePanelPrefab;
    [SerializeField, InspectorName("Сценовая панель баланса (fallback)")]
    [Tooltip("Необязательная уже созданная панель в сцене. Используется только если prefab не задан ни здесь, ни в Story UI Style.")]
    private GameObject premiumChoiceBalancePanel;
    [SerializeField, InspectorName("Текст текущего баланса")]
    [Tooltip("Fallback TMP_Text для сценовой панели. Для prefab-панели используйте PremiumChoiceBalancePanelView на самом prefab.")]
    private TMP_Text premiumChoiceBalanceText;
    [SerializeField, InspectorName("Иконка сердца")]
    [Tooltip("Fallback Image для сценовой панели. Для prefab-панели назначайте иконку внутри prefab.")]
    private Image premiumChoiceHeartIcon;
    [SerializeField, InspectorName("Формат текста баланса")]
    [Tooltip("Формат для текста баланса. {0} будет заменён текущим количеством сердец/искр. Для одного числа оставьте {0}.")]
    private string premiumChoiceBalanceTextFormat = "{0}";

    [Header("Speaker Placeholder")]
    [SerializeField] private string missingSpeakerPlaceholder = "";
    [Tooltip("Плашка имени говорящего. Можно назначить любой GameObject в сцене: сам nameplate, parent имени или отдельный контейнер.")]
    [SerializeField] private GameObject namePlateObject;
    [Tooltip("Image that draws the speaker nameplate. Story UI Style uses it for sprite, Image Type, size and position overrides.")]
    [SerializeField] private Image namePlateImage;
    [Tooltip("Если включено, Name Plate Object скрывается, когда у реплики нет имени говорящего, и показывается обратно, когда имя есть.")]
    [SerializeField] private bool hideNamePlateWhenSpeakerMissing = true;

    [Header("Подписочные кнопки")]
    [Tooltip("Кнопка перемотки на 5 реплик вперёд. Работает для игроков с подпиской.")]
    public Button fastForwardButton;
    [Tooltip("Кнопка пропуска обычных реплик до следующей катсцены. Сама катсцена не пропускается.")]
    [SerializeField] private Button skipToCutsceneButton;
    [Tooltip("Кнопка сохранения закладки. Работает для игроков с подпиской.")]
    public Button saveBookmarkButton;
    [Tooltip("Кнопка перехода к сохранённой закладке. Работает для игроков с подпиской.")]
    public Button goToBookmarkButton;

    public GameObject purchasePopup;
    public TMP_Text purchaseTitle;
    public TMP_Text purchasePrice;
    public Button buyButton;
    public Button cancelButton;

    Sequence textTransitionSequence;
    bool hasDialogueTextContent;
    RectTransform dialogueTextRect;
    RectTransform safeLayoutCapturedPanel;
    RectTransform safeLayoutCapturedText;
    Vector2 dialoguePanelBaseAnchoredPosition;
    Vector2 dialoguePanelBaseSizeDelta;
    Vector2 dialogueTextBaseSizeDelta;
    Vector2 dialogueTextBaseAnchoredPosition;
    float dialogueTextHorizontalPadding;
    TMP_FontAsset dialogueTextBaseFont;
    bool dialogueTextFontCaptured;
    bool dialogueSafeLayoutCaptured;
    StoryTextLayoutLock dialogueTextGrowDownLock;
    bool dialogueTextGrowDownTopOffsetCaptured;
    float dialogueTextGrowDownDefaultTopOffsetY;
    float dialogueTextGrowDownDefaultOffsetX;
    bool dialogueTextGrowDownDefaultResizeHeightToPreferredText;
    float dialogueTextGrowDownDefaultExtraHeight;
    float dialogueTextGrowDownDefaultMinHeight;
    float dialogueTextGrowDownDefaultMaxHeight;
    float dialogueTextGrowDownDefaultMaxFontSize;
    bool dialogueTextGrowDownDefaultShrinkTextToFitRect;
    float dialogueTextGrowDownDefaultMinAutoFontSize;
    TextOverflowModes dialogueTextGrowDownDefaultOverflowModeWhenStillTooLarge;
    bool wardrobeScreenModeActive;
    WardrobeHeroSetupPage activeWardrobeSetupPage;
    readonly List<NavigationCanvasState> wardrobeNavigationScreenStates = new List<NavigationCanvasState>();
    bool wardrobeNavigationScreensHidden;
    StoryUiStyle activeStoryUiStyle;
    GameObject activePremiumChoiceBalancePanelInstance;
    GameObject activePremiumChoiceBalancePanelPrefab;
    PremiumChoiceBalancePanelView activePremiumChoiceBalancePanelView;
    bool premiumChoiceBalancePanelVisible;
    Sprite activeDialogueBackgroundSpriteOverride;
    RectTransform characterNameRect;
    RectTransform safeLayoutCapturedNameText;
    Vector2 characterNameBaseAnchoredPosition;
    TMP_FontAsset characterNameBaseFont;
    float characterNameBaseFontSize;
    bool characterNameLayoutCaptured;
    UiImageDefaults dialogueBackgroundImageDefaults;
    UiRectDefaults dialogueBackgroundRectDefaults;
    UiImageDefaults choiceContainerImageDefaults;
    UiImageDefaults namePlateImageDefaults;
    UiRectDefaults namePlateRectDefaults;
    VerticalLayoutGroup dialoguePanelVerticalLayoutGroup;
    ContentSizeFitter dialoguePanelContentSizeFitter;
    UiVerticalLayoutGroupDefaults dialoguePanelVerticalLayoutDefaults;
    UiContentSizeFitterDefaults dialoguePanelContentSizeFitterDefaults;

    struct UiImageDefaults
    {
        public Image Target;
        public Sprite Sprite;
        public Color Color;
        public Image.Type Type;
        public bool PreserveAspect;
        public float PixelsPerUnitMultiplier;
        public Material Material;
        public bool RaycastTarget;
        public bool Captured;
    }

    struct UiRectDefaults
    {
        public RectTransform Target;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public LayoutElement LayoutElement;
        public bool LayoutElementIgnoreLayout;
        public bool Captured;
    }

    struct UiVerticalLayoutGroupDefaults
    {
        public VerticalLayoutGroup Target;
        public RectOffset Padding;
        public TextAnchor ChildAlignment;
        public float Spacing;
        public bool ReverseArrangement;
        public bool ChildControlWidth;
        public bool ChildControlHeight;
        public bool ChildScaleWidth;
        public bool ChildScaleHeight;
        public bool ChildForceExpandWidth;
        public bool ChildForceExpandHeight;
        public bool Captured;
    }

    struct UiContentSizeFitterDefaults
    {
        public ContentSizeFitter Target;
        public ContentSizeFitter.FitMode HorizontalFit;
        public ContentSizeFitter.FitMode VerticalFit;
        public bool Captured;
    }

    struct NavigationCanvasState
    {
        public CanvasGroup Group;
        public bool ActiveSelf;
        public float Alpha;
        public bool Interactable;
        public bool BlocksRaycasts;
        public bool IgnoreParentGroups;

        public NavigationCanvasState(CanvasGroup group)
        {
            Group = group;
            ActiveSelf = group != null && group.gameObject.activeSelf;
            Alpha = group != null ? group.alpha : 0f;
            Interactable = group != null && group.interactable;
            BlocksRaycasts = group != null && group.blocksRaycasts;
            IgnoreParentGroups = group != null && group.ignoreParentGroups;
        }
    }

    public GameObject DialoguePanelObject
    {
        get
        {
            AutoWireRequiredReferences();
            return dialoguePanel != null ? dialoguePanel.gameObject : null;
        }
    }

    public RectTransform DialoguePanelRect
    {
        get
        {
            AutoWireRequiredReferences();
            return dialoguePanel;
        }
    }

    public Image DialogueBackgroundImage
    {
        get
        {
            AutoWireRequiredReferences();
            return dialogueBackgroundImage;
        }
    }

    public RectTransform DialogueBackgroundRect
    {
        get
        {
            AutoWireRequiredReferences();
            return ResolveDialogueBackgroundRect();
        }
    }

    public IReadOnlyList<Image> DialogueExtraBackgroundImages
    {
        get
        {
            AutoWireRequiredReferences();
            return dialogueExtraBackgroundImages;
        }
    }

    public Image NamePlateImage
    {
        get
        {
            AutoWireRequiredReferences();
            return namePlateImage;
        }
    }

    public RectTransform NamePlateRect
    {
        get
        {
            AutoWireRequiredReferences();
            if (namePlateImage != null)
                return namePlateImage.rectTransform;

            return namePlateObject != null ? namePlateObject.transform as RectTransform : null;
        }
    }

    public GameObject ChoiceContainerObject => choiceContainer != null ? choiceContainer.gameObject : null;
    public VerticalLayoutGroup DialoguePanelVerticalLayoutGroup
    {
        get
        {
            AutoWireRequiredReferences();
            return FindDialoguePanelVerticalLayoutGroup();
        }
    }

    public ContentSizeFitter DialoguePanelContentSizeFitter
    {
        get
        {
            AutoWireRequiredReferences();
            return FindDialoguePanelContentSizeFitter();
        }
    }

    public GameObject WardrobePanelObject
    {
        get
        {
            AutoWireRequiredReferences();
            return wardrobePanel;
        }
    }

    public void SetWardrobePanel(GameObject panel)
    {
        if (wardrobePanel == panel)
            return;

        CloseWardrobe();
        wardrobePanel = panel;
        activeWardrobeSetupPage = null;
        AutoWireRequiredReferences();
    }

    public void ApplyStoryUiStyle(StoryUiStyle style, Sprite backgroundSpriteOverride = null)
    {
        AutoWireRequiredReferences();
        CaptureDialogueSafeLayout();
        RestoreDialoguePanelBaseLayout();
        activeStoryUiStyle = style;
        activeDialogueBackgroundSpriteOverride = backgroundSpriteOverride;
        CaptureDefaultDialogueBackgroundImage();
        CaptureDefaultDialogueBackgroundRect();
        CaptureDefaultChoiceContainerImage();
        CaptureDefaultNamePlateImage();
        CaptureDefaultNamePlateRect();
        CaptureDefaultDialoguePanelLayout();

        if (style == null || (!style.HasDialogueBackgroundSprite && backgroundSpriteOverride == null))
            RestoreDefaultDialogueBackgroundImage();
        if (style == null || !style.HasDialogueBackgroundRectOverrides)
            RestoreDefaultDialogueBackgroundRect();
        if (style == null)
            RestoreDefaultChoiceContainerImage();
        if (style == null || !style.HasNamePlateImageOverrides)
            RestoreDefaultNamePlateImage();
        if (style == null || !style.OverrideNamePlateRect)
            RestoreDefaultNamePlateRect();
        if (style == null || !style.OverrideDialoguePanelVerticalLayout)
            RestoreDefaultDialoguePanelVerticalLayout();
        if (style == null || !style.OverrideDialoguePanelContentSizeFitter)
            RestoreDefaultDialoguePanelContentSizeFitter();

        ReapplyActiveStoryUiStyle();
    }

    public void RefreshDialogueExtraBackgroundImagesFromScene()
    {
        AutoWireRequiredReferences();
        dialogueExtraBackgroundImages = FindDialogueExtraBackgroundImages();
    }

    public void RestoreDefaultStoryUiStyle()
    {
        AutoWireRequiredReferences();
        CaptureDialogueSafeLayout();
        RestoreDialoguePanelBaseLayout();
        activeStoryUiStyle = null;
        activeDialogueBackgroundSpriteOverride = null;
        CaptureDefaultDialogueBackgroundImage();
        RestoreDefaultDialogueBackgroundImage();
        CaptureDefaultDialogueBackgroundRect();
        RestoreDefaultDialogueBackgroundRect();
        CaptureDefaultChoiceContainerImage();
        RestoreDefaultChoiceContainerImage();
        CaptureDefaultNamePlateImage();
        RestoreDefaultNamePlateImage();
        CaptureDefaultNamePlateRect();
        RestoreDefaultNamePlateRect();
        CaptureDefaultDialoguePanelLayout();
        RestoreDefaultDialoguePanelVerticalLayout();
        RestoreDefaultDialoguePanelContentSizeFitter();
        ApplyActiveDialogueTextLayout();
        ApplyActiveCharacterNameLayout();
        ApplyActiveNamePlateStyle();
        RefreshDialogueBackgroundLayout();
        ApplyActiveDialoguePanelRect();
    }

    void ReapplyActiveStoryUiStyle()
    {
        AutoWireRequiredReferences();

        if (dialogueBackgroundImage != null)
        {
            if (activeStoryUiStyle != null)
                activeStoryUiStyle.ApplyTo(dialogueBackgroundImage);

            if (activeDialogueBackgroundSpriteOverride != null &&
                (activeStoryUiStyle == null || !activeStoryUiStyle.HasDialogueBackgroundSprite))
            {
                dialogueBackgroundImage.sprite = activeDialogueBackgroundSpriteOverride;
                dialogueBackgroundImage.SetAllDirty();
            }
        }

        ApplyActiveDialogueBackgroundRect();
        ApplyActiveDialogueExtraLayers();
        ApplyActiveNamePlateStyle();
        ApplyActiveChoicePanelStyle();
        RefreshVisiblePremiumChoiceBalancePanel();
        ApplyActiveStyleToVisibleChoiceButtons();
        ApplyActiveDialoguePanelLayout();
        ApplyActiveDialoguePanelRect();
        ApplyActiveDialogueTextLayout();
        ApplyActiveCharacterNameLayout();
        RefreshDialogueBackgroundLayout();
        ApplyActiveDialoguePanelRect();
        ApplyActiveDialogueTextLayout();
        ApplyActiveCharacterNameLayout();
        ApplyActiveDialoguePanelAutoLayout();
        ReapplyOverriddenDialogueBackgroundRect();
    }

    public void PreviewDialogueInterface(
        StoryUiStyle style,
        Sprite backgroundSpriteOverride,
        string speakerName,
        string bodyText)
    {
        ApplyStoryUiStyle(style, backgroundSpriteOverride);
        EnsureStoryUiVisible();
        ClearChoices();
        SetDialogueTexts(ReplacePlaceholdersSafe(speakerName), ReplacePlaceholdersSafe(bodyText), false);
        ClampDialoguePanelToSafeArea();
    }

    public bool PreviewChoiceInterface(
        StoryUiStyle style,
        Sprite backgroundSpriteOverride,
        IReadOnlyList<string> choices = null,
        bool clearDialogue = true)
    {
        ApplyStoryUiStyle(style, backgroundSpriteOverride);

        if (!EnsureChoiceUi())
            return false;

        EnsureStoryUiVisible();
        if (clearDialogue)
            ShowChoicePlaceholder();
        ClearChoices();
        PrepareChoiceContainer();

        choices ??= new[]
        {
            "Согласиться",
            "Задать вопрос",
            "Промолчать"
        };

        int visibleCount = 0;
        for (int i = 0; i < choices.Count; i++)
        {
            string labelText = choices[i];
            if (string.IsNullOrWhiteSpace(labelText))
                continue;

            Button button = CreateChoiceButton(out GameObject choiceRoot);
            if (button == null)
                continue;

            choiceRoot.name = "Interface Preview Choice";
            choiceRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            ClearChoiceButtonListeners(button);
            visibleCount++;

            TMP_Text label = FindChoiceButtonLabel(choiceRoot, button);
            if (label != null)
                label.text = ReplacePlaceholdersSafe(labelText);

            SetChoiceButtonCostText(choiceRoot, button, 0);
            ApplyActiveChoiceButtonStyle(button);
            RefreshChoiceButtonLayout(button);
        }

        ApplyChoiceLayout(visibleCount);
        ApplyActiveDialoguePanelRect();
        ReapplyOverriddenDialogueBackgroundRect();
        return visibleCount > 0;
    }

    public void HideInterfacePreview()
    {
        EndWardrobeScreenMode(true);
        ClearDialogue();
        ClearChoices();
        RestoreDefaultStoryUiStyle();
        SetDialoguePanelVisible(false);
    }

    public bool ValidateCutsceneUserInterface()
    {
        bool ok = true;

        if (dialoguePanel == null)
        {
            Debug.LogError("[DialogueUIManager] dialoguePanel is required for cutscene UI. Assign it explicitly; runtime cutscene UI fallback is disabled.", this);
            ok = false;
        }

        if (nameText == null)
        {
            Debug.LogError("[DialogueUIManager] nameText is required for cutscene UI.", this);
            ok = false;
        }

        if (dialogueText == null)
        {
            Debug.LogError("[DialogueUIManager] dialogueText is required for cutscene UI.", this);
            ok = false;
        }

        return ok;
    }

    void Awake()
    {
        AutoWireRequiredReferences();
        DetachStoryUiFromCameraRoot();
        CaptureDialogueSafeLayout();
        CaptureDialogueTextGrowDownDefault(FindDialogueTextGrowDownLock());
        if (nameText != null)
            CaptureCharacterNameLayout(nameText.rectTransform);
        ValidateRequiredReferences();
        ResetStoryUi();
    }

    void Start()
    {
        DetachStoryUiFromCameraRoot();

        if (fastForwardButton != null)
            fastForwardButton.onClick.AddListener(() => StoryManager.Instance?.FastForward());

        if (skipToCutsceneButton != null)
            skipToCutsceneButton.onClick.AddListener(() => StoryManager.Instance?.StartSkipToNextCutscene());

        if (saveBookmarkButton != null)
            saveBookmarkButton.onClick.AddListener(() => StoryManager.Instance?.SaveBookmark());

        if (goToBookmarkButton != null)
            goToBookmarkButton.onClick.AddListener(() => StoryManager.Instance?.GoToBookmark());
    }

    void OnEnable()
    {
        PlayerData.HeartsChanged += HandlePremiumChoiceHeartsChanged;
        RefreshPremiumChoiceBalanceText();
    }

    void OnValidate()
    {
        AutoWireRequiredReferences();
        dialoguePanelPadding.x = Mathf.Max(0f, dialoguePanelPadding.x);
        dialoguePanelPadding.y = Mathf.Max(0f, dialoguePanelPadding.y);
        textFadeOutDuration = Mathf.Max(0f, textFadeOutDuration);
        textFadeInDuration = Mathf.Max(0f, textFadeInDuration);
        if (dialogueBackgroundImageDefaults.Captured && dialogueBackgroundImageDefaults.Target != dialogueBackgroundImage)
            dialogueBackgroundImageDefaults = default;
        if (dialogueBackgroundRectDefaults.Captured && dialogueBackgroundRectDefaults.Target != ResolveDialogueBackgroundRect())
            dialogueBackgroundRectDefaults = default;
        if (choiceContainerImageDefaults.Captured && choiceContainerImageDefaults.Target != FindChoiceContainerBackgroundImage())
            choiceContainerImageDefaults = default;
        if (namePlateImageDefaults.Captured && namePlateImageDefaults.Target != ResolveNamePlateImage())
            namePlateImageDefaults = default;
        if (namePlateRectDefaults.Captured && namePlateRectDefaults.Target != ResolveNamePlateRect())
            namePlateRectDefaults = default;
    }

    void OnDisable()
    {
        PlayerData.HeartsChanged -= HandlePremiumChoiceHeartsChanged;
        KillTextTransition();
        SetDialogueTextAlpha(hasDialogueTextContent ? 1f : 0f);
    }

    void OnDestroy()
    {
        PlayerData.HeartsChanged -= HandlePremiumChoiceHeartsChanged;
        KillTextTransition();
    }

    void AutoWireRequiredReferences()
    {
        if (wardrobePanel == null)
        {
            var wardrobe = FindObjectOfType<WardrobeController>(true);
            if (wardrobe != null)
                wardrobePanel = wardrobe.gameObject;
        }

        if (dialoguePanel == null)
            dialoguePanel = FindDialoguePanel();

        if (autoFindDialogueBackgroundImage &&
            (dialogueBackgroundImage == null ||
             (dialoguePanel != null && !dialogueBackgroundImage.transform.IsChildOf(dialoguePanel)) ||
             IsDialogueExtraBackgroundImageName(dialogueBackgroundImage.name)))
        {
            dialogueBackgroundImage = FindDialogueBackgroundImage();
        }

        if (dialogueExtraBackgroundImages == null)
            dialogueExtraBackgroundImages = new List<Image>();

        if (namePlateImage == null)
            namePlateImage = ResolveNamePlateImage();
    }

    void DetachStoryUiFromCameraRoot()
    {
        if (!keepStoryUiOutsideCameraRoot)
            return;

        // Keep the scene hierarchy/render order exactly as authored.
    }

    bool ValidateRequiredReferences()
    {
        bool ok = true;

        if (nameText == null) { Debug.LogError("[DialogueUIManager] nameText is not assigned", this); ok = false; }
        if (dialogueText == null) { Debug.LogError("[DialogueUIManager] dialogueText is not assigned", this); ok = false; }
        if (choiceButtonPrefab == null) { Debug.LogError("[DialogueUIManager] choiceButtonPrefab is not assigned", this); ok = false; }
        if (choiceContainer == null) { Debug.LogError("[DialogueUIManager] choiceContainer is not assigned", this); ok = false; }
        if (wardrobePanel == null) { Debug.LogError("[DialogueUIManager] wardrobePanel is not assigned", this); ok = false; }

        return ok;
    }

    bool EnsureWardrobePanel()
    {
        AutoWireRequiredReferences();
        if (wardrobePanel != null) return true;

        Debug.LogError("[DialogueUIManager] wardrobePanel is required for wardrobe UI.", this);
        return false;
    }

    void SetWardrobePanelVisible(bool visible)
    {
        bool usesExternalStoryPage =
            activeWardrobeSetupPage != null &&
            wardrobePanel != null &&
            !activeWardrobeSetupPage.transform.IsChildOf(wardrobePanel.transform);

        if (!visible)
        {
            SetPanelCanvasVisible(wardrobePanel, false, true);

            if (usesExternalStoryPage)
                SetPanelCanvasVisible(activeWardrobeSetupPage.gameObject, false, true);

            activeWardrobeSetupPage = null;
            return;
        }

        if (usesExternalStoryPage)
        {
            SetPanelCanvasVisible(wardrobePanel, false, false);
            SetPanelCanvasVisible(activeWardrobeSetupPage.gameObject, true, false);
            return;
        }

        SetPanelCanvasVisible(wardrobePanel, true, false);
    }

    static void SetPanelCanvasVisible(GameObject panel, bool visible, bool deactivateWhenHidden)
    {
        if (panel == null)
            return;

        if (visible && !panel.activeSelf)
            panel.SetActive(true);

        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panel.AddComponent<CanvasGroup>();

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;

        if (!visible && deactivateWhenHidden && panel.activeSelf)
            panel.SetActive(false);
    }

    void SetDialoguePanelVisible(bool visible)
    {
        AutoWireRequiredReferences();
        if (dialoguePanel == null)
            return;

        if (!visible &&
            wardrobePanel != null &&
            wardrobePanel.activeInHierarchy &&
            wardrobePanel.transform.IsChildOf(dialoguePanel.transform))
        {
            Debug.LogWarning("[DialogueUIManager] Dialogue panel cannot be disabled because the wardrobe panel is inside it.", this);
            return;
        }

        if (dialoguePanel.gameObject.activeSelf != visible)
            dialoguePanel.gameObject.SetActive(visible);

        if (visible)
            ReapplyActiveStoryUiStyle();
    }

    void BeginWardrobeScreenMode(WardrobeHeroSetupPage setupPage = null)
    {
        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(DialogueUIManager),
            nameof(BeginWardrobeScreenMode),
            "[WARDROBE][UI] Beginning wardrobe screen mode.",
            LogMetadata.Of(
                "setupPage", setupPage != null ? setupPage.name : "",
                "wardrobePanel", wardrobePanel != null ? wardrobePanel.name : "",
                "dialoguePanel", dialoguePanel != null ? dialoguePanel.name : "",
                "wasActive", wardrobeScreenModeActive));

        StoryManager.Instance?.FadeOutStoryAudioForWardrobe();
        ClearDialogue();
        ClearChoices();
        activeWardrobeSetupPage = setupPage ?? FindWardrobeHeroSetupPage();
        CloseAllWardrobeHeroSetupPages(activeWardrobeSetupPage);
        AssignWardrobeCategoryTabs(activeWardrobeSetupPage);
        wardrobeScreenModeActive = true;
        SetWardrobePanelVisible(true);
        HideNavigationScreensForWardrobe();
        SetDialoguePanelVisible(false);
    }

    void EndWardrobeScreenMode(bool hideWardrobePanel, bool showDialoguePanel = true)
    {
        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(DialogueUIManager),
            nameof(EndWardrobeScreenMode),
            "[WARDROBE][UI] Ending wardrobe screen mode.",
            LogMetadata.Of(
                "hideWardrobePanel", hideWardrobePanel,
                "showDialoguePanel", showDialoguePanel,
                "wasActive", wardrobeScreenModeActive,
                "activeSetupPage", activeWardrobeSetupPage != null ? activeWardrobeSetupPage.name : "",
                "savedNavigationStates", wardrobeNavigationScreenStates != null ? wardrobeNavigationScreenStates.Count : 0));

        if (hideWardrobePanel)
            SetWardrobePanelVisible(false);

        RestoreNavigationScreensAfterWardrobe();
        wardrobeScreenModeActive = false;
        if (showDialoguePanel)
            SetDialoguePanelVisible(true);
    }

    void EnsureStoryUiVisible()
    {
        if (wardrobeScreenModeActive)
            EndWardrobeScreenMode(true);
        else
            SetDialoguePanelVisible(true);
    }

    void HideNavigationScreensForWardrobe()
    {
        GameObject setupRoot = activeWardrobeSetupPage != null ? activeWardrobeSetupPage.gameObject : null;
        StoryScreenNavigator navigator = GetComponentInParent<StoryScreenNavigator>(true);
        if (navigator == null)
            navigator = FindObjectOfType<StoryScreenNavigator>(true);

        UIScreenMarker[] markers = FindObjectsOfType<UIScreenMarker>(true);
        CaptureNavigationScreenStates(markers, setupRoot);

        AppLogger.Info(
            AppLogCategory.ScreenNavigation,
            nameof(DialogueUIManager),
            nameof(HideNavigationScreensForWardrobe),
            "[SCREEN][WARDROBE] Hiding navigation screens behind wardrobe overlay.",
            LogMetadata.Of(
                "hasNavigator", navigator != null,
                "markerCount", markers != null ? markers.Length : 0,
                "capturedStates", wardrobeNavigationScreenStates != null ? wardrobeNavigationScreenStates.Count : 0,
                "wardrobePanel", wardrobePanel != null ? wardrobePanel.name : "",
                "setupRoot", setupRoot != null ? setupRoot.name : ""));

        if (navigator != null)
            navigator.HideScreensForOverlay(wardrobePanel, setupRoot);

        for (int i = 0; i < markers.Length; i++)
        {
            UIScreenMarker marker = markers[i];
            if (marker == null || IsRelatedToWardrobeRoot(marker.transform, wardrobePanel, setupRoot))
                continue;

            CanvasGroup group = GetOrAddCanvasGroup(marker.gameObject);

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    void RestoreNavigationScreensAfterWardrobe()
    {
        StoryScreenNavigator navigator = GetComponentInParent<StoryScreenNavigator>(true);
        if (navigator == null)
            navigator = FindObjectOfType<StoryScreenNavigator>(true);

        AppLogger.Info(
            AppLogCategory.ScreenNavigation,
            nameof(DialogueUIManager),
            nameof(RestoreNavigationScreensAfterWardrobe),
            "[SCREEN][WARDROBE] Restoring navigation screens after wardrobe overlay.",
            LogMetadata.Of(
                "hasNavigator", navigator != null,
                "screensHidden", wardrobeNavigationScreensHidden,
                "capturedStates", wardrobeNavigationScreenStates != null ? wardrobeNavigationScreenStates.Count : 0));

        if (navigator != null)
            navigator.RestoreScreensAfterOverlay();

        if (!wardrobeNavigationScreensHidden)
            return;

        for (int i = 0; i < wardrobeNavigationScreenStates.Count; i++)
            RestoreNavigationCanvasState(wardrobeNavigationScreenStates[i]);

        wardrobeNavigationScreenStates.Clear();
        wardrobeNavigationScreensHidden = false;
    }

    void CaptureNavigationScreenStates(UIScreenMarker[] markers, GameObject setupRoot)
    {
        if (!wardrobeNavigationScreensHidden)
        {
            wardrobeNavigationScreenStates.Clear();
            wardrobeNavigationScreensHidden = true;
        }

        if (markers == null)
            return;

        for (int i = 0; i < markers.Length; i++)
        {
            UIScreenMarker marker = markers[i];
            if (marker == null || IsRelatedToWardrobeRoot(marker.transform, wardrobePanel, setupRoot))
                continue;

            CaptureNavigationCanvasState(GetOrAddCanvasGroup(marker.gameObject));
        }
    }

    void CaptureNavigationCanvasState(CanvasGroup group)
    {
        if (group == null)
            return;

        for (int i = 0; i < wardrobeNavigationScreenStates.Count; i++)
        {
            if (wardrobeNavigationScreenStates[i].Group == group)
                return;
        }

        wardrobeNavigationScreenStates.Add(new NavigationCanvasState(group));
    }

    static void RestoreNavigationCanvasState(NavigationCanvasState state)
    {
        CanvasGroup group = state.Group;
        if (group == null)
            return;

        if (state.ActiveSelf && !group.gameObject.activeSelf)
            group.gameObject.SetActive(true);

        group.alpha = state.Alpha;
        group.interactable = state.Interactable;
        group.blocksRaycasts = state.BlocksRaycasts;
        group.ignoreParentGroups = state.IgnoreParentGroups;

        if (!state.ActiveSelf && group.gameObject.activeSelf)
            group.gameObject.SetActive(false);
    }

    static CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        if (target == null)
            return null;

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.AddComponent<CanvasGroup>();

        return group;
    }

    static bool IsRelatedToWardrobeRoot(Transform target, params GameObject[] wardrobeRoots)
    {
        if (target == null || wardrobeRoots == null)
            return false;

        for (int i = 0; i < wardrobeRoots.Length; i++)
        {
            GameObject root = wardrobeRoots[i];
            if (root == null)
                continue;

            Transform rootTransform = root.transform;
            if (rootTransform == null)
                continue;

            if (target == rootTransform ||
                target.IsChildOf(rootTransform) ||
                rootTransform.IsChildOf(target))
            {
                return true;
            }
        }

        return false;
    }

    bool EnsureDialogueText()
    {
        if (nameText != null && dialogueText != null)
            return true;

        Debug.LogError("[DialogueUIManager] dialogue text references are not assigned.", this);
        return false;
    }

    bool EnsureChoiceUi()
    {
        if (ResolveChoiceButtonPrefab() != null && choiceContainer != null)
            return true;

        Debug.LogError("[DialogueUIManager] choice button prefab and choiceContainer are required for choices.", this);
        return false;
    }

    GameObject ResolveChoiceButtonPrefab(bool premiumChoice = false)
    {
        SyncActiveStoryUiStyleFromStoryManager();

        if (premiumChoice)
        {
            if (activeStoryUiStyle != null && activeStoryUiStyle.PremiumChoiceButtonPrefabOverride != null)
                return activeStoryUiStyle.PremiumChoiceButtonPrefabOverride;

            if (premiumChoiceButtonPrefab != null)
                return premiumChoiceButtonPrefab;
        }

        if (activeStoryUiStyle != null && activeStoryUiStyle.ChoiceButtonPrefabOverride != null)
            return activeStoryUiStyle.ChoiceButtonPrefabOverride;

        return choiceButtonPrefab;
    }

    bool SyncActiveStoryUiStyleFromStoryManager()
    {
        if (!Application.isPlaying)
            return false;

        StoryManager manager = StoryManager.Instance;
        if (manager == null || manager.dialogueUI != this || !manager.HasSelectedStory)
            return false;

        manager.TryResolveCurrentStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite);
        if (style == activeStoryUiStyle && backgroundSprite == activeDialogueBackgroundSpriteOverride)
            return false;

        ApplyStoryUiStyle(style, backgroundSprite);
        return true;
    }

    Button CreateChoiceButton(out GameObject root)
    {
        return CreateChoiceButton(out root, false);
    }

    Button CreateChoiceButton(out GameObject root, bool premiumChoice)
    {
        root = null;
        GameObject prefab = ResolveChoiceButtonPrefab(premiumChoice);
        if (prefab == null || choiceContainer == null)
            return null;

        bool usesDedicatedPremiumPrefab = premiumChoice && HasDedicatedPremiumChoiceButtonPrefab();
        root = Instantiate(prefab, choiceContainer);
        Button button = FindChoiceButtonComponent(root);
        if (button != null)
        {
            if (usesDedicatedPremiumPrefab)
                dedicatedPremiumChoiceButtons[button] = true;
            else
                dedicatedPremiumChoiceButtons.Remove(button);

            PrepareChoiceButton(button);
            return button;
        }

        Debug.LogError("[DialogueUIManager] choiceButtonPrefab must contain a child named Button with a Button component.", root);
        if (Application.isPlaying)
            Destroy(root);
        else
            DestroyImmediate(root);

        root = null;
        return null;
    }

    bool HasDedicatedPremiumChoiceButtonPrefab()
    {
        return premiumChoiceButtonPrefab != null ||
               (activeStoryUiStyle != null && activeStoryUiStyle.PremiumChoiceButtonPrefabOverride != null);
    }

    static Button FindChoiceButtonComponent(GameObject root)
    {
        if (root == null)
            return null;

        Transform namedButton = FindDescendantByName(root.transform, "Button");
        if (namedButton != null)
        {
            Button button = namedButton.GetComponent<Button>();
            if (button != null)
                return button;
        }

        return root.GetComponentInChildren<Button>(true);
    }

    static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        if (string.Equals(root.name, targetName, System.StringComparison.Ordinal))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindDescendantByName(root.GetChild(i), targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    static void PrepareChoiceButton(Button button)
    {
        if (button == null)
            return;

        if (button.targetGraphic == null)
        {
            Image image = button.GetComponent<Image>();
            if (image != null)
                button.targetGraphic = image;
        }

        if (button.targetGraphic != null)
            button.targetGraphic.raycastTarget = true;

        button.interactable = true;
    }

    static void ClearChoiceButtonListeners(Button button)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
    }

    static void RegisterChoiceButtonClick(Button button, UnityAction action)
    {
        if (button == null || action == null)
            return;

        PrepareChoiceButton(button);
        button.onClick.AddListener(action);
    }

    static TMP_Text FindChoiceButtonLabel(GameObject root, Button button)
    {
        TMP_Text bodyText = FindChoiceButtonTextByName(root, button, "BodyText");
        if (bodyText != null)
            return bodyText;

        TMP_Text label = FindFirstChoiceText(root, "CostText");
        if (label != null)
            return label;

        label = FindFirstChoiceText(button != null ? button.gameObject : null, "CostText");
        if (label != null)
            return label;

        return root != null
            ? root.GetComponentInChildren<TMP_Text>(true)
            : button != null
                ? button.GetComponentInChildren<TMP_Text>(true)
                : null;
    }

    static TMP_Text FindChoiceButtonCostText(GameObject root, Button button)
    {
        return FindChoiceButtonTextByName(root, button, "CostText");
    }

    static TMP_Text FindChoiceButtonTextByName(GameObject root, Button button, string textObjectName)
    {
        Transform target = root != null ? FindDescendantByName(root.transform, textObjectName) : null;
        if (target == null && button != null)
            target = FindDescendantByName(button.transform, textObjectName);

        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    static TMP_Text FindFirstChoiceText(GameObject root, string excludedName)
    {
        if (root == null)
            return null;

        TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null)
                continue;

            if (!string.IsNullOrEmpty(excludedName) &&
                string.Equals(label.gameObject.name, excludedName, System.StringComparison.Ordinal))
                continue;

            return label;
        }

        return null;
    }

    static void SetChoiceButtonCostText(GameObject root, Button button, int cost)
    {
        TMP_Text costText = FindChoiceButtonCostText(root, button);
        if (costText == null)
            return;

        cost = SaveDataSanitizer.ClampCurrencyValue(cost);
        bool visible = cost > 0;
        costText.text = visible ? cost.ToString() : "";
        costText.gameObject.SetActive(visible);
    }

    static bool IsPaidChoiceForBalancePanel(ChoiceOption option)
    {
        return option != null && option.isPremium && option.premiumCost > 0;
    }

    void HandlePremiumChoiceHeartsChanged(int hearts)
    {
        SetPremiumChoiceBalanceText(hearts);
    }

    void RefreshPremiumChoiceBalanceText()
    {
        SetPremiumChoiceBalanceText(PlayerData.Hearts);
    }

    void SetPremiumChoiceBalanceText(int hearts)
    {
        if (activePremiumChoiceBalancePanelView != null)
            activePremiumChoiceBalancePanelView.SetBalance(hearts);

        if (premiumChoiceBalanceText == null)
            return;

        string format = string.IsNullOrWhiteSpace(premiumChoiceBalanceTextFormat)
            ? "{0}"
            : premiumChoiceBalanceTextFormat;

        try
        {
            premiumChoiceBalanceText.text = string.Format(format, SaveDataSanitizer.ClampCurrencyValue(hearts));
        }
        catch (System.FormatException)
        {
            premiumChoiceBalanceText.text = SaveDataSanitizer.ClampCurrencyValue(hearts).ToString();
        }
    }

    void SetPremiumChoiceBalancePanelVisible(bool visible)
    {
        bool shouldShow = showPremiumChoiceBalancePanel && visible;
        premiumChoiceBalancePanelVisible = shouldShow;

        if (shouldShow)
        {
            GameObject panel = ResolvePremiumChoiceBalancePanelObject();
            if (panel != null)
            {
                PremiumChoiceBalancePanelView view = ResolvePremiumChoiceBalancePanelView(panel);
                if (view != null)
                    view.SetVisible(true);
                else
                    SetActiveIfDifferent(panel, true);
            }

            RefreshPremiumChoiceBalanceText();
            return;
        }

        HidePremiumChoiceBalancePanel();
    }

    void RefreshVisiblePremiumChoiceBalancePanel()
    {
        if (!premiumChoiceBalancePanelVisible)
            return;

        HidePremiumChoiceBalancePanel();
        SetPremiumChoiceBalancePanelVisible(true);
    }

    GameObject ResolvePremiumChoiceBalancePanelObject()
    {
        GameObject prefab = ResolvePremiumChoiceBalancePanelPrefab();
        if (prefab != null)
            return EnsurePremiumChoiceBalancePanelInstance(prefab);

        DestroyPremiumChoiceBalancePanelInstance();
        activePremiumChoiceBalancePanelView = ResolvePremiumChoiceBalancePanelView(premiumChoiceBalancePanel);
        return premiumChoiceBalancePanel;
    }

    GameObject ResolvePremiumChoiceBalancePanelPrefab()
    {
        if (activeStoryUiStyle != null && activeStoryUiStyle.PremiumChoiceBalancePanelPrefabOverride != null)
            return activeStoryUiStyle.PremiumChoiceBalancePanelPrefabOverride;

        return premiumChoiceBalancePanelPrefab;
    }

    GameObject EnsurePremiumChoiceBalancePanelInstance(GameObject prefab)
    {
        if (prefab == null)
            return null;

        if (activePremiumChoiceBalancePanelInstance != null &&
            activePremiumChoiceBalancePanelPrefab != prefab)
        {
            DestroyPremiumChoiceBalancePanelInstance();
        }

        if (activePremiumChoiceBalancePanelInstance == null)
        {
            Transform parent = ResolvePremiumChoiceBalancePanelParent();
            activePremiumChoiceBalancePanelInstance = Instantiate(prefab, parent, false);
            activePremiumChoiceBalancePanelInstance.name = prefab.name + " (Premium Choice Balance)";
            activePremiumChoiceBalancePanelPrefab = prefab;
            PlacePremiumChoiceBalancePanel(activePremiumChoiceBalancePanelInstance.transform);
        }

        activePremiumChoiceBalancePanelView = ResolvePremiumChoiceBalancePanelView(activePremiumChoiceBalancePanelInstance);
        SetActiveIfDifferent(activePremiumChoiceBalancePanelInstance, true);
        return activePremiumChoiceBalancePanelInstance;
    }

    Transform ResolvePremiumChoiceBalancePanelParent()
    {
        if (choiceContainer != null && choiceContainer.parent != null)
            return choiceContainer.parent;

        return choiceContainer;
    }

    void PlacePremiumChoiceBalancePanel(Transform panel)
    {
        if (panel == null || choiceContainer == null || panel.parent != choiceContainer.parent)
            return;

        panel.SetSiblingIndex(choiceContainer.GetSiblingIndex());

        if (panel is RectTransform rect && activeStoryUiStyle != null)
            rect.anchoredPosition += activeStoryUiStyle.PremiumChoiceBalancePanelOffset;
    }

    PremiumChoiceBalancePanelView ResolvePremiumChoiceBalancePanelView(GameObject panel)
    {
        return panel != null ? panel.GetComponent<PremiumChoiceBalancePanelView>() : null;
    }

    void HidePremiumChoiceBalancePanel()
    {
        if (activePremiumChoiceBalancePanelView != null)
            activePremiumChoiceBalancePanelView.SetVisible(false);

        DestroyPremiumChoiceBalancePanelInstance();

        SetActiveIfDifferent(premiumChoiceBalancePanel, false);

        if (premiumChoiceBalanceText != null)
            SetActiveIfDifferent(premiumChoiceBalanceText.gameObject, false);

        if (premiumChoiceHeartIcon != null)
            SetActiveIfDifferent(premiumChoiceHeartIcon.gameObject, false);
    }

    void DestroyPremiumChoiceBalancePanelInstance()
    {
        if (activePremiumChoiceBalancePanelInstance != null)
            DestroyUiObject(activePremiumChoiceBalancePanelInstance);

        activePremiumChoiceBalancePanelInstance = null;
        activePremiumChoiceBalancePanelPrefab = null;
        activePremiumChoiceBalancePanelView = null;
    }

    static void SetActiveIfDifferent(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    static string ReplacePlaceholdersSafe(string value)
    {
        string resolved = DialogueVariableResolver.ResolveText(
            value ?? "",
            DialogueVariableContext.StoryUi(nameof(DialogueUIManager)));
        return SafeTextSanitizer.SanitizeStoryText(resolved);
    }

    static bool IsVisualPlaceholder(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        string trimmed = value.Trim();
        return trimmed == "." ||
               trimmed == "..." ||
               trimmed == "\u2026";
    }

    void SetDialogueTexts(string speakerName, string bodyText, bool animate = true)
    {
        if (!EnsureDialogueText())
            return;

        speakerName ??= "";
        bodyText ??= "";

        bool hasContent = !string.IsNullOrEmpty(speakerName) || !string.IsNullOrEmpty(bodyText);
        bool textChanged = nameText.text != speakerName || dialogueText.text != bodyText;

        if (!textChanged)
        {
            KillTextTransition();
            hasDialogueTextContent = hasContent;
            SetDialogueTextAlpha(hasContent ? 1f : 0f);
            ApplyActiveCharacterNameLayout();
            ClampDialoguePanelToSafeArea();
            ReapplyOverriddenDialogueBackgroundRect();
            return;
        }

        if (!animate || !CanAnimateTextChange())
        {
            SetDialogueTextsInstant(speakerName, bodyText, hasContent);
            return;
        }

        KillTextTransition();

        float fadeOutDuration = hasDialogueTextContent ? textFadeOutDuration : 0f;
        float fadeInDuration = hasContent ? textFadeInDuration : 0f;

        if (fadeOutDuration <= 0f && fadeInDuration <= 0f)
        {
            SetDialogueTextsInstant(speakerName, bodyText, hasContent);
            return;
        }

        textTransitionSequence = DOTween.Sequence().SetUpdate(useUnscaledTextFade);

        if (fadeOutDuration > 0f)
            textTransitionSequence.Append(BuildTextFadeTween(0f, fadeOutDuration));
        else
            textTransitionSequence.AppendCallback(() => SetDialogueTextAlpha(0f));

        textTransitionSequence.AppendCallback(() =>
        {
            ApplyDialogueTexts(speakerName, bodyText);
            hasDialogueTextContent = hasContent;
            ClampDialoguePanelToSafeArea();
        });

        if (fadeInDuration > 0f)
            textTransitionSequence.Append(BuildTextFadeTween(1f, fadeInDuration));
        else
            textTransitionSequence.AppendCallback(() => SetDialogueTextAlpha(hasContent ? 1f : 0f));

        textTransitionSequence.OnComplete(() =>
        {
            SetDialogueTextAlpha(hasContent ? 1f : 0f);
            textTransitionSequence = null;
            ClampDialoguePanelToSafeArea();
        });
    }

    void SetDialogueTextsInstant(string speakerName, string bodyText, bool hasContent)
    {
        KillTextTransition();
        ApplyDialogueTexts(speakerName, bodyText);
        hasDialogueTextContent = hasContent;
        SetDialogueTextAlpha(hasContent ? 1f : 0f);
        ClampDialoguePanelToSafeArea();
    }

    void ApplyDialogueTexts(string speakerName, string bodyText)
    {
        if (nameText != null)
            nameText.text = speakerName ?? "";

        if (dialogueText != null)
            dialogueText.text = bodyText ?? "";

        ApplyNamePlateVisibility(speakerName);
        ApplyActiveCharacterNameLayout();
        ReapplyOverriddenDialogueBackgroundRect();
    }

    void ApplyNamePlateVisibility(string speakerName)
    {
        GameObject plateObject = namePlateObject != null
            ? namePlateObject
            : (ResolveNamePlateImage() != null ? ResolveNamePlateImage().gameObject : null);

        if (!hideNamePlateWhenSpeakerMissing || plateObject == null)
            return;

        bool shouldShow = !string.IsNullOrWhiteSpace(speakerName);
        if (plateObject.activeSelf != shouldShow)
            plateObject.SetActive(shouldShow);
    }

    bool CanAnimateTextChange()
    {
        return animateTextChanges &&
               Application.isPlaying &&
               gameObject.activeInHierarchy &&
               nameText != null &&
               dialogueText != null &&
               nameText.gameObject.activeInHierarchy &&
               dialogueText.gameObject.activeInHierarchy &&
               (textFadeOutDuration > 0f || textFadeInDuration > 0f);
    }

    Tween BuildTextFadeTween(float alpha, float duration)
    {
        var fade = DOTween.Sequence().SetUpdate(useUnscaledTextFade);

        if (nameText != null)
            fade.Join(nameText.DOFade(alpha, duration).SetEase(textFadeEase));

        if (dialogueText != null)
            fade.Join(dialogueText.DOFade(alpha, duration).SetEase(textFadeEase));

        return fade;
    }

    void SetDialogueTextAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (nameText != null)
            nameText.alpha = alpha;

        if (dialogueText != null)
            dialogueText.alpha = alpha;
    }

    void KillTextTransition()
    {
        if (textTransitionSequence == null)
            return;

        textTransitionSequence.Kill();
        textTransitionSequence = null;
    }

    string ResolveSpeakerName(DialogueLine line, DialogueIdentityResult identity = null, string bodyText = "")
    {
        if (identity == null &&
            line != null &&
            (line.speaker != null || !string.IsNullOrWhiteSpace(line.speakerId)))
        {
            identity = DialogueIdentity.ResolveSpeaker(new DialogueIdentityRequest
            {
                Line = line,
                BodyText = string.IsNullOrEmpty(bodyText) ? line.richText : bodyText,
                SourceObject = gameObject
            });
        }

        if (identity != null && !string.IsNullOrWhiteSpace(identity.DisplayName))
            return identity.DisplayName;

        if (line != null && line.speaker != null)
            return ReplacePlaceholdersSafe(line.speaker.characterName);

        string placeholder = ReplacePlaceholdersSafe(missingSpeakerPlaceholder);
        return IsVisualPlaceholder(placeholder) ? "" : placeholder;
    }

    public void ShowLine(DialogueLine line)
    {
        if (!EnsureDialogueText())
            return;

        EnsureStoryUiVisible();
        string bodyText = ReplacePlaceholdersSafe(line != null ? line.richText : "");
        SetDialogueTexts(ResolveSpeakerName(line, null, bodyText), bodyText);
    }

    public void ShowLineText(DialogueLine line, string resolvedRichText, DialogueIdentityResult identity = null, bool animate = true)
    {
        if (!EnsureDialogueText())
            return;

        EnsureStoryUiVisible();
        string bodyText = SafeTextSanitizer.SanitizeStoryText(resolvedRichText ?? "");
        SetDialogueTexts(ResolveSpeakerName(line, identity, bodyText), bodyText, animate);
    }

    public void ClearDialogue()
    {
        SetDialogueTexts("", "", false);
    }

    public void HideDialoguePanelForCutsceneIntro()
    {
        ClearDialogue();
        ClearChoices();
        SetDialoguePanelVisible(false);
    }

    public void ShowSystemMessage(string text)
    {
        string message = ReplacePlaceholdersSafe(text ?? "");
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (ToastManager.Instance != null)
        {
            ToastManager.Instance.ShowSystemMessage(message);
            return;
        }

        Debug.Log("[DialogueUIManager] " + message, this);
    }

    public void ShowWardrobeSystemMessage(string text)
    {
        string message = ReplacePlaceholdersSafe(text ?? "");

        WardrobeHeroSetupPage setupPage = FindWardrobeHeroSetupPage();
        if (setupPage != null)
        {
            setupPage.ShowTransientSystemMessage(message);
            return;
        }

        Debug.LogWarning("[DialogueUIManager] " + message, this);
    }

    public void ShowChoicePlaceholder()
    {
        EnsureStoryUiVisible();
        SetDialogueTexts("", ResolveChoiceDialoguePlaceholder(), false);
    }

    public void ShowChoicePlaceholderIfDialogueEmpty()
    {
        bool hasName = nameText != null && !string.IsNullOrWhiteSpace(nameText.text);
        bool hasBody = dialogueText != null && !string.IsNullOrWhiteSpace(dialogueText.text);
        if (!hasName && !hasBody)
            ShowChoicePlaceholder();
    }

    public void ShowChoiceHeader(DialogueLine line)
    {
        string bodyText = ReplacePlaceholdersSafe(line != null ? line.richText : "");
        if (IsVisualPlaceholder(bodyText))
        {
            ShowChoicePlaceholder();
            return;
        }

        if (!EnsureDialogueText())
            return;

        EnsureStoryUiVisible();
        SetDialogueTexts(ResolveSpeakerName(line, null, bodyText), bodyText);
    }

    string ResolveChoiceDialoguePlaceholder()
    {
        string placeholder = ReplacePlaceholdersSafe(choiceDialoguePlaceholder);
        if (string.IsNullOrWhiteSpace(placeholder))
            return DefaultChoiceDialoguePlaceholder;

        return string.Equals(placeholder.Trim(), LegacyChoiceDialoguePlaceholder, System.StringComparison.OrdinalIgnoreCase)
            ? DefaultChoiceDialoguePlaceholder
            : placeholder;
    }

    void LateUpdate()
    {
        ClampDialoguePanelToSafeArea();
    }

    RectTransform FindDialoguePanel()
    {
        if (dialogueText == null)
            return null;

        Transform current = dialogueText.transform.parent;
        int depth = 0;
        while (current != null && depth < 5)
        {
            RectTransform rect = current as RectTransform;
            if (rect != null && (nameText == null || nameText.transform.IsChildOf(current)))
                return rect;

            current = current.parent;
            depth++;
        }

        return dialogueText.rectTransform;
    }

    Image FindDialogueBackgroundImage()
    {
        if (dialoguePanel == null)
            return null;

        Image panelImage = dialoguePanel.GetComponent<Image>();
        if (panelImage != null)
            return panelImage;

        Image[] images = dialoguePanel.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && IsPrimaryDialogueBackgroundImageName(image.name))
                return image;
        }

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && IsDialogueBackgroundImageName(image.name))
                return image;
        }

        return images.Length > 0 ? images[0] : null;
    }

    List<Image> FindDialogueExtraBackgroundImages()
    {
        var result = new List<Image>();
        if (dialoguePanel == null)
            return result;

        Image[] images = dialoguePanel.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image == dialogueBackgroundImage)
                continue;

            if (!IsDialogueExtraBackgroundImageName(image.name))
                continue;

            result.Add(image);
        }

        return result;
    }

    Image ResolveNamePlateImage()
    {
        if (namePlateImage != null)
            return namePlateImage;

        if (namePlateObject != null)
        {
            Image directImage = namePlateObject.GetComponent<Image>();
            if (directImage != null)
                return directImage;

            Image childImage = namePlateObject.GetComponentInChildren<Image>(true);
            if (childImage != null)
                return childImage;
        }

        Transform nameParent = nameText != null ? nameText.transform.parent : null;
        if (nameParent != null)
        {
            Image directImage = nameParent.GetComponent<Image>();
            if (directImage != null)
                return directImage;

            Image childImage = nameParent.GetComponentInChildren<Image>(true);
            if (childImage != null)
                return childImage;
        }

        return null;
    }

    RectTransform ResolveNamePlateRect()
    {
        Image image = ResolveNamePlateImage();
        if (image != null)
            return image.rectTransform;

        return namePlateObject != null ? namePlateObject.transform as RectTransform : null;
    }

    static bool IsPrimaryDialogueBackgroundImageName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        string normalized = objectName.Trim();
        return normalized.Equals("Background", System.StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("DialogueBackground", System.StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Dialogue Background", System.StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("DialoguePanel", System.StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Dialogue Panel", System.StringComparison.OrdinalIgnoreCase);
    }

    static bool IsDialogueBackgroundImageName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        return objectName.IndexOf("background", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("dialoguepanel", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("dialogue panel", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsDialogueExtraBackgroundImageName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        if (IsPrimaryDialogueBackgroundImageName(objectName))
            return false;

        return objectName.IndexOf("background", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("extra", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("layer", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("overlay", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("transparency", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("panel", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("плаш", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("фон", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    Image FindChoiceContainerBackgroundImage()
    {
        if (choiceContainer == null)
            return null;

        Image directImage = choiceContainer.GetComponent<Image>();
        if (directImage != null)
            return directImage;

        Image[] images = choiceContainer.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.GetComponentInParent<Button>() != null)
                continue;

            if (IsChoicePanelBackgroundName(image.name))
                return image;
        }

        return null;
    }

    static bool IsChoicePanelBackgroundName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        return objectName.IndexOf("background", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("choicepanel", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("choice panel", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void CaptureDefaultDialogueBackgroundImage()
    {
        CaptureDefaultImage(ref dialogueBackgroundImageDefaults, dialogueBackgroundImage);
    }

    void RestoreDefaultDialogueBackgroundImage()
    {
        RestoreDefaultImage(dialogueBackgroundImageDefaults, dialogueBackgroundImage);
    }

    void CaptureDefaultDialogueBackgroundRect()
    {
        CaptureDefaultRect(ref dialogueBackgroundRectDefaults, ResolveDialogueBackgroundRect());
    }

    void RestoreDefaultDialogueBackgroundRect()
    {
        RestoreDefaultRect(dialogueBackgroundRectDefaults, ResolveDialogueBackgroundRect());
    }

    void CaptureDefaultChoiceContainerImage()
    {
        CaptureDefaultImage(ref choiceContainerImageDefaults, FindChoiceContainerBackgroundImage());
    }

    void RestoreDefaultChoiceContainerImage()
    {
        RestoreDefaultImage(choiceContainerImageDefaults, FindChoiceContainerBackgroundImage());
    }

    void CaptureDefaultNamePlateImage()
    {
        CaptureDefaultImage(ref namePlateImageDefaults, ResolveNamePlateImage());
    }

    void RestoreDefaultNamePlateImage()
    {
        RestoreDefaultImage(namePlateImageDefaults, ResolveNamePlateImage());
    }

    void CaptureDefaultNamePlateRect()
    {
        CaptureDefaultRect(ref namePlateRectDefaults, ResolveNamePlateRect());
    }

    void RestoreDefaultNamePlateRect()
    {
        RestoreDefaultRect(namePlateRectDefaults, ResolveNamePlateRect());
    }

    void CaptureDefaultDialoguePanelLayout()
    {
        CaptureDefaultVerticalLayoutGroup(ref dialoguePanelVerticalLayoutDefaults, FindDialoguePanelVerticalLayoutGroup());
        CaptureDefaultContentSizeFitter(ref dialoguePanelContentSizeFitterDefaults, FindDialoguePanelContentSizeFitter());
    }

    void RestoreDefaultDialoguePanelVerticalLayout()
    {
        RestoreDefaultVerticalLayoutGroup(dialoguePanelVerticalLayoutDefaults, FindDialoguePanelVerticalLayoutGroup());
    }

    void RestoreDefaultDialoguePanelContentSizeFitter()
    {
        RestoreDefaultContentSizeFitter(dialoguePanelContentSizeFitterDefaults, FindDialoguePanelContentSizeFitter());
    }

    void ApplyActiveDialoguePanelLayout()
    {
        CaptureDefaultDialoguePanelLayout();

        if (activeStoryUiStyle == null)
        {
            RestoreDefaultDialoguePanelVerticalLayout();
            RestoreDefaultDialoguePanelContentSizeFitter();
            return;
        }

        VerticalLayoutGroup layoutGroup = FindDialoguePanelVerticalLayoutGroup();
        if (layoutGroup != null)
        {
            if (activeStoryUiStyle.OverrideDialoguePanelVerticalLayout)
                ApplyDialoguePanelVerticalLayout(layoutGroup);
            else
                RestoreDefaultDialoguePanelVerticalLayout();
        }

        ContentSizeFitter fitter = FindDialoguePanelContentSizeFitter();
        if (fitter != null)
        {
            if (activeStoryUiStyle.OverrideDialoguePanelContentSizeFitter)
                ApplyDialoguePanelContentSizeFitter(fitter);
            else
                RestoreDefaultDialoguePanelContentSizeFitter();
        }

        MarkDialoguePanelLayoutDirty();
    }

    void ApplyDialoguePanelVerticalLayout(VerticalLayoutGroup layoutGroup)
    {
        if (layoutGroup == null || activeStoryUiStyle == null)
            return;

        CopyRectOffset(activeStoryUiStyle.DialoguePanelVerticalLayoutPadding, layoutGroup.padding);
        layoutGroup.spacing = activeStoryUiStyle.DialoguePanelVerticalLayoutSpacing;
        layoutGroup.childAlignment = activeStoryUiStyle.DialoguePanelVerticalLayoutChildAlignment;
        layoutGroup.reverseArrangement = activeStoryUiStyle.DialoguePanelVerticalLayoutReverseArrangement;
        layoutGroup.childControlWidth = activeStoryUiStyle.DialoguePanelVerticalLayoutControlChildWidth;
        layoutGroup.childControlHeight = activeStoryUiStyle.DialoguePanelVerticalLayoutControlChildHeight;
        layoutGroup.childScaleWidth = activeStoryUiStyle.DialoguePanelVerticalLayoutUseChildScaleWidth;
        layoutGroup.childScaleHeight = activeStoryUiStyle.DialoguePanelVerticalLayoutUseChildScaleHeight;
        layoutGroup.childForceExpandWidth = activeStoryUiStyle.DialoguePanelVerticalLayoutChildForceExpandWidth;
        layoutGroup.childForceExpandHeight = activeStoryUiStyle.DialoguePanelVerticalLayoutChildForceExpandHeight;
    }

    void ApplyDialoguePanelContentSizeFitter(ContentSizeFitter fitter)
    {
        if (fitter == null || activeStoryUiStyle == null)
            return;

        fitter.horizontalFit = activeStoryUiStyle.DialoguePanelContentSizeFitterHorizontalFit;
        fitter.verticalFit = activeStoryUiStyle.DialoguePanelContentSizeFitterVerticalFit;
    }

    void ApplyActiveNamePlateStyle()
    {
        Image image = ResolveNamePlateImage();
        RectTransform rect = ResolveNamePlateRect();

        if (image != null)
            CaptureDefaultNamePlateImage();
        if (rect != null)
            CaptureDefaultNamePlateRect();

        if (activeStoryUiStyle == null)
        {
            RestoreDefaultNamePlateImage();
            RestoreDefaultNamePlateRect();
            return;
        }

        RestoreDefaultNamePlateImage();
        RestoreDefaultNamePlateRect();

        activeStoryUiStyle.ApplyToNamePlate(image, rect);
    }

    void ApplyActiveDialogueBackgroundRect()
    {
        RectTransform rect = ResolveDialogueBackgroundRect();
        if (rect == null)
            return;

        CaptureDefaultDialogueBackgroundRect();
        SetDialogueBackgroundAutoSizeSuppressedByStyle(
            activeStoryUiStyle != null && activeStoryUiStyle.HasDialogueBackgroundRectOverrides);

        if (activeStoryUiStyle == null)
        {
            RestoreDefaultDialogueBackgroundRect();
            return;
        }

        if (activeStoryUiStyle.HasDialogueBackgroundRectOverrides)
        {
            SetRectIgnoreLayout(rect, true);
            activeStoryUiStyle.ApplyToDialogueBackgroundRect(rect);
        }
        else
        {
            RestoreDefaultDialogueBackgroundRect();
        }

        LayoutRebuilder.MarkLayoutForRebuild(rect);
        if (dialoguePanel != null)
            LayoutRebuilder.MarkLayoutForRebuild(dialoguePanel);
    }

    void ReapplyOverriddenDialogueBackgroundRect()
    {
        if (activeStoryUiStyle == null || !activeStoryUiStyle.HasDialogueBackgroundRectOverrides)
            return;

        ApplyActiveDialogueBackgroundRect();

        RectTransform rect = ResolveDialogueBackgroundRect();
        if (rect != null)
        {
            LayoutRebuilder.MarkLayoutForRebuild(rect);
            if (!Application.isPlaying)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        RebuildDialoguePanelForImmediatePreview();
    }

    void ApplyActiveDialogueExtraLayers()
    {
        if (dialogueExtraBackgroundImages == null)
            dialogueExtraBackgroundImages = new List<Image>();

        if (dialogueExtraBackgroundImages.Count == 0)
            dialogueExtraBackgroundImages = FindDialogueExtraBackgroundImages();

        IReadOnlyList<DialoguePanelExtraLayerStyle> layers = activeStoryUiStyle != null
            ? activeStoryUiStyle.DialogueExtraLayers
            : null;

        bool hasLayers = layers != null && layers.Count > 0;
        var used = new HashSet<Image>();

        if (hasLayers && activeStoryUiStyle != null)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                DialoguePanelExtraLayerStyle layer = layers[i];
                Image target = ResolveDialogueExtraLayerTarget(layer, i, used);
                if (target == null)
                    continue;

                activeStoryUiStyle.ApplyToDialogueExtraLayer(target, layer);
                used.Add(target);
            }
        }

        for (int i = 0; i < dialogueExtraBackgroundImages.Count; i++)
        {
            Image image = dialogueExtraBackgroundImages[i];
            if (image == null || used.Contains(image))
                continue;

            image.gameObject.SetActive(false);
        }
    }

    Image ResolveDialogueExtraLayerTarget(DialoguePanelExtraLayerStyle layer, int index, HashSet<Image> used)
    {
        if (dialogueExtraBackgroundImages == null)
            return null;

        if (layer != null && !string.IsNullOrWhiteSpace(layer.TargetPath))
        {
            for (int i = 0; i < dialogueExtraBackgroundImages.Count; i++)
            {
                Image image = dialogueExtraBackgroundImages[i];
                if (image == null || used.Contains(image))
                    continue;

                if (GetDialoguePanelRelativePath(image.transform) == layer.TargetPath)
                    return image;
            }
        }

        if (layer != null && !string.IsNullOrWhiteSpace(layer.TargetName))
        {
            for (int i = 0; i < dialogueExtraBackgroundImages.Count; i++)
            {
                Image image = dialogueExtraBackgroundImages[i];
                if (image == null || used.Contains(image))
                    continue;

                if (string.Equals(image.name, layer.TargetName, System.StringComparison.OrdinalIgnoreCase))
                    return image;
            }
        }

        int visibleIndex = 0;
        for (int i = 0; i < dialogueExtraBackgroundImages.Count; i++)
        {
            Image image = dialogueExtraBackgroundImages[i];
            if (image == null || used.Contains(image))
                continue;

            if (visibleIndex == index)
                return image;

            visibleIndex++;
        }

        return null;
    }

    string GetDialoguePanelRelativePath(Transform target)
    {
        if (target == null)
            return "";

        Transform root = dialoguePanel != null ? dialoguePanel.transform : null;
        if (root == null || target == root)
            return target.name;

        var names = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    static void CaptureDefaultImage(ref UiImageDefaults defaults, Image image)
    {
        if (image == null)
            return;

        if (defaults.Captured && defaults.Target == image)
            return;

        defaults = new UiImageDefaults
        {
            Target = image,
            Sprite = image.sprite,
            Color = image.color,
            Type = image.type,
            PreserveAspect = image.preserveAspect,
            PixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier,
            Material = image.material,
            RaycastTarget = image.raycastTarget,
            Captured = true
        };
    }

    static void RestoreDefaultImage(UiImageDefaults defaults, Image image)
    {
        if (!defaults.Captured ||
            defaults.Target == null ||
            defaults.Target != image)
        {
            return;
        }

        image.sprite = defaults.Sprite;
        image.color = defaults.Color;
        image.type = defaults.Type;
        image.preserveAspect = defaults.PreserveAspect;
        image.pixelsPerUnitMultiplier = defaults.PixelsPerUnitMultiplier;
        image.material = defaults.Material;
        image.raycastTarget = defaults.RaycastTarget;
        image.SetAllDirty();
    }

    static void CaptureDefaultRect(ref UiRectDefaults defaults, RectTransform rect)
    {
        if (rect == null)
            return;

        if (defaults.Captured && defaults.Target == rect)
            return;

        LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
        defaults = new UiRectDefaults
        {
            Target = rect,
            AnchorMin = rect.anchorMin,
            AnchorMax = rect.anchorMax,
            Pivot = rect.pivot,
            AnchoredPosition = rect.anchoredPosition,
            SizeDelta = rect.sizeDelta,
            LayoutElement = layoutElement,
            LayoutElementIgnoreLayout = layoutElement != null && layoutElement.ignoreLayout,
            Captured = true
        };
    }

    static void RestoreDefaultRect(UiRectDefaults defaults, RectTransform rect)
    {
        if (!defaults.Captured ||
            defaults.Target == null ||
            defaults.Target != rect)
        {
            return;
        }

        rect.anchorMin = defaults.AnchorMin;
        rect.anchorMax = defaults.AnchorMax;
        rect.pivot = defaults.Pivot;
        rect.anchoredPosition = defaults.AnchoredPosition;
        rect.sizeDelta = defaults.SizeDelta;
        if (defaults.LayoutElement != null)
        {
            defaults.LayoutElement.ignoreLayout = defaults.LayoutElementIgnoreLayout;
        }
        else
        {
            LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
            if (layoutElement != null)
                layoutElement.ignoreLayout = false;
        }
        LayoutRebuilder.MarkLayoutForRebuild(rect);
    }

    static void SetRectIgnoreLayout(RectTransform rect, bool ignoreLayout)
    {
        if (rect == null)
            return;

        LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
        if (layoutElement == null && ignoreLayout)
            layoutElement = rect.gameObject.AddComponent<LayoutElement>();

        if (layoutElement == null)
            return;

        layoutElement.ignoreLayout = ignoreLayout;
        if (ignoreLayout)
        {
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
        }
    }

    static void CaptureDefaultVerticalLayoutGroup(
        ref UiVerticalLayoutGroupDefaults defaults,
        VerticalLayoutGroup layoutGroup)
    {
        if (layoutGroup == null)
            return;

        if (defaults.Captured && defaults.Target == layoutGroup)
            return;

        defaults = new UiVerticalLayoutGroupDefaults
        {
            Target = layoutGroup,
            Padding = CopyRectOffset(layoutGroup.padding),
            ChildAlignment = layoutGroup.childAlignment,
            Spacing = layoutGroup.spacing,
            ReverseArrangement = layoutGroup.reverseArrangement,
            ChildControlWidth = layoutGroup.childControlWidth,
            ChildControlHeight = layoutGroup.childControlHeight,
            ChildScaleWidth = layoutGroup.childScaleWidth,
            ChildScaleHeight = layoutGroup.childScaleHeight,
            ChildForceExpandWidth = layoutGroup.childForceExpandWidth,
            ChildForceExpandHeight = layoutGroup.childForceExpandHeight,
            Captured = true
        };
    }

    static void RestoreDefaultVerticalLayoutGroup(
        UiVerticalLayoutGroupDefaults defaults,
        VerticalLayoutGroup layoutGroup)
    {
        if (!defaults.Captured ||
            defaults.Target == null ||
            defaults.Target != layoutGroup)
        {
            return;
        }

        CopyRectOffset(defaults.Padding, layoutGroup.padding);
        layoutGroup.childAlignment = defaults.ChildAlignment;
        layoutGroup.spacing = defaults.Spacing;
        layoutGroup.reverseArrangement = defaults.ReverseArrangement;
        layoutGroup.childControlWidth = defaults.ChildControlWidth;
        layoutGroup.childControlHeight = defaults.ChildControlHeight;
        layoutGroup.childScaleWidth = defaults.ChildScaleWidth;
        layoutGroup.childScaleHeight = defaults.ChildScaleHeight;
        layoutGroup.childForceExpandWidth = defaults.ChildForceExpandWidth;
        layoutGroup.childForceExpandHeight = defaults.ChildForceExpandHeight;
        LayoutRebuilder.MarkLayoutForRebuild(layoutGroup.transform as RectTransform);
    }

    static void CaptureDefaultContentSizeFitter(
        ref UiContentSizeFitterDefaults defaults,
        ContentSizeFitter fitter)
    {
        if (fitter == null)
            return;

        if (defaults.Captured && defaults.Target == fitter)
            return;

        defaults = new UiContentSizeFitterDefaults
        {
            Target = fitter,
            HorizontalFit = fitter.horizontalFit,
            VerticalFit = fitter.verticalFit,
            Captured = true
        };
    }

    static void RestoreDefaultContentSizeFitter(
        UiContentSizeFitterDefaults defaults,
        ContentSizeFitter fitter)
    {
        if (!defaults.Captured ||
            defaults.Target == null ||
            defaults.Target != fitter)
        {
            return;
        }

        fitter.horizontalFit = defaults.HorizontalFit;
        fitter.verticalFit = defaults.VerticalFit;
        LayoutRebuilder.MarkLayoutForRebuild(fitter.transform as RectTransform);
    }

    static RectOffset CopyRectOffset(RectOffset source)
    {
        if (source == null)
            return new RectOffset();

        return new RectOffset(source.left, source.right, source.top, source.bottom);
    }

    static void CopyRectOffset(RectOffset source, RectOffset target)
    {
        if (target == null)
            return;

        target.left = source != null ? source.left : 0;
        target.right = source != null ? source.right : 0;
        target.top = source != null ? source.top : 0;
        target.bottom = source != null ? source.bottom : 0;
    }

    void RefreshDialogueBackgroundLayout()
    {
        DialogueBackgroundAutoSize autoSize = ResolveDialogueBackgroundAutoSize();
        if (autoSize == null)
            return;

        bool suppressAutoSize = activeStoryUiStyle != null && activeStoryUiStyle.HasDialogueBackgroundRectOverrides;
        autoSize.SetSuppressedByStoryUiStyle(suppressAutoSize);
        if (suppressAutoSize)
            return;

        autoSize.MarkDirty();
        autoSize.RefreshNow();
    }

    DialogueBackgroundAutoSize ResolveDialogueBackgroundAutoSize()
    {
        DialogueBackgroundAutoSize autoSize = null;

        if (dialogueBackgroundImage != null)
            autoSize = dialogueBackgroundImage.GetComponent<DialogueBackgroundAutoSize>();

        if (autoSize == null && dialoguePanel != null)
            autoSize = dialoguePanel.GetComponentInChildren<DialogueBackgroundAutoSize>(true);

        return autoSize;
    }

    void SetDialogueBackgroundAutoSizeSuppressedByStyle(bool suppressed)
    {
        DialogueBackgroundAutoSize autoSize = ResolveDialogueBackgroundAutoSize();
        if (autoSize != null)
            autoSize.SetSuppressedByStoryUiStyle(suppressed);
    }

    RectTransform ResolveDialogueBackgroundRect()
    {
        return dialogueBackgroundImage != null
            ? dialogueBackgroundImage.rectTransform
            : null;
    }

    VerticalLayoutGroup FindDialoguePanelVerticalLayoutGroup()
    {
        if (dialoguePanelVerticalLayoutGroup != null &&
            dialoguePanelVerticalLayoutGroup.transform != null &&
            dialoguePanel != null &&
            dialoguePanelVerticalLayoutGroup.transform.IsChildOf(dialoguePanel))
        {
            return dialoguePanelVerticalLayoutGroup;
        }

        dialoguePanelVerticalLayoutGroup = null;
        if (dialoguePanel == null)
            return null;

        VerticalLayoutGroup[] groups = dialoguePanel.GetComponentsInChildren<VerticalLayoutGroup>(true);
        if (groups == null || groups.Length == 0)
            return null;

        Transform bodyTextTransform = dialogueText != null ? dialogueText.transform : null;
        for (int i = 0; i < groups.Length; i++)
        {
            VerticalLayoutGroup group = groups[i];
            if (group != null &&
                bodyTextTransform != null &&
                bodyTextTransform.IsChildOf(group.transform))
            {
                dialoguePanelVerticalLayoutGroup = group;
                return dialoguePanelVerticalLayoutGroup;
            }
        }

        for (int i = 0; i < groups.Length; i++)
        {
            VerticalLayoutGroup group = groups[i];
            if (group != null &&
                string.Equals(group.name, "Container", System.StringComparison.OrdinalIgnoreCase))
            {
                dialoguePanelVerticalLayoutGroup = group;
                return dialoguePanelVerticalLayoutGroup;
            }
        }

        dialoguePanelVerticalLayoutGroup = groups[0];
        return dialoguePanelVerticalLayoutGroup;
    }

    ContentSizeFitter FindDialoguePanelContentSizeFitter()
    {
        VerticalLayoutGroup layoutGroup = FindDialoguePanelVerticalLayoutGroup();
        if (layoutGroup == null)
        {
            dialoguePanelContentSizeFitter = null;
            return null;
        }

        if (dialoguePanelContentSizeFitter != null &&
            dialoguePanelContentSizeFitter.transform == layoutGroup.transform)
        {
            return dialoguePanelContentSizeFitter;
        }

        dialoguePanelContentSizeFitter = layoutGroup.GetComponent<ContentSizeFitter>();
        return dialoguePanelContentSizeFitter;
    }

    void MarkDialoguePanelLayoutDirty()
    {
        RectTransform layoutRect = dialoguePanelVerticalLayoutGroup != null
            ? dialoguePanelVerticalLayoutGroup.transform as RectTransform
            : null;

        if (layoutRect != null)
            LayoutRebuilder.MarkLayoutForRebuild(layoutRect);

        if (dialoguePanel != null)
            LayoutRebuilder.MarkLayoutForRebuild(dialoguePanel);

        if (!Application.isPlaying)
        {
            if (layoutRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRect);
            if (dialoguePanel != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(dialoguePanel);
            Canvas.ForceUpdateCanvases();
        }
    }

    void ClampDialoguePanelToSafeArea()
    {
        if (!keepDialoguePanelOnScreen || dialoguePanel == null || !dialoguePanel.gameObject.activeInHierarchy)
            return;

        CaptureDialogueSafeLayout();

        if (activeStoryUiStyle != null && activeStoryUiStyle.OverrideDialoguePanelRect)
        {
            ApplyActiveDialoguePanelRect();
            FitDialogueTextToPanel();
            ApplyActiveDialogueTextLayout();
            ApplyActiveDialoguePanelAutoLayout();
            return;
        }

        Canvas canvas = dialoguePanel.GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        RectTransform parent = dialoguePanel.parent as RectTransform;
        if (canvasRect == null || parent == null)
            return;

        Rect safe = canvasRect.rect;
        safe.xMin += dialoguePanelPadding.x;
        safe.xMax -= dialoguePanelPadding.x;
        safe.yMin += dialoguePanelPadding.y;
        safe.yMax -= dialoguePanelPadding.y;

        if (safe.width <= 0f || safe.height <= 0f)
            return;

        RestoreDialoguePanelSafeLayout();

        if (shrinkDialoguePanelToScreen)
            FitDialoguePanelToSafeArea(safe);

        FitDialogueTextToPanel();
        ApplyActiveDialogueTextLayout();
        ApplyActiveDialoguePanelAutoLayout();

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, dialoguePanel);
        Vector2 delta = Vector2.zero;

        if (bounds.size.x > safe.width)
            delta.x = safe.center.x - bounds.center.x;
        else if (bounds.min.x < safe.xMin)
            delta.x = safe.xMin - bounds.min.x;
        else if (bounds.max.x > safe.xMax)
            delta.x = safe.xMax - bounds.max.x;

        if (bounds.size.y > safe.height)
        {
            if (!allowDialoguePanelBelowScreen && bounds.min.y < safe.yMin)
                delta.y = safe.yMin - bounds.min.y;
            else if (bounds.max.y > safe.yMax)
                delta.y = safe.yMax - bounds.max.y;
        }
        else if (!allowDialoguePanelBelowScreen && bounds.min.y < safe.yMin)
            delta.y = safe.yMin - bounds.min.y;
        else if (bounds.max.y > safe.yMax)
            delta.y = safe.yMax - bounds.max.y;

        if (Mathf.Approximately(delta.x, 0f) && Mathf.Approximately(delta.y, 0f))
            return;

        Vector3 worldOrigin = canvasRect.TransformPoint(Vector3.zero);
        Vector3 worldDelta = canvasRect.TransformPoint(delta);
        Vector3 parentOrigin = parent.InverseTransformPoint(worldOrigin);
        Vector3 parentDelta = parent.InverseTransformPoint(worldDelta);
        dialoguePanel.anchoredPosition = ResolveActiveDialoguePanelAnchoredPosition() + (Vector2)(parentDelta - parentOrigin);
    }

    void CaptureDialogueSafeLayout()
    {
        if (dialoguePanel == null)
            return;

        dialogueTextRect = dialogueText != null ? dialogueText.rectTransform : null;

        if (dialogueSafeLayoutCaptured &&
            safeLayoutCapturedPanel == dialoguePanel &&
            safeLayoutCapturedText == dialogueTextRect)
        {
            return;
        }

        safeLayoutCapturedPanel = dialoguePanel;
        safeLayoutCapturedText = dialogueTextRect;
        dialoguePanelBaseAnchoredPosition = dialoguePanel.anchoredPosition;
        dialoguePanelBaseSizeDelta = dialoguePanel.sizeDelta;

        if (dialogueTextRect != null)
        {
            dialogueTextBaseSizeDelta = dialogueTextRect.sizeDelta;
            dialogueTextBaseAnchoredPosition = dialogueTextRect.anchoredPosition;
            dialogueTextHorizontalPadding = Mathf.Max(0f, dialoguePanel.rect.width - dialogueTextRect.rect.width);
            dialogueTextBaseFont = dialogueText != null ? dialogueText.font : null;
            dialogueTextFontCaptured = dialogueText != null;
        }
        else
        {
            dialogueTextBaseSizeDelta = Vector2.zero;
            dialogueTextBaseAnchoredPosition = Vector2.zero;
            dialogueTextHorizontalPadding = 0f;
            dialogueTextBaseFont = null;
            dialogueTextFontCaptured = false;
        }

        dialogueSafeLayoutCaptured = true;
    }

    void RestoreDialoguePanelSafeLayout()
    {
        if (!dialogueSafeLayoutCaptured || dialoguePanel == null)
            return;

        dialoguePanel.anchoredPosition = ResolveActiveDialoguePanelAnchoredPosition();
        dialoguePanel.sizeDelta = ResolveActiveDialoguePanelSizeDelta();

        if (dialogueTextRect != null)
        {
            dialogueTextRect.anchoredPosition = dialogueTextBaseAnchoredPosition;
            dialogueTextRect.sizeDelta = dialogueTextBaseSizeDelta;
        }
    }

    void RestoreDialoguePanelBaseLayout()
    {
        if (!dialogueSafeLayoutCaptured || dialoguePanel == null)
            return;

        dialoguePanel.anchoredPosition = dialoguePanelBaseAnchoredPosition;
        dialoguePanel.sizeDelta = dialoguePanelBaseSizeDelta;

        if (dialogueTextRect != null)
        {
            dialogueTextRect.anchoredPosition = dialogueTextBaseAnchoredPosition;
            dialogueTextRect.sizeDelta = dialogueTextBaseSizeDelta;
        }

        FindDialogueTextGrowDownLock()?.CaptureBaseLayoutFromCurrentRect();
        RestoreCharacterNameBaseLayout();
        RebuildDialoguePanelForImmediatePreview();
    }

    void ApplyActiveDialogueTextLayout()
    {
        ApplyActiveDialogueTextFont();
        ApplyActiveDialogueTextTopOffset();

        if (activeStoryUiStyle == null ||
            !activeStoryUiStyle.OverrideBodyTextOffsetY ||
            dialogueTextRect == null)
        {
            return;
        }

        CaptureDialogueSafeLayout();

        Vector2 textPosition = dialogueTextRect.anchoredPosition;
        textPosition.y = dialogueTextBaseAnchoredPosition.y + activeStoryUiStyle.BodyTextOffsetY;
        dialogueTextRect.anchoredPosition = textPosition;
    }

    void ApplyActiveDialogueTextFont()
    {
        if (dialogueText == null)
            return;

        CaptureDialogueSafeLayout();

        if (!dialogueTextFontCaptured)
            return;

        TMP_FontAsset targetFont = activeStoryUiStyle != null && activeStoryUiStyle.OverrideBodyTextFont
            ? activeStoryUiStyle.BodyTextFont
            : dialogueTextBaseFont;

        if (dialogueText.font == targetFont)
            return;

        dialogueText.font = targetFont;
        dialogueText.SetAllDirty();
    }

    void ApplyActiveDialoguePanelRect()
    {
        if (dialoguePanel == null ||
            activeStoryUiStyle == null ||
            !activeStoryUiStyle.OverrideDialoguePanelRect)
        {
            return;
        }

        dialoguePanel.anchoredPosition = activeStoryUiStyle.DialoguePanelAnchoredPosition;
        dialoguePanel.sizeDelta = activeStoryUiStyle.DialoguePanelSizeDelta;
        RebuildDialoguePanelForImmediatePreview();
    }

    void RebuildDialoguePanelForImmediatePreview()
    {
        if (dialoguePanel == null)
            return;

        LayoutRebuilder.MarkLayoutForRebuild(dialoguePanel);

        if (Application.isPlaying)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(dialoguePanel);
        Canvas.ForceUpdateCanvases();
    }

    Vector2 ResolveActiveDialoguePanelAnchoredPosition()
    {
        if (activeStoryUiStyle != null && activeStoryUiStyle.OverrideDialoguePanelRect)
            return activeStoryUiStyle.DialoguePanelAnchoredPosition;

        return dialoguePanelBaseAnchoredPosition;
    }

    Vector2 ResolveActiveDialoguePanelSizeDelta()
    {
        if (activeStoryUiStyle != null && activeStoryUiStyle.OverrideDialoguePanelRect)
            return activeStoryUiStyle.DialoguePanelSizeDelta;

        return dialoguePanelBaseSizeDelta;
    }

    void ApplyActiveCharacterNameLayout()
    {
        if (nameText == null)
            return;

        characterNameRect = nameText.rectTransform;
        CaptureCharacterNameLayout(characterNameRect);

        if (characterNameRect == null || !characterNameLayoutCaptured)
            return;

        Vector2 position = characterNameBaseAnchoredPosition;
        if (activeStoryUiStyle != null && activeStoryUiStyle.OverrideCharacterNameOffset)
            position += activeStoryUiStyle.CharacterNameOffset;

        characterNameRect.anchoredPosition = position;
        ApplyActiveCharacterNameFont();
    }

    void RestoreCharacterNameBaseLayout()
    {
        if (!characterNameLayoutCaptured || characterNameRect == null)
            return;

        characterNameRect.anchoredPosition = characterNameBaseAnchoredPosition;
        if (nameText != null)
            nameText.fontSize = characterNameBaseFontSize;
    }

    void CaptureCharacterNameLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        if (characterNameLayoutCaptured && safeLayoutCapturedNameText == rect)
            return;

        safeLayoutCapturedNameText = rect;
        characterNameBaseAnchoredPosition = rect.anchoredPosition;
        characterNameBaseFont = nameText != null ? nameText.font : null;
        characterNameBaseFontSize = nameText != null ? nameText.fontSize : 0f;
        characterNameLayoutCaptured = true;
    }

    void ApplyActiveCharacterNameFont()
    {
        if (nameText == null || !characterNameLayoutCaptured)
            return;

        TMP_FontAsset targetFont = activeStoryUiStyle != null && activeStoryUiStyle.OverrideCharacterNameFont
            ? activeStoryUiStyle.CharacterNameFont
            : characterNameBaseFont;

        float targetFontSize = activeStoryUiStyle != null && activeStoryUiStyle.OverrideCharacterNameFontSize
            ? activeStoryUiStyle.CharacterNameFontSize
            : characterNameBaseFontSize;

        bool changed = false;
        if (nameText.font != targetFont)
        {
            nameText.font = targetFont;
            changed = true;
        }

        if (targetFontSize > 0f && !Mathf.Approximately(nameText.fontSize, targetFontSize))
        {
            nameText.fontSize = targetFontSize;
            if (nameText.enableAutoSizing && nameText.fontSizeMax > 0f)
                nameText.fontSizeMax = Mathf.Max(nameText.fontSizeMin, targetFontSize);
            changed = true;
        }

        if (changed)
            nameText.SetAllDirty();
    }

    void ApplyActiveDialogueTextTopOffset()
    {
        StoryTextLayoutLock growDownLock = FindDialogueTextGrowDownLock();
        if (growDownLock == null)
            return;

        CaptureDialogueTextGrowDownDefault(growDownLock);

        float topOffsetY = activeStoryUiStyle != null && activeStoryUiStyle.OverrideBodyTextTopOffsetY
            ? activeStoryUiStyle.BodyTextTopOffsetY
            : dialogueTextGrowDownDefaultTopOffsetY;

        float offsetX = activeStoryUiStyle != null && activeStoryUiStyle.OverrideBodyTextGrowDownOffsetX
            ? activeStoryUiStyle.BodyTextGrowDownOffsetX
            : dialogueTextGrowDownDefaultOffsetX;

        growDownLock.SetOffsets(offsetX, topOffsetY);
        growDownLock.ApplyLayoutOverrides(
            true,
            activeStoryUiStyle != null && activeStoryUiStyle.OverrideBodyTextResizeHeightToPreferredText
                ? activeStoryUiStyle.BodyTextResizeHeightToPreferredText
                : dialogueTextGrowDownDefaultResizeHeightToPreferredText,
            true,
            activeStoryUiStyle != null && activeStoryUiStyle.OverrideBodyTextExtraHeight
                ? activeStoryUiStyle.BodyTextExtraHeight
                : dialogueTextGrowDownDefaultExtraHeight,
            true,
            activeStoryUiStyle != null && activeStoryUiStyle.OverrideBodyTextMinHeight
                ? activeStoryUiStyle.BodyTextMinHeight
                : dialogueTextGrowDownDefaultMinHeight,
            true,
            activeStoryUiStyle != null && activeStoryUiStyle.OverrideBodyTextMaxHeight
                ? activeStoryUiStyle.BodyTextMaxHeight
                : dialogueTextGrowDownDefaultMaxHeight,
            true,
            activeStoryUiStyle != null && activeStoryUiStyle.OverrideBodyTextMaxFontSize
                ? activeStoryUiStyle.BodyTextMaxFontSize
                : dialogueTextGrowDownDefaultMaxFontSize,
            true,
            activeStoryUiStyle != null && activeStoryUiStyle.OverrideBodyTextShrinkTextToFitRect
                ? activeStoryUiStyle.BodyTextShrinkTextToFitRect
                : dialogueTextGrowDownDefaultShrinkTextToFitRect,
            true,
            activeStoryUiStyle != null && activeStoryUiStyle.OverrideBodyTextMinAutoFontSize
                ? activeStoryUiStyle.BodyTextMinAutoFontSize
                : dialogueTextGrowDownDefaultMinAutoFontSize,
            true,
            activeStoryUiStyle != null && activeStoryUiStyle.OverrideBodyTextOverflowModeWhenStillTooLarge
                ? activeStoryUiStyle.BodyTextOverflowModeWhenStillTooLarge
                : dialogueTextGrowDownDefaultOverflowModeWhenStillTooLarge);
    }

    void CaptureDialogueTextGrowDownDefault(StoryTextLayoutLock growDownLock)
    {
        if (growDownLock == null)
            return;

        if (dialogueTextGrowDownLock != growDownLock)
        {
            dialogueTextGrowDownLock = growDownLock;
            dialogueTextGrowDownTopOffsetCaptured = false;
        }

        if (dialogueTextGrowDownTopOffsetCaptured)
            return;

        dialogueTextGrowDownDefaultTopOffsetY = growDownLock.TopOffsetY;
        dialogueTextGrowDownDefaultOffsetX = growDownLock.OffsetX;
        dialogueTextGrowDownDefaultResizeHeightToPreferredText = growDownLock.ResizeHeightToPreferredText;
        dialogueTextGrowDownDefaultExtraHeight = growDownLock.ExtraHeight;
        dialogueTextGrowDownDefaultMinHeight = growDownLock.MinHeight;
        dialogueTextGrowDownDefaultMaxHeight = growDownLock.MaxHeight;
        dialogueTextGrowDownDefaultMaxFontSize = growDownLock.MaxFontSize;
        dialogueTextGrowDownDefaultShrinkTextToFitRect = growDownLock.ShrinkTextToFitRect;
        dialogueTextGrowDownDefaultMinAutoFontSize = growDownLock.MinAutoFontSize;
        dialogueTextGrowDownDefaultOverflowModeWhenStillTooLarge = growDownLock.OverflowModeWhenStillTooLarge;
        dialogueTextGrowDownTopOffsetCaptured = true;
    }

    StoryTextLayoutLock FindDialogueTextGrowDownLock()
    {
        if (dialogueTextGrowDownLock != null)
            return dialogueTextGrowDownLock;

        if (dialogueText != null)
            dialogueTextGrowDownLock = dialogueText.GetComponent<StoryTextLayoutLock>();

        if (dialogueTextGrowDownLock == null && dialogueTextRect != null)
            dialogueTextGrowDownLock = dialogueTextRect.GetComponent<StoryTextLayoutLock>();

        return dialogueTextGrowDownLock;
    }

    void FitDialoguePanelToSafeArea(Rect safe)
    {
        if (!dialogueSafeLayoutCaptured || dialoguePanel == null)
            return;

        if (dialoguePanel.anchorMin.x == dialoguePanel.anchorMax.x)
        {
            float targetWidth = Mathf.Min(ResolveActiveDialoguePanelSizeDelta().x, safe.width);
            if (targetWidth > 0f && !Mathf.Approximately(dialoguePanel.rect.width, targetWidth))
                dialoguePanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        }
    }

    void FitDialogueTextToPanel()
    {
        if (!dialogueSafeLayoutCaptured || dialogueTextRect == null)
            return;

        if (dialogueTextRect.anchorMin.x != dialogueTextRect.anchorMax.x)
            return;

        float baseWidth = Mathf.Max(1f, dialogueTextBaseSizeDelta.x);
        float availableWidth = dialoguePanel != null
            ? dialoguePanel.rect.width - dialogueTextHorizontalPadding
            : baseWidth;

        float targetWidth = Mathf.Clamp(availableWidth, 1f, baseWidth);
        if (!Mathf.Approximately(dialogueTextRect.rect.width, targetWidth))
            dialogueTextRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
    }

    void ApplyActiveDialoguePanelAutoLayout()
    {
        if (dialogueText != null)
            dialogueTextRect = dialogueText.rectTransform;

        if (dialoguePanel == null || dialogueText == null || dialogueTextRect == null)
            return;

        CaptureDialogueSafeLayout();

        bool changed = ApplyActiveBodyTextHorizontalClamp();
        changed |= ApplyActiveDialoguePanelAutoHeight();

        if (!changed)
            return;

        LayoutRebuilder.MarkLayoutForRebuild(dialoguePanel);
        if (!Application.isPlaying)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(dialoguePanel);
            Canvas.ForceUpdateCanvases();
        }
    }

    bool ApplyActiveBodyTextHorizontalClamp()
    {
        if (activeStoryUiStyle == null ||
            !activeStoryUiStyle.OverrideBodyTextHorizontalClamp ||
            !activeStoryUiStyle.BodyTextHorizontalClamp ||
            dialoguePanel == null ||
            dialogueText == null ||
            dialogueTextRect == null)
        {
            return false;
        }

        float inset = Mathf.Max(0f, activeStoryUiStyle.BodyTextHorizontalInset);
        float targetWidth = ResolveBodyTextClampWidth(inset);
        if (targetWidth <= 0f)
            return false;

        bool changed = false;

        if (!dialogueText.enableWordWrapping)
        {
            dialogueText.enableWordWrapping = true;
            changed = true;
        }

        TextOverflowModes targetOverflowMode = activeStoryUiStyle.OverrideBodyTextOverflowModeWhenStillTooLarge
            ? activeStoryUiStyle.BodyTextOverflowModeWhenStillTooLarge
            : TextOverflowModes.Ellipsis;
        if (dialogueText.overflowMode != targetOverflowMode)
        {
            dialogueText.overflowMode = targetOverflowMode;
            changed = true;
        }

        if (!Mathf.Approximately(dialogueTextRect.rect.width, targetWidth))
        {
            dialogueTextRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
            changed = true;
        }

        changed |= ClampDialogueTextRectInsidePanel(inset);
        return changed;
    }

    float ResolveBodyTextClampWidth(float inset)
    {
        if (dialoguePanel == null)
            return 0f;

        float availableWidth = Mathf.Max(1f, dialoguePanel.rect.width - inset * 2f);
        float maxWidth = activeStoryUiStyle != null ? activeStoryUiStyle.BodyTextMaxWidth : 0f;
        if (maxWidth > 0f)
            availableWidth = Mathf.Min(availableWidth, maxWidth);

        return Mathf.Max(1f, availableWidth);
    }

    bool ClampDialogueTextRectInsidePanel(float inset)
    {
        if (dialoguePanel == null || dialogueTextRect == null)
            return false;

        RectTransform textParent = dialogueTextRect.parent as RectTransform;
        if (textParent == null)
            return false;

        Bounds textBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(dialoguePanel, dialogueTextRect);
        Rect panelRect = dialoguePanel.rect;
        float minX = panelRect.xMin + inset;
        float maxX = panelRect.xMax - inset;
        if (maxX <= minX)
            return false;

        float deltaX = 0f;
        if (textBounds.size.x > maxX - minX)
            deltaX = ((minX + maxX) * 0.5f) - textBounds.center.x;
        else if (textBounds.min.x < minX)
            deltaX = minX - textBounds.min.x;
        else if (textBounds.max.x > maxX)
            deltaX = maxX - textBounds.max.x;

        if (Mathf.Approximately(deltaX, 0f))
            return false;

        dialogueTextRect.anchoredPosition += TransformDeltaBetweenRects(dialoguePanel, textParent, new Vector2(deltaX, 0f));
        return true;
    }

    bool ApplyActiveDialoguePanelAutoHeight()
    {
        if (activeStoryUiStyle == null ||
            !activeStoryUiStyle.OverrideDialoguePanelAutoHeight ||
            !activeStoryUiStyle.DialoguePanelAutoHeight ||
            dialoguePanel == null ||
            dialogueText == null ||
            dialogueTextRect == null)
        {
            return false;
        }

        float textWidth = Mathf.Max(1f, dialogueTextRect.rect.width);
        Vector2 preferredTextSize = dialogueText.GetPreferredValues(dialogueText.text ?? "", textWidth, Mathf.Infinity);

        float minHeight = activeStoryUiStyle.DialoguePanelAutoMinHeight;
        float baseHeight = ResolveActiveDialoguePanelSizeDelta().y;
        if (baseHeight <= 0f)
            baseHeight = dialoguePanel.rect.height;

        float maxHeight = activeStoryUiStyle.DialoguePanelAutoMaxHeight > 0f
            ? activeStoryUiStyle.DialoguePanelAutoMaxHeight
            : baseHeight;

        float textTopInset = ResolveDialogueTextTopInsetInPanel();
        float bottomPadding = ResolveDialoguePanelAutoBottomPadding(baseHeight);
        float targetHeight = textTopInset + preferredTextSize.y + bottomPadding;

        if (minHeight > 0f)
            targetHeight = Mathf.Max(targetHeight, minHeight);

        if (maxHeight > 0f)
            targetHeight = Mathf.Min(targetHeight, maxHeight);

        targetHeight = Mathf.Max(1f, targetHeight);
        bool changed = SetRectHeight(
            dialoguePanel,
            targetHeight,
            activeStoryUiStyle.DialoguePanelAutoHeightKeepTop,
            activeStoryUiStyle.DialoguePanelAutoHeightGrowthUpFactor);
        changed |= ApplyDialogueExtraLayerAutoHeight(targetHeight);
        return changed;
    }

    float ResolveDialogueTextTopInsetInPanel()
    {
        if (dialoguePanel == null || dialogueTextRect == null)
            return 0f;

        Bounds textBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(dialoguePanel, dialogueTextRect);
        return Mathf.Max(0f, dialoguePanel.rect.yMax - textBounds.max.y);
    }

    float ResolveDialoguePanelAutoBottomPadding(float baseHeight)
    {
        if (activeStoryUiStyle == null)
            return 0f;

        float padding = Mathf.Max(0f, activeStoryUiStyle.DialoguePanelAutoHeightPadding);
        if (baseHeight > 0f)
            padding = Mathf.Min(padding, Mathf.Max(0f, baseHeight * 0.35f));

        return padding;
    }

    bool ApplyDialogueExtraLayerAutoHeight(float targetHeight)
    {
        if (activeStoryUiStyle == null || activeStoryUiStyle.DialogueExtraLayers == null)
            return false;

        if (dialogueExtraBackgroundImages == null || dialogueExtraBackgroundImages.Count == 0)
            dialogueExtraBackgroundImages = FindDialogueExtraBackgroundImages();

        bool changed = false;
        var used = new HashSet<Image>();
        IReadOnlyList<DialoguePanelExtraLayerStyle> layers = activeStoryUiStyle.DialogueExtraLayers;
        for (int i = 0; i < layers.Count; i++)
        {
            DialoguePanelExtraLayerStyle layer = layers[i];
            if (layer == null || !layer.Enabled || !layer.MatchDialoguePanelAutoHeight)
                continue;

            Image target = ResolveDialogueExtraLayerTarget(layer, i, used);
            if (target == null)
                continue;

            used.Add(target);
            RectTransform rect = target.rectTransform;
            if (rect == null || rect == dialoguePanel)
                continue;

            changed |= SetRectHeight(
                rect,
                targetHeight,
                activeStoryUiStyle.DialoguePanelAutoHeightKeepTop,
                activeStoryUiStyle.DialoguePanelAutoHeightGrowthUpFactor);
        }

        return changed;
    }

    static bool SetRectHeight(RectTransform rect, float targetHeight, bool keepTop, float growthUpFactor = 0f)
    {
        if (rect == null || Mathf.Approximately(rect.rect.height, targetHeight))
            return false;

        float previousHeight = rect.rect.height;
        Vector3 topBefore = keepTop ? GetWorldTopCenter(rect) : Vector3.zero;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        if (keepTop)
            rect.position += topBefore - GetWorldTopCenter(rect);

        float growthShiftY = (targetHeight - previousHeight) * Mathf.Max(0f, growthUpFactor);
        if (Mathf.Abs(growthShiftY) >= 0.01f)
            rect.anchoredPosition += new Vector2(0f, growthShiftY);

        return true;
    }

    static Vector3 GetWorldTopCenter(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return (corners[1] + corners[2]) * 0.5f;
    }

    static Vector2 TransformDeltaBetweenRects(RectTransform source, RectTransform targetParent, Vector2 sourceLocalDelta)
    {
        Vector3 worldOrigin = source.TransformPoint(Vector3.zero);
        Vector3 worldDelta = source.TransformPoint(new Vector3(sourceLocalDelta.x, sourceLocalDelta.y, 0f));
        return (Vector2)(targetParent.InverseTransformPoint(worldDelta) - targetParent.InverseTransformPoint(worldOrigin));
    }

    public void ClearChoices()
    {
        dedicatedPremiumChoiceButtons.Clear();
        SetPremiumChoiceBalancePanelVisible(false);

        if (choiceContainer == null) return;

        List<GameObject> children = new List<GameObject>();
        foreach (Transform t in choiceContainer)
            children.Add(t.gameObject);

        for (int i = 0; i < children.Count; i++)
            DestroyUiObject(children[i]);

        ApplyChoiceSpacing(0);
        choiceContainer.gameObject.SetActive(false);
    }

    static void DestroyUiObject(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
        {
            Destroy(target);
            return;
        }

        DestroyImmediate(target);
    }

    public void ResetStoryUi()
    {
        EndWardrobeScreenMode(true, false);
        ClearDialogue();
        ClearChoices();
        SetDialoguePanelVisible(false);

        SetWardrobePanelVisible(false);

        if (purchasePopup != null)
            purchasePopup.SetActive(false);
    }

    public bool ShowChoice(ChoiceNode node)
    {
        if (!EnsureChoiceUi())
        {
            SetPremiumChoiceBalancePanelVisible(false);
            return false;
        }

        EnsureStoryUiVisible();
        ClearChoices();
        PrepareChoiceContainer();
        ShowChoicePlaceholderIfDialogueEmpty();

        if (node == null || node.options == null)
            return false;

        int visibleCount = 0;
        bool hasVisiblePremiumChoice = false;
        for (int i = 0; i < node.options.Count; i++)
        {
            int index = i;

            var option = node.options[i];
            if (!ChoiceRegionFilter.IsVisible(option))
                continue;

            bool premiumChoice = option != null && option.isPremium;
            bool paidChoiceForBalancePanel = IsPaidChoiceForBalancePanel(option);

            Button button = CreateChoiceButton(out GameObject choiceRoot, premiumChoice);
            if (button == null)
                continue;

            visibleCount++;
            if (paidChoiceForBalancePanel)
                hasVisiblePremiumChoice = true;

            TMP_Text label = FindChoiceButtonLabel(choiceRoot, button);
            if (label != null)
                label.text = ReplacePlaceholdersSafe(option != null ? option.text : "");

            SetChoiceButtonCostText(choiceRoot, button, premiumChoice && option != null ? option.premiumCost : 0);
            ApplyActiveChoiceButtonStyle(button, premiumChoice);
            RefreshChoiceButtonLayout(button);

            RegisterChoiceButtonClick(button, () =>
            {
                StoryManager.Instance?.SelectChoice(node, index);
            });
        }

        ApplyChoiceLayout(visibleCount);
        SetPremiumChoiceBalancePanelVisible(visibleCount > 0 && hasVisiblePremiumChoice);
        return visibleCount > 0;
    }

    public void OpenWardrobe()
    {
        if (!EnsureWardrobePanel()) return;

        if (OpenHeroWardrobeSetup(null))
            return;

        BeginWardrobeScreenMode();
    }

    public void CloseWardrobe()
    {
        AutoWireRequiredReferences();

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(DialogueUIManager),
            nameof(CloseWardrobe),
            "[WARDROBE][UI] Closing wardrobe UI.",
            LogMetadata.Of(
                "wardrobePanel", wardrobePanel != null ? wardrobePanel.name : "",
                "wardrobePanelActive", wardrobePanel != null && wardrobePanel.activeSelf,
                "wardrobeScreenModeActive", wardrobeScreenModeActive,
                "activeSetupPage", activeWardrobeSetupPage != null ? activeWardrobeSetupPage.name : ""));

        CloseAllWardrobeHeroSetupPages();
        EndWardrobeScreenMode(true);
    }

    public bool OpenHeroWardrobeSetup(
        System.Action onComplete,
        bool skipWhenAlreadyCompleted = false,
        System.Action onCancel = null,
        GameData contextData = null,
        bool saveProgressOnComplete = true)
    {
        if (!EnsureWardrobePanel())
            return false;

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(DialogueUIManager),
            nameof(OpenHeroWardrobeSetup),
            "[WARDROBE][UI] Opening full wardrobe setup from story.",
            LogMetadata.Of(
                "contextGame", contextData != null ? contextData.name : "",
                "contextStoryId", ResolveGameDataStoryId(contextData),
                "skipWhenAlreadyCompleted", skipWhenAlreadyCompleted,
                "saveProgressOnComplete", saveProgressOnComplete,
                "hasCompleteCallback", onComplete != null,
                "hasCancelCallback", onCancel != null));

        var setupPage = FindWardrobeHeroSetupPage(page => page.UseForOpenWardrobeNode);
        if (setupPage == null)
        {
            Debug.LogError("[DialogueUIManager] WardrobeHeroSetupPage with Use For Open Wardrobe Node enabled is required for the custom wardrobe setup UI.", this);
            return false;
        }

        if (contextData != null)
            setupPage.PrepareForStory(contextData);

        BeginWardrobeScreenMode(setupPage);
        bool opened = setupPage.OpenFullSetup(onComplete, onCancel, skipWhenAlreadyCompleted, saveProgressOnComplete);
        if (!opened)
            EndWardrobeScreenMode(true);

        return opened;
    }

    public bool OpenWardrobeChoice(WardrobeChoiceNode node, GameData contextData = null)
    {
        if (!EnsureWardrobePanel()) return false;

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(DialogueUIManager),
            nameof(OpenWardrobeChoice),
            "[WARDROBE][UI] Opening wardrobe choice UI.",
            LogMetadata.Of(
                "contextGame", contextData != null ? contextData.name : "",
                "contextStoryId", ResolveGameDataStoryId(contextData),
                "nodeGuid", node != null ? node.guid : "",
                "nodeName", node != null ? node.name : "",
                "itemCount", node != null && node.availableClothes != null ? node.availableClothes.Count : 0));

        if (TryOpenWardrobeChoiceOnWardrobePage(node, contextData))
            return true;

        if (TryOpenWardrobeChoiceOnArrowWardrobe(node))
            return true;

        Debug.LogError("[DialogueUIManager] WardrobeChoiceNode requires a wardrobe screen, but no story wardrobe UI is available.", this);
        return false;

    }


    public void ShowPurchasePopup(
    string chapterName,
    int price,
    System.Action onBuy,
    System.Action onCancel)
    {
        if (purchasePopup == null || buyButton == null || cancelButton == null)
        {
            Debug.LogError("[DialogueUIManager] purchase popup references are not assigned.", this);
            onCancel?.Invoke();
            return;
        }

        purchasePopup.SetActive(true);

        if (purchaseTitle != null)
            purchaseTitle.text = $"Глава {chapterName} закрыта";
        if (purchasePrice != null)
            purchasePrice.text = $"Открыть главу за {price} свечей";

        buyButton.onClick.RemoveAllListeners();
        cancelButton.onClick.RemoveAllListeners();

        buyButton.onClick.AddListener(() =>
        {
            purchasePopup.SetActive(false);
            onBuy?.Invoke();
        });

        cancelButton.onClick.AddListener(() =>
        {
            purchasePopup.SetActive(false);
            onCancel?.Invoke();
        });
    }

    public void ShowAppearancePrompt(string text)
    {
        if (!EnsureDialogueText())
            return;

        EnsureStoryUiVisible();
        SetDialogueTexts("", ReplacePlaceholdersSafe(text));
    }

    public bool ShowAppearanceChoice(AppearanceChoiceNode node)
    {
        if (TryOpenAppearanceChoiceOnWardrobePage(node))
            return true;

        if (TryOpenAppearanceChoiceOnArrowWardrobe(node))
            return true;

        if (!EnsureChoiceUi())
            return false;

        EnsureStoryUiVisible();
        ClearChoices();
        PrepareChoiceContainer();
        ShowChoicePlaceholderIfDialogueEmpty();

        if (node == null || node.options == null)
            return false;

        int visibleCount = 0;
        for (int i = 0; i < node.options.Count; i++)
        {
            int index = i;
            var option = node.options[i];

            Button button = CreateChoiceButton(out GameObject choiceRoot);
            if (button == null)
                continue;

            visibleCount++;
            TMP_Text label = FindChoiceButtonLabel(choiceRoot, button);
            if (label != null)
                label.text = ReplacePlaceholdersSafe(option != null ? option.label : "");

            SetChoiceButtonCostText(choiceRoot, button, 0);
            ApplyActiveChoiceButtonStyle(button);
            RefreshChoiceButtonLayout(button);

            RegisterChoiceButtonClick(button, () =>
            {
                StoryManager.Instance?.SelectAppearance(node, index);
            });
        }

        ApplyChoiceLayout(visibleCount);
        return node.options.Count > 0;
    }

    void ApplyChoiceSpacing(int visibleChoiceCount)
    {
        if (!adjustChoiceSpacingByVisibleCount || choiceContainer == null)
            return;

        var layout = choiceContainer.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            return;

        int threshold = Mathf.Max(0, compactChoiceSpacingThreshold);
        float targetSpacing = visibleChoiceCount > threshold
            ? compactChoiceSpacing
            : regularChoiceSpacing;

        if (Mathf.Approximately(layout.spacing, targetSpacing))
            return;

        layout.spacing = targetSpacing;

        if (choiceContainer is RectTransform choiceRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(choiceRect);
    }

    void ApplyChoiceLayout(int visibleChoiceCount)
    {
        ApplyChoiceSpacing(visibleChoiceCount);

        DialogueChoiceLayout groupLayout = ResolveChoiceLayout();
        if (groupLayout != null)
        {
            if (activeStoryUiStyle != null)
                activeStoryUiStyle.ApplyToChoiceLayout(groupLayout);
            else
                groupLayout.RefreshNow();
        }
        else
        {
            EqualizeChoiceButtonsByVisibleLength();
        }

        ApplyChoiceHeightSpacingLayout(visibleChoiceCount);
    }

    bool TryOpenAppearanceChoiceOnArrowWardrobe(AppearanceChoiceNode node)
    {
        if (node == null || node.options == null || node.options.Count == 0)
            return false;

        if (!EnsureWardrobePanel())
            return false;

        var wardrobe = FindWardrobeController();
        if (wardrobe == null)
            return false;

        BeginWardrobeScreenMode();
        wardrobe.OpenAppearance(node.options, index =>
        {
            CloseWardrobe();
            StoryManager.Instance?.SelectAppearance(node, index);
        });

        return true;
    }

    bool TryOpenAppearanceChoiceOnWardrobePage(AppearanceChoiceNode node)
    {
        if (node == null || node.options == null || node.options.Count == 0)
            return false;

        if (!EnsureWardrobePanel())
            return false;

        var setupPage = FindWardrobeHeroSetupPage(page => page.UseForStoryAppearanceChoices);
        if (setupPage == null)
            return false;

        BeginWardrobeScreenMode(setupPage);
        bool opened = setupPage.OpenStoryAppearanceChoice(node, index =>
        {
            CloseWardrobe();
            StoryManager.Instance?.SelectAppearance(node, index);
        });

        if (!opened)
            EndWardrobeScreenMode(true);

        return opened;
    }

    bool TryOpenWardrobeChoiceOnWardrobePage(WardrobeChoiceNode node, GameData contextData)
    {
        if (node == null || node.availableClothes == null || node.availableClothes.Count == 0)
            return false;

        var setupPage = FindWardrobeHeroSetupPage(page => page.UseForStoryWardrobeChoices);
        if (setupPage == null)
            return false;

        if (contextData != null)
            setupPage.PrepareForStory(contextData);

        BeginWardrobeScreenMode(setupPage);
        bool opened = setupPage.OpenStoryWardrobeChoice(node, index =>
        {
            StoryManager.Instance?.SelectClothing(node, index);
        });

        if (opened)
        {
            AppLogger.Info(
                AppLogCategory.Wardrobe,
                nameof(DialogueUIManager),
                nameof(TryOpenWardrobeChoiceOnWardrobePage),
                "[WARDROBE][UI] Story wardrobe choice opened on WardrobeHeroSetupPage.",
                LogMetadata.Of(
                    "setupPage", setupPage.name,
                    "nodeGuid", node.guid,
                    "nodeName", node.name,
                    "itemCount", node.availableClothes != null ? node.availableClothes.Count : 0,
                    "contextGame", contextData != null ? contextData.name : "",
                    "contextStoryId", ResolveGameDataStoryId(contextData)));
            SyncWardrobeTabsToStoryWardrobeChoice(setupPage, node);
        }
        else
        {
            AppLogger.Warn(
                AppLogCategory.Wardrobe,
                nameof(DialogueUIManager),
                nameof(TryOpenWardrobeChoiceOnWardrobePage),
                "[WARDROBE][UI] WardrobeHeroSetupPage rejected story wardrobe choice.",
                LogMetadata.Of(
                    "setupPage", setupPage.name,
                    "nodeGuid", node.guid,
                    "nodeName", node.name,
                    "itemCount", node.availableClothes != null ? node.availableClothes.Count : 0,
                    "contextGame", contextData != null ? contextData.name : "",
                    "contextStoryId", ResolveGameDataStoryId(contextData)),
                recoverable: true);
            EndWardrobeScreenMode(true);
        }

        return opened;
    }

    static string ResolveGameDataStoryId(GameData data)
    {
        if (data == null || data.Story == null)
            return "";

        string storyId = SaveDataSanitizer.SanitizeIdentifier(data.Story.StoryId);
        if (!string.IsNullOrEmpty(storyId))
            return storyId;

        storyId = SaveDataSanitizer.SanitizeIdentifier(data.Story.storyId);
        if (!string.IsNullOrEmpty(storyId))
            return storyId;

        return SaveDataSanitizer.SanitizeIdentifier(data.Story.name);
    }

    void SyncWardrobeTabsToStoryWardrobeChoice(WardrobeHeroSetupPage setupPage, WardrobeChoiceNode node)
    {
        if (setupPage == null || node == null || node.availableClothes == null)
            return;

        WardrobeCategoryTabs tabs = FindWardrobeCategoryTabsFor(setupPage);
        if (tabs == null)
            return;

        tabs.AssignWardrobePage(setupPage);

        WardrobeCategoryTabType category = GetFirstVisibleWardrobeChoiceCategory(node);
        if (category != WardrobeCategoryTabType.None)
            tabs.OpenCategory(category);
    }

    WardrobeCategoryTabs FindWardrobeCategoryTabsFor(WardrobeHeroSetupPage setupPage)
    {
        WardrobeCategoryTabs tabs = setupPage != null
            ? setupPage.GetComponentInChildren<WardrobeCategoryTabs>(true)
            : null;
        if (tabs != null)
            return tabs;

        if (wardrobePanel != null)
        {
            tabs = wardrobePanel.GetComponentInChildren<WardrobeCategoryTabs>(true);
            if (tabs != null)
                return tabs;
        }

        return FindObjectOfType<WardrobeCategoryTabs>(true);
    }

    WardrobeCategoryTabType GetFirstVisibleWardrobeChoiceCategory(WardrobeChoiceNode node)
    {
        if (node == null || node.availableClothes == null)
            return WardrobeCategoryTabType.None;

        string targetCharacterId = FirstNonEmpty(node.characterId, node.character != null ? node.character.name : "", "hero");
        StoryManager manager = StoryManager.Instance;
        string storyId = manager != null ? manager.CurrentStoryId : "";
        string chapterId = manager != null ? FirstNonEmpty(manager.CurrentChapterId, manager.CurrentEpisodeId) : "";

        for (int i = 0; i < node.availableClothes.Count; i++)
        {
            if (!node.IsOptionVisible(i))
                continue;

            ClothingItem item = node.availableClothes[i];
            if (item == null || !item.IsAvailableForWardrobe(targetCharacterId, storyId, chapterId))
                continue;

            return GetWardrobeCategoryForClothingType(item.type);
        }

        return WardrobeCategoryTabType.None;
    }

    static WardrobeCategoryTabType GetWardrobeCategoryForClothingType(ClothingType type)
    {
        switch (type)
        {
            case ClothingType.Hair:
                return WardrobeCategoryTabType.Hair;
            case ClothingType.Outfit:
                return WardrobeCategoryTabType.Outfit;
            case ClothingType.Accessory:
                return WardrobeCategoryTabType.Accessories;
            default:
                return WardrobeCategoryTabType.None;
        }
    }

    bool TryOpenWardrobeChoiceOnArrowWardrobe(WardrobeChoiceNode node)
    {
        if (node == null || node.availableClothes == null || node.availableClothes.Count == 0)
            return false;

        var wardrobe = FindWardrobeController();
        if (wardrobe == null)
            return false;

        var visibleClothes = new List<ClothingItem>();
        var sourceIndexes = new List<int>();
        string targetCharacterId = FirstNonEmpty(node.characterId, node.character != null ? node.character.name : "", "hero");
        StoryManager manager = StoryManager.Instance;
        string storyId = manager != null ? manager.CurrentStoryId : "";
        string chapterId = manager != null ? FirstNonEmpty(manager.CurrentChapterId, manager.CurrentEpisodeId) : "";
        for (int i = 0; i < node.availableClothes.Count; i++)
        {
            if (!node.IsOptionVisible(i))
                continue;

            ClothingItem item = node.availableClothes[i];
            if (item == null || !item.IsAvailableForWardrobe(targetCharacterId, storyId, chapterId))
                continue;

            visibleClothes.Add(item);
            sourceIndexes.Add(i);
        }

        if (visibleClothes.Count == 0)
            return false;

        BeginWardrobeScreenMode();
        wardrobe.Open(node.characterId, node.character, visibleClothes, index =>
        {
            if (index < 0 || index >= sourceIndexes.Count)
                return;

            StoryManager.Instance?.SelectClothing(node, sourceIndexes[index]);
        });

        return true;
    }

    WardrobeHeroSetupPage FindWardrobeHeroSetupPage(System.Predicate<WardrobeHeroSetupPage> predicate = null)
    {
        StoryManager manager = StoryManager.Instance;
        string storyId = manager != null ? manager.CurrentStoryId : "";
        string chapterId = manager != null ? FirstNonEmpty(manager.CurrentChapterId, manager.CurrentEpisodeId) : "";

        WardrobeHeroSetupPage page = WardrobeHeroSetupPage.FindBestForStory((Transform)null, storyId, chapterId, predicate);
        if (page != null)
            return page;

        if (wardrobePanel != null)
        {
            page = WardrobeHeroSetupPage.FindBestForStory(wardrobePanel.transform, storyId, chapterId, predicate);
            if (page != null)
                return page;

            page = FindFirstWardrobeHeroSetupPage(wardrobePanel.transform, predicate);
            if (page != null)
                return page;
        }

        return FindFirstWardrobeHeroSetupPage(null, predicate);
    }

    WardrobeHeroSetupPage FindFirstWardrobeHeroSetupPage(
        Transform searchRoot,
        System.Predicate<WardrobeHeroSetupPage> predicate = null)
    {
        WardrobeHeroSetupPage[] pages = searchRoot != null
            ? searchRoot.GetComponentsInChildren<WardrobeHeroSetupPage>(true)
            : FindObjectsOfType<WardrobeHeroSetupPage>(true);

        for (int i = 0; i < pages.Length; i++)
        {
            WardrobeHeroSetupPage page = pages[i];
            if (page == null || !page.gameObject.scene.IsValid())
                continue;

            if (predicate != null && !predicate(page))
                continue;

            return page;
        }

        return null;
    }

    void CloseAllWardrobeHeroSetupPages(WardrobeHeroSetupPage except = null)
    {
        WardrobeHeroSetupPage[] pages = FindObjectsOfType<WardrobeHeroSetupPage>(true);
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null && pages[i] != except)
                pages[i].Close();
        }
    }

    void AssignWardrobeCategoryTabs(WardrobeHeroSetupPage setupPage)
    {
        if (setupPage == null)
            return;

        AssignWardrobeCategoryTabsInRoot(setupPage.transform, setupPage);

        if (wardrobePanel != null)
            AssignWardrobeCategoryTabsInRoot(wardrobePanel.transform, setupPage);
    }

    static void AssignWardrobeCategoryTabsInRoot(Transform root, WardrobeHeroSetupPage setupPage)
    {
        if (root == null || setupPage == null)
            return;

        WardrobeCategoryTabs[] tabs = root.GetComponentsInChildren<WardrobeCategoryTabs>(true);
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] != null)
                tabs[i].AssignWardrobePage(setupPage);
        }
    }

    static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return "";
    }

    WardrobeController FindWardrobeController()
    {
        if (wardrobePanel == null)
            return null;

        var wardrobe = wardrobePanel.GetComponentInChildren<WardrobeController>(true);
        if (wardrobe == null)
            wardrobe = FindObjectOfType<WardrobeController>(true);

        return wardrobe;
    }

    void PrepareChoiceContainer()
    {
        if (choiceContainer == null)
            return;

        choiceContainer.gameObject.SetActive(true);
        choiceContainer.SetAsLastSibling();
        ApplyActiveChoicePanelStyle();
    }

    DialogueChoiceLayout ResolveChoiceLayout()
    {
        if (!useDialogueChoiceLayout || choiceContainer == null)
            return null;

        if (choiceLayout == null)
            choiceLayout = choiceContainer.GetComponent<DialogueChoiceLayout>();

        return choiceLayout;
    }

    void ApplyChoiceHeightSpacingLayout(int visibleChoiceCount)
    {
        ChoiceHeightSpacingLayout heightLayout = ResolveChoiceHeightSpacingLayout();
        if (heightLayout == null)
            return;

        heightLayout.RefreshNow(visibleChoiceCount);
    }

    ChoiceHeightSpacingLayout ResolveChoiceHeightSpacingLayout()
    {
        if (!useChoiceHeightSpacingLayout || choiceContainer == null)
            return null;

        if (choiceHeightSpacingLayout == null)
            choiceHeightSpacingLayout = choiceContainer.GetComponent<ChoiceHeightSpacingLayout>();

        if (choiceHeightSpacingLayout == null && createChoiceHeightSpacingLayoutIfMissing && Application.isPlaying)
            choiceHeightSpacingLayout = choiceContainer.gameObject.AddComponent<ChoiceHeightSpacingLayout>();

        return choiceHeightSpacingLayout;
    }

    void ApplyActiveChoicePanelStyle()
    {
        if (activeStoryUiStyle == null)
            return;

        activeStoryUiStyle.ApplyToChoicePanel(FindChoiceContainerBackgroundImage());
    }

    void ApplyActiveChoiceButtonStyle(Button button, bool premiumChoice = false)
    {
        if (button == null || activeStoryUiStyle == null)
            return;

        if ((premiumChoice && HasDedicatedPremiumChoiceButtonPrefab()) ||
            dedicatedPremiumChoiceButtons.ContainsKey(button))
            return;

        activeStoryUiStyle.ApplyToChoiceButton(button);
    }

    void ApplyActiveStyleToVisibleChoiceButtons()
    {
        if (choiceContainer == null || activeStoryUiStyle == null)
            return;

        foreach (Transform child in choiceContainer)
        {
            if (child == null)
                continue;

            Button button = FindChoiceButtonComponent(child.gameObject);
            if (button != null)
                ApplyActiveChoiceButtonStyle(button);
        }
    }

    void RefreshChoiceButtonLayout(Button button)
    {
        if (button == null)
            return;

        var autoSize = button.GetComponent<ButtonTextAutoSize>();
        if (autoSize == null)
            autoSize = button.GetComponentInParent<ButtonTextAutoSize>();
        if (autoSize != null)
            autoSize.RefreshNow();

        if (choiceContainer is RectTransform choiceRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(choiceRect);
    }

    void EqualizeChoiceButtonsByVisibleLength()
    {
        if (ResolveChoiceLayout() != null)
            return;

        if (!equalizeChoiceButtonsByVisibleLength || choiceContainer == null)
            return;

        Canvas.ForceUpdateCanvases();

        var groups = new Dictionary<int, List<ChoiceButtonLayoutEntry>>();
        foreach (Transform child in choiceContainer)
        {
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            Button button = FindChoiceButtonComponent(child.gameObject);
            RectTransform rect = child as RectTransform;
            TMP_Text label = FindChoiceButtonLabel(child.gameObject, button);

            if (button == null || rect == null || label == null)
                continue;

            int visibleLength = StoryManager.CountVisibleDialogueChars(label.text);
            if (visibleLength <= 0)
                continue;

            RefreshChoiceButtonLayout(button);

            if (!groups.TryGetValue(visibleLength, out var group))
            {
                group = new List<ChoiceButtonLayoutEntry>();
                groups.Add(visibleLength, group);
            }

            group.Add(new ChoiceButtonLayoutEntry(button, rect, label));
        }

        foreach (var pair in groups)
        {
            List<ChoiceButtonLayoutEntry> group = pair.Value;
            if (group.Count < 2)
                continue;

            float targetWidth = 0f;
            float targetHeight = 0f;
            float targetFontSize = float.MaxValue;

            for (int i = 0; i < group.Count; i++)
            {
                ChoiceButtonLayoutEntry entry = group[i];
                targetWidth = Mathf.Max(targetWidth, entry.Rect.rect.width);
                targetHeight = Mathf.Max(targetHeight, entry.Rect.rect.height);
                targetFontSize = Mathf.Min(targetFontSize, entry.Label.fontSize);
            }

            targetWidth = Mathf.Ceil(targetWidth);
            targetHeight = Mathf.Ceil(targetHeight);

            for (int i = 0; i < group.Count; i++)
            {
                ChoiceButtonLayoutEntry entry = group[i];

                if (equalizeChoiceFontSizeByVisibleLength && targetFontSize < float.MaxValue)
                {
                    entry.Label.fontSize = targetFontSize;
                    if (entry.Label.enableAutoSizing && entry.Label.fontSizeMax > 0f)
                        entry.Label.fontSizeMax = Mathf.Min(entry.Label.fontSizeMax, targetFontSize);
                }

                LayoutElement layout = entry.Button.GetComponent<LayoutElement>();
                if (layout == null)
                    layout = entry.Button.gameObject.AddComponent<LayoutElement>();

                layout.minWidth = targetWidth;
                layout.preferredWidth = targetWidth;
                layout.minHeight = targetHeight;
                layout.preferredHeight = targetHeight;
            }
        }

        if (choiceContainer is RectTransform choiceRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(choiceRect);
    }

    readonly struct ChoiceButtonLayoutEntry
    {
        public readonly Button Button;
        public readonly RectTransform Rect;
        public readonly TMP_Text Label;

        public ChoiceButtonLayoutEntry(Button button, RectTransform rect, TMP_Text label)
        {
            Button = button;
            Rect = rect;
            Label = label;
        }
    }
}
