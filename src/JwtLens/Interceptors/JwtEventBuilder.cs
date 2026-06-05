using JwtLens.Analysis;
using JwtLens.Configuration;
using JwtLens.Models;
using Lens.Abstractions;

namespace JwtLens.Interceptors;

/// <summary>
/// Builds <see cref="CapturedJwt"/> instances from raw JWT tokens.
/// Shared logic between middleware and delegating handler.
/// </summary>
internal static class JwtEventBuilder
{
    /// <summary>
    /// Decodes a JWT and builds a fully analyzed <see cref="CapturedJwt"/> event.
    /// </summary>
    public static CapturedJwt Build(
        string rawToken,
        TokenDirection direction,
        string? requestUri,
        string? httpMethod,
        JwtLensOptions options,
        ClaimDiffTracker diffTracker,
        IRedactor redactor)
    {
        var decodeResult = JwtDecoder.Decode(rawToken);

        if (!decodeResult.Success)
        {
            return new CapturedJwt
            {
                Direction = direction,
                RequestUri = requestUri,
                HttpMethod = httpMethod,
                DecodedSuccessfully = false,
                DecodeError = decodeResult.Error
            };
        }

        var expiryResult = ExpiryAnalyzer.Analyze(decodeResult.Payload, options.WarnIfExpiresWithin);

        var algorithmWarnings = options.FlagWeakAlgorithms
            ? AlgorithmAnalyzer.Analyze(decodeResult.Algorithm, options.WeakAlgorithms)
            : [];

        decodeResult.Payload.TryGetValue("sub", out var subject);

        var claimDiffs = options.TrackClaimDiffs
            ? diffTracker.TrackAndDiff(subject, decodeResult.Payload)
            : [];

        var redactedPayload = RedactPayload(decodeResult.Payload, options.SensitiveClaimNames, redactor);

        return new CapturedJwt
        {
            Direction = direction,
            RequestUri = requestUri,
            HttpMethod = httpMethod,
            RawToken = TruncateToken(rawToken),
            Header = decodeResult.Header,
            Payload = redactedPayload,
            HasSignature = decodeResult.HasSignature,
            Algorithm = decodeResult.Algorithm,
            Subject = subject,
            ExpiresAt = expiryResult.ExpiresAt,
            IssuedAt = expiryResult.IssuedAt,
            IsExpired = expiryResult.IsExpired,
            IsExpiringSoon = expiryResult.IsExpiringSoon,
            TimeToExpiry = expiryResult.HasExpiry ? expiryResult.TimeToExpiry : null,
            AlgorithmWarnings = algorithmWarnings,
            ClaimDiffs = claimDiffs,
            DecodedSuccessfully = true
        };
    }

    private static Dictionary<string, string> RedactPayload(
        Dictionary<string, string> payload,
        HashSet<string> sensitiveClaimNames,
        IRedactor redactor)
    {
        var result = new Dictionary<string, string>(payload.Count);

        foreach (var (key, value) in payload)
        {
            if (sensitiveClaimNames.Contains(key) || redactor.IsSensitive(key))
            {
                result[key] = redactor.Redact(value);
            }
            else
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string TruncateToken(string token)
    {
        const int maxLength = 500;
        return token.Length <= maxLength ? token : token[..maxLength] + "...";
    }
}
