using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

public class ApiContractSmokeTests
{
    [Test]
    public void ApiRoutes_CoreDocumentedPaths_MatchNovelAppDocs()
    {
        Assert.That(ApiRoutes.BaseUrl, Is.EqualTo("https://nocturnedc.ru"));
        Assert.That(ApiRoutes.AuthGuest, Is.EqualTo("/auth/guest"));
        Assert.That(ApiRoutes.AuthRefresh, Is.EqualTo("/auth/refresh"));
        Assert.That(ApiRoutes.AuthRestore, Is.EqualTo("/auth/restore"));
        Assert.That(ApiRoutes.ContentUiTexts, Is.EqualTo("/content/ui-texts"));
        Assert.That(ApiRoutes.PlayerProgressSave, Is.EqualTo("/player/progress/save"));
        Assert.That(ApiRoutes.PlayerWardrobe, Is.EqualTo("/player/wardrobe"));
        Assert.That(ApiRoutes.PlayerWardrobeBuy, Is.EqualTo("/player/wardrobe/buy"));
        Assert.That(ApiRoutes.PlayerGallery, Is.EqualTo("/player/gallery"));
        Assert.That(ApiRoutes.PlayerGalleryUnlock, Is.EqualTo("/player/gallery/unlock"));
        Assert.That(ApiRoutes.UnityChoiceCosts, Is.EqualTo("/unity/choice-costs"));
        Assert.That(ApiRoutes.UnityWardrobeCosts, Is.EqualTo("/unity/wardrobe-costs"));
        Assert.That(ApiRoutes.ContentEpisodeGraph("ep_1"), Is.EqualTo("/content/episode/ep_1/graph"));
        Assert.That(ApiRoutes.PlayerFavoriteCheck("story_1"), Is.EqualTo("/player/favorites/story_1/check"));
    }

    [Test]
    public void ApiContract_ServerCommandSecurityAndPayloads_MatchNovelAppDocs()
    {
        ApiEndpoint authRefresh = ApiContract.Find("POST", ApiRoutes.AuthRefresh);
        Assert.That(authRefresh, Is.Not.Null);
        Assert.That(authRefresh.AuthRequirement, Is.EqualTo(ApiAuthRequirement.None));
        Assert.That(authRefresh.RequiredRequestFields, Is.EquivalentTo(new[] { "refreshToken" }));
        Assert.That(authRefresh.Documented, Is.True);

        ApiEndpoint wardrobeBuy = ApiContract.Find("POST", ApiRoutes.PlayerWardrobeBuy);
        Assert.That(wardrobeBuy, Is.Not.Null);
        Assert.That(wardrobeBuy.AuthRequirement, Is.EqualTo(ApiAuthRequirement.BearerJwt));
        Assert.That(wardrobeBuy.RuntimeAllowed, Is.True);
        Assert.That(wardrobeBuy.Sensitive, Is.True);
        Assert.That(wardrobeBuy.RequiredRequestFields, Is.EquivalentTo(new[] { "itemId" }));

        ApiEndpoint wardrobeGet = ApiContract.Find("GET", ApiRoutes.PlayerWardrobe);
        Assert.That(wardrobeGet, Is.Not.Null);
        Assert.That(wardrobeGet.AuthRequirement, Is.EqualTo(ApiAuthRequirement.BearerJwt));
        Assert.That(wardrobeGet.RuntimeAllowed, Is.True);

        ApiEndpoint unityChoicePost = ApiContract.Find("POST", ApiRoutes.UnityChoiceCosts);
        Assert.That(unityChoicePost, Is.Not.Null);
        Assert.That(unityChoicePost.AuthRequirement, Is.EqualTo(ApiAuthRequirement.AdminKey));
        Assert.That(unityChoicePost.RuntimeAllowed, Is.False);
        Assert.That(unityChoicePost.RequiredRequestFields, Is.EquivalentTo(new[] { "storyId", "episodeId", "choices" }));

        ApiEndpoint unityWardrobePost = ApiContract.Find("POST", ApiRoutes.UnityWardrobeCosts);
        Assert.That(unityWardrobePost, Is.Not.Null);
        Assert.That(unityWardrobePost.AuthRequirement, Is.EqualTo(ApiAuthRequirement.AdminKey));
        Assert.That(unityWardrobePost.RuntimeAllowed, Is.False);
        Assert.That(unityWardrobePost.RequiredRequestFields, Is.EquivalentTo(new[] { "items" }));

        ApiEndpoint adminPlayers = ApiContract.Find("GET", ApiRoutes.AdminPlayers);
        Assert.That(adminPlayers, Is.Not.Null);
        Assert.That(adminPlayers.AuthRequirement, Is.EqualTo(ApiAuthRequirement.AdminKey));
        Assert.That(adminPlayers.RuntimeAllowed, Is.False);

        ApiEndpoint legacyRefreshRestore = ApiContract.Find("POST", ApiRoutes.AuthRestore);
        Assert.That(legacyRefreshRestore, Is.Not.Null);
        Assert.That(legacyRefreshRestore.Documented, Is.True, "Documented restore-code endpoint must take precedence over legacy refresh-token restore.");
        Assert.That(legacyRefreshRestore.RequiredRequestFields, Is.EquivalentTo(new[] { "restoreCode", "deviceId" }));
    }

    [Test]
    public void ApiContract_PlayerProgressShopAndSocialPayloads_MatchNovelAppDocs()
    {
        AssertRequiredBearerRuntime("POST", ApiRoutes.PlayerProgressSave, "episodeId", "nodeId", "stats", "variables");
        AssertRequiredBearerRuntime("POST", ApiRoutes.PlayerProgressRewind, "episodeId", "nodeId");
        AssertRequiredBearerRuntime("POST", ApiRoutes.PlayerHeroName, "storyId", "name");
        AssertRequiredBearerRuntime("POST", ApiRoutes.PlayerPushToken, "token");
        AssertRequiredBearerRuntime("POST", ApiRoutes.PlayerCandlesSpend, "amount");
        AssertRequiredBearerRuntime("POST", ApiRoutes.PlayerHeartsSpend, "amount");
        AssertRequiredBearerRuntime("POST", ApiRoutes.PlayerGalleryUnlock, "sceneId");
        AssertRequiredBearerRuntime("POST", ApiRoutes.PlayerRelationshipUnlock, "characterId");
        AssertRequiredBearerRuntime("POST", ApiRoutes.PlayerRelationshipUpdate, "characterId", "delta");
        AssertRequiredBearerRuntime("POST", ApiRoutes.PlayerPromoApply, "code");
        AssertRequiredBearerRuntime("POST", ApiRoutes.ShopOrders, "productId");
        AssertRequiredBearerRuntime("POST", ApiRoutes.PurchasesConfirm, "productId", "receipt");
        AssertRequiredBearerRuntime("POST", ApiRoutes.PurchasesRestore, "store");
        AssertRequiredBearerRuntime("POST", ApiRoutes.PlayerChapterComplete, "episodeId");
        AssertRequiredBearerRuntime("POST", ApiRoutes.PlayerStoryComplete, "storyId");

        Assert.That(ApiContract.RequiresBearerToken("GET", ApiRoutes.PlayerProfile), Is.True);
        Assert.That(ApiContract.RequiresBearerToken("GET", ApiRoutes.PlayerFeatures), Is.True);
        Assert.That(ApiContract.RequiresBearerToken("GET", ApiRoutes.ContentCatalog), Is.True);
        Assert.That(ApiContract.RequiresBearerToken("POST", ApiRoutes.PlayerAdReward), Is.True);
        Assert.That(ApiContract.RequiresBearerToken("POST", ApiRoutes.PlayerDailyClaim), Is.True);
        Assert.That(ApiContract.RequiresBearerToken("GET", ApiRoutes.PlayerGallery), Is.True);
        Assert.That(ApiContract.RequiresBearerToken("GET", ApiRoutes.PlayerRelationships), Is.True);
        Assert.That(ApiContract.RequiresBearerToken("GET", ApiRoutes.PlayerReadingStats), Is.True);
    }

    [Test]
    public void ApiContract_RuntimeAllowlist_BlocksAdminPublisherAndLegacyRoutes()
    {
        Assert.That(ApiContract.IsRuntimeAllowed("POST", "/admin/catalog/story"), Is.False);
        Assert.That(ApiContract.IsRuntimeAllowed("POST", ApiRoutes.UnityChoiceCosts), Is.False);
        Assert.That(ApiContract.IsRuntimeAllowed("POST", "/player/favorites/add"), Is.False);

        Assert.That(ApiContract.IsRuntimeAllowed("GET", ApiRoutes.ContentUiTexts), Is.True);
        Assert.That(ApiContract.IsRuntimeAllowed("POST", ApiRoutes.PlayerFavorites), Is.True);
        Assert.That(ApiContract.IsRuntimeAllowed("DELETE", ApiRoutes.PlayerFavoriteForStory("story_1")), Is.True);
    }

    [Test]
    public void ApiContract_ProtectedRoutes_RequireBearerToken()
    {
        Assert.That(ApiContract.RequiresBearerToken("GET", ApiRoutes.PlayerProgress), Is.True);
        Assert.That(ApiContract.RequiresBearerToken("GET", ApiRoutes.ContentCatalog), Is.True);
        Assert.That(ApiContract.RequiresBearerToken("GET", ApiRoutes.ContentUiTexts), Is.True);
        Assert.That(ApiContract.RequiresBearerToken("POST", ApiRoutes.PurchasesConfirm), Is.True);
        Assert.That(ApiContract.RequiresBearerToken("POST", ApiRoutes.AuthGuest), Is.False);
    }

    [Test]
    public void SafeTextSanitizer_AllowsSafeTmpTagsAndEscapesUnsafeMarkup()
    {
        string sanitized = SafeTextSanitizer.SanitizeStoryText(
            "<b>Hello</b> <color=#ff00ff>world</color> <link=\"x\">bad</link> <sprite=1>");

        Assert.That(sanitized, Does.Contain("<b>Hello</b>"));
        Assert.That(sanitized, Does.Contain("<color=#ff00ff>world</color>"));
        Assert.That(sanitized, Does.Not.Contain("<link"));
        Assert.That(sanitized, Does.Not.Contain("<sprite"));
        Assert.That(sanitized, Does.Contain("&lt;link"));
        Assert.That(sanitized, Does.Contain("&lt;sprite"));
    }

    [Test]
    public void AppLogger_RedactsAuthSecretsInJsonAndHeaders()
    {
        var method = typeof(AppLogger).GetMethod("RedactText", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        string input = "{\"refreshToken\":\"refresh-secret\",\"idToken\":\"id-secret\",\"restoreCode\":\"restore-secret\",\"purchaseToken\":\"purchase-secret\",\"receipt\":\"receipt-secret\",\"X-Admin-Key\":\"admin-secret\",\"Authorization\":\"Bearer jwt.secret.value\"}";
        string redacted = (string)method.Invoke(null, new object[] { input });

        Assert.That(redacted, Does.Not.Contain("refresh-secret"));
        Assert.That(redacted, Does.Not.Contain("id-secret"));
        Assert.That(redacted, Does.Not.Contain("restore-secret"));
        Assert.That(redacted, Does.Not.Contain("purchase-secret"));
        Assert.That(redacted, Does.Not.Contain("receipt-secret"));
        Assert.That(redacted, Does.Not.Contain("admin-secret"));
        Assert.That(redacted, Does.Not.Contain("jwt.secret.value"));
        Assert.That(redacted, Does.Contain("[REDACTED]"));
    }

    [Test]
    public void RuntimeCode_DoesNotScatterRawNovelAppEndpointStringsOutsideApiContract()
    {
        string root = Path.GetFullPath("Assets/_Project/Scripts/Runtime");
        var endpointPattern = new Regex("\"/(auth|player|content|shop|purchases|unity|admin|health)[A-Za-z0-9_?&=./{}-]*\"", RegexOptions.Compiled);

        foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(path);
            if (fileName == "ApiContract.cs")
                continue;

            int lineNumber = 0;
            foreach (string line in File.ReadLines(path))
            {
                lineNumber++;
                if (endpointPattern.IsMatch(line))
                    Assert.Fail("Raw NovelApp endpoint string found outside ApiContract: " + path + ":" + lineNumber + " -> " + line.Trim());
            }
        }
    }

    static void AssertRequiredBearerRuntime(string method, string path, params string[] requiredFields)
    {
        ApiEndpoint endpoint = ApiContract.Find(method, path);
        Assert.That(endpoint, Is.Not.Null, method + " " + path);
        Assert.That(endpoint.AuthRequirement, Is.EqualTo(ApiAuthRequirement.BearerJwt), method + " " + path);
        Assert.That(endpoint.RuntimeAllowed, Is.True, method + " " + path);
        Assert.That(endpoint.RequiredRequestFields, Is.EquivalentTo(requiredFields), method + " " + path);
    }
}
