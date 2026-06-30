using System;
using UnityEngine;

public enum AppearanceType
{
    Default,
    European,
    African,
    Asian,
    Latino
}

public class PlayerAppearance : MonoBehaviour
{
    public static PlayerAppearance Instance;

    public static AppearanceType CurrentAppearance { get; private set; } = AppearanceType.Default;
    public static string PlayerName { get; private set; } = HeroCustomizationStore.DefaultPlayerName;
    public static string OutfitId { get; private set; } = "";
    public static string HairId { get; private set; } = "";
    public static string AccessoryId { get; private set; } = "";
    public static Sprite OutfitSprite { get; private set; }
    public static Sprite HairSprite { get; private set; }
    public static Sprite AccessorySprite { get; private set; }
    public static ClothingItem OutfitItem { get; private set; }
    public static ClothingItem HairItem { get; private set; }
    public static ClothingItem AccessoryItem { get; private set; }

    public static event Action<AppearanceType> OnAppearanceChanged;
    public static event Action OnWardrobeChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Instance = null;
        CurrentAppearance = AppearanceType.Default;
        PlayerName = HeroCustomizationStore.DefaultPlayerName;
        OutfitId = "";
        HairId = "";
        AccessoryId = "";
        OutfitSprite = null;
        HairSprite = null;
        AccessorySprite = null;
        OutfitItem = null;
        HairItem = null;
        AccessoryItem = null;
        OnAppearanceChanged = null;
        OnWardrobeChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadBeforeSceneStarts()
    {
        Load();
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Load();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void SetAppearance(AppearanceType type)
    {
        type = HeroCustomizationState.NormalizeAppearance(type);
        CurrentAppearance = type;

        SaveCurrentState();
        OnAppearanceChanged?.Invoke(type);
    }

    public static void SetPlayerName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        PlayerName = HeroCustomizationState.NormalizePlayerName(name);
        SaveCurrentState();
    }

    public static void SetEquippedClothing(ClothingType type, string id, Sprite sprite, ClothingItem item = null)
    {
        switch (type)
        {
            case ClothingType.Hair:
                HairId = id ?? "";
                HairSprite = sprite != null ? sprite : (item != null ? item.sprite : null);
                HairItem = item != null && item.type == ClothingType.Hair ? item : null;
                break;

            case ClothingType.Outfit:
                OutfitId = id ?? "";
                OutfitSprite = sprite != null ? sprite : (item != null ? item.sprite : null);
                OutfitItem = item != null && item.type == ClothingType.Outfit ? item : null;
                break;

            case ClothingType.Accessory:
                AccessoryId = id ?? "";
                AccessorySprite = sprite != null ? sprite : (item != null ? item.sprite : null);
                AccessoryItem = item != null && item.type == ClothingType.Accessory ? item : null;
                break;
        }

        SaveCurrentState();
        OnWardrobeChanged?.Invoke();
    }

    public static HeroCustomizationState CaptureState()
    {
        return HeroCustomizationState.CaptureCurrent();
    }

    public static void ApplyState(
        HeroCustomizationState state,
        Sprite outfitSprite = null,
        Sprite hairSprite = null,
        ClothingItem outfitItem = null,
        ClothingItem hairItem = null,
        Sprite accessorySprite = null,
        ClothingItem accessoryItem = null,
        bool save = true,
        bool notify = true)
    {
        if (state == null)
            state = new HeroCustomizationState();

        state.Normalized();
        string previousOutfitId = OutfitId;
        string previousHairId = HairId;
        string previousAccessoryId = AccessoryId;
        ClothingItem previousOutfitItem = OutfitItem;
        ClothingItem previousHairItem = HairItem;
        ClothingItem previousAccessoryItem = AccessoryItem;
        Sprite previousOutfitSprite = OutfitSprite;
        Sprite previousHairSprite = HairSprite;
        Sprite previousAccessorySprite = AccessorySprite;
        bool keepPreviousOutfit = !string.IsNullOrWhiteSpace(state.outfitId) &&
            string.Equals(previousOutfitId, state.outfitId, StringComparison.OrdinalIgnoreCase);
        bool keepPreviousHair = !string.IsNullOrWhiteSpace(state.hairId) &&
            string.Equals(previousHairId, state.hairId, StringComparison.OrdinalIgnoreCase);
        bool keepPreviousAccessory = !string.IsNullOrWhiteSpace(state.accessoryId) &&
            string.Equals(previousAccessoryId, state.accessoryId, StringComparison.OrdinalIgnoreCase);

        CurrentAppearance = state.appearance;
        PlayerName = state.playerName;
        OutfitId = state.outfitId;
        HairId = state.hairId;
        AccessoryId = state.accessoryId;
        OutfitItem = outfitItem != null && outfitItem.type == ClothingType.Outfit
            ? outfitItem
            : keepPreviousOutfit ? previousOutfitItem : null;
        HairItem = hairItem != null && hairItem.type == ClothingType.Hair
            ? hairItem
            : keepPreviousHair ? previousHairItem : null;
        AccessoryItem = accessoryItem != null && accessoryItem.type == ClothingType.Accessory
            ? accessoryItem
            : keepPreviousAccessory ? previousAccessoryItem : null;
        OutfitSprite = outfitSprite != null
            ? outfitSprite
            : OutfitItem != null ? OutfitItem.sprite : keepPreviousOutfit ? previousOutfitSprite : null;
        HairSprite = hairSprite != null
            ? hairSprite
            : HairItem != null ? HairItem.sprite : keepPreviousHair ? previousHairSprite : null;
        AccessorySprite = accessorySprite != null
            ? accessorySprite
            : AccessoryItem != null ? AccessoryItem.sprite : keepPreviousAccessory ? previousAccessorySprite : null;

        if (save)
            HeroCustomizationStore.Save(state);

        if (notify)
        {
            OnAppearanceChanged?.Invoke(CurrentAppearance);
            OnWardrobeChanged?.Invoke();
        }
    }

    static void Load()
    {
        ApplyState(HeroCustomizationStore.Load(), save: false, notify: false);
    }

    public static string ReplacePlaceholders(string text)
    {
        return DialogueVariableResolver.ResolveText(
            text,
            DialogueVariableContext.StoryUi(nameof(PlayerAppearance)));
    }

    static void SaveCurrentState()
    {
        HeroCustomizationStore.Save(CaptureState());
    }
}
