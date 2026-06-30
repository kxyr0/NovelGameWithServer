#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(StoryLayerLayout))]
public sealed class StoryLayerLayoutDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        return EditorGUIUtility.singleLineHeight +
               EditorGUIUtility.standardVerticalSpacing +
               CharacterSpritePreviewLayoutDrawerUtil.FieldsHeight(
                   property.FindPropertyRelative("offset"),
                   property.FindPropertyRelative("width"),
                   property.FindPropertyRelative("height"),
                   property.FindPropertyRelative("scale"),
                   property.FindPropertyRelative("preserveAspect"));
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = CharacterSpritePreviewLayoutDrawerUtil.TakeLine(ref position);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref position, property.FindPropertyRelative("offset"));
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref position, property.FindPropertyRelative("width"));
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref position, property.FindPropertyRelative("height"));
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref position, property.FindPropertyRelative("scale"));
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref position, property.FindPropertyRelative("preserveAspect"));
        EditorGUI.indentLevel--;

        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(StoryPositionLayout))]
public sealed class StoryPositionLayoutDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        return EditorGUIUtility.singleLineHeight +
               EditorGUIUtility.standardVerticalSpacing +
               CharacterSpritePreviewLayoutDrawerUtil.FieldsHeight(
                   property.FindPropertyRelative("offset"),
                   property.FindPropertyRelative("scale"));
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = CharacterSpritePreviewLayoutDrawerUtil.TakeLine(ref position);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref position, property.FindPropertyRelative("offset"));
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref position, property.FindPropertyRelative("scale"));
        EditorGUI.indentLevel--;

        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(AppearanceVariant))]
public sealed class AppearanceVariantDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        SerializedProperty appearanceType = property.FindPropertyRelative("appearanceType");
        SerializedProperty defaultSprite = property.FindPropertyRelative("defaultSprite");
        SerializedProperty previewOffset = property.FindPropertyRelative("previewOffset");
        SerializedProperty previewWidth = property.FindPropertyRelative("previewWidth");
        SerializedProperty previewHeight = property.FindPropertyRelative("previewHeight");
        SerializedProperty previewPreserveAspect = property.FindPropertyRelative("previewPreserveAspect");
        SerializedProperty storyLayerLayout = property.FindPropertyRelative("storyLayerLayout");
        SerializedProperty storyPositionLayout = property.FindPropertyRelative("storyPositionLayout");
        SerializedProperty wardrobeLayerLayout = property.FindPropertyRelative("wardrobeLayerLayout");
        SerializedProperty emotions = property.FindPropertyRelative("emotions");
        return CharacterSpritePreviewLayoutDrawerUtil.FoldoutHeight(
                   96f,
                   appearanceType,
                   defaultSprite,
                   previewOffset,
                   previewWidth,
                   previewHeight,
                   previewPreserveAspect) +
               EditorGUIUtility.standardVerticalSpacing +
               EditorGUI.GetPropertyHeight(storyLayerLayout, true) +
               EditorGUIUtility.standardVerticalSpacing +
               EditorGUI.GetPropertyHeight(storyPositionLayout, true) +
               EditorGUIUtility.standardVerticalSpacing +
               EditorGUI.GetPropertyHeight(wardrobeLayerLayout, true) +
               EditorGUIUtility.standardVerticalSpacing +
               EditorGUI.GetPropertyHeight(emotions, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = CharacterSpritePreviewLayoutDrawerUtil.TakeLine(ref position);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;

        SerializedProperty appearanceType = property.FindPropertyRelative("appearanceType");
        SerializedProperty defaultSprite = property.FindPropertyRelative("defaultSprite");
        SerializedProperty previewOffset = property.FindPropertyRelative("previewOffset");
        SerializedProperty previewWidth = property.FindPropertyRelative("previewWidth");
        SerializedProperty previewHeight = property.FindPropertyRelative("previewHeight");
        SerializedProperty previewPreserveAspect = property.FindPropertyRelative("previewPreserveAspect");
        SerializedProperty storyLayerLayout = property.FindPropertyRelative("storyLayerLayout");
        SerializedProperty storyPositionLayout = property.FindPropertyRelative("storyPositionLayout");
        SerializedProperty wardrobeLayerLayout = property.FindPropertyRelative("wardrobeLayerLayout");

        Rect layoutRect = CharacterSpritePreviewLayoutDrawerUtil.TakePreviewBlock(
            ref position,
            96f,
            appearanceType,
            defaultSprite,
            previewOffset,
            previewWidth,
            previewHeight,
            previewPreserveAspect);
        Rect fieldsRect = layoutRect;
        fieldsRect.width -= 104f;
        Rect previewRect = new Rect(fieldsRect.xMax + 8f, layoutRect.y, 96f, 96f);

        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, appearanceType);
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, defaultSprite);
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, previewOffset, "Preview Offset");
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, previewWidth, "Preview Width");
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, previewHeight, "Preview Height");
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, previewPreserveAspect, "Preserve Aspect");
        CharacterSpritePreviewLayoutDrawerUtil.DrawSpritePreview(previewRect, defaultSprite);

        CharacterSpritePreviewLayoutDrawerUtil.AddSpacing(ref position);
        Rect storyRect = new Rect(position.x, position.y, position.width, EditorGUI.GetPropertyHeight(storyLayerLayout, true));
        EditorGUI.PropertyField(storyRect, storyLayerLayout, true);
        position.y += storyRect.height + EditorGUIUtility.standardVerticalSpacing;
        position.height -= storyRect.height + EditorGUIUtility.standardVerticalSpacing;

        Rect positionRect = new Rect(position.x, position.y, position.width, EditorGUI.GetPropertyHeight(storyPositionLayout, true));
        EditorGUI.PropertyField(positionRect, storyPositionLayout, true);
        position.y += positionRect.height + EditorGUIUtility.standardVerticalSpacing;
        position.height -= positionRect.height + EditorGUIUtility.standardVerticalSpacing;

        Rect wardrobeRect = new Rect(position.x, position.y, position.width, EditorGUI.GetPropertyHeight(wardrobeLayerLayout, true));
        EditorGUI.PropertyField(wardrobeRect, wardrobeLayerLayout, true);
        position.y += wardrobeRect.height + EditorGUIUtility.standardVerticalSpacing;
        position.height -= wardrobeRect.height + EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty emotions = property.FindPropertyRelative("emotions");
        Rect emotionsRect = new Rect(position.x, position.y, position.width, EditorGUI.GetPropertyHeight(emotions, true));
        EditorGUI.PropertyField(emotionsRect, emotions, true);

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(CharacterEmotion))]
public sealed class CharacterEmotionDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        return CharacterSpritePreviewLayoutDrawerUtil.FoldoutHeight(
            80f,
            property.FindPropertyRelative("emotion"),
            property.FindPropertyRelative("sprite"),
            property.FindPropertyRelative("previewOffset"),
            property.FindPropertyRelative("previewWidth"),
            property.FindPropertyRelative("previewHeight"),
            property.FindPropertyRelative("previewPreserveAspect"),
            property.FindPropertyRelative("storyLayout"));
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        DrawEmotion(position, property, label, "sprite");
    }

    static void DrawEmotion(Rect position, SerializedProperty property, GUIContent label, string spritePropertyName)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = CharacterSpritePreviewLayoutDrawerUtil.TakeLine(ref position);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;

        SerializedProperty emotion = property.FindPropertyRelative("emotion");
        SerializedProperty sprite = property.FindPropertyRelative(spritePropertyName);
        SerializedProperty previewOffset = property.FindPropertyRelative("previewOffset");
        SerializedProperty previewWidth = property.FindPropertyRelative("previewWidth");
        SerializedProperty previewHeight = property.FindPropertyRelative("previewHeight");
        SerializedProperty previewPreserveAspect = property.FindPropertyRelative("previewPreserveAspect");
        SerializedProperty storyLayout = property.FindPropertyRelative("storyLayout");

        Rect layoutRect = CharacterSpritePreviewLayoutDrawerUtil.TakePreviewBlock(
            ref position,
            80f,
            emotion,
            sprite,
            previewOffset,
            previewWidth,
            previewHeight,
            previewPreserveAspect);
        Rect fieldsRect = layoutRect;
        fieldsRect.width -= 88f;
        Rect previewRect = new Rect(fieldsRect.xMax + 8f, layoutRect.y, 80f, 80f);

        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, emotion);
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, sprite);
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, previewOffset, "Preview Offset");
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, previewWidth, "Preview Width");
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, previewHeight, "Preview Height");
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, previewPreserveAspect, "Preserve Aspect");
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, storyLayout, "Story Layout");
        CharacterSpritePreviewLayoutDrawerUtil.DrawSpritePreview(previewRect, sprite);

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(CharacterEmotionLayer))]
public sealed class CharacterEmotionLayerDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        return CharacterSpritePreviewLayoutDrawerUtil.FoldoutHeight(
            80f,
            property.FindPropertyRelative("emotion"),
            property.FindPropertyRelative("faceSprite"),
            property.FindPropertyRelative("previewOffset"),
            property.FindPropertyRelative("previewWidth"),
            property.FindPropertyRelative("previewHeight"),
            property.FindPropertyRelative("previewPreserveAspect"),
            property.FindPropertyRelative("storyLayout"));
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        DrawLayer(position, property, label);
    }

    static void DrawLayer(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = CharacterSpritePreviewLayoutDrawerUtil.TakeLine(ref position);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;

        SerializedProperty emotion = property.FindPropertyRelative("emotion");
        SerializedProperty faceSprite = property.FindPropertyRelative("faceSprite");
        SerializedProperty previewOffset = property.FindPropertyRelative("previewOffset");
        SerializedProperty previewWidth = property.FindPropertyRelative("previewWidth");
        SerializedProperty previewHeight = property.FindPropertyRelative("previewHeight");
        SerializedProperty previewPreserveAspect = property.FindPropertyRelative("previewPreserveAspect");
        SerializedProperty storyLayout = property.FindPropertyRelative("storyLayout");

        Rect layoutRect = CharacterSpritePreviewLayoutDrawerUtil.TakePreviewBlock(
            ref position,
            80f,
            emotion,
            faceSprite,
            previewOffset,
            previewWidth,
            previewHeight,
            previewPreserveAspect);
        Rect fieldsRect = layoutRect;
        fieldsRect.width -= 88f;
        Rect previewRect = new Rect(fieldsRect.xMax + 8f, layoutRect.y, 80f, 80f);

        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, emotion);
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, faceSprite);
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, previewOffset, "Preview Offset");
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, previewWidth, "Preview Width");
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, previewHeight, "Preview Height");
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, previewPreserveAspect, "Preserve Aspect");
        CharacterSpritePreviewLayoutDrawerUtil.DrawField(ref fieldsRect, storyLayout, "Story Layout");
        CharacterSpritePreviewLayoutDrawerUtil.DrawSpritePreview(previewRect, faceSprite);

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}

static class CharacterSpritePreviewLayoutDrawerUtil
{
    public static float FoldoutHeight(float previewHeight, params SerializedProperty[] fields)
    {
        return EditorGUIUtility.singleLineHeight +
               EditorGUIUtility.standardVerticalSpacing +
               Mathf.Max(FieldsHeight(fields), previewHeight);
    }

    public static Rect TakeLine(ref Rect position)
    {
        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        position.height -= EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        return line;
    }

    public static Rect TakePreviewBlock(ref Rect position, float previewHeight, params SerializedProperty[] fields)
    {
        float height = Mathf.Max(FieldsHeight(fields), previewHeight);
        Rect block = new Rect(position.x, position.y, position.width, height);
        position.y += height;
        position.height -= height;
        return block;
    }

    public static void AddSpacing(ref Rect position)
    {
        position.y += EditorGUIUtility.standardVerticalSpacing;
        position.height -= EditorGUIUtility.standardVerticalSpacing;
    }

    public static void DrawField(ref Rect position, SerializedProperty property, string label = null)
    {
        if (property == null)
            return;

        GUIContent content = string.IsNullOrEmpty(label) ? null : new GUIContent(label);
        float height = EditorGUI.GetPropertyHeight(property, content, true);
        Rect line = new Rect(position.x, position.y, position.width, height);
        if (string.IsNullOrEmpty(label))
            EditorGUI.PropertyField(line, property, true);
        else
            EditorGUI.PropertyField(line, property, content, true);

        position.y += height + EditorGUIUtility.standardVerticalSpacing;
    }

    public static float FieldsHeight(params SerializedProperty[] fields)
    {
        if (fields == null || fields.Length == 0)
            return 0f;

        float height = 0f;
        foreach (SerializedProperty field in fields)
        {
            if (field == null)
                continue;

            height += EditorGUI.GetPropertyHeight(field, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }

    public static void DrawSpritePreview(Rect rect, SerializedProperty spriteProperty)
    {
        GUI.Box(rect, GUIContent.none);

        if (spriteProperty == null || spriteProperty.objectReferenceValue == null)
            return;

        Texture texture = AssetPreview.GetAssetPreview(spriteProperty.objectReferenceValue);
        if (texture == null)
            texture = AssetPreview.GetMiniThumbnail(spriteProperty.objectReferenceValue);

        if (texture != null)
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
    }
}

[CustomEditor(typeof(CharacterData))]
public sealed class CharacterDataLiveWardrobePreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }

    static WardrobeHeroSetupPage FindWardrobePage()
    {
        WardrobeHeroSetupPage fallback = null;
        WardrobeHeroSetupPage[] pages = Resources.FindObjectsOfTypeAll<WardrobeHeroSetupPage>();
        foreach (WardrobeHeroSetupPage page in pages)
        {
            if (page == null || EditorUtility.IsPersistent(page))
                continue;

            if (page.gameObject.activeInHierarchy)
                return page;

            if (fallback == null)
                fallback = page;
        }

        return fallback;
    }

    static CharacterViewManager FindCharacterView()
    {
        CharacterViewManager fallback = null;
        CharacterViewManager[] views = Resources.FindObjectsOfTypeAll<CharacterViewManager>();
        foreach (CharacterViewManager view in views)
        {
            if (view == null || EditorUtility.IsPersistent(view))
                continue;

            if (view.gameObject.activeInHierarchy)
                return view;

            if (fallback == null)
                fallback = view;
        }

        return fallback;
    }

    static void ShowOnStory(CharacterViewManager view, CharacterData character, CharacterPosition position)
    {
        if (view == null || character == null)
            return;

        if (!Application.isPlaying)
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Show Story Preview Character");

        view.SetupCharacter(character, CharacterEmotionType.Idle, position);
        view.DisableUnused(
            position == CharacterPosition.Left,
            position == CharacterPosition.Center,
            position == CharacterPosition.Right);
        MarkStoryPreviewDirty(view);
    }

    static void ClearStoryPosition(CharacterViewManager view, CharacterPosition position)
    {
        if (view == null)
            return;

        if (!Application.isPlaying)
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Clear Story Preview Character");

        view.SetupCharacter(null, CharacterEmotionType.Idle, position);
        MarkStoryPreviewDirty(view);
    }

    static void ClearStoryPreview(CharacterViewManager view)
    {
        if (view == null)
            return;

        if (!Application.isPlaying)
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Clear Story Preview");

        view.DisableUnused(false, false, false);
        MarkStoryPreviewDirty(view);
    }

    static void MarkStoryPreviewDirty(CharacterViewManager view)
    {
        if (view == null)
            return;

        EditorUtility.SetDirty(view);

        if (!Application.isPlaying && view.gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
    }
}
#endif
