using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[Serializable]
public sealed class DivinationCardBackendDto
{
    public string id;
    public string cardId;
    public string tarotCardId;
    public string key;
    public string slug;
    public string name;
    public string title;
    public string description;
    public string resultText;
    public string imageUrl;
    public string image;
    public string url;
    public float weight;
    public float probability;
    public bool isPublished;
    public bool published;
    public bool active;
    public bool isActive;
    public DivinationRewardDto reward;
    public DivinationRewardDto[] rewards;

    [NonSerialized] public string rawJson;
    [NonSerialized] public bool hasPublishedValue;
    [NonSerialized] public bool hasActiveValue;

    public string EffectiveId => FirstNonEmpty(id, cardId, tarotCardId, key, slug);
    public string EffectiveTitle => FirstNonEmpty(title, name);
    public string EffectiveDescription => description ?? "";
    public string EffectiveImageUrl => FirstNonEmpty(imageUrl, image, url);

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return "";
    }
}

[Serializable]
public sealed class DivinationCooldownDto
{
    public bool available;
    public bool canDraw;
    public string nextAvailableAtUtc;
    public string nextDrawAt;
    public string lastDrawAt;
    public int remainingSeconds;

    [NonSerialized] public string rawJson;
    [NonSerialized] public bool hasAvailability;

    public bool IsAvailable(bool defaultValue = true)
    {
        return hasAvailability ? (available || canDraw) : defaultValue;
    }
}

[Serializable]
public sealed class DivinationRewardDto
{
    public string type;
    public string id;
    public string itemId;
    public string currency;
    public string displayName;
    public string title;
    public int amount;
    public int value;
    public int count;
    public int hearts;
    public int candles;
    public int subscriptionDays;

    [NonSerialized] public string rawJson;
}

[Serializable]
public sealed class DivinationTarotDrawResponseDto
{
    public bool ok;
    public bool canDraw;
    public bool available;
    public DivinationCardBackendDto card;
    public DivinationCardBackendDto tarotCard;
    public DivinationRewardDto reward;
    public DivinationRewardDto[] rewards;
    public DivinationCooldownDto cooldown;
    public string nextDrawAt;
    public string lastDrawAt;
    public string nextAvailableAtUtc;
    public int remainingSeconds;

    [NonSerialized] public string rawJson;
    [NonSerialized] public bool hasOkValue;
    [NonSerialized] public bool hasAvailability;

    public DivinationCardBackendDto SelectedCard => card ?? tarotCard;

    public bool IsDrawAvailable(bool defaultValue = true)
    {
        if (cooldown != null && cooldown.hasAvailability)
            return cooldown.IsAvailable(defaultValue);

        return hasAvailability ? (available || canDraw) : defaultValue;
    }
}

[Serializable]
public sealed class DivinationTarotStatusResponseDto
{
    public bool canDraw;
    public bool available;
    public string lastDrawAt;
    public string nextDrawAt;
    public string nextAvailableAtUtc;
    public int remainingSeconds;
    public DivinationCooldownDto cooldown;

    [NonSerialized] public string rawJson;
    [NonSerialized] public bool hasAvailability;

    public bool IsDrawAvailable(bool defaultValue = true)
    {
        if (cooldown != null && cooldown.hasAvailability)
            return cooldown.IsAvailable(defaultValue);

        return hasAvailability ? (available || canDraw) : defaultValue;
    }
}

public static class DivinationBackendJsonParser
{
    private const string LogPrefix = "[Divination]";

    public static DivinationTarotDrawResponseDto ParseDrawResponse(string json)
    {
        string payload = ResolvePayload(json);
        if (string.IsNullOrWhiteSpace(payload) || !NetworkJson.LooksLikeJsonObject(payload))
        {
            Debug.LogWarning(LogPrefix + " backend JSON parse failed: draw payload is empty or not an object.");
            return null;
        }

        DivinationTarotDrawResponseDto response = NetworkJson.FromJson<DivinationTarotDrawResponseDto>(payload) ??
                                                  new DivinationTarotDrawResponseDto();
        response.rawJson = json;
        response.hasOkValue = NetworkJson.GetRawValue(payload, "ok") != null;
        if (response.hasOkValue)
            response.ok = NetworkJson.GetBool(payload, "ok", true);

        ApplyAvailability(payload, out response.hasAvailability, out response.available, out response.canDraw);
        response.lastDrawAt = FirstNonEmpty(response.lastDrawAt, NetworkJson.GetString(payload, "lastDrawAt"));
        response.nextDrawAt = FirstNonEmpty(response.nextDrawAt, NetworkJson.GetString(payload, "nextDrawAt"));
        response.nextAvailableAtUtc = FirstNonEmpty(
            response.nextAvailableAtUtc,
            NetworkJson.GetString(payload, "nextAvailableAtUtc"),
            response.nextDrawAt);
        response.remainingSeconds = Mathf.Max(
            response.remainingSeconds,
            NetworkJson.GetInt(payload, "remainingSeconds", 0));

        response.card = ParseCardFromContainer(payload);
        response.tarotCard = response.card;
        response.cooldown = ParseCooldown(payload);
        response.rewards = ParseRewards(payload, response.card);
        response.reward = response.rewards != null && response.rewards.Length > 0 ? response.rewards[0] : null;

        if (response.cooldown != null)
        {
            if (!string.IsNullOrWhiteSpace(response.cooldown.lastDrawAt))
                response.lastDrawAt = response.cooldown.lastDrawAt;
            if (!string.IsNullOrWhiteSpace(response.cooldown.nextDrawAt))
                response.nextDrawAt = response.cooldown.nextDrawAt;
            if (!string.IsNullOrWhiteSpace(response.cooldown.nextAvailableAtUtc))
                response.nextAvailableAtUtc = response.cooldown.nextAvailableAtUtc;
            if (response.cooldown.remainingSeconds > 0)
                response.remainingSeconds = response.cooldown.remainingSeconds;
        }

        if (response.card == null && !response.IsDrawAvailable(true))
            return response;

        if (response.card == null)
            Debug.LogWarning(LogPrefix + " backend JSON parse failed: draw response has no card.");
        else
            Debug.Log(LogPrefix + " backend JSON loaded: card id '" + response.card.EffectiveId + "'.");

        return response;
    }

    public static DivinationTarotStatusResponseDto ParseStatusResponse(string json)
    {
        string payload = ResolvePayload(json);
        if (string.IsNullOrWhiteSpace(payload) || !NetworkJson.LooksLikeJsonObject(payload))
        {
            Debug.LogWarning(LogPrefix + " backend JSON parse failed: status payload is empty or not an object.");
            return null;
        }

        DivinationTarotStatusResponseDto response = NetworkJson.FromJson<DivinationTarotStatusResponseDto>(payload) ??
                                                    new DivinationTarotStatusResponseDto();
        response.rawJson = json;
        ApplyAvailability(payload, out response.hasAvailability, out response.available, out response.canDraw);
        response.lastDrawAt = FirstNonEmpty(response.lastDrawAt, NetworkJson.GetString(payload, "lastDrawAt"));
        response.nextDrawAt = FirstNonEmpty(response.nextDrawAt, NetworkJson.GetString(payload, "nextDrawAt"));
        response.nextAvailableAtUtc = FirstNonEmpty(
            response.nextAvailableAtUtc,
            NetworkJson.GetString(payload, "nextAvailableAtUtc"),
            response.nextDrawAt);
        response.remainingSeconds = Mathf.Max(
            response.remainingSeconds,
            NetworkJson.GetInt(payload, "remainingSeconds", 0));
        response.cooldown = ParseCooldown(payload);

        if (response.cooldown == null)
        {
            response.cooldown = new DivinationCooldownDto
            {
                available = response.available,
                canDraw = response.canDraw,
                hasAvailability = response.hasAvailability,
                lastDrawAt = response.lastDrawAt,
                nextDrawAt = response.nextDrawAt,
                nextAvailableAtUtc = response.nextAvailableAtUtc,
                remainingSeconds = response.remainingSeconds,
                rawJson = payload
            };
        }

        Debug.Log(LogPrefix + " cooldown state loaded: canDraw=" + response.IsDrawAvailable(true));
        return response;
    }

    private static string ResolvePayload(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "";

        string trimmed = json.Trim();
        if (!NetworkJson.LooksLikeJsonObject(trimmed))
            return trimmed;

        string nested = FirstNonEmptyRaw(
            NetworkJson.GetRawValue(trimmed, "data"),
            NetworkJson.GetRawValue(trimmed, "result"),
            NetworkJson.GetRawValue(trimmed, "draw"));

        if (!string.IsNullOrWhiteSpace(nested) && NetworkJson.LooksLikeJsonObject(nested))
            return nested;

        return trimmed;
    }

    private static DivinationCardBackendDto ParseCardFromContainer(string json)
    {
        string rawCard = FirstNonEmptyRaw(
            NetworkJson.GetRawValue(json, "card"),
            NetworkJson.GetRawValue(json, "tarotCard"),
            NetworkJson.GetRawValue(json, "selectedCard"));

        if (!string.IsNullOrWhiteSpace(rawCard) && NetworkJson.LooksLikeJsonObject(rawCard))
            return ParseCard(rawCard);

        DivinationCardBackendDto directCard = ParseCard(json);
        return HasAnyCardIdentity(directCard) ? directCard : null;
    }

    public static DivinationCardBackendDto ParseCard(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson) || !NetworkJson.LooksLikeJsonObject(rawJson))
            return null;

        DivinationCardBackendDto card = NetworkJson.FromJson<DivinationCardBackendDto>(rawJson) ??
                                        new DivinationCardBackendDto();
        card.rawJson = rawJson;
        card.id = FirstNonEmpty(
            card.id,
            NetworkJson.GetFirstString(rawJson, "id", "cardId", "tarotCardId", "key", "slug"));
        card.cardId = FirstNonEmpty(card.cardId, NetworkJson.GetString(rawJson, "cardId"));
        card.tarotCardId = FirstNonEmpty(card.tarotCardId, NetworkJson.GetString(rawJson, "tarotCardId"));
        card.key = FirstNonEmpty(card.key, NetworkJson.GetString(rawJson, "key"));
        card.slug = FirstNonEmpty(card.slug, NetworkJson.GetString(rawJson, "slug"));
        card.name = FirstNonEmpty(card.name, NetworkJson.GetString(rawJson, "name"));
        card.title = FirstNonEmpty(card.title, NetworkJson.GetString(rawJson, "title"));
        card.description = FirstNonEmpty(
            card.description,
            GetStringOnly(rawJson, "description"),
            GetStringOnly(rawJson, "text"));
        card.resultText = FirstNonEmpty(
            card.resultText,
            GetStringOnly(rawJson, "resultText"));
        card.imageUrl = FirstNonEmpty(
            card.imageUrl,
            NetworkJson.GetFirstString(rawJson, "imageUrl", "image", "url"));
        card.weight = Mathf.Max(card.weight, GetFloat(rawJson, "weight", 0f));
        card.probability = Mathf.Max(card.probability, GetFloat(rawJson, "probability", 0f));

        bool hasPublished = NetworkJson.GetRawValue(rawJson, "isPublished") != null ||
                            NetworkJson.GetRawValue(rawJson, "published") != null;
        if (hasPublished)
        {
            card.hasPublishedValue = true;
            card.isPublished = NetworkJson.GetBool(rawJson, "isPublished", card.isPublished);
            card.published = NetworkJson.GetBool(rawJson, "published", card.published);
        }

        bool hasActive = NetworkJson.GetRawValue(rawJson, "active") != null ||
                         NetworkJson.GetRawValue(rawJson, "isActive") != null;
        if (hasActive)
        {
            card.hasActiveValue = true;
            card.active = NetworkJson.GetBool(rawJson, "active", card.active);
            card.isActive = NetworkJson.GetBool(rawJson, "isActive", card.isActive);
        }

        card.rewards = ParseRewards(rawJson, null);
        card.reward = card.rewards != null && card.rewards.Length > 0 ? card.rewards[0] : null;
        return card;
    }

    public static DivinationCooldownDto ParseCooldown(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || !NetworkJson.LooksLikeJsonObject(json))
            return null;

        string rawCooldown = NetworkJson.GetRawValue(json, "cooldown");
        string source = !string.IsNullOrWhiteSpace(rawCooldown) && NetworkJson.LooksLikeJsonObject(rawCooldown)
            ? rawCooldown
            : json;

        bool hasAnyCooldownField =
            NetworkJson.GetRawValue(source, "available") != null ||
            NetworkJson.GetRawValue(source, "canDraw") != null ||
            NetworkJson.GetRawValue(source, "nextAvailableAtUtc") != null ||
            NetworkJson.GetRawValue(source, "nextDrawAt") != null ||
            NetworkJson.GetRawValue(source, "remainingSeconds") != null ||
            NetworkJson.GetRawValue(source, "lastDrawAt") != null;
        if (!hasAnyCooldownField)
            return null;

        DivinationCooldownDto cooldown = NetworkJson.FromJson<DivinationCooldownDto>(source) ??
                                         new DivinationCooldownDto();
        cooldown.rawJson = source;
        ApplyAvailability(source, out cooldown.hasAvailability, out cooldown.available, out cooldown.canDraw);
        cooldown.lastDrawAt = FirstNonEmpty(cooldown.lastDrawAt, NetworkJson.GetString(source, "lastDrawAt"));
        cooldown.nextDrawAt = FirstNonEmpty(cooldown.nextDrawAt, NetworkJson.GetString(source, "nextDrawAt"));
        cooldown.nextAvailableAtUtc = FirstNonEmpty(
            cooldown.nextAvailableAtUtc,
            NetworkJson.GetString(source, "nextAvailableAtUtc"),
            cooldown.nextDrawAt);
        cooldown.remainingSeconds = Mathf.Max(
            cooldown.remainingSeconds,
            NetworkJson.GetInt(source, "remainingSeconds", 0));
        return cooldown;
    }

    public static DivinationRewardDto[] ParseRewards(string json, DivinationCardBackendDto card)
    {
        var rewards = new List<DivinationRewardDto>();
        ParseRewardArray(NetworkJson.GetRawValue(json, "rewards"), rewards);
        ParseRewardObject(NetworkJson.GetRawValue(json, "reward"), rewards);

        if (card != null && !string.IsNullOrWhiteSpace(card.rawJson))
        {
            ParseRewardArray(NetworkJson.GetRawValue(card.rawJson, "rewards"), rewards);
            ParseRewardObject(NetworkJson.GetRawValue(card.rawJson, "reward"), rewards);
        }

        if (rewards.Count == 0)
        {
            DivinationRewardDto legacy = ParseLegacyReward(json);
            if (legacy != null)
                rewards.Add(legacy);
        }

        if (rewards.Count == 0)
            return new DivinationRewardDto[0];

        Debug.Log(LogPrefix + " reward parsed: " + rewards.Count + " item(s).");
        return rewards.ToArray();
    }

    private static DivinationRewardDto ParseLegacyReward(string json)
    {
        int hearts = Mathf.Max(0, NetworkJson.GetInt(json, "heartsReward", NetworkJson.GetInt(json, "hearts", 0)));
        int candles = Mathf.Max(0, NetworkJson.GetInt(json, "candlesReward", NetworkJson.GetInt(json, "candles", 0)));
        int subscriptionDays = Mathf.Max(0, NetworkJson.GetInt(json, "subscriptionDays", 0));
        if (hearts <= 0 && candles <= 0 && subscriptionDays <= 0)
            return null;

        return new DivinationRewardDto
        {
            type = "legacy",
            hearts = hearts,
            candles = candles,
            subscriptionDays = subscriptionDays,
            rawJson = json
        };
    }

    private static void ParseRewardArray(string rawArray, List<DivinationRewardDto> rewards)
    {
        if (string.IsNullOrWhiteSpace(rawArray) || !rawArray.TrimStart().StartsWith("[", StringComparison.Ordinal))
            return;

        foreach (string rawItem in NetworkJson.GetArrayItems(rawArray))
            ParseRewardObject(rawItem, rewards);
    }

    private static void ParseRewardObject(string rawReward, List<DivinationRewardDto> rewards)
    {
        if (string.IsNullOrWhiteSpace(rawReward) || !NetworkJson.LooksLikeJsonObject(rawReward))
            return;

        DivinationRewardDto reward = NetworkJson.FromJson<DivinationRewardDto>(rawReward) ??
                                     new DivinationRewardDto();
        reward.rawJson = rawReward;
        reward.type = FirstNonEmpty(reward.type, NetworkJson.GetFirstString(rawReward, "type", "rewardType"));
        reward.id = FirstNonEmpty(reward.id, NetworkJson.GetFirstString(rawReward, "id", "rewardId"));
        reward.itemId = FirstNonEmpty(reward.itemId, NetworkJson.GetString(rawReward, "itemId"));
        reward.currency = FirstNonEmpty(reward.currency, NetworkJson.GetString(rawReward, "currency"));
        reward.displayName = FirstNonEmpty(
            reward.displayName,
            NetworkJson.GetFirstString(rawReward, "displayName", "label", "name"));
        reward.title = FirstNonEmpty(reward.title, NetworkJson.GetString(rawReward, "title"));
        reward.amount = Mathf.Max(reward.amount, NetworkJson.GetInt(rawReward, "amount", 0));
        reward.value = Mathf.Max(reward.value, NetworkJson.GetInt(rawReward, "value", 0));
        reward.count = Mathf.Max(reward.count, NetworkJson.GetInt(rawReward, "count", 0));
        reward.hearts = Mathf.Max(reward.hearts, NetworkJson.GetInt(rawReward, "hearts", 0));
        reward.candles = Mathf.Max(reward.candles, NetworkJson.GetInt(rawReward, "candles", 0));
        reward.subscriptionDays = Mathf.Max(reward.subscriptionDays, NetworkJson.GetInt(rawReward, "subscriptionDays", 0));

        if (!HasRewardData(reward))
        {
            Debug.LogWarning(LogPrefix + " invalid reward data ignored.");
            return;
        }

        rewards.Add(reward);
    }

    private static bool HasRewardData(DivinationRewardDto reward)
    {
        return reward != null &&
               (!string.IsNullOrWhiteSpace(reward.type) ||
                !string.IsNullOrWhiteSpace(reward.id) ||
                !string.IsNullOrWhiteSpace(reward.itemId) ||
                !string.IsNullOrWhiteSpace(reward.currency) ||
                !string.IsNullOrWhiteSpace(reward.displayName) ||
                !string.IsNullOrWhiteSpace(reward.title) ||
                reward.amount > 0 ||
                reward.value > 0 ||
                reward.count > 0 ||
                reward.hearts > 0 ||
                reward.candles > 0 ||
                reward.subscriptionDays > 0);
    }

    private static void ApplyAvailability(string json, out bool hasAvailability, out bool available, out bool canDraw)
    {
        bool hasAvailable = NetworkJson.GetRawValue(json, "available") != null;
        bool hasCanDraw = NetworkJson.GetRawValue(json, "canDraw") != null;
        hasAvailability = hasAvailable || hasCanDraw;
        available = hasAvailable ? NetworkJson.GetBool(json, "available", false) : false;
        canDraw = hasCanDraw ? NetworkJson.GetBool(json, "canDraw", false) : false;
    }

    private static bool HasAnyCardIdentity(DivinationCardBackendDto card)
    {
        return card != null &&
               (!string.IsNullOrWhiteSpace(card.EffectiveId) ||
                !string.IsNullOrWhiteSpace(card.EffectiveTitle) ||
                !string.IsNullOrWhiteSpace(card.description));
    }

    private static string GetStringOnly(string json, string key)
    {
        string raw = NetworkJson.GetRawValue(json, key);
        if (string.IsNullOrWhiteSpace(raw) ||
            raw == "null" ||
            raw.TrimStart().StartsWith("{", StringComparison.Ordinal) ||
            raw.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            return "";
        }

        return NetworkJson.GetString(json, key);
    }

    private static float GetFloat(string json, string key, float defaultValue)
    {
        string raw = NetworkJson.GetRawValue(json, key);
        if (string.IsNullOrWhiteSpace(raw) || raw == "null")
            return defaultValue;

        if (float.TryParse(raw.Trim('"'), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            return value;

        return defaultValue;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return "";
    }

    private static string FirstNonEmptyRaw(params string[] values)
    {
        return FirstNonEmpty(values);
    }
}

public static class DivinationRewardDisplayFormatter
{
    public static string FormatRewards(IEnumerable<DivinationRewardDto> rewards)
    {
        if (rewards == null)
            return "";

        var lines = new List<string>();
        foreach (DivinationRewardDto reward in rewards)
        {
            string line = FormatReward(reward);
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
        }

        return string.Join("\n", lines.ToArray());
    }

    public static string FormatReward(DivinationRewardDto reward)
    {
        if (reward == null)
            return "";

        var parts = new List<string>();
        AddAmount(parts, reward.hearts, "hearts");
        AddAmount(parts, reward.candles, "candles");
        AddAmount(parts, reward.subscriptionDays, "subscription days");

        int genericAmount = FirstPositive(reward.amount, reward.value, reward.count);
        string displayName = FirstNonEmpty(
            reward.displayName,
            reward.title,
            reward.currency,
            reward.itemId,
            reward.id,
            reward.type);
        if (genericAmount > 0 && !string.IsNullOrWhiteSpace(displayName))
            parts.Add(displayName + " +" + genericAmount);
        else if (!string.IsNullOrWhiteSpace(displayName) && parts.Count == 0)
            parts.Add(displayName);

        return string.Join("\n", parts.ToArray());
    }

    private static void AddAmount(List<string> parts, int amount, string label)
    {
        if (amount > 0)
            parts.Add(label + " +" + amount);
    }

    private static int FirstPositive(params int[] values)
    {
        if (values == null)
            return 0;

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] > 0)
                return values[i];
        }

        return 0;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return "";
    }
}

public static class DivinationCooldownFormatter
{
    public static string Format(DivinationCooldownDto cooldown, string availableText = "")
    {
        if (cooldown == null)
            return "";

        if (cooldown.IsAvailable(false))
            return availableText ?? "";

        if (cooldown.remainingSeconds > 0)
            return "Available in " + FormatDuration(cooldown.remainingSeconds);

        string nextAt = FirstNonEmpty(cooldown.nextAvailableAtUtc, cooldown.nextDrawAt);
        if (!string.IsNullOrWhiteSpace(nextAt))
            return "Available at " + nextAt;

        return "Not available yet";
    }

    public static string FormatDuration(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        TimeSpan time = TimeSpan.FromSeconds(seconds);
        if (time.TotalDays >= 1d)
            return Mathf.FloorToInt((float)time.TotalDays) + "d " + time.Hours + "h";
        if (time.TotalHours >= 1d)
            return Mathf.FloorToInt((float)time.TotalHours) + "h " + time.Minutes + "m";
        if (time.TotalMinutes >= 1d)
            return Mathf.FloorToInt((float)time.TotalMinutes) + "m";
        return seconds + "s";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return "";
    }
}
