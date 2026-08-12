using UnityEngine;

public sealed class HoverRevealPanelInteractivity
{
    private CanvasGroup _canvasGroup;

    public void Bind(RectTransform panelRoot)
    {
        _canvasGroup = null;
        if (panelRoot == null)
            return;

        _canvasGroup = panelRoot.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = panelRoot.gameObject.AddComponent<CanvasGroup>();
    }

    public void ApplyOpened()
    {
        Apply(true, true);
    }

    public void ApplyClosed()
    {
        Apply(false, false);
    }

    public void ApplyOpening()
    {
        Apply(false, true);
    }

    public void ApplyClosing()
    {
        Apply(false, false);
    }

    private void Apply(bool interactable, bool blocksRaycasts)
    {
        if (_canvasGroup == null)
            return;

        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = interactable;
        _canvasGroup.blocksRaycasts = blocksRaycasts;
    }
}
