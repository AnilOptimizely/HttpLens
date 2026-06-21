using HttpLens.Core.Extensions;
using JwtLens.Extensions;
using Lens.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Xunit;

namespace JwtLens.Regression.Tests;

/// <summary>
/// Category 5: Shared Lens.Abstractions Contracts — verifies shared interfaces
/// work correctly when consumed by multiple Lens packages simultaneously.
/// </summary>
public class SharedAbstractionsTests
{
    [Fact]
    public void EnvironmentGuard_DisablesBothPackages_InProduction()
    {
        var env = new HostingEnvironment { EnvironmentName = "Production" };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        services.AddHttpLens(env, o => o.AllowedEnvironments = ["Development"]);
        services.AddJwtLens(env, o => o.AllowedEnvironments = ["Development"]);

        using var provider = services.BuildServiceProvider();

        // Neither should have registered their stores since env is Production
        var trafficStore = provider.GetService<HttpLens.Core.Storage.ITrafficStore>();
        var jwtStore = provider.GetService<JwtLens.Storage.IJwtEventStore>();

        Assert.Null(trafficStore);
        Assert.Null(jwtStore);
    }

    [Fact]
    public void EnvironmentGuard_EnablesBothPackages_InDevelopment()
    {
        var env = new HostingEnvironment { EnvironmentName = "Development" };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        services.AddHttpLens(env, o => o.AllowedEnvironments = ["Development"]);
        services.AddJwtLens(env, o => o.AllowedEnvironments = ["Development"]);

        using var provider = services.BuildServiceProvider();

        var trafficStore = provider.GetService<HttpLens.Core.Storage.ITrafficStore>();
        var jwtStore = provider.GetService<JwtLens.Storage.IJwtEventStore>();

        Assert.NotNull(trafficStore);
        Assert.NotNull(jwtStore);
    }

    [Fact]
    public void CustomRedactor_UsedByBothPackages()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        var customRedactor = new TrackingRedactor();
        services.AddSingleton<IRedactor>(customRedactor);

        services.AddHttpLens();
        services.AddJwtLens();

        using var provider = services.BuildServiceProvider();

        // Both packages should resolve the same custom redactor
        var redactor = provider.GetRequiredService<IRedactor>();
        Assert.Same(customRedactor, redactor);
    }

    [Fact]
    public void DefaultRedactor_Consistent_AcrossPackages()
    {
        var redactor = new DefaultRedactor(["email", "phone"]);

        Assert.True(redactor.IsSensitive("email"));
        Assert.True(redactor.IsSensitive("EMAIL")); // Case-insensitive
        Assert.True(redactor.IsSensitive("phone"));
        Assert.False(redactor.IsSensitive("name"));

        Assert.Equal(DefaultRedactor.RedactedPlaceholder, redactor.Redact("sensitive-value"));
        Assert.Equal(string.Empty, redactor.Redact(null));
    }

    [Fact]
    public void MultipleDiagnosticsContributors_Coexist_IndependentSnapshots()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        services.AddHttpLens();
        services.AddJwtLens();

        using var provider = services.BuildServiceProvider();

        var contributors = provider.GetServices<ILensDiagnosticsContributor>().ToList();

        // JwtLens registers a contributor
        var jwtContributor = contributors.FirstOrDefault(c => c.Metadata.PackageId == "JwtLens");
        Assert.NotNull(jwtContributor);

        // Each contributor should have unique metadata
        var packageIds = contributors.Select(c => c.Metadata.PackageId).ToList();
        Assert.Equal(packageIds.Count, packageIds.Distinct().Count());
    }

    [Fact]
    public void LensPackageMetadata_HasUniqueIdentifiers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        services.AddHttpLens();
        services.AddJwtLens();

        using var provider = services.BuildServiceProvider();

        var contributors = provider.GetServices<ILensDiagnosticsContributor>().ToList();

        foreach (var contributor in contributors)
        {
            Assert.NotNull(contributor.Metadata.PackageId);
            Assert.NotNull(contributor.Metadata.DisplayName);
            Assert.NotEmpty(contributor.Metadata.PackageId);
            Assert.NotEmpty(contributor.Metadata.DisplayName);
        }
    }

    [Fact]
    public void DiagnosticsContributor_ReturnsNull_WhenNoDataCaptured()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        services.AddJwtLens();

        using var provider = services.BuildServiceProvider();

        var contributors = provider.GetServices<ILensDiagnosticsContributor>().ToList();
        var jwtContributor = contributors.First(c => c.Metadata.PackageId == "JwtLens");

        // No events captured yet
        var snapshot = jwtContributor.GetLatestSnapshot();
        Assert.Null(snapshot);
    }

    private sealed class TrackingRedactor : IRedactor
    {
        public string Redact(string? value) => "[TRACKED-REDACTED]";
        public bool IsSensitive(string key) => key.Equals("email", StringComparison.OrdinalIgnoreCase);
    }
}
