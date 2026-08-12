using UnityEngine;

public sealed class SubscriptionEntitlementSerializer
{
    public string ToJson(CachedSubscriptionEntitlement cache)
    {
        return cache == null ? "" : JsonUtility.ToJson(cache, false);
    }

    public bool TryFromJson(string json, out CachedSubscriptionEntitlement cache)
    {
        cache = null;
        if (string.IsNullOrWhiteSpace(json) || json.Length > SaveDataSanitizer.MaxSerializedChars)
            return false;

        try
        {
            cache = JsonUtility.FromJson<CachedSubscriptionEntitlement>(json);
            return cache != null && cache.schemaVersion >= 1 && cache.entitlement != null;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[Subscription] Failed to parse entitlement cache: " + exception.Message);
            cache = null;
            return false;
        }
    }
}
