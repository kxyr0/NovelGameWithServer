using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PremiumChoiceBalancePanelView : MonoBehaviour
{
    [Header("Платный выбор: баланс")]
    [SerializeField, InspectorName("Текст текущего баланса")]
    [Tooltip("TMP_Text внутри prefab-панели. Сюда будет записано текущее количество сердец/искр игрока.")]
    private TMP_Text balanceText;

    [SerializeField, InspectorName("Иконка валюты")]
    [Tooltip("Image иконки валюты внутри prefab-панели. Спрайт и внешний вид назначаются вручную в prefab.")]
    private Image currencyIcon;

    [SerializeField, InspectorName("Формат текста баланса")]
    [Tooltip("Формат текста. {0} заменяется текущим балансом. Для одного числа оставьте {0}.")]
    private string balanceTextFormat = "{0}";

    public void SetBalance(int balance)
    {
        if (balanceText == null)
            return;

        balance = SaveDataSanitizer.ClampCurrencyValue(balance);
        string format = string.IsNullOrWhiteSpace(balanceTextFormat) ? "{0}" : balanceTextFormat;

        try
        {
            balanceText.text = string.Format(format, balance);
        }
        catch (System.FormatException)
        {
            balanceText.text = balance.ToString();
        }
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);

        if (balanceText != null && balanceText.gameObject.activeSelf != visible)
            balanceText.gameObject.SetActive(visible);

        if (currencyIcon != null && currencyIcon.gameObject.activeSelf != visible)
            currencyIcon.gameObject.SetActive(visible);
    }
}
