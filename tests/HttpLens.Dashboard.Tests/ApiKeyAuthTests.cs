using HttpLens.Core.Configuration;
using HttpLens.Core.Models;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Extensions;
using HttpLens.Dashboard.Tests.Helpers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using System.Net;
using Xunit;

namespace HttpLens.Dashboard.Tests;

/// <summary>
/// API Key Authentication tests.
/// Verifies that when ApiKey is configured, all HttpLens routes require authentication
/// via X-HttpLens-Key header or ?key= query parameter.
/// Also verifies non-HttpLens routes are not affected and that removing the key (dev mode)
/// allows unrestricted access.
/// </summary>
public class ApiKeyAuthTests
{
    private const string TestApiKey = "test-secret-key-2026";

    /// <summary>Test 24: Dashboard with no key → 401.</summary>
    [Fact]
    public async Task Dashboard_NoKey_Returns401()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test 25: Dashboard with correct key in query → 200.</summary>
    [Fact]
    public async Task Dashboard_CorrectKeyInQuery_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey);
        var client = host.GetTestClient();

        var response = await client.GetAsync($"/_httplens?key={TestApiKey}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 26: Dashboard with wrong key → 401.</summary>
    [Fact]
    public async Task Dashboard_WrongKey_Returns401()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens?key=wrong-key");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test 27: API with correct X-HttpLens-Key header → 200.</summary>
    [Fact]
    public async Task Api_CorrectHeader_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", TestApiKey);

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 28: API with wrong header value → 401.</summary>
    [Fact]
    public async Task Api_WrongHeader_Returns401()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "wrong");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test 29: API with no header → 401.</summary>
    [Fact]
    public async Task Api_NoHeader_Returns401()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test 30: Export curl with correct header → 200.</summary>
    [Fact]
    public async Task Api_ExportCurl_CorrectHeader_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey);
        var store = host.Services.GetRequiredService<ITrafficStore>();
        store.Add(new HttpTrafficRecord
        {
            RequestMethod = "GET",
            RequestUri = "https://example.com/test"
        });
        var recordId = store.GetAll()[0].Id;

        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", TestApiKey);

        var response = await client.GetAsync($"/_httplens/api/traffic/{recordId}/export/curl");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("curl", body);
    }

    /// <summary>Test 31: DELETE with correct header → 204.</summary>
    [Fact]
    public async Task Api_DeleteTraffic_CorrectHeader_Returns204()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey);
        var store = host.Services.GetRequiredService<ITrafficStore>();
        store.Add(new HttpTrafficRecord { RequestMethod = "GET" });

        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", TestApiKey);

        var response = await client.DeleteAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(store.GetAll());
    }

    /// <summary>Test 32: DELETE without key → 401.</summary>
    [Fact]
    public async Task Api_DeleteTraffic_NoKey_Returns401()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey);
        var client = host.GetTestClient();

        var response = await client.DeleteAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test 33: API with correct ?key= → 200.</summary>
    [Fact]
    public async Task Api_CorrectQueryKey_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey);
        var client = host.GetTestClient();

        var response = await client.GetAsync($"/_httplens/api/traffic?key={TestApiKey}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 34: API with wrong ?key= → 401.</summary>
    [Fact]
    public async Task Api_WrongQueryKey_Returns401()
    {
        using var host =await CreateHostHelper.CreateHostWithApiKey(TestApiKey);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic?key=wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test 35: App route not affected by API key.</summary>
    [Fact]
    public async Task AppRoute_WithApiKeyConfigured_NotAffected()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey, addSampleEndpoint: true);
        var client = host.GetTestClient();

        // No API key header — app routes should work
        var response = await client.GetAsync("/api/weather");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 36: Another app route not affected.</summary>
    [Fact]
    public async Task AnotherAppRoute_WithApiKeyConfigured_NotAffected()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey, addSampleEndpoint: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 42: Dashboard loads without auth when no API key configured.</summary>
    [Fact]
    public async Task Dashboard_NoApiKey_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(apiKey: null);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 43: API works without auth when no API key configured.</summary>
    [Fact]
    public async Task Api_NoApiKey_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(apiKey: null);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Correct header overrides wrong query key.</summary>
    [Fact]
    public async Task Api_CorrectHeaderOverridesWrongQuery_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", TestApiKey);

        var response = await client.GetAsync("/_httplens/api/traffic?key=wrong");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>401 response has JSON error body.</summary>
    [Fact]
    public async Task Api_Unauthorized_ReturnsJsonErrorBody()
    {
        using var host = await CreateHostHelper.CreateHostWithApiKey(TestApiKey);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("error", body);
        Assert.Contains("API key", body, StringComparison.OrdinalIgnoreCase);
    } 
}
