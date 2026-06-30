using XNode;

public class PremiumNode : BaseStoryNode
{
    public int cost;

    [Output] public BaseStoryNode successNode;
    [Output] public BaseStoryNode failNode;
}