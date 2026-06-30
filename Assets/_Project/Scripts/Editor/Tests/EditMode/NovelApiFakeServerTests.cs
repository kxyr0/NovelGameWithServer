#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

public sealed class NovelApiFakeServerTests
{
    [UnityTest]
    public IEnumerator NetworkManager_RuntimeCommands_SendDocumentedRequestsToFakeServer()
    {
        using (var server = new FakeNovelApiServer())
        {
            server.Start();

            var go = new GameObject("NetworkManagerFakeApiTest");
            go.SetActive(false);
            NetworkManager network = null;

            try
            {
                ClearNetworkState();
                network = go.AddComponent<NetworkManager>();
                ConfigureNetworkManager(network, server.BaseUrl);

                bool balanceOk = false;
                yield return RunUnityCoroutine(network.SyncBalance(ok => balanceOk = ok));
                Assert.That(balanceOk, Is.True);
                Assert.That(NetworkManager.LastBalance.hearts, Is.EqualTo(100));
                AssertAuthorizedRuntimeRequest(server.LastRequest("GET", ApiRoutes.PlayerBalance));

                bool spendOk = false;
                yield return RunUnityCoroutine(network.SpendHearts(7, ok => spendOk = ok));
                Assert.That(spendOk, Is.True);
                Assert.That(NetworkManager.LastBalance.hearts, Is.EqualTo(93));
                FakeNovelApiServer.Request spend = server.LastRequest("POST", ApiRoutes.PlayerHeartsSpend);
                AssertAuthorizedRuntimeRequest(spend);
                Assert.That(NetworkJson.GetInt(spend.Body, "amount", 0), Is.EqualTo(7));

                bool wardrobeOk = false;
                yield return RunUnityCoroutine(network.PurchaseWardrobeItem("outfit_city", ok => wardrobeOk = ok));
                Assert.That(wardrobeOk, Is.True);
                Assert.That(NetworkManager.LastBalance.hearts, Is.EqualTo(78));
                FakeNovelApiServer.Request wardrobe = server.LastRequest("POST", ApiRoutes.PlayerWardrobeBuy);
                AssertAuthorizedRuntimeRequest(wardrobe);
                Assert.That(NetworkJson.GetString(wardrobe.Body, "itemId"), Is.EqualTo("outfit_city"));
                Assert.That(NetworkJson.GetRawValue(wardrobe.Body, "price"), Is.Null, "Client must not send trusted price for wardrobe purchases.");

                bool duplicateWardrobeOk = true;
                yield return RunUnityCoroutine(network.PurchaseWardrobeItem("outfit_city", ok => duplicateWardrobeOk = ok));
                Assert.That(duplicateWardrobeOk, Is.False, "Duplicate wardrobe purchases must fail cleanly on server 409.");
                Assert.That(NetworkManager.LastBalance.hearts, Is.EqualTo(78), "Failed wardrobe purchase must not mutate local balance.");

                bool overspendOk = true;
                yield return RunUnityCoroutine(network.SpendHearts(999, ok => overspendOk = ok));
                Assert.That(overspendOk, Is.False, "Server 422 insufficient_hearts must fail cleanly.");
                Assert.That(NetworkManager.LastBalance.hearts, Is.EqualTo(78), "Failed hearts spend must not mutate local balance.");

                bool wardrobeSyncOk = false;
                yield return RunUnityCoroutine(network.SyncWardrobeOwnership(ok => wardrobeSyncOk = ok));
                Assert.That(wardrobeSyncOk, Is.True);
                AssertAuthorizedRuntimeRequest(server.LastRequest("GET", ApiRoutes.PlayerWardrobe));

                bool viewedOk = false;
                yield return RunUnityCoroutine(network.MarkScenesViewed(
                    "ep_s1e1",
                    new List<string> { "node_intro", "node_intro", "node_choice" },
                    (ok, _) => viewedOk = ok));
                Assert.That(viewedOk, Is.True);
                FakeNovelApiServer.Request viewed = server.LastRequest("POST", ApiRoutes.PlayerScenesViewed);
                AssertAuthorizedRuntimeRequest(viewed);
                Assert.That(NetworkJson.GetString(viewed.Body, "episodeId"), Is.EqualTo("ep_s1e1"));
                Assert.That(NetworkJson.GetRawValue(viewed.Body, "nodeIds"), Does.Contain("node_intro"));

                EpisodeGraphResponse graph = null;
                yield return RunUnityCoroutine(network.FetchEpisodeGraphResponse("ep_s1e1", "0", response => graph = response));
                Assert.That(graph, Is.Not.Null);
                Assert.That(graph.episodeId, Is.EqualTo("ep_s1e1"));
                Assert.That(graph.contentVersion, Is.EqualTo("v1"));
                Assert.That(graph.graphJson, Does.Contain("\"nodes\""));
                AssertAuthorizedRuntimeRequest(server.LastRequest("GET", ApiRoutes.ContentEpisodeGraph("ep_s1e1")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                ClearNetworkState();
            }
        }
    }

    [UnityTest]
    public IEnumerator NetworkManager_ResourceAndCatalogCommands_SendDocumentedRequestsToFakeServer()
    {
        using (var server = new FakeNovelApiServer())
        {
            server.Start();

            var go = new GameObject("NetworkManagerResourceApiTest");
            go.SetActive(false);
            NetworkManager network = null;

            try
            {
                ClearNetworkState();
                network = go.AddComponent<NetworkManager>();
                ConfigureNetworkManager(network, server.BaseUrl);

                bool balanceOk = false;
                yield return RunUnityCoroutine(network.SyncBalance(ok => balanceOk = ok));
                Assert.That(balanceOk, Is.True);
                Assert.That(NetworkManager.LastBalance.candles, Is.EqualTo(3));
                AssertAuthorizedRuntimeRequest(server.LastRequest("GET", ApiRoutes.PlayerBalance));

                bool candlesOk = false;
                yield return RunUnityCoroutine(network.SpendCandles(2, ok => candlesOk = ok));
                Assert.That(candlesOk, Is.True);
                Assert.That(NetworkManager.LastBalance.candles, Is.EqualTo(1));
                FakeNovelApiServer.Request candles = server.LastRequest("POST", ApiRoutes.PlayerCandlesSpend);
                AssertAuthorizedRuntimeRequest(candles);
                Assert.That(NetworkJson.GetInt(candles.Body, "amount", 0), Is.EqualTo(2));

                bool adRewardOk = false;
                yield return RunUnityCoroutine(network.ClaimAdReward((ok, _) => adRewardOk = ok));
                Assert.That(adRewardOk, Is.True);
                Assert.That(NetworkManager.LastBalance.hearts, Is.EqualTo(102));
                AssertAuthorizedRuntimeRequest(server.LastRequest("POST", ApiRoutes.PlayerAdReward));

                bool dailyRewardOk = false;
                yield return RunUnityCoroutine(network.ClaimDailyReward((ok, _) => dailyRewardOk = ok));
                Assert.That(dailyRewardOk, Is.True);
                Assert.That(NetworkManager.LastBalance.hearts, Is.EqualTo(107));
                AssertAuthorizedRuntimeRequest(server.LastRequest("POST", ApiRoutes.PlayerDailyClaim));

                bool catalogOk = false;
                yield return RunUnityCoroutine(network.SyncCatalog(ok => catalogOk = ok));
                Assert.That(catalogOk, Is.True);
                Assert.That(NetworkManager.TryGetCatalogEpisode("ep_s1e1", out CatalogEpisodeResponse episode), Is.True);
                Assert.That(episode.title, Is.EqualTo("Chapter 1"));
                Assert.That(episode.isUnlocked, Is.True);
                Assert.That(episode.hasRemoteContent, Is.True);
                AssertAuthorizedRuntimeRequest(server.LastRequest("GET", ApiRoutes.ContentCatalog));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                ClearNetworkState();
            }
        }
    }

    [UnityTest]
    public IEnumerator NetworkManager_RefreshAuthToken_UsesDocumentedRefreshEndpoint()
    {
        using (var server = new FakeNovelApiServer())
        {
            server.Start();

            var go = new GameObject("NetworkManagerRefreshAuthTest");
            go.SetActive(false);
            NetworkManager network = null;

            try
            {
                ClearNetworkState();
                network = go.AddComponent<NetworkManager>();
                ConfigureNetworkManager(network, server.BaseUrl);

                bool refreshOk = false;
                yield return RunUnityCoroutine(network.RefreshAuthToken(ok => refreshOk = ok));
                Assert.That(refreshOk, Is.True);
                Assert.That(NetworkManager.IsAuthenticated, Is.True);
                Assert.That(GetStaticField<string>(typeof(NetworkManager), "_authToken"), Is.EqualTo(FakeNovelApiServer.RefreshedJwt));
                Assert.That(GetStaticField<string>(typeof(NetworkManager), "_refreshToken"), Is.EqualTo(FakeNovelApiServer.RefreshedRefreshToken));

                FakeNovelApiServer.Request refresh = server.LastRequest("POST", ApiRoutes.AuthRefresh);
                Assert.That(refresh.Headers.ContainsKey("Authorization"), Is.False, "Auth refresh must not send an old bearer token.");
                Assert.That(refresh.Headers.ContainsKey("X-Admin-Key"), Is.False, "Auth refresh must not send admin credentials.");
                Assert.That(NetworkJson.GetString(refresh.Body, "refreshToken"), Is.EqualTo(FakeNovelApiServer.RefreshToken));
                Assert.That(NetworkJson.GetRawValue(refresh.Body, "deviceId"), Is.Null, "Documented /auth/refresh body is { refreshToken } only.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                ClearNetworkState();
            }
        }
    }

    [UnityTest]
    public IEnumerator NetworkManager_InvalidProgressCommands_DoNotSendMalformedRequestsToFakeServer()
    {
        using (var server = new FakeNovelApiServer())
        {
            server.Start();

            var go = new GameObject("NetworkManagerInvalidProgressApiTest");
            go.SetActive(false);
            NetworkManager network = null;

            try
            {
                ClearNetworkState();
                network = go.AddComponent<NetworkManager>();
                ConfigureNetworkManager(network, server.BaseUrl);

                int undoRequestsBefore = server.CountRequests("POST", ApiRoutes.PlayerProgressUndoChoice);
                bool undoOk = true;
                string undoError = null;
                yield return RunUnityCoroutine(network.UndoChoice(0, "node_choice", false, (ok, error) =>
                {
                    undoOk = ok;
                    undoError = error;
                }));

                Assert.That(undoOk, Is.False);
                Assert.That(undoError, Does.Contain("Invalid undo amount"));
                Assert.That(server.CountRequests("POST", ApiRoutes.PlayerProgressUndoChoice), Is.EqualTo(undoRequestsBefore));

                int rewindRequestsBefore = server.CountRequests("POST", ApiRoutes.PlayerProgressRewind);
                string rewindResponse = "unset";
                string rewindError = null;
                yield return RunUnityCoroutine(network.RewindProgress("", "node_intro", (json, error) =>
                {
                    rewindResponse = json;
                    rewindError = error;
                }));

                Assert.That(rewindResponse, Is.Null);
                Assert.That(rewindError, Does.Contain("Invalid rewind progress payload"));
                Assert.That(server.CountRequests("POST", ApiRoutes.PlayerProgressRewind), Is.EqualTo(rewindRequestsBefore));

                rewindResponse = "unset";
                rewindError = null;
                yield return RunUnityCoroutine(network.RewindProgress("ep_s1e1", "", (json, error) =>
                {
                    rewindResponse = json;
                    rewindError = error;
                }));

                Assert.That(rewindResponse, Is.Null);
                Assert.That(rewindError, Does.Contain("Invalid rewind progress payload"));
                Assert.That(server.CountRequests("POST", ApiRoutes.PlayerProgressRewind), Is.EqualTo(rewindRequestsBefore));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                ClearNetworkState();
            }
        }
    }

    [UnityTest]
    public IEnumerator NetworkManager_PlayerContentUtilityCommands_SendDocumentedRequestsToFakeServer()
    {
        using (var server = new FakeNovelApiServer())
        {
            server.Start();

            var go = new GameObject("NetworkManagerUtilityApiTest");
            go.SetActive(false);
            NetworkManager network = null;

            try
            {
                ClearNetworkState();
                network = go.AddComponent<NetworkManager>();
                ConfigureNetworkManager(network, server.BaseUrl);

                string response = null;
                string error = "unset";
                yield return RunUnityCoroutine(network.FetchEpisodeVersion("ep_s1e1", (json, err) =>
                {
                    response = json;
                    error = err;
                }));
                Assert.That(error, Is.Null);
                Assert.That(response, Does.Contain("\"contentVersion\":\"v2\""));
                AssertAuthorizedRuntimeRequest(server.LastRequest("GET", ApiRoutes.ContentEpisodeVersion("ep_s1e1")));

                bool unlockOk = false;
                yield return RunUnityCoroutine(network.UnlockEpisode("ep_locked", true, (ok, _) => unlockOk = ok));
                Assert.That(unlockOk, Is.True);
                FakeNovelApiServer.Request unlock = server.LastRequest("POST", ApiRoutes.ContentEpisodeUnlock);
                AssertAuthorizedRuntimeRequest(unlock);
                Assert.That(NetworkJson.GetString(unlock.Body, "episodeId"), Is.EqualTo("ep_locked"));
                Assert.That(NetworkJson.GetBool(unlock.Body, "confirmed"), Is.True);

                bool completeOk = false;
                yield return RunUnityCoroutine(network.CompleteEpisode("ep_s1e1", "ep_s1e2", (ok, _) => completeOk = ok));
                Assert.That(completeOk, Is.True);
                FakeNovelApiServer.Request complete = server.LastRequest("POST", ApiRoutes.PlayerEpisodeComplete);
                AssertAuthorizedRuntimeRequest(complete);
                Assert.That(NetworkJson.GetString(complete.Body, "episodeId"), Is.EqualTo("ep_s1e1"));
                Assert.That(NetworkJson.GetString(complete.Body, "nextEpisodeId"), Is.EqualTo("ep_s1e2"));

                bool replayOk = false;
                yield return RunUnityCoroutine(network.ReplayEpisode("ep_s1e1", (ok, _) => replayOk = ok));
                Assert.That(replayOk, Is.True);
                Assert.That(NetworkJson.GetString(server.LastRequest("POST", ApiRoutes.PlayerEpisodeReplay).Body, "episodeId"), Is.EqualTo("ep_s1e1"));

                bool jumpOk = false;
                yield return RunUnityCoroutine(network.JumpToEpisode("ep_s1e2", (ok, _) => jumpOk = ok));
                Assert.That(jumpOk, Is.True);
                Assert.That(NetworkJson.GetString(server.LastRequest("POST", ApiRoutes.PlayerEpisodeJump).Body, "episodeId"), Is.EqualTo("ep_s1e2"));

                bool restartOk = false;
                yield return RunUnityCoroutine(network.RestartSeason("season_1", (ok, _) => restartOk = ok));
                Assert.That(restartOk, Is.True);
                Assert.That(NetworkJson.GetString(server.LastRequest("POST", ApiRoutes.PlayerSeasonRestart).Body, "seasonId"), Is.EqualTo("season_1"));

                response = null;
                error = "unset";
                yield return RunUnityCoroutine(network.FetchViewedScenes("ep_s1e1", (json, err) =>
                {
                    response = json;
                    error = err;
                }));
                Assert.That(error, Is.Null);
                Assert.That(response, Does.Contain("node_intro"));
                Assert.That(server.LastRequest("GET", ApiRoutes.PlayerScenesViewed).Target, Does.Contain("episodeId=ep_s1e1"));

                yield return RunJsonCommand(network.FetchGallery, ApiRoutes.PlayerGallery, server);

                bool galleryUnlockOk = false;
                yield return RunUnityCoroutine(network.UnlockGalleryScene("scene_forest", (ok, _) => galleryUnlockOk = ok));
                Assert.That(galleryUnlockOk, Is.True);
                FakeNovelApiServer.Request galleryUnlock = server.LastRequest("POST", ApiRoutes.PlayerGalleryUnlock);
                AssertAuthorizedRuntimeRequest(galleryUnlock);
                Assert.That(NetworkJson.GetString(galleryUnlock.Body, "sceneId"), Is.EqualTo("scene_forest"));

                yield return RunJsonCommand(network.FetchSlots, ApiRoutes.PlayerSlots, server);

                bool switchOk = false;
                yield return RunUnityCoroutine(network.SwitchSlot(2, (ok, _) => switchOk = ok));
                Assert.That(switchOk, Is.True);
                Assert.That(NetworkJson.GetInt(server.LastRequest("POST", ApiRoutes.PlayerSlotSwitch).Body, "slotId", 0), Is.EqualTo(2));

                bool forkOk = false;
                yield return RunUnityCoroutine(network.ForkSlot((ok, _) => forkOk = ok));
                Assert.That(forkOk, Is.True);
                AssertAuthorizedRuntimeRequest(server.LastRequest("POST", ApiRoutes.PlayerSlotFork));

                bool resetOk = false;
                yield return RunUnityCoroutine(network.ResetStoryProgress("story_1", (ok, _) => resetOk = ok));
                Assert.That(resetOk, Is.True);
                Assert.That(NetworkJson.GetString(server.LastRequest("POST", ApiRoutes.PlayerStoryReset).Body, "storyId"), Is.EqualTo("story_1"));

                yield return RunJsonCommand(callback => network.FetchCatGreeting(99, callback), "/player/cat/greet", server);
                Assert.That(server.LastRequest("GET", "/player/cat/greet").Target, Does.Contain("hour=23"));

                yield return RunJsonCommand(network.FetchCatName, ApiRoutes.PlayerCatName, server);

                bool catNameOk = false;
                yield return RunUnityCoroutine(network.SetCatName("Mira", (ok, _) => catNameOk = ok));
                Assert.That(catNameOk, Is.True);
                Assert.That(NetworkJson.GetString(server.LastRequest("POST", ApiRoutes.PlayerCatName).Body, "name"), Is.EqualTo("Mira"));

                yield return RunJsonCommand(network.FetchDiceStatus, ApiRoutes.PlayerDiceStatus, server);

                bool rollOk = false;
                yield return RunUnityCoroutine(network.RollDice((ok, _) => rollOk = ok));
                Assert.That(rollOk, Is.True);
                AssertAuthorizedRuntimeRequest(server.LastRequest("POST", ApiRoutes.PlayerDiceRoll));

                yield return RunJsonCommand(network.FetchTarotStatus, ApiRoutes.PlayerTarotStatus, server);

                bool tarotOk = false;
                yield return RunUnityCoroutine(network.DrawTarot((ok, _) => tarotOk = ok));
                Assert.That(tarotOk, Is.True);
                AssertAuthorizedRuntimeRequest(server.LastRequest("POST", ApiRoutes.PlayerTarotDraw));

                yield return RunJsonCommand(network.FetchFavorites, ApiRoutes.PlayerFavorites, server);

                bool favoriteOk = false;
                yield return RunUnityCoroutine(network.AddFavorite("story_1", (ok, _) => favoriteOk = ok));
                Assert.That(favoriteOk, Is.True);
                Assert.That(NetworkJson.GetString(server.LastRequest("POST", ApiRoutes.PlayerFavorites).Body, "storyId"), Is.EqualTo("story_1"));

                yield return RunJsonCommand(callback => network.CheckFavorite("story_1", callback), ApiRoutes.PlayerFavoriteCheck("story_1"), server);

                bool removeFavoriteOk = false;
                yield return RunUnityCoroutine(network.RemoveFavorite("story_1", (ok, _) => removeFavoriteOk = ok));
                Assert.That(removeFavoriteOk, Is.True);
                AssertAuthorizedRuntimeRequest(server.LastRequest("DELETE", ApiRoutes.PlayerFavoriteForStory("story_1")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                ClearNetworkState();
            }
        }
    }

    [UnityTest]
    public IEnumerator NetworkManager_ShopAndPurchaseCommands_SendDocumentedRequestsToFakeServer()
    {
        using (var server = new FakeNovelApiServer())
        {
            server.Start();

            var go = new GameObject("NetworkManagerShopApiTest");
            go.SetActive(false);
            NetworkManager network = null;

            try
            {
                ClearNetworkState();
                network = go.AddComponent<NetworkManager>();
                ConfigureNetworkManager(network, server.BaseUrl);

                yield return RunJsonCommand(network.FetchShopPrices, ApiRoutes.ShopPrices, server);
                yield return RunJsonCommand(network.FetchShopItems, ApiRoutes.ShopItems, server);

                string orderResponse = null;
                string orderError = "unset";
                yield return RunUnityCoroutine(network.CreateShopOrder("pack_small", 2, (json, err) =>
                {
                    orderResponse = json;
                    orderError = err;
                }));
                Assert.That(orderError, Is.Null);
                Assert.That(orderResponse, Does.Contain("order_1"));
                FakeNovelApiServer.Request order = server.LastRequest("POST", ApiRoutes.ShopOrders);
                AssertAuthorizedRuntimeRequest(order);
                Assert.That(NetworkJson.GetString(order.Body, "productId"), Is.EqualTo("pack_small"));
                Assert.That(NetworkJson.GetInt(order.Body, "quantity", 0), Is.EqualTo(2));
                Assert.That(NetworkJson.GetString(order.Body, "platform"), Is.Not.Empty);

                bool confirmOk = false;
                yield return RunUnityCoroutine(network.ConfirmPurchase(
                    "google",
                    "pack_small",
                    "tx_1",
                    "{\"Store\":\"GooglePlay\",\"TransactionID\":\"tx_1\",\"Payload\":\"{}\"}",
                    "sig_1",
                    (ok, _) => confirmOk = ok));
                Assert.That(confirmOk, Is.True);
                FakeNovelApiServer.Request confirm = server.LastRequest("POST", ApiRoutes.PurchasesConfirm);
                AssertAuthorizedRuntimeRequest(confirm);
                Assert.That(NetworkJson.GetString(confirm.Body, "productId"), Is.EqualTo("pack_small"));
                Assert.That(NetworkJson.GetString(confirm.Body, "store"), Is.EqualTo("google"));
                Assert.That(NetworkJson.GetString(confirm.Body, "provider"), Is.EqualTo("google"));
                Assert.That(NetworkJson.GetString(confirm.Body, "transactionId"), Is.EqualTo("tx_1"));
                Assert.That(NetworkJson.GetString(confirm.Body, "receipt"), Is.Not.Empty);
                Assert.That(NetworkJson.GetString(confirm.Body, "signature"), Is.EqualTo("sig_1"));

                bool restoreOk = false;
                yield return RunUnityCoroutine(network.RestorePurchases("google", "restore_1", (ok, _) => restoreOk = ok));
                Assert.That(restoreOk, Is.True);
                FakeNovelApiServer.Request restore = server.LastRequest("POST", ApiRoutes.PurchasesRestore);
                AssertAuthorizedRuntimeRequest(restore);
                Assert.That(NetworkJson.GetString(restore.Body, "store"), Is.EqualTo("google"));
                Assert.That(NetworkJson.GetString(restore.Body, "provider"), Is.EqualTo("google"));
                Assert.That(NetworkJson.GetString(restore.Body, "restoreToken"), Is.EqualTo("restore_1"));

                yield return RunJsonCommand(network.FetchPurchaseHistory, ApiRoutes.PurchasesHistory, server);
                yield return RunJsonCommand(network.FetchPurchaseProducts, ApiRoutes.PurchasesProducts, server);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                ClearNetworkState();
            }
        }
    }

    [UnityTest]
    public IEnumerator NetworkManager_ProgressProfileAndSocialCommands_SendDocumentedRequestsToFakeServer()
    {
        using (var server = new FakeNovelApiServer())
        {
            server.Start();

            var go = new GameObject("NetworkManagerProgressApiTest");
            go.SetActive(false);
            NetworkManager network = null;

            try
            {
                ClearNetworkState();
                network = go.AddComponent<NetworkManager>();
                ConfigureNetworkManager(network, server.BaseUrl);

                yield return RunJsonCommand(network.FetchProfile, ApiRoutes.PlayerProfile, server);

                bool featuresOk = false;
                yield return RunUnityCoroutine(network.SyncFeatures(ok => featuresOk = ok));
                Assert.That(featuresOk, Is.True);
                Assert.That(NetworkManager.FullAccessEnabled, Is.True);
                Assert.That(NetworkManager.FastForwardEnabled, Is.True);
                Assert.That(NetworkManager.BookmarksEnabled, Is.True);
                AssertAuthorizedRuntimeRequest(server.LastRequest("GET", ApiRoutes.PlayerFeatures));

                bool loadProgressOk = false;
                yield return RunUnityCoroutine(network.LoadProgress(ok => loadProgressOk = ok));
                Assert.That(loadProgressOk, Is.True);
                Assert.That(NetworkManager.LastProgressEpisodeId, Is.EqualTo("ep_s1e1"));
                Assert.That(NetworkManager.LastProgressNodeGuid, Is.EqualTo("node_intro"));
                AssertAuthorizedRuntimeRequest(server.LastRequest("GET", ApiRoutes.PlayerProgress));

                var snapshot = new SaveData
                {
                    storyId = "story_1",
                    episodeId = "ep_s1e1",
                    currentNodeGuid = "node_choice",
                    playerName = "Alex",
                    savedAtIso = "2026-05-16T07:00:12Z"
                };
                snapshot.statKeys.Add("trust");
                snapshot.statValues.Add(3);
                yield return RunPrivateCoroutine(
                    network,
                    "SaveProgressCoroutine",
                    "ep_s1e1",
                    "node_choice",
                    snapshot,
                    new Dictionary<string, int> { { "trust", 3 } },
                    new Dictionary<string, bool> { { "met_cat", true } },
                    new List<string> { "ep_s1e1" });
                FakeNovelApiServer.Request progressSave = server.LastRequest("POST", ApiRoutes.PlayerProgressSave);
                AssertAuthorizedRuntimeRequest(progressSave);
                Assert.That(NetworkJson.GetString(progressSave.Body, "episodeId"), Is.EqualTo("ep_s1e1"));
                Assert.That(NetworkJson.GetString(progressSave.Body, "nodeId"), Is.EqualTo("node_choice"));
                Assert.That(NetworkJson.GetRawValue(progressSave.Body, "stats"), Does.Contain("trust"));
                Assert.That(NetworkJson.GetRawValue(progressSave.Body, "variables"), Does.Contain("met_cat"));

                bool undoOk = false;
                yield return RunUnityCoroutine(network.UndoChoice(7, "node_choice", false, (ok, _) => undoOk = ok));
                Assert.That(undoOk, Is.True);
                FakeNovelApiServer.Request undo = server.LastRequest("POST", ApiRoutes.PlayerProgressUndoChoice);
                AssertAuthorizedRuntimeRequest(undo);
                Assert.That(NetworkJson.GetInt(undo.Body, "amount", 0), Is.EqualTo(7));
                Assert.That(NetworkJson.GetString(undo.Body, "nodeGuid"), Is.EqualTo("node_choice"));

                string rewindResponse = null;
                string rewindError = "unset";
                yield return RunUnityCoroutine(network.RewindProgress("ep_s1e1", "node_intro", (json, err) =>
                {
                    rewindResponse = json;
                    rewindError = err;
                }));
                Assert.That(rewindError, Is.Null);
                Assert.That(rewindResponse, Does.Contain("node_intro"));
                FakeNovelApiServer.Request rewind = server.LastRequest("POST", ApiRoutes.PlayerProgressRewind);
                AssertAuthorizedRuntimeRequest(rewind);
                Assert.That(NetworkJson.GetString(rewind.Body, "episodeId"), Is.EqualTo("ep_s1e1"));
                Assert.That(NetworkJson.GetString(rewind.Body, "nodeId"), Is.EqualTo("node_intro"));

                bool heroSyncOk = false;
                yield return RunUnityCoroutine(network.SyncHeroName(ok => heroSyncOk = ok));
                Assert.That(heroSyncOk, Is.True);
                AssertAuthorizedRuntimeRequest(server.LastRequest("GET", ApiRoutes.PlayerHeroName));

                yield return RunPrivateCoroutine(network, "SetHeroNameCoroutine", "Alex", "node_intro", "ep_s1e1", "story_1");
                FakeNovelApiServer.Request heroPost = server.LastRequest("POST", ApiRoutes.PlayerHeroName);
                AssertAuthorizedRuntimeRequest(heroPost);
                Assert.That(NetworkJson.GetString(heroPost.Body, "storyId"), Is.EqualTo("story_1"));
                Assert.That(NetworkJson.GetString(heroPost.Body, "name"), Is.EqualTo("Alex"));

                string bookmarkNode = null;
                string bookmarkEpisode = null;
                yield return RunUnityCoroutine(network.LoadBookmark((nodeGuid, episodeId) =>
                {
                    bookmarkNode = nodeGuid;
                    bookmarkEpisode = episodeId;
                }));
                Assert.That(bookmarkNode, Is.EqualTo("node_bookmark"));
                Assert.That(bookmarkEpisode, Is.EqualTo("ep_s1e1"));
                AssertAuthorizedRuntimeRequest(server.LastRequest("GET", ApiRoutes.PlayerBookmark));

                yield return RunPrivateCoroutine(network, "SaveBookmarkCoroutine", "node_bookmark", "ep_s1e1", "story_1", snapshot, "Before choice");
                FakeNovelApiServer.Request bookmarkSave = server.LastRequest("POST", ApiRoutes.PlayerBookmarkSave);
                AssertAuthorizedRuntimeRequest(bookmarkSave);
                Assert.That(NetworkJson.GetString(bookmarkSave.Body, "nodeGuid"), Is.EqualTo("node_bookmark"));
                Assert.That(NetworkJson.GetString(bookmarkSave.Body, "episodeId"), Is.EqualTo("ep_s1e1"));
                Assert.That(NetworkJson.GetString(bookmarkSave.Body, "storyId"), Is.EqualTo("story_1"));

                string pushResponse = null;
                string pushError = "unset";
                yield return RunUnityCoroutine(network.RegisterPushToken("push_token_1", "fcm", (json, err) =>
                {
                    pushResponse = json;
                    pushError = err;
                }));
                Assert.That(pushError, Is.Null);
                Assert.That(pushResponse, Does.Contain("\"ok\":true"));
                FakeNovelApiServer.Request push = server.LastRequest("POST", ApiRoutes.PlayerPushToken);
                AssertAuthorizedRuntimeRequest(push);
                Assert.That(NetworkJson.GetString(push.Body, "token"), Is.EqualTo("push_token_1"));
                Assert.That(NetworkJson.GetString(push.Body, "provider"), Is.EqualTo("fcm"));
                Assert.That(NetworkJson.GetString(push.Body, "platform"), Is.Not.Empty);

                yield return RunJsonCommand(network.FetchRelationships, ApiRoutes.PlayerRelationships, server);

                bool unlockRelationshipOk = false;
                yield return RunUnityCoroutine(network.UnlockRelationship("char_mira", "story_1", (ok, _) => unlockRelationshipOk = ok));
                Assert.That(unlockRelationshipOk, Is.True);
                FakeNovelApiServer.Request relationshipUnlock = server.LastRequest("POST", ApiRoutes.PlayerRelationshipUnlock);
                Assert.That(NetworkJson.GetString(relationshipUnlock.Body, "characterId"), Is.EqualTo("char_mira"));
                Assert.That(NetworkJson.GetString(relationshipUnlock.Body, "storyId"), Is.EqualTo("story_1"));

                bool updateRelationshipOk = false;
                yield return RunUnityCoroutine(network.UpdateRelationship("char_mira", 5, "story_1", (ok, _) => updateRelationshipOk = ok));
                Assert.That(updateRelationshipOk, Is.True);
                FakeNovelApiServer.Request relationshipUpdate = server.LastRequest("POST", ApiRoutes.PlayerRelationshipUpdate);
                Assert.That(NetworkJson.GetString(relationshipUpdate.Body, "characterId"), Is.EqualTo("char_mira"));
                Assert.That(NetworkJson.GetInt(relationshipUpdate.Body, "delta", 0), Is.EqualTo(5));

                bool promoOk = false;
                yield return RunUnityCoroutine(network.ApplyPromoCode("WELCOME", (ok, _) => promoOk = ok));
                Assert.That(promoOk, Is.True);
                Assert.That(NetworkJson.GetString(server.LastRequest("POST", ApiRoutes.PlayerPromoApply).Body, "code"), Is.EqualTo("WELCOME"));

                bool completeChapterOk = false;
                yield return RunUnityCoroutine(network.CompleteChapter("ep_s1e1", (ok, _) => completeChapterOk = ok));
                Assert.That(completeChapterOk, Is.True);
                Assert.That(NetworkJson.GetString(server.LastRequest("POST", ApiRoutes.PlayerChapterComplete).Body, "episodeId"), Is.EqualTo("ep_s1e1"));

                bool completeStoryOk = false;
                yield return RunUnityCoroutine(network.CompleteStory("story_1", (ok, _) => completeStoryOk = ok));
                Assert.That(completeStoryOk, Is.True);
                Assert.That(NetworkJson.GetString(server.LastRequest("POST", ApiRoutes.PlayerStoryComplete).Body, "storyId"), Is.EqualTo("story_1"));

                yield return RunJsonCommand(network.FetchReadingStats, ApiRoutes.PlayerReadingStats, server);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                ClearNetworkState();
            }
        }
    }

    [UnityTest]
    public IEnumerator UnityPublishers_SendDocumentedPayloadsToFakeServer()
    {
        using (var server = new FakeNovelApiServer())
        {
            server.Start();

            StoryGraph graph = null;
            ClothingItem outfit = null;

            try
            {
                graph = ScriptableObject.CreateInstance<StoryGraph>();
                graph.episodeId = "ep_test";

                var choice = graph.AddNode<ChoiceNode>();
                choice.guid = "choice_paid";
                choice.name = "Paid Choice";
                choice.options = new List<ChoiceOption>
                {
                    new ChoiceOption { text = "Free", isPremium = false, premiumCost = 0 },
                    new ChoiceOption { text = "Paid", isPremium = true, premiumCost = 7 }
                };

                outfit = ScriptableObject.CreateInstance<ClothingItem>();
                outfit.id = "outfit_city";

                var wardrobe = graph.AddNode<WardrobeChoiceNode>();
                wardrobe.guid = "wardrobe_paid";
                wardrobe.availableClothes = new List<ClothingItem> { outfit };
                wardrobe.premiumCosts = new List<int> { 15 };

                UnityChoiceCostsPublishPayload choicePayload = UnityChoiceCostsPublisher.BuildPayload(
                    new[] { graph },
                    "story_test",
                    "");
                Assert.That(choicePayload.costs.Count, Is.EqualTo(2));
                Assert.That(choicePayload.choices.Count, Is.EqualTo(choicePayload.costs.Count), "Documented /unity/choice-costs payload must expose choices[].");

                UnityPublisherRequestResult choicePublish = null;
                yield return UnityChoiceCostsPublisher.Publish(
                    choicePayload,
                    result => choicePublish = result,
                    server.BaseUrl,
                    FakeNovelApiServer.AdminKey,
                    allowUnsigned: false);
                Assert.That(choicePublish.Success, Is.True, choicePublish.Error + "\n" + choicePublish.Body);

                FakeNovelApiServer.Request choicePost = server.LastRequest("POST", ApiRoutes.UnityChoiceCosts);
                Assert.That(choicePost.Headers.TryGetValue("X-Admin-Key", out string adminKey), Is.True);
                Assert.That(adminKey, Is.EqualTo(FakeNovelApiServer.AdminKey));
                Assert.That(NetworkJson.GetRawValue(choicePost.Body, "choices"), Does.Contain("choice_paid"));

                UnityPublisherRequestResult choiceFetch = null;
                yield return UnityChoiceCostsPublisher.Fetch(
                    "story_test",
                    "ep_test",
                    result => choiceFetch = result,
                    server.BaseUrl,
                    FakeNovelApiServer.AdminKey,
                    allowUnsigned: false);
                Assert.That(choiceFetch.Success, Is.True, choiceFetch.Error);
                Assert.That(server.LastRequest("GET", ApiRoutes.UnityChoiceCosts).Target, Does.Contain("storyId=story_test"));

                UnityPublisherRequestResult choiceDelete = null;
                yield return UnityChoiceCostsPublisher.Delete(
                    "choice_paid",
                    result => choiceDelete = result,
                    server.BaseUrl,
                    FakeNovelApiServer.AdminKey,
                    allowUnsigned: false);
                Assert.That(choiceDelete.Success, Is.True, choiceDelete.Error);
                Assert.That(server.LastRequest("DELETE", ApiRoutes.UnityChoiceCostByNode("choice_paid")).Path, Is.EqualTo(ApiRoutes.UnityChoiceCostByNode("choice_paid")));

                UnityWardrobeCostsPublishPayload wardrobePayload = UnityWardrobeCostsPublisher.BuildPayload(new[] { graph });
                Assert.That(wardrobePayload.items.Count, Is.EqualTo(1));
                Assert.That(wardrobePayload.items[0].itemId, Is.EqualTo("outfit_city"));
                Assert.That(wardrobePayload.items[0].price, Is.EqualTo(15));

                UnityPublisherRequestResult wardrobePublish = null;
                yield return UnityWardrobeCostsPublisher.Publish(
                    wardrobePayload,
                    result => wardrobePublish = result,
                    server.BaseUrl,
                    FakeNovelApiServer.AdminKey,
                    allowUnsigned: false);
                Assert.That(wardrobePublish.Success, Is.True, wardrobePublish.Error + "\n" + wardrobePublish.Body);

                FakeNovelApiServer.Request wardrobePost = server.LastRequest("POST", ApiRoutes.UnityWardrobeCosts);
                Assert.That(NetworkJson.GetRawValue(wardrobePost.Body, "items"), Does.Contain("outfit_city"));
                Assert.That(NetworkJson.GetRawValue(wardrobePost.Body, "items"), Does.Contain("\"price\":15"));

                UnityPublisherRequestResult wardrobeFetch = null;
                yield return UnityWardrobeCostsPublisher.Fetch(
                    result => wardrobeFetch = result,
                    server.BaseUrl,
                    FakeNovelApiServer.AdminKey,
                    allowUnsigned: false);
                Assert.That(wardrobeFetch.Success, Is.True, wardrobeFetch.Error);
                Assert.That(NetworkJson.GetInt(wardrobeFetch.Body, "count", 0), Is.EqualTo(1));
            }
            finally
            {
                if (graph != null)
                    UnityEngine.Object.DestroyImmediate(graph);
                if (outfit != null)
                    UnityEngine.Object.DestroyImmediate(outfit);
            }
        }
    }

    [Test]
    public void RuntimeApiGuards_BlockPublisherAdminAndUnsafePaths()
    {
        Assert.That(ApiContract.IsRuntimeAllowed("POST", ApiRoutes.UnityChoiceCosts), Is.False);
        Assert.That(ApiContract.IsRuntimeAllowed("POST", ApiRoutes.UnityWardrobeCosts), Is.False);
        Assert.That(ApiContract.Find("POST", ApiRoutes.UnityWardrobeCosts).AuthRequirement, Is.EqualTo(ApiAuthRequirement.AdminKey));

        MethodInfo guard = typeof(NetworkManager).GetMethod(
            "IsAllowedPublicNetworkPath",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(guard, Is.Not.Null);

        Assert.That((bool)guard.Invoke(null, new object[] { ApiRoutes.UnityWardrobeCosts, "POST" }), Is.False);
        Assert.That((bool)guard.Invoke(null, new object[] { "/admin/players", "GET" }), Is.False);
        Assert.That((bool)guard.Invoke(null, new object[] { "/content/episode/%2e%2e/admin/graph", "GET" }), Is.False);
        Assert.That((bool)guard.Invoke(null, new object[] { "//evil.example/player/balance", "GET" }), Is.False);
        Assert.That((bool)guard.Invoke(null, new object[] { ApiRoutes.PlayerBalance, "GET" }), Is.True);
    }

    static void AssertAuthorizedRuntimeRequest(FakeNovelApiServer.Request request)
    {
        Assert.That(request, Is.Not.Null);
        Assert.That(request.Headers.TryGetValue("Authorization", out string authorization), Is.True);
        Assert.That(authorization, Is.EqualTo("Bearer " + FakeNovelApiServer.Jwt));
        Assert.That(request.Headers.ContainsKey("X-Admin-Key"), Is.False, "Runtime player/content calls must not carry admin credentials.");
    }

    static IEnumerator RunUnityCoroutine(IEnumerator routine)
    {
        if (routine == null)
            yield break;

        var stack = new Stack<IEnumerator>();
        stack.Push(routine);

        while (stack.Count > 0)
        {
            IEnumerator current = stack.Peek();
            if (!current.MoveNext())
            {
                if (current is IDisposable disposable)
                    disposable.Dispose();
                stack.Pop();
                continue;
            }

            object yielded = current.Current;
            if (yielded is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }

            if (yielded is UnityWebRequestAsyncOperation webOperation)
            {
                while (!webOperation.isDone)
                    yield return null;
                continue;
            }

            if (yielded is AsyncOperation operation)
            {
                while (!operation.isDone)
                    yield return null;
                continue;
            }

            yield return null;
        }
    }

    static IEnumerator RunJsonCommand(
        Func<Action<string, string>, IEnumerator> command,
        string expectedPath,
        FakeNovelApiServer server)
    {
        string response = null;
        string error = "unset";
        yield return RunUnityCoroutine(command((json, err) =>
        {
            response = json;
            error = err;
        }));

        Assert.That(error, Is.Null);
        Assert.That(response, Is.Not.Null.And.Not.Empty);
        AssertAuthorizedRuntimeRequest(server.LastRequest("GET", expectedPath));
    }

    static IEnumerator RunPrivateCoroutine(object instance, string methodName, params object[] args)
    {
        object routine = InvokePrivate(instance, methodName, args);
        Assert.That(routine, Is.InstanceOf<IEnumerator>(), methodName + " did not return IEnumerator.");
        yield return RunUnityCoroutine((IEnumerator)routine);
    }

    static void ConfigureNetworkManager(NetworkManager network, string baseUrl)
    {
        SetInstanceField(network, "_resolvedBaseUrl", baseUrl);
        SetInstanceField(network, "_runtimeConfig", new NetworkRuntimeConfigData
        {
            selectedEnvironmentId = "fake",
            requestTimeoutSeconds = 5,
            maxRetries = 0,
            retryDelaySeconds = 0f,
            retryServerErrors = false,
            showOfflineToasts = false,
            environments = new List<NetworkEnvironmentEntry>
            {
                new NetworkEnvironmentEntry
                {
                    id = "fake",
                    displayName = "Fake API",
                    baseUrl = baseUrl
                }
            }
        });
        SetInstanceField<object>(network, "_httpClient", null);

        bool applied = (bool)InvokePrivate(
            network,
            "ApplyAuthResponse",
            "{\"authToken\":\"" + FakeNovelApiServer.Jwt + "\",\"refreshToken\":\"fake-refresh\",\"playerId\":\"player_fake\"}");
        Assert.That(applied, Is.True);
    }

    static void ClearNetworkState()
    {
        SetAutoProperty(typeof(NetworkManager), "IsOnline", false);
        SetAutoProperty(typeof(NetworkManager), "IsAuthenticated", false);
        SetAutoProperty(typeof(NetworkManager), "AuthFlowCompleted", false);
        SetAutoProperty(typeof(NetworkManager), "LastNetworkError", "");
        SetAutoProperty(typeof(NetworkManager), "LastErrorKind", NetworkErrorKind.Success);
        SetAutoProperty(typeof(NetworkManager), "LastProgressNodeGuid", "");
        SetAutoProperty(typeof(NetworkManager), "LastProgressEpisodeId", "");
        SetAutoProperty(typeof(NetworkManager), "LastProgressSnapshotJson", "");
        SetAutoProperty(typeof(NetworkManager), "LastProgressRawJson", "");
        SetAutoProperty(typeof(NetworkManager), "LastProgressUpdatedAtIso", "");

        ClearStaticCollection("_lastUnlockedEpisodes");
        ClearStaticCollection("_lastProgressStats");
        ClearStaticCollection("_lastProgressFlags");
        ClearStaticCollection("_catalogSeasons");
        ClearStaticCollection("_catalogEpisodes");
        ClearStaticCollection("_pendingProgress");
        ClearStaticCollection("_pendingBookmarks");

        SetStaticField<string>(typeof(NetworkManager), "_authToken", null);
        SetStaticField<string>(typeof(NetworkManager), "_refreshToken", null);
        SetStaticField<string>(typeof(NetworkManager), "_playerId", null);
        SetAutoProperty(typeof(NetworkManager), "Instance", (NetworkManager)null);

        NetworkManager.CurrentProfile.playerId = "";
        NetworkManager.LastBalance.hearts = 0;
        NetworkManager.LastBalance.candles = 0;
        NetworkManager.LastBalance.candlesCap = 0;

        PlayerPrefs.DeleteKey("VN_AUTH_TOKEN");
        PlayerPrefs.DeleteKey("VN_REFRESH_TOKEN");
        PlayerPrefs.DeleteKey("VN_REFRESH_TOKEN_V2");
        PlayerPrefs.DeleteKey("VN_PLAYER_ID");
        PlayerPrefs.Save();
    }

    static object InvokePrivate(object instance, string methodName, params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName + " not found.");
        return method.Invoke(instance, args);
    }

    static void SetInstanceField<T>(object instance, string fieldName, T value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName + " not found.");
        field.SetValue(instance, value);
    }

    static void SetAutoProperty<T>(Type type, string propertyName, T value)
    {
        SetStaticField(type, "<" + propertyName + ">k__BackingField", value);
    }

    static void SetStaticField<T>(Type type, string fieldName, T value)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, fieldName + " not found.");
        field.SetValue(null, value);
    }

    static T GetStaticField<T>(Type type, string fieldName)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, fieldName + " not found.");
        return (T)field.GetValue(null);
    }

    static void ClearStaticCollection(string fieldName)
    {
        FieldInfo field = typeof(NetworkManager).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        object collection = field != null ? field.GetValue(null) : null;
        MethodInfo clear = collection != null ? collection.GetType().GetMethod("Clear") : null;
        clear?.Invoke(collection, null);
    }

    sealed class FakeNovelApiServer : IDisposable
    {
        public const string Jwt = "fake-jwt";
        public const string RefreshedJwt = "fake-jwt-refreshed";
        public const string RefreshToken = "fake-refresh";
        public const string RefreshedRefreshToken = "fake-refresh-refreshed";
        public const string AdminKey = "fake-admin-key";

        readonly object _lock = new object();
        readonly List<Request> _requests = new List<Request>();
        readonly HashSet<string> _ownedWardrobe = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _favorites = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _galleryScenes = new HashSet<string>(StringComparer.Ordinal) { "scene_intro" };
        readonly Dictionary<string, int> _wardrobeCosts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "outfit_city", 15 }
        };

        TcpListener _listener;
        Thread _thread;
        bool _disposed;
        int _hearts = 100;
        int _candles = 3;
        readonly List<string> _viewedScenes = new List<string>();

        public string BaseUrl { get; private set; }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUrl = "http://127.0.0.1:" + port;
            _thread = new Thread(ListenLoop) { IsBackground = true };
            _thread.Start();
        }

        public Request LastRequest(string method, string path)
        {
            lock (_lock)
            {
                for (int i = _requests.Count - 1; i >= 0; i--)
                {
                    Request request = _requests[i];
                    if (string.Equals(request.Method, method, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(request.Path, path, StringComparison.Ordinal))
                    {
                        return request;
                    }
                }
            }

            Assert.Fail("Fake API did not receive " + method + " " + path);
            return null;
        }

        public int CountRequests(string method, string path)
        {
            int count = 0;
            lock (_lock)
            {
                for (int i = 0; i < _requests.Count; i++)
                {
                    Request request = _requests[i];
                    if (string.Equals(request.Method, method, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(request.Path, path, StringComparison.Ordinal))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        void ListenLoop()
        {
            while (!_disposed)
            {
                try
                {
                    using (TcpClient client = _listener.AcceptTcpClient())
                    {
                        client.ReceiveTimeout = 5000;
                        client.SendTimeout = 5000;
                        HandleClient(client);
                    }
                }
                catch (SocketException)
                {
                    if (!_disposed)
                        Thread.Sleep(10);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (InvalidOperationException)
                {
                    return;
                }
            }
        }

        void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            Request request = ReadRequest(stream);
            if (request == null)
                return;

            lock (_lock)
                _requests.Add(request);

            Response response = Handle(request);
            WriteResponse(stream, response.StatusCode, response.Body);
        }

        Response Handle(Request request)
        {
            if (request.Path.StartsWith("/player/", StringComparison.Ordinal) ||
                request.Path.StartsWith("/content/", StringComparison.Ordinal) ||
                request.Path.StartsWith("/shop/", StringComparison.Ordinal) ||
                request.Path.StartsWith("/purchases/", StringComparison.Ordinal))
            {
                if (!request.Headers.TryGetValue("Authorization", out string authorization) ||
                    (authorization != "Bearer " + Jwt && authorization != "Bearer " + RefreshedJwt))
                {
                    return Json(401, "{\"error\":\"unauthorized\"}");
                }
            }

            if (request.Path.StartsWith("/unity/", StringComparison.Ordinal))
            {
                if (!request.Headers.TryGetValue("X-Admin-Key", out string adminKey) ||
                    adminKey != AdminKey)
                {
                    return Json(401, "{\"error\":\"admin_key_required\"}");
                }
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.AuthRefresh)
            {
                string refreshToken = NetworkJson.GetString(request.Body, "refreshToken");
                if (refreshToken != RefreshToken)
                    return Json(401, "{\"error\":\"invalid_refresh\"}");

                return Json(
                    200,
                    "{\"authToken\":\"" + RefreshedJwt + "\",\"refreshToken\":\"" + RefreshedRefreshToken + "\",\"playerId\":\"player_fake\"}");
            }

            if (request.Method == "GET" && request.Path == ApiRoutes.ContentCatalog)
            {
                return Json(
                    200,
                    "{\"episodes\":[{\"episodeId\":\"ep_s1e1\",\"seasonId\":\"s1_season1\",\"storyId\":\"story_1\",\"order\":1,\"title\":\"Chapter 1\",\"isPremium\":false,\"candleCost\":0,\"geoRestricted\":false,\"isUnlocked\":true,\"isCompleted\":false,\"contentVersion\":\"v1\",\"hasRemoteContent\":true}]}");
            }

            if (request.Method == "GET" && request.Path == ApiRoutes.ContentEpisodeVersion("ep_s1e1"))
                return Json(200, "{\"episodeId\":\"ep_s1e1\",\"contentVersion\":\"v2\"}");

            if (request.Method == "POST" && request.Path == ApiRoutes.ContentEpisodeUnlock)
            {
                string episodeId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "episodeId"));
                if (string.IsNullOrEmpty(episodeId))
                    return Json(422, "{\"error\":\"episode_required\"}");

                _candles = Math.Max(0, _candles - 1);
                return Json(200, "{\"ok\":true,\"episodeId\":\"" + NetworkJson.Escape(episodeId) + "\",\"candleCost\":1,\"candles\":" + _candles + "}");
            }

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerBalance)
                return Json(200, BalanceJson());

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerProfile)
                return Json(200, "{\"playerId\":\"player_fake\",\"locale\":\"ru\",\"platform\":\"editor\",\"heroName\":\"Alex\"}");

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerFeatures)
                return Json(200, "{\"fullAccess\":true,\"fastForward\":{\"enabled\":true,\"steps\":12},\"bookmarks\":{\"enabled\":true,\"capacity\":5}}");

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerProgress)
            {
                return Json(
                    200,
                    "{\"schemaVersion\":1,\"storyId\":\"story_1\",\"heroName\":\"Alex\",\"currentEpisodeId\":\"ep_s1e1\",\"currentNodeGuid\":\"node_intro\",\"updatedAt\":\"2026-05-16T07:00:12Z\",\"stats\":{\"trust\":3},\"flags\":{\"met_cat\":true},\"unlockedEpisodes\":[\"ep_s1e1\"],\"features\":{\"fullAccess\":true,\"fastForwardEnabled\":true,\"bookmarksEnabled\":true},\"snapshot\":{\"storyId\":\"story_1\",\"episodeId\":\"ep_s1e1\",\"currentNodeGuid\":\"node_intro\",\"savedAtIso\":\"2026-05-16T07:00:12Z\",\"playerName\":\"Alex\"}}");
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerProgressSave)
            {
                string episodeId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "episodeId"));
                string nodeId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "nodeId"));
                if (string.IsNullOrEmpty(episodeId) || string.IsNullOrEmpty(nodeId))
                    return Json(422, "{\"error\":\"progress_payload_required\"}");

                return Json(200, "{\"ok\":true,\"episodeId\":\"" + NetworkJson.Escape(episodeId) + "\",\"nodeId\":\"" + NetworkJson.Escape(nodeId) + "\"}");
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerProgressUndoChoice)
            {
                if (NetworkJson.GetInt(request.Body, "amount", 0) <= 0)
                    return Json(422, "{\"error\":\"amount_required\"}");

                return Json(200, "{\"ok\":true,\"hearts\":" + _hearts + ",\"candles\":" + _candles + "}");
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerProgressRewind)
            {
                string episodeId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "episodeId"));
                string nodeId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "nodeId"));
                if (string.IsNullOrEmpty(episodeId) || string.IsNullOrEmpty(nodeId))
                    return Json(422, "{\"error\":\"rewind_payload_required\"}");

                return Json(200, "{\"ok\":true,\"episodeId\":\"" + NetworkJson.Escape(episodeId) + "\",\"nodeId\":\"" + NetworkJson.Escape(nodeId) + "\"}");
            }

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerHeroName)
                return Json(200, "{\"storyId\":\"story_1\",\"heroName\":\"Alex\"}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerHeroName)
            {
                string storyId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "storyId"));
                string name = SaveDataSanitizer.SanitizePlayerName(NetworkJson.GetString(request.Body, "name"));
                if (string.IsNullOrEmpty(storyId) || string.IsNullOrEmpty(name))
                    return Json(422, "{\"error\":\"hero_name_required\"}");

                return Json(200, "{\"storyId\":\"" + NetworkJson.Escape(storyId) + "\",\"heroName\":\"" + NetworkJson.Escape(name) + "\"}");
            }

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerBookmark)
            {
                return Json(
                    200,
                    "{\"bookmark\":{\"nodeGuid\":\"node_bookmark\",\"episodeId\":\"ep_s1e1\",\"storyId\":\"story_1\",\"savedAt\":\"2026-05-16T07:00:12Z\",\"label\":\"Before choice\",\"snapshot\":{\"storyId\":\"story_1\",\"episodeId\":\"ep_s1e1\",\"currentNodeGuid\":\"node_bookmark\",\"savedAtIso\":\"2026-05-16T07:00:12Z\"}}}");
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerBookmarkSave)
            {
                string nodeGuid = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "nodeGuid"));
                string episodeId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "episodeId"));
                if (string.IsNullOrEmpty(nodeGuid) || string.IsNullOrEmpty(episodeId))
                    return Json(422, "{\"error\":\"bookmark_payload_required\"}");

                return Json(200, "{\"ok\":true,\"nodeGuid\":\"" + NetworkJson.Escape(nodeGuid) + "\",\"episodeId\":\"" + NetworkJson.Escape(episodeId) + "\"}");
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerPushToken)
            {
                string token = NetworkJson.GetString(request.Body, "token");
                if (string.IsNullOrEmpty(token))
                    return Json(422, "{\"error\":\"push_token_required\"}");

                return Json(200, "{\"ok\":true}");
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerHeartsSpend)
            {
                int amount = NetworkJson.GetInt(request.Body, "amount", -1);
                if (amount <= 0 || amount > _hearts)
                    return Json(422, "{\"error\":\"insufficient_hearts\"}");

                _hearts -= amount;
                return Json(200, BalanceJson());
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerCandlesSpend)
            {
                int amount = NetworkJson.GetInt(request.Body, "amount", -1);
                if (amount <= 0 || amount > _candles)
                    return Json(422, "{\"error\":\"insufficient_candles\"}");

                _candles -= amount;
                return Json(200, BalanceJson());
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerAdReward)
            {
                _hearts += 2;
                return Json(200, BalanceJson());
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerDailyClaim)
            {
                _hearts += 5;
                return Json(200, BalanceJson());
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerWardrobeBuy)
            {
                string itemId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "itemId"));
                if (string.IsNullOrEmpty(itemId) || !_wardrobeCosts.TryGetValue(itemId, out int price))
                    return Json(404, "{\"error\":\"item_not_found\"}");
                if (_ownedWardrobe.Contains(itemId))
                    return Json(409, "{\"error\":\"already_owned\"}");
                if (_hearts < price)
                    return Json(422, "{\"error\":\"insufficient_hearts\"}");

                _hearts -= price;
                _ownedWardrobe.Add(itemId);
                return Json(200, "{\"itemId\":\"" + NetworkJson.Escape(itemId) + "\",\"pricePaid\":" + price + ",\"heartsRemaining\":" + _hearts + "}");
            }

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerWardrobe)
                return Json(200, "{\"playerId\":\"player_fake\",\"owned\":[{\"itemId\":\"outfit_city\",\"pricePaid\":15,\"purchasedAt\":\"2026-05-16T07:00:12Z\"}]}");

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerScenesViewed)
                return Json(200, "{\"episodeId\":\"ep_s1e1\",\"nodeIds\":[\"node_intro\",\"node_choice\"],\"count\":2}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerScenesViewed)
            {
                string rawNodeIds = NetworkJson.GetRawValue(request.Body, "nodeIds");
                foreach (string rawNode in NetworkJson.GetArrayItems(rawNodeIds))
                {
                    string nodeId = rawNode.Trim('"');
                    if (!_viewedScenes.Contains(nodeId))
                        _viewedScenes.Add(nodeId);
                }

                return Json(200, "{\"ok\":true,\"count\":" + _viewedScenes.Count + "}");
            }

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerGallery)
                return Json(200, "{\"unlockedScenes\":[\"scene_intro\"],\"count\":1}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerGalleryUnlock)
            {
                string sceneId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "sceneId"));
                if (string.IsNullOrEmpty(sceneId))
                    return Json(422, "{\"error\":\"scene_required\"}");

                bool isNew = _galleryScenes.Add(sceneId);
                return Json(200, "{\"unlocked\":true,\"sceneId\":\"" + NetworkJson.Escape(sceneId) + "\",\"isNew\":" + (isNew ? "true" : "false") + "}");
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerEpisodeComplete)
                return Json(200, "{\"ok\":true,\"episodeId\":\"" + NetworkJson.Escape(NetworkJson.GetString(request.Body, "episodeId")) + "\",\"hearts\":" + _hearts + ",\"candles\":" + _candles + "}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerEpisodeReplay)
                return Json(200, "{\"ok\":true,\"episodeId\":\"" + NetworkJson.Escape(NetworkJson.GetString(request.Body, "episodeId")) + "\"}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerEpisodeJump)
                return Json(200, "{\"ok\":true,\"episodeId\":\"" + NetworkJson.Escape(NetworkJson.GetString(request.Body, "episodeId")) + "\"}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerSeasonRestart)
                return Json(200, "{\"ok\":true,\"seasonId\":\"" + NetworkJson.Escape(NetworkJson.GetString(request.Body, "seasonId")) + "\"}");

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerSlots)
                return Json(200, "{\"slots\":[{\"slotId\":1,\"active\":true},{\"slotId\":2,\"active\":false}],\"count\":2}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerSlotSwitch)
                return Json(200, "{\"ok\":true,\"slotId\":" + NetworkJson.GetInt(request.Body, "slotId", 0) + "}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerSlotFork)
                return Json(200, "{\"ok\":true,\"slotId\":3}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerStoryReset)
                return Json(200, "{\"ok\":true,\"storyId\":\"" + NetworkJson.Escape(NetworkJson.GetString(request.Body, "storyId")) + "\"}");

            if (request.Method == "GET" && request.Path == "/player/cat/greet")
                return Json(200, "{\"phrase\":\"hello\",\"show\":true}");

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerCatName)
                return Json(200, "{\"name\":\"Mira\"}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerCatName)
                return Json(200, "{\"ok\":true,\"name\":\"" + NetworkJson.Escape(NetworkJson.GetString(request.Body, "name")) + "\"}");

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerDiceStatus)
                return Json(200, "{\"canRoll\":true,\"nextRollAt\":null}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerDiceRoll)
                return Json(200, "{\"ok\":true,\"result\":4,\"reward\":{\"hearts\":1},\"hearts\":" + (_hearts + 1) + "}");

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerTarotStatus)
                return Json(200, "{\"canDraw\":true,\"lastDrawAt\":null,\"nextDrawAt\":null}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerTarotDraw)
                return Json(200, "{\"ok\":true,\"card\":{\"id\":\"card_1\",\"name\":\"Secret\"},\"reward\":{\"hearts\":1},\"hearts\":" + (_hearts + 1) + "}");

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerFavorites)
                return Json(200, "{\"favorites\":[\"story_1\"],\"count\":1}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerFavorites)
            {
                string storyId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "storyId"));
                if (!string.IsNullOrEmpty(storyId))
                    _favorites.Add(storyId);

                return Json(200, "{\"ok\":true,\"storyId\":\"" + NetworkJson.Escape(storyId) + "\"}");
            }

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerFavoriteCheck("story_1"))
                return Json(200, "{\"storyId\":\"story_1\",\"favorite\":true}");

            if (request.Method == "DELETE" && request.Path == ApiRoutes.PlayerFavoriteForStory("story_1"))
            {
                _favorites.Remove("story_1");
                return Json(200, "{\"ok\":true,\"storyId\":\"story_1\"}");
            }

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerRelationships)
                return Json(200, "{\"relationships\":[{\"characterId\":\"char_mira\",\"level\":1}],\"count\":1}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerRelationshipUnlock)
            {
                string characterId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "characterId"));
                if (string.IsNullOrEmpty(characterId))
                    return Json(422, "{\"error\":\"character_required\"}");

                return Json(200, "{\"ok\":true,\"characterId\":\"" + NetworkJson.Escape(characterId) + "\"}");
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerRelationshipUpdate)
            {
                string characterId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "characterId"));
                if (string.IsNullOrEmpty(characterId))
                    return Json(422, "{\"error\":\"character_required\"}");

                return Json(200, "{\"ok\":true,\"characterId\":\"" + NetworkJson.Escape(characterId) + "\",\"delta\":" + NetworkJson.GetInt(request.Body, "delta", 0) + "}");
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerPromoApply)
            {
                string code = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "code"));
                if (string.IsNullOrEmpty(code))
                    return Json(422, "{\"error\":\"code_required\"}");

                _hearts += 5;
                return Json(200, "{\"ok\":true,\"code\":\"" + NetworkJson.Escape(code) + "\",\"hearts\":" + _hearts + "}");
            }

            if (request.Method == "GET" && request.Path == ApiRoutes.ShopPrices)
                return Json(200, "{\"prices\":[{\"productId\":\"pack_small\",\"priceLabel\":\"$0.99\",\"sortOrder\":1}],\"count\":1}");

            if (request.Method == "GET" && request.Path == ApiRoutes.ShopItems)
                return Json(200, "{\"items\":[{\"productId\":\"pack_small\",\"title\":\"Small pack\",\"hearts\":10,\"sortOrder\":1}],\"count\":1}");

            if (request.Method == "POST" && request.Path == ApiRoutes.ShopOrders)
            {
                string productId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "productId"));
                if (string.IsNullOrEmpty(productId))
                    return Json(422, "{\"error\":\"product_required\"}");

                return Json(200, "{\"orderId\":\"order_1\",\"productId\":\"" + NetworkJson.Escape(productId) + "\",\"status\":\"created\"}");
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PurchasesConfirm)
            {
                string productId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "productId"));
                string receipt = NetworkJson.GetString(request.Body, "receipt");
                if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(receipt))
                    return Json(422, "{\"error\":\"purchase_payload_required\"}");

                _hearts += 10;
                return Json(200, "{\"ok\":true,\"productId\":\"" + NetworkJson.Escape(productId) + "\",\"hearts\":" + _hearts + "}");
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.PurchasesRestore)
            {
                string store = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(request.Body, "store"));
                if (string.IsNullOrEmpty(store))
                    return Json(422, "{\"error\":\"store_required\"}");

                return Json(200, "{\"ok\":true,\"store\":\"" + NetworkJson.Escape(store) + "\",\"restored\":1}");
            }

            if (request.Method == "GET" && request.Path == ApiRoutes.PurchasesHistory)
                return Json(200, "{\"history\":[{\"productId\":\"pack_small\",\"transactionId\":\"tx_1\"}],\"count\":1}");

            if (request.Method == "GET" && request.Path == ApiRoutes.PurchasesProducts)
                return Json(200, "{\"products\":[{\"productId\":\"pack_small\",\"title\":\"Small pack\",\"hearts\":10}],\"count\":1}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerChapterComplete)
                return Json(200, "{\"ok\":true,\"episodeId\":\"" + NetworkJson.Escape(NetworkJson.GetString(request.Body, "episodeId")) + "\"}");

            if (request.Method == "POST" && request.Path == ApiRoutes.PlayerStoryComplete)
                return Json(200, "{\"ok\":true,\"storyId\":\"" + NetworkJson.Escape(NetworkJson.GetString(request.Body, "storyId")) + "\",\"hearts\":" + _hearts + "}");

            if (request.Method == "GET" && request.Path == ApiRoutes.PlayerReadingStats)
                return Json(200, "{\"chaptersCompleted\":1,\"storiesCompleted\":1,\"minutesRead\":42}");

            if (request.Method == "GET" && request.Path == ApiRoutes.ContentEpisodeGraph("ep_s1e1"))
            {
                return Json(
                    200,
                    "{\"episodeId\":\"ep_s1e1\",\"contentVersion\":\"v1\",\"graph\":{\"episodeId\":\"ep_s1e1\",\"nodes\":[]}}");
            }

            if (request.Method == "POST" && request.Path == ApiRoutes.UnityChoiceCosts)
            {
                string rawChoices = NetworkJson.GetRawValue(request.Body, "choices");
                if (string.IsNullOrWhiteSpace(rawChoices) || !rawChoices.Contains("choice_paid"))
                    return Json(422, "{\"error\":\"choices_required\"}");

                return Json(200, "{\"ok\":true,\"count\":1}");
            }

            if (request.Method == "GET" && request.Path == ApiRoutes.UnityChoiceCosts)
                return Json(200, "{\"count\":1,\"choices\":[{\"nodeGuid\":\"choice_paid\",\"cost\":7}]}");

            if (request.Method == "DELETE" && request.Path == ApiRoutes.UnityChoiceCostByNode("choice_paid"))
                return Json(200, "{\"ok\":true,\"deleted\":\"choice_paid\"}");

            if (request.Method == "POST" && request.Path == ApiRoutes.UnityWardrobeCosts)
            {
                string rawItems = NetworkJson.GetRawValue(request.Body, "items");
                if (string.IsNullOrWhiteSpace(rawItems) ||
                    !rawItems.Contains("outfit_city") ||
                    !rawItems.Contains("\"price\":15"))
                {
                    return Json(422, "{\"error\":\"items_required\"}");
                }

                return Json(200, "{\"ok\":true,\"count\":1}");
            }

            if (request.Method == "GET" && request.Path == ApiRoutes.UnityWardrobeCosts)
                return Json(200, "{\"count\":1,\"costs\":{\"outfit_city\":15}}");

            return Json(404, "{\"error\":\"not_found\",\"path\":\"" + NetworkJson.Escape(request.Path) + "\"}");
        }

        string BalanceJson()
        {
            return "{\"hearts\":" + _hearts + ",\"candles\":" + _candles + ",\"candlesCap\":5,\"isSubscriber\":false,\"adMultiplier\":1,\"dailyStreakDay\":2,\"catName\":\"Barsik\"}";
        }

        static Request ReadRequest(NetworkStream stream)
        {
            byte[] bytes = ReadRequestBytes(stream);
            if (bytes.Length == 0)
                return null;

            int headerEnd = IndexOf(bytes, Encoding.ASCII.GetBytes("\r\n\r\n"), bytes.Length);
            if (headerEnd < 0)
                return null;

            string headerText = Encoding.UTF8.GetString(bytes, 0, headerEnd);
            string[] lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0)
                return null;

            string[] requestLine = lines[0].Split(' ');
            if (requestLine.Length < 2)
                return null;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0)
                    continue;

                string key = lines[i].Substring(0, colon).Trim();
                string value = lines[i].Substring(colon + 1).Trim();
                headers[key] = value;
            }

            int bodyStart = headerEnd + 4;
            string body = bodyStart < bytes.Length
                ? Encoding.UTF8.GetString(bytes, bodyStart, bytes.Length - bodyStart)
                : "";

            string target = requestLine[1];
            string path = target;
            int query = path.IndexOf('?');
            if (query >= 0)
                path = path.Substring(0, query);

            return new Request
            {
                Method = requestLine[0].ToUpperInvariant(),
                Target = target,
                Path = path,
                Headers = headers,
                Body = body
            };
        }

        static byte[] ReadRequestBytes(NetworkStream stream)
        {
            var data = new MemoryStream();
            byte[] buffer = new byte[4096];
            byte[] marker = Encoding.ASCII.GetBytes("\r\n\r\n");
            int headerEnd = -1;

            while (headerEnd < 0)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;

                data.Write(buffer, 0, read);
                byte[] current = data.ToArray();
                headerEnd = IndexOf(current, marker, current.Length);
                if (current.Length > 64 * 1024)
                    break;
            }

            byte[] bytes = data.ToArray();
            if (headerEnd < 0)
                return bytes;

            string headers = Encoding.UTF8.GetString(bytes, 0, headerEnd);
            int contentLength = ParseContentLength(headers);
            int bodyStart = headerEnd + marker.Length;
            while (bytes.Length - bodyStart < contentLength)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;

                data.Write(buffer, 0, read);
                bytes = data.ToArray();
            }

            return bytes;
        }

        static int ParseContentLength(string headers)
        {
            string[] lines = headers.Split(new[] { "\r\n" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                int colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;

                if (string.Equals(line.Substring(0, colon).Trim(), "Content-Length", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(line.Substring(colon + 1).Trim(), out int value))
                {
                    return Math.Max(0, value);
                }
            }

            return 0;
        }

        static int IndexOf(byte[] haystack, byte[] needle, int length)
        {
            if (haystack == null || needle == null || needle.Length == 0)
                return -1;

            int max = Math.Min(length, haystack.Length) - needle.Length;
            for (int i = 0; i <= max; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return i;
            }

            return -1;
        }

        static Response Json(int statusCode, string body)
        {
            return new Response { StatusCode = statusCode, Body = body ?? "{}" };
        }

        static void WriteResponse(NetworkStream stream, int statusCode, string body)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body ?? "{}");
            string reason = statusCode >= 200 && statusCode < 300 ? "OK" : "ERROR";
            string header =
                "HTTP/1.1 " + statusCode + " " + reason + "\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                "Content-Length: " + bodyBytes.Length + "\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
            stream.Flush();
        }

        public void Dispose()
        {
            _disposed = true;
            try
            {
                _listener?.Stop();
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }

            if (_thread != null && _thread.IsAlive)
                _thread.Join(1000);
        }

        public sealed class Request
        {
            public string Method;
            public string Target;
            public string Path;
            public Dictionary<string, string> Headers;
            public string Body;
        }

        sealed class Response
        {
            public int StatusCode;
            public string Body;
        }
    }
}
#endif
