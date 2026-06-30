using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public sealed class WardrobeOptionButtonSpriteSwap
{
    [SerializeField]
    [InspectorName("Image")]
    [Tooltip("Image, у которого нужно менять sprite вместе с состоянием Выбрать/Выбрано.")]
    private Image _image;

    [SerializeField]
    [InspectorName("Обычный Sprite")]
    [Tooltip("Sprite для состояния Выбрать. Если пусто и включен Захватить текущий, будет взят текущий sprite из Image.")]
    private Sprite _defaultSprite;

    [SerializeField]
    [InspectorName("Sprite когда выбрано")]
    [Tooltip("Sprite для состояния Выбрано.")]
    private Sprite _selectedSprite;

    [SerializeField]
    [InspectorName("Захватить текущий")]
    [Tooltip("Если Обычный Sprite пустой, взять его из текущего Image при запуске.")]
    private bool _captureCurrentAsDefault = true;

    public void CaptureDefaultIfNeeded()
    {
        if (!_captureCurrentAsDefault || _defaultSprite != null || _image == null)
            return;

        _defaultSprite = _image.sprite;
    }

    public void Apply(bool selected)
    {
        if (_image == null)
            return;

        Sprite sprite = selected ? _selectedSprite : _defaultSprite;
        if (sprite != null)
            _image.sprite = sprite;
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Wardrobe Option Button View")]
public sealed class WardrobeOptionButtonView : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField]
    [InspectorName("Button")]
    [Tooltip("Button этого варианта. Если пусто, скрипт попробует взять Button на этом же объекте.")]
    private Button _button;

    [SerializeField]
    [InspectorName("Текст названия")]
    [Tooltip("TMP_Text с названием варианта. Можно оставить пустым, если на кнопке нужен только текст Выбрать/Выбрано.")]
    private TMP_Text _optionLabelText;

    [SerializeField]
    [InspectorName("Текст состояния")]
    [Tooltip("TMP_Text, где будет написано Выбрать или Выбрано.")]
    private TMP_Text _stateText;

    [SerializeField]
    [InspectorName("Image кнопки")]
    [Tooltip("Основная картинка кнопки. Если пусто, скрипт возьмет Button.targetGraphic как Image или Image на этом объекте.")]
    private Image _buttonImage;

    [SerializeField]
    [InspectorName("Один текст как состояние")]
    [Tooltip("Если отдельный Текст состояния не задан, использовать Текст названия для Выбрать/Выбрано.")]
    private bool _useLabelTextAsStateWhenMissing = true;

    [Header("Тексты")]
    [SerializeField]
    [InspectorName("Не выбрано")]
    [Tooltip("Текст на невыбранном варианте.")]
    private string _defaultStateText = "Выбрать";

    [SerializeField]
    [InspectorName("Выбрано")]
    [Tooltip("Текст на выбранном варианте.")]
    private string _selectedStateText = "Выбрано";

    [SerializeField]
    [InspectorName("Недоступно")]
    [Tooltip("Текст на кнопке, когда текущая категория не содержит доступных вариантов.")]
    private string _unavailableStateText = "Недоступно";

    [Header("Визуал")]
    [SerializeField]
    [InspectorName("Обычный Sprite кнопки")]
    [Tooltip("Sprite кнопки в состоянии Выбрать. Если пусто и включен Захватить текущий, будет взят текущий sprite из Image кнопки.")]
    private Sprite _defaultButtonSprite;

    [SerializeField]
    [InspectorName("Sprite кнопки когда выбрано")]
    [Tooltip("Sprite кнопки в состоянии Выбрано.")]
    private Sprite _selectedButtonSprite;

    [SerializeField]
    [InspectorName("Захватить текущий Sprite")]
    [Tooltip("Если Обычный Sprite кнопки пустой, взять его из Image кнопки при запуске.")]
    private bool _captureCurrentButtonSpriteAsDefault = true;

    [SerializeField]
    [InspectorName("Доп. Image спрайты")]
    [Tooltip("Дополнительные картинки кнопки или декора, которым тоже нужно менять sprite при Выбрать/Выбрано.")]
    private WardrobeOptionButtonSpriteSwap[] _extraSpriteSwaps = Array.Empty<WardrobeOptionButtonSpriteSwap>();

    [Header("Premium")]
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private Image _costIcon;
    [SerializeField] private string _paidStateText = "Купить за";
    [SerializeField] private float _paidStateTextOffsetX = -60f;

    [SerializeField]
    [InspectorName("Sprite Fade выбранного")]
    [Tooltip("Все Sprite Fade элементы этой option-кнопки: плашка, декор, рамка. Выбранный вариант включает active, остальные возвращаются в default.")]
    private UISpriteStateFade[] _selectedSpriteFades = Array.Empty<UISpriteStateFade>();

    [SerializeField]
    [InspectorName("Декор выбранного")]
    [Tooltip("Дополнительные объекты декора, которые видны только у выбранного варианта.")]
    private GameObject[] _selectedDecorObjects = Array.Empty<GameObject>();

    [SerializeField]
    [InspectorName("Декор всегда виден")]
    [Tooltip("Если включено, декор не выключается у невыбранного варианта: меняется только sprite/состояние.")]
    private bool _keepDecorObjectsVisible = true;

    [SerializeField]
    [InspectorName("Отключать hover у fades")]
    [Tooltip("Отключает hover-логику у Sprite Fade, чтобы состояние держалось от выбора, а не от курсора.")]
    private bool _disableHoverOnStateFades = true;

    [SerializeField]
    [InspectorName("Выбранная кнопка кликабельна")]
    [Tooltip("Если выключить, выбранный вариант перестанет принимать повторный клик.")]
    private bool _selectedButtonInteractable;

    [SerializeField]
    [InspectorName("Обновлять при OnEnable")]
    [Tooltip("При включении объекта сразу применить последнее выбранное/невыбранное состояние.")]
    private bool _applyOnEnable = true;

    private int _optionIndex = -1;
    private UnityAction _clickAction;
    private Action<int> _onClicked;
    private bool _selected;
    private bool _unavailable;
    private string _optionLabel = "";
    private int _premiumCost;
    private Sprite _premiumCostIcon;
    private RectTransform _stateTextRect;
    private Vector2 _defaultStateTextAnchoredPosition;
    private bool _defaultStateTextPositionCaptured;
    private bool _premiumReferenceWarningLogged;

    private void Awake()
    {
        EnsureReferences();
        PrepareStateFades();
    }

    private void OnEnable()
    {
        EnsureReferences();
        PrepareStateFades();

        if (_applyOnEnable)
            ApplySelectedState();
    }

    private void OnValidate()
    {
        _selectedSpriteFades ??= Array.Empty<UISpriteStateFade>();
        _selectedDecorObjects ??= Array.Empty<GameObject>();
        _extraSpriteSwaps ??= Array.Empty<WardrobeOptionButtonSpriteSwap>();
        EnsureReferences();
    }

    private void OnDestroy()
    {
        UnbindClick();
    }

    public void Configure(int optionIndex, string optionLabel, Action<int> onClicked)
    {
        EnsureReferences();
        UnbindClick();

        _optionIndex = optionIndex;
        _onClicked = onClicked;
        SetOptionLabel(optionLabel);

        if (_button != null)
        {
            _clickAction = HandleClick;
            _button.onClick.AddListener(_clickAction);
        }
    }

    public void SetOptionLabel(string optionLabel)
    {
        _optionLabel = optionLabel ?? "";

        if (_optionLabelText == null)
            return;

        if (_stateText == null && _useLabelTextAsStateWhenMissing)
            return;

        _optionLabelText.text = _optionLabel;
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        ApplySelectedState();
    }

    public void SetUnavailable(bool unavailable)
    {
        _unavailable = unavailable;
        if (_unavailable)
        {
            _selected = false;
            _premiumCost = 0;
        }

        ApplySelectedState();
    }

    public void SetPremiumCost(int premiumCost, Sprite premiumCostIcon = null)
    {
        EnsureReferences();
        _premiumCost = Mathf.Max(0, premiumCost);
        _premiumCostIcon = premiumCostIcon;
        LogMissingPremiumReferencesIfNeeded();
        ApplySelectedState();
    }

    private void LogMissingPremiumReferencesIfNeeded()
    {
        if (_premiumReferenceWarningLogged || _premiumCost <= 0 || (_costText != null && _costIcon != null))
            return;

        _premiumReferenceWarningLogged = true;
        AppLogger.Warn(
            AppLogCategory.Wardrobe,
            nameof(WardrobeOptionButtonView),
            nameof(SetPremiumCost),
            "[WARDROBE][BUTTON_PREMIUM_REFERENCES] Paid option button is missing CostText or CostIcon reference.",
            LogMetadata.Of(
                "button", name,
                "optionIndex", _optionIndex,
                "optionLabel", _optionLabel,
                "premiumCost", _premiumCost,
                "hasCostText", _costText != null,
                "hasCostIcon", _costIcon != null),
            recoverable: true);
    }

    private void ApplySelectedState()
    {
        TMP_Text stateText = ResolveStateText();
        bool showPremium = !_unavailable && !_selected && _premiumCost > 0;

        if (stateText != null)
            stateText.text = _unavailable ? _unavailableStateText : _selected ? _selectedStateText : showPremium ? _paidStateText : _defaultStateText;

        if (_button != null)
            _button.interactable = !_unavailable && (_selectedButtonInteractable || !_selected);

        ApplyPremiumState(stateText, showPremium);
        ApplyDirectSprites(_selected || _unavailable);
        ApplyStateFades(_selected && !_unavailable);
        ApplyDecor(_selected && !_unavailable);
    }

    private void ApplyPremiumState(TMP_Text stateText, bool showPremium)
    {
        CaptureDefaultStateTextPosition(stateText);

        if (_costText != null)
        {
            _costText.text = showPremium ? _premiumCost.ToString() : "";
            if (_costText.gameObject.activeSelf != showPremium)
                _costText.gameObject.SetActive(showPremium);
        }

        if (_costIcon != null)
        {
            if (_premiumCostIcon != null)
                _costIcon.sprite = _premiumCostIcon;

            _costIcon.enabled = showPremium;
            if (_costIcon.gameObject.activeSelf != showPremium)
                _costIcon.gameObject.SetActive(showPremium);
        }

        if (_stateTextRect == null || !_defaultStateTextPositionCaptured)
            return;

        Vector2 position = _defaultStateTextAnchoredPosition;
        if (showPremium)
            position.x += _paidStateTextOffsetX;

        _stateTextRect.anchoredPosition = position;
    }

    private void CaptureDefaultStateTextPosition(TMP_Text stateText)
    {
        if (stateText == null)
            return;

        RectTransform rect = stateText.rectTransform;
        if (rect == null)
            return;

        if (_stateTextRect == rect && _defaultStateTextPositionCaptured)
            return;

        _stateTextRect = rect;
        _defaultStateTextAnchoredPosition = rect.anchoredPosition;
        _defaultStateTextPositionCaptured = true;
    }

    private TMP_Text ResolveStateText()
    {
        if (_stateText != null)
            return _stateText;

        return _useLabelTextAsStateWhenMissing ? _optionLabelText : null;
    }

    private void HandleClick()
    {
        if (_unavailable)
            return;

        _onClicked?.Invoke(_optionIndex);
    }

    private void UnbindClick()
    {
        if (_button != null && _clickAction != null)
            _button.onClick.RemoveListener(_clickAction);

        _clickAction = null;
        _onClicked = null;
    }

    private void EnsureReferences()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        if (_buttonImage == null && _button != null)
            _buttonImage = _button.targetGraphic as Image;

        if (_buttonImage == null)
            _buttonImage = GetComponent<Image>();

        if (_buttonImage == null && _button != null)
            _buttonImage = FindBestChildImage(_button.transform);

        if (_buttonImage == null)
            _buttonImage = FindBestChildImage(transform);

        if (_costText == null)
            _costText = FindTextByName("CostText");

        if (_costIcon == null)
            _costIcon = FindCostIcon();

        CaptureDefaultSpritesIfNeeded();
    }

    private TMP_Text FindTextByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text != null && string.Equals(text.name, objectName, StringComparison.OrdinalIgnoreCase))
                return text;
        }

        return null;
    }

    private Image FindCostIcon()
    {
        Image[] images = GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (IsButtonGraphic(image))
                continue;

            if (HasCostIconName(image.name))
            {
                return image;
            }
        }

        return FindNearestCostTextImage(images);
    }

    private bool IsButtonGraphic(Image image)
    {
        if (image == null || image == _buttonImage)
            return true;

        return _button != null && image == _button.targetGraphic;
    }

    private static bool HasCostIconName(string imageName)
    {
        if (string.IsNullOrWhiteSpace(imageName))
            return false;

        return imageName.IndexOf("Cost", StringComparison.OrdinalIgnoreCase) >= 0 ||
            imageName.IndexOf("Price", StringComparison.OrdinalIgnoreCase) >= 0 ||
            imageName.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0 ||
            imageName.IndexOf("Coin", StringComparison.OrdinalIgnoreCase) >= 0 ||
            imageName.IndexOf("Premium", StringComparison.OrdinalIgnoreCase) >= 0 ||
            imageName.IndexOf("Currency", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasDecorName(string imageName)
    {
        if (string.IsNullOrWhiteSpace(imageName))
            return false;

        return imageName.IndexOf("Decor", StringComparison.OrdinalIgnoreCase) >= 0 ||
            imageName.IndexOf("Frame", StringComparison.OrdinalIgnoreCase) >= 0 ||
            imageName.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0 ||
            imageName.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private Image FindNearestCostTextImage(Image[] images)
    {
        if (_costText == null || images == null)
            return null;

        Transform costParent = _costText.transform.parent;
        if (costParent == null)
            return null;

        Image best = null;
        int bestDistance = int.MaxValue;
        int costSiblingIndex = _costText.transform.GetSiblingIndex();

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (IsButtonGraphic(image) || image.transform.parent != costParent || HasDecorName(image.name))
                continue;

            int distance = Mathf.Abs(image.transform.GetSiblingIndex() - costSiblingIndex);
            if (distance < bestDistance)
            {
                best = image;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static Image FindBestChildImage(Transform root)
    {
        if (root == null)
            return null;

        Image[] images = root.GetComponentsInChildren<Image>(true);
        Image best = null;
        float bestArea = -1f;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            if (image.GetComponentInParent<Button>(true) != root.GetComponent<Button>())
                continue;

            RectTransform rect = image.rectTransform;
            float area = Mathf.Abs(rect.rect.width * rect.rect.height);
            if (area > bestArea)
            {
                bestArea = area;
                best = image;
            }
        }

        return best;
    }

    private void CaptureDefaultSpritesIfNeeded()
    {
        if (_captureCurrentButtonSpriteAsDefault && _defaultButtonSprite == null && _buttonImage != null)
            _defaultButtonSprite = _buttonImage.sprite;

        if (_extraSpriteSwaps == null)
            return;

        for (int i = 0; i < _extraSpriteSwaps.Length; i++)
            _extraSpriteSwaps[i]?.CaptureDefaultIfNeeded();
    }

    private void ApplyDirectSprites(bool selected)
    {
        if (_buttonImage != null)
        {
            Sprite sprite = selected ? _selectedButtonSprite : _defaultButtonSprite;
            if (sprite != null)
                _buttonImage.sprite = sprite;
        }

        if (_extraSpriteSwaps == null)
            return;

        for (int i = 0; i < _extraSpriteSwaps.Length; i++)
            _extraSpriteSwaps[i]?.Apply(selected);
    }

    private void PrepareStateFades()
    {
        if (!_disableHoverOnStateFades || _selectedSpriteFades == null)
            return;

        for (int i = 0; i < _selectedSpriteFades.Length; i++)
        {
            UISpriteStateFade fade = _selectedSpriteFades[i];
            if (fade != null)
                fade.SetPointerHoverEnabled(false, false);
        }
    }

    private void ApplyStateFades(bool active)
    {
        if (_selectedSpriteFades == null)
            return;

        for (int i = 0; i < _selectedSpriteFades.Length; i++)
        {
            UISpriteStateFade fade = _selectedSpriteFades[i];
            if (fade != null)
                fade.SetActiveState(active);
        }
    }

    private void ApplyDecor(bool active)
    {
        if (_selectedDecorObjects == null)
            return;

        bool visible = _keepDecorObjectsVisible || active;
        for (int i = 0; i < _selectedDecorObjects.Length; i++)
        {
            GameObject decor = _selectedDecorObjects[i];
            if (decor != null && decor.activeSelf != visible)
                decor.SetActive(visible);
        }
    }
}
