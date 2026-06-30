using System;

[Serializable]
public readonly struct InterstitialAdFrequencyPolicy
{
    public readonly float CooldownSeconds;
    public readonly float MinimumGameplaySecondsBeforeFirstInterstitial;
    public readonly float DelayAfterRewardedSeconds;

    public InterstitialAdFrequencyPolicy(
        float cooldownSeconds,
        float minimumGameplaySecondsBeforeFirstInterstitial,
        float delayAfterRewardedSeconds)
    {
        CooldownSeconds = Math.Max(0f, cooldownSeconds);
        MinimumGameplaySecondsBeforeFirstInterstitial = Math.Max(0f, minimumGameplaySecondsBeforeFirstInterstitial);
        DelayAfterRewardedSeconds = Math.Max(0f, delayAfterRewardedSeconds);
    }
}
