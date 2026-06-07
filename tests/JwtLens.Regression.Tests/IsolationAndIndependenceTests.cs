using System.Net;
using System.Net.Http.Headers;
using HttpLens.Core.Extensions;
using HttpLens.Core.Models;
using HttpLens.Core.Storage;
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
/// Category 6: Isolation &amp; Independence — verifies that HttpLens and JwtLens
/// operate independently and don't interfere with each other.
/// </summary>
public class IsolationAndIndependenceTests : IAsyncLifetime
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

    [Fact]
    public async Task ClearJwtEvents_DoesNotAffectTrafficRecords()
    {
        // Clear stores first
        await _dashboardClient!.DeleteAsync($"{BasePath}/api/traffic");
        var jwtStore = _host!.Services.GetRequiredService<IJwtEventStore>();
        jwtStore.Clear();

        // Populate both stores
        using var client = CreateInterceptedClient();
        var token = TestJwtHelper.CreateToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await client.SendAsync(request);

        var trafficStore = _host.Services.GetRequiredService<ITrafficStore>();

        Assert.True(trafficStore.Count > 0);
        Assert.True(jwtStore.Count > 0);

        // Clear JWT events
        jwtStore.Clear();

        // Traffic records should remain
        Assert.True(trafficStore.Count > 0);
        Assert.Equal(0, jwtStore.Count);
    }

    [Fact]
    public async Task ClearTrafficRecords_DoesNotAffectJwtEvents()
    {
        var jwtStore = _host!.Services.GetRequiredService<IJwtEventStore>();
        jwtStore.Clear();
        await _dashboardClient!.DeleteAsync($"{BasePath}/api/traffic");

        // Populate both stores
        using var client = CreateInterceptedClient();
        var token = TestJwtHelper.CreateToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await client.SendAsync(request);

        var trafficStore = _host!.Services.GetRequiredService<ITrafficStore>();

        Assert.True(trafficStore.Count > 0);
        Assert.True(jwtStore.Count > 0);

        // Clear traffic via API
        await _dashboardClient.DeleteAsync($"{BasePath}/api/traffic");

        // JWT events should remain
        Assert.True(jwtStore.Count > 0);
        Assert.Equal(0, trafficStore.Count);
    }

    [Fact]
    public async Task JwtLensDisabled_DoesNotAffectHttpLensCapture()
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer(o => o.AllowSynchronousIO = true);
                web.ConfigureServices(services =>
                {
                    services.AddHttpLens();
                    services.AddJwtLens(o => o.IsEnabled = false);
                    services.AddRouting();
                    services.AddHttpClient("TestClient");

                    services.ConfigureAll<HttpClientFactoryOptions>(factoryOptions =>
                    {
                        factoryOptions.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                        {
                            var server = handlerBuilder.Services.GetRequiredService<IServer>();
                            if (server is TestServer testServer)
                                handlerBuilder.PrimaryHandler = testServer.CreateHandler();

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
                        endpoints.MapHttpLensDashboard("/_httplens");
                        endpoints.MapGet("/mock/data", () => Results.Json(new { ok = true }));
                    });
                });
            });

        using var host = await builder.StartAsync();
        using var dashClient = host.GetTestClient();
        var factory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("TestClient");
        client.BaseAddress = dashClient.BaseAddress;

        var token = TestJwtHelper.CreateToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await client.SendAsync(request);

        // HttpLens should still capture traffic
        var trafficResponse = await dashClient.GetAsync("/_httplens/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.True(traffic!.Total > 0);

        // JwtLens should NOT capture (disabled)
        var jwtStore = host.Services.GetRequiredService<IJwtEventStore>();
        Assert.Equal(0, jwtStore.Count);

        await host.StopAsync();
    }

    [Fact]
    public async Task HttpLensOnly_BaselineControlTest_WorksWithoutJwtLens()
    {
        // A host with ONLY HttpLens — simulates "before JwtLens" state
        var builder = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer(o => o.AllowSynchronousIO = true);
                web.ConfigureServices(services =>
                {
                    services.AddHttpLens();
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
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHttpLensDashboard("/_httplens");
                        endpoints.MapGet("/mock/data", () => Results.Json(new { ok = true }));
                    });
                });
            });

        using var host = await builder.StartAsync();
        using var dashClient = host.GetTestClient();
        var factory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("TestClient");
        client.BaseAddress = dashClient.BaseAddress;

        await client.GetAsync("/mock/data");

        var trafficResponse = await dashClient.GetAsync("/_httplens/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.True(traffic!.Total > 0);

        await host.StopAsync();
    }

    [Fact]
    public async Task BothMiddlewares_InPipeline_NoOrderingIssues()
    {
        // Outbound request through factory client (hits both delegating handlers)
        using var client = CreateInterceptedClient();
        var token = TestJwtHelper.CreateToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record TrafficListDto(int Total, HttpTrafficRecord[] Records);
}
