using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Story Save Slot View")]
public sealed class StorySaveSlotView : MonoBehaviour
{
    [Header("Основное")]
    [SerializeField]
    [InspectorName("Контроллер экрана")]
    [Tooltip("StorySaveSlotsScreenController, который управляет этим слотом. Можно не заполнять: контроллер сам назначит себя при Refresh/OnEnable.")]
    private StorySaveSlotsScreenController _controller;

    [SerializeField, Min(0)]
    [InspectorName("Номер слота вручную")]
    [Tooltip("Номер слота на экране: 1, 2, 3, 4, 5. Если оставить 0, контроллер выставит номер по порядку в массиве Slot Views.")]
    private int _slotNumberOverride;

    [SerializeField]
    [InspectorName("Заблокирован")]
    [Tooltip("Если включено, слот считается locked: кнопки записи/удаления/загрузки отключаются, включается Locked Root.")]
    private bool _locked;

    [Header("Текст и изображение")]
    [SerializeField]
    [InspectorName("Текст слота")]
    [Tooltip("TMP_Text для основной надписи слота: 'Слот 1', '(Пустой)', 'Слот 3'.")]
    private TMP_Text _titleText;

    [SerializeField]
    [InspectorName("Дополнительный текст")]
    [Tooltip("Необязательный TMP_Text для даты/главы/описания сохранения. Можно оставить пустым, если в макете только один текст.")]
    private TMP_Text _detailsText;

    [SerializeField]
    [InspectorName("Image слота")]
    [Tooltip("Необязательный Image для состояния слота: обложка истории, пустая иконка или locked-иконка.")]
    private Image _previewImage;

    [Header("Кнопки")]
    [SerializeField]
    [InspectorName("Кнопка слота")]
    [Tooltip("Кнопка всей плашки. Обычно используется для загрузки слота. Можно оставить пустой, если нужны только запись и мусорка.")]
    private Button _slotButton;

    [SerializeField]
    [InspectorName("Кнопка записи")]
    [Tooltip("Кнопка с иконкой карандаша/записи. При нажатии перезаписывает этот слот текущим прогрессом.")]
    private Button _writeButton;

    [SerializeField]
    [InspectorName("Кнопка удаления")]
    [Tooltip("Кнопка с иконкой мусорки. При нажатии удаляет сохранение из этого слота.")]
    private Button _deleteButton;

    [SerializeField]
    [InspectorName("Авто-привязать кнопки")]
    [Tooltip("Если включено, компонент сам подпишет Slot Button, Write Button и Delete Button на свои методы. Если хочешь привязать OnClick вручную в инспекторе, выключи.")]
    private bool _bindButtonsAutomatically = true;

    [Header("Root-объекты состояний")]
    [SerializeField]
    [InspectorName("Locked Root")]
    [Tooltip("Объект/иконка замка. Включается только когда слот заблокирован.")]
    private GameObject _lockedRoot;

    [SerializeField]
    [InspectorName("Actions Root")]
    [Tooltip("Общий объект с кнопками записи и удаления. Скрывается у locked-слотов.")]
    private GameObject _actionsRoot;

    [SerializeField]
    [InspectorName("Empty Root")]
    [Tooltip("Необязательный объект для состояния пустого слота. Включается, когда сохранения нет и слот не locked.")]
    private GameObject _emptyRoot;

    [SerializeField]
    [InspectorName("Filled Root")]
    [Tooltip("Необязательный объект для состояния занятого слота. Включается, когда сохранение есть и слот не locked.")]
    private GameObject _filledRoot;

    [SerializeField]
    [InspectorName("Selected Root")]
    [Tooltip("Необязательный объект подсветки выбранного слота. Включается, когда этот слот выбран как активная цель сохранения истории.")]
    private GameObject _selectedRoot;

    [Header("Спрайты состояний")]
    [SerializeField]
    [InspectorName("Спрайт пустого слота")]
    [Tooltip("Спрайт для Preview Image, когда слот пустой. Можно не заполнять.")]
    private Sprite _emptySprite;

    [SerializeField]
    [InspectorName("Спрайт занятого слота")]
    [Tooltip("Спрайт для Preview Image, когда слот занят. Если контроллер передаст обложку истории, она будет приоритетнее.")]
    private Sprite _filledSprite;

    [SerializeField]
    [InspectorName("Спрайт locked")]
    [Tooltip("Спрайт для Preview Image, когда слот заблокирован. Можно не заполнять, если замок отдельным объектом.")]
    private Sprite _lockedSprite;

    [Header("Поведение")]
    [SerializeField]
    [InspectorName("Удаление только если занято")]
    [Tooltip("Если включено, кнопка мусорки показывается и работает только когда в слоте есть сохранение.")]
    private bool _deleteOnlyWhenFilled = true;


    private int _slotNumber;
    private bool _hasSave;
    private bool _isBound;

    public int SlotNumber => _slotNumber > 0 ? _slotNumber : Mathf.Max(0, _slotNumberOverride);
    public bool Locked => _locked;
    public bool HasSave => _hasSave;

    private void OnEnable()
    {
        if (_bindButtonsAutomatically)
            BindButtonListeners();
    }

    private void OnDisable()
    {
        if (_bindButtonsAutomatically)
            UnbindButtonListeners();
    }

    private void OnValidate()
    {
        _slotNumberOverride = Mathf.Max(0, _slotNumberOverride);
    }

    public void Bind(StorySaveSlotsScreenController controller, int slotNumber)
    {
        if (controller != null)
            _controller = controller;

        _slotNumber = Mathf.Max(1, slotNumber);

        if (_bindButtonsAutomatically && isActiveAndEnabled)
            BindButtonListeners();
    }

    public void Refresh(StorySaveSlotInfo info)
    {
        if (info == null)
            return;

        _slotNumber = Mathf.Max(1, info.SlotNumber);
        _hasSave = info.HasSave;
        bool locked = info.Locked || _locked;

        ApplyText(info, locked);

        ApplyPreviewSprite(info, locked);
        SetActive(_lockedRoot, locked);
        SetActive(_actionsRoot, !locked && info.ShowActions);
        SetActive(_emptyRoot, !locked && !info.HasSave);
        SetActive(_filledRoot, !locked && info.HasSave);
        SetActive(_selectedRoot, !locked && info.IsSelected);

        bool showWrite = !locked && info.CanWrite;
        bool showDelete = !locked && info.CanDelete && (!_deleteOnlyWhenFilled || info.HasSave);
        SetButtonVisible(_writeButton, showWrite);
        SetButtonVisible(_deleteButton, showDelete);
    }

    public void SaveToThisSlot()
    {
        ResolveController()?.SaveToSlotNumber(SlotNumber);
    }

    public void DeleteThisSlot()
    {
        ResolveController()?.DeleteSlotNumber(SlotNumber);
    }

    public void LoadThisSlot()
    {
        ResolveController()?.LoadSlotNumber(SlotNumber);
    }

    public void SelectThisSlot()
    {
        ResolveController()?.HandleSlotClicked(this);
    }

    private StorySaveSlotsScreenController ResolveController()
    {
        if (_controller == null)
            _controller = GetComponentInParent<StorySaveSlotsScreenController>(true);

        return _controller;
    }

    private void BindButtonListeners()
    {
        if (_isBound)
            return;

        if (_slotButton != null)
            _slotButton.onClick.AddListener(SelectThisSlot);
        if (_writeButton != null)
            _writeButton.onClick.AddListener(SaveToThisSlot);
        if (_deleteButton != null)
            _deleteButton.onClick.AddListener(DeleteThisSlot);

        _isBound = true;
    }

    private void UnbindButtonListeners()
    {
        if (!_isBound)
            return;

        if (_slotButton != null)
            _slotButton.onClick.RemoveListener(SelectThisSlot);
        if (_writeButton != null)
            _writeButton.onClick.RemoveListener(SaveToThisSlot);
        if (_deleteButton != null)
            _deleteButton.onClick.RemoveListener(DeleteThisSlot);

        _isBound = false;
    }

    private void ApplyText(StorySaveSlotInfo info, bool locked)
    {
        string title = info.Title ?? "";
        string details = info.Details ?? "";

        if (!locked && !info.HasSave && info.IsSelected && !string.IsNullOrWhiteSpace(details))
        {
            title = details;
            details = "";
        }

        if (_titleText != null && _titleText == _detailsText)
        {
            _titleText.text = string.IsNullOrWhiteSpace(details)
                ? title
                : title + "\n" + details;
            _titleText.enabled = !string.IsNullOrWhiteSpace(_titleText.text);
            return;
        }

        if (_titleText != null)
        {
            _titleText.text = title;
            _titleText.enabled = !string.IsNullOrWhiteSpace(title);
        }

        if (_detailsText != null)
        {
            _detailsText.text = details;
            _detailsText.enabled = !string.IsNullOrWhiteSpace(details);
        }
    }

    private void ApplyPreviewSprite(StorySaveSlotInfo info, bool locked)
    {
        if (_previewImage == null)
            return;

        Sprite sprite = null;
        if (locked)
            sprite = _lockedSprite;
        else if (info.HasSave)
            sprite = info.PreviewSprite != null ? info.PreviewSprite : _filledSprite;
        else
            sprite = _emptySprite;

        if (sprite != null)
            _previewImage.sprite = sprite;

        _previewImage.enabled = sprite != null || _previewImage.sprite != null;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private static void SetButtonVisible(Button button, bool visible)
    {
        if (button != null && button.gameObject.activeSelf != visible)
            button.gameObject.SetActive(visible);
    }

}
