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
    // ── Подписочные функции ──────────────────────────────────

    /// <summary>
    /// Перемотка вперёд на N слайдов. Требует подписки.
    /// Вызывается из кнопки в UI.
    /// </summary>
    public void FastForward()
    {
        if (!CheckSubscription(SubscriptionFeature.FastForward)) return;

        var history = storyHistory ?? StoryHistory.Instance;
        // Серверные флаги приоритетнее локальных настроек
        int steps = NetworkManager.IsAuthenticated
            ? NetworkManager.FastForwardSteps
            : (history != null ? history.FastForwardSteps : 5);

        // Прокручиваем N раз — каждый раз как клик по диалогу.
        // Останавливаемся на выборах (activeDialogueNode == null) — нельзя выбирать ветку автоматически.
        for (int i = 0; i < steps; i++)
        {
            if (activeDialogueNode == null) break;
            OnDialogueClick();
            if (activeDialogueNode == null) break;
        }
    }

    /// <summary>
    /// Сохранить закладку на текущей ноде. Требует подписки.
    /// </summary>
    public void SaveBookmark()
    {
        if (!CheckSubscription(SubscriptionFeature.Bookmarks)) return;

        var history = storyHistory ?? StoryHistory.Instance;
        var node = activeDialogueNode ?? (BaseStoryNode)GameState.Instance?.currentNode;

        if (history == null || node == null)
        {
            ToastManager.Instance?.ShowSystemMessage("Нечего сохранять");
            return;
        }

        SaveData snapshot = SaveManager.Instance != null
            ? SaveManager.Instance.BuildCurrentSaveData(this)
            : null;

        if (snapshot == null)
        {
            ToastManager.Instance?.ShowSystemMessage("Не удалось сохранить закладку");
            return;
        }

        history.SaveBookmark(snapshot);

        if (NetworkManager.Instance != null)
            NetworkManager.Instance.SaveBookmarkAsync(snapshot);

        ToastManager.Instance?.ShowSystemMessage("Сцена сохранена в закладки");
    }

    /// <summary>
    /// Перейти к закладке (если есть). Требует подписки.
    /// </summary>
    public void GoToBookmark()
    {
        if (!CheckSubscription(SubscriptionFeature.Bookmarks)) return;

        StartCoroutine(GoToBookmarkRoutine());
    }

    IEnumerator GoToBookmarkRoutine()
    {
        var history = storyHistory ?? StoryHistory.Instance;
        if (history == null)
        {
            ToastManager.Instance?.ShowSystemMessage("Закладок нет");
            yield break;
        }

        history.LoadBookmarkFromPrefs(CurrentStoryId);
        var bookmark = history.GetBookmark();

        if (!history.HasBookmark && NetworkManager.Instance != null && NetworkManager.IsAuthenticated)
        {
            SaveData serverBookmark = null;
            yield return NetworkManager.Instance.LoadBookmarkSnapshot(data => serverBookmark = data);

            if (serverBookmark != null && BookmarkMatchesSelectedStory(serverBookmark))
            {
                history.ApplyServerBookmark(serverBookmark);
                bookmark = history.GetBookmark();
            }
        }

        if (!history.HasBookmark || bookmark.saveData == null)
        {
            ToastManager.Instance?.ShowSystemMessage("Закладок нет");
            yield break;
        }

        if (TryRestoreSnapshot(bookmark.saveData, "bookmark"))
        {
            string time = bookmark.savedAt.ToString("HH:mm");
            ToastManager.Instance?.ShowSystemMessage($"Возврат к сцене ({time})");
            yield break;
        }

        ToastManager.Instance?.ShowSystemMessage("Сцена из закладки не найдена");
    }

    /// <summary>
    /// Проверить подписку. Если нет — показать тост и вернуть false.
    /// </summary>
    bool CheckSubscription(SubscriptionFeature feature)
    {
        if (feature == SubscriptionFeature.FastForward && NetworkManager.FastForwardEnabled) return true;
        if (feature == SubscriptionFeature.Bookmarks && NetworkManager.BookmarksEnabled) return true;

        var sub = SubscriptionManager.Instance;
        if (sub != null && sub.Has(feature)) return true;

        ToastManager.Instance?.ShowSystemMessage("Доступно по подписке");
        return false;
    }

    public void ReturnToMainMenu()
    {
        FadeOutStorySessionAudio();

        if (menuController != null)
        {
            menuController.ReturnToMenu(CloseEndPanel);
        }
        else
        {
            CloseEndPanel();
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }

    void ProcessStatChange(StatChangeNode node)
    {
        if (node == null || !EnsureGameState("applying stat change"))
            return;

        int previousValue = GameState.Instance.GetStat(node.statId);
        GameState.Instance.AddStat(node.statId, node.delta);
        int appliedDelta = GameState.Instance.GetStat(node.statId) - previousValue;
        RecordEpisodeStatDelta(node.statId, appliedDelta);
        ReportRelationshipStatChange(node.statId, appliedDelta);
        ShowStatChangeFeedback(node.statId, node.displayName, appliedDelta, node.systemMessage);

        GoNext(node, "exit");
    }

    void ShowStatChangeFeedback(string statId, string displayName, int delta, string systemMessage)
    {
        string resolvedDisplayName = ResolveStatDisplayName(statId, displayName);
        systemMessage = NormalizeRelationshipSystemMessage(systemMessage, delta);
        bool hasStatText = !string.IsNullOrWhiteSpace(resolvedDisplayName);
        bool hasSystemMessage = !string.IsNullOrWhiteSpace(systemMessage);

        if (!hasStatText && !hasSystemMessage)
            return;

        if (_statChangeOverlay == null)
            _statChangeOverlay = FindObjectOfType<StatChangeOverlay>(true);

        if (_statChangeOverlay != null)
        {
            if (!hasStatText && hasSystemMessage)
                _statChangeOverlay.ShowMessage(systemMessage);
            else
                _statChangeOverlay.ShowStatChange(statId, resolvedDisplayName, delta, systemMessage);
            return;
        }

        if (ToastManager.Instance == null)
            return;

        if (hasSystemMessage)
            ToastManager.Instance.ShowSystemMessage(systemMessage);
        else
            ToastManager.Instance.ShowStat(resolvedDisplayName, delta);
    }

    static string NormalizeRelationshipSystemMessage(string systemMessage, int delta)
    {
        if (string.IsNullOrWhiteSpace(systemMessage) || delta == 0)
            return systemMessage;

        string trimmed = systemMessage.Trim();
        const string improvedPrefix = "У вас улучшились отношения ";
        if (trimmed.StartsWith(improvedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return BuildRelationshipSystemMessage(trimmed.Substring(improvedPrefix.Length), true);
        }

        const string worsenedPrefix = "У вас ухудшились отношения ";
        if (trimmed.StartsWith(worsenedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return BuildRelationshipSystemMessage(trimmed.Substring(worsenedPrefix.Length), false);
        }

        if (!TryExtractRelationshipTarget(trimmed, out string relationTarget))
            return systemMessage;

        return BuildRelationshipSystemMessage(relationTarget, delta > 0);
    }

    static string BuildRelationshipSystemMessage(string relationTarget, bool improved)
    {
        relationTarget = NormalizeRelationshipTarget(relationTarget);
        if (string.IsNullOrWhiteSpace(relationTarget))
            return "";

        string verb = improved ? "улучшились" : "ухудшились";
        return $"Отношения {relationTarget} {verb}.";
    }

    static string NormalizeRelationshipTarget(string relationTarget)
    {
        if (string.IsNullOrWhiteSpace(relationTarget))
            return "";

        return relationTarget.Trim().TrimEnd('.');
    }

    static bool TryExtractRelationshipTarget(string message, out string relationTarget)
    {
        relationTarget = "";

        if (TryExtractAfterPrefixBeforeSuffix(
                message,
                "Отношения с ",
                new[] { " улучшились", " стали теплее", " стали лучше", " ухудшились", " стали хуже" },
                out string target))
        {
            relationTarget = "с " + CapitalizeFirstLetter(target);
            return true;
        }

        if (TryExtractAfterPrefixBeforeSuffix(
                message,
                "Ваши с ",
                new[] { " отношения улучшились", " отношения стали теплее", " отношения стали лучше", " отношения ухудшились", " отношения стали хуже" },
                out target))
        {
            relationTarget = "с " + CapitalizeFirstLetter(target);
            return true;
        }

        return false;
    }

    static bool TryExtractAfterPrefixBeforeSuffix(string value, string prefix, string[] suffixes, out string extracted)
    {
        extracted = "";

        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrEmpty(prefix) || suffixes == null)
            return false;

        string trimmed = value.Trim().TrimEnd('.');
        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string withoutPrefix = trimmed.Substring(prefix.Length);
        for (int i = 0; i < suffixes.Length; i++)
        {
            string suffix = suffixes[i];
            if (string.IsNullOrEmpty(suffix) || !withoutPrefix.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            extracted = withoutPrefix.Substring(0, withoutPrefix.Length - suffix.Length).Trim();
            return !string.IsNullOrWhiteSpace(extracted);
        }

        return false;
    }

    static string CapitalizeFirstLetter(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        value = value.Trim();
        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsLetter(value[i]))
                continue;

            char upper = char.ToUpperInvariant(value[i]);
            return i == 0
                ? upper + value.Substring(1)
                : value.Substring(0, i) + upper + value.Substring(i + 1);
        }

        return value;
    }

    static string EnsureSentencePeriod(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        value = value.Trim();
        char last = value[value.Length - 1];
        return last == '.' || last == '!' || last == '?' ? value : value + ".";
    }

    static string ResolveStatDisplayName(string statId, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        if (string.IsNullOrWhiteSpace(statId))
            return "";

        switch (statId.Trim().ToLowerInvariant())
        {
            case "town":
            case "city":
            case "gorod":
                return "\u0413\u043e\u0440\u043e\u0434";
            case "story":
            case "tale":
            case "fairytale":
            case "skazka":
                return "\u0421\u043a\u0430\u0437\u043a\u0430";
            case "reputation":
            case "rep":
                return "\u0420\u0435\u043f\u0443\u0442\u0430\u0446\u0438\u044f";
            case "heart":
            case "hearts":
                return "\u0421\u0435\u0440\u0434\u0446\u0430";
            default:
                return "";
        }
    }

    void ProcessCameraNode(CameraNode node)
    {
        var cam = cameraController ?? CameraController.Instance;
        if (cam != null)
        {
            switch (node.mode)
            {
                case CameraNode.CameraMode.Position:
                    cam.PanToSpeaker(node.targetPosition);
                    break;
                case CameraNode.CameraMode.Offset:
                    cam.PanToOffset(node.xOffset, node.duration);
                    break;
                case CameraNode.CameraMode.Reset:
                    cam.ResetInstant();
                    break;
            }
        }
        GoNext(node, "exit");
    }

    /// <summary>
    /// Авто-пан к позиции спикера текущей реплики.
    /// </summary>
    void TryAutoPan(DialogueLine line)
    {
        if (!autoPanToSpeaker) return;

        var cam = cameraController ?? CameraController.Instance;
        if (cam == null || activeDialogueNode == null) return;

        CharacterData speaker = line?.speaker;
        if (speaker == null || !IsRenderableStorySpeaker(speaker)) return;

        if (speaker.keepStorySlotPositionOnSpeakerFocus)
        {
            cam.PanToOffset(cam.centerOffset);
            return;
        }

        if (TryGetActiveSpeakerPosition(activeDialogueNode, speaker, out var position))
        {
            cam.PanToSpeaker(position);
            return;
        }

        cam.PanToOffset(cam.centerOffset);
    }

    void TryPanCutsceneBackground(DialogueLine line)
    {
        if (!moveCutsceneBackgroundWithCamera || line == null)
            return;

        var cam = cameraController ?? CameraController.Instance;
        if (cam == null)
            return;

        CharacterData speaker = line.speaker;
        if (speaker == null || !IsRenderableStorySpeaker(speaker))
            return;

        if (speaker.keepStorySlotPositionOnSpeakerFocus)
        {
            cam.ResetBackgroundOnly(cutsceneBackgroundPanDuration);
            return;
        }

        if (TryGetActiveSpeakerPosition(activeDialogueNode, speaker, out var position))
        {
            cam.PanBackgroundOnlyToSpeaker(position, cutsceneBackgroundCameraStrength, cutsceneBackgroundPanDuration);
            return;
        }

        cam.ResetBackgroundOnly(cutsceneBackgroundPanDuration);
    }

    void ResetCutsceneBackgroundCamera()
    {
        if (!resetCutsceneBackgroundCameraOnExit)
            return;

        var cam = cameraController ?? CameraController.Instance;
        cam?.ResetBackgroundOnly(0f);
    }

    void RecordDialogueHistory(DialogueLine line)
    {
        if (suppressProgressPersistence || line == null || GameState.Instance == null)
            return;

        DialogueIdentityResult identity = ResolveDialogueIdentity(line, line.richText ?? "");
        string text = NormalizeHistoryText(ReplaceStoryPlaceholders(line.richText ?? "", line, identity));
        if (string.IsNullOrEmpty(text))
            return;

        string speaker = identity != null ? NormalizeHistoryText(identity.DisplayName) : "";

        GameState.Instance.AddHistory(string.IsNullOrEmpty(speaker) ? text : speaker + ": " + text);
    }

    void RecordChoiceHistory(ChoiceNode node, int index)
    {
        if (suppressProgressPersistence || node == null || node.options == null || GameState.Instance == null)
            return;

        if (index < 0 || index >= node.options.Count)
            return;

        var option = node.options[index];
        if (option == null)
            return;

        string text = NormalizeHistoryText(ReplaceStoryPlaceholders(option.text ?? ""));
        if (string.IsNullOrEmpty(text))
            return;

        GameState.Instance.AddHistory("> " + text);
    }

    void RecordPhoneHistory(PhoneDialogueNode node)
    {
        if (suppressProgressPersistence || node == null || node.messages == null || GameState.Instance == null)
            return;

        string contact = NormalizeHistoryText(ReplaceStoryPlaceholders(node.contactName ?? ""));
        string playerName = NormalizeHistoryText(ReplaceStoryPlaceholders("{playerName}"));

        foreach (var message in node.messages)
        {
            if (message == null)
                continue;

            string text = NormalizeHistoryText(ReplaceStoryPlaceholders(message.text ?? ""));
            if (string.IsNullOrEmpty(text) && message.attachment != null)
                text = "[attachment]";
            if (string.IsNullOrEmpty(text))
                continue;

            string speaker = message.side == PhoneMessageSide.Incoming ? contact : playerName;
            GameState.Instance.AddHistory(string.IsNullOrEmpty(speaker) ? text : speaker + ": " + text);
        }
    }

    string NormalizeHistoryText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return string.Join(" ", value.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    void ProcessEffect(EffectNode node)
    {
        if (EffectManager.Instance != null)
            EffectManager.Instance.PlayEffect(node);

        GoNext(node, "exit");
    }

    void ProcessPremium(PremiumNode node)
    {
        if (node == null)
            return;

        if (!IsValidPremiumCost(node.cost))
        {
            Debug.LogWarning("[StoryManager] Refused premium node with invalid cost: " + (node != null ? node.cost : 0));
            HandlePremiumFailure(node);
            return;
        }

        int cost = node.cost;
        string pendingKey = GetPremiumNodePendingKey(node);
        if (!string.IsNullOrEmpty(pendingKey) && !_pendingPremiumNodeSpends.Add(pendingKey))
            return;

        if (NetworkManager.Instance != null && NetworkManager.IsAuthenticated)
        {
            StartCoroutine(SpendPremiumNodeAndContinue(node, cost, pendingKey));
            return;
        }

        if (!PrototypeFeatureFlags.LocalPremiumSpendEnabled)
        {
            if (!string.IsNullOrEmpty(pendingKey))
                _pendingPremiumNodeSpends.Remove(pendingKey);

            Debug.LogWarning("[StoryManager] Local premium-node spend is disabled. Route premium access through API/IAP.");
            HandlePremiumFailure(node);
            return;
        }

        if (PlayerData.Hearts >= cost)
        {
            PlayerData.AddHeartValue(-cost);
            if (!string.IsNullOrEmpty(pendingKey))
                _pendingPremiumNodeSpends.Remove(pendingKey);

            GoNext(node, "successNode");
        }
        else
        {
            if (!string.IsNullOrEmpty(pendingKey))
                _pendingPremiumNodeSpends.Remove(pendingKey);

            HandlePremiumFailure(node);
        }
    }

    IEnumerator SpendPremiumNodeAndContinue(PremiumNode node, int cost, string pendingKey)
    {
        bool ok = false;
        yield return NetworkManager.Instance.SpendHearts(
            cost,
            "premium_node",
            node != null ? node.guid : "",
            -1,
            node != null ? node.guid : "",
            result => ok = result);

        if (!string.IsNullOrEmpty(pendingKey))
            _pendingPremiumNodeSpends.Remove(pendingKey);

        if (ok)
            GoNext(node, "successNode");
        else
            HandlePremiumFailure(node);
    }

    string GetPremiumNodePendingKey(PremiumNode node)
    {
        string nodeGuid = node != null ? SaveDataSanitizer.SanitizeIdentifier(node.guid) : "";
        return !string.IsNullOrEmpty(nodeGuid)
            ? "premium-node:" + nodeGuid
            : node != null ? "premium-node-instance:" + node.GetInstanceID() : "";
    }

    void HandlePremiumFailure(PremiumNode node)
    {
        if (node == null)
            return;

        var failPort = node.GetOutputPort("failNode");
        if (failPort != null && failPort.Connection != null)
            GoNext(node, "failNode");
        else
            OpenShopForCurrency();
    }

    void ProcessImageNode(ImageNode node)
    {
        if (isSkippingToNextChoice || (isSkippingToNextCutscene && !IsCutsceneImageNode(node)))
        {
            HideImageOverlayIfVisible();
            GoNext(node, "exit");
            return;
        }

        if (IsCutsceneImageNode(node))
        {
            ProcessCutsceneImage(node);
            return;
        }

        if (imageOverlay == null)
            imageOverlay = ImageOverlayUI.FindOrCreateRuntimeOverlay();

        if (imageOverlay != null)
        {
            imageOverlay.Show(node, () => GoNext(node, "exit"));
        }
        else
        {
            Debug.LogWarning("StoryManager: imageOverlay не назначен");
            GoNext(node, "exit");
        }
    }

    void HideImageOverlayIfVisible()
    {
        if (imageOverlay == null)
            imageOverlay = ImageOverlayUI.Instance;

        imageOverlay?.HideImmediate();
    }

}
