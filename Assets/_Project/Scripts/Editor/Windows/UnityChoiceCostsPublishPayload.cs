#if UNITY_EDITOR
using System;
using System.Collections.Generic;

[Serializable]
public sealed class UnityChoiceCostsPublishPayload
{
    public string storyId;
    public string episodeId;
    public string source;
    public string generatedAt;
    public List<UnityChoiceCostEntry> costs = new List<UnityChoiceCostEntry>();
    public List<UnityChoiceCostEntry> choices = new List<UnityChoiceCostEntry>();
    public List<UnityChoiceCostEntry> items = new List<UnityChoiceCostEntry>();
    public List<UnityChoiceCostEntry> choiceCosts = new List<UnityChoiceCostEntry>();
}
#endif
