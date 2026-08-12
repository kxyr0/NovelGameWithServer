#if UNITY_EDITOR
using System.Collections.Generic;

internal enum AuthorInkExitKind
{
    Next,
    Choice,
    True,
    False,
    Appearance,
    Wardrobe
}

internal sealed class AuthorInkExitRef
{
    public StoryJsonNode Node;
    public AuthorInkExitKind Kind;
    public int Index;

    public AuthorInkExitRef(StoryJsonNode node, AuthorInkExitKind kind, int index = -1)
    {
        Node = node;
        Kind = kind;
        Index = index;
    }

    public void Connect(string targetId)
    {
        if (Node == null)
            return;

        switch (Kind)
        {
            case AuthorInkExitKind.Choice:
                if (Node.choices != null && Index >= 0 && Index < Node.choices.Count)
                    Node.choices[Index].next = targetId;
                break;
            case AuthorInkExitKind.True:
                Node.trueNext = targetId;
                break;
            case AuthorInkExitKind.False:
                Node.falseNext = targetId;
                break;
            case AuthorInkExitKind.Appearance:
                if (Node.appearanceOptions != null && Index >= 0 && Index < Node.appearanceOptions.Count)
                    Node.appearanceOptions[Index].next = targetId;
                break;
            case AuthorInkExitKind.Wardrobe:
                while (Node.exits.Count <= Index)
                    Node.exits.Add("");
                Node.exits[Index] = targetId;
                break;
            default:
                Node.next = targetId;
                break;
        }
    }
}

internal sealed class AuthorInkFlowCursor
{
    public readonly List<AuthorInkExitRef> Open = new List<AuthorInkExitRef>();
    public readonly List<string> PendingAnchors = new List<string>();
}

internal sealed class AuthorInkPendingRoute
{
    public AuthorInkExitRef Exit;
    public string Target;
    public int Line;
}
#endif
