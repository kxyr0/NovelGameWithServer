using System;

public interface IStoryChoiceTimelineService
{
    event Action Changed;
    StoryChoiceTimelineState GetState(string storyId, bool premiumAllowed, bool busy);
    bool RecordChoiceShown(string storyId, string episodeId, string choiceGuid, SaveData snapshot);
    bool RecordChoiceSelected(string storyId, string choiceGuid, int selectedIndex);
    bool TryGetRewindBack(string storyId, out int targetIndex, out SaveData snapshot);
    bool TryGetRewindForward(string storyId, out int targetIndex, out SaveData snapshot);
    bool TryGetUndo(string storyId, out int targetIndex, out SaveData snapshot);
    bool CommitRestore(string storyId, int targetIndex);
    bool CommitUndo(string storyId, int targetIndex);
}

public sealed class StoryChoiceTimelineService : IStoryChoiceTimelineService
{
    const int MaxCheckpoints = 100;
    readonly IStoryChoiceTimelineStore _store;
    public event Action Changed;

    public StoryChoiceTimelineService(IStoryChoiceTimelineStore store)
    {
        _store = store;
    }

    public StoryChoiceTimelineState GetState(string storyId, bool premiumAllowed, bool busy)
    {
        StoryChoiceTimeline timeline = Load(storyId);
        bool back = premiumAllowed && !busy && timeline.currentIndex > 0;
        bool forward = premiumAllowed && !busy && timeline.currentIndex >= 0 && timeline.currentIndex < timeline.checkpoints.Count - 1;
        bool undo = premiumAllowed && !busy && FindLastSelected(timeline) >= 0;
        return new StoryChoiceTimelineState(back, forward, undo, busy);
    }

    public bool RecordChoiceShown(string storyId, string episodeId, string choiceGuid, SaveData snapshot)
    {
        snapshot = SaveDataSanitizer.SanitizeCopy(snapshot);
        if (snapshot == null || string.IsNullOrEmpty(choiceGuid))
            return false;
        StoryChoiceTimeline timeline = Load(storyId);
        TrimFuture(timeline);
        if (timeline.checkpoints.Count > 0 && timeline.checkpoints[timeline.checkpoints.Count - 1].choiceNodeGuid == choiceGuid)
            return true;
        timeline.checkpoints.Add(NewCheckpoint(storyId, episodeId, choiceGuid, snapshot));
        timeline.currentIndex = timeline.checkpoints.Count - 1;
        TrimOldest(timeline);
        return SaveAndNotify(timeline);
    }

    public bool RecordChoiceSelected(string storyId, string choiceGuid, int selectedIndex)
    {
        StoryChoiceTimeline timeline = Load(storyId);
        int index = FindByGuid(timeline, choiceGuid);
        if (index < 0)
            return false;
        TrimAfter(timeline, index);
        timeline.currentIndex = index;
        timeline.checkpoints[index].selectedIndex = selectedIndex;
        return SaveAndNotify(timeline);
    }

    public bool TryGetRewindBack(string storyId, out int targetIndex, out SaveData snapshot)
    {
        StoryChoiceTimeline timeline = Load(storyId);
        targetIndex = timeline.currentIndex - 1;
        return TryGetSnapshot(timeline, targetIndex, out snapshot);
    }

    public bool TryGetRewindForward(string storyId, out int targetIndex, out SaveData snapshot)
    {
        StoryChoiceTimeline timeline = Load(storyId);
        targetIndex = timeline.currentIndex + 1;
        return TryGetSnapshot(timeline, targetIndex, out snapshot);
    }

    public bool TryGetUndo(string storyId, out int targetIndex, out SaveData snapshot)
    {
        StoryChoiceTimeline timeline = Load(storyId);
        targetIndex = FindLastSelected(timeline);
        return TryGetSnapshot(timeline, targetIndex, out snapshot);
    }

    public bool CommitRestore(string storyId, int targetIndex)
    {
        StoryChoiceTimeline timeline = Load(storyId);
        if (targetIndex < 0 || targetIndex >= timeline.checkpoints.Count)
            return false;
        timeline.currentIndex = targetIndex;
        return SaveAndNotify(timeline);
    }

    public bool CommitUndo(string storyId, int targetIndex)
    {
        StoryChoiceTimeline timeline = Load(storyId);
        if (targetIndex < 0 || targetIndex >= timeline.checkpoints.Count)
            return false;
        TrimAfter(timeline, targetIndex);
        timeline.currentIndex = targetIndex;
        timeline.checkpoints[targetIndex].selectedIndex = -1;
        return SaveAndNotify(timeline);
    }

    StoryChoiceTimeline Load(string storyId)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (_store != null && _store.TryLoad(storyId, out StoryChoiceTimeline timeline))
            return timeline;
        return new StoryChoiceTimeline { storyId = storyId, currentIndex = -1 };
    }

    static StoryChoiceCheckpoint NewCheckpoint(string storyId, string episodeId, string choiceGuid, SaveData snapshot)
    {
        return new StoryChoiceCheckpoint
        {
            checkpointId = Guid.NewGuid().ToString("N"),
            storyId = SaveDataSanitizer.SanitizeIdentifier(storyId),
            episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId),
            choiceNodeGuid = SaveDataSanitizer.SanitizeIdentifier(choiceGuid),
            createdAtUtc = DateTime.UtcNow.ToString("o"),
            snapshot = snapshot
        };
    }

    bool SaveAndNotify(StoryChoiceTimeline timeline)
    {
        bool saved = _store != null && _store.Save(timeline);
        if (saved)
            Changed?.Invoke();
        return saved;
    }

    static bool TryGetSnapshot(StoryChoiceTimeline timeline, int index, out SaveData snapshot)
    {
        snapshot = index >= 0 && index < timeline.checkpoints.Count
            ? SaveDataSanitizer.SanitizeCopy(timeline.checkpoints[index].snapshot)
            : null;
        return snapshot != null && snapshot.HasPosition;
    }

    static int FindByGuid(StoryChoiceTimeline timeline, string choiceGuid)
    {
        for (int i = timeline.checkpoints.Count - 1; i >= 0; i--)
            if (timeline.checkpoints[i].choiceNodeGuid == choiceGuid)
                return i;
        return -1;
    }

    static int FindLastSelected(StoryChoiceTimeline timeline)
    {
        for (int i = timeline.checkpoints.Count - 1; i >= 0; i--)
            if (timeline.checkpoints[i].selectedIndex >= 0)
                return i;
        return -1;
    }

    static void TrimFuture(StoryChoiceTimeline timeline)
    {
        if (timeline.currentIndex >= 0)
            TrimAfter(timeline, timeline.currentIndex);
    }

    static void TrimAfter(StoryChoiceTimeline timeline, int index)
    {
        while (timeline.checkpoints.Count > index + 1)
            timeline.checkpoints.RemoveAt(timeline.checkpoints.Count - 1);
    }

    static void TrimOldest(StoryChoiceTimeline timeline)
    {
        while (timeline.checkpoints.Count > MaxCheckpoints)
        {
            timeline.checkpoints.RemoveAt(0);
            timeline.currentIndex = Math.Max(-1, timeline.currentIndex - 1);
        }
    }
}
