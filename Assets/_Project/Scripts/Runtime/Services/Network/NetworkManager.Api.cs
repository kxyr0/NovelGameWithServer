using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public sealed partial class NetworkManager
{
    private const int MaxNodeIdsPerBatch = 500;
    private const int MaxPurchaseReceiptChars = 64 * 1024;

    public IEnumerator RefreshAuthToken(Action<bool> callback = null)
    {
        if (string.IsNullOrEmpty(_refreshToken))
        {
            callback?.Invoke(false);
            yield break;
        }

        bool refreshed = false;
        yield return RestoreSession(GetOrCreateDeviceId(), _refreshToken, ok => refreshed = ok, startSync: false);
        callback?.Invoke(refreshed);
    }

    public IEnumerator RestoreAccountWithCode(string restoreCode, Action<bool> callback = null)
    {
        restoreCode = SanitizeCredential(restoreCode, 256);
        if (string.IsNullOrEmpty(restoreCode))
        {
            callback?.Invoke(false);
            yield break;
        }

        var body = new RestoreCodeAuthRequest
        {
            restoreCode = restoreCode,
            deviceId = GetOrCreateDeviceId()
        };

        yield return Post(ApiRoutes.AuthRestore, body, null, (json, err) =>
        {
            if (err != null)
            {
                AppLogger.Warn(
                    AppLogCategory.Auth,
                    nameof(NetworkManager),
                    nameof(RestoreAccountWithCode),
                    "Restore-code authentication failed.",
                    LogMetadata.Of("endpoint", ApiRoutes.AuthRestore, "error", err),
                    recoverable: true);
                callback?.Invoke(false);
                return;
            }

            bool applied = ApplyAuthResponse(json);
            if (applied)
                StartPostAuthSync();
            callback?.Invoke(applied);
        }, allowRetry: true);
    }

    public IEnumerator SocialAuth(string provider, string idToken, Action<bool> callback = null)
    {
        provider = (provider ?? "").Trim().ToLowerInvariant();
        if (provider != "apple")
        {
            callback?.Invoke(false);
            yield break;
        }

        idToken = SanitizeCredential(idToken, MaxCredentialLength);
        if (string.IsNullOrEmpty(idToken))
        {
            callback?.Invoke(false);
            yield break;
        }

        var body = new SocialAuthRequest
        {
            provider = provider,
            idToken = idToken,
            deviceId = GetOrCreateDeviceId()
        };

        yield return Post(ApiRoutes.AuthSocial, body, null, (json, err) =>
        {
            if (err != null)
            {
                AppLogger.Warn(
                    AppLogCategory.Auth,
                    nameof(NetworkManager),
                    nameof(SocialAuth),
                    "Social authentication failed.",
                    LogMetadata.Of("endpoint", ApiRoutes.AuthSocial, "provider", body.provider, "error", err),
                    recoverable: true);
                callback?.Invoke(false);
                return;
            }

            bool applied = ApplyAuthResponse(json);
            if (applied)
                StartPostAuthSync();
            callback?.Invoke(applied);
        }, allowRetry: true);
    }

    public IEnumerator CheckHealth(Action<string, string> callback)
    {
        yield return SendRequest(
            () => _httpClient.CreateGetRequest(ApiRoutes.Health, null),
            result => callback?.Invoke(result.IsSuccess ? result.Text : null, result.IsSuccess ? null : result.Error),
            allowRetry: true);
    }

    public IEnumerator FetchProfile(Action<string, string> callback)
    {
        yield return GetRuntime(ApiRoutes.PlayerProfile, callback);
    }

    public IEnumerator FetchEpisodeVersion(string episodeId, Action<string, string> callback)
    {
        string safeEpisodeId = SanitizeIdForPath(episodeId);
        if (string.IsNullOrEmpty(safeEpisodeId))
        {
            callback?.Invoke(null, "Invalid episodeId.");
            yield break;
        }

        yield return GetRuntime(ApiRoutes.ContentEpisodeVersion(safeEpisodeId), callback);
    }

    public IEnumerator UnlockEpisode(string episodeId, bool confirmed, Action<bool, string> callback = null)
    {
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        if (string.IsNullOrEmpty(episodeId))
        {
            callback?.Invoke(false, "Invalid episodeId.");
            yield break;
        }

        if (IsCatalogEpisodeUnlocked(episodeId, false))
        {
            MarkCatalogEpisodeUnlocked(episodeId, null);
            callback?.Invoke(true, "");
            yield break;
        }

        var body = new EpisodeUnlockRequest
        {
            episodeId = episodeId,
            confirmed = confirmed
        };

        yield return PostRuntimeMutation(ApiRoutes.ContentEpisodeUnlock, body, (ok, payload) =>
        {
            if (ok)
                MarkCatalogEpisodeUnlocked(episodeId, payload);
            callback?.Invoke(ok, payload);
        }, applyBalance: true);
    }

    public IEnumerator CompleteEpisode(string episodeId, string nextEpisodeId = null, Action<bool, string> callback = null)
    {
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        nextEpisodeId = SaveDataSanitizer.SanitizeIdentifier(nextEpisodeId);
        if (string.IsNullOrEmpty(episodeId))
        {
            callback?.Invoke(false, "Invalid episodeId.");
            yield break;
        }

        var body = new EpisodeCompleteRequest
        {
            episodeId = episodeId,
            nextEpisodeId = nextEpisodeId
        };

        yield return PostRuntimeMutation(ApiRoutes.PlayerEpisodeComplete, body, callback, applyBalance: true);
    }

    public void CompleteEpisodeAsync(string episodeId, string nextEpisodeId = null)
    {
        StartCoroutine(CompleteEpisode(episodeId, nextEpisodeId));
    }

    public IEnumerator ReplayEpisode(string episodeId, Action<bool, string> callback = null)
    {
        yield return PostEpisodeIdMutation(ApiRoutes.PlayerEpisodeReplay, episodeId, callback);
    }

    public IEnumerator JumpToEpisode(string episodeId, Action<bool, string> callback = null)
    {
        yield return PostEpisodeIdMutation(ApiRoutes.PlayerEpisodeJump, episodeId, callback);
    }

    public IEnumerator RestartSeason(string seasonId, Action<bool, string> callback = null)
    {
        seasonId = SaveDataSanitizer.SanitizeIdentifier(seasonId);
        if (string.IsNullOrEmpty(seasonId))
        {
            callback?.Invoke(false, "Invalid seasonId.");
            yield break;
        }

        yield return PostRuntimeMutation(
            ApiRoutes.PlayerSeasonRestart,
            new SeasonIdRequest { seasonId = seasonId },
            callback,
            applyBalance: false);
    }

    public IEnumerator SpendHearts(int amount, Action<bool> callback)
    {
        yield return SpendHearts(amount, "", "", callback);
    }

    public IEnumerator SpendHearts(int amount, string reason, string nodeGuid, Action<bool> callback)
    {
        yield return SpendHearts(amount, reason, nodeGuid, -1, "", callback);
    }

    public IEnumerator SpendHearts(int amount, string reason, string nodeGuid, int choiceIndex, string purchaseKey, Action<bool> callback)
    {
        if (amount <= 0 || amount > SaveDataSanitizer.MaxCurrencyValue)
        {
            AppLogger.Warn(
                AppLogCategory.Network,
                nameof(NetworkManager),
                nameof(SpendHearts),
                "Refusing invalid hearts spend request.",
                LogMetadata.Of("amount", amount, "maxAmount", SaveDataSanitizer.MaxCurrencyValue),
                recoverable: true);
            callback?.Invoke(false);
            yield break;
        }

        if (!IsAuthenticated)
        {
            if (!PrototypeFeatureFlags.LocalPremiumSpendEnabled)
            {
                AppLogger.Warn(
                    AppLogCategory.Security,
                    nameof(NetworkManager),
                    nameof(SpendHearts),
                    "Local hearts spend fallback is disabled.",
                    recoverable: true);
                callback?.Invoke(false);
                yield break;
            }

            bool ok = PlayerData.Hearts >= amount;
            if (ok)
                PlayerData.AddHeartValue(-amount);
            callback?.Invoke(ok);
            yield break;
        }

        string safeReason = SaveDataSanitizer.SanitizeIdentifier(reason);
        string safeNodeGuid = SaveDataSanitizer.SanitizeIdentifier(nodeGuid);
        string safePurchaseKey = SaveDataSanitizer.SanitizeIdentifier(purchaseKey);

        var body = new HeartsSpendRequest
        {
            amount = amount
        };

        yield return PostRuntimeMutation(ApiRoutes.PlayerHeartsSpend, body, (ok, payload) =>
        {
            if (!ok)
            {
                AppLogger.Warn(
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(SpendHearts),
                    "Server did not confirm hearts spend.",
                    LogMetadata.Of(
                        "endpoint", ApiRoutes.PlayerHeartsSpend,
                        "amount", amount,
                        "reason", safeReason,
                        "nodeGuid", safeNodeGuid,
                        "choiceIndex", choiceIndex,
                        "purchaseKey", safePurchaseKey,
                        "payload", payload ?? ""),
                    recoverable: true);
            }

            callback?.Invoke(ok);
        }, applyBalance: true);
    }

    public IEnumerator PurchaseWardrobeItem(int amount, string purchaseKey, Action<bool> callback)
    {
        yield return PurchaseWardrobeItemById(purchaseKey, amount, callback);
    }

    public IEnumerator PurchaseWardrobeItem(string itemId, Action<bool> callback)
    {
        yield return PurchaseWardrobeItemById(itemId, 0, callback);
    }

    public IEnumerator SyncWardrobeOwnership(Action<bool> callback = null)
    {
        if (!IsAuthenticated)
        {
            callback?.Invoke(false);
            yield break;
        }

        yield return GetRuntime(ApiRoutes.PlayerWardrobe, (json, err) =>
        {
            if (err != null)
            {
                AppLogger.Warn(
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(SyncWardrobeOwnership),
                    "Server wardrobe ownership sync failed.",
                    LogMetadata.Of("endpoint", ApiRoutes.PlayerWardrobe, "error", err),
                    recoverable: true);
                callback?.Invoke(false);
                return;
            }

            try
            {
                List<string> itemIds = ParseOwnedWardrobeItemIds(json);
                int mergedCount = MergeOwnedWardrobeItems(itemIds);
                AppLogger.Info(
                    AppLogCategory.Wardrobe,
                    nameof(NetworkManager),
                    nameof(SyncWardrobeOwnership),
                    "[WARDROBE][SYNC] Server wardrobe ownership applied.",
                    LogMetadata.Of(
                        "endpoint", ApiRoutes.PlayerWardrobe,
                        "serverOwnedCount", itemIds.Count,
                        "mergedCount", mergedCount,
                        "hasGameState", GameState.Instance != null));
                callback?.Invoke(true);
            }
            catch (Exception e)
            {
                AppLogger.Error(
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(SyncWardrobeOwnership),
                    "Failed to parse player wardrobe ownership response.",
                    e,
                    LogMetadata.Of("endpoint", ApiRoutes.PlayerWardrobe),
                    recoverable: true);
                SetLastError(NetworkErrorKind.InvalidResponse, e.Message);
                callback?.Invoke(false);
            }
        });
    }

    private IEnumerator PurchaseWardrobeItemById(string itemId, int expectedCost, Action<bool> callback)
    {
        itemId = SaveDataSanitizer.SanitizeIdentifier(itemId);
        expectedCost = SaveDataSanitizer.ClampCurrencyValue(expectedCost);
        if (string.IsNullOrEmpty(itemId))
        {
            callback?.Invoke(false);
            yield break;
        }

        var body = new WardrobePurchaseRequest
        {
            itemId = itemId
        };

        yield return PostRuntime(ApiRoutes.PlayerWardrobeBuy, body, (json, err) =>
        {
            if (err != null)
            {
                AppLogger.Warn(
                    AppLogCategory.Wardrobe,
                    nameof(NetworkManager),
                    nameof(PurchaseWardrobeItemById),
                    "[WARDROBE][PURCHASE_FAILED] Server did not confirm wardrobe purchase.",
                    LogMetadata.Of(
                        "endpoint", ApiRoutes.PlayerWardrobeBuy,
                        "itemId", itemId,
                        "expectedCost", expectedCost,
                        "error", err),
                    recoverable: true);
                callback?.Invoke(false);
                return;
            }

            if (ResponseHasApiError(json))
            {
                AppLogger.Warn(
                    AppLogCategory.Wardrobe,
                    nameof(NetworkManager),
                    nameof(PurchaseWardrobeItemById),
                    "[WARDROBE][PURCHASE_FAILED] Server returned wardrobe purchase error.",
                    LogMetadata.Of(
                        "endpoint", ApiRoutes.PlayerWardrobeBuy,
                        "itemId", itemId,
                        "expectedCost", expectedCost,
                        "apiError", NetworkJson.GetString(json, "error")),
                    recoverable: true);
                callback?.Invoke(false);
                return;
            }

            try
            {
                ApplyWardrobePurchaseResponse(json, itemId, expectedCost);
                callback?.Invoke(true);
            }
            catch (Exception e)
            {
                AppLogger.Error(
                    AppLogCategory.Network,
                    nameof(NetworkManager),
                    nameof(PurchaseWardrobeItemById),
                    "Failed to parse wardrobe purchase response.",
                    e,
                    LogMetadata.Of("endpoint", ApiRoutes.PlayerWardrobeBuy, "itemId", itemId),
                    recoverable: true);
                SetLastError(NetworkErrorKind.InvalidResponse, e.Message);
                callback?.Invoke(false);
            }
        });
    }

    private void ApplyWardrobePurchaseResponse(string json, string requestedItemId, int expectedCost)
    {
        if (!NetworkJson.LooksLikeJsonObject(json))
            throw new Exception("Wardrobe purchase response is not a JSON object");

        WardrobePurchaseResponse response = NetworkJson.FromJson<WardrobePurchaseResponse>(json);
        string purchasedItemId = SaveDataSanitizer.SanitizeIdentifier(FirstNonEmptyRawString(
            response != null ? response.itemId : "",
            NetworkJson.GetString(json, "itemId"),
            requestedItemId));
        if (string.IsNullOrEmpty(purchasedItemId))
            throw new Exception("Wardrobe purchase response has no itemId");

        ApplyBalancePatchFromJson(json);
        int heartsRemaining = ApplyWardrobeHeartsRemaining(json);
        int pricePaid = NetworkJson.GetRawValue(json, "pricePaid") != null
            ? SaveDataSanitizer.ClampCurrencyValue(NetworkJson.GetInt(json, "pricePaid", 0))
            : 0;

        if (GameState.Instance != null)
            GameState.Instance.AddClothing(purchasedItemId);

        if (expectedCost > 0 && pricePaid > 0 && expectedCost != pricePaid)
        {
            AppLogger.Warn(
                AppLogCategory.Wardrobe,
                nameof(NetworkManager),
                nameof(ApplyWardrobePurchaseResponse),
                "[WARDROBE][PURCHASE] Server wardrobe price differs from local node cost.",
                LogMetadata.Of(
                    "itemId", purchasedItemId,
                    "expectedCost", expectedCost,
                    "pricePaid", pricePaid),
                recoverable: true);
        }

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(NetworkManager),
            nameof(ApplyWardrobePurchaseResponse),
            "[WARDROBE][PURCHASE] Server wardrobe purchase applied.",
            LogMetadata.Of(
                "itemId", purchasedItemId,
                "pricePaid", pricePaid,
                "heartsRemaining", heartsRemaining,
                "hasGameState", GameState.Instance != null));
    }

    private int ApplyWardrobeHeartsRemaining(string json)
    {
        if (NetworkJson.GetRawValue(json, "heartsRemaining") == null)
            return -1;

        int heartsRemaining = SaveDataSanitizer.ClampCurrencyValue(NetworkJson.GetInt(json, "heartsRemaining", _lastBalance.hearts));
        _lastBalance.hearts = heartsRemaining;
        _lastBalance.updatedAtIso = DateTime.UtcNow.ToString("o");
        PlayerData.SetHeartsValue(heartsRemaining);
        return heartsRemaining;
    }

    private static List<string> ParseOwnedWardrobeItemIds(string json)
    {
        if (!NetworkJson.LooksLikeJsonObject(json))
            throw new Exception("Wardrobe ownership response is not a JSON object");

        var result = new List<string>();
        WardrobeOwnershipResponse response = NetworkJson.FromJson<WardrobeOwnershipResponse>(json);
        if (response != null && response.owned != null)
        {
            for (int i = 0; i < response.owned.Count; i++)
            {
                WardrobeOwnedItemResponse item = response.owned[i];
                if (item != null)
                    AddWardrobeItemId(result, item.itemId);
            }
        }

        string rawOwned = FirstNonEmptyRaw(
            NetworkJson.GetRawValue(json, "owned"),
            NetworkJson.GetRawValue(json, "items"),
            NetworkJson.GetRawValue(json, "wardrobe"));
        if (!string.IsNullOrWhiteSpace(rawOwned))
        {
            foreach (string rawItem in NetworkJson.GetArrayItems(rawOwned))
                AddWardrobeItemId(result, ExtractWardrobeItemId(rawItem));
        }

        return result;
    }

    private static string ExtractWardrobeItemId(string rawItem)
    {
        if (string.IsNullOrWhiteSpace(rawItem))
            return "";

        string trimmed = rawItem.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
            return NetworkJson.GetString(trimmed, "itemId");

        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
            return NetworkJson.GetString("{\"itemId\":" + trimmed + "}", "itemId");

        return trimmed;
    }

    private static void AddWardrobeItemId(List<string> itemIds, string itemId)
    {
        if (itemIds == null || itemIds.Count >= SaveDataSanitizer.MaxWardrobeEntries)
            return;

        itemId = SaveDataSanitizer.SanitizeIdentifier(itemId);
        if (!string.IsNullOrEmpty(itemId) && !itemIds.Contains(itemId))
            itemIds.Add(itemId);
    }

    private static int MergeOwnedWardrobeItems(List<string> itemIds)
    {
        if (GameState.Instance == null || itemIds == null || itemIds.Count == 0)
            return 0;

        return GameState.Instance.AddClothingRange(itemIds);
    }

    public IEnumerator ClaimAdReward(Action<bool, string> callback = null)
    {
        yield return PostRuntimeMutation(ApiRoutes.PlayerAdReward, new EmptyRequest(), callback, applyBalance: true);
    }

    public IEnumerator ClaimDailyReward(Action<bool, string> callback = null)
    {
        yield return PostRuntimeMutation(ApiRoutes.PlayerDailyClaim, new EmptyRequest(), callback, applyBalance: true);
    }

    public IEnumerator UndoChoice(int amount, string nodeGuid = null, bool isOutfit = false, Action<bool, string> callback = null)
    {
        if (amount <= 0 || amount > SaveDataSanitizer.MaxCurrencyValue)
        {
            AppLogger.Warn(
                AppLogCategory.Network,
                nameof(NetworkManager),
                nameof(UndoChoice),
                "Refusing invalid undo-choice amount.",
                LogMetadata.Of("amount", amount, "maxAmount", SaveDataSanitizer.MaxCurrencyValue),
                recoverable: true);
            callback?.Invoke(false, "Invalid undo amount.");
            yield break;
        }

        var body = new UndoChoiceRequest
        {
            amount = amount,
            nodeGuid = SaveDataSanitizer.SanitizeIdentifier(nodeGuid),
            isOutfit = isOutfit
        };

        yield return PostRuntimeMutation(ApiRoutes.PlayerProgressUndoChoice, body, callback, applyBalance: true);
    }

    public IEnumerator RewindProgress(string episodeId, string nodeGuid, Action<string, string> callback)
    {
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        nodeGuid = SaveDataSanitizer.SanitizeIdentifier(nodeGuid);
        if (string.IsNullOrEmpty(episodeId) || string.IsNullOrEmpty(nodeGuid))
        {
            callback?.Invoke(null, "Invalid rewind progress payload.");
            yield break;
        }

        var body = new RewindProgressRequest
        {
            episodeId = episodeId,
            nodeGuid = nodeGuid,
            nodeId = nodeGuid
        };

        yield return PostRuntime(ApiRoutes.PlayerProgressRewind, body, callback);
    }

    public IEnumerator RegisterPushToken(string token, string provider = null, Action<string, string> callback = null)
    {
        token = SanitizeCredential(token, 4096);
        if (string.IsNullOrEmpty(token))
        {
            callback?.Invoke(null, "Invalid push token.");
            yield break;
        }

        var body = new PushTokenRequest
        {
            token = token,
            provider = SaveDataSanitizer.SanitizeIdentifier(provider),
            platform = GetPlatform()
        };

        yield return PostRuntime(ApiRoutes.PlayerPushToken, body, callback);
    }

    public IEnumerator FetchSlots(Action<string, string> callback)
    {
        yield return GetRuntime(ApiRoutes.PlayerSlots, callback);
    }

    public IEnumerator SwitchSlot(int slotId, Action<bool, string> callback = null)
    {
        if (slotId <= 0 || slotId > 99)
        {
            callback?.Invoke(false, "Invalid slotId.");
            yield break;
        }

        var body = new SlotSwitchRequest
        {
            slotId = slotId,
            slot = slotId
        };

        yield return PostRuntimeMutation(ApiRoutes.PlayerSlotSwitch, body, callback, applyBalance: false);
    }

    public IEnumerator ForkSlot(Action<bool, string> callback = null)
    {
        yield return PostRuntimeMutation(ApiRoutes.PlayerSlotFork, new EmptyRequest(), callback, applyBalance: false);
    }

    public IEnumerator ResetStoryProgress(string storyId, Action<bool, string> callback = null)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (string.IsNullOrEmpty(storyId))
        {
            callback?.Invoke(false, "Invalid storyId.");
            yield break;
        }

        yield return PostRuntimeMutation(
            ApiRoutes.PlayerStoryReset,
            new StoryIdRequest { storyId = storyId },
            callback,
            applyBalance: false);
    }

    public IEnumerator MarkScenesViewed(string episodeId, List<string> nodeIds, Action<bool, string> callback = null)
    {
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        List<string> safeNodeIds = SanitizeNodeIdList(nodeIds);
        if (string.IsNullOrEmpty(episodeId) || safeNodeIds.Count == 0)
        {
            callback?.Invoke(false, "Invalid viewed scene payload.");
            yield break;
        }

        var body = new ScenesViewedRequest
        {
            episodeId = episodeId,
            nodeIds = safeNodeIds,
            nodeGuids = new List<string>(safeNodeIds)
        };

        yield return PostRuntimeMutation(ApiRoutes.PlayerScenesViewed, body, callback, applyBalance: false);
    }

    public IEnumerator FetchViewedScenes(string episodeId, Action<string, string> callback)
    {
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        if (string.IsNullOrEmpty(episodeId))
        {
            callback?.Invoke(null, "Invalid episodeId.");
            yield break;
        }

        yield return GetRuntime(ApiRoutes.PlayerScenesViewedForEpisode(episodeId), callback);
    }

    public IEnumerator FetchGallery(Action<string, string> callback)
    {
        yield return GetRuntime(ApiRoutes.PlayerGallery, callback);
    }

    public IEnumerator UnlockGalleryScene(string sceneId, Action<bool, string> callback = null)
    {
        sceneId = SaveDataSanitizer.SanitizeIdentifier(sceneId);
        if (string.IsNullOrEmpty(sceneId))
        {
            callback?.Invoke(false, "Invalid sceneId.");
            yield break;
        }

        yield return PostRuntimeMutation(
            ApiRoutes.PlayerGalleryUnlock,
            new GalleryUnlockRequest { sceneId = sceneId },
            callback,
            applyBalance: false);
    }

    public IEnumerator FetchTarotStatus(Action<string, string> callback)
    {
        yield return GetRuntime(ApiRoutes.PlayerTarotStatus, callback);
    }

    public IEnumerator DrawTarot(Action<bool, string> callback = null)
    {
        yield return PostRuntimeMutation(ApiRoutes.PlayerTarotDraw, new EmptyRequest(), callback, applyBalance: true);
    }

    public IEnumerator FetchCatGreeting(int hour, Action<string, string> callback)
    {
        hour = Mathf.Clamp(hour, 0, 23);
        yield return GetRuntime(ApiRoutes.PlayerCatGreeting(hour), callback);
    }

    public IEnumerator FetchCatName(Action<string, string> callback)
    {
        yield return GetRuntime(ApiRoutes.PlayerCatName, callback);
    }

    public IEnumerator SetCatName(string name, Action<bool, string> callback = null)
    {
        name = SaveDataSanitizer.SanitizePlayerName(name);
        if (string.IsNullOrEmpty(name))
        {
            callback?.Invoke(false, "Invalid cat name.");
            yield break;
        }

        yield return PostRuntimeMutation(ApiRoutes.PlayerCatName, new CatNameRequest { name = name }, callback, applyBalance: false);
    }

    public IEnumerator FetchDiceStatus(Action<string, string> callback)
    {
        yield return GetRuntime(ApiRoutes.PlayerDiceStatus, callback);
    }

    public IEnumerator RollDice(Action<bool, string> callback = null)
    {
        yield return PostRuntimeMutation(ApiRoutes.PlayerDiceRoll, new EmptyRequest(), callback, applyBalance: true);
    }

    public IEnumerator CheckFavorite(string storyId, Action<string, string> callback)
    {
        string safeStoryId = SanitizeIdForPath(storyId);
        if (string.IsNullOrEmpty(safeStoryId))
        {
            callback?.Invoke(null, "Invalid storyId.");
            yield break;
        }

        yield return GetRuntime(ApiRoutes.PlayerFavoriteCheck(safeStoryId), callback);
    }

    public IEnumerator FetchFavorites(Action<string, string> callback)
    {
        yield return GetRuntime(ApiRoutes.PlayerFavorites, callback);
    }

    public IEnumerator AddFavorite(string storyId, Action<bool, string> callback = null)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (string.IsNullOrEmpty(storyId))
        {
            callback?.Invoke(false, "Invalid storyId.");
            yield break;
        }

        yield return PostRuntimeMutation(
            ApiRoutes.PlayerFavorites,
            new StoryIdRequest { storyId = storyId },
            callback,
            applyBalance: false);
    }

    public IEnumerator RemoveFavorite(string storyId, Action<bool, string> callback = null)
    {
        string safeStoryId = SanitizeIdForPath(storyId);
        if (string.IsNullOrEmpty(safeStoryId))
        {
            callback?.Invoke(false, "Invalid storyId.");
            yield break;
        }

        yield return DeleteRuntime(ApiRoutes.PlayerFavoriteForStory(safeStoryId), (json, err) =>
        {
            callback?.Invoke(err == null, err == null ? json : err);
        });
    }

    public IEnumerator FetchRelationships(Action<string, string> callback)
    {
        yield return GetRuntime(ApiRoutes.PlayerRelationships, callback);
    }

    public IEnumerator UnlockRelationship(string characterId, string storyId = null, Action<bool, string> callback = null)
    {
        characterId = SaveDataSanitizer.SanitizeIdentifier(characterId);
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (string.IsNullOrEmpty(characterId))
        {
            callback?.Invoke(false, "Invalid characterId.");
            yield break;
        }

        var body = new RelationshipUnlockRequest
        {
            characterId = characterId,
            storyId = storyId
        };

        yield return PostRuntimeMutation(ApiRoutes.PlayerRelationshipUnlock, body, callback, applyBalance: false);
    }

    public IEnumerator UpdateRelationship(string characterId, int delta, string storyId = null, Action<bool, string> callback = null)
    {
        characterId = SaveDataSanitizer.SanitizeIdentifier(characterId);
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (string.IsNullOrEmpty(characterId))
        {
            callback?.Invoke(false, "Invalid characterId.");
            yield break;
        }

        var body = new RelationshipUpdateRequest
        {
            characterId = characterId,
            storyId = storyId,
            delta = SaveDataSanitizer.ClampStatValue(delta)
        };

        yield return PostRuntimeMutation(ApiRoutes.PlayerRelationshipUpdate, body, callback, applyBalance: false);
    }

    public IEnumerator ApplyPromoCode(string code, Action<bool, string> callback = null)
    {
        code = SanitizeCredential(code, 64);
        if (string.IsNullOrEmpty(code))
        {
            callback?.Invoke(false, "Invalid promo code.");
            yield break;
        }

        yield return PostRuntimeMutation(ApiRoutes.PlayerPromoApply, new PromoCodeRequest { code = code }, callback, applyBalance: true);
    }

    public IEnumerator FetchShopPrices(Action<string, string> callback)
    {
        yield return GetRuntime(ApiRoutes.ShopPrices, callback);
    }

    public IEnumerator FetchShopItems(Action<string, string> callback)
    {
        yield return GetRuntime(ApiRoutes.ShopItems, callback);
    }

    public IEnumerator CreateShopOrder(string productId, int quantity, Action<string, string> callback)
    {
        productId = SaveDataSanitizer.SanitizeIdentifier(productId);
        if (string.IsNullOrEmpty(productId))
        {
            callback?.Invoke(null, "Invalid productId.");
            yield break;
        }

        var body = new ShopOrderRequest
        {
            productId = productId,
            quantity = Mathf.Clamp(quantity <= 0 ? 1 : quantity, 1, 99),
            platform = GetPlatform()
        };

        yield return PostRuntime(ApiRoutes.ShopOrders, body, callback);
    }

    public IEnumerator ConfirmPurchase(
        string store,
        string productId,
        string transactionId,
        string receipt,
        string signature = null,
        Action<bool, string> callback = null)
    {
        string safeReceipt = SanitizeReceipt(receipt);
        PurchaseReceiptParts receiptParts = ExtractPurchaseReceiptParts(safeReceipt);
        string safeStore = SaveDataSanitizer.SanitizeIdentifier(FirstNonEmptyRawString(store, receiptParts.store));
        string provider = NormalizeIapProvider(safeStore);

        var body = new PurchaseConfirmRequest
        {
            store = safeStore,
            provider = provider,
            unityStore = safeStore,
            platform = GetPlatform(),
            productId = SaveDataSanitizer.SanitizeIdentifier(FirstNonEmptyRawString(productId, receiptParts.productId)),
            storeProductId = SaveDataSanitizer.SanitizeIdentifier(receiptParts.productId),
            transactionId = SanitizeCredential(FirstNonEmptyRawString(transactionId, receiptParts.transactionId, receiptParts.orderId), 256),
            orderId = SanitizeCredential(receiptParts.orderId, 256),
            packageName = SanitizeCredential(receiptParts.packageName, 256),
            purchaseToken = SanitizeCredential(receiptParts.purchaseToken, 4096),
            receipt = safeReceipt,
            payload = SanitizeReceipt(receiptParts.payload),
            originalJson = SanitizeReceipt(receiptParts.originalJson),
            signature = SanitizeCredential(FirstNonEmptyRawString(signature, receiptParts.signature), 4096)
        };

        if (string.IsNullOrEmpty(body.store) ||
            string.IsNullOrEmpty(body.productId) ||
            string.IsNullOrEmpty(body.receipt))
        {
            callback?.Invoke(false, "Invalid purchase confirmation payload.");
            yield break;
        }

        yield return PostRuntimeMutation(ApiRoutes.PurchasesConfirm, body, callback, applyBalance: true);
    }

    public IEnumerator RestorePurchases(string store, string restoreToken = null, Action<bool, string> callback = null)
    {
        string safeStore = SaveDataSanitizer.SanitizeIdentifier(store);
        var body = new PurchaseRestoreRequest
        {
            store = safeStore,
            provider = NormalizeIapProvider(safeStore),
            unityStore = safeStore,
            platform = GetPlatform(),
            restoreToken = SanitizeCredential(restoreToken, 4096)
        };

        if (string.IsNullOrEmpty(body.store))
        {
            callback?.Invoke(false, "Invalid purchase restore payload.");
            yield break;
        }

        yield return PostRuntimeMutation(ApiRoutes.PurchasesRestore, body, callback, applyBalance: true);
    }

    public IEnumerator FetchPurchaseHistory(Action<string, string> callback)
    {
        yield return GetRuntime(ApiRoutes.PurchasesHistory, callback);
    }

    public IEnumerator FetchPurchaseProducts(Action<string, string> callback)
    {
        yield return GetRuntime(ApiRoutes.PurchasesProducts, callback);
    }

    public IEnumerator CompleteChapter(string episodeId, Action<bool, string> callback = null)
    {
        yield return PostEpisodeIdMutation(ApiRoutes.PlayerChapterComplete, episodeId, callback);
    }

    public IEnumerator CompleteStory(string storyId, Action<bool, string> callback = null)
    {
        storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (string.IsNullOrEmpty(storyId))
        {
            callback?.Invoke(false, "Invalid storyId.");
            yield break;
        }

        yield return PostRuntimeMutation(
            ApiRoutes.PlayerStoryComplete,
            new StoryIdRequest { storyId = storyId },
            callback,
            applyBalance: true);
    }

    public IEnumerator FetchReadingStats(Action<string, string> callback)
    {
        yield return GetRuntime(ApiRoutes.PlayerReadingStats, callback);
    }

    private IEnumerator GetRuntime(string path, Action<string, string> callback)
    {
        if (!IsAuthenticated)
        {
            callback?.Invoke(null, "Not authenticated.");
            yield break;
        }

        if (!IsAllowedRuntimeApiPath(NormalizeApiPath(path), "GET"))
        {
            callback?.Invoke(null, "Blocked runtime API path.");
            yield break;
        }

        yield return GetInternal(path, callback, allowRetry: true);
    }

    private IEnumerator PostRuntime(string path, object body, Action<string, string> callback)
    {
        if (!IsAuthenticated)
        {
            callback?.Invoke(null, "Not authenticated.");
            yield break;
        }

        if (!IsAllowedRuntimeApiPath(NormalizeApiPath(path), "POST"))
        {
            callback?.Invoke(null, "Blocked runtime API path.");
            yield break;
        }

        yield return Post(path, body ?? new EmptyRequest(), _authToken, callback, allowRetry: true);
    }

    private IEnumerator DeleteRuntime(string path, Action<string, string> callback)
    {
        if (!IsAuthenticated)
        {
            callback?.Invoke(null, "Not authenticated.");
            yield break;
        }

        if (!IsAllowedRuntimeApiPath(NormalizeApiPath(path), "DELETE"))
        {
            callback?.Invoke(null, "Blocked runtime API path.");
            yield break;
        }

        yield return SendAuthorizedRequest(
            () => _httpClient.CreateDeleteRequest(path, _authToken),
            result => callback?.Invoke(result.IsSuccess ? result.Text : null, result.IsSuccess ? null : result.Error),
            allowRetry: true);
    }

    private IEnumerator PostRuntimeMutation(
        string path,
        object body,
        Action<bool, string> callback,
        bool applyBalance)
    {
        yield return PostRuntime(path, body, (json, err) =>
        {
            if (err != null)
            {
                callback?.Invoke(false, err);
                return;
            }

            if (ResponseHasApiError(json))
            {
                callback?.Invoke(false, NetworkJson.GetString(json, "error"));
                return;
            }

            if (applyBalance)
                ApplyBalancePatchFromJson(json);

            callback?.Invoke(true, json);
        });
    }

    private IEnumerator PostEpisodeIdMutation(string path, string episodeId, Action<bool, string> callback)
    {
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        if (string.IsNullOrEmpty(episodeId))
        {
            callback?.Invoke(false, "Invalid episodeId.");
            yield break;
        }

        yield return PostRuntimeMutation(
            path,
            new EpisodeIdRequest { episodeId = episodeId },
            callback,
            applyBalance: true);
    }

    private void ApplyBalancePatchFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || !NetworkJson.LooksLikeJsonObject(json))
            return;

        string nestedBalance = FirstNonEmptyRaw(
            NetworkJson.GetRawValue(json, "balances"),
            NetworkJson.GetRawValue(json, "balance"));
        if (!string.IsNullOrWhiteSpace(nestedBalance) && NetworkJson.LooksLikeJsonObject(nestedBalance))
        {
            var balance = NetworkJson.FromJson<BalanceResponse>(nestedBalance);
            if (balance != null)
                ApplyBalance(balance);
        }

        bool touched = false;
        if (NetworkJson.GetRawValue(json, "hearts") != null)
        {
            _lastBalance.hearts = SaveDataSanitizer.ClampCurrencyValue(NetworkJson.GetInt(json, "hearts", _lastBalance.hearts));
            PlayerData.SetHeartsValue(_lastBalance.hearts);
            touched = true;
        }

        if (NetworkJson.GetRawValue(json, "candles") != null)
        {
            _lastBalance.candles = SaveDataSanitizer.ClampCurrencyValue(NetworkJson.GetInt(json, "candles", _lastBalance.candles));
            PlayerData.SetCandlesValue(_lastBalance.candles);
            touched = true;
        }

        if (NetworkJson.GetRawValue(json, "candlesCap") != null)
            _lastBalance.candlesCap = SaveDataSanitizer.ClampCurrencyValue(NetworkJson.GetInt(json, "candlesCap", _lastBalance.candlesCap));

        if (NetworkJson.GetRawValue(json, "dailyStreakDay") != null)
            _lastBalance.dailyStreakDay = Mathf.Max(0, NetworkJson.GetInt(json, "dailyStreakDay", _lastBalance.dailyStreakDay));

        ApplyDailyStreakPatchFromJson(json);

        string nextCandleAt = NetworkJson.GetString(json, "nextCandleAt");
        if (!string.IsNullOrEmpty(nextCandleAt))
            _lastBalance.nextCandleAt = SaveDataSanitizer.SanitizeSavedAtIso(nextCandleAt);

        if (NetworkJson.GetRawValue(json, "isSubscriber") != null)
            _lastBalance.isSubscriber = NetworkJson.GetBool(json, "isSubscriber", _lastBalance.isSubscriber);

        if (NetworkJson.GetRawValue(json, "adMultiplier") != null)
            _lastBalance.adMultiplier = Mathf.Clamp(NetworkJson.GetInt(json, "adMultiplier", _lastBalance.adMultiplier), 0, 100);

        string catName = NetworkJson.GetString(json, "catName");
        if (!string.IsNullOrEmpty(catName))
            _lastBalance.catName = SaveDataSanitizer.SanitizePlayerName(catName);

        if (touched)
            _lastBalance.updatedAtIso = DateTime.UtcNow.ToString("o");
    }

    private void ApplyDailyStreakPatchFromJson(string json)
    {
        string rawDailyStreak = NetworkJson.GetRawValue(json, "dailyStreak");
        if (string.IsNullOrWhiteSpace(rawDailyStreak) || !NetworkJson.LooksLikeJsonObject(rawDailyStreak))
            return;

        DailyStreakResponse dailyStreak = NetworkJson.FromJson<DailyStreakResponse>(rawDailyStreak);
        if (dailyStreak == null)
            return;

        _lastBalance.dailyStreakDay = Mathf.Max(0, dailyStreak.day);
        _lastBalance.dailyRewardAvailabilityKnown = true;
        _lastBalance.dailyRewardCanClaim = dailyStreak.canClaim;
        _lastBalance.dailyRewardAmount = SaveDataSanitizer.ClampCurrencyValue(dailyStreak.reward);
        _lastBalance.dailyLastClaimAt = SaveDataSanitizer.SanitizeSavedAtIso(dailyStreak.lastClaimAt);
    }

    private void MarkCatalogEpisodeUnlocked(string episodeId, string json)
    {
        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        if (string.IsNullOrEmpty(episodeId))
            return;

        if (_catalogEpisodes.TryGetValue(episodeId, out var episode) && episode != null)
        {
            episode.isUnlocked = true;
            if (NetworkJson.GetRawValue(json, "candleCost") != null)
                episode.candleCost = ClampCatalogCandleCost(NetworkJson.GetInt(json, "candleCost", episode.candleCost));
        }

        if (!_lastUnlockedEpisodes.Contains(episodeId) && _lastUnlockedEpisodes.Count < MaxCatalogEpisodes)
            _lastUnlockedEpisodes.Add(episodeId);
    }

    private static bool ResponseHasApiError(string json)
    {
        return !string.IsNullOrEmpty(NetworkJson.GetString(json, "error"));
    }

    private static string SanitizeIdForPath(string value)
    {
        value = SaveDataSanitizer.SanitizeIdentifier(value);
        return string.IsNullOrEmpty(value) ? "" : UnityWebRequest.EscapeURL(value);
    }

    private static List<string> SanitizeNodeIdList(List<string> nodeIds)
    {
        var result = new List<string>();
        if (nodeIds == null)
            return result;

        for (int i = 0; i < nodeIds.Count && result.Count < MaxNodeIdsPerBatch; i++)
        {
            string safe = SaveDataSanitizer.SanitizeIdentifier(nodeIds[i]);
            if (!string.IsNullOrEmpty(safe) && !result.Contains(safe))
                result.Add(safe);
        }

        return result;
    }

    private static string SanitizeReceipt(string receipt)
    {
        receipt = SanitizeCredential(receipt, MaxPurchaseReceiptChars);
        return receipt ?? "";
    }

    private static PurchaseReceiptParts ExtractPurchaseReceiptParts(string receipt)
    {
        var result = new PurchaseReceiptParts();
        if (string.IsNullOrWhiteSpace(receipt) || !NetworkJson.LooksLikeJsonObject(receipt))
            return result;

        result.store = NetworkJson.GetFirstString(receipt, "Store", "store");
        result.transactionId = NetworkJson.GetFirstString(receipt, "TransactionID", "transactionId", "transactionID");
        result.productId = NetworkJson.GetFirstString(receipt, "productId", "ProductId", "sku");
        result.purchaseToken = NetworkJson.GetFirstString(receipt, "purchaseToken", "token");
        result.orderId = NetworkJson.GetFirstString(receipt, "orderId", "OrderId");
        result.packageName = NetworkJson.GetFirstString(receipt, "packageName", "package");

        string payload = NetworkJson.GetFirstString(receipt, "Payload", "payload", "receipt");
        result.payload = payload ?? "";

        if (NetworkJson.LooksLikeJsonObject(payload))
        {
            result.signature = NetworkJson.GetFirstString(payload, "signature", "Signature");
            result.originalJson = NetworkJson.GetFirstString(payload, "json", "Json", "originalJson", "signedData");

            result.purchaseToken = FirstNonEmptyRawString(
                result.purchaseToken,
                NetworkJson.GetFirstString(payload, "purchaseToken", "token"));
            result.productId = FirstNonEmptyRawString(
                result.productId,
                NetworkJson.GetFirstString(payload, "productId", "ProductId", "sku"));
            result.orderId = FirstNonEmptyRawString(
                result.orderId,
                NetworkJson.GetFirstString(payload, "orderId", "OrderId"));
            result.packageName = FirstNonEmptyRawString(
                result.packageName,
                NetworkJson.GetFirstString(payload, "packageName", "package"));
        }

        if (NetworkJson.LooksLikeJsonObject(result.originalJson))
        {
            result.purchaseToken = FirstNonEmptyRawString(
                result.purchaseToken,
                NetworkJson.GetFirstString(result.originalJson, "purchaseToken", "token"));
            result.productId = FirstNonEmptyRawString(
                result.productId,
                NetworkJson.GetFirstString(result.originalJson, "productId", "ProductId", "sku"));
            result.orderId = FirstNonEmptyRawString(
                result.orderId,
                NetworkJson.GetFirstString(result.originalJson, "orderId", "OrderId"));
            result.packageName = FirstNonEmptyRawString(
                result.packageName,
                NetworkJson.GetFirstString(result.originalJson, "packageName", "package"));
        }

        return result;
    }

    private static string NormalizeIapProvider(string store)
    {
        string safeStore = SaveDataSanitizer.SanitizeIdentifier(store);
        if (string.IsNullOrEmpty(safeStore))
            return "";

        string lower = safeStore.ToLowerInvariant();
        if (lower.Contains("google") || lower.Contains("android"))
            return "google";

        if (lower.Contains("apple") || lower.Contains("appstore") || lower.Contains("ios") || lower.Contains("mac"))
            return "apple";

        return safeStore;
    }

    private sealed class PurchaseReceiptParts
    {
        public string store;
        public string transactionId;
        public string productId;
        public string payload;
        public string originalJson;
        public string signature;
        public string purchaseToken;
        public string orderId;
        public string packageName;
    }

    private static bool IsAllowedRuntimeApiPath(string normalized, string method)
    {
        return ApiContract.IsRuntimeAllowed(method, normalized);
    }
}

[Serializable]
internal sealed class RefreshAuthRequest
{
    public string refreshToken;
}

[Serializable]
internal sealed class RestoreAuthRequest
{
    public string deviceId;
    public string refreshToken;
}

[Serializable]
internal sealed class RestoreCodeAuthRequest
{
    public string restoreCode;
    public string deviceId;
}

[Serializable]
internal sealed class SocialAuthRequest
{
    public string provider;
    public string idToken;
    public string deviceId;
}

[Serializable]
internal sealed class CandleSpendRequest
{
    public int amount;
}

[Serializable]
internal sealed class HeartsSpendRequest
{
    public int amount;
}

[Serializable]
internal sealed class WardrobePurchaseRequest
{
    public string itemId;
}

[Serializable]
internal sealed class WardrobePurchaseResponse
{
    public string itemId;
    public int pricePaid;
    public int heartsRemaining;
}

[Serializable]
internal sealed class WardrobeOwnershipResponse
{
    public string playerId;
    public List<WardrobeOwnedItemResponse> owned;
}

[Serializable]
internal sealed class WardrobeOwnedItemResponse
{
    public string itemId;
    public int pricePaid;
    public string purchasedAt;
}

[Serializable]
internal sealed class EpisodeIdRequest
{
    public string episodeId;
}

[Serializable]
internal sealed class EpisodeUnlockRequest
{
    public string episodeId;
    public bool confirmed;
}

[Serializable]
internal sealed class EpisodeCompleteRequest
{
    public string episodeId;
    public string nextEpisodeId;
}

[Serializable]
internal sealed class SeasonIdRequest
{
    public string seasonId;
}

[Serializable]
internal sealed class StoryIdRequest
{
    public string storyId;
}

[Serializable]
internal sealed class UndoChoiceRequest
{
    public int amount;
    public string nodeGuid;
    public bool isOutfit;
}

[Serializable]
internal sealed class RewindProgressRequest
{
    public string episodeId;
    public string nodeGuid;
    public string nodeId;
}

[Serializable]
internal sealed class PushTokenRequest
{
    public string token;
    public string provider;
    public string platform;
}

[Serializable]
internal sealed class SlotSwitchRequest
{
    public int slotId;
    public int slot;
}

[Serializable]
internal sealed class ScenesViewedRequest
{
    public string episodeId;
    public List<string> nodeIds = new List<string>();
    public List<string> nodeGuids = new List<string>();
}

[Serializable]
internal sealed class GalleryUnlockRequest
{
    public string sceneId;
}

[Serializable]
internal sealed class CatNameRequest
{
    public string name;
}

[Serializable]
internal sealed class RelationshipUnlockRequest
{
    public string characterId;
    public string storyId;
}

[Serializable]
internal sealed class RelationshipUpdateRequest
{
    public string characterId;
    public string storyId;
    public int delta;
}

[Serializable]
internal sealed class PromoCodeRequest
{
    public string code;
}

[Serializable]
internal sealed class ShopOrderRequest
{
    public string productId;
    public int quantity;
    public string platform;
}

[Serializable]
internal sealed class PurchaseConfirmRequest
{
    public string store;
    public string provider;
    public string unityStore;
    public string platform;
    public string productId;
    public string storeProductId;
    public string transactionId;
    public string orderId;
    public string packageName;
    public string purchaseToken;
    public string receipt;
    public string payload;
    public string originalJson;
    public string signature;
}

[Serializable]
internal sealed class PurchaseRestoreRequest
{
    public string store;
    public string provider;
    public string unityStore;
    public string platform;
    public string restoreToken;
}
