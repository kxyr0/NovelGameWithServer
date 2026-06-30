using System;
using UnityEngine;

[Serializable]
public class ChoiceData
{
    public string text;
    public string nextNodeGuid;
    public bool isPremium;
    public int premiumCost;
    public string requiredVariable;
    public int requiredValue;
}