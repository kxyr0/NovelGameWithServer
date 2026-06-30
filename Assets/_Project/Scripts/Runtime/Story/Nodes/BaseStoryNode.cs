using XNode;
using UnityEngine;
using System;

public abstract class BaseStoryNode : Node
{
    [Input] public BaseStoryNode enter;
    [Output] public BaseStoryNode exit;

    [HideInInspector] public string guid = Guid.NewGuid().ToString();

    public override object GetValue(NodePort port) { return null; }
}