#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;

public class StoryTextWorkspaceWindow : EditorWindow
{
    const float SidebarWidth = 330f;
    const int PreviewLimit = 110;

    StoryGraph _graph;
    BaseStoryNode _selectedNode;
    Vector2 _nodeListScroll;
    Vector2 _editorScroll;
    string _search = "";
    bool _showDialogue = true;
    bool _showChoice = true;
    bool _showPhone = true;
    bool _showPhoneScriptImporter = true;
    string _phoneScriptDraft = "\u041C\u044D\u0433: \u0423 \u043C\u0435\u043D\u044F \u0431\u0443\u0434\u0435\u0442 \u043F\u043E\u0434\u043A\u0430\u0441\u0442 \u0441 \u0413\u0430\u0431\u0440\u0438\u044D\u043B\u043E\u043C \u041C\u043E\u0440\u0442\u0435\u043B\u043B\u043E\u043C!!!\n{PlayerName}: \u0421 \u043A\u0435\u043C?\n\u041C\u044D\u0433: \u0421\u0442\u044B\u0434\u043D\u043E \u043D\u0435 \u0437\u043D\u0430\u0442\u044C, \u0441 \u0442\u0432\u043E\u0435\u0439-\u0442\u043E \u043F\u0440\u043E\u0444\u0435\u0441\u0441\u0438\u0435\u0439))\n\u041C\u044D\u0433: \u0424\u043E\u0442\u043E";

    static GUIStyle _wrappedTextArea;
    static GUIStyle _mutedLabel;
    static GUIStyle _nodeButton;

    [MenuItem("VN/Story Text Workspace")]
    public static void OpenMenu()
    {
        var window = GetWindow<StoryTextWorkspaceWindow>("Story Text");
        window.minSize = new Vector2(900f, 600f);
        window.UseCurrentSelection();
    }

    public static void Open(StoryGraph graph, BaseStoryNode node = null)
    {
        var window = GetWindow<StoryTextWorkspaceWindow>("Story Text");
        window.minSize = new Vector2(900f, 600f);
        window._graph = graph;
        window._selectedNode = node;
        window.Show();
        window.Focus();
    }

    void OnSelectionChange()
    {
        if (_graph == null)
            UseCurrentSelection();

        Repaint();
    }

    void OnGUI()
    {
        EnsureStyles();
        DrawToolbar();

        if (_graph == null)
        {
            EditorGUILayout.HelpBox("Select a StoryGraph or a story node to edit chapter text in one place.", MessageType.Info);
            return;
        }

        DrawStats();

        EditorGUILayout.BeginHorizontal();
        DrawNodeList();
        DrawSelectedNodeEditor();
        EditorGUILayout.EndHorizontal();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        EditorGUI.BeginChangeCheck();
        _graph = (StoryGraph)EditorGUILayout.ObjectField(_graph, typeof(StoryGraph), false, GUILayout.Width(260f));
        if (EditorGUI.EndChangeCheck())
            _selectedNode = null;

        if (GUILayout.Button("Use Selection", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            UseCurrentSelection();

        if (_graph != null && GUILayout.Button("Open xNode", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            NodeEditorWindow.Open(_graph);

        GUILayout.Space(8f);
        _search = GUILayout.TextField(_search, GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarTextField, GUILayout.MinWidth(180f));

        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50f)))
            _search = "";

        GUILayout.FlexibleSpace();

        _showDialogue = GUILayout.Toggle(_showDialogue, "Dialogue", EditorStyles.toolbarButton, GUILayout.Width(75f));
        _showChoice = GUILayout.Toggle(_showChoice, "Choice", EditorStyles.toolbarButton, GUILayout.Width(65f));
        _showPhone = GUILayout.Toggle(_showPhone, "Phone", EditorStyles.toolbarButton, GUILayout.Width(60f));

        if (GUILayout.Button("Copy Graph Text", EditorStyles.toolbarButton, GUILayout.Width(115f)))
            CopyGraphText();

        if (GUILayout.Button("Auto Titles", EditorStyles.toolbarButton, GUILayout.Width(85f)))
            AutoTitleAllNodes();

        EditorGUILayout.EndHorizontal();
    }

    void DrawStats()
    {
        var nodes = GetTextNodes().ToList();
        int lineCount = nodes.OfType<DialogueNode>().Sum(n => n.lines != null ? n.lines.Count : 0);
        lineCount += nodes.OfType<ChoiceNode>().Sum(n => n.lines != null ? n.lines.Count : 0);
        int optionCount = nodes.OfType<ChoiceNode>().Sum(n => n.options != null ? n.options.Count : 0);
        int phoneCount = nodes.OfType<PhoneDialogueNode>().Sum(n => n.messages != null ? n.messages.Count : 0);
        int chars = nodes.Sum(n => BuildNodePlainText(n).Length);
        int words = CountWords(nodes.Select(BuildNodePlainText));

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Nodes: " + nodes.Count, GUILayout.Width(90f));
        EditorGUILayout.LabelField("Lines: " + lineCount, GUILayout.Width(90f));
        EditorGUILayout.LabelField("Choices: " + optionCount, GUILayout.Width(100f));
        EditorGUILayout.LabelField("Phone: " + phoneCount, GUILayout.Width(90f));
        EditorGUILayout.LabelField("Words: " + words, GUILayout.Width(100f));
        EditorGUILayout.LabelField("Chars: " + chars, GUILayout.Width(100f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    void DrawNodeList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth));
        EditorGUILayout.LabelField("Text nodes", EditorStyles.boldLabel);

        var nodes = GetFilteredNodes().ToList();
        if (_selectedNode == null && nodes.Count > 0)
            _selectedNode = nodes[0];

        _nodeListScroll = EditorGUILayout.BeginScrollView(_nodeListScroll);
        foreach (var node in nodes)
            DrawNodeRow(node);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    void DrawNodeRow(BaseStoryNode node)
    {
        if (node == null) return;

        bool selected = node == _selectedNode;
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = selected ? new Color(0.55f, 0.72f, 1f) : old;

        string title = GetNodeDisplayName(node);
        string details = GetNodeSummary(node);
        string label = title + "\n" + details;

        if (GUILayout.Button(label, _nodeButton, GUILayout.Height(50f)))
        {
            _selectedNode = node;
            Selection.activeObject = node;
        }

        GUI.backgroundColor = old;
    }

    void DrawSelectedNodeEditor()
    {
        EditorGUILayout.BeginVertical();

        if (_selectedNode == null)
        {
            EditorGUILayout.HelpBox("Select a node in the left list.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField(GetNodeDisplayName(_selectedNode), EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(50f)))
            EditorGUIUtility.PingObject(_selectedNode);

        if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(58f)))
            Selection.activeObject = _selectedNode;

        if (GUILayout.Button("Copy Node Text", EditorStyles.toolbarButton, GUILayout.Width(105f)))
            CopyNodeText(_selectedNode);

        if (GUILayout.Button("Auto Title", EditorStyles.toolbarButton, GUILayout.Width(78f)))
            AutoTitleNode(_selectedNode, true);

        EditorGUILayout.EndHorizontal();

        _editorScroll = EditorGUILayout.BeginScrollView(_editorScroll);

        if (_selectedNode is DialogueNode dialogue)
            DrawDialogueNode(dialogue);
        else if (_selectedNode is ChoiceNode choice)
            DrawChoiceNode(choice);
        else if (_selectedNode is PhoneDialogueNode phone)
            DrawPhoneNode(phone);
        else
            EditorGUILayout.HelpBox("This node has no large text editor.", MessageType.Info);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawDialogueNode(DialogueNode node)
    {
        DrawBaseInfo(node);
        DrawStringField(node, "Node title", node.nodeTitle, value => node.nodeTitle = value);
        DrawSerializedProperty(node, "activeCharacters", true);
        DrawDialogueLines(node, node.lines, "Dialogue lines");
    }

    void DrawChoiceNode(ChoiceNode node)
    {
        EnsureChoicePorts(node);
        DrawBaseInfo(node);
        DrawStringField(node, "Node title", node.nodeTitle, value => node.nodeTitle = value);
        DrawSerializedProperty(node, "activeCharacters", true);
        DrawDialogueLines(node, node.lines, "Choice prompt lines");
        DrawChoiceOptions(node);
    }

    void DrawPhoneNode(PhoneDialogueNode node)
    {
        DrawBaseInfo(node);

        EditorGUI.BeginChangeCheck();
        string contactName = EditorGUILayout.TextField("Contact", node.contactName);
        PhoneHeaderContactMode headerContactMode = (PhoneHeaderContactMode)EditorGUILayout.EnumPopup("Header mode", node.headerContactMode);
        Sprite avatar = (Sprite)EditorGUILayout.ObjectField("Avatar", node.contactAvatar, typeof(Sprite), false);
        float typingDelay = EditorGUILayout.FloatField("Typing delay", node.typingDelay);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(node, "Edit phone node");
            node.contactName = contactName;
            node.headerContactMode = headerContactMode;
            node.contactAvatar = avatar;
            node.typingDelay = Mathf.Max(0f, typingDelay);
            MarkDirty(node);
        }

        if (node.messages == null)
            node.messages = new List<PhoneMessage>();

        DrawPhoneScriptImporter(node);
        DrawPhoneConversationPreview(node);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Messages", EditorStyles.boldLabel);

        for (int i = 0; i < node.messages.Count; i++)
        {
            if (node.messages[i] == null)
                node.messages[i] = new PhoneMessage();

            DrawPhoneMessage(node, i);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add incoming", GUILayout.Height(28f)))
            AddPhoneMessage(node, PhoneMessageSide.Incoming);
        if (GUILayout.Button("Add outgoing", GUILayout.Height(28f)))
            AddPhoneMessage(node, PhoneMessageSide.Outgoing);
        EditorGUILayout.EndHorizontal();
    }

    void DrawPhoneScriptImporter(PhoneDialogueNode node)
    {
        EditorGUILayout.Space(8f);
        _showPhoneScriptImporter = EditorGUILayout.Foldout(_showPhoneScriptImporter, "Phone script paste", true);
        if (!_showPhoneScriptImporter)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.HelpBox(
            "Paste lines like \"Мэг: text\" and \"{PlayerName}: text\". {PlayerName}/NAME/ИМЯ/ГГ/Я become outgoing bubbles; every other speaker becomes incoming. Add [photo=asset name or path] to attach a sprite.",
            MessageType.Info);

        _phoneScriptDraft = EditorGUILayout.TextArea(_phoneScriptDraft ?? "", _wrappedTextArea, GUILayout.MinHeight(92f));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Load current messages", GUILayout.Height(26f)))
            _phoneScriptDraft = BuildPhoneScriptFromNode(node);

        GUI.enabled = !string.IsNullOrWhiteSpace(_phoneScriptDraft);
        if (GUILayout.Button("Replace messages", GUILayout.Height(26f)))
            ApplyPhoneScript(node, append: false);
        if (GUILayout.Button("Append messages", GUILayout.Height(26f)))
            ApplyPhoneScript(node, append: true);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    void DrawPhoneConversationPreview(PhoneDialogueNode node)
    {
        if (node == null || node.messages == null || node.messages.Count == 0)
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Conversation preview", EditorStyles.boldLabel);

        int previewCount = Mathf.Min(8, node.messages.Count);
        for (int i = 0; i < previewCount; i++)
        {
            PhoneMessage message = node.messages[i];
            if (message == null)
                continue;

            bool outgoing = message.side == PhoneMessageSide.Outgoing;
            EditorGUILayout.BeginHorizontal();
            if (outgoing)
                GUILayout.Space(70f);

            string text = string.IsNullOrWhiteSpace(message.text)
                ? (message.attachment != null ? "[attachment]" : "[empty]")
                : DialogueVariableResolver.ResolveText(
                    message.text.Trim(),
                    DialogueVariableContext.PhoneDialogue(nameof(StoryTextWorkspaceWindow)));
            string senderName = !string.IsNullOrWhiteSpace(message.senderName)
                ? DialogueVariableResolver.ResolveText(
                    message.senderName.Trim(),
                    DialogueVariableContext.PhoneDialogue(nameof(StoryTextWorkspaceWindow)))
                : "";
            if (!string.IsNullOrWhiteSpace(senderName))
                text = senderName + ": " + text;
            if (message.attachment != null)
                text += "\n[" + message.attachment.name + "]";

            var bubbleStyle = new GUIStyle(EditorStyles.helpBox)
            {
                wordWrap = true,
                alignment = outgoing ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft
            };
            bubbleStyle.normal.textColor = outgoing ? new Color(0.72f, 0.92f, 1f) : new Color(0.92f, 0.92f, 0.92f);
            EditorGUILayout.LabelField(text, bubbleStyle, GUILayout.MaxWidth(520f));

            if (!outgoing)
                GUILayout.Space(70f);
            EditorGUILayout.EndHorizontal();
        }

        if (node.messages.Count > previewCount)
            EditorGUILayout.LabelField("... " + (node.messages.Count - previewCount) + " more message(s)", _mutedLabel);
    }

    void ApplyPhoneScript(PhoneDialogueNode node, bool append)
    {
        if (node == null)
            return;

        List<PhoneMessage> parsed = ParsePhoneScript(_phoneScriptDraft, node.contactName);
        if (parsed.Count == 0)
            return;

        Undo.RecordObject(node, append ? "Append phone script" : "Replace phone script");
        if (node.messages == null || !append)
            node.messages = new List<PhoneMessage>();

        node.messages.AddRange(parsed);
        MarkDirty(node);
    }

    static List<PhoneMessage> ParsePhoneScript(string script, string contactName)
    {
        var messages = new List<PhoneMessage>();
        if (string.IsNullOrWhiteSpace(script))
            return messages;

        string[] lines = script.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        PhoneMessageSide lastSide = PhoneMessageSide.Incoming;
        string lastSenderName = ResolvePhoneSenderNameForSide(lastSide, contactName);

        foreach (string rawLine in lines)
        {
            string line = (rawLine ?? "").Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            PhoneMessageSide side = lastSide;
            string senderName = lastSenderName;
            string body = line;
            string speaker;
            string text;

            if (TryReadPhoneSpeakerLine(line, out speaker, out text))
            {
                side = IsOutgoingPhoneSpeaker(speaker, contactName) ? PhoneMessageSide.Outgoing : PhoneMessageSide.Incoming;
                senderName = NormalizePhoneSenderName(speaker, side, contactName);
                body = text;
            }
            else if (line.StartsWith(">"))
            {
                side = PhoneMessageSide.Outgoing;
                senderName = "{PlayerName}";
                body = line.Substring(1).Trim();
            }
            else if (line.StartsWith("<"))
            {
                side = PhoneMessageSide.Incoming;
                senderName = ResolvePhoneSenderNameForSide(side, contactName);
                body = line.Substring(1).Trim();
            }
            else if (StartsWithPhoneTag(line, "[out]", out text) ||
                     StartsWithPhoneTag(line, "[outgoing]", out text) ||
                     StartsWithPhoneTag(line, "[me]", out text))
            {
                side = PhoneMessageSide.Outgoing;
                senderName = "{PlayerName}";
                body = text;
            }
            else if (StartsWithPhoneTag(line, "[in]", out text) ||
                     StartsWithPhoneTag(line, "[incoming]", out text))
            {
                side = PhoneMessageSide.Incoming;
                senderName = ResolvePhoneSenderNameForSide(side, contactName);
                body = text;
            }

            Sprite attachment;
            bool usePhotoLayout;
            body = ExtractPhoneAttachment(body, out attachment, out usePhotoLayout);
            messages.Add(new PhoneMessage
            {
                senderName = senderName,
                side = side,
                text = body,
                attachment = attachment,
                usePhotoLayout = usePhotoLayout || attachment != null
            });
            lastSide = side;
            lastSenderName = senderName;
        }

        return messages;
    }

    static bool TryReadPhoneSpeakerLine(string line, out string speaker, out string text)
    {
        speaker = "";
        text = line;

        int colonIndex = line.IndexOf(':');
        if (colonIndex <= 0)
            return false;

        speaker = line.Substring(0, colonIndex).Trim();
        text = line.Substring(colonIndex + 1).Trim();
        return !string.IsNullOrWhiteSpace(speaker);
    }

    static bool StartsWithPhoneTag(string line, string tag, out string text)
    {
        text = "";
        if (line == null || tag == null || !line.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
            return false;

        text = line.Substring(tag.Length).Trim();
        return true;
    }

    static bool IsOutgoingPhoneSpeaker(string speaker, string contactName)
    {
        string normalized = NormalizePhoneSpeaker(speaker);
        if (string.IsNullOrEmpty(normalized))
            return false;

        if (!string.IsNullOrWhiteSpace(contactName) &&
            string.Equals(normalized, NormalizePhoneSpeaker(contactName), StringComparison.OrdinalIgnoreCase))
            return false;

        if (DialogueVariableResolver.IsPlayerSpeakerName(
                speaker,
                DialogueVariableContext.PhoneDialogue(nameof(StoryTextWorkspaceWindow))))
            return true;

        if (normalized == "\u0438\u043C\u044F" ||
            normalized == "\u0433\u0433" ||
            normalized == "\u0433\u0435\u0440\u043E\u0438\u043D\u044F" ||
            normalized == "\u044F" ||
            normalized == "me" ||
            normalized == "hero" ||
            normalized == "player")
            return true;

        if (normalized == "contact" ||
            normalized == "meg" ||
            normalized == "\u043C\u044D\u0433")
            return false;

        return normalized == "out" || normalized == "outgoing";
    }

    static string NormalizePhoneSpeaker(string value)
    {
        return (value ?? "").Trim().Trim('[', ']', '<', '>').ToLowerInvariant();
    }

    static string NormalizePhoneSenderName(string speaker, PhoneMessageSide side, string contactName)
    {
        if (DialogueVariableResolver.IsPlayerNameToken(speaker))
            return "{PlayerName}";

        string value = (speaker ?? "").Trim();
        string normalized = NormalizePhoneSpeaker(value);
        if (normalized == "\u0438\u043C\u044F" ||
            normalized == "\u0433\u0433" ||
            normalized == "\u0433\u0435\u0440\u043E\u0438\u043D\u044F" ||
            normalized == "\u044F" ||
            normalized == "me" ||
            normalized == "hero" ||
            normalized == "player" ||
            normalized == "name")
            return "{PlayerName}";

        if ((normalized == "contact" || normalized == "in" || normalized == "incoming") &&
            !string.IsNullOrWhiteSpace(contactName))
            return contactName.Trim();

        return string.IsNullOrWhiteSpace(value)
            ? ResolvePhoneSenderNameForSide(side, contactName)
            : value;
    }

    static string ResolvePhoneSenderNameForSide(PhoneMessageSide side, string contactName)
    {
        return side == PhoneMessageSide.Outgoing
            ? "{PlayerName}"
            : string.IsNullOrWhiteSpace(contactName) ? "Contact" : contactName.Trim();
    }

    static string ExtractPhoneAttachment(string body, out Sprite attachment, out bool usePhotoLayout)
    {
        attachment = null;
        usePhotoLayout = false;
        if (string.IsNullOrWhiteSpace(body))
            return "";

        string[] markers = { "[attachment=", "[image=", "[photo=", "[фото=" };
        for (int i = 0; i < markers.Length; i++)
        {
            string marker = markers[i];
            int start = body.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                continue;

            int end = body.IndexOf(']', start + marker.Length);
            if (end < 0)
                continue;

            string token = body.Substring(start + marker.Length, end - start - marker.Length).Trim().Trim('"', '\'');
            attachment = ResolvePhoneAttachmentSprite(token);
            usePhotoLayout = marker.IndexOf("photo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             marker.IndexOf("\u0444\u043E\u0442\u043E", StringComparison.OrdinalIgnoreCase) >= 0;
            body = (body.Substring(0, start) + body.Substring(end + 1)).Trim();
            break;
        }

        if (body.IndexOf("[photo]", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            usePhotoLayout = true;
            body = body.Replace("[photo]", "").Trim();
        }

        if (body.IndexOf("[\u0444\u043E\u0442\u043E]", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            usePhotoLayout = true;
            body = body.Replace("[\u0444\u043E\u0442\u043E]", "").Trim();
        }

        return body;
    }

    static Sprite ResolvePhoneAttachmentSprite(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        string normalized = token.Replace("\\", "/").Trim();
        if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(normalized);
            if (sprite != null)
                return sprite;
        }

        string[] guids = AssetDatabase.FindAssets(normalized + " t:Sprite");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    static string BuildPhoneScriptFromNode(PhoneDialogueNode node)
    {
        if (node == null || node.messages == null)
            return "";

        string incomingName = string.IsNullOrWhiteSpace(node.contactName) ? "Contact" : node.contactName.Trim();
        var lines = new List<string>();
        foreach (PhoneMessage message in node.messages)
        {
            if (message == null)
                continue;

            string speaker = !string.IsNullOrWhiteSpace(message.senderName)
                ? message.senderName.Trim()
                : message.side == PhoneMessageSide.Outgoing ? "{PlayerName}" : incomingName;
            string text = message.text ?? "";
            if (message.attachment != null)
                text = (text + " [photo=" + AssetDatabase.GetAssetPath(message.attachment) + "]").Trim();
            else if (message.usePhotoLayout)
                text = (text + " [photo]").Trim();
            lines.Add(speaker + ": " + text);
        }

        return string.Join("\n", lines);
    }

    void DrawBaseInfo(BaseStoryNode node)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Asset name", node.name);
        EditorGUILayout.SelectableLabel("GUID: " + node.guid, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        EditorGUILayout.LabelField(GetNodeSummary(node), _mutedLabel);
        EditorGUILayout.EndVertical();
    }

    void DrawDialogueLines(UnityEngine.Object owner, List<DialogueLine> lines, string label)
    {
        if (lines == null)
        {
            if (owner is DialogueNode dialogue)
                dialogue.lines = new List<DialogueLine>();
            else if (owner is ChoiceNode choice)
                choice.lines = new List<DialogueLine>();
            lines = owner is DialogueNode d ? d.lines : ((ChoiceNode)owner).lines;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label + " (" + lines.Count + ")", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Add line", GUILayout.Width(80f)))
        {
            Undo.RecordObject(owner, "Add dialogue line");
            lines.Add(new DialogueLine());
            MarkDirty(owner);
        }
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] == null)
                lines[i] = new DialogueLine();

            DrawDialogueLine(owner, lines, i);
        }
    }

    void DrawDialogueLine(UnityEngine.Object owner, List<DialogueLine> lines, int index)
    {
        DialogueLine line = lines[index];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Line " + (index + 1), EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        GUI.enabled = index > 0;
        if (GUILayout.Button("Up", GUILayout.Width(42f)))
        {
            Undo.RecordObject(owner, "Move dialogue line");
            Swap(lines, index, index - 1);
            MarkDirty(owner);
        }

        GUI.enabled = index < lines.Count - 1;
        if (GUILayout.Button("Down", GUILayout.Width(52f)))
        {
            Undo.RecordObject(owner, "Move dialogue line");
            Swap(lines, index, index + 1);
            MarkDirty(owner);
        }

        GUI.enabled = true;
        if (GUILayout.Button("Delete", GUILayout.Width(58f)))
        {
            Undo.RecordObject(owner, "Delete dialogue line");
            lines.RemoveAt(index);
            MarkDirty(owner);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        CharacterData speaker = (CharacterData)EditorGUILayout.ObjectField("Speaker", line.speaker, typeof(CharacterData), false);
        CharacterEmotionType emotion = (CharacterEmotionType)EditorGUILayout.EnumPopup("Emotion", line.emotion);
        string text = EditorGUILayout.TextArea(line.richText ?? "", _wrappedTextArea, GUILayout.MinHeight(GetTextAreaHeight(line.richText)));
        DialogueStyle style = (DialogueStyle)EditorGUILayout.ObjectField("Style", line.style, typeof(DialogueStyle), false);
        string comment = EditorGUILayout.TextArea(line.authorComment ?? "", _wrappedTextArea, GUILayout.MinHeight(42f));

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(owner, "Edit dialogue line");
            line.speaker = speaker;
            line.emotion = emotion;
            line.richText = text;
            line.style = style;
            line.authorComment = comment;
            MarkDirty(owner);
        }

        EditorGUILayout.EndVertical();
    }

    void DrawChoiceOptions(ChoiceNode node)
    {
        if (node.options == null)
            node.options = new List<ChoiceOption>();

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Options (" + node.options.Count + ")", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Add option", GUILayout.Width(90f)))
            AddChoiceOption(node);
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < node.options.Count; i++)
        {
            if (node.options[i] == null)
                node.options[i] = new ChoiceOption();

            DrawChoiceOption(node, i);
        }
    }

    void DrawChoiceOption(ChoiceNode node, int index)
    {
        ChoiceOption option = node.options[index];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Option " + (index + 1), EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        var port = node.GetOutputPort("choices " + index);
        string branchState = port != null && port.IsConnected ? "Branch: linked" : "Branch: empty";
        EditorGUILayout.LabelField(branchState, _mutedLabel, GUILayout.Width(100f));

        GUI.enabled = index > 0;
        if (GUILayout.Button("Up", GUILayout.Width(42f)))
            MoveChoiceOption(node, index, index - 1);

        GUI.enabled = index < node.options.Count - 1;
        if (GUILayout.Button("Down", GUILayout.Width(52f)))
            MoveChoiceOption(node, index, index + 1);

        GUI.enabled = index == node.options.Count - 1;
        if (GUILayout.Button("Delete", GUILayout.Width(58f)))
        {
            RemoveLastChoiceOption(node);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        string text = EditorGUILayout.TextArea(option.text ?? "", _wrappedTextArea, GUILayout.MinHeight(GetTextAreaHeight(option.text)));
        bool premium = EditorGUILayout.Toggle("Premium", option.isPremium);
        int cost = EditorGUILayout.IntField("Premium cost", option.premiumCost);
        string requiredVariable = EditorGUILayout.TextField("Required variable", option.requiredVariable);
        int requiredValue = EditorGUILayout.IntField("Required value", option.requiredValue);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(node, "Edit choice option");
            option.text = text;
            option.isPremium = premium;
            option.premiumCost = Mathf.Max(0, cost);
            option.requiredVariable = requiredVariable;
            option.requiredValue = requiredValue;
            MarkDirty(node);
        }

        EditorGUILayout.EndVertical();
    }

    void DrawPhoneMessage(PhoneDialogueNode node, int index)
    {
        PhoneMessage message = node.messages[index];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Message " + (index + 1), EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        GUI.enabled = index > 0;
        if (GUILayout.Button("Up", GUILayout.Width(42f)))
        {
            Undo.RecordObject(node, "Move phone message");
            Swap(node.messages, index, index - 1);
            MarkDirty(node);
        }

        GUI.enabled = index < node.messages.Count - 1;
        if (GUILayout.Button("Down", GUILayout.Width(52f)))
        {
            Undo.RecordObject(node, "Move phone message");
            Swap(node.messages, index, index + 1);
            MarkDirty(node);
        }

        GUI.enabled = true;
        if (GUILayout.Button("Duplicate", GUILayout.Width(72f)))
        {
            Undo.RecordObject(node, "Duplicate phone message");
            node.messages.Insert(index + 1, new PhoneMessage
            {
                senderName = message.senderName,
                side = message.side,
                text = message.text,
                timeText = message.timeText,
                attachment = message.attachment,
                usePhotoLayout = message.usePhotoLayout
            });
            MarkDirty(node);
        }

        if (GUILayout.Button("Delete", GUILayout.Width(58f)))
        {
            Undo.RecordObject(node, "Delete phone message");
            node.messages.RemoveAt(index);
            MarkDirty(node);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        string senderName = EditorGUILayout.TextField("Sender", message.senderName ?? "");
        string timeText = EditorGUILayout.TextField("Time", message.timeText ?? "");
        PhoneMessageSide side = (PhoneMessageSide)EditorGUILayout.EnumPopup("Side", message.side);
        Sprite attachment = (Sprite)EditorGUILayout.ObjectField("Attachment", message.attachment, typeof(Sprite), false);
        bool usePhotoLayout = EditorGUILayout.Toggle("Use Photo Layout", message.usePhotoLayout || attachment != null);
        string text = EditorGUILayout.TextArea(message.text ?? "", _wrappedTextArea, GUILayout.MinHeight(GetTextAreaHeight(message.text)));

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(node, "Edit phone message");
            message.senderName = senderName;
            message.timeText = timeText;
            message.side = side;
            message.attachment = attachment;
            message.usePhotoLayout = usePhotoLayout || attachment != null;
            message.text = text;
            MarkDirty(node);
        }

        EditorGUILayout.EndVertical();
    }

    void AddPhoneMessage(PhoneDialogueNode node, PhoneMessageSide side)
    {
        if (node == null)
            return;

        Undo.RecordObject(node, "Add phone message");
        if (node.messages == null)
            node.messages = new List<PhoneMessage>();
        node.messages.Add(new PhoneMessage
        {
            senderName = ResolvePhoneSenderNameForSide(side, node.contactName),
            side = side
        });
        MarkDirty(node);
    }

    void DrawStringField(UnityEngine.Object owner, string label, string current, Action<string> setter)
    {
        EditorGUI.BeginChangeCheck();
        string value = EditorGUILayout.TextField(label, current ?? "");
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(owner, "Edit " + label);
            setter(value);
            MarkDirty(owner);
        }
    }

    static void DrawSerializedProperty(UnityEngine.Object owner, string propertyName, bool includeChildren)
    {
        var serialized = new SerializedObject(owner);
        serialized.Update();
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, includeChildren);
        serialized.ApplyModifiedProperties();
    }

    void UseCurrentSelection()
    {
        if (Selection.activeObject is StoryGraph selectedGraph)
        {
            _graph = selectedGraph;
            _selectedNode = null;
            return;
        }

        if (Selection.activeObject is BaseStoryNode selectedNode)
        {
            _selectedNode = selectedNode;
            _graph = selectedNode.graph as StoryGraph;
        }
    }

    IEnumerable<BaseStoryNode> GetTextNodes()
    {
        if (_graph == null || _graph.nodes == null)
            yield break;

        foreach (var raw in _graph.nodes)
        {
            if (raw is DialogueNode || raw is ChoiceNode || raw is PhoneDialogueNode)
                yield return raw as BaseStoryNode;
        }
    }

    IEnumerable<BaseStoryNode> GetFilteredNodes()
    {
        return GetTextNodes()
            .Where(IsTypeVisible)
            .Where(MatchesSearch)
            .OrderBy(n => n.position.x)
            .ThenBy(n => n.position.y);
    }

    bool IsTypeVisible(BaseStoryNode node)
    {
        if (node is DialogueNode) return _showDialogue;
        if (node is ChoiceNode) return _showChoice;
        if (node is PhoneDialogueNode) return _showPhone;
        return false;
    }

    bool MatchesSearch(BaseStoryNode node)
    {
        if (string.IsNullOrWhiteSpace(_search))
            return true;

        string needle = _search.Trim();
        string haystack = GetNodeDisplayName(node) + "\n" + node.guid + "\n" + BuildNodePlainText(node);
        return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string GetNodeDisplayName(BaseStoryNode node)
    {
        if (node == null) return "<missing>";

        string type = node.GetType().Name;
        string title = "";

        if (node is DialogueNode dialogue)
            title = dialogue.nodeTitle;
        else if (node is ChoiceNode choice)
            title = choice.nodeTitle;
        else if (node is PhoneDialogueNode phone)
            title = phone.contactName;

        if (string.IsNullOrWhiteSpace(title))
            title = node.name;

        return type + ": " + Truncate(title, 70);
    }

    static string GetNodeSummary(BaseStoryNode node)
    {
        if (node is DialogueNode dialogue)
        {
            int lines = dialogue.lines != null ? dialogue.lines.Count : 0;
            return lines + " lines, " + CountWords(BuildNodePlainText(node)) + " words";
        }

        if (node is ChoiceNode choice)
        {
            int prompts = choice.lines != null ? choice.lines.Count : 0;
            int options = choice.options != null ? choice.options.Count : 0;
            return prompts + " prompt lines, " + options + " options";
        }

        if (node is PhoneDialogueNode phone)
        {
            int messages = phone.messages != null ? phone.messages.Count : 0;
            return messages + " messages, " + CountWords(BuildNodePlainText(node)) + " words";
        }

        return "";
    }

    static string BuildNodePlainText(BaseStoryNode node)
    {
        if (node == null) return "";

        if (node is DialogueNode dialogue)
            return BuildDialogueLinesText(dialogue.lines);

        if (node is ChoiceNode choice)
        {
            var parts = new List<string>();
            parts.Add(BuildDialogueLinesText(choice.lines));
            if (choice.options != null)
                parts.AddRange(choice.options.Where(o => o != null).Select(o => "-> " + (o.text ?? "")));
            return string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        if (node is PhoneDialogueNode phone)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(phone.contactName))
                parts.Add("[Phone: " + phone.contactName + "]");
            if (phone.messages != null)
            {
                foreach (var message in phone.messages)
                {
                    if (message == null) continue;
                    string prefix = !string.IsNullOrWhiteSpace(message.senderName)
                        ? message.senderName.Trim() + ": "
                        : message.side == PhoneMessageSide.Incoming ? "< " : "> ";
                    parts.Add(prefix + (message.text ?? ""));
                }
            }
            return string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        return "";
    }

    static string BuildDialogueLinesText(IEnumerable<DialogueLine> lines)
    {
        if (lines == null) return "";

        var parts = new List<string>();
        foreach (var line in lines)
        {
            if (line == null) continue;
            string speaker = line.speaker != null ? line.speaker.characterName : "";
            string text = line.richText ?? "";
            if (string.IsNullOrWhiteSpace(speaker))
                parts.Add(text);
            else
                parts.Add(speaker + ": " + text);
        }
        return string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    void CopyGraphText()
    {
        if (_graph == null) return;

        var parts = new List<string> { "# " + _graph.name };
        foreach (var node in GetTextNodes().OrderBy(n => n.position.x).ThenBy(n => n.position.y))
        {
            parts.Add("");
            parts.Add("## " + GetNodeDisplayName(node));
            parts.Add(BuildNodePlainText(node));
        }

        EditorGUIUtility.systemCopyBuffer = string.Join("\n", parts);
        ShowNotification(new GUIContent("Graph text copied"));
    }

    void CopyNodeText(BaseStoryNode node)
    {
        EditorGUIUtility.systemCopyBuffer = BuildNodePlainText(node);
        ShowNotification(new GUIContent("Node text copied"));
    }

    void AutoTitleAllNodes()
    {
        if (_graph == null) return;

        Undo.RecordObjects(GetTextNodes().Cast<UnityEngine.Object>().ToArray(), "Auto title story nodes");
        foreach (var node in GetTextNodes())
            AutoTitleNode(node, false);

        EditorUtility.SetDirty(_graph);
        NodeEditorWindow.RepaintAll();
    }

    void AutoTitleNode(BaseStoryNode node, bool recordUndo)
    {
        if (node == null) return;
        if (recordUndo)
            Undo.RecordObject(node, "Auto title story node");

        string title = "";

        if (node is DialogueNode dialogue)
        {
            title = FirstNonEmptyLine(dialogue.lines);
            dialogue.nodeTitle = title;
            node.name = "Dialogue - " + TruncateForName(title);
        }
        else if (node is ChoiceNode choice)
        {
            title = FirstNonEmptyLine(choice.lines);
            if (string.IsNullOrWhiteSpace(title) && choice.options != null && choice.options.Count > 0 && choice.options[0] != null)
                title = choice.options[0].text;
            choice.nodeTitle = title;
            node.name = "Choice - " + TruncateForName(title);
        }
        else if (node is PhoneDialogueNode phone)
        {
            title = string.IsNullOrWhiteSpace(phone.contactName) ? FirstPhoneMessage(phone) : phone.contactName;
            node.name = "Phone - " + TruncateForName(title);
        }

        MarkDirty(node);
    }

    static string FirstNonEmptyLine(IEnumerable<DialogueLine> lines)
    {
        if (lines == null) return "";

        foreach (var line in lines)
        {
            if (line != null && !string.IsNullOrWhiteSpace(line.richText))
                return line.richText.Trim();
        }

        return "";
    }

    static string FirstPhoneMessage(PhoneDialogueNode node)
    {
        if (node == null || node.messages == null) return "";

        foreach (var message in node.messages)
        {
            if (message != null && !string.IsNullOrWhiteSpace(message.text))
                return message.text.Trim();
        }

        return "";
    }

    void AddChoiceOption(ChoiceNode node)
    {
        Undo.RecordObject(node, "Add choice option");
        if (node.options == null)
            node.options = new List<ChoiceOption>();
        node.options.Add(new ChoiceOption());
        EnsureChoicePorts(node);
        MarkDirty(node);
    }

    void RemoveLastChoiceOption(ChoiceNode node)
    {
        if (node.options == null || node.options.Count == 0)
            return;

        Undo.RecordObject(node, "Remove choice option");
        int last = node.options.Count - 1;
        var port = node.GetOutputPort("choices " + last);
        if (port != null)
        {
            port.ClearConnections();
            node.RemoveDynamicPort(port);
        }

        node.options.RemoveAt(last);
        if (node.choices != null && node.choices.Count > last)
            node.choices.RemoveAt(last);

        MarkDirty(node);
    }

    void MoveChoiceOption(ChoiceNode node, int from, int to)
    {
        if (node.options == null || from < 0 || to < 0 || from >= node.options.Count || to >= node.options.Count)
            return;

        Undo.RecordObject(node, "Move choice option");

        var fromPort = node.GetOutputPort("choices " + from);
        var toPort = node.GetOutputPort("choices " + to);
        if (fromPort != null && toPort != null)
            fromPort.SwapConnections(toPort);

        Swap(node.options, from, to);
        if (node.choices != null && from < node.choices.Count && to < node.choices.Count)
            Swap(node.choices, from, to);

        MarkDirty(node);
    }

    static void EnsureChoicePorts(ChoiceNode node)
    {
        if (node == null) return;

        if (node.options == null)
            node.options = new List<ChoiceOption>();
        if (node.choices == null)
            node.choices = new List<BaseStoryNode>();

        while (node.choices.Count < node.options.Count)
            node.choices.Add(null);
        while (node.choices.Count > node.options.Count)
            node.choices.RemoveAt(node.choices.Count - 1);

        for (int i = 0; i < node.options.Count; i++)
        {
            string portName = "choices " + i;
            if (!node.HasPort(portName))
                node.AddDynamicOutput(typeof(BaseStoryNode), Node.ConnectionType.Multiple, Node.TypeConstraint.None, portName);
        }
    }

    static void MarkDirty(UnityEngine.Object owner)
    {
        if (owner == null) return;

        EditorUtility.SetDirty(owner);
        if (owner is BaseStoryNode node && node.graph != null)
            EditorUtility.SetDirty(node.graph);
        NodeEditorWindow.RepaintAll();
    }

    static void Swap<T>(IList<T> list, int a, int b)
    {
        T temp = list[a];
        list[a] = list[b];
        list[b] = temp;
    }

    static float GetTextAreaHeight(string value)
    {
        int length = string.IsNullOrEmpty(value) ? 0 : value.Length;
        return Mathf.Clamp(62f + (length / 90f) * 18f, 82f, 260f);
    }

    static string TruncateForName(string value)
    {
        value = Truncate(value, 38);
        return string.IsNullOrWhiteSpace(value) ? "Untitled" : value.Replace("\r", " ").Replace("\n", " ");
    }

    static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        value = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length <= max ? value : value.Substring(0, max) + "...";
    }

    static int CountWords(IEnumerable<string> values)
    {
        int total = 0;
        foreach (string value in values)
            total += CountWords(value);
        return total;
    }

    static int CountWords(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        int count = 0;
        bool inWord = false;

        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
                continue;
            }

            if (!inWord)
            {
                count++;
                inWord = true;
            }
        }

        return count;
    }

    static void EnsureStyles()
    {
        if (_wrappedTextArea == null)
        {
            _wrappedTextArea = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                fontSize = 13,
                richText = false
            };
        }

        if (_mutedLabel == null)
        {
            _mutedLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };
            _mutedLabel.normal.textColor = new Color(0.62f, 0.66f, 0.72f);
        }

        if (_nodeButton == null)
        {
            _nodeButton = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                fontSize = 11,
                padding = new RectOffset(8, 8, 4, 4)
            };
        }
    }
}
#endif
