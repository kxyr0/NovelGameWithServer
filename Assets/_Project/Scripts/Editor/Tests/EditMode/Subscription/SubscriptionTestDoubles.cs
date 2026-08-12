using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class TestSubscriptionClock : ISubscriptionClock
{
    public DateTime UtcNow { get; set; }
}

public sealed class TestSubscriptionVerifier : ISubscriptionSignatureVerifier
{
    public bool Trusted = true;
    public bool IsTrusted(SubscriptionEntitlement entitlement) => Trusted;
}

public sealed class TestSubscriptionStore : ISubscriptionEntitlementStore
{
    public CachedSubscriptionEntitlement Cache;
    public bool LoadSucceeds = true;
    public bool Saved;
    public bool Deleted;

    public bool TryLoad(out CachedSubscriptionEntitlement cache)
    {
        cache = Cache;
        return LoadSucceeds && Cache != null;
    }

    public bool Save(CachedSubscriptionEntitlement cache)
    {
        Cache = cache;
        Saved = true;
        return true;
    }

    public void Delete()
    {
        Cache = null;
        Deleted = true;
    }
}

public sealed class TestSubscriptionProvider : ISubscriptionEntitlementProvider
{
    public int Calls;
    public SubscriptionEntitlementResult Result = SubscriptionEntitlementResult.From(SubscriptionFeatureState.Unavailable);

    public Task<SubscriptionEntitlementResult> RefreshAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Result);
    }
}

public static class SubscriptionTestFactory
{
    public static SubscriptionFeatureConfig Config(bool enabled)
    {
        var config = UnityEngine.ScriptableObject.CreateInstance<SubscriptionFeatureConfig>();
        config.SetFeaturesEnabledForEditor(enabled);
        return config;
    }

    public static SubscriptionEntitlement Active(DateTime verifiedAt, DateTime expiresAt, string episodeId = "ep_sub")
    {
        return new SubscriptionEntitlement
        {
            schemaVersion = 1,
            userId = "user_1",
            status = "active",
            verifiedAtUtc = verifiedAt.ToString("o"),
            startsAtUtc = verifiedAt.AddDays(-1).ToString("o"),
            expiresAtUtc = expiresAt.ToString("o"),
            signedToken = "a.b.c",
            episodeIds = new System.Collections.Generic.List<string> { episodeId }
        };
    }

    public static CachedSubscriptionEntitlement Cache(SubscriptionEntitlement entitlement, DateTime observedAt)
    {
        return new CachedSubscriptionEntitlement
        {
            schemaVersion = 1,
            entitlement = entitlement,
            localVerifiedAtUtc = observedAt.ToString("o"),
            lastObservedUtc = observedAt.ToString("o")
        };
    }
}
