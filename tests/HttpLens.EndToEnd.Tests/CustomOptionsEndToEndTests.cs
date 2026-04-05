using System.Net;
using System.Net.Http.Json;
using System.Text;
using HttpLens.Core.Extensions;
using HttpLens.Core.Models;
using HttpLens.Dashboard.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Xunit;

namespace HttpLens.EndToEnd.Tests;

/// <summary>
/// End-to-end tests for custom configuration options: disabled body capture,
/// custom sensitive headers, max records eviction, and custom dashboard path.
/// </summary>
public class CustomOptionsEndToEndTests
{
    private const string BasePath = "/_custom-httplens";

    private static IHostBuilder CreateHost(Action<HttpLens.Core.Configuration.HttpLensOptions>? configure = null)
    {
        return new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer(o => o.AllowSynchronousIO = true);
                web.ConfigureServices(services =>
                {
                    services.AddHttpLens(configure ?? (_ => { }));
                    services.AddRouting();
                    services.AddHttpClient("TestClient");

                    // Route factory-created HttpClients through the TestServer.
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
    }

    private static HttpClient CreateInterceptedClient(IHost host, HttpClient dashboardClient)
    {
        var factory = host.Services.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("TestClient");
        client.BaseAddress = dashboardClient.BaseAddress;
        return client;
    }

    [Fact]
    public async Task BodyCaptureDisabled_DoesNotStoreRequestOrResponseBody()
    {
        using var host = await CreateHost(opts =>
        {
            opts.CaptureRequestBody = false;
            opts.CaptureResponseBody = false;
        }).StartAsync();

        var dashboardClient = host.GetTestClient();
        using var client = CreateInterceptedClient(host, dashboardClient);

        var content = new StringContent("{\"payload\":\"data\"}", Encoding.UTF8, "application/json");
        await client.PostAsync("/mock/echo", content);

        var trafficResponse = await dashboardClient.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.NotNull(traffic);

        var record = Assert.Single(traffic!.Records, r => r.RequestUri!.Contains("/mock/echo"));
        Assert.Null(record.RequestBody);
        Assert.Null(record.ResponseBody);

        await host.StopAsync();
    }

    [Fact]
    public async Task CustomSensitiveHeaders_AreMasked()
    {
        using var host = await CreateHost(opts =>
        {
            opts.SensitiveHeaders.Add("X-Custom-Secret");
        }).StartAsync();

        var dashboardClient = host.GetTestClient();
        using var client = CreateInterceptedClient(host, dashboardClient);

        var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
        request.Headers.TryAddWithoutValidation("X-Custom-Secret", "my-very-secret-value-12345");
        await client.SendAsync(request);

        var trafficResponse = await dashboardClient.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.NotNull(traffic);

        var record = Assert.Single(traffic!.Records, r => r.RequestUri!.Contains("/mock/data"));
        Assert.True(record.RequestHeaders.ContainsKey("X-Custom-Secret"));
        var masked = record.RequestHeaders["X-Custom-Secret"].First();
        Assert.DoesNotContain("my-very-secret-value-12345", masked);
        Assert.Contains("••••••••", masked);

        await host.StopAsync();
    }

    [Fact]
    public async Task MaxStoredRecords_EvictsOldestRecords()
    {
        using var host = await CreateHost(opts =>
        {
            opts.MaxStoredRecords = 2;
        }).StartAsync();

        var dashboardClient = host.GetTestClient();
        using var client = CreateInterceptedClient(host, dashboardClient);

        // Generate 3 requests, but only 2 should be stored.
        await client.GetAsync("/mock/data");
        await client.GetAsync("/mock/data");
        await client.GetAsync("/mock/data");

        var trafficResponse = await dashboardClient.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.NotNull(traffic);
        Assert.Equal(2, traffic!.Total);

        await host.StopAsync();
    }

    [Fact]
    public async Task CustomDashboardPath_ServesCorrectly()
    {
        using var host = await CreateHost().StartAsync();
        var dashboardClient = host.GetTestClient();

        // The dashboard should be served at the custom path.
        var response = await dashboardClient.GetAsync(BasePath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.ToString());

        // The API should also be at the custom path.
        var apiResponse = await dashboardClient.GetAsync($"{BasePath}/api/traffic");
        Assert.Equal(HttpStatusCode.OK, apiResponse.StatusCode);

        await host.StopAsync();
    }

    /// <summary>DTO for deserializing the traffic list API response.</summary>
    private sealed record TrafficListDto(int Total, HttpTrafficRecord[] Records);
}
