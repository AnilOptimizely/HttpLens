using JwtLens.Middleware;
using JwtLens.Storage;
using Lens.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;

namespace JwtLens.Extensions;

/// <summary>
/// Extension methods for registering JwtLens services.
/// </summary>
public static class JwtLensServiceCollectionExtensions
{
    /// <summary>
    /// Adds JwtLens services to the DI container.
    /// </summary>
    public static IServiceCollection AddJwtLens(this IServiceCollection services, int maxEvents = 1000)
    {
        services.AddSingleton(new JwtEventStore(maxEvents));
        services.AddSingleton<ClaimDiffTracker>();
        services.AddSingleton<JwtLensDiagnosticsContributor>();
        services.AddSingleton<ILensDiagnosticsContributor>(sp => sp.GetRequiredService<JwtLensDiagnosticsContributor>());
        services.AddTransient<JwtOutboundHandler>();

        // Auto-attach outbound handler to all HttpClient instances
        services.ConfigureAll<HttpClientFactoryOptions>(factoryOptions =>
        {
            factoryOptions.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                var handler = builder.Services.GetRequiredService<JwtOutboundHandler>();
                builder.AdditionalHandlers.Add(handler);
            });
        });

        return services;
    }

    /// <summary>
    /// Adds JwtLens inbound JWT capture middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseJwtLens(this IApplicationBuilder app)
    {
        return app.UseMiddleware<JwtCaptureMiddleware>();
    }
}
