#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

public static partial class RevengeSicilianInkIntegration
{
    static void WriteIntegrationReport(
        AuthorInkSharedContext shared,
        List<string> report,
        StoryData story)
    {
        var lines = new List<string>
        {
            "MPS AUTO IMPORT REPORT",
            "======================",
            "",
            "Что теперь создаётся автоматически:",
            "- StoryData в корне истории (generated-папка содержит только производные Graph/Chapter assets).",
            "- Stats/*.asset из VAR секции // Статы и те же Stat ID в Menu GameData / Story Stats.",
            "- Characters/*.asset для всех реальных speakers; Элементина и hero указывают на hero.asset.",
            "- WardrobeItems/*.asset для outfit/hair и явных Гардероб choices + списки в GameData/Wardrobe.",
            "- revenge_sicilian_style_JsonAssetLibrary.asset с явными ссылками и Missing slots для отсутствующих media.",
            "- JSON characters[] для всех speakers; Элементина нормализуется в hero.",
            "- Медиа привязываются только по безопасному exact-normalized совпадению имени.",
            "",
            "Как выбирается asset:",
            "1) Уже существующая ручная ссылка в StoryJsonAssetLibrary имеет высший приоритет и НЕ затирается.",
            "2) Затем ищется ровно одно совпадение внутри папки этой истории.",
            "3) Затем ровно одно совпадение по всему проекту.",
            "4) При 0 или >1 совпадений importer НЕ угадывает: создаёт Missing <Type> slot в AssetLibrary и пишет ASSET:UNRESOLVED/CONFLICT сюда.",
            "   Для сравнения имени игнорируются регистр, пробелы, '-', '_' и пунктуация.",
            "   Ручная ссылка в AssetLibrary после этого имеет приоритет и при повторном импорте не затирается.",
            "",
            "Рекомендуемые папки:",
            "  Backgrounds/  — фоны и image assets",
            "  Audio/        — music/sfx/ambience",
            "  Cutscenes/    — cutscene media",
            "  Characters/   — CharacterData (созданы placeholders)",
            "  WardrobeItems/— ClothingItem (созданы placeholders)",
            "  Stats/        — StatDefinition",
            "",
            "StoryData: " + (story != null ? AssetDatabase.GetAssetPath(story) : "<missing>"),
            "AssetLibrary: " + AssetLibraryPath,
            "GameData: " + MenuGameDataPath,
            "",
            "Detected story stats: " + string.Join(", ", GetVariablesOfKind(shared, AuthorInkVariableKind.Stat)),
            "Detected relationships: " + string.Join(", ", GetVariablesOfKind(shared, AuthorInkVariableKind.Relationship)),
            "Detected speakers: " + string.Join(", ", shared.Speakers.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
            "",
            "DETAILS",
            "-------"
        };

        lines.AddRange(report);
        File.WriteAllText(BindingReportPath, string.Join("\n", lines), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(BindingReportPath, ImportAssetOptions.ForceSynchronousImport);
    }
}
#endif
