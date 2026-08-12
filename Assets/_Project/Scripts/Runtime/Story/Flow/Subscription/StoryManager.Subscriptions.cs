using VContainer;

public partial class StoryManager
{
    public event System.Action SubscriptionTimelineStateChanged;

    ISubscriptionEntitlementService _subscriptionEntitlements;
    ISubscriptionEpisodeAccessService _subscriptionEpisodeAccess;
    IStoryChoiceTimelineService _subscriptionTimeline;
    bool _subscriptionTimelineBusy;

    [Inject]
    public void ConstructSubscriptionServices(
        ISubscriptionEntitlementService entitlements,
        ISubscriptionEpisodeAccessService episodeAccess,
        IStoryChoiceTimelineService timeline)
    {
        _subscriptionEntitlements = entitlements;
        _subscriptionEpisodeAccess = episodeAccess;
        _subscriptionTimeline = timeline;
    }

    void CaptureSubscriptionChoiceCheckpoint(ChoiceNode node)
    {
        if (!CanUseSubscriptionTimeline() || node == null || SaveManager.Instance == null)
            return;
        SaveData snapshot = SaveManager.Instance.BuildCurrentSaveData(this);
        if (snapshot == null || !snapshot.HasPosition)
            return;
        if (_subscriptionTimeline.RecordChoiceShown(CurrentStoryId, CurrentEpisodeId, node.guid, snapshot))
            SubscriptionTimelineStateChanged?.Invoke();
    }

    void ConfirmSubscriptionChoice(ChoiceNode node, int index)
    {
        if (!CanUseSubscriptionTimeline() || node == null)
            return;
        if (_subscriptionTimeline.RecordChoiceSelected(CurrentStoryId, node.guid, index))
            SubscriptionTimelineStateChanged?.Invoke();
    }

    bool CanOpenChapterBySubscription(ChapterData chapter, string episodeId)
    {
        if (_subscriptionEpisodeAccess == null)
            return false;
        return _subscriptionEpisodeAccess.Decide(chapter, episodeId, IsChapterAlreadyUnlocked(chapter)).Allowed;
    }

    public StoryChoiceTimelineState GetSubscriptionTimelineState()
    {
        if (_subscriptionTimeline == null)
            return new StoryChoiceTimelineState(false, false, false, _subscriptionTimelineBusy);
        return _subscriptionTimeline.GetState(CurrentStoryId, CanUseSubscriptionTimeline(), _subscriptionTimelineBusy);
    }

    public bool TrySubscriptionRewindBack()
    {
        if (!CanUseSubscriptionTimeline() || _subscriptionTimelineBusy)
            return false;
        if (!_subscriptionTimeline.TryGetRewindBack(CurrentStoryId, out int index, out SaveData snapshot))
            return false;
        return RestoreSubscriptionTimelineSnapshot(snapshot, () => _subscriptionTimeline.CommitRestore(CurrentStoryId, index), "subscription rewind back");
    }

    public bool TrySubscriptionRewindForward()
    {
        if (!CanUseSubscriptionTimeline() || _subscriptionTimelineBusy)
            return false;
        if (!_subscriptionTimeline.TryGetRewindForward(CurrentStoryId, out int index, out SaveData snapshot))
            return false;
        return RestoreSubscriptionTimelineSnapshot(snapshot, () => _subscriptionTimeline.CommitRestore(CurrentStoryId, index), "subscription rewind forward");
    }

    public bool TrySubscriptionUndoLastChoice()
    {
        if (!CanUseSubscriptionTimeline() || _subscriptionTimelineBusy)
            return false;
        if (!_subscriptionTimeline.TryGetUndo(CurrentStoryId, out int index, out SaveData snapshot))
            return false;
        return RestoreSubscriptionTimelineSnapshot(snapshot, () => _subscriptionTimeline.CommitUndo(CurrentStoryId, index), "subscription undo choice");
    }

    bool RestoreSubscriptionTimelineSnapshot(SaveData snapshot, System.Func<bool> commit, string source)
    {
        _subscriptionTimelineBusy = true;
        try
        {
            if (!TryRestoreSnapshot(snapshot, source))
                return false;
            if (commit == null || !commit())
                return false;
            SaveManager.Instance?.Save(snapshot, ResolveProgressSaveSlot());
            SubscriptionTimelineStateChanged?.Invoke();
            return true;
        }
        finally
        {
            _subscriptionTimelineBusy = false;
        }
    }

    bool CanUseSubscriptionTimeline()
    {
        if (_subscriptionEntitlements == null)
            return false;
        return _subscriptionEntitlements.CurrentState == SubscriptionFeatureState.ActiveOnline ||
               _subscriptionEntitlements.CurrentState == SubscriptionFeatureState.ActiveOffline;
    }
}
