using System.Collections.Generic;
using UnityEngine;

public class DialogueNode : BaseStoryNode
{
    [TextArea] public string nodeTitle;

    public List<DialogueCharacterEntry> activeCharacters = new List<DialogueCharacterEntry>(3);

    public List<DialogueLine> lines = new List<DialogueLine>();
}