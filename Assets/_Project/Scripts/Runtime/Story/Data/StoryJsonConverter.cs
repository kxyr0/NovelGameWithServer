using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;
using XNode;

public sealed class StoryJsonConversionReport
{
    private readonly List<string> _errors = new List<string>();
    private readonly List<string> _warnings = new List<string>();

    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyList<string> Warnings => _warnings;
    public bool HasErrors => _errors.Count > 0;
    public bool HasWarnings => _warnings.Count > 0;

    public void AddError(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            _errors.Add(message);
    }

    public void AddWarning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            _warnings.Add(message);
    }

    public string ToDisplayString()
    {
        var lines = new List<string>();
        lines.AddRange(_errors.Select(error => "Error: " + error));
        lines.AddRange(_warnings.Select(warning => "Warning: " + warning));
        return lines.Count > 0 ? string.Join("\n", lines) : "";
    }
}

public static class StoryJsonConverter
{
    private const int CurrentVersion = 2;
    private const int MinimumSupportedVersion = 1;
    private const int MaximumSupportedVersion = 2;
    private const string InputPortName = "enter";
    private const string DefaultOutputPortName = "exit";

    public static bool IsCanonicalJson(string json)
    {
        if (!NetworkJson.LooksLikeJsonObject(json))
            return false;

        string version = NetworkJson.GetRawValue(json, "version");
        string nodes = NetworkJson.GetRawValue(json, "nodes");
        return !string.IsNullOrWhiteSpace(version) && !string.IsNullOrWhiteSpace(nodes);
    }

    public static string SanitizeDisplayText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string result = SafeTextSanitizer.SanitizeStoryText(RemoveParentheticalSystemInstructions(value));
        while (result.Contains("  "))
            result = result.Replace("  ", " ");

        return result.Trim();
    }

    public static bool IsSystemInstructionText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string stripped = StripRichTextTags(text).Trim();
        if (string.IsNullOrEmpty(stripped))
            return false;

        string normalized = stripped.Trim(' ', '.', ':', ';', '!', '?').ToLowerInvariant();
        return normalized.StartsWith("\u044d\u043a\u0440\u0430\u043d \u0432\u044b\u0431\u043e\u0440\u0430", StringComparison.Ordinal) ||
               normalized == "\u0441\u0446\u0435\u043d\u0430 \u0433\u0430\u0440\u0434\u0435\u0440\u043e\u0431" ||
               normalized == "\u0441\u0446\u0435\u043d\u0430: \u0433\u0430\u0440\u0434\u0435\u0440\u043e\u0431" ||
               string.IsNullOrWhiteSpace(SanitizeDisplayText(stripped));
    }

    public static bool TryParseDocument(string json, out StoryJsonDocument document, out string reason)
    {
        bool result = TryParseDocumentInternal(json, out document, out var report);
        reason = report.ToDisplayString();
        return result;
    }

    public static bool TryBuildGraph(
        string json,
        string fallbackEpisodeId,
        out StoryGraph graph,
        out string reason,
        StoryJsonAssetResolver resolver = null)
    {
        bool result = TryBuildGraphWithReport(json, fallbackEpisodeId, out graph, out var report, resolver);
        reason = report.ToDisplayString();
        return result;
    }

    public static bool TryBuildGraphWithReport(
        string json,
        string fallbackEpisodeId,
        out StoryGraph graph,
        out StoryJsonConversionReport report,
        StoryJsonAssetResolver resolver = null)
    {
        graph = null;
        report = new StoryJsonConversionReport();
        resolver ??= new StoryJsonAssetResolver();

        if (!TryParseDocumentInternal(json, out var document, out report))
            return false;

        graph = ScriptableObject.CreateInstance<StoryGraph>();
        graph.hideFlags = HideFlags.DontSave;
        graph.name = "Json_" + FirstNonEmpty(document.episodeId, document.chapterId, fallbackEpisodeId, "Chapter");
        graph.episodeId = FirstNonEmpty(document.episodeId, document.chapterId, fallbackEpisodeId);
        string defaultPlayerName = ResolveDefaultPlayerName(document);
        if (!string.IsNullOrWhiteSpace(defaultPlayerName))
            graph.defaultPlayerName = defaultPlayerName;
        PlayerNameCaseForms defaultPlayerNameCases = ResolveDefaultPlayerNameCaseForms(document);
        if (PlayerNameInflector.HasAnyCaseForms(defaultPlayerNameCases))
            graph.defaultPlayerNameCases = defaultPlayerNameCases;

        var characters = BuildCharacterNameMap(document);
        var nodesById = new Dictionary<string, BaseStoryNode>(StringComparer.OrdinalIgnoreCase);
        var nodesByDto = new Dictionary<StoryJsonNode, BaseStoryNode>();
        StoryJsonNode firstNonStartDto = null;

        foreach (var dto in document.nodes)
        {
            if (dto == null)
            {
                report.AddError("Node entry is null.");
                continue;
            }

            string id = NormalizeId(dto.id);
            if (string.IsNullOrWhiteSpace(id))
            {
                report.AddError("Node has no required id.");
                continue;
            }

            if (nodesById.ContainsKey(id))
            {
                report.AddError("Duplicate node id: " + id);
                continue;
            }

            if (!TryCreateNode(graph, dto, id, resolver, characters, report, out var node))
                continue;

            nodesById[id] = node;
            nodesByDto[dto] = node;

            if (firstNonStartDto == null && !(node is StartNode))
                firstNonStartDto = dto;
        }

        if (report.HasErrors)
            return false;

        foreach (var dto in document.nodes)
        {
            if (dto == null || !nodesByDto.TryGetValue(dto, out var node))
                continue;

            ConnectNode(dto, node, nodesById, report);
        }

        if (!nodesById.Values.Any(node => node is StartNode) && firstNonStartDto != null)
        {
            var start = AddNode<StartNode>(graph, "start");
            start.name = "Start";
            Connect(start, DefaultOutputPortName, nodesByDto[firstNonStartDto], report, "auto start");
        }

        return !report.HasErrors;
    }

    public static bool TryExportGraph(
        StoryGraph graph,
        out string json,
        out string reason,
        StoryJsonAssetResolver resolver = null,
        bool prettyPrint = true)
    {
        bool result = TryExportGraphWithReport(graph, out json, out var report, resolver, prettyPrint);
        reason = report.ToDisplayString();
        return result;
    }

    public static bool TryExportGraphWithReport(
        StoryGraph graph,
        out string json,
        out StoryJsonConversionReport report,
        StoryJsonAssetResolver resolver = null,
        bool prettyPrint = true)
    {
        json = "";
        report = new StoryJsonConversionReport();
        resolver ??= new StoryJsonAssetResolver();

        if (graph == null)
        {
            report.AddError("StoryGraph is null.");
            return false;
        }

        var document = new StoryJsonDocument
        {
            version = CurrentVersion,
            storyId = "",
            chapterId = graph.episodeId ?? "",
            episodeId = graph.episodeId ?? "",
            title = graph.name ?? "",
            defaultPlayerName = graph.defaultPlayerName ?? "",
            defaultPlayerNameCases = PlayerNameInflector.HasAnyCaseForms(graph.defaultPlayerNameCases)
                ? graph.defaultPlayerNameCases
                : null,
            nodes = new List<StoryJsonNode>(),
            characters = new List<StoryJsonCharacter>()
        };

        var exportedCharacters = new Dictionary<string, StoryJsonCharacter>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in graph.nodes.OfType<BaseStoryNode>())
        {
            var dto = ExportNode(node, resolver, exportedCharacters, report);
            if (dto != null)
                document.nodes.Add(dto);
        }

        document.characters = exportedCharacters.Values.ToList();
        json = JsonUtility.ToJson(document, prettyPrint);
        return !report.HasErrors;
    }

    private static bool TryParseDocumentInternal(
        string json,
        out StoryJsonDocument document,
        out StoryJsonConversionReport report)
    {
        document = null;
        report = new StoryJsonConversionReport();

        if (!NetworkJson.LooksLikeJsonObject(json))
        {
            report.AddError("JSON payload must be an object.");
            return false;
        }

        try
        {
            document = JsonUtility.FromJson<StoryJsonDocument>(json);
        }
        catch (Exception exception)
        {
            report.AddError("Cannot parse JSON: " + exception.Message);
            return false;
        }

        if (document == null)
        {
            report.AddError("Cannot parse JSON document.");
            return false;
        }

        if (document.version == 0)
            document.version = MinimumSupportedVersion;

        if (document.version < MinimumSupportedVersion || document.version > MaximumSupportedVersion)
            report.AddError("Unsupported story JSON version: " + document.version);

        if (document.nodes == null || document.nodes.Count == 0)
            report.AddError("Story JSON has no nodes.");

        return !report.HasErrors;
    }

    private static Dictionary<string, string> BuildCharacterNameMap(StoryJsonDocument document)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (document?.characters == null)
            return result;

        foreach (var character in document.characters)
        {
            if (character == null)
                continue;

            string id = FirstNonEmpty(character.id, character.asset, character.guid, character.name);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            result[id] = FirstNonEmpty(character.name, character.id, character.asset, character.guid);
        }

        return result;
    }

    private static string ResolveDefaultPlayerName(StoryJsonDocument document)
    {
        if (document == null)
            return "";

        string documentDefaultName = FirstNonEmpty(document.defaultPlayerName, document.defaultName);
        if (!string.IsNullOrWhiteSpace(documentDefaultName) && !IsPlayerNamePlaceholder(documentDefaultName))
            return documentDefaultName;

        if (document.nodes != null)
        {
            foreach (var node in document.nodes)
            {
                if (node == null || NormalizeType(node.type) != StoryJsonTypes.NameChoice)
                    continue;

                string defaultName = FirstNonEmpty(node.defaultName);
                if (!string.IsNullOrWhiteSpace(defaultName) && !IsPlayerNamePlaceholder(defaultName))
                    return defaultName;
            }
        }

        if (document.characters != null)
        {
            foreach (var character in document.characters)
            {
                if (character == null)
                    continue;

                string id = FirstNonEmpty(character.id, character.asset, character.guid);
                string normalizedId = NormalizeToken(id);
                if (normalizedId == "hero" || normalizedId == "player" || normalizedId == "gg")
                {
                    string heroName = FirstNonEmpty(character.name, character.id, character.asset, character.guid);
                    return IsPlayerNamePlaceholder(heroName) ? "" : heroName;
                }
            }
        }

        return "";
    }

    private static PlayerNameCaseForms ResolveDefaultPlayerNameCaseForms(StoryJsonDocument document)
    {
        if (document == null)
            return null;

        if (PlayerNameInflector.HasAnyCaseForms(document.defaultPlayerNameCases))
            return document.defaultPlayerNameCases;

        return PlayerNameInflector.HasAnyCaseForms(document.defaultNameCases)
            ? document.defaultNameCases
            : null;
    }

    private static bool IsPlayerNamePlaceholder(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return DialogueVariableResolver.IsPlayerNameToken(value);
    }

    private static bool TryCreateNode(
        StoryGraph graph,
        StoryJsonNode dto,
        string id,
        StoryJsonAssetResolver resolver,
        Dictionary<string, string> characterNames,
        StoryJsonConversionReport report,
        out BaseStoryNode node)
    {
        node = null;
        string type = NormalizeType(dto.type);

        switch (type)
        {
            case StoryJsonTypes.Start:
                node = AddNode<StartNode>(graph, id);
                break;
            case StoryJsonTypes.Scene:
                node = AddNode<SceneSetupNode>(graph, id);
                ConfigureSceneNode((SceneSetupNode)node, dto, resolver, report);
                break;
            case StoryJsonTypes.Dialogue:
                node = AddNode<DialogueNode>(graph, id);
                ConfigureDialogueNode((DialogueNode)node, dto, resolver, characterNames, report);
                break;
            case StoryJsonTypes.Cutscene:
                node = AddNode<CutsceneNode>(graph, id);
                ConfigureCutsceneNode((CutsceneNode)node, dto, resolver, characterNames, report);
                break;
            case StoryJsonTypes.Choice:
                node = AddNode<ChoiceNode>(graph, id);
                ConfigureChoiceNode((ChoiceNode)node, dto, resolver, characterNames, report);
                break;
            case StoryJsonTypes.StatChange:
                node = AddNode<StatChangeNode>(graph, id);
                ConfigureStatChangeNode((StatChangeNode)node, dto);
                break;
            case StoryJsonTypes.VariableChange:
                node = AddNode<VariableChangeNode>(graph, id);
                ConfigureVariableChangeNode((VariableChangeNode)node, dto);
                break;
            case StoryJsonTypes.Condition:
                node = AddNode<ConditionNode>(graph, id);
                ConfigureConditionNode((ConditionNode)node, dto);
                break;
            case StoryJsonTypes.Premium:
                node = AddNode<PremiumNode>(graph, id);
                ConfigurePremiumNode((PremiumNode)node, dto);
                break;
            case StoryJsonTypes.Camera:
                node = AddNode<CameraNode>(graph, id);
                ConfigureCameraNode((CameraNode)node, dto, report);
                break;
            case StoryJsonTypes.Image:
                node = AddNode<ImageNode>(graph, id);
                ConfigureImageNode((ImageNode)node, dto, resolver, report);
                break;
            case StoryJsonTypes.PhoneDialogue:
                node = AddNode<PhoneDialogueNode>(graph, id);
                ConfigurePhoneDialogueNode((PhoneDialogueNode)node, dto, resolver, report);
                break;
            case StoryJsonTypes.Effect:
                node = AddNode<EffectNode>(graph, id);
                ConfigureEffectNode((EffectNode)node, dto, report);
                break;
            case StoryJsonTypes.Banner:
                node = AddNode<StoryBannerNode>(graph, id);
                ConfigureStoryBannerNode((StoryBannerNode)node, dto);
                break;
            case StoryJsonTypes.NameChoice:
                node = AddNode<NameChoiceNode>(graph, id);
                ConfigureNameChoiceNode((NameChoiceNode)node, dto);
                break;
            case StoryJsonTypes.AppearanceChoice:
                node = AddNode<AppearanceChoiceNode>(graph, id);
                ConfigureAppearanceChoiceNode((AppearanceChoiceNode)node, dto, resolver, report);
                break;
            case StoryJsonTypes.WardrobeChoice:
                node = AddNode<WardrobeChoiceNode>(graph, id);
                ConfigureWardrobeChoiceNode((WardrobeChoiceNode)node, dto, resolver, characterNames, report);
                break;
            case StoryJsonTypes.AddClothing:
                node = AddNode<AddClothingNode>(graph, id);
                ConfigureAddClothingNode((AddClothingNode)node, dto, resolver, report);
                break;
            case StoryJsonTypes.OpenWardrobe:
                node = AddNode<OpenWardrobeNode>(graph, id);
                break;
            case StoryJsonTypes.WardrobeCheck:
                node = AddNode<WardrobeCheckNode>(graph, id);
                ConfigureWardrobeCheckNode((WardrobeCheckNode)node, dto);
                break;
            default:
                report.AddError("Unknown node type '" + (dto.type ?? "") + "' for node '" + id + "'.");
                return false;
        }

        node.guid = id;
        node.name = FirstNonEmpty(SanitizeDisplayText(dto.title), type, id);
        node.position = dto.position;
        return true;
    }

    private static void ConfigureSceneNode(
        SceneSetupNode node,
        StoryJsonNode dto,
        StoryJsonAssetResolver resolver,
        StoryJsonConversionReport report)
    {
        node.sceneLabel = dto.label ?? "";
        node.suggestedBackground = dto.suggestedBackground ?? "";
        node.suggestedMusic = dto.suggestedMusic ?? "";

        var sceneData = ScriptableObject.CreateInstance<SceneSetupData>();
        sceneData.hideFlags = HideFlags.DontSave;
        sceneData.name = "JsonScene_" + node.guid;
        sceneData.backgroundId = dto.background ?? "";
        sceneData.backgroundVideoId = dto.backgroundVideo ?? "";
        sceneData.backgroundGifId = dto.backgroundGif ?? "";
        sceneData.backgroundOverlayId = dto.backgroundOverlay ?? "";
        sceneData.musicId = dto.music ?? "";
        sceneData.stopMusic = dto.stopMusic;
        sceneData.startSfxId = dto.startSfx ?? "";
        sceneData.stopSfx = dto.stopSfx;
        ConfigureSceneBackground(sceneData, dto, resolver, node.guid, report);
        sceneData.backgroundGif = ResolveAsset(dto.backgroundGif, resolver.ResolveTextAsset, "backgroundGif", node.guid, report);
        sceneData.backgroundOverlay = ResolveAsset(dto.backgroundOverlay, resolver.ResolveSprite, "backgroundOverlay", node.guid, report);
        sceneData.music = ResolveAsset(dto.music, resolver.ResolveAudioClip, "music", node.guid, report);
        sceneData.startSfx = ResolveAsset(dto.startSfx, resolver.ResolveAudioClip, "startSfx", node.guid, report);
        node.sceneData = sceneData;
    }

    private static void ConfigureSceneBackground(
        SceneSetupData sceneData,
        StoryJsonNode dto,
        StoryJsonAssetResolver resolver,
        string nodeId,
        StoryJsonConversionReport report)
    {
        sceneData.backgroundVideo = ResolveAsset(dto.backgroundVideo, resolver.ResolveVideoClip, "backgroundVideo", nodeId, report);

        if (sceneData.backgroundVideo == null && string.IsNullOrWhiteSpace(dto.backgroundVideo))
        {
            sceneData.backgroundVideo = TryResolveAsset(dto.background, resolver.ResolveVideoClip);
        }

        if (sceneData.backgroundVideo == null)
            sceneData.background = TryResolveAsset(dto.background, resolver.ResolveSprite);

        if (!string.IsNullOrWhiteSpace(dto.background) &&
            sceneData.background == null &&
            sceneData.backgroundVideo == null)
        {
            string message = "Node '" + nodeId + "' references missing asset in 'background': " +
                             dto.background +
                             " (expected Sprite or VideoClip; use StoryJsonAssetLibrary or an asset name/path).";
            report.AddWarning(message);
            Debug.LogWarning("[StoryJson] " + message);
        }
    }

    private static void ConfigureDialogueNode(
        DialogueNode node,
        StoryJsonNode dto,
        StoryJsonAssetResolver resolver,
        Dictionary<string, string> characterNames,
        StoryJsonConversionReport report)
    {
        node.nodeTitle = FirstNonEmpty(SanitizeDisplayText(dto.title), SanitizeDisplayText(dto.label));
        node.lines = BuildDialogueLines(dto.lines, resolver, characterNames, report, dto.id);
        node.activeCharacters = BuildActiveCharactersOrAuto(dto.activeCharacters, dto.lines, resolver, characterNames, report, dto.id);
    }

    private static void ConfigureCutsceneNode(
        CutsceneNode node,
        StoryJsonNode dto,
        StoryJsonAssetResolver resolver,
        Dictionary<string, string> characterNames,
        StoryJsonConversionReport report)
    {
        string imageId = FirstNonEmpty(dto.image, dto.background);
        string videoId = FirstNonEmpty(dto.video, dto.backgroundVideo);
        string gifId = FirstNonEmpty(dto.gif, dto.backgroundGif);

        Sprite image = TryResolveAsset(imageId, resolver.ResolveSprite);
        VideoClip video = ResolveAsset(videoId, resolver.ResolveVideoClip, "video", dto.id, report);
        TextAsset gif = ResolveAsset(gifId, resolver.ResolveTextAsset, "gif", dto.id, report);

        if (image == null &&
            video == null &&
            gif == null &&
            string.IsNullOrWhiteSpace(videoId) &&
            string.IsNullOrWhiteSpace(gifId))
        {
            video = TryResolveAsset(imageId, resolver.ResolveVideoClip);
            if (video == null)
                gif = TryResolveAsset(imageId, resolver.ResolveTextAsset);
        }

        if (!string.IsNullOrWhiteSpace(imageId) &&
            image == null &&
            video == null &&
            gif == null)
        {
            string message = "Node '" + dto.id + "' references missing asset in cutscene media: " +
                             imageId +
                             " (expected Sprite, VideoClip or TextAsset; use StoryJsonAssetLibrary or an asset name/path).";
            report.AddWarning(message);
            Debug.LogWarning("[StoryJson] " + message);
        }

        node.Configure(
            image,
            video,
            gif,
            dto.textDelay > 0f ? dto.textDelay : 0.6f,
            !dto.showCharacters,
            FirstNonEmpty(SanitizeDisplayText(dto.title), SanitizeDisplayText(dto.label)),
            BuildDialogueLines(dto.lines, resolver, characterNames, report, dto.id));
        node.ConfigureHeroBuildCutsceneOverrides(BuildHeroBuildCutsceneOverrides(dto, resolver, report));
    }

    private static void ConfigureChoiceNode(
        ChoiceNode node,
        StoryJsonNode dto,
        StoryJsonAssetResolver resolver,
        Dictionary<string, string> characterNames,
        StoryJsonConversionReport report)
    {
        node.nodeTitle = FirstNonEmpty(SanitizeDisplayText(dto.title), SanitizeDisplayText(dto.label));
        node.lines = BuildDialogueLines(dto.lines, resolver, characterNames, report, dto.id);
        node.activeCharacters = BuildActiveCharactersOrAuto(dto.activeCharacters, dto.lines, resolver, characterNames, report, dto.id);

        string choicePrompt = SanitizeDisplayText(dto.choicePrompt);
        if (node.lines.Count == 0 && !string.IsNullOrWhiteSpace(choicePrompt))
            node.lines.Add(new DialogueLine { richText = choicePrompt });

        node.options = new List<ChoiceOption>();
        node.choices = new List<BaseStoryNode>();

        if (dto.choices == null)
            return;

        for (int i = 0; i < dto.choices.Count; i++)
        {
            var choice = dto.choices[i] ?? new StoryJsonChoice();
            node.options.Add(new ChoiceOption
            {
                text = SanitizeDisplayText(choice.text),
                isPremium = choice.isPremium,
                premiumCost = SaveDataSanitizer.ClampCurrencyValue(choice.premiumCost),
                requiredVariable = choice.requiredVariable ?? "",
                requiredValue = choice.requiredValue,
                hideInRestrictedRegions = choice.hideInRestrictedRegions,
                hiddenRegionCodes = choice.hiddenRegionCodes != null
                    ? new List<string>(choice.hiddenRegionCodes)
                    : new List<string>()
            });
            node.choices.Add(null);
            EnsureDynamicOutput(node, "choices " + i);
        }
    }

    private static void ConfigureStatChangeNode(StatChangeNode node, StoryJsonNode dto)
    {
        node.statId = dto.statId ?? "";
        node.delta = SaveDataSanitizer.ClampStatValue(dto.statDelta);
        node.displayName = dto.statDisplayName ?? "";
        node.systemMessage = dto.systemMessage ?? "";
    }

    private static void ConfigureVariableChangeNode(VariableChangeNode node, StoryJsonNode dto)
    {
        node.variableKey = dto.variableKey ?? "";
        node.deltaValue = SaveDataSanitizer.ClampStatValue(dto.deltaValue);
        node.Add = dto.add;
    }

    private static void ConfigureConditionNode(ConditionNode node, StoryJsonNode dto)
    {
        node.variableKey = FirstNonEmpty(dto.leftVariableKey, dto.variableKey);
        node.compareVariableKey = FirstNonEmpty(dto.rightVariableKey, dto.compareVariableKey);
        node.comparison = ParseConditionComparison(dto.comparison, dto.id);
        node.requiredValue = dto.requiredValue;
    }

    private static void ConfigurePremiumNode(PremiumNode node, StoryJsonNode dto)
    {
        node.cost = SaveDataSanitizer.ClampCurrencyValue(dto.cost);
    }

    private static void ConfigureCameraNode(CameraNode node, StoryJsonNode dto, StoryJsonConversionReport report)
    {
        node.mode = ParseEnum(dto.mode, CameraNode.CameraMode.Position, "camera mode", dto.id, report);
        node.targetPosition = ParseEnum(dto.targetPosition, CharacterPosition.Center, "camera targetPosition", dto.id, report);
        node.xOffset = dto.xOffset;
        node.duration = Mathf.Max(0f, dto.duration);
    }

    private static void ConfigureImageNode(
        ImageNode node,
        StoryJsonNode dto,
        StoryJsonAssetResolver resolver,
        StoryJsonConversionReport report)
    {
        Sprite image = TryResolveAsset(dto.image, resolver.ResolveSprite);
        VideoClip video = ResolveAsset(dto.video, resolver.ResolveVideoClip, "video", dto.id, report);
        TextAsset gif = ResolveAsset(dto.gif, resolver.ResolveTextAsset, "gif", dto.id, report);

        if (image == null &&
            video == null &&
            gif == null &&
            string.IsNullOrWhiteSpace(dto.video) &&
            string.IsNullOrWhiteSpace(dto.gif))
        {
            video = TryResolveAsset(dto.image, resolver.ResolveVideoClip);
            if (video == null)
                gif = TryResolveAsset(dto.image, resolver.ResolveTextAsset);
        }

        if (!string.IsNullOrWhiteSpace(dto.image) &&
            image == null &&
            video == null &&
            gif == null)
        {
            string message = "Node '" + dto.id + "' references missing asset in 'image': " +
                             dto.image +
                             " (expected Sprite, VideoClip or TextAsset; use StoryJsonAssetLibrary or an asset name/path).";
            report.AddWarning(message);
            Debug.LogWarning("[StoryJson] " + message);
        }

        node.Configure(
            image,
            video,
            gif,
            FirstNonEmpty(dto.caption, "Рассмотреть"),
            dto.description ?? "",
            dto.zoomable);
        node.ConfigureHeroBuildCutsceneOverrides(BuildHeroBuildCutsceneOverrides(dto, resolver, report));
    }

    private static List<HeroBuildCutsceneOverride> BuildHeroBuildCutsceneOverrides(
        StoryJsonNode dto,
        StoryJsonAssetResolver resolver,
        StoryJsonConversionReport report)
    {
        var result = new List<HeroBuildCutsceneOverride>();
        if (dto?.heroBuildCutsceneOverrides == null || dto.heroBuildCutsceneOverrides.Count == 0)
            return result;

        for (int i = 0; i < dto.heroBuildCutsceneOverrides.Count; i++)
        {
            StoryJsonHeroBuildCutsceneOverride overrideDto = dto.heroBuildCutsceneOverrides[i];
            if (overrideDto == null)
                continue;

            HeroBuildCutsceneMedia media = ResolveHeroBuildCutsceneOverrideMedia(overrideDto, resolver, report, dto.id, i);
            List<string> hairIds = BuildOverrideHairIds(overrideDto);
            if (hairIds.Count == 0)
                hairIds.Add(overrideDto.hairId ?? "");

            for (int hairIndex = 0; hairIndex < hairIds.Count; hairIndex++)
            {
                var rule = new HeroBuildCutsceneOverride();
                rule.ConfigureFromJson(
                    overrideDto.enabled,
                    overrideDto.ruleName,
                    overrideDto.matchAppearance,
                    ParseEnum(overrideDto.appearance, AppearanceType.Default, "hero build cutscene appearance", dto.id, report),
                    overrideDto.outfitId,
                    hairIds[hairIndex],
                    overrideDto.accessoryId,
                    media.Image,
                    media.Video,
                    media.Gif,
                    overrideDto.image,
                    overrideDto.video,
                    overrideDto.gif);
                result.Add(rule);
            }
        }

        return result;
    }

    private static List<string> BuildOverrideHairIds(StoryJsonHeroBuildCutsceneOverride overrideDto)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddOverrideHairId(result, seen, overrideDto.hairId);
        if (overrideDto.hairIds != null)
        {
            foreach (string hairId in overrideDto.hairIds)
                AddOverrideHairId(result, seen, hairId);
        }

        return result;
    }

    private static void AddOverrideHairId(List<string> result, HashSet<string> seen, string hairId)
    {
        string normalized = SaveDataSanitizer.SanitizeIdentifier(hairId);
        if (!string.IsNullOrEmpty(normalized) && seen.Add(normalized))
            result.Add(normalized);
    }

    private static HeroBuildCutsceneMedia ResolveHeroBuildCutsceneOverrideMedia(
        StoryJsonHeroBuildCutsceneOverride overrideDto,
        StoryJsonAssetResolver resolver,
        StoryJsonConversionReport report,
        string nodeId,
        int overrideIndex)
    {
        string fieldPrefix = "heroBuildCutsceneOverrides[" + overrideIndex + "]";
        Sprite image = TryResolveAsset(overrideDto.image, resolver.ResolveSprite);
        VideoClip video = ResolveAsset(overrideDto.video, resolver.ResolveVideoClip, fieldPrefix + ".video", nodeId, report);
        TextAsset gif = ResolveAsset(overrideDto.gif, resolver.ResolveTextAsset, fieldPrefix + ".gif", nodeId, report);

        if (image == null &&
            video == null &&
            gif == null &&
            string.IsNullOrWhiteSpace(overrideDto.video) &&
            string.IsNullOrWhiteSpace(overrideDto.gif))
        {
            video = TryResolveAsset(overrideDto.image, resolver.ResolveVideoClip);
            if (video == null)
                gif = TryResolveAsset(overrideDto.image, resolver.ResolveTextAsset);
        }

        if (!string.IsNullOrWhiteSpace(overrideDto.image) &&
            image == null &&
            video == null &&
            gif == null)
        {
            string message = "Node '" + nodeId + "' references missing asset in '" + fieldPrefix + ".image': " +
                             overrideDto.image +
                             " (expected Sprite, VideoClip or TextAsset; fallback cutscene media will be used).";
            report.AddWarning(message);
            Debug.LogWarning("[StoryJson] " + message);
        }

        return new HeroBuildCutsceneMedia(image, video, gif);
    }

    private static void ConfigurePhoneDialogueNode(
        PhoneDialogueNode node,
        StoryJsonNode dto,
        StoryJsonAssetResolver resolver,
        StoryJsonConversionReport report)
    {
        node.contactName = dto.contactName ?? "";
        node.headerContactMode = ParseEnum(
            dto.headerContactMode,
            PhoneHeaderContactMode.CurrentIncomingSender,
            "phone header contact mode",
            dto.id,
            report);
        node.contactAvatar = ResolveAsset(dto.contactAvatar, resolver.ResolveSprite, "contactAvatar", dto.id, report);
        node.typingDelay = dto.typingDelay > 0f ? dto.typingDelay : 0.8f;
        node.messages = new List<PhoneMessage>();

        if (dto.messages == null)
            return;

        foreach (var message in dto.messages)
        {
            if (message == null)
                continue;

            PhoneMessageSide side = ParseEnum(message.side, PhoneMessageSide.Incoming, "phone message side", dto.id, report);
            string messageText = message.text ?? "";
            bool usePhotoLayout = message.usePhotoLayout || message.photoLayout;
            if (TryStripPhonePhotoLayoutToken(ref messageText))
                usePhotoLayout = true;
            string timeText = FirstNonEmpty(message.timeText, message.time);
            if (string.IsNullOrWhiteSpace(timeText) &&
                TrySplitPhoneMessageLeadingTime(messageText, out string leadingTime, out string bodyText))
            {
                timeText = leadingTime;
                messageText = bodyText;
            }
            Sprite attachment = ResolveAsset(message.attachment, resolver.ResolveSprite, "attachment", dto.id, report);

            node.messages.Add(new PhoneMessage
            {
                senderName = ResolvePhoneMessageSenderName(message, side, node.contactName),
                text = messageText,
                timeText = timeText,
                side = side,
                attachment = attachment,
                usePhotoLayout = usePhotoLayout || attachment != null
            });
        }
    }

    private static bool TryStripPhonePhotoLayoutToken(ref string value)
    {
        bool found = false;
        value = RemoveTokenIgnoreCase(value, "[photo]", ref found);
        value = RemoveTokenIgnoreCase(value, "[\u0444\u043E\u0442\u043E]", ref found);
        if (found)
            value = (value ?? "").Trim();
        return found;
    }

    private static string RemoveTokenIgnoreCase(string value, string token, ref bool found)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(token))
            return value ?? "";

        int index = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            found = true;
            value = value.Remove(index, token.Length);
            index = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }

    private static bool TrySplitPhoneMessageLeadingTime(string value, out string timeText, out string bodyText)
    {
        timeText = "";
        bodyText = value ?? "";
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        int newlineIndex = normalized.IndexOf('\n');
        if (newlineIndex <= 0)
            return false;

        string firstLine = normalized.Substring(0, newlineIndex).Trim();
        if (!LooksLikePhoneMessageTime(firstLine))
            return false;

        string rest = normalized.Substring(newlineIndex + 1).TrimStart('\n');
        if (string.IsNullOrWhiteSpace(rest))
            return false;

        timeText = firstLine;
        bodyText = rest;
        return true;
    }

    private static bool LooksLikePhoneMessageTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string[] parts = value.Trim().Split(':', '.');
        if (parts.Length < 2 || parts.Length > 3)
            return false;

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length == 0)
                return false;

            if (i == 0 && part.Length > 2)
                return false;
            if (i > 0 && part.Length != 2)
                return false;

            for (int j = 0; j < part.Length; j++)
            {
                if (!char.IsDigit(part[j]))
                    return false;
            }
        }

        return true;
    }

    private static string ResolvePhoneMessageSenderName(
        StoryJsonPhoneMessage message,
        PhoneMessageSide side,
        string contactName)
    {
        string senderName = FirstNonEmpty(message != null ? message.senderName : "", message != null ? message.speaker : "");
        if (!string.IsNullOrWhiteSpace(senderName))
            return NormalizePhoneMessageSenderName(senderName, side, contactName);

        return side == PhoneMessageSide.Outgoing
            ? "{PlayerName}"
            : FirstNonEmpty(contactName, "Contact");
    }

    private static string NormalizePhoneMessageSenderName(string senderName, PhoneMessageSide side, string contactName)
    {
        if (DialogueVariableResolver.IsPlayerNameToken(senderName))
            return "{PlayerName}";

        string value = (senderName ?? "").Trim();
        string normalized = value.Trim('[', ']', '<', '>').ToLowerInvariant();
        if (side == PhoneMessageSide.Outgoing)
            return "{PlayerName}";

        if (normalized == "me" ||
            normalized == "hero" ||
            normalized == "player" ||
            normalized == "name" ||
            normalized == "\u0438\u043C\u044F" ||
            normalized == "\u0433\u0433" ||
            normalized == "\u044F")
            return "{PlayerName}";

        if ((normalized == "contact" || normalized == "in" || normalized == "incoming") &&
            !string.IsNullOrWhiteSpace(contactName))
            return contactName.Trim();

        return string.IsNullOrWhiteSpace(value)
            ? side == PhoneMessageSide.Outgoing ? "{PlayerName}" : FirstNonEmpty(contactName, "Contact")
            : value;
    }

    private static void ConfigureEffectNode(EffectNode node, StoryJsonNode dto, StoryJsonConversionReport report)
    {
        node.effect = ParseEnum(dto.effect, EffectType.None, "effect", dto.id, report);
        node.duration = Mathf.Max(0f, dto.duration);
        node.intensity = dto.intensity;
    }

    private static void ConfigureStoryBannerNode(StoryBannerNode node, StoryJsonNode dto)
    {
        node.message = SanitizeDisplayText(FirstNonEmpty(dto.systemMessage, dto.title, dto.label, dto.description));
        if (dto.duration > 0f)
            node.fallbackDuration = dto.duration;
    }

    private static void ConfigureNameChoiceNode(NameChoiceNode node, StoryJsonNode dto)
    {
        node.promptText = dto.promptText ?? "";
        node.defaultName = FirstNonEmpty(dto.defaultName, "\u0410\u043b\u0438\u0441\u0430");
        if (dto.forceShow)
            node.forceShow = true;
    }

    private static void ConfigureAppearanceChoiceNode(
        AppearanceChoiceNode node,
        StoryJsonNode dto,
        StoryJsonAssetResolver resolver,
        StoryJsonConversionReport report)
    {
        node.promptText = dto.promptText ?? "";
        node.singleExit = dto.singleExit;
        node.options = new List<AppearanceOption>();
        node.choices = new List<BaseStoryNode>();

        if (dto.appearanceOptions == null)
            return;

        for (int i = 0; i < dto.appearanceOptions.Count; i++)
        {
            var option = dto.appearanceOptions[i] ?? new StoryJsonAppearanceOption();
            node.options.Add(new AppearanceOption
            {
                label = option.label ?? "",
                type = ParseEnum(option.type, AppearanceType.Default, "appearance type", dto.id, report),
                previewSprite = ResolveAsset(option.previewSprite, resolver.ResolveSprite, "previewSprite", dto.id, report)
            });
            node.choices.Add(null);

            if (!node.singleExit)
                EnsureDynamicOutput(node, "choices " + i);
        }
    }

    private static void ConfigureWardrobeChoiceNode(
        WardrobeChoiceNode node,
        StoryJsonNode dto,
        StoryJsonAssetResolver resolver,
        Dictionary<string, string> characterNames,
        StoryJsonConversionReport report)
    {
        node.characterId = dto.characterId ?? "";
        node.character = ResolveCharacter(node.characterId, resolver, characterNames);
        node.availableClothes = new List<ClothingItem>();
        node.premiumCosts = new List<int>();
        node.optionRules = new List<WardrobeChoiceOptionRule>();
        node.exits = new List<BaseStoryNode>();

        if (dto.clothes != null)
        {
            for (int i = 0; i < dto.clothes.Count; i++)
            {
                string clothingId = dto.clothes[i];
                WardrobeChoiceOptionRule rule = GetWardrobeOptionRule(dto, i);
                node.availableClothes.Add(ResolveClothingOrPlaceholder(clothingId, resolver, dto.id, report));
                node.optionRules.Add(rule);
                node.premiumCosts.Add(rule != null && rule.GetPremiumCost() > 0
                    ? rule.GetPremiumCost()
                    : GetWardrobePremiumCost(dto, i));
                node.exits.Add(null);
            }
        }

        int exitCount = dto.exits != null ? dto.exits.Count : 0;
        int targetCount = Mathf.Max(node.exits.Count, exitCount);
        while (node.exits.Count < targetCount)
            node.exits.Add(null);

        for (int i = 0; i < targetCount; i++)
            EnsureDynamicOutput(node, "exits " + i);
    }

    private static void ConfigureAddClothingNode(
        AddClothingNode node,
        StoryJsonNode dto,
        StoryJsonAssetResolver resolver,
        StoryJsonConversionReport report)
    {
        node.clothing = ResolveAsset(dto.clothing, resolver.ResolveClothing, "clothing", dto.id, report);
    }

    private static void ConfigureWardrobeCheckNode(WardrobeCheckNode node, StoryJsonNode dto)
    {
        node.itemId = dto.itemId ?? "";
    }

    private static void ConnectNode(
        StoryJsonNode dto,
        BaseStoryNode node,
        Dictionary<string, BaseStoryNode> nodesById,
        StoryJsonConversionReport report)
    {
        string type = NormalizeType(dto.type);

        if (node is StartNode && string.IsNullOrWhiteSpace(dto.next) && nodesById.Count > 1)
        {
            report.AddError("Node '" + dto.id + "' is start and must have next.");
            return;
        }

        switch (type)
        {
            case StoryJsonTypes.Start:
                ConnectById(node, DefaultOutputPortName, dto.next, nodesById, report, dto.id, true);
                ConnectById(node, "next", dto.next, nodesById, report, dto.id, false);
                break;
            case StoryJsonTypes.Choice:
                ConnectChoiceNode((ChoiceNode)node, dto, nodesById, report);
                ConnectById(node, DefaultOutputPortName, dto.next, nodesById, report, dto.id, false);
                break;
            case StoryJsonTypes.Condition:
                ConnectRequiredById(node, "trueExit", dto.trueNext, nodesById, report, dto.id);
                ConnectRequiredById(node, "falseExit", dto.falseNext, nodesById, report, dto.id);
                break;
            case StoryJsonTypes.Premium:
                ConnectRequiredById(node, "successNode", dto.successNext, nodesById, report, dto.id);
                ConnectById(node, "failNode", dto.failNext, nodesById, report, dto.id, false);
                break;
            case StoryJsonTypes.AppearanceChoice:
                ConnectAppearanceChoiceNode((AppearanceChoiceNode)node, dto, nodesById, report);
                break;
            case StoryJsonTypes.WardrobeChoice:
                ConnectWardrobeChoiceNode((WardrobeChoiceNode)node, dto, nodesById, report);
                ConnectById(node, DefaultOutputPortName, dto.next, nodesById, report, dto.id, false);
                break;
            case StoryJsonTypes.WardrobeCheck:
                ConnectRequiredById(node, "hasItem", dto.hasItemNext, nodesById, report, dto.id);
                ConnectRequiredById(node, "noItem", dto.noItemNext, nodesById, report, dto.id);
                break;
            case StoryJsonTypes.Banner:
            case StoryJsonTypes.NameChoice:
                ConnectById(node, DefaultOutputPortName, dto.next, nodesById, report, dto.id, false);
                break;
            default:
                ConnectById(node, DefaultOutputPortName, dto.next, nodesById, report, dto.id, false);
                break;
        }
    }

    private static void ConnectChoiceNode(
        ChoiceNode node,
        StoryJsonNode dto,
        Dictionary<string, BaseStoryNode> nodesById,
        StoryJsonConversionReport report)
    {
        if (dto.choices == null)
            return;

        for (int i = 0; i < dto.choices.Count; i++)
        {
            var choice = dto.choices[i];
            string portName = "choices " + i;
            EnsureDynamicOutput(node, portName);

            if (choice == null || string.IsNullOrWhiteSpace(choice.next))
            {
                report.AddError("Choice '" + dto.id + "' option " + i + " has no next.");
                continue;
            }

            ConnectRequiredById(node, portName, choice.next, nodesById, report, dto.id);
        }
    }

    private static void ConnectAppearanceChoiceNode(
        AppearanceChoiceNode node,
        StoryJsonNode dto,
        Dictionary<string, BaseStoryNode> nodesById,
        StoryJsonConversionReport report)
    {
        if (node.singleExit)
        {
            ConnectById(node, DefaultOutputPortName, dto.next, nodesById, report, dto.id, false);
            return;
        }

        if (dto.appearanceOptions == null)
            return;

        for (int i = 0; i < dto.appearanceOptions.Count; i++)
        {
            var option = dto.appearanceOptions[i];
            string portName = "choices " + i;
            EnsureDynamicOutput(node, portName);

            if (option == null || string.IsNullOrWhiteSpace(option.next))
            {
                report.AddError("Appearance choice '" + dto.id + "' option " + i + " has no next.");
                continue;
            }

            ConnectRequiredById(node, portName, option.next, nodesById, report, dto.id);
        }
    }

    private static void ConnectWardrobeChoiceNode(
        WardrobeChoiceNode node,
        StoryJsonNode dto,
        Dictionary<string, BaseStoryNode> nodesById,
        StoryJsonConversionReport report)
    {
        if ((dto.exits == null || dto.exits.Count == 0) && !string.IsNullOrWhiteSpace(dto.next))
        {
            for (int i = 0; i < node.exits.Count; i++)
            {
                string portName = "exits " + i;
                EnsureDynamicOutput(node, portName);
                if (ConnectRequiredById(node, portName, dto.next, nodesById, report, dto.id) &&
                    nodesById.TryGetValue(dto.next, out var target))
                {
                    node.exits[i] = target;
                }
            }

            ConnectById(node, DefaultOutputPortName, dto.next, nodesById, report, dto.id, false);
            return;
        }

        if (dto.exits == null)
            return;

        while (node.exits.Count < dto.exits.Count)
            node.exits.Add(null);

        for (int i = 0; i < dto.exits.Count; i++)
        {
            string nextId = dto.exits[i];
            if (string.IsNullOrWhiteSpace(nextId))
                continue;

            string portName = "exits " + i;
            EnsureDynamicOutput(node, portName);
            if (ConnectRequiredById(node, portName, nextId, nodesById, report, dto.id) &&
                nodesById.TryGetValue(nextId, out var target))
            {
                node.exits[i] = target;
            }
        }
    }

    private static bool ConnectRequiredById(
        BaseStoryNode outputNode,
        string outputPortName,
        string targetId,
        Dictionary<string, BaseStoryNode> nodesById,
        StoryJsonConversionReport report,
        string sourceId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            report.AddError("Node '" + sourceId + "' port '" + outputPortName + "' has no next target.");
            return false;
        }

        return ConnectById(outputNode, outputPortName, targetId, nodesById, report, sourceId, true);
    }

    private static bool ConnectById(
        BaseStoryNode outputNode,
        string outputPortName,
        string targetId,
        Dictionary<string, BaseStoryNode> nodesById,
        StoryJsonConversionReport report,
        string sourceId,
        bool required)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return !required;

        if (!nodesById.TryGetValue(targetId, out var inputNode) || inputNode == null)
        {
            report.AddError("Node '" + sourceId + "' port '" + outputPortName + "' points to missing node '" + targetId + "'.");
            return false;
        }

        return Connect(outputNode, outputPortName, inputNode, report, sourceId);
    }

    private static bool Connect(
        BaseStoryNode outputNode,
        string outputPortName,
        BaseStoryNode inputNode,
        StoryJsonConversionReport report,
        string sourceId)
    {
        if (outputNode == null || inputNode == null)
            return false;

        var outputPort = outputNode.GetOutputPort(outputPortName);
        var inputPort = inputNode.GetInputPort(InputPortName);
        if (outputPort == null)
        {
            report.AddError("Node '" + sourceId + "' has no output port '" + outputPortName + "'.");
            return false;
        }

        if (inputPort == null)
        {
            report.AddError("Node '" + inputNode.guid + "' has no input port '" + InputPortName + "'.");
            return false;
        }

        if (!outputPort.IsConnectedTo(inputPort))
            outputPort.Connect(inputPort);

        return true;
    }

    private static StoryJsonNode ExportNode(
        BaseStoryNode node,
        StoryJsonAssetResolver resolver,
        Dictionary<string, StoryJsonCharacter> exportedCharacters,
        StoryJsonConversionReport report)
    {
        var dto = new StoryJsonNode
        {
            id = GetNodeId(node),
            guid = node.guid,
            position = node.position,
            next = GetConnectedId(node, DefaultOutputPortName)
        };

        switch (node)
        {
            case StartNode start:
                dto.type = StoryJsonTypes.Start;
                dto.next = FirstNonEmpty(GetConnectedId(start, DefaultOutputPortName), GetConnectedId(start, "next"));
                break;
            case SceneSetupNode scene:
                dto.type = StoryJsonTypes.Scene;
                dto.label = scene.sceneLabel ?? "";
                dto.suggestedBackground = scene.suggestedBackground ?? "";
                dto.suggestedMusic = scene.suggestedMusic ?? "";
                if (scene.sceneData != null)
                {
                    dto.background = resolver.GetAssetId(scene.sceneData.background);
                    dto.backgroundVideo = resolver.GetAssetId(scene.sceneData.backgroundVideo);
                    dto.backgroundGif = resolver.GetAssetId(scene.sceneData.backgroundGif);
                    dto.backgroundOverlay = resolver.GetAssetId(scene.sceneData.backgroundOverlay);
                    dto.music = resolver.GetAssetId(scene.sceneData.music);
                    dto.stopMusic = scene.sceneData.stopMusic;
                    dto.startSfx = resolver.GetAssetId(scene.sceneData.startSfx);
                    dto.stopSfx = scene.sceneData.stopSfx;
                }
                break;
            case CutsceneNode cutscene:
                dto.type = StoryJsonTypes.Cutscene;
                dto.title = cutscene.nodeTitle ?? "";
                dto.image = resolver.GetAssetId(cutscene.defaultImage);
                dto.video = resolver.GetAssetId(cutscene.defaultVideo);
                dto.gif = resolver.GetAssetId(cutscene.defaultGif);
                dto.textDelay = cutscene.TextDelay;
                dto.showCharacters = !cutscene.HideCharacters;
                dto.lines = ExportDialogueLines(cutscene.lines, resolver, exportedCharacters);
                dto.heroBuildCutsceneOverrides = ExportHeroBuildCutsceneOverrides(cutscene.heroBuildCutsceneOverrides, resolver);
                break;
            case DialogueNode dialogue:
                dto.type = StoryJsonTypes.Dialogue;
                dto.title = dialogue.nodeTitle ?? "";
                dto.activeCharacters = ExportActiveCharacters(dialogue.activeCharacters, resolver, exportedCharacters);
                dto.lines = ExportDialogueLines(dialogue.lines, resolver, exportedCharacters);
                break;
            case ChoiceNode choice:
                dto.type = StoryJsonTypes.Choice;
                dto.title = choice.nodeTitle ?? "";
                dto.activeCharacters = ExportActiveCharacters(choice.activeCharacters, resolver, exportedCharacters);
                dto.lines = ExportDialogueLines(choice.lines, resolver, exportedCharacters);
                dto.choicePrompt = choice.lines != null && choice.lines.Count > 0 ? choice.lines[0].richText : "";
                dto.choices = ExportChoices(choice);
                break;
            case StatChangeNode statChange:
                dto.type = StoryJsonTypes.StatChange;
                dto.statId = statChange.statId ?? "";
                dto.statDelta = SaveDataSanitizer.ClampStatValue(statChange.delta);
                dto.statDisplayName = statChange.displayName ?? "";
                dto.systemMessage = statChange.systemMessage ?? "";
                break;
            case VariableChangeNode variableChange:
                dto.type = StoryJsonTypes.VariableChange;
                dto.variableKey = variableChange.variableKey ?? "";
                dto.deltaValue = SaveDataSanitizer.ClampStatValue(variableChange.deltaValue);
                dto.add = variableChange.Add;
                break;
            case ConditionNode condition:
                dto.type = StoryJsonTypes.Condition;
                dto.variableKey = condition.variableKey ?? "";
                dto.comparison = condition.comparison != ConditionComparison.Equals
                    ? condition.comparison.ToString()
                    : "";
                dto.compareVariableKey = condition.compareVariableKey ?? "";
                dto.requiredValue = condition.requiredValue;
                dto.trueNext = GetConnectedId(condition, "trueExit");
                dto.falseNext = GetConnectedId(condition, "falseExit");
                dto.next = "";
                break;
            case PremiumNode premium:
                dto.type = StoryJsonTypes.Premium;
                dto.cost = SaveDataSanitizer.ClampCurrencyValue(premium.cost);
                dto.successNext = GetConnectedId(premium, "successNode");
                dto.failNext = GetConnectedId(premium, "failNode");
                dto.next = "";
                break;
            case CameraNode camera:
                dto.type = StoryJsonTypes.Camera;
                dto.mode = camera.mode.ToString();
                dto.targetPosition = camera.targetPosition.ToString();
                dto.xOffset = camera.xOffset;
                dto.duration = camera.duration;
                break;
            case ImageNode image:
                dto.type = StoryJsonTypes.Image;
                dto.image = resolver.GetAssetId(image.defaultImage);
                dto.video = resolver.GetAssetId(image.defaultVideo);
                dto.gif = resolver.GetAssetId(image.defaultGif);
                dto.caption = image.caption ?? "";
                dto.description = image.description ?? "";
                dto.zoomable = image.zoomable;
                dto.heroBuildCutsceneOverrides = ExportHeroBuildCutsceneOverrides(image.heroBuildCutsceneOverrides, resolver);
                break;
            case PhoneDialogueNode phone:
                dto.type = StoryJsonTypes.PhoneDialogue;
                dto.contactName = phone.contactName ?? "";
                dto.headerContactMode = phone.headerContactMode.ToString();
                dto.contactAvatar = resolver.GetAssetId(phone.contactAvatar);
                dto.typingDelay = phone.typingDelay;
                dto.messages = ExportPhoneMessages(phone.messages, resolver);
                break;
            case EffectNode effect:
                dto.type = StoryJsonTypes.Effect;
                dto.effect = effect.effect.ToString();
                dto.duration = effect.duration;
                dto.intensity = effect.intensity;
                break;
            case StoryBannerNode banner:
                dto.type = StoryJsonTypes.Banner;
                dto.systemMessage = banner.message ?? "";
                dto.duration = banner.fallbackDuration;
                break;
            case NameChoiceNode nameChoice:
                dto.type = StoryJsonTypes.NameChoice;
                dto.promptText = nameChoice.promptText ?? "";
                dto.defaultName = nameChoice.defaultName ?? "";
                dto.forceShow = nameChoice.forceShow;
                break;
            case AppearanceChoiceNode appearanceChoice:
                dto.type = StoryJsonTypes.AppearanceChoice;
                dto.promptText = appearanceChoice.promptText ?? "";
                dto.singleExit = appearanceChoice.singleExit;
                dto.next = appearanceChoice.singleExit ? GetConnectedId(appearanceChoice, DefaultOutputPortName) : "";
                dto.appearanceOptions = ExportAppearanceOptions(appearanceChoice, resolver);
                break;
            case WardrobeChoiceNode wardrobeChoice:
                dto.type = StoryJsonTypes.WardrobeChoice;
                dto.characterId = wardrobeChoice.characterId ?? "";
                dto.clothes = ExportClothes(wardrobeChoice.availableClothes, resolver);
                dto.premiumCosts = ExportWardrobePremiumCosts(wardrobeChoice);
                dto.optionRules = ExportWardrobeOptionRules(wardrobeChoice);
                dto.exits = ExportWardrobeExits(wardrobeChoice);
                break;
            case AddClothingNode addClothing:
                dto.type = StoryJsonTypes.AddClothing;
                dto.clothing = resolver.GetClothingId(addClothing.clothing);
                break;
            case OpenWardrobeNode:
                dto.type = StoryJsonTypes.OpenWardrobe;
                break;
            case WardrobeCheckNode wardrobeCheck:
                dto.type = StoryJsonTypes.WardrobeCheck;
                dto.itemId = wardrobeCheck.itemId ?? "";
                dto.hasItemNext = GetConnectedId(wardrobeCheck, "hasItem");
                dto.noItemNext = GetConnectedId(wardrobeCheck, "noItem");
                dto.next = "";
                break;
            default:
                report.AddWarning("Skipped unsupported node type: " + node.GetType().Name);
                return null;
        }

        return dto;
    }

    private static List<DialogueCharacterEntry> BuildActiveCharactersOrAuto(
        List<StoryJsonActiveCharacter> entries,
        List<StoryJsonLine> lines,
        StoryJsonAssetResolver resolver,
        Dictionary<string, string> characterNames,
        StoryJsonConversionReport report,
        string nodeId)
    {
        var explicitCharacters = BuildActiveCharacters(entries, resolver, characterNames, report, nodeId);
        return explicitCharacters.Count > 0
            ? explicitCharacters
            : BuildAutoActiveCharacters(lines, resolver, characterNames, report, nodeId);
    }

    private static List<DialogueCharacterEntry> BuildActiveCharacters(
        List<StoryJsonActiveCharacter> entries,
        StoryJsonAssetResolver resolver,
        Dictionary<string, string> characterNames,
        StoryJsonConversionReport report,
        string nodeId)
    {
        var result = new List<DialogueCharacterEntry>();
        if (entries == null)
            return result;

        foreach (var entry in entries)
        {
            if (entry == null)
                continue;

            if (string.IsNullOrWhiteSpace(entry.character))
            {
                string message = "Node '" + nodeId + "' has active character entry without character id.";
                report.AddWarning(message);
                Debug.LogWarning("[StoryJson] " + message);
                continue;
            }

            result.Add(new DialogueCharacterEntry
            {
                character = ResolveCharacter(entry.character, resolver, characterNames),
                emotion = ParseEnum(entry.emotion, CharacterEmotionType.Idle, "character emotion", nodeId, report),
                position = ParseEnum(entry.position, CharacterPosition.Center, "character position", nodeId, report),
                speakerNameHint = entry.character ?? ""
            });
        }

        return result;
    }

    private static List<DialogueCharacterEntry> BuildAutoActiveCharacters(
        List<StoryJsonLine> lines,
        StoryJsonAssetResolver resolver,
        Dictionary<string, string> characterNames,
        StoryJsonConversionReport report,
        string nodeId)
    {
        var result = new List<DialogueCharacterEntry>();
        if (lines == null)
            return result;

        var seenSpeakers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.speaker))
                continue;

            string speakerId = NormalizeId(line.speaker);
            if (!seenSpeakers.Add(speakerId))
                continue;

            CharacterData character = ResolveCharacter(speakerId, resolver, characterNames);
            result.Add(new DialogueCharacterEntry
            {
                character = character,
                emotion = CharacterEmotionType.Idle,
                position = IsHeroSpeaker(speakerId, character) ? CharacterPosition.Left : CharacterPosition.Right,
                speakerNameHint = speakerId
            });
        }

        return result;
    }

    private static List<DialogueLine> BuildDialogueLines(
        List<StoryJsonLine> lines,
        StoryJsonAssetResolver resolver,
        Dictionary<string, string> characterNames,
        StoryJsonConversionReport report,
        string nodeId)
    {
        var result = new List<DialogueLine>();
        if (lines == null)
            return result;

        foreach (var line in lines)
        {
            if (line == null)
                continue;

            string speakerId = NormalizeId(line.speaker);
            characterNames.TryGetValue(speakerId, out string speakerNameHint);
            if (string.IsNullOrWhiteSpace(line.speaker) && IsSystemInstructionDialogueText(line.text))
                continue;

            string richText = SanitizeDisplayText(line.text);
            if (string.IsNullOrWhiteSpace(line.speaker) && string.IsNullOrWhiteSpace(richText))
                continue;

            result.Add(new DialogueLine
            {
                speakerId = speakerId,
                speakerNameHint = FirstNonEmpty(speakerNameHint, line.speaker),
                speaker = ResolveCharacter(line.speaker, resolver, characterNames),
                emotion = ParseEnum(line.emotion, CharacterEmotionType.Idle, "line emotion", nodeId, report),
                richText = richText,
                style = ResolveAsset(line.style, resolver.ResolveDialogueStyle, "style", nodeId, report),
                authorComment = line.authorComment ?? ""
            });
        }

        return result;
    }

    private static bool IsSystemInstructionDialogueText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return IsSystemInstructionText(text);
    }

    private static string RemoveParentheticalSystemInstructions(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var builder = new System.Text.StringBuilder(value.Length);
        int index = 0;
        while (index < value.Length)
        {
            int open = value.IndexOf('(', index);
            if (open < 0)
            {
                builder.Append(value, index, value.Length - index);
                break;
            }

            int close = value.IndexOf(')', open + 1);
            if (close < 0)
            {
                builder.Append(value, index, value.Length - index);
                break;
            }

            builder.Append(value, index, open - index);
            string inner = value.Substring(open + 1, close - open - 1);
            if (!ContainsSystemInstructionMarker(inner))
                builder.Append(value, open, close - open + 1);

            index = close + 1;
        }

        return builder.ToString();
    }

    private static bool ContainsSystemInstructionMarker(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = StripRichTextTags(value).ToLowerInvariant();
        if (normalized.Contains("\u0438\u0433\u0440\u043e\u043a\u0430 \u043f\u0435\u0440\u0435\u043a\u0438\u0434\u044b\u0432\u0430\u0435\u0442") ||
            normalized.Contains("\u044d\u043a\u0440\u0430\u043d \u0433\u0430\u0440\u0434\u0435\u0440\u043e\u0431\u0430"))
        {
            return true;
        }

        bool mentionsScreen = normalized.Contains("\u043d\u0430 \u044d\u043a\u0440\u0430\u043d\u0435");
        bool hasDisplayVerb =
            normalized.Contains("\u0432\u044b\u0441\u0432\u0435\u0447") ||
            normalized.Contains("\u043f\u043e\u043a\u0430\u0437\u044b\u0432\u0430") ||
            normalized.Contains("\u043e\u0442\u043e\u0431\u0440\u0430\u0436") ||
            normalized.Contains("\u0432\u044b\u0432\u043e\u0434");

        return mentionsScreen && hasDisplayVerb;
    }

    private static string StripRichTextTags(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var builder = new System.Text.StringBuilder(value.Length);
        bool insideTag = false;
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (character == '<')
            {
                insideTag = true;
                continue;
            }

            if (insideTag)
            {
                if (character == '>')
                    insideTag = false;

                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static List<StoryJsonActiveCharacter> ExportActiveCharacters(
        List<DialogueCharacterEntry> entries,
        StoryJsonAssetResolver resolver,
        Dictionary<string, StoryJsonCharacter> exportedCharacters)
    {
        var result = new List<StoryJsonActiveCharacter>();
        if (entries == null)
            return result;

        foreach (var entry in entries)
        {
            if (entry == null)
                continue;

            string characterId = ExportCharacter(entry.character, resolver, exportedCharacters);
            result.Add(new StoryJsonActiveCharacter
            {
                character = FirstNonEmpty(characterId, entry.speakerNameHint),
                emotion = entry.emotion.ToString(),
                position = entry.position.ToString()
            });
        }

        return result;
    }

    private static List<StoryJsonLine> ExportDialogueLines(
        List<DialogueLine> lines,
        StoryJsonAssetResolver resolver,
        Dictionary<string, StoryJsonCharacter> exportedCharacters)
    {
        var result = new List<StoryJsonLine>();
        if (lines == null)
            return result;

        foreach (var line in lines)
        {
            if (line == null)
                continue;

            string exportedSpeaker = ExportCharacter(line.speaker, resolver, exportedCharacters);
            result.Add(new StoryJsonLine
            {
                speaker = FirstNonEmpty(line.speakerId, exportedSpeaker),
                emotion = line.emotion.ToString(),
                text = line.richText ?? "",
                style = resolver.GetAssetId(line.style),
                authorComment = line.authorComment ?? ""
            });
        }

        return result;
    }

    private static List<StoryJsonHeroBuildCutsceneOverride> ExportHeroBuildCutsceneOverrides(
        IReadOnlyList<HeroBuildCutsceneOverride> overrides,
        StoryJsonAssetResolver resolver)
    {
        var result = new List<StoryJsonHeroBuildCutsceneOverride>();
        if (overrides == null)
            return result;

        foreach (HeroBuildCutsceneOverride rule in overrides)
        {
            if (rule == null)
                continue;

            result.Add(new StoryJsonHeroBuildCutsceneOverride
            {
                enabled = rule.Enabled,
                ruleName = rule.RuleName,
                matchAppearance = rule.MatchAppearance,
                appearance = rule.MatchAppearance ? rule.Appearance.ToString() : "",
                outfitId = rule.OutfitId ?? "",
                hairId = rule.HairId ?? "",
                accessoryId = rule.AccessoryId ?? "",
                image = FirstNonEmpty(rule.ImageAssetId, resolver.GetAssetId(rule.DefaultImage)),
                video = FirstNonEmpty(rule.VideoAssetId, resolver.GetAssetId(rule.DefaultVideo)),
                gif = FirstNonEmpty(rule.GifAssetId, resolver.GetAssetId(rule.DefaultGif))
            });
        }

        return result;
    }

    private static List<StoryJsonChoice> ExportChoices(ChoiceNode node)
    {
        var result = new List<StoryJsonChoice>();
        int count = node.options != null ? node.options.Count : 0;
        for (int i = 0; i < count; i++)
        {
            var option = node.options[i] ?? new ChoiceOption();
            result.Add(new StoryJsonChoice
            {
                text = option.text ?? "",
                isPremium = option.isPremium,
                premiumCost = SaveDataSanitizer.ClampCurrencyValue(option.premiumCost),
                requiredVariable = option.requiredVariable ?? "",
                requiredValue = option.requiredValue,
                hideInRestrictedRegions = option.hideInRestrictedRegions,
                hiddenRegionCodes = option.hiddenRegionCodes != null
                    ? new List<string>(option.hiddenRegionCodes)
                    : new List<string>(),
                next = GetConnectedId(node, "choices " + i)
            });
        }

        return result;
    }

    private static List<StoryJsonPhoneMessage> ExportPhoneMessages(
        List<PhoneMessage> messages,
        StoryJsonAssetResolver resolver)
    {
        var result = new List<StoryJsonPhoneMessage>();
        if (messages == null)
            return result;

        foreach (var message in messages)
        {
            if (message == null)
                continue;

            result.Add(new StoryJsonPhoneMessage
            {
                senderName = message.senderName ?? "",
                text = message.text ?? "",
                timeText = message.timeText ?? "",
                side = message.side.ToString(),
                attachment = resolver.GetAssetId(message.attachment),
                usePhotoLayout = message.usePhotoLayout
            });
        }

        return result;
    }

    private static List<StoryJsonAppearanceOption> ExportAppearanceOptions(
        AppearanceChoiceNode node,
        StoryJsonAssetResolver resolver)
    {
        var result = new List<StoryJsonAppearanceOption>();
        int count = node.options != null ? node.options.Count : 0;
        for (int i = 0; i < count; i++)
        {
            var option = node.options[i] ?? new AppearanceOption();
            result.Add(new StoryJsonAppearanceOption
            {
                label = option.label ?? "",
                type = option.type.ToString(),
                previewSprite = resolver.GetAssetId(option.previewSprite),
                next = node.singleExit ? "" : GetConnectedId(node, "choices " + i)
            });
        }

        return result;
    }

    private static List<string> ExportClothes(List<ClothingItem> clothes, StoryJsonAssetResolver resolver)
    {
        var result = new List<string>();
        if (clothes == null)
            return result;

        foreach (var item in clothes)
            result.Add(resolver.GetClothingId(item));

        return result;
    }

    private static List<string> ExportWardrobeExits(WardrobeChoiceNode node)
    {
        var result = new List<string>();
        int count = Mathf.Max(
            node.availableClothes != null ? node.availableClothes.Count : 0,
            node.exits != null ? node.exits.Count : 0);

        for (int i = 0; i < count; i++)
        {
            string next = GetConnectedId(node, "exits " + i);
            if (string.IsNullOrEmpty(next) && node.exits != null && i < node.exits.Count && node.exits[i] != null)
                next = GetNodeId(node.exits[i]);

            result.Add(next ?? "");
        }

        return result;
    }

    private static List<int> ExportWardrobePremiumCosts(WardrobeChoiceNode node)
    {
        var result = new List<int>();
        int count = node != null && node.availableClothes != null ? node.availableClothes.Count : 0;
        bool hasPaidOption = false;

        for (int i = 0; i < count; i++)
        {
            int cost = node.GetPremiumCost(i);
            result.Add(cost);
            hasPaidOption |= cost > 0;
        }

        return hasPaidOption ? result : new List<int>();
    }

    private static List<StoryJsonWardrobeOptionRule> ExportWardrobeOptionRules(WardrobeChoiceNode node)
    {
        var result = new List<StoryJsonWardrobeOptionRule>();
        int count = node != null && node.availableClothes != null ? node.availableClothes.Count : 0;
        bool hasRules = false;

        for (int i = 0; i < count; i++)
        {
            WardrobeChoiceOptionRule rule = node.GetOptionRule(i);
            var dto = new StoryJsonWardrobeOptionRule
            {
                premiumCost = node.GetPremiumCost(i),
                requiredVariable = rule != null ? rule.requiredVariable ?? "" : "",
                requiredValue = rule != null ? rule.requiredValue : 0,
                requiredItemId = rule != null ? rule.requiredItemId ?? "" : "",
                hideInRestrictedRegions = rule != null && rule.hideInRestrictedRegions,
                hiddenRegionCodes = rule != null && rule.hiddenRegionCodes != null
                    ? new List<string>(rule.hiddenRegionCodes)
                    : new List<string>(),
                purchaseKey = rule != null ? rule.purchaseKey ?? "" : "",
                unavailableMessage = rule != null ? rule.unavailableMessage ?? "" : ""
            };

            result.Add(dto);
            hasRules |= HasWardrobeRule(dto);
        }

        return hasRules ? result : new List<StoryJsonWardrobeOptionRule>();
    }

    private static WardrobeChoiceOptionRule GetWardrobeOptionRule(StoryJsonNode dto, int index)
    {
        StoryJsonWardrobeOptionRule source = dto.optionRules != null && index >= 0 && index < dto.optionRules.Count
            ? dto.optionRules[index]
            : null;

        int legacyCost = GetWardrobePremiumCost(dto, index);
        if (source == null && legacyCost <= 0)
            return null;

        var rule = new WardrobeChoiceOptionRule
        {
            premiumCost = source != null
                ? SaveDataSanitizer.ClampCurrencyValue(source.premiumCost)
                : legacyCost,
            requiredVariable = source != null ? source.requiredVariable ?? "" : "",
            requiredValue = source != null ? source.requiredValue : 0,
            requiredItemId = source != null ? source.requiredItemId ?? "" : "",
            hideInRestrictedRegions = source != null && source.hideInRestrictedRegions,
            hiddenRegionCodes = source != null && source.hiddenRegionCodes != null
                ? new List<string>(source.hiddenRegionCodes)
                : new List<string>(),
            purchaseKey = source != null ? source.purchaseKey ?? "" : "",
            unavailableMessage = source != null ? source.unavailableMessage ?? "" : ""
        };

        if (rule.premiumCost <= 0)
            rule.premiumCost = legacyCost;

        return rule.HasAnyRule() ? rule : null;
    }

    private static bool HasWardrobeRule(StoryJsonWardrobeOptionRule rule)
    {
        return rule != null &&
            (rule.premiumCost > 0 ||
             !string.IsNullOrWhiteSpace(rule.requiredVariable) ||
             !string.IsNullOrWhiteSpace(rule.requiredItemId) ||
             rule.hideInRestrictedRegions ||
             (rule.hiddenRegionCodes != null && rule.hiddenRegionCodes.Count > 0) ||
             !string.IsNullOrWhiteSpace(rule.purchaseKey) ||
             !string.IsNullOrWhiteSpace(rule.unavailableMessage));
    }

    private static int GetWardrobePremiumCost(StoryJsonNode dto, int index)
    {
        int cost;
        if (TryGetWardrobeCost(dto.premiumCosts, index, out cost) ||
            TryGetWardrobeCost(dto.clothingCosts, index, out cost) ||
            TryGetWardrobeCost(dto.clothesCosts, index, out cost))
        {
            return cost;
        }

        return 0;
    }

    private static bool TryGetWardrobeCost(List<int> costs, int index, out int cost)
    {
        cost = 0;
        if (costs == null || index < 0 || index >= costs.Count)
            return false;

        cost = SaveDataSanitizer.ClampCurrencyValue(costs[index]);
        return true;
    }

    private static string ExportCharacter(
        CharacterData character,
        StoryJsonAssetResolver resolver,
        Dictionary<string, StoryJsonCharacter> exportedCharacters)
    {
        if (character == null)
            return "";

        string id = resolver.GetCharacterId(character);
        if (string.IsNullOrWhiteSpace(id))
            id = character.characterName;
        if (string.IsNullOrWhiteSpace(id))
            id = character.name;

        if (!string.IsNullOrWhiteSpace(id) && !exportedCharacters.ContainsKey(id))
        {
            exportedCharacters[id] = new StoryJsonCharacter
            {
                id = id,
                name = character.characterName ?? character.name,
                asset = resolver.GetAssetId(character)
            };
        }

        return id ?? "";
    }

    private static CharacterData ResolveCharacter(
        string id,
        StoryJsonAssetResolver resolver,
        Dictionary<string, string> characterNames)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        characterNames.TryGetValue(id, out string displayName);
        return resolver.ResolveCharacter(id, displayName);
    }

    private static bool IsHeroSpeaker(string speakerId, CharacterData character)
    {
        if (character != null && character.inheritAppearanceFromPlayer)
            return true;

        string token = NormalizeToken(speakerId);
        switch (token)
        {
            case "hero":
            case "gg":
            case "mainhero":
            case "player":
                return true;
            default:
                return false;
        }
    }

    private static T ResolveAsset<T>(
        string id,
        Func<string, T> resolver,
        string fieldName,
        string nodeId,
        StoryJsonConversionReport report) where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        T asset = resolver(id);
        if (asset == null)
        {
            string message = "Node '" + nodeId + "' references missing asset in '" + fieldName + "': " + id;
            report.AddWarning(message);
            Debug.LogWarning("[StoryJson] " + message);
        }

        return asset;
    }

    private static T TryResolveAsset<T>(
        string id,
        Func<string, T> resolver) where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(id) || resolver == null)
            return null;

        return resolver(id);
    }

    private static T ParseEnum<T>(
        string value,
        T fallback,
        string label,
        string nodeId,
        StoryJsonConversionReport report) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        if (typeof(T) == typeof(CharacterEmotionType) && TryParseCharacterEmotion(value, out var emotion))
            return (T)(object)emotion;

        if (typeof(T) == typeof(AppearanceType) && TryParseAppearanceType(value, out var appearance))
            return (T)(object)appearance;

        if (Enum.TryParse(value, true, out T parsed) && Enum.IsDefined(typeof(T), parsed))
            return parsed;

        report.AddWarning("Node '" + nodeId + "' has unknown " + label + ": " + value);
        return fallback;
    }

    private static ConditionComparison ParseConditionComparison(string value, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ConditionComparison.Equals;

        if (Enum.TryParse(value, true, out ConditionComparison parsed) &&
            Enum.IsDefined(typeof(ConditionComparison), parsed))
        {
            return parsed;
        }

        switch (NormalizeToken(value))
        {
            case "eq":
            case "equal":
            case "equals":
            case "==":
                return ConditionComparison.Equals;
            case "ne":
            case "notequal":
            case "notequals":
            case "!=":
                return ConditionComparison.NotEquals;
            case "gt":
            case "greater":
            case "greaterthan":
            case ">":
                return ConditionComparison.GreaterThan;
            case "gte":
            case "ge":
            case "greaterorequal":
            case "greaterthanorequal":
            case ">=":
                return ConditionComparison.GreaterOrEqual;
            case "lt":
            case "less":
            case "lessthan":
            case "<":
                return ConditionComparison.LessThan;
            case "lte":
            case "le":
            case "lessorequal":
            case "lessthanorequal":
            case "<=":
                return ConditionComparison.LessOrEqual;
            default:
                Debug.LogWarning("[StoryJson] Node '" + nodeId + "' has unknown condition comparison: " + value + ". Equals will be used.");
                return ConditionComparison.Equals;
        }
    }

    private static bool TryParseAppearanceType(string value, out AppearanceType appearance)
    {
        appearance = AppearanceType.Default;

        if (Enum.TryParse(value, true, out appearance) && Enum.IsDefined(typeof(AppearanceType), appearance))
            return true;

        switch (NormalizeToken(value))
        {
            case "latina":
            case "latin":
                appearance = AppearanceType.Latino;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseCharacterEmotion(string value, out CharacterEmotionType emotion)
    {
        emotion = CharacterEmotionType.Idle;

        if (Enum.TryParse(value, true, out emotion) && Enum.IsDefined(typeof(CharacterEmotionType), emotion))
            return true;

        string normalized = NormalizeToken(value);
        switch (normalized)
        {
            case "kasannoyed":
            case "\u0440\u0430\u0437\u0434\u0440\u0430\u0436\u0435\u043d\u0438\u0435":
            case "\u0440\u0430\u0437\u0434\u0440\u0430\u0436\u0435\u043d\u043d\u043e":
            case "\u0440\u0430\u0437\u0434\u0440\u0430\u0436\u0451\u043d\u043d\u043e":
                emotion = CharacterEmotionType.Annoyed;
                return true;
            case "kasindifference":
            case "indifferent":
                emotion = CharacterEmotionType.Indifference;
                return true;
            case "kasscull":
            case "kasskull":
            case "scull":
            case "skull":
                emotion = CharacterEmotionType.Scull;
                return true;
            case "kasrolleyes":
            case "kasrolleye":
                emotion = CharacterEmotionType.EyeRoll;
                return true;
            case "normal":
            case "\u043d\u0435\u0439\u0442\u0440\u0430\u043b\u044c":
            case "\u043d\u0435\u0439\u0442\u0440\u0430\u043b\u044c\u043d\u043e":
                emotion = CharacterEmotionType.Neutral;
                return true;
            case "thoughtful":
            case "thinking":
            case "\u0437\u0430\u0434\u0443\u043c\u0447\u0438\u0432\u043e":
            case "\u0437\u0430\u0434\u0443\u043c\u0447\u0438\u0432\u043e\u0441\u0442\u044c":
            case "\u0437\u0430\u0434\u0443\u043c\u0447\u0438\u0432\u044b\u0439":
            case "\u0437\u0430\u0434\u0443\u043c\u0447\u0438\u0432\u0430\u044f":
                emotion = CharacterEmotionType.Thinking;
                return true;
            case "distracted":
            case "\u043e\u0442\u0432\u043b\u0435\u0447\u0435\u043d\u043d\u043e":
            case "\u043e\u0442\u0432\u043b\u0435\u0447\u0451\u043d\u043d\u043e":
                emotion = CharacterEmotionType.Distraction;
                return true;
            case "rolleyes":
            case "rolleye":
            case "\u0437\u0430\u043a\u0430\u0442\u0430\u043d\u043d\u044b\u0435\u0433\u043b\u0430\u0437\u0430":
            case "\u0437\u0430\u043a\u0430\u0442\u0438\u043b\u0430\u0433\u043b\u0430\u0437\u0430":
            case "\u0437\u0430\u043a\u0430\u0442\u044b\u0432\u0430\u0435\u0442\u0433\u043b\u0430\u0437\u0430":
                emotion = CharacterEmotionType.EyeRoll;
                return true;
            case "closedeyes":
                emotion = CharacterEmotionType.EyesClosed;
                return true;
            case "raisedbrow":
            case "raisedeyebrow":
            case "\u0432\u044b\u0433\u043d\u0443\u0442\u0430\u044f\u0431\u0440\u043e\u0432\u044c":
            case "\u0432\u044b\u0433\u0438\u0431\u0430\u0435\u0442\u0431\u0440\u043e\u0432\u044c":
            case "\u0432\u044b\u0433\u043d\u0443\u043b\u0431\u0440\u043e\u0432\u044c":
                emotion = CharacterEmotionType.RaisedEyebrow;
                return true;
            case "widesmile":
            case "\u0448\u0438\u0440\u043e\u043a\u0430\u044f\u0443\u043b\u044b\u0431\u043a\u0430":
                emotion = CharacterEmotionType.WideSmile;
                return true;
            case "smile":
            case "\u0443\u043b\u044b\u0431\u043a\u0430":
            case "\u0443\u043b\u044b\u0431\u0430\u0435\u0442\u0441\u044f":
                emotion = CharacterEmotionType.Smile;
                return true;
            case "lookaside":
            case "looktotheside":
            case "\u0433\u043b\u0430\u0437\u0430\u0432\u0441\u0442\u043e\u0440\u043e\u043d\u0443":
            case "\u0432\u0437\u0433\u043b\u044f\u0434\u0432\u0441\u0442\u043e\u0440\u043e\u043d\u0443":
            case "\u0432\u0441\u0442\u043e\u0440\u043e\u043d\u0443":
                emotion = CharacterEmotionType.LookToSide;
                return true;
            case "looktoinside":
            case "looktotheinside":
            case "looktoinsite":
            case "looktotheinsite":
            case "looltotheinsite":
                emotion = CharacterEmotionType.LookToInside;
                return true;
            case "\u0441\u0435\u0440\u044c\u0435\u0437\u043d\u043e":
            case "\u0441\u0435\u0440\u044c\u0451\u0437\u043d\u043e":
            case "\u0441\u0435\u0440\u044c\u0435\u0437\u043d\u044b\u0439":
            case "\u0441\u0435\u0440\u044c\u0451\u0437\u043d\u044b\u0439":
            case "\u0441\u0442\u0440\u043e\u0433\u043e":
                emotion = CharacterEmotionType.Serious;
                return true;
            case "\u0443\u0434\u0438\u0432\u043b\u0435\u043d\u0438\u0435":
            case "\u0443\u0434\u0438\u0432\u043b\u0435\u043d\u043d\u043e":
            case "\u0443\u0434\u0438\u0432\u043b\u0451\u043d\u043d\u043e":
                emotion = CharacterEmotionType.Surprised;
                return true;
            case "\u0441\u043c\u0443\u0449\u0435\u043d\u0438\u0435":
            case "\u0441\u043c\u0443\u0449\u0435\u043d\u043d\u043e":
            case "\u0441\u043c\u0443\u0449\u0451\u043d\u043d\u043e":
                emotion = CharacterEmotionType.Embarrassed;
                return true;
            case "\u0440\u0430\u0441\u0442\u0435\u0440\u044f\u043d\u043d\u043e":
            case "\u0440\u0430\u0441\u0442\u0435\u0440\u044f\u043d\u043d\u043e\u0441\u0442\u044c":
                emotion = CharacterEmotionType.Confused;
                return true;
            case "\u0433\u0440\u0443\u0441\u0442\u044c":
                emotion = CharacterEmotionType.Sad;
                return true;
            case "\u043f\u043e\u0434\u0436\u0430\u0442\u044b\u0435\u0433\u0443\u0431\u044b":
                emotion = CharacterEmotionType.Frown;
                return true;
            case "\u0432\u043e\u0437\u043c\u0443\u0449\u0435\u043d\u0438\u0435":
                emotion = CharacterEmotionType.Indignant;
                return true;
            case "\u0437\u043b\u043e\u0441\u0442\u044c":
            case "\u0437\u043b\u043e":
            case "\u0441\u0435\u0440\u0434\u0438\u0442\u043e":
            case "\u0441\u0435\u0440\u0434\u0438\u0442\u044b\u0439":
                emotion = CharacterEmotionType.Angry;
                return true;
            case "\u0438\u0441\u043f\u0443\u0433\u0430\u043d\u043d\u043e":
            case "\u0438\u0441\u043f\u0443\u0433":
            case "\u0441\u0442\u0440\u0430\u0445":
                emotion = CharacterEmotionType.Funk;
                return true;
            case "\u0445\u043c\u0443\u0440\u043e":
            case "\u0445\u043c\u0443\u0440\u044b\u0439":
                emotion = CharacterEmotionType.Scull;
                return true;
            case "\u0447\u0435\u0440\u043d\u044b\u0439\u0441\u0438\u043b\u0443\u044d\u0442":
            case "\u0447\u0435\u0440\u043d\u044b\u0439\u0441\u0438\u043b\u0443\u0435\u0442":
            case "\u0442\u0451\u043c\u043d\u044b\u0439\u0441\u0438\u043b\u0443\u044d\u0442":
            case "\u0442\u0435\u043c\u043d\u044b\u0439\u0441\u0438\u043b\u0443\u044d\u0442":
            case "\u0441\u0438\u043b\u0443\u044d\u0442\u0434\u0435\u0432\u043e\u0447\u043a\u04381":
            case "\u0441\u0438\u043b\u0443\u0435\u0442\u0434\u0435\u0432\u043e\u0447\u043a\u04381":
                emotion = CharacterEmotionType.Idle;
                return true;
            case "\u0443\u0445\u043c\u044b\u043b\u043a\u0430":
            case "\u0441\u0443\u0445\u043c\u044b\u043b\u043a\u043e\u0439":
                emotion = CharacterEmotionType.Smirk;
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value
            .Trim()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .ToLowerInvariant();
    }

    private static ClothingItem ResolveClothingOrPlaceholder(
        string clothingId,
        StoryJsonAssetResolver resolver,
        string nodeId,
        StoryJsonConversionReport report)
    {
        var clothing = ResolveAsset(clothingId, resolver.ResolveClothing, "clothes", nodeId, report);
        if (clothing != null || string.IsNullOrWhiteSpace(clothingId))
            return clothing;

        var placeholder = ScriptableObject.CreateInstance<ClothingItem>();
        placeholder.hideFlags = HideFlags.DontSave;
        placeholder.name = "MissingClothing_" + clothingId;
        placeholder.id = clothingId;
        report.AddWarning("Node '" + nodeId + "' uses missing clothing '" + clothingId + "'. Runtime placeholder was created so the story can continue.");
        return placeholder;
    }

    private static T AddNode<T>(StoryGraph graph, string id) where T : BaseStoryNode
    {
        var node = graph.AddNode<T>();
        node.hideFlags = HideFlags.DontSave;
        node.guid = id;
        node.name = typeof(T).Name + "_" + id;
        return node;
    }

    private static void EnsureDynamicOutput(BaseStoryNode node, string portName)
    {
        if (node != null && node.GetOutputPort(portName) == null)
            node.AddDynamicOutput(typeof(BaseStoryNode), Node.ConnectionType.Multiple, Node.TypeConstraint.None, portName);
    }

    private static string GetConnectedId(BaseStoryNode node, string portName)
    {
        var port = node != null ? node.GetOutputPort(portName) : null;
        var connected = port?.Connection?.node as BaseStoryNode;
        return connected != null ? GetNodeId(connected) : "";
    }

    private static string GetNodeId(BaseStoryNode node)
    {
        return node == null ? "" : FirstNonEmpty(node.guid, node.name);
    }

    private static string NormalizeId(string value)
    {
        return (value ?? "").Trim();
    }

    private static string NormalizeType(string value)
    {
        string type = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(type))
            return "";

        string normalized = type.Replace("-", "").Replace("_", "").ToLowerInvariant();
        switch (normalized)
        {
            case "start": return StoryJsonTypes.Start;
            case "scene":
            case "scenesetup": return StoryJsonTypes.Scene;
            case "dialog":
            case "dialogue": return StoryJsonTypes.Dialogue;
            case "cutscene":
            case "cinematic":
            case "cg": return StoryJsonTypes.Cutscene;
            case "choice": return StoryJsonTypes.Choice;
            case "statchange": return StoryJsonTypes.StatChange;
            case "variablechange": return StoryJsonTypes.VariableChange;
            case "condition": return StoryJsonTypes.Condition;
            case "premium": return StoryJsonTypes.Premium;
            case "camera": return StoryJsonTypes.Camera;
            case "image": return StoryJsonTypes.Image;
            case "phonedialogue":
            case "phone": return StoryJsonTypes.PhoneDialogue;
            case "effect": return StoryJsonTypes.Effect;
            case "banner": return StoryJsonTypes.Banner;
            case "storybanner": return StoryJsonTypes.Banner;
            case "titleoverlay": return StoryJsonTypes.Banner;
            case "name": return StoryJsonTypes.NameChoice;
            case "namechoice": return StoryJsonTypes.NameChoice;
            case "appearancechoice": return StoryJsonTypes.AppearanceChoice;
            case "wardrobechoice": return StoryJsonTypes.WardrobeChoice;
            case "addclothing": return StoryJsonTypes.AddClothing;
            case "openwardrobe": return StoryJsonTypes.OpenWardrobe;
            case "wardrobecheck": return StoryJsonTypes.WardrobeCheck;
            default: return type;
        }
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "";
    }
}
