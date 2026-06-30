using UnityEngine;

public class NameChoiceNode : BaseStoryNode
{
    [TextArea(1, 3)]
    public string promptText = "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u0438\u043c\u044f \u0433\u0435\u0440\u043e\u0438\u043d\u0438";
    public string defaultName = "\u0410\u043b\u0438\u0441\u0430";
    public bool forceShow = true;
}
