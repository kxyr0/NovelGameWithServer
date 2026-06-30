using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueIdentityIssueSeverity
{
    Warning,
    Error
}

public sealed class DialogueIdentityValidationIssue
{
    public DialogueIdentityIssueSeverity Severity;
    public string StoryId = "";
    public string ChapterId = "";
    public string NodeId = "";
    public int LineIndex = -1;
    public string SpeakerId = "";
    public string Nameplate = "";
    public string Body = "";
    public DialogueIdentitySource Source = DialogueIdentitySource.Unknown;
    public string Message = "";

    public override string ToString()
    {
        return "[" + Severity + "] " +
               "story=" + StoryId +
               " chapter=" + ChapterId +
               " node=" + NodeId +
               " line=" + LineIndex +
               " speaker=" + SpeakerId +
               " nameplate=" + Nameplate +
               " source=" + Source +
               " message=" + Message;
    }
}

public sealed class DialogueIdentityValidationReport
{
    readonly List<DialogueIdentityValidationIssue> _issues = new List<DialogueIdentityValidationIssue>();

    public IReadOnlyList<DialogueIdentityValidationIssue> Issues => _issues;
    public int WarningCount { get; private set; }
    public int ErrorCount { get; private set; }
    public bool HasIssues => _issues.Count > 0;

    public void Add(DialogueIdentityValidationIssue issue)
    {
        if (issue == null)
            return;

        _issues.Add(issue);
        if (issue.Severity == DialogueIdentityIssueSeverity.Error)
            ErrorCount++;
        else
            WarningCount++;
    }

    public void AddRange(DialogueIdentityValidationReport report)
    {
        if (report == null || report.Issues == null)
            return;

        foreach (DialogueIdentityValidationIssue issue in report.Issues)
            Add(issue);
    }
}

public static class DialogueIdentityValidator
{
    public static DialogueIdentityValidationReport ValidateGraph(
        StoryGraph graph,
        string storyId = "",
        string chapterId = "",
        IEnumerable<string> knownNames = null)
    {
        var report = new DialogueIdentityValidationReport();
        if (graph == null || graph.nodes == null)
            return report;

        List<string> names = CollectGraphNames(graph, knownNames);

        foreach (var rawNode in graph.nodes)
        {
            BaseStoryNode node = rawNode as BaseStoryNode;
            if (node == null)
                continue;

            if (node is DialogueNode dialogue)
                ValidateLines(dialogue.lines, storyId, chapterId, node.guid, names, report);
            else if (node is ChoiceNode choice)
                ValidateLines(choice.lines, storyId, chapterId, node.guid, names, report);
            else if (node is CutsceneNode cutscene)
                ValidateLines(cutscene.lines, storyId, chapterId, node.guid, names, report);
        }

        return report;
    }

    public static DialogueIdentityValidationReport ValidateJsonDocument(
        StoryJsonDocument document,
        string assetLabel = "")
    {
        var report = new DialogueIdentityValidationReport();
        if (document == null || document.nodes == null)
            return report;

        string storyId = FirstNonEmpty(document.storyId, assetLabel);
        string chapterId = FirstNonEmpty(document.chapterId, document.episodeId);
        Dictionary<string, string> characterNames = BuildCharacterNameMap(document);
        List<string> knownNames = new List<string>(characterNames.Values);
        if (!string.IsNullOrWhiteSpace(document.defaultName))
            knownNames.Add(document.defaultName);
        if (!string.IsNullOrWhiteSpace(document.defaultPlayerName))
            knownNames.Add(document.defaultPlayerName);

        foreach (StoryJsonNode node in document.nodes)
        {
            if (node == null || node.lines == null)
                continue;

            for (int i = 0; i < node.lines.Count; i++)
            {
                StoryJsonLine line = node.lines[i];
                if (line == null || string.IsNullOrWhiteSpace(line.speaker))
                    continue;

                string speakerId = SaveDataSanitizer.SanitizeIdentifier(line.speaker);
                characterNames.TryGetValue(speakerId, out string displayName);
                bool playerSpeaker = DialogueIdentity.IsPlayerSpeaker(speakerId, null, displayName);

                if (playerSpeaker && !LooksLikePlayerNameReference(displayName))
                {
                    AddIssue(
                        report,
                        storyId,
                        chapterId,
                        node.id,
                        i,
                        speakerId,
                        displayName,
                        line.text,
                        DialogueIdentitySource.StoryActorMap,
                        "Player speaker uses literal character name in story actor map. Use {playerName}.");
                }

                string expectedName = playerSpeaker ? "" : displayName;
                List<string> literals = DialogueIdentityLiteralScanner.FindUnexpectedLiterals(
                    line.text,
                    expectedName,
                    displayName,
                    knownNames);
                if (literals.Count > 0)
                {
                    AddIssue(
                        report,
                        storyId,
                        chapterId,
                        node.id,
                        i,
                        speakerId,
                        displayName,
                        line.text,
                        DialogueIdentitySource.LiteralText,
                        "Body text contains literal names that differ from speaker mapping: " + string.Join(", ", literals.ToArray()));
                }
            }
        }

        return report;
    }

    static void ValidateLines(
        IReadOnlyList<DialogueLine> lines,
        string storyId,
        string chapterId,
        string nodeId,
        List<string> knownNames,
        DialogueIdentityValidationReport report)
    {
        if (lines == null)
            return;

        for (int i = 0; i < lines.Count; i++)
        {
            DialogueLine line = lines[i];
            if (line == null || (line.speaker == null && string.IsNullOrWhiteSpace(line.speakerId)))
                continue;

            DialogueIdentityResult identity = DialogueIdentity.ResolveSpeaker(new DialogueIdentityRequest
            {
                StoryId = storyId,
                ChapterId = chapterId,
                NodeId = nodeId,
                LineIndex = i,
                Line = line,
                BodyText = line.richText ?? ""
            });

            string rawName = line.speaker != null ? line.speaker.characterName : "";
            string resolvedBody = DialogueVariableResolver.ResolveText(
                line.richText ?? "",
                DialogueVariableContext.StoryUi(nameof(DialogueIdentityValidator), null, storyId, chapterId, identity));

            if (identity.IsFallback && !string.IsNullOrWhiteSpace(identity.SpeakerId))
            {
                AddIssue(
                    report,
                    storyId,
                    chapterId,
                    nodeId,
                    i,
                    identity.SpeakerId,
                    identity.DisplayName,
                    resolvedBody,
                    identity.Source,
                    "Fallback display name was used despite speaker mapping.");
            }

            if (line.speaker == null && !string.IsNullOrWhiteSpace(line.speakerId))
            {
                AddIssue(
                    report,
                    storyId,
                    chapterId,
                    nodeId,
                    i,
                    identity.SpeakerId,
                    identity.DisplayName,
                    resolvedBody,
                    DialogueIdentitySource.Fallback,
                    "Dialogue line has speaker id but missing CharacterData.");
            }

            if (identity.IsDynamicPlayerName &&
                !LooksLikePlayerNameReference(rawName) &&
                !NamesEqual(rawName, identity.DisplayName))
            {
                AddIssue(
                    report,
                    storyId,
                    chapterId,
                    nodeId,
                    i,
                    identity.SpeakerId,
                    identity.DisplayName,
                    resolvedBody,
                    identity.Source,
                    "Player line nameplate resolves from profile but CharacterData has a different literal display name.");
            }

            if (!identity.IsDynamicPlayerName &&
                !string.IsNullOrWhiteSpace(line.speakerNameHint) &&
                !LooksLikeIdentifier(line.speakerNameHint) &&
                !NamesEqual(line.speakerNameHint, identity.DisplayName))
            {
                AddIssue(
                    report,
                    storyId,
                    chapterId,
                    nodeId,
                    i,
                    identity.SpeakerId,
                    identity.DisplayName,
                    resolvedBody,
                    identity.Source,
                    "CharacterData display name differs from story actor display name without explicit alias config.");
            }

            List<string> literals = DialogueIdentityLiteralScanner.FindUnexpectedLiterals(
                resolvedBody,
                identity.DisplayName,
                rawName,
                knownNames);
            if (literals.Count > 0)
            {
                AddIssue(
                    report,
                    storyId,
                    chapterId,
                    nodeId,
                    i,
                    identity.SpeakerId,
                    identity.DisplayName,
                    resolvedBody,
                    DialogueIdentitySource.LiteralText,
                    "Body text contains literal names that differ from resolved speaker display name: " + string.Join(", ", literals.ToArray()));
            }
        }
    }

    static List<string> CollectGraphNames(StoryGraph graph, IEnumerable<string> knownNames)
    {
        var result = new List<string>();
        if (knownNames != null)
            result.AddRange(knownNames);

        if (graph == null || graph.nodes == null)
            return result;

        foreach (var rawNode in graph.nodes)
        {
            BaseStoryNode node = rawNode as BaseStoryNode;
            if (node == null)
                continue;

            if (node is DialogueNode dialogue)
                CollectLineNames(dialogue.lines, result);
            else if (node is ChoiceNode choice)
                CollectLineNames(choice.lines, result);
            else if (node is CutsceneNode cutscene)
                CollectLineNames(cutscene.lines, result);
        }

        return result;
    }

    static void CollectLineNames(IEnumerable<DialogueLine> lines, List<string> names)
    {
        if (lines == null)
            return;

        foreach (DialogueLine line in lines)
        {
            if (line == null)
                continue;

            if (line.speaker != null && !string.IsNullOrWhiteSpace(line.speaker.characterName))
                names.Add(line.speaker.characterName);
            if (!string.IsNullOrWhiteSpace(line.speakerNameHint))
                names.Add(line.speakerNameHint);
        }
    }

    static Dictionary<string, string> BuildCharacterNameMap(StoryJsonDocument document)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (document == null || document.characters == null)
            return result;

        foreach (StoryJsonCharacter character in document.characters)
        {
            if (character == null)
                continue;

            string id = SaveDataSanitizer.SanitizeIdentifier(FirstNonEmpty(character.id, character.asset, character.guid, character.name));
            if (string.IsNullOrWhiteSpace(id))
                continue;

            result[id] = FirstNonEmpty(character.name, character.id, character.asset, character.guid);
        }

        return result;
    }

    static void AddIssue(
        DialogueIdentityValidationReport report,
        string storyId,
        string chapterId,
        string nodeId,
        int lineIndex,
        string speakerId,
        string nameplate,
        string body,
        DialogueIdentitySource source,
        string message)
    {
        report.Add(new DialogueIdentityValidationIssue
        {
            Severity = DialogueIdentityIssueSeverity.Warning,
            StoryId = storyId ?? "",
            ChapterId = chapterId ?? "",
            NodeId = nodeId ?? "",
            LineIndex = lineIndex,
            SpeakerId = speakerId ?? "",
            Nameplate = nameplate ?? "",
            Body = body ?? "",
            Source = source,
            Message = message ?? ""
        });
    }

    static bool LooksLikePlayerNameReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (DialogueVariableResolver.IsPlayerNameToken(value))
            return true;

        return value.IndexOf("playerName", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("player_name", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("heroName", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("hero_name", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool LooksLikeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || c == '/'))
                return false;
        }

        return value.IndexOf('_') >= 0 || value.IndexOf('-') >= 0 || value.IndexOf('/') >= 0;
    }

    static bool NamesEqual(string left, string right)
    {
        left = SaveDataSanitizer.SanitizePlayerName(left);
        right = SaveDataSanitizer.SanitizePlayerName(right);
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return "";
    }
}
