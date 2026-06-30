using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class PlayerNameResolutionTests
{
    const string StoryId = "player_name_resolution_zls";
    const string ChapterId = "zls_1";
    const string DefaultName = "\u0410\u043b\u0438\u0441\u0430";

    GameObject _managerObject;
    StoryManager _storyManager;
    StoryGraph _graph;
    ChapterData _chapter;
    StoryData _storyData;

    [SetUp]
    public void SetUp()
    {
        HeroCustomizationStore.DeleteStoredState();
        HeroCustomizationStore.DeletePlayerNameForStory(StoryId);
        PlayerAppearance.ApplyState(new HeroCustomizationState(), save: false, notify: false);
        StoryManager.Instance = null;

        _graph = ScriptableObject.CreateInstance<StoryGraph>();
        _graph.name = "PlayerNameResolutionGraph";
        _graph.defaultPlayerName = DefaultName;
        _graph.episodeId = ChapterId;

        _chapter = ScriptableObject.CreateInstance<ChapterData>();
        _chapter.Configure(ChapterId, "ZLS 1", _graph, false, 0);

        _storyData = ScriptableObject.CreateInstance<StoryData>();
        _storyData.Configure(StoryId, "ZLS", new[] { _chapter });

        _managerObject = new GameObject("PlayerNameResolutionStoryManager");
        _storyManager = _managerObject.AddComponent<StoryManager>();
        _storyManager.storyData = _storyData;
        _storyManager.storyGraph = _graph;
        StoryManager.Instance = _storyManager;
    }

    [TearDown]
    public void TearDown()
    {
        HeroCustomizationStore.DeleteStoredState();
        HeroCustomizationStore.DeletePlayerNameForStory(StoryId);
        PlayerAppearance.ApplyState(new HeroCustomizationState(), save: false, notify: false);

        if (_managerObject != null)
            Object.DestroyImmediate(_managerObject);
        if (_storyData != null)
            Object.DestroyImmediate(_storyData);
        if (_chapter != null)
            Object.DestroyImmediate(_chapter);
        if (_graph != null)
            Object.DestroyImmediate(_graph);

        StoryManager.Instance = null;
    }

    [Test]
    public void DialogueResolver_UsesStoryDefaultName_ForZlsPlayerNameToken()
    {
        string resolved = DialogueVariableResolver.ResolveText(
            "{playerName} \u043e\u0441\u043c\u0430\u0442\u0440\u0438\u0432\u0430\u0435\u0442\u0441\u044f",
            DialogueVariableContext.StoryUi(nameof(PlayerNameResolutionTests), null, StoryId, ChapterId));

        Assert.That(resolved, Is.EqualTo(DefaultName + " \u043e\u0441\u043c\u0430\u0442\u0440\u0438\u0432\u0430\u0435\u0442\u0441\u044f"));
        Assert.That(DialogueVariableResolver.FallbackPlayerName, Is.EqualTo(HeroCustomizationStore.DefaultPlayerName));
    }

    [Test]
    public void DialogueResolver_DoesNotDeleteStoryScopedDefaultName()
    {
        HeroCustomizationStore.SavePlayerNameForStory(StoryId, DefaultName);

        string resolved = DialogueVariableResolver.ResolveText(
            "{playerName}",
            DialogueVariableContext.StoryUi(nameof(PlayerNameResolutionTests), null, StoryId, ChapterId));

        Assert.That(resolved, Is.EqualTo(DefaultName));
        Assert.That(HeroCustomizationStore.TryLoadPlayerNameForStory(StoryId, out string storedName), Is.True);
        Assert.That(storedName, Is.EqualTo(DefaultName));
    }

    [Test]
    public void DialogueResolver_UsesSavedProfileBeforeStoryDefaultName()
    {
        CharacterProfileService.SaveSelectedPlayerName("\u0414\u0430\u0440\u0438\u043d\u0430", "", nameof(PlayerNameResolutionTests));

        string resolved = DialogueVariableResolver.ResolveText(
            "{playerName}",
            DialogueVariableContext.StoryUi(nameof(PlayerNameResolutionTests), null, StoryId, ChapterId));

        Assert.That(resolved, Is.EqualTo("\u0414\u0430\u0440\u0438\u043d\u0430"));
    }

    [Test]
    public void CharacterProfileService_SaveSelectedName_PersistsStoryAndGlobalName()
    {
        string saved = CharacterProfileService.SaveSelectedPlayerName(
            "\u0414\u0430\u0440\u0438\u043d\u0430",
            StoryId,
            nameof(PlayerNameResolutionTests));

        Assert.That(saved, Is.EqualTo("\u0414\u0430\u0440\u0438\u043d\u0430"));
        Assert.That(HeroCustomizationStore.TryLoadPlayerNameForStory(StoryId, out string storyName), Is.True);
        Assert.That(storyName, Is.EqualTo("\u0414\u0430\u0440\u0438\u043d\u0430"));
        Assert.That(HeroCustomizationStore.Load().playerName, Is.EqualTo("\u0414\u0430\u0440\u0438\u043d\u0430"));
        Assert.That(PlayerAppearance.PlayerName, Is.EqualTo("\u0414\u0430\u0440\u0438\u043d\u0430"));
    }

    [Test]
    public void DialogueResolver_ResolvesPlayerNameCases()
    {
        string resolved = DialogueVariableResolver.ResolveText(
            "{playerName}|{playerName:gen}|{playerName:dat}|{playerName:acc}|{playerName:ins}|{playerName:prep}|[player_name:\u0432\u0438\u043d]",
            DialogueVariableContext.StoryUi(nameof(PlayerNameResolutionTests), null, StoryId, ChapterId));

        Assert.That(
            resolved,
            Is.EqualTo("\u0410\u043b\u0438\u0441\u0430|\u0410\u043b\u0438\u0441\u044b|\u0410\u043b\u0438\u0441\u0435|\u0410\u043b\u0438\u0441\u0443|\u0410\u043b\u0438\u0441\u043e\u0439|\u0410\u043b\u0438\u0441\u0435|\u0410\u043b\u0438\u0441\u0443"));
    }

    [Test]
    public void DialogueResolver_UnknownCaseFallsBackToNominative()
    {
        string resolved = DialogueVariableResolver.ResolveText(
            "\u043e\u043d \u0437\u0430\u043c\u0435\u0447\u0430\u0435\u0442 {playerName:unknown}",
            DialogueVariableContext.StoryUi(nameof(PlayerNameResolutionTests), null, StoryId, ChapterId));

        Assert.That(resolved, Is.EqualTo("\u043e\u043d \u0437\u0430\u043c\u0435\u0447\u0430\u0435\u0442 " + DefaultName));
    }

    [Test]
    public void PlayerNameInflector_InflectsSafeRussianFemaleNamesOnly()
    {
        Assert.That(PlayerNameInflector.Resolve("\u0410\u043b\u0438\u0441\u0430", PlayerNameCase.Accusative), Is.EqualTo("\u0410\u043b\u0438\u0441\u0443"));
        Assert.That(PlayerNameInflector.Resolve("\u041c\u0430\u0448\u0430", PlayerNameCase.Genitive), Is.EqualTo("\u041c\u0430\u0448\u0438"));
        Assert.That(PlayerNameInflector.Resolve("\u041c\u0430\u0440\u0438\u044f", PlayerNameCase.Accusative), Is.EqualTo("\u041c\u0430\u0440\u0438\u044e"));
        Assert.That(PlayerNameInflector.Resolve("\u0410\u043d\u043d\u0430 \u041c\u0430\u0440\u0438\u044f", PlayerNameCase.Accusative), Is.EqualTo("\u0410\u043d\u043d\u0430 \u041c\u0430\u0440\u0438\u044e"));
        Assert.That(PlayerNameInflector.Resolve("\u0410\u043d\u043d\u0430-\u0421\u043e\u0444\u0438\u044f", PlayerNameCase.Accusative), Is.EqualTo("\u0410\u043d\u043d\u0430-\u0421\u043e\u0444\u0438\u044e"));

        Assert.That(PlayerNameInflector.Resolve("\u042d\u043b\u0438\u0441\u043e\u043d", PlayerNameCase.Accusative), Is.EqualTo("\u042d\u043b\u0438\u0441\u043e\u043d"));
        Assert.That(PlayerNameInflector.Resolve("John", PlayerNameCase.Accusative), Is.EqualTo("John"));
        Assert.That(PlayerNameInflector.Resolve("123", PlayerNameCase.Accusative), Is.EqualTo("123"));
    }

    [Test]
    public void StoryDefaultCaseOverrides_DoNotAffectCustomPlayerName()
    {
        _graph.defaultPlayerNameCases.acc = "\u0410\u043b\u0438\u0441\u043e\u0447\u043a\u0443";

        string defaultResolved = DialogueVariableResolver.ResolveText(
            "\u043e\u043d \u0437\u0430\u043c\u0435\u0447\u0430\u0435\u0442 {playerName:acc}",
            DialogueVariableContext.StoryUi(nameof(PlayerNameResolutionTests), null, StoryId, ChapterId));
        Assert.That(defaultResolved, Is.EqualTo("\u043e\u043d \u0437\u0430\u043c\u0435\u0447\u0430\u0435\u0442 \u0410\u043b\u0438\u0441\u043e\u0447\u043a\u0443"));

        HeroCustomizationStore.SavePlayerNameForStory(StoryId, "\u041c\u0430\u0440\u0438\u044f");
        string customResolved = DialogueVariableResolver.ResolveText(
            "\u043e\u043d \u0437\u0430\u043c\u0435\u0447\u0430\u0435\u0442 {playerName:acc}",
            DialogueVariableContext.StoryUi(nameof(PlayerNameResolutionTests), null, StoryId, ChapterId));

        Assert.That(customResolved, Is.EqualTo("\u043e\u043d \u0437\u0430\u043c\u0435\u0447\u0430\u0435\u0442 \u041c\u0430\u0440\u0438\u044e"));
    }

    [Test]
    public void StoryJson_DefaultPlayerNameCases_RoundTripThroughGraph()
    {
        string json =
            "{\"version\":1,\"chapterId\":\"case_chapter\",\"episodeId\":\"case_ep\"," +
            "\"defaultPlayerName\":\"\u0410\u043b\u0438\u0441\u0430\",\"defaultPlayerNameCases\":{\"acc\":\"\u0410\u043b\u0438\u0441\u043e\u0447\u043a\u0443\"}," +
            "\"nodes\":[{\"id\":\"start\",\"type\":\"start\",\"next\":\"dialogue_1\"},{\"id\":\"dialogue_1\",\"type\":\"dialogue\",\"lines\":[{\"text\":\"x\"}]}]}";

        StoryGraph graph = null;
        try
        {
            Assert.That(StoryJsonConverter.TryBuildGraph(json, "fallback", out graph, out string reason), Is.True, reason);
            Assert.That(graph.defaultPlayerName, Is.EqualTo(DefaultName));
            Assert.That(graph.defaultPlayerNameCases.acc, Is.EqualTo("\u0410\u043b\u0438\u0441\u043e\u0447\u043a\u0443"));

            Assert.That(StoryJsonConverter.TryExportGraph(graph, out string exported, out reason), Is.True, reason);
            Assert.That(StoryJsonConverter.TryParseDocument(exported, out StoryJsonDocument document, out reason), Is.True, reason);
            Assert.That(document.defaultPlayerNameCases.acc, Is.EqualTo("\u0410\u043b\u0438\u0441\u043e\u0447\u043a\u0443"));
        }
        finally
        {
            if (graph != null)
                Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void SaveManager_StoresStoryDefaultName_WhenRuntimeNameIsGeneric()
    {
        string playerName = InvokeStatic<string>(
            typeof(SaveManager),
            "ResolvePlayerNameForSave",
            StoryId,
            HeroCustomizationStore.DefaultPlayerName,
            _storyManager);

        Assert.That(playerName, Is.EqualTo(DefaultName));
    }

    [Test]
    public void StoryRestore_UsesStoryDefaultName_ForOldSnapshotWithEmptyPlayerName()
    {
        var snapshot = new SaveData
        {
            storyId = StoryId,
            episodeId = ChapterId,
            currentNodeGuid = "node_a",
            playerName = ""
        };

        string restoredName = InvokeInstance<string>(
            _storyManager,
            "ResolveRestoredPlayerName",
            snapshot,
            _graph,
            HeroCustomizationStore.DefaultPlayerName);

        Assert.That(restoredName, Is.EqualTo(DefaultName));
        Assert.That(_storyManager.ResolveStoryPlayerNameForSaveFallback(""), Is.EqualTo(DefaultName));
    }

    [Test]
    public void StoryRestore_UsesSavedProfileBeforeStoryDefaultName()
    {
        CharacterProfileService.SaveSelectedPlayerName("\u0414\u0430\u0440\u0438\u043d\u0430", "", nameof(PlayerNameResolutionTests));
        var snapshot = new SaveData
        {
            storyId = StoryId,
            episodeId = ChapterId,
            currentNodeGuid = "node_a",
            playerName = ""
        };

        string restoredName = InvokeInstance<string>(
            _storyManager,
            "ResolveRestoredPlayerName",
            snapshot,
            _graph,
            HeroCustomizationStore.DefaultPlayerName);

        Assert.That(restoredName, Is.EqualTo("\u0414\u0430\u0440\u0438\u043d\u0430"));
    }

    [Test]
    public void NetworkSnapshots_DoNotStripStoryDefaultName()
    {
        var outgoing = new SaveData
        {
            storyId = StoryId,
            episodeId = ChapterId,
            currentNodeGuid = "node_a",
            playerName = DefaultName,
            currency = 10,
            hearts = 5
        };

        SaveData serverSafe = InvokeStatic<SaveData>(
            typeof(NetworkManager),
            "CreateServerSafeSnapshot",
            outgoing);

        Assert.That(serverSafe.playerName, Is.EqualTo(DefaultName));

        var incoming = new SaveData
        {
            storyId = StoryId,
            episodeId = ChapterId,
            currentNodeGuid = "node_a",
            playerName = DefaultName
        };

        InvokeStatic<object>(
            typeof(NetworkManager),
            "SanitizeIncomingServerSnapshot",
            incoming);

        Assert.That(incoming.playerName, Is.EqualTo(DefaultName));
    }

    [Test]
    public void StoryJson_PlayerNameCaseAuthoringAudit_WarnsOnly()
    {
        string root = Path.Combine("Assets", "_MyProject", "Data", "Stories");
        if (!Directory.Exists(root))
            return;

        PlayerNameCaseAuditPattern[] suspiciousPatterns = BuildPlayerNameCaseAuditPatterns();

        foreach (string path in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            foreach (PlayerNameCaseAuditPattern pattern in suspiciousPatterns)
            {
                foreach (Match match in pattern.Regex.Matches(text))
                {
                    Debug.LogWarning(
                        "[AUTHORING][PLAYER_NAME_CASE] Suspicious nominative {playerName}; consider " +
                        pattern.SuggestedToken + " (" + pattern.Reason + "). " +
                        path + " :: " + match.Value);
                }
            }
        }
    }

    static PlayerNameCaseAuditPattern[] BuildPlayerNameCaseAuditPatterns()
    {
        return new[]
        {
            CaseAudit(
                "acc",
                "direct object after transitive verb",
                TriggerBeforePlayerNamePattern(
                    "\u0437\u0430\u043c\u0435\u0447\u0430\\w*",
                    "\u0443\u0432\u0438\u0434\\w*",
                    "\u0432\u0438\u0434\\w*",
                    "\u0440\u0430\u0441\u0441\u043c\u0430\u0442\u0440\u0438\u0432\u0430\\w*",
                    "\u0440\u0430\u0437\u0433\u043b\u044f\u0434\\w*",
                    "\u043e\u0441\u043c\u0430\u0442\u0440\u0438\u0432\u0430\\w*",
                    "\u0432\u0441\u0442\u0440\u0435\u0447\u0430\\w*",
                    "\u043e\u0431\u043d\u0438\u043c\u0430\\w*",
                    "\u0446\u0435\u043b\u0443\\w*",
                    "\u0434\u0435\u0440\u0436\\w*",
                    "\u0445\u0432\u0430\u0442\\w*",
                    "\u0442\u0440\u043e\u0433\\w*",
                    "\u043e\u043a\u043b\u0438\u043a\\w*",
                    "\u0437\u043e\u0432\\w*",
                    "\u0431\u0443\u0434\\w*",
                    "\u043f\u0443\u0433\u0430\\w*",
                    "\u0443\u0441\u043f\u043e\u043a\u0430\u0438\u0432\u0430\\w*",
                    "\u043f\u0440\u043e\u0432\u043e\u0436\u0430\\w*",
                    "\u0442\u043e\u043b\u043a\u0430\\w*",
                    "\u043f\u043e\u0434\u0442\u0430\u043b\u043a\u0438\u0432\u0430\\w*",
                    "\u043e\u0441\u0442\u0430\u043d\u0430\u0432\u043b\u0438\u0432\u0430\\w*",
                    "\u043f\u0440\u0438\u0436\u0438\u043c\u0430\\w*")),
            CaseAudit(
                "acc",
                "accusative preposition or gaze direction",
                PhraseBeforePlayerNamePattern(
                    "\u0441\u043a\u0432\u043e\u0437\u044c",
                    "\u0447\u0435\u0440\u0435\u0437",
                    "\u043f\u0440\u043e",
                    "\u0441\u043c\u043e\u0442\u0440\\w*\\s+\u043d\u0430",
                    "\u0433\u043b\u044f\u0434\\w*\\s+\u043d\u0430",
                    "\u043f\u043e\u0441\u043c\u0430\u0442\u0440\u0438\u0432\u0430\\w*\\s+\u043d\u0430")),
            CaseAudit(
                "gen",
                "genitive preposition",
                PhraseBeforePlayerNamePattern(
                    "\u0431\u0435\u0437",
                    "\u0434\u043b\u044f",
                    "\u0434\u043e",
                    "\u043e\u0442",
                    "\u0443",
                    "\u043e\u043a\u043e\u043b\u043e",
                    "\u0432\u043e\u0437\u043b\u0435",
                    "\u043c\u0438\u043c\u043e",
                    "\u043f\u043e\u0441\u043b\u0435",
                    "\u043a\u0440\u043e\u043c\u0435",
                    "\u0432\u043c\u0435\u0441\u0442\u043e",
                    "\u043f\u0440\u043e\u0442\u0438\u0432",
                    "\u0441\u0440\u0435\u0434\u0438",
                    "\u0432\u0434\u043e\u043b\u044c",
                    "\u0438\u0437-\u0437\u0430",
                    "\u0438\u0437-\u043f\u043e\u0434")),
            CaseAudit(
                "gen",
                "genitive-controlled verb",
                TriggerBeforePlayerNamePattern(
                    "\u043a\u0430\u0441\u0430\\w*",
                    "\u0431\u043e\u0438\\w*",
                    "\u0438\u0437\u0431\u0435\u0433\u0430\\w*",
                    "\u0441\u0442\u043e\u0440\u043e\u043d\u0438\\w*",
                    "\u043b\u0438\u0448\u0430\\w*")),
            CaseAudit(
                "dat",
                "dative recipient or addressee",
                TriggerBeforePlayerNamePattern(
                    "\u0434\u0430[\u0435\u0451]\\w*",
                    "\u0434\u0430\u0432\u0430\\w*",
                    "\u0434\u0430\u0440\\w*",
                    "\u043f\u0440\u0438\u043d\u043e\u0441\u0438\\w*",
                    "\u043f\u043e\u0434\u0430[\u0435\u0451]\\w*",
                    "\u043f\u0435\u0440\u0435\u0434\u0430[\u0435\u0451]\\w*",
                    "\u043f\u0440\u043e\u0442\u044f\u0433\u0438\u0432\u0430\\w*",
                    "\u043f\u043e\u043a\u0430\u0437\u044b\u0432\u0430\\w*",
                    "\u043e\u0431\u044a\u044f\u0441\u043d\u044f\\w*",
                    "\u0440\u0430\u0441\u0441\u043a\u0430\u0437\u044b\u0432\u0430\\w*",
                    "\u043e\u0442\u0432\u0435\u0447\u0430\\w*",
                    "\u0448\u0435\u043f\u0447\\w*",
                    "\u043f\u043e\u043c\u043e\u0433\u0430\\w*",
                    "\u0443\u043b\u044b\u0431\u0430\\w*\u0441\u044f",
                    "\u043a\u0438\u0432\u0430\\w*",
                    "\u043c\u0430\u0448\\w*",
                    "\u0437\u0432\u043e\u043d\\w*")),
            CaseAudit(
                "dat",
                "dative preposition",
                PhraseBeforePlayerNamePattern(
                    "\u043a",
                    "\u043d\u0430\u0432\u0441\u0442\u0440\u0435\u0447\u0443")),
            CaseAudit(
                "dat",
                "body-part action usually needs dative owner",
                "(?<![\\p{L}\\p{Nd}_])(?:\u0432\u044b\u0442\u0438\u0440\u0430\\w*|\u043f\u043e\u043f\u0440\u0430\u0432\u043b\u044f\\w*|\u0433\u043b\u0430\u0434\\w*)\\s+" +
                PlayerNameAuditPlaceholderPattern +
                "\\s+(?:\u0449\u0435\u043a\\w*|\u043b\u0438\u0446\\w*|\u0440\u0443\u043a\\w*|\u043b\u0430\u0434\u043e\u043d\\w*|\u0432\u043e\u043b\u043e\u0441\\w*)"),
            CaseAudit(
                "ins",
                "instrumental preposition",
                PhraseBeforePlayerNamePattern(
                    "\u0441",
                    "\u0441\u043e",
                    "\u043f\u0435\u0440\u0435\u0434",
                    "\u043d\u0430\u0434",
                    "\u043f\u043e\u0434",
                    "\u0437\u0430",
                    "\u043c\u0435\u0436\u0434\u0443")),
            CaseAudit(
                "ins",
                "instrumental-controlled verb",
                TriggerBeforePlayerNamePattern(
                    "\u043b\u044e\u0431\u0443\\w*\u0441\u044f",
                    "\u0432\u043e\u0441\u0445\u0438\u0449\u0430\\w*\u0441\u044f",
                    "\u0433\u043e\u0440\u0434\\w*\u0441\u044f",
                    "\u0438\u043d\u0442\u0435\u0440\u0435\u0441\u0443\\w*\u0441\u044f",
                    "\u0443\u043f\u0440\u0430\u0432\u043b\u044f\\w*")),
            CaseAudit(
                "prep",
                "prepositional phrase",
                PhraseBeforePlayerNamePattern(
                    "\u043e",
                    "\u043e\u0431",
                    "\u043e\u0431\u043e",
                    "\u043f\u0440\u0438",
                    "\u0434\u0443\u043c\u0430\\w*\\s+\u043e(?:\u0431|\u0431\u043e)?",
                    "\u0433\u043e\u0432\u043e\u0440\\w*\\s+\u043e(?:\u0431|\u0431\u043e)?",
                    "\u0440\u0430\u0441\u0441\u043a\u0430\u0437\u044b\u0432\u0430\\w*\\s+\u043e(?:\u0431|\u0431\u043e)?",
                    "\u0432\u0441\u043f\u043e\u043c\u0438\u043d\u0430\\w*\\s+\u043e(?:\u0431|\u0431\u043e)?",
                    "\u043c\u0435\u0447\u0442\u0430\\w*\\s+\u043e(?:\u0431|\u0431\u043e)?",
                    "\u0437\u0430\u0431\u043e\u0442\\w*\u0441\u044f\\s+\u043e(?:\u0431|\u0431\u043e)?"))
        };
    }

    const string PlayerNameAuditPlaceholderPattern = @"(?:\{playerName\}|\[player_name\])";

    static PlayerNameCaseAuditPattern CaseAudit(string caseCode, string reason, string pattern)
    {
        return new PlayerNameCaseAuditPattern(
            "{playerName:" + caseCode + "}",
            reason,
            pattern);
    }

    static string TriggerBeforePlayerNamePattern(params string[] triggerPatterns)
    {
        return PhraseBeforePlayerNamePattern(triggerPatterns);
    }

    static string PhraseBeforePlayerNamePattern(params string[] phrasePatterns)
    {
        return "(?<![\\p{L}\\p{Nd}_])(?:" +
               string.Join("|", phrasePatterns) +
               ")\\s+" +
               PlayerNameAuditPlaceholderPattern;
    }

    sealed class PlayerNameCaseAuditPattern
    {
        public readonly Regex Regex;
        public readonly string SuggestedToken;
        public readonly string Reason;

        public PlayerNameCaseAuditPattern(string suggestedToken, string reason, string pattern)
        {
            SuggestedToken = suggestedToken;
            Reason = reason;
            Regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }

    static T InvokeStatic<T>(System.Type type, string methodName, params object[] args)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, methodName + " was not found.");
        object result = method.Invoke(null, args);
        return result is T value ? value : default(T);
    }

    static T InvokeInstance<T>(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, methodName + " was not found.");
        object result = method.Invoke(target, args);
        return result is T value ? value : default(T);
    }
}
