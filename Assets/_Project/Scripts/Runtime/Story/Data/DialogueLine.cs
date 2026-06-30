using System;
using UnityEngine;

[Serializable]
public class DialogueLine
{
    [HideInInspector] public string speakerId;
    [HideInInspector] public string speakerNameHint;
    public CharacterData speaker;
    public CharacterEmotionType emotion;
    [TextArea(3, 10)]
    public string richText;
    public DialogueStyle style;
    [TextArea(2, 5)]
    public string authorComment;
}
