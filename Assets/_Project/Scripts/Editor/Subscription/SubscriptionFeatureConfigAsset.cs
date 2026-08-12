#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SubscriptionFeatureConfigAsset
{
    const string ResourcesFolder = "Assets/_Project/Resources";
    const string AssetPath = ResourcesFolder + "/SubscriptionFeatureConfig.asset";

    [MenuItem("VN/Subscription/Select Feature Config", priority = 10)]
    public static void SelectOrCreate()
    {
        Selection.activeObject = EnsureAsset();
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    public static void EnsureAssetForBatch()
    {
        EnsureAsset();
    }

    public static SubscriptionFeatureConfig EnsureAsset()
    {
        SubscriptionFeatureConfig existing = AssetDatabase.LoadAssetAtPath<SubscriptionFeatureConfig>(AssetPath);
        if (existing != null)
            return existing;

        EnsureFolder(ResourcesFolder);
        var asset = ScriptableObject.CreateInstance<SubscriptionFeatureConfig>();
        AssetDatabase.CreateAsset(asset, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return asset;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
