using System.Net;
using HttpLens.Core.Configuration;
using HttpLens.Core.Models;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Api;
using HttpLens.Dashboard.Tests.Models;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using Xunit;

namespace HttpLens.Dashboard.Tests;

public class TrafficApiEndpointsTests : IAsyncLifetime
{
    private IHost? _host;
    private HttpClient? _client;
    private InMemoryTrafficStore? _store;

    public async Task InitializeAsync()
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.Configure<HttpLensOptions>(_ => { });
                    services.AddSingleton<ITrafficStore>(sp =>
                    {
                        var opts = sp.GetRequiredService<IOptions<HttpLensOptions>>();
                        _store = new InMemoryTrafficStore(opts);
                        return _store;
                    });
                    services.AddRouting();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(ep => ep.MapHttpLensApi("/_httplens"));
                });
            });

        _host = await builder.StartAsync();
        _client = _host.GetTestClient();
        // Force resolution so the factory runs and _store gets assigned.
        _store = (InMemoryTrafficStore)_host.Services.GetRequiredService<ITrafficStore>();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host is not null) await _host.StopAsync();
        _host?.Dispose();
    }

    [Fact]
    public async Task GetTrafficReturnsJsonWithTotalAndRecords()
    {
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://x.com" });
        _store!.Add(new HttpTrafficRecord { RequestMethod = "POST", RequestUri = "https://y.com" });

        var response = await _client!.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Total);
        Assert.Equal(2, body.Records.Length);
    }

    [Fact]
    public async Task GetTrafficByIdReturns404ForMissingId()
    {
        var response = await _client!.GetAsync($"/_httplens/api/traffic/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTrafficClearsStore()
    {
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET" });

        var response = await _client!.DeleteAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, _store.Count);
    }

    [Fact]
    public async Task GetTraffic_WithMethodFilter_ReturnsFilteredResults()
    {
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://a.com" });
        _store!.Add(new HttpTrafficRecord { RequestMethod = "POST", RequestUri = "https://b.com" });
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://c.com" });

        var response = await _client!.GetAsync("/_httplens/api/traffic?method=GET");
        var body = await response.Content.ReadFromJsonAsync<TrafficListDto>();

        Assert.NotNull(body);
        Assert.Equal(2, body!.Total);
        Assert.All(body.Records, r => Assert.Equal("GET", r.RequestMethod));
    }

    [Fact]
    public async Task GetTraffic_WithStatusFilter_ReturnsFilteredResults()
    {
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://a.com", ResponseStatusCode = 200 });
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://b.com", ResponseStatusCode = 404 });
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://c.com", ResponseStatusCode = 429 });

        var response = await _client!.GetAsync("/_httplens/api/traffic?status=4");
        var body = await response.Content.ReadFromJsonAsync<TrafficListDto>();

        Assert.NotNull(body);
        Assert.Equal(2, body!.Total);
        Assert.All(body.Records, r => Assert.True(r.ResponseStatusCode >= 400 && r.ResponseStatusCode < 500));
    }

    [Fact]
    public async Task GetTraffic_WithSearchFilter_ReturnsFilteredResults()
    {
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://api.github.com/repos" });
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://example.com/health" });

        var response = await _client!.GetAsync("/_httplens/api/traffic?search=github");
        var body = await response.Content.ReadFromJsonAsync<TrafficListDto>();

        Assert.NotNull(body);
        Assert.Single(body!.Records);
        Assert.Contains("github", body.Records[0].RequestUri);
    }

    [Fact]
    public async Task GetTraffic_WithCombinedFilters_FiltersCorrectly()
    {
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://api.github.com/repos", ResponseStatusCode = 200 });
        _store!.Add(new HttpTrafficRecord { RequestMethod = "POST", RequestUri = "https://api.github.com/graphql", ResponseStatusCode = 200 });
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://example.com/test", ResponseStatusCode = 200 });

        var response = await _client!.GetAsync("/_httplens/api/traffic?method=GET&host=github.com");
        var body = await response.Content.ReadFromJsonAsync<TrafficListDto>();

        Assert.NotNull(body);
        Assert.Single(body!.Records);
        Assert.Equal("GET", body.Records[0].RequestMethod);
        Assert.Contains("github.com", body.Records[0].RequestUri);
    }

    [Fact]
    public async Task GetTraffic_FilteredTotal_ReflectsFilteredCount()
    {
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://a.com", ResponseStatusCode = 200 });
        _store!.Add(new HttpTrafficRecord { RequestMethod = "POST", RequestUri = "https://b.com", ResponseStatusCode = 200 });
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://c.com", ResponseStatusCode = 200 });
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://d.com", ResponseStatusCode = 200 });

        var response = await _client!.GetAsync("/_httplens/api/traffic?method=GET");
        var body = await response.Content.ReadFromJsonAsync<TrafficListDto>();

        Assert.NotNull(body);
        // Total should reflect filtered count, not store count
        Assert.Equal(3, body!.Total);
        Assert.Equal(3, body.Records.Length);
    }

    [Fact]
    public async Task GetTraffic_NoFilters_ReturnsAllRecords()
    {
        _store!.Add(new HttpTrafficRecord { RequestMethod = "GET", RequestUri = "https://a.com" });
        _store!.Add(new HttpTrafficRecord { RequestMethod = "POST", RequestUri = "https://b.com" });

        var response = await _client!.GetAsync("/_httplens/api/traffic");
        var body = await response.Content.ReadFromJsonAsync<TrafficListDto>();

        Assert.NotNull(body);
        Assert.Equal(2, body!.Total);
    }

    /// <summary>DTO for deserializing the traffic list API response.</summary>
    private sealed record TrafficListDto(int Total, HttpTrafficRecord[] Records);
}
