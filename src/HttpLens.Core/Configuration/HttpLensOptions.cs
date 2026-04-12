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

    /// <summary>
    /// Master switch to enable/disable all HttpLens functionality.
    /// When false, the delegating handler becomes a pass-through (no capture),
    /// and the dashboard/API endpoints return 404.
    /// Default: true.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Environments where HttpLens is allowed to run.
    /// If non-empty, HttpLens will only activate when the current hosting environment
    /// matches one of these values (case-insensitive).
    /// When empty (default), HttpLens runs in all environments.
    /// Example: ["Development", "Staging"]
    /// </summary>
    public List<string> AllowedEnvironments { get; set; } = [];

    /// <summary>   
    /// API key required to access the dashboard and API endpoints.
    /// When null or empty, no API key authentication is required (development mode).
    /// The key can be provided via the X-HttpLens-Key request header or ?key= query parameter.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Name of an ASP.NET Core authorization policy to apply to all dashboard and API endpoints.
    /// The policy must be registered via AddAuthorization() in the host application.
    /// When null (default), no authorization policy is applied.
    /// Example: "HttpLensAccess"
    /// </summary>
    public string? AuthorizationPolicy { get; set; }

    /// <summary>
    /// IP addresses or CIDR ranges allowed to access the dashboard and API.
    /// When empty (default), all IP addresses are allowed.
    /// Supports IPv4, IPv6, and CIDR notation.
    /// Example: ["10.0.0.0/8", "192.168.1.0/24", "::1", "127.0.0.1"]
    /// </summary>
    public List<string> AllowedIpRanges { get; set; } = [];
}
