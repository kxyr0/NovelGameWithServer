public readonly struct InterstitialAdShowDecision
{
    public InterstitialAdShowDecision(bool allowed, string reason)
    {
        Allowed = allowed;
        Reason = reason ?? "";
    }

    public bool Allowed { get; }
    public string Reason { get; }

    public static InterstitialAdShowDecision Allow()
    {
        return new InterstitialAdShowDecision(true, "");
    }

    public static InterstitialAdShowDecision Skip(string reason)
    {
        return new InterstitialAdShowDecision(false, reason);
    }
}
