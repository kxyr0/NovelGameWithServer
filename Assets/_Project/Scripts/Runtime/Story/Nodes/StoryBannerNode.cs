using UnityEngine;

public class StoryBannerNode : BaseStoryNode
{
    [TextArea(2, 5)]
    public string message = "";
    public bool waitForCompletion = true;
    [Min(0f)]
    public float fallbackDuration = 2f;
}
