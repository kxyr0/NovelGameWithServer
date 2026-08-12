using System;
using System.Collections.Generic;

public enum SubscriptionFeatureState
{
    Disabled = 0,
    Unknown = 1,
    ActiveOnline = 2,
    ActiveOffline = 3,
    VerificationRequired = 4,
    Expired = 5,
    Unavailable = 6
}

public enum SubscriptionAccessReason
{
    FreeEpisode = 0,
    PurchasedEpisode = 1,
    SubscriptionOnline = 2,
    SubscriptionOffline = 3,
    FeatureDisabled = 4,
    NotInSubscription = 5,
    EntitlementMissing = 6,
    EntitlementExpired = 7,
    VerificationRequired = 8,
    EpisodeNotDownloaded = 9,
    ServerUnavailable = 10
}

[Serializable]
public sealed class SubscriptionEntitlement
{
    public int schemaVersion = 1;
    public string userId = "";
    public string status = "";
    public string startsAtUtc = "";
    public string expiresAtUtc = "";
    public string verifiedAtUtc = "";
    public string accessRule = "";
    public string signedToken = "";
    public string serverSignature = "";
    public List<string> episodeIds = new List<string>();

    public bool AllowsEpisode(string episodeId)
    {
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        if (string.IsNullOrEmpty(episodeId))
            return false;
        if (string.Equals(accessRule, "all_subscription", StringComparison.OrdinalIgnoreCase))
            return true;
        if (episodeIds == null)
            return false;
        for (int i = 0; i < episodeIds.Count; i++)
            if (string.Equals(SaveDataSanitizer.SanitizeIdentifier(episodeIds[i]), episodeId, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}

[Serializable]
public sealed class CachedSubscriptionEntitlement
{
    public int schemaVersion = 1;
    public string lastObservedUtc = "";
    public string localVerifiedAtUtc = "";
    public SubscriptionEntitlement entitlement = new SubscriptionEntitlement();
}

public readonly struct SubscriptionEpisodeAccessContext
{
    public readonly string EpisodeId;
    public readonly bool IsFree;
    public readonly bool IsPurchased;
    public readonly bool IsSubscriptionEpisode;
    public readonly bool IsDownloaded;

    public SubscriptionEpisodeAccessContext(string episodeId, bool isFree, bool isPurchased, bool isSubscriptionEpisode, bool isDownloaded)
    {
        EpisodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        IsFree = isFree;
        IsPurchased = isPurchased;
        IsSubscriptionEpisode = isSubscriptionEpisode;
        IsDownloaded = isDownloaded;
    }
}

public readonly struct SubscriptionEpisodeAccessDecision
{
    public readonly bool Allowed;
    public readonly SubscriptionAccessReason Reason;
    public readonly SubscriptionFeatureState State;

    public SubscriptionEpisodeAccessDecision(bool allowed, SubscriptionAccessReason reason, SubscriptionFeatureState state)
    {
        Allowed = allowed;
        Reason = reason;
        State = state;
    }
}
