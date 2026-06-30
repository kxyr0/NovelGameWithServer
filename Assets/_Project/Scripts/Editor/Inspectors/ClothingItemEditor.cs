#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ClothingItem))]
public sealed class ClothingItemEditor : Editor
{
    const string WardrobeOffsetProperty = "wardrobeOffset";
    const string WardrobeWidthProperty = "wardrobeWidth";
    const string WardrobeHeightProperty = "wardrobeHeight";
    const string WardrobeSizeProperty = "wardrobeSize";
    const string WardrobeScaleProperty = "wardrobeScale";
    const string WardrobePreserveAspectProperty = "wardrobePreserveAspect";
    const string WardrobeAppearanceLayoutsProperty = "wardrobeAppearanceLayouts";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (targets.Length != 1)
            return;

        ClothingItem item = (ClothingItem)target;
        DrawWardrobeCopyTools(item);
    }

    void DrawWardrobeCopyTools(ClothingItem source)
    {
        if (source == null || source.type != ClothingType.Hair)
            return;

        ClothingWardrobeLayoutGroup group = source.GetResolvedWardrobeLayoutGroup();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Wardrobe Layout Copy", EditorStyles.boldLabel);

        if (group == ClothingWardrobeLayoutGroup.None)
        {
            EditorGUILayout.HelpBox(
                "Группа волос не определена. Выбери Wardrobe Layout Group вручную или переименуй id так, чтобы в нем был silk, na_skoruyu, ukladka, hollywood и т.п.",
                MessageType.Info);
            return;
        }

        List<ClothingItem> targetsInGroup = FindMatchingHairGroup(source, group);
        EditorGUILayout.HelpBox(
            "Группа: " + group + ". Найдено других причесок в этой папке: " + targetsInGroup.Count + ".",
            MessageType.None);

        using (new EditorGUI.DisabledScope(targetsInGroup.Count == 0))
        {
            if (GUILayout.Button("Copy Wardrobe Offset To Hair Group"))
                CopyToGroup(source, targetsInGroup, copyFullLayout: false);

            if (GUILayout.Button("Copy Full Wardrobe Layout To Hair Group"))
                CopyToGroup(source, targetsInGroup, copyFullLayout: true);
        }
    }

    static List<ClothingItem> FindMatchingHairGroup(ClothingItem source, ClothingWardrobeLayoutGroup group)
    {
        var result = new List<ClothingItem>();
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath))
            return result;

        string directory = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrEmpty(directory))
            return result;

        directory = directory.Replace('\\', '/');
        string[] guids = AssetDatabase.FindAssets("t:ClothingItem", new[] { directory });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ClothingItem item = AssetDatabase.LoadAssetAtPath<ClothingItem>(path);
            if (item == null || item == source || item.type != ClothingType.Hair)
                continue;

            if (item.GetResolvedWardrobeLayoutGroup() == group)
                result.Add(item);
        }

        result.Sort((left, right) => string.Compare(left.name, right.name, System.StringComparison.OrdinalIgnoreCase));
        return result;
    }

    static void CopyToGroup(ClothingItem source, List<ClothingItem> targetsInGroup, bool copyFullLayout)
    {
        if (source == null || targetsInGroup == null || targetsInGroup.Count == 0)
            return;

        string actionLabel = copyFullLayout ? "full wardrobe layout" : "wardrobe offset";
        bool confirmed = EditorUtility.DisplayDialog(
            "Copy " + actionLabel,
            "Copy " + actionLabel + " from '" + source.name + "' to " + targetsInGroup.Count + " hair assets in the same group?",
            "Copy",
            "Cancel");

        if (!confirmed)
            return;

        SerializedObject sourceObject = new SerializedObject(source);
        foreach (ClothingItem destination in targetsInGroup)
        {
            Undo.RecordObject(destination, "Copy Hair Wardrobe Layout");
            SerializedObject destinationObject = new SerializedObject(destination);

            CopyVector2(sourceObject, destinationObject, WardrobeOffsetProperty);

            if (copyFullLayout)
            {
                CopyFloat(sourceObject, destinationObject, WardrobeWidthProperty);
                CopyFloat(sourceObject, destinationObject, WardrobeHeightProperty);
                CopyVector2(sourceObject, destinationObject, WardrobeSizeProperty);
                CopyVector3(sourceObject, destinationObject, WardrobeScaleProperty);
                CopyBool(sourceObject, destinationObject, WardrobePreserveAspectProperty);
                CopyAppearanceLayouts(sourceObject, destinationObject);
            }

            destinationObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(destination);
            NotifyPreviewSystems(destination);
        }

        AssetDatabase.SaveAssets();
    }

    static void CopyVector2(SerializedObject source, SerializedObject destination, string propertyName)
    {
        SerializedProperty from = source.FindProperty(propertyName);
        SerializedProperty to = destination.FindProperty(propertyName);
        if (from != null && to != null)
            to.vector2Value = from.vector2Value;
    }

    static void CopyVector3(SerializedObject source, SerializedObject destination, string propertyName)
    {
        SerializedProperty from = source.FindProperty(propertyName);
        SerializedProperty to = destination.FindProperty(propertyName);
        if (from != null && to != null)
            to.vector3Value = from.vector3Value;
    }

    static void CopyFloat(SerializedObject source, SerializedObject destination, string propertyName)
    {
        SerializedProperty from = source.FindProperty(propertyName);
        SerializedProperty to = destination.FindProperty(propertyName);
        if (from != null && to != null)
            to.floatValue = from.floatValue;
    }

    static void CopyBool(SerializedObject source, SerializedObject destination, string propertyName)
    {
        SerializedProperty from = source.FindProperty(propertyName);
        SerializedProperty to = destination.FindProperty(propertyName);
        if (from != null && to != null)
            to.boolValue = from.boolValue;
    }

    static void CopyAppearanceLayouts(SerializedObject source, SerializedObject destination)
    {
        SerializedProperty from = source.FindProperty(WardrobeAppearanceLayoutsProperty);
        SerializedProperty to = destination.FindProperty(WardrobeAppearanceLayoutsProperty);
        if (from == null || to == null || !from.isArray || !to.isArray)
            return;

        to.arraySize = from.arraySize;
        for (int i = 0; i < from.arraySize; i++)
        {
            SerializedProperty fromElement = from.GetArrayElementAtIndex(i);
            SerializedProperty toElement = to.GetArrayElementAtIndex(i);
            CopyRelativeEnum(fromElement, toElement, "appearanceType");
            CopyRelativeVector2(fromElement, toElement, "offset");
            CopyRelativeFloat(fromElement, toElement, "width");
            CopyRelativeFloat(fromElement, toElement, "height");
            CopyRelativeVector3(fromElement, toElement, "scale");
            CopyRelativeBool(fromElement, toElement, "overridePreserveAspect");
            CopyRelativeBool(fromElement, toElement, "preserveAspect");
        }
    }

    static void CopyRelativeEnum(SerializedProperty source, SerializedProperty destination, string propertyName)
    {
        SerializedProperty from = source.FindPropertyRelative(propertyName);
        SerializedProperty to = destination.FindPropertyRelative(propertyName);
        if (from != null && to != null)
            to.enumValueIndex = from.enumValueIndex;
    }

    static void CopyRelativeVector2(SerializedProperty source, SerializedProperty destination, string propertyName)
    {
        SerializedProperty from = source.FindPropertyRelative(propertyName);
        SerializedProperty to = destination.FindPropertyRelative(propertyName);
        if (from != null && to != null)
            to.vector2Value = from.vector2Value;
    }

    static void CopyRelativeVector3(SerializedProperty source, SerializedProperty destination, string propertyName)
    {
        SerializedProperty from = source.FindPropertyRelative(propertyName);
        SerializedProperty to = destination.FindPropertyRelative(propertyName);
        if (from != null && to != null)
            to.vector3Value = from.vector3Value;
    }

    static void CopyRelativeFloat(SerializedProperty source, SerializedProperty destination, string propertyName)
    {
        SerializedProperty from = source.FindPropertyRelative(propertyName);
        SerializedProperty to = destination.FindPropertyRelative(propertyName);
        if (from != null && to != null)
            to.floatValue = from.floatValue;
    }

    static void CopyRelativeBool(SerializedProperty source, SerializedProperty destination, string propertyName)
    {
        SerializedProperty from = source.FindPropertyRelative(propertyName);
        SerializedProperty to = destination.FindPropertyRelative(propertyName);
        if (from != null && to != null)
            to.boolValue = from.boolValue;
    }

    static void NotifyPreviewSystems(ClothingItem item)
    {
        WardrobeHeroSetupPage.EditorNotifyClothingItemChanged(item);
        WardrobeController.EditorNotifyClothingItemChanged(item);
        CharacterViewManager.EditorNotifyClothingItemChanged(item);
    }
}
#endif
