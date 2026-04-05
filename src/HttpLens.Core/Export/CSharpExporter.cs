using System.Text;
using HttpLens.Core.Models;

namespace HttpLens.Core.Export;

/// <summary>Exports an <see cref="HttpTrafficRecord"/> as a C# <c>HttpClient</c> code snippet.</summary>
public static class CSharpExporter
{
    private static readonly HashSet<string> ContentHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Type", "Content-Length", "Content-Encoding", "Content-Language",
        "Content-Location", "Content-MD5", "Content-Range", "Content-Disposition", "Expires", "Last-Modified"
    };

    /// <summary>Generates a valid C# code snippet that reproduces the captured request.</summary>
    public static string Export(HttpTrafficRecord record)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using var client = new HttpClient();");
        sb.AppendLine($"var request = new HttpRequestMessage(HttpMethod.{MethodToPascal(record.RequestMethod)}, \"{EscapeString(record.RequestUri)}\");");

        foreach (var (name, values) in record.RequestHeaders)
        {
            if (ContentHeaders.Contains(name)) continue;
            var value = string.Join(", ", values);
            sb.AppendLine($"request.Headers.TryAddWithoutValidation(\"{EscapeString(name)}\", \"{EscapeString(value)}\");");
        }

        if (!string.IsNullOrEmpty(record.RequestBody))
        {
            var mediaType = record.RequestContentType ?? "text/plain";
            // Extract just the media type (e.g. "application/json" from "application/json; charset=utf-8")
            var semiIdx = mediaType.IndexOf(';');
            if (semiIdx >= 0) mediaType = mediaType[..semiIdx].Trim();

            sb.AppendLine($"request.Content = new StringContent(@\"{EscapeVerbatim(record.RequestBody)}\", Encoding.UTF8, \"{EscapeString(mediaType)}\");");
        }

        sb.AppendLine("var response = await client.SendAsync(request);");
        sb.AppendLine("var body = await response.Content.ReadAsStringAsync();");

        return sb.ToString();
    }

    private static string MethodToPascal(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => "Get",
            "POST" => "Post",
            "PUT" => "Put",
            "DELETE" => "Delete",
            "PATCH" => "Patch",
            "HEAD" => "Head",
            "OPTIONS" => "Options",
            "TRACE" => "Trace",
            _ => "Get"
        };
    }

    private static string EscapeString(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string EscapeVerbatim(string value)
        => value.Replace("\"", "\"\"");
}
