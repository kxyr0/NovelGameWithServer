using UnityEngine;

public partial class SaveManager
{
    /// <summary>
    /// Creates a NEW story save from the current runtime state and makes that slot
    /// the active branch for subsequent autosaves/progress saves.
    /// The previously selected save is left untouched.
    /// </summary>
    public SaveData CreateNewSaveFromCurrent(
        int slot,
        StoryManager storyManager = null)
    {
        if (!SavePathResolver.IsValidSlot(slot))
        {
            Debug.LogWarning(
                $"[SAVE][CREATE_NEW_REJECTED] Invalid slot: {slot}.",
                this);
            return null;
        }

        if (storyManager == null)
            storyManager = StoryManager.Instance;

        SaveData data = BuildCurrentSaveData(storyManager);
        if (data == null || !data.HasPosition)
        {
            Debug.LogWarning(
                "[SAVE][CREATE_NEW_REJECTED] Current runtime state has no restorable position.",
                this);
            return null;
        }

        string storyId = SaveDataSanitizer.SanitizeIdentifier(data.storyId);
        if (string.IsNullOrEmpty(storyId))
        {
            Debug.LogWarning(
                "[SAVE][CREATE_NEW_REJECTED] Current runtime state has no story id.",
                this);
            return null;
        }

        if (LoadForStorySlotIfExists(storyId, slot) != null)
        {
            Debug.LogWarning(
                $"[SAVE][CREATE_NEW_REJECTED] Story save already exists. storyId='{storyId}', slot={slot}.",
                this);
            return null;
        }

        SaveData safeData = SaveDataSanitizer.SanitizeCopy(data);
        if (safeData == null || !safeData.HasPosition)
            return null;

        string storyPath = Persistence.Paths.GetStorySavePath(slot, storyId);
        SaveOperationResult writeResult = Persistence.WriteSaveFile(
            storyPath,
            safeData,
            slot,
            "manual-new",
            nameof(SaveManager));

        if (!writeResult.Success)
        {
            Debug.LogWarning(
                $"[SAVE][CREATE_NEW_FAILED] Could not create story save. storyId='{storyId}', slot={slot}, " +
                $"error='{writeResult.ErrorType}', message='{writeResult.Message}'.",
                this);
            return null;
        }

        StorySaveSlotSelection.SelectSlot(storyId, slot);
        Persistence.CreateSnapshot(safeData, slot, nameof(CreateNewSaveFromCurrent));
        NotifyStorySaveChanged(storyId);

        Debug.Log(
            $"[SAVE][CREATE_NEW_SUCCESS] Created and selected a new story branch. storyId='{storyId}', slot={slot}.",
            this);

        return safeData;
    }

    /// <summary>
    /// Prepares an EMPTY slot as a completely fresh playthrough without touching
    /// any existing save files. The normal MenuController start flow creates the
    /// first snapshot when the story begins progressing.
    /// </summary>
    public bool PrepareFreshPlaythroughSlot(StoryData storyData, int slot)
    {
        if (storyData == null || !SavePathResolver.IsValidSlot(slot))
        {
            Debug.LogWarning(
                $"[SAVE][FRESH_PLAYTHROUGH_REJECTED] Invalid story or slot. slot={slot}.",
                this);
            return false;
        }

        string storyId = SaveDataSanitizer.SanitizeIdentifier(storyData.StoryId);
        if (string.IsNullOrEmpty(storyId))
            storyId = SaveDataSanitizer.SanitizeIdentifier(storyData.storyId);
        if (string.IsNullOrEmpty(storyId))
            storyId = SaveDataSanitizer.SanitizeIdentifier(storyData.name);

        if (string.IsNullOrEmpty(storyId))
        {
            Debug.LogWarning(
                "[SAVE][FRESH_PLAYTHROUGH_REJECTED] Story has no usable id.",
                this);
            return false;
        }

        // CREATE-only semantics: a stale UI free-slot value must never overwrite a save.
        if (LoadForStorySlotIfExists(storyId, slot) != null)
        {
            Debug.LogWarning(
                $"[SAVE][FRESH_PLAYTHROUGH_REJECTED] Target slot is already occupied. storyId='{storyId}', slot={slot}.",
                this);
            return false;
        }

        StorySaveSlotSelection.SelectSlot(storyId, slot);

        // Reset only the active runtime/story-scoped state. Existing save files remain
        // untouched and can still be loaded later as independent playthroughs.
        StoryProgressResetUtility.ResetStoryRuntimeStateForFreshSlot(
            storyData,
            storyId);

        NetworkManager.ClearLocalProgressCache(clearPendingSync: true);

        Debug.Log(
            $"[SAVE][FRESH_PLAYTHROUGH_READY] Fresh playthrough prepared. storyId='{storyId}', slot={slot}.",
            this);

        return true;
    }
}
