using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class LocalSaveSecurity
{
    public const string SavePurpose = "save";
    public const string BookmarkPurpose = "bookmark";
    public const string HeroCustomizationPurpose = "hero_customization";
    public const string EpisodeSummaryPurpose = "episode_summary";
    public const string ChapterBoundaryPurpose = "chapter_boundary";
    public const string ChapterUnlockPurpose = "chapter_unlock";
    public const string StoryProgressionPurpose = "story_progression";
    public const string PlayerCurrencyPurpose = "player_currency";
    public const string SetupFlagPurpose = "setup_flag";
    public const string SubscriptionPurpose = "subscription";
    public const string PrototypeFlagPurpose = "prototype_flag";
    public const string FavoritesPurpose = "favorites";
    public const string RemoteGraphCachePurpose = "remote_graph_cache";
    public const string RegionCachePurpose = "region_cache";
    public const int MaxProtectedPayloadChars = SaveDataSanitizer.MaxSerializedChars * 4;

    const string EnvelopeFormat = "nocturne-local-secure-v1";
    const string DeviceIdKey = "VN_DEVICE_ID";
    const string KeyDomain = "nocturne-local-save-security-v1";

    public static string ProtectJson(string json, string purpose, bool prettyPrint = false)
    {
        return ProtectText(json, purpose, prettyPrint);
    }

    public static string ProtectText(string text, string purpose, bool prettyPrint = false)
    {
        return ProtectText(text, purpose, SaveDataSanitizer.MaxSerializedChars, MaxProtectedPayloadChars, prettyPrint);
    }

    public static string ProtectLargeText(string text, string purpose, int maxPlainChars, int maxProtectedChars, bool prettyPrint = false)
    {
        return ProtectText(text, purpose, maxPlainChars, maxProtectedChars, prettyPrint);
    }

    public static bool TryUnprotectJson(string storedText, string purpose, out string json, out bool wasProtected)
    {
        return TryUnprotectText(storedText, purpose, out json, out wasProtected);
    }

    public static bool TryUnprotectText(string storedText, string purpose, out string text, out bool wasProtected)
    {
        return TryUnprotectText(storedText, purpose, SaveDataSanitizer.MaxSerializedChars, MaxProtectedPayloadChars, out text, out wasProtected);
    }

    public static bool TryUnprotectLargeText(string storedText, string purpose, int maxPlainChars, int maxProtectedChars, out string text, out bool wasProtected)
    {
        return TryUnprotectText(storedText, purpose, maxPlainChars, maxProtectedChars, out text, out wasProtected);
    }

    static string ProtectText(string text, string purpose, int maxPlainChars, int maxProtectedChars, bool prettyPrint)
    {
        if (text == null || text.Length > maxPlainChars)
            return "";

        string safePurpose = NormalizePurpose(purpose);
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        var envelope = new SecureEnvelope
        {
            version = 1,
            format = EnvelopeFormat,
            purpose = safePurpose,
            payload = payload,
            mac = ComputeMac(safePurpose, payload),
            savedAtIso = DateTime.UtcNow.ToString("o")
        };

        string protectedText = JsonUtility.ToJson(envelope, prettyPrint);
        return protectedText.Length <= maxProtectedChars ? protectedText : "";
    }

    static bool TryUnprotectText(string storedText, string purpose, int maxPlainChars, int maxProtectedChars, out string text, out bool wasProtected)
    {
        text = "";
        wasProtected = false;

        if (string.IsNullOrWhiteSpace(storedText) || storedText.Length > maxProtectedChars)
            return false;

        SecureEnvelope envelope = TryReadEnvelope(storedText);
        if (!IsEnvelope(envelope))
        {
            text = storedText;
            return text.Length <= maxPlainChars;
        }

        wasProtected = true;
        string safePurpose = NormalizePurpose(purpose);
        if (envelope.version != 1 ||
            !string.Equals(envelope.format, EnvelopeFormat, StringComparison.Ordinal) ||
            !string.Equals(envelope.purpose, safePurpose, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(envelope.payload) ||
            string.IsNullOrWhiteSpace(envelope.mac))
        {
            return false;
        }

        string expectedMac = ComputeMac(safePurpose, envelope.payload);
        if (!FixedTimeEquals(envelope.mac, expectedMac))
            return false;

        try
        {
            byte[] payloadBytes = Convert.FromBase64String(envelope.payload);
            if (payloadBytes.Length > maxPlainChars * 4)
                return false;

            text = Encoding.UTF8.GetString(payloadBytes);
            return text != null && text.Length <= maxPlainChars;
        }
        catch (Exception exception)
        {
            ThrottledAppLogger.Warn(
                nameof(LocalSaveSecurity) + ".TryUnprotectText.Decode",
                AppLogCategory.Security,
                nameof(LocalSaveSecurity),
                nameof(TryUnprotectText),
                "Protected local payload could not be decoded.",
                LogMetadata.Of("purpose", safePurpose, "errorType", exception.GetType().Name));
            text = "";
            return false;
        }
    }

    static SecureEnvelope TryReadEnvelope(string storedText)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(storedText) || !storedText.TrimStart().StartsWith("{", StringComparison.Ordinal))
                return null;

            return JsonUtility.FromJson<SecureEnvelope>(storedText);
        }
        catch (Exception exception)
        {
            ThrottledAppLogger.Warn(
                nameof(LocalSaveSecurity) + ".TryReadEnvelope",
                AppLogCategory.Security,
                nameof(LocalSaveSecurity),
                nameof(TryReadEnvelope),
                "Protected local envelope could not be parsed.",
                LogMetadata.Of(
                    "payloadChars", storedText != null ? storedText.Length : 0,
                    "errorType", exception.GetType().Name));
            return null;
        }
    }

    static bool IsEnvelope(SecureEnvelope envelope)
    {
        return envelope != null &&
               (!string.IsNullOrEmpty(envelope.format) ||
                !string.IsNullOrEmpty(envelope.payload) ||
                !string.IsNullOrEmpty(envelope.mac));
    }

    static string ComputeMac(string purpose, string payload)
    {
        byte[] key = GetMacKey();
        byte[] data = Encoding.UTF8.GetBytes(EnvelopeFormat + "\n" + purpose + "\n" + payload);
        using (var hmac = new HMACSHA256(key))
            return Convert.ToBase64String(hmac.ComputeHash(data));
    }

    static byte[] GetMacKey()
    {
        string seed = KeyDomain + "|" +
            GetOrCreateLocalDeviceId() + "|" +
            (Application.identifier ?? "") + "|" +
            (Application.companyName ?? "") + "|" +
            (Application.productName ?? "") + "|" +
            (SystemInfo.deviceUniqueIdentifier ?? "");

        using (SHA256 sha = SHA256.Create())
            return sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
    }

    static string GetOrCreateLocalDeviceId()
    {
        string id = "";
        try
        {
            id = SaveDataSanitizer.SanitizeIdentifier(PlayerPrefs.GetString(DeviceIdKey, ""));
        }
        catch (Exception exception)
        {
            ThrottledAppLogger.Warn(
                nameof(LocalSaveSecurity) + ".ReadDeviceId",
                AppLogCategory.Security,
                nameof(LocalSaveSecurity),
                nameof(GetOrCreateLocalDeviceId),
                "Local device id could not be read from PlayerPrefs.",
                LogMetadata.Of("errorType", exception.GetType().Name));
            id = "";
        }

        if (!string.IsNullOrEmpty(id))
            return id;

        id = SaveDataSanitizer.SanitizeIdentifier(SystemInfo.deviceUniqueIdentifier);
        if (string.IsNullOrEmpty(id) || id == SystemInfo.unsupportedIdentifier)
            id = Guid.NewGuid().ToString("N");

        try
        {
            PlayerPrefs.SetString(DeviceIdKey, id);
            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            ThrottledAppLogger.Warn(
                nameof(LocalSaveSecurity) + ".WriteDeviceId",
                AppLogCategory.Security,
                nameof(LocalSaveSecurity),
                nameof(GetOrCreateLocalDeviceId),
                "Local device id could not be persisted; using the in-memory id for this run.",
                LogMetadata.Of("errorType", exception.GetType().Name));
        }

        return id;
    }

    static bool FixedTimeEquals(string left, string right)
    {
        if (left == null || right == null)
            return false;

        byte[] leftBytes;
        byte[] rightBytes;
        try
        {
            leftBytes = Convert.FromBase64String(left);
            rightBytes = Convert.FromBase64String(right);
        }
        catch (Exception exception)
        {
            ThrottledAppLogger.Warn(
                nameof(LocalSaveSecurity) + ".FixedTimeEquals.Decode",
                AppLogCategory.Security,
                nameof(LocalSaveSecurity),
                nameof(FixedTimeEquals),
                "Secure payload MAC could not be decoded for comparison.",
                LogMetadata.Of("errorType", exception.GetType().Name));
            return false;
        }

        if (leftBytes.Length != rightBytes.Length)
            return false;

        int diff = 0;
        for (int i = 0; i < leftBytes.Length; i++)
            diff |= leftBytes[i] ^ rightBytes[i];

        return diff == 0;
    }

    static string NormalizePurpose(string purpose)
    {
        string safe = SaveDataSanitizer.SanitizeIdentifier(purpose);
        return string.IsNullOrEmpty(safe) ? "default" : safe;
    }

    [Serializable]
    sealed class SecureEnvelope
    {
        public int version;
        public string format;
        public string purpose;
        public string payload;
        public string mac;
        public string savedAtIso;
    }
}
