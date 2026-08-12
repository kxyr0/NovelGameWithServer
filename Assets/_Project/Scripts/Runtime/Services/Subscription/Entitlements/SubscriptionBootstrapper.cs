using System;
using System.Threading;
using VContainer.Unity;

public sealed class SubscriptionBootstrapper : IStartable, IDisposable
{
    readonly SubscriptionFeatureConfig _config;
    readonly ISubscriptionEntitlementService _service;
    CancellationTokenSource _cancellation;

    public SubscriptionBootstrapper(
        SubscriptionFeatureConfig config,
        ISubscriptionEntitlementService service)
    {
        _config = config;
        _service = service;
    }

    public void Start()
    {
        if (_config == null || !_config.FeaturesEnabled || _service == null)
            return;

        _cancellation = new CancellationTokenSource();
        _ = _service.RefreshAsync(_cancellation.Token);
    }

    public void Dispose()
    {
        if (_cancellation == null)
            return;
        _cancellation.Cancel();
        _cancellation.Dispose();
        _cancellation = null;
    }
}
