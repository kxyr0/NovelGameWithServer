#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class NetworkRuntimeConfigFile
{
    public const string ResourceAssetPath = "Assets/Resources/NovelTemplate/network-runtime-config.json";
    public const string StageTemplatePath = "Assets/_Project/Configs/Deployment/network-runtime-config.stage.json";
    public const string ProductionTemplatePath = "Assets/_Project/Configs/Deployment/network-runtime-config.prod.json";

    public static NetworkRuntimeConfigData LoadOrDefault()
    {
        if (!File.Exists(ResourceAssetPath))
            return new NetworkRuntimeConfigData();

        string json = File.ReadAllText(ResourceAssetPath);
        var config = JsonUtility.FromJson<NetworkRuntimeConfigData>(json);
        return config ?? new NetworkRuntimeConfigData();
    }

    public static void Save(NetworkRuntimeConfigData config, string path = ResourceAssetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(config, true));
        AssetDatabase.ImportAsset(path);
        NetworkRuntimeConfigLoader.ResetCache();
    }
}
#endif
