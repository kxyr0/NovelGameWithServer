#if UNITY_EDITOR
using System;
using System.Collections.Generic;

[Serializable]
public sealed class ParsedSceneData
{
    public string sceneDescription;
    public string suggestedBackground;
    public string suggestedMusic;
    public List<ParsedStoryNodeData> nodes = new List<ParsedStoryNodeData>();
}
#endif
