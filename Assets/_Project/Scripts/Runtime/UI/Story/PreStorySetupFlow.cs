using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public sealed class PreStoryAppearanceOptionBinding
{
    [Tooltip("Кнопка, которой игрок выбирает этот вариант внешности.")]
    [SerializeField] private Button _button;

    [Tooltip("Тип внешности, который сохранится после нажатия этой кнопки.")]
    [SerializeField] private AppearanceType _appearanceType = AppearanceType.Default;

    [Tooltip("Текст на кнопке варианта внешности. Можно оставить пустым, если надпись уже настроена в префабе.")]
    [SerializeField] private TMP_Text _labelText;

    [Tooltip("Название варианта внешности, которое скрипт запишет в текст кнопки.")]
    [SerializeField] private string _label;

    [Tooltip("Image для превью внешности. Можно оставить пустым, если превью уже настроено в префабе.")]
    [SerializeField] private Image _previewImage;

    [Tooltip("Спрайт превью, который будет показан для этого варианта внешности.")]
    [SerializeField] private Sprite _previewSprite;

    [Tooltip("Объект-индикатор выбранного варианта, например рамка или галочка.")]
    [SerializeField] private GameObject _selectedMarker;

    private UnityAction _clickAction;

    public AppearanceType AppearanceType => _appearanceType;
    public Button Button => _button;

    public void Bind(int optionIndex, Action<int> onSelected)
    {
        Unbind();

        if (_button == null || onSelected == null)
            return;

        _clickAction = () => onSelected(optionIndex);
        _button.onClick.AddListener(_clickAction);
    }

    public void Unbind()
    {
        if (_button != null && _clickAction != null)
            _button.onClick.RemoveListener(_clickAction);

        _clickAction = null;
    }

    public void RefreshView(bool selected)
    {
        if (_labelText != null && !string.IsNullOrEmpty(_label))
            _labelText.text = _label;

        if (_previewImage != null && _previewSprite != null)
        {
            _previewImage.sprite = _previewSprite;
            _previewImage.enabled = true;
        }

        if (_selectedMarker != null)
            _selectedMarker.SetActive(selected);
    }
}

[Serializable]
public sealed class PreStoryNamePanelBackgroundOverride
{
    [Tooltip("Story ID, для которого нужно заменить фон экрана ввода имени.")]
    [SerializeField] private string _storyId;

    [Tooltip("Sprite фона экрана ввода имени для этой истории.")]
    [SerializeField] private Sprite _backgroundSprite;

    public Sprite BackgroundSprite => _backgroundSprite;

    public bool Matches(string storyId)
    {
        return Normalize(_storyId) == Normalize(storyId);
    }

    public void Validate()
    {
        _storyId = Normalize(_storyId);
    }

    static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }
}

[Serializable]
public sealed class PreStoryDefaultHeroNameOverride
{
    [Tooltip("Story ID, для которого нужно своё стартовое имя героини.")]
    [SerializeField] private string _storyId;

    [Tooltip("Имя, которое подставится в поле имени только для этой истории.")]
    [SerializeField] private string _defaultHeroName;

    public string DefaultHeroName => _defaultHeroName;

    public bool Matches(string storyId)
    {
        return Normalize(_storyId) == Normalize(storyId);
    }

    public void Validate()
    {
        _storyId = Normalize(_storyId);
        _defaultHeroName = string.IsNullOrWhiteSpace(_defaultHeroName) ? "" : _defaultHeroName.Trim();
    }

    static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Novel Template/UI/Pre Story Setup Flow")]
public sealed class PreStorySetupFlow : MonoBehaviour
{
    private const string SetupCompletedKey = "VN_PRE_STORY_SETUP_DONE";
    private const string FallbackNamePanelObjectName = "inputName";
    private const string DefaultIntroMessage = "История начнётся совсем скоро! А пока выберите внешний вид и имя для вашей героини.";

    [Header("Назначение: плашка, выбор имени и внешности перед стартом истории")]
    [Tooltip("Корневой объект всего предстартового UI. Скрипт включает его при запуске процесса и выключает после завершения.")]
    [SerializeField] private GameObject _rootObject;

    [Tooltip("Если включено, предстартовый процесс будет пропущен после первого успешного завершения.")]
    [SerializeField] private bool _skipAfterFirstCompletion = true;

    [Header("Плашка перед историей")]
    [Tooltip("Панель с вводным сообщением перед выбором имени и внешности.")]
    [SerializeField] private GameObject _introPanel;

    [Tooltip("TMP_Text, в который выводится текст вводной плашки.")]
    [SerializeField] private TMP_Text _introMessageText;

    [Tooltip("Текст, который игрок увидит на вводной плашке перед историей.")]
    [TextArea]
    [SerializeField] private string _introMessage = DefaultIntroMessage;

    [Tooltip("Кнопка перехода с вводной плашки к выбору имени.")]
    [SerializeField] private Button _introContinueButton;

    [Tooltip("Показывать вводную плашку перед выбором имени и внешности.")]
    [SerializeField] private bool _showIntroStep = true;

    [Header("Экран выбора имени")]
    [Tooltip("Панель, на которой игрок выбирает имя героини.")]
    [SerializeField] private GameObject _namePanel;

    [Tooltip("Большой Image фона всего экрана ввода имени. Обычно это объект Background рядом с PreStoryFlow.")]
    [SerializeField] private Image _nameScreenBackgroundImage;

    [Tooltip("Автоматически искать большой фон экрана ввода имени, если поле выше не назначено.")]
    [SerializeField] private bool _autoFindNameScreenBackgroundImage = true;

    [Tooltip("TMP_InputField, куда игрок вводит имя героини.")]
    [SerializeField] private TMP_InputField _nameInputField;

    [Tooltip("Текст placeholder внутри поля имени. Если назначен, скрипт подставит туда стартовое имя.")]
    [SerializeField] private TMP_Text _namePlaceholderText;

    [Tooltip("Кнопка подтверждения введённого имени.")]
    [SerializeField] private Button _nameConfirmButton;

    [Tooltip("Текст, который будет автоматически записан в кнопку продолжения, если у prefab/кнопки есть TMP_Text или его нужно создать.")]
    [SerializeField] private string _nameConfirmButtonLabel = "Продолжить";

    [Tooltip("Родитель, куда Story UI Style будет создавать prefab кнопки продолжения. Если пусто, используется родитель текущей кнопки или панель имени.")]
    [SerializeField] private Transform _nameConfirmButtonPrefabParent;

    [Tooltip("Дополнительный TMP_Text на экране ввода имени. Если поле пустое, стиль истории может создать его автоматически.")]
    [SerializeField] private TMP_Text _nameExtraTextOne;

    [Tooltip("Второй дополнительный TMP_Text на экране ввода имени. Если поле пустое, стиль истории может создать его автоматически.")]
    [SerializeField] private TMP_Text _nameExtraTextTwo;

    [Tooltip("Дополнительные TMP_Text на экране ввода имени. Стили историй могут создавать новые элементы автоматически.")]
    [SerializeField] private List<TMP_Text> _nameExtraTexts = new List<TMP_Text>();

    [Tooltip("Image фона панели ввода имени. Если поле пустое, скрипт попробует найти Background/Backgrund внутри Name Panel.")]
    [SerializeField] private Image _namePanelBackgroundImage;

    [Tooltip("Автоматически искать Image фона панели ввода имени, если поле выше не назначено.")]
    [SerializeField] private bool _autoFindNamePanelBackgroundImage = true;

    [Tooltip("Фоны панели ввода имени по Story ID. Так PP может использовать hero_name.png, а ZLS оставить свой фон.")]
    [HideInInspector]
    [SerializeField] private List<PreStoryNamePanelBackgroundOverride> _namePanelBackgroundOverrides = new List<PreStoryNamePanelBackgroundOverride>();

    [Tooltip("Имя героини, которое будет сохранено, если игрок оставит поле пустым.")]
    [SerializeField] private string _defaultHeroName = "\u0413\u0435\u0440\u043e\u0438\u043d\u044f";

    [Tooltip("Стартовые имена по Story ID. Используется раньше общего Default Hero Name и не протекает в другие истории.")]
    [SerializeField] private List<PreStoryDefaultHeroNameOverride> _defaultHeroNameOverrides = new List<PreStoryDefaultHeroNameOverride>();

    [Tooltip("Подсказка в пустом поле ввода имени.")]
    [SerializeField] private string _namePlaceholder = "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u0438\u043c\u044f \u043f\u0435\u0440\u0441\u043e\u043d\u0430\u0436\u0430";

    [Tooltip("Объекты, которые нужно скрыть только на экране ввода имени. Сюда можно добавить диалоговую плашку или другой UI истории.")]
    [SerializeField] private List<GameObject> _hideWhileNameStepOpen = new List<GameObject>();

    [Tooltip("Если имя уже сохранено, подставлять его вместо стартового имени.")]
    [SerializeField] private bool _useSavedNameAsInitial = true;

    [Tooltip("Максимальная длина имени героини.")]
    [SerializeField] private int _maxNameLength = 20;

    [Tooltip("Сохранять имя на сервер через NetworkManager, если игрок авторизован.")]
    [SerializeField] private bool _syncNameWithServer = true;

    [Tooltip("Показывать экран выбора имени.")]
    [SerializeField] private bool _showNameStep = true;

    [Header("Экран выбора внешности")]
    [Tooltip("Панель, на которой игрок выбирает внешность героини.")]
    [SerializeField] private GameObject _appearancePanel;

    [Tooltip("Варианты внешности. Каждый элемент можно связать с кнопкой и индикатором выбранного состояния.")]
    [SerializeField] private PreStoryAppearanceOptionBinding[] _appearanceOptions = Array.Empty<PreStoryAppearanceOptionBinding>();

    [Tooltip("Кнопка подтверждения выбранной внешности. Если поле пустое, клик по варианту может сразу завершать процесс.")]
    [SerializeField] private Button _appearanceConfirmButton;

    [Tooltip("Индекс варианта внешности, который выбран по умолчанию.")]
    [SerializeField] private int _defaultAppearanceOptionIndex;

    [Tooltip("Сразу завершать выбор внешности после клика по варианту.")]
    [SerializeField] private bool _confirmAppearanceOnSelect;

    [Tooltip("Показывать экран выбора внешности.")]
    [SerializeField] private bool _showAppearanceStep = true;

    [Header("События")]
    [Tooltip("Событие вызывается после выбора имени и внешности, прямо перед запуском истории.")]
    [SerializeField] private UnityEvent _completed;

    [Tooltip("Событие вызывается при отмене предстартового процесса.")]
    [SerializeField] private UnityEvent _cancelled;

    private Action _onComplete;
    private Action _onCancel;
    private int _selectedAppearanceIndex = -1;
    private bool _isVisible;
    private SetupMode _setupMode = SetupMode.Full;
    private bool _saveCompletionOnComplete = true;
    private readonly List<HiddenObjectState> _hiddenNameStepStates = new List<HiddenObjectState>();
    private Image _capturedNamePanelBackgroundImage;
    private Sprite _defaultNamePanelBackgroundSprite;
    private bool _namePanelBackgroundCaptured;
    private StoryUiStyle _activeStoryUiStyle;
    private string _activeStoryUiStoryId;
    private bool _nameScreenDefaultsCaptured;
    private ImageStyleState _defaultNameScreenBackgroundStyle;
    private ImageStyleState _defaultNamePanelBackgroundStyle;
    private ImageStyleState _defaultNameInputFieldStyle;
    private ImageStyleState _defaultNameConfirmButtonStyle;
    private TextStyleState _defaultNameInputTextStyle;
    private TextStyleState _defaultNamePlaceholderTextStyle;
    private TextStyleState _defaultNameConfirmButtonTextStyle;
    private TextStyleState _defaultNameExtraTextOneStyle;
    private TextStyleState _defaultNameExtraTextTwoStyle;
    private RectTransformState _defaultNamePanelBackgroundRect;
    private RectTransformState _defaultNameInputFieldRect;
    private RectTransformState _defaultNameInputTextRect;
    private RectTransformState _defaultNamePlaceholderTextRect;
    private RectTransformState _defaultNameConfirmButtonRect;
    private RectTransformState _defaultNameConfirmButtonTextRect;
    private RectTransformState _defaultNameExtraTextOneRect;
    private RectTransformState _defaultNameExtraTextTwoRect;
    private Button _defaultNameConfirmButton;
    private bool _defaultNameConfirmButtonActiveSelf;
    private GameObject _spawnedNameConfirmButtonPrefab;
    private bool _defaultNameExtraTextOneActiveSelf;
    private bool _defaultNameExtraTextTwoActiveSelf;

    public bool IsVisible => _isVisible;
    public bool ShouldShowBeforeStory => ShouldShowBeforeStoryForStoryId(ResolveActiveStoryId());
    public bool ShouldShowBeforeStoryFor(StoryData story) => ShouldShowBeforeStoryForStoryId(ResolveStoryId(story));
    public string CurrentInputName => _nameInputField != null ? _nameInputField.text : "";
    public int SelectedAppearanceIndex => _selectedAppearanceIndex;
    public Image NameScreenBackgroundImage { get { EnsureReferences(); return _nameScreenBackgroundImage; } }
    public Image NamePanelBackgroundImage { get { EnsureReferences(); return _namePanelBackgroundImage; } }
    public RectTransform NamePanelBackgroundRect => _namePanelBackgroundImage != null ? _namePanelBackgroundImage.rectTransform : null;
    public TMP_InputField NameInputField { get { EnsureReferences(); return _nameInputField; } }
    public Image NameInputFieldImage => NameInputField != null ? ResolveSelectableImage(NameInputField) : null;
    public RectTransform NameInputFieldRect => NameInputField != null ? NameInputField.transform as RectTransform : null;
    public TMP_Text NameInputText => NameInputField != null ? NameInputField.textComponent : null;
    public RectTransform NameInputTextRect => NameInputText != null ? NameInputText.rectTransform : null;
    public TMP_Text NamePlaceholderText { get { EnsureReferences(); return _namePlaceholderText; } }
    public RectTransform NamePlaceholderTextRect => NamePlaceholderText != null ? NamePlaceholderText.rectTransform : null;
    public Button NameConfirmButton { get { EnsureReferences(); return _nameConfirmButton; } }
    public Image NameConfirmButtonImage => NameConfirmButton != null ? ResolveSelectableImage(NameConfirmButton) : null;
    public RectTransform NameConfirmButtonRect
    {
        get
        {
            if (_spawnedNameConfirmButtonPrefab != null &&
                _spawnedNameConfirmButtonPrefab.transform is RectTransform prefabRect)
            {
                return prefabRect;
            }

            return NameConfirmButton != null ? NameConfirmButton.transform as RectTransform : null;
        }
    }
    public TMP_Text NameConfirmButtonText
    {
        get
        {
            TMP_Text label = _spawnedNameConfirmButtonPrefab != null
                ? _spawnedNameConfirmButtonPrefab.GetComponentInChildren<TMP_Text>(true)
                : null;

            return label != null
                ? label
                : (NameConfirmButton != null ? NameConfirmButton.GetComponentInChildren<TMP_Text>(true) : null);
        }
    }
    public RectTransform NameConfirmButtonTextRect => NameConfirmButtonText != null ? NameConfirmButtonText.rectTransform : null;
    public TMP_Text NameExtraTextOne { get { EnsureReferences(); return _nameExtraTextOne; } }
    public RectTransform NameExtraTextOneRect => NameExtraTextOne != null ? NameExtraTextOne.rectTransform : null;
    public TMP_Text NameExtraTextTwo { get { EnsureReferences(); return _nameExtraTextTwo; } }
    public RectTransform NameExtraTextTwoRect => NameExtraTextTwo != null ? NameExtraTextTwo.rectTransform : null;

    public TMP_Text EnsureNameExtraTextOne()
    {
        return EnsureNameExtraText(0);
    }

    public TMP_Text EnsureNameExtraTextTwo()
    {
        return EnsureNameExtraText(1);
    }

    public TMP_Text GetNameExtraText(int index)
    {
        EnsureReferences();
        if (index < 0 || _nameExtraTexts == null || index >= _nameExtraTexts.Count)
            return null;

        return _nameExtraTexts[index];
    }

    public TMP_Text EnsureNameExtraText(int index)
    {
        EnsureReferences();
        return EnsureNameExtraTextAtIndex(index);
    }

    public TMP_Text ResolveNameExtraText(int index, TMP_Text explicitTarget, string pathOrName, bool createIfMissing)
    {
        EnsureReferences();

        TMP_Text target = explicitTarget;
        if (target == null && !string.IsNullOrWhiteSpace(pathOrName))
            target = FindNameExtraTextByPathOrName(pathOrName);

        if (target != null)
        {
            RegisterNameExtraText(index, target);
            return target;
        }

        return createIfMissing ? EnsureNameExtraTextAtIndex(index) : GetNameExtraText(index);
    }

    public void HideNameExtraTextsFrom(int firstIndexToHide)
    {
        EnsureReferences();
        if (_nameExtraTexts == null)
            return;

        int start = Mathf.Max(0, firstIndexToHide);
        for (int i = start; i < _nameExtraTexts.Count; i++)
        {
            TMP_Text text = _nameExtraTexts[i];
            if (text == null)
                continue;

            text.text = "";
            text.gameObject.SetActive(false);
        }
    }

    public Button ApplyNameConfirmButtonPrefabOverride(GameObject prefab)
    {
        EnsureReferences();
        if (_defaultNameConfirmButton == null && _nameConfirmButton != null)
        {
            _defaultNameConfirmButton = _nameConfirmButton;
            _defaultNameConfirmButtonActiveSelf = _nameConfirmButton.gameObject.activeSelf;
        }

        RestoreNameConfirmButtonPrefabOverride();

        if (prefab == null)
            return _nameConfirmButton;

        Transform parent = ResolveNameConfirmButtonPrefabParent();
        if (parent == null)
            return _nameConfirmButton;

        Button previousButton = _nameConfirmButton;
        GameObject instance = Instantiate(prefab, parent, false);
        instance.name = prefab.name;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            instance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
#endif
        instance.SetActive(true);

        Button button = instance.GetComponent<Button>();
        if (button == null)
            button = instance.GetComponentInChildren<Button>(true);

        if (button == null)
        {
            Debug.LogError("[PreStorySetupFlow] Name confirm button prefab must contain a Button component.", instance);
            DestroyUiObject(instance);
            return _nameConfirmButton;
        }

        _spawnedNameConfirmButtonPrefab = instance;

        if (_defaultNameConfirmButton != null)
            _defaultNameConfirmButton.gameObject.SetActive(false);

        if (previousButton != null)
            previousButton.onClick.RemoveListener(ConfirmName);

        _nameConfirmButton = button;
        EnsureNameConfirmButtonLabel(instance, button);
        BindNameConfirmButton();

        return _nameConfirmButton;
    }

    private void EnsureNameConfirmButtonLabel(GameObject root, Button button)
    {
        string label = string.IsNullOrWhiteSpace(_nameConfirmButtonLabel)
            ? "Продолжить"
            : _nameConfirmButtonLabel.Trim();

        TMP_Text text = root != null ? root.GetComponentInChildren<TMP_Text>(true) : null;
        if (text == null && button != null)
            text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
            text = CreateNameConfirmButtonLabel(root, button);

        if (text == null)
            return;

        text.text = label;
        text.gameObject.SetActive(true);
        text.SetAllDirty();
    }

    private TMP_Text CreateNameConfirmButtonLabel(GameObject root, Button button)
    {
        Transform parent = button != null
            ? button.transform
            : (root != null ? root.transform : null);
        if (parent == null)
            return null;

        GameObject labelObject = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        TMP_Text template = _defaultNameConfirmButton != null
            ? _defaultNameConfirmButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        if (template == null)
            template = NameInputText != null ? NameInputText : NamePlaceholderText;

        if (template != null)
        {
            text.font = template.font;
            text.fontSize = template.fontSize;
            text.color = template.color;
            text.alignment = template.alignment;
            text.enableAutoSizing = template.enableAutoSizing;
            text.fontSizeMin = template.fontSizeMin;
            text.fontSizeMax = template.fontSizeMax;
        }
        else
        {
            text.fontSize = 48f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
        }

        text.raycastTarget = false;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        return text;
    }

    private void BindNameConfirmButton()
    {
        if (_nameConfirmButton == null)
            return;

        GameObject buttonRoot = _spawnedNameConfirmButtonPrefab != null
            ? _spawnedNameConfirmButtonPrefab
            : _nameConfirmButton.gameObject;

        EnsureNameConfirmButtonLabel(
            buttonRoot,
            _nameConfirmButton);
        BindConfirmNameToButtons(buttonRoot);
    }

    private void BindConfirmNameToButtons(GameObject root)
    {
        bool boundAny = false;
        if (root != null)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                    continue;

                buttons[i].onClick.RemoveListener(ConfirmName);
                buttons[i].onClick.AddListener(ConfirmName);
                boundAny = true;
            }
        }

        if (boundAny || _nameConfirmButton == null)
            return;

        _nameConfirmButton.onClick.RemoveListener(ConfirmName);
        _nameConfirmButton.onClick.AddListener(ConfirmName);
    }

    private void UnbindConfirmNameFromButtons()
    {
        GameObject buttonRoot = _spawnedNameConfirmButtonPrefab != null
            ? _spawnedNameConfirmButtonPrefab
            : (_nameConfirmButton != null ? _nameConfirmButton.gameObject : null);

        if (buttonRoot != null)
        {
            Button[] buttons = buttonRoot.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                    buttons[i].onClick.RemoveListener(ConfirmName);
            }
        }

        if (_nameConfirmButton != null)
            _nameConfirmButton.onClick.RemoveListener(ConfirmName);
    }

    private bool CanShowIntroStep => _showIntroStep && _introPanel != null;
    private bool CanShowNameStep => _showNameStep && _namePanel != null;
    private bool CanShowAppearanceStep => _showAppearanceStep && _appearancePanel != null;

    private enum SetupMode
    {
        Full,
        NameOnly
    }

    private sealed class HiddenObjectState
    {
        public GameObject Target;
        public bool WasActiveSelf;
        public CanvasGroup CanvasGroup;
        public bool HadCanvasGroup;
        public float Alpha;
        public bool Interactable;
        public bool BlocksRaycasts;
    }

    private struct ImageStyleState
    {
        public Image Target;
        public Sprite Sprite;
        public Color Color;
        public Image.Type Type;
        public bool PreserveAspect;
        public float PixelsPerUnitMultiplier;
        public Material Material;
        public bool RaycastTarget;

        public ImageStyleState(Image target)
        {
            Target = target;
            Sprite = target != null ? target.sprite : null;
            Color = target != null ? target.color : Color.white;
            Type = target != null ? target.type : Image.Type.Simple;
            PreserveAspect = target != null && target.preserveAspect;
            PixelsPerUnitMultiplier = target != null ? target.pixelsPerUnitMultiplier : 1f;
            Material = target != null ? target.material : null;
            RaycastTarget = target != null && target.raycastTarget;
        }

        public void Restore()
        {
            if (Target == null)
                return;

            Target.sprite = Sprite;
            Target.color = Color;
            Target.type = Type;
            Target.preserveAspect = PreserveAspect;
            Target.pixelsPerUnitMultiplier = PixelsPerUnitMultiplier;
            Target.material = Material;
            Target.raycastTarget = RaycastTarget;
            Target.SetAllDirty();
        }
    }

    private struct TextStyleState
    {
        public TMP_Text Target;
        public TMP_FontAsset Font;
        public Color Color;
        public float FontSize;
        public bool EnableAutoSizing;
        public float FontSizeMin;
        public float FontSizeMax;
        public TextAlignmentOptions Alignment;
        public bool EnableWordWrapping;
        public TextOverflowModes OverflowMode;

        public TextStyleState(TMP_Text target)
        {
            Target = target;
            Font = target != null ? target.font : null;
            Color = target != null ? target.color : Color.white;
            FontSize = target != null ? target.fontSize : 36f;
            EnableAutoSizing = target != null && target.enableAutoSizing;
            FontSizeMin = target != null ? target.fontSizeMin : 0f;
            FontSizeMax = target != null ? target.fontSizeMax : 0f;
            Alignment = target != null ? target.alignment : TextAlignmentOptions.Center;
            EnableWordWrapping = target != null && target.enableWordWrapping;
            OverflowMode = target != null ? target.overflowMode : TextOverflowModes.Overflow;
        }

        public void Restore()
        {
            if (Target == null)
                return;

            Target.font = Font;
            Target.color = Color;
            Target.fontSize = FontSize;
            Target.enableAutoSizing = EnableAutoSizing;
            Target.fontSizeMin = FontSizeMin;
            Target.fontSizeMax = FontSizeMax;
            Target.alignment = Alignment;
            Target.enableWordWrapping = EnableWordWrapping;
            Target.overflowMode = OverflowMode;
            Target.SetAllDirty();
        }
    }

    private struct RectTransformState
    {
        public RectTransform Target;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public Vector2 Pivot;

        public RectTransformState(RectTransform target)
        {
            Target = target;
            AnchorMin = target != null ? target.anchorMin : Vector2.zero;
            AnchorMax = target != null ? target.anchorMax : Vector2.one;
            AnchoredPosition = target != null ? target.anchoredPosition : Vector2.zero;
            SizeDelta = target != null ? target.sizeDelta : Vector2.zero;
            Pivot = target != null ? target.pivot : new Vector2(0.5f, 0.5f);
        }

        public void Restore()
        {
            if (Target == null)
                return;

            Target.anchorMin = AnchorMin;
            Target.anchorMax = AnchorMax;
            Target.anchoredPosition = AnchoredPosition;
            Target.sizeDelta = SizeDelta;
            Target.pivot = Pivot;
        }
    }

    private void Reset()
    {
        _rootObject = gameObject;
        _nameInputField = GetComponentInChildren<TMP_InputField>(true);
    }

    private void Awake()
    {
        EnsureReferences();
        CaptureNameScreenDefaults();
        BindButtons();
        HideAllPanels();
    }

    private void OnValidate()
    {
        _maxNameLength = Mathf.Clamp(_maxNameLength, 1, 64);

        if (string.IsNullOrWhiteSpace(_defaultHeroName))
            _defaultHeroName = "\u0413\u0435\u0440\u043e\u0438\u043d\u044f";

        if (string.IsNullOrWhiteSpace(_namePlaceholder))
            _namePlaceholder = "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u0438\u043c\u044f \u043f\u0435\u0440\u0441\u043e\u043d\u0430\u0436\u0430";

        if (string.IsNullOrWhiteSpace(_nameConfirmButtonLabel))
            _nameConfirmButtonLabel = "Продолжить";

        if (string.IsNullOrWhiteSpace(_introMessage))
            _introMessage = DefaultIntroMessage;

        if (_rootObject == null)
            _rootObject = gameObject;

        if (_appearanceOptions == null)
            _appearanceOptions = Array.Empty<PreStoryAppearanceOptionBinding>();

        if (_hideWhileNameStepOpen == null)
            _hideWhileNameStepOpen = new List<GameObject>();

        ValidateNamePanelBackgroundOverrides();
        ValidateDefaultHeroNameOverrides();
    }

    private void OnDestroy()
    {
        UnbindButtons();
        RestoreNameConfirmButtonPrefabOverride();
    }

    public void Show(Action onComplete = null, Action onCancel = null)
    {
        if (!ShouldShowBeforeStory)
        {
            SafeInvoke(onComplete, "complete");
            return;
        }

        EnsureReferences();
        RefreshButtonBindings();

        _onComplete = onComplete;
        _onCancel = onCancel;
        _isVisible = true;
        _setupMode = SetupMode.Full;
        _saveCompletionOnComplete = true;

        if (_rootObject != null)
            _rootObject.SetActive(true);

        PrepareIntroStep();
        PrepareNameStep();
        ApplyNameScreenStyleForCurrentStory();
        SelectAppearance(GetValidAppearanceIndex(_defaultAppearanceOptionIndex), false);

        ShowFirstStep();
    }

    public void ShowNameOnly(
        Action onComplete = null,
        Action onCancel = null,
        bool saveCompletionOnComplete = false,
        string suggestedName = null,
        string defaultNameOverride = null)
    {
        EnsureReferences();
        RefreshButtonBindings();

        _onComplete = onComplete;
        _onCancel = onCancel;
        _isVisible = true;
        _setupMode = SetupMode.NameOnly;
        _saveCompletionOnComplete = saveCompletionOnComplete;

        if (_rootObject != null)
            _rootObject.SetActive(true);

        PrepareNameStep(suggestedName, defaultNameOverride);
        ApplyNameScreenStyleForCurrentStory();

        if (CanShowNameStep)
        {
            ShowOnlyPanel(_namePanel);
            ForceNameInputVisible();
        }
        else
            ConfirmName(defaultNameOverride);
    }

    public void ContinueFromIntro()
    {
        if (CanShowNameStep)
        {
            ShowOnlyPanel(_namePanel);
            ForceNameInputVisible();
            return;
        }

        if (_showNameStep)
            SaveName();

        if (CanShowAppearanceStep)
        {
            ShowOnlyPanel(_appearancePanel);
            return;
        }

        if (_showAppearanceStep)
            SaveAppearance();

        CompleteFlow();
    }

    public void ConfirmName()
    {
        ConfirmName(null);
    }

    private void ConfirmName(string defaultNameOverride)
    {
        SaveName(defaultNameOverride);

        if (_setupMode == SetupMode.NameOnly)
        {
            CompleteFlow(_saveCompletionOnComplete);
            return;
        }

        if (CanShowAppearanceStep)
        {
            ShowOnlyPanel(_appearancePanel);
            return;
        }

        if (_showAppearanceStep)
            SaveAppearance();

        CompleteFlow();
    }

    public void SelectAppearance(int optionIndex)
    {
        SelectAppearance(optionIndex, true);
    }

    public void ConfirmAppearance()
    {
        SaveAppearance();
        CompleteFlow();
    }

    public void Cancel()
    {
        HideAllPanels();
        _isVisible = false;

        UnityEvent cancelledEvent = _cancelled;
        cancelledEvent?.Invoke();

        Action callback = _onCancel;
        ClearCallbacks();
        SafeInvoke(callback, "cancel");
    }

    public void ResetCompletionFlag()
    {
        try
        {
            string storyId = ResolveActiveStoryId();
            LocalSecurePrefs.Delete(GetSetupCompletedKey(storyId));
            HeroCustomizationStore.DeletePlayerNameForStory(storyId);
            if (!string.IsNullOrWhiteSpace(storyId))
                LocalSecurePrefs.Delete(SetupCompletedKey);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PreStorySetupFlow: не удалось сбросить флаг завершения: {exception.Message}", this);
        }
    }

    public void MarkCompleted()
    {
        SaveCompletionFlag();
    }

    public void HideNameStepObjectsImmediately()
    {
        HideNameStepObjects();
    }

    public void RestoreNameStepObjectsImmediately()
    {
        RestoreNameStepHiddenObjects();
    }

    public void ApplyStoryUiStyle(StoryUiStyle style, string storyId = null)
    {
        EnsureReferences();
        CaptureNameScreenDefaults();
        _activeStoryUiStyle = style;
        _activeStoryUiStoryId = NormalizeStoryId(storyId);
        ApplyNameScreenStyleForCurrentStory();
    }

    public void PreviewNameInterface(StoryUiStyle style, string storyId, string previewName)
    {
        EnsureReferences();
        RefreshButtonBindings();
        CaptureNameScreenDefaults();

        _onComplete = null;
        _onCancel = null;
        _isVisible = true;
        _setupMode = SetupMode.NameOnly;
        _saveCompletionOnComplete = false;
        _activeStoryUiStyle = style;
        _activeStoryUiStoryId = NormalizeStoryId(storyId);

        if (_rootObject != null)
            _rootObject.SetActive(true);

        PrepareNameStep(previewName, previewName);
        ApplyNameScreenStyleForCurrentStory();

        if (CanShowNameStep)
        {
            ShowOnlyPanel(_namePanel);
            ForceNameInputVisible();
        }
    }

    public void HidePreview()
    {
        HideAllPanels();
        _isVisible = false;
        ClearCallbacks();
    }

    private void SelectAppearance(int optionIndex, bool allowAutoConfirm)
    {
        if (_appearanceOptions == null || _appearanceOptions.Length == 0)
        {
            _selectedAppearanceIndex = -1;
            return;
        }

        _selectedAppearanceIndex = GetValidAppearanceIndex(optionIndex);
        RefreshAppearanceOptions();

        if (allowAutoConfirm && _confirmAppearanceOnSelect)
            ConfirmAppearance();
    }

    private void ShowFirstStep()
    {
        if (CanShowIntroStep)
        {
            ShowOnlyPanel(_introPanel);
            return;
        }

        ContinueFromIntro();
    }

    private void PrepareIntroStep()
    {
        if (_introMessageText != null)
            _introMessageText.text = _introMessage;
    }

    private void PrepareNameStep(string suggestedName = null, string defaultNameOverride = null)
    {
        string initialName = ResolveInitialName(suggestedName, defaultNameOverride);

        if (_namePlaceholderText != null)
            _namePlaceholderText.text = _namePlaceholder;

        if (_nameInputField == null)
            return;

        _nameInputField.characterLimit = _maxNameLength;

        try
        {
            _nameInputField.SetTextWithoutNotify(initialName);
        }
        catch (NullReferenceException exception)
        {
            Debug.LogWarning($"PreStorySetupFlow: name input is not fully configured yet: {exception.Message}", this);
        }
    }

    private string ResolveInitialName(string suggestedName = null, string defaultNameOverride = null)
    {
        string storyId = ResolveActiveStoryId();
        string fallbackName = ResolveDefaultHeroName(storyId, defaultNameOverride);

        if (_useSavedNameAsInitial && SafeTryLoadStoryPlayerName(storyId, out string storyName))
            return NormalizeName(storyName, fallbackName);

        if (!string.IsNullOrWhiteSpace(suggestedName))
            return NormalizeName(suggestedName, fallbackName);

        return NormalizeName(fallbackName, fallbackName);
    }

    private void SaveName(string defaultNameOverride = null)
    {
        string storyId = ResolveActiveStoryId();
        string fallbackName = ResolveDefaultHeroName(storyId, defaultNameOverride);
        string heroName = NormalizeName(_nameInputField != null ? _nameInputField.text : fallbackName, fallbackName);

        try
        {
            heroName = CharacterProfileService.SaveSelectedPlayerName(
                heroName,
                storyId,
                nameof(PreStorySetupFlow));

            if (_syncNameWithServer && NetworkManager.Instance != null)
                NetworkManager.Instance.SetHeroNameAsync(heroName, storyId: storyId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PreStorySetupFlow: не удалось сохранить имя героини: {exception.Message}", this);
            CharacterProfileService.SaveSelectedPlayerName(heroName, storyId, nameof(PreStorySetupFlow) + ".fallback");
        }
    }

    private void SaveAppearance()
    {
        if (_appearanceOptions == null || _appearanceOptions.Length == 0)
            return;

        int optionIndex = GetValidAppearanceIndex(_selectedAppearanceIndex);
        PreStoryAppearanceOptionBinding option = _appearanceOptions[optionIndex];

        if (option != null)
        {
            PlayerAppearance.SetAppearance(option.AppearanceType);
            HeroCustomizationStore.SaveAppearanceForStory(ResolveActiveStoryId(), option.AppearanceType);
        }
    }

    private void CompleteFlow()
    {
        CompleteFlow(_saveCompletionOnComplete);
    }

    private void CompleteFlow(bool saveCompletion)
    {
        if (saveCompletion)
            SaveCompletionFlag();

        HideAllPanels();
        _isVisible = false;

        UnityEvent completedEvent = _completed;
        completedEvent?.Invoke();

        Action callback = _onComplete;
        ClearCallbacks();
        SafeInvoke(callback, "complete");
    }

    private void SaveCompletionFlag()
    {
        try
        {
            string storyId = ResolveActiveStoryId();
            LocalSecurePrefs.SetBool(GetSetupCompletedKey(storyId), GetSetupCompletionPurpose(storyId), true);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PreStorySetupFlow: не удалось сохранить флаг завершения: {exception.Message}", this);
        }
    }

    private void ShowOnlyPanel(GameObject targetPanel)
    {
        GameObject targetRoot = GetStepRoot(targetPanel);

        SetPanelActive(GetIntroStepRoot(), targetRoot == GetIntroStepRoot());
        SetPanelActive(GetNameStepRoot(), targetRoot == GetNameStepRoot());
        SetPanelActive(GetAppearanceStepRoot(), targetRoot == GetAppearanceStepRoot());
        SetNameStepHiddenObjectsVisible(targetRoot != GetNameStepRoot());

        if (_rootObject != null)
            _rootObject.SetActive(true);

        BringPanelToFront(targetRoot);
    }

    private void HideAllPanels()
    {
        SetPanelActive(GetIntroStepRoot(), false);
        SetPanelActive(GetNameStepRoot(), false);
        SetPanelActive(GetAppearanceStepRoot(), false);
        SetNameStepHiddenObjectsVisible(true);

        if (_rootObject != null)
            _rootObject.SetActive(false);
    }

    private void RefreshAppearanceOptions()
    {
        if (_appearanceOptions == null)
            return;

        for (int i = 0; i < _appearanceOptions.Length; i++)
        {
            if (_appearanceOptions[i] != null)
                _appearanceOptions[i].RefreshView(i == _selectedAppearanceIndex);
        }
    }

    private int GetValidAppearanceIndex(int optionIndex)
    {
        if (_appearanceOptions == null || _appearanceOptions.Length == 0)
            return -1;

        return Mathf.Clamp(optionIndex, 0, _appearanceOptions.Length - 1);
    }

    private string NormalizeName(string rawName, string defaultNameOverride = null)
    {
        string fallbackName = !string.IsNullOrWhiteSpace(defaultNameOverride)
            ? defaultNameOverride.Trim()
            : _defaultHeroName;

        string normalizedName = string.IsNullOrWhiteSpace(rawName)
            ? fallbackName
            : rawName.Trim();

        int maxLength = Mathf.Clamp(_maxNameLength, 1, 64);
        if (normalizedName.Length > maxLength)
            normalizedName = normalizedName.Substring(0, maxLength);

        return normalizedName;
    }

    private string ResolveDefaultHeroName(string storyId, string defaultNameOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(defaultNameOverride))
            return defaultNameOverride.Trim();

        if (TryGetDefaultHeroNameOverride(storyId, out string storyDefaultName))
            return storyDefaultName.Trim();

        return string.IsNullOrWhiteSpace(_defaultHeroName)
            ? HeroCustomizationStore.DefaultPlayerName
            : _defaultHeroName.Trim();
    }

    private void EnsureReferences()
    {
        if (_rootObject == null)
            _rootObject = gameObject;

        if (_namePanel == null)
            _namePanel = FindNamePanelObject();

        if (_nameInputField == null && _namePanel != null)
            _nameInputField = _namePanel.GetComponentInChildren<TMP_InputField>(true);

        if (_nameInputField == null)
            _nameInputField = GetComponentInChildren<TMP_InputField>(true);

        if (_namePanel == null && _nameInputField != null)
            _namePanel = ResolveNamePanelFromInput();

        if (_namePlaceholderText == null && _nameInputField != null)
            _namePlaceholderText = _nameInputField.placeholder as TMP_Text;

        if (_nameConfirmButton == null)
            _nameConfirmButton = FindNameConfirmButton();

        SyncLegacyNameExtraTextReferences();

        if (_namePanelBackgroundImage == null && _autoFindNamePanelBackgroundImage)
            _namePanelBackgroundImage = FindNamePanelBackgroundImage();

        if (_nameScreenBackgroundImage == null && _autoFindNameScreenBackgroundImage)
            _nameScreenBackgroundImage = FindNameScreenBackgroundImage();
    }

    private void SyncLegacyNameExtraTextReferences()
    {
        if (_nameExtraTexts == null)
            _nameExtraTexts = new List<TMP_Text>();

        EnsureNameExtraTextSlot(0);
        EnsureNameExtraTextSlot(1);

        if (_nameExtraTexts[0] == null && _nameExtraTextOne != null)
            _nameExtraTexts[0] = _nameExtraTextOne;
        if (_nameExtraTexts[1] == null && _nameExtraTextTwo != null)
            _nameExtraTexts[1] = _nameExtraTextTwo;

        if (_nameExtraTextOne == null)
            _nameExtraTextOne = _nameExtraTexts[0];
        if (_nameExtraTextTwo == null)
            _nameExtraTextTwo = _nameExtraTexts[1];
    }

    private void EnsureNameExtraTextSlot(int index)
    {
        if (_nameExtraTexts == null)
            _nameExtraTexts = new List<TMP_Text>();

        while (_nameExtraTexts.Count <= index)
            _nameExtraTexts.Add(null);
    }

    private void RegisterNameExtraText(int index, TMP_Text target)
    {
        if (index < 0 || target == null)
            return;

        EnsureNameExtraTextSlot(index);
        _nameExtraTexts[index] = target;

        if (index == 0)
            _nameExtraTextOne = target;
        else if (index == 1)
            _nameExtraTextTwo = target;
    }

    private TMP_Text EnsureNameExtraTextAtIndex(int index)
    {
        if (index < 0)
            return null;

        EnsureNameExtraTextSlot(index);

        TMP_Text target = _nameExtraTexts[index];
        if (target != null)
            return target;

        string objectName = $"NameExtraText{index + 1}";
        target = FindNameExtraText(objectName);
        if (target == null)
            target = CreateNameExtraText(objectName, GetDefaultNameExtraTextPosition(index));

        RegisterNameExtraText(index, target);

        return target;
    }

    private TMP_Text CreateNameExtraText(string objectName, Vector2 defaultAnchoredPosition)
    {
        Transform parent = _namePanel != null
            ? _namePanel.transform
            : (_rootObject != null ? _rootObject.transform : transform);

        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RegisterCreatedObjectUndo(textObject, "Create Name Extra Text");
#endif
        textObject.transform.SetParent(parent, false);
        textObject.SetActive(false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        TMP_Text template = NamePlaceholderText != null ? NamePlaceholderText : (NameInputText != null ? NameInputText : NameConfirmButtonText);
        if (template != null)
        {
            text.font = template.font;
            text.fontSize = template.fontSize;
            text.color = template.color;
            text.alignment = template.alignment;
            text.enableAutoSizing = template.enableAutoSizing;
            text.fontSizeMin = template.fontSizeMin;
            text.fontSizeMax = template.fontSizeMax;
        }
        else
        {
            text.fontSize = 48f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
        }

        text.text = "";
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = defaultAnchoredPosition;
        rect.sizeDelta = new Vector2(900f, 90f);

        CaptureNameExtraTextDefault(text, objectName);
        return text;
    }

    private static Vector2 GetDefaultNameExtraTextPosition(int index)
    {
        if (index == 0)
            return new Vector2(0f, 210f);
        if (index == 1)
            return new Vector2(0f, -210f);

        return new Vector2(0f, 210f - 110f * index);
    }

    private TMP_Text FindNameExtraText(string objectName)
    {
        if (_namePanel == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        TMP_Text[] texts = _namePanel.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text != null && string.Equals(text.name, objectName, StringComparison.OrdinalIgnoreCase))
                return text;
        }

        return null;
    }

    private TMP_Text FindNameExtraTextByPathOrName(string pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
            return null;

        string normalized = pathOrName.Trim().Replace('\\', '/');
        TMP_Text result = FindNameExtraTextInRoot(_namePanel != null ? _namePanel.transform : null, normalized);
        if (result != null)
            return result;

        return FindNameExtraTextInRoot(_rootObject != null ? _rootObject.transform : transform, normalized);
    }

    private static TMP_Text FindNameExtraTextInRoot(Transform root, string pathOrName)
    {
        if (root == null || string.IsNullOrWhiteSpace(pathOrName))
            return null;

        Transform direct = root.Find(pathOrName);
        if (direct != null && direct.TryGetComponent(out TMP_Text directText))
            return directText;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            if (string.Equals(text.name, pathOrName, StringComparison.OrdinalIgnoreCase))
                return text;

            string relativePath = BuildRelativePath(root, text.transform);
            if (string.Equals(relativePath, pathOrName, StringComparison.OrdinalIgnoreCase))
                return text;
        }

        return null;
    }

    private static string BuildRelativePath(Transform root, Transform target)
    {
        if (root == null || target == null)
            return "";

        if (root == target)
            return target.name;

        List<string> parts = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        if (current != root)
            return target.name;

        parts.Reverse();
        return string.Join("/", parts);
    }

    private void CaptureNameExtraTextDefault(TMP_Text text, string objectName)
    {
        if (!_nameScreenDefaultsCaptured || text == null)
            return;

        if (string.Equals(objectName, "NameExtraText1", StringComparison.OrdinalIgnoreCase))
        {
            _defaultNameExtraTextOneStyle = new TextStyleState(text);
            _defaultNameExtraTextOneRect = new RectTransformState(text.rectTransform);
            _defaultNameExtraTextOneActiveSelf = false;
        }
        else if (string.Equals(objectName, "NameExtraText2", StringComparison.OrdinalIgnoreCase))
        {
            _defaultNameExtraTextTwoStyle = new TextStyleState(text);
            _defaultNameExtraTextTwoRect = new RectTransformState(text.rectTransform);
            _defaultNameExtraTextTwoActiveSelf = false;
        }
    }

    private void RefreshButtonBindings()
    {
        UnbindButtons();
        BindButtons();
    }

    private void ForceNameInputVisible()
    {
        if (_namePanel == null)
            return;

        GameObject nameRoot = GetNameStepRoot();
        SetPanelActive(nameRoot, true);
        SetPanelActive(_namePanel, true);
        BringPanelToFront(nameRoot != null ? nameRoot : _namePanel);

        if (_nameInputField != null && gameObject.activeInHierarchy)
            StartCoroutine(FocusNameInputNextFrame());
    }

    private IEnumerator FocusNameInputNextFrame()
    {
        yield return null;

        if (!_isVisible || _nameInputField == null || !_nameInputField.gameObject.activeInHierarchy)
            yield break;

        try
        {
            _nameInputField.interactable = true;
            _nameInputField.ForceLabelUpdate();
            _nameInputField.Select();
            _nameInputField.ActivateInputField();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PreStorySetupFlow: failed to focus name input: {exception.Message}", this);
        }
    }

    private GameObject FindNamePanelObject()
    {
        Transform sceneRoot = transform.root;
        Transform fromSceneRoot = FindChildByName(sceneRoot, FallbackNamePanelObjectName);
        if (fromSceneRoot != null)
            return fromSceneRoot.gameObject;

        Transform fromRootObject = _rootObject != null
            ? FindChildByName(_rootObject.transform, FallbackNamePanelObjectName)
            : null;
        if (fromRootObject != null)
            return fromRootObject.gameObject;

        Scene currentScene = gameObject.scene;
        if (!currentScene.IsValid())
            return null;

        GameObject[] roots = currentScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
                continue;

            Transform candidate = FindChildByName(root.transform, FallbackNamePanelObjectName);
            if (candidate != null)
                return candidate.gameObject;
        }

        return null;
    }

    private GameObject ResolveNamePanelFromInput()
    {
        if (_nameInputField == null)
            return null;

        Transform current = _nameInputField.transform;
        while (current != null)
        {
            if (string.Equals(current.name, FallbackNamePanelObjectName, StringComparison.OrdinalIgnoreCase))
                return current.gameObject;

            current = current.parent;
        }

        return _nameInputField.gameObject;
    }

    private Image FindNamePanelBackgroundImage()
    {
        if (_namePanel == null)
            return null;

        Image directImage = _namePanel.GetComponent<Image>();
        if (directImage != null)
            return directImage;

        Image[] images = _namePanel.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            if (IsNamePanelBackgroundName(image.name))
                return image;
        }

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && image.GetComponentInParent<TMP_InputField>() == null)
                return image;
        }

        return images.Length > 0 ? images[0] : null;
    }

    private static bool IsNamePanelBackgroundName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        return objectName.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("backgrund", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("hero_name", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private Image FindNameScreenBackgroundImage()
    {
        Transform[] roots =
        {
            _rootObject != null ? _rootObject.transform : null,
            _rootObject != null && _rootObject.transform.parent != null ? _rootObject.transform.parent : null,
            transform.parent,
            transform.root
        };

        Image best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < roots.Length; i++)
        {
            Transform root = roots[i];
            if (root == null)
                continue;

            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int j = 0; j < images.Length; j++)
            {
                Image image = images[j];
                int score = ScoreNameScreenBackgroundCandidate(image, root);
                if (score > bestScore)
                {
                    best = image;
                    bestScore = score;
                }
            }
        }

        return bestScore > 0 ? best : null;
    }

    private int ScoreNameScreenBackgroundCandidate(Image image, Transform searchRoot)
    {
        if (image == null ||
            image == _namePanelBackgroundImage ||
            IsInside(image.transform, _namePanel) ||
            IsInside(image.transform, _introPanel) ||
            IsInside(image.transform, _appearancePanel) ||
            image.GetComponentInParent<Button>(true) != null ||
            image.GetComponentInParent<TMP_InputField>(true) != null)
        {
            return int.MinValue;
        }

        string name = image.name ?? "";
        int score = 0;
        if (string.Equals(name, "Background", StringComparison.OrdinalIgnoreCase))
            score += 100;
        else if (name.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("backgrund", StringComparison.OrdinalIgnoreCase) >= 0)
            score += 60;

        if (image.transform.parent == searchRoot)
            score += 30;
        if (_rootObject != null && _rootObject.transform.parent != null && image.transform.parent == _rootObject.transform.parent)
            score += 20;
        if (image.GetComponent<DialogueTapHandler>() != null)
            score += 20;
        if (image.rectTransform.anchorMin == Vector2.zero && image.rectTransform.anchorMax == Vector2.one)
            score += 10;
        if (name.IndexOf("dialogue", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("choice", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("stat", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("chapter", StringComparison.OrdinalIgnoreCase) >= 0)
            score -= 100;

        return score;
    }

    private static bool IsInside(Transform candidate, GameObject root)
    {
        return candidate != null && root != null && candidate.IsChildOf(root.transform);
    }

    private void ApplyNameScreenStyleForCurrentStory()
    {
        EnsureReferences();
        CaptureNameScreenDefaults();
        RestoreNameScreenDefaults();

        if (_activeStoryUiStyle != null)
        {
            _activeStoryUiStyle.ApplyToPreStorySetupFlow(this);
            return;
        }

        ApplyNamePanelBackgroundForCurrentStory();
    }

    private void CaptureNameScreenDefaults()
    {
        if (_nameScreenDefaultsCaptured)
            return;

        _defaultNameScreenBackgroundStyle = new ImageStyleState(_nameScreenBackgroundImage);
        _defaultNamePanelBackgroundStyle = new ImageStyleState(_namePanelBackgroundImage);
        _defaultNameInputFieldStyle = new ImageStyleState(NameInputFieldImage);
        _defaultNameConfirmButtonStyle = new ImageStyleState(NameConfirmButtonImage);
        _defaultNameInputTextStyle = new TextStyleState(NameInputText);
        _defaultNamePlaceholderTextStyle = new TextStyleState(_namePlaceholderText);
        _defaultNameConfirmButtonTextStyle = new TextStyleState(NameConfirmButtonText);
        _defaultNameExtraTextOneStyle = new TextStyleState(_nameExtraTextOne);
        _defaultNameExtraTextTwoStyle = new TextStyleState(_nameExtraTextTwo);
        _defaultNamePanelBackgroundRect = new RectTransformState(NamePanelBackgroundRect);
        _defaultNameInputFieldRect = new RectTransformState(NameInputFieldRect);
        _defaultNameInputTextRect = new RectTransformState(NameInputTextRect);
        _defaultNamePlaceholderTextRect = new RectTransformState(NamePlaceholderTextRect);
        _defaultNameConfirmButtonRect = new RectTransformState(NameConfirmButtonRect);
        _defaultNameConfirmButtonTextRect = new RectTransformState(NameConfirmButtonTextRect);
        _defaultNameExtraTextOneRect = new RectTransformState(_nameExtraTextOne != null ? _nameExtraTextOne.rectTransform : null);
        _defaultNameExtraTextTwoRect = new RectTransformState(_nameExtraTextTwo != null ? _nameExtraTextTwo.rectTransform : null);
        _defaultNameConfirmButton = _nameConfirmButton;
        _defaultNameConfirmButtonActiveSelf = _nameConfirmButton != null && _nameConfirmButton.gameObject.activeSelf;
        _defaultNameExtraTextOneActiveSelf = _nameExtraTextOne != null && _nameExtraTextOne.gameObject.activeSelf;
        _defaultNameExtraTextTwoActiveSelf = _nameExtraTextTwo != null && _nameExtraTextTwo.gameObject.activeSelf;
        _nameScreenDefaultsCaptured = true;
    }

    private void RestoreNameScreenDefaults()
    {
        if (!_nameScreenDefaultsCaptured)
            return;

        RestoreNameConfirmButtonPrefabOverride();
        _defaultNameScreenBackgroundStyle.Restore();
        _defaultNamePanelBackgroundStyle.Restore();
        _defaultNameInputFieldStyle.Restore();
        _defaultNameConfirmButtonStyle.Restore();
        _defaultNameInputTextStyle.Restore();
        _defaultNamePlaceholderTextStyle.Restore();
        _defaultNameConfirmButtonTextStyle.Restore();
        _defaultNameExtraTextOneStyle.Restore();
        _defaultNameExtraTextTwoStyle.Restore();
        _defaultNamePanelBackgroundRect.Restore();
        _defaultNameInputFieldRect.Restore();
        _defaultNameInputTextRect.Restore();
        _defaultNamePlaceholderTextRect.Restore();
        _defaultNameConfirmButtonRect.Restore();
        _defaultNameConfirmButtonTextRect.Restore();
        _defaultNameExtraTextOneRect.Restore();
        _defaultNameExtraTextTwoRect.Restore();
        RestoreNameExtraTextActiveState(_nameExtraTextOne, _defaultNameExtraTextOneActiveSelf);
        RestoreNameExtraTextActiveState(_nameExtraTextTwo, _defaultNameExtraTextTwoActiveSelf);
    }

    private Transform ResolveNameConfirmButtonPrefabParent()
    {
        if (_nameConfirmButtonPrefabParent != null)
            return _nameConfirmButtonPrefabParent;

        if (_defaultNameConfirmButton != null && _defaultNameConfirmButton.transform.parent != null)
            return _defaultNameConfirmButton.transform.parent;

        if (_nameConfirmButton != null && _nameConfirmButton.transform.parent != null)
            return _nameConfirmButton.transform.parent;

        if (_namePanel != null)
            return _namePanel.transform;

        if (_rootObject != null)
            return _rootObject.transform;

        return transform;
    }

    private void RestoreNameConfirmButtonPrefabOverride()
    {
        if (_spawnedNameConfirmButtonPrefab != null)
        {
            UnbindConfirmNameFromButtons();

            DestroyUiObject(_spawnedNameConfirmButtonPrefab);
            _spawnedNameConfirmButtonPrefab = null;
        }

        if (_defaultNameConfirmButton == null)
            return;

        _nameConfirmButton = _defaultNameConfirmButton;
        _nameConfirmButton.gameObject.SetActive(_defaultNameConfirmButtonActiveSelf);
    }

    private static void DestroyUiObject(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private static void RestoreNameExtraTextActiveState(TMP_Text text, bool activeSelf)
    {
        if (text != null)
            text.gameObject.SetActive(activeSelf);
    }

    private void ApplyNamePanelBackgroundForCurrentStory()
    {
        Image image = _namePanelBackgroundImage;
        if (image == null && _autoFindNamePanelBackgroundImage)
        {
            image = FindNamePanelBackgroundImage();
            _namePanelBackgroundImage = image;
        }

        if (image == null)
            return;

        CaptureDefaultNamePanelBackground(image);

        string storyId = ResolveActiveStoryId();
        if (string.IsNullOrWhiteSpace(storyId))
            storyId = _activeStoryUiStoryId;

        if (TryGetNamePanelBackgroundOverride(storyId, out Sprite sprite) && sprite != null)
        {
            image.sprite = sprite;
            image.SetAllDirty();
            return;
        }

        RestoreDefaultNamePanelBackground(image);
    }

    private static Image ResolveSelectableImage(Selectable selectable)
    {
        if (selectable == null)
            return null;

        Image image = selectable.targetGraphic as Image;
        if (image != null)
            return image;

        return selectable.GetComponent<Image>();
    }

    private void CaptureDefaultNamePanelBackground(Image image)
    {
        if (image == null)
            return;

        if (_namePanelBackgroundCaptured && _capturedNamePanelBackgroundImage == image)
            return;

        _capturedNamePanelBackgroundImage = image;
        _defaultNamePanelBackgroundSprite = image.sprite;
        _namePanelBackgroundCaptured = true;
    }

    private void RestoreDefaultNamePanelBackground(Image image)
    {
        if (!_namePanelBackgroundCaptured || _capturedNamePanelBackgroundImage != image)
            return;

        image.sprite = _defaultNamePanelBackgroundSprite;
        image.SetAllDirty();
    }

    private bool TryGetNamePanelBackgroundOverride(string storyId, out Sprite sprite)
    {
        sprite = null;

        if (string.IsNullOrWhiteSpace(storyId) || _namePanelBackgroundOverrides == null)
            return false;

        for (int i = 0; i < _namePanelBackgroundOverrides.Count; i++)
        {
            PreStoryNamePanelBackgroundOverride entry = _namePanelBackgroundOverrides[i];
            if (entry == null || !entry.Matches(storyId))
                continue;

            sprite = entry.BackgroundSprite;
            return sprite != null;
        }

        return false;
    }

    private bool TryGetDefaultHeroNameOverride(string storyId, out string defaultHeroName)
    {
        defaultHeroName = "";

        if (string.IsNullOrWhiteSpace(storyId) || _defaultHeroNameOverrides == null)
            return false;

        for (int i = 0; i < _defaultHeroNameOverrides.Count; i++)
        {
            PreStoryDefaultHeroNameOverride entry = _defaultHeroNameOverrides[i];
            if (entry == null || !entry.Matches(storyId))
                continue;

            defaultHeroName = entry.DefaultHeroName;
            return !string.IsNullOrWhiteSpace(defaultHeroName);
        }

        return false;
    }

    private void ValidateNamePanelBackgroundOverrides()
    {
        if (_namePanelBackgroundOverrides == null)
        {
            _namePanelBackgroundOverrides = new List<PreStoryNamePanelBackgroundOverride>();
            return;
        }

        for (int i = 0; i < _namePanelBackgroundOverrides.Count; i++)
            _namePanelBackgroundOverrides[i]?.Validate();
    }

    private void ValidateDefaultHeroNameOverrides()
    {
        if (_defaultHeroNameOverrides == null)
        {
            _defaultHeroNameOverrides = new List<PreStoryDefaultHeroNameOverride>();
            return;
        }

        for (int i = 0; i < _defaultHeroNameOverrides.Count; i++)
            _defaultHeroNameOverrides[i]?.Validate();
    }

    private Button FindNameConfirmButton()
    {
        Button button = FindBestButton(_namePanel != null ? _namePanel.transform : null);
        if (button != null)
            return button;

        Transform parent = _namePanel != null ? _namePanel.transform.parent : null;
        return FindBestButton(parent);
    }

    private static Button FindBestButton(Transform root)
    {
        if (root == null)
            return null;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        Button firstButton = null;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            firstButton ??= button;
            string name = button.name.ToLowerInvariant();
            if (name.Contains("continue") ||
                name.Contains("confirm") ||
                name.Contains("submit") ||
                name.Contains("next") ||
                name.Contains("ok") ||
                name.Contains("name"))
            {
                return button;
            }
        }

        return firstButton;
    }

    private static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null)
            return null;

        if (string.Equals(root.name, objectName, StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void SetNameStepHiddenObjectsVisible(bool visible)
    {
        if (visible)
        {
            RestoreNameStepHiddenObjects();
            return;
        }

        HideNameStepObjects();
    }

    private void HideNameStepObjects()
    {
        if (_hideWhileNameStepOpen == null || _hideWhileNameStepOpen.Count == 0 || _hiddenNameStepStates.Count > 0)
            return;

        foreach (GameObject target in _hideWhileNameStepOpen)
        {
            if (target == null)
                continue;

            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            bool hadCanvasGroup = canvasGroup != null;
            if (canvasGroup == null)
                canvasGroup = target.AddComponent<CanvasGroup>();

            _hiddenNameStepStates.Add(new HiddenObjectState
            {
                Target = target,
                WasActiveSelf = target.activeSelf,
                CanvasGroup = canvasGroup,
                HadCanvasGroup = hadCanvasGroup,
                Alpha = canvasGroup.alpha,
                Interactable = canvasGroup.interactable,
                BlocksRaycasts = canvasGroup.blocksRaycasts
            });

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (target.activeSelf)
                target.SetActive(false);
        }
    }

    private void RestoreNameStepHiddenObjects()
    {
        if (_hiddenNameStepStates.Count == 0)
            return;

        for (int i = 0; i < _hiddenNameStepStates.Count; i++)
        {
            HiddenObjectState state = _hiddenNameStepStates[i];
            if (state == null || state.Target == null)
                continue;

            if (state.CanvasGroup != null)
            {
                state.CanvasGroup.alpha = state.Alpha;
                state.CanvasGroup.interactable = state.Interactable;
                state.CanvasGroup.blocksRaycasts = state.BlocksRaycasts;

                if (!state.HadCanvasGroup)
                    Destroy(state.CanvasGroup);
            }

            if (state.Target.activeSelf != state.WasActiveSelf)
                state.Target.SetActive(state.WasActiveSelf);
        }

        _hiddenNameStepStates.Clear();
    }

    private void BindButtons()
    {
        if (_introContinueButton != null)
            _introContinueButton.onClick.AddListener(ContinueFromIntro);

        BindNameConfirmButton();

        if (_appearanceConfirmButton != null)
            _appearanceConfirmButton.onClick.AddListener(ConfirmAppearance);

        if (_appearanceOptions == null)
            return;

        for (int i = 0; i < _appearanceOptions.Length; i++)
        {
            if (_appearanceOptions[i] != null)
                _appearanceOptions[i].Bind(i, SelectAppearance);
        }
    }

    private void UnbindButtons()
    {
        if (_introContinueButton != null)
            _introContinueButton.onClick.RemoveListener(ContinueFromIntro);

        UnbindConfirmNameFromButtons();

        if (_appearanceConfirmButton != null)
            _appearanceConfirmButton.onClick.RemoveListener(ConfirmAppearance);

        if (_appearanceOptions == null)
            return;

        for (int i = 0; i < _appearanceOptions.Length; i++)
        {
            if (_appearanceOptions[i] != null)
                _appearanceOptions[i].Unbind();
        }
    }

    private void ClearCallbacks()
    {
        _onComplete = null;
        _onCancel = null;
    }

    private GameObject GetStepRoot(GameObject panel)
    {
        if (panel == _introPanel)
            return GetIntroStepRoot();

        if (panel == _namePanel)
            return GetNameStepRoot();

        if (panel == _appearancePanel)
            return GetAppearanceStepRoot();

        return panel;
    }

    private GameObject GetIntroStepRoot()
    {
        return ResolveStepRoot(_introPanel, _introContinueButton);
    }

    private GameObject GetNameStepRoot()
    {
        return ResolveStepRoot(_namePanel, _nameConfirmButton);
    }

    private GameObject GetAppearanceStepRoot()
    {
        return ResolveStepRoot(_appearancePanel, _appearanceConfirmButton);
    }

    private static GameObject ResolveStepRoot(GameObject panel, Button stepButton)
    {
        if (panel == null)
            return null;

        Transform buttonParent = stepButton != null ? stepButton.transform.parent : null;
        if (buttonParent != null && buttonParent == panel.transform.parent)
            return buttonParent.gameObject;

        return panel;
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel == null)
            return;

        if (active)
            ActivateParents(panel);

        if (panel.activeSelf != active)
            panel.SetActive(active);
    }

    private static void ActivateParents(GameObject target)
    {
        if (target == null)
            return;

        Transform parent = target.transform.parent;
        while (parent != null)
        {
            if (!parent.gameObject.activeSelf)
                parent.gameObject.SetActive(true);

            parent = parent.parent;
        }
    }

    private static void BringPanelToFront(GameObject panel)
    {
        if (panel == null)
            return;

        Transform panelTransform = panel.transform;
        panelTransform.SetAsLastSibling();

        Transform parent = panelTransform.parent;
        if (parent != null)
            parent.SetAsLastSibling();
    }

    private static bool SafeHasStoryPlayerName(string storyId)
    {
        try
        {
            return HeroCustomizationStore.HasStoredPlayerNameForStory(storyId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("PreStorySetupFlow: не удалось прочитать имя для истории: " + exception.Message);
            return false;
        }
    }

    private static bool SafeTryLoadStoryPlayerName(string storyId, out string playerName)
    {
        playerName = "";

        try
        {
            return HeroCustomizationStore.TryLoadPlayerNameForStory(storyId, out playerName);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("PreStorySetupFlow: не удалось загрузить имя для истории: " + exception.Message);
            playerName = "";
            return false;
        }
    }

    private bool ShouldShowBeforeStoryForStoryId(string storyId)
    {
        storyId = NormalizeStoryId(storyId);

        if (_showNameStep && !SafeHasStoryPlayerName(storyId))
            return true;

        return !_skipAfterFirstCompletion || !IsSetupCompleted(storyId);
    }

    private static bool IsSetupCompleted(string storyId)
    {
        return LocalSecurePrefs.GetBool(GetSetupCompletedKey(storyId), GetSetupCompletionPurpose(storyId), false);
    }

    private static string GetSetupCompletedKey(string storyId)
    {
        storyId = NormalizeStoryId(storyId);
        return string.IsNullOrEmpty(storyId)
            ? SetupCompletedKey
            : SetupCompletedKey + ":" + storyId;
    }

    private static string GetSetupCompletionPurpose(string storyId)
    {
        storyId = NormalizeStoryId(storyId);
        return string.IsNullOrEmpty(storyId)
            ? LocalSaveSecurity.SetupFlagPurpose + ":pre_story"
            : LocalSaveSecurity.SetupFlagPurpose + ":pre_story:" + storyId;
    }

    private static string ResolveStoryId(StoryData story)
    {
        return story != null ? NormalizeStoryId(story.StoryId) : "";
    }

    private static string ResolveActiveStoryId()
    {
        StoryManager storyManager = StoryManager.Instance;
        if (storyManager == null && !Application.isPlaying)
            storyManager = UnityEngine.Object.FindObjectOfType<StoryManager>(true);

        if (storyManager != null && !string.IsNullOrWhiteSpace(storyManager.CurrentStoryId))
            return NormalizeStoryId(storyManager.CurrentStoryId);

        if (GameState.Instance != null && !string.IsNullOrWhiteSpace(GameState.Instance.CurrentStoryId))
            return NormalizeStoryId(GameState.Instance.CurrentStoryId);

        return "";
    }

    private static string NormalizeStoryId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }

    private static void SafeInvoke(Action callback, string label)
    {
        try
        {
            callback?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PreStorySetupFlow: callback '{label}' failed: {exception.Message}");
        }
    }
}
