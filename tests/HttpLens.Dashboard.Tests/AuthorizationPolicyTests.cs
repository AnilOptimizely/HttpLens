using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using HttpLens.Core.Configuration;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Extensions;
using HttpLens.Dashboard.Tests.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using Xunit;

namespace HttpLens.Dashboard.Tests;

/// <summary>
/// ASP.NET Core Authorization Policy tests.
/// Uses a custom TestAuthHandler to simulate authenticated/unauthenticated requests
/// with specific roles, verifying that RequireAuthorization() is applied correctly
/// to HttpLens dashboard and API routes.
/// </summary>
public class AuthorizationPolicyTests
{
    /// <summary>Test 50: Dashboard — unauthenticated → 401</summary>
    [Fact]
    public async Task Dashboard_Unauthenticated_Returns401()
    {
        using var host = await CreateHostWithAuthPolicy(authorizationPolicy: "HttpLensAccess");
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test 51: Dashboard — authenticated with Admin role → 200</summary>
    [Fact]
    public async Task Dashboard_AuthenticatedAdmin_Returns200()
    {
        using var host = await CreateHostWithAuthPolicy(authorizationPolicy: "HttpLensAccess");
        var client = host.GetTestClient();

        client.DefaultRequestHeaders.Add("X-Test-User", "admin");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 52: Dashboard — authenticated with "User" role (not Admin) → 403</summary>
    [Fact]
    public async Task Dashboard_AuthenticatedNonAdmin_Returns403()
    {
        using var host = await CreateHostWithAuthPolicy(authorizationPolicy: "HttpLensAccess");
        var client = host.GetTestClient();

        client.DefaultRequestHeaders.Add("X-Test-User", "viewer");
        client.DefaultRequestHeaders.Add("X-Test-Role", "User");

        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Test 53: API — unauthenticated → 401</summary>
    [Fact]
    public async Task Api_Unauthenticated_Returns401()
    {
        using var host = await CreateHostWithAuthPolicy(authorizationPolicy: "HttpLensAccess");
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test 54: API — authenticated Admin → 200</summary>
    [Fact]
    public async Task Api_AuthenticatedAdmin_Returns200()
    {
        using var host = await CreateHostWithAuthPolicy(authorizationPolicy: "HttpLensAccess");
        var client = host.GetTestClient();

        client.DefaultRequestHeaders.Add("X-Test-User", "admin");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 55: Non-HttpLens route — unauthenticated → 200 (not protected)</summary>
    [Fact]
    public async Task NonHttpLensRoute_Unauthenticated_Returns200()
    {
        using var host = await CreateHostWithAuthPolicy(
            authorizationPolicy: "HttpLensAccess",
            addSampleEndpoint: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/weather");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 56: No AuthorizationPolicy → dashboard loads without auth</summary>
    [Fact]
    public async Task Dashboard_NoPolicy_Returns200WithoutAuth()
    {
        using var host = await CreateHostWithAuthPolicy(authorizationPolicy: null);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test 56b: No AuthorizationPolicy → API works without auth</summary>
    [Fact]
    public async Task Api_NoPolicy_Returns200WithoutAuth()
    {
        using var host = await CreateHostWithAuthPolicy(authorizationPolicy: null);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Admin with correct API key → 200</summary>
    [Fact]
    public async Task Api_AdminWithApiKey_Returns200()
    {
        using var host = await CreateHostWithAuthPolicy(
            authorizationPolicy: "HttpLensAccess",
            apiKey: "secret-123");
        var client = host.GetTestClient();

        client.DefaultRequestHeaders.Add("X-Test-User", "admin");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        client.DefaultRequestHeaders.Add("X-HttpLens-Key", "secret-123");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Admin without API key → 401 (API key check fails)</summary>
    [Fact]
    public async Task Api_AdminWithoutApiKey_Returns401()
    {
        using var host = await CreateHostWithAuthPolicy(
            authorizationPolicy: "HttpLensAccess",
            apiKey: "secret-123");
        var client = host.GetTestClient();

        client.DefaultRequestHeaders.Add("X-Test-User", "admin");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper: Create TestServer with auth + optional policy
    // ═══════════════════════════════════════════════════════════════

    private static async Task<IHost> CreateHostWithAuthPolicy(
        string? authorizationPolicy,
        string? apiKey = null,
        bool addSampleEndpoint = false)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);

                    services.AddAuthorization(opts =>
                    {
                        opts.AddPolicy("HttpLensAccess", policy =>
                            policy.RequireRole("Admin"));
                    });

                    services.Configure<HttpLensOptions>(opts =>
                    {
                        opts.IsEnabled = true;
                        opts.AuthorizationPolicy = authorizationPolicy;
                        opts.ApiKey = apiKey;
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
                    // CORRECT ORDER: Routing → Authentication → Authorization → Endpoints
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
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