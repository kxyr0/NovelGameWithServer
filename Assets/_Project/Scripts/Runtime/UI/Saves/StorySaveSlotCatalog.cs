using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct StorySaveSlotEntry
{
    public StorySaveSlotEntry(int slot, SaveData save, DateTime savedAtUtc)
    {
        Slot = slot;
        Save = save;
        SavedAtUtc = savedAtUtc;
    }

    public int Slot { get; }
    public SaveData Save { get; }
    public DateTime SavedAtUtc { get; }
}

public static class StorySaveSlotCatalog
{
    public static List<StorySaveSlotEntry> Read(
        string storyId,
        int firstSlot,
        int lastSlot,
        bool newestFirst,
        out int firstFreeSlot)
    {
        var result = new List<StorySaveSlotEntry>();
        firstFreeSlot = -1;

        SaveManager manager = SaveManager.Instance;
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (manager == null || string.IsNullOrEmpty(storyId))
            return result;

        firstSlot = Mathf.Clamp(firstSlot, 1, SavePathResolver.MaxSaveSlot);
        lastSlot = Mathf.Clamp(lastSlot, firstSlot, SavePathResolver.MaxSaveSlot);

        for (int slot = firstSlot; slot <= lastSlot; slot++)
        {
            SaveData save = manager.LoadForStorySlotIfExists(storyId, slot);
            if (save == null || !save.HasPosition)
            {
                if (firstFreeSlot < 0)
                    firstFreeSlot = slot;

                continue;
            }

            result.Add(new StorySaveSlotEntry(
                slot,
                save,
                StorySaveMetadataResolver.ResolveSavedAtUtc(save)));
        }

        result.Sort((left, right) => newestFirst
            ? right.SavedAtUtc.CompareTo(left.SavedAtUtc)
            : left.SavedAtUtc.CompareTo(right.SavedAtUtc));

        return result;
    }
}
