#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StoryData))]
public sealed class StoryDataEditor : Editor
{
    SerializedProperty _script;
    SerializedProperty _storyId;
    SerializedProperty _storyName;
    SerializedProperty _chapters;
    SerializedProperty _storyUiStyle;
    SerializedProperty _dialogueBackgroundSprite;
    SerializedProperty _useSeparateCutsceneStoryUiStyle;
    SerializedProperty _cutsceneStoryUiStyle;
    SerializedProperty _cutsceneDialogueBackgroundSprite;

    bool _showFallbackUiFields;

    void OnEnable()
    {
        _script = serializedObject.FindProperty("m_Script");
        _storyId = serializedObject.FindProperty("_storyId");
        _storyName = serializedObject.FindProperty("_storyName");
        _chapters = serializedObject.FindProperty("_chapters");
        _storyUiStyle = serializedObject.FindProperty("_storyUiStyle");
        _dialogueBackgroundSprite = serializedObject.FindProperty("_dialogueBackgroundSprite");
        _useSeparateCutsceneStoryUiStyle = serializedObject.FindProperty("_useSeparateCutsceneStoryUiStyle");
        _cutsceneStoryUiStyle = serializedObject.FindProperty("_cutsceneStoryUiStyle");
        _cutsceneDialogueBackgroundSprite = serializedObject.FindProperty("_cutsceneDialogueBackgroundSprite");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(_script);

        EditorGUILayout.PropertyField(_storyId, new GUIContent("Story ID"));
        EditorGUILayout.PropertyField(_storyName, new GUIContent("Название истории"));
        EditorGUILayout.PropertyField(_chapters, new GUIContent("Главы"), true);

        DrawStoryInterfaceBlock();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawStoryInterfaceBlock()
    {
        StoryData story = (StoryData)target;
        string storyId = StoryInterfaceEditorUtility.ResolveStoryId(story, _storyId != null ? _storyId.stringValue : "");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Интерфейс по Story ID", EditorStyles.boldLabel);

        bool hasEntry = StoryInterfaceEditorUtility.TryGetCatalogEntry(
            story,
            storyId,
            out StoryInterfaceStyleCatalog catalog,
            out StoryInterfaceStyleEntry entry);

        if (catalog == null)
        {
            EditorGUILayout.HelpBox("Story UI Catalog не найден. Без него новая история будет брать только fallback-поля ниже.", MessageType.Warning);
        }
        else if (!hasEntry)
        {
            EditorGUILayout.HelpBox($"Для Story ID '{storyId}' нет записи в Story UI Catalog. Добавь историю в каталог, чтобы интерфейс выбирался автоматически.", MessageType.Warning);
        }
        else
        {
            StoryUiStyle style = entry.StoryUiStyle;
            string styleName = style != null ? style.name : "style не назначен";
            EditorGUILayout.HelpBox($"Подключено через каталог: {StoryInterfaceEditorUtility.ResolveEntryStoryId(entry)} -> {styleName}. Это главный источник UI для истории.", MessageType.Info);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Открыть каталог"))
                StoryInterfaceStyleCatalogEditor.SelectDefaultCatalog();

            using (new EditorGUI.DisabledScope(!hasEntry || entry.StoryUiStyle == null))
            {
                if (GUILayout.Button("Открыть style"))
                    StoryInterfaceEditorUtility.SelectAndPing(entry.StoryUiStyle);
            }

            if (GUILayout.Button("Предпросмотр UI"))
                OpenPreview(catalog, story, storyId);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!hasEntry))
            {
                if (GUILayout.Button("Синхронизировать fallback из каталога"))
                    CopyCatalogEntryToFallback(entry);
            }

            if (GUILayout.Button("Очистить fallback UI"))
                ClearFallbackUiFields();
        }

        _showFallbackUiFields = EditorGUILayout.Foldout(_showFallbackUiFields, "Fallback UI поля StoryData", true);
        if (_showFallbackUiFields)
        {
            EditorGUILayout.HelpBox(
                "Обычно эти поля не трогаются. Они нужны только как запасной вариант, если для истории нет записи в Story UI Catalog.",
                MessageType.None);
            EditorGUILayout.PropertyField(_storyUiStyle, new GUIContent("Fallback Story UI Style"));
            EditorGUILayout.PropertyField(_dialogueBackgroundSprite, new GUIContent("Запасной background"));
            EditorGUILayout.PropertyField(_useSeparateCutsceneStoryUiStyle, new GUIContent("Separate Cutscene Story UI"));
            using (new EditorGUI.DisabledScope(_useSeparateCutsceneStoryUiStyle == null || !_useSeparateCutsceneStoryUiStyle.boolValue))
            {
                EditorGUILayout.PropertyField(_cutsceneStoryUiStyle, new GUIContent("Fallback Cutscene Story UI Style"));
                EditorGUILayout.PropertyField(_cutsceneDialogueBackgroundSprite, new GUIContent("Запасной background катсцен"));
            }
        }
    }

    void OpenPreview(StoryInterfaceStyleCatalog catalog, StoryData story, string storyId)
    {
        serializedObject.ApplyModifiedProperties();
        StoryJsonAssetLibrary library = StoryInterfaceEditorUtility.FindLibraryForStory(story, storyId);
        StoryInterfacePreviewWindow.OpenForStory(catalog, story, storyId, library);
    }

    void CopyCatalogEntryToFallback(StoryInterfaceStyleEntry entry)
    {
        if (entry == null)
            return;

        Undo.RecordObject(target, "Sync StoryData UI Fallback From Catalog");
        _storyUiStyle.objectReferenceValue = entry.StoryUiStyle;
        _dialogueBackgroundSprite.objectReferenceValue = entry.DialogueBackgroundSprite;
        _useSeparateCutsceneStoryUiStyle.boolValue = entry.UseSeparateCutsceneStoryUiStyle;
        _cutsceneStoryUiStyle.objectReferenceValue = entry.CutsceneStoryUiStyle;
        _cutsceneDialogueBackgroundSprite.objectReferenceValue = entry.CutsceneDialogueBackgroundSprite;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    void ClearFallbackUiFields()
    {
        Undo.RecordObject(target, "Clear StoryData UI Fallback");
        _storyUiStyle.objectReferenceValue = null;
        _dialogueBackgroundSprite.objectReferenceValue = null;
        _useSeparateCutsceneStoryUiStyle.boolValue = false;
        _cutsceneStoryUiStyle.objectReferenceValue = null;
        _cutsceneDialogueBackgroundSprite.objectReferenceValue = null;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }
}
#endif
