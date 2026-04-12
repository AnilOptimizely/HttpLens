using HttpLens.Core.Configuration;
using HttpLens.Core.Interceptors;
using HttpLens.Core.Models;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Extensions;
using HttpLens.Dashboard.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace HttpLens.Dashboard.Tests;

/// <summary>
/// IsEnabled Master Switch tests.
/// Verifies that when IsEnabled=false, the dashboard returns 404, API returns 404,
/// traffic capture is bypassed, and app routes continue to work normally.
/// Also tests runtime toggle via IOptionsMonitor.
/// </summary>
public class IsEnabledMasterSwitchTests
{
    
    /// <summary>Dashboard returns 200 when enabled.</summary>
    [Fact]
    public async Task Dashboard_IsEnabledTrue_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostSampleEndpoint(isEnabled: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>API returns 200 with traffic data when enabled.</summary>
    [Fact]
    public async Task Api_IsEnabledTrue_Returns200WithTrafficData()
    {
        using var host = await CreateHostHelper.CreateHostSampleEndpoint(isEnabled: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TrafficListDto>();
        Assert.NotNull(body);
    }

    /// <summary>Traffic is captured when enabled.</summary>
    [Fact]
    public async Task TrafficCapture_IsEnabledTrue_RecordsCaptured()
    {
        using var host = await CreateHostHelper.CreateHostSampleEndpoint(isEnabled: true);
        var store = host.Services.GetRequiredService<ITrafficStore>();

        store.Add(new HttpTrafficRecord
        {
            RequestMethod = "GET",
            RequestUri = "https://example.com/test"
        });

        Assert.Single(store.GetAll());
    }

    /// <summary>Dashboard returns 404 when disabled.</summary>
    [Fact]
    public async Task Dashboard_IsEnabledFalse_Returns404()
    {
        using var host = await CreateHostHelper.CreateHostSampleEndpoint(isEnabled: false);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>API returns 404 when disabled.</summary>
    [Fact]
    public async Task Api_IsEnabledFalse_Returns404()
    {
        using var host = await CreateHostHelper.CreateHostSampleEndpoint(isEnabled: false);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>App routes still work when HttpLens is disabled.</summary>
    [Fact]
    public async Task AppRoutes_IsEnabledFalse_StillWork()
    {
        using var host = await CreateHostHelper.CreateHostSampleEndpoint(isEnabled: false, addSampleEndpoint: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/weather");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test: Dashboard sub-paths also return 404 when disabled.</summary>
    [Fact]
    public async Task DashboardSubPaths_IsEnabledFalse_Return404()
    {
        using var host = await CreateHostHelper.CreateHostSampleEndpoint(isEnabled: false);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/js/some.js");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Test: Handler captures traffic when enabled.</summary>
    [Fact]
    public async Task Handler_IsEnabledTrue_CapturesTraffic()
    {
        var (handler, store, _) = BuildHandler(isEnabled: true);
        var client = CreateClient(handler);

        await client.GetAsync("https://example.com/test");

        Assert.Single(store.GetAll());
        Assert.Equal("GET", store.GetAll()[0].RequestMethod);
    }

    /// <summary>Test: Handler passes through without capturing when disabled.</summary>
    [Fact]
    public async Task Handler_IsEnabledFalse_PassesThroughWithoutCapturing()
    {
        var (handler, store, _) = BuildHandler(isEnabled: false);
        var client = CreateClient(handler);

        var result = await client.GetAsync("https://example.com/test");

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Empty(store.GetAll());
    }

    /// <summary>Test: Toggle from enabled → disabled at runtime stops capture.</summary>
    [Fact]
    public async Task Handler_RuntimeToggle_EnabledToDisabled_StopsCapture()
    {
        var (handler, store, monitor) = BuildHandler(isEnabled: true);
        var client = CreateClient(handler);

        // Enabled: should capture
        await client.GetAsync("https://example.com/first");
        Assert.Single(store.GetAll());

        // Disable at runtime
        monitor.Set(new HttpLensOptions { IsEnabled = false });

        // Disabled: should pass through
        await client.GetAsync("https://example.com/second");
        Assert.Single(store.GetAll()); // still only one record
    }

    /// <summary>Test: Toggle from disabled → enabled at runtime resumes capture.</summary>
    [Fact]
    public async Task Handler_RuntimeToggle_DisabledToEnabled_ResumesCapture()
    {
        var (handler, store, monitor) = BuildHandler(isEnabled: false);
        var client = CreateClient(handler);

        // Disabled: should not capture
        await client.GetAsync("https://example.com/first");
        Assert.Empty(store.GetAll());

        // Enable at runtime
        monitor.Set(new HttpLensOptions { IsEnabled = true });

        // Enabled: should capture
        await client.GetAsync("https://example.com/second");
        Assert.Single(store.GetAll());
        Assert.Equal("https://example.com/second", store.GetAll()[0].RequestUri);
    }

    /// <summary>Test: Dashboard endpoint filter respects runtime toggle.</summary>
    [Fact]
    public async Task ApiEndpointFilter_RuntimeToggle_Returns404WhenDisabled()
    {
        // Start enabled
        var optionsHolder = new MutableOptionsHolder { IsEnabled = true };
        using var host = await CreateHostWithMutableOptions(optionsHolder);
        var client = host.GetTestClient();

        // Enabled → 200
        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Disable at runtime
        optionsHolder.IsEnabled = false;

        // Disabled → 404
        response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Re-enable at runtime
        optionsHolder.IsEnabled = true;

        // Enabled again → 200
        response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    

    /// <summary>
    /// Creates a host where IsEnabled can be changed at runtime via the holder object.
    /// </summary>
    private static async Task<IHost> CreateHostWithMutableOptions(MutableOptionsHolder holder)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton<IOptionsMonitor<HttpLensOptions>>(
                        new DelegatingOptionsMonitor(holder));
                    services.AddSingleton<ITrafficStore>(sp =>
                    {
                        var opts = Options.Create(new HttpLensOptions());
                        return new InMemoryTrafficStore(opts);
                    });
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

    // ═══════════════════════════════════════════════════════════════
    // Helpers — DelegatingHandler tests
    // ═══════════════════════════════════════════════════════════════

    private sealed class FakeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private T _value;
        public TestOptionsMonitor(T value) => _value = value;
        public T CurrentValue => _value;
        public T Get(string? name) => _value;
        public void Set(T value) => _value = value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private static (HttpLensDelegatingHandler handler, ITrafficStore store, TestOptionsMonitor<HttpLensOptions> monitor)
        BuildHandler(bool isEnabled)
    {
        var options = new HttpLensOptions { IsEnabled = isEnabled };
        var store = new InMemoryTrafficStore(Options.Create(options));
        var monitor = new TestOptionsMonitor<HttpLensOptions>(options);
        var handler = new HttpLensDelegatingHandler(store, monitor);
        return (handler, store, monitor);
    }

    private static HttpClient CreateClient(HttpLensDelegatingHandler handler)
    {
        handler.InnerHandler = new FakeHandler();
        return new HttpClient(handler);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers — Mutable options for runtime toggle integration tests
    // ═══════════════════════════════════════════════════════════════

    private sealed class MutableOptionsHolder
    {
        public bool IsEnabled { get; set; } = true;
    }

    private sealed class DelegatingOptionsMonitor : IOptionsMonitor<HttpLensOptions>
    {
        private readonly MutableOptionsHolder _holder;
        public DelegatingOptionsMonitor(MutableOptionsHolder holder) => _holder = holder;
        public HttpLensOptions CurrentValue => new() { IsEnabled = _holder.IsEnabled };
        public HttpLensOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<HttpLensOptions, string?> listener) => null;
    }

    private sealed record TrafficListDto(int Total, HttpTrafficRecord[] Records);
}