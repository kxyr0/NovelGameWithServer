#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class RevengeSicilianInkIntegration
{
    static List<StatDefinition> EnsureStats(AuthorInkSharedContext shared, List<string> report)
    {
        List<string> statNames = GetVariablesOfKind(shared, AuthorInkVariableKind.Stat);
        var result = new List<StatDefinition>();

        for (int i = 0; i < statNames.Count; i++)
        {
            string statName = statNames[i];
            string path = StatsFolder + "/mps_stat_" + SafeAssetToken(statName) + ".asset";
            StatDefinition stat = CreateOrLoadAsset<StatDefinition>(path, out bool created);
            stat.statId = statName;
            stat.displayName = statName;
            stat.order = i;

            if (stat.icon == null && TryResolveUniqueSprite(statName, out Sprite icon, out _))
                stat.icon = icon;

            EditorUtility.SetDirty(stat);
            result.Add(stat);
            report.Add("[STAT] " + statName + " -> " + path + (created ? " (created)" : " (updated)"));
        }

        if (result.Count == 0)
            report.Add("[WARN] В Ink не найдено ни одного VAR из секции // Статы.");

        return result;
    }

    static Dictionary<string, CharacterData> EnsureCharacters(
        AuthorInkSharedContext shared,
        List<StoryJsonAssetReference> references,
        List<string> report)
    {
        var result = new Dictionary<string, CharacterData>(StringComparer.OrdinalIgnoreCase);
        StoryJsonAssetReference heroReference = FindReference(references, "hero");
        CharacterData hero = heroReference != null ? heroReference.Character : null;
        bool heroCreated = false;
        if (hero == null)
        {
            hero = CreateOrLoadAsset<CharacterData>(CharactersFolder + "/hero.asset", out heroCreated);
            if (heroCreated || string.IsNullOrWhiteSpace(hero.characterName))
                hero.characterName = "{PlayerName}";
            if (heroCreated)
                hero.inheritAppearanceFromPlayer = true;
            EditorUtility.SetDirty(hero);
            UpsertReference(references, "hero", StoryJsonAssetReference.CreateCharacter("hero", hero), AssetReferenceKind.Character, report);
        }

        result["hero"] = hero;
        UpsertReference(references, "Элементина", StoryJsonAssetReference.CreateCharacter("Элементина", hero), AssetReferenceKind.Character, report);
        report.Add("[CHARACTER] hero -> " + AssetDatabase.GetAssetPath(hero) + (heroCreated ? " (created)" : " (existing/manual binding preserved)"));

        var speakers = new List<string>(shared.Speakers);
        speakers.Sort(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < speakers.Count; i++)
        {
            string speaker = speakers[i];
            if (string.IsNullOrWhiteSpace(speaker) || string.Equals(speaker, "Элементина", StringComparison.OrdinalIgnoreCase))
                continue;

            string generatedPath = CharactersFolder + "/mps_" + SafeAssetToken(speaker) + ".asset";
            StoryJsonAssetReference existingReference = FindReference(references, speaker);
            CharacterData character = existingReference != null ? existingReference.Character : null;
            bool created = false;
            if (character == null)
            {
                character = CreateOrLoadAsset<CharacterData>(generatedPath, out created);
                if (created || string.IsNullOrWhiteSpace(character.characterName))
                    character.characterName = speaker;
                if (created || string.IsNullOrWhiteSpace(character.name))
                    character.name = "mps_" + SafeAssetToken(speaker);
                EditorUtility.SetDirty(character);
                UpsertReference(references, speaker, StoryJsonAssetReference.CreateCharacter(speaker, character), AssetReferenceKind.Character, report);
            }

            string characterPath = AssetDatabase.GetAssetPath(character);
            bool managedByImporter = string.Equals(characterPath, generatedPath, StringComparison.OrdinalIgnoreCase);
            if (managedByImporter && character != null && character.defaultSprite == null && TryResolveUniqueSprite(speaker, out Sprite defaultSprite, out _))
            {
                character.defaultSprite = defaultSprite;
                EditorUtility.SetDirty(character);
                report.Add("[CHARACTER:AUTO-SPRITE] " + speaker + " <- " + AssetDatabase.GetAssetPath(defaultSprite));
            }

            result[speaker] = character;
            report.Add("[CHARACTER] " + speaker + " -> " + characterPath +
                       (created ? " (placeholder created)" : managedByImporter ? " (managed placeholder)" : " (manual binding preserved)"));
        }

        return result;
    }
}
#endif
