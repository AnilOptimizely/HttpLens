using System.Net;
using System.Net.Http.Json;
using HttpLens.Core.Configuration;
using HttpLens.Core.Extensions;
using HttpLens.Core.Interceptors;
using HttpLens.Core.Storage;
using HttpLens.Dashboard.Extensions;
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
/// Phase 7: AllowedEnvironments tests.
/// Verifies that HttpLens services are only registered when the current hosting
/// environment is in the AllowedEnvironments list.
/// When the environment is not allowed, AddHttpLens returns early with zero registrations.
/// When AllowedEnvironments is empty (default), all environments are permitted.
/// </summary>
public class AllowedEnvironmentsTests
{
    // ═══════════════════════════════════════════════════════════════
    // 7.1 — AllowedEnvironments: ["Development"] in Development
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 57: Services registered when environment matches allowlist.</summary>
    [Fact]
    public void AddHttpLens_EnvironmentMatches_RegistersServices()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Development");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = new List<string> { "Development" };
        });

        var provider = services.BuildServiceProvider();
        var store = provider.GetService<ITrafficStore>();
        Assert.NotNull(store);
    }

    /// <summary>Test 58: DelegatingHandler registered when environment matches.</summary>
    [Fact]
    public void AddHttpLens_EnvironmentMatches_RegistersHandler()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Development");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = new List<string> { "Development" };
        });

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<HttpLensDelegatingHandler>();
        Assert.NotNull(handler);
    }

    // ═══════════════════════════════════════════════════════════════
    // 7.2 — AllowedEnvironments: ["Development"] in Production
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 59: Services NOT registered when environment is not in allowlist.</summary>
    [Fact]
    public void AddHttpLens_EnvironmentNotInList_DoesNotRegisterServices()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Production");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = new List<string> { "Development" };
        });

        var provider = services.BuildServiceProvider();
        var store = provider.GetService<ITrafficStore>();
        Assert.Null(store);
    }

    /// <summary>Test 60: Handler NOT registered when environment excluded.</summary>
    [Fact]
    public void AddHttpLens_EnvironmentNotInList_DoesNotRegisterHandler()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Production");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = new List<string> { "Development" };
        });

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<HttpLensDelegatingHandler>();
        Assert.Null(handler);
    }

    // ═══════════════════════════════════════════════════════════════
    // 7.3 — AllowedEnvironments: empty (default) — all environments
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 61: Empty allowlist → services registered in any environment.</summary>
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

    // ═══════════════════════════════════════════════════════════════
    // 7.4 — Multiple environments in allowlist
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 62: Multiple environments — "Development" matches.</summary>
    [Fact]
    public void AddHttpLens_MultipleAllowed_MatchesDevelopment()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Development");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = new List<string> { "Development", "Staging" };
        });

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ITrafficStore>());
    }

    /// <summary>Test 63: Multiple environments — "Staging" matches.</summary>
    [Fact]
    public void AddHttpLens_MultipleAllowed_MatchesStaging()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Staging");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = new List<string> { "Development", "Staging" };
        });

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ITrafficStore>());
    }

    /// <summary>Test 64: Multiple environments — "Production" not in list → no registration.</summary>
    [Fact]
    public void AddHttpLens_MultipleAllowed_ProductionExcluded()
    {
        var services = new ServiceCollection();
        var env = CreateEnvironment("Production");

        services.AddHttpLens(env, opts =>
        {
            opts.AllowedEnvironments = new List<string> { "Development", "Staging" };
        });

        var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<ITrafficStore>());
    }

    // ═══════════════════════════════════════════════════════════════
    // 7.5 — Case insensitivity
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 65: Environment matching is case-insensitive.</summary>
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
            opts.AllowedEnvironments = new List<string> { allowedEnv };
        });

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ITrafficStore>());
    }

    // ═══════════════════════════════════════════════════════════════
    // 7.6 — Integration: Full pipeline with TestServer
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 66: Dashboard accessible when environment is in allowlist.</summary>
    [Fact]
    public async Task Integration_AllowedEnvironment_DashboardReturns200()
    {
        using var host = await CreateIntegrationHost("Development", new[] { "Development" },
            mapDashboard: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Test 67: App routes work when environment is NOT in allowlist.
    /// When excluded, AddHttpLens skips registration so MapHttpLensDashboard must NOT be called
    /// (ITrafficStore won't be in DI). This mirrors the real-world pattern:
    ///   if (env.IsDevelopment()) app.MapHttpLensDashboard();
    /// </summary>
    [Fact]
    public async Task Integration_ExcludedEnvironment_AppRoutesStillWork()
    {
        using var host = await CreateIntegrationHost("Production", new[] { "Development" },
            mapDashboard: false, addSampleEndpoint: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/api/weather");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Test 67b: When excluded, HttpLens routes are not mapped (404).
    /// </summary>
    [Fact]
    public async Task Integration_ExcludedEnvironment_DashboardNotMapped()
    {
        using var host = await CreateIntegrationHost("Production", new[] { "Development" },
            mapDashboard: false, addSampleEndpoint: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Test 68: Empty AllowedEnvironments in Production → dashboard works.</summary>
    [Fact]
    public async Task Integration_EmptyAllowedEnvironments_DashboardWorksEverywhere()
    {
        using var host = await CreateIntegrationHost("Production", Array.Empty<string>(),
            mapDashboard: true);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/_httplens/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // 7.7 — AddHttpLens without IHostEnvironment still works
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Test 69: AddHttpLens() without environment parameter always registers.</summary>
    [Fact]
    public void AddHttpLens_WithoutEnvironment_AlwaysRegisters()
    {
        var services = new ServiceCollection();

        // Calling the overload without IHostEnvironment
        services.AddHttpLens(opts =>
        {
            opts.AllowedEnvironments = new List<string> { "Development" };
        });

        // AllowedEnvironments is set but not checked by this overload
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ITrafficStore>());
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        return new FakeHostEnvironment { EnvironmentName = environmentName };
    }

    private static async Task<IHost> CreateIntegrationHost(
        string environmentName,
        string[] allowedEnvironments,
        bool mapDashboard = true,
        bool addSampleEndpoint = false)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.UseEnvironment(environmentName);
                web.ConfigureServices((ctx, services) =>
                {
                    services.AddHttpLens(ctx.HostingEnvironment, opts =>
                    {
                        opts.AllowedEnvironments = new List<string>(allowedEnvironments);
                    });
                    services.AddRouting();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(ep =>
                    {
                        // Only map dashboard if services were registered.
                        // This mirrors the real-world pattern:
                        //   if (env.IsDevelopment()) app.MapHttpLensDashboard();
                        if (mapDashboard)
                        {
                            ep.MapHttpLensDashboard();
                        }

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

    /// <summary>Minimal fake IHostEnvironment for unit tests.</summary>
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}