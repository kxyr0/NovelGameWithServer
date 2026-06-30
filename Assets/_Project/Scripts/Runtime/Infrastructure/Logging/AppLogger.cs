using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public enum AppLogLevel
{
    Trace = 0,
    Debug = 10,
    Info = 20,
    Warn = 30,
    Error = 40,
    Fatal = 50
}

public static class AppLogCategory
{
    public const string App = "app";
    public const string Error = "error";
    public const string Server = "server";
    public const string Storage = "storage";
    public const string Network = "network";
    public const string Auth = "auth";
    public const string Security = "security";
    public const string ApiContract = "api-contract";
    public const string Performance = "performance";
    public const string Diagnostics = "diagnostics";
    public const string StoryFlow = "story-flow";
    public const string StoryUi = "story-ui";
    public const string ScreenNavigation = "screen-navigation";
    public const string Menu = "menu";
    public const string Wardrobe = "wardrobe";
    public const string Shop = "shop";
    public const string EndScreen = "end-screen";
    public const string PhoneDialogue = "phone-dialogue";
    public const string Dotween = "dotween";
    public const string SaveSystem = "save-system";
    public const string Editor = "editor";
    public const string Layout = "layout";
    public const string Ads = "ads";
    public const string StorySeasonReward = "story-season-reward";
    public const string StoryProgression = "story-progression";
    public const string CurrencyReward = "currency-reward";
    public const string RewardSave = "reward-save";
}

public sealed class AppLoggerSettings
{
    public AppLogLevel LogLevel = Debug.isDebugBuild ? AppLogLevel.Debug : AppLogLevel.Info;
    public string LogDirectory = "";
    public bool LogToConsole = false;
    public bool LogToFile = true;
    public long MaxFileSizeBytes = 5L * 1024L * 1024L;
    public int RetentionDays = 14;
    public bool EnableDiagnostics = true;
    public int DiagnosticsIntervalMs = 60000;
    public int SlowOperationThresholdMs = 500;
}

public static class LogMetadata
{
    public static IDictionary<string, object> Of(params object[] keyValues)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (keyValues == null)
            return metadata;

        for (int i = 0; i + 1 < keyValues.Length; i += 2)
        {
            string key = keyValues[i] != null ? keyValues[i].ToString() : "";
            if (string.IsNullOrWhiteSpace(key))
                continue;

            metadata[key] = keyValues[i + 1];
        }

        return metadata;
    }
}

public static class AppLogger
{
    private const int MaxMessageChars = 4096;
    private const int MaxMetadataValueChars = 1024;
    private const int MaxStackChars = 8192;

    private static readonly object SyncRoot = new object();
    private static readonly Regex BearerTokenRegex = new Regex(
        @"(?i)\bBearer\s+[A-Za-z0-9\-._~+/]+=*",
        RegexOptions.Compiled);
    private static readonly Regex SensitiveAssignmentRegex = new Regex(
        @"(?i)([""']?\b(password|token|accessToken|authToken|refreshToken|idToken|restoreCode|secret|apiKey|adminKey|x-admin-key|authorization|cookie|privateKey|session|jwt|purchaseToken|restoreToken|receipt|signature)\b[""']?\s*[:=]\s*)(""[^""]*""|'[^']*'|[^\s,;}\]]+)",
        RegexOptions.Compiled);

    private static AppLoggerSettings _settings;
    private static bool _initialized;
    private static bool _fileLoggingAvailable;
    private static DateTime _lastRetentionCheckUtc = DateTime.MinValue;

    public static AppLoggerSettings Settings
    {
        get
        {
            EnsureInitialized();
            return _settings;
        }
    }

    public static void Initialize()
    {
        EnsureInitialized();
    }

    public static void DebugLog(
        string category,
        string component,
        string operation,
        string message,
        IDictionary<string, object> metadata = null,
        long? durationMs = null,
        string correlationId = null)
    {
        Write(AppLogLevel.Debug, category, component, operation, message, null, metadata, durationMs, correlationId, null);
    }

    public static void Trace(
        string category,
        string component,
        string operation,
        string message,
        IDictionary<string, object> metadata = null,
        long? durationMs = null,
        string correlationId = null)
    {
        Write(AppLogLevel.Trace, category, component, operation, message, null, metadata, durationMs, correlationId, null);
    }

    public static void Info(
        string category,
        string component,
        string operation,
        string message,
        IDictionary<string, object> metadata = null,
        long? durationMs = null,
        string correlationId = null)
    {
        Write(AppLogLevel.Info, category, component, operation, message, null, metadata, durationMs, correlationId, null);
    }

    public static void Warn(
        string category,
        string component,
        string operation,
        string message,
        IDictionary<string, object> metadata = null,
        long? durationMs = null,
        string correlationId = null,
        bool? recoverable = true)
    {
        Write(AppLogLevel.Warn, category, component, operation, message, null, metadata, durationMs, correlationId, recoverable);
    }

    public static void Error(
        string category,
        string component,
        string operation,
        string message,
        Exception exception = null,
        IDictionary<string, object> metadata = null,
        long? durationMs = null,
        string correlationId = null,
        bool? recoverable = true)
    {
        Write(AppLogLevel.Error, category, component, operation, message, exception, metadata, durationMs, correlationId, recoverable);
    }

    public static void Fatal(
        string category,
        string component,
        string operation,
        string message,
        Exception exception = null,
        IDictionary<string, object> metadata = null,
        long? durationMs = null,
        string correlationId = null)
    {
        Write(AppLogLevel.Fatal, category, component, operation, message, exception, metadata, durationMs, correlationId, false);
    }

    public static void UnityLog(LogType type, string condition, string stackTrace)
    {
        AppLogLevel level = MapUnityLevel(type);
        string category = ResolveUnityCategory(condition, stackTrace);
        string component = ResolveUnityComponent(condition, stackTrace);
        Exception exception = null;

        if (type == LogType.Exception)
            exception = new Exception(Trim(condition, MaxMessageChars));

        Write(
            level,
            category,
            component,
            "UnityLog",
            condition,
            exception,
            LogMetadata.Of("unityLogType", type.ToString()),
            null,
            null,
            level < AppLogLevel.Error,
            stackTrace);
    }

    private static void Write(
        AppLogLevel level,
        string category,
        string component,
        string operation,
        string message,
        Exception exception,
        IDictionary<string, object> metadata,
        long? durationMs,
        string correlationId,
        bool? recoverable,
        string stackTraceOverride = null)
    {
        EnsureInitialized();

        if (level < _settings.LogLevel)
            return;

        category = NormalizeCategory(category);
        string line = FormatLine(level, category, component, operation, message, exception, metadata, durationMs, correlationId, recoverable, stackTraceOverride);

        if (_settings.LogToFile && _fileLoggingAvailable)
        {
            try
            {
                WriteLineToCategoryFile(category, line);
                if (level >= AppLogLevel.Error && category != AppLogCategory.Error)
                    WriteLineToCategoryFile(AppLogCategory.Error, line);
            }
            catch (Exception fileException)
            {
                _fileLoggingAvailable = false;
                WriteFallback("Logging file output failed: " + fileException.Message);
                WriteFallback(line);
            }
        }

        if (_settings.LogToConsole)
            WriteFallback(line);
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
            return;

        lock (SyncRoot)
        {
            if (_initialized)
                return;

            _settings = LoadSettings();
            _fileLoggingAvailable = false;

            if (_settings.LogToFile)
            {
                try
                {
                    Directory.CreateDirectory(_settings.LogDirectory);
                    _fileLoggingAvailable = true;
                    ApplyRetention();
                }
                catch (Exception exception)
                {
                    WriteFallback("Logging initialization failed: " + exception.Message);
                }
            }

            _initialized = true;
        }
    }

    private static AppLoggerSettings LoadSettings()
    {
        var settings = new AppLoggerSettings();
        settings.LogLevel = ParseLevel(GetEnvironment("LOG_LEVEL"), settings.LogLevel);
        settings.LogDirectory = ResolveLogDirectory(GetEnvironment("LOG_DIR"));
        settings.LogToConsole = ParseBool(GetEnvironment("LOG_TO_CONSOLE"), settings.LogToConsole);
        settings.LogToFile = ParseBool(GetEnvironment("LOG_TO_FILE"), settings.LogToFile);
        settings.MaxFileSizeBytes = ParseSize(GetEnvironment("LOG_MAX_FILE_SIZE"), settings.MaxFileSizeBytes);
        settings.RetentionDays = ParseInt(GetEnvironment("LOG_RETENTION_DAYS"), settings.RetentionDays, 0, 3650);
        settings.EnableDiagnostics = ParseBool(GetEnvironment("LOG_ENABLE_DIAGNOSTICS"), settings.EnableDiagnostics);
        settings.DiagnosticsIntervalMs = ParseInt(GetEnvironment("LOG_DIAGNOSTICS_INTERVAL_MS"), settings.DiagnosticsIntervalMs, 5000, 86400000);
        settings.SlowOperationThresholdMs = ParseInt(GetEnvironment("LOG_SLOW_OPERATION_MS"), settings.SlowOperationThresholdMs, 1, 600000);
        return settings;
    }

    private static string ResolveLogDirectory(string configuredDirectory)
    {
        string requested = string.IsNullOrWhiteSpace(configuredDirectory)
            ? "logs"
            : configuredDirectory.Trim();

        if (Path.IsPathRooted(requested))
            return Path.GetFullPath(requested);

#if UNITY_EDITOR
        string root = ResolveProjectRoot();
#else
        string root = Application.persistentDataPath;
#endif
        return Path.GetFullPath(Path.Combine(root, requested));
    }

    private static string ResolveProjectRoot()
    {
        try
        {
            string dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
            {
                var info = new DirectoryInfo(dataPath);
                if (string.Equals(info.Name, "Assets", StringComparison.OrdinalIgnoreCase) && info.Parent != null)
                    return info.Parent.FullName;
            }
        }
        catch (Exception exception)
        {
            WriteFallback("Logging project root resolution via Application.dataPath failed: " + exception.Message);
        }

        try
        {
            return Directory.GetCurrentDirectory();
        }
        catch (Exception exception)
        {
            WriteFallback("Logging project root resolution via current directory failed: " + exception.Message);
            return Application.persistentDataPath;
        }
    }

    private static void WriteLineToCategoryFile(string category, string line)
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(_settings.LogDirectory);
            ApplyRetentionIfNeeded();

            string path = Path.Combine(_settings.LogDirectory, SanitizeFilePart(category) + ".log");
            RotateIfNeeded(path);
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    private static void RotateIfNeeded(string path)
    {
        if (_settings.MaxFileSizeBytes <= 0 || !File.Exists(path))
            return;

        var info = new FileInfo(path);
        if (info.Length < _settings.MaxFileSizeBytes)
            return;

        string baseName = Path.GetFileNameWithoutExtension(path);
        string rotatedName = baseName + "." + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff", CultureInfo.InvariantCulture) + ".log";
        string rotatedPath = Path.Combine(Path.GetDirectoryName(path) ?? _settings.LogDirectory, rotatedName);
        File.Move(path, rotatedPath);
    }

    private static void ApplyRetentionIfNeeded()
    {
        if (_settings.RetentionDays <= 0)
            return;

        DateTime now = DateTime.UtcNow;
        if ((now - _lastRetentionCheckUtc).TotalMinutes < 60)
            return;

        ApplyRetention();
    }

    private static void ApplyRetention()
    {
        _lastRetentionCheckUtc = DateTime.UtcNow;
        if (_settings.RetentionDays <= 0 || string.IsNullOrEmpty(_settings.LogDirectory) || !Directory.Exists(_settings.LogDirectory))
            return;

        DateTime cutoffUtc = DateTime.UtcNow.AddDays(-_settings.RetentionDays);
        foreach (string file in Directory.GetFiles(_settings.LogDirectory, "*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoffUtc)
                    File.Delete(file);
            }
            catch (Exception exception)
            {
                WriteFallback("Logging retention cleanup failed for '" + file + "': " + exception.Message);
            }
        }
    }

    private static string FormatLine(
        AppLogLevel level,
        string category,
        string component,
        string operation,
        string message,
        Exception exception,
        IDictionary<string, object> metadata,
        long? durationMs,
        string correlationId,
        bool? recoverable,
        string stackTraceOverride)
    {
        var builder = new StringBuilder(512);
        builder.Append('[').Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append("] ");
        builder.Append('[').Append(LevelName(level)).Append("] ");
        builder.Append('[').Append(category).Append("] ");
        builder.Append('[').Append(SafeToken(component, "unknown")).Append("] ");
        builder.Append('[').Append(SafeToken(operation, "unknown")).Append(']');

        if (!string.IsNullOrWhiteSpace(correlationId))
            builder.Append(" correlationId=").Append(Quote(correlationId));

        if (durationMs.HasValue)
            builder.Append(" durationMs=").Append(durationMs.Value);

        if (recoverable.HasValue)
            builder.Append(" recoverable=").Append(recoverable.Value ? "true" : "false");

        builder.Append(" message=").Append(Quote(message));

        if (exception != null)
        {
            builder.Append(" error=").Append(Quote(exception.GetType().Name + ": " + exception.Message));
            builder.Append(" stack=").Append(Quote(
                !string.IsNullOrWhiteSpace(stackTraceOverride) ? stackTraceOverride : exception.ToString(),
                MaxStackChars));
        }
        else if (!string.IsNullOrWhiteSpace(stackTraceOverride))
        {
            builder.Append(" stack=").Append(Quote(stackTraceOverride, MaxStackChars));
        }

        string metadataText = FormatMetadata(metadata);
        if (!string.IsNullOrEmpty(metadataText))
            builder.Append(" metadata=").Append(Quote(metadataText, MaxMessageChars));

        return builder.ToString();
    }

    private static string FormatMetadata(IDictionary<string, object> metadata)
    {
        if (metadata == null || metadata.Count == 0)
            return "";

        var builder = new StringBuilder();
        foreach (var pair in metadata)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            if (builder.Length > 0)
                builder.Append(' ');

            builder.Append(SafeToken(pair.Key, "key"));
            builder.Append('=');
            builder.Append(Quote(FormatMetadataValue(pair.Key, pair.Value), MaxMetadataValueChars));
        }

        return builder.ToString();
    }

    private static string FormatMetadataValue(string key, object value)
    {
        if (IsSensitiveKey(key))
            return "[REDACTED]";

        if (value == null)
            return "null";

        string text;
        if (value is bool)
            text = (bool)value ? "true" : "false";
        else if (value is IFormattable formattable)
            text = formattable.ToString(null, CultureInfo.InvariantCulture);
        else
            text = value.ToString();

        return RedactText(Trim(text, MaxMetadataValueChars));
    }

    private static string Quote(string value, int maxChars = MaxMessageChars)
    {
        value = RedactText(Trim(value, maxChars));
        if (value == null)
            value = "";

        return "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n") + "\"";
    }

    private static string RedactText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? "";

        string result = BearerTokenRegex.Replace(value, "Bearer [REDACTED]");
        result = SensitiveAssignmentRegex.Replace(result, "$1[REDACTED]");
        return result;
    }

    private static bool IsSensitiveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        string lower = key.ToLowerInvariant();
        return lower.Contains("password") ||
               lower.Contains("token") ||
               lower.Contains("accesstoken") ||
               lower.Contains("refreshtoken") ||
               lower.Contains("restorecode") ||
               lower.Contains("secret") ||
               lower.Contains("apikey") ||
               lower.Contains("adminkey") ||
               lower.Contains("x-admin-key") ||
               lower.Contains("authorization") ||
               lower.Contains("cookie") ||
               lower.Contains("receipt") ||
               lower.Contains("signature") ||
               lower.Contains("privatekey") ||
               lower.Contains("session") ||
               lower.Contains("jwt");
    }

    private static string NormalizeCategory(string category)
    {
        category = SafeToken(category, AppLogCategory.App).ToLowerInvariant();
        switch (category)
        {
            case AppLogCategory.App:
            case AppLogCategory.Error:
            case AppLogCategory.Server:
            case AppLogCategory.Storage:
            case AppLogCategory.Network:
            case AppLogCategory.Auth:
            case AppLogCategory.Security:
            case AppLogCategory.ApiContract:
            case AppLogCategory.Performance:
            case AppLogCategory.Diagnostics:
            case AppLogCategory.StoryFlow:
            case AppLogCategory.StoryUi:
            case AppLogCategory.ScreenNavigation:
            case AppLogCategory.Menu:
            case AppLogCategory.Wardrobe:
            case AppLogCategory.Shop:
            case AppLogCategory.EndScreen:
            case AppLogCategory.PhoneDialogue:
            case AppLogCategory.Dotween:
            case AppLogCategory.SaveSystem:
            case AppLogCategory.Editor:
            case AppLogCategory.Layout:
            case AppLogCategory.Ads:
            case AppLogCategory.StorySeasonReward:
            case AppLogCategory.StoryProgression:
            case AppLogCategory.CurrencyReward:
            case AppLogCategory.RewardSave:
                return category;
            default:
                return AppLogCategory.App;
        }
    }

    private static string ResolveUnityCategory(string condition, string stackTrace)
    {
        string text = ((condition ?? "") + "\n" + (stackTrace ?? "")).ToLowerInvariant();
        if (text.Contains("auth") || text.Contains("login"))
            return AppLogCategory.Auth;
        if (text.Contains("api contract") || text.Contains("apicontract"))
            return AppLogCategory.ApiContract;
        if (text.Contains("baseurl") || text.Contains("server") || text.Contains("connection"))
            return AppLogCategory.Server;
        if (text.Contains("[net]") || text.Contains("network") || text.Contains("unitywebrequest") || text.Contains("http"))
            return AppLogCategory.Network;
        if (text.Contains("dotween") || text.Contains("tween"))
            return AppLogCategory.Dotween;
        if (text.Contains("wardrobe") || text.Contains("clothing"))
            return AppLogCategory.Wardrobe;
        if (text.Contains("screen navigator") || text.Contains("screen id") || text.Contains("uiscreen"))
            return AppLogCategory.ScreenNavigation;
        if (text.Contains("[shop]") || text.Contains("[iap]") || text.Contains("purchase"))
            return AppLogCategory.Shop;
        if (text.Contains("storymanager") || text.Contains("story flow") || text.Contains("currentnode"))
            return AppLogCategory.StoryFlow;
        if (text.Contains("menucontroller") || text.Contains("history screen"))
            return AppLogCategory.Menu;
        if (text.Contains("endscreen") || text.Contains("end screen") || text.Contains("end-screen") || text.Contains("финал"))
            return AppLogCategory.EndScreen;
        if (text.Contains("layout") || text.Contains("guilayout") || text.Contains("recttransform") || text.Contains("canvas"))
            return AppLogCategory.Layout;
        if (text.Contains("editor") || text.Contains("inspector") || text.Contains("ongui"))
            return AppLogCategory.Editor;
        if (text.Contains("saveprogress") || text.Contains("save progress"))
            return AppLogCategory.SaveSystem;
        if (text.Contains("save") || text.Contains("playerprefs") || text.Contains("cache") || text.Contains("bookmark") || text.Contains("localsecureprefs"))
            return AppLogCategory.Storage;
        return AppLogCategory.App;
    }

    private static string ResolveUnityComponent(string condition, string stackTrace)
    {
        string prefix = ExtractBracketPrefix(condition);
        if (!string.IsNullOrEmpty(prefix))
            return prefix;

        if (!string.IsNullOrEmpty(stackTrace))
        {
            string[] lines = stackTrace.Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 ||
                    line.Contains("AppLogger") ||
                    line.Contains("NovelTemplateLoggingBootstrap") ||
                    line.Contains("UnityEngine.Debug"))
                {
                    continue;
                }

                int separator = line.IndexOf('.');
                if (separator > 0)
                    return SafeToken(line.Substring(0, separator), "UnityLog");
            }
        }

        return "UnityLog";
    }

    private static string ExtractBracketPrefix(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition) || condition[0] != '[')
            return "";

        int end = condition.IndexOf(']');
        if (end <= 1 || end > 48)
            return "";

        return SafeToken(condition.Substring(1, end - 1), "");
    }

    private static AppLogLevel MapUnityLevel(LogType type)
    {
        switch (type)
        {
            case LogType.Error:
            case LogType.Assert:
            case LogType.Exception:
                return AppLogLevel.Error;
            case LogType.Warning:
                return AppLogLevel.Warn;
            default:
                return AppLogLevel.Info;
        }
    }

    private static string LevelName(AppLogLevel level)
    {
        switch (level)
        {
            case AppLogLevel.Trace:
                return "TRACE";
            case AppLogLevel.Debug:
                return "DEBUG";
            case AppLogLevel.Info:
                return "INFO";
            case AppLogLevel.Warn:
                return "WARN";
            case AppLogLevel.Error:
                return "ERROR";
            case AppLogLevel.Fatal:
                return "CRITICAL";
            default:
                return "INFO";
        }
    }

    private static string SafeToken(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        value = Trim(RedactText(value.Trim()), 96);
        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
                builder.Append(c);
        }

        return builder.Length > 0 ? builder.ToString() : fallback;
    }

    private static string SanitizeFilePart(string value)
    {
        return SafeToken(value, AppLogCategory.App).ToLowerInvariant();
    }

    private static string Trim(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value) || maxChars <= 0)
            return value ?? "";

        return value.Length <= maxChars ? value : value.Substring(0, maxChars) + "...";
    }

    private static string GetEnvironment(string key)
    {
        try
        {
            return Environment.GetEnvironmentVariable(key);
        }
        catch (Exception exception)
        {
            WriteFallback("Logging environment read failed for '" + key + "': " + exception.Message);
            return null;
        }
    }

    private static AppLogLevel ParseLevel(string value, AppLogLevel fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        switch (value.Trim().ToUpperInvariant())
        {
            case "TRACE":
                return AppLogLevel.Trace;
            case "DEBUG":
                return AppLogLevel.Debug;
            case "INFO":
                return AppLogLevel.Info;
            case "WARN":
            case "WARNING":
                return AppLogLevel.Warn;
            case "ERROR":
                return AppLogLevel.Error;
            case "CRITICAL":
            case "FATAL":
                return AppLogLevel.Fatal;
            default:
                return fallback;
        }
    }

    private static bool ParseBool(string value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "yes":
            case "on":
                return true;
            case "0":
            case "false":
            case "no":
            case "off":
                return false;
            default:
                return fallback;
        }
    }

    private static int ParseInt(string value, int fallback, int min, int max)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            return fallback;

        return Mathf.Clamp(parsed, min, max);
    }

    private static long ParseSize(string value, long fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        string text = value.Trim().ToUpperInvariant();
        long multiplier = 1;
        if (text.EndsWith("KB", StringComparison.Ordinal))
        {
            multiplier = 1024L;
            text = text.Substring(0, text.Length - 2).Trim();
        }
        else if (text.EndsWith("MB", StringComparison.Ordinal))
        {
            multiplier = 1024L * 1024L;
            text = text.Substring(0, text.Length - 2).Trim();
        }
        else if (text.EndsWith("GB", StringComparison.Ordinal))
        {
            multiplier = 1024L * 1024L * 1024L;
            text = text.Substring(0, text.Length - 2).Trim();
        }

        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            return fallback;

        if (parsed <= 0)
            return fallback;

        return Math.Min(parsed * multiplier, 1024L * 1024L * 1024L);
    }

    private static void WriteFallback(string line)
    {
        try
        {
            Console.Error.WriteLine(line);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine("AppLogger fallback output failed: " + exception.Message);
        }
    }
}
