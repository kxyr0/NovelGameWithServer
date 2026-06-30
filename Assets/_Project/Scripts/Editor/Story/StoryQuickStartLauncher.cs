using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable]
public sealed class StoryQuickStartPoint
{
    public readonly string title;
    public readonly string nodeGuid;

    public StoryQuickStartPoint(string title, string nodeGuid)
    {
        this.title = title;
        this.nodeGuid = nodeGuid;
    }
}

[Serializable]
public sealed class StoryQuickStartChapter
{
    public readonly string title;
    public readonly string chapterId;
    public readonly StoryQuickStartPoint[] points;

    public StoryQuickStartChapter(string title, string chapterId, params StoryQuickStartPoint[] points)
    {
        this.title = title;
        this.chapterId = chapterId;
        this.points = points ?? Array.Empty<StoryQuickStartPoint>();
    }
}

[Serializable]
public sealed class StoryQuickStartStory
{
    public readonly string title;
    public readonly string storyAssetPath;
    public readonly StoryQuickStartChapter[] chapters;

    public StoryQuickStartStory(string title, string storyAssetPath, params StoryQuickStartChapter[] chapters)
    {
        this.title = title;
        this.storyAssetPath = storyAssetPath;
        this.chapters = chapters ?? Array.Empty<StoryQuickStartChapter>();
    }
}

[InitializeOnLoad]
public static class StoryQuickStartLauncher
{
    public const string ZlsStoryAssetPath = "Assets/_MyProject/Data/Stories/Only_the_heart_sees_clearly/Only_the_heart_sees_clearly_Story.asset";
    public const string PpStoryAssetPath = "Assets/_MyProject/Data/Stories/privychka_pritvoryatsya/StoryJsonGenerated/privychka_pritvoryatsya/privychka_pritvoryatsya_Story.asset";

    const string PendingLaunchKey = "VN.StoryQuickStart.Pending";
    const string PendingStoryPathKey = "VN.StoryQuickStart.StoryPath";
    const string PendingChapterIdKey = "VN.StoryQuickStart.ChapterId";
    const string PendingNodeGuidKey = "VN.StoryQuickStart.NodeGuid";
    const string PendingJumpNodeGuidKey = "VN.StoryQuickStart.PendingJumpNodeGuid";
    const double LaunchTimeoutSeconds = 8.0;

    static readonly StoryQuickStartStory[] Stories =
    {
        new StoryQuickStartStory(
            "Only the Heart Sees Clearly",
            ZlsStoryAssetPath,
            new StoryQuickStartChapter(
                "ZLS_1",
                "zls_1",
                Point("Name and Appearance", "zls1_name_001"),
                Point("Start Story Confirm", "zls1_choice_001"),
                Point("Train First Choice", "zls1_choice_002"),
                Point("Village Arrival", "zls1_scene_005_village_station"),
                Point("Ivan Horse Cutscene", "zls1_image_001"),
                Point("Bab Nyura House", "zls1_scene_009_house_inside"),
                Point("Bedroom Investigation", "zls1_choice_013"),
                Point("Dream Path", "zls1_choice_019"),
                Point("Dream Question", "zls1_choice_020"),
                Point("Final Cutscene", "zls1_image_004")),
            new StoryQuickStartChapter(
                "ZLS_2",
                "zls_2",
                Point("Wardrobe Outfit", "zls2_wardrobe_001_outfit"),
                Point("Wardrobe Hair", "zls2_wardrobe_002_hair"),
                Point("Wardrobe Confirm", "zls2_choice_002_look_confirm"),
                Point("Kitchen Questions", "zls2_choice_questions_start"),
                Point("Market", "zls2_scene_005_market"),
                Point("Jewelry Choice", "zls2_choice_004_jewelry"),
                Point("Phone Choice", "zls2_choice_005_phone"),
                Point("River Path", "zls2_scene_010b_river_path"),
                Point("River Cutscene", "zls2_image_001_oksana"),
                Point("After River Cutscene", "zls2_scene_011b_river_after_oksana"),
                Point("Ivan Route", "zls2_scene_013_ivan_romantic"),
                Point("Final", "zls2_dialogue_038_final"))),
        new StoryQuickStartStory(
            "Privychka Pritvoryatsya",
            PpStoryAssetPath,
            new StoryQuickStartChapter(
                "PP_1",
                "pp_1",
                Point("Wardrobe", "pp_open_wardrobe"),
                Point("After Wardrobe Confirm", "pp_choice_after_wardrobe"),
                Point("Character Axis", "pp_choice_character_axis_001"),
                Point("Vlad First Choice", "pp_choice_confront_vlad_001"),
                Point("Coffee Choice", "pp_choice_take_coffee_001"),
                Point("Cabinet", "pp_scene_cabinet_001"),
                Point("Paperwork Reply", "pp_choice_paperwork_reply_001"),
                Point("Vlad Cutscene", "pp_image_vlad_cutscene_001"),
                Point("Gabriel Cutscene", "pp_image_gabriel_cutscene_001"),
                Point("Remi Cafeteria Choice", "pp_choice_remi_romance_axis_001"),
                Point("Doubt Axis", "pp_choice_doubt_axis_001"),
                Point("Final", "pp_dialogue_final_001")),
            new StoryQuickStartChapter(
                "PP_2",
                "pp_2",
                Point("Entrance", "pp2_scene_entrance_001"),
                Point("Wardrobe Outfit", "pp2_choice_outfit_001"),
                Point("Wardrobe Hair", "pp2_choice_hair_001"),
                Point("After Wardrobe", "pp2_dialogue_after_wardrobe_001"),
                Point("Case Motivation", "pp2_choice_case_motivation_001"),
                Point("Principles vs Feelings", "pp2_condition_principles_vs_feelings_001"),
                Point("Will Reply", "pp2_choice_will_reply_001"),
                Point("Cafeteria", "pp2_scene_cafeteria_001"),
                Point("Matchmaker Choice", "pp2_choice_matchmaker_001"),
                Point("Cabinet Evening", "pp2_scene_cabinet_evening_001"),
                Point("Gym", "pp2_scene_gym_001"),
                Point("Escape Choice", "pp2_choice_escape_attempt_001"),
                Point("Training Continue", "pp2_choice_vlad_training_continue_001"),
                Point("Training Cutscene", "pp2_image_vlad_training_cutscene_001"),
                Point("Final", "pp2_dialogue_final_001")))
    };

    static double _launchDeadline;
    static double _jumpDeadline;
    static bool _launching;

    public static IReadOnlyList<StoryQuickStartStory> AllStories => Stories;

    static StoryQuickStartLauncher()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update -= TryConsumePendingLaunch;
        EditorApplication.update += TryConsumePendingLaunch;
        EditorApplication.update -= TryConsumePendingNodeJump;
        EditorApplication.update += TryConsumePendingNodeJump;
    }

    static StoryQuickStartPoint Point(string title, string nodeGuid)
    {
        return new StoryQuickStartPoint(title, nodeGuid);
    }

    [MenuItem("VN/Story Preview/Open Story Preview", priority = 0)]
    public static void OpenQuickStartWindow()
    {
        StoryQuickStartWindow.Open();
    }

    public static void StartZls1() => RequestLaunch(ZlsStoryAssetPath, "zls_1");

    public static void StartZls2() => RequestLaunch(ZlsStoryAssetPath, "zls_2");

    public static void StartPp1() => RequestLaunch(PpStoryAssetPath, "pp_1");

    public static void StartPp2() => RequestLaunch(PpStoryAssetPath, "pp_2");

    public static void StartZls1Menu() => StartZls1();

    public static void StartZls1AtName() => StartZls1AtNode("zls1_name_001");

    public static void StartZls1AtConfirm() => StartZls1AtNode("zls1_choice_001");

    public static void StartZls1AtTrainChoice() => StartZls1AtNode("zls1_choice_002");

    public static void StartZls1AtVillage() => StartZls1AtNode("zls1_scene_005_village_station");

    public static void StartZls1AtIvanHorse() => StartZls1AtNode("zls1_image_001");

    public static void StartZls1AtBedroomInvestigation() => StartZls1AtNode("zls1_choice_013");

    public static void StartZls1AtDreamPath() => StartZls1AtNode("zls1_choice_019");

    public static void StartZls1AtFinalCutscene() => StartZls1AtNode("zls1_image_004");

    public static void StartZls2Menu() => StartZls2();

    public static void StartZls2AtWardrobeConfirm() => StartZls2AtNode("zls2_choice_002_look_confirm");

    public static void StartZls2AtMarketQuestions() => StartZls2AtNode("zls2_choice_questions_start");

    public static void StartZls2AtMarket() => StartZls2AtNode("zls2_scene_005_market");

    public static void StartZls2AtRiverPath() => StartZls2AtNode("zls2_scene_010b_river_path");

    public static void StartZls2AtRiverCutscene() => StartZls2AtNode("zls2_image_001_oksana");

    public static void StartZls2AfterRiverCutscene() => StartZls2AtNode("zls2_scene_011b_river_after_oksana");

    public static void StartZls2AtIvanRoute() => StartZls2AtNode("zls2_scene_013_ivan_romantic");

    public static void StartPp1Menu() => StartPp1();

    public static void StartPp1AtWardrobe() => StartPp1AtNode("pp_open_wardrobe");

    public static void StartPp1AtAxis() => StartPp1AtNode("pp_choice_character_axis_001");

    public static void StartPp1AtCoffee() => StartPp1AtNode("pp_choice_take_coffee_001");

    public static void StartPp1AtVladCutscene() => StartPp1AtNode("pp_image_vlad_cutscene_001");

    public static void StartPp1AtGabrielCutscene() => StartPp1AtNode("pp_image_gabriel_cutscene_001");

    public static void StartPp1AtCafeteria() => StartPp1AtNode("pp_choice_remi_romance_axis_001");

    public static void StartPp1AtFinal() => StartPp1AtNode("pp_dialogue_final_001");

    public static void StartPp2Menu() => StartPp2();

    public static void StartPp2AtWardrobe() => StartPp2AtNode("pp2_choice_outfit_001");

    public static void StartPp2AtCaseMotivation() => StartPp2AtNode("pp2_choice_case_motivation_001");

    public static void StartPp2AtWillReply() => StartPp2AtNode("pp2_choice_will_reply_001");

    public static void StartPp2AtCafeteria() => StartPp2AtNode("pp2_scene_cafeteria_001");

    public static void StartPp2AtGym() => StartPp2AtNode("pp2_scene_gym_001");

    public static void StartPp2AtTrainingCutscene() => StartPp2AtNode("pp2_image_vlad_training_cutscene_001");

    public static void StartPp2AtFinal() => StartPp2AtNode("pp2_dialogue_final_001");

    public static void StartZls1AtNode(string nodeGuid) => RequestLaunch(ZlsStoryAssetPath, "zls_1", nodeGuid);
    public static void StartZls2AtNode(string nodeGuid) => RequestLaunch(ZlsStoryAssetPath, "zls_2", nodeGuid);
    public static void StartPp1AtNode(string nodeGuid) => RequestLaunch(PpStoryAssetPath, "pp_1", nodeGuid);
    public static void StartPp2AtNode(string nodeGuid) => RequestLaunch(PpStoryAssetPath, "pp_2", nodeGuid);

    public static void RequestLaunch(string storyAssetPath, string chapterId, string nodeGuid = "")
    {
        if (EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Story Preview", "Unity is compiling scripts. Try again after compilation finishes.", "OK");
            return;
        }

        EditorPrefs.SetBool(PendingLaunchKey, true);
        EditorPrefs.SetString(PendingStoryPathKey, storyAssetPath);
        EditorPrefs.SetString(PendingChapterIdKey, chapterId);
        EditorPrefs.SetString(PendingNodeGuidKey, nodeGuid ?? "");
        _launchDeadline = EditorApplication.timeSinceStartup + LaunchTimeoutSeconds;

        if (!EditorApplication.isPlaying)
        {
            EditorApplication.EnterPlaymode();
            return;
        }

        TryConsumePendingLaunch();
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && HasPendingLaunch())
            _launchDeadline = EditorApplication.timeSinceStartup + LaunchTimeoutSeconds;
    }

    static void TryConsumePendingLaunch()
    {
        if (_launching || !EditorApplication.isPlaying || !HasPendingLaunch())
            return;

        if (_launchDeadline <= 0.0)
            _launchDeadline = EditorApplication.timeSinceStartup + LaunchTimeoutSeconds;

        if (EditorApplication.timeSinceStartup > _launchDeadline)
        {
            Debug.LogError("[Story Preview] Timed out waiting for StoryManager/GameState in Play Mode.");
            ClearPendingLaunch();
            return;
        }

        StoryManager storyManager = FindSceneObject<StoryManager>();
        if (storyManager == null || GameState.Instance == null)
            return;

        _launching = true;
        try
        {
            string storyPath = EditorPrefs.GetString(PendingStoryPathKey, ZlsStoryAssetPath);
            string chapterId = EditorPrefs.GetString(PendingChapterIdKey, "zls_2");
            StoryData story = AssetDatabase.LoadAssetAtPath<StoryData>(storyPath);
            if (story == null)
            {
                Debug.LogError("[Story Preview] StoryData was not found: " + storyPath);
                ClearPendingLaunch();
                return;
            }

            MenuController menuController = FindSceneObject<MenuController>();
            if (menuController != null)
            {
                menuController.OpenStoryScreen();
                menuController.MainMenuMusicPlayer?.StopMusic();
            }

            if (!storyManager.SelectStory(story))
            {
                ClearPendingLaunch();
                return;
            }

            storyManager.CloseEndPanel();
            string nodeGuid = EditorPrefs.GetString(PendingNodeGuidKey, "");
            bool started = storyManager.StartStoryFromChapterIdForEditorTest(chapterId);
            if (started)
                Debug.Log("[Story Preview] Started chapter '" + chapterId + "' for editor testing.");

            if (started && !string.IsNullOrWhiteSpace(nodeGuid))
            {
                EditorPrefs.SetString(PendingJumpNodeGuidKey, nodeGuid);
                _jumpDeadline = EditorApplication.timeSinceStartup + LaunchTimeoutSeconds;
            }

            ClearPendingLaunch();
        }
        finally
        {
            _launching = false;
        }
    }

    static bool HasPendingLaunch()
    {
        return EditorPrefs.GetBool(PendingLaunchKey, false);
    }

    static void ClearPendingLaunch()
    {
        EditorPrefs.DeleteKey(PendingLaunchKey);
        EditorPrefs.DeleteKey(PendingStoryPathKey);
        EditorPrefs.DeleteKey(PendingChapterIdKey);
        EditorPrefs.DeleteKey(PendingNodeGuidKey);
    }

    static void TryConsumePendingNodeJump()
    {
        if (!EditorApplication.isPlaying)
            return;

        string nodeGuid = EditorPrefs.GetString(PendingJumpNodeGuidKey, "");
        if (string.IsNullOrWhiteSpace(nodeGuid))
            return;

        if (_jumpDeadline <= 0.0)
            _jumpDeadline = EditorApplication.timeSinceStartup + LaunchTimeoutSeconds;

        if (EditorApplication.timeSinceStartup > _jumpDeadline)
        {
            Debug.LogError("[Story Preview] Timed out waiting for node jump: " + nodeGuid);
            ClearPendingNodeJump();
            return;
        }

        StoryManager storyManager = FindSceneObject<StoryManager>();
        if (storyManager == null || storyManager.storyGraph == null || GameState.Instance?.currentNode == null)
            return;

        BaseStoryNode node = FindNodeByGuid(storyManager.storyGraph, nodeGuid);
        if (node == null)
        {
            Debug.LogError("[Story Preview] Node was not found: " + nodeGuid);
            ClearPendingNodeJump();
            return;
        }

        storyManager.ProcessNode(node, false, false);
        Debug.Log("[Story Preview] Jumped to node '" + nodeGuid + "'.");
        ClearPendingNodeJump();
    }

    static BaseStoryNode FindNodeByGuid(StoryGraph graph, string nodeGuid)
    {
        if (graph == null || graph.nodes == null || string.IsNullOrWhiteSpace(nodeGuid))
            return null;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] is BaseStoryNode node && node.guid == nodeGuid)
                return node;
        }

        return null;
    }

    static void ClearPendingNodeJump()
    {
        EditorPrefs.DeleteKey(PendingJumpNodeGuidKey);
        _jumpDeadline = 0.0;
    }

    static T FindSceneObject<T>() where T : UnityEngine.Object
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < objects.Length; i++)
        {
            T item = objects[i];
            if (item == null)
                continue;

            GameObject gameObject = null;
            if (item is Component component)
                gameObject = component.gameObject;
            else if (item is GameObject go)
                gameObject = go;

            if (gameObject == null || !gameObject.scene.IsValid())
                continue;

            if (EditorUtility.IsPersistent(item))
                continue;

            return item;
        }

        return null;
    }
}

public sealed class StoryQuickStartWindow : EditorWindow
{
    Vector2 _scroll;

    public static void Open()
    {
        var window = GetWindow<StoryQuickStartWindow>("Story Preview");
        window.minSize = new Vector2(420f, 360f);
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Story Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Preview registered stories from the beginning or jump to important story moments. This is the single editor entry point for story preview.", MessageType.Info);
        DrawPinnedStoryButtons();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (StoryQuickStartStory story in StoryQuickStartLauncher.AllStories)
            DrawStory(story);

        DrawUnregisteredStories();
        EditorGUILayout.EndScrollView();
    }

    static void DrawPinnedStoryButtons()
    {
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUILayout.LabelField("Stories", GUILayout.Width(48f));
            foreach (StoryQuickStartStory story in StoryQuickStartLauncher.AllStories)
            {
                if (story == null || story.chapters == null)
                    continue;

                foreach (StoryQuickStartChapter chapter in story.chapters)
                {
                    if (chapter == null)
                        continue;

                    if (GUILayout.Button(chapter.title, EditorStyles.toolbarButton, GUILayout.MinWidth(64f)))
                        StoryQuickStartLauncher.RequestLaunch(story.storyAssetPath, chapter.chapterId);
                }
            }
        }
    }

    static void DrawStory(StoryQuickStartStory story)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(story.title, EditorStyles.boldLabel);

        StoryData storyData = AssetDatabase.LoadAssetAtPath<StoryData>(story.storyAssetPath);
        using (new EditorGUI.DisabledScope(storyData == null))
        {
            foreach (StoryQuickStartChapter chapter in story.chapters)
                DrawChapter(story.storyAssetPath, chapter);
        }

        if (storyData == null)
            EditorGUILayout.HelpBox("Missing StoryData: " + story.storyAssetPath, MessageType.Warning);
    }

    static void DrawChapter(string storyAssetPath, StoryQuickStartChapter chapter)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(chapter.title, EditorStyles.boldLabel);
                if (GUILayout.Button("Start", GUILayout.Width(96f)))
                    StoryQuickStartLauncher.RequestLaunch(storyAssetPath, chapter.chapterId);
            }

            int columns = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 54f) / 180f));
            int column = 0;
            EditorGUILayout.BeginHorizontal();
            foreach (StoryQuickStartPoint point in chapter.points)
            {
                if (GUILayout.Button(point.title, GUILayout.Height(24f)))
                    StoryQuickStartLauncher.RequestLaunch(storyAssetPath, chapter.chapterId, point.nodeGuid);

                column++;
                if (column >= columns)
                {
                    column = 0;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    static void DrawUnregisteredStories()
    {
        string[] guids = AssetDatabase.FindAssets("t:StoryData", new[] { "Assets/_MyProject/Data/Stories" });
        var registeredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (StoryQuickStartStory story in StoryQuickStartLauncher.AllStories)
            registeredPaths.Add(story.storyAssetPath);

        bool drewHeader = false;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (registeredPaths.Contains(path) || path.Contains("/__EditorTest/"))
                continue;

            StoryData story = AssetDatabase.LoadAssetAtPath<StoryData>(path);
            if (story == null || story.chapters == null || story.chapters.Count == 0)
                continue;

            if (!drewHeader)
            {
                drewHeader = true;
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Other StoryData Assets", EditorStyles.boldLabel);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(story.storyName) ? story.name : story.storyName, EditorStyles.boldLabel);
                foreach (ChapterData chapter in story.chapters)
                {
                    if (chapter == null)
                        continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(chapter.chapterName) ? chapter.chapterId : chapter.chapterName);
                        if (GUILayout.Button("Start", GUILayout.Width(96f)))
                            StoryQuickStartLauncher.RequestLaunch(path, chapter.chapterId);
                    }
                }
            }
        }
    }
}

public static class ZlsQuickStartLauncher
{
    public static void StartZls2() => StoryQuickStartLauncher.StartZls2();
    public static void StartZls2AtWardrobeConfirm() => StoryQuickStartLauncher.StartZls2AtWardrobeConfirm();
    public static void StartZls2AtMarketQuestions() => StoryQuickStartLauncher.StartZls2AtMarketQuestions();
    public static void StartZls2AtRiverPath() => StoryQuickStartLauncher.StartZls2AtRiverPath();
    public static void StartZls2AtRiverCutscene() => StoryQuickStartLauncher.StartZls2AtRiverCutscene();
    public static void StartZls2AfterRiverCutscene() => StoryQuickStartLauncher.StartZls2AfterRiverCutscene();
    public static void StartZls2AtIvanRoute() => StoryQuickStartLauncher.StartZls2AtIvanRoute();
    public static void StartZls2AtNode(string nodeGuid) => StoryQuickStartLauncher.StartZls2AtNode(nodeGuid);
    public static void RequestLaunch(string storyAssetPath, string chapterId, string nodeGuid = "") =>
        StoryQuickStartLauncher.RequestLaunch(storyAssetPath, chapterId, nodeGuid);
}

[InitializeOnLoad]
public static class StoryQuickStartToolbarButton
{
    const string ButtonRootName = "StoryPreviewToolbarButtonRoot";
    const string LegacyButtonRootName = "StoryQuickStartToolbarButtonRoot";
    const string ButtonText = "Story Preview";

    static readonly Type ToolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
    static ScriptableObject _toolbar;

    static StoryQuickStartToolbarButton()
    {
        EditorApplication.update -= TryAttachButton;
        EditorApplication.update += TryAttachButton;
    }

    static void TryAttachButton()
    {
        if (ToolbarType == null)
            return;

        if (_toolbar == null)
        {
            UnityEngine.Object[] toolbars = Resources.FindObjectsOfTypeAll(ToolbarType);
            if (toolbars == null || toolbars.Length == 0)
                return;

            _toolbar = toolbars[0] as ScriptableObject;
        }

        VisualElement root = GetToolbarRoot();
        VisualElement zone = root?.Q("ToolbarZoneRightAlign");
        if (zone == null)
            return;

        zone.Q(LegacyButtonRootName)?.RemoveFromHierarchy();

        if (zone.Q(ButtonRootName) != null)
            return;

        var wrapper = new VisualElement { name = ButtonRootName };
        wrapper.style.flexDirection = FlexDirection.Row;
        wrapper.style.alignItems = Align.Center;
        wrapper.style.marginLeft = 6;
        wrapper.style.marginRight = 4;

        var button = new Button(StoryQuickStartWindow.Open)
        {
            text = ButtonText,
            tooltip = "Open story preview by story and chapter."
        };
        button.style.height = 22;
        button.style.minWidth = 84;
        button.style.paddingLeft = 8;
        button.style.paddingRight = 8;

        wrapper.Add(button);
        zone.Add(wrapper);
    }

    static VisualElement GetToolbarRoot()
    {
        if (_toolbar == null || ToolbarType == null)
            return null;

        var rootField = ToolbarType.GetField("m_Root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return rootField?.GetValue(_toolbar) as VisualElement;
    }
}
