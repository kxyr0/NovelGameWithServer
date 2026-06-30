using UnityEngine;

[CreateAssetMenu(menuName = "VN/Stat")]
public class StatDefinition : ScriptableObject
{
    public string statId;
    public string displayName;
    public int order;
    public Sprite icon;
}