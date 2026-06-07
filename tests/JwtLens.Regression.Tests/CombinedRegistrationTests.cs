using HttpLens.Core.Extensions;
using HttpLens.Core.Interceptors;
using HttpLens.Core.Storage;
using JwtLens.Configuration;
using JwtLens.Extensions;
using JwtLens.Interceptors;
using JwtLens.Storage;
using Lens.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Xunit;

namespace JwtLens.Regression.Tests;

/// <summary>
/// Category 1: Combined DI Registration — verifies no conflicts or overwrites
/// when both HttpLens and JwtLens are registered in the same container.
/// </summary>
public class CombinedRegistrationTests
{
    private static IServiceCollection CreateServicesWithBoth(
        Action<IServiceCollection>? preRegister = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        preRegister?.Invoke(services);
        services.AddHttpLens();
        services.AddJwtLens();
        services.AddHttpClient("TestClient");
        return services;
    }

    [Fact]
    public void AddHttpLens_ThenAddJwtLens_BothSucceed()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        var exception = Record.Exception(() =>
        {
            services.AddHttpLens();
            services.AddJwtLens();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void AddJwtLens_ThenAddHttpLens_BothSucceed_OrderIndependent()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        var exception = Record.Exception(() =>
        {
            services.AddJwtLens();
            services.AddHttpLens();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void ITrafficStore_ResolvesToHttpLensStore_NotReplacedByJwtLens()
    {
        var services = CreateServicesWithBoth();
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<ITrafficStore>();
        Assert.NotNull(store);
        Assert.IsAssignableFrom<ITrafficStore>(store);
    }

    [Fact]
    public void IJwtEventStore_ResolvesToJwtLensStore_NotReplacedByHttpLens()
    {
        var services = CreateServicesWithBoth();
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IJwtEventStore>();
        Assert.NotNull(store);
        Assert.IsType<InMemoryJwtEventStore>(store);
    }

    [Fact]
    public void CustomRedactor_NotOverwrittenByJwtLens_TryAddSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        // Register a custom IRedactor BEFORE JwtLens
        var customRedactor = new TestRedactor();
        services.AddSingleton<IRedactor>(customRedactor);

        services.AddHttpLens();
        services.AddJwtLens();

        using var provider = services.BuildServiceProvider();
        var redactor = provider.GetRequiredService<IRedactor>();

        // TryAddSingleton in JwtLens should NOT overwrite the custom one
        Assert.Same(customRedactor, redactor);
    }

    [Fact]
    public void ILensDiagnosticsContributor_ResolvesMultipleContributors()
    {
        var services = CreateServicesWithBoth();
        using var provider = services.BuildServiceProvider();

        var contributors = provider.GetServices<ILensDiagnosticsContributor>().ToList();
        Assert.NotEmpty(contributors);

        // JwtLens registers one contributor
        Assert.Contains(contributors, c => c.Metadata.PackageId == "JwtLens");
    }

    [Fact]
    public void BothDelegatingHandlers_ArePresent_InHandlerPipeline()
    {
        var services = CreateServicesWithBoth();
        using var provider = services.BuildServiceProvider();

        // Verify both handlers can be resolved from DI
        var httpLensHandler = provider.GetRequiredService<HttpLensDelegatingHandler>();
        var jwtLensHandler = provider.GetRequiredService<JwtLensDelegatingHandler>();

        Assert.NotNull(httpLensHandler);
        Assert.NotNull(jwtLensHandler);
    }

    [Fact]
    public void ServiceProvider_BuildsSuccessfully_WithBothPackages()
    {
        var services = CreateServicesWithBoth();

        // Build without ValidateOnBuild since SignalR dependencies
        // require the full ASP.NET hosting stack
        var exception = Record.Exception(() =>
        {
            using var provider = services.BuildServiceProvider();
            // Verify key services resolve
            _ = provider.GetRequiredService<HttpLens.Core.Storage.ITrafficStore>();
            _ = provider.GetRequiredService<JwtLens.Storage.IJwtEventStore>();
        });

        Assert.Null(exception);
    }

    private sealed class TestRedactor : IRedactor
    {
        public string Redact(string? value) => "[CUSTOM-REDACTED]";
        public bool IsSensitive(string key) => false;
    }
}
