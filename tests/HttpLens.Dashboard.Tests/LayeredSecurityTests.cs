using System.Net;
using HttpLens.Dashboard.Tests.Helpers;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace HttpLens.Dashboard.Tests;

/// <summary>
/// Layered Security — combining multiple security features & Middleware Execution Order verification.
/// Verifies that security layers are evaluated in the correct order:
///   EnabledGuard → IpAllowlist → ApiKey → ASP.NET Auth → Endpoint
/// and that earlier layers short-circuit before later ones are reached.
/// </summary>
public class LayeredSecurityTests
{
    /// <summary>Localhost with correct API key → 200.</summary>
    [Fact]
    public async Task ApiKeyPlusIp_LocalhostWithKey_Returns200()
    {
        using var host = await CreateHostHelper.CreateHost(
            apiKey: "my-key",
            allowedIpRanges: ["127.0.0.1", "::1"],
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "my-key");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Localhost without API key → 401 (IP passes, key fails).</summary>
    [Fact]
    public async Task ApiKeyPlusIp_LocalhostWithoutKey_Returns401()
    {
        using var host = await CreateHostHelper.CreateHost(
            apiKey: "my-key",
            allowedIpRanges: ["127.0.0.1", "::1"],
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Blocked IP with correct API key → 403 (IP blocked first).</summary>
    [Fact]
    public async Task ApiKeyPlusIp_BlockedIpWithKey_Returns403()
    {
        using var host = await CreateHostHelper.CreateHost(
            apiKey: "my-key",
            allowedIpRanges: ["127.0.0.1", "::1"],
            remoteIp: IPAddress.Parse("192.168.1.1"));
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "my-key");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Dashboard: localhost with correct key → 200.</summary>
    [Fact]
    public async Task ApiKeyPlusIp_Dashboard_LocalhostWithKey_Returns200()
    {
        using var host = await CreateHostHelper.CreateHost(
            apiKey: "my-key",
            allowedIpRanges: ["127.0.0.1", "::1"],
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens?key=my-key");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Dashboard: blocked IP with key → 403.</summary>
    [Fact]
    public async Task ApiKeyPlusIp_Dashboard_BlockedIpWithKey_Returns403()
    {
        using var host = await CreateHostHelper.CreateHost(
            apiKey: "my-key",
            allowedIpRanges: ["127.0.0.1", "::1"],
            remoteIp: IPAddress.Parse("10.0.0.5"));
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens?key=my-key");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>IsEnabled=false with correct API key → 404 (master switch wins).</summary>
    [Fact]
    public async Task IsEnabledPlusApiKey_DisabledWithKey_Returns404()
    {
        using var host = await CreateHostHelper.CreateHost(
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
        using var host = await CreateHostHelper.CreateHost(
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
        using var host = await CreateHostHelper.CreateHost(
            isEnabled: false,
            apiKey: "my-key",
            remoteIp: IPAddress.Loopback,
            addSampleEndpoint: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/weather");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Dev env, localhost, correct key → 200.</summary>
    [Fact]
    public async Task AllLayers_AllowedEnvLocalHostCorrectKey_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostAllLayers(
            environment: "Development",
            allowedEnvironments: ["Development", "Staging"],
            apiKey: "super-secret",
            allowedIpRanges: ["127.0.0.1", "::1"],
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "super-secret");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Dev env, localhost, no key → 401.</summary>
    [Fact]
    public async Task AllLayers_AllowedEnvLocalHostNoKey_Returns401()
    {
        using var host = await CreateHostHelper.CreateHostAllLayers(
            environment: "Development",
            allowedEnvironments: ["Development", "Staging"],
            apiKey: "super-secret",
            allowedIpRanges: ["127.0.0.1", "::1"],
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Dev env, blocked IP, correct key → 403.</summary>
    [Fact]
    public async Task AllLayers_AllowedEnvBlockedIpCorrectKey_Returns403()
    {
        using var host = await CreateHostHelper.CreateHostAllLayers(
            environment: "Development",
            allowedEnvironments: ["Development", "Staging"],
            apiKey: "super-secret",
            allowedIpRanges: ["127.0.0.1", "::1"],
            remoteIp: IPAddress.Parse("192.168.1.1"));
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "super-secret");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Production env (excluded) → dashboard not mapped (404).</summary>
    [Fact]
    public async Task AllLayers_ExcludedEnv_Returns404()
    {
        // When env is excluded, AddHttpLens skips registration, dashboard is not mapped
        using var host = await CreateHostHelper.CreateHostAllLayers(
            environment: "Production",
            allowedEnvironments: ["Development", "Staging"],
            apiKey: "super-secret",
            allowedIpRanges: ["127.0.0.1", "::1"],
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
        using var host = await CreateHostHelper.CreateHostAllLayers(
            environment: "Production",
            allowedEnvironments: ["Development", "Staging"],
            apiKey: "super-secret",
            allowedIpRanges: ["127.0.0.1", "::1"],
            remoteIp: IPAddress.Loopback,
            addSampleEndpoint: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/weather");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// IsEnabled=false → returns 404, never reaches ApiKey layer.
    /// Verify no authentication-related response (no JSON error body about API key).
    /// </summary>
    [Fact]
    public async Task Order_DisabledNeverReachesApiKey_Returns404()
    {
        using var host = await CreateHostHelper.CreateHost(
            isEnabled: false,
            apiKey: "my-key",
            allowedIpRanges: ["127.0.0.1", "::1"],
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        // Should NOT contain API key error — request was stopped at EnabledGuard
        Assert.DoesNotContain("API key", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// IsEnabled=true, blocked IP → returns 403, never reaches ApiKey.
    /// Ensures IP check runs before API key check.
    /// </summary>
    [Fact]
    public async Task Order_BlockedIpNeverReachesApiKey_Returns403Not401()
    {
        using var host = await CreateHostHelper.CreateHost(
            isEnabled: true,
            apiKey: "my-key",
            allowedIpRanges: ["10.0.0.0/8"],
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
    /// IsEnabled=true, allowed IP, wrong key → returns 401, not 403.
    /// Proves IP passed, then ApiKey check rejected.
    /// </summary>
    [Fact]
    public async Task Order_AllowedIpWrongKey_Returns401Not403()
    {
        using var host = await CreateHostHelper.CreateHost(
            isEnabled: true,
            apiKey: "my-key",
            allowedIpRanges: ["127.0.0.1", "::1"],
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
        using var host = await CreateHostHelper.CreateHost(
            isEnabled: false,
            apiKey: "my-key",
            allowedIpRanges: ["10.0.0.0/8"],
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
        using var host = await CreateHostHelper.CreateHost(
            isEnabled: true,
            apiKey: "my-key",
            allowedIpRanges: ["10.0.0.0/8"],
            remoteIp: IPAddress.Parse("192.168.1.1"));
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens?key=my-key");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}