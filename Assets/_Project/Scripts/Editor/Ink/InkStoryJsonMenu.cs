#if UNITY_EDITOR
using System.IO;
using Ink.UnityIntegration;
using UnityEditor;
using UnityEngine;

public static class InkStoryJsonMenu
{
    private const string ExamplePath = "Assets/_MyProject/Ink/Examples/nocturnal_smoke.ink";

    [MenuItem("VN/Ink/Создать пример Ink", priority = 10)]
    public static void CreateExampleInk()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ExamplePath));
        if (!File.Exists(ExamplePath))
            File.WriteAllText(ExamplePath, ExampleText());

        EnsureCompiledAsMaster(ExamplePath);
        Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(ExamplePath);
        Debug.Log("[Ink] Пример Ink готов: " + ExamplePath);
    }

    [MenuItem("VN/Ink/Экспорт выбранного Ink в Story JSON", priority = 20)]
    public static void ExportSelectedInk()
    {
        if (!TryGetSelectedInk(out InkFile inkFile, out string inkPath))
        {
            EditorUtility.DisplayDialog("Ink", "Выберите .ink файл в Project.", "OK");
            return;
        }

        string fileName = Path.GetFileNameWithoutExtension(inkPath);
        string jsonPath = Path.Combine(Path.GetDirectoryName(inkPath), fileName + "_story.json")
            .Replace("\\", "/");
        EnsureCompiledAsMaster(inkPath);
        inkFile = LoadInkFileAtPath(inkPath);
        if (!InkStoryJsonConverter.TryConvert(inkFile, "", fileName, out string json, out string error))
        {
            EditorUtility.DisplayDialog("Ink экспорт", error, "OK");
            return;
        }

        File.WriteAllText(jsonPath, json);
        AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceSynchronousImport);
        StoryJsonAutoImporter.TryAutoImport(jsonPath, out string importMessage);
        Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(jsonPath);
        Debug.Log("[Ink] Story JSON создан: " + jsonPath + "\n" + importMessage);
    }

    [MenuItem("VN/Ink/Экспорт выбранного Ink в Story JSON", true)]
    private static bool CanExportSelectedInk()
    {
        return TryGetSelectedInk(out _, out _);
    }

    public static bool TryConvertExample(out string json, out string error)
    {
        EnsureCompiledAsMaster(ExamplePath);
        InkFile inkFile = LoadInkFileAtPath(ExamplePath);
        return InkStoryJsonConverter.TryConvert(inkFile, "", "ink_smoke_episode", out json, out error);
    }

    public static void EnsureCompiledAsMaster(string path)
    {
        AssetImporter importer = AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return;
        }

        var serialized = new SerializedObject(importer);
        SerializedProperty property = serialized.FindProperty("compileAsMasterFileOverride");
        if (property != null && !property.boolValue)
        {
            property.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
            return;
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    public static InkFile LoadInkFileAtPath(string path)
    {
        InkFile direct = AssetDatabase.LoadAssetAtPath<InkFile>(path);
        if (direct != null)
            return direct;

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is InkFile inkFile)
                return inkFile;

        return null;
    }

    private static bool TryGetSelectedInk(out InkFile inkFile, out string path)
    {
        path = AssetDatabase.GetAssetPath(Selection.activeObject);
        inkFile = !string.IsNullOrWhiteSpace(path) ? LoadInkFileAtPath(path) : null;
        return inkFile != null;
    }

    private static string ExampleText()
    {
        return "# storyId: ink_smoke_story\n" +
               "# episodeId: ink_smoke_episode\n" +
               "# title: Проверка Ink\n" +
               "# scene: smoke_room\n" +
               "# bg: /media/smoke_room.webp\n\n" +
               "Анна: Это тестовая глава Ink.\n\n" +
               "* [Ответить спокойно]\n" +
               "    Анна: Значит, импорт работает.\n\n" +
               "* [Промолчать]\n" +
               "    Анна: Даже молчание попадает в общий формат.\n\n" +
               "-> END\n";
    }
}
#endif
