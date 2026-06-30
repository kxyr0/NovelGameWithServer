using UnityEditor;
using UnityEngine;

public static class RuntimeDebugMenu
{
    const string FpsOverlayEditorPrefsKey = "VN.Debug.FpsOverlay.Enabled";
    const string DiagnosticsEditorPrefsKey = "VN.Debug.RuntimeDiagnostics.Enabled";

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        bool enabled = EditorPrefs.GetBool(FpsOverlayEditorPrefsKey, false);
        PlayerPrefs.SetInt(RuntimePerformanceDiagnostics.FpsOverlayPlayerPrefsKey, enabled ? 1 : 0);
        bool diagnosticsEnabled = EditorPrefs.GetBool(DiagnosticsEditorPrefsKey, false);
        PlayerPrefs.SetInt(RuntimePerformanceDiagnostics.DiagnosticsPlayerPrefsKey, diagnosticsEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    [MenuItem("Tools/Debug/Enable FPS Overlay")]
    public static void EnableFpsOverlay()
    {
        SetFpsOverlay(true);
    }

    [MenuItem("Tools/Debug/Disable FPS Overlay")]
    public static void DisableFpsOverlay()
    {
        SetFpsOverlay(false);
    }

    [MenuItem("Tools/Debug/Toggle FPS Overlay")]
    public static void ToggleFpsOverlay()
    {
        SetFpsOverlay(!EditorPrefs.GetBool(FpsOverlayEditorPrefsKey, false));
    }

    [MenuItem("Tools/Debug/Dump Runtime Diagnostics")]
    public static void DumpRuntimeDiagnostics()
    {
        RuntimePerformanceDiagnostics.DumpReport(true);
    }

    [MenuItem("Tools/Debug/Enable Runtime Diagnostics")]
    public static void EnableRuntimeDiagnostics()
    {
        SetRuntimeDiagnostics(true);
    }

    [MenuItem("Tools/Debug/Disable Runtime Diagnostics")]
    public static void DisableRuntimeDiagnostics()
    {
        SetRuntimeDiagnostics(false);
    }

    [MenuItem("Tools/Debug/Enable FPS Overlay", true)]
    static bool ValidateEnableFpsOverlay()
    {
        Menu.SetChecked("Tools/Debug/Enable FPS Overlay", EditorPrefs.GetBool(FpsOverlayEditorPrefsKey, false));
        return true;
    }

    [MenuItem("Tools/Debug/Disable FPS Overlay", true)]
    static bool ValidateDisableFpsOverlay()
    {
        Menu.SetChecked("Tools/Debug/Disable FPS Overlay", !EditorPrefs.GetBool(FpsOverlayEditorPrefsKey, false));
        return true;
    }

    [MenuItem("Tools/Debug/Toggle FPS Overlay", true)]
    static bool ValidateToggleFpsOverlay()
    {
        Menu.SetChecked("Tools/Debug/Toggle FPS Overlay", EditorPrefs.GetBool(FpsOverlayEditorPrefsKey, false));
        return true;
    }

    [MenuItem("Tools/Debug/Enable Runtime Diagnostics", true)]
    static bool ValidateEnableRuntimeDiagnostics()
    {
        Menu.SetChecked("Tools/Debug/Enable Runtime Diagnostics", EditorPrefs.GetBool(DiagnosticsEditorPrefsKey, false));
        return true;
    }

    [MenuItem("Tools/Debug/Disable Runtime Diagnostics", true)]
    static bool ValidateDisableRuntimeDiagnostics()
    {
        Menu.SetChecked("Tools/Debug/Disable Runtime Diagnostics", !EditorPrefs.GetBool(DiagnosticsEditorPrefsKey, false));
        return true;
    }

    static void SetFpsOverlay(bool enabled)
    {
        EditorPrefs.SetBool(FpsOverlayEditorPrefsKey, enabled);
        RuntimePerformanceDiagnostics.SetFpsOverlayEnabled(enabled);
        Debug.Log("FPS overlay " + (enabled ? "enabled" : "disabled") + ".");
    }

    static void SetRuntimeDiagnostics(bool enabled)
    {
        EditorPrefs.SetBool(DiagnosticsEditorPrefsKey, enabled);
        RuntimePerformanceDiagnostics.SetDiagnosticsEnabled(enabled);
        Debug.Log("Runtime diagnostics " + (enabled ? "enabled" : "disabled") + ".");
    }
}
