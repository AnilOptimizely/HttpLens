using System.Text.Json;
using System.Text.Json.Serialization;
using HttpLens.Core.Models;

namespace HttpLens.Core.Export;

/// <summary>Exports traffic records as a HAR 1.2 JSON string.</summary>
public static class HarExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Generates a valid HAR 1.2 JSON string from the supplied records.</summary>
    public static string Export(IReadOnlyList<HttpTrafficRecord> records)
    {
        var har = new HarRoot
        {
            Log = new HarLog
            {
                Version = "1.2",
                Creator = new HarCreator { Name = "HttpLens", Version = "1.0.0" },
                Entries = [.. records.Select(ToEntry)]
            }
        };

        return JsonSerializer.Serialize(har, JsonOptions);
    }

    private static HarEntry ToEntry(HttpTrafficRecord r)
    {
        var durationMs = r.Duration.TotalMilliseconds;
        var uri = r.RequestUri;

        return new HarEntry
        {
            StartedDateTime = r.Timestamp.ToString("O"),
            Time = durationMs,
            Request = new HarRequest
            {
                Method = r.RequestMethod,
                Url = uri,
                HttpVersion = "HTTP/1.1",
                Headers = ToHarHeaders(r.RequestHeaders),
                QueryString = ExtractQueryParams(uri),
                BodySize = r.RequestBodySizeBytes ?? -1,
                PostData = string.IsNullOrEmpty(r.RequestBody) ? null : new HarPostData
                {
                    MimeType = r.RequestContentType ?? "text/plain",
                    Text = r.RequestBody
                }
            },
            Response = new HarResponse
            {
                Status = r.ResponseStatusCode ?? 0,
                StatusText = r.ResponseStatusCode.HasValue ? ReasonPhrase(r.ResponseStatusCode.Value) : "Error",
                HttpVersion = "HTTP/1.1",
                Headers = ToHarHeaders(r.ResponseHeaders),
                Content = new HarContent
                {
                    Size = r.ResponseBodySizeBytes ?? -1,
                    MimeType = r.ResponseContentType ?? "text/plain",
                    Text = r.ResponseBody
                },
                BodySize = r.ResponseBodySizeBytes ?? -1
            },
            Cache = new HarCache(),
            Timings = new HarTimings { Send = 0, Wait = durationMs, Receive = 0 }
        };
    }

    private static List<HarNameValue> ToHarHeaders(Dictionary<string, string[]> headers)
    {
        var list = new List<HarNameValue>();
        foreach (var (name, values) in headers)
        {
            foreach (var value in values)
            {
                list.Add(new HarNameValue { Name = name, Value = value });
            }
        }
        return list;
    }

    private static List<HarNameValue> ExtractQueryParams(string url)
    {
        try
        {
            var uri = new Uri(url);
            var query = uri.Query.TrimStart('?');
            if (string.IsNullOrEmpty(query)) return [];

            return [.. query.Split('&')
                .Select(pair =>
                {
                    var idx = pair.IndexOf('=');
                    return idx >= 0
                        ? new HarNameValue
                        {
                            Name = Uri.UnescapeDataString(pair[..idx]),
                            Value = Uri.UnescapeDataString(pair[(idx + 1)..])
                        }
                        : new HarNameValue { Name = Uri.UnescapeDataString(pair), Value = "" };
                })];
        }
        catch
        {
            return [];
        }
    }

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        200 => "OK",
        201 => "Created",
        204 => "No Content",
        301 => "Moved Permanently",
        302 => "Found",
        304 => "Not Modified",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        500 => "Internal Server Error",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        _ => ""
    };
}
