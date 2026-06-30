using UnityEngine;

[CreateAssetMenu(menuName = "VN/Dialogue Style")]
public class DialogueStyle : ScriptableObject
{
    public Font font;
    public int fontSize = 36;
    public Color fontColor = Color.white;
    public Sprite backgroundSprite;
}