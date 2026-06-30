#if UNITY_EDITOR
public sealed class UnityPublisherRequestResult
{
    public bool Success;
    public long StatusCode;
    public string Body;
    public string Error;

    public static UnityPublisherRequestResult Fail(string error)
    {
        return new UnityPublisherRequestResult
        {
            Success = false,
            StatusCode = 0,
            Body = "",
            Error = SaveDataSanitizer.SanitizeHistoryLine(error)
        };
    }
}
#endif
