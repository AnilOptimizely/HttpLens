using JwtLens.Models;

namespace JwtLens.Analysis;

/// <summary>
/// Analyzes JWT expiration claims and determines if a token is expired or expiring soon.
/// </summary>
public static class ExpiryAnalyzer
{
    /// <summary>
    /// Analyzes the expiry status of a decoded JWT payload.
    /// </summary>
    /// <param name="payload">The decoded payload claims.</param>
    /// <param name="warnThreshold">The threshold for "expiring soon" warnings.</param>
    /// <param name="now">The current time (for testability). Defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    /// <returns>An <see cref="ExpiryResult"/> describing the token's expiry status.</returns>
    public static ExpiryResult Analyze(Dictionary<string, string> payload, TimeSpan warnThreshold, DateTimeOffset? now = null)
    {
        var currentTime = now ?? DateTimeOffset.UtcNow;

        if (!payload.TryGetValue("exp", out var expValue) || !long.TryParse(expValue, out var expUnix))
        {
            return new ExpiryResult { HasExpiry = false };
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
        var timeToExpiry = expiresAt - currentTime;
        var isExpired = timeToExpiry <= TimeSpan.Zero;
        var isExpiringSoon = !isExpired && timeToExpiry <= warnThreshold;

        DateTimeOffset? issuedAt = null;
        if (payload.TryGetValue("iat", out var iatValue) && long.TryParse(iatValue, out var iatUnix))
        {
            issuedAt = DateTimeOffset.FromUnixTimeSeconds(iatUnix);
        }

        return new ExpiryResult
        {
            HasExpiry = true,
            ExpiresAt = expiresAt,
            IssuedAt = issuedAt,
            IsExpired = isExpired,
            IsExpiringSoon = isExpiringSoon,
            TimeToExpiry = timeToExpiry
        };
    }
}

/// <summary>
/// Result of expiry analysis for a JWT.
/// </summary>
public sealed class ExpiryResult
{
    /// <summary>Whether the token has an exp claim.</summary>
    public bool HasExpiry { get; init; }

    /// <summary>The expiration time.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>The issued-at time, if available.</summary>
    public DateTimeOffset? IssuedAt { get; init; }

    /// <summary>Whether the token is expired.</summary>
    public bool IsExpired { get; init; }

    /// <summary>Whether the token is expiring within the warning threshold.</summary>
    public bool IsExpiringSoon { get; init; }

    /// <summary>Time remaining until expiry.</summary>
    public TimeSpan? TimeToExpiry { get; init; }
}
