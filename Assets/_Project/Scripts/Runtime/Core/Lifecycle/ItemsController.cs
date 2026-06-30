using TMPro;
using UnityEngine;

/// <summary>
/// Контроллер отображения валюты.
/// Если в сцене есть CurrencyBar — делегирует обновление ему (с анимацией).
/// Иначе — обновляет TMP_Text напрямую (старое поведение).
/// </summary>
public class ItemsController : MonoBehaviour
{
    [Header("Прямые ссылки (fallback если нет CurrencyBar)")]
    public TMP_Text candles;
    public TMP_Text hearts;

    public static ItemsController Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Instance = null;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetCandles(int count)
    {
        count = Mathf.Max(0, count);

        if (CurrencyBar.Instance != null)
        {
            CurrencyBar.Instance.Refresh();
            return;
        }

        if (candles != null) candles.text = count.ToString();
    }

    public void SetHearts(int count)
    {
        count = Mathf.Max(0, count);

        if (CurrencyBar.Instance != null)
        {
            CurrencyBar.Instance.Refresh();
            return;
        }

        if (hearts != null) hearts.text = count.ToString();
    }
}
