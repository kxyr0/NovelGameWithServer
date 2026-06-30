using System;
using System.Collections.Generic;
using UnityEngine;

public enum ClothingType
{
    Hair,
    Outfit,
    Accessory
}

public enum SkinTone
{
    Light,
    Tan,
    Dark
}

public enum ClothingWardrobeLayoutGroup
{
    Auto,
    None,
    Silk,
    NaSkoruyu,
    Ukladka,
    Hollywood,
    LeaveAsIs,
    Braid,
    Bun,
    Kare,
    Loose
}

[Serializable]
public class ClothingWardrobeAppearanceLayout
{
    public AppearanceType appearanceType = AppearanceType.Default;
    public Vector2 offset;
    [Min(0f)] public float width;
    [Min(0f)] public float height;
    public Vector3 scale = Vector3.one;
    public bool overridePreserveAspect;
    public bool preserveAspect = true;

    public bool Matches(AppearanceType appearance)
    {
        return appearanceType == HeroCustomizationState.NormalizeAppearance(appearance);
    }

    public void Normalize()
    {
        appearanceType = HeroCustomizationState.NormalizeAppearance(appearanceType);
        width = Mathf.Max(0f, width);
        height = Mathf.Max(0f, height);
        scale.x = Mathf.Approximately(scale.x, 0f) ? 1f : scale.x;
        scale.y = Mathf.Approximately(scale.y, 0f) ? 1f : scale.y;
        scale.z = Mathf.Approximately(scale.z, 0f) ? 1f : scale.z;
    }
}

public struct ClothingWardrobePreviewLayout
{
    public Vector2 Offset;
    public Vector2 Size;
    public Vector3 Scale;
    public bool PreserveAspect;
}

[CreateAssetMenu(menuName = "VN/Clothing Item")]
public class ClothingItem : ScriptableObject
{
    public static event Action<ClothingItem> Changed;

    public string id;

    [SerializeField] private string ownerCharacterId;

    [Header("Story Availability")]
    [SerializeField] private List<string> visibleInStoryIds = new List<string>();
    [SerializeField] private List<string> visibleInChapterIds = new List<string>();
    [SerializeField] private List<string> hiddenInStoryIds = new List<string>();
    [SerializeField] private List<string> hiddenInChapterIds = new List<string>();

    [SerializeField] private string displayName;

    public ClothingType type;

    public SkinTone skinTone;

    public Sprite sprite;

    [Header("Wardrobe Preview Layout")]
    public Vector2 wardrobeOffset;
    [Min(0f)] public float wardrobeWidth;
    [Min(0f)] public float wardrobeHeight;
    [HideInInspector]
    public Vector2 wardrobeSize;
    public Vector3 wardrobeScale = Vector3.one;
    public bool wardrobePreserveAspect = true;

    [Tooltip("Группа предметов с одинаковой посадкой в гардеробе. Auto пытается определить группу по id, например silk, na_skoruyu, ukladka или hollywood.")]
    public ClothingWardrobeLayoutGroup wardrobeLayoutGroup = ClothingWardrobeLayoutGroup.Auto;

    [Header("Wardrobe Per-Appearance Overrides")]
    [Tooltip("Дополнительная настройка позиции предмета в гардеробе для каждого типа внешности героини. Смещение прибавляется к общему смещению гардероба; ширина и высота заменяют общие значения только если больше 0.")]
    public System.Collections.Generic.List<ClothingWardrobeAppearanceLayout> wardrobeAppearanceLayouts = new System.Collections.Generic.List<ClothingWardrobeAppearanceLayout>();

    public string OwnerCharacterId => ownerCharacterId;

    public string DisplayName => GetDisplayName();

    public IReadOnlyList<string> VisibleInStoryIds => visibleInStoryIds;
    public IReadOnlyList<string> VisibleInChapterIds => visibleInChapterIds;
    public IReadOnlyList<string> HiddenInStoryIds => hiddenInStoryIds;
    public IReadOnlyList<string> HiddenInChapterIds => hiddenInChapterIds;

    public bool IsAvailableForWardrobe(string characterId, string storyId, string chapterId)
    {
        return IsAvailableForCharacter(characterId) && IsAvailableForStoryContext(storyId, chapterId);
    }

    public bool IsAvailableForCharacter(string characterId)
    {
        string ownerId = NormalizeCharacterId(ownerCharacterId);
        if (string.IsNullOrEmpty(ownerId))
            return true;

        string targetId = NormalizeCharacterId(characterId);
        if (string.IsNullOrEmpty(targetId))
            targetId = "hero";

        return string.Equals(ownerId, targetId, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsAvailableForStoryContext(string storyId, string chapterId)
    {
        storyId = NormalizeStoryContextId(storyId);
        chapterId = NormalizeStoryContextId(chapterId);

        if (MatchesAnyStoryContextId(hiddenInStoryIds, storyId) ||
            MatchesAnyStoryContextId(hiddenInChapterIds, chapterId))
        {
            return false;
        }

        bool hasStoryAllowList = HasStoryContextEntries(visibleInStoryIds);
        bool hasChapterAllowList = HasStoryContextEntries(visibleInChapterIds);
        if (!hasStoryAllowList && !hasChapterAllowList)
            return true;

        if (string.IsNullOrEmpty(storyId) && string.IsNullOrEmpty(chapterId))
            return false;

        bool storyAllowed = !hasStoryAllowList || MatchesAnyStoryContextId(visibleInStoryIds, storyId);
        bool chapterAllowed = !hasChapterAllowList || MatchesAnyStoryContextId(visibleInChapterIds, chapterId);
        return storyAllowed && chapterAllowed;
    }

    public ClothingWardrobeLayoutGroup GetResolvedWardrobeLayoutGroup()
    {
        if (wardrobeLayoutGroup != ClothingWardrobeLayoutGroup.Auto)
            return wardrobeLayoutGroup;

        return ResolveWardrobeLayoutGroup(id, name);
    }

    public static ClothingWardrobeLayoutGroup ResolveWardrobeLayoutGroup(string id, string assetName = "")
    {
        string value = ((id ?? "") + " " + (assetName ?? "")).ToLowerInvariant();
        value = value.Replace('-', '_').Replace(' ', '_');

        if (value.Contains("silk"))
            return ClothingWardrobeLayoutGroup.Silk;
        if (value.Contains("na_skoruyu") || value.Contains("na_skoruy"))
            return ClothingWardrobeLayoutGroup.NaSkoruyu;
        if (value.Contains("ukladka"))
            return ClothingWardrobeLayoutGroup.Ukladka;
        if (value.Contains("hollywood"))
            return ClothingWardrobeLayoutGroup.Hollywood;
        if (value.Contains("leave_as_is"))
            return ClothingWardrobeLayoutGroup.LeaveAsIs;
        if (value.Contains("braid"))
            return ClothingWardrobeLayoutGroup.Braid;
        if (value.Contains("bun"))
            return ClothingWardrobeLayoutGroup.Bun;
        if (value.Contains("kare"))
            return ClothingWardrobeLayoutGroup.Kare;
        if (value.Contains("loose"))
            return ClothingWardrobeLayoutGroup.Loose;

        return ClothingWardrobeLayoutGroup.None;
    }

    public Vector2 GetWardrobePreviewSize()
    {
        if (wardrobeWidth > 0f || wardrobeHeight > 0f)
            return new Vector2(wardrobeWidth, wardrobeHeight);

        return wardrobeSize;
    }

    public bool HasWardrobePreviewSize()
    {
        Vector2 size = GetWardrobePreviewSize();
        return size.x > 0f && size.y > 0f;
    }

    public ClothingWardrobePreviewLayout GetWardrobePreviewLayout(AppearanceType appearance)
    {
        Vector2 baseSize = GetWardrobePreviewSize();
        var result = new ClothingWardrobePreviewLayout
        {
            Offset = wardrobeOffset,
            Size = baseSize,
            Scale = NormalizeScale(wardrobeScale),
            PreserveAspect = wardrobePreserveAspect
        };

        ClothingWardrobeAppearanceLayout overrideLayout = GetWardrobeAppearanceLayout(appearance);
        if (overrideLayout == null)
            return result;

        overrideLayout.Normalize();
        result.Offset += overrideLayout.offset;

        if (overrideLayout.width > 0f)
            result.Size.x = overrideLayout.width;
        if (overrideLayout.height > 0f)
            result.Size.y = overrideLayout.height;

        Vector3 overrideScale = NormalizeScale(overrideLayout.scale);
        result.Scale = new Vector3(
            result.Scale.x * overrideScale.x,
            result.Scale.y * overrideScale.y,
            result.Scale.z * overrideScale.z);

        if (overrideLayout.overridePreserveAspect)
            result.PreserveAspect = overrideLayout.preserveAspect;

        return result;
    }

    void OnValidate()
    {
        ownerCharacterId = NormalizeCharacterId(ownerCharacterId);
        NormalizeStoryContextIdList(visibleInStoryIds);
        NormalizeStoryContextIdList(visibleInChapterIds);
        NormalizeStoryContextIdList(hiddenInStoryIds);
        NormalizeStoryContextIdList(hiddenInChapterIds);
        wardrobeWidth = Mathf.Max(0f, wardrobeWidth);
        wardrobeHeight = Mathf.Max(0f, wardrobeHeight);
        wardrobeScale = NormalizeScale(wardrobeScale);

        if (wardrobeWidth <= 0f && wardrobeHeight <= 0f && wardrobeSize.x > 0f && wardrobeSize.y > 0f)
        {
            wardrobeWidth = wardrobeSize.x;
            wardrobeHeight = wardrobeSize.y;
        }

        if (wardrobeWidth > 0f || wardrobeHeight > 0f)
            wardrobeSize = new Vector2(wardrobeWidth, wardrobeHeight);

        if (wardrobeAppearanceLayouts != null)
        {
            foreach (ClothingWardrobeAppearanceLayout layout in wardrobeAppearanceLayouts)
                layout?.Normalize();
        }

        Changed?.Invoke(this);

#if UNITY_EDITOR
        WardrobeHeroSetupPage.EditorNotifyClothingItemChanged(this);
        WardrobeController.EditorNotifyClothingItemChanged(this);
        CharacterViewManager.EditorNotifyClothingItemChanged(this);
#endif
    }

    public string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName.Trim();

        return HumanizeId(!string.IsNullOrWhiteSpace(id) ? id : name);
    }

    static string NormalizeCharacterId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string sanitized = SaveDataSanitizer.SanitizeIdentifier(value);
        return string.IsNullOrWhiteSpace(sanitized) ? value.Trim() : sanitized.Trim();
    }

    static string NormalizeStoryContextId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string sanitized = SaveDataSanitizer.SanitizeIdentifier(value);
        return string.IsNullOrWhiteSpace(sanitized) ? value.Trim() : sanitized.Trim();
    }

    static bool HasStoryContextEntries(List<string> values)
    {
        if (values == null)
            return false;

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return true;
        }

        return false;
    }

    static bool MatchesAnyStoryContextId(List<string> values, string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId) || values == null)
            return false;

        targetId = NormalizeStoryContextId(targetId);
        foreach (string value in values)
        {
            string normalized = NormalizeStoryContextId(value);
            if (!string.IsNullOrEmpty(normalized) &&
                string.Equals(normalized, targetId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    static void NormalizeStoryContextIdList(List<string> values)
    {
        if (values == null)
            return;

        for (int i = 0; i < values.Count; i++)
            values[i] = NormalizeStoryContextId(values[i]);
    }

    ClothingWardrobeAppearanceLayout GetWardrobeAppearanceLayout(AppearanceType appearance)
    {
        if (wardrobeAppearanceLayouts == null)
            return null;

        appearance = HeroCustomizationState.NormalizeAppearance(appearance);
        foreach (ClothingWardrobeAppearanceLayout layout in wardrobeAppearanceLayouts)
        {
            if (layout != null && layout.Matches(appearance))
                return layout;
        }

        return null;
    }

    static Vector3 NormalizeScale(Vector3 scale)
    {
        scale.x = Mathf.Approximately(scale.x, 0f) ? 1f : scale.x;
        scale.y = Mathf.Approximately(scale.y, 0f) ? 1f : scale.y;
        scale.z = Mathf.Approximately(scale.z, 0f) ? 1f : scale.z;
        return scale;
    }

    static string HumanizeId(string rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId))
            return "";

        string normalized = rawId.Trim();
        string known = GetKnownDisplayName(normalized);
        if (!string.IsNullOrEmpty(known))
            return known;

        string[] parts = normalized
            .Replace('-', '_')
            .Replace(')', '_')
            .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return normalized;

        for (int i = 0; i < parts.Length; i++)
            parts[i] = Capitalize(parts[i]);

        return string.Join(" ", parts);
    }

    static string GetKnownDisplayName(string normalizedId)
    {
        switch (normalizedId.ToLowerInvariant())
        {
            case "devitsa_krasa": return "\u0414\u0435\u0432\u0438\u0446\u0430-\u043a\u0440\u0430\u0441\u0430";
            case "doroga_v_podlesie": return "\u0414\u043e\u0440\u043e\u0433\u0430 \u0432 \u041f\u043e\u0434\u043b\u0435\u0441\u044c\u0435";
            case "dress": return "\u041f\u043b\u0430\u0442\u044c\u0435";
            case "gorodskaya": return "\u0413\u043e\u0440\u043e\u0434\u0441\u043a\u0430\u044f";
            case "ivan_defolt": return "\u0418\u0432\u0430\u043d";
            case "letnya_nega": return "\u041b\u0435\u0442\u043d\u044f\u044f \u043d\u0435\u0433\u0430";
            case "mestnaya)obolstitelnitsa": return "\u041c\u0435\u0441\u0442\u043d\u0430\u044f \u043e\u0431\u043e\u043b\u044c\u0441\u0442\u0438\u0442\u0435\u043b\u044c\u043d\u0438\u0446\u0430";
            case "na skoruy_ruku": return "\u041d\u0430 \u0441\u043a\u043e\u0440\u0443\u044e \u0440\u0443\u043a\u0443";
            case "night_cloth": return "\u041d\u043e\u0447\u043d\u043e\u0439 \u043d\u0430\u0440\u044f\u0434";
            case "shirt": return "\u0420\u0443\u0431\u0430\u0448\u043a\u0430";
            case "suit": return "\u041a\u043e\u0441\u0442\u044e\u043c";
            case "hair_braid_black": return "\u0427\u0435\u0440\u043d\u0430\u044f \u043a\u043e\u0441\u0430";
            case "hair_braid_blonde": return "\u0421\u0432\u0435\u0442\u043b\u0430\u044f \u043a\u043e\u0441\u0430";
            case "hair_braid_brown": return "\u041a\u0430\u0448\u0442\u0430\u043d\u043e\u0432\u0430\u044f \u043a\u043e\u0441\u0430";
            case "hair_bun_black": return "\u0427\u0435\u0440\u043d\u044b\u0439 \u043f\u0443\u0447\u043e\u043a";
            case "hair_bun_blonde": return "\u0421\u0432\u0435\u0442\u043b\u044b\u0439 \u043f\u0443\u0447\u043e\u043a";
            case "hair_bun_brown": return "\u041a\u0430\u0448\u0442\u0430\u043d\u043e\u0432\u044b\u0439 \u043f\u0443\u0447\u043e\u043a";
            case "hair_kare_black": return "\u041a\u0430\u0440\u0435 (\u0442\u0435\u043c\u043d\u043e\u0432\u043e\u043b\u043e\u0441\u0430\u044f)";
            case "hair_kare_blonde": return "\u041a\u0430\u0440\u0435 (\u0437\u043e\u043b\u043e\u0442\u0438\u0441\u0442\u0430\u044f)";
            case "hair_kare_brown": return "\u041a\u0430\u0440\u0435 (\u043a\u0430\u0448\u0442\u0430\u043d\u043e\u0432\u0430\u044f)";
            case "hair_loose_black": return "\u0427\u0435\u0440\u043d\u044b\u0435 \u0440\u0430\u0441\u043f\u0443\u0449\u0435\u043d\u043d\u044b\u0435";
            case "hair_loose_blonde": return "\u0421\u0432\u0435\u0442\u043b\u044b\u0435 \u0440\u0430\u0441\u043f\u0443\u0449\u0435\u043d\u043d\u044b\u0435";
            case "hair_loose_brown": return "\u041a\u0430\u0448\u0442\u0430\u043d\u043e\u0432\u044b\u0435 \u0440\u0430\u0441\u043f\u0443\u0449\u0435\u043d\u043d\u044b\u0435";
            default: return "";
        }
    }

    static string Capitalize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.Length == 1
            ? value.ToUpperInvariant()
            : char.ToUpperInvariant(value[0]) + value.Substring(1);
    }
}
