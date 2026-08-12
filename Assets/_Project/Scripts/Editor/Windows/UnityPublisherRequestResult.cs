#if UNITY_EDITOR
using UnityEngine.Networking;

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

    public static UnityPublisherRequestResult FromRequest(UnityWebRequest request, int maxBodyChars)
    {
        string body = request.downloadHandler != null ? request.downloadHandler.text : "";
        if (body != null && body.Length > maxBodyChars)
            body = body.Substring(0, maxBodyChars);

        bool success = request.result == UnityWebRequest.Result.Success &&
                       request.responseCode >= 200 &&
                       request.responseCode < 300;
        return new UnityPublisherRequestResult
        {
            Success = success,
            StatusCode = request.responseCode,
            Body = body ?? "",
            Error = success ? "" : SaveDataSanitizer.SanitizeHistoryLine(request.error)
        };
    }
}
#endif
