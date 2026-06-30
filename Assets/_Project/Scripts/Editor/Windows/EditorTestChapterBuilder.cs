#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;

public static class EditorTestChapterBuilder
{
    public const int AllNodeTypeCount = 17;

    const string StoryId = "editor_test_story";
    const string StoryName = "Редакторская тестовая история";
    const string ChapterId = "editor_test_all_nodes";
    const string ChapterName = "Редакторская проверка: все типы узлов";
    const string EpisodeId = "editor_test_all_nodes";
    const string GraphName = "Редакторская проверка - все типы узлов";

    [InitializeOnLoadMethod]
    static void EnsureGeneratedAssetExistsAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode)
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            bool hasStory = AssetDatabase.LoadAssetAtPath<StoryData>(EditorTestChapterLoader.StoryAssetPath) != null;
            bool hasChapter = AssetDatabase.LoadAssetAtPath<ChapterData>(EditorTestChapterLoader.ChapterAssetPath) != null;
            bool hasGraph = AssetDatabase.LoadAssetAtPath<StoryGraph>(EditorTestChapterLoader.GraphAssetPath) != null;
            if (hasStory && hasChapter && hasGraph)
                return;

            try
            {
                EnsureTestStory();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[EditorTestChapter] Автосоздание не удалось: " + exception.Message);
            }
        };
    }

    public static StoryData EnsureTestStory()
    {
        EnsureFolder(EditorTestChapterLoader.RootFolder);
        EnsureFolder(EditorTestChapterLoader.RootFolder + "/Graphs");
        EnsureFolder(EditorTestChapterLoader.RootFolder + "/Chapters");

        if (AssetDatabase.LoadAssetAtPath<StoryGraph>(EditorTestChapterLoader.GraphAssetPath) != null)
            AssetDatabase.DeleteAsset(EditorTestChapterLoader.GraphAssetPath);

        var graph = CreateGraph();
        var chapter = EnsureChapter(graph);
        var story = EnsureStory(chapter);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return story;
    }

    public static void EnsureTestStoryFromCommandLine()
    {
        EnsureTestStory();
    }

    static StoryGraph CreateGraph()
    {
        var graph = ScriptableObject.CreateInstance<StoryGraph>();
        graph.name = GraphName;
        graph.episodeId = EpisodeId;
        AssetDatabase.CreateAsset(graph, EditorTestChapterLoader.GraphAssetPath);

        CharacterData character = ResolveCharacter(EditorTestChapterLoader.GraphAssetPath);
        ClothingItem outfit = ResolveClothing(ClothingType.Outfit, "editor_test_outfit", EditorTestChapterLoader.GraphAssetPath);
        ClothingItem hair = ResolveClothing(ClothingType.Hair, "editor_test_hair", EditorTestChapterLoader.GraphAssetPath);

        var start = AddNode<StartNode>(graph, "editor_test_start", "01 Старт", new Vector2(0f, 0f));
        var scene = AddNode<SceneSetupNode>(graph, "editor_test_scene", "02 Настройка сцены", new Vector2(300f, 0f));
        var dialogue = AddNode<DialogueNode>(graph, "editor_test_dialogue", "03 Диалог", new Vector2(680f, 0f));
        var variable = AddNode<VariableChangeNode>(graph, "editor_test_variable", "04 Изменение переменной", new Vector2(1100f, 0f));
        var condition = AddNode<ConditionNode>(graph, "editor_test_condition", "05 Условие", new Vector2(1420f, 0f));
        var conditionFallback = AddNode<DialogueNode>(graph, "editor_test_condition_false", "05b Условие не выполнено", new Vector2(1740f, 230f));
        var choice = AddNode<ChoiceNode>(graph, "editor_test_choice", "06 Выбор", new Vector2(1740f, 0f));
        var choiceBranch = AddNode<DialogueNode>(graph, "editor_test_choice_branch", "06b Ветка выбора", new Vector2(2140f, 230f));
        var stat = AddNode<StatChangeNode>(graph, "editor_test_stat", "07 Изменение стата", new Vector2(2140f, 0f));
        var premium = AddNode<PremiumNode>(graph, "editor_test_premium", "08 Премиум", new Vector2(2460f, 0f));
        var camera = AddNode<CameraNode>(graph, "editor_test_camera", "09 Камера", new Vector2(2780f, 0f));
        var image = AddNode<ImageNode>(graph, "editor_test_image", "10 Изображение", new Vector2(3100f, 0f));
        var phone = AddNode<PhoneDialogueNode>(graph, "editor_test_phone", "11 Телефонный диалог", new Vector2(3440f, 0f));
        var effect = AddNode<EffectNode>(graph, "editor_test_effect", "12 Эффект", new Vector2(3820f, 0f));
        var appearance = AddNode<AppearanceChoiceNode>(graph, "editor_test_appearance", "13 Выбор внешности", new Vector2(4140f, 0f));
        var wardrobeChoice = AddNode<WardrobeChoiceNode>(graph, "editor_test_wardrobe_choice", "14 Выбор гардероба", new Vector2(4560f, 0f));
        var addClothing = AddNode<AddClothingNode>(graph, "editor_test_add_clothing", "15 Добавление одежды", new Vector2(4920f, 0f));
        var wardrobeCheck = AddNode<WardrobeCheckNode>(graph, "editor_test_wardrobe_check", "16 Проверка гардероба", new Vector2(5260f, 0f));
        var wardrobeMissing = AddNode<DialogueNode>(graph, "editor_test_wardrobe_missing", "16b Одежда не найдена", new Vector2(5600f, 230f));
        var openWardrobe = AddNode<OpenWardrobeNode>(graph, "editor_test_open_wardrobe", "17 Открытие гардероба", new Vector2(5600f, 0f));
        var finalDialogue = AddNode<DialogueNode>(graph, "editor_test_final", "18 Финальный диалог", new Vector2(5940f, 0f));

        ConfigureScene(scene);
        ConfigureDialogue(dialogue, character, "Проверка DialogueNode", "Эта строка проверяет обычный диалог и подстановку имени: {playerName}.");
        ConfigureVariable(variable);
        ConfigureCondition(condition);
        ConfigureDialogue(conditionFallback, character, "Ветка false условия", "Запасная ветка условия подключена для проверки.");
        ConfigureChoice(choice, character);
        ConfigureDialogue(choiceBranch, character, "Дополнительная ветка выбора", "Альтернативная ветка выбора возвращается в основной тестовый маршрут.");
        ConfigureStat(stat);
        ConfigurePremium(premium);
        ConfigureCamera(camera);
        ConfigureImage(image);
        ConfigurePhone(phone);
        ConfigureEffect(effect);
        ConfigureAppearance(appearance);
        ConfigureWardrobeChoice(wardrobeChoice, character, outfit, hair, addClothing);
        ConfigureAddClothing(addClothing, outfit);
        ConfigureWardrobeCheck(wardrobeCheck, outfit);
        ConfigureDialogue(wardrobeMissing, character, "Запасная ветка гардероба", "Подходящий предмет одежды не найден.");
        ConfigureDialogue(finalDialogue, character, "Готово", "Все типы узлов редакторской тестовой главы присутствуют.");

        Connect(start, "exit", scene, "enter");
        Connect(scene, "exit", dialogue, "enter");
        Connect(dialogue, "exit", variable, "enter");
        Connect(variable, "exit", condition, "enter");
        Connect(condition, "trueExit", choice, "enter");
        Connect(condition, "falseExit", conditionFallback, "enter");
        Connect(conditionFallback, "exit", choice, "enter");
        Connect(choice, "choices 0", stat, "enter");
        Connect(choice, "choices 1", choiceBranch, "enter");
        Connect(choiceBranch, "exit", stat, "enter");
        Connect(stat, "exit", premium, "enter");
        Connect(premium, "successNode", camera, "enter");
        Connect(premium, "failNode", camera, "enter");
        Connect(camera, "exit", image, "enter");
        Connect(image, "exit", phone, "enter");
        Connect(phone, "exit", effect, "enter");
        Connect(effect, "exit", appearance, "enter");
        Connect(appearance, "choices 0", wardrobeChoice, "enter");
        Connect(appearance, "choices 1", wardrobeChoice, "enter");
        Connect(wardrobeChoice, "exits 0", addClothing, "enter");
        Connect(wardrobeChoice, "exits 1", addClothing, "enter");
        Connect(addClothing, "exit", wardrobeCheck, "enter");
        Connect(wardrobeCheck, "hasItem", openWardrobe, "enter");
        Connect(wardrobeCheck, "noItem", wardrobeMissing, "enter");
        Connect(wardrobeMissing, "exit", openWardrobe, "enter");
        Connect(openWardrobe, "exit", finalDialogue, "enter");

        MarkGraphDirty(graph);
        return graph;
    }

    static void ConfigureScene(SceneSetupNode node)
    {
        node.sceneLabel = "Редакторская тестовая сцена";
        var data = ScriptableObject.CreateInstance<SceneSetupData>();
        data.name = "EditorTestSceneData";
        data.backgroundId = "editor_test_background";
        data.musicId = "editor_test_music";
        AssetDatabase.AddObjectToAsset(data, EditorTestChapterLoader.GraphAssetPath);
        node.sceneData = data;
        EditorUtility.SetDirty(data);
    }

    static void ConfigureDialogue(DialogueNode node, CharacterData character, string title, string text)
    {
        node.nodeTitle = title;
        node.activeCharacters = BuildActiveCharacters(character);
        node.lines = new List<DialogueLine>
        {
            new DialogueLine
            {
                speakerId = character != null ? SaveDataSanitizer.SanitizeIdentifier(character.name) : "",
                speakerNameHint = character != null ? character.name : "",
                speaker = character,
                emotion = CharacterEmotionType.Happy,
                richText = text
            }
        };
    }

    static void ConfigureVariable(VariableChangeNode node)
    {
        node.variableKey = "editor_test_flag";
        node.deltaValue = 1;
        node.Add = false;
    }

    static void ConfigureCondition(ConditionNode node)
    {
        node.variableKey = "editor_test_flag";
        node.requiredValue = 1;
    }

    static void ConfigureChoice(ChoiceNode node, CharacterData character)
    {
        node.nodeTitle = "Проверка ChoiceNode";
        node.activeCharacters = BuildActiveCharacters(character);
        node.lines = new List<DialogueLine>
        {
            new DialogueLine
            {
                speakerId = character != null ? SaveDataSanitizer.SanitizeIdentifier(character.name) : "",
                speakerNameHint = character != null ? character.name : "",
                speaker = character,
                emotion = CharacterEmotionType.Thinking,
                richText = "Выберите тестовый маршрут."
            }
        };
        node.options = new List<ChoiceOption>
        {
            new ChoiceOption { text = "Основной маршрут со всеми узлами" },
            new ChoiceOption { text = "Альтернативная ветка с возвращением" }
        };
        node.choices = new List<BaseStoryNode> { null, null };
        EnsureDynamicOutput(node, "choices 0");
        EnsureDynamicOutput(node, "choices 1");
    }

    static void ConfigureStat(StatChangeNode node)
    {
        node.statId = "editor_test_stat";
        node.delta = 1;
        node.displayName = "Редакторский тестовый стат";
        node.systemMessage = "Редакторский тестовый стат изменён.";
    }

    static void ConfigurePremium(PremiumNode node)
    {
        node.cost = 1;
    }

    static void ConfigureCamera(CameraNode node)
    {
        node.mode = CameraNode.CameraMode.Offset;
        node.xOffset = 120f;
        node.duration = 0.2f;
    }

    static void ConfigureImage(ImageNode node)
    {
        node.Configure(null, null, null, "Проверка изображения", "Панель ImageNode без внешних медиа.", true);
    }

    static void ConfigurePhone(PhoneDialogueNode node)
    {
        node.contactName = "Тест редактора";
        node.typingDelay = 0.1f;
        node.messages = new List<PhoneMessage>
        {
            new PhoneMessage { senderName = "Контакт", side = PhoneMessageSide.Incoming, text = "Входящее сообщение PhoneDialogueNode." },
            new PhoneMessage { senderName = "{PlayerName}", side = PhoneMessageSide.Outgoing, text = "Исходящий ответ PhoneDialogueNode." }
        };
    }

    static void ConfigureEffect(EffectNode node)
    {
        node.effect = EffectType.Shake;
        node.duration = 0.35f;
        node.intensity = 2f;
    }

    static void ConfigureAppearance(AppearanceChoiceNode node)
    {
        node.promptText = "Проверка AppearanceChoiceNode";
        node.singleExit = false;
        node.options = new List<AppearanceOption>
        {
            new AppearanceOption { label = "По умолчанию", type = AppearanceType.Default },
            new AppearanceOption { label = "Европейская", type = AppearanceType.European }
        };
        node.choices = new List<BaseStoryNode> { null, null };
        EnsureDynamicOutput(node, "choices 0");
        EnsureDynamicOutput(node, "choices 1");
    }

    static void ConfigureWardrobeChoice(
        WardrobeChoiceNode node,
        CharacterData character,
        ClothingItem outfit,
        ClothingItem hair,
        BaseStoryNode exitNode)
    {
        node.characterId = "hero";
        node.character = character;
        node.availableClothes = new List<ClothingItem> { outfit, hair };
        node.exits = new List<BaseStoryNode> { exitNode, exitNode };
        EnsureDynamicOutput(node, "exits 0");
        EnsureDynamicOutput(node, "exits 1");
    }

    static void ConfigureAddClothing(AddClothingNode node, ClothingItem item)
    {
        node.clothing = item;
    }

    static void ConfigureWardrobeCheck(WardrobeCheckNode node, ClothingItem item)
    {
        node.itemId = item != null ? item.id : "editor_test_outfit";
    }

    static List<DialogueCharacterEntry> BuildActiveCharacters(CharacterData character)
    {
        return new List<DialogueCharacterEntry>
        {
            new DialogueCharacterEntry
            {
                character = character,
                emotion = CharacterEmotionType.Happy,
                position = CharacterPosition.Center
            }
        };
    }

    static StoryData EnsureStory(ChapterData chapter)
    {
        var story = AssetDatabase.LoadAssetAtPath<StoryData>(EditorTestChapterLoader.StoryAssetPath);
        if (story == null)
        {
            story = ScriptableObject.CreateInstance<StoryData>();
            AssetDatabase.CreateAsset(story, EditorTestChapterLoader.StoryAssetPath);
        }

        story.Configure(StoryId, StoryName, new[] { chapter });
        EditorUtility.SetDirty(story);
        return story;
    }

    static ChapterData EnsureChapter(StoryGraph graph)
    {
        var chapter = AssetDatabase.LoadAssetAtPath<ChapterData>(EditorTestChapterLoader.ChapterAssetPath);
        if (chapter == null)
        {
            chapter = ScriptableObject.CreateInstance<ChapterData>();
            AssetDatabase.CreateAsset(chapter, EditorTestChapterLoader.ChapterAssetPath);
        }

        chapter.Configure(ChapterId, ChapterName, graph, false, 0);
        EditorUtility.SetDirty(chapter);
        return chapter;
    }

    static T AddNode<T>(StoryGraph graph, string guid, string name, Vector2 position) where T : BaseStoryNode
    {
        var node = graph.AddNode<T>();
        node.guid = guid;
        node.name = name;
        node.position = position;
        node.graph = graph;
        AssetDatabase.AddObjectToAsset(node, EditorTestChapterLoader.GraphAssetPath);
        EditorUtility.SetDirty(node);
        return node;
    }

    static void Connect(BaseStoryNode from, string outputPortName, BaseStoryNode to, string inputPortName)
    {
        NodePort output = from != null ? from.GetOutputPort(outputPortName) : null;
        NodePort input = to != null ? to.GetInputPort(inputPortName) : null;
        if (output != null && input != null && !output.IsConnectedTo(input))
            output.Connect(input);
    }

    static void EnsureDynamicOutput(BaseStoryNode node, string portName)
    {
        if (node != null && !node.HasPort(portName))
            node.AddDynamicOutput(typeof(BaseStoryNode), Node.ConnectionType.Multiple, Node.TypeConstraint.None, portName);
    }

    static CharacterData ResolveCharacter(string graphPath)
    {
        CharacterData existing = FindFirstAsset<CharacterData>("t:CharacterData", "Assets/_MyProject/Data");
        if (existing != null)
            return existing;

        var character = ScriptableObject.CreateInstance<CharacterData>();
        character.name = "Editor_Test_Character";
        character.characterName = "Тестовый персонаж";
        AssetDatabase.AddObjectToAsset(character, graphPath);
        EditorUtility.SetDirty(character);
        return character;
    }

    static ClothingItem ResolveClothing(ClothingType type, string fallbackId, string graphPath)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ClothingItem", new[] { "Assets/_MyProject/Data" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<ClothingItem>(path);
            if (item != null && item.type == type && !string.IsNullOrWhiteSpace(item.id))
                return item;
        }

        var clothing = ScriptableObject.CreateInstance<ClothingItem>();
        clothing.name = fallbackId;
        clothing.id = fallbackId;
        clothing.type = type;
        AssetDatabase.AddObjectToAsset(clothing, graphPath);
        EditorUtility.SetDirty(clothing);
        return clothing;
    }

    static T FindFirstAsset<T>(string filter, string folder) where T : UnityEngine.Object
    {
        string[] folders = AssetDatabase.IsValidFolder(folder)
            ? new[] { folder }
            : null;

        foreach (string guid in AssetDatabase.FindAssets(filter, folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
        }

        return null;
    }

    static void MarkGraphDirty(StoryGraph graph)
    {
        if (graph == null)
            return;

        foreach (var node in graph.nodes)
        {
            if (node != null)
                EditorUtility.SetDirty(node);
        }

        EditorUtility.SetDirty(graph);
    }

    static string EnsureFolder(string path)
    {
        path = path.Replace("\\", "/").TrimEnd('/');
        if (AssetDatabase.IsValidFolder(path))
            return path;

        string[] parts = path.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
            throw new InvalidOperationException("Папка должна находиться внутри Assets: " + path);

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }

        return path;
    }
}
#endif
