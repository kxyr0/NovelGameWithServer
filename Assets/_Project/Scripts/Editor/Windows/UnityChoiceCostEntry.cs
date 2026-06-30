#if UNITY_EDITOR
using System;

[Serializable]
public sealed class UnityChoiceCostEntry
{
    public string storyId;
    public string episodeId;
    public string chapterId;
    public string nodeGuid;
    public string nodeId;
    public string nodeTitle;
    public int choiceIndex;
    public int optionIndex;
    public int cost;
    public string currency;
    public string choiceText;
    public string itemId;
    public string label;
    public string source;
}
#endif
