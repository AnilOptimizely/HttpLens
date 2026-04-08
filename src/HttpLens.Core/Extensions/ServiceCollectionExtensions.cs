using HttpLens.Core.Configuration;
using HttpLens.Core.Interceptors;
using HttpLens.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace HttpLens.Core.Extensions;

/// <summary>Registers HttpLens services into the DI container.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the HttpLens traffic store, delegating handler, and wires the handler
    /// to every <c>HttpClient</c> registered via <c>IHttpClientFactory</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional callback to customise <see cref="HttpLensOptions"/>.</param>
    public static IServiceCollection AddHttpLens(
        this IServiceCollection services,
        Action<HttpLensOptions>? configure = null)
    {
        if (configure is not null)
            services.Configure(configure);
        else
            services.Configure<HttpLensOptions>(_ => { });

        services.AddSingleton<ITrafficStore, InMemoryTrafficStore>();
        services.AddTransient<HttpLensDelegatingHandler>();

        // Auto-attach to every named/typed HttpClient registered via IHttpClientFactory.
        services.ConfigureAll<HttpClientFactoryOptions>(factoryOptions =>
        {
            factoryOptions.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                var handler = builder.Services.GetRequiredService<HttpLensDelegatingHandler>();
                builder.AdditionalHandlers.Add(handler);
            });
        });

        // Process-wide interception via DiagnosticListener for manually-newed HttpClient instances.
        services.AddSingleton<DiagnosticInterceptor>();
        services.AddHostedService<DiagnosticInterceptorHostedService>();

        return services;
    }

    /// <summary>
    /// Adds the HttpLens traffic store, delegating handler, and wires the handler
    /// to every <c>HttpClient</c> registered via <c>IHttpClientFactory</c>.
    /// When <see cref="HttpLensOptions.AllowedEnvironments"/> is configured, HttpLens is only
    /// registered if the current hosting environment is in that list.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The current hosting environment used to check <see cref="HttpLensOptions.AllowedEnvironments"/>.</param>
    /// <param name="configure">Optional callback to customise <see cref="HttpLensOptions"/>.</param>
    public static IServiceCollection AddHttpLens(
        this IServiceCollection services,
        IHostEnvironment environment,
        Action<HttpLensOptions>? configure = null)
    {
        var tempOptions = new HttpLensOptions();
        configure?.Invoke(tempOptions);

        if (tempOptions.AllowedEnvironments.Count > 0 &&
            !tempOptions.AllowedEnvironments.Contains(
                environment.EnvironmentName, StringComparer.OrdinalIgnoreCase))
        {
            // Environment not in allowlist — don't register anything (zero overhead).
            return services;
        }

        return services.AddHttpLens(configure);
    }
}
