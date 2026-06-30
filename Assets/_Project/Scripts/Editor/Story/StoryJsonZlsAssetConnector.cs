#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

public static class StoryJsonZlsAssetConnector
{
    private const string LogPrefix = "[StoryJsonZlsAssetConnector]";
    private const string JsonPath = "Assets/_MyProject/Data/Stories/Only_the_heart_sees_clearly/zls_1.json";
    private const string LibraryPath = "Assets/_MyProject/Data/Stories/Only_the_heart_sees_clearly/ZLS_1_JsonAssetLibrary.asset";

    private static readonly AssetBinding[] Bindings =
    {
        Video("bg_oak", "b5dee3a5b02f9da44bf06d275119d277"),
        Video("bg_train_carriage", "81ab5a4c99ac80047a7422f7bf4d4131"),
        Sprite("bg_city_station_platform", "846ef77ca12206e40a7dbff52f0d1bf7"),
        Video("bg_village_station_platform", "8a3bfc8821efea94783a1bbbef519144"),
        Sprite("bg_village_meadow_day", "e8a00ca306fff8542ae3f19f3a0103e3"),
        Video("bg_bab_nyura_house_exterior_day", "f2ad989c6261457468fff072340558ad"),
        Sprite("bg_bab_nyura_house_inside_day", "f39b0fdf2c1e1fa42a1abca09da31853"),
        Sprite("bg_bab_nyura_house_inside_night", "adf4494b6d9618a4987c4c624fb6c584"),
        Sprite("bg_village_evening", "af450570a2eb39c4fa1ba30fb67a1b2e"),
        Video("bg_scarecrow_evening", "4934bf5699dbc9f4c9dfe640424c7c79"),
        Video("bg_stable_evening", "243bb397d2e7c0e47b059c71bcbaccda"),
        Sprite("bg_washstand_evening", "fba876ee99396a740bcdf11bc32bfd5e"),
        Sprite("bg_bedroom_evening", "b8c14acff2d84cc40a1945cb60412208"),
        Sprite("bg_bedroom_night", "c7c752f426a61f4488b87d3119f8e55a"),
        Video("bg_magic_book_dark", "9700951265942f74f91397af21d33647"),
        Video("bg_dream_forest", "33883f62705a7cf4283e079153cccd9a"),
        Video("bg_dream_path", "1d554fb02bf2f1448ac3ff3e4a2385d5"),
        Video("bg_burning_book", "a17f9a1c5bb209a48b7999a9611d6eca"),

        Sprite("preview_hero_european", "e98a9a90ca6a59647b04794fd20e5bdc"),
        Sprite("preview_hero_latina", "624c6ce90735bf446a0fdd2558907541"),
        Sprite("preview_hero_asian", "dc62aed4e18708948b0084add4140fd7"),

        Sprite("cg_ivan_on_horse", "ad2a0092b5f688d4bb2db4c20541f151"),
        Sprite("cg_ivan_at_stable", "ad2a0092b5f688d4bb2db4c20541f151"),
        Sprite("img_phone_no_signal_e", "cc8118634c3cdd04ba064ddd2a7e5ec5"),
        Video("cg_book_kashchey", "a17f9a1c5bb209a48b7999a9611d6eca")
    };

    [MenuItem("VN/Connect ZLS 1 Story Assets")]
    public static void ConnectZls1AssetsAndReimport()
    {
        bool ok = TryConnectZls1AssetsAndReimport(out string message);
        if (ok)
            Debug.Log(LogPrefix + " " + message);
        else
            Debug.LogError(LogPrefix + " " + message);
    }

    public static void ConnectZls1AssetsAndReimportBatch()
    {
        bool ok = TryConnectZls1AssetsAndReimport(out string message);
        if (ok)
            Debug.Log(LogPrefix + " " + message);
        else
            Debug.LogError(LogPrefix + " " + message);

        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }

    public static bool TryConnectZls1AssetsAndReimport(out string message)
    {
        message = "";

        var library = AssetDatabase.LoadAssetAtPath<StoryJsonAssetLibrary>(LibraryPath);
        if (library == null)
        {
            message = "Asset library was not found: " + LibraryPath;
            return false;
        }

        var missing = new List<string>();
        var newReferences = new List<StoryJsonAssetReference>();

        foreach (var binding in Bindings)
        {
            if (!binding.TryCreateReference(out var reference, out string failure))
            {
                missing.Add(failure);
                continue;
            }

            newReferences.Add(reference);
        }

        if (missing.Count > 0)
        {
            message = "Some bindings could not be resolved:\n" + string.Join("\n", missing);
            return false;
        }

        var connectedIds = new HashSet<string>(Bindings.Select(binding => binding.Id), StringComparer.OrdinalIgnoreCase);
        var merged = new List<StoryJsonAssetReference>();

        if (library.Assets != null)
        {
            merged.AddRange(library.Assets.Where(entry =>
                entry != null &&
                !connectedIds.Contains(entry.Id)));
        }

        merged.AddRange(newReferences);
        library.Configure(merged);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        bool imported = StoryJsonAutoImporter.TryAutoImport(JsonPath, out string importMessage);
        if (!imported)
        {
            message = "Asset library was updated, but JSON reimport failed.\n" + importMessage;
            return false;
        }

        message =
            "Connected " + newReferences.Count + " media bindings and reimported ZLS_1.\n" +
            importMessage;
        return true;
    }

    private static AssetBinding Sprite(string id, string guid)
    {
        return new AssetBinding(id, guid, AssetKind.Sprite);
    }

    private static AssetBinding Video(string id, string guid)
    {
        return new AssetBinding(id, guid, AssetKind.Video);
    }

    private enum AssetKind
    {
        Sprite,
        Video
    }

    private sealed class AssetBinding
    {
        public AssetBinding(string id, string guid, AssetKind kind)
        {
            Id = id ?? "";
            Guid = guid ?? "";
            Kind = kind;
        }

        public string Id { get; }
        private string Guid { get; }
        private AssetKind Kind { get; }

        public bool TryCreateReference(out StoryJsonAssetReference reference, out string failure)
        {
            reference = null;
            failure = "";

            string path = AssetDatabase.GUIDToAssetPath(Guid);
            if (string.IsNullOrEmpty(path))
            {
                failure = Id + ": GUID was not found: " + Guid;
                return false;
            }

            switch (Kind)
            {
                case AssetKind.Sprite:
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                    {
                        failure = Id + ": Sprite was not found at " + path;
                        return false;
                    }

                    reference = StoryJsonAssetReference.CreateSprite(Id, sprite);
                    return true;

                case AssetKind.Video:
                    var video = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
                    if (video == null)
                    {
                        failure = Id + ": VideoClip was not found at " + path;
                        return false;
                    }

                    reference = StoryJsonAssetReference.CreateVideo(Id, video);
                    return true;

                default:
                    failure = Id + ": Unsupported asset kind: " + Kind;
                    return false;
            }
        }
    }
}
#endif
