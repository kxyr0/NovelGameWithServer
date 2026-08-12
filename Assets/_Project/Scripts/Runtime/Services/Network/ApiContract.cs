using System;
using System.Collections.Generic;
using UnityEngine.Networking;

public enum ApiAuthRequirement
{
    None,
    BearerJwt,
    AdminKey
}

public enum ApiEndpointKind
{
    Public,
    Auth,
    Player,
    Content,
    Shop,
    Purchase,
    Admin,
    UnityPublisher,
    Legacy,
    UndocumentedDiagnostics
}

public enum ApiRateLimitGroup
{
    None,
    Auth,
    AdminAuth,
    Player,
    Sensitive
}

public sealed class ApiEndpoint
{
    public readonly string Name;
    public readonly string Method;
    public readonly string PathTemplate;
    public readonly ApiAuthRequirement AuthRequirement;
    public readonly ApiEndpointKind Kind;
    public readonly ApiRateLimitGroup PrimaryRateLimit;
    public readonly bool Sensitive;
    public readonly bool RuntimeAllowed;
    public readonly bool Documented;
    public readonly string[] RequiredRequestFields;
    public readonly string[] SupportedExtensionFields;
    public readonly string Notes;

    public ApiEndpoint(
        string name,
        string method,
        string pathTemplate,
        ApiAuthRequirement authRequirement,
        ApiEndpointKind kind,
        ApiRateLimitGroup primaryRateLimit,
        bool sensitive,
        bool runtimeAllowed,
        bool documented,
        string[] requiredRequestFields = null,
        string[] supportedExtensionFields = null,
        string notes = "")
    {
        Name = name ?? "";
        Method = ApiContract.NormalizeMethod(method);
        PathTemplate = ApiContract.NormalizePath(pathTemplate);
        AuthRequirement = authRequirement;
        Kind = kind;
        PrimaryRateLimit = primaryRateLimit;
        Sensitive = sensitive;
        RuntimeAllowed = runtimeAllowed;
        Documented = documented;
        RequiredRequestFields = requiredRequestFields ?? new string[0];
        SupportedExtensionFields = supportedExtensionFields ?? new string[0];
        Notes = notes ?? "";
    }

    public bool Matches(string method, string path)
    {
        if (!string.Equals(Method, ApiContract.NormalizeMethod(method), StringComparison.Ordinal))
            return false;

        string normalizedPath = ApiContract.NormalizePath(path);
        if (string.Equals(PathTemplate, normalizedPath, StringComparison.Ordinal))
            return true;

        return MatchesPathTemplate(normalizedPath);
    }

    private bool MatchesPathTemplate(string normalizedPath)
    {
        string[] templateSegments = PathTemplate.Split('/');
        string[] pathSegments = normalizedPath.Split('/');
        if (templateSegments.Length != pathSegments.Length)
            return false;

        for (int i = 0; i < templateSegments.Length; i++)
        {
            string template = templateSegments[i];
            if (template.Length >= 2 && template[0] == '{' && template[template.Length - 1] == '}')
            {
                if (string.IsNullOrEmpty(pathSegments[i]))
                    return false;
                continue;
            }

            if (!string.Equals(template, pathSegments[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}

public static class ApiRoutes
{
    public const string BaseUrl = "https://nocturnedc.ru";

    public const string AuthGuest = "/auth/guest";
    public const string AuthRefresh = "/auth/refresh";
    public const string AuthRestore = "/auth/restore";
    public const string AuthSocial = "/auth/social";
    public const string Health = "/health";

    public const string ContentCatalog = "/content/catalog";
    public const string ContentEpisodeUnlock = "/content/episode/unlock";
    public const string ContentUiTexts = "/content/ui-texts";

    public const string PlayerProgress = "/player/progress";
    public const string PlayerProgressSave = "/player/progress/save";
    public const string PlayerProgressUndoChoice = "/player/progress/undo-choice";
    public const string PlayerProgressRewind = "/player/progress/rewind";
    public const string PlayerSubscriptionEntitlement = "/player/subscription/entitlement";
    public const string PlayerProfile = "/player/profile";
    public const string PlayerFeatures = "/player/features";
    public const string PlayerHeroName = "/player/hero-name";
    public const string PlayerBookmark = "/player/bookmark";
    public const string PlayerBookmarkSave = "/player/bookmark/save";
    public const string PlayerPushToken = "/player/push-token";
    public const string PlayerBalance = "/player/balance";
    public const string PlayerCandlesSpend = "/player/candles/spend";
    public const string PlayerHeartsSpend = "/player/hearts/spend";
    public const string PlayerWardrobe = "/player/wardrobe";
    public const string PlayerWardrobeBuy = "/player/wardrobe/buy";
    public const string PlayerAdReward = "/player/ad/reward";
    public const string PlayerDailyClaim = "/player/daily/claim";
    public const string PlayerSlots = "/player/slots";
    public const string PlayerSlotSwitch = "/player/slot/switch";
    public const string PlayerSlotFork = "/player/slot/fork";
    public const string PlayerStoryReset = "/player/story/reset";
    public const string PlayerScenesViewed = "/player/scenes/viewed";
    public const string PlayerGallery = "/player/gallery";
    public const string PlayerGalleryUnlock = "/player/gallery/unlock";
    public const string PlayerTarotStatus = "/player/tarot/status";
    public const string PlayerTarotDraw = "/player/tarot/draw";
    public const string PlayerCatName = "/player/cat/name";
    public const string PlayerDiceStatus = "/player/dice/status";
    public const string PlayerDiceRoll = "/player/dice/roll";
    public const string PlayerFavorites = "/player/favorites";
    public const string PlayerRelationships = "/player/relationships";
    public const string PlayerRelationshipUnlock = "/player/relationship/unlock";
    public const string PlayerRelationshipUpdate = "/player/relationship/update";
    public const string PlayerPromoApply = "/player/promo/apply";
    public const string PlayerEpisodeComplete = "/player/episode/complete";
    public const string PlayerEpisodeReplay = "/player/episode/replay";
    public const string PlayerEpisodeJump = "/player/episode/jump";
    public const string PlayerSeasonRestart = "/player/season/restart";
    public const string PlayerChapterComplete = "/player/chapter/complete";
    public const string PlayerStoryComplete = "/player/story/complete";
    public const string PlayerReadingStats = "/player/reading-stats";

    public const string ShopPrices = "/shop/prices";
    public const string ShopItems = "/shop/items";
    public const string ShopOrders = "/shop/orders";

    public const string PurchasesConfirm = "/purchases/confirm";
    public const string PurchasesRestore = "/purchases/restore";
    public const string PurchasesHistory = "/purchases/history";
    public const string PurchasesProducts = "/purchases/products";

    public const string UnityChoiceCosts = "/unity/choice-costs";
    public const string UnityWardrobeCosts = "/unity/wardrobe-costs";

    public const string AdminPlayers = "/admin/players";
    public const string AdminBulkBalance = "/admin/bulk/balance";

    public static string ContentEpisodeGraph(string episodeId)
        => "/content/episode/" + EscapePathSegment(episodeId) + "/graph";

    public static string ContentEpisodeVersion(string episodeId)
        => "/content/episode/" + EscapePathSegment(episodeId) + "/version";

    public static string ContentUiTextsQuery(string screenId, string storyId, string locale)
    {
        var query = new List<string>(3);
        if (!string.IsNullOrEmpty(locale))
            query.Add("locale=" + EscapeQueryValue(locale));
        if (!string.IsNullOrEmpty(screenId))
            query.Add("screenId=" + EscapeQueryValue(screenId));
        if (!string.IsNullOrEmpty(storyId))
            query.Add("storyId=" + EscapeQueryValue(storyId));

        return query.Count == 0 ? ContentUiTexts : ContentUiTexts + "?" + string.Join("&", query.ToArray());
    }

    public static string PlayerHeroNameForStory(string storyId)
    {
        return string.IsNullOrEmpty(storyId)
            ? PlayerHeroName
            : PlayerHeroName + "?storyId=" + UnityWebRequest.EscapeURL(storyId);
    }

    public static string PlayerScenesViewedForEpisode(string episodeId)
        => PlayerScenesViewed + "?episodeId=" + UnityWebRequest.EscapeURL(episodeId ?? "");

    public static string PlayerCatGreeting(int hour)
        => "/player/cat/greet?hour=" + Math.Max(0, Math.Min(23, hour));

    public static string PlayerFavoriteForStory(string storyId)
        => PlayerFavorites + "/" + EscapePathSegment(storyId);

    public static string PlayerFavoriteCheck(string storyId)
        => PlayerFavoriteForStory(storyId) + "/check";

    public static string UnityChoiceCostByNode(string nodeGuid)
        => UnityChoiceCosts + "/" + EscapePathSegment(nodeGuid);

    public static string AdminPlayer(string playerId)
        => AdminPlayers + "/" + EscapePathSegment(playerId);

    public static string AdminPlayerBalance(string playerId)
        => AdminPlayer(playerId) + "/balance";

    public static string UnityChoiceCostsQuery(string storyId, string episodeId)
    {
        var query = new List<string>(2);
        if (!string.IsNullOrEmpty(storyId))
            query.Add("storyId=" + UnityWebRequest.EscapeURL(storyId));
        if (!string.IsNullOrEmpty(episodeId))
            query.Add("episodeId=" + UnityWebRequest.EscapeURL(episodeId));

        return query.Count == 0 ? UnityChoiceCosts : UnityChoiceCosts + "?" + string.Join("&", query.ToArray());
    }

    private static string EscapeQueryValue(string value)
        => UnityWebRequest.EscapeURL(value ?? "").Replace("+", "%20");

    public static string EscapePathSegment(string value)
        => UnityWebRequest.EscapeURL(value ?? "");
}

public static class ApiContract
{
    public const string DocumentationUrl = "https://nocturnedc.ru/api-docs";

    private static readonly ApiEndpoint[] Endpoints =
    {
        Ep("AuthGuest", "POST", ApiRoutes.AuthGuest, ApiAuthRequirement.None, ApiEndpointKind.Auth, ApiRateLimitGroup.Auth, false, true, true, new[] { "deviceId" }),
        Ep("AuthRefresh", "POST", ApiRoutes.AuthRefresh, ApiAuthRequirement.None, ApiEndpointKind.Auth, ApiRateLimitGroup.Auth, false, true, true, new[] { "refreshToken" }),
        Ep("AuthRestore", "POST", ApiRoutes.AuthRestore, ApiAuthRequirement.None, ApiEndpointKind.Auth, ApiRateLimitGroup.Auth, false, true, true, new[] { "restoreCode", "deviceId" }),
        Ep("AuthSocial", "POST", ApiRoutes.AuthSocial, ApiAuthRequirement.None, ApiEndpointKind.Auth, ApiRateLimitGroup.Auth, false, true, true, new[] { "provider", "idToken", "deviceId" }),
        Ep("Health", "GET", ApiRoutes.Health, ApiAuthRequirement.None, ApiEndpointKind.UndocumentedDiagnostics, ApiRateLimitGroup.None, false, false, false, null, null, "Optional connectivity probe; not present in public API docs."),

        Ep("ContentCatalog", "GET", ApiRoutes.ContentCatalog, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Content, ApiRateLimitGroup.Player, false, true, true),
        Ep("ContentUiTexts", "GET", ApiRoutes.ContentUiTexts, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Content, ApiRateLimitGroup.Player, false, true, true, null, new[] { "id", "text", "enabled", "locale", "screenId", "storyId", "updatedAt", "version" }),
        Ep("ContentEpisodeUnlock", "POST", ApiRoutes.ContentEpisodeUnlock, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Content, ApiRateLimitGroup.Player, true, true, true, new[] { "episodeId" }),
        Ep("ContentEpisodeGraph", "GET", "/content/episode/{episodeId}/graph", ApiAuthRequirement.BearerJwt, ApiEndpointKind.Content, ApiRateLimitGroup.Player, false, true, true),
        Ep("ContentEpisodeVersion", "GET", "/content/episode/{episodeId}/version", ApiAuthRequirement.BearerJwt, ApiEndpointKind.Content, ApiRateLimitGroup.Player, false, true, true),

        Ep("PlayerProgress", "GET", ApiRoutes.PlayerProgress, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerProgressSave", "POST", ApiRoutes.PlayerProgressSave, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "episodeId", "nodeId", "stats", "variables" }, new[] { "storyId", "currentEpisodeId", "currentNodeGuid", "flags", "snapshot", "unlockedEpisodes" }),
        Ep("PlayerSubscriptionEntitlement", "GET", ApiRoutes.PlayerSubscriptionEntitlement, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerProgressUndoChoice", "POST", ApiRoutes.PlayerProgressUndoChoice, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, true, true, true),
        Ep("PlayerProgressRewind", "POST", ApiRoutes.PlayerProgressRewind, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, true, true, true, new[] { "episodeId", "nodeId" }),
        Ep("PlayerProfile", "GET", ApiRoutes.PlayerProfile, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerFeatures", "GET", ApiRoutes.PlayerFeatures, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerHeroNameGet", "GET", ApiRoutes.PlayerHeroName, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerHeroNamePost", "POST", ApiRoutes.PlayerHeroName, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "storyId", "name" }),
        Ep("PlayerBookmark", "GET", ApiRoutes.PlayerBookmark, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerBookmarkSave", "POST", ApiRoutes.PlayerBookmarkSave, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerPushToken", "POST", ApiRoutes.PlayerPushToken, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "token" }),
        Ep("PlayerBalance", "GET", ApiRoutes.PlayerBalance, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerCandlesSpend", "POST", ApiRoutes.PlayerCandlesSpend, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, true, true, true, new[] { "amount" }),
        Ep("PlayerHeartsSpend", "POST", ApiRoutes.PlayerHeartsSpend, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, true, true, true, new[] { "amount" }),
        Ep("PlayerWardrobe", "GET", ApiRoutes.PlayerWardrobe, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerWardrobeBuy", "POST", ApiRoutes.PlayerWardrobeBuy, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Sensitive, true, true, true, new[] { "itemId" }),
        Ep("PlayerAdReward", "POST", ApiRoutes.PlayerAdReward, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, true, true, true),
        Ep("PlayerDailyClaim", "POST", ApiRoutes.PlayerDailyClaim, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, true, true, true),
        Ep("PlayerSlots", "GET", ApiRoutes.PlayerSlots, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerSlotSwitch", "POST", ApiRoutes.PlayerSlotSwitch, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, true, true, true, new[] { "slotId" }),
        Ep("PlayerSlotFork", "POST", ApiRoutes.PlayerSlotFork, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, true, true, true),
        Ep("PlayerStoryReset", "POST", ApiRoutes.PlayerStoryReset, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "storyId" }),
        Ep("PlayerScenesViewedGet", "GET", ApiRoutes.PlayerScenesViewed, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerScenesViewedPost", "POST", ApiRoutes.PlayerScenesViewed, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "episodeId", "nodeIds" }),
        Ep("PlayerGalleryGet", "GET", ApiRoutes.PlayerGallery, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerGalleryUnlock", "POST", ApiRoutes.PlayerGalleryUnlock, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "sceneId" }),
        Ep("PlayerTarotStatus", "GET", ApiRoutes.PlayerTarotStatus, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerTarotDraw", "POST", ApiRoutes.PlayerTarotDraw, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, true, true, true),
        Ep("PlayerCatGreeting", "GET", "/player/cat/greet", ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerCatNameGet", "GET", ApiRoutes.PlayerCatName, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerCatNamePost", "POST", ApiRoutes.PlayerCatName, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "name" }),
        Ep("PlayerDiceStatus", "GET", ApiRoutes.PlayerDiceStatus, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerDiceRoll", "POST", ApiRoutes.PlayerDiceRoll, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, true, true, true),
        Ep("PlayerFavoritesGet", "GET", ApiRoutes.PlayerFavorites, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerFavoritesPost", "POST", ApiRoutes.PlayerFavorites, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "storyId" }),
        Ep("PlayerFavoriteDelete", "DELETE", "/player/favorites/{storyId}", ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerFavoriteCheck", "GET", "/player/favorites/{storyId}/check", ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerRelationships", "GET", ApiRoutes.PlayerRelationships, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),
        Ep("PlayerRelationshipUnlock", "POST", ApiRoutes.PlayerRelationshipUnlock, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, true, true, true, new[] { "characterId" }),
        Ep("PlayerRelationshipUpdate", "POST", ApiRoutes.PlayerRelationshipUpdate, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, true, true, true, new[] { "characterId", "delta" }),
        Ep("PlayerPromoApply", "POST", ApiRoutes.PlayerPromoApply, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, true, true, true, new[] { "code" }),
        Ep("PlayerEpisodeComplete", "POST", ApiRoutes.PlayerEpisodeComplete, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "episodeId" }),
        Ep("PlayerEpisodeReplay", "POST", ApiRoutes.PlayerEpisodeReplay, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "episodeId" }),
        Ep("PlayerEpisodeJump", "POST", ApiRoutes.PlayerEpisodeJump, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "episodeId" }),
        Ep("PlayerSeasonRestart", "POST", ApiRoutes.PlayerSeasonRestart, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "seasonId" }),
        Ep("PlayerChapterComplete", "POST", ApiRoutes.PlayerChapterComplete, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "episodeId" }),
        Ep("PlayerStoryComplete", "POST", ApiRoutes.PlayerStoryComplete, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true, new[] { "storyId" }),
        Ep("PlayerReadingStats", "GET", ApiRoutes.PlayerReadingStats, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Player, ApiRateLimitGroup.Player, false, true, true),

        Ep("ShopPrices", "GET", ApiRoutes.ShopPrices, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Shop, ApiRateLimitGroup.Player, false, true, true, null, new[] { "buttonId", "productId", "priceLabel", "price", "sortOrder" }),
        Ep("ShopItems", "GET", ApiRoutes.ShopItems, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Shop, ApiRateLimitGroup.Player, false, true, true, null, new[] { "buttonId", "productId", "name", "title", "displayName", "amount", "candles", "hearts", "priceLabel", "sortOrder", "order", "position" }),
        Ep("ShopOrders", "POST", ApiRoutes.ShopOrders, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Shop, ApiRateLimitGroup.Player, true, true, true, new[] { "productId" }),
        Ep("PurchasesConfirm", "POST", ApiRoutes.PurchasesConfirm, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Purchase, ApiRateLimitGroup.Player, true, true, true, new[] { "productId", "receipt" }),
        Ep("PurchasesRestore", "POST", ApiRoutes.PurchasesRestore, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Purchase, ApiRateLimitGroup.Player, true, true, true, new[] { "store" }),
        Ep("PurchasesHistory", "GET", ApiRoutes.PurchasesHistory, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Purchase, ApiRateLimitGroup.Player, false, true, true),
        Ep("PurchasesProducts", "GET", ApiRoutes.PurchasesProducts, ApiAuthRequirement.BearerJwt, ApiEndpointKind.Purchase, ApiRateLimitGroup.Player, false, true, true, null, new[] { "buttonId", "productId", "name", "title", "displayName", "amount", "candles", "hearts", "priceLabel", "localizedPrice", "sortOrder", "order", "position" }),

        Ep("UnityChoiceCostsPost", "POST", ApiRoutes.UnityChoiceCosts, ApiAuthRequirement.AdminKey, ApiEndpointKind.UnityPublisher, ApiRateLimitGroup.Sensitive, true, false, true, new[] { "storyId", "episodeId", "choices" }),
        Ep("UnityChoiceCostsGet", "GET", ApiRoutes.UnityChoiceCosts, ApiAuthRequirement.AdminKey, ApiEndpointKind.UnityPublisher, ApiRateLimitGroup.Sensitive, true, false, true),
        Ep("UnityChoiceCostsDelete", "DELETE", "/unity/choice-costs/{nodeGuid}", ApiAuthRequirement.AdminKey, ApiEndpointKind.UnityPublisher, ApiRateLimitGroup.Sensitive, true, false, true),
        Ep("UnityWardrobeCostsPost", "POST", ApiRoutes.UnityWardrobeCosts, ApiAuthRequirement.AdminKey, ApiEndpointKind.UnityPublisher, ApiRateLimitGroup.Sensitive, true, false, true, new[] { "items" }),
        Ep("UnityWardrobeCostsGet", "GET", ApiRoutes.UnityWardrobeCosts, ApiAuthRequirement.AdminKey, ApiEndpointKind.UnityPublisher, ApiRateLimitGroup.Sensitive, true, false, true),
        Ep("AdminPlayersGet", "GET", ApiRoutes.AdminPlayers, ApiAuthRequirement.AdminKey, ApiEndpointKind.Admin, ApiRateLimitGroup.Sensitive, true, false, true),
        Ep("AdminPlayerGet", "GET", "/admin/players/{id}", ApiAuthRequirement.AdminKey, ApiEndpointKind.Admin, ApiRateLimitGroup.Sensitive, true, false, true),
        Ep("AdminPlayerBalancePost", "POST", "/admin/players/{id}/balance", ApiAuthRequirement.AdminKey, ApiEndpointKind.Admin, ApiRateLimitGroup.Sensitive, true, false, true, null, new[] { "hearts", "candles", "isSubscriber", "subscriptionDays" }),
        Ep("AdminBulkBalancePost", "POST", ApiRoutes.AdminBulkBalance, ApiAuthRequirement.AdminKey, ApiEndpointKind.Admin, ApiRateLimitGroup.Sensitive, true, false, true, null, new[] { "hearts", "candles", "playerIds" }),

        Ep("LegacyFavoritesAdd", "POST", "/player/favorites/add", ApiAuthRequirement.BearerJwt, ApiEndpointKind.Legacy, ApiRateLimitGroup.Player, false, false, false, new[] { "storyId" }, null, "Undocumented legacy route; do not use in runtime client."),
        Ep("LegacyRestoreWithRefreshToken", "POST", ApiRoutes.AuthRestore, ApiAuthRequirement.None, ApiEndpointKind.Legacy, ApiRateLimitGroup.Auth, false, false, false, new[] { "deviceId", "refreshToken" }, null, "Undocumented legacy shape; replaced by /auth/refresh.")
    };

    public static IReadOnlyList<ApiEndpoint> AllEndpoints => Endpoints;

    public static ApiEndpoint Find(string method, string path)
    {
        string normalizedMethod = NormalizeMethod(method);
        string normalizedPath = NormalizePath(path);
        for (int i = 0; i < Endpoints.Length; i++)
        {
            if (Endpoints[i].Matches(normalizedMethod, normalizedPath))
                return Endpoints[i];
        }

        return null;
    }

    public static bool IsRuntimeAllowed(string method, string path)
    {
        ApiEndpoint endpoint = Find(method, path);
        return endpoint != null && endpoint.RuntimeAllowed;
    }

    public static bool RequiresBearerToken(string method, string path)
    {
        ApiEndpoint endpoint = Find(method, path);
        return endpoint != null && endpoint.AuthRequirement == ApiAuthRequirement.BearerJwt;
    }

    public static bool IsAdminOrPublisher(string path)
    {
        string normalized = NormalizePath(path);
        return normalized.StartsWith("/admin/", StringComparison.Ordinal) ||
               normalized.StartsWith("/unity/", StringComparison.Ordinal);
    }

    public static string ResolveLogCategory(string method, string path)
    {
        ApiEndpoint endpoint = Find(method, path);
        if (endpoint == null)
            return AppLogCategory.Network;

        switch (endpoint.Kind)
        {
            case ApiEndpointKind.Auth:
                return AppLogCategory.Auth;
            case ApiEndpointKind.Admin:
            case ApiEndpointKind.UnityPublisher:
                return AppLogCategory.Security;
            default:
                return endpoint.Name == "PlayerProgressSave" ? AppLogCategory.SaveSystem : AppLogCategory.Network;
        }
    }

    public static bool IsSaveProgress(string method, string path)
    {
        ApiEndpoint endpoint = Find(method, path);
        return endpoint != null && endpoint.Name == "PlayerProgressSave";
    }

    public static List<string> GetRateLimitGroups(string method, string path)
    {
        var groups = new List<string>(2);
        ApiEndpoint endpoint = Find(method, path);
        if (endpoint == null)
            return groups;

        AddGroup(groups, endpoint.PrimaryRateLimit);
        if (endpoint.Sensitive)
            AddGroup(groups, ApiRateLimitGroup.Sensitive);

        return groups;
    }

    public static string NormalizeMethod(string method)
        => string.IsNullOrWhiteSpace(method) ? "" : method.Trim().ToUpperInvariant();

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        string normalized = path.Trim().Replace('\\', '/');
        int queryIndex = normalized.IndexOfAny(new[] { '?', '#' });
        if (queryIndex >= 0)
            normalized = normalized.Substring(0, queryIndex);

        if (normalized.Length == 0)
            return "";

        return normalized.StartsWith("/", StringComparison.Ordinal) ? normalized : "/" + normalized;
    }

    public static string RedactedEndpointForLog(string path)
        => NormalizePath(path);

    private static ApiEndpoint Ep(
        string name,
        string method,
        string path,
        ApiAuthRequirement auth,
        ApiEndpointKind kind,
        ApiRateLimitGroup rateLimit,
        bool sensitive,
        bool runtimeAllowed,
        bool documented,
        string[] requiredFields = null,
        string[] extensionFields = null,
        string notes = "")
    {
        return new ApiEndpoint(name, method, path, auth, kind, rateLimit, sensitive, runtimeAllowed, documented, requiredFields, extensionFields, notes);
    }

    private static void AddGroup(List<string> groups, ApiRateLimitGroup group)
    {
        string key = ToRateLimitKey(group);
        if (!string.IsNullOrEmpty(key) && !groups.Contains(key))
            groups.Add(key);
    }

    private static string ToRateLimitKey(ApiRateLimitGroup group)
    {
        switch (group)
        {
            case ApiRateLimitGroup.Auth:
                return "auth";
            case ApiRateLimitGroup.AdminAuth:
                return "admin-auth";
            case ApiRateLimitGroup.Player:
                return "player";
            case ApiRateLimitGroup.Sensitive:
                return "sensitive";
            default:
                return "";
        }
    }
}
