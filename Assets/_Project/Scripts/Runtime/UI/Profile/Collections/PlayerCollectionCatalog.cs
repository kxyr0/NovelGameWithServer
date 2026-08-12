using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerCollectionKind
{
    Moment,
    Card
}

[Serializable]
public sealed class PlayerCollectionItemDefinition
{
    [SerializeField] private PlayerCollectionKind _kind;
    [SerializeField, Tooltip("Stable ID used when a card is granted.")]
    private string _itemId;
    [SerializeField, Tooltip("Story ID for a moment, for example only_the_heart_sees_clearly.")]
    private string _storyId;
    [SerializeField, Tooltip("Cutscene node ID from story JSON, for example zls1_image_001.")]
    private string _sourceNodeId;
    [SerializeField] private Sprite _image;
    [SerializeField, Tooltip("All possible covers for a character-dependent cutscene.")]
    private List<Sprite> _imageVariants = new List<Sprite>();
    [SerializeField] private string _title;
    [SerializeField] private string _storyTitle;

    public PlayerCollectionKind Kind => _kind;
    public Sprite Image => _image;
    public string Title => _title ?? string.Empty;
    public string StoryTitle => _storyTitle ?? string.Empty;
    public string StorageId => _kind == PlayerCollectionKind.Moment
        ? BuildMomentStorageId(_storyId, _sourceNodeId)
        : BuildCardStorageId(_itemId);
    public bool IsConfigured => !string.IsNullOrEmpty(StorageId);

    public Sprite ResolveImage(string collectedImageId)
    {
        if (MatchesImage(_image, collectedImageId))
            return _image;

        for (int i = 0; _imageVariants != null && i < _imageVariants.Count; i++)
        {
            if (MatchesImage(_imageVariants[i], collectedImageId))
                return _imageVariants[i];
        }

        return _image;
    }

    public static string BuildMomentStorageId(string storyId, string nodeId)
    {
        storyId = Normalize(storyId);
        nodeId = Normalize(nodeId);
        return string.IsNullOrEmpty(storyId) || string.IsNullOrEmpty(nodeId)
            ? string.Empty
            : $"moment|{storyId}|{nodeId}";
    }

    public static string BuildCardStorageId(string cardId)
    {
        cardId = Normalize(cardId);
        return string.IsNullOrEmpty(cardId) ? string.Empty : $"card|{cardId}";
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static bool MatchesImage(Sprite image, string imageId)
    {
        return image != null && !string.IsNullOrWhiteSpace(imageId) &&
               string.Equals(image.name, imageId.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }
}

[CreateAssetMenu(
    fileName = "PlayerCollectionCatalog",
    menuName = "Nocturne/Profile/Player Collection Catalog")]
public sealed class PlayerCollectionCatalog : ScriptableObject
{
    [SerializeField] private List<PlayerCollectionItemDefinition> _items =
        new List<PlayerCollectionItemDefinition>();

    public IReadOnlyList<PlayerCollectionItemDefinition> Items => _items;
}
