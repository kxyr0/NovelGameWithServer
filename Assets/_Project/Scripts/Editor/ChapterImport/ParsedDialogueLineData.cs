#if UNITY_EDITOR
using System;

[Serializable]
public sealed class ParsedDialogueLineData
{
    public string speaker;
    public CharacterData characterData;
    public string emotion;
    public string text;
}
#endif
