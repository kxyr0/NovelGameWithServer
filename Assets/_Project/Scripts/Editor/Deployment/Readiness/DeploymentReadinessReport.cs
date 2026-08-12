#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

public sealed class DeploymentReadinessReport
{
    public string GeneratedAtIso = DateTime.UtcNow.ToString("o");
    public string BuildTarget = "";
    public readonly List<DeploymentReadinessItem> Items = new List<DeploymentReadinessItem>();

    public int FailCount => Count(DeploymentReadinessStatus.Fail);
    public int WarnCount => Count(DeploymentReadinessStatus.Warn);
    public int PassCount => Count(DeploymentReadinessStatus.Pass);
    public bool IsReady => FailCount == 0;

    public string ToMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Отчёт готовности к выкладке");
        builder.AppendLine();
        builder.AppendLine("- Создан: `" + GeneratedAtIso + "`");
        builder.AppendLine("- Платформа сборки: `" + BuildTarget + "`");
        builder.AppendLine("- Итог: `" + PassCount + " OK`, `" + WarnCount + " предупреждений`, `" + FailCount + " ошибок`");
        builder.AppendLine();
        builder.AppendLine("| Статус | Область | Проверка | Детали |");
        builder.AppendLine("| --- | --- | --- | --- |");

        foreach (DeploymentReadinessItem item in Items)
        {
            builder.Append("| ");
            builder.Append(StatusText(item.Status));
            builder.Append(" | ");
            builder.Append(Escape(item.Area));
            builder.Append(" | ");
            builder.Append(Escape(item.Title));
            builder.Append(" | ");
            builder.Append(Escape(item.Detail));
            builder.AppendLine(" |");
        }

        return builder.ToString();
    }

    private int Count(DeploymentReadinessStatus status)
    {
        int count = 0;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i] != null && Items[i].Status == status)
                count++;
        }

        return count;
    }

    private static string StatusText(DeploymentReadinessStatus status)
    {
        switch (status)
        {
            case DeploymentReadinessStatus.Pass:
                return "OK";
            case DeploymentReadinessStatus.Warn:
                return "ПРЕДУПРЕЖДЕНИЕ";
            default:
                return "ОШИБКА";
        }
    }

    private static string Escape(string value)
    {
        return (value ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }
}
#endif
