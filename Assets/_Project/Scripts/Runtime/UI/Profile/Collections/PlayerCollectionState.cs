using System;
using UnityEngine;

public static class PlayerCollectionState
{
    private const string PrefsPrefix = "nocturne.collection.owned.";
    private const string ImageSuffix = ".image";

    public static event Action Changed;

    public static bool IsOwned(PlayerCollectionItemDefinition item)
    {
        return item != null && IsOwned(item.StorageId);
    }

    public static bool IsOwned(string storageId)
    {
        return !string.IsNullOrWhiteSpace(storageId) &&
               PlayerPrefs.GetInt(PrefsPrefix + storageId, 0) == 1;
    }

    public static bool GrantMoment(string storyId, string cutsceneNodeId)
    {
        return GrantMoment(storyId, cutsceneNodeId, null);
    }

    public static bool GrantMoment(
        string storyId, string cutsceneNodeId, Sprite displayedImage)
    {
        string storageId = PlayerCollectionItemDefinition.BuildMomentStorageId(
            storyId, cutsceneNodeId);
        return Grant(storageId, displayedImage != null ? displayedImage.name : "");
    }

    public static bool GrantCard(string cardId)
    {
        return Grant(PlayerCollectionItemDefinition.BuildCardStorageId(cardId), "");
    }

    public static string GetCollectedImageId(PlayerCollectionItemDefinition item)
    {
        if (item == null || string.IsNullOrEmpty(item.StorageId))
            return string.Empty;

        return PlayerPrefs.GetString(
            PrefsPrefix + item.StorageId + ImageSuffix, string.Empty);
    }

    public static bool TryUnlockStoryNode(string storyId, BaseStoryNode node)
    {
        if (node is ImageNode imageNode)
            return GrantMoment(storyId, node.guid, imageNode.image);

        if (node is CutsceneNode cutsceneNode)
            return GrantMoment(storyId, node.guid, cutsceneNode.image);

        return false;
    }

    private static bool Grant(string storageId, string imageId)
    {
        if (string.IsNullOrEmpty(storageId))
            return false;

        bool newlyOwned = !IsOwned(storageId);
        string imageKey = PrefsPrefix + storageId + ImageSuffix;
        bool imageChanged = !string.IsNullOrWhiteSpace(imageId) &&
            !string.Equals(PlayerPrefs.GetString(imageKey, ""), imageId,
                StringComparison.Ordinal);

        if (!newlyOwned && !imageChanged)
            return false;

        if (newlyOwned)
            PlayerPrefs.SetInt(PrefsPrefix + storageId, 1);
        if (imageChanged)
            PlayerPrefs.SetString(imageKey, imageId);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return newlyOwned;
    }
}
