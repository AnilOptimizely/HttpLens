using System.Net;
using System.Text;
using System.Text.Json;
using HttpLens.Core.Extensions;
using HttpLens.Core.Models;
using HttpLens.Dashboard.Extensions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Http;
using Xunit;

namespace HttpLens.EndToEnd.Tests;

/// <summary>
/// End-to-end tests that verify the full HttpLens pipeline:
/// registration → HttpClient interception → traffic storage → dashboard API retrieval → export.
/// Uses TestServer to host both the "application under test" and a mock downstream service.
/// Factory-created HttpClients are wired to route through the TestServer's in-memory handler
/// so that outbound calls are served locally without real network access.
/// </summary>
public class EndToEndPipelineTests : IAsyncLifetime
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
                    services.AddRouting();

                    services.AddHttpClient("TestClient", client =>
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "E2E-Test/1.0");
                    });

                    // Route all factory-created HttpClients through the TestServer
                    // so outbound calls hit the in-memory pipeline instead of the network.
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

                        // Mock downstream endpoints that factory-created clients will call.
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

    /// <summary>Create a factory HttpClient whose requests flow through the HttpLens handler chain.</summary>
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
    public async Task GetRequest_IsCaptured_AndVisibleViaApi()
    {
        await ClearTraffic();

        // Make an outbound GET call through the intercepted factory client.
        using var client = CreateInterceptedClient();
        var response = await client.GetAsync("/mock/data");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Retrieve captured traffic via the dashboard API.
        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        Assert.Equal(HttpStatusCode.OK, trafficResponse.StatusCode);

        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.NotNull(traffic);

        var record = Assert.Single(traffic!.Records, r => r.RequestUri!.Contains("/mock/data"));
        Assert.Equal("GET", record.RequestMethod);
        Assert.True(record.IsSuccess);
        Assert.Equal(200, record.ResponseStatusCode);
        Assert.True(record.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task PostRequest_CapturesBothRequestAndResponseBody()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        var content = new StringContent("{\"key\":\"value\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/mock/echo", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.NotNull(traffic);

        var record = Assert.Single(traffic!.Records, r => r.RequestUri!.Contains("/mock/echo"));
        Assert.Equal("POST", record.RequestMethod);
        Assert.NotNull(record.RequestBody);
        Assert.Contains("\"key\"", record.RequestBody);
        Assert.Contains("\"value\"", record.RequestBody);
        Assert.NotNull(record.ResponseBody);
        Assert.Contains("\"key\"", record.ResponseBody);
    }

    [Fact]
    public async Task GetTrafficById_ReturnsSpecificRecord()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        await client.GetAsync("/mock/data");

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        var record = traffic!.Records.First(r => r.RequestUri!.Contains("/mock/data"));

        // Retrieve by specific ID via the dashboard API.
        var byIdResponse = await _dashboardClient.GetAsync($"{BasePath}/api/traffic/{record.Id}");
        Assert.Equal(HttpStatusCode.OK, byIdResponse.StatusCode);

        var single = await byIdResponse.Content.ReadFromJsonAsync<HttpTrafficRecord>();
        Assert.NotNull(single);
        Assert.Equal(record.Id, single!.Id);
        Assert.Equal(record.RequestUri, single.RequestUri);
    }

    [Fact]
    public async Task ClearTraffic_RemovesAllRecords()
    {
        using var client = CreateInterceptedClient();
        await client.GetAsync("/mock/data");

        var beforeResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var before = await beforeResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.True(before!.Total > 0);

        var clearResponse = await _dashboardClient.DeleteAsync($"{BasePath}/api/traffic");
        Assert.Equal(HttpStatusCode.NoContent, clearResponse.StatusCode);

        var afterResponse = await _dashboardClient.GetAsync($"{BasePath}/api/traffic");
        var after = await afterResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.Equal(0, after!.Total);
    }

    [Fact]
    public async Task ExportCurl_ReturnsValidCurlCommand()
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
        Assert.Contains("-X GET", curl);
    }

    [Fact]
    public async Task ExportCSharp_ReturnsValidCode()
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
        Assert.Contains("HttpRequestMessage", code);
        Assert.Contains("/mock/data", code);
    }

    [Fact]
    public async Task ExportHar_ReturnsValidHarJson()
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
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("log", out var log));
        Assert.True(log.TryGetProperty("entries", out var entries));
        Assert.True(entries.GetArrayLength() > 0);
    }

    [Fact]
    public async Task SensitiveHeaders_AreMasked_InCapturedTraffic()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/mock/data");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer super-secret-token-12345");
        await client.SendAsync(request);

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        var record = Assert.Single(traffic!.Records, r => r.RequestUri!.Contains("/mock/data"));

        Assert.True(record.RequestHeaders.ContainsKey("Authorization"));
        var maskedValue = record.RequestHeaders["Authorization"].First();
        Assert.DoesNotContain("super-secret-token-12345", maskedValue);
        Assert.Contains("••••••••", maskedValue);
    }

    [Fact]
    public async Task DashboardRoot_ServesHtmlContent()
    {
        var response = await _dashboardClient!.GetAsync(BasePath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task MultipleRequests_AllTrafficCaptured()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        await client.GetAsync("/mock/data");
        var postContent = new StringContent("{\"a\":1}", Encoding.UTF8, "application/json");
        await client.PostAsync("/mock/echo", postContent);

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.NotNull(traffic);
        Assert.True(traffic!.Total >= 2);
        Assert.Contains(traffic.Records, r => r.RequestUri!.Contains("/mock/data"));
        Assert.Contains(traffic.Records, r => r.RequestUri!.Contains("/mock/echo"));
    }

    [Fact]
    public async Task ResponseHeaders_AreCaptured()
    {
        await ClearTraffic();

        using var client = CreateInterceptedClient();
        await client.GetAsync("/mock/data");

        var trafficResponse = await _dashboardClient!.GetAsync($"{BasePath}/api/traffic");
        var traffic = await trafficResponse.Content.ReadFromJsonAsync<TrafficListDto>();
        var record = Assert.Single(traffic!.Records, r => r.RequestUri!.Contains("/mock/data"));

        Assert.NotNull(record.ResponseHeaders);
        Assert.True(record.ResponseHeaders.Count > 0);
        Assert.NotNull(record.ResponseContentType);
        Assert.Contains("json", record.ResponseContentType);
    }
    /// <summary>DTO for deserializing the traffic list API response.</summary>
    private sealed record TrafficListDto(int Total, HttpTrafficRecord[] Records);
}
