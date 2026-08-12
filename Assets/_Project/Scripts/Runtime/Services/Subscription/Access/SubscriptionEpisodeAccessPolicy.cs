public interface ISubscriptionEpisodeAccessPolicy
{
    SubscriptionEpisodeAccessDecision Decide(
        SubscriptionEpisodeAccessContext context,
        SubscriptionFeatureState state,
        SubscriptionEntitlement entitlement);
}

public sealed class SubscriptionEpisodeAccessPolicy : ISubscriptionEpisodeAccessPolicy
{
    public SubscriptionEpisodeAccessDecision Decide(
        SubscriptionEpisodeAccessContext context,
        SubscriptionFeatureState state,
        SubscriptionEntitlement entitlement)
    {
        if (context.IsFree)
            return Allow(SubscriptionAccessReason.FreeEpisode, state);
        if (context.IsPurchased)
            return Allow(SubscriptionAccessReason.PurchasedEpisode, state);
        if (!context.IsSubscriptionEpisode)
            return Deny(SubscriptionAccessReason.NotInSubscription, state);
        if (state == SubscriptionFeatureState.Disabled)
            return Deny(SubscriptionAccessReason.FeatureDisabled, state);
        if (entitlement == null || !entitlement.AllowsEpisode(context.EpisodeId))
            return Deny(SubscriptionAccessReason.EntitlementMissing, state);
        if (state == SubscriptionFeatureState.ActiveOffline && !context.IsDownloaded)
            return Deny(SubscriptionAccessReason.EpisodeNotDownloaded, state);
        if (state == SubscriptionFeatureState.ActiveOnline)
            return Allow(SubscriptionAccessReason.SubscriptionOnline, state);
        if (state == SubscriptionFeatureState.ActiveOffline)
            return Allow(SubscriptionAccessReason.SubscriptionOffline, state);
        if (state == SubscriptionFeatureState.Expired)
            return Deny(SubscriptionAccessReason.EntitlementExpired, state);
        if (state == SubscriptionFeatureState.Unavailable)
            return Deny(SubscriptionAccessReason.ServerUnavailable, state);
        return Deny(SubscriptionAccessReason.VerificationRequired, state);
    }

    static SubscriptionEpisodeAccessDecision Allow(SubscriptionAccessReason reason, SubscriptionFeatureState state)
    {
        return new SubscriptionEpisodeAccessDecision(true, reason, state);
    }

    static SubscriptionEpisodeAccessDecision Deny(SubscriptionAccessReason reason, SubscriptionFeatureState state)
    {
        return new SubscriptionEpisodeAccessDecision(false, reason, state);
    }
}
