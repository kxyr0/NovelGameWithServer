using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class NetworkJson
{
    const int MaxGenericJsonChars = 2 * 1024 * 1024;
    const int MaxParsedArrayItems = 1000;
    const int MaxParsedDictionaryItems = 512;

    public static T FromJson<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        if (json.Length > MaxGenericJsonChars)
        {
            ThrottledAppLogger.Warn(
                nameof(NetworkJson) + ".OversizedJson:" + typeof(T).Name,
                AppLogCategory.Network,
                nameof(NetworkJson),
                nameof(FromJson),
                "JSON payload is too large and was ignored.",
                LogMetadata.Of(
                    "dtoType", typeof(T).Name,
                    "payloadChars", json.Length,
                    "maxPayloadChars", MaxGenericJsonChars));
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception exception)
        {
            ThrottledAppLogger.Warn(
                nameof(NetworkJson) + ".ParseFailed:" + typeof(T).Name,
                AppLogCategory.Network,
                nameof(NetworkJson),
                nameof(FromJson),
                "Failed to parse JSON payload.",
                LogMetadata.Of(
                    "dtoType", typeof(T).Name,
                    "payloadChars", json.Length,
                    "errorType", exception.GetType().Name,
                    "error", exception.Message));
            return null;
        }
    }

    public static List<T> FromJsonArray<T>(string json) where T : class
    {
        var result = new List<T>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        foreach (var rawItem in ParseArray(json))
        {
            if (result.Count >= MaxParsedArrayItems)
                break;

            if (string.IsNullOrWhiteSpace(rawItem))
                continue;

            var item = FromJson<T>(rawItem);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    public static string ToJson(object dto, bool prettyPrint = false)
    {
        if (dto == null)
            return "{}";

        if (dto is SaveProgressRequest progress)
            return ToProgressJson(progress);

        if (dto is BookmarkRequest bookmark)
            return ToBookmarkJson(bookmark);

        return JsonUtility.ToJson(dto, prettyPrint);
    }

    public static string ToSaveDataJson(SaveData data, bool prettyPrint = false)
    {
        SaveData safeData = SaveDataSanitizer.SanitizeCopy(data);
        return safeData == null ? "" : JsonUtility.ToJson(safeData, prettyPrint);
    }

    public static SaveData FromSaveDataJson(string json)
    {
        if (!SaveDataSanitizer.IsSerializedSizeAllowed(json))
            return null;

        return SaveDataSanitizer.Sanitize(FromJson<SaveData>(json));
    }

    public static string GetString(string json, string key)
    {
        var raw = GetRawValue(json, key);
        if (string.IsNullOrEmpty(raw) || raw == "null")
            return null;

        if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
            return UnescapeJsonString(raw.Substring(1, raw.Length - 2));

        return raw;
    }

    public static string GetFirstString(string json, params string[] keys)
    {
        if (keys == null)
            return null;

        foreach (var key in keys)
        {
            var value = GetString(json, key);
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }

    public static int GetInt(string json, string key, int defaultValue = 0)
    {
        var raw = GetRawValue(json, key);
        if (string.IsNullOrEmpty(raw) || raw == "null")
            return defaultValue;

        if (int.TryParse(raw.Trim('"'), out int value))
            return value;

        return defaultValue;
    }

    public static bool GetBool(string json, string key, bool defaultValue = false)
    {
        var raw = GetRawValue(json, key);
        if (string.IsNullOrEmpty(raw) || raw == "null")
            return defaultValue;

        if (bool.TryParse(raw.Trim('"'), out bool value))
            return value;

        return defaultValue;
    }

    public static bool LooksLikeJsonObject(string json)
    {
        return !string.IsNullOrWhiteSpace(json) && json.TrimStart().StartsWith("{");
    }

    public static SaveData GetSaveData(string json, string key)
    {
        var raw = GetRawValue(json, key);
        if (string.IsNullOrEmpty(raw) || raw == "null")
            return null;

        if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
            raw = UnescapeJsonString(raw.Substring(1, raw.Length - 2));

        if (string.IsNullOrWhiteSpace(raw) || raw[0] != '{')
            return null;

        if (raw.Length > SaveDataSanitizer.MaxSerializedChars)
            return null;

        return FromSaveDataJson(raw);
    }

    public static List<string> GetStringList(string json, string key)
    {
        var raw = GetRawValue(json, key);
        if (string.IsNullOrEmpty(raw) || raw == "null")
            return new List<string>();

        var result = new List<string>();
        foreach (var item in ParseArray(raw))
        {
            if (string.IsNullOrEmpty(item) || item == "null")
                continue;

            string value = item.Length >= 2 && item[0] == '"' && item[item.Length - 1] == '"'
                ? UnescapeJsonString(item.Substring(1, item.Length - 2))
                : item;

            value = SaveDataSanitizer.SanitizeIdentifier(value);
            if (!string.IsNullOrEmpty(value) && !result.Contains(value))
                result.Add(value);

            if (result.Count >= MaxParsedDictionaryItems)
                break;
        }

        return result;
    }

    public static List<string> GetArrayItems(string rawArray)
    {
        return ParseArray(rawArray);
    }

    public static Dictionary<string, int> GetIntDictionary(string json, string key)
    {
        var raw = GetRawValue(json, key);
        var result = new Dictionary<string, int>();
        if (string.IsNullOrEmpty(raw) || raw == "null")
            return result;

        foreach (var kv in ParseObject(raw))
        {
            if (result.Count >= MaxParsedDictionaryItems)
                break;

            string safeKey = SaveDataSanitizer.SanitizeStatKey(kv.Key);
            if (!string.IsNullOrEmpty(safeKey) && int.TryParse(kv.Value, out int value))
                result[safeKey] = SaveDataSanitizer.ClampStatValue(value);
        }

        return result;
    }

    public static Dictionary<string, bool> GetBoolDictionary(string json, string key)
    {
        var raw = GetRawValue(json, key);
        var result = new Dictionary<string, bool>();
        if (string.IsNullOrEmpty(raw) || raw == "null")
            return result;

        foreach (var kv in ParseObject(raw))
        {
            if (result.Count >= MaxParsedDictionaryItems)
                break;

            string safeKey = SaveDataSanitizer.SanitizeStatKey(kv.Key);
            if (!string.IsNullOrEmpty(safeKey) && bool.TryParse(kv.Value, out bool value))
                result[safeKey] = value;
        }

        return result;
    }

    public static Dictionary<string, string> GetStringDictionary(string json, string key)
    {
        var raw = GetRawValue(json, key);
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(raw) || raw == "null")
            return result;

        foreach (var kv in ParseObject(raw))
        {
            if (result.Count >= MaxParsedDictionaryItems)
                break;

            string safeKey = SaveDataSanitizer.SanitizeIdentifier(kv.Key);
            if (string.IsNullOrEmpty(safeKey))
                continue;

            var value = kv.Value;
            if (string.IsNullOrEmpty(value) || value == "null")
                continue;

            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                result[safeKey] = SaveDataSanitizer.SanitizeIdentifier(UnescapeJsonString(value.Substring(1, value.Length - 2)));
            else
                result[safeKey] = SaveDataSanitizer.SanitizeIdentifier(value);
        }

        return result;
    }

    public static string GetRawValue(string json, string key)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return null;

        if (!LooksLikeJsonObject(json))
            return null;

        var properties = ParseObject(json);
        return properties.TryGetValue(key, out string value) ? value : null;
    }

    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var sb = new StringBuilder(value.Length + 8);
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    static string ToProgressJson(SaveProgressRequest request)
    {
        var sb = new StringBuilder(256);
        sb.Append('{');
        AppendString(sb, "storyId", request.storyId);
        sb.Append(',');
        AppendString(sb, "episodeId", request.episodeId);
        sb.Append(',');
        AppendString(sb, "nodeId", request.nodeId);
        sb.Append(',');
        AppendString(sb, "currentEpisodeId", request.currentEpisodeId);
        sb.Append(',');
        AppendString(sb, "currentNodeGuid", request.currentNodeGuid);
        sb.Append(',');
        AppendRaw(sb, "snapshot", SaveDataToRawJson(request.snapshot));
        sb.Append(',');
        AppendRaw(sb, "stats", IntDictionaryToJson(request.stats));
        sb.Append(',');
        AppendRaw(sb, "flags", BoolDictionaryToJson(request.flags));
        sb.Append(',');
        AppendRaw(sb, "variables", BoolDictionaryToJson(request.variables));
        sb.Append(',');
        AppendRaw(sb, "unlockedEpisodes", StringListToJson(request.unlockedEpisodes));
        sb.Append('}');
        return sb.ToString();
    }

    static string ToBookmarkJson(BookmarkRequest request)
    {
        var sb = new StringBuilder(256);
        sb.Append('{');
        AppendString(sb, "nodeGuid", request.nodeGuid);
        sb.Append(',');
        AppendString(sb, "episodeId", request.episodeId);
        sb.Append(',');
        AppendString(sb, "storyId", request.storyId);
        sb.Append(',');
        AppendRaw(sb, "snapshot", SaveDataToRawJson(request.snapshot));
        sb.Append(',');
        AppendString(sb, "label", request.label);
        sb.Append('}');
        return sb.ToString();
    }

    static string SaveDataToRawJson(SaveData data)
    {
        return data == null ? "null" : ToSaveDataJson(data);
    }

    static void AppendString(StringBuilder sb, string key, string value)
    {
        sb.Append('"').Append(key).Append("\":\"").Append(Escape(value)).Append('"');
    }

    static void AppendRaw(StringBuilder sb, string key, string rawJson)
    {
        sb.Append('"').Append(key).Append("\":").Append(string.IsNullOrEmpty(rawJson) ? "null" : rawJson);
    }

    static string IntDictionaryToJson(Dictionary<string, int> values)
    {
        if (values == null || values.Count == 0)
            return "{}";

        var sb = new StringBuilder("{");
        bool first = true;
        foreach (var kv in values)
        {
            if (!first) sb.Append(',');
            AppendNumberPair(sb, kv.Key, kv.Value);
            first = false;
        }
        sb.Append('}');
        return sb.ToString();
    }

    static string BoolDictionaryToJson(Dictionary<string, bool> values)
    {
        if (values == null || values.Count == 0)
            return "{}";

        var sb = new StringBuilder("{");
        bool first = true;
        foreach (var kv in values)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(Escape(kv.Key)).Append("\":").Append(kv.Value ? "true" : "false");
            first = false;
        }
        sb.Append('}');
        return sb.ToString();
    }

    static string StringListToJson(List<string> values)
    {
        if (values == null || values.Count == 0)
            return "[]";

        var sb = new StringBuilder("[");
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(Escape(values[i] ?? "")).Append('"');
        }

        sb.Append(']');
        return sb.ToString();
    }

    static void AppendNumberPair(StringBuilder sb, string key, int value)
    {
        sb.Append('"').Append(Escape(key)).Append("\":").Append(value);
    }

    static int FindStringEnd(string json, int start)
    {
        bool escaped = false;
        for (int i = start; i < json.Length; i++)
        {
            char c = json[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
                return i;
        }

        return -1;
    }

    static int FindContainerEnd(string json, int start, char open)
    {
        char close = open == '{' ? '}' : ']';
        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = start; i < json.Length; i++)
        {
            char c = json[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString) continue;

            if (c == open) depth++;
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    static Dictionary<string, string> ParseObject(string json)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        json = json.Trim();
        if (json.Length < 2 || json[0] != '{' || json[json.Length - 1] != '}')
            return result;

        int idx = 1;
        while (idx < json.Length - 1)
        {
            SkipWhitespace(json, ref idx);
            if (idx >= json.Length - 1 || json[idx] == '}')
                break;
            if (json[idx] != '"')
                break;

            int keyEnd = FindStringEnd(json, idx + 1);
            if (keyEnd < 0)
                break;

            string key = UnescapeJsonString(json.Substring(idx + 1, keyEnd - idx - 1));
            idx = keyEnd + 1;
            SkipWhitespace(json, ref idx);
            if (idx >= json.Length || json[idx] != ':')
                break;

            idx++;
            SkipWhitespace(json, ref idx);
            if (idx >= json.Length)
                break;

            int valueStart = idx;
            int valueEnd = FindValueEnd(json, idx, '}');
            if (valueEnd < valueStart)
                break;

            result[key] = json.Substring(valueStart, valueEnd - valueStart + 1).Trim();
            if (result.Count >= MaxParsedDictionaryItems)
                break;

            idx = valueEnd + 1;
            SkipWhitespace(json, ref idx);
            if (idx < json.Length && json[idx] == ',')
                idx++;
        }

        return result;
    }

    static List<string> ParseArray(string json)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        json = json.Trim();
        if (json.Length < 2 || json[0] != '[' || json[json.Length - 1] != ']')
            return result;

        int idx = 1;
        while (idx < json.Length - 1)
        {
            SkipWhitespace(json, ref idx);
            if (idx >= json.Length - 1 || json[idx] == ']')
                break;

            int valueStart = idx;
            int valueEnd = FindValueEnd(json, idx, ']');
            if (valueEnd < valueStart)
                break;

            result.Add(json.Substring(valueStart, valueEnd - valueStart + 1).Trim());
            if (result.Count >= MaxParsedArrayItems)
                break;

            idx = valueEnd + 1;
            SkipWhitespace(json, ref idx);
            if (idx < json.Length && json[idx] == ',')
                idx++;
        }

        return result;
    }

    static int FindValueEnd(string json, int start, char containerClose)
    {
        if (start >= json.Length)
            return -1;

        char first = json[start];
        if (first == '"')
            return FindStringEnd(json, start + 1);

        if (first == '{' || first == '[')
            return FindContainerEnd(json, start, first);

        int idx = start;
        while (idx < json.Length && json[idx] != ',' && json[idx] != containerClose)
            idx++;

        return idx - 1;
    }

    static void SkipWhitespace(string json, ref int idx)
    {
        while (idx < json.Length && char.IsWhiteSpace(json[idx]))
            idx++;
    }

    static string UnescapeJsonString(string value)
    {
        var sb = new StringBuilder(value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }

            if (i + 1 >= value.Length)
                break;

            char escaped = value[++i];
            switch (escaped)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (i + 4 < value.Length && TryParseUnicodeEscape(value, i + 1, out char unicode))
                    {
                        sb.Append(unicode);
                        i += 4;
                    }
                    else
                    {
                        sb.Append('u');
                    }
                    break;
                default:
                    sb.Append(escaped);
                    break;
            }
        }

        return sb.ToString();
    }

    static bool TryParseUnicodeEscape(string value, int start, out char result)
    {
        result = '\0';
        int code = 0;

        for (int i = 0; i < 4; i++)
        {
            int nibble = HexToInt(value[start + i]);
            if (nibble < 0)
                return false;

            code = (code << 4) | nibble;
        }

        result = (char)code;
        return true;
    }

    static int HexToInt(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return -1;
    }
}
