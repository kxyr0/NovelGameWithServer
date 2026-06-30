using XNode;
using UnityEngine;

public class WardrobeCheckNode : BaseStoryNode
{
    public string itemId;

    [Output] public BaseStoryNode hasItem;
    [Output] public BaseStoryNode noItem;
}