using System;
using UnityEngine;

[Serializable]
public class CharacterEmotion
{
    public CharacterEmotionType emotion;
    public Sprite sprite;

    [Header("Preview Layout")]
    public Vector2 previewOffset;
    [Min(0f)] public float previewWidth;
    [Min(0f)] public float previewHeight;
    public bool previewPreserveAspect = true;

    [Header("Story Layer Layout")]
    public StoryLayerLayout storyLayout = new StoryLayerLayout();

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
        storyLayout ??= new StoryLayerLayout();
        storyLayout.Normalize();
    }
}

/// <summary>
/// Слой эмоции для режима слоевых эмоций (useLayeredEmotions = true).
/// Содержит только спрайт лица/мимики — без тела.
/// </summary>
[Serializable]
public class CharacterEmotionLayer
{
    public CharacterEmotionType emotion;
    [Tooltip("Спрайт лица или мимики для этой эмоции. В слоевом режиме накладывается поверх тела.")]
    public Sprite faceSprite;

    [Header("Preview Layout")]
    public Vector2 previewOffset;
    [Min(0f)] public float previewWidth;
    [Min(0f)] public float previewHeight;
    public bool previewPreserveAspect = true;

    [Header("Story Layer Layout")]
    public StoryLayerLayout storyLayout = new StoryLayerLayout();

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
        storyLayout ??= new StoryLayerLayout();
        storyLayout.Normalize();
    }
}
