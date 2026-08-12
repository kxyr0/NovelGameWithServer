using System;
using UnityEngine;

public partial class SaveManager
{
    public static event Action<string> OnStorySaveChanged;

    internal static void NotifyStorySaveChanged(string storyId)
    {
        try
        {
            OnStorySaveChanged?.Invoke(SaveDataSanitizer.SanitizeIdentifier(storyId));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStorySaveEvents()
    {
        OnStorySaveChanged = null;
    }
}
