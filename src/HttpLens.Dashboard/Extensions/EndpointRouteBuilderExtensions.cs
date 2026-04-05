using HttpLens.Dashboard.Api;
using HttpLens.Dashboard.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HttpLens.Dashboard.Extensions;

/// <summary>Extension methods for mounting the HttpLens dashboard onto a route builder.</summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Mounts the HttpLens dashboard SPA and its JSON API at <paramref name="path"/>.
    /// </summary>
    /// <param name="endpoints">The application's endpoint route builder.</param>
    /// <param name="path">The base URL path. Defaults to <c>/_httplens</c>.</param>
    public static IEndpointRouteBuilder MapHttpLensDashboard(
        this IEndpointRouteBuilder endpoints,
        string path = "/_httplens")
    {
        // Register the JSON API endpoints.
        endpoints.MapHttpLensApi(path);

        // Map a catch-all route for static SPA assets.
        endpoints.Map($"{path}/{{**slug}}", async context =>
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

        // Also map the base path itself to serve index.html.
        endpoints.Map(path, async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            DashboardMiddleware.TryServeResource(DashboardMiddleware.IndexHtmlResourceName, context);
            await Task.CompletedTask;
        });

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
