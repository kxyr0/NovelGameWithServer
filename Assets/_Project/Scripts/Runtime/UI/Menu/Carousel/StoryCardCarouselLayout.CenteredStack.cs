using UnityEngine;

public sealed partial class StoryCardCarouselLayout
{
    [Header("Centered Card Stack")]
    [SerializeField, Tooltip("Keeps cards upright in one centered stack.")]
    private bool _centeredStackEnabled = true;

    [SerializeField, Min(0f), Tooltip("Horizontal peek of the cards behind the selected card.")]
    private float _sidePeekDistance = 260f;

    [SerializeField, Tooltip("Shared vertical position for every card in the stack.")]
    private float _stackCenterY;

    [SerializeField, Tooltip("Scale of the selected card.")]
    private Vector2 _selectedStackScale = new Vector2(0.6f, 0.6f);

    [SerializeField, Tooltip("Scale of cards behind the selected card.")]
    private Vector2 _unselectedStackScale = new Vector2(0.4f, 0.4f);

    private void ApplyCenteredStackPresentation(
        int slotOffset,
        bool selected,
        ref Vector2 anchoredPosition,
        ref float rotationZ,
        ref Vector3 scale)
    {
        if (!_centeredStackEnabled)
            return;

        float x = selected
            ? 0f
            : Mathf.Sign(slotOffset) * Mathf.Max(0f, _sidePeekDistance);
        Vector2 targetScale = selected
            ? NormalizeStackScale(_selectedStackScale, 0.6f)
            : NormalizeStackScale(_unselectedStackScale, 0.4f);

        anchoredPosition = new Vector2(x, _stackCenterY);
        rotationZ = 0f;
        scale = new Vector3(targetScale.x, targetScale.y, 1f);
    }

    private void ValidateCenteredStackPresentation()
    {
        _sidePeekDistance = Mathf.Max(0f, _sidePeekDistance);
        _selectedStackScale = NormalizeStackScale(_selectedStackScale, 0.6f);
        _unselectedStackScale = NormalizeStackScale(_unselectedStackScale, 0.4f);
    }

    private static Vector2 NormalizeStackScale(Vector2 value, float fallback)
    {
        return new Vector2(
            value.x > 0f ? value.x : fallback,
            value.y > 0f ? value.y : fallback);
    }
}
