using JwtLens.Storage;
using Lens.Abstractions;

namespace JwtLens.Middleware;

/// <summary>
/// Provides diagnostics data for JwtLens to the Lens dashboard.
/// </summary>
public sealed class JwtLensDiagnosticsContributor : ILensDiagnosticsContributor
{
    private readonly JwtEventStore _store;

    /// <summary>
    /// Creates a new instance of <see cref="JwtLensDiagnosticsContributor"/>.
    /// </summary>
    public JwtLensDiagnosticsContributor(JwtEventStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public LensPackageMetadata Metadata { get; } = new("JwtLens", "JWT Lens")
    {
        Version = "0.1.0-preview.1",
        Description = "JWT inspection and diagnostics for ASP.NET Core"
    };

    /// <inheritdoc/>
    public LensDiagnosticsSnapshot? GetLatestSnapshot()
    {
        var events = _store.GetAll();
        if (events.Count == 0) return null;

        var latest = events[^1];
        var expiredCount = events.Count(e => e.IsExpired);
        var expiringSoonCount = events.Count(e => e.IsExpiringSoon);
        var warningCount = events.Count(e => e.AlgorithmWarnings.Count > 0);

        return new LensDiagnosticsSnapshot("JwtLens")
        {
            EventCount = _store.TotalCaptured,
            Data = new Dictionary<string, string>
            {
                ["StoredEvents"] = events.Count.ToString(),
                ["ExpiredTokens"] = expiredCount.ToString(),
                ["ExpiringSoonTokens"] = expiringSoonCount.ToString(),
                ["TokensWithAlgorithmWarnings"] = warningCount.ToString(),
                ["LatestTokenAlgorithm"] = latest.Algorithm ?? "unknown",
                ["LatestTokenSubject"] = latest.Payload.TryGetValue("sub", out var sub) ? sub?.ToString() ?? "unknown" : "unknown",
                ["LatestTokenDirection"] = latest.Direction
            }
        };
    }
}
