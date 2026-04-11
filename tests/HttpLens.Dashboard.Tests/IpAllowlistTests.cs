using System.Net;
using HttpLens.Core.Configuration;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Extensions;
using HttpLens.Dashboard.Middleware;
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
/// Phase 5: IP Allowlist tests.
/// Unit tests for the static IsIpAllowed method and integration tests using TestServer
/// with mock RemoteIpAddress to verify end-to-end IP filtering.
/// </summary>
public class IpAllowlistTests
{
    // ═══════════════════════════════════════════════════════════════
    // 5.1 — IsIpAllowed: Exact IP matching
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("127.0.0.1", new[] { "127.0.0.1", "::1" }, true)]
    [InlineData("::1", new[] { "127.0.0.1", "::1" }, true)]
    [InlineData("192.168.1.1", new[] { "127.0.0.1", "::1" }, false)]
    [InlineData("10.0.0.1", new[] { "127.0.0.1", "::1" }, false)]
    public void IsIpAllowed_ExactMatch(string ip, string[] ranges, bool expected)
    {
        var result = IpAllowlistMiddleware.IsIpAllowed(IPAddress.Parse(ip), ranges);
        Assert.Equal(expected, result);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.2 — IsIpAllowed: CIDR range matching
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("10.0.1.5", new[] { "10.0.0.0/8" }, true)]       // Test 46: inside 10.0.0.0/8
    [InlineData("10.255.255.255", new[] { "10.0.0.0/8" }, true)]  // upper bound of /8
    [InlineData("192.168.1.1", new[] { "10.0.0.0/8" }, false)]    // Test 47: outside 10.0.0.0/8
    [InlineData("11.0.0.1", new[] { "10.0.0.0/8" }, false)]       // just outside /8
    [InlineData("192.168.1.100", new[] { "192.168.1.0/24" }, true)]
    [InlineData("192.168.2.1", new[] { "192.168.1.0/24" }, false)]
    [InlineData("172.16.5.10", new[] { "172.16.0.0/12" }, true)]
    [InlineData("172.32.0.1", new[] { "172.16.0.0/12" }, false)]
    public void IsIpAllowed_CidrRange(string ip, string[] ranges, bool expected)
    {
        var result = IpAllowlistMiddleware.IsIpAllowed(IPAddress.Parse(ip), ranges);
        Assert.Equal(expected, result);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.2 — IsIpAllowed: IPv4-mapped IPv6 normalisation
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void IsIpAllowed_IPv4MappedIPv6_NormalisedCorrectly()
    {
        // ::ffff:127.0.0.1 should match allowlist entry "127.0.0.1"
        var mappedIp = IPAddress.Parse("::ffff:127.0.0.1");
        Assert.True(IpAllowlistMiddleware.IsIpAllowed(mappedIp, new[] { "127.0.0.1" }));
    }

    [Fact]
    public void IsIpAllowed_IPv4MappedIPv6_MatchesCidr()
    {
        // ::ffff:10.0.1.5 should match CIDR "10.0.0.0/8"
        var mappedIp = IPAddress.Parse("::ffff:10.0.1.5");
        Assert.True(IpAllowlistMiddleware.IsIpAllowed(mappedIp, new[] { "10.0.0.0/8" }));
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.3 — IsIpAllowed: Empty allowlist
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void IsIpAllowed_EmptyList_ReturnsFalse()
    {
        // Note: The middleware itself skips the check when empty (allows all).
        // The static method returns false for an empty list — the middleware
        // handles the "empty = allow all" logic before calling IsIpAllowed.
        var result = IpAllowlistMiddleware.IsIpAllowed(IPAddress.Parse("1.2.3.4"), Array.Empty<string>());
        Assert.False(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5.2 — Integration: CIDR range via TestServer with mock IP
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 46: Request from 10.0.1.5 with AllowedIpRanges=["10.0.0.0/8"] → 200</summary>
    [Fact]
    public async Task Dashboard_AllowedCidrRange_Returns200()
    {
        using var host = await CreateHostWithIpAllowlist(
            allowedRanges: new[] { "10.0.0.0/8" },
            remoteIp: IPAddress.Parse("10.0.1.5"));

        var client = host.GetTestClient();
        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 47: Request from 192.168.1.1 with AllowedIpRanges=["10.0.0.0/8"] → 403</summary>
    [Fact]
    public async Task Dashboard_BlockedIp_Returns403()
    {
        using var host = await CreateHostWithIpAllowlist(
            allowedRanges: new[] { "10.0.0.0/8" },
            remoteIp: IPAddress.Parse("192.168.1.1"));

        var client = host.GetTestClient();
        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Test 48: Empty AllowedIpRanges → request from any IP returns 200</summary>
    [Fact]
    public async Task Dashboard_EmptyAllowlist_AllowsAllIps()
    {
        using var host = await CreateHostWithIpAllowlist(
            allowedRanges: Array.Empty<string>(),
            remoteIp: IPAddress.Parse("203.0.113.50"));

        var client = host.GetTestClient();
        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 49: Non-HttpLens route from blocked IP → still 200 (not protected)</summary>
    [Fact]
    public async Task NonHttpLensRoute_BlockedIp_StillReturns200()
    {
        using var host = await CreateHostWithIpAllowlist(
            allowedRanges: new[] { "10.0.0.0/8" },
            remoteIp: IPAddress.Parse("192.168.1.1"),
            addSampleEndpoint: true);

        var client = host.GetTestClient();
        var response = await client.GetAsync("/api/weather");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 44/45: Localhost with AllowedIpRanges=["127.0.0.1","::1"] → 200</summary>
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task Dashboard_LocalhostAllowed_Returns200(string ip)
    {
        using var host = await CreateHostWithIpAllowlist(
            allowedRanges: new[] { "127.0.0.1", "::1" },
            remoteIp: IPAddress.Parse(ip));

        var client = host.GetTestClient();
        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Multiple CIDR ranges — allowed IP in second range</summary>
    [Fact]
    public async Task Dashboard_MultipleRanges_MatchesAny()
    {
        using var host = await CreateHostWithIpAllowlist(
            allowedRanges: new[] { "10.0.0.0/8", "172.16.0.0/12" },
            remoteIp: IPAddress.Parse("172.16.5.10"));

        var client = host.GetTestClient();
        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper: Create a TestServer with specific IP allowlist + mock IP
    // ═══════════════════════════════════════════════════════════════

    private static async Task<IHost> CreateHostWithIpAllowlist(
        string[] allowedRanges,
        IPAddress remoteIp,
        bool addSampleEndpoint = false)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer(o =>
                {
                    // Override the RemoteIpAddress for all test requests.
                    o.AllowSynchronousIO = true;
                });
                web.ConfigureServices(services =>
                {
                    services.Configure<HttpLensOptions>(opts =>
                    {
                        opts.IsEnabled = true;
                        opts.AllowedIpRanges = new List<string>(allowedRanges);
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
                    // Middleware to set the mock RemoteIpAddress on every request.
                    app.Use(async (context, next) =>
                    {
                        context.Connection.RemoteIpAddress = remoteIp;
                        await next();
                    });

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
}