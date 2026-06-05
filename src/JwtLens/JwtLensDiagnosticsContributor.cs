using JwtLens.Storage;
using Lens.Abstractions;

namespace JwtLens;

/// <summary>
/// Implements <see cref="ILensDiagnosticsContributor"/> to expose JwtLens data to the shared dashboard.
/// </summary>
public sealed class JwtLensDiagnosticsContributor : ILensDiagnosticsContributor
{
    private readonly IJwtEventStore _store;

    /// <summary>
    /// Initializes a new instance of <see cref="JwtLensDiagnosticsContributor"/>.
    /// </summary>
    public JwtLensDiagnosticsContributor(IJwtEventStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public LensPackageMetadata Metadata { get; } = new("JwtLens", "JWT Lens")
    {
        Description = "Decodes, validates, and explains JWTs flowing through your pipeline.",
        Version = "0.1.0-preview.1"
    };

    /// <inheritdoc />
    public LensDiagnosticsSnapshot? GetLatestSnapshot()
    {
        var events = _store.GetAll();
        if (events.Count == 0)
            return null;

        var latest = events[^1];
        var expiredCount = events.Count(e => e.IsExpired);
        var expiringSoonCount = events.Count(e => e.IsExpiringSoon);
        var withWarnings = events.Count(e => e.AlgorithmWarnings.Count > 0);

        return new LensDiagnosticsSnapshot("JwtLens")
        {
            EventCount = _store.TotalCaptured,
            Data = new Dictionary<string, string>
            {
                ["StoredEvents"] = events.Count.ToString(),
                ["ExpiredTokens"] = expiredCount.ToString(),
                ["ExpiringSoonTokens"] = expiringSoonCount.ToString(),
                ["TokensWithAlgorithmWarnings"] = withWarnings.ToString(),
                ["LatestTokenAlgorithm"] = latest.Algorithm ?? "unknown",
                ["LatestTokenSubject"] = latest.Subject ?? "unknown",
                ["LatestTokenDirection"] = latest.Direction.ToString()
            }
        };
    }
}
