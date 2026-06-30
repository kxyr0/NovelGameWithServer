#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameData))]
public sealed class GameDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (targets.Length != 1)
            return;

        DrawStoryLoadingMediaBlock((GameData)target);
    }

    private void DrawStoryLoadingMediaBlock(GameData data)
    {
        if (data == null)
            return;

        StoryLoadingMediaReadinessReport report = StoryLoadingMediaReadinessPolicies.Shared.Evaluate(data);

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Story Loading Media", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(BuildStatusText(data, report), ToMessageType(report.Severity));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Migrate To Addressables"))
                RunMigration(data, overwriteExistingReferences: false, clearDirectFallbacks: false);

            if (GUILayout.Button("Strict Lazy Migrate"))
                RunMigration(data, overwriteExistingReferences: false, clearDirectFallbacks: true);

            if (GUILayout.Button("Force Remigrate"))
                RunMigration(data, overwriteExistingReferences: true, clearDirectFallbacks: false);
        }
    }

    private void RunMigration(GameData data, bool overwriteExistingReferences, bool clearDirectFallbacks)
    {
        if (data == null)
            return;

        if (clearDirectFallbacks)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Strict lazy migration",
                "This will migrate this story's loading image/video/GIF to Addressables and clear direct fallback references only after matching Addressable references exist.",
                "Migrate",
                "Cancel");

            if (!confirmed)
                return;
        }

        serializedObject.ApplyModifiedProperties();
        StoryLoadingMediaAddressablesMigration.MigrationSummary summary =
            StoryLoadingMediaAddressablesMigration.MigrateGameDataAsset(
                data,
                overwriteExistingReferences,
                clearDirectFallbacks);

        serializedObject.Update();
        Repaint();
        EditorUtility.DisplayDialog("Story Loading Media", summary.ToDialogText(), "OK");
    }

    private static MessageType ToMessageType(StoryLoadingMediaReadinessSeverity severity)
    {
        switch (severity)
        {
            case StoryLoadingMediaReadinessSeverity.Error:
                return MessageType.Error;
            case StoryLoadingMediaReadinessSeverity.Warning:
                return MessageType.Warning;
            default:
                return MessageType.Info;
        }
    }

    private static string BuildStatusText(GameData data, StoryLoadingMediaReadinessReport report)
    {
        GameStoryLoadingMediaSettings settings = data != null ? data.LoadingMedia : null;
        bool overrideEnabled = settings != null && settings.OverrideLoadingMedia;
        bool hasAddressables = settings != null && settings.HasAddressableMedia;
        bool hasDirectFallback = settings != null && settings.HasDirectMedia;

        return "Override enabled: " + YesNo(overrideEnabled) + "\n" +
            "Addressable replacements: " + YesNo(hasAddressables) + "\n" +
            "Direct fallback references: " + YesNo(hasDirectFallback) + "\n" +
            "Lazy-load safe: " + YesNo(report.IsLazyLoadSafe) + "\n\n" +
            report.Message;
    }

    private static string YesNo(bool value)
    {
        return value ? "yes" : "no";
    }
}
#endif
