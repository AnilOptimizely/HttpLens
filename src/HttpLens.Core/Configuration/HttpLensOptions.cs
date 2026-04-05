namespace HttpLens.Core.Configuration;

/// <summary>Options for configuring the HttpLens middleware.</summary>
public sealed class HttpLensOptions
{
    /// <summary>Maximum number of traffic records to keep in memory. Default: 500.</summary>
    public int MaxStoredRecords { get; set; } = 500;

    /// <summary>Maximum number of characters to capture per request/response body. Default: 64000.</summary>
    public int MaxBodyCaptureSize { get; set; } = 64_000;

    /// <summary>URL path where the dashboard UI is served. Default: "/_httplens".</summary>
    public string DashboardPath { get; set; } = "/_httplens";

    /// <summary>Headers whose values will be masked in captured traffic. Not implemented in v0.1.</summary>
    public HashSet<string> SensitiveHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key"
    };

    /// <summary>Whether to capture request bodies. Default: true.</summary>
    public bool CaptureRequestBody { get; set; } = true;

    /// <summary>Whether to capture response bodies. Default: true.</summary>
    public bool CaptureResponseBody { get; set; } = true;

    /// <summary>Whether to capture traffic from manually-created HttpClient instances via DiagnosticListener. Default: true.</summary>
    public bool EnableDiagnosticInterception { get; set; } = true;
}
