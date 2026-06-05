using JwtLens.Analysis;
using JwtLens.Configuration;
using JwtLens.Interceptors;
using JwtLens.Storage;
using Lens.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace JwtLens.Extensions;

/// <summary>
/// Extension methods for registering JwtLens services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds JwtLens services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional callback to configure <see cref="JwtLensOptions"/>.</param>
    public static IServiceCollection AddJwtLens(
        this IServiceCollection services,
        Action<JwtLensOptions>? configure = null)
    {
        if (configure is not null)
            services.Configure(configure);

        services.TryAddSingleton<IRedactor>(new DefaultRedactor());
        services.AddSingleton<ClaimDiffTracker>();
        services.AddSingleton<IJwtEventStore, InMemoryJwtEventStore>();
        services.AddSingleton<ILensDiagnosticsContributor, JwtLensDiagnosticsContributor>();
        services.AddTransient<JwtLensDelegatingHandler>();

        return services;
    }

    /// <summary>
    /// Adds JwtLens services to the service collection with environment-based activation.
    /// When <see cref="JwtLensOptions.AllowedEnvironments"/> is configured, JwtLens is only
    /// registered if the current hosting environment is in that list.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The current hosting environment.</param>
    /// <param name="configure">Optional callback to configure <see cref="JwtLensOptions"/>.</param>
    public static IServiceCollection AddJwtLens(
        this IServiceCollection services,
        IHostEnvironment environment,
        Action<JwtLensOptions>? configure = null)
    {
        var tempOptions = new JwtLensOptions();
        configure?.Invoke(tempOptions);

        if (tempOptions.AllowedEnvironments.Count > 0 &&
            !EnvironmentGuard.IsAllowed(environment, tempOptions.AllowedEnvironments))
        {
            return services;
        }

        return services.AddJwtLens(configure);
    }
}
