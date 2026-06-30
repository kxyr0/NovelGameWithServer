using NUnit.Framework;

public sealed class InterstitialAdFrequencyLimiterTests
{
    private float _time;

    [SetUp]
    public void SetUp()
    {
        _time = 0f;
    }

    [Test]
    public void CanShow_BlocksBeforeFirstDelay()
    {
        var limiter = CreateLimiter();
        var policy = new InterstitialAdFrequencyPolicy(30f, 10f, 0f);

        _time = 9f;
        InterstitialAdShowDecision decision = limiter.CanShow(policy, true, true, true, true);

        Assert.That(decision.Allowed, Is.False);
        Assert.That(decision.Reason, Is.EqualTo("first_delay"));
    }

    [Test]
    public void CanShow_BlocksDuringCooldown()
    {
        var limiter = CreateLimiter();
        var policy = new InterstitialAdFrequencyPolicy(30f, 0f, 0f);

        _time = 10f;
        limiter.MarkInterstitialShown();

        _time = 39f;
        InterstitialAdShowDecision decision = limiter.CanShow(policy, true, true, true, true);

        Assert.That(decision.Allowed, Is.False);
        Assert.That(decision.Reason, Is.EqualTo("cooldown"));
    }

    [Test]
    public void CanShow_BlocksAfterRewarded()
    {
        var limiter = CreateLimiter();
        var policy = new InterstitialAdFrequencyPolicy(0f, 0f, 20f);

        _time = 5f;
        limiter.MarkRewardedFinished();

        _time = 24f;
        InterstitialAdShowDecision decision = limiter.CanShow(policy, true, true, true, true);

        Assert.That(decision.Allowed, Is.False);
        Assert.That(decision.Reason, Is.EqualTo("rewarded_delay"));
    }

    [Test]
    public void CanShow_AllowsAfterDelays()
    {
        var limiter = CreateLimiter();
        var policy = new InterstitialAdFrequencyPolicy(30f, 10f, 20f);

        _time = 10f;
        limiter.MarkInterstitialShown();
        _time = 15f;
        limiter.MarkRewardedFinished();

        _time = 46f;
        InterstitialAdShowDecision decision = limiter.CanShow(policy, true, true, true, true);

        Assert.That(decision.Allowed, Is.True);
    }

    [Test]
    public void CanShow_BlocksWhileAnotherAdShowing()
    {
        var limiter = CreateLimiter();
        var policy = new InterstitialAdFrequencyPolicy(0f, 0f, 0f);

        limiter.MarkAdShowing();
        InterstitialAdShowDecision decision = limiter.CanShow(policy, true, true, true, true);

        Assert.That(decision.Allowed, Is.False);
        Assert.That(decision.Reason, Is.EqualTo("another_ad_showing"));
    }

    private InterstitialAdFrequencyLimiter CreateLimiter()
    {
        return new InterstitialAdFrequencyLimiter(() => _time);
    }
}
