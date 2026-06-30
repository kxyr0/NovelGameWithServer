using System;
using System.Collections.Generic;

[Serializable]
public sealed class RemoteNodeDto
{
    public string guid;
    public string type;
    public List<RemoteLineDto> lines = new List<RemoteLineDto>();
    public string choicePrompt;
    public List<RemoteChoiceOptionDto> choices = new List<RemoteChoiceOptionDto>();
    public string statId;
    public int statDelta;
    public string statDisplayName;
}
