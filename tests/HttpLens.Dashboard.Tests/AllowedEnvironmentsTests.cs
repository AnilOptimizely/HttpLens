using HttpLens.Core.Extensions;
using HttpLens.Core.Interceptors;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Tests.Helpers;
using HttpLens.Dashboard.Tests.Models;
using Microsoft.AspNetCore.TestHost;
using System.Net;
using Xunit;

namespace HttpLens.Dashboard.Tests;

/// <summary>
/// AllowedEnvironments tests.
/// Verifies that HttpLens services are only registered when the current hosting
/// environment is in the AllowedEnvironments list.
/// When the environment is not allowed, AddHttpLens returns early with zero registrations.
/// When AllowedEnvironments is empty (default), all environments are permitted.
/// </summary>
public class AllowedEnvironmentsTests
{
    /// <summary>Test: Services registered when environment matches allowlist.</summary>
    [Fact]
    public void AddHttpLens_EnvironmentMatches_RegistersServices()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Development");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = ["Development"];
        });

        var provider = services.BuildServiceProvider();
        var store = provider.GetService<ITrafficStore>();
        Assert.NotNull(store);
    }

    /// <summary>Test: DelegatingHandler registered when environment matches.</summary>
    [Fact]
    public void AddHttpLens_EnvironmentMatches_RegistersHandler()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Development");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = ["Development"];
        });

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<HttpLensDelegatingHandler>();
        Assert.NotNull(handler);
    }

    /// <summary>Test: Services NOT registered when environment is not in allowlist.</summary>
    [Fact]
    public void AddHttpLens_EnvironmentNotInList_DoesNotRegisterServices()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Production");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = ["Development"];
        });

        var provider = services.BuildServiceProvider();
        var store = provider.GetService<ITrafficStore>();
        Assert.Null(store);
    }

    /// <summary>Test: Handler NOT registered when environment excluded.</summary>
    [Fact]
    public void AddHttpLens_EnvironmentNotInList_DoesNotRegisterHandler()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Production");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = ["Development"];
        });

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<HttpLensDelegatingHandler>();
        Assert.Null(handler);
    }

    /// <summary>Test: Empty allowlist → services registered in any environment.</summary>
    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("CustomEnv")]
    public void AddHttpLens_EmptyAllowedEnvironments_RegistersInAnyEnvironment(string envName)
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment(envName);

        services.AddHttpLens(env); // no configure callback — default empty AllowedEnvironments

        var provider = services.BuildServiceProvider();
        var store = provider.GetService<ITrafficStore>();
        Assert.NotNull(store);
    }

    /// <summary>Test: Multiple environments — "Development" matches.</summary>
    [Fact]
    public void AddHttpLens_MultipleAllowed_MatchesDevelopment()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Development");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = ["Development", "Staging"];
        });

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ITrafficStore>());
    }

    /// <summary>Test: Multiple environments — "Staging" matches.</summary>
    [Fact]
    public void AddHttpLens_MultipleAllowed_MatchesStaging()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Staging");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = ["Development", "Staging"];
        });

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ITrafficStore>());
    }

    /// <summary>Test: Multiple environments — "Production" not in list → no registration.</summary>
    [Fact]
    public void AddHttpLens_MultipleAllowed_ProductionExcluded()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Production");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = ["Development", "Staging"];
        });

        var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<ITrafficStore>());
    }

    /// <summary>Test: Environment matching is case-insensitive.</summary>
    [Theory]
    [InlineData("development", "Development")]
    [InlineData("DEVELOPMENT", "Development")]
    [InlineData("Development", "development")]
    [InlineData("Staging", "STAGING")]
    public void AddHttpLens_CaseInsensitive_Matches(string actualEnv, string allowedEnv)
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment(actualEnv);

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = [allowedEnv];
        });

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ITrafficStore>());
    }

    /// <summary>Test: Dashboard accessible when environment is in allowlist.</summary>
    [Fact]
    public async Task Integration_AllowedEnvironment_DashboardReturns200()
    {
        using var host =  await CreateHostHelper.CreateIntegrationHost("Development", ["Development"],
            mapDashboard: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Test: App routes work when environment is NOT in allowlist.
    /// When excluded, AddHttpLens skips registration so MapHttpLensDashboard must NOT be called
    /// (ITrafficStore won't be in DI). This mirrors the real-world pattern:
    ///   if (env.IsDevelopment()) app.MapHttpLensDashboard();
    /// </summary>
    [Fact]
    public async Task Integration_ExcludedEnvironment_AppRoutesStillWork()
    {
        using var host = await CreateHostHelper.CreateIntegrationHost("Production", ["Development"],
            mapDashboard: false, addSampleEndpoint: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/weather");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Test: When excluded, HttpLens routes are not mapped (404).
    /// </summary>
    [Fact]
    public async Task Integration_ExcludedEnvironment_DashboardNotMapped()
    {
        using var host = await CreateHostHelper.CreateIntegrationHost("Production", ["Development"],
            mapDashboard: false, addSampleEndpoint: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Test: Empty AllowedEnvironments in Production → dashboard works.</summary>
    [Fact]
    public async Task Integration_EmptyAllowedEnvironments_DashboardWorksEverywhere()
    {
        using var host = await CreateHostHelper.CreateIntegrationHost("Production", [],
            mapDashboard: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Test: AddHttpLens() without environment parameter always registers.</summary>
    [Fact]
    public void AddHttpLens_WithoutEnvironment_AlwaysRegisters()
    {
        var services = new ServiceCollection();

        // Calling the overload without IHostEnvironment
        services.AddHttpLens(opts =>
        {
            opts.AllowedEnvironments = ["Development"];
        });

        // AllowedEnvironments is set but not checked by this overload
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ITrafficStore>());
    }

    private static FakeHostEnvironment CreateEnvironment(string environmentName)
    {
        return new FakeHostEnvironment { EnvironmentName = environmentName };
    }
}