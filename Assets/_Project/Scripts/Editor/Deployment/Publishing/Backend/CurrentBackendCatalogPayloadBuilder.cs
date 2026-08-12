#if UNITY_EDITOR
using System;
using System.Text;

public static class CurrentBackendCatalogPayloadBuilder
{
    public static string BuildStory(
        string storyId,
        string title,
        bool allowHeroRename,
        out string error)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        title = SaveDataSanitizer.SanitizeHistoryLine(title);
        if (string.IsNullOrWhiteSpace(storyId))
            return Fail("Укажите ID истории.", out error);
        if (string.IsNullOrWhiteSpace(title))
            return Fail("Укажите название истории.", out error);

        error = "";
        return "{" + StringField("storyId", storyId) + "," +
            StringField("title", title) + "," +
            BoolField("allowHeroRename", allowHeroRename) + "}";
    }

    public static string BuildSeason(
        string seasonId,
        string title,
        int order,
        out string error)
    {
        seasonId = SaveDataSanitizer.SanitizeIdentifier(seasonId);
        title = SaveDataSanitizer.SanitizeHistoryLine(title);
        if (string.IsNullOrWhiteSpace(seasonId))
            return Fail("Укажите ID сезона.", out error);
        if (string.IsNullOrWhiteSpace(title))
            return Fail("Укажите название сезона.", out error);

        error = "";
        return "{" + StringField("seasonId", seasonId) + "," +
            StringField("title", title) + "," +
            NumberField("order", Math.Max(0, order)) + "}";
    }

    public static string BuildEpisode(
        string episodeId,
        string title,
        bool isPremium,
        int candleCost,
        int order,
        bool geoRestricted,
        out string error)
    {
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        title = SaveDataSanitizer.SanitizeHistoryLine(title);
        if (string.IsNullOrWhiteSpace(episodeId))
            return Fail("Укажите ID эпизода.", out error);
        if (string.IsNullOrWhiteSpace(title))
            return Fail("Укажите название эпизода.", out error);

        error = "";
        var builder = new StringBuilder("{");
        builder.Append(StringField("episodeId", episodeId)).Append(',');
        builder.Append(StringField("title", title)).Append(',');
        builder.Append(BoolField("isPremium", isPremium)).Append(',');
        builder.Append(NumberField("candleCost", Math.Max(0, candleCost))).Append(',');
        builder.Append(NumberField("order", Math.Max(0, order))).Append(',');
        builder.Append(BoolField("geoRestricted", geoRestricted)).Append('}');
        return builder.ToString();
    }

    private static string StringField(string key, string value)
    {
        return "\"" + key + "\":\"" + NetworkJson.Escape(value) + "\"";
    }

    private static string BoolField(string key, bool value)
    {
        return "\"" + key + "\":" + (value ? "true" : "false");
    }

    private static string NumberField(string key, int value)
    {
        return "\"" + key + "\":" + value;
    }

    private static string Fail(string message, out string error)
    {
        error = message;
        return "";
    }
}
#endif
