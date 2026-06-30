#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public static class StoryJsonPpAssetConnector
{
    const string LogPrefix = "[StoryJsonPpAssetConnector]";
    const string StoryId = "privychka_pritvoryatsya";
    const string StoryName = "Привычка притворяться";
    const string StoryFolder = "Assets/_MyProject/Data/Stories/privychka_pritvoryatsya";
    const string ArtRoot = "Assets/_MyProject/Art/Привычка притворяться";
    const string ScenePath = "Assets/_MyProject/Scenes/Game.unity";
    const string GameCatalogPath = "Assets/_MyProject/Data/Games/Game Catalog.asset";

    static readonly string[] JsonPaths =
    {
        StoryFolder + "/PP_1.json",
        StoryFolder + "/PP_2.json"
    };

    [MenuItem("VN/Connect PP Story Assets")]
    public static void ConnectPpAssetsAndReimport()
    {
        bool ok = TryConnectPpAssetsAndReimport(out string message);
        if (ok)
            Debug.Log(LogPrefix + " " + message);
        else
            Debug.LogError(LogPrefix + " " + message);
    }

    public static void ConnectPpAssetsAndReimportBatch()
    {
        bool ok = TryConnectPpAssetsAndReimport(out string message);
        if (ok)
            Debug.Log(LogPrefix + " " + message);
        else
            Debug.LogError(LogPrefix + " " + message);

        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }

    public static bool TryConnectPpAssetsAndReimport(out string message)
    {
        message = "";

        EnsureStoryFolders();
        AssetDatabase.Refresh();

        var library = CreateOrLoadAsset<StoryJsonAssetLibrary>(StoryFolder + "/" + StoryId + "_JsonAssetLibrary.asset");
        var references = new List<StoryJsonAssetReference>();
        var missing = new List<string>();

        var clothingById = new Dictionary<string, ClothingItem>(StringComparer.OrdinalIgnoreCase);
        var hero = ConfigureCharacters(references, clothingById, missing);
        ConfigureClothing(references, clothingById, missing);
        ConfigureMedia(references, missing);

        if (missing.Count > 0)
        {
            message = "Some PP asset bindings could not be resolved:\n" + string.Join("\n", missing);
            return false;
        }

        library.Configure(references);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var importMessages = new List<string>();
        foreach (string jsonPath in JsonPaths)
        {
            bool imported = StoryJsonAutoImporter.TryAutoImport(jsonPath, out string importMessage);
            importMessages.Add(importMessage);
            if (!imported)
            {
                message = "Asset library was updated, but JSON import failed for " + jsonPath + ".\n" + importMessage;
                return false;
            }
        }

        RegisterGameData();
        ConfigureWardrobeScene(hero, clothingById.Values.ToList());

        message =
            "Connected PP asset library, imported " + JsonPaths.Length + " chapters, registered GameData, and configured story wardrobe.\n" +
            string.Join("\n\n", importMessages.Where(text => !string.IsNullOrWhiteSpace(text)));
        return true;
    }

    static CharacterData ConfigureCharacters(
        List<StoryJsonAssetReference> references,
        Dictionary<string, ClothingItem> clothingById,
        List<string> missing)
    {
        CharacterData hero = CreateOrLoadAsset<CharacterData>(StoryFolder + "/Characters/hero.asset");
        hero.name = "hero";
        hero.characterName = "{PlayerName}";
        hero.inheritAppearanceFromPlayer = true;
        hero.useLayeredEmotions = true;
        hero.defaultSprite = LoadSprite("Главная героиня/Тело/Европейка/Без подсветки.PNG", "hero.default", missing);
        hero.bodySprite = hero.defaultSprite;
        hero.emotionLayers = BuildHeroFaceLayers("Главная героиня/Эмоции/Европейка", "", missing);
        hero.appearanceVariants = new List<AppearanceVariant>
        {
            CreateAppearanceVariant(AppearanceType.European, "Главная героиня/Тело/Европейка/Без подсветки.PNG", missing),
            CreateAppearanceVariant(AppearanceType.African, "Главная героиня/Тело/Афромериканка_/Без подсветки.PNG", missing),
            CreateAppearanceVariant(AppearanceType.Asian, "Главная героиня/Тело/Азиатка/Без подсветки.PNG", missing)
        };
        EditorUtility.SetDirty(hero);
        references.Add(StoryJsonAssetReference.CreateCharacter("hero", hero));

        CharacterData vlad = CreateLayeredCharacter(
            "pp_vlad",
            "Влад",
            "Фавориты/Влад/Тело/Just_body.PNG",
            "Фавориты/Влад/Эмоции",
            new[]
            {
                Face(CharacterEmotionType.Neutral, "Normal.PNG"),
                Face(CharacterEmotionType.Smile, "Smile.PNG"),
                Face(CharacterEmotionType.Smirk, "Smirk.PNG"),
                Face(CharacterEmotionType.RaisedEyebrow, "Raised_eyebrow.PNG"),
                Face(CharacterEmotionType.EyeRoll, "Roll_eyes.PNG"),
                Face(CharacterEmotionType.Thinking, "Look_to_the_side.PNG"),
                Face(CharacterEmotionType.Confused, "Confusion.PNG"),
                Face(CharacterEmotionType.Embarrassed, "Embarrassed.PNG"),
                Face(CharacterEmotionType.Annoyed, "Annoyed.PNG"),
                Face(CharacterEmotionType.Scull, "Irritation.PNG")
            },
            missing);
        vlad.permanentHair = CreateClothing(clothingById, "pp_vlad_hair_black", "Влад: волосы", ClothingType.Hair, "Фавориты/Влад/Прически/Black_hair.PNG", missing);
        vlad.permanentOutfit = CreateClothing(clothingById, "pp_vlad_outfit_sweater", "Влад: свитер", ClothingType.Outfit, "Фавориты/Влад/Одежда/Sweater.PNG", missing);
        EditorUtility.SetDirty(vlad);

        references.Add(StoryJsonAssetReference.CreateCharacter("pp_vlad", vlad));
        references.Add(StoryJsonAssetReference.CreateCharacter("pp_gabriel", CreateLayeredCharacter(
            "pp_gabriel",
            "Габриэль",
            "Фавориты/Габриэль_/Body/Body_Normal_.png",
            "Фавориты/Габриэль_/Face",
            new[]
            {
                Face(CharacterEmotionType.Neutral, "Thoughtful_.png"),
                Face(CharacterEmotionType.Smile, "Grin.png"),
                Face(CharacterEmotionType.Smirk, "Flirt_.png"),
                Face(CharacterEmotionType.RaisedEyebrow, "Raised_eyebrow.png"),
                Face(CharacterEmotionType.EyeRoll, "Roll_eyes.png"),
                Face(CharacterEmotionType.Thinking, "Thoughtful_.png"),
                Face(CharacterEmotionType.Surprised, "Surprised_.png"),
                Face(CharacterEmotionType.Annoyed, "Annoyed_.png")
            },
            missing)));

        references.Add(StoryJsonAssetReference.CreateCharacter("pp_james", CreateFullSpriteCharacter(
            "pp_james",
            "Джеймс",
            "Персонажи/Джеймс/James_Normal.png",
            new[]
            {
                Emotion(CharacterEmotionType.Neutral, "Персонажи/Джеймс/James_Normal.png"),
                Emotion(CharacterEmotionType.Smile, "Персонажи/Джеймс/James_smile.png"),
                Emotion(CharacterEmotionType.Angry, "Персонажи/Джеймс/James_Angry.png")
            },
            missing)));

        references.Add(StoryJsonAssetReference.CreateCharacter("pp_remi", CreateFullSpriteCharacter(
            "pp_remi",
            "Реми",
            "Персонажи/Реми/Remi_Normal.png",
            new[]
            {
                Emotion(CharacterEmotionType.Neutral, "Персонажи/Реми/Remi_Normal.png"),
                Emotion(CharacterEmotionType.Smile, "Персонажи/Реми/Remi_Smile.png"),
                Emotion(CharacterEmotionType.Thinking, "Персонажи/Реми/Remi_Thoughtful .png")
            },
            missing)));

        references.Add(StoryJsonAssetReference.CreateCharacter("pp_mag", CreateFullSpriteCharacter(
            "pp_mag",
            "Мэг",
            "Персонажи/Мэг/Mag_Normal.png",
            new[]
            {
                Emotion(CharacterEmotionType.Neutral, "Персонажи/Мэг/Mag_Normal.png"),
                Emotion(CharacterEmotionType.Smile, "Персонажи/Мэг/Mag_Smile.png"),
                Emotion(CharacterEmotionType.EyeRoll, "Персонажи/Мэг/Mag_Roll_Eyes.png")
            },
            missing)));

        references.Add(StoryJsonAssetReference.CreateCharacter("pp_kurt", CreateFullSpriteCharacter(
            "pp_kurt",
            "Курт",
            "Персонажи/Курт/Kurt_Normal.png",
            new[]
            {
                Emotion(CharacterEmotionType.Neutral, "Персонажи/Курт/Kurt_Normal.png"),
                Emotion(CharacterEmotionType.Smile, "Персонажи/Курт/Kurt_Smile.png"),
                Emotion(CharacterEmotionType.Thinking, "Персонажи/Курт/Kurt_Thoughtful .png")
            },
            missing)));

        CharacterData robert = CreateFullSpriteCharacter(
            "pp_rob",
            "Роб",
            "Персонажи/Роберт_/Robert_Hide.png",
            new[] { Emotion(CharacterEmotionType.Idle, "Персонажи/Роберт_/Robert_Hide.png") },
            missing);
        references.Add(StoryJsonAssetReference.CreateCharacter("pp_rob", robert));
        references.Add(StoryJsonAssetReference.CreateCharacter("pp_black_silhouette", robert));
        references.Add(StoryJsonAssetReference.CreateCharacter("pp_black_silhouette_girl", robert));
        references.Add(StoryJsonAssetReference.CreateCharacter("pp_guy", robert));

        references.Add(StoryJsonAssetReference.CreateCharacter("pp_will", CreatePlaceholderCharacter("pp_will", "Уилл")));

        return hero;
    }

    static void ConfigureClothing(
        List<StoryJsonAssetReference> references,
        Dictionary<string, ClothingItem> clothingById,
        List<string> missing)
    {
        var items = new[]
        {
            CreateClothing(clothingById, "pp_outfit_ritm_goroda", "Ритм города", ClothingType.Outfit, "Главная героиня/Одежда/ritm_goroda.png", missing),
            CreateClothing(clothingById, "pp_outfit_ocharovanie", "Очарование", ClothingType.Outfit, "Главная героиня/Одежда/ocharovanie.png", missing),
            CreateClothing(clothingById, "pp_outfit_legkost", "Легкость", ClothingType.Outfit, "Главная героиня/Одежда/Legkost.PNG", missing),
            CreateClothing(clothingById, "pp_outfit_sportivny_kostyum", "Спортивный костюм", ClothingType.Outfit, "Главная героиня/Одежда/sportwear.png", missing),
            CreateClothing(clothingById, "pp_outfit_pp2_toropilas", "Торопилась", ClothingType.Outfit, "Главная героиня/Одежда/1.png", missing),
            CreateClothing(clothingById, "pp_outfit_pp2_delovoy_kostyum", "Деловой костюм", ClothingType.Outfit, "Главная героиня/Одежда/4.png", missing),
            CreateClothing(clothingById, "pp_outfit_pp2_kak_na_podium", "Как на подиум", ClothingType.Outfit, "Главная героиня/Одежда/5.png", missing),
            CreateClothing(clothingById, "pp_hair_silk_blonde", "Шелк: блонд", ClothingType.Hair, "Главная героиня/Прически/silk_blonde.png", missing),
            CreateClothing(clothingById, "pp_hair_silk_brown", "Шелк: каштан", ClothingType.Hair, "Главная героиня/Прически/silk_brown.png", missing),
            CreateClothing(clothingById, "pp_hair_silk_black", "Шелк: черный", ClothingType.Hair, "Главная героиня/Прически/silk_black.png", missing),
            CreateClothing(clothingById, "pp_hair_pp2_leave_as_is", "Оставить как есть", ClothingType.Hair, "Главная героиня/Прически/silk_brown.png", missing),
            CreateClothing(clothingById, "pp_hair_pp2_na_skoruyu_blond", "На скорую: блонд", ClothingType.Hair, "Главная героиня/Прически/1 blonde.png", missing),
            CreateClothing(clothingById, "pp_hair_pp2_na_skoruyu_dark_chestnut", "На скорую: каштан", ClothingType.Hair, "Главная героиня/Прически/1 brown.png", missing),
            CreateClothing(clothingById, "pp_hair_pp2_na_skoruyu_coal", "На скорую: черный", ClothingType.Hair, "Главная героиня/Прически/1 black.png", missing),
            CreateClothing(clothingById, "pp_hair_pp2_ukladka_blond", "Укладка: блонд", ClothingType.Hair, "Главная героиня/Прически/2 blonde.png", missing),
            CreateClothing(clothingById, "pp_hair_pp2_ukladka_dark_chestnut", "Укладка: каштан", ClothingType.Hair, "Главная героиня/Прически/2 brown.png", missing),
            CreateClothing(clothingById, "pp_hair_pp2_ukladka_coal", "Укладка: черный", ClothingType.Hair, "Главная героиня/Прически/2 black.png", missing),
            CreateClothing(clothingById, "pp_hair_pp2_hollywood_blond", "Голливуд: блонд", ClothingType.Hair, "Главная героиня/Прически/3 blonde.png", missing),
            CreateClothing(clothingById, "pp_hair_pp2_hollywood_dark_chestnut", "Голливуд: каштан", ClothingType.Hair, "Главная героиня/Прически/3 brown.png", missing),
            CreateClothing(clothingById, "pp_hair_pp2_hollywood_coal", "Голливуд: черный", ClothingType.Hair, "Главная героиня/Прически/3 black.png", missing)
        };

        foreach (ClothingItem item in items)
        {
            if (item != null)
                references.Add(StoryJsonAssetReference.CreateClothing(item.id, item));
        }
    }

    static void ConfigureMedia(List<StoryJsonAssetReference> references, List<string> missing)
    {
        AddSprite(references, "pp_bg_street_morning", "Фоны/Morning.png", missing);
        AddVideo(references, "pp_bg_cabinet", "Фоны/cabinet_day.mp4", missing);
        AddSprite(references, "pp_bg_cabinet_evening", "Фоны/cabinet_evening.png", missing);
        AddSprite(references, "pp_bg_cafeteria", "Фоны/DINING_ROOM.png", missing);
        AddSprite(references, "pp_bg_corridor", "Фоны/corridor_day.jpg", missing);
        AddSprite(references, "pp_bg_corridor_evening", "Фоны/corridor_evening.png", missing);
        AddSprite(references, "pp_bg_entrance", "Фоны/entrance.png", missing);
        AddSprite(references, "pp_bg_gym", "Фоны/GYM_POLICE.png", missing);
        AddSprite(references, "pp_bg_bedroom_gg", "Фоны/Bedroom_gg.png", missing);
        AddSprite(references, "pp_bg_phone_chat", "\u0424\u043E\u043D\u044B/Phone.png", missing);
        AddSprite(references, "pp_sms_bubble", "\u0424\u043E\u043D\u044B/sms_bubble.png", missing);
        AddVideo(references, "pp_bg_street_day", "Фоны/Street_Day.mp4", missing);
        AddSprite(references, "pp_cg_vlad", "Кат-сцены/Vlad.png", missing);
        AddSprite(references, "pp_cg_gabriel", "Кат-сцены/Gabriel.jpeg", missing);
        AddSprite(references, "pp_cg_vlad_training", "Кат-сцены/1.png", missing);
        AddSprite(references, "pp_cg_vlad_training_european_blond", "Кат-сцены/1.png", missing);
        AddSprite(references, "pp_cg_vlad_training_european_dark_chestnut", "Кат-сцены/2.png", missing);
        AddSprite(references, "pp_cg_vlad_training_european_coal", "Кат-сцены/3.png", missing);
        AddSprite(references, "pp_cg_vlad_training_african_blond", "Кат-сцены/4.png", missing);
        AddSprite(references, "pp_cg_vlad_training_african_dark_chestnut", "Кат-сцены/5.png", missing);
        AddSprite(references, "pp_cg_vlad_training_african_coal", "Кат-сцены/6.png", missing);
        AddSprite(references, "pp_cg_vlad_training_asian_blond", "Кат-сцены/7.png", missing);
        AddSprite(references, "pp_cg_vlad_training_asian_dark_chestnut", "Кат-сцены/8.png", missing);
        AddSprite(references, "pp_cg_vlad_training_asian_coal", "Кат-сцены/9.png", missing);
        AddSprite(references, "pp_mag_avatar", "Персонажи/Мэг/Mag_Normal.png", missing);
        AddSprite(references, "pp_rob_avatar", "Персонажи/Роберт_/Robert_Hide.png", missing);
        AddSprite(references, "pp_will_avatar", "Персонажи/Роберт_/Robert_Hide.png", missing);
        AddAudio(references, "pp_music_povsednevny", "Основная музыка/Every_day.wav", missing);
        AddAudio(references, "pp_music_povsednevnost", "Основная музыка/Every_day.wav", missing);
        AddAudio(references, "pp_music_tension", "Основная музыка/self_defense.wav", missing);
        AddAudio(references, "pp_music_battle", "Основная музыка/self_defense.wav", missing);
        AddAudio(references, "pp_music_vlad_theme", "Основная музыка/Vlad.wav", missing);
        AddAudio(references, "pp_music_wardrobe", "Основная музыка/Wardrobe.wav", missing);
    }

    static void RegisterGameData()
    {
        StoryData story = FindStoryData(StoryId);
        if (story == null)
            return;

        story.Configure(StoryId, StoryName, story.Chapters);
        EditorUtility.SetDirty(story);

        GameData gameData = CreateOrLoadAsset<GameData>(StoryFolder + "/Menu/" + StoryId + "_GameData.asset");
        Sprite cover = LoadSprite("Плашки/final_screen_fon.png", "game cover", new List<string>());
        gameData.Configure(StoryName, story, cover);
        EditorUtility.SetDirty(gameData);

        GameCatalog catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(GameCatalogPath);
        if (catalog != null && catalog.AddGame(gameData))
            EditorUtility.SetDirty(catalog);

        AssetDatabase.SaveAssets();
    }

    static void ConfigureWardrobeScene(CharacterData hero, List<ClothingItem> items)
    {
        if (hero == null || items == null || !File.Exists(ScenePath))
            return;

        var activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        WardrobeHeroSetupPage fallback = UnityEngine.Object.FindObjectsOfType<WardrobeHeroSetupPage>(true).FirstOrDefault();
        if (fallback == null)
            return;

        ConfigureFallbackWardrobe(fallback);

        WardrobeHeroSetupPage page = UnityEngine.Object.FindObjectsOfType<WardrobeHeroSetupPage>(true)
            .FirstOrDefault(candidate => candidate != null && candidate.name == "WardrobeHeroSetupPage_PP");
        if (page == null)
        {
            GameObject clone = UnityEngine.Object.Instantiate(fallback.gameObject, fallback.transform.parent);
            clone.name = "WardrobeHeroSetupPage_PP";
            page = clone.GetComponent<WardrobeHeroSetupPage>();
        }

        List<ClothingItem> outfits = items.Where(item => item != null && item.type == ClothingType.Outfit).ToList();
        List<ClothingItem> hairs = items.Where(item => item != null && item.type == ClothingType.Hair).ToList();
        ConfigurePpWardrobePage(page, hero, outfits, hairs);

        EditorUtility.SetDirty(page);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
    }

    static void ConfigureFallbackWardrobe(WardrobeHeroSetupPage page)
    {
        var so = new SerializedObject(page);
        ClearStringArray(so.FindProperty("_storyIds"));
        ClearStringArray(so.FindProperty("_chapterIds"));
        SetBool(so.FindProperty("_useAsFallbackForUnmatchedStories"), true);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ConfigurePpWardrobePage(
        WardrobeHeroSetupPage page,
        CharacterData hero,
        List<ClothingItem> outfits,
        List<ClothingItem> hairs)
    {
        var so = new SerializedObject(page);
        SetStringArray(so.FindProperty("_storyIds"), new[] { StoryId });
        ClearStringArray(so.FindProperty("_chapterIds"));
        SetBool(so.FindProperty("_useAsFallbackForUnmatchedStories"), false);
        SetObject(so.FindProperty("_targetCharacter"), hero);
        SetString(so.FindProperty("_targetCharacterId"), "hero");
        SetString(so.FindProperty("_completionPrefsKey"), "VN_WARDROBE_HERO_SETUP_DONE_" + StoryId);
        SetObject(so.FindProperty("_defaultOutfitItem"), outfits.FirstOrDefault());
        SetObject(so.FindProperty("_defaultHairItem"), hairs.FirstOrDefault());
        SetObjectArray(so.FindProperty("_outfitItems"), outfits.Cast<UnityEngine.Object>().ToArray());
        SetObjectArray(so.FindProperty("_hairItems"), hairs.Cast<UnityEngine.Object>().ToArray());
        ConfigureAppearanceOptions(so.FindProperty("_appearanceOptions"));
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ConfigureAppearanceOptions(SerializedProperty property)
    {
        if (property == null || !property.isArray)
            return;

        var options = new[]
        {
            new Tuple<string, AppearanceType, Sprite>("Европейка", AppearanceType.European, LoadSprite("Главная героиня/Тело/Европейка/Без подсветки.PNG", "appearance european", new List<string>())),
            new Tuple<string, AppearanceType, Sprite>("Афроамериканка", AppearanceType.African, LoadSprite("Главная героиня/Тело/Афромериканка_/Без подсветки.PNG", "appearance african", new List<string>())),
            new Tuple<string, AppearanceType, Sprite>("Азиатка", AppearanceType.Asian, LoadSprite("Главная героиня/Тело/Азиатка/Без подсветки.PNG", "appearance asian", new List<string>()))
        };

        property.arraySize = options.Length;
        for (int i = 0; i < options.Length; i++)
        {
            SerializedProperty item = property.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("label").stringValue = options[i].Item1;
            item.FindPropertyRelative("type").enumValueIndex = (int)options[i].Item2;
            item.FindPropertyRelative("previewSprite").objectReferenceValue = options[i].Item3;
            item.FindPropertyRelative("enabled").boolValue = true;
        }
    }

    static void ReplaceLargestImageSprite(GameObject root, Sprite sprite)
    {
        if (root == null || sprite == null)
            return;

        Image best = null;
        float bestArea = 0f;
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image == null || image.sprite == null)
                continue;

            RectTransform rect = image.rectTransform;
            Vector2 size = rect != null ? rect.rect.size : Vector2.zero;
            float area = Mathf.Abs(size.x * size.y);
            if (area <= bestArea)
                continue;

            bestArea = area;
            best = image;
        }

        if (best != null)
        {
            best.sprite = sprite;
            best.preserveAspect = false;
            EditorUtility.SetDirty(best);
        }
    }

    static CharacterData CreateLayeredCharacter(
        string id,
        string displayName,
        string bodyPath,
        string faceFolder,
        IEnumerable<FaceBinding> faces,
        List<string> missing)
    {
        CharacterData character = CreateOrLoadAsset<CharacterData>(StoryFolder + "/Characters/" + id + ".asset");
        character.name = id;
        character.characterName = displayName;
        character.inheritAppearanceFromPlayer = false;
        character.useLayeredEmotions = true;
        character.bodySprite = LoadSprite(bodyPath, id + ".body", missing);
        character.defaultSprite = character.bodySprite;
        character.emotionLayers = faces
            .Select(face => new CharacterEmotionLayer
            {
                emotion = face.Emotion,
                faceSprite = LoadSprite(faceFolder + "/" + face.FileName, id + "." + face.Emotion, missing)
            })
            .Where(layer => layer.faceSprite != null)
            .ToList();
        EditorUtility.SetDirty(character);
        return character;
    }

    static CharacterData CreateFullSpriteCharacter(
        string id,
        string displayName,
        string defaultSpritePath,
        IEnumerable<EmotionBinding> emotions,
        List<string> missing)
    {
        CharacterData character = CreateOrLoadAsset<CharacterData>(StoryFolder + "/Characters/" + id + ".asset");
        character.name = id;
        character.characterName = displayName;
        character.inheritAppearanceFromPlayer = false;
        character.useLayeredEmotions = false;
        character.defaultSprite = LoadSprite(defaultSpritePath, id + ".default", missing);
        character.emotions = emotions
            .Select(binding => new CharacterEmotion
            {
                emotion = binding.Emotion,
                sprite = LoadSprite(binding.FilePath, id + "." + binding.Emotion, missing)
            })
            .Where(entry => entry.sprite != null)
            .ToList();
        EditorUtility.SetDirty(character);
        return character;
    }

    static CharacterData CreatePlaceholderCharacter(string id, string displayName)
    {
        CharacterData character = CreateOrLoadAsset<CharacterData>(StoryFolder + "/Characters/" + id + ".asset");
        character.name = id;
        character.characterName = displayName;
        EditorUtility.SetDirty(character);
        return character;
    }

    static AppearanceVariant CreateAppearanceVariant(AppearanceType type, string bodyPath, List<string> missing)
    {
        return new AppearanceVariant
        {
            appearanceType = type,
            defaultSprite = LoadSprite(bodyPath, "appearance." + type, missing)
        };
    }

    static List<CharacterEmotionLayer> BuildHeroFaceLayers(string folder, string prefix, List<string> missing)
    {
        var files = new[]
        {
            Face(CharacterEmotionType.Neutral, prefix + "normal.PNG"),
            Face(CharacterEmotionType.Idle, prefix + "normal.PNG"),
            Face(CharacterEmotionType.Smile, prefix + "smile.PNG"),
            Face(CharacterEmotionType.Smirk, prefix + "smirk.PNG"),
            Face(CharacterEmotionType.RaisedEyebrow, prefix + "raised_eyebrow.PNG"),
            Face(CharacterEmotionType.EyeRoll, prefix + "roll_eyes.PNG"),
            Face(CharacterEmotionType.LookToSide, prefix + "look_to_the_insite.PNG"),
            Face(CharacterEmotionType.LookToInside, prefix + "look_to_the_insite.PNG"),
            Face(CharacterEmotionType.Thinking, prefix + "thoughtful.PNG"),
            Face(CharacterEmotionType.Confused, prefix + "confusion.PNG"),
            Face(CharacterEmotionType.Embarrassed, prefix + "embarrassment.PNG"),
            Face(CharacterEmotionType.Annoyed, prefix + "angry.PNG"),
            Face(CharacterEmotionType.Angry, prefix + "angry.PNG"),
            Face(CharacterEmotionType.Sad, prefix + "sad.PNG"),
            Face(CharacterEmotionType.Frown, prefix + "pursed_lips.PNG")
        };

        return files
            .Select(face => new CharacterEmotionLayer
            {
                emotion = face.Emotion,
                faceSprite = LoadSprite(folder + "/" + face.FileName, "hero." + face.Emotion, missing)
            })
            .Where(layer => layer.faceSprite != null)
            .ToList();
    }

    static ClothingItem CreateClothing(
        Dictionary<string, ClothingItem> clothingById,
        string id,
        string displayName,
        ClothingType type,
        string spritePath,
        List<string> missing)
    {
        ClothingItem item = CreateOrLoadAsset<ClothingItem>(StoryFolder + "/WardrobeItems/" + id + ".asset");
        item.name = id;
        item.id = id;
        item.type = type;
        item.sprite = LoadSprite(spritePath, id, missing);
        item.wardrobePreserveAspect = true;
        SetPrivateString(item, "displayName", displayName);
        ConfigureClothingAvailability(item, id);
        EditorUtility.SetDirty(item);
        clothingById[id] = item;
        return item;
    }

    static void ConfigureClothingAvailability(ClothingItem item, string id)
    {
        if (item == null)
            return;

        string[] visibleChapters = GetVisibleChaptersForClothing(id);
        string[] hiddenChapters = string.Equals(id, "pp_outfit_sportivny_kostyum", StringComparison.OrdinalIgnoreCase)
            ? new[] { "pp_1" }
            : Array.Empty<string>();

        var serialized = new SerializedObject(item);
        ClearStringArray(serialized.FindProperty("visibleInStoryIds"));
        SetStringArray(serialized.FindProperty("visibleInChapterIds"), visibleChapters);
        ClearStringArray(serialized.FindProperty("hiddenInStoryIds"));
        SetStringArray(serialized.FindProperty("hiddenInChapterIds"), hiddenChapters);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static string[] GetVisibleChaptersForClothing(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Array.Empty<string>();

        if (id.StartsWith("pp_outfit_pp2_", StringComparison.OrdinalIgnoreCase) ||
            id.StartsWith("pp_hair_pp2_", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(id, "pp_outfit_sportivny_kostyum", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "pp_2" };
        }

        if (id.StartsWith("pp_outfit_", StringComparison.OrdinalIgnoreCase) ||
            id.StartsWith("pp_hair_silk_", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "pp_1" };
        }

        return Array.Empty<string>();
    }


    static void AddSprite(List<StoryJsonAssetReference> references, string id, string relativePath, List<string> missing)
    {
        Sprite sprite = LoadSprite(relativePath, id, missing);
        if (sprite != null)
            references.Add(StoryJsonAssetReference.CreateSprite(id, sprite));
    }

    static void AddVideo(List<StoryJsonAssetReference> references, string id, string relativePath, List<string> missing)
    {
        string path = ArtRoot + "/" + relativePath;
        VideoClip video = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
        if (video == null)
        {
            missing.Add(id + ": missing VideoClip at " + path);
            return;
        }

        references.Add(StoryJsonAssetReference.CreateVideo(id, video));
    }

    static void AddAudio(List<StoryJsonAssetReference> references, string id, string relativePath, List<string> missing)
    {
        string path = ArtRoot + "/" + relativePath;
        AudioClip audio = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (audio == null)
        {
            missing.Add(id + ": missing AudioClip at " + path);
            return;
        }

        references.Add(StoryJsonAssetReference.CreateAudio(id, audio));
    }

    static Sprite LoadSprite(string relativePath, string idForError, List<string> missing)
    {
        string path = ArtRoot + "/" + relativePath;
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
            return sprite;

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;
        }

        missing.Add(idForError + ": missing Sprite at " + path);
        return null;
    }

    static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
    {
        path = path.Replace("\\", "/");
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        EnsureFolder(Path.GetDirectoryName(path).Replace("\\", "/"));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static void EnsureStoryFolders()
    {
        EnsureFolder(StoryFolder);
        foreach (string folder in new[] { "Chapters", "Graphs", "Characters", "WardrobeItems", "Backgrounds", "Cutscenes", "Audio", "UI", "Json", "Menu" })
            EnsureFolder(StoryFolder + "/" + folder);
    }

    static void EnsureFolder(string folder)
    {
        folder = (folder ?? "").Replace("\\", "/").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            return;

        string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
        string name = Path.GetFileName(folder);
        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(parent, name);
    }

    static StoryData FindStoryData(string storyId)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:StoryData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StoryData story = AssetDatabase.LoadAssetAtPath<StoryData>(path);
            if (story != null && string.Equals(story.storyId, storyId, StringComparison.OrdinalIgnoreCase))
                return story;
        }

        return null;
    }

    static void SetPrivateString(UnityEngine.Object target, string propertyName, string value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value ?? "";
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetStringArray(SerializedProperty property, string[] values)
    {
        if (property == null || !property.isArray)
            return;

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).stringValue = values[i] ?? "";
    }

    static void ClearStringArray(SerializedProperty property)
    {
        if (property != null && property.isArray)
            property.arraySize = 0;
    }

    static void SetObjectArray(SerializedProperty property, UnityEngine.Object[] values)
    {
        if (property == null || !property.isArray)
            return;

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void SetObject(SerializedProperty property, UnityEngine.Object value)
    {
        if (property != null)
            property.objectReferenceValue = value;
    }

    static void SetString(SerializedProperty property, string value)
    {
        if (property != null)
            property.stringValue = value ?? "";
    }

    static void SetBool(SerializedProperty property, bool value)
    {
        if (property != null)
            property.boolValue = value;
    }

    static EmotionBinding Emotion(CharacterEmotionType emotion, string filePath)
    {
        return new EmotionBinding(emotion, filePath);
    }

    static FaceBinding Face(CharacterEmotionType emotion, string fileName)
    {
        return new FaceBinding(emotion, fileName);
    }

    readonly struct EmotionBinding
    {
        public readonly CharacterEmotionType Emotion;
        public readonly string FilePath;

        public EmotionBinding(CharacterEmotionType emotion, string filePath)
        {
            Emotion = emotion;
            FilePath = filePath;
        }
    }

    readonly struct FaceBinding
    {
        public readonly CharacterEmotionType Emotion;
        public readonly string FileName;

        public FaceBinding(CharacterEmotionType emotion, string fileName)
        {
            Emotion = emotion;
            FileName = fileName;
        }
    }
}
#endif
