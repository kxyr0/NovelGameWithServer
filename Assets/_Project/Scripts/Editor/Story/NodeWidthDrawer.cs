using XNodeEditor;

[CustomNodeEditor(typeof(BaseStoryNode))]
public class NodeWidthDrawer : NodeEditor
{
    public override int GetWidth()
    {
        return 400;
    }
}
