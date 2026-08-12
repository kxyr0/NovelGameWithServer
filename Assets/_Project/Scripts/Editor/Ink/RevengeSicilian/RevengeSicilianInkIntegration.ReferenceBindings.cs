#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static partial class RevengeSicilianInkIntegration
{
    static void UpsertReference(
        List<StoryJsonAssetReference> references,
        string id,
        StoryJsonAssetReference replacement,
        AssetReferenceKind expectedKind,
        List<string> report)
    {
        if (replacement == null || string.IsNullOrWhiteSpace(id))
            return;

        int index = references.FindIndex(entry => entry != null && entry.Matches(id));
        if (index < 0)
        {
            references.Add(replacement);
            return;
        }

        StoryJsonAssetReference existing = references[index];
        if (HasExpectedAsset(existing, expectedKind))
            return;

        if (HasAnyAsset(existing))
        {
            report.Add("[ASSET:CONFLICT] id '" + id + "' уже занят другим типом в AssetLibrary; автоматическая замена запрещена.");
            return;
        }

        references[index] = replacement;
    }

    static StoryJsonAssetReference FindReference(List<StoryJsonAssetReference> references, string id)
    {
        return references.FirstOrDefault(entry => entry != null && entry.Matches(id));
    }

    static StoryJsonAssetKindHint ToKindHint(AssetReferenceKind kind)
    {
        switch (kind)
        {
            case AssetReferenceKind.Character: return StoryJsonAssetKindHint.Character;
            case AssetReferenceKind.Clothing: return StoryJsonAssetKindHint.Clothing;
            case AssetReferenceKind.Sprite: return StoryJsonAssetKindHint.Sprite;
            case AssetReferenceKind.Audio: return StoryJsonAssetKindHint.Audio;
            case AssetReferenceKind.Video: return StoryJsonAssetKindHint.Video;
            case AssetReferenceKind.Text: return StoryJsonAssetKindHint.TextAsset;
            default: return StoryJsonAssetKindHint.None;
        }
    }

    static bool HasExpectedAsset(StoryJsonAssetReference entry, AssetReferenceKind kind)
    {
        if (entry == null)
            return false;
        switch (kind)
        {
            case AssetReferenceKind.Character: return entry.Character != null;
            case AssetReferenceKind.Clothing: return entry.Clothing != null;
            case AssetReferenceKind.Sprite: return entry.Sprite != null;
            case AssetReferenceKind.Audio: return entry.Audio != null;
            case AssetReferenceKind.Video: return entry.Video != null;
            case AssetReferenceKind.Text: return entry.TextAsset != null;
            default: return false;
        }
    }

    static bool HasAnyAsset(StoryJsonAssetReference entry)
    {
        return entry != null &&
               (entry.Character != null || entry.Clothing != null || entry.Sprite != null ||
                entry.Audio != null || entry.Video != null || entry.TextAsset != null || entry.DialogueStyle != null);
    }

    static string AssetPathOf(StoryJsonAssetReference entry, AssetReferenceKind kind)
    {
        UnityEngine.Object asset = null;
        if (entry != null)
        {
            switch (kind)
            {
                case AssetReferenceKind.Character: asset = entry.Character; break;
                case AssetReferenceKind.Clothing: asset = entry.Clothing; break;
                case AssetReferenceKind.Sprite: asset = entry.Sprite; break;
                case AssetReferenceKind.Audio: asset = entry.Audio; break;
                case AssetReferenceKind.Video: asset = entry.Video; break;
                case AssetReferenceKind.Text: asset = entry.TextAsset; break;
            }
        }
        return asset != null ? AssetDatabase.GetAssetPath(asset) : "<none>";
    }

    static int CountKind(List<StoryJsonAssetReference> references, AssetReferenceKind kind)
    {
        return references.Count(entry => HasExpectedAsset(entry, kind));
    }

    static void ReportAppearanceValues(AuthorInkSharedContext shared, List<string> report)
    {
        if (!shared.StringValues.TryGetValue("appearance", out HashSet<string> values) || values.Count == 0)
            return;

        report.Add(
            "[APPEARANCE:MANUAL] Ink values: " + string.Join(", ", values) +
            ". Они не мапятся автоматически в AppearanceType: это авторские Palermo/Katania/Messina, а не гарантированные runtime enum values.");
    }

    static bool TryResolveUniqueSprite(string id, out Sprite asset, out string resolution)
    {
        if (_spriteIndex == null)
            _spriteIndex = new ExactAssetIndex<Sprite>(StoryFolder);
        asset = _spriteIndex.Resolve(id, out resolution);
        return asset != null;
    }
}
#endif
