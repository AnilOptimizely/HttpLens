using System.Net;
using HttpLens.Dashboard.Tests.Helpers;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace HttpLens.Dashboard.Tests;

public class TrafficHubEndpointTests
{
    [Fact]
    public async Task HubNegotiate_DefaultConfiguration_ReturnsSuccessStatus()
    {
        using var host = await CreateHostHelper.CreateHost();
        var client = host.GetTestClient();

        var response = await client.PostAsync("/_httplens/hub/negotiate?negotiateVersion=1", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HubNegotiate_ApiKeyMissing_ReturnsUnauthorized()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey("my-key");
        var client = host.GetTestClient();

        var response = await client.PostAsync("/_httplens/hub/negotiate?negotiateVersion=1", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HubNegotiate_BlockedIp_ReturnsForbidden()
    {
        using var host = await CreateHostHelper.CreateHost(
            apiKey: null,
            allowedIpRanges: ["127.0.0.1"],
            remoteIp: IPAddress.Parse("192.168.1.33"));
        var client = host.GetTestClient();

        var response = await client.PostAsync("/_httplens/hub/negotiate?negotiateVersion=1", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HubNegotiate_IsEnabledFalse_ReturnsNotFound()
    {
        using var host = await CreateHostHelper.CreateHost(isEnabled: false);
        var client = host.GetTestClient();

        var response = await client.PostAsync("/_httplens/hub/negotiate?negotiateVersion=1", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
