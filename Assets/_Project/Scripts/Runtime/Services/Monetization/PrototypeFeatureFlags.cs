using UnityEngine;

public static class PrototypeFeatureFlags
{
    const string KEY_SHOP_CURRENCY_GRANTS = "VN_PROTO_SHOP_CURRENCY_GRANTS";
    const string KEY_LOCAL_PREMIUM_SPEND = "VN_PROTO_LOCAL_PREMIUM_SPEND";
    const string KEY_DEV_CURRENCY_TOOLS = "VN_PROTO_DEV_CURRENCY_TOOLS";
    const string KEY_REMOTE_EPISODE_GRAPHS = "VN_PROTO_REMOTE_EPISODE_GRAPHS";

    public static bool ShopCurrencyGrantsEnabled => GetDangerousPrototypeBool(KEY_SHOP_CURRENCY_GRANTS, DefaultPrototypeEnabled);
    public static bool LocalPremiumSpendEnabled => GetDangerousPrototypeBool(KEY_LOCAL_PREMIUM_SPEND, DefaultPrototypeEnabled);
    public static bool DevCurrencyToolsEnabled => GetDangerousPrototypeBool(KEY_DEV_CURRENCY_TOOLS, DefaultPrototypeEnabled);
    // Remote story graphs must be enabled explicitly. Development Build no longer enables them by default.
    public static bool RemoteEpisodeGraphsEnabled => GetBool(KEY_REMOTE_EPISODE_GRAPHS, false);

    public static void SetShopCurrencyGrantsEnabled(bool enabled) => SetDangerousPrototypeBool(KEY_SHOP_CURRENCY_GRANTS, enabled);
    public static void SetLocalPremiumSpendEnabled(bool enabled) => SetDangerousPrototypeBool(KEY_LOCAL_PREMIUM_SPEND, enabled);
    public static void SetDevCurrencyToolsEnabled(bool enabled) => SetDangerousPrototypeBool(KEY_DEV_CURRENCY_TOOLS, enabled);
    public static void SetRemoteEpisodeGraphsEnabled(bool enabled) => SetBool(KEY_REMOTE_EPISODE_GRAPHS, enabled);

    static bool DefaultPrototypeEnabled
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }

    static bool DangerousPrototypeFlagsAllowed
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }

    static bool GetDangerousPrototypeBool(string key, bool defaultValue)
    {
        return DangerousPrototypeFlagsAllowed && GetBool(key, defaultValue);
    }

    static void SetDangerousPrototypeBool(string key, bool enabled)
    {
        if (!DangerousPrototypeFlagsAllowed)
        {
            Debug.LogWarning($"PrototypeFeatureFlags: '{key}' is locked off in release builds.");
            return;
        }

        SetBool(key, enabled);
    }

    static bool GetBool(string key, bool defaultValue)
    {
        try
        {
            return LocalSecurePrefs.GetBool(key, LocalSaveSecurity.PrototypeFlagPurpose, defaultValue);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"PrototypeFeatureFlags: failed to load '{key}': {exception.Message}");
            return defaultValue;
        }
    }

    static void SetBool(string key, bool enabled)
    {
        try
        {
            LocalSecurePrefs.SetBool(key, LocalSaveSecurity.PrototypeFlagPurpose, enabled);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"PrototypeFeatureFlags: failed to save '{key}': {exception.Message}");
        }
    }
}
