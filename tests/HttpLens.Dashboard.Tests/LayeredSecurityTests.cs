using System.Net;
using HttpLens.Core.Configuration;
using HttpLens.Core.Extensions;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace HttpLens.Dashboard.Tests;

/// <summary>
/// Phase 8: Layered Security — combining multiple security features.
/// Phase 9: Middleware Execution Order verification.
/// Verifies that security layers are evaluated in the correct order:
///   EnabledGuard → IpAllowlist → ApiKey → ASP.NET Auth → Endpoint
/// and that earlier layers short-circuit before later ones are reached.
/// </summary>
public class LayeredSecurityTests
{
    // ═══════════════════════════════════════════════════════════════
    // 8.1 — API Key + IP Allowlist
    // ══════════════════════════════════════════════════════════════���

    /// <summary>Test 64: Localhost with correct API key → 200.</summary>
    [Fact]
    public async Task ApiKeyPlusIp_LocalhostWithKey_Returns200()
    {
        using var host = await CreateHost(
            apiKey: "my-key",
            allowedIpRanges: new[] { "127.0.0.1", "::1" },
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "my-key");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 65: Localhost without API key → 401 (IP passes, key fails).</summary>
    [Fact]
    public async Task ApiKeyPlusIp_LocalhostWithoutKey_Returns401()
    {
        using var host = await CreateHost(
            apiKey: "my-key",
            allowedIpRanges: new[] { "127.0.0.1", "::1" },
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test 66: Blocked IP with correct API key → 403 (IP blocked first).</summary>
    [Fact]
    public async Task ApiKeyPlusIp_BlockedIpWithKey_Returns403()
    {
        using var host = await CreateHost(
            apiKey: "my-key",
            allowedIpRanges: new[] { "127.0.0.1", "::1" },
            remoteIp: IPAddress.Parse("192.168.1.1"));
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "my-key");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // 8.1 — Dashboard SPA routes (same layered checks)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Dashboard: localhost with correct key → 200.</summary>
    [Fact]
    public async Task ApiKeyPlusIp_Dashboard_LocalhostWithKey_Returns200()
    {
        using var host = await CreateHost(
            apiKey: "my-key",
            allowedIpRanges: new[] { "127.0.0.1", "::1" },
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens?key=my-key");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Dashboard: blocked IP with key → 403.</summary>
    [Fact]
    public async Task ApiKeyPlusIp_Dashboard_BlockedIpWithKey_Returns403()
    {
        using var host = await CreateHost(
            apiKey: "my-key",
            allowedIpRanges: new[] { "127.0.0.1", "::1" },
            remoteIp: IPAddress.Parse("10.0.0.5"));
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens?key=my-key");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // 8.2 — IsEnabled + API Key
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 67: IsEnabled=false with correct API key → 404 (master switch wins).</summary>
    [Fact]
    public async Task IsEnabledPlusApiKey_DisabledWithKey_Returns404()
    {
        using var host = await CreateHost(
            isEnabled: false,
            apiKey: "my-key",
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "my-key");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Dashboard: IsEnabled=false with correct key in query → 404.</summary>
    [Fact]
    public async Task IsEnabledPlusApiKey_Dashboard_DisabledWithKey_Returns404()
    {
        using var host = await CreateHost(
            isEnabled: false,
            apiKey: "my-key",
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens?key=my-key");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>IsEnabled=false, app routes still work.</summary>
    [Fact]
    public async Task IsEnabledPlusApiKey_Disabled_AppRoutesWork()
    {
        using var host = await CreateHost(
            isEnabled: false,
            apiKey: "my-key",
            remoteIp: IPAddress.Loopback,
            addSampleEndpoint: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/weather");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // 8.3 — All Layers Combined
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 68: Dev env, localhost, correct key → 200.</summary>
    [Fact]
    public async Task AllLayers_AllowedEnvLocalHostCorrectKey_Returns200()
    {
        using var host = await CreateHostAllLayers(
            environment: "Development",
            allowedEnvironments: new[] { "Development", "Staging" },
            apiKey: "super-secret",
            allowedIpRanges: new[] { "127.0.0.1", "::1" },
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "super-secret");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 69: Dev env, localhost, no key → 401.</summary>
    [Fact]
    public async Task AllLayers_AllowedEnvLocalHostNoKey_Returns401()
    {
        using var host = await CreateHostAllLayers(
            environment: "Development",
            allowedEnvironments: new[] { "Development", "Staging" },
            apiKey: "super-secret",
            allowedIpRanges: new[] { "127.0.0.1", "::1" },
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test 70: Dev env, blocked IP, correct key → 403.</summary>
    [Fact]
    public async Task AllLayers_AllowedEnvBlockedIpCorrectKey_Returns403()
    {
        using var host = await CreateHostAllLayers(
            environment: "Development",
            allowedEnvironments: new[] { "Development", "Staging" },
            apiKey: "super-secret",
            allowedIpRanges: new[] { "127.0.0.1", "::1" },
            remoteIp: IPAddress.Parse("192.168.1.1"));
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "super-secret");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Test 71: Production env (excluded) → dashboard not mapped (404).</summary>
    [Fact]
    public async Task AllLayers_ExcludedEnv_Returns404()
    {
        // When env is excluded, AddHttpLens skips registration, dashboard is not mapped
        using var host = await CreateHostAllLayers(
            environment: "Production",
            allowedEnvironments: new[] { "Development", "Staging" },
            apiKey: "super-secret",
            allowedIpRanges: new[] { "127.0.0.1", "::1" },
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "super-secret");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Production env (excluded) — app routes still work.</summary>
    [Fact]
    public async Task AllLayers_ExcludedEnv_AppRoutesWork()
    {
        using var host = await CreateHostAllLayers(
            environment: "Production",
            allowedEnvironments: new[] { "Development", "Staging" },
            apiKey: "super-secret",
            allowedIpRanges: new[] { "127.0.0.1", "::1" },
            remoteIp: IPAddress.Loopback,
            addSampleEndpoint: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/weather");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 9: Middleware Execution Order
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Test 72: IsEnabled=false → returns 404, never reaches ApiKey layer.
    /// Verify no authentication-related response (no JSON error body about API key).
    /// </summary>
    [Fact]
    public async Task Order_DisabledNeverReachesApiKey_Returns404()
    {
        using var host = await CreateHost(
            isEnabled: false,
            apiKey: "my-key",
            allowedIpRanges: new[] { "127.0.0.1", "::1" },
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        // Should NOT contain API key error — request was stopped at EnabledGuard
        Assert.DoesNotContain("API key", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test 73: IsEnabled=true, blocked IP → returns 403, never reaches ApiKey.
    /// Ensures IP check runs before API key check.
    /// </summary>
    [Fact]
    public async Task Order_BlockedIpNeverReachesApiKey_Returns403Not401()
    {
        using var host = await CreateHost(
            isEnabled: true,
            apiKey: "my-key",
            allowedIpRanges: new[] { "10.0.0.0/8" },
            remoteIp: IPAddress.Parse("192.168.1.1"));
        var client = host.GetTestClient();
        // Provide WRONG key to prove we never get 401
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "wrong-key");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Access denied", body);
        Assert.DoesNotContain("API key", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test 74: IsEnabled=true, allowed IP, wrong key → returns 401, not 403.
    /// Proves IP passed, then ApiKey check rejected.
    /// </summary>
    [Fact]
    public async Task Order_AllowedIpWrongKey_Returns401Not403()
    {
        using var host = await CreateHost(
            isEnabled: true,
            apiKey: "my-key",
            allowedIpRanges: new[] { "127.0.0.1", "::1" },
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "wrong-key");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("API key", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Order: Disabled short-circuits before IP check.
    /// Blocked IP + disabled → 404, not 403.
    /// </summary>
    [Fact]
    public async Task Order_DisabledBlockedIp_Returns404Not403()
    {
        using var host = await CreateHost(
            isEnabled: false,
            apiKey: "my-key",
            allowedIpRanges: new[] { "10.0.0.0/8" },
            remoteIp: IPAddress.Parse("192.168.1.1"));
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Order: Dashboard SPA — same order applies.
    /// Blocked IP → 403, even with correct API key.
    /// </summary>
    [Fact]
    public async Task Order_Dashboard_BlockedIpWithKey_Returns403()
    {
        using var host = await CreateHost(
            isEnabled: true,
            apiKey: "my-key",
            allowedIpRanges: new[] { "10.0.0.0/8" },
            remoteIp: IPAddress.Parse("192.168.1.1"));
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens?key=my-key");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper: Standard host (no AllowedEnvironments)
    // ═══════════════════════════════════════════════════════════════

    private static async Task<IHost> CreateHost(
        bool isEnabled = true,
        string? apiKey = null,
        string[]? allowedIpRanges = null,
        IPAddress? remoteIp = null,
        bool addSampleEndpoint = false)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.Configure<HttpLensOptions>(opts =>
                    {
                        opts.IsEnabled = isEnabled;
                        opts.ApiKey = apiKey;
                        if (allowedIpRanges != null)
                            opts.AllowedIpRanges = new List<string>(allowedIpRanges);
                    });
                    services.AddSingleton<ITrafficStore>(sp =>
                    {
                        var opts = sp.GetRequiredService<IOptions<HttpLensOptions>>();
                        return new InMemoryTrafficStore(opts);
                    });
                    services.AddRouting();
                });
                web.Configure(app =>
                {
                    // Set mock IP if specified
                    if (remoteIp != null)
                    {
                        app.Use(async (context, next) =>
                        {
                            context.Connection.RemoteIpAddress = remoteIp;
                            await next();
                        });
                    }

                    app.UseRouting();
                    app.UseEndpoints(ep =>
                    {
                        ep.MapHttpLensDashboard();
                        if (addSampleEndpoint)
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

    // ═══════════════════════════════════════════════════════════════
    // Helper: Host with AllowedEnvironments
    // ═══════════════════════════════════════════════════════════════

    private static async Task<IHost> CreateHostAllLayers(
        string environment,
        string[] allowedEnvironments,
        string apiKey,
        string[] allowedIpRanges,
        IPAddress remoteIp,
        bool addSampleEndpoint = false)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.UseEnvironment(environment);
                web.ConfigureServices((ctx, services) =>
                {
                    services.AddHttpLens(ctx.HostingEnvironment, opts =>
                    {
                        opts.AllowedEnvironments = new List<string>(allowedEnvironments);
                        opts.ApiKey = apiKey;
                        opts.AllowedIpRanges = new List<string>(allowedIpRanges);
                    });
                    services.AddRouting();
                });
                web.Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        context.Connection.RemoteIpAddress = remoteIp;
                        await next();
                    });

                    app.UseRouting();
                    app.UseEndpoints(ep =>
                    {
                        // Only map dashboard if services were registered
                        if (app.ApplicationServices.GetService<ITrafficStore>() != null)
                        {
                            ep.MapHttpLensDashboard();
                        }

                        if (addSampleEndpoint)
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
}