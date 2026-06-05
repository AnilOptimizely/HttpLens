using Xunit;
using JwtLens.Analysis;
using JwtLens.Configuration;
using JwtLens.Extensions;
using JwtLens.Storage;
using Lens.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace JwtLens.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddJwtLens_RegistersAllRequiredServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddJwtLens();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IJwtEventStore>());
        Assert.NotNull(provider.GetService<ILensDiagnosticsContributor>());
        Assert.NotNull(provider.GetService<ClaimDiffTracker>());
        Assert.NotNull(provider.GetService<IRedactor>());
    }

    [Fact]
    public void AddJwtLens_WithOptions_AppliesConfiguration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddJwtLens(o =>
        {
            o.WarnIfExpiresWithin = TimeSpan.FromMinutes(10);
            o.MaxStoredEvents = 500;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtLensOptions>>();

        Assert.Equal(TimeSpan.FromMinutes(10), options.Value.WarnIfExpiresWithin);
        Assert.Equal(500, options.Value.MaxStoredEvents);
    }

    [Fact]
    public void AddJwtLens_DoesNotOverrideExistingRedactor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        var customRedactor = new DefaultRedactor(["custom_field"]);
        services.AddSingleton<IRedactor>(customRedactor);
        services.AddJwtLens();

        var provider = services.BuildServiceProvider();
        var redactor = provider.GetRequiredService<IRedactor>();

        Assert.True(redactor.IsSensitive("custom_field"));
    }

    [Fact]
    public void AddJwtLens_StoreIsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddJwtLens();

        var provider = services.BuildServiceProvider();

        var store1 = provider.GetRequiredService<IJwtEventStore>();
        var store2 = provider.GetRequiredService<IJwtEventStore>();

        Assert.Same(store1, store2);
    }
}
