namespace JwtLens.Models;

/// <summary>
/// Represents a warning about a JWT algorithm.
/// </summary>
public sealed class JwtAlgorithmWarning
{
    /// <summary>
    /// Initializes a new instance of <see cref="JwtAlgorithmWarning"/>.
    /// </summary>
    /// <param name="algorithm">The algorithm that triggered the warning.</param>
    /// <param name="severity">The severity level of the warning.</param>
    /// <param name="message">A human-readable description of the warning.</param>
    public JwtAlgorithmWarning(string algorithm, WarningSeverity severity, string message)
    {
        Algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
        Severity = severity;
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }

    /// <summary>The algorithm that triggered the warning.</summary>
    public string Algorithm { get; }

    /// <summary>The severity level.</summary>
    public WarningSeverity Severity { get; }

    /// <summary>Human-readable description of the issue.</summary>
    public string Message { get; }
}

/// <summary>
/// Severity levels for JWT warnings.
/// </summary>
public enum WarningSeverity
{
    /// <summary>Informational notice.</summary>
    Info,

    /// <summary>Potential issue that should be reviewed.</summary>
    Warning,

    /// <summary>Critical security concern.</summary>
    Critical
}
