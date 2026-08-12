using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Profile Collection Card Collector")]
public sealed class PlayerCollectionCardCollector : MonoBehaviour
{
    [SerializeField] private bool _collectLocalTestCards;

    public void Collect(DivinationTarotCardRuntimeData card)
    {
        if (card == null)
        {
            Debug.LogWarning(
                "PlayerCollectionCardCollector: prepared card is null.",
                this);
            return;
        }

        if (!card.fromServer && !_collectLocalTestCards)
            return;

        CollectById(card.id);
    }

    public void CollectById(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
        {
            Debug.LogWarning(
                "PlayerCollectionCardCollector: card id is empty.",
                this);
            return;
        }

        PlayerCollectionState.GrantCard(cardId);
    }
}
