#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class RevengeSicilianInkIntegration
{
    static void ConfigureGameDataWardrobe(
        SerializedProperty wardrobeProperty,
        List<string> report)
    {
        if (wardrobeProperty == null)
        {
            report.Add("[WARN] В GameData не найден _wardrobeSetup.");
            return;
        }

        StoryJsonAssetLibrary library = AssetDatabase.LoadAssetAtPath<StoryJsonAssetLibrary>(AssetLibraryPath);
        if (library == null)
        {
            report.Add("[WARN] AssetLibrary не найден; GameData wardrobe setup не синхронизирован.");
            return;
        }

        CharacterData hero = library.FindCharacter("hero");
        var outfits = new List<ClothingItem>();
        var hairs = new List<ClothingItem>();
        var accessories = new List<ClothingItem>();
        var seen = new HashSet<ClothingItem>();

        IReadOnlyList<StoryJsonAssetReference> entries = library.Assets;
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                ClothingItem item = entries[i]?.Clothing;
                if (item == null || !seen.Add(item))
                    continue;

                switch (item.type)
                {
                    case ClothingType.Outfit:
                        outfits.Add(item);
                        break;
                    case ClothingType.Hair:
                        hairs.Add(item);
                        break;
                    case ClothingType.Accessory:
                        accessories.Add(item);
                        break;
                }
            }
        }

        outfits.Sort(CompareClothing);
        hairs.Sort(CompareClothing);
        accessories.Sort(CompareClothing);

        SerializedProperty overrideAssets = wardrobeProperty.FindPropertyRelative("_overrideWardrobeAssets");
        SerializedProperty targetCharacter = wardrobeProperty.FindPropertyRelative("_targetCharacter");
        SerializedProperty targetCharacterId = wardrobeProperty.FindPropertyRelative("_targetCharacterId");

        if (overrideAssets != null)
            overrideAssets.boolValue = hero != null || outfits.Count > 0 || hairs.Count > 0 || accessories.Count > 0;
        if (targetCharacter != null && hero != null)
            targetCharacter.objectReferenceValue = hero;
        if (targetCharacterId != null)
            targetCharacterId.stringValue = "hero";

        SetObjectArray(wardrobeProperty.FindPropertyRelative("_outfitItems"), outfits);
        SetObjectArray(wardrobeProperty.FindPropertyRelative("_hairItems"), hairs);
        SetObjectArray(wardrobeProperty.FindPropertyRelative("_accessoryItems"), accessories);

        SetDefaultIfEmpty(wardrobeProperty.FindPropertyRelative("_defaultOutfitItem"), outfits);
        SetDefaultIfEmpty(wardrobeProperty.FindPropertyRelative("_defaultHairItem"), hairs);
        SetDefaultIfEmpty(wardrobeProperty.FindPropertyRelative("_defaultAccessoryItem"), accessories);

        // _appearanceOptions намеренно не трогаем:
        // Ink использует Palermo/Katania/Messina как авторские значения,
        // а runtime ожидает AppearanceType. Автоматически угадывать семантику здесь нельзя.
        report.Add(
            "[GAMEDATA:WARDROBE] hero=" + (hero != null ? AssetDatabase.GetAssetPath(hero) : "<missing>") +
            ", outfits=" + outfits.Count +
            ", hair=" + hairs.Count +
            ", accessories=" + accessories.Count +
            ". Appearance options сохранены как есть.");
    }

    static int CompareClothing(ClothingItem left, ClothingItem right)
    {
        string leftId = left != null ? left.id : "";
        string rightId = right != null ? right.id : "";
        return string.Compare(leftId, rightId, StringComparison.OrdinalIgnoreCase);
    }

    static void SetObjectArray<T>(SerializedProperty property, List<T> assets)
        where T : UnityEngine.Object
    {
        if (property == null || !property.isArray)
            return;

        property.arraySize = assets != null ? assets.Count : 0;
        for (int i = 0; i < property.arraySize; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];
    }

    static void SetDefaultIfEmpty<T>(SerializedProperty property, List<T> assets)
        where T : UnityEngine.Object
    {
        if (property == null || property.objectReferenceValue != null || assets == null || assets.Count == 0)
            return;

        property.objectReferenceValue = assets[0];
    }

    static void RegisterGameDataInCatalog(GameData gameData, List<string> report)
    {
        if (gameData == null)
            return;

        const string catalogPath = "Assets/_MyProject/Data/Games/Game Catalog.asset";
        GameCatalog catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(catalogPath);
        if (catalog == null)
        {
            report.Add("[WARN] Game Catalog не найден: " + catalogPath + ". GameData создан/обновлён, но в каталог не добавлен.");
            return;
        }

        if (catalog.AddGame(gameData))
        {
            EditorUtility.SetDirty(catalog);
            report.Add("[GAMEDATA] Добавлен в Game Catalog: " + catalogPath);
        }
    }
}
#endif
