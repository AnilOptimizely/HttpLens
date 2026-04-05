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
                Entries = records.Select(ToEntry).ToList()
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
            if (string.IsNullOrEmpty(query)) return new List<HarNameValue>();

            return query.Split('&')
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
                })
                .ToList();
        }
        catch
        {
            return new List<HarNameValue>();
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

    // ── HAR model classes ────────────────────────────────────────────────────

    private sealed class HarRoot { public HarLog Log { get; set; } = new(); }

    private sealed class HarLog
    {
        public string Version { get; set; } = "1.2";
        public HarCreator Creator { get; set; } = new();
        public List<HarEntry> Entries { get; set; } = new();
    }

    private sealed class HarCreator
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
    }

    private sealed class HarEntry
    {
        public string StartedDateTime { get; set; } = "";
        public double Time { get; set; }
        public HarRequest Request { get; set; } = new();
        public HarResponse Response { get; set; } = new();
        public HarCache Cache { get; set; } = new();
        public HarTimings Timings { get; set; } = new();
    }

    private sealed class HarRequest
    {
        public string Method { get; set; } = "";
        public string Url { get; set; } = "";
        public string HttpVersion { get; set; } = "";
        public List<HarNameValue> Headers { get; set; } = new();
        public List<HarNameValue> QueryString { get; set; } = new();
        public long BodySize { get; set; }
        public HarPostData? PostData { get; set; }
    }

    private sealed class HarResponse
    {
        public int Status { get; set; }
        public string StatusText { get; set; } = "";
        public string HttpVersion { get; set; } = "";
        public List<HarNameValue> Headers { get; set; } = new();
        public HarContent Content { get; set; } = new();
        public long BodySize { get; set; }
    }

    private sealed class HarContent
    {
        public long Size { get; set; }
        public string MimeType { get; set; } = "";
        public string? Text { get; set; }
    }

    private sealed class HarPostData
    {
        public string MimeType { get; set; } = "";
        public string? Text { get; set; }
    }

    private sealed class HarCache { }

    private sealed class HarTimings
    {
        public double Send { get; set; }
        public double Wait { get; set; }
        public double Receive { get; set; }
    }

    private sealed class HarNameValue
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
    }
}
