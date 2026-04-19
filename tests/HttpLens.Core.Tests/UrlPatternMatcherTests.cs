using HttpLens.Core.Interceptors;
using Xunit;

namespace HttpLens.Core.Tests;

public class UrlPatternMatcherTests
{
    [Fact]
    public void ShouldCapture_NoPatterns_ReturnsTrue()
    {
        var result = UrlPatternMatcher.ShouldCapture(
            "https://api.example.com/test",
            excludePatterns: [],
            includePatterns: []);

        Assert.True(result);
    }

    [Fact]
    public void ShouldCapture_ExcludeMatchesStar_ReturnsFalse()
    {
        var result = UrlPatternMatcher.ShouldCapture(
            "https://api.example.com/health",
            excludePatterns: ["*health*"],
            includePatterns: []);

        Assert.False(result);
    }

    [Fact]
    public void ShouldCapture_ExcludeDoesNotMatch_ReturnsTrue()
    {
        var result = UrlPatternMatcher.ShouldCapture(
            "https://api.example.com/users",
            excludePatterns: ["*health*"],
            includePatterns: []);

        Assert.True(result);
    }

    [Fact]
    public void ShouldCapture_IncludeMatches_ReturnsTrue()
    {
        var result = UrlPatternMatcher.ShouldCapture(
            "https://api.github.com/repos",
            excludePatterns: [],
            includePatterns: ["https://api.github.com/*"]);

        Assert.True(result);
    }

    [Fact]
    public void ShouldCapture_IncludeDoesNotMatch_ReturnsFalse()
    {
        var result = UrlPatternMatcher.ShouldCapture(
            "https://internal.example.com/data",
            excludePatterns: [],
            includePatterns: ["https://api.github.com/*"]);

        Assert.False(result);
    }

    [Fact]
    public void ShouldCapture_ExcludeTakesPrecedenceOverInclude_ReturnsFalse()
    {
        var result = UrlPatternMatcher.ShouldCapture(
            "https://api.github.com/health",
            excludePatterns: ["*health*"],
            includePatterns: ["https://api.github.com/*"]);

        Assert.False(result);
    }

    [Fact]
    public void ShouldCapture_CaseInsensitive()
    {
        var result = UrlPatternMatcher.ShouldCapture(
            "https://api.example.com/HEALTH",
            excludePatterns: ["*health*"],
            includePatterns: []);

        Assert.False(result);
    }

    [Theory]
    [InlineData("https://api.example.com/api/ping", "*/api/ping", true)]
    [InlineData("https://api.example.com/v1/ping", "*/v1/ping", true)]
    [InlineData("https://api.example.com/v1/users", "*/api/ping", false)]
    public void ShouldCapture_ExcludeWithVariousPatterns(string uri, string pattern, bool excluded)
    {
        var result = UrlPatternMatcher.ShouldCapture(
            uri,
            excludePatterns: [pattern],
            includePatterns: []);

        Assert.Equal(!excluded, result);
    }

    [Fact]
    public void ShouldCapture_MultipleExcludePatterns_AnyMatchExcludes()
    {
        var result = UrlPatternMatcher.ShouldCapture(
            "https://api.example.com/ping",
            excludePatterns: ["*health*", "*ping*"],
            includePatterns: []);

        Assert.False(result);
    }

    [Fact]
    public void ShouldCapture_MultipleIncludePatterns_AnyMatchIncludes()
    {
        var result = UrlPatternMatcher.ShouldCapture(
            "https://internal.example.com/data",
            excludePatterns: [],
            includePatterns: ["https://api.github.com/*", "https://internal.example.com/*"]);

        Assert.True(result);
    }

    [Fact]
    public void ShouldCapture_EmptyUri_NoPatterns_ReturnsTrue()
    {
        var result = UrlPatternMatcher.ShouldCapture(
            string.Empty,
            excludePatterns: [],
            includePatterns: []);

        Assert.True(result);
    }
}
