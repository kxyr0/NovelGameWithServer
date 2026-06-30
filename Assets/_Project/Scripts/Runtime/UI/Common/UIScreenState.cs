using System;
using System.Collections.Generic;
using UnityEngine;

public static class UIScreenState
{
    private static readonly List<string> _activeScreens = new List<string>();
    private static readonly Dictionary<string, int> _screenRefCounts = new Dictionary<string, int>(StringComparer.Ordinal);

    public static string CurrentScreenId { get; private set; } = "";
    public static string SelectedScreenId { get; private set; } = "";
    public static event Action<string> CurrentScreenChanged;
    public static event Action<string> SelectedScreenChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        _activeScreens.Clear();
        _screenRefCounts.Clear();
        CurrentScreenId = "";
        SelectedScreenId = "";
        CurrentScreenChanged = null;
        SelectedScreenChanged = null;
    }

    public static void RegisterScreen(string screenId)
    {
        screenId = NormalizeScreenId(screenId);
        if (screenId.Length == 0)
            return;

        _screenRefCounts.TryGetValue(screenId, out int count);
        _screenRefCounts[screenId] = count + 1;

        _activeScreens.Remove(screenId);
        _activeScreens.Add(screenId);
        SetCurrent(screenId);
    }

    public static void UnregisterScreen(string screenId)
    {
        screenId = NormalizeScreenId(screenId);
        if (screenId.Length == 0)
            return;

        if (!_screenRefCounts.TryGetValue(screenId, out int count) || count <= 1)
        {
            _screenRefCounts.Remove(screenId);
            _activeScreens.Remove(screenId);
        }
        else
        {
            _screenRefCounts[screenId] = count - 1;
        }

        string nextScreen = _activeScreens.Count > 0
            ? _activeScreens[_activeScreens.Count - 1]
            : "";

        SetCurrent(nextScreen);
    }

    public static bool IsCurrent(string screenId)
    {
        screenId = NormalizeScreenId(screenId);
        return screenId.Length > 0 && CurrentScreenId == screenId;
    }

    public static bool IsSelected(string screenId)
    {
        screenId = NormalizeScreenId(screenId);
        return screenId.Length > 0 && SelectedScreenId == screenId;
    }

    public static void SetSelectedScreen(string screenId)
    {
        screenId = NormalizeScreenId(screenId);
        if (SelectedScreenId == screenId)
            return;

        SelectedScreenId = screenId;
        SelectedScreenChanged?.Invoke(SelectedScreenId);
    }

    public static void ClearSelectedScreen()
    {
        SetSelectedScreen("");
    }

    public static void SetCurrentScreen(string screenId)
    {
        screenId = NormalizeScreenId(screenId);
        _activeScreens.Clear();
        _screenRefCounts.Clear();

        if (screenId.Length > 0)
        {
            _activeScreens.Add(screenId);
            _screenRefCounts[screenId] = 1;
        }

        SetCurrent(screenId);
    }

    private static void SetCurrent(string screenId)
    {
        screenId = NormalizeScreenId(screenId);
        if (CurrentScreenId == screenId)
            return;

        CurrentScreenId = screenId;
        CurrentScreenChanged?.Invoke(CurrentScreenId);
#if UNITY_EDITOR
        NotifyEditorStoryManagers(CurrentScreenId);
#endif
    }

    public static string NormalizeScreenId(string screenId)
    {
        return string.IsNullOrWhiteSpace(screenId) ? "" : screenId.Trim();
    }

#if UNITY_EDITOR
    private static void NotifyEditorStoryManagers(string screenId)
    {
        if (Application.isPlaying)
            return;

        StoryManager[] managers = Resources.FindObjectsOfTypeAll<StoryManager>();
        for (int i = 0; i < managers.Length; i++)
        {
            StoryManager manager = managers[i];
            if (manager != null)
                manager.HandleEditorScreenStateChanged(screenId);
        }
    }
#endif
}
