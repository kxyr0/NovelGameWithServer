#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class RevengeSicilianInkIntegration
{
    static Dictionary<string, ClothingItem> EnsureWardrobeItems(
        AuthorInkSharedContext shared,
        List<CompiledEpisode> compiled,
        List<StoryJsonAssetReference> references,
        List<string> report)
    {
        var requested = CollectWardrobeItems(shared, compiled);
        var result = new Dictionary<string, ClothingItem>(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, ClothingType> pair in requested)
        {
            string id = pair.Key;
            ClothingType type = pair.Value;
            string prefix = type == ClothingType.Hair ? "hair" : type == ClothingType.Outfit ? "outfit" : "item";
            string generatedPath = WardrobeFolder + "/mps_" + prefix + "_" + SafeAssetToken(id) + ".asset";
            string path = generatedPath;
            StoryJsonAssetReference existingReference = FindReference(references, id);
            ClothingItem item = existingReference != null ? existingReference.Clothing : null;
            bool created = false;
            if (item == null)
                item = CreateOrLoadAsset<ClothingItem>(generatedPath, out created);
            else
                path = AssetDatabase.GetAssetPath(item);

            bool managedByImporter = string.Equals(path, generatedPath, StringComparison.OrdinalIgnoreCase);
            if (managedByImporter)
            {
                item.id = id;
                item.type = type;

                var serialized = new SerializedObject(item);
                SerializedProperty owner = serialized.FindProperty("ownerCharacterId");
                if (owner != null) owner.stringValue = "hero";
                SerializedProperty displayName = serialized.FindProperty("displayName");
                if (displayName != null && (created || string.IsNullOrWhiteSpace(displayName.stringValue)))
                    displayName.stringValue = id;
                SetSingleString(serialized.FindProperty("visibleInStoryIds"), StoryId);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                if (item.sprite == null && TryResolveUniqueSprite(id, out Sprite sprite, out _))
                    item.sprite = sprite;

                EditorUtility.SetDirty(item);
            }

            result[id] = item;
            UpsertReference(references, id, StoryJsonAssetReference.CreateClothing(id, item), AssetReferenceKind.Clothing, report);
            report.Add("[WARDROBE] " + id + " -> " + path + " [" + type + "]" +
                       (created ? " (placeholder created)" : managedByImporter ? " (managed placeholder)" : " (manual binding preserved)"));
        }

        return result;
    }

    static Dictionary<string, ClothingType> CollectWardrobeItems(
        AuthorInkSharedContext shared,
        List<CompiledEpisode> compiled)
    {
        var result = new Dictionary<string, ClothingType>(StringComparer.OrdinalIgnoreCase);
        AddStringValues(shared, "outfit", ClothingType.Outfit, result);
        AddStringValues(shared, "hair", ClothingType.Hair, result);

        for (int c = 0; c < compiled.Count; c++)
        {
            StoryJsonDocument document = compiled[c].Document;
            if (document?.nodes == null)
                continue;

            for (int n = 0; n < document.nodes.Count; n++)
            {
                StoryJsonNode node = document.nodes[n];
                if (node == null || !string.Equals(node.type, StoryJsonTypes.WardrobeChoice, StringComparison.OrdinalIgnoreCase))
                    continue;

                ClothingType inferred = InferWardrobeType(node.label);
                if (node.clothes == null)
                    continue;

                for (int i = 0; i < node.clothes.Count; i++)
                {
                    string id = node.clothes[i]?.Trim();
                    if (!string.IsNullOrWhiteSpace(id) && !result.ContainsKey(id))
                        result[id] = inferred;
                }
            }
        }

        return result;
    }

    static void AddStringValues(
        AuthorInkSharedContext shared,
        string variable,
        ClothingType type,
        Dictionary<string, ClothingType> target)
    {
        if (!shared.StringValues.TryGetValue(variable, out HashSet<string> values))
            return;

        foreach (string value in values)
            if (!string.IsNullOrWhiteSpace(value))
                target[value] = type;
    }

    static ClothingType InferWardrobeType(string prompt)
    {
        string normalized = NormalizeAssetKey(prompt);
        if (normalized.Contains("причес") || normalized.Contains("hair"))
            return ClothingType.Hair;
        if (normalized.Contains("одеж") || normalized.Contains("outfit") || normalized.Contains("образ"))
            return ClothingType.Outfit;
        return ClothingType.Accessory;
    }
}
#endif
