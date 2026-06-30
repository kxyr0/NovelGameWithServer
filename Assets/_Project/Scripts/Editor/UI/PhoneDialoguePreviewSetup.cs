#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PhoneDialoguePreviewSetup
{
    const string PhoneUiName = "PhoneDialogueUI";
    const string PhoneSpritePath = "Assets/_MyProject/Art/\u041F\u0440\u0438\u0432\u044B\u0447\u043A\u0430 \u043F\u0440\u0438\u0442\u0432\u043E\u0440\u044F\u0442\u044C\u0441\u044F/\u0424\u043E\u043D\u044B/Phone.png";
    const string BubbleSpritePath = "Assets/_MyProject/Art/\u041F\u0440\u0438\u0432\u044B\u0447\u043A\u0430 \u043F\u0440\u0438\u0442\u0432\u043E\u0440\u044F\u0442\u044C\u0441\u044F/\u0424\u043E\u043D\u044B/sms_bubble.png";

    public static PhoneDialogueUI FindScenePhoneUi()
    {
        return FindSceneObject<PhoneDialogueUI>();
    }

    public static StoryUserInterface FindSceneStoryUserInterface()
    {
        return FindSceneObject<StoryUserInterface>();
    }

    public static bool IsAssignedToStoryManager(PhoneDialogueUI phoneUi)
    {
        if (phoneUi == null)
            return false;

        StoryManager storyManager = FindSceneObject<StoryManager>();
        if (storyManager == null)
            return false;

        SerializedObject serializedStoryManager = new SerializedObject(storyManager);
        SerializedProperty property = serializedStoryManager.FindProperty("phoneDialogueUI");
        return property != null && property.objectReferenceValue == phoneUi;
    }

    public static void AssignToStoryManager(PhoneDialogueUI phoneUi)
    {
        if (phoneUi == null)
            return;

        StoryManager storyManager = FindSceneObject<StoryManager>();
        if (storyManager == null)
        {
            Debug.LogWarning("PhoneDialoguePreviewSetup: StoryManager was not found in the open scene.");
            return;
        }

        Undo.RecordObject(storyManager, "Assign PhoneDialogueUI");
        SerializedObject serializedStoryManager = new SerializedObject(storyManager);
        SerializedProperty property = serializedStoryManager.FindProperty("phoneDialogueUI");
        if (property == null)
        {
            Debug.LogWarning("PhoneDialoguePreviewSetup: StoryManager.phoneDialogueUI was not found.");
            return;
        }

        property.objectReferenceValue = phoneUi;
        serializedStoryManager.ApplyModifiedProperties();
        EditorUtility.SetDirty(storyManager);
        MarkSceneDirty(phoneUi.gameObject);
    }

    public static PhoneDialogueUI CreateOrConfigureInOpenScene()
    {
        Canvas canvas = FindOrCreateCanvas();
        if (canvas == null)
            return null;

        PhoneDialogueUI phoneUi = FindScenePhoneUi();
        if (phoneUi == null)
        {
            GameObject root = new GameObject(PhoneUiName, typeof(RectTransform), typeof(PhoneDialogueUI));
            Undo.RegisterCreatedObjectUndo(root, "Create PhoneDialogueUI");
            Undo.SetTransformParent(root.transform, canvas.transform, "Parent PhoneDialogueUI");
            phoneUi = root.GetComponent<PhoneDialogueUI>();
        }
        else if (phoneUi.GetComponentInParent<Canvas>(true) == null)
        {
            Undo.SetTransformParent(phoneUi.transform, canvas.transform, "Parent PhoneDialogueUI");
        }

        GameObject rootObject = phoneUi.gameObject;
        bool wasVisible = phoneUi.panel != null && phoneUi.panel.activeSelf;
        RectTransform rootRect = EnsureComponent<RectTransform>(rootObject);
        Stretch(rootRect);

        Sprite phoneSprite = LoadSprite(PhoneSpritePath);
        Sprite bubbleSprite = LoadSprite(BubbleSpritePath);

        GameObject panel = GetOrCreateChild(rootObject.transform, "Panel", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
        RectTransform panelRect = EnsureComponent<RectTransform>(panel);
        Stretch(panelRect);
        Image panelImage = EnsureComponent<Image>(panel);
        panelImage.sprite = phoneSprite;
        panelImage.type = Image.Type.Simple;
        panelImage.preserveAspect = true;
        panelImage.color = Color.white;
        Button tapButton = EnsureComponent<Button>(panel);
        tapButton.transition = Selectable.Transition.None;
        tapButton.targetGraphic = panelImage;

        TextMeshProUGUI contactText = GetOrCreateText(panel.transform, "ContactNameText");
        ConfigureAnchored(contactText.rectTransform, new Vector2(0.24f, 0.865f), new Vector2(0.76f, 0.93f), Vector2.zero, Vector2.zero);
        contactText.alignment = TextAlignmentOptions.Center;
        contactText.fontSize = 28f;
        contactText.fontStyle = FontStyles.Bold;
        contactText.color = new Color(0.92f, 0.94f, 0.97f, 1f);
        contactText.raycastTarget = false;

        GameObject avatarObject = GetOrCreateChild(panel.transform, "ContactAvatar", typeof(RectTransform), typeof(Image), typeof(Mask));
        RectTransform avatarRect = EnsureComponent<RectTransform>(avatarObject);
        ConfigureAnchored(avatarRect, new Vector2(0.135f, 0.852f), new Vector2(0.205f, 0.925f), Vector2.zero, Vector2.zero);
        Image avatarImage = EnsureComponent<Image>(avatarObject);
        avatarImage.color = Color.white;
        avatarImage.preserveAspect = true;
        Mask avatarMask = EnsureComponent<Mask>(avatarObject);
        avatarMask.showMaskGraphic = true;
        avatarObject.SetActive(false);

        ScrollRect scrollRect = CreateMessagesScroll(panel.transform);
        RectTransform contentRect = scrollRect.content;

        GameObject typingIndicator = GetOrCreateChild(contentRect, "TypingIndicator", typeof(RectTransform), typeof(CanvasGroup));
        TextMeshProUGUI typingText = GetOrCreateText(typingIndicator.transform, "TypingText");
        ConfigureAnchored(typingText.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 8f), new Vector2(-18f, -8f));
        typingText.text = "...";
        typingText.fontSize = 30f;
        typingText.alignment = TextAlignmentOptions.Left;
        typingText.color = new Color(0.85f, 0.87f, 0.9f, 1f);
        LayoutElement typingLayout = EnsureComponent<LayoutElement>(typingIndicator);
        typingLayout.minHeight = 52f;
        typingLayout.preferredHeight = 52f;
        typingIndicator.SetActive(false);

        TextMeshProUGUI tapToContinue = GetOrCreateText(panel.transform, "TapToContinueText");
        ConfigureAnchored(tapToContinue.rectTransform, new Vector2(0.18f, 0.105f), new Vector2(0.82f, 0.145f), Vector2.zero, Vector2.zero);
        tapToContinue.text = "Tap to continue";
        tapToContinue.fontSize = 20f;
        tapToContinue.alignment = TextAlignmentOptions.Center;
        tapToContinue.color = new Color(0.76f, 0.78f, 0.82f, 1f);
        tapToContinue.raycastTarget = false;
        tapToContinue.gameObject.SetActive(false);

        GameObject templates = GetOrCreateChild(rootObject.transform, "Templates", typeof(RectTransform));
        templates.SetActive(true);
        GameObject incomingBubble = CreateBubbleTemplate(templates.transform, "IncomingBubbleTemplate", bubbleSprite, false);
        GameObject outgoingBubble = CreateBubbleTemplate(templates.transform, "OutgoingBubbleTemplate", bubbleSprite, true);
        templates.SetActive(false);

        Undo.RecordObject(phoneUi, "Configure PhoneDialogueUI");
        phoneUi.panel = panel;
        phoneUi.contactNameText = contactText;
        phoneUi.contactAvatarImage = avatarImage;
        phoneUi.messagesContainer = contentRect;
        phoneUi.incomingBubblePrefab = incomingBubble;
        phoneUi.outgoingBubblePrefab = outgoingBubble;
        phoneUi.typingIndicator = typingIndicator;
        phoneUi.tapToContinueText = tapToContinue;
        phoneUi.tapArea = tapButton;
        phoneUi.AutoFillPhoneReferencesFromHierarchy();
        EditorUtility.SetDirty(phoneUi);

        StoryUserInterface storyUserInterface = FindOrCreateStoryUserInterface(phoneUi, canvas);
        if (storyUserInterface != null)
        {
            Undo.RecordObject(storyUserInterface, "Configure StoryUserInterface Phone");
            storyUserInterface.MigratePhoneReferencesFromLegacyPhoneDialogueUI(overwrite: false);
            storyUserInterface.AutoFillPhoneReferences(overwrite: false);
            storyUserInterface.ApplyPhoneConfiguration(nameof(CreateOrConfigureInOpenScene));
            EditorUtility.SetDirty(storyUserInterface);
        }

        panel.SetActive(Application.isPlaying && wasVisible);
        AssignToStoryManager(phoneUi);
        EnsureEventSystem();
        Selection.activeGameObject = rootObject;
        EditorGUIUtility.PingObject(rootObject);
        MarkSceneDirty(rootObject);
        return phoneUi;
    }

    public static StoryUserInterface FindOrCreateStoryUserInterface(PhoneDialogueUI phoneUi = null, Canvas canvas = null)
    {
        StoryUserInterface storyUserInterface = FindSceneStoryUserInterface();
        if (storyUserInterface != null)
            return storyUserInterface;

        GameObject owner = GameObject.Find("StoryUserInterface");
        if (owner == null)
        {
            DialogueUIManager dialogueUI = FindSceneObject<DialogueUIManager>();
            if (dialogueUI != null)
                owner = dialogueUI.gameObject;
        }

        if (owner == null && phoneUi != null)
            owner = phoneUi.gameObject;

        if (owner == null)
        {
            owner = new GameObject("StoryUserInterface", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(owner, "Create StoryUserInterface");
            if (canvas == null)
                canvas = FindSceneObject<Canvas>();
            if (canvas != null)
                Undo.SetTransformParent(owner.transform, canvas.transform, "Parent StoryUserInterface");
        }

        storyUserInterface = owner.GetComponent<StoryUserInterface>();
        if (storyUserInterface == null)
            storyUserInterface = Undo.AddComponent<StoryUserInterface>(owner);

        if (phoneUi != null)
        {
            storyUserInterface.MigratePhoneReferencesFromLegacyPhoneDialogueUI(overwrite: false);
            storyUserInterface.AutoFillPhoneReferences(overwrite: false);
        }

        MarkSceneDirty(owner);
        return storyUserInterface;
    }

    static ScrollRect CreateMessagesScroll(Transform panel)
    {
        GameObject scrollObject = GetOrCreateChild(panel, "MessagesScrollRect", typeof(RectTransform), typeof(ScrollRect));
        RectTransform scrollRectTransform = EnsureComponent<RectTransform>(scrollObject);
        ConfigureAnchored(scrollRectTransform, new Vector2(0.17f, 0.18f), new Vector2(0.83f, 0.84f), Vector2.zero, Vector2.zero);

        GameObject viewportObject = GetOrCreateChild(scrollObject.transform, "Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        RectTransform viewportRect = EnsureComponent<RectTransform>(viewportObject);
        Stretch(viewportRect);
        Image viewportImage = EnsureComponent<Image>(viewportObject);
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        Mask mask = EnsureComponent<Mask>(viewportObject);
        mask.showMaskGraphic = false;

        GameObject contentObject = GetOrCreateChild(viewportObject.transform, "Content", out bool contentCreated, typeof(RectTransform));
        RectTransform contentRect = EnsureComponent<RectTransform>(contentObject);
        if (contentCreated)
        {
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup verticalLayout = EnsureComponent<VerticalLayoutGroup>(contentObject);
            verticalLayout.childAlignment = TextAnchor.UpperLeft;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;
            verticalLayout.spacing = 10f;
            verticalLayout.padding = new RectOffset(8, 8, 8, 8);

            ContentSizeFitter fitter = EnsureComponent<ContentSizeFitter>(contentObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        ScrollRect scrollRect = EnsureComponent<ScrollRect>(scrollObject);
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 18f;
        return scrollRect;
    }

    static GameObject CreateBubbleTemplate(Transform parent, string name, Sprite bubbleSprite, bool outgoing)
    {
        GameObject root = GetOrCreateChild(parent, name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(CanvasGroup), typeof(LayoutElement));
        RectTransform rootRect = EnsureComponent<RectTransform>(root);
        rootRect.sizeDelta = new Vector2(0f, 88f);

        HorizontalLayoutGroup rowLayout = EnsureComponent<HorizontalLayoutGroup>(root);
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.spacing = 8f;

        LayoutElement rowElement = EnsureComponent<LayoutElement>(root);
        rowElement.minHeight = 72f;
        rowElement.preferredHeight = 88f;

        GameObject leftSpacer = GetOrCreateChild(root.transform, "LeftSpacer", typeof(RectTransform), typeof(LayoutElement));
        GameObject bubble = GetOrCreateChild(root.transform, "BubbleBackground", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        GameObject rightSpacer = GetOrCreateChild(root.transform, "RightSpacer", typeof(RectTransform), typeof(LayoutElement));

        LayoutElement leftLayout = EnsureComponent<LayoutElement>(leftSpacer);
        LayoutElement rightLayout = EnsureComponent<LayoutElement>(rightSpacer);
        leftLayout.flexibleWidth = outgoing ? 1f : 0f;
        leftLayout.preferredWidth = outgoing ? 80f : 0f;
        rightLayout.flexibleWidth = outgoing ? 0f : 1f;
        rightLayout.preferredWidth = outgoing ? 0f : 80f;

        Image bubbleImage = EnsureComponent<Image>(bubble);
        bubbleImage.sprite = bubbleSprite;
        bubbleImage.type = bubbleSprite != null && bubbleSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        bubbleImage.color = outgoing
            ? new Color(0.16f, 0.43f, 0.82f, 0.96f)
            : new Color(0.18f, 0.19f, 0.23f, 0.96f);

        VerticalLayoutGroup bubbleLayout = EnsureComponent<VerticalLayoutGroup>(bubble);
        bubbleLayout.childAlignment = TextAnchor.MiddleLeft;
        bubbleLayout.childControlWidth = true;
        bubbleLayout.childControlHeight = true;
        bubbleLayout.childForceExpandWidth = true;
        bubbleLayout.childForceExpandHeight = false;
        bubbleLayout.padding = new RectOffset(18, 18, 12, 12);
        bubbleLayout.spacing = 8f;

        ContentSizeFitter bubbleFitter = EnsureComponent<ContentSizeFitter>(bubble);
        bubbleFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bubbleFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement bubbleElement = EnsureComponent<LayoutElement>(bubble);
        bubbleElement.minHeight = 56f;
        bubbleElement.preferredWidth = 500f;
        bubbleElement.flexibleWidth = 0f;

        TextMeshProUGUI senderText = GetOrCreateText(bubble.transform, outgoing ? "ContactNameText (1)" : "ContactNameText");
        senderText.text = outgoing ? "{PlayerName}" : "Contact";
        senderText.fontSize = 18f;
        senderText.color = outgoing
            ? new Color(0.82f, 0.9f, 1f, 1f)
            : new Color(0.74f, 0.76f, 0.82f, 1f);
        senderText.alignment = TextAlignmentOptions.TopLeft;
        senderText.raycastTarget = false;
        LayoutElement senderLayout = EnsureComponent<LayoutElement>(senderText.gameObject);
        senderLayout.minHeight = 20f;
        senderLayout.preferredHeight = 24f;

        TextMeshProUGUI messageText = GetOrCreateText(bubble.transform, "MessageText");
        messageText.text = outgoing ? "Outgoing message" : "Incoming message";
        messageText.fontSize = 24f;
        messageText.color = Color.white;
        messageText.alignment = TextAlignmentOptions.TopLeft;
        messageText.enableWordWrapping = true;
        messageText.raycastTarget = false;
        LayoutElement textLayout = EnsureComponent<LayoutElement>(messageText.gameObject);
        textLayout.minHeight = 28f;
        textLayout.flexibleWidth = 1f;

        TextMeshProUGUI timeText = GetOrCreateText(bubble.transform, "TimeText");
        timeText.text = "15:25";
        timeText.fontSize = 14f;
        timeText.color = new Color(0.82f, 0.87f, 0.94f, 1f);
        timeText.alignment = TextAlignmentOptions.TopLeft;
        timeText.enableWordWrapping = false;
        timeText.overflowMode = TextOverflowModes.Overflow;
        timeText.raycastTarget = false;
        RectTransform timeRect = EnsureComponent<RectTransform>(timeText.gameObject);
        timeRect.anchorMin = new Vector2(0f, 1f);
        timeRect.anchorMax = new Vector2(0f, 1f);
        timeRect.pivot = new Vector2(0f, 1f);
        timeRect.anchoredPosition = Vector2.zero;
        timeRect.sizeDelta = new Vector2(96f, 20f);
        LayoutElement timeLayout = EnsureComponent<LayoutElement>(timeText.gameObject);
        timeLayout.ignoreLayout = true;
        timeLayout.preferredWidth = 96f;
        timeLayout.preferredHeight = 20f;

        GameObject attachment = GetOrCreateChild(bubble.transform, "AttachmentImage", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        RectTransform attachmentRect = EnsureComponent<RectTransform>(attachment);
        attachmentRect.sizeDelta = new Vector2(420f, 240f);
        Image attachmentImage = EnsureComponent<Image>(attachment);
        attachmentImage.color = Color.white;
        attachmentImage.preserveAspect = true;
        LayoutElement attachmentLayout = EnsureComponent<LayoutElement>(attachment);
        attachmentLayout.preferredWidth = 420f;
        attachmentLayout.preferredHeight = 240f;
        attachment.SetActive(false);

        return root;
    }

    static Canvas FindOrCreateCanvas()
    {
        Canvas canvas = FindSceneObject<Canvas>();
        if (canvas != null)
            return canvas;

        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 1f;

        MarkSceneDirty(canvasObject);
        return canvas;
    }

    static void EnsureEventSystem()
    {
        if (FindSceneObject<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        MarkSceneDirty(eventSystem);
    }

    static TextMeshProUGUI GetOrCreateText(Transform parent, string name)
    {
        GameObject textObject = GetOrCreateChild(parent, name, typeof(RectTransform), typeof(TextMeshProUGUI));
        return EnsureComponent<TextMeshProUGUI>(textObject);
    }

    static GameObject GetOrCreateChild(Transform parent, string name, params System.Type[] components)
    {
        bool created;
        return GetOrCreateChild(parent, name, out created, components);
    }

    static GameObject GetOrCreateChild(Transform parent, string name, out bool created, params System.Type[] components)
    {
        Transform child = FindDirectChild(parent, name);
        GameObject gameObject;
        created = false;
        if (child != null)
        {
            gameObject = child.gameObject;
        }
        else
        {
            gameObject = new GameObject(name, components);
            created = true;
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
            Undo.SetTransformParent(gameObject.transform, parent, "Parent " + name);
        }

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null || gameObject.GetComponent(components[i]) != null)
                continue;

            Undo.AddComponent(gameObject, components[i]);
        }

        return gameObject;
    }

    static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == name)
                return child;
        }

        return null;
    }

    static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component != null)
            return component;

        return Undo.AddComponent<T>(gameObject);
    }

    static void Stretch(RectTransform rect)
    {
        ConfigureAnchored(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    static void ConfigureAnchored(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
            return sprite;

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static T FindSceneObject<T>() where T : Object
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < objects.Length; i++)
        {
            T item = objects[i];
            if (item == null || EditorUtility.IsPersistent(item))
                continue;

            GameObject gameObject = null;
            Component component = item as Component;
            if (component != null)
                gameObject = component.gameObject;
            else
                gameObject = item as GameObject;

            if (gameObject == null || !gameObject.scene.IsValid())
                continue;

            return item;
        }

        return null;
    }

    static void MarkSceneDirty(GameObject gameObject)
    {
        if (gameObject == null || !gameObject.scene.IsValid())
            return;

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
}
#endif
