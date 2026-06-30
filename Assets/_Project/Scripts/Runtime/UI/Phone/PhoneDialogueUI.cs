using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PhoneDialogueUI : MonoBehaviour
{
    public static PhoneDialogueUI Instance { get; private set; }
    const float ScrollBottomStickEpsilon = 0.035f;

    [Header("Старые ссылки, сохраняются для совместимости")]
    public GameObject panel;
    public TMP_Text contactNameText;
    public Image contactAvatarImage;
    public RectTransform messagesContainer;
    public GameObject incomingBubblePrefab;
    public GameObject outgoingBubblePrefab;
    public GameObject typingIndicator;
    public TMP_Text tapToContinueText;
    public Button tapArea;

    [Header("Ссылки UI телефона")]
    [SerializeField] private PhoneDialogueUIReferences _phoneReferences = new PhoneDialogueUIReferences();

    [Header("Layout телефона")]
    [SerializeField] private PhoneDialogueLayoutSettings _layoutSettings = new PhoneDialogueLayoutSettings();

    [Header("Предпросмотр телефона")]
    [SerializeField] private PhonePreviewSettings _previewSettings = new PhonePreviewSettings();

    PhoneDialogueUIReferences _activePhoneReferences;
    PhoneDialogueLayoutSettings _activeLayoutSettings;
    PhonePreviewSettings _activePreviewSettings;
    StoryUserInterface _activeConfigurationOwner;

    [Header("Runtime-анимация")]
    [Tooltip("Длительность появления баббла в runtime.")]
    public float bubbleFadeIn = 0.25f;
    [Tooltip("Пауза перед сообщением по умолчанию, если PhoneDialogueNode не задал свою.")]
    public float defaultTypingDelay = 0.8f;

    Action _onComplete;
    bool _isPlaying;
    bool _tapReceived;
    Coroutine _playRoutine;
    Tween _tapHintTween;
    PhoneDialogueNode _activeNode;
    RectTransformLayoutSnapshot _preservedMessageContentRect;
    RectTransform _trackedHeaderContactRect;
    Vector2 _appliedHeaderContactOffset;
    Vector2 _appliedHeaderContactSizeOffset;

    public bool IsVisible => ResolvePanel() != null && ResolvePanel().activeSelf;
    public PhoneDialogueUIReferences PhoneReferences => _activePhoneReferences ?? _phoneReferences;
    public PhoneDialogueLayoutSettings LayoutSettings => _activeLayoutSettings ?? _layoutSettings;
    public PhonePreviewSettings PreviewSettings => _activePreviewSettings ?? _previewSettings;
    public StoryUserInterface ActiveConfigurationOwner => _activeConfigurationOwner;

    bool ArePhoneLayoutSettingsDisabled()
    {
        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        return layoutSettings != null && layoutSettings.disableAllPhoneLayoutSettings;
    }

    struct RectTransformLayoutSnapshot
    {
        public bool isValid;
        public RectTransform rect;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 pivot;
        public Vector3 localScale;
        public Quaternion localRotation;

        public static RectTransformLayoutSnapshot Capture(RectTransform target)
        {
            if (target == null)
                return default;

            return new RectTransformLayoutSnapshot
            {
                isValid = true,
                rect = target,
                anchorMin = target.anchorMin,
                anchorMax = target.anchorMax,
                anchoredPosition = target.anchoredPosition,
                sizeDelta = target.sizeDelta,
                pivot = target.pivot,
                localScale = target.localScale,
                localRotation = target.localRotation
            };
        }

        public bool Restore()
        {
            if (!isValid || rect == null)
                return false;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = localScale;
            rect.localRotation = localRotation;
            return true;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Instance = null;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        EnsureSettings();
        HideMessageTemplates();
        StopPlayback(clearMessages: false, clearCallback: true);
        GameObject root = ResolvePanel();
        if (root != null)
            root.SetActive(false);
    }

    void Start()
    {
        if (tapArea != null)
        {
            tapArea.onClick.RemoveListener(OnTap);
            tapArea.onClick.AddListener(OnTap);
        }
    }

    void OnDisable()
    {
        PhonePreviewSettings previewSettings = PreviewSettings;
        StopPlayback(clearMessages: previewSettings == null || previewSettings.clearPreviewOnDisable, clearCallback: true);

        GameObject root = ResolvePanel();
        if (root != null)
            root.SetActive(false);

        AppLogger.Info(
            AppLogCategory.PhoneDialogue,
            nameof(PhoneDialogueUI),
            nameof(OnDisable),
            "Runtime-предпросмотр телефона остановлен из-за смены сцены или отключения объекта.",
            LogMetadata.Of("object", gameObject != null ? gameObject.name : ""));
    }

    void OnDestroy()
    {
        if (tapArea != null)
            tapArea.onClick.RemoveListener(OnTap);

        StopPlayback(clearMessages: false, clearCallback: true);

        if (Instance == this)
            Instance = null;
    }

    void OnValidate()
    {
        EnsureSettings();
        bubbleFadeIn = Mathf.Max(0f, bubbleFadeIn);
        defaultTypingDelay = Mathf.Max(0f, defaultTypingDelay);
        HideMessageTemplates();
    }

    void OnRectTransformDimensionsChange()
    {
        if (IsVisible)
            RecalculateLayout("RectTransformDimensionsChanged");
    }

    public void Show(PhoneDialogueNode node, Action onComplete)
    {
        EnsureSettings();
        EnsureStoryUserInterfaceConfiguration(nameof(Show));
        StopPlayback(clearMessages: true, clearCallback: false);

        _onComplete = onComplete;
        _tapReceived = false;
        _activeNode = node;

        GameObject root = ResolvePanel();
        if (node == null || root == null)
        {
            ThrottledAppLogger.Warn(
                "PhoneDialogueShowMissingPanelOrNode",
                AppLogCategory.PhoneDialogue,
                nameof(PhoneDialogueUI),
                nameof(Show),
                "Невозможно показать телефонный диалог: не назначен panel или PhoneDialogueNode.",
                LogMetadata.Of("hasNode", node != null, "hasPanel", root != null));
            Complete();
            return;
        }

        PhonePreviewValidationResult validation = PhonePreviewValidator.Validate(this, node, requireMessages: true);
        LogValidation(validation, nameof(Show));
        if (validation.HasErrors)
        {
            Complete();
            return;
        }

        _preservedMessageContentRect = CaptureMessageContentRectIfPreserved();
        ApplyMessageContentPreserveOverrides(nameof(Show));
        ApplyHeader(node, ResolveInitialHeaderContactName(node));
        ClearMessages();
        ScrollToBottom();
        ApplyPhoneLayoutRoots();
        EnsureMessageContentLayout();
        RestoreMessageContentRectIfPreserved(_preservedMessageContentRect, nameof(Show));
        BringToFrontForStory();
        root.SetActive(true);
        BringToFrontForStory();

        float delay = node.typingDelay > 0 ? node.typingDelay : defaultTypingDelay;
        AppLogger.Info(
            AppLogCategory.PhoneDialogue,
            nameof(PhoneDialogueUI),
            nameof(Show),
            "Запущен runtime-предпросмотр телефона.",
            LogMetadata.Of("node", node.name, "messages", node.messages != null ? node.messages.Count : 0));
        _playRoutine = StartCoroutine(PlayMessages(node.messages ?? new List<PhoneMessage>(), delay));
    }

    public bool ShowStaticPreview(PhoneDialogueNode node, string reason = "EditModePreview")
    {
        EnsureSettings();
        EnsureStoryUserInterfaceConfiguration(reason);
        StopPlayback(clearMessages: true, clearCallback: true);
        _activeNode = node;

        GameObject root = ResolvePanel();
        if (root == null)
        {
            ThrottledAppLogger.Warn(
                "StaticPhonePreviewMissingRoot",
                AppLogCategory.PhoneDialogue,
                nameof(PhoneDialogueUI),
                nameof(ShowStaticPreview),
                "Не назначен PhoneDialogueUI. Предпросмотр телефона невозможен.");
            return false;
        }

        PhonePreviewValidationResult validation = PhonePreviewValidator.Validate(this, node, requireMessages: false);
        LogValidation(validation, nameof(ShowStaticPreview));
        if (validation.HasErrors)
            return false;

        _preservedMessageContentRect = CaptureMessageContentRectIfPreserved();
        ApplyMessageContentPreserveOverrides(nameof(ShowStaticPreview));
        root.SetActive(true);
        BringToFrontForStory();
        ApplyHeader(node, ResolveInitialHeaderContactName(node));
        ClearMessages();
        ScrollToBottom();
        ApplyPhoneLayoutRoots();
        EnsureMessageContentLayout();

        List<PhoneMessage> messages = node != null && node.messages != null ? node.messages : new List<PhoneMessage>();
        PhonePreviewSettings previewSettings = PreviewSettings;
        int limit = previewSettings != null ? previewSettings.editorPreviewMessageLimit : 24;
        int count = Mathf.Min(messages.Count, Mathf.Max(1, limit));
        for (int i = 0; i < count; i++)
        {
            ApplyHeaderForMessage(messages[i]);
            SpawnBubble(messages[i], animate: false, messageIndex: i);
        }

        RestoreMessageContentRectIfPreserved(_preservedMessageContentRect, reason);
        RecalculateLayout(reason);
        RestoreMessageContentRectIfPreserved(_preservedMessageContentRect, reason);
        AppLogger.Info(
            AppLogCategory.PhoneDialogue,
            nameof(PhoneDialogueUI),
            nameof(ShowStaticPreview),
            "Предпросмотр обновлён.",
            LogMetadata.Of("reason", reason, "messages", count, "node", node != null ? node.name : ""));
        return true;
    }

    public void BringToFrontForStory()
    {
        Transform ownerTransform = transform;
        if (ownerTransform != null && ownerTransform.parent != null)
            ownerTransform.SetAsLastSibling();

        GameObject root = ResolvePanel();
        Transform rootTransform = root != null ? root.transform : null;
        if (rootTransform != null && rootTransform != ownerTransform && rootTransform.parent != null)
            rootTransform.SetAsLastSibling();
    }

    public void AutoFillPhoneReferencesFromHierarchy()
    {
        EnsureSettings();
        PhoneReferences.AutoFillFrom(this);
        SyncLegacyFieldsFromReferences();
        HideMessageTemplates();
    }

    public void ConfigureFromStoryUserInterface(StoryUserInterface owner, string reason = "StoryUserInterface")
    {
        if (owner == null)
            return;

        EnsureSettings();
        _activeConfigurationOwner = owner;
        _activePhoneReferences = owner.PhoneReferences;
        _activeLayoutSettings = owner.PhoneLayoutSettings;
        _activePreviewSettings = owner.PhonePreviewSettings;
        EnsureSettings();
        PhoneReferences.phoneDialogueUI = this;
        PhoneReferences.AutoFillFrom(this);
        SyncLegacyFieldsFromReferences();
        HideMessageTemplates();
        AppLogger.Info(
            AppLogCategory.PhoneDialogue,
            nameof(PhoneDialogueUI),
            nameof(ConfigureFromStoryUserInterface),
            "PhoneDialogueUI получил конфигурацию из StoryUserInterface.",
            LogMetadata.Of(
                "owner", owner != null ? owner.name : "",
                "reason", reason,
                "phone", gameObject != null ? gameObject.name : ""));
    }

    void EnsureStoryUserInterfaceConfiguration(string reason)
    {
        if (_activeConfigurationOwner != null)
            return;

        StoryUserInterface owner = FindStoryUserInterfaceOwner();
        if (owner != null)
            ConfigureFromStoryUserInterface(owner, reason);
    }

    StoryUserInterface FindStoryUserInterfaceOwner()
    {
        StoryUserInterface[] owners = FindObjectsOfType<StoryUserInterface>(true);
        if (owners == null || owners.Length == 0)
            return null;

        for (int i = 0; i < owners.Length; i++)
        {
            StoryUserInterface owner = owners[i];
            if (owner != null &&
                owner.PhoneReferences != null &&
                owner.PhoneReferences.phoneDialogueUI == this)
            {
                return owner;
            }
        }

        for (int i = 0; i < owners.Length; i++)
        {
            StoryUserInterface owner = owners[i];
            if (owner != null && owner.ResolvePhoneDialogueUI() == this)
                return owner;
        }

        return owners.Length == 1 ? owners[0] : null;
    }

    public void CopySerializedConfigurationTo(
        PhoneDialogueUIReferences targetReferences,
        PhoneDialogueLayoutSettings targetLayout,
        PhonePreviewSettings targetPreview,
        bool overwrite)
    {
        EnsureSettings();
        if (targetReferences != null)
            targetReferences.CopyFrom(_phoneReferences, this, overwrite);
        if (targetLayout != null)
            targetLayout.CopyFrom(_layoutSettings, overwrite);
        if (targetPreview != null)
            targetPreview.CopyFrom(_previewSettings, overwrite);
    }

    public bool HasSerializedPhoneConfiguration()
    {
        EnsureSettings();
        return _phoneReferences != null && _phoneReferences.HasAnyReferences();
    }

    public void Hide()
    {
        StopPlayback(clearMessages: true, clearCallback: true);

        GameObject root = ResolvePanel();
        if (root != null)
            root.SetActive(false);

        AppLogger.Info(
            AppLogCategory.PhoneDialogue,
            nameof(PhoneDialogueUI),
            nameof(Hide),
            "Предпросмотр очищен.");
    }

    public int RecalculateLayout(string reason)
    {
        EnsureSettings();
        EnsureStoryUserInterfaceConfiguration(reason);
        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        bool shouldStickToBottom = layoutSettings != null && layoutSettings.scrollToBottom && ShouldStickToBottom();
        if (ArePhoneLayoutSettingsDisabled())
        {
            ResetTrackedPhoneLayoutOverrides();
            Canvas.ForceUpdateCanvases();
            return PhoneLayoutValidator.ValidateAndLog(this, reason, _activeNode);
        }

        ApplyMessageContentPreserveOverrides(nameof(RecalculateLayout));
        RectTransformLayoutSnapshot contentSnapshot = CaptureMessageContentRectIfPreserved();

        RectTransform content = ResolveMessageContent();
        if (content != null)
        {
            ApplyPhoneLayoutRoots();
            EnsureMessageContentLayout();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            ReapplySpawnedMessageOffsets(content);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            RestoreMessageContentRectIfPreserved(contentSnapshot, reason);
        }

        if (shouldStickToBottom)
            ScrollToBottom();
        if (ShouldPreserveMessageContentLayout())
            RestoreMessageContentRectIfPreserved(contentSnapshot, reason);

        return PhoneLayoutValidator.ValidateAndLog(this, reason, _activeNode);
    }

    void ReapplySpawnedMessageOffsets(RectTransform content)
    {
        if (content == null)
            return;

        if (ArePhoneLayoutSettingsDisabled())
        {
            ResetTrackedPhoneLayoutOverrides(content);
            return;
        }

        for (int i = 0; i < content.childCount; i++)
        {
            Transform child = content.GetChild(i);
            if (child == null)
                continue;

            PhoneDialoguePreviewMessageMarker marker = child.GetComponent<PhoneDialoguePreviewMessageMarker>();
            if (marker == null || marker.templateLayout == null)
                continue;

            GameObject bubble = child.gameObject;
            TMP_Text messageText = marker.templateReferences != null
                ? marker.templateReferences.FindMessageTextIn(bubble)
                : bubble.GetComponentInChildren<TMP_Text>(true);
            if (messageText != null)
                ApplyBubbleTextLayout(
                    messageText,
                    marker.templateLayout,
                    ShouldReserveTimeTextSpace(marker.templateLayout, marker.resolvedTimeText));

            TMP_Text senderNameText = marker.templateReferences != null
                ? marker.templateReferences.FindSenderNameTextIn(bubble)
                : null;
            if (senderNameText != null)
                ApplySenderNameState(
                    bubble,
                    marker.side,
                    marker.senderName,
                    marker.templateLayout,
                    senderNameText,
                    marker.templateReferences);

            TMP_Text timeText = marker.templateReferences != null
                ? marker.templateReferences.FindTimeTextIn(bubble)
                : null;
            if (timeText == null && marker.templateLayout.showTimeText)
                timeText = FindOrCreateTimeText(bubble, marker.templateReferences, marker.templateLayout);
            if (timeText != null)
                ApplyTimeTextState(bubble, timeText, marker.templateLayout, marker.resolvedTimeText);

            if (!marker.templateLayout.hideAvatar)
                ApplyAvatarOffset(bubble, marker.templateReferences, marker.templateLayout);

            Image attachment = marker.templateReferences != null
                ? marker.templateReferences.FindAttachmentImageIn(bubble)
                : null;
            if (attachment != null && attachment.gameObject.activeInHierarchy)
                ApplyTrackedOffset(attachment.rectTransform, marker.templateLayout.photoOffset, ref marker.appliedPhotoOffset);

            ApplyBubbleLayout(
                bubble,
                marker.side,
                marker.usesPhotoLayout,
                marker.hasAttachment,
                marker.templateLayout,
                marker.templateReferences);
        }
    }

    void ResetTrackedPhoneLayoutOverrides()
    {
        ResetPhoneRootLayoutOverrides(PhoneReferences);
        ResetTrackedHeaderContactLayout();
        ResetTrackedPhoneLayoutOverrides(ResolveMessageContent());
    }

    void ResetTrackedHeaderContactLayout()
    {
        if (_trackedHeaderContactRect == null)
            return;

        ApplyTrackedOffset(_trackedHeaderContactRect, Vector2.zero, ref _appliedHeaderContactOffset);
        ApplyTrackedSizeOffset(_trackedHeaderContactRect, Vector2.zero, ref _appliedHeaderContactSizeOffset);
    }

    void ResetTrackedPhoneLayoutOverrides(RectTransform content)
    {
        if (content == null)
            return;

        for (int i = 0; i < content.childCount; i++)
        {
            Transform child = content.GetChild(i);
            if (child == null)
                continue;

            GameObject bubble = child.gameObject;
            PhoneDialoguePreviewMessageMarker marker = bubble.GetComponent<PhoneDialoguePreviewMessageMarker>();
            if (marker == null)
                continue;

            RectTransform rowRect = bubble.GetComponent<RectTransform>();
            if (rowRect != null)
                ApplyTrackedOffset(rowRect, Vector2.zero, ref marker.appliedRowOffset);
            HorizontalOrVerticalLayoutGroup rowGroup = bubble.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (rowGroup != null)
            {
                rowGroup.padding = new RectOffset();
                rowGroup.spacing = 0f;
            }

            RectTransform containerRect = marker.templateReferences != null
                ? marker.templateReferences.FindContainerIn(bubble)
                : null;
            if (containerRect != null)
                ApplyTrackedOffset(containerRect, Vector2.zero, ref marker.appliedBubbleOffset);
            HorizontalOrVerticalLayoutGroup containerGroup = marker.templateReferences != null
                ? marker.templateReferences.FindContainerLayoutGroupIn(bubble)
                : containerRect != null ? containerRect.GetComponent<HorizontalOrVerticalLayoutGroup>() : null;
            if (containerGroup != null)
            {
                containerGroup.padding = new RectOffset();
                containerGroup.spacing = 0f;
            }

            TMP_Text messageText = marker.templateReferences != null
                ? marker.templateReferences.FindMessageTextIn(bubble)
                : bubble.GetComponentInChildren<TMP_Text>(true);
            if (messageText != null)
                messageText.margin = Vector4.zero;

            TMP_Text senderNameText = marker.templateReferences != null
                ? marker.templateReferences.FindSenderNameTextIn(bubble)
                : null;
            if (senderNameText != null && senderNameText.rectTransform != null)
            {
                senderNameText.margin = Vector4.zero;
                ApplyTrackedOffset(senderNameText.rectTransform, Vector2.zero, ref marker.appliedSenderNameOffset);
                ApplyTrackedSizeOffset(senderNameText.rectTransform, Vector2.zero, ref marker.appliedSenderNameSizeOffset);
            }

            TMP_Text timeText = marker.templateReferences != null
                ? marker.templateReferences.FindTimeTextIn(bubble)
                : null;
            if (timeText != null && timeText.rectTransform != null)
            {
                timeText.margin = Vector4.zero;
                ApplyTrackedOffset(timeText.rectTransform, Vector2.zero, ref marker.appliedTimeTextOffset);
                ApplyTrackedSizeOffset(timeText.rectTransform, Vector2.zero, ref marker.appliedTimeTextSizeOffset);
            }

            RectTransform avatarRect = ResolveAvatarOffsetTarget(bubble, marker.templateReferences);
            if (avatarRect != null)
                ApplyTrackedOffset(avatarRect, Vector2.zero, ref marker.appliedAvatarOffset);

            Image attachment = marker.templateReferences != null
                ? marker.templateReferences.FindAttachmentImageIn(bubble)
                : null;
            if (attachment != null && attachment.rectTransform != null)
                ApplyTrackedOffset(attachment.rectTransform, Vector2.zero, ref marker.appliedPhotoOffset);
        }
    }

    void ApplyPhoneLayoutRoots()
    {
        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        PhoneDialogueUIReferences references = PhoneReferences;
        if (layoutSettings == null || references == null)
            return;
        if (layoutSettings.disableAllPhoneLayoutSettings)
        {
            ResetPhoneRootLayoutOverrides(references);
            return;
        }

        RectTransform safeArea = references.safeArea;
        if (safeArea != null)
        {
            Vector4 padding = layoutSettings.safeAreaPadding;
            safeArea.offsetMin = new Vector2(padding.x, padding.w);
            safeArea.offsetMax = new Vector2(-padding.z, -padding.y);
        }
    }

    static void ResetPhoneRootLayoutOverrides(PhoneDialogueUIReferences references)
    {
        if (references == null)
            return;

        RectTransform safeArea = references.safeArea;
        if (safeArea != null)
        {
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
        }

        RectTransform content = references.messageContent;
        if (content == null)
            return;

        VerticalLayoutGroup vertical = content.GetComponent<VerticalLayoutGroup>();
        if (vertical != null)
        {
            vertical.padding = new RectOffset();
            vertical.spacing = 0f;
        }
    }

    IEnumerator PlayMessages(List<PhoneMessage> messages, float delay)
    {
        _isPlaying = true;

        foreach (var msg in messages)
        {
            if (msg == null)
                continue;

            ApplyHeaderForMessage(msg);

            bool shouldStickToBottom = ShouldStickToBottom();
            if (typingIndicator != null)
            {
                typingIndicator.SetActive(true);
                ScrollToBottomIfNeeded(shouldStickToBottom);
            }

            yield return new WaitForSeconds(delay);

            if (typingIndicator != null)
                typingIndicator.SetActive(false);

            shouldStickToBottom = shouldStickToBottom && ShouldStickToBottom();
            SpawnBubble(msg, animate: true, messageIndex: -1);
            ScrollToBottomIfNeeded(shouldStickToBottom);

            float postAppearDelay = ResolveMessagePostAppearDelay();
            if (postAppearDelay > 0f)
                yield return new WaitForSeconds(postAppearDelay);
        }

        _isPlaying = false;
        _playRoutine = null;
        RecalculateLayout("RuntimeMessagesComplete");

        if (tapToContinueText != null)
        {
            tapToContinueText.gameObject.SetActive(true);
            if (Application.isPlaying)
            {
                tapToContinueText.DOFade(0f, 0f);
                _tapHintTween = tapToContinueText.DOFade(1f, 0.5f).SetLoops(-1, LoopType.Yoyo);
            }
        }
    }

    void SpawnBubble(PhoneMessage msg, bool animate, int messageIndex)
    {
        RectTransform content = ResolveMessageContent();
        if (msg == null || content == null)
            return;

        bool isIncoming = msg.side == PhoneMessageSide.Incoming;
        bool hasAttachment = msg.attachment != null;
        bool usesPhotoLayout = UsesPhotoLayout(msg);
        PhoneMessageTemplateReferences templateReferences = PhoneReferences.ResolveTemplateReferences(msg.side, usesPhotoLayout);
        GameObject template = templateReferences != null
            ? templateReferences.ResolveRootObject()
            : null;
        if (template == null)
            template = isIncoming ? ResolveIncomingBubblePrefab() : ResolveOutgoingBubblePrefab();
        if (template == null)
        {
            ThrottledAppLogger.Warn(
                "PhoneDialogueMissingBubblePrefab:" + (isIncoming ? "incoming" : "outgoing"),
                AppLogCategory.PhoneDialogue,
                nameof(PhoneDialogueUI),
                nameof(SpawnBubble),
                "Не назначен шаблон SMS-баббла. Сообщение не будет отображено.",
                LogMetadata.Of("side", isIncoming ? "Incoming" : "Outgoing", "messageIndex", messageIndex));
            return;
        }

        GameObject bubble = Instantiate(template, content);
        bubble.name = "PhoneMessage_" + (messageIndex >= 0 ? messageIndex.ToString("00") : content.childCount.ToString("00"));
        PhoneMessageTemplateLayoutSettings templateLayout = ResolveTemplateLayout(msg.side, usesPhotoLayout, templateReferences);
        PhoneDialoguePreviewMessageMarker marker = bubble.GetComponent<PhoneDialoguePreviewMessageMarker>();
        if (marker == null)
            marker = bubble.AddComponent<PhoneDialoguePreviewMessageMarker>();
        string senderName = ResolveMessageSenderName(msg);
        marker.Configure(templateReferences, templateLayout, msg.side, senderName, hasAttachment, usesPhotoLayout);
        bubble.SetActive(true);

        ApplySenderName(bubble, msg, senderName, templateReferences);
        ApplyAvatarVisibility(bubble, templateReferences, templateLayout);

        TMP_Text textComp = templateReferences != null
            ? templateReferences.FindMessageTextIn(bubble)
            : bubble.GetComponentInChildren<TMP_Text>(true);
        TMP_Text timeTextComp = FindOrCreateTimeText(bubble, templateReferences, templateLayout);
        string resolvedTimeText;
        string bodyText = ResolveBodyText(
            msg,
            templateLayout != null && !ArePhoneLayoutSettingsDisabled(),
            out resolvedTimeText);
        marker.resolvedTimeText = resolvedTimeText;
        if (textComp != null)
        {
            textComp.text = DialogueVariableResolver.ResolveText(
                bodyText,
                CreatePhoneVariableContext());
            ApplyBubbleTextLayout(
                textComp,
                templateLayout,
                ShouldReserveTimeTextSpace(templateLayout, resolvedTimeText));
        }
        else
        {
            AppLogger.Error(
                AppLogCategory.PhoneDialogue,
                nameof(PhoneDialogueUI),
                nameof(SpawnBubble),
                "В шаблоне SMS-баббла не найден TMP_Text для текста сообщения.",
                null,
                LogMetadata.Of(
                    "side", isIncoming ? "Incoming" : "Outgoing",
                    "messageIndex", messageIndex,
                    "template", template != null ? template.name : ""),
                recoverable: true);
        }

        if (msg.attachment != null)
            ApplyAttachment(bubble, msg.attachment, templateReferences, ResolveTemplateLayout(msg.side, true, templateReferences));
        else
            HideAttachmentImages(bubble, templateReferences);

        ApplyTimeTextState(bubble, timeTextComp, templateLayout, resolvedTimeText);
        ApplyBubbleLayout(bubble, msg.side, usesPhotoLayout, hasAttachment, templateLayout, templateReferences);
        TMP_Text finalSenderNameText = templateReferences != null ? templateReferences.FindSenderNameTextIn(bubble) : null;
        ApplySenderNameState(bubble, msg.side, senderName, templateLayout, finalSenderNameText, templateReferences);
        ApplyTimeTextState(bubble, timeTextComp, templateLayout, resolvedTimeText);
        if (animate)
            StabilizeSpawnedMessageLayout(content);
        ApplyBubbleAnimation(bubble, isIncoming, animate);
    }

    void ApplyHeader(PhoneDialogueNode node, string contactNameOverride = null)
    {
        DialogueVariableContext variableContext = CreatePhoneVariableContext();
        TMP_Text contactText = ResolveContactNameText();
        string contactName = !string.IsNullOrWhiteSpace(contactNameOverride)
            ? contactNameOverride
            : node != null ? node.contactName : "";
        ApplyHeaderContactName(contactText, contactName, variableContext);

        Image avatar = ResolveContactAvatarImage();
        if (avatar != null)
        {
            Sprite avatarSprite = node != null && node.contactAvatar != null ? node.contactAvatar : PhoneReferences.defaultAvatarSprite;
            avatar.gameObject.SetActive(avatarSprite != null);
            if (avatarSprite != null)
                avatar.sprite = avatarSprite;
        }
    }

    string ResolveInitialHeaderContactName(PhoneDialogueNode node)
    {
        if (node == null)
            return "";

        if (node.headerContactMode != PhoneHeaderContactMode.CurrentIncomingSender)
            return node.contactName;

        PhoneMessage firstSenderMessage = FindFirstHeaderSenderMessage(node);
        string senderName = ResolveHeaderSenderName(firstSenderMessage);
        return !string.IsNullOrWhiteSpace(senderName) ? senderName : node.contactName;
    }

    PhoneMessage FindFirstHeaderSenderMessage(PhoneDialogueNode node)
    {
        if (node == null || node.messages == null)
            return null;

        for (int i = 0; i < node.messages.Count; i++)
        {
            PhoneMessage message = node.messages[i];
            if (!string.IsNullOrWhiteSpace(ResolveHeaderSenderName(message)))
                return message;
        }

        return null;
    }

    void ApplyHeaderForMessage(PhoneMessage msg)
    {
        if (_activeNode == null || msg == null)
            return;

        if (_activeNode.headerContactMode != PhoneHeaderContactMode.CurrentIncomingSender)
            return;

        string senderName = ResolveHeaderSenderName(msg);
        if (string.IsNullOrWhiteSpace(senderName))
            return;

        ApplyHeader(_activeNode, senderName);
    }

    string ResolveHeaderSenderName(PhoneMessage msg)
    {
        if (msg == null || msg.side != PhoneMessageSide.Incoming)
            return "";

        string senderName = ResolveMessageSenderName(msg);
        if (string.IsNullOrWhiteSpace(senderName))
            return "";

        DialogueVariableContext variableContext = CreatePhoneVariableContext();
        if (DialogueVariableResolver.IsPlayerSpeakerName(senderName, variableContext))
            return "";

        return senderName.Trim();
    }

    void ApplyHeaderContactName(TMP_Text contactText, string contactName, DialogueVariableContext variableContext)
    {
        if (contactText == null)
            return;

        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        bool layoutDisabled = ArePhoneLayoutSettingsDisabled();
        if (!layoutDisabled)
        {
            bool showContactName = layoutSettings == null || layoutSettings.showHeaderContactName;
            contactText.gameObject.SetActive(showContactName);
            if (!showContactName)
                return;
        }

        contactText.text = DialogueVariableResolver.ResolveText(
            contactName ?? "",
            variableContext);
        if (layoutDisabled)
            ResetTrackedHeaderContactLayout();
        else
            ApplyHeaderContactNameLayout(contactText, layoutSettings);
    }

    void ApplyHeaderContactNameLayout(TMP_Text contactText, PhoneDialogueLayoutSettings layoutSettings)
    {
        if (contactText == null || layoutSettings == null || layoutSettings.disableAllPhoneLayoutSettings)
            return;

        ApplyTextTypography(
            contactText,
            layoutSettings.headerContactNameFontSize,
            layoutSettings.overrideHeaderContactNameAutoSize,
            layoutSettings.headerContactNameAutoSize,
            layoutSettings.headerContactNameMinFontSize,
            layoutSettings.headerContactNameMaxFontSize,
            layoutSettings.headerContactNameLineSpacing);

        if (layoutSettings.headerContactNameMargin != Vector4.zero)
            contactText.margin = layoutSettings.headerContactNameMargin;

        RectTransform contactRect = contactText.rectTransform;
        if (contactRect == null)
            return;

        if (_trackedHeaderContactRect != contactRect)
        {
            if (_trackedHeaderContactRect != null)
            {
                ApplyTrackedOffset(_trackedHeaderContactRect, Vector2.zero, ref _appliedHeaderContactOffset);
                ApplyTrackedSizeOffset(_trackedHeaderContactRect, Vector2.zero, ref _appliedHeaderContactSizeOffset);
            }

            _trackedHeaderContactRect = contactRect;
            _appliedHeaderContactOffset = Vector2.zero;
            _appliedHeaderContactSizeOffset = Vector2.zero;
        }

        ApplyTrackedOffset(contactRect, layoutSettings.headerContactNameOffset, ref _appliedHeaderContactOffset);
        ApplyTrackedSizeOffset(contactRect, layoutSettings.headerContactNameSizeOffset, ref _appliedHeaderContactSizeOffset);
    }

    void ApplySenderName(
        GameObject bubble,
        PhoneMessage msg,
        string senderName,
        PhoneMessageTemplateReferences templateReferences)
    {
        if (bubble == null || templateReferences == null)
            return;

        PhoneMessageTemplateLayoutSettings templateLayout = ResolveTemplateLayout(msg.side, UsesPhotoLayout(msg), templateReferences);
        TMP_Text senderNameText = templateReferences.FindSenderNameTextIn(bubble);
        ApplySenderNameState(bubble, msg.side, senderName, templateLayout, senderNameText, templateReferences);
    }

    static bool UsesPhotoLayout(PhoneMessage msg)
    {
        return msg != null && (msg.attachment != null || msg.usePhotoLayout);
    }

    void ApplySenderNameState(
        GameObject bubble,
        PhoneMessageSide side,
        string explicitSenderName,
        PhoneMessageTemplateLayoutSettings templateLayout,
        TMP_Text senderNameText,
        PhoneMessageTemplateReferences templateReferences = null)
    {
        if (senderNameText == null)
            return;

        if (ArePhoneLayoutSettingsDisabled())
        {
            if (senderNameText.gameObject.activeSelf)
            {
                DialogueVariableContext disabledVariableContext = CreatePhoneVariableContext();
                string disabledSenderName = !string.IsNullOrWhiteSpace(explicitSenderName)
                    ? explicitSenderName
                    : ResolveFallbackSenderName(side);
                senderNameText.text = DialogueVariableResolver.ResolveText(disabledSenderName, disabledVariableContext);
            }
            return;
        }

        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        bool showSender = (layoutSettings != null && layoutSettings.showSenderNamesInBubbles) ||
                          (templateLayout != null && templateLayout.showSenderName);
        senderNameText.gameObject.SetActive(showSender);
        if (!showSender)
            return;

        DialogueVariableContext variableContext = CreatePhoneVariableContext();
        string senderName = !string.IsNullOrWhiteSpace(explicitSenderName)
            ? explicitSenderName
            : ResolveFallbackSenderName(side);
        senderNameText.text = DialogueVariableResolver.ResolveText(senderName, variableContext);
        ApplySenderNameTextLayout(bubble, senderNameText, templateLayout, templateReferences);
    }

    string ResolveMessageSenderName(PhoneMessage msg)
    {
        if (msg != null && !string.IsNullOrWhiteSpace(msg.senderName))
            return msg.senderName.Trim();

        return msg != null
            ? ResolveFallbackSenderName(msg.side)
            : "";
    }

    string ResolveFallbackSenderName(PhoneMessageSide side)
    {
        return side == PhoneMessageSide.Incoming
            ? (_activeNode != null ? _activeNode.contactName : "")
            : "{PlayerName}";
    }

    DialogueVariableContext CreatePhoneVariableContext()
    {
        return DialogueVariableContext.PhoneDialogue(
            nameof(PhoneDialogueUI),
            gameObject,
            ResolveActiveStoryId());
    }

    string ResolveActiveStoryId()
    {
        if (_activeNode != null && !string.IsNullOrWhiteSpace(_activeNode.previewStoryId))
            return _activeNode.previewStoryId;

        if (StoryManager.Instance != null && !string.IsNullOrWhiteSpace(StoryManager.Instance.CurrentStoryId))
            return StoryManager.Instance.CurrentStoryId;

        if (GameState.Instance != null && !string.IsNullOrWhiteSpace(GameState.Instance.CurrentStoryId))
            return GameState.Instance.CurrentStoryId;

        return "";
    }

    void ApplySenderNameTextLayout(
        GameObject bubble,
        TMP_Text senderNameText,
        PhoneMessageTemplateLayoutSettings templateLayout,
        PhoneMessageTemplateReferences templateReferences)
    {
        if (senderNameText == null || templateLayout == null || ArePhoneLayoutSettingsDisabled())
            return;

        ApplyTextTypography(
            senderNameText,
            templateLayout.senderNameFontSize,
            templateLayout.overrideSenderNameAutoSize,
            templateLayout.senderNameAutoSize,
            templateLayout.senderNameMinFontSize,
            templateLayout.senderNameMaxFontSize,
            templateLayout.senderNameLineSpacing);

        senderNameText.margin = templateLayout.senderNameMargin != Vector4.zero
            ? templateLayout.senderNameMargin
            : new Vector4(
                senderNameText.margin.x,
                senderNameText.margin.y,
                senderNameText.margin.z,
                templateLayout.senderNameBottomSpacing);

        RectTransform senderRect = senderNameText.rectTransform;
        if (senderRect == null)
            return;

        LayoutElement senderLayoutElement = senderRect.GetComponent<LayoutElement>();
        if (senderLayoutElement == null)
            senderLayoutElement = senderRect.gameObject.AddComponent<LayoutElement>();
        senderLayoutElement.ignoreLayout = true;

        PhoneDialoguePreviewMessageMarker marker = bubble != null
            ? bubble.GetComponent<PhoneDialoguePreviewMessageMarker>()
            : null;

        if (templateLayout.senderNameAnchor != PhoneSenderNameAnchor.Custom)
        {
            RectTransform anchorTarget = ResolveSenderNameAnchorTarget(
                bubble,
                templateReferences,
                templateLayout.senderNameRelativeTo);
            if (anchorTarget != null && senderRect.parent != anchorTarget)
                senderRect.SetParent(anchorTarget, false);

            ApplySenderNameAnchor(senderRect, templateLayout.senderNameAnchor, templateLayout.senderNameOffset);
            if (marker != null)
                marker.appliedSenderNameOffset = templateLayout.senderNameOffset;
        }
        else if (marker != null)
        {
            ApplyTrackedOffset(senderRect, templateLayout.senderNameOffset, ref marker.appliedSenderNameOffset);
        }
        else
        {
            senderRect.anchoredPosition += templateLayout.senderNameOffset;
        }

        senderRect.SetAsLastSibling();

        if (marker != null)
        {
            ApplyTrackedSizeOffset(senderRect, templateLayout.senderNameSizeOffset, ref marker.appliedSenderNameSizeOffset);
            return;
        }

        senderRect.sizeDelta += templateLayout.senderNameSizeOffset;
    }

    static RectTransform ResolveSenderNameAnchorTarget(
        GameObject bubble,
        PhoneMessageTemplateReferences templateReferences,
        PhoneSenderNameRelativeTo relativeTo)
    {
        if (bubble == null)
            return null;

        if (relativeTo == PhoneSenderNameRelativeTo.MessageRoot)
            return bubble.GetComponent<RectTransform>();

        RectTransform container = templateReferences != null
            ? templateReferences.FindContainerIn(bubble)
            : null;
        return container != null ? container : bubble.GetComponent<RectTransform>();
    }

    static void ApplySenderNameAnchor(
        RectTransform senderRect,
        PhoneSenderNameAnchor anchor,
        Vector2 offset)
    {
        if (senderRect == null)
            return;

        if (anchor == PhoneSenderNameAnchor.TopRight)
        {
            senderRect.anchorMin = new Vector2(1f, 1f);
            senderRect.anchorMax = new Vector2(1f, 1f);
            senderRect.pivot = new Vector2(1f, 1f);
        }
        else
        {
            senderRect.anchorMin = new Vector2(0f, 1f);
            senderRect.anchorMax = new Vector2(0f, 1f);
            senderRect.pivot = new Vector2(0f, 1f);
        }

        senderRect.anchoredPosition = offset;
    }

    TMP_Text FindOrCreateTimeText(
        GameObject bubble,
        PhoneMessageTemplateReferences templateReferences,
        PhoneMessageTemplateLayoutSettings templateLayout)
    {
        if (bubble == null || templateLayout == null)
            return null;

        TMP_Text timeText = templateReferences != null ? templateReferences.FindTimeTextIn(bubble) : null;
        if (ArePhoneLayoutSettingsDisabled())
            return timeText;

        if (timeText != null || !templateLayout.showTimeText)
            return timeText;

        RectTransform parentRect = templateReferences != null
            ? templateReferences.FindContainerIn(bubble)
            : null;
        Transform parent = parentRect != null ? parentRect : bubble.transform;
        GameObject timeObject = new GameObject("TimeText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        timeObject.transform.SetParent(parent, false);

        RectTransform rect = timeObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(96f, 20f);

        TextMeshProUGUI createdText = timeObject.GetComponent<TextMeshProUGUI>();
        createdText.text = "";
        createdText.fontSize = 14f;
        createdText.enableWordWrapping = false;
        createdText.overflowMode = TextOverflowModes.Overflow;
        createdText.alignment = TextAlignmentOptions.TopLeft;
        createdText.color = new Color(0.82f, 0.87f, 0.94f, 1f);
        createdText.raycastTarget = false;

        LayoutElement layoutElement = timeObject.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
        layoutElement.preferredWidth = 96f;
        layoutElement.preferredHeight = 20f;

        return createdText;
    }

    void ApplyTimeTextState(
        GameObject bubble,
        TMP_Text timeText,
        PhoneMessageTemplateLayoutSettings templateLayout,
        string resolvedTime)
    {
        if (timeText == null || templateLayout == null)
            return;

        if (ArePhoneLayoutSettingsDisabled())
        {
            if (!string.IsNullOrWhiteSpace(resolvedTime))
            {
                timeText.text = DialogueVariableResolver.ResolveText(
                    resolvedTime.Trim(),
                    CreatePhoneVariableContext());
            }
            return;
        }

        if (!templateLayout.showTimeText)
        {
            timeText.gameObject.SetActive(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(resolvedTime))
        {
            timeText.text = DialogueVariableResolver.ResolveText(
                resolvedTime.Trim(),
                CreatePhoneVariableContext());
        }

        bool hasVisibleTime = !string.IsNullOrWhiteSpace(timeText.text);
        timeText.gameObject.SetActive(hasVisibleTime);
        if (!hasVisibleTime)
            return;

        ApplyTimeTextLayout(bubble, timeText, templateLayout);
    }

    static string ResolveBodyText(PhoneMessage msg, bool canUseSeparateTimeText, out string resolvedTimeText)
    {
        resolvedTimeText = "";
        string rawText = msg != null ? msg.text ?? "" : "";
        string explicitTime = msg != null ? msg.timeText ?? "" : "";
        if (!string.IsNullOrWhiteSpace(explicitTime))
        {
            resolvedTimeText = explicitTime.Trim();
            return rawText;
        }

        if (canUseSeparateTimeText &&
            TrySplitLeadingTime(rawText, out string leadingTime, out string bodyText))
        {
            resolvedTimeText = leadingTime;
            return bodyText;
        }

        return rawText;
    }

    static bool TrySplitLeadingTime(string value, out string timeText, out string bodyText)
    {
        timeText = "";
        bodyText = value ?? "";
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        int newlineIndex = normalized.IndexOf('\n');
        if (newlineIndex <= 0)
            return false;

        string firstLine = normalized.Substring(0, newlineIndex).Trim();
        if (!LooksLikeStandaloneTime(firstLine))
            return false;

        string rest = normalized.Substring(newlineIndex + 1).TrimStart('\n');
        if (string.IsNullOrWhiteSpace(rest))
            return false;

        timeText = firstLine;
        bodyText = rest;
        return true;
    }

    static bool LooksLikeStandaloneTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string[] parts = value.Trim().Split(':', '.');
        if (parts.Length < 2 || parts.Length > 3)
            return false;

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length == 0)
                return false;

            if (i == 0 && part.Length > 2)
                return false;
            if (i > 0 && part.Length != 2)
                return false;

            for (int j = 0; j < part.Length; j++)
            {
                if (!char.IsDigit(part[j]))
                    return false;
            }
        }

        return true;
    }

    void ApplyTimeTextLayout(
        GameObject bubble,
        TMP_Text timeText,
        PhoneMessageTemplateLayoutSettings templateLayout)
    {
        if (timeText == null || templateLayout == null || ArePhoneLayoutSettingsDisabled())
            return;

        ApplyTextTypography(
            timeText,
            templateLayout.timeTextFontSize,
            templateLayout.overrideTimeTextAutoSize,
            templateLayout.timeTextAutoSize,
            templateLayout.timeTextMinFontSize,
            templateLayout.timeTextMaxFontSize,
            templateLayout.timeTextLineSpacing);

        if (templateLayout.timeTextMargin != Vector4.zero)
            timeText.margin = templateLayout.timeTextMargin;

        RectTransform timeRect = timeText.rectTransform;
        if (timeRect == null)
            return;

        LayoutElement timeLayoutElement = timeRect.GetComponent<LayoutElement>();
        if (timeLayoutElement == null)
            timeLayoutElement = timeRect.gameObject.AddComponent<LayoutElement>();
        timeLayoutElement.ignoreLayout = true;
        timeRect.SetAsLastSibling();

        PhoneDialoguePreviewMessageMarker marker = bubble != null
            ? bubble.GetComponent<PhoneDialoguePreviewMessageMarker>()
            : null;
        if (marker != null)
        {
            ApplyTrackedOffset(timeRect, templateLayout.timeTextOffset, ref marker.appliedTimeTextOffset);
            ApplyTrackedSizeOffset(timeRect, templateLayout.timeTextSizeOffset, ref marker.appliedTimeTextSizeOffset);
            return;
        }

        timeRect.anchoredPosition += templateLayout.timeTextOffset;
        timeRect.sizeDelta += templateLayout.timeTextSizeOffset;
    }

    PhoneMessageTemplateLayoutSettings ResolveTemplateLayout(
        PhoneMessageSide side,
        bool hasAttachment,
        PhoneMessageTemplateReferences templateReferences = null)
    {
        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        PhoneMessageTemplateLayoutSettings layout = layoutSettings != null
            ? layoutSettings.ResolveTemplateLayout(side, hasAttachment)
            : null;
        if (layout != null)
            return layout;

        if (templateReferences != null)
        {
            templateReferences.Ensure();
            if (templateReferences.layout != null)
                return templateReferences.layout;
        }

        return null;
    }

    void ApplyAvatarVisibility(
        GameObject bubble,
        PhoneMessageTemplateReferences templateReferences,
        PhoneMessageTemplateLayoutSettings templateLayout)
    {
        if (bubble == null)
            return;

        if (ArePhoneLayoutSettingsDisabled())
            return;

        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        bool hideAvatar = layoutSettings == null ||
                          layoutSettings.hideAvatarsInBubbles ||
                          templateLayout == null ||
                          templateLayout.hideAvatar;
        if (!hideAvatar)
        {
            ApplyAvatarOffset(bubble, templateReferences, templateLayout);
            return;
        }

        if (templateReferences != null)
        {
            RectTransform avatarCircle = templateReferences.avatarCircle != null
                ? FindInstanceComponent(templateReferences.root, bubble, templateReferences.avatarCircle)
                : null;
            Image avatarImage = templateReferences.avatarImage != null
                ? FindInstanceComponent(templateReferences.root, bubble, templateReferences.avatarImage)
                : null;

            if (avatarCircle != null)
                avatarCircle.gameObject.SetActive(false);
            if (avatarImage != null)
                avatarImage.gameObject.SetActive(false);
        }

        Transform[] children = bubble.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name.IndexOf("Avatar", StringComparison.OrdinalIgnoreCase) >= 0)
                child.gameObject.SetActive(false);
        }
    }

    void ApplyAvatarOffset(
        GameObject bubble,
        PhoneMessageTemplateReferences templateReferences,
        PhoneMessageTemplateLayoutSettings templateLayout)
    {
        if (bubble == null || templateLayout == null || templateLayout.avatarOffset == Vector2.zero || ArePhoneLayoutSettingsDisabled())
            return;

        RectTransform targetRect = ResolveAvatarOffsetTarget(bubble, templateReferences);
        PhoneDialoguePreviewMessageMarker marker = bubble.GetComponent<PhoneDialoguePreviewMessageMarker>();
        if (targetRect != null)
        {
            if (marker != null)
                ApplyTrackedOffset(targetRect, templateLayout.avatarOffset, ref marker.appliedAvatarOffset);
            else
                targetRect.anchoredPosition += templateLayout.avatarOffset;
            return;
        }

    }

    RectTransform ResolveAvatarOffsetTarget(GameObject bubble, PhoneMessageTemplateReferences templateReferences)
    {
        if (bubble == null)
            return null;

        RectTransform avatarCircle = templateReferences != null && templateReferences.avatarCircle != null
            ? FindInstanceComponent(templateReferences.root, bubble, templateReferences.avatarCircle)
            : null;
        if (avatarCircle != null)
            return avatarCircle;

        Image avatarImage = templateReferences != null && templateReferences.avatarImage != null
            ? FindInstanceComponent(templateReferences.root, bubble, templateReferences.avatarImage)
            : null;
        if (avatarImage != null)
            return avatarImage.rectTransform;

        Transform[] children = bubble.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child.name.IndexOf("Avatar", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            RectTransform rect = child as RectTransform;
            if (rect != null)
                return rect;
        }

        return null;
    }

    void ApplyAttachment(
        GameObject bubble,
        Sprite attachment,
        PhoneMessageTemplateReferences templateReferences,
        PhoneMessageTemplateLayoutSettings templateLayout)
    {
        Image referencedAttachment = templateReferences != null ? templateReferences.FindAttachmentImageIn(bubble) : null;
        if (referencedAttachment != null)
        {
            referencedAttachment.sprite = attachment;
            referencedAttachment.color = Color.white;
            referencedAttachment.gameObject.SetActive(true);
            RectTransform referencedRect = referencedAttachment.rectTransform;
            if (referencedRect != null && templateLayout != null && !ArePhoneLayoutSettingsDisabled())
            {
                referencedRect.sizeDelta = templateLayout.photoMessageSize;
                PhoneDialoguePreviewMessageMarker marker = bubble.GetComponent<PhoneDialoguePreviewMessageMarker>();
                if (marker != null)
                    ApplyTrackedOffset(referencedRect, templateLayout.photoOffset, ref marker.appliedPhotoOffset);
                else
                    referencedRect.anchoredPosition += templateLayout.photoOffset;
            }
            return;
        }

        Image[] images = bubble.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null)
                continue;

            if (img.gameObject.name.IndexOf("Attachment", StringComparison.OrdinalIgnoreCase) >= 0 ||
                img.gameObject.name.IndexOf("Photo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                img.sprite = attachment;
                img.color = Color.white;
                img.gameObject.SetActive(true);

                RectTransform rect = img.rectTransform;
                if (rect != null && templateLayout != null && !ArePhoneLayoutSettingsDisabled())
                {
                    rect.sizeDelta = templateLayout.photoMessageSize;
                    PhoneDialoguePreviewMessageMarker marker = bubble.GetComponent<PhoneDialoguePreviewMessageMarker>();
                    if (marker != null)
                        ApplyTrackedOffset(rect, templateLayout.photoOffset, ref marker.appliedPhotoOffset);
                    else
                        rect.anchoredPosition += templateLayout.photoOffset;
                }
                return;
            }
        }
    }

    void HideAttachmentImages(GameObject bubble, PhoneMessageTemplateReferences templateReferences)
    {
        if (bubble == null)
            return;

        Image referencedAttachment = templateReferences != null ? templateReferences.FindAttachmentImageIn(bubble) : null;
        if (referencedAttachment != null)
            referencedAttachment.gameObject.SetActive(false);

        Image[] images = bubble.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null)
                continue;

            if (img.gameObject.name.IndexOf("Attachment", StringComparison.OrdinalIgnoreCase) >= 0 ||
                img.gameObject.name.IndexOf("Photo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                img.gameObject.SetActive(false);
            }
        }
    }

    void ApplyBubbleLayout(
        GameObject bubble,
        PhoneMessageSide side,
        bool usesPhotoLayout,
        bool hasAttachment,
        PhoneMessageTemplateLayoutSettings templateLayout,
        PhoneMessageTemplateReferences templateReferences)
    {
        if (bubble == null || templateLayout == null || ArePhoneLayoutSettingsDisabled())
            return;

        PhoneDialoguePreviewMessageMarker marker = bubble.GetComponent<PhoneDialoguePreviewMessageMarker>();
        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        PhoneMessageTemplateLayoutSettings positionLayout = layoutSettings != null
            ? layoutSettings.ResolvePositionLayout(side, usesPhotoLayout, templateLayout)
            : templateLayout;
        if (positionLayout == null)
            positionLayout = templateLayout;

        float resolvedHorizontalOffset = ResolveHorizontalOffset(side, usesPhotoLayout, templateLayout);
        Vector2 resolvedLayoutOffset = positionLayout.rowPositionOffset +
                                       new Vector2(resolvedHorizontalOffset, 0f);
        Vector2 resolvedBubbleOffset = positionLayout.bubblePositionOffset +
                                       ResolveIncomingMessageRootVisualOffset(side, templateLayout);
        RectTransform containerRect = templateReferences != null
            ? templateReferences.FindContainerIn(bubble)
            : null;
        RectTransform rowRect = bubble.GetComponent<RectTransform>();
        HorizontalOrVerticalLayoutGroup rowGroup = EnsureMessageRowLayoutGroup(bubble, templateLayout);
        ConfigureMessageRootSpacerLayout(bubble, templateLayout.useMessageRootVerticalLayout);

        RectTransform viewport = PhoneReferences.ResolveViewport(this);
        float viewportWidth = viewport != null && viewport.rect.width > 0f ? viewport.rect.width : 390f;
        float maxWidth = Mathf.Max(templateLayout.minWidth, viewportWidth * templateLayout.maxWidthPercent);

        bool fullWidthRows = layoutSettings == null || layoutSettings.forceFullWidthMessageRows;
        LayoutElement rowLayoutElement = bubble.GetComponent<LayoutElement>();
        if (rowLayoutElement == null)
            rowLayoutElement = bubble.AddComponent<LayoutElement>();

        rowLayoutElement.flexibleWidth = fullWidthRows ? 1f : 0f;
        rowLayoutElement.minWidth = fullWidthRows ? viewportWidth : templateLayout.minWidth;
        rowLayoutElement.preferredWidth = fullWidthRows ? viewportWidth : rowLayoutElement.preferredWidth;

        TMP_Text text = templateReferences != null
            ? templateReferences.FindMessageTextIn(bubble)
            : bubble.GetComponentInChildren<TMP_Text>(true);
        GameObject bubbleBody = containerRect != null ? containerRect.gameObject : bubble;
        LayoutElement bodyLayoutElement = bubbleBody.GetComponent<LayoutElement>();
        if (bodyLayoutElement == null)
            bodyLayoutElement = bubbleBody.AddComponent<LayoutElement>();
        bodyLayoutElement.ignoreLayout = false;

        if (text != null)
        {
            bodyLayoutElement.flexibleWidth = 0f;
            bodyLayoutElement.minWidth = templateLayout.minWidth;
            float horizontalPadding = templateLayout.leftPadding + templateLayout.rightPadding;
            float verticalPadding = templateLayout.topPadding + templateLayout.bottomPadding + ResolveActiveTimeTextReserve(bubble, templateReferences, templateLayout);
            float containerHorizontalPadding = templateLayout.overrideContainerVerticalLayout
                ? templateLayout.containerPadding.x + templateLayout.containerPadding.z
                : 0f;
            float containerVerticalPadding = templateLayout.overrideContainerVerticalLayout
                ? templateLayout.containerPadding.y + templateLayout.containerPadding.w
                : 0f;
            float maxTextWidth = Mathf.Max(1f, maxWidth - horizontalPadding);
            Vector4 originalMargin = text.margin;
            text.margin = Vector4.zero;
            Vector2 naturalTextSize = text.GetPreferredValues(text.text);
            float resolvedTextWidth = Mathf.Clamp(naturalTextSize.x, 1f, maxTextWidth);
            Vector2 wrappedTextSize = text.GetPreferredValues(text.text, resolvedTextWidth, 0f);
            text.margin = originalMargin;
            float preferredWidth = Mathf.Clamp(
                wrappedTextSize.x + horizontalPadding,
                templateLayout.minWidth,
                maxWidth);
            float preferredHeight = wrappedTextSize.y + verticalPadding;
            if (hasAttachment)
            {
                preferredWidth = Mathf.Max(preferredWidth, templateLayout.photoMessageSize.x);
                preferredHeight = Mathf.Max(preferredHeight, templateLayout.photoMessageSize.y + verticalPadding);
            }
            bodyLayoutElement.preferredWidth = Mathf.Max(
                templateLayout.minWidth,
                preferredWidth + templateLayout.bubbleSizeOffset.x + containerHorizontalPadding);
            bodyLayoutElement.preferredHeight = Mathf.Max(
                1f,
                preferredHeight + templateLayout.bubbleSizeOffset.y + containerVerticalPadding);
            rowLayoutElement.minHeight = bodyLayoutElement.preferredHeight;
            rowLayoutElement.preferredHeight = bodyLayoutElement.preferredHeight;
            rowLayoutElement.flexibleHeight = 0f;

            if (containerRect != null)
            {
                EnsureRectGrowsDown(containerRect);
                containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bodyLayoutElement.preferredWidth);
                containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyLayoutElement.preferredHeight);
            }

            RectTransform textRect = text.rectTransform;
            if (textRect != null)
            {
                NormalizeMessageTextRect(textRect);
                ApplyBodyTextScale(textRect, templateLayout);
                LayoutElement textLayoutElement = textRect.GetComponent<LayoutElement>();
                if (textLayoutElement == null)
                    textLayoutElement = textRect.gameObject.AddComponent<LayoutElement>();

                textLayoutElement.ignoreLayout = false;
                textLayoutElement.flexibleWidth = 0f;
                textLayoutElement.flexibleHeight = 0f;
                float textPreferredWidth = Mathf.Max(1f, bodyLayoutElement.preferredWidth - containerHorizontalPadding);
                float textPreferredHeight = Mathf.Max(1f, bodyLayoutElement.preferredHeight - containerVerticalPadding);
                textLayoutElement.minWidth = Mathf.Min(templateLayout.minWidth, textPreferredWidth);
                textLayoutElement.preferredWidth = textPreferredWidth;
                textLayoutElement.preferredHeight = textPreferredHeight;
                textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textPreferredWidth);
                textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textPreferredHeight);
            }
        }

        ApplyContainerLayout(bubble, containerRect, templateLayout, templateReferences);
        ApplyBackgroundStretch(bubble, templateLayout, templateReferences);
        EnsureMessageTextDrawOrder(bubble, text, templateReferences);

        if (rowGroup != null)
        {
            rowGroup.enabled = true;
            RectOffset resolvedRowPadding;
            TextAnchor resolvedRowAlignment;
            if (templateLayout.useMessageRootVerticalLayout)
            {
                resolvedRowAlignment = templateLayout.messageRootVerticalLayoutChildAlignment;
                rowGroup.childAlignment = resolvedRowAlignment;
                rowGroup.spacing = templateLayout.messageRootVerticalLayoutSpacing;
                resolvedRowPadding = ToRectOffset(templateLayout.messageRootVerticalLayoutPadding);
                rowGroup.reverseArrangement = templateLayout.messageRootVerticalLayoutReverseArrangement;
                rowGroup.childControlWidth = templateLayout.messageRootVerticalLayoutControlChildWidth;
                rowGroup.childControlHeight = templateLayout.messageRootVerticalLayoutControlChildHeight;
                rowGroup.childScaleWidth = templateLayout.messageRootVerticalLayoutUseChildScaleWidth;
                rowGroup.childScaleHeight = templateLayout.messageRootVerticalLayoutUseChildScaleHeight;
                rowGroup.childForceExpandWidth = templateLayout.messageRootVerticalLayoutChildForceExpandWidth;
                rowGroup.childForceExpandHeight = templateLayout.messageRootVerticalLayoutChildForceExpandHeight;
            }
            else
            {
                resolvedRowAlignment = layoutSettings != null
                    ? layoutSettings.ResolveRowAlignment(side, usesPhotoLayout, templateLayout)
                    : templateLayout.rowAlignment;
                rowGroup.childAlignment = resolvedRowAlignment;
                rowGroup.spacing = 0f;
                resolvedRowPadding = ToRectOffset(positionLayout.rowPadding);
                resolvedRowPadding.bottom += Mathf.RoundToInt(positionLayout.verticalSpacing);
                rowGroup.childControlWidth = false;
                rowGroup.childControlHeight = false;
                rowGroup.childScaleWidth = false;
                rowGroup.childScaleHeight = false;
                rowGroup.childForceExpandWidth = false;
                rowGroup.childForceExpandHeight = false;
            }

            if (!templateLayout.useMessageRootVerticalLayout)
            {
                ApplyLayoutOffsetToRowPadding(
                    resolvedRowPadding,
                    resolvedRowAlignment,
                    resolvedLayoutOffset);
            }
            rowGroup.padding = resolvedRowPadding;
            float resolvedRowHeight = Mathf.Max(
                1f,
                bodyLayoutElement.preferredHeight + resolvedRowPadding.top + resolvedRowPadding.bottom);
            rowLayoutElement.minHeight = resolvedRowHeight;
            rowLayoutElement.preferredHeight = resolvedRowHeight;
            rowLayoutElement.flexibleHeight = 0f;
        }

        if (containerRect != null)
        {
            if (marker != null)
                ApplyTrackedOffset(containerRect, resolvedBubbleOffset, ref marker.appliedBubbleOffset);
            else
                containerRect.anchoredPosition += resolvedBubbleOffset;
        }

        if (rowRect != null)
        {
            Vector2 resolvedRowOffset = Vector2.zero;
            if (marker != null)
                ApplyTrackedOffset(rowRect, resolvedRowOffset, ref marker.appliedRowOffset);
        }
    }

    static void ApplyLayoutOffsetToRowPadding(RectOffset padding, TextAnchor alignment, Vector2 offset)
    {
        if (padding == null || offset == Vector2.zero)
            return;

        int x = Mathf.RoundToInt(offset.x);
        int y = Mathf.RoundToInt(offset.y);

        if (x != 0)
        {
            if (IsRightAligned(alignment))
                padding.right -= x;
            else if (IsCenterAligned(alignment))
            {
                padding.left += x;
                padding.right -= x;
            }
            else
            {
                padding.left += x;
            }
        }

        if (y != 0)
        {
            padding.top -= y;
            padding.bottom += y;
        }
    }

    static void EnsureRectGrowsDown(RectTransform rect)
    {
        if (rect == null || Mathf.Approximately(rect.pivot.y, 1f))
            return;

        Vector2 pivot = rect.pivot;
        Vector2 anchoredPosition = rect.anchoredPosition;
        anchoredPosition.y += (1f - pivot.y) * rect.rect.height;
        pivot.y = 1f;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
    }

    static bool IsRightAligned(TextAnchor alignment)
    {
        return alignment == TextAnchor.UpperRight ||
               alignment == TextAnchor.MiddleRight ||
               alignment == TextAnchor.LowerRight;
    }

    static bool IsCenterAligned(TextAnchor alignment)
    {
        return alignment == TextAnchor.UpperCenter ||
               alignment == TextAnchor.MiddleCenter ||
               alignment == TextAnchor.LowerCenter;
    }

    HorizontalOrVerticalLayoutGroup EnsureMessageRowLayoutGroup(
        GameObject bubble,
        PhoneMessageTemplateLayoutSettings templateLayout)
    {
        if (bubble == null)
            return null;

        if (templateLayout != null && templateLayout.useMessageRootVerticalLayout)
        {
            VerticalLayoutGroup vertical = bubble.GetComponent<VerticalLayoutGroup>();
            HorizontalLayoutGroup existingHorizontal = bubble.GetComponent<HorizontalLayoutGroup>();
            if (existingHorizontal != null)
                existingHorizontal.enabled = false;

            if (vertical != null)
            {
                vertical.enabled = true;
                return vertical;
            }

            HorizontalOrVerticalLayoutGroup existingVerticalModeGroup = bubble.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (existingVerticalModeGroup != null)
                existingVerticalModeGroup.enabled = false;

            return bubble.AddComponent<VerticalLayoutGroup>();
        }

        HorizontalLayoutGroup horizontal = bubble.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null)
        {
            horizontal.enabled = true;
            return horizontal;
        }

        HorizontalOrVerticalLayoutGroup existingFallbackGroup = bubble.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (existingFallbackGroup != null)
            existingFallbackGroup.enabled = false;

        return bubble.AddComponent<HorizontalLayoutGroup>();
    }

    static void ConfigureMessageRootSpacerLayout(GameObject bubble, bool useMessageRootVerticalLayout)
    {
        if (bubble == null)
            return;

        Transform root = bubble.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null || !IsMessageRootSpacer(child))
                continue;

            LayoutElement layoutElement = child.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = child.gameObject.AddComponent<LayoutElement>();

            layoutElement.ignoreLayout = useMessageRootVerticalLayout;
        }
    }

    static bool IsMessageRootSpacer(Transform child)
    {
        return child != null &&
               child.name.IndexOf("Spacer", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static Vector2 ResolveIncomingMessageRootVisualOffset(
        PhoneMessageSide side,
        PhoneMessageTemplateLayoutSettings templateLayout)
    {
        if (side != PhoneMessageSide.Incoming ||
            templateLayout == null ||
            !templateLayout.useMessageRootVerticalLayout)
        {
            return Vector2.zero;
        }

        Vector4 padding = templateLayout.messageRootVerticalLayoutPadding;
        return new Vector2(padding.x - padding.z, 0f);
    }

    static void NormalizeMessageTextRect(RectTransform textRect)
    {
        if (textRect == null)
            return;

        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.localScale = Vector3.one;
        textRect.localRotation = Quaternion.identity;
    }

    static void ApplyBodyTextScale(RectTransform textRect, PhoneMessageTemplateLayoutSettings templateLayout)
    {
        if (textRect == null || templateLayout == null)
            return;

        Vector3 scale = templateLayout.bodyTextScale;
        textRect.localScale = scale == Vector3.zero ? Vector3.one : scale;
    }

    void EnsureMessageTextDrawOrder(
        GameObject bubble,
        TMP_Text text,
        PhoneMessageTemplateReferences templateReferences)
    {
        if (bubble == null || text == null || templateReferences == null)
            return;

        RectTransform textRect = text.rectTransform;
        if (textRect != null)
            textRect.SetAsLastSibling();
    }

    void ApplyContainerLayout(
        GameObject bubble,
        RectTransform containerRect,
        PhoneMessageTemplateLayoutSettings templateLayout,
        PhoneMessageTemplateReferences templateReferences)
    {
        if (bubble == null || templateLayout == null || containerRect == null || ArePhoneLayoutSettingsDisabled())
            return;

        if (templateLayout.overrideContainerVerticalLayout)
        {
            HorizontalOrVerticalLayoutGroup configuredGroup = templateReferences != null
                ? templateReferences.FindContainerLayoutGroupIn(bubble)
                : null;
            VerticalLayoutGroup vertical = configuredGroup as VerticalLayoutGroup;
            if (vertical == null)
            {
                if (configuredGroup != null)
                    configuredGroup.enabled = false;
                vertical = containerRect.GetComponent<VerticalLayoutGroup>();
            }
            if (vertical == null)
                vertical = containerRect.gameObject.AddComponent<VerticalLayoutGroup>();

            vertical.enabled = true;
            vertical.padding = ToRectOffset(templateLayout.containerPadding);
            vertical.spacing = templateLayout.containerSpacing;
            vertical.childAlignment = templateLayout.containerChildAlignment;
            vertical.reverseArrangement = templateLayout.containerReverseArrangement;
            vertical.childControlWidth = templateLayout.containerControlChildWidth;
            vertical.childControlHeight = templateLayout.containerControlChildHeight;
            vertical.childScaleWidth = templateLayout.containerUseChildScaleWidth;
            vertical.childScaleHeight = templateLayout.containerUseChildScaleHeight;
            vertical.childForceExpandWidth = templateLayout.containerChildForceExpandWidth;
            vertical.childForceExpandHeight = templateLayout.containerChildForceExpandHeight;
        }

        if (!templateLayout.overrideContainerContentSizeFitter)
            return;

        ContentSizeFitter fitter = templateReferences != null
            ? templateReferences.FindContainerSizeFitterIn(bubble)
            : null;
        if (fitter == null)
            fitter = containerRect.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = containerRect.gameObject.AddComponent<ContentSizeFitter>();

        fitter.enabled = true;
        fitter.horizontalFit = templateLayout.containerHorizontalFit;
        fitter.verticalFit = templateLayout.containerVerticalFit;
    }

    void ApplyBackgroundStretch(
        GameObject bubble,
        PhoneMessageTemplateLayoutSettings templateLayout,
        PhoneMessageTemplateReferences templateReferences)
    {
        if (bubble == null || templateLayout == null || templateReferences == null || ArePhoneLayoutSettingsDisabled())
            return;

        Image background = templateReferences.FindBackgroundImageIn(bubble);
        if (background == null)
            return;

        RectTransform backgroundRect = background.rectTransform;
        if (backgroundRect == null)
            return;

        RectTransform containerRect = templateReferences.FindContainerIn(bubble);
        bool backgroundIsLayoutRoot = backgroundRect == containerRect || background.gameObject == bubble;

        if (templateLayout.backgroundSendToBack)
            backgroundRect.SetAsFirstSibling();

        if (templateLayout.backgroundIgnoreLayout && !backgroundIsLayoutRoot)
        {
            LayoutElement layoutElement = backgroundRect.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = backgroundRect.gameObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
        }

        if (!templateLayout.stretchBackground || backgroundIsLayoutRoot)
            return;

        Vector4 offsets = templateLayout.backgroundStretchOffsets;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.offsetMin = new Vector2(offsets.x, offsets.w);
        backgroundRect.offsetMax = new Vector2(-offsets.z, -offsets.y);
    }

    float ResolveHorizontalOffset(
        PhoneMessageSide side,
        bool hasAttachment,
        PhoneMessageTemplateLayoutSettings templateLayout)
    {
        if (ArePhoneLayoutSettingsDisabled())
            return 0f;

        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        return layoutSettings != null
            ? layoutSettings.ResolveHorizontalOffset(side, hasAttachment, templateLayout)
            : templateLayout != null ? templateLayout.horizontalOffset : 0f;
    }

    void ApplyBubbleTextLayout(TMP_Text text, PhoneMessageTemplateLayoutSettings templateLayout, bool reserveTimeTextSpace = false)
    {
        if (text == null || templateLayout == null || ArePhoneLayoutSettingsDisabled())
            return;

        ApplyTextTypography(
            text,
            templateLayout.bodyFontSize,
            templateLayout.overrideBodyAutoSize,
            templateLayout.bodyAutoSize,
            templateLayout.bodyMinFontSize,
            templateLayout.bodyMaxFontSize,
            templateLayout.bodyLineSpacing);
        ApplyBodyTextScale(text.rectTransform, templateLayout);

        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.margin = new Vector4(
            templateLayout.leftPadding + templateLayout.textOffsetInsideBubble.x,
            templateLayout.topPadding + templateLayout.textOffsetInsideBubble.y + (reserveTimeTextSpace ? ResolveTimeTextReserve(templateLayout) : 0f),
            templateLayout.rightPadding,
            templateLayout.bottomPadding);
    }

    static bool ShouldReserveTimeTextSpace(PhoneMessageTemplateLayoutSettings templateLayout, string resolvedTimeText)
    {
        return templateLayout != null &&
               templateLayout.showTimeText &&
               !string.IsNullOrWhiteSpace(resolvedTimeText);
    }

    static float ResolveActiveTimeTextReserve(
        GameObject bubble,
        PhoneMessageTemplateReferences templateReferences,
        PhoneMessageTemplateLayoutSettings templateLayout)
    {
        if (bubble == null || templateLayout == null || !templateLayout.showTimeText)
            return 0f;

        TMP_Text timeText = templateReferences != null
            ? templateReferences.FindTimeTextIn(bubble)
            : null;
        if (timeText == null)
            return 0f;

        return timeText.gameObject.activeSelf ? ResolveTimeTextReserve(templateLayout) : 0f;
    }

    static float ResolveTimeTextReserve(PhoneMessageTemplateLayoutSettings templateLayout)
    {
        if (templateLayout == null)
            return 0f;

        float fontSize = templateLayout.timeTextFontSize > 0f ? templateLayout.timeTextFontSize : 14f;
        if (templateLayout.timeTextMaxFontSize > 0f)
            fontSize = Mathf.Max(fontSize, templateLayout.timeTextMaxFontSize);
        if (templateLayout.timeTextMinFontSize > 0f)
            fontSize = Mathf.Max(fontSize, templateLayout.timeTextMinFontSize);

        return Mathf.Max(16f, fontSize + Mathf.Max(0f, templateLayout.timeTextSizeOffset.y) + 4f);
    }

    static void ApplyTextTypography(
        TMP_Text text,
        float fontSize,
        bool overrideAutoSize,
        bool autoSize,
        float minFontSize,
        float maxFontSize,
        float lineSpacing)
    {
        if (text == null)
            return;

        if (fontSize > 0f)
            text.fontSize = Mathf.Max(1f, fontSize);

        if (overrideAutoSize)
            text.enableAutoSizing = autoSize;

        if (minFontSize > 0f || maxFontSize > 0f)
        {
            float nextMin = minFontSize > 0f ? minFontSize : text.fontSizeMin;
            float nextMax = maxFontSize > 0f ? maxFontSize : text.fontSizeMax;

            if (nextMin <= 0f)
                nextMin = Mathf.Max(1f, text.fontSize);
            if (nextMax <= 0f)
                nextMax = Mathf.Max(nextMin, text.fontSize);

            text.fontSizeMin = Mathf.Max(1f, nextMin);
            text.fontSizeMax = Mathf.Max(text.fontSizeMin, nextMax);
        }

        if (fontSize > 0f && text.enableAutoSizing && text.fontSizeMax < text.fontSize)
            text.fontSizeMax = text.fontSize;

        if (!Mathf.Approximately(lineSpacing, 0f))
            text.lineSpacing = lineSpacing;
    }

    static RectOffset ToRectOffset(Vector4 padding)
    {
        return new RectOffset(
            Mathf.RoundToInt(padding.x),
            Mathf.RoundToInt(padding.z),
            Mathf.RoundToInt(padding.y),
            Mathf.RoundToInt(padding.w));
    }

    static void ApplyTrackedOffset(RectTransform rect, Vector2 targetOffset, ref Vector2 appliedOffset)
    {
        if (rect == null)
            return;

        rect.anchoredPosition -= appliedOffset;
        rect.anchoredPosition += targetOffset;
        appliedOffset = targetOffset;
    }

    static void ApplyTrackedSizeOffset(RectTransform rect, Vector2 targetOffset, ref Vector2 appliedOffset)
    {
        if (rect == null)
            return;

        rect.sizeDelta -= appliedOffset;
        rect.sizeDelta += targetOffset;
        appliedOffset = targetOffset;
    }

    void EnsureMessageContentLayout()
    {
        if (ArePhoneLayoutSettingsDisabled())
            return;

        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        if (layoutSettings != null && !layoutSettings.enforceContentVerticalLayout)
            return;

        RectTransform content = ResolveMessageContent();
        if (content == null)
            return;

        VerticalLayoutGroup vertical = content.GetComponent<VerticalLayoutGroup>();
        if (vertical == null)
            vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
        if (vertical != null)
        {
            vertical.enabled = true;
            vertical.childAlignment = TextAnchor.UpperLeft;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            if (layoutSettings != null)
            {
                vertical.spacing = layoutSettings.messageVerticalSpacing;
                vertical.padding = ToRectOffset(layoutSettings.messageContentPadding);
            }
        }

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            if (layoutSettings != null &&
                layoutSettings.preserveMessageContentLayout &&
                layoutSettings.disableMessageContentSizeFitterWhenPreserved)
            {
                fitter.enabled = false;
            }
            else
            {
                fitter.enabled = true;
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }
    }

    bool ShouldPreserveMessageContentLayout()
    {
        if (ArePhoneLayoutSettingsDisabled())
            return false;

        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        return layoutSettings != null && layoutSettings.preserveMessageContentLayout;
    }

    void ApplyMessageContentPreserveOverrides(string reason)
    {
        if (ArePhoneLayoutSettingsDisabled())
            return;

        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        if (layoutSettings == null ||
            !layoutSettings.preserveMessageContentLayout ||
            !layoutSettings.disableMessageContentSizeFitterWhenPreserved)
        {
            return;
        }

        RectTransform content = ResolveMessageContent();
        if (content == null)
            return;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null || !fitter.enabled)
            return;

        fitter.enabled = false;
        AppLogger.DebugLog(
            AppLogCategory.PhoneDialogue,
            nameof(PhoneDialogueUI),
            nameof(ApplyMessageContentPreserveOverrides),
            "ContentSizeFitter телефона отключён из-за включённого Content override.",
            LogMetadata.Of("reason", reason, "content", content.name));
    }

    RectTransformLayoutSnapshot CaptureMessageContentRectIfPreserved()
    {
        if (!ShouldPreserveMessageContentLayout())
            return default;

        return RectTransformLayoutSnapshot.Capture(ResolveMessageContent());
    }

    void RestoreMessageContentRectIfPreserved(RectTransformLayoutSnapshot snapshot, string reason)
    {
        if (!ShouldPreserveMessageContentLayout())
            return;

        if (!snapshot.Restore())
        {
            _preservedMessageContentRect = CaptureMessageContentRectIfPreserved();
            snapshot = _preservedMessageContentRect;
            if (!snapshot.Restore())
                return;
        }

        AppLogger.DebugLog(
            AppLogCategory.PhoneDialogue,
            nameof(PhoneDialogueUI),
            nameof(RestoreMessageContentRectIfPreserved),
            "Content RectTransform телефона восстановлен из override.",
            LogMetadata.Of("reason", reason, "content", snapshot.rect != null ? snapshot.rect.name : ""));
    }

    static T FindInstanceComponent<T>(GameObject templateRoot, GameObject instanceRoot, T templateComponent)
        where T : Component
    {
        if (templateRoot == null || instanceRoot == null || templateComponent == null)
            return null;

        string path = BuildTransformPath(templateRoot.transform, templateComponent.transform);
        Transform target = string.IsNullOrEmpty(path) ? instanceRoot.transform : instanceRoot.transform.Find(path);
        return target != null ? target.GetComponent<T>() : null;
    }

    static string BuildTransformPath(Transform root, Transform target)
    {
        if (root == null || target == null || root == target)
            return "";

        var parts = new Stack<string>();
        Transform cursor = target;
        while (cursor != null && cursor != root)
        {
            parts.Push(cursor.name);
            cursor = cursor.parent;
        }

        return cursor == root ? string.Join("/", parts.ToArray()) : "";
    }

    void ApplyBubbleAnimation(GameObject bubble, bool isIncoming, bool animate)
    {
        if (bubble == null)
            return;

        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        if (layoutSettings != null)
            layoutSettings.Normalize();

        PhoneMessageAppearAnimation animationMode = layoutSettings != null
            ? layoutSettings.messageAppearAnimation
            : PhoneMessageAppearAnimation.Fade;
        float duration = layoutSettings != null && layoutSettings.messageAppearDuration > 0f
            ? layoutSettings.messageAppearDuration
            : bubbleFadeIn;
        bool canAnimate = animate &&
                          Application.isPlaying &&
                          DOTween.instance != null &&
                          animationMode != PhoneMessageAppearAnimation.None &&
                          duration > 0f;

        CanvasGroup canvasGroup = bubble.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = bubble.AddComponent<CanvasGroup>();
        canvasGroup.DOKill();

        RectTransform animationTarget = ResolveBubbleAnimationTarget(bubble);
        if (animationTarget != null)
            animationTarget.DOKill();

        if (!canAnimate)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        bool useFade = UsesFadeAnimation(animationMode);
        bool useSlide = UsesSlideAnimation(animationMode);
        bool useScale = UsesScaleAnimation(animationMode);
        if (!useFade && animationTarget == null)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        canvasGroup.alpha = useFade ? 0f : 1f;
        Sequence sequence = DOTween.Sequence();
        Ease ease = layoutSettings != null ? layoutSettings.messageAppearEase : Ease.OutCubic;
        if (ease == Ease.Unset)
            ease = Ease.OutCubic;

        if (useFade)
            sequence.Join(canvasGroup.DOFade(1f, duration).SetEase(ease));

        Vector2 originalPosition = Vector2.zero;
        Vector3 originalScale = Vector3.one;
        if (animationTarget != null)
        {
            originalPosition = animationTarget.anchoredPosition;
            originalScale = animationTarget.localScale;

            if (useSlide)
            {
                Vector2 slideOffset = ResolveMessageSlideOffset(layoutSettings, isIncoming);
                animationTarget.anchoredPosition = originalPosition + slideOffset;
                sequence.Join(animationTarget.DOAnchorPos(originalPosition, duration).SetEase(ease));
            }

            if (useScale)
            {
                float scaleFrom = layoutSettings != null ? layoutSettings.messageAppearScaleFrom : 0.98f;
                scaleFrom = Mathf.Max(0.01f, scaleFrom);
                animationTarget.localScale = new Vector3(
                    originalScale.x * scaleFrom,
                    originalScale.y * scaleFrom,
                    originalScale.z);
                sequence.Join(animationTarget.DOScale(originalScale, duration).SetEase(ease));
            }
        }

        sequence.OnKill(() =>
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
            if (animationTarget != null)
            {
                animationTarget.anchoredPosition = originalPosition;
                animationTarget.localScale = originalScale;
            }
        });
    }

    static void StabilizeSpawnedMessageLayout(RectTransform content)
    {
        if (content == null)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
    }

    float ResolveMessagePostAppearDelay()
    {
        PhoneDialogueLayoutSettings layoutSettings = LayoutSettings;
        if (layoutSettings == null)
            return 0.15f;

        layoutSettings.Normalize();
        float delay = layoutSettings.messagePostAppearDelay;
        if (layoutSettings.messageAppearAnimation != PhoneMessageAppearAnimation.None &&
            layoutSettings.messageAppearDuration > 0f)
        {
            delay += layoutSettings.messageAppearDuration;
        }

        return Mathf.Max(0f, delay);
    }

    RectTransform ResolveBubbleAnimationTarget(GameObject bubble)
    {
        if (bubble == null)
            return null;

        PhoneDialoguePreviewMessageMarker marker = bubble.GetComponent<PhoneDialoguePreviewMessageMarker>();
        RectTransform container = marker != null && marker.templateReferences != null
            ? marker.templateReferences.FindContainerIn(bubble)
            : null;
        return container;
    }

    static Vector2 ResolveMessageSlideOffset(PhoneDialogueLayoutSettings layoutSettings, bool isIncoming)
    {
        Vector2 offset = layoutSettings != null
            ? layoutSettings.messageAppearSlideOffset
            : new Vector2(22f, 0f);
        offset.x = Mathf.Abs(offset.x) * (isIncoming ? -1f : 1f);
        return offset;
    }

    static bool UsesFadeAnimation(PhoneMessageAppearAnimation mode)
    {
        return mode == PhoneMessageAppearAnimation.Fade ||
               mode == PhoneMessageAppearAnimation.FadeAndSlide ||
               mode == PhoneMessageAppearAnimation.FadeAndScale ||
               mode == PhoneMessageAppearAnimation.FadeSlideAndScale;
    }

    static bool UsesSlideAnimation(PhoneMessageAppearAnimation mode)
    {
        return mode == PhoneMessageAppearAnimation.Slide ||
               mode == PhoneMessageAppearAnimation.FadeAndSlide ||
               mode == PhoneMessageAppearAnimation.SlideAndScale ||
               mode == PhoneMessageAppearAnimation.FadeSlideAndScale;
    }

    static bool UsesScaleAnimation(PhoneMessageAppearAnimation mode)
    {
        return mode == PhoneMessageAppearAnimation.Scale ||
               mode == PhoneMessageAppearAnimation.FadeAndScale ||
               mode == PhoneMessageAppearAnimation.SlideAndScale ||
               mode == PhoneMessageAppearAnimation.FadeSlideAndScale;
    }

    void OnTap()
    {
        if (_isPlaying || _tapReceived)
            return;

        _tapReceived = true;
        Complete();
    }

    void ClearMessages()
    {
        RectTransform content = ResolveMessageContent();
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child == null || IsTemplateObject(child.gameObject))
                continue;

            PhoneDialogueTweenCleanup.KillHierarchy(child.gameObject);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    void ScrollToBottom()
    {
        RectTransform content = ResolveMessageContent();
        if (content == null)
            return;

        Canvas.ForceUpdateCanvases();
        ScrollRect scrollRect = PhoneReferences.ResolveScrollRect(this);
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    void ScrollToBottomIfNeeded(bool shouldScroll)
    {
        if (shouldScroll)
            ScrollToBottom();
    }

    bool ShouldStickToBottom()
    {
        ScrollRect scrollRect = PhoneReferences.ResolveScrollRect(this);
        if (scrollRect == null)
            return true;

        RectTransform content = scrollRect.content != null ? scrollRect.content : ResolveMessageContent();
        RectTransform viewport = scrollRect.viewport;
        if (content == null || viewport == null)
            return scrollRect.verticalNormalizedPosition <= ScrollBottomStickEpsilon;

        if (content.rect.height <= viewport.rect.height + 1f)
            return true;

        return scrollRect.verticalNormalizedPosition <= ScrollBottomStickEpsilon;
    }

    void Complete()
    {
        Action callback = _onComplete;
        StopPlayback(clearMessages: false, clearCallback: true);

        GameObject root = ResolvePanel();
        if (root != null)
            root.SetActive(false);

        AppLogger.Info(
            AppLogCategory.PhoneDialogue,
            nameof(PhoneDialogueUI),
            nameof(Complete),
            "Runtime-предпросмотр телефона завершён.");
        callback?.Invoke();
    }

    void StopPlayback(bool clearMessages, bool clearCallback)
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        if (tapToContinueText != null)
        {
            if (DOTween.instance != null)
                tapToContinueText.DOKill();
            tapToContinueText.gameObject.SetActive(false);
        }

        if (_tapHintTween != null)
            _tapHintTween.Kill();
        _tapHintTween = null;

        if (typingIndicator != null)
            typingIndicator.SetActive(false);

        PhoneDialogueTweenCleanup.KillHierarchy(ResolvePanel());

        _isPlaying = false;
        _tapReceived = false;

        if (clearMessages)
            ClearMessages();
        if (clearCallback)
            _onComplete = null;
    }

    void EnsureSettings()
    {
        if (_phoneReferences == null)
            _phoneReferences = new PhoneDialogueUIReferences();
        if (_layoutSettings == null)
            _layoutSettings = new PhoneDialogueLayoutSettings();
        if (_previewSettings == null)
            _previewSettings = new PhonePreviewSettings();

        _phoneReferences.Ensure();
        _layoutSettings.Normalize();
        _previewSettings.Normalize();
        if (_activePhoneReferences != null)
            _activePhoneReferences.Ensure();
        if (_activeLayoutSettings != null)
            _activeLayoutSettings.Normalize();
        if (_activePreviewSettings != null)
            _activePreviewSettings.Normalize();
    }

    void SyncLegacyFieldsFromReferences()
    {
        PhoneDialogueUIReferences references = PhoneReferences;
        if (references.previewRoot != null)
            panel = references.previewRoot;
        TMP_Text resolvedHeaderContactText = ResolveContactNameText();
        if (resolvedHeaderContactText != null)
            contactNameText = resolvedHeaderContactText;
        if (references.headerContactAvatarImage != null)
            contactAvatarImage = references.headerContactAvatarImage;
        if (references.messageContent != null)
            messagesContainer = references.messageContent;
        GameObject incomingTemplate = references.ResolveIncomingBubbleTemplate(this);
        if (incomingTemplate != null)
            incomingBubblePrefab = incomingTemplate;
        GameObject outgoingTemplate = references.ResolveOutgoingBubbleTemplate(this);
        if (outgoingTemplate != null)
            outgoingBubblePrefab = outgoingTemplate;
    }

    GameObject ResolvePanel()
    {
        EnsureSettings();
        return PhoneReferences.ResolveRoot(this);
    }

    TMP_Text ResolveContactNameText()
    {
        EnsureSettings();
        TMP_Text configured = PhoneReferences.ResolveContactNameText(this);
        if (IsHeaderContactText(configured))
            return configured;

        TMP_Text headerText = FindHeaderContactNameText();
        return headerText != null ? headerText : configured;
    }

    bool IsHeaderContactText(TMP_Text text)
    {
        if (text == null)
            return false;

        RectTransform header = PhoneReferences.header;
        if (header == null)
            header = FindRectTransformByName(transform, "Header");

        return header == null || IsTransformChildOf(text.transform, header);
    }

    TMP_Text FindHeaderContactNameText()
    {
        RectTransform header = PhoneReferences.header;
        if (header == null)
            header = FindRectTransformByName(transform, "Header");

        if (header == null)
            return null;

        TMP_Text directText = header.GetComponent<TMP_Text>();
        if (directText != null)
            return directText;

        TMP_Text[] texts = header.GetComponentsInChildren<TMP_Text>(true);
        if (texts == null || texts.Length == 0)
            return null;

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate != null && candidate.name.IndexOf("contact", StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate != null && candidate.name.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;
        }

        return texts[0];
    }

    static bool IsTransformChildOf(Transform child, Transform parent)
    {
        if (child == null || parent == null)
            return false;

        Transform current = child;
        while (current != null)
        {
            if (current == parent)
                return true;
            current = current.parent;
        }

        return false;
    }

    static RectTransform FindRectTransformByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
            return null;

        if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
            return root as RectTransform;

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform found = FindRectTransformByName(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    Image ResolveContactAvatarImage()
    {
        EnsureSettings();
        return PhoneReferences.ResolveContactAvatarImage(this);
    }

    RectTransform ResolveMessageContent()
    {
        EnsureSettings();
        return PhoneReferences.ResolveMessageContent(this);
    }

    GameObject ResolveIncomingBubblePrefab()
    {
        EnsureSettings();
        return PhoneReferences.ResolveIncomingBubbleTemplate(this);
    }

    GameObject ResolveOutgoingBubblePrefab()
    {
        EnsureSettings();
        return PhoneReferences.ResolveOutgoingBubbleTemplate(this);
    }

    bool IsTemplateObject(GameObject value)
    {
        return value != null &&
               (value == ResolveIncomingBubblePrefab() ||
                value == ResolveOutgoingBubblePrefab() ||
                value == PhoneReferences.ResolvePhotoBubbleTemplate(this));
    }

    void HideMessageTemplates()
    {
        HideTemplateIfSceneObject(ResolveIncomingBubblePrefab());
        HideTemplateIfSceneObject(ResolveOutgoingBubblePrefab());
        HideTemplateIfSceneObject(PhoneReferences.ResolvePhotoBubbleTemplate(this));
    }

    static void HideTemplateIfSceneObject(GameObject template)
    {
        if (template == null)
            return;

        if (!template.scene.IsValid())
            return;

        if (template.activeSelf)
            template.SetActive(false);
    }

    void LogValidation(PhonePreviewValidationResult validation, string operation)
    {
        if (validation == null)
            return;

        for (int i = 0; i < validation.Warnings.Count; i++)
        {
            ThrottledAppLogger.Warn(
                "PhoneValidationWarning:" + validation.Warnings[i],
                AppLogCategory.PhoneDialogue,
                nameof(PhoneDialogueUI),
                operation,
                validation.Warnings[i],
                LogMetadata.Of("object", gameObject != null ? gameObject.name : ""));
        }

        for (int i = 0; i < validation.Errors.Count; i++)
        {
            AppLogger.Error(
                AppLogCategory.PhoneDialogue,
                nameof(PhoneDialogueUI),
                operation,
                validation.Errors[i],
                null,
                LogMetadata.Of("object", gameObject != null ? gameObject.name : ""),
                recoverable: true);
        }
    }
}
