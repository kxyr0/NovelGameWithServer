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
    void ProcessPhoneDialogue(PhoneDialogueNode node)
    {
        RecordPhoneHistory(node);

        if (IsAutoSkippingStoryContent())
        {
            GoNext(node, "exit");
            return;
        }

        if (phoneDialogueUI != null)
        {
            phoneDialogueUI.BringToFrontForStory();
            phoneDialogueUI.Show(node, () => GoNext(node, "exit"));
        }
        else
        {
            AppLogger.Warn(
                AppLogCategory.PhoneDialogue,
                nameof(StoryManager),
                nameof(ProcessPhoneDialogue),
                "StoryManager.phoneDialogueUI не назначен. PhoneDialogueNode будет пропущена.",
                LogMetadata.Of("node", node != null ? node.name : "", "guid", node != null ? node.guid : ""));
            GoNext(node, "exit");
        }
    }

    void ProcessAppearanceChoice(AppearanceChoiceNode node)
    {
        if (node == null || !EnsureDialogueUI("showing an appearance choice"))
            return;

        BeginHeroSetupStoryUiSuppression();

        dialogueUI.ClearChoices();
        dialogueUI.ClearDialogue();

        if (!string.IsNullOrEmpty(node.promptText))
            dialogueUI.ShowAppearancePrompt(node.promptText);

        if (node.options == null || node.options.Count == 0)
        {
            Debug.LogWarning("[StoryManager] AppearanceChoiceNode has no options.", node);
            GoNext(node, "exit");
            return;
        }

        if (!dialogueUI.ShowAppearanceChoice(node))
        {
            Debug.LogError("[StoryManager] Appearance choice UI failed. Auto-selecting the first appearance option to avoid blocking the story.", node);
            SelectAppearance(node, 0);
        }
    }

    public void SelectAppearance(AppearanceChoiceNode node, int index)
    {
        if (node == null || node.options == null || index < 0 || index >= node.options.Count)
            return;

        var option = node.options[index];
        if (option == null)
            return;

        PlayerAppearance.SetAppearance(option.type);
        HeroCustomizationStore.SaveAppearanceForStory(CurrentStoryId, option.type);

        if (node.singleExit)
        {
            GoNext(node, "exit");
        }
        else
        {
            var port = node.GetOutputPort("choices " + index);
            if (port != null && port.Connection != null)
                ProcessNode(port.Connection.node as BaseStoryNode);
            else
                GoNext(node, "exit");
        }
    }

    /// <summary>
    /// Открыть магазин при нехватке валюты.
    /// Подключи shopController в инспекторе.
    /// </summary>
    public void OpenShopForCurrency()
    {
        if (ShopController.Instance != null)
        {
            ShopController.Instance.Open();
            return;
        }

        if (shopPanel != null)
            shopPanel.SetActive(true);
        else
            Debug.LogWarning("StoryManager: shopPanel не назначен в инспекторе");
    }

    public void ProcessNode(BaseStoryNode node, bool trackHistory = true, bool syncProgress = true)
    {
        if (node == null) return;
        if (GameState.Instance == null)
        {
            Debug.LogError("[StoryManager] GameState is not available.");
            return;
        }

        if (!(node is CutsceneNode) && !(node is SceneSetupNode))
            ClearCutsceneRuntimeState(!cutsceneBackgroundSceneActive);

        RestoreHeroSetupStoryUiForNode(node);

        GameState.Instance.currentNode = node;
        PlayerCollectionState.TryUnlockStoryNode(CurrentStoryId, node);

        AppLogger.Info(
            AppLogCategory.StoryFlow,
            nameof(StoryManager),
            nameof(ProcessNode),
            "[STORY][NODE] Processing story node.",
            BuildStoryNodeMetadata(node, trackHistory, syncProgress));

        var history = storyHistory ?? StoryHistory.Instance;
        if (trackHistory)
            history?.Push(node);

        if (syncProgress && ShouldPersistProgressForNode(node))
            PersistProgress(node);

        if (node is SceneSetupNode)
            ProcessScene(node as SceneSetupNode);
        else if (node is StartNode)
            GoNext(node, "exit");
        else if (node is CutsceneNode)
            ProcessCutscene(node as CutsceneNode);
        else if (node is DialogueNode)
            ProcessDialogue(node as DialogueNode);
        else if (node is ChoiceNode)
            ProcessChoice(node as ChoiceNode);
        else if (node is VariableChangeNode)
            ProcessVariable(node as VariableChangeNode);
        else if (node is ConditionNode)
            ProcessCondition(node as ConditionNode);
        else if (node is AddClothingNode)
            ProcessAddClothing(node as AddClothingNode);
        else if (node is OpenWardrobeNode)
            ProcessOpenWardrobe(node as OpenWardrobeNode);
        else if (node is WardrobeChoiceNode)
            ProcessWardrobeChoice(node as WardrobeChoiceNode);
        else if (node is WardrobeCheckNode)
            ProcessWardrobeCheck(node as WardrobeCheckNode);
        else if (node is StatChangeNode)
            ProcessStatChange(node as StatChangeNode);
        else if (node is AppearanceChoiceNode)
            ProcessAppearanceChoice(node as AppearanceChoiceNode);
        else if (node is ImageNode)
            ProcessImageNode(node as ImageNode);
        else if (node is PhoneDialogueNode)
            ProcessPhoneDialogue(node as PhoneDialogueNode);
        else if (node is EffectNode)
            ProcessEffect(node as EffectNode);
        else if (node is StoryBannerNode)
            ProcessStoryBanner(node as StoryBannerNode);
        else if (node is NameChoiceNode)
            ProcessNameChoice(node as NameChoiceNode);
        else if (node is PremiumNode)
            ProcessPremium(node as PremiumNode);
        else if (node is CameraNode)
            ProcessCameraNode(node as CameraNode);
    }

    static bool ShouldPersistProgressForNode(BaseStoryNode node)
    {
        return node is DialogueNode ||
               node is ChoiceNode ||
               node is WardrobeChoiceNode ||
               node is AppearanceChoiceNode ||
               node is NameChoiceNode;
    }

    IDictionary<string, object> BuildStoryNodeMetadata(BaseStoryNode node, bool trackHistory, bool syncProgress)
    {
        return LogMetadata.Of(
            "storyId", CurrentStoryId,
            "chapterId", CurrentChapterId,
            "episodeId", CurrentEpisodeId,
            "chapterIndex", CurrentChapterIndex,
            "dialogueLineIndex", CurrentDialogueLineIndex,
            "nodeGuid", node != null ? node.guid : "",
            "nodeName", node != null ? node.name : "",
            "nodeType", node != null ? node.GetType().Name : "",
            "trackHistory", trackHistory,
            "syncProgress", syncProgress,
            "persistable", ShouldPersistProgressForNode(node),
            "historyCount", GameState.Instance != null && GameState.Instance.history != null ? GameState.Instance.history.Count : 0);
    }

    public void SelectClothing(WardrobeChoiceNode node, int index)
    {
        if (node == null || node.availableClothes == null || index < 0 || index >= node.availableClothes.Count)
            return;

        var item = node.availableClothes[index];
        int cost = node.GetPremiumCost(index);
        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(StoryManager),
            nameof(SelectClothing),
            "[WARDROBE][SELECT] Wardrobe item selection requested by UI.",
            BuildWardrobeChoiceMetadata(node, index, item, cost));

        if (GameState.Instance == null)
            return;

        bool clearsSlot = node.TryGetClearSlotType(index, out _);
        if (!clearsSlot && !IsClothingAvailableForWardrobeNode(node, item))
        {
            AppLogger.Warn(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(SelectClothing),
                "[WARDROBE][SELECT_DENIED] Item is not available for the current story context.",
                BuildWardrobeChoiceMetadata(node, index, item, cost),
                recoverable: true);
            Debug.LogWarning($"[StoryManager] Ignoring wardrobe item '{(item != null ? item.id : "<null>")}' because it belongs to another character.", node);
            return;
        }

        string restrictionMessage;
        if (!node.CanSelectOption(index, GameState.Instance, out restrictionMessage))
        {
            AppLogger.Warn(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(SelectClothing),
                "[WARDROBE][SELECT_DENIED] Option rule blocked selection.",
                AddWardrobeMessageMetadata(BuildWardrobeChoiceMetadata(node, index, item, cost), restrictionMessage),
                recoverable: true);
            ShowWardrobeOptionDenied(restrictionMessage);
            return;
        }

        if (clearsSlot)
        {
            if (cost > 0)
            {
                AppLogger.Warn(
                    AppLogCategory.Wardrobe,
                    nameof(StoryManager),
                    nameof(SelectClothing),
                    "[WARDROBE][SELECT_DENIED] Clear-slot option cannot have a premium cost.",
                    BuildWardrobeChoiceMetadata(node, index, item, cost),
                    recoverable: true);
                ShowWardrobeOptionDenied("Некорректная настройка варианта гардероба");
                return;
            }

            DoSelectClothing(node, index, null);
            return;
        }

        if (cost > 0 && !IsWardrobeItemUnlocked(item))
        {
            if (_pendingWardrobeSelections.Contains(GetWardrobePurchaseKey(node, index, item)))
            {
                AppLogger.Info(
                    AppLogCategory.Wardrobe,
                    nameof(StoryManager),
                    nameof(SelectClothing),
                    "[WARDROBE][PURCHASE] Purchase already pending for this wardrobe item.",
                    BuildWardrobeChoiceMetadata(node, index, item, cost));
                return;
            }

            TryPurchaseWardrobeChoice(node, index, item, cost);
            return;
        }

        DoSelectClothing(node, index, item);
    }

    void TryPurchaseWardrobeChoice(WardrobeChoiceNode node, int index, ClothingItem item, int cost)
    {
        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(StoryManager),
            nameof(TryPurchaseWardrobeChoice),
            "[WARDROBE][PURCHASE] Trying to purchase paid wardrobe choice.",
            BuildWardrobeChoiceMetadata(node, index, item, cost));

        if (!IsValidPremiumCost(cost))
        {
            AppLogger.Warn(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(TryPurchaseWardrobeChoice),
                "[WARDROBE][PURCHASE_DENIED] Invalid premium cost.",
                BuildWardrobeChoiceMetadata(node, index, item, cost),
                recoverable: true);
            Debug.LogWarning("[StoryManager] Refused wardrobe choice with invalid cost: " + cost);
            ShowWardrobePurchaseFailure(false);
            return;
        }

        if (item == null || string.IsNullOrEmpty(item.id))
        {
            AppLogger.Warn(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(TryPurchaseWardrobeChoice),
                "[WARDROBE][PURCHASE_DENIED] Paid wardrobe choice has no valid item id.",
                BuildWardrobeChoiceMetadata(node, index, item, cost),
                recoverable: true);
            Debug.LogWarning("[StoryManager] Refused paid wardrobe choice without a valid clothing item.", node);
            ShowWardrobePurchaseFailure(false);
            return;
        }

        if (IsWardrobeItemUnlocked(item))
        {
            DoSelectClothing(node, index, item);
            return;
        }

        if (PlayerData.Hearts < cost)
        {
            AppLogger.Info(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(TryPurchaseWardrobeChoice),
                "[WARDROBE][PURCHASE_DENIED] Player does not have enough hearts.",
                BuildWardrobeChoiceMetadata(node, index, item, cost));
            ShowWardrobePurchaseFailure(true);
            return;
        }

        if (!NetworkManager.IsAuthenticated)
        {
            if (!PrototypeFeatureFlags.LocalPremiumSpendEnabled)
            {
                AppLogger.Warn(
                    AppLogCategory.Wardrobe,
                    nameof(StoryManager),
                    nameof(TryPurchaseWardrobeChoice),
                    "[WARDROBE][PURCHASE_DENIED] Local premium spend is disabled and player is not authenticated.",
                    BuildWardrobeChoiceMetadata(node, index, item, cost),
                    recoverable: true);
                Debug.LogWarning("[StoryManager] Local wardrobe spend fallback is disabled.");
                ShowWardrobePurchaseFailure(false);
                return;
            }
        }

        string pendingKey = GetWardrobePurchaseKey(node, index, item);
        if (!_pendingWardrobeSelections.Add(pendingKey))
            return;

        StartCoroutine(SpendWardrobeChoiceAndSelect(node, index, item, cost, pendingKey));
    }

    IEnumerator SpendWardrobeChoiceAndSelect(WardrobeChoiceNode node, int index, ClothingItem item, int cost, string pendingKey)
    {
        bool ok = false;

        if (IsWardrobeItemUnlocked(item))
        {
            _pendingWardrobeSelections.Remove(pendingKey);
            DoSelectClothing(node, index, item);
            yield break;
        }

        if (NetworkManager.Instance != null && NetworkManager.IsAuthenticated)
        {
            yield return NetworkManager.Instance.PurchaseWardrobeItem(cost, item.id, result => ok = result);
        }
        else if (PrototypeFeatureFlags.LocalPremiumSpendEnabled && PlayerData.Hearts >= cost)
        {
            PlayerData.AddHeartValue(-cost);
            ok = true;
        }

        _pendingWardrobeSelections.Remove(pendingKey);

        if (ok)
        {
            AppLogger.Info(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(SpendWardrobeChoiceAndSelect),
                "[WARDROBE][PURCHASE_SUCCESS] Paid wardrobe choice unlocked.",
                BuildWardrobeChoiceMetadata(node, index, item, cost));
            DoSelectClothing(node, index, item);
        }
        else
        {
            AppLogger.Warn(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(SpendWardrobeChoiceAndSelect),
                "[WARDROBE][PURCHASE_FAILED] Paid wardrobe choice could not be unlocked.",
                BuildWardrobeChoiceMetadata(node, index, item, cost),
                recoverable: true);
            ShowWardrobePurchaseFailure(false);
        }
    }

    void DoSelectClothing(WardrobeChoiceNode node, int index, ClothingItem item)
    {
        if (GameState.Instance == null)
            return;

        if (node != null && node.TryGetClearSlotType(index, out ClothingType clearSlotType))
        {
            string equipKey = GetWardrobeEquipKey(node, clearSlotType);
            GameState.Instance.UnequipClothing(equipKey);
            PlayerAppearance.SetEquippedClothing(clearSlotType, "", null, null);
            AppLogger.Info(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(DoSelectClothing),
                "[WARDROBE][UNEQUIP] Wardrobe slot cleared by story choice.",
                BuildWardrobeChoiceMetadata(node, index, null, 0));
        }
        else if (item != null && !string.IsNullOrEmpty(item.id))
        {
            GameState.Instance.AddClothing(item.id);
            GameState.Instance.EquipClothing(GetWardrobeEquipKey(node, item), item.id);
            PlayerAppearance.SetEquippedClothing(item.type, item.id, item.sprite, item);
            AppLogger.Info(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(DoSelectClothing),
                "[WARDROBE][EQUIP] Wardrobe item equipped and added to GameState.",
                BuildWardrobeChoiceMetadata(node, index, item, node != null ? node.GetPremiumCost(index) : 0));
        }
        else
        {
            AppLogger.Error(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(DoSelectClothing),
                "[WARDROBE][EQUIP_FAILED] Wardrobe choice has missing clothing asset.",
                metadata: BuildWardrobeChoiceMetadata(node, index, item, node != null ? node.GetPremiumCost(index) : 0),
                recoverable: true);
            Debug.LogError($"[StoryManager] WardrobeChoiceNode '{node.guid}' has missing clothing asset at index {index}. Continuing without equipping it.", node);
        }

        if (TryGetWardrobeSelectionNextNode(node, index, out var nextNode, out _) &&
            TryContinueToChainedWardrobeChoice(nextNode))
        {
            AppLogger.Info(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(DoSelectClothing),
                "[WARDROBE][CHAIN] Continuing directly into chained wardrobe choice.",
                AddNextNodeMetadata(BuildWardrobeChoiceMetadata(node, index, item, node != null ? node.GetPremiumCost(index) : 0), nextNode));
            return;
        }

        if (EnsureDialogueUI("closing wardrobe choice"))
            dialogueUI.CloseWardrobe();

        ContinueOnStoryScreen(() => ContinueAfterClothingSelection(node, index));
    }

    bool IsWardrobeItemUnlocked(ClothingItem item)
    {
        return item != null &&
            !string.IsNullOrEmpty(item.id) &&
            GameState.Instance != null &&
            GameState.Instance.HasClothing(item.id);
    }

    string GetWardrobePurchaseKey(WardrobeChoiceNode node, int index, ClothingItem item)
    {
        string nodeGuid = node != null ? SaveDataSanitizer.SanitizeIdentifier(node.guid) : "";
        string itemId = item != null ? SaveDataSanitizer.SanitizeIdentifier(item.id) : "";
        if (!string.IsNullOrEmpty(itemId))
            return "wardrobe-item:" + itemId;

        if (!string.IsNullOrEmpty(nodeGuid) || !string.IsNullOrEmpty(itemId))
            return nodeGuid + ":" + Mathf.Max(0, index) + ":" + itemId;

        return node != null ? "node:" + node.GetInstanceID() : "wardrobe";
    }

    void ShowWardrobePurchaseFailure(bool notEnoughHearts)
    {
        string message = notEnoughHearts
            ? "\u041d\u0435\u0434\u043e\u0441\u0442\u0430\u0442\u043e\u0447\u043d\u043e \u0438\u0441\u043a\u0440"
            : "\u041f\u043e\u043a\u0443\u043f\u043a\u0430 \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u043d\u0430";

        ShowWardrobeOptionDenied(message);
    }

    void ShowWardrobeOptionDenied(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            message = "\u0412\u0430\u0440\u0438\u0430\u043d\u0442 \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u0435\u043d";

        if (ToastManager.Instance != null)
            ToastManager.Instance.ShowSystemMessage(message);
        else if (dialogueUI != null)
            dialogueUI.ShowWardrobeSystemMessage(message);
        else
            Debug.LogWarning("[StoryManager] " + message);
    }

    void ContinueAfterClothingSelection(WardrobeChoiceNode node, int index)
    {
        if (TryGetWardrobeSelectionNextNode(node, index, out var nextNode, out bool usedSpecificExit))
        {
            AppLogger.Info(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(ContinueAfterClothingSelection),
                "[WARDROBE][CONTINUE] Continuing after wardrobe selection through configured next node.",
                AddNextNodeMetadata(BuildWardrobeChoiceMetadata(node, index, node != null && node.availableClothes != null && index >= 0 && index < node.availableClothes.Count ? node.availableClothes[index] : null, node != null ? node.GetPremiumCost(index) : 0), nextNode));
            ProcessNode(nextNode);
            return;
        }

        if (!usedSpecificExit)
            Debug.LogWarning($"[StoryManager] WardrobeChoiceNode '{node.guid}' has no exit for option {index}. Trying default exit.", node);

        GoNext(node, "exit");
    }

    bool TryGetWardrobeSelectionNextNode(WardrobeChoiceNode node, int index, out BaseStoryNode nextNode, out bool usedSpecificExit)
    {
        nextNode = null;
        usedSpecificExit = false;

        if (node == null)
            return false;

        if (node.exits != null && index >= 0 && index < node.exits.Count && node.exits[index] != null)
        {
            nextNode = node.exits[index];
            usedSpecificExit = true;
            return true;
        }

        var port = node.GetOutputPort("exit");
        if (port == null || port.Connection == null)
            return false;

        nextNode = port.Connection.node as BaseStoryNode;
        return nextNode != null;
    }

    bool TryContinueToChainedWardrobeChoice(BaseStoryNode nextNode)
    {
        if (!TryResolveChainedWardrobeChoice(nextNode, out var nextWardrobe, out var bridgeNodes))
            return false;

        for (int i = 0; i < bridgeNodes.Count; i++)
        {
            if (!ApplyWardrobeBridgeNode(bridgeNodes[i]))
                return false;
        }

        return OpenChainedWardrobeChoice(nextWardrobe);
    }

    bool TryResolveChainedWardrobeChoice(
        BaseStoryNode nextNode,
        out WardrobeChoiceNode nextWardrobe,
        out List<BaseStoryNode> bridgeNodes)
    {
        nextWardrobe = null;
        bridgeNodes = new List<BaseStoryNode>();

        BaseStoryNode node = nextNode;
        var visited = new HashSet<BaseStoryNode>();
        var previewState = new WardrobeBridgePreviewState();

        for (int guard = 0; node != null && guard < 32; guard++)
        {
            if (!visited.Add(node))
                return false;

            nextWardrobe = node as WardrobeChoiceNode;
            if (CanOpenWardrobeChoiceInline(nextWardrobe))
                return true;

            if (!IsWardrobeBridgeNode(node) || !TryPreviewWardrobeBridgeNextNode(node, previewState, out var bridgedNext))
                return false;

            bridgeNodes.Add(node);
            node = bridgedNext;
        }

        return false;
    }

    bool OpenChainedWardrobeChoice(WardrobeChoiceNode nextWardrobe)
    {
        if (!EnsureDialogueUI("showing chained wardrobe choice"))
            return false;

        BeginHeroSetupStoryUiSuppression();
        ClearCutsceneRuntimeState();

        if (GameState.Instance != null)
            GameState.Instance.currentNode = nextWardrobe;

        var history = storyHistory ?? StoryHistory.Instance;
        history?.Push(nextWardrobe);
        PersistProgress(nextWardrobe);

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(StoryManager),
            nameof(OpenChainedWardrobeChoice),
            "[WARDROBE][CHAIN] Opening chained wardrobe choice without returning to the history screen.",
            BuildWardrobeChoiceMetadata(nextWardrobe, -1, null, 0));

        ProcessWardrobeChoice(nextWardrobe, false);
        return true;
    }

    static bool IsWardrobeBridgeNode(BaseStoryNode node)
    {
        return node is VariableChangeNode ||
               node is ConditionNode ||
               node is WardrobeCheckNode ||
               node is StatChangeNode ||
               node is AddClothingNode;
    }

    bool TryPreviewWardrobeBridgeNextNode(
        BaseStoryNode node,
        WardrobeBridgePreviewState previewState,
        out BaseStoryNode nextNode)
    {
        nextNode = null;
        if (previewState == null)
            return false;

        if (node is VariableChangeNode variableNode)
        {
            if (!EnsureGameState("previewing a wardrobe bridge variable change"))
                return false;

            int previousValue = previewState.GetInt(variableNode.variableKey);
            int newValue = variableNode.Add
                ? SaveDataSanitizer.ClampStatDelta(previousValue, variableNode.deltaValue)
                : SaveDataSanitizer.ClampStatValue(variableNode.deltaValue);
            previewState.SetInt(variableNode.variableKey, newValue);
            return TryGetConnectedStoryNode(variableNode, "exit", out nextNode);
        }

        if (node is ConditionNode conditionNode)
        {
            if (!EnsureGameState("checking a wardrobe bridge condition"))
                return false;

            int leftValue = previewState.GetInt(conditionNode.variableKey);
            int rightValue = string.IsNullOrWhiteSpace(conditionNode.compareVariableKey)
                ? conditionNode.requiredValue
                : previewState.GetInt(conditionNode.compareVariableKey);
            string portName = EvaluateCondition(leftValue, rightValue, conditionNode.comparison)
                ? "trueExit"
                : "falseExit";

            return TryGetConnectedStoryNode(conditionNode, portName, out nextNode);
        }

        if (node is WardrobeCheckNode wardrobeCheckNode)
        {
            if (!EnsureGameState("checking a wardrobe bridge item"))
                return false;

            bool hasItem = !string.IsNullOrEmpty(wardrobeCheckNode.itemId) &&
                           previewState.HasClothing(wardrobeCheckNode.itemId);
            return TryGetConnectedStoryNode(wardrobeCheckNode, hasItem ? "hasItem" : "noItem", out nextNode);
        }

        if (node is StatChangeNode statNode)
            return TryGetConnectedStoryNode(statNode, "exit", out nextNode);

        if (node is AddClothingNode addClothingNode)
        {
            previewState.AddClothing(addClothingNode.clothing);
            return TryGetConnectedStoryNode(addClothingNode, "exit", out nextNode);
        }

        return false;
    }

    sealed class WardrobeBridgePreviewState
    {
        readonly Dictionary<string, int> _intValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> _ownedClothing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public int GetInt(string key)
        {
            key = SaveDataSanitizer.SanitizeIdentifier(key);
            if (string.IsNullOrEmpty(key))
                return 0;

            return _intValues.TryGetValue(key, out var value)
                ? value
                : GameState.Instance.GetInt(key);
        }

        public void SetInt(string key, int value)
        {
            key = SaveDataSanitizer.SanitizeIdentifier(key);
            if (!string.IsNullOrEmpty(key))
                _intValues[key] = value;
        }

        public bool HasClothing(string itemId)
        {
            itemId = SaveDataSanitizer.SanitizeIdentifier(itemId);
            if (string.IsNullOrEmpty(itemId))
                return false;

            return _ownedClothing.Contains(itemId) || GameState.Instance.HasClothing(itemId);
        }

        public void AddClothing(ClothingItem clothing)
        {
            string itemId = clothing != null ? SaveDataSanitizer.SanitizeIdentifier(clothing.id) : "";
            if (!string.IsNullOrEmpty(itemId))
                _ownedClothing.Add(itemId);
        }
    }

    bool ApplyWardrobeBridgeNode(BaseStoryNode node)
    {
        if (node is VariableChangeNode variableNode)
            return ApplyWardrobeBridgeVariable(variableNode);

        if (node is StatChangeNode statNode)
            return ApplyWardrobeBridgeStat(statNode);

        if (node is AddClothingNode addClothingNode)
        {
            if (GameState.Instance == null)
                return false;

            if (addClothingNode.clothing != null)
                GameState.Instance.AddClothing(addClothingNode.clothing.id);

            return true;
        }

        return node is ConditionNode || node is WardrobeCheckNode;
    }

    bool ApplyWardrobeBridgeVariable(VariableChangeNode node)
    {
        if (node == null || !EnsureGameState("applying a wardrobe bridge variable change"))
            return false;

        int previousValue = GameState.Instance.GetInt(node.variableKey);
        int newValue = node.Add
            ? SaveDataSanitizer.ClampStatDelta(previousValue, node.deltaValue)
            : SaveDataSanitizer.ClampStatValue(node.deltaValue);
        GameState.Instance.SetInt(node.variableKey, newValue);

        int appliedDelta = newValue - previousValue;
        RecordEpisodeStatDelta(node.variableKey, appliedDelta);
        ReportRelationshipStatChange(node.variableKey, appliedDelta);
        string displayName = ResolveStatDisplayName(node.variableKey, "");
        if (appliedDelta != 0 && !string.IsNullOrWhiteSpace(displayName))
            ShowStatChangeFeedback(node.variableKey, displayName, appliedDelta, "");

        return true;
    }

    bool ApplyWardrobeBridgeStat(StatChangeNode node)
    {
        if (node == null || !EnsureGameState("applying a wardrobe bridge stat change"))
            return false;

        int previousValue = GameState.Instance.GetStat(node.statId);
        GameState.Instance.AddStat(node.statId, node.delta);
        int appliedDelta = GameState.Instance.GetStat(node.statId) - previousValue;
        RecordEpisodeStatDelta(node.statId, appliedDelta);
        ReportRelationshipStatChange(node.statId, appliedDelta);
        ShowStatChangeFeedback(node.statId, node.displayName, appliedDelta, node.systemMessage);
        return true;
    }

    bool TryGetConnectedStoryNode(BaseStoryNode node, string portName, out BaseStoryNode nextNode)
    {
        nextNode = null;

        if (node == null || string.IsNullOrEmpty(portName))
            return false;

        var port = node.GetOutputPort(portName);
        if (port == null || port.Connection == null)
            return false;

        nextNode = port.Connection.node as BaseStoryNode;
        return nextNode != null;
    }

    static bool CanOpenWardrobeChoiceInline(WardrobeChoiceNode node)
    {
        if (node == null || node.availableClothes == null)
            return false;

        for (int i = 0; i < node.availableClothes.Count; i++)
        {
            if (!node.IsOptionVisible(i))
                continue;

            if (node.TryGetClearSlotType(i, out _))
                return true;

            if (node.availableClothes[i] != null &&
                IsClothingAvailableForWardrobeNode(node, node.availableClothes[i]))
                return true;
        }

        return false;
    }

    string GetWardrobeEquipKey(WardrobeChoiceNode node, ClothingItem item)
    {
        return GetWardrobeEquipKey(node, item != null ? item.type : ClothingType.Outfit);
    }

    string GetWardrobeEquipKey(WardrobeChoiceNode node, ClothingType type)
    {
        string characterId = "";
        if (node != null)
        {
            characterId = !string.IsNullOrWhiteSpace(node.characterId)
                ? node.characterId
                : node.character != null ? node.character.name : "";
        }

        if (string.IsNullOrWhiteSpace(characterId))
            characterId = "hero";

        if (type == ClothingType.Hair)
            return characterId + ":hair";

        if (type == ClothingType.Accessory)
            return characterId + ":accessory";

        return characterId + ":outfit";
    }

    static bool IsClothingAvailableForWardrobeNode(WardrobeChoiceNode node, ClothingItem item)
    {
        if (item == null)
            return false;

        string characterId = "";
        if (node != null)
        {
            characterId = !string.IsNullOrWhiteSpace(node.characterId)
                ? node.characterId
                : node.character != null ? node.character.name : "";
        }

        if (string.IsNullOrWhiteSpace(characterId))
            characterId = "hero";

        StoryManager manager = Instance;
        string storyId = manager != null ? manager.CurrentStoryId : "";
        string chapterId = "";
        if (manager != null)
            chapterId = !string.IsNullOrWhiteSpace(manager.CurrentChapterId) ? manager.CurrentChapterId : manager.CurrentEpisodeId;

        return item.IsAvailableForWardrobe(characterId, storyId, chapterId);
    }

    void ProcessWardrobeChoice(WardrobeChoiceNode node)
    {
        ProcessWardrobeChoice(node, true);
    }

    void ProcessWardrobeChoice(WardrobeChoiceNode node, bool openWardrobeScreen)
    {
        if (node == null)
            return;

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(StoryManager),
            nameof(ProcessWardrobeChoice),
            "[WARDROBE][NODE] Processing wardrobe choice node.",
            LogMetadata.Of(
                "storyId", CurrentStoryId,
                "chapterId", CurrentChapterId,
                "episodeId", CurrentEpisodeId,
                "nodeGuid", node.guid,
                "nodeName", node.name,
                "openWardrobeScreen", openWardrobeScreen,
                "itemCount", node.availableClothes != null ? node.availableClothes.Count : 0,
                "visibleCount", CountVisibleWardrobeOptions(node),
                "paidCount", CountPaidWardrobeOptions(node)));

        if (node.availableClothes == null || node.availableClothes.Count == 0)
        {
            AppLogger.Warn(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(ProcessWardrobeChoice),
                "[WARDROBE][NODE_EMPTY] WardrobeChoiceNode has no available clothes.",
                BuildWardrobeChoiceMetadata(node, -1, null, 0),
                recoverable: true);
            Debug.LogWarning("[StoryManager] WardrobeChoiceNode has no available clothes.", node);
            GoNext(node, "exit");
            return;
        }

        if (!EnsureDialogueUI("showing wardrobe choice"))
            return;

        // Story flow must never wait for a network ownership refresh before opening the wardrobe.
        // The authenticated session already performs wardrobe sync in NetworkManager.SyncAll(); this
        // extra refresh is only a best-effort background update. Waiting here used to leave the
        // player on an empty dialogue frame for up to ~48 seconds (15 s timeout * 3 attempts +
        // retry delays) whenever the wardrobe endpoint was slow or unavailable.
        StartWardrobeOwnershipSyncForStoryNonBlocking("wardrobe_choice", node);

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(StoryManager),
            nameof(ProcessWardrobeChoice),
            "[WARDROBE][TRANSITION] Opening wardrobe choice immediately from local state.",
            LogMetadata.Of(
                "storyId", CurrentStoryId,
                "chapterId", CurrentChapterId,
                "episodeId", CurrentEpisodeId,
                "nodeGuid", node.guid,
                "nodeName", node.name,
                "openWardrobeScreen", openWardrobeScreen,
                "networkAuthenticated", NetworkManager.IsAuthenticated));

        if (!dialogueUI.OpenWardrobeChoice(node, ResolveWardrobeContextData()))
        {
            int fallbackIndex = FindFirstVisibleWardrobeOption(node);
            if (fallbackIndex >= 0)
            {
                AppLogger.Warn(
                    AppLogCategory.Wardrobe,
                    nameof(StoryManager),
                    nameof(ProcessWardrobeChoice),
                    "[WARDROBE][UI_FAILED] Wardrobe UI failed, auto-selecting first visible option.",
                    BuildWardrobeChoiceMetadata(node, fallbackIndex, node.availableClothes[fallbackIndex], node.GetPremiumCost(fallbackIndex)),
                    recoverable: true);
                Debug.LogError("[StoryManager] Wardrobe choice UI failed. Auto-selecting the first visible clothing option to avoid blocking the story.", node);
                SelectClothing(node, fallbackIndex);
            }
            else
            {
                AppLogger.Warn(
                    AppLogCategory.Wardrobe,
                    nameof(StoryManager),
                    nameof(ProcessWardrobeChoice),
                    "[WARDROBE][UI_FAILED] Wardrobe UI failed and no visible options were found.",
                    BuildWardrobeChoiceMetadata(node, -1, null, 0),
                    recoverable: true);
                Debug.LogWarning("[StoryManager] Wardrobe choice has no visible options. Continuing through default exit.", node);
                GoNext(node, "exit");
            }
        }
    }

    int FindFirstVisibleWardrobeOption(WardrobeChoiceNode node)
    {
        if (node == null || node.availableClothes == null)
            return -1;

        for (int i = 0; i < node.availableClothes.Count; i++)
        {
            if (!node.IsOptionVisible(i))
                continue;

            if (node.TryGetClearSlotType(i, out _))
                return i;

            if (node.availableClothes[i] != null &&
                IsClothingAvailableForWardrobeNode(node, node.availableClothes[i]))
                return i;
        }

        return -1;
    }

    void ProcessNameChoice(NameChoiceNode node)
    {
        if (node == null)
            return;

        if (dialogueUI != null)
        {
            dialogueUI.ClearChoices();
            dialogueUI.ClearDialogue();
        }

        BeginHeroSetupStoryUiSuppression();

        Action continueStory = () => GoNext(node, "exit");
        PreStorySetupFlow setupFlow = menuController != null
            ? menuController.PreStorySetupFlow
            : FindObjectOfType<PreStorySetupFlow>(true);

        if (setupFlow != null)
        {
            setupFlow.ShowNameOnly(continueStory, continueStory, false, node.defaultName, node.defaultName);
            return;
        }

        PlayerNameInputUI nameInput = PlayerNameInputUI.Instance ?? FindObjectOfType<PlayerNameInputUI>(true);
        if (nameInput != null)
        {
            nameInput.Show(continueStory, node.forceShow, node.defaultName);
            return;
        }

        string fallbackName = ResolvePersistablePlayerNameForSave(node.defaultName);
        if (!string.IsNullOrWhiteSpace(fallbackName))
        {
            CharacterProfileService.SaveSelectedPlayerName(
                fallbackName,
                CurrentStoryId,
                nameof(ProcessNameChoice));
        }

        GoNext(node, "exit");
    }

    void ProcessStoryBanner(StoryBannerNode node)
    {
        if (node == null)
            return;

        if (IsAutoSkippingStoryContent())
        {
            GoNext(node, "exit");
            return;
        }

        StartCoroutine(ProcessStoryBannerRoutine(node));
    }

    IEnumerator ProcessStoryBannerRoutine(StoryBannerNode node)
    {
        if (dialogueUI != null)
        {
            dialogueUI.ClearChoices();
            dialogueUI.ClearDialogue();
        }

        if (_chapterTitleOverlay == null)
            _chapterTitleOverlay = FindObjectOfType<ChapterTitleOverlay>(true);

        string message = StoryJsonConverter.SanitizeDisplayText(ReplaceStoryPlaceholders(node.message ?? ""));
        if (StoryJsonConverter.IsSystemInstructionText(message))
            message = "";
        if (string.IsNullOrWhiteSpace(message))
        {
            GoNext(node, "exit");
            yield break;
        }

        if (_chapterTitleOverlay != null)
        {
            Coroutine overlayRoutine = _chapterTitleOverlay.ShowText(message);
            if (node.waitForCompletion && overlayRoutine != null)
            {
                yield return overlayRoutine;
            }
            else if (node.fallbackDuration > 0f)
            {
                yield return new WaitForSeconds(node.fallbackDuration);
            }
        }
        else if (dialogueUI != null)
        {
            dialogueUI.ShowSystemMessage(message);
            if (node.fallbackDuration > 0f)
                yield return new WaitForSeconds(node.fallbackDuration);
        }

        GoNext(node, "exit");
    }

    void ProcessWardrobeCheck(WardrobeCheckNode node)
    {
        if (node == null || !EnsureGameState("checking wardrobe item"))
            return;

        // A story branch must not freeze while waiting for the wardrobe API. Use the already
        // synchronized/local ownership cache and refresh it opportunistically in the background.
        StartWardrobeOwnershipSyncForStoryNonBlocking("wardrobe_check", node);

        bool hasItem = !string.IsNullOrEmpty(node.itemId) && GameState.Instance.HasClothing(node.itemId);
        GoNext(node, hasItem ? "hasItem" : "noItem");
    }

    const float WardrobeOwnershipStorySyncCooldownSeconds = 30f;
    bool wardrobeOwnershipStorySyncInFlight;
    float wardrobeOwnershipStorySyncLastStartedAt = -1000f;

    void StartWardrobeOwnershipSyncForStoryNonBlocking(string reason, BaseStoryNode node)
    {
        if (NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
            return;

        if (wardrobeOwnershipStorySyncInFlight)
        {
            AppLogger.DebugLog(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(StartWardrobeOwnershipSyncForStoryNonBlocking),
                "[WARDROBE][SYNC_ASYNC] Reusing wardrobe ownership sync already in flight.",
                BuildWardrobeOwnershipSyncMetadata(reason, node));
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (now - wardrobeOwnershipStorySyncLastStartedAt < WardrobeOwnershipStorySyncCooldownSeconds)
        {
            AppLogger.DebugLog(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(StartWardrobeOwnershipSyncForStoryNonBlocking),
                "[WARDROBE][SYNC_ASYNC] Recent wardrobe ownership sync is fresh enough; skipping duplicate request.",
                BuildWardrobeOwnershipSyncMetadata(reason, node));
            return;
        }

        wardrobeOwnershipStorySyncInFlight = true;
        wardrobeOwnershipStorySyncLastStartedAt = now;
        StartCoroutine(SyncWardrobeOwnershipForStoryBackground(reason, node));
    }

    IEnumerator SyncWardrobeOwnershipForStoryBackground(string reason, BaseStoryNode node)
    {
        bool synced = false;
        string safeReason = SaveDataSanitizer.SanitizeIdentifier(reason);

        AppLogger.DebugLog(
            AppLogCategory.Wardrobe,
            nameof(StoryManager),
            nameof(SyncWardrobeOwnershipForStoryBackground),
            "[WARDROBE][SYNC_ASYNC] Background wardrobe ownership sync started without blocking story UI.",
            BuildWardrobeOwnershipSyncMetadata(safeReason, node));

        yield return NetworkManager.Instance.SyncWardrobeOwnership(result => synced = result);
        wardrobeOwnershipStorySyncInFlight = false;

        if (synced)
        {
            AppLogger.DebugLog(
                AppLogCategory.Wardrobe,
                nameof(StoryManager),
                nameof(SyncWardrobeOwnershipForStoryBackground),
                "[WARDROBE][SYNC_ASYNC] Background wardrobe ownership sync completed.",
                BuildWardrobeOwnershipSyncMetadata(safeReason, node));
            yield break;
        }

        AppLogger.Warn(
            AppLogCategory.Wardrobe,
            nameof(StoryManager),
            nameof(SyncWardrobeOwnershipForStoryBackground),
            "[WARDROBE][SYNC_ASYNC] Server wardrobe ownership sync failed; story continued with local cache.",
            BuildWardrobeOwnershipSyncMetadata(safeReason, node),
            recoverable: true);
    }

    IDictionary<string, object> BuildWardrobeOwnershipSyncMetadata(string reason, BaseStoryNode node)
    {
        return LogMetadata.Of(
            "reason", SaveDataSanitizer.SanitizeIdentifier(reason),
            "storyId", CurrentStoryId,
            "chapterId", CurrentChapterId,
            "episodeId", CurrentEpisodeId,
            "nodeGuid", node != null ? node.guid : "",
            "nodeName", node != null ? node.name : "",
            "inFlight", wardrobeOwnershipStorySyncInFlight,
            "cooldownSeconds", WardrobeOwnershipStorySyncCooldownSeconds);
    }

    void ProcessOpenWardrobe(OpenWardrobeNode node)
    {
        BeginHeroSetupStoryUiSuppression();

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(StoryManager),
            nameof(ProcessOpenWardrobe),
            "[WARDROBE][OPEN_NODE] Story requested full wardrobe setup.",
            LogMetadata.Of(
                "storyId", CurrentStoryId,
                "chapterId", CurrentChapterId,
                "episodeId", CurrentEpisodeId,
                "nodeGuid", node != null ? node.guid : "",
                "nodeName", node != null ? node.name : ""));

        StartCoroutine(ProcessOpenWardrobeRoutine(node));
    }

    IEnumerator ProcessOpenWardrobeRoutine(OpenWardrobeNode node)
    {
        bool returnedFromWardrobe = false;
        Action returnToStory = () =>
        {
            if (returnedFromWardrobe)
                return;

            returnedFromWardrobe = true;
            ReturnFromOpenWardrobe(node);
        };

        // Open the wardrobe immediately. Server ownership refresh is best-effort and must never
        // hold the story screen hostage on a slow mobile connection.
        StartWardrobeOwnershipSyncForStoryNonBlocking("open_wardrobe", node);

        if (EnsureDialogueUI("opening wardrobe setup") &&
            dialogueUI.OpenHeroWardrobeSetup(
                returnToStory,
                false,
                returnToStory,
                ResolveWardrobeContextData(),
                saveProgressOnComplete: false))
        {
            yield break;
        }

        ReturnFromOpenWardrobe(node);
    }

    void ReturnFromOpenWardrobe(OpenWardrobeNode node)
    {
        if (TryGetConnectedStoryNode(node, "exit", out BaseStoryNode nextNode) &&
            TryContinueToChainedWardrobeChoice(nextNode))
        {
            return;
        }

        if (dialogueUI != null)
            dialogueUI.CloseWardrobe();

        RestoreHeroSetupStoryUi();
        PersistProgress(node);

        AppLogger.Info(
            AppLogCategory.Wardrobe,
            nameof(StoryManager),
            nameof(ReturnFromOpenWardrobe),
            "[WARDROBE][RETURN] Returning from full wardrobe setup to the story flow.",
            AddNextNodeMetadata(
                LogMetadata.Of(
                    "storyId", CurrentStoryId,
                    "chapterId", CurrentChapterId,
                    "episodeId", CurrentEpisodeId,
                    "nodeGuid", node != null ? node.guid : "",
                    "nodeName", node != null ? node.name : ""),
                TryGetConnectedStoryNode(node, "exit", out BaseStoryNode exitNode) ? exitNode : null));

        ContinueOnStoryScreen(() => GoNext(node, "exit"));
    }

    void ContinueOnStoryScreen(Action continueStory)
    {
        AppLogger.Info(
            AppLogCategory.ScreenNavigation,
            nameof(StoryManager),
            nameof(ContinueOnStoryScreen),
            "[STORY][SCREEN] Ensuring story screen is visible before continuing story logic.",
            LogMetadata.Of(
                "storyId", CurrentStoryId,
                "chapterId", CurrentChapterId,
                "episodeId", CurrentEpisodeId,
                "currentNodeGuid", GameState.Instance != null && GameState.Instance.currentNode != null ? GameState.Instance.currentNode.guid : "",
                "hasMenuController", menuController != null));

        if (menuController != null)
            menuController.OpenStoryScreen(null);

        continueStory?.Invoke();
    }

    IDictionary<string, object> BuildWardrobeChoiceMetadata(WardrobeChoiceNode node, int index, ClothingItem item, int cost)
    {
        ClothingType clearSlotType = ClothingType.Outfit;
        bool clearsSlot = node != null && node.TryGetClearSlotType(index, out clearSlotType);
        string equipKey = clearsSlot
            ? GetWardrobeEquipKey(node, clearSlotType)
            : item != null ? GetWardrobeEquipKey(node, item) : "";
        string itemType = clearsSlot
            ? clearSlotType.ToString()
            : item != null ? item.type.ToString() : "";

        return LogMetadata.Of(
            "storyId", CurrentStoryId,
            "chapterId", CurrentChapterId,
            "episodeId", CurrentEpisodeId,
            "nodeGuid", node != null ? node.guid : "",
            "nodeName", node != null ? node.name : "",
            "index", index,
            "itemId", item != null ? item.id : "",
            "purchaseKey", node != null && index >= 0 ? node.GetServerPurchaseKey(index) : "",
            "itemName", item != null ? item.name : "",
            "itemType", itemType,
            "clearsSlot", clearsSlot,
            "equipKey", equipKey,
            "cost", SaveDataSanitizer.ClampCurrencyValue(cost),
            "owned", IsWardrobeItemUnlocked(item),
            "hearts", PlayerData.Hearts,
            "candles", PlayerData.Candles,
            "availableForContext", clearsSlot || IsClothingAvailableForWardrobeNode(node, item),
            "currentNodeGuid", GameState.Instance != null && GameState.Instance.currentNode != null ? GameState.Instance.currentNode.guid : "");
    }

    static IDictionary<string, object> AddWardrobeMessageMetadata(IDictionary<string, object> metadata, string message)
    {
        if (metadata == null)
            metadata = LogMetadata.Of();

        metadata["message"] = message ?? "";
        return metadata;
    }

    static IDictionary<string, object> AddNextNodeMetadata(IDictionary<string, object> metadata, BaseStoryNode nextNode)
    {
        if (metadata == null)
            metadata = LogMetadata.Of();

        metadata["nextNodeGuid"] = nextNode != null ? nextNode.guid : "";
        metadata["nextNodeName"] = nextNode != null ? nextNode.name : "";
        metadata["nextNodeType"] = nextNode != null ? nextNode.GetType().Name : "";
        return metadata;
    }

    static int CountVisibleWardrobeOptions(WardrobeChoiceNode node)
    {
        if (node == null || node.availableClothes == null)
            return 0;

        int count = 0;
        for (int i = 0; i < node.availableClothes.Count; i++)
        {
            if (!node.IsOptionVisible(i))
                continue;

            if (node.TryGetClearSlotType(i, out _) || node.availableClothes[i] != null)
                count++;
        }

        return count;
    }

    static int CountPaidWardrobeOptions(WardrobeChoiceNode node)
    {
        if (node == null || node.availableClothes == null)
            return 0;

        int count = 0;
        for (int i = 0; i < node.availableClothes.Count; i++)
        {
            if (node.availableClothes[i] != null && node.GetPremiumCost(i) > 0)
                count++;
        }

        return count;
    }

    GameData ResolveWardrobeContextData()
    {
        if (menuController == null)
            menuController = FindObjectOfType<MenuController>(true);

        return menuController != null ? menuController.CurrentStoryContextData : null;
    }

    void ProcessAddClothing(AddClothingNode node)
    {
        if (node.clothing != null)
        {
            GameState.Instance.AddClothing(node.clothing.id);
        }

        GoNext(node, "exit");
    }

    void ProcessScene(SceneSetupNode node, bool needExit = true)
    {
        if (node == null)
            return;

        var data = node.sceneData;
        if (data == null)
        {
            Debug.LogWarning("[StoryManager] SceneSetupNode has no sceneData.", node);
            if (needExit)
                GoNext(node, "exit");
            return;
        }

        RefreshSceneAssets(data);
        bool wasCutsceneBackgroundLayout = cutsceneBackgroundSceneActive;
        bool useCutsceneBackgroundLayout = IsCutsceneBackgroundScene(node, data);

        if (wasCutsceneBackgroundLayout && !useCutsceneBackgroundLayout)
            backgroundView?.HideCurrentMediaBeforeLayoutSwitch();

        cutsceneBackgroundSceneActive = useCutsceneBackgroundLayout;

        if (useCutsceneBackgroundLayout)
            BeginCutsceneBackgroundFraming();
        else
            EndCutsceneBackgroundFraming();

        ResetCutsceneBackgroundCamera();

        if (backgroundView != null && data.backgroundVideo != null)
            backgroundView.SetBackgroundVideo(data.backgroundVideo);
        else if (backgroundView != null && data.backgroundGif != null)
            backgroundView.SetBackgroundGif(data.backgroundGif);
        else if (backgroundView != null && data.background != null)
            backgroundView.SetBackground(data.background);
        else if (IsIntentionalEmptyBackgroundScene(node))
            backgroundView?.ClearBackground();
        else
            Debug.LogWarning($"[StoryManager] Scene '{node.guid}' has no background media. Keeping the previous background.", node);

        if (useCutsceneBackgroundLayout)
            BeginCutsceneBackgroundFraming();

        ApplySceneAudio(data);

        tapHandler?.ResetCooldown();

        if (needExit)
            GoNext(node, "exit");
    }

    void RefreshSceneAssets(SceneSetupData data)
    {
        if (data == null)
            return;

        ChapterData chapter = GetCurrentChapterOrNull();
        StoryJsonAssetResolver resolver = CreateJsonAssetResolver(chapter);

        VideoClip resolvedBackgroundVideo = ResolveOptionalAsset(data.backgroundVideoId, resolver.ResolveVideoClip);
        TextAsset resolvedBackgroundGif = ResolveOptionalAsset(data.backgroundGifId, resolver.ResolveTextAsset);
        Sprite resolvedBackground = ResolveOptionalAsset(data.backgroundId, resolver.ResolveSprite);

        if (resolvedBackgroundVideo == null && !string.IsNullOrWhiteSpace(data.backgroundId))
            resolvedBackgroundVideo = resolver.ResolveVideoClip(data.backgroundId);

        if (resolvedBackgroundVideo == null && data.backgroundVideo != null)
            resolvedBackgroundVideo = data.backgroundVideo;

        if (resolvedBackgroundGif == null && data.backgroundGif != null)
            resolvedBackgroundGif = data.backgroundGif;

        if (resolvedBackground == null && data.background != null)
            resolvedBackground = data.background;

        if (resolvedBackgroundVideo != null)
        {
            data.backgroundVideo = resolvedBackgroundVideo;
            data.backgroundGif = null;
            data.background = null;
        }
        else if (resolvedBackgroundGif != null)
        {
            data.backgroundVideo = null;
            data.backgroundGif = resolvedBackgroundGif;
            data.background = null;
        }
        else if (resolvedBackground != null)
        {
            data.backgroundVideo = null;
            data.backgroundGif = null;
            data.background = resolvedBackground;
        }

        Sprite overlay = ResolveOptionalAsset(data.backgroundOverlayId, resolver.ResolveSprite);
        if (overlay != null)
            data.backgroundOverlay = overlay;

        AudioClip music = ResolveOptionalAsset(data.musicId, resolver.ResolveAudioClip);
        if (music != null)
            data.music = music;

        AudioClip startSfx = ResolveOptionalAsset(data.startSfxId, resolver.ResolveAudioClip);
        if (startSfx != null)
            data.startSfx = startSfx;
    }

    static bool IsIntentionalEmptyBackgroundScene(SceneSetupNode node)
    {
        if (node == null)
            return false;

        return ContainsEmptyBackgroundMarker(node.guid) ||
               ContainsEmptyBackgroundMarker(node.sceneLabel) ||
               ContainsEmptyBackgroundMarker(node.suggestedBackground);
    }

    static bool ContainsEmptyBackgroundMarker(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string lower = value.ToLowerInvariant();
        return lower.Contains("dark") ||
               lower.Contains("black") ||
               lower.Contains("\u0442\u0435\u043c\u043d") ||
               lower.Contains("\u0442\u0451\u043c\u043d") ||
               lower.Contains("\u0447\u0435\u0440\u043d") ||
               lower.Contains("\u0447\u0451\u0440\u043d");
    }

    static bool IsCutsceneBackgroundScene(SceneSetupNode node, SceneSetupData data)
    {
        if (node == null || data == null)
            return false;

        return IsCutsceneSceneMarker(node.guid) ||
               IsCutsceneSceneMarker(node.sceneLabel) ||
               IsCutsceneSceneMarker(node.suggestedBackground) ||
               IsCutsceneMediaId(data.backgroundId) ||
               IsCutsceneMediaId(data.backgroundVideoId) ||
               IsCutsceneMediaId(data.backgroundGifId) ||
               IsCutsceneMediaName(data.background) ||
               IsCutsceneMediaName(data.backgroundVideo) ||
               IsCutsceneMediaName(data.backgroundGif);
    }

    static bool IsCutsceneSceneMarker(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string prepared = value.Replace('\\', '/').ToLowerInvariant();
        return prepared.Contains("\u043a\u0430\u0442-\u0441\u0446\u0435\u043d") ||
               prepared.Contains("\u043a\u0430\u0442\u0441\u0446\u0435\u043d") ||
               prepared.Contains("cutscene") ||
               prepared.Contains("cg_") ||
               prepared.Contains("_cg");
    }

    static bool IsCutsceneMediaId(string mediaId)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
            return false;

        string prepared = mediaId.Replace('\\', '/').ToLowerInvariant();
        return prepared.Contains("\u043a\u0430\u0442-\u0441\u0446\u0435\u043d\u044b") ||
               prepared.Contains("\u043a\u0430\u0442\u0441\u0446\u0435\u043d\u044b") ||
               prepared.Contains("cutscene") ||
               prepared.Contains("/cg/") ||
               prepared.Contains("/cg_");
    }

    static bool IsCutsceneMediaName(UnityEngine.Object asset)
    {
        if (asset == null)
            return false;

        string prepared = asset.name.ToLowerInvariant();
        return prepared.Contains("cutscene") ||
               prepared.StartsWith("cg_") ||
               prepared.Contains("_cg");
    }

    static T ResolveOptionalAsset<T>(string id, Func<string, T> resolver) where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(id) || resolver == null)
            return null;

        return resolver(id);
    }

}
