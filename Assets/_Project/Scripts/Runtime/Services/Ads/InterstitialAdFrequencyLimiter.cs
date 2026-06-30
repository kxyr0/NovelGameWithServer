using System;

public sealed class InterstitialAdFrequencyLimiter
{
    private readonly Func<float> _timeProvider;
    private float _sessionStartedAt;
    private float _lastInterstitialShownAt = float.NegativeInfinity;
    private float _lastRewardedFinishedAt = float.NegativeInfinity;

    public InterstitialAdFrequencyLimiter(Func<float> timeProvider)
    {
        _timeProvider = timeProvider ?? (() => 0f);
        ResetSession();
    }

    public bool IsAdShowing { get; private set; }

    public void ResetSession()
    {
        _sessionStartedAt = Now;
        _lastInterstitialShownAt = float.NegativeInfinity;
        _lastRewardedFinishedAt = float.NegativeInfinity;
        IsAdShowing = false;
    }

    public void MarkAdShowing()
    {
        IsAdShowing = true;
    }

    public void MarkAdClosed()
    {
        IsAdShowing = false;
    }

    public void MarkInterstitialShown()
    {
        _lastInterstitialShownAt = Now;
    }

    public void MarkRewardedFinished()
    {
        _lastRewardedFinishedAt = Now;
    }

    public InterstitialAdShowDecision CanShow(
        InterstitialAdFrequencyPolicy policy,
        bool adsEnabled,
        bool entitlementAllowsAds,
        bool initialized,
        bool ready)
    {
        if (!adsEnabled)
            return InterstitialAdShowDecision.Skip("ads_disabled");

        if (!entitlementAllowsAds)
            return InterstitialAdShowDecision.Skip("ads_removed");

        if (!initialized)
            return InterstitialAdShowDecision.Skip("not_initialized");

        if (IsAdShowing)
            return InterstitialAdShowDecision.Skip("another_ad_showing");

        if (!ready)
            return InterstitialAdShowDecision.Skip("not_ready");

        float now = Now;
        if (now - _sessionStartedAt < policy.MinimumGameplaySecondsBeforeFirstInterstitial)
            return InterstitialAdShowDecision.Skip("first_delay");

        if (now - _lastInterstitialShownAt < policy.CooldownSeconds)
            return InterstitialAdShowDecision.Skip("cooldown");

        if (now - _lastRewardedFinishedAt < policy.DelayAfterRewardedSeconds)
            return InterstitialAdShowDecision.Skip("rewarded_delay");

        return InterstitialAdShowDecision.Allow();
    }

    private float Now => Math.Max(0f, _timeProvider());
}
