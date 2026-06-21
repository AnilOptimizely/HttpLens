namespace JwtLens.Models;

/// <summary>
/// Represents a detected difference in a claim between consecutive tokens for the same subject.
/// </summary>
public sealed class ClaimDiff
{
    /// <summary>The name of the claim that changed.</summary>
    public string ClaimName { get; init; } = string.Empty;

    /// <summary>The type of change detected.</summary>
    public ClaimDiffType DiffType { get; init; }

    /// <summary>The previous value (null for Added).</summary>
    public string? PreviousValue { get; init; }

    /// <summary>The current value (null for Removed).</summary>
    public string? CurrentValue { get; init; }
}
