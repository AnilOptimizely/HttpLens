using JwtLens.Storage;
using Microsoft.AspNetCore.Http;

namespace JwtLens.Middleware;

/// <summary>
/// ASP.NET Core middleware that captures inbound JWT tokens from Authorization headers.
/// </summary>
public sealed class JwtCaptureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly JwtEventStore _store;
    private readonly ClaimDiffTracker _diffTracker;

    /// <summary>
    /// Creates a new instance of <see cref="JwtCaptureMiddleware"/>.
    /// </summary>
    public JwtCaptureMiddleware(RequestDelegate next, JwtEventStore store, ClaimDiffTracker diffTracker)
    {
        _next = next;
        _store = store;
        _diffTracker = diffTracker;
    }

    /// <summary>
    /// Processes an HTTP request, capturing JWT information if a ****** is present.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (!string.IsNullOrEmpty(authHeader) &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();

            if (!string.IsNullOrEmpty(token))
            {
                var evt = JwtDecoder.Decode(token, "Inbound");
                var diffs = _diffTracker.ComputeDiffs(evt);
                var eventWithDiffs = new Models.JwtEvent
                {
                    Id = evt.Id,
                    Timestamp = evt.Timestamp,
                    DecodedSuccessfully = evt.DecodedSuccessfully,
                    Algorithm = evt.Algorithm,
                    Direction = evt.Direction,
                    AlgorithmWarnings = evt.AlgorithmWarnings,
                    IsExpired = evt.IsExpired,
                    IsExpiringSoon = evt.IsExpiringSoon,
                    ExpiresAt = evt.ExpiresAt,
                    HasSignature = evt.HasSignature,
                    DecodeError = evt.DecodeError,
                    Payload = evt.Payload,
                    ClaimDiffs = diffs
                };
                _store.Add(eventWithDiffs);
            }
        }

        await _next(context);
    }
}
