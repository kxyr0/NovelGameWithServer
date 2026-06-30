using System;
using System.Collections.Generic;

[Serializable]
public sealed class RemoteChoiceOptionDto
{
    public string text;
    public bool isPremium;
    public int premiumCost;
    public string requiredVariable;
    public int requiredValue;
    public bool hideInRestrictedRegions;
    public List<string> hiddenRegionCodes = new List<string>();
    public List<RemoteNodeDto> branch = new List<RemoteNodeDto>();
}
