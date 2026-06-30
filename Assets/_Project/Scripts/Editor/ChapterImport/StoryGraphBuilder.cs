#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;

public static class StoryGraphBuilder
{
    private const float NodeWidth = 320f;
    private const float EstimatedNodeHeight = 200f;
    private const float HorizontalGap = 80f;
    private const float VerticalGap = 60f;

    public static string Build(
        ParsedChapterData data,
        string graphName,
        string savePath,
        bool matchAssets = true)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        savePath = NormalizeSavePath(savePath);
        graphName = string.IsNullOrWhiteSpace(graphName) ? "StoryGraph" : graphName.Trim();

        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
            AssetDatabase.Refresh();
        }

        var graph = ScriptableObject.CreateInstance<StoryGraph>();
        graph.name = graphName;

        string assetPath = Path.Combine(savePath, MakeSafeAssetFileName(graphName) + ".asset")
            .Replace("\\", "/");
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

        AssetDatabase.CreateAsset(graph, assetPath);

        var startNode = AddNode<StartNode>(graph, assetPath, Vector2.zero);
        BaseStoryNode lastExit = startNode;
        string lastPortName = "exit";
        float x = NodeWidth + HorizontalGap;

        foreach (ParsedSceneData scene in data.scenes)
        {
            var sceneNode = AddNode<SceneSetupNode>(graph, assetPath, new Vector2(x, 0f));
            sceneNode.name = $"Scene_{TruncateName(scene.sceneDescription)}";
            sceneNode.suggestedBackground = scene.suggestedBackground;
            sceneNode.suggestedMusic = scene.suggestedMusic;

            if (matchAssets)
            {
                TrySetBackground(sceneNode, scene.suggestedBackground);
                TrySetMusic(sceneNode, scene.suggestedMusic);
            }

            EditorUtility.SetDirty(sceneNode);

            Connect(lastExit, lastPortName, sceneNode, "enter");
            lastExit = sceneNode;
            lastPortName = "exit";
            x += NodeWidth + HorizontalGap;

            var context = new BuildContext(graph, assetPath);
            ChainResult chain = BuildNodeChain(scene.nodes, context, x, 0f);

            if (chain.firstNode != null)
                Connect(lastExit, lastPortName, chain.firstNode, "enter");

            lastExit = chain.lastNode ?? lastExit;
            lastPortName = chain.lastPortName ?? "exit";
            x = chain.nextX;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return assetPath;
    }

    private static ChainResult BuildNodeChain(
        List<ParsedStoryNodeData> nodes,
        BuildContext context,
        float startX,
        float startY)
    {
        var result = new ChainResult { nextX = startX };
        BaseStoryNode previousNode = null;
        string previousPort = "exit";
        float x = startX;

        foreach (ParsedStoryNodeData nodeData in nodes)
        {
            BaseStoryNode createdNode = null;

            if (nodeData.type == "dialogue")
            {
                createdNode = BuildDialogueNode(nodeData, context, new Vector2(x, startY));
            }
            else if (nodeData.type == "choice")
            {
                createdNode = BuildChoiceNode(nodeData, context, new Vector2(x, startY), out float choiceWidth);
                x += choiceWidth - (NodeWidth + HorizontalGap);
            }

            if (createdNode == null)
                continue;

            if (result.firstNode == null)
                result.firstNode = createdNode;

            if (previousNode != null)
                Connect(previousNode, previousPort, createdNode, "enter");

            previousNode = createdNode;
            previousPort = "exit";
            x += NodeWidth + HorizontalGap;
        }

        result.lastNode = previousNode;
        result.lastPortName = previousPort;
        result.nextX = x;

        return result;
    }

    private static DialogueNode BuildDialogueNode(
        ParsedStoryNodeData data,
        BuildContext context,
        Vector2 position)
    {
        var node = AddNode<DialogueNode>(context.graph, context.assetPath, position);
        node.lines = new List<DialogueLine>();

        foreach (ParsedDialogueLineData lineData in data.lines)
        {
            node.lines.Add(new DialogueLine
            {
                speakerId = SaveDataSanitizer.SanitizeIdentifier(lineData.speaker),
                speakerNameHint = lineData.speaker ?? "",
                speaker = lineData.characterData,
                emotion = ParseEmotion(lineData.emotion),
                richText = lineData.text
            });
        }

        node.activeCharacters = new List<DialogueCharacterEntry>();
        var seenSpeakers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ParsedDialogueLineData lineData in data.lines)
        {
            if (string.IsNullOrEmpty(lineData.speaker) || !seenSpeakers.Add(lineData.speaker))
                continue;

            node.activeCharacters.Add(new DialogueCharacterEntry
            {
                character = lineData.characterData,
                emotion = ParseEmotion(lineData.emotion),
                position = StoryGraphSpeakerPositionResolver.GetDefaultPosition(lineData.speaker, lineData.characterData),
                speakerNameHint = lineData.speaker
            });
        }

        EditorUtility.SetDirty(node);
        return node;
    }

    private static ChoiceNode BuildChoiceNode(
        ParsedStoryNodeData data,
        BuildContext context,
        Vector2 position,
        out float totalWidth)
    {
        var node = AddNode<ChoiceNode>(context.graph, context.assetPath, position);
        node.options = new List<ChoiceOption>();

        if (!string.IsNullOrEmpty(data.choicePrompt))
        {
            node.lines = new List<DialogueLine>
            {
                new DialogueLine { richText = data.choicePrompt }
            };
        }

        float branchX = position.x + NodeWidth + HorizontalGap;
        float maxBranchWidth = 0f;

        for (int i = 0; i < data.choices.Count; i++)
        {
            ParsedChoiceOptionData choiceData = data.choices[i];
            node.options.Add(new ChoiceOption { text = choiceData.text });

            float branchY = position.y + i * (EstimatedNodeHeight + VerticalGap);
            var branchContext = new BuildContext(context.graph, context.assetPath);
            ChainResult branch = BuildNodeChain(choiceData.branch, branchContext, branchX, branchY);

            if (branch.firstNode != null)
            {
                NodePort outputPort = node.GetOutputPort("choices " + i);
                NodePort inputPort = branch.firstNode.GetInputPort("enter");
                if (outputPort != null && inputPort != null)
                    outputPort.Connect(inputPort);
            }

            float branchWidth = branch.nextX - branchX;
            if (branchWidth > maxBranchWidth)
                maxBranchWidth = branchWidth;
        }

        EditorUtility.SetDirty(node);
        totalWidth = NodeWidth + HorizontalGap + maxBranchWidth;
        return node;
    }

    private static T AddNode<T>(StoryGraph graph, string assetPath, Vector2 position) where T : BaseStoryNode
    {
        T node = graph.AddNode<T>();
        node.position = position;
        node.graph = graph;
        node.guid = Guid.NewGuid().ToString();
        AssetDatabase.AddObjectToAsset(node, assetPath);
        return node;
    }

    private static void Connect(BaseStoryNode from, string fromPort, BaseStoryNode to, string toPort)
    {
        NodePort output = from.GetOutputPort(fromPort);
        NodePort input = to.GetInputPort(toPort);
        if (output != null && input != null && !output.IsConnectedTo(input))
            output.Connect(input);
    }

    private static void TrySetBackground(SceneSetupNode node, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        foreach (string guid in AssetDatabase.FindAssets($"{name} t:Sprite"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                continue;

            SceneSetupData sceneData = EnsureSceneData(node);
            sceneData.background = sprite;
            EditorUtility.SetDirty(sceneData);
            EditorUtility.SetDirty(node);
            return;
        }
    }

    private static void TrySetMusic(SceneSetupNode node, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        foreach (string guid in AssetDatabase.FindAssets($"{name} t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                continue;

            SceneSetupData sceneData = EnsureSceneData(node);
            sceneData.music = clip;
            EditorUtility.SetDirty(sceneData);
            EditorUtility.SetDirty(node);
            return;
        }
    }

    private static SceneSetupData EnsureSceneData(SceneSetupNode node)
    {
        if (node.sceneData != null)
            return node.sceneData;

        var sceneData = ScriptableObject.CreateInstance<SceneSetupData>();
        sceneData.name = "SceneData_" + node.name;
        node.sceneData = sceneData;

        string graphPath = AssetDatabase.GetAssetPath(node.graph);
        if (!string.IsNullOrEmpty(graphPath))
            AssetDatabase.AddObjectToAsset(sceneData, graphPath);

        return sceneData;
    }

    private static string NormalizeSavePath(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath))
            savePath = "Assets/_MyProject/Data/Stories/Generated/Graphs";

        savePath = savePath.Trim().Replace("\\", "/").TrimEnd('/');
        if (!string.Equals(savePath, "Assets", StringComparison.Ordinal) &&
            !savePath.StartsWith("Assets/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Путь сохранения графа должен быть внутри Assets: " + savePath);
        }

        return savePath;
    }

    private static string MakeSafeAssetFileName(string value)
    {
        var invalidCharacters = new HashSet<char>(Path.GetInvalidFileNameChars())
        {
            '/',
            '\\'
        };

        string fileName = new string(value
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray()).Trim();

        return string.IsNullOrEmpty(fileName) ? "StoryGraph" : fileName;
    }

    private static CharacterEmotionType ParseEmotion(string emotion)
    {
        if (string.IsNullOrEmpty(emotion))
            return CharacterEmotionType.Idle;

        return Enum.TryParse(emotion, ignoreCase: true, out CharacterEmotionType result)
            ? result
            : CharacterEmotionType.Idle;
    }

    private static string TruncateName(string value, int maxLength = 20)
    {
        if (string.IsNullOrEmpty(value))
            return "Untitled";

        string normalized = value.Replace(" ", "_").Replace("/", "_");
        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }

    private sealed class BuildContext
    {
        public readonly StoryGraph graph;
        public readonly string assetPath;

        public BuildContext(StoryGraph graph, string assetPath)
        {
            this.graph = graph;
            this.assetPath = assetPath;
        }
    }

    private sealed class ChainResult
    {
        public BaseStoryNode firstNode;
        public BaseStoryNode lastNode;
        public string lastPortName = "exit";
        public float nextX;
    }
}
#endif
