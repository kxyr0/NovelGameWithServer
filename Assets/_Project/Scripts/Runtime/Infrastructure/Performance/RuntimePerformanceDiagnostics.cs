using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

public sealed class RuntimeTextureLoadScope : IDisposable
{
    readonly string _key;
    readonly long _startedAt;
    readonly bool _duplicate;
    bool _completed;

    internal RuntimeTextureLoadScope(string key, bool duplicate)
    {
        _key = string.IsNullOrWhiteSpace(key) ? "unknown" : key;
        _duplicate = duplicate;
        _startedAt = AppDiagnostics.StartTimer();
    }

    public bool IsDuplicate => _duplicate;

    public void Complete(bool success, string detail = "")
    {
        if (_completed)
            return;

        _completed = true;
        RuntimePerformanceDiagnostics.RecordTextureLoadCompleted(
            _key,
            AppDiagnostics.ElapsedMilliseconds(_startedAt),
            success,
            _duplicate,
            detail);
    }

    public void Dispose()
    {
        Complete(false, "disposed");
    }
}

public sealed class RuntimePerformanceSnapshot
{
    public float CurrentFps;
    public float AverageFps;
    public float MinFps;
    public float FrameMilliseconds;
    public long ManagedMemoryBytes;
    public long TotalAllocatedMemoryBytes;
    public long GcAllocatedInFrameBytes;
    public string SceneName;
    public string DeviceModel;
}

[DefaultExecutionOrder(-9000)]
public sealed class RuntimePerformanceDiagnostics : MonoBehaviour
{
    public const string DiagnosticsPlayerPrefsKey = "VN_DEBUG_RUNTIME_DIAGNOSTICS";
    public const string FpsOverlayPlayerPrefsKey = "VN_DEBUG_FPS_OVERLAY";

    static readonly ProfilerMarker TextureLoadMarker = new ProfilerMarker("NovelTemplate.TextureLoad");
    static readonly ProfilerMarker AsyncOperationMarker = new ProfilerMarker("NovelTemplate.AsyncOperation");
    static readonly object SyncRoot = new object();
    static readonly HashSet<string> InFlightTextureLoads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, int> TextureLoadCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, long> TextureLoadDurations = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    public static RuntimePerformanceDiagnostics Instance { get; private set; }

    readonly Queue<string> _events = new Queue<string>();
    readonly StringBuilder _builder = new StringBuilder(2048);

    ProfilerRecorder _gcAllocatedRecorder;
    RuntimeFpsOverlay _overlay;
    RuntimePerformanceSnapshot _snapshot = new RuntimePerformanceSnapshot();
    float _sampleElapsed;
    float _reportElapsed;
    float _avgFpsAccumulator;
    float _minFps = float.MaxValue;
    int _sampleFrames;
    int _totalFrames;
    int _longFrameCount;
    long _lastManagedMemoryBytes;
    long _lastSceneLoadStartedAt;
    string _lastSceneName = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Instance = null;
        lock (SyncRoot)
        {
            InFlightTextureLoads.Clear();
            TextureLoadCounts.Clear();
            TextureLoadDurations.Clear();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeBeforeSceneLoad()
    {
        EnsureInstance();
    }

    public static bool DiagnosticsEnabled
    {
        get
        {
            return Debug.isDebugBuild ||
                   Application.isEditor ||
                   PlayerPrefs.GetInt(DiagnosticsPlayerPrefsKey, 0) == 1;
        }
    }

    public static bool FpsOverlayEnabled => PlayerPrefs.GetInt(FpsOverlayPlayerPrefsKey, 0) == 1;

    public static RuntimePerformanceSnapshot Snapshot
    {
        get
        {
            EnsureInstance();
            return Instance != null ? Instance._snapshot : new RuntimePerformanceSnapshot();
        }
    }

    public static void SetDiagnosticsEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(DiagnosticsPlayerPrefsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        if (Application.isPlaying)
            EnsureInstance();
    }

    public static void SetFpsOverlayEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(FpsOverlayPlayerPrefsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        if (Application.isPlaying)
            EnsureInstance();
        if (Instance != null)
            Instance.ApplyOverlayState();
    }

    public static RuntimeTextureLoadScope BeginTextureLoad(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            key = "unknown";

        bool duplicate = false;
        lock (SyncRoot)
        {
            duplicate = InFlightTextureLoads.Contains(key);
            InFlightTextureLoads.Add(key);
        }

        if (duplicate)
            AddEvent("duplicate texture request: " + key);

        return new RuntimeTextureLoadScope(key, duplicate);
    }

    public static void RecordTextureLoadCompleted(
        string key,
        long durationMs,
        bool success,
        bool duplicate,
        string detail = "")
    {
        if (string.IsNullOrWhiteSpace(key))
            key = "unknown";

        TextureLoadMarker.Begin();
        try
        {
            lock (SyncRoot)
            {
                InFlightTextureLoads.Remove(key);
                TextureLoadCounts.TryGetValue(key, out int count);
                TextureLoadCounts[key] = count + 1;
                TextureLoadDurations.TryGetValue(key, out long totalMs);
                TextureLoadDurations[key] = totalMs + Math.Max(0L, durationMs);
            }
        }
        finally
        {
            TextureLoadMarker.End();
        }

        if (!DiagnosticsEnabled)
            return;

        if (!Application.isPlaying && Instance == null)
            return;

        EnsureInstance();
        AddEvent(
            "texture " + (success ? "loaded" : "failed") +
            " key=" + key +
            " ms=" + durationMs +
            " duplicate=" + duplicate +
            (string.IsNullOrWhiteSpace(detail) ? "" : " detail=" + detail));

        AppDiagnostics.LogIfSlow(
            AppLogCategory.Performance,
            nameof(RuntimePerformanceDiagnostics),
            "TextureLoad",
            durationMs,
            LogMetadata.Of("key", key, "success", success, "duplicate", duplicate, "detail", detail),
            120);
    }

    public static void TrackAsyncOperation(string label, AsyncOperation operation)
    {
        if (operation == null)
            return;

        if (!Application.isPlaying)
            return;

        EnsureInstance();
        if (Instance != null)
            Instance.StartCoroutine(Instance.TrackAsyncOperationRoutine(label, operation));
    }

    public static string DumpReport(bool writeFile = true)
    {
        if (Instance == null && Application.isPlaying)
            EnsureInstance();
        string report = Instance != null ? Instance.BuildReport() : "Runtime performance diagnostics are not available.";

        if (writeFile)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "runtime-performance-report.txt");
                File.WriteAllText(path, report);
                AppLogger.Info(
                    AppLogCategory.Performance,
                    nameof(RuntimePerformanceDiagnostics),
                    nameof(DumpReport),
                    "Runtime performance report dumped.",
                    LogMetadata.Of("path", path));
            }
            catch (Exception exception)
            {
                AppLogger.Warn(
                    AppLogCategory.Performance,
                    nameof(RuntimePerformanceDiagnostics),
                    nameof(DumpReport),
                    "Failed to write runtime performance report.",
                    LogMetadata.Of("exceptionType", exception.GetType().Name),
                    recoverable: true);
            }
        }

        Debug.Log(report);
        return report;
    }

    public static void AddEvent(string message)
    {
        if (!DiagnosticsEnabled)
            return;

        if (!Application.isPlaying && Instance == null)
            return;

        EnsureInstance();
        if (Instance != null)
            Instance.EnqueueEvent(message);
    }

    static void EnsureInstance()
    {
        if (Instance != null)
            return;

        GameObject gameObject = new GameObject(nameof(RuntimePerformanceDiagnostics));
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        gameObject.AddComponent<RuntimePerformanceDiagnostics>();
        DontDestroyOnLoad(gameObject);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        TryStartRecorders();
        ApplyOverlayState();
        _lastSceneName = SceneManager.GetActiveScene().name;
        _lastSceneLoadStartedAt = AppDiagnostics.StartTimer();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        DisposeRecorder(ref _gcAllocatedRecorder);
    }

    void Update()
    {
        UpdateFrameStats();

        if (Input.GetKeyDown(KeyCode.F3))
            SetFpsOverlayEnabled(!FpsOverlayEnabled);

        if (Input.GetKeyDown(KeyCode.F4))
            DumpReport(true);

        if (!DiagnosticsEnabled)
            return;

        _reportElapsed += Time.unscaledDeltaTime;
        if (_reportElapsed >= 30f)
        {
            _reportElapsed = 0f;
            AppLogger.DebugLog(
                AppLogCategory.Performance,
                nameof(RuntimePerformanceDiagnostics),
                "RollingReport",
                "Runtime performance rolling report.",
                BuildReportMetadata());
        }
    }

    void UpdateFrameStats()
    {
        float delta = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float fps = 1f / delta;
        float frameMs = delta * 1000f;

        _sampleElapsed += delta;
        _sampleFrames++;
        _totalFrames++;
        _avgFpsAccumulator += fps;
        _minFps = Mathf.Min(_minFps, fps);

        if (frameMs >= 50f)
        {
            _longFrameCount++;
            AddEvent("long frame ms=" + frameMs.ToString("0.0"));
        }

        if (_sampleElapsed < 0.25f)
            return;

        _lastManagedMemoryBytes = GC.GetTotalMemory(false);
        _snapshot.CurrentFps = fps;
        _snapshot.AverageFps = _totalFrames > 0 ? _avgFpsAccumulator / _totalFrames : fps;
        _snapshot.MinFps = _minFps < float.MaxValue ? _minFps : fps;
        _snapshot.FrameMilliseconds = frameMs;
        _snapshot.ManagedMemoryBytes = _lastManagedMemoryBytes;
        _snapshot.TotalAllocatedMemoryBytes = Profiler.GetTotalAllocatedMemoryLong();
        _snapshot.GcAllocatedInFrameBytes = _gcAllocatedRecorder.Valid ? (long)_gcAllocatedRecorder.LastValue : 0L;
        _snapshot.SceneName = SceneManager.GetActiveScene().name;
        _snapshot.DeviceModel = SystemInfo.deviceModel;

        _sampleElapsed = 0f;
        _sampleFrames = 0;
    }

    void ApplyOverlayState()
    {
        if (!FpsOverlayEnabled)
        {
            if (_overlay != null)
                _overlay.gameObject.SetActive(false);
            return;
        }

        if (_overlay == null)
        {
            GameObject overlayObject = new GameObject(nameof(RuntimeFpsOverlay));
            overlayObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(overlayObject);
            _overlay = overlayObject.AddComponent<RuntimeFpsOverlay>();
        }

        _overlay.gameObject.SetActive(true);
    }

    void TryStartRecorders()
    {
        DisposeRecorder(ref _gcAllocatedRecorder);
        try
        {
            _gcAllocatedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }
        catch (Exception)
        {
            _gcAllocatedRecorder = default;
        }
    }

    static void DisposeRecorder(ref ProfilerRecorder recorder)
    {
        if (!recorder.Valid)
            return;

        recorder.Dispose();
        recorder = default;
    }

    void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        _lastSceneLoadStartedAt = AppDiagnostics.StartTimer();
        _lastSceneName = newScene.name;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        long durationMs = AppDiagnostics.ElapsedMilliseconds(_lastSceneLoadStartedAt);
        AddEvent("scene loaded name=" + scene.name + " mode=" + mode + " ms=" + durationMs);
        AppDiagnostics.LogIfSlow(
            AppLogCategory.Performance,
            nameof(RuntimePerformanceDiagnostics),
            "SceneLoaded",
            durationMs,
            LogMetadata.Of("scene", scene.name, "mode", mode.ToString(), "previousScene", _lastSceneName),
            250);
    }

    System.Collections.IEnumerator TrackAsyncOperationRoutine(string label, AsyncOperation operation)
    {
        string safeLabel = string.IsNullOrWhiteSpace(label) ? "async-operation" : label;
        long startedAt = AppDiagnostics.StartTimer();

        while (operation != null && !operation.isDone)
            yield return null;

        AsyncOperationMarker.Begin();
        try
        {
            long durationMs = AppDiagnostics.ElapsedMilliseconds(startedAt);
            AddEvent("async completed label=" + safeLabel + " ms=" + durationMs);
            AppDiagnostics.LogIfSlow(
                AppLogCategory.Performance,
                nameof(RuntimePerformanceDiagnostics),
                "AsyncOperation",
                durationMs,
                LogMetadata.Of("label", safeLabel),
                250);
        }
        finally
        {
            AsyncOperationMarker.End();
        }
    }

    void EnqueueEvent(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _events.Enqueue(DateTime.UtcNow.ToString("O") + " " + message);
        while (_events.Count > 64)
            _events.Dequeue();
    }

    IDictionary<string, object> BuildReportMetadata()
    {
        return LogMetadata.Of(
            "fps", _snapshot.CurrentFps,
            "avgFps", _snapshot.AverageFps,
            "minFps", _snapshot.MinFps,
            "frameMs", _snapshot.FrameMilliseconds,
            "managedMemoryBytes", _snapshot.ManagedMemoryBytes,
            "allocatedMemoryBytes", _snapshot.TotalAllocatedMemoryBytes,
            "gcAllocatedInFrameBytes", _snapshot.GcAllocatedInFrameBytes,
            "longFrameCount", _longFrameCount,
            "scene", _snapshot.SceneName,
            "device", _snapshot.DeviceModel);
    }

    string BuildReport()
    {
        _builder.Length = 0;
        _builder.AppendLine("Runtime performance report");
        _builder.AppendLine("scene=" + _snapshot.SceneName);
        _builder.AppendLine("device=" + _snapshot.DeviceModel);
        _builder.AppendLine("fps=" + _snapshot.CurrentFps.ToString("0.0"));
        _builder.AppendLine("avgFps=" + _snapshot.AverageFps.ToString("0.0"));
        _builder.AppendLine("minFps=" + _snapshot.MinFps.ToString("0.0"));
        _builder.AppendLine("frameMs=" + _snapshot.FrameMilliseconds.ToString("0.0"));
        _builder.AppendLine("managedMemoryBytes=" + _snapshot.ManagedMemoryBytes);
        _builder.AppendLine("allocatedMemoryBytes=" + _snapshot.TotalAllocatedMemoryBytes);
        _builder.AppendLine("gcAllocatedInFrameBytes=" + _snapshot.GcAllocatedInFrameBytes);
        _builder.AppendLine("longFrameCount=" + _longFrameCount);

        lock (SyncRoot)
        {
            _builder.AppendLine("textureLoads=" + TextureLoadCounts.Count);
            foreach (KeyValuePair<string, int> entry in TextureLoadCounts)
            {
                TextureLoadDurations.TryGetValue(entry.Key, out long durationMs);
                _builder.AppendLine("texture key=" + entry.Key + " count=" + entry.Value + " totalMs=" + durationMs);
            }
        }

        _builder.AppendLine("events=");
        foreach (string item in _events)
            _builder.AppendLine(item);

        return _builder.ToString();
    }
}
