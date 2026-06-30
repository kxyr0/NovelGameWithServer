using System;

public sealed class StoryStartPreloadProgressReporter : IProgress<StoryStartPreloadProgress>
{
    private readonly Action<StoryStartPreloadProgress> _onProgress;

    public StoryStartPreloadProgressReporter(Action<StoryStartPreloadProgress> onProgress)
    {
        _onProgress = onProgress;
    }

    public void Report(StoryStartPreloadProgress value)
    {
        _onProgress?.Invoke(value);
    }
}
