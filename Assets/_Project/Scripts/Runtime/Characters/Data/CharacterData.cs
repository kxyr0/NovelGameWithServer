using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject персонажа.
///
/// Режим 1 — классический (useLayeredEmotions = false):
///   Один спрайт на эмоцию. Список emotions заполняется как обычно.
///
/// Режим 2 — слоевой (useLayeredEmotions = true):
///   - bodySprite    — базовое тело персонажа (статичное)
///   - emotionLayers — список спрайтов лица/мимики по эмоциям
///   CharacterViewManager накладывает лицо поверх тела и делает crossfade
///   только по слою лица при смене эмоции.
///
/// Для персонажей, чья внешность зависит от ГГ (брат, сестра и т.д.):
///   1. Включи inheritAppearanceFromPlayer = true.
///   2. Заполни appearanceVariants для каждого AppearanceType.
/// </summary>
[CreateAssetMenu(menuName = "VN/Character")]
public class CharacterData : ScriptableObject
{
    public static event Action<CharacterData> Changed;

    public string characterName;
    public Sprite defaultSprite;

    public Sprite hairSprite;
    public List<CharacterEmotion> emotions = new List<CharacterEmotion>();

    [Header("Слоевые эмоции")]
    [Tooltip("Если включено, персонаж собирается из слоёв: базовое тело плюс отдельные лица. Художнику достаточно подготовить эмоции лица, а не полные спрайты.")]
    public bool useLayeredEmotions = false;

    [Tooltip("Основной спрайт тела для слоевого режима.")]
    public Sprite bodySprite;

    [Tooltip("Список спрайтов лица для эмоций в слоевом режиме.")]
    public List<CharacterEmotionLayer> emotionLayers = new List<CharacterEmotionLayer>();

    [Header("Наследование внешности ГГ")]
    [Tooltip("Если включено, персонаж берёт внешность из выбора главной героини. Подходит для родственников и похожих персонажей.")]
    public bool inheritAppearanceFromPlayer = false;

    [Tooltip("Набор базовых спрайтов и эмоций для каждого типа внешности.")]
    public List<AppearanceVariant> appearanceVariants = new List<AppearanceVariant>();

    [Header("Story Layer Layout")]
    [Tooltip("Настройка слоёв персонажа в сценах истории. Эти смещения применяются только в диалогах и обычных сценах истории.")]
    public CharacterStoryLayerLayout storyLayerLayout = new CharacterStoryLayerLayout();

    [Header("Story Position Layout")]
    [Tooltip("Позиция и размер всего персонажа в сценах истории. Используй это поле для конкретного персонажа, а не общие слоты Left, Center и Right.")]
    public CharacterStoryPositionLayout storyPositionLayout = new CharacterStoryPositionLayout();

    [Header("Story Camera")]
    [Tooltip("Не панорамировать камеру к этому персонажу автоматически. Включи, если его авторская позиция уже должна оставаться видимой.")]
    public bool keepStorySlotPositionOnSpeakerFocus;

    [Header("Wardrobe Layer Layout")]
    [Tooltip("Настройка превью персонажа в гардеробе. Используй для выравнивания тела, одежды и волос на экране гардероба.")]
    public CharacterWardrobeLayerLayout wardrobeLayerLayout = new CharacterWardrobeLayerLayout();

    [Header("Permanent Story Equipment")]
    [Tooltip("Одежда, которая всегда рисуется на этом персонаже в сценах истории. Подходит для неигровых персонажей, чья одежда не зависит от выбора игрока.")]
    public ClothingItem permanentOutfit;

    [Tooltip("Причёска, которая всегда рисуется на этом персонаже в сценах истории. Подходит для неигровых персонажей, чьи волосы не зависят от выбора игрока.")]
    public ClothingItem permanentHair;

    [Tooltip("Аксессуар, который всегда рисуется на этом персонаже в сценах истории.")]
    public ClothingItem permanentAccessory;

    [Tooltip("Точные настройки одежды или волос для этого персонажа в сценах истории. Используй, если отдельному ClothingItem нужно своё смещение именно для этого персонажа.")]
    public List<CharacterEquipmentStoryLayout> storyEquipmentLayouts = new List<CharacterEquipmentStoryLayout>();

    [Tooltip("Точные настройки одежды или волос для этого персонажа в гардеробе. Используй, если отдельному ClothingItem нужно своё смещение в гардеробе для персонажа или типа внешности.")]
    public List<CharacterEquipmentWardrobeLayout> wardrobeEquipmentLayouts = new List<CharacterEquipmentWardrobeLayout>();

    // ────────────────────────────────────────────────

    /// <summary>
    /// Получить спрайт эмоции (классический режим).
    /// В слоевом режиме возвращает bodySprite — используй GetFaceSprite отдельно.
    /// </summary>
    public Sprite GetEmotion(CharacterEmotionType emotionType)
    {
        if (inheritAppearanceFromPlayer)
        {
            var variant = GetVariantForCurrentAppearance();
            if (variant != null)
            {
                var variantEmotion = variant.emotions.Find(x => x.emotion == emotionType);
                if (variantEmotion != null && variantEmotion.sprite != null)
                    return variantEmotion.sprite;

                if (variant.defaultSprite != null)
                    return variant.defaultSprite;
            }
        }

        var e = emotions.Find(x => x.emotion == emotionType);
        if (e != null && e.sprite != null) return e.sprite;
        return defaultSprite;
    }

    /// <summary>
    /// [Слоевой режим] Получить спрайт лица для эмоции.
    /// Возвращает null если режим не слоевой или спрайт не задан.
    /// </summary>
    public Sprite GetFaceSprite(CharacterEmotionType emotionType)
    {
        return TryGetFaceSprite(emotionType, out Sprite faceSprite, out _)
            ? faceSprite
            : null;
    }

    public bool TryGetFaceSprite(
        CharacterEmotionType emotionType,
        out Sprite faceSprite,
        out CharacterEmotionType resolvedEmotionType)
    {
        faceSprite = null;
        resolvedEmotionType = emotionType;

        if (!useLayeredEmotions)
            return false;

        foreach (CharacterEmotionType candidate in GetEmotionFallbackCandidates(emotionType))
        {
            faceSprite = GetFaceSpriteExact(candidate);
            if (faceSprite == null)
                continue;

            resolvedEmotionType = candidate;
            return true;
        }

        return false;
    }

    Sprite GetFaceSpriteExact(CharacterEmotionType emotionType)
    {
        var layer = emotionLayers != null ? emotionLayers.Find(x => x != null && x.emotion == emotionType) : null;
        if (layer != null && layer.faceSprite != null)
            return layer.faceSprite;

        // Backward-compatible path for characters that were imported with body in
        // bodySprite/defaultSprite and face overlays in the legacy emotions list.
        var emotion = GetEmotionEntry(emotionType);
        if (emotion != null && emotion.sprite != null && emotion.sprite != GetBodySprite())
            return emotion.sprite;

        return null;
    }

    static IEnumerable<CharacterEmotionType> GetEmotionFallbackCandidates(CharacterEmotionType emotionType)
    {
        yield return emotionType;

        if (emotionType != CharacterEmotionType.Idle)
            yield return CharacterEmotionType.Idle;

        if (emotionType != CharacterEmotionType.Neutral)
            yield return CharacterEmotionType.Neutral;
    }

    /// <summary>
    /// Получить базовый спрайт тела.
    /// В слоевом режиме — bodySprite; в классическом — defaultSprite с учётом наследования.
    /// </summary>
    public Sprite GetBodySprite()
    {
        if (useLayeredEmotions && bodySprite != null)
            return bodySprite;

        return GetBaseSprite();
    }

    /// <summary>
    /// Получить базовый спрайт персонажа с учётом внешности ГГ.
    /// </summary>
    public Sprite GetBaseSprite()
    {
        if (inheritAppearanceFromPlayer)
        {
            var variant = GetVariantForCurrentAppearance();
            if (variant != null && variant.defaultSprite != null)
                return variant.defaultSprite;
        }
        return defaultSprite;
    }

    public AppearanceVariant GetVariantForCurrentAppearance()
    {
        return GetAppearanceVariant(PlayerAppearance.CurrentAppearance);
    }

    public AppearanceVariant GetAppearanceVariant(AppearanceType appearance)
    {
        if (appearanceVariants == null)
            return null;

        return appearanceVariants.Find(x => x != null && x.appearanceType == appearance);
    }

    public CharacterEmotion GetEmotionEntry(CharacterEmotionType emotionType)
    {
        if (inheritAppearanceFromPlayer)
        {
            var variant = GetVariantForCurrentAppearance();
            if (variant != null && variant.emotions != null)
            {
                var variantEmotion = variant.emotions.Find(x => x != null && x.emotion == emotionType);
                if (variantEmotion != null)
                    return variantEmotion;
            }
        }

        return emotions != null ? emotions.Find(x => x != null && x.emotion == emotionType) : null;
    }

    public CharacterEmotionLayer GetFaceLayer(CharacterEmotionType emotionType)
    {
        return emotionLayers != null ? emotionLayers.Find(x => x != null && x.emotion == emotionType) : null;
    }

    public StoryLayerLayout GetStoryBodyLayout()
    {
        AppearanceVariant variant = inheritAppearanceFromPlayer ? GetVariantForCurrentAppearance() : null;
        StoryLayerLayout variantLayout = variant?.storyLayerLayout?.body;
        if (variantLayout != null && variantLayout.HasCustomLayout())
            return variantLayout;

        StoryLayerLayout characterLayout = storyLayerLayout?.body;
        return characterLayout != null && characterLayout.HasCustomLayout() ? characterLayout : null;
    }

    public StoryLayerLayout GetStoryEmotionLayout()
    {
        AppearanceVariant variant = inheritAppearanceFromPlayer ? GetVariantForCurrentAppearance() : null;
        StoryLayerLayout variantLayout = variant?.storyLayerLayout?.emotion;
        if (variantLayout != null && variantLayout.HasCustomLayout())
            return variantLayout;

        StoryLayerLayout characterLayout = storyLayerLayout?.emotion;
        return characterLayout != null && characterLayout.HasCustomLayout() ? characterLayout : null;
    }

    public StoryLayerLayout GetStoryEquipmentLayout(ClothingItem item, ClothingType type)
    {
        AppearanceType currentAppearance = inheritAppearanceFromPlayer
            ? PlayerAppearance.CurrentAppearance
            : AppearanceType.Default;

        CharacterEquipmentStoryLayout best = null;
        int bestScore = -1;
        if (storyEquipmentLayouts != null)
        {
            foreach (CharacterEquipmentStoryLayout entry in storyEquipmentLayouts)
            {
                if (entry == null || !entry.Matches(item, type, currentAppearance))
                    continue;

                int score = entry.Specificity();
                if (score > bestScore)
                {
                    best = entry;
                    bestScore = score;
                }
            }
        }

        if (best != null)
            return best.layout;

        AppearanceVariant variant = inheritAppearanceFromPlayer ? GetVariantForCurrentAppearance() : null;
        StoryLayerLayout variantLayout = variant?.storyLayerLayout?.GetEquipmentLayout(type);
        if (variantLayout != null && variantLayout.HasCustomLayout())
            return variantLayout;

        StoryLayerLayout characterLayout = storyLayerLayout?.GetEquipmentLayout(type);
        return characterLayout != null && characterLayout.HasCustomLayout() ? characterLayout : null;
    }

    public StoryLayerLayout GetWardrobeBodyLayout()
    {
        AppearanceVariant variant = inheritAppearanceFromPlayer ? GetVariantForCurrentAppearance() : null;
        return CombineLayerLayouts(
            wardrobeLayerLayout?.body,
            variant?.wardrobeLayerLayout?.body);
    }

    public StoryLayerLayout GetWardrobeEquipmentLayout(ClothingItem item, ClothingType type)
    {
        AppearanceType currentAppearance = inheritAppearanceFromPlayer
            ? PlayerAppearance.CurrentAppearance
            : AppearanceType.Default;

        CharacterEquipmentWardrobeLayout best = null;
        int bestScore = -1;
        if (wardrobeEquipmentLayouts != null)
        {
            foreach (CharacterEquipmentWardrobeLayout entry in wardrobeEquipmentLayouts)
            {
                if (entry == null || !entry.Matches(item, type, currentAppearance))
                    continue;

                int score = entry.Specificity();
                if (score > bestScore)
                {
                    best = entry;
                    bestScore = score;
                }
            }
        }

        AppearanceVariant variant = inheritAppearanceFromPlayer ? GetVariantForCurrentAppearance() : null;
        return CombineLayerLayouts(
            wardrobeLayerLayout?.GetEquipmentLayout(type),
            variant?.wardrobeLayerLayout?.GetEquipmentLayout(type),
            best?.layout);
    }

    public StoryPositionLayout GetStoryPositionLayout(CharacterPosition position)
    {
        AppearanceVariant variant = inheritAppearanceFromPlayer ? GetVariantForCurrentAppearance() : null;
        StoryPositionLayout variantLayout = variant?.storyPositionLayout?.GetCombinedLayout(position);
        if (variantLayout != null && variantLayout.HasCustomLayout())
            return variantLayout;

        StoryPositionLayout characterLayout = storyPositionLayout?.GetCombinedLayout(position);
        return characterLayout != null && characterLayout.HasCustomLayout() ? characterLayout : null;
    }

    public ClothingItem GetPermanentEquipmentItem(ClothingType type)
    {
        ClothingItem item;
        switch (type)
        {
            case ClothingType.Hair:
                item = permanentHair;
                break;
            case ClothingType.Accessory:
                item = permanentAccessory;
                break;
            default:
                item = permanentOutfit;
                break;
        }

        return item != null && item.type == type ? item : null;
    }

    void OnValidate()
    {
        NormalizeStoryLayouts();

        Changed?.Invoke(this);

#if UNITY_EDITOR
        WardrobeHeroSetupPage.EditorNotifyCharacterDataChanged(this);
        CharacterViewManager.EditorNotifyCharacterDataChanged(this);
#endif
    }

    void NormalizeStoryLayouts()
    {
        storyLayerLayout ??= new CharacterStoryLayerLayout();
        storyLayerLayout.Normalize();

        storyPositionLayout ??= new CharacterStoryPositionLayout();
        storyPositionLayout.Normalize();

        wardrobeLayerLayout ??= new CharacterWardrobeLayerLayout();
        wardrobeLayerLayout.Normalize();

        if (storyEquipmentLayouts != null)
        {
            foreach (CharacterEquipmentStoryLayout entry in storyEquipmentLayouts)
                entry?.Normalize();
        }

        if (wardrobeEquipmentLayouts != null)
        {
            foreach (CharacterEquipmentWardrobeLayout entry in wardrobeEquipmentLayouts)
                entry?.Normalize();
        }

        if (appearanceVariants != null)
        {
            foreach (AppearanceVariant variant in appearanceVariants)
                variant?.Normalize();
        }

        if (emotions != null)
        {
            foreach (CharacterEmotion emotion in emotions)
                emotion?.Normalize();
        }

        if (emotionLayers != null)
        {
            foreach (CharacterEmotionLayer layer in emotionLayers)
                layer?.Normalize();
        }
    }

    static StoryLayerLayout CombineLayerLayouts(params StoryLayerLayout[] layouts)
    {
        if (layouts == null)
            return null;

        StoryLayerLayout result = new StoryLayerLayout();
        bool hasLayout = false;

        foreach (StoryLayerLayout source in layouts)
        {
            if (source == null || !source.HasCustomLayout())
                continue;

            source.Normalize();
            result.offset += source.offset;
            if (source.width > 0f)
                result.width = source.width;
            if (source.height > 0f)
                result.height = source.height;

            result.scale = new Vector3(
                result.scale.x * source.scale.x,
                result.scale.y * source.scale.y,
                result.scale.z * source.scale.z);
            result.preserveAspect = source.preserveAspect;
            hasLayout = true;
        }

        return hasLayout ? result : null;
    }
}

// ────────────────────────────────────────────────

[Serializable]
public class AppearanceVariant
{
    [Tooltip("Тип внешности главной героини, для которого используется этот вариант.")]
    public AppearanceType appearanceType;

    [Tooltip("Базовый спрайт персонажа для выбранного типа внешности.")]
    public Sprite defaultSprite;

    [Tooltip("Эмоции для выбранного типа внешности. Если список пустой, будет использоваться defaultSprite.")]
    public List<CharacterEmotion> emotions = new List<CharacterEmotion>();

    [Header("Default Sprite Preview Layout")]
    public Vector2 previewOffset;
    [Min(0f)] public float previewWidth;
    [Min(0f)] public float previewHeight;
    public bool previewPreserveAspect = true;

    [Header("Story Layer Layout")]
    public CharacterStoryLayerLayout storyLayerLayout = new CharacterStoryLayerLayout();

    [Header("Story Position Layout")]
    public CharacterStoryPositionLayout storyPositionLayout = new CharacterStoryPositionLayout();

    [Header("Wardrobe Layer Layout")]
    public CharacterWardrobeLayerLayout wardrobeLayerLayout = new CharacterWardrobeLayerLayout();

    public Vector2 GetPreviewSize()
    {
        return new Vector2(previewWidth, previewHeight);
    }

    public bool HasPreviewSize()
    {
        return previewWidth > 0f && previewHeight > 0f;
    }

    public void Normalize()
    {
        previewWidth = Mathf.Max(0f, previewWidth);
        previewHeight = Mathf.Max(0f, previewHeight);

        storyLayerLayout ??= new CharacterStoryLayerLayout();
        storyLayerLayout.Normalize();

        storyPositionLayout ??= new CharacterStoryPositionLayout();
        storyPositionLayout.Normalize();

        wardrobeLayerLayout ??= new CharacterWardrobeLayerLayout();
        wardrobeLayerLayout.Normalize();

        if (emotions != null)
        {
            foreach (CharacterEmotion emotion in emotions)
                emotion?.Normalize();
        }
    }
}
