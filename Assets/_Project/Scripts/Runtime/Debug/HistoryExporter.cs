using System;
using System.IO;
using UnityEngine;

public class HistoryExporter : MonoBehaviour
{
    public void ExportToText()
    {
        var history = GameState.Instance != null ? GameState.Instance.history : null;
        string content = history != null ? string.Join("\n", history) : string.Empty;
        string path = Path.Combine(Application.persistentDataPath, "history.txt");

        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(path, content);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"HistoryExporter: failed to export history to '{path}': {exception.Message}", this);
        }
    }
}
