using UnityEngine;

public class SubscriptionManager : MonoBehaviour
{
    public static SubscriptionManager Instance;

    const string SubscriptionKey = "VN_SUBSCRIPTION";

    [Header("Разработка")]
    [Tooltip("Открывать функции подписки в редакторе и development-сборках без реальной покупки. В релизной сборке эта настройка не используется.")]
    public bool unlockAllInEditor = true;

    bool _hasActiveSubscription;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Instance = null;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _hasActiveSubscription = SafeGetSubscriptionFlag();
#else
        _hasActiveSubscription = false;
#endif
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool Has(SubscriptionFeature feature)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (unlockAllInEditor) return true;
        return _hasActiveSubscription;
#else
        return false;
#endif
    }

    public void Activate()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _hasActiveSubscription = true;
        SafeSaveSubscriptionFlag(true);
        Debug.Log("[Subscription] Activated");
#else
        Debug.LogWarning("[Subscription] Local subscription activation is disabled in release builds.");
#endif
    }

    public void Deactivate()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _hasActiveSubscription = false;
        SafeSaveSubscriptionFlag(false);
        Debug.Log("[Subscription] Deactivated");
#else
        Debug.LogWarning("[Subscription] Local subscription deactivation is disabled in release builds.");
#endif
    }

    static bool SafeGetSubscriptionFlag()
    {
        try
        {
            return LocalSecurePrefs.GetBool(SubscriptionKey, LocalSaveSecurity.SubscriptionPurpose, false);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[Subscription] Failed to load subscription flag: " + exception.Message);
            return false;
        }
    }

    static void SafeSaveSubscriptionFlag(bool active)
    {
        try
        {
            LocalSecurePrefs.SetBool(SubscriptionKey, LocalSaveSecurity.SubscriptionPurpose, active);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[Subscription] Failed to save subscription flag: " + exception.Message);
        }
    }
}

public enum SubscriptionFeature
{
    FastForward,
    Bookmarks,
}
