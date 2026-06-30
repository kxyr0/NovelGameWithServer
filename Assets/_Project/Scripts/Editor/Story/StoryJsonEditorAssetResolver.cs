#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

public sealed class StoryJsonEditorAssetResolver : StoryJsonAssetResolver
{
    public override CharacterData ResolveCharacter(string id, string displayName = null)
    {
        var character = ResolveProjectAsset<CharacterData>(id);
        if (character != null)
            return character;

        string key = FirstNonEmpty(id, displayName);
        if (!string.IsNullOrWhiteSpace(key))
        {
            character = LoadAllAssets<CharacterData>()
                .FirstOrDefault(asset =>
                    string.Equals(asset.name, key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(asset.characterName, key, StringComparison.OrdinalIgnoreCase));
            if (character != null)
                return character;
        }

        return base.ResolveCharacter(id, displayName);
    }

    public override ClothingItem ResolveClothing(string id)
    {
        var clothing = ResolveProjectAsset<ClothingItem>(id);
        if (clothing != null)
            return clothing;

        return LoadAllAssets<ClothingItem>()
            .FirstOrDefault(asset =>
                string.Equals(asset.id, id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(asset.name, id, StringComparison.OrdinalIgnoreCase));
    }

    public override Sprite ResolveSprite(string id)
    {
        return ResolveProjectAsset<Sprite>(id);
    }

    public override VideoClip ResolveVideoClip(string id)
    {
        return ResolveProjectAsset<VideoClip>(id);
    }

    public override TextAsset ResolveTextAsset(string id)
    {
        return ResolveProjectAsset<TextAsset>(id);
    }

    public override AudioClip ResolveAudioClip(string id)
    {
        return ResolveProjectAsset<AudioClip>(id);
    }

    public override DialogueStyle ResolveDialogueStyle(string id)
    {
        return ResolveProjectAsset<DialogueStyle>(id);
    }

    public override string GetAssetId(UnityEngine.Object asset)
    {
        if (asset == null)
            return "";

        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
            return asset.name;

        string guid = AssetDatabase.AssetPathToGUID(path);
        return string.IsNullOrEmpty(guid) ? path : guid;
    }

    private static T ResolveProjectAsset<T>(string id) where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        string value = id.Trim();

        if (value.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            var byPath = AssetDatabase.LoadAssetAtPath<T>(value);
            if (byPath != null)
                return byPath;
        }

        string guidPath = AssetDatabase.GUIDToAssetPath(value);
        if (!string.IsNullOrEmpty(guidPath))
        {
            var byGuid = AssetDatabase.LoadAssetAtPath<T>(guidPath);
            if (byGuid != null)
                return byGuid;
        }

        var byResource = Resources.Load<T>(value);
        if (byResource != null)
            return byResource;

        string typeName = typeof(T).Name;
        foreach (string guid in AssetDatabase.FindAssets(value + " t:" + typeName))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
        }

        return null;
    }

    private static T[] LoadAllAssets<T>() where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToArray();
    }
}
#endif
