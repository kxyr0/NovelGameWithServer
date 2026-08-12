using UnityEngine;

public enum HoverRevealPanelInputMode
{
    Auto,
    DesktopHover,
    MobileClick
}

public enum HoverRevealPanelInputRole
{
    Trigger,
    Panel
}

public static class HoverRevealPanelInputModeExtensions
{
    public static bool UsesDesktopHover(this HoverRevealPanelInputMode mode)
    {
        return mode == HoverRevealPanelInputMode.DesktopHover ||
               mode == HoverRevealPanelInputMode.Auto && !Application.isMobilePlatform;
    }

    public static bool UsesMobileClick(this HoverRevealPanelInputMode mode)
    {
        return mode == HoverRevealPanelInputMode.MobileClick ||
               mode == HoverRevealPanelInputMode.Auto && Application.isMobilePlatform;
    }
}
