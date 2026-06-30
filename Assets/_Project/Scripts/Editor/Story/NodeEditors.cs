#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;

[CustomNodeEditor(typeof(StartNode))]
public class StartNodeEditor : NodeEditor
{
    public override int GetWidth() => 200;
    public override Color GetTint() => new Color(0.2f, 0.8f, 0.3f);
}

[CustomNodeEditor(typeof(DialogueNode))]
public class DialogueNodeEditor : NodeEditor
{
    public override int GetWidth() => 520;
    public override Color GetTint() => new Color(0.25f, 0.5f, 0.9f);

    public override void OnBodyGUI()
    {
        serializedObject.Update();

        var node = target as DialogueNode;
        StoryNodeEditorTools.DrawPorts(node);
        StoryNodeEditorTools.DrawProperty(serializedObject, "nodeTitle", true);
        StoryNodeEditorTools.DrawTextStats(node.lines);
        StoryNodeEditorTools.DrawCharactersPreview(node.activeCharacters, GetWidth() - 24);

        if (StoryNodeEditorTools.DrawExpandedToggle(node))
        {
            StoryNodeEditorTools.DrawProperty(serializedObject, "activeCharacters", true);
            StoryNodeEditorTools.DrawProperty(serializedObject, "lines", true);
        }
        else
        {
            StoryNodeEditorTools.DrawDialoguePreview(node.lines, GetWidth() - 24);
        }

        StoryNodeEditorTools.DrawTextWorkspaceButton(node);

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomNodeEditor(typeof(ChoiceNode))]
public class ChoiceNodeEditor : NodeEditor
{
    public override int GetWidth() => 500;
    public override Color GetTint() => new Color(0.9f, 0.8f, 0.1f);

    public override void OnBodyGUI()
    {
        serializedObject.Update();

        var node = target as ChoiceNode;
        StoryNodeEditorTools.EnsureChoicePorts(node);
        serializedObject.Update();
        StoryNodeEditorTools.DrawPorts(node);
        StoryNodeEditorTools.DrawProperty(serializedObject, "nodeTitle", true);

        int promptLines = node.lines != null ? node.lines.Count : 0;
        int options = node.options != null ? node.options.Count : 0;
        EditorGUILayout.LabelField(promptLines + " prompt line(s), " + options + " option(s)", EditorStyles.miniLabel);

        if (StoryNodeEditorTools.DrawExpandedToggle(node))
        {
            StoryNodeEditorTools.DrawProperty(serializedObject, "activeCharacters", true);
            StoryNodeEditorTools.DrawProperty(serializedObject, "lines", true);
            StoryNodeEditorTools.DrawProperty(serializedObject, "options", true);
            StoryNodeEditorTools.DrawProperty(serializedObject, "choices", true);
        }
        else
        {
            StoryNodeEditorTools.DrawDialoguePreview(node.lines, GetWidth() - 24);
            StoryNodeEditorTools.DrawChoicePortsAndPreview(node, GetWidth() - 24);
        }

        StoryNodeEditorTools.DrawTextWorkspaceButton(node);

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomNodeEditor(typeof(PhoneDialogueNode))]
public class PhoneDialogueNodeEditor : NodeEditor
{
    public override int GetWidth() => 460;
    public override Color GetTint() => new Color(0.2f, 0.6f, 0.35f);

    public override void OnBodyGUI()
    {
        serializedObject.Update();

        var node = target as PhoneDialogueNode;
        StoryNodeEditorTools.DrawPorts(node);
        StoryNodeEditorTools.DrawProperty(serializedObject, "contactName", true);
        StoryNodeEditorTools.DrawProperty(serializedObject, "headerContactMode", true);
        StoryNodeEditorTools.DrawProperty(serializedObject, "contactAvatar", true);
        StoryNodeEditorTools.DrawProperty(serializedObject, "typingDelay", true);

        int messages = node.messages != null ? node.messages.Count : 0;
        EditorGUILayout.LabelField(messages + " message(s), " + StoryNodeEditorTools.CountPhoneWords(node) + " word(s)", EditorStyles.miniLabel);

        if (StoryNodeEditorTools.DrawExpandedToggle(node))
            StoryNodeEditorTools.DrawProperty(serializedObject, "messages", true);
        else
            StoryNodeEditorTools.DrawPhonePreview(node, GetWidth() - 24);

        if (GUILayout.Button("Open phone script editor", GUILayout.Height(24f)))
            StoryTextWorkspaceWindow.Open(node.graph as StoryGraph, node);

        StoryNodeEditorTools.DrawTextWorkspaceButton(node);

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomNodeEditor(typeof(ConditionNode))]
public class ConditionNodeEditor : NodeEditor
{
    public override int GetWidth() => 320;
    public override Color GetTint() => new Color(0.9f, 0.55f, 0.1f);

    public override void OnBodyGUI()
    {
        base.OnBodyGUI();

        var node = target as ConditionNode;
        if (node == null) return;

        EditorGUILayout.Space(4);
        var style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = new Color(1f, 0.9f, 0.7f);
        string right = string.IsNullOrWhiteSpace(node.compareVariableKey)
            ? node.requiredValue.ToString()
            : node.compareVariableKey;
        EditorGUILayout.LabelField("Condition: " + node.variableKey + " " + GetComparisonLabel(node.comparison) + " " + right, style);
    }

    static string GetComparisonLabel(ConditionComparison comparison)
    {
        switch (comparison)
        {
            case ConditionComparison.NotEquals: return "!=";
            case ConditionComparison.GreaterThan: return ">";
            case ConditionComparison.GreaterOrEqual: return ">=";
            case ConditionComparison.LessThan: return "<";
            case ConditionComparison.LessOrEqual: return "<=";
            case ConditionComparison.Equals:
            default:
                return "==";
        }
    }
}

[CustomNodeEditor(typeof(StatChangeNode))]
public class StatChangeNodeEditor : NodeEditor
{
    public override int GetWidth() => 320;
    public override Color GetTint() => new Color(0.6f, 0.2f, 0.8f);

    public override void OnBodyGUI()
    {
        base.OnBodyGUI();

        var node = target as StatChangeNode;
        if (node == null) return;

        EditorGUILayout.Space(4);
        string sign = node.delta >= 0 ? "+" : "";
        var style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = node.delta >= 0 ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f);
        EditorGUILayout.LabelField(node.statId + " " + sign + node.delta, style);
    }
}

[CustomNodeEditor(typeof(PremiumNode))]
public class PremiumNodeEditor : NodeEditor
{
    public override int GetWidth() => 280;
    public override Color GetTint() => new Color(0.9f, 0.75f, 0.1f);

    public override void OnBodyGUI()
    {
        base.OnBodyGUI();

        var node = target as PremiumNode;
        if (node == null) return;

        EditorGUILayout.Space(4);
        var style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = new Color(1f, 0.95f, 0.6f);
        EditorGUILayout.LabelField("Cost: " + node.cost + " hearts", style);
    }
}

[CustomNodeEditor(typeof(SceneSetupNode))]
public class SceneSetupNodeEditor : NodeEditor
{
    public override int GetWidth() => 380;
    public override Color GetTint() => new Color(0.3f, 0.3f, 0.35f);

    public override void OnBodyGUI()
    {
        serializedObject.Update();

        var node = target as SceneSetupNode;
        StoryNodeEditorTools.DrawPorts(node);
        StoryNodeEditorTools.DrawProperty(serializedObject, "sceneLabel", true);
        StoryNodeEditorTools.DrawProperty(serializedObject, "sceneData", true);
        serializedObject.ApplyModifiedProperties();

        if (node == null) return;

        if (node.sceneData == null)
        {
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Create SceneSetupData", GUILayout.Height(24f)))
                StoryNodeEditorTools.CreateSceneDataSubAsset(node);
            return;
        }

        DrawSceneDataFields(node.sceneData);

        EditorGUILayout.Space(4);
        var labelStyle = new GUIStyle(EditorStyles.miniLabel);
        labelStyle.normal.textColor = new Color(0.8f, 0.85f, 0.9f);

        if (node.sceneData.background != null)
        {
            StoryNodeEditorTools.DrawSpritePreview(node.sceneData.background, GetWidth() - 24, 9f / 16f);
            EditorGUILayout.LabelField("Background: " + node.sceneData.background.name, labelStyle);
        }
        else if (node.sceneData.backgroundVideo != null)
        {
            EditorGUILayout.LabelField("Video: " + node.sceneData.backgroundVideo.name, labelStyle);
        }
        else if (node.sceneData.backgroundGif != null)
        {
            EditorGUILayout.LabelField("GIF: " + node.sceneData.backgroundGif.name, labelStyle);
        }
        else
        {
            EditorGUILayout.LabelField("No background", labelStyle);
        }

        if (node.sceneData.music != null)
            EditorGUILayout.LabelField("Music: " + node.sceneData.music.name, labelStyle);
    }

    static void DrawSceneDataFields(SceneSetupData data)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Scene media", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        Undo.RecordObject(data, "Edit Scene Setup Data");

        data.background = (Sprite)EditorGUILayout.ObjectField("Background", data.background, typeof(Sprite), false);
        data.backgroundVideo = (UnityEngine.Video.VideoClip)EditorGUILayout.ObjectField("Background video", data.backgroundVideo, typeof(UnityEngine.Video.VideoClip), false);
        data.backgroundGif = (TextAsset)EditorGUILayout.ObjectField("Background GIF", data.backgroundGif, typeof(TextAsset), false);
        data.backgroundOverlay = (Sprite)EditorGUILayout.ObjectField("Overlay", data.backgroundOverlay, typeof(Sprite), false);

        EditorGUILayout.Space(3);
        data.music = (AudioClip)EditorGUILayout.ObjectField("Music", data.music, typeof(AudioClip), false);
        data.stopMusic = EditorGUILayout.Toggle("Stop current Music", data.stopMusic);
        data.stopSfx = EditorGUILayout.Toggle("Stop current SFX", data.stopSfx);
        data.startSfx = (AudioClip)EditorGUILayout.ObjectField("Start SFX", data.startSfx, typeof(AudioClip), false);

        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(data);
    }
}

[CustomNodeEditor(typeof(AppearanceChoiceNode))]
public class AppearanceChoiceNodeEditor : NodeEditor
{
    public override int GetWidth() => 380;
    public override Color GetTint() => new Color(0.9f, 0.4f, 0.7f);

    public override void OnBodyGUI()
    {
        base.OnBodyGUI();

        var node = target as AppearanceChoiceNode;
        if (node == null || node.options == null) return;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Appearance options:", EditorStyles.miniLabel);
        foreach (var option in node.options)
        {
            if (option == null) continue;
            EditorGUILayout.LabelField("  -> " + option.label + " (" + option.type + ")", EditorStyles.miniLabel);
        }
    }
}

[CustomNodeEditor(typeof(WardrobeChoiceNode))]
public class WardrobeChoiceNodeEditor : NodeEditor
{
    public override int GetWidth() => 350;
    public override Color GetTint() => new Color(0.1f, 0.75f, 0.75f);
}

[CustomNodeEditor(typeof(AddClothingNode))]
public class AddClothingNodeEditor : NodeEditor
{
    public override int GetWidth() => 300;
    public override Color GetTint() => new Color(0.1f, 0.65f, 0.65f);
}

[CustomNodeEditor(typeof(EffectNode))]
public class EffectNodeEditor : NodeEditor
{
    public override int GetWidth() => 280;
    public override Color GetTint() => new Color(0.85f, 0.2f, 0.2f);

    public override void OnBodyGUI()
    {
        base.OnBodyGUI();

        var node = target as EffectNode;
        if (node == null) return;

        EditorGUILayout.Space(4);
        var style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = new Color(1f, 0.7f, 0.7f);
        EditorGUILayout.LabelField(node.effect + " (" + node.duration + "s, x" + node.intensity + ")", style);
    }
}

[CustomNodeEditor(typeof(VariableChangeNode))]
public class VariableChangeNodeEditor : NodeEditor
{
    public override int GetWidth() => 300;
    public override Color GetTint() => new Color(0.4f, 0.6f, 0.4f);
}

[CustomNodeEditor(typeof(CameraNode))]
public class CameraNodeEditor : NodeEditor
{
    public override int GetWidth() => 320;
    public override Color GetTint() => new Color(0.3f, 0.7f, 0.9f);

    public override void OnBodyGUI()
    {
        base.OnBodyGUI();

        var node = target as CameraNode;
        if (node == null) return;

        EditorGUILayout.Space(4);
        var style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = new Color(0.7f, 0.92f, 1f);

        string desc;
        switch (node.mode)
        {
            case CameraNode.CameraMode.Position:
                desc = "Camera -> " + node.targetPosition;
                break;
            case CameraNode.CameraMode.Offset:
                desc = "Camera X " + (node.xOffset >= 0 ? "+" : "") + node.xOffset + "px";
                break;
            case CameraNode.CameraMode.Reset:
                desc = "Camera reset";
                break;
            default:
                desc = "Camera";
                break;
        }

        if (node.duration > 0)
            desc += " | " + node.duration + "s";

        EditorGUILayout.LabelField(desc, style);
    }
}

[CustomNodeEditor(typeof(ImageNode))]
public class ImageNodeEditor : NodeEditor
{
    public override int GetWidth() => 340;
    public override Color GetTint() => new Color(0.3f, 0.7f, 0.55f);

    public override void OnBodyGUI()
    {
        base.OnBodyGUI();

        var node = target as ImageNode;
        if (node == null) return;

        if (node.image != null)
            StoryNodeEditorTools.DrawSpritePreview(node.image, GetWidth() - 24, -1f, 150f);

        EditorGUILayout.Space(4);
        var style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = new Color(0.7f, 1f, 0.85f);

        string caption = string.IsNullOrEmpty(node.caption) ? "Close" : node.caption;
        EditorGUILayout.LabelField("Caption: " + caption, style);

        if (!string.IsNullOrEmpty(node.description))
            EditorGUILayout.LabelField(StoryNodeEditorTools.Truncate(node.description, 70), EditorStyles.wordWrappedMiniLabel);

        if (node.video != null)
            EditorGUILayout.LabelField("Video: " + node.video.name, style);
        else if (node.gif != null)
            EditorGUILayout.LabelField("GIF: " + node.gif.name, style);

        if (node.zoomable)
            EditorGUILayout.LabelField("Zoom enabled", style);
    }
}

static class StoryNodeEditorTools
{
    static readonly HashSet<int> ExpandedNodes = new HashSet<int>();
    static GUIStyle _previewBox;
    static GUIStyle _mutedLabel;

    public static void DrawPorts(BaseStoryNode node)
    {
        if (node == null) return;
        NodeEditorGUILayout.PortPair(node.GetInputPort("enter"), node.GetOutputPort("exit"));
    }

    public static void DrawProperty(SerializedObject serializedObject, string propertyName, bool includeChildren)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            NodeEditorGUILayout.PropertyField(property, includeChildren);
    }

    public static bool DrawExpandedToggle(BaseStoryNode node)
    {
        bool expanded = ExpandedNodes.Contains(node.GetInstanceID());
        string label = expanded ? "Hide full inspector fields" : "Show full inspector fields";

        if (GUILayout.Button(label, EditorStyles.miniButton))
        {
            if (expanded)
                ExpandedNodes.Remove(node.GetInstanceID());
            else
                ExpandedNodes.Add(node.GetInstanceID());
        }

        return ExpandedNodes.Contains(node.GetInstanceID());
    }

    public static void DrawTextWorkspaceButton(BaseStoryNode node)
    {
        EditorGUILayout.Space(4);
        if (GUILayout.Button("Open in Story Text Workspace", GUILayout.Height(24f)))
            StoryTextWorkspaceWindow.Open(node.graph as StoryGraph, node);
    }

    public static void DrawTextStats(IList<DialogueLine> lines)
    {
        int lineCount = lines != null ? lines.Count : 0;
        int words = CountDialogueWords(lines);
        int chars = CountDialogueChars(lines);
        EditorGUILayout.LabelField(lineCount + " line(s), " + words + " word(s), " + chars + " char(s)", EditorStyles.miniLabel);
    }

    public static void DrawCharactersPreview(IList<DialogueCharacterEntry> entries, float width)
    {
        if (entries == null || entries.Count == 0)
            return;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Active characters", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        int shown = 0;
        foreach (var entry in entries)
        {
            if (entry == null || entry.character == null)
                continue;

            shown++;
            if (shown > 4)
                break;

            EditorGUILayout.BeginVertical(GUILayout.Width(100f));

            Sprite sprite = entry.character.useLayeredEmotions
                ? entry.character.GetBodySprite()
                : entry.character.GetEmotion(entry.emotion);

            if (sprite != null)
                DrawSpritePreview(sprite, 96f, -1f, 115f);
            else
                GUILayout.Box("No sprite", PreviewBox, GUILayout.Width(96f), GUILayout.Height(70f));

            var nameStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            EditorGUILayout.LabelField(entry.character.characterName, nameStyle);
            EditorGUILayout.LabelField(entry.emotion + " | " + entry.position, nameStyle);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndHorizontal();
    }

    public static void DrawDialoguePreview(IList<DialogueLine> lines, float width)
    {
        if (lines == null || lines.Count == 0)
        {
            EditorGUILayout.LabelField("No text lines", MutedLabel);
            return;
        }

        EditorGUILayout.Space(4);
        int previewCount = Mathf.Min(4, lines.Count);

        for (int i = 0; i < previewCount; i++)
        {
            var line = lines[i];
            if (line == null) continue;

            string speaker = line.speaker != null ? "[" + line.speaker.characterName + "] " : "";
            string text = speaker + (line.richText ?? "");
            EditorGUILayout.LabelField(Truncate(text, 150), PreviewBox, GUILayout.Width(width));
        }

        if (lines.Count > previewCount)
            EditorGUILayout.LabelField("... " + (lines.Count - previewCount) + " more line(s)", MutedLabel);
    }

    public static void DrawChoicePortsAndPreview(ChoiceNode node, float width)
    {
        if (node == null || node.options == null || node.options.Count == 0)
        {
            EditorGUILayout.LabelField("No options", MutedLabel);
            return;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);

        for (int i = 0; i < node.options.Count; i++)
        {
            var option = node.options[i];
            string text = option != null ? option.text : "";
            string prefix = option != null && option.isPremium ? "[" + option.premiumCost + "] " : "";
            string label = (i + 1) + ". " + prefix + Truncate(text, 90);
            var port = node.GetOutputPort("choices " + i);

            if (port != null)
                NodeEditorGUILayout.PortField(new GUIContent(label), port, GUILayout.Width(width));
            else
                EditorGUILayout.LabelField(label + " (missing port)", MutedLabel);
        }
    }

    public static void DrawPhonePreview(PhoneDialogueNode node, float width)
    {
        if (node == null || node.messages == null || node.messages.Count == 0)
        {
            EditorGUILayout.LabelField("No messages", MutedLabel);
            return;
        }

        int previewCount = Mathf.Min(5, node.messages.Count);
        for (int i = 0; i < previewCount; i++)
        {
            var message = node.messages[i];
            if (message == null) continue;
            string prefix = !string.IsNullOrWhiteSpace(message.senderName)
                ? message.senderName.Trim() + ": "
                : message.side == PhoneMessageSide.Incoming ? "< " : "> ";
            EditorGUILayout.LabelField(prefix + Truncate(message.text, 120), PreviewBox, GUILayout.Width(width));
        }

        if (node.messages.Count > previewCount)
            EditorGUILayout.LabelField("... " + (node.messages.Count - previewCount) + " more message(s)", MutedLabel);
    }

    public static void DrawSpritePreview(Sprite sprite, float width, float fixedAspect = -1f, float maxHeight = 160f)
    {
        if (sprite == null || sprite.texture == null)
            return;

        Rect texRect = new Rect(
            sprite.textureRect.x / sprite.texture.width,
            sprite.textureRect.y / sprite.texture.height,
            sprite.textureRect.width / sprite.texture.width,
            sprite.textureRect.height / sprite.texture.height);

        float height = fixedAspect > 0f
            ? width * fixedAspect
            : width * sprite.textureRect.height / Mathf.Max(1f, sprite.textureRect.width);
        height = Mathf.Min(height, maxHeight);

        Rect drawRect = GUILayoutUtility.GetRect(width, height);
        GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, texRect, true);
    }

    public static void EnsureChoicePorts(ChoiceNode node)
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
            {
                node.AddDynamicOutput(typeof(BaseStoryNode), Node.ConnectionType.Multiple, Node.TypeConstraint.None, portName);
                EditorUtility.SetDirty(node);
            }
        }
    }

    public static SceneSetupData CreateSceneDataSubAsset(SceneSetupNode node)
    {
        if (node == null)
            return null;

        var sceneData = ScriptableObject.CreateInstance<SceneSetupData>();
        sceneData.name = SafeObjectName(node.name) + "_SceneData";

        string graphPath = AssetDatabase.GetAssetPath(node.graph);
        if (!string.IsNullOrEmpty(graphPath))
            AssetDatabase.AddObjectToAsset(sceneData, graphPath);

        Undo.RecordObject(node, "Create Scene Setup Data");
        node.sceneData = sceneData;
        EditorUtility.SetDirty(sceneData);
        EditorUtility.SetDirty(node);
        AssetDatabase.SaveAssets();
        return sceneData;
    }

    static string SafeObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Scene";

        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value.Replace(' ', '_');
    }

    public static int CountPhoneWords(PhoneDialogueNode node)
    {
        if (node == null || node.messages == null) return 0;
        return CountWords(node.messages.Where(m => m != null).Select(m => m.text));
    }

    public static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        value = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length <= max ? value : value.Substring(0, max) + "...";
    }

    static int CountDialogueWords(IList<DialogueLine> lines)
    {
        if (lines == null) return 0;
        return CountWords(lines.Where(l => l != null).Select(l => l.richText));
    }

    static int CountDialogueChars(IList<DialogueLine> lines)
    {
        if (lines == null) return 0;
        int count = 0;
        foreach (var line in lines)
            count += line != null && line.richText != null ? line.richText.Length : 0;
        return count;
    }

    static int CountWords(IEnumerable<string> values)
    {
        int total = 0;
        foreach (var value in values)
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

    static GUIStyle PreviewBox
    {
        get
        {
            if (_previewBox == null)
            {
                _previewBox = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true,
                    fontSize = 10,
                    padding = new RectOffset(6, 6, 4, 4)
                };
                _previewBox.normal.textColor = new Color(0.86f, 0.9f, 0.95f);
            }
            return _previewBox;
        }
    }

    static GUIStyle MutedLabel
    {
        get
        {
            if (_mutedLabel == null)
            {
                _mutedLabel = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                _mutedLabel.normal.textColor = new Color(0.6f, 0.64f, 0.7f);
            }
            return _mutedLabel;
        }
    }
}
#endif
