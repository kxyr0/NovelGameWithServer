using System.Collections.Generic;
using UnityEngine;
using XNode;

public class ChoiceNode : BaseStoryNode
{
    [TextArea] public string nodeTitle;

    public List<DialogueCharacterEntry> activeCharacters = new List<DialogueCharacterEntry>(3);

    public List<DialogueLine> lines = new List<DialogueLine>();

    public List<ChoiceOption> options = new List<ChoiceOption>();

    [Output(dynamicPortList = true)]
    public List<BaseStoryNode> choices;
}