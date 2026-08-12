#if UNITY_EDITOR
using NUnit.Framework;

public sealed class DeploymentReadinessReportTests
{
    [Test]
    public void ToMarkdown_RendersSummaryAndEscapesTablePipes()
    {
        var report = new DeploymentReadinessReport
        {
            GeneratedAtIso = "2026-07-14T00:00:00Z",
            BuildTarget = "StandaloneWindows64"
        };

        report.Items.Add(DeploymentReadinessItem.Pass("Stage", "Config", "OK"));
        report.Items.Add(DeploymentReadinessItem.Warn("Manifest", "Сборка", "Не найдено | необязательно"));
        report.Items.Add(DeploymentReadinessItem.Fail("Prod", "Загрузка", "Нет файлов"));

        string markdown = report.ToMarkdown();
        Assert.That(markdown, Does.Contain("`1 OK`, `1 предупреждений`, `1 ошибок`"));
        Assert.That(markdown, Does.Contain("Не найдено \\| необязательно"));
        Assert.IsFalse(report.IsReady);
    }

    [Test]
    public void Scan_IncludesNocturnalToolingChecks()
    {
        DeploymentReadinessReport report = DeploymentReadinessScanner.Scan();

        AssertHasItem(report, "Инструменты Nocturnal", "Окно сервера");
        AssertHasItem(report, "Инструменты Nocturnal", "Инструкция");
        AssertHasItem(report, "Инструменты Nocturnal", "Покрытие локальной проверки backend");
        AssertHasItem(report, "Инструменты Nocturnal", "Покрытие ожидания HTTP в editor coroutine");
    }

    private static void AssertHasItem(DeploymentReadinessReport report, string area, string title)
    {
        for (int i = 0; i < report.Items.Count; i++)
        {
            DeploymentReadinessItem item = report.Items[i];
            if (item != null && item.Area == area && item.Title == title)
                return;
        }

        Assert.Fail("Не найдена проверка готовности: " + area + " / " + title);
    }
}
#endif
