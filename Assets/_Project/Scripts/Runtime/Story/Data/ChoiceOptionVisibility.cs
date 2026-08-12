using System;

public static class ChoiceOptionVisibility
{
    public static bool IsVisible(ChoiceOption option)
    {
        if (!ChoiceRegionFilter.IsVisible(option))
            return false;

        if (option == null || !option.hideWhenRequirementNotMet || string.IsNullOrEmpty(option.requiredVariable))
            return true;

        return GameState.Instance == null ||
               GameState.Instance.GetInt(option.requiredVariable) >= option.requiredValue;
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
}
