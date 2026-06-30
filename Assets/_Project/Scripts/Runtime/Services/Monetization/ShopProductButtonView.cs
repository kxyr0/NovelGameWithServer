using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Purchasing;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ShopProductButtonView : MonoBehaviour
{
    [Header("Идентификатор кнопки")]
    [InspectorName("ID кнопки")]
    [Tooltip("Стабильный ID именно этой Unity-карточки. Не является ID товара. Нужен, чтобы админка могла обновлять эту кнопку даже если productId, цена или порядок изменятся.")]
    [SerializeField] private string _buttonId = "";

    [Header("Товар")]
    [InspectorName("ID товара")]
    [Tooltip("ID товара для покупки и IAP/серверного заказа. Например candles_vase_30. Может меняться через админку, а кнопка всё равно найдётся по ID кнопки.")]
    [SerializeField] private string _productId = "";
    [InspectorName("Название")]
    [Tooltip("Название на карточке магазина. Например Вазочка свечей. Сервер может переопределить это поле через name/title/displayName.")]
    [FormerlySerializedAs("_displayName")]
    [SerializeField] private string _productName = "";
    [InspectorName("Количество")]
    [Tooltip("Сколько ресурса выдаёт покупка. Например 30 для x30.")]
    [FormerlySerializedAs("_amount")]
    [SerializeField] private int _rewardAmount = 1;
    [InspectorName("Текст количества")]
    [Tooltip("Текст для UI, например x30. Если оставить пустым, будет автоматически показано x + Количество. Это не влияет на серверный amount и выдачу валюты.")]
    [SerializeField] private string _amountDisplay = "";
    [InspectorName("Тип награды")]
    [Tooltip("Что выдаёт покупка: свечи или искры/сердца. Это не цена. Цена задаётся отдельным полем Текст цены.")]
    [FormerlySerializedAs("_currency")]
    [SerializeField] private ShopCurrency _rewardCurrency = ShopCurrency.Candles;
    [InspectorName("Подпись награды")]
    [Tooltip("Текстовая подпись награды, если нужен отдельный текст вроде Свечи. Можно оставить пустым, если на карточке уже есть название и xКоличество.")]
    [FormerlySerializedAs("_currencyLabel")]
    [SerializeField] private string _rewardLabel = "";
    [InspectorName("Текст цены")]
    [Tooltip("Полный текст цены одной строкой. Например: 999 рублей. Не нужно отдельно указывать валюту цены.")]
    [SerializeField] private string _priceLabel = "";
    [InspectorName("Количество в заказе")]
    [Tooltip("Сколько единиц productId отправлять в заказ. Обычно 1. Это не x30 на карточке, x30 задаётся в поле Количество.")]
    [FormerlySerializedAs("_quantity")]
    [SerializeField] private int _orderQuantity = 1;
    [InspectorName("Тип IAP")]
    [Tooltip("Тип продукта для Unity IAP: Consumable для расходуемых покупок вроде свечей, NonConsumable для постоянных покупок, Subscription для подписок.")]
    [SerializeField] private ProductType _productType = ProductType.Consumable;
    [InspectorName("Порядок")]
    [Tooltip("Локальный порядок карточки. Меньше значение — выше/раньше. Сервер может переопределить через sortOrder/order/position.")]
    [SerializeField] private int _sortOrder = 0;
    [InspectorName("Брать порядок с сервера")]
    [Tooltip("Если включено, sortOrder из админки/сервера двигает карточку. Если выключено, используется локальное поле Порядок.")]
    [SerializeField] private bool _useServerSortOrder = true;

    [Header("Запасной визуал")]
    [InspectorName("Иконка")]
    [Tooltip("Локальная иконка товара. Используется как fallback, если сервер не прислал свою иконку.")]
    [FormerlySerializedAs("_icon")]
    [SerializeField] private Sprite _fallbackIcon;
    [InspectorName("Скрывать без сервера")]
    [Tooltip("Если включено, карточка скрывается, когда после загрузки сервера для её ID кнопки/ID товара не нашлось данных.")]
    [SerializeField] private bool _hideWhenServerMissing = false;
    [InspectorName("Отключать без ID товара")]
    [Tooltip("Если включено, кнопка покупки становится неактивной, когда ID товара пустой.")]
    [SerializeField] private bool _disableWhenProductIdMissing = true;
    [InspectorName("Скрывать иконку без спрайта")]
    [Tooltip("Если включено, объект иконки будет выключаться, когда у товара нет спрайта. Обычно оставляй выключенным, чтобы карточка магазина случайно не погасила фон или декоративный Image.")]
    [SerializeField] private bool _hideIconWhenMissing = false;

    [Header("Ссылки")]
    [InspectorName("Кнопка")]
    [Tooltip("Button, по клику которого запускается покупка этого товара.")]
    [FormerlySerializedAs("_button")]
    [SerializeField] private Button _buyButton;
    [InspectorName("Иконка товара")]
    [Tooltip("Image, куда будет поставлена иконка товара.")]
    [FormerlySerializedAs("_iconImage")]
    [SerializeField] private Image _productIconImage;
    [InspectorName("Текст названия")]
    [Tooltip("TMP_Text для названия товара, например Вазочка свечей.")]
    [FormerlySerializedAs("_nameText")]
    [SerializeField] private TMP_Text _productNameText;
    [InspectorName("Текст количества")]
    [Tooltip("TMP_Text для количества награды, например x30.")]
    [FormerlySerializedAs("_amountText")]
    [SerializeField] private TMP_Text _amountDisplayText;
    [InspectorName("Отдельный текст награды")]
    [Tooltip("Не цена. Опциональный TMP_Text для подписи награды, например Свечи. Можно оставить пустым.")]
    [FormerlySerializedAs("_currencyText")]
    [SerializeField] private TMP_Text _rewardLabelText;
    [InspectorName("Текст цены")]
    [Tooltip("TMP_Text, куда будет записан полный текст цены: 999 рублей.")]
    [FormerlySerializedAs("_priceText")]
    [SerializeField] private TMP_Text _priceLabelText;
    [InspectorName("Общий текст карточки")]
    [Tooltip("Опциональный TMP_Text для собранной подписи вида Название x30 Свечи. Если дизайн использует отдельные тексты, оставь пустым.")]
    [FormerlySerializedAs("_combinedLabelText")]
    [SerializeField] private TMP_Text _combinedProductLabelText;
    [InspectorName("Объект цены")]
    [Tooltip("Опциональный GameObject с визуалом цены. Будет скрыт, если текст цены пустой.")]
    [SerializeField] private GameObject _priceRoot;
    [InspectorName("Объект недоступности")]
    [Tooltip("Опциональный GameObject для состояния недоступного товара.")]
    [SerializeField] private GameObject _unavailableRoot;

    private ShopItemData _currentData;
    private Action<ShopItemData> _onBuy;
    private bool _listenerBound;
    private Button _boundButton;
    private ShopProductButtonClickProxy _clickProxy;
    private int _lastHandledClickFrame = -1;

    public string ButtonId => ResolveButtonId();
    public string ProductId => SaveDataSanitizer.SanitizeIdentifier(_productId);
    public int LocalSortOrder => _sortOrder;
    public bool HideWhenServerMissing => _hideWhenServerMissing;
    public ShopItemData CurrentData => _currentData;

    private void Awake()
    {
        ResolveReferences();
        BindButton();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindButton();
    }

    private void OnDestroy()
    {
        if (_boundButton != null && _listenerBound)
            _boundButton.onClick.RemoveListener(HandleButtonClick);
    }
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        string sanitized = SaveDataSanitizer.SanitizeIdentifier(_buttonId);
        if (_buttonId != sanitized)
            _buttonId = sanitized;

        if (string.IsNullOrWhiteSpace(_buttonId) || HasDuplicateButtonIdInScene(_buttonId))
        {
            _buttonId = CreateButtonId();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        _rewardAmount = Mathf.Max(0, _rewardAmount);
        _orderQuantity = Mathf.Clamp(_orderQuantity <= 0 ? 1 : _orderQuantity, 1, 99);
        ResolveReferences();
        RefreshEditorPreview();
    }
#endif

    public ShopItemData BuildLocalData()
    {
        string buttonId = ButtonId;
        string productId = ProductId;
        int rewardAmount = Mathf.Max(0, _rewardAmount);
        string label = string.IsNullOrWhiteSpace(_productName) ? productId : _productName.Trim();
        string rewardLabel = string.IsNullOrWhiteSpace(_rewardLabel)
            ? _rewardCurrency == ShopCurrency.Hearts ? "Искры" : "Свечи"
            : _rewardLabel.Trim();

        return new ShopItemData
        {
            buttonId = buttonId,
            productId = productId,
            label = label,
            icon = _fallbackIcon,
            amount = rewardAmount,
            amountDisplay = ResolveAmountDisplay(_amountDisplay, rewardAmount),
            currency = _rewardCurrency,
            currencyLabel = rewardLabel,
            priceLabel = _priceLabel != null ? _priceLabel.Trim() : "",
            quantity = Mathf.Clamp(_orderQuantity <= 0 ? 1 : _orderQuantity, 1, 99),
            productType = _productType,
            sortOrder = _sortOrder,
            hasSortOrder = true
        };
    }

    public void Setup(ShopItemData data, Action<ShopItemData> onBuy, bool hasServerData)
    {
        ResolveReferences();
        BindButton();

        _currentData = data ?? BuildLocalData();
        _onBuy = onBuy;

        if (_hideWhenServerMissing && !hasServerData)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        ApplyDataToViews(_currentData, hasServerData);
        IDictionary<string, object> metadata = BuildLogMetadata("setup", "shop_build");
        metadata["hasServerData"] = hasServerData;
        AppLogger.Info(
            AppLogCategory.Shop,
            nameof(ShopProductButtonView),
            nameof(Setup),
            "[SHOP_BUTTON][SETUP] Shop product button view was configured.",
            metadata);
    }

    public int ResolveSortOrder(ShopItemData data)
    {
        if (_useServerSortOrder && data != null && data.hasSortOrder)
            return data.sortOrder;

        return _sortOrder;
    }

    public string BuildAdminPayloadJson()
    {
        ShopItemData data = BuildLocalData();
        var builder = new StringBuilder(256);
        builder.Append('{');
        AppendJsonString(builder, "buttonId", data.buttonId, false);
        AppendJsonString(builder, "productId", data.productId, true);
        AppendJsonString(builder, "name", data.label, true);
        AppendJsonInt(builder, "amount", data.amount, true);
        AppendJsonString(builder, "amountDisplay", data.amountDisplay, true);
        AppendJsonString(builder, "currency", data.currency.ToString(), true);
        AppendJsonString(builder, "currencyLabel", data.currencyLabel, true);
        AppendJsonString(builder, "priceLabel", data.priceLabel, true);
        AppendJsonInt(builder, "quantity", data.quantity, true);
        AppendJsonString(builder, "productType", data.productType.ToString(), true);
        AppendJsonInt(builder, "sortOrder", data.sortOrder, true);
        builder.Append('}');
        return builder.ToString();
    }

    [ContextMenu("Магазин/Вывести JSON для админки")]
    private void LogAdminProductPayload()
    {
        Debug.Log(BuildAdminPayloadJson(), this);
    }

    [ContextMenu("Магазин/Пересоздать ID кнопки")]
    private void RegenerateButtonId()
    {
        _buttonId = CreateButtonId();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void ApplyDataToViews(ShopItemData data, bool hasServerData)
    {
        string label = data != null ? data.label ?? "" : "";
        int amount = data != null ? data.amount : 0;
        string amountDisplay = ResolveAmountDisplay(data != null ? data.amountDisplay : "", amount);
        string currencyLabel = data != null ? data.currencyLabel ?? "" : "";
        string priceLabel = data != null ? data.priceLabel ?? "" : "";
        Sprite icon = data != null && data.icon != null ? data.icon : _fallbackIcon;
        bool hasProductId = data != null && !string.IsNullOrWhiteSpace(data.productId);

        if (_productIconImage != null)
        {
            if (icon != null)
            {
                if (!_productIconImage.gameObject.activeSelf)
                    _productIconImage.gameObject.SetActive(true);

                _productIconImage.sprite = icon;
            }
            else if (_hideIconWhenMissing)
            {
                _productIconImage.gameObject.SetActive(false);
            }
            else if (!_productIconImage.gameObject.activeSelf)
            {
                _productIconImage.gameObject.SetActive(true);
            }
        }

        SetText(_productNameText, label);
        SetText(_amountDisplayText, amountDisplay);
        SetText(_rewardLabelText, currencyLabel);
        SetText(_priceLabelText, priceLabel);
        SetText(_combinedProductLabelText, BuildCombinedLabel(label, amountDisplay, currencyLabel));

        if (_priceRoot != null)
            _priceRoot.SetActive(!string.IsNullOrWhiteSpace(priceLabel));
        if (_unavailableRoot != null)
            _unavailableRoot.SetActive(!hasProductId || (!hasServerData && _hideWhenServerMissing));
        if (_buyButton != null)
        {
            _buyButton.interactable = hasProductId || !_disableWhenProductIdMissing;
            if (!_buyButton.interactable)
            {
                AppLogger.Warn(
                    AppLogCategory.Shop,
                    nameof(ShopProductButtonView),
                    nameof(ApplyDataToViews),
                    "[SHOP_BUTTON][DISABLED] Shop product button was disabled because productId is missing.",
                    BuildLogMetadata("disabled_missing_product_id", "setup"),
                    recoverable: true);
            }
        }
    }

    private void ResolveReferences()
    {
        if (_buyButton == null)
            _buyButton = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);

        if (_productIconImage == null)
            _productIconImage = FindImageByName("Icon", "IconItem", "ProductIcon", "Product");

        if (_productNameText == null)
            _productNameText = FindTextByName("Name", "Title", "Label");
        if (_amountDisplayText == null)
            _amountDisplayText = FindTextByName("Amount", "Count", "Quantity");
        if (_rewardLabelText == null)
            _rewardLabelText = FindTextByName("Currency", "Reward");
        if (_priceLabelText == null)
            _priceLabelText = FindTextByName("Price", "Cost");
        if (_combinedProductLabelText == null)
            _combinedProductLabelText = FindTextByName("Combined", "Description");
    }

    private string ResolveButtonId()
    {
        string buttonId = SaveDataSanitizer.SanitizeIdentifier(_buttonId);
        if (!string.IsNullOrEmpty(buttonId))
            return buttonId;

        return ProductId;
    }

    private void BindButton()
    {
        if (_buyButton == null)
        {
            AppLogger.Warn(
                AppLogCategory.Shop,
                nameof(ShopProductButtonView),
                nameof(BindButton),
                "[SHOP_BUTTON][BIND_FAILED] Shop product view has no Button reference.",
                BuildLogMetadata("bind_failed", "none"),
                recoverable: true);
            return;
        }

        if (_boundButton != null && _boundButton != _buyButton && _listenerBound)
        {
            _boundButton.onClick.RemoveListener(HandleButtonClick);
            _listenerBound = false;
        }

        _buyButton.onClick.RemoveListener(HandleButtonClick);
        _buyButton.onClick.AddListener(HandleButtonClick);
        _boundButton = _buyButton;
        _listenerBound = true;
        BindClickProxy();
    }

    private void BindClickProxy()
    {
        if (_buyButton == null)
            return;

        _clickProxy = _buyButton.GetComponent<ShopProductButtonClickProxy>();
        if (_clickProxy == null)
            _clickProxy = _buyButton.gameObject.AddComponent<ShopProductButtonClickProxy>();

        _clickProxy.Bind(this);
    }

    private void HandleButtonClick()
    {
        DispatchClick("button_onClick");
    }

    internal void HandlePointerProxyClick(PointerEventData eventData)
    {
        DispatchClick("pointer_proxy");
    }

    private void DispatchClick(string source)
    {
        if (_lastHandledClickFrame == Time.frameCount)
            return;

        _lastHandledClickFrame = Time.frameCount;

        if (_buyButton != null && (!_buyButton.enabled || !_buyButton.interactable || !_buyButton.gameObject.activeInHierarchy))
        {
            AppLogger.Warn(
                AppLogCategory.Shop,
                nameof(ShopProductButtonView),
                nameof(DispatchClick),
                "[SHOP_BUTTON][CLICK_BLOCKED] Shop product button click was blocked by Button state.",
                BuildLogMetadata("click_blocked", source),
                recoverable: true);
            return;
        }

        if (_currentData == null)
            _currentData = BuildLocalData();

        AppLogger.Info(
            AppLogCategory.Shop,
            nameof(ShopProductButtonView),
            nameof(DispatchClick),
            "[SHOP_BUTTON][CLICK] Shop product button click reached view script.",
            BuildLogMetadata("click", source));

        if (_onBuy != null)
        {
            _onBuy.Invoke(_currentData);
            return;
        }

        if (ShopController.Instance != null)
        {
            AppLogger.Warn(
                AppLogCategory.Shop,
                nameof(ShopProductButtonView),
                nameof(DispatchClick),
                "[SHOP_BUTTON][CLICK_FALLBACK] Product view was not bound by BuildShop; routing click through ShopController.Instance.",
                BuildLogMetadata("click_fallback", source),
                recoverable: true);
            ShopController.Instance.BuyFromProductButtonView(_currentData, this, source);
            return;
        }

        AppLogger.Error(
            AppLogCategory.Shop,
            nameof(ShopProductButtonView),
            nameof(DispatchClick),
            "[SHOP_BUTTON][CLICK_DROPPED] Product click has no onBuy callback and no ShopController.Instance.",
            metadata: BuildLogMetadata("click_dropped", source));
    }

    private IDictionary<string, object> BuildLogMetadata(string reason, string source)
    {
        ShopItemData data = _currentData ?? BuildLocalData();
        return LogMetadata.Of(
            "reason", reason ?? "",
            "source", source ?? "",
            "viewObject", name,
            "viewPath", GetHierarchyPath(transform),
            "buttonId", data != null ? data.buttonId ?? "" : "",
            "productId", data != null ? data.productId ?? "" : "",
            "label", data != null ? data.label ?? "" : "",
            "amount", data != null ? data.amount : 0,
            "amountDisplay", data != null ? data.amountDisplay ?? "" : "",
            "currency", data != null ? data.currency.ToString() : "",
            "hasCurrentData", _currentData != null,
            "hasOnBuyCallback", _onBuy != null,
            "buttonAssigned", _buyButton != null,
            "boundButtonAssigned", _boundButton != null,
            "listenerBound", _listenerBound,
            "buttonInteractable", _buyButton != null && _buyButton.interactable,
            "buttonActiveInHierarchy", _buyButton != null && _buyButton.gameObject.activeInHierarchy,
            "viewActiveInHierarchy", gameObject.activeInHierarchy,
            "frame", Time.frameCount);
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
            return "";

        var builder = new StringBuilder(128);
        Transform current = target;
        while (current != null)
        {
            if (builder.Length == 0)
                builder.Insert(0, current.name);
            else
                builder.Insert(0, current.name + "/");

            current = current.parent;
        }

        return builder.ToString();
    }

    private Image FindImageByName(params string[] names)
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int n = 0; n < names.Length; n++)
        {
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null && ContainsName(image.name, names[n]))
                    return image;
            }
        }

        return null;
    }

    private TMP_Text FindTextByName(params string[] names)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int n = 0; n < names.Length; n++)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null && ContainsName(text.name, names[n]))
                    return text;
            }
        }

        return null;
    }

    private void RefreshEditorPreview()
    {
        int rewardAmount = Mathf.Max(0, _rewardAmount);
        string amountDisplay = ResolveAmountDisplay(_amountDisplay, rewardAmount);
        string rewardLabel = string.IsNullOrWhiteSpace(_rewardLabel)
            ? _rewardCurrency == ShopCurrency.Hearts ? "Искры" : "Свечи"
            : _rewardLabel.Trim();

        SetText(_productNameText, _productName);
        SetText(_amountDisplayText, amountDisplay);
        SetText(_rewardLabelText, rewardLabel);
        SetText(_priceLabelText, _priceLabel);
        SetText(_combinedProductLabelText, BuildCombinedLabel(_productName, amountDisplay, rewardLabel));

        if (_priceRoot != null)
            _priceRoot.SetActive(!string.IsNullOrWhiteSpace(_priceLabel));
    }

    private static string ResolveAmountDisplay(string configuredDisplay, int amount)
    {
        if (!string.IsNullOrWhiteSpace(configuredDisplay))
            return configuredDisplay.Trim();

        return amount > 0 ? "x" + amount : "";
    }

    private static string BuildCombinedLabel(string label, string amountDisplay, string currencyLabel)
    {
        label = label ?? "";
        amountDisplay = amountDisplay ?? "";
        currencyLabel = currencyLabel ?? "";

        if (string.IsNullOrWhiteSpace(label))
            return string.IsNullOrWhiteSpace(currencyLabel)
                ? amountDisplay
                : amountDisplay + " " + currencyLabel;

        if (string.IsNullOrWhiteSpace(amountDisplay))
            return label;

        return string.IsNullOrWhiteSpace(currencyLabel)
            ? label + " " + amountDisplay
            : label + " " + amountDisplay + " " + currencyLabel;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? "";
    }

    private static bool ContainsName(string source, string token)
    {
        return !string.IsNullOrEmpty(source)
            && !string.IsNullOrEmpty(token)
            && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void AppendJsonString(StringBuilder builder, string key, string value, bool comma)
    {
        if (comma)
            builder.Append(',');

        builder.Append('"').Append(EscapeJson(key)).Append("\":\"").Append(EscapeJson(value)).Append('"');
    }

    private static void AppendJsonInt(StringBuilder builder, string key, int value, bool comma)
    {
        if (comma)
            builder.Append(',');

        builder.Append('"').Append(EscapeJson(key)).Append("\":").Append(value);
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string CreateButtonId()
    {
        return "shop_button_" + Guid.NewGuid().ToString("N").Substring(0, 12);
    }

#if UNITY_EDITOR
    private bool HasDuplicateButtonIdInScene(string buttonId)
    {
        if (string.IsNullOrWhiteSpace(buttonId) || UnityEditor.EditorUtility.IsPersistent(this))
            return false;

        ShopProductButtonView[] views = Resources.FindObjectsOfTypeAll<ShopProductButtonView>();
        for (int i = 0; i < views.Length; i++)
        {
            ShopProductButtonView view = views[i];
            if (view == null || view == this || UnityEditor.EditorUtility.IsPersistent(view))
                continue;
            if (view.gameObject.scene != gameObject.scene)
                continue;
            if (string.Equals(view._buttonId, buttonId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
#endif
}

[DisallowMultipleComponent]
sealed class ShopProductButtonClickProxy : MonoBehaviour, IPointerClickHandler
{
    private ShopProductButtonView _owner;

    public void Bind(ShopProductButtonView owner)
    {
        _owner = owner;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _owner?.HandlePointerProxyClick(eventData);
    }
}
