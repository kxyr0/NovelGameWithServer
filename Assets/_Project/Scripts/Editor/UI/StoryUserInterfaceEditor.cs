#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(StoryUserInterface))]
public sealed class StoryUserInterfaceEditor : Editor
{
    static string _previewContactName = "\u0420\u043E\u0431";
    static string _previewScript =
        "Мэг: У меня будет подкаст с Габриэлем Мортеллоом!!!\n" +
        "{PlayerName}: С кем?\n" +
        "Мэг: Стыдно не знать, с твоей-то профессией))\n" +
        "Мэг: Фото [фото]";
    static PhoneDialogueNode _previewNode;
    static bool _showEndScreenReferences;
    static bool _showEndScreenRoot = true;
    static bool _showEndScreenTexts = true;
    static bool _showEndScreenStats = true;
    static bool _showEndScreenLayout;
    static bool _showEndScreenPreview;
    static bool _showEndScreenStatBindings = true;
    static bool _liveEndScreenPreview = true;
    static readonly Dictionary<string, bool> _endScreenFoldoutState = new Dictionary<string, bool>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        StoryUserInterface storyUserInterface = (StoryUserInterface)target;
        DrawSectionTitle("Телефон");
        DrawProperty("_applyPhoneConfigurationOnEnable", "Применять при включении");

        DrawSectionTitle("Ссылки UI телефона");
        DrawProperty("_phoneReferences", "Ссылки UI телефона");

        DrawSectionTitle("Шаблоны сообщений");
        DrawTemplateSummary(storyUserInterface);

        DrawSectionTitle("Layout сообщений");
        DrawProperty("_phoneLayoutSettings", "Layout сообщений");

        DrawSectionTitle("Предпросмотр телефона");
        DrawPreviewFields(storyUserInterface);
        DrawPreviewButtons(storyUserInterface);

        DrawSectionTitle("Диагностика телефона");
        DrawDiagnostics(storyUserInterface);

        DrawSectionTitle("Финальный экран");
        EditorGUI.BeginChangeCheck();
        DrawEndScreenSection(storyUserInterface);
        bool endScreenChanged = EditorGUI.EndChangeCheck();

        serializedObject.ApplyModifiedProperties();
        if (_liveEndScreenPreview && endScreenChanged)
            RefreshEndScreenLivePreview(storyUserInterface);
    }

    void DrawSectionTitle(string title)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    static GUIContent Content(string label, string tooltip = null)
    {
        return new GUIContent(label, tooltip ?? "");
    }

    static string GetPropertyTooltip(string propertyName)
    {
        switch (propertyName)
        {
            case "_applyEndScreenConfigurationOnEnable":
                return "Автоматически применяет настройки финального экрана из StoryUserInterface при Awake/OnEnable.";
            case "root":
                return "Главный объект финального экрана.";
            case "canvasGroup":
                return "CanvasGroup финального экрана для alpha/interactable/blocksRaycasts.";
            case "safeArea":
                return "RectTransform, которому применяются отступы safe area.";
            case "panelRoot":
                return "Основной RectTransform панели финального экрана.";
            case "backgroundImage":
                return "Image, в который ставится фон финального экрана.";
            case "backgroundOverride":
                return "Фон именно для этой истории. Если пусто, используется fallback/default.";
            case "defaultBackground":
                return "Запасной фон, если story override не задан.";
            case "titleText":
                return "Текст заголовка, например «Серия окончена».";
            case "storyTitleText":
                return "Опциональный текст названия истории.";
            case "completedEpisodeText":
                return "Опциональный текст завершённой серии.";
            case "nextEpisodeText":
                return "Опциональный текст следующей серии.";
            case "statsContainer":
                return "Родитель для авто-созданных строк. Если строки уже стоят на сцене, нажми «Собрать с экрана» и это поле можно не трогать.";
            case "statRowTemplate":
                return "Шаблон для авто-создания строк. Не нужен, если ты собираешь уже готовые строки с экрана.";
            case "statsBackgroundImage":
                return "Опциональный общий фон под блоком статов.";
            case "statsBackgroundOverride":
                return "Спрайт общего фона блока статов для этой истории.";
            case "hideStatsBackground":
                return "Скрыть общий фон блока статов.";
            case "continueButton":
                return "Единая кнопка «Продолжить»: запускает следующую серию или возвращает в меню.";
            case "continueButtonPlateImage":
                return "Image, на котором лежит плашка кнопки Продолжить.";
            case "continueButtonPlateSprite":
                return "Итоговый sprite плашки кнопки.";
            case "continueButtonPlateSpriteSource":
                return "Источник sprite: файл, выбранный объект или sprite из сцены.";
            case "continueButtonText":
                return "TMP-текст кнопки. Код его не меняет.";
            default:
                return "";
        }
    }

    static void DrawStatProperty(SerializedProperty property, string label, string tooltip, bool includeChildren = false)
    {
        if (property == null)
            return;

        EditorGUILayout.PropertyField(property, Content(label, tooltip), includeChildren);
    }

    static void DrawGroupTitle(string title, string hint = null)
    {
        EditorGUILayout.Space(5f);
        EditorGUILayout.LabelField(Content(title, hint), EditorStyles.boldLabel);
        if (!string.IsNullOrWhiteSpace(hint))
            EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);
    }

    static bool DrawStateFoldout(string key, string title, bool defaultExpanded)
    {
        if (!_endScreenFoldoutState.TryGetValue(key, out bool expanded))
            expanded = defaultExpanded;

        expanded = EditorGUILayout.Foldout(expanded, title, true, EditorStyles.foldoutHeader);
        _endScreenFoldoutState[key] = expanded;
        return expanded;
    }

    static void DrawEndSpriteRow(
        string label,
        SerializedProperty spriteProperty,
        SerializedProperty sourceProperty,
        Action copyFromScene)
    {
        if (spriteProperty == null)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            UnityEngine.Object sourceObject = sourceProperty != null
                ? sourceProperty.objectReferenceValue
                : null;
            if (sourceObject == null && spriteProperty.objectReferenceValue != null)
            {
                sourceObject = spriteProperty.objectReferenceValue;
                if (sourceProperty != null)
                    sourceProperty.objectReferenceValue = sourceObject;
            }

            EditorGUI.BeginChangeCheck();
            UnityEngine.Object pickedSource = EditorGUILayout.ObjectField(
                Content("Источник", "Файл, sprite или объект сцены, из которого взять картинку."),
                sourceObject,
                typeof(UnityEngine.Object),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                if (pickedSource == null)
                {
                    if (sourceProperty != null)
                        sourceProperty.objectReferenceValue = null;
                }
                else
                {
                    AssignSpriteFromObject(spriteProperty, sourceProperty, pickedSource);
                }
            }

            EditorGUI.BeginChangeCheck();
            Sprite pickedSprite = (Sprite)EditorGUILayout.ObjectField(
                Content("Итоговый Sprite", "Именно этот sprite будет применён к Image."),
                spriteProperty.objectReferenceValue,
                typeof(Sprite),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                spriteProperty.objectReferenceValue = pickedSprite;
                if (sourceProperty != null)
                    sourceProperty.objectReferenceValue = pickedSprite;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Content("Файл...", "Выбрать png/jpg/svg из проекта или с диска.")))
                    AssignSpriteFromExternalFile(spriteProperty, sourceProperty);

                using (new EditorGUI.DisabledScope(Selection.activeObject == null))
                {
                    if (GUILayout.Button(Content("Выбранное", "Взять sprite из выбранного объекта Project/Hierarchy.")))
                        AssignSpriteFromObject(spriteProperty, sourceProperty, Selection.activeObject);
                }

                if (copyFromScene != null && GUILayout.Button(Content("из сцены", "Скопировать sprite из назначенного Image.")))
                    copyFromScene();

                if (GUILayout.Button(Content("Очистить", "Очистить источник и итоговый sprite.")))
                {
                    spriteProperty.objectReferenceValue = null;
                    if (sourceProperty != null)
                        sourceProperty.objectReferenceValue = null;
                }

                using (new EditorGUI.DisabledScope(spriteProperty.objectReferenceValue == null))
                {
                    if (GUILayout.Button(Content("Ping Sprite", "Показать итоговый sprite в Project.")))
                        EditorGUIUtility.PingObject(spriteProperty.objectReferenceValue);
                }
            }
        }
    }

    static bool AssignSpriteFromExternalFile(SerializedProperty spriteProperty, SerializedProperty sourceProperty)
    {
        string sourcePath = EditorUtility.OpenFilePanelWithFilters(
            "Выбрать sprite для финального экрана",
            "",
            new[] { "Images", "png,jpg,jpeg,svg", "All files", "*" });
        if (string.IsNullOrWhiteSpace(sourcePath))
            return false;

        string assetPath = ConvertOrImportSpritePath(sourcePath);
        Sprite sprite = LoadSpriteAtPath(assetPath);
        if (sprite == null)
            return false;

        spriteProperty.objectReferenceValue = sprite;
        if (sourceProperty != null)
            sourceProperty.objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(assetPath) ?? sprite;
        return true;
    }

    static bool AssignSpriteFromObject(SerializedProperty spriteProperty, SerializedProperty sourceProperty, UnityEngine.Object source)
    {
        Sprite sprite = ResolveSpriteFromObject(source);
        if (sprite == null)
            return false;

        spriteProperty.objectReferenceValue = sprite;
        if (sourceProperty != null)
            sourceProperty.objectReferenceValue = source != null ? source : sprite;
        return true;
    }

    static Sprite ResolveSpriteFromObject(UnityEngine.Object source)
    {
        if (source == null)
            return null;
        if (source is Sprite sprite)
            return sprite;
        if (source is Image image)
            return image.sprite;
        if (source is Button button)
            return ResolveButtonPlateImage(button)?.sprite;
        if (source is GameObject gameObject)
        {
            Image objectImage = gameObject.GetComponent<Image>() ?? gameObject.GetComponentInChildren<Image>(true);
            return objectImage != null ? objectImage.sprite : LoadSpriteAtPath(AssetDatabase.GetAssetPath(source));
        }
        if (source is Component component)
        {
            Image componentImage = component.GetComponent<Image>() ?? component.GetComponentInChildren<Image>(true);
            return componentImage != null ? componentImage.sprite : null;
        }

        return LoadSpriteAtPath(AssetDatabase.GetAssetPath(source));
    }

    static string ConvertOrImportSpritePath(string absolutePath)
    {
        string normalizedPath = Path.GetFullPath(absolutePath).Replace('\\', '/');
        string projectAssetsPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
        if (normalizedPath.StartsWith(projectAssetsPath, StringComparison.OrdinalIgnoreCase))
            return "Assets" + normalizedPath.Substring(projectAssetsPath.Length);

        string importDirectory = "Assets/_Project/Art/ImportedEndScreenSprites";
        Directory.CreateDirectory(importDirectory);
        string destination = AssetDatabase.GenerateUniqueAssetPath(importDirectory + "/" + Path.GetFileName(normalizedPath));
        File.Copy(normalizedPath, destination, overwrite: false);
        AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
        EnsureSpriteImporter(destination);
        return destination;
    }

    static Sprite LoadSpriteAtPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        EnsureSpriteImporter(assetPath);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
            return sprite;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite nestedSprite)
                return nestedSprite;
        }

        return null;
    }

    static void EnsureSpriteImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null || importer.textureType == TextureImporterType.Sprite)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.SaveAndReimport();
    }

    static void CopySpriteFromImage(SerializedProperty imageProperty, SerializedProperty spriteProperty, SerializedProperty sourceProperty)
    {
        if (imageProperty == null || spriteProperty == null)
            return;

        Image image = imageProperty.objectReferenceValue as Image;
        if (image == null || image.sprite == null)
            return;

        spriteProperty.objectReferenceValue = image.sprite;
        if (sourceProperty != null)
            sourceProperty.objectReferenceValue = image.sprite;
    }

    static void CopyContinueButtonPlateFromScene(SerializedProperty references)
    {
        if (references == null)
            return;

        SerializedProperty plateImageProperty = references.FindPropertyRelative("continueButtonPlateImage");
        Image plateImage = plateImageProperty != null ? plateImageProperty.objectReferenceValue as Image : null;
        if (plateImage == null)
        {
            Button button = references.FindPropertyRelative("continueButton")?.objectReferenceValue as Button;
            plateImage = ResolveButtonPlateImage(button);
            if (plateImageProperty != null && plateImage != null)
                plateImageProperty.objectReferenceValue = plateImage;
        }

        if (plateImage == null || plateImage.sprite == null)
            return;

        SerializedProperty spriteProperty = references.FindPropertyRelative("continueButtonPlateSprite");
        SerializedProperty sourceProperty = references.FindPropertyRelative("continueButtonPlateSpriteSource");
        if (spriteProperty != null)
            spriteProperty.objectReferenceValue = plateImage.sprite;
        if (sourceProperty != null)
            sourceProperty.objectReferenceValue = plateImage.sprite;
    }

    static Image ResolveButtonPlateImage(Button button)
    {
        if (button == null)
            return null;

        Image targetImage = button.targetGraphic as Image;
        if (targetImage != null)
            return targetImage;

        Image ownImage = button.GetComponent<Image>();
        return ownImage != null ? ownImage : button.GetComponentInChildren<Image>(true);
    }

    void DrawProperty(string propertyName, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox("Поле " + propertyName + " не найдено.", MessageType.Warning);
            return;
        }

        EditorGUILayout.PropertyField(property, Content(label, GetPropertyTooltip(propertyName)), true);
    }

    void DrawEndScreenSection(StoryUserInterface storyUserInterface)
    {
        SerializedProperty references = serializedObject.FindProperty("_endScreenReferences");
        if (references == null)
        {
            EditorGUILayout.HelpBox("Поле _endScreenReferences не найдено.", MessageType.Warning);
            return;
        }

        DrawEndScreenToolbar(storyUserInterface, references);
        DrawProperty("_applyEndScreenConfigurationOnEnable", "Применять при включении");
        DrawEndScreenBackground(references);
        DrawEndScreenButton(references);
        DrawEndScreenStatBindings("_endScreenStatBindings", "Статы", storyUserInterface, references);
        DrawEndScreenServiceLinks(references);
        DrawFoldoutProperty("_endScreenLayoutSettings", "Layout и Safe Area", ref _showEndScreenLayout);
        DrawFoldoutProperty("_endScreenPreviewSettings", "Preview", ref _showEndScreenPreview);
        DrawEndScreenDiagnostics(storyUserInterface);
    }

    void DrawEndScreenToolbar(StoryUserInterface storyUserInterface, SerializedProperty references)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _liveEndScreenPreview = EditorGUILayout.Toggle(
                Content("Живой preview", "Сразу перерисовывать финальный экран при изменениях."),
                _liveEndScreenPreview);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Content("Показать preview", "Показать финальный экран в Scene/Game view."), GUILayout.Height(26f)))
                    ShowEndScreenPreview(storyUserInterface);

                if (GUILayout.Button(Content("Скрыть preview", "Выключить объект финального экрана."), GUILayout.Height(26f)))
                {
                    serializedObject.ApplyModifiedProperties();
                    StoryEndScreenController controller = storyUserInterface.ResolveEndScreenController();
                    if (controller != null)
                        Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "Hide End Screen Preview");
                    storyUserInterface.ClearEndScreenPreview();
                    MarkDirty(storyUserInterface);
                    SceneView.RepaintAll();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Content("Автозаполнить", "Найти ссылки финального экрана в текущей сцене."), GUILayout.Height(26f)))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(storyUserInterface, "Auto Fill End Screen References");
                    storyUserInterface.AutoFillEndScreenReferences(overwrite: false);
                    MarkDirty(storyUserInterface);
                }

                if (GUILayout.Button(Content("Собрать экран", "Автозаполнить ссылки и скопировать текущие фоны, плашки, иконки и строки статов в override этой истории."), GUILayout.Height(26f)))
                    CollectWholeEndScreenFromScene(storyUserInterface, references);

                if (GUILayout.Button(Content("Проверить", "Показать ошибки и предупреждения по финальному экрану."), GUILayout.Height(26f)))
                    ShowEndScreenValidationDialog(storyUserInterface);
            }
        }
    }

    void CollectWholeEndScreenFromScene(StoryUserInterface storyUserInterface, SerializedProperty references)
    {
        if (storyUserInterface == null || references == null)
            return;

        serializedObject.ApplyModifiedProperties();
        Undo.RecordObject(storyUserInterface, "Collect Whole End Screen");
        storyUserInterface.AutoFillEndScreenReferences(overwrite: false);
        serializedObject.Update();

        references = serializedObject.FindProperty("_endScreenReferences");
        CopyEndScreenCoreSpritesFromScene(references);

        SerializedProperty statBindings = serializedObject.FindProperty("_endScreenStatBindings");
        CollectEndScreenStatBindingsFromScene(statBindings, references, storyUserInterface);

        serializedObject.ApplyModifiedProperties();
        storyUserInterface.ApplyEndScreenConfiguration(nameof(CollectWholeEndScreenFromScene));
        RefreshEndScreenLivePreview(storyUserInterface);
        MarkDirty(storyUserInterface);
    }

    static void CopyEndScreenCoreSpritesFromScene(SerializedProperty references)
    {
        if (references == null)
            return;

        CopySpriteFromImage(
            references.FindPropertyRelative("backgroundImage"),
            references.FindPropertyRelative("backgroundOverride"),
            null);
        CopySpriteFromImage(
            references.FindPropertyRelative("statsBackgroundImage"),
            references.FindPropertyRelative("statsBackgroundOverride"),
            null);
        CopyContinueButtonPlateFromScene(references);
    }

    void DrawEndScreenBackground(SerializedProperty references)
    {
        DrawGroupTitle("Фон", "Фон финального экрана и общий фон блока статов.");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawRelative(references, "backgroundImage", "Background Image");
            DrawEndSpriteRow(
                "Sprite фона",
                references.FindPropertyRelative("backgroundOverride"),
                null,
                () => CopySpriteFromImage(
                    references.FindPropertyRelative("backgroundImage"),
                    references.FindPropertyRelative("backgroundOverride"),
                    null));
            DrawRelative(references, "defaultBackground", "Fallback фон");
            DrawRelative(references, "statsBackgroundImage", "Фон блока статов");
            DrawEndSpriteRow(
                "Sprite фона статов",
                references.FindPropertyRelative("statsBackgroundOverride"),
                null,
                () => CopySpriteFromImage(
                    references.FindPropertyRelative("statsBackgroundImage"),
                    references.FindPropertyRelative("statsBackgroundOverride"),
                    null));
            DrawRelative(references, "hideStatsBackground", "Скрыть фон блока статов");
        }
    }

    void DrawEndScreenButton(SerializedProperty references)
    {
        DrawGroupTitle("Кнопка", "Одна кнопка Продолжить. Здесь меняется только визуал, не текст.");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawRelative(references, "continueButton", "Continue Button");
            DrawRelative(references, "continueButtonPlateImage", "Image плашки");
            DrawEndSpriteRow(
                "Sprite плашки кнопки",
                references.FindPropertyRelative("continueButtonPlateSprite"),
                references.FindPropertyRelative("continueButtonPlateSpriteSource"),
                () => CopyContinueButtonPlateFromScene(references));
            DrawRelative(references, "continueButtonText", "Текст кнопки");
        }
    }

    void DrawEndScreenServiceLinks(SerializedProperty references)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (DrawFoldout(ref _showEndScreenReferences, "Служебные ссылки"))
            {
                DrawEndScreenReferenceGroup(ref _showEndScreenRoot, "Корень", references,
                    ("root", "Root"),
                    ("canvasGroup", "Canvas Group"),
                    ("safeArea", "Safe Area"),
                    ("panelRoot", "Panel Root"));

                DrawEndScreenReferenceGroup(ref _showEndScreenTexts, "Тексты", references,
                    ("titleText", "Заголовок"),
                    ("storyTitleText", "Название истории"),
                    ("completedEpisodeText", "Завершённая серия"),
                    ("nextEpisodeText", "Следующая серия"));

                DrawEndScreenReferenceGroup(ref _showEndScreenStats, "Контейнер статов", references,
                    ("statsContainer", "Родитель авто-строк"),
                    ("statRowTemplate", "Шаблон авто-строки"));
            }
        }
    }

    void DrawRelative(SerializedProperty parent, string propertyName, string label)
    {
        SerializedProperty property = parent != null ? parent.FindPropertyRelative(propertyName) : null;
        if (property == null)
            return;

        EditorGUILayout.PropertyField(property, Content(label, GetPropertyTooltip(propertyName)), true);
    }

    void DrawEndScreenReferenceGroup(ref bool expanded, string title, SerializedProperty parent, params (string propertyName, string label)[] fields)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (!DrawFoldout(ref expanded, title))
                return;

            EditorGUI.indentLevel++;
            for (int i = 0; i < fields.Length; i++)
                DrawRelative(parent, fields[i].propertyName, fields[i].label);
            EditorGUI.indentLevel--;
        }
    }

    void DrawFoldoutProperty(string propertyName, string label, ref bool expanded)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox("Поле " + propertyName + " не найдено.", MessageType.Warning);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (!DrawFoldout(ref expanded, label))
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(property, GUIContent.none, true);
            EditorGUI.indentLevel--;
        }
    }

    void DrawEndScreenStatBindings(string propertyName, string label, StoryUserInterface storyUserInterface, SerializedProperty references)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox("Поле " + propertyName + " не найдено.", MessageType.Warning);
            return;
        }

        DrawGroupTitle(label, "Одна карточка равна одной строке на финальном экране.");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (!DrawFoldout(ref _showEndScreenStatBindings, "Строки статов"))
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Статы", EditorStyles.boldLabel);
                if (GUILayout.Button(Content("Собрать с экрана", "Найти все видимые строки статов в EndScreen и сделать из них bindings с override-картинками."), GUILayout.Width(138f)))
                    CollectEndScreenStatBindingsFromScene(property, references, storyUserInterface);
                if (GUILayout.Button("+", GUILayout.Width(30f)))
                    AddEndScreenStatBinding(property);
            }

            EditorGUILayout.HelpBox(
                "Проще всего нажать «Собрать с экрана»: все строки на финальном экране попадут сюда как override. Контейнер строк нужен только если ты генерируешь новые строки из шаблона.",
                MessageType.None);

            if (property.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Список статов пуст. Нажми +, чтобы добавить строку.", MessageType.Info);
                return;
            }

            for (int i = 0; i < property.arraySize; i++)
                DrawEndScreenStatBindingElement(property, property.GetArrayElementAtIndex(i), i);
        }
    }

    static void DrawEndScreenStatBindingElement(SerializedProperty array, SerializedProperty item, int index)
    {
        if (array == null || item == null)
            return;

        SerializedProperty enabled = item.FindPropertyRelative("enabled");
        SerializedProperty label = item.FindPropertyRelative("label");
        SerializedProperty statId = item.FindPropertyRelative("statId");
        SerializedProperty statAliases = item.FindPropertyRelative("statAliases");
        SerializedProperty valueMode = item.FindPropertyRelative("valueMode");
        SerializedProperty previewValue = item.FindPropertyRelative("previewValue");
        SerializedProperty row = item.FindPropertyRelative("row");
        SerializedProperty backgroundImage = item.FindPropertyRelative("backgroundImage");
        SerializedProperty plateImage = item.FindPropertyRelative("plateImage");
        SerializedProperty iconImage = item.FindPropertyRelative("iconImage");
        SerializedProperty lineText = item.FindPropertyRelative("lineText");
        SerializedProperty labelText = item.FindPropertyRelative("labelText");
        SerializedProperty valueText = item.FindPropertyRelative("valueText");
        SerializedProperty backgroundSprite = item.FindPropertyRelative("backgroundSprite");
        SerializedProperty backgroundSpriteSource = item.FindPropertyRelative("backgroundSpriteSource");
        SerializedProperty plateSprite = item.FindPropertyRelative("plateSprite");
        SerializedProperty plateSpriteSource = item.FindPropertyRelative("plateSpriteSource");
        SerializedProperty icon = item.FindPropertyRelative("icon");
        SerializedProperty iconSpriteSource = item.FindPropertyRelative("iconSpriteSource");
        SerializedProperty hideBackground = item.FindPropertyRelative("hideBackground");
        SerializedProperty hidePlate = item.FindPropertyRelative("hidePlate");
        SerializedProperty hideIcon = item.FindPropertyRelative("hideIcon");
        SerializedProperty overrideRowPosition = item.FindPropertyRelative("overrideRowPosition");
        SerializedProperty rowAnchoredPosition = item.FindPropertyRelative("rowAnchoredPosition");
        SerializedProperty rowOffset = item.FindPropertyRelative("rowOffset");
        SerializedProperty backgroundOffset = item.FindPropertyRelative("backgroundOffset");
        SerializedProperty plateOffset = item.FindPropertyRelative("plateOffset");
        SerializedProperty iconOffset = item.FindPropertyRelative("iconOffset");
        SerializedProperty lineTextOffset = item.FindPropertyRelative("lineTextOffset");
        SerializedProperty labelTextOffset = item.FindPropertyRelative("labelTextOffset");
        SerializedProperty valueTextOffset = item.FindPropertyRelative("valueTextOffset");
        SerializedProperty overrideRowSize = item.FindPropertyRelative("overrideRowSize");
        SerializedProperty rowSize = item.FindPropertyRelative("rowSize");
        SerializedProperty ignoreParentLayoutWhenPositioned = item.FindPropertyRelative("ignoreParentLayoutWhenPositioned");
        SerializedProperty hideWhenZero = item.FindPropertyRelative("hideWhenZero");
        SerializedProperty format = item.FindPropertyRelative("format");

        string labelValue = !string.IsNullOrWhiteSpace(label?.stringValue)
            ? label.stringValue
            : "Стат " + (index + 1);
        string statIdValue = !string.IsNullOrWhiteSpace(statId?.stringValue) ? statId.stringValue : "custom_stat";
        string title = (enabled == null || enabled.boolValue ? "✓ " : "□ ") + labelValue + "    " + statIdValue;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (enabled != null)
                    enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(18f));

                item.isExpanded = EditorGUILayout.Foldout(item.isExpanded, title, true);

                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (GUILayout.Button("↑", GUILayout.Width(24f)))
                    {
                        array.MoveArrayElement(index, index - 1);
                        return;
                    }
                }

                using (new EditorGUI.DisabledScope(index >= array.arraySize - 1))
                {
                    if (GUILayout.Button("↓", GUILayout.Width(24f)))
                    {
                        array.MoveArrayElement(index, index + 1);
                        return;
                    }
                }

                if (GUILayout.Button("x", GUILayout.Width(24f)))
                {
                    array.DeleteArrayElementAtIndex(index);
                    return;
                }
            }

            if (!item.isExpanded)
                return;

            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Min(132f, EditorGUIUtility.currentViewWidth * 0.36f);

            DrawGroupTitle("Значение", "Что показываем и откуда берём число.");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawStatProperty(label, "Название", "Текст на экране: Самооценка, Принципы, Чувства.");
                DrawStatProperty(statId, "Stat ID", "Ключ в GameState.");
                DrawStatProperty(valueMode, "Источник", "Откуда брать число.");
                DrawStatProperty(previewValue, "Preview", "Число для редакторского preview.");
                DrawStatProperty(format, "Формат", "{0} означает значение.");
                DrawStatProperty(hideWhenZero, "Скрыть при 0", "Не показывать строку при нуле.");
            }

            if (DrawStateFoldout(item.propertyPath + ".aliases", "Алиасы Stat ID", false))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    DrawStatProperty(statAliases, "Алиасы", "Дополнительные ключи того же стата.", true);
            }

            if (DrawStateFoldout(item.propertyPath + ".objects", "Объекты", true))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawStatProperty(row, "Row", "Корень строки.");
                    using (new EditorGUI.DisabledScope(row == null || row.objectReferenceValue == null))
                    {
                        if (GUILayout.Button(Content("Автозаполнить из Row", "Найти фон, плашку, иконку и тексты внутри Row."), GUILayout.Height(22f)))
                            AutoFillEndScreenStatBindingFromRow(item);
                    }
                    DrawStatProperty(backgroundImage, "Background", "Image фона строки.");
                    DrawStatProperty(plateImage, "Plate", "Image декоративной плашки строки.");
                    DrawStatProperty(iconImage, "Icon", "Image иконки.");
                    DrawStatProperty(lineText, "Line Text", "Один текст всей строки.");
                    DrawStatProperty(labelText, "Label Text", "Отдельный текст названия.");
                    DrawStatProperty(valueText, "Value Text", "Отдельный текст значения.");
                }
            }

            if (DrawStateFoldout(item.propertyPath + ".visuals", "Картинки", true))
            {
                DrawEndSpriteRow("Фон строки", backgroundSprite, backgroundSpriteSource, () => CopySpriteFromImage(backgroundImage, backgroundSprite, backgroundSpriteSource));
                DrawEndSpriteRow("Плашка строки", plateSprite, plateSpriteSource, () => CopySpriteFromImage(plateImage, plateSprite, plateSpriteSource));
                DrawEndSpriteRow("Иконка", icon, iconSpriteSource, () => CopySpriteFromImage(iconImage, icon, iconSpriteSource));

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    DrawStatProperty(hideBackground, "Скрыть фон", "Выключить фон строки.");
                    DrawStatProperty(hidePlate, "Скрыть плашку", "Выключить плашку строки.");
                    DrawStatProperty(hideIcon, "Скрыть иконку", "Выключить иконку строки.");
                }
            }

            if (DrawStateFoldout(item.propertyPath + ".position", "Позиция", false))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawStatProperty(overrideRowPosition, "Переопределить позицию", "Задать точную позицию Row.");
                    if (overrideRowPosition != null && overrideRowPosition.boolValue && rowAnchoredPosition != null)
                        DrawStatProperty(rowAnchoredPosition, "Anchored Position", "Позиция Row.");
                    DrawStatProperty(overrideRowSize, "Переопределить размер", "Задать размер Row.");
                    if (overrideRowSize != null && overrideRowSize.boolValue && rowSize != null)
                        DrawStatProperty(rowSize, "Размер Row", "Размер строки.");
                    DrawStatProperty(rowOffset, "Offset строки", "Смещение Row.");
                    DrawStatProperty(backgroundOffset, "Offset фона", "Смещение Background.");
                    DrawStatProperty(plateOffset, "Offset плашки", "Смещение Plate.");
                    DrawStatProperty(iconOffset, "Offset иконки", "Смещение Icon.");
                    DrawStatProperty(lineTextOffset, "Offset текста строки", "Смещение Line Text.");
                    DrawStatProperty(labelTextOffset, "Offset названия", "Смещение Label Text.");
                    DrawStatProperty(valueTextOffset, "Offset значения", "Смещение Value Text.");
                    if (((overrideRowPosition != null && overrideRowPosition.boolValue) ||
                        (overrideRowSize != null && overrideRowSize.boolValue)) &&
                        ignoreParentLayoutWhenPositioned != null)
                    {
                        DrawStatProperty(ignoreParentLayoutWhenPositioned, "Игнорировать parent layout", "Layout не будет перетирать ручную позицию.");
                    }
                }
            }

            EditorGUIUtility.labelWidth = oldLabelWidth;
        }
    }

    static void AutoFillEndScreenStatBindingFromRow(SerializedProperty item)
    {
        if (item == null)
            return;

        SerializedProperty rowProperty = item.FindPropertyRelative("row");
        RectTransform row = rowProperty != null ? rowProperty.objectReferenceValue as RectTransform : null;
        if (row == null)
            return;

        SetObjectIfEmpty(item, "backgroundImage", FindImageInRow(row, true, false, "background", "bg", "fon", "фон"));
        SetObjectIfEmpty(item, "plateImage", FindImageInRow(row, true, true, "plate", "back", "frame", "panel", "plashka", "podlozka", "подлож", "плаш"));
        SetObjectIfEmpty(item, "iconImage", FindImageInRow(row, false, false, "icon", "икон"));

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text singleVisibleText = ResolveSingleVisibleEndScreenText(texts);
        if (singleVisibleText != null)
        {
            SetObjectIfEmpty(item, "lineText", singleVisibleText);
            ClearObject(item, "labelText");
            ClearObject(item, "valueText");
        }
        else if (texts.Length == 1)
        {
            SetObjectIfEmpty(item, "lineText", texts[0]);
            ClearObject(item, "labelText");
            ClearObject(item, "valueText");
        }
        else if (texts.Length > 1)
        {
            TMP_Text labelText = FindTextInRowStrict(row, "label", "name", "title", "назв");
            TMP_Text valueText = FindTextInRowStrict(row, "value", "count", "amount", "number", "знач", "число");
            if (labelText == null && valueText != null)
            {
                SetObjectIfEmpty(item, "lineText", valueText);
                ClearObject(item, "labelText");
                ClearObject(item, "valueText");
            }
            else
            {
                SetObjectIfEmpty(item, "labelText", labelText);
                SetObjectIfEmpty(item, "valueText", valueText);
            }
        }

        Image background = item.FindPropertyRelative("backgroundImage")?.objectReferenceValue as Image;
        Image plate = item.FindPropertyRelative("plateImage")?.objectReferenceValue as Image;
        Image icon = item.FindPropertyRelative("iconImage")?.objectReferenceValue as Image;
        if (background != null)
        {
            SetObjectIfEmpty(item, "backgroundSprite", background.sprite);
            SetObjectIfEmpty(item, "backgroundSpriteSource", background.sprite);
        }
        if (plate != null)
        {
            SetObjectIfEmpty(item, "plateSprite", plate.sprite);
            SetObjectIfEmpty(item, "plateSpriteSource", plate.sprite);
        }
        if (icon != null)
        {
            SetObjectIfEmpty(item, "icon", icon.sprite);
            SetObjectIfEmpty(item, "iconSpriteSource", icon.sprite);
        }
    }

    static void CollectEndScreenStatBindingsFromScene(SerializedProperty array, SerializedProperty references, StoryUserInterface storyUserInterface)
    {
        if (array == null)
            return;

        Transform root = ResolveEndScreenRootForEditor(references, storyUserInterface);
        if (root == null)
        {
            EditorUtility.DisplayDialog("StoryUserInterface End Screen", "Не найден EndScreen Root. Сначала назначь Root или нажми «Автозаполнить».", "OK");
            return;
        }

        List<RectTransform> rows = FindEndScreenStatRows(root);
        if (rows.Count == 0)
        {
            EditorUtility.DisplayDialog("StoryUserInterface End Screen", "Не нашёл строки статов на экране. Проверь, что объекты называются вроде CityFinalStat, FeelFinalStat или содержат Text + Image.", "OK");
            return;
        }

        if (storyUserInterface != null)
            Undo.RecordObject(storyUserInterface, "Collect End Screen Stat Bindings");

        array.arraySize = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            RectTransform row = rows[i];
            AddEndScreenStatBinding(array);
            SerializedProperty item = array.GetArrayElementAtIndex(array.arraySize - 1);
            if (item == null)
                continue;

            (string label, string statId, string[] aliases) = GuessStatBinding(row, i);
            SetEndScreenBindingString(item, "label", label);
            SetEndScreenBindingString(item, "statId", statId);
            SetEndScreenBindingStringArray(item, "statAliases", aliases);
            SetEndScreenBindingObject(item, "row", row);
            AutoFillEndScreenStatBindingFromRow(item);
            item.isExpanded = false;
        }

        array.serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(array.serializedObject.targetObject);
    }

    static Transform ResolveEndScreenRootForEditor(SerializedProperty references, StoryUserInterface storyUserInterface)
    {
        GameObject root = references?.FindPropertyRelative("root")?.objectReferenceValue as GameObject;
        if (root != null)
            return root.transform;

        StoryEndScreenController controller = storyUserInterface != null ? storyUserInterface.ResolveEndScreenController() : null;
        if (controller != null && controller.References != null)
        {
            GameObject resolvedRoot = controller.References.ResolveRoot(controller);
            if (resolvedRoot != null)
                return resolvedRoot.transform;
        }

        return null;
    }

    static List<RectTransform> FindEndScreenStatRows(Transform root)
    {
        var rows = new List<RectTransform>();
        if (root == null)
            return rows;

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null || rect.transform == root)
                continue;
            if (IsLikelyEndScreenStatRow(rect))
                rows.Add(rect);
        }

        return rows;
    }

    static bool IsLikelyEndScreenStatRow(RectTransform rect)
    {
        if (rect == null || rect.GetComponent<Button>() != null)
            return false;

        string haystack = (rect.name + "\n" + BuildTransformPath(rect)).ToLowerInvariant();
        bool hasStatName =
            ContainsAnyToken(haystack,
                "finalstat", "statrow", "cityfinal", "fairytalefinal", "respectfinal", "sparkfinal",
                "heartfinal", "candlefinal", "самооцен", "princip", "принцип", "feel", "чувств") ||
            (ContainsAnyToken(haystack, "city", "town", "город", "fairytale", "story", "сказ", "respect", "reputation", "уваж", "spark", "heart", "искр", "свеч") &&
             ContainsAnyToken(haystack, "stat", "final"));
        if (!hasStatName)
            return false;

        if (HasLikelyStatRowChild(rect))
            return false;

        return rect.GetComponentInChildren<TMP_Text>(true) != null &&
               rect.GetComponentsInChildren<Image>(true).Length > 0;
    }

    static bool HasLikelyStatRowChild(RectTransform rect)
    {
        if (rect == null)
            return false;

        for (int i = 0; i < rect.childCount; i++)
        {
            Transform child = rect.GetChild(i);
            if (child == null)
                continue;

            string name = child.name.ToLowerInvariant();
            if (name.Contains("finalstat") || name.Contains("statrow"))
                return true;
        }

        return false;
    }

    static (string label, string statId, string[] aliases) GuessStatBinding(RectTransform row, int index)
    {
        string haystack = (row != null ? row.name + "\n" + BuildTransformPath(row) : "").ToLowerInvariant();

        if (ContainsAnyToken(haystack, "city", "town", "город", "gorod"))
            return ("Город", "city", new[] { "town", "gorod" });
        if (ContainsAnyToken(haystack, "fairytale", "story", "tale", "сказ", "skazka"))
            return ("Сказка", "fairytale", new[] { "story", "tale", "skazka" });
        if (ContainsAnyToken(haystack, "respect", "reputation", "rep", "уваж", "репутац"))
            return ("Репутация", "reputation", new[] { "respect", "rep" });
        if (ContainsAnyToken(haystack, "spark", "heart", "искр", "серд"))
            return ("Искры", "hearts", new[] { "sparks", "heart" });
        if (ContainsAnyToken(haystack, "candle", "свеч"))
            return ("Свечи", "candles", Array.Empty<string>());
        if (ContainsAnyToken(haystack, "self", "esteem", "samo", "самооцен"))
            return ("Самооценка", "self_esteem", new[] { "self", "samoocenka" });
        if (ContainsAnyToken(haystack, "princip", "принцип"))
            return ("Принципы", "principles", new[] { "principle", "princip" });
        if (ContainsAnyToken(haystack, "feel", "чувств"))
            return ("Чувства", "feels", new[] { "feel", "feelings" });

        string label = FirstReadableText(row);
        if (string.IsNullOrWhiteSpace(label))
            label = row != null && !string.IsNullOrWhiteSpace(row.name) ? row.name : "Стат " + (index + 1);

        return (label, SanitizeStatId(row != null ? row.name : label, index), Array.Empty<string>());
    }

    static string FirstReadableText(RectTransform row)
    {
        if (row == null)
            return "";

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            string value = texts[i] != null ? (texts[i].text ?? "").Trim() : "";
            if (string.IsNullOrWhiteSpace(value))
                continue;

            value = value.Replace("\r", " ").Replace("\n", " ").Trim();
            int colon = value.IndexOf(':');
            if (colon > 0)
                value = value.Substring(0, colon).Trim();

            if (IsMostlyNumber(value))
                continue;

            return value;
        }

        return "";
    }

    static bool IsMostlyNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!char.IsDigit(c) && c != '+' && c != '-' && c != ' ' && c != '.')
                return false;
        }

        return true;
    }

    static string SanitizeStatId(string value, int index)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "stat_" + (index + 1);

        var chars = new List<char>(value.Length);
        bool previousUnderscore = false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = char.ToLowerInvariant(value[i]);
            bool keep = (c >= 'a' && c <= 'z') || char.IsDigit(c);
            if (keep)
            {
                chars.Add(c);
                previousUnderscore = false;
            }
            else if (!previousUnderscore)
            {
                chars.Add('_');
                previousUnderscore = true;
            }
        }

        string result = new string(chars.ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "stat_" + (index + 1) : result;
    }

    static bool ContainsAnyToken(string value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value) || tokens == null)
            return false;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!string.IsNullOrWhiteSpace(token) && value.Contains(token.ToLowerInvariant()))
                return true;
        }

        return false;
    }

    static string BuildTransformPath(Transform transform)
    {
        if (transform == null)
            return "";

        var names = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    static Image FindImageInRow(RectTransform row, bool allowRowImage, bool allowFallback, params string[] tokens)
    {
        if (row == null)
            return null;

        Image[] images = row.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || (!allowRowImage && image.transform == row))
                continue;

            string name = image.name.ToLowerInvariant();
            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                string token = tokens[tokenIndex];
                if (!string.IsNullOrWhiteSpace(token) && name.Contains(token.ToLowerInvariant()))
                    return image;
            }
        }

        if (!allowFallback)
            return null;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && (allowRowImage || image.transform != row))
                return image;
        }

        return null;
    }

    static TMP_Text FindTextInRow(RectTransform row, params string[] tokens)
    {
        if (row == null)
            return null;

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            string name = text.name.ToLowerInvariant();
            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                string token = tokens[tokenIndex];
                if (!string.IsNullOrWhiteSpace(token) && name.Contains(token.ToLowerInvariant()))
                    return text;
            }
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    static TMP_Text FindTextInRowStrict(RectTransform row, params string[] tokens)
    {
        if (row == null)
            return null;

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            string name = text.name.ToLowerInvariant();
            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                string token = tokens[tokenIndex];
                if (!string.IsNullOrWhiteSpace(token) && name.Contains(token.ToLowerInvariant()))
                    return text;
            }
        }

        return null;
    }

    static TMP_Text ResolveSingleVisibleEndScreenText(TMP_Text[] texts)
    {
        if (texts == null || texts.Length == 0)
            return null;

        TMP_Text result = null;
        int usableCount = 0;
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || !text.enabled || !text.gameObject.activeInHierarchy || text.color.a <= 0.001f)
                continue;

            usableCount++;
            result = text;
            if (usableCount > 1)
                return null;
        }

        return usableCount == 1 ? result : null;
    }

    static void SetObjectIfEmpty(SerializedProperty item, string name, UnityEngine.Object value)
    {
        if (item == null || value == null)
            return;

        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null && property.objectReferenceValue == null)
            property.objectReferenceValue = value;
    }

    static void ClearObject(SerializedProperty item, string name)
    {
        SerializedProperty property = item != null ? item.FindPropertyRelative(name) : null;
        if (property != null)
            property.objectReferenceValue = null;
    }

    static void AddEndScreenStatBinding(SerializedProperty array)
    {
        if (array == null)
            return;

        int index = array.arraySize;
        array.InsertArrayElementAtIndex(index);
        SerializedProperty item = array.GetArrayElementAtIndex(index);
        if (item == null)
            return;

        SetEndScreenBindingBool(item, "enabled", true);
        SetEndScreenBindingString(item, "label", "Стат");
        SetEndScreenBindingString(item, "statId", "custom_stat");
        SetEndScreenBindingArraySize(item, "statAliases", 0);
        SetEndScreenBindingEnum(item, "valueMode", (int)StoryEndScreenStatValueMode.CurrentTotal);
        SetEndScreenBindingInt(item, "previewValue", 0);
        SetEndScreenBindingObject(item, "row", null);
        SetEndScreenBindingObject(item, "backgroundImage", null);
        SetEndScreenBindingObject(item, "plateImage", null);
        SetEndScreenBindingObject(item, "iconImage", null);
        SetEndScreenBindingObject(item, "lineText", null);
        SetEndScreenBindingObject(item, "labelText", null);
        SetEndScreenBindingObject(item, "valueText", null);
        SetEndScreenBindingObject(item, "backgroundSprite", null);
        SetEndScreenBindingObject(item, "backgroundSpriteSource", null);
        SetEndScreenBindingObject(item, "plateSprite", null);
        SetEndScreenBindingObject(item, "plateSpriteSource", null);
        SetEndScreenBindingObject(item, "icon", null);
        SetEndScreenBindingObject(item, "iconSpriteSource", null);
        SetEndScreenBindingBool(item, "hideBackground", false);
        SetEndScreenBindingBool(item, "hidePlate", false);
        SetEndScreenBindingBool(item, "hideIcon", false);
        SetEndScreenBindingBool(item, "overrideRowPosition", false);
        SetEndScreenBindingVector2(item, "rowAnchoredPosition", Vector2.zero);
        SetEndScreenBindingVector2(item, "rowOffset", Vector2.zero);
        SetEndScreenBindingVector2(item, "backgroundOffset", Vector2.zero);
        SetEndScreenBindingVector2(item, "plateOffset", Vector2.zero);
        SetEndScreenBindingVector2(item, "iconOffset", Vector2.zero);
        SetEndScreenBindingVector2(item, "lineTextOffset", Vector2.zero);
        SetEndScreenBindingVector2(item, "labelTextOffset", Vector2.zero);
        SetEndScreenBindingVector2(item, "valueTextOffset", Vector2.zero);
        SetEndScreenBindingBool(item, "overrideRowSize", false);
        SetEndScreenBindingVector2(item, "rowSize", Vector2.zero);
        SetEndScreenBindingBool(item, "ignoreParentLayoutWhenPositioned", true);
        SetEndScreenBindingBool(item, "hideWhenZero", false);
        SetEndScreenBindingString(item, "format", "{0}");
        item.isExpanded = true;
    }

    static void SetEndScreenBindingBool(SerializedProperty item, string name, bool value)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null)
            property.boolValue = value;
    }

    static void SetEndScreenBindingString(SerializedProperty item, string name, string value)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null)
            property.stringValue = value ?? "";
    }

    static void SetEndScreenBindingInt(SerializedProperty item, string name, int value)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null)
            property.intValue = value;
    }

    static void SetEndScreenBindingEnum(SerializedProperty item, string name, int value)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null)
            property.enumValueIndex = value;
    }

    static void SetEndScreenBindingObject(SerializedProperty item, string name, UnityEngine.Object value)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null)
            property.objectReferenceValue = value;
    }

    static void SetEndScreenBindingVector2(SerializedProperty item, string name, Vector2 value)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null)
            property.vector2Value = value;
    }

    static void SetEndScreenBindingArraySize(SerializedProperty item, string name, int size)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property != null && property.isArray)
            property.arraySize = Mathf.Max(0, size);
    }

    static void SetEndScreenBindingStringArray(SerializedProperty item, string name, string[] values)
    {
        SerializedProperty property = item.FindPropertyRelative(name);
        if (property == null || !property.isArray)
            return;

        values ??= Array.Empty<string>();
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).stringValue = values[i] ?? "";
    }

    static bool DrawFoldout(ref bool expanded, string title)
    {
        expanded = EditorGUILayout.Foldout(expanded, title, true, EditorStyles.foldoutHeader);
        return expanded;
    }

    void DrawTemplateSummary(StoryUserInterface storyUserInterface)
    {
        PhoneDialogueUIReferences references = storyUserInterface.PhoneReferences;
        if (references == null)
            return;

        DrawTemplateLine("Incoming", references.incomingTemplate);
        DrawTemplateLine("Outgoing", references.outgoingTemplate);
        DrawTemplateLine("Photo", references.photoMessageTemplate);
    }

    static void DrawTemplateLine(string label, PhoneMessageTemplateReferences template)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(76f));
            EditorGUILayout.ObjectField(template != null ? template.root : null, typeof(GameObject), true);
            bool hasText = template != null && template.messageText != null;
            bool hasName = template != null && template.senderNameText != null;
            bool hasTime = template != null && template.timeText != null;
            EditorGUILayout.LabelField(hasText ? "Text OK" : "No Text", GUILayout.Width(64f));
            EditorGUILayout.LabelField(hasName ? "Name OK" : "No Name", GUILayout.Width(72f));
            EditorGUILayout.LabelField(hasTime ? "Time OK" : "No Time", GUILayout.Width(72f));
        }
    }

    void DrawPreviewFields(StoryUserInterface storyUserInterface)
    {
        PhonePreviewSettings previewSettings = storyUserInterface != null
            ? storyUserInterface.PhonePreviewSettings
            : null;
        string previewContactName = previewSettings != null
            ? previewSettings.quickPreviewContactName
            : _previewContactName;
        EditorGUI.BeginChangeCheck();
        previewContactName = EditorGUILayout.TextField("Контакт", previewContactName);
        if (EditorGUI.EndChangeCheck())
        {
            _previewContactName = previewContactName;
            if (previewSettings != null)
            {
                Undo.RecordObject(storyUserInterface, "Change Phone Preview Contact");
                previewSettings.quickPreviewContactName = previewContactName;
                previewSettings.Normalize();
                MarkDirty(storyUserInterface);
            }
        }
        else
        {
            _previewContactName = previewContactName;
        }
        EditorGUILayout.LabelField("Сценарий preview");
        _previewScript = EditorGUILayout.TextArea(_previewScript ?? "", GUILayout.MinHeight(78f));
    }

    void DrawPreviewButtons(StoryUserInterface storyUserInterface)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Автозаполнить ссылки телефона", GUILayout.Height(26f)))
            {
                Undo.RecordObject(storyUserInterface, "Auto Fill Phone UI References");
                storyUserInterface.AutoFillPhoneReferences(overwrite: false);
                MarkDirty(storyUserInterface);
            }

            if (GUILayout.Button("Проверить ссылки телефона", GUILayout.Height(26f)))
                ShowValidationDialog(storyUserInterface);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Показать preview", GUILayout.Height(26f)))
                ShowPreview(storyUserInterface);

            if (GUILayout.Button("Очистить preview", GUILayout.Height(26f)))
            {
                PhoneDialogueUI phoneUi = storyUserInterface.ResolvePhoneDialogueUI();
                if (phoneUi != null)
                    Undo.RegisterFullObjectHierarchyUndo(phoneUi.gameObject, "Clear Phone Preview");
                storyUserInterface.ClearPhonePreview();
                MarkDirty(storyUserInterface);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Пересчитать layout", GUILayout.Height(26f)))
            {
                PhoneDialogueUI phoneUi = storyUserInterface.ResolvePhoneDialogueUI();
                if (phoneUi != null)
                    Undo.RegisterFullObjectHierarchyUndo(phoneUi.gameObject, "Recalculate Phone Layout");
                storyUserInterface.RecalculatePhoneLayout(nameof(StoryUserInterfaceEditor));
                MarkDirty(storyUserInterface);
            }

            if (GUILayout.Button("Открыть PhoneDialogueUI", GUILayout.Height(26f)))
            {
                PhoneDialogueUI phoneUi = storyUserInterface.ResolvePhoneDialogueUI();
                if (phoneUi != null)
                {
                    Selection.activeGameObject = phoneUi.gameObject;
                    EditorGUIUtility.PingObject(phoneUi.gameObject);
                }
            }
        }

        if (GUILayout.Button("Мигрировать ссылки из StoryUiStyle", GUILayout.Height(26f)))
        {
            Undo.RecordObject(storyUserInterface, "Migrate Phone References");
            PhoneDialogueUI phoneUi = storyUserInterface.ResolvePhoneDialogueUI();
            if (phoneUi != null)
                Undo.RecordObject(phoneUi, "Migrate Phone References");
            storyUserInterface.MigratePhoneReferencesFromLegacyPhoneDialogueUI(overwrite: false);
            MarkDirty(storyUserInterface);
            if (phoneUi != null)
                EditorUtility.SetDirty(phoneUi);
        }
    }

    void DrawDiagnostics(StoryUserInterface storyUserInterface)
    {
        PhoneDialogueUI phoneUi = storyUserInterface.ResolvePhoneDialogueUI();
        if (phoneUi == null)
        {
            EditorGUILayout.HelpBox("PhoneDialogueUI не найден в сцене. Создай/настрой экран телефона или назначь ссылку в блоке выше.", MessageType.Error);
            if (GUILayout.Button("Создать/настроить PhoneDialogueUI", GUILayout.Height(26f)))
            {
                PhoneDialoguePreviewSetup.CreateOrConfigureInOpenScene();
                MarkDirty(storyUserInterface);
            }
            return;
        }

        if (phoneUi.HasSerializedPhoneConfiguration())
        {
            EditorGUILayout.HelpBox(
                "В PhoneDialogueUI ещё есть legacy-ссылки. Они используются как fallback, но рабочие scene-ссылки должны жить здесь, в StoryUserInterface.",
                MessageType.Warning);
        }

        PhonePreviewValidationResult validation = storyUserInterface.ValidatePhoneReferences(BuildPreviewNode(storyUserInterface), false);
        DrawValidationResult(validation);
    }

    void DrawEndScreenDiagnostics(StoryUserInterface storyUserInterface)
    {
        StoryEndScreenController controller = storyUserInterface.ResolveEndScreenController();
        if (controller == null)
        {
            EditorGUILayout.HelpBox("StoryEndScreenController не найден в сцене. Назначь Root/EndScreen или добавь controller на финальный экран.", MessageType.Error);
            return;
        }

        StoryEndScreenValidationResult validation = storyUserInterface.ValidateEndScreen(requireRuntime: false);
        DrawEndScreenValidationResult(validation);
    }

    void ShowValidationDialog(StoryUserInterface storyUserInterface)
    {
        PhonePreviewValidationResult validation = storyUserInterface.ValidatePhoneReferences(BuildPreviewNode(storyUserInterface), true);
        EditorUtility.DisplayDialog("StoryUserInterface Phone", FormatValidation(validation), "OK");
        AppLogger.Info(
            AppLogCategory.Editor,
            nameof(StoryUserInterfaceEditor),
            nameof(ShowValidationDialog),
            "Phone references validation requested from StoryUserInterface inspector.",
            LogMetadata.Of(
                "errors", validation != null ? validation.Errors.Count : 0,
                "warnings", validation != null ? validation.Warnings.Count : 0,
                "object", storyUserInterface != null ? storyUserInterface.name : ""));
    }

    void ShowEndScreenValidationDialog(StoryUserInterface storyUserInterface)
    {
        serializedObject.ApplyModifiedProperties();
        StoryEndScreenValidationResult validation = storyUserInterface.ValidateEndScreen(requireRuntime: true);
        EditorUtility.DisplayDialog("StoryUserInterface End Screen", FormatEndScreenValidation(validation), "OK");
        AppLogger.Info(
            AppLogCategory.Editor,
            nameof(StoryUserInterfaceEditor),
            nameof(ShowEndScreenValidationDialog),
            "End screen validation requested from StoryUserInterface inspector.",
            LogMetadata.Of(
                "errors", validation != null ? validation.Errors.Count : 0,
                "warnings", validation != null ? validation.Warnings.Count : 0,
                "object", storyUserInterface != null ? storyUserInterface.name : ""));
    }

    void ShowPreview(StoryUserInterface storyUserInterface)
    {
        PhoneDialogueUI phoneUi = storyUserInterface.ResolvePhoneDialogueUI();
        if (phoneUi != null)
            Undo.RegisterFullObjectHierarchyUndo(phoneUi.gameObject, "Preview Phone UI");

        bool shown = storyUserInterface.ShowPhonePreview(BuildPreviewNode(storyUserInterface), nameof(StoryUserInterfaceEditor));
        if (!shown)
            EditorUtility.DisplayDialog("StoryUserInterface Phone", "Предпросмотр телефона не смог отрисоваться. Проверь ссылки и шаблоны.", "OK");

        MarkDirty(storyUserInterface);
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    void ShowEndScreenPreview(StoryUserInterface storyUserInterface)
    {
        serializedObject.ApplyModifiedProperties();
        StoryEndScreenController controller = storyUserInterface.ResolveEndScreenController();
        if (controller != null)
            Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "Preview End Screen");

        bool shown = storyUserInterface.ShowEndScreenPreview(nameof(StoryUserInterfaceEditor));
        if (!shown)
            EditorUtility.DisplayDialog("StoryUserInterface End Screen", "Preview финального экрана не смог отрисоваться. Проверь Root, Background, TitleText и ContinueButton.", "OK");

        MarkDirty(storyUserInterface);
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    void RefreshEndScreenLivePreview(StoryUserInterface storyUserInterface)
    {
        if (storyUserInterface == null || Application.isPlaying)
            return;

        StoryEndScreenController controller = storyUserInterface.ResolveEndScreenController();
        if (controller == null)
            return;

        bool shown = storyUserInterface.ShowEndScreenPreview("StoryUserInterfaceLivePreview");
        if (!shown)
            return;

        MarkDirty(storyUserInterface);
        EditorUtility.SetDirty(controller);
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    PhoneDialogueNode BuildPreviewNode(StoryUserInterface storyUserInterface)
    {
        if (_previewNode == null)
        {
            _previewNode = CreateInstance<PhoneDialogueNode>();
            _previewNode.hideFlags = HideFlags.HideAndDontSave;
            _previewNode.name = "StoryUserInterface Phone Preview Node";
        }

        string configuredContactName = storyUserInterface != null &&
            storyUserInterface.PhonePreviewSettings != null
            ? storyUserInterface.PhonePreviewSettings.quickPreviewContactName
            : _previewContactName;
        if (string.IsNullOrWhiteSpace(configuredContactName))
            configuredContactName = _previewContactName;
        _previewNode.contactName = string.IsNullOrWhiteSpace(configuredContactName)
            ? "\u0420\u043E\u0431"
            : configuredContactName.Trim();
        _previewNode.typingDelay = 0.15f;
        bool useDefaultPhoto = storyUserInterface != null &&
                               storyUserInterface.PhonePreviewSettings != null &&
                               storyUserInterface.PhonePreviewSettings.useDefaultPhotoSpriteInQuickPreview;
        Sprite defaultAttachment = useDefaultPhoto && storyUserInterface.PhoneReferences != null
            ? storyUserInterface.PhoneReferences.defaultPhotoSprite
            : null;
        _previewNode.messages = BuildMessages(_previewScript, _previewNode.contactName, defaultAttachment);
        return _previewNode;
    }

    static List<PhoneMessage> BuildMessages(string script, string contactName, Sprite defaultAttachment)
    {
        var messages = new List<PhoneMessage>();
        if (string.IsNullOrWhiteSpace(script))
            return messages;

        string[] lines = script.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        PhoneMessageSide lastSide = PhoneMessageSide.Incoming;
        string lastSenderName = ResolveSenderName(lastSide, contactName);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = (lines[i] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string speaker = "";
            string text = line;
            int colon = line.IndexOf(':');
            if (colon > 0)
            {
                speaker = line.Substring(0, colon).Trim();
                text = line.Substring(colon + 1).Trim();
            }

            PhoneMessageSide side = string.IsNullOrWhiteSpace(speaker)
                ? lastSide
                : IsOutgoingSpeaker(speaker, contactName) ? PhoneMessageSide.Outgoing : PhoneMessageSide.Incoming;
            string senderName = string.IsNullOrWhiteSpace(speaker)
                ? lastSenderName
                : NormalizeSenderName(speaker, side, contactName);

            bool hasPhoto = text.IndexOf("[photo]", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf("[фото]", System.StringComparison.OrdinalIgnoreCase) >= 0;

            messages.Add(new PhoneMessage
            {
                senderName = senderName,
                side = side,
                text = text.Replace("[photo]", "").Replace("[фото]", "").Trim(),
                timeText = messages.Count == 0 ? "15:25" : "",
                attachment = hasPhoto ? defaultAttachment : null,
                usePhotoLayout = hasPhoto
            });
            lastSide = side;
            lastSenderName = senderName;
        }

        return messages;
    }

    static bool IsOutgoingSpeaker(string speaker, string contactName)
    {
        if (DialogueVariableResolver.IsPlayerSpeakerName(
                speaker,
                DialogueVariableContext.PhoneDialogue(nameof(StoryUserInterfaceEditor))))
            return true;

        string value = (speaker ?? "").Trim().Trim('[', ']', '<', '>').ToLowerInvariant();
        string contact = (contactName ?? "").Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(contact) && value == contact)
            return false;

        if (value == "name" ||
            value == "hero" ||
            value == "me" ||
            value == "player" ||
            value == "имя" ||
            value == "гг" ||
            value == "я")
            return true;

        if (value == "contact" ||
            value == "meg" ||
            value == "мэг")
            return false;

        return value == "out" || value == "outgoing";
    }

    static string NormalizeSenderName(string speaker, PhoneMessageSide side, string contactName)
    {
        if (DialogueVariableResolver.IsPlayerNameToken(speaker))
            return "{PlayerName}";

        string value = (speaker ?? "").Trim();
        string normalized = value.Trim('[', ']', '<', '>').ToLowerInvariant();
        if (normalized == "name" ||
            normalized == "hero" ||
            normalized == "me" ||
            normalized == "player" ||
            normalized == "\u0438\u043C\u044F" ||
            normalized == "\u0433\u0433" ||
            normalized == "\u044F")
            return "{PlayerName}";

        if ((normalized == "contact" || normalized == "in" || normalized == "incoming") &&
            !string.IsNullOrWhiteSpace(contactName))
            return contactName.Trim();

        return string.IsNullOrWhiteSpace(value) ? ResolveSenderName(side, contactName) : value;
    }

    static string ResolveSenderName(PhoneMessageSide side, string contactName)
    {
        return side == PhoneMessageSide.Outgoing
            ? "{PlayerName}"
            : string.IsNullOrWhiteSpace(contactName) ? "Contact" : contactName.Trim();
    }

    static void DrawValidationResult(PhonePreviewValidationResult validation)
    {
        if (validation == null)
            return;

        if (!validation.HasErrors && !validation.HasWarnings)
        {
            EditorGUILayout.HelpBox("Ссылки телефона готовы для Edit Mode preview и runtime.", MessageType.Info);
            return;
        }

        for (int i = 0; i < validation.Errors.Count; i++)
            EditorGUILayout.HelpBox(validation.Errors[i], MessageType.Error);
        for (int i = 0; i < validation.Warnings.Count; i++)
            EditorGUILayout.HelpBox(validation.Warnings[i], MessageType.Warning);
    }

    static string FormatValidation(PhonePreviewValidationResult validation)
    {
        if (validation == null)
            return "Проверка не выполнена.";
        if (!validation.HasErrors && !validation.HasWarnings)
            return "Phone UI готов: критичных ошибок и предупреждений нет.";

        var builder = new System.Text.StringBuilder(512);
        for (int i = 0; i < validation.Errors.Count; i++)
            builder.Append("Ошибка: ").AppendLine(validation.Errors[i]);
        for (int i = 0; i < validation.Warnings.Count; i++)
            builder.Append("Предупреждение: ").AppendLine(validation.Warnings[i]);
        return builder.ToString();
    }

    static void DrawEndScreenValidationResult(StoryEndScreenValidationResult validation)
    {
        if (validation == null)
            return;

        if (!validation.HasErrors && !validation.HasWarnings)
        {
            EditorGUILayout.HelpBox("End Screen готов для preview и runtime.", MessageType.Info);
            return;
        }

        for (int i = 0; i < validation.Errors.Count; i++)
            EditorGUILayout.HelpBox(validation.Errors[i], MessageType.Error);
        for (int i = 0; i < validation.Warnings.Count; i++)
            EditorGUILayout.HelpBox(validation.Warnings[i], MessageType.Warning);
    }

    static string FormatEndScreenValidation(StoryEndScreenValidationResult validation)
    {
        if (validation == null)
            return "Проверка не выполнена.";
        if (!validation.HasErrors && !validation.HasWarnings)
            return "End Screen готов: критичных ошибок и предупреждений нет.";

        var builder = new System.Text.StringBuilder(512);
        for (int i = 0; i < validation.Errors.Count; i++)
            builder.Append("Ошибка: ").AppendLine(validation.Errors[i]);
        for (int i = 0; i < validation.Warnings.Count; i++)
            builder.Append("Предупреждение: ").AppendLine(validation.Warnings[i]);
        return builder.ToString();
    }

    static void MarkDirty(StoryUserInterface storyUserInterface)
    {
        if (storyUserInterface == null)
            return;

        EditorUtility.SetDirty(storyUserInterface);
        if (storyUserInterface.gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(storyUserInterface.gameObject.scene);
    }
}
#endif
