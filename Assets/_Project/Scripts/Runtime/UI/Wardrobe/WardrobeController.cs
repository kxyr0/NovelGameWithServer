using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WardrobeController : MonoBehaviour
{
    public static WardrobeController Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private Image clothingPreview;
    [SerializeField] private Image bodyPreview;
    [SerializeField] private Image outfitPreview;
    [SerializeField] private Image hairPreview;
    [SerializeField] private TMP_Text clothingName;

    [Header("Character")]
    [SerializeField] private CharacterData targetCharacter;
    [SerializeField] private RectTransform characterRoot;

    [Header("Navigation")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button confirmButton;

    [Header("Swipe Animation")]
    [SerializeField] private float swipeDistance = 480f;
    [SerializeField] private float swipeDuration = 0.32f;
    [SerializeField] private Ease swipeEase = Ease.OutCubic;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Fallbacks")]
    [SerializeField] private Sprite defaultOutfitSprite;
    [SerializeField] private bool usePlayerOutfitAsFallback = true;

    [SerializeField] private List<ClothingItem> availableClothes = new List<ClothingItem>();

    public Image ClothingPreview => clothingPreview;
    public TMP_Text ClothingName => clothingName;
    public CharacterData TargetCharacter => targetCharacter;
    public IReadOnlyList<ClothingItem> AvailableClothes => availableClothes;

    private enum SelectionMode
    {
        Clothing,
        Appearance
    }

    private readonly List<AppearanceOption> availableAppearances = new List<AppearanceOption>();

    private SelectionMode selectionMode = SelectionMode.Clothing;
    private int currentIndex = 0;
    private string targetCharacterId = "";
    private Action<int> onConfirmed;
    private Vector2 characterHomePosition;
    private bool hasCharacterHomePosition;
    private Sequence activeSwipe;
    private CanvasGroup characterCanvasGroup;
    private LayerDefaults bodyLayerDefaults;
    private LayerDefaults outfitLayerDefaults;
    private LayerDefaults hairLayerDefaults;

    private struct LayerDefaults
    {
        public bool captured;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector3 localScale;
        public bool preserveAspect;

        public static LayerDefaults Capture(Image image)
        {
            RectTransform rect = image.rectTransform;
            return new LayerDefaults
            {
                captured = true,
                anchoredPosition = rect.anchoredPosition,
                sizeDelta = rect.sizeDelta,
                localScale = rect.localScale,
                preserveAspect = image.preserveAspect
            };
        }
    }

    private void Awake()
    {
        Instance = this;
        AutoWire();
        CaptureLayerDefaults();
        CaptureCharacterHomePosition();
        BindNavigationButtons();
    }

    private void OnEnable()
    {
        ClothingItem.Changed -= OnClothingItemChanged;
        ClothingItem.Changed += OnClothingItemChanged;

        AutoWire();
        CaptureLayerDefaults();
        CaptureCharacterHomePosition();
        BindNavigationButtons();

        if (availableClothes != null && availableClothes.Count > 0)
            RefreshView();
        else
            EnsureBaseLayersVisible();
    }

    private void OnDisable()
    {
        ClothingItem.Changed -= OnClothingItemChanged;
        KillSwipe(restorePosition: true);
        SetNavigationInteractable(true);
    }

    private void OnDestroy()
    {
        UnbindNavigationButtons();

        if (Instance == this)
            Instance = null;
    }

    private void OnClothingItemChanged(ClothingItem item)
    {
        if (item == null || selectionMode != SelectionMode.Clothing || availableClothes == null || availableClothes.Count == 0)
            return;

        currentIndex = Mathf.Clamp(currentIndex, 0, availableClothes.Count - 1);
        if (availableClothes[currentIndex] != item)
            return;

        SetPreviewSprite(item);
    }

#if UNITY_EDITOR
    public static void EditorNotifyClothingItemChanged(ClothingItem item)
    {
        if (item == null || Application.isPlaying)
            return;

        WardrobeController[] controllers = Resources.FindObjectsOfTypeAll<WardrobeController>();
        foreach (WardrobeController controller in controllers)
        {
            if (controller == null || EditorUtility.IsPersistent(controller))
                continue;

            controller.OnClothingItemChanged(item);
            EditorUtility.SetDirty(controller);
        }
    }
#endif

    public void Open(CharacterData character, List<ClothingItem> clothes)
    {
        Open("", character, clothes, null);
    }

    public void Open(string characterId, CharacterData character, List<ClothingItem> clothes, Action<int> confirmCallback = null)
    {
        selectionMode = SelectionMode.Clothing;
        targetCharacter = character;
        targetCharacterId = characterId ?? "";
        availableClothes = confirmCallback == null
            ? FilterClothesForTarget(clothes)
            : clothes ?? new List<ClothingItem>();
        availableAppearances.Clear();
        onConfirmed = confirmCallback;

        currentIndex = 0;

        AutoWire();
        CaptureLayerDefaults();
        CaptureCharacterHomePosition(force: true);
        SetNavigationInteractable(true);
        RefreshView();
    }

    public void OpenAppearance(List<AppearanceOption> appearances, Action<int> confirmCallback = null)
    {
        selectionMode = SelectionMode.Appearance;
        targetCharacter = null;
        targetCharacterId = "";
        if (availableClothes == null)
            availableClothes = new List<ClothingItem>();
        else
            availableClothes.Clear();
        availableAppearances.Clear();

        if (appearances != null)
        {
            foreach (AppearanceOption option in appearances)
            {
                if (option != null)
                    availableAppearances.Add(option);
            }
        }

        onConfirmed = confirmCallback;
        currentIndex = 0;

        AutoWire();
        CaptureLayerDefaults();
        CaptureCharacterHomePosition(force: true);
        SetNavigationInteractable(true);
        EnsureDefaultOutfitVisible();
        RefreshView();
    }

    private void RefreshView()
    {
        if (selectionMode == SelectionMode.Appearance)
        {
            RefreshAppearanceView();
            return;
        }

        if (availableClothes == null || availableClothes.Count == 0)
        {
            EnsureBaseLayersVisible();
            if (clothingName != null)
                clothingName.text = "";
            return;
        }

        currentIndex = Mathf.Clamp(currentIndex, 0, availableClothes.Count - 1);
        var item = availableClothes[currentIndex];

        SetPreviewSprite(item);

        if (clothingName != null)
            clothingName.text = GetDisplayName(item);
    }

    private void RefreshAppearanceView()
    {
        if (availableAppearances.Count == 0)
        {
            SetAppearancePreview(null);
            if (clothingName != null)
                clothingName.text = "";
            return;
        }

        currentIndex = Mathf.Clamp(currentIndex, 0, availableAppearances.Count - 1);
        AppearanceOption option = availableAppearances[currentIndex];
        AppearanceVariant variant = option != null && targetCharacter != null
            ? targetCharacter.GetAppearanceVariant(option.type)
            : null;

        SetAppearancePreview(GetAppearancePreviewSprite(variant, option != null ? option.previewSprite : null), variant);

        if (clothingName != null)
            clothingName.text = GetAppearanceDisplayName(option);
    }

    public void NextClothing()
    {
        MoveClothing(1);
    }

    public void PreviousClothing()
    {
        MoveClothing(-1);
    }

    public void ConfirmCurrent()
    {
        if (selectionMode == SelectionMode.Appearance)
        {
            if (availableAppearances.Count == 0)
                return;

            ConfirmIndex();
            return;
        }

        if (availableClothes == null || availableClothes.Count == 0)
            return;

        ConfirmIndex();
    }

    private void ConfirmIndex()
    {
        if (selectionMode == SelectionMode.Appearance)
            currentIndex = Mathf.Clamp(currentIndex, 0, availableAppearances.Count - 1);
        else
            currentIndex = Mathf.Clamp(currentIndex, 0, availableClothes.Count - 1);

        int selectedIndex = currentIndex;

        if (onConfirmed != null)
        {
            Action<int> callback = onConfirmed;
            onConfirmed = null;
            callback.Invoke(selectedIndex);
            return;
        }

        EquipCurrent();
    }

    public void EquipCurrent()
    {
        if (availableClothes == null || availableClothes.Count == 0)
            return;

        currentIndex = Mathf.Clamp(currentIndex, 0, availableClothes.Count - 1);
        ClothingItem item = availableClothes[currentIndex];
        if (item == null || targetCharacter == null || GameState.Instance == null)
            return;

        GameState.Instance.EquipClothing(GetEquipKey(item), item.id);
        PlayerAppearance.SetEquippedClothing(item.type, item.id, item.sprite, item);

        ApplyToCharacter(item);
    }

    private void ApplyToCharacter(ClothingItem item)
    {
        if (targetCharacter == null || item == null)
            return;

        if (item.type == ClothingType.Hair)
            targetCharacter.hairSprite = item.sprite;
    }

    private void MoveClothing(int direction)
    {
        if (GetOptionCount() == 0)
            return;

        if (activeSwipe != null && activeSwipe.IsActive())
            return;

        int nextIndex = WrapIndex(currentIndex + direction);
        if (nextIndex == currentIndex)
            return;

        if (characterRoot == null || swipeDuration <= 0f)
        {
            currentIndex = nextIndex;
            RefreshView();
            return;
        }

        PlaySwipe(direction, nextIndex);
    }

    private void PlaySwipe(int direction, int nextIndex)
    {
        CaptureCharacterHomePosition();
        EnsureCharacterCanvasGroup();

        float distance = Mathf.Abs(swipeDistance);
        float outOffset = direction > 0 ? -distance : distance;
        float inOffset = -outOffset;
        float halfDuration = Mathf.Max(0.01f, swipeDuration * 0.5f);

        SetNavigationInteractable(false);
        characterRoot.anchoredPosition = characterHomePosition;

        activeSwipe = DOTween.Sequence()
            .SetUpdate(useUnscaledTime)
            .SetTarget(this);

        activeSwipe
            .Append(characterRoot.DOAnchorPos(characterHomePosition + Vector2.right * outOffset, halfDuration).SetEase(Ease.InCubic));

        if (characterCanvasGroup != null)
            activeSwipe.Join(characterCanvasGroup.DOFade(0f, halfDuration).SetEase(Ease.InCubic));

        activeSwipe.AppendCallback(() =>
        {
            currentIndex = nextIndex;
            RefreshView();
            characterRoot.anchoredPosition = characterHomePosition + Vector2.right * inOffset;
        });

        activeSwipe
            .Append(characterRoot.DOAnchorPos(characterHomePosition, halfDuration).SetEase(swipeEase));

        if (characterCanvasGroup != null)
            activeSwipe.Join(characterCanvasGroup.DOFade(1f, halfDuration).SetEase(swipeEase));

        activeSwipe.OnComplete(() =>
        {
            activeSwipe = null;
            SetNavigationInteractable(true);
        });
    }

    private void SetPreviewSprite(ClothingItem item)
    {
        Image target = GetPreviewTarget(item);
        if (target == null)
            return;

        EnsureBaseLayersVisible();

        if (item == null || item.sprite == null)
        {
            if (item != null)
                Debug.LogWarning("Wardrobe item '" + item.id + "' has no sprite. Keeping the current preview layer visible.");
            return;
        }

        target.sprite = item.sprite;
        ShowLayer(target);
        ApplyItemLayout(target, item);
    }

    private Image GetPreviewTarget(ClothingItem item)
    {
        if (item != null && item.type == ClothingType.Hair && hairPreview != null)
            return hairPreview;

        if (item != null && item.type == ClothingType.Outfit && outfitPreview != null)
            return outfitPreview;

        if (outfitPreview != null)
            return outfitPreview;

        return clothingPreview;
    }

    private int WrapIndex(int index)
    {
        int count = GetOptionCount();
        if (count == 0)
            return 0;

        return (index % count + count) % count;
    }

    private int GetOptionCount()
    {
        return selectionMode == SelectionMode.Appearance
            ? availableAppearances.Count
            : availableClothes != null ? availableClothes.Count : 0;
    }

    private string GetDisplayName(ClothingItem item)
    {
        if (item == null)
            return "";

        string displayName = item.GetDisplayName();
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        return item.name ?? "";
    }

    private string GetAppearanceDisplayName(AppearanceOption option)
    {
        if (option == null)
            return "";

        if (!string.IsNullOrWhiteSpace(option.label))
            return option.label;

        return option.type.ToString();
    }

    private void SetAppearancePreview(Sprite sprite, AppearanceVariant variant = null)
    {
        Image target = bodyPreview != null ? bodyPreview : clothingPreview;
        if (target != null && sprite != null)
        {
            target.sprite = sprite;
            ShowLayer(target);
            ApplyAppearanceLayout(target, variant);
        }

        EnsureBaseLayersVisible();
    }

    private static Sprite GetAppearancePreviewSprite(AppearanceVariant variant, Sprite fallback)
    {
        return variant != null && variant.defaultSprite != null ? variant.defaultSprite : fallback;
    }

    private string GetEquipKey(ClothingItem item)
    {
        string characterId = GetTargetCharacterId();

        if (item != null && item.type == ClothingType.Hair)
            return characterId + ":hair";

        if (item != null && item.type == ClothingType.Accessory)
            return characterId + ":accessory";

        return characterId + ":outfit";
    }

    private List<ClothingItem> FilterClothesForTarget(List<ClothingItem> clothes)
    {
        var filtered = new List<ClothingItem>();
        if (clothes == null)
            return filtered;

        string characterId = GetTargetCharacterId();
        StoryManager manager = StoryManager.Instance;
        string storyId = manager != null ? manager.CurrentStoryId : "";
        string chapterId = manager != null
            ? (!string.IsNullOrWhiteSpace(manager.CurrentChapterId) ? manager.CurrentChapterId : manager.CurrentEpisodeId)
            : "";
        foreach (ClothingItem item in clothes)
        {
            if (item != null && item.IsAvailableForWardrobe(characterId, storyId, chapterId))
                filtered.Add(item);
        }

        return filtered;
    }

    private string GetTargetCharacterId()
    {
        string characterId = !string.IsNullOrWhiteSpace(targetCharacterId)
            ? targetCharacterId
            : targetCharacter != null ? targetCharacter.name : "";

        return string.IsNullOrWhiteSpace(characterId) ? "hero" : characterId;
    }

    private void AutoWire()
    {
        if (bodyPreview == null)
            bodyPreview = FindLayerImage("Body");

        if (bodyPreview == null)
            bodyPreview = clothingPreview;

        Image foundOutfit = FindLayerImage("Outfit");
        if (foundOutfit != null && (outfitPreview == null || outfitPreview == bodyPreview || outfitPreview == clothingPreview))
            outfitPreview = foundOutfit;

        if (outfitPreview == null && clothingPreview != null && clothingPreview != bodyPreview)
            outfitPreview = clothingPreview;

        if (hairPreview == null)
            hairPreview = FindLayerImage("Hair");

        if (characterRoot == null)
            characterRoot = FindCharacterRoot();

        if (clothingName == null)
            clothingName = GetComponentInChildren<TMP_Text>(true);

        AutoWireButtons();
    }

    private Image FindLayerImage(string objectName)
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image != null && image.name == objectName)
                return image;
        }

        return null;
    }

    private RectTransform FindCharacterRoot()
    {
        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform rect in rects)
        {
            if (rect != null && rect.name == "Character")
                return rect;
        }

        return clothingPreview != null ? clothingPreview.rectTransform : null;
    }

    private void AutoWireButtons()
    {
        Transform buttonRoot = clothingName != null && clothingName.transform.parent != null
            ? clothingName.transform.parent
            : transform;

        Button[] buttons = buttonRoot.GetComponentsInChildren<Button>(true);
        if (buttons == null || buttons.Length == 0)
            return;

        Button leftmost = null;
        Button rightmost = null;
        Button namedPrevious = null;
        Button namedNext = null;
        Button namedConfirm = null;
        float leftX = float.MaxValue;
        float rightX = float.MinValue;

        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            if (namedPrevious == null && IsPreviousButtonCandidate(button))
                namedPrevious = button;

            if (namedNext == null && IsNextButtonCandidate(button))
                namedNext = button;

            if (namedConfirm == null && IsConfirmButtonCandidate(button))
                namedConfirm = button;

            if (button == confirmButton || IsConfirmButtonCandidate(button))
                continue;

            RectTransform rect = button.transform as RectTransform;
            float x = rect != null ? rect.anchoredPosition.x : button.transform.localPosition.x;

            if (x < leftX)
            {
                leftX = x;
                leftmost = button;
            }

            if (x > rightX)
            {
                rightX = x;
                rightmost = button;
            }
        }

        if (previousButton == null)
            previousButton = namedPrevious != null ? namedPrevious : leftmost;
        if (nextButton == null)
            nextButton = namedNext != null ? namedNext : rightmost;

        if (confirmButton == null && namedConfirm != previousButton && namedConfirm != nextButton)
            confirmButton = namedConfirm;
    }

    private bool IsPreviousButtonCandidate(Button button)
    {
        return ButtonTextContains(button, "prev", "previous", "back", "left", "arrowleft", "<", "\u043d\u0430\u0437\u0430\u0434", "\u0432\u043b\u0435\u0432");
    }

    private bool IsNextButtonCandidate(Button button)
    {
        return ButtonTextContains(button, "next", "right", "arrowright", ">", "\u0434\u0430\u043b\u0435\u0435", "\u0432\u043f\u0440\u0430\u0432");
    }

    private bool IsConfirmButtonCandidate(Button button)
    {
        return ButtonTextContains(
            button,
            "confirm",
            "ready",
            "done",
            "apply",
            "accept",
            "complete",
            "ok",
            "\u0433\u043e\u0442\u043e\u0432",
            "\u043f\u0440\u0438\u043d\u044f\u0442",
            "\u043f\u043e\u0434\u0442\u0432\u0435\u0440\u0434");
    }

    private bool ButtonTextContains(Button button, params string[] fragments)
    {
        if (button == null || fragments == null)
            return false;

        string text = button.name ?? "";
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            text += " " + label.text;

        text = text.ToLowerInvariant();

        for (int i = 0; i < fragments.Length; i++)
        {
            string fragment = fragments[i];
            if (!string.IsNullOrWhiteSpace(fragment) && text.Contains(fragment.ToLowerInvariant()))
                return true;
        }

        return false;
    }

    private void BindNavigationButtons()
    {
        if (previousButton != null)
        {
            previousButton.onClick.RemoveListener(PreviousClothing);
            previousButton.onClick.AddListener(PreviousClothing);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(NextClothing);
            nextButton.onClick.AddListener(NextClothing);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmCurrent);
            confirmButton.onClick.AddListener(ConfirmCurrent);
        }
    }

    private void UnbindNavigationButtons()
    {
        if (previousButton != null)
            previousButton.onClick.RemoveListener(PreviousClothing);
        if (nextButton != null)
            nextButton.onClick.RemoveListener(NextClothing);
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmCurrent);
    }

    private void SetNavigationInteractable(bool interactable)
    {
        if (previousButton != null)
            previousButton.interactable = interactable;
        if (nextButton != null)
            nextButton.interactable = interactable;
        if (confirmButton != null)
            confirmButton.interactable = interactable;
    }

    private void CaptureCharacterHomePosition(bool force = false)
    {
        if (characterRoot == null)
            return;

        if (!hasCharacterHomePosition || force)
        {
            characterHomePosition = characterRoot.anchoredPosition;
            hasCharacterHomePosition = true;
        }
    }

    private void CaptureLayerDefaults()
    {
        CaptureLayerDefaults(bodyPreview, ref bodyLayerDefaults);
        CaptureLayerDefaults(outfitPreview, ref outfitLayerDefaults);
        CaptureLayerDefaults(hairPreview, ref hairLayerDefaults);
    }

    private void CaptureLayerDefaults(Image image, ref LayerDefaults defaults)
    {
        if (image == null || defaults.captured)
            return;

        defaults = LayerDefaults.Capture(image);
    }

    private void EnsureBaseLayersVisible()
    {
        if (bodyPreview != null && bodyPreview.sprite != null)
            ShowLayer(bodyPreview);

        EnsureDefaultOutfitVisible();

        if (hairPreview != null && hairPreview.sprite != null)
            ShowLayer(hairPreview);
    }

    private void EnsureDefaultOutfitVisible()
    {
        if (outfitPreview == null || outfitPreview == bodyPreview)
            return;

        Sprite fallbackSprite = ResolveFallbackOutfitSprite();
        if (outfitPreview.sprite == null && fallbackSprite != null)
        {
            outfitPreview.sprite = fallbackSprite;
            ApplyDefaultLayout(outfitPreview);
        }

        if (outfitPreview.sprite != null)
            ShowLayer(outfitPreview);
    }

    private Sprite ResolveFallbackOutfitSprite()
    {
        if (defaultOutfitSprite != null)
            return defaultOutfitSprite;

        if (usePlayerOutfitAsFallback && PlayerAppearance.OutfitSprite != null)
            return PlayerAppearance.OutfitSprite;

        if (availableClothes == null)
            return null;

        foreach (ClothingItem item in availableClothes)
        {
            if (item != null && item.type == ClothingType.Outfit && item.sprite != null)
                return item.sprite;
        }

        return null;
    }

    private void ShowLayer(Image image)
    {
        if (image == null)
            return;

        image.gameObject.SetActive(true);
        image.enabled = image.sprite != null;
    }

    private void ApplyItemLayout(Image image, ClothingItem item)
    {
        if (image == null || item == null)
            return;

        RectTransform rect = image.rectTransform;

        ClothingWardrobePreviewLayout layout = item.GetWardrobePreviewLayout(PlayerAppearance.CurrentAppearance);
        rect.anchoredPosition3D = new Vector3(layout.Offset.x, layout.Offset.y, 0f);

        Vector2 previewSize = layout.Size;
        if (previewSize.x > 0f && previewSize.y > 0f)
            rect.sizeDelta = previewSize;
        else
            rect.sizeDelta = GetFallbackPreviewSize(image, item.sprite);

        rect.localScale = NormalizeScale(layout.Scale);

        image.preserveAspect = layout.PreserveAspect;

        ApplyWardrobeCharacterLayout(image, targetCharacter != null ? targetCharacter.GetWardrobeEquipmentLayout(item, item.type) : null);
    }

    private Vector2 GetFallbackPreviewSize(Image image, Sprite sprite)
    {
        if (sprite != null && sprite.rect.width > 0f && sprite.rect.height > 0f)
            return new Vector2(sprite.rect.width, sprite.rect.height);

        LayerDefaults defaults = GetLayerDefaults(image);
        return defaults.sizeDelta;
    }

    private static Vector3 NormalizeScale(Vector3 scale)
    {
        scale.x = Mathf.Approximately(scale.x, 0f) ? 1f : scale.x;
        scale.y = Mathf.Approximately(scale.y, 0f) ? 1f : scale.y;
        scale.z = Mathf.Approximately(scale.z, 0f) ? 1f : scale.z;
        return scale;
    }

    private void ApplyAppearanceLayout(Image image, AppearanceVariant variant)
    {
        if (image == null)
            return;

        if (variant == null)
        {
            ApplyDefaultLayout(image);
            ApplyWardrobeCharacterLayout(image, targetCharacter != null ? targetCharacter.GetWardrobeBodyLayout() : null);
            return;
        }

        LayerDefaults defaults = GetLayerDefaults(image);
        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = defaults.anchoredPosition + variant.previewOffset;

        Vector2 previewSize = variant.GetPreviewSize();
        if (previewSize.x > 0f && previewSize.y > 0f)
            rect.sizeDelta = previewSize;
        else
            rect.sizeDelta = defaults.sizeDelta;

        rect.localScale = defaults.localScale;
        image.preserveAspect = variant.previewPreserveAspect;

        ApplyWardrobeCharacterLayout(image, targetCharacter != null ? targetCharacter.GetWardrobeBodyLayout() : null);
    }

    private void ApplyWardrobeCharacterLayout(Image image, StoryLayerLayout layout)
    {
        if (image == null || layout == null || !layout.HasCustomLayout())
            return;

        RectTransform rect = image.rectTransform;
        rect.anchoredPosition += layout.offset;

        Vector2 size = rect.sizeDelta;
        if (layout.width > 0f)
            size.x = layout.width;
        if (layout.height > 0f)
            size.y = layout.height;
        rect.sizeDelta = size;

        Vector3 scale = NormalizeScale(layout.scale);
        rect.localScale = new Vector3(
            rect.localScale.x * scale.x,
            rect.localScale.y * scale.y,
            rect.localScale.z * scale.z);
        image.preserveAspect = layout.preserveAspect;
    }

    private void ApplyDefaultLayout(Image image)
    {
        if (image == null)
            return;

        LayerDefaults defaults = GetLayerDefaults(image);
        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = defaults.anchoredPosition;
        rect.sizeDelta = defaults.sizeDelta;
        rect.localScale = defaults.localScale;
        image.preserveAspect = defaults.preserveAspect;
    }

    private LayerDefaults GetLayerDefaults(Image image)
    {
        if (image == bodyPreview && bodyLayerDefaults.captured)
            return bodyLayerDefaults;
        if (image == outfitPreview && outfitLayerDefaults.captured)
            return outfitLayerDefaults;
        if (image == hairPreview && hairLayerDefaults.captured)
            return hairLayerDefaults;

        return LayerDefaults.Capture(image);
    }

    private void EnsureCharacterCanvasGroup()
    {
        if (characterRoot == null || characterCanvasGroup != null)
            return;

        characterCanvasGroup = characterRoot.GetComponent<CanvasGroup>();
        if (characterCanvasGroup == null)
            characterCanvasGroup = characterRoot.gameObject.AddComponent<CanvasGroup>();
        characterCanvasGroup.alpha = 1f;
    }

    private void KillSwipe(bool restorePosition)
    {
        if (activeSwipe != null)
        {
            activeSwipe.Kill();
            activeSwipe = null;
        }

        if (restorePosition && characterRoot != null && hasCharacterHomePosition)
            characterRoot.anchoredPosition = characterHomePosition;

        if (characterCanvasGroup != null)
            characterCanvasGroup.alpha = 1f;
    }
}
