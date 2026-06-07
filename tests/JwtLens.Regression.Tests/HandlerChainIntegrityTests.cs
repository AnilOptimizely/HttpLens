using System.Net;
using System.Net.Http.Headers;
using HttpLens.Core.Extensions;
using HttpLens.Core.Models;
using HttpLens.Dashboard.Extensions;
using JwtLens.Extensions;
using JwtLens.Interceptors;
using JwtLens.Storage;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Http;
using Xunit;

namespace JwtLens.Regression.Tests;

/// <summary>
/// Category 4: Handler Chain Integrity — verifies both HttpLens and JwtLens
/// delegating handlers fire correctly for the same outbound requests.
/// </summary>
public class HandlerChainIntegrityTests : IAsyncLifetime
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

                            // Wire JwtLensDelegatingHandler into the pipeline
                            var jwtHandler = handlerBuilder.Services.GetRequiredService<JwtLensDelegatingHandler>();
                            handlerBuilder.AdditionalHandlers.Add(jwtHandler);
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
                        endpoints.MapGet("/mock/data", () => Results.Json(new { ok = true }));
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
    public async Task RequestWithJwt_CapturedByBoth_HttpLensAndJwtLens()
    {
        await ClearTraffic();
        var jwtStore = _host!.Services.GetRequiredService<IJwtEventStore>();
        jwtStore.Clear();

        using var client = CreateInterceptedClient();
        var token = TestJwtHelper.CreateToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await client.SendAsync(request);

        // HttpLens captured the traffic
        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.True(traffic!.Total > 0);
        Assert.Contains(traffic.Records, r => r.RequestUri!.Contains("/mock/data"));

        // JwtLens captured the JWT event
        Assert.True(jwtStore.Count > 0);
        var events = jwtStore.GetAll();
        Assert.Contains(events, e => e.RequestUri != null && e.RequestUri.Contains("/mock/data"));
    }

    [Fact]
    public async Task RequestWithoutJwt_CapturedByHttpLens_IgnoredByJwtLens()
    {
        await ClearTraffic();
        var jwtStore = _host!.Services.GetRequiredService<IJwtEventStore>();
        jwtStore.Clear();

        using var client = CreateInterceptedClient();
        var response = await client.GetAsync("/mock/data");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // HttpLens captured the traffic
        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.True(traffic!.Total > 0);

        // JwtLens should NOT have captured anything (no JWT in request)
        Assert.Equal(0, jwtStore.Count);
    }

    [Fact]
    public async Task AuthorizationHeader_NotSwallowed_BothHandlersCanReadIt()
    {
        await ClearTraffic();
        var jwtStore = _host!.Services.GetRequiredService<IJwtEventStore>();
        jwtStore.Clear();

        using var client = CreateInterceptedClient();
        var token = TestJwtHelper.CreateToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Both handlers should have seen the Authorization header
        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        var record = traffic!.Records.First(r => r.RequestUri!.Contains("/mock/data"));

        // HttpLens captured it (masked)
        Assert.True(record.RequestHeaders.ContainsKey("Authorization"));

        // JwtLens captured the JWT event
        Assert.True(jwtStore.Count > 0);
        var jwtEvent = jwtStore.GetAll().First();
        Assert.True(jwtEvent.DecodedSuccessfully);
    }

    [Fact]
    public async Task MultipleSequentialRequests_NoDisposeIssues()
    {
        await ClearTraffic();
        var jwtStore = _host!.Services.GetRequiredService<IJwtEventStore>();
        jwtStore.Clear();

        using var client = CreateInterceptedClient();

        // Send multiple requests rapidly
        for (int i = 0; i < 10; i++)
        {
            var token = TestJwtHelper.CreateToken();
            var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // All should be captured by both
        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.True(traffic!.Total >= 10);
        Assert.True(jwtStore.Count >= 10);
    }

    private sealed record TrafficListDto(int Total, HttpTrafficRecord[] Records);
}
