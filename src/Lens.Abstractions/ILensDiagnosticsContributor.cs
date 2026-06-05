namespace Lens.Abstractions;

/// <summary>
/// Contract for Lens packages that contribute diagnostics data to the dashboard.
/// Implement this interface to register a package as a diagnostics contributor.
/// </summary>
public interface ILensDiagnosticsContributor
{
    /// <summary>
    /// Gets metadata describing this Lens package.
    /// </summary>
    LensPackageMetadata Metadata { get; }

    /// <summary>
    /// Gets the most recent diagnostics snapshot from this contributor.
    /// Returns <see langword="null"/> if no data has been captured yet.
    /// </summary>
    LensDiagnosticsSnapshot? GetLatestSnapshot();
}
