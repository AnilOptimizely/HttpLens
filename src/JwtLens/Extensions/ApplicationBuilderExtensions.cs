using JwtLens.Middleware;
using Microsoft.AspNetCore.Builder;

namespace JwtLens.Extensions;

/// <summary>
/// Extension methods for registering JwtLens middleware in the ASP.NET Core pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the JwtLens middleware to the request pipeline.
    /// This middleware inspects inbound requests for JWT ******
    /// </summary>
    /// <param name="app">The application builder.</param>
    public static IApplicationBuilder UseJwtLens(this IApplicationBuilder app)
    {
        return app.UseMiddleware<JwtLensMiddleware>();
    }
}
