using System;

public static class ChoiceRegionFilter
{
    public static bool IsVisible(ChoiceOption option)
    {
        return !IsHiddenForCurrentRegion(option);
    }

    public static bool IsHiddenForCurrentRegion(ChoiceOption option)
    {
        if (option == null)
            return false;

        if (!option.hideInRestrictedRegions &&
            (option.hiddenRegionCodes == null || option.hiddenRegionCodes.Count == 0))
        {
            return false;
        }

        if (RegionAccessGate.HasActiveDeveloperBypass())
            return false;

        if (IsRegionSensitive(option) && RegionAccessGate.ShouldHideRegionSensitiveChoicesWithoutResolvedIp())
            return true;

        string regionCode = RegionAccessGate.GetCurrentRegionCode();
        if (string.IsNullOrWhiteSpace(regionCode))
            return false;

        if (option.hideInRestrictedRegions && RegionAccessGate.IsRestrictedRegionCode(regionCode))
            return true;

        if (option.hiddenRegionCodes == null)
            return false;

        for (int i = 0; i < option.hiddenRegionCodes.Count; i++)
        {
            if (string.Equals(
                    RegionAccessGate.NormalizeRegionCode(option.hiddenRegionCodes[i]),
                    RegionAccessGate.NormalizeRegionCode(regionCode),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasRegionSensitiveOptions(ChoiceNode node)
    {
        if (node == null || node.options == null)
            return false;

        for (int i = 0; i < node.options.Count; i++)
        {
            if (IsRegionSensitive(node.options[i]))
                return true;
        }

        return false;
    }

    public static int CountVisibleOptions(ChoiceNode node)
    {
        if (node == null || node.options == null)
            return 0;

        int count = 0;
        for (int i = 0; i < node.options.Count; i++)
        {
            if (IsVisible(node.options[i]))
                count++;
        }

        return count;
    }

    private static bool IsRegionSensitive(ChoiceOption option)
    {
        return option != null &&
               (option.hideInRestrictedRegions ||
                (option.hiddenRegionCodes != null && option.hiddenRegionCodes.Count > 0));
    }
}
