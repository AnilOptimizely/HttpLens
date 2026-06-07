using JwtLens.Models;

namespace JwtLens.Analysis;

/// <summary>
/// Analyzes JWT algorithms and produces warnings for weak or dangerous algorithms.
/// </summary>
public static class AlgorithmAnalyzer
{
    /// <summary>
    /// Analyzes the algorithm specified in a JWT header.
    /// </summary>
    /// <param name="algorithm">The algorithm value from the JWT header.</param>
    /// <param name="weakAlgorithms">The set of algorithms considered weak.</param>
    /// <returns>A list of warnings (empty if the algorithm is acceptable).</returns>
    public static List<JwtAlgorithmWarning> Analyze(string? algorithm, HashSet<string> weakAlgorithms)
    {
        var warnings = new List<JwtAlgorithmWarning>();

        if (string.IsNullOrEmpty(algorithm))
        {
            warnings.Add(new JwtAlgorithmWarning(
                "missing",
                WarningSeverity.Critical,
                "JWT header does not specify an algorithm."));
            return warnings;
        }

        if (string.Equals(algorithm, "none", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(new JwtAlgorithmWarning(
                algorithm,
                WarningSeverity.Critical,
                "Algorithm 'none' means the token is unsigned and can be forged by anyone."));
            return warnings;
        }

        if (weakAlgorithms.Contains(algorithm))
        {
            warnings.Add(new JwtAlgorithmWarning(
                algorithm,
                WarningSeverity.Warning,
                $"Algorithm '{algorithm}' is considered weak. Consider using RS256 or ES256."));
        }

        return warnings;
    }
}
