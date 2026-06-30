using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

internal static class NovelTemplateLoggingBootstrap
{
    private static bool _installed;

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void InitializeInEditor()
    {
        Install();
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeBeforeSceneLoad()
    {
        Install();
        EnsureRuntimeObject();
    }

    private static void Install()
    {
        if (_installed)
            return;

        _installed = true;
        AppLogger.Initialize();

        Application.logMessageReceivedThreaded += HandleUnityLog;
        Application.quitting += HandleApplicationQuitting;
        AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
        TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;

        AppLogger.Info(
            AppLogCategory.App,
            nameof(NovelTemplateLoggingBootstrap),
            "Install",
            "Logging diagnostics initialized.",
            LogMetadata.Of(
                "logDir", AppLogger.Settings.LogDirectory,
                "logLevel", AppLogger.Settings.LogLevel,
                "logToFile", AppLogger.Settings.LogToFile,
                "logToConsole", AppLogger.Settings.LogToConsole,
                "diagnosticsEnabled", AppLogger.Settings.EnableDiagnostics,
                "diagnosticsIntervalMs", AppLogger.Settings.DiagnosticsIntervalMs));
    }

    private static void EnsureRuntimeObject()
    {
        if (NovelTemplateDiagnosticsRuntime.Instance != null)
            return;

        var gameObject = new GameObject(nameof(NovelTemplateDiagnosticsRuntime));
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        gameObject.AddComponent<NovelTemplateDiagnosticsRuntime>();
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
    }

    private static void HandleUnityLog(string condition, string stackTrace, LogType type)
    {
        AppLogger.UnityLog(type, condition, stackTrace);
    }

    private static void HandleApplicationQuitting()
    {
        AppLogger.Info(
            AppLogCategory.App,
            nameof(NovelTemplateLoggingBootstrap),
            "ApplicationQuit",
            "Application shutdown requested.",
            BuildRuntimeMetadata());
    }

    private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        Exception exception = args.ExceptionObject as Exception;
        AppLogger.Fatal(
            AppLogCategory.Error,
            nameof(NovelTemplateLoggingBootstrap),
            "UnhandledException",
            "Unhandled exception reached the application domain.",
            exception,
            LogMetadata.Of("isTerminating", args.IsTerminating));
    }

    private static void HandleUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args)
    {
        AppLogger.Error(
            AppLogCategory.Error,
            nameof(NovelTemplateLoggingBootstrap),
            "UnobservedTaskException",
            "Unobserved task exception was reported.",
            args.Exception,
            null,
            null,
            null,
            recoverable: true);
    }

    internal static System.Collections.Generic.IDictionary<string, object> BuildRuntimeMetadata()
    {
        long managedBytes = 0;
        string managedMemoryError = "";
        try
        {
            managedBytes = GC.GetTotalMemory(false);
        }
        catch (Exception exception)
        {
            managedMemoryError = FormatMetadataException(exception);
        }

        string sceneName = "";
        string sceneError = "";
        try
        {
            sceneName = SceneManager.GetActiveScene().name;
        }
        catch (Exception exception)
        {
            sceneError = FormatMetadataException(exception);
        }

        StoryManager storyManager = StoryManager.Instance;
        GameState gameState = GameState.Instance;
        BaseStoryNode currentNode = gameState != null ? gameState.currentNode : null;
        SaveData runtimeSave = BuildRuntimeSaveSnapshot(storyManager);

        IDictionary<string, object> metadata = LogMetadata.Of(
            "uptimeSeconds", Time.realtimeSinceStartup,
            "managedMemoryBytes", managedBytes,
            "scene", sceneName,
            "networkOnline", NetworkManager.IsOnline,
            "networkAuthenticated", NetworkManager.IsAuthenticated,
            "networkErrorKind", NetworkManager.LastErrorKind,
            "hasPendingSync", NetworkManager.HasPendingSync,
            "storyId", storyManager != null ? storyManager.CurrentStoryId : "",
            "chapterId", storyManager != null ? storyManager.CurrentChapterId : "",
            "episodeId", storyManager != null ? storyManager.CurrentEpisodeId : "",
            "seasonIndex", storyManager != null ? storyManager.CurrentSeasonIndex : 0,
            "chapterIndex", storyManager != null ? storyManager.CurrentChapterIndex : 0,
            "dialogueLineIndex", storyManager != null ? storyManager.CurrentDialogueLineIndex : 0,
            "runtimeNodeGuid", currentNode != null ? currentNode.guid : "",
            "runtimeNodeName", currentNode != null ? currentNode.name : "",
            "runtimeNodeType", currentNode != null ? currentNode.GetType().Name : "",
            "gameStateStoryId", gameState != null ? gameState.CurrentStoryId : "",
            "hearts", PlayerData.Hearts,
            "candles", PlayerData.Candles);

        AddGameStateMetadata(metadata, gameState);
        AddRuntimeSaveMetadata(metadata, runtimeSave);

        if (!string.IsNullOrEmpty(managedMemoryError))
            metadata["managedMemoryError"] = managedMemoryError;
        if (!string.IsNullOrEmpty(sceneError))
            metadata["sceneError"] = sceneError;

        return metadata;
    }

    private static SaveData BuildRuntimeSaveSnapshot(StoryManager storyManager)
    {
        try
        {
            if (SaveManager.Instance == null || GameState.Instance == null)
                return null;

            return SaveManager.Instance.BuildCurrentSaveData(storyManager);
        }
        catch (Exception exception)
        {
            AppLogger.Warn(
                AppLogCategory.Diagnostics,
                nameof(NovelTemplateLoggingBootstrap),
                nameof(BuildRuntimeSaveSnapshot),
                "Runtime save snapshot for diagnostics failed.",
                LogMetadata.Of("error", FormatMetadataException(exception)),
                recoverable: true);
            return null;
        }
    }

    private static void AddGameStateMetadata(IDictionary<string, object> metadata, GameState gameState)
    {
        if (metadata == null || gameState == null)
            return;

        try
        {
            var owned = gameState.GetOwnedClothesSnapshot();
            var equipped = gameState.GetEquippedClothesSnapshot();
            var stats = gameState.GetStatsSnapshot();
            metadata["gameStateHistoryCount"] = gameState.history != null ? gameState.history.Count : 0;
            metadata["gameStateWardrobeCount"] = owned != null ? owned.Count : 0;
            metadata["gameStateEquippedCount"] = equipped != null ? equipped.Count : 0;
            metadata["gameStateStatsCount"] = stats != null ? stats.Count : 0;
            metadata["gameStateWardrobe"] = CompactStrings(owned);
            metadata["gameStateEquipped"] = CompactPairs(equipped);
            metadata["gameStateStats"] = CompactPairs(stats);
        }
        catch (Exception exception)
        {
            metadata["gameStateSnapshotError"] = FormatMetadataException(exception);
        }
    }

    private static void AddRuntimeSaveMetadata(IDictionary<string, object> metadata, SaveData save)
    {
        if (metadata == null || save == null)
            return;

        metadata["saveStoryId"] = save.storyId ?? "";
        metadata["saveChapterId"] = save.chapterId ?? "";
        metadata["saveEpisodeId"] = save.episodeId ?? "";
        metadata["saveGraphName"] = save.graphName ?? "";
        metadata["saveNodeGuid"] = save.currentNodeGuid ?? "";
        metadata["saveDialogueLineIndex"] = save.currentDialogueLineIndex;
        metadata["savePlayerName"] = save.playerName ?? "";
        metadata["saveAppearance"] = save.appearance;
        metadata["saveOutfit"] = save.heroOutfitId ?? "";
        metadata["saveHair"] = save.heroHairId ?? "";
        metadata["saveAccessory"] = save.heroAccessoryId ?? "";
        metadata["saveHistoryCount"] = save.history != null ? save.history.Count : 0;
        metadata["saveWardrobeCount"] = save.wardrobe != null ? save.wardrobe.Count : 0;
        metadata["saveEquippedCount"] = save.equippedClothes != null ? save.equippedClothes.Count : 0;
        metadata["saveStatsCount"] = save.statKeys != null ? save.statKeys.Count : 0;
        metadata["saveWardrobe"] = CompactStrings(save.wardrobe);
        metadata["saveEquipped"] = CompactPairs(save.equippedClothes);
    }

    private static string CompactStrings(IEnumerable<string> values, int limit = 12)
    {
        if (values == null)
            return "";

        var parts = new List<string>();
        int total = 0;
        foreach (string value in values)
        {
            total++;
            if (parts.Count < limit && !string.IsNullOrEmpty(value))
                parts.Add(value);
        }

        if (total > limit)
            parts.Add("+" + (total - limit).ToString() + " more");

        return string.Join(",", parts);
    }

    private static string CompactPairs(IEnumerable<StringPair> pairs, int limit = 12)
    {
        if (pairs == null)
            return "";

        var parts = new List<string>();
        int total = 0;
        foreach (StringPair pair in pairs)
        {
            if (pair == null)
                continue;

            total++;
            if (parts.Count < limit)
                parts.Add((pair.key ?? "") + "=" + (pair.value ?? ""));
        }

        if (total > limit)
            parts.Add("+" + (total - limit).ToString() + " more");

        return string.Join(",", parts);
    }

    private static string CompactPairs<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> pairs, int limit = 12)
    {
        if (pairs == null)
            return "";

        var parts = new List<string>();
        int total = 0;
        foreach (KeyValuePair<TKey, TValue> pair in pairs)
        {
            total++;
            if (parts.Count < limit)
                parts.Add(Convert.ToString(pair.Key) + "=" + Convert.ToString(pair.Value));
        }

        if (total > limit)
            parts.Add("+" + (total - limit).ToString() + " more");

        return string.Join(",", parts);
    }

    private static string FormatMetadataException(Exception exception)
    {
        return exception == null ? "unknown" : exception.GetType().Name + ": " + exception.Message;
    }
}

[DefaultExecutionOrder(-10000)]
internal sealed class NovelTemplateDiagnosticsRuntime : MonoBehaviour
{
    public static NovelTemplateDiagnosticsRuntime Instance { get; private set; }

    private float _nextHeartbeatAt;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ScheduleNextHeartbeat();

        AppLogger.Info(
            AppLogCategory.App,
            nameof(NovelTemplateDiagnosticsRuntime),
            "Awake",
            "Runtime diagnostics object started.",
            NovelTemplateLoggingBootstrap.BuildRuntimeMetadata());
    }

    private void Update()
    {
        if (!AppLogger.Settings.EnableDiagnostics)
            return;

        if (Time.realtimeSinceStartup < _nextHeartbeatAt)
            return;

        WriteHeartbeat();
        ScheduleNextHeartbeat();
    }

    private void OnApplicationPause(bool paused)
    {
        AppLogger.Info(
            AppLogCategory.App,
            nameof(NovelTemplateDiagnosticsRuntime),
            "ApplicationPause",
            paused ? "Application paused." : "Application resumed.",
            NovelTemplateLoggingBootstrap.BuildRuntimeMetadata());
    }

    private void OnApplicationQuit()
    {
        AppLogger.Info(
            AppLogCategory.App,
            nameof(NovelTemplateDiagnosticsRuntime),
            "ApplicationQuit",
            "Runtime diagnostics object is shutting down.",
            NovelTemplateLoggingBootstrap.BuildRuntimeMetadata());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void WriteHeartbeat()
    {
        AppLogger.Info(
            AppLogCategory.Diagnostics,
            nameof(NovelTemplateDiagnosticsRuntime),
            "Heartbeat",
            "Runtime heartbeat.",
            NovelTemplateLoggingBootstrap.BuildRuntimeMetadata());
    }

    private void ScheduleNextHeartbeat()
    {
        _nextHeartbeatAt = Time.realtimeSinceStartup + Mathf.Max(5f, AppLogger.Settings.DiagnosticsIntervalMs / 1000f);
    }
}
