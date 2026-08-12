#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class RevengeSicilianInkIntegration
{
    static void LinkMenuGameData(
        StoryData story,
        AuthorInkSharedContext shared,
        List<string> report)
    {
        if (story == null)
        {
            report.Add("[WARN] StoryData не найден после импорта; GameData не перепривязан.");
            return;
        }

        GameData gameData = AssetDatabase.LoadAssetAtPath<GameData>(MenuGameDataPath);
        bool gameDataCreated = false;
        if (gameData == null)
        {
            gameData = ScriptableObject.CreateInstance<GameData>();
            gameData.name = StoryId;
            AssetDatabase.CreateAsset(gameData, MenuGameDataPath);
            gameDataCreated = true;
            report.Add("[GAMEDATA] Создан минимальный GameData: " + MenuGameDataPath);
        }

        story.Configure(StoryId, StoryName, story.Chapters);
        EditorUtility.SetDirty(story);

        var serialized = new SerializedObject(gameData);
        SerializedProperty gameName = serialized.FindProperty("_gameName");
        if (gameName != null && (gameDataCreated || string.IsNullOrWhiteSpace(gameName.stringValue)))
            gameName.stringValue = StoryName;

        SerializedProperty storyProperty = serialized.FindProperty("_story");
        if (storyProperty != null) storyProperty.objectReferenceValue = story;
        SerializedProperty episodeCount = serialized.FindProperty("_episodeCount");
        if (episodeCount != null) episodeCount.intValue = Episodes.Length;
        SerializedProperty currentEpisode = serialized.FindProperty("_currentEpisodeNumber");
        if (currentEpisode != null) currentEpisode.intValue = 1;
        SerializedProperty comingSoon = serialized.FindProperty("_forceComingSoon");
        if (comingSoon != null) comingSoon.boolValue = false;

        ConfigureGameDataStats(serialized.FindProperty("_storyStats"), shared, report);
        ConfigureGameDataWardrobe(serialized.FindProperty("_wardrobeSetup"), report);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gameData);

        RegisterGameDataInCatalog(gameData, report);
        report.Add("[GAMEDATA] StoryData + episode count + Story Stats + wardrobe bindings синхронизированы: " + MenuGameDataPath);
    }

    static void ConfigureGameDataStats(
        SerializedProperty statsProperty,
        AuthorInkSharedContext shared,
        List<string> report)
    {
        if (statsProperty == null || !statsProperty.isArray)
        {
            report.Add("[WARN] В GameData не найден сериализуемый массив _storyStats.");
            return;
        }

        List<string> statNames = GetVariablesOfKind(shared, AuthorInkVariableKind.Stat);
        statsProperty.arraySize = statNames.Count;
        for (int i = 0; i < statNames.Count; i++)
        {
            string statName = statNames[i];
            StatDefinition definition = AssetDatabase.LoadAssetAtPath<StatDefinition>(
                StatsFolder + "/mps_stat_" + SafeAssetToken(statName) + ".asset");

            SerializedProperty item = statsProperty.GetArrayElementAtIndex(i);
            SerializedProperty label = item.FindPropertyRelative("_label");
            SerializedProperty statId = item.FindPropertyRelative("_statId");
            SerializedProperty value = item.FindPropertyRelative("_value");
            SerializedProperty icon = item.FindPropertyRelative("_icon");

            if (label != null) label.stringValue = definition != null ? definition.displayName : statName;
            if (statId != null) statId.stringValue = statName;
            if (value != null) value.intValue = 0;
            if (icon != null) icon.objectReferenceValue = definition != null ? definition.icon : null;
        }
    }
}
#endif
