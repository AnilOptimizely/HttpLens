namespace JwtLens.Configuration;

/// <summary>
/// Options for configuring the JwtLens middleware and analysis features.
/// </summary>
public sealed class JwtLensOptions
{
    /// <summary>
    /// Master switch to enable/disable all JwtLens functionality.
    /// When false, the middleware and handler become pass-throughs.
    /// Default: true.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Tokens expiring within this threshold will trigger a warning.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan WarnIfExpiresWithin { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to track claim diffs between consecutive tokens for the same subject.
    /// Default: true.
    /// </summary>
    public bool TrackClaimDiffs { get; set; } = true;

    /// <summary>
    /// Whether to flag weak or dangerous algorithms (e.g., "none", "HS256").
    /// Default: true.
    /// </summary>
    public bool FlagWeakAlgorithms { get; set; } = true;

    /// <summary>
    /// Maximum number of JWT events to keep in the in-memory store.
    /// Default: 200.
    /// </summary>
    public int MaxStoredEvents { get; set; } = 200;

    /// <summary>
    /// Whether to capture JWTs from outbound HttpClient requests.
    /// Default: true.
    /// </summary>
    public bool CaptureOutboundTokens { get; set; } = true;

    /// <summary>
    /// Whether to capture JWTs from inbound ASP.NET Core requests.
    /// Default: true.
    /// </summary>
    public bool CaptureInboundTokens { get; set; } = true;

    /// <summary>
    /// Claim names whose values should be redacted in stored snapshots.
    /// Default includes email, phone_number, address, birthdate.
    /// </summary>
    public HashSet<string> SensitiveClaimNames { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "email",
        "phone_number",
        "address",
        "birthdate"
    };

    /// <summary>
    /// Environments where JwtLens is allowed to run.
    /// When empty, JwtLens runs in all environments.
    /// </summary>
    public List<string> AllowedEnvironments { get; set; } = [];

    /// <summary>
    /// Algorithms considered weak or dangerous that will trigger warnings.
    /// Default includes "none" and "HS256".
    /// </summary>
    public HashSet<string> WeakAlgorithms { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "HS256"
    };
}
