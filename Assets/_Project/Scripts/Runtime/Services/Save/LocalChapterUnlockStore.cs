public static class LocalChapterUnlockStore
{
    public static bool IsUnlocked(string key)
    {
        key = NormalizeKey(key);
        if (string.IsNullOrEmpty(key))
            return false;

        return LocalSecurePrefs.GetBool(key, GetPurpose(key), false);
    }

    public static void SetUnlocked(string key, bool unlocked)
    {
        key = NormalizeKey(key);
        if (string.IsNullOrEmpty(key))
            return;

        LocalSecurePrefs.SetBool(key, GetPurpose(key), unlocked);
    }

    static string GetPurpose(string key)
    {
        return LocalSaveSecurity.ChapterUnlockPurpose + ":" + SaveDataSanitizer.SanitizeIdentifier(key);
    }

    static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? "" : key.Trim();
    }
}
