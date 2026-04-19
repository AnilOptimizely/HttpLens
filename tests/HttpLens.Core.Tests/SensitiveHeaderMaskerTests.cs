using HttpLens.Core.Interceptors;
using Xunit;

namespace HttpLens.Core.Tests;

public class SensitiveHeaderMaskerTests
{
    [Fact]
    public void MasksAuthorizationHeaderValueCorrectly()
    {
        var headers = new Dictionary<string, string[]>
        {
            ["Authorization"] = new[] { "Bearer fake-test-token-value-for-unit-testing" }
        };
        var sensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Authorization" };

        var result = SensitiveHeaderMasker.Mask(headers, sensitive);

        var masked = result["Authorization"][0];
        Assert.StartsWith("Bear", masked);
        Assert.EndsWith("ting", masked);
        Assert.Contains("••••••••", masked);
    }

    [Fact]
    public void MasksShortValuesEntirely()
    {
        var headers = new Dictionary<string, string[]>
        {
            ["X-Api-Key"] = new[] { "abc" }
        };
        var sensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "X-Api-Key" };

        var result = SensitiveHeaderMasker.Mask(headers, sensitive);

        Assert.Equal("••••••••", result["X-Api-Key"][0]);
    }

    [Fact]
    public void MasksExactlyEightCharValuesEntirely()
    {
        var headers = new Dictionary<string, string[]>
        {
            ["Authorization"] = new[] { "12345678" }
        };
        var sensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Authorization" };

        var result = SensitiveHeaderMasker.Mask(headers, sensitive);

        Assert.Equal("••••••••", result["Authorization"][0]);
    }

    [Fact]
    public void LeavesNonSensitiveHeadersUntouched()
    {
        var headers = new Dictionary<string, string[]>
        {
            ["Content-Type"] = new[] { "application/json" },
            ["Authorization"] = new[] { "Bearer mytoken123" }
        };
        var sensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Authorization" };

        var result = SensitiveHeaderMasker.Mask(headers, sensitive);

        Assert.Equal("application/json", result["Content-Type"][0]);
        Assert.Contains("••••••••", result["Authorization"][0]);
    }

    [Fact]
    public void CaseInsensitiveMatching()
    {
        var headers = new Dictionary<string, string[]>
        {
            ["authorization"] = new[] { "Bearer longtoken1234" }
        };
        var sensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Authorization" };

        var result = SensitiveHeaderMasker.Mask(headers, sensitive);

        Assert.Contains("••••••••", result["authorization"][0]);
    }
}
