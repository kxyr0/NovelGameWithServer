#if UNITY_EDITOR
using System;
using System.Collections.Generic;

[Serializable]
public sealed class ParsedChoiceOptionData
{
    public string text;
    public List<ParsedStoryNodeData> branch = new List<ParsedStoryNodeData>();
}
#endif
