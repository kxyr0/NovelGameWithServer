using System;
using System.Collections;
using UnityEngine;

public class AutoSaveSystem : MonoBehaviour
{
    [Min(1f)] public float interval = 30f;

    Coroutine _routine;

    void OnValidate()
    {
        interval = Mathf.Max(1f, interval);
    }

    void OnEnable()
    {
        if (!Application.isPlaying || _routine != null)
            return;

        _routine = StartCoroutine(AutoSaveLoop());
    }

    void OnDisable()
    {
        if (_routine == null)
            return;

        StopCoroutine(_routine);
        _routine = null;
    }

    IEnumerator AutoSaveLoop()
    {
        var wait = new WaitForSecondsRealtime(Mathf.Max(1f, interval));

        while (enabled)
        {
            wait.waitTime = Mathf.Max(1f, interval);
            yield return wait;
            TryAutoSave();
        }
    }

    void TryAutoSave()
    {
        long startedAt = AppDiagnostics.StartTimer();
        try
        {
            SaveManager saveManager = SaveManager.Instance;
            StoryManager storyManager = StoryManager.Instance;

            if (saveManager == null || storyManager == null || !storyManager.HasSelectedStory)
                return;

            int saveSlot = storyManager.ResolveProgressSaveSlot();

            // StoryManager already persists progress while the player advances.
            // Do not serialize/write the same state every N seconds when nothing changed.
            if (!saveManager.HasUnsavedRuntimeState(storyManager, saveSlot))
                return;

            saveManager.SaveCurrentDataLightweight(saveSlot, storyManager);
        }
        catch (Exception exception)
        {
            AppDiagnostics.LogOperationFailed(
                AppLogCategory.SaveSystem,
                nameof(AutoSaveSystem),
                nameof(TryAutoSave),
                "[SAVE][AUTOSAVE_FAILURE] Autosave failed.",
                startedAt,
                exception,
                LogMetadata.Of("errorType", exception.GetType().Name),
                recoverable: true);
        }
    }
}
