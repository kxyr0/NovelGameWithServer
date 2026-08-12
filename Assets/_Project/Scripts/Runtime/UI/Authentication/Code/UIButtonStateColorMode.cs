using UnityEngine;
using UnityEngine.UI;

public enum UIButtonStateColorMode
{
    ButtonColorTint,
    TargetGraphic
}

public static class UIButtonStateColor
{
    public static void Apply(
        Button button,
        bool ready,
        Color readyColor,
        Color disabledColor,
        UIButtonStateColorMode mode)
    {
        if (button == null)
            return;

        if (mode == UIButtonStateColorMode.TargetGraphic)
        {
            button.transition = Selectable.Transition.None;
            button.interactable = ready;
            if (button.targetGraphic != null)
                button.targetGraphic.color = ready ? readyColor : disabledColor;
            return;
        }

        button.transition = Selectable.Transition.ColorTint;
        if (button.targetGraphic != null)
            button.targetGraphic.color = Color.white;
        ColorBlock colors = button.colors;
        colors.normalColor = readyColor;
        colors.highlightedColor = readyColor;
        colors.selectedColor = readyColor;
        colors.pressedColor = readyColor;
        colors.disabledColor = disabledColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.interactable = ready;
    }
}
