using UnityEngine;

public enum StoryStartLoadingProgressPhase
{
    Idle = 0,
    Loading = 1,
    Completing = 2,
    Complete = 3
}

public readonly struct StoryStartLoadingProgressSnapshot
{
    public StoryStartLoadingProgressSnapshot(float visibleProgress, string status, StoryStartLoadingProgressPhase phase)
    {
        VisibleProgress = Mathf.Clamp01(visibleProgress);
        Status = status ?? "";
        Phase = phase;
    }

    public float VisibleProgress { get; }
    public string Status { get; }
    public StoryStartLoadingProgressPhase Phase { get; }
}

public sealed class StoryStartLoadingProgressModel
{
    private readonly float _fakeProgressCeiling;
    private readonly float _fakeProgressDuration;
    private readonly float _progressCatchUpSpeed;
    private readonly AnimationCurve _fakeProgressCurve;
    private string _fallbackStatus;

    public StoryStartLoadingProgressModel(
        float fakeProgressCeiling,
        float fakeProgressDuration,
        float progressCatchUpSpeed,
        AnimationCurve fakeProgressCurve,
        string initialStatus)
    {
        _fakeProgressCeiling = Mathf.Clamp(fakeProgressCeiling, 0.1f, 0.99f);
        _fakeProgressDuration = Mathf.Max(0.05f, fakeProgressDuration);
        _progressCatchUpSpeed = Mathf.Max(0.01f, progressCatchUpSpeed);
        _fakeProgressCurve = fakeProgressCurve;
        Reset(initialStatus);
    }

    public float VisibleProgress { get; private set; }
    public float RealProgress { get; private set; }
    public string Status { get; private set; }
    public StoryStartLoadingProgressPhase Phase { get; private set; }

    public void Reset(string initialStatus)
    {
        _fallbackStatus = initialStatus ?? "";
        VisibleProgress = 0f;
        RealProgress = 0f;
        Status = _fallbackStatus;
        Phase = StoryStartLoadingProgressPhase.Loading;
    }

    public void Report(float normalizedProgress, string status)
    {
        RealProgress = Mathf.Clamp01(normalizedProgress);
        ReportStatus(status);
    }

    public void ReportStatus(string status)
    {
        Status = string.IsNullOrWhiteSpace(status) ? _fallbackStatus : status;
        if (Phase == StoryStartLoadingProgressPhase.Idle)
            Phase = StoryStartLoadingProgressPhase.Loading;
    }

    public StoryStartLoadingProgressSnapshot TickLoading(float elapsed, float deltaTime)
    {
        Phase = StoryStartLoadingProgressPhase.Loading;

        float fakeProgress = EvaluateFakeProgress(elapsed);
        float targetProgress = Mathf.Max(fakeProgress, RealProgress * _fakeProgressCeiling);
        float maxDelta = _progressCatchUpSpeed * Mathf.Max(0f, deltaTime);
        VisibleProgress = Mathf.MoveTowards(VisibleProgress, Mathf.Clamp01(targetProgress), maxDelta);

        return Snapshot();
    }

    public StoryStartLoadingProgressSnapshot TickCompleting(
        float startProgress,
        float elapsed,
        float duration,
        string completeStatus)
    {
        Phase = StoryStartLoadingProgressPhase.Completing;
        Status = string.IsNullOrWhiteSpace(completeStatus) ? Status : completeStatus;

        float safeDuration = Mathf.Max(0f, duration);
        float t = safeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / safeDuration);
        VisibleProgress = Mathf.SmoothStep(Mathf.Clamp01(startProgress), 1f, t);

        if (t >= 1f)
            Phase = StoryStartLoadingProgressPhase.Complete;

        return Snapshot();
    }

    public StoryStartLoadingProgressSnapshot Complete(string completeStatus)
    {
        RealProgress = 1f;
        VisibleProgress = 1f;
        Status = string.IsNullOrWhiteSpace(completeStatus) ? Status : completeStatus;
        Phase = StoryStartLoadingProgressPhase.Complete;
        return Snapshot();
    }

    public StoryStartLoadingProgressSnapshot Snapshot()
    {
        return new StoryStartLoadingProgressSnapshot(VisibleProgress, Status, Phase);
    }

    private float EvaluateFakeProgress(float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / _fakeProgressDuration);
        float curve = _fakeProgressCurve != null ? _fakeProgressCurve.Evaluate(t) : t;
        return Mathf.Clamp01(curve) * _fakeProgressCeiling;
    }
}
