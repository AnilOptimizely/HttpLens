namespace JwtLens.Models;

/// <summary>
/// Represents an algorithm-related warning for a JWT.
/// </summary>
public sealed class AlgorithmWarning
{
    /// <summary>Warning severity level.</summary>
    public string Severity { get; init; } = "Warning";

    /// <summary>Descriptive message.</summary>
    public string Message { get; init; } = string.Empty;
}
