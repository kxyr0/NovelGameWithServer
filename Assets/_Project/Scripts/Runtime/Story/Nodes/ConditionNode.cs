using UnityEngine;
using XNode;

public enum ConditionComparison
{
    Equals = 0,
    NotEquals = 1,
    GreaterThan = 2,
    GreaterOrEqual = 3,
    LessThan = 4,
    LessOrEqual = 5
}

public class ConditionNode : BaseStoryNode
{
    public string variableKey;
    public ConditionComparison comparison = ConditionComparison.Equals;
    public string compareVariableKey;
    public int requiredValue;

    [Output] public BaseStoryNode trueExit;
    [Output] public BaseStoryNode falseExit;
}
