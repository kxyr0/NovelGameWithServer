using System;
using System.Threading;
using System.Threading.Tasks;

public interface ISubscriptionClock
{
    DateTime UtcNow { get; }
}

public interface ISubscriptionSignatureVerifier
{
    bool IsTrusted(SubscriptionEntitlement entitlement);
}

public interface ISubscriptionEntitlementStore
{
    bool TryLoad(out CachedSubscriptionEntitlement cache);
    bool Save(CachedSubscriptionEntitlement cache);
    void Delete();
}

public interface ISubscriptionEntitlementProvider
{
    Task<SubscriptionEntitlementResult> RefreshAsync(CancellationToken cancellationToken);
}

public interface ISubscriptionEntitlementService
{
    event Action<SubscriptionFeatureState> StateChanged;
    SubscriptionFeatureState CurrentState { get; }
    SubscriptionEntitlement CurrentEntitlement { get; }
    Task<SubscriptionEntitlementResult> RefreshAsync(CancellationToken cancellationToken);
    SubscriptionEntitlementResult EvaluateCacheOnly();
}

public sealed class SystemSubscriptionClock : ISubscriptionClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class SubscriptionEntitlementResult
{
    public SubscriptionFeatureState State;
    public SubscriptionEntitlement Entitlement;
    public string Message = "";

    public static SubscriptionEntitlementResult From(SubscriptionFeatureState state, SubscriptionEntitlement entitlement = null, string message = "")
    {
        return new SubscriptionEntitlementResult
        {
            State = state,
            Entitlement = entitlement,
            Message = message ?? ""
        };
    }
}
