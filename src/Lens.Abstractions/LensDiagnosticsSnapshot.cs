namespace Lens.Abstractions;

/// <summary>
/// A point-in-time diagnostics snapshot produced by a Lens package.
/// Uses a simple key-value model for initial versions.
/// </summary>
public sealed class LensDiagnosticsSnapshot
{
    /// <summary>
    /// Initializes a new instance of <see cref="LensDiagnosticsSnapshot"/>.
    /// </summary>
    /// <param name="packageId">The source package ID.</param>
    public LensDiagnosticsSnapshot(string packageId)
    {
        PackageId = packageId ?? throw new ArgumentNullException(nameof(packageId));
    }

    /// <summary>
    /// The NuGet package ID that produced this snapshot.
    /// </summary>
    public string PackageId { get; }

    /// <summary>
    /// When this snapshot was captured.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Diagnostics data as key-value pairs. Values should already be redacted where appropriate.
    /// </summary>
    public Dictionary<string, string> Data { get; init; } = new();

    /// <summary>
    /// Total number of events captured by the source package since startup.
    /// </summary>
    public long EventCount { get; init; }
}
