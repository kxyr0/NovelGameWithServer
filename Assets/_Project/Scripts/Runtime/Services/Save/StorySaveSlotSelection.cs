using UnityEngine;

public static class StorySaveSlotSelection
{
    public const int DefaultSlot = 1;

    const string KeyPrefix = "story_save_selected_slot_";
    const string PurposePrefix = "selected_story_save_slot";

    public static int GetSelectedSlot(string storyId)
    {
        return TryGetSelectedSlot(storyId, out int slot) ? slot : DefaultSlot;
    }

    public static bool TryGetSelectedSlot(string storyId, out int slot)
    {
        slot = DefaultSlot;
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (string.IsNullOrEmpty(storyId))
            return false;

        string key = GetPrefsKey(storyId);
        if (!PlayerPrefs.HasKey(key))
            return false;

        int storedSlot = LocalSecurePrefs.GetInt(key, GetPurpose(storyId), DefaultSlot);
        if (!SavePathResolver.IsValidSlot(storedSlot))
        {
            ClearSelectedSlot(storyId);
            return false;
        }

        slot = storedSlot;
        return true;
    }

    public static bool IsSelectedSlot(string storyId, int slot)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        return !string.IsNullOrEmpty(storyId) &&
               SavePathResolver.IsValidSlot(slot) &&
               GetSelectedSlot(storyId) == slot;
    }

    public static void SelectSlot(string storyId, int slot)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (string.IsNullOrEmpty(storyId) || !SavePathResolver.IsValidSlot(slot))
            return;

        LocalSecurePrefs.SetInt(GetPrefsKey(storyId), GetPurpose(storyId), slot);
    }

    public static void ClearSelectedSlot(string storyId)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (string.IsNullOrEmpty(storyId))
            return;

        LocalSecurePrefs.Delete(GetPrefsKey(storyId));
    }

    static string GetPrefsKey(string storyId)
    {
        return KeyPrefix + SaveDataSanitizer.SafeKeyPart(storyId, "story", 96);
    }

    static string GetPurpose(string storyId)
    {
        return LocalSaveSecurity.SetupFlagPurpose + ":" + PurposePrefix + ":" + SaveDataSanitizer.SafeKeyPart(storyId, "story", 96);
    }
}
