namespace Lens.Abstractions;

/// <summary>
/// Describes a Lens package for discovery and dashboard integration.
/// </summary>
public sealed class LensPackageMetadata
{
    /// <summary>
    /// Initializes a new instance of <see cref="LensPackageMetadata"/>.
    /// </summary>
    /// <param name="packageId">The NuGet package ID (e.g., "JwtLens").</param>
    /// <param name="displayName">Human-readable display name (e.g., "JWT Lens").</param>
    public LensPackageMetadata(string packageId, string displayName)
    {
        PackageId = packageId ?? throw new ArgumentNullException(nameof(packageId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }

    /// <summary>
    /// The NuGet package ID.
    /// </summary>
    public string PackageId { get; }

    /// <summary>
    /// Human-readable display name for the dashboard.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Optional short description of what this package diagnoses.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Optional version string of the installed package.
    /// </summary>
    public string? Version { get; init; }
}
