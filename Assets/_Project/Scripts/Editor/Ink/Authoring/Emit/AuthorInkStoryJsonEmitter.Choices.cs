#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal sealed partial class AuthorInkStoryJsonEmitter
{
    void EmitChoice(AuthorInkChoiceStatement choice, AuthorInkFlowCursor cursor)
    {
        if (LooksLikeWardrobeSection(choice) && CanMapWardrobeBranches(choice))
        {
            EmitWardrobe(choice, choice.Prompt, cursor, false);
            return;
        }

        StoryJsonNode node = NewNode(choice.Line, StoryJsonTypes.Choice, "choice", "Выбор");
        node.choicePrompt = StoryJsonConverter.SanitizeDisplayText(choice.Prompt);
        ConnectIncomingAndBind(node, cursor);
        _document.nodes.Add(node);
        cursor.Open.Clear();

        for (int i = 0; i < choice.Branches.Count; i++)
        {
            AuthorInkChoiceBranch branch = choice.Branches[i];
            string onceKey = branch.OnceOnly ? OnceKey(branch.Line) : "";
            node.choices.Add(new StoryJsonChoice
            {
                text = CleanChoiceDisplayText(branch.Text, branch.PremiumCost),
                isPremium = branch.PremiumCost > 0,
                premiumCost = branch.PremiumCost,
                requiredVariable = onceKey,
                requiredValue = branch.OnceOnly ? 1 : 0,
                hideWhenRequirementNotMet = branch.OnceOnly
            });

            var branchCursor = new AuthorInkFlowCursor();
            branchCursor.Open.Add(new AuthorInkExitRef(node, AuthorInkExitKind.Choice, i));
            if (branch.OnceOnly)
                EmitInternalSet(onceKey, 0, branch.Line, branchCursor, "consume");
            if (!string.IsNullOrWhiteSpace(branch.Label))
                EmitInternalSet(ChoiceKey(branch.Label), 1, branch.Line, branchCursor, "selected");

            EmitSequence(branch.Body, branchCursor);
            cursor.Open.AddRange(branchCursor.Open);
            MovePendingAnchors(branchCursor, cursor);
        }

        if (!string.IsNullOrWhiteSpace(choice.ContinuationAnchor))
            AddPendingAnchor(cursor, choice.ContinuationAnchor);
    }

    void EmitWardrobe(AuthorInkChoiceStatement choice, string prompt, AuthorInkFlowCursor cursor, bool explicitWardrobe)
    {
        StoryJsonNode node = NewNode(choice.Line, StoryJsonTypes.WardrobeChoice, "wardrobe", "Гардероб");
        node.characterId = _options.HeroCharacterId;
        node.label = StoryJsonConverter.SanitizeDisplayText(prompt);
        ConnectIncomingAndBind(node, cursor);
        _document.nodes.Add(node);
        cursor.Open.Clear();

        for (int i = 0; i < choice.Branches.Count; i++)
        {
            AuthorInkChoiceBranch branch = choice.Branches[i];
            string itemId = ExtractWardrobeItemId(branch, out List<AuthorInkStatement> remainingBody);
            if (string.IsNullOrWhiteSpace(itemId))
            {
                itemId = CleanWardrobeLabel(branch.Text);
                if (explicitWardrobe)
                    _report.Warn(branch.Line, "Для гардероба нет явного item id; используется подпись как id: '" + itemId + "'.");
            }

            node.clothes.Add(itemId);
            node.premiumCosts.Add(branch.PremiumCost);
            node.exits.Add("");

            var branchCursor = new AuthorInkFlowCursor();
            branchCursor.Open.Add(new AuthorInkExitRef(node, AuthorInkExitKind.Wardrobe, i));
            EmitSequence(remainingBody, branchCursor);
            cursor.Open.AddRange(branchCursor.Open);
            MovePendingAnchors(branchCursor, cursor);
        }

        if (!string.IsNullOrWhiteSpace(choice.ContinuationAnchor))
            AddPendingAnchor(cursor, choice.ContinuationAnchor);
    }

    void EmitSwitch(AuthorInkSwitchStatement inkSwitch, AuthorInkFlowCursor cursor)
    {
        var incoming = new List<AuthorInkExitRef>(cursor.Open);
        cursor.Open.Clear();
        List<string> pendingAnchors = new List<string>(cursor.PendingAnchors);
        cursor.PendingAnchors.Clear();
        AuthorInkExitRef previousFalse = null;

        for (int i = 0; i < inkSwitch.Cases.Count; i++)
        {
            AuthorInkSwitchCase item = inkSwitch.Cases[i];
            if (item.IsElse)
            {
                var elseCursor = new AuthorInkFlowCursor();
                if (previousFalse != null) elseCursor.Open.Add(previousFalse);
                else elseCursor.Open.AddRange(incoming);
                elseCursor.PendingAnchors.AddRange(pendingAnchors);
                EmitSequence(item.Body, elseCursor);
                cursor.Open.AddRange(elseCursor.Open);
                MovePendingAnchors(elseCursor, cursor);
                previousFalse = null;
                continue;
            }

            StoryJsonNode condition = NewNode(item.Line, StoryJsonTypes.Condition, "condition", "Ink branch " + item.Label);
            condition.variableKey = ChoiceKey(item.Label);
            condition.requiredValue = 1;
            condition.comparison = "GreaterOrEqual";

            if (previousFalse != null)
                previousFalse.Connect(condition.id);
            else
            {
                for (int j = 0; j < incoming.Count; j++) incoming[j].Connect(condition.id);
                BindAnchors(pendingAnchors, condition.id);
            }
            _document.nodes.Add(condition);

            var trueCursor = new AuthorInkFlowCursor();
            trueCursor.Open.Add(new AuthorInkExitRef(condition, AuthorInkExitKind.True));
            EmitSequence(item.Body, trueCursor);
            cursor.Open.AddRange(trueCursor.Open);
            MovePendingAnchors(trueCursor, cursor);
            previousFalse = new AuthorInkExitRef(condition, AuthorInkExitKind.False);
        }

        if (previousFalse != null)
            cursor.Open.Add(previousFalse);
    }

}
#endif
