#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

public sealed class StoryInterfacePreviewWindow : EditorWindow
{
    enum PreviewMode
    {
        None,
        Dialogue,
        Choices,
        NameInput,
        Stat,
        Relationship,
        Chapter,
        Cutscene,
        Phone,
        Overview
    }

    enum PreviewScreen
    {
        Dialogue,
        Choices,
        NameInput,
        Stats,
        Relationships,
        Chapter,
        Cutscene,
        Phone,
        Overview
    }

    enum PreviewEquipmentMode
    {
        Saved,
        None,
        Override
    }

    [Serializable]
    sealed class CharacterPreviewSlot
    {
        public bool enabled;
        public CharacterData character;
        public CharacterEmotionType emotion = CharacterEmotionType.Idle;
    }

    StoryJsonAssetLibrary _library;
    StoryInterfaceStyleCatalog _styleCatalog;
    string _storyId = "";
    bool _useCutsceneStyle;
    bool _livePreview = true;
    bool _showCharacterControls = true;
    bool _previewCharacters = true;
    bool _previewAppearanceInitialized;
    bool _characterSlotsInitialized;
    AppearanceType _previewAppearance = AppearanceType.Default;
    PreviewEquipmentMode _outfitPreviewMode = PreviewEquipmentMode.Saved;
    PreviewEquipmentMode _hairPreviewMode = PreviewEquipmentMode.Saved;
    ClothingItem _previewOutfit;
    ClothingItem _previewHair;
    CharacterPreviewSlot _leftCharacter = new CharacterPreviewSlot { enabled = true };
    CharacterPreviewSlot _centerCharacter = new CharacterPreviewSlot();
    CharacterPreviewSlot _rightCharacter = new CharacterPreviewSlot();
    HeroCustomizationState _capturedAppearanceState;
    ClothingItem _capturedOutfitItem;
    ClothingItem _capturedHairItem;
    ClothingItem _capturedAccessoryItem;
    Sprite _capturedOutfitSprite;
    Sprite _capturedHairSprite;
    Sprite _capturedAccessorySprite;
    bool _hasCapturedAppearanceState;
    int _statPreviewIndex;
    int _relationshipPreviewIndex;
    PreviewScreen _previewScreen;
    PreviewMode _activePreviewMode;
    int _activeStatDelta = 1;
    string _dialogueSpeaker = "Элисон";
    string _dialogueBody = "Текст диалога для проверки отступов, плашки, шрифта и стиля текущей истории.";
    string _choiceOne = "Согласиться";
    string _choiceTwo = "Задать вопрос";
    string _choiceThree = "Промолчать";
    string _namePreviewValue = "Элисон";
    string _chapterPreviewTitle = "ГЛАВА 1: НОВАЯ РОЛЬ";
    Sprite _cutscenePreviewImage;
    VideoClip _cutscenePreviewVideo;
    TextAsset _cutscenePreviewGif;
    string _cutscenePreviewSpeaker = "";
    string _cutscenePreviewText = "\u0422\u0435\u043a\u0441\u0442 \u043a\u0430\u0442\u0441\u0446\u0435\u043d\u044b \u043f\u043e\u044f\u0432\u0438\u0442\u0441\u044f \u043f\u043e\u0441\u043b\u0435 \u043c\u0435\u0434\u0438\u0430 \u0438 \u0438\u0441\u043f\u043e\u043b\u044c\u0437\u0443\u0435\u0442 \u043a\u0430\u0442\u0441\u0446\u0435\u043d\u043d\u044b\u0439 \u0441\u0442\u0438\u043b\u044c UI.";
    bool _cutscenePreviewShowText = true;
    bool _cutscenePreviewHideCharacters = true;
    PhoneDialogueNode _phonePreviewSourceNode;
    PhoneDialogueNode _runtimePhonePreviewNode;
    Sprite _phonePreviewAvatar;
    Sprite _phonePreviewAttachment;
    string _phonePreviewContactName = "\u0420\u043E\u0431";
    string _phonePreviewScript = "\u041C\u044D\u0433: \u0423 \u043C\u0435\u043D\u044F \u0431\u0443\u0434\u0435\u0442 \u043F\u043E\u0434\u043A\u0430\u0441\u0442 \u0441 \u0413\u0430\u0431\u0440\u0438\u044D\u043B\u0435\u043C \u041C\u043E\u0440\u0442\u0435\u043B\u043B\u043E\u043C!!!\n{PlayerName}: \u0421 \u043A\u0435\u043C?\n\u041C\u044D\u0433: \u0421\u0442\u044B\u0434\u043D\u043E \u043D\u0435 \u0437\u043D\u0430\u0442\u044C, \u0441 \u0442\u0432\u043E\u0435\u0439-\u0442\u043E \u043F\u0440\u043E\u0444\u0435\u0441\u0441\u0438\u0435\u0439))\n\u041C\u044D\u0433: \u0424\u043E\u0442\u043E [\u0444\u043E\u0442\u043E]";
    float _phonePreviewTypingDelay = 0.15f;
    bool _phonePreviewHideCharacters = true;
    string _relationshipPreviewName = "";
    Vector2 _scrollPosition;
    bool _showContext = true;
    string _lastLiveSignature;
    double _nextLiveRefreshTime;
    bool _liveRefreshQueued;

    [MenuItem("VN/Interface Preview/Open Interface Preview", priority = 1)]
    public static void Open()
    {
        StoryInterfacePreviewWindow window = GetWindow<StoryInterfacePreviewWindow>("UI Preview");
        PrepareWindowSize(window);
        window.Show();
    }

    public static void OpenForStory(
        StoryInterfaceStyleCatalog styleCatalog,
        StoryData story,
        string storyId,
        StoryJsonAssetLibrary library)
    {
        StoryInterfacePreviewWindow window = GetWindow<StoryInterfacePreviewWindow>("UI Preview");
        PrepareWindowSize(window);
        window._styleCatalog = styleCatalog != null ? styleCatalog : ResolveDefaultStyleCatalog();
        window._library = library != null ? library : ResolveLibraryForStory(story);
        window._storyId = FirstNonEmpty(storyId, story != null ? story.storyId : "", ResolveStoryIdForLibrary(window._library));
        window._useCutsceneStyle = false;
        window._previewScreen = PreviewScreen.Dialogue;
        window._activePreviewMode = PreviewMode.Dialogue;
        window._lastLiveSignature = null;
        window._characterSlotsInitialized = false;
        window.EnsureCharacterPreviewState();
        window.Show();
        window.Focus();
        window.RefreshActivePreview(true);
    }

    public static void RefreshOpenLivePreviewsForStyle(StoryUiStyle style)
    {
        StoryInterfacePreviewWindow[] windows = Resources.FindObjectsOfTypeAll<StoryInterfacePreviewWindow>();
        if (windows == null || windows.Length == 0)
            return;

        for (int i = 0; i < windows.Length; i++)
        {
            StoryInterfacePreviewWindow window = windows[i];
            if (window == null || !window._livePreview || window._activePreviewMode == PreviewMode.None)
                continue;

            window._lastLiveSignature = null;
            window._nextLiveRefreshTime = 0d;
            window.RefreshActivePreview(false);
            window.Repaint();
        }
    }

    static void PrepareWindowSize(StoryInterfacePreviewWindow window)
    {
        if (window == null)
            return;

        window.minSize = new Vector2(620f, 520f);
        Rect position = window.position;
        if (position.width <= 1f || position.height <= 1f)
            return;

        position.width = Mathf.Max(position.width, 620f);
        position.height = Mathf.Max(position.height, 520f);
        window.position = position;
    }

    void OnEnable()
    {
        if (_library == null)
            _library = ResolveSelectedLibrary();
        if (_styleCatalog == null)
            _styleCatalog = ResolveDefaultStyleCatalog();
        if (string.IsNullOrWhiteSpace(_storyId))
            _storyId = ResolveSelectedStoryId();

        EnsureCharacterPreviewState();
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        Undo.undoRedoPerformed -= RefreshLivePreviewAfterUndo;
        Undo.undoRedoPerformed += RefreshLivePreviewAfterUndo;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        Undo.undoRedoPerformed -= RefreshLivePreviewAfterUndo;
        EditorApplication.delayCall -= RefreshLivePreviewDelayed;
        _liveRefreshQueued = false;
        HideCutscenePreview(false);
        HidePhonePreview(false);
        HideCharacterPreview(false);
        RestorePreviewAppearanceState();
    }

    void OnDestroy()
    {
        ClosePreviewSafely();
    }

    void OnSelectionChange()
    {
        if (_library == null)
            _library = ResolveSelectedLibrary();

        Repaint();
    }

    void OnProjectChange()
    {
        InvalidateLivePreview();
    }

    void OnHierarchyChange()
    {
        InvalidateLivePreview();
    }

    void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        EditorGUILayout.LabelField("Interface Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Показывает только интерфейс истории: диалог, выборы, статы и плашку главы. Сюжетные ноды не запускаются.", MessageType.Info);

        _showContext = EditorGUILayout.Foldout(_showContext, "История и стиль", true);
        if (_showContext)
        {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _library = (StoryJsonAssetLibrary)EditorGUILayout.ObjectField("Story Asset Library", _library, typeof(StoryJsonAssetLibrary), false);

                    if (GUILayout.Button("Выбранное", GUILayout.Width(96f)))
                    {
                        _library = ResolveSelectedLibrary();
                        _storyId = ResolveSelectedStoryId();
                        if (_styleCatalog == null)
                            _styleCatalog = ResolveDefaultStyleCatalog();
                        _characterSlotsInitialized = false;
                        EnsureCharacterPreviewState();
                        InvalidateLivePreview();
                    }
                }

                _styleCatalog = (StoryInterfaceStyleCatalog)EditorGUILayout.ObjectField("Story UI Catalog", _styleCatalog, typeof(StoryInterfaceStyleCatalog), false);
                _storyId = EditorGUILayout.TextField("Story ID", _storyId);
                _useCutsceneStyle = EditorGUILayout.ToggleLeft("Use cutscene style", _useCutsceneStyle);
                _livePreview = EditorGUILayout.ToggleLeft("Live preview", _livePreview);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Обновить", GUILayout.Width(140f)))
                    {
                        _lastLiveSignature = null;
                        RefreshActivePreview(true);
                    }
                }
            }

            if (EditorGUI.EndChangeCheck())
                InvalidateLivePreview();
        }

        DrawCharacterPreviewControls();
        DrawPreviewScreenTabs();
        DrawPreviewScreenContent();

        if (_library == null)
            EditorGUILayout.HelpBox("Можно работать и без Story Asset Library, если стиль найден через Story UI Catalog и Story ID.", MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    void DrawPreviewModeButton(string label, PreviewMode mode, int statDelta = 1)
    {
        bool usesDelta = mode == PreviewMode.Stat || mode == PreviewMode.Relationship;
        bool active = _activePreviewMode == mode && (!usesDelta || _activeStatDelta == (statDelta >= 0 ? 1 : -1));
        Color previousColor = GUI.backgroundColor;
        if (active)
            GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);

        if (GUILayout.Button(label, GUILayout.Height(28f)))
            SetPreviewMode(mode, statDelta);

        GUI.backgroundColor = previousColor;
    }

    void DrawPreviewScreenTabs()
    {
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Экран предпросмотра", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPreviewScreenButton("Диалог", PreviewScreen.Dialogue);
                DrawPreviewScreenButton("Выборы", PreviewScreen.Choices);
                DrawPreviewScreenButton("Имя", PreviewScreen.NameInput);
                DrawPreviewScreenButton("Статы", PreviewScreen.Stats);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPreviewScreenButton("Отношения", PreviewScreen.Relationships);
                DrawPreviewScreenButton("Глава", PreviewScreen.Chapter);
                DrawPreviewScreenButton("Всё", PreviewScreen.Overview);

                if (GUILayout.Button("Скрыть", GUILayout.Height(28f)))
                    ClosePreviewSafely();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPreviewScreenButton("Катсцена", PreviewScreen.Cutscene);
                DrawPreviewScreenButton("Телефон", PreviewScreen.Phone);
            }
        }
    }

    void DrawPreviewScreenButton(string label, PreviewScreen screen)
    {
        bool active = _previewScreen == screen;
        Color previousColor = GUI.backgroundColor;
        if (active)
            GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);

        if (GUILayout.Button(label, GUILayout.Height(28f)))
            SelectPreviewScreen(screen);

        GUI.backgroundColor = previousColor;
    }

    void SelectPreviewScreen(PreviewScreen screen)
    {
        _previewScreen = screen;
        switch (screen)
        {
            case PreviewScreen.Dialogue:
                SetPreviewMode(PreviewMode.Dialogue);
                break;
            case PreviewScreen.Choices:
                SetPreviewMode(PreviewMode.Choices);
                break;
            case PreviewScreen.NameInput:
                SetPreviewMode(PreviewMode.NameInput);
                break;
            case PreviewScreen.Stats:
                SetPreviewMode(PreviewMode.Stat, _activeStatDelta);
                break;
            case PreviewScreen.Relationships:
                SetPreviewMode(PreviewMode.Relationship, _activeStatDelta);
                break;
            case PreviewScreen.Chapter:
                SetPreviewMode(PreviewMode.Chapter);
                break;
            case PreviewScreen.Cutscene:
                SetPreviewMode(PreviewMode.Cutscene);
                break;
            case PreviewScreen.Phone:
                SetPreviewMode(PreviewMode.Phone);
                break;
            case PreviewScreen.Overview:
                SetPreviewMode(PreviewMode.Overview);
                break;
        }
    }

    void DrawPreviewScreenContent()
    {
        EditorGUILayout.Space(6f);

        switch (_previewScreen)
        {
            case PreviewScreen.Dialogue:
                DrawDialoguePreviewControls();
                break;
            case PreviewScreen.Choices:
                DrawChoicesPreviewControls();
                break;
            case PreviewScreen.NameInput:
                DrawNameInputPreviewControls();
                break;
            case PreviewScreen.Stats:
                DrawStatsPreviewControls();
                break;
            case PreviewScreen.Relationships:
                DrawRelationshipPreviewControls();
                break;
            case PreviewScreen.Chapter:
                DrawChapterPreviewControls();
                break;
            case PreviewScreen.Cutscene:
                DrawCutscenePreviewControls();
                break;
            case PreviewScreen.Phone:
                DrawPhonePreviewControls();
                break;
            case PreviewScreen.Overview:
                DrawOverviewPreviewControls();
                break;
        }
    }

    void DrawDialoguePreviewControls()
    {
        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Диалог", EditorStyles.boldLabel);
            _dialogueSpeaker = EditorGUILayout.TextField("Имя персонажа", _dialogueSpeaker);
            EditorGUILayout.LabelField("Текст диалога");
            _dialogueBody = EditorGUILayout.TextArea(_dialogueBody, GUILayout.MinHeight(64f));

            DrawPreviewModeButton("Показать диалог", PreviewMode.Dialogue);
        }

        if (EditorGUI.EndChangeCheck())
            InvalidateLivePreview();
    }

    void DrawChoicesPreviewControls()
    {
        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Выборы", EditorStyles.boldLabel);
            _choiceOne = EditorGUILayout.TextField("Выбор 1", _choiceOne);
            _choiceTwo = EditorGUILayout.TextField("Выбор 2", _choiceTwo);
            _choiceThree = EditorGUILayout.TextField("Выбор 3", _choiceThree);

            DrawPreviewModeButton("Показать выборы", PreviewMode.Choices);
        }

        if (EditorGUI.EndChangeCheck())
            InvalidateLivePreview();
    }

    void DrawNameInputPreviewControls()
    {
        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Экран имени", EditorStyles.boldLabel);
            _namePreviewValue = EditorGUILayout.TextField("Имя на экране", _namePreviewValue);

            DrawPreviewModeButton("Показать экран имени", PreviewMode.NameInput);
        }

        if (EditorGUI.EndChangeCheck())
            InvalidateLivePreview();
    }

    void DrawStatsPreviewControls()
    {
        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Статы", EditorStyles.boldLabel);
            DrawStatPreviewPopup();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPreviewModeButton("Стат +", PreviewMode.Stat, 1);
                DrawPreviewModeButton("Стат -", PreviewMode.Stat, -1);
            }
        }

        if (EditorGUI.EndChangeCheck())
            InvalidateLivePreview();
    }

    void DrawRelationshipPreviewControls()
    {
        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Отношения", EditorStyles.boldLabel);
            DrawRelationshipPreviewPopup();
            _relationshipPreviewName = EditorGUILayout.TextField("Имя вручную", _relationshipPreviewName);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPreviewModeButton("Отношения +", PreviewMode.Relationship, 1);
                DrawPreviewModeButton("Отношения -", PreviewMode.Relationship, -1);
            }
        }

        if (EditorGUI.EndChangeCheck())
            InvalidateLivePreview();
    }

    void DrawChapterPreviewControls()
    {
        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Глава", EditorStyles.boldLabel);
            _chapterPreviewTitle = EditorGUILayout.TextField("Текст главы", _chapterPreviewTitle);

            DrawPreviewModeButton("Показать главу", PreviewMode.Chapter);
        }

        if (EditorGUI.EndChangeCheck())
            InvalidateLivePreview();
    }

    void DrawCutscenePreviewControls()
    {
        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Cutscene", EditorStyles.boldLabel);
            _cutscenePreviewImage = (Sprite)EditorGUILayout.ObjectField("Image", _cutscenePreviewImage, typeof(Sprite), false);
            _cutscenePreviewVideo = (VideoClip)EditorGUILayout.ObjectField("Video", _cutscenePreviewVideo, typeof(VideoClip), false);
            _cutscenePreviewGif = (TextAsset)EditorGUILayout.ObjectField("GIF TextAsset", _cutscenePreviewGif, typeof(TextAsset), false);
            _cutscenePreviewHideCharacters = EditorGUILayout.ToggleLeft("Hide characters like gameplay cutscene", _cutscenePreviewHideCharacters);
            _cutscenePreviewShowText = EditorGUILayout.ToggleLeft("Show cutscene text", _cutscenePreviewShowText);

            using (new EditorGUI.DisabledScope(!_cutscenePreviewShowText))
            {
                _cutscenePreviewSpeaker = EditorGUILayout.TextField("Speaker", _cutscenePreviewSpeaker);
                EditorGUILayout.LabelField("Text");
                _cutscenePreviewText = EditorGUILayout.TextArea(_cutscenePreviewText, GUILayout.MinHeight(64f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use first story media", GUILayout.Height(28f)))
                {
                    FillCutsceneMediaFromLibrary();
                    InvalidateLivePreview();
                }

                DrawPreviewModeButton("Show cutscene", PreviewMode.Cutscene);
            }
        }

        if (EditorGUI.EndChangeCheck())
            InvalidateLivePreview();
    }

    void DrawPhonePreviewControls()
    {
        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Телефон: предпросмотр сообщений", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Использует существующий PhoneDialogueUI в открытой сцене. В Edit Mode показывает статичный экран без DOTween и корутин; в Play Mode запускает настоящий runtime-показ.",
                MessageType.Info);

            PhoneDialogueUI scenePhoneUi = PhoneDialoguePreviewSetup.FindScenePhoneUi();
            StoryUserInterface storyUserInterface = PhoneDialoguePreviewSetup.FindSceneStoryUserInterface();

            _phonePreviewSourceNode = (PhoneDialogueNode)EditorGUILayout.ObjectField("PhoneDialogueNode", _phonePreviewSourceNode, typeof(PhoneDialogueNode), false);

            using (new EditorGUI.DisabledScope(_phonePreviewSourceNode != null))
            {
                PhonePreviewSettings previewSettings = storyUserInterface != null
                    ? storyUserInterface.PhonePreviewSettings
                    : null;
                string phonePreviewContactName = previewSettings != null
                    ? previewSettings.quickPreviewContactName
                    : _phonePreviewContactName;
                phonePreviewContactName = EditorGUILayout.TextField("Контакт", phonePreviewContactName);
                if (phonePreviewContactName != _phonePreviewContactName)
                {
                    _phonePreviewContactName = phonePreviewContactName;
                    if (previewSettings != null)
                    {
                        Undo.RecordObject(storyUserInterface, "Change Phone Preview Contact");
                        previewSettings.quickPreviewContactName = phonePreviewContactName;
                        previewSettings.Normalize();
                        EditorUtility.SetDirty(storyUserInterface);
                    }
                }
                _phonePreviewAvatar = (Sprite)EditorGUILayout.ObjectField("Аватар", _phonePreviewAvatar, typeof(Sprite), false);
                _phonePreviewAttachment = (Sprite)EditorGUILayout.ObjectField("Фото по умолчанию", _phonePreviewAttachment, typeof(Sprite), false);
                _phonePreviewTypingDelay = Mathf.Max(0f, EditorGUILayout.FloatField("Задержка печати", _phonePreviewTypingDelay));
                EditorGUILayout.LabelField("Сценарий");
                _phonePreviewScript = EditorGUILayout.TextArea(_phonePreviewScript ?? "", GUILayout.MinHeight(92f));
            }

            _phonePreviewHideCharacters = EditorGUILayout.ToggleLeft("Скрывать персонажей истории", _phonePreviewHideCharacters);

            if (scenePhoneUi == null)
            {
                EditorGUILayout.HelpBox("В сцене нет PhoneDialogueUI. Нажми кнопку ниже, чтобы создать или настроить экран телефона из существующих ассетов.", MessageType.Warning);
                if (GUILayout.Button("Создать/настроить PhoneDialogueUI", GUILayout.Height(28f)))
                {
                    scenePhoneUi = PhoneDialoguePreviewSetup.CreateOrConfigureInOpenScene();
                    if (scenePhoneUi != null)
                        InvalidateLivePreview();
                }
            }
            else if (storyUserInterface == null)
            {
                EditorGUILayout.HelpBox("PhoneDialogueUI найден, но StoryUserInterface ещё не хранит ссылки телефона. Нажми кнопку ниже для миграции.", MessageType.Warning);
                if (GUILayout.Button("Мигрировать в StoryUserInterface", GUILayout.Height(28f)))
                {
                    storyUserInterface = PhoneDialoguePreviewSetup.FindOrCreateStoryUserInterface(scenePhoneUi);
                    if (storyUserInterface != null)
                    {
                        storyUserInterface.MigratePhoneReferencesFromLegacyPhoneDialogueUI(overwrite: false);
                        storyUserInterface.ApplyPhoneConfiguration(nameof(StoryInterfacePreviewWindow));
                        InvalidateLivePreview();
                    }
                }
            }
            else if (!PhoneDialoguePreviewSetup.IsAssignedToStoryManager(scenePhoneUi))
            {
                EditorGUILayout.HelpBox("PhoneDialogueUI найден, но StoryManager.phoneDialogueUI не назначен. Runtime phone-ноды будут пропускаться, пока ссылка не назначена.", MessageType.Warning);
                if (GUILayout.Button("Назначить в StoryManager", GUILayout.Height(28f)))
                {
                    PhoneDialoguePreviewSetup.AssignToStoryManager(scenePhoneUi);
                    InvalidateLivePreview();
                }
            }
            else if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Edit Mode: кнопка ниже сразу отрисует статичный phone preview в Game view.", MessageType.None);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPreviewModeButton("Показать телефон", PreviewMode.Phone);
                if (GUILayout.Button("Скрыть телефон", GUILayout.Height(28f)))
                    HidePhonePreview(true);
            }
        }

        if (EditorGUI.EndChangeCheck())
            InvalidateLivePreview();
    }

    void DrawOverviewPreviewControls()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Всё сразу", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Показывает диалог, выборы, выбранный стат и плашку главы вместе. Тексты меняются в отдельных экранах выше.", MessageType.Info);
            DrawPreviewModeButton("Показать всё", PreviewMode.Overview);
        }
    }

    void DrawCharacterPreviewControls()
    {
        EnsureCharacterPreviewState();

        EditorGUILayout.Space(6f);
        _showCharacterControls = EditorGUILayout.Foldout(_showCharacterControls, "Characters in gameplay preview", true);
        if (!_showCharacterControls)
            return;

        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _previewCharacters = EditorGUILayout.ToggleLeft("Show characters", _previewCharacters);

            using (new EditorGUI.DisabledScope(!_previewCharacters))
            {
                _previewAppearance = (AppearanceType)EditorGUILayout.EnumPopup("Hero appearance", _previewAppearance);
                DrawEquipmentPreviewControls("Outfit", ClothingType.Outfit, ref _outfitPreviewMode, ref _previewOutfit);
                DrawEquipmentPreviewControls("Hair", ClothingType.Hair, ref _hairPreviewMode, ref _previewHair);

                EditorGUILayout.Space(4f);
                DrawCharacterPresetButtons();
                DrawCharacterSlotControls("Left", _leftCharacter);
                DrawCharacterSlotControls("Center", _centerCharacter);
                DrawCharacterSlotControls("Right", _rightCharacter);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!_previewCharacters))
                {
                    if (GUILayout.Button("Auto fill", GUILayout.Width(96f)))
                    {
                        AutoFillCharacterSlots();
                        InvalidateLivePreview();
                    }
                }

                if (GUILayout.Button("Hide characters", GUILayout.Width(120f)))
                {
                    _previewCharacters = false;
                    HideCharacterPreview(true);
                    RestorePreviewAppearanceState();
                    InvalidateLivePreview();
                }
            }

            if (_previewCharacters && FindSceneObject<CharacterViewManager>() == null)
                EditorGUILayout.HelpBox("Open a scene with CharacterViewManager to preview story characters.", MessageType.Warning);
        }

        if (EditorGUI.EndChangeCheck())
            InvalidateLivePreview();
    }

    void DrawEquipmentPreviewControls(
        string label,
        ClothingType type,
        ref PreviewEquipmentMode mode,
        ref ClothingItem item)
    {
        string[] modeLabels = { "Saved/current", "None", "Selected item" };
        int modeIndex = Mathf.Clamp((int)mode, 0, modeLabels.Length - 1);
        mode = (PreviewEquipmentMode)EditorGUILayout.Popup(label + " mode", modeIndex, modeLabels);
        if (mode != PreviewEquipmentMode.Override)
            return;

        DrawClothingPopup(label, type, ref item);
    }

    void DrawClothingPopup(string label, ClothingType type, ref ClothingItem item)
    {
        List<ClothingPreviewEntry> entries = BuildClothingPreviewEntries(_library, type);
        if (entries.Count > 0)
        {
            int selectedIndex = FindClothingPreviewIndex(entries, item);
            string[] labels = new string[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                labels[i] = entries[i].Label;

            int nextIndex = EditorGUILayout.Popup(label, selectedIndex, labels);
            item = entries[Mathf.Clamp(nextIndex, 0, entries.Count - 1)].Item;
        }
        else
        {
            EditorGUILayout.HelpBox("No " + type + " assets found.", MessageType.Info);
        }

        item = (ClothingItem)EditorGUILayout.ObjectField(label + " asset", item, typeof(ClothingItem), false);
        if (item != null && item.type != type)
            item = null;
    }

    void DrawCharacterPresetButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Solo Left"))
                ApplyCharacterPreset(true, false, false);
            if (GUILayout.Button("Solo Center"))
                ApplyCharacterPreset(false, true, false);
            if (GUILayout.Button("Solo Right"))
                ApplyCharacterPreset(false, false, true);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Left + Center"))
                ApplyCharacterPreset(true, true, false);
            if (GUILayout.Button("Left + Right"))
                ApplyCharacterPreset(true, false, true);
            if (GUILayout.Button("Center + Right"))
                ApplyCharacterPreset(false, true, true);
            if (GUILayout.Button("All 3"))
                ApplyCharacterPreset(true, true, true);
        }
    }

    void DrawCharacterSlotControls(string label, CharacterPreviewSlot slot)
    {
        if (slot == null)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            slot.enabled = EditorGUILayout.ToggleLeft(label, slot.enabled);
            using (new EditorGUI.DisabledScope(!slot.enabled))
            {
                DrawCharacterPopup("Character", ref slot.character);
                slot.emotion = (CharacterEmotionType)EditorGUILayout.EnumPopup("Emotion", slot.emotion);
            }
        }
    }

    void DrawCharacterPopup(string label, ref CharacterData character)
    {
        List<CharacterPreviewEntry> entries = BuildCharacterPreviewEntries(_library, character);
        int selectedIndex = FindCharacterPreviewIndex(entries, character);
        string[] labels = new string[entries.Count];
        for (int i = 0; i < entries.Count; i++)
            labels[i] = entries[i].Label;

        int nextIndex = EditorGUILayout.Popup(label, selectedIndex, labels);
        character = entries[Mathf.Clamp(nextIndex, 0, entries.Count - 1)].Character;

        character = (CharacterData)EditorGUILayout.ObjectField(label + " asset", character, typeof(CharacterData), false);
    }

    string[] BuildChoicePreviewTexts()
    {
        var choices = new List<string>();
        AddChoicePreviewText(choices, _choiceOne);
        AddChoicePreviewText(choices, _choiceTwo);
        AddChoicePreviewText(choices, _choiceThree);
        return choices.Count > 0 ? choices.ToArray() : new[] { "Согласиться", "Задать вопрос", "Промолчать" };
    }

    static void AddChoicePreviewText(List<string> choices, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            choices.Add(value);
    }

    void FillCutsceneMediaFromLibrary()
    {
        if (_library == null || _library.Assets == null)
            return;

        IReadOnlyList<StoryJsonAssetReference> assets = _library.Assets;
        VideoClip firstVideo = null;
        TextAsset firstGif = null;
        Sprite firstImage = null;

        for (int i = 0; i < assets.Count; i++)
        {
            StoryJsonAssetReference asset = assets[i];
            if (asset == null)
                continue;

            if (firstVideo == null && asset.Video != null)
                firstVideo = asset.Video;
            if (firstGif == null && IsLikelyGifAsset(asset.Id, asset.TextAsset))
                firstGif = asset.TextAsset;
            if (firstImage == null && asset.Sprite != null)
                firstImage = asset.Sprite;

            bool hasNeededVideo = _cutscenePreviewVideo != null || firstVideo != null;
            bool hasNeededGif = _cutscenePreviewGif != null || firstGif != null;
            bool hasNeededImage = _cutscenePreviewImage != null || firstImage != null;
            if (hasNeededVideo && hasNeededGif && hasNeededImage)
                break;
        }

        if (_cutscenePreviewVideo == null)
            _cutscenePreviewVideo = firstVideo;
        if (_cutscenePreviewGif == null)
            _cutscenePreviewGif = firstGif;
        if (_cutscenePreviewImage == null)
            _cutscenePreviewImage = firstImage;
    }

    static bool IsLikelyGifAsset(string id, TextAsset asset)
    {
        if (asset == null)
            return false;

        string value = ((id ?? "") + " " + asset.name + " " + AssetDatabase.GetAssetPath(asset)).ToLowerInvariant();
        return value.Contains(".gif") || value.Contains("_gif") || value.Contains(" gif");
    }

    void EnsureCharacterPreviewState()
    {
        if (_leftCharacter == null)
            _leftCharacter = new CharacterPreviewSlot { enabled = true };
        if (_centerCharacter == null)
            _centerCharacter = new CharacterPreviewSlot();
        if (_rightCharacter == null)
            _rightCharacter = new CharacterPreviewSlot();

        if (!_previewAppearanceInitialized)
        {
            _previewAppearance = PlayerAppearance.CurrentAppearance;
            _previewAppearanceInitialized = true;
        }

        if (!_characterSlotsInitialized)
        {
            AutoFillCharacterSlots();
            _characterSlotsInitialized = true;
        }
    }

    void ApplyCharacterPreset(bool left, bool center, bool right)
    {
        EnsureCharacterPreviewState();
        _previewCharacters = true;
        _leftCharacter.enabled = left;
        _centerCharacter.enabled = center;
        _rightCharacter.enabled = right;
        AutoFillCharacterSlots();
        InvalidateLivePreview();
    }

    void AutoFillCharacterSlots()
    {
        List<CharacterData> characters = BuildAvailableCharacters(_library);
        if (characters.Count == 0)
            return;

        var used = new HashSet<CharacterData>();
        RegisterAssignedCharacter(_leftCharacter, used);
        RegisterAssignedCharacter(_centerCharacter, used);
        RegisterAssignedCharacter(_rightCharacter, used);

        FillCharacterSlot(_leftCharacter, characters, used);
        FillCharacterSlot(_centerCharacter, characters, used);
        FillCharacterSlot(_rightCharacter, characters, used);
    }

    static void RegisterAssignedCharacter(CharacterPreviewSlot slot, HashSet<CharacterData> used)
    {
        if (slot != null && slot.enabled && slot.character != null)
            used.Add(slot.character);
    }

    static void FillCharacterSlot(
        CharacterPreviewSlot slot,
        IReadOnlyList<CharacterData> characters,
        HashSet<CharacterData> used)
    {
        if (slot == null || !slot.enabled || slot.character != null)
            return;

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterData candidate = characters[i];
            if (candidate == null || used.Contains(candidate))
                continue;

            slot.character = candidate;
            used.Add(candidate);
            return;
        }

        slot.character = characters[0];
    }

    static List<CharacterData> BuildAvailableCharacters(StoryJsonAssetLibrary library)
    {
        var result = new List<CharacterData>();
        var seen = new HashSet<int>();

        if (library != null && library.Assets != null)
        {
            IReadOnlyList<StoryJsonAssetReference> assets = library.Assets;
            for (int i = 0; i < assets.Count; i++)
            {
                CharacterData character = assets[i] != null ? assets[i].Character : null;
                AddAvailableCharacter(result, seen, character);
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:CharacterData");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AddAvailableCharacter(result, seen, AssetDatabase.LoadAssetAtPath<CharacterData>(path));
        }

        return result;
    }

    static void AddAvailableCharacter(List<CharacterData> result, HashSet<int> seen, CharacterData character)
    {
        if (character == null)
            return;

        int id = character.GetInstanceID();
        if (!seen.Add(id))
            return;

        result.Add(character);
    }

    static List<CharacterPreviewEntry> BuildCharacterPreviewEntries(
        StoryJsonAssetLibrary library,
        CharacterData selectedCharacter)
    {
        var result = new List<CharacterPreviewEntry>();
        var seen = new HashSet<int>();
        result.Add(new CharacterPreviewEntry(null, "None", -1));

        if (library != null && library.Assets != null)
        {
            IReadOnlyList<StoryJsonAssetReference> assets = library.Assets;
            for (int i = 0; i < assets.Count; i++)
            {
                StoryJsonAssetReference asset = assets[i];
                CharacterData character = asset != null ? asset.Character : null;
                AddCharacterPreviewEntry(result, seen, character, asset != null ? asset.Id : "", i);
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:CharacterData");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            CharacterData character = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            AddCharacterPreviewEntry(result, seen, character, "", 10000 + i);
        }

        if (selectedCharacter != null)
            AddCharacterPreviewEntry(result, seen, selectedCharacter, "", 50000);

        result.Sort((left, right) =>
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    static void AddCharacterPreviewEntry(
        List<CharacterPreviewEntry> result,
        HashSet<int> seen,
        CharacterData character,
        string id,
        int order)
    {
        if (character == null)
            return;

        int key = character.GetInstanceID();
        if (!seen.Add(key))
            return;

        result.Add(new CharacterPreviewEntry(
            character,
            FirstNonEmpty(character.characterName, id, character.name),
            order));
    }

    static int FindCharacterPreviewIndex(IReadOnlyList<CharacterPreviewEntry> entries, CharacterData character)
    {
        if (entries == null || entries.Count == 0 || character == null)
            return 0;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Character == character)
                return i;
        }

        return 0;
    }

    static List<ClothingPreviewEntry> BuildClothingPreviewEntries(StoryJsonAssetLibrary library, ClothingType type)
    {
        var result = new List<ClothingPreviewEntry>();
        var seen = new HashSet<int>();
        result.Add(new ClothingPreviewEntry(null, "Select item...", -1));

        if (library != null && library.Assets != null)
        {
            IReadOnlyList<StoryJsonAssetReference> assets = library.Assets;
            for (int i = 0; i < assets.Count; i++)
            {
                StoryJsonAssetReference asset = assets[i];
                ClothingItem item = asset != null ? asset.Clothing : null;
                AddClothingPreviewEntry(result, seen, item, asset != null ? asset.Id : "", i, type);
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:ClothingItem");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ClothingItem item = AssetDatabase.LoadAssetAtPath<ClothingItem>(path);
            AddClothingPreviewEntry(result, seen, item, "", 10000 + i, type);
        }

        result.Sort((left, right) =>
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    static void AddClothingPreviewEntry(
        List<ClothingPreviewEntry> result,
        HashSet<int> seen,
        ClothingItem item,
        string id,
        int order,
        ClothingType type)
    {
        if (item == null || item.type != type)
            return;

        int key = item.GetInstanceID();
        if (!seen.Add(key))
            return;

        result.Add(new ClothingPreviewEntry(
            item,
            FirstNonEmpty(item.DisplayName, item.id, id, item.name),
            order));
    }

    static int FindClothingPreviewIndex(IReadOnlyList<ClothingPreviewEntry> entries, ClothingItem item)
    {
        if (entries == null || entries.Count == 0 || item == null)
            return 0;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Item == item)
                return i;
        }

        return 0;
    }

    void DrawStatPreviewPopup()
    {
        List<StatPreviewEntry> entries = BuildStatPreviewEntries(_library, _styleCatalog, _storyId, _useCutsceneStyle);
        if (entries.Count == 0)
        {
            _statPreviewIndex = 0;
            return;
        }

        string[] labels = new string[entries.Count];
        for (int i = 0; i < entries.Count; i++)
            labels[i] = entries[i].Label;

        _statPreviewIndex = Mathf.Clamp(_statPreviewIndex, 0, entries.Count - 1);
        _statPreviewIndex = EditorGUILayout.Popup("Stat preview", _statPreviewIndex, labels);
    }

    void DrawRelationshipPreviewPopup()
    {
        List<RelationshipPreviewEntry> entries = BuildRelationshipPreviewEntries(_library, _styleCatalog, _storyId, _useCutsceneStyle);
        if (entries.Count == 0)
        {
            _relationshipPreviewIndex = 0;
            return;
        }

        string[] labels = new string[entries.Count];
        for (int i = 0; i < entries.Count; i++)
            labels[i] = entries[i].Label;

        _relationshipPreviewIndex = Mathf.Clamp(_relationshipPreviewIndex, 0, entries.Count - 1);
        _relationshipPreviewIndex = EditorGUILayout.Popup("Персонаж", _relationshipPreviewIndex, labels);
        EditorGUILayout.LabelField("Stat ID", entries[_relationshipPreviewIndex].StatId);
    }

    void SetPreviewMode(PreviewMode mode, int statDelta = 1)
    {
        _activePreviewMode = mode;
        _activeStatDelta = statDelta >= 0 ? 1 : -1;
        _lastLiveSignature = null;
        RefreshActivePreview(true);
    }

    void OnEditorUpdate()
    {
        if (!_livePreview || _activePreviewMode == PreviewMode.None)
            return;

        if (EditorApplication.timeSinceStartup < _nextLiveRefreshTime)
            return;

        _nextLiveRefreshTime = EditorApplication.timeSinceStartup + 0.12d;

        string signature = BuildCurrentLiveSignature();

        if (signature == _lastLiveSignature)
            return;

        RefreshActivePreview(false);
    }

    void RefreshLivePreviewAfterUndo()
    {
        InvalidateLivePreview();
    }

    void InvalidateLivePreview()
    {
        _lastLiveSignature = null;
        _nextLiveRefreshTime = 0d;
        QueueLivePreviewRefresh();
        Repaint();
    }

    void QueueLivePreviewRefresh()
    {
        if (!_livePreview || _activePreviewMode == PreviewMode.None || _liveRefreshQueued)
            return;

        _liveRefreshQueued = true;
        EditorApplication.delayCall += RefreshLivePreviewDelayed;
    }

    void RefreshLivePreviewDelayed()
    {
        _liveRefreshQueued = false;
        EditorApplication.delayCall -= RefreshLivePreviewDelayed;

        if (this == null || !_livePreview || _activePreviewMode == PreviewMode.None)
            return;

        _nextLiveRefreshTime = 0d;
        RefreshActivePreview(false);
        Repaint();
    }

    void ClosePreviewSafely()
    {
        _activePreviewMode = PreviewMode.None;
        _lastLiveSignature = null;
        _liveRefreshQueued = false;
        EditorApplication.delayCall -= RefreshLivePreviewDelayed;
        HidePreview(false);
        HideCutscenePreview(false);
        HidePhonePreview(false);
        HideCharacterPreview(false);
        RestorePreviewAppearanceState();
    }

    bool UsesStyle(StoryUiStyle style)
    {
        if (style == null)
            return true;

        bool resolveCutsceneStyle = _activePreviewMode == PreviewMode.Cutscene || _useCutsceneStyle;
        ResolveStyle(_library, _styleCatalog, _storyId, resolveCutsceneStyle, out StoryUiStyle resolvedStyle, out _);
        return resolvedStyle == null || resolvedStyle == style;
    }

    void RefreshActivePreview(bool registerUndo)
    {
        if (_activePreviewMode != PreviewMode.Cutscene)
            HideCutscenePreview(false);
        if (_activePreviewMode != PreviewMode.Phone)
            HidePhonePreview(false);

        switch (_activePreviewMode)
        {
            case PreviewMode.Dialogue:
                PreviewDialogue(_library, _styleCatalog, _storyId, _useCutsceneStyle, _dialogueSpeaker, _dialogueBody, registerUndo);
                break;
            case PreviewMode.Choices:
                PreviewChoices(_library, _styleCatalog, _storyId, _useCutsceneStyle, BuildChoicePreviewTexts(), registerUndo);
                break;
            case PreviewMode.NameInput:
                PreviewNameInput(_library, _styleCatalog, _storyId, _useCutsceneStyle, _namePreviewValue, registerUndo);
                break;
            case PreviewMode.Stat:
                PreviewStat(_library, _styleCatalog, _storyId, _useCutsceneStyle, _statPreviewIndex, _activeStatDelta, registerUndo, true);
                break;
            case PreviewMode.Relationship:
                PreviewRelationship(_library, _styleCatalog, _storyId, _useCutsceneStyle, _relationshipPreviewIndex, _relationshipPreviewName, _activeStatDelta, registerUndo, true);
                break;
            case PreviewMode.Chapter:
                PreviewChapter(_library, _styleCatalog, _storyId, _useCutsceneStyle, _chapterPreviewTitle, registerUndo, true, true);
                break;
            case PreviewMode.Cutscene:
                PreviewCutscene(
                    _library,
                    _styleCatalog,
                    _storyId,
                    _cutscenePreviewImage,
                    _cutscenePreviewVideo,
                    _cutscenePreviewGif,
                    _cutscenePreviewSpeaker,
                    _cutscenePreviewText,
                    _cutscenePreviewShowText,
                    _cutscenePreviewHideCharacters,
                    registerUndo);
                break;
            case PreviewMode.Phone:
                PreviewPhone(registerUndo);
                break;
            case PreviewMode.Overview:
                PreviewOverview(_library, _styleCatalog, _storyId, _useCutsceneStyle, BuildChoicePreviewTexts(), _dialogueSpeaker, _dialogueBody, _chapterPreviewTitle, _statPreviewIndex, _activeStatDelta, registerUndo);
                break;
        }

        bool cutsceneHidesCharacters = _activePreviewMode == PreviewMode.Cutscene && _cutscenePreviewHideCharacters;
        bool phoneHidesCharacters = _activePreviewMode == PreviewMode.Phone && _phonePreviewHideCharacters;
        if (!cutsceneHidesCharacters && !phoneHidesCharacters)
            ApplyCharacterPreview(registerUndo);

        _lastLiveSignature = BuildCurrentLiveSignature();
    }

    string BuildCurrentLiveSignature()
    {
        var builder = new StringBuilder(4096);
        builder.Append(BuildLiveSignature(
            _library,
            _styleCatalog,
            _storyId,
            _activePreviewMode == PreviewMode.Cutscene || _useCutsceneStyle,
            _activePreviewMode,
            _activePreviewMode == PreviewMode.Relationship ? _relationshipPreviewIndex : _statPreviewIndex,
            _activeStatDelta));

        builder.Append(_dialogueSpeaker ?? "").Append('|')
            .Append(_dialogueBody ?? "").Append('|')
            .Append(_choiceOne ?? "").Append('|')
            .Append(_choiceTwo ?? "").Append('|')
            .Append(_choiceThree ?? "").Append('|')
            .Append(_namePreviewValue ?? "").Append('|')
            .Append(_chapterPreviewTitle ?? "").Append('|')
            .Append(_relationshipPreviewName ?? "").Append('|');

        AppendCutscenePreviewSignature(builder);
        AppendPhonePreviewSignature(builder);
        AppendCharacterPreviewSignature(builder);

        if (_activePreviewMode == PreviewMode.Chapter || _activePreviewMode == PreviewMode.Overview)
            AppendObjectSignature(builder, FindSceneObject<ChapterTitleOverlay>());
        if (_activePreviewMode == PreviewMode.Dialogue || _activePreviewMode == PreviewMode.Choices || _activePreviewMode == PreviewMode.Overview)
            AppendObjectSignature(builder, FindSceneObject<DialogueUIManager>());
        if (_activePreviewMode == PreviewMode.Relationship)
            AppendObjectSignature(builder, FindSceneObject<StatChangeOverlay>());

        return builder.ToString();
    }

    void AppendCutscenePreviewSignature(StringBuilder builder)
    {
        builder.Append(_cutscenePreviewSpeaker ?? "").Append('|')
            .Append(_cutscenePreviewText ?? "").Append('|')
            .Append(_cutscenePreviewShowText ? 1 : 0).Append('|')
            .Append(_cutscenePreviewHideCharacters ? 1 : 0).Append('|');

        AppendObjectSignature(builder, _cutscenePreviewImage);
        AppendObjectSignature(builder, _cutscenePreviewVideo);
        AppendObjectSignature(builder, _cutscenePreviewGif);

        if (_activePreviewMode == PreviewMode.Cutscene)
        {
            AppendObjectSignature(builder, FindSceneObject<BackgroundViewManager>());
            AppendObjectSignature(builder, FindCutsceneDialogueUi());
        }
    }

    void AppendPhonePreviewSignature(StringBuilder builder)
    {
        builder.Append(_phonePreviewContactName ?? "").Append('|')
            .Append(_phonePreviewScript ?? "").Append('|')
            .Append(_phonePreviewTypingDelay).Append('|')
            .Append(_phonePreviewHideCharacters ? 1 : 0).Append('|');

        AppendObjectSignature(builder, _phonePreviewSourceNode);
        AppendObjectSignature(builder, _phonePreviewAvatar);
        AppendObjectSignature(builder, _phonePreviewAttachment);

        if (_activePreviewMode == PreviewMode.Phone)
            AppendObjectSignature(builder, FindSceneObject<PhoneDialogueUI>());
    }

    void AppendCharacterPreviewSignature(StringBuilder builder)
    {
        builder.Append(_previewCharacters ? 1 : 0).Append('|')
            .Append((int)_previewAppearance).Append('|')
            .Append((int)_outfitPreviewMode).Append('|')
            .Append((int)_hairPreviewMode).Append('|');

        AppendCharacterSlotSignature(builder, _leftCharacter);
        AppendCharacterSlotSignature(builder, _centerCharacter);
        AppendCharacterSlotSignature(builder, _rightCharacter);
        AppendObjectSignature(builder, _previewOutfit);
        AppendObjectSignature(builder, _previewHair);

        if (_previewCharacters)
            AppendObjectSignature(builder, FindSceneObject<CharacterViewManager>());
    }

    static void AppendCharacterSlotSignature(StringBuilder builder, CharacterPreviewSlot slot)
    {
        if (slot == null)
        {
            builder.Append("<slot:null>|");
            return;
        }

        builder.Append(slot.enabled ? 1 : 0).Append('|')
            .Append((int)slot.emotion).Append('|');
        AppendObjectSignature(builder, slot.character);
    }

    void ApplyCharacterPreview(bool registerUndo)
    {
        EnsureCharacterPreviewState();

        if (!_previewCharacters)
        {
            HideCharacterPreview(registerUndo);
            RestorePreviewAppearanceState();
            return;
        }

        bool leftUsed = IsCharacterSlotUsed(_leftCharacter);
        bool centerUsed = IsCharacterSlotUsed(_centerCharacter);
        bool rightUsed = IsCharacterSlotUsed(_rightCharacter);
        if (!leftUsed && !centerUsed && !rightUsed)
        {
            HideCharacterPreview(registerUndo);
            RestorePreviewAppearanceState();
            return;
        }

        CharacterViewManager characterView = FindSceneObject<CharacterViewManager>();
        if (characterView == null)
        {
            if (registerUndo)
                ShowMissingCharacterViewDialog();
            return;
        }

        CapturePreviewAppearanceStateIfNeeded();
        ApplyPreviewAppearanceState();

        if (registerUndo)
            Undo.RegisterFullObjectHierarchyUndo(characterView.gameObject, "Preview Story Characters");

        ApplyCharacterSlotPreview(characterView, _leftCharacter, CharacterPosition.Left);
        ApplyCharacterSlotPreview(characterView, _centerCharacter, CharacterPosition.Center);
        ApplyCharacterSlotPreview(characterView, _rightCharacter, CharacterPosition.Right);
        characterView.DisableUnused(leftUsed, centerUsed, rightUsed);
        EditorUtility.SetDirty(characterView);
        RepaintEditorViews();
    }

    static bool IsCharacterSlotUsed(CharacterPreviewSlot slot)
    {
        return slot != null && slot.enabled && slot.character != null;
    }

    static void ApplyCharacterSlotPreview(
        CharacterViewManager characterView,
        CharacterPreviewSlot slot,
        CharacterPosition position)
    {
        if (characterView == null || !IsCharacterSlotUsed(slot))
            return;

        characterView.SetupCharacter(slot.character, slot.emotion, position);
    }

    void CapturePreviewAppearanceStateIfNeeded()
    {
        if (_hasCapturedAppearanceState)
            return;

        _capturedAppearanceState = HeroCustomizationState.CaptureCurrent();
        _capturedOutfitItem = PlayerAppearance.OutfitItem;
        _capturedHairItem = PlayerAppearance.HairItem;
        _capturedAccessoryItem = PlayerAppearance.AccessoryItem;
        _capturedOutfitSprite = PlayerAppearance.OutfitSprite;
        _capturedHairSprite = PlayerAppearance.HairSprite;
        _capturedAccessorySprite = PlayerAppearance.AccessorySprite;
        _hasCapturedAppearanceState = true;
    }

    void ApplyPreviewAppearanceState()
    {
        HeroCustomizationState captured = _capturedAppearanceState ?? HeroCustomizationState.CaptureCurrent();
        var state = new HeroCustomizationState
        {
            playerName = captured.playerName,
            appearance = _previewAppearance,
            outfitId = ResolvePreviewEquipmentId(_outfitPreviewMode, _previewOutfit, captured.outfitId),
            hairId = ResolvePreviewEquipmentId(_hairPreviewMode, _previewHair, captured.hairId),
            accessoryId = captured.accessoryId
        }.Normalized();

        PlayerAppearance.ApplyState(
            state,
            outfitSprite: ResolvePreviewEquipmentSprite(_outfitPreviewMode, _previewOutfit, _capturedOutfitSprite),
            hairSprite: ResolvePreviewEquipmentSprite(_hairPreviewMode, _previewHair, _capturedHairSprite),
            outfitItem: ResolvePreviewEquipmentItem(_outfitPreviewMode, _previewOutfit, _capturedOutfitItem, ClothingType.Outfit),
            hairItem: ResolvePreviewEquipmentItem(_hairPreviewMode, _previewHair, _capturedHairItem, ClothingType.Hair),
            accessorySprite: _capturedAccessorySprite,
            accessoryItem: _capturedAccessoryItem,
            save: false,
            notify: true);
    }

    static string ResolvePreviewEquipmentId(
        PreviewEquipmentMode mode,
        ClothingItem item,
        string capturedId)
    {
        switch (mode)
        {
            case PreviewEquipmentMode.None:
                return "";
            case PreviewEquipmentMode.Override:
                return item != null ? FirstNonEmpty(item.id, item.name) : "";
            default:
                return capturedId ?? "";
        }
    }

    static Sprite ResolvePreviewEquipmentSprite(
        PreviewEquipmentMode mode,
        ClothingItem item,
        Sprite capturedSprite)
    {
        switch (mode)
        {
            case PreviewEquipmentMode.None:
                return null;
            case PreviewEquipmentMode.Override:
                return item != null ? item.sprite : null;
            default:
                return capturedSprite;
        }
    }

    static ClothingItem ResolvePreviewEquipmentItem(
        PreviewEquipmentMode mode,
        ClothingItem item,
        ClothingItem capturedItem,
        ClothingType type)
    {
        switch (mode)
        {
            case PreviewEquipmentMode.None:
                return null;
            case PreviewEquipmentMode.Override:
                return item != null && item.type == type ? item : null;
            default:
                return capturedItem != null && capturedItem.type == type ? capturedItem : null;
        }
    }

    void RestorePreviewAppearanceState()
    {
        if (!_hasCapturedAppearanceState)
            return;

        HeroCustomizationState state = _capturedAppearanceState ?? new HeroCustomizationState();
        PlayerAppearance.ApplyState(
            state,
            outfitSprite: _capturedOutfitSprite,
            hairSprite: _capturedHairSprite,
            outfitItem: _capturedOutfitItem,
            hairItem: _capturedHairItem,
            accessorySprite: _capturedAccessorySprite,
            accessoryItem: _capturedAccessoryItem,
            save: false,
            notify: true);

        _capturedAppearanceState = null;
        _capturedOutfitItem = null;
        _capturedHairItem = null;
        _capturedAccessoryItem = null;
        _capturedOutfitSprite = null;
        _capturedHairSprite = null;
        _capturedAccessorySprite = null;
        _hasCapturedAppearanceState = false;
    }

    static void PreviewDialogue(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        bool useCutsceneStyle,
        string speakerName,
        string bodyText,
        bool registerUndo)
    {
        DialogueUIManager ui = FindSceneObject<DialogueUIManager>();
        if (ui == null)
        {
            if (registerUndo)
                ShowMissingSceneUiDialog();
            return;
        }

        ResolveStyle(library, styleCatalog, storyId, useCutsceneStyle, out StoryUiStyle style, out Sprite sprite);
        if (registerUndo)
            Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Preview Dialogue UI");

        ApplyStoryPreviewBackground(library, storyId, registerUndo);
        HideStatOverlay();
        HideChapterOverlay();
        HideNamePreview(registerUndo);
        if (!string.IsNullOrWhiteSpace(speakerName) || !string.IsNullOrWhiteSpace(bodyText))
        {
            ui.PreviewDialogueInterface(
                style,
                sprite,
                FirstNonEmpty(speakerName, "Элисон"),
                FirstNonEmpty(bodyText, "Текст диалога для проверки отступов, плашки, шрифта и стиля текущей истории."));
            RepaintEditorViews();
            return;
        }

        ui.PreviewDialogueInterface(
            style,
            sprite,
            "Элисон",
            "Текст диалога для проверки отступов, плашки, шрифта и стиля текущей истории.");
        RepaintEditorViews();
    }

    static void PreviewChoices(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        bool useCutsceneStyle,
        IReadOnlyList<string> choices,
        bool registerUndo)
    {
        DialogueUIManager ui = FindSceneObject<DialogueUIManager>();
        if (ui == null)
        {
            if (registerUndo)
                ShowMissingSceneUiDialog();
            return;
        }

        ResolveStyle(library, styleCatalog, storyId, useCutsceneStyle, out StoryUiStyle style, out Sprite sprite);
        if (registerUndo)
            Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Preview Choice UI");

        ApplyStoryPreviewBackground(library, storyId, registerUndo);
        HideStatOverlay();
        HideChapterOverlay();
        HideNamePreview(registerUndo);
        ui.PreviewChoiceInterface(style, sprite, choices);
        RepaintEditorViews();
    }

    static void PreviewNameInput(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        bool useCutsceneStyle,
        string previewName,
        bool registerUndo)
    {
        PreStorySetupFlow setupFlow = FindSceneObject<PreStorySetupFlow>();
        if (setupFlow == null)
        {
            if (registerUndo)
                ShowMissingSceneUiDialog();
            return;
        }

        ResolveStyle(library, styleCatalog, storyId, useCutsceneStyle, out StoryUiStyle style, out _);

        DialogueUIManager ui = FindSceneObject<DialogueUIManager>();
        if (ui != null)
        {
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Hide Dialogue UI Preview");

            ui.HideInterfacePreview();
        }

        HideStatOverlay();
        HideChapterOverlay();

        if (registerUndo)
            Undo.RegisterFullObjectHierarchyUndo(setupFlow.gameObject, "Preview Name Input UI");

        setupFlow.PreviewNameInterface(style, storyId, FirstNonEmpty(previewName, "Элисон"));
        RepaintEditorViews();
    }

    static void PreviewCutscene(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        Sprite image,
        VideoClip video,
        TextAsset gif,
        string speakerName,
        string bodyText,
        bool showText,
        bool hideCharacters,
        bool registerUndo)
    {
        ResolveStyle(library, styleCatalog, storyId, true, out StoryUiStyle style, out Sprite sprite);

        HideStatOverlay();
        HideChapterOverlay();
        HideNamePreview(registerUndo);

        bool hasPreviewTarget = false;

        BackgroundViewManager backgroundView = FindSceneObject<BackgroundViewManager>();
        if (backgroundView != null)
        {
            hasPreviewTarget = true;
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(backgroundView.gameObject, "Preview Cutscene Background");

            backgroundView.BeginCutsceneHorizontalFraming();
            if (video != null)
                backgroundView.SetBackgroundVideo(video);
            else if (gif != null)
                backgroundView.SetBackgroundGif(gif);
            else if (image != null)
                backgroundView.SetBackground(image);
        }

        if (hideCharacters)
            HideCharacterPreview(registerUndo);

        DialogueUIManager ui = FindCutsceneDialogueUi();
        if (ui != null)
        {
            hasPreviewTarget = true;
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Preview Cutscene UI");

            if (showText && !string.IsNullOrWhiteSpace(bodyText))
            {
                ui.PreviewDialogueInterface(
                    style,
                    sprite,
                    FirstNonEmpty(speakerName, ""),
                    PlayerAppearance.ReplacePlaceholders(bodyText));
            }
            else
            {
                ui.ApplyStoryUiStyle(style, sprite);
                ui.HideDialoguePanelForCutsceneIntro();
            }
        }

        if (!hasPreviewTarget && registerUndo)
            ShowMissingCutscenePreviewDialog();

        RepaintEditorViews();
    }

    void PreviewPhone(bool registerUndo)
    {
        if (_phonePreviewHideCharacters)
            HideCharacterPreview(registerUndo);

        PhoneDialogueUI phoneUi = FindSceneObject<PhoneDialogueUI>();
        if (phoneUi == null)
        {
            if (registerUndo)
                phoneUi = PhoneDialoguePreviewSetup.CreateOrConfigureInOpenScene();
        }

        if (phoneUi == null)
        {
            if (registerUndo)
                ShowMissingPhonePreviewDialog("Открой сцену с PhoneDialogueUI или нажми «Создать/настроить PhoneDialogueUI».");
            return;
        }

        StoryUserInterface storyUserInterface = PhoneDialoguePreviewSetup.FindSceneStoryUserInterface();
        if (storyUserInterface == null && registerUndo)
            storyUserInterface = PhoneDialoguePreviewSetup.FindOrCreateStoryUserInterface(phoneUi);

        if (registerUndo && phoneUi.gameObject != null)
            Undo.RegisterFullObjectHierarchyUndo(phoneUi.gameObject, "Preview Phone UI");
        if (registerUndo && storyUserInterface != null)
            Undo.RecordObject(storyUserInterface, "Preview Phone UI");

        ApplyStoryPreviewBackground(_library, _storyId, registerUndo);
        if (storyUserInterface != null)
        {
            storyUserInterface.AutoFillPhoneReferences(overwrite: false);
            storyUserInterface.ApplyPhoneConfiguration(nameof(StoryInterfacePreviewWindow));
        }
        else
        {
            phoneUi.AutoFillPhoneReferencesFromHierarchy();
        }

        if (!PhoneDialoguePreviewSetup.IsAssignedToStoryManager(phoneUi))
            PhoneDialoguePreviewSetup.AssignToStoryManager(phoneUi);

        PhoneDialogueNode node = _phonePreviewSourceNode != null
            ? _phonePreviewSourceNode
            : BuildRuntimePhonePreviewNode();
        if (node == null)
            return;
        node.previewStoryId = _storyId;

        bool shown = storyUserInterface != null
            ? storyUserInterface.ShowPhonePreview(node, nameof(StoryInterfacePreviewWindow))
            : Application.isPlaying
                ? new PhoneDialogueRuntimePlayer().Play(phoneUi, node, null)
                : new PhoneDialogueEditorPreviewRenderer().Render(phoneUi, node, nameof(StoryInterfacePreviewWindow));
        if (!shown && registerUndo)
            ShowMissingPhonePreviewDialog("Phone preview не смог отрисоваться. Проверь ссылки PhoneDialogueUI и шаблоны SMS-бабблов.");

        if (storyUserInterface != null)
            EditorUtility.SetDirty(storyUserInterface);
        EditorUtility.SetDirty(phoneUi);
        RepaintEditorViews();
    }

    PhoneDialogueNode BuildRuntimePhonePreviewNode()
    {
        if (_runtimePhonePreviewNode == null)
        {
            _runtimePhonePreviewNode = CreateInstance<PhoneDialogueNode>();
            _runtimePhonePreviewNode.hideFlags = HideFlags.HideAndDontSave;
            _runtimePhonePreviewNode.name = "Phone Preview Runtime Node";
        }

        StoryUserInterface storyUserInterface = PhoneDialoguePreviewSetup.FindSceneStoryUserInterface();
        string settingsContactName = storyUserInterface != null && storyUserInterface.PhonePreviewSettings != null
            ? storyUserInterface.PhonePreviewSettings.quickPreviewContactName
            : "";
        _runtimePhonePreviewNode.contactName = FirstNonEmpty(
            _phonePreviewContactName,
            settingsContactName,
            "\u0420\u043E\u0431");
        _runtimePhonePreviewNode.previewStoryId = _storyId;
        _runtimePhonePreviewNode.contactAvatar = _phonePreviewAvatar;
        _runtimePhonePreviewNode.typingDelay = Mathf.Max(0f, _phonePreviewTypingDelay);
        _runtimePhonePreviewNode.messages = BuildPhonePreviewMessages(
            _phonePreviewScript,
            _runtimePhonePreviewNode.contactName,
            _phonePreviewAttachment,
            _storyId);
        return _runtimePhonePreviewNode;
    }

    static List<PhoneMessage> BuildPhonePreviewMessages(
        string script,
        string contactName,
        Sprite defaultAttachment,
        string storyId = "")
    {
        var messages = new List<PhoneMessage>();
        if (string.IsNullOrWhiteSpace(script))
            return messages;

        string[] lines = script.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        PhoneMessageSide lastSide = PhoneMessageSide.Incoming;
        string lastSenderName = ResolvePhonePreviewSenderName(lastSide, contactName);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = (lines[i] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string speaker;
            string text;
            PhoneMessageSide side = lastSide;
            string senderName = lastSenderName;
            if (TryReadPhonePreviewSpeakerLine(line, out speaker, out text))
            {
                side = IsPhonePreviewOutgoingSpeaker(speaker, contactName, storyId)
                    ? PhoneMessageSide.Outgoing
                    : PhoneMessageSide.Incoming;
                senderName = NormalizePhonePreviewSenderName(speaker, side, contactName);
            }
            else
            {
                text = line;
            }

            bool usePhotoLayout;
            Sprite attachment = ExtractPhonePreviewAttachment(ref text, defaultAttachment, out usePhotoLayout);
            messages.Add(new PhoneMessage
            {
                senderName = senderName,
                side = side,
                text = text,
                timeText = messages.Count == 0 ? "15:25" : "",
                attachment = attachment,
                usePhotoLayout = usePhotoLayout || attachment != null
            });
            lastSide = side;
            lastSenderName = senderName;
        }

        return messages;
    }

    static bool TryReadPhonePreviewSpeakerLine(string line, out string speaker, out string text)
    {
        speaker = "";
        text = line;
        int colon = line.IndexOf(':');
        if (colon <= 0)
            return false;

        speaker = line.Substring(0, colon).Trim();
        text = line.Substring(colon + 1).Trim();
        return !string.IsNullOrWhiteSpace(speaker);
    }

    static bool IsPhonePreviewOutgoingSpeaker(string speaker, string contactName, string storyId = "")
    {
        string value = (speaker ?? "").Trim().Trim('[', ']', '<', '>').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string contact = (contactName ?? "").Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(contact) && value == contact)
            return false;

        if (DialogueVariableResolver.IsPlayerSpeakerName(
                speaker,
                DialogueVariableContext.PhoneDialogue(nameof(StoryInterfacePreviewWindow), storyId: storyId)))
            return true;

        if (value == "name" ||
            value == "hero" ||
            value == "me" ||
            value == "player" ||
            value == "\u0438\u043C\u044F" ||
            value == "\u0433\u0433" ||
            value == "\u044F")
            return true;

        if (value == "contact" ||
            value == "meg" ||
            value == "\u043C\u044D\u0433")
            return false;

        return value == "out" || value == "outgoing";
    }

    static string NormalizePhonePreviewSenderName(string speaker, PhoneMessageSide side, string contactName)
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

        return string.IsNullOrWhiteSpace(value)
            ? ResolvePhonePreviewSenderName(side, contactName)
            : value;
    }

    static string ResolvePhonePreviewSenderName(PhoneMessageSide side, string contactName)
    {
        return side == PhoneMessageSide.Outgoing
            ? "{PlayerName}"
            : FirstNonEmpty(contactName, "Contact");
    }

    static Sprite ExtractPhonePreviewAttachment(ref string text, Sprite defaultAttachment, out bool usePhotoLayout)
    {
        usePhotoLayout = false;
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (text.IndexOf("[photo]", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            usePhotoLayout = true;
            text = text.Replace("[photo]", "").Trim();
            return defaultAttachment;
        }

        if (text.IndexOf("[\u0444\u043E\u0442\u043E]", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            usePhotoLayout = true;
            text = text.Replace("[\u0444\u043E\u0442\u043E]", "").Trim();
            return defaultAttachment;
        }

        if (text.IndexOf("photo", StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf("\u0444\u043E\u0442\u043E", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            usePhotoLayout = true;
            text = RemoveTokenIgnoreCase(RemoveTokenIgnoreCase(text, "photo"), "\u0444\u043E\u0442\u043E").Trim();
            return defaultAttachment;
        }

        return null;
    }

    static string RemoveTokenIgnoreCase(string value, string token)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(token))
            return value ?? "";

        var builder = new StringBuilder(value.Length);
        int start = 0;
        while (start < value.Length)
        {
            int index = value.IndexOf(token, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                builder.Append(value, start, value.Length - start);
                break;
            }

            builder.Append(value, start, index - start);
            start = index + token.Length;
        }

        return builder.ToString();
    }

    static void PreviewOverview(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        bool useCutsceneStyle,
        IReadOnlyList<string> choices,
        string speakerName,
        string bodyText,
        string chapterTitle,
        int statIndex,
        int delta,
        bool registerUndo)
    {
        ResolveStyle(library, styleCatalog, storyId, useCutsceneStyle, out StoryUiStyle style, out Sprite sprite);

        ApplyStoryPreviewBackground(library, storyId, registerUndo);
        bool hasPreviewTarget = false;

        DialogueUIManager ui = FindSceneObject<DialogueUIManager>();
        if (ui != null)
        {
            hasPreviewTarget = true;
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Preview Dialogue And Choice UI");

            ui.PreviewDialogueInterface(
                style,
                sprite,
                FirstNonEmpty(speakerName, "Элисон"),
                FirstNonEmpty(bodyText, "Текст диалога для проверки плашки вместе с кнопками выбора."));
            ui.PreviewChoiceInterface(style, sprite, choices, false);
        }

        HideNamePreview(registerUndo);

        StatChangeOverlay statOverlay = FindSceneObject<StatChangeOverlay>();
        if (statOverlay != null)
        {
            hasPreviewTarget = true;
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(statOverlay.gameObject, "Preview Stat UI");

            StatPreviewEntry entry = ResolveStatPreviewEntry(library, styleCatalog, storyId, useCutsceneStyle, statIndex);
            statOverlay.ApplyStoryUiStyle(style, storyId);
            statOverlay.PreviewStatChange(entry.StatId, entry.DisplayName, delta);
        }

        ChapterTitleOverlay chapterOverlay = FindSceneObject<ChapterTitleOverlay>();
        if (chapterOverlay != null)
        {
            hasPreviewTarget = true;
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(chapterOverlay.gameObject, "Preview Chapter Title UI");

            chapterOverlay.ApplyStoryUiStyle(style);
            chapterOverlay.PreviewChapterTitle(0, FirstNonEmpty(chapterTitle, "ГЛАВА 1: НОВАЯ РОЛЬ"));
        }

        if (!hasPreviewTarget && registerUndo)
            ShowMissingSceneUiDialog();

        RepaintEditorViews();
    }

    static void PreviewStat(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        bool useCutsceneStyle,
        int statIndex,
        int delta,
        bool registerUndo,
        bool hideDialogue)
    {
        StatChangeOverlay overlay = FindSceneObject<StatChangeOverlay>();
        if (overlay == null)
        {
            if (registerUndo)
                ShowMissingSceneUiDialog();
            return;
        }

        ResolveStyle(library, styleCatalog, storyId, useCutsceneStyle, out StoryUiStyle style, out _);
        StatPreviewEntry entry = ResolveStatPreviewEntry(library, styleCatalog, storyId, useCutsceneStyle, statIndex);

        ApplyStoryPreviewBackground(library, storyId, registerUndo);
        DialogueUIManager ui = FindSceneObject<DialogueUIManager>();
        if (hideDialogue && ui != null)
        {
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Hide Dialogue UI Preview");

            ui.HideInterfacePreview();
        }

        HideNamePreview(registerUndo);
        if (hideDialogue)
            HideChapterOverlay();

        if (registerUndo)
            Undo.RegisterFullObjectHierarchyUndo(overlay.gameObject, "Preview Stat UI");

        overlay.ApplyStoryUiStyle(style, storyId);
        overlay.PreviewStatChange(entry.StatId, entry.DisplayName, delta);
        RepaintEditorViews();
    }

    static void PreviewRelationship(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        bool useCutsceneStyle,
        int relationshipIndex,
        string overrideDisplayName,
        int delta,
        bool registerUndo,
        bool hideDialogue)
    {
        StatChangeOverlay overlay = FindSceneObject<StatChangeOverlay>();
        if (overlay == null)
        {
            if (registerUndo)
                ShowMissingSceneUiDialog();
            return;
        }

        ResolveStyle(library, styleCatalog, storyId, useCutsceneStyle, out StoryUiStyle style, out _);
        RelationshipPreviewEntry entry = ResolveRelationshipPreviewEntry(library, styleCatalog, storyId, useCutsceneStyle, relationshipIndex);
        if (!string.IsNullOrWhiteSpace(overrideDisplayName))
            entry = new RelationshipPreviewEntry(entry.StatId, entry.CharacterId, overrideDisplayName, entry.Order);

        ApplyStoryPreviewBackground(library, storyId, registerUndo);
        DialogueUIManager ui = FindSceneObject<DialogueUIManager>();
        if (hideDialogue && ui != null)
        {
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Hide Dialogue UI Preview");

            ui.HideInterfacePreview();
        }

        HideNamePreview(registerUndo);
        if (hideDialogue)
            HideChapterOverlay();

        if (registerUndo)
            Undo.RegisterFullObjectHierarchyUndo(overlay.gameObject, "Preview Relationship UI");

        overlay.ApplyStoryUiStyle(style, storyId);
        overlay.PreviewStatChange(
            BuildRelationshipStatId(entry),
            entry.DisplayName,
            delta,
            BuildRelationshipPreviewMessage(entry.DisplayName, delta));
        RepaintEditorViews();
    }

    static void PreviewChapter(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        bool useCutsceneStyle,
        string titleText,
        bool registerUndo,
        bool hideDialogue,
        bool hideStat)
    {
        ChapterTitleOverlay overlay = FindSceneObject<ChapterTitleOverlay>();
        if (overlay == null)
        {
            if (registerUndo)
                ShowMissingSceneUiDialog();
            return;
        }

        ResolveStyle(library, styleCatalog, storyId, useCutsceneStyle, out StoryUiStyle style, out _);

        ApplyStoryPreviewBackground(library, storyId, registerUndo);
        DialogueUIManager ui = FindSceneObject<DialogueUIManager>();
        if (hideDialogue && ui != null)
        {
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Hide Dialogue UI Preview");

            ui.HideInterfacePreview();
        }

        HideNamePreview(registerUndo);
        if (hideStat)
            HideStatOverlay();

        if (registerUndo)
            Undo.RegisterFullObjectHierarchyUndo(overlay.gameObject, "Preview Chapter Title UI");

        overlay.ApplyStoryUiStyle(style);
        overlay.PreviewChapterTitle(0, FirstNonEmpty(titleText, "ГЛАВА 1: НОВАЯ РОЛЬ"));
        RepaintEditorViews();
    }

    static void HidePreview(bool registerUndo)
    {
        DialogueUIManager ui = FindSceneObject<DialogueUIManager>();
        if (ui != null)
        {
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Hide Interface Preview");

            ui.HideInterfacePreview();
        }

        StatChangeOverlay overlay = FindSceneObject<StatChangeOverlay>();
        if (overlay != null)
        {
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(overlay.gameObject, "Hide Stat Preview");

            overlay.HideInstant();
        }

        ChapterTitleOverlay chapterTitle = FindSceneObject<ChapterTitleOverlay>();
        if (chapterTitle != null)
        {
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(chapterTitle.gameObject, "Hide Chapter Title Preview");

            chapterTitle.HideInstant();
        }

        PreStorySetupFlow setupFlow = FindSceneObject<PreStorySetupFlow>();
        if (setupFlow != null)
        {
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(setupFlow.gameObject, "Hide Name Input Preview");

            setupFlow.HidePreview();
        }

        RepaintEditorViews();
    }

    static void HideCharacterPreview(bool registerUndo)
    {
        CharacterViewManager characterView = FindSceneObject<CharacterViewManager>();
        if (characterView == null)
            return;

        if (registerUndo)
            Undo.RegisterFullObjectHierarchyUndo(characterView.gameObject, "Hide Story Characters Preview");

        characterView.DisableUnused(false, false, false);
        EditorUtility.SetDirty(characterView);
        RepaintEditorViews();
    }

    static void HideCutscenePreview(bool registerUndo)
    {
        BackgroundViewManager backgroundView = FindSceneObject<BackgroundViewManager>();
        if (backgroundView != null)
        {
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(backgroundView.gameObject, "Hide Cutscene Preview");

            backgroundView.EndCutsceneHorizontalFraming();
            EditorUtility.SetDirty(backgroundView);
        }

        DialogueUIManager ui = FindCutsceneDialogueUi();
        if (ui != null)
        {
            if (registerUndo)
                Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Hide Cutscene UI Preview");

            ui.HideDialoguePanelForCutsceneIntro();
        }
    }

    static void HidePhonePreview(bool registerUndo)
    {
        PhoneDialogueUI phoneUi = FindSceneObject<PhoneDialogueUI>();
        if (phoneUi == null)
            return;

        if (registerUndo && phoneUi.gameObject != null)
            Undo.RegisterFullObjectHierarchyUndo(phoneUi.gameObject, "Hide Phone UI Preview");

        phoneUi.Hide();
        RepaintEditorViews();
    }

    static void HideStatOverlay()
    {
        StatChangeOverlay overlay = FindSceneObject<StatChangeOverlay>();
        if (overlay != null)
            overlay.HideInstant();
    }

    static void HideChapterOverlay()
    {
        ChapterTitleOverlay chapterTitle = FindSceneObject<ChapterTitleOverlay>();
        if (chapterTitle != null)
            chapterTitle.HideInstant();
    }

    static void HideNamePreview(bool registerUndo)
    {
        PreStorySetupFlow setupFlow = FindSceneObject<PreStorySetupFlow>();
        if (setupFlow == null)
            return;

        if (registerUndo)
            Undo.RegisterFullObjectHierarchyUndo(setupFlow.gameObject, "Hide Name Input Preview");

        setupFlow.HidePreview();
    }

    static void ResolveStyle(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        bool useCutsceneStyle,
        out StoryUiStyle style,
        out Sprite sprite)
    {
        style = null;
        sprite = null;

        if (styleCatalog != null && !string.IsNullOrWhiteSpace(storyId))
        {
            bool found = useCutsceneStyle
                ? styleCatalog.TryGetCutsceneStoryUiStyle(null, storyId, out style, out sprite)
                : styleCatalog.TryGetStoryUiStyle(null, storyId, out style, out sprite);

            if (found)
                return;
        }

        if (library == null)
            return;

        if (useCutsceneStyle)
            library.TryGetCutsceneStoryUiStyle(out style, out sprite);
        else
            library.TryGetStoryUiStyle(out style, out sprite);
    }

    static void ApplyStoryPreviewBackground(StoryJsonAssetLibrary library, string storyId, bool registerUndo)
    {
        BackgroundViewManager backgroundView = FindSceneObject<BackgroundViewManager>();
        if (backgroundView == null)
            return;

        if (!TryResolveStoryPreviewBackground(library, storyId, out Sprite sprite, out string source))
        {
            ThrottledAppLogger.Warn(
                "StoryPreviewBackgroundMissing:" + (storyId ?? ""),
                AppLogCategory.StoryUi,
                nameof(StoryInterfacePreviewWindow),
                nameof(ApplyStoryPreviewBackground),
                "Фон новеллы для предпросмотра не найден. Проверь SceneSetupNode или StoryJsonAssetLibrary.",
                LogMetadata.Of("storyId", storyId ?? "", "library", library != null ? library.name : ""));
            return;
        }

        if (registerUndo && backgroundView.gameObject != null)
            Undo.RegisterFullObjectHierarchyUndo(backgroundView.gameObject, "Preview Story Background");

        backgroundView.PreviewStaticBackground(sprite);
        EditorUtility.SetDirty(backgroundView);
        AppLogger.DebugLog(
            AppLogCategory.Layout,
            nameof(StoryInterfacePreviewWindow),
            nameof(ApplyStoryPreviewBackground),
            "Фон новеллы применён для предпросмотра.",
            LogMetadata.Of(
                "storyId", storyId ?? "",
                "sprite", sprite != null ? sprite.name : "",
                "source", source ?? ""));
    }

    static bool TryResolveStoryPreviewBackground(
        StoryJsonAssetLibrary library,
        string storyId,
        out Sprite sprite,
        out string source)
    {
        sprite = null;
        source = "";

        ChapterData chapter = FindPreviewChapter(library, storyId);
        if (chapter != null && TryGetFirstSceneBackground(chapter.Graph, out sprite, out source))
            return true;

        if (TryGetFirstSceneBackground(Selection.activeObject as StoryGraph, out sprite, out source))
            return true;

        if (TryGetBackgroundFromLibrary(library, out sprite, out source))
            return true;

        return false;
    }

    static ChapterData FindPreviewChapter(StoryJsonAssetLibrary library, string storyId)
    {
        if (library == null && string.IsNullOrWhiteSpace(storyId))
            return null;

        ChapterData best = null;
        int bestScore = -1;
        string normalizedStoryId = (storyId ?? "").Trim().ToLowerInvariant();

        foreach (string guid in AssetDatabase.FindAssets("t:ChapterData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ChapterData chapter = AssetDatabase.LoadAssetAtPath<ChapterData>(path);
            if (chapter == null)
                continue;

            int score = 0;
            if (library != null && chapter.JsonAssetLibrary == library)
                score += 1000;

            string chapterStoryId = ResolveStoryIdForChapter(chapter);
            if (!string.IsNullOrEmpty(normalizedStoryId) &&
                !string.IsNullOrEmpty(chapterStoryId) &&
                string.Equals(chapterStoryId.Trim(), normalizedStoryId, StringComparison.OrdinalIgnoreCase))
            {
                score += 300;
            }

            if (score <= bestScore)
                continue;

            best = chapter;
            bestScore = score;
        }

        return bestScore > 0 ? best : null;
    }

    static bool TryGetFirstSceneBackground(StoryGraph graph, out Sprite sprite, out string source)
    {
        sprite = null;
        source = "";
        if (graph == null || graph.nodes == null)
            return false;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            SceneSetupNode sceneNode = graph.nodes[i] as SceneSetupNode;
            if (sceneNode == null || sceneNode.sceneData == null || sceneNode.sceneData.background == null)
                continue;

            sprite = sceneNode.sceneData.background;
            source = graph.name + "/" + sceneNode.name;
            return true;
        }

        return false;
    }

    static bool TryGetBackgroundFromLibrary(StoryJsonAssetLibrary library, out Sprite sprite, out string source)
    {
        sprite = null;
        source = "";
        if (library == null || library.Assets == null)
            return false;

        IReadOnlyList<StoryJsonAssetReference> assets = library.Assets;
        Sprite firstSprite = null;
        string firstSpriteSource = "";
        for (int i = 0; i < assets.Count; i++)
        {
            StoryJsonAssetReference asset = assets[i];
            if (asset == null || asset.Sprite == null)
                continue;

            if (firstSprite == null)
            {
                firstSprite = asset.Sprite;
                firstSpriteSource = "library:" + FirstNonEmpty(asset.Id, asset.Sprite.name);
            }

            string marker = (asset.Id + " " + asset.Sprite.name + " " + AssetDatabase.GetAssetPath(asset.Sprite)).ToLowerInvariant();
            if (marker.Contains("background") ||
                marker.Contains("_bg") ||
                marker.Contains("bg_") ||
                marker.Contains("фон") ||
                marker.Contains("scene"))
            {
                sprite = asset.Sprite;
                source = "library:" + FirstNonEmpty(asset.Id, asset.Sprite.name);
                return true;
            }
        }

        if (firstSprite == null)
            return false;

        sprite = firstSprite;
        source = firstSpriteSource;
        ThrottledAppLogger.Warn(
            "StoryPreviewBackgroundFallback:" + library.name,
            AppLogCategory.StoryUi,
            nameof(StoryInterfacePreviewWindow),
            nameof(TryGetBackgroundFromLibrary),
            "SceneSetupNode не найден, используется первый доступный Sprite из StoryJsonAssetLibrary как fallback-фон предпросмотра.",
            LogMetadata.Of("library", library.name, "sprite", firstSprite.name));
        return true;
    }

    static string BuildLiveSignature(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        bool useCutsceneStyle,
        PreviewMode previewMode,
        int statPreviewIndex,
        int statDelta)
    {
        var builder = new StringBuilder(2048);
        builder.Append((int)previewMode)
            .Append('|')
            .Append(useCutsceneStyle ? 1 : 0)
            .Append('|')
            .Append(statPreviewIndex)
            .Append('|')
            .Append(statDelta)
            .Append('|')
            .Append(storyId ?? "")
            .Append('|');

        ResolveStyle(library, styleCatalog, storyId, useCutsceneStyle, out StoryUiStyle style, out Sprite sprite);
        TryResolveStoryPreviewBackground(library, storyId, out Sprite storyBackground, out _);
        AppendObjectSignature(builder, library);
        AppendObjectSignature(builder, styleCatalog);
        AppendObjectSignature(builder, style);
        AppendObjectSignature(builder, sprite);
        AppendObjectSignature(builder, storyBackground);
        AppendStatDefinitionSignatures(builder, style);

        if (previewMode == PreviewMode.Stat || previewMode == PreviewMode.Relationship)
            AppendObjectSignature(builder, FindSceneObject<StatChangeOverlay>());

        return builder.ToString();
    }

    static void AppendStatDefinitionSignatures(StringBuilder builder, StoryUiStyle style)
    {
        if (style == null || style.StatDefinitionAssets == null)
            return;

        IReadOnlyList<StatDefinition> statDefinitions = style.StatDefinitionAssets;
        for (int i = 0; i < statDefinitions.Count; i++)
            AppendObjectSignature(builder, statDefinitions[i]);
    }

    static void AppendObjectSignature(StringBuilder builder, UnityEngine.Object target)
    {
        if (target == null)
        {
            builder.Append("<null>|");
            return;
        }

        builder.Append(target.GetInstanceID())
            .Append(':')
            .Append(AssetDatabase.GetAssetPath(target))
            .Append(':');

        try
        {
            builder.Append(EditorJsonUtility.ToJson(target));
        }
        catch (Exception)
        {
            builder.Append(target.name);
        }

        builder.Append('|');
    }

    static StoryJsonAssetLibrary ResolveSelectedLibrary()
    {
        UnityEngine.Object selected = Selection.activeObject;

        StoryJsonAssetLibrary library = selected as StoryJsonAssetLibrary;
        if (library != null)
            return library;

        ChapterData chapter = selected as ChapterData;
        if (chapter != null)
            return chapter.JsonAssetLibrary;

        StoryData story = selected as StoryData;
        if (story != null)
            return ResolveLibraryForStory(story);

        return null;
    }

    static StoryJsonAssetLibrary ResolveLibraryForStory(StoryData story)
    {
        if (story == null || story.Chapters == null)
            return null;

        foreach (ChapterData chapter in story.Chapters)
        {
            if (chapter != null && chapter.JsonAssetLibrary != null)
                return chapter.JsonAssetLibrary;
        }

        return null;
    }

    static string ResolveSelectedStoryId()
    {
        UnityEngine.Object selected = Selection.activeObject;

        StoryData story = selected as StoryData;
        if (story != null)
            return story.storyId;

        ChapterData chapter = selected as ChapterData;
        if (chapter != null)
            return ResolveStoryIdForChapter(chapter);

        StoryJsonAssetLibrary library = selected as StoryJsonAssetLibrary;
        if (library != null)
            return ResolveStoryIdForLibrary(library);

        return "";
    }

    static StoryInterfaceStyleCatalog ResolveDefaultStyleCatalog()
    {
        string[] guids = AssetDatabase.FindAssets("t:StoryInterfaceStyleCatalog");
        if (guids == null || guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<StoryInterfaceStyleCatalog>(path);
    }

    static string ResolveStoryIdForLibrary(StoryJsonAssetLibrary library)
    {
        if (library == null)
            return "";

        foreach (string guid in AssetDatabase.FindAssets("t:StoryData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StoryData story = AssetDatabase.LoadAssetAtPath<StoryData>(path);
            if (story == null || story.Chapters == null)
                continue;

            foreach (ChapterData chapter in story.Chapters)
            {
                if (chapter != null && chapter.JsonAssetLibrary == library)
                    return story.storyId;
            }
        }

        return InferStoryIdFromAssetPath(AssetDatabase.GetAssetPath(library));
    }

    static string ResolveStoryIdForChapter(ChapterData chapter)
    {
        if (chapter == null)
            return "";

        foreach (string guid in AssetDatabase.FindAssets("t:StoryData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StoryData story = AssetDatabase.LoadAssetAtPath<StoryData>(path);
            if (story == null || story.Chapters == null)
                continue;

            foreach (ChapterData candidate in story.Chapters)
            {
                if (candidate == chapter)
                    return story.storyId;
            }
        }

        return InferStoryIdFromAssetPath(AssetDatabase.GetAssetPath(chapter));
    }

    static string InferStoryIdFromAssetPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return "";

        string normalized = assetPath.Replace('\\', '/');
        const string storiesRoot = "Assets/_MyProject/Data/Stories/";
        int start = normalized.IndexOf(storiesRoot, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return "";

        string rest = normalized.Substring(start + storiesRoot.Length);
        int slash = rest.IndexOf('/');
        return slash > 0 ? rest.Substring(0, slash) : "";
    }

    static StatPreviewEntry ResolveStatPreviewEntry(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        bool useCutsceneStyle,
        int statIndex)
    {
        List<StatPreviewEntry> entries = BuildStatPreviewEntries(library, styleCatalog, storyId, useCutsceneStyle);
        if (entries.Count == 0)
            return new StatPreviewEntry("interface_preview", "Стат", 0);

        return entries[Mathf.Clamp(statIndex, 0, entries.Count - 1)];
    }

    static List<StatPreviewEntry> BuildStatPreviewEntries(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        bool useCutsceneStyle)
    {
        var result = new List<StatPreviewEntry>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ResolveStyle(library, styleCatalog, storyId, useCutsceneStyle, out StoryUiStyle style, out _);
        if (style == null)
            return result;

        IReadOnlyList<StatDefinition> statDefinitions = style.StatDefinitionAssets;
        if (statDefinitions != null)
        {
            for (int i = 0; i < statDefinitions.Count; i++)
            {
                StatDefinition definition = statDefinitions[i];
                if (definition == null)
                    continue;

                AddStatPreviewEntry(
                    result,
                    seenIds,
                    definition.statId,
                    definition.displayName,
                    definition.order);
            }
        }

        IReadOnlyList<StatChangeOverlayDefinition> overlayDefinitions = style.StatOverlayDefinitions;
        if (overlayDefinitions != null)
        {
            for (int i = 0; i < overlayDefinitions.Count; i++)
            {
                StatChangeOverlayDefinition definition = overlayDefinitions[i];
                if (definition == null)
                    continue;

                AddStatPreviewEntry(
                    result,
                    seenIds,
                    definition.statId,
                    definition.displayName,
                    1000 + i);
            }
        }

        result.Sort((left, right) => left.Order.CompareTo(right.Order));
        return result;
    }

    static RelationshipPreviewEntry ResolveRelationshipPreviewEntry(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        bool useCutsceneStyle,
        int relationshipIndex)
    {
        List<RelationshipPreviewEntry> entries = BuildRelationshipPreviewEntries(library, styleCatalog, storyId, useCutsceneStyle);
        if (entries.Count == 0)
            return new RelationshipPreviewEntry("relationship:interface_preview", "interface_preview", "Персонаж", 0);

        return entries[Mathf.Clamp(relationshipIndex, 0, entries.Count - 1)];
    }

    static List<RelationshipPreviewEntry> BuildRelationshipPreviewEntries(
        StoryJsonAssetLibrary library,
        StoryInterfaceStyleCatalog styleCatalog,
        string storyId,
        bool useCutsceneStyle)
    {
        var result = new List<RelationshipPreviewEntry>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<StatPreviewEntry> statEntries = BuildStatPreviewEntries(library, styleCatalog, storyId, useCutsceneStyle);

        for (int i = 0; i < statEntries.Count; i++)
        {
            StatPreviewEntry stat = statEntries[i];
            if (!IsRelationshipStatId(stat.StatId))
                continue;

            AddRelationshipPreviewEntry(
                result,
                seenIds,
                stat.StatId,
                ExtractRelationshipCharacterId(stat.StatId),
                stat.DisplayName,
                stat.Order);
        }

        if (library != null && library.Assets != null)
        {
            IReadOnlyList<StoryJsonAssetReference> assets = library.Assets;
            for (int i = 0; i < assets.Count; i++)
            {
                StoryJsonAssetReference asset = assets[i];
                CharacterData character = asset != null ? asset.Character : null;
                if (character == null)
                    continue;

                AddRelationshipPreviewEntry(
                    result,
                    seenIds,
                    "relationship:" + ToStatIdPart(asset.Id),
                    asset.Id,
                    FirstNonEmpty(character.characterName, asset.Id, character.name),
                    500 + i);
            }
        }

        if (result.Count == 0)
        {
            AddRelationshipPreviewEntry(result, seenIds, "relationship:vlad", "vlad", "Влад", 0);
            AddRelationshipPreviewEntry(result, seenIds, "relationship:alice", "alice", "Алиса", 1);
            AddRelationshipPreviewEntry(result, seenIds, "relationship:elison", "elison", "Элисон", 2);
        }

        result.Sort((left, right) => left.Order.CompareTo(right.Order));
        return result;
    }

    static void AddRelationshipPreviewEntry(
        List<RelationshipPreviewEntry> entries,
        HashSet<string> seenIds,
        string statId,
        string characterId,
        string displayName,
        int order)
    {
        characterId = FirstNonEmpty(characterId, ExtractRelationshipCharacterId(statId), displayName, "character");
        string resolvedStatId = IsRelationshipStatId(statId) ? statId : "relationship:" + ToStatIdPart(characterId);
        string key = FirstNonEmpty(resolvedStatId, characterId, displayName);
        if (!seenIds.Add(key ?? ""))
            return;

        entries.Add(new RelationshipPreviewEntry(
            resolvedStatId,
            characterId,
            FirstNonEmpty(displayName, HumanizeIdentifier(characterId), "Персонаж"),
            order));
    }

    static bool IsRelationshipStatId(string statId)
    {
        if (string.IsNullOrWhiteSpace(statId))
            return false;

        string lower = statId.Trim().ToLowerInvariant();
        return lower.StartsWith("relationship:", StringComparison.Ordinal) ||
               lower.StartsWith("relationship_", StringComparison.Ordinal) ||
               lower.StartsWith("relationship-", StringComparison.Ordinal) ||
               lower.StartsWith("relationship.", StringComparison.Ordinal) ||
               lower.StartsWith("rel:", StringComparison.Ordinal) ||
               lower.StartsWith("rel_", StringComparison.Ordinal) ||
               lower.StartsWith("rel-", StringComparison.Ordinal) ||
               lower.StartsWith("rel.", StringComparison.Ordinal);
    }

    static string ExtractRelationshipCharacterId(string statId)
    {
        if (string.IsNullOrWhiteSpace(statId))
            return "";

        string trimmed = statId.Trim();
        string lower = trimmed.ToLowerInvariant();
        string[] prefixes =
        {
            "relationship:",
            "relationship_",
            "relationship-",
            "relationship.",
            "rel:",
            "rel_",
            "rel-",
            "rel."
        };

        for (int i = 0; i < prefixes.Length; i++)
        {
            if (lower.StartsWith(prefixes[i], StringComparison.Ordinal))
                return trimmed.Substring(prefixes[i].Length).Trim();
        }

        return "";
    }

    static string BuildRelationshipStatId(RelationshipPreviewEntry entry)
    {
        if (IsRelationshipStatId(entry.StatId))
            return entry.StatId;

        return "relationship:" + ToStatIdPart(FirstNonEmpty(entry.CharacterId, entry.DisplayName, "character"));
    }

    static string BuildRelationshipPreviewMessage(string displayName, int delta)
    {
        string target = NormalizeRelationshipTargetForMessage(displayName);
        return delta >= 0
            ? "У вас улучшились отношения " + target
            : "У вас ухудшились отношения " + target;
    }

    static string NormalizeRelationshipTargetForMessage(string displayName)
    {
        string target = FirstNonEmpty(displayName, "персонажем").Trim();
        if (target.StartsWith("с ", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith("со ", StringComparison.OrdinalIgnoreCase))
            return target;

        return "с " + target;
    }

    static string ToStatIdPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "character";

        return value.Trim().Replace(' ', '_').Replace('\t', '_');
    }

    static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string text = value.Trim().Replace('_', ' ').Replace('-', ' ').Replace('.', ' ');
        if (text.Length <= 1)
            return text.ToUpperInvariant();

        return char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    static void AddStatPreviewEntry(
        List<StatPreviewEntry> entries,
        HashSet<string> seenIds,
        string statId,
        string displayName,
        int order)
    {
        if (string.IsNullOrWhiteSpace(statId) && string.IsNullOrWhiteSpace(displayName))
            return;

        string key = string.IsNullOrWhiteSpace(statId) ? displayName : statId;
        if (!seenIds.Add(key ?? ""))
            return;

        entries.Add(new StatPreviewEntry(statId, displayName, order));
    }

    static T FindSceneObject<T>() where T : UnityEngine.Object
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < objects.Length; i++)
        {
            T item = objects[i];
            if (item == null || EditorUtility.IsPersistent(item))
                continue;

            GameObject gameObject = null;
            Component component = item as Component;
            if (component != null)
                gameObject = component.gameObject;
            else
                gameObject = item as GameObject;

            if (gameObject == null || !gameObject.scene.IsValid())
                continue;

            return item;
        }

        return null;
    }

    static DialogueUIManager FindCutsceneDialogueUi()
    {
        StoryManager storyManager = FindSceneObject<StoryManager>();
        DialogueUIManager cutsceneUi = TryGetStoryManagerDialogueUi(
            storyManager,
            "CutsceneUserInterface",
            "cutsceneUserInterface",
            "_cutsceneUserInterface",
            "defaultCutsceneUserInterface");
        if (cutsceneUi != null)
            return cutsceneUi;

        return FindSceneObject<DialogueUIManager>();
    }

    static DialogueUIManager TryGetStoryManagerDialogueUi(StoryManager storyManager, params string[] memberNames)
    {
        if (storyManager == null || memberNames == null)
            return null;

        Type type = storyManager.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (int i = 0; i < memberNames.Length; i++)
        {
            string memberName = memberNames[i];
            if (string.IsNullOrWhiteSpace(memberName))
                continue;

            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null && typeof(DialogueUIManager).IsAssignableFrom(property.PropertyType))
            {
                DialogueUIManager value = property.GetValue(storyManager, null) as DialogueUIManager;
                if (value != null)
                    return value;
            }

            FieldInfo field = type.GetField(memberName, flags);
            if (field != null && typeof(DialogueUIManager).IsAssignableFrom(field.FieldType))
            {
                DialogueUIManager value = field.GetValue(storyManager) as DialogueUIManager;
                if (value != null)
                    return value;
            }
        }

        return null;
    }

    static void ShowMissingSceneUiDialog()
    {
        EditorUtility.DisplayDialog(
            "Interface Preview",
            "Открой сцену с DialogueUIManager и StatChangeOverlay, чтобы смотреть предпросмотр интерфейса.",
            "OK");
    }

    static void ShowMissingCharacterViewDialog()
    {
        EditorUtility.DisplayDialog(
            "Interface Preview",
            "Open a scene with CharacterViewManager to preview story characters.",
            "OK");
    }

    static void ShowMissingCutscenePreviewDialog()
    {
        EditorUtility.DisplayDialog(
            "Interface Preview",
            "Open a scene with BackgroundViewManager or DialogueUIManager to preview cutscenes.",
            "OK");
    }

    static void ShowMissingPhonePreviewDialog(string message)
    {
        EditorUtility.DisplayDialog(
            "Предпросмотр интерфейса",
            string.IsNullOrWhiteSpace(message) ? "Предпросмотр телефона недоступен." : message,
            "OK");
    }

    static void RepaintEditorViews()
    {
        Canvas.ForceUpdateCanvases();
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return "";
    }

    struct RelationshipPreviewEntry
    {
        public readonly string StatId;
        public readonly string CharacterId;
        public readonly string DisplayName;
        public readonly int Order;

        public string Label
        {
            get { return FirstNonEmpty(DisplayName, CharacterId, StatId, "Персонаж"); }
        }

        public RelationshipPreviewEntry(string statId, string characterId, string displayName, int order)
        {
            StatId = statId ?? "";
            CharacterId = characterId ?? "";
            DisplayName = FirstNonEmpty(displayName, characterId, statId, "Персонаж");
            Order = order;
        }
    }

    struct StatPreviewEntry
    {
        public readonly string StatId;
        public readonly string DisplayName;
        public readonly int Order;

        public string Label
        {
            get { return FirstNonEmpty(DisplayName, StatId, "Стат"); }
        }

        public StatPreviewEntry(string statId, string displayName, int order)
        {
            StatId = statId ?? "";
            DisplayName = FirstNonEmpty(displayName, statId, "Стат");
            Order = order;
        }
    }

    struct CharacterPreviewEntry
    {
        public readonly CharacterData Character;
        public readonly string Label;
        public readonly int Order;

        public CharacterPreviewEntry(CharacterData character, string label, int order)
        {
            Character = character;
            Label = FirstNonEmpty(label, character != null ? character.name : "", "Character");
            Order = order;
        }
    }

    struct ClothingPreviewEntry
    {
        public readonly ClothingItem Item;
        public readonly string Label;
        public readonly int Order;

        public ClothingPreviewEntry(ClothingItem item, string label, int order)
        {
            Item = item;
            Label = FirstNonEmpty(label, item != null ? item.name : "", "Item");
            Order = order;
        }
    }
}

[InitializeOnLoad]
public static class StoryInterfacePreviewToolbarButton
{
    const string ButtonRootName = "StoryInterfacePreviewToolbarButtonRoot";
    const string ButtonText = "UI Preview";

    static readonly Type ToolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
    static ScriptableObject _toolbar;

    static StoryInterfacePreviewToolbarButton()
    {
        EditorApplication.update -= TryAttachButton;
        EditorApplication.update += TryAttachButton;
    }

    static void TryAttachButton()
    {
        if (ToolbarType == null)
            return;

        if (_toolbar == null)
        {
            UnityEngine.Object[] toolbars = Resources.FindObjectsOfTypeAll(ToolbarType);
            if (toolbars == null || toolbars.Length == 0)
                return;

            _toolbar = toolbars[0] as ScriptableObject;
        }

        VisualElement root = GetToolbarRoot();
        VisualElement zone = root != null ? root.Q("ToolbarZoneRightAlign") : null;
        if (zone == null || zone.Q(ButtonRootName) != null)
            return;

        var buttonRoot = new VisualElement
        {
            name = ButtonRootName
        };
        buttonRoot.style.flexDirection = FlexDirection.Row;
        buttonRoot.style.alignItems = Align.Center;
        buttonRoot.style.marginLeft = 4f;
        buttonRoot.style.marginRight = 4f;

        var button = new Button(StoryInterfacePreviewWindow.Open)
        {
            text = ButtonText
        };
        button.tooltip = "Открыть предпросмотр интерфейса истории";
        button.style.height = 20f;
        button.style.minWidth = 72f;
        buttonRoot.Add(button);
        zone.Add(buttonRoot);
    }

    static VisualElement GetToolbarRoot()
    {
        if (_toolbar == null)
            return null;

        var rootField = ToolbarType.GetField("m_Root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return rootField != null ? rootField.GetValue(_toolbar) as VisualElement : null;
    }
}
#endif
