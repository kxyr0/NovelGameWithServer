#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DeploymentCliVerifier
{
    public const string ReportPath = "Library/NocturnalProjectVerification.txt";

    [MenuItem("VN/Выкладка/Проверить проект Nocturnal", priority = 35)]
    public static void RunFromMenu()
    {
        RunNow();
    }

    public static int RunNow()
    {
        return RunChecks();
    }

    public static void RunForBatchMode()
    {
        int failures = RunChecks();
        if (Application.isBatchMode)
            EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static int RunChecks()
    {
        var lines = new List<string>();
        int failures = 0;
        DeploymentCliBackendChecks.Run(lines, ref failures);
        DeploymentCliToolingChecks.Run(lines, ref failures);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllLines(ReportPath, lines.ToArray());
        string summary = "Проверка проекта Nocturnal: " + (failures == 0 ? "OK" : "ОШИБОК " + failures);
        if (failures == 0)
            Debug.Log(summary + "\n" + ReportPath);
        else
            Debug.LogError(summary + "\n" + string.Join("\n", lines));
        return failures;
    }
}
#endif
