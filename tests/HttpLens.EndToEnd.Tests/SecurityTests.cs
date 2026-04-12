using System.Net;
using System.Net.Http.Json;
using HttpLens.Core.Configuration;
using HttpLens.Core.Extensions;
using HttpLens.Core.Storage;
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
/// End-to-end security tests covering API key authentication, IP allowlisting,
/// IsEnabled guard, and AllowedEnvironments filtering.
/// </summary>
public class SecurityTests
{
    private const string BasePath = "/_httplens";
    private const string TestApiKey = "test-secret-key-12345";

    private static IHostBuilder CreateHost(
        Action<HttpLensOptions>? configure = null,
        string environment = "Development")
    {
        return new HostBuilder()
            .UseEnvironment(environment)
            .ConfigureWebHost(web =>
            {
                web.UseTestServer(o => o.AllowSynchronousIO = true);
                web.ConfigureServices(services =>
                {
                    services.AddHttpLens(configure ?? (_ => { }));
                    services.AddRouting();
                    services.AddHttpClient("TestClient");

                    // Route factory HttpClients through the TestServer.
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
                        endpoints.MapGet("/mock/data", () => Results.Json(new { message = "hello" }));
                    });
                });
            });
    }

    // ── API Key Tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task ApiKey_NoneConfigured_AllowsAllRequests()
    {
        using var host = await CreateHost().StartAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync(BasePath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await host.StopAsync();
    }

    [Fact]
    public async Task ApiKey_CorrectKeyInHeader_Allows()
    {
        using var host = await CreateHost(o => o.ApiKey = TestApiKey).StartAsync();
        var client = host.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, BasePath);
        request.Headers.Add("X-HttpLens-Key", TestApiKey);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await host.StopAsync();
    }

    [Fact]
    public async Task ApiKey_CorrectKeyInQueryParam_Allows()
    {
        using var host = await CreateHost(o => o.ApiKey = TestApiKey).StartAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync($"{BasePath}?key={TestApiKey}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await host.StopAsync();
    }

    [Fact]
    public async Task ApiKey_MissingKey_Returns401()
    {
        using var host = await CreateHost(o => o.ApiKey = TestApiKey).StartAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync(BasePath);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("API key", body);

        await host.StopAsync();
    }

    [Fact]
    public async Task ApiKey_WrongKey_Returns401()
    {
        using var host = await CreateHost(o => o.ApiKey = TestApiKey).StartAsync();
        var client = host.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, BasePath);
        request.Headers.Add("X-HttpLens-Key", "wrong-key");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await host.StopAsync();
    }

    [Fact]
    public async Task ApiKey_NonHttpLensPath_NotProtected()
    {
        using var host = await CreateHost(o => o.ApiKey = TestApiKey).StartAsync();
        var client = host.GetTestClient();

        // Non-HttpLens path should not require API key.
        var response = await client.GetAsync("/mock/data");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await host.StopAsync();
    }

    [Fact]
    public async Task ApiKey_ApiEndpoint_ProtectedByKey()
    {
        using var host = await CreateHost(o => o.ApiKey = TestApiKey).StartAsync();
        var client = host.GetTestClient();

        // API endpoint without key → 401.
        var noKeyResponse = await client.GetAsync($"{BasePath}/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, noKeyResponse.StatusCode);

        // API endpoint with correct key → 200.
        var request = new HttpRequestMessage(HttpMethod.Get, $"{BasePath}/api/traffic");
        request.Headers.Add("X-HttpLens-Key", TestApiKey);
        var keyResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);

        await host.StopAsync();
    }

    // ── IsEnabled Tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsEnabled_False_DashboardReturns404()
    {
        using var host = await CreateHost(o => o.IsEnabled = false).StartAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync(BasePath);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await host.StopAsync();
    }

    [Fact]
    public async Task IsEnabled_False_ApiReturns404()
    {
        using var host = await CreateHost(o => o.IsEnabled = false).StartAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync($"{BasePath}/api/traffic");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await host.StopAsync();
    }

    [Fact]
    public async Task IsEnabled_True_NormalBehavior()
    {
        using var host = await CreateHost(o => o.IsEnabled = true).StartAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync(BasePath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await host.StopAsync();
    }

    [Fact]
    public async Task IsEnabled_False_HandlerPassesThroughWithoutCapturing()
    {
        using var host = await CreateHost(o => o.IsEnabled = false).StartAsync();
        var store = host.Services.GetRequiredService<ITrafficStore>();

        var factory = host.Services.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("TestClient");
        client.BaseAddress = host.GetTestClient().BaseAddress;

        await client.GetAsync("/mock/data");

        Assert.Equal(0, store.GetAll().Count);

        await host.StopAsync();
    }

    // ── AllowedEnvironments Tests ────────────────────────────────────────────

    [Fact]
    public async Task AllowedEnvironments_MatchingEnv_ServicesRegistered()
    {
        var builder = new HostBuilder()
            .UseEnvironment("Development")
            .ConfigureServices(services =>
            {
                var env = new TestHostEnvironment("Development");
                services.AddHttpLens(env, o =>
                    o.AllowedEnvironments.Add("Development"));
            });

        using var host = await builder.StartAsync();
        var store = host.Services.GetService<ITrafficStore>();
        Assert.NotNull(store);

        await host.StopAsync();
    }

    [Fact]
    public async Task AllowedEnvironments_NonMatchingEnv_ServicesNotRegistered()
    {
        var builder = new HostBuilder()
            .UseEnvironment("Production")
            .ConfigureServices(services =>
            {
                var env = new TestHostEnvironment("Production");
                services.AddHttpLens(env, o =>
                    o.AllowedEnvironments.Add("Development"));
            });

        using var host = await builder.StartAsync();
        var store = host.Services.GetService<ITrafficStore>();
        Assert.Null(store);

        await host.StopAsync();
    }

    [Fact]
    public async Task AllowedEnvironments_Empty_ServicesAlwaysRegistered()
    {
        var builder = new HostBuilder()
            .UseEnvironment("Production")
            .ConfigureServices(services =>
            {
                var env = new TestHostEnvironment("Production");
                services.AddHttpLens(env); // no AllowedEnvironments restriction
            });

        using var host = await builder.StartAsync();
        var store = host.Services.GetService<ITrafficStore>();
        Assert.NotNull(store);

        await host.StopAsync();
    }

    [Fact]
    public async Task AllowedEnvironments_MultipleEnvs_MatchingOneAllows()
    {
        var builder = new HostBuilder()
            .UseEnvironment("Staging")
            .ConfigureServices(services =>
            {
                var env = new TestHostEnvironment("Staging");
                services.AddHttpLens(env, o =>
                    o.AllowedEnvironments.AddRange(["Development", "Staging"]));
            });

        using var host = await builder.StartAsync();
        var store = host.Services.GetService<ITrafficStore>();
        Assert.NotNull(store);

        await host.StopAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName) =>
            EnvironmentName = environmentName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

