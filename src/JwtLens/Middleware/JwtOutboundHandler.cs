using JwtLens.Analysis;
using JwtLens.Storage;

namespace JwtLens.Middleware;

/// <summary>
/// DelegatingHandler that captures outbound JWT tokens from HttpClient requests.
/// </summary>
public sealed class JwtOutboundHandler : DelegatingHandler
{
    private readonly JwtEventStore _store;
    private readonly ClaimDiffTracker _diffTracker;

    /// <summary>
    /// Creates a new instance of <see cref="JwtOutboundHandler"/>.
    /// </summary>
    public JwtOutboundHandler(JwtEventStore store, ClaimDiffTracker diffTracker)
    {
        _store = store;
        _diffTracker = diffTracker;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization?.Scheme?.Equals("Bearer", StringComparison.OrdinalIgnoreCase) == true)
        {
            var token = request.Headers.Authorization.Parameter;
            if (!string.IsNullOrEmpty(token))
            {
                var evt = JwtDecoder.Decode(token, "Outbound");
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

        return await base.SendAsync(request, cancellationToken);
    }
}
