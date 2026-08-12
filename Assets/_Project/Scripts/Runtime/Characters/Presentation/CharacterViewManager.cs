using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum ActiveCharacterSwitchHideMode
{
    None = 0,
    Instant = 1,
    Fade = 2
}

/// <summary>
/// Показывает персонажей на позициях Left / Center / Right.
/// Код только подставляет спрайты в назначенные UI-слои и не меняет позицию объектов из инспектора.
/// </summary>
public class CharacterViewManager : MonoBehaviour
{
    [Header("Слот Left: независимые слои персонажа")]
    [Tooltip("Image слоя Body в слоте Left. Сюда ставится базовое тело или спрайт текущей внешности. Код не двигает и не выключает объект: позиция, якоря, размер и иерархия берутся из инспектора.")]
    public Image leftBodyImage;

    [FormerlySerializedAs("leftFaceImage")]
    [Tooltip("Image слоя Emotion в слоте Left. Сюда ставится эмоция текущей реплики. Слой независим от Body: код меняет только Source Image и Enabled, не двигая и не выключая GameObject.")]
    public Image leftEmotionImage;

    [Tooltip("Image слоя Outfit в слоте Left. Сюда ставится одежда, выбранная игроком. Слой независим от Body: код меняет только Source Image и Enabled, не двигая и не выключая GameObject.")]
    public Image leftOutfitImage;

    [Tooltip("Image слоя Hair в слоте Left. Сюда ставится причёска, выбранная игроком. Слой независим от Body: код меняет только Source Image и Enabled, не двигая и не выключая GameObject.")]
    public Image leftHairImage;

    [Tooltip("Image слоя Accessory в слоте Left. Если поле пустое, менеджер создаст [Equipment] Accessory внутри Body.")]
    public Image leftAccessoryImage;

    [Header("Слот Center: независимые слои персонажа")]
    [Tooltip("Image слоя Body в слоте Center. Перетащи сюда именно объект Body из Center. На этот слой ставится базовое тело или спрайт текущей внешности; позиция, якоря, размер и иерархия остаются из инспектора.")]
    public Image centerBodyImage;

    [FormerlySerializedAs("centerFaceImage")]
    [Tooltip("Image слоя Emotion в слоте Center. Перетащи сюда объект Emotion из Center. На этот слой ставится эмоция текущей реплики, а Body остаётся базовым телом.")]
    public Image centerEmotionImage;

    [Tooltip("Image слоя Outfit в слоте Center. Перетащи сюда объект Outfit из Center. На этот слой ставится одежда, выбранная игроком; код не двигает и не выключает GameObject.")]
    public Image centerOutfitImage;

    [Tooltip("Image слоя Hair в слоте Center. Перетащи сюда объект Hair из Center. На этот слой ставится причёска, выбранная игроком; код не двигает и не выключает GameObject.")]
    public Image centerHairImage;

    [Tooltip("Image слоя Accessory в слоте Center. Если поле пустое, менеджер создаст [Equipment] Accessory внутри Body.")]
    public Image centerAccessoryImage;

    [Header("Слот Right: независимые слои персонажа")]
    [Tooltip("Image слоя Body в слоте Right. Сюда ставится базовое тело или спрайт текущей внешности. Код не двигает и не выключает объект: позиция, якоря, размер и иерархия берутся из инспектора.")]
    public Image rightBodyImage;

    [FormerlySerializedAs("rightFaceImage")]
    [Tooltip("Image слоя Emotion в слоте Right. Сюда ставится эмоция текущей реплики. Слой независим от Body: код меняет только Source Image и Enabled, не двигая и не выключая GameObject.")]
    public Image rightEmotionImage;

    [Tooltip("Image слоя Outfit в слоте Right. Сюда ставится одежда, выбранная игроком. Слой независим от Body: код меняет только Source Image и Enabled, не двигая и не выключая GameObject.")]
    public Image rightOutfitImage;

    [Tooltip("Image слоя Hair в слоте Right. Сюда ставится причёска, выбранная игроком. Слой независим от Body: код меняет только Source Image и Enabled, не двигая и не выключая GameObject.")]
    public Image rightHairImage;

    [Tooltip("Image слоя Accessory в слоте Right. Если поле пустое, менеджер создаст [Equipment] Accessory внутри Body.")]
    public Image rightAccessoryImage;

    [Header("Плавная смена эмоций")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Длительность плавной смены эмоции в секундах. Меняется только прозрачность слоя Emotion или Body при старой однослойной схеме; позиция, якоря и размер не меняются.")]
    private float emotionFadeDuration = 0.25f;

    [SerializeField]
    [Tooltip("Если героиня появляется в слоте сразу с эмоцией, сначала показать нейтральное лицо, а затем плавно сменить его на эмоцию реплики.")]
    private bool animateInitialHeroEmotion = true;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Пауза перед первой сменой нейтрального лица героини на эмоцию реплики.")]
    private float initialHeroEmotionDelay = 0.08f;

    [Header("Смена персонажа в одном слоте")]
    [Tooltip("Как скрывать текущего персонажа, когда другой персонаж занимает тот же слот Left, Center или Right.")]
    [SerializeField] private ActiveCharacterSwitchHideMode activeCharacterSwitchHideMode = ActiveCharacterSwitchHideMode.Instant;

    [Tooltip("Длительность плавной замены персонажа в том же слоте. Используется только в режиме скрытия Fade.")]
    [Min(0f)]
    [SerializeField] private float activeCharacterSwitchFadeDuration = 0.25f;

    CharacterData _currentLeft;
    CharacterData _currentCenter;
    CharacterData _currentRight;

    CharacterEmotionType _emotionLeft;
    CharacterEmotionType _emotionCenter;
    CharacterEmotionType _emotionRight;

    readonly Dictionary<Image, LayerDefaults> _layerDefaults = new Dictionary<Image, LayerDefaults>();
    readonly Dictionary<RectTransform, SlotDefaults> _slotDefaults = new Dictionary<RectTransform, SlotDefaults>();
    readonly Dictionary<Image, Tween> _pendingInitialEmotionTweens = new Dictionary<Image, Tween>();

    struct LayerDefaults
    {
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public Vector3 LocalScale;
        public bool PreserveAspect;

        public static LayerDefaults Capture(Image image)
        {
            RectTransform rect = image.rectTransform;
            return new LayerDefaults
            {
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta,
                LocalScale = rect.localScale,
                PreserveAspect = image.preserveAspect
            };
        }
    }

    struct LayerLayout
    {
        public bool HasCustomLayout;
        public Vector2 Offset;
        public Vector2 Size;
        public bool HasWidth;
        public bool HasHeight;
        public Vector3 Scale;
        public bool HasScale;
        public bool PreserveAspect;

        public static LayerLayout Default => new LayerLayout();

        public static LayerLayout FromStory(StoryLayerLayout layout)
        {
            if (layout == null || !layout.HasCustomLayout())
                return Default;

            return new LayerLayout
            {
                HasCustomLayout = true,
                Offset = layout.offset,
                Size = layout.Size,
                HasWidth = layout.width > 0f,
                HasHeight = layout.height > 0f,
                Scale = layout.scale,
                HasScale = true,
                PreserveAspect = layout.preserveAspect
            };
        }

    }

    struct SlotDefaults
    {
        public Vector2 AnchoredPosition;
        public Vector3 LocalScale;

        public static SlotDefaults Capture(RectTransform rect)
        {
            return new SlotDefaults
            {
                AnchoredPosition = rect.anchoredPosition,
                LocalScale = rect.localScale
            };
        }
    }

    struct SlotLayout
    {
        public bool HasCustomLayout;
        public Vector2 Offset;
        public Vector3 Scale;

        public static SlotLayout Default => new SlotLayout { Scale = Vector3.one };

        public static SlotLayout FromStory(StoryPositionLayout layout)
        {
            if (layout == null || !layout.HasCustomLayout())
                return Default;

            return new SlotLayout
            {
                HasCustomLayout = true,
                Offset = layout.offset,
                Scale = layout.scale
            };
        }
    }

    void Awake()
    {
        DisableAllInstant();
    }

    void OnDisable()
    {
        PlayerAppearance.OnAppearanceChanged -= RefreshCurrentSlots;
        PlayerAppearance.OnWardrobeChanged -= RefreshCurrentSlots;
        CharacterData.Changed -= OnCharacterDataChanged;
        ClothingItem.Changed -= OnClothingItemChanged;
        KillTweens();
    }

    void OnDestroy()
    {
        PlayerAppearance.OnAppearanceChanged -= RefreshCurrentSlots;
        PlayerAppearance.OnWardrobeChanged -= RefreshCurrentSlots;
        CharacterData.Changed -= OnCharacterDataChanged;
        ClothingItem.Changed -= OnClothingItemChanged;
        KillTweens();
    }

    void OnEnable()
    {
        PlayerAppearance.OnAppearanceChanged -= RefreshCurrentSlots;
        PlayerAppearance.OnAppearanceChanged += RefreshCurrentSlots;
        PlayerAppearance.OnWardrobeChanged -= RefreshCurrentSlots;
        PlayerAppearance.OnWardrobeChanged += RefreshCurrentSlots;
        CharacterData.Changed -= OnCharacterDataChanged;
        CharacterData.Changed += OnCharacterDataChanged;
        ClothingItem.Changed -= OnClothingItemChanged;
        ClothingItem.Changed += OnClothingItemChanged;
    }

    void RefreshCurrentSlots()
    {
        if (_currentLeft != null)
            SetupSlot(_currentLeft, _emotionLeft, CharacterPosition.Left, leftBodyImage, leftEmotionImage, leftOutfitImage, leftHairImage, leftAccessoryImage, ref _currentLeft, ref _emotionLeft);

        if (_currentCenter != null)
            SetupSlot(_currentCenter, _emotionCenter, CharacterPosition.Center, centerBodyImage, centerEmotionImage, centerOutfitImage, centerHairImage, centerAccessoryImage, ref _currentCenter, ref _emotionCenter);

        if (_currentRight != null)
            SetupSlot(_currentRight, _emotionRight, CharacterPosition.Right, rightBodyImage, rightEmotionImage, rightOutfitImage, rightHairImage, rightAccessoryImage, ref _currentRight, ref _emotionRight);
    }

    void RefreshCurrentSlots(AppearanceType _)
    {
        RefreshCurrentSlots();
    }

    void OnCharacterDataChanged(CharacterData character)
    {
        if (character == null || !UsesCharacter(character))
            return;

        RefreshCurrentSlots();
    }

    void OnClothingItemChanged(ClothingItem item)
    {
        if (item == null ||
            item != PlayerAppearance.OutfitItem &&
            item != PlayerAppearance.HairItem &&
            item != PlayerAppearance.AccessoryItem)
            return;

        RefreshCurrentSlots();
    }

    bool UsesCharacter(CharacterData character)
    {
        return character != null &&
               (_currentLeft == character || _currentCenter == character || _currentRight == character);
    }

    bool UsesEquippedItem(ClothingItem item)
    {
        return item != null &&
               (item == PlayerAppearance.OutfitItem ||
                item == PlayerAppearance.HairItem ||
                item == PlayerAppearance.AccessoryItem ||
                UsesPermanentItem(_currentLeft, item) ||
                UsesPermanentItem(_currentCenter, item) ||
                UsesPermanentItem(_currentRight, item));
    }

    static bool UsesPermanentItem(CharacterData character, ClothingItem item)
    {
        if (character == null || item == null)
            return false;

        return character.GetPermanentEquipmentItem(item.type) == item;
    }

    void RefreshIfUses(CharacterData character)
    {
        if (!UsesCharacter(character))
            return;

        RefreshCurrentSlots();
    }

    void RefreshIfUses(ClothingItem item)
    {
        if (!UsesEquippedItem(item))
            return;

        RefreshCurrentSlots();
    }

#if UNITY_EDITOR
    public static void EditorNotifyCharacterDataChanged(CharacterData character)
    {
        if (character == null)
            return;

        foreach (CharacterViewManager manager in Resources.FindObjectsOfTypeAll<CharacterViewManager>())
        {
            if (manager == null || UnityEditor.EditorUtility.IsPersistent(manager))
                continue;

            manager.RefreshIfUses(character);
            UnityEditor.EditorUtility.SetDirty(manager);
        }
    }

    public static void EditorNotifyClothingItemChanged(ClothingItem item)
    {
        if (item == null)
            return;

        foreach (CharacterViewManager manager in Resources.FindObjectsOfTypeAll<CharacterViewManager>())
        {
            if (manager == null || UnityEditor.EditorUtility.IsPersistent(manager))
                continue;

            manager.RefreshIfUses(item);
            UnityEditor.EditorUtility.SetDirty(manager);
        }
    }
#endif

    public void SetupCharacter(CharacterData data, CharacterEmotionType emotion, CharacterPosition position)
    {
        switch (position)
        {
            case CharacterPosition.Left:
                SetupSlotWithSwitchMode(
                    data,
                    emotion,
                    CharacterPosition.Left,
                    leftBodyImage,
                    leftEmotionImage,
                    leftOutfitImage,
                    leftHairImage,
                    leftAccessoryImage,
                    ref _currentLeft,
                    ref _emotionLeft);
                break;

            case CharacterPosition.Center:
                SetupSlotWithSwitchMode(
                    data,
                    emotion,
                    CharacterPosition.Center,
                    centerBodyImage,
                    centerEmotionImage,
                    centerOutfitImage,
                    centerHairImage,
                    centerAccessoryImage,
                    ref _currentCenter,
                    ref _emotionCenter);
                break;

            case CharacterPosition.Right:
                SetupSlotWithSwitchMode(
                    data,
                    emotion,
                    CharacterPosition.Right,
                    rightBodyImage,
                    rightEmotionImage,
                    rightOutfitImage,
                    rightHairImage,
                    rightAccessoryImage,
                    ref _currentRight,
                    ref _emotionRight);
                break;
        }
    }

    public void FadeOutPosition(CharacterPosition position, float duration)
    {
        switch (position)
        {
            case CharacterPosition.Left:
                FadeOutSlot(CharacterPosition.Left, leftBodyImage, leftEmotionImage, leftOutfitImage, leftHairImage, leftAccessoryImage, duration);
                break;
            case CharacterPosition.Center:
                FadeOutSlot(CharacterPosition.Center, centerBodyImage, centerEmotionImage, centerOutfitImage, centerHairImage, centerAccessoryImage, duration);
                break;
            case CharacterPosition.Right:
                FadeOutSlot(CharacterPosition.Right, rightBodyImage, rightEmotionImage, rightOutfitImage, rightHairImage, rightAccessoryImage, duration);
                break;
        }
    }

    public void HideAllExcept(CharacterPosition visiblePosition, float duration)
    {
        if (visiblePosition != CharacterPosition.Left)
            FadeOutSlot(CharacterPosition.Left, leftBodyImage, leftEmotionImage, leftOutfitImage, leftHairImage, leftAccessoryImage, duration);

        if (visiblePosition != CharacterPosition.Center)
            FadeOutSlot(CharacterPosition.Center, centerBodyImage, centerEmotionImage, centerOutfitImage, centerHairImage, centerAccessoryImage, duration);

        if (visiblePosition != CharacterPosition.Right)
            FadeOutSlot(CharacterPosition.Right, rightBodyImage, rightEmotionImage, rightOutfitImage, rightHairImage, rightAccessoryImage, duration);
    }

    public void HideAll(float duration)
    {
        FadeOutSlot(CharacterPosition.Left, leftBodyImage, leftEmotionImage, leftOutfitImage, leftHairImage, leftAccessoryImage, duration);
        FadeOutSlot(CharacterPosition.Center, centerBodyImage, centerEmotionImage, centerOutfitImage, centerHairImage, centerAccessoryImage, duration);
        FadeOutSlot(CharacterPosition.Right, rightBodyImage, rightEmotionImage, rightOutfitImage, rightHairImage, rightAccessoryImage, duration);
    }

    void SetupSlotWithSwitchMode(
        CharacterData data,
        CharacterEmotionType emotion,
        CharacterPosition position,
        Image bodyImage,
        Image emotionImage,
        Image outfitImage,
        Image hairImage,
        Image accessoryImage,
        ref CharacterData currentData,
        ref CharacterEmotionType currentEmotion)
    {
        if (ShouldHidePreviousCharacterForSwitch(currentData, data))
        {
            switch (activeCharacterSwitchHideMode)
            {
                case ActiveCharacterSwitchHideMode.Instant:
                    HideSlot(bodyImage, emotionImage, outfitImage, hairImage, accessoryImage);
                    break;

                case ActiveCharacterSwitchHideMode.Fade:
                    if (activeCharacterSwitchFadeDuration > 0f && DOTween.instance != null)
                    {
                        CharacterData nextData = data;
                        CharacterEmotionType nextEmotion = emotion;
                        FadeOutSlot(position, bodyImage, emotionImage, outfitImage, hairImage, accessoryImage, activeCharacterSwitchFadeDuration, () =>
                            SetupCharacter(nextData, nextEmotion, position));
                        return;
                    }

                    HideSlot(bodyImage, emotionImage, outfitImage, hairImage, accessoryImage);
                    break;
            }
        }

        SetupSlot(
            data,
            emotion,
            position,
            bodyImage,
            emotionImage,
            outfitImage,
            hairImage,
            accessoryImage,
            ref currentData,
            ref currentEmotion);
    }

    static bool ShouldHidePreviousCharacterForSwitch(CharacterData currentData, CharacterData nextData)
    {
        return currentData != null && nextData != null && currentData != nextData;
    }

    void SetupSlot(
        CharacterData data,
        CharacterEmotionType emotion,
        CharacterPosition position,
        Image bodyImage,
        Image emotionImage,
        Image outfitImage,
        Image hairImage,
        Image accessoryImage,
        ref CharacterData currentData,
        ref CharacterEmotionType currentEmotion)
    {
        if (data == null)
        {
            HideSlot(bodyImage, emotionImage, outfitImage, hairImage, accessoryImage);
            currentData = null;
            return;
        }

        bool sameCharacter = currentData == data;
        bool sameEmotion = sameCharacter && currentEmotion == emotion;
        bool animateInitialEmotion = ShouldAnimateInitialHeroEmotion(data, emotion, sameCharacter);
        CharacterEmotionType visibleEmotion = animateInitialEmotion
            ? CharacterEmotionType.Neutral
            : emotion;

        currentData = data;
        currentEmotion = emotion;

        KillPendingInitialEmotionTween(bodyImage);
        KillPendingInitialEmotionTween(emotionImage);

        EnsureSlotVisible(bodyImage, emotionImage, outfitImage, hairImage, accessoryImage);
        ApplySlotLayout(FindSlotRoot(bodyImage), SlotLayout.FromStory(data.GetStoryPositionLayout(position)));

        bool hasSeparateEmotionLayer = emotionImage != null && data.useLayeredEmotions;
        Sprite bodySprite = hasSeparateEmotionLayer
            ? ResolveBodySprite(data)
            : ResolveSingleLayerSprite(data, visibleEmotion);
        LayerLayout bodyLayout = hasSeparateEmotionLayer
            ? ResolveBodyLayout(data)
            : ResolveSingleLayerLayout(data, visibleEmotion);

        if (!hasSeparateEmotionLayer && sameCharacter && !sameEmotion)
            CrossFadeImage(bodyImage, bodySprite, bodyLayout);
        else
            SetLayerSprite(bodyImage, bodySprite, bodyLayout);

        if (hasSeparateEmotionLayer)
        {
            Sprite emotionSprite = ResolveEmotionSprite(data, visibleEmotion, out CharacterEmotionType resolvedEmotion);
            LayerLayout emotionLayout = ResolveEmotionLayout(data, resolvedEmotion);
            if (sameCharacter && !sameEmotion)
                CrossFadeImage(emotionImage, emotionSprite, emotionLayout);
            else
                SetLayerSprite(emotionImage, emotionSprite, emotionLayout);
        }
        else
        {
            SetLayerSprite(emotionImage, null, LayerLayout.Default);
        }

        ApplyEquipmentLayers(data, bodyImage, outfitImage, hairImage, accessoryImage);

        if (animateInitialEmotion)
            AnimateInitialHeroEmotion(data, emotion, position, hasSeparateEmotionLayer, bodyImage, emotionImage);
    }

    bool ShouldAnimateInitialHeroEmotion(CharacterData data, CharacterEmotionType targetEmotion, bool sameCharacter)
    {
        if (!animateInitialHeroEmotion || data == null || sameCharacter)
            return false;

        if (!data.inheritAppearanceFromPlayer)
            return false;

        if (targetEmotion == CharacterEmotionType.Neutral || targetEmotion == CharacterEmotionType.Idle)
            return false;

        return emotionFadeDuration > 0f;
    }

    void AnimateInitialHeroEmotion(
        CharacterData data,
        CharacterEmotionType targetEmotion,
        CharacterPosition position,
        bool hasSeparateEmotionLayer,
        Image bodyImage,
        Image emotionImage)
    {
        Image targetImage = hasSeparateEmotionLayer ? emotionImage : bodyImage;
        if (targetImage == null)
            return;

        KillPendingInitialEmotionTween(targetImage);

        if (initialHeroEmotionDelay <= 0f)
        {
            ApplyInitialHeroEmotionTarget(data, targetEmotion, position, hasSeparateEmotionLayer, bodyImage, emotionImage);
            return;
        }

        Tween tween = DOVirtual.DelayedCall(initialHeroEmotionDelay, () =>
        {
            _pendingInitialEmotionTweens.Remove(targetImage);
            ApplyInitialHeroEmotionTarget(data, targetEmotion, position, hasSeparateEmotionLayer, bodyImage, emotionImage);
        }, false).SetTarget(targetImage);

        _pendingInitialEmotionTweens[targetImage] = tween;
    }

    void ApplyInitialHeroEmotionTarget(
        CharacterData data,
        CharacterEmotionType targetEmotion,
        CharacterPosition position,
        bool hasSeparateEmotionLayer,
        Image bodyImage,
        Image emotionImage)
    {
        if (data == null || GetCurrentCharacter(position) != data || GetCurrentEmotion(position) != targetEmotion)
            return;

        if (hasSeparateEmotionLayer)
        {
            Sprite emotionSprite = ResolveEmotionSprite(data, targetEmotion, out CharacterEmotionType resolvedEmotion);
            LayerLayout emotionLayout = ResolveEmotionLayout(data, resolvedEmotion);
            CrossFadeImage(emotionImage, emotionSprite, emotionLayout);
            return;
        }

        Sprite bodySprite = ResolveSingleLayerSprite(data, targetEmotion);
        LayerLayout bodyLayout = ResolveSingleLayerLayout(data, targetEmotion);
        CrossFadeImage(bodyImage, bodySprite, bodyLayout);
    }

    Sprite ResolveBodySprite(CharacterData data)
    {
        if (data == null)
            return null;

        if (data.inheritAppearanceFromPlayer)
        {
            AppearanceVariant variant = data.GetVariantForCurrentAppearance();
            if (variant != null && variant.defaultSprite != null)
                return variant.defaultSprite;
        }

        if (data.useLayeredEmotions && data.bodySprite != null)
            return data.bodySprite;

        if (data.bodySprite != null)
            return data.bodySprite;

        return data.GetBodySprite();
    }

    Sprite ResolveSingleLayerSprite(CharacterData data, CharacterEmotionType emotion)
    {
        if (data == null)
            return null;

        foreach (CharacterEmotionType candidate in GetEmotionFallbackCandidates(emotion))
        {
            CharacterEmotion entry = data.GetEmotionEntry(candidate);
            if (entry != null && entry.sprite != null)
                return entry.sprite;
        }

        Sprite emotionSprite = data.GetEmotion(emotion);
        return emotionSprite != null ? emotionSprite : ResolveBodySprite(data);
    }

    Sprite ResolveEmotionSprite(
        CharacterData data,
        CharacterEmotionType emotion,
        out CharacterEmotionType resolvedEmotion)
    {
        resolvedEmotion = emotion;

        if (data == null || !data.useLayeredEmotions)
            return null;

        if (data.TryGetFaceSprite(emotion, out Sprite faceSprite, out resolvedEmotion))
            return faceSprite;

        return null;
    }

    static IEnumerable<CharacterEmotionType> GetEmotionFallbackCandidates(CharacterEmotionType emotion)
    {
        yield return emotion;

        if (emotion != CharacterEmotionType.Idle)
            yield return CharacterEmotionType.Idle;

        if (emotion != CharacterEmotionType.Neutral)
            yield return CharacterEmotionType.Neutral;
    }

    LayerLayout ResolveBodyLayout(CharacterData data)
    {
        if (data == null)
            return LayerLayout.Default;

        LayerLayout storyLayout = LayerLayout.FromStory(data.GetStoryBodyLayout());
        if (storyLayout.HasCustomLayout)
            return storyLayout;

        return LayerLayout.Default;
    }

    LayerLayout ResolveSingleLayerLayout(CharacterData data, CharacterEmotionType emotion)
    {
        if (data == null)
            return LayerLayout.Default;

        CharacterEmotion emotionEntry = data.GetEmotionEntry(emotion);
        LayerLayout storyEmotionLayout = LayerLayout.FromStory(emotionEntry?.storyLayout);
        if (storyEmotionLayout.HasCustomLayout)
            return storyEmotionLayout;

        LayerLayout characterEmotionLayout = LayerLayout.FromStory(data.GetStoryEmotionLayout());
        if (characterEmotionLayout.HasCustomLayout)
            return characterEmotionLayout;

        return ResolveBodyLayout(data);
    }

    LayerLayout ResolveEmotionLayout(CharacterData data, CharacterEmotionType emotion)
    {
        if (data == null)
            return LayerLayout.Default;

        CharacterEmotionLayer faceLayer = data.GetFaceLayer(emotion);
        LayerLayout storyFaceLayout = LayerLayout.FromStory(faceLayer?.storyLayout);
        if (storyFaceLayout.HasCustomLayout)
            return storyFaceLayout;

        CharacterEmotion emotionEntry = data.GetEmotionEntry(emotion);
        LayerLayout storyEmotionLayout = LayerLayout.FromStory(emotionEntry?.storyLayout);
        if (storyEmotionLayout.HasCustomLayout)
            return storyEmotionLayout;

        LayerLayout characterEmotionLayout = LayerLayout.FromStory(data.GetStoryEmotionLayout());
        if (characterEmotionLayout.HasCustomLayout)
            return characterEmotionLayout;

        return LayerLayout.Default;
    }

    void ApplyEquipmentLayers(CharacterData data, Image bodyImage, Image outfitImage, Image hairImage, Image accessoryImage)
    {
        bool usePlayerWardrobe = data != null && data.inheritAppearanceFromPlayer;
        ClothingItem permanentOutfit = data != null ? data.GetPermanentEquipmentItem(ClothingType.Outfit) : null;
        ClothingItem permanentHair = data != null ? data.GetPermanentEquipmentItem(ClothingType.Hair) : null;
        ClothingItem permanentAccessory = data != null ? data.GetPermanentEquipmentItem(ClothingType.Accessory) : null;
        ClothingItem outfitItem = usePlayerWardrobe && PlayerAppearance.OutfitItem != null
            ? PlayerAppearance.OutfitItem
            : permanentOutfit;
        ClothingItem hairItem = usePlayerWardrobe && PlayerAppearance.HairItem != null
            ? PlayerAppearance.HairItem
            : permanentHair;
        ClothingItem accessoryItem = usePlayerWardrobe && PlayerAppearance.AccessoryItem != null
            ? PlayerAppearance.AccessoryItem
            : permanentAccessory;
        SetEquipmentLayer(
            bodyImage,
            outfitImage,
            "Outfit",
            ResolveEquipmentSprite(usePlayerWardrobe ? PlayerAppearance.OutfitSprite : null, outfitItem),
            ResolveEquipmentLayout(data, outfitItem, ClothingType.Outfit));
        SetEquipmentLayer(
            bodyImage,
            accessoryImage,
            "Accessory",
            ResolveEquipmentSprite(usePlayerWardrobe ? PlayerAppearance.AccessorySprite : null, accessoryItem),
            ResolveEquipmentLayout(data, accessoryItem, ClothingType.Accessory));
        SetEquipmentLayer(
            bodyImage,
            hairImage,
            "Hair",
            ResolveEquipmentSprite(usePlayerWardrobe ? PlayerAppearance.HairSprite : null, hairItem),
            ResolveEquipmentLayout(data, hairItem, ClothingType.Hair));
    }

    static Sprite ResolveEquipmentSprite(Sprite equippedSprite, ClothingItem item)
    {
        if (equippedSprite != null)
            return equippedSprite;

        return item != null ? item.sprite : null;
    }

    LayerLayout ResolveEquipmentLayout(CharacterData data, ClothingItem item, ClothingType type)
    {
        if (data == null)
            return LayerLayout.Default;

        return LayerLayout.FromStory(data.GetStoryEquipmentLayout(item, type));
    }

    void SetEquipmentLayer(Image bodyImage, Image explicitLayer, string layerName, Sprite sprite, LayerLayout layout)
    {
        Image layer = explicitLayer != null ? explicitLayer : GetOrCreateEquipmentLayer(bodyImage, layerName);
        SetLayerSprite(layer, sprite, layout);
    }

    Image GetOrCreateEquipmentLayer(Image bodyImage, string layerName)
    {
        if (bodyImage == null)
            return null;

        string objectName = "[Equipment] " + layerName;
        Transform existing = bodyImage.transform.Find(objectName);
        if (existing != null)
            return existing.GetComponent<Image>();

        var layerObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        layerObject.transform.SetParent(bodyImage.transform, false);

        var rect = layerObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        var image = layerObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = bodyImage.preserveAspect;
        image.enabled = false;
        return image;
    }

    void CrossFadeImage(Image image, Sprite newSprite, LayerLayout layout)
    {
        if (image == null)
            return;

        if (!image.enabled || image.sprite == null || image.sprite == newSprite || emotionFadeDuration <= 0f)
        {
            SetLayerSprite(image, newSprite, layout);
            return;
        }

        Image oldImage = CreateCrossFadeImage(image);
        if (oldImage == null)
        {
            SetLayerSprite(image, newSprite, layout);
            return;
        }

        CanvasGroup newCanvasGroup = GetOrAddCanvasGroup(image.gameObject);
        CanvasGroup oldCanvasGroup = GetOrAddCanvasGroup(oldImage.gameObject);
        newCanvasGroup.interactable = false;
        newCanvasGroup.blocksRaycasts = false;
        oldCanvasGroup.interactable = false;
        oldCanvasGroup.blocksRaycasts = false;

        if (DOTween.instance != null)
        {
            DOTween.Kill(image);
            DOTween.Kill(image.rectTransform);
            DOTween.Kill(newCanvasGroup);
            DOTween.Kill(oldCanvasGroup);
        }

        SetLayerSprite(image, newSprite, layout);
        newCanvasGroup.alpha = newSprite != null ? 0f : 1f;
        oldCanvasGroup.alpha = 1f;

        Tween oldFade = oldCanvasGroup.DOFade(0f, emotionFadeDuration).SetTarget(oldImage);
        Tween newFade = newSprite != null
            ? newCanvasGroup.DOFade(1f, emotionFadeDuration).SetTarget(image)
            : null;

        Sequence sequence = DOTween.Sequence();
        sequence.Join(oldFade);
        if (newFade != null)
            sequence.Join(newFade);
        sequence.OnComplete(() =>
        {
            if (newCanvasGroup != null)
                newCanvasGroup.alpha = 1f;

            DestroyCrossFadeImage(oldImage);
        });
    }

    Image CreateCrossFadeImage(Image source)
    {
        if (source == null || source.rectTransform == null || source.transform.parent == null)
            return null;

        GameObject overlay = new GameObject($"{source.name}_PreviousFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        overlay.hideFlags = HideFlags.DontSave;

        Transform parent = source.transform.parent;
        overlay.transform.SetParent(parent, false);
        overlay.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

        RectTransform sourceRect = source.rectTransform;
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        CopyRectTransform(sourceRect, overlayRect);

        Image image = overlay.GetComponent<Image>();
        image.sprite = source.sprite;
        image.type = source.type;
        image.preserveAspect = source.preserveAspect;
        image.raycastTarget = false;
        image.color = source.color;
        image.material = source.material;
        image.enabled = source.enabled && source.sprite != null;

        CanvasGroup canvasGroup = overlay.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.ignoreParentGroups = false;

        return image;
    }

    static void CopyRectTransform(RectTransform source, RectTransform destination)
    {
        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.anchoredPosition = source.anchoredPosition;
        destination.sizeDelta = source.sizeDelta;
        destination.offsetMin = source.offsetMin;
        destination.offsetMax = source.offsetMax;
        destination.pivot = source.pivot;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    static void DestroyCrossFadeImage(Image image)
    {
        if (image == null)
            return;

        GameObject target = image.gameObject;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    void SetLayerSprite(Image image, Sprite sprite, LayerLayout layout)
    {
        if (image == null)
            return;

        ApplyLayerLayout(image, sprite != null ? layout : LayerLayout.Default);

        if (!image.gameObject.activeSelf)
            image.gameObject.SetActive(true);

        image.sprite = sprite;
        image.enabled = sprite != null;
        SetImageAlpha(image, 1f);

        CanvasGroup canvasGroup = image.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    void ApplyLayerLayout(Image image, LayerLayout layout)
    {
        if (image == null)
            return;

        LayerDefaults defaults = GetLayerDefaults(image);
        RectTransform rect = image.rectTransform;

        if (!layout.HasCustomLayout)
        {
            rect.anchoredPosition = defaults.AnchoredPosition;
            rect.sizeDelta = defaults.SizeDelta;
            rect.localScale = defaults.LocalScale;
            image.preserveAspect = defaults.PreserveAspect;
            return;
        }

        rect.anchoredPosition = defaults.AnchoredPosition + layout.Offset;
        Vector2 size = defaults.SizeDelta;
        if (layout.HasWidth)
            size.x = layout.Size.x;
        if (layout.HasHeight)
            size.y = layout.Size.y;
        rect.sizeDelta = size;

        Vector3 scale = layout.HasScale ? layout.Scale : Vector3.one;
        rect.localScale = new Vector3(
            defaults.LocalScale.x * scale.x,
            defaults.LocalScale.y * scale.y,
            defaults.LocalScale.z * scale.z);

        image.preserveAspect = layout.PreserveAspect;
    }

    void ApplySlotLayout(Transform slotRoot, SlotLayout layout)
    {
        RectTransform rect = slotRoot as RectTransform;
        if (rect == null)
            return;

        SlotDefaults defaults = GetSlotDefaults(rect);

        if (!layout.HasCustomLayout)
        {
            rect.anchoredPosition = defaults.AnchoredPosition;
            rect.localScale = defaults.LocalScale;
            return;
        }

        rect.anchoredPosition = defaults.AnchoredPosition + layout.Offset;
        rect.localScale = new Vector3(
            defaults.LocalScale.x * layout.Scale.x,
            defaults.LocalScale.y * layout.Scale.y,
            defaults.LocalScale.z * layout.Scale.z);
    }

    LayerDefaults GetLayerDefaults(Image image)
    {
        if (image == null)
            return new LayerDefaults();

        if (_layerDefaults.TryGetValue(image, out LayerDefaults defaults))
            return defaults;

        defaults = LayerDefaults.Capture(image);
        _layerDefaults[image] = defaults;
        return defaults;
    }

    SlotDefaults GetSlotDefaults(RectTransform rect)
    {
        if (rect == null)
            return new SlotDefaults();

        if (_slotDefaults.TryGetValue(rect, out SlotDefaults defaults))
            return defaults;

        defaults = SlotDefaults.Capture(rect);
        _slotDefaults[rect] = defaults;
        return defaults;
    }

    void EnsureSlotVisible(Image bodyImage, Image emotionImage, Image outfitImage, Image hairImage, Image accessoryImage)
    {
        Transform slotRoot = FindSlotRoot(bodyImage);
        KillSlotFade(slotRoot);
        ResetSlotCanvasGroups(slotRoot);

        EnsureLayerVisible(bodyImage, slotRoot);
        EnsureLayerVisible(emotionImage, slotRoot);
        EnsureLayerVisible(outfitImage, slotRoot);
        EnsureLayerVisible(accessoryImage, slotRoot);
        EnsureLayerVisible(hairImage, slotRoot);
    }

    Transform FindSlotRoot(Image bodyImage)
    {
        if (bodyImage == null)
            return null;

        Transform current = bodyImage.transform;
        while (current != null)
        {
            if (IsCharacterSlotRoot(current))
                return current;

            if (current == transform)
                break;

            current = current.parent;
        }

        return bodyImage.transform;
    }

    static bool IsCharacterSlotRoot(Transform target)
    {
        if (target == null)
            return false;

        return target.name == "Left" || target.name == "Center" || target.name == "Right";
    }

    static void EnsureLayerVisible(Image image, Transform slotRoot)
    {
        if (image == null)
            return;

        Transform current = image.transform;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);

            CanvasGroup canvasGroup = current.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                if (DOTween.instance != null)
                    DOTween.Kill(canvasGroup);

                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (current == slotRoot)
                break;

            current = current.parent;
        }
    }

    public void DisableUnused(bool leftUsed, bool centerUsed, bool rightUsed)
    {
        if (!leftUsed)
        {
            HideSlot(leftBodyImage, leftEmotionImage, leftOutfitImage, leftHairImage, leftAccessoryImage);
            _currentLeft = null;
        }

        if (!centerUsed)
        {
            HideSlot(centerBodyImage, centerEmotionImage, centerOutfitImage, centerHairImage, centerAccessoryImage);
            _currentCenter = null;
        }

        if (!rightUsed)
        {
            HideSlot(rightBodyImage, rightEmotionImage, rightOutfitImage, rightHairImage, rightAccessoryImage);
            _currentRight = null;
        }
    }

    void DisableAllInstant()
    {
        HideSlot(leftBodyImage, leftEmotionImage, leftOutfitImage, leftHairImage, leftAccessoryImage);
        HideSlot(centerBodyImage, centerEmotionImage, centerOutfitImage, centerHairImage, centerAccessoryImage);
        HideSlot(rightBodyImage, rightEmotionImage, rightOutfitImage, rightHairImage, rightAccessoryImage);
    }

    void FadeOutSlot(
        CharacterPosition position,
        Image bodyImage,
        Image emotionImage,
        Image outfitImage,
        Image hairImage,
        Image accessoryImage,
        float duration,
        Action onComplete = null)
    {
        CharacterData currentData = GetCurrentCharacter(position);
        if (currentData == null)
        {
            HideSlot(bodyImage, emotionImage, outfitImage, hairImage, accessoryImage);
            return;
        }

        CharacterData fadedData = currentData;
        Transform slotRoot = FindSlotRoot(bodyImage);
        Transform fadeTarget = slotRoot != null ? slotRoot : bodyImage != null ? bodyImage.transform : transform;

        if (duration <= 0f || DOTween.instance == null)
        {
            HideSlot(bodyImage, emotionImage, outfitImage, hairImage, accessoryImage);
            ClearCurrentCharacterIfMatches(position, fadedData);
            onComplete?.Invoke();
            return;
        }

        PrepareSlotLayersForUnifiedFade(slotRoot, bodyImage, emotionImage, outfitImage, hairImage, accessoryImage);
        DOTween.Kill(fadeTarget);

        if (CanFadeWholeSlot(slotRoot, bodyImage, emotionImage, outfitImage, hairImage, accessoryImage))
        {
            CanvasGroup slotGroup = GetOrAddCanvasGroup(slotRoot.gameObject);
            if (slotGroup == null)
            {
                FadeOutSlotLayers(position, fadedData, fadeTarget, bodyImage, emotionImage, outfitImage, hairImage, accessoryImage, duration, onComplete);
                return;
            }

            DOTween.Kill(slotGroup);
            slotGroup.alpha = 1f;
            slotGroup.interactable = false;
            slotGroup.blocksRaycasts = false;
            slotGroup.DOFade(0f, duration)
                .SetTarget(fadeTarget)
                .OnComplete(() =>
            {
                if (GetCurrentCharacter(position) != fadedData)
                    return;

                slotGroup.alpha = 1f;
                HideSlot(bodyImage, emotionImage, outfitImage, hairImage, accessoryImage);
                ClearCurrentCharacterIfMatches(position, fadedData);
                onComplete?.Invoke();
            });
            return;
        }

        FadeOutSlotLayers(position, fadedData, fadeTarget, bodyImage, emotionImage, outfitImage, hairImage, accessoryImage, duration, onComplete);
    }

    void FadeOutSlotLayers(
        CharacterPosition position,
        CharacterData fadedData,
        Transform fadeTarget,
        Image bodyImage,
        Image emotionImage,
        Image outfitImage,
        Image hairImage,
        Image accessoryImage,
        float duration,
        Action onComplete = null)
    {
        SetSlotVisualAlpha(bodyImage, emotionImage, outfitImage, hairImage, accessoryImage, 1f);

        DOTween.To(
                () => 1f,
                alpha => SetSlotVisualAlpha(bodyImage, emotionImage, outfitImage, hairImage, accessoryImage, alpha),
                0f,
                duration)
            .SetTarget(fadeTarget)
            .OnComplete(() =>
        {
            if (GetCurrentCharacter(position) != fadedData)
                return;

            HideSlot(bodyImage, emotionImage, outfitImage, hairImage, accessoryImage);
            ClearCurrentCharacterIfMatches(position, fadedData);
            onComplete?.Invoke();
        });
    }

    void PrepareSlotLayersForUnifiedFade(Transform slotRoot, Image bodyImage, Image emotionImage, Image outfitImage, Image hairImage, Image accessoryImage)
    {
        ResetSlotCanvasGroups(slotRoot);
        ResetSlotImagesForUnifiedFade(slotRoot);
        ResetLayerForUnifiedFade(bodyImage);
        ResetLayerForUnifiedFade(emotionImage);
        ResetLayerForUnifiedFade(ResolveEquipmentLayer(bodyImage, outfitImage, "Outfit"));
        ResetLayerForUnifiedFade(ResolveEquipmentLayer(bodyImage, accessoryImage, "Accessory"));
        ResetLayerForUnifiedFade(ResolveEquipmentLayer(bodyImage, hairImage, "Hair"));
    }

    static bool CanFadeWholeSlot(Transform slotRoot, Image bodyImage, Image emotionImage, Image outfitImage, Image hairImage, Image accessoryImage)
    {
        if (slotRoot == null)
            return false;

        return ContainsLayer(slotRoot, bodyImage) &&
               ContainsLayer(slotRoot, emotionImage) &&
               ContainsLayer(slotRoot, ResolveEquipmentLayer(bodyImage, outfitImage, "Outfit")) &&
               ContainsLayer(slotRoot, ResolveEquipmentLayer(bodyImage, accessoryImage, "Accessory")) &&
               ContainsLayer(slotRoot, ResolveEquipmentLayer(bodyImage, hairImage, "Hair"));
    }

    static bool ContainsLayer(Transform slotRoot, Image image)
    {
        return image == null || image.transform == slotRoot || image.transform.IsChildOf(slotRoot);
    }

    void SetSlotVisualAlpha(Image bodyImage, Image emotionImage, Image outfitImage, Image hairImage, Image accessoryImage, float alpha)
    {
        SetImageAlpha(bodyImage, alpha);
        SetImageAlpha(emotionImage, alpha);
        SetImageAlpha(ResolveEquipmentLayer(bodyImage, outfitImage, "Outfit"), alpha);
        SetImageAlpha(ResolveEquipmentLayer(bodyImage, accessoryImage, "Accessory"), alpha);
        SetImageAlpha(ResolveEquipmentLayer(bodyImage, hairImage, "Hair"), alpha);
    }

    static void ResetLayerForUnifiedFade(Image image)
    {
        if (image == null)
            return;

        if (DOTween.instance != null)
        {
            DOTween.Kill(image);
            DOTween.Kill(image.rectTransform);
        }

        SetImageAlpha(image, 1f);

        CanvasGroup canvasGroup = image.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            return;

        if (DOTween.instance != null)
            DOTween.Kill(canvasGroup);

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    static void ResetSlotCanvasGroups(Transform slotRoot)
    {
        if (slotRoot == null)
            return;

        CanvasGroup[] canvasGroups = slotRoot.GetComponentsInChildren<CanvasGroup>(true);
        foreach (CanvasGroup canvasGroup in canvasGroups)
        {
            if (canvasGroup == null)
                continue;

            if (DOTween.instance != null)
                DOTween.Kill(canvasGroup);

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    static void ResetSlotImagesForUnifiedFade(Transform slotRoot)
    {
        if (slotRoot == null)
            return;

        Image[] images = slotRoot.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
            ResetLayerForUnifiedFade(image);
    }

    static void KillSlotFade(Transform slotRoot)
    {
        if (slotRoot == null || DOTween.instance == null)
            return;

        DOTween.Kill(slotRoot);
    }

    CharacterData GetCurrentCharacter(CharacterPosition position)
    {
        return position switch
        {
            CharacterPosition.Left => _currentLeft,
            CharacterPosition.Center => _currentCenter,
            CharacterPosition.Right => _currentRight,
            _ => null
        };
    }

    CharacterEmotionType GetCurrentEmotion(CharacterPosition position)
    {
        return position switch
        {
            CharacterPosition.Left => _emotionLeft,
            CharacterPosition.Center => _emotionCenter,
            CharacterPosition.Right => _emotionRight,
            _ => CharacterEmotionType.Idle
        };
    }

    void KillPendingInitialEmotionTween(Image image)
    {
        if (image == null)
            return;

        if (_pendingInitialEmotionTweens.TryGetValue(image, out Tween tween) && tween != null && tween.IsActive())
            tween.Kill();

        _pendingInitialEmotionTweens.Remove(image);
    }

    void ClearCurrentCharacterIfMatches(CharacterPosition position, CharacterData character)
    {
        switch (position)
        {
            case CharacterPosition.Left:
                if (_currentLeft == character)
                    _currentLeft = null;
                break;
            case CharacterPosition.Center:
                if (_currentCenter == character)
                    _currentCenter = null;
                break;
            case CharacterPosition.Right:
                if (_currentRight == character)
                    _currentRight = null;
                break;
        }
    }

    void HideSlot(Image bodyImage, Image emotionImage, Image outfitImage, Image hairImage, Image accessoryImage)
    {
        Transform slotRoot = FindSlotRoot(bodyImage);
        KillPendingInitialEmotionTween(bodyImage);
        KillPendingInitialEmotionTween(emotionImage);
        KillSlotFade(slotRoot);
        ResetSlotCanvasGroups(slotRoot);
        ResetSlotImagesForUnifiedFade(slotRoot);
        ApplySlotLayout(slotRoot, SlotLayout.Default);
        SetLayerSprite(bodyImage, null, LayerLayout.Default);
        SetLayerSprite(emotionImage, null, LayerLayout.Default);
        SetLayerSprite(ResolveEquipmentLayer(bodyImage, outfitImage, "Outfit"), null, LayerLayout.Default);
        SetLayerSprite(ResolveEquipmentLayer(bodyImage, accessoryImage, "Accessory"), null, LayerLayout.Default);
        SetLayerSprite(ResolveEquipmentLayer(bodyImage, hairImage, "Hair"), null, LayerLayout.Default);
    }

    static Image ResolveEquipmentLayer(Image bodyImage, Image explicitLayer, string layerName)
    {
        if (explicitLayer != null)
            return explicitLayer;

        if (bodyImage == null)
            return null;

        Transform existing = bodyImage.transform.Find("[Equipment] " + layerName);
        return existing != null ? existing.GetComponent<Image>() : null;
    }

    static CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        if (target == null)
            return null;

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = target.AddComponent<CanvasGroup>();

        return canvasGroup;
    }

    void KillTweens()
    {
        if (DOTween.instance == null)
            return;

        KillImageTweens(leftBodyImage);
        KillImageTweens(leftEmotionImage);
        KillImageTweens(leftOutfitImage);
        KillImageTweens(leftHairImage);

        KillImageTweens(centerBodyImage);
        KillImageTweens(centerEmotionImage);
        KillImageTweens(centerOutfitImage);
        KillImageTweens(centerHairImage);

        KillImageTweens(rightBodyImage);
        KillImageTweens(rightEmotionImage);
        KillImageTweens(rightOutfitImage);
        KillImageTweens(rightHairImage);

        KillSlotTween(leftBodyImage);
        KillSlotTween(centerBodyImage);
        KillSlotTween(rightBodyImage);
    }

    static void KillImageTweens(Image image)
    {
        if (image == null)
            return;

        DOTween.Kill(image);
        DOTween.Kill(image.rectTransform);

        CanvasGroup canvasGroup = image.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            DOTween.Kill(canvasGroup);
    }

    void KillSlotTween(Image bodyImage)
    {
        Transform slotRoot = FindSlotRoot(bodyImage);
        if (slotRoot != null)
            DOTween.Kill(slotRoot);

        CanvasGroup canvasGroup = slotRoot != null ? slotRoot.GetComponent<CanvasGroup>() : null;
        if (canvasGroup != null)
            DOTween.Kill(canvasGroup);
    }
}
