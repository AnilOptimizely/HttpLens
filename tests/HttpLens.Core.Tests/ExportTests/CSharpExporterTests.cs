using HttpLens.Core.Export;
using HttpLens.Core.Models;
using Xunit;

namespace HttpLens.Core.Tests.ExportTests;

public class CSharpExporterTests
{
    [Fact]
    public void GeneratesValidCSharpForGetRequest()
    {
        var record = new HttpTrafficRecord
        {
            RequestMethod = "GET",
            RequestUri = "https://api.example.com/users",
            RequestHeaders = new Dictionary<string, string[]>
            {
                ["Accept"] = new[] { "application/json" }
            }
        };

        var code = CSharpExporter.Export(record);

        Assert.Contains("new HttpClient()", code);
        Assert.Contains("HttpMethod.Get", code);
        Assert.Contains("https://api.example.com/users", code);
        Assert.Contains("TryAddWithoutValidation(\"Accept\", \"application/json\")", code);
        Assert.DoesNotContain("StringContent", code);
    }

    [Fact]
    public void GeneratesValidCSharpForPostWithBody()
    {
        var record = new HttpTrafficRecord
        {
            RequestMethod = "POST",
            RequestUri = "https://api.example.com/users",
            RequestHeaders = new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "application/json" }
            },
            RequestBody = """{"name":"test"}""",
            RequestContentType = "application/json"
        };

        var code = CSharpExporter.Export(record);

        Assert.Contains("HttpMethod.Post", code);
        Assert.Contains("StringContent", code);
        Assert.Contains("application/json", code);
    }

    [Fact]
    public void SkipsContentTypeInHeaders()
    {
        var record = new HttpTrafficRecord
        {
            RequestMethod = "POST",
            RequestUri = "https://api.example.com/",
            RequestHeaders = new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "application/json" },
                ["Accept"] = new[] { "application/json" }
            },
            RequestBody = "{}",
            RequestContentType = "application/json"
        };

        var code = CSharpExporter.Export(record);

        Assert.DoesNotContain("TryAddWithoutValidation(\"Content-Type\"", code);
        Assert.Contains("TryAddWithoutValidation(\"Accept\"", code);
    }

    [Fact]
    public void EscapesQuotesInVerbatimStrings()
    {
        var record = new HttpTrafficRecord
        {
            RequestMethod = "POST",
            RequestUri = "https://api.example.com/",
            RequestHeaders = new Dictionary<string, string[]>(),
            RequestBody = """{"key":"value"}""",
            RequestContentType = "application/json"
        };

        var code = CSharpExporter.Export(record);

        // Verbatim strings double the quotes: {"key":"value"} → {""key"":""value""}
        Assert.Contains(@"{""""key"""":""""value""""}", code);
    }
}
