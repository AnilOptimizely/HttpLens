using System.Net;
using HttpLens.Dashboard.Middleware;
using HttpLens.Dashboard.Tests.Helpers;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace HttpLens.Dashboard.Tests;

/// <summary>
/// IP Allowlist tests.
/// Unit tests for the static IsIpAllowed method and integration tests using TestServer
/// with mock RemoteIpAddress to verify end-to-end IP filtering.
/// </summary>
public class IpAllowlistTests
{
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

    [Fact]
    public void IsIpAllowed_IPv4MappedIPv6_NormalisedCorrectly()
    {
        // ::ffff:127.0.0.1 should match allowlist entry "127.0.0.1"
        var mappedIp = IPAddress.Parse("::ffff:127.0.0.1");
        Assert.True(IpAllowlistMiddleware.IsIpAllowed(mappedIp, ["127.0.0.1"]));
    }

    [Fact]
    public void IsIpAllowed_IPv4MappedIPv6_MatchesCidr()
    {
        // ::ffff:10.0.1.5 should match CIDR "10.0.0.0/8"
        var mappedIp = IPAddress.Parse("::ffff:10.0.1.5");
        Assert.True(IpAllowlistMiddleware.IsIpAllowed(mappedIp, ["10.0.0.0/8"]));
    }

    [Fact]
    public void IsIpAllowed_EmptyList_ReturnsFalse()
    {
        // Note: The middleware itself skips the check when empty (allows all).
        // The static method returns false for an empty list — the middleware
        // handles the "empty = allow all" logic before calling IsIpAllowed.
        var result = IpAllowlistMiddleware.IsIpAllowed(IPAddress.Parse("1.2.3.4"), []);
        Assert.False(result);
    }

   
    /// <summary>Test: Request from 10.0.1.5 with AllowedIpRanges=["10.0.0.0/8"] → 200</summary>
    [Fact]
    public async Task Dashboard_AllowedCidrRange_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostWithIpAllowlist(
            allowedRanges: ["10.0.0.0/8"],
            remoteIp: IPAddress.Parse("10.0.1.5"));

        var client = host.GetTestClient();
        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test: Request from 192.168.1.1 with AllowedIpRanges=["10.0.0.0/8"] → 403</summary>
    [Fact]
    public async Task Dashboard_BlockedIp_Returns403()
    {
        using var host = await CreateHostHelper.CreateHostWithIpAllowlist(
            allowedRanges: ["10.0.0.0/8"],
            remoteIp: IPAddress.Parse("192.168.1.1"));

        var client = host.GetTestClient();
        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Test: Empty AllowedIpRanges → request from any IP returns 200</summary>
    [Fact]
    public async Task Dashboard_EmptyAllowlist_AllowsAllIps()
    {
        using var host = await CreateHostHelper.CreateHostWithIpAllowlist(
            allowedRanges: [],
            remoteIp: IPAddress.Parse("203.0.113.50"));

        var client = host.GetTestClient();
        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test: Non-HttpLens route from blocked IP → still 200 (not protected)</summary>
    [Fact]
    public async Task NonHttpLensRoute_BlockedIp_StillReturns200()
    {
        using var host = await CreateHostHelper.CreateHostWithIpAllowlist(
            allowedRanges: ["10.0.0.0/8"],
            remoteIp: IPAddress.Parse("192.168.1.1"),
            addSampleEndpoint: true);

        var client = host.GetTestClient();
        var response = await client.GetAsync("/api/weather");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test: Localhost with AllowedIpRanges=["127.0.0.1","::1"] → 200</summary>
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task Dashboard_LocalhostAllowed_Returns200(string ip)
    {
        using var host = await CreateHostHelper.CreateHostWithIpAllowlist(
            allowedRanges: ["127.0.0.1", "::1"],
            remoteIp: IPAddress.Parse(ip));

        var client = host.GetTestClient();
        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Multiple CIDR ranges — allowed IP in second range</summary>
    [Fact]
    public async Task Dashboard_MultipleRanges_MatchesAny()
    {
        using var host = await CreateHostHelper.CreateHostWithIpAllowlist(
            allowedRanges: ["10.0.0.0/8", "172.16.0.0/12"],
            remoteIp: IPAddress.Parse("172.16.5.10"));

        var client = host.GetTestClient();
        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}