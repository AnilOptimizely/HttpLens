using Xunit;
using JwtLens.Analysis;

namespace JwtLens.Tests;

public sealed class AlgorithmAnalyzerTests
{
    private static readonly HashSet<string> DefaultWeakAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "HS256"
    };

    [Fact]
    public void Analyze_NoneAlgorithm_ReturnsCriticalWarning()
    {
        var warnings = AlgorithmAnalyzer.Analyze("none", DefaultWeakAlgorithms);

        Assert.Single(warnings);
        Assert.Equal(Models.WarningSeverity.Critical, warnings[0].Severity);
        Assert.Contains("unsigned", warnings[0].Message);
    }

    [Fact]
    public void Analyze_NoneAlgorithm_CaseInsensitive()
    {
        var warnings = AlgorithmAnalyzer.Analyze("None", DefaultWeakAlgorithms);

        Assert.Single(warnings);
        Assert.Equal(Models.WarningSeverity.Critical, warnings[0].Severity);
    }

    [Fact]
    public void Analyze_WeakAlgorithm_ReturnsWarning()
    {
        var warnings = AlgorithmAnalyzer.Analyze("HS256", DefaultWeakAlgorithms);

        Assert.Single(warnings);
        Assert.Equal(Models.WarningSeverity.Warning, warnings[0].Severity);
        Assert.Contains("weak", warnings[0].Message);
    }

    [Fact]
    public void Analyze_StrongAlgorithm_ReturnsNoWarnings()
    {
        var warnings = AlgorithmAnalyzer.Analyze("RS256", DefaultWeakAlgorithms);

        Assert.Empty(warnings);
    }

    [Fact]
    public void Analyze_NullAlgorithm_ReturnsCriticalWarning()
    {
        var warnings = AlgorithmAnalyzer.Analyze(null, DefaultWeakAlgorithms);

        Assert.Single(warnings);
        Assert.Equal(Models.WarningSeverity.Critical, warnings[0].Severity);
        Assert.Contains("does not specify", warnings[0].Message);
    }

    [Fact]
    public void Analyze_EmptyAlgorithm_ReturnsCriticalWarning()
    {
        var warnings = AlgorithmAnalyzer.Analyze("", DefaultWeakAlgorithms);

        Assert.Single(warnings);
        Assert.Equal(Models.WarningSeverity.Critical, warnings[0].Severity);
    }

    [Fact]
    public void Analyze_ES256_ReturnsNoWarnings()
    {
        var warnings = AlgorithmAnalyzer.Analyze("ES256", DefaultWeakAlgorithms);

        Assert.Empty(warnings);
    }
}
