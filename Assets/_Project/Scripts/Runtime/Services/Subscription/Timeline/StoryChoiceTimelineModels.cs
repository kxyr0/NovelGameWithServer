using System;
using System.Collections.Generic;

[Serializable]
public sealed class StoryChoiceCheckpoint
{
    public int schemaVersion = 1;
    public string checkpointId = "";
    public string storyId = "";
    public string episodeId = "";
    public string choiceNodeGuid = "";
    public int selectedIndex = -1;
    public string createdAtUtc = "";
    public SaveData snapshot;
}

[Serializable]
public sealed class StoryChoiceTimeline
{
    public int schemaVersion = 1;
    public string storyId = "";
    public int currentIndex = -1;
    public List<StoryChoiceCheckpoint> checkpoints = new List<StoryChoiceCheckpoint>();
}

public readonly struct StoryChoiceTimelineState
{
    public readonly bool CanRewindBack;
    public readonly bool CanRewindForward;
    public readonly bool CanUndoChoice;
    public readonly bool Busy;

    public StoryChoiceTimelineState(bool back, bool forward, bool undo, bool busy)
    {
        CanRewindBack = back;
        CanRewindForward = forward;
        CanUndoChoice = undo;
        Busy = busy;
    }
}
