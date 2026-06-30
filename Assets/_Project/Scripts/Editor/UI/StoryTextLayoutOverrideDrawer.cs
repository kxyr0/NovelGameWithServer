using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(StoryTextLayoutOverride))]
public sealed class StoryTextLayoutOverrideDrawer : PropertyDrawer
{
    const float CompactButtonWidth = 44f;
    const float FullButtonsWidth = 160f;
    const float CompactButtonsThreshold = 320f;
    const float MinFoldoutWidth = 88f;

    static readonly string[] CopyFieldNames =
    {
        "_topOffsetY",
        "_offsetX",
        "_overrideTextWidth",
        "_textWidth",
        "_overrideBackgroundPadding",
        "_backgroundPadding",
        "_overrideBackgroundMinSize",
        "_backgroundMinSize",
        "_overrideBackgroundMaxSize",
        "_backgroundMaxSize",
        "_overrideBackgroundGrowthUpFactor",
        "_backgroundGrowthUpFactor",
        "_overrideResizeHeightToPreferredText",
        "_resizeHeightToPreferredText",
        "_overrideExtraHeight",
        "_extraHeight",
        "_overrideMinHeight",
        "_minHeight",
        "_overrideMaxHeight",
        "_maxHeight",
        "_overrideMaxFontSize",
        "_maxFontSize",
        "_overrideShrinkTextToFitRect",
        "_shrinkTextToFitRect",
        "_overrideMinAutoFontSize",
        "_minAutoFontSize",
        "_overrideOverflowModeWhenStillTooLarge",
        "_overflowModeWhenStillTooLarge"
    };

    static readonly string[] DrawFieldNames =
    {
        "_storyId",
        "_topOffsetY",
        "_offsetX",
        "_overrideTextWidth",
        "_textWidth",
        "_overrideBackgroundPadding",
        "_backgroundPadding",
        "_overrideBackgroundMinSize",
        "_backgroundMinSize",
        "_overrideBackgroundMaxSize",
        "_backgroundMaxSize",
        "_overrideBackgroundGrowthUpFactor",
        "_backgroundGrowthUpFactor",
        "_overrideResizeHeightToPreferredText",
        "_resizeHeightToPreferredText",
        "_overrideExtraHeight",
        "_extraHeight",
        "_overrideMinHeight",
        "_minHeight",
        "_overrideMaxHeight",
        "_maxHeight",
        "_overrideMaxFontSize",
        "_maxFontSize",
        "_overrideShrinkTextToFitRect",
        "_shrinkTextToFitRect",
        "_overrideMinAutoFontSize",
        "_minAutoFontSize",
        "_overrideOverflowModeWhenStillTooLarge",
        "_overflowModeWhenStillTooLarge"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        if (!property.isExpanded)
            return height;

        for (int i = 0; i < DrawFieldNames.Length; i++)
        {
            SerializedProperty child = property.FindPropertyRelative(DrawFieldNames[i]);
            if (child != null)
                height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect headerRect = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight);

        TryGetArrayInfo(property.propertyPath, out string arrayPath, out int index);
        SerializedProperty array = !string.IsNullOrEmpty(arrayPath)
            ? property.serializedObject.FindProperty(arrayPath)
            : null;

        Rect foldoutRect = headerRect;
        bool useCompactButtons = array != null && headerRect.width < CompactButtonsThreshold;
        if (array != null)
            foldoutRect.width = Mathf.Max(MinFoldoutWidth, foldoutRect.width - (useCompactButtons ? CompactButtonWidth : FullButtonsWidth));

        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GetHeaderLabel(property, index), true);

        if (array != null)
        {
            if (useCompactButtons)
            {
                Rect menuRect = new Rect(headerRect.xMax - 40f, headerRect.y, 40f, headerRect.height);
                if (GUI.Button(menuRect, "..."))
                    ShowHeaderActionMenu(property.serializedObject, property.propertyPath, array, arrayPath, index);

                if (!property.isExpanded)
                {
                    EditorGUI.EndProperty();
                    return;
                }

                DrawExpandedFields(position, property, headerRect);
                EditorGUI.EndProperty();
                return;
            }

            Rect copyRect = new Rect(headerRect.xMax - 156f, headerRect.y, 40f, headerRect.height);
            Rect previousRect = new Rect(headerRect.xMax - 112f, headerRect.y, 64f, headerRect.height);
            Rect otherRect = new Rect(headerRect.xMax - 44f, headerRect.y, 44f, headerRect.height);

            if (GUI.Button(copyRect, "коп."))
                CopyCurrentToClipboard(property);

            using (new EditorGUI.DisabledScope(index <= 0))
            {
                if (GUI.Button(previousRect, "из пред."))
                    CopyFromArrayElement(property.serializedObject, arrayPath, index, index - 1);
            }

            using (new EditorGUI.DisabledScope(array.arraySize <= 1))
            {
                if (GUI.Button(otherRect, "из..."))
                    ShowCopyMenu(property.serializedObject, array, arrayPath, index);
            }
        }

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        DrawExpandedFields(position, property, headerRect);
        EditorGUI.EndProperty();
    }

    static void DrawExpandedFields(Rect position, SerializedProperty property, Rect headerRect)
    {
        EditorGUI.indentLevel++;
        float y = headerRect.yMax + EditorGUIUtility.standardVerticalSpacing;
        for (int i = 0; i < DrawFieldNames.Length; i++)
        {
            SerializedProperty child = property.FindPropertyRelative(DrawFieldNames[i]);
            if (child == null)
                continue;

            float childHeight = EditorGUI.GetPropertyHeight(child, true);
            Rect childRect = new Rect(position.x, y, position.width, childHeight);
            EditorGUI.PropertyField(childRect, child, true);
            y += childHeight + EditorGUIUtility.standardVerticalSpacing;
        }
        EditorGUI.indentLevel--;
    }

    static void CopyCurrentToClipboard(SerializedProperty property)
    {
        EditorGUIUtility.systemCopyBuffer = BuildClipboardText(property);
    }

    static void CopyCurrentToClipboard(SerializedObject serializedObject, string propertyPath)
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        if (property != null)
            CopyCurrentToClipboard(property);
    }

    static string BuildClipboardText(SerializedProperty property)
    {
        var builder = new System.Text.StringBuilder();
        AppendPropertyValue(builder, "storyId", property.FindPropertyRelative("_storyId"));
        for (int i = 0; i < CopyFieldNames.Length; i++)
            AppendPropertyValue(builder, CopyFieldNames[i], property.FindPropertyRelative(CopyFieldNames[i]));

        return builder.ToString().TrimEnd();
    }

    static void AppendPropertyValue(System.Text.StringBuilder builder, string label, SerializedProperty property)
    {
        if (builder == null || property == null)
            return;

        builder.Append(label);
        builder.Append(": ");
        builder.Append(FormatPropertyValue(property));
        builder.AppendLine();
    }

    static string FormatPropertyValue(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Float:
                return property.floatValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            case SerializedPropertyType.String:
                return property.stringValue ?? "";
            case SerializedPropertyType.Vector2:
                Vector2 value = property.vector2Value;
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0:0.###}, {1:0.###}",
                    value.x,
                    value.y);
            case SerializedPropertyType.Enum:
                return property.enumDisplayNames != null &&
                    property.enumValueIndex >= 0 &&
                    property.enumValueIndex < property.enumDisplayNames.Length
                        ? property.enumDisplayNames[property.enumValueIndex]
                        : property.enumValueIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            default:
                return property.propertyType.ToString();
        }
    }

    static void ShowHeaderActionMenu(
        SerializedObject serializedObject,
        string propertyPath,
        SerializedProperty array,
        string arrayPath,
        int targetIndex)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(
            new GUIContent("копировать значения"),
            false,
            () => CopyCurrentToClipboard(serializedObject, propertyPath));

        if (targetIndex > 0)
        {
            menu.AddItem(
                new GUIContent("взять из предыдущей"),
                false,
                () => CopyFromArrayElement(serializedObject, arrayPath, targetIndex, targetIndex - 1));
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("взять из предыдущей"));
        }

        if (array.arraySize > 1)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (i == targetIndex)
                    continue;

                int sourceIndex = i;
                SerializedProperty source = array.GetArrayElementAtIndex(i);
                menu.AddItem(
                    new GUIContent($"взять из другой/{GetHeaderLabel(source, i)}"),
                    false,
                    () => CopyFromArrayElement(serializedObject, arrayPath, targetIndex, sourceIndex));
            }
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("взять из другой"));
        }

        menu.ShowAsContext();
    }

    static void ShowCopyMenu(SerializedObject serializedObject, SerializedProperty array, string arrayPath, int targetIndex)
    {
        GenericMenu menu = new GenericMenu();
        for (int i = 0; i < array.arraySize; i++)
        {
            if (i == targetIndex)
                continue;

            int sourceIndex = i;
            SerializedProperty source = array.GetArrayElementAtIndex(i);
            menu.AddItem(
                new GUIContent(GetHeaderLabel(source, i)),
                false,
                () => CopyFromArrayElement(serializedObject, arrayPath, targetIndex, sourceIndex));
        }

        menu.ShowAsContext();
    }

    static void CopyFromArrayElement(SerializedObject serializedObject, string arrayPath, int targetIndex, int sourceIndex)
    {
        serializedObject.Update();
        SerializedProperty array = serializedObject.FindProperty(arrayPath);
        if (array == null ||
            sourceIndex < 0 ||
            targetIndex < 0 ||
            sourceIndex >= array.arraySize ||
            targetIndex >= array.arraySize ||
            sourceIndex == targetIndex)
        {
            return;
        }

        Undo.RecordObject(serializedObject.targetObject, "Copy Story Text Layout Override");
        SerializedProperty source = array.GetArrayElementAtIndex(sourceIndex);
        SerializedProperty target = array.GetArrayElementAtIndex(targetIndex);
        for (int i = 0; i < CopyFieldNames.Length; i++)
            CopyPropertyValue(source.FindPropertyRelative(CopyFieldNames[i]), target.FindPropertyRelative(CopyFieldNames[i]));

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(serializedObject.targetObject);
        ApplySelectedPreview(serializedObject.targetObject);
    }

    static void CopyPropertyValue(SerializedProperty source, SerializedProperty target)
    {
        if (source == null || target == null || source.propertyType != target.propertyType)
            return;

        switch (source.propertyType)
        {
            case SerializedPropertyType.Boolean:
                target.boolValue = source.boolValue;
                break;
            case SerializedPropertyType.Float:
                target.floatValue = source.floatValue;
                break;
            case SerializedPropertyType.Vector2:
                target.vector2Value = source.vector2Value;
                break;
            case SerializedPropertyType.Enum:
                target.enumValueIndex = source.enumValueIndex;
                break;
        }
    }

    static void ApplySelectedPreview(Object targetObject)
    {
        StoryTextLayoutLock layoutLock = targetObject as StoryTextLayoutLock;
        if (layoutLock == null || Selection.activeGameObject != layoutLock.gameObject)
            return;

        layoutLock.ApplyNow();
    }

    static string GetHeaderLabel(SerializedProperty property, int index)
    {
        SerializedProperty storyId = property.FindPropertyRelative("_storyId");
        string prefix = index >= 0 ? $"{index + 1}. " : "";
        string title = storyId != null && !string.IsNullOrWhiteSpace(storyId.stringValue)
            ? $"{prefix}{storyId.stringValue.Trim()}"
            : $"{prefix}Story override";

        string summary = GetHeaderSummary(property);
        return string.IsNullOrWhiteSpace(summary) ? title : $"{title} | {summary}";
    }

    static string GetHeaderSummary(SerializedProperty property)
    {
        SerializedProperty topOffsetY = property.FindPropertyRelative("_topOffsetY");
        SerializedProperty offsetX = property.FindPropertyRelative("_offsetX");
        SerializedProperty overrideTextWidth = property.FindPropertyRelative("_overrideTextWidth");
        SerializedProperty textWidth = property.FindPropertyRelative("_textWidth");
        SerializedProperty overrideBackgroundPadding = property.FindPropertyRelative("_overrideBackgroundPadding");
        SerializedProperty backgroundPadding = property.FindPropertyRelative("_backgroundPadding");
        SerializedProperty overrideBackgroundMaxSize = property.FindPropertyRelative("_overrideBackgroundMaxSize");
        SerializedProperty backgroundMaxSize = property.FindPropertyRelative("_backgroundMaxSize");
        SerializedProperty overrideBackgroundGrowth = property.FindPropertyRelative("_overrideBackgroundGrowthUpFactor");
        SerializedProperty backgroundGrowth = property.FindPropertyRelative("_backgroundGrowthUpFactor");
        SerializedProperty overrideMaxHeight = property.FindPropertyRelative("_overrideMaxHeight");
        SerializedProperty maxHeight = property.FindPropertyRelative("_maxHeight");

        var builder = new System.Text.StringBuilder();
        if (topOffsetY != null)
            AppendSummaryPart(builder, $"Y {FormatFloat(topOffsetY.floatValue)}");
        if (offsetX != null)
            AppendSummaryPart(builder, $"X {FormatFloat(offsetX.floatValue)}");
        if (overrideTextWidth != null && overrideTextWidth.boolValue && textWidth != null)
            AppendSummaryPart(builder, $"W {FormatFloat(textWidth.floatValue)}");
        if (overrideBackgroundPadding != null && overrideBackgroundPadding.boolValue && backgroundPadding != null)
        {
            Vector2 padding = backgroundPadding.vector2Value;
            AppendSummaryPart(builder, $"Pad {FormatFloat(padding.x)}/{FormatFloat(padding.y)}");
        }
        if (overrideBackgroundMaxSize != null && overrideBackgroundMaxSize.boolValue && backgroundMaxSize != null)
        {
            Vector2 maxSize = backgroundMaxSize.vector2Value;
            AppendSummaryPart(builder, $"ПлMax {FormatFloat(maxSize.x)}/{FormatFloat(maxSize.y)}");
        }
        if (overrideBackgroundGrowth != null && overrideBackgroundGrowth.boolValue && backgroundGrowth != null)
            AppendSummaryPart(builder, $"Up {FormatFloat(backgroundGrowth.floatValue)}");
        if (overrideMaxHeight != null && overrideMaxHeight.boolValue && maxHeight != null)
            AppendSummaryPart(builder, $"Max {FormatFloat(maxHeight.floatValue)}");

        return builder.ToString();
    }

    static void AppendSummaryPart(System.Text.StringBuilder builder, string value)
    {
        if (builder.Length > 0)
            builder.Append(" ");

        builder.Append(value);
    }

    static string FormatFloat(float value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    static bool TryGetArrayInfo(string propertyPath, out string arrayPath, out int index)
    {
        const string marker = ".Array.data[";
        arrayPath = "";
        index = -1;

        int markerIndex = propertyPath.LastIndexOf(marker, System.StringComparison.Ordinal);
        if (markerIndex < 0)
            return false;

        int indexStart = markerIndex + marker.Length;
        int indexEnd = propertyPath.IndexOf(']', indexStart);
        if (indexEnd < 0)
            return false;

        arrayPath = propertyPath.Substring(0, markerIndex);
        return int.TryParse(propertyPath.Substring(indexStart, indexEnd - indexStart), out index);
    }
}
