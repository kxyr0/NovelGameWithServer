using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class StoryJsonAssetResolver
{
    private readonly Dictionary<string, CharacterData> _transientCharacters =
        new Dictionary<string, CharacterData>(System.StringComparer.OrdinalIgnoreCase);

    public virtual string CacheKey => GetType().FullName;

    public virtual CharacterData ResolveCharacter(string id, string displayName = null)
    {
        string key = FirstNonEmpty(id, displayName);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var character = Resources.Load<CharacterData>(key);
        if (character != null)
            return character;

        if (_transientCharacters.TryGetValue(key, out character) && character != null)
            return character;

        Debug.LogError(
            "[StoryJson] Character '" + key + "' was not found. " +
            "Add it to ChapterData.jsonAssetLibrary or Resources. " +
            "A temporary character will be used to prevent a crash.");

        character = ScriptableObject.CreateInstance<CharacterData>();
        character.hideFlags = HideFlags.DontSave;
        character.name = "JsonCharacter_" + key;
        character.characterName = FirstNonEmpty(displayName, id);
        _transientCharacters[key] = character;
        return character;
    }

    public virtual ClothingItem ResolveClothing(string id)
    {
        return ResolveResource<ClothingItem>(id);
    }

    public virtual Sprite ResolveSprite(string id)
    {
        return ResolveResource<Sprite>(id);
    }

    public virtual VideoClip ResolveVideoClip(string id)
    {
        return ResolveResource<VideoClip>(id);
    }

    public virtual TextAsset ResolveTextAsset(string id)
    {
        return ResolveResource<TextAsset>(id);
    }

    public virtual AudioClip ResolveAudioClip(string id)
    {
        return ResolveResource<AudioClip>(id);
    }

    public virtual DialogueStyle ResolveDialogueStyle(string id)
    {
        return ResolveResource<DialogueStyle>(id);
    }

    public virtual string GetAssetId(Object asset)
    {
        return asset != null ? asset.name : "";
    }

    public virtual string GetCharacterId(CharacterData character)
    {
        if (character == null)
            return "";

        string assetId = GetAssetId(character);
        return !string.IsNullOrWhiteSpace(assetId) ? assetId : character.characterName;
    }

    public virtual string GetClothingId(ClothingItem clothing)
    {
        if (clothing == null)
            return "";

        return !string.IsNullOrWhiteSpace(clothing.id) ? clothing.id : GetAssetId(clothing);
    }

    protected static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "";
    }

    protected static T ResolveResource<T>(string id) where T : Object
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        T resource = Resources.Load<T>(id);
        if (resource != null)
            return resource;

#if UNITY_EDITOR
        return ResolveEditorAsset<T>(id);
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    private static T ResolveEditorAsset<T>(string id) where T : Object
    {
        string normalizedId = NormalizeAssetId(id);

        T byPath = AssetDatabase.LoadAssetAtPath<T>(normalizedId);
        if (byPath != null)
            return byPath;

        Object[] assetsAtPath = AssetDatabase.LoadAllAssetsAtPath(normalizedId);
        foreach (Object asset in assetsAtPath)
        {
            if (asset is T typed)
                return typed;
        }

        string[] guids = AssetDatabase.FindAssets(normalizedId + " t:" + typeof(T).Name);
        T fallback = null;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T exact = FindAssetAtPath<T>(path, normalizedId, exactName: true);
            if (exact != null)
                return exact;

            if (fallback == null)
                fallback = FindAssetAtPath<T>(path, normalizedId, exactName: false);
        }

        return fallback;
    }

    private static T FindAssetAtPath<T>(string path, string normalizedId, bool exactName) where T : Object
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object asset in assets)
        {
            T typed = asset as T;
            if (typed == null)
                continue;

            string normalizedName = NormalizeAssetId(typed.name);
            if (exactName && normalizedName == normalizedId)
                return typed;

            if (!exactName && normalizedName.Contains(normalizedId))
                return typed;
        }

        if (!exactName)
            return AssetDatabase.LoadAssetAtPath<T>(path);

        return null;
    }

    private static string NormalizeAssetId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().Replace('\\', '/');
    }
#endif
}
