using UnityEngine;

[CreateAssetMenu(menuName = "VN/Wardrobe Item")]
public class WardrobeItem : ScriptableObject
{
    public string itemId;
    public string displayName;
    public Sprite icon;
    public int unlockCost;
}