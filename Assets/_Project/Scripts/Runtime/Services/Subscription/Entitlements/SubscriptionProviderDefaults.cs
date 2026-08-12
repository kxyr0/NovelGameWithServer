using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class UnavailableSubscriptionEntitlementProvider : ISubscriptionEntitlementProvider
{
    public Task<SubscriptionEntitlementResult> RefreshAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(SubscriptionEntitlementResult.From(
            SubscriptionFeatureState.Unavailable,
            null,
            "Backend endpoint is not configured: GET /player/subscription/entitlement."));
    }
}

public sealed class SubscriptionSignedTokenVerifier : ISubscriptionSignatureVerifier
{
    public bool IsTrusted(SubscriptionEntitlement entitlement)
    {
        return false;
    }
}
