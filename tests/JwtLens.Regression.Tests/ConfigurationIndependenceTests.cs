using HttpLens.Core.Configuration;
using HttpLens.Core.Extensions;
using JwtLens.Configuration;
using JwtLens.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace JwtLens.Regression.Tests;

/// <summary>
/// Category 8: Configuration Independence — verifies HttpLensOptions and JwtLensOptions
/// are fully independent and don't interfere with each other.
/// </summary>
public class ConfigurationIndependenceTests
{
    [Fact]
    public void HttpLensOptions_AndJwtLensOptions_AreIndependent()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        services.AddHttpLens(o =>
        {
            o.IsEnabled = false;
            o.MaxStoredRecords = 100;
        });
        services.AddJwtLens(o =>
        {
            o.IsEnabled = true;
            o.MaxStoredEvents = 50;
        });

        using var provider = services.BuildServiceProvider();

        var httpOptions = provider.GetRequiredService<IOptions<HttpLensOptions>>().Value;
        var jwtOptions = provider.GetRequiredService<IOptions<JwtLensOptions>>().Value;

        Assert.False(httpOptions.IsEnabled);
        Assert.True(jwtOptions.IsEnabled);
        Assert.Equal(100, httpOptions.MaxStoredRecords);
        Assert.Equal(50, jwtOptions.MaxStoredEvents);
    }

    [Fact]
    public void ChangingHttpLensOptions_DoesNotAffectJwtLensOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        services.AddHttpLens(o =>
        {
            o.ExcludeUrlPatterns = ["*health*"];
            o.SensitiveHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "X-Custom" };
        });
        services.AddJwtLens();

        using var provider = services.BuildServiceProvider();

        var jwtOptions = provider.GetRequiredService<IOptions<JwtLensOptions>>().Value;

        // JwtLens defaults should be unchanged
        Assert.True(jwtOptions.IsEnabled);
        Assert.Equal(200, jwtOptions.MaxStoredEvents);
        Assert.True(jwtOptions.CaptureOutboundTokens);
        Assert.True(jwtOptions.CaptureInboundTokens);
    }

    [Fact]
    public void ChangingJwtLensOptions_DoesNotAffectHttpLensOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        services.AddHttpLens();
        services.AddJwtLens(o =>
        {
            o.WarnIfExpiresWithin = TimeSpan.FromMinutes(30);
            o.FlagWeakAlgorithms = false;
            o.TrackClaimDiffs = false;
            o.MaxStoredEvents = 10;
        });

        using var provider = services.BuildServiceProvider();

        var httpOptions = provider.GetRequiredService<IOptions<HttpLensOptions>>().Value;

        // HttpLens defaults should be unchanged
        Assert.True(httpOptions.IsEnabled);
        Assert.Equal(500, httpOptions.MaxStoredRecords);
        Assert.True(httpOptions.CaptureRequestBody);
        Assert.True(httpOptions.CaptureResponseBody);
    }

    [Fact]
    public void OptionsMonitor_IndependentReloads()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        services.AddHttpLens();
        services.AddJwtLens();

        using var provider = services.BuildServiceProvider();

        var httpMonitor = provider.GetRequiredService<IOptionsMonitor<HttpLensOptions>>();
        var jwtMonitor = provider.GetRequiredService<IOptionsMonitor<JwtLensOptions>>();

        // Both should provide their defaults independently
        Assert.True(httpMonitor.CurrentValue.IsEnabled);
        Assert.True(jwtMonitor.CurrentValue.IsEnabled);
        Assert.Equal(500, httpMonitor.CurrentValue.MaxStoredRecords);
        Assert.Equal(200, jwtMonitor.CurrentValue.MaxStoredEvents);
    }

    [Fact]
    public void NamedOptions_DoNotCollide()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        // Register with explicit names to verify no collision
        services.Configure<HttpLensOptions>("HttpLens", o => o.MaxStoredRecords = 999);
        services.Configure<JwtLensOptions>("JwtLens", o => o.MaxStoredEvents = 111);

        services.AddHttpLens();
        services.AddJwtLens();

        using var provider = services.BuildServiceProvider();

        var httpSnapshot = provider.GetRequiredService<IOptionsSnapshot<HttpLensOptions>>();
        var jwtSnapshot = provider.GetRequiredService<IOptionsSnapshot<JwtLensOptions>>();

        // Named options should be independent
        Assert.Equal(999, httpSnapshot.Get("HttpLens").MaxStoredRecords);
        Assert.Equal(111, jwtSnapshot.Get("JwtLens").MaxStoredEvents);

        // Default (unnamed) options should be separate
        var httpDefault = httpSnapshot.Get(Options.DefaultName);
        var jwtDefault = jwtSnapshot.Get(Options.DefaultName);
        Assert.NotEqual(999, httpDefault.MaxStoredRecords); // Default is 500
        Assert.NotEqual(111, jwtDefault.MaxStoredEvents);   // Default is 200
    }
}
