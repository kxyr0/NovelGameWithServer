using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ChoiceOption
{
    public string text;
    public bool isPremium;
    public int premiumCost;
    public string requiredVariable;
    public int requiredValue;
    public bool hideWhenRequirementNotMet;
    public bool hideInRestrictedRegions;
    public List<string> hiddenRegionCodes = new List<string>();
}
