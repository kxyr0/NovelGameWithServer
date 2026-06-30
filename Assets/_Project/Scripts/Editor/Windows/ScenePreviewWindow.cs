#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Окно превью сцены — показывает как сцена выглядит в игре,
/// не запуская Play Mode.
///
/// Открытие:
///   VN → Scene Preview
///   Или кнопка "👁 Открыть превью сцены" в DialogueNode
///
/// Возможности:
///   - Фон сцены (берётся из ближайшего SceneSetupNode в графе)
///   - Персонажи с эмоциями в позициях Left / Center / Right
///   - Диалоговый бокс с именем спикера и текстом реплики
///   - Навигация по репликам (← →)
///   - Выбор ноды вручную через ObjectField
/// </summary>
public class ScenePreviewWindow : EditorWindow
{
    // ── Данные ──────────────────────────────────────────────
    DialogueNode _dialogueNode;
    SceneSetupData _sceneOverride; // если хочется задать фон вручную
    int _lineIndex = 0;

    // ── Настройки отображения ────────────────────────────────
    bool _showBackground = true;
    bool _showCharacters = true;
    bool _showDialogueBox = true;

    // ── Размер телефона (соотношение 9:16) ───────────────────
    const float PhoneAspect = 9f / 16f;

    // ── Цвета UI ─────────────────────────────────────────────
    static readonly Color BoxBg         = new Color(0.08f, 0.08f, 0.12f, 0.88f);
    static readonly Color NameBg        = new Color(0.15f, 0.25f, 0.55f, 0.95f);
    static readonly Color TextColor     = Color.white;
    static readonly Color NameColor     = new Color(0.7f, 0.88f, 1f);
    static readonly Color SubtleColor   = new Color(0.6f, 0.6f, 0.6f);

    // ─────────────────────────────────────────────────────────

    public static void Open()
    {
        var w = GetWindow<ScenePreviewWindow>("Scene Preview");
        w.minSize = new Vector2(380, 600);
    }

    /// <summary>
    /// Открыть окно и сразу загрузить конкретную DialogueNode.
    /// </summary>
    public static void OpenWithNode(DialogueNode node)
    {
        var w = GetWindow<ScenePreviewWindow>("Scene Preview");
        w.minSize = new Vector2(380, 600);
        w._dialogueNode = node;
        w._lineIndex = 0;
        w.Repaint();
    }

    void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space(4);

        // ── Выбор ноды и фона ────────────────────────────────
        EditorGUI.BeginChangeCheck();
        _dialogueNode = (DialogueNode)EditorGUILayout.ObjectField(
            "DialogueNode", _dialogueNode, typeof(DialogueNode), false);
        if (EditorGUI.EndChangeCheck()) _lineIndex = 0;

        _sceneOverride = (SceneSetupData)EditorGUILayout.ObjectField(
            "Фон (override)", _sceneOverride, typeof(SceneSetupData), false);

        EditorGUILayout.Space(6);

        // ── Превью-экран ─────────────────────────────────────
        float availW = position.width - 16;
        float availH = position.height - 180;

        // Вписываем в телефонный аспект
        float previewW, previewH;
        if (availW * (1f / PhoneAspect) <= availH)
        {
            previewW = availW;
            previewH = availW / PhoneAspect;
        }
        else
        {
            previewH = availH;
            previewW = availH * PhoneAspect;
        }

        Rect previewRect = GUILayoutUtility.GetRect(previewW, previewH);
        previewRect.x = (position.width - previewW) * 0.5f;
        previewRect.width = previewW;
        previewRect.height = previewH;

        DrawPreview(previewRect);

        GUILayout.Space(previewH > availH ? 0 : availH - previewH);

        // ── Навигация по репликам ────────────────────────────
        DrawNavigation();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        _showBackground  = GUILayout.Toggle(_showBackground,  "🖼 Фон",  EditorStyles.toolbarButton);
        _showCharacters  = GUILayout.Toggle(_showCharacters,  "👤 Персы", EditorStyles.toolbarButton);
        _showDialogueBox = GUILayout.Toggle(_showDialogueBox, "💬 Диалог", EditorStyles.toolbarButton);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↺", EditorStyles.toolbarButton, GUILayout.Width(28)))
        {
            _lineIndex = 0;
            Repaint();
        }
        EditorGUILayout.EndHorizontal();
    }

    void DrawNavigation()
    {
        if (_dialogueNode == null || _dialogueNode.lines == null || _dialogueNode.lines.Count == 0)
            return;

        int total = _dialogueNode.lines.Count;

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = _lineIndex > 0;
        if (GUILayout.Button("← Пред", GUILayout.Height(30))) { _lineIndex--; Repaint(); }
        GUI.enabled = true;

        GUILayout.Label($"{_lineIndex + 1} / {total}", new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter
        }, GUILayout.ExpandWidth(true));

        GUI.enabled = _lineIndex < total - 1;
        if (GUILayout.Button("След →", GUILayout.Height(30))) { _lineIndex++; Repaint(); }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    void DrawPreview(Rect rect)
    {
        // Рамка
        EditorGUI.DrawRect(rect, new Color(0.05f, 0.05f, 0.08f));

        if (_dialogueNode == null) 
        {
            DrawCenteredLabel(rect, "Выбери DialogueNode");
            return;
        }

        // ── Фон ──────────────────────────────────────────────
        if (_showBackground)
        {
            SceneSetupData scene = _sceneOverride ?? FindSceneData();
            if (scene != null && scene.background != null)
                DrawSpriteFill(rect, scene.background);
            else
                EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.2f));
        }

        // ── Персонажи ─────────────────────────────────────────
        if (_showCharacters && _dialogueNode.activeCharacters != null)
        {
            // Для текущей реплики берём эмоцию спикера
            CharacterData currentSpeaker = null;
            CharacterEmotionType currentEmotion = CharacterEmotionType.Idle;

            if (_dialogueNode.lines != null && _lineIndex < _dialogueNode.lines.Count)
            {
                var line = _dialogueNode.lines[_lineIndex];
                currentSpeaker = line?.speaker;
                if (line != null) currentEmotion = line.emotion;
            }

            foreach (var entry in _dialogueNode.activeCharacters)
            {
                if (entry == null || entry.character == null) continue;

                // Определяем эмоцию: если это спикер текущей реплики — берём её
                CharacterEmotionType emo = (entry.character == currentSpeaker)
                    ? currentEmotion
                    : entry.emotion;

                Sprite sprite = entry.character.useLayeredEmotions
                    ? entry.character.GetBodySprite()
                    : entry.character.GetEmotion(emo);

                if (sprite == null) continue;

                Rect charRect = GetCharacterRect(rect, entry.position);

                // Затемняем не-спикеров
                bool isSpeaker = entry.character == currentSpeaker || currentSpeaker == null;
                if (!isSpeaker)
                {
                    DrawSpriteWithTint(charRect, sprite, new Color(0.5f, 0.5f, 0.55f, 0.85f));
                }
                else
                {
                    DrawSpriteContain(charRect, sprite);
                }

                // Лицо поверх тела (слоевой режим)
                if (entry.character.useLayeredEmotions)
                {
                    Sprite face = entry.character.GetFaceSprite(emo);
                    if (face != null)
                        DrawSpriteContain(charRect, face);
                }
            }
        }

        // ── Диалоговый бокс ──────────────────────────────────
        if (_showDialogueBox && _dialogueNode.lines != null && _dialogueNode.lines.Count > 0)
        {
            if (_lineIndex < _dialogueNode.lines.Count)
            {
                var line = _dialogueNode.lines[_lineIndex];
                DrawDialogueBox(rect, line);
            }
        }

        // ── Подсказка если нет реплик ─────────────────────────
        if (_dialogueNode.lines == null || _dialogueNode.lines.Count == 0)
            DrawCenteredLabel(rect, "Реплики не добавлены");
    }

    // ── Вспомогательные методы ───────────────────────────────

    SceneSetupData FindSceneData()
    {
        // Пробуем найти SceneSetupNode в том же графе
        if (_dialogueNode.graph == null) return null;

        foreach (var node in _dialogueNode.graph.nodes)
        {
            if (node is SceneSetupNode setup && setup.sceneData != null)
                return setup.sceneData;
        }
        return null;
    }

    Rect GetCharacterRect(Rect screen, CharacterPosition pos)
    {
        float charW = screen.width * 0.45f;
        float charH = screen.height * 0.72f;
        float yBottom = screen.yMax - screen.height * 0.28f;

        float xCenter = pos switch
        {
            CharacterPosition.Left   => screen.x + screen.width * 0.18f,
            CharacterPosition.Center => screen.x + screen.width * 0.5f,
            CharacterPosition.Right  => screen.x + screen.width * 0.82f,
            _                        => screen.x + screen.width * 0.5f
        };

        return new Rect(xCenter - charW * 0.5f, yBottom - charH, charW, charH);
    }

    void DrawDialogueBox(Rect screen, DialogueLine line)
    {
        float boxH = screen.height * 0.28f;
        Rect boxRect = new Rect(screen.x, screen.yMax - boxH, screen.width, boxH);

        // Фон бокса
        EditorGUI.DrawRect(boxRect, BoxBg);

        // Имя
        if (line.speaker != null && !string.IsNullOrEmpty(line.speaker.characterName))
        {
            float nameH = 22f;
            Rect nameRect = new Rect(boxRect.x + 10, boxRect.y - nameH + 2, 180, nameH);
            EditorGUI.DrawRect(nameRect, NameBg);

            var nameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = NameColor },
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 0, 0)
            };
            GUI.Label(nameRect, line.speaker.characterName, nameStyle);
        }

        // Текст реплики
        var textStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            normal = { textColor = TextColor },
            fontSize = Mathf.Clamp((int)(screen.width * 0.034f), 10, 16),
            wordWrap = true,
            padding = new RectOffset(12, 12, 10, 10)
        };

        string displayText = PlayerAppearance_GetName(line.richText ?? "");
        GUI.Label(new Rect(boxRect.x, boxRect.y + 4, boxRect.width, boxRect.height - 4),
            displayText, textStyle);
    }

    // Подстановка {playerName} в превью
    static string PlayerAppearance_GetName(string text)
    {
        return DialogueVariableResolver.ResolveText(
            text,
            DialogueVariableContext.StoryUi(nameof(ScenePreviewWindow)));
    }

    static void DrawSpriteFill(Rect rect, Sprite sprite)
    {
        if (sprite == null) return;
        Texture2D tex = sprite.texture;
        Rect tr = sprite.textureRect;
        var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        GUI.DrawTextureWithTexCoords(rect, tex, uv, false);
    }

    static void DrawSpriteContain(Rect rect, Sprite sprite)
    {
        if (sprite == null) return;
        Texture2D tex = sprite.texture;
        Rect tr = sprite.textureRect;

        float spriteAspect = tr.width / tr.height;
        float rectAspect = rect.width / rect.height;

        Rect drawRect;
        if (spriteAspect > rectAspect)
        {
            float h = rect.width / spriteAspect;
            drawRect = new Rect(rect.x, rect.y + (rect.height - h) * 0.5f, rect.width, h);
        }
        else
        {
            float w = rect.height * spriteAspect;
            drawRect = new Rect(rect.x + (rect.width - w) * 0.5f, rect.y, w, rect.height);
        }

        var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        GUI.DrawTextureWithTexCoords(drawRect, tex, uv, true);
    }

    static void DrawSpriteWithTint(Rect rect, Sprite sprite, Color tint)
    {
        Color prev = GUI.color;
        GUI.color = tint;
        DrawSpriteContain(rect, sprite);
        GUI.color = prev;
    }

    static void DrawCenteredLabel(Rect rect, string text)
    {
        var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter
        };
        GUI.Label(rect, text, style);
    }
}
#endif
