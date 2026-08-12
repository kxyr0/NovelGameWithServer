using System;
using System.Globalization;

public sealed class SubscriptionEntitlementEvaluator
{
    readonly SubscriptionFeatureConfig _config;
    readonly ISubscriptionClock _clock;
    readonly ISubscriptionSignatureVerifier _verifier;

    public SubscriptionEntitlementEvaluator(
        SubscriptionFeatureConfig config,
        ISubscriptionClock clock,
        ISubscriptionSignatureVerifier verifier)
    {
        _config = config;
        _clock = clock;
        _verifier = verifier;
    }

    public SubscriptionEntitlementResult EvaluateOnline(SubscriptionEntitlement entitlement)
    {
        if (!FeaturesEnabled())
            return SubscriptionEntitlementResult.From(SubscriptionFeatureState.Disabled);
        if (!IsEntitlementTrusted(entitlement))
            return SubscriptionEntitlementResult.From(SubscriptionFeatureState.Unavailable, null, "Entitlement signature is not trusted.");
        if (!IsStatusActive(entitlement.status))
            return SubscriptionEntitlementResult.From(SubscriptionFeatureState.Expired, entitlement);
        if (!TryParseUtc(entitlement.expiresAtUtc, out DateTime expiresAt))
            return SubscriptionEntitlementResult.From(SubscriptionFeatureState.Unavailable, null, "Invalid entitlement expiration.");
        if (_clock.UtcNow > expiresAt)
            return SubscriptionEntitlementResult.From(SubscriptionFeatureState.Expired, entitlement);
        return SubscriptionEntitlementResult.From(SubscriptionFeatureState.ActiveOnline, entitlement);
    }

    public SubscriptionEntitlementResult EvaluateCache(CachedSubscriptionEntitlement cache)
    {
        if (!FeaturesEnabled())
            return SubscriptionEntitlementResult.From(SubscriptionFeatureState.Disabled);
        if (cache == null || cache.entitlement == null)
            return SubscriptionEntitlementResult.From(SubscriptionFeatureState.Unknown);
        if (!IsEntitlementTrusted(cache.entitlement))
            return SubscriptionEntitlementResult.From(SubscriptionFeatureState.VerificationRequired, null, "Invalid cached entitlement signature.");
        if (ClockRolledBack(cache.lastObservedUtc))
            return SubscriptionEntitlementResult.From(SubscriptionFeatureState.VerificationRequired, null, "Local clock rollback detected.");
        if (!IsStatusActive(cache.entitlement.status))
            return SubscriptionEntitlementResult.From(SubscriptionFeatureState.Expired, cache.entitlement);
        if (!TryParseUtc(cache.entitlement.expiresAtUtc, out DateTime expiresAt) ||
            !TryParseUtc(cache.entitlement.verifiedAtUtc, out DateTime verifiedAt))
        {
            return SubscriptionEntitlementResult.From(SubscriptionFeatureState.VerificationRequired, null, "Cached entitlement dates are invalid.");
        }

        DateTime offlineUntil = Min(expiresAt, verifiedAt.AddHours(_config.OfflineVerificationWindowHours));
        if (_clock.UtcNow <= offlineUntil)
            return SubscriptionEntitlementResult.From(SubscriptionFeatureState.ActiveOffline, cache.entitlement);
        if (_clock.UtcNow > expiresAt)
            return SubscriptionEntitlementResult.From(SubscriptionFeatureState.Expired, cache.entitlement);
        return SubscriptionEntitlementResult.From(SubscriptionFeatureState.VerificationRequired, cache.entitlement);
    }

    bool FeaturesEnabled()
    {
        return _config != null && _config.FeaturesEnabled;
    }

    bool IsEntitlementTrusted(SubscriptionEntitlement entitlement)
    {
        return entitlement != null && (_verifier == null || _verifier.IsTrusted(entitlement));
    }

    bool ClockRolledBack(string lastObservedUtc)
    {
        if (!TryParseUtc(lastObservedUtc, out DateTime lastObserved))
            return false;
        DateTime tolerated = lastObserved.AddMinutes(-_config.ClockRollbackToleranceMinutes);
        return _clock.UtcNow < tolerated;
    }

    static bool IsStatusActive(string status)
    {
        return string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);
    }

    static DateTime Min(DateTime left, DateTime right)
    {
        return left <= right ? left : right;
    }

    public static bool TryParseUtc(string value, out DateTime utc)
    {
        if (DateTime.TryParse(value, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc))
        {
            utc = utc.ToUniversalTime();
            return true;
        }
        utc = default;
        return false;
    }
}
