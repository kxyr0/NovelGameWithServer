using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using XNode;

public partial class StoryManager
{
    public void StartStory()
    {
        if (!EnsureStorySelected() || !ValidateStoryData())
            return;

        AppLogger.Info(
            AppLogCategory.App,
            nameof(StoryManager),
            nameof(StartStory),
            "Starting story from first chapter.",
            LogMetadata.Of("storyId", CurrentStoryId, "chapterCount", StoryChapterCount));

        if (endStoryPanel != null)
            endStoryPanel.SetActive(false);

        currentSeason = 0;
        currentChapter = 0;
        ResetEndPanelState();
        ClearChapterBoundaryResume();
        StartNewSeasonRewardRunForCurrentChapter(nameof(StartStory));

        StartCurrentChapter();
    }

    public bool StartStoryFromChapterId(string chapterId)
    {
        if (string.IsNullOrWhiteSpace(chapterId))
        {
            AppLogger.Error(
                AppLogCategory.App,
                nameof(StoryManager),
                nameof(StartStoryFromChapterId),
                "Cannot start chapter because chapterId is empty.",
                metadata: LogMetadata.Of("storyId", CurrentStoryId),
                recoverable: true);
            Debug.LogError("[StoryManager] Cannot start chapter: chapterId is empty.", this);
            return false;
        }

        if (!EnsureStorySelected() || !ValidateStoryData())
            return false;

        var chapters = GetStoryChapters();
        for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
        {
            ChapterData chapter = chapters[chapterIndex];
            if (chapter == null)
                continue;

            if (string.Equals(chapter.chapterId, chapterId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(chapter.name, chapterId, StringComparison.OrdinalIgnoreCase))
            {
                return StartStoryFromChapterIndex(chapterIndex);
            }
        }

        AppLogger.Error(
            AppLogCategory.App,
            nameof(StoryManager),
            nameof(StartStoryFromChapterId),
            "Cannot start chapter because it was not found in the selected story.",
            metadata: LogMetadata.Of("storyId", CurrentStoryId, "chapterId", chapterId),
            recoverable: true);
        Debug.LogError($"[StoryManager] Cannot start chapter '{chapterId}': chapter was not found in selected story.", this);
        return false;
    }

#if UNITY_EDITOR
    public bool StartStoryFromChapterIdForEditorTest(string chapterId)
    {
        StopAllCoroutines();
        isSkippingToNextChoice = false;
        isSkippingToNextCutscene = false;
        skipToNextChoiceRoutine = null;
        skipToNextCutsceneRoutine = null;
        return StartStoryFromChapterId(chapterId);
    }
#endif

    public bool StartStoryFromChapterIndex(int chapterIndex)
    {
        if (!EnsureStorySelected() || !ValidateStoryData())
            return false;

        var chapters = GetStoryChapters();
        if (chapterIndex < 0 || chapterIndex >= chapters.Count)
        {
            AppLogger.Error(
                AppLogCategory.App,
                nameof(StoryManager),
                nameof(StartStoryFromChapterIndex),
                "Cannot start chapter because chapter index is out of range.",
                metadata: LogMetadata.Of("storyId", CurrentStoryId, "chapterIndex", chapterIndex, "chapterCount", chapters.Count),
                recoverable: true);
            Debug.LogError($"[StoryManager] Cannot start chapter index {chapterIndex}: valid range is 0..{chapters.Count - 1}.", this);
            return false;
        }

        if (endStoryPanel != null)
            endStoryPanel.SetActive(false);

        currentSeason = 0;
        currentChapter = chapterIndex;
        storyGraph = null;
        activeDialogueNode = null;
        currentLineIndex = 0;
        ResetDialogueLinePages();
        ClearCutsceneRuntimeState();
        ResetEndPanelState();
        ClearChapterBoundaryResume();
        dialogueUI?.ResetStoryUi();
        StartNewSeasonRewardRunForCurrentChapter(nameof(StartStoryFromChapterIndex));

        StartCurrentChapter();
        return true;
    }

    void StartCurrentChapter()
    {
        StartCoroutine(StartCurrentChapterRoutine());
    }

    IEnumerator StartCurrentChapterRoutine()
    {
        long startedAt = AppDiagnostics.StartTimer();
        if (!EnsureStorySelected())
            yield break;

        if (endStoryPanel != null)
            endStoryPanel.SetActive(false);

        var chapter = GetCurrentChapterOrNull();
        if (chapter == null)
        {
            LogChapterStartFailed("Current chapter is missing.", startedAt, null);
            Debug.LogError("[StoryManager] Current chapter is missing");
            yield break;
        }

        if (!HasPlayableLocalChapter())
            yield return SyncRemoteGraphCacheIfNeeded(ResolveChapterEpisodeId(chapter));

        storyGraph = ResolveGraphForChapter(chapter) ?? chapter.graph;
        if (storyGraph == null)
        {
            LogChapterStartFailed("Chapter graph is missing.", startedAt, chapter);
            Debug.LogError("Chapter graph missing");
            yield break;
        }

        if (ChapterLoadingScreen.Instance != null)
        {
            ChapterLoadingScreen.Instance.Show(GetChapterDisplayName(chapter), () => DoStartCurrentChapter(chapter));
        }
        else
        {
            DoStartCurrentChapter(chapter);
        }
    }

    void DoStartCurrentChapter(ChapterData chapter)
    {
        long startedAt = AppDiagnostics.StartTimer();
        // Инициализируем GameState под текущую историю (изолированный гардероб и статы)
        if (!EnsureGameState("starting a chapter"))
            return;

        if (!string.IsNullOrEmpty(CurrentStoryId))
            GameState.Instance.InitForStory(CurrentStoryId);

        ResetCurrentEpisodeSummary();
        ResetCameraPosition();
        backgroundView?.ClearBackground();
        ShowChapterTitle(chapter);

        StartNode startNode = null;
        var activeGraph = storyGraph != null ? storyGraph : chapter.graph;
        if (activeGraph == null)
        {
            LogChapterStartFailed("Active graph is missing.", startedAt, chapter);
            Debug.LogError("Active graph missing");
            return;
        }

        if (activeGraph.nodes == null)
        {
            LogChapterStartFailed("Active graph has no node list.", startedAt, chapter);
            Debug.LogError("Active graph has no nodes");
            return;
        }

        ApplyCurrentHeroCustomizationToStory(activeGraph);

        foreach (var node in activeGraph.nodes)
        {
            if (node is StartNode)
            {
                startNode = node as StartNode;
                break;
            }
        }

        if (startNode == null)
        {
            LogChapterStartFailed("Start node was not found.", startedAt, chapter);
            Debug.LogError("StartNode not found");
            return;
        }

        ProcessNode(startNode);
        long durationMs = AppDiagnostics.ElapsedMilliseconds(startedAt);
        var metadata = BuildChapterStartMetadata(chapter, activeGraph);
        AppLogger.Info(
            AppLogCategory.App,
            nameof(StoryManager),
            nameof(DoStartCurrentChapter),
            "Chapter started.",
            metadata,
            durationMs);
        AppDiagnostics.LogIfSlow(
            AppLogCategory.App,
            nameof(StoryManager),
            nameof(DoStartCurrentChapter),
            durationMs,
            metadata);
    }

    void LogChapterStartFailed(string message, long startedAt, ChapterData chapter)
    {
        AppLogger.Error(
            AppLogCategory.App,
            nameof(StoryManager),
            nameof(StartCurrentChapterRoutine),
            message,
            metadata: BuildChapterStartMetadata(chapter, storyGraph),
            durationMs: AppDiagnostics.ElapsedMilliseconds(startedAt),
            recoverable: true);
    }

    IDictionary<string, object> BuildChapterStartMetadata(ChapterData chapter, StoryGraph graph)
    {
        return LogMetadata.Of(
            "storyId", CurrentStoryId,
            "seasonIndex", CurrentSeasonIndex,
            "chapterIndex", CurrentChapterIndex,
            "chapterId", chapter != null ? chapter.chapterId : "",
            "episodeId", chapter != null ? ResolveChapterEpisodeId(chapter) : "",
            "graphName", graph != null ? graph.name : "",
            "nodeCount", graph != null && graph.nodes != null ? graph.nodes.Count : 0);
    }

    void ApplyCurrentHeroCustomizationToStory(StoryGraph graph)
    {
        HeroCustomizationState state = ResolveCurrentHeroCustomizationState(graph);
        state.playerName = ResolveRestoredPlayerName(null, graph, state.playerName);
        ClothingItem outfit = ResolveHeroClothing(state.outfitId, ClothingType.Outfit, graph);
        ClothingItem hair = ResolveHeroClothing(state.hairId, ClothingType.Hair, graph);
        ClothingItem accessory = ResolveHeroClothing(state.accessoryId, ClothingType.Accessory, graph);
        Sprite outfitSprite = outfit != null
            ? outfit.sprite
            : !string.IsNullOrWhiteSpace(state.outfitId) &&
              string.Equals(PlayerAppearance.OutfitId, state.outfitId, StringComparison.OrdinalIgnoreCase)
                ? PlayerAppearance.OutfitSprite
                : null;
        Sprite hairSprite = hair != null
            ? hair.sprite
            : !string.IsNullOrWhiteSpace(state.hairId) &&
              string.Equals(PlayerAppearance.HairId, state.hairId, StringComparison.OrdinalIgnoreCase)
                ? PlayerAppearance.HairSprite
                : null;
        Sprite accessorySprite = accessory != null
            ? accessory.sprite
            : !string.IsNullOrWhiteSpace(state.accessoryId) &&
              string.Equals(PlayerAppearance.AccessoryId, state.accessoryId, StringComparison.OrdinalIgnoreCase)
                ? PlayerAppearance.AccessorySprite
                : null;

        PlayerAppearance.ApplyState(
            state,
            outfitSprite,
            hairSprite,
            outfit,
            hair,
            accessorySprite,
            accessory);

        if (GameState.Instance == null)
            return;

        if (!string.IsNullOrEmpty(state.outfitId))
            GameState.Instance.EquipClothing("hero:outfit", state.outfitId);

        if (!string.IsNullOrEmpty(state.hairId))
            GameState.Instance.EquipClothing("hero:hair", state.hairId);

        if (!string.IsNullOrEmpty(state.accessoryId))
            GameState.Instance.EquipClothing("hero:accessory", state.accessoryId);
    }

    void EnsureHeroCustomizationReadyForDisplay()
    {
        bool needsResolution =
            NeedsRenderableEquipment(PlayerAppearance.OutfitId, PlayerAppearance.OutfitSprite, PlayerAppearance.OutfitItem) ||
            NeedsRenderableEquipment(PlayerAppearance.HairId, PlayerAppearance.HairSprite, PlayerAppearance.HairItem) ||
            NeedsRenderableEquipment(PlayerAppearance.AccessoryId, PlayerAppearance.AccessorySprite, PlayerAppearance.AccessoryItem);

        if (GameState.Instance != null)
        {
            Dictionary<string, string> equippedClothes = GameState.Instance.GetEquippedClothesSnapshot();
            string outfitId = FindEquippedHeroClothingId(equippedClothes, "hero:outfit", "outfit");
            string hairId = FindEquippedHeroClothingId(equippedClothes, "hero:hair", "hair");
            string accessoryId = FindEquippedHeroClothingId(equippedClothes, "hero:accessory", "accessory");

            needsResolution |= !string.IsNullOrWhiteSpace(outfitId) &&
                !string.Equals(PlayerAppearance.OutfitId, outfitId, StringComparison.OrdinalIgnoreCase);
            needsResolution |= !string.IsNullOrWhiteSpace(hairId) &&
                !string.Equals(PlayerAppearance.HairId, hairId, StringComparison.OrdinalIgnoreCase);
            needsResolution |= !string.IsNullOrWhiteSpace(accessoryId) &&
                !string.Equals(PlayerAppearance.AccessoryId, accessoryId, StringComparison.OrdinalIgnoreCase);
        }

        if (needsResolution)
            ApplyCurrentHeroCustomizationToStory(storyGraph);
    }

    static bool NeedsRenderableEquipment(string id, Sprite sprite, ClothingItem item)
    {
        return !string.IsNullOrWhiteSpace(id) && (sprite == null || item == null);
    }

    HeroCustomizationState ResolveCurrentHeroCustomizationState(StoryGraph graph)
    {
        HeroCustomizationState state = PlayerAppearance.CaptureState();

        if (TryResolveStoredStoryAppearance(out AppearanceType storedAppearance))
            state.appearance = storedAppearance;
        else if (!string.IsNullOrEmpty(CurrentStoryId))
            state.appearance = AppearanceType.Default;

        if (GameState.Instance == null)
            return state;

        Dictionary<string, string> equippedClothes = GameState.Instance.GetEquippedClothesSnapshot();
        string outfitId = FindEquippedHeroClothingId(equippedClothes, "hero:outfit", "outfit");
        string hairId = FindEquippedHeroClothingId(equippedClothes, "hero:hair", "hair");
        string accessoryId = FindEquippedHeroClothingId(equippedClothes, "hero:accessory", "accessory");

        if (!string.IsNullOrWhiteSpace(outfitId) &&
            !ShouldPreferCurrentHeroClothing(state.outfitId, outfitId, ClothingType.Outfit, graph))
        {
            state.outfitId = outfitId;
        }

        if (!string.IsNullOrWhiteSpace(hairId) &&
            !ShouldPreferCurrentHeroClothing(state.hairId, hairId, ClothingType.Hair, graph))
        {
            state.hairId = hairId;
        }

        if (!string.IsNullOrWhiteSpace(accessoryId) &&
            !ShouldPreferCurrentHeroClothing(state.accessoryId, accessoryId, ClothingType.Accessory, graph))
        {
            state.accessoryId = accessoryId;
        }

        return state.Normalized();
    }

    static string FindEquippedHeroClothingId(Dictionary<string, string> equippedClothes, string preferredKey, string slotSuffix)
    {
        if (equippedClothes == null || equippedClothes.Count == 0)
            return "";

        if (!string.IsNullOrWhiteSpace(preferredKey) &&
            equippedClothes.TryGetValue(preferredKey, out string preferredValue) &&
            !string.IsNullOrWhiteSpace(preferredValue))
        {
            return preferredValue.Trim();
        }

        if (string.IsNullOrWhiteSpace(slotSuffix))
            return "";

        if (equippedClothes.TryGetValue(slotSuffix, out string legacyValue) &&
            !string.IsNullOrWhiteSpace(legacyValue))
            return legacyValue.Trim();

        return "";
    }

    void OnChapterFinished(BaseStoryNode terminalNode = null)
    {
        if (terminalNode != null && GameState.Instance != null)
            GameState.Instance.currentNode = terminalNode;

        TryApplySeasonCompletionRewardForCompletedChapter(currentChapter);
        CaptureCompletedEpisodeSummary();

        var chapters = GetStoryChapters();
        if (storyData == null || chapters.Count == 0)
        {
            Debug.LogWarning("[StoryManager] Cannot advance chapter: story data is missing.");
            MarkEndPanelClosedStory();
            OpenEndPanel();
            SetPurchaseVisible(false);
            return;
        }

        lastCompletedChapter = currentChapter;
        int completedChapterIndex = currentChapter;
        ChapterData completedChapter = GetCurrentChapterOrNull();
        string completedEpisodeId = ResolveChapterEpisodeId(completedChapter);
        string completedStoryId = CurrentStoryId;

        int saveSlot = ResolveProgressSaveSlot();
        SaveData completedSnapshot = SaveManager.Instance != null
            ? SaveManager.Instance.SaveCurrentData(saveSlot, this)
            : null;

        currentChapter++;
        currentSeason = 0;

        if (currentChapter >= chapters.Count)
        {
            currentChapter = completedChapterIndex;
            MarkEndPanelClosedStory();
            SaveChapterBoundaryResume(completedChapterIndex, -1, true);
            Debug.Log("Story finished");
            OpenEndPanel();
            SetPurchaseVisible(false);
            SyncCompletedBoundarySnapshot(completedSnapshot, completedEpisodeId);
            ReportEpisodeCompletion(completedEpisodeId, "", completedStoryId, true);
            // ReturnToMainMenu();
            return;
        }

        var nextChapter = GetCurrentChapterOrNull();
        if (nextChapter == null)
        {
            Debug.LogWarning("[StoryManager] Cannot advance chapter: next chapter is missing.");
            MarkEndPanelClosedStory();
            SaveChapterBoundaryResume(completedChapterIndex, -1, true);
            OpenEndPanel();
            SetPurchaseVisible(false);
            SyncCompletedBoundarySnapshot(completedSnapshot, completedEpisodeId);
            ReportEpisodeCompletion(completedEpisodeId, "", completedStoryId, true);
            return;
        }

        MarkEndPanelNextChapter(currentChapter);
        ReportEpisodeCompletion(completedEpisodeId, ResolveChapterEpisodeId(nextChapter), completedStoryId, false);
        bool nextChapterLocked = IsChapterPremium(nextChapter) && !IsChapterUnlocked(currentSeason, currentChapter);
        SetPurchaseVisible(nextChapterLocked);
        SaveChapterBoundaryResume(completedChapterIndex, currentChapter, false);
        SyncCompletedBoundarySnapshot(completedSnapshot, completedEpisodeId);
        OpenEndPanel();

        if (nextChapterLocked)
            WirePurchaseButton(nextChapter);
    }

    void ReportEpisodeCompletion(string episodeId, string nextEpisodeId, string storyId, bool storyFinished)
    {
        if (string.IsNullOrEmpty(episodeId) || NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
            return;

        StartCoroutine(ReportEpisodeCompletionRoutine(episodeId, nextEpisodeId, storyId, storyFinished));
    }

    IEnumerator ReportEpisodeCompletionRoutine(string episodeId, string nextEpisodeId, string storyId, bool storyFinished)
    {
        yield return NetworkManager.Instance.CompleteEpisode(episodeId, nextEpisodeId);
        yield return NetworkManager.Instance.CompleteChapter(episodeId);

        if (storyFinished && !string.IsNullOrEmpty(storyId))
            yield return NetworkManager.Instance.CompleteStory(storyId);
    }

    private void OpenEndPanel()
    {
        PrepareStoryPresentationForEndPanel();

        if (endStoryPanel != null)
        {
            StoryEndScreenController endScreen = endStoryPanel.GetComponentInChildren<StoryEndScreenController>(true);
            if (endScreen != null)
            {
                Sprite unusedEndScreenBackground;
                TryResolveCurrentStoryUiStyle(out StoryUiStyle endScreenStyle, out unusedEndScreenBackground);
                endScreen.ApplyStoryUiStyle(endScreenStyle, CurrentStoryId, preview: false);
                endStoryPanel.SetActive(true);
                endScreen.ShowRuntime(nameof(OpenEndPanel));
                return;
            }

            endStoryPanel.SetActive(true);
        }

        AppLogger.Warn(
            AppLogCategory.EndScreen,
            nameof(StoryManager),
            nameof(OpenEndPanel),
            "StoryEndScreenController was not found. Legacy end panel text fallback was used.",
            LogMetadata.Of("storyId", CurrentStoryId, "episodeId", LastCompletedEpisodeId),
            recoverable: true);

        if (townText != null)
            townText.text = FormatLastCompletedEpisodeStatLine("\u0413\u043e\u0440\u043e\u0434", "city", "town", "gorod");
        if (storyText != null)
            storyText.text = FormatLastCompletedEpisodeStatLine("\u0421\u043a\u0430\u0437\u043a\u0430", "fairytale", "story", "tale", "skazka");
        if (reputationText != null)
            reputationText.text = FormatLastCompletedEpisodeStatLine("\u0420\u0435\u043f\u0443\u0442\u0430\u0446\u0438\u044f", "respect", "reputation", "rep");
        if (heartsText != null)
            heartsText.text = FormatLastCompletedEpisodeHeartsLine("\u0418\u0441\u043a\u0440\u044b");
    }

    void PrepareStoryPresentationForEndPanel()
    {
        activeDialogueNode = null;
        currentLineIndex = 0;
        ResetDialogueLinePages();
        ClearCutsceneRuntimeState();
        HideImageOverlayIfVisible();
        dialogueUI?.ResetStoryUi();
        cutsceneUserInterface?.HideDialoguePanelForCutsceneIntro();
        phoneDialogueUI?.Hide();
        _chapterTitleOverlay?.HideInstant();
        characterView?.HideAll(0f);
        ResetCameraPosition();
        backgroundView?.StopVideo();
    }

    public void CloseEndPanel()
    {
        if (endStoryPanel != null)
            endStoryPanel.SetActive(false);
    }

    public bool ContinueFromEndPanel()
    {
        if (!EnsureStorySelected() || !ValidateStoryData())
            return false;

        if (!EndPanelHasNextChapter)
        {
            Debug.Log("[StoryManager] End panel has no next chapter to continue.", this);
            return false;
        }

        return StartChapterFromEndPanel(ResolveEndPanelNextChapterIndex(), true);
    }

    public bool RestartCompletedChapterFromEndPanel()
    {
        if (!EnsureStorySelected() || !ValidateStoryData())
            return false;

        if (!CanRestartCompletedChapter)
        {
            Debug.Log("[StoryManager] End panel has no completed chapter to restart.", this);
            return false;
        }

        return StartChapterFromEndPanel(lastCompletedChapter, true);
    }

    bool StartChapterFromEndPanel(int chapterIndex, bool respectPremiumLock)
    {
        ChapterData chapter = GetChapterAtIndexOrNull(chapterIndex);
        if (chapter == null)
            return false;

        currentSeason = 0;
        currentChapter = chapterIndex;
        storyGraph = null;
        activeDialogueNode = null;
        currentLineIndex = 0;
        ResetDialogueLinePages();
        ClearCutsceneRuntimeState();
        ClearChapterBoundaryResume();
        dialogueUI?.ResetStoryUi();

        if (respectPremiumLock && IsChapterPremium(chapter) && !IsChapterUnlocked(currentSeason, currentChapter))
        {
            ShowPurchasePopup(chapter);
            return false;
        }

        SetPurchaseVisible(false);
        CloseEndPanel();
        StartNewSeasonRewardRunForCurrentChapter(nameof(StartChapterFromEndPanel));
        StartCurrentChapter();
        return true;
    }

    void ShowPurchasePopup(ChapterData chapter)
    {
        if (!EnsureDialogueUI("showing a purchase popup"))
        {
            ReturnToMainMenu();
            return;
        }

        dialogueUI.ShowPurchasePopup(
            GetChapterDisplayName(chapter),
            GetChapterUnlockCost(chapter),
            () => PurchaseChapter(chapter),
            () => ReturnToMainMenu()
        );
    }

    void PurchaseChapter(ChapterData chapter)
    {
        if (IsChapterAlreadyUnlocked(chapter))
        {
            SaveChapterUnlock(currentSeason, currentChapter);
            ClearChapterBoundaryResume();
            CloseEndPanel();
            SetPurchaseVisible(false);
            StartNewSeasonRewardRunForCurrentChapter(nameof(PurchaseChapter));
            StartCurrentChapter();
            return;
        }

        int unlockCost = GetChapterUnlockCost(chapter);
        if (!IsValidPremiumCost(unlockCost))
        {
            Debug.LogWarning("[StoryManager] Refused chapter purchase with invalid unlock cost: " + unlockCost);
            return;
        }

        string pendingKey = GetChapterPurchasePendingKey(chapter);
        if (!string.IsNullOrEmpty(pendingKey) && !_pendingChapterPurchases.Add(pendingKey))
            return;

        if (NetworkManager.Instance != null && NetworkManager.IsAuthenticated)
        {
            StartCoroutine(PurchaseChapterFromServer(chapter, pendingKey));
            return;
        }

        PurchaseChapterLocally(chapter, unlockCost, pendingKey);
    }

    IEnumerator PurchaseChapterFromServer(ChapterData chapter, string pendingKey)
    {
        string episodeId = ResolveChapterEpisodeId(chapter);
        if (string.IsNullOrEmpty(episodeId))
        {
            if (!string.IsNullOrEmpty(pendingKey))
                _pendingChapterPurchases.Remove(pendingKey);
            yield break;
        }

        if (IsChapterAlreadyUnlocked(chapter))
        {
            if (!string.IsNullOrEmpty(pendingKey))
                _pendingChapterPurchases.Remove(pendingKey);

            SaveChapterUnlock(currentSeason, currentChapter);
            ClearChapterBoundaryResume();
            CloseEndPanel();
            SetPurchaseVisible(false);
            StartNewSeasonRewardRunForCurrentChapter(nameof(PurchaseChapterFromServer));
            StartCurrentChapter();
            yield break;
        }

        bool unlocked = false;
        yield return NetworkManager.Instance.UnlockEpisode(episodeId, false, (ok, payload) =>
        {
            unlocked = ok;
            if (!ok)
                Debug.LogWarning("[StoryManager] Server chapter unlock failed: " + payload);
        });

        if (!string.IsNullOrEmpty(pendingKey))
            _pendingChapterPurchases.Remove(pendingKey);

        if (!unlocked)
            yield break;

        SaveChapterUnlock(currentSeason, currentChapter);
        ClearChapterBoundaryResume();
        CloseEndPanel();
        SetPurchaseVisible(false);
        StartNewSeasonRewardRunForCurrentChapter(nameof(PurchaseChapterFromServer));
        StartCurrentChapter();
    }

    void PurchaseChapterLocally(ChapterData chapter, int unlockCost, string pendingKey)
    {
        if (IsChapterAlreadyUnlocked(chapter))
        {
            if (!string.IsNullOrEmpty(pendingKey))
                _pendingChapterPurchases.Remove(pendingKey);

            SaveChapterUnlock(currentSeason, currentChapter);
            ClearChapterBoundaryResume();
            CloseEndPanel();
            SetPurchaseVisible(false);
            StartNewSeasonRewardRunForCurrentChapter(nameof(PurchaseChapterLocally));
            StartCurrentChapter();
            return;
        }

        if (!PrototypeFeatureFlags.LocalPremiumSpendEnabled)
        {
            if (!string.IsNullOrEmpty(pendingKey))
                _pendingChapterPurchases.Remove(pendingKey);

            Debug.LogWarning("[StoryManager] Local chapter purchase is disabled. Unlock chapters through API/IAP.");
            return;
        }

        if (!EnsureGameState("purchasing a chapter"))
        {
            if (!string.IsNullOrEmpty(pendingKey))
                _pendingChapterPurchases.Remove(pendingKey);
            return;
        }

        if (GameState.Instance.currency < unlockCost)
        {
            if (!string.IsNullOrEmpty(pendingKey))
                _pendingChapterPurchases.Remove(pendingKey);
            return;
        }

        GameState.Instance.SpendCurrency(unlockCost);
        if (!string.IsNullOrEmpty(pendingKey))
            _pendingChapterPurchases.Remove(pendingKey);

        SaveChapterUnlock(currentSeason, currentChapter);
        ClearChapterBoundaryResume();
        CloseEndPanel();
        SetPurchaseVisible(false);

        StartNewSeasonRewardRunForCurrentChapter(nameof(PurchaseChapterLocally));
        StartCurrentChapter();
    }

    bool IsChapterAlreadyUnlocked(ChapterData chapter)
    {
        if (chapter == null)
            return false;

        string episodeId = ResolveChapterEpisodeId(chapter);
        if (!string.IsNullOrEmpty(episodeId) && HasUnlockedEpisode(episodeId))
            return true;

        string stableKey = GetChapterUnlockKey(chapter);
        if (!string.IsNullOrEmpty(stableKey) && LocalChapterUnlockStore.IsUnlocked(stableKey))
            return true;

        if (TryGetChapter(currentSeason, currentChapter) == chapter &&
            LocalChapterUnlockStore.IsUnlocked(GetLegacyChapterUnlockKey(currentSeason, currentChapter)))
        {
            return true;
        }

        return false;
    }

    string GetChapterPurchasePendingKey(ChapterData chapter)
    {
        string episodeId = SaveDataSanitizer.SanitizeIdentifier(ResolveChapterEpisodeId(chapter));
        if (!string.IsNullOrEmpty(episodeId))
            return "chapter:" + episodeId;

        string chapterId = SaveDataSanitizer.SanitizeIdentifier(chapter != null ? chapter.chapterId : "");
        if (!string.IsNullOrEmpty(chapterId))
            return "chapter:" + chapterId;

        return chapter != null ? "chapter-instance:" + chapter.GetInstanceID() : "";
    }

    void SaveChapterUnlock(int season, int chapter)
    {
        SetChapterUnlockState(season, chapter, true);
        PlayerPrefs.Save();
    }

    bool IsChapterUnlocked(int season, int chapter)
    {
        var chapterData = TryGetChapter(season, chapter);
        string episodeId = ResolveChapterEpisodeId(chapterData);
        if (!string.IsNullOrEmpty(episodeId) && HasUnlockedEpisode(episodeId))
            return true;

        if (IsChapterPremium(chapterData) && !PrototypeFeatureFlags.LocalPremiumSpendEnabled)
            return false;

        string stableKey = GetChapterUnlockKey(chapterData);
        if (!string.IsNullOrEmpty(stableKey) && LocalChapterUnlockStore.IsUnlocked(stableKey))
            return true;

        return LocalChapterUnlockStore.IsUnlocked(GetLegacyChapterUnlockKey(season, chapter));
    }

    void ApplyUnlockedEpisodesFromServer()
    {
        var chapters = GetStoryChapters();
        if (storyData == null || chapters.Count == 0 || NetworkManager.LastUnlockedEpisodes == null)
            return;

        for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
        {
            var chapter = chapters[chapterIndex];
            var episodeId = ResolveChapterEpisodeId(chapter);
            if (string.IsNullOrEmpty(episodeId) || !HasUnlockedEpisode(episodeId))
                continue;

            SetChapterUnlockState(0, chapterIndex, true);
        }

        PlayerPrefs.Save();
    }

    bool HasUnlockedEpisode(string episodeId)
    {
        if (string.IsNullOrEmpty(episodeId))
            return false;

        if (NetworkManager.IsCatalogEpisodeUnlocked(episodeId, false))
            return true;

        if (NetworkManager.LastUnlockedEpisodes == null)
            return false;

        foreach (var unlockedEpisodeId in NetworkManager.LastUnlockedEpisodes)
        {
            if (unlockedEpisodeId == episodeId)
                return true;
        }

        return false;
    }

    List<string> CollectUnlockedEpisodeIds()
    {
        var result = new List<string>();
        var chapters = GetStoryChapters();
        if (storyData == null || chapters.Count == 0)
            return result;

        for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
        {
            var chapter = chapters[chapterIndex];
            if (chapter == null) continue;

            string episodeId = ResolveChapterEpisodeId(chapter);
            if (string.IsNullOrEmpty(episodeId))
                continue;

            if (!IsChapterPremium(chapter) || IsChapterUnlocked(0, chapterIndex))
                result.Add(episodeId);
        }

        if (!string.IsNullOrEmpty(CurrentEpisodeId) && !result.Contains(CurrentEpisodeId))
            result.Add(CurrentEpisodeId);

        return result;
    }

    void SetChapterUnlockState(int season, int chapter, bool unlocked)
    {
        int value = unlocked ? 1 : 0;
        var chapterData = TryGetChapter(season, chapter);
        string stableKey = GetChapterUnlockKey(chapterData);
        if (!string.IsNullOrEmpty(stableKey))
        {
            LocalChapterUnlockStore.SetUnlocked(stableKey, value == 1);
            PlayerPrefs.SetInt(stableKey, value);
        }

        string legacyKey = GetLegacyChapterUnlockKey(season, chapter);
        LocalChapterUnlockStore.SetUnlocked(legacyKey, value == 1);
        PlayerPrefs.SetInt(legacyKey, value);
    }

    ChapterData TryGetChapter(int season, int chapter)
    {
        var chapters = GetStoryChapters();
        if (storyData == null || chapters.Count == 0) return null;

        int chapterIndex = chapter;
        if (season != 0 && storyData.TryGetChapterIndex(season, chapter, out int flatChapterIndex))
            chapterIndex = flatChapterIndex;

        if (chapterIndex < 0 || chapterIndex >= chapters.Count) return null;

        return chapters[chapterIndex];
    }

    private void ShowChapterTitle(ChapterData chapter)
    {
        if (_chapterTitleOverlay == null)
            _chapterTitleOverlay = FindObjectOfType<ChapterTitleOverlay>(true);

        if (_chapterTitleOverlay == null)
            return;

        _chapterTitleOverlay.ShowChapter(currentChapter, GetChapterDisplayName(chapter));
    }

    string GetChapterDisplayName(ChapterData chapter)
    {
        string fallback = chapter != null ? chapter.chapterName : "";
        return StoryJsonConverter.SanitizeDisplayText(NetworkManager.GetCatalogEpisodeTitle(ResolveChapterEpisodeId(chapter), fallback));
    }

    bool IsChapterPremium(ChapterData chapter)
    {
        if (chapter == null)
            return false;

        return chapter.isPremium || NetworkManager.IsCatalogEpisodePremium(ResolveChapterEpisodeId(chapter), false);
    }

    int GetChapterUnlockCost(ChapterData chapter)
    {
        if (chapter == null)
            return 0;

        int localCost = SaveDataSanitizer.ClampCurrencyValue(chapter.unlockCost);
        string episodeId = ResolveChapterEpisodeId(chapter);
        if (!string.IsNullOrEmpty(episodeId) && NetworkManager.TryGetCatalogEpisode(episodeId, out var catalogEpisode))
            return SaveDataSanitizer.ClampCurrencyValue(catalogEpisode.candleCost);

        return localCost;
    }

    string ResolveChapterEpisodeId(ChapterData chapter)
    {
        if (chapter == null) return "";
        if (chapter.graph != null && !string.IsNullOrEmpty(chapter.graph.episodeId))
            return SaveDataSanitizer.SanitizeIdentifier(chapter.graph.episodeId);
        if (!string.IsNullOrEmpty(chapter.chapterId))
            return SaveDataSanitizer.SanitizeIdentifier(chapter.chapterId);
        return SaveDataSanitizer.SanitizeIdentifier(chapter.chapterName);
    }

    static string GetChapterUnlockKey(ChapterData chapter)
    {
        if (chapter == null) return "";
        if (!string.IsNullOrEmpty(chapter.chapterId))
            return "chapter_unlock_" + SaveDataSanitizer.SafeKeyPart(chapter.chapterId);
        if (chapter.graph != null && !string.IsNullOrEmpty(chapter.graph.episodeId))
            return "chapter_unlock_" + SaveDataSanitizer.SafeKeyPart(chapter.graph.episodeId);
        return "";
    }

    static string GetLegacyChapterUnlockKey(int season, int chapter)
    {
        return "chapter_" + season + "_" + chapter;
    }

}
