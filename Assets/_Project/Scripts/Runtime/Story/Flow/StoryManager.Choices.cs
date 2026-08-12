using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using XNode;

public partial class StoryManager
{
    void ProcessChoice(ChoiceNode node)
    {
        if (node == null || !EnsureDialogueUI("showing choices"))
            return;

        GameState.Instance.currentNode = node;

        activeDialogueNode = null;
        currentLineIndex = 0;
        ResetDialogueLinePages();
        dialogueUI.ClearChoices();
        dialogueUI.ClearDialogue();

        DialogueLine choiceHeader = node.lines != null && node.lines.Count > 0 ? node.lines[0] : null;

        if (node.options == null || node.options.Count == 0)
        {
            Debug.LogWarning("[StoryManager] Choice node has no options.", node);
            GoNext(node, "exit");
            return;
        }

        RegionAccessGate.EnsureIpLookupStarted();

        if (ChoiceOptionVisibility.CountVisibleOptions(node) == 0)
        {
            if (ChoiceRegionFilter.HasRegionSensitiveOptions(node) && RegionAccessGate.IsIpRegionLookupPending())
            {
                StartCoroutine(WaitForRegionThenProcessChoice(node));
                return;
            }

            Debug.LogWarning("[StoryManager] Choice node has no visible options for the current region.", node);
            GoNext(node, "exit");
            return;
        }

        CaptureSubscriptionChoiceCheckpoint(node);

        if (!dialogueUI.ShowChoice(node))
        {
            Debug.LogError("[StoryManager] Choice UI failed. The story cannot show this choice node.", node);
        }

        if (HasVisibleChoiceHeader(choiceHeader))
        {
            dialogueUI.ShowChoiceHeader(choiceHeader);
            RecordDialogueHistory(choiceHeader);
        }
        else
        {
            dialogueUI.ShowChoicePlaceholder();
        }

        PersistProgress(node);
    }

    static bool HasVisibleChoiceHeader(DialogueLine line)
    {
        if (line == null || string.IsNullOrWhiteSpace(line.richText))
            return false;

        string trimmed = line.richText.Trim();
        return trimmed != "." &&
               trimmed != "..." &&
               trimmed != "\u2026";
    }

    IEnumerator WaitForRegionThenProcessChoice(ChoiceNode node)
    {
        const float timeout = 6f;
        float elapsed = 0f;

        while (RegionAccessGate.IsIpRegionLookupPending() && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (node != null)
            ProcessChoice(node);
    }

    public void SelectChoice(ChoiceNode node, int index)
    {
        if (node == null || node.options == null || index < 0 || index >= node.options.Count)
            return;

        var option = node.options[index];
        if (option == null || GameState.Instance == null)
            return;

        string pendingKey = GetChoicePendingKey(node);
        if (!string.IsNullOrEmpty(pendingKey) && _pendingChoiceSelections.Contains(pendingKey))
            return;

        if (!ChoiceRegionFilter.IsVisible(option))
            return;

        if (!string.IsNullOrEmpty(option.requiredVariable))
        {
            if (GameState.Instance.GetInt(option.requiredVariable) < option.requiredValue)
                return;
        }

        if (option.isPremium)
        {
            int premiumCost = option.premiumCost;
            if (!IsValidPremiumCost(premiumCost))
            {
                Debug.LogWarning("[StoryManager] Refused premium choice with invalid cost: " + premiumCost);
                return;
            }

            if (!NetworkManager.IsAuthenticated && PlayerData.Hearts < premiumCost)
                return;

            if (!string.IsNullOrEmpty(pendingKey) && !_pendingChoiceSelections.Add(pendingKey))
                return;

            StartCoroutine(SpendPremiumChoiceAndSelect(node, index, premiumCost, pendingKey));
            return;
        }

        DoSelectChoice(node, index);
    }

    /// <summary>Spend a premium choice once and continue only after confirmation.</summary>
    IEnumerator SpendPremiumChoiceAndSelect(ChoiceNode node, int index, int cost, string pendingKey)
    {
        bool ok = false;
        bool usedNetworkSpend = NetworkManager.Instance != null;

        if (usedNetworkSpend)
        {
            string purchaseKey = GetChoicePurchaseKey(node, index);
            yield return NetworkManager.Instance.SpendHearts(
                cost,
                "premium_choice",
                node != null ? node.guid : "",
                index,
                purchaseKey,
                result => ok = result);
        }
        else
        {
            ok = TrySpendPremiumChoiceLocally(cost);
        }

        if (!ok && usedNetworkSpend)
        {
            Debug.LogWarning(
                "[StoryManager] Premium choice spend was not confirmed. " +
                $"Trying local fallback if allowed. node='{(node != null ? node.guid : "")}', " +
                $"index={index}, cost={cost}, hearts={PlayerData.Hearts}, " +
                $"authenticated={NetworkManager.IsAuthenticated}, " +
                $"localFallbackEnabled={PrototypeFeatureFlags.LocalPremiumSpendEnabled}");

            ok = TrySpendPremiumChoiceLocally(cost);
        }

        if (!string.IsNullOrEmpty(pendingKey))
            _pendingChoiceSelections.Remove(pendingKey);

        if (ok && GameState.Instance != null && GameState.Instance.currentNode == node)
            DoSelectChoice(node, index);
    }

    void DoSelectChoice(ChoiceNode node, int index)
    {
        if (node == null || index < 0)
            return;

        ConfirmSubscriptionChoice(node, index);
        RecordChoiceHistory(node, index);

        var port = node.GetOutputPort("choices " + index);
        if (port != null && port.Connection != null)
            ProcessNode(port.Connection.node as BaseStoryNode);
    }

    /// <summary>Фолбэк: NetworkManager недоступен — списываем локально и переходим.</summary>
    bool TrySpendPremiumChoiceLocally(int cost)
    {
        if (!PrototypeFeatureFlags.LocalPremiumSpendEnabled)
        {
            Debug.LogWarning("[StoryManager] Local premium choice fallback is disabled.");
            return false;
        }

        if (!IsValidPremiumCost(cost))
        {
            Debug.LogWarning("[StoryManager] Local premium choice fallback refused invalid cost: " + cost);
            return false;
        }

        if (PlayerData.Hearts < cost)
            return false;

        PlayerData.AddHeartValue(-cost);
        return true;
    }

    string GetChoicePendingKey(ChoiceNode node)
    {
        string nodeGuid = node != null ? SaveDataSanitizer.SanitizeIdentifier(node.guid) : "";
        return !string.IsNullOrEmpty(nodeGuid)
            ? "choice:" + nodeGuid
            : node != null ? "choice-instance:" + node.GetInstanceID() : "";
    }

    string GetChoicePurchaseKey(ChoiceNode node, int index)
    {
        string nodeGuid = node != null ? SaveDataSanitizer.SanitizeIdentifier(node.guid) : "";
        if (string.IsNullOrEmpty(nodeGuid))
            return "";

        return nodeGuid + ":" + Mathf.Max(0, index);
    }

    static bool IsValidPremiumCost(int cost)
    {
        return cost > 0 && cost <= SaveDataSanitizer.MaxCurrencyValue;
    }

    void ProcessVariable(VariableChangeNode node)
    {
        if (node == null || !EnsureGameState("applying variable change"))
            return;

        int val = GameState.Instance.GetInt(node.variableKey);
        int newValue = node.Add
            ? SaveDataSanitizer.ClampStatDelta(val, node.deltaValue)
            : SaveDataSanitizer.ClampStatValue(node.deltaValue);
        GameState.Instance.SetInt(node.variableKey, newValue);

        int appliedDelta = newValue - val;
        RecordEpisodeStatDelta(node.variableKey, appliedDelta);
        ReportRelationshipStatChange(node.variableKey, appliedDelta);
        string displayName = ResolveStatDisplayName(node.variableKey, "");
        if (appliedDelta != 0 && !string.IsNullOrWhiteSpace(displayName))
            ShowStatChangeFeedback(node.variableKey, displayName, appliedDelta, "");

        GoNext(node, "exit");
    }

    void ReportRelationshipStatChange(string statId, int delta)
    {
        if (delta == 0 || NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
            return;

        string characterId = ExtractRelationshipCharacterId(statId);
        if (string.IsNullOrEmpty(characterId))
            return;

        StartCoroutine(NetworkManager.Instance.UpdateRelationship(characterId, delta, CurrentStoryId, (ok, payload) =>
        {
            if (!ok)
                Debug.LogWarning("[StoryManager] Relationship update failed: " + payload);
        }));
    }

    static string ExtractRelationshipCharacterId(string statId)
    {
        statId = SaveDataSanitizer.SanitizeIdentifier(statId);
        if (string.IsNullOrEmpty(statId))
            return "";

        string lower = statId.ToLowerInvariant();
        string[] prefixes =
        {
            "relationship:",
            "relationship_",
            "relationship-",
            "relationship.",
            "rel:",
            "rel_",
            "rel-",
            "rel."
        };

        for (int i = 0; i < prefixes.Length; i++)
        {
            if (!lower.StartsWith(prefixes[i], StringComparison.Ordinal))
                continue;

            return SaveDataSanitizer.SanitizeIdentifier(statId.Substring(prefixes[i].Length));
        }

        return "";
    }

    void ProcessCondition(ConditionNode node)
    {
        if (node == null || !EnsureGameState("checking a condition"))
            return;

        int leftValue = GameState.Instance.GetInt(node.variableKey);
        int rightValue = string.IsNullOrWhiteSpace(node.compareVariableKey)
            ? node.requiredValue
            : GameState.Instance.GetInt(node.compareVariableKey);

        if (EvaluateCondition(leftValue, rightValue, node.comparison))
            GoNext(node, "trueExit");
        else
            GoNext(node, "falseExit");
    }

    static bool EvaluateCondition(int leftValue, int rightValue, ConditionComparison comparison)
    {
        switch (comparison)
        {
            case ConditionComparison.NotEquals:
                return leftValue != rightValue;
            case ConditionComparison.GreaterThan:
                return leftValue > rightValue;
            case ConditionComparison.GreaterOrEqual:
                return leftValue >= rightValue;
            case ConditionComparison.LessThan:
                return leftValue < rightValue;
            case ConditionComparison.LessOrEqual:
                return leftValue <= rightValue;
            case ConditionComparison.Equals:
            default:
                return leftValue == rightValue;
        }
    }

    void GoNext(BaseStoryNode node, string portName)
    {
        if (node == null)
        {
            OnChapterFinished();
            return;
        }

        var port = node.GetOutputPort(portName);

        if (port == null || port.Connection == null)
        {
            OnChapterFinished(node);
            return;
        }

        var nextNode = port.Connection.node as BaseStoryNode;
        if (nextNode == null)
        {
            OnChapterFinished(node);
            return;
        }

        ProcessNode(nextNode);
    }
}
