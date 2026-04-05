using System.Text.Json;
using HttpLens.Core.Export;
using HttpLens.Core.Models;
using Xunit;

namespace HttpLens.Core.Tests.ExportTests;

public class HarExporterTests
{
    [Fact]
    public void GeneratesValidHarJson()
    {
        var records = new List<HttpTrafficRecord>
        {
            new()
            {
                RequestMethod = "GET",
                RequestUri = "https://api.example.com/users?page=1",
                RequestHeaders = new Dictionary<string, string[]>
                {
                    ["Accept"] = new[] { "application/json" }
                },
                ResponseStatusCode = 200,
                ResponseHeaders = new Dictionary<string, string[]>
                {
                    ["Content-Type"] = new[] { "application/json" }
                },
                ResponseBody = """{"users":[]}""",
                ResponseBodySizeBytes = 13,
                Timestamp = DateTimeOffset.UtcNow,
                Duration = TimeSpan.FromMilliseconds(150)
            }
        };

        var json = HarExporter.Export(records);

        Assert.NotNull(json);
        Assert.Contains("\"version\"", json);
    }

    [Fact]
    public void DeserialisedStructureIsValid()
    {
        var records = new List<HttpTrafficRecord>
        {
            new()
            {
                RequestMethod = "POST",
                RequestUri = "https://api.example.com/items",
                RequestHeaders = new Dictionary<string, string[]>(),
                ResponseStatusCode = 201,
                ResponseHeaders = new Dictionary<string, string[]>(),
                Timestamp = DateTimeOffset.UtcNow,
                Duration = TimeSpan.FromMilliseconds(200)
            },
            new()
            {
                RequestMethod = "GET",
                RequestUri = "https://api.example.com/items/1",
                RequestHeaders = new Dictionary<string, string[]>(),
                ResponseStatusCode = 200,
                ResponseHeaders = new Dictionary<string, string[]>(),
                Timestamp = DateTimeOffset.UtcNow,
                Duration = TimeSpan.FromMilliseconds(50)
            }
        };

        var json = HarExporter.Export(records);
        var doc = JsonDocument.Parse(json);
        var log = doc.RootElement.GetProperty("log");

        Assert.Equal("1.2", log.GetProperty("version").GetString());
        Assert.Equal("HttpLens", log.GetProperty("creator").GetProperty("name").GetString());
        Assert.Equal(2, log.GetProperty("entries").GetArrayLength());

        var entry = log.GetProperty("entries")[0];
        Assert.Equal("POST", entry.GetProperty("request").GetProperty("method").GetString());
        Assert.Equal(201, entry.GetProperty("response").GetProperty("status").GetInt32());
    }
}
