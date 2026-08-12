using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(
    fileName = "StoryRuntimeAssetRegistry",
    menuName = "Nocturne/Story/Runtime Asset Registry")]
public sealed class StoryRuntimeAssetRegistry : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string key;
        public UnityEngine.Object asset;
    }

    [SerializeField] private string _storyId = "";
    [SerializeField] private List<Entry> _entries = new List<Entry>();

    public string StoryId => _storyId;
    public IReadOnlyList<Entry> Entries => _entries;

#if UNITY_EDITOR
    public void EditorSetData(string storyId, List<Entry> entries)
    {
        _storyId = NormalizeStoryId(storyId);
        _entries = entries ?? new List<Entry>();
    }
#endif

    public static string NormalizeStoryId(string storyId)
    {
        return (storyId ?? "").Trim().Replace('\\', '/').Trim('/');
    }
}

public static class StoryRuntimeAssetRegistryResolver
{
    private const string ResourcesFolder = "StoryRuntimeRegistry/";

    private static readonly Dictionary<string, StoryRuntimeAssetRegistry> RegistryCache =
        new Dictionary<string, StoryRuntimeAssetRegistry>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, List<UnityEngine.Object>> ActiveIndex =
        new Dictionary<string, List<UnityEngine.Object>>(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> LoggedFallbacks =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static string _activeStoryId = "";
    private static StoryRuntimeAssetRegistry _activeRegistry;

    public static string ActiveStoryId => _activeStoryId;
    public static bool HasActiveRegistry => _activeRegistry != null;
    public static int ActiveEntryCount => _activeRegistry != null && _activeRegistry.Entries != null
        ? _activeRegistry.Entries.Count
        : 0;

    public static bool SetActiveStory(string storyId)
    {
        storyId = StoryRuntimeAssetRegistry.NormalizeStoryId(storyId);
        if (string.IsNullOrEmpty(storyId))
            return false;

        if (_activeRegistry != null &&
            string.Equals(_activeStoryId, storyId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        _activeStoryId = storyId;
        _activeRegistry = LoadRegistry(storyId);
        RebuildActiveIndex();

        Debug.Log(
            $"[STORY_ASSET_REGISTRY][ACTIVE] platform={Application.platform} storyId='{storyId}' " +
            $"loaded={_activeRegistry != null} entries={ActiveEntryCount}.");

        return _activeRegistry != null;
    }

    public static T Resolve<T>(string id) where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(id) || _activeRegistry == null)
            return null;

        foreach (string key in EnumerateLookupKeys(id))
        {
            if (!ActiveIndex.TryGetValue(key, out List<UnityEngine.Object> candidates) || candidates == null)
                continue;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] is T typed)
                {
                    LogFallbackOnce(typeof(T), id, key, typed);
                    return typed;
                }
            }
        }

        return null;
    }

    public static string DescribeActiveRegistry()
    {
        return $"storyId='{_activeStoryId}' loaded={_activeRegistry != null} entries={ActiveEntryCount}";
    }

    private static StoryRuntimeAssetRegistry LoadRegistry(string storyId)
    {
        if (RegistryCache.TryGetValue(storyId, out StoryRuntimeAssetRegistry cached))
            return cached;

        string resourceName = ResourcesFolder + MakeResourceName(storyId);
        StoryRuntimeAssetRegistry registry = Resources.Load<StoryRuntimeAssetRegistry>(resourceName);
        RegistryCache[storyId] = registry;
        return registry;
    }

    private static void RebuildActiveIndex()
    {
        ActiveIndex.Clear();
        LoggedFallbacks.Clear();

        if (_activeRegistry == null || _activeRegistry.Entries == null)
            return;

        for (int i = 0; i < _activeRegistry.Entries.Count; i++)
        {
            StoryRuntimeAssetRegistry.Entry entry = _activeRegistry.Entries[i];
            if (entry == null || entry.asset == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            string key = NormalizeKey(entry.key);
            if (string.IsNullOrEmpty(key))
                continue;

            if (!ActiveIndex.TryGetValue(key, out List<UnityEngine.Object> list))
            {
                list = new List<UnityEngine.Object>();
                ActiveIndex[key] = list;
            }

            if (!list.Contains(entry.asset))
                list.Add(entry.asset);
        }
    }

    private static IEnumerable<string> EnumerateLookupKeys(string id)
    {
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string normalized = NormalizeKey(id);
        if (!string.IsNullOrEmpty(normalized) && emitted.Add(normalized))
            yield return normalized;

        string fileName = Path.GetFileName(normalized);
        if (!string.IsNullOrEmpty(fileName) && emitted.Add(fileName))
            yield return fileName;

        string withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        if (!string.IsNullOrEmpty(withoutExtension) && emitted.Add(withoutExtension))
            yield return withoutExtension;

        if (normalized.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
        {
            string withoutAssets = normalized.Substring("assets/".Length);
            if (emitted.Add(withoutAssets))
                yield return withoutAssets;
        }

        if (normalized.StartsWith("assets/data/", StringComparison.OrdinalIgnoreCase))
        {
            string withoutData = normalized.Substring("assets/data/".Length);
            if (emitted.Add(withoutData))
                yield return withoutData;
        }
    }

    private static string NormalizeKey(string value)
    {
        return (value ?? "")
            .Trim()
            .Trim('"')
            .Replace('\\', '/')
            .Trim()
            .ToLowerInvariant();
    }

    private static string MakeResourceName(string storyId)
    {
        char[] chars = StoryRuntimeAssetRegistry.NormalizeStoryId(storyId).ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c == '/' || c == '\\' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|')
                chars[i] = '_';
        }
        return new string(chars);
    }

    private static void LogFallbackOnce(Type type, string requestedId, string matchedKey, UnityEngine.Object asset)
    {
        string token = _activeStoryId + "|" + type.FullName + "|" + requestedId;
        if (!LoggedFallbacks.Add(token))
            return;

        Debug.Log(
            $"[STORY_ASSET_REGISTRY][RESOLVED] storyId='{_activeStoryId}' type='{type.Name}' " +
            $"requested='{requestedId}' matched='{matchedKey}' asset='{asset.name}'.");
    }
}

public sealed class StoryRuntimeRegistryAssetResolver : StoryJsonAssetResolver
{
    public override string CacheKey =>
        "runtime-registry:" + StoryRuntimeAssetRegistryResolver.ActiveStoryId + ":" +
        StoryRuntimeAssetRegistryResolver.ActiveEntryCount;

    public override CharacterData ResolveCharacter(string id, string displayName = null)
    {
        CharacterData asset = StoryRuntimeAssetRegistryResolver.Resolve<CharacterData>(id);
        if (asset != null)
            return asset;

        asset = StoryRuntimeAssetRegistryResolver.Resolve<CharacterData>(displayName);
        return asset != null ? asset : base.ResolveCharacter(id, displayName);
    }

    public override ClothingItem ResolveClothing(string id)
    {
        return StoryRuntimeAssetRegistryResolver.Resolve<ClothingItem>(id) ?? base.ResolveClothing(id);
    }

    public override Sprite ResolveSprite(string id)
    {
        return StoryRuntimeAssetRegistryResolver.Resolve<Sprite>(id) ?? base.ResolveSprite(id);
    }

    public override VideoClip ResolveVideoClip(string id)
    {
        return StoryRuntimeAssetRegistryResolver.Resolve<VideoClip>(id) ?? base.ResolveVideoClip(id);
    }

    public override TextAsset ResolveTextAsset(string id)
    {
        return StoryRuntimeAssetRegistryResolver.Resolve<TextAsset>(id) ?? base.ResolveTextAsset(id);
    }

    public override AudioClip ResolveAudioClip(string id)
    {
        return StoryRuntimeAssetRegistryResolver.Resolve<AudioClip>(id) ?? base.ResolveAudioClip(id);
    }

    public override DialogueStyle ResolveDialogueStyle(string id)
    {
        return StoryRuntimeAssetRegistryResolver.Resolve<DialogueStyle>(id) ?? base.ResolveDialogueStyle(id);
    }

    public override string GetAssetId(UnityEngine.Object asset)
    {
        return asset != null ? asset.name : base.GetAssetId(asset);
    }
}
