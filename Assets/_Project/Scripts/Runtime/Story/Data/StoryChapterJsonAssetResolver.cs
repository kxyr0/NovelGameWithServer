using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Runtime JSON resolver for a concrete chapter.
///
/// Resolution order is deliberately deterministic:
/// 1) Chapter JSON asset library;
/// 2) runtime story registry (Android-safe);
/// 3) references already present in the generated StoryGraph;
/// 4) legacy Resources/editor fallback from StoryJsonAssetResolver.
///
/// The generated graph fallback is important because it already contains the
/// authoritative CharacterData references even when a runtime JSON graph was
/// rebuilt with an incomplete/missing asset binding.
/// </summary>
public sealed class StoryChapterJsonAssetResolver : StoryJsonAssetResolver
{
    private readonly StoryJsonAssetLibrary _library;
    private readonly StoryGraph _generatedGraph;

    private Dictionary<string, CharacterData> _generatedCharacters;
    private bool _generatedCharactersIndexed;

    public StoryChapterJsonAssetResolver(
        StoryJsonAssetLibrary library,
        StoryGraph generatedGraph)
    {
        _library = library;
        _generatedGraph = generatedGraph;
    }

    public override string CacheKey
    {
        get
        {
            string libraryKey = _library != null
                ? _library.GetInstanceID().ToString()
                : "none";
            string graphKey = _generatedGraph != null
                ? _generatedGraph.GetInstanceID().ToString()
                : "none";

            return "chapter-json:" + libraryKey + ":" + graphKey + ":" +
                   StoryRuntimeAssetRegistryResolver.ActiveStoryId + ":" +
                   StoryRuntimeAssetRegistryResolver.ActiveEntryCount;
        }
    }

    public bool TryResolveKnownCharacter(
        string id,
        string displayName,
        out CharacterData character,
        out string source)
    {
        character = null;
        source = "";

        if (_library != null)
        {
            character = _library.FindCharacter(id);
            if (character == null && !string.IsNullOrWhiteSpace(displayName))
                character = _library.FindCharacter(displayName);

            if (character != null)
            {
                source = "json-library";
                return true;
            }
        }

        character = StoryRuntimeAssetRegistryResolver.Resolve<CharacterData>(id);
        if (character == null && !string.IsNullOrWhiteSpace(displayName))
            character = StoryRuntimeAssetRegistryResolver.Resolve<CharacterData>(displayName);

        if (character != null)
        {
            source = "runtime-registry";
            return true;
        }

        character = ResolveGeneratedGraphCharacter(id, displayName);
        if (character != null)
        {
            source = "generated-graph";
            return true;
        }

        character = null;
        source = "";
        return false;
    }

    public override CharacterData ResolveCharacter(string id, string displayName = null)
    {
        if (TryResolveKnownCharacter(id, displayName, out CharacterData character, out _))
            return character;

        return base.ResolveCharacter(id, displayName);
    }

    public override ClothingItem ResolveClothing(string id)
    {
        ClothingItem asset = _library != null ? _library.FindClothing(id) : null;
        if (asset != null)
            return asset;

        asset = StoryRuntimeAssetRegistryResolver.Resolve<ClothingItem>(id);
        return asset != null ? asset : base.ResolveClothing(id);
    }

    public override Sprite ResolveSprite(string id)
    {
        Sprite asset = _library != null ? _library.FindSprite(id) : null;
        if (asset != null)
            return asset;

        asset = StoryRuntimeAssetRegistryResolver.Resolve<Sprite>(id);
        return asset != null ? asset : base.ResolveSprite(id);
    }

    public override VideoClip ResolveVideoClip(string id)
    {
        VideoClip asset = _library != null ? _library.FindVideoClip(id) : null;
        if (asset != null)
            return asset;

        asset = StoryRuntimeAssetRegistryResolver.Resolve<VideoClip>(id);
        return asset != null ? asset : base.ResolveVideoClip(id);
    }

    public override TextAsset ResolveTextAsset(string id)
    {
        TextAsset asset = _library != null ? _library.FindTextAsset(id) : null;
        if (asset != null)
            return asset;

        asset = StoryRuntimeAssetRegistryResolver.Resolve<TextAsset>(id);
        return asset != null ? asset : base.ResolveTextAsset(id);
    }

    public override AudioClip ResolveAudioClip(string id)
    {
        AudioClip asset = _library != null ? _library.FindAudioClip(id) : null;
        if (asset != null)
            return asset;

        asset = StoryRuntimeAssetRegistryResolver.Resolve<AudioClip>(id);
        return asset != null ? asset : base.ResolveAudioClip(id);
    }

    public override DialogueStyle ResolveDialogueStyle(string id)
    {
        DialogueStyle asset = _library != null ? _library.FindDialogueStyle(id) : null;
        if (asset != null)
            return asset;

        asset = StoryRuntimeAssetRegistryResolver.Resolve<DialogueStyle>(id);
        return asset != null ? asset : base.ResolveDialogueStyle(id);
    }

    public override string GetAssetId(UnityEngine.Object asset)
    {
        string id = _library != null ? _library.FindIdForAsset(asset) : "";
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        return asset != null ? asset.name : base.GetAssetId(asset);
    }

    private CharacterData ResolveGeneratedGraphCharacter(string id, string displayName)
    {
        EnsureGeneratedCharactersIndexed();
        if (_generatedCharacters == null || _generatedCharacters.Count == 0)
            return null;

        foreach (string key in EnumerateCharacterKeys(id, displayName))
        {
            if (_generatedCharacters.TryGetValue(key, out CharacterData character) && character != null)
                return character;
        }

        return null;
    }

    private void EnsureGeneratedCharactersIndexed()
    {
        if (_generatedCharactersIndexed)
            return;

        _generatedCharactersIndexed = true;
        _generatedCharacters = new Dictionary<string, CharacterData>(StringComparer.OrdinalIgnoreCase);

        if (_generatedGraph == null || _generatedGraph.nodes == null)
            return;

        foreach (var node in _generatedGraph.nodes)
        {
            if (node is DialogueNode dialogue)
            {
                if (dialogue.lines != null)
                {
                    foreach (DialogueLine line in dialogue.lines)
                    {
                        if (line == null || line.speaker == null)
                            continue;

                        IndexCharacter(line.speaker, line.speakerId, line.speakerNameHint);
                    }
                }

                if (dialogue.activeCharacters != null)
                {
                    foreach (DialogueCharacterEntry entry in dialogue.activeCharacters)
                    {
                        if (entry == null || entry.character == null)
                            continue;

                        IndexCharacter(entry.character, entry.speakerNameHint);
                    }
                }
            }
            else if (node is WardrobeChoiceNode wardrobe && wardrobe.character != null)
            {
                IndexCharacter(wardrobe.character, wardrobe.characterId);
            }
        }
    }

    private void IndexCharacter(CharacterData character, params string[] aliases)
    {
        if (character == null)
            return;

        AddCharacterKey(character.name, character);
        AddCharacterKey(character.characterName, character);

        if (aliases == null)
            return;

        for (int i = 0; i < aliases.Length; i++)
            AddCharacterKey(aliases[i], character);
    }

    private void AddCharacterKey(string value, CharacterData character)
    {
        foreach (string key in EnumerateCharacterKeys(value))
        {
            if (!_generatedCharacters.ContainsKey(key))
                _generatedCharacters.Add(key, character);
        }
    }

    private static IEnumerable<string> EnumerateCharacterKeys(params string[] values)
    {
        if (values == null)
            yield break;

        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < values.Length; i++)
        {
            string value = values[i];
            if (string.IsNullOrWhiteSpace(value))
                continue;

            string exact = value.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(exact) && emitted.Add(exact))
                yield return exact;

            string compact = NormalizeCharacterKey(value);
            if (!string.IsNullOrEmpty(compact) && emitted.Add(compact))
                yield return compact;

            if (compact.StartsWith("jsoncharacter", StringComparison.OrdinalIgnoreCase))
            {
                string withoutPrefix = compact.Substring("jsoncharacter".Length);
                if (!string.IsNullOrEmpty(withoutPrefix) && emitted.Add(withoutPrefix))
                    yield return withoutPrefix;
            }
        }
    }

    private static string NormalizeCharacterKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim()
                .Replace(" ", "")
                .Replace("_", "")
                .Replace("-", "")
                .ToLowerInvariant();
    }
}
