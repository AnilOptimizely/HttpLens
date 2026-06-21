using JwtLens.Middleware;
using JwtLens.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace JwtLens.Extensions;

/// <summary>
/// Extension methods for registering JwtLens middleware in the ASP.NET Core pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the JwtLens middleware to the request pipeline.
    /// This middleware inspects inbound requests for JWT bearer tokens.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public static IApplicationBuilder UseJwtLens(this IApplicationBuilder app)
    {
        if (app.ApplicationServices.GetService<IJwtEventStore>() is null)
        {
            return app;
        }

        return app.UseMiddleware<JwtLensMiddleware>();
    }
}
