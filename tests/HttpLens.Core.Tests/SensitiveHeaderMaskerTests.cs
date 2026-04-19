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
            ["Authorization"] = ["Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.eyJzdWIiOiIxIn0.dummytoken"]
        };
        var sensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Authorization" };

        var result = SensitiveHeaderMasker.Mask(headers, sensitive);

        var masked = result["Authorization"][0];
        Assert.StartsWith("Bear", masked);
        Assert.EndsWith("oken", masked);
        Assert.Contains("••••••••", masked);
    }

    [Fact]
    public void MasksShortValuesEntirely()
    {
        var headers = new Dictionary<string, string[]>
        {
            ["X-Api-Key"] = ["abc"]
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
            ["Authorization"] = ["12345678"]
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
            ["Content-Type"] = ["application/json"],
            ["Authorization"] = ["Bearer mytoken123"]
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
            ["authorization"] = ["Bearer longtoken1234"]
        };
        var sensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Authorization" };

        var result = SensitiveHeaderMasker.Mask(headers, sensitive);

        Assert.Contains("••••••••", result["authorization"][0]);
    }
}
