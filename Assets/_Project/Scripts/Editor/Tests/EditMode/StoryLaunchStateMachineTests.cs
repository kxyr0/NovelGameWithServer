using NUnit.Framework;

public class StoryLaunchStateMachineTests
{
    [Test]
    public void StoryLaunchStateMachine_StartsIdle()
    {
        var stateMachine = new StoryLaunchStateMachine();

        Assert.That(stateMachine.IsIdle, Is.True);
        Assert.That(stateMachine.Current, Is.EqualTo(StoryLaunchState.Idle));
    }

    [Test]
    public void StoryLaunchStateMachine_EnterAndReset_UpdatesState()
    {
        var stateMachine = new StoryLaunchStateMachine();

        stateMachine.Enter(StoryLaunchState.AskingHeroName);

        Assert.That(stateMachine.IsIdle, Is.False);
        Assert.That(stateMachine.Is(StoryLaunchState.AskingHeroName), Is.True);

        stateMachine.Reset();

        Assert.That(stateMachine.IsIdle, Is.True);
        Assert.That(stateMachine.ToString(), Is.EqualTo(nameof(StoryLaunchState.Idle)));
    }
}
