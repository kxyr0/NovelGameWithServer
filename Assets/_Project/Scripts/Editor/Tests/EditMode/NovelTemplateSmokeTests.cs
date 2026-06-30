using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;
using XNode;

public class NovelTemplateSmokeTests
{
    private static readonly string[] DataRoots =
    {
        "Assets/_MyProject/Data",
        "Assets/NovelTemplate/Data"
    };

    private static readonly string[] GameScenePaths =
    {
        "Assets/_MyProject/Scenes/Game.unity",
        "Assets/Scenes/Game.unity"
    };

    private const string NetworkConfigPath = "Assets/Resources/NovelTemplate/network-runtime-config.json";

    [Test]
    public void EncapsulatedTypes_DoNotExposePublicMutableFields()
    {
        var guardedTypes = new[]
        {
            typeof(GameData),
            typeof(GameCatalog),
            typeof(MenuController),
            typeof(StoryScreenNavigator),
            typeof(StoryScreenNavigator.ScreenBinding),
            typeof(StoryEndScreenController),
            typeof(StoryEndScreenController.ButtonBinding),
            typeof(StoryEndScreenController.TextBinding),
            typeof(UIScreenTransitionAnimator),
            typeof(UIScreenVisibilityRule),
            typeof(GameObjectToggle),
            typeof(GameObjectToggle.ToggleEvent),
            typeof(StoryData),
            typeof(SeasonData),
            typeof(ChapterData),
            typeof(CutsceneNode),
            typeof(GameButtonView),
            typeof(ImageNode),
            typeof(ImageOverlayUI),
            typeof(BackgroundViewManager),
            typeof(AnimatedGifPlayer),
            typeof(DecodedAnimatedGif)
        };

        foreach (var type in guardedTypes)
        {
            var publicFields = type
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(field => !field.IsLiteral)
                .Select(field => field.Name)
                .ToArray();

            Assert.That(
                publicFields,
                Is.Empty,
                $"{type.Name} exposes public mutable fields: {string.Join(", ", publicFields)}. Use [SerializeField] private fields plus explicit properties/methods.");
        }
    }

    [Test]
    public void EncapsulatedTypes_DoNotExposeMutableCollectionProperties()
    {
        var guardedTypes = new[]
        {
            typeof(GameCatalog),
            typeof(MenuController),
            typeof(StoryScreenNavigator),
            typeof(StoryScreenNavigator.ScreenBinding),
            typeof(UIScreenVisibilityRule),
            typeof(GameObjectToggle),
            typeof(StoryData),
            typeof(SeasonData)
        };

        foreach (var type in guardedTypes)
        {
            var mutableCollectionProperties = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => IsMutableCollectionType(property.PropertyType))
                .Select(property => property.Name)
                .ToArray();

            Assert.That(
                mutableCollectionProperties,
                Is.Empty,
                $"{type.Name} exposes mutable collection properties: {string.Join(", ", mutableCollectionProperties)}. Use IReadOnlyList plus explicit methods.");
        }
    }

    [Test]
    public void StoryCatalog_HasStableIdsAndNames()
    {
        var storyIds = new HashSet<string>();
        foreach (var story in LoadAssets<StoryData>("t:StoryData"))
        {
            Assert.That(story.storyId, Is.Not.Empty, $"{story.name} has empty storyId");
            Assert.That(story.storyName, Is.Not.Empty, $"{story.name} has empty storyName");
            Assert.That(storyIds.Add(story.storyId), Is.True, $"Duplicate storyId: {story.storyId}");
            Assert.That(story.chapters, Is.Not.Empty, $"{story.name} has no chapters");

            var chapterIds = new HashSet<string>();
            foreach (var chapter in story.chapters)
            {
                Assert.That(chapter, Is.Not.Null, $"{story.name} contains null chapter");
                Assert.That(chapter.chapterId, Is.Not.Empty, $"{chapter.name} has empty chapterId");
                Assert.That(chapter.chapterName, Is.Not.Empty, $"{chapter.name} has empty chapterName");
                Assert.That(chapterIds.Add(chapter.chapterId), Is.True, $"Duplicate chapterId: {chapter.chapterId}");
                Assert.That(chapter.graph, Is.Not.Null, $"{chapter.name} has no graph");
                Assert.That(chapter.graph.episodeId, Is.EqualTo(chapter.chapterId), $"{chapter.name} graph episodeId must match chapterId");
            }
        }
    }

    [Test]
    public void StoryGraphs_HaveUniqueNodeGuidsAndStartNodes()
    {
        foreach (var graph in LoadAssets<StoryGraph>("t:StoryGraph"))
        {
            Assert.That(graph.episodeId, Is.Not.Empty, $"{graph.name} has empty episodeId");
            Assert.That(graph.nodes.OfType<StartNode>().Any(), Is.True, $"{graph.name} has no StartNode");

            var nodeGuids = new HashSet<string>();
            foreach (var node in graph.nodes.OfType<BaseStoryNode>())
            {
                Assert.That(node.guid, Is.Not.Empty, $"{graph.name}/{node.name} has empty node guid");
                Assert.That(nodeGuids.Add(node.guid), Is.True, $"{graph.name} duplicate node guid: {node.guid}");
            }
        }
    }

    [Test]
    public void DialoguePaging_UsesWholeSentencesInsteadOfCharacterCuts()
    {
        var pages = BuildDialogueSentencePagesForTest(
            "First sentence is complete. Second sentence moves.",
            32);

        Assert.That(pages, Is.EqualTo(new[]
        {
            "First sentence is complete.",
            "Second sentence moves."
        }));
    }

    [Test]
    public void DialoguePaging_KeepsOversizedSentenceTogether()
    {
        var pages = BuildDialogueSentencePagesForTest(
            "This sentence is deliberately longer than the page limit. Short.",
            20);

        Assert.That(pages, Is.EqualTo(new[]
        {
            "This sentence is deliberately longer than the page limit.",
            "Short."
        }));
    }

    [Test]
    public void ProgressRestore_SelectsChapterBySnapshotIds()
    {
        var firstGraph = PlayModeStoryFactory.CreateDialogueGraph("restore_ep01", "First", "restore_intro", "Intro");
        var secondGraph = PlayModeStoryFactory.CreateDialogueGraph("restore_ep02", "Second", "restore_target", "Target");
        var firstChapter = PlayModeStoryFactory.CreateChapter("restore_chapter_01", "Chapter 1", firstGraph);
        var chapter = PlayModeStoryFactory.CreateChapter("restore_chapter_02", "Chapter 2", secondGraph);
        var story = PlayModeStoryFactory.CreateStory("restore_story", "restore_story_s01", firstChapter, chapter);

        var targetNode = secondGraph.nodes.OfType<BaseStoryNode>().FirstOrDefault(n => !string.IsNullOrEmpty(n.guid));
        Assert.That(targetNode, Is.Not.Null, $"{secondGraph.name} has no restorable node");

        var go = new GameObject("StoryManagerRestoreSmoke");
        try
        {
            var manager = go.AddComponent<StoryManager>();
            Assert.That(manager.SelectStory(story), Is.True);

            var snapshot = new SaveData
            {
                storyId = story.storyId,
                seasonId = story.seasons != null && story.seasons.Count > 0 ? story.seasons[0].seasonId : "",
                chapterId = chapter.chapterId,
                episodeId = secondGraph.episodeId,
                currentNodeGuid = targetNode.guid,
                currentSeasonIndex = 0,
                currentChapterIndex = 1
            };

            var method = typeof(StoryManager).GetMethod(
                "TrySelectChapterForSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "TrySelectChapterForSnapshot not found");

            object[] args = { snapshot, null };
            var ok = (bool)method.Invoke(manager, args);

            Assert.That(ok, Is.True, "Snapshot did not resolve to a chapter graph");
            Assert.That(args[1], Is.SameAs(secondGraph));
            Assert.That(manager.CurrentChapterIndex, Is.EqualTo(1));
            Assert.That(manager.CurrentEpisodeId, Is.EqualTo(secondGraph.episodeId));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(story);
            Object.DestroyImmediate(firstChapter);
            Object.DestroyImmediate(chapter);
            Object.DestroyImmediate(firstGraph);
            Object.DestroyImmediate(secondGraph);
        }
    }

    [Test]
    public void GameScene_HasRequiredSceneReferences()
    {
        string scenePath = GameScenePaths.FirstOrDefault(File.Exists);
        Assert.That(scenePath, Is.Not.Null, "Game scene was not found in expected paths: " + string.Join(", ", GameScenePaths));

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var dialogue = Object.FindObjectOfType<DialogueUIManager>(true);
        Assert.That(dialogue, Is.Not.Null);
        Assert.That(dialogue.nameText, Is.Not.Null);
        Assert.That(dialogue.dialogueText, Is.Not.Null);
        Assert.That(dialogue.choiceButtonPrefab, Is.Not.Null);
        Assert.That(dialogue.choiceContainer, Is.Not.Null);
        Assert.That(dialogue.wardrobePanel, Is.Not.Null);
        Assert.That(dialogue.wardrobePanel.GetComponentInChildren<WardrobeController>(true), Is.Not.Null);

        var menu = Object.FindObjectOfType<MenuController>(true);
        Assert.That(menu, Is.Not.Null);
        Assert.That(menu.GameCatalog, Is.Not.Null);
        Assert.That(menu.Games, Is.Not.Empty);
        Assert.That(menu.GameButtonPrefab, Is.Not.Null);
        Assert.That(menu.GamesParent, Is.Not.Null);
        Assert.That(menu.StoryManager, Is.Not.Null);

        var story = Object.FindObjectOfType<StoryManager>(true);
        Assert.That(story, Is.Not.Null);
        Assert.That(story.musicSource, Is.Not.Null);
        Assert.That(story.sfxSource, Is.Not.Null);
        Assert.That(story.characterView, Is.Not.Null);
        Assert.That(story.backgroundView, Is.Not.Null);
        Assert.That(story.dialogueUI, Is.Not.Null);
        Assert.That(story.endStoryPanel, Is.Not.Null);
        Assert.That(story.storyData, Is.Null, "Game scene should wait for explicit story selection");
    }

    [Test]
    public void EpisodeGraphResponses_ParseLiveAndLegacyShapes()
    {
        var live = NetworkManager.ParseEpisodeGraphResponse(
            "{\"episodeId\":\"ep_s1e1\",\"contentVersion\":\"1.0.0\",\"graph\":{\"nodes\":[]}}",
            "fallback_episode");
        Assert.That(live.episodeId, Is.EqualTo("ep_s1e1"));
        Assert.That(live.contentVersion, Is.EqualTo("1.0.0"));
        Assert.That(live.graphJson, Is.EqualTo("{\"nodes\":[]}"));

        var legacy = NetworkManager.ParseEpisodeGraphResponse(
            "{\"version\":\"1.0.1\",\"graphJson\":\"{\\\"scenes\\\":[]}\"}",
            "fallback_episode");
        Assert.That(legacy.episodeId, Is.EqualTo("fallback_episode"));
        Assert.That(legacy.contentVersion, Is.EqualTo("1.0.1"));
        Assert.That(legacy.graphJson, Is.EqualTo("{\"scenes\":[]}"));
    }

    [Test]
    public void StoryJson_CutsceneBuildsFullscreenDialogueNode()
    {
        StoryGraph graph = null;
        try
        {
            string json = "{"
                + "\"version\":1,"
                + "\"episodeId\":\"cutscene_ep\","
                + "\"nodes\":["
                + "{\"id\":\"start\",\"type\":\"start\",\"next\":\"intro_cg\"},"
                + "{\"id\":\"intro_cg\",\"type\":\"cutscene\",\"title\":\"Intro CG\",\"textDelay\":1.25,"
                + "\"lines\":[{\"text\":\"The forest held its breath.\"}]}"
                + "]}";

            bool ok = StoryJsonConverter.TryBuildGraph(json, "cutscene_ep", out graph, out string reason);

            Assert.That(ok, Is.True, reason);
            var cutscene = graph.nodes.OfType<CutsceneNode>().SingleOrDefault();
            Assert.That(cutscene, Is.Not.Null);
            Assert.That(cutscene.nodeTitle, Is.EqualTo("Intro CG"));
            Assert.That(cutscene.TextDelay, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(cutscene.HideCharacters, Is.True);
            Assert.That(cutscene.lines, Has.Count.EqualTo(1));
            Assert.That(cutscene.lines[0].richText, Is.EqualTo("The forest held its breath."));
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void NetworkJson_ProgressAndBookmarkRequestsIncludeSnapshots()
    {
        var snapshot = new SaveData
        {
            storyId = "story_api",
            seasonId = "season_1",
            chapterId = "ep_api_01",
            episodeId = "ep_api_01",
            currentNodeGuid = "node_api_01",
            currentDialogueLineIndex = 2,
            savedAtIso = "2026-04-23T00:00:00.0000000Z"
        };

        var progress = CreateRuntimeDto("SaveProgressRequest");
        SetDtoField(progress, "storyId", snapshot.storyId);
        SetDtoField(progress, "currentEpisodeId", snapshot.episodeId);
        SetDtoField(progress, "currentNodeGuid", snapshot.currentNodeGuid);
        SetDtoField(progress, "snapshot", snapshot);
        SetDtoField(progress, "stats", new Dictionary<string, int> { { "town", 2 } });
        SetDtoField(progress, "flags", new Dictionary<string, bool> { { "met_lena", true } });
        SetDtoField(progress, "unlockedEpisodes", new List<string> { "ep_api_01" });

        string progressJson = NetworkJson.ToJson(progress);
        var progressSnapshot = NetworkJson.GetSaveData(progressJson, "snapshot");

        Assert.That(NetworkJson.GetString(progressJson, "storyId"), Is.EqualTo("story_api"));
        Assert.That(progressSnapshot, Is.Not.Null);
        Assert.That(progressSnapshot.currentNodeGuid, Is.EqualTo("node_api_01"));
        Assert.That(progressSnapshot.currentDialogueLineIndex, Is.EqualTo(2));
        Assert.That(NetworkJson.GetIntDictionary(progressJson, "stats")["town"], Is.EqualTo(2));
        Assert.That(NetworkJson.GetBoolDictionary(progressJson, "flags")["met_lena"], Is.True);
        Assert.That(NetworkJson.GetStringList(progressJson, "unlockedEpisodes"), Does.Contain("ep_api_01"));

        var bookmark = CreateRuntimeDto("BookmarkRequest");
        SetDtoField(bookmark, "nodeGuid", snapshot.currentNodeGuid);
        SetDtoField(bookmark, "episodeId", snapshot.episodeId);
        SetDtoField(bookmark, "storyId", snapshot.storyId);
        SetDtoField(bookmark, "snapshot", snapshot);
        SetDtoField(bookmark, "label", "api bookmark");

        string bookmarkJson = NetworkJson.ToJson(bookmark);
        var bookmarkSnapshot = NetworkJson.GetSaveData(bookmarkJson, "snapshot");

        Assert.That(NetworkJson.GetString(bookmarkJson, "storyId"), Is.EqualTo("story_api"));
        Assert.That(bookmarkSnapshot, Is.Not.Null);
        Assert.That(bookmarkSnapshot.currentDialogueLineIndex, Is.EqualTo(2));
    }

    [Test]
    public void NetworkJson_RestoreRequestUsesDocumentedTokenContract()
    {
        var restore = CreateRuntimeDto("RestoreAuthRequest");
        SetDtoField(restore, "deviceId", "device-doc");
        SetDtoField(restore, "refreshToken", "refresh-doc-token");

        string json = NetworkJson.ToJson(restore);

        Assert.That(NetworkJson.GetString(json, "deviceId"), Is.EqualTo("device-doc"));
        Assert.That(NetworkJson.GetString(json, "refreshToken"), Is.EqualTo("refresh-doc-token"));
        Assert.That(NetworkJson.GetRawValue(json, "token"), Is.Null);
        Assert.That(NetworkJson.GetRawValue(json, "authToken"), Is.Null);
    }

    [Test]
    public void NetworkManager_RestoreUnauthorizedProbesExistingTokenBeforeReset()
    {
        var go = new GameObject("NetworkManagerRestoreFallbackSmoke");
        NetworkManager network = null;

        try
        {
            PlayModeTestState.ClearNetworkState();
            network = go.AddComponent<NetworkManager>();
            network.enabled = false;

            InvokePrivate(
                network,
                "ApplyAuthResponse",
                "{\"authToken\":\"jwt-existing-token\",\"refreshToken\":\"refresh-existing-token\",\"playerId\":\"player_existing\"}");

            var result = CreateRuntimeDto("NetworkRequestResult");
            SetDtoField(result, "ResponseCode", 401L);
            SetDtoField(result, "Kind", NetworkErrorKind.Unauthorized);

            bool shouldProbe = (bool)InvokePrivateStatic("ShouldProbeTokenAfterRestoreFailure", result);
            Assert.That(shouldProbe, Is.True);
        }
        finally
        {
            if (network != null)
                Object.DestroyImmediate(network.gameObject);
            else
                Object.DestroyImmediate(go);

            PlayModeTestState.ClearNetworkState();
        }
    }

    [Test]
    public void NetworkManager_AcceptsDocumentedAuthResponseAndProfileState()
    {
        var go = new GameObject("NetworkManagerAuthContractSmoke");
        NetworkManager network = null;

        try
        {
            PlayModeTestState.ClearNetworkState();
            network = go.AddComponent<NetworkManager>();
            network.enabled = false;

            var method = typeof(NetworkManager).GetMethod(
                "ApplyAuthResponse",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "ApplyAuthResponse not found");

            bool applied = (bool)method.Invoke(
                network,
                new object[]
                {
                    "{\"authToken\":\"jwt-doc-token\",\"refreshToken\":\"refresh-doc-token\",\"playerId\":\"player_doc\",\"isNew\":true,\"profile\":{\"locale\":\"ru\",\"platform\":\"editor\",\"createdAt\":\"2026-04-26T00:00:00Z\"}}"
                });

            Assert.That(applied, Is.True);
            Assert.That(NetworkManager.IsAuthenticated, Is.True);
            Assert.That(NetworkManager.CurrentProfile.playerId, Is.EqualTo("player_doc"));
            Assert.That(NetworkManager.CurrentProfile.isNew, Is.True);
            Assert.That(NetworkManager.CurrentProfile.locale, Is.EqualTo("ru"));
            Assert.That(NetworkManager.CurrentProfile.platform, Is.EqualTo("editor"));
            Assert.That(NetworkManager.LastErrorKind, Is.EqualTo(NetworkErrorKind.Success));
            Assert.That(PlayerPrefs.GetString("VN_REFRESH_TOKEN"), Is.Empty);
            string protectedRefreshToken = PlayerPrefs.GetString("VN_REFRESH_TOKEN_V2", "");
            Assert.That(protectedRefreshToken, Does.StartWith("v1:"));
            Assert.That(protectedRefreshToken, Is.Not.EqualTo("refresh-doc-token"));
        }
        finally
        {
            if (network != null)
                Object.DestroyImmediate(network.gameObject);
            else
                Object.DestroyImmediate(go);

            PlayModeTestState.ClearNetworkState();
        }
    }

    [Test]
    public void NetworkManager_BookmarkResponse_ParsesLiveArrayShape()
    {
        var go = new GameObject("NetworkManagerBookmarkContractSmoke");
        NetworkManager network = null;

        try
        {
            PlayModeTestState.ClearNetworkState();
            network = go.AddComponent<NetworkManager>();
            network.enabled = false;

            var bookmark = InvokePrivate(
                network,
                "ParseBookmarkResponse",
                "{\"bookmarks\":[{\"nodeGuid\":\"node_live\",\"episodeId\":\"ep_live\",\"label\":\"Live bookmark\",\"savedAt\":\"2026-04-26T17:46:37.7537408Z\"}]}");

            Assert.That(bookmark, Is.Not.Null);
            Assert.That(GetInstanceField<string>(bookmark, "nodeGuid"), Is.EqualTo("node_live"));
            Assert.That(GetInstanceField<string>(bookmark, "episodeId"), Is.EqualTo("ep_live"));
        }
        finally
        {
            if (network != null)
                Object.DestroyImmediate(network.gameObject);
            else
                Object.DestroyImmediate(go);

            PlayModeTestState.ClearNetworkState();
        }
    }

    [Test]
    public void NetworkJson_HeroNameRequestUsesLiveApiContract()
    {
        var request = CreateRuntimeDto("HeroNameRequest");
        SetDtoField(request, "name", "Anna");
        SetDtoField(request, "storyId", "story_1");

        string json = NetworkJson.ToJson(request);

        Assert.That(NetworkJson.GetString(json, "name"), Is.EqualTo("Anna"));
        Assert.That(NetworkJson.GetString(json, "storyId"), Is.EqualTo("story_1"));
        Assert.That(NetworkJson.GetRawValue(json, "heroName"), Is.Null);
        Assert.That(NetworkJson.GetRawValue(json, "nodeGuid"), Is.Null);
        Assert.That(NetworkJson.GetRawValue(json, "episodeId"), Is.Null);
    }

    [Test]
    public void NetworkJson_FeaturesResponseIncludesDocumentedFullAccessFlag()
    {
        var response = CreateRuntimeDto("FeaturesResponse");
        SetDtoField(response, "fullAccess", true);

        string json = NetworkJson.ToJson(response);

        Assert.That(NetworkJson.GetBool(json, "fullAccess"), Is.True);
    }

    [Test]
    public void NetworkManager_BalanceParsesDocumentedDailyStreakShape()
    {
        var go = new GameObject("NetworkManagerBalanceContractSmoke");
        NetworkManager network = null;

        try
        {
            PlayModeTestState.ClearNetworkState();
            network = go.AddComponent<NetworkManager>();
            network.enabled = false;

            var balance = CreateRuntimeDto("BalanceResponse");
            SetDtoField(balance, "hearts", 50);
            SetDtoField(balance, "candles", 3);
            SetDtoField(balance, "candlesCap", 3);
            SetDtoField(balance, "nextCandleAt", "2026-04-26T00:00:00Z");

            var daily = CreateRuntimeDto("DailyStreakResponse");
            SetDtoField(daily, "day", 4);
            SetDtoField(balance, "dailyStreak", daily);

            InvokePrivate(network, "ApplyBalance", balance);

            Assert.That(NetworkManager.LastBalance.hearts, Is.EqualTo(50));
            Assert.That(NetworkManager.LastBalance.candles, Is.EqualTo(3));
            Assert.That(NetworkManager.LastBalance.dailyStreakDay, Is.EqualTo(4));
            Assert.That(NetworkManager.LastBalance.nextCandleAt, Is.EqualTo("2026-04-26T00:00:00Z"));
        }
        finally
        {
            if (network != null)
                Object.DestroyImmediate(network.gameObject);
            else
                Object.DestroyImmediate(go);

            PlayModeTestState.ClearNetworkState();
        }
    }

    [Test]
    public void NetworkManager_ProgressParsesDocumentedHeroNamesShape()
    {
        var go = new GameObject("NetworkManagerHeroNamesContractSmoke");
        NetworkManager network = null;

        try
        {
            PlayModeTestState.ClearNetworkState();
            network = go.AddComponent<NetworkManager>();
            network.enabled = false;

            InvokePrivate(
                network,
                "ApplyLoadedProgressJson",
                "{\"currentEpisodeId\":\"ep_api_01\",\"currentNodeGuid\":\"node_api_01\",\"heroNames\":{\"story_api\":\"Lena\"}}");

            Assert.That(NetworkManager.CurrentProfile.heroName, Is.EqualTo("Lena"));
            Assert.That(NetworkManager.LastProgressEpisodeId, Is.EqualTo("ep_api_01"));
            Assert.That(NetworkManager.LastProgressNodeGuid, Is.EqualTo("node_api_01"));
        }
        finally
        {
            if (network != null)
                Object.DestroyImmediate(network.gameObject);
            else
                Object.DestroyImmediate(go);

            PlayModeTestState.ClearNetworkState();
        }
    }

    [Test]
    public void NetworkJson_FavoriteAddRequestUsesStoryIdContract()
    {
        var request = CreateRuntimeDto("FavoriteAddRequest");
        SetDtoField(request, "storyId", "story_1");

        string json = NetworkJson.ToJson(request);

        Assert.That(NetworkJson.GetString(json, "storyId"), Is.EqualTo("story_1"));
        Assert.That(NetworkJson.GetRawValue(json, "episodeId"), Is.Null);
    }

    [Test]
    public void ProgressReconciliation_LocalV2WinsOverServerGuidWithoutTimestamp()
    {
        var go = new GameObject("NetworkManagerReconcileSmoke");
        NetworkManager network = null;

        try
        {
            PlayModeTestState.ClearNetworkState();
            network = go.AddComponent<NetworkManager>();
            network.enabled = false;

            InvokePrivate(network, "ApplyLoadedProgressJson", "{\"currentEpisodeId\":\"ep_api_01\",\"currentNodeGuid\":\"server_guid_only\"}");

            var local = new SaveData
            {
                version = SaveData.CurrentVersion,
                storyId = "story_api",
                episodeId = "ep_api_01",
                currentNodeGuid = "local_full_snapshot",
                savedAtIso = "2026-04-26T10:00:00.0000000Z"
            };

            var resolved = NetworkManager.ResolveLatestProgressSnapshot("story_api", local);

            Assert.That(resolved, Is.SameAs(local));
            Assert.That(resolved.currentNodeGuid, Is.EqualTo("local_full_snapshot"));
        }
        finally
        {
            if (network != null)
                Object.DestroyImmediate(network.gameObject);
            else
                Object.DestroyImmediate(go);

            PlayModeTestState.ClearNetworkState();
        }
    }

    [Test]
    public void ProgressReconciliation_ServerNewerWinsAndClearsStalePending()
    {
        var go = new GameObject("NetworkManagerPendingReconcileSmoke");
        NetworkManager network = null;

        try
        {
            PlayModeTestState.ClearNetworkState();
            network = go.AddComponent<NetworkManager>();
            network.enabled = false;

            var pending = new PendingProgressPayload
            {
                storyId = "story_api",
                currentEpisodeId = "ep_api_01",
                currentNodeGuid = "pending_old",
                savedAtIso = "2026-04-26T09:00:00.0000000Z",
                snapshot = new SaveData
                {
                    version = SaveData.CurrentVersion,
                    storyId = "story_api",
                    episodeId = "ep_api_01",
                    currentNodeGuid = "pending_old",
                    savedAtIso = "2026-04-26T09:00:00.0000000Z"
                }
            };

            InvokePrivateStatic("SavePendingProgress", pending);
            Assert.That(NetworkManager.HasPendingSync, Is.True);

            InvokePrivate(
                network,
                "ApplyLoadedProgressJson",
                "{\"storyId\":\"story_api\",\"currentEpisodeId\":\"ep_api_01\",\"currentNodeGuid\":\"server_new\",\"updatedAt\":\"2026-04-26T11:00:00.0000000Z\"}");

            var resolved = NetworkManager.ResolveLatestProgressSnapshot("story_api", null);

            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved.currentNodeGuid, Is.EqualTo("server_new"));
            Assert.That(NetworkManager.HasPendingSync, Is.False);
        }
        finally
        {
            if (network != null)
                Object.DestroyImmediate(network.gameObject);
            else
                Object.DestroyImmediate(go);

            PlayModeTestState.ClearNetworkState();
        }
    }

    [Test]
    public void NetworkJson_UnescapesUnicodeStrings()
    {
        string message = NetworkJson.GetString(
            "{\"message\":\"\\u041f\\u0440\\u0438\\u0432\\u0435\\u0442\"}",
            "message");

        Assert.That(message, Is.EqualTo("\u041f\u0440\u0438\u0432\u0435\u0442"));
    }

    [Test]
    public void CatalogResponse_ParsesArrayAndEnvelopeShapes()
    {
        const string arrayJson =
            "[{\"seasonId\":\"season_1\",\"title\":\"Season 1\",\"order\":1,\"episodes\":[{\"episodeId\":\"ep_s1e1\",\"seasonId\":\"season_1\",\"order\":1,\"title\":\"Pilot\",\"isPremium\":false,\"candleCost\":0,\"isUnlocked\":true,\"hasRemoteContent\":true}]}]";
        const string envelopeJson =
            "{\"seasons\":[{\"seasonId\":\"season_2\",\"title\":\"Season 2\",\"order\":2,\"episodes\":[{\"episodeId\":\"ep_s2e1\",\"seasonId\":\"season_2\",\"order\":1,\"title\":\"Return\",\"isPremium\":true,\"candleCost\":3,\"isUnlocked\":false,\"hasRemoteContent\":false}]}]}";
        const string storyArrayJson =
            "[{\"storyId\":\"story_1\",\"title\":\"Story\",\"seasons\":[{\"seasonId\":\"season_3\",\"storyId\":\"story_1\",\"title\":\"Season 3\",\"order\":3,\"episodes\":[{\"episodeId\":\"ep_s3e1\",\"seasonId\":\"season_3\",\"storyId\":\"story_1\",\"order\":1,\"title\":\"Nested\",\"isPremium\":false,\"candleCost\":0,\"isUnlocked\":true,\"hasRemoteContent\":true}]}]},{\"storyId\":\"story_2\",\"title\":\"Story 2\",\"seasons\":[{\"seasonId\":\"season_4\",\"title\":\"Season 4\",\"order\":4,\"episodes\":[{\"episodeId\":\"ep_s4e1\",\"seasonId\":\"season_4\",\"order\":1,\"title\":\"Nested 2\",\"isPremium\":false,\"candleCost\":0,\"isUnlocked\":true,\"hasRemoteContent\":false}]}]}]";
        const string documentedJson =
            "{\"episodes\":[{\"id\":\"ep_s1e1\",\"title\":\"Episode 1\",\"season\":1,\"episode\":1,\"version\":\"1.0.0\",\"isPremium\":false,\"candleCost\":0}]}";

        var arraySeasons = NetworkManager.ParseCatalogResponse(arrayJson);
        var envelopeSeasons = NetworkManager.ParseCatalogResponse(envelopeJson);
        var storyArraySeasons = NetworkManager.ParseCatalogResponse(storyArrayJson);
        var documentedSeasons = NetworkManager.ParseCatalogResponse(documentedJson);

        Assert.That(arraySeasons.Count, Is.EqualTo(1));
        Assert.That(arraySeasons[0].seasonId, Is.EqualTo("season_1"));
        Assert.That(arraySeasons[0].episodes[0].episodeId, Is.EqualTo("ep_s1e1"));
        Assert.That(arraySeasons[0].episodes[0].hasRemoteContent, Is.True);

        Assert.That(envelopeSeasons.Count, Is.EqualTo(1));
        Assert.That(envelopeSeasons[0].seasonId, Is.EqualTo("season_2"));
        Assert.That(envelopeSeasons[0].episodes[0].isPremium, Is.True);
        Assert.That(envelopeSeasons[0].episodes[0].candleCost, Is.EqualTo(3));

        Assert.That(storyArraySeasons.Count, Is.EqualTo(2));
        Assert.That(storyArraySeasons[0].seasonId, Is.EqualTo("season_3"));
        Assert.That(storyArraySeasons[0].storyId, Is.EqualTo("story_1"));
        Assert.That(storyArraySeasons[0].episodes[0].episodeId, Is.EqualTo("ep_s3e1"));
        Assert.That(storyArraySeasons[0].episodes[0].storyId, Is.EqualTo("story_1"));
        Assert.That(storyArraySeasons[1].seasonId, Is.EqualTo("season_4"));
        Assert.That(storyArraySeasons[1].storyId, Is.EqualTo("story_2"));
        Assert.That(storyArraySeasons[1].episodes[0].storyId, Is.EqualTo("story_2"));

        Assert.That(documentedSeasons.Count, Is.EqualTo(1));
        Assert.That(documentedSeasons[0].seasonId, Is.EqualTo("season_1"));
        Assert.That(documentedSeasons[0].episodes[0].episodeId, Is.EqualTo("ep_s1e1"));
        Assert.That(documentedSeasons[0].episodes[0].contentVersion, Is.EqualTo("1.0.0"));
        Assert.That(documentedSeasons[0].episodes[0].hasRemoteContent, Is.True);
    }

    [Test]
    public void NetworkManager_OfflineSpendCandlesUsesFullAmount()
    {
        var go = new GameObject("NetworkManagerSpendSmoke");
        var network = go.AddComponent<NetworkManager>();
        network.enabled = false;

        try
        {
            PlayModeTestState.ClearNetworkState();
            PrototypeFeatureFlags.SetLocalPremiumSpendEnabled(true);
            PlayerData.SetCandlesValue(5);

            bool? ok = null;
            var routine = network.SpendCandles(3, result => ok = result);
            while (routine.MoveNext()) { }

            Assert.That(ok, Is.True);
            Assert.That(PlayerData.Candles, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(go);
            PlayModeTestState.ClearNetworkState();
        }
    }

    [Test]
    public void PlayerData_LoadOnStartupPreservesExplicitRuntimeBalance()
    {
        var candlesField = typeof(PlayerData).GetField("<Candles>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var loadedField = typeof(PlayerData).GetField("_loaded", BindingFlags.Static | BindingFlags.NonPublic);
        var loadOnStartup = typeof(PlayerData).GetMethod("LoadOnStartup", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(candlesField, Is.Not.Null);
        Assert.That(loadedField, Is.Not.Null);
        Assert.That(loadOnStartup, Is.Not.Null);

        try
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            PlayerData.SetCandlesValue(3);
            candlesField.SetValue(null, 7);
            loadedField.SetValue(null, true);

            loadOnStartup.Invoke(null, null);

            Assert.That(PlayerData.Candles, Is.EqualTo(7));
        }
        finally
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            PlayerData.SetCandlesValue(0);
        }
    }

    [Test]
    public void PlayerData_StartPreservesExplicitRuntimeBalance()
    {
        var candlesField = typeof(PlayerData).GetField("<Candles>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var loadedField = typeof(PlayerData).GetField("_loaded", BindingFlags.Static | BindingFlags.NonPublic);
        var root = new GameObject("PlayerDataStartRegression");

        Assert.That(candlesField, Is.Not.Null);
        Assert.That(loadedField, Is.Not.Null);

        try
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            PlayerData.SetCandlesValue(3);
            candlesField.SetValue(null, 7);
            loadedField.SetValue(null, true);

            var playerData = root.AddComponent<PlayerData>();
            InvokePrivate(playerData, "Start");

            Assert.That(PlayerData.Candles, Is.EqualTo(7));
        }
        finally
        {
            Object.DestroyImmediate(root);
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            PlayerData.SetCandlesValue(0);
        }
    }

    [Test]
    public void UnityChoiceCostsPublisher_BuildsSanitizedPaidChoicePayload()
    {
        StoryGraph graph = null;
        try
        {
            graph = ScriptableObject.CreateInstance<StoryGraph>();
            graph.episodeId = "ep_test";

            var choice = graph.AddNode<ChoiceNode>();
            choice.guid = "choice_paid";
            choice.name = "Paid Choice";
            choice.options = new List<ChoiceOption>
            {
                new ChoiceOption { text = "Free", isPremium = false, premiumCost = 0 },
                new ChoiceOption { text = "Paid", isPremium = true, premiumCost = 7 }
            };

            UnityChoiceCostsPublishPayload payload = UnityChoiceCostsPublisher.BuildPayload(
                new[] { graph },
                "story_test",
                "");

            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.costs.Count, Is.EqualTo(1));
            Assert.That(payload.choices.Count, Is.EqualTo(1));
            Assert.That(payload.items.Count, Is.EqualTo(1));
            Assert.That(payload.choiceCosts.Count, Is.EqualTo(1));

            UnityChoiceCostEntry entry = payload.costs[0];
            Assert.That(entry.storyId, Is.EqualTo("story_test"));
            Assert.That(entry.episodeId, Is.EqualTo("ep_test"));
            Assert.That(entry.nodeGuid, Is.EqualTo("choice_paid"));
            Assert.That(entry.choiceIndex, Is.EqualTo(1));
            Assert.That(entry.cost, Is.EqualTo(7));
            Assert.That(entry.currency, Is.EqualTo("hearts"));
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void NetworkManager_RuntimeAllowlistBlocksUnityPublisherEndpoints()
    {
        var method = typeof(NetworkManager).GetMethod(
            "IsAllowedRuntimeApiPath",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        bool allowed = (bool)method.Invoke(null, new object[] { "/unity/choice-costs", "POST" });
        Assert.That(allowed, Is.False);
    }

    [Test]
    public void RemoteEpisodeGraphCache_RoundTripsVersionedPayload()
    {
        const string episodeId = "ep_remote_cache_smoke";
        RemoteEpisodeGraphCache.Delete(episodeId);

        try
        {
            RemoteEpisodeGraphCache.Save(
                episodeId,
                "2.5.0",
                "{\"scenes\":[]}",
                "{\"contentVersion\":\"2.5.0\",\"graph\":{\"scenes\":[]}}");

            Assert.That(RemoteEpisodeGraphCache.GetLocalVersion(episodeId), Is.EqualTo("2.5.0"));
            Assert.That(RemoteEpisodeGraphCache.TryLoad(episodeId, out var entry), Is.True);
            Assert.That(entry.episodeId, Is.EqualTo(episodeId));
            Assert.That(entry.contentVersion, Is.EqualTo("2.5.0"));
            Assert.That(entry.graphJson, Is.EqualTo("{\"scenes\":[]}"));
        }
        finally
        {
            RemoteEpisodeGraphCache.Delete(episodeId);
        }
    }

    [Test]
    public void RemoteStoryGraphImporter_BuildsGraphFromScenesDto()
    {
        const string episodeId = "ep_remote_import_smoke";
        const string graphJson =
            "{\"scenes\":[{\"sceneDescription\":\"Intro\",\"nodes\":[{\"type\":\"dialogue\",\"lines\":[{\"speaker\":\"Lena\",\"emotion\":\"Happy\",\"text\":\"Hello\"}]},{\"type\":\"choice\",\"choicePrompt\":\"Choose\",\"choices\":[{\"text\":\"Go\",\"branch\":[{\"type\":\"stat_change\",\"statId\":\"town\",\"statDelta\":1,\"statDisplayName\":\"Town\"}]},{\"text\":\"Stay\",\"branch\":[]}]}]}]}";

        ExpectMissingCharacterLog("Lena");
        Assert.That(
            RemoteStoryGraphImporter.TryBuildGraph(episodeId, graphJson, out var graph, out var reason),
            Is.True,
            reason);

        Assert.That(graph, Is.Not.Null);
        Assert.That(graph.episodeId, Is.EqualTo(episodeId));
        Assert.That(graph.nodes.OfType<StartNode>().Count(), Is.EqualTo(1));
        Assert.That(graph.nodes.OfType<SceneSetupNode>().Count(), Is.EqualTo(1));
        Assert.That(graph.nodes.OfType<DialogueNode>().Count(), Is.EqualTo(1));
        Assert.That(graph.nodes.OfType<ChoiceNode>().Count(), Is.EqualTo(1));
        Assert.That(graph.nodes.OfType<StatChangeNode>().Count(), Is.EqualTo(1));

        var choice = graph.nodes.OfType<ChoiceNode>().Single();
        Assert.That(choice.options.Count, Is.EqualTo(2));
        Assert.That(choice.GetOutputPort("choices 0"), Is.Not.Null);
        Assert.That(choice.GetOutputPort("choices 0").Connection, Is.Not.Null);
        Assert.That(choice.GetOutputPort("choices 1"), Is.Not.Null);
    }

    [Test]
    public void RemoteStoryGraphImporter_BuildsGraphFromFlatNodesDto()
    {
        const string episodeId = "ep_remote_flat_import_smoke";
        const string graphJson =
            "{\"sceneDescription\":\"Pilot\",\"suggestedBackground\":\"cafe\",\"nodes\":[{\"guid\":\"flat_dialogue\",\"type\":\"dialogue\",\"lines\":[{\"speaker\":\"Lena\",\"emotion\":\"Happy\",\"text\":\"Hello\"}]},{\"guid\":\"flat_stat\",\"type\":\"statChange\",\"statId\":\"town\",\"statDelta\":2,\"statDisplayName\":\"Town\"}]}";

        ExpectMissingCharacterLog("Lena");
        Assert.That(
            RemoteStoryGraphImporter.TryBuildGraph(episodeId, graphJson, out var graph, out var reason),
            Is.True,
            reason);

        Assert.That(graph, Is.Not.Null);
        Assert.That(graph.episodeId, Is.EqualTo(episodeId));
        Assert.That(graph.nodes.OfType<StartNode>().Count(), Is.EqualTo(1));
        Assert.That(graph.nodes.OfType<SceneSetupNode>().Count(), Is.EqualTo(1));
        Assert.That(graph.nodes.OfType<DialogueNode>().Count(), Is.EqualTo(1));
        Assert.That(graph.nodes.OfType<StatChangeNode>().Count(), Is.EqualTo(1));

        var scene = graph.nodes.OfType<SceneSetupNode>().Single();
        Assert.That(scene.sceneLabel, Is.EqualTo("Pilot"));
        Assert.That(scene.suggestedBackground, Is.EqualTo("cafe"));
    }

    [Test]
    public void NetworkRuntimeConfig_ExistsAndResolvesSelectedEnvironment()
    {
        var configAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(NetworkConfigPath);
        Assert.That(configAsset, Is.Not.Null, $"Missing network config asset: {NetworkConfigPath}");

        NetworkRuntimeConfigLoader.ResetCache();
        var config = NetworkRuntimeConfigLoader.Load();

        Assert.That(config, Is.Not.Null);
        Assert.That(config.ResolveSelectedEnvironmentId(), Is.EqualTo("prod"));
        Assert.That(config.ResolveSelectedEnvironment(), Is.Not.Null);
        Assert.That(config.ResolveBaseUrl(), Is.EqualTo(ApiRoutes.BaseUrl));
        Assert.That(config.GetRequestTimeoutSeconds(), Is.GreaterThanOrEqualTo(1));
        Assert.That(config.GetRetryCount(), Is.GreaterThanOrEqualTo(0));
    }

    [UnityTest]
    [Explicit("Manual-only low-volume probe. Writes disposable guest progress/bookmark state to the configured NovelApp API.")]
    public IEnumerator LiveApi_DisposableGuest_KeyScenarioSmoke()
    {
        const string baseUrl = ApiRoutes.BaseUrl;
        string deviceId = "kxyr0-smoke-" + System.DateTime.UtcNow.Ticks;

        LiveApiResponse guest = null;
        yield return SendLiveApiRequest(
            "POST",
            baseUrl + ApiRoutes.AuthGuest,
            "{\"deviceId\":\"" + deviceId + "\",\"platform\":\"editor\",\"appVersion\":\"1.0.0\"}",
            null,
            response => guest = response);

        Assert.That(guest.statusCode, Is.EqualTo(200), guest.body);
        string token = NetworkJson.GetFirstString(guest.body, "token", "authToken");
        string refreshToken = NetworkJson.GetString(guest.body, "refreshToken");
        Assert.That(token, Is.Not.Empty, "Guest auth must return token");
        Assert.That(refreshToken, Is.Not.Empty, "Guest auth must return refreshToken");

        LiveApiResponse restore = null;
        yield return SendLiveApiRequest(
            "POST",
            baseUrl + ApiRoutes.AuthRefresh,
            "{\"refreshToken\":\"" + NetworkJson.Escape(refreshToken) + "\"}",
            null,
            response => restore = response);
        Assert.That(restore.statusCode, Is.EqualTo(200).Or.EqualTo(401), restore.body);
        if (restore.statusCode == 200 && !string.IsNullOrEmpty(NetworkJson.GetFirstString(restore.body, "token", "authToken")))
        {
            token = NetworkJson.GetFirstString(restore.body, "token", "authToken");
        }
        else if (restore.statusCode == 401)
        {
            TestContext.WriteLine(ApiRoutes.AuthRefresh + " returned 401 for a fresh disposable guest; continuing with the still-valid guest auth token.");
        }

        LiveApiResponse balance = null;
        yield return SendLiveApiRequest("GET", baseUrl + ApiRoutes.PlayerBalance, null, token, response => balance = response);
        Assert.That(balance.statusCode, Is.EqualTo(200), balance.body);
        Assert.That(NetworkJson.GetRawValue(balance.body, "candles"), Is.Not.Null);

        LiveApiResponse features = null;
        yield return SendLiveApiRequest("GET", baseUrl + ApiRoutes.PlayerFeatures, null, token, response => features = response);
        Assert.That(features.statusCode, Is.EqualTo(200), features.body);

        LiveApiResponse heroName = null;
        yield return SendLiveApiRequest("GET", baseUrl + ApiRoutes.PlayerHeroName, null, token, response => heroName = response);
        Assert.That(heroName.statusCode, Is.EqualTo(200), heroName.body);

        LiveApiResponse catalog = null;
        yield return SendLiveApiRequest("GET", baseUrl + ApiRoutes.ContentCatalog, null, token, response => catalog = response);
        Assert.That(catalog.statusCode, Is.EqualTo(200), catalog.body);

        string episodeId = GetFirstEpisodeId(catalog.body);
        Assert.That(episodeId, Is.Not.Empty, "Catalog must expose at least one episode id");

        string nodeGuid = "kxyr0-smoke-node-" + System.DateTime.UtcNow.Ticks;
        LiveApiResponse saveProgress = null;
        yield return SendLiveApiRequest(
            "POST",
            baseUrl + ApiRoutes.PlayerProgressSave,
            "{\"episodeId\":\"" + NetworkJson.Escape(episodeId) + "\",\"nodeId\":\"" + NetworkJson.Escape(nodeGuid) + "\",\"currentEpisodeId\":\"" + NetworkJson.Escape(episodeId) + "\",\"currentNodeGuid\":\"" + NetworkJson.Escape(nodeGuid) + "\",\"stats\":{\"kxyr0Smoke\":1},\"variables\":{\"kxyr0Smoke\":true},\"flags\":{\"kxyr0Smoke\":true},\"unlockedEpisodes\":[\"" + NetworkJson.Escape(episodeId) + "\"]}",
            token,
            response => saveProgress = response);
        Assert.That(saveProgress.statusCode, Is.EqualTo(200), saveProgress.body);

        LiveApiResponse progress = null;
        yield return SendLiveApiRequest("GET", baseUrl + ApiRoutes.PlayerProgress, null, token, response => progress = response);
        Assert.That(progress.statusCode, Is.EqualTo(200), progress.body);
        Assert.That(NetworkJson.GetString(progress.body, "currentNodeGuid"), Is.EqualTo(nodeGuid));

        LiveApiResponse graph = null;
        yield return SendLiveApiRequest(
            "GET",
            baseUrl + ApiRoutes.ContentEpisodeGraph(episodeId),
            null,
            token,
            response => graph = response);
        Assert.That(graph.statusCode == 200 || graph.statusCode == 304, Is.True, graph.body);

        bool bookmarksEnabled = NetworkJson.GetBool(NetworkJson.GetRawValue(features.body, "bookmarks"), "enabled");
        LiveApiResponse bookmarkSave = null;
        yield return SendLiveApiRequest(
            "POST",
            baseUrl + ApiRoutes.PlayerBookmarkSave,
            "{\"nodeGuid\":\"" + NetworkJson.Escape(nodeGuid) + "\",\"episodeId\":\"" + NetworkJson.Escape(episodeId) + "\",\"label\":\"kxyr0 smoke\"}",
            token,
            response => bookmarkSave = response);

        if (bookmarkSave.statusCode == 200)
        {
            LiveApiResponse bookmark = null;
            yield return SendLiveApiRequest("GET", baseUrl + ApiRoutes.PlayerBookmark, null, token, response => bookmark = response);
            Assert.That(bookmark.statusCode, Is.EqualTo(200), bookmark.body);
            Assert.That(bookmark.body, Does.Contain(nodeGuid));
        }
        else
        {
            Assert.That(bookmarksEnabled, Is.False, "Enabled bookmarks should be saved by the live API.");
            Assert.That(bookmarkSave.statusCode, Is.EqualTo(402), bookmarkSave.body);
            Assert.That(bookmarkSave.body, Does.Contain("feature_locked"));
        }
    }

    [UnityTest]
    public IEnumerator PlayMode_RemoteFlatGraph_SupportsSaveLoadAndBookmark()
    {
        return RunPlayModeScenario(RemoteFlatGraphScenario);
    }

    [UnityTest]
    public IEnumerator PlayMode_ChapterTransition_UnlocksPremiumChapter()
    {
        return RunPlayModeScenario(PremiumChapterScenario);
    }

    IEnumerator RemoteFlatGraphScenario(PlayModeStoryTestRig rig)
    {
        var localGraph = PlayModeStoryFactory.CreateDialogueGraph(
            "playmode_remote_ep01",
            "Local fallback",
            "local_dialogue",
            "Local intro");
        var story = PlayModeStoryFactory.CreateStory(
            "playmode_story",
            "playmode_story_s01",
            PlayModeStoryFactory.CreateChapter("playmode_remote_ep01", "Pilot", localGraph));

        const string remoteGraphJson =
            "{\"sceneDescription\":\"Pilot intro\",\"suggestedBackground\":\"cafe\",\"nodes\":[{\"guid\":\"remote_dialogue\",\"type\":\"dialogue\",\"lines\":[{\"speaker\":\"Lena\",\"text\":\"Remote intro\"},{\"speaker\":\"Lena\",\"text\":\"Remote follow-up\"}]}]}";

        rig.SeedCatalog(new CatalogEpisodeResponse
        {
            episodeId = "playmode_remote_ep01",
            seasonId = "playmode_story_s01",
            order = 1,
            title = "Pilot",
            isPremium = false,
            candleCost = 0,
            isUnlocked = true,
            contentVersion = "1.0.0",
            hasRemoteContent = true
        });
        rig.SeedRemoteGraph("playmode_remote_ep01", "1.0.0", remoteGraphJson);
        rig.SelectStory(story);

        ExpectMissingCharacterLog("Lena");
        rig.StoryManager.StartStory();
        yield return WaitForCondition(
            () => rig.CurrentNode != null && rig.CurrentNode.guid == "remote_dialogue",
            "Remote graph node did not become active.");

        Assert.That(rig.StoryManager.storyGraph, Is.Not.SameAs(localGraph));
        Assert.That(rig.DialogueUI.dialogueText.text, Is.EqualTo("Remote intro"));

        var save = SaveManager.Instance.SaveCurrentData(0, rig.StoryManager);
        Assert.That(save, Is.Not.Null);
        Assert.That(save.currentNodeGuid, Is.EqualTo("remote_dialogue"));
        Assert.That(save.currentDialogueLineIndex, Is.EqualTo(0));

        rig.StoryManager.SaveBookmark();
        Assert.That(StoryHistory.Instance.LoadBookmarkFromPrefs("playmode_story"), Is.True);

        rig.StoryManager.OnDialogueClick();
        Assert.That(rig.StoryManager.CurrentDialogueLineIndex, Is.EqualTo(1));
        Assert.That(rig.DialogueUI.dialogueText.text, Is.EqualTo("Remote follow-up"));

        rig.StoryManager.LoadSaveAndStart();
        yield return WaitForCondition(
            () => rig.CurrentNode != null &&
                  rig.CurrentNode.guid == "remote_dialogue" &&
                  rig.StoryManager.CurrentDialogueLineIndex == 1 &&
                  rig.DialogueUI.dialogueText.text == "Remote follow-up",
            "Save/load did not restore the latest autosaved remote dialogue line.");

        rig.StoryManager.GoToBookmark();
        yield return WaitForCondition(
            () => rig.CurrentNode != null &&
                  rig.CurrentNode.guid == "remote_dialogue" &&
                  rig.StoryManager.CurrentDialogueLineIndex == 0 &&
                  rig.DialogueUI.dialogueText.text == "Remote intro",
            "Bookmark restore did not return to the saved remote dialogue line.");
    }

    IEnumerator PremiumChapterScenario(PlayModeStoryTestRig rig)
    {
        var firstGraph = PlayModeStoryFactory.CreateDialogueGraph(
            "playmode_linear_ep01",
            "Intro",
            "chapter_one_dialogue",
            "Finish chapter one");
        var premiumGraph = PlayModeStoryFactory.CreateDialogueGraph(
            "playmode_premium_ep02",
            "Premium intro",
            "premium_dialogue",
            "Premium chapter started");

        var story = PlayModeStoryFactory.CreateStory(
            "playmode_story",
            "playmode_story_s01",
            PlayModeStoryFactory.CreateChapter("playmode_linear_ep01", "Chapter 1", firstGraph),
            PlayModeStoryFactory.CreateChapter("playmode_premium_ep02", "Chapter 2", premiumGraph, isPremium: true, unlockCost: 3));

        rig.SeedCatalog(
            new CatalogEpisodeResponse
            {
                episodeId = "playmode_linear_ep01",
                seasonId = "playmode_story_s01",
                order = 1,
                title = "Chapter 1",
                isPremium = false,
                candleCost = 0,
                isUnlocked = true
            },
            new CatalogEpisodeResponse
            {
                episodeId = "playmode_premium_ep02",
                seasonId = "playmode_story_s01",
                order = 2,
                title = "Chapter 2",
                isPremium = true,
                candleCost = 3,
                isUnlocked = false
            });

        PlayerData.SetCandlesValue(10);
        Assert.That(PlayerData.Candles, Is.EqualTo(10));
        rig.SelectStory(story);
        Assert.That(PlayerData.Candles, Is.EqualTo(10));

        rig.StoryManager.StartStory();
        yield return WaitForCondition(
            () => rig.CurrentNode != null && rig.CurrentNode.guid == "chapter_one_dialogue",
            "Chapter one dialogue did not start.");
        Assert.That(PlayerData.Candles, Is.EqualTo(10));

        rig.StoryManager.OnDialogueClick();
        yield return null;

        Assert.That(PlayerData.Candles, Is.EqualTo(10));
        Assert.That(rig.StoryManager.CurrentChapterIndex, Is.EqualTo(1));
        Assert.That(rig.StoryManager.endStoryPanel.activeSelf, Is.True);
        Assert.That(rig.StoryManager.CanContinueFromEndPanel, Is.True);
        Assert.That(rig.StoryManager.EndPanelNextChapterTitle, Is.EqualTo("Chapter 2"));
        Assert.That(rig.StoryManager.LastCompletedChapterTitle, Is.EqualTo("Chapter 1"));

        rig.StoryManager.purchase.onClick.Invoke();
        Assert.That(rig.DialogueUI.purchasePopup.activeSelf, Is.True);
        Assert.That(PlayerData.Candles, Is.EqualTo(10));
        Assert.That(rig.DialogueUI.purchasePrice.text, Does.Contain("3"));

        rig.DialogueUI.buyButton.onClick.Invoke();
        yield return WaitForCondition(
            () => rig.CurrentNode != null && rig.CurrentNode.guid == "premium_dialogue",
            "Premium chapter did not start after purchase.");

        Assert.That(PlayerData.Candles, Is.EqualTo(7));
        Assert.That(PlayerPrefs.GetInt("chapter_unlock_playmode_premium_ep02", 0), Is.EqualTo(1));
        Assert.That(rig.StoryManager.CurrentChapterIndex, Is.EqualTo(1));
    }

    IEnumerator RunPlayModeScenario(System.Func<PlayModeStoryTestRig, IEnumerator> scenario)
    {
        System.Exception failure = null;
        PlayModeStoryTestRig rig = null;

        yield return new EnterPlayMode();

        PlayModeTestState.ResetAll();
        rig = PlayModeStoryTestRig.Create();

        var routine = scenario != null ? scenario(rig) : null;
        while (failure == null && routine != null)
        {
            object current = null;

            try
            {
                if (!routine.MoveNext())
                    break;

                current = routine.Current;
            }
            catch (System.Exception ex)
            {
                failure = ex;
                break;
            }

            yield return current;
        }

        rig?.Dispose();
        PlayModeTestState.ResetAll();

        yield return null;
        PlayModeTestState.ClearDotweenState();
        PlayModeTestState.RegisterDotweenExitCleanup();

        bool previousIgnoreFailingLogs = LogAssert.ignoreFailingMessages;
        LogAssert.ignoreFailingMessages = true;
        yield return new ExitPlayMode();
        LogAssert.ignoreFailingMessages = previousIgnoreFailingLogs;
        PlayModeTestState.ClearDotweenState();

        if (failure != null)
            throw failure;
    }

    static IEnumerator WaitForCondition(System.Func<bool> predicate, string failureMessage, int maxFrames = 60)
    {
        for (int frame = 0; frame < maxFrames; frame++)
        {
            if (predicate())
                yield break;

            yield return null;
        }

        Assert.Fail(failureMessage);
    }

    static IEnumerator SendLiveApiRequest(string method, string url, string body, string token, System.Action<LiveApiResponse> callback)
    {
        using var req = new UnityWebRequest(url, method);
        req.downloadHandler = new DownloadHandlerBuffer();

        if (!string.IsNullOrEmpty(body))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.SetRequestHeader("Content-Type", "application/json");
        }

        if (!string.IsNullOrEmpty(token))
            req.SetRequestHeader("Authorization", "Bearer " + token);

        req.timeout = 20;
        var operation = req.SendWebRequest();
        while (!operation.isDone)
            yield return null;

        callback?.Invoke(new LiveApiResponse
        {
            statusCode = req.responseCode,
            body = req.downloadHandler != null ? req.downloadHandler.text : "",
            error = req.error ?? ""
        });
    }

    static string GetFirstEpisodeId(string catalogJson)
    {
        var rawEpisodes = NetworkJson.GetRawValue(catalogJson, "episodes");
        if (!string.IsNullOrWhiteSpace(rawEpisodes))
        {
            foreach (var rawEpisode in NetworkJson.GetArrayItems(rawEpisodes))
            {
                var id = NetworkJson.GetFirstString(rawEpisode, "episodeId", "id");
                if (!string.IsNullOrEmpty(id))
                    return id;
            }
        }

        var seasons = NetworkManager.ParseCatalogResponse(catalogJson);
        foreach (var season in seasons)
        {
            if (season == null || season.episodes == null)
                continue;

            foreach (var episode in season.episodes)
            {
                if (episode != null && !string.IsNullOrEmpty(episode.episodeId))
                    return episode.episodeId;
            }
        }

        return "";
    }

    sealed class LiveApiResponse
    {
        public long statusCode;
        public string body;
        public string error;
    }

    static object CreateRuntimeDto(string typeName)
    {
        var type = typeof(NetworkManager).Assembly.GetType(typeName);
        Assert.That(type, Is.Not.Null, $"Runtime DTO type not found: {typeName}");
        return System.Activator.CreateInstance(type);
    }

    static void SetDtoField(object dto, string fieldName, object value)
    {
        Assert.That(dto, Is.Not.Null);

        var field = dto.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{dto.GetType().Name}.{fieldName} not found");
        field.SetValue(dto, value);
    }

    static object InvokePrivate(object target, string methodName, params object[] args)
    {
        Assert.That(target, Is.Not.Null);
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} not found");
        return method.Invoke(target, args);
    }

    static object InvokePrivateStatic(string methodName, params object[] args)
    {
        var method = typeof(NetworkManager).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"NetworkManager.{methodName} not found");
        return method.Invoke(null, args);
    }

    static List<string> BuildDialogueSentencePagesForTest(string text, int maxVisibleChars)
    {
        var method = typeof(StoryManager).GetMethod(
            "BuildDialogueSentencePages",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "StoryManager.BuildDialogueSentencePages not found");
        return (List<string>)method.Invoke(null, new object[] { text, maxVisibleChars });
    }

    static T GetInstanceField<T>(object target, string fieldName)
    {
        Assert.That(target, Is.Not.Null);

        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} not found");
        return (T)field.GetValue(target);
    }

    static void SetInstanceField(object target, string fieldName, object value)
    {
        Assert.That(target, Is.Not.Null);

        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} not found");
        field.SetValue(target, value);
    }

    sealed class PlayModeStoryTestRig
    {
        readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();
        readonly List<string> _remoteEpisodeIds = new List<string>();

        public GameObject Root { get; private set; }
        public StoryManager StoryManager { get; private set; }
        public DialogueUIManager DialogueUI { get; private set; }
        public GameState GameState { get; private set; }
        public BaseStoryNode CurrentNode => GameState != null ? GameState.currentNode : null;

        public static PlayModeStoryTestRig Create()
        {
            var rig = new PlayModeStoryTestRig();
            rig.Build();
            return rig;
        }

        public void SelectStory(StoryData story)
        {
            TrackAsset(story);
            if (story != null && story.chapters != null)
            {
                foreach (var chapter in story.chapters)
                {
                    TrackAsset(chapter);
                    if (chapter != null && chapter.graph != null)
                        TrackAsset(chapter.graph);
                }
            }

            if (story != null && story.seasons != null)
            {
                foreach (var season in story.seasons)
                {
                    TrackAsset(season);
                    if (season == null || season.chapters == null)
                        continue;

                    foreach (var chapter in season.chapters)
                    {
                        TrackAsset(chapter);
                        if (chapter != null && chapter.graph != null)
                            TrackAsset(chapter.graph);
                    }
                }
            }

            Assert.That(StoryManager.SelectStory(story), Is.True);
        }

        public void SeedCatalog(params CatalogEpisodeResponse[] episodes)
        {
            PlayModeTestState.SeedCatalog(episodes);
        }

        public void SeedRemoteGraph(string episodeId, string version, string graphJson)
        {
            _remoteEpisodeIds.Add(episodeId);
            RemoteEpisodeGraphCache.Delete(episodeId);
            RemoteEpisodeGraphCache.Save(
                episodeId,
                version,
                graphJson,
                "{\"episodeId\":\"" + episodeId + "\",\"contentVersion\":\"" + version + "\",\"graph\":" + graphJson + "}");
        }

        public void Dispose()
        {
            foreach (var episodeId in _remoteEpisodeIds)
                RemoteEpisodeGraphCache.Delete(episodeId);

            for (int i = _assets.Count - 1; i >= 0; i--)
            {
                if (_assets[i] != null)
                    UnityEngine.Object.DestroyImmediate(_assets[i]);
            }

            _assets.Clear();

            if (Root != null)
                UnityEngine.Object.DestroyImmediate(Root);

            StoryManager.Instance = null;
            GameState.Instance = null;
            SaveManager.Instance = null;
            StoryHistory.Instance = null;
            SubscriptionManager.Instance = null;
            PlayerAppearance.Instance = null;
        }

        void Build()
        {
            Root = new GameObject("PlayModeStoryTestRig");
            Root.SetActive(false);
            new GameObject("PlayModeDotweenCleanupGuard").AddComponent<DotweenSceneCleanupGuard>();

            var audioPrimary = Root.AddComponent<AudioSource>();
            var audioSecondary = Root.AddComponent<AudioSource>();
            var characterView = Root.AddComponent<CharacterViewManager>();
            var backgroundView = Root.AddComponent<BackgroundViewManager>();
            var storyHistory = Root.AddComponent<StoryHistory>();
            Root.AddComponent<PlayerAppearance>();
            var gameState = Root.AddComponent<GameState>();
            Root.AddComponent<SaveManager>();
            Root.AddComponent<SubscriptionManager>();
            var dialogueUI = Root.AddComponent<DialogueUIManager>();

            dialogueUI.nameText = CreateText("NameText", Root.transform);
            dialogueUI.dialogueText = CreateText("DialogueText", Root.transform);
            dialogueUI.choiceContainer = CreateRect("ChoiceContainer", Root.transform);
            dialogueUI.choiceButtonPrefab = CreateButton("ChoiceButtonPrefab", Root.transform, "Choice").gameObject;
            dialogueUI.choiceButtonPrefab.SetActive(false);
            dialogueUI.wardrobePanel = CreateRect("WardrobePanel", Root.transform).gameObject;
            dialogueUI.purchasePopup = CreateRect("PurchasePopup", Root.transform).gameObject;
            dialogueUI.purchaseTitle = CreateText("PurchaseTitle", dialogueUI.purchasePopup.transform);
            dialogueUI.purchasePrice = CreateText("PurchasePrice", dialogueUI.purchasePopup.transform);
            dialogueUI.buyButton = CreateButton("BuyButton", dialogueUI.purchasePopup.transform, "Buy");
            dialogueUI.cancelButton = CreateButton("CancelButton", dialogueUI.purchasePopup.transform, "Cancel");
            dialogueUI.purchasePopup.SetActive(false);

            var storyManager = Root.AddComponent<StoryManager>();
            storyManager.musicSource = audioPrimary;
            storyManager.sfxSource = audioSecondary;
            storyManager.characterView = characterView;
            storyManager.backgroundView = backgroundView;
            storyManager.dialogueUI = dialogueUI;
            storyManager.storyHistory = storyHistory;
            storyManager.endStoryPanel = CreateRect("EndPanel", Root.transform).gameObject;
            storyManager.noConnectionPanel = CreateRect("NoConnectionPanel", Root.transform).gameObject;
            storyManager.townText = CreateText("TownText", storyManager.endStoryPanel.transform);
            storyManager.storyText = CreateText("StoryText", storyManager.endStoryPanel.transform);
            storyManager.reputationText = CreateText("ReputationText", storyManager.endStoryPanel.transform);
            storyManager.heartsText = CreateText("HeartsText", storyManager.endStoryPanel.transform);
            storyManager.purchase = CreateButton("PurchaseButton", storyManager.endStoryPanel.transform, "Purchase");

            Root.SetActive(true);

            StoryManager = storyManager;
            DialogueUI = dialogueUI;
            GameState = gameState;
        }

        void TrackAsset(UnityEngine.Object asset)
        {
            if (asset != null && !_assets.Contains(asset))
                _assets.Add(asset);
        }

        static TMP_Text CreateText(string name, Transform parent)
        {
            var go = CreateRect(name, parent).gameObject;
            return go.AddComponent<TextMeshProUGUI>();
        }

        static Button CreateButton(string name, Transform parent, string label)
        {
            var go = CreateRect(name, parent).gameObject;
            go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            var text = CreateText(name + "Label", go.transform);
            text.text = label;
            return button;
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }

    [UnityTest]
    public IEnumerator PhoneDialogueUI_HideStopsPlaybackWithoutLateMessages()
    {
        var root = new GameObject("PhoneDialogueUISmoke");
        StoryGraph graph = null;

        try
        {
            var ui = root.AddComponent<PhoneDialogueUI>();
            ui.panel = CreateStandaloneRect("PhonePanel", root.transform).gameObject;
            ui.contactNameText = CreateStandaloneText("ContactName", ui.panel.transform);
            ui.contactAvatarImage = CreateStandaloneRect("ContactAvatar", ui.panel.transform).gameObject.AddComponent<Image>();
            ui.messagesContainer = CreateStandaloneRect("Messages", ui.panel.transform);
            ui.incomingBubblePrefab = CreatePhoneBubblePrefab("IncomingPrefab", root.transform);
            ui.outgoingBubblePrefab = CreatePhoneBubblePrefab("OutgoingPrefab", root.transform);
            ui.typingIndicator = CreateStandaloneRect("TypingIndicator", ui.panel.transform).gameObject;
            ui.tapToContinueText = CreateStandaloneText("TapToContinue", ui.panel.transform);
            ui.tapArea = CreateStandaloneButton("TapArea", ui.panel.transform, "Tap");
            ui.defaultTypingDelay = 0.05f;

            graph = ScriptableObject.CreateInstance<StoryGraph>();
            var node = graph.AddNode<PhoneDialogueNode>();
            node.contactName = "Smoke";
            node.typingDelay = 0.05f;
            node.messages = new List<PhoneMessage>
            {
                new PhoneMessage { senderName = "Contact", text = "one", side = PhoneMessageSide.Incoming },
                new PhoneMessage { senderName = "{PlayerName}", text = "two", side = PhoneMessageSide.Outgoing }
            };

            bool completed = false;

            yield return null;

            ui.Show(node, () => completed = true);
            yield return WaitEditModeSeconds(0.01f);

            ui.Hide();
            yield return WaitEditModeSeconds(0.15f);

            Assert.That(ui.IsVisible, Is.False);
            Assert.That(ui.messagesContainer.childCount, Is.EqualTo(0), "Hide must stop coroutine before late bubbles spawn");
            Assert.That(ui.typingIndicator.activeSelf, Is.False);
            Assert.That(ui.tapToContinueText.gameObject.activeSelf, Is.False);
            Assert.That(completed, Is.False, "External Hide should not auto-complete story flow");
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);

            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void VideoBackgroundPlayer_OnEnableWithoutSourceDoesNotLogFailure()
    {
        var root = new GameObject("VideoBackgroundPlayerNoSource", typeof(RectTransform));

        try
        {
            root.AddComponent<CanvasGroup>();
            root.AddComponent<RawImage>();
            root.AddComponent<VideoPlayer>();
            root.AddComponent<VideoBackgroundPlayer>();

            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void VideoBackgroundPlayer_CreateRenderTextureCreatesNativeTexture()
    {
        var root = new GameObject("VideoBackgroundPlayerRenderTexture", typeof(RectTransform));

        try
        {
            root.AddComponent<CanvasGroup>();
            root.AddComponent<RawImage>();
            root.AddComponent<VideoPlayer>();
            var player = root.AddComponent<VideoBackgroundPlayer>();

            InvokePrivate(player, "CreateRenderTexture", new Vector2Int(32, 32));

            var renderTexture = GetInstanceField<RenderTexture>(player, "_renderTexture");
            Assert.That(renderTexture, Is.Not.Null);
            Assert.That(renderTexture.IsCreated(), Is.True);

            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [UnityTest]
    public IEnumerator StoryStartLoadingScreen_ActivatesRootBeforeGifPlayback()
    {
        var host = new GameObject("StoryStartLoadingHost", typeof(RectTransform));
        GameData gameData = null;
        TextAsset gifAsset = null;
        Texture2D fallbackFrame = null;

        try
        {
            var screen = host.AddComponent<StoryStartLoadingScreen>();
            var loadingRoot = new GameObject("LoadingRoot", typeof(RectTransform), typeof(CanvasGroup));
            loadingRoot.transform.SetParent(host.transform, false);

            var coverObject = new GameObject("Cover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            coverObject.transform.SetParent(loadingRoot.transform, false);
            var coverImage = coverObject.GetComponent<Image>();

            var gifObject = new GameObject("Loading Cover GIF", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AnimatedGifPlayer));
            gifObject.transform.SetParent(coverObject.transform, false);
            var gifPlayer = gifObject.GetComponent<AnimatedGifPlayer>();

            fallbackFrame = new Texture2D(1, 1);
            fallbackFrame.SetPixel(0, 0, Color.white);
            fallbackFrame.Apply();
            SetInstanceField(gifPlayer, "frames", new List<Texture2D> { fallbackFrame });

            SetInstanceField(screen, "_root", loadingRoot);
            SetInstanceField(screen, "_canvasGroup", loadingRoot.GetComponent<CanvasGroup>());
            SetInstanceField(screen, "_coverImage", coverImage);
            SetInstanceField(screen, "_coverGifPlayer", gifPlayer);
            SetInstanceField(screen, "_hideOnAwake", false);
            SetInstanceField(screen, "_showDuration", 0f);
            SetInstanceField(screen, "_hideDuration", 0f);
            SetInstanceField(screen, "_minVisibleDuration", 0f);
            SetInstanceField(screen, "_finishToFullDuration", 0f);
            SetInstanceField(screen, "_completeHoldDuration", 0f);
            SetInstanceField(screen, "_preloadCoverTexture", false);
            SetInstanceField(screen, "_preloadStoryTextures", false);
            SetInstanceField(screen, "_preloadAudioData", false);

            loadingRoot.SetActive(false);

            gifAsset = new TextAsset("GIF89a\u0001\u0000\u0001\u0000\u0000\u0000\u0000;");
            gameData = ScriptableObject.CreateInstance<GameData>();
            SetInstanceField(gameData, "_gameIconGif", gifAsset);

            screen.Show(gameData, null);
            yield return null;

            Assert.That(loadingRoot.activeInHierarchy, Is.True);
            Assert.That(gifObject.activeInHierarchy, Is.True);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (gameData != null)
                Object.DestroyImmediate(gameData);

            if (gifAsset != null)
                Object.DestroyImmediate(gifAsset);

            if (fallbackFrame != null)
                Object.DestroyImmediate(fallbackFrame);

            Object.DestroyImmediate(host);
        }
    }

    [UnityTest]
    public IEnumerator ChapterLoadingScreen_HideImmediateHidesActivePanelWithoutCompleting()
    {
        var root = new GameObject("ChapterLoadingScreenRegression", typeof(RectTransform));
        var panel = new GameObject("LoadingPanel", typeof(RectTransform));

        try
        {
            panel.transform.SetParent(root.transform, false);
            panel.SetActive(false);

            var screen = root.AddComponent<ChapterLoadingScreen>();
            SetInstanceField(screen, "loadingPanel", panel);
            SetInstanceField(screen, "minDuration", 10f);
            SetInstanceField(screen, "maxDuration", 10f);

            bool completed = false;
            screen.Show("Chapter", () => completed = true);

            Assert.That(panel.activeSelf, Is.True);

            screen.HideImmediate();
            yield return null;

            Assert.That(panel.activeSelf, Is.False);
            Assert.That(completed, Is.False);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void MenuController_FindSceneObjectByNameIgnoresOtherScenes()
    {
        Scene controllerScene = EditorSceneManager.NewPreviewScene();
        Scene otherScene = EditorSceneManager.NewPreviewScene();
        GameObject controllerRoot = null;
        GameObject otherNavigation = null;
        GameObject localNavigation = null;

        try
        {
            controllerRoot = new GameObject("MenuControllerHost");
            SceneManager.MoveGameObjectToScene(controllerRoot, controllerScene);
            var controller = controllerRoot.AddComponent<MenuController>();

            otherNavigation = new GameObject("Navigation");
            SceneManager.MoveGameObjectToScene(otherNavigation, otherScene);

            Assert.That(InvokePrivate(controller, "FindSceneObjectByName", "Navigation"), Is.Null);

            localNavigation = new GameObject("Navigation");
            SceneManager.MoveGameObjectToScene(localNavigation, controllerScene);

            Assert.That(InvokePrivate(controller, "FindSceneObjectByName", "Navigation"), Is.SameAs(localNavigation));
        }
        finally
        {
            if (controllerRoot != null)
                Object.DestroyImmediate(controllerRoot);
            if (otherNavigation != null)
                Object.DestroyImmediate(otherNavigation);
            if (localNavigation != null)
                Object.DestroyImmediate(localNavigation);

            if (controllerScene.IsValid())
                EditorSceneManager.ClosePreviewScene(controllerScene);
            if (otherScene.IsValid())
                EditorSceneManager.ClosePreviewScene(otherScene);
        }
    }

    [Test]
    public void ShopController_CurrentShopScreenRefreshesSceneDefaultBalanceTexts()
    {
        GameObject root = null;

        try
        {
            ResetShopBalanceTestState();
            PlayerData.SetBalanceValues(52, 3);

            TMP_Text heartsText;
            TMP_Text candlesText;
            CreateShopBalanceController(out heartsText, out candlesText, out root);

            heartsText.text = "100";
            candlesText.text = "100";
            Assert.That(heartsText.text, Is.EqualTo("100"));
            Assert.That(candlesText.text, Is.EqualTo("100"));

            UIScreenState.SetCurrentScreen("Shop");

            Assert.That(heartsText.text, Is.EqualTo("52"));
            Assert.That(candlesText.text, Is.EqualTo("3"));
        }
        finally
        {
            if (root != null)
                Object.DestroyImmediate(root);

            ResetShopBalanceTestState();
        }
    }

    [Test]
    public void ShopController_PlayerDataBalanceChangedRefreshesShopTexts()
    {
        GameObject root = null;

        try
        {
            ResetShopBalanceTestState();

            TMP_Text heartsText;
            TMP_Text candlesText;
            CreateShopBalanceController(out heartsText, out candlesText, out root);

            PlayerData.SetBalanceValues(52, 3);

            Assert.That(heartsText.text, Is.EqualTo("52"));
            Assert.That(candlesText.text, Is.EqualTo("3"));
        }
        finally
        {
            if (root != null)
                Object.DestroyImmediate(root);

            ResetShopBalanceTestState();
        }
    }

    [Test]
    public void StoryAudio_SceneWithoutMusicKeepsCurrentMusic()
    {
        PlayModeStoryTestRig rig = null;
        StoryGraph graph = null;
        AudioClip music = null;

        try
        {
            rig = PlayModeStoryTestRig.Create();
            graph = ScriptableObject.CreateInstance<StoryGraph>();
            music = CreateTestAudioClip("story_music_a");

            var musicScene = AddAudioScene(graph, "black_music_scene", music);
            var emptyScene = AddAudioScene(graph, "black_empty_music_scene");

            InvokePrivate(rig.StoryManager, "ProcessScene", musicScene, false);
            Assert.That(rig.StoryManager.musicSource.clip, Is.SameAs(music));
            Assert.That(GetStoryCurrentMusic(rig.StoryManager), Is.SameAs(music));

            InvokePrivate(rig.StoryManager, "ProcessScene", emptyScene, false);

            Assert.That(rig.StoryManager.musicSource.clip, Is.SameAs(music));
            Assert.That(GetStoryCurrentMusic(rig.StoryManager), Is.SameAs(music));
        }
        finally
        {
            DestroyTestObject(music);
            DestroyTestObject(graph);
            rig?.Dispose();
        }
    }

    [Test]
    public void StoryAudio_StopMusicSceneClearsCurrentMusic()
    {
        PlayModeStoryTestRig rig = null;
        StoryGraph graph = null;
        AudioClip music = null;

        try
        {
            rig = PlayModeStoryTestRig.Create();
            graph = ScriptableObject.CreateInstance<StoryGraph>();
            music = CreateTestAudioClip("story_music_stop");

            var musicScene = AddAudioScene(graph, "black_music_scene", music);
            var stopScene = AddAudioScene(graph, "black_stop_music_scene", stopMusic: true);

            InvokePrivate(rig.StoryManager, "ProcessScene", musicScene, false);
            rig.StoryManager.musicSource.Stop();
            InvokePrivate(rig.StoryManager, "ProcessScene", stopScene, false);

            Assert.That(rig.StoryManager.musicSource.clip, Is.Null);
            Assert.That(GetStoryCurrentMusic(rig.StoryManager), Is.Null);
        }
        finally
        {
            DestroyTestObject(music);
            DestroyTestObject(graph);
            rig?.Dispose();
        }
    }

    [Test]
    public void StoryAudio_RepeatedFadeOutKeepsOriginalMusicVolume()
    {
        PlayModeStoryTestRig rig = null;
        AudioClip music = null;

        try
        {
            rig = PlayModeStoryTestRig.Create();
            music = CreateTestAudioClip("story_music_repeated_fade");

            SeedStoryMusicState(rig.StoryManager, music);
            rig.StoryManager.musicSource.volume = 0.2f;
            SetInstanceField(rig.StoryManager, "_storyMusicRestoreVolume", 0.45f);

            InvokePrivate(rig.StoryManager, "FadeOutStoryMusic", 0f);

            Assert.That(rig.StoryManager.musicSource.clip, Is.Null);
            Assert.That(rig.StoryManager.musicSource.volume, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(GetStoryCurrentMusic(rig.StoryManager), Is.Null);
        }
        finally
        {
            DestroyTestObject(music);
            rig?.Dispose();
        }
    }

    [Test]
    public void StoryAudio_StopSfxSceneRestoresSourceVolume()
    {
        PlayModeStoryTestRig rig = null;
        StoryGraph graph = null;
        AudioClip sfx = null;

        try
        {
            rig = PlayModeStoryTestRig.Create();
            graph = ScriptableObject.CreateInstance<StoryGraph>();
            sfx = CreateTestAudioClip("story_sfx_stop");

            rig.StoryManager.sfxSource.clip = sfx;
            rig.StoryManager.sfxSource.volume = 0.65f;

            var stopScene = AddAudioScene(graph, "black_stop_sfx_scene", stopSfx: true);
            InvokePrivate(rig.StoryManager, "ProcessScene", stopScene, false);

            Assert.That(rig.StoryManager.sfxSource.clip, Is.Null);
            Assert.That(rig.StoryManager.sfxSource.volume, Is.EqualTo(0.65f).Within(0.001f));
        }
        finally
        {
            DestroyTestObject(sfx);
            DestroyTestObject(graph);
            rig?.Dispose();
        }
    }

    [Test]
    public void StoryAudio_MainAndWardrobeScreensClearStoryMusic()
    {
        PlayModeStoryTestRig rig = null;
        AudioClip music = null;

        try
        {
            UIScreenState.SetCurrentScreen("");
            rig = PlayModeStoryTestRig.Create();
            music = CreateTestAudioClip("story_music_screen_boundary");

            SeedStoryMusicState(rig.StoryManager, music);
            UIScreenState.SetCurrentScreen("Story");
            UIScreenState.SetCurrentScreen("Wardrobe");
            Assert.That(rig.StoryManager.musicSource.clip, Is.Null);
            Assert.That(GetStoryCurrentMusic(rig.StoryManager), Is.Null);

            SeedStoryMusicState(rig.StoryManager, music);
            UIScreenState.SetCurrentScreen("Story");
            UIScreenState.SetCurrentScreen("MainScreen");
            Assert.That(rig.StoryManager.musicSource.clip, Is.Null);
            Assert.That(GetStoryCurrentMusic(rig.StoryManager), Is.Null);
        }
        finally
        {
            UIScreenState.SetCurrentScreen("");
            DestroyTestObject(music);
            rig?.Dispose();
        }
    }

    [Test]
    public void StoryCamera_NarrationLineDoesNotAutoPanToHeroSlot()
    {
        GameObject root = null;
        StoryGraph graph = null;
        CharacterData hero = null;
        Texture2D texture = null;
        Sprite sprite = null;

        try
        {
            CreateStoryCameraPanRig(out root, out var manager, out var camera);
            camera.PanToOffset(camera.rightOffset, 0f);
            float beforeOffset = camera.CurrentOffset;

            graph = ScriptableObject.CreateInstance<StoryGraph>();
            var node = graph.AddNode<DialogueNode>();
            hero = CreateRenderableCharacter("hero", out texture, out sprite);
            node.activeCharacters.Add(new DialogueCharacterEntry
            {
                character = hero,
                emotion = CharacterEmotionType.Idle,
                position = CharacterPosition.Left,
                speakerNameHint = "hero"
            });

            SetInstanceField(manager, "activeDialogueNode", node);
            var narrationLine = new DialogueLine { richText = "Earlier" };

            InvokePrivate(manager, "HandleNarrationLine", narrationLine);
            InvokePrivate(manager, "TryAutoPan", narrationLine);

            Assert.That(camera.CurrentOffset, Is.EqualTo(beforeOffset).Within(0.001f));
        }
        finally
        {
            DestroyTestObject(sprite);
            DestroyTestObject(texture);
            DestroyTestObject(hero);
            DestroyTestObject(graph);
            DestroyTestObject(root);
        }
    }

    [Test]
    public void StoryCamera_NonRenderableVoiceSpeakerDoesNotPanToActiveRightSlot()
    {
        GameObject root = null;
        StoryGraph graph = null;
        CharacterData voice = null;

        try
        {
            CreateStoryCameraPanRig(out root, out var manager, out var camera);
            camera.PanToOffset(camera.leftOffset, 0f);
            float beforeOffset = camera.CurrentOffset;

            graph = ScriptableObject.CreateInstance<StoryGraph>();
            var node = graph.AddNode<DialogueNode>();
            voice = CreateNonRenderableCharacter("voice");
            node.activeCharacters.Add(new DialogueCharacterEntry
            {
                character = voice,
                emotion = CharacterEmotionType.Idle,
                position = CharacterPosition.Right,
                speakerNameHint = "voice"
            });

            SetInstanceField(manager, "activeDialogueNode", node);
            InvokePrivate(manager, "TryAutoPan", new DialogueLine
            {
                speaker = voice,
                emotion = CharacterEmotionType.Idle,
                richText = "Voice line"
            });

            Assert.That(camera.CurrentOffset, Is.EqualTo(beforeOffset).Within(0.001f));
        }
        finally
        {
            DestroyTestObject(voice);
            DestroyTestObject(graph);
            DestroyTestObject(root);
        }
    }

    [Test]
    public void StoryCamera_RenderableSpeakerStillPansToActiveRightSlot()
    {
        GameObject root = null;
        StoryGraph graph = null;
        CharacterData speaker = null;
        Texture2D texture = null;
        Sprite sprite = null;

        try
        {
            CreateStoryCameraPanRig(out root, out var manager, out var camera);

            graph = ScriptableObject.CreateInstance<StoryGraph>();
            var node = graph.AddNode<DialogueNode>();
            speaker = CreateRenderableCharacter("visible_speaker", out texture, out sprite);
            node.activeCharacters.Add(new DialogueCharacterEntry
            {
                character = speaker,
                emotion = CharacterEmotionType.Idle,
                position = CharacterPosition.Right,
                speakerNameHint = "visible_speaker"
            });

            SetInstanceField(manager, "activeDialogueNode", node);
            InvokePrivate(manager, "TryAutoPan", new DialogueLine
            {
                speaker = speaker,
                emotion = CharacterEmotionType.Idle,
                richText = "Visible line"
            });

            Assert.That(camera.CurrentOffset, Is.EqualTo(camera.rightOffset).Within(0.001f));
        }
        finally
        {
            DestroyTestObject(sprite);
            DestroyTestObject(texture);
            DestroyTestObject(speaker);
            DestroyTestObject(graph);
            DestroyTestObject(root);
        }
    }

    [Test]
    public void GameScene_KeepsSpeakerAutoPanButDisablesNarrationHeroPan()
    {
        string scenePath = GameScenePaths.FirstOrDefault(File.Exists);
        Assert.That(scenePath, Is.Not.Null, "Game scene not found");

        string yaml = File.ReadAllText(scenePath);
        Assert.That(yaml, Does.Contain("autoPanToSpeaker: 1"));
        Assert.That(yaml, Does.Contain("panToHeroOnNarrationLines: 0"));
    }

    static IEnumerator WaitEditModeSeconds(float seconds)
    {
        var end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
            yield return null;
    }

    static ShopController CreateShopBalanceController(out TMP_Text heartsText, out TMP_Text candlesText, out GameObject root)
    {
        root = new GameObject("ShopBalanceSmoke");
        root.SetActive(false);

        var panel = CreateStandaloneRect("ShopPanel", root.transform).gameObject;
        panel.AddComponent<CanvasGroup>();

        heartsText = CreateStandaloneText("HeartsBodyText", panel.transform);
        candlesText = CreateStandaloneText("CandlesBodyText", panel.transform);
        heartsText.text = "100";
        candlesText.text = "100";

        var controller = root.AddComponent<ShopController>();
        controller.panel = panel;
        controller.heartsBalanceText = heartsText;
        controller.candlesBalanceText = candlesText;

        root.SetActive(true);
        return controller;
    }

    static void CreateStoryCameraPanRig(out GameObject root, out StoryManager manager, out CameraController camera)
    {
        root = new GameObject("StoryCameraPanSmoke");
        root.SetActive(false);

        RectTransform viewport = CreateStandaloneRect("Viewport", root.transform);
        viewport.sizeDelta = new Vector2(1000f, 1000f);

        RectTransform cameraRoot = CreateStandaloneRect("Background", viewport);
        cameraRoot.sizeDelta = new Vector2(2400f, 1000f);

        manager = root.AddComponent<StoryManager>();
        camera = root.AddComponent<CameraController>();
        camera.cameraRoot = cameraRoot;
        camera.leftOffset = 460f;
        camera.centerOffset = 0f;
        camera.rightOffset = -460f;
        camera.maxOffsetX = 1200f;
        camera.panDuration = 0f;
        InvokePrivate(camera, "CaptureRootBasePositions");

        manager.cameraController = camera;
        manager.autoPanToSpeaker = true;
    }

    static CharacterData CreateRenderableCharacter(string name, out Texture2D texture, out Sprite sprite)
    {
        texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));

        var character = ScriptableObject.CreateInstance<CharacterData>();
        character.name = name;
        character.characterName = name;
        character.defaultSprite = sprite;
        return character;
    }

    static CharacterData CreateNonRenderableCharacter(string name)
    {
        var character = ScriptableObject.CreateInstance<CharacterData>();
        character.name = name;
        character.characterName = name;
        return character;
    }

    static void DestroyTestObject(UnityEngine.Object target)
    {
        if (target != null)
            Object.DestroyImmediate(target);
    }

    static void ResetShopBalanceTestState()
    {
        UIScreenState.SetCurrentScreen("");
        UIScreenState.ClearSelectedScreen();
        ShopController.Instance = null;
        PlayerData.SetBalanceValues(0, 0);
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }

    static AudioClip CreateTestAudioClip(string name)
    {
        return AudioClip.Create(name, 128, 1, 44100, false);
    }

    static SceneSetupNode AddAudioScene(
        StoryGraph graph,
        string guid,
        AudioClip music = null,
        bool stopMusic = false,
        bool stopSfx = false,
        AudioClip startSfx = null)
    {
        Assert.That(graph, Is.Not.Null);

        var scene = graph.AddNode<SceneSetupNode>();
        scene.guid = guid;
        scene.sceneData = ScriptableObject.CreateInstance<SceneSetupData>();
        scene.sceneData.music = music;
        scene.sceneData.stopMusic = stopMusic;
        scene.sceneData.stopSfx = stopSfx;
        scene.sceneData.startSfx = startSfx;
        return scene;
    }

    static AudioClip GetStoryCurrentMusic(StoryManager manager)
    {
        return GetInstanceField<AudioClip>(manager, "currentMusic");
    }

    static void SeedStoryMusicState(StoryManager manager, AudioClip music)
    {
        Assert.That(manager, Is.Not.Null);
        manager.musicSource.clip = music;
        manager.musicSource.volume = 0.45f;
        SetInstanceField(manager, "currentMusic", music);
    }

    static void ExpectMissingCharacterLog(string characterId)
    {
        LogAssert.Expect(
            LogType.Error,
            new Regex(@"\[StoryJson\] Character '" + Regex.Escape(characterId) + @"' was not found\."));
    }

    static class PlayModeStoryFactory
    {
        public static StoryData CreateStory(string storyId, string seasonId, params ChapterData[] chapters)
        {
            var story = ScriptableObject.CreateInstance<StoryData>();
            story.name = "Story_" + storyId;
            story.Configure(storyId, storyId, chapters ?? System.Array.Empty<ChapterData>());
            return story;
        }

        public static ChapterData CreateChapter(string chapterId, string chapterName, StoryGraph graph, bool isPremium = false, int unlockCost = 0)
        {
            var chapter = ScriptableObject.CreateInstance<ChapterData>();
            chapter.name = "Chapter_" + chapterId;
            chapter.Configure(chapterId, chapterName, graph, isPremium, unlockCost);
            return chapter;
        }

        public static StoryGraph CreateDialogueGraph(string episodeId, string sceneLabel, string dialogueGuid, params string[] lines)
        {
            var graph = ScriptableObject.CreateInstance<StoryGraph>();
            graph.name = "Graph_" + episodeId;
            graph.episodeId = episodeId;

            var start = graph.AddNode<StartNode>();
            start.guid = episodeId + ":start";

            var scene = graph.AddNode<SceneSetupNode>();
            scene.guid = episodeId + ":scene";
            scene.sceneLabel = sceneLabel;
            scene.sceneData = ScriptableObject.CreateInstance<SceneSetupData>();

            var dialogue = graph.AddNode<DialogueNode>();
            dialogue.guid = dialogueGuid;
            dialogue.lines = new List<DialogueLine>();

            if (lines != null)
            {
                foreach (var line in lines)
                {
                    dialogue.lines.Add(new DialogueLine
                    {
                        richText = line ?? ""
                    });
                }
            }

            Connect(start, "exit", scene, "enter");
            Connect(scene, "exit", dialogue, "enter");

            return graph;
        }

        static void Connect(BaseStoryNode from, string outputPortName, BaseStoryNode to, string inputPortName)
        {
            var outputPort = from.GetOutputPort(outputPortName);
            var inputPort = to.GetInputPort(inputPortName);
            if (outputPort != null && inputPort != null)
                outputPort.Connect(inputPort);
        }
    }

    static TMP_Text CreateStandaloneText(string name, Transform parent)
    {
        var go = CreateStandaloneRect(name, parent).gameObject;
        return go.AddComponent<TextMeshProUGUI>();
    }

    static Button CreateStandaloneButton(string name, Transform parent, string label)
    {
        var go = CreateStandaloneRect(name, parent).gameObject;
        go.AddComponent<Image>();
        var button = go.AddComponent<Button>();
        var text = CreateStandaloneText(name + "Label", go.transform);
        text.text = label;
        return button;
    }

    static RectTransform CreateStandaloneRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static GameObject CreatePhoneBubblePrefab(string name, Transform parent)
    {
        var bubble = CreateStandaloneRect(name, parent).gameObject;
        bubble.AddComponent<CanvasGroup>();
        CreateStandaloneText(name + "_Text", bubble.transform);
        return bubble;
    }

    static class PlayModeTestState
    {
        public static void ResetAll()
        {
            StopRuntimeCoroutines();
            ClearSaveFiles();
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            PrototypeFeatureFlags.SetLocalPremiumSpendEnabled(true);
            PrototypeFeatureFlags.SetRemoteEpisodeGraphsEnabled(true);

            PlayerData.SetCandlesValue(0);
            PlayerData.SetHeartsValue(0);

            StoryManager.Instance = null;
            GameState.Instance = null;
            SaveManager.Instance = null;
            StoryHistory.Instance = null;
            SubscriptionManager.Instance = null;
            PlayerAppearance.Instance = null;
            SetAutoProperty<ToastManager>(typeof(ToastManager), "Instance", null);

            ClearDotweenState();

            ClearRemoteImporterCache();
            ClearNetworkState();
        }

        static void StopRuntimeCoroutines()
        {
            foreach (var manager in Resources.FindObjectsOfTypeAll<NetworkManager>())
            {
                if (manager != null)
                    manager.StopAllCoroutines();
            }

            foreach (var manager in Resources.FindObjectsOfTypeAll<StoryManager>())
            {
                if (manager != null)
                    manager.StopAllCoroutines();
            }
        }

        public static void ClearDotweenState()
        {
            if (DG.Tweening.DOTween.instance != null)
            {
                DG.Tweening.DOTween.KillAll();
                DG.Tweening.DOTween.Clear(true);
            }

            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.name == "[DOTween]")
                    Object.DestroyImmediate(go);
            }
        }

        public static void RegisterDotweenExitCleanup()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode &&
                state != PlayModeStateChange.EnteredEditMode)
                return;

            ClearDotweenState();
            EditorApplication.delayCall += ClearDotweenState;

            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        public static void SeedCatalog(params CatalogEpisodeResponse[] episodes)
        {
            var seasons = GetField<List<CatalogSeasonResponse>>(typeof(NetworkManager), "_catalogSeasons");
            var map = GetField<Dictionary<string, CatalogEpisodeResponse>>(typeof(NetworkManager), "_catalogEpisodes");

            seasons.Clear();
            map.Clear();

            var season = new CatalogSeasonResponse
            {
                seasonId = "playmode_story_s01",
                title = "PlayMode Season",
                order = 1,
                episodes = new List<CatalogEpisodeResponse>()
            };

            if (episodes != null)
            {
                foreach (var episode in episodes)
                {
                    if (episode == null || string.IsNullOrEmpty(episode.episodeId))
                        continue;

                    season.episodes.Add(episode);
                    map[episode.episodeId] = episode;
                }
            }

            seasons.Add(season);
        }

        static void ClearSaveFiles()
        {
            if (!Directory.Exists(Application.persistentDataPath))
                return;

            foreach (var path in Directory.GetFiles(Application.persistentDataPath, "save_*.json"))
                File.Delete(path);
        }

        static void ClearRemoteImporterCache()
        {
            var field = typeof(RemoteStoryGraphImporter).GetField("ImportedGraphs", BindingFlags.Static | BindingFlags.NonPublic);
            var cache = field != null ? field.GetValue(null) as IDictionary : null;
            cache?.Clear();
        }

        public static void ClearNetworkState()
        {
            GetField<List<string>>(typeof(NetworkManager), "_lastUnlockedEpisodes").Clear();
            GetField<Dictionary<string, int>>(typeof(NetworkManager), "_lastProgressStats").Clear();
            GetField<Dictionary<string, bool>>(typeof(NetworkManager), "_lastProgressFlags").Clear();
            GetField<List<CatalogSeasonResponse>>(typeof(NetworkManager), "_catalogSeasons").Clear();
            GetField<Dictionary<string, CatalogEpisodeResponse>>(typeof(NetworkManager), "_catalogEpisodes").Clear();
            GetField<Dictionary<string, PendingProgressPayload>>(typeof(NetworkManager), "_pendingProgress").Clear();
            GetField<Dictionary<string, PendingBookmarkPayload>>(typeof(NetworkManager), "_pendingBookmarks").Clear();

            SetAutoProperty(typeof(NetworkManager), "IsOnline", false);
            SetAutoProperty(typeof(NetworkManager), "IsAuthenticated", false);
            SetAutoProperty(typeof(NetworkManager), "LastNetworkError", "");
            SetAutoProperty(typeof(NetworkManager), "LastErrorKind", NetworkErrorKind.Success);
            SetAutoProperty(typeof(NetworkManager), "LastProgressNodeGuid", "");
            SetAutoProperty(typeof(NetworkManager), "LastProgressEpisodeId", "");
            SetAutoProperty(typeof(NetworkManager), "LastProgressSnapshotJson", "");
            SetAutoProperty(typeof(NetworkManager), "LastProgressRawJson", "");
            SetAutoProperty(typeof(NetworkManager), "LastProgressUpdatedAtIso", "");
            SetAutoProperty(typeof(NetworkManager), "FastForwardEnabled", false);
            SetAutoProperty(typeof(NetworkManager), "FastForwardSteps", 5);
            SetAutoProperty(typeof(NetworkManager), "BookmarksEnabled", false);
            SetAutoProperty(typeof(NetworkManager), "BookmarkCapacity", 30);

            SetStaticField<string>(typeof(NetworkManager), "_authToken", null);
            SetStaticField<string>(typeof(NetworkManager), "_refreshToken", null);
            SetStaticField<string>(typeof(NetworkManager), "_playerId", null);
            SetStaticField<NetworkManager>(typeof(NetworkManager), "Instance", null);
            SetStaticField<bool>(typeof(NetworkManager), "_serverBookmarkLocked", false);

            NetworkManager.CurrentProfile.playerId = "";
            NetworkManager.CurrentProfile.isNew = false;
            NetworkManager.CurrentProfile.locale = "";
            NetworkManager.CurrentProfile.platform = "";
            NetworkManager.CurrentProfile.createdAt = "";
            NetworkManager.CurrentProfile.heroName = "";
            NetworkManager.LastBalance.candles = 0;
            NetworkManager.LastBalance.hearts = 0;
            NetworkManager.LastBalance.candlesCap = 0;
            NetworkManager.LastBalance.dailyStreakDay = 0;
            NetworkManager.LastBalance.nextCandleAt = "";
            NetworkManager.LastBalance.updatedAtIso = "";

            PlayerPrefs.DeleteKey("VN_PENDING_PROGRESS_INDEX");
            PlayerPrefs.DeleteKey("VN_PENDING_BOOKMARK_INDEX");
            PlayerPrefs.DeleteKey("VN_AUTH_TOKEN");
            PlayerPrefs.DeleteKey("VN_REFRESH_TOKEN");
            PlayerPrefs.DeleteKey("VN_REFRESH_TOKEN_V2");
            PlayerPrefs.DeleteKey("VN_PLAYER_ID");
        }

        static T GetField<T>(System.Type type, string fieldName) where T : class
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            return field != null ? field.GetValue(null) as T : null;
        }

        static void SetAutoProperty<T>(System.Type type, string propertyName, T value)
        {
            SetStaticField(type, "<" + propertyName + ">k__BackingField", value);
        }

        static void SetStaticField<T>(System.Type type, string fieldName, T value)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            field?.SetValue(null, value);
        }
    }

    [DefaultExecutionOrder(int.MaxValue)]
    sealed class DotweenSceneCleanupGuard : MonoBehaviour
    {
        void OnDestroy()
        {
            PlayModeTestState.ClearDotweenState();
        }
    }

    static IEnumerable<T> LoadAssets<T>(string filter) where T : Object
    {
        string[] existingRoots = DataRoots.Where(AssetDatabase.IsValidFolder).ToArray();
        Assert.That(existingRoots, Is.Not.Empty, "No project data folders found: " + string.Join(", ", DataRoots));

        return AssetDatabase.FindAssets(filter, existingRoots)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null);
    }

    static bool IsMutableCollectionType(System.Type type)
    {
        if (type == null || type == typeof(string))
            return false;

        if (typeof(IList).IsAssignableFrom(type))
            return true;

        if (!type.IsGenericType)
            return false;

        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(List<>) ||
               definition == typeof(Dictionary<,>) ||
               definition == typeof(HashSet<>);
    }
}
