#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StoryJsonAssetLibrary))]
public sealed class StoryJsonAssetLibraryEditor : Editor
{
    private static readonly string[] AssetFieldNames =
    {
        "_character",
        "_clothing",
        "_sprite",
        "_video",
        "_textAsset",
        "_audio",
        "_dialogueStyle"
    };

    private static readonly string[] AssetFieldLabels =
    {
        "Character",
        "Clothing",
        "Sprite",
        "Video",
        "Text",
        "Audio",
        "Style"
    };

    private SerializedProperty _script;
    private SerializedProperty _storyUiStyle;
    private SerializedProperty _dialogueBackgroundSprite;
    private SerializedProperty _useSeparateCutsceneStoryUiStyle;
    private SerializedProperty _cutsceneStoryUiStyle;
    private SerializedProperty _cutsceneDialogueBackgroundSprite;
    private SerializedProperty _assets;
    private string _searchText = "";
    private UnityEngine.Object _assetFilter;
    private bool _showMissingOnly;
    private bool _showUiFallbackFields;
    private Vector2 _scroll;

    private readonly List<int> _visibleIndices = new List<int>();

    private void OnEnable()
    {
        _script = serializedObject.FindProperty("m_Script");
        _storyUiStyle = serializedObject.FindProperty("_storyUiStyle");
        _dialogueBackgroundSprite = serializedObject.FindProperty("_dialogueBackgroundSprite");
        _useSeparateCutsceneStoryUiStyle = serializedObject.FindProperty("_useSeparateCutsceneStoryUiStyle");
        _cutsceneStoryUiStyle = serializedObject.FindProperty("_cutsceneStoryUiStyle");
        _cutsceneDialogueBackgroundSprite = serializedObject.FindProperty("_cutsceneDialogueBackgroundSprite");
        _assets = serializedObject.FindProperty("_assets");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptField();
        DrawStoryUiStyleFields();

        if (_assets == null || !_assets.isArray)
        {
            EditorGUILayout.HelpBox("Asset list property was not found.", MessageType.Error);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawSearchTools();
        DrawSortTools();

        BuildVisibleIndices();
        DrawSummary();
        DrawAssetList();
        DrawAddButton();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawScriptField()
    {
        if (_script == null)
            return;

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(_script);
    }

    private void DrawCatalogDrivenStoryUiStyleFields()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Интерфейс истории", EditorStyles.boldLabel);

        if (_storyUiStyle == null || _dialogueBackgroundSprite == null)
        {
            EditorGUILayout.HelpBox("Поля style не найдены. Дождись перекомпиляции Unity.", MessageType.Warning);
            return;
        }

        StoryJsonAssetLibrary library = (StoryJsonAssetLibrary)target;
        StoryData ownerStory = StoryInterfaceEditorUtility.FindStoryForLibrary(library);
        string storyId = StoryInterfaceEditorUtility.ResolveStoryId(ownerStory, GuessStoryIdFromLibraryPath(library));

        bool hasEntry = StoryInterfaceEditorUtility.TryGetCatalogEntry(
            ownerStory,
            storyId,
            out StoryInterfaceStyleCatalog catalog,
            out StoryInterfaceStyleEntry entry);

        if (ownerStory == null)
        {
            EditorGUILayout.HelpBox(
                "Эта библиотека не найдена в ChapterData ни одной истории. UI всё равно можно настроить вручную, но лучше подключить библиотеку к главе истории.",
                MessageType.Warning);
        }
        else if (!hasEntry)
        {
            EditorGUILayout.HelpBox(
                $"Для Story ID '{storyId}' нет записи в Story UI Catalog. Основной UI лучше задавать там, а поля этой библиотеки оставить fallback.",
                MessageType.Warning);
        }
        else
        {
            string styleName = entry.StoryUiStyle != null ? entry.StoryUiStyle.name : "style не назначен";
            EditorGUILayout.HelpBox(
                $"UI берётся из Story UI Catalog: {StoryInterfaceEditorUtility.ResolveEntryStoryId(entry)} -> {styleName}. Поля ниже используются только как fallback.",
                MessageType.Info);
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
                StoryInterfacePreviewWindow.OpenForStory(catalog, ownerStory, storyId, library);
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

        _showUiFallbackFields = EditorGUILayout.Foldout(_showUiFallbackFields, "Fallback UI поля библиотеки", true);
        if (_showUiFallbackFields)
        {
            EditorGUILayout.HelpBox(
                "Обычно эти поля не трогаются. Они нужны только для старых историй или как запасной вариант, если Story UI Catalog не дал style.",
                MessageType.None);

            DrawProperty(_storyUiStyle, "Fallback Story UI Style");
            DrawProperty(_dialogueBackgroundSprite, "Запасной background");
            DrawProperty(_useSeparateCutsceneStoryUiStyle, "Separate Cutscene Story UI");

            bool useSeparateCutsceneStyle =
                _useSeparateCutsceneStoryUiStyle != null &&
                _useSeparateCutsceneStoryUiStyle.boolValue;

            using (new EditorGUI.DisabledScope(!useSeparateCutsceneStyle))
            {
                DrawProperty(_cutsceneStoryUiStyle, "Fallback Cutscene Story UI Style");
                DrawProperty(_cutsceneDialogueBackgroundSprite, "Запасной background катсцен");
            }
        }
    }

    private void DrawStoryUiStyleFields()
    {
        DrawCatalogDrivenStoryUiStyleFields();
    }

    private void DrawLegacyStoryUiStyleFields()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Story UI Style", EditorStyles.boldLabel);

        DrawProperty(_storyUiStyle, "Story UI Style");
        DrawProperty(_dialogueBackgroundSprite, "Dialogue Panel Sprite");

        if (_storyUiStyle == null || _dialogueBackgroundSprite == null)
        {
            EditorGUILayout.HelpBox("Поля стиля плашки не найдены. Дождись перекомпиляции Unity.", MessageType.Warning);
            return;
        }

        bool hasRegularStyle =
            _storyUiStyle.objectReferenceValue != null ||
            _dialogueBackgroundSprite.objectReferenceValue != null;

        if (!hasRegularStyle)
        {
            EditorGUILayout.HelpBox(
                "Assign a Story UI Style here. The style can change the dialogue panel, choice buttons, and stat overlay for this JSON story.",
                MessageType.Info);
        }

        DrawProperty(_useSeparateCutsceneStoryUiStyle, "Separate Cutscene Style");

        bool useSeparateCutsceneStyle =
            _useSeparateCutsceneStoryUiStyle != null &&
            _useSeparateCutsceneStoryUiStyle.boolValue;

        using (new EditorGUI.DisabledScope(!useSeparateCutsceneStyle))
        {
            DrawProperty(_cutsceneStoryUiStyle, "Cutscene Story UI Style");
            DrawProperty(_cutsceneDialogueBackgroundSprite, "Cutscene Background Sprite");
        }
    }

    private void CopyCatalogEntryToFallback(StoryInterfaceStyleEntry entry)
    {
        if (entry == null)
            return;

        Undo.RecordObject(target, "Sync Story JSON Library UI Fallback From Catalog");
        _storyUiStyle.objectReferenceValue = entry.StoryUiStyle;
        _dialogueBackgroundSprite.objectReferenceValue = entry.DialogueBackgroundSprite;
        _useSeparateCutsceneStoryUiStyle.boolValue = entry.UseSeparateCutsceneStoryUiStyle;
        _cutsceneStoryUiStyle.objectReferenceValue = entry.CutsceneStoryUiStyle;
        _cutsceneDialogueBackgroundSprite.objectReferenceValue = entry.CutsceneDialogueBackgroundSprite;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private void ClearFallbackUiFields()
    {
        Undo.RecordObject(target, "Clear Story JSON Library UI Fallback");
        _storyUiStyle.objectReferenceValue = null;
        _dialogueBackgroundSprite.objectReferenceValue = null;
        _useSeparateCutsceneStoryUiStyle.boolValue = false;
        _cutsceneStoryUiStyle.objectReferenceValue = null;
        _cutsceneDialogueBackgroundSprite.objectReferenceValue = null;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static string GuessStoryIdFromLibraryPath(StoryJsonAssetLibrary library)
    {
        string path = AssetDatabase.GetAssetPath(library);
        string root = StoryInterfaceEditorUtility.ResolveStoryRootFolder(path, "");
        if (string.IsNullOrWhiteSpace(root))
            return "";

        int slash = root.LastIndexOf('/');
        return slash >= 0 ? root.Substring(slash + 1) : root;
    }

    private static void DrawProperty(SerializedProperty property, string label)
    {
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private void DrawSearchTools()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Search", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _searchText = EditorGUILayout.TextField(_searchText);
        if (GUILayout.Button("Clear", GUILayout.Width(54f)))
        {
            _searchText = "";
            _assetFilter = null;
            _showMissingOnly = false;
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        _assetFilter = EditorGUILayout.ObjectField("Asset filter", _assetFilter, typeof(UnityEngine.Object), false);
        _showMissingOnly = EditorGUILayout.ToggleLeft("Show entries without assigned assets", _showMissingOnly);
    }

    private void DrawSortTools()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Sort", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Id A-Z"))
            SortAssets(CompareById);

        if (GUILayout.Button("Type + Id"))
            SortAssets(CompareByTypeThenId);

        if (GUILayout.Button("Asset Name"))
            SortAssets(CompareByAssetName);

        if (GUILayout.Button("Missing First"))
            SortAssets(CompareMissingFirst);
        EditorGUILayout.EndHorizontal();
    }

    private void BuildVisibleIndices()
    {
        _visibleIndices.Clear();

        for (int i = 0; i < _assets.arraySize; i++)
        {
            SerializedProperty element = _assets.GetArrayElementAtIndex(i);
            if (EntryMatchesFilter(element))
                _visibleIndices.Add(i);
        }
    }

    private void DrawSummary()
    {
        EditorGUILayout.Space(4f);
        string label = HasActiveFilter()
            ? "Assets: " + _visibleIndices.Count + " / " + _assets.arraySize
            : "Assets: " + _assets.arraySize;
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
    }

    private void DrawAssetList()
    {
        if (_visibleIndices.Count == 0)
        {
            EditorGUILayout.HelpBox("No matching assets. Try another id, asset name, file path, or drag an asset into Asset filter.", MessageType.Info);
            return;
        }

        int removeIndex = -1;
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(220f));

        for (int visible = 0; visible < _visibleIndices.Count; visible++)
        {
            int index = _visibleIndices[visible];
            SerializedProperty element = _assets.GetArrayElementAtIndex(index);
            if (DrawEntry(index, element))
                removeIndex = index;
        }

        EditorGUILayout.EndScrollView();

        if (removeIndex >= 0)
        {
            Undo.RecordObject(target, "Remove Story JSON Asset Reference");
            _assets.DeleteArrayElementAtIndex(removeIndex);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            GUIUtility.ExitGUI();
        }
    }

    private bool DrawEntry(int index, SerializedProperty element)
    {
        EntryData data = CreateEntryData(element, index);
        bool removeRequested = false;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        string header = "#" + index.ToString("00") + "  " + data.IdLabel + "  [" + data.TypeLabel + "]  " + data.AssetLabel;
        element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, header, true);

        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(data.Id)))
        {
            if (GUILayout.Button("Copy Id", GUILayout.Width(62f)))
                EditorGUIUtility.systemCopyBuffer = data.Id;
        }

        using (new EditorGUI.DisabledScope(data.PrimaryAsset == null))
        {
            if (GUILayout.Button("Ping", GUILayout.Width(44f)))
                EditorGUIUtility.PingObject(data.PrimaryAsset);

            if (GUILayout.Button("Select", GUILayout.Width(54f)))
            {
                Selection.activeObject = data.PrimaryAsset;
                EditorGUIUtility.PingObject(data.PrimaryAsset);
            }
        }

        if (GUILayout.Button("-", GUILayout.Width(24f)))
            removeRequested = true;

        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(data.PrimaryAssetPath))
            EditorGUILayout.LabelField(data.PrimaryAssetPath, EditorStyles.miniLabel);

        if (element.isExpanded)
        {
            EditorGUI.indentLevel++;
            DrawRelativeProperty(element, "_id", "Id");
            DrawRelativeProperty(element, "_character", "Character");
            DrawRelativeProperty(element, "_clothing", "Clothing");
            DrawRelativeProperty(element, "_sprite", "Sprite");
            DrawRelativeProperty(element, "_video", "Video");
            DrawRelativeProperty(element, "_textAsset", "Text Asset");
            DrawRelativeProperty(element, "_audio", "Audio");
            DrawRelativeProperty(element, "_dialogueStyle", "Dialogue Style");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        return removeRequested;
    }

    private void DrawAddButton()
    {
        EditorGUILayout.Space(4f);
        if (!GUILayout.Button("Add Empty Asset Reference"))
            return;

        Undo.RecordObject(target, "Add Story JSON Asset Reference");
        int index = _assets.arraySize;
        _assets.InsertArrayElementAtIndex(index);
        ClearEntry(_assets.GetArrayElementAtIndex(index));
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        GUIUtility.ExitGUI();
    }

    private void DrawRelativeProperty(SerializedProperty element, string propertyName, string label)
    {
        SerializedProperty property = element.FindPropertyRelative(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private void SortAssets(Comparison<EntryData> comparison)
    {
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();

        Undo.RecordObject(target, "Sort Story JSON Asset Library");

        List<EntryData> desired = new List<EntryData>();
        List<int> currentOrder = new List<int>();

        for (int i = 0; i < _assets.arraySize; i++)
        {
            desired.Add(CreateEntryData(_assets.GetArrayElementAtIndex(i), i));
            currentOrder.Add(i);
        }

        desired.Sort(comparison);

        for (int targetIndex = 0; targetIndex < desired.Count; targetIndex++)
        {
            int originalIndex = desired[targetIndex].OriginalIndex;
            int currentIndex = currentOrder.IndexOf(originalIndex);
            if (currentIndex < 0 || currentIndex == targetIndex)
                continue;

            _assets.MoveArrayElement(currentIndex, targetIndex);
            currentOrder.RemoveAt(currentIndex);
            currentOrder.Insert(targetIndex, originalIndex);
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        GUIUtility.ExitGUI();
    }

    private bool EntryMatchesFilter(SerializedProperty element)
    {
        EntryData data = CreateEntryData(element, -1);

        if (_showMissingOnly && !data.IsMissing)
            return false;

        if (_assetFilter != null && !MatchesAssetFilter(element, _assetFilter))
            return false;

        if (string.IsNullOrWhiteSpace(_searchText))
            return true;

        string[] tokens = _searchText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i].Trim();
            if (token.Length == 0)
                continue;

            if (data.SearchText.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        return true;
    }

    private bool HasActiveFilter()
    {
        return !string.IsNullOrWhiteSpace(_searchText) || _assetFilter != null || _showMissingOnly;
    }

    private static bool MatchesAssetFilter(SerializedProperty element, UnityEngine.Object filter)
    {
        if (filter == null)
            return true;

        string filterPath = AssetDatabase.GetAssetPath(filter);

        for (int i = 0; i < AssetFieldNames.Length; i++)
        {
            UnityEngine.Object asset = GetObject(element, AssetFieldNames[i]);
            if (asset == null)
                continue;

            if (asset == filter)
                return true;

            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(assetPath) &&
                !string.IsNullOrEmpty(filterPath) &&
                string.Equals(assetPath, filterPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static EntryData CreateEntryData(SerializedProperty element, int originalIndex)
    {
        string id = GetString(element, "_id");
        List<string> types = new List<string>();
        List<string> names = new List<string>();
        List<string> paths = new List<string>();
        UnityEngine.Object primaryAsset = null;
        string primaryPath = "";

        for (int i = 0; i < AssetFieldNames.Length; i++)
        {
            UnityEngine.Object asset = GetObject(element, AssetFieldNames[i]);
            if (asset == null)
                continue;

            types.Add(AssetFieldLabels[i]);
            names.Add(asset.name);

            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
                paths.Add(path);

            if (primaryAsset == null)
            {
                primaryAsset = asset;
                primaryPath = path;
            }
        }

        string typeLabel = types.Count > 0 ? string.Join("+", types.ToArray()) : "Missing";
        string assetLabel = names.Count > 0 ? string.Join(", ", names.ToArray()) : "<none>";
        string searchText = string.Join(" ", new[]
        {
            id ?? "",
            typeLabel,
            assetLabel,
            string.Join(" ", paths.ToArray())
        });

        return new EntryData
        {
            OriginalIndex = originalIndex,
            Id = id ?? "",
            IdLabel = string.IsNullOrWhiteSpace(id) ? "<empty id>" : id,
            TypeLabel = typeLabel,
            AssetLabel = assetLabel,
            PrimaryAsset = primaryAsset,
            PrimaryAssetPath = primaryPath ?? "",
            IsMissing = primaryAsset == null,
            SearchText = searchText
        };
    }

    private static string GetString(SerializedProperty element, string propertyName)
    {
        SerializedProperty property = element.FindPropertyRelative(propertyName);
        return property != null ? property.stringValue : "";
    }

    private static UnityEngine.Object GetObject(SerializedProperty element, string propertyName)
    {
        SerializedProperty property = element.FindPropertyRelative(propertyName);
        return property != null ? property.objectReferenceValue : null;
    }

    private static void ClearEntry(SerializedProperty element)
    {
        SerializedProperty id = element.FindPropertyRelative("_id");
        if (id != null)
            id.stringValue = "";

        for (int i = 0; i < AssetFieldNames.Length; i++)
        {
            SerializedProperty property = element.FindPropertyRelative(AssetFieldNames[i]);
            if (property != null)
                property.objectReferenceValue = null;
        }
    }

    private static int CompareById(EntryData left, EntryData right)
    {
        int result = CompareText(left.Id, right.Id);
        return result != 0 ? result : left.OriginalIndex.CompareTo(right.OriginalIndex);
    }

    private static int CompareByTypeThenId(EntryData left, EntryData right)
    {
        int result = CompareText(left.TypeLabel, right.TypeLabel);
        if (result != 0)
            return result;

        return CompareById(left, right);
    }

    private static int CompareByAssetName(EntryData left, EntryData right)
    {
        int result = CompareText(left.AssetLabel, right.AssetLabel);
        if (result != 0)
            return result;

        return CompareById(left, right);
    }

    private static int CompareMissingFirst(EntryData left, EntryData right)
    {
        int result = right.IsMissing.CompareTo(left.IsMissing);
        if (result != 0)
            return result;

        return CompareById(left, right);
    }

    private static int CompareText(string left, string right)
    {
        return string.Compare(left ?? "", right ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class EntryData
    {
        public int OriginalIndex;
        public string Id;
        public string IdLabel;
        public string TypeLabel;
        public string AssetLabel;
        public UnityEngine.Object PrimaryAsset;
        public string PrimaryAssetPath;
        public bool IsMissing;
        public string SearchText;
    }
}
#endif
