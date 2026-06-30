using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StatChangeOverlay))]
[CanEditMultipleObjects]
public sealed class StatChangeOverlayEditor : Editor
{
    SerializedProperty _useStoryLayoutOverrides;
    SerializedProperty _editorPreviewStoryId;
    SerializedProperty _storyLayoutOverrides;

    void OnEnable()
    {
        _useStoryLayoutOverrides = serializedObject.FindProperty("_useStoryLayoutOverrides");
        _editorPreviewStoryId = serializedObject.FindProperty("_editorPreviewStoryId");
        _storyLayoutOverrides = serializedObject.FindProperty("_storyLayoutOverrides");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawStoryLayoutSection();
        EditorGUILayout.Space(8f);

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "_useStoryLayoutOverrides",
            "_editorPreviewStoryId",
            "_storyLayoutOverrides");

        serializedObject.ApplyModifiedProperties();
    }

    void DrawStoryLayoutSection()
    {
        EditorGUILayout.LabelField("Layout по ID истории", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Icon Image Settings ниже остаются fallback. Если включён этот блок и ID истории найден в списке, offset/spacing/padding берутся из записи этой истории.",
            MessageType.Info);

        EditorGUILayout.PropertyField(_useStoryLayoutOverrides, new GUIContent("Использовать ID истории"));
        EditorGUILayout.PropertyField(_editorPreviewStoryId, new GUIContent("Preview Story ID"));
        DrawStoryOverrideList();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Применить Preview ID"))
                ApplyPreviewStoryLayout();

            if (GUILayout.Button("Скопировать текущий layout в Preview ID"))
                CopyCurrentLayoutToPreviewStory();
        }
    }

    void DrawStoryOverrideList()
    {
        if (_storyLayoutOverrides == null)
            return;

        int removeIndex = -1;

        for (int i = 0; i < _storyLayoutOverrides.arraySize; i++)
        {
            SerializedProperty entry = _storyLayoutOverrides.GetArrayElementAtIndex(i);
            SerializedProperty storyId = entry.FindPropertyRelative("_storyId");
            string title = string.IsNullOrWhiteSpace(storyId.stringValue)
                ? $"История #{i + 1}"
                : storyId.stringValue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, title, true);
                if (GUILayout.Button("-", GUILayout.Width(24f)))
                    removeIndex = i;
            }

            if (entry.isExpanded)
            {
                EditorGUILayout.PropertyField(storyId, new GUIContent("Story ID"));
                DrawOverridePair(entry, "_overridePanelPadding", "_panelPadding", "Отступы плашки");
                DrawOverridePair(entry, "_overrideIconSize", "_iconSize", "Размер иконки");
                DrawOverridePair(entry, "_overrideIconOffset", "_iconOffset", "Offset иконки");
                DrawOverridePair(entry, "_overrideIconVisualScale", "_iconVisualScale", "Scale иконки");
                DrawOverridePair(entry, "_overrideIconMinSize", "_iconMinSize", "Min size иконки");
                DrawOverridePair(entry, "_overrideReserveIconSpaceWhenHidden", "_reserveIconSpaceWhenHidden", "Резерв места без иконки");
                DrawOverridePair(entry, "_overrideIconParentSpacing", "_iconParentSpacing", "Spacing иконка-текст");
                DrawOverridePair(entry, "_overrideIconParentPadding", "_iconParentPadding", "Padding родителя");
            }

            EditorGUILayout.EndVertical();
        }

        if (removeIndex >= 0)
            _storyLayoutOverrides.DeleteArrayElementAtIndex(removeIndex);

        if (GUILayout.Button("Добавить ID истории"))
        {
            int index = _storyLayoutOverrides.arraySize;
            _storyLayoutOverrides.InsertArrayElementAtIndex(index);
            SerializedProperty entry = _storyLayoutOverrides.GetArrayElementAtIndex(index);
            entry.isExpanded = true;
            ResetStoryOverrideEntry(entry, _editorPreviewStoryId != null ? _editorPreviewStoryId.stringValue : "");
        }
    }

    static void DrawOverridePair(SerializedProperty entry, string toggleName, string valueName, string label)
    {
        SerializedProperty toggle = entry.FindPropertyRelative(toggleName);
        SerializedProperty value = entry.FindPropertyRelative(valueName);
        if (toggle == null || value == null)
            return;

        EditorGUILayout.PropertyField(toggle, new GUIContent($"Использовать {label}"));
        using (new EditorGUI.DisabledScope(!toggle.boolValue))
            EditorGUILayout.PropertyField(value, new GUIContent(label), true);
    }

    static void ResetStoryOverrideEntry(SerializedProperty entry, string storyId)
    {
        SetString(entry, "_storyId", storyId);
        SetBool(entry, "_overridePanelPadding", false);
        SetVector2(entry, "_panelPadding", new Vector2(640f, 96f));
        SetBool(entry, "_overrideIconSize", false);
        SetVector2(entry, "_iconSize", Vector2.zero);
        SetBool(entry, "_overrideIconOffset", false);
        SetVector2(entry, "_iconOffset", Vector2.zero);
        SetBool(entry, "_overrideIconVisualScale", false);
        SetVector2(entry, "_iconVisualScale", Vector2.one);
        SetBool(entry, "_overrideIconMinSize", false);
        SetVector2(entry, "_iconMinSize", Vector2.zero);
        SetBool(entry, "_overrideReserveIconSpaceWhenHidden", false);
        SetBool(entry, "_reserveIconSpaceWhenHidden", false);
        SetBool(entry, "_overrideIconParentSpacing", false);
        SetFloat(entry, "_iconParentSpacing", 0f);
        SetBool(entry, "_overrideIconParentPadding", false);
        ResetRectOffset(entry.FindPropertyRelative("_iconParentPadding"));
    }

    static void SetString(SerializedProperty parent, string name, string value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
            property.stringValue = value ?? "";
    }

    static void SetBool(SerializedProperty parent, string name, bool value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
            property.boolValue = value;
    }

    static void SetFloat(SerializedProperty parent, string name, float value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
            property.floatValue = value;
    }

    static void SetVector2(SerializedProperty parent, string name, Vector2 value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
            property.vector2Value = value;
    }

    static void ResetRectOffset(SerializedProperty property)
    {
        if (property == null)
            return;

        SetInt(property, "m_Left", 0);
        SetInt(property, "m_Right", 0);
        SetInt(property, "m_Top", 0);
        SetInt(property, "m_Bottom", 0);
    }

    static void SetInt(SerializedProperty parent, string name, int value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
            property.intValue = value;
    }

    void ApplyPreviewStoryLayout()
    {
        serializedObject.ApplyModifiedProperties();

        foreach (Object selectedTarget in targets)
        {
            StatChangeOverlay overlay = selectedTarget as StatChangeOverlay;
            if (overlay == null)
                continue;

            Undo.RecordObject(overlay, "Apply Stat Overlay Story Layout");
            overlay.ApplyStoryLayoutOverrideForCurrentStory();
            EditorUtility.SetDirty(overlay);
        }
    }

    void CopyCurrentLayoutToPreviewStory()
    {
        serializedObject.ApplyModifiedProperties();

        foreach (Object selectedTarget in targets)
        {
            StatChangeOverlay overlay = selectedTarget as StatChangeOverlay;
            if (overlay == null)
                continue;

            Undo.RecordObject(overlay, "Copy Stat Overlay Layout To Story");
            overlay.CopyCurrentLayoutToPreviewStoryOverride();
            EditorUtility.SetDirty(overlay);
        }

        serializedObject.Update();
    }
}
