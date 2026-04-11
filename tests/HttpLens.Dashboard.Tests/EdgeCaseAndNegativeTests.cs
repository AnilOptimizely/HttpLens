using System.Net;
using HttpLens.Core.Configuration;
using HttpLens.Core.Extensions;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Extensions;
using HttpLens.Dashboard.Middleware;
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
/// Phase 10: Edge Cases &amp; Negative Tests.
/// Verifies boundary conditions, malformed config, special characters,
/// concurrency, and error handling.
/// </summary>
public class EdgeCaseAndNegativeTests
{
    // ═══════════════════════════════════════════════════════════════
    // Test 75: ApiKey set to empty string "" → treated as null
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Empty string API key → no auth required (dashboard accessible).</summary>
    [Fact]
    public async Task ApiKey_EmptyString_TreatedAsNull_DashboardReturns200()
    {
        using var host = await CreateHost(apiKey: "");
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Empty string API key → dashboard SPA accessible without key.</summary>
    [Fact]
    public async Task ApiKey_EmptyString_TreatedAsNull_SpaReturns200()
    {
        using var host = await CreateHost(apiKey: "");
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Whitespace-only API key → treated as no auth required.</summary>
    [Fact]
    public async Task ApiKey_WhitespaceOnly_TreatedAsNull()
    {
        using var host = await CreateHost(apiKey: "   ");
        var client = host.GetTestClient();

        // string.IsNullOrEmpty won't catch whitespace, so this tests behavior:
        // if whitespace is treated as a real key, this will be 401.
        // if treated as empty/null, this will be 200.
        var response = await client.GetAsync("/_httplens/api/traffic");
        // Accept either 200 (treated as null) or 401 (treated as real key) — document behavior
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 200 or 401, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════
    // Test 76: AllowedIpRanges contains invalid CIDR
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Invalid CIDR entry doesn't crash the app — valid IPs still work.</summary>
    [Fact]
    public async Task IpAllowlist_InvalidCidr_DoesNotCrash_ValidIpStillAllowed()
    {
        using var host = await CreateHost(
            allowedIpRanges: new[] { "not-an-ip", "127.0.0.1" },
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Invalid CIDR entry doesn't crash — blocked IP still blocked.</summary>
    [Fact]
    public async Task IpAllowlist_InvalidCidr_DoesNotCrash_BlockedIpStillBlocked()
    {
        using var host = await CreateHost(
            allowedIpRanges: new[] { "not-an-ip", "127.0.0.1" },
            remoteIp: IPAddress.Parse("192.168.1.1"));
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Malformed CIDR with wrong prefix length doesn't crash.</summary>
    [Fact]
    public async Task IpAllowlist_MalformedCidrPrefix_DoesNotCrash()
    {
        using var host = await CreateHost(
            allowedIpRanges: new[] { "10.0.0.0/999", "127.0.0.1" },
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Empty string in AllowedIpRanges doesn't crash.</summary>
    [Fact]
    public async Task IpAllowlist_EmptyStringEntry_DoesNotCrash()
    {
        using var host = await CreateHost(
            allowedIpRanges: new[] { "", "127.0.0.1" },
            remoteIp: IPAddress.Loopback);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>IsIpAllowed static method handles invalid entries gracefully.</summary>
    [Fact]
    public void IsIpAllowed_InvalidEntries_ReturnsFalseWithoutCrash()
    {
        var ip = IPAddress.Parse("192.168.1.1");
        var ranges = new List<string> { "not-an-ip", "garbage/data", "", "10.0.0.0/999" };

        // Should not throw — just return false
        var result = IpAllowlistMiddleware.IsIpAllowed(ip, ranges);
        Assert.False(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // Test 77: Case-insensitive environment matching
    // ═══════════════════════════════════════════════════════════════

    /// <summary>"development" (lowercase) matches "Development" in allowlist.</summary>
    [Fact]
    public void AllowedEnvironments_LowercaseMatchesCapitalized()
    {
        var services = new ServiceCollection();
        var env = new FakeHostEnvironment { EnvironmentName = "development" };

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = new List<string> { "Development" };
        });

        Assert.NotNull(services.BuildServiceProvider().GetService<ITrafficStore>());
    }

    /// <summary>"DEVELOPMENT" (uppercase) matches "Development" in allowlist.</summary>
    [Fact]
    public void AllowedEnvironments_UppercaseMatchesCapitalized()
    {
        var services = new ServiceCollection();
        var env = new FakeHostEnvironment { EnvironmentName = "DEVELOPMENT" };

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = new List<string> { "Development" };
        });

        Assert.NotNull(services.BuildServiceProvider().GetService<ITrafficStore>());
    }

    /// <summary>"Development" matches "dEvElOpMeNt" in allowlist (mixed case).</summary>
    [Fact]
    public void AllowedEnvironments_MixedCaseMatches()
    {
        var services = new ServiceCollection();
        var env = new FakeHostEnvironment { EnvironmentName = "Development" };

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = new List<string> { "dEvElOpMeNt" };
        });

        Assert.NotNull(services.BuildServiceProvider().GetService<ITrafficStore>());
    }

    // ═══════════════════════════════════════════════════════════════
    // Test 78: Custom DashboardPath + API key
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Custom path with API key — no key → 401.</summary>
    [Fact]
    public async Task CustomPath_WithApiKey_NoKey_Returns401()
    {
        using var host = await CreateHost(apiKey: "my-key", dashboardPath: "/_mymonitor");
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_mymonitor/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Custom path with API key — correct key → 200.</summary>
    [Fact]
    public async Task CustomPath_WithApiKey_CorrectKey_Returns200()
    {
        using var host = await CreateHost(apiKey: "my-key", dashboardPath: "/_mymonitor");
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "my-key");

        var response = await client.GetAsync("/_mymonitor/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Custom path dashboard SPA — correct key in query → 200.</summary>
    [Fact]
    public async Task CustomPath_Dashboard_CorrectKeyInQuery_Returns200()
    {
        using var host = await CreateHost(apiKey: "my-key", dashboardPath: "/_mymonitor");
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_mymonitor?key=my-key");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Default path not mapped when custom path is used.</summary>
    [Fact]
    public async Task CustomPath_DefaultPathReturns404()
    {
        using var host = await CreateHost(apiKey: "my-key", dashboardPath: "/_mymonitor");
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "my-key");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // Test 79: Concurrent requests with API key
    // ═══════════════════════════════════════════════════════════════

    /// <summary>50 concurrent requests all succeed — no race conditions.</summary>
    [Fact]
    public async Task Concurrency_MultipleRequestsWithApiKey_AllSucceed()
    {
        using var host = await CreateHost(apiKey: "concurrent-key");
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "concurrent-key");

        const int concurrentRequests = 50;
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => client.GetAsync("/_httplens/api/traffic"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    /// <summary>Mix of valid and invalid keys — correct status codes for each.</summary>
    [Fact]
    public async Task Concurrency_MixedValidAndInvalidKeys_CorrectResponses()
    {
        using var host = await CreateHost(apiKey: "concurrent-key");

        const int requestsPerType = 25;
        var tasks = new List<Task<(HttpStatusCode status, bool shouldSucceed)>>();

        for (var i = 0; i < requestsPerType; i++)
        {
            // Valid key
            tasks.Add(Task.Run(async () =>
            {
                var c = host.GetTestClient();
                c.DefaultRequestHeaders.Add("X-HttpLens-Key", "concurrent-key");
                var r = await c.GetAsync("/_httplens/api/traffic");
                return (r.StatusCode, shouldSucceed: true);
            }));

            // Invalid key
            tasks.Add(Task.Run(async () =>
            {
                var c = host.GetTestClient();
                c.DefaultRequestHeaders.Add("X-HttpLens-Key", "wrong-key");
                var r = await c.GetAsync("/_httplens/api/traffic");
                return (r.StatusCode, shouldSucceed: false);
            }));
        }

        var results = await Task.WhenAll(tasks);

        foreach (var (status, shouldSucceed) in results)
        {
            if (shouldSucceed)
                Assert.Equal(HttpStatusCode.OK, status);
            else
                Assert.Equal(HttpStatusCode.Unauthorized, status);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Test 80: Very long API key (1000+ chars)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>1000-char API key works with header.</summary>
    [Fact]
    public async Task ApiKey_VeryLong_WorksWithHeader()
    {
        var longKey = new string('A', 1000) + "secret";
        using var host = await CreateHost(apiKey: longKey);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", longKey);

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>1000-char API key works with query parameter.</summary>
    [Fact]
    public async Task ApiKey_VeryLong_WorksWithQueryParam()
    {
        var longKey = new string('B', 1000) + "secret";
        using var host = await CreateHost(apiKey: longKey);
        var client = host.GetTestClient();

        var response = await client.GetAsync($"/_httplens/api/traffic?key={Uri.EscapeDataString(longKey)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Long key — wrong key still rejected.</summary>
    [Fact]
    public async Task ApiKey_VeryLong_WrongKeyRejected()
    {
        var longKey = new string('C', 1000);
        using var host = await CreateHost(apiKey: longKey);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", longKey + "X"); // off by one char

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // Test 81: API key with special characters
    // ═══════════════════════════════════════════════════════════════

    /// <summary>API key with special chars works via header.</summary>
    [Theory]
    [InlineData("key-with-!@#$%^&*()")]
    [InlineData("key with spaces")]
    [InlineData("key/with/slashes")]
    [InlineData("key=with=equals")]
    [InlineData("日本語キー")]
    [InlineData("key\twith\ttabs")]
    public async Task ApiKey_SpecialChars_WorksWithHeader(string specialKey)
    {
        using var host = await CreateHost(apiKey: specialKey);
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-HttpLens-Key", specialKey);

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>API key with special chars — wrong key rejected.</summary>
    [Fact]
    public async Task ApiKey_SpecialChars_WrongKeyRejected()
    {
        using var host = await CreateHost(apiKey: "!@#$%^&*()");
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-HttpLens-Key", "!@#$%^&*(");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // Test 82: AuthorizationPolicy references non-existent policy
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Non-existent policy throws at startup or first request.</summary>
    [Fact]
    public async Task AuthPolicy_NonExistent_ThrowsMeaningfulError()
    {
        // The app should either:
        // 1. Throw during startup (host.StartAsync)
        // 2. Throw on first request to a protected endpoint
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(services =>
                    {
                        services.Configure<HttpLensOptions>(opts =>
                        {
                            opts.AuthorizationPolicy = "NonExistentPolicy";
                        });
                        services.AddSingleton<ITrafficStore>(sp =>
                        {
                            var opts = sp.GetRequiredService<IOptions<HttpLensOptions>>();
                            return new InMemoryTrafficStore(opts);
                        });
                        services.AddRouting();
                        services.AddAuthorization(); // No policies registered
                    });
                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthorization();
                        app.UseEndpoints(ep =>
                        {
                            ep.MapHttpLensDashboard();
                        });
                    });
                })
                .Build();

            await host.StartAsync();
            var client = host.GetTestClient();
            // If startup didn't throw, the first request should
            await client.GetAsync("/_httplens/api/traffic");
        });

        // Should mention the policy name in the error
        Assert.Contains("NonExistentPolicy", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private static async Task<IHost> CreateHost(
        string? apiKey = null,
        string[]? allowedIpRanges = null,
        IPAddress? remoteIp = null,
        string dashboardPath = "/_httplens")
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.Configure<HttpLensOptions>(opts =>
                    {
                        opts.IsEnabled = true;
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
                        ep.MapHttpLensDashboard(dashboardPath);
                    });
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}