using System;
using System.Collections.Generic;
using System.Diagnostics;

public static class AppDiagnostics
{
    public static long StartTimer()
    {
        return Stopwatch.GetTimestamp();
    }

    public static long ElapsedMilliseconds(long startedAt)
    {
        long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
        return (long)(elapsedTicks * 1000.0 / Stopwatch.Frequency);
    }

    public static void LogIfSlow(
        string category,
        string component,
        string operation,
        long durationMs,
        IDictionary<string, object> metadata = null,
        int? thresholdMs = null)
    {
        int threshold = thresholdMs.HasValue
            ? thresholdMs.Value
            : AppLogger.Settings.SlowOperationThresholdMs;

        if (durationMs < threshold)
            return;

        var safeMetadata = metadata != null
            ? new Dictionary<string, object>(metadata, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        safeMetadata["thresholdMs"] = threshold;
        safeMetadata["sourceCategory"] = category;

        AppLogger.Warn(
            AppLogCategory.Performance,
            component,
            operation,
            "Slow operation detected.",
            safeMetadata,
            durationMs,
            recoverable: true);
    }

    public static void LogOperationCompleted(
        string category,
        string component,
        string operation,
        string message,
        long startedAt,
        IDictionary<string, object> metadata = null)
    {
        long durationMs = ElapsedMilliseconds(startedAt);
        AppLogger.DebugLog(category, component, operation, message, metadata, durationMs);
        LogIfSlow(category, component, operation, durationMs, metadata);
    }

    public static void LogOperationFailed(
        string category,
        string component,
        string operation,
        string message,
        long startedAt,
        Exception exception,
        IDictionary<string, object> metadata = null,
        bool recoverable = true)
    {
        long durationMs = ElapsedMilliseconds(startedAt);
        AppLogger.Error(category, component, operation, message, exception, metadata, durationMs, recoverable: recoverable);
        LogIfSlow(category, component, operation, durationMs, metadata);
    }
}
