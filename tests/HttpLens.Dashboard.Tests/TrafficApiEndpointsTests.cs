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
    /// <summary>DTO for deserializing the traffic list API response.</summary>
    private sealed record TrafficListDto(int Total, HttpTrafficRecord[] Records);
}
