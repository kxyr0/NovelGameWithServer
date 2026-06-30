using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class StoryJsonSmokeTests
{
    [Test]
    public void CanonicalJson_DialogueChoice_RoundTripsNodeIdsAndLinks()
    {
        string json =
            "{\"version\":1,\"storyId\":\"story_json\",\"chapterId\":\"chapter_json\",\"episodeId\":\"ep_json\",\"title\":\"JSON Chapter\"," +
            "\"characters\":[{\"id\":\"hero\",\"name\":\"Алиса\"}]," +
            "\"nodes\":[" +
            "{\"id\":\"start\",\"type\":\"start\",\"next\":\"dialogue_1\"}," +
            "{\"id\":\"dialogue_1\",\"type\":\"dialogue\",\"lines\":[{\"speaker\":\"hero\",\"emotion\":\"Happy\",\"text\":\"Привет\"}],\"next\":\"choice_1\"}," +
            "{\"id\":\"choice_1\",\"type\":\"choice\",\"choicePrompt\":\"Что дальше?\",\"choices\":[{\"text\":\"Пойти\",\"next\":\"end_a\"},{\"text\":\"Остаться\",\"next\":\"end_b\"}]}," +
            "{\"id\":\"end_a\",\"type\":\"dialogue\",\"lines\":[{\"text\":\"Ветка A\"}]}," +
            "{\"id\":\"end_b\",\"type\":\"dialogue\",\"lines\":[{\"text\":\"Ветка B\"}]}" +
            "]}";

        StoryGraph graph = null;
        try
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[StoryJson\] Character 'hero' was not found\."));
            Assert.That(StoryJsonConverter.TryBuildGraph(json, "fallback", out graph, out string reason), Is.True, reason);

            var choice = graph.nodes.OfType<ChoiceNode>().FirstOrDefault(node => node.guid == "choice_1");
            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.GetOutputPort("choices 0").Connection.node, Is.SameAs(graph.nodes.OfType<BaseStoryNode>().First(node => node.guid == "end_a")));
            Assert.That(choice.GetOutputPort("choices 1").Connection.node, Is.SameAs(graph.nodes.OfType<BaseStoryNode>().First(node => node.guid == "end_b")));

            Assert.That(StoryJsonConverter.TryExportGraph(graph, out string exported, out reason), Is.True, reason);
            Assert.That(StoryJsonConverter.TryParseDocument(exported, out var document, out reason), Is.True, reason);

            var exportedChoice = document.nodes.FirstOrDefault(node => node.id == "choice_1");
            Assert.That(exportedChoice, Is.Not.Null);
            Assert.That(exportedChoice.choices[0].next, Is.EqualTo("end_a"));
            Assert.That(exportedChoice.choices[1].next, Is.EqualTo("end_b"));
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void CanonicalJson_AllSupportedNodeTypes_BuildsGraph()
    {
        string json =
            "{\"version\":1,\"chapterId\":\"chapter_all\",\"episodeId\":\"ep_all\",\"nodes\":[" +
            "{\"id\":\"start\",\"type\":\"start\",\"next\":\"scene_1\"}," +
            "{\"id\":\"scene_1\",\"type\":\"scene\",\"label\":\"Forest\",\"next\":\"dialogue_1\"}," +
            "{\"id\":\"dialogue_1\",\"type\":\"dialogue\",\"lines\":[{\"text\":\"Line\"}],\"next\":\"choice_1\"}," +
            "{\"id\":\"choice_1\",\"type\":\"choice\",\"choices\":[{\"text\":\"Stat\",\"next\":\"stat_1\"},{\"text\":\"Variable\",\"next\":\"variable_1\"}]}," +
            "{\"id\":\"stat_1\",\"type\":\"statChange\",\"statId\":\"reputation\",\"statDelta\":1,\"next\":\"condition_1\"}," +
            "{\"id\":\"variable_1\",\"type\":\"variableChange\",\"variableKey\":\"flag\",\"deltaValue\":1,\"add\":true,\"next\":\"camera_1\"}," +
            "{\"id\":\"condition_1\",\"type\":\"condition\",\"variableKey\":\"flag\",\"requiredValue\":1,\"trueNext\":\"premium_1\",\"falseNext\":\"image_1\"}," +
            "{\"id\":\"premium_1\",\"type\":\"premium\",\"cost\":3,\"successNext\":\"phone_1\",\"failNext\":\"effect_1\"}," +
            "{\"id\":\"camera_1\",\"type\":\"camera\",\"mode\":\"Offset\",\"xOffset\":100,\"duration\":0.2,\"next\":\"image_1\"}," +
            "{\"id\":\"image_1\",\"type\":\"image\",\"caption\":\"Закрыть\",\"next\":\"phone_1\"}," +
            "{\"id\":\"phone_1\",\"type\":\"phoneDialogue\",\"contactName\":\"Этан\",\"messages\":[{\"text\":\"SMS\",\"side\":\"Incoming\"}],\"next\":\"effect_1\"}," +
            "{\"id\":\"effect_1\",\"type\":\"effect\",\"effect\":\"Shake\",\"duration\":0.5,\"intensity\":2,\"next\":\"appearance_1\"}," +
            "{\"id\":\"appearance_1\",\"type\":\"appearanceChoice\",\"promptText\":\"Выбор\",\"appearanceOptions\":[{\"label\":\"Default\",\"type\":\"Default\",\"next\":\"wardrobe_choice_1\"}]}," +
            "{\"id\":\"wardrobe_choice_1\",\"type\":\"wardrobeChoice\",\"characterId\":\"hero\",\"clothes\":[\"missing_clothing\"],\"exits\":[\"add_clothing_1\"]}," +
            "{\"id\":\"add_clothing_1\",\"type\":\"addClothing\",\"clothing\":\"missing_clothing\",\"next\":\"open_wardrobe_1\"}," +
            "{\"id\":\"open_wardrobe_1\",\"type\":\"openWardrobe\",\"next\":\"wardrobe_check_1\"}," +
            "{\"id\":\"wardrobe_check_1\",\"type\":\"wardrobeCheck\",\"itemId\":\"missing_clothing\",\"hasItemNext\":\"end_yes\",\"noItemNext\":\"end_no\"}," +
            "{\"id\":\"end_yes\",\"type\":\"dialogue\",\"lines\":[{\"text\":\"yes\"}]}," +
            "{\"id\":\"end_no\",\"type\":\"dialogue\",\"lines\":[{\"text\":\"no\"}]}" +
            "]}";

        StoryGraph graph = null;
        try
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[StoryJson\] Character 'hero' was not found\."));
            Assert.That(StoryJsonConverter.TryBuildGraph(json, "fallback", out graph, out string reason), Is.True, reason);
            AssertNode<StartNode>(graph);
            AssertNode<SceneSetupNode>(graph);
            AssertNode<DialogueNode>(graph);
            AssertNode<ChoiceNode>(graph);
            AssertNode<StatChangeNode>(graph);
            AssertNode<VariableChangeNode>(graph);
            AssertNode<ConditionNode>(graph);
            AssertNode<PremiumNode>(graph);
            AssertNode<CameraNode>(graph);
            AssertNode<ImageNode>(graph);
            AssertNode<PhoneDialogueNode>(graph);
            AssertNode<EffectNode>(graph);
            AssertNode<AppearanceChoiceNode>(graph);
            AssertNode<WardrobeChoiceNode>(graph);
            AssertNode<AddClothingNode>(graph);
            AssertNode<OpenWardrobeNode>(graph);
            AssertNode<WardrobeCheckNode>(graph);
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void CanonicalJson_Condition_CanCompareTwoStats()
    {
        string json =
            "{\"version\":1,\"chapterId\":\"chapter_condition\",\"episodeId\":\"ep_condition\",\"nodes\":[" +
            "{\"id\":\"start\",\"type\":\"start\",\"next\":\"condition_1\"}," +
            "{\"id\":\"condition_1\",\"type\":\"condition\",\"variableKey\":\"principles\",\"comparison\":\"greaterThan\",\"compareVariableKey\":\"feelings\",\"trueNext\":\"principles_win\",\"falseNext\":\"feelings_win\"}," +
            "{\"id\":\"principles_win\",\"type\":\"dialogue\",\"lines\":[{\"text\":\"Principles\"}]}," +
            "{\"id\":\"feelings_win\",\"type\":\"dialogue\",\"lines\":[{\"text\":\"Feelings\"}]}" +
            "]}";

        StoryGraph graph = null;
        try
        {
            Assert.That(StoryJsonConverter.TryBuildGraph(json, "fallback", out graph, out string reason), Is.True, reason);

            var condition = graph.nodes.OfType<ConditionNode>().FirstOrDefault(node => node.guid == "condition_1");
            Assert.That(condition, Is.Not.Null);
            Assert.That(condition.variableKey, Is.EqualTo("principles"));
            Assert.That(condition.comparison, Is.EqualTo(ConditionComparison.GreaterThan));
            Assert.That(condition.compareVariableKey, Is.EqualTo("feelings"));

            Assert.That(StoryJsonConverter.TryExportGraph(graph, out string exported, out reason), Is.True, reason);
            Assert.That(StoryJsonConverter.TryParseDocument(exported, out var document, out reason), Is.True, reason);

            var exportedCondition = document.nodes.FirstOrDefault(node => node.id == "condition_1");
            Assert.That(exportedCondition, Is.Not.Null);
            Assert.That(exportedCondition.variableKey, Is.EqualTo("principles"));
            Assert.That(exportedCondition.comparison, Is.EqualTo(nameof(ConditionComparison.GreaterThan)));
            Assert.That(exportedCondition.compareVariableKey, Is.EqualTo("feelings"));
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void CanonicalJson_MissingCharacter_LogsErrorAndBuildsSafeFallback()
    {
        string json =
            "{\"version\":1,\"nodes\":[" +
            "{\"id\":\"dialogue_1\",\"type\":\"dialogue\",\"activeCharacters\":[{\"character\":\"missing_hero\"}],\"lines\":[{\"speaker\":\"missing_hero\",\"text\":\"Hi\"}]}" +
            "]}";

        StoryGraph graph = null;
        try
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[StoryJson\] Character 'missing_hero' was not found\."));
            Assert.That(StoryJsonConverter.TryBuildGraph(json, "ep", out graph, out string reason), Is.True, reason);

            var dialogue = graph.nodes.OfType<DialogueNode>().FirstOrDefault();
            Assert.That(dialogue, Is.Not.Null);
            Assert.That(dialogue.activeCharacters[0].character, Is.Not.Null);
            Assert.That(dialogue.lines[0].speaker, Is.SameAs(dialogue.activeCharacters[0].character));
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void CanonicalJson_UnknownNodeType_ReturnsReadableError()
    {
        string json = "{\"version\":1,\"nodes\":[{\"id\":\"bad\",\"type\":\"unknownMagic\"}]}";

        Assert.That(StoryJsonConverter.TryBuildGraph(json, "ep", out _, out string reason), Is.False);
        Assert.That(reason, Does.Contain("Unknown node type"));
        Assert.That(reason, Does.Contain("bad"));
    }

    [Test]
    public void CanonicalJson_MissingChoiceNext_ReturnsReadableError()
    {
        string json =
            "{\"version\":1,\"nodes\":[" +
            "{\"id\":\"start\",\"type\":\"start\",\"next\":\"choice_1\"}," +
            "{\"id\":\"choice_1\",\"type\":\"choice\",\"choices\":[{\"text\":\"Broken\"}]}" +
            "]}";

        Assert.That(StoryJsonConverter.TryBuildGraph(json, "ep", out _, out string reason), Is.False);
        Assert.That(reason, Does.Contain("has no next"));
    }

    [Test]
    public void CanonicalJson_AssetLibrary_ResolvesCharacterById()
    {
        var library = ScriptableObject.CreateInstance<StoryJsonAssetLibrary>();
        var character = ScriptableObject.CreateInstance<CharacterData>();
        StoryGraph graph = null;

        try
        {
            character.characterName = "Алиса";
            library.Configure(new[]
            {
                StoryJsonAssetReference.CreateCharacter("hero", character)
            });

            string json =
                "{\"version\":1,\"nodes\":[" +
                "{\"id\":\"dialogue_1\",\"type\":\"dialogue\",\"lines\":[{\"speaker\":\"hero\",\"text\":\"Привет\"}]}" +
                "]}";

            var resolver = new StoryJsonAssetLibraryResolver(library);
            Assert.That(StoryJsonConverter.TryBuildGraph(json, "ep", out graph, out string reason, resolver), Is.True, reason);

            var dialogue = graph.nodes.OfType<DialogueNode>().FirstOrDefault();
            Assert.That(dialogue, Is.Not.Null);
            Assert.That(dialogue.lines[0].speaker, Is.SameAs(character));
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(library);
        }
    }

    [Test]
    public void CanonicalJson_AutoBuildsActiveCharactersFromLineSpeakers()
    {
        var library = ScriptableObject.CreateInstance<StoryJsonAssetLibrary>();
        var hero = ScriptableObject.CreateInstance<CharacterData>();
        var npc = ScriptableObject.CreateInstance<CharacterData>();
        StoryGraph graph = null;

        try
        {
            hero.name = "hero";
            hero.characterName = "Алиса";
            hero.inheritAppearanceFromPlayer = true;
            npc.name = "ivan";
            npc.characterName = "Иван";

            library.Configure(new[]
            {
                StoryJsonAssetReference.CreateCharacter("hero", hero),
                StoryJsonAssetReference.CreateCharacter("ivan", npc)
            });

            string json =
                "{\"version\":1,\"nodes\":[" +
                "{\"id\":\"dialogue_1\",\"type\":\"dialogue\",\"lines\":[" +
                "{\"speaker\":\"hero\",\"emotion\":\"Happy\",\"text\":\"Привет\"}," +
                "{\"speaker\":\"ivan\",\"emotion\":\"Serious\",\"text\":\"Ты опоздала\"}," +
                "{\"speaker\":\"hero\",\"emotion\":\"Idle\",\"text\":\"Знаю\"}" +
                "]}]}";

            var resolver = new StoryJsonAssetLibraryResolver(library);
            Assert.That(StoryJsonConverter.TryBuildGraph(json, "ep", out graph, out string reason, resolver), Is.True, reason);

            var dialogue = graph.nodes.OfType<DialogueNode>().FirstOrDefault();
            Assert.That(dialogue, Is.Not.Null);
            Assert.That(dialogue.activeCharacters, Has.Count.EqualTo(2));
            Assert.That(dialogue.activeCharacters[0].character, Is.SameAs(hero));
            Assert.That(dialogue.activeCharacters[0].position, Is.EqualTo(CharacterPosition.Left));
            Assert.That(dialogue.activeCharacters[1].character, Is.SameAs(npc));
            Assert.That(dialogue.activeCharacters[1].position, Is.EqualTo(CharacterPosition.Right));
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);
            Object.DestroyImmediate(hero);
            Object.DestroyImmediate(npc);
            Object.DestroyImmediate(library);
        }
    }

    [Test]
    public void CanonicalJson_HeroBuildCutsceneOverrides_ImportAndExportHairRules()
    {
        var library = ScriptableObject.CreateInstance<StoryJsonAssetLibrary>();
        var fallbackTexture = new Texture2D(1, 1);
        var overrideTexture = new Texture2D(1, 1);
        var fallbackSprite = Sprite.Create(fallbackTexture, new Rect(0, 0, 1, 1), Vector2.zero);
        var overrideSprite = Sprite.Create(overrideTexture, new Rect(0, 0, 1, 1), Vector2.zero);
        StoryGraph graph = null;

        try
        {
            library.Configure(new[]
            {
                StoryJsonAssetReference.CreateSprite("fallback_cg", fallbackSprite),
                StoryJsonAssetReference.CreateSprite("override_cg", overrideSprite)
            });

            string json =
                "{\"version\":2,\"nodes\":[" +
                "{\"id\":\"image_1\",\"type\":\"image\",\"image\":\"fallback_cg\",\"heroBuildCutsceneOverrides\":[" +
                "{\"enabled\":true,\"ruleName\":\"blond\",\"image\":\"override_cg\",\"hairIds\":[\"hair_a\",\"hair_b\"]}" +
                "]}" +
                "]}";

            var resolver = new StoryJsonAssetLibraryResolver(library);
            Assert.That(StoryJsonConverter.TryBuildGraph(json, "ep", out graph, out string reason, resolver), Is.True, reason);

            var imageNode = graph.nodes.OfType<ImageNode>().FirstOrDefault();
            Assert.That(imageNode, Is.Not.Null);
            Assert.That(imageNode.defaultImage, Is.SameAs(fallbackSprite));
            Assert.That(imageNode.heroBuildCutsceneOverrides, Has.Count.EqualTo(2));
            Assert.That(imageNode.heroBuildCutsceneOverrides[0].HairId, Is.EqualTo("hair_a"));
            Assert.That(imageNode.heroBuildCutsceneOverrides[1].HairId, Is.EqualTo("hair_b"));
            Assert.That(imageNode.heroBuildCutsceneOverrides[0].DefaultImage, Is.SameAs(overrideSprite));

            Assert.That(StoryJsonConverter.TryExportGraph(graph, out string exported, out reason, resolver), Is.True, reason);
            Assert.That(StoryJsonConverter.TryParseDocument(exported, out var document, out reason), Is.True, reason);

            var exportedImage = document.nodes.FirstOrDefault(node => node.id == "image_1");
            Assert.That(exportedImage, Is.Not.Null);
            Assert.That(exportedImage.heroBuildCutsceneOverrides, Has.Count.EqualTo(2));
            Assert.That(exportedImage.heroBuildCutsceneOverrides[0].image, Is.EqualTo("override_cg"));
            Assert.That(exportedImage.heroBuildCutsceneOverrides[0].hairId, Is.EqualTo("hair_a"));
            Assert.That(exportedImage.heroBuildCutsceneOverrides[1].hairId, Is.EqualTo("hair_b"));
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);
            Object.DestroyImmediate(fallbackSprite);
            Object.DestroyImmediate(overrideSprite);
            Object.DestroyImmediate(fallbackTexture);
            Object.DestroyImmediate(overrideTexture);
            Object.DestroyImmediate(library);
        }
    }

    [Test]
    public void PpTrainingCutsceneOverrides_SelectExpectedVariantForHeroBuilds()
    {
        const string jsonPath = "Assets/_MyProject/Data/Stories/privychka_pritvoryatsya/PP_2.json";
        const string libraryPath = "Assets/_MyProject/Data/Stories/privychka_pritvoryatsya/privychka_pritvoryatsya_JsonAssetLibrary.asset";
        const string cutsceneNodeId = "pp2_image_vlad_training_cutscene_001";

        var library = AssetDatabase.LoadAssetAtPath<StoryJsonAssetLibrary>(libraryPath);
        Assert.That(library, Is.Not.Null, $"Asset library was not found: {libraryPath}");
        Assert.That(File.Exists(jsonPath), Is.True, $"PP_2 source json was not found: {jsonPath}");

        StoryGraph graph = null;
        try
        {
            string json = File.ReadAllText(jsonPath);
            var resolver = new StoryJsonAssetLibraryResolver(library);
            Assert.That(StoryJsonConverter.TryBuildGraph(json, "pp_2", out graph, out string reason, resolver), Is.True, reason);

            var imageNode = graph.nodes.OfType<ImageNode>().FirstOrDefault(node => node.guid == cutsceneNodeId);
            Assert.That(imageNode, Is.Not.Null, $"Cutscene node was not found: {cutsceneNodeId}");
            Assert.That(imageNode.defaultImage, Is.Not.Null, "Fallback image must stay assigned.");

            var cases = new[]
            {
                new PpTrainingCutsceneCase(AppearanceType.European, "pp_hair_pp2_hollywood_blond", "pp_cg_vlad_training_european_blond"),
                new PpTrainingCutsceneCase(AppearanceType.European, "pp_hair_pp2_na_skoruyu_dark_chestnut", "pp_cg_vlad_training_european_dark_chestnut"),
                new PpTrainingCutsceneCase(AppearanceType.European, "pp_hair_pp2_ukladka_coal", "pp_cg_vlad_training_european_coal"),
                new PpTrainingCutsceneCase(AppearanceType.African, "pp_hair_pp2_na_skoruyu_blond", "pp_cg_vlad_training_african_blond"),
                new PpTrainingCutsceneCase(AppearanceType.African, "pp_hair_pp2_ukladka_dark_chestnut", "pp_cg_vlad_training_african_dark_chestnut"),
                new PpTrainingCutsceneCase(AppearanceType.African, "pp_hair_pp2_hollywood_coal", "pp_cg_vlad_training_african_coal"),
                new PpTrainingCutsceneCase(AppearanceType.Asian, "pp_hair_pp2_ukladka_blond", "pp_cg_vlad_training_asian_blond"),
                new PpTrainingCutsceneCase(AppearanceType.Asian, "pp_hair_pp2_hollywood_dark_chestnut", "pp_cg_vlad_training_asian_dark_chestnut"),
                new PpTrainingCutsceneCase(AppearanceType.Asian, "pp_hair_pp2_na_skoruyu_coal", "pp_cg_vlad_training_asian_coal")
            };

            foreach (var testCase in cases)
            {
                string selectedId = ResolveFirstMatchingCutsceneAssetId(imageNode.heroBuildCutsceneOverrides, testCase.Appearance, testCase.HairId);
                TestContext.WriteLine($"{testCase.Appearance} + {testCase.HairId} -> {selectedId}");
                Assert.That(selectedId, Is.EqualTo(testCase.ExpectedImageId));
            }

            string fallbackId = ResolveFirstMatchingCutsceneAssetId(
                imageNode.heroBuildCutsceneOverrides,
                AppearanceType.European,
                "pp_hair_pp2_leave_as_is");
            TestContext.WriteLine($"European + pp_hair_pp2_leave_as_is -> {fallbackId}");
            Assert.That(fallbackId, Is.EqualTo("fallback"));
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void CanonicalJson_ImageField_ResolvesNonSpriteMedia()
    {
        var library = ScriptableObject.CreateInstance<StoryJsonAssetLibrary>();
        var gif = new TextAsset("gif-bytes");
        StoryGraph graph = null;

        try
        {
            library.Configure(new[]
            {
                StoryJsonAssetReference.CreateText("inline_gif", gif)
            });

            string json =
                "{\"version\":1,\"nodes\":[" +
                "{\"id\":\"image_1\",\"type\":\"image\",\"image\":\"inline_gif\",\"caption\":\"Close\"}" +
                "]}";

            var resolver = new StoryJsonAssetLibraryResolver(library);
            Assert.That(StoryJsonConverter.TryBuildGraph(json, "ep", out graph, out string reason, resolver), Is.True, reason);

            var imageNode = graph.nodes.OfType<ImageNode>().FirstOrDefault();
            Assert.That(imageNode, Is.Not.Null);
            Assert.That(imageNode.image, Is.Null);
            Assert.That(imageNode.gif, Is.SameAs(gif));
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);
            Object.DestroyImmediate(gif);
            Object.DestroyImmediate(library);
        }
    }

    [Test]
    public void LegacyRemoteJson_StillBuildsGraph()
    {
        string json =
            "{\"scenes\":[{\"sceneDescription\":\"Legacy scene\",\"nodes\":[" +
            "{\"type\":\"dialogue\",\"guid\":\"legacy_dialogue\",\"lines\":[{\"speaker\":\"Лена\",\"emotion\":\"Happy\",\"text\":\"Старый формат работает\"}]}" +
            "]}]}";

        StoryGraph graph = null;
        try
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[StoryJson\] Character '.+' was not found\."));
            Assert.That(RemoteStoryGraphImporter.TryBuildGraph("legacy_ep", json, out graph, out string reason), Is.True, reason);
            Assert.That(graph.nodes.OfType<SceneSetupNode>().Any(), Is.True);
            Assert.That(graph.nodes.OfType<DialogueNode>().Any(node => node.guid == "legacy_dialogue"), Is.True);
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);
        }
    }

    private static string ResolveFirstMatchingCutsceneAssetId(
        IReadOnlyList<HeroBuildCutsceneOverride> overrides,
        AppearanceType appearance,
        string hairId)
    {
        var state = new HeroCustomizationState
        {
            appearance = appearance,
            hairId = hairId
        };
        state.Normalized();

        if (overrides != null)
        {
            foreach (HeroBuildCutsceneOverride rule in overrides)
            {
                if (rule != null && rule.TryResolve(state, out _))
                    return rule.ImageAssetId;
            }
        }

        return "fallback";
    }

    private readonly struct PpTrainingCutsceneCase
    {
        public PpTrainingCutsceneCase(AppearanceType appearance, string hairId, string expectedImageId)
        {
            Appearance = appearance;
            HairId = hairId;
            ExpectedImageId = expectedImageId;
        }

        public AppearanceType Appearance { get; }
        public string HairId { get; }
        public string ExpectedImageId { get; }
    }

    private static void AssertNode<T>(StoryGraph graph) where T : BaseStoryNode
    {
        Assert.That(graph.nodes.OfType<T>().Any(), Is.True, typeof(T).Name + " was not created.");
    }
}
