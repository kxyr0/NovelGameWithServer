using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Карточка товара в магазине.
///
/// Префаб "ShopItem":
///   ├── IconImage     — Image иконки
///   ├── LabelText     — TMP_Text ("50 Сердец")
///   ├── PriceText     — TMP_Text ("99 ₽")
///   └── BuyButton     — Button
/// </summary>
public class ShopItemView : MonoBehaviour
{
    [Header("Ссылки")]
    public Image iconImage;
    public TMP_Text labelText;
    public TMP_Text priceText;
    public Button buyButton;

    ShopItemData _data;
    Action<ShopItemData> _onBuy;

    void Start()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(() => _onBuy?.Invoke(_data));
    }

    public void Setup(ShopItemData data, Action<ShopItemData> onBuy)
    {
        _data = data;
        _onBuy = onBuy;

        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(data != null && data.icon != null);
            if (data != null && data.icon != null) iconImage.sprite = data.icon;
        }

        if (labelText != null) labelText.text = data != null ? data.label : "";
        if (priceText != null) priceText.text = data != null ? data.priceLabel : "";
    }
}
