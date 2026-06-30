using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DivinationTarotTestCard
{
    [SerializeField]
    [Tooltip("Тестовый ID карты. Нужен только для логов и отладки.")]
    private string _id = "test_card";

    [SerializeField]
    [Tooltip("Название тестовой карты, которое можно вывести в TMP_Text.")]
    private string _name = "Тестовая карта";

    [SerializeField, TextArea(2, 8)]
    [Tooltip("Описание тестовой карты. Можно оставить пустым.")]
    private string _description;

    [SerializeField]
    [Tooltip("Спрайт карты для теста без админки и без сервера.")]
    private Sprite _sprite;

    [SerializeField, Min(0)]
    [Tooltip("Сколько искр/сердечек показывает тестовая награда. В тестовом режиме это только UI-данные, серверный баланс не меняется.")]
    private int _heartsReward;

    [SerializeField, Min(0)]
    [Tooltip("Сколько свечей показывает тестовая награда. В тестовом режиме это только UI-данные, серверный баланс не меняется.")]
    private int _candlesReward;

    [SerializeField, Min(0f)]
    [Tooltip("Вес выпадения тестовой карты. 0 исключает карту из случайного выбора.")]
    private float _weight = 1f;

    public string Id => SaveDataSanitizer.SafeKeyPart(_id, "test_card", 64);
    public string Name => _name ?? "";
    public string Description => _description ?? "";
    public Sprite Sprite => _sprite;
    public int HeartsReward => SaveDataSanitizer.ClampCurrencyValue(_heartsReward);
    public int CandlesReward => SaveDataSanitizer.ClampCurrencyValue(_candlesReward);
    public float Weight => Mathf.Max(0f, _weight);
    public bool IsValid => _sprite != null && Weight > 0f;
}

[CreateAssetMenu(fileName = "Divination Tarot Test Deck", menuName = "Nocturne/UI/Divination Tarot Test Deck")]
public sealed class DivinationTarotTestDeckConfig : ScriptableObject
{
    [SerializeField]
    [Tooltip("Локальные тестовые карты для проверки Divination UI без админки, сервера и опубликованных tarot-карт.")]
    private List<DivinationTarotTestCard> _cards = new List<DivinationTarotTestCard>();

    public IReadOnlyList<DivinationTarotTestCard> Cards => _cards ?? EmptyCards;

    private static readonly IReadOnlyList<DivinationTarotTestCard> EmptyCards = Array.Empty<DivinationTarotTestCard>();

    public DivinationTarotTestCard PickRandom()
    {
        IReadOnlyList<DivinationTarotTestCard> cards = Cards;
        float totalWeight = 0f;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && cards[i].IsValid)
                totalWeight += cards[i].Weight;
        }

        if (totalWeight <= 0f)
            return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < cards.Count; i++)
        {
            DivinationTarotTestCard card = cards[i];
            if (card == null || !card.IsValid)
                continue;

            roll -= card.Weight;
            if (roll <= 0f)
                return card;
        }

        for (int i = cards.Count - 1; i >= 0; i--)
        {
            if (cards[i] != null && cards[i].IsValid)
                return cards[i];
        }

        return null;
    }

    private void OnValidate()
    {
        _cards ??= new List<DivinationTarotTestCard>();
    }
}
