using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StoryInterfaceStyleCatalog))]
public sealed class StoryInterfaceStyleCatalogEditor : Editor
{
    SerializedProperty _entries;
    StoryUiStyle _templateStyle;
    Sprite _templateBackgroundSprite;
    bool _createStyleWhenAddingMissing = true;
    Vector2 _missingScroll;

    void OnEnable()
    {
        _entries = serializedObject.FindProperty("_entries");
    }

    [MenuItem("VN/Interface Preview/Select Story UI Catalog", priority = 2)]
    public static void SelectDefaultCatalog()
    {
        StoryInterfaceStyleCatalog catalog = FindDefaultCatalog();
        if (catalog == null)
        {
            const string folder = "Assets/_MyProject/Data/Stories";
            EnsureFolder(folder);
            catalog = ScriptableObject.CreateInstance<StoryInterfaceStyleCatalog>();
            AssetDatabase.CreateAsset(catalog, folder + "/StoryInterfaceStyleCatalog.asset");
            AssetDatabase.SaveAssets();
        }

        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        StoryInterfaceStyleCatalog catalog = (StoryInterfaceStyleCatalog)target;
        CatalogAudit audit = BuildAudit(catalog);

        DrawTopTools(audit);
        EditorGUILayout.Space(8f);
        DrawEntries();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawTopTools(CatalogAudit audit)
    {
        EditorGUILayout.LabelField("Story UI Catalog", EditorStyles.boldLabel);

        string status = $"Историй в проекте: {audit.StoryCount}. В каталоге: {_entries.arraySize}. Без записи: {audit.MissingStories.Count}. Без style asset: {audit.EntriesWithoutStyle}. Дубликатов ID: {audit.DuplicateIds.Count}.";
        MessageType statusType = audit.HasProblems ? MessageType.Warning : MessageType.Info;
        EditorGUILayout.HelpBox(status, statusType);

        _templateStyle = (StoryUiStyle)EditorGUILayout.ObjectField("Шаблон style", _templateStyle, typeof(StoryUiStyle), false);
        _templateBackgroundSprite = (Sprite)EditorGUILayout.ObjectField("Шаблон background", _templateBackgroundSprite, typeof(Sprite), false);
        _createStyleWhenAddingMissing = EditorGUILayout.ToggleLeft("Создавать style asset для новых записей", _createStyleWhenAddingMissing);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Добавить все пропущенные истории"))
                AddMissingStories(audit.MissingStories);

            if (GUILayout.Button("Создать style для записей без style"))
                CreateMissingStylesForEntries();
        }

        if (audit.DuplicateIds.Count > 0)
            EditorGUILayout.HelpBox("Дубли Story ID: " + string.Join(", ", audit.DuplicateIds), MessageType.Warning);

        if (audit.MissingStories.Count > 0)
        {
            EditorGUILayout.LabelField("Пропущенные истории", EditorStyles.boldLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_missingScroll, GUILayout.MinHeight(48f), GUILayout.MaxHeight(120f)))
            {
                _missingScroll = scroll.scrollPosition;
                for (int i = 0; i < audit.MissingStories.Count; i++)
                {
                    StoryData story = audit.MissingStories[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(story, typeof(StoryData), false);
                        if (GUILayout.Button("Добавить", GUILayout.Width(90f)))
                            AddStory(story);
                    }
                }
            }
        }
    }

    void DrawEntries()
    {
        EditorGUILayout.LabelField("Настройки по историям", EditorStyles.boldLabel);

        int removeIndex = -1;
        for (int i = 0; i < _entries.arraySize; i++)
        {
            SerializedProperty entry = _entries.GetArrayElementAtIndex(i);
            string title = ResolveEntryTitle(entry, i);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, title, true);
                if (GUILayout.Button("Preview", GUILayout.Width(70f)))
                    OpenPreview(entry);
                if (GUILayout.Button("-", GUILayout.Width(24f)))
                    removeIndex = i;
            }

            if (entry.isExpanded)
                DrawEntry(entry);

            EditorGUILayout.EndVertical();
        }

        if (removeIndex >= 0)
            _entries.DeleteArrayElementAtIndex(removeIndex);

        if (GUILayout.Button("Добавить пустую запись"))
        {
            int index = _entries.arraySize;
            _entries.InsertArrayElementAtIndex(index);
            ResetEntry(_entries.GetArrayElementAtIndex(index), null);
        }
    }

    void DrawEntry(SerializedProperty entry)
    {
        EditorGUILayout.PropertyField(entry.FindPropertyRelative("_label"), new GUIContent("Метка"));
        EditorGUILayout.PropertyField(entry.FindPropertyRelative("_storyAsset"), new GUIContent("Story asset"));
        EditorGUILayout.PropertyField(entry.FindPropertyRelative("_storyIds"), new GUIContent("Story IDs"), true);
        EditorGUILayout.HelpBox(
            "Story IDs связывают JSON-историю с интерфейсом. Все визуальные настройки этой истории лежат в Style asset ниже: диалог, выборы, статы, заголовок главы, отступы BodyText и layout иконок.",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        SerializedProperty styleProperty = entry.FindPropertyRelative("_storyUiStyle");
        EditorGUILayout.PropertyField(styleProperty, new GUIContent("Story UI Style"));
        EditorGUILayout.PropertyField(entry.FindPropertyRelative("_dialogueBackgroundSprite"), new GUIContent("Dialogue background"));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Создать/назначить style"))
                CreateOrAssignStyle(entry, false);

            if (GUILayout.Button("Скопировать из шаблона"))
                CreateOrAssignStyle(entry, true);

            using (new EditorGUI.DisabledScope(styleProperty.objectReferenceValue == null))
            {
                if (GUILayout.Button("Открыть style"))
                    SelectStyle(styleProperty.objectReferenceValue as StoryUiStyle);
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(entry.FindPropertyRelative("_useSeparateCutsceneStoryUiStyle"), new GUIContent("Separate Cutscene Story UI"));
        if (entry.FindPropertyRelative("_useSeparateCutsceneStoryUiStyle").boolValue)
        {
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("_cutsceneStoryUiStyle"), new GUIContent("Cutscene Story UI Style"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("_cutsceneDialogueBackgroundSprite"), new GUIContent("Cutscene background"));
        }
    }

    void AddMissingStories(List<StoryData> stories)
    {
        if (stories == null || stories.Count == 0)
            return;

        Undo.RecordObject(target, "Add Missing Story UI Entries");
        serializedObject.Update();

        for (int i = 0; i < stories.Count; i++)
            AddStoryInternal(stories[i]);

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
    }

    void AddStory(StoryData story)
    {
        Undo.RecordObject(target, "Add Story UI Entry");
        serializedObject.Update();
        AddStoryInternal(story);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
    }

    void AddStoryInternal(StoryData story)
    {
        int index = _entries.arraySize;
        _entries.InsertArrayElementAtIndex(index);
        SerializedProperty entry = _entries.GetArrayElementAtIndex(index);
        ResetEntry(entry, story);

        if (_createStyleWhenAddingMissing)
            CreateOrAssignStyle(entry, _templateStyle != null);
    }

    void CreateMissingStylesForEntries()
    {
        Undo.RecordObject(target, "Create Missing Story UI Styles");
        serializedObject.Update();

        for (int i = 0; i < _entries.arraySize; i++)
        {
            SerializedProperty entry = _entries.GetArrayElementAtIndex(i);
            SerializedProperty style = entry.FindPropertyRelative("_storyUiStyle");
            if (style != null && style.objectReferenceValue == null)
                CreateOrAssignStyle(entry, _templateStyle != null);
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
    }

    void CreateOrAssignStyle(SerializedProperty entry, bool forceCopyTemplate)
    {
        string storyId = ResolveEntryStoryId(entry);
        if (string.IsNullOrWhiteSpace(storyId))
        {
            EditorUtility.DisplayDialog("Story UI Catalog", "Сначала укажи Story ID или Story asset.", "OK");
            return;
        }

        string uiFolder = ResolveUiFolder(entry, storyId);
        EnsureFolder(uiFolder);

        StoryUiStyle existingStyle = FindExistingStyle(uiFolder);
        StoryUiStyle style = existingStyle;

        if (style == null || forceCopyTemplate)
        {
            string stylePath = AssetDatabase.GenerateUniqueAssetPath(uiFolder + "/" + SafeFileName(storyId) + "_StoryUiStyle.asset");
            if ((forceCopyTemplate || _templateStyle != null) && _templateStyle != null)
            {
                string templatePath = AssetDatabase.GetAssetPath(_templateStyle);
                if (!string.IsNullOrWhiteSpace(templatePath))
                {
                    AssetDatabase.CopyAsset(templatePath, stylePath);
                    style = AssetDatabase.LoadAssetAtPath<StoryUiStyle>(stylePath);
                }
            }

            if (style == null)
            {
                style = ScriptableObject.CreateInstance<StoryUiStyle>();
                AssetDatabase.CreateAsset(style, stylePath);
            }

            style.name = storyId + "_StoryUiStyle";
            EditorUtility.SetDirty(style);
        }

        entry.FindPropertyRelative("_storyUiStyle").objectReferenceValue = style;

        if (_templateBackgroundSprite != null)
            entry.FindPropertyRelative("_dialogueBackgroundSprite").objectReferenceValue = _templateBackgroundSprite;

        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
    }

    void OpenPreview(SerializedProperty entry)
    {
        serializedObject.ApplyModifiedProperties();

        StoryInterfaceStyleCatalog catalog = (StoryInterfaceStyleCatalog)target;
        StoryData story = entry.FindPropertyRelative("_storyAsset").objectReferenceValue as StoryData;
        string storyId = ResolveEntryStoryId(entry);
        StoryJsonAssetLibrary library = FindLibraryForStory(story, storyId);
        StoryInterfacePreviewWindow.OpenForStory(catalog, story, storyId, library);
    }

    static void SelectStyle(StoryUiStyle style)
    {
        if (style == null)
            return;

        Selection.activeObject = style;
        EditorGUIUtility.PingObject(style);
    }

    static void ResetEntry(SerializedProperty entry, StoryData story)
    {
        string storyId = story != null ? Normalize(story.storyId) : "";
        if (string.IsNullOrWhiteSpace(storyId) && story != null)
            storyId = Normalize(story.name);

        entry.isExpanded = true;
        entry.FindPropertyRelative("_label").stringValue = storyId;
        entry.FindPropertyRelative("_storyAsset").objectReferenceValue = story;

        SerializedProperty ids = entry.FindPropertyRelative("_storyIds");
        ids.ClearArray();
        if (!string.IsNullOrWhiteSpace(storyId))
        {
            ids.InsertArrayElementAtIndex(0);
            ids.GetArrayElementAtIndex(0).stringValue = storyId;
        }

        entry.FindPropertyRelative("_storyUiStyle").objectReferenceValue = null;
        entry.FindPropertyRelative("_dialogueBackgroundSprite").objectReferenceValue = null;
        entry.FindPropertyRelative("_useSeparateCutsceneStoryUiStyle").boolValue = false;
        entry.FindPropertyRelative("_cutsceneStoryUiStyle").objectReferenceValue = null;
        entry.FindPropertyRelative("_cutsceneDialogueBackgroundSprite").objectReferenceValue = null;
    }

    CatalogAudit BuildAudit(StoryInterfaceStyleCatalog catalog)
    {
        var audit = new CatalogAudit();
        audit.Stories.AddRange(FindAllStories());
        audit.StoryCount = audit.Stories.Count;

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _entries.arraySize; i++)
        {
            SerializedProperty entry = _entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("_storyUiStyle").objectReferenceValue == null)
                audit.EntriesWithoutStyle++;

            List<string> ids = ResolveEntryStoryIds(entry);
            for (int j = 0; j < ids.Count; j++)
            {
                string id = ids[j];
                if (!seenIds.Add(id) && !audit.DuplicateIds.Contains(id))
                    audit.DuplicateIds.Add(id);
            }
        }

        for (int i = 0; i < audit.Stories.Count; i++)
        {
            StoryData story = audit.Stories[i];
            string storyId = story != null ? story.storyId : "";
            if (story != null && !catalog.TryGetEntry(story, storyId, out _))
                audit.MissingStories.Add(story);
        }

        return audit;
    }

    static List<StoryData> FindAllStories()
    {
        var result = new List<StoryData>();
        string[] guids = AssetDatabase.FindAssets("t:StoryData", new[] { "Assets/_MyProject/Data/Stories" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path.IndexOf("/__EditorTest/", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            StoryData story = AssetDatabase.LoadAssetAtPath<StoryData>(path);
            if (story != null)
                result.Add(story);
        }

        result.Sort((left, right) => string.Compare(left.storyId, right.storyId, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    static StoryInterfaceStyleCatalog FindDefaultCatalog()
    {
        string[] guids = AssetDatabase.FindAssets("t:StoryInterfaceStyleCatalog");
        if (guids == null || guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<StoryInterfaceStyleCatalog>(path);
    }

    static List<string> ResolveEntryStoryIds(SerializedProperty entry)
    {
        var result = new List<string>();

        SerializedProperty ids = entry.FindPropertyRelative("_storyIds");
        if (ids != null)
        {
            for (int i = 0; i < ids.arraySize; i++)
            {
                string id = Normalize(ids.GetArrayElementAtIndex(i).stringValue);
                if (!string.IsNullOrWhiteSpace(id))
                    result.Add(id);
            }
        }

        StoryData story = entry.FindPropertyRelative("_storyAsset").objectReferenceValue as StoryData;
        if (story != null && !string.IsNullOrWhiteSpace(story.storyId))
            result.Add(Normalize(story.storyId));

        return result;
    }

    static string ResolveEntryTitle(SerializedProperty entry, int index)
    {
        string storyId = ResolveEntryStoryId(entry);
        if (!string.IsNullOrWhiteSpace(storyId))
            return storyId;

        string label = entry.FindPropertyRelative("_label").stringValue;
        return string.IsNullOrWhiteSpace(label) ? "Entry " + (index + 1) : label;
    }

    static string ResolveEntryStoryId(SerializedProperty entry)
    {
        SerializedProperty ids = entry.FindPropertyRelative("_storyIds");
        if (ids != null && ids.arraySize > 0)
        {
            string id = Normalize(ids.GetArrayElementAtIndex(0).stringValue);
            if (!string.IsNullOrWhiteSpace(id))
                return id;
        }

        StoryData story = entry.FindPropertyRelative("_storyAsset").objectReferenceValue as StoryData;
        if (story != null && !string.IsNullOrWhiteSpace(story.storyId))
            return Normalize(story.storyId);

        return Normalize(entry.FindPropertyRelative("_label").stringValue);
    }

    static string ResolveUiFolder(SerializedProperty entry, string storyId)
    {
        StoryData story = entry.FindPropertyRelative("_storyAsset").objectReferenceValue as StoryData;
        string root = ResolveStoryRootFolder(story != null ? AssetDatabase.GetAssetPath(story) : "", storyId);
        return root + "/UI";
    }

    static string ResolveStoryRootFolder(string assetPath, string storyId)
    {
        const string storiesRoot = "Assets/_MyProject/Data/Stories/";
        string normalized = (assetPath ?? "").Replace('\\', '/');
        int start = normalized.IndexOf(storiesRoot, StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            string rest = normalized.Substring(start + storiesRoot.Length);
            int slash = rest.IndexOf('/');
            if (slash > 0)
                return storiesRoot + rest.Substring(0, slash);
        }

        return storiesRoot + SafeFileName(storyId);
    }

    static StoryUiStyle FindExistingStyle(string uiFolder)
    {
        string[] guids = AssetDatabase.FindAssets("t:StoryUiStyle", new[] { uiFolder });
        if (guids == null || guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<StoryUiStyle>(path);
    }

    static StoryJsonAssetLibrary FindLibraryForStory(StoryData story, string storyId)
    {
        if (story != null && story.Chapters != null)
        {
            foreach (ChapterData chapter in story.Chapters)
            {
                if (chapter != null && chapter.JsonAssetLibrary != null)
                    return chapter.JsonAssetLibrary;
            }
        }

        string root = ResolveStoryRootFolder(story != null ? AssetDatabase.GetAssetPath(story) : "", storyId);
        string[] guids = AssetDatabase.FindAssets("t:StoryJsonAssetLibrary", new[] { root });
        if (guids != null && guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<StoryJsonAssetLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));

        return null;
    }

    static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static string SafeFileName(string value)
    {
        value = Normalize(value);
        if (string.IsNullOrWhiteSpace(value))
            return "story";

        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value.Replace(' ', '_');
    }

    static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }

    sealed class CatalogAudit
    {
        public readonly List<StoryData> Stories = new List<StoryData>();
        public readonly List<StoryData> MissingStories = new List<StoryData>();
        public readonly List<string> DuplicateIds = new List<string>();
        public int StoryCount;
        public int EntriesWithoutStyle;

        public bool HasProblems => MissingStories.Count > 0 || DuplicateIds.Count > 0 || EntriesWithoutStyle > 0;
    }
}
