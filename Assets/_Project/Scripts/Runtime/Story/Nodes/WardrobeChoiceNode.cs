using System;
using System.Collections.Generic;
using UnityEngine;
using XNode;

public class WardrobeChoiceNode : BaseStoryNode
{
    public string characterId;
    public CharacterData character;

    public List<ClothingItem> availableClothes = new List<ClothingItem>();
    public List<int> premiumCosts = new List<int>();
    public List<WardrobeChoiceOptionRule> optionRules = new List<WardrobeChoiceOptionRule>();

    [Output(dynamicPortList = true)]
    public List<BaseStoryNode> exits;

    public int GetPremiumCost(int index)
    {
        WardrobeChoiceOptionRule rule = GetOptionRule(index);
        if (rule != null)
        {
            int ruleCost = rule.GetPremiumCost();
            if (ruleCost > 0)
                return ruleCost;
        }

        int configuredCost = GetConfiguredPremiumCost(index);
        if (configuredCost > 0)
            return configuredCost;

        return GetFallbackPremiumCost(index);
    }

    public WardrobeChoiceOptionRule GetOptionRule(int index)
    {
        if (optionRules == null || index < 0 || index >= optionRules.Count)
            return null;

        return optionRules[index];
    }

    public bool IsOptionVisible(int index)
    {
        WardrobeChoiceOptionRule rule = GetOptionRule(index);
        return rule == null || rule.IsVisible();
    }

    public bool CanSelectOption(int index, GameState state, out string message)
    {
        message = "";
        WardrobeChoiceOptionRule rule = GetOptionRule(index);
        return rule == null || rule.CanSelect(state, out message);
    }

    public string GetServerPurchaseKey(int index)
    {
        ClothingItem item = availableClothes != null && index >= 0 && index < availableClothes.Count
            ? availableClothes[index]
            : null;
        string itemId = item != null ? item.id : "";

        WardrobeChoiceOptionRule rule = GetOptionRule(index);
        if (rule != null)
            return rule.GetServerPurchaseKey(guid, index, itemId);

        itemId = SaveDataSanitizer.SanitizeIdentifier(itemId);
        string nodeGuid = SaveDataSanitizer.SanitizeIdentifier(guid);
        if (!string.IsNullOrEmpty(nodeGuid) && !string.IsNullOrEmpty(itemId))
            return nodeGuid + ":" + index + ":" + itemId;

        return nodeGuid;
    }

    int GetConfiguredPremiumCost(int index)
    {
        if (premiumCosts == null || index < 0 || index >= premiumCosts.Count)
            return 0;

        int cost = premiumCosts[index];
        if (cost <= 0)
            return 0;

        return Mathf.Min(cost, SaveDataSanitizer.MaxCurrencyValue);
    }

    int GetFallbackPremiumCost(int index)
    {
        if (availableClothes == null || index < 0 || index >= availableClothes.Count)
            return 0;

        if (string.IsNullOrEmpty(guid))
            return 0;

        ClothingItem item = availableClothes[index];
        string itemId = item != null ? SaveDataSanitizer.SanitizeIdentifier(item.id).ToLowerInvariant() : "";
        if (string.IsNullOrEmpty(itemId))
            return 0;

        if (string.Equals(guid, "zls2_wardrobe_001_outfit", StringComparison.OrdinalIgnoreCase))
        {
            if (itemId == "mestnaya)obolstitelnitsa")
                return 15;

            if (itemId == "devitsa_krasa")
                return 25;
        }

        if (string.Equals(guid, "zls2_wardrobe_002_hair", StringComparison.OrdinalIgnoreCase))
        {
            if (itemId.StartsWith("hair_bun_", StringComparison.OrdinalIgnoreCase))
                return 15;

            if (itemId.StartsWith("hair_braid_", StringComparison.OrdinalIgnoreCase))
                return 25;
        }

        return 0;
    }
}
