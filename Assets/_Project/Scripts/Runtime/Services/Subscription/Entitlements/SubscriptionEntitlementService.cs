using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class SubscriptionEntitlementService : ISubscriptionEntitlementService
{
    readonly SubscriptionFeatureConfig _config;
    readonly ISubscriptionEntitlementProvider _provider;
    readonly ISubscriptionEntitlementStore _store;
    readonly SubscriptionEntitlementEvaluator _evaluator;
    readonly ISubscriptionClock _clock;

    public event Action<SubscriptionFeatureState> StateChanged;
    public SubscriptionFeatureState CurrentState { get; private set; }
    public SubscriptionEntitlement CurrentEntitlement { get; private set; }

    public SubscriptionEntitlementService(
        SubscriptionFeatureConfig config,
        ISubscriptionEntitlementProvider provider,
        ISubscriptionEntitlementStore store,
        SubscriptionEntitlementEvaluator evaluator,
        ISubscriptionClock clock)
    {
        _config = config;
        _provider = provider;
        _store = store;
        _evaluator = evaluator;
        _clock = clock;
        CurrentState = config != null && config.FeaturesEnabled ? SubscriptionFeatureState.Unknown : SubscriptionFeatureState.Disabled;
    }

    public async Task<SubscriptionEntitlementResult> RefreshAsync(CancellationToken cancellationToken)
    {
        if (_config == null || !_config.FeaturesEnabled)
            return Apply(SubscriptionEntitlementResult.From(SubscriptionFeatureState.Disabled));

        SubscriptionEntitlementResult online;
        try
        {
            online = _provider != null
                ? await _provider.RefreshAsync(cancellationToken)
                : SubscriptionEntitlementResult.From(SubscriptionFeatureState.Unavailable, null, "No provider configured.");
        }
        catch (Exception exception)
        {
            online = SubscriptionEntitlementResult.From(SubscriptionFeatureState.Unavailable, null, exception.Message);
        }
        if (cancellationToken.IsCancellationRequested)
            return Apply(SubscriptionEntitlementResult.From(CurrentState, CurrentEntitlement, "Canceled."));

        if (online.State == SubscriptionFeatureState.ActiveOnline)
            return ApplyTrustedOnline(online.Entitlement);
        if (online.State == SubscriptionFeatureState.Expired)
            return Apply(online);

        SubscriptionEntitlementResult cached = EvaluateCacheOnly();
        return cached.State == SubscriptionFeatureState.ActiveOffline
            ? cached
            : Apply(SubscriptionEntitlementResult.From(online.State, cached.Entitlement, online.Message));
    }

    public SubscriptionEntitlementResult EvaluateCacheOnly()
    {
        if (_config == null || !_config.FeaturesEnabled)
            return Apply(SubscriptionEntitlementResult.From(SubscriptionFeatureState.Disabled));
        if (_store == null || !_store.TryLoad(out CachedSubscriptionEntitlement cache))
            return Apply(SubscriptionEntitlementResult.From(SubscriptionFeatureState.Unknown));

        SubscriptionEntitlementResult result = _evaluator.EvaluateCache(cache);
        if (ShouldAdvanceLastObserved(cache.lastObservedUtc))
        {
            cache.lastObservedUtc = _clock.UtcNow.ToString("o");
            _store.Save(cache);
        }
        return Apply(result);
    }

    SubscriptionEntitlementResult ApplyTrustedOnline(SubscriptionEntitlement entitlement)
    {
        SubscriptionEntitlementResult result = _evaluator.EvaluateOnline(entitlement);
        if (result.State == SubscriptionFeatureState.ActiveOnline && _store != null)
        {
            _store.Save(new CachedSubscriptionEntitlement
            {
                schemaVersion = 1,
                entitlement = entitlement,
                localVerifiedAtUtc = _clock.UtcNow.ToString("o"),
                lastObservedUtc = _clock.UtcNow.ToString("o")
            });
        }
        return Apply(result);
    }

    bool ShouldAdvanceLastObserved(string lastObservedUtc)
    {
        if (!SubscriptionEntitlementEvaluator.TryParseUtc(lastObservedUtc, out DateTime lastObserved))
            return true;

        return _clock.UtcNow >= lastObserved;
    }

    SubscriptionEntitlementResult Apply(SubscriptionEntitlementResult result)
    {
        result ??= SubscriptionEntitlementResult.From(SubscriptionFeatureState.Unknown);
        CurrentEntitlement = result.Entitlement;
        if (CurrentState != result.State)
        {
            CurrentState = result.State;
            StateChanged?.Invoke(CurrentState);
        }
        return result;
    }
}
