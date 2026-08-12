#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

[Serializable]
public sealed class ContentReleaseUploadPlan
{
    public string generatedAtIso = "";
    public string environmentId = "";
    public string channel = "";
    public string buildTarget = "";
    public string contentVersion = "";
    public string sourceDirectory = "";
    public string destinationRootUrl = "";
    public string uploadMode = "";
    public string uploadRootPath = "";
    public int fileCount;
    public long totalBytes;
    public List<ContentReleaseUploadPlanFile> files = new List<ContentReleaseUploadPlanFile>();

    public string ToMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# План загрузки Addressables");
        builder.AppendLine();
        builder.AppendLine("- Создан: `" + generatedAtIso + "`");
        builder.AppendLine("- Среда: `" + environmentId + "`");
        builder.AppendLine("- Канал: `" + channel + "`");
        builder.AppendLine("- Платформа сборки: `" + buildTarget + "`");
        builder.AppendLine("- Версия контента: `" + contentVersion + "`");
        builder.AppendLine("- Абсолютная папка источника: `" + sourceDirectory + "`");
        builder.AppendLine("- Публичный URL для Unity: `" + destinationRootUrl + "`");
        builder.AppendLine("- Способ загрузки: `" + uploadMode + "`");
        builder.AppendLine("- Путь загрузки файлов: `" + uploadRootPath + "`");
        builder.AppendLine("- Файлов: `" + fileCount + "`, байт: `" + totalBytes + "`");
        builder.AppendLine();
        builder.AppendLine("Загрузите каждый файл из колонки «Абсолютный локальный путь» в место из колонки «Куда положить». Unity будет скачивать его по публичному URL.");
        builder.AppendLine();
        builder.AppendLine("| Файл | Абсолютный локальный путь | Байт | SHA256 | Куда положить | Публичный URL для Unity |");
        builder.AppendLine("| --- | --- | ---: | --- | --- | --- |");

        foreach (ContentReleaseUploadPlanFile file in files)
        {
            builder.Append("| ");
            builder.Append(Escape(file.path));
            builder.Append(" | ");
            builder.Append(Escape(file.sourceAbsolutePath));
            builder.Append(" | ");
            builder.Append(file.bytes);
            builder.Append(" | `");
            builder.Append(file.sha256);
            builder.Append("` | ");
            builder.Append(Escape(file.uploadTargetPath));
            builder.Append(" | ");
            builder.Append(Escape(file.destinationUrl));
            builder.AppendLine(" |");
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return (value ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }
}

[Serializable]
public sealed class ContentReleaseUploadPlanFile
{
    public string path = "";
    public string sourceAbsolutePath = "";
    public long bytes;
    public string sha256 = "";
    public string uploadTargetPath = "";
    public string destinationUrl = "";
}
#endif
