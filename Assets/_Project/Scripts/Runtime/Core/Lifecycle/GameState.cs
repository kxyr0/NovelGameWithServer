using System.Collections.Generic;
using UnityEngine;

public class GameState : MonoBehaviour
{
    public static GameState Instance;

    [Header("History")]
    public int maxHistoryEntries = 1000;

    public BaseStoryNode currentNode;

    public int currency
    {
        get => PlayerData.Candles;
        set => PlayerData.SetCandlesValue(value);
    }

    public Dictionary<string, int> stats = new Dictionary<string, int>();
    public List<string> history = new List<string>();

    HashSet<string> ownedClothes = new HashSet<string>();
    public HashSet<string> wardrobe => ownedClothes;
    Dictionary<string, string> equippedClothes = new Dictionary<string, string>();

    const string STATS_KEY_PREFIX = "VN_STATS_";
    const string OWNED_KEY_PREFIX = "VN_OWNED_";
    const string EQUIPPED_KEY_PREFIX = "VN_EQUIPPED_";

    const string FALLBACK_STATS_KEY = "VN_STATS";
    const string FALLBACK_OWNED_KEY = "VN_OWNED_CLOTHES";
    const string FALLBACK_EQUIPPED_KEY = "VN_EQUIPPED_CLOTHES";
    const int MaxPrefsPayloadChars = LocalSaveSecurity.MaxProtectedPayloadChars;

    string _storyId;
    public string CurrentStoryId => _storyId;

    string StatsKey => string.IsNullOrEmpty(_storyId) ? FALLBACK_STATS_KEY : STATS_KEY_PREFIX + _storyId;
    string OwnedKey => string.IsNullOrEmpty(_storyId) ? FALLBACK_OWNED_KEY : OWNED_KEY_PREFIX + _storyId;
    string EquippedKey => string.IsNullOrEmpty(_storyId) ? FALLBACK_EQUIPPED_KEY : EQUIPPED_KEY_PREFIX + _storyId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Instance = null;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void InitForStory(string storyId)
    {
        _storyId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        ClearRuntimeState();
        LoadStats();
        LoadClothes();

        AppLogger.Info(
            AppLogCategory.SaveSystem,
            nameof(GameState),
            nameof(InitForStory),
            "[GAMESTATE][INIT] Runtime state initialized for story.",
            LogMetadata.Of(
                "storyId", _storyId,
                "statsCount", stats != null ? stats.Count : 0,
                "ownedCount", ownedClothes != null ? ownedClothes.Count : 0,
                "equippedCount", equippedClothes != null ? equippedClothes.Count : 0,
                "owned", CompactStrings(ownedClothes),
                "equipped", CompactEquipped(equippedClothes)));
    }

    public Dictionary<string, int> GetStatsSnapshot()
    {
        EnsureCollections();
        return new Dictionary<string, int>(stats);
    }

    public List<string> GetOwnedClothesSnapshot()
    {
        EnsureCollections();
        return new List<string>(ownedClothes);
    }

    public Dictionary<string, string> GetEquippedClothesSnapshot()
    {
        EnsureCollections();
        return new Dictionary<string, string>(equippedClothes);
    }

    public void ApplySnapshot(SaveData data)
    {
        data = SaveDataSanitizer.Sanitize(data);
        if (data == null)
            return;

        if (!string.IsNullOrEmpty(data.storyId))
            _storyId = data.storyId;

        ClearRuntimeState();

        if (data.history != null)
        {
            foreach (var line in data.history)
            {
                if (!string.IsNullOrEmpty(line))
                    history.Add(line);
            }

            TrimHistory();
        }

        if (data.wardrobe != null)
        {
            foreach (var id in data.wardrobe)
            {
                if (!string.IsNullOrEmpty(id))
                    ownedClothes.Add(id);
            }
        }

        if (data.equippedClothes != null)
        {
            foreach (var pair in data.equippedClothes)
            {
                if (pair != null && !string.IsNullOrEmpty(pair.key) && !string.IsNullOrEmpty(pair.value))
                    equippedClothes[pair.key] = pair.value;
            }
        }

        if (!string.IsNullOrEmpty(data.heroOutfitId) && !HasEquippedClothing("hero:outfit"))
            equippedClothes["hero:outfit"] = data.heroOutfitId;

        if (!string.IsNullOrEmpty(data.heroHairId) && !HasEquippedClothing("hero:hair"))
            equippedClothes["hero:hair"] = data.heroHairId;

        if (!string.IsNullOrEmpty(data.heroAccessoryId) && !HasEquippedClothing("hero:accessory"))
            equippedClothes["hero:accessory"] = data.heroAccessoryId;

        if (data.statKeys != null && data.statValues != null)
        {
            int count = Mathf.Min(data.statKeys.Count, data.statValues.Count);
            for (int i = 0; i < count; i++)
            {
                if (!string.IsNullOrEmpty(data.statKeys[i]))
                    stats[data.statKeys[i]] = data.statValues[i];
            }
        }

        if (PrototypeFeatureFlags.LocalPremiumSpendEnabled)
        {
            PlayerData.SetCandlesValue(data.currency);
            PlayerData.SetHeartsValue(data.hearts);
        }

        SaveStats();
        SaveClothes();
    }

    public void AddHistory(string line)
    {
        line = SaveDataSanitizer.SanitizeHistoryLine(line);
        if (string.IsNullOrEmpty(line))
            return;

        EnsureCollections();
        history.Add(line);
        TrimHistory();
    }

    void TrimHistory()
    {
        EnsureCollections();
        int limit = Mathf.Max(1, maxHistoryEntries);
        while (history.Count > limit)
            history.RemoveAt(0);
    }

    public int GetStat(string id)
    {
        if (string.IsNullOrEmpty(id))
            return 0;

        EnsureCollections();
        return stats.TryGetValue(id, out int value) ? value : 0;
    }

    public void AddStat(string id, int value)
    {
        id = SaveDataSanitizer.SanitizeStatKey(id);
        if (string.IsNullOrEmpty(id))
            return;

        EnsureCollections();
        stats[id] = SaveDataSanitizer.ClampStatDelta(GetStat(id), value);
        SaveStats();
    }

    public void AddCurrency(int value)
    {
        currency = SaveDataSanitizer.ClampCurrencyDelta(currency, value);
    }

    public bool SpendCurrency(int value)
    {
        if (value <= 0)
        {
            Debug.LogWarning("GameState: refused non-positive currency spend.");
            return false;
        }

        if (currency < value)
            return false;

        currency = SaveDataSanitizer.ClampCurrencyDelta(currency, -value);
        return true;
    }

    public void AddWardrobe(string id) => AddClothing(id);

    public bool HasWardrobe(string id) => HasClothing(id);

    public int GetInt(string key)
    {
        return GetStat(SaveDataSanitizer.SanitizeStatKey(key));
    }

    public void SetInt(string key, int value)
    {
        key = SaveDataSanitizer.SanitizeStatKey(key);
        if (string.IsNullOrEmpty(key))
            return;

        EnsureCollections();
        stats[key] = SaveDataSanitizer.ClampStatValue(value);
        SaveStats();
    }

    void SaveStats()
    {
        EnsureCollections();

        string data = "";
        int saved = 0;
        foreach (var kv in stats)
        {
            string key = SaveDataSanitizer.SanitizeStatKey(kv.Key);
            if (!string.IsNullOrEmpty(key))
            {
                data += key + ":" + SaveDataSanitizer.ClampStatValue(kv.Value) + ";";
                saved++;
                if (saved >= SaveDataSanitizer.MaxStatEntries)
                    break;
            }
        }

        try
        {
            PlayerPrefs.SetString(StatsKey, ProtectPrefsPayload(data, "stats"));
            LocalSecurePrefs.MarkSecure(StatsKey);
            PlayerPrefs.Save();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"GameState: failed to save stats for '{_storyId}': {exception.Message}", this);
        }
    }

    public void LoadStats()
    {
        EnsureCollections();
        stats.Clear();

        string data = LoadPrefsPayload(StatsKey, "stats", out bool wasProtected);
        if (string.IsNullOrEmpty(data))
            return;

        var pairs = data.Split(';');
        foreach (var p in pairs)
        {
            if (stats.Count >= SaveDataSanitizer.MaxStatEntries)
                break;

            if (string.IsNullOrEmpty(p))
                continue;

            var kv = p.Split(new[] { ':' }, 2);
            if (kv.Length < 2 || string.IsNullOrEmpty(kv[0]))
                continue;

            string key = SaveDataSanitizer.SanitizeStatKey(kv[0]);
            if (string.IsNullOrEmpty(key))
                continue;

            int.TryParse(kv[1], out int value);
            stats[key] = SaveDataSanitizer.ClampStatValue(value);
        }

        if (!wasProtected)
            SaveStats();
    }

    public void AddClothing(string id)
    {
        id = SaveDataSanitizer.SanitizeIdentifier(id);
        if (string.IsNullOrEmpty(id))
            return;

        EnsureCollections();
        bool added = ownedClothes.Add(id);
        SaveClothes();

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(GameState),
            nameof(AddClothing),
            "[GAMESTATE][WARDROBE] Clothing ownership recorded.",
            LogMetadata.Of(
                "storyId", _storyId,
                "itemId", id,
                "added", added,
                "ownedCount", ownedClothes.Count,
                "owned", CompactStrings(ownedClothes)));
    }

    public int AddClothingRange(IEnumerable<string> itemIds)
    {
        if (itemIds == null)
            return 0;

        EnsureCollections();
        int addedCount = 0;
        foreach (string itemId in itemIds)
        {
            if (ownedClothes.Count >= SaveDataSanitizer.MaxWardrobeEntries)
                break;

            string safeItemId = SaveDataSanitizer.SanitizeIdentifier(itemId);
            if (!string.IsNullOrEmpty(safeItemId) && ownedClothes.Add(safeItemId))
                addedCount++;
        }

        if (addedCount <= 0)
            return 0;

        SaveClothes();

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(GameState),
            nameof(AddClothingRange),
            "[GAMESTATE][WARDROBE] Clothing ownership merged.",
            LogMetadata.Of(
                "storyId", _storyId,
                "addedCount", addedCount,
                "ownedCount", ownedClothes.Count,
                "owned", CompactStrings(ownedClothes)));

        return addedCount;
    }

    public bool HasClothing(string id)
    {
        id = SaveDataSanitizer.SanitizeIdentifier(id);
        if (string.IsNullOrEmpty(id))
            return false;

        EnsureCollections();
        return ownedClothes.Contains(id);
    }

    public void EquipClothing(string characterId, string clothingId)
    {
        characterId = SaveDataSanitizer.SanitizeIdentifier(characterId);
        clothingId = SaveDataSanitizer.SanitizeIdentifier(clothingId);
        if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(clothingId))
            return;

        EnsureCollections();
        equippedClothes[characterId] = clothingId;
        SaveClothes();

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(GameState),
            nameof(EquipClothing),
            "[GAMESTATE][WARDROBE] Clothing equipped.",
            LogMetadata.Of(
                "storyId", _storyId,
                "equipKey", characterId,
                "itemId", clothingId,
                "equipped", CompactEquipped(equippedClothes)));
    }

    public string GetEquipped(string characterId)
    {
        characterId = SaveDataSanitizer.SanitizeIdentifier(characterId);
        if (string.IsNullOrEmpty(characterId))
            return null;

        EnsureCollections();
        return equippedClothes.TryGetValue(characterId, out string clothingId) ? clothingId : null;
    }

    bool HasEquippedClothing(string characterId)
    {
        characterId = SaveDataSanitizer.SanitizeIdentifier(characterId);
        EnsureCollections();
        return !string.IsNullOrEmpty(characterId) &&
               equippedClothes.TryGetValue(characterId, out string clothingId) &&
               !string.IsNullOrEmpty(clothingId);
    }

    void SaveClothes()
    {
        EnsureCollections();

        string data = "";
        int equippedSaved = 0;
        foreach (var kv in equippedClothes)
        {
            string key = SaveDataSanitizer.SanitizeIdentifier(kv.Key);
            string value = SaveDataSanitizer.SanitizeIdentifier(kv.Value);
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
            {
                data += key + ":" + value + ";";
                equippedSaved++;
                if (equippedSaved >= SaveDataSanitizer.MaxEquippedEntries)
                    break;
            }
        }

        try
        {
            PlayerPrefs.SetString(OwnedKey, ProtectPrefsPayload(string.Join(",", SanitizeOwnedClothesForSave()), "owned"));
            PlayerPrefs.SetString(EquippedKey, ProtectPrefsPayload(data, "equipped"));
            LocalSecurePrefs.MarkSecure(OwnedKey);
            LocalSecurePrefs.MarkSecure(EquippedKey);
            PlayerPrefs.Save();

            AppLogger.DebugLog(
                AppLogCategory.SaveSystem,
                nameof(GameState),
                nameof(SaveClothes),
                "[GAMESTATE][SAVE] Local wardrobe PlayerPrefs saved.",
                LogMetadata.Of(
                    "storyId", _storyId,
                    "ownedKey", OwnedKey,
                    "equippedKey", EquippedKey,
                    "ownedCount", ownedClothes.Count,
                    "equippedCount", equippedClothes.Count,
                    "owned", CompactStrings(ownedClothes),
                    "equipped", CompactEquipped(equippedClothes)));
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"GameState: failed to save wardrobe for '{_storyId}': {exception.Message}", this);
        }
    }

    public void LoadClothes()
    {
        EnsureCollections();
        ownedClothes.Clear();
        equippedClothes.Clear();

        var owned = LoadPrefsPayload(OwnedKey, "owned", out bool ownedWasProtected);
        if (!string.IsNullOrEmpty(owned))
        {
            foreach (var id in owned.Split(','))
            {
                if (ownedClothes.Count >= SaveDataSanitizer.MaxWardrobeEntries)
                    break;

                string safeId = SaveDataSanitizer.SanitizeIdentifier(id);
                if (!string.IsNullOrEmpty(safeId))
                    ownedClothes.Add(safeId);
            }
        }

        var eq = LoadPrefsPayload(EquippedKey, "equipped", out bool equippedWasProtected);
        if (string.IsNullOrEmpty(eq))
        {
            if (!ownedWasProtected && ownedClothes.Count > 0)
                SaveClothes();

            AppLogger.Info(
                AppLogCategory.SaveSystem,
                nameof(GameState),
                nameof(LoadClothes),
                "[GAMESTATE][LOAD] Local wardrobe PlayerPrefs loaded without equipped entries.",
                LogMetadata.Of(
                    "storyId", _storyId,
                    "ownedKey", OwnedKey,
                    "equippedKey", EquippedKey,
                    "ownedProtected", ownedWasProtected,
                    "equippedProtected", equippedWasProtected,
                    "ownedCount", ownedClothes.Count,
                    "equippedCount", equippedClothes.Count,
                    "owned", CompactStrings(ownedClothes),
                    "equipped", CompactEquipped(equippedClothes)));
            return;
        }

        var pairs = eq.Split(';');
        foreach (var p in pairs)
        {
            if (equippedClothes.Count >= SaveDataSanitizer.MaxEquippedEntries)
                break;

            if (!TryParseEquippedPair(p, out string key, out string value))
                continue;

            equippedClothes[key] = value;
        }

        if (!ownedWasProtected || !equippedWasProtected)
            SaveClothes();

        AppLogger.Info(
            AppLogCategory.SaveSystem,
            nameof(GameState),
            nameof(LoadClothes),
            "[GAMESTATE][LOAD] Local wardrobe PlayerPrefs loaded.",
            LogMetadata.Of(
                "storyId", _storyId,
                "ownedKey", OwnedKey,
                "equippedKey", EquippedKey,
                "ownedProtected", ownedWasProtected,
                "equippedProtected", equippedWasProtected,
                "ownedCount", ownedClothes.Count,
                "equippedCount", equippedClothes.Count,
                "owned", CompactStrings(ownedClothes),
                "equipped", CompactEquipped(equippedClothes)));
    }

    static string CompactStrings(IEnumerable<string> values, int maxItems = 24)
    {
        if (values == null)
            return "";

        maxItems = Mathf.Clamp(maxItems, 1, 64);
        var items = new List<string>();
        int total = 0;
        foreach (string raw in values)
        {
            total++;
            if (items.Count >= maxItems)
                continue;

            string value = SaveDataSanitizer.SanitizeIdentifier(raw);
            if (!string.IsNullOrEmpty(value))
                items.Add(value);
        }

        if (total > items.Count)
            items.Add("+" + (total - items.Count));

        return string.Join(",", items);
    }

    static string CompactEquipped(Dictionary<string, string> values, int maxItems = 24)
    {
        if (values == null || values.Count == 0)
            return "";

        maxItems = Mathf.Clamp(maxItems, 1, 64);
        var items = new List<string>();
        int total = 0;
        foreach (var kvp in values)
        {
            total++;
            if (items.Count >= maxItems)
                continue;

            string key = SaveDataSanitizer.SanitizeIdentifier(kvp.Key);
            string value = SaveDataSanitizer.SanitizeIdentifier(kvp.Value);
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                items.Add(key + ":" + value);
        }

        if (total > items.Count)
            items.Add("+" + (total - items.Count));

        return string.Join(",", items);
    }

    void EnsureCollections()
    {
        if (stats == null)
            stats = new Dictionary<string, int>();

        if (history == null)
            history = new List<string>();

        if (ownedClothes == null)
            ownedClothes = new HashSet<string>();

        if (equippedClothes == null)
            equippedClothes = new Dictionary<string, string>();
    }

    void ClearRuntimeState()
    {
        EnsureCollections();
        stats.Clear();
        ownedClothes.Clear();
        equippedClothes.Clear();
        history.Clear();
    }

    string SafeGetString(string key, string defaultValue)
    {
        try
        {
            return PlayerPrefs.GetString(key, defaultValue);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"GameState: failed to load '{key}': {exception.Message}", this);
            return defaultValue;
        }
    }

    string ProtectPrefsPayload(string payload, string purpose)
    {
        string protectedPayload = LocalSaveSecurity.ProtectText(payload ?? "", GetPrefsPurpose(purpose));
        if (string.IsNullOrEmpty(protectedPayload))
            throw new System.InvalidOperationException("Failed to protect local GameState payload.");

        return protectedPayload;
    }

    string LoadPrefsPayload(string key, string purpose, out bool wasProtected)
    {
        wasProtected = false;
        string stored = SafeGetString(key, "");
        if (string.IsNullOrEmpty(stored))
            return "";

        if (stored.Length > MaxPrefsPayloadChars)
        {
            DeletePrefsKey(key);
            return "";
        }

        if (!LocalSaveSecurity.TryUnprotectText(stored, GetPrefsPurpose(purpose), out string payload, out wasProtected))
        {
            Debug.LogWarning($"GameState: ignored tampered local payload '{key}'.", this);
            DeletePrefsKey(key);
            return "";
        }

        if (!wasProtected && LocalSecurePrefs.HasSecureMarker(key))
        {
            Debug.LogWarning($"GameState: ignored downgraded local payload '{key}'.", this);
            DeletePrefsKey(key);
            return "";
        }

        if (wasProtected)
            LocalSecurePrefs.EnsureSecureMarker(key);

        return payload;
    }

    string GetPrefsPurpose(string purpose)
    {
        return "gamestate:" + SaveDataSanitizer.SanitizeIdentifier(purpose) + ":" + SaveDataSanitizer.SafeKeyPart(_storyId, "global");
    }

    void DeletePrefsKey(string key)
    {
        try
        {
            LocalSecurePrefs.Delete(key);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"GameState: failed to delete invalid key '{key}': {exception.Message}", this);
        }
    }

    static bool TryParseEquippedPair(string raw, out string key, out string value)
    {
        key = "";
        value = "";

        if (string.IsNullOrEmpty(raw))
            return false;

        int separator = raw.LastIndexOf(':');
        if (separator <= 0 || separator >= raw.Length - 1)
            return false;

        key = raw.Substring(0, separator);
        value = raw.Substring(separator + 1);
        key = SaveDataSanitizer.SanitizeIdentifier(key);
        value = SaveDataSanitizer.SanitizeIdentifier(value);
        return !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value);
    }

    List<string> SanitizeOwnedClothesForSave()
    {
        List<string> result = new List<string>();
        foreach (string id in ownedClothes)
        {
            string safeId = SaveDataSanitizer.SanitizeIdentifier(id);
            if (!string.IsNullOrEmpty(safeId) && !result.Contains(safeId))
            {
                result.Add(safeId);
                if (result.Count >= SaveDataSanitizer.MaxWardrobeEntries)
                    break;
            }
        }

        return result;
    }

}
