namespace HttpLens.Core.Models;

/// <summary>Represents a single captured outbound HTTP request/response pair.</summary>
public sealed class HttpTrafficRecord
{
    /// <summary>Unique identifier for this record.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>When the request was initiated (UTC).</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Total round-trip duration.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Named HttpClient that made the request, or "(unnamed)".</summary>
    public string HttpClientName { get; set; } = "(unnamed)";

    // ── Request ───────────────────────────────────────────────────────────────

    /// <summary>HTTP method (GET, POST, …).</summary>
    public string RequestMethod { get; set; } = string.Empty;

    /// <summary>Full request URI.</summary>
    public string RequestUri { get; set; } = string.Empty;

    /// <summary>Merged request headers (including Content headers).</summary>
    public Dictionary<string, string[]> RequestHeaders { get; set; } = new();

    /// <summary>Captured request body text (null when capture is disabled).</summary>
    public string? RequestBody { get; set; }

    /// <summary>Request Content-Type header value.</summary>
    public string? RequestContentType { get; set; }

    /// <summary>Exact byte-length of the request body before any truncation.</summary>
    public long? RequestBodySizeBytes { get; set; }

    // ── Response ──────────────────────────────────────────────────────────────

    /// <summary>HTTP response status code.</summary>
    public int? ResponseStatusCode { get; set; }

    /// <summary>Merged response headers (including Content headers).</summary>
    public Dictionary<string, string[]> ResponseHeaders { get; set; } = new();

    /// <summary>Captured response body text (null when capture is disabled).</summary>
    public string? ResponseBody { get; set; }

    /// <summary>Response Content-Type header value.</summary>
    public string? ResponseContentType { get; set; }

    /// <summary>Exact byte-length of the response body before any truncation.</summary>
    public long? ResponseBodySizeBytes { get; set; }

    // ── Outcome ───────────────────────────────────────────────────────────────

    /// <summary>True when the response indicates success (2xx) and no exception was thrown.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Exception details when the request threw (ToString()).</summary>
    public string? Exception { get; set; }

    // ── Future phases (placeholder) ───────────────────────────────────────────

    /// <summary>W3C Trace ID from the active Activity (v0.3+).</summary>
    public string? TraceId { get; set; }

    /// <summary>W3C Parent Span ID from the active Activity (v0.3+).</summary>
    public string? ParentSpanId { get; set; }

    /// <summary>Path of the inbound request that triggered this outbound call (v0.3+).</summary>
    public string? InboundRequestPath { get; set; }

    /// <summary>Attempt number for retry tracking (v0.4+). Default: 1.</summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>Groups all retried attempts together (v0.4+).</summary>
    public Guid? RetryGroupId { get; set; }
}
