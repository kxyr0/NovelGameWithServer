using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum WardrobeCategoryTabType
{
    [InspectorName("Нет")]
    None = 0,

    [InspectorName("Типаж")]
    Appearance = 1,

    [InspectorName("Прически")]
    Hair = 2,

    [InspectorName("Наряды")]
    Outfit = 3,

    [InspectorName("Аксессуары")]
    Accessories = 4,

    [InspectorName("Своя вкладка")]
    Custom = 100
}

[Serializable]
public sealed class WardrobeCategoryChangedEvent : UnityEvent<WardrobeCategoryTabType>
{
}

[Serializable]
public sealed class WardrobeCategoryTabBinding
{
    [SerializeField]
    [InspectorName("Категория")]
    [Tooltip("Какую часть гардероба открывает эта кнопка: типаж, прически, наряды, аксессуары или своя вкладка.")]
    private WardrobeCategoryTabType _category = WardrobeCategoryTabType.Appearance;

    [SerializeField]
    [InspectorName("Кнопка")]
    [Tooltip("Button нижней вкладки. Игрок нажимает сюда, чтобы сделать эту вкладку активной.")]
    private Button _button;

    [SerializeField]
    [InspectorName("Контент вкладки")]
    [Tooltip("Корневой объект контента этой вкладки. Активная вкладка видна, остальные скрываются.")]
    private GameObject _contentRoot;

    [SerializeField]
    [InspectorName("CanvasGroup контента")]
    [Tooltip("CanvasGroup контента вкладки. Если задан, скрипт скрывает неактивный контент через alpha/interactable/blocksRaycasts.")]
    private CanvasGroup _contentCanvasGroup;

    [SerializeField]
    [InspectorName("Текст выбранного варианта")]
    [Tooltip("TMP_Text внутри этой вкладки, куда пишется название выбранного типажа, прически или наряда.")]
    private TMP_Text _selectedOptionNameText;

    [SerializeField]
    [InspectorName("Текст если пусто")]
    [Tooltip("Что писать, если в текущей вкладке нет выбранного варианта.")]
    private string _emptySelectedOptionText = "";

    [SerializeField]
    [InspectorName("Active Sprite Fade")]
    [Tooltip("Sprite Fade элементы этой кнопки. Для выбранной вкладки включается active, для остальных возвращается default.")]
    private UISpriteStateFade[] _selectedSpriteFades = Array.Empty<UISpriteStateFade>();

    [SerializeField]
    [InspectorName("Стартовая вкладка")]
    [Tooltip("Если включено, эта вкладка будет выбрана первой при открытии, если в контроллере не задана другая активная категория.")]
    private bool _startsActive;

    [SerializeField]
    [InspectorName("При выборе")]
    [Tooltip("Дополнительные действия при выборе вкладки. Удобно для будущих аксессуаров или своей логики.")]
    private UnityEvent _onSelected = new UnityEvent();

    [NonSerialized] private UnityAction _clickAction;

    public WardrobeCategoryTabType Category => _category;
    public Button Button => _button;
    public GameObject ContentRoot => _contentRoot;
    public CanvasGroup ContentCanvasGroup => _contentCanvasGroup;
    public TMP_Text SelectedOptionNameText => _selectedOptionNameText;
    public bool StartsActive => _startsActive;

    public CanvasGroup ResolveContentCanvasGroup(bool createIfMissing)
    {
        if (_contentCanvasGroup != null)
            return _contentCanvasGroup;

        if (_contentRoot == null)
            return null;

        _contentCanvasGroup = _contentRoot.GetComponent<CanvasGroup>();
        if (_contentCanvasGroup == null && createIfMissing)
            _contentCanvasGroup = _contentRoot.AddComponent<CanvasGroup>();

        return _contentCanvasGroup;
    }

    public void Bind(Action<WardrobeCategoryTabBinding> onClicked)
    {
        if (_button == null)
            return;

        Unbind();
        _clickAction = () => onClicked?.Invoke(this);
        _button.onClick.AddListener(_clickAction);
    }

    public void Unbind()
    {
        if (_button != null && _clickAction != null)
            _button.onClick.RemoveListener(_clickAction);

        _clickAction = null;
    }

    public void ApplyVisual(bool active, bool disablePointerHover, bool propagateLinkedFades)
    {
        if (_selectedSpriteFades == null)
            return;

        for (int i = 0; i < _selectedSpriteFades.Length; i++)
        {
            UISpriteStateFade fade = _selectedSpriteFades[i];
            if (fade == null)
                continue;

            if (disablePointerHover)
                fade.SetPointerHoverEnabled(false, false);

            fade.SetActiveState(active, propagateLinkedFades);
        }
    }

    public void SetSelectedOptionName(string label)
    {
        if (_selectedOptionNameText == null)
            return;

        _selectedOptionNameText.text = string.IsNullOrWhiteSpace(label)
            ? (_emptySelectedOptionText ?? "")
            : label;
    }

    public void InvokeSelected()
    {
        _onSelected?.Invoke();
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Wardrobe Category Tabs")]
public sealed class WardrobeCategoryTabs : MonoBehaviour
{
    [Header("Связь с гардеробом")]
    [SerializeField]
    [InspectorName("Wardrobe Hero Setup Page")]
    [Tooltip("Страница гардероба, которая уже умеет показывать типаж, прически и наряды. Назначь сюда объект с WardrobeHeroSetupPage.")]
    private WardrobeHeroSetupPage _wardrobePage;

    [SerializeField]
    [InspectorName("Дергать страницу гардероба")]
    [Tooltip("Если включено, вкладки Типаж/Прически/Наряды будут открывать соответствующие списки вариантов в WardrobeHeroSetupPage.")]
    private bool _driveWardrobePage = true;

    [Header("Поведение")]
    [SerializeField]
    [InspectorName("Категория по умолчанию")]
    [Tooltip("Какая вкладка выбрана первой, если ни одна строка ниже не помечена как стартовая.")]
    private WardrobeCategoryTabType _defaultCategory = WardrobeCategoryTabType.Appearance;

    [SerializeField]
    [InspectorName("Применять при OnEnable")]
    [Tooltip("При включении объекта сразу выбрать стартовую вкладку и обновить видимость панелей.")]
    private bool _applyOnEnable = true;

    [SerializeField]
    [InspectorName("Запоминать runtime выбор")]
    [Tooltip("Если экран выключили и включили снова в этой же сессии, оставить последнюю выбранную вкладку.")]
    private bool _rememberRuntimeSelection = true;

    [SerializeField]
    [InspectorName("Сбрасывать вкладку при открытии")]
    [Tooltip("Если включено, при каждом входе на экран гардероба выбирается стартовая вкладка, а не последняя runtime-вкладка.")]
    private bool _resetToDefaultWhenOpened = true;

    [SerializeField]
    [InspectorName("Скрывать неактивный контент")]
    [Tooltip("Неактивные вкладки получают alpha 0 и перестают принимать клики. Если CanvasGroup не задан, объект будет выключен через SetActive.")]
    private bool _hideInactiveContent = true;

    [SerializeField]
    [InspectorName("Выключать объекты контента")]
    [Tooltip("Если включено, неактивные панели вкладок будут выключаться через SetActive(false), даже если у них есть CanvasGroup.")]
    private bool _deactivateInactiveContentObjects;

    [SerializeField]
    [InspectorName("Активная кнопка кликабельна")]
    [Tooltip("Оставить выбранную кнопку кликабельной. Обычно выключено: активная вкладка уже выбрана.")]
    private bool _selectedButtonInteractable;

    [SerializeField]
    [InspectorName("Остальные кнопки кликабельны")]
    [Tooltip("Оставить невыбранные кнопки кликабельными, чтобы игрок мог переключиться на другую вкладку.")]
    private bool _inactiveButtonsInteractable = true;

    [SerializeField]
    [InspectorName("Отключать hover у вкладок")]
    [Tooltip("Отключает hover-логику у Sprite Fade вкладок, чтобы active/default задавались только выбранной категорией.")]
    private bool _disableHoverOnTabFades = true;

    [SerializeField]
    [InspectorName("Linked fades у вкладок")]
    [Tooltip("Если выключено, вкладка меняет только явно назначенные Sprite Fade и не зажигает связанные элементы другой вкладки.")]
    private bool _propagateLinkedTabFades;

    [Header("Вкладки")]
    [SerializeField]
    [InspectorName("Список вкладок")]
    [Tooltip("Одна строка на одну нижнюю кнопку: Типаж, Прически, Наряды, Аксессуары. Ссылки назначаются вручную.")]
    private WardrobeCategoryTabBinding[] _tabs = Array.Empty<WardrobeCategoryTabBinding>();

    [Header("События")]
    [SerializeField]
    [InspectorName("Категория изменилась")]
    [Tooltip("Вызывается после выбора вкладки. Можно использовать для будущей отдельной логики аксессуаров.")]
    private WardrobeCategoryChangedEvent _categoryChanged = new WardrobeCategoryChangedEvent();

    private WardrobeCategoryTabType _currentCategory;
    private WardrobeHeroSetupPage _subscribedWardrobePage;

    public WardrobeCategoryTabType CurrentCategory => _currentCategory;

    private void OnEnable()
    {
        EnsureRuntimeBindings();

        if (_applyOnEnable)
            OpenDefaultCategory();

        SyncCurrentWardrobeOption();
    }

    private void OnDisable()
    {
        UnsubscribeWardrobePage();
        UnbindTabs();
    }

    private void OnValidate()
    {
        _tabs ??= Array.Empty<WardrobeCategoryTabBinding>();
    }

    public void SelectAppearance()
    {
        SelectCategory(WardrobeCategoryTabType.Appearance);
    }

    public void SelectHair()
    {
        SelectCategory(WardrobeCategoryTabType.Hair);
    }

    public void SelectOutfit()
    {
        SelectCategory(WardrobeCategoryTabType.Outfit);
    }

    public void SelectAccessories()
    {
        SelectCategory(WardrobeCategoryTabType.Accessories);
    }

    public void SelectByIndex(int index)
    {
        if (_tabs == null || index < 0 || index >= _tabs.Length || _tabs[index] == null)
            return;

        SelectCategory(_tabs[index].Category);
    }

    public void SelectCategory(WardrobeCategoryTabType category)
    {
        SelectCategory(category, notify: true);
    }

    public void Refresh()
    {
        ApplyTabState(_currentCategory);
        SyncCurrentWardrobeOption();
    }

    public void AssignWardrobePage(WardrobeHeroSetupPage wardrobePage)
    {
        if (_wardrobePage == wardrobePage)
            return;

        UnsubscribeWardrobePage();
        _wardrobePage = wardrobePage;
        SubscribeWardrobePage();
        SyncCurrentWardrobeOption();
    }

    public void OpenDefaultCategory()
    {
        EnsureRuntimeBindings();

        if (_resetToDefaultWhenOpened)
            _currentCategory = WardrobeCategoryTabType.None;

        SelectCategory(ResolveInitialCategory(), notify: false);
        SyncCurrentWardrobeOption();
    }

    public void OpenCategory(WardrobeCategoryTabType category)
    {
        EnsureRuntimeBindings();

        if (_resetToDefaultWhenOpened)
            _currentCategory = WardrobeCategoryTabType.None;

        SelectCategory(category, notify: false);
        SyncCurrentWardrobeOption();
    }

    private void SelectCategory(WardrobeCategoryTabType category, bool notify)
    {
        if (category == WardrobeCategoryTabType.None)
            category = ResolveInitialCategory();

        WardrobeCategoryTabBinding binding = FindBinding(category);
        if (binding == null && _tabs != null && _tabs.Length > 0)
        {
            binding = FindFirstValidBinding();
            if (binding != null)
                category = binding.Category;
        }

        if (category == WardrobeCategoryTabType.None)
            return;

        ClearTabState();
        _currentCategory = category;
        ApplyTabState(category);
        DriveWardrobePage(category);
        EnsureActiveContentVisible(category);
        SyncCurrentWardrobeOption();

        if (notify)
        {
            binding?.InvokeSelected();
            _categoryChanged?.Invoke(category);
        }
    }

    private WardrobeCategoryTabType ResolveInitialCategory()
    {
        if (_rememberRuntimeSelection && _currentCategory != WardrobeCategoryTabType.None)
            return _currentCategory;

        WardrobeCategoryTabBinding startBinding = FindStartsActiveBinding();
        if (startBinding != null)
            return startBinding.Category;

        if (_defaultCategory != WardrobeCategoryTabType.None)
            return _defaultCategory;

        WardrobeCategoryTabBinding firstBinding = FindFirstValidBinding();
        return firstBinding != null ? firstBinding.Category : WardrobeCategoryTabType.None;
    }

    private void BindTabs()
    {
        if (_tabs == null)
            return;

        for (int i = 0; i < _tabs.Length; i++)
        {
            WardrobeCategoryTabBinding tab = _tabs[i];
            if (tab != null)
                tab.Bind(HandleTabClicked);
        }
    }

    private void UnbindTabs()
    {
        if (_tabs == null)
            return;

        for (int i = 0; i < _tabs.Length; i++)
        {
            WardrobeCategoryTabBinding tab = _tabs[i];
            if (tab != null)
                tab.Unbind();
        }
    }

    private void HandleTabClicked(WardrobeCategoryTabBinding tab)
    {
        if (tab == null)
            return;

        SelectCategory(tab.Category);
    }

    private void EnsureRuntimeBindings()
    {
        AutoWireReferences();
        BindTabs();
        SubscribeWardrobePage();
    }

    private void AutoWireReferences()
    {
        if (_wardrobePage != null)
            return;

        _wardrobePage = GetComponentInParent<WardrobeHeroSetupPage>(true);

        if (_wardrobePage == null)
            _wardrobePage = FindObjectOfType<WardrobeHeroSetupPage>(true);
    }

    private void ClearTabState()
    {
        if (_tabs == null)
            return;

        for (int i = 0; i < _tabs.Length; i++)
        {
            WardrobeCategoryTabBinding tab = _tabs[i];
            if (tab == null)
                continue;

            ApplyButtonState(tab, false);
            ApplyContentState(tab, false);
            tab.ApplyVisual(false, _disableHoverOnTabFades, _propagateLinkedTabFades);
        }
    }

    private void ApplyTabState(WardrobeCategoryTabType activeCategory)
    {
        if (_tabs == null)
            return;

        for (int i = 0; i < _tabs.Length; i++)
        {
            WardrobeCategoryTabBinding tab = _tabs[i];
            if (tab == null)
                continue;

            bool active = tab.Category == activeCategory;
            ApplyButtonState(tab, active);
            ApplyContentState(tab, IsContentActiveForCategory(tab, activeCategory));
            tab.ApplyVisual(active, _disableHoverOnTabFades, _propagateLinkedTabFades);
        }

        EnforceInactiveTabVisuals(activeCategory);
    }

    private void EnforceInactiveTabVisuals(WardrobeCategoryTabType activeCategory)
    {
        if (_tabs == null)
            return;

        for (int i = 0; i < _tabs.Length; i++)
        {
            WardrobeCategoryTabBinding tab = _tabs[i];
            if (tab == null || tab.Category == activeCategory)
                continue;

            tab.ApplyVisual(false, _disableHoverOnTabFades, propagateLinkedFades: false);
        }
    }

    private bool IsContentActiveForCategory(WardrobeCategoryTabBinding tab, WardrobeCategoryTabType activeCategory)
    {
        if (tab == null)
            return false;

        if (tab.Category == activeCategory)
            return true;

        GameObject contentRoot = tab.ContentRoot;
        if (contentRoot == null || _tabs == null)
            return false;

        for (int i = 0; i < _tabs.Length; i++)
        {
            WardrobeCategoryTabBinding other = _tabs[i];
            if (other != null && other.Category == activeCategory && other.ContentRoot == contentRoot)
                return true;
        }

        return false;
    }

    private void EnsureActiveContentVisible(WardrobeCategoryTabType activeCategory)
    {
        if (_wardrobePage != null)
            _wardrobePage.EnsureSetupPanelVisible();

        WardrobeCategoryTabBinding activeTab = FindBinding(activeCategory);
        if (activeTab == null)
            return;

        ApplyContentState(activeTab, true);
    }

    private void ApplyButtonState(WardrobeCategoryTabBinding tab, bool active)
    {
        Button button = tab.Button;
        if (button == null)
            return;

        button.interactable = active ? _selectedButtonInteractable : _inactiveButtonsInteractable;
    }

    private void ApplyContentState(WardrobeCategoryTabBinding tab, bool active)
    {
        GameObject contentRoot = tab.ContentRoot;
        CanvasGroup canvasGroup = tab.ResolveContentCanvasGroup(createIfMissing: active || _hideInactiveContent);

        if (!_hideInactiveContent)
        {
            if (contentRoot != null && !contentRoot.activeSelf)
                contentRoot.SetActive(true);

            if (canvasGroup != null && active)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            return;
        }

        if (contentRoot != null)
        {
            if (active && !contentRoot.activeSelf)
                contentRoot.SetActive(true);
            else if (!active && _deactivateInactiveContentObjects && contentRoot.activeSelf)
                contentRoot.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = active ? 1f : 0f;
            canvasGroup.interactable = active;
            canvasGroup.blocksRaycasts = active;
            return;
        }

        if (contentRoot != null && !_deactivateInactiveContentObjects && contentRoot.activeSelf != active)
            contentRoot.SetActive(active);
    }

    private void DriveWardrobePage(WardrobeCategoryTabType category)
    {
        if (!_driveWardrobePage || _wardrobePage == null)
            return;

        switch (category)
        {
            case WardrobeCategoryTabType.Appearance:
                _wardrobePage.ShowAppearanceCategory();
                break;
            case WardrobeCategoryTabType.Hair:
                _wardrobePage.ShowHairCategory();
                break;
            case WardrobeCategoryTabType.Outfit:
                _wardrobePage.ShowOutfitCategory();
                break;
            case WardrobeCategoryTabType.Accessories:
                _wardrobePage.ShowAccessoriesCategory();
                break;
        }
    }

    private void SubscribeWardrobePage()
    {
        if (_subscribedWardrobePage == _wardrobePage)
            return;

        UnsubscribeWardrobePage();

        if (_wardrobePage == null)
            return;

        _subscribedWardrobePage = _wardrobePage;
        _subscribedWardrobePage.OptionSelectionChanged += HandleWardrobeOptionSelectionChanged;
    }

    private void UnsubscribeWardrobePage()
    {
        if (_subscribedWardrobePage != null)
            _subscribedWardrobePage.OptionSelectionChanged -= HandleWardrobeOptionSelectionChanged;

        _subscribedWardrobePage = null;
    }

    private void HandleWardrobeOptionSelectionChanged(WardrobeOptionSelectionInfo info)
    {
        WardrobeCategoryTabType category = MapStepToCategory(info.step);
        WardrobeCategoryTabBinding binding = FindBinding(category);
        binding?.SetSelectedOptionName(info.label);
    }

    private void SyncCurrentWardrobeOption()
    {
        if (_wardrobePage == null)
            return;

        HandleWardrobeOptionSelectionChanged(_wardrobePage.GetCurrentOptionSelectionInfo());
    }

    private static WardrobeCategoryTabType MapStepToCategory(WardrobeHeroSetupStep step)
    {
        switch (step)
        {
            case WardrobeHeroSetupStep.Appearance:
                return WardrobeCategoryTabType.Appearance;
            case WardrobeHeroSetupStep.Hair:
                return WardrobeCategoryTabType.Hair;
            case WardrobeHeroSetupStep.Outfit:
                return WardrobeCategoryTabType.Outfit;
            case WardrobeHeroSetupStep.Accessories:
                return WardrobeCategoryTabType.Accessories;
            default:
                return WardrobeCategoryTabType.None;
        }
    }

    private WardrobeCategoryTabBinding FindBinding(WardrobeCategoryTabType category)
    {
        if (_tabs == null)
            return null;

        for (int i = 0; i < _tabs.Length; i++)
        {
            WardrobeCategoryTabBinding tab = _tabs[i];
            if (tab != null && tab.Category == category)
                return tab;
        }

        return null;
    }

    private WardrobeCategoryTabBinding FindStartsActiveBinding()
    {
        if (_tabs == null)
            return null;

        for (int i = 0; i < _tabs.Length; i++)
        {
            WardrobeCategoryTabBinding tab = _tabs[i];
            if (tab != null && tab.StartsActive && tab.Category != WardrobeCategoryTabType.None)
                return tab;
        }

        return null;
    }

    private WardrobeCategoryTabBinding FindFirstValidBinding()
    {
        if (_tabs == null)
            return null;

        for (int i = 0; i < _tabs.Length; i++)
        {
            WardrobeCategoryTabBinding tab = _tabs[i];
            if (tab != null && tab.Category != WardrobeCategoryTabType.None)
                return tab;
        }

        return null;
    }
}
