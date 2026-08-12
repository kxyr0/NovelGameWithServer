using NUnit.Framework;

public sealed class SubscriptionAccessPolicyTests
{
    readonly SubscriptionEpisodeAccessPolicy _policy = new SubscriptionEpisodeAccessPolicy();

    [Test]
    public void FreeEpisode_IsAvailableWithoutSubscription()
    {
        var decision = Decide(Context(isFree: true), SubscriptionFeatureState.Disabled, null);

        Assert.That(decision.Allowed, Is.True);
        Assert.That(decision.Reason, Is.EqualTo(SubscriptionAccessReason.FreeEpisode));
    }

    [Test]
    public void PurchasedEpisode_RemainsAvailableAfterSubscriptionExpired()
    {
        var decision = Decide(Context(purchased: true), SubscriptionFeatureState.Expired, null);

        Assert.That(decision.Allowed, Is.True);
        Assert.That(decision.Reason, Is.EqualTo(SubscriptionAccessReason.PurchasedEpisode));
    }

    [Test]
    public void ActiveOnline_AllowsSubscriptionEpisode()
    {
        var decision = Decide(Context(downloaded: false), SubscriptionFeatureState.ActiveOnline, Entitlement());

        Assert.That(decision.Allowed, Is.True);
        Assert.That(decision.Reason, Is.EqualTo(SubscriptionAccessReason.SubscriptionOnline));
    }

    [Test]
    public void ActiveOffline_AllowsOnlyDownloadedSubscriptionEpisode()
    {
        var allowed = Decide(Context(downloaded: true), SubscriptionFeatureState.ActiveOffline, Entitlement());
        var denied = Decide(Context(downloaded: false), SubscriptionFeatureState.ActiveOffline, Entitlement());

        Assert.That(allowed.Allowed, Is.True);
        Assert.That(denied.Allowed, Is.False);
        Assert.That(denied.Reason, Is.EqualTo(SubscriptionAccessReason.EpisodeNotDownloaded));
    }

    [Test]
    public void UnknownOrDisabledSubscription_DoesNotOpenSubscriptionEpisode()
    {
        var disabled = Decide(Context(), SubscriptionFeatureState.Disabled, Entitlement());
        var unknown = Decide(Context(), SubscriptionFeatureState.Unknown, Entitlement());

        Assert.That(disabled.Allowed, Is.False);
        Assert.That(disabled.Reason, Is.EqualTo(SubscriptionAccessReason.FeatureDisabled));
        Assert.That(unknown.Allowed, Is.False);
        Assert.That(unknown.Reason, Is.EqualTo(SubscriptionAccessReason.VerificationRequired));
    }

    [Test]
    public void EntitlementMustAllowEpisode()
    {
        var decision = Decide(Context(episodeId: "ep_other"), SubscriptionFeatureState.ActiveOnline, Entitlement());

        Assert.That(decision.Allowed, Is.False);
        Assert.That(decision.Reason, Is.EqualTo(SubscriptionAccessReason.EntitlementMissing));
    }

    [Test]
    public void ExpiredOrUnavailable_DenySubscriptionEpisode()
    {
        var expired = Decide(Context(), SubscriptionFeatureState.Expired, Entitlement());
        var unavailable = Decide(Context(), SubscriptionFeatureState.Unavailable, Entitlement());

        Assert.That(expired.Reason, Is.EqualTo(SubscriptionAccessReason.EntitlementExpired));
        Assert.That(unavailable.Reason, Is.EqualTo(SubscriptionAccessReason.ServerUnavailable));
    }

    SubscriptionEpisodeAccessDecision Decide(
        SubscriptionEpisodeAccessContext context,
        SubscriptionFeatureState state,
        SubscriptionEntitlement entitlement)
    {
        return _policy.Decide(context, state, entitlement);
    }

    static SubscriptionEpisodeAccessContext Context(
        string episodeId = "ep_sub",
        bool isFree = false,
        bool purchased = false,
        bool downloaded = true)
    {
        return new SubscriptionEpisodeAccessContext(episodeId, isFree, purchased, true, downloaded);
    }

    static SubscriptionEntitlement Entitlement()
    {
        return new SubscriptionEntitlement
        {
            status = "active",
            episodeIds = new System.Collections.Generic.List<string> { "ep_sub" },
            signedToken = "a.b.c"
        };
    }
}
