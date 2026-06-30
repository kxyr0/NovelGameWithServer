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
    private void Start()
    {
        // Запуск через сервер: дожидаемся авторизации, потом загружаем прогресс.
        // Если сервер недоступен или авторизация не прошла — показать экран ошибки/повтора.
        if (noConnectionPanel != null)
            noConnectionPanel.SetActive(false);
    }

    /// <summary>
    /// Стартовый поток: авторизация → загрузка прогресса → запуск с нужной ноды.
    /// Игра не запускается без подтверждённой сессии на сервере.
    /// </summary>
    private IEnumerator StartupFlow()
    {
        // Шаг 0: ждём пока NetworkManager инициализируется (он тоже в Start())
        if (!EnsureStorySelected())
            yield break;

        float waitForManager = 3f;
        float waitElapsed = 0f;
        while (NetworkManager.Instance == null && waitElapsed < waitForManager)
        {
            waitElapsed += Time.deltaTime;
            yield return null;
        }

        if (NetworkManager.Instance == null)
        {
            Debug.LogError("[StoryManager] NetworkManager не найден в сцене!");
            ShowNoConnectionError();
            yield break;
        }

        // Шаг 1: дожидаемся авторизации NetworkManager
        // NetworkManager.Start() запускает AuthFlow — ждём его завершения
        float timeout = 30f;
        float elapsed = 0f;

        while (!NetworkManager.IsAuthenticated && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!NetworkManager.IsAuthenticated)
        {
            Debug.LogError("[StoryManager] Авторизация не прошла — нет соединения с сервером");
            ShowNoConnectionError();
            yield break;
        }

        // Шаг 2: загружаем прогресс с сервера
        if (StoryProgressResetUtility.ShouldForceFreshStart(CurrentStoryId))
        {
            NetworkManager.ClearLocalProgressCache(clearPendingSync: true);
            StartStory();
            yield break;
        }

        bool progressLoaded = false;
        yield return NetworkManager.Instance.LoadProgress(ok => progressLoaded = ok);

        if (!progressLoaded)
        {
            Debug.LogWarning("[StoryManager] Прогресс не загрузился — начинаем с начала");
            StartStory();
            yield break;
        }

        // Шаг 3: если есть сохранённая позиция — восстанавливаем, иначе с начала
        string nodeGuid = NetworkManager.LastProgressNodeGuid;
        string episodeId = NetworkManager.LastProgressEpisodeId;

        if (!string.IsNullOrEmpty(nodeGuid))
            LoadFromServerProgress(nodeGuid, episodeId);
        else
            StartStory();
    }

    /// <summary>
    /// Показать экран ошибки подключения (реализовать в инспекторе).
    /// </summary>
    void ShowNoConnectionError()
    {
        if (noConnectionPanel != null)
            noConnectionPanel.SetActive(true);
        else
            Debug.LogError("[StoryManager] noConnectionPanel не назначен — нет UI ошибки подключения");
    }

    /// <summary>
    /// Повторить подключение. Назначь на кнопку «Повторить» в noConnectionPanel.
    /// </summary>
    public void RetryConnection()
    {
        if (noConnectionPanel != null)
            noConnectionPanel.SetActive(false);

        StartCoroutine(LoadAndStart());
    }

    public bool SelectStory(StoryData selectedStory)
    {
        long startedAt = AppDiagnostics.StartTimer();
        JsonGraphCache.Clear();
        selectedStory = EditorTestChapterLoader.ResolveStory(selectedStory);
        FadeOutStoryAudioForStorySelection();

        if (selectedStory == null)
        {
            AppLogger.Error(
                AppLogCategory.App,
                nameof(StoryManager),
                nameof(SelectStory),
                "Story selection failed because StoryData was null.",
                metadata: LogMetadata.Of("selectedStoryIsNull", true),
                durationMs: AppDiagnostics.ElapsedMilliseconds(startedAt),
                recoverable: true);
            Debug.LogError("[StoryManager] SelectStory received null StoryData. Assign StoryData in the selected GameData asset before starting the story.", this);
            storyData = null;
            storySelected = false;
            storyGraph = null;
            activeDialogueNode = null;
            currentLineIndex = 0;
            ResetDialogueLinePages();
            ClearCutsceneRuntimeState();
            ResetEndPanelState();
            ApplyStoryUserInterfaceProfile();
            return false;
        }

        storyData = selectedStory;
        storySelected = true;
        currentSeason = 0;
        currentChapter = 0;
        storyGraph = null;
        activeDialogueNode = null;
        currentLineIndex = 0;
        ResetDialogueLinePages();
        ClearCutsceneRuntimeState();
        ResetEndPanelState();
        ApplyStoryUserInterfaceProfile();

        if (!EnsureStorySelected())
            return false;

        if (!ValidateStoryData())
        {
            AppLogger.Error(
                AppLogCategory.App,
                nameof(StoryManager),
                nameof(SelectStory),
                "Story selection failed validation.",
                metadata: LogMetadata.Of("storyId", CurrentStoryId, "storyName", CurrentStoryTitle),
                durationMs: AppDiagnostics.ElapsedMilliseconds(startedAt),
                recoverable: true);
            storyData = null;
            storySelected = false;
            ApplyStoryUserInterfaceProfile();
            return false;
        }

        GameState.Instance?.InitForStory(CurrentStoryId);
        ApplyPendingInitialStoryStats();
        (storyHistory ?? StoryHistory.Instance)?.LoadBookmarkFromPrefs(CurrentStoryId);
        AppLogger.Info(
            AppLogCategory.App,
            nameof(StoryManager),
            nameof(SelectStory),
            "Story selected.",
            LogMetadata.Of("storyId", CurrentStoryId, "storyName", CurrentStoryTitle, "chapterCount", StoryChapterCount),
            AppDiagnostics.ElapsedMilliseconds(startedAt));
        return true;
    }

    /// <summary>
    /// Загрузить прогресс с сервера и запустить историю.
    /// Вызывается из MenuController при выборе истории.
    /// </summary>
    public IEnumerator LoadAndStart()
    {
        long startedAt = AppDiagnostics.StartTimer();
        if (!EnsureStorySelected() || !ValidateStoryData())
            yield break;

        var history = storyHistory ?? StoryHistory.Instance;
        history?.LoadBookmarkFromPrefs(CurrentStoryId);

        int saveSlot = ResolveProgressSaveSlot();
        SaveData localSnapshot = SaveManager.Instance != null
            ? SaveManager.Instance.LoadForStory(CurrentStoryId, saveSlot)
            : null;

        if (StoryProgressResetUtility.ShouldForceFreshStart(CurrentStoryId))
        {
            NetworkManager.ClearLocalProgressCache(clearPendingSync: true);
            LogLoadAndStartCompleted("Starting from beginning after local progress reset.", startedAt, "forceFreshStart");
            Debug.Log("[StoryManager] Starting from beginning after local progress reset.");
            StartStory();
            yield break;
        }

        if (ShouldUseSelectedSaveSlotAsAuthoritative())
        {
            if (localSnapshot == null || !localSnapshot.HasPosition)
            {
                LogLoadAndStartCompleted("Started story from an empty selected save slot.", startedAt, "emptySelectedSlot");
                StartFreshFromSelectedSaveSlot(saveSlot, "empty selected slot");
                yield break;
            }

            if (TryRestoreSnapshot(localSnapshot, "selected save slot"))
            {
                LogLoadAndStartCompleted("Restored from selected save slot.", startedAt, "selectedSaveSlot");
                yield break;
            }

            LogLoadAndStartCompleted("Selected save slot could not be restored; starting fresh.", startedAt, "selectedSaveSlotInvalid");
            StartFreshFromSelectedSaveSlot(saveSlot, "selected save slot restore failed");
            yield break;
        }

        if (TryResumeFromStoredChapterBoundary())
        {
            LogLoadAndStartCompleted("Resumed from stored chapter boundary.", startedAt, "chapterBoundary");
            yield break;
        }

        if (TryRestoreSnapshot(localSnapshot, "local save"))
        {
            LogLoadAndStartCompleted("Restored from local save.", startedAt, "localSave");
            yield break;
        }

        if (HasPlayableLocalChapter())
        {
            LogLoadAndStartCompleted("Started playable local chapter without waiting for server progress.", startedAt, "localChapter");
            Debug.Log("[StoryManager] Starting local chapter without waiting for server progress.");
            StartStory();
            yield break;
        }

        bool authenticated = false;
        yield return WaitForAuthentication(ok => authenticated = ok);

        if (!authenticated)
        {
            if (TryRestoreSnapshot(localSnapshot, "local save"))
            {
                LogLoadAndStartCompleted("Restored from local save after authentication failure.", startedAt, "localSaveAfterAuthFailure");
                yield break;
            }

            LogLoadAndStartCompleted("Started selected local story after authentication failure.", startedAt, "authFailureFallback");
            Debug.LogWarning("[StoryManager] Server authentication failed. Starting selected local story graph without server progress.");
            StartStory();
            yield break;
        }

        yield return NetworkManager.Instance.SyncCatalog();
        yield return NetworkManager.Instance.SyncBalance();
        yield return NetworkManager.Instance.SyncFeatures();

        bool progressLoaded = false;
        yield return NetworkManager.Instance.LoadProgress(ok => progressLoaded = ok);
        ApplyUnlockedEpisodesFromServer();

        SaveData selectedSnapshot = progressLoaded
            ? NetworkManager.ResolveLatestProgressSnapshot(CurrentStoryId, localSnapshot)
            : localSnapshot;

        if (selectedSnapshot != null)
        {
            yield return SyncRemoteGraphCacheIfNeeded(selectedSnapshot);

            if (TryResumeFromStoredChapterBoundary())
            {
                LogLoadAndStartCompleted("Resumed from stored chapter boundary after remote cache sync.", startedAt, "chapterBoundaryAfterRemoteCache");
                yield break;
            }

            if (TryRestoreSnapshot(selectedSnapshot, ResolveSnapshotSourceLabel(selectedSnapshot, localSnapshot)))
            {
                if (ReferenceEquals(selectedSnapshot, localSnapshot) && NetworkManager.Instance != null)
                {
                    NetworkManager.Instance.SaveProgressAsync(
                        localSnapshot.episodeId,
                        localSnapshot.currentNodeGuid,
                        localSnapshot,
                        GameState.Instance != null ? GameState.Instance.GetStatsSnapshot() : null,
                        null,
                        CollectUnlockedEpisodeIds());
                }

                if (NetworkManager.Instance != null)
                    yield return NetworkManager.Instance.FlushPendingSync(CurrentStoryId);

                LogLoadAndStartCompleted("Restored from selected progress snapshot.", startedAt, ResolveSnapshotSourceLabel(selectedSnapshot, localSnapshot));
                yield break;
            }
        }

        yield return SyncRemoteGraphCacheIfNeeded(localSnapshot);
        if (TryResumeFromStoredChapterBoundary())
        {
            LogLoadAndStartCompleted("Resumed from stored chapter boundary after local cache sync.", startedAt, "chapterBoundaryAfterLocalCache");
            yield break;
        }

        if (TryRestoreSnapshot(localSnapshot, "local save"))
        {
            LogLoadAndStartCompleted("Restored from local save after fallback cache sync.", startedAt, "localSaveAfterCacheSync");
            yield break;
        }

        LogLoadAndStartCompleted("Started story from beginning.", startedAt, "freshStart");
        StartStory();
    }

    void LogLoadAndStartCompleted(string message, long startedAt, string source)
    {
        long durationMs = AppDiagnostics.ElapsedMilliseconds(startedAt);
        AppLogger.Info(
            AppLogCategory.App,
            nameof(StoryManager),
            nameof(LoadAndStart),
            message,
            LogMetadata.Of(
                "storyId", CurrentStoryId,
                "chapterId", CurrentChapterId,
                "episodeId", CurrentEpisodeId,
                "source", source,
                "networkAuthenticated", NetworkManager.IsAuthenticated,
                "networkOnline", NetworkManager.IsOnline),
            durationMs);
        AppDiagnostics.LogIfSlow(
            AppLogCategory.App,
            nameof(StoryManager),
            nameof(LoadAndStart),
            durationMs,
            LogMetadata.Of("storyId", CurrentStoryId, "source", source));
    }

    bool ShouldUseSelectedSaveSlotAsAuthoritative()
    {
        return true;
    }

    string ResolveSnapshotSourceLabel(SaveData selectedSnapshot, SaveData localSnapshot)
    {
        if (ReferenceEquals(selectedSnapshot, localSnapshot))
            return "local save";

        var pending = NetworkManager.GetPendingProgressSnapshot(CurrentStoryId);
        if (ReferenceEquals(selectedSnapshot, pending))
            return "pending sync";

        return "server progress";
    }

    /// <summary>
    /// Восстановить игровую сессию по данным с сервера.
    /// Ищет нужную ноду в графе по guid.
    /// </summary>
    void LoadFromServerProgress(string nodeGuid, string episodeId)
    {
        var snapshot = new SaveData
        {
            storyId = CurrentStoryId,
            episodeId = episodeId,
            currentNodeGuid = nodeGuid
        };

        if (!TryRestoreSnapshot(snapshot, "server progress"))
            StartStory();
    }

    /// <summary>
    /// Найти граф по episodeId среди всех глав в storyData.
    /// Возвращает null если не найден.
    /// </summary>
    XNode.NodeGraph FindGraphByEpisodeId(string episodeId)
    {
        if (string.IsNullOrEmpty(episodeId) || storyData == null) return null;

        var directChapters = GetStoryChapters();
        for (int chapterIndex = 0; chapterIndex < directChapters.Count; chapterIndex++)
        {
            var chapter = directChapters[chapterIndex];
            if (chapter == null) continue;

            if (chapter.graph is StoryGraph sg &&
                !string.IsNullOrEmpty(sg.episodeId) &&
                sg.episodeId == episodeId)
            {
                currentSeason = 0;
                currentChapter = chapterIndex;
                storyGraph = ResolveGraphForChapter(chapter) ?? sg;
                return storyGraph;
            }
        }

        if (storyData.seasons == null) return null;

        foreach (var season in storyData.seasons)
        {
            if (season == null || season.chapters == null) continue;

            foreach (var chapter in season.chapters)
            {
                if (chapter == null) continue;

                if (chapter.graph is StoryGraph sg &&
                    !string.IsNullOrEmpty(sg.episodeId) &&
                    sg.episodeId == episodeId)
                {
                    // Выставляем текущий сезон/главу чтобы OnChapterFinished правильно работал
                    currentSeason = storyData.IndexOfSeason(season);
                    int flatChapterIndex = storyData.IndexOfChapter(chapter);
                    currentChapter = flatChapterIndex >= 0 ? flatChapterIndex : season.IndexOfChapter(chapter);
                    storyGraph = ResolveGraphForChapter(chapter) ?? sg;
                    return storyGraph;
                }
            }
        }
        return null;
    }

    StoryGraph GetCurrentGraph()
    {
        return GetCurrentGraphOrNull();
    }

    StoryGraph GetCurrentGraphOrNull()
    {
        var chapter = GetCurrentChapterOrNull();
        return chapter != null ? ResolveGraphForChapter(chapter) ?? chapter.graph : null;
    }

    IReadOnlyList<ChapterData> GetStoryChapters()
    {
        return storyData != null && storyData.chapters != null
            ? storyData.chapters
            : EmptyChapters;
    }

    SeasonData GetCurrentSeasonOrNull()
    {
        if (storyData == null || storyData.seasons == null) return null;
        if (currentSeason < 0 || currentSeason >= storyData.seasons.Count) return null;
        return storyData.seasons[currentSeason];
    }

    ChapterData GetCurrentChapterOrNull()
    {
        var chapters = GetStoryChapters();
        if (currentChapter < 0 || currentChapter >= chapters.Count) return null;
        return chapters[currentChapter];
    }

    ChapterData GetChapterAtIndexOrNull(int chapterIndex)
    {
        var chapters = GetStoryChapters();
        if (chapterIndex < 0 || chapterIndex >= chapters.Count) return null;
        return chapters[chapterIndex];
    }

    void ResetEndPanelState()
    {
        lastCompletedChapter = -1;
        endPanelNextChapter = -1;
        endPanelStoryFinished = false;
        ResetEpisodeSummaryState();
    }

    void MarkEndPanelClosedStory()
    {
        endPanelNextChapter = -1;
        endPanelStoryFinished = true;
    }

    void MarkEndPanelNextChapter(int chapterIndex)
    {
        endPanelNextChapter = GetChapterAtIndexOrNull(chapterIndex) != null ? chapterIndex : -1;
        endPanelStoryFinished = false;
    }

    int ResolveEndPanelNextChapterIndex()
    {
        if (GetChapterAtIndexOrNull(endPanelNextChapter) != null)
            return endPanelNextChapter;

        int nextAfterCompleted = lastCompletedChapter + 1;
        if (lastCompletedChapter >= 0 && GetChapterAtIndexOrNull(nextAfterCompleted) != null)
            return nextAfterCompleted;

        return -1;
    }

    bool TryResumeFromStoredChapterBoundary()
    {
        if (!HasSelectedStory || !ValidateStoryData())
            return false;

        string prefix = GetChapterBoundaryResumeKeyPrefix();
        int hasBoundary = LoadChapterBoundaryValue(prefix, "has", 0, out bool hasWasProtected, out bool hasHadValue);
        if (hasBoundary != 1)
            return false;

        LoadLastCompletedEpisodeSummary(prefix);

        int completedChapterIndex = LoadChapterBoundaryValue(prefix, "completed", -1, out bool completedWasProtected, out bool completedHadValue);
        int nextChapterIndex = LoadChapterBoundaryValue(prefix, "next", -1, out bool nextWasProtected, out bool nextHadValue);
        bool storyFinished = LoadChapterBoundaryValue(prefix, "finished", 0, out bool finishedWasProtected, out bool finishedHadValue) == 1;
        bool shouldMigrateBoundary = (hasHadValue && !hasWasProtected) ||
            (completedHadValue && !completedWasProtected) ||
            (nextHadValue && !nextWasProtected) ||
            (finishedHadValue && !finishedWasProtected);

        if (storyFinished)
        {
            ChapterData completedChapter = GetChapterAtIndexOrNull(completedChapterIndex);
            if (completedChapter == null)
            {
                ClearChapterBoundaryResume();
                return false;
            }

            currentSeason = 0;
            currentChapter = completedChapterIndex;
            lastCompletedChapter = completedChapterIndex;
            MarkEndPanelClosedStory();
            PrepareBoundaryResumeState(completedChapter);
            SetPurchaseVisible(false);
            if (shouldMigrateBoundary)
                SaveChapterBoundaryResume(completedChapterIndex, nextChapterIndex, true);
            OpenEndPanel();
            return true;
        }

        ChapterData nextChapter = GetChapterAtIndexOrNull(nextChapterIndex);
        if (nextChapter == null)
        {
            ClearChapterBoundaryResume();
            return false;
        }

        currentSeason = 0;
        currentChapter = nextChapterIndex;
        lastCompletedChapter = completedChapterIndex;
        MarkEndPanelNextChapter(nextChapterIndex);

        if (IsChapterPremium(nextChapter) && !IsChapterUnlocked(currentSeason, currentChapter))
        {
            PrepareBoundaryResumeState(nextChapter);
            SetPurchaseVisible(true);
            WirePurchaseButton(nextChapter);
            if (shouldMigrateBoundary)
                SaveChapterBoundaryResume(completedChapterIndex, nextChapterIndex, false);
            OpenEndPanel();
            return true;
        }

        ClearChapterBoundaryResume();
        StartNewSeasonRewardRunForCurrentChapter(nameof(TryResumeFromStoredChapterBoundary));
        StartCurrentChapter();
        return true;
    }

    void PrepareBoundaryResumeState(ChapterData chapter)
    {
        if (GameState.Instance != null &&
            !string.Equals(GameState.Instance.CurrentStoryId, CurrentStoryId, StringComparison.OrdinalIgnoreCase))
        {
            GameState.Instance.InitForStory(CurrentStoryId);
        }

        storyGraph = ResolveGraphForChapter(chapter) ?? chapter?.graph;
        activeDialogueNode = null;
        currentLineIndex = 0;
        ResetDialogueLinePages();
        ClearCutsceneRuntimeState();
        ResetCameraPosition();
        dialogueUI?.ResetStoryUi();
    }

    void SaveChapterBoundaryResume(int completedChapterIndex, int nextChapterIndex, bool storyFinished)
    {
        if (!HasSelectedStory)
            return;

        string prefix = GetChapterBoundaryResumeKeyPrefix();
        SaveChapterBoundaryValue(prefix, "has", 1);
        SaveChapterBoundaryValue(prefix, "completed", completedChapterIndex);
        SaveChapterBoundaryValue(prefix, "next", nextChapterIndex);
        SaveChapterBoundaryValue(prefix, "finished", storyFinished ? 1 : 0);
        SaveLastCompletedEpisodeSummary(prefix);
        PlayerPrefs.Save();
    }

    void ClearChapterBoundaryResume()
    {
        if (!HasSelectedStory)
            return;

        string prefix = GetChapterBoundaryResumeKeyPrefix();
        DeleteChapterBoundaryKey(prefix + "has");
        DeleteChapterBoundaryKey(prefix + "completed");
        DeleteChapterBoundaryKey(prefix + "next");
        DeleteChapterBoundaryKey(prefix + "finished");
        ClearLastCompletedEpisodeSummary(prefix);
        PlayerPrefs.Save();
    }

    string GetChapterBoundaryResumeKeyPrefix()
    {
        return ChapterBoundaryResumePrefix + SaveDataSanitizer.SafeKeyPart(CurrentStoryId) + "_";
    }

    static void SaveChapterBoundaryValue(string prefsPrefix, string suffix, int value)
    {
        string payload = SaveDataSanitizer.ClampStatValue(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string protectedPayload = LocalSaveSecurity.ProtectText(payload, GetChapterBoundaryPurpose(prefsPrefix, suffix));
        if (string.IsNullOrEmpty(protectedPayload))
        {
            Debug.LogWarning("[StoryManager] Chapter boundary payload could not be protected.");
            return;
        }

        string key = prefsPrefix + suffix;
        PlayerPrefs.SetString(key, protectedPayload);
        LocalSecurePrefs.MarkSecure(key);
    }

    static int LoadChapterBoundaryValue(string prefsPrefix, string suffix, int defaultValue, out bool wasProtected, out bool hadValue)
    {
        wasProtected = false;
        hadValue = false;

        string key = prefsPrefix + suffix;
        if (!PlayerPrefs.HasKey(key))
            return defaultValue;

        string stored = "";
        try
        {
            stored = PlayerPrefs.GetString(key, "");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[StoryManager] Failed to read chapter boundary value: " + exception.Message);
        }

        if (!string.IsNullOrEmpty(stored))
        {
            hadValue = true;
            if (stored.Length > LocalSaveSecurity.MaxProtectedPayloadChars)
            {
                DeleteChapterBoundaryKey(key);
                return defaultValue;
            }

            if (!LocalSaveSecurity.TryUnprotectText(stored, GetChapterBoundaryPurpose(prefsPrefix, suffix), out string payload, out wasProtected))
            {
                Debug.LogWarning("[StoryManager] Ignored chapter boundary payload with invalid integrity.");
                DeleteChapterBoundaryKey(key);
                hadValue = false;
                return defaultValue;
            }

            if (!wasProtected && LocalSecurePrefs.HasSecureMarker(key))
            {
                Debug.LogWarning("[StoryManager] Ignored downgraded chapter boundary payload.");
                DeleteChapterBoundaryKey(key);
                hadValue = false;
                return defaultValue;
            }

            if (wasProtected)
                LocalSecurePrefs.EnsureSecureMarker(key);

            return int.TryParse(payload, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed)
                ? SaveDataSanitizer.ClampStatValue(parsed)
                : defaultValue;
        }

        try
        {
            if (LocalSecurePrefs.HasSecureMarker(key))
            {
                Debug.LogWarning("[StoryManager] Ignored downgraded chapter boundary int.");
                DeleteChapterBoundaryKey(key);
                hadValue = false;
                return defaultValue;
            }

            hadValue = true;
            return SaveDataSanitizer.ClampStatValue(PlayerPrefs.GetInt(key, defaultValue));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[StoryManager] Failed to read legacy chapter boundary value: " + exception.Message);
            DeleteChapterBoundaryKey(key);
            hadValue = false;
            return defaultValue;
        }
    }

    static void DeleteChapterBoundaryKey(string key)
    {
        try
        {
            LocalSecurePrefs.Delete(key);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[StoryManager] Failed to delete invalid chapter boundary key: " + exception.Message);
        }
    }

    static string GetChapterBoundaryPurpose(string prefsPrefix, string suffix)
    {
        return LocalSaveSecurity.ChapterBoundaryPurpose + ":" +
               SaveDataSanitizer.SanitizeIdentifier(prefsPrefix) + ":" +
               SaveDataSanitizer.SanitizeIdentifier(suffix);
    }

    bool HasPlayableLocalChapter()
    {
        var chapter = GetCurrentChapterOrNull();
        if (chapter == null)
            return false;

        if (chapter.graph != null)
            return true;

        return chapter.jsonGraph != null && !string.IsNullOrWhiteSpace(chapter.jsonGraph.text);
    }

}
