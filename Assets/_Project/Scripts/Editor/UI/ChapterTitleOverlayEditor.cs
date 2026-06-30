using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(ChapterTitleOverlay))]
[CanEditMultipleObjects]
public sealed class ChapterTitleOverlayEditor : Editor
{
    SerializedProperty _editorPreviewStoryId;

    void OnEnable()
    {
        _editorPreviewStoryId = serializedObject.FindProperty("_editorPreviewStoryId");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawStoryStyleSection();
        EditorGUILayout.Space(8f);

        DrawPropertiesExcluding(serializedObject, "m_Script", "_editorPreviewStoryId");

        serializedObject.ApplyModifiedProperties();
    }

    void DrawStoryStyleSection()
    {
        EditorGUILayout.LabelField("UI главы по Story ID", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Для разных историй плашку главы теперь настраивай в Story UI Style, раздел \"Заголовок главы\". Поля ниже на этом компоненте остаются базовым fallback для сцены.",
            MessageType.Info);

        string currentStoryId = ResolveCurrentStoryId(out StoryData currentStory);
        if (_editorPreviewStoryId != null && string.IsNullOrWhiteSpace(_editorPreviewStoryId.stringValue))
            _editorPreviewStoryId.stringValue = currentStoryId;

        EditorGUILayout.PropertyField(_editorPreviewStoryId, new GUIContent("Preview Story ID"));

        string storyId = _editorPreviewStoryId != null ? _editorPreviewStoryId.stringValue : currentStoryId;
        StoryInterfaceStyleCatalog catalog = StoryInterfaceEditorUtility.FindDefaultCatalog();
        StoryInterfaceStyleEntry entry = null;
        bool hasEntry = catalog != null && catalog.TryGetEntry(currentStory, storyId, out entry);
        StoryUiStyle style = entry != null ? entry.StoryUiStyle : null;

        if (catalog == null)
        {
            EditorGUILayout.HelpBox("Story UI Catalog не найден. Без него настройки по Story ID не применятся.", MessageType.Warning);
        }
        else if (!hasEntry)
        {
            EditorGUILayout.HelpBox($"Для Story ID '{storyId}' нет записи в Story UI Catalog.", MessageType.Warning);
        }
        else
        {
            string styleName = style != null ? style.name : "style не назначен";
            EditorGUILayout.HelpBox($"Подключено: {StoryInterfaceEditorUtility.ResolveEntryStoryId(entry)} -> {styleName}", MessageType.None);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Открыть каталог"))
                StoryInterfaceStyleCatalogEditor.SelectDefaultCatalog();

            using (new EditorGUI.DisabledScope(style == null))
            {
                if (GUILayout.Button("Открыть style"))
                    StoryInterfaceEditorUtility.SelectAndPing(style);
            }

            if (GUILayout.Button("Preview главы"))
                PreviewChapterTitle(style);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(style == null))
            {
                if (GUILayout.Button("Применить style"))
                    ApplyPreviewStyle(true);

                if (GUILayout.Button("Скопировать всё в style"))
                    CopyCurrentSettingsToStyle(style);
            }
        }
    }

    void PreviewChapterTitle(StoryUiStyle style)
    {
        serializedObject.ApplyModifiedProperties();

        foreach (Object selectedTarget in targets)
        {
            ChapterTitleOverlay overlay = selectedTarget as ChapterTitleOverlay;
            if (overlay == null)
                continue;

            Undo.RegisterFullObjectHierarchyUndo(overlay.gameObject, "Preview Chapter Title UI");
            overlay.ApplyStoryUiStyle(style);
            overlay.PreviewTitleText("ГЛАВА 1: НОВАЯ РОЛЬ");
            EditorUtility.SetDirty(overlay);
        }

        Canvas.ForceUpdateCanvases();
        SceneView.RepaintAll();
    }

    void ApplyPreviewStyle(bool registerUndo)
    {
        string currentStoryId = ResolveCurrentStoryId(out StoryData currentStory);
        string storyId = _editorPreviewStoryId != null && !string.IsNullOrWhiteSpace(_editorPreviewStoryId.stringValue)
            ? _editorPreviewStoryId.stringValue
            : currentStoryId;

        StoryInterfaceStyleCatalog catalog = StoryInterfaceEditorUtility.FindDefaultCatalog();
        if (catalog == null || !catalog.TryGetEntry(currentStory, storyId, out StoryInterfaceStyleEntry entry) || entry.StoryUiStyle == null)
            return;

        foreach (Object selectedTarget in targets)
        {
            ChapterTitleOverlay overlay = selectedTarget as ChapterTitleOverlay;
            if (overlay == null)
                continue;

            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(overlay.gameObject, "Apply Chapter Title Story UI Style");

            overlay.ApplyStoryUiStyle(entry.StoryUiStyle);
            EditorUtility.SetDirty(overlay);
        }

        Canvas.ForceUpdateCanvases();
        SceneView.RepaintAll();
    }

    void CopyCurrentSettingsToStyle(StoryUiStyle style)
    {
        if (style == null || targets.Length == 0)
            return;

        ChapterTitleOverlay overlay = targets[0] as ChapterTitleOverlay;
        if (overlay == null)
            return;

        serializedObject.ApplyModifiedProperties();

        Undo.RecordObject(style, "Copy Chapter Title Settings To Style");
        SerializedObject overlayObject = new SerializedObject(overlay);
        SerializedObject styleObject = new SerializedObject(style);

        CopyVisualSettings(overlay, styleObject);
        CopyPair(overlayObject, styleObject, "_centerOnShow", "_overrideChapterTitleCenterOnShow", "_chapterTitleCenterOnShow");
        CopyPair(overlayObject, styleObject, "_bringToFrontOnShow", "_overrideChapterTitleBringToFrontOnShow", "_chapterTitleBringToFrontOnShow");
        CopyPair(overlayObject, styleObject, "_backgroundDimSizeMode", "_overrideChapterTitleBackgroundDimSizeMode", "_chapterTitleBackgroundDimSizeMode");
        CopyPair(overlayObject, styleObject, "_backgroundDimFixedSize", "_overrideChapterTitleBackgroundDimFixedSize", "_chapterTitleBackgroundDimFixedSize");
        CopyPair(overlayObject, styleObject, "_backgroundDimColor", "_overrideChapterTitleBackgroundDimColor", "_chapterTitleBackgroundDimColor");
        CopyPair(overlayObject, styleObject, "_backgroundDimAlpha", "_overrideChapterTitleBackgroundDimAlpha", "_chapterTitleBackgroundDimAlpha");
        CopyPair(overlayObject, styleObject, "_textMode", "_overrideChapterTitleTextMode", "_chapterTitleTextMode");
        CopyPair(overlayObject, styleObject, "_textFormat", "_overrideChapterTitleTextFormat", "_chapterTitleTextFormat");
        CopyPair(overlayObject, styleObject, "_numberAndTitleFormat", "_overrideChapterTitleNumberAndTitleFormat", "_chapterTitleNumberAndTitleFormat");
        CopyPair(overlayObject, styleObject, "_chapterNumberOffset", "_overrideChapterTitleNumberOffset", "_chapterTitleNumberOffset");
        CopyPair(overlayObject, styleObject, "_emptyTitleFallback", "_overrideChapterTitleEmptyTitleFallback", "_chapterTitleEmptyTitleFallback");
        CopyPair(overlayObject, styleObject, "_trimTitle", "_overrideChapterTitleTrimTitle", "_chapterTitleTrimTitle");
        CopyPair(overlayObject, styleObject, "_uppercaseTitle", "_overrideChapterTitleUppercaseTitle", "_chapterTitleUppercaseTitle");
        CopyPair(overlayObject, styleObject, "_animationMode", "_overrideChapterTitleAnimationMode", "_chapterTitleAnimationMode");
        CopyPair(overlayObject, styleObject, "_shownAnchoredPosition", "_overrideChapterTitleShownPosition", "_chapterTitleShownPosition");
        CopyPair(overlayObject, styleObject, "_captureShownPositionOnAwake", "_overrideChapterTitleCaptureShownPositionOnAwake", "_chapterTitleCaptureShownPositionOnAwake");
        CopyPair(overlayObject, styleObject, "_hiddenOffsetY", "_overrideChapterTitleHiddenOffsetY", "_chapterTitleHiddenOffsetY");
        CopyPair(overlayObject, styleObject, "_enterDuration", "_overrideChapterTitleEnterDuration", "_chapterTitleEnterDuration");
        CopyPair(overlayObject, styleObject, "_visibleDuration", "_overrideChapterTitleVisibleDuration", "_chapterTitleVisibleDuration");
        CopyPair(overlayObject, styleObject, "_exitDuration", "_overrideChapterTitleExitDuration", "_chapterTitleExitDuration");
        CopyPair(overlayObject, styleObject, "_fadeWithMovement", "_overrideChapterTitleFadeWithMovement", "_chapterTitleFadeWithMovement");
        CopyPair(overlayObject, styleObject, "_animatePosition", "_overrideChapterTitleAnimatePosition", "_chapterTitleAnimatePosition");
        CopyPair(overlayObject, styleObject, "_useUnscaledTime", "_overrideChapterTitleUseUnscaledTime", "_chapterTitleUseUnscaledTime");
        CopyPair(overlayObject, styleObject, "_disableRootAfterExit", "_overrideChapterTitleDisableRootAfterExit", "_chapterTitleDisableRootAfterExit");

        SetBool(styleObject, "_overrideChapterTitleSpecificPaddingSettings", true);
        CopyValue(overlayObject.FindProperty("_useSpecificTitlePadding"), styleObject.FindProperty("_chapterTitleUseSpecificPadding"));
        CopyValue(overlayObject.FindProperty("_specificTitlePaddingMarkers"), styleObject.FindProperty("_chapterTitleSpecificPaddingMarkers"));
        CopyValue(overlayObject.FindProperty("_specificTitlePadding"), styleObject.FindProperty("_chapterTitleSpecificPadding"));

        styleObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssets();
        ApplyPreviewStyle(true);
    }

    static void CopyVisualSettings(ChapterTitleOverlay overlay, SerializedObject styleObject)
    {
        Image panelImage = overlay.PanelBackgroundImage;
        if (panelImage != null)
        {
            SetObject(styleObject, "_chapterTitlePanelSprite", panelImage.sprite);
            SetBool(styleObject, "_overrideChapterTitlePanelColor", true);
            SetColor(styleObject, "_chapterTitlePanelColor", panelImage.color);
            SetBool(styleObject, "_overrideChapterTitlePanelImageType", true);
            SetEnum(styleObject, "_chapterTitlePanelImageType", (int)panelImage.type);
        }

        TMP_Text titleText = overlay.TitleText;
        if (titleText != null)
        {
            SetBool(styleObject, "_overrideChapterTitleTextColor", true);
            SetColor(styleObject, "_chapterTitleTextColor", titleText.color);
            SetObject(styleObject, "_chapterTitleTextFont", titleText.font);
            SetBool(styleObject, "_overrideChapterTitleTextFontSize", true);
            SetFloat(styleObject, "_chapterTitleTextFontSize", titleText.fontSize);
        }
    }

    static void CopyPair(SerializedObject sourceObject, SerializedObject targetObject, string sourceName, string overrideName, string targetName)
    {
        SetBool(targetObject, overrideName, true);
        CopyValue(sourceObject.FindProperty(sourceName), targetObject.FindProperty(targetName));
    }

    static void CopyValue(SerializedProperty source, SerializedProperty target)
    {
        if (source == null || target == null)
            return;

        if (source.isArray && target.isArray && source.propertyType == SerializedPropertyType.Generic)
        {
            target.arraySize = source.arraySize;
            for (int i = 0; i < source.arraySize; i++)
                CopyValue(source.GetArrayElementAtIndex(i), target.GetArrayElementAtIndex(i));
            return;
        }

        switch (target.propertyType)
        {
            case SerializedPropertyType.Boolean:
                target.boolValue = source.boolValue;
                break;
            case SerializedPropertyType.Float:
                target.floatValue = source.floatValue;
                break;
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Enum:
                target.intValue = source.intValue;
                break;
            case SerializedPropertyType.String:
                target.stringValue = source.stringValue;
                break;
            case SerializedPropertyType.Color:
                target.colorValue = source.colorValue;
                break;
            case SerializedPropertyType.Vector2:
                target.vector2Value = source.vector2Value;
                break;
            case SerializedPropertyType.ObjectReference:
                target.objectReferenceValue = source.objectReferenceValue;
                break;
        }
    }

    static string ResolveCurrentStoryId(out StoryData story)
    {
        StoryManager manager = FindObjectOfType<StoryManager>(true);
        story = manager != null ? manager.storyData : null;

        if (manager != null && !string.IsNullOrWhiteSpace(manager.CurrentStoryId))
            return manager.CurrentStoryId;

        if (story != null && !string.IsNullOrWhiteSpace(story.storyId))
            return story.storyId;

        StoryInterfaceStyleCatalog catalog = StoryInterfaceEditorUtility.FindDefaultCatalog();
        if (catalog != null && catalog.Entries != null)
        {
            foreach (StoryInterfaceStyleEntry entry in catalog.Entries)
            {
                string id = StoryInterfaceEditorUtility.ResolveEntryStoryId(entry);
                if (!string.IsNullOrWhiteSpace(id))
                    return id;
            }
        }

        return "";
    }

    static void SetBool(SerializedObject target, string propertyName, bool value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    static void SetFloat(SerializedObject target, string propertyName, float value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    static void SetColor(SerializedObject target, string propertyName, Color value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
            property.colorValue = value;
    }

    static void SetEnum(SerializedObject target, string propertyName, int value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    static void SetObject(SerializedObject target, string propertyName, Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }
}
