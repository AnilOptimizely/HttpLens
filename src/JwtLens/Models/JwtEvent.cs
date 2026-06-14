namespace JwtLens.Models;

/// <summary>
/// Represents a captured JWT event (inbound or outbound).
/// </summary>
public sealed class JwtEvent
{
    /// <summary>Unique event identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>When the event was captured.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Whether the token was successfully decoded.</summary>
    public bool DecodedSuccessfully { get; init; }

    /// <summary>The algorithm from the JWT header.</summary>
    public string? Algorithm { get; init; }

    /// <summary>Direction of the token (Inbound or Outbound).</summary>
    public string Direction { get; init; } = "Inbound";

    /// <summary>Algorithm-related warnings.</summary>
    public List<AlgorithmWarning> AlgorithmWarnings { get; init; } = new();

    /// <summary>Whether the token has expired.</summary>
    public bool IsExpired { get; init; }

    /// <summary>Whether the token is expiring soon (within threshold).</summary>
    public bool IsExpiringSoon { get; init; }

    /// <summary>Token expiration time, if present.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Whether the token has a signature segment.</summary>
    public bool HasSignature { get; init; }

    /// <summary>Decode error message, if decoding failed.</summary>
    public string? DecodeError { get; init; }

    /// <summary>Decoded payload claims (with redaction applied).</summary>
    public Dictionary<string, object?> Payload { get; init; } = new();

    /// <summary>Claim differences from the previous token for the same subject.</summary>
    public List<ClaimDiff> ClaimDiffs { get; init; } = new();
}
