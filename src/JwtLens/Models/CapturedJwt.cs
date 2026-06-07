namespace JwtLens.Models;

/// <summary>
/// Represents a captured and decoded JWT event.
/// </summary>
public sealed class CapturedJwt
{
    /// <summary>Unique identifier for this captured event.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>When the token was captured.</summary>
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Direction of the request (inbound or outbound).</summary>
    public required TokenDirection Direction { get; init; }

    /// <summary>The request URI associated with this token.</summary>
    public string? RequestUri { get; init; }

    /// <summary>The HTTP method of the associated request.</summary>
    public string? HttpMethod { get; init; }

    /// <summary>The raw JWT string (may be truncated for storage).</summary>
    public string? RawToken { get; init; }

    /// <summary>Decoded JWT header claims.</summary>
    public Dictionary<string, string> Header { get; init; } = new();

    /// <summary>Decoded JWT payload claims (sensitive values may be redacted).</summary>
    public Dictionary<string, string> Payload { get; init; } = new();

    /// <summary>Whether the token has a signature segment.</summary>
    public bool HasSignature { get; init; }

    /// <summary>The algorithm specified in the header.</summary>
    public string? Algorithm { get; init; }

    /// <summary>The subject claim value (used for claim diff tracking).</summary>
    public string? Subject { get; init; }

    /// <summary>Token expiration time, if present.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Token issued-at time, if present.</summary>
    public DateTimeOffset? IssuedAt { get; init; }

    /// <summary>Whether the token is expired at time of capture.</summary>
    public bool IsExpired { get; init; }

    /// <summary>Whether the token is expiring within the configured warning threshold.</summary>
    public bool IsExpiringSoon { get; init; }

    /// <summary>Time remaining until expiry (null if no exp claim).</summary>
    public TimeSpan? TimeToExpiry { get; init; }

    /// <summary>Algorithm warnings detected for this token.</summary>
    public List<JwtAlgorithmWarning> AlgorithmWarnings { get; init; } = [];

    /// <summary>Claim differences from the previous token for the same subject.</summary>
    public List<ClaimDiff> ClaimDiffs { get; init; } = [];

    /// <summary>Whether decoding was successful.</summary>
    public bool DecodedSuccessfully { get; init; }

    /// <summary>Error message if decoding failed.</summary>
    public string? DecodeError { get; init; }
}
