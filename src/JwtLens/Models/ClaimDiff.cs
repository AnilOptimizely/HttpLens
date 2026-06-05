namespace JwtLens.Models;

/// <summary>
/// Represents a difference in claims between two consecutive tokens for the same subject.
/// </summary>
public sealed class ClaimDiff
{
    /// <summary>The name of the claim that changed.</summary>
    public required string ClaimName { get; init; }

    /// <summary>The previous value (null if claim was added).</summary>
    public string? PreviousValue { get; init; }

    /// <summary>The new value (null if claim was removed).</summary>
    public string? NewValue { get; init; }

    /// <summary>The type of change.</summary>
    public required ClaimDiffType DiffType { get; init; }
}

/// <summary>
/// Types of claim differences.
/// </summary>
public enum ClaimDiffType
{
    /// <summary>Claim was added in the new token.</summary>
    Added,

    /// <summary>Claim was removed from the new token.</summary>
    Removed,

    /// <summary>Claim value changed between tokens.</summary>
    Modified
}
