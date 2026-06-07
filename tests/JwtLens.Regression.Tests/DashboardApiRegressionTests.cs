using System.Net;
using System.Text.Json;
using HttpLens.Core.Extensions;
using HttpLens.Core.Models;
using HttpLens.Dashboard.Extensions;
using JwtLens.Extensions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Http;
using Xunit;

namespace JwtLens.Regression.Tests;

/// <summary>
/// Category 3: HttpLens.Dashboard API Endpoints Still Serve Correctly when JwtLens is co-registered.
/// </summary>
public class DashboardApiRegressionTests : IAsyncLifetime
{
    private IHost? _host;
    private HttpClient? _dashboardClient;
    private const string BasePath = "/_httplens";

    public async Task InitializeAsync()
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer(o => o.AllowSynchronousIO = true);
                web.ConfigureServices(services =>
                {
                    services.AddHttpLens();
                    services.AddJwtLens();
                    services.AddRouting();

                    services.AddHttpClient("TestClient");

                    services.ConfigureAll<HttpClientFactoryOptions>(factoryOptions =>
                    {
                        factoryOptions.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                        {
                            var server = handlerBuilder.Services.GetRequiredService<IServer>();
                            if (server is TestServer testServer)
                                handlerBuilder.PrimaryHandler = testServer.CreateHandler();
                        });
                    });
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseJwtLens();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHttpLensDashboard(BasePath);

                        endpoints.MapGet("/mock/data", () =>
                            Results.Json(new { message = "hello" }));
                    });
                });
            });

        _host = await builder.StartAsync();
        _dashboardClient = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _dashboardClient?.Dispose();
        if (_host is not null) await _host.StopAsync();
        _host?.Dispose();
    }

    private HttpClient CreateInterceptedClient()
    {
        var factory = _host!.Services.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("TestClient");
        client.BaseAddress = _dashboardClient!.BaseAddress;
        return client;
    }

    private async Task ClearTraffic()
    {
        await _dashboardClient!.DeleteAsync($"{BasePath}/api/traffic");
    }

    [Fact]
    public async Task GetTraffic_ReturnsTrafficRecords_NotJwtEvents()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
        request.Headers.TryAddWithoutValidation("Authorization", $"******");
        await client.SendAsync(request);

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        Assert.Equal(HttpStatusCode.OK, trafficResponse.StatusCode);

        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.NotNull(traffic);
        Assert.True(traffic!.Total > 0);

        // Verify these are traffic records, not JWT events
        var record = traffic.Records.First();
        Assert.NotNull(record.RequestMethod);
        Assert.NotNull(record.RequestUri);
    }

    [Fact]
    public async Task GetTrafficById_ReturnsSpecificRecord_WithJwtLensPresent()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        await client.GetAsync("/mock/data");

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        var record = traffic!.Records.First(r => r.RequestUri!.Contains("/mock/data"));

        var byIdResponse = await _dashboardClient.GetAsync($"{BasePath}/api/traffic/{record.Id}");
        Assert.Equal(HttpStatusCode.OK, byIdResponse.StatusCode);

        var single = await byIdResponse.Content.ReadFromJsonAsync<HttpTrafficRecord>();
        Assert.NotNull(single);
        Assert.Equal(record.Id, single!.Id);
    }

    [Fact]
    public async Task DeleteTraffic_ClearsTrafficOnly_NotJwtStore()
    {
        using var client = CreateInterceptedClient();

        // Make request with JWT to populate both stores
        var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
        request.Headers.TryAddWithoutValidation("Authorization", $"******");
        await client.SendAsync(request);

        var jwtStore = _host!.Services.GetRequiredService<JwtLens.Storage.IJwtEventStore>();
        var jwtCountBefore = jwtStore.Count;

        // Clear traffic
        var clearResponse = await _dashboardClient!.DeleteAsync($"{BasePath}/api/traffic");
        Assert.Equal(HttpStatusCode.NoContent, clearResponse.StatusCode);

        // Verify traffic is cleared
        var afterResponse = await _dashboardClient.GetAsync($"{BasePath}/api/traffic");
        var after = await afterResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.Equal(0, after!.Total);

        // Verify JWT store is NOT affected
        Assert.Equal(jwtCountBefore, jwtStore.Count);
    }

    [Fact]
    public async Task ExportCurl_StillWorks_WithJwtLensPresent()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        await client.GetAsync("/mock/data");

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        var record = traffic!.Records.First(r => r.RequestUri!.Contains("/mock/data"));

        var curlResponse = await _dashboardClient.GetAsync($"{BasePath}/api/traffic/{record.Id}/export/curl");
        Assert.Equal(HttpStatusCode.OK, curlResponse.StatusCode);

        var curl = await curlResponse.Content.ReadAsStringAsync();
        Assert.Contains("curl", curl);
        Assert.Contains("/mock/data", curl);
    }

    [Fact]
    public async Task ExportCSharp_StillWorks_WithJwtLensPresent()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        await client.GetAsync("/mock/data");

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        var record = traffic!.Records.First(r => r.RequestUri!.Contains("/mock/data"));

        var csharpResponse = await _dashboardClient.GetAsync($"{BasePath}/api/traffic/{record.Id}/export/csharp");
        Assert.Equal(HttpStatusCode.OK, csharpResponse.StatusCode);

        var code = await csharpResponse.Content.ReadAsStringAsync();
        Assert.Contains("HttpClient", code);
    }

    [Fact]
    public async Task ExportHar_StillWorks_WithJwtLensPresent()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        await client.GetAsync("/mock/data");

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        var record = traffic!.Records.First(r => r.RequestUri!.Contains("/mock/data"));

        var harResponse = await _dashboardClient.GetAsync($"{BasePath}/api/traffic/export/har?ids={record.Id}");
        Assert.Equal(HttpStatusCode.OK, harResponse.StatusCode);

        var harJson = await harResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(harJson);
        Assert.True(doc.RootElement.TryGetProperty("log", out _));
    }

    [Fact]
    public async Task DashboardRoot_ServesHtml_WithJwtLensPresent()
    {
        var response = await _dashboardClient!.GetAsync(BasePath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.ToString());
    }

    private sealed record TrafficListDto(int Total, HttpTrafficRecord[] Records);
}
