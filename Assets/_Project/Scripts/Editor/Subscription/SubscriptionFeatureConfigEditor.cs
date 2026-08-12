#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SubscriptionFeatureConfig))]
public sealed class SubscriptionFeatureConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var config = (SubscriptionFeatureConfig)target;
        string label = config.FeaturesEnabled
            ? "Disable Subscription Features"
            : "Enable Subscription Features";

        GUILayout.Space(8);
        if (!GUILayout.Button(label, GUILayout.Height(32)))
            return;

        Undo.RecordObject(config, label);
        config.SetFeaturesEnabledForEditor(!config.FeaturesEnabled);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
    }
}
#endif
