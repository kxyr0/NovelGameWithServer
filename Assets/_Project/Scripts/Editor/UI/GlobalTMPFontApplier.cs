using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public sealed class GlobalTMPFontApplier : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private bool applyOnValidate = true;
    [SerializeField] private bool includeInactive = true;

#if UNITY_EDITOR
    bool _queued;
#endif

    void OnValidate()
    {
        if (!applyOnValidate || font == null)
            return;

#if UNITY_EDITOR
        QueueEditorApply();
#else
        ApplyFontToAllTexts();
#endif
    }

    [ContextMenu("Apply Font To All TMP Texts")]
    public void ApplyFontToAllTexts()
    {
        if (font == null)
            return;

        TMP_Text[] texts = FindObjectsOfType<TMP_Text>(includeInactive);
        foreach (TMP_Text text in texts)
        {
            if (text == null || text.font == font)
                continue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.RecordObject(text, "Apply TMP Font");
#endif

            text.font = font;
            text.SetAllDirty();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(text);
#endif
        }

#if UNITY_EDITOR
        if (!Application.isPlaying && gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

#if UNITY_EDITOR
    void QueueEditorApply()
    {
        if (_queued)
            return;

        _queued = true;
        EditorApplication.delayCall += ApplyQueued;
    }

    void ApplyQueued()
    {
        _queued = false;

        if (this == null || Application.isPlaying)
            return;

        ApplyFontToAllTexts();
    }
#endif
}
