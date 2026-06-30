#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
#if UNITY_2019_1_OR_NEWER && USE_ADVANCED_GENERIC_MENU
using GenericMenu = XNodeEditor.AdvancedGenericMenu;
#endif

[CustomNodeGraphEditor(typeof(StoryGraph), "VN.StoryGraphEditor")]
public class StoryGraphVisualEditor : NodeGraphEditor
{
    const float ToolbarWidth = 860f;
    const float ToolbarHeight = 76f;
    const float NodeGap = 140f;

    readonly List<StoryGraphIssue> _issues = new List<StoryGraphIssue>();
    bool _autoConnect = true;
    bool _showIssues;
    Vector2 _issueScroll;

    StoryGraph Graph => target as StoryGraph;

    [MenuItem("VN/Open Visual Story Graph")]
    public static void OpenSelectedGraph()
    {
        StoryGraph graph = Selection.activeObject as StoryGraph;
        if (graph == null && Selection.activeObject is BaseStoryNode node)
            graph = node.graph as StoryGraph;

        if (graph == null)
        {
            EditorUtility.DisplayDialog(
                "Visual Story Graph",
                "Select a StoryGraph asset or one of its story nodes first.",
                "OK");
            return;
        }

        NodeEditorWindow.Open(graph);
    }

    public override void OnOpen()
    {
        ValidateGraph(false);
    }

    public override void OnGUI()
    {
        if (Graph == null)
            return;

        DrawIssueHighlights();
        DrawToolbar();

        if (_showIssues)
            DrawIssuePanel();
    }

    public override void AddContextMenuItems(GenericMenu menu, Type compatibleType = null, NodePort.IO direction = NodePort.IO.Input)
    {
        Vector2 position = NodeEditorWindow.current != null
            ? NodeEditorWindow.current.WindowToGridPosition(Event.current.mousePosition)
            : Vector2.zero;

        menu.AddItem(new GUIContent("VN/Add Scene"), false, () => CreateQuickNode(typeof(SceneSetupNode), position));
        menu.AddItem(new GUIContent("VN/Add Dialogue"), false, () => CreateQuickNode(typeof(DialogueNode), position));
        menu.AddItem(new GUIContent("VN/Add Choice"), false, () => CreateQuickNode(typeof(ChoiceNode), position));
        menu.AddItem(new GUIContent("VN/Add Phone Dialogue"), false, () => CreateQuickNode(typeof(PhoneDialogueNode), position));
        menu.AddItem(new GUIContent("VN/Add Image"), false, () => CreateQuickNode(typeof(ImageNode), position));
        menu.AddItem(new GUIContent("VN/Add Music Change"), false, () => CreateQuickNode(typeof(SceneSetupNode), position, StoryNodeTemplate.MusicChange));
        menu.AddSeparator("VN/");
        menu.AddItem(new GUIContent("VN/Validate Graph"), false, () => ValidateGraph(true));
        menu.AddItem(new GUIContent("VN/Open Text Workspace"), false, () => StoryTextWorkspaceWindow.Open(Graph));
        menu.AddSeparator("");

        base.AddContextMenuItems(menu, compatibleType, direction);
    }

    public override Gradient GetNoodleGradient(NodePort output, NodePort input)
    {
        if (TryGetFlowColor(output, out Color color))
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.Lerp(color, Color.white, 0.25f), 1f) },
                new[] { new GradientAlphaKey(input == null ? 0.65f : 1f, 0f), new GradientAlphaKey(input == null ? 0.65f : 1f, 1f) });
            return gradient;
        }

        return base.GetNoodleGradient(output, input);
    }

    public override float GetNoodleThickness(NodePort output, NodePort input)
    {
        return TryGetFlowColor(output, out _) ? base.GetNoodleThickness(output, input) + 1.5f : base.GetNoodleThickness(output, input);
    }

    public override Color GetPortColor(NodePort port)
    {
        return TryGetFlowColor(port, out Color color) ? color : base.GetPortColor(port);
    }

    void DrawToolbar()
    {
        Rect rect = new Rect(12f, 8f, Mathf.Min(ToolbarWidth, NodeEditorWindow.current.position.width - 24f), ToolbarHeight);
        GUILayout.BeginArea(rect, EditorStyles.toolbar);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Story Graph", EditorStyles.boldLabel, GUILayout.Width(92f));
        DrawQuickButton("Scene", typeof(SceneSetupNode));
        DrawQuickButton("Dialogue", typeof(DialogueNode));
        DrawQuickButton("Choice", typeof(ChoiceNode));
        DrawQuickButton("Phone", typeof(PhoneDialogueNode));
        DrawQuickButton("Image", typeof(ImageNode));
        DrawQuickButton("Music", typeof(SceneSetupNode), StoryNodeTemplate.MusicChange);
        GUILayout.FlexibleSpace();
        _autoConnect = GUILayout.Toggle(_autoConnect, "Auto connect", EditorStyles.toolbarButton, GUILayout.Width(106f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(92f);
        DrawQuickButton("Condition", typeof(ConditionNode));
        DrawQuickButton("Premium", typeof(PremiumNode));
        DrawQuickButton("Variable", typeof(VariableChangeNode));
        DrawQuickButton("Stat", typeof(StatChangeNode));
        DrawQuickButton("Camera", typeof(CameraNode));
        DrawQuickButton("Effect", typeof(EffectNode));
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(76f)))
            ValidateGraph(true);
        if (GUILayout.Button(IssueLabel(), EditorStyles.toolbarButton, GUILayout.Width(90f)))
            _showIssues = !_showIssues;
        if (GUILayout.Button("Text", EditorStyles.toolbarButton, GUILayout.Width(48f)))
            StoryTextWorkspaceWindow.Open(Graph, GetSelectedStoryNode(Graph));
        if (GUILayout.Button("Frame", EditorStyles.toolbarButton, GUILayout.Width(56f)))
            FrameGraph();

        EditorGUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void DrawQuickButton(string label, Type nodeType, StoryNodeTemplate template = StoryNodeTemplate.Default)
    {
        if (GUILayout.Button(label, EditorStyles.toolbarButton, GUILayout.Width(72f)))
            CreateQuickNode(nodeType, GetNextNodePosition(GetSelectedStoryNode(Graph)), template);
    }

    string IssueLabel()
    {
        if (_issues.Count == 0)
            return "Issues: 0";

        int errors = _issues.Count(issue => issue.Severity == StoryGraphIssueSeverity.Error);
        return errors > 0 ? "Errors: " + errors : "Warnings: " + _issues.Count;
    }

    void DrawIssuePanel()
    {
        Rect rect = new Rect(12f, ToolbarHeight + 18f, 440f, Mathf.Min(380f, NodeEditorWindow.current.position.height - ToolbarHeight - 34f));
        GUILayout.BeginArea(rect, EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Graph Validation", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Repair", EditorStyles.miniButton, GUILayout.Width(58f)))
            RepairGraph();
        if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(48f)))
            EditorGUIUtility.systemCopyBuffer = BuildIssueReport();
        if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f)))
            _showIssues = false;
        EditorGUILayout.EndHorizontal();

        if (_issues.Count == 0)
        {
            EditorGUILayout.HelpBox("No structural issues found.", MessageType.Info);
            GUILayout.EndArea();
            return;
        }

        _issueScroll = EditorGUILayout.BeginScrollView(_issueScroll);
        foreach (var issue in _issues)
            DrawIssue(issue);
        EditorGUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    void DrawIssue(StoryGraphIssue issue)
    {
        Color oldColor = GUI.color;
        GUI.color = issue.Severity == StoryGraphIssueSeverity.Error
            ? new Color(1f, 0.55f, 0.55f)
            : new Color(1f, 0.82f, 0.45f);

        string prefix = issue.Severity == StoryGraphIssueSeverity.Error ? "Error" : "Warning";
        string nodeName = issue.Node != null ? issue.Node.name : "Graph";
        string label = prefix + " | " + nodeName + ": " + issue.Message;

        if (GUILayout.Button(label, EditorStyles.miniButtonLeft))
            SelectNode(issue.Node);

        GUI.color = oldColor;
    }

    void DrawIssueHighlights()
    {
        if (!_showIssues || _issues.Count == 0 || NodeEditorWindow.current == null)
            return;

        var window = NodeEditorWindow.current;
        var nodeGroups = _issues
            .Where(issue => issue.Node != null)
            .GroupBy(issue => issue.Node);

        foreach (var group in nodeGroups)
        {
            BaseStoryNode node = group.Key;
            Vector2 size = window.nodeSizes.TryGetValue(node, out Vector2 cachedSize)
                ? cachedSize
                : new Vector2(320f, 130f);

            Rect rect = window.GridToWindowRect(new Rect(node.position, size));
            rect = new Rect(rect.x - 5f, rect.y - 5f, rect.width + 10f, rect.height + 10f);

            bool hasError = group.Any(issue => issue.Severity == StoryGraphIssueSeverity.Error);
            Color color = hasError ? new Color(1f, 0.18f, 0.12f, 0.95f) : new Color(1f, 0.62f, 0.1f, 0.9f);
            DrawRectOutline(rect, color, 3f);
        }
    }

    void CreateQuickNode(Type nodeType, Vector2 position, StoryNodeTemplate template = StoryNodeTemplate.Default)
    {
        StoryGraph graph = Graph;
        if (graph == null || !typeof(BaseStoryNode).IsAssignableFrom(nodeType))
            return;

        BaseStoryNode selected = GetSelectedStoryNode(graph);
        BaseStoryNode node = CreateNode(nodeType, position) as BaseStoryNode;
        if (node == null)
            return;

        Undo.RecordObject(node, "Configure Story Node");
        node.guid = Guid.NewGuid().ToString();
        node.name = MakeNodeName(graph, nodeType, template);
        ConfigureNewNode(node, template);

        if (_autoConnect && selected != null && selected != node)
            ConnectFromSelected(selected, node);

        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(graph);
        ValidateGraph(_showIssues);
        SelectNode(node);
        NodeEditorWindow.RepaintAll();
    }

    void ConfigureNewNode(BaseStoryNode node, StoryNodeTemplate template)
    {
        switch (node)
        {
            case SceneSetupNode scene:
                scene.sceneLabel = template == StoryNodeTemplate.MusicChange ? "Music change" : "New scene";
                scene.sceneData = CreateSceneDataSubAsset(template == StoryNodeTemplate.MusicChange ? "MusicChange" : "SceneSetup");
                break;
            case DialogueNode dialogue:
                dialogue.nodeTitle = "New dialogue";
                dialogue.lines = new List<DialogueLine> { new DialogueLine { richText = "New line." } };
                break;
            case ChoiceNode choice:
                choice.nodeTitle = "New choice";
                choice.lines = new List<DialogueLine> { new DialogueLine { richText = "What happens next?" } };
                choice.options = new List<ChoiceOption>
                {
                    new ChoiceOption { text = "Continue" },
                    new ChoiceOption { text = "Look around" }
                };
                choice.choices = new List<BaseStoryNode>();
                StoryNodeEditorTools.EnsureChoicePorts(choice);
                break;
            case PhoneDialogueNode phone:
                phone.contactName = "Contact";
                phone.messages = new List<PhoneMessage> { new PhoneMessage { senderName = "Contact", text = "New message." } };
                break;
            case ConditionNode condition:
                condition.variableKey = "variable";
                condition.requiredValue = 1;
                break;
            case PremiumNode premium:
                premium.cost = 1;
                break;
            case VariableChangeNode variable:
                variable.variableKey = "variable";
                variable.deltaValue = 1;
                variable.Add = true;
                break;
            case StatChangeNode stat:
                stat.statId = "stat";
                stat.delta = 1;
                break;
            case EffectNode effect:
                effect.duration = 0.5f;
                effect.intensity = 5f;
                break;
        }
    }

    bool ConnectFromSelected(BaseStoryNode from, BaseStoryNode to)
    {
        NodePort output = PickOutputPort(from);
        NodePort input = to.GetInputPort("enter");
        if (output == null || input == null)
            return false;

        if (output.IsConnected)
        {
            if (!CanInsertIntoChain(output, to, out NodePort oldInput))
                return false;

            NodePort newOutput = to.GetOutputPort("exit");
            if (newOutput == null)
                return false;

            Undo.RecordObject(from, "Insert Story Node");
            Undo.RecordObject(to, "Insert Story Node");
            Undo.RecordObject(oldInput.node, "Insert Story Node");
            output.Disconnect(oldInput);
            output.Connect(input);
            newOutput.Connect(oldInput);
            EditorUtility.SetDirty(oldInput.node);
        }
        else
        {
            output.Connect(input);
        }

        EditorUtility.SetDirty(from);
        EditorUtility.SetDirty(to);
        return true;
    }

    NodePort PickOutputPort(BaseStoryNode node)
    {
        if (node == null)
            return null;

        if (node is ChoiceNode choice)
        {
            StoryNodeEditorTools.EnsureChoicePorts(choice);
            int count = choice.options != null ? choice.options.Count : 0;
            for (int i = 0; i < count; i++)
            {
                NodePort port = choice.GetOutputPort("choices " + i);
                if (port != null && !port.IsConnected)
                    return port;
            }
            return null;
        }

        if (node is ConditionNode)
            return FirstUnconnected(node, "trueExit", "falseExit");

        if (node is PremiumNode)
            return FirstUnconnected(node, "successNode", "failNode");

        NodePort exit = node.GetOutputPort("exit");
        if (exit != null)
            return exit;

        return node.Outputs.FirstOrDefault(port => port != null && !port.IsConnected);
    }

    static NodePort FirstUnconnected(BaseStoryNode node, params string[] names)
    {
        foreach (string name in names)
        {
            NodePort port = node.GetOutputPort(name);
            if (port != null && !port.IsConnected)
                return port;
        }

        return null;
    }

    static bool CanInsertIntoChain(NodePort output, BaseStoryNode newNode, out NodePort oldInput)
    {
        oldInput = null;
        if (output == null || newNode == null || output.fieldName != "exit" || !output.IsConnected)
            return false;

        if (newNode is ChoiceNode || newNode is ConditionNode || newNode is PremiumNode)
            return false;

        oldInput = output.Connection;
        return oldInput != null && oldInput.node != null && newNode.GetOutputPort("exit") != null;
    }

    Vector2 GetNextNodePosition(BaseStoryNode selected)
    {
        if (selected != null)
        {
            Vector2 size = NodeEditorWindow.current != null && NodeEditorWindow.current.nodeSizes.TryGetValue(selected, out Vector2 cachedSize)
                ? cachedSize
                : new Vector2(320f, 120f);
            return selected.position + new Vector2(size.x + NodeGap, 0f);
        }

        if (NodeEditorWindow.current != null)
            return NodeEditorWindow.current.WindowToGridPosition(NodeEditorWindow.current.position.size * 0.5f);

        return Vector2.zero;
    }

    static BaseStoryNode GetSelectedStoryNode(StoryGraph graph)
    {
        if (graph == null)
            return null;

        foreach (UnityEngine.Object selected in Selection.objects)
        {
            if (selected is BaseStoryNode node && node.graph == graph)
                return node;
        }

        return null;
    }

    SceneSetupData CreateSceneDataSubAsset(string prefix)
    {
        var sceneData = ScriptableObject.CreateInstance<SceneSetupData>();
        sceneData.name = prefix + "Data";

        string graphPath = AssetDatabase.GetAssetPath(Graph);
        if (!string.IsNullOrEmpty(graphPath))
            AssetDatabase.AddObjectToAsset(sceneData, graphPath);

        EditorUtility.SetDirty(sceneData);
        return sceneData;
    }

    static string MakeNodeName(StoryGraph graph, Type type, StoryNodeTemplate template)
    {
        string baseName = template == StoryNodeTemplate.MusicChange ? "Music" : type.Name.Replace("Node", "");
        int count = graph.nodes != null ? graph.nodes.Count(node => node != null && node.GetType() == type) : 0;
        return baseName + " - " + count;
    }

    void SelectNode(BaseStoryNode node)
    {
        if (node == null)
            return;

        Selection.activeObject = node;
        if (NodeEditorWindow.current != null)
            NodeEditorWindow.current.SelectNode(node, false);
        EditorGUIUtility.PingObject(node);
    }

    void FrameGraph()
    {
        StoryGraph graph = Graph;
        if (graph == null || graph.nodes == null || graph.nodes.Count == 0 || NodeEditorWindow.current == null)
            return;

        var nodes = graph.nodes.Where(node => node != null).Cast<UnityEngine.Object>().ToArray();
        if (nodes.Length == 0)
            return;

        Selection.objects = nodes;
        NodeEditorWindow.current.Home();
    }

    void ValidateGraph(bool showPanel)
    {
        _issues.Clear();
        _issues.AddRange(BuildIssues(Graph));
        _issues.Sort((a, b) => a.Severity != b.Severity
            ? a.Severity.CompareTo(b.Severity)
            : string.Compare(a.Message, b.Message, StringComparison.Ordinal));

        if (showPanel)
            _showIssues = true;
    }

    List<StoryGraphIssue> BuildIssues(StoryGraph graph)
    {
        var issues = new List<StoryGraphIssue>();
        if (graph == null)
            return issues;

        var nodes = graph.nodes != null
            ? graph.nodes.OfType<BaseStoryNode>().ToList()
            : new List<BaseStoryNode>();

        int startCount = nodes.Count(node => node is StartNode);
        if (startCount == 0)
            issues.Add(StoryGraphIssue.Error(null, "StartNode is missing."));
        else if (startCount > 1)
            issues.Add(StoryGraphIssue.Warning(null, "There are " + startCount + " StartNode entries. The runtime will use the first one it finds."));

        var seenGuids = new HashSet<string>();
        foreach (BaseStoryNode node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.guid))
                issues.Add(StoryGraphIssue.Error(node, "Node GUID is empty."));
            else if (!seenGuids.Add(node.guid))
                issues.Add(StoryGraphIssue.Error(node, "Node GUID is duplicated."));
        }

        if (startCount > 0)
        {
            var reachable = CollectReachableNodes(nodes.OfType<StartNode>());
            foreach (BaseStoryNode node in nodes)
            {
                if (!(node is StartNode) && !reachable.Contains(node))
                    issues.Add(StoryGraphIssue.Warning(node, "Node is not reachable from StartNode."));
            }
        }

        foreach (BaseStoryNode node in nodes)
            ValidateNode(node, issues);

        return issues;
    }

    static HashSet<BaseStoryNode> CollectReachableNodes(IEnumerable<StartNode> starts)
    {
        var reachable = new HashSet<BaseStoryNode>();
        var queue = new Queue<BaseStoryNode>();

        foreach (StartNode start in starts)
        {
            if (start != null && reachable.Add(start))
                queue.Enqueue(start);
        }

        while (queue.Count > 0)
        {
            BaseStoryNode node = queue.Dequeue();
            foreach (NodePort output in node.Outputs)
            {
                if (output == null)
                    continue;

                foreach (NodePort connection in output.GetConnections())
                {
                    if (connection?.node is BaseStoryNode next && reachable.Add(next))
                        queue.Enqueue(next);
                }
            }
        }

        return reachable;
    }

    static void ValidateNode(BaseStoryNode node, List<StoryGraphIssue> issues)
    {
        switch (node)
        {
            case StartNode _:
                RequireOutput(node, "exit", issues, StoryGraphIssueSeverity.Error, "StartNode has no connected exit.");
                break;
            case DialogueNode dialogue:
                ValidateDialogueNode(dialogue, issues);
                WarnTerminal(node, issues);
                break;
            case ChoiceNode choice:
                ValidateChoiceNode(choice, issues);
                break;
            case PhoneDialogueNode phone:
                ValidatePhoneNode(phone, issues);
                WarnTerminal(node, issues);
                break;
            case SceneSetupNode scene:
                if (scene.sceneData == null)
                    issues.Add(StoryGraphIssue.Warning(scene, "Scene data is empty."));
                WarnTerminal(node, issues);
                break;
            case ConditionNode condition:
                if (string.IsNullOrWhiteSpace(condition.variableKey))
                    issues.Add(StoryGraphIssue.Warning(condition, "Condition variable key is empty."));
                RequireOutput(condition, "trueExit", issues, StoryGraphIssueSeverity.Warning, "trueExit is not connected.");
                RequireOutput(condition, "falseExit", issues, StoryGraphIssueSeverity.Warning, "falseExit is not connected.");
                break;
            case PremiumNode premium:
                if (premium.cost <= 0)
                    issues.Add(StoryGraphIssue.Warning(premium, "Premium cost is zero or negative."));
                RequireOutput(premium, "successNode", issues, StoryGraphIssueSeverity.Warning, "successNode is not connected.");
                RequireOutput(premium, "failNode", issues, StoryGraphIssueSeverity.Warning, "failNode is not connected. Runtime will open the shop when payment fails.");
                break;
            case VariableChangeNode variable:
                if (string.IsNullOrWhiteSpace(variable.variableKey))
                    issues.Add(StoryGraphIssue.Warning(variable, "Variable key is empty."));
                WarnTerminal(node, issues);
                break;
            case StatChangeNode stat:
                if (string.IsNullOrWhiteSpace(stat.statId))
                    issues.Add(StoryGraphIssue.Warning(stat, "Stat id is empty."));
                WarnTerminal(node, issues);
                break;
            default:
                WarnTerminal(node, issues);
                break;
        }
    }

    static void ValidateDialogueNode(DialogueNode node, List<StoryGraphIssue> issues)
    {
        if (node.lines == null || node.lines.Count == 0)
        {
            issues.Add(StoryGraphIssue.Warning(node, "Dialogue has no lines."));
            return;
        }

        for (int i = 0; i < node.lines.Count; i++)
        {
            if (node.lines[i] == null || string.IsNullOrWhiteSpace(node.lines[i].richText))
                issues.Add(StoryGraphIssue.Warning(node, "Dialogue line " + (i + 1) + " is empty."));
        }
    }

    static void ValidateChoiceNode(ChoiceNode node, List<StoryGraphIssue> issues)
    {
        int optionCount = node.options != null ? node.options.Count : 0;
        if (optionCount == 0)
        {
            issues.Add(StoryGraphIssue.Error(node, "Choice has no options."));
            return;
        }

        if (node.choices == null || node.choices.Count != optionCount)
            issues.Add(StoryGraphIssue.Warning(node, "Choice port list count does not match options count. Use Repair or open the node once."));

        for (int i = 0; i < optionCount; i++)
        {
            ChoiceOption option = node.options[i];
            if (option == null || string.IsNullOrWhiteSpace(option.text))
                issues.Add(StoryGraphIssue.Warning(node, "Choice option " + (i + 1) + " has empty text."));

            NodePort port = node.GetOutputPort("choices " + i);
            if (port == null)
                issues.Add(StoryGraphIssue.Error(node, "Choice output port choices " + i + " is missing."));
            else if (!port.IsConnected)
                issues.Add(StoryGraphIssue.Warning(node, "Choice output " + (i + 1) + " is not connected."));
        }

        if (node.lines == null || node.lines.Count == 0 || node.lines.All(line => line == null || string.IsNullOrWhiteSpace(line.richText)))
            issues.Add(StoryGraphIssue.Warning(node, "Choice prompt text is empty."));
    }

    static void ValidatePhoneNode(PhoneDialogueNode node, List<StoryGraphIssue> issues)
    {
        if (node.messages == null || node.messages.Count == 0)
        {
            issues.Add(StoryGraphIssue.Warning(node, "Phone dialogue has no messages."));
            return;
        }

        for (int i = 0; i < node.messages.Count; i++)
        {
            if (node.messages[i] == null || string.IsNullOrWhiteSpace(node.messages[i].text))
                issues.Add(StoryGraphIssue.Warning(node, "Phone message " + (i + 1) + " is empty."));
        }
    }

    static void RequireOutput(BaseStoryNode node, string portName, List<StoryGraphIssue> issues, StoryGraphIssueSeverity severity, string message)
    {
        NodePort port = node.GetOutputPort(portName);
        if (port == null || !port.IsConnected)
            issues.Add(new StoryGraphIssue(node, severity, message));
    }

    static void WarnTerminal(BaseStoryNode node, List<StoryGraphIssue> issues)
    {
        if (node == null || node is ChoiceNode || node is ConditionNode || node is PremiumNode)
            return;

        NodePort exit = node.GetOutputPort("exit");
        if (exit != null && !exit.IsConnected)
            issues.Add(StoryGraphIssue.Warning(node, "Exit is not connected. This branch will finish the chapter here."));
    }

    void RepairGraph()
    {
        StoryGraph graph = Graph;
        if (graph == null || graph.nodes == null)
            return;

        Undo.RecordObject(graph, "Repair Story Graph");
        var seen = new HashSet<string>();
        foreach (BaseStoryNode node in graph.nodes.OfType<BaseStoryNode>())
        {
            Undo.RecordObject(node, "Repair Story Graph");
            if (string.IsNullOrWhiteSpace(node.guid) || seen.Contains(node.guid))
            {
                do
                {
                    node.guid = Guid.NewGuid().ToString();
                }
                while (!seen.Add(node.guid));
            }
            else
            {
                seen.Add(node.guid);
            }

            if (node is ChoiceNode choice)
                StoryNodeEditorTools.EnsureChoicePorts(choice);

            EditorUtility.SetDirty(node);
        }

        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        ValidateGraph(true);
    }

    string BuildIssueReport()
    {
        if (_issues.Count == 0)
            return "Story graph validation: no issues.";

        return string.Join(Environment.NewLine, _issues.Select(issue =>
        {
            string severity = issue.Severity == StoryGraphIssueSeverity.Error ? "ERROR" : "WARNING";
            string node = issue.Node != null ? issue.Node.name : "Graph";
            return severity + " | " + node + " | " + issue.Message;
        }));
    }

    static bool TryGetFlowColor(NodePort port, out Color color)
    {
        color = Color.white;
        if (port == null || !port.IsOutput)
            return false;

        string name = port.fieldName;
        if (string.IsNullOrEmpty(name))
            return false;

        if (name.StartsWith("choices ", StringComparison.Ordinal))
        {
            int index = ParseTrailingIndex(name);
            Color[] colors =
            {
                new Color(0.98f, 0.75f, 0.18f),
                new Color(0.2f, 0.72f, 1f),
                new Color(0.78f, 0.48f, 1f),
                new Color(0.35f, 0.9f, 0.55f)
            };
            color = colors[Mathf.Abs(index) % colors.Length];
            return true;
        }

        switch (name)
        {
            case "trueExit":
            case "successNode":
            case "hasItem":
                color = new Color(0.25f, 0.95f, 0.45f);
                return true;
            case "falseExit":
            case "failNode":
            case "noItem":
                color = new Color(1f, 0.32f, 0.25f);
                return true;
            case "exit":
                color = new Color(0.42f, 0.68f, 1f);
                return true;
            default:
                return false;
        }
    }

    static int ParseTrailingIndex(string value)
    {
        int space = value.LastIndexOf(' ');
        if (space >= 0 && int.TryParse(value.Substring(space + 1), out int index))
            return index;
        return 0;
    }

    static void DrawRectOutline(Rect rect, Color color, float width)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
    }

    enum StoryGraphIssueSeverity
    {
        Error = 0,
        Warning = 1
    }

    enum StoryNodeTemplate
    {
        Default,
        MusicChange
    }

    sealed class StoryGraphIssue
    {
        public readonly BaseStoryNode Node;
        public readonly StoryGraphIssueSeverity Severity;
        public readonly string Message;

        public StoryGraphIssue(BaseStoryNode node, StoryGraphIssueSeverity severity, string message)
        {
            Node = node;
            Severity = severity;
            Message = message;
        }

        public static StoryGraphIssue Error(BaseStoryNode node, string message)
        {
            return new StoryGraphIssue(node, StoryGraphIssueSeverity.Error, message);
        }

        public static StoryGraphIssue Warning(BaseStoryNode node, string message)
        {
            return new StoryGraphIssue(node, StoryGraphIssueSeverity.Warning, message);
        }
    }
}
#endif
