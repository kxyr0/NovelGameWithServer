using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PhoneDialogueLayoutSettings))]
public sealed class PhoneDialogueLayoutSettingsDrawer : PropertyDrawer
{
    sealed class Category
    {
        public readonly string Title;
        public readonly string[] Fields;

        public Category(string title, string[] fields)
        {
            Title = title;
            Fields = fields;
        }
    }

    static readonly Dictionary<string, bool> CategoryExpanded = new Dictionary<string, bool>();

    static readonly Category[] Categories =
    {
        new Category("Master", new[]
        {
            "disableAllPhoneLayoutSettings"
        }),
        new Category("Safe Area", new[]
        {
            "safeAreaPadding",
            "messageContentPadding"
        }),
        new Category("Header Contact", new[]
        {
            "showHeaderContactName",
            "headerContactNameOffset",
            "headerContactNameSizeOffset",
            "headerContactNameMargin",
            "headerContactNameFontSize",
            "overrideHeaderContactNameAutoSize",
            "headerContactNameAutoSize",
            "headerContactNameMinFontSize",
            "headerContactNameMaxFontSize",
            "headerContactNameLineSpacing"
        }),
        new Category("Bubbles", new[]
        {
            "messageVerticalSpacing",
            "bubbleHorizontalOffset",
            "incomingBubbleHorizontalOffset",
            "outgoingBubbleHorizontalOffset",
            "photoBubbleHorizontalOffset",
            "photoUsesMessageSidePosition",
            "bubbleTopPadding",
            "bubbleBottomPadding",
            "bubbleLeftPadding",
            "bubbleRightPadding",
            "maxBubbleWidthPercent",
            "minBubbleWidth",
            "textOffsetInsideBubble",
            "photoMessageSize",
            "scrollToBottom",
            "showSenderNamesInBubbles",
            "hideAvatarsInBubbles"
        }),
        new Category("Content Layout", new[]
        {
            "enforceContentVerticalLayout",
            "preserveMessageContentLayout",
            "disableMessageContentSizeFitterWhenPreserved",
            "forceFullWidthMessageRows"
        }),
        new Category("Animation", new[]
        {
            "messageAppearAnimation",
            "messageAppearDuration",
            "messagePostAppearDelay",
            "messageAppearSlideOffset",
            "messageAppearScaleFrom",
            "messageAppearEase"
        }),
        new Category("Incoming", new[]
        {
            "incomingLayout"
        }),
        new Category("Outgoing", new[]
        {
            "outgoingLayout"
        }),
        new Category("Photo", new[]
        {
            "photoLayout"
        })
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        if (!property.isExpanded)
            return line;

        float height = line + spacing;
        for (int i = 0; i < Categories.Length; i++)
        {
            Category category = Categories[i];
            if (!HasAnyVisibleField(property, category))
                continue;

            height += line + spacing;
            if (!IsCategoryExpanded(property, category))
                continue;

            for (int j = 0; j < category.Fields.Length; j++)
            {
                SerializedProperty fieldProperty = property.FindPropertyRelative(category.Fields[j]);
                if (fieldProperty == null)
                    continue;

                height += EditorGUI.GetPropertyHeight(fieldProperty, true) + spacing;
            }
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float y = position.y;

        Rect headerRect = new Rect(position.x, y, position.width, line);
        property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);
        y += line + spacing;

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < Categories.Length; i++)
            {
                Category category = Categories[i];
                if (!HasAnyVisibleField(property, category))
                    continue;

                Rect categoryRect = new Rect(position.x, y, position.width, line);
                bool expanded = IsCategoryExpanded(property, category);
                expanded = EditorGUI.Foldout(categoryRect, expanded, category.Title, true);
                SetCategoryExpanded(property, category, expanded);
                y += line + spacing;

                if (!expanded)
                    continue;

                EditorGUI.indentLevel++;
                for (int j = 0; j < category.Fields.Length; j++)
                {
                    SerializedProperty fieldProperty = property.FindPropertyRelative(category.Fields[j]);
                    if (fieldProperty == null)
                        continue;

                    float fieldHeight = EditorGUI.GetPropertyHeight(fieldProperty, true);
                    Rect fieldRect = new Rect(position.x, y, position.width, fieldHeight);
                    EditorGUI.PropertyField(fieldRect, fieldProperty, true);
                    y += fieldHeight + spacing;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    static bool HasAnyVisibleField(SerializedProperty property, Category category)
    {
        for (int i = 0; i < category.Fields.Length; i++)
        {
            if (property.FindPropertyRelative(category.Fields[i]) != null)
                return true;
        }

        return false;
    }

    static bool IsCategoryExpanded(SerializedProperty property, Category category)
    {
        bool expanded;
        return CategoryExpanded.TryGetValue(GetCategoryKey(property, category), out expanded) && expanded;
    }

    static void SetCategoryExpanded(SerializedProperty property, Category category, bool expanded)
    {
        CategoryExpanded[GetCategoryKey(property, category)] = expanded;
    }

    static string GetCategoryKey(SerializedProperty property, Category category)
    {
        return property.serializedObject.targetObject.GetInstanceID() + ":" + property.propertyPath + ":" + category.Title;
    }
}
