using System;
using System.Collections.Generic;
using UnityEngine;
using XNode;

public static class RemoteStoryGraphImporter
{
    const int MaxRemoteGraphJsonChars = 1024 * 1024;
    const int MaxRemoteScenes = 100;
    const int MaxRemoteNodesPerArray = 500;
    const int MaxRemoteChoices = 64;
    const int MaxRemoteBranchDepth = 16;

    static readonly Dictionary<string, StoryGraph> ImportedGraphs = new Dictionary<string, StoryGraph>();

    public static bool TryBuildGraph(
        RemoteEpisodeGraphCacheEntry entry,
        out StoryGraph graph,
        out string reason,
        StoryJsonAssetResolver resolver = null)
    {
        graph = null;
        reason = "empty remote graph entry";

        if (entry == null || string.IsNullOrWhiteSpace(entry.episodeId))
            return false;

        string safeEpisodeId = SaveDataSanitizer.SanitizeIdentifier(entry.episodeId);
        if (string.IsNullOrEmpty(safeEpisodeId))
            return false;

        resolver ??= new StoryJsonAssetResolver();
        string cacheKey = safeEpisodeId + "::" + SaveDataSanitizer.SanitizeIdentifier(entry.contentVersion ?? "0") + "::" + resolver.CacheKey;
        if (ImportedGraphs.TryGetValue(cacheKey, out graph) && graph != null)
        {
            reason = null;
            return true;
        }

        if (!TryBuildGraph(safeEpisodeId, entry.graphJson, out graph, out reason, resolver))
            return false;

        ImportedGraphs[cacheKey] = graph;
        return true;
    }

    public static bool TryBuildGraph(
        string episodeId,
        string graphJson,
        out StoryGraph graph,
        out string reason,
        StoryJsonAssetResolver resolver = null)
    {
        graph = null;
        reason = null;
        resolver ??= new StoryJsonAssetResolver();

        if (string.IsNullOrWhiteSpace(graphJson))
        {
            reason = "empty remote graph payload";
            return false;
        }

        if (graphJson.Length > MaxRemoteGraphJsonChars)
        {
            reason = "remote graph payload is too large";
            return false;
        }

        episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        if (string.IsNullOrEmpty(episodeId))
            episodeId = "remote_episode";

        if (StoryJsonConverter.IsCanonicalJson(graphJson))
            return StoryJsonConverter.TryBuildGraph(graphJson, episodeId, out graph, out reason, resolver);

        if (!TryParseDocument(graphJson, out var document, out reason))
            return false;

        var importContext = new ImportContext(episodeId ?? "", resolver);
        if (!TryBuildScenes(importContext, document.scenes, out reason))
            return false;

        graph = importContext.graph;
        return graph != null;
    }

    static bool TryParseDocument(string graphJson, out RemoteGraphDocument document, out string reason)
    {
        document = null;
        reason = null;

        string rawScenes = NetworkJson.GetRawValue(graphJson, "scenes");
        if (!string.IsNullOrEmpty(rawScenes))
            return TryParseScenesDocument(graphJson, out document, out reason);

        string rawNodes = NetworkJson.GetRawValue(graphJson, "nodes");
        if (!string.IsNullOrEmpty(rawNodes))
            return TryParseFlatNodesDocument(graphJson, out document, out reason);

        reason = "unsupported remote graph schema";
        return false;
    }

    static bool TryParseScenesDocument(string graphJson, out RemoteGraphDocument document, out string reason)
    {
        document = null;
        reason = null;

        var scenes = ParseScenesArray(NetworkJson.GetRawValue(graphJson, "scenes"));
        if (scenes.Count == 0)
        {
            reason = "remote graph has no scenes";
            return false;
        }

        document = new RemoteGraphDocument
        {
            scenes = scenes
        };

        return true;
    }

    static bool TryParseFlatNodesDocument(string graphJson, out RemoteGraphDocument document, out string reason)
    {
        document = null;
        reason = null;

        var nodes = ParseNodesArray(NetworkJson.GetRawValue(graphJson, "nodes"), 0);
        if (nodes.Count == 0)
        {
            reason = "remote graph has no nodes";
            return false;
        }

        document = new RemoteGraphDocument
        {
            scenes = new List<RemoteSceneDto>
            {
                new RemoteSceneDto
                {
                    sceneDescription = SanitizeRemoteText(ParseSceneMetadataValue(graphJson, "sceneDescription")),
                    suggestedBackground = SaveDataSanitizer.SanitizeIdentifier(ParseSceneMetadataValue(graphJson, "suggestedBackground")),
                    suggestedMusic = SaveDataSanitizer.SanitizeIdentifier(ParseSceneMetadataValue(graphJson, "suggestedMusic")),
                    nodes = nodes
                }
            }
        };

        return true;
    }

    static List<RemoteSceneDto> ParseScenesArray(string rawScenes)
    {
        var result = new List<RemoteSceneDto>();
        if (string.IsNullOrWhiteSpace(rawScenes))
            return result;

        foreach (var rawScene in NetworkJson.GetArrayItems(rawScenes))
        {
            if (result.Count >= MaxRemoteScenes)
                break;

            if (string.IsNullOrWhiteSpace(rawScene))
                continue;

            result.Add(new RemoteSceneDto
            {
                sceneDescription = SanitizeRemoteText(NetworkJson.GetString(rawScene, "sceneDescription")),
                suggestedBackground = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(rawScene, "suggestedBackground")),
                suggestedMusic = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(rawScene, "suggestedMusic")),
                nodes = ParseNodesArray(NetworkJson.GetRawValue(rawScene, "nodes"), 0)
            });
        }

        return result;
    }

    static List<RemoteNodeDto> ParseNodesArray(string rawNodes, int depth)
    {
        var result = new List<RemoteNodeDto>();
        if (string.IsNullOrWhiteSpace(rawNodes) || depth > MaxRemoteBranchDepth)
            return result;

        foreach (var rawNode in NetworkJson.GetArrayItems(rawNodes))
        {
            if (result.Count >= MaxRemoteNodesPerArray)
                break;

            if (string.IsNullOrWhiteSpace(rawNode))
                continue;

            result.Add(ParseNode(rawNode, depth));
        }

        return result;
    }

    static RemoteNodeDto ParseNode(string rawNode, int depth)
    {
        return new RemoteNodeDto
        {
            guid = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(rawNode, "guid")),
            type = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(rawNode, "type")),
            lines = ParseLinesArray(NetworkJson.GetRawValue(rawNode, "lines")),
            choicePrompt = SanitizeRemoteText(NetworkJson.GetString(rawNode, "choicePrompt")),
            choices = ParseChoiceOptionsArray(NetworkJson.GetRawValue(rawNode, "choices"), depth),
            statId = SaveDataSanitizer.SanitizeStatKey(NetworkJson.GetString(rawNode, "statId")),
            statDelta = SaveDataSanitizer.ClampStatValue(ParseInt(rawNode, "statDelta")),
            statDisplayName = SanitizeRemoteText(NetworkJson.GetString(rawNode, "statDisplayName"))
        };
    }

    static List<RemoteLineDto> ParseLinesArray(string rawLines)
    {
        var result = new List<RemoteLineDto>();
        if (string.IsNullOrWhiteSpace(rawLines))
            return result;

        foreach (var rawLine in NetworkJson.GetArrayItems(rawLines))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            result.Add(new RemoteLineDto
            {
                speaker = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(rawLine, "speaker")),
                emotion = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(rawLine, "emotion")),
                text = SanitizeRemoteText(NetworkJson.GetString(rawLine, "text"))
            });
        }

        return result;
    }

    static List<RemoteChoiceOptionDto> ParseChoiceOptionsArray(string rawChoices, int depth)
    {
        var result = new List<RemoteChoiceOptionDto>();
        if (string.IsNullOrWhiteSpace(rawChoices))
            return result;

        foreach (var rawChoice in NetworkJson.GetArrayItems(rawChoices))
        {
            if (result.Count >= MaxRemoteChoices)
                break;

            if (string.IsNullOrWhiteSpace(rawChoice))
                continue;

            result.Add(new RemoteChoiceOptionDto
            {
                text = SanitizeRemoteText(NetworkJson.GetString(rawChoice, "text")),
                isPremium = ParseBool(rawChoice, "isPremium"),
                premiumCost = SaveDataSanitizer.ClampCurrencyValue(ParseInt(rawChoice, "premiumCost")),
                requiredVariable = SaveDataSanitizer.SanitizeStatKey(NetworkJson.GetString(rawChoice, "requiredVariable")),
                requiredValue = SaveDataSanitizer.ClampStatValue(ParseInt(rawChoice, "requiredValue")),
                hideWhenRequirementNotMet = ParseBool(rawChoice, "hideWhenRequirementNotMet"),
                hideInRestrictedRegions = ParseBool(rawChoice, "hideInRestrictedRegions"),
                hiddenRegionCodes = ParseHiddenRegionCodes(rawChoice),
                branch = depth < MaxRemoteBranchDepth
                    ? ParseNodesArray(NetworkJson.GetRawValue(rawChoice, "branch"), depth + 1)
                    : new List<RemoteNodeDto>()
            });
        }

        return result;
    }

    static List<string> ParseHiddenRegionCodes(string rawChoice)
    {
        var result = new List<string>();
        AddRegionCodes(result, NetworkJson.GetStringList(rawChoice, "hiddenRegionCodes"));
        AddRegionCodes(result, NetworkJson.GetStringList(rawChoice, "hiddenRegions"));
        AddRegionCodes(result, NetworkJson.GetStringList(rawChoice, "restrictedRegions"));
        return result;
    }

    static void AddRegionCodes(List<string> target, List<string> source)
    {
        if (target == null || source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            string code = RegionAccessGate.NormalizeRegionCode(source[i]);
            if (string.IsNullOrEmpty(code) || target.Exists(existing => string.Equals(RegionAccessGate.NormalizeRegionCode(existing), code, StringComparison.OrdinalIgnoreCase)))
                continue;

            target.Add(code);
        }
    }

    static string ParseSceneMetadataValue(string graphJson, string key)
    {
        var rawScene = NetworkJson.GetRawValue(graphJson, "scene");
        return FirstNonEmpty(
            NetworkJson.GetString(graphJson, key),
            NetworkJson.GetString(rawScene, key));
    }

    static int ParseInt(string json, string key)
    {
        var raw = NetworkJson.GetRawValue(json, key);
        return int.TryParse(raw, out var value) ? value : 0;
    }

    static bool ParseBool(string json, string key)
    {
        var raw = NetworkJson.GetRawValue(json, key);
        if (bool.TryParse(raw, out var value))
            return value;

        return raw == "1";
    }

    static bool TryBuildScenes(ImportContext context, List<RemoteSceneDto> scenes, out string reason)
    {
        reason = null;

        var startNode = AddNode<StartNode>(context.graph, BuildStableGuid(context.episodeId, "start"));
        BaseStoryNode lastExit = startNode;
        string lastPortName = "exit";

        for (int sceneIndex = 0; sceneIndex < scenes.Count; sceneIndex++)
        {
            var scene = scenes[sceneIndex] ?? new RemoteSceneDto();
            var sceneNode = AddNode<SceneSetupNode>(
                context.graph,
                BuildStableGuid(context.episodeId, "scene/" + sceneIndex));

            sceneNode.sceneLabel = scene.sceneDescription ?? "";
            sceneNode.suggestedBackground = scene.suggestedBackground ?? "";
            sceneNode.suggestedMusic = scene.suggestedMusic ?? "";
            sceneNode.sceneData = CreateSceneSetupData(context.episodeId, sceneIndex, scene.sceneDescription);

            Connect(lastExit, lastPortName, sceneNode, "enter");
            lastExit = sceneNode;
            lastPortName = "exit";

            if (!TryBuildNodeChain(
                context,
                scene.nodes,
                "scene/" + sceneIndex + "/node",
                out var chain,
                out reason))
            {
                return false;
            }

            if (chain.firstNode != null)
                Connect(lastExit, lastPortName, chain.firstNode, "enter");

            lastExit = chain.lastNode ?? lastExit;
            lastPortName = chain.lastPortName ?? "exit";
        }

        return true;
    }

    static bool TryBuildNodeChain(
        ImportContext context,
        List<RemoteNodeDto> nodes,
        string pathPrefix,
        out ChainResult result,
        out string reason)
    {
        result = new ChainResult();
        reason = null;

        if (nodes == null || nodes.Count == 0)
            return true;

        BaseStoryNode previous = null;
        string previousPort = "exit";

        for (int index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index] ?? new RemoteNodeDto();
            string nodePath = pathPrefix + "/" + index;

            if (!TryBuildNode(context, node, nodePath, out var createdNode, out reason))
                return false;

            if (createdNode == null)
                continue;

            if (result.firstNode == null)
                result.firstNode = createdNode;

            if (previous != null)
                Connect(previous, previousPort, createdNode, "enter");

            previous = createdNode;
            previousPort = "exit";
        }

        result.lastNode = previous;
        result.lastPortName = previousPort;
        return true;
    }

    static bool TryBuildNode(
        ImportContext context,
        RemoteNodeDto data,
        string nodePath,
        out BaseStoryNode node,
        out string reason)
    {
        node = null;
        reason = null;

        string type = (data.type ?? "").Trim().ToLowerInvariant();
        switch (type)
        {
            case "dialog":
            case "dialogue":
                node = BuildDialogueNode(context, data, nodePath);
                return true;

            case "choice":
                return TryBuildChoiceNode(context, data, nodePath, out node, out reason);

            case "statchange":
            case "stat-change":
            case "stat_change":
                node = BuildStatChangeNode(context, data, nodePath);
                return true;

            default:
                reason = "unsupported remote node type: " + (data.type ?? "<empty>");
                return false;
        }
    }

    static DialogueNode BuildDialogueNode(ImportContext context, RemoteNodeDto data, string nodePath)
    {
        var node = AddNode<DialogueNode>(context.graph, BuildStableGuid(context.episodeId, nodePath, data.guid));
        node.lines = new List<DialogueLine>();
        node.activeCharacters = new List<DialogueCharacterEntry>();

        if (data.lines == null)
            return node;

        var seenSpeakers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var lineData in data.lines)
        {
            if (lineData == null)
                continue;

            CharacterData speaker = ResolveSpeaker(context, lineData.speaker);
            var emotion = ParseEmotion(lineData.emotion);

            node.lines.Add(new DialogueLine
            {
                speakerId = SaveDataSanitizer.SanitizeIdentifier(lineData.speaker),
                speakerNameHint = lineData.speaker ?? "",
                speaker = speaker,
                emotion = emotion,
                richText = lineData.text ?? ""
            });

            if (string.IsNullOrWhiteSpace(lineData.speaker) || !seenSpeakers.Add(lineData.speaker))
                continue;

            node.activeCharacters.Add(new DialogueCharacterEntry
            {
                character = speaker,
                emotion = emotion,
                position = StoryGraphSpeakerPositionResolver.GetDefaultPosition(lineData.speaker, speaker),
                speakerNameHint = lineData.speaker
            });
        }

        return node;
    }

    static bool TryBuildChoiceNode(
        ImportContext context,
        RemoteNodeDto data,
        string nodePath,
        out BaseStoryNode node,
        out string reason)
    {
        reason = null;

        var choiceNode = AddNode<ChoiceNode>(context.graph, BuildStableGuid(context.episodeId, nodePath, data.guid));
        choiceNode.lines = new List<DialogueLine>();
        choiceNode.options = new List<ChoiceOption>();
        choiceNode.choices = new List<BaseStoryNode>();

        if (!string.IsNullOrWhiteSpace(data.choicePrompt))
        {
            choiceNode.lines.Add(new DialogueLine
            {
                richText = data.choicePrompt
            });
        }

        if (data.choices != null)
        {
            for (int optionIndex = 0; optionIndex < data.choices.Count; optionIndex++)
            {
                var optionData = data.choices[optionIndex] ?? new RemoteChoiceOptionDto();
                choiceNode.options.Add(new ChoiceOption
                {
                    text = optionData.text ?? "",
                    isPremium = optionData.isPremium,
                    premiumCost = SaveDataSanitizer.ClampCurrencyValue(optionData.premiumCost),
                    requiredVariable = optionData.requiredVariable ?? "",
                    requiredValue = SaveDataSanitizer.ClampStatValue(optionData.requiredValue),
                    hideWhenRequirementNotMet = optionData.hideWhenRequirementNotMet,
                    hideInRestrictedRegions = optionData.hideInRestrictedRegions,
                    hiddenRegionCodes = optionData.hiddenRegionCodes != null
                        ? new List<string>(optionData.hiddenRegionCodes)
                        : new List<string>()
                });
                choiceNode.choices.Add(null);

                string portName = "choices " + optionIndex;
                if (choiceNode.GetOutputPort(portName) == null)
                    choiceNode.AddDynamicOutput(typeof(BaseStoryNode), fieldName: portName);

                if (!TryBuildNodeChain(
                    context,
                    optionData.branch,
                    nodePath + "/choice/" + optionIndex,
                    out var branchResult,
                    out reason))
                {
                    node = null;
                    return false;
                }

                if (branchResult.firstNode != null)
                    Connect(choiceNode, portName, branchResult.firstNode, "enter");
            }
        }

        node = choiceNode;
        return true;
    }

    static StatChangeNode BuildStatChangeNode(ImportContext context, RemoteNodeDto data, string nodePath)
    {
        var node = AddNode<StatChangeNode>(context.graph, BuildStableGuid(context.episodeId, nodePath, data.guid));
        node.statId = data.statId ?? "";
        node.delta = SaveDataSanitizer.ClampStatValue(data.statDelta);
        node.displayName = data.statDisplayName ?? "";
        return node;
    }

    static T AddNode<T>(StoryGraph graph, string guid) where T : BaseStoryNode
    {
        var node = graph.AddNode<T>();
        node.hideFlags = HideFlags.DontSave;
        node.guid = guid;
        node.name = typeof(T).Name;
        return node;
    }

    static void Connect(BaseStoryNode outputNode, string outputPortName, BaseStoryNode inputNode, string inputPortName)
    {
        if (outputNode == null || inputNode == null)
            return;

        var outputPort = outputNode.GetOutputPort(outputPortName);
        var inputPort = inputNode.GetInputPort(inputPortName);
        if (outputPort != null && inputPort != null)
            outputPort.Connect(inputPort);
    }

    static CharacterData ResolveSpeaker(ImportContext context, string speakerName)
    {
        if (string.IsNullOrWhiteSpace(speakerName))
            return null;

        if (context.speakers.TryGetValue(speakerName, out var character))
            return character;

        character = context.resolver.ResolveCharacter(speakerName, speakerName);
        context.speakers[speakerName] = character;
        return character;
    }

    static SceneSetupData CreateSceneSetupData(string episodeId, int sceneIndex, string sceneDescription)
    {
        var sceneData = ScriptableObject.CreateInstance<SceneSetupData>();
        sceneData.hideFlags = HideFlags.DontSave;
        sceneData.name = "RemoteScene_" + episodeId + "_" + sceneIndex;
        return sceneData;
    }

    static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "";
    }

    static CharacterEmotionType ParseEmotion(string value)
    {
        return Enum.TryParse(value ?? "", true, out CharacterEmotionType emotion)
            ? emotion
            : CharacterEmotionType.Idle;
    }

    static string SanitizeRemoteText(string value)
    {
        return SaveDataSanitizer.SanitizeContentText(value);
    }

    static string BuildStableGuid(string episodeId, string path, string explicitGuid = null)
    {
        string safeGuid = SaveDataSanitizer.SanitizeIdentifier(explicitGuid);
        if (!string.IsNullOrWhiteSpace(safeGuid))
            return safeGuid;

        string safeEpisodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        string safePath = SaveDataSanitizer.SanitizeIdentifier(path);
        return (string.IsNullOrEmpty(safeEpisodeId) ? "episode" : safeEpisodeId) + ":" + safePath;
    }

    class ImportContext
    {
        public readonly string episodeId;
        public readonly StoryGraph graph;
        public readonly StoryJsonAssetResolver resolver;
        public readonly Dictionary<string, CharacterData> speakers =
            new Dictionary<string, CharacterData>(StringComparer.OrdinalIgnoreCase);

        public ImportContext(string episodeId, StoryJsonAssetResolver resolver)
        {
            this.episodeId = episodeId;
            this.resolver = resolver ?? new StoryJsonAssetResolver();
            graph = ScriptableObject.CreateInstance<StoryGraph>();
            graph.hideFlags = HideFlags.DontSave;
            graph.name = "Remote_" + (string.IsNullOrWhiteSpace(episodeId) ? "Episode" : episodeId);
            graph.episodeId = SaveDataSanitizer.SanitizeIdentifier(episodeId);
        }
    }

    class ChainResult
    {
        public BaseStoryNode firstNode;
        public BaseStoryNode lastNode;
        public string lastPortName = "exit";
    }
}
