#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DeploymentReadinessMenu
{
    private const string ReportDirectory = "Library/DeploymentReadiness";
    private const string ReportFileName = "deployment-readiness-report.md";

    [MenuItem("VN/Выкладка/Отчёт готовности", priority = 31)]
    public static void Generate()
    {
        DeploymentReadinessReport report = DeploymentReadinessScanner.Scan();
        string path = Write(report);
        string message = report.IsReady
            ? "Отчёт готовности создан."
            : "Отчёт готовности создан, но есть проблемы.";

        EditorGUIUtility.systemCopyBuffer = Path.GetFullPath(path);
        Debug.Log("[DeploymentReadiness] " + message + " " + path);
        EditorUtility.DisplayDialog("Готовность к выкладке", message + "\n\nПуть скопирован в буфер:\n" + path, "OK");
    }

    public static string Write(DeploymentReadinessReport report)
    {
        Directory.CreateDirectory(ReportDirectory);
        string path = Path.Combine(ReportDirectory, ReportFileName);
        File.WriteAllText(path, report.ToMarkdown());
        return path;
    }
}
#endif
