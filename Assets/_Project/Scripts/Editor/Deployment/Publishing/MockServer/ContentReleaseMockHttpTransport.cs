#if UNITY_EDITOR
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

public static class ContentReleaseMockHttpTransport
{
    private const int MaxHeaderChars = 64 * 1024;
    private const int MaxBodyChars = 256 * 1024;

    public static ContentReleaseMockHttpRequest Read(NetworkStream stream)
    {
        var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
        string requestLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(requestLine))
            return null;

        string[] parts = requestLine.Split(' ');
        if (parts.Length < 2)
            return null;

        var request = new ContentReleaseMockHttpRequest
        {
            Method = parts[0].Trim().ToUpperInvariant(),
            Target = parts[1].Trim()
        };
        SplitTarget(request);

        int headerChars = 0;
        string line;
        while (!string.IsNullOrEmpty(line = reader.ReadLine()))
        {
            headerChars += line.Length;
            if (headerChars > MaxHeaderChars)
                return null;

            int separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            string name = line.Substring(0, separator).Trim();
            string value = line.Substring(separator + 1).Trim();
            request.Headers[name] = value;
        }

        request.Body = ReadBody(reader, request);
        return request;
    }

    public static void Write(NetworkStream stream, ContentReleaseMockHttpResponse response)
    {
        byte[] body = Encoding.UTF8.GetBytes(response.Body ?? "");
        string headers =
            "HTTP/1.1 " + response.StatusCode + " " + Reason(response.StatusCode) + "\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            "Content-Length: " + body.Length + "\r\n" +
            "Connection: close\r\n\r\n";

        byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(body, 0, body.Length);
    }

    private static string ReadBody(StreamReader reader, ContentReleaseMockHttpRequest request)
    {
        if (!request.Headers.TryGetValue("Content-Length", out string rawLength) ||
            !int.TryParse(rawLength, out int length) ||
            length <= 0)
        {
            return "";
        }

        length = Math.Min(length, MaxBodyChars);
        char[] buffer = new char[length];
        int read = 0;
        while (read < length)
        {
            int count = reader.Read(buffer, read, length - read);
            if (count <= 0)
                break;
            read += count;
        }

        return new string(buffer, 0, read);
    }

    private static void SplitTarget(ContentReleaseMockHttpRequest request)
    {
        int queryIndex = request.Target.IndexOf('?');
        request.Path = queryIndex >= 0 ? request.Target.Substring(0, queryIndex) : request.Target;
        request.Query = queryIndex >= 0 ? request.Target.Substring(queryIndex + 1) : "";
        if (!request.Path.StartsWith("/", StringComparison.Ordinal))
            request.Path = "/" + request.Path;
        request.Path = request.Path.TrimEnd('/');
        if (string.IsNullOrEmpty(request.Path))
            request.Path = "/";
    }

    private static string Reason(int statusCode)
    {
        if (statusCode >= 200 && statusCode < 300)
            return "OK";
        if (statusCode == 401)
            return "Unauthorized";
        if (statusCode == 404)
            return "Not Found";
        if (statusCode == 422)
            return "Unprocessable Entity";
        return "Error";
    }
}
#endif
