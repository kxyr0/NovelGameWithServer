#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

internal sealed partial class AuthorInkStoryJsonEmitter
{
    readonly AuthorInkCompileOptions _options;
    readonly AuthorInkSharedContext _shared;
    readonly AuthorInkImportReport _report;
    readonly AuthorInkTextMapper _textMapper;
    readonly AuthorInkLogicMapper _logicMapper;
    readonly StoryJsonDocument _document;
    readonly Dictionary<string, string> _anchors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    readonly List<AuthorInkPendingRoute> _routes = new List<AuthorInkPendingRoute>();
    readonly Dictionary<int, int> _idOrdinals = new Dictionary<int, int>();
    string _terminalId;
    int _currentLine;

    public AuthorInkStoryJsonEmitter(
        AuthorInkCompileOptions options,
        AuthorInkSharedContext shared,
        AuthorInkImportReport report)
    {
        _options = options;
        _shared = shared;
        _report = report;
        _textMapper = new AuthorInkTextMapper(shared);
        _logicMapper = new AuthorInkLogicMapper(shared);
        _document = new StoryJsonDocument
        {
            version = 2,
            storyId = options.StoryId,
            chapterId = options.EpisodeId,
            episodeId = options.EpisodeId,
            title = options.Title,
            defaultName = options.DefaultName,
            defaultPlayerName = options.DefaultName
        };
    }

    public StoryJsonDocument Emit(List<AuthorInkStatement> statements)
    {
        EmitCharacterManifest();

        var start = NewNode(0, StoryJsonTypes.Start, "start", "Start");
        start.id = "start";
        start.guid = "start";
        _document.nodes.Add(start);

        var cursor = new AuthorInkFlowCursor();
        cursor.Open.Add(new AuthorInkExitRef(start, AuthorInkExitKind.Next));

        EmitOnceInitializers(statements, cursor);
        EmitSequence(statements, cursor);
        BindPendingAnchorsToTerminal(cursor);
        ConnectOpenToTerminal(cursor);
        EnsureTerminal();
        ResolveRoutes();

        _report.OutputNodes = _document.nodes.Count;
        return _document;
    }

    void EmitSequence(List<AuthorInkStatement> statements, AuthorInkFlowCursor cursor)
    {
        if (statements == null)
            return;

        for (int i = 0; i < statements.Count; i++)
        {
            AuthorInkStatement statement = statements[i];
            _currentLine = statement.Line;

            if (statement is AuthorInkAnchorStatement anchor)
            {
                AddPendingAnchor(cursor, anchor.Name);
                continue;
            }
            if (statement is AuthorInkTextStatement text)
            {
                EmitText(text, cursor);
                continue;
            }
            if (statement is AuthorInkDirectiveStatement directive)
            {
                StoryJsonNode node = AuthorInkDirectiveMapper.Map(directive, kind => NextId(directive.Line, kind), _report);
                if (node != null)
                    Place(node, cursor);
                continue;
            }
            if (statement is AuthorInkLogicStatement logic)
            {
                if (_logicMapper.TryMap(logic.Raw, logic.Line, kind => NextId(logic.Line, kind), _report, out StoryJsonNode node) && node != null)
                    Place(node, cursor);
                continue;
            }
            if (statement is AuthorInkDivertStatement divert)
            {
                Route(cursor, divert.Target, divert.Line);
                continue;
            }
            if (statement is AuthorInkInlineConditionStatement inlineCondition)
            {
                EmitInlineCondition(inlineCondition, cursor);
                continue;
            }
            if (statement is AuthorInkChoiceStatement choice)
            {
                EmitChoice(choice, cursor);
                continue;
            }
            if (statement is AuthorInkWardrobeStatement wardrobe)
            {
                EmitWardrobe(wardrobe.Choice, wardrobe.Prompt, cursor, true);
                continue;
            }
            if (statement is AuthorInkSwitchStatement inkSwitch)
                EmitSwitch(inkSwitch, cursor);
        }
    }

    void EmitText(AuthorInkTextStatement block, AuthorInkFlowCursor cursor)
    {
        const int maxLinesPerNode = 12;
        StoryJsonNode node = null;
        for (int i = 0; i < block.Lines.Count; i++)
        {
            if (node == null || node.lines.Count >= maxLinesPerNode)
            {
                node = NewNode(block.Lines[i].Line, StoryJsonTypes.Dialogue, "dialogue", "Диалог");
                Place(node, cursor);
            }

            StoryJsonLine mapped = _textMapper.Map(block.Lines[i], _report);
            if (!string.IsNullOrWhiteSpace(mapped.speaker) &&
                string.Equals(mapped.speaker, _options.DefaultName, StringComparison.OrdinalIgnoreCase))
            {
                mapped.speaker = _options.HeroCharacterId;
            }
            if (string.IsNullOrWhiteSpace(mapped.text))
                continue;
            node.lines.Add(mapped);
            _report.DialogueLines++;
        }
    }
}
#endif
