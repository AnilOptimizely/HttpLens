using System.Net;
using System.Text;
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
/// Category 2: HttpLens.Core Traffic Interception Still Works when JwtLens is co-registered.
/// </summary>
public class TrafficInterceptionRegressionTests : IAsyncLifetime
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

                    services.AddHttpClient("TestClient", client =>
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "Regression-Test/1.0");
                    });

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
                            Results.Json(new { message = "hello world" }));

                        endpoints.MapPost("/mock/echo", async context =>
                        {
                            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(body);
                        });
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
    public async Task GetRequest_IsCaptured_WhenBothPackagesRegistered()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        var response = await client.GetAsync("/mock/data");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.NotNull(traffic);

        var record = Assert.Single(traffic!.Records, r => r.RequestUri!.Contains("/mock/data"));
        Assert.Equal("GET", record.RequestMethod);
        Assert.True(record.IsSuccess);
        Assert.Equal(200, record.ResponseStatusCode);
    }

    [Fact]
    public async Task PostRequest_CapturesBodyAndHeaders_WhenBothPackagesRegistered()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        var content = new StringContent("{\"key\":\"value\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/mock/echo", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        var record = Assert.Single(traffic!.Records, r => r.RequestUri!.Contains("/mock/echo"));

        Assert.Equal("POST", record.RequestMethod);
        Assert.NotNull(record.RequestBody);
        Assert.Contains("\"key\"", record.RequestBody);
        Assert.NotNull(record.ResponseBody);
    }

    [Fact]
    public async Task SensitiveHeaders_AreMasked_WhenBothPackagesRegistered()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
        request.Headers.TryAddWithoutValidation("Authorization", "******");
        await client.SendAsync(request);

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        var record = Assert.Single(traffic!.Records, r => r.RequestUri!.Contains("/mock/data"));

        Assert.True(record.RequestHeaders.ContainsKey("Authorization"));
        var maskedValue = record.RequestHeaders["Authorization"].First();
        Assert.DoesNotContain("secret-token-12345", maskedValue);
        Assert.Contains("••••••••", maskedValue);
    }

    [Fact]
    public async Task ResponseHeaders_AreCaptured_WhenBothPackagesRegistered()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        await client.GetAsync("/mock/data");

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        var record = Assert.Single(traffic!.Records, r => r.RequestUri!.Contains("/mock/data"));

        Assert.NotNull(record.ResponseHeaders);
        Assert.True(record.ResponseHeaders.Count > 0);
    }

    [Fact]
    public async Task HttpLensDisabled_DoesNotCapture_JwtLensStillWorks()
    {
        // This test uses a separate host with HttpLens disabled
        var builder = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer(o => o.AllowSynchronousIO = true);
                web.ConfigureServices(services =>
                {
                    services.AddHttpLens(o => o.IsEnabled = false);
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

                            // Wire JwtLens handler
                            var jwtHandler = handlerBuilder.Services.GetRequiredService<JwtLens.Interceptors.JwtLensDelegatingHandler>();
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

        // Make request with JWT
        var token = TestJwtHelper.CreateToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        await client.SendAsync(request);

        // HttpLens traffic store should be empty (disabled — handler is pass-through)
        var trafficStore = host.Services.GetRequiredService<HttpLens.Core.Storage.ITrafficStore>();
        Assert.Equal(0, trafficStore.Count);

        // JwtLens should still have captured the JWT event
        var jwtStore = host.Services.GetRequiredService<JwtLens.Storage.IJwtEventStore>();
        Assert.True(jwtStore.Count > 0);

        await host.StopAsync();
    }

    private sealed record TrafficListDto(int Total, HttpTrafficRecord[] Records);
}
