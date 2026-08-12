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
    private void ResetCameraPosition()
    {
        var cam = cameraController ?? CameraController.Instance;
        cam?.ResetInstant();
    }

    void Update()
    {
        if (ShouldStartJumpToPhoneFromKeyboard())
        {
            StartJumpToPhone();
            return;
        }

        if (isJumpingToPhone)
            return;

        if (ShouldStartSkipToNextCutsceneFromKeyboard())
        {
            StartSkipToNextCutscene();
            return;
        }

        if (ShouldStartSkipToNextChoiceFromKeyboard())
        {
            StartSkipToNextChoice();
            return;
        }

        if (IsAutoSkippingStoryContent())
            return;

        if (ShouldAdvanceDialogueFromKeyboard())
        {
            AdvanceDialogueFromInput();
            return;
        }

        // Фолбэк: если DialogueTapHandler не назначен — ловим тап/клик здесь.
        // (Работает, но не учитывает UI-блокировку — назначь tapHandler в инспекторе для продакшена.)
        if (tapHandler != null) return; // tapHandler сам всё обрабатывает

        bool tapped = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        tapped = Input.GetMouseButtonDown(0);
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            tapped = true;
#endif

        if (!tapped) return;

        // Не срабатывать когда нажат UI-элемент (кнопка, слайдер и т.д.)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
#if !UNITY_EDITOR && !UNITY_STANDALONE
        if (Input.touchCount > 0 && EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return;
#endif

        OnDialogueClick();
    }

    bool ShouldStartSkipToNextChoiceFromKeyboard()
    {
        if (!skipToNextChoiceWithKeyboard || skipToNextChoiceKey == KeyCode.None)
            return false;

        if (IsShortcutModifierHeld())
            return false;

        if (ignoreDialogueKeyboardInputWhenTyping && IsTypingIntoSelectedInputField())
            return false;

        return Input.GetKeyDown(skipToNextChoiceKey);
    }

    bool ShouldStartSkipToNextCutsceneFromKeyboard()
    {
        if (!skipToNextCutsceneWithKeyboard || skipToNextCutsceneKey == KeyCode.None)
            return false;

        if (IsShortcutModifierHeld())
            return false;

        if (ignoreDialogueKeyboardInputWhenTyping && IsTypingIntoSelectedInputField())
            return false;

        return Input.GetKeyDown(skipToNextCutsceneKey);
    }

    bool ShouldStartJumpToPhoneFromKeyboard()
    {
        if (!jumpToPhoneWithKeyboard || jumpToPhoneKey == KeyCode.None)
            return false;

        if (IsShortcutModifierHeld())
            return false;

        if (ignoreDialogueKeyboardInputWhenTyping && IsTypingIntoSelectedInputField())
            return false;

        return Input.GetKeyDown(jumpToPhoneKey);
    }

    public void StartSkipToNextChoice()
    {
        if (skipToNextChoiceRoutine != null || skipToNextCutsceneRoutine != null || GameState.Instance == null)
            return;

        skipToNextChoiceRoutine = StartCoroutine(SkipToNextChoiceRoutine());
    }

    public void StartSkipToNextCutscene()
    {
        if (skipToNextChoiceRoutine != null || skipToNextCutsceneRoutine != null || GameState.Instance == null)
            return;

        skipToNextCutsceneRoutine = StartCoroutine(SkipToNextCutsceneRoutine());
    }

    public void StartJumpToPhone()
    {
        if (jumpToPhoneRoutine != null ||
            skipToNextChoiceRoutine != null ||
            skipToNextCutsceneRoutine != null ||
            GameState.Instance == null)
        {
            return;
        }

        jumpToPhoneRoutine = StartCoroutine(JumpToPhoneRoutine());
    }

    IEnumerator SkipToNextChoiceRoutine()
    {
        const int yieldEveryNodes = 32;
        isSkippingToNextChoice = true;

        int skippedNodes = 0;
        while (skippedNodes < skipToNextChoiceMaxNodes)
        {
            BaseStoryNode node = GetCurrentNodeForSkip();
            if (node == null || IsSkipToNextChoiceStopNode(node))
                break;

            if (activeDialogueNode != null)
            {
                SkipActiveDialogueNodeToExit();
                skippedNodes++;

                if (skippedNodes % yieldEveryNodes == 0)
                    yield return null;

                continue;
            }

            // Some nodes wait for UI/coroutines. If they do not advance themselves
            // during skip mode, stop here instead of looping forever.
            BaseStoryNode beforeWait = node;
            yield return null;

            BaseStoryNode afterWait = GetCurrentNodeForSkip();
            if (afterWait == beforeWait && activeDialogueNode == null)
                break;
        }

        isSkippingToNextChoice = false;
        skipToNextChoiceRoutine = null;
    }

    IEnumerator SkipToNextCutsceneRoutine()
    {
        const int yieldEveryNodes = 32;
        isSkippingToNextCutscene = true;

        int skippedNodes = 0;
        while (skippedNodes < skipToNextChoiceMaxNodes)
        {
            BaseStoryNode node = GetCurrentNodeForSkip();
            if (node == null || IsCutsceneSkipTargetNode(node))
                break;

            if (activeDialogueNode != null && TryStopSkipOnDialogueBeforeCutscene(activeDialogueNode))
                break;

            if (IsSkipToNextCutsceneBlockedByInteraction(node))
                break;

            if (TrySkipCurrentNonCutsceneImageNode(node))
            {
                skippedNodes++;

                if (skippedNodes % yieldEveryNodes == 0)
                    yield return null;

                continue;
            }

            if (activeDialogueNode != null)
            {
                SkipActiveDialogueNodeToExit();
                skippedNodes++;

                if (skippedNodes % yieldEveryNodes == 0)
                    yield return null;

                continue;
            }

            BaseStoryNode beforeWait = node;
            yield return null;

            BaseStoryNode afterWait = GetCurrentNodeForSkip();
            if (afterWait == beforeWait && activeDialogueNode == null)
                break;
        }

        isSkippingToNextCutscene = false;
        skipToNextCutsceneRoutine = null;
    }

    IEnumerator JumpToPhoneRoutine()
    {
        const int yieldEveryNodes = 32;
        isJumpingToPhone = true;

        AppLogger.Info(
            AppLogCategory.PhoneDialogue,
            nameof(StoryManager),
            nameof(JumpToPhoneRoutine),
            "Запущен быстрый переход к phone node.",
            LogMetadata.Of(
                "storyId", CurrentStoryId,
                "targetGuid", jumpToPhoneTargetNodeGuid ?? "",
                "maxNodes", jumpToPhoneMaxNodes));

        int skippedNodes = 0;
        bool reachedPhone = false;
        while (skippedNodes < jumpToPhoneMaxNodes)
        {
            BaseStoryNode node = GetCurrentNodeForSkip();
            if (node == null)
                break;

            if (IsPhoneJumpTargetNode(node))
            {
                reachedPhone = true;
                break;
            }

            if (node is PhoneDialogueNode)
            {
                phoneDialogueUI?.Hide();
                GoNext(node, "exit");
                skippedNodes++;
                if (skippedNodes % yieldEveryNodes == 0)
                    yield return null;
                continue;
            }

            if (activeDialogueNode != null)
            {
                SkipActiveDialogueNodeToExit();
                skippedNodes++;

                if (skippedNodes % yieldEveryNodes == 0)
                    yield return null;

                continue;
            }

            if (node is ChoiceNode choiceNode)
            {
                if (!TrySelectConfiguredDefaultChoiceForPhoneJump(choiceNode))
                    break;

                skippedNodes++;
                if (skippedNodes % yieldEveryNodes == 0)
                    yield return null;
                continue;
            }

            if (IsPhoneJumpBlockedBySetupNode(node))
            {
                AppLogger.Warn(
                    AppLogCategory.PhoneDialogue,
                    nameof(StoryManager),
                    nameof(JumpToPhoneRoutine),
                    "Быстрый переход к phone node остановлен на setup-ноде, которую нельзя безопасно выбрать автоматически.",
                    LogMetadata.Of("node", node.name, "guid", node.guid, "type", node.GetType().Name));
                break;
            }

            if (TrySkipCurrentImageNodeForPhoneJump(node))
            {
                skippedNodes++;
                if (skippedNodes % yieldEveryNodes == 0)
                    yield return null;
                continue;
            }

            BaseStoryNode beforeWait = node;
            yield return null;

            BaseStoryNode afterWait = GetCurrentNodeForSkip();
            if (afterWait == beforeWait && activeDialogueNode == null)
                break;
        }

        if (reachedPhone)
        {
            AppLogger.Info(
                AppLogCategory.PhoneDialogue,
                nameof(StoryManager),
                nameof(JumpToPhoneRoutine),
                "Быстрый переход к phone node завершён.",
                LogMetadata.Of("skippedNodes", skippedNodes, "storyId", CurrentStoryId));
        }
        else
        {
            AppLogger.Warn(
                AppLogCategory.PhoneDialogue,
                nameof(StoryManager),
                nameof(JumpToPhoneRoutine),
                "Phone node не найдена в пределах лимита быстрого перехода.",
                LogMetadata.Of("skippedNodes", skippedNodes, "maxNodes", jumpToPhoneMaxNodes, "storyId", CurrentStoryId));
        }

        isJumpingToPhone = false;
        jumpToPhoneRoutine = null;
    }

    bool IsPhoneJumpTargetNode(BaseStoryNode node)
    {
        if (!(node is PhoneDialogueNode))
            return false;

        string targetGuid = (jumpToPhoneTargetNodeGuid ?? "").Trim();
        if (string.IsNullOrEmpty(targetGuid))
            return true;

        return string.Equals(node.guid, targetGuid, StringComparison.OrdinalIgnoreCase);
    }

    bool IsPhoneJumpBlockedBySetupNode(BaseStoryNode node)
    {
        return node is AppearanceChoiceNode ||
               node is WardrobeChoiceNode ||
               node is NameChoiceNode ||
               node is OpenWardrobeNode;
    }

    bool TrySelectConfiguredDefaultChoiceForPhoneJump(ChoiceNode node)
    {
        if (node == null || node.options == null || node.options.Count == 0)
            return false;

        int start = Mathf.Clamp(jumpToPhoneDefaultChoiceIndex, 0, node.options.Count - 1);
        for (int offset = 0; offset < node.options.Count; offset++)
        {
            int index = (start + offset) % node.options.Count;
            if (!IsSafePhoneJumpChoice(node.options[index]))
                continue;

            AppLogger.Info(
                AppLogCategory.PhoneDialogue,
                nameof(StoryManager),
                nameof(TrySelectConfiguredDefaultChoiceForPhoneJump),
                "Автоматически выбран безопасный вариант для перехода к phone node.",
                LogMetadata.Of("node", node.name, "guid", node.guid, "choiceIndex", index));
            DoSelectChoice(node, index);
            return true;
        }

        AppLogger.Warn(
            AppLogCategory.PhoneDialogue,
            nameof(StoryManager),
            nameof(TrySelectConfiguredDefaultChoiceForPhoneJump),
            "Не найден безопасный вариант выбора для перехода к phone node.",
            LogMetadata.Of("node", node.name, "guid", node.guid, "options", node.options.Count));
        return false;
    }

    bool IsSafePhoneJumpChoice(ChoiceOption option)
    {
        if (option == null)
            return false;

        if (!ChoiceRegionFilter.IsVisible(option))
            return false;

        if (!string.IsNullOrEmpty(option.requiredVariable) &&
            GameState.Instance != null &&
            GameState.Instance.GetInt(option.requiredVariable) < option.requiredValue)
        {
            return false;
        }

        if (option.isPremium && !jumpToPhoneAllowPremiumDefaultChoice)
            return false;

        return true;
    }

    bool TrySkipCurrentImageNodeForPhoneJump(BaseStoryNode node)
    {
        if (!(node is ImageNode imageNode))
            return false;

        HideImageOverlayIfVisible();
        GoNext(imageNode, "exit");
        return true;
    }

    bool TrySkipCurrentNonCutsceneImageNode(BaseStoryNode node)
    {
        if (!(node is ImageNode imageNode) || IsCutsceneImageNode(imageNode))
            return false;

        HideImageOverlayIfVisible();
        GoNext(imageNode, "exit");
        return true;
    }

    BaseStoryNode GetCurrentNodeForSkip()
    {
        if (activeDialogueNode != null)
            return activeDialogueNode;

        return GameState.Instance != null ? GameState.Instance.currentNode as BaseStoryNode : null;
    }

    bool IsSkipToNextChoiceStopNode(BaseStoryNode node)
    {
        return node is ChoiceNode ||
               node is AppearanceChoiceNode ||
               node is WardrobeChoiceNode ||
               node is NameChoiceNode ||
               node is OpenWardrobeNode;
    }

    bool IsSkipToNextCutsceneBlockedByInteraction(BaseStoryNode node)
    {
        return IsSkipToNextChoiceStopNode(node);
    }

    bool IsCutsceneSkipTargetNode(BaseStoryNode node)
    {
        return node is CutsceneNode || IsCutsceneImageNode(node as ImageNode);
    }

    bool IsCutsceneImageNode(ImageNode node)
    {
        if (node == null)
            return false;

        if (node.video != null || node.gif != null)
            return true;

        if (!node.zoomable && node.image != null)
            return true;

        return ContainsCutsceneMarker(node.caption) ||
               ContainsCutsceneMarker(node.description) ||
               ContainsCutsceneMarker(node.image != null ? node.image.name : "");
    }

    static bool ContainsCutsceneMarker(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string prepared = value.Trim().ToLowerInvariant();
        return prepared.Contains("cutscene") ||
               prepared.Contains("кат-сцен") ||
               prepared.Contains("катсцен") ||
               prepared.Contains("\u043a\u0430\u0442-\u0441\u0446\u0435\u043d") ||
               prepared.Contains("\u043a\u0430\u0442 \u0441\u0446\u0435\u043d") ||
               prepared.Contains("\u043a\u0430\u0442\u0441\u0446\u0435\u043d") ||
               prepared.StartsWith("cg_", StringComparison.Ordinal) ||
               prepared.Contains("_cg_");
    }

    bool TryStopSkipOnDialogueBeforeCutscene(DialogueNode node)
    {
        if (node == null || node is CutsceneNode || !DoesExitLeadToCutsceneSkipTarget(node))
            return false;

        ShowLastDialogueLineBeforeCutscene(node);
        return true;
    }

    bool DoesExitLeadToCutsceneSkipTarget(BaseStoryNode node)
    {
        if (node == null)
            return false;

        NodePort exitPort = node.GetOutputPort("exit");
        if (exitPort == null)
            return false;

        foreach (NodePort connection in exitPort.GetConnections())
        {
            if (IsCutsceneSkipTargetNode(connection.node as BaseStoryNode))
                return true;
        }

        return false;
    }

    void ShowLastDialogueLineBeforeCutscene(DialogueNode node)
    {
        if (node == null || node.lines == null || node.lines.Count == 0)
            return;

        if (!EnsureDialogueUI("showing the dialogue line before a cutscene"))
            return;

        int lastLineIndex = node.lines.Count - 1;
        RecordSkippedDialogueHistoryRange(node, currentLineIndex + 1, lastLineIndex);

        activeDialogueNode = node;
        currentLineIndex = lastLineIndex;
        ResetDialogueLinePages();

        DialogueLine line = node.lines[currentLineIndex];
        if (IsNarrationLine(line))
            HandleNarrationLine(line);
        else
            TryShowDialogueSpeaker(line, !HasExplicitCharacters(node), out _);

        ShowDialogueLinePage(line);
        RecordDialogueHistory(line);
        TryAutoPan(line);
        PersistProgress(node);
    }

    bool IsAutoSkippingStoryContent()
    {
        return isSkippingToNextChoice || isSkippingToNextCutscene;
    }

    void SkipActiveDialogueNodeToExit()
    {
        DialogueNode node = activeDialogueNode;
        if (node == null)
            return;

        DialogueUIManager targetUi = GetDialogueInterfaceForNode(node);
        activeDialogueNode = null;
        RecordSkippedDialogueHistory(node, currentLineIndex + 1);
        currentLineIndex = 0;
        ResetDialogueLinePages();
        bool exitOpensScene = node is CutsceneNode && DoesCutsceneExitOpenScene(node);
        if (node is CutsceneNode)
        {
            PrepareCutsceneBackgroundForExit(node);
            HideCutsceneUserInterfaceIfNeeded(node);
        }
        else
        {
            ClearCutsceneRuntimeState();
            targetUi?.ClearDialogue();
        }

        GoNext(node, "exit");
        if (exitOpensScene)
            CompleteCutsceneBackgroundExit();
    }

    void RecordSkippedDialogueHistory(DialogueNode node, int startIndex)
    {
        if (node == null || node.lines == null)
            return;

        RecordSkippedDialogueHistoryRange(node, startIndex, node.lines.Count);
    }

    void RecordSkippedDialogueHistoryRange(DialogueNode node, int startIndex, int endIndexExclusive)
    {
        if (node == null || node.lines == null)
            return;

        int first = Mathf.Clamp(startIndex, 0, node.lines.Count);
        int last = Mathf.Clamp(endIndexExclusive, first, node.lines.Count);
        for (int i = first; i < last; i++)
            RecordDialogueHistory(node.lines[i]);
    }

    bool ShouldAdvanceDialogueFromKeyboard()
    {
        if (!advanceDialogueWithKeyboard || !Input.anyKeyDown)
            return false;

        if (IsShortcutModifierHeld())
            return false;

        if (ignoreDialogueKeyboardInputWhenTyping && IsTypingIntoSelectedInputField())
            return false;

        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            return true;
        }

        if (!advanceDialogueWithAnyKeyboardKey)
            return false;

        return !AnyServiceKeyboardKeyDown() && AnyKeyboardKeyDown();
    }

    static bool IsShortcutModifierHeld()
    {
        return Input.GetKey(KeyCode.LeftControl) ||
               Input.GetKey(KeyCode.RightControl) ||
               Input.GetKey(KeyCode.LeftShift) ||
               Input.GetKey(KeyCode.RightShift) ||
               Input.GetKey(KeyCode.LeftAlt) ||
               Input.GetKey(KeyCode.RightAlt) ||
               Input.GetKey(KeyCode.LeftCommand) ||
               Input.GetKey(KeyCode.RightCommand);
    }

    void AdvanceDialogueFromInput()
    {
        if (tapHandler != null)
            tapHandler.TryAdvance(ignoreCooldown: true);
        else
            OnDialogueClick();
    }

    static bool AnyKeyboardKeyDown()
    {
        for (int i = 0; i < KeyboardAdvanceKeys.Length; i++)
        {
            if (Input.GetKeyDown(KeyboardAdvanceKeys[i]))
                return true;
        }

        return false;
    }

    static bool AnyServiceKeyboardKeyDown()
    {
        return Input.GetKeyDown(KeyCode.Print) ||
               Input.GetKeyDown(KeyCode.SysReq) ||
               Input.GetKeyDown(KeyCode.Pause) ||
               Input.GetKeyDown(KeyCode.Break) ||
               Input.GetKeyDown(KeyCode.CapsLock) ||
               Input.GetKeyDown(KeyCode.Numlock) ||
               Input.GetKeyDown(KeyCode.ScrollLock) ||
               Input.GetKeyDown(KeyCode.Menu) ||
               Input.GetKeyDown(KeyCode.LeftWindows) ||
               Input.GetKeyDown(KeyCode.RightWindows) ||
               Input.GetKeyDown(KeyCode.Help);
    }

    static bool IsTypingIntoSelectedInputField()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            return false;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        return selected.GetComponentInParent<TMP_InputField>() != null ||
               selected.GetComponentInParent<InputField>() != null;
    }

    static KeyCode[] BuildKeyboardAdvanceKeys()
    {
        Array values = Enum.GetValues(typeof(KeyCode));
        var keys = new List<KeyCode>(values.Length);

        foreach (KeyCode key in values)
        {
            if (key == KeyCode.None)
                continue;

            string keyName = key.ToString();
            if (keyName.StartsWith("Mouse", StringComparison.Ordinal) ||
                keyName.StartsWith("Joystick", StringComparison.Ordinal) ||
                IsModifierKeyName(keyName) ||
                IsServiceKeyboardKeyName(keyName))
            {
                continue;
            }

            keys.Add(key);
        }

        return keys.ToArray();
    }

    static bool IsServiceKeyboardKey(KeyCode key)
    {
        return IsServiceKeyboardKeyName(key.ToString());
    }

    static bool IsModifierKeyName(string keyName)
    {
        return keyName.Contains("Shift") ||
               keyName.Contains("Control") ||
               keyName.Contains("Alt") ||
               keyName.Contains("Command") ||
               keyName.Contains("Apple");
    }

    static bool IsServiceKeyboardKeyName(string keyName)
    {
        switch (keyName)
        {
            case "Print":
            case "SysReq":
            case "Pause":
            case "Break":
            case "CapsLock":
            case "Numlock":
            case "ScrollLock":
            case "Menu":
            case "LeftWindows":
            case "RightWindows":
            case "Help":
                return true;
            default:
                return false;
        }
    }

}
