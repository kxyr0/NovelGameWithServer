#if UNITY_EDITOR
using System.IO;
using UnityEditor;

public static class ContentReleaseUploadPlanMenu
{
    [MenuItem("VN/Выкладка/План загрузки Тест", priority = 32)]
    public static void GenerateStage()
    {
        Generate(DeploymentEnvironmentIds.Stage);
    }

    [MenuItem("VN/Выкладка/План загрузки Прод", priority = 33)]
    public static void GenerateProduction()
    {
        Generate(DeploymentEnvironmentIds.Production);
    }

    private static void Generate(string environmentId)
    {
        if (!ContentReleaseUploadPlanBuilder.TryWrite(
                environmentId,
                out string jsonPath,
                out string markdownPath,
                out string error))
        {
            EditorUtility.DisplayDialog("План загрузки Addressables", error, "OK");
            return;
        }

        string markdownFullPath = Path.GetFullPath(markdownPath);
        string jsonFullPath = Path.GetFullPath(jsonPath);
        EditorGUIUtility.systemCopyBuffer = markdownFullPath;
        EditorUtility.DisplayDialog(
            "План загрузки Addressables",
            "План загрузки создан.\n\nАбсолютный путь к Markdown скопирован в буфер:\n" +
            markdownFullPath + "\n\nJSON:\n" + jsonFullPath +
            "\n\nОткройте Markdown и загрузите файлы по указанным адресам назначения.",
            "OK");
    }
}
#endif
