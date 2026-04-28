using HttpLens.Core.Configuration;
using HttpLens.Core.Interceptors;
using HttpLens.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

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

        TryAddSignalR(services);
        services.AddSingleton<InMemoryTrafficStore>();
        services.AddSingleton<SqliteTrafficStore>();
        services.AddSingleton<ITrafficStore>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<HttpLensOptions>>();
            return options.Value.EnableSqlitePersistence
                ? sp.GetRequiredService<SqliteTrafficStore>()
                : sp.GetRequiredService<InMemoryTrafficStore>();
        });
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
        TryAddTrafficHubNotifier(services);

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
            return services;
        }

        return services.AddHttpLens(configure);
    }

    private static void TryAddTrafficHubNotifier(IServiceCollection services)
    {
        const string typeName = "HttpLens.Dashboard.Hubs.TrafficHubNotifier, HttpLens.Dashboard";
        var notifierType = Type.GetType(typeName, throwOnError: false);
        if (notifierType is null || !typeof(IHostedService).IsAssignableFrom(notifierType))
            return;

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton(typeof(IHostedService), notifierType));
    }

    private static void TryAddSignalR(IServiceCollection services)
    {
        const string extensionsTypeName = "Microsoft.Extensions.DependencyInjection.SignalRDependencyInjectionExtensions, Microsoft.AspNetCore.SignalR";
        var extensionsType = Type.GetType(extensionsTypeName, throwOnError: false);
        if (extensionsType is null)
            return;

        var addSignalRMethod = extensionsType?
            .GetMethod("AddSignalR", [typeof(IServiceCollection)]);
        if (addSignalRMethod is null)
        {
            System.Diagnostics.Trace.WriteLine("HttpLens: SignalR extensions type was found, but AddSignalR(IServiceCollection) was not found.");
            return;
        }

        try
        {
            _ = addSignalRMethod.Invoke(null, [services]);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"HttpLens: failed to invoke AddSignalR(IServiceCollection): {ex}");
        }
    }
}
