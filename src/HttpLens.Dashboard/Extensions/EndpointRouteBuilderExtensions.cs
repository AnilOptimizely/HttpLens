using HttpLens.Core.Configuration;
using HttpLens.Dashboard.Api;
using HttpLens.Dashboard.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HttpLens.Dashboard.Extensions;

/// <summary>Extension methods for mounting the HttpLens dashboard onto a route builder.</summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Mounts the HttpLens dashboard SPA and its JSON API at <paramref name="path"/>.
    /// Security middleware (<see cref="EnabledGuardMiddleware"/>, <see cref="IpAllowlistMiddleware"/>,
    /// <see cref="ApiKeyAuthMiddleware"/>) is applied automatically — no additional
    /// <c>UseMiddleware</c> calls are required in the host application.
    /// </summary>
    /// <param name="endpoints">The application's endpoint route builder.</param>
    /// <param name="path">The base URL path. Defaults to <c>/_httplens</c>.</param>
    public static IEndpointRouteBuilder MapHttpLensDashboard(
        this IEndpointRouteBuilder endpoints,
        string path = "/_httplens")
    {
        // Resolve the authorization policy from current options.
        var optionsMonitor = endpoints.ServiceProvider.GetRequiredService<IOptionsMonitor<HttpLensOptions>>();
        var authorizationPolicy = optionsMonitor.CurrentValue.AuthorizationPolicy;

        // Register the JSON API endpoints with the optional authorization policy.
        endpoints.MapHttpLensApi(path, authorizationPolicy);

        // Build a security sub-pipeline that wraps each SPA route.
        // Order: EnabledGuard → IpAllowlist → ApiKey → handler.
        RequestDelegate BuildSecuredHandler(RequestDelegate handler)
        {
            var appBuilder = endpoints.CreateApplicationBuilder();
            appBuilder.UseMiddleware<EnabledGuardMiddleware>(path);
            appBuilder.UseMiddleware<IpAllowlistMiddleware>(path);
            appBuilder.UseMiddleware<ApiKeyAuthMiddleware>(path);
            appBuilder.Run(handler);
            return appBuilder.Build();
        }

        // Map a catch-all route for static SPA assets through the security pipeline.
        var catchAllPipeline = BuildSecuredHandler(async context =>
        {
            var slug = (string?)context.Request.RouteValues["slug"] ?? string.Empty;
            var resourceName = BuildResourceName(slug);

            if (!DashboardMiddleware.TryServeResource(resourceName, context))
            {
                // SPA fallback — serve index.html for unknown paths.
                context.Response.ContentType = "text/html; charset=utf-8";
                DashboardMiddleware.TryServeResource(DashboardMiddleware.IndexHtmlResourceName, context);
            }

            await Task.CompletedTask;
        });

        var catchAll = endpoints.Map($"{path}/{{**slug}}", catchAllPipeline).ExcludeFromDescription();

        // Map the base path itself through the security pipeline to serve index.html.
        var basePipeline = BuildSecuredHandler(async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            DashboardMiddleware.TryServeResource(DashboardMiddleware.IndexHtmlResourceName, context);
            await Task.CompletedTask;
        });

        var baseRoute = endpoints.Map(path, basePipeline).ExcludeFromDescription();

        // Apply ASP.NET Core authorization policy to SPA routes when configured.
        if (!string.IsNullOrEmpty(authorizationPolicy))
        {
            catchAll.RequireAuthorization(authorizationPolicy);
            baseRoute.RequireAuthorization(authorizationPolicy);
        }

        return endpoints;
    }

    private static string BuildResourceName(string slug)
    {
        // Convert URL path segments to embedded resource name convention.
        // e.g. "js/httplens.bundle.js" → "HttpLens.Dashboard.wwwroot.js.httplens.bundle.js"
        var normalized = slug.Replace('/', '.').TrimStart('.');
        return $"HttpLens.Dashboard.wwwroot.{normalized}";
    }
}
