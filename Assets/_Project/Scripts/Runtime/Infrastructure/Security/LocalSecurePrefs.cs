using System.Globalization;
using UnityEngine;

public static class LocalSecurePrefs
{
    const string SecureMarkerSuffix = "__SECURE_V1";

    public static string GetString(string key, string purpose, string defaultValue = "")
    {
        key = NormalizeKey(key);
        if (string.IsNullOrEmpty(key) || !PlayerPrefs.HasKey(key))
            return defaultValue;

        try
        {
            string stored = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(stored))
                return defaultValue;

            if (stored.Length > LocalSaveSecurity.MaxProtectedPayloadChars)
            {
                Delete(key);
                return defaultValue;
            }

            if (!LocalSaveSecurity.TryUnprotectText(stored, NormalizePurpose(purpose, key), out string value, out bool wasProtected))
            {
                LogSecurityWarning(nameof(GetString), "Ignored invalid secure PlayerPrefs payload.", key);
                Delete(key);
                return defaultValue;
            }

            if (!wasProtected && HasSecureMarker(key))
            {
                LogSecurityWarning(nameof(GetString), "Ignored downgraded raw PlayerPrefs payload.", key);
                Delete(key);
                return defaultValue;
            }

            if (!wasProtected)
                SetString(key, purpose, value);

            return value ?? defaultValue;
        }
        catch (System.Exception exception)
        {
            LogSecurityError(nameof(GetString), "Failed to load secure PlayerPrefs string.", key, exception);
            return defaultValue;
        }
    }

    public static int GetInt(string key, string purpose, int defaultValue = 0)
    {
        key = NormalizeKey(key);
        if (string.IsNullOrEmpty(key) || !PlayerPrefs.HasKey(key))
            return defaultValue;

        try
        {
            string stored = PlayerPrefs.GetString(key, "");
            if (!string.IsNullOrEmpty(stored))
            {
                if (stored.Length > LocalSaveSecurity.MaxProtectedPayloadChars)
                {
                    Delete(key);
                    return defaultValue;
                }

                if (!LocalSaveSecurity.TryUnprotectText(stored, NormalizePurpose(purpose, key), out string value, out bool wasProtected))
                {
                    LogSecurityWarning(nameof(GetInt), "Ignored invalid secure PlayerPrefs int payload.", key);
                    Delete(key);
                    return defaultValue;
                }

                int parsed = ParseInt(value, defaultValue);
                if (!wasProtected && HasSecureMarker(key))
                {
                    LogSecurityWarning(nameof(GetInt), "Ignored downgraded raw PlayerPrefs int payload.", key);
                    Delete(key);
                    return defaultValue;
                }

                if (!wasProtected)
                    SetInt(key, purpose, parsed);

                return parsed;
            }

            if (HasSecureMarker(key))
            {
                LogSecurityWarning(nameof(GetInt), "Ignored downgraded raw PlayerPrefs int payload.", key);
                Delete(key);
                return defaultValue;
            }

            int legacyValue = PlayerPrefs.GetInt(key, defaultValue);
            SetInt(key, purpose, legacyValue);
            return legacyValue;
        }
        catch (System.Exception exception)
        {
            LogSecurityError(nameof(GetInt), "Failed to load secure PlayerPrefs int.", key, exception);
            return defaultValue;
        }
    }

    public static bool GetBool(string key, string purpose, bool defaultValue = false)
    {
        return GetInt(key, purpose, defaultValue ? 1 : 0) == 1;
    }

    public static void SetString(string key, string purpose, string value)
    {
        key = NormalizeKey(key);
        if (string.IsNullOrEmpty(key))
            return;

        try
        {
            string protectedValue = LocalSaveSecurity.ProtectText(value ?? "", NormalizePurpose(purpose, key));
            if (string.IsNullOrEmpty(protectedValue))
            {
                LogSecurityWarning(nameof(SetString), "Failed to protect PlayerPrefs value.", key);
                return;
            }

            PlayerPrefs.SetString(key, protectedValue);
            MarkSecure(key);
            PlayerPrefs.Save();
        }
        catch (System.Exception exception)
        {
            LogSecurityError(nameof(SetString), "Failed to save secure PlayerPrefs value.", key, exception);
        }
    }

    public static void SetInt(string key, string purpose, int value)
    {
        SetString(key, purpose, value.ToString(CultureInfo.InvariantCulture));
    }

    public static void SetBool(string key, string purpose, bool value)
    {
        SetInt(key, purpose, value ? 1 : 0);
    }

    public static void Delete(string key)
    {
        key = NormalizeKey(key);
        if (string.IsNullOrEmpty(key))
            return;

        try
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(GetSecureMarkerKey(key));
            PlayerPrefs.Save();
        }
        catch (System.Exception exception)
        {
            LogSecurityError(nameof(Delete), "Failed to delete secure PlayerPrefs value.", key, exception);
        }
    }

    public static void MarkSecure(string key)
    {
        key = NormalizeKey(key);
        if (string.IsNullOrEmpty(key))
            return;

        try
        {
            PlayerPrefs.SetInt(GetSecureMarkerKey(key), 1);
        }
        catch (System.Exception exception)
        {
            LogSecurityError(nameof(MarkSecure), "Failed to mark PlayerPrefs value as secure.", key, exception);
        }
    }

    public static void EnsureSecureMarker(string key)
    {
        key = NormalizeKey(key);
        if (string.IsNullOrEmpty(key) || HasSecureMarker(key))
            return;

        try
        {
            MarkSecure(key);
            PlayerPrefs.Save();
        }
        catch (System.Exception exception)
        {
            LogSecurityError(nameof(EnsureSecureMarker), "Failed to persist PlayerPrefs secure marker.", key, exception);
        }
    }

    static int ParseInt(string value, int defaultValue)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;
    }

    static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? "" : key.Trim();
    }

    static string NormalizePurpose(string purpose, string key)
    {
        purpose = SaveDataSanitizer.SanitizeIdentifier(purpose);
        key = SaveDataSanitizer.SanitizeIdentifier(key);
        return (string.IsNullOrEmpty(purpose) ? "prefs" : purpose) + ":" + key;
    }

    public static bool HasSecureMarker(string key)
    {
        key = NormalizeKey(key);
        if (string.IsNullOrEmpty(key))
            return false;

        try
        {
            return PlayerPrefs.GetInt(GetSecureMarkerKey(key), 0) == 1;
        }
        catch (System.Exception exception)
        {
            ThrottledAppLogger.Warn(
                nameof(LocalSecurePrefs) + ".HasSecureMarker",
                AppLogCategory.Security,
                nameof(LocalSecurePrefs),
                nameof(HasSecureMarker),
                "Secure PlayerPrefs marker could not be read.",
                LogMetadata.Of(
                    "key", SaveDataSanitizer.SafeKeyPart(key, "pref", 96),
                    "errorType", exception.GetType().Name));
            return false;
        }
    }

    static string GetSecureMarkerKey(string key)
    {
        return SaveDataSanitizer.SafeKeyPart(key, "pref", 96) + SecureMarkerSuffix;
    }

    static void LogSecurityWarning(string operation, string message, string key)
    {
        ThrottledAppLogger.Warn(
            nameof(LocalSecurePrefs) + "." + operation + ":" + SaveDataSanitizer.SafeKeyPart(key, "pref", 96),
            AppLogCategory.Security,
            nameof(LocalSecurePrefs),
            operation,
            message,
            LogMetadata.Of("key", SaveDataSanitizer.SafeKeyPart(key, "pref", 96)));
    }

    static void LogSecurityError(string operation, string message, string key, System.Exception exception)
    {
        AppLogger.Error(
            AppLogCategory.Security,
            nameof(LocalSecurePrefs),
            operation,
            message,
            exception,
            LogMetadata.Of("key", SaveDataSanitizer.SafeKeyPart(key, "pref", 96)),
            recoverable: true);
    }
}
