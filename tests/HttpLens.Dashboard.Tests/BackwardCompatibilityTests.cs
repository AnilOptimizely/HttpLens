using System.Net;
using System.Net.Http.Json;
using HttpLens.Core.Configuration;
using HttpLens.Core.Extensions;
using HttpLens.Core.Models;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace HttpLens.Dashboard.Tests;

/// <summary>
/// Phase 2: Backward Compatibility tests.
/// Verifies that the default zero-config setup continues to work
/// after security features were added — no breaking changes.
/// </summary>
public class BackwardCompatibilityTests
{
    // ═══════════════════════════════════════════════════════════════
    // 2.1 — Zero-config: AddHttpLens() with no options
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 1: AddHttpLens() with no parameters compiles and registers services.</summary>
    [Fact]
    public async Task AddHttpLens_NoParameters_RegistersServicesSuccessfully()
    {
        using var host = await CreateDefaultHost();

        // Verify core services are registered
        var store = host.Services.GetService<ITrafficStore>();
        Assert.NotNull(store);

        var options = host.Services.GetRequiredService<IOptionsMonitor<HttpLensOptions>>();
        Assert.NotNull(options.CurrentValue);
    }

    /// <summary>Test 2: Default options have expected values.</summary>
    [Fact]
    public async Task AddHttpLens_DefaultOptions_HaveCorrectValues()
    {
        using var host = await CreateDefaultHost();
        var options = host.Services.GetRequiredService<IOptionsMonitor<HttpLensOptions>>();
        var opts = options.CurrentValue;

        Assert.True(opts.IsEnabled);
        Assert.Null(opts.ApiKey);
        Assert.Null(opts.AuthorizationPolicy);
        Assert.Empty(opts.AllowedIpRanges);
        Assert.Empty(opts.AllowedEnvironments);
        Assert.Equal(500, opts.MaxStoredRecords);
        Assert.Equal("/_httplens", opts.DashboardPath);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2.2 — Dashboard serves HTML with zero config
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 3: GET /_httplens returns 200 with HTML content.</summary>
    [Fact]
    public async Task Dashboard_ZeroConfig_Returns200WithHtml()
    {
        using var host = await CreateDefaultHost();
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.ToString());
    }

    /// <summary>Test 4: GET /_httplens/api/traffic returns 200 with JSON.</summary>
    [Fact]
    public async Task Api_ZeroConfig_Returns200WithJson()
    {
        using var host = await CreateDefaultHost();
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.NotNull(body);
        Assert.Equal(0, body!.Total);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2.3 — Non-HttpLens routes are not affected
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 5: App routes work normally alongside HttpLens.</summary>
    [Fact]
    public async Task AppRoutes_ZeroConfig_WorkNormally()
    {
        using var host = await CreateDefaultHost(addSampleEndpoints: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/weather");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2.4 — AddHttpLens with configure callback still works
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 6: AddHttpLens with configure callback applies settings.</summary>
    [Fact]
    public async Task AddHttpLens_WithConfigureCallback_AppliesSettings()
    {
        using var host = await CreateHostWithOptions(opts =>
        {
            opts.MaxStoredRecords = 100;
            opts.ApiKey = "my-key";
        });

        var options = host.Services.GetRequiredService<IOptionsMonitor<HttpLensOptions>>();
        Assert.Equal(100, options.CurrentValue.MaxStoredRecords);
        Assert.Equal("my-key", options.CurrentValue.ApiKey);
    }

    /// <summary>Test 7: Dashboard with API key requires authentication.</summary>
    [Fact]
    public async Task Dashboard_WithApiKeyConfigured_Requires401()
    {
        using var host = await CreateHostWithOptions(opts =>
        {
            opts.ApiKey = "my-key";
        });
        var client = host.GetTestClient();

        // No key → blocked
        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test 8: Dashboard with API key in query string succeeds.</summary>
    [Fact]
    public async Task Dashboard_WithCorrectApiKeyQueryParam_Returns200()
    {
        using var host = await CreateHostWithOptions(opts =>
        {
            opts.ApiKey = "my-key";
        });
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens?key=my-key");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2.5 — Traffic capture works in default config
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 9: Traffic store captures records when enabled (default).</summary>
    [Fact]
    public async Task TrafficStore_DefaultConfig_CapturesRecords()
    {
        using var host = await CreateDefaultHost();
        var store = host.Services.GetRequiredService<ITrafficStore>();

        store.Add(new HttpTrafficRecord
        {
            RequestMethod = "GET",
            RequestUri = "https://example.com/test"
        });

        Assert.Single(store.GetAll());
    }

    /// <summary>Test 10: DELETE /_httplens/api/traffic clears the store.</summary>
    [Fact]
    public async Task Api_DeleteTraffic_ClearsStore()
    {
        using var host = await CreateDefaultHost();
        var store = host.Services.GetRequiredService<ITrafficStore>();
        var client = host.GetTestClient();

        store.Add(new HttpTrafficRecord { RequestMethod = "GET" });
        Assert.Single(store.GetAll());

        var response = await client.DeleteAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(store.GetAll());
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private static async Task<IHost> CreateDefaultHost(bool addSampleEndpoints = false)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    // Zero-config: just AddHttpLens() with no parameters
                    services.AddHttpLens();
                    services.AddRouting();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(ep =>
                    {
                        ep.MapHttpLensDashboard();

                        if (addSampleEndpoints)
                        {
                            ep.MapGet("/api/weather", () => Results.Ok(new { temp = 20 }));
                        }
                    });
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    private static async Task<IHost> CreateHostWithOptions(Action<HttpLensOptions> configure)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddHttpLens(configure);
                    services.AddRouting();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(ep =>
                    {
                        ep.MapHttpLensDashboard();
                    });
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    private sealed record TrafficListDto(int Total, HttpTrafficRecord[] Records);
}
