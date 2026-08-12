using System;
using System.Globalization;

internal static class AdDailyLimitStore
{
    private const string KeyPrefix = "VN_YANDEX_REWARDED_";
    private const string PurposePrefix = "yandex_rewarded_daily:";

    public static int GetUsedToday(int limit)
    {
        limit = Math.Max(1, limit);
        string suffix = GetPlayerSuffix();
        string dayKey = KeyPrefix + "DAY_" + suffix;
        string countKey = KeyPrefix + "COUNT_" + suffix;
        int today = GetUtcDay();
        int storedDay = LocalSecurePrefs.GetInt(dayKey, PurposePrefix + "day:" + suffix, 0);
        int used = Math.Max(0, LocalSecurePrefs.GetInt(countKey, PurposePrefix + "count:" + suffix, 0));

        if (storedDay == today || storedDay > today)
            return Math.Min(limit, used);

        LocalSecurePrefs.SetInt(dayKey, PurposePrefix + "day:" + suffix, today);
        LocalSecurePrefs.SetInt(countKey, PurposePrefix + "count:" + suffix, 0);
        return 0;
    }

    public static int GetRemainingToday(int limit)
    {
        limit = Math.Max(1, limit);
        return Math.Max(0, limit - GetUsedToday(limit));
    }

    public static int CommitReward(int limit)
    {
        limit = Math.Max(1, limit);
        string suffix = GetPlayerSuffix();
        int used = Math.Min(limit, GetUsedToday(limit) + 1);
        LocalSecurePrefs.SetInt(
            KeyPrefix + "COUNT_" + suffix,
            PurposePrefix + "count:" + suffix,
            used);
        return used;
    }

    private static string GetPlayerSuffix()
    {
        string id = NetworkManager.CurrentProfile != null
            ? NetworkManager.CurrentProfile.playerId
            : "";
        id = SaveDataSanitizer.SanitizeIdentifier(id);
        return string.IsNullOrEmpty(id) ? "local_device" : id;
    }

    private static int GetUtcDay()
    {
        return int.Parse(DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}
