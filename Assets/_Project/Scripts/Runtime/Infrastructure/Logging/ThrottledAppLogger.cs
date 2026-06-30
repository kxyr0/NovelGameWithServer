using System;
using System.Collections.Generic;

public static class ThrottledAppLogger
{
    private sealed class State
    {
        public DateTime FirstUtc;
        public DateTime LastUtc;
        public DateTime LastWrittenUtc;
        public int RepeatCount;
        public string Category;
        public string Component;
        public string Operation;
        public string Message;
        public IDictionary<string, object> Metadata;
    }

    private static readonly object SyncRoot = new object();
    private static readonly Dictionary<string, State> States = new Dictionary<string, State>(StringComparer.Ordinal);

    public static void Debug(
        string key,
        string category,
        string component,
        string operation,
        string message,
        IDictionary<string, object> metadata = null,
        double summaryIntervalSeconds = 10d)
    {
        Write(AppLogLevel.Debug, key, category, component, operation, message, metadata, summaryIntervalSeconds);
    }

    public static void Warn(
        string key,
        string category,
        string component,
        string operation,
        string message,
        IDictionary<string, object> metadata = null,
        double summaryIntervalSeconds = 10d)
    {
        Write(AppLogLevel.Warn, key, category, component, operation, message, metadata, summaryIntervalSeconds);
    }

    public static void Error(
        string key,
        string category,
        string component,
        string operation,
        string message,
        Exception exception = null,
        IDictionary<string, object> metadata = null,
        double summaryIntervalSeconds = 10d)
    {
        string stableKey = NormalizeKey(key, category, component, operation, message);
        AppLogger.Error(category, component, operation, message, exception, metadata, recoverable: true);
        CountRepeat(stableKey, category, component, operation, message, metadata, summaryIntervalSeconds, AppLogLevel.Error);
    }

    private static void Write(
        AppLogLevel level,
        string key,
        string category,
        string component,
        string operation,
        string message,
        IDictionary<string, object> metadata,
        double summaryIntervalSeconds)
    {
        string stableKey = NormalizeKey(key, category, component, operation, message);
        bool writeNow = false;
        IDictionary<string, object> summary = null;

        lock (SyncRoot)
        {
            DateTime now = DateTime.UtcNow;
            if (!States.TryGetValue(stableKey, out State state))
            {
                state = new State
                {
                    FirstUtc = now,
                    LastUtc = now,
                    LastWrittenUtc = now,
                    RepeatCount = 0,
                    Category = category,
                    Component = component,
                    Operation = operation,
                    Message = message,
                    Metadata = metadata
                };
                States[stableKey] = state;
                writeNow = true;
            }
            else
            {
                state.LastUtc = now;
                state.RepeatCount++;
                state.Category = category;
                state.Component = component;
                state.Operation = operation;
                state.Message = message;
                state.Metadata = metadata;

                if ((now - state.LastWrittenUtc).TotalSeconds >= Math.Max(1d, summaryIntervalSeconds))
                {
                    summary = BuildSummaryMetadata(state, metadata);
                    state.LastWrittenUtc = now;
                    state.FirstUtc = now;
                    state.RepeatCount = 0;
                }
            }
        }

        if (writeNow)
        {
            WriteToAppLogger(level, category, component, operation, message, metadata);
        }
        else if (summary != null)
        {
            WriteToAppLogger(level, category, component, operation, message + " Repeated messages were collapsed into this summary.", summary);
        }
    }

    private static void CountRepeat(
        string stableKey,
        string category,
        string component,
        string operation,
        string message,
        IDictionary<string, object> metadata,
        double summaryIntervalSeconds,
        AppLogLevel level)
    {
        IDictionary<string, object> summary = null;
        lock (SyncRoot)
        {
            DateTime now = DateTime.UtcNow;
            if (!States.TryGetValue(stableKey, out State state))
            {
                States[stableKey] = new State
                {
                    FirstUtc = now,
                    LastUtc = now,
                    LastWrittenUtc = now,
                    Category = category,
                    Component = component,
                    Operation = operation,
                    Message = message,
                    Metadata = metadata
                };
                return;
            }

            state.LastUtc = now;
            state.RepeatCount++;
            state.Metadata = metadata;
            if ((now - state.LastWrittenUtc).TotalSeconds >= Math.Max(1d, summaryIntervalSeconds))
            {
                summary = BuildSummaryMetadata(state, metadata);
                state.LastWrittenUtc = now;
                state.FirstUtc = now;
                state.RepeatCount = 0;
            }
        }

        if (summary != null)
            WriteToAppLogger(level, category, component, operation, message + " Repeated messages were collapsed into this summary.", summary);
    }

    private static IDictionary<string, object> BuildSummaryMetadata(State state, IDictionary<string, object> latestMetadata)
    {
        var summary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            { "repeatCount", state.RepeatCount },
            { "firstUtc", state.FirstUtc.ToString("o") },
            { "lastUtc", state.LastUtc.ToString("o") },
            { "latestComponent", state.Component },
            { "latestOperation", state.Operation }
        };

        if (latestMetadata != null)
        {
            foreach (var pair in latestMetadata)
                summary[pair.Key] = pair.Value;
        }

        return summary;
    }

    private static void WriteToAppLogger(
        AppLogLevel level,
        string category,
        string component,
        string operation,
        string message,
        IDictionary<string, object> metadata)
    {
        switch (level)
        {
            case AppLogLevel.Trace:
                AppLogger.Trace(category, component, operation, message, metadata);
                break;
            case AppLogLevel.Debug:
                AppLogger.DebugLog(category, component, operation, message, metadata);
                break;
            case AppLogLevel.Warn:
                AppLogger.Warn(category, component, operation, message, metadata, recoverable: true);
                break;
            case AppLogLevel.Error:
            case AppLogLevel.Fatal:
                AppLogger.Error(category, component, operation, message, null, metadata, recoverable: true);
                break;
            default:
                AppLogger.Info(category, component, operation, message, metadata);
                break;
        }
    }

    private static string NormalizeKey(string key, string category, string component, string operation, string message)
    {
        if (!string.IsNullOrWhiteSpace(key))
            return key.Trim();

        return (category ?? "") + ":" + (component ?? "") + ":" + (operation ?? "") + ":" + (message ?? "");
    }
}
