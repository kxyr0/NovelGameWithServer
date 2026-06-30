#if UNITY_EDITOR
using System;
using System.Collections.Generic;

[Serializable]
public sealed class ParsedStoryNodeData
{
    public string type;
    public List<ParsedDialogueLineData> lines = new List<ParsedDialogueLineData>();
    public string choicePrompt;
    public List<ParsedChoiceOptionData> choices = new List<ParsedChoiceOptionData>();
    public string statId;
    public int statDelta;
    public string statDisplayName;
}
#endif
