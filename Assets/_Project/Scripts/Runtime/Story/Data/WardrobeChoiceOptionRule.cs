using System;
using System.Collections.Generic;

[Serializable]
public sealed class WardrobeChoiceOptionRule
{
    public int premiumCost;
    public string requiredVariable;
    public int requiredValue;
    public string requiredItemId;
    public bool hideInRestrictedRegions;
    public List<string> hiddenRegionCodes = new List<string>();
    public string purchaseKey;
    public string unavailableMessage;

    public int GetPremiumCost()
    {
        return SaveDataSanitizer.ClampCurrencyValue(premiumCost);
    }

    public bool HasAnyRule()
    {
        return GetPremiumCost() > 0 ||
            !string.IsNullOrWhiteSpace(requiredVariable) ||
            !string.IsNullOrWhiteSpace(requiredItemId) ||
            hideInRestrictedRegions ||
            (hiddenRegionCodes != null && hiddenRegionCodes.Count > 0) ||
            !string.IsNullOrWhiteSpace(purchaseKey) ||
            !string.IsNullOrWhiteSpace(unavailableMessage);
    }

    public bool IsVisible()
    {
        if (!IsRegionSensitive())
            return true;

        if (RegionAccessGate.HasActiveDeveloperBypass())
            return true;

        if (RegionAccessGate.ShouldHideRegionSensitiveChoicesWithoutResolvedIp())
            return false;

        string regionCode = RegionAccessGate.GetCurrentRegionCode();
        if (string.IsNullOrWhiteSpace(regionCode))
            return true;

        if (hideInRestrictedRegions && RegionAccessGate.IsRestrictedRegionCode(regionCode))
            return false;

        if (hiddenRegionCodes == null)
            return true;

        string normalizedRegion = RegionAccessGate.NormalizeRegionCode(regionCode);
        for (int i = 0; i < hiddenRegionCodes.Count; i++)
        {
            if (string.Equals(
                    RegionAccessGate.NormalizeRegionCode(hiddenRegionCodes[i]),
                    normalizedRegion,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public bool CanSelect(GameState state, out string message)
    {
        message = "";

        if (!IsVisible())
        {
            message = GetUnavailableMessage();
            return false;
        }

        if (!string.IsNullOrWhiteSpace(requiredVariable))
        {
            int currentValue = state != null ? state.GetInt(requiredVariable) : 0;
            if (currentValue < requiredValue)
            {
                message = GetUnavailableMessage();
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(requiredItemId))
        {
            if (state == null || !state.HasClothing(requiredItemId))
            {
                message = GetUnavailableMessage();
                return false;
            }
        }

        return true;
    }

    public string GetServerPurchaseKey(string nodeGuid, int index, string itemId)
    {
        if (!string.IsNullOrWhiteSpace(purchaseKey))
            return SaveDataSanitizer.SanitizeIdentifier(purchaseKey);

        nodeGuid = SaveDataSanitizer.SanitizeIdentifier(nodeGuid);
        itemId = SaveDataSanitizer.SanitizeIdentifier(itemId);
        if (!string.IsNullOrEmpty(nodeGuid) && !string.IsNullOrEmpty(itemId))
            return nodeGuid + ":" + index + ":" + itemId;

        if (!string.IsNullOrEmpty(nodeGuid))
            return nodeGuid + ":" + index;

        return itemId;
    }

    string GetUnavailableMessage()
    {
        return !string.IsNullOrWhiteSpace(unavailableMessage)
            ? unavailableMessage
            : "\u0412\u0430\u0440\u0438\u0430\u043d\u0442 \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u0435\u043d";
    }

    bool IsRegionSensitive()
    {
        return hideInRestrictedRegions || (hiddenRegionCodes != null && hiddenRegionCodes.Count > 0);
    }
}
