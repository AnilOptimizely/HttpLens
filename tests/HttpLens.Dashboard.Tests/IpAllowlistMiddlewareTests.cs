using System.Net;
using HttpLens.Dashboard.Middleware;
using Xunit;

namespace HttpLens.Dashboard.Tests;

/// <summary>
/// Unit tests for <see cref="IpAllowlistMiddleware"/> IP matching logic.
/// </summary>
public class IpAllowlistMiddlewareTests
{
    private static bool IsAllowed(string ip, params string[] ranges) =>
        IpAllowlistMiddleware.IsIpAllowed(IPAddress.Parse(ip), ranges);

    [Fact]
    public void ExactIpv4Match_Allowed()
    {
        Assert.True(IsAllowed("127.0.0.1", "127.0.0.1"));
    }

    [Fact]
    public void ExactIpv4_NoMatch_Denied()
    {
        Assert.False(IsAllowed("10.0.0.1", "127.0.0.1"));
    }

    [Fact]
    public void CidrRange_IpInRange_Allowed()
    {
        Assert.True(IsAllowed("10.0.1.5", "10.0.0.0/8"));
    }

    [Fact]
    public void CidrRange_IpOutsideRange_Denied()
    {
        Assert.False(IsAllowed("192.168.1.1", "10.0.0.0/8"));
    }

    [Fact]
    public void CidrRange_Slash24_BorderIp_Allowed()
    {
        Assert.True(IsAllowed("192.168.1.254", "192.168.1.0/24"));
    }

    [Fact]
    public void CidrRange_Slash24_IpOutside_Denied()
    {
        Assert.False(IsAllowed("192.168.2.1", "192.168.1.0/24"));
    }

    [Fact]
    public void Ipv6Loopback_ExactMatch_Allowed()
    {
        Assert.True(IsAllowed("::1", "::1"));
    }

    [Fact]
    public void Ipv4MappedIpv6_NormalisedToIpv4_Allowed()
    {
        // ::ffff:127.0.0.1 should match "127.0.0.1"
        var mappedIpv6 = IPAddress.Parse("::ffff:127.0.0.1");
        Assert.True(IpAllowlistMiddleware.IsIpAllowed(mappedIpv6, new[] { "127.0.0.1" }));
    }

    [Fact]
    public void MultipleRanges_FirstMatchAllows()
    {
        Assert.True(IsAllowed("10.5.6.7", "192.168.0.0/16", "10.0.0.0/8"));
    }

    [Fact]
    public void EmptyRangeList_AlwaysAllowed()
    {
        // The middleware skips the check when the list is empty;
        // IsIpAllowed with an empty list should return false (caller skips the call).
        // We verify the static helper returns false for an empty list.
        Assert.False(IpAllowlistMiddleware.IsIpAllowed(IPAddress.Loopback, Array.Empty<string>()));
    }

    [Fact]
    public void CidrSlash32_ExactHostMatch_Allowed()
    {
        Assert.True(IsAllowed("10.10.10.10", "10.10.10.10/32"));
    }

    [Fact]
    public void CidrSlash0_MatchesAll_Allowed()
    {
        Assert.True(IsAllowed("1.2.3.4", "0.0.0.0/0"));
        Assert.True(IsAllowed("255.255.255.255", "0.0.0.0/0"));
    }
}
