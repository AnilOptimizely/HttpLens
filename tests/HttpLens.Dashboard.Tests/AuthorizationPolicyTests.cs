using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using HttpLens.Core.Configuration;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Extensions;
using HttpLens.Dashboard.Tests.Helpers;
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
    /// <summary>Test: Dashboard — unauthenticated → 401</summary>
    [Fact]
    public async Task Dashboard_Unauthenticated_Returns401()
    {
        using var host = await CreateHostHelper.CreateHostWithAuthPolicy(authorizationPolicy: "HttpLensAccess");
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test: Dashboard — authenticated with Admin role → 200</summary>
    [Fact]
    public async Task Dashboard_AuthenticatedAdmin_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostWithAuthPolicy(authorizationPolicy: "HttpLensAccess");
        var client = host.GetTestClient();

        client.DefaultRequestHeaders.Add("X-Test-User", "admin");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test: Dashboard — authenticated with "User" role (not Admin) → 403</summary>
    [Fact]
    public async Task Dashboard_AuthenticatedNonAdmin_Returns403()
    {
        using var host = await CreateHostHelper.CreateHostWithAuthPolicy(authorizationPolicy: "HttpLensAccess");
        var client = host.GetTestClient();

        client.DefaultRequestHeaders.Add("X-Test-User", "viewer");
        client.DefaultRequestHeaders.Add("X-Test-Role", "User");

        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Test: API — unauthenticated → 401</summary>
    [Fact]
    public async Task Api_Unauthenticated_Returns401()
    {
        using var host = await CreateHostHelper.CreateHostWithAuthPolicy(authorizationPolicy: "HttpLensAccess");
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Test: API — authenticated Admin → 200</summary>
    [Fact]
    public async Task Api_AuthenticatedAdmin_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostWithAuthPolicy(authorizationPolicy: "HttpLensAccess");
        var client = host.GetTestClient();

        client.DefaultRequestHeaders.Add("X-Test-User", "admin");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test: Non-HttpLens route — unauthenticated → 200 (not protected)</summary>
    [Fact]
    public async Task NonHttpLensRoute_Unauthenticated_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostWithAuthPolicy(
            authorizationPolicy: "HttpLensAccess",
            addSampleEndpoint: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/weather");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test: No AuthorizationPolicy → dashboard loads without auth</summary>
    [Fact]
    public async Task Dashboard_NoPolicy_Returns200WithoutAuth()
    {
        using var host = await CreateHostHelper.CreateHostWithAuthPolicy(authorizationPolicy: null);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test: No AuthorizationPolicy → API works without auth</summary>
    [Fact]
    public async Task Api_NoPolicy_Returns200WithoutAuth()
    {
        using var host = await CreateHostHelper.CreateHostWithAuthPolicy(authorizationPolicy: null);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Admin with correct API key → 200</summary>
    [Fact]
    public async Task Api_AdminWithApiKey_Returns200()
    {
        using var host = await CreateHostHelper.CreateHostWithAuthPolicy(
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
        using var host = await CreateHostHelper.CreateHostWithAuthPolicy(
            authorizationPolicy: "HttpLensAccess",
            apiKey: "secret-123");
        var client = host.GetTestClient();

        client.DefaultRequestHeaders.Add("X-Test-User", "admin");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}