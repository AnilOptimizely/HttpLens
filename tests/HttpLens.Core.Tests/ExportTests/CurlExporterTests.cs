using HttpLens.Core.Export;
using HttpLens.Core.Models;
using Xunit;

namespace HttpLens.Core.Tests.ExportTests;

public class CurlExporterTests
{
    [Fact]
    public void GeneratesValidCurlForGetRequest()
    {
        var record = new HttpTrafficRecord
        {
            RequestMethod = "GET",
            RequestUri = "https://api.example.com/users?page=1",
            RequestHeaders = new Dictionary<string, string[]>
            {
                ["Accept"] = new[] { "application/json" }
            }
        };

        var curl = CurlExporter.Export(record);

        Assert.Contains("curl -X GET 'https://api.example.com/users?page=1'", curl);
        Assert.Contains("-H 'Accept: application/json'", curl);
        Assert.DoesNotContain("-d", curl);
    }

    [Fact]
    public void GeneratesValidCurlForPostWithJsonBody()
    {
        var record = new HttpTrafficRecord
        {
            RequestMethod = "POST",
            RequestUri = "https://api.example.com/users",
            RequestHeaders = new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "application/json" }
            },
            RequestBody = """{"name":"test"}"""
        };

        var curl = CurlExporter.Export(record);

        Assert.Contains("curl -X POST", curl);
        Assert.Contains("-d '{\"name\":\"test\"}'", curl);
    }

    [Fact]
    public void EscapesSingleQuotesInBody()
    {
        var record = new HttpTrafficRecord
        {
            RequestMethod = "POST",
            RequestUri = "https://api.example.com/",
            RequestHeaders = new Dictionary<string, string[]>(),
            RequestBody = "it's a test"
        };

        var curl = CurlExporter.Export(record);

        Assert.Contains("it'\\''s a test", curl);
    }

    [Fact]
    public void IncludesAllHeaders()
    {
        var record = new HttpTrafficRecord
        {
            RequestMethod = "GET",
            RequestUri = "https://api.example.com/",
            RequestHeaders = new Dictionary<string, string[]>
            {
                ["Accept"] = new[] { "application/json" },
                ["Authorization"] = new[] { "Bearer token123" },
                ["X-Custom"] = new[] { "value" }
            }
        };

        var curl = CurlExporter.Export(record);

        Assert.Contains("-H 'Accept: application/json'", curl);
        Assert.Contains("-H 'Authorization: Bearer token123'", curl);
        Assert.Contains("-H 'X-Custom: value'", curl);
    }
}
