#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal sealed partial class AuthorInkStoryJsonEmitter
{
    void EmitInlineCondition(AuthorInkInlineConditionStatement conditionData, AuthorInkFlowCursor cursor)
    {
        StoryJsonNode node = NewNode(conditionData.Line, StoryJsonTypes.Condition, "condition", "Ink condition");
        node.variableKey = conditionData.Variable;
        node.requiredValue = conditionData.RequiredValue;
        node.comparison = conditionData.Comparison;
        ConnectIncomingAndBind(node, cursor);
        cursor.Open.Clear();
        _document.nodes.Add(node);
        AddRoute(new AuthorInkExitRef(node, AuthorInkExitKind.True), conditionData.TrueTarget, conditionData.Line);
        AddRoute(new AuthorInkExitRef(node, AuthorInkExitKind.False), conditionData.FalseTarget, conditionData.Line);
    }

    void Place(StoryJsonNode node, AuthorInkFlowCursor cursor)
    {
        ConnectIncomingAndBind(node, cursor);
        _document.nodes.Add(node);
        cursor.Open.Clear();
        cursor.Open.Add(new AuthorInkExitRef(node, AuthorInkExitKind.Next));
    }

    void ConnectIncomingAndBind(StoryJsonNode node, AuthorInkFlowCursor cursor)
    {
        for (int i = 0; i < cursor.Open.Count; i++)
            cursor.Open[i].Connect(node.id);
        BindAnchors(cursor.PendingAnchors, node.id);
        cursor.PendingAnchors.Clear();
    }

    void Route(AuthorInkFlowCursor cursor, string target, int line)
    {
        for (int i = 0; i < cursor.Open.Count; i++)
            AddRoute(cursor.Open[i], target, line);
        cursor.Open.Clear();

        for (int i = 0; i < cursor.PendingAnchors.Count; i++)
            _aliases[cursor.PendingAnchors[i]] = target;
        cursor.PendingAnchors.Clear();
    }

    void AddRoute(AuthorInkExitRef exit, string target, int line)
    {
        if (IsTerminalTarget(target))
        {
            exit.Connect(EnsureTerminal());
            return;
        }
        _routes.Add(new AuthorInkPendingRoute { Exit = exit, Target = target, Line = line });
    }

    void ResolveRoutes()
    {
        for (int i = 0; i < _routes.Count; i++)
        {
            AuthorInkPendingRoute route = _routes[i];
            string target = ResolveAlias(route.Target, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            if (_anchors.TryGetValue(target, out string nodeId))
                route.Exit.Connect(nodeId);
            else if (IsTerminalTarget(target))
                route.Exit.Connect(EnsureTerminal());
            else
            {
                route.Exit.Connect(EnsureTerminal());
                _report.Warn(route.Line, "Divert target '" + route.Target + "' не найден в эпизоде; ветка завершает серию вместо битой ссылки.");
            }
        }
    }

    string ResolveAlias(string target, HashSet<string> visited)
    {
        string current = (target ?? "").Trim();
        while (_aliases.TryGetValue(current, out string next) && visited.Add(current))
            current = next;
        return current;
    }

    bool IsTerminalTarget(string target)
    {
        string value = (target ?? "").Trim();
        return string.Equals(value, "END", StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(_options.NextEpisodeKnot) && string.Equals(value, _options.NextEpisodeKnot, StringComparison.OrdinalIgnoreCase));
    }

    string EnsureTerminal()
    {
        if (!string.IsNullOrEmpty(_terminalId))
            return _terminalId;
        StoryJsonNode terminal = NewNode(int.MaxValue, StoryJsonTypes.Scene, "end", "Конец серии");
        terminal.label = "__episode_end";
        _terminalId = terminal.id;
        _document.nodes.Add(terminal);
        return _terminalId;
    }

    void AddPendingAnchor(AuthorInkFlowCursor cursor, string name)
    {
        if (!string.IsNullOrWhiteSpace(name) && !cursor.PendingAnchors.Contains(name))
            cursor.PendingAnchors.Add(name.Trim());
    }

    void BindAnchors(List<string> anchors, string nodeId)
    {
        for (int i = 0; i < anchors.Count; i++)
        {
            string name = anchors[i];
            if (_anchors.ContainsKey(name))
                _report.Warn(_currentLine, "Anchor '" + name + "' объявлен повторно; используется последнее объявление.");
            _anchors[name] = nodeId;
        }
    }


    void ConnectOpenToTerminal(AuthorInkFlowCursor cursor)
    {
        if (cursor.Open.Count == 0)
            return;
        string terminalId = EnsureTerminal();
        for (int i = 0; i < cursor.Open.Count; i++)
            cursor.Open[i].Connect(terminalId);
        cursor.Open.Clear();
    }

    void BindPendingAnchorsToTerminal(AuthorInkFlowCursor cursor)
    {
        if (cursor.PendingAnchors.Count == 0) return;
        BindAnchors(cursor.PendingAnchors, EnsureTerminal());
        cursor.PendingAnchors.Clear();
    }

    static void MovePendingAnchors(AuthorInkFlowCursor from, AuthorInkFlowCursor to)
    {
        for (int i = 0; i < from.PendingAnchors.Count; i++)
            if (!to.PendingAnchors.Contains(from.PendingAnchors[i]))
                to.PendingAnchors.Add(from.PendingAnchors[i]);
        from.PendingAnchors.Clear();
    }

}
#endif
