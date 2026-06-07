using Xunit;
using JwtLens.Analysis;

namespace JwtLens.Tests;

public sealed class ExpiryAnalyzerTests
{
    [Fact]
    public void Analyze_ExpiredToken_ReturnsExpired()
    {
        var payload = new Dictionary<string, string>
        {
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString()
        };

        var result = ExpiryAnalyzer.Analyze(payload, TimeSpan.FromMinutes(5));

        Assert.True(result.HasExpiry);
        Assert.True(result.IsExpired);
        Assert.False(result.IsExpiringSoon);
    }

    [Fact]
    public void Analyze_ExpiringSoonToken_ReturnsExpiringSoon()
    {
        var payload = new Dictionary<string, string>
        {
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeSeconds().ToString()
        };

        var result = ExpiryAnalyzer.Analyze(payload, TimeSpan.FromMinutes(5));

        Assert.True(result.HasExpiry);
        Assert.False(result.IsExpired);
        Assert.True(result.IsExpiringSoon);
    }

    [Fact]
    public void Analyze_ValidToken_NeitherExpiredNorExpiringSoon()
    {
        var payload = new Dictionary<string, string>
        {
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()
        };

        var result = ExpiryAnalyzer.Analyze(payload, TimeSpan.FromMinutes(5));

        Assert.True(result.HasExpiry);
        Assert.False(result.IsExpired);
        Assert.False(result.IsExpiringSoon);
    }

    [Fact]
    public void Analyze_NoExpClaim_ReturnsNoExpiry()
    {
        var payload = new Dictionary<string, string>
        {
            ["sub"] = "user123"
        };

        var result = ExpiryAnalyzer.Analyze(payload, TimeSpan.FromMinutes(5));

        Assert.False(result.HasExpiry);
        Assert.False(result.IsExpired);
        Assert.False(result.IsExpiringSoon);
    }

    [Fact]
    public void Analyze_WithIatClaim_ParsesIssuedAt()
    {
        var iatTime = DateTimeOffset.UtcNow.AddHours(-1);
        var payload = new Dictionary<string, string>
        {
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString(),
            ["iat"] = iatTime.ToUnixTimeSeconds().ToString()
        };

        var result = ExpiryAnalyzer.Analyze(payload, TimeSpan.FromMinutes(5));

        Assert.NotNull(result.IssuedAt);
    }

    [Fact]
    public void Analyze_InvalidExpValue_ReturnsNoExpiry()
    {
        var payload = new Dictionary<string, string>
        {
            ["exp"] = "not-a-number"
        };

        var result = ExpiryAnalyzer.Analyze(payload, TimeSpan.FromMinutes(5));

        Assert.False(result.HasExpiry);
    }

    [Fact]
    public void Analyze_WithExplicitNow_UsesProvidedTime()
    {
        var fixedNow = DateTimeOffset.Parse("2025-01-01T12:00:00Z");
        var payload = new Dictionary<string, string>
        {
            ["exp"] = DateTimeOffset.Parse("2025-01-01T12:03:00Z").ToUnixTimeSeconds().ToString()
        };

        var result = ExpiryAnalyzer.Analyze(payload, TimeSpan.FromMinutes(5), fixedNow);

        Assert.True(result.IsExpiringSoon);
        Assert.NotNull(result.TimeToExpiry);
        Assert.True(result.TimeToExpiry.Value.TotalMinutes is > 2.9 and < 3.1);
    }
}
