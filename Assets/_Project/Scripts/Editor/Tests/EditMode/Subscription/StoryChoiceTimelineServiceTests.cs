using NUnit.Framework;

public sealed class StoryChoiceTimelineServiceTests
{
    InMemoryTimelineStore _store;
    StoryChoiceTimelineService _service;

    [SetUp]
    public void SetUp()
    {
        _store = new InMemoryTimelineStore();
        _service = new StoryChoiceTimelineService(_store);
    }

    [Test]
    public void CreatesCheckpointBeforeChoice()
    {
        bool ok = _service.RecordChoiceShown("story", "ep1", "choice1", Snapshot("choice1", 1));

        Assert.That(ok, Is.True);
        Assert.That(_store.Timeline.checkpoints.Count, Is.EqualTo(1));
        Assert.That(_store.Timeline.checkpoints[0].snapshot.statValues[0], Is.EqualTo(1));
    }

    [Test]
    public void RestoresPreviousChoiceAndForwardOnlyToExistingCheckpoint()
    {
        AddSelected("choice1", 0, 1);
        AddSelected("choice2", 1, 2);

        bool back = _service.TryGetRewindBack("story", out int backIndex, out SaveData backSnapshot);
        Assert.That(back, Is.True);
        Assert.That(backSnapshot.currentNodeGuid, Is.EqualTo("choice1"));

        _service.CommitRestore("story", backIndex);
        bool forward = _service.TryGetRewindForward("story", out _, out SaveData forwardSnapshot);
        Assert.That(forward, Is.True);
        Assert.That(forwardSnapshot.currentNodeGuid, Is.EqualTo("choice2"));
    }

    [Test]
    public void CannotForwardIntoUnseenScene()
    {
        AddSelected("choice1", 0, 1);

        bool forward = _service.TryGetRewindForward("story", out _, out _);

        Assert.That(forward, Is.False);
    }

    [Test]
    public void UndoLastChoiceReturnsSnapshotBeforeChoice()
    {
        AddSelected("choice1", 0, 10);
        AddSelected("choice2", 1, 20);

        bool ok = _service.TryGetUndo("story", out int index, out SaveData snapshot);
        _service.CommitUndo("story", index);

        Assert.That(ok, Is.True);
        Assert.That(snapshot.currentNodeGuid, Is.EqualTo("choice2"));
        Assert.That(_store.Timeline.checkpoints.Count, Is.EqualTo(2));
        Assert.That(_store.Timeline.checkpoints[1].selectedIndex, Is.EqualTo(-1));
    }

    [Test]
    public void UndoSnapshotContainsPreviousVariables()
    {
        AddSelected("choice1", 0, 3);
        AddSelected("choice2", 1, 7);

        _service.TryGetUndo("story", out _, out SaveData snapshot);

        Assert.That(snapshot.statKeys[0], Is.EqualTo("trust"));
        Assert.That(snapshot.statValues[0], Is.EqualTo(7));
    }

    [Test]
    public void SelectingDifferentBranchDeletesOldFuture()
    {
        AddSelected("choice1", 0, 1);
        AddSelected("choice2", 1, 2);
        _service.TryGetRewindBack("story", out int index, out _);
        _service.CommitRestore("story", index);

        _service.RecordChoiceSelected("story", "choice1", 2);

        Assert.That(_store.Timeline.checkpoints.Count, Is.EqualTo(1));
        Assert.That(_store.Timeline.checkpoints[0].selectedIndex, Is.EqualTo(2));
    }

    [Test]
    public void OldSaveWithoutTimelineHasNoOperations()
    {
        StoryChoiceTimelineState state = _service.GetState("story", premiumAllowed: true, busy: false);

        Assert.That(state.CanUndoChoice, Is.False);
        Assert.That(state.CanRewindBack, Is.False);
        Assert.That(state.CanRewindForward, Is.False);
    }

    [Test]
    public void BusyStateDisablesButtons()
    {
        AddSelected("choice1", 0, 1);
        StoryChoiceTimelineState state = _service.GetState("story", premiumAllowed: true, busy: true);

        Assert.That(state.CanUndoChoice, Is.False);
        Assert.That(state.Busy, Is.True);
    }

    [Test]
    public void SnapshotWriteErrorDoesNotReportCheckpointCreated()
    {
        _store.SaveSucceeds = false;

        bool ok = _service.RecordChoiceShown("story", "ep1", "choice1", Snapshot("choice1", 1));

        Assert.That(ok, Is.False);
    }

    [Test]
    public void CorruptedSnapshotCannotBeRestored()
    {
        _store.Timeline = new StoryChoiceTimeline { storyId = "story", currentIndex = 0 };
        _store.Timeline.checkpoints.Add(new StoryChoiceCheckpoint { choiceNodeGuid = "bad", selectedIndex = 0 });

        bool ok = _service.TryGetUndo("story", out _, out _);

        Assert.That(ok, Is.False);
    }

    void AddSelected(string guid, int selected, int stat)
    {
        _service.RecordChoiceShown("story", "ep1", guid, Snapshot(guid, stat));
        _service.RecordChoiceSelected("story", guid, selected);
    }

    static SaveData Snapshot(string nodeGuid, int stat)
    {
        var data = new SaveData { storyId = "story", episodeId = "ep1", currentNodeGuid = nodeGuid, savedAtIso = System.DateTime.UtcNow.ToString("o") };
        data.statKeys.Add("trust");
        data.statValues.Add(stat);
        return data;
    }

    sealed class InMemoryTimelineStore : IStoryChoiceTimelineStore
    {
        public StoryChoiceTimeline Timeline;
        public bool SaveSucceeds = true;
        public bool TryLoad(string storyId, out StoryChoiceTimeline timeline)
        {
            timeline = Timeline;
            return timeline != null;
        }
        public bool Save(StoryChoiceTimeline timeline)
        {
            if (!SaveSucceeds)
                return false;
            Timeline = timeline;
            return true;
        }
        public void Delete(string storyId) => Timeline = null;
    }
}
