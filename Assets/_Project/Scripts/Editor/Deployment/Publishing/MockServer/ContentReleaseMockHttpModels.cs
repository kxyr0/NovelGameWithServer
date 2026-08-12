#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public sealed class ContentReleaseMockHttpRequest
{
    public string Method = "";
    public string Target = "";
    public string Path = "";
    public string Query = "";
    public string Body = "";
    public readonly Dictionary<string, string> Headers =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public readonly struct ContentReleaseMockHttpResponse
{
    public ContentReleaseMockHttpResponse(int statusCode, string body)
    {
        StatusCode = statusCode;
        Body = body ?? "";
    }

    public int StatusCode { get; }
    public string Body { get; }

    public static ContentReleaseMockHttpResponse Json(int statusCode, string body)
    {
        return new ContentReleaseMockHttpResponse(statusCode, body);
    }
}
#endif
