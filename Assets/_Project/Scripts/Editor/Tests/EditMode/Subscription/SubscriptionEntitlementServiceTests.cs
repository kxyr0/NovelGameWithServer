using System;
using NUnit.Framework;

public sealed class SubscriptionEntitlementServiceTests
{
    [Test]
    public void DisabledFeature_DoesNotCallProviderOrReadCache()
    {
        var provider = new TestSubscriptionProvider();
        var store = new TestSubscriptionStore { Cache = SubscriptionTestFactory.Cache(Entitlement(), Now) };
        var service = CreateService(false, provider, store, Now);

        SubscriptionEntitlementResult result = service.RefreshAsync(default).Result;

        Assert.That(result.State, Is.EqualTo(SubscriptionFeatureState.Disabled));
        Assert.That(provider.Calls, Is.EqualTo(0));
        Assert.That(store.Saved, Is.False);
    }

    [Test]
    public void ActiveOnline_SavesTrustedEntitlement()
    {
        var provider = new TestSubscriptionProvider { Result = SubscriptionEntitlementResult.From(SubscriptionFeatureState.ActiveOnline, Entitlement()) };
        var store = new TestSubscriptionStore();
        var service = CreateService(true, provider, store, Now);

        SubscriptionEntitlementResult result = service.RefreshAsync(default).Result;

        Assert.That(result.State, Is.EqualTo(SubscriptionFeatureState.ActiveOnline));
        Assert.That(store.Saved, Is.True);
        Assert.That(store.Cache.entitlement.userId, Is.EqualTo("user_1"));
    }

    [Test]
    public void ServerUnavailable_UsesValidOfflineCache()
    {
        var store = new TestSubscriptionStore { Cache = SubscriptionTestFactory.Cache(Entitlement(Now.AddHours(-71), Now.AddDays(4)), Now) };
        var service = CreateService(true, new TestSubscriptionProvider(), store, Now);

        SubscriptionEntitlementResult result = service.RefreshAsync(default).Result;

        Assert.That(result.State, Is.EqualTo(SubscriptionFeatureState.ActiveOffline));
    }

    [Test]
    public void CacheOlderThan72Hours_RequiresVerification()
    {
        var store = new TestSubscriptionStore { Cache = SubscriptionTestFactory.Cache(Entitlement(Now.AddHours(-73), Now.AddDays(4)), Now) };
        var service = CreateService(true, new TestSubscriptionProvider(), store, Now);

        SubscriptionEntitlementResult result = service.EvaluateCacheOnly();

        Assert.That(result.State, Is.EqualTo(SubscriptionFeatureState.VerificationRequired));
    }

    [Test]
    public void SubscriptionExpiresBeforeOfflineWindow_BecomesExpired()
    {
        var store = new TestSubscriptionStore { Cache = SubscriptionTestFactory.Cache(Entitlement(Now.AddHours(-2), Now.AddMinutes(-1)), Now) };
        var service = CreateService(true, new TestSubscriptionProvider(), store, Now);

        SubscriptionEntitlementResult result = service.EvaluateCacheOnly();

        Assert.That(result.State, Is.EqualTo(SubscriptionFeatureState.Expired));
    }

    [Test]
    public void InvalidSignature_DoesNotGrantOfflineAccess()
    {
        var store = new TestSubscriptionStore { Cache = SubscriptionTestFactory.Cache(Entitlement(), Now) };
        var service = CreateService(true, new TestSubscriptionProvider(), store, Now, trusted: false);

        SubscriptionEntitlementResult result = service.EvaluateCacheOnly();

        Assert.That(result.State, Is.EqualTo(SubscriptionFeatureState.VerificationRequired));
    }

    [Test]
    public void DefaultVerifier_DoesNotTrustTokenShapeWithoutRealSignatureCheck()
    {
        var verifier = new SubscriptionSignedTokenVerifier();

        Assert.That(verifier.IsTrusted(Entitlement()), Is.False);
    }

    [Test]
    public void ClockRollback_ForcesVerification()
    {
        var store = new TestSubscriptionStore { Cache = SubscriptionTestFactory.Cache(Entitlement(), Now) };
        var service = CreateService(true, new TestSubscriptionProvider(), store, Now.AddHours(-1));

        SubscriptionEntitlementResult result = service.EvaluateCacheOnly();

        Assert.That(result.State, Is.EqualTo(SubscriptionFeatureState.VerificationRequired));
        Assert.That(store.Saved, Is.False);
        Assert.That(store.Cache.lastObservedUtc, Is.EqualTo(Now.ToString("o")));
    }

    [Test]
    public void CorruptedCache_ReturnsUnknown()
    {
        var store = new TestSubscriptionStore { LoadSucceeds = false };
        var service = CreateService(true, new TestSubscriptionProvider(), store, Now);

        SubscriptionEntitlementResult result = service.EvaluateCacheOnly();

        Assert.That(result.State, Is.EqualTo(SubscriptionFeatureState.Unknown));
    }

    [Test]
    public void ServerUnavailableAndExpiredCache_ReturnsUnavailable()
    {
        var store = new TestSubscriptionStore { Cache = SubscriptionTestFactory.Cache(Entitlement(Now.AddDays(-4), Now.AddDays(-1)), Now) };
        var service = CreateService(true, new TestSubscriptionProvider(), store, Now);

        SubscriptionEntitlementResult result = service.RefreshAsync(default).Result;

        Assert.That(result.State, Is.EqualTo(SubscriptionFeatureState.Unavailable));
    }

    static readonly DateTime Now = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);

    static SubscriptionEntitlement Entitlement()
    {
        return Entitlement(Now, Now.AddDays(7));
    }

    static SubscriptionEntitlement Entitlement(DateTime verifiedAt, DateTime expiresAt)
    {
        return SubscriptionTestFactory.Active(verifiedAt, expiresAt);
    }

    static SubscriptionEntitlementService CreateService(bool enabled, TestSubscriptionProvider provider, TestSubscriptionStore store, DateTime now, bool trusted = true)
    {
        var config = SubscriptionTestFactory.Config(enabled);
        var clock = new TestSubscriptionClock { UtcNow = now };
        var verifier = new TestSubscriptionVerifier { Trusted = trusted };
        var evaluator = new SubscriptionEntitlementEvaluator(config, clock, verifier);
        return new SubscriptionEntitlementService(config, provider, store, evaluator, clock);
    }
}
