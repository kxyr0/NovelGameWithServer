public enum StoryLaunchState
{
    Idle,
    AskingHeroName,
    OpeningWardrobe,
    WaitingForWardrobe,
    StartingStory
}

public sealed class StoryLaunchStateMachine
{
    public StoryLaunchState Current { get; private set; } = StoryLaunchState.Idle;
    public bool IsIdle => Current == StoryLaunchState.Idle;

    public bool Is(StoryLaunchState state)
    {
        return Current == state;
    }

    public void Enter(StoryLaunchState state)
    {
        Current = state;
    }

    public void Reset()
    {
        Current = StoryLaunchState.Idle;
    }

    public override string ToString()
    {
        return Current.ToString();
    }
}
