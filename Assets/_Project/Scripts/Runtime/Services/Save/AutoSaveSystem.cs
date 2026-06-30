using UnityEngine;

public class AutoSaveSystem : MonoBehaviour
{
    public float interval = 30f;
    float timer;

    void OnValidate()
    {
        interval = Mathf.Max(1f, interval);
    }

    void Update()
    {
        if (interval <= 0f)
            interval = 1f;

        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0;
            TryAutoSave();
        }
    }

    void TryAutoSave()
    {
        long startedAt = AppDiagnostics.StartTimer();
        try
        {
            if (SaveManager.Instance != null && StoryManager.Instance != null && StoryManager.Instance.HasSelectedStory)
            {
                AppLogger.Info(
                    AppLogCategory.SaveSystem,
                    nameof(AutoSaveSystem),
                    nameof(TryAutoSave),
                    "[SAVE][AUTOSAVE_START] Autosave timer fired.",
                    LogMetadata.Of(
                        "storyId", StoryManager.Instance.CurrentStoryId,
                        "episodeId", StoryManager.Instance.CurrentEpisodeId,
                        "nodeGuid", GameState.Instance != null && GameState.Instance.currentNode != null ? GameState.Instance.currentNode.guid : ""));
                int saveSlot = StoryManager.Instance.ResolveProgressSaveSlot();
                SaveManager.Instance.SaveCurrentData(saveSlot, StoryManager.Instance);

                AppDiagnostics.LogOperationCompleted(
                    AppLogCategory.SaveSystem,
                    nameof(AutoSaveSystem),
                    nameof(TryAutoSave),
                    "[SAVE][AUTOSAVE_SUCCESS] Autosave request completed.",
                    startedAt,
                    LogMetadata.Of("storyId", StoryManager.Instance.CurrentStoryId));
            }
        }
        catch (System.Exception exception)
        {
            AppDiagnostics.LogOperationFailed(
                AppLogCategory.SaveSystem,
                nameof(AutoSaveSystem),
                nameof(TryAutoSave),
                "[SAVE][AUTOSAVE_FAILURE] Autosave failed.",
                startedAt,
                exception,
                LogMetadata.Of(
                    "storyId", StoryManager.Instance != null ? StoryManager.Instance.CurrentStoryId : "",
                    "errorType", exception.GetType().Name),
                recoverable: true);
            Debug.LogWarning($"AutoSaveSystem: autosave failed: {exception.Message}", this);
        }
    }
}
