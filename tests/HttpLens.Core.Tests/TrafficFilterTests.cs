using HttpLens.Core.Filtering;
using HttpLens.Core.Models;
using Xunit;

namespace HttpLens.Core.Tests;

public class TrafficFilterTests
{
    private static IReadOnlyList<HttpTrafficRecord> CreateSampleRecords() =>
    [
        new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://api.github.com/repos", ResponseStatusCode = 200 },
        new HttpTrafficRecord { RequestMethod = "POST", RequestUri = "https://api.github.com/graphql", ResponseStatusCode = 201 },
        new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://internal.example.com/health", ResponseStatusCode = 200 },
        new HttpTrafficRecord { RequestMethod = "DELETE", RequestUri = "https://api.github.com/repos/123", ResponseStatusCode = 404 },
        new HttpTrafficRecord { RequestMethod = "PUT", RequestUri = "https://api.example.com/users/1", ResponseStatusCode = 500 },
        new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://api.example.com/search", ResponseStatusCode = 429 },
    ];

    [Fact]
    public void Apply_NoFilters_ReturnsAllRecords()
    {
        var records = CreateSampleRecords();
        var criteria = new TrafficFilterCriteria();

        var result = TrafficFilter.Apply(records, criteria);

        Assert.Equal(records.Count, result.Count);
    }

    [Fact]
    public void Apply_MethodFilter_ReturnsOnlyMatching()
    {
        var records = CreateSampleRecords();
        var criteria = new TrafficFilterCriteria(Method: "GET");

        var result = TrafficFilter.Apply(records, criteria);

        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.Equal("GET", r.RequestMethod));
    }

    [Fact]
    public void Apply_MethodFilter_CaseInsensitive()
    {
        var records = CreateSampleRecords();
        var criteria = new TrafficFilterCriteria(Method: "get");

        var result = TrafficFilter.Apply(records, criteria);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Apply_StatusPrefixFilter_ReturnsMatching()
    {
        var records = CreateSampleRecords();
        var criteria = new TrafficFilterCriteria(Status: "4");

        var result = TrafficFilter.Apply(records, criteria);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.True(r.ResponseStatusCode >= 400 && r.ResponseStatusCode < 500));
    }

    [Fact]
    public void Apply_StatusPrefixFilter_ExactMatch()
    {
        var records = CreateSampleRecords();
        var criteria = new TrafficFilterCriteria(Status: "200");

        var result = TrafficFilter.Apply(records, criteria);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(200, r.ResponseStatusCode));
    }

    [Fact]
    public void Apply_HostFilter_ReturnsMatching()
    {
        var records = CreateSampleRecords();
        var criteria = new TrafficFilterCriteria(Host: "github.com");

        var result = TrafficFilter.Apply(records, criteria);

        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.Contains("github.com", r.RequestUri));
    }

    [Fact]
    public void Apply_SearchFilter_CaseInsensitive()
    {
        var records = CreateSampleRecords();
        var criteria = new TrafficFilterCriteria(Search: "GRAPHQL");

        var result = TrafficFilter.Apply(records, criteria);

        Assert.Single(result);
        Assert.Contains("graphql", result[0].RequestUri);
    }

    [Fact]
    public void Apply_CombinedFilters_AllMustMatch()
    {
        var records = CreateSampleRecords();
        var criteria = new TrafficFilterCriteria(Method: "GET", Host: "github.com");

        var result = TrafficFilter.Apply(records, criteria);

        Assert.Single(result);
        Assert.Equal("GET", result[0].RequestMethod);
        Assert.Contains("github.com", result[0].RequestUri);
        Assert.Equal(200, result[0].ResponseStatusCode);
    }

    [Fact]
    public void Apply_EmptyInput_ReturnsEmpty()
    {
        var records = Array.Empty<HttpTrafficRecord>();
        var criteria = new TrafficFilterCriteria(Method: "GET");

        var result = TrafficFilter.Apply(records, criteria);

        Assert.Empty(result);
    }

    [Fact]
    public void Apply_NullStatusCode_ExcludedByStatusFilter()
    {
        var records = new List<HttpTrafficRecord>
        {
            new() { RequestMethod = "GET", RequestUri = "https://example.com", ResponseStatusCode = null },
            new() { RequestMethod = "GET", RequestUri = "https://example.com", ResponseStatusCode = 200 },
        };
        var criteria = new TrafficFilterCriteria(Status: "2");

        var result = TrafficFilter.Apply(records, criteria);

        Assert.Single(result);
        Assert.Equal(200, result[0].ResponseStatusCode);
    }

    [Fact]
    public void IsEmpty_AllNull_ReturnsTrue()
    {
        var criteria = new TrafficFilterCriteria();
        Assert.True(criteria.IsEmpty);
    }

    [Fact]
    public void IsEmpty_HasMethod_ReturnsFalse()
    {
        var criteria = new TrafficFilterCriteria(Method: "GET");
        Assert.False(criteria.IsEmpty);
    }
}
