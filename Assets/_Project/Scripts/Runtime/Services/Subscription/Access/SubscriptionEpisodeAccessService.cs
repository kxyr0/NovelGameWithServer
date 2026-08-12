public interface ISubscriptionEpisodeAccessService
{
    SubscriptionEpisodeAccessDecision Decide(ChapterData chapter, string episodeId, bool purchased);
}

public sealed class SubscriptionEpisodeAccessService : ISubscriptionEpisodeAccessService
{
    readonly ISubscriptionEntitlementService _entitlements;
    readonly ISubscriptionEpisodeAccessPolicy _policy;

    public SubscriptionEpisodeAccessService(
        ISubscriptionEntitlementService entitlements,
        ISubscriptionEpisodeAccessPolicy policy)
    {
        _entitlements = entitlements;
        _policy = policy;
    }

    public SubscriptionEpisodeAccessDecision Decide(ChapterData chapter, string episodeId, bool purchased)
    {
        bool free = !IsPremium(chapter, episodeId);
        var context = new SubscriptionEpisodeAccessContext(
            episodeId,
            free,
            purchased,
            IsSubscriptionEpisode(episodeId),
            IsDownloaded(chapter, episodeId));
        return _policy.Decide(context, State, _entitlements?.CurrentEntitlement);
    }

    SubscriptionFeatureState State => _entitlements != null
        ? _entitlements.CurrentState
        : SubscriptionFeatureState.Disabled;

    static bool IsPremium(ChapterData chapter, string episodeId)
    {
        return (chapter != null && chapter.isPremium) ||
               NetworkManager.IsCatalogEpisodePremium(episodeId, false);
    }

    static bool IsSubscriptionEpisode(string episodeId)
    {
        if (string.IsNullOrEmpty(episodeId))
            return false;
        if (!NetworkManager.TryGetCatalogEpisode(episodeId, out var episode))
            return true;
        return episode.isPremium && !episode.isUnlocked;
    }

    static bool IsDownloaded(ChapterData chapter, string episodeId)
    {
        if (chapter != null && (chapter.graph != null || chapter.jsonGraph != null))
            return true;
        return !string.IsNullOrEmpty(episodeId) && RemoteEpisodeGraphCache.TryLoad(episodeId, out _);
    }
}
