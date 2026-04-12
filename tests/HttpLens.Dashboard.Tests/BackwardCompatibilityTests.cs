using HttpLens.Core.Configuration;
using HttpLens.Core.Models;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Tests.Helpers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using System.Net;
using Xunit;

namespace HttpLens.Dashboard.Tests;

/// <summary>
/// Backward Compatibility tests.
/// Verifies that the default zero-config setup continues to work
/// after security features were added — no breaking changes.
/// </summary>
public class BackwardCompatibilityTests
{ 
    /// <summary>Test : AddHttpLens() with no parameters compiles and registers services.</summary>
    [Fact]
    public async Task AddHttpLens_NoParameters_RegistersServicesSuccessfully()
    {
        using var host = await CreateHostHelper.CreateDefaultHost();

        // Verify core services are registered
        var store = host.Services.GetService<ITrafficStore>();
        Assert.NotNull(store);

        var options = host.Services.GetRequiredService<IOptionsMonitor<HttpLensOptions>>();
        Assert.NotNull(options.CurrentValue);
    }

    /// <summary>Test : Default options have expected values.</summary>
    [Fact]
    public async Task AddHttpLens_DefaultOptions_HaveCorrectValues()
    {
        using var host = await CreateHostHelper.CreateDefaultHost();
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

    /// <summary>Test : GET /_httplens returns 200 with HTML content.</summary>
    [Fact]
    public async Task Dashboard_ZeroConfig_Returns200WithHtml()
    {
        using var host = await CreateHostHelper.CreateDefaultHost();
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.ToString());
    }

    /// <summary>Test : GET /_httplens/api/traffic returns 200 with JSON.</summary>
    [Fact]
    public async Task Api_ZeroConfig_Returns200WithJson()
    {
        using var host = await CreateHostHelper.CreateDefaultHost();
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.NotNull(body);
        Assert.Equal(0, body!.Total);
    }

    /// <summary>Test : App routes work normally alongside HttpLens.</summary>
    [Fact]
    public async Task AppRoutes_ZeroConfig_WorkNormally()
    {
        using var host = await CreateHostHelper.CreateDefaultHost(addSampleEndpoints: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/weather");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test : AddHttpLens with configure callback applies settings.</summary>
    [Fact]
    public async Task AddHttpLens_WithConfigureCallback_AppliesSettings()
    {
        using var host = await CreateHostHelper.CreateHostWithOptions(opts =>
        {
            opts.MaxStoredRecords = 100;
            opts.ApiKey = "my-key";
        });

        var options = host.Services.GetRequiredService<IOptionsMonitor<HttpLensOptions>>();
        Assert.Equal(100, options.CurrentValue.MaxStoredRecords);
        Assert.Equal("my-key", options.CurrentValue.ApiKey);
    }

    /// <summary>Test : Dashboard with API key requires authentication.</summary>
    [Fact]
    public async Task Dashboard_WithApiKeyConfigured_Requires401()
    {
        using var host = await CreateHostHelper.CreateHostWithOptions(opts =>
        {
            opts.ApiKey = "my-key";
        });
        var client = host.GetTestClient();

        // No key → blocked
        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test : Dashboard with API key in query string succeeds.</summary>
    [Fact]
    public async Task Dashboard_WithCorrectApiKeyQueryParam_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostWithOptions(opts =>
        {
            opts.ApiKey = "my-key";
        });
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens?key=my-key");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }


    /// <summary>Test : Traffic store captures records when enabled (default).</summary>
    [Fact]
    public async Task TrafficStore_DefaultConfig_CapturesRecords()
    {
        using var host = await CreateHostHelper.CreateDefaultHost();
        var store = host.Services.GetRequiredService<ITrafficStore>();

        store.Add(new HttpTrafficRecord
        {
            RequestMethod = "GET",
            RequestUri = "https://example.com/test"
        });

        Assert.Single(store.GetAll());
    }

    /// <summary>Test : DELETE /_httplens/api/traffic clears the store.</summary>
    [Fact]
    public async Task Api_DeleteTraffic_ClearsStore()
    {
        using var host = await CreateHostHelper.CreateDefaultHost();
        var store = host.Services.GetRequiredService<ITrafficStore>();
        var client = host.GetTestClient();

        store.Add(new HttpTrafficRecord { RequestMethod = "GET" });
        Assert.Single(store.GetAll());

        var response = await client.DeleteAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(store.GetAll());
    }

    private sealed record TrafficListDto(int Total, HttpTrafficRecord[] Records);
}
