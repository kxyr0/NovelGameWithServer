using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PhoneLTRBAttribute))]
public sealed class PhoneLTRBDrawer : PropertyDrawer
{
    const float FieldGap = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Vector4)
            return EditorGUI.GetPropertyHeight(property, label, true);

        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        return property.isExpanded ? line + spacing + 4f * (line + spacing) - spacing : line;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Vector4)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            Vector4 value = property.vector4Value;
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = headerRect.yMax + spacing;

            EditorGUI.indentLevel++;
            value.x = DrawSide(position, ref y, line, spacing, "Left", value.x);
            value.z = DrawSide(position, ref y, line, spacing, "Right", value.z);
            value.y = DrawSide(position, ref y, line, spacing, "Top", value.y);
            value.w = DrawSide(position, ref y, line, spacing, "Bottom", value.w);
            EditorGUI.indentLevel--;

            property.vector4Value = value;
        }

        EditorGUI.EndProperty();
    }

    static float DrawSide(
        Rect totalPosition,
        ref float y,
        float lineHeight,
        float spacing,
        string label,
        float currentValue)
    {
        Rect rect = EditorGUI.IndentedRect(new Rect(totalPosition.x, y, totalPosition.width, lineHeight));
        float labelWidth = EditorGUIUtility.labelWidth - EditorGUI.indentLevel * 15f;
        labelWidth = Mathf.Clamp(labelWidth, 70f, rect.width * 0.45f);

        Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
        Rect fieldRect = new Rect(labelRect.xMax + FieldGap, rect.y, rect.width - labelWidth - FieldGap, rect.height);

        EditorGUI.LabelField(labelRect, label);
        float nextValue = EditorGUI.FloatField(fieldRect, currentValue);

        y += lineHeight + spacing;
        return nextValue;
    }
}
