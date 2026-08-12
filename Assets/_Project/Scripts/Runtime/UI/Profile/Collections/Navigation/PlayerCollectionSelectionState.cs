using System;
using UnityEngine;

public static class PlayerCollectionSelectionState
{
    public static event Action Changed;

    public static PlayerCollectionItemDefinition CurrentItem { get; private set; }
    public static Sprite CurrentImage { get; private set; }
    public static bool HasSelection => CurrentItem != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        CurrentItem = null;
        CurrentImage = null;
        Changed = null;
    }

    public static void Select(PlayerCollectionItemDefinition item)
    {
        CurrentItem = item;
        CurrentImage = item != null
            ? item.ResolveImage(PlayerCollectionState.GetCollectedImageId(item))
            : null;
        Changed?.Invoke();
    }

    public static void Clear()
    {
        if (!HasSelection && CurrentImage == null)
            return;

        CurrentItem = null;
        CurrentImage = null;
        Changed?.Invoke();
    }
}
