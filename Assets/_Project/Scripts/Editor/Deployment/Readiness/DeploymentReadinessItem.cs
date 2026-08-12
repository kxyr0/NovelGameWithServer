#if UNITY_EDITOR
using System;

public enum DeploymentReadinessStatus
{
    Pass,
    Warn,
    Fail
}

[Serializable]
public sealed class DeploymentReadinessItem
{
    public DeploymentReadinessStatus Status;
    public string Area = "";
    public string Title = "";
    public string Detail = "";

    public static DeploymentReadinessItem Pass(string area, string title, string detail = "")
    {
        return Create(DeploymentReadinessStatus.Pass, area, title, detail);
    }

    public static DeploymentReadinessItem Warn(string area, string title, string detail = "")
    {
        return Create(DeploymentReadinessStatus.Warn, area, title, detail);
    }

    public static DeploymentReadinessItem Fail(string area, string title, string detail = "")
    {
        return Create(DeploymentReadinessStatus.Fail, area, title, detail);
    }

    private static DeploymentReadinessItem Create(
        DeploymentReadinessStatus status,
        string area,
        string title,
        string detail)
    {
        return new DeploymentReadinessItem
        {
            Status = status,
            Area = area ?? "",
            Title = title ?? "",
            Detail = detail ?? ""
        };
    }
}
#endif
