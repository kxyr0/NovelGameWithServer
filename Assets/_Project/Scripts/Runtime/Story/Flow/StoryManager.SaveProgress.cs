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
    public void LoadSaveAndStart()
    {
        if (!EnsureStorySelected())
            return;

        int saveSlot = ResolveProgressSaveSlot();
        SaveData save = SaveManager.Instance != null
            ? SaveManager.Instance.LoadForStory(CurrentStoryId, saveSlot)
            : null;

        if (TryRestoreSnapshot(save, "local save"))
            return;

        StartFreshFromSelectedSaveSlot(saveSlot, "empty selected slot");
    }
    public bool TryLoadSaveSlot(int slot)
    {
        if (!EnsureStorySelected())
            return false;

        SaveData save = SaveManager.Instance != null
            ? SaveManager.Instance.LoadForStory(CurrentStoryId, slot)
            : null;

        bool restored = TryRestoreSnapshot(save, "manual slot " + slot);
        if (!restored)
            return false;

        StorySaveSlotSelection.SelectSlot(CurrentStoryId, slot);
        return true;
    }

    public bool StartFreshFromSaveSlot(int slot)
    {
        if (!EnsureStorySelected() || !SavePathResolver.IsValidSlot(slot))
            return false;

        StartFreshFromSelectedSaveSlot(slot, "empty manual slot " + slot);
        return true;
    }

    public bool TrySaveCurrentToSlot(int slot)
    {
        if (!EnsureStorySelected() || SaveManager.Instance == null)
            return false;

        SaveData saved = SaveManager.Instance.SaveCurrentData(slot, this);
        bool hasSave = saved != null && saved.HasPosition;
        if (hasSave)
            StorySaveSlotSelection.SelectSlot(CurrentStoryId, slot);

        return hasSave;
    }

    public int ResolveProgressSaveSlot()
    {
        string storyId = SaveDataSanitizer.SanitizeIdentifier(CurrentStoryId);
        int slot = StorySaveSlotSelection.GetSelectedSlot(storyId);
        return SavePathResolver.IsValidSlot(slot) ? slot : StorySaveSlotSelection.DefaultSlot;
    }

    void StartFreshFromSelectedSaveSlot(int slot, string reason)
    {
        if (!EnsureStorySelected())
            return;

        if (!SavePathResolver.IsValidSlot(slot))
            slot = StorySaveSlotSelection.DefaultSlot;

        StorySaveSlotSelection.SelectSlot(CurrentStoryId, slot);
        StoryProgressResetUtility.ResetStoryRuntimeStateForFreshSlot(storyData, CurrentStoryId);
        NetworkManager.ClearLocalProgressCache(clearPendingSync: true);

        AppLogger.Info(
            AppLogCategory.SaveSystem,
            nameof(StoryManager),
            nameof(StartFreshFromSelectedSaveSlot),
            "[SAVE][FRESH_SLOT] Starting story from a clean selected slot.",
            LogMetadata.Of(
                "storyId", CurrentStoryId,
                "slot", slot,
                "reason", reason ?? ""));

        StartStory();
    }

    bool EnsureStorySelected()
    {
        if (storySelected && storyData != null)
            return true;

        Debug.LogError("[StoryManager] storyData must be selected with SelectStory() before starting the story.");
        return false;
    }

    bool ValidateStoryData()
    {
        var chapters = GetStoryChapters();
        if (storyData == null || chapters.Count == 0)
        {
            Debug.LogError("[StoryManager] Selected storyData has no chapters.");
            return false;
        }

        for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
        {
            var chapter = chapters[chapterIndex];
            if (chapter == null)
            {
                Debug.LogError($"[StoryManager] Chapter {chapterIndex} is missing.");
                return false;
            }
        }

        return true;
    }

    IEnumerator WaitForAuthentication(Action<bool> callback)
    {
        float waitForManager = 3f;
        float elapsed = 0f;

        while (NetworkManager.Instance == null && elapsed < waitForManager)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (NetworkManager.Instance == null)
        {
            callback?.Invoke(false);
            yield break;
        }

        float timeout = 30f;
        elapsed = 0f;

        while (!NetworkManager.IsAuthenticated && elapsed < timeout)
        {
            if (NetworkManager.AuthFlowCompleted)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        callback?.Invoke(NetworkManager.IsAuthenticated);
    }

    SaveData BuildSnapshotFromNetworkProgress()
    {
        if (!string.IsNullOrEmpty(NetworkManager.LastProgressSnapshotJson))
        {
            try
            {
                var snapshot = NetworkJson.FromSaveDataJson(NetworkManager.LastProgressSnapshotJson);
                if (snapshot != null)
                {
                    EnrichSnapshotWithNetworkProgress(snapshot);
                    return snapshot;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StoryManager] Failed to parse server snapshot: " + e.Message);
            }
        }

        if (string.IsNullOrEmpty(NetworkManager.LastProgressNodeGuid))
            return null;

        var snapshotFromProgress = new SaveData
        {
            version = 1,
            storyId = CurrentStoryId,
            episodeId = NetworkManager.LastProgressEpisodeId,
            currentNodeGuid = NetworkManager.LastProgressNodeGuid
        };

        EnrichSnapshotWithNetworkProgress(snapshotFromProgress);
        return snapshotFromProgress;
    }

    void EnrichSnapshotWithNetworkProgress(SaveData snapshot)
    {
        if (snapshot == null)
            return;

        if (string.IsNullOrEmpty(snapshot.storyId))
            snapshot.storyId = CurrentStoryId;
        if (string.IsNullOrEmpty(snapshot.currentNodeGuid))
            snapshot.currentNodeGuid = NetworkManager.LastProgressNodeGuid;
        if (string.IsNullOrEmpty(snapshot.episodeId))
            snapshot.episodeId = NetworkManager.LastProgressEpisodeId;

        if (NetworkManager.LastProgressStats != null && NetworkManager.LastProgressStats.Count > 0)
        {
            snapshot.version = SaveData.CurrentVersion;
            snapshot.savedAtIso = string.IsNullOrEmpty(snapshot.savedAtIso)
                ? DateTime.UtcNow.ToString("o")
                : snapshot.savedAtIso;
            snapshot.currency = PlayerData.Candles;
            snapshot.hearts = PlayerData.Hearts;
            if (string.IsNullOrEmpty(snapshot.playerName))
                snapshot.playerName = ResolveStoryPlayerNameForSaveFallback(PlayerAppearance.PlayerName, storyGraph);

            snapshot.statKeys.Clear();
            snapshot.statValues.Clear();
            foreach (var kv in NetworkManager.LastProgressStats)
            {
                snapshot.statKeys.Add(kv.Key);
                snapshot.statValues.Add(kv.Value);
            }
        }
    }

    bool TryRestoreSnapshot(SaveData snapshot, string source)
    {
        snapshot = SaveDataSanitizer.Sanitize(snapshot);
        SaveValidationResult validation = new SaveValidator().ValidateSnapshot(snapshot, CurrentStoryId);
        if (!validation.IsValid)
        {
            AppLogger.Warn(
                AppLogCategory.SaveSystem,
                nameof(StoryManager),
                nameof(TryRestoreSnapshot),
                "[SNAPSHOT][VALIDATION_FAILED] Snapshot was rejected before restore.",
                LogMetadata.Of(
                    "source", source,
                    "storyId", snapshot != null ? snapshot.storyId : "",
                    "expectedStoryId", CurrentStoryId,
                    "nodeGuid", snapshot != null ? snapshot.currentNodeGuid : "",
                    "errorType", validation.ErrorType,
                    "reason", validation.Message),
                recoverable: true);
            return false;
        }

        if (GameState.Instance == null)
        {
            Debug.LogError("[StoryManager] Cannot restore without GameState.");
            return false;
        }

        if (!string.IsNullOrEmpty(snapshot.storyId) &&
            !string.IsNullOrEmpty(CurrentStoryId) &&
            snapshot.storyId != CurrentStoryId)
        {
            Debug.LogWarning($"[StoryManager] Ignoring {source}: story mismatch {snapshot.storyId} != {CurrentStoryId}");
            return false;
        }

        if (!TrySelectChapterForSnapshot(snapshot, out var graph))
            return false;

        var targetNode = FindUniqueNodeByGuid(graph, snapshot.currentNodeGuid, false);
        if (targetNode == null &&
            TryFindNodeByGuidInSelectedStory(snapshot.currentNodeGuid, out StoryGraph recoveredGraph, out BaseStoryNode recoveredNode))
        {
            graph = recoveredGraph;
            targetNode = recoveredNode;
            UpdateSnapshotChapterContext(snapshot, graph);
        }

        if (targetNode == null)
            return false;

        if (targetNode is OpenWardrobeNode && TryGetConnectedStoryNode(targetNode, "exit", out BaseStoryNode wardrobeExitNode))
        {
            targetNode = wardrobeExitNode;
            snapshot.currentNodeGuid = wardrobeExitNode.guid;
            snapshot.currentDialogueLineIndex = 0;
            UpdateSnapshotChapterContext(snapshot, graph);
        }

        storyGraph = graph;

        if (HasGameplaySnapshot(snapshot))
            GameState.Instance?.ApplySnapshot(snapshot);
        else
            GameState.Instance?.InitForStory(CurrentStoryId);

        RestoreCurrentEpisodeSummaryFromSaveData(snapshot);
        ApplyHeroCustomizationSnapshot(snapshot, graph);
        RestoreOrStartSeasonRewardRunForCurrentChapter(nameof(TryRestoreSnapshot));

        activeDialogueNode = null;
        currentLineIndex = 0;
        ResetDialogueLinePages();
        ClearCutsceneRuntimeState();

        ResetCameraPosition();

        var scene = FindSceneBeforeNode(graph, targetNode);
        if (scene != null)
            ProcessScene(scene, false);

        ShowChapterTitle(GetCurrentChapterOrNull());

        suppressProgressPersistence = true;
        try
        {
            ProcessNode(targetNode, false, false);
            ApplyDialogueLineSnapshot(targetNode, snapshot.currentDialogueLineIndex);
        }
        finally
        {
            suppressProgressPersistence = false;
        }

        Debug.Log($"[StoryManager] Restored {source}: {targetNode.GetType().Name} {targetNode.guid}");
        return true;
    }

    bool HasGameplaySnapshot(SaveData snapshot)
    {
        if (snapshot == null || string.IsNullOrEmpty(snapshot.savedAtIso))
            return false;

        if (snapshot.version >= SaveData.CurrentVersion)
            return true;

        return HasLegacyGameplayPayload(snapshot);
    }

    static bool HasLegacyGameplayPayload(SaveData snapshot)
    {
        if (snapshot == null)
            return false;

        return (snapshot.statKeys != null && snapshot.statKeys.Count > 0) ||
               (snapshot.history != null && snapshot.history.Count > 0) ||
               (snapshot.wardrobe != null && snapshot.wardrobe.Count > 0) ||
               (snapshot.equippedClothes != null && snapshot.equippedClothes.Count > 0) ||
               snapshot.currency != 0 ||
               snapshot.hearts != 0;
    }

    void ApplyHeroCustomizationSnapshot(SaveData snapshot, StoryGraph graph)
    {
        HeroCustomizationState state = HeroCustomizationState.FromSaveData(snapshot);
        state.playerName = ResolveRestoredPlayerName(snapshot, graph, state.playerName);
        if (IsUserPersistablePlayerName(state.playerName, graph))
            HeroCustomizationStore.SavePlayerNameForStory(CurrentStoryId, state.playerName);

        HeroCustomizationState currentState = PlayerAppearance.CaptureState();
        if (TryResolveStoredStoryAppearance(out AppearanceType storedAppearance))
            state.appearance = storedAppearance;

        if (ShouldPreferCurrentHeroClothing(currentState.outfitId, state.outfitId, ClothingType.Outfit, graph))
        {
            state.outfitId = currentState.outfitId;
        }

        if (ShouldPreferCurrentHeroClothing(currentState.hairId, state.hairId, ClothingType.Hair, graph))
        {
            state.hairId = currentState.hairId;
        }

        if (ShouldPreferCurrentHeroClothing(currentState.accessoryId, state.accessoryId, ClothingType.Accessory, graph))
        {
            state.accessoryId = currentState.accessoryId;
        }

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

        PlayerAppearance.ApplyState(state, outfitSprite, hairSprite, outfit, hair, accessorySprite, accessory);
        HeroCustomizationStore.SaveAppearanceForStory(CurrentStoryId, state.appearance);

        if (GameState.Instance == null)
            return;

        if (!string.IsNullOrEmpty(state.outfitId))
            GameState.Instance.EquipClothing("hero:outfit", state.outfitId);

        if (!string.IsNullOrEmpty(state.hairId))
            GameState.Instance.EquipClothing("hero:hair", state.hairId);

        if (!string.IsNullOrEmpty(state.accessoryId))
            GameState.Instance.EquipClothing("hero:accessory", state.accessoryId);
    }

    bool ShouldPreferCurrentHeroClothing(string currentId, string snapshotId, ClothingType type, StoryGraph graph)
    {
        if (string.IsNullOrWhiteSpace(currentId))
            return false;

        if (string.Equals(currentId, snapshotId, StringComparison.OrdinalIgnoreCase))
            return false;

        return ResolveHeroClothing(currentId, type, graph) != null;
    }

    string ResolveRestoredPlayerName(SaveData snapshot, StoryGraph graph, string fallbackName)
    {
        string snapshotName = snapshot != null ? snapshot.playerName : "";
        if (IsPersistablePlayerName(snapshotName, graph))
            return HeroCustomizationState.NormalizePlayerName(snapshotName);

        if (TryResolveStoredStoryPlayerName(out string storyStoredName))
        {
            if (IsPersistablePlayerName(storyStoredName, graph))
                return HeroCustomizationState.NormalizePlayerName(storyStoredName);

            HeroCustomizationStore.DeletePlayerNameForStory(CurrentStoryId);
        }

        string storyDefaultName = ResolveStoryDefaultPlayerName(graph);
        if (CharacterProfileService.TryResolveSavedOrActivePlayerName(
                CurrentStoryId,
                storyDefaultName,
                out string profileName,
                out _) &&
            IsPersistablePlayerName(profileName, graph))
        {
            return HeroCustomizationState.NormalizePlayerName(profileName);
        }

        if (IsPersistablePlayerName(storyDefaultName, graph))
            return HeroCustomizationState.NormalizePlayerName(storyDefaultName);

        if (IsPersistablePlayerName(fallbackName, graph))
            return HeroCustomizationState.NormalizePlayerName(fallbackName);

        return HeroCustomizationStore.DefaultPlayerName;
    }

    bool TryResolveStoredStoryPlayerName(out string playerName)
    {
        playerName = "";

        try
        {
            return HeroCustomizationStore.TryLoadPlayerNameForStory(CurrentStoryId, out playerName);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[StoryManager] Failed to load story-scoped player name: {exception.Message}");
            playerName = "";
            return false;
        }
    }

    bool TryResolveStoredStoryAppearance(out AppearanceType appearance)
    {
        appearance = AppearanceType.Default;

        try
        {
            return HeroCustomizationStore.TryLoadAppearanceForStory(CurrentStoryId, out appearance);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[StoryManager] Failed to load story-scoped appearance: {exception.Message}");
            appearance = AppearanceType.Default;
            return false;
        }
    }

    string ResolveStoryDefaultPlayerName(StoryGraph currentGraph)
    {
        var chapters = GetStoryChapters();
        if (chapters == null)
            return "";

        foreach (ChapterData chapter in chapters)
        {
            if (chapter == null)
                continue;

            StoryGraph graph = ReferenceEquals(chapter.graph, currentGraph)
                ? currentGraph
                : ResolveGraphForChapter(chapter) ?? chapter.graph;

            if (graph == null || string.IsNullOrWhiteSpace(graph.defaultPlayerName))
                continue;

            if (IsExplicitPlayerName(graph.defaultPlayerName))
                return graph.defaultPlayerName;
        }

        return "";
    }

    PlayerNameCaseForms ResolveStoryDefaultPlayerNameCaseForms(StoryGraph currentGraph)
    {
        if (currentGraph != null && PlayerNameInflector.HasAnyCaseForms(currentGraph.defaultPlayerNameCases))
            return currentGraph.defaultPlayerNameCases;

        var chapters = GetStoryChapters();
        if (chapters == null)
            return null;

        foreach (ChapterData chapter in chapters)
        {
            if (chapter == null)
                continue;

            StoryGraph graph = ReferenceEquals(chapter.graph, currentGraph)
                ? currentGraph
                : ResolveGraphForChapter(chapter) ?? chapter.graph;

            if (graph != null && PlayerNameInflector.HasAnyCaseForms(graph.defaultPlayerNameCases))
                return graph.defaultPlayerNameCases;
        }

        return null;
    }

    string ResolveHeroCharacterDisplayName(StoryGraph graph)
    {
        ChapterData chapter = GetCurrentChapterOrNull();
        CharacterData libraryHero = chapter != null && chapter.jsonAssetLibrary != null
            ? chapter.jsonAssetLibrary.FindCharacter(heroCharacterId)
            : null;
        if (libraryHero != null && !string.IsNullOrWhiteSpace(libraryHero.characterName))
            return libraryHero.characterName;

        if (graph == null || graph.nodes == null)
            return "";

        foreach (Node node in graph.nodes)
        {
            if (node is DialogueNode dialogue && dialogue.lines != null)
            {
                foreach (DialogueLine line in dialogue.lines)
                {
                    CharacterData speaker = line != null ? line.speaker : null;
                    if (IsHeroSpeaker(speaker) && !string.IsNullOrWhiteSpace(speaker.characterName))
                        return speaker.characterName;
                }
            }
        }

        return "";
    }

    static bool IsExplicitPlayerName(string value)
    {
        if (!IsDisplayPlayerName(value))
            return false;

        string trimmed = value.Trim();
        return !string.Equals(trimmed, HeroCustomizationStore.DefaultPlayerName, StringComparison.OrdinalIgnoreCase);
    }

    static bool IsDisplayPlayerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string trimmed = SaveDataSanitizer.SanitizePlayerName(value);
        return !string.IsNullOrWhiteSpace(trimmed) &&
               !DialogueVariableResolver.IsPlayerNameToken(trimmed);
    }

    bool IsPersistablePlayerName(string value, StoryGraph graph)
    {
        if (!IsDisplayPlayerName(value))
            return false;

        string normalized = HeroCustomizationState.NormalizePlayerName(value);
        if (string.Equals(normalized, HeroCustomizationStore.DefaultPlayerName, StringComparison.OrdinalIgnoreCase) &&
            !IsStoryDefaultPlayerName(normalized, graph))
        {
            return false;
        }

        return true;
    }

    bool IsUserPersistablePlayerName(string value, StoryGraph graph)
    {
        return IsPersistablePlayerName(value, graph);
    }

    public bool IsStoryDefaultPlayerNameForCurrentStory(string value)
    {
        return IsStoryDefaultPlayerName(value, storyGraph);
    }

    public string ResolveStoryDefaultPlayerNameForCurrentStory()
    {
        return ResolveStoryDefaultPlayerName(storyGraph);
    }

    public PlayerNameCaseForms ResolveStoryDefaultPlayerNameCaseFormsForCurrentStory(string playerName)
    {
        return IsStoryDefaultPlayerNameForCurrentStory(playerName)
            ? ResolveStoryDefaultPlayerNameCaseForms(storyGraph)
            : null;
    }

    public string ResolvePersistablePlayerNameForSave(string value)
    {
        return ResolvePersistablePlayerNameForSave(value, storyGraph);
    }

    public string ResolveStoryPlayerNameForSaveFallback(string fallbackValue)
    {
        return ResolveStoryPlayerNameForSaveFallback(fallbackValue, storyGraph);
    }

    string ResolveStoryPlayerNameForSaveFallback(string fallbackValue, StoryGraph graph)
    {
        string storyDefaultName = ResolveStoryDefaultPlayerName(graph);
        if (IsPersistablePlayerName(storyDefaultName, graph))
            return HeroCustomizationState.NormalizePlayerName(storyDefaultName);

        return ResolvePersistablePlayerNameForSave(fallbackValue, graph);
    }

    string ResolvePersistablePlayerNameForSave(string value, StoryGraph graph)
    {
        if (IsPersistablePlayerName(value, graph))
            return HeroCustomizationState.NormalizePlayerName(value);

        string storyDefaultName = ResolveStoryDefaultPlayerName(graph);
        return IsPersistablePlayerName(storyDefaultName, graph)
            ? HeroCustomizationState.NormalizePlayerName(storyDefaultName)
            : "";
    }

    bool IsStoryDefaultPlayerName(string value, StoryGraph graph)
    {
        if (!IsExplicitPlayerName(value))
            return false;

        return NamesEqual(value, graph != null ? graph.defaultPlayerName : "") ||
               NamesEqual(value, ResolveHeroCharacterDisplayName(graph)) ||
               NamesEqual(value, ResolveStoryDefaultPlayerName(graph));
    }

    static bool NamesEqual(string left, string right)
    {
        left = SaveDataSanitizer.SanitizePlayerName(left);
        right = SaveDataSanitizer.SanitizePlayerName(right);
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    ClothingItem ResolveHeroClothing(string id, ClothingType type, StoryGraph graph)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        WardrobeHeroSetupPage setupPage = WardrobeHeroSetupPage.FindBestForStory(
            (Transform)null,
            CurrentStoryId,
            !string.IsNullOrWhiteSpace(CurrentChapterId) ? CurrentChapterId : CurrentEpisodeId);
        if (setupPage != null && setupPage.TryFindClothing(id, type, out ClothingItem setupItem))
            return setupItem;

        if (TryFindClothingInGraph(graph, id, type, out ClothingItem graphItem))
            return graphItem;

        ChapterData chapter = GetCurrentChapterOrNull();
        ClothingItem libraryItem = chapter != null && chapter.jsonAssetLibrary != null
            ? chapter.jsonAssetLibrary.FindClothing(id)
            : null;

        return libraryItem != null && libraryItem.type == type ? libraryItem : null;
    }

    static bool TryFindClothingInGraph(StoryGraph graph, string id, ClothingType type, out ClothingItem item)
    {
        item = null;
        if (graph == null || graph.nodes == null || string.IsNullOrWhiteSpace(id))
            return false;

        foreach (var node in graph.nodes)
        {
            WardrobeChoiceNode wardrobeNode = node as WardrobeChoiceNode;
            if (wardrobeNode == null || wardrobeNode.availableClothes == null)
                continue;

            foreach (ClothingItem clothing in wardrobeNode.availableClothes)
            {
                if (MatchesClothing(clothing, id, type))
                {
                    item = clothing;
                    return true;
                }
            }
        }

        return false;
    }

    static bool MatchesClothing(ClothingItem item, string id, ClothingType type)
    {
        if (item == null || item.type != type || string.IsNullOrWhiteSpace(id))
            return false;

        return string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(item.name, id, StringComparison.OrdinalIgnoreCase);
    }

    bool BookmarkMatchesSelectedStory(SaveData snapshot)
    {
        if (snapshot == null) return false;
        return string.IsNullOrEmpty(snapshot.storyId) || snapshot.storyId == CurrentStoryId;
    }

    bool TrySelectChapterForSnapshot(SaveData snapshot, out StoryGraph graph)
    {
        graph = null;

        if (storyData == null)
            return false;

        var chapters = GetStoryChapters();

        if (!string.IsNullOrEmpty(snapshot.episodeId) || !string.IsNullOrEmpty(snapshot.chapterId))
        {
            for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
            {
                var chapter = chapters[chapterIndex];
                if (chapter == null || chapter.graph == null) continue;

                bool episodeMatches = !string.IsNullOrEmpty(snapshot.episodeId) &&
                    ((chapter.graph != null && chapter.graph.episodeId == snapshot.episodeId) ||
                     chapter.chapterId == snapshot.episodeId);
                bool chapterMatches = !string.IsNullOrEmpty(snapshot.chapterId) &&
                    chapter.chapterId == snapshot.chapterId;

                if (!episodeMatches && !chapterMatches) continue;

                currentSeason = 0;
                currentChapter = chapterIndex;
                graph = ResolveGraphForChapter(chapter) ?? chapter.graph;
                return true;
            }

            if (storyData.seasons == null)
                return false;

            for (int seasonIndex = 0; seasonIndex < storyData.seasons.Count; seasonIndex++)
            {
                var season = storyData.seasons[seasonIndex];
                if (season == null || season.chapters == null) continue;

                for (int chapterIndex = 0; chapterIndex < season.chapters.Count; chapterIndex++)
                {
                    var chapter = season.chapters[chapterIndex];
                    if (chapter == null || chapter.graph == null) continue;

                    bool episodeMatches = !string.IsNullOrEmpty(snapshot.episodeId) &&
                        ((chapter.graph != null && chapter.graph.episodeId == snapshot.episodeId) ||
                         chapter.chapterId == snapshot.episodeId);
                    bool chapterMatches = !string.IsNullOrEmpty(snapshot.chapterId) &&
                        chapter.chapterId == snapshot.chapterId;

                    if (!episodeMatches && !chapterMatches) continue;

                    currentSeason = seasonIndex;
                    int matchedFlatChapterIndex = storyData.IndexOfChapter(chapter);
                    currentChapter = matchedFlatChapterIndex >= 0 ? matchedFlatChapterIndex : chapterIndex;
                    graph = ResolveGraphForChapter(chapter) ?? chapter.graph;
                    return true;
                }
            }
        }

        if (snapshot.currentSeasonIndex <= 0 &&
            snapshot.currentChapterIndex >= 0 &&
            snapshot.currentChapterIndex < chapters.Count)
        {
            currentSeason = 0;
            currentChapter = snapshot.currentChapterIndex;
            var chapter = chapters[currentChapter];
            if (chapter == null)
                return false;

            graph = ResolveGraphForChapter(chapter) ?? chapter.graph;
            return graph != null;
        }

        if (storyData.TryGetChapterIndex(snapshot.currentSeasonIndex, snapshot.currentChapterIndex, out int flatChapterIndex) &&
            flatChapterIndex >= 0 &&
            flatChapterIndex < chapters.Count)
        {
            currentSeason = 0;
            currentChapter = flatChapterIndex;
            var chapter = chapters[currentChapter];
            if (chapter == null)
                return false;

            graph = ResolveGraphForChapter(chapter) ?? chapter.graph;
            return graph != null;
        }

        if (storyData.seasons == null)
            return false;

        if (snapshot.currentSeasonIndex >= 0 &&
            snapshot.currentSeasonIndex < storyData.seasons.Count)
        {
            var season = storyData.seasons[snapshot.currentSeasonIndex];
            if (season != null &&
                season.chapters != null &&
                snapshot.currentChapterIndex >= 0 &&
                snapshot.currentChapterIndex < season.chapters.Count)
            {
                var chapter = season.chapters[snapshot.currentChapterIndex];
                if (chapter != null && chapter.graph != null)
                {
                    currentSeason = snapshot.currentSeasonIndex;
                    int restoredFlatChapterIndex = storyData.IndexOfChapter(chapter);
                    currentChapter = restoredFlatChapterIndex >= 0 ? restoredFlatChapterIndex : snapshot.currentChapterIndex;
                    graph = ResolveGraphForChapter(chapter) ?? chapter.graph;
                    return true;
                }
            }
        }

        graph = GetCurrentGraphOrNull();
        return graph != null;
    }

    BaseStoryNode FindUniqueNodeByGuid(StoryGraph graph, string nodeGuid, bool logMissing = true)
    {
        if (graph == null || graph.nodes == null || string.IsNullOrEmpty(nodeGuid)) return null;

        BaseStoryNode match = null;
        int count = 0;

        foreach (var node in graph.nodes)
        {
            var storyNode = node as BaseStoryNode;
            if (storyNode == null || storyNode.guid != nodeGuid) continue;

            match = storyNode;
            count++;
        }

        if (count == 1)
            return match;

        if (count > 1)
            Debug.LogError($"[StoryManager] Ambiguous node guid in graph {graph.name}: {nodeGuid}");
        else if (logMissing)
            Debug.LogWarning($"[StoryManager] Node guid not found in graph {graph.name}: {nodeGuid}");

        return null;
    }

    bool TryFindNodeByGuidInSelectedStory(string nodeGuid, out StoryGraph graph, out BaseStoryNode targetNode)
    {
        graph = null;
        targetNode = null;

        if (storyData == null || string.IsNullOrEmpty(nodeGuid))
            return false;

        int matchedSeason = 0;
        int matchedChapter = -1;
        var chapters = GetStoryChapters();
        for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
        {
            ChapterData chapter = chapters[chapterIndex];
            if (chapter == null)
                continue;

            StoryGraph chapterGraph = ResolveGraphForChapter(chapter) ?? chapter.graph;
            BaseStoryNode node = FindUniqueNodeByGuid(chapterGraph, nodeGuid, false);
            if (node == null)
                continue;

            if (targetNode != null)
            {
                Debug.LogError($"[StoryManager] Ambiguous node guid across story chapters: {nodeGuid}");
                return false;
            }

            graph = chapterGraph;
            targetNode = node;
            matchedSeason = 0;
            matchedChapter = chapterIndex;
        }

        if (targetNode == null && storyData.seasons != null)
        {
            for (int seasonIndex = 0; seasonIndex < storyData.seasons.Count; seasonIndex++)
            {
                SeasonData season = storyData.seasons[seasonIndex];
                if (season == null || season.chapters == null)
                    continue;

                for (int chapterIndex = 0; chapterIndex < season.chapters.Count; chapterIndex++)
                {
                    ChapterData chapter = season.chapters[chapterIndex];
                    if (chapter == null)
                        continue;

                    StoryGraph chapterGraph = ResolveGraphForChapter(chapter) ?? chapter.graph;
                    BaseStoryNode node = FindUniqueNodeByGuid(chapterGraph, nodeGuid, false);
                    if (node == null)
                        continue;

                    if (targetNode != null)
                    {
                        Debug.LogError($"[StoryManager] Ambiguous node guid across story chapters: {nodeGuid}");
                        return false;
                    }

                    graph = chapterGraph;
                    targetNode = node;
                    matchedSeason = seasonIndex;
                    int flatChapterIndex = storyData.IndexOfChapter(chapter);
                    matchedChapter = flatChapterIndex >= 0 ? flatChapterIndex : chapterIndex;
                }
            }
        }

        if (targetNode == null || graph == null || matchedChapter < 0)
            return false;

        currentSeason = matchedSeason;
        currentChapter = matchedChapter;
        return true;
    }

    void UpdateSnapshotChapterContext(SaveData snapshot, StoryGraph graph)
    {
        if (snapshot == null)
            return;

        snapshot.currentSeasonIndex = currentSeason;
        snapshot.currentChapterIndex = currentChapter;

        ChapterData chapter = GetCurrentChapterOrNull();
        if (chapter != null)
            snapshot.chapterId = chapter.chapterId ?? "";

        if (graph != null)
        {
            snapshot.graphName = graph.name ?? "";
        }

        string episodeId = graph != null && !string.IsNullOrWhiteSpace(graph.episodeId)
            ? graph.episodeId
            : ResolveChapterEpisodeId(chapter);
        snapshot.episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
    }

    SceneSetupNode FindSceneBeforeNode(StoryGraph graph, BaseStoryNode targetNode)
    {
        if (graph == null || graph.nodes == null || targetNode == null || targetNode is SceneSetupNode)
            return null;

        var queue = new Queue<TraversalState>();
        var visited = new HashSet<string>();

        foreach (var node in graph.nodes)
        {
            if (node is StartNode start)
                queue.Enqueue(new TraversalState(start, null));
        }

        while (queue.Count > 0)
        {
            var state = queue.Dequeue();
            if (state.node == null) continue;

            string key = state.node.GetInstanceID() + ":" +
                         (state.lastScene != null ? state.lastScene.GetInstanceID().ToString() : "none");
            if (!visited.Add(key))
                continue;

            if (state.node == targetNode)
                return state.lastScene;

            var nextScene = state.node is SceneSetupNode scene ? scene : state.lastScene;
            foreach (var next in GetConnectedStoryNodes(state.node))
                queue.Enqueue(new TraversalState(next, nextScene));
        }

        return null;
    }

    IEnumerable<BaseStoryNode> GetConnectedStoryNodes(BaseStoryNode node)
    {
        foreach (var output in node.Outputs)
        {
            foreach (var connection in output.GetConnections())
            {
                if (connection.node is BaseStoryNode storyNode)
                    yield return storyNode;
            }
        }
    }

    void ApplyDialogueLineSnapshot(BaseStoryNode targetNode, int lineIndex)
    {
        var dialogueNode = targetNode as DialogueNode;
        if (dialogueNode == null || dialogueNode.lines == null || dialogueNode.lines.Count == 0)
            return;

        if (dialogueNode is CutsceneNode)
        {
            if (!EnsureCutsceneUserInterface("restoring a cutscene line"))
                return;
        }
        else if (!EnsureDialogueUI("restoring a dialogue line"))
        {
            return;
        }

        currentLineIndex = Mathf.Clamp(lineIndex, 0, dialogueNode.lines.Count - 1);
        activeDialogueNode = dialogueNode;

        var line = dialogueNode.lines[currentLineIndex];
        ApplyDialogueLineVisualSnapshot(dialogueNode, line);
        ShowDialogueLinePage(line);
        if (dialogueNode is CutsceneNode)
            TryPanCutsceneBackground(line);
        else
            TryAutoPan(line);
    }

    void ApplyDialogueLineVisualSnapshot(DialogueNode dialogueNode, DialogueLine line)
    {
        if (dialogueNode == null || dialogueNode is CutsceneNode)
            return;

        EnsureRuntimeActiveCharacters(dialogueNode);

        if (IsNarrationLine(line))
        {
            HandleNarrationLine(line);
            return;
        }

        TryShowDialogueSpeaker(line, !HasExplicitCharacters(dialogueNode), out _);
    }

    void PersistProgress(BaseStoryNode node)
    {
        if (suppressProgressPersistence || node == null || !HasSelectedStory)
            return;

        int saveSlot = ResolveProgressSaveSlot();
        SaveData snapshot = SaveManager.Instance != null
            ? SaveManager.Instance.SaveCurrentDataLightweight(saveSlot, this)
            : null;

        if (snapshot != null && StoryProgressResetUtility.ShouldForceFreshStart(CurrentStoryId))
            StoryProgressResetUtility.ClearForceFreshStart(CurrentStoryId);

        if (snapshot == null || !NetworkManager.IsAuthenticated)
            return;

        NetworkManager.Instance?.SaveProgressAsync(
            snapshot.episodeId,
            snapshot.currentNodeGuid,
            snapshot,
            GameState.Instance != null ? GameState.Instance.GetStatsSnapshot() : null,
            null,
            CollectUnlockedEpisodeIds());
    }

    void SyncCompletedBoundarySnapshot(SaveData snapshot, string episodeId)
    {
        if (snapshot == null || NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
            return;

        NetworkManager.Instance.SaveProgressAsync(
            string.IsNullOrEmpty(snapshot.episodeId) ? episodeId : snapshot.episodeId,
            snapshot.currentNodeGuid,
            snapshot,
            GameState.Instance != null ? GameState.Instance.GetStatsSnapshot() : null,
            null,
            CollectUnlockedEpisodeIds());
    }

    struct TraversalState
    {
        public BaseStoryNode node;
        public SceneSetupNode lastScene;

        public TraversalState(BaseStoryNode node, SceneSetupNode lastScene)
        {
            this.node = node;
            this.lastScene = lastScene;
        }
    }

}
