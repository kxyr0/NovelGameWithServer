using System;
using UnityEngine;

[Serializable]
public class StoryLayerLayout
{
    public Vector2 offset;
    [Min(0f)] public float width;
    [Min(0f)] public float height;
    public Vector3 scale = Vector3.one;
    public bool preserveAspect = true;

    public Vector2 Size => new Vector2(width, height);

    public bool HasSize => width > 0f || height > 0f;

    public bool HasCustomLayout()
    {
        return !Mathf.Approximately(offset.x, 0f) ||
               !Mathf.Approximately(offset.y, 0f) ||
               width > 0f ||
               height > 0f ||
               !Mathf.Approximately(scale.x, 1f) ||
               !Mathf.Approximately(scale.y, 1f) ||
               !Mathf.Approximately(scale.z, 1f);
    }

    public void Normalize()
    {
        width = Mathf.Max(0f, width);
        height = Mathf.Max(0f, height);
        scale.x = Mathf.Approximately(scale.x, 0f) ? 1f : scale.x;
        scale.y = Mathf.Approximately(scale.y, 0f) ? 1f : scale.y;
        scale.z = Mathf.Approximately(scale.z, 0f) ? 1f : scale.z;
    }
}

[Serializable]
public class StoryPositionLayout
{
    public Vector2 offset;
    public Vector3 scale = Vector3.one;

    public bool HasCustomLayout()
    {
        return !Mathf.Approximately(offset.x, 0f) ||
               !Mathf.Approximately(offset.y, 0f) ||
               !Mathf.Approximately(scale.x, 1f) ||
               !Mathf.Approximately(scale.y, 1f) ||
               !Mathf.Approximately(scale.z, 1f);
    }

    public void Normalize()
    {
        scale.x = Mathf.Approximately(scale.x, 0f) ? 1f : scale.x;
        scale.y = Mathf.Approximately(scale.y, 0f) ? 1f : scale.y;
        scale.z = Mathf.Approximately(scale.z, 0f) ? 1f : scale.z;
    }
}

[Serializable]
public class CharacterStoryPositionLayout
{
    public StoryPositionLayout all = new StoryPositionLayout();
    public StoryPositionLayout left = new StoryPositionLayout();
    public StoryPositionLayout center = new StoryPositionLayout();
    public StoryPositionLayout right = new StoryPositionLayout();

    public StoryPositionLayout GetCombinedLayout(CharacterPosition position)
    {
        StoryPositionLayout result = new StoryPositionLayout();
        bool hasLayout = false;

        Apply(all, result, ref hasLayout);
        Apply(GetPositionLayout(position), result, ref hasLayout);

        return hasLayout ? result : null;
    }

    public void Normalize()
    {
        all ??= new StoryPositionLayout();
        left ??= new StoryPositionLayout();
        center ??= new StoryPositionLayout();
        right ??= new StoryPositionLayout();

        all.Normalize();
        left.Normalize();
        center.Normalize();
        right.Normalize();
    }

    StoryPositionLayout GetPositionLayout(CharacterPosition position)
    {
        return position switch
        {
            CharacterPosition.Left => left,
            CharacterPosition.Center => center,
            CharacterPosition.Right => right,
            _ => null
        };
    }

    static void Apply(StoryPositionLayout source, StoryPositionLayout target, ref bool hasLayout)
    {
        if (source == null || !source.HasCustomLayout())
            return;

        source.Normalize();
        target.offset += source.offset;
        target.scale = new Vector3(
            target.scale.x * source.scale.x,
            target.scale.y * source.scale.y,
            target.scale.z * source.scale.z);
        hasLayout = true;
    }
}

[Serializable]
public class CharacterStoryLayerLayout
{
    public StoryLayerLayout body = new StoryLayerLayout();
    public StoryLayerLayout emotion = new StoryLayerLayout();
    public StoryLayerLayout outfit = new StoryLayerLayout();
    public StoryLayerLayout hair = new StoryLayerLayout();
    public StoryLayerLayout accessory = new StoryLayerLayout();

    public StoryLayerLayout GetEquipmentLayout(ClothingType type)
    {
        switch (type)
        {
            case ClothingType.Hair:
                return hair;
            case ClothingType.Accessory:
                return accessory;
            default:
                return outfit;
        }
    }

    public void Normalize()
    {
        body ??= new StoryLayerLayout();
        emotion ??= new StoryLayerLayout();
        outfit ??= new StoryLayerLayout();
        hair ??= new StoryLayerLayout();
        accessory ??= new StoryLayerLayout();

        body.Normalize();
        emotion.Normalize();
        outfit.Normalize();
        hair.Normalize();
        accessory.Normalize();
    }
}

[Serializable]
public class CharacterWardrobeLayerLayout
{
    public StoryLayerLayout body = new StoryLayerLayout();
    public StoryLayerLayout outfit = new StoryLayerLayout();
    public StoryLayerLayout hair = new StoryLayerLayout();
    public StoryLayerLayout accessory = new StoryLayerLayout();

    public StoryLayerLayout GetEquipmentLayout(ClothingType type)
    {
        switch (type)
        {
            case ClothingType.Hair:
                return hair;
            case ClothingType.Accessory:
                return accessory;
            default:
                return outfit;
        }
    }

    public void Normalize()
    {
        body ??= new StoryLayerLayout();
        outfit ??= new StoryLayerLayout();
        hair ??= new StoryLayerLayout();
        accessory ??= new StoryLayerLayout();

        body.Normalize();
        outfit.Normalize();
        hair.Normalize();
        accessory.Normalize();
    }
}

[Serializable]
public class CharacterEquipmentStoryLayout
{
    [Tooltip("Конкретный предмет одежды или волос. Если оставить пустым, настройка станет запасной для выбранного типа предмета.")]
    public ClothingItem item;
    public ClothingType type = ClothingType.Outfit;
    public bool anyAppearance = true;
    public AppearanceType appearanceType = AppearanceType.Default;
    public StoryLayerLayout layout = new StoryLayerLayout();

    public bool Matches(ClothingItem candidate, ClothingType targetType, AppearanceType currentAppearance)
    {
        if (layout == null || !layout.HasCustomLayout())
            return false;

        ClothingType entryType = item != null ? item.type : type;
        if (entryType != targetType)
            return false;

        if (candidate != null && candidate.type != targetType)
            return false;

        if (item != null && item != candidate)
            return false;

        if (!anyAppearance && appearanceType != currentAppearance)
            return false;

        return true;
    }

    public int Specificity()
    {
        int score = 0;
        if (item != null)
            score += 10;
        if (!anyAppearance)
            score += 1;
        return score;
    }

    public void Normalize()
    {
        if (item != null)
            type = item.type;

        layout ??= new StoryLayerLayout();
        layout.Normalize();
    }
}

[Serializable]
public class CharacterEquipmentWardrobeLayout
{
    [Tooltip("Конкретный предмет одежды или волос. Если оставить пустым, настройка станет запасной для выбранного типа предмета.")]
    public ClothingItem item;
    public ClothingType type = ClothingType.Outfit;
    public bool anyAppearance = true;
    public AppearanceType appearanceType = AppearanceType.Default;
    public StoryLayerLayout layout = new StoryLayerLayout();

    public bool Matches(ClothingItem candidate, ClothingType targetType, AppearanceType currentAppearance)
    {
        if (layout == null || !layout.HasCustomLayout())
            return false;

        ClothingType entryType = item != null ? item.type : type;
        if (entryType != targetType)
            return false;

        if (candidate != null && candidate.type != targetType)
            return false;

        if (item != null && item != candidate)
            return false;

        if (!anyAppearance && appearanceType != currentAppearance)
            return false;

        return true;
    }

    public int Specificity()
    {
        int score = 0;
        if (item != null)
            score += 10;
        if (!anyAppearance)
            score += 1;
        return score;
    }

    public void Normalize()
    {
        if (item != null)
            type = item.type;

        appearanceType = HeroCustomizationState.NormalizeAppearance(appearanceType);
        layout ??= new StoryLayerLayout();
        layout.Normalize();
    }
}
