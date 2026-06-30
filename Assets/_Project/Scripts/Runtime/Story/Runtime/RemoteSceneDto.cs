using System;
using System.Collections.Generic;

[Serializable]
public sealed class RemoteSceneDto
{
    public string sceneDescription;
    public string suggestedBackground;
    public string suggestedMusic;
    public List<RemoteNodeDto> nodes = new List<RemoteNodeDto>();
}
